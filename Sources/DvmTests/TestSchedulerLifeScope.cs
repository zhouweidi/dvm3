using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace DvmTests
{
	[TestClass]
	public class TestSchedulerLifeScope
	{
		[TestMethod]
		public void Normal()
		{
			using (var cts = new CancellationTokenSource())
			using (var scheduler = new Scheduler(4, cts.Token))
			{
				Sleep();
			}
		}

		[TestMethod]
		public void NoCancellationToken()
		{
			using (var scheduler = new Scheduler(4, CancellationToken.None))
			{
				Sleep();
			}
		}

		[TestMethod]
		public void ExplicitCancel()
		{
			using (var cts = new CancellationTokenSource())
			using (var scheduler = new Scheduler(4, cts.Token))
			{
				Sleep();

				cts.Cancel();
			}
		}

		[TestMethod]
		public void ExplicitCancelImmediately()
		{
			using (var cts = new CancellationTokenSource())
			using (var scheduler = new Scheduler(4, cts.Token))
			{
				cts.Cancel();

				Sleep();
			}
		}

		static void Sleep()
		{
			Thread.Sleep(500);
		}
	}
}
