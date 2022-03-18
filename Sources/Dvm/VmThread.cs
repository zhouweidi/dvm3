using System;
using System.Threading;

namespace Dvm
{
	interface IVmThreadController
	{
		CancellationToken EndToken { get; }

		void RequestToEnd();
		void HandleError(Exception exception);
	}

	abstract class VmThread : DisposableObject
	{
		readonly IVmThreadController m_controller;
		readonly Thread m_thread;

		protected CancellationToken EndToken => m_controller.EndToken;

		protected VmThread(IVmThreadController controller, string name)
		{
			m_controller = controller;

			m_thread = new Thread(ThreadRootEntry)
			{
				Name = name
			};

			m_thread.Start();
		}

		protected override void OnDispose(bool explicitCall)
		{
			m_controller.RequestToEnd();

			if (explicitCall)
				m_thread.Join();
		}

		void ThreadRootEntry()
		{
			try
			{
				ThreadEntry();
			}
			catch (OperationCanceledException ex) when (ex.CancellationToken == EndToken)
			{
				// Update VM.State
				m_controller.RequestToEnd();
			}
			catch (Exception ex)
			{
				m_controller.HandleError(ex);
			}
		}

		protected abstract void ThreadEntry();
	}
}
