# Framework pro pohybující se příšery / živočichy

Návrh frameworku pro skládání chování příšer z vyměnitelných modulů. Cíl: kombinovat
**styl pohybu** a **rozhodovací logiku** nezávisle, bez globálního pathfindingu — vše
stojí na **lokálním rozhodování**.

> Stav: návrh zrevidovaný, **implementace probíhá** — kroky 1–3 hotové, featura
> [Sbírání předmětů](#featura-sbírání-předmětů) naimplementovaná (smysly, `SeekItemRule`,
> `PickUpItemRule`, inventář příšery; krok C čeká na test v editoru), viz [Implementační pořadí](#implementační-pořadí). Při implementaci padla rozhodnutí, která
> návrh upřesňují (jména, zrušený `AiContext`, `SlowTick(period)`, cíl jen jako pozice…) — dokument
> je už obsahuje.

## Stav revize — kde jsme skončili

Dokument prošel kritickou revizí (probrat každou featuru: je potřeba? funguje na příkladech?
existuje jednodušší varianta?). Všechny sekce zrevidovány; zbývají jen
[otevřené drobnosti](#otevřené-drobnosti-dořešit-při-implementaci) k dořešení v kódu.

| Sekce | Stav |
|-------|------|
| Organizace tříd (dědičnost stylu × kompozice pravidel) | ✅ zrevidováno |
| Motor (ChLegsArms jako jediný motor) | ✅ zrevidováno |
| Mozek (Rule/Modifier, blackboard, eval) | ✅ zrevidováno |
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
 └─ MonsterBrain        „MOZEK" – abstraktní, VŽDY STEJNÝ framework. Drží blackboard (= veškerý
      │                          běhový stav), eval smyčku, sleep. Na IActiveObject je registrován
      │                          JEN když je přísera probuzená.
      ├─ CrawlerStyle   „CHŮZE" – listová třída = styl pohybu. Overriduje překlad Directive→desired*
      ├─ FlyStyle                 podle terénu/těla. Smí být stavová (scratch jako pole).
      └─ SurfaceStyle             Volba stylu = která komponenta je na prefabu.

vedle motoru (data, ne další MonoBehaviour navíc):
Status                 „STAV"   – HP + drives (energie, hlad, spánek). Krmí podmínky pravidel.
AiSettings             „DRUH"   – ScriptableObject: seznamy pravidel + konfigurace stylu.
  IAiRule[]            „ŘÍZENÍ" – prioritizovaný seznam, soupeří o pohybový zámek. [SerializeReference].
  IAiModifier[]        „REFLEXY"– instantní, běží každý fixed krok, neberou zámek. [SerializeReference].
SleepController                 – wake/sleep a (de)registrace do per-frame ticku.
```

**Klíč: dvě nezávislé skládací osy, každá jiným mechanismem.**

| Osa | Mechanismus | Proč |
|-----|-------------|------|
| **Styl pohybu** (`CrawlerStyle`, `FlyStyle`…) | **dědičnost** – listová třída řetězce | *Jedna* volba na příšeru (tělo se za běhu nemění), pravé **is-a**, a chce **těsný přístup k motoru** (`desired*`, `map`, `body`) **i k blackboardu** (čte smysly/scratch). Obojí má zadarmo přes `protected`. Přesně případ PRO dědičnost. |
| **Pravidla** (`IAiRule`/`IAiModifier`) | **kompozice** – `[SerializeReference]` seznamy | Je jich *mnoho*, mix-and-match, pořadí = priorita. Dědičnost by znamenala explozi tříd. Přesně případ PROTI dědičnosti. |

Obě osy zůstávají nezávislé: `CrawlerStyle` jede s hloupým i bohatým seznamem pravidel; pravidla
„utíkej před hráčem, jinak hledej jídlo" jedou nad lezoucí i létající příserou. Styl se „skládá"
výběrem komponenty na prefabu místo `[SerializeReference]`.

> **Proč `MonsterBrain` jako mezičlánek, ne samostatný MonoBehaviour:** mozek vlastní eval
> smyčku, která každý frame volá `AdjustLegsArms` a čte stav motoru (sleep). Když je to tatáž
> instance, má těsný přístup zadarmo. Samostatná komponenta by si vynutila buď rozšiřování
> veřejného API `ChLegsArms` (zbytečná abstrakce, viz [Filosofie](#filosofie)), nebo `GetComponent`
> drátování. Dědičnost ten šev ruší. `SmallMonster` byl přesně takový styl (podtřída
> `ChLegsArms` s crawl/flip + `WantMove`) → `MonsterBrain` se vložil mezi něj a bázi a ze
> `SmallMonster` se stal `CrawlerStyle`.

### Jeden tick

Probuzená přísera dělá **veškerou AI v ticku** (`IActiveObject`) společně s motorem — **ve fixed
kroku (50 Hz)**, ne v `Update`: motor nesmí záviset na frame rate a rozhodování z něj nemá smysl
odpojovat (navíc deterministické, `dt` konstanta). `ChLegsArms` už dělá raycasty a RB síly — pár AI
ifů navíc je zanedbatelných. Žádné dělení na „rychlý gait" a „pomalý mozek".

```
MonsterBrain.GameFixedUpdate:  Think() → ApplyDirective() → AdjustLegsArms() → base (síly)
```

Throttling se neřeší na úrovni pravidel (žádná `Urgency`/`Cadence` v interfacu), ale na úrovni
**dat** (freshness smyslů) a přes **`brain.SlowTick(period)`**, který si pravidlo přečte samo:

```csharp
public bool SlowTick(int period) => (Game.Instance.FixedStepCounter + slowOffset) % period == 0;
```

Periodu volí pravidlo (zná důležitost informace; 25 = ~2×/s). `slowOffset` dostane každá přísera
v `AfterMapPlaced` z globálního čítače → ~100 probuzených příšer **nedělá drahý sken ve stejném
kroku** (jinak pravidelný spike).

### Rozdělení stavu (skládání + perf)

- **Stavové, s Unity refs + těsně vázané na motor** (`ChLegsArms` → `MonsterBrain` → styl) →
  **jeden MonoBehaviour** (dědičný řetězec, listová třída = styl). Jeden na prefab, běží
  v existujícím prefab poolingu. Hra nepoužívá Unity `Update()` (jede přes `IActiveObject` smyčku
  v `Game`), takže MB bez magických metod nemá per-frame režii navíc. **Styl smí být stavový** —
  jeho scratch (např. `turnTimeout`, `direction` v `CrawlerStyle`) jsou prostě pole listové třídy;
  reset v `Cleanup` kvůli poolingu (hodnotu z prefabu přes `placeable.Prototype`, vzor
  `Placeable.Cleanup`). **Konfigurace stylu ale patří do `AiSettings`** (viz
  [Konfigurační body](#konfigurační-body)).
- **Bezstavové vyhodnocovače** (pravidla, modifiery) → **plain C# přes `[SerializeReference]`**
  v seznamech. Nemají vnitřní stav → **nepotřebují vlastní pooling**. Durativní/sdílený běhový stav
  drží **centrální blackboard** na `MonsterBrain` a `Status`.

---

## Motor: ChLegsArms jako jediný motor

> Revidováno. Původní návrh měl `IMotor` + `MoveIntent` + `LeggedMotor`/`FlyMotor`/`SurfaceMotor`.
> **Zrušeno.** Stavíme na jedné komponentě.

### Zjištění z kódu

`ChLegsArms` je „knihovna, která dělá co se jí řekne". Řídicí plocha jsou pole `desired*`
(`desiredVelocity`, `desiredJump`, `desiredCrouch`, `desiredZMove`, `desiredCatch`, `desiredHold`,
`desiredPickUp`, `holdTarget`…); `GameFixedUpdate` je čistě výkonná smyčka nad nimi a `AdjustLegsArms`
řeší umisťování/odpojování končetin. Žádná AI uvnitř není — rozhodování je v podtřídě
(`CrawlerStyle` přes `MonsterBrain`, `Character3`). To je správný řez.

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

- ~~**Zobecnit pevný počet nohou/rukou**~~ — odloženo: nic v krocích 1–7 to nežádá (crawler
  i dravec jsou 2+2, balon vypne nohy `MovementMode`em). Až s druhým konkrétním konzumentem,
  viz [chlegsarms-refactor.md](chlegsarms-refactor.md#odloženo).
- ✅ **`MovementMode` + přepínání `body.useGravity`** — hotovo, i přepínání za běhu
  (`ChLegsArms.MoveMode`, výchozí režim druhu v `ChSettings.DefaultMovementMode`).
  Létání samo zatím neodzkoušené — `Free` nemá konzumenta.
- Vyčlenit **public API** pro AI: settery `desired*` (případně tenké metody),
  `Sleep(bool)` (uspí RB, collidery zůstávají). Zpětná vazba „narazil" nepatří motoru —
  viz [Zpětná vazba z plnění rozkazu](#zpětná-vazba-z-plnění-rozkazu).
- Při refaktoru zpřehlednit `GameFixedUpdate` (rozdělit pohon / podporu nohou / drag / hold).

---

## Mozek: prioritizovaná pravidla

> Revidováno. Sloučeno `IRule` + `IMonsterAction` do jednoho rozhraní. Zrušeno: `Blocking` flag,
> `Urgency` enum, per-rule `Cadence`, indirekce `GetAction`.

Pravidla jsou dvojího druhu — v **oddělených seznamech** (pořadí = priorita, proto dvě rozhraní):

### A) `IAiRule` — soupeří o pohybový zámek

```csharp
public enum ActionStatus { Running, Done }

public interface IAiRule                    // [SerializeReference]
{
    bool Test(MonsterBrain brain);          // vstupní podmínka (bezstavová, no-alloc)
    bool CanBeInterrupted { get; }          // smí ho přebít VYŠŠÍ pravidlo (nebezpečí přeruší žraní)
    void Begin(MonsterBrain brain);         // jednou při získání zámku (init scratch, animace, instant efekt)
    ActionStatus Tick(MonsterBrain brain);  // každý fixed krok, dokud drží zámek; Running/Done
    // (později, volitelně) void End(MonsterBrain brain, bool completed);  // úklid přerušeného
}
```

> **Pravidla dostávají přímo `MonsterBrain`** (původní `AiContext` zrušen — `Status`, `SlowTick`,
> `SetDirective`, smysly i `desired*` jsou na brainu, context by jen duplikoval). Implementace musí
> být **bezstavová**: všechny příšery druhu sdílí instance pravidel z jednoho `AiSettings` assetu.

**Konvence pojmenování:** pravidla nesou příponu `Rule` / `Modifier` (`RoamRule`, `FleeRule`,
`SleepRule`…) a leží ve složce `Ai/Rules/` → namespace `Assets.Scripts.Ai.Rules`. Na první pohled
(kód, inspektor, stack trace) je jasné, že jde o pravidlo; ruší i kolizi `SleepRule` × `SleepController`.

V jednu chvíli drží **zámek jen jedno pravidlo**. Tři typy chování pokrývá jedno rozhraní:
- **Instant** (např. `SetDirectionRule`): `Test`→`Begin` udělá efekt→`Tick` vrátí `Done` hned. Zabere
  frame (rozhodl o pohybu), ale zámek nedrží.
- **Durativní one-shot** (bodnutí, hod): drží zámek, `Tick` počítá animaci, `Done` až doběhne.
  Typicky `CanBeInterrupted = false`.
- **Spojité řízení** (útěk): `Tick` si **sám přetestuje** podmínku a vrátí `Done`, až pomine.
  `CanBeInterrupted = true`.

Přirozený invariant: *interruptible* chování jsou skoro vždy bezstavový steering (přerušení = prostě
zahodit, žádný úklid), *non-interruptible* doběhnou. Proto `End()`/cleanup do v1 nepotřebujeme.

### B) `IAiModifier` — reflexy, neberou zámek

```csharp
public interface IAiModifier                // [SerializeReference]
{
    bool Test(MonsterBrain brain);
    void Apply(MonsterBrain brain);         // instantní, každý fixed krok, nikdy nebere zámek
}
```

Modifiery se vyhodnocují **každý krok, i když pravidlo drží zámek**, a **nikdy** zámek neberou
ani nezastaví kaskádu pravidel. Mění *kontext/flagy* (orientace, nálada, odvozený stav), které
pak čtou pravidla / styl. Slouží i k **řetězení**: instantní modifier změní kontext a další
pravidla na to navážou. (Vodítko: modifier ať nepřepisuje primární pohybový příkaz, jen kontext —
ať se nepere s pravidlem/stylem.) V1 lezoucí příšera má `Modifiers[]` klidně prázdný.

### Vyhodnocovací smyčka (každý fixed krok)

```
// 1) Modifiery — VŽDY všechny, shora dolů
foreach m in Modifiers:
    if m.Test(brain): m.Apply(brain)

// 2) Pravidla — arbitráž zámku (running + runningIndex v blackboardu)
if running != null && !running.CanBeInterrupted:
    if running.Tick(brain) == Done: running = null
    return
foreach i in 0 .. (running != null ? runningIndex : Rules.Length):   // jen NAD běžícím
    if Rules[i].Test(brain):
        Rules[i].Begin(brain)
        running = (Rules[i].Tick(brain) == Running) ? Rules[i] : null; runningIndex = i
        return                             // první, kdo zabere, vyhrává — nižší priorita NEpřebíjí
if running != null:                        // nikdo vyšší nezabral → pokračuj v běžícím
    if running.Tick(brain) == Done: running = null

// 3) Pohyb (mimo Think, takže běží i po return)
if directive.Kind != None && directive.Kind != Manual:
    ApplyDirective(in directive)           // virtual; styl (listová třída) přeloží na desired*
// AdjustLegsArms + ChLegsArms.GameFixedUpdate provede desired*
```

**Pravidla se neřetězí** (první, kdo projde `Test`, zabere krok — i instantní). Řetězení je
úkol modifierů. Tím zůstává priorita zachovaná.

---

## Blackboard (centrální stav na MonsterBrain)

Klíčové pozorování: protože **v jednu chvíli běží jen jedno pravidlo**, durativní stav nepotřebuje
per-pravidlo úložiště — stačí **jedna sdílená sada „scratch" polí**. Tři jasně oddělené regiony:

| Region | Kdo zapisuje | Co | Životnost |
|--------|--------------|----|-----------|
| **Perception cache** | smysly (lazy gettery) | `SenseResult` per typ (pevná pole) | persistentní, freshness řízená pravidlem |
| **Action scratch** | běžící pravidlo | `ScratchTimer`, `ScratchPhase`, `ScratchPos`, `ScratchValue` (public — pravidla jsou v jiném namespace) | jen po dobu běhu pravidla |
| **Paměť nedosažitelných míst** | pravidla (`MarkUnreachable`) | `UnreachableSpot[4]` (pozice + `Until`), čte ji smysl | sekundy (`GiveUpTime`) |
| **Motor command** | pravidlo / styl | `CurrentDirective` (`None`/`Manual`/…) | standing (trvá, dokud se nezmění) |

Drives jsou ve `Status`, stav končetin v `ChLegsArms` — **neduplikujeme**. Plus `running`
+ `runningIndex` (běžící pravidlo). Celý blackboard se resetuje v `AfterMapPlaced` (pooling).

`Begin` inicializuje scratch (timer, *zachytí cíl do scratche*). `Tick` scratch aktualizuje
(`timer -= dt`). Na `Done` se scratch uvolní pro další.

> **Rozhodnuto — cíl je vždy POZICE, nikdy `Label`.** `Label` může být mezitím poolnut a ožít jako
> něco jiného (střela „na label" by pak letěla úplně jinam; na zapamatovanou pozici je to v pořádku).
> Proto `Label` nepřežije jeden výpočet: žije jen uvnitř vyhodnocení smyslu nebo dotazu motoru
> v okamžiku uchopení. Do scratche, cache smyslu ani paměti se neukládá — `scratchTarget` zrušen.
> Dlouhodobou vazbu na konkrétní objekt má jen motor po uchopení, a ta jde přes `Connectable`
> (pooling-safe: `Cleanup` odpojí connectables → `OnLimbDetached`).

---

## Smysly / Perception

> Revidováno. Throttling je zodpovědnost **pravidla** (zná důležitost informace), ne smyslu (zná
> jen fakt). Smysly jsou **lazy** — pod blokujícím vyšším pravidlem se nižší `Test` ani nezavolá,
> takže se smysl vůbec nespočítá.

### Princip: netrottluj test, trottluj data

Smysl zjistí fakt a uloží `(hodnota, timestamp)`. Konzument (pravidlo) řekne, jak staré to smí být:

```csharp
ref readonly var loot = ref brain.Sense(SenseId.Loot, maxAge: 0.5f);  // [s]; je-li cache starší, přepočítá teď
```

- **Sdílení:** dvě pravidla čtou stejný `brain.Sense(...)` → spočítá se max 1× za potřebné okno,
  ostatní převezmou. Vlastníkem dotazu je getter, ne test. Nejpřísnější konzument (nejmenší
  `maxAge`) fakticky určuje refresh rate.
- **Reakce každý frame:** test čte cache levně každý frame; throttluje se jen *drahý sken* uvnitř
  getteru. Urgentní data → malé `maxAge`; jídlo → velké.
- **Vnitřně-pomalá rozhodnutí** („mám jít spát?"): pravidlo se zhradí `brain.SlowTick(period)`
  s periodou dle potřeby (viz [Jeden tick](#jeden-tick)) — žádná pole per-rule.

### `SenseResult`

```csharp
public struct SenseResult
{
    public int Step;           // fixed krok (+ slowOffset) posledního výpočtu → freshness
    public bool Found;
    public Vector2 Position;   // střed nalezeného objektu
    public float Value;        // vzdálenost / množství / skóre — význam dle smyslu
}
```

- **Paměť:** blackboard má **pevná pole pro všechny typy smyslů ve hře** (typů bude jednotky →
  paměťově nula; žádný dictionary/alokace; cache-friendly). „Mrtvá" pole u nepoužitého smyslu nic
  nestojí.
- **Pooling:** `SenseResult` **nemá `Label`** — viz [Blackboard](#blackboard-centrální-stav-na-monsterbrain).
  Konzument pracuje s `Position` (stará max. `maxAge`), takže nikdy nemíří na poolnutý objekt.
- **Přímé dotazy:** speciální/komplexní jednorázový dotaz smí pravidlo udělat napřímo
  (`brain.ActiveMap.Get(...)`) bez smyslové abstrakce. Smysly jsou jen pro sdílená, opakovaná, cachovatelná
  fakta. Přímý dotaz v `Test` (běží každý frame) ať se zhradí `SlowTick`em.

### Konfigurace smyslů (data, ne kód)

Smysl = **výpočet + parametry**. Drtivá většina = jeden výpočet „nejbližší objekt daného `Ksid`
v okruhu R" (vzor `InventorySearcher`: `map.Get(...)` / `Secondary().Get(...)`). Liší se jen `Ksid`
a dosah („Potrava" = `Plant` u býložravce, `Prey` u dravce; „Nebezpečí" = jiný `Ksid`/dosah u každého).
Druhů výpočtu jsou jednotky → **enum + centrální `switch`** (ne delegáti, ne polymorfní objekty):

```csharp
public enum SenseId   { Loot, /* později Food, Danger, Prey, Mate, Home… (jen na konec!) */ }
public enum SenseKind { None, NearestKsid, /* později NearestKsidSec – sekundární mapa */ }

[Serializable]
public struct SenseConfig
{
    public SenseId  Slot;          // který slot plní
    public SenseKind Kind;         // který výpočet (switch)
    public Ksid     Ksid;          // co hledá
    public float    Radius;        // dosah
    public bool     NeedsSight;    // kandidát musí být vidět (raycast na vrstvu Default)
    public bool     SkipUnreachable; // přeskoč místa z paměti nedosažitelných míst
    public float    TrackRadius;   // >0 = tunelové vidění, velikost okna kolem posledního nálezu
    // doplňkové parametry přidáváme SEM stejným stylem; default (0/None/false) = „nezadáno"
}
```

`NearestKsid` (hotovo, `MonsterBrain.Senses.cs`): `map.Get` ve čtverci 2R → filtr kruhem, sebe sama
a nedosažitelných míst → částečný selection sort; s `NeedsSight` raycastuje max 3 nejbližší
kandidáty, dokud jeden není vidět. Filtr přepíše kandidáty do `List<Candidate>` s předpočítaným
`Center` a vzdáleností (`Center` jde přes `transform.position` — nativní volání, počítá se jednou);
test nedosažitelných míst se úplně přeskočí, když paměť vypršela (`unreachableUntilMax`).
`NearestKsidSec` přidáme se smyslem, který ho potřebuje (nebezpečí) — pak dostane i pole s výběrem
sekundární mapy. Perf rezerva do budoucna: sekundární mapa `Items` (předměty registrované přes
`SecondaryMapIndex`) — až to profiler ukáže nebo bude potřeba velký dosah.

**Tunelové vidění (`TrackRadius > 0`)** — je-li poslední nález nalezený a nejvýš `TrackMaxAge`
(0,7 s) starý, hledá se nejdřív jen v okně `TrackRadius` kolem něj: kandidát nejblíž k *poslední
pozici* (ne k příšeře), se stejnými filtry (`Radius` od příšery, nedosažitelná místa) a s `NeedsSight`
jen jeden raycast. Když nic nenajde nebo cíl není vidět → plné hledání. Přínos: perf (okno 2×2 m =
16 buněk vs. okruh 5 m = 400) a příšera nepřeskakuje mezi cíli. Proto per smysl: `Loot` ano,
budoucí `Danger` ne (sledoval by vzdálenějšího predátora a přehlédl bližšího). Fokus končí sám,
když cíl zmizí, uteče z okna, nebo `MarkUnreachable` zneplatní cache. Vynucené periodické
přehodnocení fokusu zatím neděláme.

Plochý `struct` (ne `[SerializeReference]`): no-alloc, cache-friendly, sedí na „fixní pole pro
všechny typy"; override hodnoty je triviální. Exotický smysl → nový `SenseKind`, který nepotřebná
pole ignoruje, nebo rozšířit struct (mrtvá pole jsou zdarma).

### Sdílení + override (default / shared / per-field)

Dvě vrstvy skládané **kompozicí** (ne dědičností settingů):

```csharp
[CreateAssetMenu]
public class SenseProfile : ScriptableObject { public SenseConfig[] Senses; }  // sdílený archetyp

// v AiSettings:
public SenseProfile  SenseProfile;     // sdílený základ (default)
public SenseConfig[] SenseOverrides;   // jen odchylky
```

**Override je per-položku konfigurace, ne per-slot:** v override záznamu se uplatní **jen nedefaultní
pole**, ostatní si nechají hodnotu z profilu. Tím lze odladit jen dosah a `Ksid` zdědit — a stejně
tak nové doplňkové parametry: starý config je nechá default a override se jich nedotkne.

```csharp
// resolve: jednou, lazy, cache na AiSettings (sdíleno všemi instancemi druhu)
var resolved = new SenseConfig[SenseCount];                 // indexed by (int)Slot
foreach (var c in SenseProfile.Senses)  resolved[(int)c.Slot] = c;
foreach (var o in SenseOverrides) {                          // merge per-field, jen nedefaultní
    ref var b = ref resolved[(int)o.Slot];
    if (o.Kind   != SenseKind.None) b.Kind   = o.Kind;
    if (o.Ksid   != default)        b.Ksid   = o.Ksid;
    if (o.Radius != 0)              b.Radius = o.Radius;
    if (o.NeedsSight)               b.NeedsSight = true;   // bool jde overridem jen zapnout
    if (o.SkipUnreachable)          b.SkipUnreachable = true;
    if (o.TrackRadius != 0)         b.TrackRadius = o.TrackRadius;
}
```

Cache je `[NonSerialized]` na `AiSettings` (`ResolvedSenses`), `OnValidate` ji zahodí.

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
public ref readonly SenseResult Sense(SenseId id, float maxAge)   // [s]
{
    ref var r = ref senseCache[(int)id];
    int period = Mathf.Max(1, Mathf.RoundToInt(maxAge / Time.fixedDeltaTime));
    int step = Game.Instance.FixedStepCounter + slowOffset;
    if (r.Step != InvalidStep && r.Step / period == step / period) return ref r;   // stejné okno → cache
    ref readonly var cfg = ref AiSettings.ResolvedSenses[(int)id];
    switch (cfg.Kind) {
        case SenseKind.NearestKsid: EvalNearestKsid(in cfg, ref r); break;
        default:                    r.Found = false; break;
    }
    r.Step = step;
    return ref r;
}
```

**Rozložení v čase — mřížka oken (stejná myšlenka jako `SlowTick`):** platnost se neměří časem od
výpočtu, ale oknem `(krok + slowOffset) / period` na mřížce fixed kroků. Hranice oken má každá
příšera jinde (`slowOffset` se přiděluje postupně), takže příšery umístěné ve stejném kroku (načtení
levelu) nepřepočítávají synchronně — nezávisle na tom, jak často pravidla čtou. Deterministické,
v celých krocích. Cena: první čtení po delší pauze spočítá uprostřed okna a na hranici znovu (max
1 výpočet navíc). Maximální stáří < `maxAge`, průměrné ~poloviční.

`InvalidateSenses()` vynutí přepočet všech slotů (volá ho `MarkUnreachable` a reset blackboardu).

**Editor validace:** varuj, když má profil dva záznamy na stejný `Slot`, nebo když pravidlo čte
`SenseId` s `Kind == None` v resolved tabulce.

---

## Directive — adaptér AI → motor

> Revidováno. **Není** datový typ kolující AI (žádné pravidlo nečte cizí `Kind`). Je to
> **standing příkaz pohybu v blackboardu**, který styl každý fixed krok překládá na `desired*`.
> Vypadl ze signatur `Test`/`Tick`.

```csharp
public enum DirectiveKind
{
    None,          // žádný rozkaz — výchozí stav po spawnu i po poolingu (= default); styl se nevolá
    Manual,        // pravidlo řídí desired* samo; styl se nevolá
    Stop, Roam, GoDirection, GoToward,
}

public struct Directive
{
    public DirectiveKind Kind;
    public int Direction;     // -1/+1 pro lezce
    public Vector2 Target;    // pro GoToward
    public float SpeedScale;  // 0..1 – zrychlí (strach) / zpomalí (únava)
}
```

Dva způsoby, jak pravidlo řídí pohyb:
1. **`brain.SetDirective(kind [, direction | target], speedScale = 1)`** — vyšší-úrovňový příkaz;
   **styl ho překládá na `desired*` podle terénu** (lezec hlídá díry/hrany, balon steeruje/odráží).
   Tohle je ten odpojovací šev, kvůli kterému framework existuje — pravidlo řekne *co*, styl
   rozhodne *jak* podle těla. Překlad je `protected virtual ApplyDirective(in Directive)` na
   `MonsterBrain`, kterou listová třída stylu overriduje — má tak přímý přístup k `desired*`, `map`,
   `body` i blackboardu.
2. **`desired*` napřímo přes `brain`** (= motor, je to `ChLegsArms`) — přesné řízení (ruka při
   bodnutí). Pravidlo pak nastaví `Kind = Manual`; smyčka přeskočí `ApplyDirective`.

**Standing = kontinuita zadarmo:** pravidlo nastaví directive obvykle v `Begin` (cíl do scratche);
`Tick` může být skoro prázdný — styl pokračuje v rozkazu každý krok sám (jede k uloženému cíli).
Přepočet jen když se něco změní (cíl se hnul, rozkaz nejde splnit). I krok, kdy žádné pravidlo
„nepřevezme", jede přísera dál podle standing directive — pohyb se netrhá.

### `CrawlerStyle` — překlad rozkazu (hotovo)

| Rozkaz | Lezec |
|--------|-------|
| `Stop` | `desiredVelocity.x = 0` |
| `Roam` | lez; při zaseknutí nebo hraně **otoč** (původní `SmallMonster`) |
| `GoDirection` | lez daným směrem; při bloku **drží směr**: pauza → rozjezd → zkusí znovu |
| `GoToward` | směr = `sign(Target.x − x)`, jinak jako `GoDirection`; blíž než 0.25 m ⇒ stůj |

Automat: pauza (stojí) → rozjezd (tlačí, zaseknutí podle rychlosti se netestuje — z klidu je
rychlost 0) → pohyb (tlačí, testuje zaseknutí). **Hrana (`AiSettings.AvoidHoles`) se testuje ve
všech fázích s tlakem, i v rozjezdu** — jinak by opakovaný pokus `GoDirection` tlačil do díry.
Změna rozkazu nebo směru začíná rozjezdem (jinak by klid po `Stop` vypadal jako zaseknutí).
`SpeedScale` násobí `ChSettings.maxSpeed`.

### Zpětná vazba z plnění rozkazu

> ✅ **Rozhodnuto pro cílené pohyby (`GoToward`): pravidlo měří POKROK samo, styl nic nehlásí.**
> Směrová razítka bloku (varianty níže) odloženo, dokud je nebude potřebovat `FleeRule`.

Rozkaz vydá pravidlo, plní ho styl — a **jen styl ví, že cesta nejde** (zeď, díra). Pravidlo to
neví přímo, ale u cíleného pohybu to **pozná z výsledku**: když se k cíli `Patience` sekund
nepřiblížil (o `MinProgress`), vzdá to. Geometrická vzdálenost funguje stejně pro lezce i létavce,
nepotřebuje projekci směru do „move prostoru" těla ani počítání pokusů — trpělivost je jeden
parametr pravidla. Pokryje zeď, díru, zaseknutí i cíl nad hlavou.

Sdílený helper na `MonsterBrain` (používá scratch: `ScratchPos` = poslední cíl, `ScratchValue` =
nejmenší vzdálenost, `ScratchTimer` = čas posledního pokroku); co dělat při zaseknutí, rozhoduje
pravidlo:

```csharp
brain.StartApproach(target);                          // Begin
if (!brain.TrackApproach(target, Patience))           // Tick; cíl poskočil o > 0.5 m → začne znovu
    { brain.MarkUnreachable(target, GiveUpTime); return Done; }
```

**Paměť nedosažitelných míst** (`MonsterBrain.Senses.cs`) — `UnreachableSpot { Pos; Until }[4]`,
kruhový buffer (s jedinou položkou by příšera přeskakovala mezi dvěma nedosažitelnými cíli).
Zapisují ji pravidla (`MarkUnreachable`, zároveň zneplatní cache smyslů), čte ji smysl se
`SkipUnreachable`: kandidát do 0,5 m od zapamatovaného místa se přeskočí. Pamatuje se **místo, ne
objekt** — když se předmět pohne, přestane záznamu odpovídat a příšera to zkusí znovu; po
`GiveUpTime` zkusí tak jako tak (otevřené dveře, odstraněná překážka). Ztráty oproti směrovým
razítkům: pomalejší reakce (až po `Patience`) a po obejití zdi to nezkusí hned z druhé strany.

Pro `FleeRule` (bez cíle) nejspíš stačí stejný vzor s opačným znaménkem (pokrok = rostoucí
vzdálenost od nebezpečí). Kdyby ne, varianty zveřejnění bloku stylem (TODO v `CrawlerStyle.Crawl`
zůstává):

1. **Event `BlockedThisStep`** (true jen v kroku detekce) — **nevhodné**: bezstavová pravidla
   čtou stav, jen když na ně přijde řada (pod jiným pravidlem se `Test` nevolá) → event propásnou.
2. **Stav vázaný na rozkaz — `float BlockedTime`** (jak dlouho už rozkaz nejde plnit). Trpělivost
   bez scratche (`Tick`: `BlockedTime > Patience → Done`). **Past:** další krok `FleeRule.Test`
   znovu projde (nebezpečí trvá) → zabere zámek, `Done`… nižší `DefendRule` se nedostane ke slovu.
   Aby to `Test` poznal, musel by číst `CurrentDirective` — a pravidlo nemá číst cizí rozkaz.
3. **Paměť terénu — razítko bloku per směr** (nemaže se změnou rozkazu):
   `bool WasBlocked(int dir, float withinSeconds)` + `protected ReportBlocked(int dir)` pro styl.
   Pravidlo rozhodne **už v `Test`** (`DangerNear && !brain.WasBlocked(awayDir, Memory)`) → přirozeně
   nastoupí nižší pravidlo, po `Memory` s zkusí znovu. Bez oscilace, bez čtení rozkazu, bez scratche.
   Zobecnění do 2D (probráno): styl hlásí směr pokusu `Vector2`, projekci směru k cíli dělá styl
   (`virtual ToMoveDir`), razítko drží sérii pokusů (`Attempts`, `FirstTime`/`LastTime`, pozice)
   a pravidlo zadá práh `IsBlocked(dir, minAttempts)`. Funkční, ale komplexní — proto odloženo.

---

## Sleep / Wake

> Revidováno. Spánek je **chování i perf nástroj** zároveň. Funkcionalitu mají **všechny příšery**,
> liší se jen parametry (v `AiSettings`).

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

**1) Dobrovolně — AI pravidlo `SleepRule : IAiRule`** (jedna třída, víc instancí v `Rules[]`,
priorita pořadím: NightSleep > Nap > MicroPause; tři délky):

