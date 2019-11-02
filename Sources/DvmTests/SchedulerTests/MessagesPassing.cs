using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class MessagesPassing : TestSchedulerBase
	{
		[TestMethod]
		public void AddNewVipos()
		{
			var one = new MyVipo(TheScheduler, "1");
			var two = new MyVipo(TheScheduler, "2");

			one.Start();
			two.Start();

			HookConsoleOutput();

			Sleep();

			var consoleOutput = GetConsoleOutput();

			Assert.IsTrue(consoleOutput.Contains("MyVipo '1' ticks #1, messages []"));
			Assert.IsTrue(consoleOutput.Contains("MyVipo '2' ticks #1, messages []"));
			Assert.IsTrue(consoleOutput.Contains("MyVipo '2' ticks #2, messages [MessageBase]"));
		}

		class MyVipo : Vipo
		{
			int m_tickedCount;

			public MyVipo(Scheduler scheduler, string name)
				: base(scheduler, name, CallbackOptions.All)
			{
			}

			protected override void OnTick(TickTask tickTask)
			{
				++m_tickedCount;

				Console.WriteLine($"MyVipo '{Name}' ticks #{m_tickedCount}, messages [{JoinMessageBodies(tickTask.Messages)}]");

				if (Vid == new Vid(1, 1, null))
					SendMessage(new Vid(1, 2, null), DefaultMessageBody);
			}
		}
	}
}
