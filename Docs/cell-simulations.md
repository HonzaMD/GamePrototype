# Cell Simulations — návrh

Per-buňková simulace pro vlastnosti světa (teplota, prvky v hlíně, kapaliny, později vítr/tlak vzduchu...). Společný framework, tři algoritmy.

> Status: **návrh / prototyp**. Zatím není implementováno, není rozhodnuto, jestli se to dostane do hry. Slouží jako podklad pro budoucí prototyp.

## Cíl

- Pole simulovaných veličin nad mřížkou buněk (0.5 m × 0.5 m × 0.5 m, sdílená s `Map`).
- Měřítko: až 432 × 432 buněk per svět, až 5 světů → ~933k buněk celkem.
- Krok simulace per svět **deterministicky** (ne amortizace v čase jako u písku).
- Per cell update čte stav `t-1` sebe a sousedů, píše stav `t`.
- Frekvence ~5 Hz (každý 10. fixed update).
- Game objekty mají náhodný R/W přístup do polí — různá frekvence per objekt.

## Update 2026-05-08: 3-buffer R/W model

Při implementaci jsme změnili gameplay ↔ sim mechanismus pro spojitá pole (teplota, fill, pressure, element). Místo původního *Front/Back + write queue* používáme **3-buffer model**:

- **Public** — gameplay R/W, instant visibility vlastních zápisů.
- **Front** — sim snapshot `Public` na začátku stepu (kopie). Sim z něho čte.
- **Back** — sim výstup. Po sim kroku se aplikuje delta: `Public += Back - Front`.

**Důvod**: queue model rozbíjel "check-then-take" vzor (gameplay přečte hodnotu, rozhodne, zapíše; další přečtení vidí starou hodnotu, protože queue se aplikuje až v dalším stepu → opakovaný zápis ⇒ debt). 3-buffer model dává gameplay-side instant visibility a sim si drží vlastní izolovanou kopii (Front), takže běh jobu je race-free i bez locků.

**Cena**: jeden buffer per pole navíc + jeden snapshot copy + jeden reconcile pass per step. Oba pasy jsou Burst-vektorizovatelné lineární smyčky, sub-ms na 933k cellech.

**Zápisové fronty (`NativeQueue<...Write>`)** ponecháváme v plánu pro **řídké/diskrétní zápisy** (zejména `material` change), kde je batch pattern přirozenější a 3. buffer by byl plýtvání pamětí. Per-field continuous queues z původního návrhu (TempWrite, FillWrite, ElementWrite) odpadly.

Detaily jsou aplikovány níže v sekci *Layout (SoA per svět)* a *Gameplay API*. Diskusní sekce o debt modelu, Margolus konzervaci, kavitaci atd. zůstávají v platnosti — gameplay-write semantika "signed storage + debt" funguje na *Public* bufferu identicky.

## Architektonická volba: CPU + Burst Jobs

Důvody:
1. Zátěž (~5M cell-updateů/s) je pro Burst pohodová, GPU by byla overkill.
2. Charakteristika přístupu (gameplay random R/W, nepředvídatelná frekvence) rozbíjí GPU readback. AsyncGPUReadback latence ~2-3 framy.
3. Plánuje se na background thread (kde má `StaticPhysics` rezervu).
4. Determinismus a debugging triviálně.

GPU se uvažuje pouze jako pozdější vrstva nad těžké izolované úlohy (např. Poisson solver pro plnou Eulerovskou tekutinovou simulaci, kdyby kdykoliv vznikla).

## Společný framework

### Layout (SoA per svět)

```csharp
public sealed class CellSimWorld : IDisposable
{
    public int Width, Height;

    // Sdílené (single buffer, čtené ze všech simulací).
    // Material byte = flags v horních bitech | ID v dolních bitech (viz "Material encoding").
    public NativeArray<byte> material;

    // Per pole (3-buffer model — viz Update 2026-05-08):
    //   *Public* — gameplay R/W, instant visibility
    //   *Front*  — snapshot Public na začátku stepu, sim čte
    //   *Back*   — sim výstup; reconcile: Public += Back - Front
    public NativeArray<float> temperaturePublic, temperatureFront, temperatureBack;
    public NativeArray<short> fillPublic,        fillFront,        fillBack;   // logický rozsah 0..255,
                                                                                // signed/widened pro overflow / debt
    public NativeArray<float> pressurePublic,    pressureFront,    pressureBack;
    public NativeArray<sbyte> elementPublic,     elementFront,     elementBack; // signed pro debt model

    // Tabulky (small, indexované celým material bytem):
    public NativeArray<MaterialProps> matTable;

    // Material change queue — ponecháno pro řídké/diskrétní zápisy (transformace cell typu).
    // Kontinuální pole (temp/fill/element) jsou pokryta 3-buffer modelem výše.
    public NativeQueue<MaterialWrite> materialWrites;

    public JobHandle pending;
    public bool      InitMode;     // true při loadu — sim se neplánuje, gameplay zapisuje přímo do Public

    public int Idx(int x, int y) => y * Width + x;
    public int Idx(Vector2Int pos) => pos.y * Width + pos.x;
}

public struct MaterialProps
{
    public float thermalConductivity;
    public float heatCapacity;
    public float strength;          // pro destrukci tlakem
    public short targetFill;        // cílový fill pro compressibility term (voda=255, stěna=0, porézní hlína např. 64)
    public byte  canHoldElement;    // 0/1 — může cell nést prvky? (vstupuje do Margolus block rule)
    // flags duplicitně i v material bytu pro rychlý hot-path test bez indirekce
}
```

