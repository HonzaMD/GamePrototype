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

        public LvlBuildMode BuildMode;

        public MapSettings[] Settings;
        [NonSerialized]
        public Map[] Maps;

        private readonly Dictionary<string, Map> scenesToLoad = new();
        public bool IsWorking;

        private void Awake()
        {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
        }

        public void CreateMaps(Ksids ksids)
        {
            Debug.Assert(Maps == null, "Cekal jsem ze mapy budou prazdne");
            CellList.CheckEmpty();
            Maps = new Map[6];

            for (int f = 0; f < WorldsCount; f++)
            {
                if (Settings.Length > f && Settings[f] != null)
                {
                    Maps[f] = new(Settings[f], ksids);
                    Game.Map = Maps[f]; // TODO odebrat

                    foreach (var scene in Settings[f].Scenes)
                        scenesToLoad.Add(scene, Maps[f]);
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

                level.Create(map, BuildMode);
            }

            IsWorking = scenesToLoad.Count > 0;
        }
    }

    public enum LvlBuildMode
    {
        All,
        Statics,
        Dynamics,
    }

}
