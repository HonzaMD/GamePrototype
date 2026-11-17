using Assets.Scripts.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.Text;

namespace Assets.Scripts.Core
{
    [ExecuteInEditMode]
    public class TimeOfDay : MonoBehaviour
    {
        [Range(0f, 360)]
        public float SunPos;

        [Range(0f, 360)]
        public float MoonPos;
        [Range(0f, 360)]
        public float Moon2Pos;

        [Range(-180, 180)]
        public float AxisTilt;

        [Range(0f, 360)]
        public float Time;

        public Quaternion WorldPos;

        [Range(0f, 360)]
        public float WorldOrientation;

        public Transform Sun;
        public Transform Moon;
        public Transform Moon2;
        public Volume VolumeWithSky;

        [Serializable]
        public struct BakePoint
        {
            [Range(0f, 360)]
            public float BakeTime;
            [Tooltip("Herni cas (stupne), kdy se prepne na dalsi BakePoint")]
            [Range(0f, 360)]
            public float ChangeTime;
            [Tooltip("Time (stupne), na ktery se promita zacatek intervalu")]
            [Range(0f, 360)]
            public float IntervalFrom;
            [Tooltip("Time (stupne), na ktery se promita konec intervalu")]
            [Range(0f, 360)]
            public float IntervalTo;
        }

        public const float DayLength = 24 * 60; // s, 360 stupnu

        public BakePoint[] BakePoints;
        public int SelectedBakeTime;

        [Tooltip("Minimalni zmena Time (stupne), nez se pohnou svetla - omezuje mihotani stinu. Nekopiruje se ze sveta.")]
        [Min(0)]
        public float TimeStep = 0.01f;
        private bool diskCrossing;
        private bool isBLighting;
        public bool IsBLighting => isBLighting;
        private string scenario;

        private struct Data
        {
            public bool? on;
            public Light light;
            public HDAdditionalLightData hdLight;
        }

        private int skyRotationId;
        private Material skyMaterial;
        private Data[] data;
        private float scenarioBlendFactor;

        public int NumScenarios => BakePoints.Length;

        private void OnValidate()
        {
            if (!Application.isPlaying && BakePoints != null && SelectedBakeTime < BakePoints.Length && SelectedBakeTime >= 0)
            {
                Time = BakePoints[SelectedBakeTime].BakeTime;
            }
            SetSunPosition();
        }

        /// <summary>
        /// Nastavi herni cas (s). Pri prechodu do jineho intervalu prepne scenar.
        /// </summary>
        public void SetGameTime(float gameTime)
        {
            int slot = FindSlot(ToDegrees(gameTime), out float time);
            if (slot != SelectedBakeTime)
            {
                SelectedBakeTime = slot;
                SetNextScenarioInit();
                Time = time;
                SetSunPosition();
            }
            else
            {
                UpdateScenarioBlend();
                // pri prechodu kotouce pres obzor plynule kvuli lightDimmer
                if (diskCrossing || Mathf.Abs(Mathf.DeltaAngle(Time, time)) >= TimeStep)
                {
                    Time = time;
                    SetSunPosition();
                }
            }
        }

        private static float ToDegrees(float gameTime) => Mathf.Repeat(gameTime / DayLength * 360f, 360f);

        /// <summary>
        /// Najde interval (ChangeTime predchoziho .. ChangeTime aktualniho) a promitne do nej cas na IntervalFrom .. IntervalTo.
        /// </summary>
        private int FindSlot(float gameDeg, out float time)
        {
            int n = BakePoints != null ? BakePoints.Length : 0;
            for (int i = 0; i < n; i++)
            {
                float start = BakePoints[(i + n - 1) % n].ChangeTime;
                float length = Mathf.Repeat(BakePoints[i].ChangeTime - start, 360f);
                if (length == 0)
                    length = 360f;
                float offset = Mathf.Repeat(gameDeg - start, 360f);
                if (offset < length)
                {
                    ref var p = ref BakePoints[i];
                    float span = Mathf.Repeat(p.IntervalTo - p.IntervalFrom, 360f);
                    time = Mathf.Repeat(p.IntervalFrom + offset / length * span, 360f);
                    return i;
                }
            }
            time = Time;
            return SelectedBakeTime;
        }

        public void ChangeLightVariant(bool insideB)
        {
            if (isBLighting != insideB)
            {
                isBLighting = insideB;
                SetNextScenarioInit();
            }
        }

        public void SetNextScenarioInit()
        {
            scenario = $"Sc{(isBLighting ? 'B' : 'A')}{SelectedBakeTime}";
            Game.Instance.MapWorlds.SelectedMap.WorldBuilder.SetLightScenario(SelectedBakeTime);

            var probeRefVolume = ProbeReferenceVolume.instance;
            scenarioBlendFactor = 0.2f;
            probeRefVolume.BlendLightingScenario(scenario, scenarioBlendFactor);
            Debug.Log($"{probeRefVolume.lightingScenario} => {probeRefVolume.otherScenario}");
        }

        private void UpdateScenarioBlend()
        {
            if (scenarioBlendFactor > 0f)
            {
                scenarioBlendFactor += 0.2f;
                var probeRefVolume = ProbeReferenceVolume.instance;
                if (scenarioBlendFactor <= 1f)
                {
                    probeRefVolume.BlendLightingScenario(scenario, scenarioBlendFactor);
                }
                else 
                { 
                    scenarioBlendFactor = 0;
                    //probeRefVolume.BlendLightingScenario(scenario, 1f);
                    probeRefVolume.lightingScenario = scenario;
                }
            }
        }

