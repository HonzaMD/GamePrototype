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

        private readonly Queue<(string, Map)> scenesToLoad = new();
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
                        scenesToLoad.Enqueue((scene, Maps[f]));
                }
            }

            IsWorking = StartSceneLoad();
        }

        private bool StartSceneLoad()
        {
            if (scenesToLoad.Count == 0)
                return false;

            SceneManager.LoadSceneAsync(scenesToLoad.Peek().Item1, LoadSceneMode.Additive);
            return true;
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scenesToLoad.Count > 0 && scenesToLoad.Peek().Item1 == scene.name)
            {
                var map = scenesToLoad.Dequeue().Item2;
                Level level = null;

                var list = ListPool<GameObject>.Rent();
                scene.GetRootGameObjects(list);
                foreach (var obj in list)
                    if (obj.TryGetComponent<Level>(out level))
                        break;
                list.Return();

                IsWorking = StartSceneLoad();

                level.Create(map, BuildMode);
            }
        }
    }

    public enum LvlBuildMode
    {
        All,
        Statics,
        Dynamics,
    }

}
