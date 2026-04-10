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

        public static Vector2 ToVector(this Dir8 dir) => vectors[(int)dir];

        public static Dir8 Opposite(this Dir8 dir) => (Dir8)(((int)dir + 4) & 7);

        public static bool IsDiagonal(this Dir8 dir) => ((int)dir & 1) != 0;
    }
}
