using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Map
{
    class WallFollower : LevelBase
    {
        protected override string[] Data => new[]
        {
"                                                                                                             HHHHH",
"H                                                                                                           P     ",
"H                                                                                                       o   P     ",
"H                                                                                                    HHHHH  P     ",
"H         HHHHH                                                                                    HH       P     ",
"H                                                                                           HHHHHHH         P     ",
"HL                                                                                         P                P     ",
"HL                                                                                         P                P     ",
"HL    HHHH                                                                                 P                P    H",
"HL       H                                                                                 o                P    H",
"HL       H  o                                                            A           HHHHHHHHHHH            P    H",
"HL       HHHHHHHHHH                                                                                HH       P    H",
"HL                   H                                                                             HH       P    H",
"HL                  H                                                    HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH    P  HHH",
"HL            o    H                                                         H          H              H    P     ",
"HL          HHHHHHH                                                          H          H              H    P     ",
"HL         H                                               o                 HHH    HHHHHHHHH   HHHHHHHH    P     ",
"HL                                                 H       H                 H                              P     ",
"HL  HHHHH                                                                    H                      o       P     ",
"HL                                                       H                   HHHHHHHHHHHH    HHHHHHHHHHH    P     ",
"HL              o                                    H                                H      H              P     ",
"HL         HHHHHHH                                                                   H       H              P     ",
"HL                H                                                                 H    HHHHH              P     ",
"HL        H                                      HH                                  H    HH                P     ",
"HL                                                                                    HHHH                  P     ",
"HH                                                                                                          P     ",
"H                                        H                                                                  P     ",
"HHHHH                                    o                                                                       H",
"HHHHHHHHH                            HHHHHHHHHHHH                                                              HHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH ",
        };
    }
}
