using Assets.Scripts.Map;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Level : MonoBehaviour, ISerializationCallbackReceiver
{
    public Game Game;
    [SerializeField]
    private PrefabsStore PrefabsStore = default;
    [SerializeField]
    private Placeable[] AdditionalPlaceables = default;

    internal Map Map { get; private set; }
    private bool mapCreated;
    private List<Placeable> mapContentToserialize;
    private MapSettings mapSettings;

    void Awake()
    {
        CellList.CheckEmpty();

        var levelSource = new Level1();
        Map = levelSource.CreateMap(Game.Ksids);

        foreach (var pair in levelSource.Placeables(PrefabsStore))
        {
            var p = Instantiate(pair.Item1, transform);
            p.transform.localPosition = pair.Item2;
            Map.Add(p);
        }

        if (AdditionalPlaceables != null)
        {
            foreach (var p in AdditionalPlaceables)
            {
                Map.Add(p);
            }
        }
        mapCreated = true;
    }

    public void OnAfterDeserialize()
    {
    }

    void OnEnable()
    {
        AssemblyReloadEvents.afterAssemblyReload += AssemblyReloadEvents_afterAssemblyReload;
    }

    void OnDisable()
    {
        AssemblyReloadEvents.afterAssemblyReload -= AssemblyReloadEvents_afterAssemblyReload;
    }

    private void AssemblyReloadEvents_afterAssemblyReload()
    {
        if (mapContentToserialize != null)
        {
            if (Map != null)
                throw new InvalidOperationException("Divnost, cekal jsem ze Map bude null!");
            CellList.CheckEmpty();
            Map = new Map(mapSettings, Game.Ksids);

            foreach (var p in mapContentToserialize)
            {
                Map.Add(p, true);
            }
            mapContentToserialize = null;
        }
    }

    public void OnBeforeSerialize()
    {
        if (mapCreated)
        {
            mapSettings = Map.Settings;
            mapContentToserialize = new List<Placeable>();
            Map.GetEverything(mapContentToserialize);
        }
    }
}
