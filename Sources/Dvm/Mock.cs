using System;

namespace Dvm
{
	class TickTask
	{
		public TickTask(Vipo vipo)
		{
			Vipo = vipo;
		}

		public Vipo Vipo { get; private set; }
	}

	public class Vipo
	{
		int m_tickedCount;
		string m_name;

		public Vipo(string name)
		{
			m_name = name;
		}

		internal void Tick(TickTask tickTask)
		{
			++m_tickedCount;
			Console.WriteLine($"Vipo '{m_name}' ticks #{m_tickedCount}");
		}
	}
}
