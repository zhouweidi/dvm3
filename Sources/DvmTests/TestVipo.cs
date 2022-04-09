using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DvmTests
{
	abstract class TestVipo : Vipo
	{
		readonly VmTestBase m_test;

		protected TestVipo(VmTestBase test, string symbol)
			: base(test.VM, symbol)
		{
			m_test = test;
		}

		protected override void OnError(Exception e)
		{
			Assert.Fail(e.ToString());
		}

		protected void Print(string content = "")
		{
			m_test.Print(content);
		}
	}

	abstract class TestAsyncVipo : AsyncVipo
	{
		readonly VmTestBase m_test;

		#region Test supports

		public TestAsyncVipo(VmTestBase test, string symbol)
			: base(test.VM, symbol)
		{
			m_test = test;
		}

		protected override void OnError(Exception e)
		{
			Assert.Fail(e.ToString());
		}

		protected void Print(string content = "")
		{
			m_test.Print(content);
		}

		#endregion
	}
}
