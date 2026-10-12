# Repository review, 2026-10-08

This review reconciles current guides with the working implementation and identifies performance
risks across the simulation, engine adapter, presentation, API, telemetry, builds and tooling.
It reviews the existing uncommitted extraction/refactor. Assets and vendored libraries are checked
through references and compilation, without editing their contents. Static findings describe
code paths; only the coolant discovery reproduction below is a fresh performance measurement.
Offline checks cannot certify native frame time, rendering, multiplayer or external provider behavior.

| Looking for | Go to |
| --- | --- |
| Current configuration and ownership | [configuration.md](configuration.md), [aerodynamics.md](aerodynamics.md) |
| API signatures and callback units | [api.md](api.md) |
| Previously measured optimization candidates | [redesign.md](redesign.md) |
| Reproduction and allocation results | [Review receipt](../tests/benchmarks/repository-review-2026-10-08.txt) |

## Findings in priority order

### Open coolant chains are retraced quadratically

**P1, reproduced.** [CoolantLoopBuilder.FindLoops](../Thermodynamics/Core/Loops/CoolantLoopBuilder.cs)
marks pipes claimed only after discovering a valid ring. Every pipe on an open straight chain
therefore starts another `TraceRing`, traversing part of the same chain and allocating port lists,
a ring list and a visited set. Across n pipes this is O(n²) traversal and allocation. An intact
closed ring is claimed once; these are different complexity cases.

| Pipes in one open chain | Fastest discovery, ms | Allocated bytes |
| ---: | ---: | ---: |
| 128 | 4.291 | 3,185,080 |
| 256 | 15.336 | 12,657,752 |
| 512 | 58.945 | 50,289,752 |
| 1,024 | 111.829 | 200,040,936 |
| 2,048 | 452.885 | 796,380,176 |

These are three warmed repetitions on synthetic chains, with zero loops found at every size.
Timing overlaps the running suite and is not a native-session hitch measurement. The near-4×
allocation for each doubling confirms the scaling independently of timing noise. The source and
procedure are in the receipt. `ThermalSimulation.Update` calls discovery on topology changes;
breaking a large ring, welding an unfinished run or editing another block can trigger it.
Diagnostics additionally retrace unclaimed pipes.

Cache component discovery outcomes within a rebuild and reuse port data. Failed traversal caching
must preserve port direction and branch diagnostics; add a long broken-ring/open-chain scaling
regression before replacing the algorithm. This is separate from the linear steady-state coolant
solve described in the older redesign survey.

### Room-map startup bypasses its cell budget

**P1, static.** [RoomMapper.Step](../Thermodynamics/Core/Surfaces/RoomMapper.cs) calls `BeginPass`
before applying its cell budget. Initialization resets the visited bitset, collects doors and
allocates/clears the full bounding-volume sealing snapshot, then copies structural surfaces.
The frontier walk is sliced, but initialization is not. Sparse grids with large extents can
allocate much more than their block population suggests; buffers retain their high-water size.
`CellBitset.Reset` also clears its retained word array on reuse. A small mapping slice therefore
does not bound startup latency or retained memory.

Budget initialization independently and validate bounding-volume allocation limits. A long or
sparse grid fixture should measure the first `Step(1)` separately from later flood-fill slices.
Change-local remapping remains the larger design candidate already recorded in the backlog.

### Heat-source work is counted but executed in one slice

**P2, static; existing optimization candidate.**
[AdvanceEnvironment](../Thermodynamics/Core/Simulation/ThermalSolverStep.cs) finishes its node
slice and then calls `AccumulateHeatSources` for all sources and nodes before returning, even
if the slice consumed the available budget. This is O(sources × nodes) per substep and may exceed
a frame's intended share by the whole source pass. Counting the work afterward does not bound
latency. It repeats temperature-independent source weighting every substep.

