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
		readonly Dictionary<Vid, SortedRequestLink> m_requests = new Dictionary<Vid, SortedRequestLink>();
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
			public readonly Vid Vid;
			public readonly long DueTime;

			public Request(Vid vid, long dueTime)
			{
				Vid = vid;
				DueTime = dueTime;
			}
		}

		public void RequestToUpdateVipo(Vipo vipo, long dueTime)
		{
			CheckDisposed();

			var request = new Request(vipo.Vid, dueTime);

			m_requestQueue.Add(request);
		}

		public void RequestToResetVipo(Vipo vipo)
		{
			CheckDisposed();

			var request = new Request(vipo.Vid, 0);

			m_requestQueue.Add(request);
		}

		#endregion

		#region SortedRequestLink

		struct SortedRequestLink
		{
			public readonly long DueTime;
			public readonly LinkedListNode<Vid> Node;

			public SortedRequestLink(long dueTime, LinkedListNode<Vid> node)
			{
				DueTime = dueTime;
				Node = node;
			}
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
				if (m_requests.TryGetValue(request.Vid, out SortedRequestLink link))
				{
					m_requests.Remove(request.Vid);

					RemoveFromSorted(link);
				}
			}
			else
			{
				// Remove existed node
				if (m_requests.TryGetValue(request.Vid, out SortedRequestLink link))
				{
					if (link.DueTime == request.DueTime)
						return;

					RemoveFromSorted(link);
				}

				// Add a new node
				if (!m_sortedRequests.TryGetValue(request.DueTime, out LinkedList<Vid> vidList))
				{
					vidList = new LinkedList<Vid>();
					m_sortedRequests[request.DueTime] = vidList;
				}

				var node = vidList.AddLast(request.Vid);

				m_requests[request.Vid] = new SortedRequestLink(request.DueTime, node);
			}
		}

		void RemoveFromSorted(SortedRequestLink link)
		{
			if (!m_sortedRequests.TryGetValue(link.DueTime, out LinkedList<Vid> vidList))
				throw new KernelFaultException("No sorted request found to remove");

			vidList.Remove(link.Node);

			if (vidList.Count == 0)
				m_sortedRequests.Remove(link.DueTime);
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