```csharp
[Serializable]
class SleepRule : IAiRule
{
    public float NeedThreshold;   // od jaké únavy
    public DayWindow When;        // časové okno (noc / den / kdykoli pro micro)
    public float Duration;        // ← délka spánku (jediný parametr)
    public bool  NeedSafeSpot;    // micro-pauza nemusí

    public bool Test(MonsterBrain brain) =>
        brain.Status.SleepNeed >= NeedThreshold
        && When.Contains(Game.Instance.TimeOfDay)
        && (!NeedSafeSpot || brain.IsSafeToSleep());   // ← styl-specifický check
    public bool CanBeInterrupted => true;                 // nebezpečí spánek přeruší (vyšší pravidlo přebije)
    public void Begin(MonsterBrain brain) => brain.Sleep.EnterSleep(Duration);
    public ActionStatus Tick(MonsterBrain brain) => ActionStatus.Done;  // další frame už není na listu
}
```

Bezpečné místo řeší běžná AI — `IsSafeToSleep()` je jen *Test gate*; styl už averzí k dírám drží
příšeru na stabilní zemi, jinak `Roam` posouvá dál. Žádný zvláštní pathfinding.

**2) Nedobrovolně — kolaps silou z `MonsterBrain`**, mimo arbitráž zámku (musí přebít i běžící
*non-interruptible* akci — útěk, bodnutí). Není to rozhodnutí, ale fyziologická pojistka před eval smyčkou:

