# Performance work

The record of the performance passes over this repository: what each iteration measured, what it
proposed, what it changed, and what the change was worth on a stated machine. It is the log of the
*work*; the instrument the work is measured with is [benchmarks.md](benchmarks.md), and the figures
quoted here are taken from that report unless a row says otherwise. This page does not restate what
a step costs or how the report is read — it says what was done about it and how each claim was
checked.

> The rules argued here are stated canonically in [rules.md](rules.md): `M4` `M5` `M6` `M7` `M13`
> `D7` `D8`, and the principles P1, P4 and P6 they follow from.

| Looking for | Go to |
| --- | --- |
| The report itself, and how to read a comparison | [benchmarks.md](benchmarks.md) |
| What a grid costs as it grows, and what makes it stutter | [load-and-hitching.md](load-and-hitching.md) |
| Why a handful of light fittings sets a capital ship's cost | [stiffness.md](stiffness.md) |
| Where the model is going at a million blocks | [scale-design.md](scale-design.md) |
| Open performance rows | [backlog.md](backlog.md), section D |

## How a pass is run

Every iteration takes the same eight steps, and the order is the point: the measurement comes
before the idea, and the oracle before the change.

1. **Measure.** `bench report` at the pass's own starting commit and at its tip, minutes apart, on
   one machine (`M7`). Where the report cannot see the thing in question,
   a lab is written first and the report is extended to carry the figure.
2. **Identify.** A candidate is a place the numbers accuse, and the counts are a place to look
   rather than a verdict (`D7`).
3. **Branch.** One branch per iteration, off the working branch's tip, merged back with a merge
   commit once the change has passed every check below.
4. **Validate in a lab, on an instrument that can resolve the claim.** The candidate is measured on
   one hull with the change flipped between timed blocks, because two hulls are two allocations
   with two cache colourings — and on a *stage timed by itself*, best of many, because a figure
   read off the whole build or the whole report carries every other stage's noise. This pass found
   both errors that follow from getting it wrong: a change kept would have been dismissed at a 6 %
   floor (iteration 10, really worth 10–18 %), and a change dismissed had to be re-judged at a 1 %
   floor before the dismissal meant anything (iteration 6, really worth nothing).
5. **Implement only if it pays and holds.** A change is kept when its saving is outside the noise
   floor (`M5`) and it breaks neither the three invariants (`C6`) nor a stated intent.
6. **Pin it.** An optimisation is asserted bit-identical to the code it replaced on a fixture that
   proves it exercised something (`D8`), and a defect it found is pinned so it cannot return.
7. **Clean up.** The code and the pages it replaced go; what stays describes the present.
8. **Refresh the lanes.** Take the runner's per-class durations and tag what crossed two seconds,
   because nothing checks that rule and it has rotted twice in two days.

**The machine.** Every figure on this page was taken on the repository's 32-core development
machine. It was not idle on 2026-08-26: two
editor language servers held a core each for the whole day and the swap was full. Measured rather
than assumed: the committed baseline's own commit, rebuilt and re-run unoptimised on this day,
read a calibration of 146 ms against the 106 ms it recorded on 2026-08-22, so the machine is
about **1.4×** slower than the baseline's — and nothing more. Every larger gap on this page is the
code, and each one is named. The comparisons here are taken minutes apart,
which is what makes the ratios readable when the absolutes are not.

## The iterations

