using System;

namespace Dvm
{
	class TickTask
	{
		public Vipo Vipo { get; }
	}

	public class Vipo
	{
		internal void Tick(TickTask tickTask)
		{
			Console.WriteLine("Vipo ticks");
		}
	}
}
