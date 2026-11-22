#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Konverze komponenty na jiny typ v ramci polymorfni hierarchie (napr. PlaceableSibling <> Placeable,
// CrawlerStyle <> FlyingStyle). Prohodi m_Script na te same komponente => zachova se fileID
// (odkazy zustanou platne) a Unity prenese hodnoty poli se stejnym nazvem.
public class ComponentTypeConverter : EditorWindow
{
    // Koreny hierarchii, ve kterych lze konvertovat. Pro dalsi hierarchii pridej typ + CONTEXT MenuItem.
    private static readonly Type[] HierarchyRoots = { typeof(Placeable), typeof(ChLegsArms) };

    private static readonly HashSet<string> InternalProps = new()
    {
        "m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset",
        "m_GameObject", "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier",
    };

    private class Candidate
    {
        public Type Type;
        public MonoScript Script;
        public string Label;
    }

    [SerializeField] private MonoBehaviour component;

    private Type rootType;
    private readonly List<Candidate> candidates = new();
    private string[] candidateLabels;
    private int selected = -1;

    // nahled pro vybrany typ
    private int previewFor = -2;
    private readonly List<string> lostFields = new();
    private readonly List<string> newFields = new();
    private readonly List<Type> requiredToAdd = new();
    private readonly List<string> brokenRequirements = new();
    private readonly List<string> brokenReferences = new();
    private bool referencesScanned;

    private Vector2 scroll;

    [MenuItem("CONTEXT/Placeable/Convert Type...")]
    [MenuItem("CONTEXT/ChLegsArms/Convert Type...")]
    private static void Open(MenuCommand command)
    {
        var window = CreateInstance<ComponentTypeConverter>();
        window.titleContent = new GUIContent("Convert Component Type");
        window.minSize = new Vector2(460, 420);
        window.component = command.context as MonoBehaviour;
        window.Init();
        window.ShowUtility();
    }

    private void OnEnable()
    {
        if (component != null && rootType == null)
            Init();
    }

    private void Init()
    {
        candidates.Clear();
        previewFor = -2;
        if (component == null)
            return;

        var type = component.GetType();
        rootType = HierarchyRoots.FirstOrDefault(r => r.IsAssignableFrom(type));
        if (rootType == null)
            return;

        var scripts = new Dictionary<Type, MonoScript>();
        foreach (var s in MonoImporter.GetAllRuntimeMonoScripts())
        {
            var c = s.GetClass();
            if (c != null && rootType.IsAssignableFrom(c))
                scripts[c] = s;
        }

        // DFS od korene => poradi i odsazeni odpovida hierarchii
        var all = TypeCache.GetTypesDerivedFrom(rootType).Append(rootType).ToList();
        void Add(Type t, int depth)
        {
            if (!t.IsAbstract && !t.IsGenericTypeDefinition && t != type && scripts.TryGetValue(t, out var script))
                candidates.Add(new Candidate { Type = t, Script = script, Label = new string(' ', depth * 4) + t.Name });
            foreach (var child in all.Where(c => c.BaseType == t).OrderBy(c => c.Name))
                Add(child, depth + 1);
        }
        Add(rootType, 0);

        candidateLabels = candidates.Select(c => c.Label).ToArray();
        selected = candidates.Count > 0 ? 0 : -1;
    }

