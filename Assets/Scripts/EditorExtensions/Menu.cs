#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Assets.Scripts.Map;
using System;

public class Menu
{
    [MenuItem("MyGame/BuildLevel")]
    static void InstantiatePrefab()
    {
        var levelSource = Level.LevelSource();
        levelSource.CreateMap(null);
        var prefabStore = AssetDatabase.LoadAssetAtPath<PrefabsStore>(@"Assets\Settings\Prefabs Store.asset");
        Transform parent = GameObject.Find("LevelRoot").transform;
//        DeleteAllChildren(parent);

        foreach (var pair in levelSource.Placeables(prefabStore, Level.Mode.Statics))
        {
            var obj = PrefabUtility.InstantiatePrefab((pair.Item1 as Placeable).gameObject, parent) as GameObject;
            obj.GetComponent<Placeable>().SetPlacedPosition(pair.Item2);
        }
    }

    private static void DeleteAllChildren(Transform parent)
    {
        while (parent.childCount > 0)
        {
            var child = parent.GetChild(parent.childCount - 1);
            GameObject.Destroy(child);
        }
    }

    //[MenuItem("Examples/Instantiate Selected", true)]
    //static bool ValidateInstantiatePrefab()
    //{
    //    GameObject go = Selection.activeObject as GameObject;
    //    if (go == null)
    //        return false;

    //    return PrefabUtility.IsPartOfPrefabAsset(go);
    //}
}
#endif