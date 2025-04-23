using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    internal class ByteReader
    {
        nint pointer;
        int cursor = 0;

        public unsafe ByteReader(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* b = bytes)
            {
                pointer = (nint)b;
            }
        }

        public unsafe short ReadInt16()
        {
            var valuePointer = (short*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 2));
            }
            cursor += 2;
            return value;
        }

        public unsafe ushort ReadUInt16()
        {
            var valuePointer = (ushort*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 2));
            }
            cursor += 2;
            return value;
        }

        public unsafe int ReadInt32()
        {
            var valuePointer = (int*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 4));
            }
            cursor += 4;
            return value;
        }

        public unsafe uint ReadUInt32()
        {
            var valuePointer = (uint*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 4));
            }
            cursor += 4;
            return value;
        }

        public unsafe long ReadInt64()
        {
            var valuePointer = (long*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 8));
            }
            cursor += 8;
            return value;
        }

        public unsafe ulong ReadUInt64()
        {
            var valuePointer = (ulong*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 8));
            }
            cursor += 8;
            return value;
        }
        public unsafe char ReadChar()
        {
            var valuePointer = (char*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 2));
            }
            cursor += 2;
            return value;
        }
        public unsafe byte ReadByte()
        {
            var valuePointer = (byte*)(pointer + cursor);
            var value = *valuePointer;
            cursor += 1;
            return value;
        }
        public unsafe bool ReadBool()
        {
            var valuePointer = (bool*)(pointer + cursor);
            var value = *valuePointer;
            cursor += 1;
            return value;
        }
        public unsafe float ReadFloat()
        {
            var valuePointer = (float*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 4));
            }
            cursor += 4;
            return value;
        }
        public unsafe double ReadDouble()
        {
            var valuePointer = (double*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 8));
            }
            cursor += 8;
            return value;
        }
        public unsafe decimal ReadDecimal()
        {
            var valuePointer = (decimal*)(pointer + cursor);
            var value = *valuePointer;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(&value, 16));
            }
            cursor += 16;
            return value;
        }

        public unsafe string ReadString()
        {
            ushort length = ReadUInt16();
            string value = Encoding.Unicode.GetString(new Span<byte>((byte*)(pointer + cursor), length));
            cursor += length;
            return value;
        }
    }
}
