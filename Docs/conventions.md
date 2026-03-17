# Konvence a systémy v GamePrototype

Přehled specifických vzorů a konvencí používaných v tomto projektu. Dokument popisuje **proč** každý systém existuje a **jak** se správně používá.

---

## 1. KSID — Hierarchický typový systém

### Proč existuje
Unity `GetComponent<T>()` a C# `is` operátor jsou pomalé a vyžadují konkrétní třídy. KSID umožňuje rychlé dotazy jako „je tento objekt nějakým druhem `SandLike`?" bez alokací a s podporou hierarchie.

### Jak funguje
`Ksid` je prostý enum s ~44 hodnotami (`StoneBlock`, `Character`, `Rope`, `SandLike`, …). `KsidDependencies` definuje rodičovské vztahy jako tuply `(child, parent)`:
```csharp
(Ksid.Rope,  Ksid.Catch)       // Rope je druh Catch
(Ksid.Stone, Ksid.SandLike)    // Stone je druh SandLike
```
`Ksids` tyto vztahy zpracuje, každý `KsidNode` si při prvním dotazu předpočítá bitový vektor všech svých předků, a pak `IsMyParent(parent)` je jen jedno bitové AND.

### Jak používat
```csharp
// Extension metody na Ksid — přes KsidGet property na Label:
if (body.KsidGet.IsChildOf(Ksid.ActivatesByThrow)) { … }
if (label.KsidGet.IsChildOfOrEq(Ksid.SpMoving)) { … }

// Nebo přes Ksids přímo (pro složitější dotazy):
var ksids = Game.Instance.Ksids;
if (ksids.IsParentOrEqual(label.KsidGet, Ksid.Explosive)) { … }
```
Nový typ: přidat hodnotu do enumu `Ksid` a (volitelně) vazbu do `KsidDependencies`.

> **Pravidlo:** Cykly v hierarchii jsou zakázané — systém při startu hodí výjimku.

---

## 2. Zamezení alokací

Výkonnostní pravidlo: za normálního běhu hry nesmí docházet k GC alokacím. Systém nabízí tři nástroje.

### 2a. ListPool\<T\>
Stack-based pool pro dočasné listy. `try/finally` se nepoužívá — při výjimce se list nevrátí do poolu, což je akceptovatelné (pool si vytvoří nový při příštím `Rent()`):
```csharp
var placeables = ListPool<Placeable>.Rent();
map.Get(placeables, myCell, Ksid.SpNodeOrSandCombiner, ref tag);
foreach (var p in placeables) { … }
placeables.Return();   // extension metoda; Clear() je součástí Return()
```

### 2b. ObjectPool / ConnectablesPool
Pool pro `MonoBehaviour` objekty (RbLabel, RbJoint, …). Objekty se uloží přes `Store()` a vytáhnou přes `Get()`. Pool hlídá stáří (age-counter) — objekt nelze znovu použít ve stejném fyzikálním framu, ve kterém byl uložen.

```csharp
// Uložit (volá se z Label.KillMe() automaticky, pokud objekt má Prototype):
Game.Instance.Pool.Store(this, prototype);

// Vzít přímo z poolu (nebo Instantiate, pokud prázdný):
Game.Instance.Pool.Get(prototype, parent, position);

// Nebo přes extension metodu Create() — volá Pool.Get + Init():
Game.Instance.PrefabsStore.Stone.Create(parent, position, map);

// Pro ConnectableLabel (RbJoint atd.) — používá ConnectablePool:
Game.Instance.PrefabsStore.RbJoint.CreateCL(parent);
```
Pool musí dostávat `UpdateAgeAtPhysicsUpdate()` jednou za fyzikální frame — dělá to automaticky `Game.FixedUpdate()`.

### 2c. NumberToString
GUI zobrazuje hmotnosti, síly atd. Místo `ToString()` (alokace!) používá předvypočítané tabulky:
```csharp
string s = NumberToString.Convert(1234);  // "1.2k" — žádná alokace
```
Rozsahy: −100…204 (přesně), 205…994 (jedno desetinné místo), 950…9949 (tisíce), 9500…100 000.

---

## 3. Systém timerů a událostí

Timery jsou záměrně implementovány **bez alokací**. Platí pravidlo: callback předávaný do `Plan()` nesmí být lambda vytvořená na místě — každé volání by alokovalo nový objekt na heapu. Lambdy (a delegáty obecně) se ukládají jako **statický nebo instanční field** a předávají se jako hotové reference.

