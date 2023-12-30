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
                (Ksid.Rope, Ksid.CharacterHolds),
                (Ksid.SmallMonster, Ksid.CharacterHolds),
                (Ksid.Stone, Ksid.CharacterHolds),
                (Ksid.Light, Ksid.CharacterHolds),
                (Ksid.StickyBomb, Ksid.CharacterHolds),
                (Ksid.Ladder, Ksid.CharacterHolds),
                (Ksid.StickyBomb, Ksid.Explosive),
                (Ksid.StickyBomb, Ksid.ActivatesByThrow),
                (Ksid.StickyBomb, Ksid.DisconnectedByCatch),                
                (Ksid.Stone, Ksid.SandLike),
                (Ksid.Light, Ksid.SandLike),

                (Ksid.Light, Ksid.DamagedByExplosion),
                (Ksid.Stone, Ksid.DamagedByExplosion),
                (Ksid.Explosive, Ksid.DamagedByExplosion),
                (Ksid.SandCombiner, Ksid.DamagedByExplosion),
                (Ksid.SmallMonster, Ksid.DamagedByExplosion),
                (Ksid.DamagedByExplosion, Ksid.AffectedByExplosion),
                (Ksid.Character, Ksid.AffectedByExplosion),
                (Ksid.Rope, Ksid.AffectedByExplosion),
                (Ksid.SpMoving, Ksid.AffectedByExplosion),
                (Ksid.DamagedByExplosion, Ksid.CausesExplosion),

                (Ksid.SpFixed, Ksid.SpNode),
                (Ksid.SpMoving, Ksid.SpNode),
                (Ksid.StoneBlock, Ksid.SpFixed),                
                (Ksid.Ladder, Ksid.SpMoving),
                (Ksid.SpNode, Ksid.SpNodeOrSandCombiner),
                (Ksid.SandCombiner, Ksid.SpNodeOrSandCombiner),
            })
        {
        }
    }
}
