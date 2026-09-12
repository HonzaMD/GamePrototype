# Refaktor ChLegsArms — stav a další fáze

Refaktor motoru `ChLegsArms` rozdělený do tří kýblů. **Kýble A a B jsou hotové**, kýbl C
je protříděný (většina bodů zrušena nebo odložena, `MovementMode` hotový). Dokument nese
analýzu, aby se dalo pokračovat bez předchozího kontextu.

Souvisí s [priser-framework.md](priser-framework.md) — celý příšeří framework
(`MonsterController` → styly pohybu) na `ChLegsArms` staví.

---

## Kýbl A — zpřehlednění (HOTOVO)

Cíl byl čistě čitelnost, **bez jediné změny chování**. Commity `Refaktor A/1`–`A/5`.

1. Úklid mrtvého kódu (`using UnityEditor`, mrtvé pole `mClose`, zakomentované bloky).
2. Sjednocení názvosloví — jedno sloveso na akci (tabulka níže).
3. Rozseknutí velkých metod (`GameFixedUpdate` 75 ř. → 8 fází, `AdjustLegsArms`,
   `TryCatchArm`, `ApplyHoldForce`, damage metody).
4. Rozdělení na `partial class` do 4 souborů.
5. Hlavičkový komentář s tokem řízení a automatem končetiny.

### Konvence sloves (dodržovat i dál)

| Sloveso | Význam |
|---|---|
| `Try…` | může selhat, vrací `bool`/enum |
| `Attach` / `Detach` | **jediný** pár pro vazbu končetina↔objekt (`Connectable` + `limbTargets` + `limbStatus`) |
| `Place` | **jen** zápis pozice/rotace transformu končetiny |
| `Catch` | cílový **stav** Catch — noha stojí / ruka se drží povrchu |
| `Hold` | cílový **stav** Hold/PickUp — ruka nese předmět |
| `Apply` | aplikace síly / poškození |
| `On…` | callback (timer, `Connectable`) |
| `Disconnect` | **vyhrazeno** pro cizí `Connectable`/`IConnector` API, nikdy pro naše končetiny |

Zrušená slovesa: `Release`, `Remove`, `Recatch`, `Connect` (v našem kódu).

### Rozdělení souborů

| Soubor | Obsah |
|---|---|
| `ChLegsArms.cs` | pole, `AwakeB`, oba vstupní body jako seznam fází, virtuální háky |
| `ChLegsArms.Limbs.cs` | stav končetin, připojování/odpojování, hledání opory |
| `ChLegsArms.Hold.cs` | držení, pickup, inventář, kolize, síla držení |
| `ChLegsArms.Forces.cs` | lokomoce, skok, drag, zMove, reakční síly, poškození |

---

## Praktické poznámky k práci s tímhle kódem

- **Kompilaci lze ověřit z CLI**, Unity Editor není potřeba:
  `dotnet build MainGameAsm.csproj` (v kořeni projektu). Vrací reálné chyby
  i warningy. Pro jistotu občas `-t:Rebuild`.
- **`.cs` soubory jsou UTF-8 s BOM + CRLF** a `core.autocrlf=false`, takže CRLF je
  uložené v repu. `sed -i` v Git Bash **zahodí CRLF** a diff pak ukáže celý soubor
  jako změněný. Pro hromadné úpravy použij .NET `File.ReadAllText`/`WriteAllText`
  s `UTF8Encoding($true)`, nebo Edit nástroj.
- **Testy pro `ChLegsArms` neexistují a nejdou napsat** (MonoBehaviour svázaný
  s Unity fyzikou; Edit Mode testy v `Assets/Tests/` jdou jen na statické
  fyzikální třídy). Jediné ověření chování je odehrát checklist:
  chůze/běh/šikmá plocha · skok (i buffer: stisk těsně před dopadem) · chytání rukama
  (pravé tlačítko / `C`), šplhání · `E` pickup, `E`+myš hold, ItemAdjust, drop ·
  `R`+klik hod, ThrowReload, **hod při uměle sníženém FPS (~20)** · nůž (`HoldAnimator`) ·
  `Ctrl` Z-move i s předmětem · inventář klávesami 0–9 i klikem v `InventoryVisualizer`,
  `Tab` · `SmallMonster` chodí a otáčí se na hraně · míření: marker DirtBuilderu
  a šipka hodu sedí pod kurzorem.
- **Perf měření:** `Game.UpdateTimes[3]` (= `UpdateObjects()`) a `UpdateTimes[0]`
  jsou public pole na `Game` → viditelné v Inspectoru za běhu. Plus frekvence
  logu `"## GC ##"` z `Game.LogGC`.
