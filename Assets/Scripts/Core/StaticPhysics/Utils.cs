using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Core.StaticPhysics
{
    internal static class Utils
    {
        public static bool IsDistanceBetter(float candidate, float other, int candidateRoot, int otherRoot) 
            => candidateRoot != 0 && (otherRoot == 0 || candidate < other || (candidate == other && candidateRoot < otherRoot));
    }
}
