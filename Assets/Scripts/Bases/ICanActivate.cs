using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Assets.Scripts.Bases
{
    interface ICanActivate
    {
        void Activate();
    }

    interface IHoldActivate
    {
        void Activate(Character3 character);
    }

    // Drzeny predmet, ktery kazdy frame mire kurzorem (napr. DirtBuilder).
    // Vrati marker (z InputControlleru), ktery ma byt aktivni, nebo null kdyz se nemiri.
    interface IHandAimer
    {
        Transform UpdateAim(Character3 character);
    }
}
