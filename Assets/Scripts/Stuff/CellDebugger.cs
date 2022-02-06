using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Scripts.Utils;
using Assets.Scripts.Map;

namespace Assets.Scripts.Stuff
{
    public class CellDebugger : MonoBehaviour
    {
        public Vector2Int CellPos;
        public CellFlags Blocking;
        public List<Placeable> CellContent = new List<Placeable>();

        private void Update()
        {
            CellPos = Game.Map.WorldToCell(transform.position);
            ref Cell cell = ref Game.Map.GetCell(CellPos);
            Blocking = cell.Blocking;
            CellContent.Clear();
            foreach (Placeable p in cell)
                CellContent.Add(p);            
        }
    }
}
