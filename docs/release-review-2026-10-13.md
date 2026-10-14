# Release review, 2026-10-13

This review evaluates Thermodynamics at `fb9ca3d2` plus the changes described below. It covers
release build checks, coolant topology cost, lifecycle retention and remaining runtime risks.
Measurements are synthetic offline discovery workloads, not native frame-time or gameplay results.
No package is deployed and no Workshop metadata, assets, public API, settings or solver equations change.

| Looking for | Go to |
| --- | --- |
| Prior implementation audit | [Repository review](repository-review-2026-10-08.md) |
| Build and deployment procedure | [Development](development.md) |
| Raw measurements and reproduction | [Validation receipt](../tests/benchmarks/release-review-2026-10-13.txt) |
| Existing product limits | [Known issues](known-issues.md) |

## Changes made

### Linear discovery of ordinary broken coolant runs

[CoolantLoopBuilder](../Thermodynamics/Core/Loops/CoolantLoopBuilder.cs) creates a rebuild-local
[search cache](../Thermodynamics/Core/Loops/CoolantLoopSearch.cs) after the first failed trace.
Every ordinary two-port pipe has two directed exits. The cache follows each exit to its terminal
fault and reuses that result for subsequent searches and diagnostics. A run with armour at one
end and mismatched plumbing at the other still reports the correct fault for each starting direction.

The first failed trace is linear; cached discovery visits each direction at most once before
resolving it. Working rings are claimed once, as before. Intact-ring-only grids never construct
the cache, preserving their original allocation cost. Ring ordering, pump direction, signatures,
flow values, diagnostic counts and examples are checked against the frozen pre-change builder.
Tests cover mixed directions and placement orders, multi-cell pumps, every break/repair position
on a ring, and custom/degenerate ports. Allocation scaling checks cover open chains and broken rings,
with diagnostics both enabled and disabled.

The fast path requires every coolant block on the grid to have exactly two distinct, cardinal,
outward-facing ports located on that block. If any fails that validation, the whole search uses
the original general traversal. That preserves custom plumbing behavior but leaves its quadratic
worst case open. Cache lifetime is one discovery call, so edits cannot leave stale topology results.
The public single-block tracer and steady-state coolant physics retain their existing behavior.

### Measured discovery cost

Final source, AMD Ryzen 9 9950X, optimized .NET 9 offline build, one warm-up and three measured
repetitions per case. Each cell below reports fastest–slowest milliseconds; allocation is for the
fastest repetition and uses decimal MB. Grid construction is excluded. The benchmark runs alone
relative to this session's builds/tests, inside a 24 GB memory cap. The receipt contains all sizes,
including diagnostics-enabled runs, raw byte counts and exact reproduction source.

| Case | Pipes | Original ms | Optimized ms | Original MB | Optimized MB |
| --- | ---: | ---: | ---: | ---: | ---: |
| Open straight chain | 2,048 | 379.132–386.252 | 0.545–0.805 | 796.380 | 0.855 |
| Open chain, alternating port order | 2,048 | 379.848–380.357 | 0.976–0.988 | 796.845 | 1.730 |
| Broken rectangular ring | 2,047 | 574.685–577.706 | 0.691–0.700 | 1,190.651 | 1.289 |
| Intact rectangular ring | 2,048 | 0.441–0.458 | 0.510–0.535 | 1.006 | 1.006 |

Open-chain allocation falls by 99.89% in the first row. With diagnostics enabled, the 2,047-pipe
broken ring falls from 2,381,302,744 to 1,288,816 bytes. The intact-ring path has identical measured
allocation; its measured time is slightly higher, so this is not an across-the-board speedup claim.
These are discovery-call costs under the offline runtime, not solver-step costs or promised game FPS.

### Release references after a parallel grid tick

[ThermalGridScheduler](../Thermodynamics/Game/ThermalGridScheduler.cs) clears its static ready list
in a `finally` block. Previously it cleared only at the beginning of another parallel tick, so
turning parallel mode off could keep the last batch of grid simulations reachable indefinitely,
including after those grids closed. Preparation or publication exceptions now release those
references too. List capacity remains reusable. This is a source-verified lifecycle correction;
there is no native heap-capture claim.

