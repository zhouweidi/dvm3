using System;

namespace Dvm
{
	public struct Vid : IFormattable, IComparable<Vid>
	{
		public static readonly Vid Empty = new Vid(0, null);

		public const string
			ShortFormat = "s",
			FullFormat = "f";

		readonly ulong m_data;
		readonly string m_description;

		#region Bits schema

		// Cid: 2 bytes | Index: 6 bytes

		// Field bytes
		const int
			CidBits = 2 * 8,
			IndexBits = 6 * 8;

		// Bits offsets
		const int
			CidBitOffset = IndexBits,
			IndexBitOffset = 0;

		// Field masks
		internal const ushort MaxCid = (ushort)(((ulong)1 << CidBits) - 1);
		internal const ulong MaxIndex = ((ulong)1 << IndexBits) - 1;

		#endregion

		#region Initialize

		Vid(ulong data, string description)
		{
			m_data = data;
			m_description = description;
		}

		internal Vid(ushort cid, ulong index, string description)
		{
			if (cid == 0 || cid > MaxCid)
				throw new ArgumentException("Invalid Cid component for a Vid", nameof(cid));

			if (index == 0 || index > MaxIndex)
				throw new ArgumentException("Invalid index component for a Vid", nameof(index));

			m_data = (((ulong)cid) << CidBitOffset) | (((ulong)index) << IndexBitOffset);
			m_description = description;
		}

		internal static ulong GetNextIndex(ref long index)
		{
			++index;

			if (index > (long)MaxIndex || index <= 0)
				index = 1;

			return (ulong)index;
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
			return ToString(ShortFormat, null);
		}

		public string ToString(string format, IFormatProvider provider = null)
		{
			if (Data == Empty.Data)
				return "Empty";

			switch (format)
			{
				case null:
				case ShortFormat:
					{
						if (string.IsNullOrEmpty(m_description))
							return m_data.ToString("X");
						else
							return string.Format("{0}^{1}",
												  m_data.ToString("X"),
												  m_description);

					}

				case FullFormat:
					{
						// <cid>-<index>^<description>
						if (string.IsNullOrEmpty(m_description))
						{
							return string.Format("{0}-{1}",
												  Index.ToString("X"),
												  Cid.ToString("X"));
						}
						else
						{
							return string.Format("{0}-{1}^{2}",
												  Index.ToString("X"),
												  Cid.ToString("X"),
												  m_description);
						}
					}

				default:
					throw new FormatException($"The {format} format string is not supported.");
			}
		}

		#endregion

		#region Properties

		public ulong Data
		{
			get { return m_data; }
		}

		internal ushort Cid
		{
			get { return (ushort)((m_data >> CidBitOffset) & MaxCid); }
		}

		internal ulong Index
		{
			get { return (ulong)((m_data >> IndexBitOffset) & MaxIndex); }
		}

		public string Description
		{
			get { return m_description; }
		}

		public bool IsEmpty
		{
			get { return this == Empty; }
		}

		#endregion
	}
}
