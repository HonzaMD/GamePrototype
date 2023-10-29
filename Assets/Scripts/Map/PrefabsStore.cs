using Assets.Scripts.Bases;
using Assets.Scripts.Stuff;
using UnityEngine;

[CreateAssetMenu]
public class PrefabsStore : ScriptableObject
{
    public Placeable Block;
    public Plank Ladder;
    public PlankSegment LadderSegment;
    public Placeable SmallMonster;
    public Rope Rope;
    public Placeable RopeSegment;
    public Placeable Stone;
    public Placeable Gravel;
    public Placeable StickyBomb;
    public RbLabel RbBase;
    public SandCombiner SandCombiner;
    public Explosion Explosion;
    public RbJoint RbJoint;
}