Track source/node cursors or fold source watts into the precomputed row while preserving sum
order and per-source diagnostics. The prior heat-source lab remains dated evidence; it is not
re-run here and its old percentages should not be presented as this tree's current costs.

### Coupled work is undercounted and cannot yield

**P2, static.** [AdvanceCoupled](../Thermodynamics/Core/Simulation/ThermalSolverStep.cs) executes
all coolant, room-air and heat-pump work without a budget parameter. Its charge is the number
of loops, rooms and pumps, while
[ThermalSolverCoupling](../Thermodynamics/Core/Simulation/ThermalSolverCoupling.cs) visits loop
pipes/contact links and room surface links. One room or one loop can have thousands of links.
With diagnostics on, room-air accumulation also clears a diagnostic field on every node every
substep. `ApplyCoupledWatts` and substep-start device work add more unsliced operations.

Account for actual links/pipes and introduce resumable coupled stages if measurements justify
it. Verify conservation and existing arithmetic order when splitting an exchange. The current
work-credit system is a pacing heuristic, not a hard bound on work or milliseconds per call.

### The new heating provider weakens the parallel-solve boundary

**P2, static; opt-in.** [ThermalGridScheduler](../Thermodynamics/Game/ThermalGridScheduler.cs)
can solve grids concurrently when `ParallelGrids` is enabled. The solver now invokes an external
heating delegate for each exposed node each substep. A stateful or allocating provider multiplies
its cost by this call count and must tolerate concurrent calls. The
[SurfaceHeating exception handler](../Thermodynamics/ThermalApiAerodynamics.cs) logs through
telemetry, clears shared registration and sends a mod message inline from the solver's thread.
That bypasses `SolveTick`'s normal deferred exception reporting on the game thread. A failing
`SurfaceHeatingGate` remains registered and can log again at subsequent environment samples.

Keep parallel execution opt-in. Defer provider-failure notification/unregistration to the game
thread, capture provider delegates consistently for a step, and validate callback concurrency
and fault recovery in a native session. An exception guard alone provides no synchronization.
The API guide now makes these callback and thread limits explicit.

### Geometry export can become a rebuild allocation hotspot

**P2, static; consumer-dependent.**
[GridGeometrySnapshot.Copy](../Thermodynamics/Core/Model/GridGeometrySnapshot.cs) allocates fresh
block-bound, mass, offset, face-weight and occupied-cell arrays on every call. The cell list grows
without an initial capacity and is then copied by `ToArray`. The getter is O(blocks + occupied
cells); it has no internal cache. Mass sweeps invalidate geometry after changed inventory masses,
so even an appropriately revision-driven consumer can request repeated large copies while cargo
changes. Calling the getter every frame worsens this independently of solver performance.

Consumers should poll the cheap revision, cache detached data and coalesce requests. Measure
inventory-changing grids before designing a delta API or imposing a refresh cadence. Any cached
snapshot must preserve the documented caller ownership of every returned array.

### Full-grid preparation remains outside solver slicing

**P2, static; known design tradeoff.**
[ThermalSimulation.Update](../Thermodynamics/Core/Simulation/ThermalSimulation.cs) rebuilds loops,
heat pumps and solver preparation synchronously after topology changes. Starting a step calls
`RequiredSubsteps`, including the full `SyncNodeState` temperature mirror walk and the stability
estimate, before `AdvanceStep` consumes its slice. A fleet beginning steps together or editing a
large grid can therefore spike even when conduction/environment stages yield correctly.

Prioritize the reproduced discovery issue and startup bounds before the wider temperature-
ownership redesign. Previous exposed-index and mirror-walk studies remain useful candidates,
but their old timing shares are not fresh measurements of the extracted production heating path.

### Thermal-vision prepasses sit outside the preparation budget

