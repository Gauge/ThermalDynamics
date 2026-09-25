# Load and hitching

What the simulation costs as a grid grows, measured rather than extrapolated; what makes a large
grid stutter; and what live worlds cost when the same questions are asked of them.

> The rules argued here are stated canonically in [rules.md](rules.md): `M4` `M5` `M7` `E3` `D7`.

| Looking for | Go to |
| --- | --- |
| The design for a million blocks, still mostly unbuilt | [scale-design.md](scale-design.md) |
| Why a handful of light fittings sets a capital ship's cost | [stiffness.md](stiffness.md) |
| The repeatable cost report, and what a substep costs | [benchmarks.md](benchmarks.md) |
| Where a grid's memory goes | [memory.md](memory.md) |

> **Read [stiffness.md](stiffness.md) beside the ladder below.** Every figure on this page was
> measured on a synthetic ship whose lightest block is a 200 kg grating, and a field dump has
> since established that a real ship's lightest block is a 16 kg light fitting — which is twelve
> times stiffer and sets the substep count for the whole grid. The benchmark ship asks for two
> substeps where the real one asks for thirty-one, so the steady cost here is optimistic by more
> than the runtime difference. The shapes of the curves and the spike findings are unaffected.

---

## The distinction the whole exercise rests on

Two costs, and confusing them is how a performance problem gets fixed in the wrong place.

**Steady cost** is what a tick costs when nothing has changed. It sets how much of a frame the
mod takes. It degrades *gracefully*: twice the cost is half the simulation rate, which a player
experiences as heat moving more slowly, and a server operator can trade away with `Frequency`.

**Spike cost** is what a tick costs when something *has* changed — a block welded, a section
shot away, a door cycled, a blueprint pasted. It sets whether the game stutters. It degrades
*catastrophically*: a player does not perceive a one-second frame as a slow simulation, they
perceive it as the game breaking.

A million-block grid that costs 100 ms every tick is playable at a low simulation rate. The same
grid stalling for one second when a block is placed is not, and no amount of lowering the rate
helps, because the stall is not the rate.

So the target for this work was never "make it fast". It was: **make the spike proportional to
what changed, and let the steady cost be whatever the arithmetic says it is.**

---

## Measuring it

Three tools, in `tests/`, described in [tests/README.md](../tests/README.md).

```bash
cd tests
dotnet run --project Thermodynamics.Sim -- bench scale                 # the ladder
dotnet run --project Thermodynamics.Sim -- bench spike --size 125000  # one block placed, split by stage
dotnet run --project Thermodynamics.Sim -- bench weld  --size 125000  # a block every tick
dotnet run --project Thermodynamics.Sim -- bench hitch --size 125000  # per-tick distribution
dotnet run --project Thermodynamics.Sim -- bench load  --size 1000000 # what world load costs
```

