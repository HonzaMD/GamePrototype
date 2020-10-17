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

    public Character3 Character;
    public SimpleCameraController Camera;
    public Level Level;
    public Ksids Ksids { get; private set; }

    private List<Trigger> triggers = new List<Trigger>();
    private HashSet<IActiveObject> activeObjects = new HashSet<IActiveObject>();
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
