﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    public class LevelLabel : MonoBehaviour
    {
        [NonSerialized]
        internal Map.Map Map;
        [NonSerialized]
        internal bool wasCloned;
    }
}
