using System;
using System.Text;

namespace POSEIDON.Reckoning
{
    /// <summary>Thrown when saved bytes (a Log, a snapshot, a save) cannot be trusted.</summary>
    public sealed class ReckoningFormatException : Exception
    {
        public ReckoningFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// Writes values as bytes in one fixed layout: little-endian integers, doubles as their raw
    /// IEEE 754 bits. <see cref="BitConverter"/> follows the machine's endianness and
    /// BinaryWriter's string format is an implementation detail, so a Log meant to outlive this
    /// build spells out every byte here instead.
    /// </summary>
    public sealed class StateWriter
    {
        private byte[] _bytes;
        private int _length;

        public StateWriter(int initialCapacity = 256)
        {
            _bytes = new byte[Math.Max(initialCapacity, 16)];
        }

        public int Length => _length;

        public void WriteByte(byte value)
        {
            Reserve(1);
            _bytes[_length++] = value;
        }

        public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUShort(ushort value)
        {
            Reserve(2);
            _bytes[_length++] = (byte)value;
            _bytes[_length++] = (byte)(value >> 8);
        }

        public void WriteUInt(uint value)
        {
            Reserve(4);
            for (int shift = 0; shift < 32; shift += 8) _bytes[_length++] = (byte)(value >> shift);
        }

        public void WriteInt(int value) => WriteUInt(unchecked((uint)value));

        public void WriteULong(ulong value)
        {
            Reserve(8);
            for (int shift = 0; shift < 64; shift += 8) _bytes[_length++] = (byte)(value >> shift);
        }

        public void WriteLong(long value) => WriteULong(unchecked((ulong)value));

        /// <summary>Writes the raw bits, so -0.0 and every NaN pattern survive a round trip.</summary>
        public void WriteDouble(double value) => WriteLong(BitConverter.DoubleToInt64Bits(value));

        /// <summary>A length (int) followed by the bytes.</summary>
        public void WriteBytes(byte[] value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            WriteInt(value.Length);
            WriteRaw(value, 0, value.Length);
        }

        public void WriteString(string value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            WriteBytes(Encoding.UTF8.GetBytes(value));
        }

        public void WriteRaw(byte[] source, int offset, int count)
        {
            Reserve(count);
            Buffer.BlockCopy(source, offset, _bytes, _length, count);
            _length += count;
        }

        /// <summary>Appends an FNV-1a 64 of everything so far; <see cref="StateReader.ForChecked"/> verifies it.</summary>
        public void WriteChecksum() => WriteULong(FixHasher.Of(_bytes, 0, _length));

        public byte[] ToArray()
        {
            var result = new byte[_length];
            Buffer.BlockCopy(_bytes, 0, result, 0, _length);
            return result;
        }

        private void Reserve(int count)
        {
            int needed = _length + count;
            if (needed <= _bytes.Length) return;
            var grown = new byte[Math.Max(needed, _bytes.Length * 2)];
            Buffer.BlockCopy(_bytes, 0, grown, 0, _length);
            _bytes = grown;
        }
    }

    /// <summary>
    /// Reads what <see cref="StateWriter"/> wrote. Saved bytes are a trust boundary, so every read
    /// is bounds-checked and failure is a <see cref="ReckoningFormatException"/>, never a wrong
    /// number.
    /// </summary>
    public sealed class StateReader
    {
        private readonly byte[] _bytes;
        private readonly int _end;
        private int _position;

        public StateReader(byte[] bytes) : this(bytes, bytes?.Length ?? 0) { }

        private StateReader(byte[] bytes, int end)
        {
            _bytes = bytes ?? throw new ArgumentNullException(nameof(bytes));
            _end = end;
        }

        /// <summary>Verifies the trailing checksum, then reads everything before it.</summary>
        public static StateReader ForChecked(byte[] bytes, string what)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length < 8) throw new ReckoningFormatException($"{what}: {bytes.Length} bytes is too short to hold a checksum.");

            int bodyLength = bytes.Length - 8;
            ulong stored = new StateReader(bytes) { _position = bodyLength }.ReadULong();
            ulong actual = FixHasher.Of(bytes, 0, bodyLength);
            if (stored != actual)
                throw new ReckoningFormatException($"{what}: checksum {actual:X16} does not match the stored {stored:X16}. The bytes are corrupt or truncated.");
            return new StateReader(bytes, bodyLength);
        }

        public bool AtEnd => _position == _end;
        public int Remaining => _end - _position;

        public byte ReadByte()
        {
            Require(1);
            return _bytes[_position++];
        }

        public bool ReadBool() => ReadByte() != 0;

        public ushort ReadUShort()
        {
            Require(2);
            ushort value = (ushort)(_bytes[_position] | (_bytes[_position + 1] << 8));
            _position += 2;
            return value;
        }

        public uint ReadUInt()
        {
            Require(4);
            uint value = 0;
            for (int i = 0; i < 4; i++) value |= (uint)_bytes[_position++] << (8 * i);
            return value;
        }

        public int ReadInt() => unchecked((int)ReadUInt());

        public ulong ReadULong()
        {
            Require(8);
            ulong value = 0;
            for (int i = 0; i < 8; i++) value |= (ulong)_bytes[_position++] << (8 * i);
            return value;
        }

        public long ReadLong() => unchecked((long)ReadULong());

        public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadLong());

        public byte[] ReadBytes()
        {
            int length = ReadInt();
            if (length < 0) throw new ReckoningFormatException($"Negative byte count {length}.");
            Require(length);
            var result = new byte[length];
            Buffer.BlockCopy(_bytes, _position, result, 0, length);
            _position += length;
            return result;
        }

        public string ReadString() => Encoding.UTF8.GetString(ReadBytes());

        private void Require(int count)
        {
            if (count > _end - _position)
                throw new ReckoningFormatException($"Wanted {count} bytes at position {_position}, only {_end - _position} remain.");
        }
    }
}