**P2, static; experimental display mode.**
[DrawIndependentFleet](../Thermodynamics/Game/ThermalVision/ThermalVisionIndependentFleet.cs)
walks all live grids, sorts visible views and culls hidden grids before starting its 2 ms
preparation clock. Drawing then collects visible regions through the region tree before
submitting their patches. The preparation timer and billboard count limit therefore do not
bound discovery, sorting or visibility work, nor can they preempt a single enumerator advance.
Crowded scenes or many regions may spend substantial CPU even if submission is capped.

Use the existing separate discovery/occlusion/preparation telemetry in native crowded-scene
validation. Consider independent visibility budgets or reusable visibility results only after
that capture identifies the dominant stage. Synthetic geometry benchmarks exclude native
billboard/GPU cost and do not establish FPS. This remains part of thermal vision's readiness gap.

## Documentation reconciled

The thermal model and telemetry now describe provider-returned heat rather than the removed
built-in cubic friction law or an inferred applied force. API guidance identifies forwarded
force/profile calls, raw geometry copy costs, missing-provider defaults, actual exception behavior
and unsynchronized live access. Configuration no longer lists the removed Aerodynamics settings
folder and assigns its debug display to the external mod. The intent page assigns atmospheric
force to Aerodynamics. The development model-suffix table and root-relative dump command are fixed.
Balance and test-lane measurements explicitly identify their historical fixture/date scope.
The redesign survey distinguishes linear coolant integration from quadratic open-chain discovery.

Historical evidence and archived decisions are retained. Passing link/name/default checks verifies
structure and some contracts; it cannot prove every prose claim matches runtime behavior. Native
and population claims remain limited to their stated measurements. This review does not certify
another repository or change simulation behavior.

## Validation

The local compile-only solution build passes with zero errors and four NU1900 warnings from
unavailable NuGet vulnerability data. Packaging is disabled, and no mod is deployed. Validation
uses SDK 10.0.112 with the existing net9.0 offline targets and cached packages. The sandbox's
read-only default CLI home was handled with a temporary CLI home; systemd user-bus access required
running the capped test command outside that sandbox.

The thermal lane passes **2,505 cases**, with zero failures or skips, in 3 m 3 s.
A final combined documentation/integration run passes **187 cases**, with zero failures or skips,
in 7 s; this checks the finished guide edits and the explicit local integration lane.
TRX artifacts are under `out/repository-review-2026-10-08/`.

Python unittest discovery passes **280 cases**: corpus tooling 240, gradient study 21, lanes 10,
glow textures 5 and static API contract 4. The static API contract check does not execute native
forces. `git diff --check` passes. Corpus population sweeps and native gameplay are not run.

The coolant reproduction was repeated with identical allocation totals. The repeated timings
are retained in the receipt; both timing runs overlap test work and are illustrative only.

## Merge review, 2026-10-11

The merge review covers the extraction, source-file splits, settings catalogue, geometry API,
thermal-vision changes, staggered step starts, frame telemetry, fixture migration and guides.
The starting branch and local `master` share commit `136c6c32`; the reviewed work is uncommitted
on top of that baseline. The existing findings above remain open, including opt-in parallel
provider fault handling; this merge does not implement the broader performance redesigns.

The review fixes a cancellation error in provider heating: the cached watts row included exposed
area and subtracted it after the callback, which could erase small positive heating values.
The row now contains only fixed waste/solar watts, with returned heating added directly.
A focused regression fails before the fix. Test summaries now distinguish fixed sources from
per-substep provider heating. Development guidance correctly excludes compile-only builds from
deployment, and the model-table repair entry is moved to its change log. Frame-pacing output
and guidance identify unmeasured time as unattributed rather than ruling out mod-related causes.

Validation results for the finished tree are recorded in the
[merge validation receipt](../tests/benchmarks/merge-review-2026-10-11.txt).
Native gameplay and population sweeps remain outside this review.

## Change log

| Date | Change |
| --- | --- |
| 2026-10-11 | Record merge review, provider-heating correction and telemetry/documentation limits. |
| 2026-10-08 | Review the current extraction/refactor; reconcile guides and reproduce quadratic open-chain discovery. |