- `Limbs` je serializované pole navázané v `Character3.prefab` a
  `Small Monster.prefab`; drží ho `[FormerlySerializedAs("Legs")]`. Prefaby je
  vhodné jednou otevřít a přeuložit, aby se YAML přepsal na nový název.
  **Pořadí 0,1 = nohy, 2,3 = ruce je nosné.**

---

## Kýbl B — rozdělení GameUpdate / FixedUpdate (HOTOVO)

Původní řez byl „rozhoduj a připojuj v Update, aplikuj síly ve FixedUpdate".
`AdjustLegsArms` je ale čistá fyzika (raycasty, `body.linearVelocity`,
`ComputePenetration`, `AttachRigidBody`, `SetCollisionIgnored`), takže běžela na jiné
frekvenci než stav, který čte i produkuje.

### Kritérium, podle kterého se rozhoduje umístění (platí i dál)

Dělící čára **není** „herní logika vs. prezentace", ale dvě otázky:

1. **Čte nebo píše to fyzikální stav?** (rychlosti RB, síly, raycasty proti pohyblivým
   colliderům, teleport transformu) → FixedUpdate. Výsledek jinak závisí na tom, kam mezi
   dva fyzikální kroky trefíš; a po zapnutí `Interpolate` vrací `transform.position`
   v Update vizuální pózu, ne fyzikální.
2. **Závisí chování na počtu volání, nebo na uplynulém čase?**
   `x -= Time.deltaTime * k` je per-sekunda → bezpečné kdekoli. „Zkus teď najít oporu
   pro nohu" je **per-volání** → jeho frekvence *je* frame rate. Při 144 fps dostane noha
   144 pokusů/s, při 40 fps 40 — postava doslova chodí jinak.

Herní logika v Update je sama o sobě v pořádku. Práce naplánovaná v Update se navíc
**sama škrtí**, když klesne FPS, kdežto práce ve FixedUpdate se **sama zesiluje**
(pomalý frame → víc fixed kroků → pomalejší frame). Pro hru CPU-bound na hlavním vlákně
je Update správný default pro elastickou práci — proto zůstává amortizovaný scheduler
v `Game.cs` (`movingObjectWorkPtr % 20`, `fixedUpdateTicker % 10`, 1s/20s buckety,
`ProcessCellStateTests(10)`) tak, jak je.

### Vstupní buffer (dřívější pracovní název „latch" se nepoužívá)

Buffer patří hráči, ne tělu — žije v `InputController`. Tři vrstvy:

```
InputController   klávesy → akce          SampleInput() → struct PlayerInput
Character3        akce → gesta → desired*  stavový automat (PickupPrepare, Throw, …)
ChLegsArms        desired* → fyzika        (AI příšer vstupuje až tady)
```

`InputController.SampleInput()` je jediné místo, kde se čte herní `Input`. Pole
`PlayerInput` jsou pojmenovaná podle akcí (`PickupPressed`, `PrimaryPressed`), ne kláves.

| Tvar | Význam | Píše / čte |
|---|---|---|
| `desired*` | co má motor dělat | stavový automat / motor |
| `*Held`, osy | level vstup — klávesa je držená | Update / FixedUpdate |
| `*Pressed`, `*Released`, požadavky, akumulátory | hrana, akumuluje se `\|=` | Update / FixedUpdate |

**Snímek místo „kdo čte, ten nuluje".** `InputController.GameFixedUpdate()` běží jako první
v `Game.FixedUpdate` a udělá `frame = pending; pending.ClearEdges();`. `Character3` čte
`ref readonly inputController.Frame`. Všichni čtenáři v kroku vidí totéž; při 0 fixed
krocích za frame se hrany akumulují, při N je dostane jen první.

Level signál hranici mezi frekvencemi snese, stačí ho přepisovat. Hrana ne: při vysokém
fps by ji další Update přepsal a ztratila by se, při nízkém by proběhla vícekrát.
Parametry hrany se zachytí v okamžiku hrany (`PrimaryPressedWithPickup`, `JumpPressTime`,
`throwVector`), nedočítají se později.

**Přepnutí postavy zahodí rozpracované hrany** (`DropInputEdges`, i při odebrání ovládané
postavy). Přenesené by novou postavu nechaly skočit nebo bodnout nožem. Poloviční gesta
(E dole na staré, nahoře na nové) jsou bezpečná, protože `DeactivateInput` volá
`ResetControl` a znovu vybraná postava startuje z `EmptyHands`/`ItemUse`. Level signály
zůstávají — jsou to fyzicky držené klávesy. Opuštěná postava vynuluje `desiredVelocity`,
`desiredCatch` drží dál (visí-li na zdi, nespadne).

