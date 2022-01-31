using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    static class ListPool<T>
    {
        private static readonly Stack<List<T>> cache = new Stack<List<T>>();
        public static List<T> Rent() => cache.Count == 0 ? new List<T>() : cache.Pop();
        public static void Return(List<T> list) 
        {
            list.Clear();
            cache.Push(list);
        }
    }

    static class ListPool
    {
        public static void Return<T>(this List<T> list) => ListPool<T>.Return(list);
    }
}
