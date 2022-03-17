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
