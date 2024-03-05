using Assets.Scripts;
using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.StaticPhysics;
using Assets.Scripts.Map;
using Assets.Scripts.Map.Visibility;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using UnityTemplateProjects;

public class Game : MonoBehaviour, ISerializationCallbackReceiver
{
    public static Game Instance { get; private set; }

    public InputController InputController;
    public Ksids Ksids { get; private set; }
    public SpInterface StaticPhysics { get; private set; }
    public Timer Timer;
    public GlobalTimerHandler GlobalTimerHandler;
    public ObjectPool Pool;
    public ConnectablesPool ConnectablePool;
    public PrefabsStore PrefabsStore;
    public PlaceableSettings DefaultPlaceableSettings;
    public LevelLabel OccludersRoot;
    public MapWorlds MapWorlds;
    public bool IsPaused { get; private set; }
    public GameState State { get; private set; }

    private readonly List<Trigger> triggers = new();
    private readonly HashSet<IActiveObject> activeObjects = new();
    private readonly Dictionary<Placeable, (int Tag, Map Map)> movingObjects = new();
    private readonly VCore visibility = new();
    private int movingObjectInsterPtr;
    private int movingObjectWorkPtr;
    private const int movingObjectMaxPtr = 20;
    private const int movingObjectVisibilityModulo = movingObjectMaxPtr / 2;

    private readonly Stopwatch sw = new();

    public int CollisionLayaerMask { get; private set; }
    public double[] UpdateTimes = new double[8];
    public double[] VisibiltyTimes = new double[6];
    public int[] VisibiltyCounters = new int[6];

    public enum GameState
    {
        Awaked,
        CreatingLevels,
        Ready,
    }

    void Update()
    {
        if (State != GameState.Ready)
        {
            DoSpecificStates();
            return;
        }

        sw.Restart();
        TimeSpan swStart;

        // Exit Sample  
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }


#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            EditorWindow window = EditorWindow.focusedWindow;
            // Assume the game view is focused.
            window.maximized = !window.maximized;
        }
#endif

        if (!IsPaused)
        {
            InputController.GameUpdate();
            swStart = sw.Elapsed;
            UpdateTriggers();
            UpdateTimes[1] = (sw.Elapsed - swStart).TotalMilliseconds; swStart = sw.Elapsed;
            UpdateMovingObjects();
            UpdateTimes[2] = (sw.Elapsed - swStart).TotalMilliseconds; swStart = sw.Elapsed;
            UpdateObjects();
            UpdateTimes[3] = (sw.Elapsed - swStart).TotalMilliseconds; swStart = sw.Elapsed;
            Timer.GameUpdate();
            UpdateTimes[4] = (sw.Elapsed - swStart).TotalMilliseconds; swStart = sw.Elapsed;
            if (movingObjectWorkPtr % movingObjectVisibilityModulo == 3)
            {
                visibility.Compute(InputController.Character.ArmSphere.transform.position, MapWorlds.SelectedMap);
                visibility.ReportDiagnostics(VisibiltyTimes, VisibiltyCounters);
                UpdateTimes[6] = (sw.Elapsed - swStart).TotalMilliseconds;
            }
            else
            {
                MapWorlds.ProcessCellStateTests(10);
                UpdateTimes[5] = (sw.Elapsed - swStart).TotalMilliseconds;
            }
        }

        //if (!cameraMode)
        //    Character.GameUpdate();
        InputController.Camera.GameUpdate();
        sw.Stop();
        UpdateTimes[0] = sw.Elapsed.TotalMilliseconds;
    }

    private void DoSpecificStates()
    {
        if (State == GameState.Awaked)
        {
            PauseGame();
            State = GameState.CreatingLevels;
            MapWorlds.CreateMaps(Ksids);
        }
        else if (State == GameState.CreatingLevels && !MapWorlds.IsWorking)
        {
            State = GameState.Ready;
            UnPauseGame();
        }
    }

    private void PauseGame()
    {
        Time.timeScale = 0;
        IsPaused = true;
    }

    private void UnPauseGame()
    {
        Time.timeScale = 1;
        IsPaused = false;
    }

    private void UpdateObjects()
    {
        foreach (var o in activeObjects)
        {
            o.GameUpdate();
        }
    }

    private void UpdateMovingObjects()
    {
        foreach (var p in movingObjects)
        {
            if (p.Value.Tag == movingObjectWorkPtr)
            {
                p.Value.Map.Move(p.Key);
                MovingObjTest(p.Key, p.Value.Map);
            }
            else
            {
                p.Key.UpdateMapPosIfMoved(p.Value.Map);
            }
        }

        movingObjectWorkPtr++;
        if (movingObjectWorkPtr >= movingObjectMaxPtr)
            movingObjectWorkPtr = 0;
    }

    private void MovingObjTest(Placeable p, Map map)
    {
        if (Ksids.IsParentOrEqual(p.Ksid, Ksid.SandLike) && p.IsNonMoving)
        {
            float y = Mathf.Repeat(p.Pivot.y, Map.CellSize.y);
            float dy = p.PosOffset.y + p.Size.y;

            if (y + dy > Map.CellSizeY3div4)
                map.AddCellStateTest(map.WorldToCell(p.Pivot), p.CellZ == 0 ? CellStateCahnge.CompactSand0 : CellStateCahnge.CompactSand1);
        }
    }

    private void UpdateTriggers()
    {
        foreach (var t in triggers)
        {
            t.TriggerUpdate();
        }
        triggers.Clear();
    }

    internal void RegisterTrigger(Trigger trigger)
    {
        triggers.Add(trigger);
    }

    public void ActivateObject(IActiveObject o) => activeObjects.Add(o);
    public void DeactivateObject(IActiveObject o) => activeObjects.Remove(o);

    internal void AddMovingObject(Placeable p, Map map)
    {
        movingObjects[p] = (movingObjectInsterPtr++, map);
        if (movingObjectInsterPtr >= movingObjectMaxPtr)
            movingObjectInsterPtr = 0;
    }
    internal void RemoveMovingObject(Placeable p) => movingObjects.Remove(p);


    private void Awake()
    {
        Instance = this;
        if (Ksids == null)
            Ksids = new KsidDependencies();
        if (StaticPhysics == null)
            StaticPhysics = new SpInterface();
        InputController.Init();
        CollisionLayaerMask = LayerMask.GetMask("Default", "MovingObjs");
    }

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
        Instance = this;
        if (Ksids == null)
            Ksids = new KsidDependencies();
    }


    void FixedUpdate()
    {
        InputController.GameFixedUpdate();
        foreach (var o in activeObjects)
        {
            o.GameFixedUpdate();
        }
        StaticPhysics.Update();
    }

    public static Map MapFromPos(float posX) => Instance.MapWorlds.MapFromPos(posX);
}