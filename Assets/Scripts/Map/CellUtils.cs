using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public static class CellUtils
    {
        public const int Cell0Shift = 8;
        public const int Cell1Shift = 16;

        public static unsafe SubCellFlags Cell0(this CellFlags flags)
        {
            byte* pi = (byte*)&flags;
            return (SubCellFlags)pi[1];
        }

        public static unsafe SubCellFlags Cell1(this CellFlags flags)
        {
            byte* pi = (byte*)&flags;
            return (SubCellFlags)pi[2];
        }

        public static CellFlags Combine(SubCellFlags subCellFlags, CellFlags flags, float z)
        {
            var shift = z < 0.25 ? Cell0Shift : Cell1Shift;
            return (CellFlags)(((int)subCellFlags << shift) | (byte)flags);
        }

        public static CellFlags Combine(SubCellFlags subCellFlags, CellFlags flags, Transform transform) => Combine(subCellFlags, flags, transform.position.z);
        public static CellFlags Combine(SubCellFlags subCellFlags, Transform transform) => Combine(subCellFlags, CellFlags.Free, transform.position.z);

        public static bool IsPartBlock0(this CellFlags flags) => (flags & CellFlags.Cell0Part) != 0;
        public static bool IsPartBlock1(this CellFlags flags) => (flags & CellFlags.Cell1Part) != 0;
        public static bool IsPartBlock(this CellFlags flags, int cell) => flags.HasSubFlag(SubCellFlags.Part, cell);

        public static bool HasSubFlag(this CellFlags flags, SubCellFlags flag, int cell) => ((int)flags & ((int)flag << (Cell0Shift << cell))) != 0;
    }
}
