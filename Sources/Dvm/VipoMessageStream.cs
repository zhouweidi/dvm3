using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Dvm
{
	public sealed class VipoMessageStream
	{
		readonly ConcurrentQueue<VipoMessage> m_inMessages;
		int m_remainingCount;
		bool m_stopReturning;
		VipoMessage m_last;
		bool m_lastIsDisposeMessage;

		internal bool LastIsDisposeMessage => m_lastIsDisposeMessage;

		internal VipoMessageStream(ConcurrentQueue<VipoMessage> inMessages, int count)
		{
			if (count <= 0)
				throw new KernelFaultException("The count of in messages to process is invalid");

			m_inMessages = inMessages;
			m_remainingCount = count;

			m_last = LoadNext(out m_lastIsDisposeMessage);
		}

		public bool GetNext(out VipoMessage vipoMessage)
		{
			if (!m_stopReturning)
			{
				// Stop on Dispose message
				if (!m_lastIsDisposeMessage)
				{
					vipoMessage = m_last;

					if (m_remainingCount == 0)
						m_stopReturning = true;
					else
						m_last = LoadNext(out m_lastIsDisposeMessage);

					return true;
				}

				m_stopReturning = true;
			}

			vipoMessage = new VipoMessage();
			return false;
		}

		// Forward-iteration sharing the progress with GetNext()
		public IEnumerable<VipoMessage> AsEnumerable()
		{
			if (!m_stopReturning)
			{
				// Stop on Dispose message
				while (!m_lastIsDisposeMessage)
				{
					yield return m_last;

					if (m_remainingCount == 0)
						break;

					m_last = LoadNext(out m_lastIsDisposeMessage);
				}

				m_stopReturning = true;
			}
		}

		VipoMessage LoadNext(out bool isDisposeMessage)
		{
			if (m_remainingCount <= 0)
				throw new KernelFaultException("No more in message to load");

			if (!m_inMessages.TryDequeue(out VipoMessage vipoMessage))
				throw new KernelFaultException("Not enough in messages to dequeue");

			--m_remainingCount;

			isDisposeMessage = ReferenceEquals(vipoMessage.Message, SystemScheduleMessage.Dispose);

			return vipoMessage;
		}

		internal bool EndsWithDisposeMessage()
		{
			// Need to dequeue all remaining
			while (m_remainingCount > 0)
				LoadNext(out m_lastIsDisposeMessage);

			return m_lastIsDisposeMessage;
		}
	}
}
