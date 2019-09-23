using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class BasicSchedule : TestSchedulerBase
	{
		[TestMethod]
		public void AddNewVipos()
		{
			var a = new Vipo("a");
			var b = new Vipo("b");

			HookConsoleOutput();

			TheScheduler.AddVipo(a);
			TheScheduler.AddVipo(b);

			Sleep();

			var consoleOutput = GetConsoleOutput();
			Assert.IsTrue(consoleOutput.Contains("Vipo 'a' ticks #1"));
			Assert.IsTrue(consoleOutput.Contains("Vipo 'b' ticks #1"));
		}
	}
}
