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

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | Named every file in the folder, which `EveryToolIsNamedByItsReadme` asks for. |
| 2026-09-19 | Added reproducible synthetic thermal CPU and allocation benchmarks, reference-output checks and optimization tradeoffs. |
