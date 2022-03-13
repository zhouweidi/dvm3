using System;
using System.Collections.Generic;

namespace Dvm
{
	public sealed class VipoJob
	{
		[Flags]
		enum Requests : byte
		{
			None = 0,
			Start = 1,
			StartWithoutCallback = 2,
			Destroy = 4,
		}

		static readonly VipoMessage[] NoMessages = new VipoMessage[0];

		readonly Vipo m_vipo;
		List<VipoMessage> m_messages;
		Requests m_requests;

		internal Vipo Vipo => m_vipo;
		public IReadOnlyList<VipoMessage> Messages => m_messages ?? (IReadOnlyList<VipoMessage>)NoMessages;
		public bool AnyRequest => m_requests != Requests.None;
		public bool StartRequest => (m_requests & Requests.Start) != 0;
		public bool DestroyRequest => (m_requests & Requests.Destroy) != 0;

		internal VipoJob(Vipo vipo)
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
	}
}
