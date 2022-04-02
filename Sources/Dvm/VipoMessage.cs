using System;

namespace Dvm
{
	public struct VipoMessage : IFormattable
	{
		public Vid From { get; private set; }
		public Vid To { get; private set; }
		public Message Message { get; private set; }

		public readonly static VipoMessage Empty = new VipoMessage();

		internal VipoMessage(Vid from, Vid to, Message message)
		{
			From = from;
			To = to;
			Message = message ?? throw new ArgumentNullException(nameof(message));
		}

		#region Formatting

		public override string ToString() => ToString(null, null);

		public string ToString(string format, IFormatProvider provider = null)
		{
			switch (format)
			{
				case null:
				case "":
					return $"{From} -> {To}, {Message}";

				case "compact":
					return $"{From:compact} -> {To:compact}, {Message:compact}";

				default:
					throw new FormatException($"The format string '{format}' is not supported.");
			}
		}

		#endregion
	}
}