```csharp
// MonsterBrain tick (jen když je probuzená — spící není na per-frame listu)
Status.TickSleepNeed(dt);                          // roste, rychleji při aktivitě
if (Status.SleepNeed >= cfg.CollapseThreshold)     // kdekoli — i visíc na stromě, i u predátora
    { EnterSleep(Status.RecoverDuration()); return; }   // délku = čas zotavení pod WakeableLevel
// ... teprve pak modifiery + arbitráž pravidel
```

> Názvy: **`SleepRule`** = AI pravidlo (rozhodne); **`SleepController`** = lifecycle helper na
> `MonsterBrain` (vykoná, je `ISimpleTimerConsumer`). Převezme `MonsterBrain.ActivateAi/DeactivateAi`
> — jediné místo sahající na `Game.Instance.ActivateObject/Deactivate`.

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

> Revidováno. **`Status` poskytuje velikost potřeby spánku** (`SleepNeed`) — je vstupem pro
> `SleepRule` i pro kolaps. (HP a `ReduceHealth` zůstávají.)

Rozšířit `Status` o normalizovaný `SleepNeed` (0..1) a **branku probuzení**:
- **`TickSleepNeed(dt)`** — volá `MonsterBrain` každý frame, dokud je probuzená (roste,
  rychleji při aktivitě). Ve spánku se nepočítá průběžně, ale **lazy** z timestampu
  (`SleepController.CurrentNeed()`, klesá `RecoverRate`).
