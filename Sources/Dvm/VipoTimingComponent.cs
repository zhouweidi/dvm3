﻿using System;
using System.Collections.Generic;

namespace Dvm
{
	class VipoTimingComponent
	{
		readonly Vipo m_vipo;
		readonly List<Timer> m_timers = new List<Timer>();
		int m_latestTimerId;
		long m_lastRequestedDueTime;

		public VipoTimingComponent(Vipo vipo)
		{
			m_vipo = vipo;
		}

		#region Timer

		class Timer
		{
			public readonly int Id;
			public readonly object Context;
			readonly int m_repeatingInterval;
			long m_dueTime;
			bool m_canDispose;

			public long DueTime => m_dueTime;
			public bool CanDispose => m_canDispose;

			public Timer(int id, long dueTime, int repeatingInterval, object context)
			{
				Id = id;
				Context = context;
				m_repeatingInterval = repeatingInterval;
				m_dueTime = dueTime;
			}

			public bool Trigger(long now)
			{
				if (now < m_dueTime)
					return false;

				if (m_repeatingInterval > 0)
					m_dueTime = now + m_repeatingInterval;
				else
					m_canDispose = true;

				return true;
			}
		}

		#endregion

		#region Create/DestroyTimer

		public int CreateTimer(int milliseconds, int intervalMilliseconds, object context)
		{
			var timer = new Timer(
				++m_latestTimerId,
				VmTiming.Now + milliseconds,
				intervalMilliseconds,
				context);

			m_timers.Add(timer);

			return timer.Id;
		}

		public void DestroyTimer(int id)
		{
			for (int i = 0; i < m_timers.Count; i++)
			{
				if (m_timers[i].Id == id)
				{
					m_timers.RemoveAt(i);
					return;
				}
			}
		}

		#endregion

		#region Run

		public IVipoMessageStream TriggerTimers()
		{
			if (m_timers.Count == 0)
				return null;

			List<UserTimerMessage> timerMessages = null;
			{
				var now = VmTiming.Now;

				for (int i = 0; i < m_timers.Count; i++)
				{
					var timer = m_timers[i];

					if (timer.Trigger(now))
					{
						if (timerMessages == null)
							timerMessages = new List<UserTimerMessage>();

						var message = new UserTimerMessage(timer.Id, timer.Context);
						timerMessages.Add(message);
					}
				}
			}

			return timerMessages != null ?
				new VipoTimerMessageStream(timerMessages, this) :
				null;
		}

		public void UpdateRequest()
		{
			if (m_timers.Count > 0)
			{
				Timer nearestTimer = null;

				m_timers.RemoveAll(timer =>
				{
					if (timer.CanDispose)
						return true;

					if (nearestTimer == null || timer.DueTime < nearestTimer.DueTime)
						nearestTimer = timer;

					return false;
				});

				if (nearestTimer != null)
				{
					if (m_lastRequestedDueTime == 0 || m_lastRequestedDueTime != nearestTimer.DueTime)
					{
						m_vipo.VM.Timing.RequestToUpdateVipo(m_vipo, nearestTimer.DueTime);
						m_lastRequestedDueTime = nearestTimer.DueTime;
					}

					return;
				}
			}

			if (m_lastRequestedDueTime != 0)
			{
				m_vipo.VM.Timing.RequestToResetVipo(m_vipo);
				m_lastRequestedDueTime = 0;
			}
		}

		#region VipoTimerMessageStream

		class VipoTimerMessageStream : IVipoMessageStream
		{
			readonly IReadOnlyList<UserTimerMessage> m_timerMessages;
			readonly VipoTimingComponent m_timing;
			int m_index;

			public VipoTimerMessageStream(IReadOnlyList<UserTimerMessage> timerMessages, VipoTimingComponent timing)
			{
				m_timerMessages = timerMessages;
				m_timing = timing;
			}

			bool IVipoMessageStream.GetNext(out VipoMessage vipoMessage)
			{
			Reload:
				if (m_index < m_timerMessages.Count)
				{
					var message = m_timerMessages[m_index];
					m_index++;

					if (!TimerExists(message.TimerId))
						goto Reload;

					vipoMessage = message.CreateVipoMessage();
					return true;
				}
				else
				{
					vipoMessage = VipoMessage.Empty;
					return false;
				}
			}

			IEnumerable<VipoMessage> IVipoMessageStream.GetConsumingEnumerable()
			{
				for (; m_index < m_timerMessages.Count; m_index++)
				{
					var message = m_timerMessages[m_index];

					if (!TimerExists(message.TimerId))
						continue;

					yield return message.CreateVipoMessage();
				}
			}

			bool TimerExists(int timerId)
			{
				for (int i = 0; i < m_timing.m_timers.Count; i++)
				{
					if (m_timing.m_timers[i].Id == timerId)
						return true;
				}

				return false;
			}
		}

		#endregion

		#endregion
	}
}
