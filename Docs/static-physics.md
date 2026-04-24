# StaticPhysics (`Assets/Scripts/Core/StaticPhysics/`)

Statický řešič sil a kloubů na grafu. Běží na samostatném vlákně a game thread s ním komunikuje výhradně přes frontu příkazů v `SpInterface`.

## K čemu to je

Klasický Unity Rigidbody solver je zbytečně drahý pro stovky objektů, které jsou "v klidu" připojené k pevným bodům (zdi, podlaha, velká tělesa). Místo toho:

1. Každý statický `Placeable` je uzel v grafu (`SpNode`). **Uzly se ve StaticPhysics nehýbou** — pozice a geometrie jointů se zachytí při vzniku a dál jsou neměnné.
2. Spoje mezi objekty jsou hrany s klouby (`SpJoint`).
3. Uzel označený `Ksid.SpFixed` (zeď, země) je *kořen* — odtud graf drží zbytek.
4. Sleduje se jen **napětí**: gravitace každého uzlu se šíří grafem směrem ke kořeni a akumuluje se jako síly v kloubech (`compress`, `moment`).
5. Když klouby překročí limity (`stretchLimit`, `compressLimit`, `momentLimit`), prasknou. Uzel, který ztratí spojení s kořenem, teprve tehdy **opouští StaticPhysics** a předá se do klasické RB simulace, která se už o pohyb postará.

Celé to umožňuje držet velké struktury prakticky zadarmo — dokud se něco neohne přes limit, žádný pohyb se nepočítá.

## Top-level architektura

```
game thread                  │  StaticPhysics thread
─────────────────────────────┼──────────────────────────────
RbJoint, Placeable, DeepGlue │
   │ AddInCommand            │
   │ AddForceCommand         │
   ▼                         │
publicInCommands ─── lock ──▶ waitingInCommands
publicForceCommands─ lock ──▶ waitingForceCommands
                              │   ▼
                              │  privateInCommands (swap)
                              │  privateForceCommands
                              │   │
                              │   ▼
                              │  GraphWorker.ApplyChanges
                              │   ├─ DeleteColorWorker
                              │   ├─ AddColorWorker
                              │   ├─ ForceWorker (Remove/Add/Temp)
                              │   └─ FindFallenWorker
                              │   │
                              │   ▼
                              │  privateOutCommands
                              │   │ (swap pod lockem)
publicOutCommands ◀─── lock ──  waitingOutCommands
   │
   ▼
ProcessOutCommands  → Placeable.SpFall / SpConnectEdgeAsRb / SpBreakEdge
```

- `SpInterface` — fasáda a thread handshake.
- `SpDataManager` — vlastní uzly, klouby a pool polí `EdgeEnd[]`.
- `GraphWorker` — orchestruje jedno kolo zpracování ("tick").
- `AddColorWorker`, `DeleteColorWorker` — udržují značení cest ke kořenům ("barvy").
- `ForceWorker` — propaguje síly a detekuje praskající klouby.
- `FindFallenWorker` — najde uzly odtržené od kořene a pošle je do RB.

## Datový model

### `SpNode` (struct, drží se v `nodes[]` ve `SpDataManager`)
- `position` — pozice v herním prostoru zachycená při vzniku uzlu. Ve StaticPhysics se **nikdy nemění** — sleduje se jen napětí v grafu, ne posuny.
- `edges` — aktuální pole hran (`EdgeEnd[]`). Půjčuje se z poolu.
- `newEdges` — *připravovaná* nová sada hran (během ApplyChanges). Po dokončení swap: `edges = newEdges`.
- `force` — aktuální vnější síla (typicky gravitace = `down * mass`).
- `isFixedRoot` — pokud != 0, uzel je kořen a toto je jeho "barva" (shodná s jeho indexem).
- `placeable` — odkaz zpátky na herní objekt (potřeba pro FallNode/SpBreakEdge callback).
- `newEdgeCount` — čítač při budování `newEdges`.

### `EdgeEnd` — jeden konec hrany uzlu

Hrana mezi A a B je zastoupena dvakrát: jeden `EdgeEnd` v `A.edges`, druhý v `B.edges`. Oba sdílejí stejný `Joint` index.

Směr šíření síly (list → kořen):

```
[LeafNode] -Out->--Joint-->-In- [Node] -Out->--Joint-->-In- [RootNode]
```

