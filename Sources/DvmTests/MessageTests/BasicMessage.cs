using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DvmTests.MessageTests
{
	[TestClass]
	public class BasicMessage
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

		[TestMethod]
		public void FormatString()
		{
			var message = new Message();

			Assert.AreEqual(message.ToString(), "MessageBase");
			Assert.AreEqual(message.ToString(""), "MessageBase");
			Assert.AreEqual(message.ToString(null), "MessageBase");
			Assert.AreEqual(message.ToString(Message.FullFormat), "Message {MessageBase}");

			var myMessage = new MyMessage(888);

			Assert.AreEqual(myMessage.ToString(), "888");
			Assert.AreEqual(myMessage.ToString(""), "888");
			Assert.AreEqual(myMessage.ToString(null), "888");
			Assert.AreEqual(myMessage.ToString(Message.FullFormat), "MyMessage {888}");

			var from = new Vid(1, 2, "aa");
			var to = new Vid(3, 4, "bb");
			var vm1 = new VipoMessage(from, to, message);
			var vm2 = new VipoMessage(from, to, myMessage);

			Assert.AreEqual(vm1.ToString(), "1000000000002^aa, 3000000000004^bb, Message {MessageBase}");
			Assert.AreEqual(vm2.ToString(), "1000000000002^aa, 3000000000004^bb, MyMessage {888}");
		}
	}
}
