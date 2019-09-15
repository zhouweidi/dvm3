class VirtualProcessor
{
	object m_lock;
	Queue<TickTask> m_tasks; // in upper lock section
	Event m_runSignal; // AutoReset?

	public VirtualProcessor(object upperLock)
	{
		m_lock = upperLock;
	}

	public void Run(TickTask task)
	{
		//lock (m_lock)
		{
			if (m_tasks.Count != 0)
				throw;

			m_tasks.Add(task);
		}

		m_runSignal.Set();
	}

	public void AppendTickTask(TickTask task)
	{
		//lock (m_lock)
		{
			// throw if not working

			m_tasks.Add(task);
		}
	}

	void ThreadMain()
	{
		for (; ; )
		{
			if (!m_runSignal.Wait())
				return;

			m_runSignal.Reset();

			for (Vipo vipo = null; ;)
			{
				TickTask task;
				lock (m_lock)
				{
					if (m_tasks.Count == 0)
					{
						m_tickControl.FreeVirtualProcessor(this, vipo);
						break;
					}

					task = m_tasks.Dequeue();
				}

				vipo = task.Vipo; // check same vipo as before
				vipo.Run(task);
			}
		}
	}
}

class TickControl
{
	Queue<TickTask> m_tickTaskQueue;

	Stack<VirtualProcessor> m_free; // Try CoroutineBag/Stack: Intialize, WaitForFreeVirtualProcessor, AddFreeVirtualProcessor

	object m_workingsLock;
	Dictionary<Vipo, VirtualProcessor> m_workings;

	public void ThreadMain() // cancel support
	{
		while (var freeVP = m_free.WaitForFreeVirtualProcessor() )
		{
			for (; ; )
			{
				var task = m_tickTaskQueue.WaitForDequeue();

				lock (m_workingsLock)
				{
					var workingVP = m_workings.TryGetValue(task.Vipo);
					if (workingVP == null)
					{
						freeVP.Run(task);
						m_workings,Add(task.Vipo, freeVP);
						break;
					}

					workingVP.AppendTickTask(task);
				}
			}
		}
	}

	internal void FreeVirtualProcessor(VirtualProcessor vp, Vipo vipo)
	{
		//lock (m_workingsLock)
			m_workings.Remove(vipo);

		m_free.AddFreeVirtualProcessor(vp);
	}
}