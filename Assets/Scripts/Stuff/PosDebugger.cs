using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Assets.Scripts.Utils;
using Assets.Scripts.Map;
using Assets.Scripts.Core;

namespace Assets.Scripts.Stuff
{
    public class PosDebugger : MonoBehaviour
    {
        private void Update()
        {
            transform.position = Game.Instance.InputController.GetMousePosOnZPlane(1);
        }
    }
}
