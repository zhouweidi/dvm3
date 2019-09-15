class Vipo
{
	public void SetInMessages(IEnumerable<Message> messages)
	{
		m_inMessages = messages;
	}
	
	
	public void Send(Message m)
	{
		if( m.To == Id )
			throw e;
	}
}
