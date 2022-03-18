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
			var one = new MyVipo(VM, "1");
			var two = new MyVipo(VM, "2");

			one.Schedule();
			two.Schedule();

			Sleep();

			var output = GetCustomOutput();

			Assert.IsTrue(output.Contains("MyVipo '1' ticks #1, messages [SystemMessageSchedule]"));
			Assert.IsTrue(output.Contains("MyVipo '2' ticks #1, messages [SystemMessageSchedule]"));
			Assert.IsTrue(output.Contains("MyVipo '2' ticks #2, messages [TestMessage]"));
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

				if (Vid == new Vid(1, 1, null))
					Send(new Vid(1, 2, null), new TestMessage());
			}
		}

		class TestMessage : Message
		{
		}
	}
}
