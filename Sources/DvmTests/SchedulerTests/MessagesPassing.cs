using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class MessagesPassing : VmTestBase
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

			var index1 = Array.IndexOf(output, "MyVipo '1' ticks #1, messages [SystemMessageSchedule]");
			Assert.IsTrue(index1 >= 0);

			var index2 = Array.IndexOf(output, "MyVipo '2' ticks #1, messages [SystemMessageSchedule]");
			Assert.IsTrue(index2 >= 0);

			var index3 = Array.IndexOf(output, "MyVipo '2' ticks #2, messages [TestMessage]");
			Assert.IsTrue(index3 >= 0);
			Assert.IsTrue(index3 > index1 && index3 > index2);
		}

		class MyVipo : Vipo
		{
			int m_tickedCount;

			public MyVipo(VirtualMachine vm, string name)
				: base(vm, name)
			{
			}

			protected override void Run(IReadOnlyList<VipoMessage> messages)
			{
				++m_tickedCount;

				var text = $"MyVipo '{Symbol}' ticks #{m_tickedCount}, messages [{JoinMessageBodies(messages)}]";
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