- **`CanWake(need)`** = `need <= WakeableLevel` — jediné kritérium probuzení. Z něj vypadne
  „kolaps nelze probudit" (začíná nad hladinou) i „dobrovolný spánek lze hned" (začíná pod ní).
- **`RecoverDuration()`** = `(SleepNeed − WakeableLevel) / RecoverRate` — délka spánku při kolapsu.
- Prahy v `AiSettings` (per druh, jinak stejný kód): `CollapseThreshold` (~1.0),
  `WakeableLevel` (~0.7), `RecoverRate`, růstová rychlost; per `SleepRule` instanci
  `NeedThreshold`/`When`/`Duration`.

Pro v1 reálně stačí **`SleepNeed`**; `Energy`/`Hunger` odložit, dokud nebudou pravidla, která je čtou.

---

## Konfigurační body

| Kde | Co se ladí |
|-----|-----------|
| volba podtřídy stylu na prefabu (`CrawlerStyle`/`FlyStyle`/…) | **styl pohybu** – žádný serializovaný odkaz, vybírá se která komponenta je na prefabu |
| `ChSettings` (už existuje) | fyzika motoru – speed, accel, jump, nohy/ruce, `MovementMode` |
| `AiSettings : ScriptableObject` (existuje) | `[SerializeReference, TypePicker] IAiRule[] Rules` (vč. konfig. `SleepRule` instancí), `IAiModifier[] Modifiers`; **konfigurace stylu** pod `[Header]` podle stylu (`Crawler`: `AvoidHoles`); později spánek (`CollapseThreshold`, `WakeableLevel`, `RecoverRate`, růst `SleepNeed`), wake radius/perioda, `SenseProfile` + `SenseConfig[] SenseOverrides` |
| pole listové komponenty stylu | jen **běhový stav** stylu (`turnTimeout`) a počáteční stav z prefabu (`direction`, reset v `Cleanup` z prototypu) — **ne konfigurace** |

