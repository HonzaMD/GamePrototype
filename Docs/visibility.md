# Visibility – stíny a okludery

**Stav:** popis současné implementace (`Assets/Scripts/Map/Visibility/`, 2026-11-07),
její limity, zdroje nestability a [nový návrh](#nový-návrh-2026-11-08) (varianty A a B, zatím
nevybráno, nic neimplementováno).

---

## Účel a výstup

Visibility zakrývá části světa, které hráč „nevidí“. Výstupem jsou **okludery** – objekty
`Occluder` (prefab `Assets/Prefabs/Occluder.prefab`, layer 11, `CastShadows: 0`) s procedurálním
meshem. Mesh je hranol: 2D polygon vytažený v ose Z od `ShadowFrontZ = -1.30` do
`ShadowBackZ = 1.80`. Vykreslena je přední podstava a boční stěny, zadní podstava ne.

Je to čistě vizuální systém – herní logika (AI apod.) z něj nic nečte.

Spouští se z `Game.Update` jen **každý 10. frame** (`visibilityPeriod`, `Game.cs:141`) na hlavním
vlákně, pro `MapWorlds.SelectedMap`, se středem v `Character.ArmSphere`. Při každém výpočtu se
všechny okludery zahodí (`DarkCaster.Free` → `Occluder.Kill`) a vytvoří znovu z poolu.

## Souřadnice a konstanty

| Co | Hodnota | Pozn. |
|---|---|---|
| viewport | 65 × 53 buněk (`HalfXSize = 32`, `HalfYSize = 26`) | 32,5 × 26,5 m, buňka 0,5 m |
| střed viewportu | buňka `(32, 26)` = buňka hráče | viewport se posouvá po celých buňkách |
| `centerPosLocal` | přesná pozice hráče v lokálních metrech | od lokální buňky 0 |
| `posToWorld` | světová pozice lokální buňky 0 | posun okluderů do světa |
| `ShadowRadius` | ≈ 24,7 m | 1,2 × polovina úhlopříčky viewportu; kam sahá zadní hrana stínu |
| `centerRadius` | 0,7 m | poloměr „oka“ pro sešikmení boků |
| `centerRadiusMarginSq` | (0,7 · 1,8)² | blíž než 1,26 m od hráče se směry boků nepočítají |
| `ffvMaxDistance` | 30 | dosah floodfillu (ortogonální krok 3, diagonální 4 → 10 buněk = 5 m) |

Neprůhledná je jen buňka, kde `map.GetCellBlocking(...).IsDoubleFull()` – obě Z vrstvy plně
obsazené. Mimo mapu je `Free` → okraj mapy je průhledný.

## Datové struktury

### `Cell` (vmap, 65 × 53)

- `state: CState`
  - `Unknown` – zatím nevyhodnoceno
  - `Visible` – floodfill, nebo `ShadowWorker` řekl „není v plném stínu“
  - `FullShadow` – zeď, nebo `ShadowWorker` řekl „plný stín“. **Všechny stavy ≥ `FullShadow`
    se chovají jako stín/zeď** (`IsFloor`/`IsSide`, `TestShadow`)
  - `Dark` – buňka v sektoru už vrženého stínu (nebo buňka hotového DC)
  - `DSeed` – buňka rostoucího DarkCasteru
- `wallType` – `Side | FloorSet` u zdí (bitový trik pro `shift` v `ShadowWorker`)
- `darkCaster: short` – id DC, kterému buňka patří (DSeed) nebo jehož stín na ni padá (Dark)

### `seedsMap` (byte, stejná velikost)

Nápověda „tuhle buňku má smysl testovat na seed“. Nenulová hodnota = test v `CreateDarkSeeds`.
Plní ji dvě různé funkce se stejným jménem `MarkPartShadowsInNearCells`:
- `(pos)` – za každou zdí +1 na 3 buňky směrem **od** hráče (`VCore.cs:464`)
- `(pos, delta)` – ±delta na 8 sousedů levé/pravé krajní buňky DC; posouvá se s krajem DC,
  takže DC může růst do stran (`VCore.cs:476`, `DarkCaster.SetCell`)

### `DarkCaster` (DC)

Shluk buněk, který vrhá jeden stín.
- `cells` – seznam buněk (seedy + případně jedna „mostová“ `Dark` buňka cizího stínu)
- `LeftCell/RightCell`, `LeftPoint/RightPoint` (středy krajních buněk), `LeftDir/RightDir`
  (směry boků, viz *Sešikmení*)
- `connectsLeft/Right` – bok je přilepený na bok sousedního stínu
- `Live` (rostl v tomto kruhu), `Active` (ještě se zpracovává), `Group` (`DCGroup`)

