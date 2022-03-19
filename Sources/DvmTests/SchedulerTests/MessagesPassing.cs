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
			var one = new MyVipo(this, "1");
			var two = new MyVipo(this, "2");

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

		class MyVipo : TestVipo
		{
			int m_tickedCount;

			public MyVipo(VmTestBase test, string name)
				: base(test, name)
			{
			}

			protected override void Run(IReadOnlyList<VipoMessage> vipoMessages)
			{
				++m_tickedCount;

				var text = $"MyVipo '{Symbol}' ticks #{m_tickedCount}, messages [{JoinMessageBodies(vipoMessages)}]";
				Print(text);

				if (Vid == new Vid(1, 1, null))
					Send(new Vid(1, 2, null), new TestMessage());
			}
		}

		class TestMessage : Message
		{
		}
	}
}
