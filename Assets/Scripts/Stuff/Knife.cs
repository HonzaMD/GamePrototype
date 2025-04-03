using Assets.Scripts.Bases;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Stuff
{
    [RequireComponent(typeof(PlaceableSibling))]
    public class Knife : MonoBehaviour, IHoldActivate
    {
        public void Activate(Character3 character)
        {
            var settings = GetComponent<PlaceableSibling>().Settings;
            character.ActivateHoldAnimation(settings.ActivityAnimation, 0.55f, 2f);
        }
    }
}
