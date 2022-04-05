using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Dvm
{
	// Vipos having timers set are weak referenced by a VmTiming object.
	// They can be well-handled after they are disposed.
	class VmTiming : VmThread
	{
		readonly BlockingCollection<Request> m_requestQueue = new BlockingCollection<Request>();
		readonly Dictionary<Vid, long> m_requests = new Dictionary<Vid, long>();
		readonly SortedDictionary<long, LinkedList<Vid>> m_sortedRequests = new SortedDictionary<long, LinkedList<Vid>>();

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
					var timeoutMilliseconds = m_sortedRequests.First().Key - Now;
					if (timeoutMilliseconds <= 0)
					{
						NotifyVipos();
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
						NotifyVipos();
				}
			}
		}

		void ProcessRequest(Request request)
		{
			if (request.DueTime == 0)
			{
				// Remove
				if (m_requests.TryGetValue(request.Vid, out long dueTime))
				{
					m_requests.Remove(request.Vid);

					RemoveFromSorted(dueTime, request.Vid);
				}
			}
			else
			{
				// Remove existed node
				if (m_requests.TryGetValue(request.Vid, out long dueTime))
				{
					if (dueTime == request.DueTime)
						return;

					RemoveFromSorted(dueTime, request.Vid);
				}

				// Add a new node
				if (!m_sortedRequests.TryGetValue(request.DueTime, out LinkedList<Vid> vidList))
				{
					vidList = new LinkedList<Vid>();
					m_sortedRequests[request.DueTime] = vidList;
				}

				vidList.AddLast(request.Vid);

				m_requests[request.Vid] = request.DueTime;
			}
		}

		void RemoveFromSorted(long dueTime, Vid vid)
		{
			var vidList = m_sortedRequests[dueTime];

			if (vidList.Remove(vid) && vidList.Count == 0)
				m_sortedRequests.Remove(dueTime);
		}

		void NotifyVipos()
		{
			var removeList = new List<long>();

			var now = Now;
			foreach (var item in m_sortedRequests)
			{
				if (item.Key > now)
					break;

				// Notify
				foreach (var vid in item.Value)
				{
					var vipo = vid.ResolveVipo();
					if (vipo != null && !vipo.Disposed)
						vipo.InputMessage(SystemScheduleMessage.Timer.CreateVipoMessage());

					// Remove
					m_requests.Remove(vid);
				}

				removeList.Add(item.Key);
			}

			// Remove
			foreach (var item in removeList)
				m_sortedRequests.Remove(item);
		}

		#endregion
	}
}
