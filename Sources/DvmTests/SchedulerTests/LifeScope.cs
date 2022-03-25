using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class LifeScope : TestBase
	{
		[TestMethod]
		public void Dispose()
		{
			using var cts = new CancellationTokenSource();
			var vm = new VirtualMachine(4, cts.Token);
			using (vm)
			{
				Assert.AreEqual(vm.State, VirtualMachineState.Running);
				Sleep();
			}

			Assert.AreEqual(vm.State, VirtualMachineState.Ended);
			Assert.IsNull(vm.Exception);
		}

		[TestMethod]
		public void Dispose_NoCancellationToken()
		{
			var vm = new VirtualMachine(4, CancellationToken.None);
			using (vm)
			{
				Assert.AreEqual(vm.State, VirtualMachineState.Running);
				Sleep();
			}

			Assert.AreEqual(vm.State, VirtualMachineState.Ended);
			Assert.IsNull(vm.Exception);
		}

		[TestMethod]
		public void Cancel()
		{
			using var cts = new CancellationTokenSource();
			var vm = new VirtualMachine(4, cts.Token);
			using (vm)
			{
				Assert.AreEqual(vm.State, VirtualMachineState.Running);

				cts.Cancel();
				// vm.State can be any state of (running, ending, ended) immediately after Cancal() call

				// Sleep a while to let VM update the state
				Sleep();

				var state = vm.State;
				Assert.IsTrue(state == VirtualMachineState.Ending || state == VirtualMachineState.Ended);
			}

			Assert.AreEqual(vm.State, VirtualMachineState.Ended);
			Assert.IsNull(vm.Exception);
		}

		[TestMethod]
		public void CancelAndDispose()
		{
			using var cts = new CancellationTokenSource();
			var vm = new VirtualMachine(4, cts.Token);
			using (vm)
			{
				Assert.AreEqual(vm.State, VirtualMachineState.Running);

				cts.Cancel();
				// vm.State can be any state of (running, ending, ended) immediately after Cancal() call
			}

			Assert.AreEqual(vm.State, VirtualMachineState.Ended);
			Assert.IsNull(vm.Exception);
		}
	}
}