        private void SetSunPosition()
        {
            InitData();

            var sunRot = Quaternion.Euler(0, SunPos, 0);
            var moonRot = Quaternion.Euler(0, MoonPos, 0);
            var moon2Rot = Quaternion.Euler(0, Moon2Pos, 0);
            var axisRot = Quaternion.Euler(0, 0, AxisTilt);
            var TimeRot = Quaternion.Euler(0, Time - SunPos, 0);
            var worldRot = Quaternion.Euler(0, WorldOrientation, 0);
            var spaceRot = worldRot * WorldPos * TimeRot * axisRot;
            if (Sun)
                Sun.localRotation =  spaceRot * sunRot;
            if (Moon)
                Moon.localRotation = spaceRot * moonRot;
            if (Moon2)
                Moon2.localRotation = spaceRot * moon2Rot;

            SetSkyRotation(spaceRot);
            SetupShadows();
        }

        private void SetSkyRotation(Quaternion spaceRot)
        {
            if (skyMaterial)
            {
                Matrix4x4 m = Matrix4x4.Rotate(spaceRot);
                skyMaterial.SetMatrix(skyRotationId, m);
            }
        }

        private void SetupShadows()
        {
            bool shadowEnabled = false;
            diskCrossing = false;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].hdLight)
                {
                    float visibility = shadowEnabled ? 0 : DiskVisibility(i);
                    if (visibility > 0 && visibility < 1)
                        diskCrossing = true;
                    bool on = visibility > 0;
                    if (on)
                        shadowEnabled = true;
                    if (on != data[i].on)
                    {
                        data[i].on = on;
                        data[i].hdLight.EnableShadows(on);
                    }
                    data[i].hdLight.lightDimmer = visibility;
                }
            }
        }

        /// <summary>
        /// Viditelna cast kotouce nad obzorem (0..1). Smoothstep aproximuje plochu kruhove usece.
        /// </summary>
        private float DiskVisibility(int i)
        {
            var hdLight = data[i].hdLight;
            var lightDir = hdLight.transform.rotation * Vector3.back;
            float sinElevation = Vector3.Dot(Vector3.up, lightDir);
            float sinRadius = Mathf.Sin(0.5f * hdLight.angularDiameter * Mathf.Deg2Rad);
            if (sinRadius <= 0)
                return sinElevation >= 0 ? 1 : 0;
            if (sinElevation < -sinRadius)
                return 0;
            return Mathf.SmoothStep(0, 1, (sinElevation + sinRadius) / (2 * sinRadius));
        }

        private void InitData()
        {
            if (data == null)
            {
                skyRotationId = Shader.PropertyToID("_SkyRotation");
                if (VolumeWithSky)
                {
                    var profile = VolumeWithSky.sharedProfile;
                    if (profile.TryGet<PhysicallyBasedSky>(out var sky))
                        skyMaterial = sky.material.value;
                }

                data = new Data[3];
                if (Sun)
                {
                    data[0].light = Sun.GetComponent<Light>();
                    data[0].hdLight = Sun.GetComponent<HDAdditionalLightData>();
                }
                
                if (Moon)
                {
                    data[1].light = Moon.GetComponent<Light>();
                    data[1].hdLight = Moon.GetComponent<HDAdditionalLightData>();
                }

                if (Moon2)
                {
                    data[2].light = Moon2.GetComponent<Light>();
                    data[2].hdLight = Moon2.GetComponent<HDAdditionalLightData>();
                }
            }
        }

        internal void CopySettingsFrom(TimeOfDay timeOfDay, float gameTime)
        {
            if (!timeOfDay)
                return;
            SunPos = timeOfDay.SunPos;
            MoonPos = timeOfDay.MoonPos;
            Moon2Pos = timeOfDay.Moon2Pos;
            AxisTilt = timeOfDay.AxisTilt;
            WorldPos = timeOfDay.WorldPos;
            WorldOrientation = timeOfDay.WorldOrientation;
            BakePoints = timeOfDay.BakePoints;
            SelectedBakeTime = FindSlot(ToDegrees(gameTime), out Time);
            scenarioBlendFactor = 0;
            SetSunPosition();
        }

        internal void ResetAPVs(bool isBLights, WorldBuilder worldBuilder)
        {
            isBLighting = isBLights;

            SceneManager.SetActiveScene(worldBuilder.gameObject.scene);
            var probeRefVolume = ProbeReferenceVolume.instance;
            probeRefVolume.SetActiveScene(worldBuilder.gameObject.scene);
            Debug.Log($"Init: World: {worldBuilder.Id} sc: {probeRefVolume.lightingScenario}, loaded: {probeRefVolume.DataHasBeenLoaded()}");

            //scenario = $"Sc{(IsBLighting ? 'B' : 'A')}{SelectedBakeTime}";
            //scenarioBlendFactor = 0f;
            //OnValidate();

            //probeRefVolume.lightingScenario = scenario;
            //Debug.Log($"PV Baking set: {probeRefVolume.currentBakingSet.name} Scenario Setup: {scenario}");
        }

        internal bool ResetAPVs2()
        {
            var probeRefVolume = ProbeReferenceVolume.instance;
            if (probeRefVolume.currentBakingSet != null && !probeRefVolume.DataHasBeenLoaded())
                return false;
            Debug.Log($"Init2: {probeRefVolume.lightingScenario}, loaded: {probeRefVolume.DataHasBeenLoaded()}");
            SetNextScenarioInit();
            return true;
        }
    }
}
