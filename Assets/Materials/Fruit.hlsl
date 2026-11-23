#ifndef FRUIT_INCLUDED
#define FRUIT_INCLUDED

// Procedurální povrch ovoce typu jahoda: dužina posetá semínky v důlcích.
// Volá se z HDRP Lit Shader Graphu přes Custom Function Node (File mode),
// funkce Fruit_float. Veškerá logika je tady, .shadergraph je jen tenká slupka.
//
// Semínka leží na sférické Fibonacciho spirále kolem středu objektu (pól = object Y),
// jako na skutečné jahodě. Nejbližší semínko se hledá inverzním Fibonacciho
// mapováním (Keinert et al. 2015) - O(1), bez závislosti na počtu semínek.
// Pravidelnost = jak moc se semínka náhodně posunou ze spirály.
// Funguje pro kulaté a oválné plody se středem v počátku object space.

static const float F_PI = 3.14159265;
static const float F_PHI = 1.61803398875;

// ---- hash (3D) -----------------------------------------------------------
float3 f_hash33(float3 p)
{
    p = float3(dot(p, float3(127.1, 311.7, 74.7)),
               dot(p, float3(269.5, 183.3, 246.1)),
               dot(p, float3(113.5, 271.9, 124.6)));
    return frac(sin(p) * 43758.5453);
}

float f_hash31(float3 p)
{
    return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
}

// ---- value noise + fbm (3D) ----------------------------------------------
float f_vnoise3(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = f_hash31(i + float3(0, 0, 0));
    float n100 = f_hash31(i + float3(1, 0, 0));
    float n010 = f_hash31(i + float3(0, 1, 0));
    float n110 = f_hash31(i + float3(1, 1, 0));
    float n001 = f_hash31(i + float3(0, 0, 1));
    float n101 = f_hash31(i + float3(1, 0, 1));
    float n011 = f_hash31(i + float3(0, 1, 1));
    float n111 = f_hash31(i + float3(1, 1, 1));
    float x00 = lerp(n000, n100, u.x);
    float x10 = lerp(n010, n110, u.x);
    float x01 = lerp(n001, n101, u.x);
    float x11 = lerp(n011, n111, u.x);
    return lerp(lerp(x00, x10, u.y), lerp(x01, x11, u.y), u.z);
}

float f_fbm3(float3 p)
{
    float s = 0.0;
    float a = 0.5;
    [unroll]
    for (int i = 0; i < 3; i++)
    {
        s += a * f_vnoise3(p);
        p *= 2.03;
        a *= 0.5;
    }
    return s;
}

// ---- nejbližší semínko na Fibonacciho spirále ----------------------------
// p      : jednotkový směr, pól na ose z
// n      : počet semínek
// jitter : náhodný posun semínek v tečné rovině (jednotky koule)
// salt   : per-instance posun hashe
// dist   : vzdálenost k nejbližšímu semínku (jednotky koule)
// id     : index nejbližšího semínka
void f_fibSeeds(float3 p, float n, float jitter, float3 salt, out float dist, out float id)
{
    // semínka mírně protažená podél poledníku
    float3 up = float3(0, 0, 1) - p * p.z;
    float upLen = length(up);
    float3 u = upLen > 1e-4 ? up / upLen : float3(1, 0, 0);
    float stretch = lerp(1.0, 1.35, saturate(upLen * 3.0));

    // buňka Fibonacciho mřížky v prostoru (phi, cos theta), ve které leží p
    float m = 1.0 - 1.0 / n;
    float phi = atan2(p.y, p.x);
    float cosT = p.z;
    float k = max(2.0, floor(log(n * F_PI * sqrt(5.0) * (1.0 - cosT * cosT)) / log(F_PHI * F_PHI)));
    float Fk = pow(F_PHI, k) / sqrt(5.0);
    float2 F = float2(round(Fk), round(Fk * F_PHI));
    float2 ka = 2.0 * F / n;
    float2 kb = 2.0 * F_PI * (frac((F + 1.0) * F_PHI) - (F_PHI - 1.0));
    float det = ka.y * kb.x - ka.x * kb.y;
    float2 v = float2(phi, cosT - m);
    float2 c = floor(float2(ka.y * v.x + kb.y * v.y, -ka.x * v.x - kb.x * v.y) / det);

    // 4x4 kandidátů (místo 2x2), aby našel nejbližší i u posunutých semínek
    float best = 1e9;
    id = 0.0;
    [unroll]
    for (int s = 0; s < 16; s++)
    {
        float2 uv = float2(s % 4, s / 4) - 1.0;
        float i = round(dot(F, uv + c));
        if (i < 0.0 || i >= n)
            continue;
        float ph = 2.0 * F_PI * frac(i * F_PHI);
        float ct = m - 2.0 * i / n;
        float st = sqrt(saturate(1.0 - ct * ct));
        float3 q = float3(cos(ph) * st, sin(ph) * st, ct);

        float3 h = f_hash33(float3(i * 0.0137, 1.7, 3.1) + salt) - 0.5;
        q = normalize(q + (h - q * dot(h, q)) * jitter);

        float3 r = q - p;
        float ru = dot(r, u);
        float3 rv = r - u * ru;
        float d2 = ru * ru / (stretch * stretch) + dot(rv, rv);
        if (d2 < best)
        {
            best = d2;
            id = i;
        }
    }
    dist = sqrt(best);
}

