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
    public int BuildProbability = 10;


    internal Map Map { get; private set; }
    private LevelBase levelSource;
    private int WorldNum => Map.Id;
    private int seed;

    private delegate void RootWorker<T>(ref LevelLabel root, T prm);
    private readonly RootWorker<bool> prepareRootA;
    private readonly RootWorker<bool> mapSpecialObjectsA;
    private readonly RootWorker<Vector3> translatePosA;
    private readonly RootWorker<bool> cloneRootA;

    public Level()
    {
        prepareRootA = PrepareRoot;
        mapSpecialObjectsA = MapSpecialObjects;
        translatePosA = (ref LevelLabel root, Vector3 delta) => root.transform.position += delta;
        cloneRootA = CloneRoot;
    }

    public void PrepareRoots(Map map, int seed)
    {
        Map = map;
        this.seed = seed;
        IterateRoots(prepareRootA, false);
    }

    private void IterateRoots<T>(RootWorker<T> action, T prm)
    {
        if (PlaceablesRoots != null)
        {
            for (int i = 0; i < PlaceablesRoots.Length; i++)
            {
                action(ref PlaceablesRoots[i], prm);
            }
        }
        if (CloseSide)
            action(ref CloseSide, prm);
        if (FarSide)
            action(ref FarSide, prm);
    }



    private void PrepareRoot(ref LevelLabel root, bool prm)
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
        IterateRoots(mapSpecialObjectsA, false);

        if (PlaceablesRoots != null)
        {
            foreach (var root in PlaceablesRoots)
            {
                foreach (var p in root.GetComponentsInChildren<Placeable>())
                {
                    p.PlaceToMap(Map, false);
                }
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


    private void MapSpecialObjects(ref LevelLabel parent, bool prm)
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
                IterateRoots(cloneRootA, false);

            IterateRoots(translatePosA, delta);
        } 
        else if (!Application.isPlaying)
        {
            IterateRoots(cloneRootA, true);
        }
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
            int i = ActivationWord.IndexOf(',', start);
            if (i > 0)
            {
                if (MatchAvs1(activationWorlds, ActivationWord.AsSpan(start, i-start)))
                    return true;
                start = i + 1;
            }
            else
            {
                return MatchAvs1(activationWorlds, ActivationWord.AsSpan(start));
            }
        }
    }

    private bool MatchAvs1(string activationWorlds, ReadOnlySpan<char> myWord)
    {
        if (myWord.Length == 0)
            return false;

        int start = 0;
        for (; ; )
        {
            if (start + myWord.Length > activationWorlds.Length)
                return false;
            int i = activationWorlds.AsSpan(start).IndexOf(myWord, StringComparison.Ordinal);
            if (i == -1)
                return false;
            int i2 = i + myWord.Length;
            if ((i == 0 || activationWorlds[i - 1] == ',') && (i2 == activationWorlds.Length || activationWorlds[i2] == ','))
                return true;
            start = i2 + 1;
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