Pro `ISimpleTimerConsumer` to zajišťuje `TimerHandler` sám (má statický interní delegate), takže volající je jednoduše `this.Plan(6f)`. U `GlobalTimerHandler` je třeba předat callback zvenku — ten musí být uložen do fieldu, typicky jako method group:

```csharp
// ŠPATNĚ — nová alokace při každém volání:
Game.Instance.GlobalTimerHandler.WithKsidFloatParams.Plan(0.3f,
    (label, dmg, i) => label.ApplyDamage(dmg, i), label, damageType, intensity);

// SPRÁVNĚ — method group uložená ve statickém fieldu:
private static Action<Label, Ksid, float> applyDamageAction = ApplyDamage;

Game.Instance.GlobalTimerHandler.WithKsidFloatParams.Plan(
    0.3f, applyDamageAction, label, damageType, intensity);
```

---

### 3a. Timer (BinaryHeap)
Základní plánovač. Callback za `delta` sekund:
```csharp
Game.Instance.Timer.Plan(callback, delta, param, token);
```
Výjimky v callbacku jsou zachyceny a zalogovány — timer se nezastaví.

---

### 3b. ISimpleTimerConsumer + ActivityTag — zrušitelný timer

Problém: callback může přijít na objekt, který mezitím změnil stav nebo byl smazán.
Řešení: objekt drží **verzi (tag)** a callback se provede jen pokud tag souhlasí.

> **Důležité:** `TimerHandler.Plan()` vždy **před** naplánováním inkrementuje `ActiveTag` objektu. Tím automaticky zneplatní jakýkoli předchozí čekající timer. Platí pro obě varianty — ruční `int` i `ActivityTag4/8` (přes property `ActiveTag`).

#### Varianta s ručním `int` tagem
Nejjednodušší forma — objekt implementuje `ISimpleTimerConsumer` (drží `ActiveTag`) a naplánuje se pomocí extension metody `this.Plan(delta)`:
```csharp
// Naplánovat (StickyBomb2 — odpálení za 10 s):
this.Plan(10f);

// Zrušit čekající callback bez přeplánování:
activeTag++;   // stačí inkrementovat; pending callback pak nesouhlasí
```

#### Varianta s ActivityTag4 / ActivityTag8
Kompaktní state machine v jednom `int`. `ActivityTag4` má 4 vnitřní stavy (bity 0–1), `ActivityTag8` má 8 stavů (bity 0–2). Zbytek bitů je verze pro zneplatnění timerů.

**Konvence stavů:**
- Stav `0` = neaktivní / mrtvý
- Init: přejdi na stav `≠ 0` (a volitelně hned naplánuj timer)
- Cleanup: zavolej `Reset()` — okamžitě inkrementuje verzi a nastaví stav na `0`, čímž zneplatní všechny čekající callbacky

```csharp
// Explosion.cs — zkráceno:
private ActivityTag4 activityTag;

public override bool IsAlive => activityTag.IsActive;
public int ActiveTag { get => activityTag.Tag; set => activityTag.Tag = value; }

public override void Init(Map.Map map) => this.Plan(0.1f);
// this.Plan() inkrementuje ActiveTag: Tag 0→1, State=1, IsActive=true

public void OnTimer()
{
    if (activityTag.State == 1)      // první výbuch
    {
        ApplyExplosionEffects();
        this.Plan(6f);               // Tag 1→2, State=2
    }
    else                             // State==2: zhasni a smaž
    {
        Kill();
    }
}

public override void Cleanup(bool goesToInventory)
{
    activityTag.Reset();   // Tag = (Tag & ~Mask) + BigIncrement → State=0, IsActive=false
    base.Cleanup(goesToInventory);
}
```

Každé volání `this.Plan()` inkrementuje tag, čímž se vždy automaticky zneplatní předchozí čekající callback. Stav (dolní bity tagu) funguje jako mini state machine — každé přeplánování posune stav o 1.

---

### 3c. GlobalTimerHandler — timer s automatickým zrušením při smrti objektu

Pokud objekt může být smazán (`Cleanup()`) dříve, než timer vyprší, a není žádoucí ručně spravovat tag, použij `GlobalTimerHandler`. Stačí předat živý `Label` — při jeho smazání se event automaticky zahodí.

