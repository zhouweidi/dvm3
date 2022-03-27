using Dvm;
using System;
using System.Diagnostics;
using System.Linq;

namespace PerformanceTests.PoliteAnts
{
	struct TestCondition
	{
		public int VmProcessorsCount;
		public bool WithInspector;

		public int AntsCount;
		public int GreetingSeedsCount;
		public float GreetingFakeProcessingSeconds;
		public int TestDurationSeconds;

		public readonly static TestCondition Default = new TestCondition()
		{
			VmProcessorsCount = 4,
			WithInspector = true,

			AntsCount = 10 * 1000,
			GreetingSeedsCount = 10 * 1000,
			GreetingFakeProcessingSeconds = 0, //0.0001f,
			TestDurationSeconds = 10,
		};
	}

	class PoliteAnts : Test
	{
		readonly TestCondition m_condition;
		readonly Ant[] m_ants;

		public PoliteAnts(TestCondition condition, string testName)
			: base(condition.VmProcessorsCount, condition.WithInspector)
		{
			m_condition = condition;
			m_ants = new Ant[m_condition.AntsCount];

			Assert(m_condition.GreetingSeedsCount >= m_condition.AntsCount);
			Assert(m_condition.GreetingSeedsCount % m_condition.AntsCount == 0);

			Print($"{testName}");
			Print();
			Print($"Ants: {m_condition.AntsCount:N0}");
			Print($"Greeting seeds: {m_condition.GreetingSeedsCount:N0}");
			Print($"Greeting fake processing: {m_condition.GreetingFakeProcessingSeconds:N4} s");
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
			// Create
			Profile("Create ants", () =>
			{
				for (int i = 0; i < m_ants.Length; i++)
					m_ants[i] = new Ant(this, $"Ant {i + 1}", m_condition.GreetingFakeProcessingSeconds);
			});

			// Register
			Profile("Register ants", () =>
			{
				for (int i = 0; i < m_ants.Length; i++)
				{
					var ant = m_ants[i];

					ant.Register();
				}
			});

			// Start greeting
			{
				int initialGreetingCount = m_condition.GreetingSeedsCount / m_condition.AntsCount;

				for (int i = 0; i < m_ants.Length; i++)
				{
					var ant = m_ants[i];

					ant.Start(initialGreetingCount);
				}
			}
		}

		void FinalReport()
		{
			var registerDuration = (from ant in m_ants
									select ant.RegisterDuration.TotalMilliseconds).Average();
			Print($"Register duration (avg): {registerDuration:N0} ms");
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
			Print($"Message rate (m/s): {(float)totalMessages / m_condition.TestDurationSeconds:N0}");
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
			var i = random.Next(m_ants.Length);

			return m_ants[i].Vid;
		}
	}
}
