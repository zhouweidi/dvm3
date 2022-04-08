using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading.Tasks;

namespace DvmTests.VipoTests
{
	[TestClass]
	public class Coroutines : VmTestBase
	{
		[TestMethod]
		public void Basic()
		{
			var a = new MyAsyncVipo(this, "a");
			var b = new MyVipo(this, "b");
			Assert.AreEqual(VM.ViposCount, 2);

			b.Schedule(a.Vid);

			Sleep();

			{
				var output = GetCustomOutput();
				Assert.IsTrue(output.Length == 3);
				Assert.IsTrue(output[0] == "0 0");
				Assert.IsTrue(output[1] == "1 1");
				Assert.IsTrue(output[2] == "2 2");
			}
		}

		class MyAsyncVipo : AsyncVipo
		{
			readonly VmTestBase m_test;

			#region Test supports

			public MyAsyncVipo(VmTestBase test, string symbol)
				: base(test.VM, symbol)
			{
				m_test = test;
			}

			protected override void OnError(Exception e)
			{
				Assert.Fail(e.ToString());
			}

			protected void Print(string content = "")
			{
				m_test.Print(content);
			}

			#endregion

			protected override async Task RunAsync()
			{
				Vid from;
				{
					var vipoMessage = await Receive();
					if (vipoMessage.Message is TestMessage message)
					{
						Print($"0 {message.Value}");

						from = vipoMessage.From;
					}
					else
					{
						Assert.Fail($"Unexpected message {vipoMessage}");
						from = Vid.Empty;
					}
				}

				{
					var vipoMessage = await ReceiveFrom(from, 200);
					if (vipoMessage.Message is TestMessage message)
						Print($"1 {message.Value}");
					else
						Assert.Fail($"Unexpected message {vipoMessage}");
				}

				{
					var now = VmTiming.Now;

					await Sleep(50);

					var duration = VmTiming.Now - now;
					Assert.IsTrue(duration >= 50);
				}

				{
					var vipoMessage = await ReceiveFrom<TestMessage>(from);
					if (vipoMessage.Message is TestMessage message)
						Print($"2 {message.Value}");
					else
						Assert.Fail($"Unexpected message {vipoMessage}");
				}
			}
		}

		class MyVipo : TestVipo
		{
			Vid m_targetVid;
			int m_timerId;
			int m_value;

			public MyVipo(VmTestBase test, string symbol)
				: base(test, symbol)
			{
			}

			protected override void Run(IVipoMessageStream messageStream)
			{
				while (messageStream.GetNext(out VipoMessage vipoMessage))
				{
					switch (vipoMessage.Message)
					{
						case UserScheduleMessage schedule:
							m_targetVid = (Vid)schedule.Context;
							m_timerId = SetRepeatedTimer(100);
							break;

						case UserTimerMessage _:
							Send(m_targetVid, new TestMessage()
							{
								Value = m_value
							});

							if (++m_value == 3)
							{
								ResetTimer(m_timerId);
								m_timerId = -1;
							}
							break;

						default:
							Assert.Fail("Unknown message");
							break;
					}
				}
			}
		}

		class TestMessage : Message
		{
			public int Value;
		}
	}
}
