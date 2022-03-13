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
		int m_idleProcessorIndex = -1;

		readonly object m_runningViposLock = new object();
		readonly Dictionary<Vipo, VmProcessor> m_runningVipos = new Dictionary<Vipo, VmProcessor>();

		#region Properties

		public int ProcessorsCount => m_processors.Length;

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
				lock (m_runningViposLock)
				{
					if (m_runningVipos.TryGetValue(job.Vipo, out VmProcessor processor))
					{
						processor.Jobs.Enqueue(job);
						continue;
					}
				}

				if (m_idleProcessorIndex < 0)
					m_idleProcessorIndex = m_idleProcessors.Take(m_controller.EndToken);

				lock (m_runningViposLock)
				{
					var idleProcessor = m_processors[m_idleProcessorIndex];
					if (idleProcessor.Jobs.Count != 0)
						throw new KernelFaultException("Expecting the idle virtual process has no job");

					m_runningVipos.Add(job.Vipo, idleProcessor);
					m_idleProcessorIndex = -1;

					idleProcessor.Jobs.Enqueue(job);
					idleProcessor.TriggerRun();
				}
			}
		}

		public VipoJob GetNextVipoJob(VmProcessor processor, Vipo vipo)
		{
			lock (m_runningViposLock)
			{
				if (processor.Jobs.Count == 0)
				{
					if (vipo == null)
						throw new KernelFaultException("Expecting a non-null vipo to free a virtual processor");

					m_runningVipos.Remove(vipo);

					goto ReturnToFree;
				}
				else
					return processor.Jobs.Dequeue();
			}

		ReturnToFree:
			m_idleProcessors.Return(processor.Index);

			return null;
		}

		#region IndexBag

		class IndexBag : DisposableObject
		{
			readonly ConcurrentBag<int> m_bag;
			readonly SemaphoreSlim m_semaphore;

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
