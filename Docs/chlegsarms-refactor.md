# Refaktor ChLegsArms — stav a další fáze

Refaktor motoru `ChLegsArms` rozdělený do tří kýblů. **Kýbl A je hotový**, B a C
čekají. Dokument nese analýzu, aby se dalo pokračovat bez předchozího kontextu.

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
  chůze/běh/šikmá plocha · skok · chytání rukama (pravé tlačítko / `C`), šplhání ·
  `E` pickup, `E`+myš hold, ItemAdjust, drop · `R`+klik hod, ThrowReload ·
  nůž (`HoldAnimator`) · `Ctrl` Z-move i s předmětem · inventář, `Tab` ·
  `SmallMonster` chodí a otáčí se na hraně.
- **Perf měření:** `Game.UpdateTimes[3]` (= `UpdateObjects()`) a `UpdateTimes[0]`
  jsou public pole na `Game` → viditelné v Inspectoru za běhu. Plus frekvence
  logu `"## GC ##"` z `Game.LogGC`.
- `Limbs` je serializované pole navázané v `Character3.prefab` a
  `Small Monster.prefab`; drží ho `[FormerlySerializedAs("Legs")]`. Prefaby je
  vhodné jednou otevřít a přeuložit, aby se YAML přepsal na nový název.
  **Pořadí 0,1 = nohy, 2,3 = ruce je nosné.**

---

## Kýbl B — rozdělení GameUpdate / FixedUpdate (DALŠÍ NA ŘADĚ)

### Nález

Dnešní řez je „rozhoduj a připojuj v Update, aplikuj síly ve FixedUpdate".
`AdjustLegsArms` je ale čistá fyzika (raycasty, `body.linearVelocity`,
`ComputePenetration`, `AttachRigidBody`, `SetCollisionIgnored`), takže běží na jiné
frekvenci než stav, který čte i produkuje.

**Správný řez: Update = vzorkování inputu (latch) + prezentace;
FixedUpdate = vše, co čte nebo píše fyzikální stav.**

### Bug: při nízkém FPS se předmět hodí s velmi malou silou

Ruku pouští až `DetachArmIfNeeded` v příštím `GameUpdate`, ale hod aplikuje
`GameFixedUpdate`. Při nízkém FPS proběhne mezi tím 1–3 dalších fixed kroků,
ve kterých `ApplyHoldForce` táhne hozený objekt zpátky k ruce.

Clamp je `HoldMoveAcceleration(1.5) × speed(→1.3) × koef`. Pro StickyBomb
(mass 5, postava mass 50) to je **~5,4 m/s ubráno na každý fixed krok**, proti
maximálnímu hodu ~8,9 m/s. Jeden krok navíc sebere ~60 % hodu, dva ho zabijí.

Existující `if (bodyToThrow == null) base.GameFixedUpdate();`
(`Character3.GameFixedUpdate`) je záplata na tentýž problém, která navíc obětuje
celý jeden fyzikální krok lokomoce, podpory nohou i dragu.

**Druhý přispěvatel:** `throwVector`/`throwForce` se počítají jen
v `ThrowController.PositionLongThrowMarker` z Update, a `SetThrowActive(false,…)`
shodí `throwActive` **před** `ThrowObj` → míření použité pro hod je vždy o frame staré.

### Obsah kýblu B

- `AdjustLegsArms` a vše fyzikální → do `GameFixedUpdate`, před aplikaci sil.
- **Latch vrstva.** Pravidlo: *level-triggered* stav (`desiredVelocity`,
  `desiredCatch`, `desiredHold`, `desiredCrouch`) hranici snese; *edge-triggered*
  událost (skok, hod, pickup, zMove, `InventoryAccess`, `TryActivateInHand`) ne.
  Latch **nastavuje jen Update (akumuluje `|=`, `++`), nuluje jen FixedUpdate**.
  Láme se to obousměrně: při vysokém FPS se událost ztratí (další Update ji
  přepíše), při nízkém proběhne 3×. Parametry hrany (`throwVector`) zachytit
  v okamžiku hrany, ne dočíst později.
  Dnešní `lastJumpTime + 0.3f` v `Character3` je záplata přesně na tohle a stane
  se z ní explicitní jump buffer.
- **Oprava hodu:** pustit ruku přímo v `ThrowObj`; zrušit hack `if (bodyToThrow == null)`.
- `AnimateHand` / `HoldAnimator.Evaluate` → FixedUpdate (žene `holdTarget`,
  který konzumuje `ApplyHoldForce` na 50 Hz — není to prezentace, je to bodnutí).
- `ThrowController.ShowThrowMarker` → Update (je prezentace a používá
  `Time.deltaTime` uvnitř FixedUpdate).
- `TickLimbTimers`: `Time.deltaTime` → `Time.fixedDeltaTime`.
- `InputController.GameUpdate()` se volá **2× za frame** (`Game.Update`
  a `Character3.UpdatePosition`) — sjednotit.
- Camera follow (`InputController.GameUpdate` → `Camera.SetAbsolutePosition`)
  do LateUpdate.

### Co patří v Update zůstat

Prezentace: `SimpleCameraController.GameUpdate`, `visibility.Compute`,
`CellSimDebug.Render`, `fpsCounter`, `SetActiveMarker` (marker DirtBuilderu),
`TimeOfDay.ChangeLightVariant`, HUD/inventář.
Vzorkování inputu: `Input.Get*`, `KeysToInventory.TestKeys()`,
`mousePosInWord = ScreenToWorldPoint(...)`.

Smíšené, chce rozseknout: `PositionLongThrowMarker` dělá zároveň marker
(prezentace) i přepočet `throwVector` (herní vstup).

### Očekávaná změna feelu

Kadence končetin se přesune z frame-time na 50 Hz → počítat s přeladěním
`LegTimeout` (dnes 6.5 postava / 5 příšera) a možná `HoldMoveAcceleration`.
Je to důsledek, ne regrese.

### Interpolace rigidbodies (až po B)

Všech 8 prefabů má `m_Interpolate: 0` (None) → pohyb je kvantovaný na 20 ms
a při >50 fps znatelně trhá; nejvíc přes kameru, která sleduje neinterpolovanou
postavu, takže cuká celý svět.

`Interpolate` zapnout **až po kýblu B**: na interpolovaném RB vrací
`transform.position` v Update opožděnou hodnotu a ve FixedUpdate skutečnou
fyzikální pózu — dnešní `AdjustLegsArms` v Update by tiše začal číst rozmazané
pozice. `m_AutoSyncTransforms: 0` znamená, že raycasty interpolace neovlivní.
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