### Material encoding

Material byte kombinuje flagy a ID:
- **Horní bity** (3–4) — flagy nejčastěji testované v sim jobs: `Solid`, `BlocksWater`, `Dirt`, `Porous`.
- **Dolní bity** (4–5) — ID variant uvnitř flag-comba (16–32 materiálů per kombinace).

Důvod: hot-path test v Burst se redukuje na `(material[idx] & SolidMask) != 0` — jeden load místo dvou (proti `mat[material[idx]].flags`). Při 100k+ buňkách per step to má smysl. `MaterialProps[256]` tabulka indexovaná celým bytem zůstává pro úplné vlastnosti (vodivost, kapacita, targetFill, ...), používá se v cestách, kde stejně potřebujeme floats.

Důvod SoA (separate `NativeArray` per pole): simulační joby čtou typicky jedno pole — cache-friendly. Gameplay queries sice tahají víc polí, ale jsou řídké, pár cache misses navíc je triviální.

### Init mode vs. runtime mode

Při loadu levelu zapisujeme desetitisíce buněk najednou — fronta by byla zbytečná režie. Proto má svět dva módy:

```
world.BeginInit()       → sim není scheduled, gameplay zapisuje přímo do material/fill/element bufferů
... level load ...
world.EndInit()         → validace (např. konzistence material flagů s targetFill), přepnutí do runtime
```

V runtime jdou všechny zápisy včetně `material` přes frontu. ApplyWritesJob na začátku stepu zaručí, že sim nikdy nevidí rozbitý stav.

### Scheduling

Krok simulace volá `Game` z `GameFixedUpdate` každý 10. tick.

**Pro pole bez vnitřní závislosti** (teplota, elementy):
```
1. ApplyWritesJob       — flush gameplay zápisů do *Front* bufferu
2. StepJob              — read Front, write Back
3. CompleteAndSwap      — Complete handle, swap Front/Back referencí
```

**Pro vodu** (relax tlaku → flow → fix) je nutné, aby flow viděl **čerstvě relaxovaný tlak**, ne hodnotu z minulého ticku. Proto dva swapy v rámci jednoho ticku:

```
1. ApplyWritesJob       — flush zápisů do fillFront, pressureFront
2. PressureRelaxJob     — read pFront, write pBack (1 nebo více sweepů)
3. Complete + swap pressure (pBack → pFront)
4. WaterFlowJob         — read pFront (= čerstvě relaxovaný), fillFront → fillBack
5. WaterColumnFixJob    — fix overflow ve fillBack
6. Complete + swap fill (fillBack → fillFront)
```

> **Důsledek**: bez pressure-swap v rámci ticku dochází k 1-tick lagu mezi tlakovou změnou (např. prokopnutí díry) a flow reakcí — výrazně se prodlouží transienty. Při více Jacobi sweepech per tick se výhoda znásobí, protože každý sweep používá výsledek předchozího.

`ApplyWrites` zapíše do `Front` PŘED schedulem stepu — žádný race s běžícím jobem.

Mezi voláními kroku se gameplay čte `Front` přímo (bez synchronizace) a zapisuje přes `Enqueue`. Read po skončení stepu je vždy čerstvě commitnutý stav.

### Gameplay R/W semantika — odebírání, debt model, material change

Klíčový problém: simulace běží asynchronně a tokeny se mezi tiky stěhují. Když gameplay chce **instantně** odebrat hlínu z buňky, simulace mohla mezitím prvky odsunout do sousedů.

**Model: signed storage + debt s parovou kompenzací**

- Storage je signed (`sbyte` pro elementy, `short` pro fill) — povoluje záporné mezistavy.
- Při odebrání hlíny atomicky odečteme **logický obsah**, který hlína "nesla" (ne jen viditelný count). Pokud cell po odečtení skončí v záporu (= simulace mezitím odsunula `k` tokenů jinam), vznikne pár:
  - `+k` phantom v některém ze 4 sousedů (token, který "utekl" před odebráním),
  - `-k` debt v původní buňce.
- Margolus s alternujícím offsetem dává sousední buňky do stejného 2×2 bloku každý druhý step. Block-redistribute strukturálně ruší opačné tokeny ve stejném bloku (sum = a+b+c+d, opačné se odečtou v baseShare). Pár startující 1 buňku od sebe se potkává v bloku každý 2. step a postupně mizí.
- Difuzní rozprostření roste jen jako √t, takže pár se nestihne rozutéct daleko před vyrušením.

**Detaily:**

