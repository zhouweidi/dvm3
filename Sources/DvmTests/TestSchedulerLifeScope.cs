using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace DvmTests
{
	[TestClass]
	public class TestSchedulerLifeScope
	{
		[TestMethod]
		public void TestLifeScope()
		{
			using (var cts = new CancellationTokenSource())
			using (var scheduler = new Scheduler(4, cts.Token))
			{
				Sleep();
			}
		}

		[TestMethod]
		public void TestLifeScope_ExplicitCancel()
		{
			using (var cts = new CancellationTokenSource())
			using (var scheduler = new Scheduler(4, cts.Token))
			{
				Sleep();

				cts.Cancel();
			}
		}

		static void Sleep()
		{
			Thread.Sleep(1000);
		}
	}
}
