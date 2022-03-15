using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Dvm
{
	// TODO 添加error state
	public enum VirtualMachineState
	{
		Running, EndRequested, End
	}

	public sealed class VirtualMachine : DisposableObject
	{
		readonly ThreadController m_controller;
		readonly VmScheduler m_scheduler;

		readonly ConcurrentDictionary<Vid, Vipo> m_vipos = new ConcurrentDictionary<Vid, Vipo>();
		readonly VidAllocator m_vidAllocator;

		#region Properties

		public VirtualMachineState State => m_controller.State;
		public Exception Exception => m_controller.Exception;
		public event Action<Exception> OnError
		{
			add { m_controller.OnError += value; }
			remove { m_controller.OnError -= value; }
		}
		public int ProcessorsCount => m_scheduler.Executor.ProcessorsCount;
		public int ViposCount => m_vipos.Count;
		internal VidAllocator VidAllocator => m_vidAllocator;

		#endregion

		#region Initialization

		public VirtualMachine(int virtualProcessorsCount = 0)
			: this(virtualProcessorsCount, CancellationToken.None)
		{ }

		public VirtualMachine(int virtualProcessorsCount, CancellationToken endToken)
		{
			m_controller = new ThreadController(endToken);

			if (virtualProcessorsCount == 0)
				virtualProcessorsCount = Environment.ProcessorCount;

			m_scheduler = new VmScheduler(m_controller, virtualProcessorsCount, m_vipos);

			m_vidAllocator = new VidAllocator(new UsedVidQuery(this));
		}

		protected override void OnDispose(bool explicitCall)
		{
			m_controller.RequestToEnd();

			if (explicitCall)
			{
				m_scheduler.Dispose();

				m_controller.Dispose();
			}
		}

		#endregion

		#region ThreadController

		class ThreadController : DisposableObject, IVmThreadController
		{
			readonly CancellationTokenSource m_endSource;

			int m_state = (int)VirtualMachineState.Running;
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
					m_state = (int)VirtualMachineState.End;

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
					OnError?.Invoke(exception);
			}

			public void RequestToEnd()
			{
				var set = Interlocked.CompareExchange(
					ref m_state,
					(int)VirtualMachineState.EndRequested,
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

		internal void AddScheduleRequest(ScheduleRequest request)
		{
			m_scheduler.AddRequest(request);
		}
	}
}
