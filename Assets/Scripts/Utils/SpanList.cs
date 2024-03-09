using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    public class SpanList<T>
    {
        private T[] data;
        private int count;
        public int Count => count;

        public SpanList(int capacity = 64)
        {
            data = new T[capacity];
        }

        public void Add(T item) 
        { 
            if (Count >= data.Length)
            {
                Array.Resize(ref data, count * 2);
            }
            data[count++] = item;
        }

        public Span<T> AsSpan() => data.AsSpan(0, count);
        public Span<T> AsSpan(int start, int length) => data.AsSpan(start, length);

        public ref T this[int index] => ref data[index];

        public void RemoveAt(int index)
        {
            count--;
            data[index] = data[count];
            data[count] = default;
        }

        public void Clear()
        {
            AsSpan().Clear();
            count = 0;
        }
    }
}
