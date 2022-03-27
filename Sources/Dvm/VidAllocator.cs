using System.Threading;

namespace Dvm
{
	interface IUsedVidQuery
	{
		bool IsUsed(Vid vid);
	}

	class VidAllocator
	{
		readonly IUsedVidQuery m_usedVidQuery;
		readonly ulong m_maxIndex;
		long m_index;

		public VidAllocator(IUsedVidQuery usedVidQuery, ulong maxIndex = Vid.MaxIndex, ulong initialIndex = 0)
		{
			m_usedVidQuery = usedVidQuery;
			m_maxIndex = maxIndex;
			m_index = (long)(initialIndex & maxIndex);
		}

		public Vid New(Vipo vipo)
		{
			// Test cases can pass in a null vipo
			//Debug.Assert(vipo != null);

			for (ulong loops = 0; loops < m_maxIndex; ++loops)
			{
				var current = (ulong)Interlocked.Increment(ref m_index) & m_maxIndex;
				if (current == 0)
					continue;

				var vid = new Vid(1, current, vipo);

				if (!m_usedVidQuery.IsUsed(vid))
					return vid;
			}

			throw new KernelFaultException("Vid is exhausted");
		}
	}
}
