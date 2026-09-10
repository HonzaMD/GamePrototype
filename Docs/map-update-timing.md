# Aktualizace mapy vůči Unity update fázím

**Stav: navrženo, neimplementováno.** Implementace čeká na dočištění `ChLegsArms`
(viz [chlegsarms-refactor.md](chlegsarms-refactor.md)).

Časování Unity smyčky, na které to celé stojí, je v
[unity-frame-order.md](unity-frame-order.md).

---

## Cíl

Mapa má obsahovat situaci podle **poslední simulace** a být konzistentní pro
každého čtenáře — ať už čte z `Update` (herní logika, časovače) nebo z
`FixedUpdate` (rozhodování `AdjustLegsArms` o končetinách).

Motivace pro fixed rate: na rozhodování ve `FixedUpdate` mají vliv výrazné pohyby
"neaktivních" objektů, jejichž pozice se čte z mapy. Dnes se ty pozice obnovují
jen na frame rate.

## Stav dnes

| Kdo | Kde se obnovuje | Politika |
|---|---|---|
| `movingObjects` | `Game.Update` → `UpdateMovingObjects()` | práh 0,1 m každý frame + plný `map.Move` po round robinu (perioda 20 framů) |
| aktivní objekty (`Character3`, `SmallMonster`) | `AdjustLegsArms()` uvnitř smyčky `activeObjects` | bezpodmínečný `map.Move`, každý fixed krok |

Rozdělení dělá `Placeable.PlaceToMap` — je to buď/anebo:

```csharp
if (TryGetComponent<IActiveObject>(out var ao))  Game.Instance.ActivateObject(ao);
else if (HasActiveRB)                            Game.Instance.AddMovingObject(this, map);
```

## Nálezy

**1. `map.Move` na začátku `FixedUpdate` není "před simulací".** Mezi koncem
předchozí `Simulate()` a tímhle bodem transformem nikdo nehne, takže čte přesně
pózu po poslední simulaci. Datově je to totéž jako "po simulaci". Není co
opravovat.

**2. Skrytá závislost na vypnuté interpolaci.** Všech 8 rigidbodies má
`m_Interpolate: 0`. Kdyby ji kdokoli zapnul, `transform.position` čtené z
`Update` by vracelo vyhlazenou, ne simulovanou pozici, a mapa by tiše dostávala
pozice posunuté až o jeden fixed krok (0,2 m při 10 m/s, skoro půl buňky).
**Interpolace musí zůstat vypnutá**, jinak návrh neplatí.

**3. Mapa se mění uvnitř čtenářské smyčky.** Objekt #5 v `AdjustLegsArms`
dotazuje mapu, kde #1–#4 už sebe posunuli a #6–#N ne. Půlrozsypaný stav závislý
na pořadí v `activeObjects`.

**4. Díra mezi poslední simulací framu a prvním `Update`.** Kdyby se refresh dělal
jen na začátku `Game.FixedUpdate`, po poslední `Simulate()` framu už nikdo
neobnoví a čtenáři v `Update` vidí stav o krok starší.

**5. Rotace a změna `Size` obcházejí práh 0,1 m.** `UpdateMapPosIfMoved` porovnává
jen pozici. Orientaci a velikost přebírá až `RefreshCoordinates` → `RefreshBounds`,
kterou volá pouze plný `map.Move` z round robinu. Rotující prkno se stojícím
pivotem nebo rostoucí `TreeTrunk` drží v mapě starý footprint až 20 framů (0,4 s
při 50 FPS). Kdo mění `Size` nebo rotuje mimo fyziku, **musí zavolat `map.Move`
v místě změny**.

**6. `UpdateTriggers()` je na špatné straně refreshe** — běží před
`UpdateMovingObjects()`, takže jako jediný čtenář v `Update` vidí mapu o celý
frame starou.

**7. `ApplyZMove` nevolá `map.Move`.** Teleportuje `transform.position.z`, což mění
`CellBlocking` bity `Cell0`/`Cell1`. Mapa drží staré blokování do dalšího fixed
kroku. Návrh to zachytí předřazeným průchodem.

**8. Kontrakt `IActiveObject` placeable není vynucený.** `PlaceToMap` je z
`movingObjects` mlčky vyřadí a spoléhá, že si `map.Move` zavolají samy. Dnes to
platí (jen `Character3` a `SmallMonster`), ale třetí pohyblivý `IActiveObject`
placeable by v mapě navždy zůstal stát na místě a nic to nenahlásí.

**9. `IsInMovingObjects` není jen značka členství.** V `RefreshCoordinates`
strhává blokovací bity:

```csharp
if (IsInMovingObjects)
    CellBlocking = CellBlocking & ~CellFlags.AllFullEx;
```

