using System;
using Assets.Scripts.Core;
using Unity.Collections;
using UnityEngine;

namespace Assets.Scripts.Map.CellSims
{
    /// <summary>
    /// Debug overlay simulovaných cell veličin (tečka per buňka).
    ///
    /// Plně izolované od zbytku kódu — jediný kontakt je volání <see cref="Render"/>
    /// z Game.Update. Zakomentuj ten jeden řádek a celá vrstva zmizí (žádné jiné
    /// místo na ni neodkazuje, žádné objekty v editoru).
    ///
    /// Toggle klávesa: F9. Vykresluje jen aktivní svět
    /// (Game.Instance.MapWorlds.SelectedMap); při přepnutí světa se textury
    /// přealokují na rozměry nové mapy. Základní chyby setupu se hlásí výjimkou.
    ///
    /// Konfigurace pouze kódem — viz region KONFIGURACE níže.
    /// </summary>
    public static class CellSimDebug
    {
        // ===================== KONFIGURACE (jen kódem) =====================

        public enum Scale { Linear = 0, Log = 1, Sqrt = 2 }

        private const KeyCode ToggleKey = KeyCode.F9;

        /// <summary>Z rovina overlaye — blíž ke kameře (-Z) než hrací rovina, ať je navrchu.</summary>
        private const float DebugZ = -0.7f;

        private const float GlobalAlpha = 0.85f;

        /// <summary>
        /// Test režim: shader ignoruje data a vykreslí tečku v KAŽDÉ buňce
        /// (i při samých nulách v simulaci) — ověření render pipeline nezávisle na datech.
        /// </summary>
        private const bool ForceTestPattern = false;

        /// <summary>Jedna zobrazovaná veličina. Přidání další = jeden řádek v <see cref="Channels"/>.</summary>
        private readonly struct Channel
        {
            public readonly string Name;
            public readonly Func<CellSimWorld, NativeArray<sbyte>> Source;
            public readonly Scale ScaleMode;     // mapování magnitudy na jas, per kanál
            public readonly Vector2 DotOffset;   // pozice tečky uvnitř buňky, 0..1
            public readonly float DotRadius;     // poloměr tečky v cell-space (0..0.5)
            public readonly float FullScale;     // |hodnota| → plná sytost
            public readonly Color Neg, Zero, Pos;

            public Channel(string name, Func<CellSimWorld, NativeArray<sbyte>> source,
                Scale scaleMode, Vector2 dotOffset, float dotRadius, float fullScale,
                Color neg, Color zero, Color pos)
            {
                Name = name; Source = source; ScaleMode = scaleMode;
                DotOffset = dotOffset; DotRadius = dotRadius; FullScale = fullScale;
                Neg = neg; Zero = zero; Pos = pos;
            }
        }

        // Seznam veličin zobrazených současně. Každá má vlastní texturu, barvy,
        // škálu a pozici tečky v buňce (ať se víc veličin nepřekrývá).
        private static readonly Channel[] Channels =
        {
            new Channel(
                name: "Element",
                source: w => w.elementPublic,
                scaleMode: Scale.Sqrt,
                dotOffset: new Vector2(0.2f, 0.2f),
                dotRadius: 0.15f,
                fullScale: 5f,
                neg:  new Color(0.25f, 0.55f, 1.00f, 1f),   // záporné (debt) = studená modrá
                zero: new Color(0.15f, 0.15f, 0.15f, 0f),   // ~0 = průhledná
                pos:  new Color(1.00f, 0.55f, 0.10f, 1f)),  // kladné = teplá oranžová
        };

        // ===================== STAV =====================

        private static bool active;
        private static Material material;
        private static Mesh quad;
        private static Texture2D[] textures;
        private static MaterialPropertyBlock[] mpbs;
        private static Map boundMap;
        private static int texW, texH;

        private static readonly int MainTexId    = Shader.PropertyToID("_MainTex");
        private static readonly int GridWId      = Shader.PropertyToID("_GridW");
        private static readonly int GridHId      = Shader.PropertyToID("_GridH");
        private static readonly int NegId        = Shader.PropertyToID("_NegColor");
        private static readonly int ZeroId       = Shader.PropertyToID("_ZeroColor");
        private static readonly int PosId        = Shader.PropertyToID("_PosColor");
        private static readonly int FullScaleId  = Shader.PropertyToID("_FullScale");
        private static readonly int ScaleModeId  = Shader.PropertyToID("_ScaleMode");
        private static readonly int DotXId       = Shader.PropertyToID("_DotX");
        private static readonly int DotYId       = Shader.PropertyToID("_DotY");
        private static readonly int DotRId       = Shader.PropertyToID("_DotR");
        private static readonly int AlphaId      = Shader.PropertyToID("_GlobalAlpha");
        private static readonly int TestModeId   = Shader.PropertyToID("_TestMode");

