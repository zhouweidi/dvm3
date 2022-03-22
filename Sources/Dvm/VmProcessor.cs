using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Dvm
{
	class VmProcessor : VmThread
	{
		readonly VmScheduler m_scheduler;
		readonly int m_index;
		readonly BlockingCollection<VipoJob> m_jobsCache = new BlockingCollection<VipoJob>();

		static readonly LocalDataStoreSlot WorkingVipoSlot = Thread.GetNamedDataSlot("WorkingVipo");

		#region Properties

		VmExecutor Executor => m_scheduler.Executor;
		public int Index => m_index;
		public int JobsCount => m_jobsCache.Count;

		#endregion

		public VmProcessor(IVmThreadController controller, VmScheduler scheduler, int index)
			: base(controller, $"DVM-VP{index}")
		{
			m_scheduler = scheduler;
			m_index = index;
		}

		protected override void OnDispose(bool explicitCall)
		{
			base.OnDispose(explicitCall);

			if (explicitCall)
				m_jobsCache.Dispose();
		}

		public void AddJob(VipoJob job)
		{
			if (job.Vipo == null)
				throw new KernelFaultException("VipoJob.Vipo is null");

			m_jobsCache.Add(job);
		}

		protected override void ThreadEntry()
		{
			for (Vipo workingVipo = null; ;)
			{
				var job = m_jobsCache.Take(EndToken);
				if (job.Vipo != workingVipo)
				{
					SetWorkingVipo(job.Vipo);
					workingVipo = job.Vipo;
				}

				RunJob(job);

				Executor.FinishJob(this, workingVipo);
			}
		}

		void RunJob(VipoJob job)
		{
			var vipo = job.Vipo;

			vipo.RunEntry(job);

			var outMessages = vipo.TakeOutMessages();
			if (outMessages != null && outMessages.Count > 0)
				m_scheduler.AddRequest(new DispatchVipoMessages(outMessages));
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
