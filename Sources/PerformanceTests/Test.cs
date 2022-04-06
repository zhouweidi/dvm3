using Dvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace PerformanceTests
{
	abstract class TestConditionBase
	{
		public int VmProcessorsCount;
		public bool WithInspector;
	}

	abstract class Test : DisposableObject
	{
		CancellationTokenSource m_cts;
		VirtualMachine m_vm;
		readonly ManualResetEvent m_exceptionOccured = new ManualResetEvent(false);
		readonly Inspector m_inspector;

		public VirtualMachine VM => m_vm;
		public Inspector Inspector => m_inspector;

		protected Test(TestConditionBase condition)
		{
			m_cts = new CancellationTokenSource();
			m_vm = new VirtualMachine(condition.VmProcessorsCount, m_cts.Token, condition.WithInspector);
			m_inspector = m_vm.Inspector;

			m_vm.OnError += OnError;

			Assert(m_vm.State == VirtualMachineState.Running);
		}

		protected override void OnDispose(bool explicitCall)
		{
			if (explicitCall)
			{
				Assert(m_vm.Exception == null);
				Assert(m_vm.State == VirtualMachineState.Running);

				m_cts.Cancel();

				SafeDispose(ref m_vm);
				SafeDispose(ref m_cts);
			}
		}

		protected virtual void OnError(Exception e)
		{
			Print(e.ToString());

			m_exceptionOccured.Set();

			Assert(e);
		}

		public abstract void Run();

		public string GetPrintContent()
		{
			lock (m_printContentLock)
				return m_persistentPrintContent.ToString();
		}

		#region Assert

		class AssertException : Exception
		{
			public AssertException()
			{
			}

			public AssertException(string message)
				: base(message)
			{
			}

			public AssertException(Exception innerException)
				: base("", innerException)
			{
			}
		}

		public static void Assert(bool condition, string message = null)
		{
			if (!condition)
				throw new AssertException(message ?? string.Empty);
		}

		public static void Assert(Exception ex)
		{
			if (ex != null)
				throw new AssertException(ex);
		}

		public static void Assert(string message)
		{
			throw new AssertException(message);
		}

		#endregion

		#region Sleep

		protected void Sleep(double seconds)
		{
			//Thread.Sleep((int)(seconds * 1000));
			var r = m_exceptionOccured.WaitOne((int)(seconds * 1000));

			Assert(!r);
		}

		protected void Sleep()
		{
			Sleep(0.5);
		}

		#endregion

		#region Print

		static readonly char[] NewlineCharacters = new[] { '\n', '\r' };

		readonly object m_printContentLock = new object();
		readonly StringBuilder m_printContent = new StringBuilder();
		readonly StringBuilder m_persistentPrintContent = new StringBuilder();

		internal void Print(string content = "")
		{
			lock (m_printContentLock)
			{
				m_printContent.AppendLine(content);
				m_persistentPrintContent.AppendLine(content);
			}

			Console.WriteLine(content);
		}

		protected string[] TakePrintOutput()
		{
			lock (m_printContentLock)
			{
				var results = m_printContent
					.ToString()
					.Split(NewlineCharacters, StringSplitOptions.RemoveEmptyEntries);

				m_printContent.Clear();

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

		protected static void WriteAndOpenFile(string fileName, string content)
		{
			File.WriteAllText(fileName, content);

			var proc = new Process
			{
				EnableRaisingEvents = false
			};

			proc.StartInfo.UseShellExecute = true;
			proc.StartInfo.FileName = fileName;
			proc.Start();

			proc.WaitForExit();
		}

		protected static void PrintInspector(Test test)
		{
			var inspector = test.Inspector;

			test.Print();
			test.Print("-- Inspector --");
			test.Print($"Discarded messages: {inspector.DiscardedMessages:N0}");
			test.Print($"JobQueue max size: {inspector.JobQueueMaxSize:N0}");
		}

		protected static string BuildConfiguration =>
#if DEBUG
			"Debug";
#else
			"Release";
#endif

		#endregion
	}
}
