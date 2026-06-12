using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    // Sdilena logika stavby hliny v bunce. Pouziva SandCombiner (premena pisku na hlinu)
    // i DirtBuilder (rucni stavba za gravel).
    public static class DirtFactory
    {
        private static readonly Vector2Int[] N4 = { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };

        // Otestuje, zda je bunka volna pro stavbu hliny. Zajimaji jen kolizni objekty:
        // pisek/SandCombiner se da premenit (sesbira se do toKill), cokoli jineho stavbu blokuje.
        public static bool CollectCellForDirt(Map.Map map, Vector2Int cell, List<Placeable> toKill)
        {
            ref var c = ref map.GetCell(cell);
            foreach (var p in c)
            {
                if ((p.CellBlocking & CellFlags.AllPartCells) == 0)
                    continue;

                if (p is SandCombiner || p.Ksid.IsChildOfOrEq(Ksid.SandLike))
                    toKill.Add(p);
                else
                    return false;
            }
            return true;
        }

        // Postavi double-cell hlinu v bunce: zabije sesbirany pisek a navaze hlinu
        // na SpNode sousedy (stejne jako SandCombiner.TurnIntoBasicDirt).
        public static Placeable BuildDirt(Map.Map map, Vector2Int cell, Transform parent, List<Placeable> toKill)
        {
            foreach (var p in toKill)
                if (p.IsAlive)
                    p.Kill();

            Vector3 dirtPos = map.CellToWorld(cell);
            var dirt = Game.Instance.PrefabsStore.BasicDirt.Create(parent, dirtPos, map);

            var spNeighbors = ListPool<Placeable>.Rent();
            int tag = 0;
            foreach (var off in N4)
                map.Get(spNeighbors, cell + off, Ksid.SpNode, ref tag);

            foreach (var c in spNeighbors)
            {
                if ((c.CellBlocking & CellFlags.AllFullEx) != 0 && (c.SpNodeIndex != 0 || c.Ksid.IsChildOfOrEq(Ksid.SpFixed)))
                {
                    dirt.CreateRbJoint(c).SetupSp();
                }
            }
            spNeighbors.Return();

            if (dirt.SpNodeIndex == 0)
                dirt.AttachRigidBody(true, false);

            return dirt;
        }
    }
}
