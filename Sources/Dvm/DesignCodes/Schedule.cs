class ScheduleEvent

class VipoInitialRun : ScheduleEvent
{
}

class VipoMessages : ScheduleEvent
{
	Vid TargetVid;
	Message[] Messages;
}

class Bus
{
	public void InputEvent(ScheduleEvent se);
	public IEnumerable<ScheduleEvent> GetEvents();
	public void WaitForEvents();
}

class Ticking
{
	Scheduler m_scheduler = ...;
	
	public Ticking()
	{
		CreateTickThreads();
	}
	
	void TickThread()
	{
		for( var vipo = m_scheduler.GetNextToTick(); vipo != null; vipo = m_scheduler.GetNextToTick() )
		{
			vipo.Tick();
			
			// 考虑在vipo tick完成后，做一个sort（依据message.to），或在Vipo.SendMessage中作排序性插入；
			// 若有序，这里可以使用Vipo lastHint
			foreach( from m in vipo.OutgoingMessages 
					 groupby m.To
					 select g )
			{
				var targetVid = g.Key;
				var messages = g.Value;

				m_scheduler.InputBus( new VipoMessages( m.To, g.Value ) );
			}				
		}
	}
}

class TickQueue
{
	// 实现一个轻量级的信号量（interlocked + SlimEvent）
	
	// 碎进整出：vipo及其零散的messages => vipo及其所有message
	
	public void Enqueue(Vipo vipo, Message[] messages)
	{
		
	}
	
	public Vipo, Message[] WaitForDequeue()
	{
		
	}
}

class Scheduler
{
	Bus m_bus = new();
	Dictionary<Vid, Vipo> m_vipos = new();

	List<Vipo, Message> m_tickQueue = new();
	
	public void Run()
	{
		for()
		{
			var events = m_bus.WaitForEvents();

			foreach( e in events )
			{
				switch( e )
				{
					case VipoInitialRun:
						Activate( e.Vipo, null );
						break;
						
					case VipoMessages:
						if( !m_vipos.ContainsKey( e.TargetVid ) )
						{
							Log( "Unreachable message" );
							continue;
						}

						var targetVipo = m_vipos[e.TargetVid];
						Activate( targetVipo, e.Messages );
						break;
				}
			}
		}
	}
	
	void Activate(Vipo vipo, Message[] messages)
	{
		m_tickQueue.Enqueue( vipo, messages );
	}

	internal Vipo GetNextToTick()
	{
		var vipo, messages = m_tickQueue.WaitForDequeue();
		
		vipo.SetInMessages( messages );

		return vipo;
	}
	
	public void InputBus(ScheduleEvent se)
	{
		m_bus.InputEvent(se);
	}
}