`Out*` je konec hrany na straně blíž listu, `In*` je zrcadlový konec blíž kořeni. Barva = index `FixedRoot` uzlu, ke kterému cesta vede.

Pole:
- `Other` — index druhého uzlu.
- `Joint` — index do `joints[]`.
- `Out0Root`, `Out1Root` — barva cesty ven touto hranou ke kořeni.
- `Out0Length`, `Out1Length` — celková délka té cesty ke kořeni.
- `In0Root`, `In1Root` — barva, která touto hranou přichází z listové strany.
- Slot `0` je vždy lepší (kratší) cesta; slot `1` je alternativa.

### `SpJoint` (struct, drží se v `joints[]`)
- `length` — vzdálenost mezi uzly spočítaná při vzniku jointu. Neaktualizuje se — uzly se ve StaticPhysics nehýbou.
- `abDir` — jednotkový směr od uzlu s nižším indexem k vyššímu; nastaví se při vzniku a dál se nemění.
- `stretchLimit`, `compressLimit`, `momentLimit` — prahy, kdy spoj praskne.
- `compress`, `moment` — akumulovaná síla z persistentních zdrojů (gravitace).
- `tempCompress`, `tempMoment` — síly z jednokolových impulzů (viz `AddTempForces`).

### `Placeable.spNodeIndex`
Rezervuje se přes `SpInterface.ReserveNodeIndex()` při vzniku uzlu a uvolní se, až dorazí výstupní `FreeNode` / `FallNode`. 0 znamená "v grafu není".

## Životní cyklus příkazu

### Vstupy (`SpCommand`)
- `AddNode`, `AddJoint`, `AddNodeAndJoint` — přidání uzlu/hrany. Typicky z `RbJoint.AddNode1`/`AddNodeAndEdge`.
- `RemoveNode` — `Placeable.Cleanup` (objekt je zničen/odpoolován).
- `RemoveJoint` — `RbJoint.Disconnect` nebo rozhodnutí řešiče, že hrana praskla.
- `UpdateForce` — změna gravitace/hmoty uzlu.
- `UpdateJointLimits` — dynamická změna limitů (např. `DeepGlue`).

### `ForceCommand`
Samostatná fronta jednorázových impulzů, které se aplikují jen jeden tick (`AddTempForces`). Po vyhodnocení prasklých hran se `tempCompress`/`tempMoment` vynulují.

### Výstupy
- `FreeNode` — uvolni index uzlu (po RemoveNode).
- `FallNode` — uzel ztratil root → `Placeable.SpFall` zapne RB.
- `FallEdge` — dva padající uzly byly spojené → vytvoří se RB `FixedJoint` kopírující limity.
- `RemoveJoint` — hrana praskla → `SpBreakEdge` zruší RB joint (pokud existoval) a spustí particle efekt.

## Thread handshake (`SpInterface`)

Tři sady bufferů pro vstup (`public`, `waiting`, `private`) se přehazují (swap) pod zámkem `sync`:

- **Game thread** plní `publicInCommands`/`publicForceCommands`.
- **Update()** pod `sync`:
  - vrátí do hry `publicOutCommands` (swap s `waitingOutCommands`),
  - pokud je něco nového a `runnerIdle`, předá input přes swap a `semaphore.Release()`.
- **Runner thread** čeká na semaforu. Po probuzení cyklicky:
  - pod `sync` swapne `waitingIn`↔`privateIn`, `waitingForce`↔`privateForce`, a pokud je `waitingOut` prázdný, swapne i ten s `privateOut`.
  - zpracuje dávku přes `GraphWorker.ApplyChanges`.
  - *uvnitř* téhož probuzení může provést několik iterací kvůli prasklým hranám (viz níže).
  - Když už nejsou vstupy → `runnerIdle = true` a zpět na `semaphore.Wait()`.

`ProcessOutCommands` na game threadu běží ve třech fázích, aby se zachovaly invarianty mezi `spNodeIndex` a poolem indexů:
1. **Callbacky na `Placeable`** — pro `FallNode` zavolá `SpFall`, pro `FallEdge` `SpConnectEdgeAsRb`, pro `RemoveJoint` `SpBreakEdge`. V této fázi ještě obě strany hrany mají platný `spNodeIndex`, takže `SpConnectEdgeAsRb` umí dohledat oba konce.
2. **`SpRemoveIndex`** — pro `FreeNode`/`FallNode` vynuluje `Placeable.spNodeIndex` (objekt už ve StaticPhysics není).
3. **`FreeNodeIndex`** — vrátí index zpět do poolu, aby ho mohl použít nový uzel.

