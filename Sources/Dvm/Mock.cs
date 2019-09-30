using System;

namespace Dvm
{
	public class Message
	{
		public static readonly Message VipoSchedule = new Message();

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
			return ReferenceEquals(this, VipoSchedule) ? "VipoSchedule" : "Other";
		}
	}

	public class KernelFault : Exception
	{
		public KernelFault()
		{
		}

		public KernelFault(string message)
			: base(message)
		{
		}
	}
}
