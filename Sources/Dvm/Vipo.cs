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
		CallbackOptions m_callbackOptions;
		CallState m_startCallState = CallState.NotRequested;
		CallState m_destroyCallState = CallState.NotRequested;
		SpinLock m_statesLock = new SpinLock();
		List<Message> m_outMessages;
		Exception m_exception;

		enum CallState
		{
			NotRequested, Requested, Callbacked
		}

		[Flags]
		internal protected enum CallbackOptions
		{
			None = 0,
			OnStart = 1,
			OnDestroy = 2,
			All = OnStart | OnDestroy,
		}

		protected Vipo(Scheduler scheduler, string name, CallbackOptions callbackOptions)
		{
			if (scheduler == null)
				throw new ArgumentNullException(nameof(scheduler));

			m_scheduler = scheduler;
			m_vid = scheduler.CreateVid(name);
			m_callbackOptions = callbackOptions;
		}

		#region Check states

		bool LockCallStates(Func<bool> check)
		{
			bool gotLock = false;
			try
			{
				m_statesLock.Enter(ref gotLock);

				return check();
			}
			finally
			{
				if (gotLock)
					m_statesLock.Exit();
			}
		}

		#endregion

		public void Start() // Can be called in any thread
		{
			var r = LockCallStates(() =>
			 {
				 switch (m_startCallState)
				 {
					 case CallState.NotRequested:
						 m_startCallState = HasCallbackOption(CallbackOptions.OnStart) ? CallState.Requested : CallState.Callbacked;
						 break;

					 case CallState.Requested:
					 case CallState.Callbacked:
						 throw new InvalidOperationException("Can't start a started vipo");
				 }

				 switch (m_destroyCallState)
				 {
					 case CallState.NotRequested:
						 break;

					 case CallState.Requested:
					 case CallState.Callbacked:
						 throw new VipoFaultException(m_vid, $"Unexpected {nameof(m_destroyCallState)} in Vipo.Start");
				 }

				 return true;
			 });

			if (r)
				m_scheduler.AddScheduleTask(new VipoStart(this));
		}

		public void Destroy() // Can be called in any thread
		{
			var r = LockCallStates(() =>
			 {
				 switch (m_startCallState)
				 {
					 case CallState.NotRequested:
						 throw new InvalidOperationException("Can't destroy an unstarted vipo");

					 case CallState.Requested:
					 case CallState.Callbacked:
						 break;
				 }

				 switch (m_destroyCallState)
				 {
					 case CallState.NotRequested:
						 m_destroyCallState = HasCallbackOption(CallbackOptions.OnDestroy) ? CallState.Requested : CallState.Callbacked;
						 break;

					 case CallState.Requested:
					 case CallState.Callbacked:
						 return false; // Ignore subsequent destroy calls on a requeted vipo
				 }

				 return true;
			 });

			if (r)
				m_scheduler.AddScheduleTask(new VipoDestroy(this));
		}

		public void Schedule() // Can be called in any thread
		{
			var r = LockCallStates(() =>
			 {
				 switch (m_startCallState)
				 {
					 case CallState.NotRequested:
						 throw new InvalidOperationException("Can't schedule an unstarted vipo");

					 case CallState.Requested:
					 case CallState.Callbacked:
						 break;
				 }

				 switch (m_destroyCallState)
				 {
					 case CallState.NotRequested:
						 break;

					 case CallState.Requested:
					 case CallState.Callbacked:
						 return false; // Ignore schedule calls on a destroy-requested or destroyed vipo
				 }

				 return true;
			 });

			if (r)
				m_scheduler.AddScheduleTask(new VipoSchedule(this));
		}

		internal void Tick(TickTask tickTask)
		{
			if (!tickTask.AnyRequest && tickTask.Messages.Count == 0)
				throw new VipoFaultException(m_vid, "No message/request to tick");

			bool isCallingDestroy = false;

			try
			{
				// OnStart
				if (tickTask.StartRequest)
				{
					OnStart();

					m_startCallState = CallState.Callbacked;
				}

				// OnTick
				OnTick(tickTask);

				// OnDestroy
				if (tickTask.DestroyRequest)
				{
					isCallingDestroy = true;
					OnDestroy();
					isCallingDestroy = false;

					m_destroyCallState = CallState.Callbacked;
				}
			}
			catch (Exception e)
			{
				m_exception = e;

				try
				{
					OnError(e);

					if (m_startCallState == CallState.Callbacked && m_destroyCallState != CallState.Callbacked && !isCallingDestroy)
					{
						OnDestroy();

						m_destroyCallState = CallState.Callbacked;
					}
				}
				catch
				{ }
			}
		}

		public void SendMessage(Message message) // Can be called only in VP thread
		{
			if (Scheduler.VirtualProcessor.GetTickingVid() != m_vid)
				throw new InvalidOperationException("It is not allowed to call SendMessage out of OnTick");

			if (message.To.IsEmpty)
				throw new ArgumentException("Can't SendMessage to an empty vid", nameof(message));

			if (message.To == Vid)
				throw new ArgumentException("Can't SendMessage to self", nameof(message));

			// No lock needed while running in VP thread
			switch (m_startCallState)
			{
				case CallState.NotRequested:
					throw new VipoFaultException(m_vid, $"Unexpected {nameof(m_startCallState)} in Vipo.SendMessage");

				case CallState.Requested:
				case CallState.Callbacked:
					break;
			}

			switch (m_destroyCallState)
			{
				case CallState.NotRequested:
					break;

				case CallState.Requested:
				case CallState.Callbacked:
					throw new InvalidOperationException("Can't call SendMessage after Destroy called");
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

		internal protected bool HasCallbackOption(CallbackOptions option)
		{
			return (m_callbackOptions & option) != 0;
		}

		#region Event handlers

		// All handlers are called on VirtualProcessor

		protected virtual void OnStart()
		{ }

		// It's called, when:
		//	1. The vipo is being destroied, and it has already been removed from Scheduler::m_vipos.
		//	2. An exception occured in OnStart or OnTick. If OnDestroy raises another exception, it will be ignored and the vipo continues to be destroyied.
		// Normally, OnDestroy should not raise any exception.
		protected virtual void OnDestroy()
		{ }

		protected abstract void OnTick(TickTask tickTask);

		// It's called when OnStart, OnDestroy or OnTick raises an exception.
		// If OnError raises another exception, it will be ignored and OnDestroy will be skipped neither.
		// Normally, OnError should not raise any exception.
		protected virtual void OnError(Exception e)
		{
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
			get { return m_vid.Name; }
		}

		public Exception Exception
		{
			get { return m_exception; }
		}

		public VipoStage Stage
		{
			get
			{
				VipoStage vs;

				switch (m_startCallState)
				{
					case CallState.NotRequested:
					default:
						vs = VipoStage.NotStarted;
						break;

					case CallState.Requested:
						vs = VipoStage.StartRequested;
						break;

					case CallState.Callbacked:
						vs = VipoStage.Running;
						break;
				}

				switch (m_destroyCallState)
				{
					case CallState.NotRequested:
						break;

					case CallState.Requested:
						vs = VipoStage.DestroyRequested;
						break;

					case CallState.Callbacked:
						vs = VipoStage.Destroyed;
						break;
				}

				return vs;
			}
		}

		#endregion
	}
}
