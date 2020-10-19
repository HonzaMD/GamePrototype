using Assets.Scripts;
using Assets.Scripts.Core;
using Assets.Scripts.Map;
using System;
using System.Collections;
using System.Collections.Generic;
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

    private List<Trigger> triggers = new List<Trigger>();
    private HashSet<IActiveObject> activeObjects = new HashSet<IActiveObject>();
    private Dictionary<Placeable, int> movingObjects = new Dictionary<Placeable, int>();
    private int movingObjectInsterPtr;
    private int movingObjectWorkPtr;
    private const int movingObjectMaxPtr = 20;

    private bool cameraMode;

    void Update()
    {
        UpdateTriggers();

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

        UpdateMovingObjects();
        UpdateObjects();

        if (!cameraMode)
            Character.GameUpdate();
        Camera.GameUpdate();
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
        Character.Camera = Camera;
        Camera.SetTransform(Character.transform.position);
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
}