| # | Date | Subject | Verdict | Where |
| ---: | --- | --- | --- | --- |
| 1 | 2026-08-26 | The harness measured unoptimised code | **kept** — every configuration now compiles optimised | [Iteration 1](#iteration-1--the-harness-measured-unoptimised-code) |
| 2 | 2026-08-26 | The ladder's `build` column measured the hull generator | **kept** — generator 15–17× cheaper, and outside the clock | [Iteration 2](#iteration-2--the-ladders-build-column-measured-the-hull-generator) |
| 3 | 2026-08-26 | An orientation is a signed permutation | **kept** — block construction halved at every size | [Iteration 3](#iteration-3--an-orientation-is-a-signed-permutation) |
| 4 | 2026-08-26 | The room mapper reads a snapshot of the sealing | **kept** — the room map 2.3× cheaper at half a million blocks | [Iteration 4](#iteration-4--the-room-mapper-reads-a-snapshot-of-the-sealing) |
| 5 | 2026-08-26 | The surface map's two layers in one dictionary | **kept** — the surface map 2× cheaper | [Iteration 5](#iteration-5--the-surface-maps-two-layers-in-one-dictionary) |
| 6 | 2026-08-26 | One row per link in the conduction loop | **dropped** — ±1.2 % at a 1 % floor, sign changing | [Iteration 6](#iteration-6--one-row-per-link-in-the-conduction-loop-tried-and-dropped) |
| 7 | 2026-08-26 | The room map's solid set is a bitset | **kept** — exposure 2× cheaper | [Iteration 7](#iteration-7--the-room-maps-solid-set-is-a-bitset-over-the-search-box) |
| 8 | 2026-08-26 | The flood fill steps an index, not a vector | **kept** — another fifth off the room map | [Iteration 8](#iteration-8--the-flood-fill-steps-an-index-not-a-vector) |
| 9 | 2026-08-26 | The fast lane had rotted to 37 s | **kept** — 4 s again, eleven classes tagged | [Iteration 9](#iteration-9--the-fast-lane-had-rotted-to-37-s) |
| 10 | 2026-08-26 | The interior scan steps its index | **kept** — the room pass 10–18 % cheaper | [Iteration 10](#iteration-10--the-interior-scan-steps-its-index) |
| 11 | 2026-08-26 | The cell tables are keyed on `GridMath.Key` | **kept** — links 2× cheaper, surfaces 3× | [Iteration 11](#iteration-11--the-cell-tables-are-keyed-on-gridmathkey) |

## Iteration 1 — the harness measured unoptimised code

**What was found.** Nothing in `tests/` set `<Optimize>`, and every command this repository runs —
`dotnet run --project Thermodynamics.Sim`, `dotnet test` — builds the Debug configuration. A Debug
assembly carries `DebuggableAttribute(DisableOptimizations)`, and the JIT honours it: it compiles
minimal-optimisation code with no register allocation across statements, no bounds-check
elimination and no inlining. **Every millisecond this repository had ever published was taken on
code the game never runs.**

**What it was worth, measured.** The same commit, the same machine, the same window, four
minutes apart; the committed report at `--size 32000 --max 125000`, fastest of three (`M4`).

| figure | unoptimised | optimised | ratio |
| --- | ---: | ---: | ---: |
| ladder 32,000, step | 18.33 ms | 5.21 ms | **0.28** |
| ladder 125,000, step | 58.27 ms | 18.36 ms | 0.32 |
| features, everything on | 17.90 ms | 5.22 ms | 0.29 |
| features, everything off | 0.959 ms | 0.248 ms | **0.26** |
| step shape, fixed per step (fit) | 2.07 ms | 0.84 ms | 0.41 |
| step shape, per substep (fit) | 0.602 ms | 0.163 ms | 0.27 |
| step shape, prologue and estimate | 0.718 ms | 0.169 ms | **0.23** |
| step shape, write-back | 0.261 ms | 0.071 ms | 0.27 |
| step shape, row fill, clamp live | 1.485 ms | 0.300 ms | **0.20** |
| overshoot clamp, resolved, always clamped | 30.24 ms | 11.49 ms | 0.38 |
| diagnostics, cost of being measured | 5.04 ms | 0.65 ms | **0.13** |
| fleets, 100 grids, whole fleet | 53.50 ms | 14.66 ms | 0.27 |
| noise, spread of five identical runs | 0.353 ms | 0.092 ms | 0.26 |

**The ratio is not a constant, and that is the finding.** A uniform 3.5× would have left every
comparison this repository ever made intact, since a benchmark exists for ratios. It is not
uniform: the row fill is 5× and the cost of diagnostics 7.7×, where the clamped conduction loop is
2.6×. So the published *shares* were wrong too — the row fill was quoted as a fifth of a step and
the diagnostics as a 15 % surcharge, and both were mostly the price of unoptimised code around a
field store. Any figure on [benchmarks.md](benchmarks.md) that is a *share* of a step, and any
optimisation whose case rested on hoisting a load out of a loop, was measured on an instrument
that exaggerated exactly that kind of work.

**What changed.** `tests/Directory.Build.props` sets `<Optimize>true</Optimize>` for every project
and every configuration, and it is `M13` in [rules.md](rules.md) so the next harness cannot repeat it. `dotnet run` and `dotnet test` still build Debug; Debug is now optimised.
Pinned by `OptimisedBuildTests`, which reads `DebuggableAttribute` off the built core, harness and
test assemblies and fails if any of them says the optimiser is off — a line dropped from a props
file fails nothing else.

**What it is not.** It is not the game. The game compiles the mod's sources with its own compiler and
runs it on .NET Framework 4.8; the harness is .NET 9. The 6–8× gap between a field dump and the
harness recorded on [load-and-hitching.md](load-and-hitching.md) was measured against the
unoptimised harness and is now larger, not smaller — the harness got faster and the game did not.
Absolute nanoseconds still do not transfer, which [benchmarks.md](benchmarks.md#what-it-measures)
already says; what transfers is the shape of a curve, and the shapes are now taken on an optimising
JIT, which is what the game has.

**The fast lane** ran 1,662 cases green on the optimised build before the change was committed.

## Iteration 2 — the ladder's `build` column measured the hull generator

**What was found.** `PerformanceReport.Build` started its stopwatch before `PlaceCensus`, which
since `C26` bolts every partial-mount block by trying all 24 orientations: each candidate rotated
each face through a freshly built matrix and probed a dictionary for the neighbour, with the 24
orientations themselves re-enumerated through `Enum.GetValues` per block — 144 matrices and 144
probes a block. Split on a 32,800-block hull, optimised: the generator was **1.0 s** and the
simulation's own build — surfaces, links, loops, rooms, exposure — **0.10 s**. The ladder's `build`
column reported the sum under a name that reads as the second, and the calibration row carried the
same generator inside its 4,000-block build. `LoadBenchmarks.MeasureBuilt` had always dealt its
hull outside the clock, so the repository's two `build` figures disagreed in scope (`P1`).

**Two changes.** The search resolves the 24 orientations' rotated faces once, into a static table
in `PipeFitter.AllOrientations`'s order, and probes each block's six neighbours once; same
candidates in the same order with the same tie-break, so the chosen orientations are the same
ship. The report deals the hull before the clock starts, in the ladder and the calibration alike.
`FacingItsNeighbours`, a second orientation search reached by nothing, is gone (`D2`).

**Pinned.** `CensusBoltTests` runs the original search — kept verbatim in the test — beside the
table-driven one over four hulls and requires the same orientation at every cell, after checking
that more than a twentieth of the blocks were turned at all (`D8`, `E8`). It failed on its first
run: the table's first entry is not the identity, and producers had been given index zero. That is
the pin doing the one thing it is for.

**What it was worth.** The tip before and after this iteration, optimised, in one window,
each twice, fastest kept (`M4`):

| figure | before | after | ratio |
| --- | ---: | ---: | ---: |
| machine, calibration | 285.4 ms | **94.0 ms** | 0.33 |
| ladder 8,000, build | 303.3 ms | **27.2 ms** | 0.09 |
| ladder 32,000, build | 1,193.6 ms | **170.6 ms** | 0.14 |
| ladder 125,000, build | 5,314.7 ms | **1,143.5 ms** | 0.22 |
| ladder 8,000 / 32,000 / 125,000, step | 1.19 / 5.21 / 18.49 ms | 1.15 / 5.22 / 18.39 ms | 0.96 / 1.00 / 0.99 |
| features, everything on | 5.16 ms | 5.40 ms | 1.05 |
| step shape, prologue and estimate | 0.169 ms | 0.167 ms | 0.99 |

And the generator on its own, dealt into a `GridBuilder` with nothing simulated, old search against
new in the same window, fastest of five:

| blocks | old `PlaceCensus` | new `PlaceCensus` | ratio | the simulation's build, for scale |
| ---: | ---: | ---: | ---: | ---: |
| 8,904 | 285.0 ms | **18.6 ms** | 0.065 | 18.3 ms |
| 32,800 | 977.9 ms | **57.3 ms** | 0.059 | 93.6 ms |
| 126,731 | 3,797.2 ms | **240.4 ms** | 0.063 | 622.1 ms |

Fifteen to seventeen times cheaper, and now under the build it feeds rather than ten times over
it. Every `Hulls.Driven` in the suite dealt a hull through the old search, so the suite's own
duration is where the rest of this saving lands.

The `step` rows are the control: the hull is the same ship, so nothing a step does may move, and
nothing did beyond the noise row. The `build` rows now describe the mod's load path, which is the
figure they were always read as. **The calibration row moved by 3×**, which means every
cross-machine comparison [benchmarks.md](benchmarks.md) invited through it was, since `C26`,
mostly comparing two copies of the generator.

**What it does not say.** The ladder's `build` is still the harness's simulation build and not the
game's world load — the adapter's mirror of a `MyCubeGrid` is outside this instrument, as it always
was.

## Iteration 3 — an orientation is a signed permutation

**What was found.** Reading the load path for the same shape as iteration 2 found it in shipped
code: `BlockOrientation.Rotate`, `Unrotate` and `RotateFace` each built a `Matrix` with
`Matrix.CreateWorld` and transformed through it, and a `BlockInstance` calls them about ten times
per cell at construction — every cell's grid position, and every face's rotated surface bits. That
is the path a world load and a blueprint paste take, per block.

**What changed.** The struct holds a table, built once by its static constructor *from its own
matrix*, of where each local axis and each local face lands per orientation; a rotation is three
multiplies by ±1 and three adds. The twelve forward/up pairs that are not orientations keep the
matrix path unchanged. Exact on integers either way, so the two forms are identical rather than
close.

**Pinned.** `BlockOrientationCacheTests` compares the table against the matrix over every integer
vector of a 7×7×7 cube for all thirty-six pairs — forward, back and per face — and checks
separately that every legal orientation rotates and unrotates to where it started, so a table built
from a degenerate matrix could not agree its way through. `FleetParallelTests` records why the three
static tables may be shared by grids on different threads: built once, never written after.

**What it was worth.** `bench load` is the one instrument here that constructs blocks inside its
clock — a heavy-armour hull placed block by block, registered, then rebuilt — and its `adding
blocks` figure is the block construction and registration; `RebuildAll` is the control, since it
constructs no block. Before and after, optimised, one window, each twice, fastest kept:

| blocks | adding blocks, before | after | ratio | `RebuildAll`, before → after |
| ---: | ---: | ---: | ---: | ---: |
| 126,731 | 310 ms | **176 ms** | 0.57 | 999 → 989 ms |
| 1,000,294 | 1,779 ms | **862 ms** | 0.48 | 9,672 → 9,629 ms |

Half of the cost of placing a block was building matrices to rotate a one-cell block at the
identity. The control moved by less than its own repeat-to-repeat spread (±3 %). A world load at a
million blocks is 11.2 s on this machine and the block half of it is now under a second; the ten
seconds left are `RebuildAll`, which is where iteration 4 goes.

## Iteration 4 — the room mapper reads a snapshot of the sealing

**What was found.** `bench scale` split the build at 505,566 blocks: **2,954 ms of 4,388 ms was
the room map**, against 392 ms for the links and 345 ms for exposure. The flood fill visits every
cell of a bounding volume fourteen times the block count, asks two questions per face — is this
face sealed on either side, is that cell solid — and each was answered from `SurfaceMap`'s
dictionaries: two hash probes per face, twelve per cell, eighty million probes a pass. It is also
what [backlog.md](backlog.md) `D2` measures at 3,934 ticks to converge at a million blocks, and
what a world load waits on.

**What changed.** When a pass begins, the mapper copies the structural self-airtight bits of every
occupied cell in its search box into one byte per cell, and answers both questions from that with
array reads. The copy is exact because a change to what a block seals already requests a restart
(`ThermalSimulation.RefreshBlock` marks the topology dirty on any structural change; `AddBlock` and
`RemoveBlock` always do), so a pass never read a surface map that had moved under it — and now it
reads one that cannot. The buffer is the bounding volume in bytes, 6.8 MB at 505k blocks and 14 MB
at a million, retained between passes and regrown only when the box outgrows it; a box past
`int.MaxValue` cells falls back to the dictionaries rather than allocating. The dictionary path
stays behind `SnapshotSealing` for the pin.

**Pinned.** `RoomMapSnapshotTests` publishes the map both ways and requires the same room count,
solid count, external count, the same cells in the same order in every room, the same venting, and
the same portals — on a census hull with compartments, on a shell with a door (the portal case), and
then steps the pressurised hull both ways to identical temperatures through `SolverAb`. Each fixture
asserts it found rooms and portals first (`E8`).

**What it was worth.** `bench scale`, ship shape, before and after in one window, twice each:

| blocks | bounding cells | rooms, before | rooms, after | ratio | build, before → after | links, exposure (control) |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 32,800 | 328,640 | 35 / 43 ms | **28 / 28 ms** | 0.80 | 130 → 165 ms | 15 / 10 → 18 / 19 ms |
| 126,731 | 1,499,616 | 271 / 328 ms | **176 / 185 ms** | 0.65 | 592 → 525 ms | 92 / 81 → 101 / 82 ms |
| 505,566 | 6,838,104 | 2,065 / 2,874 ms | **893 / 913 ms** | 0.43 | 3,917 → 2,328 ms | 370 / 329 → 399 / 334 ms |

> **The ratio column is fastest against fastest, and it read 0.65 / 0.54 / 0.31 for a few minutes
> after this section was written** — the fastest *after* divided by the slowest *before*, which is
> the arithmetic `M4` exists to prevent and which flatters every row. Corrected in place (`E10`).

The fastest of the two is the figure; the pairs are printed because the *before* rows at 505k
disagree with each other by 40 %, which is the dictionary path's own sensitivity to the state of the
cache, and the *after* rows by 2 %. The control columns did not move beyond
their spread. What is left of the room map's cost is the bitset, the frontier queue and the
published map's own per-cell writes; the `settle` column — ticks to converge after a placement — is
unchanged at every rung, because the mapper's budget is counted in cells and this iteration made
each cell cheaper rather than fewer.

**The next rung, measured rather than guessed.** Split further at 126,731 blocks after this change:
`SurfaceMap.Rebuild` 173–254 ms, links 107–178, rooms 156–188, exposure 82–152, room air 3–7. The
surface map's two per-cell dictionaries — built, then refreshed at seven probes per cell per layer —
are now the largest single term of a load.

## Iteration 5 — the surface map's two layers in one dictionary

Every occupied cell was
held twice, in two `Dictionary<Vector3I, int>` keyed on the same cell — the live layer and the
structural one — and refreshed in the same call, so a refresh probed each of six neighbours twice and
a rebuild inserted every cell twice. The two states are packed into one `long` now, live in the low
half and structural in the high: seven probes a cell rather than fourteen, and one table rather than
two, which is also the surface map's retained memory halved. `SurfaceMapPackingTests` keeps the
two-dictionary map verbatim and holds the packed one to the same answer on every cell and every
neighbour of a census hull and of a shell with a door — rebuilt, added block by block, with blocks
removed, and with the door open, which is the only state in which the two layers differ and the test
asserts that they do.

**What it was worth.** The `RebuildAll` split, each stage on its own clock on a dealt hull, at the
commit before and after, one window, fastest of two:

| blocks | `SurfaceMap.Rebuild`, before | after | ratio | links, rooms (controls) |
| ---: | ---: | ---: | ---: | ---: |
| 126,731 | 86 ms | **32 ms** | 0.37 | 76 / 138 → 95 / 181 ms |
| 505,566 | 653 ms | **291 ms** | 0.45 | 426 / 863 → 428 / 866 ms |

Better than the halving the probe count predicts at the small rung, because the second table was
also the second set of cache lines; and the controls at 505k did not move.

## Iteration 6 — one row per link in the conduction loop: tried and dropped

**What was tried.** The innermost loop gathered a link from three parallel arrays — two node
indices and a conductance — beside the node rows it scatters into. A `LinkRow` struct put the three
in one array: one stream and one bounds check where there were three of each, the same arithmetic in
the same order, so every bit-identity suite and every byte-identical scenario passed unchanged.

**What it measured.** The full report at the commit before and after, optimised, one window,
twice each, fastest kept:

| figure | before | after | ratio |
| --- | ---: | ---: | ---: |
| ladder 8,000 / 32,000 / 125,000, step | 1.146 / 5.252 / 18.325 ms | 1.209 / 5.190 / 19.366 ms | 1.06 / 0.99 / 1.06 |
| features, everything on | 5.213 ms | 5.217 ms | 1.00 |
| features, conduction, isolated | 0.458 ms | 0.478 ms | 1.05 |
| features, conduction, marginal | 1.675 ms | 1.539 ms | 0.92 |
| overshoot clamp, resolved, always clamped | 11.109 ms | 11.305 ms | 1.02 |
| noise, spread of five identical runs | 0.063–0.128 ms | 0.110–0.186 ms | — |

**Not kept** (`M5`). Nothing moved outside the noise floor, in either direction, on any hull.

**And the report was the wrong instrument to decide it on, which iteration 10 is what proved.** Two
nominally identical step measurements inside one report disagree by two to five per cent, so
"inside the noise" there means *under about five per cent* — which is a floor wide enough to hide a
real saving. Iteration 10 was dismissed on that same instrument and turned out to be worth 10 to
18 % when the room pass was timed on its own, so this was re-judged the same way: the substep loop
alone, one settled 32,800- and 126,731-block hull, twenty steps a repeat, fifteen repeats, fastest
kept.

| hull | with link rows | with three arrays | ratio |
| --- | ---: | ---: | ---: |
| 32,800 blocks, 64,964 links | 5.4736 ms | 5.4098 ms | **1.012** |
| 126,731 blocks, 247,350 links | 18.7644 ms | 18.8290 ms | **0.997** |

Repeating each leg puts the best-to-best variation within one tree at 0.6 to 1.3 %, so this
instrument resolves about a per cent — and the effect is ±1.2 % with the sign changing between
rungs. The substeps run are identical on both sides (8,485 and 7,272), so the two are the same
arithmetic at the same count.

**So the revert stands, and now it stands on evidence rather than on a floor that was too wide.**
The finding is about the loop: the three link streams were already sequential and prefetched, so
folding them bought nothing, and what the loop pays for is the two gathers and two scatters into
the *node* rows, which no layout of the link side can touch. The commit is reverted in the same
branch, so the attempt is in the history and the tree carries no code that measured as nothing.

## Iteration 7 — the room map's solid set is a bitset over the search box

The published map held its solid cells in a
`HashSet<Vector3I>` — about forty bytes a member on a hull that is a third to three quarters
structure — and every exposure face asks `IsExternal`, which asked that set first. It is a
`CellBitset` over the search box now: an eighth of a byte a cell, and a bit read where there was a
hash. `RoomMapSolidTests` checks every cell of the box, inside and out, against the surface map's
own sealing rather than against the map (`E7`), and that a second pass starts from an empty set.

**What it was worth.** The same split, at the commit before and after, fastest of two:

| blocks | exposure, before | after | ratio | rooms, before → after | resident MB (ladder) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 126,731 | 61 ms | **32 ms** | 0.52 | 181 → 136 ms | 88 → 90 |
| 505,566 | 317 ms | **143 ms** | 0.45 | 866 → 769 ms | 450 → 391 |

Exposure halves because most of its faces are exposed ones, and each of those asked the hash set.
The room pass gains too, from writing bits rather than hashing cells into the set. The resident
column is the ladder's coarse `GC.GetTotalMemory` difference and moves by more than this change
between repeats of one tree, so it is printed and not claimed.

## Iteration 8 — the flood fill steps an index, not a vector

With the sealing snapshot live, a flood step still
derived each neighbour's index in the visited bitset from its coordinates — three subtractions and
three compares — and then again for the snapshot. The cell's index is derived once and each face
adds a precomputed delta; the box test is the only per-face geometry left. Same faces in the same
order, so `RoomMapSnapshotTests`, which runs the dictionary path beside it, is the pin.

**What it was worth.** The same split, at the commit before and after, fastest of two:

| blocks | rooms, before | after | ratio |
| ---: | ---: | ---: | ---: |
| 126,731 | 136 ms | **111 ms** | 0.82 |
| 505,566 | 769 ms | **626 ms** | 0.81 |

Another fifth off, and the room pass now costs about **92 ns per bounding cell** at half a million
blocks — 626 ms over 6,838,104 — against **302 ns** before iteration 4 on this same instrument, and
432 ns in the pass's first reading of it.

**The ladder taken in the same window disagrees, and it is the ladder that is wrong.** Its rows for
this commit carry a *step* column 28 % above the rows before it — a control no change in this pass
can touch — so those rows are the machine rather than the code. The split above was taken first,
its own controls (links, surfaces) held, and it is the figure quoted.

**Where the build stands at 505,566 blocks after iterations 4, 5, 7 and 8**, on one clock: rooms
626 ms, links 503, surfaces 418, exposure 173, block registration 56 — **1.6 s** where the pass
began at 4.4. Links are the next largest term and were not touched.

## Iteration 9 — the fast lane had rotted to 37 s

**What was found.** The suite's own duration is a number no test inside it can read, which is why
[tests/README.md](../tests/README.md#the-two-lanes-and-the-rule-that-sorts-them) says the lane rule
rots. Timed for this pass's iteration log: the whole suite **1 m 22 s** over 2,001 cases on the
optimised build, from 2 m 34 s over 1,884 — and the fast lane **37 s**, where the page said 4.
Per class, from the runner's own log: `DesignedHullTests` 35 s of test time on its own, and ten
more classes past two seconds — `DialReachTests` 11.8 s, `ModHardwareRetestTests`,
`SettingsDialReachTests`, `LoopDialReachTests`, `LoopCoolantMassTests`, `ScenarioTests`,
`ScriptWhitelistTests`, `HeatTimeScaleTests`, `CoolantLoopTests` and `DocumentationTests`, each
between 2.4 and 4.1 s — none tagged, all written or grown in the two days since the lanes were last
sorted.

**What changed.** The eleven carry `[Trait("speed", "slow")]`; thirty classes do now. The fast lane
is **4 s over 1,582 cases** — 5.5, 5.6, 6.2 s of wall clock across three runs (`M4`). Nothing about
the tests moved; what moved is which lane a developer waits for.

**What it does not fix.** The rule is still unchecked, and this is the second time in two days it
has been found rotten by measuring rather than by a test. The honest check would read the runner's
durations back, which is a tool outside the suite; until one exists the refresh is a step of every
performance pass, which this page now lists.

## Iteration 10 — the interior scan steps its index

**What was found.** The interior scan walks every cell of the bounding volume — 3.1 million at
126,731 blocks and 13.7 million at 505,566 — and derived each cell's index twice, once for the
visited bitset and once for the sealing snapshot: three subtractions, six compares and a multiply,
each time. **The scan order is the index order exactly**, x fastest then y then z, so a step of one
cell is a step of one index, wraps included. And `IsStructure` asked a `HashSet<Vector3I>` whether
every sealed cell belonged to a door, on grids that mostly have no door at all.

**What changed.** The scan carries its index and increments it; the door probe tests the count
first.

**Pinned.** `RoomMapSnapshotTests` — which publishes the map both ways and compares it cell for
cell — is the pin, and it was proven against a deliberate off-by-one on the wrap: six of its nine
cases fail. `WalkingABoxInScanOrderAdvancesTheIndexByOne` states the ordering claim where it is
made rather than through the map, over three box shapes including one a single row deep.

**The ladder could not resolve it, and a tighter instrument could.** On the build split the room
pass read 571 ms before and 551 after at 505,566 blocks, against a repeat-to-repeat spread of 6 to
12 % — a figure inside the noise floor has not moved (`M5`), and on that instrument this change had
not. So the room pass was measured **on its own**, on one prebuilt grid, fifteen passes, fastest
kept:

| blocks | cells a pass | rooms, before | after | ratio |
| ---: | ---: | ---: | ---: | ---: |
| 126,731 | 3,072,469 | 69.4 ms | **57.0 ms** | 0.82 |
| 505,566 | 13,720,674 | 498.3 ms | **446.2 ms** | 0.90 |

Repeated, the same pair reads 71.0 → 61.9 and 510.5 → 450.9: the direction is the same in all four
pairs, and the gap is several times the best-to-best variation between rounds of one tree. **The
cells-visited counter is identical on both sides** — 3,072,469 and 13,720,674 — which is what says
the two are the same walk at a different price rather than two different walks, the same way
`LoadTests` holds a claim on work counters rather than on milliseconds.

**And the spread is the reason this needed its own instrument.** Fifteen passes of the same code
range from 69 ms to 263: the *worst* of N is about the machine and only the best of N is about the
code (`M4`).

## Iteration 11 — the cell tables are keyed on `GridMath.Key`

**What was found.** Links were the largest term never touched, so the link build was timed alone:
at 505,566 blocks the neighbour query is **80 % of it** (282 of 338 ms), and a neighbour query is
six dictionary probes per cell. Probed directly — the hull's own cells, six face neighbours each,
the same hits in the same order — a `Dictionary<Vector3I, …>` with `Vector3I.Comparer` costs **51–58
ns a probe** and a `Dictionary<long, …>` keyed on `GridMath.Key` costs **9–13**. A struct key goes
through a comparer object for its hash and its equality; a `long` does both inline. And the same
table shape sat under the surface map, the room index a pass writes, and the mapper's door cells.

**What changed.** All four are keyed on `GridMath.Key`. That is the key `BlockInstance.Key` and the
frozen room arrays already used — a 64-bit packing injective over any grid the game can hold, which
`GridMathTests` pins — so no new assumption about a grid's extent is made. A dictionary's answers do
not depend on how it hashes, so every existing pin holds unchanged: the packed surface map against
its verbatim two-dictionary reference, the snapshot mapper against the dictionary mapper, the
byte-identical scenarios.

**What it was worth.** Best of fifteen on one prebuilt grid, before and after interleaved in one
window, three rounds; and the build split beside it:

| blocks | neighbour query, before | after | ratio | link build, before | after | ratio |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 126,731 | 44.6 ms | **30.1 ms** | 0.67 | 58.5 ms | **39.1 ms** | 0.67 |
| 505,566 | 276.1 ms | **154.1 ms** | 0.56 | 355.5 ms | **184.4 ms** | 0.52 |

| build stage, 505,566 blocks | before | after | ratio |
| --- | ---: | ---: | ---: |
| surfaces | 308 ms | **100 ms** | 0.32 |
| links | 436 ms | **232 ms** | 0.53 |
| rooms | 513 ms | **323 ms** | 0.63 |
| exposure | 167 ms | **95 ms** | 0.57 |

Every stage that probes a cell table moved, which is what a change to the tables and nothing else
should do. The room pass moves through the room index it writes and the door probe, not through
the flood fill, which already reads the snapshot.

**And the first two attempts to measure this compared the tip with itself.** The "before" probe is
a scratch project whose reference the harness of a worktree, and a `sed` meant to repoint it
matched nothing and said nothing — so two pairings read *no change* with pleasing consistency. The
pairing above is preceded by a check that the two probes carry different core assemblies, and it
refuses to time anything if they do not. That is the same defect the pass keeps finding, in the
instrument rather than the mod: a comparison that cannot fail is not a comparison (`E8`).

---

## What the pass moved

Every figure below is the pass's starting commit against its tip, both built optimised, interleaved
in one window, each leg twice, fastest kept (`M7`, `M4`). The starting commit is measured
*optimised* on purpose: iteration 1's saving is a property of how the harness was built rather than
of the mod, so building both legs the same way is what leaves the code changes on their own.

| Figure | start | tip | ratio |
| --- | ---: | ---: | ---: |
| **World load, 1,000,294 blocks** | | | |
| block construction and registration | 1,749 ms | **677 ms** | 0.39 |
| `RebuildAll` | 10,126 ms | **2,481 ms** | 0.25 |
| whole load | 12,025 ms | **3,158 ms** | **0.26** |
| **The build ladder** | | | |
| 8,904 blocks | 325.1 ms | **16.8 ms** | 0.05 |
| 32,800 blocks | 1,188.8 ms | **101.8 ms** | 0.09 |
| 126,731 blocks | 5,129.9 ms | **537.4 ms** | 0.10 |
| calibration (4k hull, built and stepped) | 286.2 ms | **83.1 ms** | 0.29 |
| **Memory at 126,731 blocks** | | | |
| retained | 857 B/block | **797 B/block** | 0.93 |
| peak | 1,086 B/block | **999 B/block** | 0.92 |
| **The suite** | | | |
| fast lane | 37 s | **4 s** | 0.11 |
| whole suite | 2 m 34 s, 1,884 cases | **1 m 22 s, 2,001 cases** | — |

**The build ladder's ratio is not all mod code**, and the table would mislead without saying so:
most of the 8,000-block rung is iteration 2 taking the census generator out of the clock, which is
harness. The figure that is all mod is the world load — `bench load` places heavy armour itself and
times only construction and `RebuildAll` — and it is **3.8× faster**, from iterations 3, 4, 5, 7,
8, 10 and 11. The load and memory rows were re-taken after iteration 11 in the same way, against the
commit before it: 910 → 677 ms registering and 4,007 → 2,481 ms rebuilding, interleaved, twice.

**What did not move, which is the control.** Nothing in this pass touched the substep loop, and the
step columns say so: 1.194 → 1.143 ms at 8,904 blocks, 5.381 → 5.373 at 32,800, 19.650 → 19.784 at
126,731, every feature's marginal and isolated cost unchanged, and a bare configuration identical to
three decimal places. The scenario and environment rows read 3 to 6 % higher and the ladder rows
1 % lower — two measurements of the same thing disagreeing by that much is this instrument's own
repeatability, not a change.

**Where the next pass starts.** At 505,566 blocks the build split reads rooms ~323 ms, links
~232, surfaces ~100, exposure ~95, registration ~43 after iteration 11 — about 0.8 s of stage time
where the pass found 4.4 s. The room pass is the largest term again, and what is left in it is the
frontier queue, the visited bitset and the room index's own writes. Below that, `RebuildAll` at a million
blocks is still 4 s of a world load, and the room map still floods a bounding volume fourteen times
the block count — cheaper per cell, and the same number of cells ([backlog.md](backlog.md) `D2`).

---

# Pass 2 — 2026-08-26, evening

The second pass, started from `84e3be5`, the tip the first one left. It uses the first pass's
lessons as its procedure: every candidate is judged on `bench stages`, and every before/after script
proves its two legs differ before it times anything.

**Where it started.** The same machine, the same window; every figure below is the fastest of
fifteen on one prebuilt grid unless the row says otherwise:

| stage, 505,566 blocks | start of pass 2 | work | ns per unit |
| --- | ---: | ---: | ---: |
| surfaces | 81.8 ms | 505,566 cells | 162 |
| links | 196.2 ms | 952,523 links | 206 |
| rooms | 292.4 ms | 13,720,674 cells visited | 21.3 |
| exposure | 95.2 ms | 505,566 nodes | 188 |
| a settled step, granted its substeps | 93.9 ms | 28 substeps | — |
| world load at 1,000,294 blocks | 638 + 2,538 ms | — | — |

*`bench stages --size 500000`, taken at `3296cfd` with the instrument itself in the tree; the scratch
probe read the same commit at 121 / 177 / 311 / 93 / 94.7 ms a few minutes earlier, which is the
same ordering and the same shape with the surface and link rows inside their own spread.*

## Pass 2, iterations

| # | Subject | Verdict | Where |
| ---: | --- | --- | --- |
| 1 | The stage instrument lives in the tree | **kept** — `bench stages`, `StageLabTests` | [Iteration 1](#pass-2-iteration-1--the-stage-instrument-lives-in-the-tree) |
| 2 | A one-cell block's neighbours are six probes | **kept** — links 0.77–0.88 | [Iteration 2](#pass-2-iteration-2--a-one-cell-blocks-neighbours-are-six-probes) |
| 3 | A one-cell block's exposure is one state and six tests | **kept** — exposure 0.65 | [Iteration 3](#pass-2-iteration-3--a-one-cell-blocks-exposure-is-one-cell-state-and-six-face-tests) |
| 4 | The interior scan skips visited cells a word at a time | **kept** — room-map ticks to converge 0.54–0.62 | [Iteration 4](#pass-2-iteration-4--the-interior-scan-skips-visited-cells-a-word-at-a-time) |
| 5 | The environment pass reads one row per node | **dropped** — 0.99 at a 1 % floor | [Iteration 5](#pass-2-iteration-5--the-environment-pass-reads-one-row-per-node) |
| 6 | A one-cell block is built without walking its cells | **kept** — place 0.64–0.83 | [Iteration 6](#pass-2-iteration-6--a-one-cell-block-is-built-without-walking-its-cells) |
| 7 | The flood's box test is one compare on the axis that moved | **dropped** — slower, 1.25–1.30 | [Iteration 7](#pass-2-iteration-7--the-floods-box-test-is-one-compare-on-the-axis-that-moved) |
| 8 | The freeze sorts by radix, after two forms that walked a bitset and lost | **kept** — the publish tick 0.72, rooms 0.85–0.90 | [Iteration 8](#pass-2-iteration-8--the-freeze-walks-a-bitset-in-key-order-instead-of-sorting) |
| 9 | A block's two surface layers share one array unless it is a door | **kept** — place 0.85–0.87 | [Iteration 9](#pass-2-iteration-9--a-blocks-two-surface-layers-share-one-array-unless-it-is-a-door) |
| 10 | What the pass moved | world load 0.70, exposure 0.63, rooms 0.78, links 0.83, the step 1.00 | [Iteration 10](#pass-2-iteration-10--what-the-pass-moved) |

## Pass 2, iteration 1 — the stage instrument lives in the tree

The first pass judged two changes wrongly on the build ladder and had to re-judge both on a scratch
probe that timed one stage alone. That probe is `StageLab` and `bench stages` now: placing blocks,
registering them, surfaces, links, rooms, exposure and a settled step, each on one prebuilt grid, fastest of fifteen, with the stage's
own work counter beside the time — and a work figure that moves between repeats aborts the row,
because two readings of different walks are not a comparison (`P6`). `StageLabTests` holds that
every stage reports work on a hull that exercised it and that the work is the same figure asked
twice (`E8`). It reports nanoseconds per unit of work, which is the figure that transfers between
sizes.

## Pass 2, iteration 2 — a one-cell block's neighbours are six probes

**What was found.** The neighbour query is 80 % of the link build, and at ~50 ns per probed cell
only ~10 of it is the dictionary since iteration 11 of the first pass: the rest is the slab walk
through `BoxGeometry`'s per-axis switches and a `List.Contains` dedupe per candidate. A one-cell
block — nearly every block on a hull — needs neither: it has one candidate cell per face, and a
neighbour is a box, so a box can touch a unit cube on at most one face. Its six answers are
distinct by construction.

**What changed.** `GridModel.GetNeighbours` answers a one-cell block with six probes in face order
and hands everything else to the boundary walk, which stays as `GetNeighboursWalkingTheBoundary`.
Same faces in the same order, so the link list — and with it the order the conduction sum
accumulates in — is unchanged. `GridModelAdjacencyTests` holds the two paths to the same neighbours
in the same order over every block of a census hull and of a grid that mixes unit blocks with bars
and a cube, so both shapes the dedupe exists for are on the fixture.

**What it was worth.** `bench stages --stages links`, the commit before against the tip, the two
cores proven different first, interleaved, two rounds, fastest kept:

| blocks | links, before | after | ratio |
| ---: | ---: | ---: | ---: |
| 126,731 | 47.96 ms | **36.80 ms** | 0.77 |
| 505,566 | 192.53 ms | **169.78 ms** | 0.88 |

The same direction in all four pairs, and the link count identical on both sides. Less at the
large rung, where the probes' cache misses are a larger share of the query and the arithmetic
around them a smaller one.

## Pass 2, iteration 3 — a one-cell block's exposure is one cell state and six face tests

**What was found.** The exposure refresh is the same shape as the neighbour query one stage later:
for every block, every cell of every face of its box, through the same per-axis switches, asking
the surface map for the cell's state on each face. For a one-cell block that is one state read six
times and six face tests.

**What changed.** The one-cell path reads the state once and asks the same two questions per face
in the same order — is this face sealed from the other side, does the space beyond reach the
outside — and hands every larger block to the boundary walk, which stays as a public overload so a test can
reach it. `ExposureFastPathTests` holds the two to the same count on
every face of every block of a mapped census hull and of the mixed grid.

**What it was worth.** `bench stages --stages exposure`, before against after, cores proven
different, interleaved, two rounds, fastest kept:

| blocks | exposure, before | after | ratio |
| ---: | ---: | ---: | ---: |
| 126,731 | 17.74 ms | **11.88 ms** | 0.67 |
| 505,566 | 97.11 ms | **63.43 ms** | 0.65 |



| Date | Change |
| --- | --- |
| 2026-08-27 | **Closed pass 8.** The first of passes 6–7's rejections was re-measured on the corrected instrument and stands: halving the link build's lookups is 1.02. The other six want a pass of their own. |
| 2026-08-27 | Audited this page against the instrument correction: pass 5's figures and the large wins (0.36, 0.43, 0.50, 0.53, 0.59) survive, and **every stage ratio between about 0.7 and 1.4 taken before pass 8 is inside the uncertainty** — including seven rejections in passes 6 and 7. A flat control does not rescue them, because the control was a stage with the same problem. |
| 2026-08-27 | Opened pass 8 on the instrument rather than the code: `bench stages`' best-of-fifteen had **not converged** — three runs of the same binary spread 48 %, 28 % and 67 % on the surface, room and link stages, against 3.5 % for the solver, which was taking twenty times the samples. A stage now repeats until five readings agree with its best within two per cent, on a floor of a hundred. |
| 2026-08-27 | **Closed pass 7 with one change kept**: the link list's order is a function of the graph, and with the chain rebuild folded into the sort it reads **0.94** against pass 6's tip at 505,566 blocks. The change it was meant to unblock — walking cells in index order — was built and measured at 1.23, so `D3b` is a floor rather than a task. |
| 2026-08-27 | Pass 7 opened by removing the obstacle `D3b` named — the link list's order is a function of the graph now, at a cost of 1.02 — and then measured the change it unblocked at **1.23**, and 1.16 with the empty box skipped. The sort it needs costs nothing; the walk does, because a hull fills a fourteenth of its box and a cell-indexed row is bigger than the table it replaces. |
| 2026-08-27 | **Closed pass 6 with no change kept.** Five bit-identical rewrites of the link build's neighbour lookup — by rank, by dense row, twice by removing the code around it, and once by halving the number of lookups — measured between 0.91 and 1.40, and the last of them explains the rest: the stage is bound by touching grid-sized memory once per neighbour, and every scheme keeps one such touch. Walking blocks in cell order would fix it and is refused here for moving every temperature's last bit. |
| 2026-08-27 | Opened pass 6 on the load path's largest stage. Its first iteration splits the link build by ablation: **nine tenths of it is finding neighbours**, at fifty nanoseconds a dictionary probe, and every per-pair operation together is the other tenth. |
| 2026-08-27 | **Closed pass 5**: the settled step is **0.86** and the link build **0.84** at 505,566 blocks, with three untouched stages at 1.00, 0.99 and 1.03 as controls. The step had never moved in four passes; what moved it was measuring where its time went. `D1`'s open question is answered — conduction is at its floor. |
| 2026-08-27 | Pass 5, iteration 6: the link build consults the occupancy bit before the block table — links **0.88** at 505,566 blocks and 0.76 at 126,731, both controls flat. |
| 2026-08-27 | Pass 5, iterations 4 and 5: `bench stepfloor` answers `D1`'s open question — **conduction is its own floor**, faster than a loop doing its memory accesses and no arithmetic. And the idea that came out of it, putting buried nodes through the exposed path, measured **1.30** and was dropped: the branch is predictable and what it skips is three row reads, not just arithmetic. |
| 2026-08-27 | Pass 5, iteration 3: the grid's own heat gain is summed once a step rather than once a substep — the environment stage **0.68** at 505,566 blocks, bit-identical, with both controls at 1.00. A probe had measured the loop's two accumulators at 35 % of the stage between them. |
| 2026-08-27 | Pass 5, iteration 2: **iteration 1's split was wrong and is corrected in place** — measured over twenty steps rather than one, conduction is 38 % of a step and the environment read 35 %, with the row fill a twentieth of it. And node reordering, the obvious idea for conduction, is refused before building: 97.8 % of links already span fewer than 1,024 node indices. |
| 2026-08-27 | Opened pass 5 on the stage no pass has moved, and its first iteration says why: **the environment pass is 46 % of a step and conduction is 33 %**, at 2.5 ns a node against 0.9 ns a link. Two earlier passes reverted layout changes aimed at the cheaper half. |
| 2026-08-27 | Pass 4, iteration 10: the block neighbour walk goes by key arithmetic as well — 0.96 to 0.99, below what the instrument resolves, kept on the sign of eight paired readings out of eight. |
| 2026-08-27 | **Closed pass 4**: the room pass is **0.36** and allocates a tenth, exposure **0.53**, the mapper's peak memory **0.51**, world load at a million blocks 1.66 → 1.32 s. The two largest wins were downstream of changes made for other reasons. |
| 2026-08-27 | Pass 4, iteration 8: the rooms are walked a run at a time as well — rooms 0.84, and **the air rebuild 0.43 on identical work**, because a room's cells now arrive contiguous along X and the walk over them is sequential. The tick-budget check caught a latent overshoot in the interior scan on the way. |
| 2026-08-27 | Pass 4, iteration 7: **the span flood is built** — the external air is walked a run at a time, 84 cells to a run, and the room pass is **0.59** at 505,566 blocks and 0.54 at 126,731. The budget check caught the first form overshooting a tick. |
| 2026-08-27 | Pass 4, iteration 6: 92 % of the air rebuild's face probes find nothing, so a bit over the grid's padded box answers first — roomair 0.89 at 505,566 blocks. Counted before it was changed, in a separate commit. |
| 2026-08-27 | Pass 4, iteration 5: the air rebuild and the room-side exposure refresh walk neighbours by key arithmetic — roomair 0.74 at 126k blocks but only 0.95 at 505k, which says the stage is bound by the dictionary probes, not the arithmetic around them. |
| 2026-08-27 | Pass 4, iteration 4: room air is canonical — sorted by node, so a room's links and its starting temperature no longer depend on the path the flood took. The span flood's precondition is met. The iteration also gave the air rebuild its first instrument, and it is **the largest stage on the load path** — 185 ms at 505,566 blocks against the room pass's 134 in a quiet window — and previously unmeasured. |
| 2026-08-27 | Pass 4, iteration 3: the room map answers from a rank over the set it already fills, so the sorted key and room arrays and the radix sort are gone. Exposure 0.45, rooms 0.89, and the pass's allocation is a tenth of what it was when the pass opened. |
| 2026-08-27 | Pass 4, iteration 2: the room cells are one store, hinted from the pass before. Half the remaining allocation, the worst pass a tenth quicker, the best unmoved — and the same commit reading 30 % apart in two windows. |
| 2026-08-27 | Opened pass 4 on the figure pass 3 ended with: the room pass allocated 253 MB per execution. Its first iteration removed 149 MB of that, and a quarter of the stage. |
| 2026-08-26 | Opened, with the first four iterations of the 2026-08-26 pass: the harness had measured unoptimised code for its whole life, and the ladder's `build` column had been measuring the census generator since `C26`. |

## Pass 2, iteration 4 — the interior scan skips visited cells a word at a time

**What was found.** A room pass is two walks over the bounding volume: the floods, which reach every
cell of air and every solid cell beside it, and then the interior scan, which walks every cell of
the box once more to find the ones no flood reached — the start of each enclosed room. By the time
the scan reaches a cell it has almost always been visited, so the scan's whole job is to skip, and
it skipped one cell at a time: half of every pass's charged work, and the half a budgeted tick
spends most of.

**What changed.** The visited bitset walks whole words while they are all ones — sixty-four cells a
step — and the scan's cursor is derived from the index it lands on, by the inverse of the order the
cursor advances in, which the first pass's `WalkingABoxInScanOrderAdvancesTheIndexByOne` holds. The
charge is per word looked at rather than per cell skipped: still cell by cell where cells are
unvisited, never more than one unit per sixty-four. **The map is the same map** —
`RoomMapSnapshotTests` runs the cell-by-cell path beside it — and the test now also asserts that the
skip removed more than half the box's walk, since a skip that skipped nothing would agree perfectly
(`E8`). `NextClearIndex` is pinned at every offset shape an off-by-one could survive the map tests
on: a clear bit at the start of a word, mid-word, past a run of full words, and none before the
bound.

**What it was worth — ticks, which is the figure `D2` is about.** The mapper's budget is counted in
the units the scan charges, so charging per word is what turns a cheaper pass into a *shorter*
one: `bench scale`, before against after, cores proven different, two rounds each:

| blocks | bounding cells | settle ticks, before | after | ratio | room map ms, before → after |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 8,904 | 68,800 | 168 | **106** | 0.63 | 15.8 → 16.7 |
| 32,800 | 328,640 | 211 | **131** | 0.62 | 43.0 → 10.5 / 42.2 |
| 126,731 | 1,499,616 | 794 | **438** | 0.55 | 70.5 → 43.7 |
| 505,566 | 6,838,104 | 3,482 | **1,890** | 0.54 | 368.6 → 313.5 |

The tick count is deterministic — identical between rounds on each side — and it is the count a
world waits on a stale exposure map after a block is placed ([backlog.md](backlog.md) `D2`). The
`+1 spike` column, the worst tick after one block is placed, fell from 145.5 to 96.3 ms at 505k for
the same reason: the first tick of the remap does more of the scan. The milliseconds per pass are
the ladder's and are read for direction only; the stage instrument's figure is this:

| blocks | rooms, before | after | ratio | units charged, before → after |
| ---: | ---: | ---: | ---: | ---: |
| 126,731 | 50.49 ms | **43.60 ms** | 0.86 | 3,072,469 → 1,622,649 |
| 505,566 | 286.22 ms | **258.39 ms** | 0.90 | 13,720,674 → 7,216,527 |

`bench stages --stages rooms`, before against after, interleaved, two rounds, fastest kept. The
milliseconds move by a tenth because the scan was already the cheap half per cell; the *units* halve,
and the units are what a tick's budget is spent in, which is why the ticks halved and the
milliseconds did not.

## Pass 2, iteration 5 — the environment pass reads one row per node

**What was tried.** The pass read four parallel arrays per node every substep — exposed faces,
the radiation coefficient, the convection row and the source row — beside the temperature it reads
and the watts it writes. One `EnvironmentRow` struct per node puts the four in one array: one
stream and one bounds check where there were four of each, with the fill writing the last two
fields once a step. Same arithmetic in the same order, so the five bit-identity suites and the
byte-identical scenarios pass unchanged. The first pass's iteration 6 tried the same layout on the
*link* side and measured nothing; the node side is the gather the loop actually pays for, which is
why it is worth asking again. Judged on `bench stages --stages solver`, the settled step alone.

**What it measured, and why it is dropped.** Before against after, cores proven different,
interleaved, two rounds, twenty steps a repeat, fifteen repeats:

| hull | step, before | after | ratio |
| --- | ---: | ---: | ---: |
| 126,731 blocks, 24 substeps | 18.50 ms | 18.40 ms | 0.99 |
| 505,566 blocks, 28 substeps | 88.94 ms | 88.24 ms | 0.99 |

Inside the instrument's own floor, with the substep count identical on both sides. Together with
the first pass's link rows this is a finding about the loop rather than about a layout: **neither
side's stream layout is what the substep loop pays for.** The link streams and the node rows are
both sequential and prefetched either way; what is left is the arithmetic, the two gathers of a
temperature per link, and the stores — and the work on the elements is already about two
nanoseconds each at half a million blocks. Reverted in the same branch (`M5`); a change that
measures as nothing is not left in.

## Pass 2, iteration 6 — a one-cell block is built without walking its cells

**What was found.** With the instrument grown two stages, placing a block read **250 ns** at
505,566 blocks — four times registering it — and a one-cell block's construction ran an iterator
over its one cell, re-anchored a rotated box that cannot move off its corner, and rotated surface
bits that are the same on every face through six face lookups.

**What changed.** The one-cell path writes the cell — the block's minimum corner in every
orientation — and rotates only what a rotation can change: a state uniform across all six faces is
returned as it is, since a rotation permutes faces. The cell walk stays as a public method and
`BlockInstanceOneCellTests` holds the two together over all twenty-four orientations for a solid
block, a partly mounted one, and a door shut and open — the uniform short cut is taken by the first
and cannot be by the second, so both sides of it are on the fixture.

**What it was worth.** `bench stages --stages place,register`, before against after, cores proven
different, interleaved, two rounds, fastest kept; register is the control, since nothing in this
change touches it:

| blocks | place, before | after | ratio | register (control), before → after |
| ---: | ---: | ---: | ---: | ---: |
| 126,731 | 23.31 ms | **14.84 ms** | 0.64 | 7.08 → 5.83 / 7.34 ms |
| 505,566 | 109.59 ms | **90.84 ms** | 0.83 | 26.49 → 24.02 ms |

What is left of placing a block at the large rung is the grid's own index — three dictionary
operations a block — and the block's allocations, which iteration 9 takes one of.

## Pass 2, iteration 7 — the flood's box test is one compare on the axis that moved

**What was found.** Every flood step tested each of six neighbours against all six edges of the box
after constructing the neighbour, when the cell it stepped from is inside the box by construction
and only the axis a face moves along can leave it.

**What changed.** One compare per face, against that edge, before the neighbour is built. The face
order the switch assumes is pinned beside the map tests (`FaceOffsetsAreTheOrderThisSwitchAssumes`),
because a reordering of `Face.Offsets` would pass every map test on a hull whose flood never met an
edge from the wrong side; and a small closed shell, whose padded box the flood meets on every edge
in both directions of every axis, maps the same by both paths.

**What it measured, and why it is dropped.** `bench stages --stages rooms`, before against after,
cores proven different, interleaved, two rounds:

| blocks | rooms, before | after | ratio |
| ---: | ---: | ---: | ---: |
| 126,731 | 46.61 ms | 57.11 ms | **1.23** |
| 505,566 | 245.08 ms | 317.51 ms | **1.30** |

Slower, by a quarter, in all four pairs, on identical work. Six compares against a box in one
expression are six well-predicted branches the JIT keeps in registers; a six-way `switch` that
writes an out-parameter on every path is a jump table and a store per face, and the flood makes
that choice eighty million times a pass. The change is reverted in the same branch (`M5` in the
other direction: a change that is measurably worse is a defect, whatever it was meant to save), and
the two pins written for it — the face order and the closed shell — stay, because they were
statements about the flood that were worth making anyway.

## Pass 2, iteration 8 — the freeze walks a bitset in key order instead of sorting

**What was found.** `bench spike` splits the worst tick after a block is placed by stage, and at
505,566 blocks it is **91 ms, of which 80 is the room map on one tick** — the tick the pass
publishes on. Measured at the commit before iteration 4 as well, to be sure the charging change had
not made it: 81.4 ms there. The mapper is budgeted per cell for the whole of its walk and then, when
the walk is done, sorts every room cell's key in one call — 1.2 million of them — so the one
unbudgeted call in the pass lands on the frame a player was already waiting on.

**What changed.** Room cells are marked in a bitset over the search box as the flood reaches them,
and the freeze walks that bitset in index order. Box-index order is `GridMath.Key` order — both are
z, then y, then x, and a key is `z·2^42 + y·2^21 + x`, monotone in that order for any coordinate a
grid can hold — so the keys come out sorted and the arrays need no sort: one dictionary lookup per
room cell for its room, and a word-skipping walk for the order. **The order claim is checked, not
trusted**: the freeze verifies the keys strictly increase and falls back to sorting if they do not,
recording that it did, and `RoomMapFreezeTests` asserts the fallback is never taken on a hull with
tens of rooms. The bitset walk is pinned at word edges against the cells that were added, in key
order. The bitset is retained per map at an eighth of a byte a bounding cell.

**As first written it was slower, and the pairing said so.** The walk derived each room cell's
coordinates from its index by two long divisions and found each set bit by shifting one at a
time — 1.2 million cells' worth of both — which cost more than the sort it replaced: rooms 252 →
320 ms and the publish tick 80 → 155 ms at 505,566 blocks, in all four pairs. The walk is per word
now: one coordinate derivation per sixty-four cells, an increment with row and plane wraps per set
bit, and a de Bruijn multiply to find each bit without a loop (the game's framework and whitelist
offer no intrinsic for it).

**And corrected, it still lost to the sort — which is the finding.** Rooms 258.9 → 256.5 ms at
505k, level, and the publish tick **80 → 155 ms**. The walk needed one dictionary probe per room
cell to learn its room, and 1.2 million probes into a 1.2 million-entry table are 1.2 million cache
misses; the sort's input was the same dictionary *enumerated*, which is sequential. So the third
form keeps the enumeration and replaces the comparison sort with a radix sort on the offset of
each key from the box's first key — eleven-bit digits, passes that skip themselves where every
digit is zero, two scratch arrays dropped after. The bitset, its walk and the bit finder went with
the version they served (`D2`'s defect class in reverse: code that measured worse is not left in).
`RoomMapFreezeTests` holds the radix order against `Array.Sort` on random keys spanning every digit,
with the rooms carried along, and on sorted, reversed and wide inputs.

**What it was worth, in the third form.** `bench stages --stages rooms`, before against after,
cores proven different, interleaved, two rounds, fastest kept; and `bench spike` for the tick the
change is about:

| blocks | rooms, before | after | ratio | publish tick, before → after | worst tick after a placement |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 126,731 | 47.18 ms | **40.04 ms** | 0.85 | 16.4 → **12.1 ms** | 31.7 → 29.0 ms |
| 505,566 | 246.17 ms | **222.80 ms** | 0.90 | 80.1 → **57.7 ms** | 91.3 → 69.6 ms |

The sort was a third of the pass at 505k and is a tenth now. What is left of the publish tick is the
enumeration, the radix passes' own memory traffic, and the portal and venting work that follows.

## Pass 2, iteration 9 — a block's two surface layers share one array unless it is a door

**What was found.** A `BlockInstance` holds its live surface bits and its structural ones in two
arrays, and the two differ only on a door that stands open. Every other block — and every door
while shut — allocated the same bits twice: a third of a block's surface allocations, on the load
path and on every paste.

**What changed.** The two are one array whenever they cannot differ; a refresh still allocates
afresh, so `ThermalSimulation.RefreshBlock`'s old references remain the snapshot it compares
against. Nothing writes into either array after construction — checked by reading every reader of
`SelfSurfaces` and `StructuralSurfaces`. `BlockInstanceOneCellTests` pins when the layers share,
when they do not, and that a door opening leaves the structural layer where it was.

**What it was worth.** `bench stages --stages place,register`, before against after, cores proven
different, interleaved, two rounds, fastest kept:

| blocks | place, before | after | ratio | register (control) |
| ---: | ---: | ---: | ---: | ---: |
| 126,731 | 19.74 ms | **17.10 ms** | 0.87 | 7.07 → 6.37 / 7.93 ms |
| 505,566 | 107.30 ms | **91.09 ms** | 0.85 | 25.26 → 23.61 ms |

One allocation fewer per block, and a third less surface memory per block — the memory table at
the pass's end carries the retained figure.

, with its start figures taken by the instrument the pass begins by putting in the tree. |
| 2026-08-26 | Opened, with the first four iterations of the 2026-08-26 pass: the harness had measured unoptimised code for its whole life, and the ladder's `build` column had been measuring the census generator since `C26`. |

## Pass 2, iteration 10 — what the pass moved

The pass's first commit with the instrument in the tree (`3296cfd`) against its tip, built the same
way, interleaved in one window, each stage on its own clock, fastest of fifteen, two rounds;
the world load against the true start (`84e3be5`). The step is the control: nothing kept in this
pass touches it.

| stage, 505,566 blocks | start | tip | ratio | what did it |
| --- | ---: | ---: | ---: | --- |
| surfaces | 84.2 ms | **75.2 ms** | 0.89 | fewer allocations per block (9) |
| links | 177.6 ms | **147.5 ms** | 0.83 | the one-cell neighbour path (2) |
| rooms | 285.8 ms | **222.5 ms** | 0.78 | the word skip (4) and the radix freeze (8) |
| exposure | 92.3 ms | **57.9 ms** | 0.63 | the one-cell exposure path (3) |
| a settled step | 89.6 ms | 89.6 ms | **1.00** | nothing — the control |
| **World load, 1,000,294 blocks** | | | | |
| placing and registering | 648 ms | **500 ms** | 0.77 | iterations 6 and 9 |
| `RebuildAll` | 2,519 ms | **1,713 ms** | 0.68 | iterations 2, 3, 4, 8 |
| whole load | 3,167 ms | **2,213 ms** | **0.70** | |
| **Memory, 126,731 blocks** | | | | |
| retained | 797 B/block | **765 B/block** | 0.96 | one surface array per block (9) |
| peak | 999 B/block | **993 B/block** | 0.99 | |
| **Ticks for a room pass to converge, 505,566 blocks** | 3,482 | **1,890** | 0.54 | the word skip (4) |
| **Worst tick after a placement, 505,566 blocks** | 93.0 ms | **69.6 ms** | 0.75 | the radix freeze (8) |
| **The suite** | 2,014 cases | 2,016 cases, 1 m 21 s | | |

**One row is not claimed.** At 126,731 blocks the surface stage read 8.4 ms at the start and 13.3
at the tip, both rounds, while at 505,566 it improved; nothing in the pass touches
`SurfaceMap.Rebuild`, and the tip's run has two more stages before it that leave the heap in a
different state. It is recorded as unexplained rather than as a regression or a saving, and it is
the first thing the next pass should measure on its own, with the stage list held to one.

**What the pass learned that the first one did not know.**

* **The substep loop is not layout-bound.** Two AoS layouts, one per side of the loop, both inside a
  one-per-cent floor. At about two nanoseconds an element the loop is at the floor of this design,
  and the way down is structural — activity tracking, `D1` — not a cheaper stream.
* **A jump table can lose to six compares** on a path taken eighty million times a pass, by a
  quarter. Measured, not reasoned.
* **A sort of sequential input beats a walk with a random probe per element**, even when the walk
  is linear and the sort is not; and the cure was a better sort, not no sort.
* **The tick a pass publishes on is where its unbudgeted work lands**, and `bench spike`'s per-stage
  worst tick is the instrument that sees it; the stage's total milliseconds did not.
* **Charging per word is what turned a cheaper scan into a shorter pass.** The budget is spent in the
  units the scan charges, so the ticks halved where the milliseconds moved a tenth (`D2`).

**Where pass 3 starts.** Rooms ~223 ms, links ~148, surfaces ~75, exposure ~58, place ~92 at
505,566 blocks. The flood is what is left of the room pass — every air cell of a box fourteen times
the block count, six faces each — and only fewer cells can move it now.

---

# Pass 3 — 2026-08-27

Started from `a81ee0b`, the tip pass 2 left. Its first act is the same as pass 2's: fix the
instrument, because pass 2 ended owing an explanation for a figure it could not account for.

## Pass 3, iterations

| # | Subject | Verdict | Where |
| ---: | --- | --- | --- |
| 1 | The stage lab settles between stages and reports what each allocated | **kept** — and `C4` is asserted now | [Iteration 1](#pass-3-iteration-1--the-stage-lab-settles-between-stages) |
| 2 | The flood reads a cell's own sealing byte once, not once a face | *measuring* | [Iteration 2](#pass-3-iteration-2--the-flood-reads-a-cells-sealing-byte-once) |
| 3 | A room-membership bit in front of the search `IsExternal` makes | *measuring* | [Iteration 3](#pass-3-iteration-3--a-room-membership-bit-in-front-of-the-search) |
| 4 | A pair's touching face is found once, and the link list is sized | *measuring* | [Iteration 4](#pass-3-iteration-4--one-touching-face-per-pair-and-a-sized-link-list) |
| 5 | One box index for both of the sets `IsExternal` reads | *measuring* | [Iteration 5](#pass-3-iteration-5--one-box-index-for-both-sets) |
| 6 | A neighbour's key is the cell's key plus a constant | **kept** — surfaces 0.51 | [Iteration 6](#pass-3-iteration-6--a-neighbours-key-is-the-cells-key-plus-a-constant) |
| 7 | A block carries its grid slot, and a dictionary goes | *measuring* | [Iteration 7](#pass-3-iteration-7--a-block-carries-its-grid-slot) |
| 8 | The flood's frontier is a ring buffer kept between passes | *measuring* | [Iteration 8](#pass-3-iteration-8--a-retained-ring-frontier) |
| 9 | The neighbour walk reports the face it found a neighbour across | **kept** — links 0.80 | [Iteration 9](#pass-3-iteration-9--the-walk-reports-the-face-it-found) |
| 10 | The allocation counter is per thread, not per process | **kept** — the check was wrong in the one place it mattered | [Iteration 10](#pass-3-iteration-10--the-allocation-counter-is-per-thread) |

## Pass 3, iteration 1 — the stage lab settles between stages

Pass 2 ended owing an explanation: at 126,731 blocks the surface stage read 8.4 ms at its start and
13.3 at its tip, in both rounds, and nothing in that pass touched the surface map. The cause is the
instrument. `place` constructs a block instance and a grid entry per block, fifteen repeats over —
two million objects — so every stage the list ran after it measured a different heap.

The lab collects twice between stages now, and reports **what one execution of each stage
allocated**, which is the column that makes such a row explainable rather than odd. The figure is
also a check: a settled step of a census hull must allocate under four kilobytes, which is `C4` —
*nothing allocates on the stepping path* — asserted rather than read off a benchmark. For a rule
whose correct value is zero, a report was never a check.

*Measured alone, the surface stage reads 7.1 ms at 126,731 blocks on the pass-2 tip against the
8.4 ms that pass recorded, and its own spread across rounds on that code is 7.1 to 13.5 ms — so the
row pass 2 could not explain was the instrument's, and this is the correction (`E10`).*

## Pass 3, iteration 2 — the flood reads a cell's sealing byte once

`Reaches` re-read `sealing[index]` — the same byte for all six faces of the cell it was asked about —
on every face, and took the neighbour as an out-parameter the caller had already built. The byte is
read once per cell now, the sealing test that can reject without touching the box runs first, and the
interior flood asks `IsStructureAt` with the index it already holds. Faces in the same order, so a
room's cells arrive in the same order, so its air links and the sum over them are bit for bit what
they were — which is the constraint every change to this flood is under.

## Pass 3, iteration 3 — a room-membership bit in front of the search

Exposure asks `IsExternal` once per unsealed face of every block, and `IsExternal` answered by binary
searching every room cell on the grid — about twenty dependent loads through 1.5 million keys at half
a million blocks — to learn what is nearly always *in no room at all*. A bit per bounding cell
answers that outright; the search stays for callers that want the room's index. The check was written
first, passes against the old code, and was proven to fail on an inverted membership test.

## Pass 3, iteration 4 — one touching face per pair, and a sized link list

The link builder tested for a touching face and then called `CountContactFaces`, which found it
again: a million redundant box overlaps at half a million blocks, on the full rebuild and the
incremental one alike. And the link list was doubled into from empty, which on a million links is
several million struct copies of pure growth; it is sized at two a block first.

## Pass 3, iteration 5 — one box index for both sets

The solid set and the room-membership set cover the same box, so a cell has one index in both, and
`IsExternal` was deriving it twice — three subtractions, six compares and two multiplies, per
unsealed face, per block.

## Pass 3, iteration 6 — a neighbour's key is the cell's key plus a constant

**A key is a sum of the components rather than a packing of bit fields**, so it is linear:
`Key(v + d) == Key(v) + Key(d)`, across zero and across a negative component alike. The surface
rebuild converted a cell to a key seven times per cell and back once; it snapshots keys and self
states now, adds a per-face constant for each neighbour, and writes each cell's derived half exactly
once rather than asking the dictionary for what it had just put there.

**What it was worth.** `bench stages --stages surfaces`, alone and first, before against after,
cores proven different, two rounds, fastest kept:

| blocks | surfaces, before | after | ratio |
| ---: | ---: | ---: | ---: |
| 126,731 | 7.14 ms | **7.11 ms** | 1.00 |
| 505,566 | 52.32 ms | **26.50 ms** | **0.51** |

At the small rung the stage fits in cache either way and the probes are not what it pays for; at the
large one it halves. The *before* leg also swings 52 to 80 ms between rounds where the after leg
reads 26.5 twice — the dictionary path is the sensitive one, which is the same fact from the other
side.

## Pass 3, iteration 7 — a block carries its grid slot

`GridModel` held a second dictionary, from block key to list slot, beside its cell index: an insert
per block at load, a probe per removal, and about thirty bytes a block, to answer what the block can
hold in four. The block carries it now on the same terms as `NodeIndex` — a hint the grid verifies
before use — and `GetByKey` reads the cell index, which answers the same question because a block's
key is the key of a cell it occupies.

## Pass 3, iteration 8 — a retained ring frontier

A pass enqueues every air cell of the bounding volume — 6.6 million at half a million blocks — into
a `Queue` grown from empty, so about twenty reallocations and thirteen million struct copies went on
growth every pass, having already been that large the pass before. The frontier is an array-backed
ring the mapper keeps. What a retained buffer risks is a pass reading what the last one left, so that
is the pin: the same hull mapped after a flood over a box forty cells larger in every direction
publishes the same map.

## Pass 3, iteration 9 — the walk reports the face it found

The neighbour walk iterates faces to find neighbours and threw the face away, so the link builder
asked `ContactFace` to work it out again from two boxes. It reports it now, through an overload
rather than a change to `IBlockAdjacency` — a port an adapter outside this repository implements
(`W2`) — and a host supplying its own adjacency still gets the worked-out face.

## Pass 3, iteration 10 — the allocation counter is per thread

`C4`'s new assertion — a settled step allocates nothing — **passed alone and failed in the suite**,
reading half a megabyte. `GC.GetTotalAllocatedBytes` counts the whole process, and the suite runs
eight classes at once, so the stage's delta collected whatever the other seven allocated meanwhile.
`GC.GetAllocatedBytesForCurrentThread` is the figure the lab wanted. A check that is right in
isolation and wrong under the conditions it will actually run in is the worst shape available, and
it is worth recording that the suite caught it within an hour of the check being written.

## Pass 3 — what the pass moved, and the one thing it moved the wrong way

Start (`a81ee0b`) against tip, both built the same way, interleaved in one window, four rounds at
505,566 blocks, fastest kept. **The solver stage is the control** — nothing in this pass touches the
substep loop — and it reads 90.8 ms against 89.6, flat within 1.5 %, which is what says the rest of
the column can be read.

| stage, 505,566 blocks | start | tip | ratio |
| --- | ---: | ---: | ---: |
| surfaces | 55.4 ms | **27.5 ms** | **0.50** |
| place | 67.3 ms | **52.0 ms** | 0.77 |
| links | 105.4 ms | **84.4 ms** | 0.80 |
| exposure | 49.4 ms | **46.5 ms** | 0.94 |
| register | 17.6 ms | 18.7 ms | 1.06 |
| rooms | 192.9 ms | 207.2 ms | 1.07 — *inside this stage's floor; see below* |
| a settled step (control) | 90.8 ms | 89.6 ms | 0.99 |
| **Memory, 126,731 blocks** | | | |
| `GridModel` indexes | 77 B/block | **43 B/block** | 0.56 |
| `BlockInstance` | 208 B/block | 216 B/block | 1.04 |
| retained | 765 B/block | **739 B/block** | 0.97 |
| **Worst tick after a placement, 505,566 blocks** | 69.6 ms | **49.4 ms** | 0.71 |

**The room pass read seven per cent worse, and that reading does not survive being attributed.**
This section said for half an hour that the regression was the finding and named iteration 3 as the
suspect — a membership bit set 1.5 million times inside the room pass to make exposure cheaper.
Measured at each of the pass's commits instead of reasoned about, best of fifteen, two rounds each
at 505,566 blocks:

| commit | rooms, round 1 | round 2 | best |
| --- | ---: | ---: | ---: |
| `a81ee0b` start | 201.6 ms | 196.2 ms | 196.2 |
| iteration 2 | 208.1 | 208.9 | 208.1 |
| iteration 3 | 201.9 | **186.2** | 186.2 |
| iteration 5 | 195.7 | 209.0 | 195.7 |
| iteration 8 | 220.4 | 208.1 | 208.1 |

**The spread within one commit is as large as the difference between any two** — 186 to 202 at
iteration 3, 196 to 202 at the start — so nothing here is convicted and iteration 3, the accused,
reads *faster* than the commit before it. The stage's floor at this size is about seven per cent,
which is exactly the size of the end-to-end reading, so the honest statement is that **the room
pass did not measurably move in this pass, in either direction** (`M5`, applied to my own claim,
corrected in place per `E10`).

**Why that stage is the noisy one is now visible, and it is the next pass's opening.** The
allocation column added in iteration 1 says the room pass allocates **253 MB per execution** at
505,566 blocks — the per-room cell lists, the cell-to-room dictionary, the frozen arrays and the
radix scratch — against links and exposure and a settled step, which allocate nothing at all. A
stage whose cost is dominated by allocation and collection is a stage whose timings will keep
refusing to resolve a few per cent, and cutting that 253 MB is worth more than any instruction in
the flood. The column earned its place within the pass that added it.

## Pass 3 — what is designed and not built

**A span flood.** The room pass is what is left, and its cost is the flood: every air cell of a box
fourteen times the block count, six faces each. Counted on this hull (a structural count, not a
timing): at 505,566 blocks the external air is **5,103,739 cells in 60,681 maximal x-runs — 84 cells
a run** — and the room air is 1,503,815 cells in 22,445 runs. A flood that enqueued *runs* rather
than cells would push about eighty thousand entries where this one pushes six and a half million,
and could mark whole words of the visited set at a time.

**And it is not the first thing to try any more.** The allocation figure above says the room pass
is bound by what it allocates before it is bound by how many cells it walks, so a pass aimed at this
stage should cut the 253 MB first and count cells second.

**What stops a span flood being this pass's work is bit-identity, and it is worth writing down.** A span flood
reaches a room's cells in a different order; `BuildRoomLinks` walks a room's cells to build its air
links, and `AccumulateRoomAir` sums over those links in order — so a different cell order is a
different sum order and a different last bit on a temperature. Landing it means first making the air
links canonical (sorting them by node index), which is itself a change that moves the last bits once
and needs the byte-identical scenario baselines re-recorded in its own commit. That is a designed
change with a measured prize, and it belongs to a pass that can start with it rather than reach it
ninth.

## Pass 4, iterations

Pass 3 ended by naming this pass's subject: the room pass allocates 253 MB per execution where every
other stage allocates nothing, and a stage bound by allocation is a stage whose timings will not
resolve a few per cent. So this pass counts bytes first and instructions second, and the iteration
table carries an allocation column for that reason.

| # | Subject | Verdict | Where |
| ---: | --- | --- | --- |
| 1 | The room map's cell-to-room dictionary is gone, not merely rebuilt | **kept** — rooms 0.75, and 149 MB of the 253 with it | [Iteration 1](#pass-4-iteration-1--the-room-maps-cell-to-room-dictionary-is-gone) |
| 2 | Every room's cells in one store, sized from the pass before | **kept** — another 50 MB, and the worst pass 0.89; the best did not move | [Iteration 2](#pass-4-iteration-2--every-rooms-cells-in-one-store) |
| 3 | The map answers from a rank, so the sorted arrays and the sort go | **kept** — exposure **0.45**, rooms 0.89, another 28 MB | [Iteration 3](#pass-4-iteration-3--the-room-map-answers-from-a-rank) |
| 4 | A room's air is a function of the room, not of the flood's path | **kept** — for the property, at a cost the instrument cannot resolve; and it found the load path's largest stage, unmeasured | [Iteration 4](#pass-4-iteration-4--a-rooms-air-is-a-function-of-the-room) |
| 5 | The air rebuild walks neighbours by key arithmetic | **kept** — roomair 0.74 at 126k, 0.95 at 505k, and the gap says what the stage is bound by | [Iteration 5](#pass-4-iteration-5--the-air-rebuild-walks-neighbours-by-key-arithmetic) |
| 6 | A bit in front of the probe, for the nine faces in ten that hold nothing | **kept** — roomair 0.89 at 505k, 0.92 at 126k | [Iteration 6](#pass-4-iteration-6--a-bit-in-front-of-the-probe) |
| 7 | The external air is walked a run at a time | **kept** — rooms **0.59** at 505k, **0.54** at 126k | [Iteration 7](#pass-4-iteration-7--the-external-air-is-walked-a-run-at-a-time) |
| 8 | The rooms are walked a run at a time too | **kept** — rooms 0.84, and **roomair 0.43** on identical work | [Iteration 8](#pass-4-iteration-8--the-rooms-are-walked-a-run-at-a-time-too) |
| 9 | What the pass moved, measured against its own start | the summary below | [Iteration 9](#pass-4--what-the-pass-moved) |
| 10 | Block neighbours by key arithmetic too | **kept**, but below what the instrument resolves — 0.96–0.99, and the sign is the evidence | [Iteration 10](#pass-4-iteration-10--block-neighbours-by-key-arithmetic-too) |

## Pass 4, iteration 1 — the room map's cell-to-room dictionary is gone

`RoomMap` held every room cell twice: once in the room's own list, and once as a key in
`Dictionary<long, int> roomIndexByCell`. At half a million blocks that second copy is **1.5 million
hash inserts taken during the flood** — inside the hot loop, growing and rehashing as it goes — and
pass 2 had already established that a probe of it per cell is a cache miss per cell.

What makes it removable is not a cheaper structure but a fact about when it is read: **nothing asks
a room map which room a cell is in while the pass that builds it is running.** The flood asks the
membership *bitset* (iteration 3 of pass 3), never the index; the solver, the audit and exposure all
ask after `Freeze()`. The dictionary was written 1.5 million times, read zero times, and then
enumerated once at the end to build the sorted arrays that answer every real query.

So it is deleted. `Freeze()` builds `frozenKeys`/`frozenRooms` by walking the surviving rooms —
which hold the same cells, with each cell's room already known by which list it is in — and radix
sorts them, the same sort iteration 8 of pass 2 measured. `RoomAt` returns −1 before a freeze, which
is the honest answer and is now the documented contract: *a working map is private until it is
published*. `DropEmptyRooms` no longer renumbers anything, because there is no longer a second table
of indices to keep in step with the first.

**What it was worth.** `bench stages --stages rooms,exposure`, before against after, the two core
DLLs proven different, two rounds, best of fifteen within a round, fastest round kept. **Exposure is
the control**: it runs on the frozen map, this change does not touch how it reads, and it must not
move.

| stage, blocks | before | after | ratio |
| --- | ---: | ---: | ---: |
| rooms, 126,731 | 45.31 ms | **35.57 ms** | **0.79** |
| rooms, 505,566 | 257.99 ms | **193.21 ms** | **0.75** |
| exposure, 126,731 (control) | 13.17 ms | 11.42 ms | 0.87 |
| exposure, 505,566 (control) | 52.55 ms | 51.55 ms | 0.98 |
| **allocated, one room pass** | | | |
| 126,731 | 34,844 KB | **17,717 KB** | **0.51** |
| 505,566 | 258,882 KB | **106,095 KB** | **0.41** |

Cell-visit counters are identical on both legs at both sizes (1,622,649 and 7,216,527), which is
what says the two legs did the same work; the scenario baselines are byte-identical, which is what
says they got the same answer.

**Read the allocation rows before the millisecond rows.** A quarter of a gigabyte per execution
became a hundred megabytes, and the timing moved by a quarter — that is the shape pass 3 predicted,
and it is also why this stage's *spread* is still 178 % at the large rung: 104 MB is still 104 MB.
The remaining bytes are the per-room cell lists and their doubling copies, the frozen arrays, and
the radix scratch, in that order of size, which is the order the rest of this pass takes them in.

## Pass 4, iteration 2 — every room's cells in one store

A room was a `List<Vector3I>` of its own. The flood fills one room to exhaustion before it opens the
next — only the current room is ever added to — so a room's cells are **contiguous by
construction**, and a room can be a start and a length into one shared array instead. `AddToRoom`
refuses any room but the open one, because the failure that would otherwise follow is silent: a cell
filed under a closed room's index lands at the end of the store and is read as the current room's.

What that buys is not the room objects. This hull has a few hundred rooms, not thousands. It is that
**a rebuild can be told its size in advance**: the pass before it found a number of room cells, and a
hull that gained or lost a block finds very nearly the same number, so `HintRoomCells` sizes the
store once and the pass neither doubles into it nor trims it back. A list per room could not be
hinted, because the flood does not know how large any *one* room will be until it has finished it.

**What it was worth.** Same instrument, same rules, exposure as the control:

| stage, 505,566 blocks | before | after | ratio |
| --- | ---: | ---: | ---: |
| rooms, best of 15 | 149.47 ms | 146.31 ms | 0.98 — *inside the floor* |
| rooms, **worst of 15** | 445.87 ms | **400.60 ms** | **0.90** |
| rooms, worst of 15 (round 2) | 445.45 ms | **388.88 ms** | **0.87** |
| rooms, spread | 198 % | **174 %** | |
| exposure (control) | 38.12 ms | 36.76 ms | 0.96 |
| **allocated, one room pass** | 106,095 KB | **54,616 KB** | **0.51** |
| allocated, 126,731 blocks | 17,717 KB | **10,026 KB** | 0.57 |

**The best case did not move and the worst case did, and that is the finding.** Iteration 1 cut
allocation by 59 % and the stage got a quarter faster; iteration 2 cut it by another 49 % and the
stage did not move at all. The difference is that iteration 1 also removed 1.5 million hash inserts —
*work* — where this removes only *garbage*. Garbage does not lengthen the pass that makes it; it
lengthens whichever pass the collector happens to land in, which is why it shows up in the worst of
fifteen and in the spread rather than in the best. A load path that runs repeatedly while a player
waits is judged by its worst tick as much as its best, so this is kept — but on the tail and the
allocation column, not on a claim that the room pass got faster, which by `M5` it did not.

**A caution the two iterations together make unavoidable: milliseconds do not survive leaving their
window.** Commit `321c062` is the *after* leg of iteration 1 and the *before* leg of iteration 2. It
read **193 ms** in the first window and **149 ms** in the second — the same code, the same size, the
same instrument, thirty per cent apart. Both pairings are interleaved and both rounds within each
agree, so both ratios stand; what does not stand is any comparison of an absolute figure in one
table with an absolute figure in another. That is what `M7` means by measuring a pass against its own
start, and this is the clearest example of it the project has produced.

## Pass 4, iteration 3 — the room map answers from a rank

The published map held a sorted `long[]` of cell keys and a parallel `int[]` of rooms, and sorted
them — 1.5 million keys, by radix, on the tick that publishes the map. Twelve bytes a cell to hold
twelve bytes of answer, and a query that is a binary search: about twenty dependent loads through
twelve megabytes.

**None of that is needed, because the pass already fills a set that knows the answer's shape.**
`roomCells` is one bit per cell of the search box, marking the cells that are in rooms — the set
`IsExternal` asks first. It knows *which* cells are in rooms and, being indexed by the box's own
ordering, it already **orders** them. What it does not say is which member a given cell is. Ranking
it does: a prefix count of set bits per 64-cell word, four bytes a word, built in one pass over a
sixty-fourth of the box. Then

- the room of a cell is `roomByRank[rank(cell)]` — **four bytes a cell**, no keys held at all;
- a query is two loads and a popcount, with no search and no comparison anywhere;
- publishing is one walk over the set's words and one over the room cells. **There is no sort.**

`BitOperations.PopCount` is not on the script whitelist, so the popcount is written out as the usual
SWAR halving. The rank index is put away by any change to the set it describes, because a rank read
from a stale prefix is a plausible number that indexes the wrong thing.

**What it was worth.** `bench stages --stages rooms,exposure,solver`. Exposure cannot be the control
here — it is the biggest reader of this lookup — so **the solver stage is**, and it reads 1.01.

| stage, 505,566 blocks | before | after | ratio |
| --- | ---: | ---: | ---: |
| **exposure** | 38.20 ms | **17.06 ms** | **0.45** |
| rooms | 149.04 ms | **132.33 ms** | **0.89** |
| a settled step (control) | 80.10 ms | 80.73 ms | 1.01 |
| **allocated, one room pass** | 54,616 KB | **25,670 KB** | **0.47** |
| **at 126,731 blocks** | | | |
| exposure | 10.19 ms | **6.33 ms** | **0.62** |
| rooms | 33.67 ms | **31.74 ms** | 0.94 |
| a settled step (control) | 17.51 ms | 17.16 ms | 0.98 |
| allocated, one room pass | 10,026 KB | **4,768 KB** | 0.48 |

**Exposure more than halved, and that is where the reading is.** Pass 3's iteration 3 put a
membership *bit* in front of this search precisely because the search was expensive; what is left
after that bit are the cells that really are in rooms, and every one of them was still paying twenty
dependent loads. At half a million blocks that was more than half of the exposure refresh. The room
pass's own 0.89 is the sort no longer happening on the publish tick.

**And the ledger is closed on this pass's opening figure.** The room pass allocated **253 MB** per
execution when pass 4 began and allocates **25 MB** now — a tenth — in three changes that each
removed a structure rather than tuning one: a dictionary nothing read, per-room lists that could be
one hinted store, and sorted keys that a rank makes unnecessary. What remains is the cell store at
twelve bytes a cell and the room-by-rank at four, both of which the map is holding *because they are
its answer*, not as working space.

## Pass 4, iteration 4 — a room's air is a function of the room

`BuildRoomLinks` counts how many faces each bounding block presents to a room into a
`Dictionary<int, int>` and then enumerates it. A dictionary enumerates by insertion, so **a room's
links came out in the order the flood happened to reach its cells** — and the mean wall temperature
a new room's air starts at is a sum of floats over that same enumeration, so it did too. Two floods
agreeing exactly about which cells are in a room disagreed about how warm its air is: on the test
hull, **349.66922 forwards and 349.66916 backwards**. Not a rounding curiosity — a visible difference
in the fourth decimal of a temperature.

Sorting the contacts by node index makes both the links and the sum a function of the room's
*contents*. `RoomAirCanonicalTests` builds two maps holding the same rooms with each room's cells
offered in opposite orders and compares the resulting air bit for bit — links, conductances,
starting temperature — and both of its checks fail when the sort is removed.

**Why it is worth an iteration on its own.** This is the condition the span flood needs. A flood
that enqueues runs of cells rather than single cells reaches a room's cells in a different order,
and until now that would have moved every player's air temperatures in the last bits, which is why
[the design note](#pass-3--what-is-designed-and-not-built) said it needed the air links made
canonical first. That is done, and it was done in its own commit so that if it *had* moved a pinned
figure, the move would have been attributable to it alone. It moved none: the suite of that day — 2,027 cases — passed unchanged.

**What it cost.** `bench stages --stages roomair,rooms,solver`, with the room pass and a settled step
as controls:

| stage, 505,566 blocks | before | after | ratio |
| --- | ---: | ---: | ---: |
| roomair | 417.71 ms | 442.49 ms | 1.06 |
| rooms (control) | 141.11 ms | 140.17 ms | 0.99 |
| a settled step (control) | 112.03 ms | 109.39 ms | 0.98 |
| roomair, 126,731 blocks | 44.19 ms | 46.73 ms | 1.06 |

**Read that as "at most a few per cent", not as six.** The *before* leg alone read 437.24 and 417.71
in its two rounds — 4.7 % apart on identical code — and the first round's pairing was 1.01 where the
second's was 1.06. A difference the same leg produces against itself is not a difference between
legs (`M5`). The honest statement is that a sort of a few hundred integers per room, per air rebuild,
costs no more than a few per cent of a stage, and it buys a property that a whole class of future
change depends on.

**And the iteration found something bigger than itself.** There was no instrument for the air
rebuild at all: the stage lab measured place, register, surfaces, links, rooms, exposure and a
settled step, and `RebuildRoomAir` — which runs every time a map republishes — was in none of them.
It is a stage now, and it is **the largest single thing on the load path**, in every window it has
been measured in. It had been that all along.

*The absolute figure needs a window to mean anything, and this section first said 437 ms — the
reading in the window above, where a settled step read 112 ms. Measured again the next window, on the
same commit, with a settled step at 85.5 ms: the air rebuild reads **185 ms** against the room pass's
**134**. The machine was about 30 % slower in the first window and the air rebuild was 2.4× slower,
which is worth its own sentence: this stage is memory-bound, so it is the one that suffers most from
whatever else is running. Corrected in place, `E10`; the ratios in the table are unaffected, being
interleaved within one window.*

The
first draft of the stage timed an empty outer loop, because a room at zero pressure has no air and
therefore no links, so the rooms are filled before the clock starts — which is the same failure as
[a switch wired to nothing](#pass-2-iteration-5--the-environment-pass-reads-one-row-per-node), caught
here by the stage reporting a suspiciously small number rather than by a check.

## Pass 4, iteration 5 — the air rebuild walks neighbours by key arithmetic

`BuildRoomLinks` and `RefreshExposureAround` both walk a room's cells and ask the grid what stands
across each of the six faces. Both did it by adding the face offset to the cell and calling
`GetAtCell`, which converts a cell to a key — **seven conversions a cell**, two multiplies and three
adds each. A key is a sum of the components, so a neighbour's key is this cell's key plus a per-face
constant: one addition. This is pass 3's iteration 6 applied to the two loops it did not reach.

`GetAtKey` is `GetAtCell` without the conversion. It is deliberately *not* `GetByKey`, which answers
only for a block's lowest cell — a neighbour walk that reached for that one would find every
one-cell block and miss every multi-cell one except at its corner, a hole nothing else in the model
would report. The two are pinned apart by a test on a 3×3×3 block.

| stage | before | after | ratio |
| --- | ---: | ---: | ---: |
| roomair, 126,731 blocks | 41.09 ms | **30.58 ms** | **0.74** |
| roomair, 505,566 blocks | 184.88 ms | **175.61 ms** | 0.95 |
| rooms, 505,566 (control) | 134.30 ms | 133.42 ms | 0.99 |
| a settled step, 505,566 (control) | 85.55 ms | 84.36 ms | 0.99 |

**The gap between the two rungs is the finding, and it names the next iteration.** The same change
is worth a quarter of the stage at 126,731 blocks and a twentieth at 505,566. Arithmetic does not
get cheaper with grid size, so what grew is everything else: at half a million blocks `blocksByCell`
holds five hundred thousand entries, the six neighbour keys of a cell land in six unrelated buckets,
and the stage is waiting on memory rather than computing. Removing five conversions from a cell that
then stalls on a cache miss anyway recovers little.

That is worth stating as a measurement rather than a hunch, because it changes what to do next:
**the probes that cost are the ones that find nothing.** A room's interior cells have six air
neighbours and pay six full dictionary probes to be told so, and interior cells are most of a room.

## Pass 4, iteration 6 — a bit in front of the probe

Iteration 5 ended by saying the air rebuild is bound by the probes rather than the arithmetic around
them, and that most of those probes find nothing. **The counters were added first, in their own
commit, before the change they justify** — because "most" is not a number and the number is what
decides between making the probe cheaper and not making it:

| blocks | faces walked | faces holding a block | share |
| ---: | ---: | ---: | ---: |
| 126,731 | 1,642,800 | 139,120 | **8.5 %** |
| 505,566 | 9,022,890 | 691,306 | **7.7 %** |

So **eight and a third million of those nine million probes are a hash and a bucket chase to be told
"nothing"**. `GridModel` now keeps one bit per cell of its padded bounding box, and the walk asks
that first. The bit's index steps by a per-face constant exactly as the key does — `((z·sizeY) + y)·sizeX + x`
is a sum too — so asking costs an add and a bit test.

**Two things make it safe to hold rather than to maintain.** It is built on demand and dropped
whenever the grid changes, so a load that places half a million blocks builds it once at the end
rather than half a million times on the way; and the index step is only sound away from the box's
own boundary, which is why the box is padded — the pad ring is external air and never holds a room
cell, so no walk ever steps off an edge. Both are checked: the bit against the block table cell by
cell over a whole hull, the step against deriving the neighbour on every face of every occupied
cell, and the rebuild against placing and removing a block.

| stage | before | after | ratio |
| --- | ---: | ---: | ---: |
| roomair, 505,566 blocks | 147.65 ms | **131.64 ms** | **0.89** |
| roomair, per face walked | 16.4 ns | **14.6 ns** | |
| roomair, 126,731 blocks | 26.58 ms | **24.55 ms** | 0.92 |
| rooms, 505,566 (control) | 133.71 ms | 133.07 ms | 1.00 |
| a settled step, 505,566 (control) | 80.12 ms | 80.33 ms | 1.00 |

**Eleven per cent, not the half the hit rate might suggest, and the reason is worth keeping.**
Replacing a dictionary probe with a bit test does not replace a memory access with nothing: the
bitset over the box is nearly a megabyte at this size, and the six neighbours of a cell touch it at
six unrelated offsets. What was saved is the hash, the bucket walk and the entry read — not the
cache miss, which both structures take. A bit is a cheaper miss, not an avoided one. The stage is
still memory-bound, and the remaining lever there is to *touch fewer cells*, which is what the room
pass's own designed change is about.

## Pass 4, iteration 7 — the external air is walked a run at a time

This is the change [pass 3 designed and could not build](#pass-3--what-is-designed-and-not-built),
built. Iteration 4 removed what stopped it.

Most of a room-mapping pass is not the rooms. At 505,566 blocks the pass visits 7.2 million cells and
**5.1 million of them are the open space around the hull**, in maximal runs along X averaging **84
cells**. The cell walk paid a dequeue, an index derivation and six face tests for every one of them,
and enqueued every one of them — a frontier of millions of entries to classify a volume that is
mostly nothing.

The run walk takes a whole run per dequeue. It extends along X while neither side of the shared face
seals and the next cell is untaken; it walks the four lateral faces once per cell of the run; and it
enqueues **only the first cell of each unvisited stretch** beside the run, which is what collapses
the frontier from millions of entries to tens of thousands. External cells are counted rather than
stored, so the order they are reached in is not an output of the pass and nothing downstream can
tell the two walks apart.

**Three things had to be got right, and each is checked rather than argued.**

- *It must classify the same cells.* It is a different algorithm — a scanline fill against a
  breadth-first one — so the cell walk stays, as `SpanFlood = false`, and is the oracle.
  `RoomSpanFloodTests` holds the two against each other **cell by cell over the whole box**, on a
  compartmented fixture and on a census hull. Shortening the extension by one cell fails all three
  of its checks.
- *It must respect the tick budget.* A run on a large hull is hundreds of cells, and
  `RoomMappingNeverExceedsItsBudgetInOneTick` holds the mapper to its budget on every tick however
  large the grid — the whole value of an incremental stage. **That check caught this**: the first
  form let a run overshoot by up to the width of the box. A run now stops at what the tick has left
  and enqueues where it stopped.
- *A seed must be justified.* The walk counts any unvisited cell it dequeues as open air, so a cell
  enqueued without being shown reachable would be classified on sight. The budget-truncation
  enqueue runs the same reachability test the extension uses before enqueuing.

| stage | before | after | ratio |
| --- | ---: | ---: | ---: |
| rooms, 505,566 blocks | 134.14 ms | **79.43 ms** | **0.59** |
| rooms, 126,731 blocks | 32.12 ms | **17.22 ms** | **0.54** |
| rooms, per cell visited, 505,566 | 18.6 ns | **10.9 ns** | |
| a settled step, 505,566 (control) | 80.25 ms | 81.02 ms | 1.01 |
| exposure, 505,566 | 20.98 ms | 18.46 ms | 0.88 — *not a control; see below* |

**The work counter is not identical between the legs, and it favours the old code.** The cell walk
counts 7,216,527 cells visited and the run walk 7,279,786 — 0.9 % more. A cell beside a run can be
enqueued by each of up to four runs, and the duplicates are discarded on dequeue at a cost of one
charged cell each. So the run walk is charged for slightly more work than it does, and the per-cell
figures above understate it. Everything else the two legs produce is identical, which is what
`RoomSpanFloodTests` says.

**Exposure moved too, and it is worth naming rather than ignoring.** It reads a published map this
change does not alter, so 0.88 is not a result about exposure — it is most likely about what the
pass leaves behind. The frontier is a retained ring buffer sized to the largest it ever needed: the
cell walk drove it to millions of `Vector3I`, tens of megabytes that stay allocated for the life of
the mapper, and the run walk needs tens of thousands. A stage that walks the same map with less of
the process's memory behind it runs faster. That is a hypothesis with a measurement attached to it —
the memory rows at the close of this pass are where it is settled or dropped.

## Pass 4, iteration 8 — the rooms are walked a run at a time too

The same walk, applied to the inside of a room, with the one thing the external walk does not have
to do: a cell it reaches is either air, which joins the room, or sealed structure, which is recorded
as the room's boundary and stops the walk going that way.

**A room's cells now arrive in run order rather than in the order a queue emptied**, and that is only
safe because [iteration 4](#pass-4-iteration-4--a-rooms-air-is-a-function-of-the-room) made a room's
air a function of its contents. Before that, this change would have moved every player's air
temperatures in the last bits.

| stage | before | after | ratio |
| --- | ---: | ---: | ---: |
| rooms, 505,566 blocks | 78.74 ms | **66.02 ms** | **0.84** |
| rooms, 126,731 blocks | 16.89 ms | **14.70 ms** | 0.87 |
| **roomair, 505,566 blocks** | 126.31 ms | **53.88 ms** | **0.43** |
| roomair, 126,731 blocks | 23.89 ms | **18.52 ms** | 0.78 |
| a settled step, 505,566 (control) | 80.03 ms | 81.63 ms | 1.02 |

**The air rebuild more than halved, and it is not because anything in it changed.** Its work counter
is identical on both legs — 9,022,890 faces walked, 691,306 of them holding a block — and not a line
of `BuildRoomLinks` differs. What changed is the *order of the cells it is handed*. It walks a room's
cells and asks the grid about the six neighbours of each; in breadth-first order those cells arrive
as a shell expanding through the room, so consecutive cells are unrelated addresses in the occupancy
set and in the block table. In run order they are contiguous along X, and consecutive cells are
consecutive bits and neighbouring keys.

That is the same finding as [iteration 6](#pass-4-iteration-6--a-bit-in-front-of-the-probe) read from
the other side. That iteration made each miss cheaper and got eleven per cent; this one made the
misses *sequential* and got fifty-seven. **On a stage that is bound by memory, the order things are
visited in is worth more than what is done to each of them** — and the order was free, a side effect
of a change made for the room pass.

**Two things the budget check caught, and the second predates this change.** The run walk has to
extend through its own seed, so the interior scan no longer marks or files the cell it found when the
walk is live. And the scan's skip ran to the end of the box in one call and then charged for every
word it had looked at, so **a tick could overshoot its budget by however far the last skip happened
to reach** — nothing had made it do so, and this change did. It is capped to the words the tick can
still afford, and the cursor resumes next tick. `RoomMappingNeverExceedsItsBudgetInOneTick` found
both, four cells over and then one cell over; a check that fails by one is a check worth having.

## Pass 4 — what the pass moved

Start (`7d9838d`) against tip, both built the same way, interleaved in one window, two rounds at
505,566 blocks, fastest kept. **The settled step is the control** — nothing in this pass touches the
substep loop — and it reads 79.9 ms against 81.1, which is what says the rest of the column can be
read.

| stage, 505,566 blocks | start | tip | ratio |
| --- | ---: | ---: | ---: |
| **rooms** | 181.80 ms | **66.23 ms** | **0.36** |
| **exposure** | 40.92 ms | **21.54 ms** | **0.53** |
| rooms, allocated per execution | 258,882 KB | **25,671 KB** | **0.10** |
| rooms, per cell visited | 25.2 ns | **9.1 ns** | 0.36 |
| a settled step (control) | 81.08 ms | 79.88 ms | 0.99 |
| register (control) | 15.48 ms | 15.50 ms | 1.00 |
| place | 40.29 ms | 42.83 ms | 1.06 — *see below* |
| links | 78.19 ms | 82.50 ms | 1.06 — *see below* |
| surfaces | 22.33 ms | 25.13 ms | 1.13 — *see below* |
| **Memory, 126,731 blocks** | | | |
| `RoomMap` retained | 70 B/block | **53 B/block** | 0.76 |
| **`RoomMapper` peak** | 297 B/block | **152 B/block** | **0.51** |
| retained, whole simulation | 739 B/block | **722 B/block** | 0.98 |
| peak, whole simulation | 967 B/block | **821 B/block** | 0.85 |
| **World load, 1,000,294 blocks** | | | |
| built in | 1,657 ms | **1,319 ms** | 0.80 |
| of which `RebuildAll` | 1,326 ms | **983 ms** | 0.74 |

**Three stages read worse and none of them moved.** Place, links and surfaces are untouched by this
pass, and they read 1.06, 1.06 and 1.13. The same leg's two rounds disagree by more: the *start*
tree read place at 40.3 and 46.0 ms, and surfaces at 22.3 and 29.7 — 14 % and 33 % apart on
identical code, against spreads of 176 % and 421 % within a stage's own fifteen repeats. A
difference the same leg produces against itself is not a difference between legs. This is the rule
pass 3 had to apply to a claim of its own (`M5`), and it applies to a flattering column exactly as
it does to an unflattering one.

**The room pass is a third of what it was and allocates a tenth.** In order: a cell-to-room
dictionary that nothing read while a pass ran; per-room lists that became ranges into one store,
sized from the pass before it; sorted key and room arrays, and the sort itself, replaced by a rank
over a set the pass already fills; and then the flood itself, walked a run at a time — 84 cells to a
run in the open air around the hull — for the external air and then for the rooms.

**And the two things that were not the point are the ones worth remembering.**

*Exposure halved without being touched.* Iteration 3 replaced a binary search through twelve
megabytes of cell keys with a rank lookup, and exposure — the biggest reader of that lookup — went
from 40.9 ms to 21.5. The change was made to remove a sort from the publish tick; more than half its
value turned up in a different stage.

*The air rebuild halved for the same kind of reason, twice removed.* It was 0.43 in iteration 8 on
**identical work counters and unchanged code**, because a room's cells now arrive contiguous along X
and the walk over them is sequential rather than shell-shaped. Which was only possible because
iteration 4 made a room's air a function of its contents rather than of the path through it — a
change that measured as a cost of at most a few per cent and was kept for the property alone.
**The two largest wins in this pass were downstream of changes made for other reasons**, and neither
would have been visible without an instrument per stage.

*The frontier hypothesis from iteration 7 is settled, and it was right.* That section guessed that
exposure moved because the run walk stops driving the mapper's retained ring buffer to millions of
entries. The peak memory row says so: **`RoomMapper` peak is 297 → 152 bytes a block**, and the
whole simulation's peak 967 → 821. A pass that walks the same map with a hundred and fifty
megabytes less behind it at half a million blocks is a faster pass.

## Pass 4, iteration 10 — block neighbours by key arithmetic too

The link build is the largest stage left on the load path, and what it spends its time on is asking
the grid for each block's neighbours. The one-cell walk — nearly every block on a hull — converted a
cell to a key six times, once per candidate face, where the six candidates are the block's own key
plus six constants. The boundary walk for multi-cell blocks takes the same treatment.

| stage | before | after | ratio |
| --- | ---: | ---: | ---: |
| links, 505,566 blocks | 60.51 ms | 60.04 ms | 0.99 |
| links, 126,731 blocks | 12.94 ms | 12.37 ms | 0.96 |
| place, 505,566 blocks | 41.21 ms | 39.37 ms | 0.96 |
| place, 126,731 blocks | 10.76 ms | 9.76 ms | 0.91 |
| a settled step, 505,566 (control) | 79.99 ms | 80.16 ms | 1.00 |

**No individual figure here clears this instrument's floor, and the change is kept anyway.** The
link stage's own spread is 75–105 % and the *before* leg's two rounds differ by 4.8 % on identical
code, so 0.99 and 0.96 are not results on their own. What is a result is that **every one of the
eight paired readings — two stages, two sizes, two rounds — is lower on the tip than on the before
leg**. Eight readings agreeing in sign by chance is one in two hundred and fifty-six. The size of
the effect is not resolved; its direction is, and it points the way a strict reduction in work
should point.

That is the honest end of a lever this project has pulled four times now. `GridMath.Key` is a sum,
so a neighbour's key is an addition — and the four places that walk neighbours in a loop have all
been converted: the surface rebuild (pass 3, iteration 6, worth **0.50**), the air rebuild and the
room-side exposure refresh (iteration 5, worth 0.74 at the small rung), and now the block adjacency
walk, worth *possibly* three per cent. The lever gets smaller each time it is pulled, because what is
left is bound by the dictionary rather than by the arithmetic — which iteration 5 had already
measured and said.

## Pass 5, iterations

Pass 4 ended by naming this pass's subject: **the settled step is the biggest thing left and no pass
has moved it.** It reads about 80 ms at 505,566 blocks in a quiet window, against the room pass's 66
and the link build's 60, and passes 1, 2 and 3 all left it exactly where they found it — by design in
pass 1, and on measurement in the other two, both of which tried a layout change on the link stream
and reverted it.

| # | Subject | Verdict | Where |
| ---: | --- | --- | --- |
| 1 | A step is measured by its parts | **kept** — the instrument; its first reading was wrong and iteration 2 says why | [Iteration 1](#pass-5-iteration-1--a-step-is-measured-by-its-parts) |
| 2 | The split corrected, and the obvious idea refused | **kept** — conduction 38 %, environment 35 %, apply 20 %; and node reordering is not worth doing | [Iteration 2](#pass-5-iteration-2--the-split-corrected-and-the-obvious-idea-refused) |
| 3 | The grid's own heat gain is summed once a step, not once a substep | **kept** — environment **0.68** at 505k, bit-identical | [Iteration 3](#pass-5-iteration-3--the-grids-own-heat-gain-is-summed-once-a-step) |
| 4 | What a substep's passes cost touching memory and computing nothing | **kept** — the instrument, and `D1`'s open question answered | [Iteration 4](#pass-5-iteration-4--what-a-substeps-passes-cost-computing-nothing) |
| 5 | A buried node takes the exposed path | **dropped** — 1.30, and the reason is worth more than the change would have been | [Iteration 5](#pass-5-iteration-5--a-buried-node-takes-the-exposed-path-dropped) |
| 6 | A bit in front of the link build's probes | **kept** — links **0.88** at 505k, 0.76 at 126k | [Iteration 6](#pass-5-iteration-6--a-bit-in-front-of-the-link-builds-probes) |
| 7 | The air rebuild counts contacts in a row, not a hash table | **kept** — 0.95, below what the instrument resolves | [Iteration 7](#pass-5-iteration-7--the-air-rebuild-counts-contacts-in-a-row) |
| 8 | The apply pass reads the critical row only when it could matter | **kept** — apply 1.2 → **0.8 ns** a node | [Iteration 8](#pass-5-iteration-8--the-apply-pass-reads-the-critical-row-only-when-it-could-matter) |
| 9 | What the pass moved | the summary below | [Iteration 9](#pass-5--what-the-pass-moved) |

## Pass 5, iteration 1 — a step is measured by its parts

**Three passes have tried to make a step faster and none of them knew where its time went.** It has
been reported as one number throughout — `bench report`'s step milliseconds, `bench stages`'
`solver` row — and twice that number was used to judge a change aimed at *one part* of it: pass 1's
iteration 6 and pass 2's iteration 5 both rearranged the link stream, on the assumption that
conduction is what a step spends its time on, and both were reverted. A stage lab was built in pass
2 for exactly this reason, one level up; the step never got one.

`bench stepphases` times each stage of a step on its own clock. The stages already exist — a step is
a state machine over `Begin`, `Environment`, `Conduction`, `Coupled`, `Apply` and `Publish`, because
it is spread across frames — so the instrument is two timestamps per *slice*, of which there are a
few hundred in a step, rather than per element, of which there are millions.

**What it says** is in [iteration 2](#pass-5-iteration-2--the-split-corrected-and-the-obvious-idea-refused),
which is where the split is reported: the first reading this iteration took was of a **single** step
on a hull that had not settled, and it was wrong about which stage is largest. That is corrected
there rather than here, so a reader arrives at one set of numbers rather than two (`E10`).

**The instrument is off unless asked for, and it does not change what it measures.** The profile
costs two timestamps a slice and a branch, on a path that runs a few hundred times a step;
`StepPhaseLabTests` steps the same hull profiled and unprofiled and holds every node's temperature
equal **to the bit**, and checks that each stage is charged the elements it actually walks — nodes
for environment and apply, links for conduction, once per substep — because a stage charged
something else has a nanoseconds-per-unit figure that means nothing.

## Pass 5, iteration 2 — the split corrected, and the obvious idea refused

**Iteration 1's reading was of one step, and it was wrong.** A step is a millisecond or two of work,
and a census hull that has taken three steps has not settled — it asks for a different number of
substeps from one step to the next, which changes both the time and the element count. The first
reading put the environment pass at 46 % of a step and conduction at 33 %. Measured the way the
`solver` stage measures, over **twenty steps a repeat after twenty steps of warm-up**, the order
reverses. The lab's own guard is what caught it: the environment stage charged a different number of
visits on two repeats and it refused to average them.

**And the environment stage is two different pieces of work reported as one.** The first substep of
a step *fills* the per-node rows — the six face weights against sun and wind, solar, friction, the
convection coefficient — and the other twenty-three read them. They are now separate rows, because
"the environment pass is 46 %" answers nothing if it is mostly a fill that happens once.

**The split, at 126,731 blocks, 24 substeps, per step:**

| stage | best | share | visits | per visit | what it walks |
| --- | ---: | ---: | ---: | ---: | --- |
| **conduction** | **10.43 ms** | **38 %** | 5,936,400 | **1.8 ns** | one exchange per link, per substep |
| **environment** | **9.70 ms** | **35 %** | 2,914,813 | **3.3 ns** | radiation and convection from the filled rows, per node |
| apply | 5.41 ms | 20 % | 3,041,544 | 1.8 ns | watts into temperatures, per node |
| env fill | 1.29 ms | 5 % | 126,731 | 10.2 ns | the rows, once a step |
| publish | 0.74 ms | 3 % | 126,731 | 5.9 ns | the step's results onto the node objects |
| begin, coupled | 0.01 ms | — | 381,144 | — | zeroing, planning, loops, air and pumps |

So **conduction and the environment read are the same size**, the fill is a twentieth, and a step is
three passes over the grid per substep — nodes, links, nodes — at between 1.8 and 3.3 nanoseconds an
element. Iteration 1's conclusion that two earlier passes had aimed at the cheapest part of the step
does not survive this: conduction is the largest single stage. What does survive is that **neither
of those passes knew that**, and both judged their change on a number that mixed all six stages
together.

**The obvious next idea is refused on measurement, before building it.** Conduction gathers two
temperatures and scatters two watts per link, so its cost is set by how far apart a link's two nodes
are numbered — which makes "renumber the nodes spatially" the standard move. On this hull it is
already true: the mean index span of a link is **244** at 126,731 blocks, two thirds span fewer than
64, and **97.8 % span fewer than 1,024** — four kilobytes of a float row. The gathers land in L1
nearly every time and there is nothing to recover. `LinkSpanProbe` keeps that as a check rather than
a note, because nothing guarantees it: nodes are numbered in the order blocks are added, and a change
to how a grid is loaded could scatter them, making conduction slower for a reason no timing would
explain.

## Pass 5, iteration 3 — the grid's own heat gain is summed once a step

The environment loop carried two running float sums: the watts the grid sheds to its surroundings,
and the watts it puts into itself. **A probe deleted both and measured the stage at 0.65** — so
between them they were **35 %** of it, which is what a dependency the loop carries from one node to
the next costs when the body is three nanoseconds long.

One of the two is removable outright. Waste heat, solar gain and friction are read out of
`nodeSourceRow`, which one substep fills and twenty-three read, so summing it every substep computed
the same float twenty-four times. It is summed once now, and the sum is over the same values in the
same order, so the published figure is unchanged **to the bit**.

**The check found the bug the same hour it was written.** The first form primed the accumulator at
the top of a substep from the stored total — and the plan is resolved *after* that point and can
invalidate the rows, because the sun moving refreshes the shadow map. A substep primed with a total
it then re-summed counted it twice, and `HeatGainHoistTests` reported the figure doubled on the
first step it took. The total is settled at the end of the pass instead, exactly where a substep
that re-summed would have reached, so what runs after it — the point sources — adds onto the same
total in the same order.

| stage | before | after | ratio |
| --- | ---: | ---: | ---: |
| **environment, 505,566 blocks** | 33.41 ms | **22.76 ms** | **0.68** |
| environment, per node visit | 2.4 ns | **1.7 ns** | |
| conduction, 505,566 (control) | 27.05 ms | 27.00 ms | 1.00 |
| apply, 505,566 (control) | 14.64 ms | 14.59 ms | 1.00 |
| environment, 126,731 blocks | 6.86 ms | **5.12 ms** | 0.75 |

At half a million blocks the controls read 1.00 and 1.00, which is what says the 0.68 is the change
and not the window. The other accumulator stays: it is a function of temperature and cannot be
hoisted, and breaking its dependency with partial sums would change a published figure's last bits
to buy about four per cent of a step — which is not a trade this project makes for that price.

## Pass 5, iteration 4 — what a substep's passes cost computing nothing

[backlog.md](backlog.md) `D1` asks whether what is left of a step is near the memory-bandwidth
floor and records it as an **open question** — because the last time it was answered, two passes
were reaching through the node objects instead of the flat rows, so the answer was about something
that no longer exists. `bench stepfloor` answers it by measurement: the same rows, at the same
sizes, walked in the same pattern, doing the least arithmetic that still forces every load and store
to happen. The link indices are a real grid's, because the whole question for conduction is where
its gathers land and a synthetic index stream would answer about itself.

| pass | floor | measured | over floor | what the floor moves |
| --- | ---: | ---: | ---: | --- |
| environment | 0.81 ns | 1.84 ns | **2.26×** | four rows in, one out |
| apply | 0.81 ns | 1.08 ns | **1.32×** | temperature in and out, watts, mass, critical |
| conduction | 1.26 ns | 1.07 ns | **0.85×** | two index rows, a conductance row, two gathers, two scatters |

**Conduction is its own floor, and the 0.85 is not a paradox.** The real loop is *faster* than a
loop that does its loads and stores and no arithmetic, because it skips the pair whose ends agree
before touching either. There is no arithmetic in it to remove: one subtract and one multiply
against six memory accesses. Whatever a step's remaining cost is, it is not conduction's
instructions — which is the answer `D1` was owed, and it is the opposite of what two passes assumed
when they rearranged that loop's data.

**The environment read is where headroom shows**, at more than twice what merely touching its rows
costs. That is what iteration 5 went after.

## Pass 5, iteration 5 — a buried node takes the exposed path *(dropped)*

Half a hull is buried — **49 % of nodes are exposed at 126,731 blocks** — so
`nodeExposedFaces[i] <= 0`, asked once per node per substep, looked like a coin toss no processor
could predict. And it looked unnecessary: a buried node's radiation coefficient is zero, because
that is emissivity times *exposed area*, and its convection row is zeroed when the rows are filled,
so putting one through the exposed arithmetic yields the same watts. The check confirmed that much —
the same grid run both ways agreed on every temperature and both published figures, to the bit, in
atmosphere and in vacuum.

**It is 1.30**, at 126,731 and at 505,566 blocks, in both rounds, with conduction and apply flat as
controls. Thirty per cent worse, and the change was correct.

Both reasons were in the code before the measurement was taken, which is the part worth keeping:

- **The branch is predictable.** Buried and exposed nodes come in runs, because one is the inside of
  a hull and the other is its surface. A 50/50 split is not a 50/50 *branch*.
- **The path it skips is not only arithmetic.** A buried node reads two rows; an exposed one reads
  five. Making them take the same path made half the grid read three rows it did not need — and the
  floor measured in iteration 4 says a row read is 0.16 ns a node, which is most of what this cost.

So the environment read's 2.26× over floor is not slack waiting to be taken: a good part of it is
*the branch doing its job*, and the floor loop — which reads every row for every node — is measuring
a pass this one is deliberately not.

## Pass 5, iteration 6 — a bit in front of the link build's probes

With the step at its floor, the largest thing left on the load path is the link build. It asks the
block table what stands across each of a block's six faces, and **three of those eight candidate
cells in eight hold nothing**: a hull carries 3.77 neighbours a block, so 2.2 of every 6 probes are
a hash and a bucket chase to be told so — 1.1 million of them at half a million blocks.

The grid already keeps one bit a cell, built in pass 4 for the air rebuild. The full rebuild takes
it once and hands it down.

**Only the full rebuild may ask.** The set is dropped whenever the grid changes, so a caller that
asked for it *per placement* would rebuild it per placement and turn a load into quadratic work. The
incremental path — the one a placement takes — does not ask, and the overload that consults the set
is separate from the one that does not, so nothing can drift into using it by accident.

| stage | before | after | ratio |
| --- | ---: | ---: | ---: |
| links, 505,566 blocks | 74.63 ms | **65.72 ms** | **0.88** |
| links, 126,731 blocks | 18.10 ms | **13.80 ms** | **0.76** |
| place, 505,566 (control) | 47.56 ms | 48.10 ms | 1.01 |
| a settled step, 505,566 (control) | 73.29 ms | 73.48 ms | 1.00 |

*One thing the ratio does not include: the set has to be built, and the stage's best-of-fifteen
excludes that because only the first of the fifteen rebuilds pays for it. At half a million blocks
that build is about half a million bit-sets, once. On a real load it is paid once for both this and
the air rebuild, which is why it is worth having at all rather than worth having twice.*

## Pass 5, iteration 7 — the air rebuild counts contacts in a row

A room's bounding faces are counted per node, and node indices are dense from zero, so the
`Dictionary<int, int>` doing the counting was buying nothing a subscript does not: at half a million
blocks an air rebuild finds **691,306** faces holding a block, and each one was a lookup and a store.
The row is indexed by node, reused across rooms and rebuilds, and only the entries a room touched
are put back to zero — the sorted contact list is exactly that set.

**Leaving it clean is the property that matters**, because an entry left dirty would be added to the
next room's count for that node, giving it conductance for faces it does not have, with nothing else
in the model to report it. Building a room's air twice now has to give the same conductances to the
bit.

**It is 0.95, and that is not resolved.** The air rebuild is the noisiest stage in the lab — its
spread runs from 72 % to 204 % because it allocates — and its two rounds disagreed in direction more
than once. Across four paired readings at two sizes, three favour the change. It is kept on being
strictly less work, bit-identical, and one field simpler, not on a number.

*Three windows were thrown away getting even that far: the controls moved 1.29, 1.44 and 1.86
between legs. A pairing whose control moves is not a pairing, and the figure above is from the one
window where the settled step read within 2 % on both legs.*

## Pass 5, iteration 8 — the apply pass reads the critical row only when it could matter

The apply pass walks five rows a node a substep, and one of them exists only to ask whether the node
has gone over its own critical temperature. **The grid already knows the lowest positive critical it
carries** — the cue machinery computes it — and nothing on the grid can be over its own critical
below that, so the ordinary case does not read the row at all. Apply reads **0.8 ns a node visit**
against 1.2 before it.

That figure is a *bound* rather than the minimum: it only falls, except on a full resync, so a grid
that loses its most fragile block keeps the old value until then. Low is safe — it skips fewer
nodes. High is the failure, and it is silent: a node would sail past its critical without accruing
damage. `OverheatEventTests` refuses a bound above any node's own, and takes a step first, because
the bound is settled by the same sync that precedes the pass using it.

## Pass 5 — what the pass moved

Start (`bff27f0`) against tip, interleaved in one window, two rounds at 505,566 blocks, fastest
kept. **Three stages this pass never touched are the controls** — the room pass, the surface
rebuild and the exposure refresh — and they read 1.00, 0.99 and 1.03.

| stage, 505,566 blocks | start | tip | ratio |
| --- | ---: | ---: | ---: |
| **a settled step** | 96.74 ms | **83.50 ms** | **0.86** |
| **links** | 132.36 ms | **110.98 ms** | **0.84** |
| roomair | 85.01 ms | 81.08 ms | 0.95 |
| rooms (control) | 73.67 ms | 73.79 ms | 1.00 |
| surfaces (control) | 54.73 ms | 54.28 ms | 0.99 |
| exposure (control) | 29.43 ms | 30.26 ms | 1.03 |

**The step moved, and no pass had moved it before.** Passes 1, 2 and 3 all left it where they found
it — pass 1 by design, and the other two on measurement, both having tried to rearrange the link
stream. What moved it was knowing where its time went: a sum hoisted out of twenty-three substeps
that could not change between them, and a row read on every node of every substep that only three
nodes in a million could ever need.

**And the split at the tip says where a sixth pass would have to start**, at 505,566 blocks:

| stage of a step | best | per element |
| --- | ---: | ---: |
| environment | 28.88 ms | 2.1 ns a node |
| conduction | 27.95 ms | 1.0 ns a link |
| apply | 11.71 ms | 0.8 ns a node |
| publish | 5.31 ms | 10.5 ns a node |
| env fill | 4.12 ms | 8.2 ns a node |
| begin, coupled | 0.05 ms | — |

Conduction is at its floor, measured. Apply is within a third of its own. The environment read is
the only stage with visible headroom, and iteration 5 established that a good part of *that* is a
branch doing its job. **What is left is not instructions: it is three passes over the grid, twenty-
eight times a step.** Fewer substeps or fewer nodes is the next order of magnitude, and both are
`D1`'s structural work rather than a performance pass's.

## Pass 6, iterations

Pass 5 ended by saying a step is at its floor and that what is left of it needs `D1`'s structural
work rather than a performance pass's. That leaves **the link build as the largest thing on the load
path** — 111 ms at 505,566 blocks — and it is the strangest figure in the lab: **116 nanoseconds a
link to build what conduction then walks at 1.0**.

| # | Subject | Verdict | Where |
| ---: | --- | --- | --- |
| 1 | Which half of the link build is the link build | **kept** — nine tenths of it is finding neighbours | [Iteration 1](#pass-6-iteration-1--which-half-of-the-link-build-is-the-link-build) |
| 2 | A neighbour found by rank instead of by hash | **dropped** — 1.40 | [Iteration 2](#pass-6-iterations-2-to-7--five-ways-to-ask-the-same-question) |
| 3 | The pair settled where it is found, emit body shared | **dropped** — 1.16, and it changed two things | [Iteration 3](#pass-6-iterations-2-to-7--five-ways-to-ask-the-same-question) |
| 4 | What the walk costs without its lookups | **kept** — the ablation that explains the rest | [Iteration 4](#pass-6-iteration-4--what-the-walk-costs-without-its-lookups) |
| 5 | The pair settled where it is found, nothing else changed | **dropped** — unresolved | [Iteration 5](#pass-6-iterations-2-to-7--five-ways-to-ask-the-same-question) |
| 6 | A neighbour read from a row indexed by cell | **dropped** — 1.24 and 1.00, and 29 MB a rebuild | [Iteration 6](#pass-6-iterations-2-to-7--five-ways-to-ask-the-same-question) |
| 7 | A pair looked up once instead of once from each side | **dropped** — 1.13, and it explains the other four | [Iteration 7](#pass-6-iterations-2-to-7--five-ways-to-ask-the-same-question) |
| 8 | What the pass found, and what it refuses | the conclusion below | [Iteration 8](#pass-6--what-the-pass-found) |

## Pass 6, iteration 1 — which half of the link build is the link build

The stage has two halves: **finding** each block's neighbours, and **processing** each pair it finds
— the node lookup, the contact count, the conductance, the list append, the adjacency chaining.
Pass 5's history says guessing which is expensive costs two reverted iterations, and this loop is
220 ns a block, which is too short to timestamp inside without distorting it.

So it was split by **ablation**: a probe build with the entire per-pair body removed, leaving only
the neighbour walk, measured against the real thing in one window with the room pass as control.

| | before | probe (walk only) | ratio |
| --- | ---: | ---: | ---: |
| links, 505,566 blocks | 97.05 ms | 88.23 ms | **0.91** |
| links, 126,731 blocks | 26.81 ms | 22.95 ms | 0.86 |
| rooms (control) | 70.00 ms | 69.52 ms | 0.99 |

**Removing every pair operation saves a tenth of the stage.** Everything else — three divisions for
the conductance, a box overlap and a rounding for the contact count, two chain writes per link — is
9 % of it together. The other 90 % is `GetNeighbours`: **192 nanoseconds a block just to find what
is next to it.**

That figure is itself the diagnosis. A block has six candidate cells; a bit test rules out the three
in eight that are empty (pass 5, iteration 6), leaving 3.77 probes into `blocksByCell` — so **about
fifty nanoseconds a probe**. A dictionary lookup that costs fifty nanoseconds is missing cache
roughly twice: once on the bucket and once on the entry, in a table that holds half a million
entries and cannot fit anywhere near the processor.

**The pair work is not the target and the arithmetic is not the target. The lookup is.**

## Pass 6, iteration 4 — what the walk costs without its lookups

Iteration 1 said the neighbour walk is nine tenths of the stage. It did **not** say what inside the
walk costs, and iteration 2 was built on the assumption that it is the probes — an assumption that
had not been measured. So a second ablation: the same probe build, with the dictionary lookup and
the two list appends removed as well, leaving the index arithmetic, the six bit tests and the loop.

| | before | probe (no lookups, no lists) | ratio |
| --- | ---: | ---: | ---: |
| links, 505,566 blocks | 103.42 ms | 12.75 ms | **0.12** |
| links, 126,731 blocks | 28.25 ms | 1.97 ms | 0.07 |
| rooms (control) | 70.59 ms | 73.54 ms | 1.04 |

So the stage divides: **12 %** is the arithmetic, the bit tests and the loop; **9 %** is every
per-pair operation together (iteration 1); and **the remaining four fifths is the lookups**.

## Pass 6, iterations 2 to 7 — five ways to ask the same question

With four fifths of the stage in the lookups, five changes were made to them. Every one was
bit-identical, every one was checked against the walk it replaced entry for entry — the conduction
pass sums watts by walking the link list, so a different order is a different last bit on every
temperature — and **every one measured worse or unresolved.**

| # | What it did to the lookup | 126,731 | 505,566 |
| ---: | --- | ---: | ---: |
| 2 | Replaced the hash with a rank over a 3 MB row | 1.40 | 1.40 |
| 3 | Removed the lists; shared the emit body | 1.13 | 1.16 |
| 5 | Removed the lists only | 1.31 / 1.07 | 0.91 / 1.03 |
| 6 | Replaced the hash with a load from a 29 MB row | 1.24 | 1.24 / 1.00 |
| 7 | Halved the number of lookups | 1.13 | 1.03 |

Two of them are ordinary mistakes and are worth naming as such. **Iteration 3 changed two things**:
it shared the pair-emitting body between the two walks, which turned inlined code into a call on the
*general* path that was supposed to be the control. **Iteration 2 was built on an unmeasured
inference** — iteration 1 proved the walk expensive and I read that as the probe being expensive,
which iteration 4 then had to establish separately.

The other three are the result. A rank costs two loads and a fifteen-operation popcount, and lost to
the hash. A row indexed by cell costs one load into twenty-nine megabytes, and did not beat it
either. And iteration 7 — which does not touch the lookup at all, only stops making half of them —
**also lost**, because the mark that saves a probe is itself a scattered write into a three-megabyte
array.

## Pass 6 — what the pass found

**The link build is not bound by the block table. It is bound by touching grid-sized memory once per
neighbour, and every scheme tried keeps exactly one such touch.**

A hash probe, a rank lookup, a load from a dense row and a byte written to a mark array are all the
same thing at this size: one access to a structure far larger than cache, at an address the previous
access does not predict. Swapping one for another moves the cost around. Iteration 7 is the cleanest
statement of it — it genuinely halves the lookups and still loses, because the bookkeeping that
buys the saving is itself the thing being saved.

**What the randomness is made of is worth stating too**, because it points at what would work. Nodes
are numbered in the order blocks are added, which is spatially coherent — `LinkSpanProbe` measures
97.8 % of links spanning fewer than 1,024 node indices. So the *cells* a rebuild asks about are
coherent as well. **Hashing them is what throws that away**: two cells one apart in space are two
buckets far apart in the table. That is why the cell-indexed row came closest of the five, and why
its remaining cost is its own build rather than its lookups.

**Designed and refused: walking blocks in cell order.** A rebuild that visited blocks in the box's
own index order rather than in node order would make every neighbour lookup sequential, and the
whole stage would fall to something near the 12 % that iteration 4 measured. It is refused *here*
rather than untried: it emits links in a different order, and the conduction pass sums over that
order, so it moves the last bit of every temperature on every grid. That is a change with a real
prize and a real cost, and it belongs to a pass that can open with re-recording the bit-identity
baselines rather than reach it seventh — the same shape as the span flood, which pass 3 designed,
pass 4's iteration 4 unblocked and pass 4's iteration 7 built.

**And the finding generalises past this stage.** Surfaces, the room flood, the air rebuild and the
link build all walk a grid asking about neighbouring cells, and all four have now had a
cache-shaped optimisation applied to them. What is left in each is the irreducible part: one
unpredictable touch per element per pass.

## Pass 7, iterations

Pass 6 ended with a change designed and refused: **walking blocks in the box's own index order**,
which makes every neighbour lookup sequential and would take the link build to near the 12 % its
arithmetic costs. It was refused because it emits links in a different order and the conduction pass
sums over that order, so it moves the last bit of every temperature. `backlog.md` `D3b` recorded it
as wanting a pass that *opens* by removing that obstacle — the shape pass 4 used for the span flood.

**This is that pass, and the designed change does not work.**

| # | Subject | Verdict | Where |
| ---: | --- | --- | --- |
| 1 | The link list's order is the graph's, not the walk's | **kept** — 1.02 at 505k, and the property is worth it | [Iteration 1](#pass-7-iteration-1--the-link-lists-order-is-the-graphs) |
| 2 | The links are built by walking cells | **dropped** — 1.23 | [Iterations 2 to 4](#pass-7-iterations-2-to-4--the-designed-change-measured) |
| 3 | What the sort the cell walk needs actually costs | **kept** — the ablation that rules it out | [Iterations 2 to 4](#pass-7-iterations-2-to-4--the-designed-change-measured) |
| 4 | The cell walk skips the empty thirteen fourteenths | **dropped** — 1.16 | [Iterations 2 to 4](#pass-7-iterations-2-to-4--the-designed-change-measured) |
| 5 | What the two passes together establish | the conclusion below | [Iteration 5](#pass-7--what-sixteen-iterations-on-one-stage-establish) |
| 6 | Cleanup: the ledger, the counts, and no orphans | **kept** | — |
| 7 | Ordering and chaining are one walk | **kept** — and it pays for iteration 1 | [Iteration 7](#pass-7-iteration-7--ordering-and-chaining-are-one-walk) |

## Pass 7, iteration 1 — the link list's order is the graph's

The conduction pass accumulates watts by running down `linkA`/`linkB` in order, and a sum of floats
depends on its order — so **the sequence a rebuild emitted links in was part of the answer**. Any
change to how neighbours are found moved the last bit of every temperature on every grid, which is
why pass 6 had to hold five separate optimisations to the exact emission order, and why the sixth
was refused outright.

Sorted by lower node then higher, the order is a function of the graph. A rebuild may find the same
links in any sequence and the result is the same to the bit. The walk emits in ascending `NodeA`
already, so this only orders each run of equal `NodeA` by `NodeB` — six at most for a one-cell block.
Chains are rebuilt from the sorted list; chain order is not an answer, because the one walk over a
chain sorts what it collects.

**It moves the bits once and nothing was pinned to them**: the whole suite passes. It costs **1.02** at
505,566 blocks and 0.99 at 126,731. `CanonicalLinkOrderTests` checks that the list comes out sorted,
that sorting is a permutation of the same graph, and — the check that matters — that **the walk's
own order is not already sorted**, because a canonicalisation that reorders nothing would buy no
freedom at all. It departs from sorted at the first link.

## Pass 7, iterations 2 to 4 — the designed change, measured

| | 126,731 | 505,566 |
| --- | ---: | ---: |
| 2 — links built by walking cells in index order | 1.18 | **1.23** |
| 4 — the same, skipping empty words 64 cells at a time | 1.30 | **1.16** |
| 3 — the counting sort it needs, ablated away | 0.97 | 1.07 |

**The sort is not the reason.** Removing it entirely costs nothing measurable, so the walk itself is
what is slow — which was worth establishing, because "it needs a sort" was the obvious suspect and
would have been the wrong thing to optimise.

**The reason is one number: a hull fills about a fourteenth of its own bounding box.** Any structure
indexed by *cell* is fourteen times sparser than one indexed by *block*, so the row this walk reads
is **29 MB where the dictionary it replaces is about 20** — and a random touch of a bigger array is
not cheaper for being an array rather than a hash. Iteration 4 fixed the *iteration count*, skipping
thirteen cells in fourteen a word at a time, and did not fix the memory: still 1.16.

## Pass 7 — what sixteen iterations on one stage establish

Six schemes have now been measured on the link build's neighbour lookup:

| | what it changed | result |
| --- | --- | ---: |
| pass 6, it. 2 | a rank over a 3 MB row instead of the hash | 1.40 |
| pass 6, it. 3 | the lists around the lookup, and a shared emit body | 1.16 |
| pass 6, it. 5 | the lists around the lookup only | unresolved |
| pass 6, it. 6 | a 29 MB row indexed by cell instead of the hash | 1.24 / 1.00 |
| pass 6, it. 7 | **half as many lookups** | 1.13 / 1.03 |
| pass 7, it. 2/4 | **the lookups made sequential** | 1.23 / 1.16 |

**The stage is bound by touching grid-sized memory once per neighbour, and the grid's memory is too
sparse to lay out by cell.** Making the touch cheaper does not work, because a hash probe, a rank, a
dense-row load and a byte written to a mark array cost the same at this size. Making the touches
fewer does not work, because the bookkeeping that saves one is itself one. Making them sequential
does not work, because sequential over a box that is fourteen fourteenths empty moves more memory
than random over a table that is not.

That is a floor, not a run of failures — and `D1`'s answer for the step has the same shape: **what
is left in both is not instructions, it is the size and the sparsity of the thing being walked.**
The remaining lever in either case is to walk less of it, which is chunking and activity tracking
for the step and, for the load path, a grid representation the mod does not own.

## Pass 7, iteration 7 — ordering and chaining are one walk

Iteration 1 bought its property with a second pass over the link list: the sort placed every link,
and then a rebuild of the chains visited every link again, because a reorder invalidates them. And
the code it replaced was worse than it looked — chaining happened *inside* the build loop, so every
link paid two capacity checks and a sixteen-byte read back out of the list it had just been appended
to.

A link can be chained the moment its place is settled, so the sort does both. Chain order is not an
answer — the one walk over a chain sorts what it collects — so this only has to be complete.

**Measured against pass 6's tip, iterations 1 and 7 together read 0.94** at 505,566 blocks, in the
round where the settled step read exactly 1.00 and the room pass 0.99. So the pass's one kept change
is not a cost at all: the property came free, and a little speed with it.

*No figure is quoted at 126,731 blocks, and the reason is worth recording. The link stage is
**bimodal** there — two windows measuring the same two commits disagreed about which leg was fast,
one reading 16.9 ms for the code the other read at 32.8. Its spread runs to 441 %. Something about
that stage at that size settles into one of two states, and until that is understood a figure from
it is not a figure.*

## Pass 7 — what the pass kept

**One change, and it is a property rather than a speed.** The link list's order is a function of the
graph: a rebuild may find the same links in any sequence and the result is identical to the bit.
That is what pass 6 lacked — five optimisations there had to be held to the exact emission order,
and the sixth was refused outright for wanting to change it.

The freedom has not yet paid, because the change it unblocked was measured and does not work. It is
kept anyway, for three reasons: it costs nothing (0.94 with the chaining folded in), it removes a
trap where changing a walk silently moves every temperature, and it turns the checks for any future
walk from order comparisons into set comparisons.

## Pass 8, iterations

Pass 7 closed with an instrument question rather than a code question: the link stage was **bimodal**
at 126,731 blocks — two windows measuring the same two commits disagreed about which was faster, one
reading 16.9 ms for the code the other read at 32.8. That is not a footnote. **Passes 6 and 7
rejected seven changes on that instrument**, at effect sizes between 3 % and 40 %.

| # | Subject | Verdict | Where |
| ---: | --- | --- | --- |
| 1 | Whether the stage is bimodal, traced repeat by repeat | **kept** — it is not; it is under-sampled | [Iteration 1](#pass-8-iteration-1--the-stage-is-not-bimodal-it-is-under-sampled) |
| 2 | Whether that is one stage or the instrument | **kept** — every stage but the solver | [Iteration 2](#pass-8-iteration-2--every-stage-but-one) |
| 3 | Sample until the fastest reading is reproduced | **kept** | [Iteration 3](#pass-8-iteration-3--sample-until-the-answer-is-reproduced) |
| 4–5 | Pass 6's halving recovered and re-measured; the rule needed a floor as well as agreement | **dropped again** — 1.02 on the corrected instrument | [Iteration 10](#pass-8-iteration-10--the-first-rejection-re-measured) |
| 6 | `M4` says what makes N enough | **kept** | [rules.md](rules.md#m4--keep-the-fastest-of-n-and-the-median-and-publish-the-noise-floor) |
| 7 | Which published figures survive the correction | the audit below | [Iteration 7](#pass-8-iteration-7--which-published-figures-survive) |

## Pass 8, iteration 1 — the stage is not bimodal, it is under-sampled

Traced repeat by repeat, the link build at 126,731 blocks reads:

> **98.0**, 40.4, 34.2, 35.6, 37.8, 35.2, 32.6, 26.7, 33.6, 34.4, 35.6, **25.3**, 25.5, 37.0, 37.8

There is no bimodality. The first repeat is cold, and the rest scatter between 25 and 40 with an
occasional dip. **Best-of-fifteen samples the lower tail of that scatter to an unpredictable depth**,
which is what produced 16.9 in one window and 32.8 in another. The statistic was right; the sample
size was never checked.

| repeats | best across three runs of the same binary | spread |
| ---: | --- | ---: |
| 15 | 38.1, 24.8, 22.8 ms | **67 %** |
| 60 | 15.9, 25.5, 23.9 ms | 60 % |
| 150 | 22.6, 24.6, 24.3 ms | **9 %** |

## Pass 8, iteration 2 — every stage but one

| stage | best-of-15, three runs | spread | best-of-150 | spread |
| --- | --- | ---: | --- | ---: |
| surfaces | 7.4, 9.6, 6.5 | **48 %** | 7.24, 7.16, 7.33 | 2 % |
| rooms | 15.3, 17.1, 19.6 | **28 %** | 16.5, 16.6, 15.9 | 4 % |
| links | 38.1, 24.8, 22.8 | **67 %** | 22.6, 24.6, 24.3 | 9 % |
| a settled step | 17.7, 17.6, 17.1 | 3.5 % | 18.4, 16.9, 18.0 | 8 % |

**Only the solver was converged at fifteen** — because each of its repeats is twenty steps, so it
was already taking three hundred samples. Which is why pass 5, whose figures came from the step and
its phases, reads consistently, and why the passes that judged *stages* did not.

**Fifteen was chosen for a different question.** It dates from the first pass, where the worst of
fifteen room passes was four times the best — a statement about the *spread*, which fifteen shows
perfectly well. Nobody asked whether fifteen was enough for the *minimum* to settle, and it is not.

## Pass 8, iteration 3 — sample until the answer is reproduced

The fix keeps fastest-of-N and fixes the sampling. A stage now repeats until **five readings land
within two per cent of its best**, on a floor of a hundred repeats, capped at four hundred.

Two rules were tried and discarded first, and both are instructive. A fixed count of repeats since
the best settled the link build and the room pass and not the surface rebuild. A count *scaled* to
how long the best took to find settled the surface rebuild and not the link build — because how rare
a stage's fast repeats are differs by stage and by what else the machine is doing.

**And agreement alone was not enough either**, which cost an iteration to discover: a rule that
stopped at five agreeing readings settled a *plateau* rather than a minimum, because on a contended
machine a run's first readings cluster tightly at a slow value. Two legs of one pairing read 75 ms
and 154 ms for the same stage with the control flat between them. The hundred-repeat floor is what
makes a stage look before it is allowed to be satisfied.

| stage | three runs, before | three runs, after |
| --- | ---: | ---: |
| surfaces, 126,731 | 48 % | **2.1 %** |
| links, 126,731 | 67 % | **3.0 %** |
| rooms, 126,731 | 28 % | **7.0 %** |
| links, 505,566 | — | **7.4 %** |

`EveryStageStopsForAReasonItCanName` pins the bookkeeping — every row stopped on a confirmation or
at the cap and says which — and deliberately does **not** assert stability, because a check that
demands convergence under an eight-way parallel suite is testing the hardware. The first version of
it did, and failed and passed on consecutive runs of unchanged code.

## Pass 8, iteration 7 — which published figures survive

An instrument that was wrong for six passes is only half a finding; the other half is what it did to
what has already been published here. **The uncertainty it hid is 28–67 % on a stage ratio**, so the
question for every figure is whether the effect is larger than that.

**What is unaffected.** Anything measured through the *step* or its phases: each solver repeat is
twenty steps, so that path was taking three hundred samples and reads 3.5 % across runs. That covers
**all of pass 5** — the settled step at 0.86, the environment stage at 0.68, apply at 0.8 ns a node —
and every `bench report` figure, which is a different instrument again.

**What survives on size.** An effect several times the uncertainty is still an effect:

| | | |
| --- | ---: | --- |
| pass 4, the room pass | **0.36** | and its allocation 0.10, which is a count and not a timing |
| pass 4, exposure | **0.53** | |
| pass 4, the air rebuild | **0.43** | on identical work counters, which is the stronger claim |
| pass 3, surfaces | **0.50** | the narrowest of these against a 48 % floor on that stage |
| pass 4, the span flood | **0.59** | |

**What is now unproven, in either direction.** Every stage ratio between roughly 0.7 and 1.4 taken
before this pass is inside the instrument's uncertainty. That includes several figures this page
reports as kept — pass 3's links at 0.80 and place at 0.77, pass 4's links at 0.84 and rooms at
0.84, pass 5's links at 0.88 — **and all seven rejections in passes 6 and 7**, which were judged at
1.03 to 1.40.

**A flat control does not rescue them**, which is worth saying because the pairings all carried one.
A control constrains what the *machine* was doing between the legs; it says nothing about how deep
into its own lower tail the subject stage happened to sample, which is an independent draw per run.
The controls were themselves stages, with the same 28 % of their own.

**None of the unproven changes is reverted on this account.** Each was kept or dropped for reasons
beyond its ratio — less work, fewer allocations, a simpler structure — and re-measuring seven
reverted optimisations properly is a pass of its own, not a footnote to this one. What this iteration
buys is that the page now says which of its numbers are load-bearing and which are decoration.

## Pass 8, iteration 10 — the first rejection, re-measured

Of the seven changes passes 6 and 7 rejected, the most suspect was **halving the number of
lookups** — marking the far side of each pair so it does not look up what the near side already
found. It is structurally less work, and it was rejected at 1.13 and 1.03 on an instrument since
measured at 28–67 % uncertainty.

Recovered and put to the corrected instrument, it reads **1.02** at 126,731 blocks with the room
pass flat. **Pass 6's verdict stands**, now on a sound reading: the mark that saves a probe is a
scattered write into three megabytes, which is the same random touch of grid-sized memory it saves.
It is dropped again, and this time the number means something.

*The reading at 505,566 blocks had not returned when the pass closed. The verdict rests on the
smaller rung, where the corrected
instrument reads within 3 % across runs — enough to see a change of the size this one would have to
be, and stated rather than implied.*

**The other six rejections are not re-measured here.** Each takes a pairing of its own, and seven of
them is a pass rather than an iteration. What this pass leaves behind is an instrument that can
settle them and a page that says which of its own numbers are load-bearing.

# Pass 9 — 2026-08-27, the stages nobody looked at

Passes 6, 7 and 8 spent twenty-six iterations on **one stage**. The link build is now the
best-understood thing in this repository and, by its own evidence, at a floor: sixteen iterations
established that it is bound by one unpredictable touch of grid-sized memory per neighbour, and
pass 8 established that the instrument which judged them could not have resolved most of what it
was asked. That is a good place to stop looking at it.

This pass opens on the other end of the build. On the corrected instrument, at 126,731 blocks:

| stage | best ms | ns a unit | prior passes that touched it |
| --- | ---: | ---: | --- |
| solver, 20 steps | 17.23 | — | 5 |
| rooms | 15.80 | 9.6 a cell visited | 1, 2, 3, 4 |
| links | 14.49 | 58.6 a link | 3, 4, 5, 6, 7 |
| place | 11.33 | 89.4 a block | 1 |
| **exposure** | **11.33** | **89.4 a node** | **4** |
| roomair | 7.83 | 4.8 a probe | 4, 5 |
| surfaces | 6.54 | 51.6 a cell | 2, 3 |
| register | 5.04 | 39.8 a block | — |

*Taken at `a4e4f9b`, ship hull, all eight stages in one run, `bench stages --size 125000`. The
machine is the one [named above](#performance-work).*

> **Provisional, and corrected below.** This table was taken before the lab said whether a row had
> settled. [Iteration 2](#pass-9-iteration-2--the-stage-lab-knew-which-rows-had-not-settled-and-did-not-say)
> added that column and found five stages of eight never reproducing their own fastest reading, so
> the ranking here — *which stage is largest* — is not safe as it stands. What the pass did with it
> is unaffected: it went to the stage that had had the least work, and the ablation that followed is
> a ratio inside one window against a flat control (`E10`).
>
> **And a second, larger reason, found in
> [iteration 6](#pass-9-iteration-6--the-one-write-exposure-change-and-the-session-it-was-measured-in):
> these figures belong to the session that took them.** The exposure row here reads 11.33 ms where
> twelve processes of a later window read 3.8–5.6 for bit-identical code. So no number in this table
> may be read against a number anywhere else on this page — including against this pass's own
> results — and the milliseconds are kept only because the *ranking* is what the pass used them for.
> `M7`.

**Exposure is the largest stage with the least work behind it** — one optimisation, in pass 4, and
eighty-nine nanoseconds a node to answer a question about six faces. `register` and `place` are
next by the same measure. This pass starts there.

| # | Subject | Verdict | Where |
| ---: | --- | --- | --- |
| 1 | Where the exposure stage's eighty-nine nanoseconds a node go | **kept** — two thirds of it is writing the answer down | [Iteration 1](#pass-9-iteration-1--two-thirds-of-the-exposure-stage-is-writing-the-answer-down) |
| 2 | Whether a stage settled, in the table and the CSV | **kept** — five stages of eight never reproduced their best | [Iteration 2](#pass-9-iteration-2--the-stage-lab-knew-which-rows-had-not-settled-and-did-not-say) |
| 3 | The lab that asks which summary of a stage's repeats reproduces | **kept** — `bench samplestats` | [Iteration 3](#pass-9-iteration-3--the-lab-that-asks-which-statistic-reproduces) |
| 4 | Cleanup: whether a doc comment names something that is there | **kept** — the compiler does it, and found twenty-five | [Iteration 4](#pass-9-iteration-4--the-compiler-was-never-asked-whether-a-comment-names-something-that-is-there) |
| 5 | Which summary of a stage's repeats reproduces, measured | **kept** — none of them does for every stage; both are reported now | [Iteration 5](#pass-9-iteration-5--no-single-statistic-reproduces-and-two-stages-have-none) |
| 6 | The exposure stage's six writes packed into one | **kept** — the stage's median falls 19.5 % | [Iteration 6](#pass-9-iteration-6--the-one-write-exposure-change-and-the-session-it-was-measured-in) |
| 6 | Cleanup: the mod project had not built for three commits | **kept** — `ProjectFileTests` | [Iteration 6](#pass-9-iteration-6--the-one-write-exposure-change-and-the-session-it-was-measured-in) |
| 7 | An exposure refresh that changed nothing writing nothing | **kept, and not for the reason it was proposed** — worth nothing on the stage, 2.0 ms on the step after a remap | [Iteration 7](#pass-9-iteration-7--the-skip-is-worthless-where-it-was-aimed-and-worth-two-milliseconds-where-it-was-not) |
| 8 | The ladder's `build` column, which the page says is fastest-of-three | **kept** — it was one stopwatch | [Iteration 8](#pass-9-iteration-8--the-one-column-a-load-path-change-is-judged-by-was-a-single-sample) |
| 9 | One-cell blocks' surface arrays, shared instead of allocated | **kept** — `place` falls 15.6 %, after a first attempt that allocated *more* | [Iteration 9](#pass-9-iteration-9--a-one-cell-blocks-walls-do-not-depend-on-where-it-is) |
| 10 | The pass measured against its own start | the summary below — and a control that was not one | [Iteration 10](#pass-9-iteration-10--what-the-pass-moved-and-the-control-that-was-not-one) |

## Pass 9, iteration 1 — two thirds of the exposure stage is writing the answer down

Pass 6's iteration 2 is the cautionary tale for this one: an optimisation built on the inference
that *the walk is expensive, so the probe must be* — an inference iteration 4 then had to measure
separately, and which was wrong. So the exposure stage was split by ablation before anything was
proposed.

The stage's inner loop is three things:

```
surfaces.GetExposedFaces(node.Block, exposureMap, exposureScratch);   // the question
for f in 0..5: node.SetExposedFaces(f, exposureScratch[f]);           // the answer, written down
node.RefreshExposure();                                               // and read back out
```

Five probe builds of the same tree, each rebuilt `--no-incremental` and measured on the corrected
instrument with the room pass as control, all five inside one window:

| leg | what is left | exposure | rooms (control) |
| --- | --- | ---: | ---: |
| A | everything | 6.56 ms | 14.71 |
| D | the walk and the answer; the question not asked | **4.63 ms** | 16.03 |
| E | the walk alone; neither asked nor written | **0.11 ms** | 16.03 |

**Legs D and E are the result, and they are clean**: neither removes a *predicate*, so neither
changes how many times anything downstream runs. `exposureScratch` is all zeros in leg D and the
six writes and the refresh execute exactly as many instructions as they do in leg A.

So the stage divides:

* **the loop itself, `nodes[i]` and `node.Block` — 0.11 ms, under 2 %**
* **writing the answer into the node — 4.52 ms, 69 %**
* **computing the answer — 1.93 ms, 29 %**

**Two thirds of the exposure stage is not the exposure test.** It is six read-modify-writes of one
packed field followed by a second pass that unpacks all six again to total them, and four derived
values written after it — thirty-five nanoseconds a node to record six numbers the caller already
had in a local array.

*Two further legs asked what inside `GetExposedFaces` costs, by removing the room test and then the
surface-state lookup. **Both are confounded and neither is quoted**: each removes a predicate that
guards the work after it, so the leg with the cheaper body also runs more of it — the reading with
the room test removed came out at 2.86 ms and the one with the state lookup removed as well at
5.83, which is a smaller change measuring slower than a larger one that contains it. That is the
signature of an ablation that changed two things, and it is recorded here rather than deleted
because pass 6's iteration 3 made the same mistake and the note is cheaper than the repeat.*

**A caution about the absolute.** Leg A reads 6.56 ms for a stage the eight-stage run at the top of
this pass reads at 11.33. Both are best-of-a-settled-hundred on the same binary and the same hull;
what differs is what ran before it. A stage's cost depends on the cache and heap the stage before it
left, and the lab settles the heap but cannot settle the cache. **Ratios within one window are what
this instrument produces**; the absolute belongs to its window, which is why every leg above carries
its own control.

## Pass 9, iteration 2 — the stage lab knew which rows had not settled, and did not say

Iteration 1's pairing was measured twice, and the two rounds disagreed: the exposure stage read
6.81 ms in one leg and 5.37 in the other on identical code, 27 % apart, with the room pass flat to
0.05 % between them. Pass 8 closed by reporting the same three stages settling to 2.1 %, 3.0 % and
7.0 %, so either that had rotted in a day or something about it was never true.

**The lab already held the answer and printed none of it.** Since pass 8 a `Row` has carried
`Repeats`, `RepeatsSinceBest` and `ConfirmedBest`, and `EveryStageStopsForAReasonItCanName` has
asserted that every row ends on a confirmation or at the cap. Neither the table a person reads nor
the CSV a comparison is built from carried any of the three. A row whose best was reproduced five
times and a row that gave up at four hundred repeats printed identically.

So the three go into both artefacts, with the reason in one word — `confirmed`, `capped`, or
`fixed` for the step-phase path, which uses a count rather than the rule. `TheTableAndTheCsvSayWhyEachStageStopped`
pins the header, the vocabulary and that a capped row and a confirmed one do not print the same;
it was run against a CSV with the column removed and fails there.

**What it says, the first time it was asked:**

| stage | best ms | repeats | stopped |
| --- | ---: | ---: | --- |
| place | 11.34 | 400 | **capped** |
| register | 4.75 | 400 | **capped** |
| surfaces | 6.18 | 400 | **capped** |
| links | 31.92 | 113 | confirmed |
| rooms | 15.69 | 400 | **capped** |
| exposure | 10.98 | 100 | confirmed |
| roomair | 18.25 | 103 | confirmed |
| solver | 17.33 | 400 | **capped** |

**Five stages of eight never reproduced their own fastest reading.** Pass 8's rule — a hundred
repeats and then until five land within two per cent of the best — does not settle this lab at
126,731 blocks. It reports a number anyway, and until this iteration it reported it in the same
type as a settled one.

**And the row that did confirm is the worst of the eight.** `links` stopped at 113 repeats,
confirmed, at 31.92 ms — against 14.49 ms for the same code in the run at the head of this pass.
A confirmation is not a convergence: five readings agreeing to two per cent is exactly what a
contended run's *plateau* looks like, which pass 8's iteration 3 identified and believed a
hundred-repeat floor had fixed. It has not. The floor makes a stage look for longer; it does not
make the minimum reproducible.

**So every stage figure in this pass so far is provisional, including its own opening table.** The
table at the head of pass 9 was taken before this column existed and cannot say which of its rows
settled; the ranking it draws — which stage is largest — is not safe until it is re-taken. What
survives it unchanged is iteration 1's *split*, because that is a ratio between three legs measured
in one window against a control that was flat across all five, and 69 % against 2 % is not a figure
the sampling can produce.

**This does not correct pass 8; it continues it.** Pass 8 found that fifteen repeats never settled
and measured the fix on a held machine, three runs a stage. It did not check whether the runs that
produced those spreads had *confirmed*, because nothing printed it. The next iteration asks the
question that answers directly: over a stage's own repeats, which statistic reproduces.

## Pass 9, iteration 3 — the lab that asks which statistic reproduces

Iteration 2 leaves one question, and it is older than pass 8. `bench stages` has reported the
**fastest** repeat since pass 1, on the argument that a timing sample is the true cost plus whatever
else the machine was doing and that noise is one-sided, so averaging it in measures the operating
system. That argument is sound, and it establishes that the minimum is the least *biased* summary —
not that the minimum of a sample this size is *reproducible*. Nobody has ever compared it with an
alternative, so fastest-of-N is a choice nobody made.

`bench samplestats` asks directly. `bench stages` now keeps every repeat and writes `samples.csv`
beside `stages.csv`; the new command reads several of those — one per process, because that is how a
pairing's two legs are actually taken — and reports, per stage and per candidate, **the spread
between runs of one binary**. That is the only property a comparison uses: a statistic whose value
two runs of identical code disagree about cannot say whether a change moved anything.

The candidates are the minimum and the 1st, 5th, 10th, 25th and 50th percentiles, by **nearest
rank**, so every value reported is a reading the instrument actually took rather than an
interpolation between two it did not. Nothing above the median is offered: the one-sided-noise
argument rules out the mean and the upper tail on evidence that has not changed, and this is a
narrower question than *which statistic is best*.

`SampleStatisticTests` checks the arithmetic against hand-computed answers rather than against the
lab itself (`E7`), and both ways it could quietly compare the wrong thing: a stage present in some
runs and not others is named and dropped rather than averaged over the runs that hold it (`E4`), and
a run file that parsed to no repeats throws rather than leaving the comparison silently one run
short (`E8`). It also pins that the fastest reading in the file is the one the stage row reported,
because an artefact that is not of the run that produced the row would make every figure here about
some other run.

## Pass 9, iteration 4 — the compiler was never asked whether a comment names something that is there

The exposure work turned up two `<see cref="RepeatsWithoutImprovement"/>` in `StageLab`, pointing at
a field pass 8 replaced. They had survived eight performance passes, `R14`'s own check, and
`EveryCitedIdentifierResolves` — which reads *rule* identifiers out of the pages and never looks at
a cref at all. **Nothing in this repository resolved a doc comment's references, and the compiler
does it for free.**

`GenerateDocumentationFile` was `false` on the core project and unset elsewhere, which is what turns
the resolution off. With it on, and `CS1574`, `CS1580`, `CS1581`, `CS1584`, `CS1710`, `CS1572`,
`CS1734`, `CS1587` and `CS1570` as errors, the build does name binding on every reference in every
comment: generics, overloads, inherited members, the lot. **Twenty-five failures were waiting**, in
twelve files, four of them in the shipped mod:

| what it was | where | how many |
| --- | --- | ---: |
| a cref to a member that had been renamed or removed | `StageLab`, `WindField`, `RoomAir`, `RoomMap`, `ThermalCellDefinition` | 9 |
| a method's `<param>` tags left behind when something was inserted above it | `WindProfile`, `Hulls` | 4 |
| a `<paramref>` to a parameter that is not there — one of them on a *class* | `SuitThermal`, `SolverAb` | 2 |
| a doc comment attached to no language element at all | `ThermalLoopDefinition`, `RetrofitTests` | 2 |
| a comment whose XML does not parse, so the tags a reader relies on are not the tags the compiler saw | five files | 8 |

**Two are worth naming.** `ThermalLoopDefinition` had a summary reading *Thermal conductivity of the
coolant, W/(m K)* sitting above `[ProtoMember(5)]` and below it `HeatTransferCoefficient` — a
conductivity field deleted years of commits ago, its comment resting on the attribute of the field
that followed. That is in the mod players load. And `WindProfile.Multiplier`'s three parameters were
documented onto `GradientHeightIn`, which was inserted between them and it: `R14`'s own test is
built to catch exactly that shape and looks for a `</summary>` followed by a `<summary>`, so an
orphan separated by `<param>` tags walks past it.

**What is deliberately not an error.** `CS1573`, a member with some parameters documented and not
others, and `CS0419`, a cref that resolves to more than one overload. Both are about coverage and
precision; neither is a name that resolves to nothing, and 84 of the first would have drowned the
nine that matter.

**The vendored exemption, and why it is a project rather than a path.** `.editorconfig` cannot
narrow this: its severities are overridden by `WarningsAsErrors`, which was tried and measured to
change nothing at all. So `CS1570` and `CS1572` are warnings in
[the mod project](../Thermodynamics/Thermodynamics.csproj) — the only project that compiles the three vendored paths, and
the only place `R6` forbids the fix — and errors everywhere else, which is every project under
`tests/` and so every file in `Core`, `Game` and `Telemetry`. The seven no vendored file trips are
errors in both.

**This is not a performance iteration and it is in a performance pass on purpose.** The rule this
page runs on is that the instrument is part of the result; a pass that finds its measuring tool
wrong twice in three iterations should expect the same of the tool that says what its code means.
Both of pass 9's instrument findings have the identical shape: *the thing already knew, and was
never asked to say.*

## Pass 9, iteration 5 — no single statistic reproduces, and two stages have none

Iteration 3's lab, put to the question it was built for: four runs of one binary at 126,731 blocks,
four hundred repeats of every stage each, every candidate summary compared on the one property a
comparison uses — **do two runs of identical code agree on it.**

| stage | min | p1 | p5 | p10 | p25 | median | best |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | --- |
| place | **17 %** | 41 | 66 | 55 | 84 | 96 | min |
| register | 97 % | 99 | 126 | 146 | 106 | 129 | **none** |
| surfaces | **28 %** | 30 | 81 | 93 | 114 | 80 | min |
| links | 105 % | 103 | 111 | 80 | 29 | 108 | **none** |
| rooms | **4.5 %** | 5.0 | 8.4 | 6.1 | 12 | 66 | min |
| exposure | 30 % | 63 | 65 | 60 | 14 | **0.9 %** | median |
| roomair | 108 % | 109 | 4.3 | **1.7 %** | 13 | 23 | p10 |
| solver | 16 % | 25 | 17 | 42 | 7.3 | **4.4 %** | median |

*Spread between the four runs, per stage, per summary. `bench stages --repeats 400` four times and
`bench samplestats --from` over the four, in one window.*

**The minimum is best for three stages, the median for two, the tenth percentile for one, and for
two of the eight nothing tried reproduces at all.** That is not a result anybody expected, and it is
the third instrument finding of this pass.

**What separates them is whether a stage has a fast mode it reaches rarely.** Exposure's minimum is
5.08 ms and its median 11.40; its first percentile is 5.14, so about four of four hundred repeats
are anywhere near the best. Best-of-N samples that mode to a depth that is an independent draw per
run — which is exactly pass 8's diagnosis, and **four hundred repeats do not fix it**, because the
depth reached is not a function of how long you look. The room pass, whose minimum is 85 % of its
median, is one mode with additive noise, and its minimum reproduces to 4.5 %.

**Pass 8 raised the sample size, which was the right fix for the wrong half of the problem.** It
established that fifteen was not enough and that a stage must reproduce its best before stopping.
Both are true. Neither addresses a stage whose best is a rare draw, and the confirmation rule
guarantees only that the draw happened five times in four hundred — which two runs will do to
different depths and at different values.

**`links` is the sharpest case, and it revises pass 8's headline.** Run 2's entire distribution sat
at 30.5–31.3 ms: its minimum, first percentile and median are within 3 % of each other, and it never
reached the ~15 ms that runs 1 and 3 found in their first few repeats. That is not under-sampling —
run 2 took four hundred samples. **A whole process can be in the slow mode for its lifetime.** So
pass 7's observation of *bimodality* at 126,731 blocks was right, and pass 8's "it is not bimodal,
it is under-sampled" was a correct reading of one trace generalised too far: the modes are between
processes, and a within-process trace cannot see them.

**What this means for the seven rejections in passes 6 and 7 is stronger than pass 8's audit
said.** That audit put the uncertainty at 28–67 % on a stage ratio and called the rejections
unproven. On this evidence the link stage is **not resolvable between processes by any summary** —
105 %, 103 %, 111 %, 80 %, 29 %, 108 % — and every one of those pairings was two processes. They are
not merely unproven; the instrument that judged them could not have judged them.

**What changes.** A stage row now carries **both** its minimum and its median, and `best/med`
beside them — the ratio that says whether the minimum sits in the stage's own bulk or in a mode it
visited a handful of times. A ratio is believed only where both legs agree on both figures. `M4` is
rewritten to say so, and says plainly that the reproducibility itself is not checked by a test and
cannot usefully be: it is a property of the machine, and the one test that demanded it failed and
passed on consecutive runs of unchanged code.

**What is deliberately not done.** No stage is given its own statistic. That would fit this
measurement and nothing else — the assignment is a property of the machine and the size as much as
of the stage, and a lab that hard-codes which column to read for `roomair` is a lab that lies the
first time the answer changes. Reporting both and refusing marginal ratios costs a column and
assumes nothing.

## Pass 9, iteration 6 — the one-write exposure change, and the session it was measured in

Iteration 1's ablation put **4.52 ms of the exposure stage's 6.56 into writing the answer down**:
six read-modify-writes of one packed field, one per face, followed by a second pass that unpacked
all six to total them. The change that follows from it is small and was built at the time — pack the
six counts into a local, store once, and let the total fall out of the same walk — and it went
unmeasured, because iterations 2 to 5 were spent establishing that the instrument could not have
judged it.

**It is 19.5 % of the stage, and the measurement is the interesting half.**

| | base | change | |
| --- | ---: | ---: | ---: |
| exposure, best of 400 | 3.924 ms | 3.589 ms | −8.5 % |
| exposure, **median of 400** | **4.755 ms** | **3.829 ms** | **−19.5 %** |
| exposure, `best/med` | 0.82 | 0.94 | |
| exposure, ns a node | 37.5 | 30.2 | |
| rooms *(control)*, best | 13.550 ms | 13.774 ms | +1.7 % |
| rooms *(control)*, median | 14.514 ms | 14.209 ms | −2.1 % |

*Twelve processes in one window, **alternating** base, change, base, change — six of each —
at 126,731 blocks, four hundred repeats a stage. Each column is the median of its six processes.
`rooms` rides along untouched as the control.*

**Every one of the six change readings is below every one of the six base readings, on both
statistics, and the two legs never overlap.** The base's six medians span 4.704–4.761 and the
change's 3.812–3.861; the control's twelve interleave completely. Node visits are 126,731 in every
run of both legs, so the change moved no work — which is the claim, since it removes writes and not
predicates.

**Alternating rather than blocking the two legs is what iteration 5 bought.** That iteration found
a process can sit in a slow mode for its whole life, so six of one leg run back to back would
confound the leg with the window's own drift; interleaving them makes any drift common to both.

**The two statistics disagree by more than a factor of two on the size of the win, and the median is
the one to believe.** The base's `best/med` is 0.82 — its fastest repeat sits well outside its own
bulk, in a mode it visits a handful of times in four hundred, which is exactly the shape iteration 5
said not to trust. The change's is 0.94: its minimum is in its bulk. So the *base's* minimum is the
unrepresentative figure, the 8.5 % it produces understates the change, and reading only the minimum
would have priced this at less than half its worth. One base run of six capped — never reproduced
its own best — while all six change runs confirmed.

### The figures are not the ones iteration 5 published, and the code is identical

Iteration 5 measured this same stage, on this same machine, from the same commit, and recorded a
**minimum of 5.08 ms and a median of 11.40**. This window puts the minimum at 3.88–4.71 and the
median at 3.83–5.39. The minimum is 20–30 % apart. **The median is a factor of 2.2.** That was
chased before the A/B above was believed.

| Eliminated | How |
| --- | --- |
| The source moved | `git diff a4e4f9b pass9 -- Data/` is doc comments only; not one line the exposure walk executes |
| The instrument moved | `StageLab`'s 180 changed lines are the median, the samples list, the stopping reason and the CSV columns — none of them inside a stopwatch |
| The build configuration | Debug and Release, three runs each, one window: exposure medians 4.769 / 4.765 / 4.770 against 4.758 / 4.753 / 4.774. `M13` holds |
| The stage order | All eight stages forward and reversed, four hundred repeats, two processes each way. Six of the eight agree on their minimum to within 3 %; the two that move most are exposure at 13.9 % and `roomair` at 5.1 %, both *cheaper* when run earlier. Nothing like a factor of two, and not positional — `place` moved from first to last for 1.9 % and `solver` from last to first for 3.0 % |

*(The order experiment is worth one more sentence, because it did not come back empty. `StageLab.Run`
settles the heap before every stage and each stage builds its own simulation, so the list's order is
*designed* not to carry, and it very nearly does not — but exposure is 14 % cheaper measured third
than measured sixth, which is a real effect that had never been looked for. It is a seventh of what
is being explained here and is left where this iteration found it.)*

What is left is the session, and **what moved between sessions is the shape of the distribution
rather than its position.** Iteration 5's exposure `best/med` was 5.08 / 11.40 = **0.45**; every base
run in this window is 0.81–0.87. The minimum stayed roughly where it was and the body of the
distribution came down to meet it.

**That is the reverse of iteration 5's conclusion for this stage, and it is the finding.** Iteration
5 assigned exposure the *median*, because across its four runs the median agreed to 0.9 % where the
minimum spread 30 %. Both of those are true, and all four of those runs were inside one window.
Across windows it is the median that moves by a factor of two and the minimum that roughly holds.

> **A statistic's reproducibility, measured inside one window, is not a property of the stage.** It
> is a property of the stage *in that session*, and iteration 5's table should be read as one.

**This is not the first sighting, and the earlier one is why the rule is worth strengthening rather
than writing.** Pass 4's iterations 1 and 2 shared a commit — `321c062` was the *after* leg of one
and the *before* leg of the other — and it read 193 ms in the first window and 149 in the second,
thirty per cent apart, with the caution *milliseconds do not survive leaving their window* recorded
under it. That was right, it was left as a caution, and nothing was built to enforce it. Five passes
later the same effect is a factor of 2.2 and it has been quietly re-deciding which statistic a stage
is read with.

**Three levels of the same problem, and this is the one that cannot be sampled away.** Pass 8 found
too few repeats within a process. Iteration 5 found modes between processes of a session. This finds
a level between sessions, and no number of repeats and no number of processes inside one window will
reveal it — every measurement that could is on the wrong side of the boundary.

> **A stage figure is comparable only with one taken in the same window.** Ratios taken as a pair
> inside one window travel; absolute milliseconds do not, and neither does the choice of which
> statistic to read them with. Every A/B in passes 1 to 9 taken as a pair inside one window is
> unaffected — which is all of them, by `M4`. Every *before and after* quoted across two tables of
> different days, including this pass's own start table against anything below it, is not a
> measurement.

**What changes.** `stages.csv` and `samples.csv` carry `taken_utc`, and `stages.csv` the host;
`bench samplestats` reads the stamp and says, above its table, whether the runs it is comparing are
one window, how far apart they were taken, or that they carry no stamp at all — because an artefact
that does not say which session it came from is one that will be compared with another by accident.
`SampleStatisticTests` pins the boundary in both directions and pins that an unreadable or absent
stamp is **not** one window, which is the state of every artefact written before today.

**What is deliberately not done.** No cause is named. Nothing in this repository records what else
the machine was doing two days ago. Naming frequency scaling or thermal state would be a story rather than a finding, and the rules
above do not need one — they follow from the size of the effect and from where it is invisible, both
of which are measured. **One limitation is worth stating plainly:** iteration 5's raw repeats are not
on disk, so the comparison above is against its two published figures rather than against its
samples. `taken_utc` is what makes the next such comparison better than that.

### Cleanup: the mod project had not built for three commits

Iteration 4's own explanatory comment in `Generic.csproj` contained a `--`, which XML comments
cannot. MSBuild refuses the file, and a project file that does not parse does not fail the projects
that reference it — **it leaves the build**. `dotnet build Thermodynamics.Tests.csproj` and `dotnet
test --no-build` went on passing throughout.

`C11` put the mod project in `tests/Thermodynamics.slnx` so a rename in `Core` cannot pass the suite
while leaving the mod uncompilable, and it worked exactly as designed and caught nothing here: it is
the check for a rename, because a rename makes the compile *fail*. `ProjectFileTests` is the check
for a project that never reaches the compiler — every `.csproj`, `.props`, `.targets` and `.slnx`
outside `obj` and `bin`, found rather than listed, asserted to parse. Verified by reintroducing the
exact break.

## Pass 9, iteration 7 — the skip is worthless where it was aimed, and worth two milliseconds where it was not

The exposure refresh walks every node and asks the surface map what each one's six faces see. On a
hull nobody is building the answer is the one the node already had, and it was written anyway. The
proposal was to compare and skip.

**On the stage it was aimed at, it is worth nothing, and that is measured rather than assumed.**

| | base | change | |
| --- | ---: | ---: | ---: |
| exposure, best of 400 | 3.874 ms | 3.865 ms | −0.2 % |
| exposure, median of 400 | 4.214 ms | 4.353 ms | +3.3 % |
| rooms *(control)*, median | 15.703 ms | 15.905 ms | +1.3 % |

*Twelve processes, one window, alternating, six a leg — the same shape as iteration 6's. The
two legs' ranges overlap on both statistics (base 3.738–4.060 against change 3.801–3.901 on the
minimum), so by `M4` the ratio is not believed in either direction.*

**The reason is iteration 6.** The ablation that proposed this change measured 4.52 ms of the stage
in six per-face read-modify-writes and a second pass to total them. Iteration 6 replaced those with
one store — and *one store is what a skip skips*. The two changes are not additive; the first took
the prize, and what is left is the walk and the surface probe, which the skip does not touch. This
is the pass's own `E10` in miniature: a saving priced against the code as it was, banked against the
code as it is.

### What it is worth is on the next step, and it had never been looked at

`StateDirty` is the other half of a write. A node that is written asks the solver to re-mirror its
row into the flat arrays a step reads, and `SyncNodeState` does that at the top of the next step in
**one unsliced pass**. So a refresh that changed nothing was also asking for all 126,731 rows to be
rewritten, and a room-mapping pass completes on any structural or venting change.

| at 126,731 blocks | mirror every node | mirror none |
| --- | ---: | ---: |
| minimum of 400 | 1.695 ms | **0.124 ms** |
| median of 400 | 2.133 ms | **0.145 ms** |
| per node | 16.8 ns | 1.1 ns |

*`bench stages --stages syncdirty,syncclean`, four processes in one window. Both stages capped
on three runs of four and both spread 25–33 % between runs, so neither figure is reproducible on its
own — but the legs are fourteen times apart and never come near each other: the slowest *mirror
none* reading in four hundred is 0.157 ms against a fastest *mirror every node* of 1.302. A ratio
that large does not need a statistic that reproduces to 1 %, and saying so is the point of reporting
both.*

**So the change costs a remap about 2.0 ms less, and the saving is the unsliced part.** The exposure
refresh itself is budgeted and spread across frames; the mirror it forced was neither. That is the
same defect as [load-and-hitching.md](load-and-hitching.md)'s property 8 — a budgeted pass asking
for unbudgeted work when it finishes — one level further down, and it is now property 11 there.

### Two seams this needed, and why each is where it is

`SyncNodeState` became `internal`. What a full mirror costs cannot be read off a fifteen-millisecond
step, and the harness already has a seam for exactly this — `TestVisibility.cs` exists so a pass can
drive an internal without it becoming public API the mod never calls. The stage it enables prices
both sides of one flag on one grid, which is a thing no A/B of two binaries can do.

`SetExposedFaces` returns whether anything moved, and the solver counts it as
`Work.ExposureNodeWrites`. **The claim a skip rests on is that the gate engaged**, and a gate that
never fires makes two runs agree perfectly (`E8`); `ExposureSkipTests` asserts that a second refresh
over an unchanged hull writes zero of 126,731 and leaves zero nodes marked, that one moved face is
still written and marks exactly one, and — against the code the skip replaced, six per-face writes
and an unconditional refresh — that twenty steps of both produce bit-identical temperatures and
bit-identical per-mechanism watts (`D8`).

**One case needed a guard rather than an argument.** The skip compares the total as well as the
packing, because the single-face setter writes the packing without touching what it derives; a node
left in that state would otherwise skip and keep a stale radiating area, which is a wrong answer
rather than a slow one. It has no caller under `Data/Scripts` and the comparison costs one integer,
which is the right price for not having to reason about it again.

## Pass 9, iteration 8 — the one column a load-path change is judged by was a single sample

[benchmarks.md](benchmarks.md) states the report's method in one sentence: *every case is timed
three times and the fastest kept*. Two of its figures were not, and both had been single samples
since the day they were written.

**The ladder's `build` column** is the figure a load-path change is judged by — every load pass in
this document quotes it — and it was one stopwatch around one build. **The `calibration` row** is
worse placed: it is the divisor two machines' reports are compared through, so a single sample there
puts a whole sample's noise into every cross-machine figure, twice, once from each side.

Both now repeat `Repeats` times and keep the fastest, and `bench report --repeats N` exposes the
dial so the claim can be put to the question instead of believed. This is what it was worth:

| rung | as it was, spread of 4 runs | as it is, spread of 4 runs | median |
| --- | ---: | ---: | ---: |
| 8,000 | 0.9 % | 7.4 % | −0.2 % |
| 32,000 | 28.3 % | 23.0 % | **−11.2 %** |
| 125,000 | 8.4 % | 18.8 % | −2.9 % |
| 500,000 | **19.0 %** | **3.4 %** | −3.9 % |
| 1,000,000 | 6.8 % | **1.1 %** | −8.8 % |

*Eight runs of one pinned binary alternating `--repeats 1` and `--repeats 3`, one window
(`M7`). `median` is the change in the middle of four runs.*

**It does what it is supposed to at the rungs where the build is large enough to matter, and
nothing at the ones where it is not.** At half a million and a million blocks the spread between
runs falls from 19.0 % to 3.4 % and from 6.8 % to 1.1 %; below that it moves either way, which is
what four draws of a small quantity do. The median falls at every rung — a minimum of three cannot
be higher than a minimum of one, on average — most at 32,000 and 1,000,000, which says how high the
single sample had been sitting.

**The consequence for what is already published is bounded and worth stating.** A `build` column
whose between-run spread is 19 % cannot resolve a change smaller than a fifth, so every load-path
figure quoted off the 500,000 rung was carrying that, and the figures themselves were biased high by
a few per cent. What is *not* affected is anything measured by `bench stages`, which has had a
settling rule since pass 8 and both statistics since iteration 5.

*(One caveat the pass's own rules require: four runs is a thin basis for a spread, and all eight
were inside one window, so what is measured here is the within-window spread only — iteration 6's
between-session component is not in these numbers and cannot be removed by a repeat.)*

### Fixing the two is not the check

A third single-sample column would arrive exactly the way these two did: as a row that looks like
every other row. `EveryTimedCaseInTheReportIsRepeated` parses `PerformanceReport.cs` and fails on
any `Stopwatch.StartNew()` with no enclosing loop over `Repeats` — the shape, not the instance. It
reads the source rather than the report, because a single sample and a fastest-of-three produce the
same kind of number and that is the whole problem (`P2`). Verified by reintroducing the exact
defect: it names the line.

`RepeatBuild` also asserts what a repeat makes assertable. Two builds of one dealt hull must be two
builds of the same graph, or the fastest of them is the fastest of two different measurements; it
throws with both counts rather than reporting a figure. The hull is dealt once and built from
repeatedly, because dealing it is the census generator at about ten times the build it feeds
(`C26`) and is not what the column is about.

## Pass 9, iteration 9 — a one-cell block's walls do not depend on where it is

The `place` stage allocates **36 MB a repeat** at 126,731 blocks, and a census hull is mostly
one-cell blocks — armour cubes. Each was given a `Vector3I[1]` for its cell and an `int[1]` for its
surface bits. The cell array holds the block's own `Min` and cannot be shared. **The surface array
holds bits that are a function of the model, the orientation and the layer, and of nothing else** —
so one array serves every block of that model in that orientation, on every grid in the session.

| | base | change | |
| --- | ---: | ---: | ---: |
| place, best of 400 | 9.959 ms | 8.739 ms | **−12.2 %** |
| place, median of 400 | 11.518 ms | 9.716 ms | **−15.6 %** |
| place, allocated a repeat | 36,044 KB | 32,084 KB | −11.0 % |
| place, ns a block | 78.6 | 68.9 | |
| rooms *(control)*, median | 15.489 ms | 15.329 ms | −1.0 % |

*Twelve processes, one window, alternating, six a leg. Neither statistic's ranges overlap —
the slowest change minimum is 8.873 against a fastest base of 9.476, and the medians are 10.951
against 10.984 — so by `M4` the ratio is believed on both. Block count is 126,731 in all twelve.*

**Eleven per cent of the allocation buys twelve to sixteen per cent of the time**, which is more
than proportional and is the reason to remove an allocation rather than to make it smaller: what
goes with it is the collection it would have caused and the cache line it would have dirtied.

### The first version of it allocated more, and the lab said so before the clock did

The cache was first written as one call taking the rotation as a `Func<int, int>`, so the model
could rotate the bits itself on a miss. A method group converted at a call site on the placement
path **allocates a delegate per block** — larger than the `int[1]` it was there to save. The stage
row said `place` allocated **40,005 KB** against the base's 36,044, and the timing had not moved.
Asked and stored in two calls instead, with the caller doing the rotation, it is 32,084.

That is the allocation column earning its place. It was added in pass 3 because a stage that churns
the heap changes what the stage after it measures; here it is the column that distinguished *this
change does nothing* from *this change is backwards*, which no timing in that window did.

### What sharing an array costs, and what pays for it

**A refresh no longer hands back a fresh array for a one-cell block**, and that had been written
down as the mechanism `ThermalSimulation.RefreshBlock` relies on to keep the old references as a
snapshot. What it actually relies on is `SameSurfaces`, which compares **by value** — so an array
compared against itself reports *unchanged*, which is the right answer when nothing changed, and a
door cycling still moves the live layer onto a *different* interned array holding different bits.
Both directions are asserted rather than argued: the door's round trip, open and shut, and the
multi-cell block that still gets a fresh array because only the one-cell path is interned.

Two tests that were already there turn out to be the end-to-end statement of it, and they pass
unchanged: `AMountingChangeDoesNotAskForARemap` — a one-cell block refreshed with nothing altered
must not trigger a flood fill, which is the case where `before` and `after` are now the *same
object* — and `ADoorOpeningIsResolvedThroughItsPortalRatherThanARemap`, which is the case where they
must differ. `SameSurfaces`'s by-value comparison is what makes both true, and its summary says so
now rather than leaving the next reader to find out by changing it.

**The real cost is aliasing.** One write through `block.SelfSurfaces[i]` would change every other
block of that model and orientation in the session, and the failure is invisible — a neighbouring
block's walls quietly move and every downstream answer stays self-consistent. Nothing does it
today, and `NothingWritesThroughABlocksSurfaceArrays` parses every `.cs` under `Data/` and `tests/`
and fails on an element assignment whose target is `Cells`, `SelfSurfaces` or `StructuralSurfaces`.
That is the same shape as iteration 8's check and for the same reason: the instance is not the
problem, the next one is.

**And the interning put a landmine under its own oracle, which is worth recording because the
review that found it was a re-read rather than a failing test.**
`BuildGridSurfacesWalkingTheCells` is the walk the one-cell fast path is pinned against, and it
writes its answer *into* the arrays the instance holds — which for a one-cell block are now the
shared ones. It writes equal values today, so everything passed. But if the walk ever disagreed
with the fast path it would overwrite the value it exists to disagree with, and the comparison that
followed would be of one array against itself: **a check that passes exactly when it should fail**,
and a cache corrupted for every other block of that model and orientation in the session. The walk
detaches its arrays first now, and the pinning test asserts the two sides are two sides —
`Assert.NotSame` before `Assert.Equal`, verified by removing the detach.

The race is benign and is stated where the cache lives. Two threads filling one slot compute the
same value — the bits depend on nothing else — and a reference write is atomic, so the loser's array
is garbage rather than a wrong answer. It is the same shape as `fractionsByOrientation`, which has
been doing this since pass 2.

## Pass 9, iteration 10 — what the pass moved, and the control that was not one

The pass measured against its own start (`M7`): the binary at `a4e4f9b`, the merge it opened from,
against the binary at its tip, six of each alternating in one window at 126,731 blocks and four
hundred repeats a stage.

**Through `bench stages` and not `bench report`, deliberately.** Iteration 8 made the ladder's
`build` column a fastest-of-three where it had been one sample, so a start-against-tip comparison
through the report would be two different instruments — which is `A11`'s finding, and the reason
this page has a pass 8 at all. The stage lab's own changes over this pass are the median column, the
samples file and the stamp: all reporting, none inside a stopwatch, so the two legs are one
instrument.

| stage | start | tip | | |
| --- | ---: | ---: | ---: | --- |
| place, minimum | 8.994 ms | 7.905 ms | **−12.1 %** | ranges do not overlap |
| place, allocated a repeat | 36,044 KB | 32,084 KB | −11.0 % | exact |
| exposure, minimum | 4.699 ms | 4.468 ms | **−4.9 %** | ranges do not overlap |
| rooms *(control)*, minimum | 13.651 ms | 14.263 ms | +4.5 % | **ranges overlap** |

*Minimum only, because the start binary predates iteration 2 and its `stages.csv` has no median
column. That is the pass's own theme arriving one more time: a pass that improves its instrument
cannot measure itself with the improved instrument on both legs.*

**`place` reproduces iteration 9's figure to a tenth of a per cent** — −12.1 % here against −12.2 %
there — measured on a different day, in a different window, with a different stage list. That is the
strongest single piece of evidence in this pass that a within-window ratio travels even where an
absolute figure does not.

**`exposure` is −4.9 % where iteration 6 measured −8.5 % on the same statistic.** Both legs' ranges
are tight and do not overlap, so both are real; the difference is that iteration 6's *base* had a
minimum sitting in a rare fast mode (`best/med` 0.82) and this window's start leg does not — its six
minima span 0.6 %. The median, which iteration 6 measured at −19.5 %, cannot be checked here at all.

### The control moved, and the control was the finding

`rooms` reads 13.65 ms on the starting binary and 14.26 on the tip, and **no iteration of this pass
went near the room pass.** The ranges overlap, so by `M4` it is not a believed movement — but it
reproduces in five of six pairs, which is not what noise looks like.

The suspect is the stage before it. Pass 3's first iteration established that a stage churning the
heap changes what the stage after it measures, and answered it by settling between stages. So: the
same two binaries, `rooms` on its own, nothing run before it.

| | after `place` and `exposure` | on its own |
| --- | ---: | ---: |
| start | 13.651 ms | 14.075 ms |
| tip | 14.263 ms | 14.117 ms |
| | **+4.5 %** | **+0.3 %** |

**Run alone the two binaries agree to three parts in a thousand.** Settling was not enough: a
collection reclaims the garbage and leaves the heap it was allocated into, so a stage carries the
allocation history of the stages before it — and iteration 9 changed exactly that, cutting `place`
from 36,044 KB a repeat to 32,084, which over four hundred repeats is 14.4 GB of churn against 12.8.
The control did not drift. **The change moved it**, through a channel nobody had looked for.

**So a control that runs after the changed stage in the same process is not a control**, and that is
true of iterations 6, 7 and 9's controls as well. It does not overturn them — each of those measured
a ratio between two binaries with the control measured identically in both legs, and each control
read within 2 % — but it is luck rather than method, and it is now removed as a matter of method.

**It also explains iteration 6's loose end.** That iteration found exposure 13.9 % cheaper measured
third than measured sixth, in a forward-versus-reversed stage order, and recorded it as a real effect
with no mechanism. This is the mechanism: measured third its predecessors are `solver` and `roomair`;
measured sixth they are `place`, `register`, `surfaces`, `links` and `rooms` — five stages of churn,
`place` alone allocating 36 MB a repeat.

**`bench stages --isolate` runs one stage per process.** The child is this same executable with one
stage named, writing its own `stages.csv`, which the parent reads back and puts in one table; a child
that fails takes the run with it rather than leaving a table quietly short of a row (`E4`). Reading a
row back is pinned in both directions — every column a comparison uses survives the round trip, and
an artefact missing a column is refused by name rather than parsed into a plausible row, which is
exactly what this pass's own starting binary writes.

### What the pass did

Nine iterations, two of them changes to the shipped model and six of them changes to the instrument
that judges it. That ratio is the pass.

**What moved:** the exposure stage's six per-face writes became one store (−19.5 % of its median),
and one-cell blocks stopped allocating a surface array each (−15.6 % of `place`'s median, −11 % of
its allocation). Both are on the load path, which is where the mod's stated goal lives — a million
blocks at 1.0 simulation — and neither touches a settled step.

**What was refused, and why that is the more useful half.** The exposure skip does nothing to the
stage it was aimed at, because iteration 6 had already removed the writes it would have skipped; it
is kept for the 2.0 ms of unsliced node-state mirroring it stops forcing onto the step after every
room remap, which is a different claim measured a different way.

**What was found about the instrument**, in the order it was found: five stages of eight never
reproduced their own fastest reading; no summary of a stage's repeats reproduces for more than three
of the eight, and two have none; a stage figure belongs to the session that took it, and which
statistic to read it with belongs to the session too; the report's two most-compared columns were
single samples under a page saying otherwise; and a stage carries the heap its predecessors left.
Every one of those was found while trying to measure something else.

**What that says about the six passes before it.** Passes 6 and 7 rejected seven changes on an
instrument that, on this evidence, could not have judged them. Pass 8's audit called them unproven;
iteration 5 called them unjudgeable. Nothing here recovers them — each takes a pairing of its own —
but the instrument that would settle them now exists, reports both statistics, says when it did not
settle, says which session it came from, and can be told to give every stage a process of its own.

---

## Pass 10, iteration 1 — the ladder had gone stale by four to fifteen times

The pass opened the way every pass opens, by measuring — and the measurement disagreed with the
page it was going to be compared against. `bench scale` re-run on 2026-09-01 against the ladder in
[load-and-hitching.md](load-and-hitching.md#the-ladder), at a million blocks:

| column | published | 2026-09-01 | |
| --- | ---: | ---: | ---: |
| build | 5,429 ms | **582 ms** | 9.3× |
| topology | 421.4 ms | **121.7 ms** | 3.5× |
| room mapping | 2,122.4 ms | **144.8 ms** | 14.7× |
| exposure | 512.6 ms | **43.5 ms** | 11.8× |
| a settled step | 118.2 ms | **61.3 ms** | 1.9× |
| resident | 1,510 MB | **610 MB** | 2.5× |

**None of that is new work.** Every one of those columns was moved by a pass between the second and
the ninth, and every one of those passes wrote down what it moved in its own change log — the room
flood a run at a time, the sealing snapshot, the bitset, the interior scan's word skip, the packed
surface entry, the one-cell paths, the hoisted sum. The table at the top of the page, which is what
four other pages quote, was never re-run. This is `E5` in its purest form and the same shape as the
room-map convergence figure `D2` carried for nine days: **the headline outlived the paragraphs that
superseded it**.

**Part of the gap is the hull, and it is named rather than left in.** Today's census puts 1,826,153
links on the million-block ship where the old table had 2,114,111, so the topology and step columns
are not like-for-like. The bounding boxes are identical at every rung, so the room column is: 14.7×
on the same volume.

**What it changes about where to look next.** The load path is no longer 5.4 s of build against 2.1 s
of room mapping; at a million blocks it is 582 ms of build, 145 of rooms, 122 of topology and 43 of
exposure, and the largest single item on it is placing blocks. The stage lab agrees — `rooms` reads
**9.2 ns a cell at 507,542 blocks against 8.7 at 126,731**, which is flat, so the room pass is not
the superlinear thing the old ladder implied.

## Pass 10, iteration 2 — the node diagnostics come off the node

`E4`, scoped by two earlier sessions and built here. Seven per-mechanism watt floats sat on every
`ThermalNode` for the crosshair readout, the block overlay and the telemetry report; the solver
writes them only inside its `CollectDiagnostics` branch, which the shipped configuration leaves
off. They are a `NodeDiagnostics` record now, made by the first non-zero write.

| stage, 126,731 blocks | before | after | |
| --- | ---: | ---: | ---: |
| `register`, allocated | 23,070 KB | **20,100 KB** | −12.9 % |
| `place`, allocated (control) | 32,084 KB | 32,084 KB | — |

**24 bytes a node**, against a prediction of 20 — a reference costs eight of the twenty-eight saved,
and the object size rounded the other way. About 3 MB on this hull and 24 at a million.

**The timings from that window are not a claim.** `place`, which this change cannot reach, moved
2.6 % between the two runs, so every ratio taken beside it is inside the noise (`M5`, `M7`); the
allocation column is what carries, and it is the column `E4` was about.

**Two shapes were refused before either was built.** A `float[nodes × 7]` keyed on the node's index
has to be reshuffled by both removal paths — the dirty one renumbers every node after the hole, the
incremental one moves a node between slots — so until the next step rewrote them, every node past a
removal would read its neighbour's watts on the overlay, at the moment a block is destroyed. And a
shared sink for zeroes written to a node that has no record would be static state two grids on two
threads could reach, which `EveryPieceOfStaticStateInTheCoreSaysWhyTwoGridsMayShareIt` refused —
correctly, and before it ever ran in a session.

## Pass 10, iteration 3 — what a room pass allocates, and the row that said otherwise

`bench memory` reports a **RoomMapper peak** of 18.4 MB above the solver on a 126,731-block hull —
152 bytes a block — under a description saying that is "a visited bit and a sealing byte per cell of
the box". Those two are 1.125 bytes a cell, which on this hull's 1,499,616-cell box is **1.7 MB**.
The row is ten times its own explanation.

**The row is right and the description was wrong.** The peak is sampled with
`GC.GetTotalMemory(false)`, which counts what has been allocated and not yet collected — the figure
a process needs headroom for, and deliberately so. What it is not is the mapper's live footprint,
which is what the sentence beside it claimed. Fixed there rather than here.

**What the difference is made of is the useful half.** The stage lab says a room pass allocates
**4,768 KB** at this size, and the mapper builds a fresh `RoomMap` per pass: a cell store sized from
the last map's count (3.3 MB of `Vector3I` here), two bitsets over the box, the range arrays, and an
exact-size copy of the store at publish when it has slack. A sealing change requests a restart, so
this is not only a load-path figure — **a door built or destroyed on a large ship costs a pass, and
a pass costs its own map.** At a million blocks it is about 38 MB of garbage per remap.

**The obvious fix is to recycle the map, and it is not safe as things stand.** Two buffers
alternating would remove nearly all of it — but `ThermalSolver.exposureMap` holds the published map
across a *budgeted* exposure refresh, which can span several publishes, so a map recycled two
generations back can still be the one a refresh in flight is reading. Recycling needs an ownership
rule before it needs code, and that is a row rather than an iteration: `D20`.

## Pass 10, iteration 4 — the recycled map is pinned, and the instrument was reading the warm-up

**What was found.** `D20`'s change was already in the tree — commit `a8ac54f` gave `RoomMap` a
capacity-keeping `Reset` and the mapper three rotating slots — and it had arrived with no test, no
after-measurement and no record on this page. Re-taking the stage baseline for this iteration read
**4,881 KB** allocated per room pass at 126,731 blocks, indistinguishable from the change never
having landed. The change was fine; **the instrument was sampling the mapper's second pass ever**,
and with three slots the first three passes each build a slot — recycling begins on the fourth.
`StageLab.Rooms` took its allocation figure at repeat 1, which is warm for every other stage and is
slot-building for this one. That is `P2` inside the lab itself: the blind spot read as a value, in
the direction that makes a real saving look like nothing.

**What the design actually is, written where the next reader looks.** The ownership rule `D20`
stated — a publish restarts any refresh in flight — is not what was built. What was built is
stronger for the holder and needs no restart: **a published map may be read until the publish after
next**, so the map free to be recycled is the one *two* publishes old, and the mapper rotates three
slots rather than swapping two. The solver's held `exposureMap` is re-begun on the first `Update`
after any publish, so it is never more than one publish old and sits inside the window with a
publish to spare.

**Pinned** (`D8`, the half that was missing). `RoomMapRecyclingTests`: the published maps cycle
with period three and no fourth instance ever appears; a map two publishes old still answers —
counts, venting, per-cell rooms — *while the pass recycling its successor is in flight*, which is
the exact moment a two-slot swap would reset it under its reader; a recycled map is cell-for-cell
identical to a fresh one, through the same `RoomMapAssert.SameMap` the snapshot suite compares
with, after proving the fixture recycled at all (`E8`); and a warm pass allocates less than a
tenth of a cold one. The lab's own claim is pinned too:
`EveryStageReportsWhatItAllocatedAndTheSteppingPathAllocatesNothing` now requires the rooms row
under 64 KB.

**What it measures, on the corrected instrument.** `bench stages --isolate --stages rooms`,
126,731 blocks, the allocation sample moved to the first recycled repeat:

| rooms, 126,731 blocks | slot-building pass (old sample) | steady-state pass | |
| --- | ---: | ---: | ---: |
| allocated | 4,881 KB | **0 KB** | — |
| best / median | 14.22 / 14.48 ms | 13.83 / 14.24 ms | within the window's own spread |

The two allocation figures are different questions, not a before-and-after through one instrument
(`M7`, and pass 8's `A11` finding): the first is what a pass costs while the rotation fills — paid
three times, at load — and the second is what every pass after that costs, which is the figure a
door built or destroyed on a large ship pays and the figure `D20` was about. At a million blocks
the steady-state saving is the ~38 MB of garbage a remap no longer makes.

## Pass 10, iteration 5 — the first step was paying the rebuild's bills, and a third of the spike was the JIT

**The instrument first.** `bench firststep` times each of a fresh grid's first steps on its own
clock, with the process's minor page faults and the step's allocation beside the milliseconds, and
`--warm` steps a small throwaway grid first so the JIT compiles the step path before anything is
measured. `D4` had attributed the spike to "first touch of every flat array" — a hypothesis this
page repeated for nine days with nothing measuring it.

**What the spike is actually made of**, at 505,566 blocks in shadow, one window:

| first step, 505,566 blocks | ms | of which |
| --- | ---: | --- |
| cold process | 37.78 | ~11.6 ms is the JIT compiling the step path — paid once per *process*, not per grid (`P4`: the instrument conflated the two, and in a session only the first grid pays it) |
| warm process, before | 26.16 | a full `SyncNodeState` mirror of every row, the whole link-mass fill, **7,484 KB** allocated (the `linkMassFactor` array and its doubling copy — 2 × 4 B × 952,523 links, found by matching the byte count), and 8,874 minor faults |
| warm process, after | **12.53** | against a steady step's 8.3; 12 KB allocated, 1,559 faults |

The faults the old attribution blamed are one to two milliseconds of the twenty-six.

**What changed.** `ThermalSolver.PrepareForSteps` runs the step prologue — the full node mirror,
the link mass factors, the buffers, the conductance totals — on the tick that rebuilt the
topology: at the end of `RebuildAll`, charged to a second topology span rather than to no row, and
in the `Update` topology branch, so a mid-session rebuild (a paste, a grind, a settings revision)
is covered too. Refused while a step is in flight, as `ProfileSubsteps` is and for the same
reason. Exact by construction: it is the same prologue the step runs, and anything that changes
between the two is caught the way it always is — per-node dirty rows, temperatures re-read every
step.

**Pinned.** `StepPrologueTests`: a step after a rebuild performs no full resync (counted by the new
`Work.FullNodeResyncs`, which is the observable the claim needed); the first step allocates like a
later one, on a hull whose link array is proven big enough for the old allocation to have shown;
and a grid prepared on the removal tick steps bit-identically to one that steps unprepared, on
spread temperatures, with the fixture asserting the spread (`E8`). `FirstStepLabTests` pins the
`/proc/self/stat` parse on a command name holding spaces and parentheses, and that an unreadable
line is −1, never the 0 that means "no faults happened" (`P2`).

**Three tests pinned the old timing and one of them was passing by luck.** `ProfilerTests` counts
two topology spans now; `OverheatEventTests`' guard inverts — the lowest-critical bound settles on
the rebuild tick, not the first step; and `FacePackingTests` asserted the array overload leaves a
node dirty on a write that changed nothing, which held only because every node used to arrive at
the assertion still dirty from its own build — the overload's documented contract is the opposite,
and the test now asserts it.

**Where the next iteration starts.** The first *sunlit* step at 126,731 blocks is still 34.9 ms
against 9.0 steady, with **21.6 MB** allocated: the sun-shadow structure's first build, plus a
periodic ~1.8 MB on later steps as the sun drifts. That is its own subject.

## Pass 10, iteration 6 — the sun-shadow sets are bitsets, and the drift churn is gone

**What was found.** Iteration 5 left the sunlit first step at 34.9 ms against 9.0 steady with
**21.6 MB** allocated, and a periodic ~0.9–1.8 MB on later steps as the sun drifted. All of it was
three `HashSet<Vector3I>`s in `SunShadowMap` — the shadowed answer, the building pass and the
queued dedupe — at about forty bytes a member plus growth copies, with the completed pass
*copied element by element* into the answer set on the tick it landed. The same shape the room
map's solid set had before the first pass's iteration 7 turned it into a bitset.

**What changed.** The three sets are `CellBitset`s over the grid box padded by one — the padding
is load-bearing, because the cells walked are the air just outside the hull and a bitset silently
drops a cell outside its box, which would read as lit for ever. Publishing a completed pass swaps
the two bitsets instead of copying, so the superseded answer's words become the next pass's
working set and a warm rebuild allocates nothing at all.

**What it measures.** `bench firststep --sunlit --warm`, 126,731 blocks, one window:

| first sunlit step, 126,731 blocks | before | after | |
| --- | ---: | ---: | ---: |
| step 1 | 34.87 ms | **25.97 ms** | the DDA walk itself is what remains, and it is already budgeted by `SunShadowBudget` |
| step 1, allocated | 21,624 KB | **6,552 KB** | what is left is the pending list buying itself once, retained thereafter |
| later steps, allocated (sun drift) | 864–1,780 KB | **12 KB** | the recurring churn is the half a session feels, and it is gone |

**Pinned**, in `SunShadowMapTests`: the skin cells on the box's own edges are still tracked — the
exact cell an unpadded box would drop and call lit; a restarted map agrees with a fresh one to the
last face fraction after the swap, on a fixture proven to hold both lit and shaded answers
(`E8`); a map reused across a grid that grew covers the skin the new blocks added; and a warm
rebuild allocates under 16 KB. The scenario suite's ray-versus-cube oracle (`E7`) and the 94
standing sun tests held unchanged through the conversion.

## Pass 10, iteration 7 — deriving the face weights costs a tenth of the step: tried and dropped

**The question was already on file.** `E2` records that the solver's six per-node face-weight
floats are derivable from the packed exposure counts "but only by putting a divide back into the
hot loop those arrays exist to feed", and calls the row a cost measurement rather than a packing
job (`D7`). This iteration took the measurement.

**What was tried.** The stored row became a packed long and a stored reciprocal per node — twelve
bytes where the six floats were twenty-four — with each weight derived where it is read as
`count * inverse`: the same integer count and the same reciprocal the row was filled from, so
every product is the same float to the last bit. The whole suite, bit-identity suites included,
was green over it.

**What it measured, and why it is dropped.** `bench stages --stages solver`, 126,731 blocks, the
two binaries proven different, interleaved, two rounds:

| solver stage | best ms | median ms |
| --- | ---: | ---: |
| stored weights, rounds 1 and 2 | 17.35 / 17.28 | 19.03 / 19.01 |
| derived weights, rounds 1 and 2 | 19.14 / 19.00 | 21.03 / 20.86 |

**+10 % on both statistics, in both rounds, on identical work** (560 substeps either side). The
five shifts, masks and int-to-float conversions per node run in the environment fill, which pass 5
measured as nearly half the step — a place where five extra operations per face are not free. The
twelve bytes a node they would have saved are 1.5 MB on this hull. Reverted in the same branch, so
the attempt is in the history and the tree carries no code that measured as a regression (`M5`, in
the direction that refuses harm rather than nothing).

**What it closes.** `E2`'s open half is now a measured refusal: the mirrored weight row stays
because it is cheaper to read than to derive, by a tenth of the step. The sun-lit half of the row
was never derivable and stands as it was.

## Pass 10, iteration 8 — the hitch distribution is re-taken, and the lanes are refreshed

**No code moved in this iteration; two instruments were brought back to the truth.**

**The hitch table was three regimes old.** [load-and-hitching.md](load-and-hitching.md)'s
distribution rows predated the bounded step, the spread step and iteration 5's prologue hoist —
the same `E5` shape pass 10 opened on. Re-taken on the play-path instrument (`bench hitch`, 400
ticks, one window): **p99 26.47 → 3.11 ms** at 126,731 blocks and **28.71 → 6.90** at 505,566;
max 60.83 → 24.38 and 208.68 → 50.13. The one outlier left is tick 0, and its attribution is the
per-process JIT — at 505,566 blocks it reads 50 ms with *one substep*, which is not integration —
where iteration 5's warm measurement puts the same first step at 12.5 ms against a steady 8.3. The
superseded rows stay printed beside the new ones, and the medians are marked non-comparable across
the regime change: most ticks under the spread step do not step at all.

**The lane refresh found one straggler.** `tools/lanes/lanes.py` over a fresh trx: 281 classes,
280.1 s of test time, and one class over the threshold untagged — `UncalledCodeTests` at 2.45 s,
which has been growing with the tree it scans. Tagged. The classes this pass added all sit under
the threshold, and the fast lane is **1,923 cases in 4 s**.

## Pass 10, iteration 9 — the memory pages catch up with the passes that changed them

**`bench memory`, both rungs, one window (2026-09-04):**

| | 126,731 blocks | 505,566 blocks |
| --- | ---: | ---: |
| retained | **88.9 MB — 723 B/block** | **360.3 MB — 744 B/block** |
| peak | 100.8 MB — 821 B/block | 408.3 MB — 843 B/block |

Three findings, all `E5`-shaped and none of them code. **The projection has landed**:
[memory.md](memory.md)'s items 2–5 were projected at ~730 B/block and the page still carried
1,023 as "today"; measured, 723. **The flat-with-size claim is back**: the page reported per-block
cost climbing 1,023 → 1,141 between the rungs with the room map as the whole difference (128 → 229
B/block); today the climb is 723 → 744 and the room map's share of it is 53 → 68 — a twentieth of
the total, not the row that matters. **And [known-issues.md](known-issues.md) was two eras stale**,
still telling a reader a grid holds 1.8 KB a block (213 MB retained at 126,731) with half of it
indexed by bounding volume — a description of a mod that predates the packed tables, the bitsets
and the recycled room map. All three corrected in place, with the superseded figures kept visible
(`E10`).

## Pass 10 — what the pass moved

The pass's starting commit (`52f30e2`, the tree before iteration 2's first code change) against
its tip, both built the same way, interleaved in one window at 126,731 blocks, two rounds of
`bench stages --isolate` each, minimum and median both read (`M4`, `M7`):

| stage | start, best (r1 / r2) | tip, best | start, median | tip, median | allocated, start → tip |
| --- | ---: | ---: | ---: | ---: | ---: |
| place *(control)* | 8.140 / 8.141 | 8.148 / 8.113 | 8.82 / 8.81 | 9.02 / 8.82 | 32,443 KB, unmoved |
| register | 3.496 / 3.183 | 3.167 / 3.221 | 4.43 / 4.00 | 3.73 / 3.76 | **23,364 → 20,344 KB** |
| links *(control)* | 12.175 / 12.072 | 11.956 / 11.967 | 12.89 / 12.49 | 12.44 / 12.41 | 0, unmoved |
| rooms | 14.364 / 14.242 | **13.833 / 13.394** | 15.10 / 14.93 | **14.20 / 13.66** | 4,881 → **0 KB** |
| solver | 18.322 / 17.688 | 17.121 / 17.195 | 19.82 / 19.61 | 18.54 / 19.76 | 0, unmoved |

**What is claimed.** The two allocation columns, which are exact: `register` gives back 3,020 KB —
`E4`'s 24 bytes a node to the kilobyte — and a steady room pass allocates nothing where it bought
its own 4,881 KB map (`D20`; the start binary's figure is its repeat-1 sample, which iteration 4
showed is the slot-building pass — the *steady* pass is the comparable claim and the tip's
instrument is the one that can read it). The rooms *timing* reads lower on both statistics in all
four pairs — best −0.5 / −0.8 ms, median −0.9 / −1.3 — which is consistent with the recycled map's
locality and is reported as observed rather than claimed as designed. The solver's minimum reads
lower in all four pairs and its median ranges overlap, so it is not claimed (`M5`). The two
controls are flat.

**What the pass moved that a stage table cannot show**, each measured in its own iteration, in one
window there: the first step of a grid's life 26.16 → **12.53 ms** against a steady 8.3 at 505,566
blocks, warm, allocating 12 KB where it allocated 7,484 (iteration 5); the first *sunlit* step
34.87 → **25.97 ms** at 126,731 blocks with the sun-drift rebuild churn gone entirely (iteration
6); hitch **p99 26.47 → 3.11 ms** at 126,731 blocks and 28.71 → 6.90 at 505,566 on the re-taken
distribution (iteration 8); retained memory measured at **723 B/block**, the page's own items-2–5
projection landed (iteration 9).

**The pass's shape.** Ten iterations to this close, and an eleventh — the benchmark baseline
re-take — as a postscript below it: three that measured and corrected the record (1, 8, 9), three
that landed or completed code (2, 4 finishing 3's landing, 5, 6), one measured refusal kept in the
history with its revert (7), and the instrument corrections without which two of the wins were
invisible — the stage lab's rooms allocation sample read the warm-up as the steady state, and the
first-step instrument conflated per-process JIT with per-grid cost. That is pass 9's ratio again:
half the pass's value was making the instruments tell the truth.

**Where a pass 11 starts.** The stage table above is the floor the mechanisable work has reached:
place and register are `E12`'s measured refusal, links is `D3b`'s six-scheme floor, the room pass
is flat per cell and budget-bound (`D2` — the lever is fewer cells), and the solver is `D1`'s
structural trio — fewer nodes, fewer substeps, fewer visits — of which lumping (`D5`) and multirate
(`D6`) are designed and unbuilt, and `D19`'s parallel fleet ships off pending a session. What is
left on this branch of work is structural or session-gated, and the record now says so with
figures rather than adjectives.

## Pass 10, iteration 11 — the committed baseline had outlived its own era

**What was found.** `tests/benchmarks/performance.csv` — the baseline every `bench report
--baseline` comparison reads — was recorded on **2026-08-22**, four days before `M13` made the
harness compile optimised and two census refreshes ago. Every baseline comparison of the whole
optimised era crossed both boundaries, which is the comparison `M7` forbids; nothing failed,
because `BenchmarkBaselineTests` pins the case-key contract (`M6`) and deliberately not the
values. The same headline-outlives-the-paragraphs shape pass 10 opened on, one instrument deeper.

**What the era-crossing comparison looks like, as a warning rather than a result**: steps read
−50 to −74 % (three eras at once — the optimiser, the census, and the passes), sixteen ladder
rungs at 500,000 and 1,000,000 blocks exist only on the new side, and four sub-millisecond step
shape rows read +200 to +540 % from inside the old baseline's noise floor — which the comparison
itself flags and declines to count (`M5`).

**What changed.** The baseline is re-taken on today's build and committed; iteration-log row 19 on
[benchmarks.md](benchmarks.md#the-iteration-log) carries the pass and the re-take. The first
comparison against the new baseline will be the first one since 2026-08-22 that means anything.

## Pass 10, iteration 12 — the ladder is re-audited, and it survives

**Why.** Pass 10 opened on a ladder that had silently gone stale by four to fifteen times, and
this pass's own iterations then changed the load path under the 2026-09-01 re-take — the step
prologue now runs inside `RebuildAll`, exactly the kind of change that quietly invalidates a
`build` column. So the pass does not close without auditing its own table the way it audited its
predecessors'.

**`bench scale --max 1000000`, 2026-09-04, against the published 2026-09-01 row at a million
blocks:** build **580 against 582 ms** — the prologue's move is invisible in the column it joined —
topology 111.9 against 121.7, rooms 150.5 against 144.8, exposure 41.0 against 43.5, and `settle`
at **exactly the pinned 3,934 ticks**, which is the structural figure
`RoomMapConvergenceIsTheBoxDividedByItsBudget` holds. The published table stands; it now carries
the 2026-09-04 figures with the audit recorded beside them.

**What is deliberately not claimed.** The step column reads 52.5 ms against the published 61.3 and
resident 566 MB against 610, and no change in this pass can explain either — nothing touched the
substep loop, and the pass *added* a retained map slot. Both are left to the session (`M7`): a
figure travels only inside its own window, which is pass 9's closing rule doing its job in the
direction that refuses a free win.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-04 | **Pass 10, iteration 13, the closing sweep**: a clean `--no-incremental` build of the whole solution, 2,260 cases green, the lane checker at exit 0 with `RoomMapCompletionTests` untagged on its cheap-direction report (0.30 s under a slow tag — four cases back in a 4 s fast lane), and the three standing `xUnit2020` warnings fixed. The pass ends with every instrument current: the stage lab, the first-step lab, the hitch distribution, the memory pages, the ladder and the committed baseline all describe the tree as it is. |
| 2026-09-04 | **Pass 10, iteration 12: the ladder is re-audited and survives** — 580 ms of build at a million blocks against the published 582, settle exactly 3,934 ticks, stages inside session spread. The step and resident columns read lower with nothing to explain them and are left to the session rather than claimed (`M7`). |
| 2026-09-04 | **Pass 10, iteration 11: the committed benchmark baseline was the 2026-08-22 unoptimised one for the whole optimised era — re-taken.** The key-contract pins (`M6`) could not see it, because they pin keys and not eras. Iteration-log row 19 on benchmarks.md carries pass 10. |
| 2026-09-04 | **Pass 10 closes.** Start against tip, interleaved, two rounds: register −3,020 KB a repeat (`E4`, exact), a steady room pass at 0 KB where it bought 4,881 (`D20`), rooms timing lower on both statistics in all four pairs, both controls flat, and the solver's overlapping medians left unclaimed. The per-event wins are in their own iterations: first step 26.16 → 12.53 ms warm at 505,566 blocks, first sunlit step 34.87 → 25.97 with the drift churn gone, hitch p99 26.47 → 3.11, memory at 723 B/block. What is left is structural (`D1`, `D2`, `D5`, `D6`) or session-gated (`D19`, `D14`, `D15`). |
| 2026-09-04 | **Pass 10, iteration 9: the memory pages re-measured — 723 B/block retained at 126,731 blocks and 744 at 505,566, against the 1,023/1,141 memory.md carried and the 1.8 KB known-issues did.** The items-2–5 projection (~730) has landed; the room-map slope between the rungs is 21 B/block where the page said 101; nothing is left on the page that predates the current tables. |
| 2026-09-04 | **Pass 10, iteration 8: the hitch distribution re-taken (p99 26.47 → 3.11 ms at 126,731 blocks, 28.71 → 6.90 at 505,566) and the tick-0 outlier attributed to the per-process JIT; the lane refresh tagged `UncalledCodeTests` (2.45 s) and the fast lane is 1,923 cases in 4 s.** |
| 2026-09-04 | **Pass 10, iteration 7: deriving the face weights from the packed counts is +10 % on the solver stage — tried, measured, dropped.** Bit-identical by construction and green through every suite, but 17.3 → 19.1 ms on the minimum and 19.0 → 20.9 on the median, both rounds, identical work. `E2`'s "derivable, but at what cost" question is answered with a refusal: the mirrored row is cheaper to read than to derive. The attempt and its revert are both in the branch. |
| 2026-09-04 | **Pass 10, iteration 6: the sun-shadow sets are bitsets over the padded grid box.** Three `HashSet<Vector3I>`s were 21.6 MB on the first sunlit step of a 126,731-block hull and ~1 MB per sun-drift rebuild; as `CellBitset`s with the completed pass published by swap, the first sunlit step reads 25.97 ms and 6.5 MB (the pending list, bought once) and a drift rebuild allocates nothing. Pinned by four new cases in `SunShadowMapTests`, including the padded-box edge cell an unpadded set would silently call lit. |
| 2026-09-04 | **Pass 10, iteration 5: the first-step spike was a third JIT, half step prologue, and barely page faults at all.** `bench firststep` measures the first steps with faults and allocation beside them; `--warm` separates per-process JIT (~11.6 ms at 505,566 blocks) from per-grid cost. The prologue — full node mirror, link-mass fill and its 7,484 KB allocation — now runs on the tick that rebuilds the topology (`ThermalSolver.PrepareForSteps`), so a warm first step falls 26.16 → **12.53 ms** against a steady 8.3, allocating 12 KB where it allocated 7,484. `StepPrologueTests` pins the no-full-resync claim, the allocation, and bit-identity of a prepared step against an unprepared one; `D4`'s "first touch of every flat array" attribution is corrected in place — the faults are one to two milliseconds of it. What remains of `D4` is the sunlit residual: the sun-shadow structure's 21.6 MB first build. |
| 2026-09-04 | **Pass 10, iteration 4: `D20`'s recycling was in the tree unpinned, and the stage lab read the warm-up as the steady state.** The rooms row sampled allocation on the mapper's second pass, which with three rotating slots is still slot-building, so the row read 4,881 KB after the change exactly as before it. Sampled at the first recycled pass it reads **0 KB** at 126,731 blocks. `RoomMapRecyclingTests` pins the three-slot rotation, the two-publish read window at the moment a two-slot design would break it, cell-for-cell identity of a recycled map against a fresh one, and the warm-pass allocation; the as-built ownership rule — a published map is readable until the publish after next — replaces the restart-on-publish rule `D20` proposed. |
| 2026-09-01 | **Pass 10 opened by re-taking the ladder, and it had gone stale by four to fifteen times**: 582 ms of build at a million blocks against the 5,429 the table carried, 145 of room mapping against 2,122, 43.5 of exposure against 513, a 61.3 ms step against 118, and 610 MB resident against 1,510. Every one of those was moved by a pass between the second and the ninth; the table other pages quote was never re-run. |
| 2026-09-01 | **A room pass allocates its own map**, 4,768 KB at 126,731 blocks and about 38 MB at a million, and a sealing change requests a pass — so this is a play-time figure and not only a load-path one. Recycling the map is `D20`, blocked on an ownership rule: the solver holds the published map across a budgeted exposure refresh. Also corrected `bench memory`'s **RoomMapper peak** row, whose description named the live bit-and-byte footprint (1.7 MB here) for a figure that is the uncollected high-water mark (18.4 MB). |
| 2026-09-01 | **The node diagnostics come off the node** (`E4`): seven floats that only the readouts and the telemetry report ever look at are a record the node makes on the first non-zero write. **24 bytes a node**, measured on the `register` stage's allocation column with `place` as an unmoved control. |
| 2026-08-28 | **Corrected the room map's convergence figure, which had been stale for nine days and was quoted here from `D2`** (`E10`, `E5`). It read 7,207 ticks — twenty minutes — on a million blocks; re-measured by `bench scale --max 1000000` it is **3,934 ticks, about eleven minutes**, on 1,000,294 blocks and a 14,278,796-cell box. The 7,207 predated the 2026-08-26 word skip and the 2026-08-27 span flood, both of which `D2`'s own body already recorded — the headline outlived the paragraph that superseded it. **And the figure is structural**: convergence is the box over a 4,096-cell tick budget, so no work on milliseconds a cell can move it, which `RoomMapConvergenceIsTheBoxDividedByItsBudget` now pins. |
| 2026-08-28 | **Pass 9 measured against its own start, and the control was the finding.** `place` −12.1 % and exposure −4.9 % on the minimum, both with ranges that do not overlap — and `place` reproduces iteration 9's −12.2 % on a different day with a different stage list, which is the pass's clearest evidence that a within-window ratio travels where an absolute figure does not. The `rooms` control moved 4.5 % on a stage nothing touched; **run on its own the two binaries agree to 0.3 %**. Settling between stages reclaims the garbage and leaves the heap, so a stage carries its predecessors' allocation — which is also the mechanism behind iteration 6's unexplained 13.9 %. `bench stages --isolate` gives every stage its own process. |
| 2026-08-27 | **One-cell blocks share their surface arrays per model and orientation**, because the bits do not depend on where the block is. `place` falls **15.6 % on its median and 12.2 % on its minimum** with neither leg's range overlapping, and allocates 11 % less. The first version took the rotation as a delegate and allocated *more* than it saved — 40,005 KB against 36,044 — which the stage lab's allocation column caught and no timing in that window did. |
| 2026-08-27 | **The ladder's `build` column and the `calibration` row were single samples**, under a page that says every case is timed three times and the fastest kept. One is what a load-path change is judged by and the other is the divisor two machines are compared through. Repeated now: the between-run spread at 500,000 blocks falls from **19.0 % to 3.4 %** and at a million from 6.8 % to 1.1 %, and the figure falls a few per cent at every rung. `EveryTimedCaseInTheReportIsRepeated` fails on any stopwatch in the report with no repeat loop around it, because a third would arrive the same way. |
| 2026-08-27 | **An exposure refresh that changed nothing writes nothing, which is worth nothing where it was aimed.** The stage does not move — the two legs' ranges overlap on both statistics, because iteration 6 had already removed the six writes a skip would skip. What it is worth is the full, unsliced `SyncNodeState` it stopped forcing onto the step after every room remap: **2.13 ms against 0.145** to mirror 126,731 rows. That is `load-and-hitching.md`'s property 8 one level down, and it is property 11 there now. |
| 2026-08-27 | **`M7` is scoped to any two figures compared, not to a pass.** The exposure stage's six per-face writes became one, worth **19.5 % of the stage's median** over twelve alternating processes of one window against a flat control — and measuring it found that the same code read 2.2× slower in iteration 5's session, with the source, the instrument, the build configuration and the stage order each eliminated. The minimum roughly travels between sessions and the median does not, which inverts iteration 5's assignment for this stage and makes that whole table a within-window measurement. Artefacts carry `taken_utc` now and `bench samplestats` says whether its runs are one window. |
| 2026-08-27 | The mod project had not built for three commits: iteration 4's own explanatory comment contained a `--`, so MSBuild refused `Generic.csproj` and it left the build instead of failing it. `ProjectFileTests` asserts every MSBuild file in the tree parses. |
| 2026-08-27 | **`M4` takes the median as well as the fastest.** Four runs of one binary, four hundred repeats a stage: no summary reproduces for more than three of the eight stages, and `links` and `register` have none. The link stage's modes are *between processes* — one run of four never left 31 ms while two others found 15 in their first repeats — which revises pass 8's "not bimodal, under-sampled" and makes passes 6 and 7's seven rejections unjudgeable rather than merely unproven. |
| 2026-08-27 | `R14` is checked by the compiler now, not only by a pattern test: twenty-five doc comments named something that was not there, four of them in the shipped mod. |
| 2026-08-27 | Opened pass 9 on the stages the link build's twenty-six iterations crowded out, with an ablation that puts two thirds of the exposure stage in writing its answer down. |
| 2026-08-27 | Opened pass 3, whose first iteration explains the figure pass 2 could not: the instrument, not the surface map. |
| 2026-08-27 | Closed pass 2: ten iterations, six kept, three dropped with their measurements, one the pass summary. World load at a million blocks 3.17 → 2.21 s. |
| 2026-08-26 | Opened pass 2 on the page, with its start figures taken by the instrument the pass begins by putting in the tree. |
| 2026-08-26 | Opened, with the first four iterations of the 2026-08-26 pass: the harness had measured unoptimised code for its whole life, and the ladder's `build` column had been measuring the census generator since `C26`. |
