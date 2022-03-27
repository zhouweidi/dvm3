using Dvm;
using System;
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
		public void Construction()
		{
			Assert.ThrowsException<ArgumentNullException>(() => new VipoMessage(Vid.Empty, Vid.Empty, null));
		}

		[TestMethod]
		public void FormatString()
		{
			var emptyMessage = new EmptyMessage();
			Assert.AreEqual(emptyMessage.ToString(), "EmptyMessage");
			Assert.AreEqual(emptyMessage.ToString("detail"), "EmptyMessage {}");

			var myMessage = new MyMessage(888);
			Assert.AreEqual(myMessage.ToString(), "MyMessage");
			Assert.AreEqual(myMessage.ToString("detail"), "MyMessage {888}");

			Assert.ThrowsException<FormatException>(() => myMessage.ToString("???"));

			var from = new Vid(1, 2, null);
			var to = new Vid(3, 4, null);

			var vm1 = new VipoMessage(from, to, emptyMessage);
			Assert.AreEqual(vm1.ToString(), "(1000000000002, 3000000000004, EmptyMessage)");
			Assert.AreEqual(vm1.ToString("detail"), "(1.2, 3.4, EmptyMessage {})");

			var vm2 = new VipoMessage(from, to, myMessage);
			Assert.AreEqual(vm2.ToString(), "(1000000000002, 3000000000004, MyMessage)");
			Assert.AreEqual(vm2.ToString("detail"), "(1.2, 3.4, MyMessage {888})");

			Assert.ThrowsException<FormatException>(() => vm1.ToString("???"));
		}
	}
}
