using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.StaticPhysics
{
    // sily se prenaseji ze smeru In -> Out. Az se dostanou do FixedRootu
    // index fixed rootu urcuje barvu sipek
    // hrana 0 je vzdy ta lepsi tzn kratsi cetsta k cili
    struct EdgeEnd
    {
        public int Other;     // index nodu, kam hrana vede
        public int Joint;     // index SpJointu
        public int In0Root;   // prichozi barva
        public int In1Root;
        public int Out0Root;  // odchozi barva
        public int Out1Root;
        public float Out0Lengh;  // delka cesty do rootu. 
        public float Out1Lengh;
    }
}
