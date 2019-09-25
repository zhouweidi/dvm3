using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DvmTests.OtherTests
{
	[TestClass]
	public class BasicVid
	{
		[TestMethod]
		public void BitsSchema()
		{
			Assert.IsTrue(Vid.Empty.Data == 0);
			Assert.IsTrue(Vid.Empty.Cid == 0);
			Assert.IsTrue(Vid.Empty.Index == 0);
			Assert.IsNull(Vid.Empty.Description);
			Assert.IsTrue(Vid.Empty.IsEmpty);

			var vid = new Vid(1, 2, "abc");
			Assert.IsTrue(vid.Data == (((ulong)1 << (6 * 8)) | 2));
			Assert.IsTrue(vid.Cid == 1);
			Assert.IsTrue(vid.Index == 2);
			Assert.AreEqual(vid.Description, "abc");
			Assert.IsFalse(vid.IsEmpty);
		}

		[TestMethod]
		public void CompareAndOperators()
		{
			var vid1 = new Vid(1, 2, "abc");
			var vid2 = new Vid(1, 2, "abc def");
			var vid3 = new Vid(11, 22, "aabbcc");

			// Operators
			Assert.IsTrue(Vid.Empty != vid1);
			Assert.IsFalse(Vid.Empty == vid1);

			Assert.AreEqual(vid1, vid1);
			Assert.IsTrue(vid1 == vid2);
			Assert.IsTrue(vid1 != vid3);

			// IComparable<T>
			Assert.IsTrue(vid1.CompareTo(vid1) == 0);
			Assert.IsTrue(vid1.CompareTo(Vid.Empty) != 0);
			Assert.IsTrue(Vid.Empty.CompareTo(vid1) != 0);
			Assert.IsTrue(vid1.CompareTo(Vid.Empty) > 0 || Vid.Empty.CompareTo(vid1) > 0);

			Assert.IsTrue(vid1.CompareTo(vid1) == 0);
			Assert.IsTrue(vid1.CompareTo(vid2) == 0);
			Assert.IsTrue(vid1.CompareTo(vid3) < 0);
			Assert.IsTrue(vid3.CompareTo(vid1) > 0);

			// Equals
			Assert.IsTrue(vid1.Equals((object)vid1));
			Assert.IsTrue(vid1.Equals((object)vid2));
			Assert.IsFalse(vid1.Equals((object)vid3));
		}

		[TestMethod]
		public void FormatString()
		{
			var vid1 = new Vid(1, 2, null);
			var vid2 = new Vid(1, 2, "abc");

			Assert.AreEqual(vid1.ToString(Vid.ShortFormat), $"{vid1.Data:X}");
			Assert.AreEqual(vid2.ToString(Vid.ShortFormat), $"{vid2.Data:X}^abc");

			Assert.AreEqual(vid1.ToString(Vid.FullFormat), $"{vid1.Index:X}-{vid1.Cid:X}");
			Assert.AreEqual(vid2.ToString(Vid.FullFormat), $"{vid2.Index:X}-{vid2.Cid:X}^abc");
		}
	}
}
