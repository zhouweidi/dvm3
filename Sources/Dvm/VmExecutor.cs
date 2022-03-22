using System.Collections.Generic;

namespace Dvm
{
	class VmExecutor : DisposableObject
	{
		readonly IVmThreadController m_controller;
		readonly VmProcessor[] m_processors;

		readonly object m_workingProcessorsLock = new object();
		readonly Dictionary<Vipo, Workload> m_workingProcessors = new Dictionary<Vipo, Workload>();

		#region Properties

		public int ProcessorsCount => m_processors.Length;

		#endregion

		public VmExecutor(IVmThreadController controller, VmScheduler scheduler, int processorsCount)
		{
			m_controller = controller;

			m_processors = new VmProcessor[processorsCount];
			for (int i = 0; i < processorsCount; i++)
				m_processors[i] = new VmProcessor(controller, scheduler, i);
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

		#region Workload

		class Workload
		{
			public int JobsCount;
			public VmProcessor Processor;
		}

		#endregion

		public void DispatchJobs(IEnumerable<VipoJob> jobs)
		{
			foreach (var job in jobs)
			{
				// Push to the processor already has jobs of the vipo
				lock (m_workingProcessorsLock)
				{
					if (m_workingProcessors.TryGetValue(job.Vipo, out Workload workload))
					{
						workload.JobsCount++;
						workload.Processor.AddJob(job);
						continue;
					}
				}

				// Get an idle processor
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
				lock (m_workingProcessorsLock)
				{
					var workload = new Workload()
					{
						JobsCount = 1,
						Processor = idleProcessor
					};

					m_workingProcessors.Add(job.Vipo, workload);

					idleProcessor.AddJob(job);
				}
			}
		}

		public void FinishJob(VmProcessor processor, Vipo workingVipo)
		{
			lock (m_workingProcessorsLock)
			{
				if (m_workingProcessors.TryGetValue(workingVipo, out Workload workload))
				{
					if (--workload.JobsCount == 0)
						m_workingProcessors.Remove(workingVipo);
				}
			}
		}
	}
}
