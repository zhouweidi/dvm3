using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace DvmTests
{
	[TestClass]
	public class TestScheduler : TestBase
	{
		#region Basic

		Scheduler m_scheduler;
		CancellationTokenSource m_cts;

		[TestInitialize]
		public void Initialize()
		{
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

		void OnError(Exception e)
		{
		}

		#endregion

		[TestMethod]
		public void TestMethod1()
		{
			var a = new Vipo("a");
			var b = new Vipo("b");

			HookConsoleOutput();

			m_scheduler.AddTickTask(new TickTask(a));
			m_scheduler.AddTickTask(new TickTask(b));
			m_scheduler.AddTickTask(new TickTask(a));

			Sleep();

			var consoleOutput = GetConsoleOutput();
			Assert.IsTrue(consoleOutput.Contains("Vipo 'a' ticks #1"));
			Assert.IsTrue(consoleOutput.Contains("Vipo 'a' ticks #2"));
			Assert.IsTrue(consoleOutput.Contains("Vipo 'b' ticks #1"));
		}
	}
}
