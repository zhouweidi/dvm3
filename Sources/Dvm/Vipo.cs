using System;
using System.Collections.Generic;
using System.Reflection;

namespace Dvm
{
	// Ending a vipo can be started with a call to IDisposable.Dispose() on a vipo in any threads.
	// * It is an async process including 2 step:
	//   1. Detach it from VM (by the schedule request VipoDispose).
	//      Because we must ensure all the current/ongoing schedule requests and vipo jobs in the pipeline are processed before
	//      disposing the vipo since these tasks may use the resources an user allocates for the vipo.
	//   2. Dispose the resources an user allocated for it. (by the user handler OnDispose())
	//     - OnDispose() is used to dispose user resources and the end step of the ending process. It is not the same as
	//       DisposableObject.OnDispose(bool) which is called when the vipo ending process starts. OnDispose() runs in VmProcessor
	//       threads during VM works or in the same thread as the one calls VM.Dispose().
	//     - Vipo objects without OnDispose() overridden don't get the final run in VmProcessor threads while detaching it from VM.
	//     - Vipo objects with OnDispose() overridden always do regardless of whether they were attached to VM or not.
	// * VM ending process can end (dispose) vipos attached to it.
	// * An unattached vipo should be explicitly disposed by a Dispose() call whether before or after VM ends.

	public abstract class Vipo : DisposableObject
	{
		readonly VirtualMachine m_vm;
		readonly Vid m_vid;

		public bool IsAttached { get; internal set; }
		List<VipoMessage> m_outMessages;

		#region Properties

		public VirtualMachine VM => m_vm;
		public Vid Vid => m_vid;
		public string Symbol => m_vid.Symbol;

		internal bool OnDisposeOverridden => GetType().GetMethod(
			nameof(OnDispose),
			BindingFlags.NonPublic | BindingFlags.Instance,
			null,
			EmptyTypeArray,
			null).DeclaringType != typeof(Vipo);

		static readonly Type[] EmptyTypeArray = new Type[0];

		#endregion

		#region Initialization

		protected Vipo(VirtualMachine vm, string symbol)
		{
			m_vm = vm ?? throw new ArgumentNullException(nameof(vm));
			m_vid = vm.VidAllocator.New(symbol);
		}

		protected sealed override void OnDispose(bool explicitCall)
		{
			if (explicitCall)
			{
				if (m_vm.Disposed)
				{
					IsAttached = false;

					OnDispose();
				}
				else
					m_vm.AddScheduleRequest(new VipoDispose(this));
			}
		}

		public override string ToString() => "Vipo " + m_vid.ToString();

		#endregion

		#region Run

		public void Schedule(object context = null) // Can be called in any thread
		{
			CheckDisposed();

			m_vm.AddScheduleRequest(new VipoSchedule(this, context));
		}

		public void Detach() // Can be called in any thread
		{
			CheckDisposed();
		
			m_vm.AddScheduleRequest(new VipoDetach(this));
		}

		internal void RunEntry(VipoJob job)
		{
			if (job.IsEmpty)
				throw new KernelFaultException("Empty vipo job to run");

			try
			{
				if (job.Messages != null)
					Run(job.Messages);

				if (job.DisposeFlag)
					OnDispose();
			}
			catch (Exception e)
			{
				OnError(e);
			}
		}

		#region Run handlers

		// All handlers are called in VmProcessor threads

		protected abstract void Run(IReadOnlyList<VipoMessage> vipoMessages);

		// OnError should not raise an exception; if it does, the VM event OnError can be invoked.
		protected virtual void OnError(Exception e)
		{
			Dispose();
		}

		protected virtual void OnDispose()
		{
		}

		#endregion

		#endregion

		#region Message I/O

		public void Send(Vid to, Message message) // Can be called in VmProcessor threads only
		{
			var vipoMessage = new VipoMessage(m_vid, to, message);

			Send(vipoMessage);
		}

		public void Send(VipoMessage vipoMessage) // Can be called in VmProcessor threads only
		{
			CheckDisposed();

			if (!ReferenceEquals(VmProcessor.GetWorkingVipo(), this))
				throw new InvalidOperationException("It is not allowed to call Send outside of OnRun");

			if (vipoMessage.From.IsEmpty)
				throw new ArgumentException("VipoMessage.From is empty", nameof(vipoMessage));

			if (vipoMessage.To.IsEmpty)
				throw new ArgumentException("VipoMessage.To is empty", nameof(vipoMessage));

			if (vipoMessage.Message == null)
				throw new ArgumentException("VipoMessage.Message is null", nameof(vipoMessage));

			if (vipoMessage.From != m_vid)
				throw new ArgumentException("VipoMessage.From is not the sender", nameof(vipoMessage));

			// Add to out messages
			if (m_outMessages == null)
				m_outMessages = new List<VipoMessage>();

			m_outMessages.Add(vipoMessage);
		}

		internal IReadOnlyList<VipoMessage> TakeOutMessages()
		{
			if (m_outMessages == null || m_outMessages.Count == 0)
				return null;

			var result = m_outMessages;
			m_outMessages = null;

			return result;
		}

		#endregion
	}
}
