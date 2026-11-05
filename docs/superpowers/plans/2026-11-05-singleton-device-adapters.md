# Singleton Device Adapters and Create/Load Migration Plan

> **Status:** Implemented 2026-11-05. Build 0 warnings / errors; tests 290/290. Historically retained as the migration plan.

**Goal:** Move device construction and tag↔member mapping into stateless singleton adapters, using `GameItem` plus a static tag utility, without re-creating existing devices.

**Architecture:** `DeviceAdapterCatalog` owns one adapter singleton per device key. `ItemDeviceBinding` asks the catalog which keys an item has; `DeviceHost.Create` calls each adapter's `Create(GameItem)` once. `DeviceHost` stores `(Key, Adapter, Device)` bindings. Later tag changes call `Load(GameItem, device)` on the existing device. Dirty flush calls `Save(GameItem, device)`. `ITagStore`, `GameItemTagStore`, and `FakeTagStore` are deleted.

**Tech Stack:** C# / Unity 2021.3.45f2 / Mono / Harmony / xUnit test project.

**Spec:** `docs/device-state-adapter-singleton-design.md`

## Global Constraints

- Never re-create an existing device during `Load`, rebind, reconcile, or emitter reload.
- No defensive try/catch or impossible-state guards.
- Preserve one dirty flush per frame and `Save → Unregister → Expel` order.
- Keep the game's `TagState` type; remove only the Mod tag-store abstraction.
- Build and tests must pass at each completed task.

## Review Focus

1. Device identity used by `DeviceRegistry`, audio, and animation must remain the original instance.
2. `DeviceHost` must pair each `(Key, Adapter, Device)` explicitly; no key re-resolution.
3. `Create` and `Load` must not duplicate field mapping.
4. Test compilation after deleting the game-free tag store.
5. Multiple devices per item must retain deterministic Router/Reactor order.

## 0. Resolved Decisions

- D1: Use a minimal test-side `GameItem` / `TagSystem` / `TagState` stub.
- D2: Superseded: `[DeviceAdapter(key)]`; device type comes from the adapter generic.
- D3: Add `CollectorDevice(FlowProperties)` + `ReplaceStock`.
- D4: Remove parameterless constructors from IceBlock/Temperature/Collector after cutover.
- D5: Skip `LoadAll` when `host.IsDirty` during rebind.
- D6: Superseded: `TagFields.Int/Double` takes explicit getter/setter delegates (no `Expression.Compile`).
- D7: Delete Mod tag-store wrappers; keep game `TagState`.
- D8: Accept one extra Load after initial Create/reconcile.
- D9: Keep explicit tag reads in `CreateDevice`; field table only drives Load/Save.

## Task 1: Add GameItemTags

**Files:**
- Create: `src/Mod/TagMapping/Core/GameItemTags.cs`
- Modify: `src/Mod/TagMapping/Core/FlowStock.cs`
- Modify: `src/Mod/DeviceState/Sync/DeviceStateSync.cs`
- Modify: `src/Mod/DeviceServices/Core/DeviceHost.cs`

**Interfaces:**
- Produces `GameItemTags.ReadInt/ReadDouble/WriteInt/WriteDouble(GameItem, string, ...)`.
- Preserves read `modifiedState`, write `state`, equal/zero no-op semantics.

- [ ] Add the utility by moving the bodies from `GameItemTagStore`.
- [ ] Make `FlowStock`, `DeviceStateSync`, and `DeviceHost` use only the utility.
- [ ] Keep `GameItemTagStore` temporarily delegating to it.
- [ ] Run build and tests; expected green.

## Task 2: Add State Constructors

**Files:**
- Modify: `src/Logic/Devices/Reaction/IceBlock/IceBlockDevice.cs`
- Modify: `src/Logic/Devices/Reaction/Temperature/TemperatureDevice.cs`
- Modify: `src/Logic/Devices/Routing/Collector/CollectorDevice.cs`

**Interfaces:**
- `IceBlockDevice(double water, double temperature)` already exists.
- Add `TemperatureDevice(double temperature)`.
- Add `CollectorDevice(FlowProperties stock)` and `ReplaceStock(FlowProperties)`.

- [ ] Add constructors and methods; keep parameterless constructors until cutover.
- [ ] Add pure Logic tests for constructor state.
- [ ] Run build and tests; expected green.

## Task 3: Atomic Adapter Cutover

**Files:**
- Modify: `src/Mod/DeviceState/Contracts/DeviceStateContracts.cs` (rename to `DeviceStateAdapter.cs`)
- Modify: all `src/Mod/DeviceState/Adapters/*.cs` (8 adapters)
- Modify: `src/Mod/DeviceState/Catalog/DeviceAdapterCatalog.cs`
- Create: `src/Mod/DeviceState/Catalog/DeviceDefinition.cs`
- Modify: `src/Mod/DeviceServices/Core/DeviceHost.cs`
- Modify: `src/Mod/DeviceServices/Core/ItemDeviceBinding.cs`
- Modify: `src/Mod/DeviceServices/Core/DeviceRegistry.cs`
- Modify: `src/Mod/DeviceState/Sync/DeviceStateSync.cs`
- Modify: `src/Mod/DeviceState/Sync/DeviceStateReconcilePatch.cs`
- Modify: `src/Mod/GridBuilding/Services/FlowRuntime.cs`
- Delete: `src/Mod/DeviceState/Contracts/ITagStore` declarations
- Delete: `src/Mod/TagMapping/Core/GameItemTagStore.cs`
- Delete: test `FakeTagStore`
- Modify: `tests/FluxForge.Tests/FluxForge.Tests.csproj`
- Modify: `tests/FluxForge.Tests/DeviceCatalogTests.cs`
- Modify: `tests/FluxForge.Tests/DeviceStateAdapterTests.cs`

**Interfaces:**
- Catalog: `Keys`, `TryGet`, `Get`, `FindFor`, `DeviceDefinition`.
- Adapter: `Create(GameItem)`, `Load(GameItem, object)`, `Save(GameItem, object)`.
- Host: `Create(GameItem, List<DeviceDefinition>)`, `Bindings`, `LoadAll`, `SaveAll`.
- Binding: `(Key, Adapter, Device)`.

- [ ] Rewrite contract and all adapters as stateless singletons.
- [ ] Add shared `ReadState` per stateful adapter; `Create` and `Load` consume it.
- [ ] Rewrite catalog discovery; instantiate one adapter per key; validate type and no instance fields.
- [ ] Rewrite host/binding/registry/sync.
- [ ] Remove `Load` from birth; `Create` fully initializes; `Bind` calls `LoadAll` only on existing host.
- [ ] Delete `ITagStore`, `GameItemTagStore`, `FakeTagStore`.
- [ ] Update/remove adapter tests according to D1.
- [ ] Run build and tests; expected green.

## Task 4: Documentation

**Files:**
- Modify: `README.md`
- Modify: `src/Mod/README.md`
- Modify: `src/Logic/Devices/README.md`
- Modify: `docs/device-state-architecture.md`
- Modify: `docs/review-pending-fixes.md`

- [ ] Document key → adapter singleton → device.
- [ ] Document Create vs Load lifecycle.
- [ ] Document deletion of tag-store abstraction and test strategy.
- [ ] Run build and tests; expected green.
