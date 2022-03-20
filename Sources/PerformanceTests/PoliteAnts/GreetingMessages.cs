using Dvm;
using System;

namespace PerformanceTests.PoliteAnts
{
	class GreetingMessage : Message
	{
		public DateTime Timestamp { get; private set; }

		public GreetingMessage()
		{
			Timestamp = DateTime.Now;
		}
	}

	class GreetingAckMessage : Message
	{
		public DateTime Timestamp { get; private set; }

		public GreetingAckMessage(DateTime timestamp)
		{
			Timestamp = timestamp;
		}
	}
}
