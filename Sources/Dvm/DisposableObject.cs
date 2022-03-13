using System;
using System.Collections.Generic;
using System.Threading;

namespace Dvm
{
	public abstract class DisposableObject : IDisposable
	{
		volatile int m_disposed;

		public bool Disposed => m_disposed != 0;

		~DisposableObject()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		void Dispose(bool explicitCall)
		{
			int disposed = Interlocked.CompareExchange(ref m_disposed, 1, 0);
			if (disposed == 0)
			{
				OnDispose(explicitCall);

				if (explicitCall)
					GC.SuppressFinalize(this);
			}
		}

		protected abstract void OnDispose(bool explicitCall);

		protected void CheckDisposed()
		{
			if (Disposed)
				throw new ObjectDisposedException(GetType().Name);
		}

		#region Static helpers

		public static bool SafeDispose<T>(T obj)
			where T : IDisposable
		{
			if (obj != null)
			{
				obj.Dispose();
				return true;
			}

			return false;
		}

		public static bool SafeDispose<T>(ref T obj)
			where T : IDisposable
		{
			if (obj != null)
			{
				obj.Dispose();
				obj = default;

				return true;
			}

			return false;
		}

		public static bool TryDispose(object obj)
		{
			if (obj is IDisposable d)
			{
				d.Dispose();
				return true;
			}

			return false;
		}

		public static bool TryDispose<T>(ref T obj)
		{
			if (obj is IDisposable d)
			{
				d.Dispose();
				obj = default;

				return true;
			}

			return false;
		}

		public static void SafeDispose<T>(IEnumerable<T> objects)
			where T : IDisposable
		{
			if (objects != null)
			{
				foreach (var obj in objects)
				{
					if (obj != null)
						obj.Dispose();
				}
			}
		}

		public static void TryDispose<T>(IEnumerable<T> objects)
		{
			if (objects != null)
			{
				foreach (var obj in objects)
				{
					if (obj is IDisposable d)
						d.Dispose();
				}
			}
		}

		#endregion
	}
}
