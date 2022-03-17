﻿using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace DvmTests.VidTests
{
	[TestClass]
	public class BasicVid
	{
		[TestMethod]
		public void BitsSchema()
		{
			Assert.ThrowsException<ArgumentException>(() => new Vid(0, 1, null));
			Assert.ThrowsException<ArgumentException>(() => new Vid(1, 0, null));
			Assert.ThrowsException<ArgumentException>(() => new Vid(1, Vid.MaxIndex + 1, null));

			Assert.IsTrue(Vid.Empty.Data == 0);
			Assert.IsTrue(Vid.Empty.NodeId == 0);
			Assert.IsTrue(Vid.Empty.Index == 0);
			Assert.AreEqual(Vid.Empty.Symbol, "");
			Assert.IsTrue(Vid.Empty.IsEmpty);

			var vid = new Vid(1, 2, "abc");
			Assert.AreEqual(vid.Data, (((ulong)1 << (6 * 8)) | 2));
			Assert.AreEqual(vid.NodeId, 1);
			Assert.AreEqual(vid.Index, 2ul);
			Assert.AreEqual(vid.Symbol, "abc");
			Assert.IsFalse(vid.IsEmpty);
		}

		[TestMethod]
		public void Compare()
		{
			var vid1 = new Vid(1, 2, "abc");
			var vid2 = new Vid(1, 2, "abc def");
			var vid3 = new Vid(11, 22, "aabbcc");

			// Operators
			Assert.AreNotEqual(vid1, Vid.Empty);
			Assert.IsTrue(vid1 != Vid.Empty);
			Assert.IsFalse(vid1 == Vid.Empty);

			Assert.AreEqual(vid1, vid1);
			Assert.IsTrue(vid1 == vid2);
			Assert.IsTrue(vid1 != vid3);

			// IComparable<T>
			Assert.IsTrue(vid1.CompareTo(vid1) == 0);
			Assert.IsTrue(vid1.CompareTo(vid2) == 0);
			Assert.IsTrue(vid1.CompareTo(vid3) < 0);
			Assert.IsTrue(vid3.CompareTo(vid1) > 0);

			Assert.IsTrue(vid1.CompareTo(Vid.Empty) > 0);
			Assert.IsTrue(Vid.Empty.CompareTo(vid1) != 0);

			// Equals
			Assert.IsTrue(vid1.Equals(vid1));
			Assert.IsTrue(vid1.Equals(vid2));
			Assert.IsFalse(vid1.Equals(vid3));
			Assert.IsFalse(vid1.Equals(null));
			Assert.IsFalse(vid1.Equals("abc"));
		}

		[TestMethod]
		public void FormatString()
		{
			var vid1 = new Vid(1, 2, null);
			var vid2 = new Vid(1, 2, "abc");

			Assert.AreEqual(vid1.ToString(null), $"{vid1.Data:X}");
			Assert.AreEqual(vid1.ToString(""), $"{vid1.Data:X}");
			Assert.AreEqual(vid1.ToString(""), vid1.ToString());

			Assert.AreEqual(vid2.ToString(null), $"{vid2.Data:X}^abc");
			Assert.AreEqual(vid2.ToString(""), $"{vid2.Data:X}^abc");
			Assert.AreEqual(vid2.ToString(""), vid2.ToString());

			Assert.AreEqual(vid1.ToString("detail"), "1.2");
			Assert.AreEqual(vid2.ToString("detail"), "1.2^abc");

			Assert.AreEqual(Vid.Empty.ToString(), "0.0");

			Assert.ThrowsException<FormatException>(() => vid1.ToString("???"));
		}
	}
}
