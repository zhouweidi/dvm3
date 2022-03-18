using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Dvm
{
	class VmExecutor : DisposableObject
	{
		readonly IVmThreadController m_controller;
		readonly VmProcessor[] m_processors;
		readonly IndexBag m_idleProcessors;

		readonly object m_workingProcessorsLock = new object();
		readonly Dictionary<Vipo, VmProcessor> m_workingProcessors = new Dictionary<Vipo, VmProcessor>();

		#region Properties

		public int ProcessorsCount => m_processors.Length;
		public int IdleProcessorsCount => m_idleProcessors.Count;

		#endregion

		public VmExecutor(IVmThreadController controller, VmScheduler scheduler, int processorsCount)
		{
			m_controller = controller;

			m_processors = new VmProcessor[processorsCount];
			for (int i = 0; i < processorsCount; i++)
				m_processors[i] = new VmProcessor(controller, scheduler, i);

			m_idleProcessors = new IndexBag(processorsCount);
		}

		protected override void OnDispose(bool explicitCall)
		{
			m_controller.RequestToEnd();

			if (explicitCall)
			{
				m_idleProcessors.Dispose();

				for (int i = 0; i < m_processors.Length; i++)
					m_processors[i].Dispose();
			}
		}

		public void RunJobs(IEnumerable<VipoJob> jobs)
		{
			foreach (var job in jobs)
			{
				// Push to a current processor running the same vipo
				lock (m_workingProcessorsLock)
				{
					if (m_workingProcessors.TryGetValue(job.Vipo, out VmProcessor processor))
					{
						processor.AddJob(job);
						continue;
					}
				}

				// Get and start an idle processor
				var idleProcessorIndex = m_idleProcessors.Take(m_controller.EndToken);
				var idleProcessor = m_processors[idleProcessorIndex];

				lock (m_workingProcessorsLock)
				{
					if (idleProcessor.HasJobs)
						throw new KernelFaultException("An idle virtual process should have no job");

					m_workingProcessors.Add(job.Vipo, idleProcessor);
				}

				idleProcessor.StartCircle(job);
			}
		}

		public bool FinishCircle(VmProcessor processor, Vipo workingVipo)
		{
			lock (m_workingProcessorsLock)
			{
				if (!processor.HasJobs)
				{
					m_workingProcessors.Remove(workingVipo);

					goto ReturnToFree;
				}
			}

			return false;

		ReturnToFree:
			m_idleProcessors.Return(processor.Index);
			return true;
		}

		#region IndexBag

		class IndexBag : DisposableObject
		{
			readonly ConcurrentBag<int> m_bag;
			readonly SemaphoreSlim m_semaphore;

			public int Count => m_bag.Count;

			public IndexBag(int count)
			{
				m_bag = new ConcurrentBag<int>(Enumerable.Range(0, count));
				m_semaphore = new SemaphoreSlim(count, count);
			}

			protected override void OnDispose(bool explicitCall)
			{
				if (explicitCall)
					m_semaphore.Dispose();
			}

			public int Take(CancellationToken cancelToken)
			{
				m_semaphore.Wait(cancelToken);

				if (!m_bag.TryTake(out int index))
					throw new KernelFaultException("Failed to take an index from the bag");

				return index;
			}

			public void Return(int index)
			{
				m_bag.Add(index);

				m_semaphore.Release();
			}
		}

		#endregion
	}
}
