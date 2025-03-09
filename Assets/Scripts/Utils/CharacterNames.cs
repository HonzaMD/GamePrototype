using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    internal static class CharacterNames
    {
        private static string[] names = new string[] {
            "Jan",
            "Petr",
            "Hans",
            "Tri",
            "Uggo",
            "Maja",
            "Tary",
            "Thomas",
            "Jaque",
            "Mia",
            "Suze",
            "Ivan",
            "Standa",
            "Josef",
            "Viki",
            "Dan",
            "Mike",
            "Eve",
            "Torham",
        };

        private static int nameCounter;
        public static string GiveMeName()
        {
            return names[(nameCounter++) % names.Length];
        }
    }
}
