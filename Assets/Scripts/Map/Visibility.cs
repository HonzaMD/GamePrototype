using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Map
{
    internal class Visibility
    {
        public const int HalfXSize = 32;
        public const int HalfYSize = 26;

        private const int sizeX = HalfXSize * 2 + 1;
        private const int sizeY = HalfYSize * 2 + 1;

        private const float centerRadius = 0.7f;

        private readonly Map map;
        private Cell[] vmap = new Cell[sizeY * sizeX];
        private Vector2 centerPos;
        private Vector2Int centerCell;
        private Vector2Int offset;

        private enum CState : byte
        {
            Unknown,
            Visible,
            PartShadow,
            FullShadow,
            Dark,
            DarkCaster,
        }

        private enum WallType : byte
        {
            None,
            Floor,
            Side,
            Both,
        }

        private struct Cell 
        { 
            public CState state;
            public WallType wallType;
        }


        public Visinility(Map map)
        {
            this.map = map;
        }

        public void Compute(Vector2 center)
        {
            centerPos = center;
            centerCell = map.WorldToCell(center);
            offset = centerCell - new Vector2Int(HalfXSize, HalfYSize);
            Reset();

            var pos = new Vector2Int(HalfXSize, HalfYSize);
            Get(pos).state = CState.Visible;
            int circleCounter = Math.Max(HalfYSize, HalfXSize);

            while (circleCounter > 0)
            {
                pos -= Vector2Int.one;
                circleCounter--;
            }
        }

        ref Cell Get(Vector2Int coords) => ref vmap[coords.y * sizeX + coords.y];

        private void Reset()
        {
            vmap.AsSpan().Clear();
        }
    }
}
