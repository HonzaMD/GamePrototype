using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.StaticPhysics
{
    struct SpJoint
    {
        public float length;
        public float stretchLimit;
        public float compressLimit;
        public float momentLimit;
        public float stretch;
        public float compress;
        public float moment;
    }
}
