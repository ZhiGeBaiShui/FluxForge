# Event Layer Layout (`src/Events`)

> Superseded implementation shape: `docs/event-layer-design.md`. This document remains as the directory-ownership proposal; the final layer is intentionally smaller (no reflection bootstrap, no 25-file split).

Status: proposal. Target: netstandard2.1 / C# 10 / nullable disable (root csproj).

## 1. Binding rules

- Dependency arrow: `Events -> Logic | Mod | Presentation`. Never reverse.
  Gate: `grep -rn "FluxForge.Events" src/Logic src/Mod src/Presentation src/Traits` must stay empty.
  Mod activates the layer by reflection only: `Type.GetType("FluxForge.Events.EventLayerBootstrap, FluxForge")`.
- Public surface = `FlowEventApi` (3 members) + `EventIds` + `EventRunResult`. Everything else `internal`.
- No per-frame work: no `Update`/`LateUpdate`/scan loop in this layer. React to `FlowSpawner` C# events and
  sequence with coroutines on `CoroutineHost` (zero `Update` methods).
- One run per grid at a time; locks are ref-counted per owner, released on completion/teardown.
- No new csproj. Root `FluxForge.csproj` is SDK-style and auto-includes `src/**` (`<Compile Remove="tests/**/*.cs" />`).

## 2. Exact tree

```
src/Events/
├─ Public/                        namespace FluxForge.Events — the only public surface
│  ├─ FlowEventApi.cs             PUBLIC  static facade: Run(eventId, grid), LockGrid(grid, owner), UnlockGrid(grid, owner)
│  ├─ EventIds.cs                 PUBLIC  const ids: BoundaryEmitter, TwoEmitterRelay (demo discoverability only)
│  └─ EventRunResult.cs           PUBLIC  enum: Started, UnknownEvent, GridLocked, AlreadyRunning, Unavailable
├─ Runtime/
│  ├─ IEventBehavior.cs           internal  contract per event: Start(ctx), OnSpawnerBurst(ctx), Dispose()
│  ├─ EventDefinition.cs          internal  immutable id/displayName/factory/lock requirement
│  ├─ EventCatalog.cs             internal  static id→definition map; single demo registration point
│  ├─ EventFactory.cs             internal  validates grid + placement, builds EventRuntime
│  ├─ EventRuntime.cs             internal  per-run state machine, cancellation, completion + unlock
│  ├─ EventContext.cs             internal  per-run context: grid handle, geometry, RNG, scheduler, done callback
│  ├─ CoroutineHost.cs            internal  hidden DontDestroyOnLoad MonoBehaviour; owns StartCoroutine/StopAll
│  ├─ EventScheduler.cs           internal  delay/repeat helpers over CoroutineHost; callers never poll time
│  └─ SpawnerEventBridge.cs       internal  subscribes/unsubscribes FlowSpawner events; forwards bursts to runtimes
├─ Locks/
│  ├─ GridLockRegistry.cs         internal  ref-counted locks keyed by opaque grid handle + owner (pure C#, no Unity)
│  └─ GridLockScope.cs            internal  IDisposable token; Dispose = one release; guards double-unlock
├─ Placement/
│  ├─ PlacementMath.cs            internal  pure int math: rotate/offset/facing/boundary cells; no Unity types
│  ├─ GridGeometry.cs             internal  readonly struct: width/height/origin, cell mapping
│  ├─ BoundaryProbe.cs            internal  enumerates edge/corner candidates, asks adapter for occupancy
│  ├─ PlacementResolver.cs        internal  first valid footprint+facing; returns PlacementResult
│  └─ PlacementResult.cs          internal  readonly struct: ok, cells, facing, reject reason
├─ Demos/
│  ├─ BoundaryEmitter/
│  │  ├─ BoundaryEmitterEvent.cs      internal  demo 1 behavior: emitter on outer boundary, one emit per spawner burst
│  │  └─ BoundaryEmitterPlacement.cs  internal  demo 1 rule: 1x1 footprint must touch grid edge
│  └─ TwoEmitterRelay/
│     ├─ TwoEmitterRelayEvent.cs      internal  demo 2 behavior: two emitters face each other; second emits on first's spawn event
│     └─ RelaySpawnSchedule.cs        internal  demo 2 timing: spawner event → delayed relay emit, coroutine based
└─ Bootstrap/
   ├─ EventLayerBootstrap.cs     internal  idempotent Initialize(): host, catalog, lock reset; reflection entry point
   └─ EventDiagnostics.cs        internal  run counters + one-line logs; no per-frame logging
```

