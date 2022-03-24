namespace Dvm
{
	abstract class ScheduleRequest
	{
	}

	class VipoProcess : ScheduleRequest
	{
		public Vipo Vipo { get; private set; }
		public int MessageIndex { get; private set; }

		public VipoProcess(Vipo vipo, int messageIndex)
		{
			Vipo = vipo;
			MessageIndex = messageIndex;
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
