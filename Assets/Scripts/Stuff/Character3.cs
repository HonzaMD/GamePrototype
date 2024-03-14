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
using UnityTemplateProjects;

[RequireComponent(typeof(PlaceableSibling), typeof(Rigidbody))]
public class Character3 : ChLegsArms, IActiveObject, IInventoryAccessor, ILevelPlaceabe
{
    [HideInInspector]
    public SimpleCameraController Camera { get; set; }

    private Vector3 oldPos;
    private float lastJumpTime;

    private float zMoveTimeout;
    private float holdRotationAngle = 0;

    private Inventory inventory;

    private InputController inputController;
    private Rigidbody bodyToThrow;
    private bool desiredPickupAndHold;

    void Awake()
    {
        oldPos = transform.position;
        AwakeB();
    }

    public override void AfterMapPlaced(Map map)
    {
        base.AfterMapPlaced(map);
        CreateInventory();
    }

    private void CreateInventory()
    {
        inventory = Game.Instance.PrefabsStore.Inventory.Create(Game.Instance.InventoryRoot, Vector3.zero, null);
        inventory.StoreProto(Game.Instance.PrefabsStore.Gravel, 5);
        inventory.SetQuickSlot(-9, Game.Instance.PrefabsStore.Gravel);
        inventory.StoreProto(Game.Instance.PrefabsStore.StickyBomb, 3);
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

        var delta = transform.position - oldPos;
        delta.z = 0;
        oldPos = transform.position;
        Camera.SetTransform(delta);
        inputController.GameUpdate();

        bool jumpButton = Input.GetButtonDown("Jump");
        desiredCatch = Input.GetMouseButton(1);

        var slot = KeysToInventory.TestKeys();
        if (slot != 0)
            InventoryAccess(slot);

        if (Input.GetKeyDown(KeyCode.E))
        {
            if (throwCtrl.ThrowActive)
                throwCtrl.SetThrowActive(false, false, this);

            desiredPickUp = true;
        }
        if (Input.GetKeyUp(KeyCode.E) && !desiredPickupAndHold)
        {
            desiredPickUp = false;
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

        bool mouseDown = Input.GetMouseButtonDown(0);
        bool mouseUp = Input.GetMouseButtonUp(0);

        if (mouseDown && desiredPickUp)
        {
            desiredHold = false;
            if (ArmHolds)
                RecatchHold();
            desiredHold = true;
            desiredPickupAndHold = true;
        }
        if (mouseUp && desiredPickupAndHold)
        {
            desiredPickupAndHold = false;
            if (!Input.GetKey(KeyCode.E))
                desiredPickUp = false;
        }


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

        AdjustLegsArms();

        throwCtrl.SetThrowActive(((Input.GetKeyDown(KeyCode.R) || (mouseDown && throwCtrl.ThrowActive)) ^ throwCtrl.ThrowActive) && ArmHolds, mouseDown, this);

        if (throwCtrl.ThrowActive)
        {
            desiredPickUp = false;
            desiredPickupAndHold = false;
        }

        if (!desiredPickupAndHold && desiredHold && !ArmHolds && inventoryAccessor == null)
        {
            holdTarget = Vector2.zero;
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

        //if (holdButton && ArmHolds && Mathf.Abs(inY) > 0.2f)
        //{
        //    if (holdRotationAngle == 0)
        //    {
        //        holdRotationAngle = holdTarget.x > 0 ? 340 : -340;
        //    }
        //    var rot = Quaternion.AngleAxis(inY * holdRotationAngle * Time.deltaTime, Vector3.forward);
        //    holdTarget = rot * holdTarget;
        //    dropHold = false;
        //}

        if (throwCtrl.ThrowActive)
        {
            throwCtrl.PositionLongThrowMarker(this);
        }

        if (zMoveTimeout <= 0 && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
        {
            desiredZMove = transform.position.z < 0.25f ? Map.CellSize.z : -Map.CellSize.z;
            zMoveTimeout = 1;
        }
    }


    private void UncontrolledUpdate()
    {
        AdjustLegsArms();
    }

    private void InventoryAccess(int quickSlot)
    {
        desiredHold = false;
        if (ArmHolds)
            RecatchHold();
        Vector3 pos = holdTarget != Vector2.zero
            ? ArmSphere.transform.position + holdTarget.AddZ(0)
            : ArmSphere.transform.position + new Vector3(Settings.HoldPosition.x * lastXOrientation, Settings.HoldPosition.y, 0);
        var obj = inventory.ActivateObj(quickSlot, placeable.LevelGroup, pos, map);
        if (obj != null)
        {
            desiredHold = true;
            inventoryAccessor = this;
        }
    }


    public override void GameFixedUpdate()
    {
        if (bodyToThrow == null)
            base.GameFixedUpdate();
        if (inputController)
        {
            if (inputController.ThrowController.ThrowActive)
                inputController.ThrowController.ShowThrowMarker(this, body.velocity);
            if (bodyToThrow != null)
            {
                Vector2 force = inputController.ThrowController.ComputeThrowForce(bodyToThrow.mass);
                bodyToThrow.velocity = (Vector3)force + this.body.velocity;
                body.AddForce(-force, bodyToThrow.mass, VelocityFlags.None);
                bodyToThrow = null;
            }
        }
    }

    Label IInventoryAccessor.InventoryGet() => inventory.ActiveObj;

    void IInventoryAccessor.InventoryReturn(Label label)
    {
        inventoryAccessor = null;
        if (label.CanBeInInventory())
        {
            inventory.DeactivateObj(label);
        }
        else
        {
            inventory.DropObjActive();
        }
    }

    void IInventoryAccessor.InventoryDrop(Label label)
    {
        inventoryAccessor = null;
        inventory.DropObjActive();
    }

    void ILevelPlaceabe.Instantiate(Map map, Transform parent, Vector3 pos)
    {
        var placeable = GetComponent<Placeable>();
        if (!placeable.IsMapPlaced)
        {
            transform.parent = parent;
            placeable.LevelPlaceAfterInstanciate(map, pos);
        }
    }

    internal void ThrowObj(Label obj)
    {
        this.bodyToThrow = obj.Rigidbody;
        holdTarget = Vector2.zero;
        desiredHold = false;
        if (inventoryAccessor != null)
            inventoryAccessor.InventoryDrop(obj);
    }

    protected override Vector3 GetPickupMousePos(float z) => inputController.GetMousePosOnZPlane(z);

    protected override bool IsPickupAllowed(Label p) => p.KsidGet.IsChildOf(Ksid.InventoryItem);

    protected override void InventoryPickup(Label label)
    {
        if (label.CanBeInInventory())
            inventory.Store(label);
    }

    protected override void InventoryPickupAndActivate(Label label)
    {
        Debug.Assert(inventoryAccessor == null, "Cekam ze nebudu mit inventoryAccessor");
        desiredHold = true;
        inventoryAccessor = this;
        inventory.StoreAsActive(label);
    }

    internal void ActivateInput(InputController inputController)
    {
        this.inputController = inputController;
    }

    bool ILevelPlaceabe.SecondPhase => false;

    public override void Cleanup()
    {
        base.Cleanup();
        inventory.Kill();
        inventory = null;
    }
}