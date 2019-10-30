using Dvm;
using DvmTests.SchedulerTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;

namespace DvmTests.VipoTests
{
	[TestClass]
	public class Coroutines : TestSchedulerBase
	{
		class MyMessage : Message
		{
			public int Value { get; private set; }

			public MyMessage(Vid from, Vid to, int value)
				: base(from, to)
			{
				Value = value;
			}
		}

		public IEnumerator SenderVoroutine(Voroutine v)
		{
			Console.WriteLine($"SenderVoroutine '{v.Name}' START");

			v.SendMessage(new Message(v.Vid, s_vidReceiver1));
			v.SendMessage(new MyMessage(v.Vid, s_vidReceiver1, 0));
			v.SendMessage(new MyMessage(v.Vid, s_vidReceiver1, 1));
			v.SendMessage(new MyMessage(v.Vid, s_vidReceiver1, 2));

			v.SendMessage(new Message(v.Vid, s_vidReceiver2));
			v.SendMessage(new MyMessage(v.Vid, s_vidReceiver2, 0));
			v.SendMessage(new MyMessage(v.Vid, s_vidReceiver2, 1));
			v.SendMessage(new MyMessage(v.Vid, s_vidReceiver2, 2));

			Console.WriteLine($"SenderVoroutine '{v.Name}' END");

			yield break;
		}

		class ReceiverVipo : CoroutineVipo
		{
			public ReceiverVipo(Scheduler scheduler, string name)
				: base(scheduler, name)
			{
			}

			protected override IEnumerator Coroutine()
			{
				Console.WriteLine($"ReceiverVipo '{Name}' START");

				yield return Receive(
					message =>
					{
						Console.WriteLine($"ReceiverVipo '{Name}' receives message '{message.GetType().Name}'");
					});

				yield return Receive(
					message => message is MyMessage,
					message =>
					{
						Console.WriteLine($"ReceiverVipo '{Name}' receives message '{message.GetType().Name}' 0");
					});

				yield return Receive<MyMessage>(
					message =>
					{
						Console.WriteLine($"ReceiverVipo '{Name}' receives message '{message.GetType().Name}' 1");
					});

				yield return Receive<MyMessage>(
					message => message.Value == 2,
					message =>
					{
						Console.WriteLine($"ReceiverVipo '{Name}' receives message '{message.GetType().Name}' 2");
					});

				Console.WriteLine($"ReceiverVipo '{Name}' END");
			}
		}

		public IEnumerator ReceiverVoroutine(Voroutine v)
		{
			Console.WriteLine($"ReceiverVoroutine '{v.Name}' START");

			yield return v.Receive(
				message =>
				{
					Console.WriteLine($"ReceiverVoroutine '{v.Name}' receives message '{message.GetType().Name}'");
				});

			yield return v.Receive(
				message => message is MyMessage,
				message =>
				{
					Console.WriteLine($"ReceiverVoroutine '{v.Name}' receives message '{message.GetType().Name}' 0");
				});

			yield return v.Receive<MyMessage>(
				message =>
				{
					Console.WriteLine($"ReceiverVoroutine '{v.Name}' receives message '{message.GetType().Name}' 1");
				});

			yield return v.Receive<MyMessage>(
				message => message.Value == 2,
				message =>
				{
					Console.WriteLine($"ReceiverVoroutine '{v.Name}' receives message '{message.GetType().Name}' 2");
				});

			Console.WriteLine($"ReceiverVoroutine '{v.Name}' END");
		}

		static Vid s_vidReceiver1;
		static Vid s_vidReceiver2;

		[TestMethod]
		public void VipoExceptions()
		{
			HookConsoleOutput();

			var receiver1 = new ReceiverVipo(TheScheduler, "Receiver1");
			s_vidReceiver1 = receiver1.Vid;
			receiver1.Start();

			var receiver2 = TheScheduler.CreateVoroutine(ReceiverVoroutine, "Receiver2");
			s_vidReceiver2 = receiver2.Vid;
			receiver2.Start();

			var sender = TheScheduler.CreateVoroutine(SenderVoroutine, "Sender");
			sender.Start();

			Sleep();

			//receiver1.Destroy();
			//receiver2.Destroy();
			//sender.Destroy();

			Assert.AreEqual(receiver1.Stage, VipoStage.Destroyed);
			Assert.AreEqual(receiver2.Stage, VipoStage.Destroyed);
			Assert.AreEqual(sender.Stage, VipoStage.Destroyed);

			var consoleOutput = GetConsoleOutput();
			{
				Assert.IsTrue(consoleOutput.Contains("ReceiverVipo 'Receiver1' START"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVipo 'Receiver1' receives message 'Message'"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVipo 'Receiver1' receives message 'MyMessage' 0"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVipo 'Receiver1' receives message 'MyMessage' 1"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVipo 'Receiver1' receives message 'MyMessage' 2"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVipo 'Receiver1' END"));

				Assert.IsTrue(consoleOutput.Contains("ReceiverVoroutine 'Receiver2' START"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVoroutine 'Receiver2' receives message 'Message'"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVoroutine 'Receiver2' receives message 'MyMessage' 0"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVoroutine 'Receiver2' receives message 'MyMessage' 1"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVoroutine 'Receiver2' receives message 'MyMessage' 2"));
				Assert.IsTrue(consoleOutput.Contains("ReceiverVoroutine 'Receiver2' END"));

				Assert.IsTrue(consoleOutput.Contains("SenderVoroutine 'Sender' START"));
				Assert.IsTrue(consoleOutput.Contains("SenderVoroutine 'Sender' END"));
			}
		}
	}
}