    private string BlockReason()
    {
        if (component == null)
            return "Komponenta už neexistuje.";
        if (rootType == null)
            return $"{component.GetType().Name} nepatří do žádné podporované hierarchie.";
        if (PrefabUtility.IsPartOfPrefabInstance(component))
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(component);
            return $"Komponenta je součástí instance prefabu – konvertuj ji přímo v prefabu {(source ? source.transform.root.name : "")}.";
        }
        if (candidates.Count == 0)
            return "Žádný vhodný cílový typ.";
        return null;
    }

    private void OnGUI()
    {
        string block = BlockReason();
        if (component != null)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("GameObject", component.gameObject, typeof(GameObject), true);
                EditorGUILayout.TextField("Aktuální typ", component.GetType().Name);
            }
        }
        if (block != null)
        {
            EditorGUILayout.HelpBox(block, MessageType.Error);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Zavřít"))
            {
                Close();
                GUIUtility.ExitGUI();
            }
            return;
        }

        selected = EditorGUILayout.Popup("Cílový typ", selected, candidateLabels);
        var target = candidates[selected];
        if (previewFor != selected)
            BuildPreview(target.Type);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawList("Ztratí se (pole v cílovém typu nejsou)", lostFields, MessageType.Warning);
        DrawList("Nová pole (dostanou defaultní hodnotu)", newFields, MessageType.Info);
        DrawList("Přidají se komponenty (RequireComponent cílového typu)", requiredToAdd.Select(t => t.Name).ToList(), MessageType.Info);
        DrawList("Porušené RequireComponent jiných komponent", brokenRequirements, MessageType.Error);

        EditorGUILayout.Space(8);
        if (!referencesScanned)
        {
            if (GUILayout.Button("Zkontrolovat typované odkazy v assetech"))
                ScanReferences(target.Type);
        }
        else if (brokenReferences.Count == 0)
            EditorGUILayout.HelpBox("Žádné odkazy s nekompatibilním typem (prefaby, ScriptableObjecty, aktuální scéna).", MessageType.Info);
        else
            DrawList("Odkazy, které budou mít špatný typ (ztratí se)", brokenReferences, MessageType.Error);
        EditorGUILayout.HelpBox("Kód typu GetComponent<StaryTyp>() kontrola neodhalí.", MessageType.None);
        EditorGUILayout.EndScrollView();

        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(100)))
            {
                Close();
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("Convert", GUILayout.Width(100)))
            {
                Convert(target);
                Close();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.Space();
    }

    private static void DrawList(string title, List<string> items, MessageType type)
    {
        if (items.Count == 0)
            return;
        EditorGUILayout.HelpBox($"{title}:\n  " + string.Join("\n  ", items), type);
    }

    private void BuildPreview(Type target)
    {
        previewFor = selected;
        lostFields.Clear();
        newFields.Clear();
        requiredToAdd.Clear();
        brokenRequirements.Clear();
        brokenReferences.Clear();
        referencesScanned = false;

        var sourceFields = GetSerializedFields(component.GetType());
        var targetFields = GetSerializedFields(target);

        // skutecne serializovana pole zdroje
        var so = new SerializedObject(component);
        var it = so.GetIterator();
        var sourceProps = new List<string>();
        for (bool enter = true; it.Next(enter); enter = false)
            if (!InternalProps.Contains(it.name))
                sourceProps.Add(it.name);

        foreach (var name in sourceProps)
        {
            if (!targetFields.TryGetValue(name, out var tf))
                lostFields.Add(name);
            else if (sourceFields.TryGetValue(name, out var sf) && sf.FieldType != tf.FieldType)
                lostFields.Add($"{name} (jiný typ: {sf.FieldType.Name} → {tf.FieldType.Name})");
        }
        foreach (var name in targetFields.Keys)
            if (!sourceProps.Contains(name))
                newFields.Add(name);

        var go = component.gameObject;
        foreach (var req in GetRequiredTypes(target))
        {
            bool satisfied = go.GetComponents<Component>().Any(c => c != null && c != component && req.IsInstanceOfType(c))
                || req.IsAssignableFrom(target);
            if (!satisfied && !requiredToAdd.Contains(req))
                requiredToAdd.Add(req);
        }

        foreach (var other in go.GetComponents<Component>())
        {
            if (other == null || other == component)
                continue;
            foreach (var req in GetRequiredTypes(other.GetType()))
            {
                if (!req.IsInstanceOfType(component) || req.IsAssignableFrom(target))
                    continue; // stara komponenta pozadavek neplnila, nebo ho plni i nova
                bool otherSatisfies = go.GetComponents<Component>().Any(c => c != null && c != component && c != other && req.IsInstanceOfType(c));
                if (!otherSatisfies)
                    brokenRequirements.Add($"{other.GetType().Name} vyžaduje {req.Name}");
            }
        }
    }

    private static IEnumerable<Type> GetRequiredTypes(Type t)
    {
        foreach (RequireComponent rc in t.GetCustomAttributes(typeof(RequireComponent), true))
        {
            if (rc.m_Type0 != null) yield return rc.m_Type0;
            if (rc.m_Type1 != null) yield return rc.m_Type1;
            if (rc.m_Type2 != null) yield return rc.m_Type2;
        }
    }

    private static Dictionary<string, FieldInfo> GetSerializedFields(Type t)
    {
        var result = new Dictionary<string, FieldInfo>();
        for (; t != null && t != typeof(MonoBehaviour); t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (f.IsNotSerialized || f.IsInitOnly)
                    continue;
                if (!f.IsPublic && !f.IsDefined(typeof(SerializeField), true) && !f.IsDefined(typeof(SerializeReference), true))
                    continue;
                if (f.Name.Contains('<')) // backing field auto-property bez [field: SerializeField]
                    continue;
                result.TryAdd(f.Name, f);
            }
        }
        return result;
    }

    // Najde ObjectReference pole odkazujici na komponentu, jejichz deklarovany typ nebude cilovy typ splnovat.
    private void ScanReferences(Type target)
    {
        referencesScanned = true;
        brokenReferences.Clear();

        var objects = new List<(string where, Object obj)>();
        string ownPath = AssetDatabase.GetAssetPath(component);
        if (!string.IsNullOrEmpty(ownPath))
        {
            var paths = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/") && (p.EndsWith(".prefab") || p.EndsWith(".asset")))
                .ToList();
            for (int i = 0; i < paths.Count; i++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Hledám odkazy", paths[i], (float)i / paths.Count))
                    break;
                if (paths[i] != ownPath && !AssetDatabase.GetDependencies(paths[i], false).Contains(ownPath))
                    continue;
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(paths[i]))
                    if (o != null)
                        objects.Add((Path.GetFileNameWithoutExtension(paths[i]), o));
            }
            EditorUtility.ClearProgressBar();
        }
        else
        {
            foreach (var root in component.gameObject.scene.GetRootGameObjects())
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                    if (c != null)
                        objects.Add((component.gameObject.scene.name, c));
        }

        foreach (var (where, obj) in objects)
        {
            var it = new SerializedObject(obj).GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference || it.objectReferenceValue != component)
                    continue;
                string typeName = ParsePPtrType(it.type);
                if (typeName != null && !IsTypeOrBase(target, typeName))
                    brokenReferences.Add($"{where}/{obj.name} ({obj.GetType().Name}).{it.propertyPath} : {typeName}");
            }
        }
    }

    private static string ParsePPtrType(string type)
    {
        // "PPtr<$PlankSegment>"
        int start = type.IndexOf('$');
        int end = type.LastIndexOf('>');
        return start >= 0 && end > start ? type.Substring(start + 1, end - start - 1) : null;
    }

    private static bool IsTypeOrBase(Type t, string name)
    {
        for (; t != null; t = t.BaseType)
            if (t.Name == name)
                return true;
        return false;
    }

    private void Convert(Candidate target)
    {
        Undo.SetCurrentGroupName($"Convert to {target.Type.Name}");
        int undoGroup = Undo.GetCurrentGroup();
        var go = component.gameObject;
        string oldName = component.GetType().Name;

        var so = new SerializedObject(component);
        so.FindProperty("m_Script").objectReferenceValue = target.Script;
        so.ApplyModifiedProperties();

        foreach (var req in requiredToAdd)
            Undo.AddComponent(go, req);

        if (PrefabUtility.IsPartOfPrefabAsset(go))
        {
            PrefabUtility.SavePrefabAsset(go.transform.root.gameObject);
            AssetDatabase.SaveAssets();
        }
        Undo.CollapseUndoOperations(undoGroup);
        ActiveEditorTracker.sharedTracker.ForceRebuild();
        Debug.Log($"Convert: {go.name} {oldName} → {target.Type.Name}", go);
    }
}
#endif
