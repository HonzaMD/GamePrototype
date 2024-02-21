using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Scripts.Utils;
using Assets.Scripts.Map;
using Assets.Scripts.Core;

namespace Assets.Scripts.Stuff
{
    public class TouchDebugger : MonoBehaviour
    {
        public Ksid TestKsid;
        public bool Touch;

        private void Update()
        {
            Map.Map map = Game.MapFromPos(transform.position.x);
            if (map != null)
            {
                var p = GetComponent<Placeable>();
                p.KinematicMove(map);
                var list = ListPool<Placeable>.Rent();
                p.FindTouchingObjs(map, list, TestKsid, 0.1f);
                Touch = list.Count > 0;
                list.Return();
            }
        }
    }
}
