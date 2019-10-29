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
				new MyVipo(TheScheduler, ExceptionPosition.Constructor);
			});

			var vipos = new[]
				{
					new MyVipo(TheScheduler, ExceptionPosition.OnStart),
					new MyVipo(TheScheduler, ExceptionPosition.OnTick),
					new MyVipo(TheScheduler, ExceptionPosition.OnDestroy),
					new MyVipo(TheScheduler, ExceptionPosition.OnError),
				};

			var safe = new MyVipo(TheScheduler, ExceptionPosition.None);

			foreach (var v in vipos.Append(safe))
				v.Start();

			Sleep();

			foreach (var v in vipos.Append(safe))
				v.Destroy();

			Sleep();

			foreach (var v in vipos)
				Assert.IsNotNull(v.Exception);

			Assert.IsNull(safe.Exception);

			Assert.IsNull(TheScheduler.Exception);
		}

		class MyVipo : Vipo
		{
			ExceptionPosition m_throwExceptionAt;

			public MyVipo(Scheduler scheduler, ExceptionPosition at)
				: base(scheduler, at.ToString())
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

			protected override void OnTick(TickTask tickTask)
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
