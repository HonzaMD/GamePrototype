﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Map
{
    class LevelSLT : LevelBase
    {
        // 144 x 144
        protected override string[] Data => new[]
        { // line 13
"                                                                                              s                                        HHHHHHHHH",
"                                                                      L        HHHHHHHHHHHHHHHHHHHHHHHHH    HHHHHHHHHH   HHHHHHHHHHHO  HHHHHHHHH",
"                 HHH                    HHHH       m       s  HHHHHHHHL   HHHHH                        P                 H          O           ",
"                   HHHH              HHHH   HHHHHHHHHHHHHHHHHH        L                                 PPPPPPPPPPPPPPPPP           O           ",
"                            H        H                                L                                                             O  H       H",
"                            H        H                                L                                                                H    m  H",
"                     HHHHHHHHH      HH                                L                                         s                      HHHHHHHHH",
"                             RRRRRRRR                                 L                         HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH       HHH   ",
"                                                                      L                        P                                          HHH   ",
"                                       L                              L                        P                                          HHH   ",
"                               HHHHHHHH L                             L                        P     HHHHHHHHHHHHHHHHHHHHHHHHHHHHHH     HHHHHH  ",
"                                         LL                           L                        P                                        HHHHHH  ",
"                                           LLLLL                      L     m                  P                                                ",
"                                                L       L   HHHHHHHHHHHHHHHHHHHHHHHHHHHH       P                                                ",
"                                         HHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH                                            ",
"                                      HHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH                          HHH          ",
"                                     HHHHHHHHHHHHHHHH   L                               HHHHHHHHHHHHHHHHHHHHHHHHH                               ",
"                                    HHHH                L                                 HHHHHHHHHHHHHHHHHHHHHHHHHHH                           ",
"                                  HHHHH                 L                                   HHHHHHHHHHHHHHHHHHHHHHHHHH         HHHH             ",
"                                                        L                                       HHHHHHHHHHHHHHHHHHHHHH                          ",
"                                                        L  s                                     HHHHHHHHHHHHHHHHHHHHHH              H          ",
"                                HHHHHHHHHHHHHHHHHHHHHHHHHHHHH     HHHHHHHHH                          HHHHHHHHHHHHHHH                H           ",
"                                                                                                          HHHHHHHHHH               H            ",
"                                                                            HHHHHHH         HHHHHH         HHHHHHHHHH   HHHHHHHHHHH             ",
"                                HHHHHHHH                         HHHH                                     HHHHHHHHHHH  P                        ",
"                                HHHHHHHH                                                                  HHHHHHHHHHH  P                        ",
"                               HHHHHHHHH                                                                   HHHHHHHHHHH P                        ",
"                                                 RH          H                 L       s                   HHHHHHHHH   P                        ",
"                                                 RHHHHHHHHHHHHH            LLLLHHHHHHHHHHHHHHHHHHH     HHHHHHHHHHH     P                        ",
"                               HHHHHHHHH         R      H     RRRRRR     LL                                  HHHHHHHH  P                        ",
"                                HHHHHHHHHH       R      H           RHHHH                                HHHHHHHH      P                        ",
"                                HHHHHHHH                H                                                              P                        ",
"                                HHHHHHHH        s       H                                HHHHHHHHH                     P                        ",
"                               HHHHHHHHHHHHHHHHHHHHHHHHHH            s                                  HHHHHHHHHHHHHO P                        ",
"                              P HHHHHHHHHHHHHHHHHHHHHHHHHH         HHHH         HHHHH                    HHHHHHHHHH  O P                        ",
"                              P HHHHHHHHHHHHHHHHHHHHHHHHHH                                              HHHHHHHHHHH  O                          ",
"                              P  HHHHHHHHHHHHHHHHHHHHHHHHHHH                                          HHHHHHHHHHHHH  O                          ",
"                              P     HHHHHHHHHHHHHHHHHHHHHHHHHHHHH        m       H          HHHHHHHHHHHHHHHHHHHHHHH  O                          ",
"                              P      HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH  RHHHHHHHHHHHHHHHHHHHHHHHHHH   O                          ",
"                              P  RRRRRRRHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH  RHHHHHHHHHHHHHHHHHHHHHHHH     O                          ",
"                              P          HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH  RHHHHHHHHHHHHHHHHHHHHHO       O                          ",
"                              P     H         HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH  RHHHHHHHHHHHHHHHO     O       O                          ",
"                                    RRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRRR  R         P     O     O       O                          ",
"                                                                                       R         P     O     O       O                          ",
"                                                                                       RH        P     O     O       O                          ",
"                                                                                     HHHH        P     O     O       O                          ",
"                                                                                                 P     O     O                                  ",
"                                                                                                 P     O     O                                  ",
"                                                                                HHHH             P     O     O                                  ",
"                                                                                                                                                ",
"                               HHHHHH                                HHHHHHH            HHHHHHHHHH                                              ",
"                                                                                                                                                ",
"                                                          HHHHHHHH                                                                              ",
"                                         s                                                                                                      ",
"                                       HHHHHH     HHHHH                                            HHHHHH                                       ",
"                                                                                                                                                ",
"                          HHHHHHH                                                                                                               ",
"                                                                                       HHHHH             HHHHHH                                 ",
"                                                                                                                                                ",
"               HHHHHH                                                                                                                           ",
"                                                                                 HHH                                                            ",
"                                                                                                                                                ",
"                                                                           HH                                  HHHHH         HHHHH              ",
"                                                                                                                                                ",
"             HH                                               HHHHHH                                                                            ",
"     HHHHH  RHHHHHHHHHHHH                                                                          HHHHHHH                                      ",
"            R                                                                                                        HHHHHHHH                   ",
"            R                                                        HHH                                                                        ",
"            R                                                                            HHHHHHHH                             s                 ",
"            R                                                                                                                HHHHHH             ",
"            RH                                                                                                                                  ",
"            RH                                                                                                                                  ",
"            RH                                                                    HHHHHH                                                        ",
"            RH                                                                                                                     HHHHHH       ",
"            RH                                                                                                                                  ",
"            RH                           HHHH    HHHHHHHHH          A                                                                           ",
"            RH                                            HH                                                                                    ",
"     s      RH                                              HHHHHHHHHHH       HHHH       HHHH                                      sss          ",
"HHHHHHHHHHHHHHHH                                                                                                           RHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHH                                                                                                          R HHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHH                                                              H      s      H                           RR  HHHHHHHHHHHHHHHHHHHH",
"                                                                               HHHHHHHHHHHHH                           R                        ",
"                                                                                                                   RRRR                         ",
"                                                  L                                                               R                             ",
"        s    s           L   HHHHHHHHHHHHHHHHHHHHHL   HHH     m            HHH        s                          RHHHHHHHHHH                    ",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH    HHHHHHH   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH    HHHHHHH      HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH    HHHHHHH      HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH              HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH              HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH      HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH           HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH            HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHL   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH          HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL           *            L                                 *               HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHHHHL                        L             *                                HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHHHH  L                        L                                           HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHH    L                        L                 s      L          ss     sHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHH         L HHHHHHHHHHHHHHHHHHHO  HHHHHHHHHO    HHHHHHHHHHHHL  HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHH             HHHHHHHHHHHHHHHH      O           O                L                            HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"HHHHHH                     HHHHHHHHHHHHH      O      *    O    *           L *                    *                        HHHHHHHHHHHHHHHHHHHHH",
"               HHHHHH                         O           O                L                                                                    ",
"           HHHHHHHHHHHHH                      O           O                L                                                                    ",
"HHHHH         HHHHHHHH                 H        m                          L              m                    H           HHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHH        HHHH       HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH     HHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHs               HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH                                        HHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHH           HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH                     *                           HHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHH                    HHHHHHHHHHHHHHHHHHHHHHHHHHHHHH                                                      HHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHs          *             HHHHHH                                                             ss        s HHHHHHHHHHHHHHHHHHHHHH",
"HHHHHHHHHHHHHHHHHHHHH                                                                              HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH",
"   HHHHHHHHHHHHHHHHHH                              HHHHHHHHHHHHH                                HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH                 ",
"      HHHHHHHHHHHHHHHHHH      s          HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH                 HHHHHHHHHHHHHHHHHHHHHHHH                               ",
"           HHHHHHHHHHHHHHHHHHHHHHHHO                 HHHHHHHHHHHHHHHHHHHHHH           HHHHHHHHHHHHHHHHHHHHHHHHH                                 ",
"               HHHHHHHHHHHHHHHHHHH O                     HHHHHHHHHHHHHHH           HHHHHHHHHHHHHHHHHHHHHHHHHHH                                  ",
"                  HHHHHHHHHHHHHHHH O                       HHHHHHHHHH                  HHHHHHHHHHHHHHHHH                                        ",
"                    HHHHHHHHHHHHHHHO                         HHHHHH                              HHH                                            ",
"                      HHHHHHHHHHH  O                          HHHHHHHHH                           H                                             ",
"                        HHHHHHHH   O                            HHHHHHHH                   L      H                                             ",
"                        HHHHHHHH   O                           R   HHHHHHHHHHHHH    RHHHHHHL   HHH                                              ",
"                      HHHHHHHHHHHL O                         RR                     R      L                                                    ",
"                        HHHHHH    LL                  RRRRRRR                       R      L                                                    ",
"                                    LLL        HH   RR                              R      L                                                    ",
"                                       LLL      RRRR                                R      L                                                    ",
"                                         HH H                                       R      L                                                    ",
"                                                                                    R      L                                                    ",
"                                     H         s      H                             R      L                                                    ",
"                                     HHHHHHHHHHHHHHHHHH                             R      L                                                    ",
"                                                                                    R      L                                                    ",
"                                                                                    RHHHHHHHHHH      HHH                                        ",
"                                                                                    R                   HHHHH                                   ",
"                                                                                    R                                                           ",
"                                                                                    R                                s                          ",
"                                                                                    R                             HHHHH                         ",
"                                                                                    R                                                           ",
"                                                                                    R                                                           ",
"                                                                                    R                         HHHH                              ",
"                                                                                    R                                                           ",
"                                                                                    R             HHHH    HHHH                                  ",
"                                                                                    R                                                           ",
"                                                                      ss            RH        H                                                 ",
"                                                                    HHHHHHH       HHHHHHHHHHHHH                                                 ",
"                 HHHH                                               H      H     H                             HHHHHHHHHHHHH                    ",
"                     HHH     s             HHHH    s    HHH    H    H       H   H                                                               ",
"                        HHHHHHH   HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH   H        HHH                                                                ",
        }; // line 158
    }
}