1. **Margolus s negative sums**: `sum >> 2` v C# zaokrouhluje k −∞, `sum/4` k 0. Lhostejné kterou variantu zvolíme, důležité je `4*baseShare + rem == sum` a aby rotující priorita distribuovala `rem` (i záporný) symetricky. Pokrýt unit testy na malých případech.

2. **Read API clampuje na 0**: dotaz "kolik je tu železa" vrací `max(0, fill)` — záporné železo logicky neexistuje, je to účetní mezistav. Sim job musí vidět skutečnou hodnotu.

3. **Watchdog (volitelně)**: po N stepech bez zmeny materialu lze ověřit `Sum(fill)` per region jako sanity-check driftu.

**Material change — bez specialní logiky**

Když se buňka transformuje (hlína → vzduch, voda → stěna), nesnažíme se obsah aktivně přerozdělit do sousedů. Pravidlo:

> **Materiál brání zápisu, ale nebrání čtení.**

- **Voda**: zobecněný compressibility term s per-material `targetFill` přirozeně tlačí fill k cílové hodnotě. Stěna má `targetFill = 0`, takže voda v právě vytvořené stěně má `p_eff` vysoké → outflow → postupně vyteče. Detail v algoritmu 3.
- **Elementy**: adaptované Margolus block rule respektuje per-material `canHoldElement`. Stěna v bloku tokeny nepřijímá (priorita pro non-wall buňky), wall cell v bloku tokeny vypouští. Detail v algoritmu 2.

Tím získáme jednu uniformní cestu pro:
- Postupné odtékání obsahu z nově vzniklé stěny.
- Vyrušení `+k`/`−k` debt párů z odebrání.
- Přirozenou reakci na overflow (fill > 255) i underflow (fill < 0).

**Edge case — sealed wall area s uvězněným obsahem**: pokud se celá oblast buněk najednou stane stěnou bez akceptujícího souseda v žádném bloku, obsah strukturálně neumí odtéct. Tento případ explicitně neřešíme — gameplay-block velkých oblastí najednou by stejně nešel svědomitě konzervovat (kde má obsah skončit?).

### Gameplay API

Souřadnice používají `Vector2Int` (stejný systém jako `Map`). Metody jsou bounds-checked a clampují hodnoty do rozsahu typu. Pro perf-kritický kód je *Public* buffer veřejný — přímý přístup `world.elementPublic[world.Idx(pos)]` je rychlejší (bez bounds checku, bez clampu).

```csharp
// READ — instant, čte Public buffer; clamp na 0 maskuje záporné účetní mezistavy.
public float GetTemperature(Vector2Int pos);
public int   GetFill(Vector2Int pos);
public int   GetElement(Vector2Int pos);

// WRITE — instant do Public bufferu. Sim si vezme snapshot Public → Front
// na začátku dalšího stepu, žádná queue.
public void AddHeat(Vector2Int pos, float dT);
public void AddFill(Vector2Int pos, int delta);
public void AddElement(Vector2Int pos, int delta);
public void SetElement(Vector2Int pos, int value);

// Odebírání: signed delta, povoluje pokles do záporu (debt — viz "Gameplay R/W
// semantika"). Díky instant visibility v Public bufferu už opakované check-then-take
// nekumuluje debt: druhé GetElement po AddElement(-N) vidí už sníženou hodnotu.

// Material change — řídké zápisy, queue (batch transakce):
public void ChangeMaterial(Vector2Int pos, byte newMat);

// Direct access pro perf-hot kód — bez bounds checku, raw sbyte (může být záporný):
//   world.elementPublic[world.Idx(pos)] += (sbyte)delta;
//   world.elementPublic[world.Idx(x, y)] = (sbyte)v;
```

### Společné optimalizace (k zvážení později)

- **Dirty tile tracking**: rozsek na 16×16 dlaždice, sleduj per dlaždici „změnilo se něco?". Mrtvé dlaždice (rovnováha + sousedé v rovnováze) přeskoč. Při ustáleném světě 5–10× zrychlení.
- **Sub-stepping podle CFL**: automatický loop uvnitř `Step()` pokud `dt > dt_max` daný materiálovým parametrem.

---

## Algoritmus 1 — Teplota (float diffusion)

### Model

Klasická explicitní finite-difference difuze:

```
T_new[c] = T_old[c] + (dt / heatCapacity[c]) · Σ_neighbors k_eff(c,n) · (T[n] - T[c])
```

- `k_eff` = harmonický průměr vodivostí: `2·k_a·k_b / (k_a + k_b)`. Aritmetický průměr by dal divné výsledky na izolant↔vodič rozhraní.
- 4-sousední 2D stencil.
- Per cell `material[c]` → `MaterialProps[material[c]]` → `thermalConductivity`, `heatCapacity`.

### Stabilita (CFL)

```
max(k_eff) · dt / heatCapacity < 0.5    (4-sousední 2D)
```

Pokud max vodivost nutí menší krok než 200 ms (tick simulace), uvnitř kroku udělej N pod-iterací (typicky 1-4 stačí).

### Proč float a ne diskrétně

Energie je „spojitá veličina" v měřítku scény — diskretizace na byte by skončila s `flux = round(k·dt·(T_a-T_b)) = 0` pro malé rozdíly a systém by se zasekl před rovnováhou. Float je default.