## `GraphWorker.ApplyChanges` — jedno kolo

Pořadí je *dané* a jednotlivé fáze se drží invariantů, na které další fáze spoléhají.

1. **`EnsureValidNodes`** — každý příkaz ověří, že uzly stále existují; jinak se degraduje na `None` (uzel mezitím spadl).
2. **`AddNode` fáze** — alokuje `node.edges = empty`, zapíše placeable/fixedRoot, přidá uzel do `toUpdate`.
3. **`AddJointPrepare`** — zvýší `newEdgeCount` u obou konců a přidá je do `toUpdate`. Dictionary `newEdges` drží páry (uzelA, uzelB) → index příkazu, aby bylo možné rozpoznat duplicitní AddJoint v jednom ticku.
4. **`RemoveJointPrepare`** — najde joint v `edges` a přidá ho do `deletedEdges`. Pokud odebíráme hranu, která se ve stejném ticku přidávala, zruší se její `AddJoint` přes `ClearAddJoint()` (AddNodeAndJoint degraduje na AddNode).
5. **`RemoveNode`** — přidá uzel do `deletedNodes`, všechny jeho hrany do `deletedEdges`, sousedy do `toUpdate`.
6. **`CreateNewEdgeArrs`** pro každý uzel v `toUpdate` — nové pole `newEdges` = stávající hrany bez smazaných (`deletedEdges`) + místo navíc pro přidávané.
7. **`AddJoint`** — fyzicky zapíše `EdgeEnd` na obě strany a založí `SpJoint` s `length` a `abDir`.
8. **`DeleteColorWorker.Run`** — invaliduje zastaralé barevné cesty: kde se změnil `ShortestColorDistance`, smaže se příchozí barva a rekurzivně i u sousedů.
9. **`AddColorWorker.Run`** — Dijkstra-like: každý uzel v `toUpdate` zkusí prodloužit své barvy (cesty ke kořenům) k sousedům. Priority queue podle délky cesty. Udržuje se invariant, že slot `Out0` je lepší než `Out1`.
10. **Vyhodnocení `UpdateForce` commandů** — přidá `ic.indexA` do `toUpdate` (změna síly si vynutí přepočet). **`UpdateJointLimits`** přepíše limity a vynuluje `tempCompress`/`tempMoment` přes `MarkJointActive`.
11. **`ForceWorker.RemoveForces`** — pro každý uzel v `toUpdate` s `force != 0` projde graf směrem ke kořeni a **odečte** starou sílu z `compress`/`moment` všech kloubů na cestě. Iteruje podle starých `edges` (stará cesta).
12. **UpdateForce** — aplikuje přírůstky `ic.forceA` na `node.force`.
13. **`ApplyEdgeArrs`** — swap `edges = newEdges`. Od teď platí nový graf.
14. **`ForceWorker.AddForces`** — **přičte** nové síly podle nové topologie.
15. **`ForceWorker.AddTempForces`** — jednokolové impulzy (`tempCompress`/`tempMoment`).
16. **`FindFallenWorker.Run`** — pro každý uzel v `toUpdate`, který není `IsConnectedToRoot`, najde rekurzivně spadlou komponentu. Výstupem jsou `FallNode`/`FallEdge`. Uzly se rovnou odstraní z dat.
17. **`FreeJoints`** + **`FreeNodes`** — vrátí indexy jointů a uzlů do poolu, vygeneruje výstupní `FreeNode` commandy.

Po návratu z `ApplyChanges` spustí Runner detekci prasklých kloubů (`GetBrokenEdgesBigOnly` / `GetBrokenEdges`). Pokud něco praskne, RemoveJoint se vrátí zpět do `privateInCommands` a `ApplyChanges` se zavolá znovu. V rámci jednoho probuzení proběhnou **až 2 iterace s `BigOnly`** (jen jedna dominantní prasklina na každou barvu, aby nepraskal celý most najednou); pokud ani pak není hotovo, nastaví se flag `moreBrokenEdges` a při dalším průchodu téže vnitřní smyčky se přes `GetBrokenEdges` (plná sada) dorazí zbývající praskliny. Teprve když vnitřní smyčka narazí na prázdný vstup, vrací se runner na `semaphore.Wait()`.

