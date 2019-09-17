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
		}

		[TestCleanup]
		public void Cleanup()
		{
			m_cts.Cancel();

			DisposableObject.SafeDispose(ref m_scheduler);
			DisposableObject.SafeDispose(ref m_cts);

			if (DisposableObject.SafeDispose(ref m_consoleOutput))
				ResetConsoleOutput();
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
