using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Dvm
{
	class VmProcessor : VmThread
	{
		readonly VmExecutor m_executor;
		readonly int m_index;
		readonly BlockingCollection<Vipo> m_jobsQueue = new BlockingCollection<Vipo>();

		static readonly LocalDataStoreSlot WorkingVipoSlot = Thread.GetNamedDataSlot("WorkingVipo");

		#region Properties

		public int Index => m_index;
		public int JobsCount => m_jobsQueue.Count;

		#endregion

		public VmProcessor(IVmThreadController controller, VmExecutor executor, int index)
			: base(controller, $"DVM-VP{index}")
		{
			m_executor = executor;
			m_index = index;
		}

		protected override void OnDispose(bool explicitCall)
		{
			base.OnDispose(explicitCall);

			if (explicitCall)
				m_jobsQueue.Dispose();
		}

		public void AddJob(Vipo vipo)
		{
			if (vipo == null)
				throw new KernelFaultException("VipoJob.Vipo is null");

			m_jobsQueue.Add(vipo);
		}

		protected override void ThreadEntry()
		{
			for (Vipo workingVipo = null; ;)
			{
				var vipo = m_jobsQueue.Take(EndToken);
				if (vipo != workingVipo)
				{
					SetWorkingVipo(vipo);
					workingVipo = vipo;
				}

				vipo.RunEntry();

				m_executor.FinishJob(workingVipo);
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
