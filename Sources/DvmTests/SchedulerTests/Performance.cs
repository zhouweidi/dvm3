using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class Performance : VmTestBase
	{
		const int AntsCount = 10 * 1000;
		const int GreetingSeedsCount = 10 * 1000;
		const int TestDurationSeconds = 5;

		protected override int VmProcessorsCount => 4;
		protected override int MaxSchedulerCircleMilliseconds => 10;

		const float FamousPercent = 0.1f;
		const float GreetingFamousPossibilty = 0.8f;

		const int FamousAntsCount = (int)(AntsCount * FamousPercent);

		readonly Ant[] m_ants = new Ant[AntsCount];

		[TestMethod]
		public void PoliteAnts()
		{
			Assert.IsTrue(GreetingSeedsCount / FamousAntsCount >= 1);
			Assert.IsTrue(GreetingSeedsCount % FamousAntsCount == 0);

			Print($"Ants: {AntsCount:N0}");
			Print($"Greeting seeds: {GreetingSeedsCount:N0}");
			Print($"Test seconds: {TestDurationSeconds:N0}");
			Print($"VmProcessors: {VM.ProcessorsCount}");
			Print();

			// Initialize
			Profile("Create ants", () =>
			 {
				 for (int i = 0; i < m_ants.Length; i++)
				 {
					 bool famous = i < FamousAntsCount;

					 m_ants[i] = new Ant(this, $"Ant {i + 1}", famous);
				 }
			 });

			Profile("Start ants", () =>
			{
				const int InitialGreetingCount = GreetingSeedsCount / FamousAntsCount;

				for (int i = 0; i < m_ants.Length; i++)
				{
					var ant = m_ants[i];

					ant.Start(ant.IsFamous ? InitialGreetingCount : 0);
				}
			});

			// Wait for the test
			Sleep(TestDurationSeconds);

			VM.Dispose();

			// Final report
			var startDuration = (from ant in m_ants
								 select ant.StartDuration.TotalMilliseconds).Average();
			Print($"Start duration (avg): {TimeSpan.FromMilliseconds(startDuration)}");
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
			Print($"Messages rate (m/s): {(float)totalMessages / TestDurationSeconds:N0}");
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
			Print($"Greeting RTT (avg): {TimeSpan.FromMilliseconds(avgRTT)}");
			Print($"Greeting RTT (max): {TimeSpan.FromMilliseconds(maxRTT)}");
			Print($"Greeting RTT (min): {TimeSpan.FromMilliseconds(minRTT)}");
		}

		static void Profile(string name, Action action)
		{
			var sw = new Stopwatch();

			sw.Start();
			action();
			sw.Stop();

			var elapsed = sw.Elapsed;
			PrintStatic($"{name}: {elapsed}");
		}

		Vid GetGreetingTarget(Random random)
		{
			var greetingFamous = random.NextDouble() < GreetingFamousPossibilty;

			var i = greetingFamous ?
				random.Next(FamousAntsCount) :
				FamousAntsCount + random.Next(m_ants.Length - FamousAntsCount);

			return m_ants[i].Vid;
		}

		#region Ant

		class Ant : TestVipo
		{
			readonly Performance m_test;
			readonly Random m_random;
			public bool IsFamous { get; private set; }

			DateTime m_startTime;
			DateTime m_startCompleteTime;
			public int GreetingSent { get; private set; }
			public int GreetingReceived { get; private set; }
			List<TimeSpan> m_greetingRTTs = new List<TimeSpan>();

			#region Properties

			public TimeSpan StartDuration
			{
				get
				{
					Assert.IsFalse(m_startCompleteTime == DateTime.MinValue);

					return m_startCompleteTime - m_startTime;
				}
			}

			#endregion

			public Ant(Performance test, string symbol, bool famous)
				: base(test, symbol)
			{
				m_test = test;
				m_random = new Random(symbol.GetHashCode());
				IsFamous = famous;
			}

			public void Start(int initialGreetingCount)
			{
				m_startTime = DateTime.Now;

				Schedule(new StartSignal(initialGreetingCount));
			}

			class StartSignal
			{
				public int InitialGreetingCount { get; private set; }

				public StartSignal(int initialGreetingCount)
				{
					InitialGreetingCount = initialGreetingCount;
				}
			}

			protected override void Run(IReadOnlyList<VipoMessage> vipoMessages)
			{
				foreach (var m in vipoMessages)
				{
					switch (m.Message)
					{
						case SystemMessageSchedule schedule:
							Assert.AreEqual(m_startCompleteTime, DateTime.MinValue);

							m_startCompleteTime = DateTime.Now;

							if (schedule.Context is StartSignal s)
							{
								for (int i = 0; i < s.InitialGreetingCount; i++)
									SendGreetingToRandomAnt();
							}
							break;

						case GreetingMessage greeting:
							Send(m.From, new GreetingAckMessage(greeting.Timestamp));
							GreetingReceived++;

							// Forward the greeting to another
							SendGreetingToRandomAnt();
							break;

						case GreetingAckMessage ack:
							var ts = DateTime.Now - ack.Timestamp;
							lock (m_greetingRTTs)
								m_greetingRTTs.Add(ts);
							break;

						default:
							Assert.Fail();
							break;
					}
				}
			}

			void SendGreetingToRandomAnt()
			{
				Vid target;
				do
				{
					target = m_test.GetGreetingTarget(m_random);
				} while (target == Vid);

				Send(target, new GreetingMessage());

				GreetingSent++;
			}

			public IReadOnlyList<TimeSpan> GetGreetingRTTs()
			{
				lock (m_greetingRTTs)
				{
					return m_greetingRTTs.ToArray();
				}
			}
		}

		#endregion

		#region Greeting messages

		class GreetingMessage : Message
		{
			public DateTime Timestamp { get; private set; }

			public GreetingMessage()
			{
				Timestamp = DateTime.Now;
			}
		}

		class GreetingAckMessage : Message
		{
			public DateTime Timestamp { get; private set; }

			public GreetingAckMessage(DateTime timestamp)
			{
				Timestamp = timestamp;
			}
		}

		#endregion
	}
}
