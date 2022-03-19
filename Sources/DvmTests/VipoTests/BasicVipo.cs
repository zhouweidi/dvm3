using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace DvmTests.VipoTests
{
	[TestClass]
	public class BasicVipo : VmTestBase
	{
		[TestMethod]
		public void ExceptionDuringRun()
		{
			// Caught exception
			var a = new MyVipo(this, "a", false);

			a.Schedule();
			Sleep();

			var output = GetCustomOutput();
			Assert.IsTrue(output.Length == 0);

			Assert.AreEqual(VM.State, VirtualMachineState.Running);
			Assert.AreEqual(VM.ViposCount, 0);
			Assert.IsNull(VM.Exception);

			Assert.IsTrue(a.Disposed);
			Assert.IsFalse(a.IsAttached);

			// Uncaught exception
			var b = new MyVipo(this, "b", true);

			b.Schedule();
			Sleep();

			var output2 = GetCustomOutput();
			Assert.IsTrue(output2.Length == 1 && output2[0] == "VM.OnError: Error in Vipo.OnError()");

			Assert.AreEqual(VM.State, VirtualMachineState.Ending);
			Assert.AreEqual(VM.ViposCount, 1);
			Assert.IsNotNull(VM.Exception);

			// Everything is not disposed yet
			Assert.IsFalse(VM.Disposed);

			Assert.IsFalse(b.Disposed);
			Assert.IsTrue(b.IsAttached);
		}

		public override void Cleanup()
		{ }

		protected override void OnError(Exception e)
		{
			Print($"VM.OnError: {e.Message}");
		}

		class MyVipo : TestVipo
		{
			readonly bool m_throwInOnError;

			public MyVipo(VmTestBase test, string symbol, bool throwInOnError)
				: base(test, symbol)
			{
				m_throwInOnError = throwInOnError;
			}

			protected override void Run(IReadOnlyList<VipoMessage> vipoMessages)
			{
				throw new Exception("Original error in Vipo.Run()");
			}

			protected override void OnError(Exception e)
			{
				if (m_throwInOnError)
					throw new Exception("Error in Vipo.OnError()");

				base.OnError(e);
			}
		}
	}
}
