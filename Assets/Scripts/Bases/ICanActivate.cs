using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
}