**Inventář:** klávesa 0–9 s vybranou položkou v HUD jen přiřadí quick slot — čisté UI,
provede se hned v `SampleInput`. Jinak jde do snímku jako požadavek, stejně jako klik
v `InventoryVisualizer` (`RequestInventoryAccess`). Oba provede postava ve fixed kroku.

`lastJumpTime + JumpBufferTime` (0.3 s) **zůstává** — je to buffer pro hráče, který
zmáčkne skok těsně před dopadem, ne záplata na sync mezi smyčkami.

### Opravený bug: při nízkém FPS se předmět hodil s velmi malou silou

Ruku pouštěl až `DetachArmIfNeeded` v příštím `GameUpdate`, ale hod aplikoval
`GameFixedUpdate`. Při nízkém FPS proběhlo mezi tím 1–3 dalších fixed kroků, ve kterých
`ApplyHoldForce` táhla hozený objekt zpátky k ruce — pro StickyBomb (mass 5, postava
mass 50) ~5,4 m/s ubráno na každý krok proti maximálnímu hodu ~8,9 m/s.

Řeší to pořadí v `Character3.GameFixedUpdate`:

```
1. ControlledFixedUpdate()   // automat, AnimateHand, AdjustLegsArms; ThrowObj pustí ruku HNED
2. base.GameFixedUpdate()    // síly — ApplyHoldForces už na hozenou věc nesáhne
3. ApplyThrow()              // plný impuls
```

`ThrowObj` volá `DetachHold()` (nové v `ChLegsArms.Hold.cs`) **až po `InventoryDrop()`** —
`OnLimbDetached` vrací věc do inventáře, dokud je aktivní. Hack
`if (bodyToThrow == null) base.GameFixedUpdate();` je zrušený, takže se už neobětuje celý
fyzikální krok lokomoce, podpory nohou a dragu.

Míření: `SetThrowActive(false, throwIt: true, …)` volá `UpdateThrowVector` v okamžiku hodu,
před `TryActivateByThrow()`. Dřív se bralo z markeru a bylo o frame staré.

### Co se přesunulo

| Kam | Co |
|---|---|
| → FixedUpdate | `AdjustLegsArms`, celý stavový automat `Character3`, `AnimateHand`, AI + motor `SmallMonster`, `InventoryAccess` z UI |
| → Update | `ThrowController.ShowThrowMarker` + `PositionLongThrowMarker` (prezentace) |
| `deltaTime` → `fixedDeltaTime` | `TickLimbTimers`, `controlTimeout`, `resetHoldTimeout`, `zMoveTimeout`, `SmallMonster.turnTimeout` |
| zrušeno | `Character3.UpdatePosition()` → tím i druhé volání `InputController.GameUpdate()` za frame |

`InputController.GetMousePosOnZPlane` si teď spolu s `mousePosInWord` ukládá i pozici
kamery v okamžiku vzorku (`mouseRayOrigin`). Bez toho by míchal směr paprsku z jednoho
framu s pozicí kamery z jiného — dřív to vycházelo jen díky tomu, že se kamera hýbala
o dva řádky dřív. `mouseSampled` je fallback na první fyzikální krok po odpauzování, který
může předběhnout první `GameUpdate` (`Game.FixedUpdate` nemá kontrolu `State`).

### Odchylky od původního zadání kýblu B

- **`Game.cs` beze změny.** Každá položka obou smyček prošla oběma testy výše.
- **Camera follow zůstal v `InputController.GameUpdate`, LateUpdate se nedělal.**
  Jakmile `AdjustLegsArms` odešel z Update, hýbe transformem postavy už jen FixedUpdate,
  a ten v rámci framu běží **před** Update — pozice je na začátku `Game.Update` finální
  a LateUpdate by nekoupil nic.

### Očekávaná změna feelu

Kadence končetin se přesunula z frame-time na 50 Hz. Délka timeoutů v sekundách se
nemění (`TickLimbTimers` je time-based), mění se **počet pokusů o chycení za sekundu**:
při 144 fps dřív 144/s → nyní 50/s. Počítat s přeladěním `LegTimeout`
(dnes 6.5 postava / 5 příšera) a možná `HoldMoveAcceleration`. Je to důsledek, ne regrese.

`ItemAdjust` je o něco citlivější — pohyb myši se akumuluje přes framy, takže práh 0.05
už neukusuje deadzone při vysokém FPS.

Jump buffer vyprší po `JumpBufferTime` i u opuštěné postavy (`UncontrolledFixedUpdate`).
Skok zmáčknutý těsně před Tabem se tak provede nejvýš do 0,3 s, pak zmizí — dřív se
držel až do dopadu a do té doby postava neměla podporu nohou.