// 0 a 1. derivace nulové na obou koncích -> důlek plynule navazuje na povrch
float f_smootherstep(float a, float b, float x)
{
    float t = saturate((x - a) / max(b - a, 1e-4));
    return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
}

// ---- hlavní funkce volaná z Shader Graphu --------------------------------
// PositionOS   : pozice v object space (Position node, Space = Object)
// PatternOffset: per-instance variace (ColorVariations): .x otáčí spirálou kolem osy,
//                celé slouží jako sůl hashe a posun skvrn. Default (0,0,0)
// SeedCount    : počet semínek na plod
// SeedSize     : poloměr semínka (zlomek rozestupu semínek)
// PitSize      : poloměr důlku kolem semínka (zlomek rozestupu)
// PitDepth     : hloubka důlků (pro Normal From Height)
// PitDark      : ztmavení dužiny v důlku (0..1)
// ColorMix     : míra skvrn druhé barvy dužiny (0..1)
// Color        : hlavní barva dužiny (_Color -> funguje s ColorVariations)
// Color2       : druhá barva dužiny (skvrny, přechody)
// SeedColor    : barva semínek
// SmoothFlesh  : smoothness dužiny
// SmoothSeed   : smoothness semínek
// Regularity   : 1 = přesná spirála, 0 = semínka náhodně rozházená
// PitSoftness  : 0 = důlek s ostrým okrajem, 1 = pozvolný přechod do povrchu
void Fruit_float(
    float3 PositionOS, float3 PatternOffset,
    float SeedCount, float SeedSize, float PitSize, float PitDepth, float PitDark, float ColorMix,
    float3 Color, float3 Color2, float3 SeedColor, float SmoothFlesh, float SmoothSeed,
    float Regularity, float PitSoftness,
    out float3 Albedo, out float Smoothness, out float Metallic, out float Height)
{
    float n = max(round(SeedCount), 8.0);
    float spacing = sqrt(4.0 * F_PI / n);         // rozestup semínek na jednotkové kouli

    float3 dir = normalize(PositionOS);
    float3 p = float3(dir.x, dir.z, dir.y);       // pól spirály = object Y
    float sn, cs;
    sincos(PatternOffset.x, sn, cs);
    p.xy = float2(cs * p.x - sn * p.y, sn * p.x + cs * p.y);

    float jitter = (1.0 - saturate(Regularity)) * spacing;
    float seedDist, id;
    f_fibSeeds(p, n, jitter, PatternOffset, seedDist, id);
    float dist = seedDist / spacing;

    // šířka hrany podle derivací -> semínka v dálce neblikají
    float aa = max(fwidth(dist), 0.02);

    // důlek: plochá část u semínka, pak hladký přechod do povrchu
    float pitR = max(PitSize, SeedSize + 0.01);
    float pitT = dist / pitR;
    float inner = (1.0 - saturate(PitSoftness)) * 0.75;
    float pit = 1.0 - f_smootherstep(inner, 1.0, pitT);

    float seedT = saturate(dist / max(SeedSize, 1e-3));
    float seedMask = 1.0 - smoothstep(SeedSize - aa, SeedSize + aa, dist);

    // vzorový prostor pro šum: 1 jednotka ~ rozestup semínek (koule o poloměru 0.5)
    float3 P = (PositionOS + PatternOffset) * sqrt(n / F_PI);

    // dužina: skvrny druhé barvy + jemný mikrošum
    float blot = smoothstep(0.35, 0.65, f_fbm3(P * 0.3 + 5.1)) * ColorMix;
    float3 flesh = lerp(Color, Color2, blot);
    flesh *= 1.0 + (f_fbm3(P * 4.0 + 11.3) - 0.5) * 0.15;
    flesh *= 1.0 - PitDark * pit;

    // semínko: náhodná variace jasu, tmavší okraj
    float seedVar = 1.0 + (f_hash31(float3(id * 0.0137, 7.3, 0.0) + PatternOffset) - 0.5) * 0.3;
    float3 seed = SeedColor * seedVar * lerp(1.0, 0.65, seedT * seedT);

    Albedo = max(lerp(flesh, seed, seedMask), 0.0);
    Smoothness = lerp(SmoothFlesh, SmoothSeed, seedMask);
    Metallic = 0.0;

    // výška: důlek dolů, semínko vystupuje jako kopulka
    float dome = sqrt(saturate(1.0 - seedT * seedT));
    Height = 0.5 - pit * PitDepth + dome * PitDepth * 0.6
           + (f_fbm3(P * 6.0) - 0.5) * PitDepth * 0.1;
}

#endif // FRUIT_INCLUDED
