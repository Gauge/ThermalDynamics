# Thermal performance experiments

Run the command in [the optimization audit](../../docs/thermal-vision-optimization-audit.md).
The executable validates output before reporting timings. `BaselineBlockField.cs`
is a frozen offline reference, not code the mod ships. Do not update
it to mirror optimizations: that would invalidate the comparison. `results.json`
records one local run; reruns will vary with hardware and host load.

The celestial fixture compares original per-vertex sphere sampling with prepared
body/ring sampling for 32 discs. `results-celestial.json` records the measured run;
tessellation and rendered triangle count are unchanged.

The near-plane fixture retains `BaselineNearCap.cs` as the original clipping
reference and compares 4,096 camera/region combinations. `results-near-cap.json`
records the run with reusable clipping buffers.

The face traversal fixture compares interface enumeration against indexed access
using the production surface fields. `results-face-traversal.json` records timing
and allocations; it excludes native billboard submission.

The lattice sampling fixture checks each result against the frozen surface-field
reference before timing 102,400 samples. `results-lattice-sampling.json` records
the run; interpolation and clamping behavior are unchanged.

The deferred-coordinate fixture uses `BaselineSurfaceBuild.cs` to isolate patch
construction savings. `results-patch-build.json` records timings after checking
every generated patch against that frozen reference.

Selective partition comparison is recorded in `results-selective-patches.json`.
The executable exports examples to `/tmp/thermal-patch-examples.json`. Retained
exports are under `patch-study/`; render with:

```sh
python3 tools/thermal-performance/render_patch_comparison.py tools/thermal-performance/patch-study/examples.json tools/thermal-performance/patch-study/comparison.png
```

## Near-camera sampling

Near-camera sampling differential benchmark (frozen pre-optimization reference):

```sh
DOTNET_TieredCompilation=0 dotnet run --project tools/thermal-performance/ThermalPerformance.csproj -c Release -p:ThermalBuildRoot=/tmp/thermal-perf-build/ --no-restore -- --caps
```

`results-cap-sampling.json` records 1,024 deterministic caps through five fade
positions. All 54,466 output triangles and temperatures match exactly. The
median decreased from 2.986 to 2.441 ms (18.2%); measured allocation remained
40 bytes, from the measurement stopwatch. This is CPU sampling only, not GPU
rendering or an in-game FPS prediction.

## Nearest-block lookup

Nearest-block lookup can be measured separately with the same command and
`--nearest` instead of `--caps`. `results-nearest-query.json` covers 16,384
outside-kernel queries against 4,096 blocks and checks exact temperature equality
against the frozen reference. It measures query CPU cost, not frame time.

The executable references `Thermodynamics.Presentation` and `VRage.Math` only.
It does not link the physical simulation model or the SE1 drawing adapter.

## Files

`Program.cs` is the executable. With no flag it runs the general fixtures and prints their
results as JSON, which is where `results.json` came from; `--occlusion`, `--nearest` and `--caps`
each run and print one fixture instead. The `Baseline*.cs` files are the
frozen references each fixture compares against — `BaselineCapSampling.cs` is the near-camera
sampling one — and are not to be updated. `results-memory.json` holds the memory follow-up,
`results-diagonal-patches.json` the alternate-diagonal run and `diagonal-study/` its geometry, and
`quad-opportunity.json` the uniform-quad opportunity counts, which leave native rendering
unchanged; all three are read in
[the optimization audit](../../docs/thermal-vision-optimization-audit.md).

## Region occlusion

Use the command above with `--occlusion` to compare hierarchical rejection with
per-region solid-box rejection. `results-region-occlusion.json` records 100
traversals of 4,096 regions behind a nearby solid wall: both methods retain the
same 848 regions in the same order. Hierarchical traversal measured 7.525 ms
versus 39.135 ms for flat rejection across those 100 traversals. Both measured
40 bytes of stopwatch allocation. This compares two occlusion implementations,
not the previous renderer without occlusion, and excludes native submission.

