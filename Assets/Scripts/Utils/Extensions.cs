using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public static class Extensions
    {
        public static CellBLocking ToFullBlock(this Transform transform)
        {
            var z = transform.position.z;
            return z < 0 ? CellBLocking.Cell0 : z == 0 ? CellBLocking.Cell1 : CellBLocking.Cell2;
        }

        public static CellBLocking ToPartBlock(this Transform transform)
        {
            var z = transform.position.z;
            return z < 0 ? CellBLocking.Cell0Part : z == 0 ? CellBLocking.Cell1Part : CellBLocking.Cell2Part;
        }
    }
}
