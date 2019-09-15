﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Dvm
{
	class Scheduler
	{
		CancellationToken m_cancelToken;
		VirtualProcessor[] m_virtualProcessors;
		Thread m_thread;

		BlockingCollection<TickTask> m_tickTaskQueue = new BlockingCollection<TickTask>();

		FreeVPSet m_free;

		object m_workingsLock = new object();
		Dictionary<Vipo, VirtualProcessor> m_workings = new Dictionary<Vipo, VirtualProcessor>();

		#region VirtualProcessor

		class VirtualProcessor //: IDisposable
		{
			Scheduler m_scheduler;
			CancellationToken m_cancelToken;
			Thread m_thread;
			ManualResetEventSlim m_tickSignal = new ManualResetEventSlim(false);
			Queue<TickTask> m_tasks = new Queue<TickTask>();

			public VirtualProcessor(Scheduler scheduler, CancellationToken cancelToken)
			{
				m_scheduler = scheduler;

				m_cancelToken = cancelToken;

				m_thread = new Thread(ThreadEntry);
				m_thread.Start();
			}

			public void TriggerTick()
			{
				m_tickSignal.Set();
			}

			void ThreadEntry()
			{
				for (; ; )
				{
					m_tickSignal.Wait(m_cancelToken);
					m_tickSignal.Reset();

					var task = m_scheduler.GetNextTickTask(this, null);
					if (task == null)
						throw new InvalidOperationException("No initial tick task found");

					if (task.Vipo == null)
						throw new InvalidOperationException("Unexpected null vipo of a TickTask");

					var vipo = task.Vipo;
					for (; ; )
					{
						vipo.Tick(task);

						task = m_scheduler.GetNextTickTask(this, vipo);
						if (task == null)
							break;

						if (task.Vipo != vipo)
							throw new InvalidOperationException("TickTask is not the same one as the previous vipo");
					}
				}
			}

			public Queue<TickTask> TickTasks
			{
				get { return m_tasks; }
			}
		}

		#endregion

		#region FreeVPSet

		class FreeVPSet
		{
			ConcurrentBag<VirtualProcessor> m_set;
			SemaphoreSlim m_semaphore;

			public FreeVPSet(VirtualProcessor[] virtualProcessors)
			{
				m_set = new ConcurrentBag<VirtualProcessor>(virtualProcessors);
				m_semaphore = new SemaphoreSlim(virtualProcessors.Length, virtualProcessors.Length);
			}

			public VirtualProcessor Take(CancellationToken cancelToken)
			{
				m_semaphore.Wait(cancelToken);

				VirtualProcessor vp;
				if (!m_set.TryTake(out vp))
					throw new InvalidOperationException("Failed to take a free virtual processor");

				return vp;
			}

			public void Return(VirtualProcessor vp)
			{
				m_set.Add(vp);

				m_semaphore.Release();
			}
		}

		#endregion

		public Scheduler(int virtualProcessors, CancellationToken cancelToken)
		{
			m_cancelToken = cancelToken;

			// Create all vp
			m_virtualProcessors = new VirtualProcessor[virtualProcessors];
			for (int i = 0; i < virtualProcessors; i++)
				m_virtualProcessors[i] = new VirtualProcessor(this, cancelToken);

			// Add vp into free set
			m_free = new FreeVPSet(m_virtualProcessors);

			// Create schedule thread
			m_thread = new Thread(ThreadEntry);
			m_thread.Start();
		}

		void ThreadEntry()
		{
			for (; ; )
			{
				var freeVP = m_free.Take(m_cancelToken);

				for (; ; )
				{
					var tickTask = m_tickTaskQueue.Take(m_cancelToken);

					if (RunTickTaskOnVP(freeVP, tickTask))
						break;
				}
			}
		}

		bool RunTickTaskOnVP(VirtualProcessor freeVP, TickTask tickTask)
		{
			lock (m_workingsLock)
			{
				VirtualProcessor workingVP;
				if (m_workings.TryGetValue(tickTask.Vipo, out workingVP))
				{
					// throw if not working
					workingVP.TickTasks.Enqueue(tickTask);

					return false;
				}
				else
				{
					//if (freeVP.TickTasks.Count != 0) throw;

					m_workings.Add(tickTask.Vipo, freeVP);

					freeVP.TickTasks.Enqueue(tickTask);
					freeVP.TriggerTick();

					return true;
				}
			}
		}

		TickTask GetNextTickTask(VirtualProcessor vp, Vipo vipo)
		{
			lock (m_workingsLock)
			{
				if (vp.TickTasks.Count == 0)
				{
					if (vipo == null)
						throw new InvalidOperationException("Unexected null vipo");

					m_workings.Remove(vipo);

					goto ReturnToFree;
				}
				else
					return vp.TickTasks.Dequeue();
			}

		ReturnToFree:
			m_free.Return(vp);
			return null;

		}
	}
}
