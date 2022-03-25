using System.Collections.Generic;

namespace Dvm
{
	class VmExecutor : DisposableObject
	{
		readonly IVmThreadController m_controller;
		readonly VmProcessor[] m_processors;

		readonly object m_workloadsLock = new object();
		readonly Dictionary<Vid, Workload> m_workloads = new Dictionary<Vid, Workload>();

		#region Properties

		public int ProcessorsCount => m_processors.Length;

		#endregion

		#region Workload

		class Workload
		{
			public int JobsCount;
			public VmProcessor Processor;
		}

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
			}
		}

		#endregion

		#region Jobs

		public void DispatchJob(Vipo vipo)
		{
			var vid = vipo.Vid;

			// Push to the processor already has the jobs of the vipo
			lock (m_workloadsLock)
			{
				if (m_workloads.TryGetValue(vid, out Workload workload))
				{
					workload.JobsCount++;
					workload.Processor.AddJob(vipo);
					return;
				}
			}

			// Get the idlest processor
			VmProcessor idleProcessor = null;
			{
				var minJobsCount = int.MaxValue;
				for (int i = 0; i < m_processors.Length; i++)
				{
					var processor = m_processors[i];
					var jobsCount = processor.JobsCount;
					if (jobsCount < minJobsCount)
					{
						idleProcessor = processor;
						minJobsCount = jobsCount;
					}
				}
			}

			// Push to the idle processor
			lock (m_workloadsLock)
			{
				var workload = new Workload()
				{
					JobsCount = 1,
					Processor = idleProcessor
				};

				m_workloads.Add(vid, workload);

				idleProcessor.AddJob(vipo);
			}
		}

		public void FinishJob(Vipo workingVipo)
		{
			var vid = workingVipo.Vid;

			lock (m_workloadsLock)
			{
				if (!m_workloads.TryGetValue(vid, out Workload workload))
					throw new KernelFaultException($"No workload found for the working vipo '{vid}'");

				if (--workload.JobsCount == 0)
					m_workloads.Remove(vid);
			}
		}

		#endregion
	}
}
