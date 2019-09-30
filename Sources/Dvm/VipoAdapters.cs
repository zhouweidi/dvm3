using System;
using System.Collections;
using System.Collections.Generic;

namespace Dvm
{
	public abstract class CoroutineVipo : Vipo
	{
		public TickTask TickTask { get; private set; }

		IEnumerator m_enumerator;

		protected CoroutineVipo(Scheduler scheduler, string name)
			: base(scheduler, name)
		{
		}

		protected override void OnTick(TickTask tickTask)
		{
			TickTask = tickTask;

			try
			{
				if (m_enumerator == null)
					m_enumerator = Coroutine();

				if (!m_enumerator.MoveNext())
				{
					Destroy();

					DisposableObject.TryDispose(ref m_enumerator);
				}
			}
			finally
			{
				TickTask = null;
			}
		}

		protected abstract IEnumerator Coroutine();

		public IReadOnlyList<Message> InMessages
		{
			get { return TickTask.Messages; }
		}
	}

	public class SimpleCoroutineVipo : CoroutineVipo
	{
		CoroutineFunction m_coroutine;
		Func<Exception, bool> m_onError;

		public delegate IEnumerator CoroutineFunction(SimpleCoroutineVipo vipo);

		public SimpleCoroutineVipo(CoroutineFunction coroutine, Func<Exception, bool> onError, Scheduler scheduler, string name)
			: base(scheduler, name)
		{
			if (coroutine == null)
				throw new ArgumentNullException(nameof(coroutine));

			m_coroutine = coroutine;
			m_onError = onError;
		}

		protected override bool OnError(Exception e)
		{
			if (m_onError != null)
				return m_onError(e);

			return base.OnError(e);
		}

		protected override IEnumerator Coroutine()
		{
			return m_coroutine(this);
		}
	}
}
