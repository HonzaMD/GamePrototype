using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    public enum ConnectableType
    {
        Off,
        Physics,
        LegArm,
        MassTransfer,
        StickyBomb,
        OwnedByInventory,
    }

    public interface IConnectable
    {
        public void Disconnect();
        public ConnectableType Type { get; }
    }

    public interface IConnector
    {
        public void Disconnect(Label label);
    }

    public class Connectable : MonoBehaviour, IConnectable
    {
        private Func<Transform> disconnect;
        public ConnectableType Type { get; private set; }


        public void Init(Func<Transform> disconnect)
        {
            this.disconnect = disconnect;
        }

        public void Disconnect()
        {
            if (Type != ConnectableType.Off)
            {
                Type = ConnectableType.Off;
                var storage = disconnect();
                gameObject.SetActive(false);
                transform.parent = storage;
                transform.localPosition = Vector3.zero;
            }
        }

        public void ConnectTo(Label target, ConnectableType type, bool worldPositionStays = true)
        {
            Type = type;
            transform.SetParent(target.ParentForConnections, worldPositionStays);
            gameObject.SetActive(true);
        }
    }

    public abstract class ConnectableLabel : MonoBehaviour, IConnectable
    {
        public abstract ConnectableType Type { get; }
        public abstract void Disconnect();
    }
}
