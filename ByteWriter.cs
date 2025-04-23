using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    internal unsafe class ByteWriter : IDisposable
    {
        int cursor = 0;
        nint pointer;
        uint size = 0;

        public unsafe ByteWriter(uint size)
        {
            this.size = size;
            pointer = (nint)NativeMemory.Alloc(size);
        }

        public ReadOnlySpan<byte> GetSpan()
        {
            return new ReadOnlySpan<byte>((void*)pointer, (int)size);
        }

        public static int GetStringByteCount(string s)
        {
            return 2 + Encoding.Unicode.GetByteCount(s);
        }
        public void Write(string s)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(s);
            Write((ushort)bytes.Length);
            for (int i = 0; i < bytes.Length; i++)
            {
                Write(bytes[i]);
            }
        }

        public void Write(short v)
        {
            short* position = (short*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 2));
            }
            cursor += 2;
        }
        public void Write(ushort v)
        {
            ushort* position = (ushort*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 2));
            }
            cursor += 2;
        }
        public void Write(int v)
        {
            int* position = (int*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 4));
            }
            cursor += 4;
        }
        public void Write(uint v)
        {
            uint* position = (uint*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 4));
            }
            cursor += 4;
        }
        public void Write(float v)
        {
            float* position = (float*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 4));
            }
            cursor += 4;
        }
        public void Write(double v)
        {
            double* position = (double*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 8));
            }
            cursor += 8;
        }
        public void Write(decimal v)
        {
            decimal* position = (decimal*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 16));
            }
            cursor += 16;
        }
        public void Write(char v)
        {
            char* position = (char*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 2));
            }
            cursor += 2;
        }
        public void Write(bool v)
        {
            bool* position = (bool*)(pointer + cursor);
            *position = v;
            cursor += 1;
        }
        public void Write(byte v)
        {
            byte* position = (byte*)(pointer + cursor);
            *position = v;
            cursor += 1;
        }
        public void Write(long v)
        {
            long* position = (long*)(pointer + cursor);
            *position = v;
            if (BitConverter.IsLittleEndian)
            {
                MemoryExtensions.Reverse(new Span<byte>(position, 8));
            }
            cursor += 8;
        }
        public void Write(ulong v)
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
