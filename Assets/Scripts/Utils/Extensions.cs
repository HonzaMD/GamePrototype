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
        public static CellBlocking ToFullBlock(this Transform transform)
        {
            var z = transform.position.z;
            return z < 0 ? CellBlocking.Cell0 : z == 0 ? CellBlocking.Cell1 : CellBlocking.Cell2;
        }

        public static CellBlocking ToPartBlock(this Transform transform)
        {
            var z = transform.position.z;
            return z < 0 ? CellBlocking.Cell0Part : z == 0 ? CellBlocking.Cell1Part : CellBlocking.Cell2Part;
        }
    }
}
