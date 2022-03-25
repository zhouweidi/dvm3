using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

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

		List<VipoMessage> m_outMessages;
		readonly ConcurrentQueue<VipoMessage> m_inMessages = new ConcurrentQueue<VipoMessage>();
		int m_pendingInMessagesCount;

		static readonly SystemScheduleMessage SystemScheduleMessage_Dispose = new SystemScheduleMessage();
		public static int s_discardedMessages;

		#region Properties

		public VirtualMachine VM => m_vm;
		public Vid Vid => m_vid;
		public string Symbol => m_vid.Symbol;

		bool OnDisposeOverridden => GetType().GetMethod(
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
			m_vid = vm.Register(this, symbol);
		}

		protected sealed override void OnDispose(bool explicitCall)
		{
			if (explicitCall)
			{
				if (m_vm.Disposed)
					OnDispose();
				else
				{
					m_vm.Unregister(this);

					if (OnDisposeOverridden)
						InputMessage(SystemScheduleMessage_Dispose.CreateVipoMessage());
				}
			}
		}

		public override string ToString() => "Vipo " + m_vid.ToString();

		#endregion

		#region Run

		public void Schedule(object context = null) // Can be called in any thread
		{
			CheckDisposed();

			InputMessage(UserScheduleMessage.CreateVipoMessage(context));
		}

		void InputMessage(VipoMessage vipoMessage)
		{
			m_inMessages.Enqueue(vipoMessage);

			var incrementedValue = Interlocked.Increment(ref m_pendingInMessagesCount);
			if (incrementedValue == 1)
				m_vm.Schedule(this);
		}

		internal void RunEntry()
		{
			var messagesToProcess = Interlocked.Exchange(ref m_pendingInMessagesCount, 0);
			if (messagesToProcess == 0)
				throw new KernelFaultException("No message to process in a vipo circle");

			try
			{
				var messagess = TakeInMessages(messagesToProcess);
				Run(messagess);

				if (m_outMessages != null && m_outMessages.Count > 0)
					DispatchMessages();
			}
			catch (Exception e)
			{
				OnError(e);
			}
		}

		IEnumerable<VipoMessage> TakeInMessages(int count)
		{
			for (int i = 0; i < count; i++)
			{
				if (!m_inMessages.TryDequeue(out VipoMessage vipoMessage))
					throw new KernelFaultException("Not enough in messages to take");

				if (ReferenceEquals(vipoMessage.Message, SystemScheduleMessage_Dispose))
				{
					OnDispose();
					yield break;
					// TODO throw new EndException();
					// TODO only one execution allowed
				}

				yield return vipoMessage;
			}
		}

		void DispatchMessages()
		{
			foreach (var message in m_outMessages)
			{
				// TODO last vipo cache
				var vipo = m_vm.FindVipo(message.To);
				if (vipo == null)
				{
					Interlocked.Increment(ref s_discardedMessages);
					continue;
				}

				vipo.InputMessage(message);
			}

			m_outMessages.Clear();
		}

		#region Run handlers

		// All handlers are called in VmProcessor threads

		protected abstract void Run(IEnumerable<VipoMessage> vipoMessages);

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

		#region Sending messages

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

		#endregion
	}
}
