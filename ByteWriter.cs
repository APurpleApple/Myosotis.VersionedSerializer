using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    public unsafe class ByteWriter : IDisposable
    {
        int cursor = 0;
        nint pointer;
        uint size = 0;

        internal unsafe ByteWriter(uint size)
        {
            this.size = size;
            pointer = (nint)NativeMemory.Alloc(size);
        }

        public ReadOnlySpan<byte> GetSpan()
        {
            return new ReadOnlySpan<byte>((void*)pointer, (int)size);
        }

        internal static int GetStringByteCount(string s)
        {
            return 2 + Encoding.Unicode.GetByteCount(s);
        }
        internal void Write(string s)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(s);
            Write((ushort)bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                Write(bytes[i]);
            }
        }

        internal void Write(short v)
        {
            short* position = (short*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 2));
            }
            cursor += 2;
        }
        internal void Write(ushort v)
        {
            ushort* position = (ushort*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 2));
            }
            cursor += 2;
        }
        internal void Write(int v)
        {
            int* position = (int*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 4));
            }
            cursor += 4;
        }
        internal void Write(uint v)
        {
            uint* position = (uint*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 4));
            }
            cursor += 4;
        }
        internal void Write(float v)
        {
            float* position = (float*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 4));
            }
            cursor += 4;
        }
        internal void Write(double v)
        {
            double* position = (double*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 8));
            }
            cursor += 8;
        }
        internal void Write(decimal v)
        {
            decimal* position = (decimal*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 16));
            }
            cursor += 16;
        }
        internal void Write(char v)
        {
            char* position = (char*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 2));
            }
            cursor += 2;
        }
        internal void Write(bool v)
        {
            bool* position = (bool*)(pointer + cursor);
            *position = v;
            cursor += 1;
        }
        internal void Write(byte v)
        {
            byte* position = (byte*)(pointer + cursor);
            *position = v;
            cursor += 1;
        }
        internal void Write(long v)
        {
            long* position = (long*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 8));
            }
            cursor += 8;
        }
        internal void Write(ulong v)
        {
            ulong* position = (ulong*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 8));
            }
            cursor += 8;
        }
        public void Dispose()
        {
            NativeMemory.Free((void*)pointer);
        }
    }
}