Sektor stínu jde **po směru hodinových ručiček** od `LeftDir` k `RightDir`.

### `DCGroup`

Skupina DC, jejichž stíny se spojily (`TestCollapse`). Slouží jen k `GroupLeftDir/GroupRightDir`
a `GroupEquals`.

### `DarkBorders`

Množina úhlových intervalů – už vržené stíny. Hrany (`Border` = směr + DC + levá/pravá)
seřazené po směru hodinových ručiček ve 4 polích podle kvadrantu (nahoru, doprava, dolů, doleva).
Levé a pravé hrany se musí střídat (`IntegrityCheck`). Pozor: `Border.Dir` je **kopie** při
vložení, `Border.Point` se čte **živě** z DC.

## Průběh výpočtu – `VCore.Compute`

```
Reset
CastShadows          zdi → FullShadow, seedsMap za zdmi
FloodFillVisible     okolí hráče → Visible
for cc = 2 .. 31:
    DrawDarks(cc)                 kruh cc obarvit podle DarkBorders → Dark
    DoCircle(cc-1, CreateDarkSeeds)  kruh cc-1: hledání seedů, růst DC
    if TryCastDark():             hotové DC → DarkBorders + Occluder
        DrawDarks(cc)             překreslit kruh cc novými stíny
TryCastDark
BuildMeshes
```

„Kruh“ = buňky ve Čebyševově vzdálenosti `cc` od středu. Zpracovává se zevnitř ven, protože
vyhodnocení stínu v kruhu `k` stojí na výsledcích kruhů `< k`.

### 1. `CastShadows`

Pro všechny buňky kromě krajního rámu: zeď → `FullShadow`, `wallType`, a +1 do `seedsMap` na
3 buňky za zdí (od hráče: `±x`, `±y`, diagonála).

### 2. `FloodFillVisible`

Dijkstra s kbelíkovou frontou (5 front podle přírůstku vzdálenosti) z buňky hráče přes
`Unknown` buňky. Ortogonální krok 3, diagonální 4 jen když jsou oba ortogonální sousedé
viditelní (žádné řezání rohů). Do 5 m. Výsledek `Visible`.

Účinek: viditelné buňky **nemohou být seedem** a sousedním kandidátům přidávají „viditelné“
body (viz krok 3b). Díky tomu se v blízkém okolí stíny vůbec nezakládají a hráč vidí za roh.
Floodfill ale **přímo nic neodkrývá** – pokud se DC založí dál a jeho bok přes floodfill buňky
přejde, mesh je zakryje.

### 3a. `DrawDarks(cc)`

Projde kruh `cc` po směru hodinových ručiček (4 strany zvlášť). Pro první buňku najde v
`DarkBorders` nejbližší hranu po směru (`StartDrawing`); je-li to pravá hrana, buňka je uvnitř
sektoru. Při posunu po kruhu testuje, zda střed buňky překročil polopřímku aktuální hrany
(polopřímka začíná v `Border.Point`, ne v hráči) a pak přejde na další hranu
(`ContinueDrawing`). Buňky uvnitř sektoru dostanou `state = Dark`, `darkCaster = id`.
Přepisuje se **jakýkoli** stav.

`Dark` buňky pak slouží jako: blokátor pro `ShadowWorker`, zákaz seedu, a místo, kde se nový DC
napojí na existující stín (`FindTouchingShadow`).

### 3b. `CreateDarkSeeds(pos)` – kruh `cc-1`

Jen pro buňky s `seedsMap != 0`, které nejsou `Visible`/`Dark`. Skóre přes okolí 3 × 3 (vč. sebe):
- soused „za“ buňkou (od hráče) → 1 bez testu,
- jinak se soused vyhodnotí (`Unknown` → `ShadowWorker`); `Visible` → 1, stín → 0.

Za buňkou jsou vždy 3 sousedé, takže skóre ≤ 3 ⇔ **buňka i všichni její sousedé směrem k hráči
a do stran jsou ve stínu**. Taková buňka je kandidát → `DCManager.AddDarkCandidate`.

Důsledek: první (povrchová) vrstva zdi seedem není, protože má viditelné sousedy. Seedy jsou
druhá a další vrstva tlustých zdí a prázdné buňky ve stínu za zdmi. Stěny, které hráč vidí,
proto zůstávají odkryté.

### 3c. `ShadowWorker.ResolvePartShadow(pos)`

Rozhoduje, zda je buňka v **plném** stínu z **bodového** oka (`centerPosLocal`, bez sešikmení).
Souřadnice převede do kanonického oktantu (`up` = k hráči po dominantní ose, `right` = k
hráči po vedlejší, `shift` prohazuje význam floor/side). Pak jde po hranách zdí („floor“ =
hrana kolmá na `up`, „side“ = rovnoběžná) v okně **max. 2–3 buňky směrem k hráči** a ověřuje,
zda řetěz hran zakryje paprsek k protějšímu rohu buňky.