## Core hot paths

`core_benchmark.py` compares the current Core with a frozen Git revision in the same
process. `CoreBenchmark.cs.txt` contains the bounded paired runner;
`CoreRig.cs.txt` contains its deterministic 128- and 512-block rigs. The script
compiles two namespaces in a temporary directory, alternates their order over
nine samples of 96 calls, and checks every captured temperature, stability demand,
substep count and coolant energy tally bit for bit after each sample. It uses
no corpus and runs its measured cases in about one second on the recorded host.
Frozen source extraction uses one ordered Git file listing and one batch blob read;
this reduces setup processes while preserving the receipt's source hash and file order.

For the loop replacement path alone, `--case loop-rebuild` selects
`CoreRebuildRig.cs.txt`: two blocks with one coolant parcel and pump each, reusing a
separate input list. It measures only `SetLoops` and compares carried temperatures,
fill, capacity, pump speed and link count after each batch. It does not run the
eight step cases, discover rings or build a native scene. Allocation counts include
pipe-contact construction; a zero warm result here does not include discovery or first-use growth.
`--case node-preparation` selects only the four existing ordinary-node cases: stability
preparation and full steps, each with flooring off/on. This keeps a preparation change's
whole-step control while omitting unrelated coolant workloads.

```sh
python3 tools/thermal-performance/core_benchmark.py --baseline 60677d353dddc0f8b90b7ee7733cdc8789bd3c27 --game-bin "$HOME/Steam/SteamLibrary/steamapps/common/SpaceEngineers/Bin64" --output /tmp/core-hot-path.json
python3 tools/thermal-performance/core_benchmark.py --baseline 60677d353dddc0f8b90b7ee7733cdc8789bd3c27 --game-bin "$HOME/Steam/SteamLibrary/steamapps/common/SpaceEngineers/Bin64" --case loop-rebuild --output /tmp/core-loop-rebuild.json
python3 tools/thermal-performance/core_benchmark.py --baseline 60677d353dddc0f8b90b7ee7733cdc8789bd3c27 --game-bin "$HOME/Steam/SteamLibrary/steamapps/common/SpaceEngineers/Bin64" --case node-preparation --output /tmp/core-node-preparation.json
```

Use the `baseline_commit` in `results-core-continuation.json` if copying this command
to a checkout with a different history. That receipt includes all sample times,
CPU, runtime, SDK, source hashes, allocations and the exact comparison result.
Its figures cover synthetic CPU work only; the game adapter, renderer, network,
topology construction and native .NET Framework runtime are outside this measurement.
The `active-coupling-v2` fixture sets block conductivity to zero in coolant-only cases:
the continuation corrected disabled conduction still contributing to stability demand.
This keeps the original and changed implementations comparable without reproducing that bug.
The earlier `results-core-hot-path.json` is retained with its original source fingerprint;
its nonzero inactive-conductance coolant fixtures cannot be compared directly with v2.

| Case | Median before / after per 96 calls | Interpretation |
| --- | --- | --- |
| Stability preparation, 512 blocks with a per-block substep floor | 0.207287 / 0.142853 ms | 31% less CPU in preparation by reusing the flooring walk's node rates. |
| Full step on those floored blocks | 2.839789 / 2.787088 ms | 1.9% less CPU in this fixture; preparation is a small part of a step. |
| Resolved coolant, 128 pipes and 256 contacts | 1.420466 / 1.194875 ms | 16% less CPU per batch. |
| Well-mixed coolant, same pipes and contacts | 0.337635 / 0.312868 ms | 7% less CPU per batch. |
| Default unfloored full step, 512 blocks | 24.045077 / 24.097227 ms | Unchanged within measurement noise. |

All eight cases match exactly and allocate zero bytes in their measured warm
loops. Clamped and stagnant coolant changes are within the sample noise and carry
no speedup claim. Enabled physical equations, configured defaults, public contracts
and persistence formats are unchanged; disabled mechanisms and floor-off transitions
were corrected separately. `CoreHotPathTests` keeps the regression
fixtures to four blocks plus the original modulo boundary cases.

