using System;

namespace Dvm
{
	public class Vid : IEquatable<Vid>, IComparable<Vid>, IFormattable
	{
		public static readonly Vid Empty = new Vid();

		readonly ulong m_data;
		readonly WeakReference<Vipo> m_vipoRef;

		#region Bits schema

		// NodeId: 2 bytes | Index: 6 bytes

		// Field bytes
		const int
			NodeIdBits = 2 * 8,
			IndexBits = 6 * 8;

		// Bits offsets
		const int
			NodeIdBitOffset = IndexBits,
			IndexBitOffset = 0;

		// Field masks
		internal const ushort MaxNodeId = (ushort)(((ulong)1 << NodeIdBits) - 1);
		internal const ulong MaxIndex = ((ulong)1 << IndexBits) - 1;

		#endregion

		#region Properties

		public ulong Data => m_data;
		public bool IsEmpty => m_data == 0;

		internal ushort NodeId => (ushort)((m_data >> NodeIdBitOffset) & MaxNodeId);
		internal ulong Index => (m_data >> IndexBitOffset) & MaxIndex;

		#endregion

		#region Initialization

		Vid()
		{
		}

		internal Vid(ushort nodeId, ulong index, Vipo vipo)
		{
			if (nodeId == 0 || nodeId > MaxNodeId)
				throw new ArgumentException("Invalid NodeId component for a Vid", nameof(nodeId));

			if (index == 0 || index > MaxIndex)
				throw new ArgumentException("Invalid index component for a Vid", nameof(index));

			m_data = (((ulong)nodeId) << NodeIdBitOffset) | (index << IndexBitOffset);
			m_vipoRef = vipo != null ? new WeakReference<Vipo>(vipo) : null;
		}

		public override int GetHashCode()
		{
			return m_data.GetHashCode();
		}

		#endregion

		#region Resolve

		internal Vipo ResolveVipo()
		{
			if (m_vipoRef == null)
				return null;

			if (!m_vipoRef.TryGetTarget(out Vipo vipo))
				return null;

			return vipo;
		}

		public string ResolveSymbol()
		{
			var vipo = ResolveVipo();

			return vipo != null ?
				vipo.Symbol :
				string.Empty;
		}

		#endregion

		#region Equals

		public bool Equals(Vid other)
		{
			return !(other is null) && other.m_data == m_data;
		}

		public override bool Equals(object obj)
		{
			if (obj is Vid vid)
				return vid.m_data == m_data;
			else
				return false;
		}

		public static bool operator ==(Vid x, Vid y)
		{
			bool xIsNull = x is null;
			bool yIsNull = y is null;

			if (!xIsNull && !yIsNull)
				return x.m_data == y.m_data;
			else
				return xIsNull && yIsNull;
		}

		public static bool operator !=(Vid x, Vid y)
		{
			return !(x == y);
		}

		#endregion

		#region IComparable<T>

		public int CompareTo(Vid other)
		{
			return other is null ?
				1 :
				m_data.CompareTo(other.m_data);
		}

		#endregion

		#region Formatting

		public override string ToString() => ToString(null, null);

		public string ToString(string format, IFormatProvider provider = null)
		{
			if (m_data == 0)
				return "Empty";

			var symbol = ResolveSymbol();

			switch (format)
			{
				case null:
				case "":
					if (string.IsNullOrEmpty(symbol))
						return m_data.ToString("X");
					else
						return $"{m_data:X}^{symbol}";

				case "detail":
					// <nodeId>.<index>^<symbol>
					return string.IsNullOrEmpty(symbol) ?
						$"{NodeId:X}.{Index:X}" :
						$"{NodeId:X}.{Index:X}^{symbol}";

				default:
					throw new FormatException($"The format string '{format}' is not supported.");
			}
		}

		#endregion
	}
}
