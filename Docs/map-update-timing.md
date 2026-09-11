# Aktualizace mapy vůči Unity update fázím

**Stav: implementováno.**

Časování Unity smyčky, na které to celé stojí, je v
[unity-frame-order.md](unity-frame-order.md).

---

## Cíl

Mapa má obsahovat situaci podle **poslední simulace** a být konzistentní pro
každého čtenáře — ať už čte z `Update` (herní logika, časovače) nebo z
`FixedUpdate` (rozhodování `AdjustLegsArms` o končetinách).

Motivace pro fixed rate: na rozhodování ve `FixedUpdate` mají vliv výrazné pohyby
"neaktivních" objektů, jejichž pozice se čte z mapy. Dřív se ty pozice obnovovaly
jen na frame rate.

## Původní stav

| Kdo | Kde se obnovoval | Politika |
|---|---|---|
| `movingObjects` | `Game.Update` → `UpdateMovingObjects()` | práh 0,1 m každý frame + plný `map.Move` po round robinu (perioda 20 framů) |
| aktivní objekty (`Character3`, `SmallMonster`) | `AdjustLegsArms()` uvnitř smyčky `activeObjects` | bezpodmínečný `map.Move`, každý fixed krok |

`Placeable.PlaceToMap` dělil buď/anebo: `IActiveObject` komponenta → `ActivateObject`,
jinak s aktivním RB → `AddMovingObject`.

## Nálezy

**1. `map.Move` na začátku `FixedUpdate` není "před simulací".** Mezi koncem
předchozí `Simulate()` a tímhle bodem transformem nikdo nehne, takže čte přesně
pózu po poslední simulaci.

**2. Závislost na vypnuté interpolaci.** Čtení transformu z `Update` vrací při
zapnuté interpolaci vyhlazenou pózu. Řešeno tím, že refresh běží ve
`WaitForFixedUpdate` — tam transform drží simulovanou pózu (viz Kontrakt).

**3. Mapa se měnila uvnitř čtenářské smyčky.** Objekt #5 v `AdjustLegsArms`
dotazoval mapu, kde #1–#4 už sebe posunuli a #6–#N ne. Řešeno — refresh je jeden
atomický průchod mimo smyčku `activeObjects`.

**4. Díra mezi poslední simulací framu a prvním `Update`.** Řešeno — refresh běží po
každé simulaci, tedy i po poslední.

**5. Rotace a změna `Size` obcházejí práh 0,1 m.** `UpdateMapPosIfMoved` porovnává
jen pozici. Orientaci a velikost přebírá až `RefreshCoordinates` → `RefreshBounds`,
kterou volá plný `map.Move` (round robin nebo `AlwaysMapMove`). Kdo mění `Size`
nebo rotuje mimo fyziku, **musí zavolat `map.Move` v místě změny**.

**6. `UpdateTriggers()` viděl mapu o frame starou.** Řešeno — mapa je čerstvá už
na začátku `Update`.

**7. `ApplyZMove` nevolá `map.Move`.** Teleport v Z mění `CellBlocking` bity
`Cell0`/`Cell1`, které práh (jen XY) nezachytí. Postavy mají `AlwaysMapMove`, takže
se to propíše v refreshi po nejbližší simulaci.

**8. Kontrakt `IActiveObject` placeable nebyl vynucený.** Řešeno rozpojením —
členství v `movingObjects` určuje jen aktivní RB, nezávisle na `IActiveObject`.

**9. `IsInMovingObjects` strhává blokovací bity** (`AllFullEx` v `RefreshCoordinates`).
To je pro pohyblivé objekty správně a zůstává. Postavy mají `SubCellFlags: Part`,
takže je pro ně strhávání no-op.

## Řešení

### `RefreshMapPositions()` v `WaitForFixedUpdate` coroutině

`Game.MapRefreshLoop` (spouštěná v `OnEnable`) volá `RefreshMapPositions()` po
každé `Physics.Simulate`. Přesně jeden refresh na jednu simulaci vychází z
konstrukce, žádný gate není potřeba; frame bez fixed kroku refresh nespustí.

```
FixedUpdate blok (0..N×):
  Game.FixedUpdate      ← activeObjects, AdjustLegsArms: mapa po S_{n-1}
  Physics.Simulate S_n
  OnCollisionXxx        ← mapa pořád po S_{n-1}
  WaitForFixedUpdate → RefreshMapPositions()   ← mapa po S_n

Game.Update             ← mapa po poslední simulaci
  InputController.GameUpdate()
  UpdateTriggers()
  UpdateObjects()
  Timer.GameUpdate()
  visibility            ← vlastní čítač (updateTicker)
```

Jeden průchod `movingObjects`:

```
for each entry:
   slowPass && tag == workPtr → map.Move + MovingObjTest     (round robin)
   AlwaysMapMove               → map.Move
   jinak                       → UpdateMapPosIfMoved (práh 0,1 m)
```

`slowPass` = první refresh v daném framu (`Time.frameCount`). Round robin tak má
periodu 20 framů se simulací; framy bez simulace ho přeskočí (fyzikálně se nic
nepohnulo).

Tělo smyčky je v try/catch: nezachycená výjimka by coroutinu natrvalo ukončila a
mapa by se přestala obnovovat.

### Členství

- **`movingObjects`** — kdo má aktivní RB. Přidává `PlaceToMap` (objekt s RB při
  umístění, včetně postav s vlastním `Rigidbody`) a `RbLabel.Init`/`StopMoving`
  při připojení/odpojení RB.
- **`activeObjects`** — nezávislé; registruje objekt ručně
  (`Game.Instance.ActivateObject`/`DeactivateObject`), typicky v
  `AfterMapPlaced`/`Cleanup`. Obrana proti dvojí registraci ani deregistraci
  neregistrovaného objektu není — odpovědnost volajícího.
- **`Placeable.AlwaysMapMove`** — serializovaný bool (nastaven na prefabech
  `Character3` a `Small Monster`), měnitelný za běhu, `Cleanup` ho vrací z
  prototypu.

## Vědomé kompromisy

**Round robin je na frame rate.** Perioda 20 framů, takže rychlost
sand-compaction testu (`MovingObjTest`) a refreshe OBB se veze na FPS (shora
omezeno frekvencí simulace). Přesun na čistě fixed kroky by přidal zátěž při nízkém
FPS — tedy zrovna když se dusíme.

**Práh 0,1 m zůstává.** To je 20 % buňky; na hranici buňky může dotaz zařadit
objekt vedle. Vědomá tolerance.

**Kolizní callbacky čtou mapu o simulaci pozadu.** Fireují po `Simulate(S_n)`, ale
před refreshem. Dotčený čtenář (`DeepGlue`) si musí objekty přesunout sám, pokud
mu to vadí.

## Kontrakt pro čtenáře mapy

- Mapa drží stav po poslední simulaci v `Update` i ve `FixedUpdate`.
- Tolerance **0,1 m** na pozici (kromě `AlwaysMapMove`).
- Tolerance až **20 framů** na rotaci a `Size` — kdo je mění mimo fyziku, volá
  `map.Move` sám v místě změny.
- Interpolace rigidbodies: refresh čte simulovanou pózu i se zapnutou interpolací.
  `map.Move` volané z `Update` na interpolovaném RB by ale zapsalo vyhlazenou pózu
  (opraví ji další refresh). Dnes mají všechny RB `m_Interpolate: 0`.
