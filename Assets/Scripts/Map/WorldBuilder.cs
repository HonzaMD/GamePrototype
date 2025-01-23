﻿using Assets.Scripts.Bases;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Assets.Scripts.Map
{
    [RequireComponent(typeof(LevelLabel))]
    public class WorldBuilder : MonoBehaviour
    {
        public Transform BakedReflections;

        private Map map;
        private LvlBuildMode buildMode;
        private LevelName debugLevel;
        private MapSettings mapSettings;
        private readonly List<Level> levels = new();
        private string activationWorlds;
        private Transform[] bakedReflectionScenarios;

        public void Build(Map map, MapWorlds mapWorlds, MapSettings mapSettings, int seed)
        {
            this.map = map;
            this.mapSettings = mapSettings;
            buildMode = mapWorlds.BuildMode;
            debugLevel = mapWorlds.DebugLevel;
            map.WorldBuilder = this;

            Random.InitState(seed);
            InitActivationWords();
            DoSequence(transform);
            CreateLevels(Game.Instance.PrefabsStore);
        }

        public void BuildInEditor(LvlBuildMode buildMode, PrefabsStore prefabStore)
        {
            this.buildMode = buildMode;
            Random.InitState(0);
            DoSequence(transform);
            CreateLevels(prefabStore);
            levels.Clear();
        }

        private void InitActivationWords()
        {
            activationWorlds = mapSettings.ActivationWords;
        }

        private void DoSequence(Transform transform)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child.TryGetComponent<Level>(out var level))
                {
                    PrepareLevel(level);
                }
                else if (buildMode == LvlBuildMode.All)
                {
                    var lvls = ListPool<Level>.Rent();
                    child.GetComponentsInLevel1Children(lvls);
                    level = TryFindLevelByAVs(lvls);
                    if (!level)
                        level = ChooseRandomly(lvls);
                    lvls.Return();

                    if (level)
                        PrepareLevel(level);
                }
                else
                {
                    DoSequence(child.transform);
                }
            }
        }

        private Level TryFindLevelByAVs(List<Level> lvls)
        {
            foreach (var level in lvls)
            {
                if (level.MatchAVs(activationWorlds))
                    return level;
            }
            return null;
        }

        private Level ChooseRandomly(List<Level> lvls)
        {
            if (lvls.Count == 0)
                return null;
            int range = lvls.Sum(l => l.BuildProbability);
            int rnd = Random.Range(0, range);

            foreach (var level in lvls)
            {
                if (rnd < level.BuildProbability)
                    return level;
                rnd -= level.BuildProbability;
            }

            return null;
        }

        private void PrepareLevel(Level level)
        {
            if (level.MatchBuildMode(buildMode))
            {
                if (Application.isPlaying)
                    level.PrepareRoots(map, Random.Range(int.MinValue, int.MaxValue));
                levels.Add(level);
                DoSequence(level.transform);
            }
        }

        private void CreateLevels(PrefabsStore prefabStore)
        {
            foreach (Level level in levels)
                level.Create(buildMode, debugLevel, transform.position, prefabStore);
        }

        public void SetupBakedReflections(int numScenarios, RegionMap lightVariantMap)
        {
            bakedReflectionScenarios = new Transform[numScenarios];
            for (int i = 0; i < numScenarios; i++)
            {
                Transform rootA = BakedReflections.Find($"ScA{i}");
                Transform rootB = BakedReflections.Find($"ScB{i}");
                bakedReflectionScenarios[i] = rootA;

                var list = ListPool<Transform>.Rent();

                MarkProbesToMove(lightVariantMap, rootA, list);
                MarkProbesToMove(lightVariantMap, rootB, list);

                foreach (Transform tr in list)
                {
                    tr.SetParent(tr.parent == rootA ? rootB : rootA, true);
                }

                list.Return();
            }
        }

        private static void MarkProbesToMove(RegionMap lightVariantMap, Transform root, List<Transform> list)
        {
            for (int j = 0; j < root.childCount; j++)
                if (lightVariantMap.Find(root.GetChild(j)))
                    list.Add(root.GetChild(j));
        }

        public void SetLightScenario(int scenario)
        {
            for (int i = 0; i < bakedReflectionScenarios.Length; i++)
            {
                bakedReflectionScenarios[i].gameObject.SetActive(i == scenario);
            }
        }
    }
}

