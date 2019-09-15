class Scheduler
{
	Dictionary<Vid, Vipo> m_vipos = new();
	List<Vipo, Message> m_tickQueue = new();
	
	public Scheduler()
	{
		CreateTickThreads();
	}
	
	void TickThread()
	{
		for( var vipo = m_tickQueue.WaitForDequeue(); vipo != null; vipo = m_tickQueue.WaitForDequeue() )
		{
			lock( m_vipos )
			{
				if( !m_vipos.ContainsKey( vipo ) )
					m_vipos.Add( vipo.Id, vipo );
			}
				
			vipo.Tick();
			
			// 考虑在vipo tick完成后，做一个sort（依据message.to），或在Vipo.SendMessage中作排序性插入；
			// 若有序，这里可以使用Vipo lastHint
			foreach( from m in vipo.OutgoingMessages 
					 groupby m.To
					 select g )
			{
				var targetVid = g.Key;
				var messages = g.Value;

				Vipo targetVipo;
				lock( m_vipos )
					m_vipos.TryGet( targetVid, out targetVipo );

				if( targetVipo == null )
				{
					Log( "Unreachable message" );
					continue;
				}
				
				Activate( targetVipo, g.Value );
			}
			
			if( vipo.IsEnded )
			{
				lock( m_vipos )
					m_vipos.Remove( vipo.Id );
			}
		}
	}

	void Activate(Vipo vipo, Message[] messages)
	{
		m_tickQueue.Enqueue( vipo, messages );
	}
}

class TickQueue
{
	// 实现一个轻量级的信号量（interlocked + SlimEvent）
	
	// 碎进整出：vipo及其零散的messages => vipo及其所有message
	
	public void EnqueueNewVipo(Vipo vipo)
	{
		Enqueue( vipo, null );
	}

	public void Enqueue(Vipo vipo, Message[] messages)
	{
		
	}
	
	public Vipo WaitForDequeue()
	{
		var vipo, messages = InternalWaitForDequeue();
		
		vipo.SetInMessages( messages );

		return vipo;
	}
}