Za blokátor se počítá každá buňka se stavem ≥ `FullShadow` – tedy i dříve vyřešené stinné
prázdné buňky a `Dark`. Pro bodové oko je to korektní (do plně zastíněné buňky žádný paprsek
nedoletí), ale funguje to jen pokud jsou buňky před ní už vyřešené. `Unknown` buňky se berou
jako průhledné.

### 3d. `DCManager.AddDarkCandidate`

- `Test8` okolí: najde sousední `DSeed`. Žádný → `CreateNew`, jeden → `Attach`, víc různých →
  `JoinWith` (přebarvení buněk, sloučení krajních bodů, starý DC `Abandon`).
- `FindTouchingShadow` (`Test4`): pokud seed sousedí s `Dark` buňkou cizí skupiny, přidá tu
  buňku do DC jako „most“ a v `TestOtherPoint` zkusí přilepit bok: když hranice v `DarkBorders`
  u tohoto směru patří sousední skupině, nastaví `connectsLeft/Right` a převezme její směr.

### 3e. `DCManager.TryCastDark` a `DarkCaster.CanCast`

| Situace | Výsledek |
|---|---|
| `connectsLeft && connectsRight` | vrhnout |
| v tomto kruhu rostl (`Live`) | vrhnout, pokud je dost velký (`≥ 5` buněk, `≥ 7` když napojený) a boky se rozbíhají; zahodit, pokud se nerozbíhají po > 3 iteracích a má > 10 buněk |
| nerostl | vrhnout, pokud se boky rozbíhají, jinak zahodit |
| jinak, napojený (`IsReCastable`) | přidat do `DarkBorders` předběžně, dál růst |

Vržení = `DarkBorders.Add` → buňky na `Dark` → `InitOccluder`. Zahození = buňky na `FullShadow`,
**žádný okluder**.

Pozor: DC se vrhne i když ještě roste (stačí „dost velký“). Po vržení jsou jeho buňky `Dark`, nové
seedy se k němu nepřipojí (`FindSeed` hledá jen `DSeed`) a vznikne nový DC napojený bokem. Dlouhá
zeď je tak rozdělená na řetěz okluderů.

### 3f. `DarkBorders.Add`

Najde pozice obou hran nového sektoru a spočítá, kolik existujících hran leží mezi nimi:
- 0 → buď celý uvnitř existujícího stínu (`return false`), nebo v mezeře,
- 1 → překryv s jedním okrajem,
- 2 → nový sektor obsahuje celý jiný stín → **odmítnut** (`return false`),
- > 2 → výjimka.

Na překrývajících se okrajích `TestCollapse` testuje, zda se polopřímky sousedních hran
protínají; pokud ano, sektory se sloučí (`DCGroup.Join`), směr jedné hrany se srovná na druhou
(`CorrectLeftDir/RightDir` → `connects`) a hrany zmizí. Když zmizí všechny, `fullCircle`.

### 4. `BuildMeshes` → `MeshBuilderWorker.Build`

Pro každý DC s okluderem:
1. `ReMarkCells` – buňky DC dostanou unikátní `markId` (`IsColored`).
2. `ComputeStartPoint` – na levé/pravé krajní buňce vybere roh nejvzdálenější ve směru normály
   boku, případně se „vyškrábe“ z zanořené buňky na okraj shluku.
3. `FindFrontEdge` – sleduje obrys obarvených buněk od levého k pravému rohu (8-souvislost přes
   `CrossPos`). Buňky jsou nafouknuté o 0,05 m, aby okluder bezpečně obalil geometrii zdí a
   sousední okludery přesahovaly (viz *Margin* v novém návrhu). Konvexní rohy
   dostanou `Work` uzel.
4. `TriangulateFront` – ořezávání uší jen na přední linii, vždy nejkratší přepona první.
5. `AddBackEdge` – dva body na bocích ve vzdálenosti `ShadowRadius` (+ prostřední bod, když je
   úhel > 60°), uzavřít smyčku.
6. `TriangulateBack` – ořezávání z obou konců, pak obecné ořezávání (`TringulateRest`).
7. `TriangulateSide` – boční stěny hranolu podél přední linie a obou boků (zadní hrana bez stěny).

### Tvar polygonu

```
                    zadní hrana (ShadowRadius)
             L' *-----------*-----------* R'
               /                         \
     levý bok /                           \ pravý bok
  (LeftDir)  /                             \ (RightDir)
            /                               \
         L *--+   +--+          +-----+------* R
              |___|  |__________|
          přední část po obrysech buněk (k hráči)

                         ( hráč )
```

