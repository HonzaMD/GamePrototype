using Assets.Scripts.Bases;
using Assets.Scripts.Map;
using Assets.Scripts.Stuff;
using Assets.Scripts.Utils;
using System;
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

    public void Create(LvlBuildMode buildMode, LevelName debugLevel, Vector3 worldBuilderPos, PrefabsStore prefabStore)
    {
        MoveToWorldPos(worldBuilderPos);

        if (LevelName == LevelName.DebugLvl)
            LevelName = debugLevel;
        levelSource = LevelPairing.Get(LevelName);

        if (Application.isPlaying)
        {
            CreateInPlay(buildMode);
        }
        else
        {
            CreateInEditor(buildMode, prefabStore);
        }
    }


    private void CreateInPlay(LvlBuildMode buildMode)
    {
        MapSpecialObjects();

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

    private void MapSpecialObjects()
    {
        for (int i = 0; i < PlaceablesRoots.Length; i++)
        {
            MapSpecialObjects(PlaceablesRoots[i]);
        }
        if (CloseSide)
            MapSpecialObjects(CloseSide);
        if (FarSide)
            MapSpecialObjects(FarSide);
    }

    private void MapSpecialObjects(LevelLabel parent)
    {
        var regions = ListPool<LightVariantRegion>.Rent();
        parent.GetComponentsInChildren(regions);
        foreach (var region in regions) 
        {
            var v1 = region.transform.position.XY();
            var v2 = v1 + new Vector2(region.sizeX, region.sizeY);
            Map.LightVariantMap.Add(v1.x, v1.y, v2.x, v2.y);
        }
        regions.Return();

        var rps = ListPool<ReflectionProbe>.Rent();
        parent.GetComponentsInChildren(rps);
        foreach (var rp in rps)
            Map.ReflectionProbes.Add(rp);
        rps.Return();
    }

    private void CreateInEditor(LvlBuildMode buildMode, PrefabsStore prefabStore)
    {
#if UNITY_EDITOR
        if (levelSource != null)
        {
            if (!PlaceablesRoots[0].wasCloned)
                CloneRoot(ref PlaceablesRoots[0], false);
            foreach (var pair in levelSource.Placeables(prefabStore, buildMode, PlaceablesRoots[0].transform, new Vector2Int(localCellsX, localCellsY)))
            {
                var obj = UnityEditor.PrefabUtility.InstantiatePrefab((pair.Item1 as Placeable).gameObject, PlaceablesRoots[0].transform) as GameObject;
                obj.GetComponent<Placeable>().SetPlacedPosition(pair.Item2);
            }
        }
#endif
    }

    private void MoveToWorldPos(Vector3 worldBuilderPos)
    {
        Vector3 delta = transform.position - worldBuilderPos;
        if (delta != Vector3.zero)
        {
            if (!Application.isPlaying)
                CloneRoots(false);

            foreach (var root in PlaceablesRoots)
                root.transform.position += delta;
            if (CloseSide)
                CloseSide.transform.position += delta;
            if (FarSide)
                FarSide.transform.position += delta;
        } 
        else if (!Application.isPlaying)
        {
            CloneRoots(true);
        }
    }

    private void CloneRoots(bool onlyIfInactive)
    {
        for (int i = 0; i < PlaceablesRoots.Length; i++)
        {
            CloneRoot(ref PlaceablesRoots[i], onlyIfInactive);
        }
        if (CloseSide)
            CloneRoot(ref CloseSide, onlyIfInactive);
        if (FarSide)
            CloneRoot(ref FarSide, onlyIfInactive);
    }


    private void CloneRoot(ref LevelLabel root, bool onlyIfInactive)
    {
        if (!onlyIfInactive || !root.gameObject.activeInHierarchy)
        {
            root = Instantiate(root);
            root.wasCloned = true;
            root.gameObject.SetActive(true);
        }
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
