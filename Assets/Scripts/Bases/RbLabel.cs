using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    public class RbLabel : Label, ILevelPlaceabe
    {
        public override Placeable PlaceableC => GetComponentInChildren<Placeable>();
        public override Transform ParentForConnections => SubLabel.ParentForConnections;
        public override bool IsGroup => true;
        public override Label Prototype => Game.Instance.PrefabsStore.RbBase;
        public override void DetachKilledChild(Label child)
        {
            child.transform.SetParent(LevelGroup, true);
            Kill();
        }
        public override void Cleanup()
        {
            // schvalne vynechavam clenup connectables, protoze je nemam
        }

        void ILevelPlaceabe.Instantiate(Map.Map map, Transform parent, Vector3 pos)
        {
            var p = Instantiate(this, parent);
            p.PlaceableC.LevelPlaceAfterInstanciate(map, pos);
        }

        private Label SubLabel => transform.GetComponentInFirstChildren<Label>().ToRealNull();

        public void OnCollisionEnter(Collision collision)
        {
            var dest = transform.GetComponentInFirstChildren<IPhysicsEvents>();
            if (dest != null)
                dest.OnCollisionEnter(collision);
        }
    }
}
