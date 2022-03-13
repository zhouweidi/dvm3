using Dvm;
using DvmTests.SchedulerTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DvmTests.VipoTests
{
	[TestClass]
	public class LifeStages : TestSchedulerBase
	{
		#region WholeLifeStages

		[TestMethod]
		public void WholeLifeStages()
		{
			HookConsoleOutput();

			var v = new MyVipo(TheVM, "a");

			Assert.AreEqual(v.Stage, VipoStage.NotStarted);
			v.Start();
			Assert.AreEqual(v.Stage, VipoStage.StartRequested);

			Sleep();
			Assert.AreEqual(v.Stage, VipoStage.Running);

			v.Schedule();

			Sleep();

			Assert.AreEqual(v.Stage, VipoStage.Running);
			v.Destroy();
			Assert.AreEqual(v.Stage, VipoStage.DestroyRequested);

			Sleep();
			Assert.AreEqual(v.Stage, VipoStage.Destroyed);

			var consoleOutput = GetConsoleOutput();
			Assert.IsTrue(consoleOutput.Contains("MyVipo 'a' ticks #1, requests 'Start', messages []"));
			Assert.IsTrue(consoleOutput.Contains("MyVipo 'a' ticks #2, requests 'None', messages [VipoSchedule]"));
			Assert.IsTrue(consoleOutput.Contains("MyVipo 'a' ticks #3, requests 'Destroy', messages []"));
		}

		class MyVipo : Vipo
		{
			int m_tickedCount;

			public MyVipo(VirtualMachine vm, string name)
				: base(vm, name, CallbackOptions.All)
			{
				Assert.AreEqual(Stage, VipoStage.NotStarted);
			}

			protected override void OnStart()
			{
				base.OnStart();

				Assert.AreEqual(Stage, VipoStage.StartRequested);
			}

			protected override void OnDestroy()
			{
				Assert.AreEqual(Stage, VipoStage.DestroyRequested);

				base.OnDestroy();
			}

			protected override void OnTick(VipoJob job)
			{
				var requests = new List<string>();
				{
					if (job.StartRequest)
					{
						Assert.AreEqual(Stage, VipoStage.Running);
						requests.Add("Start");
					}

					if (job.DestroyRequest)
					{
						Assert.AreEqual(Stage, VipoStage.DestroyRequested);
						requests.Add("Destroy");
					}

					if (!job.AnyRequest)
					{
						Assert.AreEqual(Stage, VipoStage.Running);
						requests.Add("None");
					}
				}

				++m_tickedCount;

				Console.WriteLine($"MyVipo '{Name}' ticks #{m_tickedCount}, requests '{string.Join('|', requests)}', messages [{JoinMessageBodies(job.Messages)}]");
			}

			protected override void OnError(Exception e)
			{
				base.OnError(e);
			}
		}

		#endregion

		#region WithoutCallback

		[TestMethod]
		public void WithoutCallback()
		{
			HookConsoleOutput();

			var v = new MyVipoWithoutCallback(TheVM, "a");
			v.Start();

			Sleep();

			v.Schedule();

			Sleep();

			v.Destroy();

			var consoleOutput = GetConsoleOutput();
			Assert.IsTrue(consoleOutput.Contains("MyVipoWithoutCallback 'a' ticks #1"));
			Assert.IsTrue(consoleOutput.Contains("MyVipoWithoutCallback 'a' ticks #2"));
		}

		class MyVipoWithoutCallback : Vipo
		{
			int m_tickedCount;

			public MyVipoWithoutCallback(VirtualMachine vm, string name)
				: base(vm, name, CallbackOptions.None)
			{
			}

			protected override void OnStart()
			{
				Assert.Fail("Unexcepted being called");
			}

			protected override void OnDestroy()
			{
				Assert.Fail("Unexcepted being called");
			}

			protected override void OnTick(VipoJob job)
			{
				++m_tickedCount;

				Console.WriteLine($"MyVipoWithoutCallback '{Name}' ticks #{m_tickedCount}");
			}

			protected override void OnError(Exception e)
			{
				Assert.Fail("Unexcepted being called");
			}
		}

		#endregion
	}
}
