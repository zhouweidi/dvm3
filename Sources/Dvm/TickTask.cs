using System.Collections.Generic;

namespace Dvm
{
	public struct TickTask
	{
		internal static readonly TickTask Empty = new TickTask();

		readonly Vipo m_vipo;
		readonly List<Message> m_messages;

		internal TickTask(Vipo vipo)
		{
			m_vipo = vipo;
			m_messages = new List<Message>();
		}

		internal void AddMessage(Message message)
		{
			m_messages.Add(message);
		}

		public Vipo Vipo
		{
			get { return m_vipo; }
		}

		public IReadOnlyList<Message> Messages
		{
			get { return m_messages; }
		}

		internal bool IsEmpty
		{
			get { return m_vipo == null; }
		}
	}
}