Postavy dnes `IsInMovingObjects == false` mají, takže si blokování drží. Přidat je
do `movingObjects` dnešní cestou by jim to tiše sebralo. **Sjednocení musí ty dva
významy rozpojit.**

## Návrh

Jedna funkce `RefreshMapPositions()`, která atomicky projde celý seznam (žádný
čtenář uprostřed), a **gate: proběhla simulace od posledního běhu?** Volá se ze
dvou míst.

```
Game.FixedUpdate → gate ✔ refresh (stav po S0)  → Simulate S1
Game.FixedUpdate → gate ✔ refresh (stav po S1)  → Simulate S2
Game.Update      → gate ✔ refresh (stav po S2)  → čtenáři vidí S2

další frame bez fixed kroku:
Game.Update      → gate ✗ skip
```

Invariant: **přesně jeden refresh na jednu simulaci, nezávisle na FPS.** Při 200
FPS je to 4× méně práce než dnes, při 25 FPS 2× více — ale to je právě ta práce,
která je pro korektnost potřeba.

`WaitForFixedUpdate` coroutina by udělala totéž a nic nezískala — běží až za
kolizními callbacky, takže `DeepGlue.OnCollisionEnter` uvidí mapu o krok pozadu
tak jako tak. To je samostatný, už existující problém.

### Výsledný tvar

```
RefreshMapPositions()            ← atomicky nad celým seznamem
  gate: od posledního běhu proběhla simulace?
  for each entry:
     alwaysMove ? map.Move(p) : p.UpdateMapPosIfMoved(map)

Game.FixedUpdate:
  RefreshMapPositions()          ← == „po simulaci předchozího kroku"
  InputController.GameFixedUpdate()
  activeObjects loop             ← konzistentní mapa pro AdjustLegsArms
  ...                            → Simulate

Game.Update:
  RefreshMapPositions()          ← zavře díru po poslední simulaci framu
  UpdateTriggers()
  UpdateMovingObjectsSlowPass()  ← round robin: plný Move + MovingObjTest
  UpdateObjects()
  Timer.GameUpdate()
  visibility                     ← vlastní frame čítač
```

### Zásahy k provedení

1. **Rozpojit `IsInMovingObjects` na dva příznaky** — sémantika blokování
   (strhávání `AllFullEx`) vs. členství v refresh seznamu. Nález 9.
2. **Zaregistrovat aktivní objekty do refresh seznamu s příznakem `alwaysMove`.**
   Odstranit buď/anebo z `PlaceToMap`. Tím padá nález 8 i potřeba `map.Move`
   v `AdjustLegsArms` (lze ho ale nechat jako defenzivní no-op — `Map.Move` má
   rychlou cestu, když se pozice ani blocking nezměnily).
3. **Rozseknout `UpdateMovingObjects` na rychlý a pomalý průchod.** Rychlý
   (`RefreshMapPositions`, práh + `alwaysMove`) s gatem, dvě volací místa. Pomalý
   (round robin: plný `map.Move` + `MovingObjTest`) zůstává v `Update`.
4. **Přesunout `UpdateTriggers()` za refresh.** Nález 6.
5. **Odpojit čítač viditelnosti.** `visibility.Compute` dnes visí gatingem na
   `movingObjectWorkPtr`; po rozdělení potřebuje vlastní frame čítač, jinak by se
   počítala per simulaci místo per frame.

## Vědomé kompromisy

**Round robin zůstává na frame rate.** Perioda 20 framů, ne 20 simulací, takže
rychlost sand-compaction testu (`MovingObjTest`) a refreshe OBB se veze na FPS.
Přesun do fixed kroků by to spravil zadarmo (stejně už se iteruje), ale přidal by
zátěž při nízkém FPS — tedy zrovna když se dusíme. Při CPU-bound main threadu
necháváme v `Update`. Pokud by rychlost sand testu vadila, gatovat zvlášť na něco
frame-rate nezávislého.

**Práh 0,1 m zůstává.** To je 20 % buňky; na hranici buňky může dotaz zařadit
objekt vedle. Vědomá tolerance.

**Kolizní callbacky čtou mapu o simulaci pozadu.** Fireují po `Simulate(S_n)`, ale
mapa drží stav po `S_{n-1}`. Přeuspořádáním se to neřeší; dotčený čtenář
(`DeepGlue`) si musí objekty přesunout sám, pokud mu to vadí.

## Kontrakt pro čtenáře mapy (doplnit do conventions.md)

- Mapa je platná v `Update` po `RefreshMapPositions()` a ve `FixedUpdate` po témž.
- Tolerance **0,1 m** na pozici.
- Tolerance až **20 framů** na rotaci a `Size` — kdo je mění mimo fyziku, volá
  `map.Move` sám v místě změny.
- **Interpolace rigidbodies musí zůstat vypnutá** (`m_Interpolate: 0`).
