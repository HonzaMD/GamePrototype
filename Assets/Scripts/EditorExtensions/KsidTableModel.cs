#if UNITY_EDITOR
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Assets.Scripts.EditorExtensions
{
    public enum KsidTableAxis { Rows, Columns }

    public enum KsidCellState { None, Explicit, Inherited, Conflict }

    public struct KsidCellInfo
    {
        public KsidCellState State;
        public string Text;
        public string Tooltip;
    }

    // Editorovy pohled na KsidTable: cache bunek, textovy format, zmeny pres SerializedObject (=> Undo).
    // Textovy format bunky: fieldy v poradi deklarace oddelene ';' (Excel ho nebere jako datum), napr. "20; 2".
    public class KsidTableModel
    {
        public const char FieldSeparator = ';';

        private static Ksids editorKsids;
        public static Ksids Ksids => editorKsids ??= new KsidDependencies();

        public readonly KsidTable Table;
        public readonly SerializedObject So;
        public readonly FieldInfo[] Fields;
        public readonly string SchemaError;

        private readonly SerializedProperty rowsProp;
        private readonly SerializedProperty colsProp;
        private readonly SerializedProperty entriesProp;

        private bool dirty = true;
        private readonly List<(Ksid Row, Ksid Col)> filled = new();
        private readonly List<string> filledText = new();
        private readonly Dictionary<(Ksid, Ksid), int> index = new();
        private KsidCellInfo[,] grid = new KsidCellInfo[0, 0];
        private readonly List<string> warnings = new();
        private readonly List<int> winners = new();
        private int orphanCount;

        public KsidTableModel(KsidTable table)
        {
            Table = table;
            So = new SerializedObject(table);
            rowsProp = So.FindProperty(KsidTable.RowsField);
            colsProp = So.FindProperty(KsidTable.ColumnsField);
            entriesProp = So.FindProperty(KsidTable.EntriesField);
            Fields = GetSerializedFields(table.CellType);
            SchemaError = Fields.Where(f => !IsSupported(f.FieldType))
                .Select(f => $"Nepodporovaný typ fieldu {f.Name}: {f.FieldType.Name} (jen float, int, bool)")
                .FirstOrDefault();
            if (Fields.Length == 0)
                SchemaError = $"{table.CellType.Name} nemá žádné serializované fieldy";
        }

        public int RowCount => rowsProp.arraySize;
        public int ColCount => colsProp.arraySize;
        public Ksid GetKey(KsidTableAxis axis, int i) => (Ksid)AxisProp(axis).GetArrayElementAtIndex(i).intValue;
        public string FieldNames => string.Join($"{FieldSeparator} ", Fields.Select(f => f.Name));

        public KsidCellInfo GetCell(int row, int col)
        {
            Refresh();
            return grid[row, col];
        }

        public IReadOnlyList<string> Warnings
        {
            get { Refresh(); return warnings; }
        }

        public int OrphanCount
        {
            get { Refresh(); return orphanCount; }
        }

        public void MarkDirty() => dirty = true;

        // Vola se na zacatku OnGUI - zachyti zmeny zvenku (inspector).
        public void Update()
        {
            if (So.UpdateIfRequiredOrScript())
                dirty = true;
        }

        // ---------- cteni ----------

        private void Refresh()
        {
            if (!dirty)
                return;
            dirty = false;

            var ksids = Ksids;
            filled.Clear();
            filledText.Clear();
            index.Clear();
            warnings.Clear();
            orphanCount = 0;

            var rowKeys = AxisKeys(KsidTableAxis.Rows);
            var colKeys = AxisKeys(KsidTableAxis.Columns);

            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var e = entriesProp.GetArrayElementAtIndex(i);
                var key = ((Ksid)e.FindPropertyRelative("Row").intValue, (Ksid)e.FindPropertyRelative("Col").intValue);
                filled.Add(key);
                filledText.Add(FormatValue(e.FindPropertyRelative("Value")));
                if (!index.TryAdd(key, i))
                    warnings.Add($"Duplicitní záznam {key.Item1} × {key.Item2}");
                if (!rowKeys.Contains(key.Item1) || !colKeys.Contains(key.Item2))
                {
                    orphanCount++;
                    warnings.Add($"Záznam mimo osy: {key.Item1} × {key.Item2}");
                }
            }

            grid = new KsidCellInfo[rowKeys.Count, colKeys.Count];
            for (int r = 0; r < rowKeys.Count; r++)
                for (int c = 0; c < colKeys.Count; c++)
                    grid[r, c] = ResolveCell(ksids, rowKeys[r], colKeys[c]);

            CollectOffAxisConflicts(ksids, rowKeys, colKeys);
        }

        private KsidCellInfo ResolveCell(Ksids ksids, Ksid row, Ksid col)
        {
            if (index.TryGetValue((row, col), out int i))
                return new KsidCellInfo { State = KsidCellState.Explicit, Text = filledText[i] };

            int count = KsidTableResolver.Resolve(ksids, filled, row, col, winners);
            if (count == 0)
                return new KsidCellInfo { State = KsidCellState.None, Text = "" };
            if (count == 1)
            {
                var w = filled[winners[0]];
                return new KsidCellInfo { State = KsidCellState.Inherited, Text = filledText[winners[0]], Tooltip = $"Zděděno z {w.Row} × {w.Col}" };
            }
            return new KsidCellInfo { State = KsidCellState.Conflict, Text = "konflikt", Tooltip = "Konflikt: " + DescribeWinners() };
        }

        // Ksid, ktere nejsou na osach, se v gridu neukazou - konflikty pro ne aspon vypiseme.
        private void CollectOffAxisConflicts(Ksids ksids, List<Ksid> rowKeys, List<Ksid> colKeys)
        {
            var rowSide = Descendants(ksids, rowKeys);
            var colSide = Descendants(ksids, colKeys);
            foreach (var r in rowSide)
                foreach (var c in colSide)
                {
                    if (rowKeys.Contains(r) && colKeys.Contains(c))
                        continue;
                    if (KsidTableResolver.Resolve(ksids, filled, r, c, winners) > 1)
                        warnings.Add($"Konflikt {r} × {c}: {DescribeWinners()}");
                }
        }

        private static List<Ksid> Descendants(Ksids ksids, List<Ksid> keys)
            => ksids.AllKsids.Select(n => n.Name)
                .Where(k => keys.Any(key => ksids.IsParentOrEqual(k, key)))
                .ToList();

        private string DescribeWinners() => string.Join(", ", winners.Select(w => $"{filled[w].Row} × {filled[w].Col}"));

        private List<Ksid> AxisKeys(KsidTableAxis axis)
        {
            var p = AxisProp(axis);
            var list = new List<Ksid>(p.arraySize);
            for (int i = 0; i < p.arraySize; i++)
                list.Add((Ksid)p.GetArrayElementAtIndex(i).intValue);
            return list;
        }

        public bool HasEntry(int row, int col)
        {
            Refresh();
            return index.ContainsKey((GetKey(KsidTableAxis.Rows, row), GetKey(KsidTableAxis.Columns, col)));
        }

        public string GetExplicitText(int row, int col)
        {
            Refresh();
            return index.TryGetValue((GetKey(KsidTableAxis.Rows, row), GetKey(KsidTableAxis.Columns, col)), out int i) ? filledText[i] : "";
        }

        public int CountEntriesOn(KsidTableAxis axis, Ksid key)
        {
            Refresh();
            return filled.Count(f => (axis == KsidTableAxis.Rows ? f.Row : f.Col) == key);
        }

        // ---------- zapis bunek ----------

        // Prazdny text => smazat bunku (null). Texty musi projit TryParse. Jeden Apply => jeden krok Undo.
        public void SetCells(IReadOnlyList<(int Row, int Col, string Text)> cells, string undoName)
        {
            Refresh();
            var toDelete = new List<int>();
            foreach (var (row, col, text) in cells)
            {
                var key = (GetKey(KsidTableAxis.Rows, row), GetKey(KsidTableAxis.Columns, col));
                bool exists = index.TryGetValue(key, out int i);
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (exists)
                        toDelete.Add(i);
                    continue;
                }
                if (!exists)
                {
                    i = entriesProp.arraySize;
                    entriesProp.arraySize++;
                    var e = entriesProp.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("Row").intValue = (int)key.Item1;
                    e.FindPropertyRelative("Col").intValue = (int)key.Item2;
                }
                WriteValue(entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("Value"), text);
            }
            foreach (var i in toDelete.Distinct().OrderByDescending(i => i))
                entriesProp.DeleteArrayElementAtIndex(i);
            Apply(undoName);
        }

        // ---------- osy ----------

        public void InsertKey(KsidTableAxis axis, int at, Ksid key)
        {
            var p = AxisProp(axis);
            p.InsertArrayElementAtIndex(at);
            p.GetArrayElementAtIndex(at).intValue = (int)key;
            Apply($"Přidat {key}");
        }

        public void RemoveKey(KsidTableAxis axis, int at)
        {
            var key = GetKey(axis, at);
            DeleteEntries(e => AxisValue(e, axis).intValue == (int)key);
            AxisProp(axis).DeleteArrayElementAtIndex(at);
            Apply($"Odebrat {key}");
        }

        public void ReplaceKey(KsidTableAxis axis, int at, Ksid newKey)
        {
            var p = AxisProp(axis);
            var old = GetKey(axis, at);
            p.GetArrayElementAtIndex(at).intValue = (int)newKey;
            for (int i = 0; i < entriesProp.arraySize; i++)
            {
                var v = AxisValue(entriesProp.GetArrayElementAtIndex(i), axis);
                if (v.intValue == (int)old)
                    v.intValue = (int)newKey;
            }
            Apply($"Změnit {old} na {newKey}");
        }

        public void MoveKey(KsidTableAxis axis, int from, int to)
        {
            AxisProp(axis).MoveArrayElement(from, to);
            Apply("Přesunout");
        }

        public void DeleteOrphans()
        {
            var rows = AxisKeys(KsidTableAxis.Rows);
            var cols = AxisKeys(KsidTableAxis.Columns);
            DeleteEntries(e => !rows.Contains((Ksid)e.FindPropertyRelative("Row").intValue)
                            || !cols.Contains((Ksid)e.FindPropertyRelative("Col").intValue));
            Apply("Smazat záznamy mimo osy");
        }

        public bool ContainsKey(KsidTableAxis axis, Ksid key)
        {
            var p = AxisProp(axis);
            for (int i = 0; i < p.arraySize; i++)
                if (p.GetArrayElementAtIndex(i).intValue == (int)key)
                    return true;
            return false;
        }

        private void DeleteEntries(Func<SerializedProperty, bool> predicate)
        {
            for (int i = entriesProp.arraySize - 1; i >= 0; i--)
                if (predicate(entriesProp.GetArrayElementAtIndex(i)))
                    entriesProp.DeleteArrayElementAtIndex(i);
        }

        private static SerializedProperty AxisValue(SerializedProperty entry, KsidTableAxis axis)
            => entry.FindPropertyRelative(axis == KsidTableAxis.Rows ? "Row" : "Col");

        private SerializedProperty AxisProp(KsidTableAxis axis) => axis == KsidTableAxis.Rows ? rowsProp : colsProp;

        private void Apply(string undoName)
        {
            So.ApplyModifiedProperties();
            Undo.SetCurrentGroupName(undoName);
            dirty = true;
        }

        // ---------- textovy format ----------

        public bool TryParse(string text, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(text))
                return true;
            var parts = text.Split(FieldSeparator);
            if (parts.Length != Fields.Length)
            {
                error = $"Čekám {Fields.Length} hodnot ({FieldNames}), je jich {parts.Length}: \"{text}\"";
                return false;
            }
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseField(Fields[i].FieldType, parts[i], out _))
                {
                    error = $"{Fields[i].Name}: neplatná hodnota \"{parts[i].Trim()}\"";
                    return false;
                }
            }
            return true;
        }

        private void WriteValue(SerializedProperty value, string text)
        {
            var parts = text.Split(FieldSeparator);
            for (int i = 0; i < Fields.Length; i++)
            {
                var f = Fields[i];
                TryParseField(f.FieldType, parts[i], out double v);
                var p = value.FindPropertyRelative(f.Name);
                if (f.FieldType == typeof(float)) p.floatValue = (float)v;
                else if (f.FieldType == typeof(int)) p.intValue = (int)v;
                else if (f.FieldType == typeof(bool)) p.boolValue = v != 0;
            }
        }

        private string FormatValue(SerializedProperty value)
        {
            var parts = new string[Fields.Length];
            for (int i = 0; i < Fields.Length; i++)
            {
                var f = Fields[i];
                var p = value.FindPropertyRelative(f.Name);
                if (p == null) parts[i] = "?";
                else if (f.FieldType == typeof(float)) parts[i] = p.floatValue.ToString(CultureInfo.InvariantCulture);
                else if (f.FieldType == typeof(int)) parts[i] = p.intValue.ToString(CultureInfo.InvariantCulture);
                else if (f.FieldType == typeof(bool)) parts[i] = p.boolValue ? "1" : "0";
                else parts[i] = "?";
            }
            return string.Join($"{FieldSeparator} ", parts);
        }

        // Desetinna carka i tecka (cesky Excel).
        private static bool TryParseField(Type type, string text, out double value)
        {
            text = text.Trim().Replace(',', '.');
            value = 0;
            if (type == typeof(bool))
            {
                switch (text.ToLowerInvariant())
                {
                    case "1": case "true": value = 1; return true;
                    case "0": case "false": value = 0; return true;
                    default: return false;
                }
            }
            if (type == typeof(int))
            {
                bool ok = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i);
                value = i;
                return ok;
            }
            bool okF = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float fl);
            value = fl;
            return okF;
        }

        private static bool IsSupported(Type t) => t == typeof(float) || t == typeof(int) || t == typeof(bool);

        // Stejna pravidla jako Unity serializace; poradi deklarace.
        private static FieldInfo[] GetSerializedFields(Type t)
            => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => (f.IsPublic || f.IsDefined(typeof(SerializeField))) && !f.IsDefined(typeof(NonSerializedAttribute)))
                .OrderBy(f => f.MetadataToken)
                .ToArray();
    }
}
#endif
