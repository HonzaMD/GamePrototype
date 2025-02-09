using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Assets.Scripts.Core.Inventory
{
    internal class Hud : MonoBehaviour
    {
        private UnityEngine.UIElements.Label fpsLabel;
        private float fpsTimeStamp;
        private int framesCount;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            fpsLabel = doc.rootVisualElement.Q<UnityEngine.UIElements.Label>("FpsLabel");
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
