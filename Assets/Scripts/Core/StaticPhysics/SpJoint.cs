using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core.StaticPhysics
{
    public struct SpJoint
    {
        public float length;
        public float stretchLimit;
        public float compressLimit;
        public float momentLimit;
        public float compress;
        public float moment;
        public float tempCompress;
        public float tempMoment;
        public Vector2 abDir; // normalizovany vector z vrcholu s nizsim indexem do vrchlu s vyzsim indexem
        public Vector2 normal => new Vector2(abDir.y, -abDir.x);
    }
}
