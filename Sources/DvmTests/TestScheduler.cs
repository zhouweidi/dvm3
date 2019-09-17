using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace DvmTests
{
	[TestClass]
	public class TestScheduler
	{
		Scheduler m_scheduler;
		CancellationTokenSource m_cts;

		[TestInitialize]
		public void Initialize()
		{
			m_cts = new CancellationTokenSource();
			m_scheduler = new Scheduler(4, m_cts.Token);
		}

		[TestCleanup]
		public void Cleanup()
		{
			m_cts.Cancel();

			DisposableObject.SafeDispose(ref m_scheduler);
			DisposableObject.SafeDispose(ref m_cts);
		}

		[TestMethod]
		public void TestMethod1()
		{
			//m_scheduler.
		}
	}
}
