using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class BasicSchedule : VmTestBase
	{
		[TestMethod]
		public void RunVipos()
		{
			var a = new MyVipo(this, "a");
			var b = new MyVipo(this, "b");

			Assert.AreEqual(VM.ViposCount, 0);

			a.Schedule();
			b.Schedule();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);

			var output = GetCustomOutput();
			Assert.IsTrue(output.Contains("'a' ticks #1, messages [SystemMessageSchedule]"));
			Assert.IsTrue(output.Contains("'b' ticks #1, messages [SystemMessageSchedule]"));

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
			Assert.IsTrue(output.Contains("'a' ticks #2, messages [SystemMessageSchedule]"));
			Assert.IsTrue(output.Contains("'b' ticks #2, messages [SystemMessageSchedule]"));
		}

		class MyVipo : TestVipo
		{
			int m_tickedCount;

			public MyVipo(VmTestBase test, string symbol)
				: base(test, symbol)
			{
			}

			protected override void Run(IReadOnlyList<VipoMessage> vipoMessages)
			{
				++m_tickedCount;

				var text = $"'{Symbol}' ticks #{m_tickedCount}, messages [{JoinMessageBodies(vipoMessages)}]";
				Print(text);
			}
		}

		[TestMethod]
		public void DisposeVipo()
		{
			var a = new MyVipo(this, "a");
			var b = new MyVipoWithOnDispose(this, "b");

			Assert.AreEqual(VM.ViposCount, 0);

			a.Schedule();
			b.Schedule();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);

			var output = GetCustomOutput();
			Assert.IsTrue(output.Contains("'a' ticks #1, messages [SystemMessageSchedule]"));
			Assert.IsTrue(output.Contains("'b' ticks #1, messages [SystemMessageSchedule]"));

			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);

			VM.Dispose();
			Assert.AreEqual(VM.ViposCount, 0);

			var output2 = GetCustomOutput();
			Assert.IsTrue(output2.Length == 1 && output2[0] == "'b' OnDispose()");
			Assert.IsFalse(a.IsAttached);
			Assert.IsFalse(b.IsAttached);

			// After VM disposs, no side effect to call the following Dispose()
			a.Dispose();
			b.Dispose();
			VM.Dispose();

			var output3 = GetCustomOutput();
			Assert.IsTrue(output3.Length == 0);

			// No more call to a disposed object
			Assert.ThrowsException<ObjectDisposedException>(() => a.Schedule());
		}

		class MyVipoWithOnDispose : MyVipo
		{
			public MyVipoWithOnDispose(VmTestBase test, string symbol)
				: base(test, symbol)
			{
			}

			protected override void OnDispose()
			{
				var text = $"'{Symbol}' OnDispose()";
				Print(text);

				base.OnDispose();
			}
		}
	}
}
