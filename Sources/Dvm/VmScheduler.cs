using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Dvm
{
	class VmScheduler : VmThread
	{
		readonly VmExecutor m_executor;
		readonly ConcurrentDictionary<Vid, Vipo> m_vipos;
		readonly BlockingCollection<ScheduleRequest> m_requestsQueue = new BlockingCollection<ScheduleRequest>();

		#region Properties

		public VmExecutor Executor => m_executor;

		#endregion
		
		public VmScheduler(IVmThreadController controller, int processorsCount, ConcurrentDictionary<Vid, Vipo> vipos)
			: base(controller, "DVM-Scheduler")
		{
			m_executor = new VmExecutor(controller, this, processorsCount);
			m_vipos = vipos;
		}

		protected override void OnDispose(bool explicitCall)
		{
			base.OnDispose(explicitCall);

			if (explicitCall)
			{
				m_requestsQueue.Dispose();

				m_executor.Dispose();
			}
		}

		public void AddRequest(ScheduleRequest request)
		{
			if (request == null)
				throw new ArgumentNullException(nameof(request));

			m_requestsQueue.Add(request);
		}

		protected override void ThreadEntry()
		{
			for (var vipoJobs = new Dictionary<Vid, VipoJob>(); ; vipoJobs.Clear())
			{
				GenerateVipoJobs(vipoJobs);

				m_executor.RunJobs(vipoJobs.Values);
			}
		}

		void GenerateVipoJobs(Dictionary<Vid, VipoJob> vipoJobs)
		{
			// TODO 如果requests都是填充单一vipo，会导致这个循环一直不结束。需要设定最小循环次数或时间
			for (var request = m_requestsQueue.Take(EndToken); ;)
			{
				ProcessRequest(request, vipoJobs);

				var enoughJobs = vipoJobs.Count >= m_executor.ProcessorsCount;
				if (enoughJobs)
					break;

				var noMoreRequest = !m_requestsQueue.TryTake(out request);
				if (noMoreRequest)
				{
					if (vipoJobs.Count > 0)
						break;

					request = m_requestsQueue.Take(EndToken);
				}
			}
		}

		void ProcessRequest(ScheduleRequest request, Dictionary<Vid, VipoJob> vipoJobs)
		{
			switch (request)
			{
				case DispatchVipoMessages dispatch:
					ProcessRequest_Dispatch(dispatch, vipoJobs);
					break;

				case VipoStart start:
					ProcessRequest_VipoStart(start, vipoJobs);
					break;

				case VipoDestroy destroy:
					ProcessRequest_VipoDestroy(destroy, vipoJobs);
					break;

				case VipoSchedule schedule:
					ProcessRequest_VipoSchedule(schedule, vipoJobs);
					break;

				default:
					throw new KernelFaultException($"Unsupported schedule request '{request.GetType()}'");
			}
		}

		void ProcessRequest_Dispatch(DispatchVipoMessages dispatch, Dictionary<Vid, VipoJob> vipoJobs)
		{
			for (int i = 0; i < dispatch.Messages.Count; i++)
			{
				var message = dispatch.Messages[i];
				var vid = message.To;

				// Check if the vipo exists
				if (!m_vipos.TryGetValue(vid, out Vipo vipo))
					continue;

				// Add to a job
				var job = GetOrAddVipoJob(vipoJobs, vipo);
				job.AddMessage(message);
			}
		}

		void ProcessRequest_VipoStart(VipoStart start, Dictionary<Vid, VipoJob> vipoJobs)
		{
			var vipo = start.Vipo;
			var vid = vipo.Vid;

			// Add to the vipos list
			if (!m_vipos.TryAdd(vid, vipo))
				throw new KernelFaultException($"The vipo '{vid}' to add has already existed in the vipos list");

			// Add to a job
			var job = new VipoJob(vipo);
			if (!vipoJobs.TryAdd(vid, job))
				throw new KernelFaultException($"Failed to add a vipo job for a start request of '{vid}'");

			var withCallback = start.Vipo.HasCallbackOption(Vipo.CallbackOptions.OnStart);
			job.SetStartRequest(withCallback);
		}

		void ProcessRequest_VipoDestroy(VipoDestroy destroy, Dictionary<Vid, VipoJob> vipoJobs)
		{
			var vipo = destroy.Vipo;
			var vid = vipo.Vid;

			// Check if the vipo exists
			if (!m_vipos.ContainsKey(vid))
				throw new KernelFaultException($"No vipo '{vid}' found to deal with a destroy request");

			// Add to a job
			var withCallback = destroy.Vipo.HasCallbackOption(Vipo.CallbackOptions.OnDestroy);
			var shouldCallback = withCallback && destroy.Vipo.Exception == null;

			if (shouldCallback)
			{
				var job = GetOrAddVipoJob(vipoJobs, vipo);
				job.SetDestroyRequest();
			}

			// Remove from the vipos list
			if (!m_vipos.TryRemove(vid, out Vipo removedVipo))
				throw new KernelFaultException($"No vipo {vid} to remove");

			if (removedVipo != destroy.Vipo)
				throw new KernelFaultException($"Unmatched vipo {vid} being removed");
		}

		void ProcessRequest_VipoSchedule(VipoSchedule schedule, Dictionary<Vid, VipoJob> vipoJobs)
		{
			var vipo = schedule.Vipo;
			var vid = vipo.Vid;

			// Check if the vipo exists
			if (!m_vipos.ContainsKey(vid))
				throw new KernelFaultException($"No vipo '{vid}' found to deal with a schedule request");

			// Add to a job
			var job = GetOrAddVipoJob(vipoJobs, vipo);
			job.AddMessage(SystemVipoMessages.VipoSchedule);
		}

		static VipoJob GetOrAddVipoJob(Dictionary<Vid, VipoJob> vipoJobs, Vipo vipo)
		{
			if (!vipoJobs.TryGetValue(vipo.Vid, out VipoJob job))
			{
				job = new VipoJob(vipo);
				vipoJobs.Add(vipo.Vid, job);
			}

			return job;
		}
	}
}
