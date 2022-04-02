using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;

namespace Dvm
{
	public interface IVipoMessageStream
	{
		bool GetNext(out VipoMessage vipoMessage);
		IEnumerable<VipoMessage> GetConsumingEnumerable();
	}

	class VipoMessageStream : IVipoMessageStream
	{
		readonly ConcurrentQueue<VipoMessage> m_inMessages;
		int m_remainingCount;
		bool m_disposeMessageEncountered;
		bool m_timerMessageEncountered;
		VipoMessage? m_lastMessage;

		internal bool DisposeMessageEncountered => m_disposeMessageEncountered;
		internal bool TimerMessageEncountered => m_timerMessageEncountered;

		internal VipoMessageStream(ConcurrentQueue<VipoMessage> inMessages, int count)
		{
			if (count <= 0)
				throw new KernelFaultException("The count of in messages to process is invalid");

			m_inMessages = inMessages;
			m_remainingCount = count;

			m_lastMessage = LoadNext();
		}

		bool IVipoMessageStream.GetNext(out VipoMessage vipoMessage)
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
		IEnumerable<VipoMessage> IVipoMessageStream.GetConsumingEnumerable()
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
		Reload:
			if (m_remainingCount <= 0)
				return null;

			// Always dequeue
			if (!m_inMessages.TryDequeue(out VipoMessage vipoMessage))
				throw new KernelFaultException("Not enough in messages to dequeue");

			--m_remainingCount;

			// System dispose message
			if (!m_disposeMessageEncountered)
				m_disposeMessageEncountered = ReferenceEquals(vipoMessage.Message, SystemScheduleMessage.Dispose);

			if (m_disposeMessageEncountered)
				return null;

			// System timer message
			if (ReferenceEquals(vipoMessage.Message, SystemScheduleMessage.Timer))
			{
				Debug.Assert(!m_timerMessageEncountered); // Only one timer message expected in a batch
				m_timerMessageEncountered = true;
				goto Reload;
			}

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
