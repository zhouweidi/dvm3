using System;
using System.IO;
using System.Threading;

namespace DvmTests
{
	public class TestBase
	{
		protected static void Sleep()
		{
			Thread.Sleep(500);
		}

		protected static StringWriter HookConsoleOutput()
		{
			var sw = new StringWriter();
			Console.SetOut(sw);

			return sw;
		}

		protected static void ResetConsoleOutput()
		{
			var sw = new StreamWriter(Console.OpenStandardOutput());
			sw.AutoFlush = true;

			Console.SetOut(sw);
		}
	}
}
