using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Assertions.Must;

namespace Assets.Scripts.Utils
{
    public class LinkedListM<T> : IEnumerable<T>, IEnumerator<T>
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

        public LinkedListM(int capacity = 16)
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
        public ushort MoveNext(ushort ptr) => data[ptr].next;

        public bool HasPrev => data[ptr].prev != DataHead;
        public bool MovePrev()
        {
            ptr = data[ptr].prev;
            return ptr != DataHead;
        }
        public ushort MovePrev(ushort ptr) => data[ptr].prev;

        public int Count => count;

        public ref T Get() => ref data[ptr].item;
        public ref T Get(ushort ptr) => ref data[ptr].item;
        public ref T GetPrev() => ref data[data[ptr].prev].item;
        public ref T GetNext() => ref data[data[ptr].next].item;

        public ushort Ptr { get => ptr; set => ptr = value; }

        public T Current => data[ptr].item;

        object IEnumerator.Current => throw new NotSupportedException();

        public void ResetPtr() => ptr = 0;

        public ref T InsertAfter()
        {
            return ref InsertBetween(ptr, data[ptr].next);
        }
        public ref T InsertBefore()
        {
            return ref InsertBetween(data[ptr].prev, ptr);
        }

        private ref T InsertBetween(ushort ptr1, ushort ptr3)
        {
            ptr = Alloc();
            data[ptr].prev = ptr1;
            data[ptr].next = ptr3;
            data[ptr1].next = ptr;
            data[ptr3].prev = ptr;
            count++;
            return ref data[ptr].item;
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

        internal void Remove(ushort ptr)
        {
            this.ptr = ptr;
            Remove();
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

        public void MakeLoop()
        {
            var first = data[0].next;
            var last = data[0].prev;
            data[first].prev = last;
            data[last].next = first;
            data[0].next = DataHead;
            data[0].prev = DataHead;
        }

        private static void ThrowDeleteHead()
        {
            throw new InvalidOperationException("Nemuzes odebirat HEAD");
        }

        private void Free(ushort ptr)
        {
            var eNext = data[EmptyHead].next;
            var dPrev = data[ptr].prev;
            var dNext = data[ptr].next;
            data[ptr] = new(default, EmptyHead, eNext);
            data[EmptyHead].next = ptr;
            data[eNext].prev = ptr;
            data[dPrev].next = dNext;
            data[dNext].prev = dPrev;
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

        public LinkedListM<T> GetEnumerator()
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
