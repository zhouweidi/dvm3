using Dvm;
using System;
using System.Diagnostics;
using System.Linq;

namespace PerformanceTests.PoliteAnts
{
	struct TestCondition
	{
		public int VmProcessorsCount;
		public int MaxSchedulerCircleMilliseconds;

		public int AntsCount;
		public int GreetingSeedsCount;
		public int TestDurationSeconds;

		public float FamousPercent;
		public float GreetingFamousPossibilty;

		public readonly static TestCondition Default = new TestCondition()
		{
			VmProcessorsCount = 4,
			MaxSchedulerCircleMilliseconds = 10,

			AntsCount = 10 * 1000,
			GreetingSeedsCount = 10 * 1000,
			TestDurationSeconds = 5,

			FamousPercent = 0.1f,
			GreetingFamousPossibilty = 0.8f,
		};
	}

	class PoliteAnts : Test
	{
		readonly TestCondition m_condition;
		readonly int m_famousAntsCount;
		readonly Ant[] m_ants;

		public PoliteAnts(TestCondition condition, string testName)
			: base(condition.VmProcessorsCount, condition.MaxSchedulerCircleMilliseconds)
		{
			m_condition = condition;
			m_famousAntsCount = (int)(m_condition.AntsCount * m_condition.FamousPercent);
			m_ants = new Ant[m_condition.AntsCount];

			Assert(m_condition.GreetingSeedsCount / m_famousAntsCount >= 1);
			Assert(m_condition.GreetingSeedsCount % m_famousAntsCount == 0);

			Print($"{testName}");
			Print();
			Print($"Ants: {m_condition.AntsCount:N0}");
			Print($"Greeting seeds: {m_condition.GreetingSeedsCount:N0}");
			Print($"Test seconds: {m_condition.TestDurationSeconds:N0}");
			Print($"VmProcessors: {VM.ProcessorsCount}");
			Print($"SchedulerCircle: {m_condition.MaxSchedulerCircleMilliseconds} ms");
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
			Profile("Create ants", () =>
			{
				for (int i = 0; i < m_ants.Length; i++)
				{
					bool famous = i < m_famousAntsCount;

					m_ants[i] = new Ant(this, $"Ant {i + 1}", famous);
				}
			});

			Profile("Start ants", () =>
			{
				int initialGreetingCount = m_condition.GreetingSeedsCount / m_famousAntsCount;

				for (int i = 0; i < m_ants.Length; i++)
				{
					var ant = m_ants[i];

					ant.Start(ant.IsFamous ? initialGreetingCount : 0);
				}
			});
		}

		void FinalReport()
		{
			var startScheduleDuration = (from ant in m_ants
										 select ant.StartScheduleDuration.TotalMilliseconds).Average();
			Print($"Start schedule (avg): {startScheduleDuration:N0} ms");
			Print();

			var greetingSent = (from ant in m_ants
								select ant.GreetingSent).Average();
			var greetingSentSum = (from ant in m_ants
								   select ant.GreetingSent).Sum();
			Print($"Greeting sent (avg): {greetingSent:N1}");
			Print($"Greeting sent (sum): {greetingSentSum:N0}");

			var greetingReceived = (from ant in m_ants
									select ant.GreetingReceived).Average();
			var greetingReceivedSum = (from ant in m_ants
									   select ant.GreetingReceived).Sum();
			Print($"Greeting received (avg): {greetingReceived:N1}");
			Print($"Greeting received (sum): {greetingReceivedSum:N0}");

			var totalMessages = greetingSentSum + greetingReceivedSum;
			Print($"Messages: {totalMessages:N0}");
			Print($"Messages rate (m/s): {(float)totalMessages / m_condition.TestDurationSeconds:N0}");
			Print();

			var greetingRTTs = (from ant in m_ants
								from rtt in ant.GetGreetingRTTs()
								select rtt).ToArray();
			var avgRTT = (from rtt in greetingRTTs
						  select rtt.TotalMilliseconds).Average();
			var maxRTT = (from rtt in greetingRTTs
						  select rtt.TotalMilliseconds).Max();
			var minRTT = (from rtt in greetingRTTs
						  select rtt.TotalMilliseconds).Min();
			Print($"Greeting RTT (avg): {avgRTT:N0} ms");
			Print($"Greeting RTT (max): {maxRTT:N0} ms");
			Print($"Greeting RTT (min): {minRTT:N0} ms");
		}

		void Profile(string name, Action action)
		{
			var sw = new Stopwatch();

			sw.Start();
			action();
			sw.Stop();

			var elapsed = sw.Elapsed;
			Print($"{name}: {elapsed.TotalMilliseconds:N0} ms");
		}

		public Vid GetGreetingTarget(Random random)
		{
			var greetingFamous = random.NextDouble() < m_condition.GreetingFamousPossibilty;

			var i = greetingFamous ?
				random.Next(m_famousAntsCount) :
				m_famousAntsCount + random.Next(m_ants.Length - m_famousAntsCount);

			return m_ants[i].Vid;
		}
	}
}
