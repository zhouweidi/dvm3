using System;
using System.Collections.Generic;

namespace Dvm
{
	class VipoJob
	{
		readonly Vipo m_vipo;
		List<VipoMessage> m_messages;
		bool m_disposeFlag;

		public Vipo Vipo => m_vipo;
		public IReadOnlyList<VipoMessage> Messages => m_messages;
		public bool DisposeFlag => m_disposeFlag;
		public bool IsEmpty => m_messages == null && !m_disposeFlag;

		public VipoJob(Vipo vipo)
		{
			m_vipo = vipo ?? throw new ArgumentNullException(nameof(vipo));
		}

		public void AddMessage(VipoMessage message)
		{
			if (m_messages == null)
				m_messages = new List<VipoMessage>();

			m_messages.Add(message);
		}

		public void SetDisposeFlag() => m_disposeFlag = true;
	}
}
