using System;
using System.Collections.Generic;
using System.Text;

namespace Dvm
{
	class TickTask
	{
		public Vipo Vipo { get; }
	}

	public class Vipo
	{
		internal void Tick(TickTask tickTask)
		{ }
	}
}
