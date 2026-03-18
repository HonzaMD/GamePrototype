# Levly

## MapWorlds

- Konfigurace toho, jaké světy se mají nahrát.
- Při startu se z `Game` zavolá `MapWorlds.CreateMaps`.
  - Může nahrávat několik framů, hra se pozastaví.
- `MapWorlds` obsahuje setting pro každý svět:
  - Nyní zastropováno na 6.
  - Pokud stavíš více světů, nepoužívej pozici 0.
    - Světy jsou stavěny v 0 a na svou pozici se během nahrávání odsunou.
  - `WorldOffset` je 1000 m. Každý svět má kolem sebe hraniční oblast o šířce 200 m, takže mezera mezi světy musí být minimálně 400 m.
  - U světů se zadávají pozice a rozměry mapy — absolutní a v buňkách. Pozici v metrech musíš vynásobit 2.
  - `DebugLevel` — určí, co za level se nahraje ve světech, kde je jako level zadáno `Debug`.
- Funkce `MapWorlds`:
  - Loaduje světy a mapy.
  - Drží seznam světů a map.
  - Drží pointer na aktivní mapu/svět.
  - Umožňuje přepínání.

---

## WorldBuilder

- `MapWorlds` při loadingu pro každý svět nahraje jeho scény. V každé scéně vyhledá v rootu komponentu `WorldBuilder` a té řekne, ať se vybuildi.
- Nastavení:
  - Odkaz na root **Static Reflection Probes**
    - Tam jsou proby rozdělené podle světelných scénářů: 0 až 3 a variant A až B. Každá varianta je defaultně disabled.
  - Odkaz na **Volume**
    - Každý svět si může lokálně přenastavit grafiku.
    - Potřeba je, aby tam byl setting pro APV (Adaptive Probe Volume). Při loadingu se posouvají pozice APV spolu s tím, jak se posune svět.
  - Komponenta **TimeOfDay** — každý svět může mít jinak nastavené slunce atd.
  - **Děti** — určují, jak se postaví level:
    - `Level` — pokud vyhovuje aktuálnímu `buildMode`, tak se postaví, a pokud má podelementy, tak se rekurzivně vyhodnotí.
    - `EmptyElement` — náhodně vybere k postavení jednoho ze svých synů. Synové musí být `Level`y.
      - Nejprve si vyzkouší podle `ActivationWords`.
      - Poté podle náhody. Levely mají u sebe váhu, která udává, jak pravděpodobné jejich postavení je.
      - Pokud režim stavění není `ALL`, tak se postaví všechny varianty, které vyhovují `buildMode`.
  - Děti `WorldBuilder`u na sobě sbírají transformační offset — ten určuje pozici stavěné věci.
  - Stavět se může ta samá věc opakovaně na různé pozice — objekty se pak zduplikují.

---

## Level

- Odkazy na **Placeable Roots**:
  - Všechny rooty dej disabled (enablují se jen ty, které se v nějakém levelu použijí; mohou se i zduplikovat).
  - Root musí mít komponentu `LevelLabel`.
  - Root může obsahovat předpřipravené herní objekty (ne v `CloseSide`/`FarSide`).
  - Musíš zadat Root 0, pokud používáš stavění z písmenkového levelu.
  - `FarSide` a `CloseSide` jsou speciální rooty:
    - `CloseSide` dej do vrstvy `IgnoreRayCast`.
      - Objekty v `CloseSide` budou zduplikovány, aby správně vrhaly stíny, ale přitom nebyly v herní kameře vidět — ale byly vidět v reflection probech.
    - `FarSide` dej do `Default`.
- **Jméno levelu** — nastav, pokud chceš vybuildit level z písmenek.
- **LightVariant** — určuje, zda se level má stavět při režimech podle varianty.
- **LocalCellsX/Y** — dodatečný offset pro stavbu písmenkového levelu (v buňkách). Základní offset je daný pozicí Rootu 0 a ta se upravuje podle offsetu Levelu.
- Speciální objekty v Rootech:
  - `LightVariantRegion` — určuje, kde se nasvícení přepne na Variantu B.

---

## Random Seed

- Zadán v `MapWorlds`.
- Z něj se náhodně vygenerují seedy pro každou loadovanou scénu / `WorldBuilder` (náhoda se mezi loadingy ukládá).
- `WorldBuilder` použije seed na:
  - `InitActivationWords`
  - Náhodný výběr levelu
  - Vygenerování seedů pro každý level

---

## Activation Words

- Výběr levelu podle Activation Words (AW).
- AW mohou být zadány v `MapWorlds`, případně vygenerovány ze seedu.
- AW jsou stringy oddělené čárkou (čárky jsou jak u levelu, tak v zadání).
- K výběru dochází, pokud se alespoň jedno slovo shoduje.

---

## APV a statické globální světlo

- Každá scéna by měla mít APV (Adaptive Probe Volume).
- **Baking**:
  1. Načti scénu aditivně.
  2. Nasetupuj globální slunce podle scény.
  3. Build Level AB:
     - Spočti pozice Probe.
     - Clear Level.
  4. Build Level A.
  5. Pro každou variantu světla:
     - Nastav čas slunce.
     - Enable příslušnou skupinu statických Reflection Probes.
     - Nastav APV scénář.
     - Bake.
  6. Clear Level, Build Level B — a to samé pro B.