### Sešikmení boků

`ComputeLeftRightDir`: pro bod `a` se oko posune o 0,7 m kolmo na směr hráč→`a`. `LeftDir` vede
z oka posunutého proti směru hodinových ručiček, `RightDir` z oka posunutého po směru. Obě hrany
se tím stočí dovnitř stínu – je to **plný stín (umbra) kotouče o poloměru 0,7 m**. Hráč tak
vidí za roh a za úzké překážky.

Vedlejší efekt: pro objekt kolmé šířky `w` (měřeno mezi **středy** krajních buněk) se boky
rozbíhají jen když `w > 1,4 m`, tj. **alespoň 4 buňky**. Užší objekt má umbru konečnou
(trojúhelník), kterou systém neumí – stín nevrhne vůbec, ledaže se bokem napojí na sousední stín.

## Limity jednotlivých složek

| Složka | Limit |
|---|---|
| Vstup | Neprůhledná je jen `IsDoubleFull` buňka. Částečné buňky, jedna Z vrstva, objekty mimo mřížku nestíní. Krajní rám viewportu se nezkoumá, okraj mapy je průhledný. |
| Časování | Každý 10. frame, celé na hlavním vlákně (špička). Stín se při pohybu opožďuje a skáče po ~0,17 s. |
| Floodfill | Pevných 5 m přes jakoukoli nezeď. Jen brání seedům, nic neodkrývá. Buňky floodfillu může `DrawDarks` přepsat na `Dark`. |
| `ShadowWorker` | Bodové oko (jiný model než boky). Okno 2–3 buňky. Výsledek závisí na tom, co bylo vyřešeno dřív (`Unknown` = průhledné), počítá se líně jen u kandidátů → **závislost na pořadí**. |
| Seedy | Jen v kruzích ≤ 30 / 24 (rezerva 2 buňky). Pravidlo 3 × 3 je diskrétní a citlivé na jednu buňku. |
| DC | Min. 5 (7) buněk; boky se rozbíhají až od ~4 buněk šířky; heuristika zahazování (`iterations > 3 && cells > 10`); zahozený DC = díra bez okluderu. Buňky blíž než 1,26 m nemají směry. |
| `DarkBorders` | Řazeno jen podle směru, ignoruje počátky polopřímek (ty jsou u každého DC jinde). Hledání sousedů jen do vzdálenosti 1 kvadrantového pole (`AbsRelArrDist`) → sektory nad ~90–180° nejsou spolehlivé. Sektor obsahující jiný stín je odmítnut. |
| Spojování | Jen první dotýkající se `Dark` soused (`Test4`, první nalezený). Spojení závisí na tom, ve kterém kruhu se kdo vrhl. |
| Mesh | Ořezávání uší **bez testu, zda v uchu neleží jiný vrchol** → u složitých předních tvarů trojúhelníky mimo polygon. Zadní hrana s jedním prostředním bodem pokryje roh viewportu jen do úhlu sektoru ~134° (`ShadowRadius·cos(θ/4) ≥ 20,6 m`), pro ≥ 180° je `centerDir` špatně. Boky meshe začínají v rohu buňky, ale `DrawDarks` používá střed buňky → stín v mřížce a stín meshe se o kus liší. |
| Robustnost | Výjimky (`IntegrityCheck`, „DC obsahuje vic DC“, „cyklus v ShadowStep“, „neocekavany case“) nejsou chyceny – přeruší celý `Compute` uprostřed (okludery vzniklé, meshe nepostavené). Chycena je jen `CyclusException` v `BuildMeshes`. `IntegrityCheck` běží při každém `Add` i v release. |

## Zdroje nestability / problikávání

Seřazeno podle odhadovaného vlivu:

1. **Topologie DC závisí na pozici hráče.** Kde se dlouhá zeď rozdělí na řetěz DC (vrhnutí při
   dosažení 5 buněk), které DC se spojí, které se zahodí, které je odmítnuto – vše jsou diskrétní
   rozhodnutí. Malý posun hráče přepne rozhodnutí a okluder zmizí/objeví se nebo se přeskládá.
2. **Zahozený nebo odmítnutý DC nemá žádný okluder.** Přechod vrhnout ↔ zahodit je okamžitě
   vidět jako díra.
3. **Prahy:** min. velikost DC, rozbíhavost boků (~4 buňky), `iterations > 3 && cells > 10`,
   `centerRadiusMarginSq`. Objekty kolem prahu blikají.
