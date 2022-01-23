using Assets.Scripts.Bases;
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
    public RbLabel Stone;
    public RbLabel Gravel;
    public RbLabel RbBase;
    public SandCombiner SandCombiner;
}