### Implementace

```csharp
[BurstCompile]
struct TemperatureStepJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<byte>            material;
    [ReadOnly] public NativeArray<float>           tFront;
    [ReadOnly] public NativeArray<MaterialProps>   mat;
    [WriteOnly] public NativeArray<float>          tBack;
    public int Width, Height;
    public float dt;

    public void Execute(int idx)
    {
        int x = idx % Width;
        int y = idx / Width;
        float tc = tFront[idx];
        var mc = mat[material[idx]];

        float netFlux = 0f;
        netFlux += Flux(x - 1, y, idx - 1,     mc, tc);
        netFlux += Flux(x + 1, y, idx + 1,     mc, tc);
        netFlux += Flux(x, y - 1, idx - Width, mc, tc);
        netFlux += Flux(x, y + 1, idx + Width, mc, tc);

        tBack[idx] = tc + netFlux * dt / mc.heatCapacity;
    }

    float Flux(int nx, int ny, int nIdx, MaterialProps mc, float tc)
    {
        if ((uint)nx >= Width || (uint)ny >= Height) return 0f;
        var mn = mat[material[nIdx]];
        float k = 2f * mc.thermalConductivity * mn.thermalConductivity
                / (mc.thermalConductivity + mn.thermalConductivity + 1e-6f);
        return k * (tFront[nIdx] - tc);
    }
}
```

---

## Algoritmus 2 — Diskrétní hmota / elementy (Margolus)

### Model