4. **Závislost na pořadí v `ShadowWorker`** – stejná buňka může dopadnout jinak podle toho, které
   buňky před ní už byly vyřešené.
5. **Posun viewportu po celých buňkách** + pořadí v `DoCircle` → při přechodu hráče přes hranici
   buňky se mění pořadí zpracování.
6. **Perioda 10 framů** a úplná regenerace okluderů – skoky geometrie místo plynulé změny.

Spojité části (směry boků z přesné pozice hráče) jsou stabilní; nestabilita je v diskrétní
vrstvě nad nimi.

## Podezřelá místa (všimnuto, ne systematicky hledáno)

- **Zastaralé hrany v `DarkBorders`:** `Border.Dir` je kopie, `Border.Point` se čte živě z DC.
  DC přidané předběžně (`IsReCastable`) dál rostou a mění `LeftPoint/RightPoint`, takže hrana má
  starý směr s novým počátkem. Opakované `Add` téhož DC v dalších kruzích většinou vrátí `false`
  (sektor pokrývá sám sebe), takže konečné směry se do hranic nepromítnou. Při vržení pak
  `added = Add(dc) || IsReCastable` vrátí `true`, i když se hranice neaktualizovaly.
- **`DrawDarks` přepisuje `DSeed` živého DC** na `Dark` s jiným id – taková buňka vypadne z
  `RecolorFinishedCells` i z `FindSeed`.
- **`DrawDarks` posune jen o jednu hranu na buňku** – sektor nebo mezera užší než rozestup buněk
  na kruhu se posune o buňku.
- **Po `TryCastDark` se překreslí jen kruh `cc`**, kruh `cc-1` (právě zpracovaný) zůstane bez
  `Dark` od nových stínů.
- **`MarkPartShadowsInNearCells(pos)` na ose** (`dx` nebo `dy` = 0) přičte i do samotné zdi a dvakrát
  do jedné buňky. Neškodné, jen nepřesné.
- **`AddBackEdge` pro široké sektory** – viz limity (nepokryté rohy viewportu nad ~134°, nesmysl
  ≥ 180°).
- **Ořezávání uší bez testu obsahu** v `TriangulateFront` i `TringulateRest`.
- `TringulateRest` při zacyklení jen zaloguje a vrátí neúplný mesh.

## Příležitosti ke zjednodušení

### V rámci současné architektury

- **`WallType` je mrtvý.** Nastavuje se jen u zdí a ty mají vždy `state = FullShadow`, takže
  `IsFloor/IsSide` skončí na první podmínce. Pokud se neplánují poloprůchozí buňky (plošiny),
  dá se `wallType` i trik s `shift` odstranit (`IsFloor/IsSide` → `state >= FullShadow`).
- **`MeshBuilder.cs` se nepoužívá** (prototyp). Smazat, stejně jako zakomentovaný `PartShadow`.
- **Dvě funkce `MarkPartShadowsInNearCells`** dělají něco jiného – přejmenovat (např.
  `MarkBehindWall` / `MarkAroundEdgeCell`).
- **`Border`** ukládat jen `DC + IsLeft` a směr i bod číst z DC (nebo obojí kopírovat) – ne mix.
- **`DCGroup`** jde nahradit polem `groupId` s union-find; slouží jen ke 3 dotazům.
- **Výjimky** chytat v `Compute` a při chybě ponechat okludery z minulého výpočtu.
- `HasFloor(Vector2Int.zero)` v `ShadowWorker` je vždy `false` (volá se jen na `Unknown`).

### Zásadní: oddělit klasifikaci buněk od geometrie

Hlavní zdroj složitosti i nestability je, že se v jednom průchodu kruh po kruhu míchá:
vyhodnocení stínu v mřížce (bodové oko) ↔ seskupování buněk do DC ↔ úhlové hranice sešikmených
stínů (jiný model oka) ↔ zpětné obarvení mřížky stíny (`DrawDarks`), které ovlivní další
vyhodnocení. Na tom visí `DarkBorders`, `DCGroup`, `connects`, `IsReCastable`, `Live/Abandon`,
heuristiky a závislost na pořadí.

Sešikmení (umbra kotouče) přitom šikmé boky nepotřebuje počítat přes skupiny DC – umbra je
**průnik bodových stínů ze všech bodů oka**. Bodový stín je standardní, robustní a spojitý v
pozici oka (stín za každou k hráči natočenou hranou zdi je čtyřúhelník vytažený od oka;
sjednocení řeší překryv samo).


## Nový návrh (2026-11-08)

Výsledná varianta zatím **není vybraná** – A i B se mají vyzkoušet. Společná část platí pro obě.

### Požadavky

