using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Map
{
    class BallonTests : LevelBase
    {
        protected override string[] Data => new[]
        {
"HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"H                              H                                                                            P     H",
"H                              H                                                                        o   P     H",
"H                              H                                                                     HHHHH  P     H",
"H         HHHHH                H                        HH                                         HH       P     H",
"H                              H                        HH                                  HHHHHHH         P     H",
"HL                             H                 HHHHHHHHHHHHH                             P                P     H",
"HL                             H                        H                                  P                P     H",
"HL    HHHH                                b             H                   b              P                P    HH",
"HL       H                                              H                                  o                P    HH",
"HL       H  o                                           H                A           HHHHHHHHHHH            P    HH",
"HL       HHHHHHHHHH                                     H                                          HH       P    HH",
"HL                   H      HHHHHHHHHHH                                                            HH       P    HH",
"HL                  H                                                    HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH    P  HHHH",
"HL            o    H                                                         H          H              H    P     H",
"HL          HHHHHHH                                                          H          H              H    P     H",
"HL         H                                               o                 HHH    HHHHHHHHH   HHHHHHHH    P     H",
"HL                                                 H       H         b       H                              P     H",
"HL  HHHHH                                                                    H                      o       P     H",
"HL                                H                      H                   HHHHHHHHHHHH    HHHHHHHHHHH    P     H",
"HL              o                 H                  H                                H      H              P     H",
"HL         HHHHHHH                H                                                  H       H              P     H",
"HL                H               H                                                 H    HHHHH              P     H",
"HL        H                                      HH                                  H    HH                P     H",
"HL                                                                                    HHHH                  P     H",
"HH                                                                                                b         P     H",
"H                                        H                                                                  P     H",
"HHHHH                                    o                                                                       HH",
"HHHHHHHHH                            HHHHHHHHHHHH                                                              HHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
        };
    }
}
