﻿using System;
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
		CallState m_startCallState = CallState.NotRequested;
		CallState m_destroyCallState = CallState.NotRequested;
		SpinLock m_statesLock = new SpinLock();
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
						 m_startCallState = CallState.Requested;
						 break;

					 case CallState.Requested:
					 case CallState.Done:
						 throw new InvalidOperationException("Can only start an unstarted vipo");
				 }

				 switch (m_destroyCallState)
				 {
					 case CallState.NotRequested:
						 break;

					 case CallState.Requested:
					 case CallState.Done:
						 throw new KernelFault();
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
					 case CallState.Done:
						 break;
				 }

				 switch (m_destroyCallState)
				 {
					 case CallState.NotRequested:
						 m_destroyCallState = CallState.Requested;
						 break;

					 case CallState.Requested:
					 case CallState.Done:
						 return false; // Ignore destroy calls on a destroyed vipo
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
					 case CallState.Done:
						 break;
				 }

				 switch (m_destroyCallState)
				 {
					 case CallState.NotRequested:
						 break;

					 case CallState.Requested:
					 case CallState.Done:
						 return false; // Ignore schedule calls on a destroy-requested or destroyed vipo
				 }

				 return true;
			 });

			if (r)
				m_scheduler.AddScheduleTask(new VipoSchedule(this));
		}

		internal void Tick(TickTask tickTask)
		{
			try
			{
				// OnStart
				if (tickTask.StartRequest)
				{
					OnStart();

					m_startCallState = CallState.Done;
				}

				// OnTick
				if (!tickTask.AnyRequest && tickTask.Messages.Count == 0)
					throw new KernelFault("No message for tick while no special request set");

				OnTick(tickTask);

				// OnDestroy
				if (tickTask.DestroyRequest)
				{
					OnDestroy();

					m_destroyCallState = CallState.Done;
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

			if (message.To.IsEmpty)
				throw new InvalidOperationException("Can't send message to an empty vid");

			if (message.To == Vid)
				throw new InvalidOperationException("Can't send message to self");

			// No lock needed while running in VP thread
			switch (m_startCallState)
			{
				case CallState.NotRequested:
					throw new KernelFault();

				case CallState.Requested:
				case CallState.Done:
					break;
			}

			switch (m_destroyCallState)
			{
				case CallState.NotRequested:
					break;

				case CallState.Requested:
					throw new InvalidOperationException("Can't call SendMessage after Destroy called");

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

		#region Event handlers

		// All handlers are called on VirtualProcessor

		protected virtual void OnStart()
		{ }

		protected virtual void OnDestroy() // When it is being called, the vipo has already been removed from Scheduler::m_vipos
		{ }

		protected abstract void OnTick(TickTask tickTask);

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

				switch (m_startCallState)
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

				switch (m_destroyCallState)
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
