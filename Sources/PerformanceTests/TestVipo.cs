using Dvm;
using System;

namespace PerformanceTests
{
	abstract class TestVipo : Vipo
	{
		readonly Test m_test;

		protected TestVipo(Test test, string symbol)
			: base(test.VM, symbol)
		{
			m_test = test;
		}

		protected override void OnError(Exception e)
		{
			Assert(e);

			throw new Exception("Ant error", e);
		}

		protected void Print(string content = "")
		{
			m_test.Print(content);
		}

		protected static void Assert(bool condition)
		{
			Test.Assert(condition);
		}

		protected static void Assert(Exception ex)
		{
			Test.Assert(ex);
		}

		protected static void Assert(string message)
		{
			Test.Assert(message);
		}
	}
}
