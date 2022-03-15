using Dvm;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DvmTests.VidTests
{
	[TestClass]
	public class VidAllocation : TestBase
	{
		class FakeUsedVidQuery : IUsedVidQuery
		{
			readonly Vid m_unusedVid;

			public FakeUsedVidQuery(Vid usedVid)
			{
				m_unusedVid = usedVid;
			}

			public bool IsUsed(Vid vid)
			{
				Assert.AreNotEqual(vid, Vid.Empty);

				return vid != m_unusedVid;
			}
		}

		[TestMethod]
		public void Allocate()
		{
			const ulong MaxIndex = 0xff;

			{
				var query = new FakeUsedVidQuery(Vid.Empty);
				var allocator = new VidAllocator(query, MaxIndex);

				Assert.ThrowsException<KernelFaultException>(
					() => allocator.New(null), 
					"Vid is exhausted");
			}

			TestNextIndex(MaxIndex, 0, 1);
			TestNextIndex(MaxIndex, 1, 2);
			TestNextIndex(MaxIndex, MaxIndex - 1, MaxIndex);
			TestNextIndex(MaxIndex, MaxIndex, 1);
			TestNextIndex(MaxIndex, MaxIndex + 1, 1);
		}

		static void TestNextIndex(ulong maxIndex, ulong initialIndex, ulong expectedIndex)
		{
			var expectedVid = new Vid(1, expectedIndex, null);
			var query = new FakeUsedVidQuery(expectedVid);
			var allocator = new VidAllocator(query, maxIndex, initialIndex);

			var vid = allocator.New(null);

			Assert.AreEqual(vid, expectedVid);
		}
	}
}
