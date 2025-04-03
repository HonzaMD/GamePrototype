using Assets.Scripts;
using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityTemplateProjects;

[RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody))]
public class Character3 : ChLegsArms, IActiveObject, IHasInventory
{
    private float lastJumpTime;

    private float zMoveTimeout;
    private float controlTimeout;
    private float resetHoldTimeout;

    private Inventory inventory;

    private InputController inputController;
    private Rigidbody bodyToThrow;
    private ControlState cState;
    private bool firstPress;
    private HoldAnimator holdAnimator;

    private enum ControlState
    { 
        EmptyHands, // default s prazdnyma rukama
        PickupPrepare, // Behem drzeni E, pokud neprerusis mysi
        Pickup, // po nepreruzenem PickapPrepare (po pusteni E), Druhy pickupPrepare Pickup vypne. Mys pickup vypne. Pickup konci po 2s neaktivite (nic jsi nesebral)
        TryHold, // aktivovani mysi ze satvu EmptyHands,PickupPrepare a ItemUse, u ItemUse jen pri drzeni E. Deaktivuje se zvednutim mysi. Pohybem mysi muze prejit do ItemAdjust
        ItemAdjust, // aktivuje se pohybem zmacknute mysi ze stavu TryHold (ten vznikne ze stavu PickupPrepare). Deaktivuje se zvednutim mysi.
        Throw, // Aktivace pokud neco drzis stiskem R. Nebo z Throwreload, kdyz drzis novou vec. Deaktivace pustenim druheho stisku R
        ThrowReload, // aktivace pokud pri hodu (mys pri Throw) pokud drzis R. Po 0.2s naloaduje vec z inventare a prejde do Throw
        ItemUse, // default s plnyma rukama
        ItemAnimation, // Aktivuje se stiskem mysi z ItemUse stavu. Trva nejakou dobu. Behem animace je azkazano spousta veci a prechodu
    }

    // Prechody stavu:
    // E, R,
    // mouse down. Pri Throw hazi, pri empty hands nebo pickup prepare sbira

    void Awake()
    {
        AwakeB();
    }

    public override void AfterMapPlaced(Map map, Placeable placeableSibling, bool goesFromInventory)
    {
        base.AfterMapPlaced(map, placeableSibling, goesFromInventory);
        CreateInventory();
        Game.Instance.InputController.AddCharacter(this);
    }

    private void CreateInventory()
    {
        inventory = Game.Instance.PrefabsStore.Inventory.Create(Game.Instance.InventoryRoot, Vector3.zero, null);
        inventory.SetupIdentity(CharacterNames.GiveMeName(), InventoryType.Character, placeable.Settings.Icon);
        inventory.StoreProto(Game.Instance.PrefabsStore.Gravel, 5);
        inventory.SetQuickSlot(-9, Game.Instance.PrefabsStore.Gravel);
        inventory.StoreProto(Game.Instance.PrefabsStore.StickyBomb, 30);
        inventory.SetQuickSlot(-8, Game.Instance.PrefabsStore.StickyBomb);
        inventory.StoreProto(Game.Instance.PrefabsStore.PointLight, 3);
        inventory.SetQuickSlot(-7, Game.Instance.PrefabsStore.PointLight);
    }

    public void GameUpdate()
    {
        if (!inputController)
            UncontrolledUpdate();
        else
            ControlledUpdate();
    }

