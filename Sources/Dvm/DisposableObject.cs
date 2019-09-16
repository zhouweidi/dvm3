using System;
using System.Threading;
using System.Collections.Generic;

namespace Dvm
{
	public class DisposableObject : IDisposable
	{
		volatile int m_disposed;

		~DisposableObject()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		public bool Disposed
		{
			get { return m_disposed != 0; }
		}

		void Dispose(bool callFromDisposeMethod)
		{
			int disposed = Interlocked.CompareExchange(ref m_disposed, 1, 0);
			if (disposed == 0)
			{
				DisposeUnmanaged();

				if (callFromDisposeMethod)
				{
					DisposeManaged();

					GC.SuppressFinalize(this);
				}
			}
		}

		protected virtual void DisposeManaged()
		{ }

		protected virtual void DisposeUnmanaged()
		{ }

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
				obj = default(T);

				return true;
			}

			return false;
		}

		public static bool TrySafeDispose<T>(T obj)
		{
			var d = obj as IDisposable;
			if (d != null)
			{
				d.Dispose();
				return true;
			}

			return false;
		}

		public static bool TrySafeDispose<T>(ref T obj)
		{
			var d = obj as IDisposable;
			if (d != null)
			{
				d.Dispose();
				obj = default(T);

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

		public static void TrySafeDispose<T>(IEnumerable<T> objects)
		{
			if (objects != null)
			{
				foreach (var obj in objects)
				{
					var d = obj as IDisposable;
					if (d != null)
						d.Dispose();
				}
			}
		}

		#endregion
	}
}
