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
using Assets.Scripts.Core.Inventory;

namespace Assets.Scripts.Core
{
    // Vstup hrace za jeden fyzikalni krok. Pojmenovano podle akci, ne podle klaves.
    // Level signal (*Held, osy) hranici Update/FixedUpdate snese, pri predani snimku se nenuluje.
    // Hrana (*Pressed/*Released, pozadavky, akumulatory) ne: pri vysokem fps by ji dalsi
    // Update prepsal, pri nizkem by probehla vickrat. Proto se akumuluje pres |= a nuluje
    // ji az predani snimku. Parametry hrany se zachyti v okamziku hrany.
    public struct PlayerInput
    {
        public Vector2 MoveAxes;
        public bool CatchHeld;                  // prave tlacitko / C
        public bool ThrowHeld;                  // R
        public bool ZMoveHeld;                  // Ctrl
        public bool SlowHeld;                   // Shift

        public bool PickupPressed;              // E
        public bool PickupReleased;
        public bool ThrowPressed;               // R
        public bool ThrowReleased;
        public bool PrimaryPressed;             // leve tlacitko mysi, mimo GUI
        public bool PrimaryReleased;
        public bool PrimaryPressedWithPickup;   // stav E v okamziku kliknuti
        public bool JumpPressed;
        public float JumpPressTime;             // plati jen s JumpPressed
        public int InventorySlot;               // klavesy 0-9, 0 = zadny
        public Label InventoryKey;              // klik v InventoryVisualizer
        public Vector2 HoldAdjustShift;         // pohyb mysi kolmo na holdTarget, pro ItemAdjust
        public bool MoveModeTogglePressed;      // TEST MoveMode (smazat) - G

        public void ClearEdges()
        {
            MoveModeTogglePressed = false;      // TEST MoveMode (smazat)
            PickupPressed = false;
            PickupReleased = false;
            ThrowPressed = false;
            ThrowReleased = false;
            PrimaryPressed = false;
            PrimaryReleased = false;
            JumpPressed = false;
            InventorySlot = 0;
            InventoryKey = null;
            HoldAdjustShift = Vector2.zero;
        }
    }

    public class InputController : MonoBehaviour, IActiveObject
    {
        public bool PendingRemove { get; set; }
        public Character3 Character;
        public SimpleCameraController Camera;
        public ThrowController ThrowController;

        // Markery zarovnane na bunky, kterymi mire drzeny IHandAimer (DirtBuilder).
        // Scenove objekty, defaultne neaktivni. UpdateAim si jeden vybere, SetActiveMarker ho rozsviti.
        public Transform CellMarkerGreen;
        public Transform CellMarkerRed;
        private Transform activeMarker;

        private int characterPos;
        private readonly List<Character3> characters = new();

        // Mysi paprsek vzorkovany v GameUpdate. Uklada se i pozice kamery v okamziku vzorku:
        // GetMousePosOnZPlane z te dvojice rekonstruuje smer paprsku, a kdyby cetl pozici
        // kamery zive, michal by smer z jednoho framu s pozici z jineho.
        private Vector3 mousePosInWord;
        private Vector3 mouseRayOrigin;
        private bool mouseSampled;

        // Vstupni buffer: SampleInput (Update) plni pending, GameFixedUpdate ho preda jako
        // frame - jeden snimek pro cely fixed krok, at nezalezi na tom, kdo ho cte a kdy.
        private PlayerInput pending;
        private PlayerInput frame;

        public List<Character3> Characters => characters;

        public ref readonly PlayerInput Frame => ref frame;

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
                DropInputEdges();
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

        public void AddCharacter(Character3 character)
        {
            characters.Add(character);
            Game.Instance.Hud.InvalidateStatusRow();
        }

        public void RemoveCharacter(Character3 character)
        {
            int index = characters.IndexOf(character);
            if (index < 0)
                return;

            characters.RemoveAt(index);

            if (index == characterPos)
            {
                DropInputEdges();
                character.DeactivateInput();
                Character = null;
                Game.Instance.Hud.SetupInventory(null);
            }
            else if (index < characterPos)
            {
                characterPos--;
            }

            Game.Instance.Hud.InvalidateStatusRow();
        }

        public void SetNextCharacter()
        {
            characterPos++;
            SetupCharacter();
        }

