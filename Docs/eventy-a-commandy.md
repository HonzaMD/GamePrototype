# Eventy a Commandy Herních Objektů

Herní objekty komunikují se systémem přes lifecycle eventy (volané automaticky) a commandy (volané explicitně). Všechny jsou definované jako rozhraní nebo metody na `Label` / `Placeable`.

---

## Lifecycle Objektu

```
Instantiate()         ← kopie prefabu z poolu
    └── Init(map)     ← přidání na mapu
            └── PlaceToMap()
                    ├── AutoAttachRB()
                    ├── map.Add(this)
                    ├── registrace do update systému
                    └── AfterMapPlaced()   ← setup komponent

GameUpdate() / GameFixedUpdate()           ← per-frame

Kill()
    └── RecursiveCleanup()
            └── Cleanup(goesToInventory)   ← odregistrování ze systémů
```

---

## Lifecycle Eventy

### `Init(Map map)` — inicializace

**Implementuje:** Potomek `Label` nebo `Placeable` přetížením virtuální metody.

Volána ihned po vytvoření objektu. Ve `Placeable` automaticky zavolá `PlaceToMap()`. Přepisuj pokud potřebuješ vlastní inicializaci, ale zavolej `base.Init(map)`.

Viz: `Assets/Scripts/Bases/Label.cs`, `Placeable.cs`

---

### `AfterMapPlaced(Map map, Placeable placeableSibling, bool goesFromInventory)` — po umístění na mapu

```csharp
public interface IHasAfterMapPlaced
{
    void AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory);
}
```

**Implementuje:** Kdokoliv přes `IHasAfterMapPlaced` — typicky sibling `MonoBehaviour` komponenty na stejném objektu jako `Placeable`. Lze implementovat i přímo na potomku `Placeable` přetížením virtuální metody `AfterMapPlaced(Map map, bool goesFromInventory)` (bez parametru `placeableSibling`).

Volána z `PlaceToMap()` poté, co je objekt přidán do mapy a zaregistrován do update systémů. Ideální místo pro setup závislý na mapě (vytvoření inventáře, registrace do dalších systémů apod.).

**Parametry:**
- `map` — instance mapy, na které objekt leží
- `placeableSibling` — `Placeable` komponenta, ke které tato komponenta patří
- `goesFromInventory` — `true` pokud objekt přichází z inventáře (obnova stavu), `false` při prvním umístění

**Typická použití:**
```csharp
// Chest.cs — vytvoří inventář truhly
void IHasAfterMapPlaced.AfterMapPlaced(Map.Map map, Placeable placeableSibling, bool goesFromInventory)
{
    if (inventory == null)
    {
        inventory = Game.Instance.PrefabsStore.Inventory.Create(...);
        inventory.SetupIdentity("Chest", InventoryType.Chest, ...);
    }
}

// InventorySearcher.cs — zaregistruje se do 1-sec updatů
void IHasAfterMapPlaced.AfterMapPlaced(...)
{
    this.map = map;
    Game.Instance.ActivateObject(this);  // IActiveObject1Sec
}
```

Viz: `Assets/Scripts/Bases/PlaceableSibling.cs`

---

### `Cleanup(bool goesToInventory)` — úklid před smrtí / vložením do inventáře

```csharp
public interface IHasCleanup
{
    void Cleanup(bool goesToInventory);
}
```

**Implementuje:** Kdokoliv přes `IHasCleanup` — typicky komponenty na `PlaceableSibling`. Lze přetížit i přímo na potomku `Label` / `Placeable` virtuální metodou `Cleanup()`.

Volána těsně před tím, než objekt zanikne (`Kill()`) nebo je vložen do inventáře. `Placeable.Cleanup()` odregistruje objekt z mapy a z update systémů. Komponenty implementují `IHasCleanup` pro vlastní cleanup (zrušení joinů, zabití podřízených objektů apod.).

**Parametry:**
- `goesToInventory = false` — objekt umírá, uvolni všechny zdroje
- `goesToInventory = true` — objekt jde do inventáře, zachovej stav (neprovádí plný cleanup)

**Typická použití:**
```csharp
// Chest.cs — zabije inventář, pokud objekt umírá
void IHasCleanup.Cleanup(bool goesToInventory)
{
    if (!goesToInventory)
    {
        inventory.Kill();
        inventory = null;
    }
}

// StickyBomb2.cs — deaktivuje timery a jointy
void IHasCleanup.Cleanup(bool goesToInventory)
{
    Deactivate();
}
```

Viz: `Assets/Scripts/Bases/PlaceableSibling.cs`

---

## Update Eventy

### `IActiveObject` — per-frame update

```csharp
public interface IActiveObject
{
    void GameUpdate();
    void GameFixedUpdate();
}
```

**Implementuje:** Potomek `Placeable` nebo sibling `MonoBehaviour`. `PlaceToMap()` automaticky zaregistruje jen první nalezenou komponentu (`TryGetComponent`) — pro ostatní musíš registrovat ručně. Pro více komponent s per-frame updatem použij `IActiveObject1Sec` nebo vlastní callback.

Opt-in alternativa k MonoBehaviour `Update()`. `Cleanup()` odregistruje automaticky.

**Důležité:** Pokud jsi zaregistrován jako `IActiveObject`, musíš si v `GameUpdate()` sám aktualizovat pozici v mapě při pohybu objektu.

```csharp
// Automatická registrace v PlaceToMap():
if (TryGetComponent<IActiveObject>(out var ao))
    Game.Instance.ActivateObject(ao);

// Manuální registrace (sibling MonoBehaviour komponenty):
Game.Instance.ActivateObject(this);
Game.Instance.DeactivateObject(this);
```

