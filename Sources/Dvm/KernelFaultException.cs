using System;

namespace Dvm
{
	public class KernelFaultException : Exception
	{
		public KernelFaultException(string message)
			: base(message)
		{
		}
	}
}