## Remaining priorities

| Priority | Finding and effect | Next action / acceptance evidence |
| --- | --- | --- |
| High | [RoomMapper.BeginPass](../Thermodynamics/Core/Surfaces/RoomMapper.cs) clears visited storage and allocates/clears the full sealing volume before the flood-fill cell budget applies. Sparse grids can produce large startup stalls and retained buffers; the integer-size guard is not a practical memory budget. | Measure the first `Step(1)` separately on sparse extents, set an explicit allocation policy, and budget initialization. Preserve room and door semantics through existing mapping tests. |
| High when parallel mode is enabled | [SurfaceHeating](../Thermodynamics/ThermalApiAerodynamics.cs) catches provider exceptions on solver threads, then mutates registration, logs and sends a mod message there. Gate exceptions remain registered and can recur. The ready-list cleanup does not resolve this. | Use consistent provider state and defer fault handling to the game thread. Exercise throwing providers on multiple grids in a native session. Keep the existing default `ParallelGrids=false` until validated. |
| Medium | [AdvanceEnvironment / AdvanceCoupled](../Thermodynamics/Core/Simulation/ThermalSolverStep.cs) run source accumulation and coupled loop/room/pump work without yielding across those passes. Coupled work is charged per device rather than per contact link. | Measure worst slice duration for large rooms, loops and many sources, then add cursors and accurate work accounting while preserving summation order, conservation and boundedness. |
| Medium | [Thermal-vision fleet drawing](../Thermodynamics/Game/ThermalVision/ThermalVisionIndependentFleet.cs) performs discovery/sorting/occlusion outside the preparation timer; a single preparation advance can also exceed its budget. | Capture crowded-scene CPU and GPU costs in game before changing visibility caching or budgets. Keep experimental display modes opt-in; offline mesh timings do not establish FPS. |
| Medium, consumer-dependent | [Geometry snapshots](../Thermodynamics/Core/Model/GridGeometrySnapshot.cs) copy block and occupied-cell arrays. Inventory mass changes can cause frequent revision-driven copies. | Confirm consumers cache by revision and coalesce updates; profile changing inventories before designing deltas. Preserve ownership of returned arrays. |
| Lower, custom plumbing | One nonstandard coolant shape disables the new fast path for the whole grid. | If custom content makes this material, partition validated two-port components while preserving general branch and fault behavior. Measure before broadening the algorithm. |

These findings are source-confirmed risks, not newly measured native regressions. Broad solver,
room-map or thermal-vision redesigns should remain separate from the validated discovery change.
The existing simulation/presentation split, game whitelist build and invariant tests are useful
release foundations; passing them does not settle these adapter and native-session questions.

## Release acceptance still needed

Run a packaged candidate in the game with the required dependencies and verify world load/save/reload,
block addition/removal, grid split/merge, broken/repaired coolant rings, damage authority and client
join/correction. Test with and without the external Aerodynamics provider. Exercise provider failure
and parallel mode explicitly if that setting is supported for release. Capture frame time and memory
on large/sparse grids and a crowded overlay scene, including repeated world loads and unloads.

The full-scene thermal-vision readiness gap documented in the
[lab guide](thermal-vision-lab.md) remains open. Choose the supported release surface explicitly;
an optional experimental mode should not be described as fully validated.

## Validation

The complete local .NET suite passes **2,687 cases**, zero failures or skips, in 1 m 15 s,
including integration fixtures and the new regression cases. The NoPackage solution build passes;
a fresh mod compile reports one existing obsolete billboard API warning. The baseline fast thermal
lane passes 2,181 cases and the initial focused coolant run passes 49 cases. `git diff --check` passes.
The receipt records commands, raw benchmark results and reproduction source.

Tests run in a 24 GB systemd scope with swap disabled. Opt-in corpus population sweeps remain off;
zero skipped tests does not imply those populations were exercised. Python tools are unchanged and
are not rerun. Packaging, deployment, native gameplay, native heap capture and multiplayer testing
remain outside this review.

## Change log

| Date | Change |
| --- | --- |
| 2026-10-13 | Evaluate release priorities; cache directional coolant failures; clear parallel scheduler references; add frozen-baseline parity and allocation regressions. |