Viz: `Assets/Scripts/Bases/IActiveObject.cs`

---

### `IActiveObject1Sec` — update přibližně jednou za sekundu

```csharp
public interface IActiveObject1Sec
{
    void GameUpdate1Sec();
}
```

**Implementuje:** Kdokoliv — bez omezení počtu komponent. Registrace není automatická, proveď ji ručně v `AfterMapPlaced()`.

Vhodné pro operace, které nepotřebují per-frame přesnost (prostorové vyhledávání, AI rozhodování apod.).

```csharp
// Registrace:
Game.Instance.ActivateObject(this);    // v AfterMapPlaced()
Game.Instance.DeactivateObject(this);  // v Cleanup()
```

Viz: `Assets/Scripts/Bases/IActiveObject.cs`

---

## Interakční Eventy

**Implementuje obě:** Kdokoliv přes interface — typicky sibling `MonoBehaviour` komponenta.

### `ICanActivate.Activate()` — aktivace předmětu

```csharp
public interface ICanActivate
{
    void Activate();
}
```

Voláno při aktivaci předmětu (např. při hodu).

---

### `IHoldActivate.Activate(Character3 character)` — použití drženého předmětu

```csharp
public interface IHoldActivate
{
    void Activate(Character3 character);
}
```

Voláno z `Character3` při použití drženého předmětu (levé tlačítko myši ve stavu `ItemUse`). Předmět musí mít `Ksid.ActivatesInHand`.

```csharp
// Knife.cs — spustí animaci bodání
public void Activate(Character3 character)
{
    var settings = GetComponent<PlaceableSibling>().Settings;
    character.ActivateHoldAnimation(settings.ActivityAnimation, 0.55f, 2f);
}
```

Viz: `Assets/Scripts/Bases/ICanActivate.cs`

---

### `IPhysicsEvents.OnCollisionEnter(Collision collision)` — fyzikální kolize

```csharp
interface IPhysicsEvents
{
    void OnCollisionEnter(Collision collision);
}
```

**Implementuje:** Kdokoliv přes interface — komponenta na `PlaceableSibling`. `RbLabel` dostane Unity `OnCollisionEnter` a přepošle ho první komponentě implementující `IPhysicsEvents` na child objektech.

Typické využití: přichycení k objektu při dopadu (StickyBomb), damage při nárazu.

```csharp
// StickyBomb2.cs — přichytí se k objektu při kolizi
public void OnCollisionEnter(Collision collision)
{
    if (IsActive && Label.TryFind(collision.collider.transform, out var label))
    {
        int index = FindNextConnectable();
        if (index >= 0)
            AttachJoint(index, label as Placeable, collision.contacts[0].point);
    }
}
```

Viz: `Assets/Scripts/Bases/IPhysicsEvents.cs`

---

### `ILevelPlaceable` — instanciace z písmenkového levelu

```csharp
internal interface ILevelPlaceabe
{
    void Instantiate(Map map, Transform parent, Vector3 pos);
    bool SecondPhase { get; }
}
```

**Implementuje:** `Placeable` a `RbLabel` — interní rozhraní, běžně neimplementuješ sám.

Volané při načítání levelu. `SecondPhase = true` říká loaderu, aby objekt instancioval až po všech základních objektech (pro objekty s vazbami na jiné).

Viz: `Assets/Scripts/Map/ILevelPlaceabe.cs`

---

## Commandy

### `label.Kill()` — zabití objektu

Zahájí sekvenci zániku. Rekurzivně zavolá `Cleanup(false)` na celém stromu objektů, odpojí od map a update systémů, vrátí do poolu (nebo `Destroy`).

```csharp
label.Kill();

// Pokud objekt sám nemůže být zabit (CanBeKilled = false),
// Kill() eskaluje na rodičovský objekt.
```

---

### `label.AttachRigidBody(bool startMoving, bool incConnection)` — připojení RigidBody

Připojí fyzikální těleso k objektu. Pokud ještě nemá `RbLabel`, vytvoří ho.

**Parametry:**

| Parametr | `true` | `false` |
|----------|--------|---------|
| `startMoving` | RB se stane dynamickým (`isKinematic = false`), objekt se pohybuje fyzikou | RB zůstane kinematické |
| `incConnection` | Inkrementuje connection counter — RB přetrvá, dokud je objekt připojený k něčemu | Bez connection trackingu |

```csharp
// Objekt začne okamžitě padat / být ovlivněn fyzikou:
placeable.AttachRigidBody(startMoving: true, incConnection: false);

// Připojení s connection trackingem (joint, chyt):
placeable.AttachRigidBody(startMoving: true, incConnection: true);
```

Viz: `Assets/Scripts/Bases/Placeable.cs`

---

### `PrefabsStore.Xxx.Create(...)` — instanciace objektu z prefab store

Obecný způsob vytvoření libovolného objektu z poolu přes `PrefabsStore`. Objekt je získán z poolu, umístěn do scény a inicializován.

```csharp
Game.Instance.PrefabsStore.Explosion.Create(
    label.LevelGroup,          // parent Transform (stejná skupina v hierarchii)
    label.transform.position,  // pozice vytvoření objektu
    map                        // mapa — null pokud objekt mapu nepotřebuje
);
```

Pokud potřebuješ předat vlastní parametry do `Init()`, použij variantu `CreateWithoutInit()` a zavolej `Init()` ručně:

```csharp
var obj = Game.Instance.PrefabsStore.Xxx.CreateWithotInit(parent, position);
obj.Init(map, extraParam);
```

Viz: `Assets/Scripts/Utils/Extensions.cs`
