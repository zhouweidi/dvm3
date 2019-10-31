using System;
using System.Collections;
using System.Collections.Generic;

namespace Dvm
{
	public class YieldInstruction
	{
	}

	public class NullYieldInstruction : VipoFaultException
	{
		public NullYieldInstruction(Vid vid, string message)
			: base(vid, message)
		{
		}
	}

	class ReceiveInstruction : YieldInstruction
	{
		public Func<Message, bool> Filter { get; private set; }
		public Action<Message> Handler { get; private set; }

		public ReceiveInstruction(Func<Message, bool> filter, Action<Message> handler)
		{
			Filter = filter;
			Handler = handler;
		}
	}

	public abstract class CoroutineVipo : Vipo
	{
		public TickTask TickTask { get; private set; }

		IEnumerator m_enumerator;
		bool m_finished;
		ReceiveInstruction m_receive;

		protected CoroutineVipo(Scheduler scheduler, string name)
			: base(scheduler, name)
		{
		}

		protected override void OnTick(TickTask tickTask)
		{
			if (m_finished)
				return;

			TickTask = tickTask;

			try
			{
				if (m_enumerator == null)
					m_enumerator = Coroutine();

				for (int messageIndex = -1; ;)
				{
					if (m_receive != null)
					{
						if (tickTask.Messages.Count == 0)
							return;

						++messageIndex;

						if (m_receive.Filter != null)
						{
							while (!m_receive.Filter(InMessages[messageIndex]))
							{
								if (++messageIndex >= InMessages.Count)
									return;
							}
						}

						m_receive.Handler(InMessages[messageIndex]);
						m_receive = null;
					}

					if (m_enumerator.MoveNext())
					{
						if (!(m_enumerator.Current is YieldInstruction))
							throw new NullYieldInstruction(Vid, "Yield return a non-YieldInstruction");

						switch (m_enumerator.Current)
						{
							case ReceiveInstruction ri:
								m_receive = ri;
								break;

							case null:
								throw new VipoFaultException(Vid, "The yield instruction cannot be null");

							default:
								throw new VipoFaultException(Vid, "Unsupported yield instruction");
						}
					}
					else
					{
						Destroy();

						DisposableObject.TryDispose(ref m_enumerator);

						m_finished = true;

						return;
					}
				}
			}
			finally
			{
				TickTask = null;
			}
		}

		protected abstract IEnumerator Coroutine();

		#region Primitives

		public YieldInstruction Receive(Action<Message> handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveInstruction(null, handler);
		}

		public YieldInstruction Receive(Func<Message, bool> filter, Action<Message> handler)
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveInstruction(filter, handler);
		}

		public YieldInstruction Receive<TMessage>(Action<TMessage> handler)
			where TMessage : Message
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveInstruction(
				message => message is TMessage,
				message => handler((TMessage)message));
		}

		public YieldInstruction Receive<TMessage>(Func<TMessage, bool> filter, Action<TMessage> handler)
			where TMessage : Message
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveInstruction(
				message => message is TMessage && filter((TMessage)message),
				message => handler((TMessage)message));
		}

		#endregion

		public IReadOnlyList<Message> InMessages
		{
			get { return TickTask.Messages; }
		}
	}

	public delegate IEnumerator VoroutineProcedure(Voroutine v);

	public sealed class Voroutine : CoroutineVipo
	{
		VoroutineProcedure m_procedure;
		Action<Exception> m_handleError;

		public Voroutine(VoroutineProcedure procedure, Action<Exception> handleError, Scheduler scheduler, string name)
			: base(scheduler, name)
		{
			if (procedure == null)
				throw new ArgumentNullException(nameof(procedure));

			m_procedure = procedure;
			m_handleError = handleError;
		}

		protected override IEnumerator Coroutine()
		{
			return m_procedure(this);
		}

		protected override void OnError(Exception e)
		{
			if (m_handleError != null)
				m_handleError(e);
			else
				base.OnError(e);
		}
	}

	public delegate void VoroutineMinorProcedure(VoroutineMinor v);

	public sealed class VoroutineMinor : CoroutineVipo
	{
		VoroutineMinorProcedure m_procedure;
		Action<Exception> m_handleError;

		public VoroutineMinor(VoroutineMinorProcedure procedure, Action<Exception> handleError, Scheduler scheduler, string name)
			: base(scheduler, name)
		{
			if (procedure == null)
				throw new ArgumentNullException(nameof(procedure));

			m_procedure = procedure;
			m_handleError = handleError;
		}

		protected override IEnumerator Coroutine()
		{
			m_procedure(this);

			yield break;
		}

		protected override void OnError(Exception e)
		{
			if (m_handleError != null)
				m_handleError(e);
			else
				base.OnError(e);
		}
	}

	public static class VoroutineExtension
	{
		public static Voroutine CreateVoroutine(this Scheduler scheduler, VoroutineProcedure procedure, string name = null)
		{
			return new Voroutine(procedure, null, scheduler, name);
		}

		public static Voroutine CreateVoroutine(this Scheduler scheduler, VoroutineProcedure procedure, Action<Exception> handleError, string name = null)
		{
			return new Voroutine(procedure, handleError, scheduler, name);
		}

		public static VoroutineMinor CreateVoroutineMinor(this Scheduler scheduler, VoroutineMinorProcedure procedure, string name = null)
		{
			return new VoroutineMinor(procedure, null, scheduler, name);
		}

		public static VoroutineMinor CreateVoroutineMinor(this Scheduler scheduler, VoroutineMinorProcedure procedure, Action<Exception> handleError, string name = null)
		{
			return new VoroutineMinor(procedure, handleError, scheduler, name);
		}
	}
}
