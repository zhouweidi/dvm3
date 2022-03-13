using System.Collections.Generic;

namespace Dvm
{
	abstract class ScheduleRequest
	{
	}

	class DispatchVipoMessages : ScheduleRequest
	{
		public IReadOnlyList<VipoMessage> Messages { get; private set; }

		public DispatchVipoMessages(IReadOnlyList<VipoMessage> messages)
		{
			Messages = messages;
		}
	}

	class VipoStart : ScheduleRequest
	{
		public Vipo Vipo { get; private set; }

		public VipoStart(Vipo vipo)
		{
			Vipo = vipo;
		}
	}

	class VipoDestroy : ScheduleRequest
	{
		public Vipo Vipo { get; private set; }

		public VipoDestroy(Vipo vipo)
		{
			Vipo = vipo;
		}
	}

	class VipoSchedule : ScheduleRequest
	{
		public Vipo Vipo { get; private set; }

		public VipoSchedule(Vipo vipo)
		{
			Vipo = vipo;
		}
	}
}
