using Assets.Scripts.Bases;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Stuff;
using UnityEngine;

[CreateAssetMenu]
public class PrefabsStore : ScriptableObject
{
    public Placeable Block;
    public PlankSegment LadderSegment;
    public Placeable SmallMonster;
    public Placeable RopeSegment;
    public Placeable Stone;
    public Placeable Gravel;
    public Placeable StickyBomb;
    public RbLabel RbBase;
    public SandCombiner SandCombiner;
    public Explosion Explosion;
    public RbJoint RbJoint;
    public ParticleEffect ParticleEffect;
    public Placeable PointLight;
    public GameObject DebugVisibility;
    public Occluder Occluder;
    public Inventory Inventory;
}