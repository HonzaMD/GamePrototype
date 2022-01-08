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
}
