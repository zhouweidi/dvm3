using System.Threading;

namespace Dvm
{
	public class Inspector
	{
		int m_discardedMessages;
		int m_jobQueueMaxSize;

		public int DiscardedMessages => m_discardedMessages;
		public int JobQueueMaxSize => m_jobQueueMaxSize;

		internal void IncreaseDiscardedMessage()
		{
			Interlocked.Increment(ref m_discardedMessages);
		}

		internal void UpdateJobQueueSize(int count)
		{
			if (count > m_jobQueueMaxSize)
				m_jobQueueMaxSize = count;
		}
	}
}