    private void ControlledUpdate()
    {
        var throwCtrl = inputController.ThrowController;

        UpdatePosition();

        bool jumpButton = Input.GetButtonDown("Jump");
        desiredCatch = Input.GetMouseButton(1) || Input.GetKey(KeyCode.C);

        var slot = KeysToInventory.TestKeys();
        if (slot != 0)
        {
            if (Game.Instance.Hud.SelectedInventoryKey != null)
            {
                inventory.SetQuickSlot(slot, Game.Instance.Hud.SelectedInventoryKey);
            }
            else if (cState != ControlState.ItemAnimation)
            {
                InventoryAccess(slot);
            }
        }

        controlTimeout += Time.deltaTime;
        if (cState == ControlState.Pickup && controlTimeout > 2f)
            ResetControl();
        if (cState == ControlState.ThrowReload && controlTimeout > 0.2f)
        {
            if (!IsInventoryActive)
            {
                if (inventory.TryGetSlot(inventory.LastKey, out var lastSlot))
                    InventoryAccess(lastSlot);
                if (!IsInventoryActive)
                {
                    ResetControl();
                }
                else
                {
                    throwCtrl.SetThrowActive(true, false, this);
                }
            }
        }

        if (resetHoldTimeout > 0)
            resetHoldTimeout += Time.deltaTime;
        if (resetHoldTimeout > 0.6f && !(cState is ControlState.ItemAdjust or ControlState.TryHold or ControlState.ItemAnimation))
        {
            RecatchHold();
            resetHoldTimeout = 0;
        }

        if (Input.GetKeyDown(KeyCode.E) && cState != ControlState.ItemAnimation)
        {
            if (ResetControl() != ControlState.Pickup)
                firstPress = true;
            cState = ControlState.PickupPrepare;
        }
        if (Input.GetKeyUp(KeyCode.E) && cState == ControlState.PickupPrepare)
        {
            if (firstPress)
            {
                desiredPickUp = true;
                cState = ControlState.Pickup;
            }
            else
            {
                ResetControl();
            }
            //holdRotationAngle = 0;
            //if (!throwCtrl.ThrowActive)
            //{
            //    if (dropHold)
            //    {
            //        holdTarget = Vector2.zero;
            //        dropHold = false;
            //        desiredHold = false;
            //    }
            //    else
            //    {
            //        RecatchHold();
            //    }
            //}
        }

        bool guiInFocus = Game.Instance.Hud.GuiInFocus;
        bool mouseDown = !guiInFocus && cState != ControlState.ItemAnimation && Input.GetMouseButtonDown(0);
        bool mouseUp = Input.GetMouseButtonUp(0);

        if (mouseDown && (cState is ControlState.PickupPrepare or ControlState.EmptyHands || (cState is ControlState.ItemUse && Input.GetKey(KeyCode.E))))
        {
            desiredPickUp = false;
            pickupToHold = true;
            desiredHold = true;
            cState = ControlState.TryHold;
        }

        if (mouseDown && cState == ControlState.Pickup)
            ResetControl();

        if (ArmCatched && desiredCatch)
        {
            desiredJump = false;
        }
        else
        {
            if (jumpButton)
            {
                desiredJump = true;
                lastJumpTime = Time.time;
            }
            else if (lastJumpTime + 0.3f < Time.time)
            {
                desiredJump = false;
            }
        }

        AnimateHand();
        AdjustLegsArms(cState != ControlState.ItemAnimation);

        bool armHolds = ArmHolds;
        
        if (armHolds)
        {
            if (cState == ControlState.EmptyHands)
                cState = ControlState.ItemUse;
            if (cState == ControlState.ThrowReload)
                cState = ControlState.Throw;
        }
        else
        {
            if (cState == ControlState.Throw)
                ResetControl();
            if (cState == ControlState.ItemUse)
                cState = ControlState.EmptyHands;
        }

        if (Input.GetKeyDown(KeyCode.R) && cState != ControlState.ItemAnimation)
        {
            firstPress = false;
            if (cState != ControlState.Throw && armHolds)
            {
                ResetControl();
                firstPress = true;
                cState = ControlState.Throw;
                throwCtrl.SetThrowActive(true, false, this);
            }
        }

        if (Input.GetKeyUp(KeyCode.R) && !firstPress && cState != ControlState.ItemAnimation)
        {
            ResetControl();
        }

        if (mouseDown && throwCtrl.ThrowActive && cState == ControlState.Throw)
        {
            throwCtrl.SetThrowActive(false, true, this);
            if (cState != ControlState.ThrowReload)
                ResetControl();
        }

        if (mouseDown && cState == ControlState.ItemUse)
        {
            var ho = GetHoldObject();
            if (ho.KsidGet.IsChildOf(Ksid.ActivatesInHand) && ho.TryGetComponent(out IHoldActivate ao))
                ao.Activate(this);
        }


        if (mouseUp && cState is ControlState.TryHold or ControlState.ItemAdjust)
        {
            if (pickupToHold)
            {
                desiredHold = false;
            }
            else
            {
                resetHoldTimeout = 0.2f;
            }
            ResetControl();
        }

        if (cState != ControlState.TryHold && desiredHold && !armHolds && !IsInventoryActive)
        {
            desiredHold = false;
        }

        //desiredCrouch = holdButton && !ArmHolds;

        if (zMoveTimeout > 0)
            zMoveTimeout -= Time.deltaTime * 3f;

        if (ArmCatched)
            desiredJump = false;

        float inX = Mathf.Clamp(Input.GetAxis("Horizontal"), -1, 1);
        float inY = Mathf.Clamp(Input.GetAxis("Vertical"), -1, 1);
        var speedMode = (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) ? 0.5f : (ArmCatched || desiredCrouch) ? 0.6f : 1f;

        if (ArmCatched)
        {
            desiredVelocity = new Vector2(inX, inY) * Settings.maxSpeed * speedMode;
        }
        else
        {
            desiredVelocity.x = inX * Settings.maxSpeed * speedMode;
            desiredVelocity.y = 0;
        }

        if (armHolds && cState is ControlState.TryHold or ControlState.ItemAdjust && holdTarget != Vector2.zero)
        {
            Vector2 shift;
            // udelam kolmy
            shift.y = -Input.GetAxis("Mouse X");
            shift.x = Input.GetAxis("Mouse Y");

            float amount = Vector2.Dot(holdTarget, shift);

            if (Mathf.Abs(amount) > 0.05f)
            {
                if (cState == ControlState.TryHold)
                    ResetControl();
                cState = ControlState.ItemAdjust;

                var rot = Quaternion.AngleAxis(amount * 70, Vector3.forward);
                holdTarget = rot * holdTarget;
            }
        }

        if (throwCtrl.ThrowActive)
        {
            throwCtrl.PositionLongThrowMarker(this);
        }

        if (zMoveTimeout <= 0 && cState != ControlState.ItemAnimation && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            desiredZMove = transform.position.z < 0.25f ? Map.CellSize.z : -Map.CellSize.z;
            zMoveTimeout = 1;
        }
    }

