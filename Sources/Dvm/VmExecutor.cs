using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Dvm
{
	class VmExecutor : DisposableObject
	{
		readonly IVmThreadController m_controller;
		readonly VmProcessor[] m_processors;

		readonly object m_workloadsLock = new object();
		readonly Dictionary<Vid, bool> m_workloads = new Dictionary<Vid, bool>(); // <Vid, PendingJobFlag>
		readonly BlockingCollection<Vipo> m_jobsQueue = new BlockingCollection<Vipo>();

		#region Properties

		public int ProcessorsCount => m_processors.Length;

		#endregion

		#region Initialization

		public VmExecutor(IVmThreadController controller, int processorsCount)
		{
			m_controller = controller;

			m_processors = new VmProcessor[processorsCount];
			for (int i = 0; i < processorsCount; i++)
				m_processors[i] = new VmProcessor(controller, this, i);
		}

		public void Start()
		{
			for (int i = 0; i < m_processors.Length; i++)
				m_processors[i].Start();
		}

		protected override void OnDispose(bool explicitCall)
		{
			m_controller.RequestToEnd();

			if (explicitCall)
			{
				for (int i = 0; i < m_processors.Length; i++)
					m_processors[i].Dispose();

				m_jobsQueue.Dispose();
			}
		}

		#endregion

		#region Jobs

		public void DispatchJob(Vipo vipo)
		{
			var vid = vipo.Vid;

			// Try to push to the processor that already has the same vipo working
			lock (m_workloadsLock)
			{
				if (m_workloads.TryGetValue(vid, out bool pendingJob))
				{
					if (pendingJob)
						throw new KernelFaultException($"The working vipo '{vid}' in workload already has the pending flag set");

					m_workloads[vid] = true;
					return;
				}
			}

			// Queue the job
			m_jobsQueue.Add(vipo);
		}

		public Vipo GetJob()
		{
			var vipo = m_jobsQueue.Take(m_controller.EndToken);

			lock (m_workloadsLock)
			{
				m_workloads.Add(vipo.Vid, false);
			}

			return vipo;
		}

		public bool FinishJob(Vipo workingVipo)
		{
			var vid = workingVipo.Vid;

			// Fetch a pending job or finish the job of the vipo
			lock (m_workloadsLock)
			{
				if (!m_workloads.TryGetValue(vid, out bool pendingJob))
					throw new KernelFaultException($"No workload found for the working vipo '{vid}'");

				if (pendingJob)
				{
					m_workloads[vid] = false;
					return false;
				}
				else
				{
					m_workloads.Remove(vid);
					return true;
				}
			}
		}

		#endregion
	}
}
