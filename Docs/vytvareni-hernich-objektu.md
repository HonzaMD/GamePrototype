# Vytváření Herních Objektů

## 1. Vytvoř Grafiku (Model)

- Přidej Collider

## 2. Placeable

- Pro **jednoduché objekty**: implementuj jednoduchý potomek `Placeable` nebo použij přímo `Placeable`
- Pro **složitější**: použij `Placeable Sibling`

### PosOffset + Size

- Většině objektů má grafiku vycentrovanou kolem pivota
- Dlaždice mají pivot v levém dolním rohu
- Souřadnice:
  - `(0, 0, 0)` je v levém dolním rohu 0. buňky; `0z` je ve středu bližší vrstvy
  - `0.25z` je mezi vrstvama
  - `0.5z` je ve středu druhé vrstvy
- `PosOffset` je offset z pivota do levého dolního rohu
- Pro nastavení může pomoci (dočasný) kolider

### Rotující objekty

- Pokud obj není přibližně kruhový a rotuje, musíš přetížit `RefreshCoordinates` a upravovat v něm `PosOffset` + `Size`

### Ostatní nastavení

- Nastav `CellBlocking` pro 2-vrstvé bloky
- Nastav `SubCell Flags` pro 1-vrstvé
- Vytvoř a nastav `KSID` a potřebné závislosti (`KsidDependencies`)
- Vytvoř a přiřaď `Placeable Settings`
  - Pokud potřeba, vytvoř ikonu

## 3. Urči Vrstvu (Layer)

| Vrstva | Chování |
|--------|---------|
| `Default` | Koliduje se vším (stěny) |
| `MovingObjs` | Nekoliduje s `Markers` a `SmallObjs` |
| `SmallObjs` | Koliduje jen samo se sebou a s `Default` |
| `Catches` | Nekoliduje — slouží k chytům |

## 4. Rigid Body

- **Má fixní** — standardní přímý RB
- **AutoAttach** — potřeba pro Sand a statickou fyziku
- **Nemá** — žádný RB

## 5. Přidej Další Potřebné Komponenty

- `Collision Force To Sp` — potřeba pokud máš vlastní RB

## 6. Vytvoř Prefab

## 7. Přidej do Prefabs Store
