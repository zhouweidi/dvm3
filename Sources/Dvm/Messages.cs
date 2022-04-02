using System;

namespace Dvm
{
	public abstract class Message : IFormattable
	{
		#region Formatting

		public override string ToString() => ToString(null, null);

		public string ToString(string format, IFormatProvider provider = null)
		{
			switch (format)
			{
				case null:
				case "":
					return GetType().Name;

				case "detail":
					return $"{GetType().Name} {{{BodyToString()}}}";

				default:
					throw new FormatException($"The format string '{format}' is not supported.");
			}
		}

		protected virtual string BodyToString() => "";

		#endregion
	}

	#region System messages

	public abstract class SystemMessage : Message
	{
		internal VipoMessage CreateVipoMessage()
		{
			return new VipoMessage(Vid.Empty, Vid.Empty, this);
		}
	}

	sealed class SystemScheduleMessage : SystemMessage
	{
		internal static readonly SystemScheduleMessage Dispose = new SystemScheduleMessage();
		internal static readonly SystemScheduleMessage Timer = new SystemScheduleMessage();
	}

	public sealed class UserScheduleMessage : SystemMessage
	{
		public object Context { get; private set; }

		UserScheduleMessage(object context)
		{
			Context = context;
		}

		protected override string BodyToString() => Context == null ? "" : Context.ToString();

		static readonly VipoMessage Empty = new UserScheduleMessage(null).CreateVipoMessage();

		internal static VipoMessage CreateVipoMessage(object context)
		{
			return context == null ?
				Empty :
				new UserScheduleMessage(context).CreateVipoMessage();
		}
	}

	public sealed class UserTimerMessage : SystemMessage
	{
		public int TimerId { get; private set; }
		public object Context { get; private set; }

		internal UserTimerMessage(int timerId, object context)
		{
			TimerId = timerId;
			Context = context;
		}

		protected override string BodyToString() => 
			Context == null ? 
			TimerId.ToString() : 
			$"{TimerId}, {Context}";
	}

	#endregion
}