## Klíčové algoritmy

Pipeline z předchozí sekce ukazuje *pořadí* fází. Následující podsekce rozepisují, **co se v nich děje** a na jakých invariantech kooperují.

### Změna grafu — přidávání a odebírání

Klíčová myšlenka: **starý i nový graf existují současně**. `node.edges` drží topologii z minulého ticku, `node.newEdges` se staví odznova pro tento tick. Barvení a `RemoveForces` potřebují starou, `AddForces` novou.

1. **Validace** (`EnsureValidNodes`) — každý command ověří, že `indexA`/`indexB` stále existují; jinak `Command = None` a zbytek pipeline ho přeskakuje.
2. **Registrace změn** (pouhá evidence, do `edges[]` se ještě nic nezapisuje):
   - `AddNode` → alokuje prázdné `node.edges`, zapíše `placeable`/`isFixedRoot`/`position`. Sám o sobě uzel ještě není plně "vidět" — slouží jako rezervace indexu.
   - `AddJointPrepare` → inkrementuje `newEdgeCount` obou konců a uloží `(indexA,indexB) → icIndex` do dictionary `newEdges`. Ta slouží dvěma účelům: detekci duplicit v rámci ticku a dohledání jointu, který se v témže ticku přidává i odebírá.
   - `RemoveJointPrepare` → joint do `deletedEdges`, dekrementuje `newEdgeCount`. Pokud byla hrana přidána **v tomtéž** ticku (je v dictionary `newEdges`), odvolá se i její `AddJoint` přes `ClearAddJoint()` — párové add+remove se navzájem vyruší.
   - `RemoveNode` → samotný uzel do `deletedNodes`, všechny jeho hrany do `deletedEdges`, **všichni sousedé** do `toUpdate` (ztratili hranu). Navíc zruší (`ClearAddJoint`) i pending jointy z téhož ticku vedoucí do tohoto uzlu — jinak by protistraně zůstal navýšený `newEdgeCount`, který by nikdo nevyplnil.
3. **Stavba `newEdges[]`** (`CreateNewEdgeArrs`) — pro každý uzel v `toUpdate`:
   - alokuje nové pole délky `edges.Length + newEdgeCount`,
   - zkopíruje staré hrany kromě těch v `deletedEdges`,
   - `newEdgeCount` = počet zkopírovaných (zbývající místa se zaplní v dalším kroku).
4. **Zápis nových hran** (`AddJoint`) — teprve nyní se fyzicky vytvoří `EdgeEnd` na obou koncích a `SpJoint` s `length`/`abDir` spočítanými z `position` obou uzlů.
5. **Přepínač** (`ApplyEdgeArrs`, fáze 13) — `edges = newEdges; newEdges = null`. Tohle je středobod ticku — před ním platí "stará topologie", po něm "nová".

Čistící fáze (`FreeJoints`, `FreeNodes`) až **po** FindFallenWorker vrátí indexy do poolu a vygenerují `FreeNode` commandy.

### Hledání nejkratších cest — color workers

**Barva** = index kořenového uzlu (fixedRoot). Když má uzel `isFixedRoot != 0`, vyzařuje barvu rovnou svému indexu. Tato barva se šíří po hranách jako nálepka *"odtud vede cesta ke kořeni X"*.

**Invariant slotu 0/1**: každá hrana nese nejvýš dvě odchozí barvy — typicky když je uzel most napnutý mezi dvě opěry. `Out0Root`/`Out0Length` je *lepší* cesta (`Utils.IsDistanceBetter`), `Out1` druhá nejlepší. Protější konec má zrcadlové `In0Root`/`In1Root` (příchozí barva jedné strany = odchozí barva druhé).

Pipeline má **dva workery** v pevném pořadí:

**1. `DeleteColorWorker`** — invaliduje zastaralé barvy po topologické změně.
- `DetectChanges`: pro každý uzel v `toUpdate` projde `newEdges`. Pro každou příchozí barvu porovná `ShortestColorDistance(color)` (staré `edges`) s `ShortestColorDistanceNew(color)` (nové `newEdges`). Pokud se liší, stará cesta už neexistuje nebo se změnila → zařadí do fronty.
- Hlavní smyčka (`DeleteColor`): projde `newEdges` uzlu a kde se barva v `In*Root` shoduje s invalidovanou, smaže jak `In*Root`, tak zrcadlový `Out*Root` na protějším konci (přes `DeleteOtherEdge`). Souseda přidá do `toUpdate` a do fronty.
- Povýšení slotu: pokud byl smazán `Out0Root`, povýší se do slotu 0 `Out1`.
- **Výsledek**: `newEdges` má konzistentně odstraněné barvy z cest, které už neexistují.

