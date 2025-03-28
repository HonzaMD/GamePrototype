﻿using Assets.Scripts.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Bases
{
    public class LabelWithSettings : Label
    {
        private bool isAlive;

        public PlaceableSettings Settings;
        public Ksid Ksid;

        public override Label Prototype => Settings.Prototype;
        public override Ksid KsidGet => Ksid;
        public override Placeable PlaceableC => throw new NotSupportedException();
        public override bool IsAlive => isAlive;
        public override float GetMass() => Settings.Mass;
        public override PlaceableSettings GetSettings() => Settings;

        public override void Cleanup(bool goesToInventory)
        {
            isAlive = false;
            base.Cleanup(goesToInventory);
        }

        public override void Init(Map.Map map) => isAlive = true;
    }
}
