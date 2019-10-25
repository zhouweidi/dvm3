using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Dvm
{
	public enum SchedulerState
	{
		Running, StopRequested, Stopped
	}

	public sealed class Scheduler : DisposableObject
	{
		CancellationTokenSource m_cts;
		VirtualProcessor[] m_virtualProcessors;
		Thread m_thread;

		BlockingCollection<ScheduleTask> m_scheduleTaskQueue = new BlockingCollection<ScheduleTask>();

		FreeVPSet m_free;

		object m_workingsLock = new object();
		Dictionary<Vipo, VirtualProcessor> m_workings = new Dictionary<Vipo, VirtualProcessor>();

		SchedulerState m_state = SchedulerState.Running;
		volatile Exception m_exception;

		ConcurrentDictionary<Vid, Vipo> m_vipos = new ConcurrentDictionary<Vid, Vipo>();
		long m_vidIndex;
		SpinLock m_vidIndexLock = new SpinLock();

		#region VirtualProcessor

		internal class VirtualProcessor : DisposableObject
		{
			Scheduler m_scheduler;
			CancellationToken m_cancelToken;
			Thread m_thread;
			ManualResetEventSlim m_tickSignal = new ManualResetEventSlim(false);
			Queue<TickTask> m_tasks = new Queue<TickTask>();

			public VirtualProcessor(Scheduler scheduler, CancellationToken cancelToken, int index)
			{
				m_scheduler = scheduler;

				m_cancelToken = cancelToken;

				m_thread = m_scheduler.CreateThread($"DVM-VP{index}", VPThread);
			}

			protected override void DisposeManaged()
			{
				m_tickSignal.Dispose();

				base.DisposeManaged();
			}

			public void TriggerTick()
			{
				m_tickSignal.Set();
			}

			void VPThread()
			{
				for (; ; )
				{
					m_tickSignal.Wait(m_cancelToken);
					m_tickSignal.Reset();

					var tickTask = m_scheduler.GetNextTickTask(this, null);
					if (tickTask == null)
						throw new KernelFault("No initial tick task found");
					if (tickTask.Vipo == null)
						throw new KernelFault("Expecting non-null vipo of a TickTask");

					var vipo = tickTask.Vipo;
					SetTickingVid(vipo.Vid);

					try
					{
						for (; ; )
						{
							vipo.Tick(tickTask);

							tickTask = m_scheduler.GetNextTickTask(this, vipo);

							if (tickTask == null)
								break;

							if (tickTask.Vipo != vipo)
								throw new KernelFault("TickTask is not the same one as the previous vipo");
						}

						var outMessages = vipo.TakeOutMessages();
						if (outMessages != null)
							m_scheduler.AddScheduleTask(new DispatchVipoMessages(outMessages));
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

			public Thread Thread
			{
				get { return m_thread; }
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
			m_cts = CancellationTokenSource.CreateLinkedTokenSource(cancelToken);

			// Create all vp
			m_virtualProcessors = new VirtualProcessor[virtualProcessors];
			for (int i = 0; i < virtualProcessors; i++)
				m_virtualProcessors[i] = new VirtualProcessor(this, m_cts.Token, i);

			// Add vp into free set
			m_free = new FreeVPSet(m_virtualProcessors);

			// Create schedule thread
			m_thread = CreateThread("DVM-Kernel", KernelThread);
		}

		Thread CreateThread(string name, Action threadRun)
		{
			var thread = new Thread(() => ThreadEntry(threadRun));
			thread.Name = name;
			thread.Start();

			return thread;
		}

		protected override void DisposeUnmanaged(bool explicitCall)
		{
			m_state = SchedulerState.StopRequested;

			if (explicitCall)
			{
				m_cts.Cancel();

				for (int i = 0; i < m_virtualProcessors.Length; i++)
					m_virtualProcessors[i].Thread.Join();

				m_thread.Join();
			}
			else
			{
				for (int i = 0; i < m_virtualProcessors.Length; i++)
					m_virtualProcessors[i].Thread.Abort();

				m_thread.Abort();
			}

			m_state = SchedulerState.Stopped;

			base.DisposeUnmanaged(explicitCall);
		}

		protected override void DisposeManaged()
		{
			for (int i = 0; i < m_virtualProcessors.Length; i++)
				m_virtualProcessors[i].Dispose();

			m_scheduleTaskQueue.Dispose();
			m_cts.Dispose();

			base.DisposeManaged();
		}

		void ThreadEntry(Action threadRun)
		{
			try
			{
				threadRun();
			}
			catch (OperationCanceledException)
			{ }
			catch (Exception e)
			{
				HandleError(e);
			}
		}

		public event Action<Exception> OnError;

		void HandleError(Exception exception)
		{
			if (exception == null)
				throw new ArgumentNullException(nameof(exception));

			if (Interlocked.CompareExchange(ref m_exception, exception, null) == null)
				OnError?.Invoke(exception);
		}

		void KernelThread()
		{
			VirtualProcessor lastFreeVP = null;
			var stp = new ScheduleTasksProcessor(m_vipos);

			for (; ; )
			{
				{
					var scheduleTask = m_scheduleTaskQueue.Take(m_cts.Token);

					do
					{
						stp.Process(scheduleTask);

						if (stp.ViposToTick >= m_virtualProcessors.Length)
							break;
					}
					while (m_scheduleTaskQueue.TryTake(out scheduleTask));
				}

				foreach (var tickTask in stp.TickTasks)
				{
					if (lastFreeVP == null)
						lastFreeVP = m_free.Take(m_cts.Token);

					if (RunTickTaskOnVP(tickTask, lastFreeVP))
						lastFreeVP = null; // Consumed the VP
				}

				stp.Reset();
			}
		}

		#region ScheduleTasksProcessor

		class ScheduleTasksProcessor
		{
			ConcurrentDictionary<Vid, Vipo> m_vipos;
			Dictionary<Vid, TickTask> m_tickTasks = new Dictionary<Vid, TickTask>();

			public int ViposToTick
			{
				get { return m_tickTasks.Count; }
			}

			public IEnumerable<TickTask> TickTasks
			{
				get { return m_tickTasks.Values; }
			}

			public ScheduleTasksProcessor(ConcurrentDictionary<Vid, Vipo> vipos)
			{
				m_vipos = vipos;
			}

			public void Process(ScheduleTask scheduleTask)
			{
				switch (scheduleTask)
				{
					case DispatchVipoMessages dvm:
						for (int i = 0; i < dvm.Messages.Count; i++)
						{
							var message = dvm.Messages[i];

							var tickTask = GetTickTask(message.To);
							tickTask.AddMessage(message);
						}
						break;

					case VipoStart vs:
						{
							var vid = vs.Vipo.Vid;
							if (!m_vipos.TryAdd(vid, vs.Vipo))
								throw new InvalidOperationException($"Vipo {vid} already exists, failed to add");

							var tickTask = GetTickTask(vid);
							tickTask.SetStartRequest();
						}
						break;

					case VipoDestroy vd:
						{
							var vid = vd.Vipo.Vid;

							var tickTask = GetTickTask(vid);
							tickTask.SetDestroyRequest();

							Vipo tempVipo;
							if (!m_vipos.TryRemove(vid, out tempVipo))
								throw new InvalidOperationException($"Can't remove vipo {vid}");

							if (tempVipo != vd.Vipo)
								throw new InvalidOperationException($"Unmatched vipo {vid} being removed");
						}
						break;

					case VipoSchedule vs:
						{
							var vid = vs.Vipo.Vid;
							if (!m_vipos.ContainsKey(vid))
								throw new InvalidOperationException($"Vipo {vid} to schedule doesn't exist");

							var tickTask = GetTickTask(vid);
							tickTask.AddMessage(Message.VipoSchedule);
						}
						break;

					default:
						throw new NotSupportedException($"Unsupported schedule task '{scheduleTask.GetType()}'");
				}
			}

			TickTask GetTickTask(Vid vid)
			{
				TickTask tickTask;
				if (!m_tickTasks.TryGetValue(vid, out tickTask))
				{
					Vipo vipo;
					if (!m_vipos.TryGetValue(vid, out vipo))
						throw new InvalidOperationException($"No vipo '{vid}' found");

					tickTask = new TickTask(vipo);
					m_tickTasks.Add(vid, tickTask);
				}

				return tickTask;
			}

			public void Reset()
			{
				m_tickTasks.Clear();
			}
		}

		#endregion

		bool RunTickTaskOnVP(TickTask tickTask, VirtualProcessor freeVP)
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

		internal void AddScheduleTask(ScheduleTask scheduleTask)
		{
			if (scheduleTask == null)
				throw new ArgumentNullException(nameof(scheduleTask));

			m_scheduleTaskQueue.Add(scheduleTask);
		}

		internal Vid CreateVid(string description)
		{
			for (; ; )
			{
				ulong index;
				{
					bool gotLock = false;
					try
					{
						m_vidIndexLock.Enter(ref gotLock);

						index = Vid.GetNextIndex(ref m_vidIndex);
					}
					finally
					{
						if (gotLock)
							m_vidIndexLock.Exit();
					}
				}

				var vid = new Vid(1, index, description);
				if (!m_vipos.ContainsKey(vid))
					return vid;
			}
		}

		#region Properties

		public SchedulerState State
		{
			get { return m_state; }
		}

		public Exception Exception
		{
			get { return m_exception; }
		}

		public int ViposCount
		{
			get { return m_vipos.Count; }
		}

		#endregion
	}
}
