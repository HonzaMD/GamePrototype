using UnityEngine;

namespace Assets.Scripts.Stuff
{
    public enum Dir8 : byte
    {
        Up,
        UpRight,
        Right,
        DownRight,
        Down,
        DownLeft,
        Left,
        UpLeft,
    }

    public static class Dir8Extensions
    {
        private static readonly Vector2[] vectors =
        {
            new Vector2(0, 0.5f),      // Up
            new Vector2(0.5f, 0.5f),   // UpRight
            new Vector2(0.5f, 0),      // Right
            new Vector2(0.5f, -0.5f),  // DownRight
            new Vector2(0, -0.5f),     // Down
            new Vector2(-0.5f, -0.5f), // DownLeft
            new Vector2(-0.5f, 0),     // Left
            new Vector2(-0.5f, 0.5f),  // UpLeft
        };

        // Premapovana Y slozka smeru na rozsah 0.01-1: Down=0.01, Right=0.5, Up=1
        private static readonly float[] upBases =
        {
            1f,                          // Up
            (1f + 0.7071f) / 2f,         // UpRight  ~0.854
            0.5f,                        // Right
            (1f - 0.7071f) / 2f + 0.01f, // DownRight ~0.156
            0.02f,                       // Down
            (1f - 0.7071f) / 2f + 0.01f, // DownLeft  ~0.156
            0.5f,                        // Left
            (1f + 0.7071f) / 2f,         // UpLeft    ~0.854
        };

        public static Vector2 ToVector(this Dir8 dir) => vectors[(int)dir];

        public static Dir8 Opposite(this Dir8 dir) => (Dir8)(((int)dir + 4) & 7);

        public static bool IsDiagonal(this Dir8 dir) => ((int)dir & 1) != 0;

        public static Quaternion ToRotation(this Dir8 dir) => Quaternion.Euler(0, 0, -(int)dir * 45f);

        public static float UpWeight(this Dir8 dir, float exponent) => Mathf.Pow(upBases[(int)dir], exponent);
    }
}
