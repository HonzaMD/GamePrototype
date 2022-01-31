using Assets.Scripts.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Bases
{
    public interface IHasCleanup
    {
        void Cleanup();
    }

    public class PlaceableSibling : Placeable
    {
        public override void Cleanup()
        {
            base.Cleanup();
            var cleanups = ListPool<IHasCleanup>.Rent();
            GetComponents(cleanups);
            foreach (var c in cleanups)
                c.Cleanup();
            cleanups.Return();
        }
    }
}
