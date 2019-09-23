using System;
using System.Collections.Generic;
using System.Threading;

namespace Dvm
{
	class TickTask
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
		static volatile int s_allocator = 1;

		int m_value;

		public static Vid Create()
		{
			return new Vid()
			{
				m_value = Interlocked.Increment(ref s_allocator)
			};
		}

		public override int GetHashCode()
		{
			return m_value;
		}

		public override string ToString()
		{
			return m_value.ToString();
		}
	}

	public class Message
	{
		public static readonly Message VipoStartup = new Message();

		public Vid From { get; private set; }
		public Vid To { get; private set; }
	}

	public class Vipo
	{
		Vid m_vid;
		string m_name;
		int m_tickedCount;

		public Vipo(string name)
		{
			m_vid = Vid.Create();
			m_name = name;
		}

		internal void Tick(TickTask tickTask)
		{
			++m_tickedCount;
			Console.WriteLine($"Vipo '{m_name}' ticks #{m_tickedCount}");
		}

		public Vid Vid
		{
			get { return m_vid; }
		}
	}
}