## 3. Demo event anatomy (template)

1. `<Name>Event.cs` = `EventDefinition` + `IEventBehavior` implementation in one file.
2. `<Name>Placement.cs` only when placement rules are custom (demo 1 has one; demo 2 reuses `PlacementMath`).
3. Register: one append-only line in `EventCatalog`; add an id const in `EventIds` only for these two demos.
4. No prefabs/scenes/assets: demos act through existing grid adapter + `FlowSpawner` only.

## 4. File ownership lanes (2 programmers)

- **Lane A — Runtime & Integration:** `Public/`, `Runtime/`, `Bootstrap/` (14 files) + activation snippet for the Mod lane
  + this doc. Owns `EventCatalog` structure.
- **Lane B — Placement, Locks & Demos:** `Placement/`, `Locks/`, `Demos/` (11 files). Owns append-only demo registrations
  in `EventCatalog`.
- **Frozen contracts** (both sign off, never edited inside a feature PR): `FlowEventApi.cs`, `EventIds.cs`,
  `EventRunResult.cs`, `PlacementResult.cs`, `GridLockScope.cs`, `IEventBehavior.cs`.
- Order: contracts first (Lane A), then PlacementMath/PlacementResult + lock skeleton (Lane B) so both sides compile
  independently; parallel after that; integration and reflection smoke by Lane A last.
- Hard rule: no PR touches files outside its lane; cross-lane changes need the frozen-contract path.

## 5. Tests

Location: `tests/FluxForge.Tests/Events/` (namespace `FluxForge.Tests.Events`).

| File | Stubs needed |
| --- | --- |
| `PlacementMathTests.cs`, `GridGeometryTests.cs` | none (pure) |
| `GridLockRegistryTests.cs` | none (opaque grid handle) |
| `BoundaryProbeTests.cs`, `PlacementResolverTests.cs` | occupancy/grid stub in `tests/.../Stubs/EventGridStubs.cs` |
| `EventCatalogTests.cs` | ids resolve, demo definitions construct (exclude behaviors from assertions) |

csproj: the test project compiles sources explicitly, so append to the existing `ItemGroup`:

```xml
<Compile Include="..\..\src\Events\Placement\**\*.cs" />
<Compile Include="..\..\src\Events\Locks\**\*.cs" />
<Compile Include="..\..\src\Events\Public\EventIds.cs" />
<Compile Include="..\..\src\Events\Public\EventRunResult.cs" />
```

Do **not** include `Runtime/`, `Demos/`, `Bootstrap/`, or `Public/FlowEventApi.cs` (Unity coroutines / FlowSpawner / FlowGrid
are unloadable in the test host). If a runtime rule needs testing, extract it as pure logic into `Placement/` instead of
adding Unity stubs. Stubs live only in `tests/FluxForge.Tests/Stubs/`, never in `src/Events`. Test file lists are manual
(existing convention); main project needs no edit.

## 6. Naming conventions

- One type per file; file name = type name in PascalCase. Demo files prefixed with the event name (`BoundaryEmitter*`).
- Folders organize only; namespace stays flat `FluxForge.Events` (matches repo: `src/Mod/**` is all `FluxForge.Mod`).
- Suffixes: `*Event` definition+behavior, `*Placement` rules, `*Registry` stateful collection, `*Scope` IDisposable,
  `*Result`/`*State`/`*Geometry` readonly structs, `*Bridge`/`*Adapter` game-facing glue, `*Diagnostics` logging only.
- Members: PascalCase methods/properties, `_camelCase` private fields, `s_` statics, `I` interfaces, coroutines as
  `IEnumerator <Verb>Routine()`.
- Public API members require XML docs; internal types require a one-line `//` purpose comment.
- No region blocks, no `#if DEBUG` in runtime paths; logging goes through `EventDiagnostics`.
