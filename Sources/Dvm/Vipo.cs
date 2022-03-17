using System;
using System.Collections.Generic;

namespace Dvm
{
	public abstract class Vipo : DisposableObject
	{
		readonly VirtualMachine m_vm;
		readonly Vid m_vid;

		public bool IsAttached { get; internal set; }
		List<VipoMessage> m_outMessages;
		Exception m_exception;

		#region Properties

		public VirtualMachine VM => m_vm;
		public Vid Vid => m_vid;
		public string Symbol => m_vid.Symbol;
		public Exception Exception => m_exception;

		#endregion

		#region Initialization

		protected Vipo(VirtualMachine vm, string symbol)
		{
			m_vm = vm ?? throw new ArgumentNullException(nameof(vm));
			m_vid = vm.VidAllocator.New(symbol);
		}

		protected override void OnDispose(bool explicitCall)
		{
			if (explicitCall)
				Detach();
		}

		public override string ToString() => "Vipo " + m_vid.ToString();

		#endregion

		#region Run

		public void Schedule(object context = null) // Can be called in any thread
		{
			m_vm.AddScheduleRequest(new VipoSchedule(this, context));
		}

		public void Detach() // Can be called in any thread
		{
			m_vm.AddScheduleRequest(new VipoDetach(this));
		}

		internal void RunEntry(VipoJob job)
		{
			if (job.IsEmpty)
				throw new KernelFaultException("Empty job to run");

			try
			{
				Run(job);
			}
			catch (Exception e)
			{
				m_exception = e;

				OnError(e);
			}
		}

		#region Run handlers

		// All handlers are called in VmProcessor threads

		protected abstract void Run(VipoJob job);

		// OnError should not raise an exception; if it does, the VM event OnError can be invoked.
		protected virtual void OnError(Exception e)
		{
			Detach();
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
