using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Core.Inventory
{
    internal class Hud : MonoBehaviour
    {
        private UnityEngine.UIElements.Label fpsLabel;
        private VisualElement console;
        private float fpsTimeStamp;
        private int framesCount;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            fpsLabel = doc.rootVisualElement.Q<UnityEngine.UIElements.Label>("FpsLabel");
            console = doc.rootVisualElement.Q<VisualElement>("Console");

            Application.logMessageReceived += Application_logMessageReceived;
        }

        private void Application_logMessageReceived(string condition, string stackTrace, LogType type)
        {
            var label = console.ElementAt(0) as UnityEngine.UIElements.Label;
            label.text = condition;
            label.BringToFront();
        }

        private void Update()
        {
            framesCount++;
            fpsTimeStamp += Time.unscaledDeltaTime;
            if (fpsTimeStamp > 1)
            {
                fpsLabel.text = NumberToString.Convert((int)(framesCount / fpsTimeStamp));
                framesCount = 0;
                fpsTimeStamp = 0;
            }
        }
    }
}
