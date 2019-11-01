using System;
using System.Collections;
using System.Collections.Generic;

namespace Dvm
{
	public class YieldInstruction
	{
	}

	public class BadYieldInstruction : VipoFaultException
	{
		public BadYieldInstruction(Vid vid, string message)
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

		protected CoroutineVipo(Scheduler scheduler, string name, CallbackOptions callbackOptions)
			: base(scheduler, name, callbackOptions)
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
							throw new BadYieldInstruction(Vid, "Yield return a non-YieldInstruction");

						switch (m_enumerator.Current)
						{
							case ReceiveInstruction ri:
								m_receive = ri;
								break;

							case null:
								throw new BadYieldInstruction(Vid, "The yield instruction cannot be null");

							default:
								throw new BadYieldInstruction(Vid, "Unsupported yield instruction");
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

	#region Voroutine

	public delegate IEnumerator Voroutine(CoroutineVipo v);

	sealed class VoroutineObject : CoroutineVipo
	{
		Voroutine m_voroutine;
		Action<Exception> m_handleError;

		public VoroutineObject(Voroutine voroutine, Action<Exception> handleError, Scheduler scheduler, string name)
			: base(scheduler, name, CallbackOptions.None)
		{
			if (voroutine == null)
				throw new ArgumentNullException(nameof(voroutine));

			m_voroutine = voroutine;
			m_handleError = handleError;
		}

		protected override IEnumerator Coroutine()
		{
			return m_voroutine(this);
		}

		protected override void OnError(Exception e)
		{
			if (m_handleError != null)
				m_handleError(e);
			else
				base.OnError(e);
		}
	}

	#endregion

	#region MinorVoroutine

	public delegate void MinorVoroutine(CoroutineVipo v);

	sealed class MinorVoroutineObject : CoroutineVipo
	{
		MinorVoroutine m_voroutine;
		Action<Exception> m_handleError;

		public MinorVoroutineObject(MinorVoroutine voroutine, Action<Exception> handleError, Scheduler scheduler, string name)
			: base(scheduler, name, CallbackOptions.None)
		{
			if (voroutine == null)
				throw new ArgumentNullException(nameof(voroutine));

			m_voroutine = voroutine;
			m_handleError = handleError;
		}

		protected override IEnumerator Coroutine()
		{
			m_voroutine(this);

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

	#endregion

	public static class VoroutineExtension
	{
		public static CoroutineVipo CreateVoroutine(this Scheduler scheduler, Voroutine procedure, string name = null)
		{
			return new VoroutineObject(procedure, null, scheduler, name);
		}

		public static CoroutineVipo CreateVoroutine(this Scheduler scheduler, Voroutine procedure, Action<Exception> handleError, string name = null)
		{
			return new VoroutineObject(procedure, handleError, scheduler, name);
		}

		public static CoroutineVipo CreateVoroutineMinor(this Scheduler scheduler, MinorVoroutine procedure, string name = null)
		{
			return new MinorVoroutineObject(procedure, null, scheduler, name);
		}

		public static CoroutineVipo CreateVoroutineMinor(this Scheduler scheduler, MinorVoroutine procedure, Action<Exception> handleError, string name = null)
		{
			return new MinorVoroutineObject(procedure, handleError, scheduler, name);
		}
	}
}
