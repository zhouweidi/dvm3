using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace DvmTests
{
	[TestClass]
	public abstract class TestBase
	{
		#region Basic

		static TestBase s_currentTest;

		[TestInitialize]
		public virtual void Initialize()
		{
			s_currentTest = this;
		}

		[TestCleanup]
		public virtual void Cleanup()
		{
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

		#region Output

		static readonly char[] NewlineCharacters = new[] { '\n', '\r' };

		readonly object m_customOutputLock = new object();
		readonly StringBuilder m_customOutput = new StringBuilder();
		TestContext m_testContextInstance;

		public TestContext TestContext
		{
			set { m_testContextInstance = value; }
		}

		internal void Print(string content = "")
		{
			lock (m_customOutputLock)
				m_customOutput.AppendLine(content);

			m_testContextInstance.WriteLine(content);
		}

		protected static void PrintStatic(string content) => s_currentTest.Print(content);

		protected string[] GetCustomOutput(bool reset = true)
		{
			lock (m_customOutputLock)
			{
				var results = m_customOutput
					.ToString()
					.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries);

				if (reset)
					m_customOutput.Clear();

				return results;
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
