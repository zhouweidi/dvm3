using Dvm;

namespace DvmTests
{
	abstract class TestVipo : Vipo
	{
		readonly VmTestBase m_test;

		public TestVipo(VmTestBase test, string symbol)
			: base(test.VM, symbol)
		{
			m_test = test;
		}

		protected void Print(string content)
		{
			m_test.Print(content);
		}
	}
}
