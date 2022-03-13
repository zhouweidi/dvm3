using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading;

namespace DvmTests.SchedulerTests
{
	[TestClass]
	public class LifeScope : TestBase
	{
		[TestMethod]
		public void Normal()
		{
			using (var cts = new CancellationTokenSource())
			{
				var vm = new VirtualMachine(4, cts.Token);
				using (vm)
				{
					Assert.AreEqual(vm.State, VirtualMachineState.Running);
					Sleep();
				}

				Assert.AreEqual(vm.State, VirtualMachineState.End);
				Assert.IsNull(vm.Exception);
			}
		}

		[TestMethod]
		public void NoCancellationToken()
		{
			var vm = new VirtualMachine(4, CancellationToken.None);
			using (vm)
			{
				Assert.AreEqual(vm.State, VirtualMachineState.Running);
				Sleep();
			}

			Assert.AreEqual(vm.State, VirtualMachineState.End);
			Assert.IsNull(vm.Exception);
		}

		[TestMethod]
		public void ExplicitCancel()
		{
			using (var cts = new CancellationTokenSource())
			{
				var vm = new VirtualMachine(4, cts.Token);
				using (vm)
				{
					Assert.AreEqual(vm.State, VirtualMachineState.Running);

					Sleep();
					cts.Cancel();
				}

				Assert.AreEqual(vm.State, VirtualMachineState.End);
				Assert.IsNull(vm.Exception);
			}
		}

		[TestMethod]
		public void ExplicitCancelImmediately()
		{
			using (var cts = new CancellationTokenSource())
			{
				var vm = new VirtualMachine(4, cts.Token);
				using (vm)
				{
					Assert.AreEqual(vm.State, VirtualMachineState.Running);

					cts.Cancel();
					Sleep();
				}

				Assert.AreEqual(vm.State, VirtualMachineState.End);
				Assert.IsNull(vm.Exception);
			}
		}
	}
}
