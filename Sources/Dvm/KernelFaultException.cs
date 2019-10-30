using System;

namespace Dvm
{
	// All errors that blocks scheduler execution
	public class KernelFaultException : Exception
	{
		public KernelFaultException()
		{
		}

		public KernelFaultException(string message)
			: base(message)
		{
		}
	}
}
