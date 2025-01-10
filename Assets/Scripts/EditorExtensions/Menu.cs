#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Assets.Scripts.Map;
using System;
using UnityEngine.SceneManagement;
using Assets.Scripts.Bases;

public class Menu
{
    [MenuItem("MyGame/BuildLevelA")]
    static void BuildLevelA() => BuildLevel(LvlBuildMode.StaticsA);

    [MenuItem("MyGame/BuildLevelB")]
    static void BuildLevelB() => BuildLevel(LvlBuildMode.StaticsB);

    [MenuItem("MyGame/BuildLevelAB")]
    static void BuildLevelAB() => BuildLevel(LvlBuildMode.StaticsAB);

    private static void BuildLevel(LvlBuildMode mode)
    {
        var prefabStore = AssetDatabase.LoadAssetAtPath<PrefabsStore>(@"Assets\Settings\Prefabs Store.asset");
        var builders = GameObject.FindObjectsByType<WorldBuilder>(FindObjectsSortMode.None);

        foreach (var builder in builders)
        {
            SceneManager.SetActiveScene(builder.gameObject.scene);
            var builder2 = GameObject.Instantiate(builder);
            builder2.GetComponent<LevelLabel>().wasCloned = true;
            builder2.BuildInEditor(mode, prefabStore);
        }
    }

    [MenuItem("MyGame/ClearLevel")]
    static void InstantiateClearLevel()
    {
        var roots = GameObject.FindObjectsByType<LevelLabel>(FindObjectsSortMode.None);

        foreach (var root in roots)
        {
            if (root.wasCloned)
                GameObject.DestroyImmediate(root);
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