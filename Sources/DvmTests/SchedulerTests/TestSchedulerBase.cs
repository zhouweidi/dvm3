using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class TestSchedulerBase : TestBase
	{
		Scheduler m_scheduler;
		CancellationTokenSource m_cts;
		ManualResetEvent m_exceptionOccured = new ManualResetEvent(false);

		[TestInitialize]
		public override void Initialize()
		{
			base.Initialize();

			m_cts = new CancellationTokenSource();
			m_scheduler = new Scheduler(4, m_cts.Token);

			m_scheduler.OnError += OnError;

			Assert.AreEqual(m_scheduler.State, SchedulerState.Running);
		}

		[TestCleanup]
		public override void Cleanup()
		{
			Assert.IsNull(m_scheduler.Exception);
			Assert.AreEqual(m_scheduler.State, SchedulerState.Running);

			m_cts.Cancel();

			DisposableObject.SafeDispose(ref m_scheduler);
			DisposableObject.SafeDispose(ref m_cts);

			base.Cleanup();
		}

		protected virtual void OnError(Exception e)
		{
			m_exceptionOccured.Set();
		}

		protected override void Sleep(double seconds)
		{
			Assert.IsFalse(m_exceptionOccured.WaitOne((int)(seconds * 1000)));
		}

		internal Scheduler TheScheduler
		{
			get { return m_scheduler; }
		}
	}
}