```csharp
// StaticBehaviour.cs — reálné použití:
private static Action<Label, Ksid, float> applyDamageAction = ApplyDamage;

public static void ApplyDamageDelayed(this Label label, Ksid damageType, float intensity)
{
    if (label.KsidGet.IsChildOf(damageType))
    {
        Game.Instance.GlobalTimerHandler.WithKsidFloatParams.Plan(
            0.3f, applyDamageAction, label, damageType, intensity);
    }
}

public static void ApplyDamage(this Label label, Ksid damageType, float intensity)
{
    // … aplikuj poškození …
}
```

Pro vlastní kombinaci parametrů je třeba připravit `ParamsHandler<T1, T2>` (typovaný wrapper, součást `GlobalTimerHandler`).

---

### 3d. IActiveObject1Sec + GameUpdates1Sec
Objekty implementující `IActiveObject1Sec` dostávají `GameUpdate1Sec()` přibližně jednou za sekundu, ale zátěž je rozložena po framích (ne vše najednou). Registrace:
```csharp
Game.Instance.ActivateObject(obj as IActiveObject1Sec);
```

---

## 4. IActiveObject — herní update loop

Unity `Update()` se nepoužívá pro herní objekty. Místo toho `Game.cs` volá explicitně:

```
Game.Update()
  ├─ InputController.GameUpdate()
  ├─ UpdateTriggers()
  ├─ UpdateMovingObjects()
  ├─ UpdateObjects()          ← všechny registrované IActiveObject
  └─ Timer.GameUpdate()

Game.FixedUpdate()
  ├─ IActiveObject.GameFixedUpdate()
  └─ StaticPhysics (background thread sync)
```

Každý `Placeable` se sám zaregistruje při `PlaceToMap()` a odregistruje v `Cleanup()`. Přímé volání `Update()` je anti-pattern.

---

## 5. Map — prostorová mřížka

### Buňky
Svět je rozdělen na buňky 0.5 × 0.5 × 0.5 m. Každá buňka (`Cell`) obsahuje seznam `Placeable` objektů a příznak blokování (`CellFlags`). Jeden fyzický slot buňky jsou ve skutečnosti dvě pod-buňky (Cell0/Cell1) oddělené osou Z.

### CellList — pool paměti pro buňky
Buňky jsou v gridu, ale jejich obsah (`Cell.listInfo`) ukazuje do sdíleného poolovaného pole. Kapacity jsou mocniny 2 (2, 4, 8, …, 64). Při přidávání objektu se array podle potřeby zvětší, při ubírání zmenší — bez GC alokací.

### Add / Remove / Move
`Map.Add(p)` vypočítá rozsah buněk z pozice a velikosti objektu a přidá `p` do každé z nich. `Move(p)` optimalizuje — aktualizuje jen buňky, které se změnily. Při každé změně se přepočítají `Blocking` příznaky a spustí trigger logika.

### MapWorlds — 6 světů
6 map existuje naráz (každá posunuta o `WorldOffset`). `MapWorlds` spravuje jejich asynchronní načítání a přepínání. `MapFromPos(posX)` vrátí správnou mapu podle světové X souřadnice.

---

## 6. Attacheable RigidBody — stavy fyziky objektu

`Placeable` může být v jednom ze čtyř fyzikálních stavů:

```
[Bez RB — v mapě, žádná fyzika]
        │  AttachRigidBody(startMoving=true)          ◄─────────────────────────┐
        ▼                                                                        │ SandCombiner.Collapse()
[Aktivní Unity RigidBody]      ←──────────────────┐                             │ (rozpad → pohyblivé objekty)
   - RbLabel jako rodič                           │                             │
   - isKinematic = false                          │  SpFall() / SpBreakEdge()   │
   - padá/letí, reaguje na nárazy                 │                             │
   - možné FixedJoint spoje (RbJoint)             │           ┌─────────────────┘
        │  AddNode + AddJoint                     │           │  drobné objekty zaplní buňku
        ▼                                         │           │  a přestanou se hýbat
[Součást StaticPhysics]  ─────────────────────────┘           ▼
   - SpNodeIndex přiřazen                              [SandCombiner]
   - GraphWorker řeší omezení                            - nepohyblivý objekt v mapě (bez RB)
   - při překročení limitů: FallNode/FallEdge output     - uvězní drobné objekty, odebere jim RB
                                                         - Map sleduje, kdy se má rozpadnout
                                                         - přijatá síla → Collapse() → RB
```

### Kinematické RB
Objekt může mít RB, ale `isKinematic = true` — nepohybuje se, Unity ho nesimuuje. Takový RB existuje čistě kvůli **RbJointům**: když jiný pohyblivý objekt potřebuje se k tomuto přichytit pomocí `FixedJoint`, oba konce joinu musí mít RB. Jakmile se všechny joiny odpojí (`connectionCounter == 0`), kinematický `RbLabel` se sám zruší.

