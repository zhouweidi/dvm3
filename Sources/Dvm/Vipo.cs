using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace Dvm
{
	// Ending a vipo can be started with a call to IDisposable.Dispose() on a vipo in any threads.
	// * It is an async process including 2 step:
	//   1. Unregister it from VM.
	//   2. Dispose the resources an user allocated for it, done by the user handler OnDispose().
	//     - OnDispose() is used to dispose user resources and the final step of the ending process. It is not the same as
	//       DisposableObject.OnDispose(bool) which is called when the vipo ending process starts. OnDispose() gets called:
	//       - As the final run, in VmProcessor threads during VM works;
	//		 - Or, along with VM disposing, in the same thread as VM.Dispose().
	// * VM ending process can end (dispose) vipos registered to it.

	public abstract class Vipo : DisposableObject
	{
		readonly VirtualMachine m_vm;
		readonly string m_symbol;
		readonly Vid m_vid;

		List<VipoMessage> m_outMessages;
		readonly ConcurrentQueue<VipoMessage> m_inMessages = new ConcurrentQueue<VipoMessage>();
		int m_pendingInMessagesCount;
		Status m_status;

		#region Properties

		public VirtualMachine VM => m_vm;
		public Vid Vid => m_vid;
		public string Symbol => m_symbol ?? string.Empty;

		#endregion

		[Flags]
		enum Status : byte
		{
			Running = 1,
			Disposed = 2,
		}

		#region Initialization

		protected Vipo(VirtualMachine vm, string symbol)
		{
			m_vm = vm ?? throw new ArgumentNullException(nameof(vm));
			m_symbol = symbol;
			m_vid = vm.Register(this);
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

					if (IsOnDisposeOverridden())
					{
						var vipoMessage = SystemScheduleMessage.Dispose.CreateVipoMessage();
						InputMessage(vipoMessage);
					}
				}
			}
		}

		static readonly Type[] EmptyTypeArray = new Type[0];

		bool IsOnDisposeOverridden()
		{
			var onDisposeMethod = GetType().GetMethod(
				nameof(OnDispose),
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				EmptyTypeArray,
				null);

			return onDisposeMethod.DeclaringType != typeof(Vipo);
		}

		public override string ToString() => "Vipo " + m_vid.ToString();

		#endregion

		#region Run

		public void Schedule(object context = null) // Can be called in any thread
		{
			CheckDisposed();

			var vipoMessage = UserScheduleMessage.CreateVipoMessage(context);
			InputMessage(vipoMessage);
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
			if ((m_status & Status.Running) != 0)
				throw new KernelFaultException("The vipo is already running");

			// Any messages may come after SystemScheduleMessage.Dispose processed
			if ((m_status & Status.Disposed) != 0)
				return;

			m_status |= Status.Running;

			var messagesToProcess = Interlocked.Exchange(ref m_pendingInMessagesCount, 0);
			if (messagesToProcess == 0)
				throw new KernelFaultException("No message to process in a vipo circle");

			try
			{
				var messageStream = new VipoMessageStream(m_inMessages, messagesToProcess);

				var firstIsDisposeMessage = messageStream.DisposeMessageEncountered;
				if (!firstIsDisposeMessage)
				{
					Run(messageStream);

					if (m_outMessages != null && m_outMessages.Count > 0)
						DispatchMessages();
				}

				messageStream.ConsumeRemaining();
				if (messageStream.DisposeMessageEncountered)
				{
					OnDispose();
					m_status |= Status.Disposed;
				}
			}
			catch (Exception e)
			{
				OnError(e);
			}

			m_status &= ~Status.Running;
		}

		void DispatchMessages()
		{
			for (int i = 0; i < m_outMessages.Count; i++)
			{
				var message = m_outMessages[i];

				var vipo = message.To.ResolveVipo();
				if (vipo == null)
					vipo = m_vm.FindVipo(message.To);

				if (vipo == null)
				{
					if (m_vm.Inspector != null)
						m_vm.Inspector.IncreaseDiscardedMessage();

					continue;
				}

				vipo.InputMessage(message);
			}

			m_outMessages.Clear();
		}

		#region Run handlers

		// All handlers are called in VmProcessor threads

		protected abstract void Run(VipoMessageStream messageStream);

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

		protected void Send(Vid to, Message message) // Can be called in VmProcessor threads only
		{
			CheckDisposed();

			VmProcessor.CheckWorkingVipo(this, "It is not allowed to call Send outside of Vipo.Run");

			if (to.IsEmpty)
				throw new ArgumentException("To is empty", nameof(to));

			if (message == null)
				throw new ArgumentException("Message is null", nameof(message));

			var vipoMessage = new VipoMessage(m_vid, to, message);

			// Add to out messages
			if (m_outMessages == null)
				m_outMessages = new List<VipoMessage>();

			m_outMessages.Add(vipoMessage);
		}

		#endregion
	}
}
