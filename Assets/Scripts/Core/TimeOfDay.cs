using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

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

        public Transform Moon;
        public Transform Moon2;

        private struct Data
        {
            public bool? on;
            public Light light;
            public HDAdditionalLightData hdLight;
        }

        private Data[] data;


        private void OnValidate()
        {
            SetSunPosition();
        }

        private void SetSunPosition()
        {
            var sunRot = Quaternion.Euler(0, SunPos, 0);
            var moonRot = Quaternion.Euler(0, MoonPos, 0);
            var moon2Rot = Quaternion.Euler(0, Moon2Pos, 0);
            var axisRot = Quaternion.Euler(0, 0, AxisTilt);
            var TimeRot = Quaternion.Euler(0, Time - SunPos, 0);
            var spaceRot = WorldPos * TimeRot * axisRot;
            transform.localRotation =  spaceRot * sunRot;
            if (Moon)
                Moon.localRotation = spaceRot * moonRot;
            if (Moon2)
                Moon2.localRotation = spaceRot * moon2Rot;

            SetupShadows();
        }

        private void SetupShadows()
        {
            InitData();
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
                data = new Data[3];
                data[0].light = GetComponent<Light>();
                data[0].hdLight = GetComponent<HDAdditionalLightData>();
                
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
    }
}
