# Refaktor ChLegsArms — stav a další fáze

Refaktor motoru `ChLegsArms` rozdělený do tří kýblů. **Kýble A a B jsou hotové**, C
čeká. Dokument nese analýzu, aby se dalo pokračovat bez předchozího kontextu.

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

`Character3.SampleInput()` je jediné místo, kde se v postavě čte `Input`. Konvence:

| Tvar | Význam | Píše / čte |
|---|---|---|
| `desired*` | co má motor dělat | stavový automat / motor |
| `*Held` | level vstup — klávesa je držená | Update / FixedUpdate |
| `*Pressed`, `*Released` | hrana, akumuluje se `\|=`, nuluje `ClearInputEdges()` | Update / FixedUpdate |

Level signál hranici mezi frekvencemi snese, stačí ho přepisovat. Hrana ne: při vysokém
fps by ji další Update přepsal a ztratila by se, při nízkém by proběhla vícekrát.
Parametry hrany se zachytí v okamžiku hrany (`mousePressedWithPickupKey`, `lastJumpTime`,
`throwVector`), nedočítají se později.

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

**Známé, nezměněné:** po `DeactivateInput` (Tab) si postava drží poslední `desiredVelocity`
a `desiredCatch` a jde dál. Bylo to tak i před kýblem B, nesahalo se na to.

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

- `float[] limbStatus` → `LimbState` enum + samostatný `float freeTimer`.
  Přepíše `TrySelectFreeLimb` a `MarkIdleLimbsFree`, které dnes stojí na
  neomezeném záporném driftu floatu.
- Sjednotit 5 paralelních polí (`Limbs`, `limbStatus`, `limbTargets`,
  `limbConnectors`, `Settings.limbZ`) do `struct Limb` + `Limb[]`.
  **Perf pozor:** vždy `ref var limb = ref limbs[i]`, nikdy `foreach` ani
  předávání hodnotou (struct by měl ~40 B).
- Sloučit `TryAttachLimbTo` + `PlaceLimbAtOwnZ`/`PlaceLimbAtHitZ` — volají se
  vždy v páru (attach uspěje → hned následuje place).
- Generalizace počtu nohou/rukou — dnes natvrdo 0,1 / 2,3 a `PairedLimb = i ^ 1`.
- `MovementMode { Legged, Free }` + `body.useGravity`: stačí změnit podmínku
  `if (ArmCatched)` v `ApplyMoveForce` na `if (ArmCatched || mode == Free)`.
  Ta větev už je plný 2D regulátor rychlosti, tedy pohon letadla.
- Plné rozseknutí `TryHoldOne` (podmínkový strom — nejrizikovější místo v souboru,
  sahá na inventář, RB, `SetCollisionIgnored` i `MoveZ`).
- `MonsterController : ChLegsArms` mezivrstva + `CrawlerStyle` (port `SmallMonster`).
- Zvážit `ListPool<T>` místo statických `armCandidates`/`placeables`
  ([conventions.md](conventions.md) 2a) — dnes je riziko reentrance, protože
  `TryCatchNearbyObject` iteruje `placeables` a uvnitř volá cizí kód
  (`DisconnectTargetsOwnJoints` → `IConnector.Disconnect`).