> **The harness runs on .NET 9; the game runs .NET Framework 4.8, and the factor is now
> measured: six to eight.** The telemetry report divides a grid's solver milliseconds by the
> substep passes it made and the elements each pass walked, and the harness's own benchmarks can
> be divided the same way, so the two are directly comparable:
>
> | | elements | substeps | ns per element visit |
> | --- | ---: | ---: | ---: |
> | harness, 8,904-block ship | 29,683 | 28.1 | **3.97** |
> | harness, 43,232-block ship | 140,095 | 28.1 | **5.67** |
> | field, 1,293-block ship | 4,542 | 16.0 | **24.9** |
> | field, 42,051-block ship | 132,195 | 11.0 | **46.6** |
>
> The 1,293-block field ship is the important row. Its whole working set is a few tens of
> kilobytes and fits in L2 with room to spare, so cache pressure explains none of it — and it is
> still **6.3x** the harness's rate on a grid seven times larger. The capital ship is 8.2x. What
> is left is the runtime: no modern JIT, weaker bounds-check elimination, no vectorisation.
>
> Two caveats on those field figures. Telemetry was collecting in all of them, which switches the
> solver's per-mechanism watt figures on — **and that cost 95 %, not the 7 % this paragraph used to
> estimate.** It has since been measured directly: 3.75 ms against 7.34 ms on a 32,800-block hull.
> The figures are now written on the last substep alone, which brings it to 15 %, but every dump
> taken before that carries the whole 95 %. See
> [benchmarks.md](benchmarks.md#what-being-measured-costs). And the per-element rate includes the
> fixed per-step work — state sync, publish, the stability estimate — so it inflates on a grid
> taking few substeps; both rows above are in a regime where that is a minor term.

> **Both sides of this table have since moved and it has not been re-measured.** The harness rows
> predate the overshoot-clamp gate, the mirrored block ratings and the diagnostic batching, which
> together took the harness from 3.97–5.67 ns per element visit to 1.87–2.01. The field rows
> predate all three as well. The runtime factor cannot be recovered from these numbers — it needs a
> dump taken on a tree that carries them.
>
> So: **multiply every millisecond on this page by about seven** to guess at the game, and treat
> ratios, counts and shapes of curve as the parts that carry across unchanged.

**These benchmarks run one grid.** A world runs hundreds, and the largest single finding of this
work — every grid ticking on the same frame — was invisible to all of them and came from a
telemetry report instead. Benchmarks bound what one grid costs; only the report says what a frame
costs.

`bench scale` builds a hull at each rung of a ladder — 8k, 32k, 125k, 500k, 1M blocks — and times
every stage of an update on its own. The shape is a ship, not a cube, for the reasons in
[scale-design.md §10](scale-design.md#10-grid-shape-changes-the-arithmetic); `--shape cube` and
`--shape truss` are there to compare against.

`bench spike` is the one that says what to change. It places a single block on a settled grid and
reports the **worst call to each stage** across every tick until the grid settles, with the tick
each landed on, and the counts of what each touched. The stages do not land together — the
conduction rebuild is the tick after the placement, the room map converges hundreds of ticks
later, the exposure pass rides on the tick that publishes it — so a single "worst tick" figure
hides two of the three stalls behind whichever happened to be largest.

Everything is deterministic. The temperature spread the benchmarks seed is written out rather than
taken from `Random`, so two runs on the same machine are comparable and two machines differ only
by their speed.

---

## What it measured, and what changed

All figures from this machine, release build, single thread, ship shape.

### The ladder

Measured on hulls built from the block [`Census`](../tests/Thermodynamics.Harness/Census.cs) — the
population of a real ship, rather than the heavy armour and gratings this ladder used to use. See
[the note below](#the-ladder-is-measured-on-a-census-hull-not-an-armour-cube) for what that changed and why the
figures moved so much.

| blocks | links | bbox | exposed | build | topology | rooms | exposure | full step | sub | tick | cap | resident |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 8,904 | 18,333 | 68,800 | 72 % | 34 ms | 5.3 | 5.4 | 2.1 | 0.67 | 7 | 0.64 | 74 | 5 MB |
| 32,800 | 64,964 | 328,640 | 61 % | 51 ms | 3.4 | 16.3 | 1.6 | 1.88 | 7 | 1.34 | 20 | 18 MB |
| 126,731 | 247,350 | 1,499,616 | 49 % | 87 ms | 15.4 | 21.7 | 4.9 | 8.16 | 8 | 5.08 | 5 | 75 MB |
| 505,566 | 952,523 | 6,838,104 | 35 % | 298 ms | 60.5 | 70.8 | 21.0 | 27.39 | 8 | 10.53 | 1 | 307 MB |
| 1,000,294 | 1,826,153 | 14,278,796 | 30 % | 580 ms | 111.9 | 150.5 | 41.0 | 52.50 | 8 | 20.53 | 1 | 566 MB |

*Re-taken 2026-09-04 by `bench scale`, as pass 10's closing audit of its own opening finding: the
2026-09-01 re-take survives — build 580 against 582 at a million blocks, the structural `settle`
count exactly the pinned 3,934 ticks, and every stage column within the instrument's session
spread, with the step prologue's move onto the rebuild tick invisible in the `build` column. Two
columns read lower than 2026-09-01 for no change that could explain them — the step (52.5 against
61.3) and resident (566 MB against 610) — and both are left to the session (`M7`) rather than
claimed. **The table the 2026-09-01 re-take replaced had gone stale by four to fifteen times** — it read 5,429 ms of build, 2,122 ms of room mapping and 1,510 MB resident at a million
blocks, against 582, 145 and 610 here. Every pass from the second to the ninth moved these columns
and each recorded what it moved in its own change log; the table at the top of the page, which is
what other pages quote, was never re-run. That is `E5` in its purest form — a headline outliving the
paragraphs that superseded it — and it is the same shape as the room-map convergence figure `D2`
carried for nine days. **Part of the difference is the hull**: today's census puts 1,826,153 links on
the million-block ship where the old table had 2,114,111, so the topology and step columns are not a
like-for-like ratio. The bounding boxes are identical at every rung, so the `rooms` column is.*

**`sub` is 7 to 8 rather than the 12 this table used to read**, which is the stability estimate
asking for less on a hull whose block mix has changed, not a ceiling. The `full step` column is the
cost of the step the default settings actually take.

**`exposed` falls as the hull grows**, from 72 % to 30 %, which is the bulkheads: they are solid
slabs of the fuselage's cross-section, so a wider ship buries a larger share of itself. That
monotonicity is also the check on the row above it — see
[the mapping limit](#the-room-map-gave-up-above-seven-hundred-thousand-blocks).

Against the same ladder before the overshoot-clamp gate, the mirrored block ratings and the
diagnostic batching, the full step at a million blocks was 623 ms and the tick 55 ms. The whole
table is a fresh measurement; earlier versions of this document quoted the older one.

### The room map gave up above seven hundred thousand blocks

The `exposed` column found a defect that no timing would have. It read 95 % at a million blocks
against 35 % one rung down — a hull that had somehow become nearly all skin.

`RoomMapper.RunToCompletion` carried a flat safety limit of twenty million cell visits. The flood
walks the grid's **bounding volume**, not its block count, and this ladder's hulls fill about 7 % of
their bounding box — so twenty million is passed at around seven hundred thousand blocks. Passing it
returned without publishing anything, leaving `RoomMap.AllExternal` in place: no compartments, every
cell external, every interior block classified as facing open space.

| blocks | exposed, before | exposed, after | rooms, before | rooms, after |
| ---: | ---: | ---: | ---: | ---: |
| 505,566 | 35.5 % | 35.5 % | 67 | 67 |
| 649,382 | 33.6 % | 33.6 % | 75 | 75 |
| 804,593 | **94.8 %** | 31.8 % | **0** | 82 |
| 1,000,294 | **95.1 %** | 29.9 % | **0** | 87 |

In game the consequence is a grid that finishes its load radiating and convecting from every block
it has, with no compartment holding air, until the budgeted per-tick path walks the same flood again
— thousands of ticks later. In this ladder the consequence was that the top rung's every figure
described an unmapped hull while sitting in a column beside four mapped ones.

The limit is now derived from the volume the pass is about to walk. The flood cannot visit a cell
twice — `visited` is a bitset over the search bounds and the interior scan covers each cell once —
so four times the volume is a guard that cannot bind on a pass that is going to finish, which is
what a guard should be. `RunToCompletion` also returns whether it completed, and the ladder throws
rather than reporting a row measured on an unmapped hull.

The topology, rooms and exposure columns are each stage run **whole**, which is what a one-shot
rebuild or a world load costs. They are not what a tick costs; every one of them is now spread.

**`full step` and `tick` are not the same number, and the gap is the point.** `full step` is a
step of the whole configured length with as many substeps as the grid's stiffness asks for. `tick`
is what a tick actually pays once `MaxElementVisitsPerStep` has shortened the step to fit, and `cap`
is how many substeps that leaves. Below about a hundred thousand blocks the budget never binds and
the two agree. Above it they diverge, and that divergence is the trade being made: at a million
blocks a tick pays **25.87 ms instead of the full step's 118.20**, and simulated time advances more
slowly to pay for it — `cap` is 1 substep against the 12 the grid's stiffness asks for.

**The solver scales, and it now scales flat.** Cost per link visit was 7.8 ns at 8k and 18.4 ns at
a million — a doubling across a working set growing from ten megabytes to a gigabyte, read at the
time as an unavoidable cache effect. It is 4.7 ns at both ends now. The doubling was not the working
set: it was two passes reaching through the node objects on every visit — the damage check's rating
and the per-mechanism watt diagnostics — into memory that got further apart as the grid grew. Both
now read or write flat arrays. Nothing in the conduction pass was ever superlinear, and the part
that looked like it was has gone.

### The ladder is measured on a census hull, not an armour cube

Every figure above moved when the hulls did, and the full step at a million blocks moved by six
times — from 102 ms to 623 ms. Nothing about the solver changed. What changed is the ship.

A step is divided into as many substeps as the **stiffest** block on the grid needs, so the cost
of a grid is decided by its *lightest* block and not by its average one. This ladder used to build
heavy armour with a 200 kg grating in eight, and a real ship's lightest block is a 16 kg light
fitting — twelve times lighter, and therefore twelve times stiffer. The benchmark hull asked for
three substeps where field dumps measure twenty-one to thirty-one.

So the old ladder was not slightly optimistic. It was measuring a hull an order of magnitude
softer than the ships it claimed to describe, and every "the solver costs X" statement built on it
understated the steady cost by about six. The block mix now comes from the block-type table of a
field dump — see [`Census`](../tests/Thermodynamics.Harness/Census.cs) — and
`CensusFidelityTests` fails if it drifts away from what the reports say again.

The spike findings below are unaffected: they are about work proportional to what changed, and
that is a property of the algorithms rather than of the block mix.

### The spike, before and after

Worst tick after a single block is placed:

| blocks | before | after |
| ---: | ---: | ---: |
| 8,904 | 9.8 ms | **1.6 ms** |
| 32,800 | 23.4 ms | **3.9 ms** |
| 126,731 | 121.0 ms | **13.3 ms** |
| 505,566 | 387.3 ms | **69.8 ms** |
| 1,000,294 | **1,086.3 ms** | **156.9 ms** |

What is left in those figures is almost entirely the solver step that happens to share the tick,
not the placement: the topology stage itself is under a millisecond at every size.

Worst call per stage, on a 127k hull, one block placed:

| stage | before | after | why |
| --- | ---: | ---: | --- |
| topology | 90.5 ms | **0.6 ms** | a placed block is linked, not the grid |
| rooms | 77.2 ms | **12.2 ms** | the interior scan is charged against the budget |
| exposure | 29.7 ms | **2.6 ms** | the pass is resumable |
| solver | 22.5 ms | 22.5 ms | the steady cost, unchanged and now the largest thing left |

### Per-tick distributions

`bench weld` places a block on every tick for 120 ticks — sustained construction, the worst
realistic case for the rebuild path, and the one that never gets a quiet tick to recover in.
`bench hitch` ticks a settled grid for 300 ticks with one block welded a quarter of the way in
and one ground off half way.

| run | blocks | median | p95 | p99 | max | spike | over budget |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| weld | 32,800 | 1.73 | 3.08 | 7.62 | 11.97 | 6.9x | 0 / 120 |
| weld | 126,731 | 7.25 | 8.47 | 16.97 | 20.93 | 2.9x | 2 / 120 |
| weld | 505,566 | 34.27 | 36.71 | 44.53 | 55.47 | **1.6x** | 80 / 120 |
| hitch | 32,800 | 1.97 | 5.70 | 22.28 | 30.33 | 15.4x | 6 / 300 |
| hitch | 126,731 | 10.38 | 17.42 | 49.10 | 78.69 | 7.6x | 27 / 300 |
| hitch | 505,566 | 49.33 | 56.27 | 173.40 | **303.59** | 6.2x | 201 / 300 |

**Welding is smooth, and gets smoother as the grid grows.** A spike ratio of 1.6 on a
half-million-block grid means sustained construction is essentially flat: the ticks over budget
are the steady cost being over budget, not stalls. That is the incremental topology working — the
same run before it would have paid a 200 ms rebuild on every one of the 120 ticks.

The `hitch` rows above were taken before findings 6 to 8. Re-taken 2026-09-04, after the bounded
step, the spread step and the step prologue's move to the rebuild tick, with the worst tick in
each run attributed by stage:

| run | blocks | median | p95 | p99 | max | worst tick is |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| hitch | 126,731 | 0.31 | 2.54 | **3.11** | 24.38 | tick 0 — the process's first walk of the step path, which is mostly the JIT |
| hitch | 505,566 | 0.25 | 5.47 | **6.90** | 50.13 | tick 0, as above (one substep: the 50 ms is not integration) |

*The 2026-09-04 medians are not comparable with the rows they replace — most ticks do not step at
all under the spread step, and the earlier rows' regime stepped whole. What is comparable is the
tail: p99 26.47 → 3.11 and 28.71 → 6.90 on the same hulls. The tick-0 outlier is per-process, not
per-grid: warmed, the same first step reads 12.5 ms against a steady 8.3
([performance.md](performance.md#pass-10-iteration-5--the-first-step-was-paying-the-rebuilds-bills-and-a-third-of-the-spike-was-the-jit)),
and a game session pays its JIT once, on its first grid.* The rows they replace: median 11.28 /
19.34 / 26.47 / 60.83 at 126,731 and 23.96 / 26.36 / 28.71 / 208.68 at 505,566.

The tail is what moved. At 500k the p99 went from 173 ms to 29 ms against a median of 24, and the
median itself halved because a step is now bounded. What is left is a very tight distribution and
one outlier: **the first step** — which, measured properly on 2026-09-04 (`bench firststep`), was
never mostly the first touch this paragraph used to name: the faults are one to two milliseconds
of it, and the bulk was the step prologue (the full node mirror and the link-mass fill) plus the
JIT, which is per process. The prologue lands on the rebuild tick now and a warm first step is
**12.5 ms against a steady 8.3** at 505,566 blocks; the sunlit sun-shadow build is the tail that
remains ([performance.md](performance.md#pass-10-iteration-5--the-first-step-was-paying-the-rebuilds-bills-and-a-third-of-the-spike-was-the-jit)).

The worst call to each stage on those runs is topology 10.5 ms, rooms 10.4 ms, exposure below the
timer's resolution, and the solver everything else. Nothing but the solver is above ten
milliseconds on a half-million-block grid, and the solver is doing arithmetic rather than
bookkeeping.

---

## What keeps the spike proportional

Ten properties the code holds, each with the measurement that shows what it is worth. Each is a
place a tick once cost the whole grid for a change that touched one block, so each carries the
figure it replaced: a before-and-after is the evidence that the property holds, and the dates are in
the [change log](#change-log).

### 1. Placing a block costs its own degree, not the whole graph

Placing one block marked the graph stale and the next tick rebuilt every link on the ship. On a
million blocks that is 456 ms for one block.

A block arriving is the one topology change that needs no demolition: nothing links to a block
that was not there, so its links can be appended and every existing link left as it was. Placed
blocks are queued and the queue is drained by linking each new block's own six faces.

The parts that were proportional to the grid had to go with it — the flat arrays the substep loop
reads are appended to rather than rewritten, and conductance totals are added to rather than
recomputed. See `ThermalSolver.LinkPendingNodes`, and `IncrementalTopologyTests` for the proof
that the graph it builds is the same graph a full rebuild produces.

**Still global:** anything that can invalidate a link that already exists — a block *removed*, a
block whose mounting changed, a new adjacency source. Removal is the one that matters and is
listed under [what is still open](#what-is-still-open).

### 2. The room mapper's budget bounds its scan

The flood fill is budgeted so no tick pays for the whole grid. The walk between one room and the
next was not: it counted as a single unit of budget however far it went, and across a pass that
walk covers the whole bounding box. The tick that finished a pass on a 127k ship swept the tail of
a 1.5-million-cell box in one call and cost 77 ms.

The scan is charged cell by cell now. A pass takes about twice as many ticks and every one of them
is bounded.

The load test asserting the budget was respected passed throughout, because it read the counter
that was not counting the scan. **A budget test is only as good as what it counts.**

### 3. The exposure refresh is budgeted

It walked every node on the grid on the tick a room pass published — the same tick as the
mapper's own worst call, which is how one tick came to cost 109 ms on a grid whose steady cost is
twenty. It is resumable now, in slices scaled off the node count.

Some nodes read the previous map for a few ticks. That is not a new inaccuracy: it is the map they
had been reading for the several hundred ticks the pass took to build.

### 4. A search that finds nothing costs nothing

The coolant loop search and the heat pump rebuild each walked every block asking a question almost
every block answers no to, on every topology change. The grid counts them as they are placed, so a
ship with no plumbing — nearly every ship — skips two passes over a million blocks for an integer
test.

### 5. The mass sweep is a rota, not a walk over every block

Block mass changes with build progress and damage and the game raises no event for either, so the
only way to notice is to look. It looked at every block on the grid, every eight steps, asking the
game for each block's mass. It is a rota now, capped per tick; a grid under the cap is still swept
whole every eight steps.

This is the one finding the synthetic benchmarks cannot see — a harness has no game blocks to ask
— which is why the load numbers are not the whole story and telemetry from a real session is.

### 6. Removing a block costs its own degree

The companion to finding 1, and the harder direction. A removed block's links have to be *found*
before they can be dropped, and scanning the link list for them is proportional to the grid. Its
node also has to leave a list whose indices every link refers to, and taking it out by shifting
everything after it moves every one of those indices at once.

Both are solved by two decisions. Links are indexed per node as an intrusive chain — three `int`
arrays, no per-node collections and no managed references, which is the shape
[scale-design §6](scale-design.md#6-data-structures) asks for — so a node's links are walked in
time proportional to its degree. And a node leaves by having the last node moved into its place,
so exactly one index changes and only the links touching that one node are rewritten.

Everything else holding a node index is repaired for that one change: the coolant loops, the heat
pumps, and the room air. The room air is the one that mattered — it is not rebuilt until a room
mapping pass completes, which on a large grid is thousands of ticks after the block was removed,
so a stale index there would have poured a room's heat into whichever block inherited it.

`IncrementalTopologyTests` proves it against the global builder, including a 400-step fuzz run
that builds and grinds in a generated order and compares the graph against a rebuild after every
single change.

### 7. A step's cost is stable when nothing changes

A step's cost is its substep count times its links, and the substep count is set by the stiffest
node on the grid, which moves as the grid heats. On a 127k hull that produced a step costing 15 ms
most of the time and 70 ms occasionally, from the same grid doing the same thing.

`MaxElementVisitsPerStep` bounds it. When a step would exceed the budget, the step is made
**shorter** rather than its substeps coarser — and that distinction is the whole point.
Coarsening substeps takes steps too large for the stiffness and leans on the overshoot clamp,
which is an accuracy loss. Shortening the step advances less simulated time at exactly the same
accuracy: heat moves more slowly and nothing else changes.

**This is the setting that trades simulation rate for smoothness**, and it is what makes a grid
too large to simulate at full rate run at a lower rate smoothly rather than at full rate in
lurches. `ThermalSimulation.SimulationRate` reports how much of real time a grid is keeping up
with, so a slow grid says so rather than being mysterious. The default is about one 60 fps
frame's worth of link visits; grids below roughly a hundred thousand blocks never reach it.

### 8. Publishing the shadow map touches only what changed

The self-shadow walk was budgeted. Publishing its answer was not: a completed pass called a loop
over every node on the grid, six faces each — 760,000 shadow lookups on a 127k hull — from inside
the step. That was the 69 ms step whose conduction loop accounted for a fifth of it.

The same shape of mistake as the room mapper's, and the same fix: the refresh is spread over
steps. It is advanced inside the pass rather than at the top of a step, so a grid small enough for
the budget to cover in one go still finishes in the same substep that completed the pass.

### 9. Grids are staggered across frames

The one the benchmarks could never have found, because they run a single grid.

A field run of a 203-grid save: of 13,915 frames, **1,392 did any thermal work at all** — one in
ten, exactly — and each of the worst frames carried **183 grids**. Those frames averaged 117 ms,
worst 611 ms, and two in three exceeded a 60 fps frame. The session total was 20 % of real time,
which is a throughput number and a survivable one. Arriving in one lump every tenth frame is what
made it a stutter.

The first fix for this gave each grid one of ten phases and ticked one phase per frame, balanced by
block count. It helped and it was the wrong shape: it spread *grids*, and what needed spreading was
the work inside each of them — one 42,051-block ship on its own frame is a stutter no arrangement
of the other two hundred can fix. Finding 10 replaced it, and `ThermalGridScheduler` now simply
gives every grid a share of every frame.

> **The engine does not stagger this, whatever the source reads like.**
> `MyDistributedTypeUpdater<MyEntity>(10)` computes `m_step = ceil(Count / UpdateInterval)` and
> walks a slice per frame, which reads exactly like a stagger and is why the conclusion "the engine
> already does this" was reached from the assemblies and written down. The measurement settled it in
> one line: 1,392 frames of 13,915. Had the fix been written on the original belief it would have
> been *invisible* — a stagger applied on top of a stagger that was not there simply works, and
> nobody learns the premise was false. This is `D7`'s other half: measure before believing a
> subsystem is innocent as well as before believing it is guilty.

---

### 10. A step is spread across the frames of its window

The one the earlier findings kept circling without naming.

A grid advances one solver step every `1 / StepsPerSecond` of a second — fifteen frames at the
default `Frequency 4`. All of that step ran on one of those fifteen frames, and nothing ran on the
other fourteen. Every fix above made the lump smaller; none of them made it stop being a lump.

The step is now spread across the frames of its own window. Each frame is given the fraction of
the step that its length is of the window — `frameSeconds x StepsPerSecond` of it — which is where
`Frequency` and `SimulationSpeed` enter, since `StepsPerSecond` is what they make. Turning either
up now makes every frame do proportionally more, rather than making the lumps arrive closer
together.

**This is what the original mod did, and it was right to.** The rewrite replaced it with atomic
stepping and justified that by the solver having become order-independent — but order-independence
*permits* stepping all at once, it does not require it. It is precisely the property that makes
stepping in pieces safe, and the rewrite dropped the spreading on the strength of the thing that
had just made it correct.

The difference from the original is what is spread. That one advanced **different blocks on
different frames**, so a block's neighbours could be a frame ahead of or behind it; the result
depended on iteration order, which is why it alternated sweep direction to keep the bias fair.
This spreads the **arithmetic of one substep**, which is a sum — every exchange computed from the
temperatures at the start of the substep, accumulated into a watts buffer, and applied to every
node together at the end. A sum has the same value however many pieces it is computed in, so the
answer is *bit-identical* to computing it in one go, and `SpreadStepTests` asserts exactly that at
slice sizes of 1, 7, 64 and 1000 elements and over thirty consecutive steps.

Per frame at half a million blocks, 900 frames:

| | before (per 1/6 s tick) | after (per 1/60 s frame) |
| --- | ---: | ---: |
| median | 23.96 ms | **1.33 ms** |
| p95 | 26.36 ms | **9.45 ms** |
| p99 | 28.71 ms | **12.05 ms** |
| frames over a 60 fps budget | — | **3 of 900** |

The two columns are different units on purpose: before, a tick was the only thing that happened
and it happened every ten frames. What is comparable is that ten frames' worth of the new figure
is about 13 ms against 24 ms of the old, and none of it arrives in a lump.

### 11. An exposure pass that changed nothing does not ask for the grid to be re-mirrored

The same shape as 8, one level further down. The exposure refresh is budgeted (3) and its cost is
spread across frames; what was not budgeted was the work it *asked for afterwards*. Every node it
visited was written and marked `StateDirty`, and the next step's `SyncNodeState` rewrote all
126,731 mirrored rows in one unsliced pass at the top of the step — for an answer that, on a hull
nobody is building, was identical to the one already there.

**A room-mapping pass completes on any structural or venting change**, and each completion starts a
full exposure pass, so this arrived as a hitch on the step after every remap. A node whose faces did
not move is no longer written, so the flag is not set and the pass has nothing to do:

| at 126,731 blocks | mirror every node | mirror none |
| --- | ---: | ---: |
| minimum of 400 | 1.695 ms | **0.124 ms** |
| median of 400 | 2.133 ms | **0.145 ms** |
| per node | 16.8 ns | 1.1 ns |

*`bench stages --stages syncdirty,syncclean`, four processes in one held window (`M7`). Each column
is the median of the four; the legs never come near overlapping — the slowest *mirror none* reading
is 0.157 ms against a fastest *mirror every node* of 1.302 — which is what makes a pair whose runs
spread 25–33 % between them safe to read.*

**The saving is on the event, not on a stage.** The exposure refresh itself is unchanged — measured,
and worth nothing, because the writes it would have skipped were already removed in iteration 6 —
so what a remap costs falls from about 5.6 ms to 4.1 ms, and the part that stops arriving is the
unsliced part. `ExposureSkipTests` asserts the answers are bit-identical and that the skip engages
on every node of an unchanged hull; `SimulationWork.ExposureNodeWrites` is what says so at runtime.

### 12. The shape normals are their own pass, on their own budget

**Reconstructing a node's effective surface normal costs about seven times what counting its exposed
faces does**, and it was measured before it was budgeted:

| at 126,731 blocks | `shapenormals` | `exposure` |
| --- | ---: | ---: |
| best of 100 | **24.781 ms** | 3.561 ms |
| median | 24.920 ms | 3.644 ms |
| per node | **195.5 ns** | 28.1 ns |

*`bench stages --stages shapenormals,exposure --size 125000`, both confirmed — each reproduced its
own best within 2 % five times, so neither is a capped row. The work column is 126,731 nodes for
both, which is what says the two are the same walk doing different things at each stop.*

**So it cannot land whole.** Unsliced, a layout change on a 126,731-block hull is a 24.8 ms frame,
and the hull this mod is bounded by is four times that. `SimulationScheduler.ShapeNormalBudget` is
therefore a seventh of the exposure budget — `nodeCount / 280`, floored at 32 and capped at 512 —
which puts the two passes at about the same cost to a frame.

**Slicing is safe here for a reason worth stating, because it is not true of every pass.** A node
the pass has not reached holds a zero normal, and zero reads as a factor of *one* — the full
projected area, which is the model without the term at all. A half-finished pass therefore degrades
toward the old answer and never past it: there is no interruption that charges a hull less than it
was charged before. `ShapeDragTests` pins that, and it is why the pass needs no completion flag
guarding the friction row.

### 14. The surface rebuild keeps its scratch

**A surface rebuild allocated two `long[cells]` every time it ran** — the snapshot that lets the
derived neighbour half be written back without enumerating a dictionary it is mutating. On a
126,731-block hull that is **2 MB**, it was the whole of what the `surfaces` stage allocated, and a
rebuild runs on every structural change.

| `surfaces` at 126,731 blocks | before | after |
| --- | ---: | ---: |
| allocated | 1,980 KB | **0 KB** |
| best of 100 | 5.506 ms | 5.425 ms |

The buffers are only ever grown, so the case worth testing is a small hull rebuilt into a large
one's buffers: both loops are bounded by the table's count rather than by the array's length, and
`SurfaceRebuildScratchTests` holds a smaller grid's map against a map that never saw the larger one.

**The room air nodes are pooled too**, which was the other 2,573 KB — a `RoomAirNode` per room and
the `Links` list each carries, about 139,120 links on this hull:

| `roomair` at 126,731 blocks | before | after |
| --- | ---: | ---: |
| allocated | 2,573 KB | **0 KB** |
| best of 100 | 7.693 ms | 7.604 ms |

**Two things had to change first, and both are the kind that fail silently.** The carry-over map
held the *nodes* a rebuild was about to reuse, so a room could have read a temperature belonging to
whichever room reused its node first; it holds copies of the three values now. And `Initialised` was
written on the carry-over branch only, so a pooled node would have arrived at a room with no
remembered anchor already claiming to hold a meaningful temperature.

**The test for that second one passed against the bug at first**, which is worth recording: a
rebuild of an unchanged hull finds every anchor in the carry-over map and never takes the branch at
all. `RoomAirPoolTests` fills the room's own anchor cell in between, which moves the anchor and
makes the branch run — and it was proven to fail with the field deliberately unwritten before it was
believed.

**What was looked at and left then, and taken since.** `rooms` allocated **4,768 KB** and that was
the `RoomMap` itself; its recycling waited on an ownership rule and is done now (`D20`, 2026-09-03):
the mapper rotates three map slots, a published map is readable until the publish after next, and a
steady-state pass — the one a sealing change pays — allocates **0 KB** at 126,731 blocks. The first
three passes of a grid's life still each build a slot, which is a load-path cost paid three times
and never again. `RoomMapRecyclingTests` pins the rotation, the read window and the identity of a
recycled map with a fresh one; see
[performance.md](performance.md#pass-10-iteration-4--the-recycled-map-is-pinned-and-the-instrument-was-reading-the-warm-up).

### 13. The cell table is sized before a hull is built, not grown during it

**Placing blocks allocated 10.5 MB of nothing but old dictionaries.** `GridModel`'s cell table
doubles as it fills and every doubling copies every entry it already holds, so a 125,000-block hull
paid **84.4 bytes a block** in tables it immediately threw away — most of what the `place` stage
allocates, and the reason it and `register` are the only two stages that never reproduce their own
best.

`ThermalGrid` knows the count before it registers the first block — `Grid.GetBlocks()`, which it
already enumerates — so it says so, and `EnsureCellCapacity` sizes the table once:

| at 125,000 blocks | unsized | sized |
| --- | ---: | ---: |
| allocated by `GridModel.Add` | 10,549,776 B | **0 B** |
| per block | 84.4 B | **0 B** |
| best of five | 5.64 ms | **5.24 ms**, 0.93× |

**The harness build takes the same hint and it does not pay there, which is worth recording rather
than assuming.** Every corpus walk, lab and test builds through `GridBuilder.BuildSimulation`, which
knows its block count, so it says so — but a 2,000-ship census reads **16.44 s before and 16.57 s
after**, which is noise. The reason is the population: the median published ship is **1,112 blocks**,
where list growth is about **17 KB**, against **4 MB** at 125,000. The saving scales with the hull
and the corpus is mostly small ships. The line stays because it is correct and matches the shipped
path, not because it was measured to help here.

**A hint rather than a bound**, because the table is keyed per *cell* and the count is a block
count: a hull of one-cell blocks lands exactly and one with multi-cell blocks grows once more.
Nothing is refused for exceeding it, and a call after the first block is ignored rather than obeyed —
rebuilding a populated table would copy every entry to save copying them later.
`GridCapacityTests` holds the claim that matters, which is that a sized grid is the *same grid*:
same blocks, same lookups, same bounds.

**The solver's node lists take the same hint**, and it is the rest of the bookkeeping:

| registering 125,000 blocks | unsized | sized |
| --- | ---: | ---: |
| allocated by `Solver.AddBlock` | 23,374,848 B | **19,179,840 B** |
| per block | 187.0 B | **153.4 B** |
| best of three | 9.05 ms | **8.45 ms**, 0.93× |

153.4 bytes a block is the `ThermalNode` objects and nothing else — every byte of list growth is
gone, and what is left is irreducible without a struct-of-arrays change. **14.7 MB in total** off a
125,000-block build, both halves about 7 % quicker.

**One table is deliberately not sized, and the reason is the general rule.** A list's order is its
index order whatever its capacity, so sizing one cannot be observed. A **dictionary's is not**:
capacity decides bucket layout and so decides enumeration order. `GridModel`'s cell table is safe
because the only thing that enumerates it takes a min and a max over integers. `ThermalGrid.blocks`
is **not**, because the x-ray overlay enumerates its values and stops at `DebugOverlayMaxBoxes` — so
a different order would draw a different set of boxes on any hull over the budget. Cosmetic, and
still a behaviour change, so it keeps its regrowth.

**The `place` stage still measures the unsized path**, deliberately. That is what the incremental
per-block path does, and changing what an instrument measures changes what its history means.

### 12. The shape normals are their own pass, on their own budget — continued

**A one-cell block's neighbourhood is a table, and that is worth 6.8 % — which is the finding
rather than the saving.** Its centre is its own cell, so the 26 offsets and their unit vectors are
the same for every such block; the general walk arrived at those constants by 26 square roots and 26
divisions per node, and a census hull is mostly one-cell blocks. Replaced by a lookup — the same
values summed in the same order, held to the walk *exactly* rather than to a tolerance by
`ShapeNormalOneCellTests`, because float addition is not associative and a table read in a different
order is a different answer. **24.797 ms → 23.110 ms** at 126,731 blocks, 195.7 → 182.4 ns a node,
with `exposure` flat at 3.536 against 3.561 as the control.

**Removing every square root bought 6.8 %, so the pass is not compute-bound — it is the probes.**
Twenty-six scattered reads into the occupancy bitset is what a node costs, and the arithmetic on top
of them was nearly free. That is where a further pass has to aim, and the shape of it is fewer reads
rather than cheaper ones: the 26 neighbours of a cell occupy nine rows of three adjacent bits, so
nine word reads could replace twenty-six probes. Not built, because this pass runs only where
`EnableShapeDrag` is on and that ships off — the cost is real but almost nobody pays it.

**It is also rebuilt on the right event, which is the other half of the cost.** Exposure is
recomputed whenever a room-mapping pass completes — a door is enough — and a door does not move a
hull. A normal is a function of occupancy, so this pass is keyed on the grid's own version and skips
entirely on every change that does not add or remove a block. A running pass is never restarted
either, the rule the wind shadow already follows: a hull under construction changes every few frames,
and a pass that starts over each time never finishes.

## Catching it again

The load tests in `tests/Thermodynamics.Tests/LoadTests.cs` run with the ordinary suite and assert
these properties rather than leaving them to a benchmark nobody runs. Almost every assertion is
on a `SimulationWork` counter rather than a stopwatch, because a millisecond threshold is a claim
about the machine that ran it and a claim that placing one block must not visit every node is a
claim about the algorithm.

| Test | Property |
| --- | --- |
| `ASettledGridRebuildsNothing` | an unchanged grid pays for no rebuild of any kind |
| `PlacingOneBlockLinksTheBlockAndNotTheGrid` | fewer than sixteen node visits, whatever the grid size |
| `WeldingABurstCostsTheBurstAndNotTheGrid` | a hundred placed blocks visit a hundred nodes |
| `ManyPlacementsInOneTickCoalesceIntoOneRebuild` | one rebuild per burst, not one per block |
| `SearchesForPlumbingSkipAGridThatHasNone` | no cells scanned for loops or pumps |
| `RoomMappingNeverExceedsItsBudgetInOneTick` | the flood fill respects its budget |
| `ExposureRefreshNeverExceedsItsBudgetInOneTick` | the exposure pass respects its budget, and still covers every node |
| `ARebuildIsBilledToTopologyAndNotToTheSolver` | stage attribution is honest |
| `ReadingTheLinkCountDoesNotRebuildTheGraph` | observing does not change what is observed |
| `SolverCostPerLinkStaysProportional` | nothing quadratic crept into the conduction pass |
| `ASettledTickFitsInAFrame` | a wall-clock tripwire, loose on purpose |

The timed ones run in a collection that disables parallelisation and take the best of three runs.
They were reporting 1.7x and then 3.2x for the same binary before that, and all of the difference
was thirty other tests on the same cores.

Every test writes its figures to the test output, so a suite run is also a performance report.

---

## From a real session

Benchmarks measure a synthetic hull with no game blocks behind it. What a real world does is a
telemetry question, and the report now has a **frame cost** section: what the mod spends per
frame across every grid, the share of frames where it alone exceeded 16.7 ms, how spiky the
session was, and the worst sixteen frames in full — each with its stage split and the counts of
what those stages touched. See [telemetry.md](telemetry.md#frame-cost-and-hitching).

That last part is what makes a report actionable. "A 90 ms frame" is a number to worry about; "a
300,000-block station rebuilding its conduction graph, 300,000 nodes visited" is a line to
change.

---

## Calibrating the step budget against a real world

`MaxElementVisitsPerStep` bounds a step at a number of element visits, and its default of 1,000,000
was chosen as roughly one 60 fps frame at the harness's measured 16 ns per visit. A field report
says what that actually buys in game.

**This section measured the case that got the counting fixed.** At the time the setting was
`MaxLinkVisitsPerStep` and counted links alone; the figures below are from that build, and the
resolution is at the end.

Three 42,051-block ships, 90,136 links each, in a 203-grid world:

| | |
| --- | --- |
| substeps per step | **11.00 / 11.00 / 11.00** (sd 0.00, n 928) |
| steps clamped | 0 |
| simulation rate | 35.4 % — 150.3 s of simulated time not advanced |
| that grid's tick | 85–153 ms |

The budget is working exactly as designed: 1,000,000 ÷ 90,136 = 11, the substep count is pinned
there with zero variance, nothing clamped, and the step is shortened instead — the grid runs at
35 % of real time rather than in lurches.

What it also says is that **the default is calibrated against the harness and the harness is
optimistic**. 991,000 link visits cost 85–150 ms in game against the ~17 ms the harness's rate
predicts. Some of that is the game's .NET 4.8 runtime against the harness's .NET 9, some is that
the environment pass is per *node* per substep and the budget only counts links — this grid has
2.14 links per node, so the node work the budget cannot see is a large share of what a step does.

Practical consequence: **on a world with grids this size, the default is about five times too
generous.** A value nearer 200,000 would put those ships at 2 substeps and a tick nearer 20 ms, at
the cost of simulation rate they are already trading away. The right value is a judgement about
that trade, and it is per world, which is why it is a setting.

### How it is resolved

The counting was fixed rather than only documented. A node's cost was measured against a link's
across shapes spanning zero to three links per node — a node is worth 3.3 links at a hundred
thousand nodes and 7.5 at a quarter of a million, an exposed face a tenth to a half of one — and
the budget now counts `links + 4 × nodes` under the name `MaxElementVisitsPerStep`. See
[benchmarks.md](benchmarks.md#what-a-substep-costs).

**The default did not need moving after all, because the unit change did the work.** These ships
cost `90,136 + 4 × 42,051 = 258,340` element visits a substep, so the same 1,000,000 now grants
**3.9 substeps instead of 11** — very close to the "nearer 200,000" this section arrived at from the
other direction, and reached without asking anyone to retune a world. What was five times too
generous is now about right, and it is right for the reason the arithmetic says rather than by
being trimmed until it looked sensible.

### And the calibration it rests on was never re-taken

The paragraph above ends the story at 2026-08-21 and the story kept going. Two things moved under it
and neither was noticed until `C27` asked whether the default should move with `C24`'s retune.

**The rate the default was calibrated against is gone.** 1,000,000 was "roughly one 60 fps frame at
the harness's measured 16 ns per visit", when a visit was a *link* visit and the setting was called
`MaxLinkVisitsPerStep`. Removing the two passes that reached through the node objects took the same
step from 623 ms to 118 ms and the rate to 4.7 ns, and the harness today measures **about 1 ns** per
element visit in the unit the budget now counts in — which on this hull is **3.1 ns a link visit**,
a fifth of what the calibration assumed. The default has moved once since, and for an unrelated
reason (`Frequency`). Nobody re-took the calibration, because the figure it would be re-taken
against is in the wrong unit — which is the next paragraph.

**And the figure a reader would re-calibrate with is in a different unit.** Every *ns per element
visit* on this page, in [benchmarks.md](benchmarks.md#the-ladder) and in a telemetry dump divides by
`nodes + links`; the budget counts `links + 4 × nodes`, which on a census hull is very close to
twice as many. So anyone converting the shipped allowance into milliseconds through a published
figure would have overstated it by about two, and nobody could have converted it correctly from
anything written down.

**What it is actually worth is now measured in its own unit**, along with what the simulated time it
trades away costs in kelvin — see [benchmarks.md](benchmarks.md#what-the-allowance-is-worth) and
`bench allowance`. The short form: the allowance is a **per-frame** budget, `V × Frequency / 60`
element visits, so the 2,000,000 that shipped until `C27` granted 133,333 visits a frame; it binds
in **air** rather than in vacuum, at about 9,000 blocks in flight against 32,000 in vacuum; and what
a shortened step costs is a slow thermal clock, worth 1.19 K standing at a 5 % deficit and 36.98 K
at 60 % under a moving load. **The default is 4,000,000 since**, which is that trade and nothing
else — see [configuration.md](configuration.md#what-a-shortened-step-costs).

## What is still open

Roughly in order of how much a million-block grid would notice.

**A solver step is atomic, and at a million blocks it is 118 ms.** A step must touch every node, so
this is not an accounting mistake, and lowering `Frequency` makes the spike *less frequent* without
making it smaller. Making it smaller means not touching every node: activity tracking, chunking and
multirate stepping, all designed in [scale-design.md](scale-design.md) and none of it built.

> This paragraph used to say the step was near the memory-bandwidth floor at 16 ns per link visit.
> It was not. Two passes were reaching through the node objects on every visit, and removing both
> took the same step from 623 ms to 118 ms and the per-visit rate to 4.7 ns. What is left may well
> be near the floor; the claim has been wrong once and is not being made again without a
> measurement that isolates it.

**Memory is about 1.8 KB a block**, against the ~110 bytes a node budgeted in
[scale-design.md §6](scale-design.md#6-data-structures). Half of what a grid retains is indexed by
bounding volume rather than by block. See [memory.md](memory.md), which measures it and ranks the
changes that would give it back — and corrects the 2.5 GB figure this paragraph used to quote,
which counted garbage.

**The room map floods the bounding volume**, which is 14 times the block count on a hull and worse
on a station. It is budgeted now, so it costs ticks rather than a stall — 7,237 of them to
converge at a million blocks, which is twenty minutes of a stale map. Bounded and wrong is better
than unbounded, but it is still wrong.

**World load is 2.2 seconds at a million blocks**, in one call, before the first tick — 500 ms
placing and registering the blocks and 1.7 s in `RebuildAll`. It was **12.0 s** before the
2026-08-26 performance pass, measured on the same machine in the same window with both builds
optimised ([performance.md](performance.md#what-the-pass-moved)). `RebuildAll` is
deliberately one-shot because it is far cheaper than replaying the incremental path per block, and
a loading screen is a better place for a stall than a session — but a blueprint pasted mid-session
takes the same path.

**`SweepRoomPressure` is still per room per cadence**, with two game API calls each. Bounded by
compartment count rather than block count, so it is small on a ship and unmeasured on a station
with thousands of rooms.

---

## In the field

Three live runs against real worlds. The lab measures a synthetic ship on a quiet machine; these
measure what a session actually costs, and they are what set the shipped substep caps.

### A six-grid test world, at `MaxSubstepsPerBlock 3`

296 s, six 1,300-block large grids, `MaxSubsteps 3` / `MaxSubstepsPerBlock 3`.

| | |
| --- | --- |
| total measured | **~1.8%** of real time |
| solver alone | 1.63% |
| mean frame | 0.304 ms |
| worst frame | 25.8 ms |
| frames over a 60 fps budget | **2 of 17,352** (0.01%) |
| block updates / real second | 31,145 |
| ns per element visit | 50.4 |

Both over-budget frames are in the first eight seconds — build and load, not steady state. Drift is
0.2–0.6 K per minute against a 3 K per minute tolerance, no grid records a critical event, and total
heat damage is zero. **The tuning question is therefore not how to make it cheaper but what to spend
the headroom on.**

> The `total measured` figures are approximate because the report that produced them summed the grid
> update into the session frame that already contained it, reading 3.62% and 4.70%. The session
> frame is very nearly all grid update, so the true totals are close to half those. They cannot be
> recovered exactly without re-running. `solver alone` was never affected.

What the headroom buys, from the run's own cap sweep:

| Cap | Blocks floored | Share | Saving | Solver cost |
| --- | ---: | ---: | ---: | ---: |
| off | 0 | — | — | 11.6% of real time |
| 8 | 60 | 0.75% | 62.5% | 4.3% |
| 6 | 84 | 1.05% | 71.9% | 3.3% |
| **3** (in force) | 732 | 9.18% | 86.0% | **1.6%** |
| 1 | 3,009 | 37.7% | 95.3% | 0.5% |

**`MaxSubsteps` must move with `MaxSubstepsPerBlock`.** At these settings the per-block cap reduces
demand from 21.35 to exactly 3.00 and `MaxSubsteps 3` grants it, so nothing is refused —
`clamped_steps` is 0, and that is the two caps agreeing rather than luck. Raising the per-block cap
to 6 without raising `MaxSubsteps` to at least 6 starts refusing steps, which is the failure mode
[realism.md](realism.md#failure-and-what-actually-causes-it) measures.

### The same world at `MaxSubstepsPerBlock 6`

Same world, same ship, same build; only the two caps moved. 118.5 s.

| | cap 3 | cap 6 |
| --- | --- | --- |
| blocks floored per grid | 121.97 (9.0%) | **14.0 (1.03%)** |
| `demand_configured` | 3.000 | 6.000 |
| `clamped_steps` | 0 | **0** |
| substeps per step | 3.00 | 6.00 |
| solver share of real time | 1.63% | 2.17% |
| total measured | ~1.8% | ~2.4% |
| ns per element visit | 50.4 | **34.3** |
| worst peak-to-mean drift | 2.89 K | **1.07 K** |
| frames over 60 fps | 2 of 17,352 | 2 of 6,801 |

**It costs a third more, not double.** Twice the substep passes come out at 1.33× the solver time,
because the cost of a single pass falls 32% — 0.2314 ms to 0.1576 ms, and 50.4 ns per element visit
to 34.3. More substeps over the same node set is a tighter loop with less per-step setup and better
locality; the arithmetic per visit is unchanged, so this is cache behaviour rather than a saving in
work.

**The answer does not move, only its resolution.** Peak temperatures are within 2 K on every grid
(883–908 K against 885–910 K), so the extra substeps buy the same physics with nine tenths of the
previously under-resolved blocks resolved properly, and drift more than halves on the way.

**Cap 6 is the right place to stop.** Cap 8 floors ten blocks a grid instead of fourteen, cap 16
floors two, and uncapped floors none at roughly 3.5× the substep passes — affordable, but buying
perhaps a tenth of a kelvin of drift on a figure already three times inside tolerance.

### A fleet in atmosphere

505.6 s, 242 grids of which 205 profiled, 123,784 blocks, the largest 44,632 cells. `Frequency 8`,
`MaxSubsteps 64`, `MaxSubstepsPerBlock 0`, `MaxElementVisitsPerStep 1,000,000`. Two capital hulls,
several dropships, a food truck, and a lot of small grids.

| | |
| --- | ---: |
| total measured | 29.9% of real time |
| solver alone | 25.9% |
| mean frame | 5.30 ms |
| worst frame | 416.6 ms |
| frames over a 60 fps budget | 494 of 27,744 (1.78%) |
| substeps per step | 3.04 |
| ns per element visit | 18.9 |
| steps clamped by the substep cap | 0 |
| grids below real time | 2, slowest at 15.1% |

Nothing overheated: zero critical events, zero heat damage, peak 307 K on the hottest hull. **The
demand figures are what cost the 25.9%, not what threatened the ships.**

What the caps would buy over those 205 grids:

| Cap | Blocks raised | Share | Element visits saved | Speedup |
| --- | ---: | ---: | ---: | ---: |
| off | 0 | — | — | — |
| 16 | 3 | 0.00% | 9.6% | 1.11× |
| 8 | 209 | 0.17% | 24.2% | 1.32× |
| **6** | 777 | 0.63% | 35.4% | 1.55× |
| 4 | 1,530 | 1.24% | 46.7% | 1.88× |
| 3 | 4,913 | 3.97% | 57.0% | 2.32× |

`MaxSubstepsPerBlock 6` remains the recommendation and this fleet does not change it: 0.63% of
blocks floored for a third of the solver's work. What has changed is **what the cap stands in for**.
It no longer covers blocks whose materials are wrong — 12,764 blocks, 10.3% of the fleet, are stiff
mostly through radiation and convection, and the cap reaches those too. That is a more visible trade
than capping a conduction estimate: it changes how a block exchanges with the sky rather than with
what it is bolted to.

**Why the decorative definitions only half worked** — and why a fix that is right in vacuum is
nearly inert in air — is [stiffness.md](stiffness.md), which is where that finding belongs and where
the same question is asked of eight thousand ships rather than one save.

### One correction worth keeping

A settings file reporting `Version 6` against a branch at 4 looked like a run taken against the
wrong build. It was not: there are **two settings types with independent version numbers** —
`Core/Settings/ThermalSettings.cs` is the solver's, and `Thermodynamics/Settings.cs` is
the game-side one the `.cfg` is written from. Every field that looked missing was present. The
tuning conclusions stood without qualification, and the near-retraction is recorded because the
reasoning that produced it was sound and the premise was not.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-04 | **The ladder is re-audited and survives**: 580 ms of build at a million blocks against the published 582, settle exactly 3,934 ticks, every stage inside session spread. The step and resident columns read 14 % and 7 % lower with no change to explain them and are left to the session (`M7`). The table now carries the 2026-09-04 figures with the audit note. |
| 2026-09-04 | **The hitch distribution is re-taken** after the bounded step, the spread step and the prologue hoist: p99 26.47 → 3.11 ms at 126,731 blocks and 28.71 → 6.90 at 505,566, max 60.83 → 24.38 and 208.68 → 50.13, with the one outlier attributed to the process's first walk of the step path (the JIT) rather than to anything a second grid would pay. The superseded rows stay printed beside the new ones. Also: `UncalledCodeTests` crossed two seconds and is tagged slow — the lane checker found it on this pass's refresh. |
| 2026-09-04 | **The first-step outlier is corrected and mostly gone**: the step prologue runs on the rebuild tick (`D4`), a warm first step reads 12.5 ms against a steady 8.3 at 505,566 blocks, and the old first-touch attribution is measured at one to two milliseconds of the spike it was blamed for. |
| 2026-09-04 | **The room map is recycled (`D20` done), and section 12's "looked at and left" is taken.** Three rotating slots, a published map readable until the publish after next, and a steady-state pass at **0 KB** allocated on 126,731 blocks where it bought a 4,768 KB map before. The stage lab's rooms allocation sample moved to the first recycled repeat — at repeat 1 it was reading slot-building and showed the change as absent. |
| 2026-08-31 | **Gave the harness build the same capacity hint, and measured that it does not pay on the corpus.** A 2,000-ship census is 16.44 s before and 16.57 s after — noise. The median published ship is 1,112 blocks, where the list growth this removes is about 17 KB against 4 MB at 125,000, so the saving scales with the hull and the corpus is mostly small ships. Kept because it is correct and matches what `ThermalGrid` does on the game's own load path, and recorded as a negative result so nobody measures it again expecting one. |
| 2026-08-31 | **The room air rebuild pools its nodes: 2,573 KB to 0.** A node and a link list per room were allocated every rebuild. Two silent hazards had to be closed first — the carry-over map held the very nodes the rebuild was about to reuse, and `Initialised` was written on one branch only — and `RoomAirPoolTests` was proven to fail against the second before it was believed. Its first version did not: a rebuild of an unchanged hull finds every anchor and never takes the branch, so the test now moves a room's anchor between rebuilds. |
| 2026-08-31 | **The surface rebuild keeps its scratch: 1,980 KB to 0.** Two `long[cells]` were allocated per rebuild to snapshot a table being mutated; they are scratch, so they are kept and grown. `SurfaceRebuildScratchTests` holds a second rebuild against a first and a small hull against buffers a large one sized, which is the case a kept buffer could get wrong. Looked at and left: `roomair`'s 2,573 KB is per-room nodes whose pooling needs a full `Reset` and would fail as a silent physical bug, and `rooms`' 4,768 KB is the `RoomMap` itself, whose recycling is gated on the threading invariant that a published map is never mutated. |
| 2026-08-31 | **Sized the solver's node lists too, and recorded which table must not be sized.** `Solver.AddBlock` falls from 187.0 to **153.4 bytes a block** — the `ThermalNode` objects and nothing else — for 4.2 MB, on top of the cell table's 10.5, and both about 7 % quicker. **`ThermalGrid.blocks` keeps its regrowth on purpose**: a list's order is its index order whatever its capacity, but a dictionary's enumeration order follows its bucket layout, and the x-ray overlay enumerates that one and stops at a budget — so sizing it would draw a different set of boxes. That is the rule the other two were checked against rather than a special case. |
| 2026-08-31 | **Sized the cell table before a hull is built.** `GridModel`'s dictionary doubled as it filled and every doubling copied what it already held: **10,549,776 B** on a 125,000-block hull, 84.4 a block, thrown away immediately. `ThermalGrid` already enumerates `Grid.GetBlocks()` and so knows the count, and `EnsureCellCapacity` takes it — **0 B** after, and 0.93× the time. A hint rather than a bound, ignored after the first block, and `GridCapacityTests` pins that a sized grid is the same grid: same blocks, same lookups, same bounds. |
| 2026-08-31 | **Gave the shape-normal pass a one-cell fast path, and learned that its cost is not what it looked like.** A one-cell block's 26 neighbour offsets and unit vectors are constants, so the walk was doing 26 square roots a node to arrive at a table; the table is 24.797 → **23.110 ms** at 126,731 blocks with `exposure` flat as the control, and `ShapeNormalOneCellTests` holds the two paths equal exactly rather than within a tolerance. **6.8 % for removing all the arithmetic says the pass is memory-bound on the occupancy probes**, which is where a further pass would have to aim — nine word reads for the nine rows of three adjacent bits, rather than twenty-six probes. Left unbuilt: the pass runs only behind a switch that ships off. |
| 2026-08-31 | **Measured the shape-normal pass and gave it a budget, before it shipped rather than after** ([backlog.md](backlog.md) `K22`). At 126,731 blocks it is **24.781 ms**, 195.5 ns a node, against exposure's 3.561 ms and 28.1 ns — seven times the cost of the walk it most resembles, and a frame-eating hitch if it lands whole. Sliced at a seventh of the exposure budget, keyed on the grid's version rather than on the exposure event so a door does not trigger it, and never restarted mid-pass. Safe to slice because an unvisited node reads as no correction, so a partial pass degrades toward the model without the term and never past it. |
| 2026-09-01 | **Re-took the ladder, which had gone stale by four to fifteen times.** At a million blocks: build **582 ms** against the 5,429 the table carried, room mapping **145** against 2,122, exposure **43.5** against 513, a settled step **61.3** against 118, and **610 MB** resident against 1,510. Nothing here is new work — every one of those figures is the passes from the second to the ninth landing, each of which recorded what it moved in a change log while the table other pages quote was left where it was. Today's census hull carries 14 % fewer links, so topology and step are not a like-for-like ratio; the bounding boxes are identical, so room mapping is. |
| 2026-09-01 | The seven per-mechanism watt figures moved off `ThermalNode` into a record it makes only when something asks for them (`E4`): **20 bytes a node** in a world with the readouts off, which is every world running the shipped configuration. |
| 2026-08-27 | Added property 11: **an exposure pass that changed nothing no longer asks for the grid to be re-mirrored.** The refresh is budgeted and sliced; the full, unsliced `SyncNodeState` it forced onto the next step was not, and it arrived after every room remap for an answer identical to the one already there. Mirroring 126,731 rows is 2.13 ms against 0.145 for mirroring none. The same shape as property 8, and found while measuring something else. |
| 2026-08-27 | The fifth performance pass moved **the settled step** for the first time — 0.86 at 505,566 blocks — and the link build 0.84 ([performance.md](performance.md#pass-5--what-the-pass-moved)). It also gave a step its first per-stage instrument: at that size a step is environment 28.9 ms, conduction 28.0, apply 11.7, publish 5.3 and the row fill 4.1, and conduction is measurably at its memory floor. |
| 2026-08-27 | The fourth performance pass took the room-mapping pass to **0.36** of what it was at 505,566 blocks and the exposure refresh to **0.53**, and gave the room air rebuild — the largest single thing on the load path, and until then unmeasured by anything — an instrument ([performance.md](performance.md#pass-4--what-the-pass-moved)). Building a million-block world reads **1.32 s** against 1.66 s at the pass's start, of which `RebuildAll` is 0.98 s against 1.33 s. |
| 2026-08-27 | World load at a million blocks is **2.2 s**, from 3.2 s at the end of the first performance pass ([performance.md](performance.md#pass-2-iteration-10--what-the-pass-moved)): one-cell paths for the neighbour query, exposure and block construction, the interior scan skipping visited words, and the room map's publish sort by radix. The worst tick after a placement at 505,566 blocks is 70 ms, from 93. |
| 2026-08-26 | World load at a million blocks is **3.2 s**, from 12.0 s (4.8 s before the cell tables were rekeyed on `GridMath.Key`), measured start-against-tip in one window with both builds optimised. The block half halved (an orientation is a table lookup rather than a matrix) and `RebuildAll` fell to 25 % (the room map reads a sealing snapshot and a bitset, the surface map holds one packed entry a cell, and every cell table is keyed on a `long`). The 11 s this page carried was taken on an unoptimised harness and is not comparable to either figure — see [performance.md](performance.md), `M13`. |
| 2026-08-22 | Put the ten spike findings in the present tense: each is a property the code holds rather than a thing that was fixed, and the *fixed* marker on all ten of them said only that none was outstanding. Removed the struck-through *Removing a block still rebuilds the whole graph* from [What is still open](#what-is-still-open), which contradicted finding 6 four screens above it. |
| 2026-08-22 | Corrected the figures in the sentence reading the ladder's own divergence: it quoted a tick of 42 ms against a full step of 103, which matches neither the current table (25.87 against 118.20) nor the pre-refresh one named two paragraphs later (55 against 623). A figure on a page comes from the dataset the page is about (`E5`). |
| 2026-08-22 | Absorbed `field-tuning.md`: its three live runs are the field half of this page's question and now sit beside the lab half. Its decorative-block stiffness findings moved to [stiffness.md](stiffness.md) and its `Frequency` sweep to [configuration.md](configuration.md#frequency-is-not-the-cost-dial-it-looks-like), each to the page that already owned the subject. Added the standard header and this log. |
| 2026-08-20 | Calibrated the step budget against a real world, and resolved it by fixing the counting rather than the default: at `links + 4 × nodes` the unchanged 1,000,000 grants 3.9 substeps where link-only counting granted 11. Measured a fleet of 242 grids in atmosphere. |
| 2026-08-19 | Spread a step across the frames of its window instead of landing it whole on one, and staggered every grid in the world off the same frame. Stopped the room map giving up silently on a large grid. |
| 2026-08-18 | Opened the page: the ladder to a million blocks, the per-tick distributions, and the nine spike findings — a block placed rebuilding the whole conduction graph, the room mapper's unbounded scan, the exposure refresh with no budget, two searches walking the grid to find nothing, the mass sweep walking every block, and removal rebuilding the graph. Established the distinction the whole exercise rests on: steady cost and hitching are different problems with different fixes. |
