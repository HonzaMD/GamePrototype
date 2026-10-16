using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Ai
{
    // LADENI AI: cary v hernim okne (LineRenderery z PrefabsStore.DebugVisibility, poolovane).
    // Toggle klavesa F8. Prisery cary posilaji v GameUpdate (Line), Render z Game.Update po
    // UpdateObjects schova, co se tento frame nepouzilo. Zakomentuj volani Render a vrstva zmizi.
    //   zelena  - GoToward: cara k cili rozkazu
    //   cervena - aktivni nedosazitelna mista (MarkUnreachable)
    //   zluta   - nalezy smyslu ne starsi nez SenseMaxAge (vzadu, za ostatnimi)
    public static class AiDebug
    {
        private const KeyCode ToggleKey = KeyCode.F8;
        private const float DebugZ = -0.75f;     // bliz ke kamere nez hraci rovina (CellSimDebug -0.7)
        private const float DebugZBack = -0.72f; // vrstva za hlavnimi carami
        private const float Width = 0.03f;

        public const float SenseMaxAge = 2f;    // [s]
        public static readonly Color SenseColor = new Color(1f, 1f, 0.55f);

        public static bool Active { get; private set; }

        private static readonly List<LineRenderer> lines = new();
        private static int used;        // tento frame
        private static int shown;       // zapnute z minula

        public static void Line(Vector2 from, Vector2 to, Color color, bool back = false)
        {
            if (!Active)
                return;
            if (used == lines.Count)
                lines.Add(CreateLine());
            var line = lines[used++];
            float z = back ? DebugZBack : DebugZ;
            line.SetPosition(0, new Vector3(from.x, from.y, z));
            line.SetPosition(1, new Vector3(to.x, to.y, z));
            line.startColor = color;
            line.endColor = color;
            if (!line.enabled)
                line.enabled = true;
        }

        // Vola se kazdy frame z Game.Update po UpdateObjects.
        public static void Render()
        {
            if (Input.GetKeyDown(ToggleKey))
                Active = !Active;
            if (Active && Game.Instance.IsPaused)
                return;     // v pauze se UpdateObjects nevola - nech cary z posledniho framu

            for (int i = used; i < shown; i++)
                lines[i].enabled = false;
            shown = used;
            used = 0;
        }

        private static LineRenderer CreateLine()
        {
            var go = Object.Instantiate(Game.Instance.PrefabsStore.DebugVisibility);
            go.name = "AiDebugLine";
            Object.DontDestroyOnLoad(go);
            var line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.positionCount = 2;
            line.widthMultiplier = 1;
            line.startWidth = Width;
            line.endWidth = Width;
            return line;
        }
    }
}
