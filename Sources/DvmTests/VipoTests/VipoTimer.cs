using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

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
			var testDuration = TimeSpan.FromSeconds(1);

			do
			{
				Sleep(0.05);
			}
			while (!a.IsFinished && DateTime.Now - startTime < testDuration);

			Print($"Actual used time: {DateTime.Now - startTime}");

			{
				var output = GetCustomOutput();
				Assert.IsTrue(output.Length == 7);

				Assert.IsTrue(output.Count(s => s.StartsWith("One-off timer: ")) == 1);
				Assert.IsTrue(output.Count(s => s.StartsWith("Repeated timer: #")) == 5);
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

			protected override void OnRun(IVipoMessageStream messageStream)
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

								Print($"Repeated timer: #{m_repeatedTimerTriggeredCount}, {msSinceStart}ms, {(int)repeatedInterval.TotalMilliseconds}ms");

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

		[TestMethod]
		public void DisposeBeforeTrigger()
		{
			{
				using var a = new DisposingTimedVipo(this, "a");

				a.Schedule();

				Sleep(0.1);
			}

			GC.Collect();
			Sleep(0.5);

			Assert.AreEqual(VM.ViposCount, 0);
			Assert.IsTrue(GetCustomOutput().Length == 0);
		}

		class DisposingTimedVipo : TestVipo
		{
			public DisposingTimedVipo(VmTestBase test, string symbol)
				: base(test, symbol)
			{
			}

			protected override void OnRun(IVipoMessageStream messageStream)
			{
				while (messageStream.GetNext(out VipoMessage m))
				{
					switch (m.Message)
					{
						case UserScheduleMessage _:
							SetTimer(500);
							break;

						default:
							Assert.Fail("Unknown message");
							break;
					}
				}
			}

			protected override void OnDispose()
			{
				base.OnDispose();
			}
		}
	}
}
