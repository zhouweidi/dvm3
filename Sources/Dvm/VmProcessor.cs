using System;
using System.Threading;

namespace Dvm
{
	class VmProcessor : VmThread
	{
		readonly VmExecutor m_executor;
		readonly int m_index;

		static readonly LocalDataStoreSlot WorkingVipoSlot = Thread.GetNamedDataSlot("WorkingVipo");

		public VmProcessor(IVmThreadController controller, VmExecutor executor, int index)
			: base(controller, $"DVM-VP{index}")
		{
			m_executor = executor;
			m_index = index;
		}

		public override string ToString() => $"VmProcessor{m_index}";

		protected override void ThreadEntry()
		{
			for (Vipo workingVipo = null; ;)
			{
				var vipo = m_executor.GetJob();

				// Set working vipo slot
				if (vipo != workingVipo)
				{
					SetWorkingVipo(vipo);
					workingVipo = vipo;
				}

				// Run the vipo
				do
				{
					vipo.RunEntry();
				}
				while (!m_executor.FinishJob(vipo));

				SetWorkingVipo(null);
			}
		}

		static void SetWorkingVipo(Vipo vipo)
		{
			Thread.SetData(WorkingVipoSlot, vipo);
		}

		public static Vipo GetWorkingVipo()
		{
			return (Vipo)Thread.GetData(WorkingVipoSlot);
		}
	}
}
