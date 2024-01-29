using Assets.Scripts;
using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.StaticPhysics;
using Assets.Scripts.Map;
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
    public static Map Map;

    public Character3 Character;
    public SimpleCameraController Camera;
    public Level Level;
    public Ksids Ksids { get; private set; }
    public SpInterface StaticPhysics { get; private set; }
    public Timer Timer;
    public GlobalTimerHandler GlobalTimerHandler;
    public ObjectPool Pool;
    public ConnectablesPool ConnectablePool;
    public PrefabsStore PrefabsStore;
    public PlaceableSettings DefaultPlaceableSettings;
    public LevelLabel OccludersRoot;

    public Transform[] HoldMarkers;
    public Transform LongThrowMarker;
    private int holdMarkersToken;

    private List<Trigger> triggers = new List<Trigger>();
    private HashSet<IActiveObject> activeObjects = new HashSet<IActiveObject>();
    private Dictionary<Placeable, int> movingObjects = new Dictionary<Placeable, int>();
    private int movingObjectInsterPtr;
    private int movingObjectWorkPtr;
    private const int movingObjectMaxPtr = 20;
    private const int movingObjectVisibilityModulo = movingObjectMaxPtr / 2;

    private bool cameraMode;
    private readonly Action<object, int> DeactivateHoldMarkerA;

    private readonly Stopwatch sw = new Stopwatch();

    public int CollisionLayaerMask { get; private set; }
    public double[] UpdateTimes = new double[8];

    public Game()
    {
        DeactivateHoldMarkerA = DeactivateHoldMarker;
    }

    void Update()
    {
        sw.Restart();
        TimeSpan swStart;

        UpdateTriggers();
        UpdateTimes[1] = sw.Elapsed.TotalMilliseconds;

        // Exit Sample  
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        if (Input.GetKeyDown(KeyCode.Tab))
        {
            cameraMode = !cameraMode;
            if (cameraMode)
                Camera.ActivateControl();
            else
                Camera.DeactivateControl();
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.BackQuote))
        {
            EditorWindow window = EditorWindow.focusedWindow;
            // Assume the game view is focused.
            window.maximized = !window.maximized;
        }
#endif

        swStart = sw.Elapsed;
        UpdateMovingObjects();
        UpdateTimes[2] = (sw.Elapsed - swStart).TotalMilliseconds; swStart = sw.Elapsed;
        UpdateObjects();
        UpdateTimes[3] = (sw.Elapsed - swStart).TotalMilliseconds; swStart = sw.Elapsed;
        Timer.GameUpdate();
        UpdateTimes[4] = (sw.Elapsed - swStart).TotalMilliseconds; swStart = sw.Elapsed;
        if (movingObjectWorkPtr % movingObjectVisibilityModulo == 3)
            Map.Visibility.Compute(Character.ArmSphere.transform.position);
        else
            Map.ProcessCellStateTests(10);
        UpdateTimes[5] = (sw.Elapsed - swStart).TotalMilliseconds;

        //if (!cameraMode)
        //    Character.GameUpdate();
        Camera.GameUpdate();
        sw.Stop();
        UpdateTimes[0] = sw.Elapsed.TotalMilliseconds;
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
            if (p.Value == movingObjectWorkPtr)
            {
                Map.Move(p.Key);
                MovingObjTest(p.Key);
            }
            else
            {
                p.Key.UpdateMapPosIfMoved(Map);
            }
        }

        movingObjectWorkPtr++;
        if (movingObjectWorkPtr >= movingObjectMaxPtr)
            movingObjectWorkPtr = 0;
    }

    private void MovingObjTest(Placeable p)
    {
        if (Ksids.IsParentOrEqual(p.Ksid, Ksid.SandLike) && p.IsNonMoving)
        {
            float y = Mathf.Repeat(p.Pivot.y, Map.CellSize.y);
            float dy = p.PosOffset.y + p.Size.y;

            if (y + dy > Map.CellSizeY3div4)
                Map.AddCellStateTest(Map.WorldToCell(p.Pivot), p.CellZ == 0 ? CellStateCahnge.CompactSand0 : CellStateCahnge.CompactSand1);
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

    internal void AddMovingObject(Placeable p)
    {
        movingObjects[p] = movingObjectInsterPtr++;
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
        Camera.PairWithCharacter(Character);
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

    public Transform GetHoldMarker(float life)
    {
        foreach (var hm in HoldMarkers)
        {
            if (!hm.gameObject.activeInHierarchy)
            {
                hm.gameObject.SetActive(true);
                Timer.Plan(DeactivateHoldMarkerA, life, hm.gameObject, holdMarkersToken);
                return hm;
            }
        }
        return null;
    }

    private void DeactivateHoldMarker(object prm, int token)
    {
        if (token == holdMarkersToken)
            ((GameObject)prm).SetActive(false);
    }

    public void ClearAllHoldMarkers()
    {
        holdMarkersToken++;
        foreach (var hm in HoldMarkers)
        {
            if (hm.gameObject.activeInHierarchy)
                hm.gameObject.SetActive(false);
        }
    }

    void FixedUpdate()
    {
        foreach (var o in activeObjects)
        {
            o.GameFixedUpdate();
        }
        StaticPhysics.Update();
    }
}