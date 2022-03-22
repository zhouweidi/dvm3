using System;

namespace Dvm
{
	public struct Vid : IFormattable, IComparable<Vid>
	{
		public static readonly Vid Empty = new Vid(0, null);

		readonly ulong m_data;
		readonly string m_symbol;

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
		public string Symbol => m_symbol;
		public bool IsEmpty => m_data == 0;

		internal ushort NodeId => (ushort)((m_data >> NodeIdBitOffset) & MaxNodeId);
		internal ulong Index => (m_data >> IndexBitOffset) & MaxIndex;

		#endregion

		#region Initialization

		internal Vid(ulong data, string symbol)
		{
			m_data = data;
			m_symbol = symbol ?? string.Empty;
		}

		internal Vid(ushort nodeId, ulong index, string symbol)
		{
			if (nodeId == 0 || nodeId > MaxNodeId)
				throw new ArgumentException("Invalid NodeId component for a Vid", nameof(nodeId));

			if (index == 0 || index > MaxIndex)
				throw new ArgumentException("Invalid index component for a Vid", nameof(index));

			m_data = (((ulong)nodeId) << NodeIdBitOffset) | (index << IndexBitOffset);
			m_symbol = symbol ?? string.Empty;
		}

		public override int GetHashCode()
		{
			return m_data.GetHashCode();
		}

		#endregion

		#region Equals

		public override bool Equals(Object obj)
		{
			if (!(obj is Vid))
				return false;

			return ((Vid)obj).Data == Data;
		}

		public static bool operator ==(Vid x, Vid y)
		{
			return x.Data == y.Data;
		}

		public static bool operator !=(Vid x, Vid y)
		{
			return x.Data != y.Data;
		}

		#endregion

		#region IComparable<T>

		public int CompareTo(Vid other)
		{
			return m_data.CompareTo(other.m_data);
		}

		#endregion

		#region Formatting

		public override string ToString() => ToString(null, null);

		public string ToString(string format, IFormatProvider provider = null)
		{
			if (Data == Empty.Data)
				return "0.0";

			switch (format)
			{
				case null:
				case "":
					if (string.IsNullOrEmpty(m_symbol))
						return m_data.ToString("X");
					else
						return $"{m_data:X}^{m_symbol}";

				case "detail":
					// <nodeId>.<index>^<symbol>
					return string.IsNullOrEmpty(m_symbol) ?
						$"{NodeId:X}.{Index:X}" :
						$"{NodeId:X}.{Index:X}^{m_symbol}";

				default:
					throw new FormatException($"The format string '{format}' is not supported.");
			}
		}

		#endregion
	}
}
