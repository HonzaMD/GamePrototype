using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditorInternal;

namespace Assets.Scripts.Map.Visibility
{
    internal enum CState : byte
    {
        Unknown,
        Visible,    // neni full shadow (muzu prejit z PartShadow po testu)
        PartShadow, // kandidat na fullShadow (pak musim udelat detailni test
        FullShadow, // potreba pro detekci DCSeedu. Vsechny stavy >= FullShadow odpovidaji FullShadow
        Dark,       // bunka ve stinu darkCasteru
        DSeed,      // kandidat na darkCaster
    }

    [Flags]
    internal enum WallType : byte
    {
        None = 0,
        Floor = 1,
        Side = 2,
        FloorSet = 1 | 4,
    }

    internal struct Cell
    {
        public CState state;
        public WallType wallType;
        public short darkCaster;

        public readonly bool IsFloor(int shift) => state >= CState.FullShadow || (((int)WallType.Floor << shift) & (int)wallType) != 0;
        public readonly bool IsSide(int shift) => state >= CState.FullShadow || (((int)WallType.Side << shift) & (int)wallType) != 0;
    }
}
