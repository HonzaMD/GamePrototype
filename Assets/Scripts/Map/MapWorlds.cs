using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Assets.Scripts.Map
{
    public class MapWorlds : MonoBehaviour
    {
        public const int WorldsCount = 6;
        public const int WorldOffset = 1000;
        private const float MarginOffset = 200;
        private const float PosToMapId = 1 / (float)WorldOffset;

        public LvlBuildMode BuildMode;
        public LevelName DebugLevel;

        public MapSettings[] Settings;
        [NonSerialized]
        public readonly Map[] Maps = new Map[WorldsCount];

        public Map SelectedMap { get; set; }

        private readonly Dictionary<string, Map> scenesToLoad = new();
        [NonSerialized]
        public bool IsWorking;

        private readonly Queue<(int Pos, int Map)> candidatesQ = new();

        private void Awake()
        {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        public Map MapFromPos(float posX)
        {
            int id = (int)((posX + MarginOffset) * PosToMapId);
            id = Math.Clamp(id, 0, WorldsCount - 1);
            return Maps[id];
        }

        public void CreateMaps(Ksids ksids)
        {
            CellList.CheckEmpty();

            for (int f = 0; f < WorldsCount; f++)
            {
                Debug.Assert(Maps[f] == null, "Cekal jsem ze mapy budou prazdne");

                if (Settings.Length > f && Settings[f] != null)
                {
                    Maps[f] = new(Settings[f], ksids, f, this);
                    SelectedMap ??= Maps[f];

                    foreach (var scene in Settings[f].Scenes)
                        scenesToLoad.Add(scene, Maps[f]);
                }
                else
                {
                    Maps[f] = new(f * WorldOffset * 2, 0, 144, 144, ksids, f, this);
                }
            }

            IsWorking = scenesToLoad.Count > 0;
            StartSceneLoad();
        }

        private void StartSceneLoad()
        {
            foreach (var s in scenesToLoad.ToArray())
            {
                var scene = SceneManager.GetSceneByName(s.Key);
                if (scene != null && scene.isLoaded)
                {
                    SceneManager_sceneLoaded(scene, LoadSceneMode.Additive);
                }
                else
                {
                    SceneManager.LoadSceneAsync(s.Key, LoadSceneMode.Additive);
                }
            }
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scenesToLoad.TryGetValue(scene.name, out var map))
            {
                scenesToLoad.Remove(scene.name);
                Level level = null;

                var list = ListPool<GameObject>.Rent();
                scene.GetRootGameObjects(list);
                foreach (var obj in list)
                    if (obj.TryGetComponent<Level>(out level))
                        break;
                list.Return();

                level.Create(map, BuildMode, DebugLevel);
            }

            IsWorking = scenesToLoad.Count > 0;
        }

        public void ProcessCellStateTests(int count)
        {
            for (int f = 0; f < count && candidatesQ.Count > 0; f++)
            {
                (var cellPos, var mapId) = candidatesQ.Dequeue();
                Maps[mapId].ProcessCellStateTest(cellPos);
            }
        }

        public void EnqueueCellStateTest(int cellPos, int mapId) => candidatesQ.Enqueue((cellPos, mapId));
    }

    public enum LvlBuildMode
    {
        All,
        Statics,
        Dynamics,
    }

}
