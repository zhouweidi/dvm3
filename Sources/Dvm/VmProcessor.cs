using System;
using System.Diagnostics;
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

		[Conditional("DEBUG")]
		static void SetWorkingVipo(Vipo vipo)
		{
			Thread.SetData(WorkingVipoSlot, vipo);
		}

		[Conditional("DEBUG")]
		public static void CheckWorkingVipo(Vipo vipo, string errorMessage)
		{
			var working = (Vipo)Thread.GetData(WorkingVipoSlot);

			if (!ReferenceEquals(vipo, working))
				throw new InvalidOperationException(errorMessage);
		}
	}
}
