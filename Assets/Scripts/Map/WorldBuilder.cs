using Assets.Scripts.Bases;
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
        private Map map;
        private MapWorlds mapWorlds;
        private MapSettings mapSettings;
        private List<Level> levels = new();
        private string activationWorlds;

        internal void Build(Map map, MapWorlds mapWorlds, MapSettings mapSettings, int seed)
        {
            this.map = map;
            this.mapWorlds = mapWorlds;
            this.mapSettings = mapSettings;
            InitActivationWords();
            Random.InitState(seed);
            DoSequence(transform);
            CreateLevels();
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
                else if (mapWorlds.BuildMode == LvlBuildMode.All)
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
            return lvls[Random.Range(0, lvls.Count)];
        }

        private void PrepareLevel(Level level)
        {
            if (level.MatchBuildMode(mapWorlds.BuildMode))
            {
                level.PrepareRoots(map, Random.Range(int.MinValue, int.MaxValue));
                levels.Add(level);
                DoSequence(level.transform);
            }
        }

        private void CreateLevels()
        {
            foreach (Level level in levels)
                level.Create(mapWorlds.BuildMode, mapWorlds.DebugLevel, transform.position);
        }
    }
}

