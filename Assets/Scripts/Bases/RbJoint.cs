using Assets.Scripts.Core;
using Assets.Scripts.Core.StaticPhysics;
using Assets.Scripts.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.MaterialProperty;

namespace Assets.Scripts.Bases
{
    public class RbJoint : ConnectableLabel
    {
        [Flags]
        public enum State : byte
        {
            None = 0,
            RbConnection = 1,
            OwnsJoint = 2,
            SpConnection = 4,
        }


        public Joint Joint;
        public Placeable MyObj;
        public Placeable OtherObj;
        public RbJoint OtherConnectable;
        public State state;

        public bool IsConnected => (state & State.SpConnection) != 0 || Joint;

        public override ConnectableType Type => ConnectableType.Physics;

        internal void Setup(Placeable myObj, Placeable otherObj, RbJoint otherJ)
        {
            MyObj = myObj;
            OtherObj = otherObj;
            OtherConnectable = otherJ;
        }


        private void SetState(State value)
        {
            state = value;
            OtherConnectable.state = value;
            ActiveDebugLine();
            OtherConnectable.ActiveDebugLine();
        }

        public override void Disconnect() => Disconnect(true);

        public void Disconnect(bool trueKill)
        {
            if (Joint && (state & State.OwnsJoint) != 0)
                Destroy(Joint);
            if ((state & State.SpConnection) != 0 && MyObj.SpNodeIndex != 0 && OtherObj.SpNodeIndex != 0)
                Game.Instance.StaticPhysics.RemoveJoint(MyObj.SpNodeIndex, OtherObj.SpNodeIndex);

            OtherConnectable.Kill(trueKill);
            Kill(trueKill);
        }

        private void Kill(bool trueKill)
        {
            if ((state & State.RbConnection) != 0)
                MyObj.DetachRigidBody(false, true);
            
            DeactivateDebugLine();

            state = State.None;
            Joint = null;

            if (trueKill)
            {
                MyObj = null;
                OtherObj = null;
                OtherConnectable = null;
                Game.Instance.ConnectablePool.Store(this, Game.Instance.PrefabsStore.RbJoint);
            }
        }


        internal void SetupRb(Joint j, bool ownsJoint)
        {
            SetupRb1(ownsJoint);
            SetupRb2(j);
        }

        internal void SetupRb()
        {
            SetupRb1(true);
            var j = MyObj.Rigidbody.gameObject.AddComponent<FixedJoint>();
            var limits1 = MyObj.SpLimits;
            var limits2 = OtherObj.SpLimits;
            j.breakForce = MathF.Min(
                Math.Min(limits1.CompressLimit, limits2.CompressLimit),
                Math.Min(limits1.StretchLimit, limits2.StretchLimit)
                ) * PhysicsConsts.SpToRbLimitsMultiplier;
            j.breakTorque = Math.Min(limits1.MomentLimit, limits2.MomentLimit) * PhysicsConsts.SpToRbLimitsMultiplier;
            j.connectedBody = OtherObj.Rigidbody;
            SetupRb2(j);
        }


        internal void SetupRb1(bool ownsJoint)
        {
            if (state != State.None)
                throw new InvalidOperationException("Necekal jsem pripojeny Joint");
            SetState(ownsJoint ? State.RbConnection | State.OwnsJoint : State.RbConnection);
            MyObj.AttachRigidBody(false, true);
            OtherObj.AttachRigidBody(false, true);
        }

        internal void SetupRb2(Joint j)
        { 
            Joint = j;
            OtherConnectable.Joint = j;
        }


        internal void SetupSp()
        {
            if (MyObj.SpNodeIndex == 0 && OtherObj.SpNodeIndex != 0)
            {
                OtherConnectable.SetupSp();
            }
            else if ((state & State.SpConnection) == 0)
            {
                Disconnect(false);

                AddNode1();
                AddNodeAndEdge();
                SetState(State.SpConnection);
            }
        }

        private void AddNode1()
        {
            if (MyObj.SpNodeIndex == 0)
            {
                var cmd = new InputCommand()
                {
                    Command = SpCommand.AddNode,
                };
                MyObj.AddSpNode(ref cmd);

                Game.Instance.StaticPhysics.AddInCommand(in cmd);
            }
        }

        private void AddNodeAndEdge()
        {
            var cmd = new InputCommand();
            if (OtherObj.SpNodeIndex == 0)
            {
                cmd.Command = SpCommand.AddNodeAndJoint;
                OtherObj.AddSpNode(ref cmd);
            }
            else
            {
                cmd.indexA = OtherObj.SpNodeIndex;
                cmd.Command = SpCommand.AddJoint;
            }

            var limits1 = MyObj.SpLimits;
            var limits2 = OtherObj.SpLimits;
            cmd.stretchLimit = Math.Min(limits1.StretchLimit, limits2.StretchLimit);
            cmd.compressLimit = Math.Min(limits1.CompressLimit, limits2.CompressLimit);
            cmd.momentLimit = Math.Min(limits1.MomentLimit, limits2.MomentLimit);
            cmd.indexB = MyObj.SpNodeIndex;

            Game.Instance.StaticPhysics.AddInCommand(in cmd);
        }

        internal void ClearSp() => SetState(state & ~State.SpConnection);

        private void ActiveDebugLine()
        {
            var line = GetComponent<LineRenderer>();
            line.enabled = true;
            var p1 = transform.InverseTransformPoint(MyObj.Center.AddZ(-0.6f));
            var p2 = transform.InverseTransformPoint(OtherObj.Center.AddZ(-0.6f));
            line.SetPosition(0, p1);
            line.SetPosition(1, p2);
            line.startColor = state == State.SpConnection ? Color.red : Color.green;
        }

        private void DeactivateDebugLine() => GetComponent<LineRenderer>().enabled = false;
    }
}
