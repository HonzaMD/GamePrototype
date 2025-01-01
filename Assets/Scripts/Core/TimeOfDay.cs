using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Core
{
    [ExecuteInEditMode]
    public class TimeOfDay : MonoBehaviour
    {
        [Range(0f, 360)]
        public float SunPos;

        [Range(0f, 360)]
        public float MoonPos;

        [Range(-180, 180)]
        public float AxisTilt;

        [Range(0f, 360)]
        public float Time;

        public Quaternion WorldPos;

        public Transform Moon;

        private void OnValidate()
        {
            SetSunPosition();
        }

        private void SetSunPosition()
        {
            var sunRot = Quaternion.Euler(0, SunPos, 0);
            var moonRot = Quaternion.Euler(0, MoonPos, 0);
            var axisRot = Quaternion.Euler(0, 0, AxisTilt);
            var TimeRot = Quaternion.Euler(0, Time, 0);
            var spaceRot = WorldPos * TimeRot * axisRot;
            transform.localRotation =  spaceRot * sunRot;
            Moon.localRotation = spaceRot * moonRot;
        }
    }
}
