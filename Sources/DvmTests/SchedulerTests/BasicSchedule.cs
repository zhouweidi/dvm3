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

			a.Schedule();
			b.Schedule();

			Sleep();

			var output = GetCustomOutput();

			Assert.IsTrue(output.Contains("MyVipo 'a' ticks #1, messages [SystemMessageSchedule]"));
			Assert.IsTrue(output.Contains("MyVipo 'b' ticks #1, messages [SystemMessageSchedule]"));
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
				PrintLineStatic(text);
			}
		}
	}
}
