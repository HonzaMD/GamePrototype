using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

namespace Assets.Scripts.Core
{
    /// <summary>
    /// Zrychleny cas: kazdy frame = presne FixedPerUpdate fixed kroku + 1 Update, frame jede co nejrychleji.
    /// Vykresluje se jen kazdy N-ty frame (~RenderPeriod realneho casu).
    /// </summary>
    public class FastModeDriver
    {
        private const int FixedPerUpdate = 3;
        private const float RenderPeriod = 0.2f; // s realneho casu
        private const int InitialRenderInterval = 10;

        public bool IsOn { get; private set; }

        private int savedVSync;
        private int savedFrameRate;
        private readonly Stopwatch realTime = new();
        private double statsStart;
        private float statsGameStart;
        private int statsFrames;

        public void Toggle(float gameTime)
        {
            if (IsOn)
                Stop();
            else
                Start(gameTime);
        }

        private void Start(float gameTime)
        {
            IsOn = true;
            savedVSync = QualitySettings.vSyncCount;
            savedFrameRate = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Time.captureDeltaTime = FixedPerUpdate * Time.fixedDeltaTime;
            OnDemandRendering.renderFrameInterval = InitialRenderInterval;

            realTime.Restart();
            statsStart = 0;
            statsGameStart = gameTime;
            statsFrames = 0;
            Debug.Log("FastMode ON");
        }

        public void Stop()
        {
            if (!IsOn)
                return;
            IsOn = false;
            Time.captureDeltaTime = 0;
            OnDemandRendering.renderFrameInterval = 1;
            QualitySettings.vSyncCount = savedVSync;
            Application.targetFrameRate = savedFrameRate;
            realTime.Stop();
            Debug.Log("FastMode OFF");
        }

        /// <summary>
        /// Jednou za realnou sekundu zaloguje zrychleni a prepocita interval vykreslovani.
        /// Interval se nemeni kazdy frame, protoze Unity renderuje podle frameCount % interval.
        /// </summary>
        /// <returns>true, kdyz v tomto framu logoval (volajici muze pridat vlastni statistiky)</returns>
        public bool Update(float gameTime)
        {
            if (!IsOn)
                return false;

            statsFrames++;
            double now = realTime.Elapsed.TotalSeconds;
            double elapsed = now - statsStart;
            if (elapsed >= 1)
            {
                float frameTime = (float)(elapsed / statsFrames);
                int interval = Mathf.Max(1, Mathf.RoundToInt(RenderPeriod / frameTime));
                Debug.Log($"FastMode: speed {(gameTime - statsGameStart) / elapsed:F1}x, updates/s {statsFrames / elapsed:F0}, frame {frameTime * 1000:F2} ms, render every {OnDemandRendering.renderFrameInterval} -> {interval}");
                OnDemandRendering.renderFrameInterval = interval;

                statsStart = now;
                statsGameStart = gameTime;
                statsFrames = 0;
                return true;
            }
            return false;
        }
    }
}
