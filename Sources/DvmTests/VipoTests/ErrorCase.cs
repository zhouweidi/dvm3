using Dvm;
using DvmTests.SchedulerTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace DvmTests.VipoTests
{
	[TestClass]
	public class ErrorCase : TestSchedulerBase
	{
		enum ExceptionPosition
		{
			None, Constructor, OnStart, OnTick, OnDestroy, OnError
		}

		class TestException : Exception
		{
			public TestException(string message)
				: base(message)
			{
			}
		}

		[TestMethod]
		public void VipoExceptions()
		{
			Assert.ThrowsException<TestException>(() =>
			{
				new MyVipo(TheVM, ExceptionPosition.Constructor);
			});

			var vipos = new[]
				{
					new MyVipo(TheVM, ExceptionPosition.OnStart),
					new MyVipo(TheVM, ExceptionPosition.OnTick),
					new MyVipo(TheVM, ExceptionPosition.OnDestroy),
					new MyVipo(TheVM, ExceptionPosition.OnError),
				};

			var safe = new MyVipo(TheVM, ExceptionPosition.None);

			foreach (var v in vipos.Append(safe))
				v.Start();

			Sleep();

			foreach (var v in vipos.Append(safe))
				v.Destroy();

			Sleep();

			foreach (var v in vipos)
				Assert.IsNotNull(v.Exception);

			Assert.IsNull(safe.Exception);

			Assert.IsNull(TheVM.Exception);
		}

		class MyVipo : Vipo
		{
			ExceptionPosition m_throwExceptionAt;

			public MyVipo(VirtualMachine vm, ExceptionPosition at)
				: base(vm, at.ToString(), CallbackOptions.All)
			{
				m_throwExceptionAt = at;

				if (m_throwExceptionAt == ExceptionPosition.Constructor)
					throw new TestException("Constructor");
			}

			protected override void OnStart()
			{
				base.OnStart();

				if (m_throwExceptionAt == ExceptionPosition.OnStart)
					throw new TestException("OnStart");
			}

			protected override void OnDestroy()
			{
				if (m_throwExceptionAt == ExceptionPosition.OnDestroy)
					throw new TestException("OnDestroy");

				base.OnDestroy();
			}

			protected override void OnTick(VipoJob job)
			{
				if (m_throwExceptionAt == ExceptionPosition.OnTick)
					throw new TestException("OnTick");

				if (m_throwExceptionAt == ExceptionPosition.OnError)
					throw new TestException("OnError - Pre");
			}

			protected override void OnError(Exception e)
			{
				if (m_throwExceptionAt == ExceptionPosition.OnError)
					throw new TestException("OnError");

				base.OnError(e);
			}
		}
	}
}
