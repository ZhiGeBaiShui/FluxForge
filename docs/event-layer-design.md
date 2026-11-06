# Event Layer Design (`src/Events`)

Status: implementation target. Unity 2021.3.45f2 / Mono / Probably Stolen 0.46.

## 1. Boundary

```text
Story (future) -> Events -> Logic / Mod / Presentation
```

- Events may call Logic, Mod and Presentation.
- Logic / Mod / Presentation / Traits must not reference `FluxForge.Events`.
- Public surface is intentionally small:
  - `FlowEventApi.LockGrid(GameGridInventory)`
  - `FlowEventApi.UnlockGrid(GameGridInventory)`
  - `FlowEventApi.RunBoundaryEmitter(GameGridInventory)`
  - `FlowEventApi.RunTwoEmitterRelay(GameGridInventory)`
- Event methods return `bool`: `true` means successfully started, not completed.
- Completion is published through:
  - `public static event Action<GameGridInventory> FlowEventApi.EventCompleted`
  - fired once after cleanup, for both normal completion and failure cleanup.
- The story layer locks/unlocks manually. The event layer does not unlock a grid it did not lock.
- No `Update` / per-frame scan in this layer. Coroutine waits are time-based, not polling.

## 2. Directory

```text
src/Events/
├─ Public/
│  ├─ FlowEventApi.cs          lock/unlock + boundary event
│  └─ FlowEventApi.Relay.cs    relay event (partial facade)
├─ Runtime/
│  ├─ EventCoroutineHost.cs    hidden DontDestroyOnLoad MonoBehaviour, no Update
│  └─ EventSession.cs          item cleanup, subscriptions, completion guard
├─ Locks/
│  └─ GridLock.cs              ref-counted grid/item lock + restore
├─ Placement/
│  ├─ PlacementMath.cs         pure anchor/orientation math
│  └─ DemoPlacement.cs         DirectoryMaster item, tags, fit check, accept, re-register
└─ Demos/
   ├─ BoundaryEmitterEvent.cs  demo 1
   └─ TwoEmitterRelayEvent.cs  demo 2
```

No bootstrap/reflection: a future Story layer references this assembly directly. Root SDK csproj auto-includes `src/**`.

## 3. Locking

`GridLock.Lock(grid)`:

- ref-count by grid; first lock saves `overrideLockInsert` / `overrideLockRemove`, then `grid.LockInv()`.
- for each `grid.childItems`: save `forceDisableUse` / `forceDisableActivate`, set both `true`.
- new demo item: `GridLock.Track(item)` applies the same item flags while the grid lock is active.

Do **not** use `GeneralHelper.LockItem`: it overwrites `mayInventoryRemoveItemFunc` and is not reversible without clobbering other mods.

`Unlock` restores saved flags only on the final release. Grid lock blocks `MayRemove`; item flags block selection/activation. The event layer does not create a UI overlay, so story dialogue can still render above the grid.

## 4. Placement

`DemoPlacement.Spawn` takes:

```csharp
Spawn(
    GameGridInventory grid,
    string itemKey,
    int anchorCellX,
    int anchorCellY,
    int orientation,
    bool flipped,
    int waterMicrolitres,
    out string error)
```

Steps:

1. `DirectoryMaster.Item(itemKey)`.
2. If water > 0: `FlowStock.Write(item, props.Set(FlowProps.Water, water))`.
3. Read anchor local tags (default `0,0`) and compute the transformed anchor offset with a zero-origin probe.
4. Set `item.modifiedShape` so `ForwardTransform(anchorLocal)` lands on the requested cell.
5. `grid.CheckShapePlacement(item, item.modifiedShape, new List<GameItem>())`; fail cleanly if false (covers other mods changing grid size).
6. `grid.UncheckedAccept(item)`.
7. Because `ValidateShapeState` recomputes the shape, restore the desired `modifiedShape` afterwards, call `item.Validate()`, re-register with `runtime.Grid.Register(item)`, and `DeviceRegistry.SetFacing(...)`.
8. `GridLock.Track(item)`.

Any failure destroys the item and returns `null`; no partial item remains.

## 5. Events

### Boundary emitter

- Spawn `flow_emitter` at the left edge, orientation `3` (right), water 1,000,000 µL.
- Subscribe `FlowSpawner.OnEntityAdded` / `OnEntityRemoved` **before** `runtime.Emit(item)`.
- Capture the emitted entity; when that entity is removed, `session.Complete()`.
- If `Emit` returns false, fail and clean up.
- Completion is event-driven; no Update.

### Two-emitter relay

- Spawn left emitter orientation `3` (right) at left edge, water filled.
- Spawn right emitter orientation `1` (left) at right edge, empty.
- Subscribe left `OnEntityRemoved` before left emit.
- When left entity is removed:
  - if `FlowStock.Total(rightItem) <= 0`, the flow missed the receiver: fail.
  - otherwise start a 2-second coroutine, then subscribe right `OnEntityRemoved`, call `runtime.Emit(rightItem)`, capture the right entity, and complete when it is removed.
- Two seconds is `WaitForSeconds`, not a per-frame scan.

## 6. Cleanup / failure

`EventSession`:

- exactly one active session per grid.
- tracks demo items, event subscriptions, and an optional coroutine.
- `Complete` / `Fail` clean up once: unsubscribe, stop coroutine, destroy items.
- `Fail` logs one warning; never throws into the story layer.
- placement failure is a boundary condition; missing `FlowRuntime` and invalid item definitions are developer invariants.

## 7. Tests

- `PlacementMath` is pure; test anchor/orientation/flip/min calculation in `tests/FluxForge.Tests/Events/PlacementMathTests.cs`.
- Test csproj can include `src/Events/Placement/PlacementMath.cs` without game/Unity dependencies.
- Locking/placement/event runtime need real-game smoke tests: place on a normal grid, grid resized by another mod, and a locked grid while clicking/dragging.
