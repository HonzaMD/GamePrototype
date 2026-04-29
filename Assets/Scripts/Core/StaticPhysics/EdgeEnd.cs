using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.StaticPhysics
{
    // EdgeEnd = jeden konec hrany. Hrana existuje dvakrat (v A.edges a B.edges), sdileji Joint index.
    // Smer sireni sily (list -> koren):
    //
    //   [LeafNode] -Out->--Joint-->-In- [Node] -Out->--Joint-->-In- [RootNode]
    //
    // Out* = konec blizsi listu, In* = zrcadlovy konec blizsi koreni. Barva = index FixedRoot.
    // Slot 0 je vzdy lepsi (kratsi) cesta, slot 1 je alternativa.
    struct EdgeEnd
    {
        public int Other;         // index sousedniho uzlu
        public int Joint;         // index sdileneho SpJointu
        public int In0Root;       // barva prichazejici touto hranou z listove strany
        public int In1Root;
        public int Out0Root;      // barva cesty ven touto hranou ke koreni
        public int Out1Root;
        public float Out0Length;  // celkova delka te cesty ke koreni
        public float Out1Length;
        public float Out0Strength; // pevnost (nosnost) te cesty ke koreni; vyssi cislo = unese vic
        public float Out1Strength;

        public static void Delete0(ref EdgeEnd outEnd, ref EdgeEnd inEnd)
        {
            outEnd.Out0Root = outEnd.Out1Root;
            outEnd.Out0Length = outEnd.Out1Length;
            outEnd.Out0Strength = outEnd.Out1Strength;
            outEnd.Out1Root = 0;
            outEnd.Out1Length = 0;
            outEnd.Out1Strength = 0;
            inEnd.In0Root = inEnd.In1Root;
            inEnd.In1Root = 0;
        }

        public static void Delete1(ref EdgeEnd outEnd, ref EdgeEnd inEnd)
        {
            outEnd.Out1Root = 0;
            outEnd.Out1Length = 0;
            outEnd.Out1Strength = 0;
            inEnd.In1Root = 0;
        }

        public static void Swap(ref EdgeEnd outEnd, ref EdgeEnd inEnd)
        {
            float tempLen = outEnd.Out0Length;
            float tempStr = outEnd.Out0Strength;
            int tempRoot = outEnd.Out0Root;
            outEnd.Out0Length = outEnd.Out1Length;
            outEnd.Out0Strength = outEnd.Out1Strength;
            outEnd.Out0Root = outEnd.Out1Root;
            inEnd.In0Root = outEnd.Out1Root;
            outEnd.Out1Length = tempLen;
            outEnd.Out1Strength = tempStr;
            outEnd.Out1Root = tempRoot;
            inEnd.In1Root = tempRoot;
        }
    }
}
