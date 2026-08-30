# Framework pro pohybující se příšery / živočichy

Návrh frameworku pro skládání chování příšer z vyměnitelných modulů. Cíl: kombinovat
**styl pohybu** a **rozhodovací logiku** nezávisle, bez globálního pathfindingu — vše
stojí na **lokálním rozhodování**.

> Stav: návrh (design) zrevidovaný, připravený k implementaci. Implementace po krocích, viz
> [Implementační pořadí](#implementační-pořadí).

## Stav revize — kde jsme skončili

Dokument prošel kritickou revizí (probrat každou featuru: je potřeba? funguje na příkladech?
existuje jednodušší varianta?). Všechny sekce zrevidovány; zbývají jen
[otevřené drobnosti](#otevřené-drobnosti-dořešit-při-implementaci) k dořešení v kódu.

| Sekce | Stav |
|-------|------|
| Organizace tříd (dědičnost stylu × kompozice pravidel) | ✅ zrevidováno |
| Motor (ChLegsArms jako jediný motor) | ✅ zrevidováno |
| Mozek (Controller/Modifier, blackboard, eval) | ✅ zrevidováno |
| Smysly / Perception | ✅ zrevidováno (vč. konfigurace: `SenseConfig` + `SenseProfile` + per-field override) |
| Directive | ✅ zrevidováno |
| Sleep / Wake | ✅ zrevidováno (jeden stav, vstup `EnterSleep(duration)`, branka `Status.CanWake`, časovač přes `ActiveTag`) |
| Status / drives | ✅ zrevidováno (`SleepNeed` poskytuje `Status`; pro v1 stačí) |
| Konfigurační body, mapování příkladů | ✅ zrevidováno |

## Filosofie

- Žádný pathfinding ani globální top-level AI. Přísera vidí jen lokální okolí a reaguje na něj.
- Chování se **skládá z modulů** — stejný styl pohybu lze spárovat s různými rozhodovacími
  pravidly a naopak.
- **Perf-first:** ve velkém světě mohou existovat tisíce příšer. Drtivá většina **spí** a v jednu
  chvíli je probuzených třeba jen ~100. Probuzená přísera ale jede plnou AI **každý frame**.
- **Bez zbytečných abstrakcí.** S motorem (ChLegsArms) pracuje AI **přímo** — široké API
  komponenty je výhoda (snadno přidáme funkcionalitu, máme to pod kontrolou). Nacpat tu flexibilitu
  do interfacu by byl větší problém než užitek.

## Vrstvy a zodpovědnosti

Jeden MonoBehaviour na prefab — **dědičný řetězec** stojící na existujícím stromu `ChLegsArms`:

```
ChLegsArms             „TĚLO"  – jediný motor; desired*, AdjustLegsArms (sdílí i hráč Character3).
 ├─ Character3                   hráč – manuální řízení, žádná AI (beze změny).
 └─ MonsterController   „MOZEK" – abstraktní, VŽDY STEJNÝ framework. Drží blackboard (= veškerý
      │                          běhový stav), eval smyčku, sleep. Na IActiveObject je registrován
      │                          JEN když je přísera probuzená.
      ├─ CrawlerStyle   „CHŮZE" – listová třída = styl pohybu. Overriduje překlad Directive→desired*
      ├─ FlyStyle                 podle terénu/těla. Smí být stavová (config + scratch jako pole).
      └─ SurfaceStyle             Volba stylu = která komponenta je na prefabu.

vedle motoru (data, ne další MonoBehaviour navíc):
Status                 „STAV"  – HP + drives (energie, hlad, spánek). Krmí podmínky pravidel.
IController[]          „ŘÍZENÍ"– prioritizovaný seznam, soupeří o pohybový zámek. [SerializeReference].
IModifier[]            „REFLEXY"– instantní, běží každý frame, neberou zámek. [SerializeReference].
SleepController                – wake/sleep a (de)registrace do per-frame ticku.
```

**Klíč: dvě nezávislé skládací osy, každá jiným mechanismem.**

| Osa | Mechanismus | Proč |
|-----|-------------|------|
| **Styl pohybu** (`CrawlerStyle`, `FlyStyle`…) | **dědičnost** – listová třída řetězce | *Jedna* volba na příšeru (tělo se za běhu nemění), pravé **is-a**, a chce **těsný přístup k motoru** (`desired*`, `map`, `body`) **i k blackboardu** (čte smysly/scratch). Obojí má zadarmo přes `protected`. Přesně případ PRO dědičnost. |
| **Pravidla** (`IController`/`IModifier`) | **kompozice** – `[SerializeReference]` seznamy | Je jich *mnoho*, mix-and-match, pořadí = priorita. Dědičnost by znamenala explozi tříd. Přesně případ PROTI dědičnosti. |

Obě osy zůstávají nezávislé: `CrawlerStyle` jede s hloupým i bohatým seznamem pravidel; pravidla
„utíkej před hráčem, jinak hledej jídlo" jedou nad lezoucí i létající příserou. Styl se „skládá"
výběrem komponenty na prefabu místo `[SerializeReference]`.

> **Proč `MonsterController` jako mezičlánek, ne samostatný MonoBehaviour:** mozek vlastní eval
> smyčku, která každý frame volá `AdjustLegsArms` a čte stav motoru (sleep). Když je to tatáž
> instance, má těsný přístup zadarmo. Samostatná komponenta by si vynutila buď rozšiřování
> veřejného API `ChLegsArms` (zbytečná abstrakce, viz [Filosofie](#filosofie)), nebo `GetComponent`
> drátování. Dědičnost ten šev ruší. `SmallMonster` dnes JE přesně takový styl (podtřída
> `ChLegsArms` s crawl/flip + `WantMove`) → `MonsterController` jen vkládáme mezi něj a bázi.

### Jeden tick

Probuzená přísera dělá **veškerou AI v per-frame ticku** (`IActiveObject`) společně s motorem.
`ChLegsArms` už dělá raycasty a RB síly — pár AI ifů navíc je zanedbatelných. Žádné dělení na
„rychlý gait" a „pomalý mozek". Throttling se neřeší na úrovni pravidel (žádná `Urgency`/`Cadence`
v interfacu), ale na úrovni **dat** (freshness smyslů) a případně přes sdílený `SlowTick` signál,
který si pravidlo přečte samo.

### Rozdělení stavu (skládání + perf)

- **Stavové, s Unity refs + těsně vázané na motor** (`ChLegsArms` → `MonsterController` → styl) →
  **jeden MonoBehaviour** (dědičný řetězec, listová třída = styl). Jeden na prefab, běží
  v existujícím prefab poolingu. Hra nepoužívá Unity `Update()` (jede přes `IActiveObject` smyčku
  v `Game`), takže MB bez magických metod nemá per-frame režii navíc. **Styl smí být stavový** —
  jeho config i scratch (např. `turnTimeout`, `desiredDirection` ze `SmallMonster`) jsou prostě
  pole listové třídy; reset v `AfterMapPlaced` kvůli poolingu.
- **Bezstavové vyhodnocovače** (controllery, modifiery) → **plain C# přes `[SerializeReference]`**
  v seznamech. Nemají vnitřní stav → **nepotřebují vlastní pooling**. Durativní/sdílený běhový stav
  drží **centrální blackboard** na `MonsterController` a `Status`.

---

## Motor: ChLegsArms jako jediný motor

> Revidováno. Původní návrh měl `IMotor` + `MoveIntent` + `LeggedMotor`/`FlyMotor`/`SurfaceMotor`.
> **Zrušeno.** Stavíme na jedné komponentě.

### Zjištění z kódu

`ChLegsArms` je „knihovna, která dělá co se jí řekne". Řídicí plocha jsou pole `desired*`
(`desiredVelocity`, `desiredJump`, `desiredCrouch`, `desiredZMove`, `desiredCatch`, `desiredHold`,
`desiredPickUp`, `holdTarget`…); `GameFixedUpdate` je čistě výkonná smyčka nad nimi a `AdjustLegsArms`
řeší umisťování/odpojování končetin. Žádná AI uvnitř není — rozhodování je v podtřídě
(`SmallMonster`, `Character3`). To je správný řez.

Uvnitř už existují **dva subsystémy** (nohy = indexy 0,1; ruce = 2,3) a **dva pohybové režimy**:
- větev `else` (stojí na nohou): pohon jen horizontálně, vertikálu řeší pružina nohou + skok;
  `desiredVelocity.y` se ignoruje.
- větev `if (ArmCatched)` (visí na rukou): **plný 2D regulátor rychlosti**
  `force = clamp(desiredVelocity − bodyVelocity, maxAcceleration)`.

Ta druhá větev **je** pohon letadla.

### Létání = nový MovementMode (ne nová třída)

Na plnohodnotný let chybí jen dvě věci:
1. **Plný 2D pohon i bez chycených rukou** — podmínku změnit z `if (ArmCatched)` na
   `if (ArmCatched || mode == Free)`. Logika už existuje, jen ji odpoutat od stavu rukou.
2. **Vypnout gravitaci** (`body.useGravity = false` ve `Free` režimu).

Většina nohní mašinerie se ve vzduchu sama vyřadí (`LegOnGround == false` → skok no-op,
`GetLegForce == 0`). Přidáme:

```csharp
public enum MovementMode { Legged, Free }
```

- **`Legged`** — dnešní chování beze změny.
- **`Free`** — gravitace vypnutá („balony"); plný 2D regulátor k `desiredVelocity`; `desiredVelocity = 0`
  znamená hover. **Ruce/chytání/držení/pickup/nošení fungují dál** — letící dravec, co popadne hráče
  a odnese ho, je pak triviální (obrovský reuse, který by samostatný `FlyMotor` zahodil).

> Skutečný „letecký" motor (gravitace zapnutá + aktivní mávání) se přidá **později** jako další
> režim, až bude potřeba. Architektura „jeden motor + MovementMode" to unese.
>
> Po stěnách/stropě (dřív `SurfaceMotor`) je rovněž **režim téhož motoru** — nohy už počítají rotaci
> z normály povrchu (`GetLegRotation`, `legUpDir`); chce to nahradit globální gravitaci přitažlivostí
> k povrchu. Řešit, až bude potřeba.

### Refaktor ChLegsArms (stavíme na ní → smíme ji vylepšit)

- **Zobecnit pevný počet nohou/rukou** — dnes natvrdo 0,1 = nohy, 2,3 = ruce
  (`SelectFreeLeg`/`SelectFreeArm`). Umožnit i „jen ruce" / jiný počet.
- Přidat `MovementMode` + přepínání `body.useGravity`.
- Vyčlenit **public API** pro AI: settery `desired*` (případně tenké metody),
  `Sleep(bool)` (uspí RB, collidery zůstávají), `BlockedThisStep` (narazil → trigger re-decision;
  není nutné do v1 — `SmallMonster` zaseknutí už pozná).
- Při refaktoru zpřehlednit `GameFixedUpdate` (rozdělit pohon / podporu nohou / drag / hold).

---

## Mozek: prioritizovaná pravidla

> Revidováno. Sloučeno `IRule` + `IMonsterAction` do jednoho rozhraní. Zrušeno: `Blocking` flag,
> `Urgency` enum, per-rule `Cadence`, indirekce `GetAction`.

Pravidla jsou dvojího druhu — v **oddělených seznamech** (pořadí = priorita, proto dvě rozhraní):

### A) `IController` — soupeří o pohybový zámek

```csharp
public enum ActionStatus { Running, Done }

public interface IController              // [SerializeReference]
{
    bool Test(in AiContext ctx);          // vstupní podmínka (bezstavová, no-alloc)
    bool CanBeInterrupted { get; }        // smí ho přebít VYŠŠÍ controller (nebezpečí přeruší žraní)
    void Begin(in AiContext ctx);         // jednou při získání zámku (init scratch, animace, instant efekt)
    ActionStatus Tick(in AiContext ctx);  // každý frame dokud drží zámek; Running/Done
    // (později, volitelně) void End(in AiContext ctx, bool completed);  // úklid přerušeného
}
```

V jednu chvíli drží **zámek jen jeden controller**. Tři typy chování pokrývá jedno rozhraní:
- **Instant** (např. `SetDirection`): `Test`→`Begin` udělá efekt→`Tick` vrátí `Done` hned. Zabere
  frame (rozhodl o pohybu), ale zámek nedrží.
- **Durativní one-shot** (bodnutí, hod): drží zámek, `Tick` počítá animaci, `Done` až doběhne.
  Typicky `CanBeInterrupted = false`.
- **Spojité řízení** (útěk): `Tick` si **sám přetestuje** podmínku a vrátí `Done`, až pomine.
  `CanBeInterrupted = true`.

Přirozený invariant: *interruptible* chování jsou skoro vždy bezstavový steering (přerušení = prostě
zahodit, žádný úklid), *non-interruptible* doběhnou. Proto `End()`/cleanup do v1 nepotřebujeme.

### B) `IModifier` — reflexy, neberou zámek

```csharp
public interface IModifier               // [SerializeReference]
{
    bool Test(in AiContext ctx);
    void Apply(in AiContext ctx);         // instantní, každý frame, nikdy nebere zámek
}
```

Modifiery se vyhodnocují **každý frame, i když controller drží zámek**, a **nikdy** zámek neberou
ani nezastaví kaskádu controllerů. Mění *kontext/flagy* (orientace, nálada, odvozený stav), které
pak čtou controllery / styl. Slouží i k **řetězení**: instantní modifier změní kontext a další
pravidla na to navážou. (Vodítko: modifier ať nepřepisuje primární pohybový příkaz, jen kontext —
ať se nepere s controllerem/stylem.) V1 lezoucí příšera má `Modifiers[]` klidně prázdný.

### Vyhodnocovací smyčka (každý frame)

```
// 1) Modifiery — VŽDY všechny, shora dolů
foreach m in Modifiers:
    if m.Test(ctx): m.Apply(ctx)

// 2) Controllery — arbitráž zámku
running = bb.Running                       // běžící controller, nebo null
if running != null && !running.CanBeInterrupted:
    if running.Tick(ctx) == Done: bb.Running = null
else:
    handled = false
    foreach c in Controllers (shora; pokud běží running, jen NAD ním):
        if c.Test(ctx):
            c.Begin(ctx)
            bb.Running = (c.Tick(ctx) == Running) ? c : null
            handled = true; break          // první, kdo zabere, vyhrává — nižší priorita NEpřebíjí
    if !handled && running != null:        // nikdo vyšší nezabral → pokračuj v běžícím
        if running.Tick(ctx) == Done: bb.Running = null

// 3) Pohyb
if bb.CurrentDirective.Kind != Manual:
    ApplyDirective(in bb.CurrentDirective)     // virtual; styl (listová třída) přeloží na desired*
// ChLegsArms.GameFixedUpdate provede desired*
```

**Controllery se neřetězí** (první, kdo projde `Test`, zabere frame — i instantní). Řetězení je
úkol modifierů. Tím zůstává priorita zachovaná.

---

## Blackboard (centrální stav na MonsterController)

Klíčové pozorování: protože **v jednu chvíli běží jen jeden controller**, durativní stav nepotřebuje
per-pravidlo úložiště — stačí **jedna sdílená sada „scratch" polí**. Tři jasně oddělené regiony:

| Region | Kdo zapisuje | Co | Životnost |
|--------|--------------|----|-----------|
| **Perception cache** | smysly (lazy gettery) | `SenseResult` per typ (pevná pole) | persistentní, freshness řízená pravidlem |
| **Action scratch** | běžící controller | timer, fáze, cílový `Label`/pozice | jen po dobu běhu controlleru |
| **Motor command** | controller / styl | `CurrentDirective` (+ `Manual`) | standing (trvá, dokud se nezmění) |

Drives jsou ve `Status`, stav končetin v `ChLegsArms` — **neduplikujeme**. Plus ukazatel na
`Running` controller.

`Begin` inicializuje scratch (timer, *zachytí cíl do scratche*). `Tick` scratch aktualizuje
(`timer -= dt`). Na `Done` se scratch uvolní pro další.

---

## Smysly / Perception

> Revidováno. Throttling je zodpovědnost **pravidla** (zná důležitost informace), ne smyslu (zná
> jen fakt). Smysly jsou **lazy** — pod blokujícím vyšším pravidlem se nižší `Test` ani nezavolá,
> takže se smysl vůbec nespočítá.

### Princip: netrottluj test, trottluj data

Smysl zjistí fakt a uloží `(hodnota, timestamp)`. Konzument (pravidlo) řekne, jak staré to smí být:

```csharp
var food = ctx.Sense(SenseId.Food, maxAgeMs: 500);  // je-li cache starší, přepočítá teď
```

- **Sdílení:** dvě pravidla čtou stejný `ctx.Sense(...)` → spočítá se max 1× za potřebné okno,
  ostatní převezmou. Vlastníkem dotazu je getter, ne test. Nejpřísnější konzument (nejmenší
  `maxAge`) fakticky určuje refresh rate.
- **Reakce každý frame:** test čte cache levně každý frame; throttluje se jen *drahý sken* uvnitř
  getteru. Urgentní data → malé `maxAge`; jídlo → velké.
- **Vnitřně-pomalá rozhodnutí** („mám jít spát?"): pravidlo se zhradí sdíleným `ctx.SlowTick`
  (true ~1×/0.5 s) — žádná pole per-rule.

### `SenseResult`

```csharp
public struct SenseResult
{
    public float Timestamp;   // Time.time posledního výpočtu → freshness
    public bool Found;
    public Vector2 Position;   // kde / směr
    public Label Target;       // objekt (volitelné)
    public float Value;        // vzdálenost / množství / skóre — význam dle smyslu
}
```

- **Paměť:** blackboard má **pevná pole pro všechny typy smyslů ve hře** (typů bude jednotky →
  paměťově nula; žádný dictionary/alokace; cache-friendly). „Mrtvá" pole u nepoužitého smyslu nic
  nestojí.
- **Pooling:** `Target` (`Label`) může být mezitím poolnut/zničen — před použitím **ověřit živost**
  (freshness to z větší části řeší, ale ne vždy).
- **Přímé dotazy:** speciální/komplexní jednorázový dotaz smí pravidlo udělat napřímo
  (`ctx.Map.Get(...)`) bez smyslové abstrakce. Smysly jsou jen pro sdílená, opakovaná, cachovatelná
  fakta. Přímý dotaz v `Test` (běží každý frame) ať se zhradí `SlowTick`em.

### Konfigurace smyslů (data, ne kód)

Smysl = **výpočet + parametry**. Drtivá většina = jeden výpočet „nejbližší objekt daného `Ksid`
v okruhu R" (vzor `InventorySearcher`: `map.Get(...)` / `Secondary().Get(...)`). Liší se jen `Ksid`
a dosah („Potrava" = `Plant` u býložravce, `Prey` u dravce; „Nebezpečí" = jiný `Ksid`/dosah u každého).
Druhů výpočtu jsou jednotky → **enum + centrální `switch`** (ne delegáti, ne polymorfní objekty):

```csharp
public enum SenseId   { Food, Danger, Prey, Mate, Home, /*…*/ }   // fixní sloty do SenseResult[]
public enum SenseKind { None, NearestKsid, NearestKsidSec /* sekundární mapa */ }

[Serializable]
public struct SenseConfig
{
    public SenseId  Slot;     // který slot plní
    public SenseKind Kind;    // který výpočet (switch)
    public Ksid     Ksid;     // co hledá
    public float    Radius;   // dosah
    // doplňkové parametry přidáváme SEM stejným stylem; default (0/None/null) = „nezadáno"
}
```

Plochý `struct` (ne `[SerializeReference]`): no-alloc, cache-friendly, sedí na „fixní pole pro
všechny typy"; override hodnoty je triviální. Exotický smysl → nový `SenseKind`, který nepotřebná
pole ignoruje, nebo rozšířit struct (mrtvá pole jsou zdarma).

### Sdílení + override (default / shared / per-field)

Dvě vrstvy skládané **kompozicí** (ne dědičností settingů):

```csharp
[CreateAssetMenu]
public class SenseProfile : ScriptableObject { public SenseConfig[] Senses; }  // sdílený archetyp

// v MonsterSettings:
public SenseProfile  SenseProfile;     // sdílený základ (default)
public SenseConfig[] SenseOverrides;   // jen odchylky
```

**Override je per-položku konfigurace, ne per-slot:** v override záznamu se uplatní **jen nedefaultní
pole**, ostatní si nechají hodnotu z profilu. Tím lze odladit jen dosah a `Ksid` zdědit — a stejně
tak nové doplňkové parametry: starý config je nechá default a override se jich nedotkne.

```csharp
// resolve: jednou, lazy, cache na MonsterSettings (sdíleno všemi instancemi druhu)
var resolved = new SenseConfig[SenseCount];                 // indexed by (int)Slot
foreach (var c in SenseProfile.Senses)  resolved[(int)c.Slot] = c;
foreach (var o in SenseOverrides) {                          // merge per-field, jen nedefaultní
    ref var b = ref resolved[(int)o.Slot];
    if (o.Kind   != SenseKind.None) b.Kind   = o.Kind;
    if (o.Ksid   != default)        b.Ksid   = o.Ksid;
    if (o.Radius != 0)              b.Radius = o.Radius;
}
```

Praktický dopad — designér skoro nikdy nekonfiguruje vše:

| Příšera | `SenseProfile` | `SenseOverrides` |
|---------|----------------|------------------|
| Zajíc | `HerbivoreSenses` | — |
| Srnec | `HerbivoreSenses` | `Danger.Radius 12→18` |
| Vlk | `PredatorSenses` | — |

(`HerbivoreSenses = { Food: NearestKsid Plant r8; Danger: NearestKsidSec Predator r12 }`.) Změna
v profilu se propíše všem; výjimka je jeden řádek override.

### Runtime — lazy do slotu

Blackboard drží **per-instance** `SenseResult[] senseCache` + ref na sdílený `resolved`:

```csharp
public ref readonly SenseResult Sense(SenseId id, float maxAgeMs)
{
    ref var r = ref senseCache[(int)id];
    if (Time.time - r.Timestamp <= maxAgeMs * 0.001f) return ref r;   // čerstvé → cache
    ref readonly var cfg = ref resolved[(int)id];
    switch (cfg.Kind) {
        case SenseKind.NearestKsid:    EvalNearestKsid(cfg, secondary:false, ref r); break;
        case SenseKind.NearestKsidSec: EvalNearestKsid(cfg, secondary:true,  ref r); break;
        default:                       r.Found = false; break;
    }
    r.Timestamp = Time.time;
    return ref r;
}
```

**Editor validace:** varuj, když má profil dva záznamy na stejný `Slot`, nebo když pravidlo čte
`SenseId` s `Kind == None` v resolved tabulce.

---

## Directive — adaptér AI → motor

> Revidováno. **Není** datový typ kolující AI (žádný controller nečte cizí `Kind`). Je to
> **standing příkaz pohybu v blackboardu**, který styl každý frame překládá na `desired*`. Vypadl
> ze signatur `Test`/`Tick`.

```csharp
public enum DirectiveKind { Roam, GoDirection, GoToward, Stop, Manual }

public struct Directive
{
    public DirectiveKind Kind;
    public int Direction;     // -1/+1 pro lezce
    public Vector2 Target;    // pro GoToward
    public float SpeedScale;  // 0..1 – zrychlí (strach) / zpomalí (únava)
}
```

Dva způsoby, jak controller řídí pohyb:
1. **`ctx.SetDirective(...)`** — vyšší-úrovňový příkaz; **styl ho překládá na `desired*` podle terénu**
   (lezec hlídá díry/hrany, balon steeruje/odráží). Tohle je ten odpojovací šev, kvůli kterému
   framework existuje — controller řekne *co*, styl rozhodne *jak* podle těla. Překlad je
   `protected virtual` metoda na `MonsterController` (např. `ApplyDirective(in Directive)`), kterou
   listová třída stylu overriduje — má tak přímý přístup k `desired*`, `map`, `body` i blackboardu.
2. **`desired*` napřímo přes `ctx.Ctrl`** (= motor, je to `ChLegsArms`) — přesné řízení (ruka při
   bodnutí). Controller pak nastaví `Kind = Manual`; smyčka ten frame přeskočí `ApplyDirective`.

**Standing = kontinuita zadarmo:** controller nastaví directive obvykle v `Begin` (cíl do scratche);
`Tick` může být skoro prázdný — styl pokračuje v rozkazu každý frame sám (jede k uloženému cíli).
Přepočet jen když se něco změní (cíl se hnul, `BlockedThisStep`). I frame, kdy žádný controller
„nepřevezme", jede přísera dál podle standing directive — pohyb se netrhá.

### `AiContext` (no-alloc)

```csharp
public readonly ref struct AiContext   // předává se odkazem
{
    public readonly MonsterController Ctrl;   // blackboard + (jako ChLegsArms) i motor — táž instance
    public readonly Status Status;
    public readonly bool SlowTick;
    // Ctrl IS-A ChLegsArms → desired*/MovementMode napřímo; Sense(SenseId, maxAgeMs), Map,
    // SetDirective(...) — helpery. (Pravidla jsou samostatné [SerializeReference] objekty, ref potřebují.)
}
```

---

## Sleep / Wake

> Revidováno. Spánek je **chování i perf nástroj** zároveň. Funkcionalitu mají **všechny příšery**,
> liší se jen parametry (v `MonsterSettings`).

Cíl: tisíce příšer existuje, ~100 je probuzených. **Spící přísera je inertní z hlediska AI** — není
na žádném per-frame ani cyklickém listu. **RB neuspáváme my** — příšera zůstává v Unity fyzice
a Unity si rigidbody uspí samo, jakmile je v klidu (collidery zůstávají → dá se do ní střelit / sežrat
ji). Spící příšera **nemá žádný tick**: `SleepNeed` se počítá lazy z timestampu a probuzení řeší
jeden naplánovaný `Timer`. Zůstává jen záznam na sekundární mapě.

**Ve spánku se příšera ničeho nedrží** (pustí končetiny) → proto musí dobrovolný spánek proběhnout
na bezpečném místě (lezec tam, kde po puštění nespadne).

### Jeden stav, jeden vstup, jedna branka

Spánek je **úplně jeden mechanismus**. Liší se **jediným parametrem — délkou spánku**, kterou určuje
buď AI, nebo kód kolapsu. Žádné `wakeable`/`forced` flagy, žádné lehčí/hlubší spaní.

```csharp
void EnterSleep(float duration)                   // JEDINÝ vstup do spánku
{
    Motor.DropAllLimbs();                          // ve spánku nic neudrží (RB nech Unity uspat samo)
    Game.Instance.DeactivateObject(ctrl);          // pryč z per-frame i cyklických listů
    sleepStart = Time.time; needAtStart = Status.SleepNeed;
    this.Plan(duration);                            // ISimpleTimerConsumer.Plan → bump ActiveTag
}

bool TryWake()                                     // JEDINÁ branka — všechny eventy (časovač i vnější) přes ni
{
    if (!Status.CanWake(CurrentNeed())) return false;   // ← rozhoduje STATUS podle SleepNeed
    Status.SleepNeed = CurrentNeed();              // lazy doúčtování
    Game.Instance.ActivateObject(ctrl);            // AI se vrací; přechod do stavu Awake (bump ActiveTag
    return true;                                    //   ⇒ pending wake-časovač se vyřadí sám)
}

void OnTimer() => TryWake();                        // časovač jen zkusí bránu (jako každý event)
float CurrentNeed() => needAtStart - cfg.RecoverRate * (Time.time - sleepStart);  // klesá ve spánku
```

**„Nelze probudit při kolapsu" není flag — vypadne to ze `Status`.** `Status.CanWake(need)` vrací
`need <= cfg.WakeableLevel`. Dobrovolný spánek příšera začíná s `SleepNeed` pod tou hladinou →
probuditelná hned. Kolaps začíná v extrému nad ní → branka drží zavřeno, dokud spaním neklesne. Stejná
branka, stejný kód, dvě chování čistě z hodnoty `SleepNeed`.

**Časovače se ruší přechodem stavu, ne ručně** — využití `Timer.Plan` přes `ISimpleTimerConsumer`:
`Plan` zvedne `ActiveTag`, takže jakýkoli dřív naplánovaný `OnTimer` se při probuzení (= další `Plan`
/ přechod do Awake) sám zahodí (vzor `TimerHandler`). Žádné `CancelWakeTimer()`.

### Dva spouštěče `EnterSleep` (jediná odlišnost)

**1) Dobrovolně — AI pravidlo `Sleep : IController`** (jedna třída, víc instancí v `Controllers[]`,
priorita pořadím: NightSleep > Nap > MicroPause; tři délky):

```csharp
[Serializable]
class Sleep : IController
{
    public float NeedThreshold;   // od jaké únavy
    public DayWindow When;        // časové okno (noc / den / kdykoli pro micro)
    public float Duration;        // ← délka spánku (jediný parametr)
    public bool  NeedSafeSpot;    // micro-pauza nemusí

    public bool Test(in AiContext ctx) =>
        ctx.Status.SleepNeed >= NeedThreshold
        && When.Contains(ctx.TimeOfDay)
        && (!NeedSafeSpot || ctx.Ctrl.IsSafeToSleep());   // ← styl-specifický check
    public bool CanBeInterrupted => true;                 // nebezpečí spánek přeruší (vyšší controller přebije)
    public void Begin(in AiContext ctx) => ctx.Ctrl.Sleep.EnterSleep(Duration);
    public ActionStatus Tick(in AiContext ctx) => ActionStatus.Done;  // další frame už není na listu
}
```

Bezpečné místo řeší běžná AI — `IsSafeToSleep()` je jen *Test gate*; styl už averzí k dírám drží
příšeru na stabilní zemi, jinak `Roam` posouvá dál. Žádný zvláštní pathfinding.

**2) Nedobrovolně — kolaps silou z `MonsterController`**, mimo arbitráž zámku (musí přebít i běžící
*non-interruptible* akci — útěk, bodnutí). Není to rozhodnutí, ale fyziologická pojistka před eval smyčkou:

```csharp
// MonsterController tick (jen když je probuzená — spící není na per-frame listu)
Status.TickSleepNeed(dt);                          // roste, rychleji při aktivitě
if (Status.SleepNeed >= cfg.CollapseThreshold)     // kdekoli — i visíc na stromě, i u predátora
    { EnterSleep(Status.RecoverDuration()); return; }   // délku = čas zotavení pod WakeableLevel
// ... teprve pak modifiery + arbitráž controllerů
```

> Pozor na názvy: **`Sleep`** = AI pravidlo (rozhodne); **`SleepController`** = lifecycle helper na
> `MonsterController` (vykoná, je `ISimpleTimerConsumer`). Zůstává jediným místem sahajícím na
> `Game.Instance.ActivateObject/Deactivate`.

### Probouzení — všechny zdroje přes `TryWake()`

- **Časovač** — naplánovaná délka / čas zotavení; `OnTimer → TryWake()`.
- **Predátor (pull)** — aktivní agent dál dělá `Secondary(Beasts).Get(...)` a volá `TryWake()`.
- **Zásah** — `ReduceHealth` na spící příšeře zavolá `TryWake()` (funguje nezávisle na fyzice).

Všechny tři dělají totéž: zeptají se branky. Dobrovolný spánek probudí (uteče / vstane); **kolaps
branka odmítne, dokud se příšera nezotaví** → leží dál, takže ji predátor sežere nebo se zabije pádem,
aniž se probudí. (Zásah kolabovanou příšeru zabije, ale neprobudí — `Status.CanWake` drží zavřeno.)

### Sekundární mapa (už existuje)

`MapSecondary` (`SecondaryMapScale = 9` → buňky 9× větší) umožňuje **levné dotazy na velké
vzdálenosti**. Existuje pojmenovaná `SecondaryMap.Beasts`. Přísery se na ni registrují přes
`PlaceableSettings.SecondaryMapIndex` (jako pointy). Dotaz:
`map.Secondary(SecondaryMap.Beasts).Get(list, center, size, ksid, tag)` (vzor `InventorySearcher`).

Přísera **netestuje okolí sama** (spí → žádný tick); budí ji aktivní agent přes `TryWake` (viz výše).
**Kaskáda spánku:** když predátor sám usne, přestane budit → okolní příšery můžou taky usnout;
wakefulness se šíří jen z aktivních agentů.

> Push varianta (predátor jako `Trigger` na `SecondaryMap.Beasts`, probouzení přes
> `NewObjectsEvent`) je možné rozšíření, kdyby pull dotazy byly úzké hrdlo.

## Status / drives

> Revidováno. **`Status` poskytuje velikost potřeby spánku** (`SleepNeed`) — je vstupem pro `Sleep`
> pravidlo i pro kolaps. (HP a `ReduceHealth` zůstávají.)

Rozšířit `Status` o normalizovaný `SleepNeed` (0..1) a **branku probuzení**:
- **`TickSleepNeed(dt)`** — volá `MonsterController` každý frame, dokud je probuzená (roste,
  rychleji při aktivitě). Ve spánku se nepočítá průběžně, ale **lazy** z timestampu
  (`SleepController.CurrentNeed()`, klesá `RecoverRate`).
- **`CanWake(need)`** = `need <= WakeableLevel` — jediné kritérium probuzení. Z něj vypadne
  „kolaps nelze probudit" (začíná nad hladinou) i „dobrovolný spánek lze hned" (začíná pod ní).
- **`RecoverDuration()`** = `(SleepNeed − WakeableLevel) / RecoverRate` — délka spánku při kolapsu.
- Prahy v `MonsterSettings` (per druh, jinak stejný kód): `CollapseThreshold` (~1.0),
  `WakeableLevel` (~0.7), `RecoverRate`, růstová rychlost; per `Sleep` instanci
  `NeedThreshold`/`When`/`Duration`.

Pro v1 reálně stačí **`SleepNeed`**; `Energy`/`Hunger` odložit, dokud nebudou pravidla, která je čtou.

---

## Konfigurační body

| Kde | Co se ladí |
|-----|-----------|
| volba podtřídy stylu na prefabu (`CrawlerStyle`/`FlyStyle`/…) | **styl pohybu** – žádný serializovaný odkaz, vybírá se která komponenta je na prefabu |
| `ChSettings` (už existuje) | fyzika motoru – speed, accel, jump, nohy/ruce, `MovementMode` |
| serializovaná pole stylu (na listové komponentě) | ladění stylu – averze k dírám, steering, odraz… |
| nový `MonsterSettings : ScriptableObject` | spánek (`CollapseThreshold`, `WakeableLevel`, `RecoverRate`, růst `SleepNeed`), wake radius/perioda, `SenseProfile` + `SenseConfig[] SenseOverrides`, `[SerializeReference] IController[] Controllers` (vč. konfig. `Sleep` instancí), `[SerializeReference] IModifier[] Modifiers` (sdílitelný „rule set" napříč těly) |
| `SenseProfile : ScriptableObject` | sdílený archetyp smyslů (`SenseConfig[]`); per-monster override per-field v `MonsterSettings` |
| `PlaceableSettings.SecondaryMapIndex` | `Beasts` — registrace na sekundární mapu pro probouzení |
| `Ksid` | typy pro Perception/probouzení: např. `Beast`, `Danger`, `Food`, `Prey` |
| per-rule serializovaná pole | prahy podmínek, váhy, `maxAge` pro smysly |

## Mapování na příklady příšer

(„styl" = listová podtřída `MonsterController` na prefabu; „pravidla" = `[SerializeReference]` seznamy.)

- **Lezoucí (dnešní `SmallMonster`)** → `CrawlerStyle` (`Legged`, averze k dírám/nebezpečí) +
  minimální seznam controllerů, prázdné modifiery.
- **Po povrchu i hlavou dolů** → `SurfaceStyle` (`Legged` s povrchovou „gravitací"), jeden směr,
  otočka na nebezpečí.
- **Létající balon / kulečník** → `BounceStyle` (`Free`, gravitace off, odraz).
- **Létající dravec** → `FlyStyle` (`Free`) + ruce (chytí a odnese kořist) + bohatší controllery.
- **S rozhodováním** → kterýkoli styl + bohatší seznam pravidel (později candidate scoring).

### Připraveno na později: candidate scoring

Style naplní **statický bufr** kandidátních lokací (vzor `armCandidates` v `ChLegsArms`) a nechá
mozek je ohodnotit součtem `IScorer` pravidel; vrátí nejlepší. Kontrakt se navrhne tak, aby
`IController` a `IScorer` mohly koexistovat.

## Konvence (viz [conventions.md](conventions.md))

- **No-alloc:** podmínky/akce bez alokací; kandidáti přes statické bufry. `AiContext` je `ref struct`.
- **Pooling:** init v `Awake`, ne `AfterMapPlaced`; reset blackboardu v `AfterMapPlaced`/`Cleanup`.
  `SenseResult.Target` ověřovat na živost (pooling).
- **KSID místo `is`/`GetComponent`** pro herní interakce (`IsChildOf`/`IsChildOfOrEq`).
- Nové `.cs` pod `Assets/Scripts` → přidat `Compile Include` do `MainGameAsm.csproj`.

---

## Otevřené drobnosti (dořešit při implementaci)

Hlavní návrh je zrevidovaný. Zbývají drobnosti, které se dořeší v kódu:

1. **`TimeOfDay`** — zdroj denní doby pro `Sleep.When` (existuje ve hře denní cyklus, nebo zavést
   prostý `Game` čítač?).
2. **`IsSafeToSleep()`** — přesný style-specifický check (lezec: na zemi + ne na hraně; příp. žádné
   akutní nebezpečí).
3. **Smysly** — `enum SenseId` finální seznam + `SenseCount`; signatura `EvalNearestKsid`.
4. Případně `End()`/cleanup u controllerů (zatím odloženo, viz Mozek).

## Implementační pořadí

1. **Refaktor `ChLegsArms`** — zobecnit počet nohou/rukou, přidat `MovementMode` (`Legged`/`Free`,
   gravitace off), public API pro AI (`desired*` settery, `DropAllLimbs`). Ověřit, že `Free` lítá
   (balon). RB neuspáváme — to dělá Unity samo.
   > Rozpracováno v [chlegsarms-refactor.md](chlegsarms-refactor.md): kýbl A (zpřehlednění)
   > je hotový, kýbl B (rozdělení GameUpdate/FixedUpdate + oprava hodu) a kýbl C
   > (`MovementMode`, generalizace končetin, `MonsterController`) čekají.
2. **`MonsterController : ChLegsArms`** (abstraktní) — blackboard (3 regiony), `Directive` standing,
   eval smyčka (modifiery + arbitráž zámku), napojení na `IActiveObject`, `virtual ApplyDirective`.
3. **`CrawlerStyle : MonsterController`** — port `SmallMonster` crawl/flip + `WantMove` do override
   `ApplyDirective` (překlad `Directive` → `desired*`); styl-state jako pole, reset v `AfterMapPlaced`.
4. **`IController` / `IModifier`** + pár konkrétních pravidel a akcí; `Defend`, `SetDirection`,
   `SeekFood`, `FleePlayer`, `Roam`, `Sleep`.
5. **Perception** — `SenseResult` + `SenseConfig`/`SenseKind` switch, `SenseProfile` + per-field
   override resolve, lazy `Sense(id, maxAge)` s freshness, `SlowTick`.
6. **`Status.SleepNeed`** (+ `CanWake`/`RecoverDuration`) + **`SleepController`** (`ISimpleTimerConsumer`)
   — jeden `EnterSleep(duration)`, branka `TryWake`, centrální kolaps-guard v `MonsterController`,
   lazy `CurrentNeed`, wake přes `Timer.Plan` (rušení časovače `ActiveTag`em).
7. **`MonsterSettings`** ScriptableObject; sestavení prvních příšer z modulů.
8. (později) `BounceStyle`, povrchový režim, candidate scoring, push-trigger probouzení, `End()` cleanup.
