using System;
using System.Collections.Generic;

namespace Dvm
{
	sealed class TickTask
	{
		readonly Vipo m_vipo;
		List<Message> m_messages;
		Requests m_requests;

		[Flags]
		enum Requests : byte
		{
			None = 0,
			Start = 1,
			Destroy = 2,
		}

		internal TickTask(Vipo vipo)
		{
			m_vipo = vipo;
		}

		internal void AddMessage(Message message)
		{
			if (m_messages == null)
				m_messages = new List<Message>();

			m_messages.Add(message);
		}

		internal void SetStartRequest()
		{
			m_requests |= Requests.Start;
		}

		internal void SetDestroyRequest()
		{
			m_requests |= Requests.Destroy;
		}

		public Vipo Vipo
		{
			get { return m_vipo; }
		}

		public IReadOnlyList<Message> Messages
		{
			get { return m_messages; }
		}

		public bool AnyRequest
		{
			get { return m_requests != Requests.None; }
		}

		public bool StartRequest
		{
			get { return (m_requests & Requests.Start) != 0; }
		}

		public bool DestroyRequest
		{
			get { return (m_requests & Requests.Destroy) != 0; }
		}
	}
}
