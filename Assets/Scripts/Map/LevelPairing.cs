using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Map
{
    public enum LevelName
    {
        Level1, 
        Level2, 
        Level3, 
        Level4,
        LevelEmpty,
        LevelSLT,
        
        DebugLvl = 100,
    }

    internal static class LevelPairing
    {
        private static readonly LevelBase[] levels = new LevelBase[]
        {
            new Level1(),
            new Level2(),
            new Level3(),
            new Level4(),
            new LevelEmpty(),
            new LevelSLT(),
        };

        public static LevelBase Get(LevelName name) => levels[(int)name];
    }
}