**2. `AddColorWorker`** — šíří nové a zlepšené cesty (Dijkstra):

**Klíčové veličiny a pravidlo šíření:**
- `ShortestColorDistance(color)` (SCD) = minimum přes `Out*Length` hran uzlu pro danou barvu = nejkratší známá vzdálenost uzlu ke kořeni barvy. Pro samotný `isFixedRoot` je SCD = 0.
- **Barvu lze šířit z uzlu `A` do souseda `B` právě tehdy, když `B.SCD(color) > A.SCD(color)`.** Stačí, aby byl `B` "dál od kořene" než `A` — délka `joint.length` v této podmínce nehraje roli. Opačný směr (`B.SCD ≤ A.SCD`) je zakázán, vytvořil by zpětnou hranu ve smyčce cesty.
- Rozšíření tedy není podmíněno tím, že výsledná cesta přes `A` bude kratší než to, co `B` zatím zná — klidně může být delší a obsadit slot `Out1` jako záložní trasu (typicky most napnutý mezi dvěma kořeny, kde každý konec poskytuje jinou cestu do téhož rootu).
- Práce se řadí v `BinaryHeap<Work>` vzestupně podle `Length` (SCD uzlu). Tím má Dijkstra klasickou záruku: uzel se z heapu vybírá až když jeho SCD pro danou barvu je finální — žádný nezpracovaný uzel už ji nezlepší. Pro každou barvu se každý uzel zpracuje max jednou.

**Táž barva se šíří z více uzlů současně.** `DetectWork` sype do heapu kandidáty ze všech uzlů v `toUpdate`:
- vlastní `isFixedRoot` (pokud je uzel kořen — zdrojem barvy X je uzel X s SCD=0),
- každou barvu z `Out*Root` libovolné hrany (uzel už zná cestu ke kořeni, může ji nabídnout dalším sousedům).

Heap tedy drží pro jednu barvu často několik uzlů najednou. Dijkstra-ordering zajistí, že se vyřídí v pořadí rostoucí SCD — a díky tomu `ExpandColor` u každého souseda správně rozhodne, jestli je naše cesta zlepšením. `IsColorValid` v `DetectWork` jen zabrání duplicitě v rámci jednoho uzlu: pokud stejná barva figuruje ve více `Out*` slotech různých hran téhož uzlu, vezme se jako startovní kandidát jen jednou (ten slot s nejmenší `Out*Length`, protože SCD je minimum).

**`ExpandColor`**: odebere nejkratší `Work` z heapu, projde všechny hrany uzlu a pro každou spočítá `lengthB = joint.length + startDist`. Pro protější uzel:
- pokud už cílový slot tu barvu má, ale s delší `Out*Length` → přepíše (zlepšení),
- pokud cesta je lepší než aktuální `Out1` → obsadí slot 1, případně vyvolá swap `Out0 ↔ Out1`,
- `EnsureWritable`: pokud protějšek ještě nemá `newEdges`, vyrobí je a přidá ho do `toUpdate` (ripple efekt — změna se šíří i do původně nedotčených uzlů),
- pokud `lengthB < otherDist`, zařadí protějšek do heapu jako další kandidát na šíření.

Invariant „slot 0 je nejlepší" drží swap `Out0 ↔ Out1` po každé zápisové operaci. Jedna barva smí z uzlu vést ven jen přes jednu hranu (kontroluje `IsColorValid`) — jinak by stejná síla tekla ke kořeni dvěma různými trasami současně.

Po obou workerech je `newEdges` barevně konzistentní. V kroku 13 (`ApplyEdgeArrs`) se stává oficiálním `edges`.

### ForceWorker — dvoufázový přepočet

`joint.compress`/`joint.moment` jsou **persistentní akumulátory**. Držíme je mezi ticky a při změně upravujeme *deltami*, ne úplným přepočtem. Na tom stojí celý solver — drahý krok (propagace sil grafem) se provádí jen pro dotčené uzly.

