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
		VipoTimingComponent m_timing;

		[Flags]
		enum Status : byte
		{
			Running = 1,
			Disposed = 2,
		}

		#region Properties

		public VirtualMachine VM => m_vm;
		public Vid Vid => m_vid;
		public string Symbol => m_symbol ?? string.Empty;

		#endregion

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

		internal void InputMessage(VipoMessage vipoMessage)
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

			// Messages may come after SystemScheduleMessage.Dispose processed
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
					RunAndDispatch(messageStream);

				messageStream.ConsumeRemaining();

				if (messageStream.DisposeMessageEncountered)
					InvokeOnDispose();
				else
				{
					// Generate and process local timer messages
					if (messageStream.TimerMessageEncountered && m_timing == null)
						throw new KernelFaultException("Encountering a system timer message but no vipo timing component created");

					if (m_timing != null)
					{
						if (messageStream.TimerMessageEncountered)
						{
							var timerMessageStream = m_timing.TriggerTimers();
							if (timerMessageStream != null)
								RunAndDispatch(timerMessageStream);
						}

						m_timing.UpdateRequest();
					}
				}
			}
			catch (Exception e) when (!(e is OnDisposeException || e is KernelFaultException))
			{
				OnError(e);

				Dispose();
			}

			m_status &= ~Status.Running;
		}

		void RunAndDispatch(IVipoMessageStream messageStream)
		{
			OnRun(messageStream);

			if (m_outMessages != null && m_outMessages.Count > 0)
				DispatchMessages();
		}

		void DispatchMessages()
		{
			for (int i = 0; i < m_outMessages.Count; i++)
			{
				var message = m_outMessages[i];

				var vipo = message.To.ResolveVipo();
				if (vipo == null)
					vipo = m_vm.FindVipo(message.To);

				if (vipo == null || vipo.Disposed)
				{
					if (m_vm.Inspector != null)
						m_vm.Inspector.IncreaseDiscardedMessage();

					continue;
				}

				vipo.InputMessage(message);
			}

			m_outMessages.Clear();
		}

		class OnDisposeException : Exception
		{
			public OnDisposeException(Exception innerException)
				: base("An exception raised in Vipo.OnDispose", innerException)
			{
			}
		}

		void InvokeOnDispose()
		{
			try
			{
				OnDispose();
			}
			catch (Exception e)
			{
				throw new OnDisposeException(e);
			}

			m_status |= Status.Disposed;
		}

		#region Run handlers

		// All handlers are called in VmProcessor threads

		protected abstract void OnRun(IVipoMessageStream messageStream);

		// OnError should not raise an exception; if it does, the VM event OnError can be invoked.
		protected virtual void OnError(Exception e)
		{
		}

		protected virtual void OnDispose()
		{
		}

		#endregion

		#endregion

		#region Sending messages

		// Must be called in VmProcessor threads only
		protected void Send(Vid to, Message message)
		{
			CheckDisposed();

			VmProcessor.CheckWorkingVipo(this, "It is not allowed to call outside of Vipo.Run");

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

		#region Timer

		// Must be called in VmProcessor threads only
		protected int SetTimer(int milliseconds, object context = null)
		{
			CheckDisposed();

			VmProcessor.CheckWorkingVipo(this, "It is not allowed to call outside of Vipo.Run");

			if (milliseconds <= 0)
				throw new ArgumentException("<= 0", nameof(milliseconds));

			if (m_timing == null)
				m_timing = new VipoTimingComponent(this);

			return m_timing.CreateTimer(milliseconds, 0, context);
		}

		// Must be called in VmProcessor threads only
		protected int SetRepeatedTimer(int intervalMilliseconds, object context = null)
		{
			return SetRepeatedTimer(intervalMilliseconds, intervalMilliseconds, context);
		}

		// Must be called in VmProcessor threads only
		protected int SetRepeatedTimer(int milliseconds, int intervalMilliseconds, object context = null)
		{
			CheckDisposed();

			VmProcessor.CheckWorkingVipo(this, "It is not allowed to call outside of Vipo.Run");

			if (milliseconds <= 0)
				throw new ArgumentException("<= 0", nameof(milliseconds));

			if (intervalMilliseconds <= 0)
				throw new ArgumentException("<= 0", nameof(intervalMilliseconds));

			if (m_timing == null)
				m_timing = new VipoTimingComponent(this);

			return m_timing.CreateTimer(milliseconds, intervalMilliseconds, context);
		}

		// Must be called in VmProcessor threads only
		protected void ResetTimer(int timerId)
		{
			CheckDisposed();

			VmProcessor.CheckWorkingVipo(this, "It is not allowed to call outside of Vipo.Run");

			if (timerId <= 0)
				throw new ArgumentException("Invalid timer ID", nameof(timerId));

			if (m_timing == null)
				throw new InvalidOperationException("No vipo timer component");

			m_timing.DestroyTimer(timerId);
		}

		#endregion
	}
}
