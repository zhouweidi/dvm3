using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class BasicSchedule : TestSchedulerBase
	{
		[TestMethod]
		public void AddNewVipos()
		{
			var a = new MyVipo(TheVM, "a");
			var b = new MyVipo(TheVM, "b");

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

			public MyVipo(VirtualMachine vm, string name)
				: base(vm, name, CallbackOptions.All)
			{
			}

			protected override void OnTick(VipoJob job)
			{
				++m_tickedCount;

				Console.WriteLine($"MyVipo '{Name}' ticks #{m_tickedCount}, messages [{string.Join(',', job.Messages)}]");
			}
		}
	}
}
