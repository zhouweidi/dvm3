using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;

namespace Dvm
{
	public enum VirtualMachineState
	{
		Running, Ending, Ended
	}

	public sealed class VirtualMachine : DisposableObject
	{
		readonly ThreadController m_controller;
		readonly Inspector m_inspector;
		readonly VmScheduler m_scheduler;
		readonly VmTiming m_timing;

		readonly ConcurrentDictionary<Vid, Vipo> m_vipos = new ConcurrentDictionary<Vid, Vipo>();
		readonly VidAllocator m_vidAllocator;

		#region Properties

		public VirtualMachineState State => m_controller.State;
		public Exception Exception => m_controller.Exception;
		public event Action<Exception> OnError // Be invoked when the first exception is raised.
		{
			add { m_controller.OnError += value; }
			remove { m_controller.OnError -= value; }
		}
		public int ProcessorsCount => m_scheduler.ProcessorsCount;
		public int ViposCount => m_vipos.Count;
		public Inspector Inspector => m_inspector;

		internal VmTiming Timing => m_timing;

		#endregion

		#region Initialization

		public VirtualMachine(int virtualProcessorsCount = 0, bool withInspector = false)
			: this(virtualProcessorsCount, CancellationToken.None, withInspector)
		{ }

		public VirtualMachine(int virtualProcessorsCount, CancellationToken endToken, bool withInspector)
		{
			m_controller = new ThreadController(endToken);

			if (withInspector)
				m_inspector = new Inspector();

			if (virtualProcessorsCount == 0)
				virtualProcessorsCount = Math.Max(Environment.ProcessorCount - 1, 1);

			m_scheduler = new VmScheduler(m_controller, virtualProcessorsCount, m_inspector);
			m_scheduler.Start();

			m_timing = new VmTiming(m_controller);

			m_vidAllocator = new VidAllocator(new UsedVidQuery(this));
		}

		protected override void OnDispose(bool explicitCall)
		{
			m_controller.RequestToEnd();

			if (explicitCall)
			{
				m_scheduler.Dispose();

				m_timing.Dispose();

				m_controller.Dispose();

				if (m_vipos.Count > 0)
				{
					foreach (var vipo in m_vipos.Values)
						vipo.Dispose();

					m_vipos.Clear();
				}
			}
		}

		#endregion

		#region ThreadController

		class ThreadController : DisposableObject, IVmThreadController
		{
			readonly CancellationTokenSource m_endSource;

			volatile int m_state = (int)VirtualMachineState.Running;
			public event Action<Exception> OnError;
			volatile Exception m_exception;

			#region Properties

			public VirtualMachineState State => (VirtualMachineState)m_state;
			public Exception Exception => m_exception;

			#endregion

			public ThreadController(CancellationToken externalEndToken)
			{
				m_endSource = CancellationTokenSource.CreateLinkedTokenSource(externalEndToken);
			}

			protected override void OnDispose(bool explicitCall)
			{
				if (explicitCall)
				{
					m_state = (int)VirtualMachineState.Ended;

					m_endSource.Dispose();
				}
			}

			#region IVmThreadController

			CancellationToken IVmThreadController.EndToken => m_endSource.Token;

			void IVmThreadController.HandleError(Exception exception)
			{
				if (exception == null)
					throw new ArgumentNullException(nameof(exception));

				if (Interlocked.CompareExchange(ref m_exception, exception, null) == null)
				{
					OnError?.Invoke(exception);

					RequestToEnd();
				}
			}

			public void RequestToEnd()
			{
				var set = Interlocked.CompareExchange(
					ref m_state,
					(int)VirtualMachineState.Ending,
					(int)VirtualMachineState.Running) == (int)VirtualMachineState.Running;

				if (set)
					m_endSource.Cancel();
			}

			#endregion
		}

		#endregion

		#region UsedVidQuery

		struct UsedVidQuery : IUsedVidQuery
		{
			readonly VirtualMachine m_vm;

			public UsedVidQuery(VirtualMachine vm)
			{
				m_vm = vm;
			}

			bool IUsedVidQuery.IsUsed(Vid vid) => m_vm.m_vipos.ContainsKey(vid);
		}

		#endregion

		#region Vipos

		internal Vid Register(Vipo vipo)
		{
			Debug.Assert(vipo != null);

			if (vipo.Disposed)
				throw new KernelFaultException($"Register a disposed vipo '{vipo.Symbol}'");

			// Add to the vipos list
			var vid = m_vidAllocator.New(vipo);

			if (!m_vipos.TryAdd(vid, vipo))
				throw new KernelFaultException($"Failed to add the vipo '{vid}' to the vipos list");

			return vid;
		}

		internal void Unregister(Vipo vipo)
		{
			if (!vipo.Disposed)
				throw new KernelFaultException($"Unregister an undisposed vipo");

			// Remove from the vipos list
			var vid = vipo.Vid;

			if (!m_vipos.TryRemove(vid, out Vipo removedVipo))
				throw new KernelFaultException($"Failed to remove the vipo '{vid}' from the vipos list");

			if (!ReferenceEquals(removedVipo, vipo))
				throw new KernelFaultException($"Unmatched vipo '{vid}' being unregistered");
		}

		internal void Schedule(Vipo vipo)
		{
			m_scheduler.DispatchJob(vipo);
		}

		internal Vipo FindVipo(Vid vid)
		{
			m_vipos.TryGetValue(vid, out Vipo vipo);
			return vipo;
		}

		#endregion
	}
}
