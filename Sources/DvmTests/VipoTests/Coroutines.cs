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

			public MyMessage(Vid from, Vid to, int value)
				: base(from, to)
			{
				Value = value;
			}
		}

		#region Basic

		void SenderVoroutine(CoroutineVipo v, Vid receiver1, Vid receiver2)
		{
			Console.WriteLine($"'{v.Name}' START");

			v.SendMessage(new Message(v.Vid, receiver1));
			v.SendMessage(new MyMessage(v.Vid, receiver1, 0));
			v.SendMessage(new MyMessage(v.Vid, receiver1, 1));
			v.SendMessage(new MyMessage(v.Vid, receiver1, 2));

			v.SendMessage(new Message(v.Vid, receiver2));
			v.SendMessage(new MyMessage(v.Vid, receiver2, 0));
			v.SendMessage(new MyMessage(v.Vid, receiver2, 1));
			v.SendMessage(new MyMessage(v.Vid, receiver2, 2));

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
						Console.WriteLine($"'{Name}' receives message '{message.GetType().Name}'");
					});

				yield return Receive(
					message => message is MyMessage,
					message =>
					{
						Console.WriteLine($"'{Name}' receives message '{message.GetType().Name}' 0");
					});

				yield return Receive<MyMessage>(
					message =>
					{
						Console.WriteLine($"'{Name}' receives message '{message.GetType().Name}' 1");
					});

				yield return Receive<MyMessage>(
					message => message.Value == 2,
					message =>
					{
						Console.WriteLine($"'{Name}' receives message '{message.GetType().Name}' 2");
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
					Console.WriteLine($"'{v.Name}' receives message '{message.GetType().Name}'");
				});

			yield return v.Receive(
				message => message is MyMessage,
				message =>
				{
					Console.WriteLine($"'{v.Name}' receives message '{message.GetType().Name}' 0");
				});

			yield return v.Receive<MyMessage>(
				message =>
				{
					Console.WriteLine($"'{v.Name}' receives message '{message.GetType().Name}' 1");
				});

			yield return v.Receive<MyMessage>(
				message => message.Value == 2,
				message =>
				{
					Console.WriteLine($"'{v.Name}' receives message '{message.GetType().Name}' 2");
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
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' receives message 'Message'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' receives message 'MyMessage' 0"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' receives message 'MyMessage' 1"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' receives message 'MyMessage' 2"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver1' END"));

				Assert.IsTrue(consoleOutput.Contains("'Receiver2' START"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' receives message 'Message'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' receives message 'MyMessage' 0"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' receives message 'MyMessage' 1"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver2' receives message 'MyMessage' 2"));
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
				Assert.IsTrue(consoleOutput.Contains("'Reacter' receives message 'Message'"));
				Assert.IsTrue(consoleOutput.Contains("'Reacter' receives message 'MyMessage' 0"));
				Assert.IsTrue(consoleOutput.Contains("'Reacter' receives message 'MyMessage' 1"));
				Assert.IsTrue(consoleOutput.Contains("'Reacter' receives message 'MyMessage' 3"));
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
					switch (message)
					{
						case MyMessage mm:
							if (mm.Value == 2)
								return false;

							Console.WriteLine($"'{v.Name}' receives message '{message.GetType().Name}' {mm.Value}");
							break;

						default:
							Console.WriteLine($"'{v.Name}' receives message '{message.GetType().Name}'");
							break;
					}

					return true;
				});

			yield return v.Receive<MyMessage>(message =>
				Console.WriteLine($"'{v.Name}' receives message '{message.GetType().Name}' {message.Value}"));

			Console.WriteLine($"'{v.Name}' END");
		}

		void ProducerVoroutine(CoroutineVipo v, Vid reacter)
		{
			Console.WriteLine($"'{v.Name}' START");

			v.SendMessage(new Message(v.Vid, reacter));
			v.SendMessage(new MyMessage(v.Vid, reacter, 0));
			v.SendMessage(new MyMessage(v.Vid, reacter, 1));
			v.SendMessage(new MyMessage(v.Vid, reacter, 2));
			v.SendMessage(new MyMessage(v.Vid, reacter, 3));

			Console.WriteLine($"'{v.Name}' END");
		}

		#endregion
	}
}
