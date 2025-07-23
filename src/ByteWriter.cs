using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Myosotis.VersionedSerializer
{
    public unsafe class ByteWriter
    {
        int cursor = 0;
        int capacity = 0;
        byte* pointer;
        private bool IsDisposed;
        public Span<byte> ToSpan() => new Span<byte>(pointer, cursor);

        public ByteWriter(int capacity = 16)
        {
            this.capacity = capacity;
            pointer = (byte*)NativeMemory.Alloc((nuint)(capacity));
        }

        public void Write(string v)
        {
            Write(v.Length);
            foreach (char c in v)
            {
                Write(c);
            }
        }

        public void Write(bool v)
        {
            ExpandCheck(1);
            Span<byte> bytes = new Span<byte>(pointer + cursor, 1);
            BitConverter.GetBytes(v).CopyTo(bytes);
            cursor += 1;
        }

        public void Write(char v)
        {
            ExpandCheck(2);
            Span<byte> bytes = new Span<byte>(pointer + cursor, 2);
            BitConverter.GetBytes(v).CopyTo(bytes);
            cursor += 2;
        }

        public void Write(int v)
        {
            ExpandCheck(4);
            Span<byte> bytes = new Span<byte>(pointer + cursor, 4);
            BitConverter.GetBytes(v).CopyTo(bytes);
            cursor += 4;
        }

        public void Write(double v)
        {
            ExpandCheck(8);
            Span<byte> bytes = new Span<byte>(pointer + cursor, 8);
            BitConverter.GetBytes(v).CopyTo(bytes);
            cursor += 8;
        }

        public void Write(byte v)
        {
            ExpandCheck(1);
            pointer[cursor] = v;
            cursor += 1;
        }

        private void ExpandCheck(int neededSize)
        {
            if (cursor + neededSize > capacity)
            {
                capacity *= 2;
                pointer = (byte*)NativeMemory.Realloc(pointer, (nuint)capacity);
            }
        }

        private void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                NativeMemory.Free(pointer);
                pointer = null;

                IsDisposed = true;
            }
        }

        ~ByteWriter()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
