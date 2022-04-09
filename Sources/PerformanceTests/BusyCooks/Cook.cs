using Dvm;
using System;
using System.Collections.Generic;

namespace PerformanceTests.BusyCooks
{
	class Cook : TestVipo
	{
		// Conditions
		readonly int m_repeatedFirstDue;
		readonly int m_repeatedInterval;
		readonly int m_oneOffDue;

		// Runtime
		int m_repeatedId;
		long m_repeatedLastTriggerTime;
		SortedList<int, long> m_oneOffTimers = new SortedList<int, long>(); // <timer id, set time>

		// Results
		readonly List<int> m_repeatedTriggerTimes;
		readonly List<int> m_oneOffTriggerTimes;

		#region Properties

		public IReadOnlyList<int> RepeatedTriggerTimes => m_repeatedTriggerTimes;
		public IReadOnlyList<int> OneOffTriggerTimes => m_oneOffTriggerTimes;

		#endregion

		public Cook(BusyCooks test, string symbol, int repeatedFirstDue, int repeatedInterval, int oneOffDue, int testDurationSeconds)
			: base(test, symbol)
		{
			m_repeatedFirstDue = repeatedFirstDue;
			m_repeatedInterval = repeatedInterval;
			m_oneOffDue = oneOffDue;

			var repeatedCount = testDurationSeconds * 1000 / m_repeatedInterval + 1;

			m_repeatedTriggerTimes = new List<int>(repeatedCount);
			m_oneOffTriggerTimes = new List<int>(repeatedCount);
		}

		static readonly object StartSignal = new object();

		public void Start()
		{
			Schedule(StartSignal);
		}

		protected override void OnRun(IVipoMessageStream messageStream)
		{
			while (messageStream.GetNext(out VipoMessage m))
			{
				switch (m.Message)
				{
					case UserScheduleMessage schedule:
						if (ReferenceEquals(schedule.Context, StartSignal))
						{
							Assert(m_repeatedId == 0);

							m_repeatedId = SetRepeatedTimer(m_repeatedFirstDue, m_repeatedInterval);
						}
						else
							goto default;
						break;

					case UserTimerMessage timer:
						var now = Environment.TickCount64;

						if (timer.TimerId == m_repeatedId)
						{
							// Set one-off timer
							var oneOffTimerId = SetTimer(m_oneOffDue);
							m_oneOffTimers.Add(oneOffTimerId, now);

							// Timing
							bool isFirstTrigger = m_repeatedLastTriggerTime == 0;
							if (!isFirstTrigger)
								m_repeatedTriggerTimes.Add((int)(now - m_repeatedLastTriggerTime));

							m_repeatedLastTriggerTime = now;
						}
						else
						{
							var got = m_oneOffTimers.TryGetValue(timer.TimerId, out long setTime);
							Assert(got, $"Unknown timer ID {timer.TimerId}");

							m_oneOffTimers.Remove(timer.TimerId);

							// Timing
							m_oneOffTriggerTimes.Add((int)(now - setTime));
						}
						break;

					default:
						Assert("Unknown message");
						break;
				}
			}
		}
	}
}
