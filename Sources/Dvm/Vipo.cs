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
		readonly VirtualMachine m_vm;
		readonly Vid m_vid;
		readonly CallbackOptions m_callbackOptions;
		CallState m_startCallState = CallState.NotRequested;
		CallState m_destroyCallState = CallState.NotRequested;
		SpinLock m_statesLock = new SpinLock();
		List<VipoMessage> m_outMessages;
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

		protected Vipo(VirtualMachine vm, string symbol, CallbackOptions callbackOptions)
		{
			m_vm = vm ?? throw new ArgumentNullException(nameof(vm));
			m_vid = vm.VidAllocator.New(symbol);
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
				m_vm.AddScheduleRequest(new VipoStart(this));
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
				m_vm.AddScheduleRequest(new VipoDestroy(this));
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
				m_vm.AddScheduleRequest(new VipoSchedule(this));
		}

		internal void Tick(VipoJob job)
		{
			if (!job.AnyRequest && job.Messages.Count == 0)
				throw new VipoFaultException(m_vid, "No message/request to tick");

			bool isCallingDestroy = false;

			try
			{
				// OnStart
				if (job.StartRequest)
				{
					OnStart();

					m_startCallState = CallState.Callbacked;
				}

				// OnTick
				OnTick(job);

				// OnDestroy
				if (job.DestroyRequest)
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

		public void Send(Vid to, Message body) // Can be called only in VP thread
		{
			Send(new VipoMessage(m_vid, to, body));
		}

		public void Send(VipoMessage message) // Can be called only in VP thread
		{
			if (!ReferenceEquals(VmProcessor.GetWorkingVipo(), this))
				throw new InvalidOperationException("It is not allowed to call Send outside of OnTick");

			if (message.From.IsEmpty)
				throw new ArgumentException("The 'From' of a VipoMessage passed to Send must be non-null", nameof(message));

			if (message.From != m_vid)
				throw new ArgumentException("The 'From' of a VipoMessage passed to Send must be sender self", nameof(message));

			if (message.To.IsEmpty)
				throw new ArgumentException("Can't Send to an empty vid", nameof(message));

			if (message.To == m_vid)
				throw new ArgumentException("Can't Send to self", nameof(message));

			if (message.Body == null)
				throw new ArgumentException("The body of a VipoMessage passed to Send is null", nameof(message));

			// No lock needed while running in VP thread
			switch (m_startCallState)
			{
				case CallState.NotRequested:
					throw new VipoFaultException(m_vid, $"Unexpected {nameof(m_startCallState)} in Vipo.Send");

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
					throw new InvalidOperationException("Can't call Send after Destroy called");
			}

			if (m_outMessages == null)
				m_outMessages = new List<VipoMessage>();

			m_outMessages.Add(message);
		}

		internal IReadOnlyList<VipoMessage> TakeOutMessages()
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

		protected abstract void OnTick(VipoJob job);

		// It's called when OnStart, OnDestroy or OnTick raises an exception.
		// If OnError raises another exception, it will be ignored and OnDestroy will be skipped neither.
		// Normally, OnError should not raise any exception.
		protected virtual void OnError(Exception e)
		{
		}

		#endregion

		#region Properties

		public VirtualMachine VM
		{
			get { return m_vm; }
		}

		public Vid Vid
		{
			get { return m_vid; }
		}

		public string Symbol
		{
			get { return m_vid.Symbol; }
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
