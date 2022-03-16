using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

		protected virtual void Sleep(double seconds)
		{
			Thread.Sleep((int)(seconds * 1000));
		}

		protected void Sleep()
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

		static readonly char[] NewlineCharacters = new[] { '\n', '\r' };

		protected string[] GetConsoleOutput()
		{
			if (m_consoleOutput == null)
				throw new InvalidOperationException("Need to hood console output first");

			m_consoleOutput.Flush();

			return m_consoleOutput
				.ToString()
				.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries);
		}

		#endregion

		#region Utilities

		protected static readonly Message DefaultMessage = new Message();

		protected static string JoinMessageBodies(IEnumerable<VipoMessage> vipoMessages)
		{
			return string.Join(',', from vm in vipoMessages
									select vm.Message);
		}

		#endregion
	}
}
