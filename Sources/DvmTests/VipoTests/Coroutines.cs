using Dvm;
using DvmTests.SchedulerTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Linq;

namespace DvmTests.VipoTests
{
	[TestClass]
	public class Coroutines : TestSchedulerBase
	{
		class MyMessage : Message
		{
			public int Value { get; private set; }

			public MyMessage(int value)
			{
				Value = value;
			}

			public override string ToString()
			{
				return Value.ToString();
			}
		}

		#region Basic

		void SenderVoroutine(CoroutineVipo v, Vid receiver1, Vid receiver2)
		{
			Console.WriteLine($"'{v.Name}' START");

			v.SendMessage(new VipoMessage(v.Vid, receiver1, DefaultMessageBody));
			v.SendMessage(receiver1, new MyMessage(0));
			v.SendMessage(receiver1, new MyMessage(1));
			v.SendMessage(receiver1, new MyMessage(2));

			v.SendMessage(new VipoMessage(v.Vid, receiver2, DefaultMessageBody));
			v.SendMessage(receiver2, new MyMessage(0));
			v.SendMessage(receiver2, new MyMessage(1));
			v.SendMessage(receiver2, new MyMessage(2));

			Console.WriteLine($"'{v.Name}' END");
		}

		class ReceiverVipo : CoroutineVipo
		{
			public ReceiverVipo(Scheduler scheduler, string name)
				: base(scheduler, name, CallbackOptions.None)
			{
			}

			protected override IEnumerator Coroutine()
			{
				Console.WriteLine($"'{Name}' START");

				yield return Receive(
					message =>
					{
						Console.WriteLine($"'{Name}' receives message '{message.Body:full}'");
					});

				yield return Receive(
					message => message.Body is MyMessage,
					message =>
					{
						Console.WriteLine($"'{Name}' receives message '{message.Body:full}'");
					});

				yield return Receive<MyMessage>(
					(message, body) =>
					{
						Console.WriteLine($"'{Name}' receives message '{message.Body:full}'");
					});

				yield return Receive<MyMessage>(
					(message, body) => body.Value == 2,
					(message, body) =>
					{
						Console.WriteLine($"'{Name}' receives message '{message.Body:full}'");
					});

				Console.WriteLine($"'{Name}' END");
			}
		}

		IEnumerator ReceiverVoroutine(CoroutineVipo v)
		{
			Console.WriteLine($"'{v.Name}' START");

			yield return v.Receive(
				message =>
				{
					Console.WriteLine($"'{v.Name}' receives message '{message.Body:full}'");
				});

			yield return v.Receive(
				message => message.Body is MyMessage,
				message =>
				{
					Console.WriteLine($"'{v.Name}' receives message '{message.Body:full}'");
				});

			yield return v.Receive<MyMessage>(
				(message, body) =>
				{
					Console.WriteLine($"'{v.Name}' receives message '{message.Body:full}'");
				});

			yield return v.Receive<MyMessage>(
				(message, body) => body.Value == 2,
				(message, body) =>
				{
					Console.WriteLine($"'{v.Name}' receives message '{message.Body:full}'");
				});

			Console.WriteLine($"'{v.Name}' END");
		}

