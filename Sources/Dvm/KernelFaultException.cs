using System;

namespace Dvm
{
	public sealed class KernelFaultException : Exception
	{
		public KernelFaultException(string message)
			: base(message)
		{
		}

		public KernelFaultException(string message, Exception innerException)
			: base(message, innerException)
		{
		}
	}
}