Přepočet je kolem přepínače starého/nového grafu rozdělen na dvě symetrické fáze:

**Fáze A: `RemoveForces`** (před `ApplyEdgeArrs`) — odečtení starých příspěvků:
- Pro každý `toUpdate` uzel s `force != 0`:
  - `BestDistance` ze **starých** `edges` (stará topologie, stará barva).
  - Push do heapu `Work { force = -node.force }` (záporná síla → odečítá).
  - Propagace po staré cestě ke kořeni — na každém jointu `joint.compress += -abf`, takže se odečte přesně to, co se tam dříve přičetlo.

**Mezifáze**: aplikují se `UpdateForce` commandy (`node.force += ic.forceA`), pak `ApplyEdgeArrs` přepne topologii.

**Fáze B: `AddForces`** (po `ApplyEdgeArrs`) — přičtení nových příspěvků:
- Pro každý `toUpdate` uzel s `force != 0`:
  - `BestDistance` z **nových** `edges` (nová topologie, nová barva).
  - Push `Work { force = +node.force }`, propagace po nové cestě, `joint.compress += abf`.

**Symetrie**: pokud se topologie ani `node.force` nezměnily, Remove a Add se na každém jointu navzájem vyruší (testováno v `SpTests7ForceSymmetry`). Protože jde o aditivní akumulátor, nezáleží na pořadí průchodu heapem — stejná síla po stejné cestě se vždy odečte a přičte beze zbytku.

**Rozdělování sil mezi dvě cesty** (`Update` + `FindOtherColor`): pokud má uzel cestu k rootu A (délka `L1`) **a současně** k rootu B (délka `L2`), síla se rozdělí v inverzním poměru k délkám:
- cesta A dostane podíl `L2 / (L1+L2)`,
- cesta B dostane podíl `L1 / (L1+L2)`.

Kratší (tužší) cesta tedy nese větší část zátěže.

**Rozdělování v rámci jedné barvy** (`UpdateForce` + `GetInvLenSum`): pokud uzel vede tutéž barvu ven více hranami (více `Out*Root == color` slotů napříč `edges`), síla se po nich rozdělí podle vah:

```
w = 1 / (length * invLenSum)     kde invLenSum = Σ 1/Out*Length přes všechny Out* sloty s touto barvou
```

Součet vah je 1 (`Σ (1/Li) / Σ (1/Lj) = 1`), takže se zachová celková síla; kratší Out-hrana (tužší podcesta) nese větší podíl — stejný princip jako u rozdělování mezi dva různé rooty, jen aplikovaný na všechny odchozí trasy téže barvy najednou.

**Temp forces** (`AddTempForces`) — stejný algoritmus, ale zapisuje do `joint.tempCompress`/`joint.tempMoment`. Ty se resetují hned ve dvou místech: `MarkJointActive` je vynuluje, jakmile se joint v dalším ticku poprvé objeví v propagaci sil, a `GetBrokenEdgesBigOnly` je na závěr vyhodnocení vynuluje na všech zbývajících aktivních jointech. Výsledkem je jednokolový impulz.

### Detekce pádů a prasklin

Dvě oddělené detekce běží na konci ticku:

**Praskliny** — `ForceWorker.GetBrokenEdges*`. Spouští je `Runner` **po** návratu z `ApplyChanges`. Projde `activeEdges` (jointy, kterých se tenhle tick dotkla propagace sil):

```
damage = max(0, compress − compressLimit)
       + max(0, −compress − stretchLimit)
       + max(0, |moment| − momentLimit)
```

- `GetBrokenEdgesBigOnly` — produkční. Na každou **barvu** vybere jen jeden joint s nejvyšším `damage`. Účel: velký most nepraskne celý najednou — po ulomení jednoho článku se síly přepočítají a v další iteraci se ukáže, kolik dalších zůstalo slabých. Dá se tak simulovat postupné zhroucení.
- `GetBrokenEdges` — záložní varianta po 2 `BigOnly` iteracích (flag `moreBrokenEdges`): ulomí všechny zbývající poškozené jointy najednou.
- Každá prasklina se zapíše **dvakrát**:
  - `InputCommand(RemoveJoint)` → zpět do fronty pro další iteraci `ApplyChanges` (aby se joint z grafu skutečně odstranil),
  - `OutputCommand(RemoveJoint)` → pro game thread (`SpBreakEdge` zruší případný RB joint + spustí efekt).