- Okludery nepřístupných buněk: polygon po obrysech buněk, bez boků a zadní části.
- Odstranit problikávání. Hrana stínu má být ostrá. **Co nemá být vidět, opravdu nesmí být vidět.**
- Zachovat úvodní floodfill (vidět za roh v blízkém okolí) a sešikmení boků (umbra oka r = 0,7 m).
- Vidět za zeď v dosahu floodfillu je záměr – floodfill 1 chápeme jako rozšíření očí.

### Společná část (CPU)

1. **Snapshot mapy:** do vmapy zkopírovat `IsDoubleFull` buňky (zeď). `WallType` zrušit – nic
   takového ve hře není. Díky snapshotu půjde CPU část přesunout do jobu.
2. Všem buňkám nastavit příznak **Dark**.
3. **Floodfill 1** (Dijkstra jako dnes, krok 3/4, dosah 30): navštíví i zdi, ve zdi se zastaví.
   Každé navštívené buňce (volné i zdi) smaže Dark a nastaví **Visible**.
   - Pracuje po celých buňkách → po opravě algoritmu **hlavní zbývající zdroj skákání**.
   - Plynulost: uložit vzdálenost z Dijkstry (do textury) a práh porovnávat se zohledněním
     pozice hráče uvnitř buňky – hranice se pak posouvá plynule a zůstane ostrá.
4. **Floodfill 2** (BFS, ne Dijkstra; vlastní značení navštívených):
   - šíří se ve 4 směrech po volných buňkách; zeď: smaže Dark a zastaví se; zastaví se na
     okraji viewportu; nic neznačí jako Visible,
   - zakázaný je směr, který vede nejvíc zpět ke středu (optimalizace; na diagonále |dx| = |dy|
     zakázat oba, kvůli symetrii),
   - vlastnost: dosažená je buňka, ke které vede cesta s **neklesající Čebyševovou vzdáleností**
     od středu. Fill se tedy záměrně vrací i za zdi tam, kam nevidíš,
   - výsledek: Dark zůstane ve vnitřku tlustých zdí (≥ 3 buňky) a v prostorech uzavřených do
     stran. Výčnělky se záměrně ořežou – Dark má menší obrys než původní zdi,
   - nezávisí na pozici hráče uvnitř buňky ani na pořadí → stabilní.
5. **Hrany o nulové tloušťce** (tenké zdi, které Dark nemají, ale stín vrhat musí):

   ```
   A B C
   D E F      hrana mezi B a E, B je blíž ke středu než E
   ```

   Hrana existuje, když: B ani E není Dark (Dark hrany už obsahuje), B je zeď, E není Visible,
   (A nebo D je zeď) a (C nebo F je zeď). Detekují se všechny orientace, kde je B blíž než E –
   nezáleží na dominantní ose. Konce zdí a samostatné bloky hranu nemají (záměr – jako ořez
   výčnělků).
   - A: hrany se přidají k okluderům a vrhají stín. Stín zdegenerovaný do nulového objemu
     (hrana u hráče) se vynechá.
   - B: místo hrany se E prohlásí za Dark.

Výstup: Dark buňky (vždy zakryté), hrany, Visible buňky (+ vzdálenost floodfillu 1).

### Margin – bezpečné obalení (A i B)

Zakrývaný objem musí geometrii, která nemá být vidět, **bezpečně obalit** s rezervou cca 5 cm
(dnešní algoritmus nafukuje buňky o 0,05 m). Bez rezervy leží hranice okluderu přesně na
stěnách kvádrů zdí:
- rasterizace a přesnost floatů rozhodují pixel po pixelu, kdo vyhraje → na hraně problikávají
  okraje kvádrů, které mají být skryté (z-fighting / tenké světlé linky),
- totéž na hranici masky: bilineární vzorek, marching squares a rasterizace stínů mají chybu
  ~1 texel,
- geometrie objektů nemusí přesně sedět v buňce (zkosení, mírné přesahy).

Proto se mírně zvětšuje:
- **objem Dark buněk** – každá Dark buňka zakrývá o margin víc do všech stran,
- **šířka vrhaných stínů** – boky stínu posunout o margin ven (stín vrhá nafouknutý obrys),
- **hranice Visible výjimky** – Visible oblast se o margin zmenší, jinak by řez stínu u Visible
  buňky ležel přesně na stěně skryté zdi a problikával stejně.

Rezerva je vždy na stranu tmy – v souladu s požadavkem „co nemá být vidět, nesmí být vidět“.

Realizace:
- A: polygony Dark oblastí nafouknout o margin (jako dnes), boky stínu rovnoběžně posunout o
  margin ven.
