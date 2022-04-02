using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.StaticPhysics
{
    // sily se prenaseji ze smeru In -> Out. Az se dostanou do FixedRootu
    // index fixed rootu urcuje barvu sipek
    struct EdgeEnd
    {
        public int Other;
        public int Joint;
        public int In0Root; 
        public int In1Root;
        public int Out0Root;
        public int Out1Root;
        public float Out0Lengh;
        public float Out1Lengh;
    }
}
