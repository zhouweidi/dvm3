using System;

namespace Dvm
{
	public struct VipoMessage
	{
		public Vid From { get; private set; }
		public Vid To { get; private set; }
		public Message Body { get; private set; }

		internal VipoMessage(Message body) // For system messages
		{
			if (body == null)
				throw new ArgumentNullException(nameof(body));

			From = Vid.Empty;
			To = Vid.Empty;
			Body = body;
		}

		public VipoMessage(Vid from, Vid to, Message body)
		{
			if (body == null)
				throw new ArgumentNullException(nameof(body));

			From = from;
			To = to;
			Body = body;
		}

		public override string ToString()
		{
			return $"{From}, {To}, {Body.ToString(Message.FullFormat)}";
		}
	}

	public class Message : IFormattable
	{
		public const string FullFormat = "full";

		public override string ToString()
		{
			return "MessageBase";
		}

		public string ToString(string format, IFormatProvider provider = null)
		{
			switch (format)
			{
				case null:
				case "":
					return ToString();

				case FullFormat:
					return $"{GetType().Name} {{{ToString()}}}";

				default:
					throw new FormatException($"The {format} format string is not supported.");
			}
		}
	}

	#region System messages

	public static class SystemVipoMessages
	{
		public static readonly VipoMessage VipoSchedule = new VipoMessage(SystemMessage.VipoSchedule);
	}

	sealed class SystemMessage : Message
	{
		public static readonly SystemMessage VipoSchedule = new SystemMessage();

		public override string ToString()
		{
			if (ReferenceEquals(this, VipoSchedule))
				return nameof(VipoSchedule);
			else
				return "UNKNOWN_SYSTEM_MESSAGE";
		}
	}

	#endregion
}
