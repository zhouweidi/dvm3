using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Threading;

namespace DvmTests
{
	[TestClass]
	public abstract class VmTestBase : TestBase
	{
		VirtualMachine m_vm;
		CancellationTokenSource m_cts;
		readonly ManualResetEvent m_exceptionOccured = new ManualResetEvent(false);
		Exception m_error;

		internal VirtualMachine VM => m_vm;
		protected virtual int VmProcessorsCount => 4;

		[TestInitialize]
		public override void Initialize()
		{
			base.Initialize();

			m_cts = new CancellationTokenSource();
			m_vm = new VirtualMachine(VmProcessorsCount, m_cts.Token, false);

			m_vm.OnError += OnError;

			Assert.AreEqual(m_vm.State, VirtualMachineState.Running);
		}

		[TestCleanup]
		public override void Cleanup()
		{
			Assert.IsNull(m_vm.Exception);

			if (!m_vm.Disposed)
			{
				Assert.AreEqual(m_vm.State, VirtualMachineState.Running);

				m_cts.Cancel();

				DisposableObject.SafeDispose(ref m_vm);
			}

			DisposableObject.SafeDispose(ref m_cts);

			base.Cleanup();
		}

		protected virtual void OnError(Exception e)
		{
			Debug.Assert(m_error == null);

			m_error = e;
			m_exceptionOccured.Set();
		}

		protected override void Sleep(double seconds)
		{
			var ms = (int)(seconds * 1000);
			var errorOccured = m_exceptionOccured.WaitOne(ms);

			if (errorOccured)
			{
				Assert.IsNotNull(m_error);

				Assert.Fail(m_error.ToString());
			}
		}
	}
}
