using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DvmTests.OtherTests
{
	[TestClass]
	public class VidAllocation : TestBase
	{
		[TestMethod]
		public void Allocate()
		{
			TestNextIndex(0, 1);
			TestNextIndex(1, 2);
			TestNextIndex(-1, 1);
			TestNextIndex((long)Vid.MaxIndex - 1, Vid.MaxIndex);
			TestNextIndex((long)Vid.MaxIndex, 1);
			TestNextIndex((long)(Vid.MaxIndex + 1), 1);
		}

		static void TestNextIndex(long vidIndexCursor, ulong expectedIndexValue)
		{
			var index = Vid.GetNextIndex(ref vidIndexCursor);
			var vid = new Vid(1, index, null);

			Assert.AreEqual(vid.Index, expectedIndexValue);
		}
	}
}
