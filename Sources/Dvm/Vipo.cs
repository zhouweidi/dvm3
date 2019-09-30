using System;
using System.Collections.Generic;
using System.Threading;

namespace Dvm
{
	public enum VipoStage
	{
		NotStarted, StartRequested, Running, DestroyRequested, Destroyed
	}

	public abstract class Vipo
	{
		readonly Scheduler m_scheduler;
		readonly Vid m_vid;
		CallState m_startCall = CallState.NotRequested;
		CallState m_destroyCall = CallState.NotRequested;
		SpinLock m_stateLock = new SpinLock();
		List<Message> m_outMessages;

		enum CallState
		{
			NotRequested, Requested, Done
		}

		protected Vipo(Scheduler scheduler, string name)
		{
			m_scheduler = scheduler;
			m_vid = scheduler.CreateVid(name);
		}

		public void Start() // Can be called in any thread
		{
			CheckStates(() =>
			{
				switch (m_startCall)
				{
					case CallState.NotRequested:
						m_startCall = CallState.Requested;
						break;

					case CallState.Requested:
					case CallState.Done:
						throw new InvalidOperationException("Can only start an unstarted vipo");
				}

				switch (m_destroyCall)
				{
					case CallState.NotRequested:
						break;

					case CallState.Requested:
					case CallState.Done:
						throw new KernelFault();
				}

				m_scheduler.AddScheduleTask(new VipoStart(this));
			});
		}

		public void Destroy() // Can be called in any thread
		{
			CheckStates(() =>
			{
				switch (m_startCall)
				{
					case CallState.NotRequested:
						throw new InvalidOperationException("Can't destroy an unstarted vipo");

					case CallState.Requested:
					case CallState.Done:
						break;
				}

				switch (m_destroyCall)
				{
					case CallState.NotRequested:
						m_destroyCall = CallState.Requested;
						break;

					case CallState.Requested:
					case CallState.Done:
						return; // Ignore destroy calls on a destroyed vipo
				}

				m_scheduler.AddScheduleTask(new VipoDestroy(this));
			});
		}

		public void Schedule() // Can be called in any thread
		{
			CheckStates(() =>
			{
				switch (m_startCall)
				{
					case CallState.NotRequested:
						throw new InvalidOperationException("Can't schedule an unstarted vipo");

					case CallState.Requested:
					case CallState.Done:
						break;
				}

				switch (m_destroyCall)
				{
					case CallState.NotRequested:
						break;

					case CallState.Requested:
					case CallState.Done:
						return; // Ignore schedule calls on a destroy-requested or destroyed vipo
				}

				m_scheduler.AddScheduleTask(new VipoSchedule(this));
			});
		}

		internal void Tick(TickTask tickTask)
		{
			try
			{
				// OnStart
				if (tickTask.StartRequest)
				{
					OnStart();

					m_startCall = CallState.Done;
				}

				// OnTick
				if (!tickTask.StartRequest && tickTask.Messages.Count == 0)
					throw new KernelFault();

				OnTick(tickTask.Messages);

				// OnDestroy
				if (tickTask.DestroyRequest)
				{
					OnDestroy();

					m_destroyCall = CallState.Done;
				}
			}
			catch (Exception e) when (OnError(e))
			{
			}
		}

		public void SendMessage(Message message) // Can be called only in VP thread
		{
			if (Scheduler.VirtualProcessor.GetTickingVid() != m_vid)
				throw new InvalidOperationException("SendMessage is only allowed to call in OnTick");

			switch (m_startCall)
			{
				case CallState.NotRequested:
					throw new KernelFault();

				case CallState.Requested:
				case CallState.Done:
					break;
			}

			switch (m_destroyCall)
			{
				case CallState.NotRequested:
				case CallState.Requested:
					break;

				case CallState.Done:
					throw new KernelFault();
			}

			if (m_outMessages == null)
				m_outMessages = new List<Message>();

			m_outMessages.Add(message);
		}

		internal IReadOnlyList<Message> TakeOutMessages()
		{
			if (m_outMessages == null || m_outMessages.Count == 0)
				return null;

			var outMessages = m_outMessages;

			m_outMessages = null;

			return outMessages;
		}

		public override string ToString()
		{
			return "Vipo " + m_vid.ToString();
		}

		#region Check states

		void CheckStates(Action check)
		{
			bool gotLock = false;
			try
			{
				m_stateLock.Enter(ref gotLock);

				check();
			}
			finally
			{
				if (gotLock)
					m_stateLock.Exit();
			}
		}

		#endregion

		#region Handler

		// All handlers are called on VirtualProcessor

		protected virtual void OnStart()
		{ }

		protected virtual void OnDestroy()
		{ }

		protected abstract void OnTick(IReadOnlyList<Message> messages);

		protected virtual bool OnError(Exception e)
		{
			return false;
		}

		#endregion

		#region Properties

		public Scheduler Scheduler
		{
			get { return m_scheduler; }
		}

		public Vid Vid
		{
			get { return m_vid; }
		}

		public string Name
		{
			get { return m_vid.Description; }
		}

		public VipoStage Stage
		{
			get
			{
				VipoStage vs;

				switch (m_startCall)
				{
					case CallState.NotRequested:
					default:
						vs = VipoStage.NotStarted;
						break;

					case CallState.Requested:
						vs = VipoStage.StartRequested;
						break;

					case CallState.Done:
						vs = VipoStage.Running;
						break;
				}

				switch (m_destroyCall)
				{
					case CallState.NotRequested:
						break;

					case CallState.Requested:
						vs = VipoStage.DestroyRequested;
						break;

					case CallState.Done:
						vs = VipoStage.Destroyed;
						break;
				}

				return vs;
			}
		}

		#endregion
	}
}
