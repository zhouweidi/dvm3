using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;

namespace DvmTests.DvmScheduler
{
	[TestClass]
	public class TestSchedulerBase : TestBase
	{
		Scheduler m_scheduler;
		CancellationTokenSource m_cts;

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
		}

		internal Scheduler TheScheduler
		{
			get { return m_scheduler; }
		}
	}
}
