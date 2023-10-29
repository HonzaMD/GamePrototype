using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    public interface IConnectable
    {
        public void Disconnect();
    }

    public interface IConnector
    {
        public void Disconnect(Label label);
    }

    public class Connectable : MonoBehaviour, IConnectable
    {
        private Action disconnect;

        public void Init(Action disconnect)
        {
            this.disconnect = disconnect;
        }

        public void Disconnect()
        {
            disconnect?.Invoke();
        }
    }

    public abstract class ConnectableLabel : MonoBehaviour, IConnectable
    {
        public abstract void Disconnect();
    }
}
