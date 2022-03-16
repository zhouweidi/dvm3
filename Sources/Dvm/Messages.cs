namespace Dvm
{
	public abstract class Message
	{
		public override string ToString() => GetType().Name;
	}

	#region System messages

	public abstract class SystemMessage : Message
	{
		protected static VipoMessage Create(SystemMessage message)
		{
			return new VipoMessage(Vid.Empty, Vid.Empty, message);
		}
	}

	public sealed class SystemMessageSchedule : SystemMessage
	{
		public object Context { get; private set; }

		SystemMessageSchedule(object context)
		{
			Context = context;
		}

		internal static VipoMessage Create(object context)
		{
			var message = new SystemMessageSchedule(context);

			return SystemMessage.Create(message);
		}
	}

	#endregion
}
