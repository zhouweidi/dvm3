using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Dvm
{
	public abstract class AsyncVipo : Vipo
	{
		Task m_task;
		readonly LinkedList<AsyncOperation> m_operations = new LinkedList<AsyncOperation>();

		#region Initialization

		public AsyncVipo(VirtualMachine vm, string symbol)
			: base(vm, symbol)
		{
		}

		protected override void OnDispose()
		{
			if (m_task != null)
				m_task.Dispose();

			base.OnDispose();
		}

		#endregion

		#region Run

		protected override void OnRun(IVipoMessageStream messageStream)
		{
			if (m_task == null)
				m_task = RunAsync();

			while (m_task.Exception == null && m_operations.Count > 0 && messageStream.GetNext(out VipoMessage vipoMessage))
			{
				var lastNode = m_operations.Last;

				for (var node = m_operations.First; node != null;)
				{
					bool isLastNode = node == lastNode;

					var op = node.Value;
					if (op.TestCompleted(ref vipoMessage))
					{
						var remove = node;
						node = node.Next;

						m_operations.Remove(remove);

						op.Continue();
					}
					else
						node = node.Next;

					if (isLastNode)
						break;
				}
			}

			if (m_task.Exception != null)
				throw new Exception("RunAsync exception", m_task.Exception);
		}

		protected abstract Task RunAsync();

		#endregion

		#region AsyncOperation

		public abstract class AsyncOperation : INotifyCompletion
		{
			protected readonly AsyncVipo m_vipo;
			Action m_continuation;

			protected AsyncOperation(AsyncVipo vipo)
			{
				m_vipo = vipo;
			}

			internal protected abstract bool TestCompleted(ref VipoMessage vipoMessage);

			internal void Continue()
			{
				m_continuation.Invoke();
			}

			#region INotifyCompletion

			void INotifyCompletion.OnCompleted(Action continuation)
			{
				// Start the task and call back to 'continuation' when completed
				m_vipo.m_operations.AddLast(this);

				m_continuation = continuation;
			}

			#endregion
		}

		#endregion

		#region ReceiveOperation

		public class ReceiveOperation : AsyncOperation
		{
			readonly int m_timerId;
			readonly Func<VipoMessage, bool> m_condition;
			VipoMessage m_vipoMessage;
			bool m_completed;

			internal ReceiveOperation(AsyncVipo vipo, int timeoutMilliseconds, Func<VipoMessage, bool> condition)
				: base(vipo)
			{
				if (timeoutMilliseconds > 0)
					m_timerId = vipo.SetTimer(timeoutMilliseconds);

				m_condition = condition;
			}

			#region Await methods

			public bool IsCompleted => m_completed;

			public VipoMessage GetResult() => m_vipoMessage;

			#endregion

			internal protected override bool TestCompleted(ref VipoMessage vipoMessage)
			{
				if (m_timerId != 0 &&
					vipoMessage.Message is UserTimerMessage timer &&
					timer.TimerId == m_timerId)
				{
					m_completed = true;
					return true;
				}

				if (m_condition == null || m_condition(vipoMessage))
				{
					m_vipoMessage = vipoMessage;
					m_completed = true;

					if (m_timerId != 0)
						m_vipo.ResetTimer(m_timerId);

					return true;
				}

				return false;
			}
		}

		#endregion

		#region SleepOperation

		public class SleepOperation : AsyncOperation
		{
			readonly int m_timerId;
			bool m_completed;

			internal SleepOperation(AsyncVipo vipo, int timeoutMilliseconds)
				: base(vipo)
			{
				if (timeoutMilliseconds <= 0)
					throw new ArgumentException("<= 0", nameof(timeoutMilliseconds));

				m_timerId = vipo.SetTimer(timeoutMilliseconds);
			}

			#region Await methods

			public bool IsCompleted => m_completed;

			public void GetResult() { }

			#endregion

			internal protected override bool TestCompleted(ref VipoMessage vipoMessage)
			{
				if (vipoMessage.Message is UserTimerMessage timer && timer.TimerId == m_timerId)
				{
					m_completed = true;
					return true;
				}

				return false;
			}
		}

		#endregion

		#region Primitives

		protected ReceiveOperation Receive(int timeoutMilliseconds = 0)
		{
			return new ReceiveOperation(this, timeoutMilliseconds, null);
		}

		protected ReceiveOperation Receive<TMessage>(int timeoutMilliseconds = 0)
			where TMessage : Message
		{
			return new ReceiveOperation(this, timeoutMilliseconds, vm => vm.Message is TMessage);
		}

		protected ReceiveOperation Receive(Func<VipoMessage, bool> condition, int timeoutMilliseconds = 0)
		{
			return new ReceiveOperation(this, timeoutMilliseconds, condition);
		}

		protected ReceiveOperation ReceiveFrom(Vid from, int timeoutMilliseconds = 0)
		{
			return new ReceiveOperation(this, timeoutMilliseconds, vm => vm.From == from);
		}

		protected ReceiveOperation ReceiveFrom<TMessage>(Vid from, int timeoutMilliseconds = 0)
			where TMessage : Message
		{
			return new ReceiveOperation(this, timeoutMilliseconds, vm => vm.From == from && vm.Message is TMessage);
		}

		protected SleepOperation Sleep(int timeoutMilliseconds)
		{
			return new SleepOperation(this, timeoutMilliseconds);
		}

		#endregion
	}

	public static class AsyncOperationAwaiters
	{
		public static AsyncVipo.ReceiveOperation GetAwaiter(this AsyncVipo.ReceiveOperation o) => o;
		public static AsyncVipo.SleepOperation GetAwaiter(this AsyncVipo.SleepOperation o) => o;
	}
}