**Padání** — `FindFallenWorker.Run`, volá se jednou v každém průchodu `ApplyChanges` (fáze 16). Hledá uzly, které ztratily spojení s jakýmkoliv kořenem:
- Pro každý index v `toUpdate` (pop-style): uzel je „spadlý", pokud `!IsConnectedToRoot()` — tedy sám není fixedRoot a žádná jeho hrana už nemá `Out0Root != 0`.
- BFS přes `edges`: sousedé, kteří jsou také v `toUpdate` a také spadli, se připojí do téže komponenty. Pro hranu mezi dvěma spadlými vygeneruje `FallEdge` (v game threadu se z toho postaví RB `FixedJoint`, aby komponenta padala jako jedno tuhé těleso).
- Pro každý spadlý uzel: `FallNode` command + `data.ClearNode` (odstraní ho z grafu).

Obě detekce jsou ortogonální:
- Prasklina = síla překročila limit → joint se odstraní, uzly mohou (ale nemusí) ztratit cestu ke kořeni.
- Pád = ztráta cesty ke kořeni → celá komponenta odchází do RB simulace.

Pád bývá **důsledkem** prasklin (joint praskl → subtree ztratí root), ale může přijít i z přímého `RemoveNode` (zničení kořene, viz test 18).

### Role `toUpdate` napříč průběhem

`HashSet<int> toUpdate` je centrální worklist ticku — jaké uzly potřebují přepočítat barvy/síly. Přidává a ubírá se do něj v průběhu pipeline; na začátku dalšího ticku je prázdný.

| Fáze | Operace | Důvod |
|---|---|---|
| `AddNode` | add `ic.indexA` | nový uzel potřebuje vlastní barvu |
| `AddJointPrepare` | add oba konce | hrana ovlivní barvy a síly obou stran |
| `RemoveJointPrepare` | add oba konce | totéž opačně |
| `RemoveNode` | add sousedy (ne sám mazaný) | sousedé ztratili hranu |
| `UpdateForce` command (fáze 10) | add `ic.indexA` | síla se změnila, musí se znovu propagovat |
| `DeleteColorWorker` (během Run) | add zasažené sousedy | invalidace se šíří grafem |
| `AddColorWorker.EnsureWritable` | add dotčené uzly | soused, který nebyl v původním `toUpdate`, ale color worker do něj chce zapisovat |
| `FindFallenWorker.Run` | odebírá (pop) | po průchodu je `toUpdate` prázdný |

**Čtení** `toUpdate`:
- `CreateNewEdgeArrs` — iteruje, staví `newEdges` pro každý uzel.
- `DeleteColorWorker.DetectChanges` — iteruje, hledá změněné SCD.
- `AddColorWorker.DetectWork` — iteruje, krmí heap.
- `ForceWorker.RemoveForces` / `AddForces` / `AddTempForces` — iterují.
- `ApplyEdgeArrs` — iteruje, commituje `edges = newEdges`.
- `FindFallenWorker.Run` — konzumuje pop-em až do prázdnosti.

Komplementární set `deletedNodes` drží uzly zrušené v tomto ticku — ostatní fáze je při iteraci `toUpdate` přeskakují (`!deletedNodes.Contains(i)`). `FreeNodes` na konci `deletedNodes` vyprázdní a vygeneruje `FreeNode` commandy.

**Proč HashSet a ne List**: z více fází se do něj přidává v libovolném pořadí a je potřeba zahazovat duplicity (uzel už v setu je). Iterace pop-em ve `FindFallenWorker` pracuje přes `HashSet.Enumerator`, který je value type — nealokuje.

## Integrace s hrou

- **Vznik spoje:** `RbJoint.Setup → SetupSp → AddNode1 / AddNodeAndEdge` pošle `AddNode(AndJoint)` commandy.
- **Zrušení spoje:** `RbJoint.Disconnect` pošle `RemoveJoint`.
- **Zničení objektu:** `Label.Cleanup → Placeable.DisconnectAll` mj. zavolá `StaticPhysics.RemoveNode`.
- **Impulz z kolize:** `Placeable.ApplyTempForce → SpInterface.AddForceCommand`.
- **Změna gravitace / váhy:** `ApplyForce` s relativním přírůstkem síly.
- **DeepGlue:** modifikuje joint limity za běhu (`UpdateJointLimits`).
