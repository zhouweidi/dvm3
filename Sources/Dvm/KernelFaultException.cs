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

	public class VipoFaultException : KernelFaultException
	{
		public Vid Vid { get; private set; }

		public VipoFaultException(Vid vid)
		{
			Vid = vid;
		}

		public VipoFaultException(Vid vid, string message)
			: base(message)
		{
			Vid = vid;
		}
	}
}
