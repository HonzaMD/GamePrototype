using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    internal class FpsCounter : IActiveObject
    {
        private readonly Queue<(int Frames, float Secs)> samples = new();

        private int totalFrames;
        private float totalSeconds;
        private int currentFrames;
        private float currentSeconds;

        private float fps = 10;

        public float Fps => fps; // nikdy neni mensi nez 10

        public void GameFixedUpdate()
        {
        }

        public void GameUpdate()
        {
            totalFrames++;
            currentFrames++;
            totalSeconds += Time.deltaTime;
            currentSeconds += Time.deltaTime;

            if (currentSeconds > 1)
            {
                samples.Enqueue((currentFrames, currentSeconds));
                currentFrames = 0;
                currentSeconds = 0;
                if (samples.Count > 10)
                {
                    var sample = samples.Dequeue();
                    totalFrames -= sample.Frames;
                    totalSeconds -= sample.Secs;
                }
            }

            if (totalSeconds > 0)
            {
                fps = Mathf.Max(10, totalFrames / totalSeconds);
            }
        }
    }
}