### Interpolace rigidbodies (odblokováno kýblem B)

Všech 8 prefabů má `m_Interpolate: 0` (None) → pohyb je kvantovaný na 20 ms
a při >50 fps znatelně trhá; nejvíc přes kameru, která sleduje neinterpolovanou
postavu, takže cuká celý svět.

Blokoval to `AdjustLegsArms` v Update, který by na interpolovaném RB tiše začal číst
rozmazané pozice. Teď je ve FixedUpdate, takže **`Interpolate` jde zapnout**.
`m_AutoSyncTransforms: 0` znamená, že raycasty interpolace neovlivní.
Nezapínat globálně (CPU budget) — postava + co je v záběru; jde měnit za běhu.

---

## Kýbl C — funkční změny (patří už do frameworku)

Původní seznam byl probrán **lazy**: udělat jen to, co je jednoznačně potřeba nebo
jednoznačně výhodné. Kritéria:

1. Je to **prerekvizita** něčeho, co bude potřeba v [příšerím frameworku](priser-framework.md)?
2. Je to refaktor **bez pochybností o přínosu**?

Rozhodující okolnost: **pro `ChLegsArms` nejdou napsat testy**, jediné ověření je odehrát celý
manuální checklist (viz výše). Každá změna tady tedy stojí plný regresní průchod, ať je jakkoli
malá — čistě kosmetické přepisy jsou dražší, než vypadají. Čitelnost navíc už vyřešil kýbl A,
takže zbytek seznamu byl „aby to bylo hezčí uvnitř", ne „aby se to dalo číst".

| Bod | Prereq? | Nepochybný přínos? | Verdikt |
|---|---|---|---|
| `MovementMode` + `body.useGravity` | ano | ano (nová schopnost) | **hotovo** |
| Public API pro AI (`desired*`, `DropAllLimbs`) | ano | ano | s krokem 2 frameworku |
| Sloučit `TryAttachLimbTo` + `PlaceLimb*` | ne | malý | jen mimochodem |
| Statické bufry → `ListPool` | ne | **ne** (perf proti) | zrušeno → místo toho debug guard |
| `LimbState` enum + `freeTimer` | ne | jen čitelnost, široký dosah | odloženo |
| Generalizace počtu končetin | ne pro v1 | ne (spekulativní) | až to vynutí konkrétní příšera |
| `struct Limb` místo 5 paralelních polí | ne | **ne** (perf proti) | zrušeno |
| Plné rozseknutí `TryHoldOne` | ne | ne (max. riziko, jen cesta hráče) | zrušeno |
| `MonsterController` + `CrawlerStyle` | — | — | není refaktor, je to krok 2/3 frameworku |

### `MovementMode { Legged, Free }` (HOTOVO)

