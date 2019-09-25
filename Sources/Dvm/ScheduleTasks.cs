using System.Collections.Generic;

namespace Dvm
{
	class ScheduleTask
	{
	}

	class DispatchVipoMessages : ScheduleTask
	{
		public IReadOnlyList<Message> Messages { get; private set; }

		public DispatchVipoMessages(IReadOnlyList<Message> messages)
		{
			Messages = messages;
		}
	}

	class VipoStartup : ScheduleTask
	{
		public Vipo Vipo { get; private set; }

		public VipoStartup(Vipo vipo)
		{
			Vipo = vipo;
		}
	}
}
