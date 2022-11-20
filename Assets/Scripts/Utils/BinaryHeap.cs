using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    /// <summary>
    /// A binary heap, useful for sorting data and priority queues.
    /// pozor nevolat funkce, ktere haldu tridi. Neefektivni. (Enumerace, Contains(T), Remove(T))
    /// </summary>
    /// <typeparam name="T"><![CDATA[IComparable<T> type of item in the heap]]>.</typeparam>
    public class BinaryHeap<T> : ICollection<T>
        where T : IComparable<T>
    {
        private const int DefaultSize = 4;

        private T[] data;
        private int count = 0;
        private int capacity;
        private bool sorted;

        /// <summary>
        /// Gets the number of values in the heap. 
        /// </summary>
        public int Count => count;

        /// <summary>
        /// Gets or sets the capacity of the heap.
        /// </summary>
        public int Capacity
        {
            get => capacity;
            set
            {
                int previousCapacity = capacity;
                capacity = System.Math.Max(value, count);
                if (capacity != previousCapacity)
                {
                    var temp = new T[capacity];
                    Array.Copy(data, temp, count);
                    data = temp;
                }
            }
        }

        public T[] RawHeapDataBuffer => data;

        /// <summary>
        /// Creates a new binary heap.
        /// </summary>
        public BinaryHeap()
        {
            data = new T[DefaultSize];
            capacity = DefaultSize;
        }

        public BinaryHeap(int capacity)
        {
            if (capacity <= 0) throw new ArgumentException("kapacita musi byt kladna", "capacity");

            data = new T[capacity];
            this.capacity = capacity;
        }

        public BinaryHeap(T[] values)
        {
            if (values == null) throw new ArgumentException("values nesmi byt null", "values");

            this.data = new T[values.Length];
            this.capacity = values.Length;
            this.BuildHeapFrom(values);
        }

        private BinaryHeap(T[] data, int count)
        {
            Capacity = count;
            this.count = count;
            Array.Copy(data, this.data, count);
        }
        /// <summary>
        /// Gets the first value in the heap without removing it.
        /// </summary>
        /// <returns>The lowest value of type TValue.</returns>
        public ref T Peek() => ref data[0];

        /// <summary>
        /// Removes all items from the heap.
        /// </summary>
        public void Clear()
        {
            this.count = 0;
            Array.Clear(data, 0, data.Length);
        }

        /// <summary>
        /// Adds a key and value to the heap.
        /// </summary>
        /// <param name="item">The item to add to the heap.</param>
        public void Add(T item)
        {
            if (count == capacity)
            {
                Capacity *= 2;
            }
            data[count] = item;
            UpHeap();
            count++;
        }

        /// <summary>
        /// Removes and returns the first item in the heap.
        /// </summary>
        /// <returns>The next value in the heap.</returns>
        public T Remove()
        {
            if (this.count == 0)
            {
                throw new InvalidOperationException("Cannot remove item, heap is empty.");
            }
            T v = data[0];
            count--;
            data[0] = data[count];
            data[count] = default; //Clears the Last Node
            DownHeap(0);
            return v;
        }

        //helper function that performs up-heap bubbling
        private void UpHeap()
        {
            sorted = false;
            int p = count;
            T item = data[p];
            int par = Parent(p);
            while (par > -1 && item.CompareTo(data[par]) < 0)
            {
                data[p] = data[par]; //Swap nodes
                p = par;
                par = Parent(p);
            }
            data[p] = item;
        }

        private void EnsureSort()
        {
            if (sorted) return;
            Array.Sort(data, 0, count);
            sorted = true;
        }

        private static int Parent(int index) => (index - 1) >> 1;
        private static int Child1(int index) => (index << 1) + 1;
        private static int Child2(int index) => (index << 1) + 2;

        private void DownHeap(int startIndex)
        {
            sorted = false;
            int n;
            int p = startIndex;
            T item = data[p];
            while (true)
            {
                int ch1 = Child1(p);
                if (ch1 >= count) break;
                int ch2 = Child2(p);
                if (ch2 >= count)
                {
                    n = ch1;
                }
                else
                {
                    n = data[ch1].CompareTo(data[ch2]) < 0 ? ch1 : ch2;
                }
                if (item.CompareTo(data[n]) > 0)
                {
                    data[p] = data[n]; //Swap nodes
                    p = n;
                }
                else
                {
                    break;
                }
            }
            data[p] = item;
        }

        /// <summary>
        /// Creates a new instance of an identical binary heap.
        /// </summary>
        public BinaryHeap<T> Copy() => new BinaryHeap<T>(data, count);

        // Provizorni reseni pro nealokujici enumeraci - chtelo by to structovy enumerator
        public T GetItem(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Hodnota indexu je mimo platny rozsah");
            }

            EnsureSort();

            return data[index];
        }

        /// <summary>
        /// Gets an enumerator for the binary heap.
        /// </summary>
        /// <returns>An IEnumerator of type T.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            EnsureSort();
            for (int i = 0; i < count; i++)
            {
                yield return data[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Checks to see if the binary heap contains the specified item.
        /// </summary>
        /// <param name="item">The item to search the binary heap for.</param>
        /// <returns>A boolean, true if binary heap contains item.</returns>
        public bool Contains(T item)
        {
            EnsureSort();
            return Array.BinarySearch<T>(data, 0, count, item) >= 0;
        }

        /// <summary>
        /// Copies the binary heap to an array at the specified index.
        /// </summary>
        /// <param name="array">One dimensional array that is the destination of the copied elements.</param>
        /// <param name="arrayIndex">The zero-based index at which copying begins.</param>
        public void CopyTo(T[] array, int arrayIndex)
        {
            EnsureSort();
            Array.Copy(data, array, count);
        }

        /// <summary>
        /// Gets whether or not the binary heap is readonly.
        /// </summary>
        public bool IsReadOnly => false;

        /// <summary>
        /// Removes an item from the binary heap. This utilizes the type T's Comparer and will not remove duplicates.
        /// </summary>
        /// <param name="item">The item to be removed.</param>
        /// <returns>Boolean true if the item was removed.</returns>
        public bool Remove(T item)
        {
            EnsureSort();
            int i = Array.BinarySearch<T>(data, 0, count, item);
            if (i < 0) return false;
            Array.Copy(data, i + 1, data, i, count - i - 1);
            count--;
            data[count] = default;
            return true;
        }

        /// <summary>
        /// Zupdatuje prvek s minimalni hodnotou v halde a spravne ho zatridi.
        /// </summary>
        /// <param name="value"></param>
        public void UpdateMinimalValue(T value)
        {
            if (value.CompareTo(data[0]) == 1) //pokud je prvek vetsi, tak ho musime zatridit. Pokud je mensi nez puvodni  minimum, tak se nic delat nemusi.
            {
                data[0] = value;
                DownHeap(0);
            }
            else
            {
                data[0] = value;
            }
        }

        /// <summary>
        /// Kdyz jsou prvky classy, tak se jejich hodnata muze zmenit, bez toho aby si toho halda vsimla.
        /// Touto metodou ji dame signal, ze si ma zkontrolovat hodnotu min prvku a pripadne, ho zatridit nekam jinam.
        /// </summary>
        public void UpdateMinimalValue() => DownHeap(0);

        /// <summary>
        /// Prepise puvodne obsazene prvky haldy a efektivne postavi haldu z poskytnutych hodnot.
        /// </summary>
        public void BuildHeapFrom(T[] values)
        {
            if (this.Capacity < values.Length) this.Capacity = values.Length;
            if (this.Count > values.Length) this.Clear(); //zbytecny, ale tahle trida po sobe buffer cisti
            Array.Copy(values, this.data, values.Length);
            this.count = values.Length;

            if (count > 0)
            {
                for (int i = values.Length / 2; i >= 0; i--)
                {
                    DownHeap(i);
                }
            }
        }

        /// <summary>
        /// Efektivne postavi haldu z ulozenych hodnot. Pouzijeme napr. kdyz zmenime vnitrni hodnoty, podle kterych se tridi.
        /// </summary>
        public void Rebuild()
        {
            if (count > 0)
            {
                for (int i = count / 2; i >= 0; i--)
                {
                    DownHeap(i);
                }
            }
        }
    }
}
