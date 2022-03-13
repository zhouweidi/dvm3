using System;
using System.Collections.Generic;
using System.Threading;

namespace Dvm
{
	class VmProcessor : VmThread
	{
		readonly VmScheduler m_scheduler;
		readonly int m_index;
		readonly ManualResetEventSlim m_tickSignal = new ManualResetEventSlim(false);
		readonly Queue<VipoJob> m_jobs = new Queue<VipoJob>();

		#region Properties

		VmExecutor Executor => m_scheduler.Executor;
		public int Index => m_index;
		public Queue<VipoJob> Jobs => m_jobs;
		
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
				m_tickSignal.Dispose();
		}

		public void TriggerRun()
		{
			m_tickSignal.Set();
		}

		protected override void ThreadEntry()
		{
			for (; ; )
			{
				m_tickSignal.Wait(EndToken);
				m_tickSignal.Reset();

				var tickTask = Executor.GetNextVipoJob(this, null);
				if (tickTask == null)
					throw new KernelFaultException("No initial TickTask found");
				if (tickTask.Vipo == null)
					throw new KernelFaultException("Expecting a non-null vipo of a TickTask");

				var vipo = tickTask.Vipo;
				SetTickingVid(vipo.Vid);

				try
				{
					for (; ; )
					{
						if (vipo.Exception == null)
							vipo.Tick(tickTask);

						tickTask = Executor.GetNextVipoJob(this, vipo);
						if (tickTask == null)
							break;

						if (tickTask.Vipo != vipo)
							throw new KernelFaultException("The TickTask is not along with the previous vipo");
					}

					var outMessages = vipo.TakeOutMessages();

					if (vipo.Exception == null)
					{
						if (outMessages != null)
							m_scheduler.AddRequest(new DispatchVipoMessages(outMessages));
					}
					else
						vipo.Destroy();
				}
				finally
				{
					SetTickingVid(Vid.Empty);
				}
			}
		}

		static readonly LocalDataStoreSlot TickingVidDataSlot = Thread.GetNamedDataSlot("TickingVid");

		static void SetTickingVid(Vid vid)
		{
			Thread.SetData(TickingVidDataSlot, vid.Data);
		}

		internal static Vid GetTickingVid()
		{
			return new Vid((ulong)Thread.GetData(TickingVidDataSlot), null);
		}
	}
}
