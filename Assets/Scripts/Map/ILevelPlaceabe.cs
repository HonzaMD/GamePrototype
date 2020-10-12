using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Map
{
    internal interface ILevelPlaceabe
    {
        void Instantiate(Map map, Transform parent, Vector3 pos);
    }
}
