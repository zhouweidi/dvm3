using System;
using System.Collections.Generic;

namespace Dvm
{
	public struct TickTask
	{
		internal static readonly TickTask Empty = new TickTask();

		readonly Vipo m_vipo;
		readonly List<Message> m_messages;
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
			m_messages = new List<Message>();
			m_requests = Requests.None;
		}

		internal void AddMessage(Message message)
		{
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

		internal bool IsEmpty
		{
			get { return m_vipo == null; }
		}

		public bool StartRequest
		{
			get { return (m_requests & Requests.Start) != 0; }
		}

		public bool DestroyRequest
		{
			get { return (m_requests & Requests.Destroy) != 0; }
		}

		public bool AnyRequest
		{
			get { return m_requests != Requests.None; }
		}
	}
}
