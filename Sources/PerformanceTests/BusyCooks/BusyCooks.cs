using System;
using System.Linq;

namespace PerformanceTests.BusyCooks
{
	class TestCondition : TestConditionBase
	{
		public int CooksCount;
		public int RepeatedInterval;
		public int OneOffDueTime;
		public int TestDurationSeconds;

		public static TestCondition CreateDefault() => new TestCondition()
		{
			VmProcessorsCount = 4,
			WithInspector = true,

			CooksCount = 10 * 1000,
			RepeatedInterval = 500,
			OneOffDueTime = 250,
			TestDurationSeconds = 10,
		};
	}

	partial class BusyCooks : Test
	{
		readonly TestCondition m_condition;
		readonly Cook[] m_cooks;

		public BusyCooks(TestCondition condition, string testName)
			: base(condition)
		{
			m_condition = condition;
			m_cooks = new Cook[m_condition.CooksCount];

			Print($"{testName}");
			Print();
			Print($"Cooks: {m_condition.CooksCount:N0}");
			Print($"Repeated interval: {m_condition.RepeatedInterval:N0} ms");
			Print($"One-off due time: {m_condition.OneOffDueTime:N0} ms");
			Print($"Test duration: {m_condition.TestDurationSeconds:N0} s");
			Print($"VmProcessors: {VM.ProcessorsCount}");
			Print();
		}

		public override void Run()
		{
			Start();

			Sleep(m_condition.TestDurationSeconds);

			Dispose();

			FinalReport();
		}

		void Start()
		{
			for (int i = 0; i < m_cooks.Length; i++)
				m_cooks[i] = new Cook(this, $"Cook {i + 1}");

			var random = new Random(123);

			for (int i = 0; i < m_cooks.Length; i++)
			{
				m_cooks[i].Start(
					random.Next() % m_condition.RepeatedInterval + 1,
					m_condition.RepeatedInterval,
					m_condition.OneOffDueTime,
					m_condition.TestDurationSeconds);
			}
		}

		void FinalReport()
		{
			// Repeated received
			int repeatedReceived;
			{
				var seq = from cook in m_cooks
						  select cook.RepeatedReceived;

				repeatedReceived = seq.Sum();

				Print($"Repeated received (avg): {seq.Average():N0}");
				Print($"Repeated received (sum): {repeatedReceived:N0}");
			}

			// One-off received
			int oneOffReceived;
			{
				var seq = from cook in m_cooks
						  select cook.OneOffReceived;

				oneOffReceived = seq.Sum();

				Print($"One-off received (avg): {seq.Average():N0}");
				Print($"One-off received (sum): {oneOffReceived:N0}");
			}

			// Messages
			{
				var totalMessages = repeatedReceived + oneOffReceived;

				Print($"Messages: {totalMessages:N0}");
				Print($"Message rate (m/s): {(float)totalMessages / m_condition.TestDurationSeconds:N0}");
				Print();
			}

			// Repeated trigger time
			{
				var times = from cook in m_cooks
							from tt in cook.RepeatedTriggerTimes
							select tt;

				var avg = times.Average();
				var max = times.Max();
				var min = times.Min();

				Print($"Repeated trigger time (avg): {avg:N0} ms / {avg - m_condition.RepeatedInterval:N0} ms");
				Print($"Repeated trigger time (max): {max:N0} ms / {max - m_condition.RepeatedInterval:N0} ms");
				Print($"Repeated trigger time (min): {min:N0} ms / {min - m_condition.RepeatedInterval:N0} ms");
			}

			// One-off trigger time
			{
				var times = from cook in m_cooks
							from tt in cook.OneOffTriggerTimes
							select tt;

				var avg = times.Average();
				var max = times.Max();
				var min = times.Min();

				Print($"One-off trigger time (avg): {avg:N0} ms / {avg - m_condition.OneOffDueTime:N0} ms");
				Print($"One-off trigger time (max): {max:N0} ms / {max - m_condition.OneOffDueTime:N0} ms");
				Print($"One-off trigger time (min): {min:N0} ms / {min - m_condition.OneOffDueTime:N0} ms");
			}
		}
	}
}
