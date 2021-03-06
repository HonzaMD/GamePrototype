﻿using Assets.Scripts.Map;
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

    void Start()
    {
        CellList.CheckEmpty();

        var levelSource = new Level1();
        Map = levelSource.CreateMap(Game.Ksids);
        Game.Map = Map;

        foreach (var pair in levelSource.Placeables(PrefabsStore))
        {
            pair.Item1.Instantiate(Map, transform, pair.Item2);
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

#if UNITY_EDITOR
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
            Game.Map = Map;

            foreach (var p in mapContentToserialize)
            {
                Map.Add(p, true);
            }
            mapContentToserialize = null;
        }
    }
#endif

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
