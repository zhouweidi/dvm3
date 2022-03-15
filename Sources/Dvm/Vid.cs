﻿using System;

namespace Dvm
{
	public struct Vid : IFormattable, IComparable<Vid>
	{
		public static readonly Vid Empty = new Vid(0, null);

		public const string FullFormat = "full";

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
		public bool IsEmpty => this == Empty;

		internal ushort NodeId => (ushort)((m_data >> NodeIdBitOffset) & MaxNodeId);
		internal ulong Index => (m_data >> IndexBitOffset) & MaxIndex;

		#endregion

		#region Initialize

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

		public override string ToString()
		{
			return ToString(null, null);
		}

		public string ToString(string format, IFormatProvider provider = null)
		{
			if (Data == Empty.Data)
				return "Empty";

			switch (format)
			{
				case null:
				case "":
					if (string.IsNullOrEmpty(m_symbol))
						return m_data.ToString("X");
					else
						return string.Format("{0}^{1}",
											  m_data.ToString("X"),
											  m_symbol);

				case FullFormat:
					// <nodeId>-<index>^<symbol>
					if (string.IsNullOrEmpty(m_symbol))
					{
						return string.Format("{0}-{1}",
											  Index.ToString("X"),
											  NodeId.ToString("X"));
					}
					else
					{
						return string.Format("{0}-{1}^{2}",
											  Index.ToString("X"),
											  NodeId.ToString("X"),
											  m_symbol);
					}

				default:
					throw new FormatException($"The {format} format string is not supported.");
			}
		}

		#endregion
	}
}