		[TestMethod]
		public void Basic()
		{
			HookConsoleOutput();

			var receiver1 = new ReceiverVipo(TheScheduler, "Receiver1");
			receiver1.Start();

			var receiver2 = TheScheduler.CreateVoroutine(ReceiverVoroutine, "Receiver2");
			receiver2.Start();

			var sender = TheScheduler.CreateVoroutineMinor(v => SenderVoroutine(v, receiver1.Vid, receiver2.Vid), "Sender");
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
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' START"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' receives message 'Message {MessageBase}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' receives message 'MyMessage {0}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' receives message 'MyMessage {1}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' receives message 'MyMessage {2}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' END"));

				Assert.IsTrue(consoleOutput.Contains("'Receiver2' START"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' receives message 'Message {MessageBase}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' receives message 'MyMessage {0}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' receives message 'MyMessage {1}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' receives message 'MyMessage {2}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' END"));

				Assert.IsTrue(consoleOutput.Contains("'Sender' START"));
				Assert.IsTrue(consoleOutput.Contains("'Sender' END"));
			}
		}

		#endregion

		#region React

		[TestMethod]
		public void React()
		{
			HookConsoleOutput();

			var reacter = TheScheduler.CreateVoroutine(ReactVoroutine, "Reacter");
			reacter.Start();

			var producer = TheScheduler.CreateVoroutineMinor(v => ProducerVoroutine(v, reacter.Vid), "Producer");
			producer.Start();

			Sleep();

			//reacter.Destroy();
			//producer.Destroy();

			Assert.AreEqual(reacter.Stage, VipoStage.Destroyed);
			Assert.AreEqual(producer.Stage, VipoStage.Destroyed);

			var consoleOutput = GetConsoleOutput();
			{
				Assert.IsTrue(consoleOutput.Contains("'Reacter' START"));
				Assert.IsTrue(consoleOutput.Contains("'Reacter' receives message 'Message {MessageBase}'"));
				Assert.IsTrue(consoleOutput.Contains("'Reacter' receives message 'MyMessage {0}'"));
				Assert.IsTrue(consoleOutput.Contains("'Reacter' receives message 'MyMessage {1}'"));
				Assert.IsTrue(consoleOutput.Contains("'Reacter' receives message 'MyMessage {3}'"));
				Assert.IsFalse(consoleOutput.Contains("'Reacter' receives message 'MyMessage {2}'"));
				Assert.IsTrue(consoleOutput.Contains("'Reacter' END"));

				Assert.IsTrue(consoleOutput.Contains("'Producer' START"));
				Assert.IsTrue(consoleOutput.Contains("'Producer' END"));
			}
		}

		IEnumerator ReactVoroutine(CoroutineVipo v)
		{
			Console.WriteLine($"'{v.Name}' START");

			yield return v.React(
				message =>
				{
					switch (message.Body)
					{
						case MyMessage mm:
							if (mm.Value == 2)
								return false;

							Console.WriteLine($"'{v.Name}' receives message '{message.Body:full}'");
							break;

						default:
							Console.WriteLine($"'{v.Name}' receives message '{message.Body:full}'");
							break;
					}

					return true;
				});

			yield return v.Receive<MyMessage>((message, body) =>
				Console.WriteLine($"'{v.Name}' receives message '{message.Body:full}'"));

			Console.WriteLine($"'{v.Name}' END");
		}

		void ProducerVoroutine(CoroutineVipo v, Vid reacter)
		{
			Console.WriteLine($"'{v.Name}' START");

			v.SendMessage(new VipoMessage(v.Vid, reacter, DefaultMessageBody));
			v.SendMessage(reacter, new MyMessage(0));
			v.SendMessage(reacter, new MyMessage(1));
			v.SendMessage(reacter, new MyMessage(2));
			v.SendMessage(reacter, new MyMessage(3));

			Console.WriteLine($"'{v.Name}' END");
		}

		#endregion

		#region WaitForAnyMessage

		[TestMethod]
		public void WaitForAnyMessage()
		{
			HookConsoleOutput();

			var waiter = TheScheduler.CreateVoroutine(WaiterVoroutine, "Waiter");
			waiter.Start();

			var producer = TheScheduler.CreateVoroutineMinor(v => ProducerVoroutine(v, waiter.Vid), "Producer");
			producer.Start();

			Sleep();

			//waiter.Destroy();
			//producer.Destroy();

			Assert.AreEqual(waiter.Stage, VipoStage.Destroyed);
			Assert.AreEqual(producer.Stage, VipoStage.Destroyed);

			var consoleOutput = GetConsoleOutput();
			{
				Assert.IsTrue(consoleOutput.Contains("'Waiter' START"));
				Assert.IsTrue(consoleOutput.Contains("'Waiter' receives message 'Message {MessageBase}'"));
				Assert.IsTrue(consoleOutput.Contains("'Waiter' receives message 'MyMessage {0}'"));
				Assert.IsTrue(consoleOutput.Contains("'Waiter' receives message 'MyMessage {2}'"));
				Assert.IsFalse(consoleOutput.Contains("'Waiter' receives message 'MyMessage {1}'"));
				Assert.IsFalse(consoleOutput.Contains("'Waiter' receives message 'MyMessage {3}'"));
				Assert.IsTrue(consoleOutput.Contains("'Waiter' END"));

				Assert.IsTrue(consoleOutput.Contains("'Producer' START"));
				Assert.IsTrue(consoleOutput.Contains("'Producer' END"));
			}
		}

		IEnumerator WaiterVoroutine(CoroutineVipo v)
		{
			Console.WriteLine($"'{v.Name}' START");

			yield return v.WaitForAnyMessage();

			yield return v.Receive(
				message =>
				{
					Console.WriteLine($"'{v.Name}' receives message '{message.Body:full}'");
				});

			yield return v.WaitForAnyMessage();

			foreach (var myMessage in from m in v.InMessages
									  let mm = m.Body as MyMessage
									  where mm != null && mm.Value % 2 == 0
									  select mm)
			{
				Console.WriteLine($"'{v.Name}' receives message '{myMessage:full}'");
			}

			Console.WriteLine($"'{v.Name}' END");
		}

		#endregion
	}
}