- B: buď **dilatace výsledné masky** o margin (separabilní max filtr, při 32 texelech/buňku
  ~3 texely; jednoduché, uniformní), nebo geometricky – Dark buňky kreslit nafouknuté,
  čtyřúhelníky stínů z hran nafouknutého obrysu a Visible test zmenšený o margin. Dilatace
  řeší všechny tři body najednou.

### Varianta A – okludery jako polygony (CPU)

1. Souvislé oblasti Dark buněk + hrany → okludery. Příliš široké (> 45°) rozdělit na 2 a více
   skupin buněk a hran; každá skupina je samostatný okluder.
2. Krajní body a směry stínu se sešikmením. Krajní bod vybírat podle nejvíc vytočeného
   *sešikmeného* směru (jako dnes `TestOtherPoint`), ne podle úhlu – jinak může bok protnout
   vlastní obrys oblasti a polygon protne sám sebe.
3. Rozbíhavost se nevyžaduje: sbíhavý stín je trojúhelník s vrcholem v průsečíku boků.
   Okluder může být i bez stínu (uzavřená křivka).
4. Stín, který vede přes Visible buňky, se nekreslí. Uříznutí i dlouhého stínu je v pořádku
   (floodfill 1 = rozšíření očí). Jde o diskrétní rozhodnutí → možný skok.
5. **Vzájemné stínění okluderů – nejtěžší část:**
   - detekce: okludery nemají dobré uspořádání, mohou se zakrývat v cyklu,
   - úprava: zakrytému okluderu změnit krajní bod a směr tak, aby mezi stíny **nevznikl klín
     světla** (sešikmení obou stínů zvlášť klín otevře; na styku musí být bok bez sešikmení /
     převzít směr souseda – dnešní `connects`). Úplně zakryté okludery vyřadit,
   - návrh: místo párových testů úhlové zametání – pro každý směr nejbližší okluder, sešikmení
     jen tam, kde vzdálenost skočí. Cykly tím odpadnou.
6. Na dělicí hraně rozdělené oblasti platí totéž: bok musí být napojený, jinak klín.
7. Triangulace: dnešní ořezávání uší nemá test obsahu ucha. Pro okludery bez stínu je
   jednodušší netriangulovat a generovat obdélník na řádkový úsek Dark buněk (+ stěny jen po
   hraničních hranách). Pro stín: přední linie + boky jako dnes, s opravou.
8. Margin: polygony nafouknout, boky stínu posunout ven (viz *Margin*).

Plus: zůstává dnešní vykreslování (hranoly). Minus: diskrétní kroky (dělení, vyřazení, ořez přes
Visible), složité vzájemné stínění, vše na CPU.

### Varianta B – maska na GPU

#### Maska

1. **CPU → GPU:** buněčná textura 65 × 53 (Dark vč. E z hran, Visible, vzdálenost floodfillu 1)
   a pozice očí: střed + 8 bodů na kružnici r = 0,7 m.
   - Každé oko **zkrátit raycastem ze středu** (na zásah minus epsilon), jinak oko u tenké zdi
     leží za ní a vidí za zeď. Zkracování je plynulé. Oko uvnitř zdi nevadí (je slepé).
   - 8 bodů = vepsaný osmiúhelník (apotéma 0,65 m) → umbra o kousek větší než u kotouče, chyba
     na bezpečnou stranu.
2. **Stíny (render target, světové XY zarovnané na buňky):** pro každé oko čtyřúhelníky z
   hraničních hran Dark oblastí, odvrácených od oka, vytažené do nekonečna (w = 0). Stačí to:
   stín oblasti = sjednocení stínů jejích hraničních hran. Geometrii generuje vertex shader z
   buněčné textury, CPU neposílá mesh. Povrch zdí není Dark, takže zůstane osvětlený.
   Akumulace: kanál na oko (blend Max, 9 očí → 3 × RGBA8), nebo stencil + čítač v jednom kanálu.
3. **Kombinace:** `tma = dilatace((stín ze všech očí ∧ ¬Visible) ∨ Dark, margin)` – viz
   *Margin*. Výsledek R8, globální textura (mohou ji číst i shadery částic). Dark je díky `∨`
   zakrytý vždy, margin zajistí, že ho zakrývaná geometrie nepřesahuje.

Vlastnosti: spojité v pozici hráče (kromě floodfillu 1), nezávislé na pořadí, CPU dělá jen
floodfilly, hrany, 8 raycastů a upload malé textury → může běžet každý frame.

#### Rozlišení masky

Při viewportu 32,5 m na ~1920 px je 1 px ≈ 1,7 cm, buňka ≈ 30 px.
- **32 texelů/buňku** (1,56 cm ≈ 1 px, maska 2080 × 1696) – ostré i bez vyhlazení. Doporučeno.
- 16 texelů/buňku (3,1 cm) – s MSAA a pokrytím 0–1 místo 0/1.

