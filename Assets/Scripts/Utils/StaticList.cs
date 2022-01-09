using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    static class StaticList<T>
    {
        public static readonly List<T> List = new List<T>();
        public static void Clear() => List.Clear();
    }
}
