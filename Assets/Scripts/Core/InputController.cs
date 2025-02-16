using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityTemplateProjects;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Assets.Scripts.Utils;

namespace Assets.Scripts.Core
{
    public class InputController : MonoBehaviour, IActiveObject
    {
        public Character3 Character;
        public SimpleCameraController Camera;
        public ThrowController ThrowController;

        private int characterPos;
        private readonly List<Character3> characters = new();

        private Vector3 mousePosInWord;

        public List<Character3> Characters => characters;

        public void SetupCharacter()
        {
            if (characterPos >= characters.Count)
                characterPos = 0;
            var old = Character;

            if (characters.Count == 0)
            {
                Character = null;
            }
            else
            {
                Character = characters[characterPos];
            }

            if (old != Character)
            {
                if (old)
                    old.DeactivateInput();
                if (Character)
                {
                    Camera.PairWithCharacter(Character);
                    Character.ActivateInput(this);
                    Game.Instance.TrySwitchWorlds(Character.ActiveMap.Id);
                }
            }
        }

        public void AddCharacter(Character3 character) => characters.Add(character);

        public void SetNextCharacter()
        {
            characterPos++;
            SetupCharacter();
        }

        internal void SetCharacterInSelectedMap()
        {
            for (characterPos = 0; characterPos < characters.Count; characterPos++)
            {
                if (characters[characterPos].ActiveMap == Game.Instance.MapWorlds.SelectedMap)
                {
                    SetupCharacter();
                    break;
                }
            }
        }

        public void GameFixedUpdate()
        {
        }

        public void GameUpdate()
        {
            if (Character)
                Camera.SetAbsolutePosition(Character.transform.position);

            var mousePos = Input.mousePosition;
            mousePos.z = Camera.Camera.nearClipPlane;

            mousePosInWord = Camera.Camera.ScreenToWorldPoint(mousePos);

            Game.Instance.TimeOfDay.ChangeLightVariant(IsBLightVariant());
        }


        public bool IsBLightVariant()
        {
            var v = Character ? Character.ArmSphere.transform.position.XY() : Camera.transform.position.XY();
            return Game.Instance.MapWorlds.SelectedMap.LightVariantMap.Find(v.x, v.y);
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