Celé číslo texelů na buňku → hranice Visible/Dark přesně na texelech, posun viewportu po
buňkách bez převzorkování. Plnění při kreslení jen odvrácených hraničních hran ≈ několik
viewportů na oko (~100 M zápisů texelů/frame při 9 očích).

#### Zakrývání

Maska popisuje tmavý objem `{(x,y,z) : maska(x,y) = 1, ShadowFrontZ ≤ z ≤ ShadowBackZ}` – totéž,
co dnes hranoly. Zakrývá se překreslením unlit materiálem okluderu, jako dnes. Je potřeba
**emulovat boky hranolu**; test jen v bodě objektu nestačí (objekty ve stínu by se prozradily
černým obrysem proti pozadí).

Souřadnice (vše světové): kamera je perspektivní v záporném z a dívá se k +z.
- **P** – bod, kde paprsek pixelu protne rovinu `z = ShadowFrontZ` (= world position fragmentu
  krycího obdélníku v té rovině).
- **Q** – bod, kde tentýž paprsek narazí na první neprůhledný objekt (z hloubky scény:
  `Q = kamera + V · t`), zkrácený na `z = ShadowBackZ`.
- Paprsek prochází tmavým objemem před objektem ⇔ 2D úsečka `P.xy → Q.xy` protne tmu v masce.
  Uprostřed obrazu je úsečka bod, u okraje (sklon 0,5) 0,65 m pro objekt na z = 0, ~1,5 m pro
  pozadí.

Zpětně v uzavřeném tvaru nelze zjistit „vidí pixel bok?“ – maska je rastr; je to hledání
prvního přechodu na úsečce. Proto tři možnosti:

| | B1 hledání po úsečce | B2 generované boky | B3 vrstvy |
|---|---|---|---|
| Princip | jeden obdélník v `z = ShadowFrontZ`, shader projde úsečku P→Q, zakryje při prvním tmavém vzorku a zapíše hloubku místa vstupu | přední obdélník s `clip` + svislé obdélníky boků z hranice masky (compute marching squares → buffer úseček → `DrawProceduralIndirect`) | N obdélníků po Δz v celém pásu, každý s `clip` podle masky, kreslené zepředu dozadu |
| Materiál | transparentní (čte Scene Depth), Depth Offset | opaque | opaque |
| Přesnost | ~1 texel | ~1 texel, bez zubů | zuby až Δz · sklon paprsku |
| Cena | pixely × až ~50 vzorků | compute přes masku + tisíce obdélníků | N × fullscreen (zakryté pixely zahodí early-Z) |
| Složitost | smyčka v Shader Graphu (custom function) | procedurální vrcholy v HDRP Shader Graphu (Vertex ID + buffer) | nejjednodušší – běžný Shader Graph, statický mesh |

B2 detaily: hranice přední stěny (bilineární vzorek ≥ 0,5) a marching squares se uvnitř texelu
mohou nepatrně lišit → spára; řešit přesahem. Pořadí za frame: masky očí → marching squares →
opaque průchod (command buffer např. v `RenderPipelineManager.beginContextRendering`).

B3 zuby: mezi poslední vrstvou před objektem a objektem může paprsek vstoupit bokem a žádná
vrstva to nezachytí – chyba na nebezpečnou stranu. Zmírnění:
- **konzervativní vrstva:** vrstva `z_k` testuje celý kousek paprsku v `[z_k, z_k + Δz]`
  (2–3 vzorky podle směru pohledu) → zuby zůstanou, ale na bezpečné straně (tma se zakousne do
  objektu max. o Δz),
- hustší vrstvy kolem z = 0, kde jsou objekty,
- úplně bez zubů jen B1 nebo B2.

### Doporučený postup zkoušení

1. Společná část (snapshot, floodfilly, hrany) + debug zobrazení buněk.
2. B: maska z očí jako debug textura → zakrývání B3 (nejrychlejší prototyp) → podle výsledku B2.
3. A zkoušet, pokud B narazí (výkon GPU, HDRP).

### Otevřené otázky

- Výběr A/B a způsobu zakrývání (B1/B2/B3) – podle prototypu.
- Počet očí a rozlišení masky.
- E z hrany prohlášené za Dark je pak vždy zakryté – u konců tenkých zdí, kam posunuté oko
  vidí, se může ukázat jako hranatá tmavá buňka. Případně oddělit příznak „jen vrhá stín“.
- Plynulá hranice floodfillu 1 – jak přesně interpolovat vzdálenost se sub-cell pozicí.
- Časové vyhlazení: zakrývat okamžitě, odkrývat případně s krátkým fade.
