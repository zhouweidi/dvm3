using Dvm;
using DvmTests.SchedulerTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace DvmTests.VipoTests
{
	[TestClass]
	public class LifeStages : TestSchedulerBase
	{
		[TestMethod]
		public void WholeLifeStages()
		{
			HookConsoleOutput();

			var v = new MyVipo(TheScheduler, "a");

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

			public MyVipo(Scheduler scheduler, string name)
				: base(scheduler, name)
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

			protected override void OnTick(TickTask tickTask)
			{
				var requests = new List<string>();
				{
					if (tickTask.StartRequest)
					{
						Assert.AreEqual(Stage, VipoStage.Running);
						requests.Add("Start");
					}

					if (tickTask.DestroyRequest)
					{
						Assert.AreEqual(Stage, VipoStage.DestroyRequested);
						requests.Add("Destroy");
					}

					if (!tickTask.AnyRequest)
					{
						Assert.AreEqual(Stage, VipoStage.Running);
						requests.Add("None");
					}
				}

				++m_tickedCount;

				Console.WriteLine($"MyVipo '{Name}' ticks #{m_tickedCount}, requests '{string.Join('|', requests)}', messages [{string.Join(',', tickTask.Messages)}]");
			}

			protected override void OnError(Exception e)
			{
				base.OnError(e);
			}
		}
	}
}
