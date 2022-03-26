using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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

			Assert.AreEqual(VM.ViposCount, 2);

			a.Schedule();
			b.Schedule();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);

			var output = GetCustomOutput();
			Assert.IsTrue(output.Contains("'a' ticks #1, messages [UserScheduleMessage]"));
			Assert.IsTrue(output.Contains("'b' ticks #1, messages [UserScheduleMessage]"));

			a.Schedule();
			b.Schedule();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);

			output = GetCustomOutput();
			Assert.IsTrue(output.Contains("'a' ticks #2, messages [UserScheduleMessage]"));
			Assert.IsTrue(output.Contains("'b' ticks #2, messages [UserScheduleMessage]"));
		}

		class MyVipo : TestVipo
		{
			int m_tickedCount;

			public MyVipo(VmTestBase test, string symbol)
				: base(test, symbol)
			{
			}

			protected override void Run(VipoMessageStream messageStream)
			{
				++m_tickedCount;

				var text = $"'{Symbol}' ticks #{m_tickedCount}, messages [{JoinMessages(messageStream)}]";
				Print(text);
			}
		}

		[TestMethod]
		public void DisposeVipo()
		{
			var a = new MyVipoWithOnDispose(this, "a");
			var b = new MyVipoWithOnDispose(this, "b");

			Assert.AreEqual(VM.ViposCount, 2);

			a.Schedule();
			b.Schedule();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);
			{
				var output = GetCustomOutput();
				Assert.IsTrue(output.Contains("'a' ticks #1, messages [UserScheduleMessage]"));
				Assert.IsTrue(output.Contains("'b' ticks #1, messages [UserScheduleMessage]"));
			}

			Sleep();
			Assert.AreEqual(VM.ViposCount, 2);

			a.Dispose();
			Sleep();
			Assert.AreEqual(VM.ViposCount, 1);
			{
				var output = GetCustomOutput();
				Assert.IsTrue(output.Length == 1 && output[0] == "'a' OnDispose()");
			}

			VM.Dispose();
			Assert.AreEqual(VM.ViposCount, 0);
			{
				var output = GetCustomOutput();
				Assert.IsTrue(output.Length == 1 && output[0] == "'b' OnDispose()");
			}

			// After VM disposs, no side effect to call the following Dispose()
			a.Dispose();
			b.Dispose();
			VM.Dispose();

			{
				var output = GetCustomOutput();
				Assert.IsTrue(output.Length == 0);
			}

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
