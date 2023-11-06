using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Map
{
    public static class MapExtensions
    {
        public static void GetTouching(this Map map, List<Placeable> output, Vector3 point, Ksid ksid, float margin) 
        {
            var placeables = ListPool<Placeable>.Rent();
            var marginVec = new Vector2(margin, margin);
            map.Get(placeables, point.XY() - marginVec, marginVec * 2, ksid);
            float sqrMargin = margin * margin;
            foreach (var placeable in placeables)
            {
                if ((placeable.GetClosestPoint(point) - point).sqrMagnitude <= sqrMargin) 
                { 
                    output.Add(placeable);
                }
            }
            placeables.Return();
        }

        public static Placeable GetFirstTouching(this Map map, Vector3 point, Ksid ksid, float margin)
        {
            var placeables = ListPool<Placeable>.Rent();
            var marginVec = new Vector2(margin, margin);
            map.Get(placeables, point.XY() - marginVec, marginVec * 2, ksid);
            float sqrMargin = margin * margin;
            foreach (var placeable in placeables)
            {
                if ((placeable.GetClosestPoint(point) - point).sqrMagnitude <= sqrMargin)
                {
                    placeables.Return();
                    return placeable;
                }
            }
            placeables.Return();
            return null;
        }
    }
}
