#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Assets.Scripts.Ai;
using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Wizard pro zalozeni noveho herniho objektu ze zkopirovaneho prefabu.
// Pripravi seznam akci (duplikace settings, Prototype, hmotnost, Ksid, PrefabsStore),
// predvyplni je a uzivatel je muze upravit pred potvrzenim.
public class GameObjectSetupWizard : EditorWindow
{
    private const string MenuName = "Setup Game Object...";
    private const string PlaceableSettingsFolder = "Assets/Settings/PlaceableSettings";
    private const string AiFolder = "Assets/Settings/AI";

    // Odkaz z komponenty prefabu na settings asset, ktery se muze zduplikovat.
    private class SettingsRef
    {
        public string Title;
        public Component Owner;
        public string PropertyPath;
        public Type AssetType;
        public ScriptableObject Source;
        public bool Duplicate;
        public string NewName;
        public string Folder;
        public ScriptableObject Created;

        public string TargetPath => $"{Folder}/{NewName}.asset";
        public ScriptableObject Final => Duplicate ? Created : Source;
    }

    [SerializeField] private GameObject prefab;

    private Label rootLabel;
    private Rigidbody rb;

    private SettingsRef placeableSettings;
    private readonly List<SettingsRef> otherSettings = new();

    private bool setPrototype;
    private bool setMass;
    private float mass;

    private bool hasPosition, hasRotation, hasScale;
    private bool resetPosition, resetRotation, resetScale;

    private Ksid originalKsid;
    private Ksid ksid;
    private Label sourcePrototype;
    private readonly List<(Ksid ksid, string name)> storeKsids = new();

    private PrefabsStore store;
    private FieldInfo[] storeFields;
    private string[] storeFieldLabels;
    private int storeFieldIndex; // 0 = zadne
    private bool setStore;

    private Vector2 scroll;

    [MenuItem("Assets/MyGame/" + MenuName)]
    [MenuItem("MyGame/" + MenuName)]
    private static void Open()
    {
        var window = CreateInstance<GameObjectSetupWizard>();
        window.titleContent = new GUIContent("Setup Game Object");
        window.minSize = new Vector2(480, 520);
        window.prefab = GetSelectedPrefab();
        window.Init();
        window.ShowUtility();
    }

    [MenuItem("Assets/MyGame/" + MenuName, true)]
    [MenuItem("MyGame/" + MenuName, true)]
    private static bool ValidateOpen() => GetSelectedPrefab() != null;

    private static GameObject GetSelectedPrefab()
    {
        var go = Selection.activeObject as GameObject;
        if (go == null || !PrefabUtility.IsPartOfPrefabAsset(go) || go.transform.parent != null)
            return null;
        return FindRootLabel(go) != null ? go : null;
    }

    private static Label FindRootLabel(GameObject go)
    {
        foreach (var label in go.GetComponents<Label>())
            if (label is Placeable || label is LabelWithSettings)
                return label;
        return null;
    }

    private static Ksid GetKsid(Label label) => label switch
    {
        Placeable p => p.Ksid,
        LabelWithSettings s => s.Ksid,
        _ => Ksid.Unknown,
    };

    private static string ShortName(Object obj) => obj ? obj.name : "null";

    private void OnEnable()
    {
        // po reloadu domeny se nevyserializovany stav ztrati
        if (prefab != null && rootLabel == null)
            Init();
    }

