# Event Layer Defensive Review

Scope: `src/Events`, plus `PlacementMathTests`. Reviewed against the project convention:
validate at boundaries; trust internal invariants; do not wrap every call in defensive code.

## 1. Boundary checks

| Location | Check | Verdict | Why |
|---|---|---|---|
| `FlowEventApi.LockGrid` / `UnlockGrid` | null grid | KEEP | Public boundary from story. |
| `GridLock.Lock` / `Unlock` | null grid | KEEP | Public boundary; no state to touch otherwise. |
| `GridLock.Track` | item/grid null | KEEP | Item may have been destroyed between placement and track. |
| `DemoPlacement.Spawn` | null item/shape | THROW | Invalid JSON/item definition is a developer error. |
| `DemoPlacement.Spawn` | `CheckShapePlacement` | KEEP | Required because other mods can resize the grid; `UncheckedAccept` deliberately skips fit. |
| `DemoPlacement.Spawn` | `UncheckedAccept` false | THROW | Prior fit check makes this an invariant violation. |
| `BoundaryEmitterEvent` | `emitter == null` | KEEP | Represents a rejected placement, not a missing JSON item. |
| `BoundaryEmitterEvent` | `reference == emitted` | KEEP | Other flows can be born/die on the same grid. |
| `TwoEmitterRelayEvent` | `left == null` / `right == null` | KEEP | Same placement boundary. |
| `TwoEmitterRelayEvent` | `leftRemoved` + entity reference | KEEP | Prevents double completion and unrelated flow removal. |
| `TwoEmitterRelayEvent` | `Received` | KEEP | Receiver may miss the flow; this is the event's branching condition. |
| `EventSession.RunRoutine` | null/ended | KEEP | Coroutine may finish after a synchronous failure. |
| `EventSession.AddCleanup` | null/ended | KEEP | Allows cleanup registration after an early failure. |
| `EventSession.End` | duplicate guard | KEEP | Callback may complete twice (removed + cleanup). |

## 2. MUST-FIX

### 2.1 Emit failure is a developer error, not a runtime branch

`BoundaryEmitterEvent` and `TwoEmitterRelayEvent` are authored demos: the grid is expected to be
empty and the emitter is placed/filled by this code. `runtime.Emit` returning false means the event
definition itself is wrong. Do not add `started`/`emitting` guards or `try/finally` just to recover
from that. If emit failure ever becomes a supported runtime state, handle it explicitly at that time.

### 2.2 `EventSession.End` must not let one destroy failure strand later items

Cleanup callbacks are individually guarded, but `item.Destroy()` is not. If one item's UI destroy
throws, remaining demo items leak. Fix: guard each `Destroy` and continue.

### 2.3 Relay must abort after grid death

The 2-second relay coroutine can outlive the grid. After the wait, check `session.IsFinished` and
`runtime.IsAlive`; fail without touching a dead grid.

## 3. Acceptable for this demo

- `GridLock` restores only flags it saved; another mod changing the same flags while locked can
  overwrite them. There is no safe way to distinguish ownership without a game-wide lock API.
- No public cancel/force-unlock: story owns the lock/unlock lifecycle, per requirement.
- A single active event per grid is intentional; `EventSession.Begin` returns null otherwise.
- `EventSession` cleanup uses a small `try/catch` around subscriber callbacks because cleanup must
  continue for unrelated resources; this is not a hot path.
- `PlacementMath` is pure and tested; no need to stub Unity to test it.

## 4. Not necessary / avoid adding

- Per-frame `Update`, `WaitUntil` polling, or checking `Flows.Active` every frame.
- A 25-file runtime/catalog/reflection bootstrap for two demos.
- `GeneralHelper.LockItem` / `GeneralHelper.UnlockItem`: not reversible without clobbering delegates.
- Speculative try/catch around `DirectoryMaster.Item`, `FlowStock.Write`, `GridRegistry.Get`, or
  `FlowSpawner` subscriptions. These have explicit contracts; only the public event boundary and
  cleanup need guards.
