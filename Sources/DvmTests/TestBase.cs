using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;

namespace DvmTests
{
	[TestClass]
	public class TestBase
	{
		#region Basic

		StringWriter m_consoleOutput;

		[TestInitialize]
		public virtual void Initialize()
		{ }

		[TestCleanup]
		public virtual void Cleanup()
		{
			if (DisposableObject.SafeDispose(ref m_consoleOutput))
				ResetConsoleOutput();
		}

		#endregion

		#region Sleep

		protected static void Sleep(double seconds)
		{
			Thread.Sleep((int)(seconds * 1000));
		}

		protected static void Sleep()
		{
			Sleep(0.5);
		}

		#endregion

		#region Console output

		protected void HookConsoleOutput()
		{
			if (m_consoleOutput != null)
				throw new InvalidOperationException("Console output has been already hooked");

			m_consoleOutput = new StringWriter();
			Console.SetOut(m_consoleOutput);
		}

		void ResetConsoleOutput()
		{
			var sw = new StreamWriter(Console.OpenStandardOutput());
			sw.AutoFlush = true;

			Console.SetOut(sw);
		}

		protected string GetConsoleOutput()
		{
			if (m_consoleOutput == null)
				throw new InvalidOperationException("Need to hood console output first");

			m_consoleOutput.Flush();

			return m_consoleOutput.ToString();
		}

		#endregion
	}
}
