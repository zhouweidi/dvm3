namespace Dvm
{
	public abstract class Message
	{
		public override string ToString() => $"{GetType().Name} {{{BodyToString()}}}";
		protected virtual string BodyToString() => "";
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

		internal static VipoMessage Create(object context)
		{
			var message = new SystemMessageSchedule(context);

			return SystemMessage.Create(message);
		}

		SystemMessageSchedule(object context)
		{
			Context = context;
		}

		protected override string BodyToString() => Context == null ? "" : Context.ToString();
	}

	#endregion
}
