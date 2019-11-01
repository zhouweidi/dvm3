using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class BasicSchedule : TestSchedulerBase
	{
		[TestMethod]
		public void AddNewVipos()
		{
			var a = new MyVipo(TheScheduler, "a");
			var b = new MyVipo(TheScheduler, "b");

			a.Start();
			b.Start();

			HookConsoleOutput();

			Sleep();

			var consoleOutput = GetConsoleOutput();
			Assert.IsTrue(consoleOutput.Contains("MyVipo 'a' ticks #1, messages []"));
			Assert.IsTrue(consoleOutput.Contains("MyVipo 'b' ticks #1, messages []"));
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

				Console.WriteLine($"MyVipo '{Name}' ticks #{m_tickedCount}, messages [{string.Join(',', tickTask.Messages)}]");
			}
		}
	}
}
