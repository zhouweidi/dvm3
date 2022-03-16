using System;
using System.Collections.Generic;

namespace Dvm
{
	public sealed class VipoJob
	{
		static readonly VipoMessage[] NoMessages = new VipoMessage[0];

		readonly Vipo m_vipo;
		List<VipoMessage> m_messages;

		internal Vipo Vipo => m_vipo;
		internal bool IsEmpty => m_messages == null;
		public IReadOnlyList<VipoMessage> Messages => m_messages ?? (IReadOnlyList<VipoMessage>)NoMessages;

		internal VipoJob(Vipo vipo)
		{
			m_vipo = vipo ?? throw new ArgumentNullException(nameof(vipo));
		}

		internal void AddMessage(VipoMessage message)
		{
			if (m_messages == null)
				m_messages = new List<VipoMessage>();

			m_messages.Add(message);
		}
	}
}
