using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Dvm
{
	class VmProcessor : VmThread
	{
		readonly VmScheduler m_scheduler;
		readonly int m_index;
		readonly ManualResetEventSlim m_circleSignal = new ManualResetEventSlim(false);
		readonly ConcurrentQueue<VipoJob> m_jobsCache = new ConcurrentQueue<VipoJob>();

		static readonly LocalDataStoreSlot WorkingVipoSlot = Thread.GetNamedDataSlot("WorkingVipo");

		#region Properties

		VmExecutor Executor => m_scheduler.Executor;
		public int Index => m_index;
		public bool HasJobs => !m_jobsCache.IsEmpty;

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
				m_circleSignal.Dispose();
		}

		public void StartCircle(VipoJob job)
		{
			if (!m_jobsCache.IsEmpty)
				throw new KernelFaultException("Can't start a VM processor since it still has jobs to run");

			AddJob(job);

			m_circleSignal.Set();
		}

		public void AddJob(VipoJob job)
		{
			if (job.Vipo == null)
				throw new KernelFaultException("VipoJob.Vipo is null");

			m_jobsCache.Enqueue(job);
		}

		protected override void ThreadEntry()
		{
			for (; ; )
			{
				m_circleSignal.Wait(EndToken);
				m_circleSignal.Reset();

				for (Vipo workingVipo = null; ;)
				{
					while (m_jobsCache.TryDequeue(out VipoJob job))
					{
						if (workingVipo == null)
						{
							workingVipo = job.Vipo;
							SetWorkingVipo(workingVipo);
						}
						else if (job.Vipo != workingVipo)
							throw new KernelFaultException("Different vipos in the same VM processor circle");

						RunJob(job);
					}

					if (workingVipo == null)
						throw new KernelFaultException("Empty VM processor circle");

					if (Executor.FinishCircle(this, workingVipo))
					{
						SetWorkingVipo(null);
						break;
					}
				}
			}
		}

		void RunJob(VipoJob job)
		{
			var vipo = job.Vipo;

			if (vipo.Exception == null)
				vipo.Tick(job);

			var outMessages = vipo.TakeOutMessages();

			if (vipo.Exception == null)
			{
				if (outMessages != null)
					m_scheduler.AddRequest(new DispatchVipoMessages(outMessages));
			}
			else
				vipo.Destroy();
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
