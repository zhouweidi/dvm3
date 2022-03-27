using Dvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PerformanceTests.PoliteAnts
{
	class Ant : TestVipo
	{
		readonly PoliteAnts m_test;
		readonly Random m_random;
		public bool IsFamous { get; private set; }
		readonly float m_greetingFakeProcessingSeconds;

		DateTime m_registerBeginTime;
		DateTime m_registerEndTime;
		public int GreetingSent { get; private set; }
		public int GreetingReceived { get; private set; }
		readonly List<TimeSpan> m_greetingRTTs = new List<TimeSpan>();

		#region Properties

		public TimeSpan RegisterDuration
		{
			get
			{
				Assert(m_registerEndTime != DateTime.MinValue);

				return m_registerEndTime - m_registerBeginTime;
			}
		}

		#endregion

		public Ant(PoliteAnts test, string symbol, bool famous, float greetingFakeProcessingSeconds)
			: base(test, symbol)
		{
			m_test = test;
			m_random = new Random(symbol.GetHashCode());
			IsFamous = famous;
			m_greetingFakeProcessingSeconds = greetingFakeProcessingSeconds;
		}

		public void Register()
		{
			m_registerBeginTime = DateTime.Now;

			Schedule();
		}

		public void Start(int initialGreetingCount)
		{
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

		protected override void Run(VipoMessageStream messageStream)
		{
			while (messageStream.GetNext(out VipoMessage m))
			{
				switch (m.Message)
				{
					case UserScheduleMessage schedule:
						if (m_registerEndTime == DateTime.MinValue)
							m_registerEndTime = DateTime.Now;

						if (schedule.Context is StartSignal s)
						{
							for (int i = 0; i < s.InitialGreetingCount; i++)
								SendGreetingToRandomAnt();
						}
						break;

					case GreetingMessage greeting:
						// Fake process
						if (m_greetingFakeProcessingSeconds > 0)
							FakeProcessingWork(m_greetingFakeProcessingSeconds);

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
						Assert("Unknown message");
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

		void FakeProcessingWork(float seconds)
		{
			var sw = new Stopwatch();

			sw.Start();

			while (sw.Elapsed.TotalSeconds < seconds)
			{
				for (int i = 0; i < 10; i++)
				{
				}
			}
		}

		public IReadOnlyList<TimeSpan> GetGreetingRTTs()
		{
			lock (m_greetingRTTs)
			{
				return m_greetingRTTs.ToArray();
			}
		}
	}
}
