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
        public PlaceableSettings Settings;
        public Ksid Ksid;

        public override Label Prototype => Settings?.Prototype;
        public override Ksid KsidGet => Ksid;
        public override Placeable PlaceableC => throw new NotSupportedException();
    }
}