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
        private readonly List<IHasCleanup> cleanups = new List<IHasCleanup>();

        private void Awake()
        {
            Configure();
        }

        public void Configure()
        {
            cleanups.Clear();
            GetComponents(cleanups);
        }

        public override void Cleanup()
        {
            base.Cleanup();
            foreach (var c in cleanups)
                c.Cleanup();
        }
    }
}
