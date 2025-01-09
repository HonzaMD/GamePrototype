using Assets.Scripts.Bases;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour
{
    public string ActivationWord;
    public LevelName LevelName;
    [SerializeField]
    private LevelLabel[] PlaceablesRoots = default;
    public LevelLabel CloseSide;
    public LevelLabel FarSide;
    public int localCellsX;
    public int localCellsY;
    public bool LightVariantA, LightVariantB;

    //[NonSerialized]
    //public int CellsX;
    //[NonSerialized]
    //public int CellsY;



    internal Map Map { get; private set; }
    private LevelBase levelSource;
    private int WorldNum => Map.Id;
    private int seed;

    public void PrepareRoots(Map map, int seed)
    {
        Map = map;
        this.seed = seed;
        for (int i = 0; i < PlaceablesRoots.Length; i++)
        {
            PrepareRoot(ref PlaceablesRoots[i]);
        }
        if (CloseSide)
            PrepareRoot(ref CloseSide);
        if (FarSide)
            PrepareRoot(ref FarSide);
    }

    private void PrepareRoot(ref LevelLabel root)
    {
        if (root.Map != null)
        {
            root = Instantiate(root);
            root.wasCloned = true;
            root.Map = Map;
        }
        else
        {
            root.Map = Map;
            root.transform.position += Vector3.right * WorldNum * MapWorlds.WorldOffset;
            root.gameObject.SetActive(true);
        }
    }

    public void Create(LvlBuildMode buildMode, LevelName debugLevel, Vector3 worldBuilderPos)
    {
        MoveToWorldPos(worldBuilderPos);

        if (LevelName == LevelName.DebugLvl)
            LevelName = debugLevel;
        levelSource = LevelPairing.Get(LevelName);

        foreach (var root in PlaceablesRoots)
        {
            foreach (var p in root.GetComponentsInChildren<Placeable>())
            {
                p.PlaceToMap(Map);
            }
        }

        if (levelSource != null)
        {
            foreach (var pair in levelSource.Placeables(Game.Instance.PrefabsStore, buildMode, PlaceablesRoots[0].transform, new Vector2Int(localCellsX, localCellsY)))
            {
                pair.Item1.Instantiate(Map, PlaceablesRoots[0].transform, pair.Item2);
            }
        }

        MakeCloseSideInvisible();
    }

    private void MoveToWorldPos(Vector3 worldBuilderPos)
    {
        Vector3 delta = transform.position - worldBuilderPos;
        foreach (var root in PlaceablesRoots)
            root.transform.position += delta;
        if (CloseSide)
            CloseSide.transform.position += delta;
        if (FarSide)
            FarSide.transform.position += delta;

        //var delta2 = delta.XY() + Vector2.right * WorldNum * MapWorlds.WorldOffset;
        //delta2.Scale(Map.CellSize2dInv);
        //var intDelta = Vector2Int.FloorToInt(delta2);
        //CellsX = localCellsX + intDelta.x;
        //CellsY = localCellsY + intDelta.y;
    }

    private void MakeCloseSideInvisible()
    {
        if (CloseSide)
        {
            var renderers = ListPool<MeshRenderer>.Rent();
            CloseSide.GetComponentsInChildren<MeshRenderer>(renderers);

            foreach (var renderer in renderers)
            {
                var obj2 = GameObject.Instantiate(renderer, renderer.transform.parent);
                obj2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                obj2.gameObject.layer = 0;
            }

            renderers.Return();
        }
    }


#if UNITY_EDITOR
    internal void InstantiateInEditor(PrefabsStore prefabStore)
    {
        foreach (var pair in LevelPairing.Get(LevelName).Placeables(prefabStore, LvlBuildMode.StaticsAB, PlaceablesRoots[0].transform, new Vector2Int(localCellsX, localCellsY)))
        {
            var obj = UnityEditor.PrefabUtility.InstantiatePrefab((pair.Item1 as Placeable).gameObject, PlaceablesRoots[0].transform) as GameObject;
            obj.GetComponent<Placeable>().SetPlacedPosition(pair.Item2);
        }
    }
#endif

    internal bool MatchAVs(string activationWorlds)
    {
        if (string.IsNullOrEmpty(ActivationWord) || string.IsNullOrEmpty(activationWorlds))
            return false;

        int start = 0;
        for (; ; )
        {
            if (start + ActivationWord.Length > activationWorlds.Length)
                return false;
            int i = activationWorlds.IndexOf(ActivationWord, start, StringComparison.Ordinal);
            if (i == -1)
                return false;
            int i2 = i + ActivationWord.Length;
            if ((i == 0 || activationWorlds[i - 1] == ',') && (i2 == activationWorlds.Length || activationWorlds[i2] == ','))
                return true;
            start = i2+1;
        }
    }

    internal bool MatchBuildMode(LvlBuildMode buildMode)
    {
        return buildMode switch
        {
            LvlBuildMode.All => true,
            LvlBuildMode.StaticsA => LightVariantA,
            LvlBuildMode.StaticsB => LightVariantB,
            LvlBuildMode.StaticsAB => LightVariantA || LightVariantB,
            _ => throw new InvalidOperationException("Neplatny case")
        };
    }
}