    private ControlState ResetControl()
    {
        if (inputController != null)
        {
            var throwCtrl = inputController.ThrowController;
            if (throwCtrl.ThrowActive)
                throwCtrl.SetThrowActive(false, false, this);
        }
        if (holdAnimator != null)
        {
            holdTarget = holdAnimator.Cancel();
            holdAnimator = null;
        }
        controlTimeout = 0;
        desiredPickUp = false;
        pickupToHold = false;
        firstPress = false;
        var oldState = cState;
        cState = ArmHolds ? ControlState.ItemUse : ControlState.EmptyHands;
        return oldState;
    }

    private void UpdatePosition()
    {
        inputController.GameUpdate();
    }

    private void UncontrolledUpdate()
    {
        AnimateHand();
        AdjustLegsArms(cState != ControlState.ItemAnimation);
    }

    private void AnimateHand()
    {
        if (cState == ControlState.ItemAnimation)
        {
            holdTarget = holdAnimator.Evaluate(holdTarget);
            if (holdAnimator.Completed)
                ResetControl();
        }
    }

    public void InventoryAccess(Label key)
    {
        if (inventory.TryGetSlot(key, out var slot))
            InventoryAccess(slot);
    }

    private void InventoryAccess(int quickSlot)
    {
        desiredHold = false;
        if (ArmHolds)
            RecatchHold();
        if (IsInventoryActive)
            InventoryReturn();
        Vector3 pos = holdTarget != Vector2.zero
            ? ArmSphere.transform.position + holdTarget.AddZ(0)
            : ArmSphere.transform.position + new Vector3(Settings.HoldPosition.x * lastXOrientation, Settings.HoldPosition.y, 0);
        var obj = inventory.ActivateObj(quickSlot, placeable.LevelGroup, pos, map);
        if (obj != null)
        {
            desiredHold = true;
        }
    }


