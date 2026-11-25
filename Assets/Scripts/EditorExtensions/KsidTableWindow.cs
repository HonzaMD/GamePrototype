#if UNITY_EDITOR
using Assets.Scripts.Core;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Assets.Scripts.EditorExtensions
{
    // Tabulkovy editor KsidTable (Docs/ksid-table.md).
    // Mys/sipky vybiraji (Shift rozsiruje), klik na hlavicku = cely radek/sloupec, roh = vse.
    // Enter/F2/dvojklik/psani = editace, Delete = null, Ctrl+C/X/V = TSV (Excel). Prave tlacitko na hlavicce = osy.
    public class KsidTableWindow : EditorWindow
    {
        private const float RowHeight = 20;
        private const float HeaderHeight = 22;
        private const float MinCellWidth = 70;
        private const float MaxCellWidth = 200;
        private const float AddButtonSize = 24;
        private const float ScrollbarSize = 15;
        private const string EditControlName = "KsidTableCellEdit";

        [SerializeField] private KsidTable table;
        [SerializeField] private Vector2 scroll;
        [SerializeField] private bool showWarnings = true;

        private KsidTableModel model;
        private int gridId;

        private int anchorRow, anchorCol, cursorRow, cursorCol;

        private enum CursorMode { None, End, SelectAll }
        private bool editing;
        private int editRow, editCol;
        private string editText, editOriginal;
        private bool editFocusPending;
        private CursorMode editCursor;

        private string status;
        private bool statusError;
        private Vector2 warningsScroll;
        private string[] shownWarnings = Array.Empty<string>();
        private int shownOrphans;
        private Vector2 viewSize;

        private float rowHeaderWidth;
        private float[] colX = { 0 };

        private struct PendingDropdown { public KsidTableAxis Axis; public int At; public bool Replace; public Vector2 ScreenPos; }
        private PendingDropdown? pendingDropdown;

        private static GUIStyle cellStyle, inheritedStyle, colHeaderStyle, rowHeaderStyle, cornerStyle, errorStyle;
        private static readonly Color SelectionColor = new(0.3f, 0.55f, 1f, 0.25f);
        private static readonly Color CursorColor = new(0.3f, 0.55f, 1f, 1f);
        private static readonly Color ConflictColor = new(0.9f, 0.2f, 0.2f, 0.35f);
        private static Color LineColor => EditorGUIUtility.isProSkin ? new Color(0, 0, 0, 0.5f) : new Color(0, 0, 0, 0.2f);
        private static Color HeaderColor => EditorGUIUtility.isProSkin ? new Color(0.22f, 0.22f, 0.22f) : new Color(0.8f, 0.8f, 0.8f);
        private static Color HeaderSelectedColor => EditorGUIUtility.isProSkin ? new Color(0.25f, 0.35f, 0.5f) : new Color(0.65f, 0.75f, 0.9f);

        [MenuItem("MyGame/Ksid Table Editor")]
        private static void OpenFromMenu() => Open(Selection.activeObject as KsidTable);

        [OnOpenAsset]
        private static bool OnOpenAsset(int instanceId, int line)
        {
            if (EditorUtility.InstanceIDToObject(instanceId) is not KsidTable t)
                return false;
            Open(t);
            return true;
        }

        public static void Open(KsidTable t)
        {
            var w = GetWindow<KsidTableWindow>("Ksid Table");
            if (t != null)
                w.SetTable(t);
        }

        private void SetTable(KsidTable t)
        {
            table = t; // model se prestavi na zacatku dalsiho OnGUI
            editing = false;
            anchorRow = anchorCol = cursorRow = cursorCol = 0;
            SetStatus(null);
        }

        private void OnEnable() => Undo.undoRedoPerformed += OnUndoRedo;
        private void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        private void OnUndoRedo()
        {
            model?.MarkDirty();
            Repaint();
        }

        private void OnLostFocus()
        {
            if (editing && !CommitEdit())
                CancelEdit();
        }

        // ---------- OnGUI ----------

        private void OnGUI()
        {
            InitStyles();
            gridId = GUIUtility.GetControlID(FocusType.Keyboard);
            var e = Event.current;

            if (table == null)
                model = null;
            else if (model == null || model.Table != table)
                model = new KsidTableModel(table);

            DrawToolbar();

            if (model == null)
            {
                EditorGUILayout.HelpBox("Vyber KsidTable asset (nebo na něm dej dvojklik).", MessageType.Info);
                return;
            }
            if (model.SchemaError != null)
            {
                EditorGUILayout.HelpBox(model.SchemaError, MessageType.Error);
                return;
            }

            model.Update();
            ClampSelection();
            ShowPendingDropdown(e);
            HandleEditKeys(e);
            HandleCommands(e);
            HandleGridKeys(e);

            // GUILayout musi mit v Layoutu i v nasledujici udalosti stejne prvky - varovani bereme z Layoutu
            if (e.type == EventType.Layout)
            {
                shownWarnings = model.Warnings.ToArray();
                shownOrphans = model.OrphanCount;
            }
            var area = GUILayoutUtility.GetRect(10, 100000, 10, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (e.type != EventType.Layout)
            {
                ComputeColumns();
                DrawGrid(area, e);
            }
            DrawWarnings();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var t = (KsidTable)EditorGUILayout.ObjectField(table, typeof(KsidTable), false, GUILayout.Width(250));
            if (t != table)
                SetTable(t);
            if (model != null && model.SchemaError == null)
                GUILayout.Label($"Buňka: {model.FieldNames}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            if (status != null)
                GUILayout.Label(status, statusError ? errorStyle : EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawWarnings()
        {
            if (shownWarnings.Length == 0)
                return;
            bool show = EditorGUILayout.Foldout(showWarnings, $"Varování ({shownWarnings.Length})", true);
            if (!showWarnings)
            {
                showWarnings = show;
                return;
            }
            warningsScroll = EditorGUILayout.BeginScrollView(warningsScroll, GUILayout.Height(110));
            foreach (var w in shownWarnings)
                EditorGUILayout.LabelField(w, EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
            if (shownOrphans > 0 && GUILayout.Button($"Smazat záznamy mimo osy ({shownOrphans})", GUILayout.Width(250)))
                model.DeleteOrphans();
            showWarnings = show;
        }

        // ---------- layout ----------

        private void ComputeColumns()
        {
            int rows = model.RowCount, cols = model.ColCount;
            rowHeaderWidth = cornerStyle.CalcSize(new GUIContent(CornerText)).x + 8;
            for (int r = 0; r < rows; r++)
                rowHeaderWidth = Mathf.Max(rowHeaderWidth, rowHeaderStyle.CalcSize(new GUIContent(model.GetKey(KsidTableAxis.Rows, r).ToString())).x + 8);

            if (colX.Length != cols + 1)
                colX = new float[cols + 1];
            for (int c = 0; c < cols; c++)
            {
                float w = Mathf.Max(MinCellWidth, colHeaderStyle.CalcSize(new GUIContent(model.GetKey(KsidTableAxis.Columns, c).ToString())).x + 12);
                for (int r = 0; r < rows; r++)
                    w = Mathf.Max(w, cellStyle.CalcSize(new GUIContent(model.GetCell(r, c).Text)).x + 4);
                colX[c + 1] = colX[c] + Mathf.Min(w, MaxCellWidth);
            }
        }

        private string CornerText => $"{model.Table.RowsLabel} \\ {model.Table.ColumnsLabel}";

        private Rect CellRect(int row, int col) => new(colX[col], row * RowHeight, colX[col + 1] - colX[col], RowHeight);

        private (int Row, int Col) CellAt(Vector2 p)
        {
            int row = Mathf.FloorToInt(p.y / RowHeight);
            int col = -1;
            for (int c = 0; c < colX.Length - 1; c++)
                if (p.x >= colX[c] && p.x < colX[c + 1])
                    col = c;
            if (p.x >= colX[^1])
                col = colX.Length - 1; // za poslednim sloupcem
            if (p.x < 0)
                col = -1;
            return (row, col);
        }

        private void EnsureVisible(int row, int col)
        {
            var r = CellRect(row, col);
            if (r.xMin < scroll.x) scroll.x = r.xMin;
            else if (r.xMax > scroll.x + viewSize.x) scroll.x = r.xMax - viewSize.x;
            if (r.yMin < scroll.y) scroll.y = r.yMin;
            else if (r.yMax > scroll.y + viewSize.y) scroll.y = r.yMax - viewSize.y;
        }

        // ---------- kresleni ----------

        private void DrawGrid(Rect area, Event e)
        {
            var corner = new Rect(area.x, area.y, rowHeaderWidth, HeaderHeight);
            var colHeads = new Rect(area.x + rowHeaderWidth, area.y, area.width - rowHeaderWidth, HeaderHeight);
            var rowHeads = new Rect(area.x, area.y + HeaderHeight, rowHeaderWidth, area.height - HeaderHeight);
            var body = new Rect(area.x + rowHeaderWidth, area.y + HeaderHeight, area.width - rowHeaderWidth, area.height - HeaderHeight);

            DrawCorner(corner, e);
            DrawHeaders(KsidTableAxis.Columns, colHeads, e);
            DrawHeaders(KsidTableAxis.Rows, rowHeads, e);
            DrawBody(body, e);
            viewSize = body.size - new Vector2(ScrollbarSize, ScrollbarSize);
        }

        private void DrawCorner(Rect r, Event e)
        {
            if (e.type == EventType.Repaint)
                DrawHeaderBackground(r, false);
            GUI.Label(r, new GUIContent(CornerText, "Klik = vybrat vše"), cornerStyle);
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                if (!editing || CommitEdit())
                {
                    GUIUtility.keyboardControl = gridId;
                    SelectAll();
                }
                e.Use();
            }
        }

        private void DrawHeaders(KsidTableAxis axis, Rect area, Event e)
        {
            bool isRows = axis == KsidTableAxis.Rows;
            int count = isRows ? model.RowCount : model.ColCount;
            GUI.BeginClip(area);

            for (int i = 0; i < count; i++)
            {
                var r = isRows
                    ? new Rect(0, i * RowHeight - scroll.y, area.width, RowHeight)
                    : new Rect(colX[i] - scroll.x, 0, colX[i + 1] - colX[i], area.height);
                if (isRows ? (r.yMax < 0 || r.yMin > area.height) : (r.xMax < 0 || r.xMin > area.width))
                    continue;

                var key = model.GetKey(axis, i);
                if (e.type == EventType.Repaint)
                {
                    bool selected = HasCells && (isRows ? i >= SelMinRow && i <= SelMaxRow : i >= SelMinCol && i <= SelMaxCol);
                    DrawHeaderBackground(r, selected);
                }
                GUI.Label(r, new GUIContent(key.ToString(), ParentsTooltip(key)), isRows ? rowHeaderStyle : colHeaderStyle);
                if (e.type == EventType.MouseDown && r.Contains(e.mousePosition))
                    HeaderClick(axis, i, e);
            }

            var add = isRows
                ? new Rect(2, count * RowHeight - scroll.y + 1, AddButtonSize, RowHeight - 2)
                : new Rect(colX[count] - scroll.x + 2, 1, AddButtonSize, HeaderHeight - 2);
            if (GUI.Button(add, new GUIContent("+", isRows ? "Přidat řádek" : "Přidat sloupec"), EditorStyles.miniButton))
            {
                pendingDropdown = new PendingDropdown { Axis = axis, At = count, ScreenPos = GUIUtility.GUIToScreenPoint(add.position) };
                Repaint();
            }

            GUI.EndClip();
        }

        private static void DrawHeaderBackground(Rect r, bool selected)
        {
            EditorGUI.DrawRect(r, selected ? HeaderSelectedColor : HeaderColor);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), LineColor);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), LineColor);
        }

        private static string ParentsTooltip(Ksid key)
        {
            var parents = KsidTableModel.Ksids[key].Parents;
            return parents.Length == 0 ? "Bez rodičů" : "Rodiče: " + string.Join(", ", parents.Select(p => p.Name));
        }

        private void DrawBody(Rect area, Event e)
        {
            int rows = model.RowCount, cols = model.ColCount;
            var content = new Rect(0, 0, colX[cols] + AddButtonSize, (rows + 1) * RowHeight);
            bool mouseInArea = area.Contains(e.mousePosition);

            scroll = GUI.BeginScrollView(area, scroll, content);

            if (e.type == EventType.Repaint)
            {
                int firstRow = Mathf.Max(0, Mathf.FloorToInt(scroll.y / RowHeight));
                int lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((scroll.y + area.height) / RowHeight));
                for (int r = firstRow; r <= lastRow; r++)
                    for (int c = 0; c < cols; c++)
                    {
                        var rect = CellRect(r, c);
                        if (rect.xMax < scroll.x || rect.xMin > scroll.x + area.width)
                            continue;
                        DrawCell(r, c, rect);
                    }
                if (HasCells && !editing)
                    DrawOutline(CellRect(cursorRow, cursorCol), CursorColor);
            }

            if (editing)
                DrawEditField();

            HandleBodyMouse(e, mouseInArea);
            GUI.EndScrollView();
        }

        private void DrawCell(int r, int c, Rect rect)
        {
            var info = model.GetCell(r, c);
            if (info.State == KsidCellState.Conflict)
                EditorGUI.DrawRect(rect, ConflictColor);
            if (InSelection(r, c))
                EditorGUI.DrawRect(rect, SelectionColor);
            EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), LineColor);
            EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), LineColor);
            if (editing && r == editRow && c == editCol)
                return;
            var style = info.State == KsidCellState.Explicit ? cellStyle : inheritedStyle;
            GUI.Label(rect, new GUIContent(info.Text, info.Tooltip), style);
        }

        private static void DrawOutline(Rect r, Color color)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 2), color);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), color);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 2, r.height), color);
            EditorGUI.DrawRect(new Rect(r.xMax - 2, r.y, 2, r.height), color);
        }

        private void DrawEditField()
        {
            GUI.SetNextControlName(EditControlName);
            editText = GUI.TextField(CellRect(editRow, editCol), editText, EditorStyles.textField);
            if (editFocusPending)
            {
                // FocusControl v udalosti, kterou uz nekdo pouzil (klavesa), nezabere - zkousime v Repaint, dokud fokus neni potvrzeny
                if (EditFieldFocused)
                    editFocusPending = false;
                else if (Event.current.type == EventType.Repaint)
                    GUI.FocusControl(EditControlName);
                Repaint();
            }
            else if (editCursor != CursorMode.None && EditFieldFocused)
            {
                // TextField pri ziskani fokusu vybere vse - kurzor upravime az potom
                var te = (TextEditor)GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl);
                te.text = editText;
                if (editCursor == CursorMode.End)
                    te.MoveTextEnd();
                else
                    te.SelectAll();
                editCursor = CursorMode.None;
                Repaint();
            }
        }

        // ---------- mys ----------

        private void HeaderClick(KsidTableAxis axis, int i, Event e)
        {
            e.Use();
            if (editing && !CommitEdit())
                return;
            GUIUtility.keyboardControl = gridId;
            bool isRows = axis == KsidTableAxis.Rows;

            if (e.button == 1)
            {
                ShowHeaderMenu(axis, i, GUIUtility.GUIToScreenPoint(e.mousePosition));
                return;
            }
            if (e.button != 0 || !HasCells)
                return;

            if (isRows)
            {
                cursorRow = i;
                if (!e.shift)
                    anchorRow = i;
                anchorCol = 0;
                cursorCol = model.ColCount - 1;
            }
            else
            {
                cursorCol = i;
                if (!e.shift)
                    anchorCol = i;
                anchorRow = 0;
                cursorRow = model.RowCount - 1;
            }
            Repaint();
        }

        private void HandleBodyMouse(Event e, bool mouseInArea)
        {
            int rows = model.RowCount, cols = model.ColCount;
            switch (e.GetTypeForControl(gridId))
            {
                case EventType.MouseDown when mouseInArea:
                {
                    var (r, c) = CellAt(e.mousePosition);
                    if (r < 0 || r >= rows || c < 0 || c >= cols)
                        break;
                    e.Use();
                    if (editing && !CommitEdit())
                        break;
                    GUIUtility.keyboardControl = gridId;
                    if (e.button == 0)
                    {
                        cursorRow = r;
                        cursorCol = c;
                        if (!e.shift || e.clickCount == 2)
                        {
                            anchorRow = r;
                            anchorCol = c;
                        }
                        if (e.clickCount == 2)
                            StartEditAtCursor();
                        else
                            GUIUtility.hotControl = gridId;
                    }
                    else if (e.button == 1)
                    {
                        if (!InSelection(r, c))
                        {
                            anchorRow = cursorRow = r;
                            anchorCol = cursorCol = c;
                        }
                        ShowCellMenu();
                    }
                    Repaint();
                    break;
                }
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == gridId && HasCells)
                    {
                        var (r, c) = CellAt(e.mousePosition);
                        cursorRow = Mathf.Clamp(r, 0, rows - 1);
                        cursorCol = Mathf.Clamp(c, 0, cols - 1);
                        e.Use();
                        Repaint();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == gridId)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }
                    break;
            }
        }

        private void ShowCellMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("Kopírovat"), false, () => { Copy(); Repaint(); });
            menu.AddItem(new GUIContent("Vyjmout"), false, () => { Cut(); Repaint(); });
            menu.AddItem(new GUIContent("Vložit"), false, () => { Paste(); Repaint(); });
            menu.AddSeparator("");
            menu.AddItem(new GUIContent("Smazat (null)"), false, () => { DeleteSelection(); Repaint(); });
            menu.ShowAsContext();
        }

        private void ShowHeaderMenu(KsidTableAxis axis, int i, Vector2 screenPos)
        {
            bool isRows = axis == KsidTableAxis.Rows;
            int count = isRows ? model.RowCount : model.ColCount;
            var key = model.GetKey(axis, i);
            var m = model;
            var menu = new GenericMenu();

            void Dropdown(int at, bool replace)
            {
                pendingDropdown = new PendingDropdown { Axis = axis, At = at, Replace = replace, ScreenPos = screenPos };
                Repaint();
            }

            menu.AddItem(new GUIContent(isRows ? "Vložit nad…" : "Vložit vlevo…"), false, () => Dropdown(i, false));
            menu.AddItem(new GUIContent(isRows ? "Vložit pod…" : "Vložit vpravo…"), false, () => Dropdown(i + 1, false));
            menu.AddItem(new GUIContent("Změnit Ksid…"), false, () => Dropdown(i, true));
            menu.AddSeparator("");
            AddItem(menu, isRows ? "Posunout nahoru" : "Posunout vlevo", i > 0, () => m.MoveKey(axis, i, i - 1));
            AddItem(menu, isRows ? "Posunout dolů" : "Posunout vpravo", i < count - 1, () => m.MoveKey(axis, i, i + 1));
            menu.AddSeparator("");
            menu.AddItem(new GUIContent($"Odebrat {key}"), false, () => RemoveKey(m, axis, i));
            menu.ShowAsContext();
        }

        private void AddItem(GenericMenu menu, string text, bool enabled, Action action)
        {
            if (enabled)
                menu.AddItem(new GUIContent(text), false, () => { action(); Repaint(); });
            else
                menu.AddDisabledItem(new GUIContent(text));
        }

        private void RemoveKey(KsidTableModel m, KsidTableAxis axis, int i)
        {
            var key = m.GetKey(axis, i);
            int n = m.CountEntriesOn(axis, key);
            if (n > 0 && !EditorUtility.DisplayDialog("Odebrat " + key, $"{key} má {n} vyplněných buněk. Smazat i je?", "Smazat", "Zrušit"))
                return;
            m.RemoveKey(axis, i);
            Repaint();
        }

        // Otevira se az v Layoutu dalsiho OnGUI, mimo clip a mimo callback menu.
        private void ShowPendingDropdown(Event e)
        {
            if (e.type != EventType.Layout || pendingDropdown is not PendingDropdown p)
                return;
            pendingDropdown = null;
            var m = model;
            var items = ((Ksid[])Enum.GetValues(typeof(Ksid))).Where(k => !m.ContainsKey(p.Axis, k));
            var pos = GUIUtility.ScreenToGUIPoint(p.ScreenPos);
            new KsidDropdown(items, k =>
            {
                if (p.Replace)
                    m.ReplaceKey(p.Axis, p.At, k);
                else
                    m.InsertKey(p.Axis, p.At, k);
                Repaint();
            }).Show(new Rect(pos.x, pos.y, 220, 0));
        }

        // ---------- klavesnice ----------

        private bool GridFocused => GUIUtility.keyboardControl == gridId || GUIUtility.keyboardControl == 0;
        private static bool EditFieldFocused => GUI.GetNameOfFocusedControl() == EditControlName;

        private void HandleEditKeys(Event e)
        {
            if (!editing || e.type != EventType.KeyDown)
                return;
            switch (e.keyCode)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (CommitEdit() && !e.control && !e.command) // Ctrl+Enter = bez posunu
                        MoveCursor(1, 0, false);
                    break;
                case KeyCode.Tab:
                    if (CommitEdit())
                        MoveCursor(0, e.shift ? -1 : 1, false);
                    break;
                case KeyCode.Escape:
                    CancelEdit();
                    break;
                default:
                    if (e.character == '\n' || e.character == '\t')
                        break;
                    // pole jeste nema fokus - znak nesmi propadnout
                    if (editFocusPending && e.character != 0 && !char.IsControl(e.character) && !e.control && !e.command && !e.alt)
                    {
                        if (editCursor == CursorMode.SelectAll)
                            editText = "";
                        editText += e.character;
                        editCursor = CursorMode.End;
                        Repaint();
                        break;
                    }
                    return;
            }
            e.Use();
        }

        private void HandleGridKeys(Event e)
        {
            if (e.type != EventType.KeyDown || editing || !GridFocused || !HasCells)
                return;
            // Tab/Enter chodi i jako samostatna znakova udalost; nepouzity Tab by IMGUI poslal fokus na dalsi control (vyber assetu)
            if (e.keyCode == KeyCode.None && (e.character == '\t' || e.character == '\n'))
            {
                e.Use();
                return;
            }
            switch (e.keyCode)
            {
                case KeyCode.UpArrow: MoveCursor(-1, 0, e.shift); break;
                case KeyCode.DownArrow: MoveCursor(1, 0, e.shift); break;
                case KeyCode.LeftArrow: MoveCursor(0, -1, e.shift); break;
                case KeyCode.RightArrow: MoveCursor(0, 1, e.shift); break;
                case KeyCode.Tab: MoveCursor(0, e.shift ? -1 : 1, false); break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.F2:
                    StartEditAtCursor();
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    DeleteSelection();
                    break;
                default:
                    char ch = e.character;
                    if (ch == 0 || char.IsControl(ch) || e.control || e.command || e.alt)
                        return;
                    anchorRow = cursorRow;
                    anchorCol = cursorCol;
                    StartEdit(ch.ToString(), CursorMode.End);
                    break;
            }
            e.Use();
            Repaint();
        }

        private void HandleCommands(Event e)
        {
            if (e.type != EventType.ValidateCommand && e.type != EventType.ExecuteCommand)
                return;
            if (editing || !GridFocused || !HasCells)
                return;
            bool exec = e.type == EventType.ExecuteCommand;
            switch (e.commandName)
            {
                case "Copy": if (exec) Copy(); break;
                case "Cut": if (exec) Cut(); break;
                case "Paste": if (exec) Paste(); break;
                case "SelectAll": if (exec) SelectAll(); break;
                case "Delete":
                case "SoftDelete": if (exec) DeleteSelection(); break;
                default: return;
            }
            e.Use();
            if (exec)
                Repaint();
        }

        private void MoveCursor(int dRow, int dCol, bool extend)
        {
            if (!HasCells)
                return;
            cursorRow = Mathf.Clamp(cursorRow + dRow, 0, model.RowCount - 1);
            cursorCol = Mathf.Clamp(cursorCol + dCol, 0, model.ColCount - 1);
            if (!extend)
            {
                anchorRow = cursorRow;
                anchorCol = cursorCol;
            }
            EnsureVisible(cursorRow, cursorCol);
        }

        // ---------- editace ----------

        private void StartEditAtCursor()
        {
            anchorRow = cursorRow;
            anchorCol = cursorCol;
            // null bunka nabidne zdedenou hodnotu jako vychozi; beze zmeny se nic neulozi
            var info = model.GetCell(cursorRow, cursorCol);
            StartEdit(info.State == KsidCellState.Conflict ? "" : info.Text, CursorMode.SelectAll);
        }

        private void StartEdit(string text, CursorMode cursorMode)
        {
            editing = true;
            editRow = cursorRow;
            editCol = cursorCol;
            editText = text;
            editOriginal = model.GetCell(cursorRow, cursorCol).Text;
            editCursor = cursorMode;
            editFocusPending = true;
            EnsureVisible(editRow, editCol);
            SetStatus(null);
        }

        private bool CommitEdit()
        {
            if (!editing)
                return true;
            if (editText != editOriginal)
            {
                if (!model.TryParse(editText, out var error))
                {
                    SetStatus(error, true);
                    return false;
                }
                model.SetCells(new[] { (editRow, editCol, editText) }, "Upravit buňku");
            }
            EndEdit();
            SetStatus(null);
            return true;
        }

        private void CancelEdit()
        {
            EndEdit();
            SetStatus(null);
        }

        private void EndEdit()
        {
            editing = false;
            GUIUtility.keyboardControl = gridId;
            Repaint();
        }

        // ---------- vyber a schranka ----------

        private bool HasCells => model.RowCount > 0 && model.ColCount > 0;
        private int SelMinRow => Mathf.Min(anchorRow, cursorRow);
        private int SelMaxRow => Mathf.Max(anchorRow, cursorRow);
        private int SelMinCol => Mathf.Min(anchorCol, cursorCol);
        private int SelMaxCol => Mathf.Max(anchorCol, cursorCol);
        private bool InSelection(int r, int c) => r >= SelMinRow && r <= SelMaxRow && c >= SelMinCol && c <= SelMaxCol;

        private void ClampSelection()
        {
            int maxRow = Mathf.Max(0, model.RowCount - 1), maxCol = Mathf.Max(0, model.ColCount - 1);
            anchorRow = Mathf.Clamp(anchorRow, 0, maxRow);
            cursorRow = Mathf.Clamp(cursorRow, 0, maxRow);
            anchorCol = Mathf.Clamp(anchorCol, 0, maxCol);
            cursorCol = Mathf.Clamp(cursorCol, 0, maxCol);
            if (editing && (editRow > maxRow || editCol > maxCol || !HasCells))
                editing = false;
        }

        private void SelectAll()
        {
            if (!HasCells)
                return;
            anchorRow = anchorCol = 0;
            cursorRow = model.RowCount - 1;
            cursorCol = model.ColCount - 1;
        }

        // Kopiruji se jen explicitni hodnoty, null = prazdna bunka.
        private void Copy()
        {
            var sb = new StringBuilder();
            for (int r = SelMinRow; r <= SelMaxRow; r++)
            {
                for (int c = SelMinCol; c <= SelMaxCol; c++)
                {
                    if (c > SelMinCol)
                        sb.Append('\t');
                    sb.Append(model.GetExplicitText(r, c));
                }
                sb.Append('\n');
            }
            EditorGUIUtility.systemCopyBuffer = sb.ToString();
            SetStatus($"Zkopírováno {SelMaxRow - SelMinRow + 1}×{SelMaxCol - SelMinCol + 1}");
        }

        private void Cut()
        {
            Copy();
            DeleteSelection();
        }

        private void DeleteSelection()
        {
            var cells = new List<(int, int, string)>();
            for (int r = SelMinRow; r <= SelMaxRow; r++)
                for (int c = SelMinCol; c <= SelMaxCol; c++)
                    if (model.HasEntry(r, c))
                        cells.Add((r, c, null));
            if (cells.Count > 0)
                model.SetCells(cells, "Smazat buňky");
        }

        // Prazdna bunka ve schrance = null. Jedna hodnota a vetsi vyber = vyplnit vyber.
        private void Paste()
        {
            var text = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrEmpty(text))
                return;
            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            if (lines.Count > 1 && lines[^1].Length == 0)
                lines.RemoveAt(lines.Count - 1);
            var data = lines.Select(l => l.Split('\t').Select(Unquote).ToArray()).ToArray();

            var cells = new List<(int Row, int Col, string Text)>();
            int skipped = 0;
            bool fill = data.Length == 1 && data[0].Length == 1 && (SelMinRow != SelMaxRow || SelMinCol != SelMaxCol);
            if (fill)
            {
                for (int r = SelMinRow; r <= SelMaxRow; r++)
                    for (int c = SelMinCol; c <= SelMaxCol; c++)
                        cells.Add((r, c, data[0][0]));
            }
            else
            {
                for (int i = 0; i < data.Length; i++)
                    for (int j = 0; j < data[i].Length; j++)
                    {
                        int r = SelMinRow + i, c = SelMinCol + j;
                        if (r >= model.RowCount || c >= model.ColCount)
                            skipped++;
                        else
                            cells.Add((r, c, data[i][j]));
                    }
            }

            foreach (var (r, c, t) in cells)
            {
                if (!model.TryParse(t, out var error))
                {
                    SetStatus($"Nevloženo – {model.GetKey(KsidTableAxis.Rows, r)} × {model.GetKey(KsidTableAxis.Columns, c)}: {error}", true);
                    return;
                }
            }
            model.SetCells(cells, "Vložit");

            if (!fill && cells.Count > 0)
            {
                anchorRow = SelMinRow;
                anchorCol = SelMinCol;
                cursorRow = cells.Max(x => x.Row);
                cursorCol = cells.Max(x => x.Col);
            }
            SetStatus(skipped > 0 ? $"Vloženo, {skipped} buněk mimo tabulku vynecháno" : "Vloženo", skipped > 0);
        }

        private static string Unquote(string s)
            => s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1].Replace("\"\"", "\"") : s;

        private void SetStatus(string text, bool error = false)
        {
            status = text;
            statusError = error;
        }

        private static void InitStyles()
        {
            if (cellStyle != null)
                return;
            cellStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(4, 4, 0, 0) };
            inheritedStyle = new GUIStyle(cellStyle) { fontStyle = FontStyle.Italic };
            inheritedStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            colHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip };
            rowHeaderStyle = new GUIStyle(EditorStyles.miniBoldLabel) { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, padding = new RectOffset(4, 4, 0, 0) };
            cornerStyle = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(4, 4, 0, 0) };
            errorStyle = new GUIStyle(EditorStyles.miniLabel);
            errorStyle.normal.textColor = new Color(0.9f, 0.3f, 0.3f);
        }
    }

    [CustomEditor(typeof(KsidTable), true)]
    public class KsidTableInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Otevřít v editoru tabulky"))
                KsidTableWindow.Open((KsidTable)target);
            EditorGUILayout.Space();
            DrawDefaultInspector();
        }
    }
}
#endif
