using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{ 
    internal struct ActivityTag4
    {
        public const int Mask = 3;
        public const int BigIncrement = 4;

        public int Tag;
        public int State => Tag & Mask;
        public void Reset() => Tag = (Tag & ~Mask) + BigIncrement;
        public void SetState(int state) => Tag = (Tag & ~Mask) + BigIncrement + state;
        public void Increment() => Tag++;
        public bool IsActive => (Tag & Mask) != 0;
    }

    internal struct ActivityTag8
    {
        public const int Mask = 7;
        public const int BigIncrement = 8;

        public int Tag;
        public int State => Tag & Mask;
        public void Reset() => Tag = (Tag & ~Mask) + BigIncrement;
        public void SetState(int state) => Tag = (Tag & ~Mask) + BigIncrement + state;
        public void Increment() => Tag++;
        public bool IsActive => (Tag & Mask) != 0;
    }
}
