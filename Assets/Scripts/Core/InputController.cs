using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityTemplateProjects;

namespace Assets.Scripts.Core
{
    public class InputController : MonoBehaviour, IActiveObject
    {
        public Character3 Character;
        public SimpleCameraController Camera;
        public ThrowController ThrowController;

        private Vector3 mousePosInWord;

        public void Init()
        {
            Camera.PairWithCharacter(Character);
            Character.ActivateInput(this);
        }

        public void GameFixedUpdate()
        {
        }

        public void GameUpdate()
        {
            var mousePos = Input.mousePosition;
            mousePos.z = Camera.Camera.nearClipPlane;

            mousePosInWord = Camera.Camera.ScreenToWorldPoint(mousePos);
        }

        public Vector3 GetMousePosOnZPlane(float z)
        {
            Vector3 cameraPos = Camera.transform.position;
            float koef = (z - cameraPos.z) / (mousePosInWord.z - cameraPos.z);
            float x = (mousePosInWord.x - cameraPos.x) * koef + cameraPos.x;
            float y = (mousePosInWord.y - cameraPos.y) * koef + cameraPos.y;
            return new(x, y, z);
        }
    }
}
