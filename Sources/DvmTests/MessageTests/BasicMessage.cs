using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DvmTests.MessageTests
{
	[TestClass]
	public class BasicMessage
	{
		class EmptyMessage : Message
		{
		}

		class MyMessage : Message
		{
			public int Value { get; private set; }

			public MyMessage(int value)
			{
				Value = value;
			}

			protected override string BodyToString() => Value.ToString();
		}

		[TestMethod]
		public void FormatString()
		{
			var emptyMessage = new EmptyMessage();
			Assert.AreEqual(emptyMessage.ToString(), "EmptyMessage {}");

			var myMessage = new MyMessage(888);
			Assert.AreEqual(myMessage.ToString(), "MyMessage {888}");

			var from = new Vid(1, 2, "aa");
			var to = new Vid(3, 4, "bb");
			var vm1 = new VipoMessage(from, to, emptyMessage);
			var vm2 = new VipoMessage(from, to, myMessage);

			Assert.AreEqual(vm1.ToString(), "(1000000000002^aa, 3000000000004^bb, EmptyMessage {})");
			Assert.AreEqual(vm2.ToString(), "(1000000000002^aa, 3000000000004^bb, MyMessage {888})");
		}
	}
}