        // ===================== ENTRY POINT =====================

        /// <summary>Volá se každý frame z Game.Update. Sám si řeší toggle i lazy init.</summary>
        public static void Render()
        {
            if (Input.GetKeyDown(ToggleKey))
                active = !active;
            if (!active) return;

            var map = Game.Instance?.MapWorlds?.SelectedMap
                ?? throw new InvalidOperationException("CellSimDebug: není aktivní svět (SelectedMap == null).");
            var sim = map.CellSim
                ?? throw new InvalidOperationException("CellSimDebug: map.CellSim == null.");

            EnsureSharedResources();

            if (boundMap != map || texW != sim.Width || texH != sim.Height)
                Rebind(map, sim);

            float csx = Map.CellSize.x, csy = Map.CellSize.y;
            var matrix = Matrix4x4.TRS(
                new Vector3(map.mapOffset.x, map.mapOffset.y, DebugZ),
                Quaternion.identity,
                new Vector3(sim.Width * csx, sim.Height * csy, 1f));

            int cellCount = sim.Width * sim.Height;
            for (int i = 0; i < Channels.Length; i++)
            {
                var ch = Channels[i];
                var data = ch.Source(sim);
                if (!data.IsCreated || data.Length < cellCount)
                    throw new InvalidOperationException(
                        $"CellSimDebug: kanál '{ch.Name}' má nealokovaný/krátký buffer " +
                        $"(IsCreated={data.IsCreated}, len={data.Length}, need={cellCount}).");

                var tex = textures[i];
                tex.SetPixelData(data, 0);
                tex.Apply(false, false);

                var mpb = mpbs[i];
                mpb.SetTexture(MainTexId, tex);
                mpb.SetFloat(GridWId, sim.Width);
                mpb.SetFloat(GridHId, sim.Height);
                mpb.SetColor(NegId, ch.Neg);
                mpb.SetColor(ZeroId, ch.Zero);
                mpb.SetColor(PosId, ch.Pos);
                mpb.SetFloat(FullScaleId, ch.FullScale);
                mpb.SetFloat(ScaleModeId, (float)ch.ScaleMode);
                mpb.SetFloat(DotXId, ch.DotOffset.x);
                mpb.SetFloat(DotYId, ch.DotOffset.y);
                mpb.SetFloat(DotRId, ch.DotRadius);
                mpb.SetFloat(AlphaId, GlobalAlpha);
                mpb.SetFloat(TestModeId, ForceTestPattern ? 1f : 0f);

                Graphics.DrawMesh(quad, matrix, material, 0, null, 0, mpb);
            }
        }

        // ===================== INIT / REBIND =====================

        private static void EnsureSharedResources()
        {
            if (material == null)
            {
                var shader = Shader.Find("Hidden/CellSimDebug")
                    ?? throw new InvalidOperationException("CellSimDebug: shader 'Hidden/CellSimDebug' nenalezen.");
                material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            if (quad == null)
            {
                quad = new Mesh { name = "CellSimDebugQuad", hideFlags = HideFlags.HideAndDontSave };
                quad.SetVertices(new[]
                {
                    new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                    new Vector3(1f, 1f, 0f), new Vector3(0f, 1f, 0f),
                });
                quad.SetUVs(0, new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 1f), new Vector2(0f, 1f),
                });
                quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
                quad.RecalculateBounds();
            }
        }

        private static void Rebind(Map map, CellSimWorld sim)
        {
            DisposeTextures();

            int n = Channels.Length;
            textures = new Texture2D[n];
            mpbs = new MaterialPropertyBlock[n];
            for (int i = 0; i < n; i++)
            {
                textures[i] = new Texture2D(sim.Width, sim.Height, TextureFormat.R8, false, true)
                {
                    name = "CellSimDebug_" + Channels[i].Name,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                mpbs[i] = new MaterialPropertyBlock();
            }

            boundMap = map;
            texW = sim.Width;
            texH = sim.Height;
        }

        private static void DisposeTextures()
        {
            if (textures == null) return;
            foreach (var t in textures)
                if (t != null) UnityEngine.Object.Destroy(t);
            textures = null;
            mpbs = null;
        }
    }
}