        public void SetCharacter(int index)
        {
            characterPos = index;
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

        // Bezi jako prvni v Game.FixedUpdate: preda snimek vstupu pro cely fixed krok.
        // Probehne-li za frame vic fixed kroku, hrany dostane jen prvni.
        public void GameFixedUpdate()
        {
            frame = pending;
            pending.ClearEdges();
        }

        public void GameUpdate()
        {
            if (Character)
                Camera.SetAbsolutePosition(Character.transform.position);

            var mousePos = Input.mousePosition;
            mousePos.z = Camera.Camera.nearClipPlane;

            mousePosInWord = Camera.Camera.ScreenToWorldPoint(mousePos);
            mouseRayOrigin = Camera.transform.position;
            mouseSampled = true;

            SampleInput();

            // Prezentace pro hrace. Bez aktivni postavy se marker DirtBuilderu zhasne.
            SetActiveMarker(Character ? Character.UpdateHandAim() : null);
            if (Character)
                Character.UpdateThrowMarkers();

            Game.Instance.TimeOfDay.ChangeLightVariant(IsBLightVariant());
        }

        // Jedine misto, kde se cte herni Input. Vzorkuje se i bez vybrane postavy -
        // snimek pak nikdo neprecte a hrany zahodi dalsi predani.
        private void SampleInput()
        {
            bool guiInFocus = Game.Instance.Hud.GuiInFocus;
            bool pickupHeld = Input.GetKey(KeyCode.E);

            pending.MoveAxes = new Vector2(Mathf.Clamp(Input.GetAxis("Horizontal"), -1, 1), Mathf.Clamp(Input.GetAxis("Vertical"), -1, 1));
            pending.CatchHeld = Input.GetMouseButton(1) || Input.GetKey(KeyCode.C);
            pending.ThrowHeld = Input.GetKey(KeyCode.R);
            pending.ZMoveHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            pending.SlowHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            pending.PickupPressed |= Input.GetKeyDown(KeyCode.E);
            pending.PickupReleased |= Input.GetKeyUp(KeyCode.E);
            pending.ThrowPressed |= Input.GetKeyDown(KeyCode.R);
            pending.ThrowReleased |= Input.GetKeyUp(KeyCode.R);
            pending.PrimaryReleased |= Input.GetMouseButtonUp(0);
            pending.MoveModeTogglePressed |= Input.GetKeyDown(KeyCode.G);   // TEST MoveMode (smazat)
            pending.HoldAdjustShift += new Vector2(Input.GetAxis("Mouse Y"), -Input.GetAxis("Mouse X"));

            if (Input.GetMouseButtonDown(0) && !guiInFocus)
            {
                pending.PrimaryPressed = true;
                pending.PrimaryPressedWithPickup = pickupHeld;
            }

            if (Input.GetButtonDown("Jump"))
            {
                pending.JumpPressed = true;
                pending.JumpPressTime = Time.time;
            }

            // Klavesa 0-9 s vybranou polozkou v HUD jen priradi quick slot - to je cisty
            // inventar, provede se hned. Jinak je to pozadavek na aktivaci pro postavu.
            int slot = KeysToInventory.TestKeys();
            if (slot != 0)
            {
                var selectedKey = Game.Instance.Hud.SelectedInventoryKey;
                if (selectedKey != null)
                {
                    if (Character)
                        Character.Inventory.SetQuickSlot(slot, selectedKey);
                }
                else
                {
                    pending.InventorySlot = slot;
                }
            }
        }

        // Klik v InventoryVisualizer - kdykoli behem framu. Provede ho postava ve fixed kroku.
        public void RequestInventoryAccess(Label key)
        {
            pending.InventoryKey = key;
        }

        // Rozpracovane hrany patri postave, pro kterou je hrac zmackl. Prenesene na jinou
        // by ji nechaly skocit nebo bodnout nozem, proto se pri prepnuti zahodi.
        // Level signaly zustavaji - jsou to fyzicky drzene klavesy.
        private void DropInputEdges()
        {
            pending.ClearEdges();
            frame.ClearEdges();
        }


        public bool IsBLightVariant()
        {
            var v = Character ? Character.ArmSphere.transform.position.XY() : Camera.transform.position.XY();
            return Game.Instance.MapWorlds.SelectedMap.LightVariantMap.Find(v.x, v.y);
        }

        // Rozsviti zadany marker a zhasne ten predchozi. null = zadny marker.
        private void SetActiveMarker(Transform marker)
        {
            if (activeMarker == marker)
                return;
            if (activeMarker)
                activeMarker.gameObject.SetActive(false);
            activeMarker = marker;
            if (activeMarker)
                activeMarker.gameObject.SetActive(true);
        }

        // Kam mysi paprsek protne rovinu Z. Cte se i z GameFixedUpdate, proto stavi
        // na vzorku z posledniho GameUpdate, ne na zive pozici kamery.
        public Vector3 GetMousePosOnZPlane(float z)
        {
            // Prvni fyzikalni krok po odpauzovani muze predbehnout prvni GameUpdate.
            // Bez fallbacku by paprsek mel nulovou delku a vratil NaN do fyziky.
            Vector3 cameraPos = mouseSampled ? mouseRayOrigin : Camera.transform.position;
            float koef = (z - cameraPos.z) / (mousePosInWord.z - cameraPos.z);
            float x = (mousePosInWord.x - cameraPos.x) * koef + cameraPos.x;
            float y = (mousePosInWord.y - cameraPos.y) * koef + cameraPos.y;
            return new(x, y, z);
        }
    }
}
