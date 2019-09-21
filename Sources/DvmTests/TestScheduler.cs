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
		StringWriter m_consoleOutput;

		[TestInitialize]
		public void Initialize()
		{
			m_cts = new CancellationTokenSource();
			m_scheduler = new Scheduler(4, m_cts.Token);

			m_scheduler.OnError += OnError;

			Assert.AreEqual(m_scheduler.State, SchedulerState.Running);
		}

		[TestCleanup]
		public void Cleanup()
		{
			Assert.IsNull(m_scheduler.Exception);
			Assert.AreEqual(m_scheduler.State, SchedulerState.Running);
			
			m_cts.Cancel();

			DisposableObject.SafeDispose(ref m_scheduler);
			DisposableObject.SafeDispose(ref m_cts);

			if (DisposableObject.SafeDispose(ref m_consoleOutput))
				ResetConsoleOutput();
		}

		void OnError(Exception e)
		{
		}

		#endregion

		[TestMethod]
		public void TestMethod1()
		{
			m_consoleOutput = HookConsoleOutput();

			var sb = new StringBuilder();
			for (int i = 0; i < 2; i++)
			{
				Console.Write("World");
				sb.Append("World");

				Sleep();
			}

			Assert.AreEqual(sb.ToString(), m_consoleOutput.ToString());

			m_scheduler.AddTickTask(new TickTask());
		}
	}
}