> **Konvence:** ladicí parametry stylu patří do `AiSettings` (sdílený asset druhu), ne na komponentu.
> Pole stylu, který příšera nemá, jsou mrtvá a nic nestojí (stejná filozofie jako `SenseConfig`).
> Pozn.: `[SerializeReference]` pole potřebují v inspektoru `[TypePicker]` (vlastní drawer
> v `EditorExtensions/TypePickerDrawer.cs`) — Unity 6.0 výběr konkrétního typu samo neumí.
| `SenseProfile : ScriptableObject` | sdílený archetyp smyslů (`SenseConfig[]`); per-monster override per-field v `AiSettings` |
| `PlaceableSettings.SecondaryMapIndex` | `Beasts` — registrace na sekundární mapu pro probouzení |
| `Ksid` | typy pro Perception/probouzení: např. `Beast`, `Danger`, `Food`, `Prey` |
| per-rule serializovaná pole | prahy podmínek, váhy, `maxAge` pro smysly |

## Mapování na příklady příšer

(„styl" = listová podtřída `MonsterBrain` na prefabu; „pravidla" = `[SerializeReference]` seznamy.)

- **Lezoucí (prefab `Small Monster`)** → `CrawlerStyle` (`Legged`, averze k dírám/nebezpečí) +
  minimální seznam pravidel (dnes jen `RoamRule`), prázdné modifiery.