`results-core-contact-slices.json` records a later checkpoint after resumable coupled
contacts, scratch clearing, diagnostic fusion and safe in-flight demand queries. It uses
the same `active-coupling-v2` fixture: all eight captured states match exactly and warm
allocations remain zero. Median flooring preparation is 0.208829 / 0.137994 ms per
96 calls (34% less), resolved coolant 1.432698 / 1.161540 ms (19% less), and ordinary
unfloored stepping 24.063761 / 24.106052 ms (unchanged within noise). These remain
whole-step CPU examples; bounded-slice correctness is checked by the tiny unit fixtures.
Each receipt identifies its source checkpoint and does not measure subsequent edits.

`results-core-loop-rebuild.json` records the separate two-loop checkpoint after lookup reuse.
Captured state matches exactly; warm allocations fall from 68,352 to 6,144 bytes per 96 calls
(712 to 64 bytes per replacement). Median batch time is 0.029005 / 0.014848 ms. These tiny
timings do not establish a native frame-time benefit. The remaining allocation at this checkpoint
is two temporary pipe-port lists; later changes need their own receipt.

`results-core-loop-ports.json` repeats only that fixture after direct sink-port iteration.
The original 68,352 bytes per 96 replacements become zero warm bytes, with exact captured state.
Median batch time is 0.029827 / 0.011823 ms (best 0.025669 / 0.011703 ms). This measures
the combined lookup and sink-port improvements against the original baseline. The separately
optimized discovery walk is outside this fixture and has only a four-pipe correctness comparison.

## Tiny uniform-surface check

`--uniform-surfaces` compares the six generated faces and both transition endpoints
against frozen `BaselineSurfaceField.cs`, using one 125-sample lattice, a cooling
corner-hotspot counterexample and three tiny extreme-value cases. Timing batches rebuild the uniform field 32 times:

```sh
DOTNET_TieredCompilation=0 dotnet run --project tools/thermal-performance/ThermalPerformance.csproj -c Release -p:ThermalBuildRoot=/tmp/thermal-perf-build/ -p:SEBinPath="$HOME/Steam/SteamLibrary/steamapps/common/SpaceEngineers/Bin64" -- --uniform-surfaces
```

`results-uniform-surfaces.json` records the 2026-10-28 run, host, method and source
fingerprint. The measured operation is CPU patch preparation only. The existing
`BaselineSurfaceBuild.cs` initializer typo `B=b=C=c` was corrected to `B=b,C=c` to
restore compilation; that repair does not change the frozen uniform reference.

## Change log

| Date | Change |
| --- | --- |
| 2026-10-28 | Allow selecting only ordinary-node preparation and whole-step controls when coolant timing is unrelated to a change. |
| 2026-10-28 | Record the two-loop replacement checkpoints, ending at zero warm allocations after removing temporary sink-port lists; keep discovery and native work outside that claim. |
| 2026-10-28 | Add an opt-in two-loop replacement fixture so lookup-storage changes can be measured without rerunning step workloads. |
| 2026-10-28 | Batch frozen Core extraction into two Git processes; verify all 92 paths and contents against an independent archive and preserve the recorded baseline hash. |
| 2026-10-28 | Preserve the contact-slicing checkpoint separately: exact state and zero warm allocations in the same small paired fixture; scope timings to that recorded source. |
| 2026-10-28 | Record the final Core and fixture hash; isolate coolant from the corrected inactive-conduction demand in the v2 fixture. Preserve the initial receipt separately. |
| 2026-10-28 | Add small paired Core and uniform-surface benchmarks, frozen references and timing/identity receipts. |
| 2026-09-24 | Named every file in the folder, which `EveryToolIsNamedByItsReadme` asks for. |
| 2026-09-19 | Added reproducible synthetic thermal CPU and allocation benchmarks, reference-output checks and optimization tradeoffs. |
