using Dvm;
using System;
using System.Collections.Generic;

namespace PerformanceTests.BusyCooks
{
	class Cook : TestVipo
	{
		readonly BusyCooks m_test;

		int m_repeatedId;
		int m_oneOffDueTime;
		int m_oneOffId;

		int m_repeatedReceived;
		List<int> m_repeatedTriggerTimes;
		long m_repeatedLastReceiveTime;

		int m_oneOffReceived;
		List<int> m_oneOffTriggerTimes;

		#region Properties

		public int RepeatedReceived => m_repeatedReceived;
		public IReadOnlyList<int> RepeatedTriggerTimes => m_repeatedTriggerTimes;
		public int OneOffReceived => m_oneOffReceived;
		public IReadOnlyList<int> OneOffTriggerTimes => m_oneOffTriggerTimes;

		#endregion

		public Cook(BusyCooks test, string symbol)
			: base(test, symbol)
		{
			m_test = test;
		}

		public void Start(int firstDueMilliseconds, int repeatedMilliseconds, int oneOffMilliseconds, int testDurationSeconds)
		{
			Schedule(new StartSignal(firstDueMilliseconds, repeatedMilliseconds, oneOffMilliseconds, testDurationSeconds));
		}

		class StartSignal
		{
			public int FirstDueMilliseconds { get; private set; }
			public int RepeatedMilliseconds { get; private set; }
			public int OneOffMilliseconds { get; private set; }
			public int TestDurationSeconds { get; private set; }

			public StartSignal(int firstDueMilliseconds, int repeatedMilliseconds, int oneOffMilliseconds, int testDurationSeconds)
			{
				FirstDueMilliseconds = firstDueMilliseconds;
				RepeatedMilliseconds = repeatedMilliseconds;
				OneOffMilliseconds = oneOffMilliseconds;
				TestDurationSeconds = testDurationSeconds;
			}
		}

		protected override void Run(IVipoMessageStream messageStream)
		{
			while (messageStream.GetNext(out VipoMessage m))
			{
				switch (m.Message)
				{
					case UserScheduleMessage schedule:
						if (schedule.Context is StartSignal s)
						{
							Assert(m_repeatedId == 0);

							m_repeatedId = SetRepeatedTimer(s.FirstDueMilliseconds, s.RepeatedMilliseconds);
							m_oneOffDueTime = s.OneOffMilliseconds;

							var repeatedCount = s.TestDurationSeconds * 1000 / s.RepeatedMilliseconds + 1;

							m_repeatedTriggerTimes = new List<int>(repeatedCount);
							m_oneOffTriggerTimes = new List<int>(repeatedCount);
						}
						break;

					case UserTimerMessage timer:
						var now = Environment.TickCount64;

						if (timer.TimerId == m_repeatedId)
						{
							bool isFirstTrigger = m_repeatedLastReceiveTime == 0;

							// Set one-off timer
							if (!isFirstTrigger && m_oneOffId != 0)
								throw new Exception("One-off timer is not triggered before setting a new one");

							m_oneOffId = SetTimer(m_oneOffDueTime);

							// Timing
							if (!isFirstTrigger)
							{
								++m_repeatedReceived;
								m_repeatedTriggerTimes.Add((int)(now - m_repeatedLastReceiveTime));
							}

							m_repeatedLastReceiveTime = now;

						}
						else if (timer.TimerId == m_oneOffId)
						{
							m_oneOffId = 0;

							// Timing
							++m_oneOffReceived;
							m_oneOffTriggerTimes.Add((int)(now - m_repeatedLastReceiveTime));
						}
						else
							Assert($"Unknown timer ID {timer.TimerId}");

						break;

					default:
						Assert("Unknown message");
						break;
				}
			}
		}
	}
}
