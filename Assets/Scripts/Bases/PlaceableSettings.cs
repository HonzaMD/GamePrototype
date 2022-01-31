using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    public enum DensityMode
    {
        BoundingBox,
        Circle,
    }

    [CreateAssetMenu]
    public class PlaceableSettings : ScriptableObject
    {
        public bool HasSubPlaceables;
        public bool UseSimpleBBCollisions;
        public float Mass;
        public float Density = 1000f; // hustota vody
        public DensityMode DensityMode;
    }
}
