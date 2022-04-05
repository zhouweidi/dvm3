using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Dvm
{
	// Vipos having timers set are weak referenced by a VmTiming object.
	// They can be well-handled even they are disposed.
	class VmTiming : VmThread
	{
		readonly BlockingCollection<Request> m_requestQueue = new BlockingCollection<Request>();
		readonly Dictionary<Vid, Request> m_requests = new Dictionary<Vid, Request>();

		internal static long Now => Environment.TickCount64;

		#region Initialization

		public VmTiming(IVmThreadController controller)
			: base(controller, "DVM-Timer")
		{
		}

		protected override void OnDispose(bool explicitCall)
		{
			base.OnDispose(explicitCall);

			if (explicitCall)
				m_requestQueue.Dispose();
		}

		#endregion

		#region Request

		struct Request
		{
			public Vid Vid;
			public long DueTime;
		}

		public void RequestToUpdateVipo(Vipo vipo, long dueTime)
		{
			CheckDisposed();

			var request = new Request()
			{
				Vid = vipo.Vid,
				DueTime = dueTime,
			};

			m_requestQueue.Add(request);
		}

		public void RequestToResetVipo(Vipo vipo)
		{
			CheckDisposed();

			var request = new Request()
			{
				Vid = vipo.Vid,
			};

			m_requestQueue.Add(request);
		}

		#endregion

		#region Run

		protected override void ThreadEntry()
		{
			for (; ; )
			{
				if (m_requests.Count == 0)
				{
					var request = m_requestQueue.Take(EndToken);
					ProcessRequest(request);
				}
				else
				{
					var sortedRequests = SortRequests();

					var timeoutMilliseconds = sortedRequests[0].DueTime - Now;
					if (timeoutMilliseconds <= 0)
					{
						NotifyVipos(sortedRequests);
						continue;
					}

					if (timeoutMilliseconds > int.MaxValue)
						timeoutMilliseconds = int.MaxValue;

					var taken = m_requestQueue.TryTake(out Request request, (int)timeoutMilliseconds, EndToken);
					if (taken)
					{
						do
						{
							ProcessRequest(request);
						}
						while (m_requestQueue.TryTake(out request));
					}
					else
						NotifyVipos(sortedRequests);
				}
			}
		}

		void ProcessRequest(Request request)
		{
			if (request.DueTime == 0)
				m_requests.Remove(request.Vid);
			else
				m_requests[request.Vid] = request;
		}

		Request[] SortRequests()
		{
			return (from r in m_requests.Values
					orderby r.DueTime
					select r).ToArray();
		}

		void NotifyVipos(IEnumerable<Request> sortedRequests)
		{
			var now = Now;

			foreach (var request in sortedRequests)
			{
				if (request.DueTime > now)
					break;

				// Notify
				var vipo = request.Vid.ResolveVipo();
				if (vipo != null && !vipo.Disposed)
					vipo.InputMessage(SystemScheduleMessage.Timer.CreateVipoMessage());

				// Remove
				m_requests.Remove(request.Vid);
			}
		}

		#endregion
	}
}
