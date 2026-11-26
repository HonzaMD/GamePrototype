# KsidTable – globální tabulky indexované Ksid

Tabulka hodnot pro dvojici (Ksid řádku, Ksid sloupce), kde v datech není žádná pravidelnost a je užitečné vidět a editovat vše najednou. První použití: `FoodTable` – výživové hodnoty (jídlo × konzument).

Na rozdíl od `DamageResistance` (zadává se u každého druhu zvlášť v `PlaceableSettings`) je to jeden asset pro celou hru.

## Datový model

- `KsidTable` (`Assets/Scripts/Utils/KsidTable.cs`) – abstraktní `ScriptableObject`:
  - `Rows`, `Columns` – seznamy Ksid na osách v pořadí zobrazení. Na osách mohou být obecné i specifické Ksid.
  - `entries` – **řídký** seznam `(Row, Col, Value)`. Chybějící záznam = **null buňka** – nic neurčuje a dává šanci buňce s obecnějším Ksid.
- `KsidTable<TCell>` – konkrétní typ buňky. `TCell` je malý struct s fieldy `float`/`int`/`bool`.
- Nová tabulka = struct buňky + třída `XxxTable : KsidTable<Xxx>` s `[CreateAssetMenu]`. Editor funguje bez dalšího kódu.

```csharp
[Serializable] public struct FoodValue { public float Energy; public float Waste; }

[CreateAssetMenu]
public class FoodTable : KsidTable<FoodValue> { … }   // řádky = jídlo, sloupce = konzument
```

## Hledání buňky (dědění)

Dotaz (row, col) – obě osy se řeší najednou (`KsidTableResolver`):

1. **Kandidáti** = vyplněné buňky (r, c), kde `r` je předek-nebo-rovno `row` a `c` je předek-nebo-rovno `col`.
2. Kandidát X **přebíjí** Y, když je X specifičtější-nebo-stejný na **obou** osách zároveň.
3. Zůstanou nepřebití kandidáti: **0** = nic, **1** = výsledek, **víc** = **konflikt**.

Pořadí řádků/sloupců nehraje roli. Stejné hodnoty konflikt **neřeší**.

Typické konflikty:
- **Dva nezávislí rodiče** na jedné ose: `Fruit` je potomek `InventoryItem` i `DamagedByExplosion`, oba řádky vyplněné ve stejném sloupci.
- **Křížení**: `(Fruit, Monster)` a `(InventoryItem, SmallMonster)` pro dotaz `(Fruit, SmallMonster)` – každý je specifičtější na jiné ose.

Řeší se doplněním explicitní buňky pro konfliktní kombinaci. Pokud je jediný vítěz, dává stejný výsledek jako postupné řešení os – kombinovaný algoritmus jen nehádá u křížení.

## Editor (`KsidTableWindow`)

Otevření: dvojklik na asset, tlačítko v inspectoru, nebo `MyGame/Ksid Table Editor`.

- **Zobrazení**: explicitní hodnota normálně, zděděná šedě kurzívou (tooltip = odkud), konflikt červeně (tooltip = soupeřící buňky). Tooltip hlavičky = rodiče Ksid.
- **Výběr**: klik, tažení, Shift+klik/šipky rozšiřují. Klik na hlavičku = celý řádek/sloupec, roh = vše (Ctrl+A).
- **Editace**: Enter / F2 / dvojklik (předvyplní zděděnou hodnotu, beze změny se nic neuloží), nebo rovnou psát. Enter potvrdí a posune dolů, Tab / Shift+Tab doprava / doleva, Ctrl+Enter potvrdí bez posunu, Esc zruší. Prázdný text = null.
- **Delete** = null buňky ve výběru.
- **Ctrl+C / X / V** = TSV kompatibilní s Excelem / Google Sheets. Kopírují se jen explicitní hodnoty (null = prázdná buňka), vložení prázdné buňky = null. Jedna hodnota do většího výběru = vyplnit výběr. Neplatná hodnota zruší celé vložení.
- **Osy**: `+` na konci přidá řádek/sloupec, pravé tlačítko na hlavičce = vložit, změnit Ksid (přepíše i záznamy), posunout, odebrat (smaže i záznamy).
- **Varování** (dole): konflikty pro Ksid, které nejsou na osách (v gridu nejsou vidět), duplicitní záznamy, záznamy mimo osy (tlačítko na smazání). Jen informativní – za běhu konflikt hodí výjimku až při dotazu.
- Všechny změny jdou přes `SerializedObject` → Undo/Redo.

### Textový formát buňky

Fieldy v pořadí deklarace oddělené `;`, např. `20; 2`. Desetinná čárka i tečka. `bool` jako `1`/`0`/`true`/`false`.
`;` místo `/` proto, že Excel by `20/2` převedl na datum.

## Změna struktury buňky

- Přidání fieldu → stávající záznamy dostanou výchozí hodnotu (Unity serializace).
- Odebrání fieldu → hodnota se zahodí.
- Přejmenování → `[FormerlySerializedAs("Old")]`.
- Větší přestavba (změna typu, rozdělení fieldu) → jednorázový migrační skript.
- Text ve schránce je poziční – zkopírovaný před změnou struktury nemusí jít vložit (počet hodnot se kontroluje).

## Za běhu

- `TryGet(row, col, out value)` – false = nic; `Get(row, col)` – výjimka = nic. Konflikt hodí výjimku v obou.
- **Lazy cache**: `Dictionary<(Ksid, Ksid), int>` – index do `entries`, nebo příznak nic / konflikt. Výsledek se spočítá `KsidTableResolver`em při prvním dotazu na dvojici.
- Cache drží indexy, proto se maže v `OnValidate` (každá změna dat).
- Hustá matice `[Ksid × Ksid]` by byla rychlejší, ale s mnoha Ksid zbytečně velká. Vrátit se k ní, pokud se dotaz dostane na hot path.
- `KsidTableResolver.Resolve<T>` je generická přes `IKsidPair` (implementuje `KsidTableEntry<TCell>` i `KsidPair`) – na structech bez boxingu.
