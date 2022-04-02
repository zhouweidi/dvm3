using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DvmTests.VipoTests
{
	[TestClass]
	public class VipoTimer : VmTestBase
	{
		[TestMethod]
		public void Basic()
		{
			var a = new TimedVipo(this, "a");
			Assert.AreEqual(VM.ViposCount, 1);

			a.Schedule();

			var startTime = DateTime.Now;
			var testDuration = TimeSpan.FromSeconds(2);

			do
			{
				Sleep(0.05);
			}
			while (!a.IsFinished && DateTime.Now - startTime < testDuration);

			Print($"Actual used time: {DateTime.Now - startTime}");

			var output = GetCustomOutput();
			Assert.IsTrue(output.Length == 7);

			// TODO Test 0ms interval
		}
	}

	class TimedVipo : TestVipo
	{
		int m_oneOffTimerId;
		int m_repeatedTimerId;
		DateTime m_startTime;

		DateTime m_repeatedTimerLastTriggeredTime;
		int m_repeatedTimerTriggeredCount;

		bool m_oneOffTimerMessageReceived;
		bool m_repeatedTimerMessagesReceived;

		public bool IsFinished => m_oneOffTimerMessageReceived && m_repeatedTimerMessagesReceived;

		public TimedVipo(VmTestBase test, string symbol)
			: base(test, symbol)
		{
		}

		protected override void Run(IVipoMessageStream messageStream)
		{
			while (messageStream.GetNext(out VipoMessage m))
			{
				switch (m.Message)
				{
					case UserScheduleMessage _:
						m_oneOffTimerId = SetTimer(500);
						m_repeatedTimerId = SetRepeatedTimer(100, 100);

						m_startTime = DateTime.Now;
						m_repeatedTimerLastTriggeredTime = m_startTime;
						break;

					case UserTimerMessage timer:
						var now = DateTime.Now;
						var msSinceStart = (int)(now - m_startTime).TotalMilliseconds;

						if (timer.TimerId == m_oneOffTimerId)
						{
							Print($"One-off timer: {msSinceStart}ms");
							m_oneOffTimerMessageReceived = true;
						}
						else if (timer.TimerId == m_repeatedTimerId)
						{
							++m_repeatedTimerTriggeredCount;

							var repeatedInterval = now - m_repeatedTimerLastTriggeredTime;
							m_repeatedTimerLastTriggeredTime = now;

							Print($"Repeated timer: {msSinceStart}ms, {m_repeatedTimerTriggeredCount}, {(int)repeatedInterval.TotalMilliseconds}ms");

							if (m_repeatedTimerTriggeredCount == 5)
							{
								ResetTimer(m_repeatedTimerId);

								m_repeatedTimerMessagesReceived = true;
							}
						}
						else
							Assert.Fail($"Unknown timer ID {timer.TimerId}");
						break;

					default:
						Assert.Fail("Unknown message");
						break;
				}
			}
		}
	}
}
