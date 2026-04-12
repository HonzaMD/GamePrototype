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
                (Ksid.TreeTrunk, Ksid.Catch),
                (Ksid.Rope, Ksid.CharacterHolds),
                (Ksid.SmallMonster, Ksid.CharacterHolds),
                (Ksid.InventoryItem, Ksid.CharacterHolds),
                (Ksid.Ladder, Ksid.CharacterHolds),
                (Ksid.Cannon, Ksid.CharacterHolds),
                (Ksid.TreeTrunk, Ksid.CharacterHolds),
                (Ksid.Stone, Ksid.InventoryItem),
                (Ksid.Light, Ksid.InventoryItem),
                (Ksid.StickyBomb, Ksid.InventoryItem),
                (Ksid.Chest, Ksid.InventoryItem),
                (Ksid.Knife, Ksid.InventoryItem),
                (Ksid.StickyBomb, Ksid.Explosive),
                (Ksid.StickyBomb, Ksid.ActivatesByThrow),
                (Ksid.PoisonGas, Ksid.ActivatesByThrow),
                (Ksid.Knife, Ksid.ActivatesByThrow),
                (Ksid.StickyBomb, Ksid.DisconnectedByCatch),                
                (Ksid.Stone, Ksid.SandLike),
                (Ksid.Light, Ksid.SandLike),
                (Ksid.Knife, Ksid.SandLike),

                (Ksid.Light, Ksid.DamagedByExplosion),
                (Ksid.Stone, Ksid.DamagedByExplosion),
                (Ksid.Explosive, Ksid.DamagedByExplosion),
                (Ksid.SandCombiner, Ksid.DamagedByExplosion),
                (Ksid.SmallMonster, Ksid.DamagedByExplosion),
                (Ksid.Character, Ksid.DamagedByExplosion),
                (Ksid.Cannon, Ksid.DamagedByExplosion),
                (Ksid.DamagedByExplosion, Ksid.AffectedByExplosion),
                (Ksid.Character, Ksid.AffectedByExplosion),
                (Ksid.Rope, Ksid.AffectedByExplosion),
                (Ksid.SpMoving, Ksid.AffectedByExplosion),
                (Ksid.Knife, Ksid.AffectedByExplosion),
                (Ksid.Chest, Ksid.AffectedByExplosion),
                (Ksid.DamagedByExplosion, Ksid.CausesExplosion),

                (Ksid.SpFixed, Ksid.SpNode),
                (Ksid.SpMoving, Ksid.SpNode),
                (Ksid.StoneBlock, Ksid.SpFixed),
                (Ksid.StoneBlock, Ksid.Dirt),
                (Ksid.Ladder, Ksid.SpMoving),
                (Ksid.Cannon, Ksid.SpMoving),
                (Ksid.TreeTrunk, Ksid.SpMoving),
                (Ksid.TreeTrunk, Ksid.BlocksTreeRoots),
                (Ksid.SpNode, Ksid.SpNodeOrSandCombiner),
                (Ksid.SandCombiner, Ksid.SpNodeOrSandCombiner),

                (Ksid.Character, Ksid.HasInventory),
                (Ksid.Chest, Ksid.HasInventory),
                (Ksid.Chest, Ksid.InventoryAsObj),
                (Ksid.Knife, Ksid.HoldsAtHandle),
                (Ksid.Knife, Ksid.ActivatesInHand),

                (Ksid.Character, Ksid.DamagedByImpact),
                (Ksid.SmallMonster, Ksid.DamagedByImpact),
                (Ksid.Light, Ksid.DamagedByImpact),
                (Ksid.Cannon, Ksid.DamagedByImpact),

                (Ksid.Knife, Ksid.DealsKnifeDamage),
                (Ksid.Character, Ksid.DamagedByKnife),
                (Ksid.SmallMonster, Ksid.DamagedByKnife),

                (Ksid.Character, Ksid.DamagedByContact),
                (Ksid.SmallMonster, Ksid.DamagedByContact),
                (Ksid.Ladder, Ksid.DamagedByContact),
                (Ksid.Chest, Ksid.DamagedByContact),
                (Ksid.PoisonGas, Ksid.DealsContactDamage),
                (Ksid.HotBlock, Ksid.DealsContactDamage),

                (Ksid.Character, Ksid.ActivatesCannon),
                (Ksid.SmallMonster, Ksid.ActivatesCannon),
                (Ksid.Light, Ksid.ActivatesCannon),
            })
        {
        }
    }
}
