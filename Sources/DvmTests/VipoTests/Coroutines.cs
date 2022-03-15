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

		void SenderVoroutine(CoroutineVipo v, Vid receiver1, Vid receiver2, Vid receiver3)
		{
			Console.WriteLine($"'{v.Symbol}' START");

			v.Send(new VipoMessage(v.Vid, receiver1, DefaultMessageBody));
			v.Send(receiver1, new MyMessage(0));
			v.Send(receiver1, new MyMessage(1));
			v.Send(receiver1, new MyMessage(2));

			v.Send(new VipoMessage(v.Vid, receiver2, DefaultMessageBody));
			v.Send(receiver2, new MyMessage(0));
			v.Send(receiver2, new MyMessage(1));
			v.Send(receiver2, new MyMessage(2));

			v.Send(receiver3, DefaultMessageBody);
			v.Send(new VipoMessage(v.Vid, receiver3, DefaultMessageBody));
			v.Send(receiver3, new MyMessage(0));
			v.Send(receiver3, new MyMessage(1));
			v.Send(receiver3, new MyMessage(2));

			Console.WriteLine($"'{v.Symbol}' END");
		}

		class ReceiverVipo : CoroutineVipo
		{
			public ReceiverVipo(VirtualMachine vm, string name)
				: base(vm, name, CallbackOptions.None)
			{
			}

			protected override IEnumerator Coroutine()
			{
				Console.WriteLine($"'{Symbol}' START");

				yield return Receive(
					message =>
					{
						Console.WriteLine($"'{Symbol}' receives message '{message.Body:full}'");
					});

				yield return Receive(
					message => message.Body is MyMessage,
					message =>
					{
						Console.WriteLine($"'{Symbol}' receives message '{message.Body:full}'");
					});

				yield return Receive<MyMessage>(
					(message, body) =>
					{
						Console.WriteLine($"'{Symbol}' receives message '{message.Body:full}'");
					});

				yield return Receive<MyMessage>(
					(message, body) => body.Value == 2,
					(message, body) =>
					{
						Console.WriteLine($"'{Symbol}' receives message '{message.Body:full}'");
					});

				Console.WriteLine($"'{Symbol}' END");
			}
		}

		IEnumerator ReceiverVoroutine(CoroutineVipo v)
		{
			Console.WriteLine($"'{v.Symbol}' START");

			yield return v.Receive(
				message =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			yield return v.Receive(
				message => message.Body is MyMessage,
				message =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			yield return v.Receive<MyMessage>(
				(message, body) =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			yield return v.Receive<MyMessage>(
				(message, body) => body.Value == 2,
				(message, body) =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			Console.WriteLine($"'{v.Symbol}' END");
		}

		IEnumerator ReceiverFromVoroutine(CoroutineVipo v)
		{
			Console.WriteLine($"'{v.Symbol}' START");

			Vid from = Vid.Empty;

			yield return v.Receive(
				message => from = message.From);

			yield return v.ReceiveFrom(
				from,
				message =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			yield return v.ReceiveFrom(
				from,
				message => message.Body is MyMessage,
				message =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			yield return v.ReceiveFrom<MyMessage>(
				from,
				(message, body) =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			yield return v.ReceiveFrom<MyMessage>(
				from,
				(message, body) => body.Value == 2,
				(message, body) =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			Console.WriteLine($"'{v.Symbol}' END");
		}

		[TestMethod]
		public void Basic()
		{
			HookConsoleOutput();

			var receiver1 = new ReceiverVipo(TheVM, "Receiver1");
			var receiver2 = TheVM.CreateVoroutine(ReceiverVoroutine, "Receiver2");
			var receiver3 = TheVM.CreateVoroutine(ReceiverFromVoroutine, "Receiver3");
			var sender = TheVM.CreateVoroutineMinor(v => SenderVoroutine(v, receiver1.Vid, receiver2.Vid, receiver3.Vid), "Sender");

			receiver1.Start();
			receiver2.Start();
			receiver3.Start();
			sender.Start();

			Sleep();

			//receiver1.Destroy();
			//receiver2.Destroy();
			//receiver3.Destroy();
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

				Assert.IsTrue(consoleOutput.Contains("'Receiver3' START"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver3' receives message 'Message {MessageBase}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver3' receives message 'MyMessage {0}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver3' receives message 'MyMessage {1}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver3' receives message 'MyMessage {2}'"));
				Assert.IsTrue(consoleOutput.Contains("'Receiver3' END"));

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

			var reacter = TheVM.CreateVoroutine(ReactVoroutine, "Reacter");
			reacter.Start();

			var producer = TheVM.CreateVoroutineMinor(v => ProducerVoroutine(v, reacter.Vid), "Producer");
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
			Console.WriteLine($"'{v.Symbol}' START");

			yield return v.React(
				message =>
				{
					switch (message.Body)
					{
						case MyMessage mm:
							if (mm.Value == 2)
								return false;

							Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
							break;

						default:
							Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
							break;
					}

					return true;
				});

			yield return v.Receive<MyMessage>((message, body) =>
				Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'"));

			Console.WriteLine($"'{v.Symbol}' END");
		}

		void ProducerVoroutine(CoroutineVipo v, Vid reacter)
		{
			Console.WriteLine($"'{v.Symbol}' START");

			v.Send(new VipoMessage(v.Vid, reacter, DefaultMessageBody));
			v.Send(reacter, new MyMessage(0));
			v.Send(reacter, new MyMessage(1));
			v.Send(reacter, new MyMessage(2));
			v.Send(reacter, new MyMessage(3));

			Console.WriteLine($"'{v.Symbol}' END");
		}

		#endregion

		#region WaitForAnyMessage

		[TestMethod]
		public void WaitForAnyMessage()
		{
			HookConsoleOutput();

			var waiter = TheVM.CreateVoroutine(WaiterVoroutine, "Waiter");
			waiter.Start();

			var producer = TheVM.CreateVoroutineMinor(v => ProducerVoroutine(v, waiter.Vid), "Producer");
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
			Console.WriteLine($"'{v.Symbol}' START");

			yield return v.WaitForAnyMessage();

			yield return v.Receive(
				message =>
				{
					Console.WriteLine($"'{v.Symbol}' receives message '{message.Body:full}'");
				});

			yield return v.WaitForAnyMessage();

			foreach (var myMessage in from m in v.InMessages
									  let mm = m.Body as MyMessage
									  where mm != null && mm.Value % 2 == 0
									  select mm)
			{
				Console.WriteLine($"'{v.Symbol}' receives message '{myMessage:full}'");
			}

			Console.WriteLine($"'{v.Symbol}' END");
		}

		#endregion

		#region Call

		[TestMethod]
		public void Call()
		{
			HookConsoleOutput();

			var callee = TheVM.CreateVoroutine(CalleeVoroutine, "Callee");
			callee.Start();

			var caller = TheVM.CreateVoroutine(v => CallerVoroutine(v, callee.Vid), "Caller");
			caller.Start();

			Sleep();

			//callee.Destroy();
			//caller.Destroy();

			Assert.AreEqual(callee.Stage, VipoStage.Destroyed);
			Assert.AreEqual(caller.Stage, VipoStage.Destroyed);

			var consoleOutput = GetConsoleOutput();
			{
				Assert.IsTrue(consoleOutput.Contains("'Caller' START"));
				Assert.IsTrue(consoleOutput.Contains("'Caller' receives return message 'ResponseMessage {0}'"));
				Assert.IsTrue(consoleOutput.Contains("'Caller' receives return message 'ResponseMessage {1}'"));
				Assert.IsTrue(consoleOutput.Contains("'Caller' receives return message 'ResponseMessage {2}'"));
				Assert.IsTrue(consoleOutput.Contains("'Caller' receives return message 'ResponseMessage {444}'"));
				Assert.IsTrue(consoleOutput.Contains("'Caller' END"));

				Assert.IsTrue(consoleOutput.Contains("'Callee' START"));
				Assert.IsTrue(consoleOutput.Contains("'Callee' being called 'RequestMessage {11}'"));
				Assert.IsTrue(consoleOutput.Contains("'Callee' being called 'RequestMessage {22}'"));
				Assert.IsTrue(consoleOutput.Contains("'Callee' being called 'RequestMessage {33}'"));
				Assert.IsTrue(consoleOutput.Contains("'Callee' being called 'RequestMessage {44}'"));
				Assert.IsTrue(consoleOutput.Contains("'Callee' END"));
			}
		}

		class RequestMessage : Message
		{
			public int Value { get; private set; }

			public RequestMessage(int value)
			{
				Value = value;
			}

			public override string ToString()
			{
				return Value.ToString();
			}
		}

		class ResponseMessage : Message
		{
			public int Value { get; private set; }

			public ResponseMessage(int value)
			{
				Value = value;
			}

			public override string ToString()
			{
				return Value.ToString();
			}
		}

		IEnumerator CalleeVoroutine(CoroutineVipo v)
		{
			Console.WriteLine($"'{v.Symbol}' START");

			int count = 0;

			yield return v.React(
				message =>
				{
					Console.WriteLine($"'{v.Symbol}' being called '{message.Body:full}'");

					var mm = message.Body as RequestMessage;
					Assert.IsNotNull(mm);

					v.Send(message.From, new ResponseMessage(mm.Value == 44 ? 444 : count));

					count++;

					return count != 4;
				});

			Console.WriteLine($"'{v.Symbol}' END");
		}

		IEnumerator CallerVoroutine(CoroutineVipo v, Vid callee)
		{
			Console.WriteLine($"'{v.Symbol}' START");

			yield return v.Call(callee, new RequestMessage(11),
				message =>
				{
					Console.WriteLine($"'{v.Symbol}' receives return message '{message.Body:full}'");
				});

			yield return v.Call(callee, new RequestMessage(22),
				message => message.Body is ResponseMessage,
				message =>
				{
					Console.WriteLine($"'{v.Symbol}' receives return message '{message.Body:full}'");
				});

			yield return v.Call<ResponseMessage>(callee, new RequestMessage(33),
				(message, body) =>
				{
					Console.WriteLine($"'{v.Symbol}' receives return message '{message.Body:full}'");
				});

			yield return v.Call<ResponseMessage>(callee, new RequestMessage(44),
				(message, body) => body.Value == 444,
				(message, body) =>
				{
					Console.WriteLine($"'{v.Symbol}' receives return message '{message.Body:full}'");
				});

			Console.WriteLine($"'{v.Symbol}' END");
		}

		#endregion
	}
}
