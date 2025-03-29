using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Utils
{
    public class GOTags : MonoBehaviour
    {
        public static TagHandle HoldHandle;

        private void Awake()
        {
            HoldHandle = TagHandle.GetExistingTag("HoldHandle");
        }
    }
}
