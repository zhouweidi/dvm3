using System;
using System.Collections.Generic;
using System.Threading;

namespace Dvm
{
	public class TickTask
	{
		List<Message> m_messages = new List<Message>();

		public TickTask(Vipo vipo)
		{
			Vipo = vipo;
		}

		internal void AddMessage(Message message)
		{
			m_messages.Add(message);
		}

		public Vipo Vipo { get; private set; }

		public IReadOnlyList<Message> Messages
		{
			get { return m_messages; }
		}
	}

	public struct Vid
	{
		int m_value;

		internal Vid(int value)
		{
			m_value = value;
		}

		public override int GetHashCode()
		{
			return m_value;
		}

		public override string ToString()
		{
			return m_value.ToString();
		}

		public override bool Equals(Object obj)
		{
			if (!(obj is Vid))
			{
				return false;
			}

			Vid vid = (Vid)obj;

			return vid.m_value == m_value;
		}

		public static bool operator ==(Vid x, Vid y)
		{
			return x.m_value == y.m_value;
		}

		public static bool operator !=(Vid x, Vid y)
		{
			return x.m_value != y.m_value;
		}
	}

	public class Message
	{
		public static readonly Message VipoStartup = new Message();

		public Vid From { get; private set; }
		public Vid To { get; private set; }

		Message()
		{
		}

		public Message(Vid from, Vid to)
		{
			From = from;
			To = to;
		}

		public override string ToString()
		{
			return ReferenceEquals(this, VipoStartup) ? "VipoStartup" : "Other";
		}
	}

	public abstract class Vipo
	{
		Scheduler m_scheduler;
		Vid m_vid;
		string m_name;
		List<Message> m_outMessages = new List<Message>();

		public Vipo(Scheduler scheduler, string name)
		{
			m_scheduler = scheduler;
			m_vid = scheduler.CreateVid();
			m_name = name;
		}

		public void Start()
		{
			m_scheduler.AddVipo(this);
		}

		public abstract void Tick(TickTask tickTask);

		protected void SendMessage(Message message)
		{
			m_outMessages.Add(message);
		}

		public IReadOnlyList<Message> TakeOutMessages()
		{
			if (m_outMessages.Count == 0)
				return null;

			var outMessages = m_outMessages;

			m_outMessages = new List<Message>();

			return outMessages;
		}

		public Vid Vid
		{
			get { return m_vid; }
		}

		public string Name
		{
			get { return m_name; }
		}
	}
}
