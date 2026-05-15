#ifndef GRANITE_INCLUDED
#define GRANITE_INCLUDED

// Procedurální žula / hlína mezi granitem a hlínou.
// Volá se z HDRP Lit Shader Graphu přes Custom Function Node (File mode),
// funkce Granite_float. Veškerá logika je tady, .shadergraph je jen tenká slupka.
//
// Pravá 3D "solid" textura: krystaly žijí v prostoru (funkce světové pozice),
// ne na povrchu. Žádné švy, navazuje napříč bloky, řez blokem vypadá jako kámen.

// ---- hash (3D) -----------------------------------------------------------
float3 g_hash33(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return frac(sin(p) * 43758.5453);
}

float g_hash31(float3 p)
{
    return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
}

// ---- value noise + fbm (3D, pro domain warp a mikro variaci) -------------
float g_vnoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = g_hash31(i + float3(0, 0, 0));
    float n100 = g_hash31(i + float3(1, 0, 0));
    float n010 = g_hash31(i + float3(0, 1, 0));
    float n110 = g_hash31(i + float3(1, 1, 0));
    float n001 = g_hash31(i + float3(0, 0, 1));
    float n101 = g_hash31(i + float3(1, 0, 1));
    float n011 = g_hash31(i + float3(0, 1, 1));
    float n111 = g_hash31(i + float3(1, 1, 1));
    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

float g_fbm3(float3 p)
{
    float s = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        s += a * g_vnoise3(p);
        p *= 2.03;
        a *= 0.5;
    }
    return s;
}

// ---- voronoi (3D): F1 = vzdálenost k nejbližšímu bodu, F2 k druhému,
//      cell = ID buňky (pro náhodnou barvu krystalu) -----------------------
void g_voronoi3(float3 p, out float F1, out float F2, out float3 cell)
{
    float3 n = floor(p);
    float3 f = frac(p);
    F1 = 8.0;
    F2 = 8.0;
    cell = n;
    [unroll]
    for (int k = -1; k <= 1; k++)
    {
        [unroll]
        for (int j = -1; j <= 1; j++)
        {
            [unroll]
            for (int i = -1; i <= 1; i++)
            {
                float3 g = float3(i, j, k);
                float3 o = g_hash33(n + g);
                float3 r = g + o - f;
                float d = dot(r, r);
                if (d < F1)
                {
                    F2 = F1;
                    F1 = d;
                    cell = n + g;
                }
                else if (d < F2)
                {
                    F2 = d;
                }
            }
        }
    }
    F1 = sqrt(F1);
    F2 = sqrt(F2);
}

// ---- hlavní funkce volaná z Shader Graphu --------------------------------
// PositionOS  : pozice v object/local prostoru (Position node, Space = Object)
//               – vzor jezdí s objektem, když se hýbe
// PatternOffset: per-instance posun vzoru do jiné oblasti voronoi.
//               Nastavuje ColorVariations náhodně na každou z 8 variant
//               -> každý blok vypadá jinak. Default (0,0,0).
// Scale       : hustota krystalů
// WarpStrength: síla domain warpu (zubatost / ostrost hran)
// EdgeDark    : ztmavení tmavé sítě podél hranic krystalů (0..1)
// Speckle     : množství jemných tmavých zrnek (0..1)
// ColorVar    : variace jasu uvnitř krystalu (0..1)
// Color1..4   : barvy krystalů
// ColorWeights: relativní zastoupení barev (x..w), normalizuje se
// SmoothLight : smoothness světlých krystalů (křemen)
// SmoothDark  : smoothness tmavých krystalů (slída)
void Granite_float(
    float3 PositionOS, float3 PatternOffset, float Scale, float WarpStrength,
    float EdgeDark, float Speckle, float ColorVar,
    float3 Color1, float3 Color2, float3 Color3, float3 Color4,
    float4 ColorWeights, float SmoothLight, float SmoothDark,
    out float3 Albedo, out float Smoothness, out float Metallic, out float Height)
{
    float3 P = (PositionOS + PatternOffset) * Scale;

    // domain warp -> zubaté, ostře ohraničené krystaly (ne hladký voronoi)
    float3 warp = float3(g_fbm3(P * 0.7 + 13.1),
                         g_fbm3(P * 0.7 + 47.3),
                         g_fbm3(P * 0.7 + 71.7)) - 0.5;
    float3 q = P + warp * WarpStrength;

    float F1, F2;
    float3 cell;
    g_voronoi3(q, F1, F2, cell);

    // normalizované prahy barev
    float4 w = max(ColorWeights, 1e-4);
    w /= (w.x + w.y + w.z + w.w);
    float t1 = w.x;
    float t2 = t1 + w.y;
    float t3 = t2 + w.z;

    // náhodná hodnota krystalu -> výběr barvy
    float rnd = g_hash31(cell);
    float3 col;
    if (rnd < t1)       col = Color1;
    else if (rnd < t2)  col = Color2;
    else if (rnd < t3)  col = Color3;
    else                col = Color4;

    // variace jasu uvnitř krystalu (per-buňka + jemný mikrošum)
    float perCell = (g_hash31(cell + 3.7) - 0.5) * ColorVar;
    float micro   = (g_fbm3(P * 6.0) - 0.5) * ColorVar * 0.5;
    col *= 1.0 + perCell + micro;

    // tmavá síť podél hranic krystalů (úhlové ostré struktury)
    float edge = smoothstep(0.0, 0.06, F2 - F1);
    col *= lerp(1.0 - EdgeDark, 1.0, edge);

    // jemná tmavá zrnka – druhý vysokofrekvenční voronoi
    float sF1, sF2;
    float3 sCell;
    g_voronoi3(q * 4.3 + 19.0, sF1, sF2, sCell);
    if (g_hash31(sCell) < Speckle)
        col *= 0.22;

    Albedo = max(col, 0.0);
    Metallic = 0.0;

    // světlejší krystaly mírně lesklejší než tmavé
    float lum = dot(Albedo, float3(0.299, 0.587, 0.114));
    Smoothness = lerp(SmoothDark, SmoothLight, saturate(lum * 1.6));

    // výška pro Normal From Height: krystaly mírně vystupují, hrany zapadají
    Height = saturate(0.5 + (F1 - 0.30) * 0.8
                          - (1.0 - edge) * 0.35
                          + (g_fbm3(P * 10.0) - 0.5) * 0.18);
}

#endif // GRANITE_INCLUDED
