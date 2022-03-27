using System;
using System.Threading;

namespace Dvm
{
	class VmProcessor : VmThread
	{
		readonly VmScheduler m_scheduler;
		readonly int m_index;

		static readonly LocalDataStoreSlot WorkingVipoSlot = Thread.GetNamedDataSlot("WorkingVipo");

		public VmProcessor(IVmThreadController controller, VmScheduler scheduler, int index)
			: base(controller, $"DVM-VP{index}")
		{
			m_scheduler = scheduler;
			m_index = index;
		}

		public override string ToString() => $"VmProcessor {m_index}";

		protected override void ThreadEntry()
		{
			for (Vipo workingVipo = null; ;)
			{
				var vipo = m_scheduler.GetJob();

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
				while (!m_scheduler.FinishJob(vipo));

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
