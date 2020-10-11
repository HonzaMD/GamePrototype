using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Map
{
    [Serializable]
    public struct MapSettings
    {
        public int posx;
        public int posy;
        public int sizex;
        public int sizey;

        public MapSettings(int posx, int posy, int sizex, int sizey)
        {
            this.posx = posx;
            this.posy = posy;
            this.sizex = sizex;
            this.sizey = sizey;
        }
    }
}
