# Event Layer Smoke Test

Use a dedicated empty `GameGridInventory` with at least 18×4 cells; do not run on the player's live
counter while testing.

## 1. Lock

```csharp
FlowEventApi.LockGrid(grid);
```

Expected: no item can be dragged, selected, activated, inserted, or removed. Window chrome is not
covered by this layer; close story UI separately if needed.

## 2. Boundary emitter

```csharp
bool started = FlowEventApi.RunBoundaryEmitter(grid);
```

Expected:

- one `flow_emitter` appears at the far-left, facing right;
- the item contains water and emits one flow;
- the flow crosses the grid and disappears at the right boundary;
- the emitter is destroyed after the flow removal;
- no `OnEntityRemoved` polling loop is needed.

## 3. Two-emitter relay

```csharp
bool started = FlowEventApi.RunTwoEmitterRelay(grid);
```

Expected:

- left emitter faces right, right emitter faces left;
- left flow is absorbed by the right emitter;
- after approximately two seconds, the right emitter emits its stored flow;
- that flow leaves through the left boundary;
- both emitters are destroyed.

## 4. Unlock

```csharp
FlowEventApi.UnlockGrid(grid);
```

Expected: grid insert/remove flags and item `forceDisableUse` / `forceDisableActivate` return to their
pre-lock values. The test trait is not part of this layer.

## Failure cases

- grid width < 2: placement fails, no items remain;
- another mod changed the grid shape: `CheckShapePlacement` fails cleanly;
- receiver is absent or blocked: event 2 fails and cleans both emitters;
- grid runtime missing: event returns `false`.
