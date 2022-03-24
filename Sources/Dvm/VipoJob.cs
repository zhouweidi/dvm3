using System;

namespace Dvm
{
	class VipoJob
	{
		readonly Vipo m_vipo;
		int m_messageIndex = -1;
		bool m_disposeFlag;

		public Vipo Vipo => m_vipo;
		public int MessageIndex => m_messageIndex;
		public bool DisposeFlag => m_disposeFlag;
		public bool IsEmpty => m_messageIndex < 0 && !m_disposeFlag;

		public VipoJob(Vipo vipo)
		{
			m_vipo = vipo ?? throw new ArgumentNullException(nameof(vipo));
		}

		public void SetMessageIndex(int messageIndex)
		{
			if (messageIndex > m_messageIndex)
				m_messageIndex = messageIndex;
		}

		public void SetDisposeFlag() => m_disposeFlag = true;
	}
}