### Přechody
- `AttachRigidBody()` — vytvoří `RbLabel` (z poolu), přesune `Placeable` jako dítě, nastaví hmotnost.
- `DetachRigidBody()` — přesune `Placeable` zpět na `LevelGroup`, zničí `RbLabel` (do poolu).
- `AddSpNode()` / `SpInterface.AddInCommand()` — registrace do statické fyziky.
- `SpFall()` — StaticPhysics zjistila, že uzel nemá oporu → přejde na aktivní RB.

### RbLabel a connectionCounter
`RbLabel` drží `connectionCounter`. Každý `RbJoint` při připojení inkrementuje, při odpojení dekrementuje. Jakmile counter == 0 a objekt je kinematický, `RbLabel` se samo zničí.

### SandCombiner
Drobné pohyblivé objekty (písek, kamínky) jsou fyzikálně drahé. Když se takové objekty přestanou hýbat a **zaplní celou buňku**, mapa je agreguje do jediného `SandCombiner` objektu — nepohyblivého, bez RB. Původní objekty jsou „uvězněny" uvnitř a zbaveny svých RB.

Map průběžně hlídá stav buněk (fronta `CellStateTests` zpracovávaná v `MapWorlds.ProcessCellStateTests()`). Pokud podmínky pro SandCombiner přestanou platit — např. objekt odvedle se pohnul a uvolnil místo — SandCombiner se rozpadne zpět na individuální pohyblivé objekty (`Collapse()`). Přijatá síla (náraz, výbuch) collapse také spustí.

---

## 7. Statická fyzika (SpInterface)

Fyzikální solver pro pevné konstrukce (zdi, mosty, lana) běží na **background threadu**.

### Komunikace: triple-buffer front příkazů
```
Main thread                         Worker thread
────────────────────────────────────────────────────
publicInCommands  ──swap──►  waitingInCommands
                                    │  swap
                              privateInCommands
                                    │
                              GraphWorker.ApplyChanges()
                                    │
                              privateOutCommands
                                    │  swap
publicOutCommands  ◄──swap──  waitingOutCommands
```
Main thread nikdy nesmí přímo přistupovat k `SpDataManager` ani workerům.

### Typy příkazů (InputCommand)
| Příkaz | Popis |
|--------|-------|
| `AddNode` | Přidat objekt do simulace (pozice, gravity force, isFixed) |
| `AddJoint` | Spojit dva uzly (stretch/compress/moment limity) |
| `AddNodeAndJoint` | Atomicky přidat uzel a spoj |
| `RemoveJoint` | Zrušit spoj |
| `RemoveNode` | Odebrat uzel |
| `UpdateForce` | Změnit persistentní sílu (gravitaci písku) |

### Výstupy (OutputCommand)
| Příkaz | Reakce main threadu |
|--------|---------------------|
| `FallNode` | `nodeA.SpFall()` — přejde na Unity RB |
| `FallEdge` | `nodeA.SpConnectEdgeAsRb()` — vytvoří FixedJoint |
| `RemoveJoint` | `nodeA.SpBreakEdge()` — vizuální efekt lomu |
| `FreeNode` | Uvolní index uzlu zpět do poolu |

### Síly
- **Persistentní** (gravitace písku): `ApplyForce(spNodeIndex, force)` — zůstane do `UpdateForce`/`RemoveNode`.
- **Dočasné** (náraz): `ApplyTempForce(spNodeIndex, velocity, mass, flags)` — platí jen jeden krok.

---

## 8. Systém Connectables

### Co je Connectable
Každý `Placeable` má seznam `Connectable` komponent. Každý `Connectable` reprezentuje jedno „místo připojení" — může být volný (`Off`) nebo obsazený jedním ze stavů:

| Typ | Popis |
|-----|-------|
| `Physics` | RbJoint — Unity FixedJoint nebo SpJoint |
| `LegArm` | Ruka/noha postavy (IK target) |
| `MassTransfer` | Přenos hmotnosti (písek na objekt pod ním) |
| `StickyBomb` | Přilepená výbušnina |
| `OwnedByInventory` | Předmět je v inventáři |

