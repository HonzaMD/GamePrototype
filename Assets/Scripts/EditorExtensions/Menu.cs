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