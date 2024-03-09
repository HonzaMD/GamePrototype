using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.Inventory
{
    internal static class KeysToInventory
    {
        private static (KeyCode, int)[] mapping = new [] 
        {
            (KeyCode.Alpha0, -10),
            (KeyCode.Alpha1, -9),
            (KeyCode.Alpha2, -8),
            (KeyCode.Alpha3, -7),
            (KeyCode.Alpha4, -6),
            (KeyCode.Alpha5, -5),
            (KeyCode.Alpha6, -4),
            (KeyCode.Alpha7, -3),
            (KeyCode.Alpha8, -2),
            (KeyCode.Alpha9, -1),
        };

        public static int TestKeys()
        {
            foreach (var m in mapping)
            {
                if (Input.GetKeyDown(m.Item1))
                    return m.Item2;
            }
            return 0;
        }
    }
}
