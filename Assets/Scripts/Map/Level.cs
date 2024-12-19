using Assets.Scripts.Bases;
using Assets.Scripts.Map;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Level : MonoBehaviour
{
    [SerializeField]
    private LevelLabel[] PlaceablesRoots = default;
    public LevelName LevelName;
    public int posx;
    public int posy;


    internal Map Map { get; private set; }
    private LevelBase levelSource;

    public void Create(Map map, LvlBuildMode buildMode, LevelName debugLevel)
    {
        AssignMap(map);
        if (LevelName == LevelName.DebugLvl)
            LevelName = debugLevel;
        levelSource = LevelPairing.Get(LevelName);

        foreach (var root in PlaceablesRoots)
        {
            if (root.gameObject.activeInHierarchy)
            {
                foreach (var p in root.GetComponentsInChildren<Placeable>())
                {
                    p.PlaceToMap(Map);
                }
            }
        }

        foreach (var pair in levelSource.Placeables(Game.Instance.PrefabsStore, buildMode, new Vector2Int(posx, posy)))
        {
            pair.Item1.Instantiate(Map, PlaceablesRoots[0].transform, pair.Item2);
        }
    }

    private void AssignMap(Map map)
    {
        Map = map;
        foreach (var root in PlaceablesRoots)
        {
            root.Map = map;
        }
    }

#if UNITY_EDITOR
    internal void InstantiateInEditor(PrefabsStore prefabStore)
    {
        foreach (var pair in LevelPairing.Get(LevelName).Placeables(prefabStore, LvlBuildMode.Statics, new Vector2Int(posx, posy)))
        {
            var obj = UnityEditor.PrefabUtility.InstantiatePrefab((pair.Item1 as Placeable).gameObject, PlaceablesRoots[0].transform) as GameObject;
            obj.GetComponent<Placeable>().SetPlacedPosition(pair.Item2);
        }
    }
#endif
}
