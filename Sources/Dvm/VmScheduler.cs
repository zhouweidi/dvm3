using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Dvm
{
	class VmScheduler : VmThread
	{
		readonly VmExecutor m_executor;
		readonly int m_maxCircleMilliseconds;
		readonly ConcurrentDictionary<Vid, Vipo> m_vipos;
		readonly BlockingCollection<ScheduleRequest> m_requestsQueue = new BlockingCollection<ScheduleRequest>();

		#region Properties

		public VmExecutor Executor => m_executor;

		#endregion

		public VmScheduler(IVmThreadController controller, int processorsCount, int maxCircleMilliseconds, ConcurrentDictionary<Vid, Vipo> vipos)
			: base(controller, "DVM-Scheduler")
		{
			m_executor = new VmExecutor(controller, this, processorsCount);
			m_maxCircleMilliseconds = maxCircleMilliseconds;
			m_vipos = vipos;

			// Start the thread execution after everything gets initialized
			Start();
			m_executor.Start();
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
			var startTime = Environment.TickCount64;

			for (var request = m_requestsQueue.Take(EndToken); ;)
			{
				ProcessRequest(request, vipoJobs);

				// Time up
				if (m_maxCircleMilliseconds >= 0)
				{
					var elapsedTime = Environment.TickCount64 - startTime;
					if (elapsedTime >= m_maxCircleMilliseconds)
						break;
				}

				// Enough jobs
				var enoughJobs = vipoJobs.Count >= m_executor.IdleProcessorsCount;
				if (enoughJobs)
					break;

				// No more request
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

				case VipoSchedule schedule:
					ProcessRequest_VipoSchedule(schedule, vipoJobs);
					break;

				case VipoDetach detach:
					ProcessRequest_VipoDetach(detach);
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

		void ProcessRequest_VipoSchedule(VipoSchedule request, Dictionary<Vid, VipoJob> vipoJobs)
		{
			var vipo = request.Vipo;
			var vid = vipo.Vid;

			// Check if the vipo exists
			if (m_vipos.ContainsKey(vid))
			{
				if (!vipo.IsAttached)
					throw new KernelFaultException($"The vipo '{vid}' is not attached, but exists in the vipos list");
			}
			else
			{
				if (vipo.IsAttached)
					throw new KernelFaultException($"The vipo '{vid}' is attached, but doesn't exist in the vipos list");

				// Add to the vipos list
				if (!m_vipos.TryAdd(vid, vipo))
					throw new KernelFaultException($"Failed to add the vipo '{vid}' to the vipos list");

				vipo.IsAttached = true;
			}

			// Get or add a job
			var job = GetOrAddVipoJob(vipoJobs, vipo);
			var message = SystemMessageSchedule.Create(request.Context);
			job.AddMessage(message);
		}

		void ProcessRequest_VipoDetach(VipoDetach request)
		{
			var vipo = request.Vipo;
			var vid = vipo.Vid;

			if (!vipo.IsAttached)
			{
				if (m_vipos.ContainsKey(vid))
					throw new KernelFaultException($"The vipo '{vid}' to detach is not attached, but exists in the vipos list");

				return;
			}

			// Remove from the vipos list
			if (!m_vipos.TryRemove(vid, out Vipo removedVipo))
				throw new KernelFaultException($"Failed to remove the vipo '{vid}' from the vipos list");

			if (removedVipo != vipo)
				throw new KernelFaultException($"Unmatched vipo '{vid}' being detached");

			vipo.IsAttached = false;
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
