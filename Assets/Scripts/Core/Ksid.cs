using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core
{
    public enum Ksid
    {
        Unknown,
        StoneBlock,
        Character,
        Catch,
        Ladder,
        SmallMonster,
        Rope,
        CharacterHolds,
        SmallMonsterHolds,
        Stone,
        SandLike,
        SandCombiner,
        StickyBomb,
        Explosive,
        ActivatesByThrow,
        Explosion,
        AffectedByExplosion,
        DamagedByExplosion,
        CausesExplosion,
        DisconnectedByCatch,
        SpFixed,
        SpMoving,
        SpNode,
        ParticleEffect,
    }

    public static class KsidX
    {
        public static bool IsChildOf(this Ksid child, Ksid parent) => Game.Instance.Ksids.IsParent(child, parent);
        public static bool IsChildOfOrEq(this Ksid child, Ksid parent) => Game.Instance.Ksids.IsParentOrEqual(child, parent);
    }
}