Jediná položka kýblu C, která **přidává schopnost** místo přerovnávání existujícího kódu, a
zároveň test klíčového tvrzení návrhu („jeden motor, let je jen režim"). Čtyři dotyková místa,
větev `Legged` beze změny chování:

| Kde | Co |
|---|---|
| `ChLegsArms.cs` | `public enum MovementMode { Legged, Free }` (mimo třídu — sahají na něj AI pravidla) a `public MovementMode MoveMode` |
| `ApplyMoveForce` | `if (ArmCatched)` → `if (ArmCatched || movementMode == Free)`; ta větev už `legUpDir` nastavuje sama a ve `Free` odpadá raycast v `GetLegRotation` |
| `TryCatchWithFreeLeg` | běží jen v `Legged` — jinak by se noha chytila země, nad kterou jen proletíme, a raycast stojí navíc |
| `ChSettings.DefaultMovementMode` | výchozí režim druhu (`Legged` = 0, stávající assety se nemění) |

**Přepínání za běhu je podporované** oběma směry (balon, který se odlepí od země; dravec, který
dosedne). Setter při skutečné změně přepne `body.useGravity` a při přechodu do `Free` zavolá
`DetachAllLegs()` — nohy chycené před přepnutím by jinak držely `Catch` dál (podpora,
`LegOnGround`, povolený skok) až do překročení vzdálenosti. Zpět do `Legged` se nohy začnou
chytat v nejbližším `AdjustLegsArms`.

`AwakeB` režim inicializuje ze `Settings`, `AfterMapPlaced` ho nastavuje znovu jako **reset po
poolingu** — příšera vrácená do poolu ve `Free` by jinak ožila bez gravitace.

Ruce (chytání, držení, pickup, hod) jedou v obou režimech stejně — to je ten reuse, kvůli
kterému návrh nechce samostatný `FlyMotor`. **Samotné létání zatím není odzkoušené**, `Free`
nemá konzumenta; ověří ho první balon, nebo dočasně přepnutý `DefaultMovementMode`
na `Small Monster`.

### Odloženo

- **Public API pro AI** — v původním seznamu chybělo, přitom je to skutečný prerekvizit:
  pravidla (`IController`/`IModifier`) jsou samostatné objekty a sahají na motor přes `ctx.Ctrl`,
  ale `desired*` jsou dnes `protected`; `SleepController` potřebuje `DropAllLimbs()`
  (= privátní `DetachAllLimbs`). Dělat **až s krokem 2** (`MonsterController`), kdy bude vidět,
  co přesně pravidla potřebují — ne předem a ne jako samostatný regresní průchod.
- **`LimbState` enum + `freeTimer`** — dotkne se všech porovnání ve všech čtyřech souborech
  (`== Catch`, `<= Timeout`, `> Free`, LRU `limbStatus[i1] <= limbStatus[i2]`). „Neomezený
  záporný drift" je přitom **estetický, ne funkční**: při 50 Hz a `LegTimeout` 6.5 klesá o
  ~325/s, takže i po hodině nečinnosti je float daleko od ztráty přesnosti. Framework to nikde
  nepotřebuje.
- **Generalizace počtu končetin** — nic v krocích 1–7 frameworku ji nežádá: `CrawlerStyle` je
  port `SmallMonster` (2+2), létající dravec potřebuje ruce na kořist (2+2), balon nepotřebuje
  nic navíc (nohy vypne `MovementMode`). Cena je vysoká (`PairedLimb = i ^ 1`, natvrdo 2/3 po
  celém `Hold.cs`, `GetLegForce(0)/(1)`, `ApplyReactionToLegs`, `MarkIdleLimbsFree`, serializované
  `Limbs` na dvou prefabech, `Settings.limbZ`) a párová logika se na 4 nohy stejně nezobecní
  indexováním — chtěla by skutečnou chůzovou logiku. **Generalizovat až s druhým konkrétním
  konzumentem**, jinak to vyjde navržené špatně.
- **Sloučení `TryAttachLimbTo` + `PlaceLimb*`** — drží invariant „po úspěšném attachi je
  končetina vždy umístěná a má stav", ale je to 5 volání a varianty se liší (`OwnZ` vs `HitZ`,
  `Catch`/`Hold`/`PickUp`, různé navazující efekty), takže vznikne metoda s přepínači. Nedělat
  jako samostatný úkol s vlastním regresním průchodem — jen když se do toho místa sahá jinak.
- **Debug guard na statické bufry** (náhrada za `ListPool`) — `#if UNITY_EDITOR` příznak
  „bufr se právě používá" + assert. Pár řádků, kdykoli.

### Zrušeno

- **`struct Limb` místo 5 paralelních polí.** Horké smyčky (`TickLimbTimers`,
  `MarkIdleLimbsFree`, `ApplyLimbsContactDamage`, `ApplyReactionToCaughtLimbs`,
  `GetGroundVelocity`) skenují **jen `limbStatus`**, tedy souvislé `float[4]`. Sloučení do ~40 B
  struktury z toho udělá stride přes 40 bajtů kvůli čtení 4 — dnešní SoA je pro ně **lepší**.
  Přínos je čistě vizuální, cena je přepis ~60 přístupů plus perf regrese v kódu běžícím 50×/s
  na každé příšeře. Proti perf budgetu (CPU-bound hlavní vlákno, cíl ~100 probuzených příšer).
- **Plné rozseknutí `TryHoldOne`.** Nejrizikovější místo v souboru (inventář, RB,
  `SetCollisionIgnored`, `MoveZ`) a leží **výhradně na cestě hráče** — v1 příšery předměty
  neberou. Maximální riziko, nulový přínos pro framework.
- **`ListPool<T>` místo statických `armCandidates`/`placeables`.** Obávaná reentrance reálně
  nehrozí: `DisconnectTargetsOwnJoints` → cizí `IConnector.Disconnect` → v nejhorším cizí
  `OnLimbDetached` → inventář, a nic z toho nevolá `map.Get(placeables)`. Víc příšer to nezmění
  — běží sekvenčně, nevnořeně. `ListPool` by navíc přidal práci na hot path. Místo toho stačí
  debug guard (výše).
- **`MonsterController` + `CrawlerStyle`** není refaktor `ChLegsArms`, ale kroky 2 a 3
  [implementačního pořadí](priser-framework.md#implementační-pořadí) — řeší se tam.
