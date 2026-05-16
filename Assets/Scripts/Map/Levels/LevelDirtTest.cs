using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Map
{
    class LevelDirtTest : LevelBase
    {
        protected override string[] Data => new[]
        {
"                                                                                                             HHHHH",
"H                                                                                                           P     ",
"H                                                                                                           P     ",
"H                                                                                                           P     ",
"H         HHHHH                                                                                             P     ",
"H                                                                                                           P     ",
"HL                                                                                                          P     ",
"HL                                                                                                          P     ",
"HL    HHHH                                                                               E                  P    H",
"HL       H                                                                              DD                  P    H",
"HL       H                                                               A              DD                  P    H",
"HL       HHHHHHHHHH                                                               H    DDDDD                P    H",
"HL                                                                       DDDDDDDDDDDDDDDDDDD                P    H",
"HL                                                                       DDDHHHDDDDDDDDDDDDHHHHHHHHHHHHH    P  HHH",
"HL                                                                                         RRRRRRRRRRRRH    P     ",
"HL          HHHHHHH                                                                                         P     ",
"HL                                                                                                          P     ",
"HL                                                                                                     H    P     ",
"HL  HHHHH                                                                                              H    P     ",
"HL                                                                                           HHHHHHHHHHH    P     ",
"HL                                                              DDDD                                        P     ",
"HL         HHHHHHH                                            DDDDD                                         P     ",
"HL                H                                         DDDD                                            P     ",
"HL        H                                              DDDDDD           D                                 P     ",
"HL                                                     DDDDD              DD                                P     ",
"HH                                                     DDD                 DDD                              P     ",
"H                                                     DDDD               DDDDD                              P     ",
"HHHHH                                                DDDDD                 DDD                                   H",
"HHHHHHHHH                            HHHHHHHHHHHH    DDDDDDDDDDDDDDDDDDDDDDDDDD                                HHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH ",
        };
    }
}
