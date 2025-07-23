using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    internal unsafe class ByteReader
    {
        byte* pointer;
        int cursor = 0;

        public unsafe ByteReader(ReadOnlySpan<byte> bytes)
        {
            fixed (byte* b = bytes)
            {
                pointer = b;
            }
        }

        public unsafe int ReadInt32()
        {
            var value = BitConverter.ToInt32(new Span<byte>(pointer+cursor, 4));
            //if (BitConverter.IsLittleEndian)
            //{
            //    MemoryExtensions.Reverse(new Span<byte>(&value, 4));
            //}
            cursor += 4;
            return value;
        }
        public unsafe char ReadChar()
        {
            var value = BitConverter.ToChar(new Span<byte>(pointer + cursor, 2));
            //if (BitConverter.IsLittleEndian)
            //{
            //    MemoryExtensions.Reverse(new Span<byte>(&value, 4));
            //}
            cursor += 2;
            return value;
        }
        public unsafe byte ReadByte()
        {
            var value = pointer[cursor];
            cursor += 1;
            return value;
        }
        public unsafe bool ReadBool()
        {
            var value = BitConverter.ToBoolean(new Span<byte>(pointer + cursor, 1));

            cursor += 1;
            return value;
        }

        public unsafe double ReadDouble()
        {
            var value = BitConverter.ToDouble(new Span<byte>(pointer + cursor, 8));
            //if (BitConverter.IsLittleEndian)
            //{
            //    MemoryExtensions.Reverse(new Span<byte>(&value, 4));
            //}
            cursor += 8;
            return value;
        }
        public unsafe string ReadString()
        {
            int length = ReadInt32();
            char[] chars = new char[length];
            for (int i = 0; i < length; i++)
            {
                chars[i] = ReadChar();
            }
            return new string(chars);
        }
    }
}
