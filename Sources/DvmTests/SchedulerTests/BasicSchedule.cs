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
		public void RunVipos()
		{
			var a = new MyVipo(VM, "a");
			var b = new MyVipo(VM, "b");

			Assert.AreEqual(VM.ViposCount, 0);

			a.Schedule();
			b.Schedule();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);

			var output = GetCustomOutput();
			Assert.IsTrue(output.Contains("MyVipo 'a' ticks #1, messages [SystemMessageSchedule]"));
			Assert.IsTrue(output.Contains("MyVipo 'b' ticks #1, messages [SystemMessageSchedule]"));

			a.Detach();
			a.Detach(); // Call Detach() on the same object twice
			b.Detach();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 0);

			a.Schedule();
			b.Schedule();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);

			output = GetCustomOutput();
			Assert.IsTrue(output.Contains("MyVipo 'a' ticks #2, messages [SystemMessageSchedule]"));
			Assert.IsTrue(output.Contains("MyVipo 'b' ticks #2, messages [SystemMessageSchedule]"));
		}

		class MyVipo : Vipo
		{
			int m_tickedCount;

			public MyVipo(VirtualMachine vm, string symbol)
				: base(vm, symbol)
			{
			}

			protected override void Run(VipoJob job)
			{
				++m_tickedCount;

				var text = $"MyVipo '{Symbol}' ticks #{m_tickedCount}, messages [{JoinMessageBodies(job.Messages)}]";
				PrintLineStatic(text);
			}
		}
	}
}
