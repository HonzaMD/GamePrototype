using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    public class LinkedList<T> : IEnumerable<T>, IEnumerator<T>
    {
        private const ushort DataHead = 0;
        private const ushort EmptyHead = 1;

        private struct Node
        {
            public T item;
            public ushort prev;
            public ushort next;

            public Node(T item, ushort prev, ushort next)
            {
                this.item = item;
                this.prev = prev;
                this.next = next;
            }
        }

        private Node[] data;
        private ushort top = 2;
        private ushort ptr;
        private int count;

        public LinkedList(int capacity = 16)
        {
            if (capacity > ushort.MaxValue)
                throw new ArgumentException("Prilis velka kapacita LinkedListu");
            data = new Node[capacity];
            data[0] = new(default, DataHead, DataHead);
            data[1] = new(default, EmptyHead, EmptyHead);
        }

        public bool HasNext => data[ptr].next != DataHead;
        public bool MoveNext()
        {
            ptr = data[ptr].next;
            return ptr != DataHead;
        }

        public bool HasPrev => data[ptr].prev != DataHead;
        public bool MovePrev()
        {
            ptr = data[ptr].prev;
            return ptr != DataHead;
        }

        public int Count => count;

        public ref T Get => ref data[ptr].item;

        public ushort Ptr { get => ptr; set => ptr = value; }

        public T Current => data[ptr].item;

        object IEnumerator.Current => throw new NotSupportedException();

        public void ResetPtr() => ptr = 0;

        public void InsertAfter(T item)
        {
            ushort ptr2 = Alloc();
            ushort ptr3 = data[ptr].next;
            data[ptr2] = new(item, ptr, ptr3);
            data[ptr].next = ptr2;
            data[ptr3].prev = ptr2;
            count++;
        }

        public void InsertBefore(T item)
        {
            ushort ptr2 = Alloc();
            ushort ptr3 = data[ptr].prev;
            data[ptr2] = new(item, ptr3, ptr2);
            data[ptr].prev = ptr2;
            data[ptr3].next = ptr2;
            count++;
        }

        public void Remove()
        {
            if (ptr == DataHead)
                ThrowDeleteHead();
            var ptr2 = ptr;
            MoveNext();
            Free(ptr2);
            count--;
        }

        public void Clear()
        {
            if (count == 0)
                return;

            data[0] = new(default, DataHead, DataHead);
            data[1] = new(default, EmptyHead, EmptyHead);
            top = 2;
            ResetPtr();
            count = 0;
        }

        private static void ThrowDeleteHead()
        {
            throw new InvalidOperationException("Nemuzes odebirat HEAD");
        }

        private void Free(ushort ptr)
        {
            var next = data[EmptyHead].next;
            data[ptr] = new(default, EmptyHead, next);
            data[EmptyHead].next = ptr;
            data[next].prev = ptr;
        }

        private ushort Alloc()
        {
            var ret = data[EmptyHead].next;
            if (ret != EmptyHead)
            {
                var next = data[ret].next;
                data[EmptyHead].next = next;
                data[next].prev = EmptyHead;
            }
            else
            {
                if (top == data.Length)
                    Resize();
                ret = top;
                top++;
            }

            return ret;
        }

        private void Resize()
        {
            int capacity = Math.Max(ushort.MaxValue, data.Length * 2);
            if (capacity == data.Length)
                throw new ArgumentException("Prilis velka kapacita LinkedListu");
            Array.Resize(ref data, capacity);
        }

        public LinkedList<T> GetEnumerator()
        {
            ResetPtr();
            return this;
        }
        IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Reset() => ResetPtr();
        public void Dispose() => ResetPtr();
    }
}
