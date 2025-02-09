using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.Utils
{
    internal static class NumberToString
    {
        public static string Convert(int value)
        {
            if (value <= ranges[0].End)
            {
                if (value < ranges[0].Start)
                    return "-??";
                return texts[0][value - ranges[0].Start];
            }
            else if (value <= ranges[2].End)
            {
                if (value <= ranges[1].End)
                {
                    return texts[1][(value - ranges[1].Start) / ranges[1].Div];
                }
                else
                {
                    return texts[2][(value - ranges[2].Start) / ranges[2].Div];
                }
            }
            else
            {
                if (value <= ranges[3].End)
                {
                    return texts[3][(value - ranges[3].Start) / ranges[3].Div];
                }
                else
                {
                    return "+??";
                }
            }
        }


        private struct RangeInfo
        {
            public int Start, End;
            public int Div;
            public Func<int, string> Formater;

            public RangeInfo(int start, int end, int div, Func<int, string> formater)
            {
                Start = start;
                End = end;
                Div = div;
                Formater = formater;
            }
        }

        private static readonly RangeInfo[] ranges =
        {
            new (-100, 204, 1, i => i.ToString()),
            new (205, 994, 10, i => $"{(float)i / 10:00\\0}"),
            new (950, 9949, 100, i => $"{(float)i / 100:0\\.0}k"),
            new (9500, 100000, 1000, i => $"{(float)i / 1000:00}k")
        };

        private static readonly string[][] texts = InitTexts();

        private static string[][] InitTexts()
        {
            return ranges.Select(r => InitRange(r)).ToArray();
        }

        private static string[] InitRange(RangeInfo r)
        {
            int end = (r.End - r.Start) / r.Div;
            string[] ret = new string[end + 1];
            for (int i = 0; i <= end; i++)
                ret[i] = r.Formater(i * r.Div + r.Start);
            return ret;
        }
    }
}