### Životní cyklus
```csharp
// Init v Start():
connectable.Init(() => transform);   // callback vrátí transform pro uložení po odpojení

// Připojení:
connectable.ConnectTo(target, ConnectableType.Physics, worldPositionStays: true);

// Odpojení:
connectable.Disconnect();   // přesune objekt na storage transform, Type = Off
```

### RbJoint — fyzikální spoj
Vytváří se vždy po dvou (jeden na každém konci):
```csharp
// Placeable.cs:
RbJoint myJ    = Game.Instance.PrefabsStore.RbJoint.CreateCL(ParentForConnections);
RbJoint otherJ = Game.Instance.PrefabsStore.RbJoint.CreateCL(to.ParentForConnections);
myJ.Setup(this, to, otherJ);
otherJ.Setup(to, this, myJ);
```
`Setup()` si vybere cestu:
- **SetupRb()** — oba objekty dostanou RB (nebo použijí existující), vytvoří se `FixedJoint`.
- **SetupSp()** — přidá uzly do statické fyziky (`AddNodeAndJoint` příkaz).

### MassTransfer a SandCombiner
`SandCombiner` agreguje písek v buňce do jednoho objektu. Když detekuje objekt pod sebou, vytvoří `MassTransfer` spojení a aplikuje persistentní sílu dolů přes `StaticPhysics.ApplyForce()`. Při pohybu nebo kolapsu se spojení ruší a síla se odebere.

---

## 9. Trigger systém

`Trigger : Placeable` se automaticky aktivuje, když do jeho buněk vstoupí objekt odpovídající `Trigger.Targets` (Ksid filtr). Vše řídí mapa — `Cell.Add()` volá `TriggerAddTest`, `Cell.Remove()` volá `TriggerRemoveTest`.

```csharp
trigger.TriggerOnEvent     += OnActivate;   // poprvé se cokoli dotklo
trigger.NewObjectsEvent    += OnNewObject;  // každý nový příchozí
trigger.ObjectsRemovedEvent += OnLeft;      // někdo odešel
trigger.TriggerOffEvent    += OnDeactivate; // trigger je zcela prázdný
```
Interně se používá ref-counting (objekt může být ve více buňkách triggeru naráz).

---

## 10. Cleanup vzor

Každý herní objekt musí po sobě uklidit. Pořadí v `Cleanup()`:
1. `OwnedByInventory` spojení: pokud `goesToInventory == true`, ponechat; jinak odpojit jako ostatní.
2. Odpojit všechna ostatní `Connectable` spojení.
3. Odebrat se z mapy (`map.Remove(this)`).
4. Odregistrovat z `GlobalTimerHandler` (`OnObjectDied(this)`).
5. Odregistrovat `IActiveObject` (`Game.Instance.DeactivateObject(this)`).

Nikdy nevolej `Destroy()` přímo na herní objekty — použij `Kill()` z `Label`.

---

## 11. PrefabsStore — továrna na objekty

`PrefabsStore` je `ScriptableObject` s referencemi na všechny prefaby. Vytváření objektů:

```csharp
// S přidáním do mapy:
var stone = Game.Instance.PrefabsStore.Stone.Create(parent, position, map);

// Bez mapy (connector, joint atd.):
var joint = Game.Instance.PrefabsStore.RbJoint.CreateCL(parent);
```

Extension metody `Create<T>` a `CreateCL<T>` jsou na `Label`. Nikde jinde se `Instantiate()` nevolá přímo — vždy přes PrefabsStore nebo pool.

---

## Přehledová tabulka

| Systém | Klíčové třídy | Složka |
|--------|--------------|--------|
| Typová hierarchie | `Ksid`, `Ksids`, `KsidNode` | `Core/` |
| Pool listů | `ListPool<T>` | `Utils/` |
| Pool objektů | `ObjectPool`, `ConnectablesPool` | `Utils/` |
| GUI stringy | `NumberToString` | `Utils/` |
| Timery | `Timer`, `GlobalTimerHandler`, `ActivityTag` | `Utils/` |
| Herní loop | `IActiveObject`, `GameUpdates1Sec` | `Core/` |
| Mapa/buňky | `Map`, `Cell`, `CellList`, `MapWorlds` | `Map/` |
| Fyzika stavů | `Placeable`, `RbLabel`, `RbJoint` | `Bases/` |
| Statická fyzika | `SpInterface`, `GraphWorker` | `Core/StaticPhysics/` |
| Propojení | `Connectable`, `ConnectableLabel` | `Bases/` |
| Triggery | `Trigger` | `Bases/` |
| Továrna | `PrefabsStore` | `Map/` |
