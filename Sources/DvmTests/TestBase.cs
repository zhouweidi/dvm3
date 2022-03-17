using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DvmTests
{
	[TestClass]
	public class TestBase
	{
		#region Basic

		static TestBase s_currentTest;

		StringWriter m_consoleOutput;

		[TestInitialize]
		public virtual void Initialize()
		{
			s_currentTest = this;
		}

		[TestCleanup]
		public virtual void Cleanup()
		{
			if (DisposableObject.SafeDispose(ref m_consoleOutput))
				ResetConsoleOutput();

			s_currentTest = null;
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
			var sw = new StreamWriter(Console.OpenStandardOutput())
			{
				AutoFlush = true
			};

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

		#region Custom output

		readonly object m_customOutputLock = new object();
		readonly StringBuilder m_customOutput = new StringBuilder();

		protected void Print(string content)
		{
			lock (m_customOutputLock)
				m_customOutput.Append(content);
		}

		protected void PrintLine(string content)
		{
			lock (m_customOutputLock)
				m_customOutput.AppendLine(content);
		}

		protected static void PrintStatic(string content) => s_currentTest.Print(content);
		protected static void PrintLineStatic(string content) => s_currentTest.PrintLine(content);

		protected string[] GetCustomOutput()
		{
			lock (m_customOutputLock)
			{
				return m_customOutput
					.ToString()
					.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries);
			}
		}

		#endregion

		#region Utilities

		protected static string JoinMessageBodies(IEnumerable<VipoMessage> vipoMessages)
		{
			return string.Join(',', from vm in vipoMessages
									select vm.Message);
		}

		#endregion
	}
}
