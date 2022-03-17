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
			var one = new MyVipo(TheVM, "1");
			var two = new MyVipo(TheVM, "2");

			one.Schedule();
			two.Schedule();

			HookConsoleOutput();

			Sleep();

			var consoleOutput = GetConsoleOutput();

			Assert.IsTrue(consoleOutput.Contains("MyVipo '1' ticks #1, messages [SystemMessageSchedule]"));
			Assert.IsTrue(consoleOutput.Contains("MyVipo '2' ticks #1, messages [SystemMessageSchedule]"));
			Assert.IsTrue(consoleOutput.Contains("MyVipo '2' ticks #2, messages [TestMessage]"));
		}

		class MyVipo : Vipo
		{
			int m_tickedCount;

			public MyVipo(VirtualMachine vm, string name)
				: base(vm, name)
			{
			}

			protected override void Run(VipoJob job)
			{
				++m_tickedCount;

				var text = $"MyVipo '{Symbol}' ticks #{m_tickedCount}, messages [{JoinMessageBodies(job.Messages)}]";
				Console.WriteLine(text);

				if (Vid == new Vid(1, 1, null))
					Send(new Vid(1, 2, null), DefaultMessage);
			}
		}
	}
}
