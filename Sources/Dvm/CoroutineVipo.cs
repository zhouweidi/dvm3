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
		public Func<VipoMessage, bool> Filter { get; private set; }
		public Action<VipoMessage> Handler { get; private set; }

		public ReceiveYield(Func<VipoMessage, bool> filter, Action<VipoMessage> handler)
		{
			Filter = filter;
			Handler = handler;
		}
	}

	class ReactYield : MessageYield
	{
		public Func<VipoMessage, bool> React { get; private set; }

		public ReactYield(Func<VipoMessage, bool> react)
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
		public VipoJob Job { get; private set; }

		IEnumerator m_enumerator;
		bool m_finished;
		MessageYield m_messageYield;

		protected CoroutineVipo(VirtualMachine vm, string name, CallbackOptions callbackOptions)
			: base(vm, name, callbackOptions)
		{
		}

		protected override void OnTick(VipoJob job)
		{
			if (m_finished)
				return;

			Job = job;

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
				Job = null;
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

		public YieldInstruction Receive(Action<VipoMessage> handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(null, handler);
		}

		public YieldInstruction Receive(Func<VipoMessage, bool> filter, Action<VipoMessage> handler)
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(filter, handler);
		}

		public YieldInstruction Receive<TMessage>(Action<VipoMessage, TMessage> handler)
			where TMessage : Message
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(
				message => message.Body is TMessage,
				message => handler(message, (TMessage)message.Body));
		}

		public YieldInstruction Receive<TMessage>(Func<VipoMessage, TMessage, bool> filter, Action<VipoMessage, TMessage> handler)
			where TMessage : Message
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(
				message => message.Body is TMessage && filter(message, (TMessage)message.Body),
				message => handler(message, (TMessage)message.Body));
		}

		public YieldInstruction ReceiveFrom(Vid from, Action<VipoMessage> handler)
		{
			if (from.IsEmpty)
				throw new ArgumentException("from is empty", nameof(from));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(
				message => message.From == from,
				handler);
		}

		public YieldInstruction ReceiveFrom(Vid from, Func<VipoMessage, bool> filter, Action<VipoMessage> handler)
		{
			if (from.IsEmpty)
				throw new ArgumentException("from is empty", nameof(from));
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(
				message => message.From == from && filter(message),
				handler);
		}

		public YieldInstruction ReceiveFrom<TMessage>(Vid from, Action<VipoMessage, TMessage> handler)
			where TMessage : Message
		{
			if (from.IsEmpty)
				throw new ArgumentException("from is empty", nameof(from));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(
				message => message.Body is TMessage && message.From == from,
				message => handler(message, (TMessage)message.Body));
		}

		public YieldInstruction ReceiveFrom<TMessage>(Vid from, Func<VipoMessage, TMessage, bool> filter, Action<VipoMessage, TMessage> handler)
			where TMessage : Message
		{
			if (from.IsEmpty)
				throw new ArgumentException("from is empty", nameof(from));
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			return new ReceiveYield(
				message => message.Body is TMessage && message.From == from && filter(message, (TMessage)message.Body),
				message => handler(message, (TMessage)message.Body));
		}

		public YieldInstruction Call(Vid to, Message body, Action<VipoMessage> handler)
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			Send(to, body);

			return ReceiveFrom(to, handler);
		}

		public YieldInstruction Call(Vid to, Message body, Func<VipoMessage, bool> filter, Action<VipoMessage> handler)
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			Send(to, body);

			return ReceiveFrom(to, filter, handler);
		}

		public YieldInstruction Call<TMessage>(Vid to, Message body, Action<VipoMessage, TMessage> handler)
			where TMessage : Message
		{
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			Send(to, body);

			return ReceiveFrom(to, handler);
		}

		public YieldInstruction Call<TMessage>(Vid to, Message body, Func<VipoMessage, TMessage, bool> filter, Action<VipoMessage, TMessage> handler)
			where TMessage : Message
		{
			if (filter == null)
				throw new ArgumentNullException(nameof(filter));
			if (handler == null)
				throw new ArgumentNullException(nameof(handler));

			Send(to, body);

			return ReceiveFrom(to, filter, handler);
		}

		public YieldInstruction React(Func<VipoMessage, bool> react)
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

		public IReadOnlyList<VipoMessage> InMessages
		{
			get { return Job.Messages; }
		}
	}

	#region Voroutine

	public delegate IEnumerator Voroutine(CoroutineVipo v);

	sealed class VoroutineObject : CoroutineVipo
	{
		Voroutine m_voroutine;
		Action<Exception> m_handleError;

		public VoroutineObject(Voroutine voroutine, Action<Exception> handleError, VirtualMachine vm, string name)
			: base(vm, name, CallbackOptions.None)
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

		public MinorVoroutineObject(MinorVoroutine voroutine, Action<Exception> handleError, VirtualMachine vm, string name)
			: base(vm, name, CallbackOptions.None)
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
		public static CoroutineVipo CreateVoroutine(this VirtualMachine vm, Voroutine procedure, string name = null)
		{
			return new VoroutineObject(procedure, null, vm, name);
		}

		public static CoroutineVipo CreateVoroutine(this VirtualMachine vm, Voroutine procedure, Action<Exception> handleError, string name = null)
		{
			return new VoroutineObject(procedure, handleError, vm, name);
		}

		public static CoroutineVipo CreateVoroutineMinor(this VirtualMachine vm, MinorVoroutine procedure, string name = null)
		{
			return new MinorVoroutineObject(procedure, null, vm, name);
		}

		public static CoroutineVipo CreateVoroutineMinor(this VirtualMachine vm, MinorVoroutine procedure, Action<Exception> handleError, string name = null)
		{
			return new MinorVoroutineObject(procedure, handleError, vm, name);
		}
	}

	#endregion
}
