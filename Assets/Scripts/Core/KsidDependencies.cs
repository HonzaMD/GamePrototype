using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core
{
    class KsidDependencies : Ksids
    {
        public KsidDependencies()
            : base(new (Ksid child, Ksid parent)[]
            {
                (Ksid.Rope, Ksid.Catch),
                (Ksid.SmallMonster, Ksid.CharacterHolds),
                (Ksid.Stone, Ksid.CharacterHolds),
            })
        {
        }
    }
}
