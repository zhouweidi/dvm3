using System.Collections.Generic;

namespace Dvm
{
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
			m_vid = scheduler.CreateVid(name);
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
