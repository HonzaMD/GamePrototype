using Assets.Scripts.Bases;
using Assets.Scripts.Core;
using Assets.Scripts.Core.Inventory;
using Assets.Scripts.Map;
using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Serialization;

public abstract partial class ChLegsArms : MonoBehaviour, IHasCleanup, IHasAfterMapPlaced
{
    private const float Free = 0;
    private const float Timeout = 1;
    private const float Catch = 2;
    private const float Hold = 3;
    private const float PickUp = 4;

    public ChSettings Settings;

    // Poradi je nosne: 0,1 = nohy, 2,3 = ruce.
    [FormerlySerializedAs("Legs")]
    public Transform[] Limbs;
    private float[] limbStatus = new float[4];
    private Label[] limbTargets = new Label[4];
    private Connectable[] limbConnectors = new Connectable[4];

    protected bool LegOnGround => limbStatus[0] == Catch || limbStatus[1] == Catch;
    protected bool ArmCatched => limbStatus[2] == Catch || limbStatus[3] == Catch;
    protected bool ArmHolds => limbStatus[2] == Hold || limbStatus[3] == Hold;

    private Vector3 legUpDir = Vector3.up;

    public SphereCollider LegSphere;
    public SphereCollider ArmSphere;

    protected Rigidbody body;
    protected Placeable placeable;
    protected Map map;

    protected Vector2 desiredVelocity;
    protected int lastXOrientation = 1;
    protected bool desiredJump;
    protected float desiredZMove;
    protected bool desiredCatch;
    protected bool desiredHold;
    protected bool desiredPickUp;
    protected bool pickupToHold;
    protected bool desiredCrouch;
    protected Vector2 holdTarget;


    private static List<Vector2> armCandidates = new List<Vector2>();
    private static List<Placeable> placeables = new List<Placeable>();

    private Label pendingCollisionRestore;
    private readonly Action<object, int> OnCollisionRestoreTimerA;

    public ChLegsArms()
    {
        OnCollisionRestoreTimerA = OnCollisionRestoreTimer;
    }

    protected void AwakeB()
    {
        body = GetComponent<Rigidbody>();
        placeable = GetComponent<Placeable>();
        Settings.Initialize(ArmSphere, Limbs);
        InitConnectables();
    }

    private void InitConnectables()
    {
        for (int f = 0; f < Limbs.Length; f++)
        {
            int index = f;
            limbConnectors[f] = Limbs[f].GetComponent<Connectable>();
            limbConnectors[f].Init(() => OnLimbDetached(index));
        }
    }

    protected void AdjustLegsArms(bool allowHoldDrop)
    {
        map.Move(placeable);
        TickLimbTimers();
        DetachUnwantedLimbs(allowHoldDrop);
        TryCatchWithFreeLeg();
        TryCatchWithFreeArm();
        TryHoldOrPickUp();
        DetachCatchDuplicatingHold();
    }

    protected virtual void InventoryPickup(Label label) { }
    protected virtual void InventoryPickupAndActivate(Label label) { }

    protected virtual Vector3 GetPickupMousePos(float z) => throw new NotSupportedException();
    protected virtual bool IsPickupAllowed(Label p) => false;
    protected virtual bool HasMouseControler => false;


    public virtual void GameFixedUpdate()
    {
        AdjustXOrientation();

        Vector2 groundVelocity = GetGroundVelocity();

        ApplyMoveForce(groundVelocity);              // pise legUpDir -> musi byt pred podporou nohou
        ApplyArmsCatchForce(groundVelocity);

        bool jumpStarted = ApplyJumpOrLegSupport();  // POZOR: pri odrazu vola DetachAllLegs()
        if (!jumpStarted)
            ApplyDrag();

        ApplyZMove();                                // POZOR: teleportuje transform, prepoji koncetiny
        ApplyHoldForces();
        ApplyLimbsContactDamage();
    }

    public virtual void Cleanup(bool goesToInventory)
    {
        Debug.Assert(!goesToInventory, "Nepodporuju imistovani do inventare");
        DetachAllLimbs();
        map = null;
    }

    public virtual void AfterMapPlaced(Map map, Placeable placeableSibling, bool goesFromInventory)
    {
        this.map = map;
    }


    public virtual Label InventoryGet() => null;
    public virtual bool IsInventoryActive => false;
    public virtual void InventoryReturn() => throw new NotSupportedException();
    public virtual void InventoryDrop() => throw new NotSupportedException();

    public Map ActiveMap => map;
}