    private void Init()
    {
        otherSettings.Clear();
        storeKsids.Clear();
        if (prefab == null)
            return;

        rootLabel = FindRootLabel(prefab);
        rb = prefab.GetComponent<Rigidbody>();
        string name = prefab.name.Replace(" ", "");

        placeableSettings = CreateRef("Placeable Settings", rootLabel, "Settings", typeof(PlaceableSettings), $"{name}Settings", PlaceableSettingsFolder);
        var ps = placeableSettings.Source as PlaceableSettings;
        if (ps != null && ps.Prototype == rootLabel)
            placeableSettings.Duplicate = false; // uz je vlastni (wizard spusten znovu)
        setPrototype = true;

        setMass = true;
        mass = rb ? rb.mass : ps ? ps.Mass : 0f;

        var t = prefab.transform;
        resetPosition = hasPosition = t.localPosition != Vector3.zero;
        resetRotation = hasRotation = t.localRotation != Quaternion.identity;
        resetScale = hasScale = t.localScale != Vector3.one;

        originalKsid = ksid = GetKsid(rootLabel);
        sourcePrototype = ps != null && ps.Prototype != rootLabel ? ps.Prototype : null;

        foreach (var ch in prefab.GetComponentsInChildren<ChLegsArms>(true))
        {
            if (!IsOwnObject(ch))
                continue;
            otherSettings.Add(CreateRef($"ChSettings ({ch.GetType().Name})", ch, "Settings", typeof(ChSettings), $"{name} ChSettings", AiFolder));
            if (ch is MonsterBrain)
                otherSettings.Add(CreateRef($"AiSettings ({ch.GetType().Name})", ch, "AiSettings", typeof(AiSettings), $"{name} AI", AiFolder));
        }

        InitStore(name);
    }

    // Komponenty vnorenych prefabu (Arm, Leg...) maji vlastni settings, ty neresime.
    private bool IsOwnObject(Component c)
    {
        var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(c);
        return instanceRoot == null || instanceRoot == prefab;
    }

    private static SettingsRef CreateRef(string title, Component owner, string propertyPath, Type assetType, string newName, string defaultFolder)
    {
        var source = new SerializedObject(owner).FindProperty(propertyPath).objectReferenceValue as ScriptableObject;
        string folder = source ? Path.GetDirectoryName(AssetDatabase.GetAssetPath(source)).Replace('\\', '/') : defaultFolder;
        return new SettingsRef
        {
            Title = title,
            Owner = owner,
            PropertyPath = propertyPath,
            AssetType = assetType,
            Source = source,
            Duplicate = source == null || source.name != newName,
            NewName = newName,
            Folder = folder,
        };
    }

    private void InitStore(string name)
    {
        store = AssetDatabase.FindAssets("t:PrefabsStore")
            .Select(g => AssetDatabase.LoadAssetAtPath<PrefabsStore>(AssetDatabase.GUIDToAssetPath(g)))
            .FirstOrDefault(s => s != null);
        if (store == null)
            return;

        var allFields = typeof(PrefabsStore).GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var f in allFields)
        {
            if (f.GetValue(store) is Component c && c && c.gameObject != prefab && FindRootLabel(c.gameObject) is Label l)
                storeKsids.Add((GetKsid(l), c.gameObject.name));
        }

        storeFields = allFields.Where(f => GetStoreValue(f) != null).ToArray();
        storeFieldLabels = new[] { "(žádné)" }.Concat(storeFields.Select(f =>
        {
            var cur = f.GetValue(store) as Object;
            return cur ? $"{f.Name}  (= {cur.name})" : $"{f.Name}  (prázdné)";
        })).ToArray();

        int match = Array.FindIndex(storeFields, f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        storeFieldIndex = match + 1;
        if (match >= 0)
        {
            var cur = storeFields[match].GetValue(store) as Object;
            setStore = cur == null; // obsazene pole jen s potvrzenim
        }
    }

    // Hodnota, ktera by se do pole PrefabsStore zapsala; null = pole neni kompatibilni.
    private Object GetStoreValue(FieldInfo f)
    {
        if (f.FieldType == typeof(GameObject))
            return prefab;
        if (typeof(Component).IsAssignableFrom(f.FieldType) && prefab.TryGetComponent(f.FieldType, out var c))
            return c;
        return null;
    }