Pro prvky v hlíně (a obecně diskrétní hmotu) je float-difuze nevhodná — chceme:
- Skokovou změnu stavu (visually „prvek se objevil/zmizel"),
- **Striktní konzervaci** (žádný drift),
- Nízkou paměť (1 byte/element/cell).

Řešení: **Margolus neighborhood**.

- Per cell integer count (`sbyte`, −128..127) — kolik tokenů elementu má. Signed kvůli debt modelu (viz "Gameplay R/W semantika").
- Step pracuje na 2×2 blocích, NE na jednotlivých buňkách.
- Step `t` použije offset `(0,0)` (bloky `[0..1] × [0..1]`, `[2..3] × [2..3]`, ...).
- Step `t+1` použije offset `(1,1)` (posunuté bloky).
- **V rámci jednoho stepu si bloky nepřekrývají** → plně paralelní per-blok.
- **Suma tokenů uvnitř bloku se nemění** — konzervace strukturálně (ne kvůli implementační pečlivosti).

### Pravidlo přerozdělení v bloku

Příklad pro difuzi (přeskupování směrem k rovnoměrnému rozdělení):

```
Vstup:   blok 4 buněk a, b, c, d s počty (a, b, c, d), součet S = a+b+c+d
Cíl:     rovnoměrně S/4 každému, zbytek S%4 rozdělen deterministicky
Výstup:  každý dostane base = S / 4, zbytky se umístí na první 'rem' pozic
         podle ROTUJÍCÍ priority (viz dále).
```

#### Rotace priority — proč

Naivní fixní pořadí priorit (např. „zbytek vždy do i00") **vytváří diagonální drift**: jeden token v bloku skončí vždy v levém horním rohu bloku, kombinace s alternujícím offsetem (0,0)/(1,1) ho stahuje do (0,0) celé sítě a tam zůstane. Neuchovává se žádná globální homogenita.

Řešení: **priorita rotuje s periodou 4 podle stepu**, takže zbytkový token postupně navštíví všechny 4 pozice bloku:

```
priority[step % 4][0..3] = {
    step%4 == 0: { i00, i10, i11, i01 },
    step%4 == 1: { i10, i11, i01, i00 },
    step%4 == 2: { i11, i01, i00, i10 },
    step%4 == 3: { i01, i00, i10, i11 },
}
```

Tj. první pozice (kam jde první zbytkový token) cykluje `i00 → i10 → i11 → i01` přes 4 stepy, ostatní následují.

**Vlastnost**: jeden token v jinak prázdném okolí trasuje uzavřený 4-cyklus přes 2×2 čtverec buněk. Net drift = 0 přes 4 stepy. Lokální „točení v kruhu" je zachováno (visually neviditelné při normálních hustotách), globální homogenita drží.

#### Gravitační varianta

Pro gravitační hmotu (písek, prvky které mají sedimentovat):

```
1. base = S / 4, rem = S % 4 → základní rovnoměrná difuze.
2. Pokud cell pod buňkou má kapacitu, „přesuň hmotu vertikálně dolů":
   eBack[i01] += min(eBack[i00], capacity_i01 - eBack[i01]);
   eBack[i11] += min(eBack[i10], capacity_i11 - eBack[i11]);
   atd.
```

Pořadí: nejprve difuze (s rotující prioritou), poté gravitace. Konzervace drží — gravitace pouze přesouvá uvnitř bloku.

#### Adaptace pro material change — `canHoldElement`

Aby buňky, které se právě staly stěnou (nebo obecně materiálem, který nesmí nést prvky), **vypudily obsah** do akceptujících sousedů, modifikujeme block rule:

```
sum = a + b + c + d                                  // signed součet, vč. záporných debt tokenů
T   = mat[a].canHoldElement + mat[b].canHoldElement
    + mat[c].canHoldElement + mat[d].canHoldElement  // počet akceptujících (0..4)

if T == 4:
    standard rovnoměrný redistribute (sum/4 + rotující rem)
elif T > 0:
    baseShare = sum / T (signed div), rem = sum - baseShare * T
    rozdej baseShare jen akceptujícím; non-akceptující dostanou 0
    rem distribuuj rotující prioritou jen přes akceptující buňky
else:
    degenerate — všichni 0; obsah uvězněn (viz edge case "sealed wall area")
```

Důsledky:
- Buňka, která změnila materiál na ne-akceptující, po několika stepech konverguje k 0 (i ze záporu).
- Konzervace stále strukturálně platí — sum bloku se mění jen v degenerate `T==0` případě, který explicitně tolerujeme.
- Záporné `sum` (debt) se naředí přes akceptující buňky a difuzí potká `+k` phantom → vyrušení.

### Vlastnost „oscilace na místě, ne propagace"

Kvůli alternujícímu offsetu se token přesune o nejvýš 1 buňku per step. Lokální oscilace ±1 token na rozhraní bloků je možná, ale **falešná propagace do dálky strukturálně nehrozí** — žádný step nevidí token v dvou různých blocích současně.

### Použití

- **Prvky v hlíně** (železo, jíl, voda v půdě, ...) — perfect fit.
- **Padající písek/sníh** — klasický CA.
- **Diskrétní voda** (volitelně místo float fill) — funguje, ale ztrácíš jemnost flow rate. Float fill + flow flux je obvykle lepší pro vodu (viz dále).

### Implementace

```csharp
[BurstCompile]
struct ElementMargolusJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<byte> eFront;
    [WriteOnly] public NativeArray<byte> eBack;
    public int Width, Height;
    public int2 offset;       // (0,0) sudé, (1,1) liché stepy
    public int  stepMod4;     // 0..3, rotace priority

    // Iterujeme přes BLOKY, ne přes buňky.
    // Počet bloků = ((Width - offset.x) / 2) * ((Height - offset.y) / 2)
    public void Execute(int blockIdx)
    {
        int blocksX = (Width  - offset.x) / 2;
        int bx = blockIdx % blocksX;
        int by = blockIdx / blocksX;
        int x0 = offset.x + bx * 2;
        int y0 = offset.y + by * 2;

        int i00 = y0 * Width + x0;
        int i10 = i00 + 1;
        int i01 = i00 + Width;
        int i11 = i01 + 1;

        int sum = eFront[i00] + eFront[i10] + eFront[i01] + eFront[i11];
        int baseShare = sum >> 2;        // sum / 4
        int rem       = sum - baseShare * 4;

        // Rotující priorita: cyklus i00 → i10 → i11 → i01 podle stepMod4.
        // Pořadí indexů v `prio` určuje, kam padají zbytkové tokeny.
        int p0, p1, p2, p3;
        switch (stepMod4)
        {
            case 0:  p0 = i00; p1 = i10; p2 = i11; p3 = i01; break;
            case 1:  p0 = i10; p1 = i11; p2 = i01; p3 = i00; break;
            case 2:  p0 = i11; p1 = i01; p2 = i00; p3 = i10; break;
            default: p0 = i01; p1 = i00; p2 = i10; p3 = i11; break;
        }

        eBack[p0] = (byte)(baseShare + (rem > 0 ? 1 : 0));
        eBack[p1] = (byte)(baseShare + (rem > 1 ? 1 : 0));
        eBack[p2] = (byte)(baseShare + (rem > 2 ? 1 : 0));
        eBack[p3] = (byte)(baseShare);
    }
}
```

> **Pozor**: pokud `Width` nebo `Height` není sudé / není dělitelné po offsetu, na okraji vznikne 1-buňkový pruh, který v daném stepu není v žádném bloku. To je OK — v dalším stepu (s opačným offsetem) se zase chytí. Hlavně **konzervace platí**, protože ten okraj se nepočítá vůbec, ne že by se počítal špatně.

---

## Algoritmus 3 — Kapaliny (lokální tlak + flow)

### Model

Per vodní buňka držíme:
- `fill` (byte 0..255) — kolik vody buňka obsahuje.
- `pressure` (float) — relaxovaný tlakový pole.

Krok má **3 fáze**, sekvenčně:

#### Fáze A — pressure relaxation (1 Jacobi sweep)

```
for each cell c with fill > 0:
    if c je free-surface (cell nad sebou má ~0 fill):
        p_new[c] = 0                              // Dirichlet: hladina = atm
    else:
        sum = 0; n = 0
        for each water-neighbor nb:
            sum += p[nb] + ρ·g·(y_c - y_nb)       // y směřuje nahoru
            n++
        p_new[c] = max(0, sum / n)                // clamp pro kavitaci
```

Tohle je jeden Jacobi sweep Laplaceovy rovnice na grafu propojené vody. Tlak propaguje rychlostí 1 buňka/step. Pro silnější ekvilibraci dej 4-8 sweepů per step (stále lokální, stále Burst-friendly).

**Boundary conditions**:
- **Free surface** (`p = 0`): cell, který má **nad sebou vzduch** = (cell nad ním má `fill ≈ 0` a NENÍ solid). Hysterezi přidej na `fill` práh (free-surface pokud nad sebou má `fill < freeFillMax` a sám má `fill > selfFillMin`), aby drobné fluktuace nepřepínaly stav.
- **Pevná stěna** (material is solid): Neumann — z relaxe vyloučit (sousedem se nepočítá), takže neovlivňuje průměr.
- **Plně sealed objem** (žádná free-surface v propojené komponentě): tlak nemá Dirichlet pin, relaxace udržuje **správný relativní gradient** (hydrostatic), absolutní hladina je dána počátečními podmínkami. To je pro hru obvykle correct chování — sealed nádrž zaplněná pod tlakem si ho pamatuje, sealed objem zaplněný tence ne.

> **Pozor na typický bug**: detekce free-surface jako jen „cell nad sebou má `fill < threshold`" by chybně označila i buňky pod skálou (skála má `fill = 0`). Vrcholová buňka siphonu by se tak stala free-surface s `p = 0` a tlak by neprošel přes hump → siphon nefunguje. Vždy kontroluj **i solid flag**.

#### Fáze B — flow z tlakového gradientu (s compressibility couplingem)

**Klíčový bod**: pure hydrostatic relax z fáze A NEenforce incompressibility. Cells na flow path mají v drainu fill < 255, deficit se nikdy nedoplní (po ucpání díry zamrzne navždy). Fix = **compressibility term** v effective pressure pro flow.

Term je **zobecněný přes per-material targetFill** — stejná rovnice řeší (a) deficit/přebytek vody, (b) gameplay zápis přes/pod 0..255, (c) odtok obsahu z buňky, která změnila materiál (stěna má `targetFill = 0`).

```
p_eff[c] =
    0                                                   (free surface nebo air)
    p[c] + K · (fill[c] - mat[c].targetFill) / 255      (interior; pro vodu targetFill=255,
                                                         stěnu=0, porézní hlínu např. 64)

edge_drive = (p_eff[a] - p_eff[b]) - ρ·g·(y_b - y_a)
flux       = clamp(k_flow · edge_drive · dt, ...)
```

Aplikováno antisymetricky (pull model): každá buňka spočítá `Σ přítoků` od sousedů. Konzervace fillu na úrovni edge strukturální.

**Co dělá zobecněný compressibility term**:
- `fill == targetFill`: term = 0, čisté hydrostatic chování (rovnováha stabilní).
- `fill < targetFill` (deficit): p_eff klesne, sousední cell na cíli má vyšší p_eff → drive doplní deficit. **Lokální refill mechanism.**
- `fill > targetFill` (přebytek): p_eff stoupne → outflow do sousedů. Pokrývá overflow z gameplay zápisu i situaci, kdy se buňka stala stěnou (`targetFill = 0` → veškerý obsah se vytlačí ven).
- `fill < 0` (debt po odebrání): silný negativní p_eff → silný inflow ze sousedů, dluh se postupně splatí.

Po ucpání díry deficit migruje vzhůru k volné hladině; pondu hladina klesne, interior cells se vrátí na fill = targetFill.

**Volba K**:
- Příliš malý → fill drops dramaticky během flow, voda vypadá pružně.
- Příliš velký → stiff numerics, CFL nutí menší dt.
- Sweet spot: `K ≈ rhoG · 20` (5% fill deficit ≈ pressure deficit z 1 cell hloubky).

**CFL**:
- Hydrostatic flow: `k_flow · rhoG · dt < cell_width / 2`
- Compressibility: `k_flow · K · dt < 255 / 2` (aby fill nepřestřelil interval)

**Flux limiting per-edge** (volitelné, pro úplnou mass conservation v transientech):
```
flux = clamp(k_flow · drive · dt,
             -min(fill[b], 255 - fill[a]),    // max inflow do a
             +min(fill[a], 255 - fill[b]))    // max outflow z a
```
Obě strany edge spočítají identicky → konzistentní, žádný per-cell clamp mass loss.

#### Fáze C — vertikální fix volné hladiny

Per sloupec, seshora dolů:
- Pokud `fill > max`: přebytek strč nahoru.
- Pokud nad cellem je voda a sám má kapacitu: pull dolů.

Tohle drží integritu fill-bytů a vytváří ostrou volnou hladinu.

### Co tím získáváš

- **Siphon přes hump funguje** — tlaková spádová křivka přes hump je nakloněná dle hydrostatického rozdílu mezi nádržemi, flow phase pak žene vodu z vyšší do nižší.
- **Pomalá propagace tlakové vlny** — když přidáš úzký vysoký sloupec nad jezerem, tlak na dně sloupce se postupně propaguje do jezera (sekundy/desítky sekund), sloupec klesá, jezero stoupá. Žádné instantní zázraky.
- **Torricelli ven z díry** — automaticky. Na buňce s dírou je `p = ρ·g·h_above`, gradient přes díru je velký, flux je proporcionální.
- **Destrukce stěn tlakem** — porovnej `p` ve vodní buňce u stěny s `MaterialProps.strength`. Stěna prasknutá → cell se stane vodou.

### Limity

- **Kavitace**: clamp `p_new = max(0, ...)` simuluje, že se sloupec „roztrhne" když tlak chce klesnout pod nulu. Realistický siphon přes moc vysoký hump tím přestane fungovat — což odpovídá fyzice.
- **Konvergence** Jacobi sweepu je O(N) hopů přes celé propojené tělo. Pro velké jezero (200×100 cells) by jeden sweep/step znamenal sekundy než se ekvilibruje. Řešení: 4-8 sweepů/step nebo dirty tile.
- **Multigrid** by byl mnohem rychlejší (O(log N)), ale komplexita; zatím ne.
- **Compressible "tlakové vlny"** v kapalině v tomto schématu nejsou — tlak je čistě potenciálové pole bez setrvačnosti. Pro hru OK.
- **Mass loss při transientech**: ve fázi B `fillBack` clampuje na `[0, 255]`. V přechodovém stavu (např. právě otevřená díra do uzavřené trubky) může inflow přerůst kapacitu cíle a clamping ztratí hmotu. V ustáleném stavu žádný net flux → žádný problém. Pokud bude vidět, fix je **flux limiting per edge**: `flux = clamp(k·drive·dt, -min(fill[b], 255-fill[a]), +min(fill[a], 255-fill[b]))`. Obě strany edge spočítají stejně → pull model zůstane konzistentní.

### Implementace — pressure relaxation

```csharp
[BurstCompile]
struct WaterPressureRelaxJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<byte>  fill;
    [ReadOnly] public NativeArray<byte>  material;
    [ReadOnly] public NativeArray<float> pFront;
    [ReadOnly] public NativeArray<MaterialProps> mat;
    [WriteOnly] public NativeArray<float> pBack;
    public int Width, Height;
    public float rhoG;       // ρ * g (pre-mult), per cellHeight unit
    public byte freeFillMax; // hysteresis: cell je free-surface, pokud nad ním < tahle hodnota

    public void Execute(int idx)
    {
        if (fill[idx] == 0) { pBack[idx] = 0f; return; }

        // Free-surface detekce: cell nad sebou má vzduch (low fill AND not solid).
        // Pozor — pouhé `fill[idxUp] < freeFillMax` by chybně chytlo i buňky pod
        // skálou (skála má fill=0) → sealed objem by ztratil tlak, siphon by
        // nefungoval. Solid flag je nutný.
        int x = idx % Width, y = idx / Width;
        int idxUp = idx + Width;
        bool aboveIsAir = (y + 1 < Height)
                       && fill[idxUp] < freeFillMax
                       && (mat[material[idxUp]].flags & MaterialFlags.Solid) == 0;
        bool isFreeSurface = (y + 1 >= Height) || aboveIsAir;
        if (isFreeSurface) { pBack[idx] = 0f; return; }

        float sum = 0f; int n = 0;
        Acc(x - 1, y, idx - 1,     idx, ref sum, ref n,  0);
        Acc(x + 1, y, idx + 1,     idx, ref sum, ref n,  0);
        Acc(x, y - 1, idx - Width, idx, ref sum, ref n, -1);  // cell níž má vyšší y_c - y_nb? naopak: y_c - y_nb = +1 (já jsem výš). Vlastně sousedem dolů jsem JÁ výš, takže (y_c - y_nb) = +1.
        Acc(x, y + 1, idx + Width, idx, ref sum, ref n, +1);

        pBack[idx] = n > 0 ? math.max(0f, sum / n) : 0f;
    }

    void Acc(int nx, int ny, int nIdx, int selfIdx,
             ref float sum, ref int n, int dy_self_minus_nb)
    {
        if ((uint)nx >= Width || (uint)ny >= Height) return;
        if ((mat[material[nIdx]].flags & MaterialFlags.Solid) != 0) return; // solid: Neumann, skip
        // Air (fill == 0) ZAHRNUT s p = 0 — drží se Dirichlet boundary u rozhraní
        // voda↔vzduch (např. čerstvě prokopnutá díra). Bez toho má H 1-tick lag,
        // než hole-cell nasaje fill > 0 a začne přispívat do relaxu.
        float pNb = fill[nIdx] == 0 ? 0f : pFront[nIdx];
        sum += pNb + rhoG * dy_self_minus_nb;
        n++;
    }
}
```

> Konvence `dy = y_self - y_neighbor`: soused dolů má y_nb = y_self - 1, takže `dy = +1`. Když jsem výš než soused, hydrostatický offset se přičítá → můj tlak je vyšší o `ρ·g·1`.

### Implementace — flow phase (pull model)

```csharp
[BurstCompile]
struct WaterFlowJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<byte>  fillFront;
    [ReadOnly] public NativeArray<byte>  material;
    [ReadOnly] public NativeArray<float> pFront;
    [ReadOnly] public NativeArray<MaterialProps> mat;
    [WriteOnly] public NativeArray<byte> fillBack;
    public int Width, Height;
    public float kFlow;
    public float dt;
    public float rhoG;
    public float K;          // bulk modulus pro compressibility coupling
    public byte freeFillMax;

    public void Execute(int idx)
    {
        int x = idx % Width, y = idx / Width;
        if ((mat[material[idx]].flags & MaterialFlags.Solid) != 0)
        {
            fillBack[idx] = fillFront[idx];   // wall: nehne se
            return;
        }

        float pSelf = EffectivePressure(idx, x, y);

        float netInflow = 0f;
        netInflow += FluxFrom(x - 1, y, idx - 1,     pSelf,  0);
        netInflow += FluxFrom(x + 1, y, idx + 1,     pSelf,  0);
        netInflow += FluxFrom(x, y - 1, idx - Width, pSelf, +1);
        netInflow += FluxFrom(x, y + 1, idx + Width, pSelf, -1);

        int newFill = fillFront[idx] + (int)math.round(netInflow);
        fillBack[idx] = (byte)math.clamp(newFill, 0, 255);
    }

    float EffectivePressure(int idx, int x, int y)
    {
        // Air a free-surface: p_eff = 0 (atmospheric BC, no compressibility).
        if (fillFront[idx] == 0) return 0f;
        int idxUp = idx + Width;
        bool aboveIsAir = (y + 1 < Height)
                       && fillFront[idxUp] < freeFillMax
                       && (mat[material[idxUp]].flags & MaterialFlags.Solid) == 0;
        if (y + 1 >= Height || aboveIsAir) return 0f;

        // Interior water: hydrostatic + compressibility.
        return pFront[idx] + K * (fillFront[idx] / 255f - 1f);
    }

    float FluxFrom(int nx, int ny, int nIdx, float pSelf, int dy_self_minus_nb)
    {
        if ((uint)nx >= Width || (uint)ny >= Height) return 0f;
        if ((mat[material[nIdx]].flags & MaterialFlags.Solid) != 0) return 0f;
        float pNb = EffectivePressure(nIdx, nx, ny);
        float drive = (pNb - pSelf) - rhoG * dy_self_minus_nb;
        return math.clamp(kFlow * drive * dt, -64f, 64f);   // limit per step
    }
}
```

> **Konzervace v mezních případech**: clamp `[0, 255]` na fillu a clamp fluxu může drobně rozhodit přesnou konzervaci hmoty (overflow/underflow). Pokud bude vidět drift, řešit přes Margolus blokovou redistribuci v fáze C.

### Vertikální column fix (fáze C)

```csharp
[BurstCompile]
struct WaterColumnFixJob : IJobParallelFor   // paralelní per sloupec
{
    public NativeArray<byte> fill;
    public int Width, Height;

    public void Execute(int x)
    {
        // Zhora dolů: přebytky strč nahoru? Naopak — zhora dolů: pokud cell má vodu
        // a cell pod ním je solid/full ne, zatím nedělej nic (gravitace už proběhla
        // ve flow phase). Tady spíš oprava overflow → posun nahoru.
        for (int y = 0; y < Height; y++)
        {
            int idx = y * Width + x;
            int over = fill[idx] - 255;
            if (over > 0 && y + 1 < Height)
            {
                int idxUp = idx + Width;
                int up = fill[idxUp] + over;
                fill[idxUp] = (byte)math.min(255, up);
                fill[idx]   = 255;
                // (zbytek over by se ztratil; v praxi clampnutý flow přebytky netvoří)
            }
        }
    }
}
```

---

## Pořadí implementace (návrh)

1. **Společný framework** — `CellSimWorld`, double buffer, scheduling, gameplay API. Ověřit s nejjednodušším algoritmem (teplota).
2. **Teplota** — vizualizovat barvou na debug textuře, zahřívat/chladit pomocí gameplay API.
3. **Materiálová tabulka + integrace** — `MaterialProps` indexovaná z `Map`/`Placeable`.
4. **Diskrétní elementy** (Margolus) — druhá simulace přes stejný framework.
5. **Voda** — relaxace, flow, column fix. Nejprve izolovaný Edit Mode test (64×64 svět, vizualizace texturou) než to napojí na zbytek hry.
6. **Optimalizace** — dirty tiles, sub-stepping, případně víc Jacobi sweepů per step.

## Otevřené body

- Jak napojit `material` field na existující `Map` cell data — sdílet, nebo držet nezávislou kopii.
- Jak gameplay objekty vůbec definují svůj zápis (body interest? oblast? per cell?).
- Jak interagují simulace navzájem: teplota → bod tání hmoty → změna fáze (např. led → voda)?
- Vizualizace pro debug a hru (debug textura overlay, herní efekty).
- Persistence: ukládat se mezi savy bude jen `material` + `fill` + `temperature`? Pressure se dopočítá relaxací.
- Konkrétní rozdělení bitů v material bytu (kolik flagů × kolik ID) podle finálního seznamu materiálů a flagů — vyřešit při implementaci frameworku.
