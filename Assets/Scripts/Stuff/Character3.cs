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

[RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody), typeof(Status))]
public class Character3 : ChLegsArms, IActiveObject, IHasInventory
{
    // Jak dlouho po stisku skoku se skok jeste smi provest. Neni to zaplata na sync mezi
    // Update a FixedUpdate - je to buffer pro hrace, ktery zmackne skok tesne pred dopadem.
    private const float JumpBufferTime = 0.3f;

    public bool PendingRemove { get; set; }
    private float lastJumpTime;

    private float zMoveTimeout;
    private float controlTimeout;
    private float resetHoldTimeout;

    private Inventory inventory;
    public Status Status { get; private set; }

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
        Status = GetComponent<Status>();
    }

    public override void AfterMapPlaced(Map map, Placeable placeableSibling, bool goesFromInventory)
    {
        base.AfterMapPlaced(map, placeableSibling, goesFromInventory);
        CreateInventory();
        Game.Instance.InputController.AddCharacter(this);
        Game.Instance.ActivateObject(this);
    }

    private void CreateInventory()
    {
        inventory = Game.Instance.PrefabsStore.Inventory.Create(Game.Instance.InventoryRoot, Vector3.zero, null);
        var name = CharacterNames.GiveMeName();
        inventory.SetupIdentity(name, InventoryType.Character, placeable.Settings.Icon);
        Status.SetupIdentity(name);
        inventory.StoreProto(Game.Instance.PrefabsStore.Gravel, 5);
        inventory.SetQuickSlot(-9, Game.Instance.PrefabsStore.Gravel);
        inventory.StoreProto(Game.Instance.PrefabsStore.StickyBomb, 30);
        inventory.SetQuickSlot(-8, Game.Instance.PrefabsStore.StickyBomb);
        inventory.StoreProto(Game.Instance.PrefabsStore.PointLight, 3);
        inventory.SetQuickSlot(-7, Game.Instance.PrefabsStore.PointLight);
        inventory.StoreProto(Game.Instance.PrefabsStore.Knife, 10);
        inventory.SetQuickSlot(-6, Game.Instance.PrefabsStore.Knife);
        inventory.StoreProto(Game.Instance.PrefabsStore.JoinerGlue, 10);
        inventory.SetQuickSlot(-5, Game.Instance.PrefabsStore.JoinerGlue);
        inventory.StoreProto(Game.Instance.PrefabsStore.DeepGlue, 10);
        inventory.SetQuickSlot(-4, Game.Instance.PrefabsStore.DeepGlue);
        inventory.StoreProto(Game.Instance.PrefabsStore.DirtBuilder, 1);
        inventory.SetQuickSlot(-3, Game.Instance.PrefabsStore.DirtBuilder);
    }

    // Vstup vzorkuje InputController, prezentaci pro hrace si od postavy vola taky on
    // (UpdateHandAim, UpdateThrowMarkers). Vse ostatni cte nebo pise fyziku -> GameFixedUpdate.
    public void GameUpdate()
    {
    }

    // Prezentace hodu: mirici sipka i duch letici veci. ShowThrowMarker pocita
    // s Time.deltaTime, takze do Update patri.
    internal void UpdateThrowMarkers()
    {
        var throwCtrl = inputController.ThrowController;
        if (!throwCtrl.ThrowActive)
            return;
        throwCtrl.PositionLongThrowMarker(this);
        throwCtrl.ShowThrowMarker(this, body.linearVelocity);
    }

    // Pozadavky na inventar ze snimku vstupu: klik v UI i klavesy 0-9.
    private void ConsumeInventoryRequests(in PlayerInput input)
    {
        if (input.InventoryKey != null && inventory.TryGetSlot(input.InventoryKey, out var uiSlot))
            InventoryAccess(uiSlot);

        if (input.InventorySlot != 0 && cState != ControlState.ItemAnimation)
            InventoryAccess(input.InventorySlot);
    }

    private void ControlledFixedUpdate()
    {
        ref readonly var input = ref inputController.Frame;
        var throwCtrl = inputController.ThrowController;

        desiredCatch = input.CatchHeld;
        if (input.JumpPressed)
            lastJumpTime = input.JumpPressTime;

        ConsumeInventoryRequests(input);

        controlTimeout += Time.fixedDeltaTime;
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
            resetHoldTimeout += Time.fixedDeltaTime;
        if (resetHoldTimeout > 0.6f && !(cState is ControlState.ItemAdjust or ControlState.TryHold or ControlState.ItemAnimation))
        {
            ResetHold();
            resetHoldTimeout = 0;
        }

        if (input.PickupPressed && cState != ControlState.ItemAnimation)
        {
            if (ResetControl() != ControlState.Pickup)
                firstPress = true;
            cState = ControlState.PickupPrepare;
        }
        if (input.PickupReleased && cState == ControlState.PickupPrepare)
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
        }

        bool mouseDown = input.PrimaryPressed && cState != ControlState.ItemAnimation;
        bool mouseUp = input.PrimaryReleased;

        if (mouseDown && (cState is ControlState.PickupPrepare or ControlState.EmptyHands || (cState is ControlState.ItemUse && input.PrimaryPressedWithPickup)))
        {
            desiredPickUp = false;
            pickupToHold = true;
            desiredHold = true;
            cState = ControlState.TryHold;
        }

        if (mouseDown && cState == ControlState.Pickup)
            ResetControl();

        // Jump buffer: stisk drzi prani skocit po JumpBufferTime, at hrac trefi i skok
        // zmackly tesne pred dopadem. Razitko hrany nese snimek vstupu.
        if (ArmCatched && desiredCatch)
        {
            desiredJump = false;
        }
        else if (input.JumpPressed)
        {
            desiredJump = true;
        }
        else if (JumpBufferExpired)
        {
            desiredJump = false;
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

        if (input.ThrowPressed && cState != ControlState.ItemAnimation)
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

        if (input.ThrowReleased && !firstPress && cState != ControlState.ItemAnimation)
        {
            ResetControl();
        }

        if (mouseDown && throwCtrl.ThrowActive && cState == ControlState.Throw)
        {
            throwCtrl.SetThrowActive(false, true, this);
            if (cState != ControlState.ThrowReload)
                ResetControl();
        } 
        else if (mouseDown && cState == ControlState.ItemUse)
        {
            var ho = GetHoldObject();
            ho.TryActivateInHand(this);
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
            zMoveTimeout -= Time.fixedDeltaTime * 3f;

        if (ArmCatched)
            desiredJump = false;

        var speedMode = input.SlowHeld ? 0.5f : (ArmCatched || desiredCrouch) ? 0.6f : 1f;

        if (ArmCatched)
        {
            desiredVelocity = input.MoveAxes * Settings.maxSpeed * speedMode;
        }
        else
        {
            desiredVelocity.x = input.MoveAxes.x * Settings.maxSpeed * speedMode;
            desiredVelocity.y = 0;
        }

        if (armHolds && cState is ControlState.TryHold or ControlState.ItemAdjust && holdTarget != Vector2.zero)
        {
            float amount = Vector2.Dot(holdTarget, input.HoldAdjustShift);

            if (Mathf.Abs(amount) > 0.05f)
            {
                if (cState == ControlState.TryHold)
                    ResetControl();
                cState = ControlState.ItemAdjust;

                var rot = Quaternion.AngleAxis(amount * 70, Vector3.forward);
                holdTarget = rot * holdTarget;
            }
        }

        if (zMoveTimeout <= 0 && cState != ControlState.ItemAnimation && input.ZMoveHeld)
        {
            desiredZMove = transform.position.z < 0.25f ? Map.CellSize.z : -Map.CellSize.z;
            zMoveTimeout = 1;
        }
    }

    // Pokud drzime IHandAimer (DirtBuilder) ve stavu ItemUse, necha ho zvolit marker a vrati ho,
    // jinak null. Aktivaci/deaktivaci markeru ridi InputController ze sveho GameUpdate.
    public Transform UpdateHandAim()
    {
        if (cState == ControlState.ItemUse)
        {
            var ho = GetHoldObject();
            if (ho != null && ho.KsidGet.IsChildOf(Ksid.AimsInHand) && ho.TryGetComponent(out IHandAimer aimer))
                return aimer.UpdateAim(this);
        }
        return null;
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

    private bool JumpBufferExpired => lastJumpTime + JumpBufferTime < Time.time;

    private void UncontrolledFixedUpdate()
    {
        // Buffer musi vyprset i bez hrace. Jinak by postava opustena ve vzduchu drzela
        // prani skoku az do dopadu (klidne sekundy) a do te doby nemela podporu nohou.
        if (JumpBufferExpired)
            desiredJump = false;

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

    private void InventoryAccess(int quickSlot)
    {
        desiredHold = false;
        if (ArmHolds)
            ResetHold();
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


    // Poradi je nosne: stavovy automat muze pres ThrowObj pustit ruku, takze uz na ni
    // ApplyHoldForces v base.GameFixedUpdate nesahne a hozena vec dostane plny impuls.
    public override void GameFixedUpdate()
    {
        if (!inputController)
            UncontrolledFixedUpdate();
        else
            ControlledFixedUpdate();

        base.GameFixedUpdate();
        ApplyThrow();
    }

    private void ApplyThrow()
    {
        if (bodyToThrow == null)
            return;
        if (inputController)
        {
            Vector2 force = inputController.ThrowController.ComputeThrowForce(bodyToThrow.mass);
            bodyToThrow.linearVelocity = (Vector3)force + this.body.linearVelocity;
            body.AddForce(-force, bodyToThrow.mass, VelocityFlags.None);
        }
        bodyToThrow = null;
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
            if (cState == ControlState.Throw && inputController && inputController.Frame.ThrowHeld)
            {
                cState = ControlState.ThrowReload;
                controlTimeout = 0;
            }
        }
        // Az po InventoryDrop: OnLimbDetached vraci vec do inventare, kdyz je jeste aktivni.
        DetachHold();
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
            // Opustena postava zastavi, ale desiredCatch drzi dal - visi-li na zdi, nespadne.
            desiredVelocity = Vector2.zero;
            inputController = null;
            inventory.DisconnectQuickSlots();
        }
    }

    public override void Cleanup(bool goesToInventory)
    {
        base.Cleanup(goesToInventory);
        Game.Instance.DeactivateObject(this);
        Game.Instance.InputController.RemoveCharacter(this);
        inventory.Kill();
        inventory = null;
    }

    public void ActivateHoldAnimation(AnimationCurve animation, float returnTime, float speed)
    {
        cState = ControlState.ItemAnimation;
        holdAnimator = HoldAnimator.Create(GetHoldLimb(), holdTarget, animation, returnTime, speed);
    }
}