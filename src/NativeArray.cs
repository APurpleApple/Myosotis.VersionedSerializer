﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Myosotis.VersionedSerializer
{
    public unsafe class NativeArray<T> where T : unmanaged
    {
        Stack<int> availableIndices = new();
        private T* Elements;
        public int Count { get; private set; }
        private int Capacity;
        private int ElementSize;
        public int ActualCount => Count - availableIndices.Count;

        public Span<T> ToSpan() => new Span<T>(Elements, Count);
        public Span<T>.Enumerator GetEnumerator() => new Span<T>(Elements, Count).GetEnumerator();

        private bool IsDisposed;

        public NativeArray(int capacity = 16)
        {
            this.Capacity = capacity;
            ElementSize = Unsafe.SizeOf<T>();
            Elements = (T*)NativeMemory.Alloc((nuint)(capacity * ElementSize));
            Count = 0;
        }

        public ref T this[int i] => ref Elements[i];

        public int Add(T item)
        {

            int index = 0;
            if (availableIndices.Count == 0)
            {
                if (Count >= Capacity)
                {
                    Capacity *= 2;
                    Elements = (T*)NativeMemory.Realloc(Elements, (nuint)(Capacity * Unsafe.SizeOf<T>()));
                }
                index = Count;
                Count += 1;
            }
            else
            {
                index = availableIndices.Pop();
            }

            Elements[index] = item;

            return index;
        }

        public void RemoveLastElement()
        {
            Count -= 1;
        }

        public bool TryPop(out T element)
        {
            if (Count > 0)
            {
                element = Elements[Count - 1];
                Count -= 1;
                return true;
            }

            element = default;
            return false;
        }

        public void Clear()
        {
            Count = 0;
        }

        private void ResizeTo(int size)
        {
            Capacity = size;
            Elements = (T*)NativeMemory.Realloc((void*)Elements, (nuint)(ElementSize * Capacity));
        }

        // Fills gap by copying final element to the deleted index
        public void Delete(int index)
        {
            if (index == -1)
            {
                return;
            }

            if (index != Count - 1)
            {
                availableIndices.Push(index);
            }
            else
            {
                Count -= 1;
            }
        }

        public void CopyTo(NativeArray<T> other)
        {
            if (Count >= other.Capacity)
            {
                other.ResizeTo(Count);
            }

            NativeMemory.Copy(
                (void*)Elements,
                (void*)other.Elements,
                (nuint)(ElementSize * Count)
            );

            other.Count = Count;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                NativeMemory.Free(Elements);
                Elements = null;

                IsDisposed = true;
            }
        }

        ~NativeArray()
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