- **Po povrchu i hlavou dolů** → `SurfaceStyle` (`Legged` s povrchovou „gravitací"), jeden směr,
  otočka na nebezpečí.
- **Létající balon / kulečník** → `BounceStyle` (`Free`, gravitace off, odraz).
- **Létající dravec** → `FlyStyle` (`Free`) + ruce (chytí a odnese kořist) + bohatší pravidla.
- **S rozhodováním** → kterýkoli styl + bohatší seznam pravidel (později candidate scoring).

### Připraveno na později: candidate scoring

Style naplní **statický bufr** kandidátních lokací (vzor `armCandidates` v `ChLegsArms`) a nechá
mozek je ohodnotit součtem `IScorer` pravidel; vrátí nejlepší. Kontrakt se navrhne tak, aby
`IAiRule` a `IScorer` mohly koexistovat.

## Konvence (viz [conventions.md](conventions.md))

- **No-alloc:** podmínky/akce bez alokací; kandidáti přes statické bufry.
- **Pooling:** init v `Awake` (listová třída: `void Awake() => AwakeM();`), ne `AfterMapPlaced`;
  reset blackboardu v `AfterMapPlaced`, stav stylu v `Cleanup`. **Cíl drž jako pozici, ne `Label`.**
- **Pozice = `Center`:** „kde je příšera" je jediná definice `MonsterBrain.Center` (`placeable.Center`),
  cíle smyslů taky `Center`, raycasty `Center3D`. Ne `Pivot`/`transform` (liší se u prefabu, jehož BB
  není kolem pivotu). Výjimka: interní dotazy stylu na terén pod tělem (`CrawlerStyle.WantMove`).
  Cena je stejná jako u `Pivot` — obojí stojí na jednom `transform.position`.
- **Namespace** podle složky (`Assets.Scripts.Ai`, pravidla `Assets.Scripts.Ai.Rules`); pravidla
  s příponou `Rule`/`Modifier`, `[Serializable]`, bezstavová.
- **KSID místo `is`/`GetComponent`** pro herní interakce (`IsChildOf`/`IsChildOfOrEq`).
- Nové `.cs` pod `Assets/Scripts` → přidat `Compile Include` do `MainGameAsm.csproj`.

---

## Featura: sbírání předmětů

Mimo implementační pořadí — první konkrétní chování nad frameworkem. Příšera se rozhlíží po
předmětu daného `Ksid`, jde k němu (`GoToward`), sebere ho do inventáře; když už má dost, nesbírá;
když se k předmětu nedá dostat, vrátí se do Roam.

```
Rules: [ PickUpItemRule, SeekItemRule, RoamRule ]      (pořadí = priorita)
Senses: Loot = NearestKsid <Ksid> r<R>, SkipUnreachable, TrackRadius ~1, (NeedsSight)
```

**A+B — ✅ hotovo:** smysly (viz [Smysly](#smysly--perception)), `SeekItemRule` (`Slot`, `MaxAge`,
`SpeedScale`, `Patience`, `GiveUpTime`), hlídání pokroku + paměť nedosažitelných míst (viz
[Zpětná vazba](#zpětná-vazba-z-plnění-rozkazu)).

**C — ✅ naimplementováno (čeká na test v editoru): sebrání + inventář.** Kód v
`MonsterBrain.PickUp.cs` (`PickUp(ksid)`, `CountItems`, `IsSaturated`, lazy inventář),
`MonsterBrain.LookAt` (ukazatel) a `Rules/PickUpItemRule.cs`; v motoru `GetTargetPointer`,
`PickupQueryKsid`, `IsHeldByOtherArm`; `Inventory.CountKsid`. Druhá ruka se nechytí předmětu,
který první už drží nebo sbírá (`IsHeldByOtherArm` v `TryHoldNearItem`, platí i pro hráče — jinak by
ho při sebrání uložily obě ruce). Rozhodnutí:
- **Příkazy na jeden krok:** brain na začátku `GameFixedUpdate` zahodí `desiredPickUp` a ukazatel
  nastaví na výchozí; běžící pravidlo je v každém `Tick` nastaví znovu (`PickUp(ksid)`,
  `SetDirectiveAndLookAt`/`LookAt`). Když pravidlo
  přebije vyšší, nic nezůstane viset → `PickUpItemRule.CanBeInterrupted = true` a `End()` dál
  nepotřebujeme. Ruku, která už předmět táhne, motor dotáhne nebo upustí sám.
- **Ukazatel (`LookAt`) = obdoba myši u hráče** — motor ho čte přes `GetTargetPointer` (sbírání,
  později míření držené věci). Na začátku kroku ho určí styl podle rozkazu (`virtual DefaultLookAt`):
  `GoToward` → cíl, lezec v `Roam`/`GoDirection` → 1 m před sebe, jinak `Center`. Pravidlo ho může
  v `Tick` přepsat přes `LookAt`, nebo rovnou `SetDirectiveAndLookAt` („jdu tam = dívám se tam";
  jinam se dívat = potom ještě `LookAt`). Výchozí se počítá z rozkazu minulého kroku — při změně
  rozkazu bez `LookAt` je ukazatel jeden krok (20 ms) pozadu, což nevadí.
- **Úspěch sebrání** = vzrostl počet kusů v inventáři (`CountItems` při `Begin` ve `ScratchPhase`) —
  žádný čítač navíc.
- Umírající příšera (`!placeable.IsAlive` v `InventoryPickup`) předmět z ruky neukládá, upustí ho do světa.
- **Plný `Inventory`** (vzor `Chest`), vytvořený **lazy** při prvním sebrání (spící příšery bez
  kořisti nestojí nic), **bez Ksid `HasInventory`** — jinak by si ho hráčův `InventorySearcher`
  nalinkoval a auto-refill by z příšery tahal věci. Při smrti se inventář zabije (později mrtvé tělo
  s inventářem k prohledání).
- **Pickup v motoru bez `Label`:** hráč sbírá to, co je pod myší (bod) — sjednoceno do
  `GetTargetPointer`; příšera vrátí svůj ukazatel. `IsPickupAllowed` =
  `Ksid` ze smyslu, dotaz do mapy přes virtuální `PickupQueryKsid` (dnešní `Settings.HoldType` =
  `SmallMonsterHolds` nemá potomky). `Label` se najde až v kroku uchopení, dál ho drží `Connectable`.
- **`PickUpItemRule`** (`SenceId`, `MaxCount`, `Reach`, `Timeout`, `GiveUpTime`, `SpeedScale`),
  nad `SeekItemRule`, přerušitelné. `Test` = blíž než `Reach` (ne „v dosahu ruky" — jinak by pod
  předmětem na římse stál navždy) a není nasycen; během sbírání pomalu `GoToward` k předmětu.
  Timeout → `MarkUnreachable` (římsa, těžký předmět).
- **Saturace:** `MaxCount` přímo na obou pravidlech; počet kusů v inventáři, jejichž `Ksid` je
  potomkem `Ksid` ze smyslu.
- Držené předměty neřešíme — příšera je vidí a pokusí se je ukrást.

---

## Otevřené drobnosti (dořešit při implementaci)

Hlavní návrh je zrevidovaný. Zbývají drobnosti, které se dořeší v kódu:

1. ✅ ~~Zpětná vazba z plnění rozkazu~~ — pro `GoToward` rozhodnuto (pokrok v pravidle), viz
   [Zpětná vazba](#zpětná-vazba-z-plnění-rozkazu). Pro `FleeRule` ověřit, že vzor stačí.
2. ✅ ~~Životnost `scratchTarget`~~ — cíl jen jako pozice, viz [Blackboard](#blackboard-centrální-stav-na-monsterbrain).
3. **`TimeOfDay`** — zdroj denní doby pro `SleepRule.When`: `Game.Instance.TimeOfDay` existuje,
   ověřit API při kroku 6.
4. **`IsSafeToSleep()`** — přesný style-specifický check (lezec: na zemi + ne na hraně; příp. žádné
   akutní nebezpečí).
5. **Smysly** — `SenseId` roste podle konzumentů (zatím `Loot`); `NearestKsidSec` s prvním smyslem
   na sekundární mapě. Editor validace čtení slotu s `Kind == None` zatím chybí (duplicitní slot
   v profilu už varuje).
6. Případně `End()`/cleanup u pravidel (zatím odloženo, viz Mozek).

## Implementační pořadí

1. **Refaktor `ChLegsArms`** — ✅ v podstatě hotový. `MovementMode` (`Legged`/`Free`, gravitace
   off) je v kódu včetně přepínání za běhu; zobecnění počtu končetin je odloženo (nikdo ho
   nežádá); public API pro AI (`desired*` settery, `DropAllLimbs`) se dodělá až s krokem 2, kdy
   bude vidět, co pravidla potřebují. Zbývá ověřit, že `Free` opravdu lítá (balon).
   RB neuspáváme — to dělá Unity samo.
   > Podrobně v [chlegsarms-refactor.md](chlegsarms-refactor.md): kýble A (zpřehlednění)
   > a B (rozdělení GameUpdate/FixedUpdate + oprava hodu) jsou hotové, kýbl C je protříděný —
   > většina bodů zrušena nebo odložena.
2. ✅ **`MonsterBrain : ChLegsArms`** (abstraktní, `Ai/MonsterBrain.cs`) — blackboard (scratch
   + standing `Directive`), eval smyčka ve fixed kroku (modifiery + arbitráž zámku s `runningIndex`),
   `virtual ApplyDirective`, `SetDirective` přetížení, `SlowTick(period)` s per-instance offsetem
   (`Game.FixedStepCounter`), `ActivateAi/DeactivateAi`. Rozhraní `IAiRule`/`IAiModifier`
   (bez `AiContext`), `AiSettings` (seznamy pravidel), `DirectiveKind.None`.
3. ✅ **`CrawlerStyle : MonsterBrain`** — vznikl přejmenováním `SmallMonster.cs` se zachováním
   `.meta` (GUID → prefab `Small Monster` se přepojil sám). Překlad rozkazů (viz
   [CrawlerStyle](#crawlerstyle--překlad-rozkazu-hotovo)), oprava hlídání hrany v rozjezdu, reset
   `direction`/`turnTimeout` v `Cleanup` z prototypu (dřív pooling bug). `ChSettings.monsterMoveOnGround`
   → `AiSettings.AvoidHoles`. První pravidlo `Rules/RoamRule`. Editor: `[TypePicker]` + drawer.
   **V editoru:** vytvořit `AiSettings` asset pro Small Monster, přidat `RoamRule`, přiřadit prefabu.
4. **`IAiRule` / `IAiModifier` pravidla** — `DefendRule`, `SetDirectionRule`, `SeekFoodRule`,
   `FleeRule`, `SleepRule`. (Částečně předběhnuto featurou [Sbírání předmětů](#featura-sbírání-předmětů):
   `SeekItemRule` + zpětná vazba pro `GoToward`.)
5. ✅ **Perception** — `SenseResult` + `SenseConfig`/`SenseKind` switch, `SenseProfile` + per-field
   override resolve, lazy `Sense(id, maxAge)` s freshness (`MonsterBrain.Senses.cs`), `SlowTick`.
   Hotovo v rámci featury sbírání předmětů.
6. **`Status.SleepNeed`** (+ `CanWake`/`RecoverDuration`) + **`SleepController`** (`ISimpleTimerConsumer`)
   — jeden `EnterSleep(duration)`, branka `TryWake`, centrální kolaps-guard v `MonsterBrain`,
   lazy `CurrentNeed`, wake přes `Timer.Plan` (rušení časovače `ActiveTag`em).
7. **`AiSettings`** — doplnit spánek a smysly; sestavení prvních příšer z modulů.
8. (později) `BounceStyle`, povrchový režim, candidate scoring, push-trigger probouzení, `End()` cleanup.
