using System;

namespace Dvm
{
	public struct VipoMessage
	{
		public Vid From { get; private set; }
		public Vid To { get; private set; }
		public Message Message { get; private set; }

		public VipoMessage(Vid from, Vid to, Message message)
		{
			From = from;
			To = to;
			Message = message ?? throw new ArgumentNullException(nameof(message));
		}

		public override string ToString()
		{
			return $"{From}, {To}, {Message}";
		}
	}
}
