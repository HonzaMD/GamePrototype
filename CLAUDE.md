# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A 2.5D physics-based action/sandbox game prototype built with Unity (HDRP 17.0.3). Code comments and some variable names are in Czech.

## Development

This is a Unity project — there are no CLI build/test commands. All development is done through the Unity Editor. Tests use Unity's NUnit-based Test Runner (Window > General > Test Runner).

Tests are in `Assets/Tests/` and can be run as Edit Mode tests directly against the static physics classes without needing Play Mode.

## Architecture

### Central Singleton: `Game.cs`

`Game.cs` is the root of everything. It holds references to all major subsystems:
- `Game.Instance.InputController` — player input and camera
- `Game.Instance.MapWorlds` — multi-world/level management (6 concurrent worlds)
- `Game.Instance.MapWorlds.SelectedMap` — the active `Map` instance
- `Game.Instance.Inventory` — the active character's inventory
- `Game.Instance.StaticPhysics` — threaded physics solver
- `Game.Instance.Ksids` — the type system

`Game.cs` also owns the main update loop, calling `GameUpdate`/`GameFixedUpdate` on all registered `IActiveObject` instances rather than relying on Unity's per-component `Update()`.

### Type System: `Ksid` / `KsidDependencies`

All game objects are categorized by `Ksid` (Kind System ID) enum values. `KsidDependencies` defines a hierarchy between types (e.g., `Rope` is a subtype of `Catch`). Use `label.KsidGet.IsChildOf(Ksid.X)` / `IsChildOfOrEq` for runtime type checks — prefer this over `is`/`GetComponent` casting for game-object interactions.

### Object Hierarchy

- **`Label`** — abstract base for all game objects; handles cleanup, colliders, mass
- **`Placeable : Label`** — objects that live on the map grid (position, cell blocking, rigidbody)
- **`Connectable`** — MonoBehaviour component attached to a `Placeable`; represents one attachment slot (Physics, LegArm, MassTransfer, StickyBomb, OwnedByInventory)

### Map & Cell System (`Assets/Scripts/Map/`)

The world is divided into 0.5×0.5×0.5m cells. `Map` provides spatial queries and object placement. `MapWorlds` manages loading/unloading of multiple simultaneous world scenes. The visibility/shadow system (`Map/Visibility/`) operates on a cell viewport and runs per-frame via `VCore`.

### Character System (`Assets/Scripts/Stuff/Character3.cs`)

`Character3` is a 13-state machine controlling the player. States cover: `EmptyHands`, `PickupPrepare`, `Pickup`, `TryHold`, `ItemAdjust`, `Throw`, `ThrowReload`, `ItemUse`, `ItemAnimation`, etc. The `ICanActivate` interface on held items is called when the player uses them (e.g., `Knife` triggers its stab animation this way).

### Static Physics (`Assets/Scripts/Core/StaticPhysics/`)

Runs on a background thread. `SpInterface` exposes a command-queue API (`InputCommand`/`OutputCommand`). `GraphWorker` solves constraints and detects joint breaks. Interact only through `SpInterface` — never touch `SpDataManager` or workers directly from the main thread.

### Inventory (`Assets/Scripts/Core/Inventory/`)

`Inventory` manages slots with mass tracking and quick-access binding. `InventoryVisualizer` is built with UIElements. `KeysToInventory` maps keyboard input to slots. Multiple inventories can be linked for automatic mass balancing.

### Update Flow

```
Game.Update()
├── InputController.GameUpdate()
├── UpdateTriggers()
├── UpdateMovingObjects()
├── UpdateObjects()       ← all IActiveObject instances
└── Timer.GameUpdate()

Game.FixedUpdate()
├── InputController.GameFixedUpdate()
├── IActiveObject.GameFixedUpdate()
└── StaticPhysics (background thread, synchronized via queues)
```

### Key Interfaces

| Interface | Purpose |
|-----------|---------|
| `IActiveObject` | Opt-in to `GameUpdate`/`GameFixedUpdate` calls from `Game` |
| `ICanActivate` | Called when player activates a held item |
| `ISimpleTimerConsumer` | Callback from the unified `Timer` system |

### Utilities

- `ObjectPool` / `ConnectablesPool` — pooling; prefer these over `Instantiate`/`Destroy` for frequent objects
- `Timer` / `GlobalTimerHandler` — use instead of coroutines for delayed/recurring callbacks
- `PrefabsStore` — factory for creating prefab instances

## Conventions

Detailed patterns and conventions (no-alloc rules, KSID usage, timer system, RigidBody states, Connectables, etc.) are documented in [`Docs/conventions.md`](Docs/conventions.md).
