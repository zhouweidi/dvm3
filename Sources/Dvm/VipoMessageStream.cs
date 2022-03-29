using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Dvm
{
	public sealed class VipoMessageStream
	{
		readonly ConcurrentQueue<VipoMessage> m_inMessages;
		int m_remainingCount;
		bool m_disposeMessageEncountered;
		VipoMessage? m_lastMessage;

		internal bool DisposeMessageEncountered => m_disposeMessageEncountered;

		internal VipoMessageStream(ConcurrentQueue<VipoMessage> inMessages, int count)
		{
			if (count <= 0)
				throw new KernelFaultException("The count of in messages to process is invalid");

			m_inMessages = inMessages;
			m_remainingCount = count;

			m_lastMessage = LoadNext();
		}

		public bool GetNext(out VipoMessage vipoMessage)
		{
			// Stop on Dispose message
			if (m_lastMessage != null && !m_disposeMessageEncountered)
			{
				vipoMessage = m_lastMessage.Value;

				m_lastMessage = LoadNext();

				return true;
			}
			else
			{
				vipoMessage = VipoMessage.Empty;
				return false;
			}
		}

		// Forward-iteration sharing the progress with GetNext()
		public IEnumerable<VipoMessage> GetConsumingEnumerable()
		{
			// Stop on Dispose message
			while (m_lastMessage != null && !m_disposeMessageEncountered)
			{
				yield return m_lastMessage.Value;

				m_lastMessage = LoadNext();
			}
		}

		VipoMessage? LoadNext()
		{
			if (m_remainingCount <= 0)
				return null;

			if (!m_inMessages.TryDequeue(out VipoMessage vipoMessage))
				throw new KernelFaultException("Not enough in messages to dequeue");

			--m_remainingCount;

			if (!m_disposeMessageEncountered)
				m_disposeMessageEncountered = ReferenceEquals(vipoMessage.Message, SystemScheduleMessage.Dispose);

			return vipoMessage;
		}

		internal void ConsumeRemaining()
		{
			// Need to dequeue all remaining
			while (m_remainingCount > 0)
				LoadNext();
		}
	}
}
