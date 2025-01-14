using Assets.Scripts.Map;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
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

        public float[] BakeTimes;
        public int SelectedBakeTime;
        private bool IsBLighting;
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


        private void OnValidate()
        {
            if (BakeTimes != null && SelectedBakeTime < BakeTimes.Length && SelectedBakeTime >= 0)
            {
                Time = BakeTimes[SelectedBakeTime];
            }
            SetSunPosition();
        }

        public void ChangeLightVariant(bool insideB)
        {
            if (IsBLighting != insideB)
            {
                IsBLighting = insideB;
                SetNextScenarioInit();
            }
        }

        public void SetNextScenario()
        {
            SelectedBakeTime++;
            if (SelectedBakeTime >= BakeTimes.Length)
                SelectedBakeTime = 0;
            SetNextScenarioInit();
        }

        public void SetNextScenarioInit()
        {
            scenario = $"Sc{(IsBLighting ? 'B' : 'A')}{SelectedBakeTime}";
            OnValidate();

            var probeRefVolume = ProbeReferenceVolume.instance;
            scenarioBlendFactor = 0.2f;
            probeRefVolume.BlendLightingScenario(scenario, scenarioBlendFactor);
            Debug.Log($"{probeRefVolume.lightingScenario} => {probeRefVolume.otherScenario}");
        }

        public void SetNextScenario2()
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
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].hdLight)
                {
                    bool on = !shadowEnabled && IsAboveHorizon(i);
                    if (on)
                        shadowEnabled = true;
                    if (on != data[i].on)
                    {
                        data[i].on = on;
                        data[i].hdLight.EnableShadows(on);
                        data[i].hdLight.lightDimmer = on ? 1 : 0;
                    }
                }
            }
        }

        private bool IsAboveHorizon(int i)
        {
            var lightDir = data[i].hdLight.transform.rotation * Vector3.back;
            return Vector3.Dot(Vector3.up, lightDir) >= 0;
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

        internal void CopySettingsFrom(TimeOfDay timeOfDay)
        {
            SunPos = timeOfDay.SunPos;
            MoonPos = timeOfDay.MoonPos;
            Moon2Pos = timeOfDay.Moon2Pos;
            AxisTilt = timeOfDay.AxisTilt;
            WorldPos = timeOfDay.WorldPos;
            WorldOrientation = timeOfDay.WorldOrientation;
            BakeTimes = timeOfDay.BakeTimes;
            if (SelectedBakeTime > 0)
                SelectedBakeTime = SelectedBakeTime % BakeTimes.Length;
        }

        internal void ResetAPVs(bool isBLights, WorldBuilder worldBuilder)
        {
            IsBLighting = isBLights;

            var probeRefVolume = ProbeReferenceVolume.instance;
            probeRefVolume.SetActiveScene(worldBuilder.gameObject.scene);
            Debug.Log($"Init: {probeRefVolume.lightingScenario}, loaded: {probeRefVolume.DataHasBeenLoaded()}");

            //scenario = $"Sc{(IsBLighting ? 'B' : 'A')}{SelectedBakeTime}";
            //scenarioBlendFactor = 0f;
            //OnValidate();

            //probeRefVolume.lightingScenario = scenario;
            //Debug.Log($"PV Baking set: {probeRefVolume.currentBakingSet.name} Scenario Setup: {scenario}");
        }

        internal bool ResetAPVs2()
        {
            var probeRefVolume = ProbeReferenceVolume.instance;
            if (!probeRefVolume.DataHasBeenLoaded())
                return false;
            Debug.Log($"Init: {probeRefVolume.lightingScenario}, loaded: {probeRefVolume.DataHasBeenLoaded()}");
            SetNextScenarioInit();
            return true;
        }
    }
}
