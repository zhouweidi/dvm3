using System;
using System.Collections;
using System.Collections.Generic;

namespace Dvm
{
	#region Yield instructions

	public class YieldInstruction
	{
	}

	public class MessageYield : YieldInstruction
	{
	}

	class ReceiveYield : MessageYield
	{
		public Func<Message, bool> Filter { get; private set; }
		public Action<Message> Handler { get; private set; }

		public ReceiveYield(Func<Message, bool> filter, Action<Message> handler)
		{
			Filter = filter;
			Handler = handler;
		}
	}

	class ReactYield : MessageYield
	{
		public Func<Message, bool> React { get; private set; }

		public ReactYield(Func<Message, bool> react)
		{
			React = react;
		}
	}

	class WaitForAnyMessageYield : MessageYield
	{
		public static readonly WaitForAnyMessageYield Instance = new WaitForAnyMessageYield();
	}

	public class BadYieldInstruction : VipoFaultException
	{
		public BadYieldInstruction(Vid vid, string message)
			: base(vid, message)
		{
		}
	}

	#endregion

	public abstract class CoroutineVipo : Vipo
	{
		public TickTask TickTask { get; private set; }

		IEnumerator m_enumerator;
		bool m_finished;
		MessageYield m_messageYield;

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
				for (int messageIndex = 0; ;)
				{
					if (m_messageYield != null)
					{
						if (messageIndex >= InMessages.Count)
							return;

						switch (m_messageYield)
						{
							case ReceiveYield receive:
								{
									if (receive.Filter != null)
									{
										while (!receive.Filter(InMessages[messageIndex]))
										{
											if (++messageIndex >= InMessages.Count)
												return;
										}
									}

									receive.Handler(InMessages[messageIndex]);

									++messageIndex;
									m_messageYield = null;
								}
								break;

							case ReactYield react:
								{
									while (react.React(InMessages[messageIndex]))
									{
										if (++messageIndex >= InMessages.Count)
											return;
									}

									++messageIndex;
									m_messageYield = null;
								}
								break;

							case WaitForAnyMessageYield waitForAnyMessage:
								m_messageYield = null;
								break;

							default:
								throw new BadYieldInstruction(Vid, "Unsupported message yield instruction");
						}
					}

					if (!TickCoroutine())
						return;
				}
			}
			finally
			{
				TickTask = null;
			}
		}

		bool TickCoroutine()
		{
			if (m_enumerator == null)
				m_enumerator = Coroutine();

			if (m_enumerator.MoveNext())
			{
				if (!(m_enumerator.Current is YieldInstruction))
					throw new BadYieldInstruction(Vid, "Yield return a non-YieldInstruction");

				switch (m_enumerator.Current)
				{
					case ReceiveYield receive:
						m_messageYield = receive;
						break;

					case ReactYield react:
						m_messageYield = react;
						break;

					case WaitForAnyMessageYield waitForAnyMessage:
						m_messageYield = waitForAnyMessage;
						break;

					case null:
						throw new BadYieldInstruction(Vid, "The yield instruction cannot be null");

					default:
						throw new BadYieldInstruction(Vid, "Unsupported yield instruction");
				}

				return true;
			}
			else
			{
				Destroy();

				DisposableObject.TryDispose(ref m_enumerator);

				m_finished = true;

				return false;
			}
		}

		protected abstract IEnumerator Coroutine();

		#region Primitives

		public YieldInstruction Receive(Action<Message> handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(null, handler);
		}

		public YieldInstruction Receive(Func<Message, bool> filter, Action<Message> handler)
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(filter, handler);
		}

		public YieldInstruction Receive<TMessage>(Action<TMessage> handler)
			where TMessage : Message
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(
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

			return new ReceiveYield(
				message => message is TMessage && filter((TMessage)message),
				message => handler((TMessage)message));
		}

		public YieldInstruction React(Func<Message, bool> react)
		{
			if (react == null)
				throw new ArgumentNullException(nameof(react));

			return new ReactYield(react);
		}

		public YieldInstruction WaitForAnyMessage()
		{
			return WaitForAnyMessageYield.Instance;
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

	#endregion
}
