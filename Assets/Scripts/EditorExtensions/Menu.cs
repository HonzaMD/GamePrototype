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
        var prefabStore = AssetDatabase.LoadAssetAtPath<PrefabsStore>(@"Assets\Settings\Prefabs Store.asset");
        var levels = GameObject.FindObjectsOfType<Level>();
//        DeleteAllChildren(parent);

        foreach (var level in levels)
        {
            level.InstantiateInEditor(prefabStore);
        }
    }

    [MenuItem("MyGame/ClearLevel")]
    static void InstantiateClearLevel()
    {
        var prefabStore = AssetDatabase.LoadAssetAtPath<PrefabsStore>(@"Assets\Settings\Prefabs Store.asset");
        var levels = GameObject.FindObjectsOfType<Level>();
        //        DeleteAllChildren(parent);

        foreach (var level in levels)
        {
            DeleteAllChildren(level.transform);

        }
    }

    [MenuItem("MyGame/Kill Selected")]
    static void KillSelected()
    {
        foreach (var obj in Selection.transforms)
        {
            if (Label.TryFind(obj, out var label) && label.IsAlive)
            {
                label.Kill();
            }
        }
    }

    private static void DeleteAllChildren(Transform parent)
    {
        var tempArray = new GameObject[parent.transform.childCount];

        for (int i = 0; i < tempArray.Length; i++)
        {
            tempArray[i] = parent.transform.GetChild(i).gameObject;
        }

        foreach (var child in tempArray)
        {
            GameObject.DestroyImmediate(child);
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