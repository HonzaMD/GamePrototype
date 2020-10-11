using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core
{
    class KsidDependencies : Ksids
    {
        public KsidDependencies()
            : base(new (KsidEnum child, KsidEnum parent)[]
            {
            })
        {
        }
    }
}