    private void OnGUI()
    {
        if (prefab == null || rootLabel == null)
        {
            EditorGUILayout.HelpBox("Vyber v Project okně prefab s Placeable / LabelWithSettings v kořeni.", MessageType.Error);
            if (GUILayout.Button("Zavřít"))
                Close();
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
            EditorGUILayout.TextField("Root label", rootLabel.GetType().Name);
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        DrawKsid();
        DrawPlaceableSettings();
        DrawMass();
        DrawTransform();
        DrawStore();
        foreach (var r in otherSettings)
        {
            Section(r.Title);
            DrawSettingsRef(r);
        }
        EditorGUILayout.EndScrollView();

        GUILayout.FlexibleSpace();
        bool valid = IsRefValid(placeableSettings) && otherSettings.All(IsRefValid);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Cancel", GUILayout.Width(100)))
            {
                Close();
                GUIUtility.ExitGUI();
            }
            using (new EditorGUI.DisabledScope(!valid))
            {
                if (GUILayout.Button("OK", GUILayout.Width(100)))
                {
                    Apply();
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
        }
        EditorGUILayout.Space();
    }

    private static void Section(string title)
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
    }

    private void DrawKsid()
    {
        Section("Ksid");
        ksid = (Ksid)EditorGUILayout.EnumPopup("Ksid", ksid);
        if (ksid == Ksid.Unknown)
            EditorGUILayout.HelpBox("Ksid je Unknown.", MessageType.Warning);
        if (sourcePrototype != null && GetKsid(sourcePrototype) == ksid)
            EditorGUILayout.HelpBox($"Stejný Ksid jako zdrojový prefab {ShortName(sourcePrototype)}.", MessageType.Warning);
        var others = storeKsids.Where(k => k.ksid == ksid && (sourcePrototype == null || k.name != sourcePrototype.name)).Select(k => k.name).ToList();
        if (others.Count > 0)
            EditorGUILayout.HelpBox($"Stejný Ksid má v PrefabsStore: {string.Join(", ", others)}.", MessageType.Warning);
        if (ksid != originalKsid)
            EditorGUILayout.HelpBox($"Změní se z {originalKsid}.", MessageType.Info);
    }

    private void DrawPlaceableSettings()
    {
        Section(placeableSettings.Title);
        DrawSettingsRef(placeableSettings);
        setPrototype = EditorGUILayout.ToggleLeft($"Nastavit Prototype → {prefab.name}", setPrototype);

        var ps = placeableSettings.Source as PlaceableSettings;
        if (!placeableSettings.Duplicate && setPrototype && ps != null && ps.Prototype != null && ps.Prototype != rootLabel)
            EditorGUILayout.HelpBox($"Settings patří prefabu {ShortName(ps.Prototype)} – přepsáním Prototype ho rozbiješ.", MessageType.Warning);
    }

    private void DrawMass()
    {
        Section("Hmotnost");
        using (new EditorGUILayout.HorizontalScope())
        {
            setMass = EditorGUILayout.ToggleLeft("Nastavit [kg]", setMass, GUILayout.Width(120));
            using (new EditorGUI.DisabledScope(!setMass))
                mass = EditorGUILayout.FloatField(mass);
        }
        EditorGUILayout.LabelField(rb ? "→ Settings.Mass + Rigidbody.mass" : "→ Settings.Mass (prefab nemá Rigidbody)", EditorStyles.miniLabel);
        if (setMass && mass <= 0)
            EditorGUILayout.HelpBox("Mass 0 = spočítá se z objemu a hustoty.", MessageType.Info);
    }

    private void DrawTransform()
    {
        if (!hasPosition && !hasRotation && !hasScale)
            return;
        Section("Root Transform");
        var t = prefab.transform;
        if (hasPosition)
            resetPosition = EditorGUILayout.ToggleLeft($"Resetovat pozici {t.localPosition}", resetPosition);
        if (hasRotation)
            resetRotation = EditorGUILayout.ToggleLeft($"Resetovat rotaci {t.localEulerAngles}", resetRotation);
        if (hasScale)
            resetScale = EditorGUILayout.ToggleLeft($"Resetovat scale {t.localScale}", resetScale);
    }

    private void DrawStore()
    {
        Section("Prefabs Store");
        if (store == null)
        {
            EditorGUILayout.HelpBox("PrefabsStore asset nenalezen.", MessageType.Error);
            return;
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            setStore = EditorGUILayout.ToggleLeft("Zapsat do pole", setStore, GUILayout.Width(120));
            using (new EditorGUI.DisabledScope(!setStore))
                storeFieldIndex = EditorGUILayout.Popup(storeFieldIndex, storeFieldLabels);
        }

        if (storeFieldIndex == 0)
        {
            EditorGUILayout.HelpBox($"Pole pro {prefab.name} nenalezeno – přidej ho do PrefabsStore.cs.", MessageType.Warning);
            return;
        }
        var field = storeFields[storeFieldIndex - 1];
        var cur = field.GetValue(store) as Object;
        var target = GetStoreValue(field);
        if (cur == target)
            EditorGUILayout.HelpBox("Pole už ukazuje na tento prefab.", MessageType.Info);
        else if (cur != null)
            EditorGUILayout.HelpBox($"Pole už ukazuje na {cur.name} – zapsáním se přepíše.", MessageType.Warning);
    }

    private static void DrawSettingsRef(SettingsRef r)
    {
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.ObjectField("Aktuální", r.Source, r.AssetType, false);
        r.Duplicate = EditorGUILayout.ToggleLeft(r.Source ? "Duplikovat a přelinkovat" : "Vytvořit nový", r.Duplicate);
        if (!r.Duplicate)
        {
            if (r.Source == null)
                EditorGUILayout.HelpBox("Settings chybí.", MessageType.Warning);
            return;
        }
        r.NewName = EditorGUILayout.TextField("Nový název", r.NewName);
        EditorGUILayout.LabelField("Cesta", r.TargetPath, EditorStyles.miniLabel);
        if (string.IsNullOrWhiteSpace(r.NewName))
            EditorGUILayout.HelpBox("Název je prázdný.", MessageType.Error);
        else if (!AssetDatabase.IsValidFolder(r.Folder))
            EditorGUILayout.HelpBox($"Složka {r.Folder} neexistuje.", MessageType.Error);
        else if (AssetDatabase.LoadAssetAtPath<Object>(r.TargetPath) != null)
            EditorGUILayout.HelpBox("Asset už existuje – vytvoří se s číselnou příponou.", MessageType.Warning);
    }

    private static bool IsRefValid(SettingsRef r) =>
        !r.Duplicate || (!string.IsNullOrWhiteSpace(r.NewName) && AssetDatabase.IsValidFolder(r.Folder));

    private void Apply()
    {
        Undo.SetCurrentGroupName("Setup Game Object");
        int undoGroup = Undo.GetCurrentGroup();

        DuplicateSettings(placeableSettings);
        foreach (var r in otherSettings)
            DuplicateSettings(r);

        if (placeableSettings.Final is PlaceableSettings ps)
        {
            Undo.RecordObject(ps, "Setup Game Object");
            if (setPrototype)
                ps.Prototype = rootLabel;
            if (setMass)
                ps.Mass = mass;
            EditorUtility.SetDirty(ps);
        }

        if (setMass && rb)
        {
            Undo.RecordObject(rb, "Setup Game Object");
            rb.mass = mass;
        }

        if ((hasPosition && resetPosition) || (hasRotation && resetRotation) || (hasScale && resetScale))
        {
            var t = prefab.transform;
            Undo.RecordObject(t, "Setup Game Object");
            if (hasPosition && resetPosition)
                t.localPosition = Vector3.zero;
            if (hasRotation && resetRotation)
                t.localRotation = Quaternion.identity;
            if (hasScale && resetScale)
                t.localScale = Vector3.one;
        }

        if (ksid != originalKsid)
        {
            var so = new SerializedObject(rootLabel);
            so.FindProperty("Ksid").intValue = (int)ksid;
            so.ApplyModifiedProperties();
        }

        if (store != null && setStore && storeFieldIndex > 0)
        {
            var field = storeFields[storeFieldIndex - 1];
            Undo.RecordObject(store, "Setup Game Object");
            field.SetValue(store, GetStoreValue(field));
            EditorUtility.SetDirty(store);
        }

        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);
        Debug.Log($"Setup Game Object: {prefab.name} hotovo.", prefab);
    }

    private static void DuplicateSettings(SettingsRef r)
    {
        if (!r.Duplicate)
            return;

        string path = AssetDatabase.GenerateUniqueAssetPath(r.TargetPath);
        if (r.Source)
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(r.Source), path);
        else
            AssetDatabase.CreateAsset(CreateInstance(r.AssetType), path);
        r.Created = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);

        var so = new SerializedObject(r.Owner);
        so.FindProperty(r.PropertyPath).objectReferenceValue = r.Created;
        so.ApplyModifiedProperties();
    }
}
#endif
