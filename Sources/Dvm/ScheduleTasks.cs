using System.Collections.Generic;

namespace Dvm
{
	class ScheduleTask
	{
	}

	class DispatchMessages : ScheduleTask
	{
		public DispatchMessages(IReadOnlyList<Message> messages)
		{
			Messages = messages;
		}

		public IReadOnlyList<Message> Messages { get; private set; }
	}

	class VipoStartup : ScheduleTask
	{
		public VipoStartup(Vipo vipo)
		{
			Vipo = vipo;
		}

		public Vipo Vipo { get; private set; }
	}
}
