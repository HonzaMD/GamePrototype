# Framework pro pohybující se příšery / živočichy

Návrh frameworku pro skládání chování příšer z vyměnitelných modulů. Cíl: kombinovat
**styl pohybu** a **rozhodovací logiku** nezávisle, bez globálního pathfindingu — vše
stojí na **lokálním rozhodování**.

> Stav: návrh zrevidovaný, **implementace probíhá** — kroky 1–3 hotové, featura
> [Sbírání předmětů](#featura-sbírání-předmětů) naimplementovaná (smysly, `SeekItemRule`,
> `PickUpItemRule`, inventář příšery; krok C čeká na test v editoru) a
> [Házení předmětů](#featura-házení-předmětů) (`ThrowItemRule`, čeká na test v editoru) a
> [Létání](#featura-létání) (`FlyingStyle`, čeká na test v editoru), viz [Implementační pořadí](#implementační-pořadí). Při implementaci padla rozhodnutí, která
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
public enum SenseId   { Loot, Target, /* později Food, Danger, Prey, Mate, Home… (jen na konec!) */ }
public enum SenseKind { None, NearestKsid, /* později další druhy výpočtu */ }

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
    public bool     SightPerPlane; // NeedsSight raycastuje zvlášť v každé z rovině
    public SecondaryMap SecondaryMap; // None = hlavní mapa, jinak hledá na sekundární mapě
    // doplňkové parametry přidáváme SEM stejným stylem; default (0/None/false) = „nezadáno"
}
```

`NearestKsid` (hotovo, `MonsterBrain.Senses.cs`): `map.Get` ve čtverci 2R → filtr kruhem, sebe sama
a nedosažitelných míst → částečný selection sort; s `NeedsSight` raycastuje max 3 nejbližší
kandidáty, dokud jeden není vidět. Filtr přepíše kandidáty do `List<Candidate>` s předpočítaným
`Center` a vzdáleností (`Center` jde přes `transform.position` — nativní volání, počítá se jednou);
test nedosažitelných míst se úplně přeskočí, když paměť vypršela (`unreachableUntilMax`).
**Sekundární mapa je parametr, ne druh výpočtu** (`SenseConfig.SecondaryMap`, `None` = hlavní
mapa): týž `NearestKsid` hledá na `map.Secondary(id)` — jen mezi objekty, které se na ni registrují
(`PlaceableSettings.SecondaryMapIndex`), zato levně na velký dosah (buňky 9× větší). Dotaz jde
přes `GetPlaceables` (tunelové vidění i plné hledání). Perf rezerva do budoucna: sekundární mapa
`Items` (předměty registrované přes `SecondaryMapIndex`) — až to profiler ukáže nebo bude potřeba
velký dosah.

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
foreach (var o in SenseOverrides) {
    ref var b = ref resolved[(int)o.Slot];
    b = new SenseConfig(in b, in o);                         // merge per-field, jen nedefaultní
}

// SenseConfig - kopy konstruktor pro override:
public SenseConfig(in SenseConfig b, in SenseConfig o)
{
    this = b;
    Slot = o.Slot;
    if (o.Kind   != SenseKind.None) Kind   = o.Kind;
    if (o.Ksid   != default)        Ksid   = o.Ksid;
    if (o.Radius != 0)              Radius = o.Radius;
    if (o.NeedsSight)               NeedsSight = true;     // bool jde overridem jen zapnout
    // ... stejně pro ostatní pole
}
```

Cache je `[NonSerialized]` na `AiSettings` (`ResolvedSenses`), `OnValidate` ji zahodí.

Praktický dopad — designér skoro nikdy nekonfiguruje vše:

| Příšera | `SenseProfile` | `SenseOverrides` |
|---------|----------------|------------------|
| Zajíc | `HerbivoreSenses` | — |
| Srnec | `HerbivoreSenses` | `Danger.Radius 12→18` |
| Vlk | `PredatorSenses` | — |

(`HerbivoreSenses = { Food: NearestKsid Plant r8; Danger: NearestKsid Predator r12 SecondaryMap=Beasts }`.) Změna
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
    public Vector2 Direction; // 2D směr pro GoDirection, nemusí být normalizovaný (lezec bere sign(x))
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
| `GoDirection` | lez směrem `sign(Direction.x)` (svislý směr ⇒ stůj); při bloku **drží směr**: pauza → rozjezd → zkusí znovu |
| `GoToward` | směr = `sign(Target.x − x)`, jinak jako `GoDirection`; blíž než 0.25 m ⇒ stůj |

Automat: pauza (stojí) → rozjezd (tlačí, zaseknutí podle rychlosti se netestuje — z klidu je
rychlost 0) → pohyb (tlačí, testuje zaseknutí). **Hrana (`AiSettings.AvoidHoles`, nebezpečí před
sebou `DangerKsid`) se testuje ve
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
| `AiSettings : ScriptableObject` (existuje) | `[SerializeReference, TypePicker] IAiRule[] Rules` (vč. konfig. `SleepRule` instancí), `IAiModifier[] Modifiers`; **konfigurace stylu** pod `[Header]` podle stylu (`Danger`: `DangerKsid`; `Crawler`: `AvoidHoles`; `Jumper`: `JumpSet`); později spánek (`CollapseThreshold`, `WakeableLevel`, `RecoverRate`, růst `SleepNeed`), wake radius/perioda, `SenseProfile` + `SenseConfig[] SenseOverrides` |
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

## Featura: házení předmětů

> ✅ Naimplementováno (2026-10-08), čeká na test v editoru.

Příšera vidí cíl a má v inventáři předmět daného `Ksid` → vytáhne ho do ruky, chvíli míří a hodí
ho po cíli obloukem, který si spočítá. Když je to pro zásah potřeba, přendá předmět do druhé z roviny.

```
Rules: [ ThrowItemRule, PickUpItemRule, SeekItemRule, RoamRule ]
Senses: Target = NearestKsid Character r8, NeedsSight, SightPerPlane, TrackRadius 1
```

Rozhodnutí:
- **Předmět se identifikuje `Ksid`, ne prototypem** — stejně jako sbírání a saturace; hierarchie
  dovolí obecné pravidlo, preferenci dává pořadí (`ThrowItemRule(bomba)` nad `ThrowItemRule(kámen)`).
  Inventář: `TryFindSlotKsid`.
- **Síla hodu v `ChSettings`** (`ThrowMaxSpeed = 11`, `ThrowPower = 20`,
  `MaxThrowSpeed(m) = min(ThrowMaxSpeed, ThrowPower/√m)` — omezená energie hodu). Dřív natvrdo
  v `ThrowController`; hráč se nezměnil (`throwForce` 0..2 = poloviční až plná síla). **Provedení hodu
  je v motoru** (`ChLegsArms.ThrowHeld(obj, relVelocity)` + `ApplyThrow` na konci `GameFixedUpdate`,
  zpětný ráz) — hráč i příšera jdou stejnou cestou.
- **Dráha je čistá parabola** (gravitace −9, předměty nemají `linearDamping`) → analyticky
  (`MonsterBrain.Throw.cs`). Styly hodu (`ThrowStyle` na pravidle): `Direct` = nižší úhel plnou
  rychlostí (rychlý, plochý), `Lob` = nejmenší energie (přes překážku). Mimo dosah, když
  `v_min² = g(y + |Δ|) > MaxThrowSpeed²`. Předsazení pohyblivého cíle zatím ne.
- **Test volné dráhy jen v pravidle** (ne ve smyslu): `Segments` úseků `SphereCast` s poloměrem
  předmětu v rovině hodu; první zásah rozhoduje (Ksid cíle = volno). Hloupější příšera má méně
  segmentů a občas hodí do překážky; `Segments = 0` test úplně vypne. Odchylka tětivy od paraboly
  je `g·(T/n)²/8` (lob 1,3 s: n=4 → ~12 cm, n=2 → ~0,5 m), default 4. Rychlosti obou stylů
  se počítají jednou (`TrySolveArcs`) — v obou rovinách jsou stejné, liší se jen test dráhy.
- **Robustnost:** `v_min²` a nižší úhel Direct se počítají ve tvaru bez odčítání blízkých čísel,
  `v_min² ≤ 0` (degenerace) = mimo dosah; poloměr předmětu ≥ 2 cm; plánuje se na 97 % `vMax`
  (motor ořezává rychlost relativní k tělu).
- **Z rovina:** předmět zasáhne jen cíl ve své rovině → hází se v rovině cíle (`SenseResult.ZPlanes`,
  cíl přes dvě buňky = obě). Pořadí: rovina předmětu, druhá. Přesun `CanZMove` + `MoveZ`
  (`MoveHeldItemZ`) už na začátku míření, ať je hod čitelný; předměty přes dvě buňky se nepřesouvají.
- **`SenseConfig.SightPerPlane`** (default vypnuto = jeden šikmý paprsek mezi `Center3D`): paprsky
  mezi `Center` v rovině — nejdřív v rovině kandidáta, pak ve druhé.
- **Výpočet dráhy už v `Test`**, ale jen každých `SolveInterval` (0,5 s, `SlowTick`) — pravidlo
  zůstane bezstavové, bez dráhy se předmět ani nevytahuje a nevznikne smyčka „vytáhni–vrať“.
  Rovinu z `Test` si pravidlo uloží do `ScratchValue` (Test smí připravit scratch, jen když vrací
  true — `Begin` následuje hned) a po uchopení do ní předmět přendá. Před hodem se dráha přepočítá
  (cíl se mohl pohnout); když nejde, konec bez hodu.
- **Fáze** (`ScratchPhase`): vytažení (`DrawItem`, `DrawTimeout`) → míření (`Stop`, `LookAt`,
  `AimTime`) → hod → pauza (`Stop`, `AfterThrowTime` 1 s). `CanBeInterrupted = false` (krátká
  jednorázová akce), takže pauza blokuje i `PickUpItemRule` — jinak by hozený předmět hned chytil
  (pomalý lob dolů má jen ~1 m/s, limit relativní rychlosti `MaxPickUpSpeed` v `IsPickupAllowed`
  na něj nestačí).
- **Držení je příkaz na jeden krok** jako `desiredPickUp`: brain na začátku kroku nuluje
  `desiredHold`, `DrawItem` ho obnoví. Když pravidlo skončí bez hodu, brain po `Think` vytažený
  předmět vrátí do inventáře — držený i neuchopený. Umírající příšera vytažený předmět upustí
  do světa.
- **Inventářové API motoru:** `InventoryReturn`/`InventoryDrop` na `ChLegsArms` jsou bezpečné —
  ruku, která předmět drží, nejdřív pustí. Připojená ruka (`Connectable` pod předmětem) totiž
  blokuje `CanBeInInventory` a `ReturnActiveObj` by předmět místo návratu upustil. Potomci
  implementují jen holé `InventoryReturnRaw`/`InventoryDropRaw` (protected); holý návrat volá
  motor jen v `OnLimbDetached`, kde je ruka už `Off`.

## Featura: skákání

> 🚧 Krok 1 hotový (2026-10-13): `JumpDetector` — detekce, kam lze skočit.
> Krok 2 naimplementován (2026-10-15), čeká na test v editoru: `JumperStyle` skáče v `GoToward`.
> Krok 3 naimplementován (2026-10-18), čeká na test v editoru: `WallFollowerStyle` skáče v `Roam`
> podle pravidla ruky.

Skok má pevnou sílu odrazu (výška paraboly i doba skoku dané), ale ve vzduchu jde řídit x. Kam
příšera doskočí, se nezjišťuje simulací za běhu, ale **předem zadanými trajektoriemi**: styl skoku
je tak pod kontrolou a dotaz je levný. Trajektorie se zatím zadávají ručně, později je může
vyrábět parametrický generátor (rozsah y, pravidlo pro x, hrubě vykreslená dráha podle velikosti příšery).

**Trajektorie = ASCII mapa** (`JumpTrajectory.Parse`), skok doprava, doleva se zrcadlí `dx`.
Řádky ohraničené `|` (mezery jsou významné), horní řádek = nejvyšší y, mapa jen obaluje trajektorii:

| znak | význam |
|------|--------|
| `*` | podlaha pod příšerou = počátek [0,0], netestuje se |
| mezera | musí být volno |
| `#` | musí být doskočiště — právě jedno, je to cíl skoku (offset `*` → `#`) |
| `x` | nesmí být doskočiště — selže, když by uspěl kratší skok ve stejném směru nebo skok není potřeba |
| `?` | cokoliv (tělo příšery) |

Sady jsou stringové konstanty v `JumpTrajectories.cs`, jeden `JumpDetector` na druh příšery
(velikost + síla skoku). Všechny příšery uvažujeme jako half-cell → testuje se jedna z rovina.

**Třída buňky** (`Map.GetJumpCellClass(cell, cellz, exclude, danger)`): `Free` / `Static` (doskočiště) /
`Other` (pohyblivé). Nebezpečí (`danger`) → `Other`, testuje se první (viz
[Vyhýbání se nebezpečí](#featura-vyhýbání-se-nebezpečí)). `HasFloor` (`FullEx` nebo písek) → `Static`; bez `Part` → `Free`; jinak pomalá
cesta přes placeably v buňce: kterýkoli `IsStatic` → `Static` (statické objekty mimo mřížku dávají
jen `Part`), jinak `Other`. Příšera sama (`exclude`) se ignoruje — zasahuje i do buněk mimo `?`.
Mimo mapu = `Free` (není tam doskočiště, takže trajektorie stejně neuspěje).

**Automat** (`JumpAutomaton`) otestuje všechny trajektorie (max 64) jedním průchodem:
- Buňky všech trajektorií se seřadí globálně od nejbližší (`dx² + dy²`). Každý stav testuje
  jednu buňku a podle třídy jde na jeden ze tří dalších stavů → buňka se testuje nejvýš jednou
  a blízké buňky (volno nad hlavou) zabijí neplatné trajektorie dřív, než se sáhne na vzdálené.
- Stav = `(k, alive)`: index další buňky + živé **nerozhodnuté** trajektorie. Hodnoty otestovaných
  buněk netřeba pamatovat (požadavky jsou konjunkce), stejné klíče se slučují (DAG). Buňky, na
  kterých žádná živá trajektorie nezávisí, se přeskakují.
- Trajektorie, která projde svou poslední buňku, se hned ORuje do výsledku (emit na přechodu)
  a ze stavu zmizí. Varianta s výsledkem až v terminálech nesla v klíči i hotové trajektorie
  a vybuchla: realistická sada 28 trajektorií 61 tis. stavů místo 2 tis., 42 trajektorií 9,7 tis.
- Za běhu se čte jen `states[]` (12 B na stav): relativní buňka `Dx, Dy` a pro každou třídu
  `Next*` + `Emit*` (byte: 0 = nic, 1..64 = jedna trajektorie `1UL << (e-1)`, 65..255 = index do
  malé tabulky `emits` pro přechod, který dokončí víc trajektorií najednou — v realistických
  sadách se nevyskytuje).
- Strop `short.MaxValue` stavů, při překročení výjimka. Detektor (i s automatem) se staví líně v
  `JumpTrajectories.Get` (stavba smí alokovat), dotaz je bez alokací (`Run<T>` přes struct `IJumpCellSource`).

API: `JumpDetector.Detect(map, floorCell, cellz, dir, self)` → `ulong` maska trajektorií,
`TargetOffset(i, dir)` → podlaha doskoku. Testy: `Assets/Tests/JumpAutomatonTests.cs` (porovnání
s hrubou silou na náhodných i realistických sadách + kontrola, že se žádná buňka netestuje dvakrát)
a `Assets/Tests/JumpDetectorTests.cs` — herní sady z `JumpTrajectories` (nový detektor přidat do
`Detectors`): statistiky (stavy, nejhorší počet testů na dotaz, průměr testů v terénu), každá trajektorie uspěje ve svém
světě v obou směrech, volno nad cílem, unikátní cíle, v jednom řádku cílů nejvýš jedna úspěšná
trajektorie (statická kontrola slučitelnosti), shoda s hrubou silou v náhodném terénu a každá
trajektorie v něm aspoň jednou uspěje.

### Skokan: `JumperStyle : CrawlerStyle` (skákání v `GoToward`)

Skákání patří stylu. `CrawlerStyle` zůstává bez skoku, jen vrací stav kroku lezení (`ApplyCrawl` → `CrawlStatus`:
`Stopped / Paused / Starting / Moving / Edge / Wall`), podle kterého skokan pozná hranu a zaseknutí.
Sada trajektorií se vybírá polem `JumpSet` v `AiSettings` (`JumpTrajectories.Get`).

- **Konvence pivotu:** `transform.position` je u příšer uprostřed x a dole v y. Jeho buňka je buňka
  příšery, buňka pod ní odrazová podlaha (počátek detektoru), i když to přesně neodpovídá. Nohy
  nejsou delší než 1 buňka.
- **Kdy se detekuje** (jen na nohou): hrana nebo zaseknutí ve směru k cíli; nová plošina — po vstupu
  do nové buňky je před hlavou (dir,1) nebo (dir,2) obsazeno a nad hlavou (0,1), (0,2) volno; cíl
  skoro nad hlavou (buňka cíle |dx| ≤ 1, výš než buňka těla) — vždy a oba směry. Maska detekce se
  cachuje na buňku podlahy a 1 s, takže se na stejné buňce neopakuje.
- **Výběr — v buňkách** (buňka těla, buňka těla po dopadu = nad `#`, buňka cíle; přesnou výšku podlah
  stejně neznáme): dopad musí ostře zkrátit eukleidovskou vzdálenost k cíli proti současné pozici
  (`JumpMinGain` 0,01 buňky — stát je horší než jakýkoli pokrok; příšera nemůže stát v buňce předmětu,
  takže skok ze šikmého sousedství nad předmět zkrátí jen 1,41 → 1). Když příšera jde
  (`Moving`/`Starting`), i proti chůzi o `JumpGain` (0,5 buňky; jinak by skákala na plošinky, i když
  cíl je dole): buňka +1 se bere jako dostupná (zeď před ní spustí detekci znovu, skok neuteče); pro
  kandidáty od +2 dál se testuje buňka +2 (volno + podlaha) — projde-li, bere se chůze jako možná až
  na x kandidáta, jinak jen o 1 buňku. Vyhrává nejkratší vzdálenost. Vzdálenosti v buňkách jsou
  diskrétní a každý skok je ostře zmenšuje → nezacyklí se.
- **Dopadový pivot (fyzika):** střed buňky dopadu, ve stejné výšce nad spodkem buňky jako při odrazu
  (podlahy předpokládáme na hranách buněk, stejně jako detektor).
- **Výška skoku:** čas letu = |dx| / `maxSpeedAir` + rozjezd; za něj musí být pivot ve výšce dy
  (`vy = (g·t² + 2·dy) / 2t`), u skoku nahoru navíc vrchol aspoň dy. Vždy + `JumpExtra` (0,25 m),
  strop `jumpHeight`. Motor: `ChLegsArms.desiredJumpHeight` (0 = `jumpHeight`, po odrazu se nuluje).
- **Řízení x ve vzduchu:** plnou rychlostí `ChSettings.maxSpeedAir` (0 = `maxSpeed`) k cíli, brzdná
  křivka s polovičním zrychlením motoru. U skoku nahoru se příšera nepustí do sloupce cíle (hrana −
  půl šířky − rezerva), dokud by vlezla pod plošinu (vršek těla pod spodkem plošiny + rezerva) —
  případně nejdřív couvne. Když tělo do sloupce cíle zasahuje už při startu (typicky těsně za koncem
  plošiny nad hlavou), nejdřív po zemi vyjde ven (fáze `Prepare`, chůzí, nejdéle 1 s) a odrazí se
  až pak — ve vzduchu by to nestihla: hlava je ~0,5 m pod plošinou, stoupá ~6 m/s (≈ 4 fixed kroky),
  ale tělo musí uhnout ~0,15 m z rozjezdu ~1 m/s. Pak hned k cíli, i za cenu nárazu do boku plošiny: po nárazu do stropu
  by na „bezpečnou“ výšku nad plošinou už nemusela doletět. Spodek plošiny = spodek buňky podlahy
  dopadu (plošina se bere jako jedna buňka, buňky se netestují).
- **Konec:** dopad (nohy po odlepení zase na zemi a už nestoupá) nebo timeout (předpokládaný čas letu
  + 0,5 s). Nepovedený skok (náraz v půlce, minutí, nohy se nechytily) se řídí stejně a končí
  stejně. Před odrazem (`Prepare` a `Takeoff`, dokud motor neskočil — nohy můžou na chvíli ztratit
  kontakt) skok zruší opuštění buňky odrazu: odsunutá nebo sjetá z hrany příšera by jinak skočila
  odjinud, než se počítalo. `Takeoff` přejde do `Air` až po odrazu motoru. Po skoku lezení začne rozjezdem.
- **Změna rozkazu:** běžící skok styl dotáhne bez ohledu na `Kind` (`Manual`/`None` styl nevolá —
  zatím neřešeno). `MonsterBrain.IsJumping` — `ThrowItemRule` ve vzduchu nezačne. Blokující pravidlo
  až kdyby bylo potřeba.
- **Small Monster:** `jumpHeight 2`, `maxSpeedAir 4` — spočteno pro sadu `Small` (nejdelší skok (9,0)
  ~2 m výšky odrazu, (1,3) ~1,75 m). K vyladění v editoru.

### Wall follower: `WallFollowerStyle : JumperStyle` (skákání v `Roam`)

Pravidlo ruky z bludišť: příšera se drží stěny (místa, kam nemůže) jednou rukou (`hand` na prefabu:
`Left` / `Right`) a nikde se nepustí → cyklický pohyb. Z boku: pravá ruka při chůzi doprava drží
podlahu (seskakuje z hran), doleva strop (vyskakuje). „Točivost“ se nepoužívá — pravá ruka obíhá
místnost zevnitř proti směru hodin, ostrůvek po směru. Ostatní rozkazy jako skokan. Potřebuje
`AvoidHoles`, jinak by z hran padala. `Roam` se v lezci neotáčí (`ApplyCrawl(in d, flipOnBlock: false)`),
otočku rozhoduje křižovatka. `hand` se v `Cleanup` obnovuje z prototypu.

- **Platná buňka a směr:** platná = buňka pivotu na nohou mimo skok (jinak padá, buňky se ignorují).
  Směr (`heading`) = vektor posledního pohybu mezi platnými buňkami: chůze = rozdíl sousedních, skok =
  skutečný dopad − odraz (i nepovedený skok se vyhodnotí podle toho, kde opravdu skončil). Při volbě
  otočky nebo skoku se nastaví hned na zvolený pohyb. Chůze vpřed ho nemění: na úzké plošině se po
  dopadu nabídne (lezec hranu ještě nevidí), hned narazí na `Edge` ve stejné buňce a další křižovatka
  musí vzít další možnost podle **stejného** směru (vektor skoku). Kdyby se směr přepsal na `(dir,0)`,
  pořadí kandidátů by se změnilo a vznikal cyklus.
- **Křižovatka** (jen na nohou): `Edge` / `Wall`; dopad po skoku; neplánovaný skok (změna y platné
  buňky o 2 a víc — o 1 je ještě chůze); a jen s rukou nahoře po vstupu do nové buňky plošina, která
  začíná o buňku před nebo končí o buňku za (`NewPlatformAhead(dir)` / `(−dir)`).
- **Kandidáti:** chůze vpřed, když není zablokovaná (`Paused` / `Starting` / `Moving` — chůzi ověří
  lezec), chůze vzad (`CanStand`), skoky z obou masek detektoru. Když není nic (jen zablokovaná
  `Edge` / `Wall`), otočí se bez testu — lezec otočku ověří, případně se otočí znovu (jako původní `Roam`).
- **Řazení — úhel bez ohledu na délku:** příšera se vždy točí, co to jde. Třída podle poloroviny vůči
  přímce směru: k ruce → přesně vpřed → od ruky → přesně zpět (odkud přišla, poslední). Uvnitř
  poloroviny znaménko vektorového součinu `cross(a,b) = a.x·b.y − a.y·b.x` (= |a||b|·sin, délky
  nevadí; v otevřené polorovině je rozdíl úhlů < 180°, takže je řazení tranzitivní). Ohmatává se
  zezadu — pravá ruka proti směru hodin. Délka jen při přesně stejném směru (kratší vyhrává).
  Vzdálená plošina pod ostřejším úhlem vyhraje nad bližší — příšera to vezme zkratkou.
- **Uvěznění:** zvláštní logika není. Nepovedený skok zpět do stejné buňky nechá směr na vektoru
  skoku, takže další křižovatka dá přednost kandidátům na straně ruky od něj (příšera se točí dál,
  typicky se vrátí). Když jde jen otočka a skok, který se nedaří, střídá je — oscilace o půl kruhu.

---

## Featura: vyhýbání se nebezpečí

> Implementováno 2026-10-17, čeká na test v editoru.

`AiSettings.DangerKsid` (Small Monster: `SmallMonsterDanger` = `PoisonGas`, `HotBlock`; `Unknown` =
ničemu). Příšera nevleze do buňky s takovým objektem (vč. potomků Ksid) a nedoskočí na ni.

- **Test buňky** `MapBase.ContainsType(cell, danger, exclude)` — projde placeably buňky (prázdná buňka =
  hned ne). Obě z roviny: plyn nemá `CellBlocking`, rovinu nepoznáme.
- **`GetJumpCellClass(…, danger)`** testuje nebezpečí **před flagy** (plyn je `Free`, horký blok má
  `HasFloor`) a vrací `Other` — automat beze změny: mezera i `#` selžou (skrz plyn neskočí, na
  horký blok ani do plynu nedoskočí), `x` a `?` projdou (skok může nebezpečí v `?` „škrtnout“;
  čtvrtá třída `Danger` až kdyby to vadilo).
- **Chůze** (`CrawlerStyle.WantMove`): blízko další buňky (`IsXNearNextCell`) test buňky těla
  (dir,0) a podlahy (dir,−1) před sebou → `Edge`. `Roam` se otočí, skokan v `GoToward` zkusí
  nebezpečí přeskočit. Nezávislé na `AvoidHoles`.
- **Už stojí v nebezpečí** (`MonsterBrain.ActiveDanger`: buňka pivotu nebo podlaha pod ní) → test se
  úplně vypne (chůze i skoky), ať se nezastaví uprostřed větší nebezpečné oblasti a vyleze z ní.
- **Spouštěče** detailní detekce (`NewPlatformAhead`) nebezpečí netestují — mají zůstat levné.
  Test chůze o 2 buňky ve výběru skoku (`CanStand`) ano.
- Maska skoků se cachuje 1 s → pohyb plynu se projeví se zpožděním.
- Později: předfiltr přes sekundární mapu (nebezpečné objekty na hrubé mřížce), až bude test drahý;
  smysl „nejbližší nebezpečí“ + pravidlo útěku pro nebezpečí, které se blíží.

---

## Featura: létání

> Implementováno 2026-10-27 (`FlyingStyle`, `FlightPattern`), čeká na test v editoru. První konzument:
> Balloon Monster (1,5 × 1,5 m, double-cell). Nebezpečí (`DangerKsid`) létavec zatím nehlídá.

`FlyingStyle : MonsterBrain` — přímý potomek mozku, z lezce nic nepotřebuje. Motor ve `Free` režimu
(`ChSettings.DefaultMovementMode`): gravitace vypnutá, plný 2D regulátor k `desiredVelocity`
s limitem `maxAccelerationFloat` za krok (balon 0,1 → a ≈ 5 m/s²), rychlost `maxSpeedFloat · SpeedScale`.

**Heading** (jednotkový směr, `heading` na prefabu = počáteční směr, obnovuje se v `Cleanup` z prototypu)
+ **křivka** v jeho soustavě. Kód je pro všechny rozkazy stejný, liší se jen stáčení headingu a brzdění:

| Rozkaz | Heading | Křivka |
|--------|---------|--------|
| `Stop` | — | visí, `v = 0` |
| `Roam` | stáčí ho jen člen křivky s `RotateHeading` | `RoamPattern` |
| `GoDirection` | stáčí se k `Direction` (2D) | `TowardPattern` |
| `GoToward` | stáčí se k cíli; u cíle brzdí a neodráží se | `TowardPattern` |

Stáčení je konstantní rychlostí `TurnRate` (kruhový oblouk), po odrazu `BounceHold` s letí rovně
po odražené trajektorii. U cíle jen zpomalí a nechá se přisunout co nejblíž (zatím bez „kroužení nad cílem“).

**Brzdění u cíle** si příšera spočítá sama: `d` = vzdálenost cíle od těla − `ArriveMargin` (výchozí 0),
rychlost `min(V, √(2·0.8a·d))` — zastaví v `ArriveMargin`, brzdí o 20 % mírněji, než motor umí (reaguje
o krok později). Neodráží se, když `d < V²/(2a) + BounceMargin` — tam by ji jinak odrazila překážka,
na které cíl leží (předmět na zemi).

### Křivka — epicykly (`FlightPattern`)

```
u = h + Σ Aᵢ · rot(h, φᵢ)        φᵢ += ωᵢ·dt,  ωᵢ = 2π/Periodᵢ (znaménko = směr)
v = V · u / (1 + Σ Aᵢ)            → |v| ≤ V
```

Rotující vektory jsou vztažené k headingu — obrazec se natáčí se směrem letu (i po odrazu). Jeden člen =
trochoida: dopředu V/(1+A), smyčky od A > 1, poloměr `r = V·A/((1+A)·ω)`, dostředivé zrychlení
`V·A·ω/(1+A)` musí být ≤ a, jinak motor obrazec zploští. **Brzdění u cíle zmenšuje V → i smyčky → nálet
po spirále.** Příklady (V = 2): smyčky (1.5, 2 s); had = (0.6, 1.6 s) + (0.6, −1.6 s, fáze 180°);
květina (1.2, 2 s) + (0.5, −0.7 s); velké oblouky = člen `RotateHeading` s periodou.

**`Chaos`** — jediný mechanismus náhodnosti: ω i A každého členu × `(1 + Chaos·N(t))`, N = Perlin
v ⟨−1, 1⟩ s měřítkem `NoiseTime`, seed per příšera (hejno se nesynchronizuje). `Chaos > 1` občas otočí
směr rotace — u `RotateHeading` členu to dává bloudění. `Chaos = 0` = přesná, předvídatelná křivka.

### Odraz (měkký — jen povoleným zrychlením)

- **Detekce:** `BoxCast` obdélníkem těla (`Size − skin`) ve směru požadované rychlosti, v z přes roviny,
  které příšera zabírá (double-cell obě). Do všeho v `CollisionLayaerMask`, kromě sebe a drženého předmětu.
  Délka = brzdná dráha skutečné rychlosti + `BounceMargin`. Collidery, které box už na začátku překrývá
  (vzdálenost 0), se ignorují — zasáhly by v každém směru a příšera by se na místě odrážela tam a zpět.
  Box má ostré rohy, tělo je zaoblené, takže k překryvu dojde hlavně na rozích terénu; když do takové
  překážky tlačí, odrazí ji detekce zaseknutí.
- **Spoušť:** překážka blíž než `vn²/(2a) + BounceMargin`, kde `vn` je normálová složka skutečné rychlosti —
  šikmo se odrazí blíž ke zdi, při klouzání podél ní vůbec. Pak:
  1. **odraz** headingu (jednorázově, jen když heading míří do překážky),
  2. **ořez** složky požadované rychlosti do překážky (vždy — křivka nedrhne o zeď).
- **Styly** (`AiSettings.BounceStyle`): `Exact` podle normály; `Axis` podle normály zaokrouhlené na osu
  (na šikmé ploše, kam heading nemíří, ta druhá) a heading srovná na ±`FixedAngle` vůči podlaze — úhel se
  nastavuje jen při odrazu, v `GoToward` se nedrží; `Random` podle normály ± `BounceSpread`, aspoň 15° od povrchu.
  Bez náhodnosti se může odrážet tam a zpět — záměr.
- **Zaseknutí** (cast nic neviděl, strčil do ní hráč): skutečná rychlost < 25 % požadované po 0,5 s →
  odraz od protisměru pohybu (jako lezec, který vejde do zdi).
- Později: tvrdý odraz nohama (jako skok od stěny).

### Navazující úpravy

- **`TrackApproach`:** k trpělivosti pravidla se přičítá `virtual ApproachSlack` stylu — létavec:
  nejdelší |Period| členů `TowardPattern` + `BounceHold` (smyčky a odrazy zdrží pokrok).
- **Double-cell:** `ChLegsArms.TryCorrectZPos` u double-cell držitele předmět mezi rovinami nepřesouvá
  (příšera žádnou rovinu nepreferuje). Dosahy (`Reach`) se měří ve 2D od středu — u velké příšery stačí
  větší hodnota v konfiguraci. Lezec/skokan (`WantMove`, `DangerToAvoid`, `JumperStyle`) dál počítá
  s pivotem dole a tělem o jedné buňce — pro velké lezce by se musel zobecnit.
- **Ladění (F8):** azurová = heading, modrá = požadovaná rychlost.
- Později: sakády (moucha — skokové otočky headingu brané jako odraz), `OnValidate` kontrola
  dostředivého zrychlení členů, kroužení nad cílem.

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
5. **Smysly** — `SenseId` roste podle konzumentů (zatím `Loot`, `Target`); sekundární mapa je
   hotová jako parametr `SecondaryMap`. Editor validace čtení slotu s `Kind == None` zatím chybí (duplicitní slot
   v profilu už varuje).
6. Případně `End()`/cleanup u pravidel (zatím odloženo, viz Mozek).

## Implementační pořadí

1. **Refaktor `ChLegsArms`** — ✅ v podstatě hotový. `MovementMode` (`Legged`/`Free`, gravitace
   off) je v kódu včetně přepínání za běhu; zobecnění počtu končetin je odloženo (nikdo ho
   nežádá); public API pro AI (`desired*` settery, `DropAllLimbs`) se dodělá až s krokem 2, kdy
   bude vidět, co pravidla potřebují. `Free` má konzumenta — [létání](#featura-létání) (balon), čeká na test.
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
8. (později) povrchový režim, candidate scoring, push-trigger probouzení, `End()` cleanup.
