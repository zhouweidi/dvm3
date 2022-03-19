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

	class VipoSchedule : ScheduleRequest
	{
		public Vipo Vipo { get; private set; }
		public object Context { get; private set; }

		public VipoSchedule(Vipo vipo, object context)
		{
			Vipo = vipo;
			Context = context;
		}
	}

	class VipoDetach : ScheduleRequest
	{
		public Vipo Vipo { get; private set; }

		public VipoDetach(Vipo vipo)
		{
			Vipo = vipo;
		}
	}

	class VipoDispose : ScheduleRequest
	{
		public Vipo Vipo { get; private set; }

		public VipoDispose(Vipo vipo)
		{
			Vipo = vipo;
		}
	}
}
