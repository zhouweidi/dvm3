using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class LifeScope : TestBase
	{
		[TestMethod]
		public void Normal()
		{
			using (var cts = new CancellationTokenSource())
			{
				var scheduler = new Scheduler(4, cts.Token);
				using (scheduler)
				{
					Assert.AreEqual(scheduler.State, SchedulerState.Running);
					Sleep();
				}

				Assert.AreEqual(scheduler.State, SchedulerState.Stopped);
				Assert.IsNull(scheduler.Exception);
			}
		}

		[TestMethod]
		public void NoCancellationToken()
		{
			var scheduler = new Scheduler(4, CancellationToken.None);
			using (scheduler)
			{
				Assert.AreEqual(scheduler.State, SchedulerState.Running);
				Sleep();
			}

			Assert.AreEqual(scheduler.State, SchedulerState.Stopped);
			Assert.IsNull(scheduler.Exception);
		}

		[TestMethod]
		public void ExplicitCancel()
		{
			using (var cts = new CancellationTokenSource())
			{
				var scheduler = new Scheduler(4, cts.Token);
				using (scheduler)
				{
					Assert.AreEqual(scheduler.State, SchedulerState.Running);

					Sleep();
					cts.Cancel();
				}

				Assert.AreEqual(scheduler.State, SchedulerState.Stopped);
				Assert.IsNull(scheduler.Exception);
			}
		}

		[TestMethod]
		public void ExplicitCancelImmediately()
		{
			using (var cts = new CancellationTokenSource())
			{
				var scheduler = new Scheduler(4, cts.Token);
				using (scheduler)
				{
					Assert.AreEqual(scheduler.State, SchedulerState.Running);

					cts.Cancel();
					Sleep();
				}

				Assert.AreEqual(scheduler.State, SchedulerState.Stopped);
				Assert.IsNull(scheduler.Exception);
			}
		}
	}
}