    public override void GameFixedUpdate()
    {
        if (bodyToThrow == null)
            base.GameFixedUpdate();
        if (inputController)
        {
            if (inputController.ThrowController.ThrowActive)
                inputController.ThrowController.ShowThrowMarker(this, body.linearVelocity);
            if (bodyToThrow != null)
            {
                Vector2 force = inputController.ThrowController.ComputeThrowForce(bodyToThrow.mass);
                bodyToThrow.linearVelocity = (Vector3)force + this.body.linearVelocity;
                body.AddForce(-force, bodyToThrow.mass, VelocityFlags.None);
                bodyToThrow = null;
            }
        }
    }

    public override Label InventoryGet() => inventory.ActiveObj;

    public override bool IsInventoryActive => inventory.ActiveObj != null;

    public Inventory Inventory => inventory;

    public override void InventoryReturn()
    {
        inventory.ReturnActiveObj();
    }

    public override void InventoryDrop()
    {
        inventory.RemoveObjActive();
    }

    internal void ThrowObj(Label obj)
    {
        this.bodyToThrow = obj.Rigidbody;
        desiredHold = false;
        if (IsInventoryActive)
        {
            InventoryDrop();
            if (cState == ControlState.Throw && Input.GetKey(KeyCode.R))
            {
                cState = ControlState.ThrowReload;
                controlTimeout = 0;
            }                    
        }
    }

    protected override Vector3 GetPickupMousePos(float z) => inputController.GetMousePosOnZPlane(z);
    protected override bool HasMouseControler => inputController != null;

    protected override bool IsPickupAllowed(Label p) => p.KsidGet.IsChildOf(Ksid.InventoryItem);

    protected override void InventoryPickup(Label label)
    {
        if (cState == ControlState.Pickup)
            controlTimeout = 0;
        if (label.CanBeInInventory(inventory))
            inventory.Store(label);
    }

    protected override void InventoryPickupAndActivate(Label label)
    {
        Debug.Assert(!IsInventoryActive, "Cekam ze nebudu mit inventoryAccessor");
        desiredHold = true;
        inventory.StoreAsActive(label);
    }

    private const float mouseXDeadZone = 0.6f;
    protected override void AdjustXOrientation()
    {
        if (inputController)
        {
            var mouseX = inputController.GetMousePosOnZPlane(transform.position.z).x;
            if (lastXOrientation < 0 && mouseX > transform.position.x + mouseXDeadZone)
            {
                lastXOrientation = 1;
                FlipHoldTarget();
            }
            else if (lastXOrientation > 0 && mouseX < transform.position.x - mouseXDeadZone)
            {
                lastXOrientation = -1;
                FlipHoldTarget();
            }
        }
        else
        {
            base.AdjustXOrientation();
        }
    }

    private void FlipHoldTarget()
    {
        if (holdTarget != Vector2.zero && cState != ControlState.ItemAdjust && cState != ControlState.ItemAnimation)
        {
            holdTarget.x *= -1;
            resetHoldTimeout = 0.01f;
        }
    }

    internal void ActivateInput(InputController inputController)
    {
        this.inputController = inputController;
        inventory.ShowInQuickSlots();
        Game.Instance.Hud.SetupInventory(inventory);
    }

    internal void DeactivateInput()
    {
        if (inputController != null)
        {
            ResetControl();
            inputController = null;
            inventory.DisconnectQuickSlots();
        }
    }

    public override void Cleanup(bool goesToInventory)
    {
        base.Cleanup(goesToInventory);
        inventory.Kill();
        inventory = null;
    }

    public void ActivateHoldAnimation(AnimationCurve animation, float returnTime, float speed)
    {
        cState = ControlState.ItemAnimation;
        holdAnimator = HoldAnimator.Create(GetHoldLeg(), holdTarget, animation, returnTime, speed);
    }
}