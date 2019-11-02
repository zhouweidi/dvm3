using System;
using System.Collections.Generic;

namespace Dvm
{
	public sealed class TickTask
	{
		static readonly VipoMessage[] NoMessages = new VipoMessage[0];

		readonly Vipo m_vipo;
		List<VipoMessage> m_messages;
		Requests m_requests;

		[Flags]
		enum Requests : byte
		{
			None = 0,
			Start = 1,
			StartWithoutCallback = 2,
			Destroy = 4,
		}

		internal TickTask(Vipo vipo)
		{
			m_vipo = vipo;
		}

		internal void AddMessage(VipoMessage message)
		{
			if (m_messages == null)
				m_messages = new List<VipoMessage>();

			m_messages.Add(message);
		}

		internal void SetStartRequest(bool withCallback)
		{
			m_requests |= (withCallback ? Requests.Start : Requests.StartWithoutCallback);
		}

		internal void SetDestroyRequest()
		{
			m_requests |= Requests.Destroy;
		}

		internal Vipo Vipo
		{
			get { return m_vipo; }
		}

		public IReadOnlyList<VipoMessage> Messages
		{
			get { return m_messages == null ? (IReadOnlyList<VipoMessage>)NoMessages : m_messages; }
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
