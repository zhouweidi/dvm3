using System.Collections.Generic;

namespace Dvm
{
	class ScheduleTask
	{
	}

	class DispatchVipoMessages : ScheduleTask
	{
		public IReadOnlyList<VipoMessage> Messages { get; private set; }

		public DispatchVipoMessages(IReadOnlyList<VipoMessage> messages)
		{
			Messages = messages;
		}
	}

	class VipoStart : ScheduleTask
	{
		public Vipo Vipo { get; private set; }

		public VipoStart(Vipo vipo)
		{
			Vipo = vipo;
		}
	}

	class VipoDestroy : ScheduleTask
	{
		public Vipo Vipo { get; private set; }

		public VipoDestroy(Vipo vipo)
		{
			Vipo = vipo;
		}
	}

	class VipoSchedule : ScheduleTask
	{
		public Vipo Vipo { get; private set; }

		public VipoSchedule(Vipo vipo)
		{
			Vipo = vipo;
		}
	}
}
