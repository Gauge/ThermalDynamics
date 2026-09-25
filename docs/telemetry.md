# Telemetry

A data collection module for the live mod. It observes the running simulation and writes a
report on demand or when the world closes.

It exists to answer the questions in [known-issues.md](known-issues.md) with
measurements from real play rather than from a synthetic benchmark: what temperature blocks
actually sit at, which definitions are mistuned, what each stage of the update costs, and
whether real grids are stiffer than the integrator can follow.

**It is off by default.** `EnableTelemetry` defaults to `false`, so a shipped world pays a
handful of static bool reads and nothing else.

> The rules argued here are stated canonically in [rules.md](rules.md): `C4` `C7` `D4` `E3`.

| Looking for | Go to |
| --- | --- |
| What the figures mean | [thermal-model.md](thermal-model.md), [environment.md](environment.md) |
| The settings that switch each dump on | [configuration.md](configuration.md#telemetry) |
| What the reports have been used to find | [known-issues.md](known-issues.md) |
| The repeatable offline report | [benchmarks.md](benchmarks.md) |

## Turning it on

Either set `<EnableTelemetry>true</EnableTelemetry>` in `ThermodynamicsConfig.cfg` before
loading the world, or switch it on in chat during one:

| Command | Effect |
| --- | --- |
| `/thermal status` | Collection state, sample stride, live grids, block models built, rotor bridges. |
| `/thermal telemetry on` | Starts collecting. Attaches records and stage profilers to grids that already exist, so no reload is needed. |
| `/thermal telemetry off` | Stops. Every hook goes back to a single bool read. |
| `/thermal stride <n>` | Blocks sampled per step is `1/n` of the grid. Default 4. |
| `/thermal settings` | Every setting and its current value; `set` changes one live. |
| `/thermal dump` | Writes a report without closing the world. |
| `/thermaldump` | The same thing; kept from the previous version. |

Repeated dumps are safe: the "final state" sections are rebuilt each time rather than appended
to.

## Thermal vision test results

Use `/thermal vision range -50 50` to set a shared Celsius window for both palettes (also the default). Range changes are recorded, and different windows produce separate aggregate rows.

Enable collection on the client performing the test with `/thermal telemetry on`, then activate `/thermal vision colour` or `/thermal vision grey`. `/thermal vision note <observation>` records a short visual observation with a session-relative timestamp. `/thermal dump` includes the results in the normal timestamped telemetry log in world storage. A dedicated server cannot observe client rendering.

The `THERMAL VISION PROBE` section records every attempted draw while collection is enabled, independently of simulation sample stride. It includes:

- CPU milliseconds, split into frames with and without a geometry cache miss; min/mean/max and sample counts. This includes target lookup, model extraction, HUD updates and instrumented draw work, but not GPU completion or the final telemetry accumulator update.
- Rows by palette, locked temperature window in kelvin, block definition and outcome: submitted, partial, zero-submitted, armour submitted/partial/zero-submitted, unsupported armour deformation, missing grid parts, invalid temperature, no raycast target, or render error. Each row includes temperature in kelvin, examined/submitted triangle statistics, partial and empty frame counts, and backface/degenerate triangle totals.
- Menu-suppressed frame and rendering error counts. Suppressed frames do not enter draw timings.
- Recent activation/rejection, viewpoint-loss, manual-off and reset events; target grid/cell identity; model asset names and source/retained/unsupported-technique counts when a model enters the cache; model budget rejection; tester notes. Revision 3 supports models up to 65,536 triangles, retains up to 262,144 triangles / 128 models across target changes, and examines at most 98,304 triangles per frame. Budget rejections and cache-recycle messages belong to the diagnostic history. Model-build events require telemetry to be enabled before that model is cached.

Memory is bounded at 128 aggregate rows and two independent histories of 64 events each (important notes/mode/range/error events and target/model diagnostics), with a 320-character message limit. Additional row samples merge into an `OTHER` row; the report states overflow and event eviction counts. Probe off/on does not erase accumulated results. World telemetry reset creates a fresh accumulator, and repeated dumps do not drain it. Collection toggles pause/resume accumulation; they do not clear it. Caught render exceptions also go through the always-on fault path and the game log.

Revision 4 also measures `/thermal vision scene colour` and `/thermal vision scene grey`. Scene rows aggregate whole draw frames instead of individual target definitions. Their temperature sample is absent; the separate scene summary reports window bounds, scanned blocks, frustum/range candidates, attempted blocks, blocks with submissions, blocks with no submission/data and discovery/draw-limit frames. Automatic range rows use `AUTO` rather than creating a row for every changing window. Revision 5 adds discovery-refresh counts and per-frame discovery/sort and candidate-validation/draw CPU distributions (HUD publication and telemetry serialization are outside these two stages, but inside total probe CPU where applicable). Cached frames normally scan zero new blocks and revalidate retained candidates before drawing. A candidate is not proof of visibility; occluded candidates may affect the adaptive window. Scene limits and unsupported categories are described in [thermal-vision-design.md](thermal-vision-design.md#trying-the-scene-probe-revision-8).

Revision 6 adds overlapping budget-reason frame counts (discovery, block cap, triangle cap, time, model extraction and armour pass quota), and separate armour/entity blocks-with-submissions statistics. A frame may hit several limits, so these counts must not be summed as distinct frames. The armour-first pass is bounded; the total 32,768-triangle and soft 4 ms scene budgets remain unchanged, while the block-attempt cap is 512.

Revision 7 adds pending-build frame counts, extracted source triangles per frame and separate build CPU timings. Geometry extraction resumes across draws and is excluded from the validation/draw stage timing. Slow model draw events (8 ms or more) record the asset and number of submissions; these are elapsed-time observations, not GC/GPU attribution.

Revision 8 reports batch tests and triangles rejected by conservative plane bounds. `examined` now counts only individually processed triangles after batch rejection; `backfaces-total` includes both whole-batch and individual rejection and can exceed `examined`. Do not compare these counters across revisions without accounting for that change.

The [survey-scope candidate](thermal-vision-survey.md) adds important events for start, timeout and completed capture. Completion reports total queries (including discarded motion attempts), measured/unknown samples in the final capture, wall duration, accumulated query/temperature-lookup CPU time and maximum single-sample time. Its generic vision rows distinguish acquisition, capture and no-capture states; their model-triangle counts are zero because the image is submitted separately through Rich HUD. Frame timings do not measure Rich HUD's separate drawing cost. A query over the soft 2 ms issuance deadline is visible in the maximum sample time; the loop cannot preempt native calls.

Triangle counts measure submissions, not visible pixels. A nonzero count cannot certify occlusion, alignment, correct colour or complete scene coverage. Include visual observations or screenshots when reporting those results. See [thermal-vision-design.md](thermal-vision-design.md) for the prototype's scope and test procedure.

## Faults are recorded whether or not collection is running

Everything else in this document is *observation*: it costs something on every healthy frame, and
is rightly off unless someone is reading it. A **fault** — a caught exception — is not. It costs
nothing until the mod has already failed, and by then it is the only evidence there will be.

For a long time the two were gated together, and telemetry is off by default. Every `catch` in the
simulation adapter routes through `Telemetry.Exception`, so in an ordinary world all twenty-two of
them discarded their exception, wrote nothing to any file, and left the grid running in whatever
state the throw abandoned it in. That includes the guard around `ThermalGrid.Tick`, whose own
comment says an exception named there is worth more than a crash dump — and which named it nowhere.
B9 in the [backlog](backlog.md), a startup crash seen once and never reproduced, is exactly the
shape of bug this made unrecoverable after the fact.

Now:

| | Observation | Fault |
| --- | --- | --- |
| Recorded while collection is off | no | **yes** |
| Written to `SpaceEngineers.log` as it happens | no | **first occurrence of each kind** |
| Named in the closing log summary | no | **yes, with its count** |
| In the report's Anomalies section | yes | yes, first and marked `!!` |

Only the *first* occurrence of a kind is logged as it happens. A throw inside the step runs once
per grid per frame, so logging every one would fill the game log with the same six stack frames and
bury whatever else was in it. The count keeps rising regardless, and the summary written as the
world closes carries it — which is what distinguishes a one-off from a permanent failure, and is
the only output at all on a world that never turned collection on.

The rule lives in `AnomalyRegistry` in `TelemetryAnomalies.cs`, which is free of game types and
linked into `tests/`, so it is covered by `AnomalyRegistryTests` rather than argued about.

**A fault in the log is a reason to turn telemetry on and reproduce**, not a diagnosis. The log
line carries the exception type, its message and the top six stack frames; the report carries what
the grid was doing.

### A grid that has gone numerically bad is also a fault

An exception is not the only silent failure. A grid that goes NaN or runs away destroys a save
rather than degrading a frame, and until now it was visible only through the per-node sampling walk
— which runs only while collection is on. The worst class of bug was the one nothing in an ordinary
world was looking for. `A11` in the [backlog](backlog.md) is the case: a grid welded past its buffer
capacity divided watts by a heat capacity of zero and went NaN, whole.

Every fourth step, each grid classifies itself from three figures the solver has already summed:

| Figure | Catches |
| --- | --- |
| `EnvironmentWatts` | NaN or infinity anywhere on the grid — it is a sum over every node |
| `HeatGainWatts` | the same, on the generation side |
| the hottest node's temperature | runaway, which no watt sum has to show |

The two watt sums are accumulated on the stepping path whether or not anything is collecting, so a
bad value at any node propagates into them. That is what makes three float tests a whole-grid check
rather than a sample of one. The one case it cannot see is a bad temperature on a node with no
exposed face, which contributes to neither sum; per-node classification remains the sampled path's
job, and this is the always-on floor beneath it.

Cost is three float tests every fourth step per grid, on figures already computed, with nothing
walked and nothing allocated. A grid that is healthy and stays healthy never reaches the registry.
Reporting is on the **transition**, so a grid stuck bad is recorded once rather than every fourth
step for the rest of the session, and a grid that recovers and fails again is recorded twice —
which is the difference worth knowing. Pinned by `GridHealthTests`.

## What it costs when it is off

| Path | Cost with collection off |
| --- | --- |
| Grid update | one `Telemetry.Enabled` test |
| Grid health | three float tests every fourth step, on figures the solver already summed |
| Environment sample | one test |
| Block placed / removed | one test (`GetBlockType` returns null and the record is never made) |
| Overheat damage | one test |
| Simulation stages | `Profiler` is null; one null check per stage per update |
| Per-frame cost | nothing is accumulated and no frame is closed; one test |
| Per-mechanism watts | `CollectDiagnostics` is false, so the solver does not compute or store them |

The last row is the one that is hardest to make free. Radiation, convection, solar and friction
watts per block are diagnostics that nothing in the simulation reads; they are now produced only
for a report or for a client with the crosshair readout on. A dedicated server in ordinary play
writes none of them.

**What they cost when they are on** is worth stating plainly, because every dump in this
repository was taken with them on — taking a dump is what turns them on. Each figure is
overwritten by the next substep and read between steps, so only the last substep's writes are ever
observed; the solver now writes on that substep and no other. On a 32,800-block hull at nineteen
substeps:

| | step |
| --- | ---: |
| diagnostics off — a dedicated server in ordinary play | 3.55 ms |
| diagnostics on, written once a step | **4.11 ms** |
| diagnostics on, written every substep — what the solver used to do | 6.88 ms |

Being measured cost 3.58 ms of a step and now costs 0.56 ms. The worst case for the batching is a
grid taking one substep, where the last substep is the only substep and there is nothing to skip:
0.9564 ms against 0.9517 ms, inside the noise floor. `DiagnosticBatchingTests` asserts the
published figures are bit-identical to writing on every substep, because "the last substep's
figures" has to mean exactly that.

## What it costs when it is on

The solver steps a whole grid at once, so there is no per-block callback to pay for. Per step,
per grid:

* solver-level figures — substeps, whether the step clamped, node count, critical blocks;

> **`critical blocks` was a count of events, not of blocks, until 2026-08-21.** The damage
> check runs once per substep, and a block over its rating filed an event each time, so the
> column read the block count multiplied by the substep count — twenty-six times on a
> stiff hull, and a different multiple on every grid, since substeps move with stiffness
> and settings. Any dump taken before that date over-reports it, and not by a constant.
> The solver now files one event per block per step; see
> [benchmarks.md](benchmarks.md#one-overheat-event-per-block-per-step).
* a **rotating slice** of nodes, `1/stride` of the grid, feeding the per-definition statistics
  and the anomaly detector. Every node is seen once per `stride` steps, so full per-block
  coverage costs one pass spread over four steps rather than a pass every step;
* the frame's running total — one add per grid per tick, and one closed frame per rendered
  frame, into a histogram and a list of sixteen structs;
* structure — link, room, loop and queue counts — once in eight steps. Every figure there is a
  collection count; the link count in particular is now maintained by the solver, where the old
  code had to walk every cell for it.

Memory is bounded by construction. There are no sample buffers and no per-block history: every
figure is a streaming count, sum, sum of squares, min and max, or a fixed-bucket histogram. The
dictionaries are bounded by the number of block definitions (4096), grids that have existed
(2048) and anomaly kinds (64); overruns are counted and reported rather than allowed to grow.

## Frame cost and hitching

The section to read first when someone reports stuttering.

Every other cost figure in the report is per grid, and a stutter is not per grid: what a player
feels is everything that ran on one frame added together, and the per-grid rows can all look fine
while the frame does not. `FrameCostTracker` adds up what the mod spent on each frame across every
grid that ran on it, and keeps the **worst sixteen frames of the session in full**.

**Every frame carries every grid.** That is the mod's own doing, not the engine's: the engine fires
every grid's ten-frame update together, so `ThermalGridScheduler` ignores it and gives each grid a
share of each frame from the session component, with each grid spreading its solver step across the
frames of its own simulation window. The figure to read is therefore `mean` against `worst` rather
than how the grids are divided up — the division no longer varies. In the TestWorld1 dump, 29,386
of 29,387 frames did work. See
[engine-notes.md](engine-notes.md#entity-updates-are-not-staggered-across-frames--measured).

The report gives, under `Cost`:

| Figure | What it says |
| --- | --- |
| `frames with work` | frames on which any grid updated |
| `mean` / `worst` | the mod's cost per frame, all grids together |
| `over a 60 fps frame` | frames where the mod alone exceeded 16.7 ms, and what share that is |
| `worst over mean` | how spiky the session was — see below |
| distribution | the same fixed-bucket histogram the other timings use |
| worst frames | sixteen samples, worst first |

Each worst-frame sample names the frame, when in the session it happened, the total, the split
across topology, room mapping, exposure, solver, the environment sample, the after-step
observation and whatever none of them claimed, how many grids ran, which single grid was
worst and how large it is — and **what the one-shot stages touched**: nodes visited for topology,
nodes refreshed for exposure, cells flooded. Those counts are what turns "a 90 ms frame" into "a
300,000-block station rebuilt its whole conduction graph", which is a line to change rather than
a number to worry about.

### What the stages leave over

The cost table nests, and the nesting is only worth reading if the children add up to the parent.
For most of this mod's life they did not: `grid simulation` wrapped a grid's whole update, and the
`of which` rows covered only the stages inside `ThermalSimulation`. Reading the world before a step
and reading its output after one both sit in `ThermalGrid`, outside the simulation, and neither was
timed.

On the 2026-08-20 fleet dump that was **4,942 ms of 39,540 — an eighth of grid simulation belonging
to no row**, and on the steady-state worst frames almost all of them: five frames of 67–72 ms, at a
regular thirty-second spacing, reporting 2.0–2.8 ms of solver and nothing else.

Both stages are now timed, and the table carries an `unattributed` row that is the parent minus its
children rather than a measurement, so it cannot drift from the rows above it. A negative figure
there means two stages timed the same milliseconds, which is a defect in the instrumentation, and
it is printed rather than clamped.

```
  session frame
    of which grid simulation
      of which environment sample     planet, air, wind, weather, sun
        of which solar occlusion      the raycast, nested inside the sample
      of which topology rebuild
      of which room mapping
      of which exposure refresh
      of which solver
      of which after step             everything reading a step's output
        of which overheat damage      one engine call per critical block
        of which mass sweep           the rolling block-mass refresh
        of which room pressure
        of which lost-room scan       the diagnostic walk, telemetry or room overlay only
        of which hottest and health   the hottest-node read and the NaN check
        of which telemetry sampling   telemetry's own per-step walk
      unattributed                    the remainder: pacing, and anything still uninstrumented
  save
  load
  build                               a grid's one-off graph, rooms and exposure, before tick one
```

**`build` is a root of its own.** A grid builds its conduction graph, floods
its rooms and computes its exposure once, from the entity's own `UpdateOnceBeforeFrame` — outside
the session frame, outside any grid's update, and outside the save/load pair. It was timed into the
three stage rows regardless, which the table indents under `grid simulation`, so on a grid that had
just loaded the children exceeded the parent: 40.9 ms of stages inside an 11.9 ms update, on a
one-block grid in the 2026-08-20 fleet dump. It also added stage milliseconds to frames that had
not spent them, and it reached no total, so the mod under-reported itself by the largest single
call any grid ever makes — **179 ms on a 44,000-block ship**.

The three stage rows are now recorded only while a tick is driving them, and the build is timed as
its own root. `-- dump` checks the nesting on every grid of a dump.

**The thirty-second spike is not yet explained.** Nothing in the mod runs on a thirty-second period:
the mass sweep is every 8 steps and the hottest-node scan every 4, which at 8 steps a second is one
second and a half second. The next dump is what will name it, and it will name it in one of the two
new rows or in `unattributed`.

**`worst over mean` is the number to quote about smoothness.** A mod that is uniformly expensive
has a ratio near one and costs frame rate, which players tolerate; one that is cheap on average
and occasionally enormous has a ratio in the hundreds and costs a stutter, which they do not. The
two want completely different fixes, and a mean alone cannot tell them apart.

Frames costing under 4 ms are never considered for the list. Without that floor a quiet session
reports its sixteen most ordinary frames as though they were hitches.

The frame is closed at the top of the *next* frame rather than at the end of its own, because the
order the engine runs session components and entity components in is not something a mod
controls, and a frame closed before its grids have run records nothing.

## What the block overlay costs

The overlay draws every block of the targeted grid as a transparent box, on the render thread,
every frame. It simulates nothing, so its cost appears nowhere in the Cost table — a frame it
stalls otherwise reads as a simulation that stalled.

The **Block overlay** section records, per frame it drew: boxes considered, drawn, dropped as off
screen and dropped by the budget; the billboards handed to the renderer; the milliseconds spent;
and the radius the budget fitted, when it was limiting. `DebugOverlayMaxBoxes` sets the budget.

## Grids running below real time

`MaxElementVisitsPerStep` bounds what one solver step may cost, and a grid large enough to reach it
takes **shorter steps rather than coarser ones** — advancing less simulated time at exactly the
same accuracy. See [configuration.md](configuration.md#solver).

That is a legitimate state and the one the setting exists for, but it is also the difference
between a ship that cools in a minute and one that takes three, so the report says so rather than
leaving it to be discovered. Per grid, under `simulation rate`: the share of real time it is
keeping up with, and the simulated seconds it chose not to advance. The `Cost` section then names
how many grids are below real time and which is slowest, with a line saying what that means, so
nobody reads a slow-cooling ship as a physics bug.

A grid at 100 % has never hit the budget. **This paragraph used to say that was always true below
roughly a hundred thousand blocks, and it is not** — that reading came from vacuum, and air puts a
convection term on every exposed node. At the shipped 4,000,000 a driven census hull keeps all of
real time to about 64,000 blocks in vacuum, 32,000 on a planet surface and 16,000 in flight
([benchmarks.md](benchmarks.md#what-the-allowance-is-worth)).

**What a rate below 100 % is worth is measured now**, and it is what the load is doing rather than
what the grid is: a clock 10 % slow stands 2.52 K out under a moving load and 0.00 K under a steady
one, rising to 36.98 K at 60 % slow. So this figure matters most on the ship a player is actually
flying, which is the one it is hardest to notice on.

## Substeps

A solver step is divided into as many substeps as the **stiffest element on the grid** needs to
stay numerically stable, and every other element pays for all of them. Substeps advance no extra
simulated time — they are repeated passes over the same interval — so they are the whole of the
solver's bill and none of its output. A grid's cost is its elements times its substeps, and its
substeps are decided by one block.

The `Substeps` section is what makes that attributable rather than mysterious.

**What it costs.** Substep passes across the session, the solver milliseconds they took, and the
two derived rates: millseconds per substep pass, and nanoseconds per element visited. The second
is machine-independent enough to compare against the harness — a field session measured 45–58 ns
against the harness's 16, which is the size of the .NET 4.8 penalty and applies to every
millisecond figure in [load-and-hitching.md](load-and-hitching.md).

**Who is paying.** Substeps granted, bucketed both by grid count and by how many cells are in
those grids, because thirty pieces of debris and three capital ships are not the same finding.

**What sets it.** Per grid, the block that demanded the count — subtype, position and heat
capacity — beside what was demanded, what was granted, how much of that block's stiffness is
conduction rather than the sky, and the stiffest room air and coolant loop for comparison. Then
the same question by *definition* rather than by block: the twenty subtypes that ask the most of a
step anywhere in the world, with the spread of what each asked, since a block's demand depends on
what it is bolted to as much as on what it is.

**What a cap would do.** For each candidate `MaxSubstepsPerBlock`, how many blocks it would raise,
what the substep count would fall to, and what share of the element visits that removes — per grid
and summed over the world. This is the same arithmetic the setting itself uses, and
`SubstepFloorTests.TheProjectionPredictsWhatTheCapDoes` asserts that the projection and the
setting agree, so **a dump taken with the cap off already says what turning it on would buy** —
including how many blocks are stiff through radiation and convection rather than conduction, which
is the population where the trade is more visible.

Per grid, the detail section adds a histogram of blocks by the substeps they demand. Everything
here is also in `Thermodynamics_Grids_*.csv` and `Thermodynamics_BlockTypes_*.csv`, so two dumps
under different settings can be diffed rather than read.

Demand figures are computed from **real** heat capacities, never from the floored ones the solver
may be integrating with, so a profile describes the grid rather than the configuration and dumps
taken under different caps are directly comparable.

## Whether the report agrees with itself

Every aggregate in the report is a sum over the same list of grid records, taken at different
points while it is written, and the CSVs are another pass over that list after the text is
finished. Nothing about that is supposed to be able to disagree.

A dump of 18 August did. The Cost table's merged `grid simulation` total came to 62 % of the sum
of the per-grid rows printed further down the same file, and every session counter — frames,
steps, node updates — was short by a similar factor. The per-grid rows and the CSV agreed with
each other to the last decimal, so whatever moved, moved between sections.

Reading the code did not settle it. `TimingStat.Merge` is arithmetically correct and now has a
test that sums two hundred stats to prove it, the Cost table walks the same list the CSV does, and
between them nothing runs that could tick a grid. So the report checks itself instead. The
**Consistency** section, written last, re-takes the Cost table's own figures and states the two
invariants in a form that can fail loudly:

* the record list must not change while the report is being built;
* a grid cannot tick more often than the session is framed, because both happen once each in the
  same `Simulate` call.

It prints both readings of records, ticks, milliseconds and steps, the frame range each record was
ticked over, and the longest grid lifetime beside the session clock. A line beginning `!!` is a
defect in the telemetry rather than in the simulation.

One cause has been fixed on suspicion rather than on evidence: `Telemetry.Reset` zeroed the session
clock and cleared the registry while every live grid still held the record it had been handed, so a
record could outlive the clock its timestamps came from and go on accumulating outside every
aggregate. It now detaches those references with the list. Whether that was the cause is a question
for the next dump — a report written while the world was closing could equally explain it.

Telemetry reset runs inside the session's independent cleanup stages. If it throws, the error is
reported and the remaining unload actions still execute. The live-grid detachment checks for null
entries and missing simulations. The failed-unload incident and its evidence are recorded in
[known-issues.md](known-issues.md#failed-telemetry-teardown-can-poison-the-next-world-load).

## Grid heat balance

`vented W` and `made W` per grid: what the grid sheds to its surroundings by radiation and
convection, and what it puts into itself through waste heat, sunlight, friction and registered heat
sources. Sampled per step from figures the solver accumulates on the hot path rather than behind
`CollectDiagnostics`, because the question they answer — can this ship cool itself — is one asked
of a working ship rather than an instrumented one.

At equilibrium the two are equal. A grid whose `made` exceeds its `vented` is storing the
difference, and its temperatures will keep climbing until radiation catches up or something melts.
Venting reads zero rather than going negative while a grid is net absorbing, which a hull in
sunlight or in warm atmosphere legitimately is.

**`friction W` is one of the terms inside `made W`, reported separately because it is drag power.**
The solver computes `FrictionScale × ρ × v_rel³ × area × windward exposure` summed over the grid's
nodes, and real drag power is `½ C_d ρ A v³` — the same expression. So this column is the magnitude
of a force the mod does not apply: it turns the energy into heat in the hull and takes nothing from
the ship's motion, which over the 8,144 published blueprints at `reentry` is a median **5.05 MW** on
**100 %** of hulls, 16.8 kN at 300 m/s ([backlog.md](backlog.md) `K1`). A reader adding it to
`made W` counts it twice.

It is summed on the hot path beside the other two rather than behind `CollectDiagnostics`, for the
same reason and one more: `ThermalNode.LastFrictionWatts` is filled only when diagnostics are on, so
anything derived from walking the nodes would exist while somebody was watching a debug panel and
not otherwise.

Measured cost of collecting them: none detectable. The per-node figures were already computed by
the pass that walks every node each substep, so this is two adds; a before-and-after run of
`bench elements` at a hundred thousand nodes moved the per-node cost from 6.1–6.6 ns to 5.8 ns,
which is inside that benchmark's run-to-run spread.

## Room pressure sweep

`of which room pressure` in the cost table times the per-compartment sweep that asks the game how
full each room is. It is nested inside grid simulation like the other stages, but the host drives
it rather than the simulation, so it is timed by an explicit start and stop rather than through a
simulation phase.

Four counters sit beside it, per grid:

| Field | Meaning |
| --- | --- |
| `room pressure sweeps` | sweeps run, with compartments visited and game API calls made |
| `of which read vents` | how many sweeps needed the air-vent fallback, and how many vents those walked |

The counters are the point. A millisecond figure on a twelve-room ship cannot answer whether the
sweep scales badly on a station with thousands of compartments, because the answer is proportional
to compartment count and the ship has twelve. `game calls` divided by `compartments` should stay at
exactly two; `read vents` should stay at or near zero on a world whose gas system answers, and a
number close to the sweep count means something is permanently unanswered — see
[known-issues.md](known-issues.md).

## Work counters

Alongside the millisecond figures, the simulation counts what its one-shot stages *touched*:
rebuilds, node visits, links built, cells flooded, loop searches, substeps. See
`SimulationWork` in the core.

The two kinds of number answer different questions, and the counts are the ones that travel. A
topology stage with a worst call of 90 ms and a node-visit count equal to the grid size is a
global rebuild — an algorithm problem, reproducible anywhere. The same stage with a count of
forty is a slow machine. Milliseconds alone cannot distinguish them, which is why a report that
carried only timings could say a session stuttered without saying why.

They are also what the load tests in `tests/` assert against, so a claim proved on a benchmark and
a claim observed in a real world are the same claim.

## Output

These files land in the world's storage folder
(`%AppData%/SpaceEngineers/Saves/<world>/Storage/<mod>/`), stamped with the session start time:

| File | Contents |
| --- | --- |
| `Thermodynamics_Telemetry_<stamp>.log` | the full report |
| `Thermodynamics_BlockTypes_<stamp>.csv` | one row per block definition |
| `Thermodynamics_Grids_<stamp>.csv` | one row per grid |
| `Thermodynamics_Rooms_<stamp>.csv` | one row per compartment — see [Room dump](#room-dump) |
| `Thermodynamics_Surfaces_<stamp>.csv` | one row per block face — see [Surface dump](#surface-dump) |
| `Thermodynamics_Environment_<stamp>.csv` | one row per grid per climate sample — see [Climate dump](#climate-dump) |
| `Thermodynamics_PlanetProbes_<stamp>.csv` | one row per probe, written only when a sweep ran |

A summary line always goes to `SpaceEngineers.log`. If world storage cannot be written — the
failure mode most likely during shutdown — the whole report goes to the game log instead, so a
run is never lost silently.

## Surface dump

`Thermodynamics_Surfaces_<stamp>.csv` is written with every report: **one row per block face**, six
rows per live block, on every grid in the world.

| Column | Meaning |
| --- | --- |
| `grid`, `grid_id`, `block`, `subtype` | which block, on which grid |
| `cell_x/y/z`, `size_x/y/z` | the block's minimum cell and its extents |
| `face` | which of the six sides the row is about |
| `face_cells` | cell faces on that side — the four counts below sum to this |
| `exposed` | cell faces the model counts as open to the sky |
| `sealed` | rejected: something airtight on the other side |
| `mounted` | of the exposed faces, how many also carry a mount joint — a grating or catwalk bolted flat against a face that still sees the sky. A subset of `exposed`, not a rejection. |
| `interior` | rejected: the space beyond is a sealed room, not outdoors |
| `sun_dot` | how square the face is to the sun, −1..1 |
| `sun_lit_fraction` | the share of *this face* the sun reaches, from the shadow map — per face, since the far layer of a wall is dark toward the sun and lit on the flank |
| `solar_w`, `temperature_k`, `exposed_area_m2` | what the block is doing with all that |

The three rejection columns are the point of the file. A face that looks open in game and reports
`exposed 0` is answered by whichever of them is non-zero, and they are indistinguishable from the
exposure total alone. The same breakdown is on the crosshair readout, per face, for the block being
looked at — see `DebugTextOnScreen`.

Rows are captured per grid at its final snapshot — when it closes, or when the report is written —
rather than read from the live grids at report time. That is the difference between a full file and
a header: the report is normally written as the world closes, and by then no grid is live. A record
outlives its grid, so a ship destroyed mid-session still contributes its faces.

Limits are 50,000 rows per grid and 300,000 for the session, about fifty thousand blocks. Hitting
either logs a line and stops; it never truncates silently.

## Room dump

`Thermodynamics_Rooms_<stamp>.csv` is written with every report: **one row per compartment per
grid**, both the rooms this model found and the ones only the game has.

Both kinds are in one table on purpose. The question a reader has is "what rooms does this ship
have and which of them work", and answering it out of two tables that have to be joined by hand is
how this went unnoticed in the first place.

| Column | Meaning |
| --- | --- |
| `grid`, `grid_id` | which grid |
| `kind` | `mapped` when this model found the room, `lost` when only the game holds it |
| `index` | index within its kind, ordered by anchor, stable while the grid's shape is |
| `anchor_x/y/z` | the lexicographically smallest cell, which is the room's name in grid space |
| `cells`, `volume_m3` | how big it is |
| `vented` | mapped rooms: standing open to the sky through a door |
| `pressure`, `air_kg`, `temperature_k` | what the simulation is actually running with |
| `links` | blocks the air is coupled to. **Zero beside a non-zero `air_kg` means the air is inert** — it has mass and touches nothing |
| `game_airtight` | `MyCubeGrid.IsRoomAtPositionAirtight` at the anchor — the game's own verdict |
| `vents` | the air vents opening onto it, by terminal name |
| `vent_pressurised` | whether a vent in it reports `IsPressurized` — the game's answer about its own room |
| `oxygen_level` | the highest level any vent in it reports, or −1 when none did |
| `game_oxygen` | the game's own oxygen level in the room, 0..1, or −1 when it could not be asked. Read from the grid's gas system, independently of our map and of any vent |
| `disagreement` | mapped rooms: **the game has air in it and this model runs none** — a compartment detected and not filled, which is a different failure from one not detected at all. Note `game_airtight` means *sealed*, not *full*: a sealed empty cupboard is correct in both models and is not flagged |
| `leak_faces` | lost rooms: faces this model leaves open that the game seals |
| `leaking_blocks` | lost rooms: the block subtypes across those faces, worst first |

**`leaking_blocks` is the fix list.** A compartment losing forty faces to one subtype names that
definition's surface bits as the thing to correct, where a cell coordinate would only say that
something somewhere does not seal.

**`vents` is the identity.** A player reports a room by the vent whose terminal says pressurised
while the overlay shows nothing, not by its coordinates, so that is what the row is named by.

The report carries the same table per grid under **compartments**, capped at 60 rooms before it
defers to the CSV, with a headline count of how many compartments the game holds that this model
does not — and how many of those have a vent reporting pressurised.

### What produces the `lost` rows

Every cell the room map calls external is offered to `IsRoomAtPositionAirtight`, and the ones the
game calls airtight are grouped into connected regions
([UnmappedRooms](../Thermodynamics/Core/Surfaces/UnmappedRooms.cs)). Each region is a
compartment this model lost: the flood fill walked in from outside, no room was created, and
because pressurisation is only ever asked about rooms the map already found, nothing ever compared
the two.

The scan costs one call into the game per external cell. It runs only when the room overlay is up
or telemetry is on, on a 240-step cadence, **and only when the room map has changed since the last
scan** — a lost compartment is a property of the map, so a fleet at anchor is scanned once rather
than every thirty seconds. A field dump recorded the cost of not gating it: one frame spent 108 of
its 111 ms in the after-step, over 292 grids. It is still forced once at dump time so a report
written moments after a wall was welded describes the ship as it is, and that scan satisfies the
cadence. It is capped at 200,000 cells and says so in the report when it hits that rather than
truncating silently.

The same regions are drawn by the room overlay in red, one hue per region, brightest where a vent
in them reports pressurised — so a hole in the model reads as a hole rather than as an absence.

## Climate dump

`Thermodynamics_Environment_<stamp>.csv` is written with every report: one row per grid every ten
seconds of play, describing the world at that grid rather than the grid itself. It is the file for
balancing a planet's climate.

| Column | Meaning |
| --- | --- |
| `time_s`, `grid`, `grid_id`, `planet` | when, where, and on what |
| `altitude_surface_m` | height above the ground directly below |
| `altitude_sealevel_m` | height above the planet's mean radius |
| `latitude_deg` | against the planet's own axis: −90 at one pole, +90 at the other |
| `sun_elevation_deg` | the sun's height above the horizon, negative at night |
| `air_density`, `atmosphere_factor` | what the game reports, and what this mod makes of it |
| `ambient_k`, `ambient_c` | the ambient this mod produced |
| `underground` | 1 when the game says the grid is below the surface |
| `depth_m` | metres of ground over the grid — the figure the underground model actually uses. Zero or less is open air |
| `solar_w`, `solar_occlusion` | irradiance after atmosphere, weather and shadow, and the share shadowed |
| `convection_coeff` | the coefficient in force, W/(m²·K), with the wind and weather terms already in it |
| `wind_speed`, `wind_bearing_deg` | the wind the model uses, and where it is going: 0 north, 90 east |
| `wind_ceiling` | the game's own figure, which is the maximum the field scales |
| `weather` | the game's name for the weather standing over the grid — `RainHeavy`, `SnowLight` — empty in clear air |
| `weather_intensity` | the game's weather intensity at that point |
| `weather_ambient_k` | what that weather did to the air, K. The column that says the model reacted to it at all |
| `game_comfort` | the game's own comfort figure at that point, 0..1 — `GetTemperatureInPoint`. **Not a temperature and not a cross-check**: it is a fraction, never kelvin, and it is zero wherever there is no oxygen. Written as `game_temperature` in any dump taken before 2026-08-22. |
| `surface_material` | the voxel material under the grid: snow, sand, grass, ice |
| `grid_mean_k`, `grid_peak_k` | what the grid itself did about all of it |

The rows are raw on purpose. A climate question is the *shape* of ambient against altitude, against
latitude and around a day, and an average has none of that in it. The report also carries a
**Climate** section summarising each planet — ambient by day and by night, air density, altitude,
solar, wind, convection, depth where anything was underground, the weathers seen and what they were
worth, and the ground materials — for the headline without opening the file.

Depth and weather are summarised only over the rows they happened on. Averaging a storm against the
clear days either side of it reports a drizzle that never fell, and averaging a mine shaft against
the surface reports a hole nobody dug.

Capped at 4000 rows per grid, about eleven hours of play at the default cadence, and logged when hit.

## What is collected

**Session** — world name and path, online mode, server/dedicated/multiplayer, real and in-game
elapsed time, frame count, and every setting that produced the numbers. The settings section is
generated from the same name table the chat commands and the mod API use, so a setting added to the
config cannot go missing from the report meant to explain a run.

**World** — the game's own session settings, the game version, and the mod list. Taken by
serialising `MyObjectBuilder_SessionSettings` and flattening it, rather than from a field list here,
so a setting the game gains appears in the next dump without a code change.

The world outranks the mod, and the section names each place it does. `EnableDamage` with the
world's `DestructibleBlocks` off records damage the engine then discards; `EnableRoomAir` without
`EnableOxygen` or `EnableOxygenPressurization` leaves every room airless; `EnableSaving` off means
no dump can show a load. Each is printed as a `!!` line above the settings, because a dump read
without them reads as a broken model rather than a silenced one.

**Per grid** (kept for the life of the grid, and retained after it is destroyed) — node count,
block count, conduction links, sealed rooms, exterior cells, surface entries, coolant loops,
`RecentlyRemoved` size and room-mapper queue depth, as min/mean/max; block add/remove/ignore
counts, splits, merges, door state changes, surface refreshes, mapper passes completed, saves
and loads with payload sizes and blocks and rooms restored; simulation steps, node updates, sampled nodes,
**solver substeps and steps clamped by the substep cap**, critical block counts, damage events
and total damage, peak temperature with the block that reached it, and the final temperature
histogram; ambient temperature, air density, atmosphere factor, wind speed, convection
coefficient, effective solar energy, grid speed, the fraction of time the sun was occluded, the
fraction of time in atmosphere, and which planets were visited.

**Room mapping, per grid** — how the mapper classified the padded search box: exterior cells,
structure cells and room cells, which sum to the box exactly, plus the two numbers that explain a
room that will not seal. *Block cells left outdoors* counts cells holding a block that the map
treats as open space; that is normal for anything the game does not consider airtight — a
reactor, a lattice, the way through an open door — and it is the first thing to read when a
sealed-looking interior maps as no room. *Sealed structure read as open* counts cells that seal on
all six faces and are still not structure, which nothing can reach and so is always zero unless
the map and the grid disagree; it raises an anomaly. The last completed pass also reports its
search volume, its block cell count, how many block cell faces do not seal, how many blocks seal
on no face at all, and a bounded list naming the blocks left outdoors with their surface bits and
which of their faces are open.

The audit runs once per completed mapper pass, never while one is in flight, and never before the
first pass has finished. Both exclusions are there because both fired. A map that predates the
grid reports every block placed since as a disagreement it is not; and a grid that closes inside
its first few seconds — a paste preview, a subgrid — never maps at all, and still holds the
all-external map the mapper hands out until it has built one. Audited against that, a sound hull
reported all 27 of its cells as unaccounted for. Such a grid now prints `mapped never (no pass
completed)` and no figures, rather than zeroes that look like measurements.

**Per block definition** (aggregated across every grid) — how much of each of the six faces the
definition seals and mounts, and how often a block of that type was observed with its sealing
switched off by an open door; the thermal properties in force, block
size, placed/removed/live/peak counts, mass, thermal mass, exposed surfaces and area, and
min/mean/max/sd for temperature, per-step ΔT, conduction, radiation, convection, solar and
friction watts, heat generation, power produced and consumed and thrust draw; critical updates
and heat damage dealt; and both a sampled and a final temperature histogram.

**Cost** — call count, total, mean, worst and a distribution for each path, laid out by nesting.
The session frame is the outer measurement: it wraps the session component's per-frame call, which
is what drives every grid. Inside it sits the grid update, and inside that the four stages the
simulation reports through `ISimulationProfiler` — topology rebuild, room mapping, exposure
refresh and the solver itself — plus the solar occlusion raycast. Save and load are raised by the
engine outside the frame and are measured separately.

Only the unindented rows are summed into `total measured`, because an indented row's milliseconds
are already inside the row above it. `CostRollup.MeasuredMilliseconds` is the one place that
arithmetic happens, and it takes no grid-simulation argument by construction. Before that,
the total added grid simulation to the session frame that contains it, which reported a field dump
costing 36.8 % of real time as costing 73.1 % — roughly double, on any world where the frame is
mostly simulation, which is every world.

Splitting the update by stage is what makes the cost numbers actionable: topology and room
mapping run on block changes and are the expensive pair, while the solver runs every step and is
usually not the problem.

**Anomalies** — NaN, infinite and implausibly high temperatures, temperatures clamped to zero
from a positive value (the signature of an unstable step), and any exception caught inside the
module. Each is recorded once per kind with a count and its first and last example. Faults are
listed first and marked `!!`; see [above](#faults-are-recorded-whether-or-not-collection-is-running)
for why they are recorded even when nothing else is.

An exception's example carries its type, its message and the top six stack frames. The message
alone does not say which call threw, and the throw is often inside game code the mod only reaches
indirectly — "capacity was less than the current size" is not a diagnosis, but the same line with
`ThermalBlockCatalog.BuildSurfaces` two frames below it is. The bound is what makes that
affordable: only the first and last example of each kind are kept, however many times it fires.

Sections of the report are written from whichever thread caught the event. The game builds pasted
and projected grids on worker threads, so the registries behind grids, block types and anomalies
are locked; see F1 and F2 in [known-issues.md](known-issues.md) for what happened
before they were.

## Coolant loops and heat pumps

> **The grid CSV's loop and pump columns are session means, and a mean does not carry the
> invariants the quantity does.** `pump_cop` is the mean of each step's coefficient, not
> `pump_lift_w / pump_draw_w`, and the two agree only for a pump whose throttle never moved — so a
> dump cannot be used to check a coefficient against its own lift and draw. The one claim that
> survives the averaging is that a pump whose mean draw is exactly zero drew zero on every step,
> and therefore lifted nothing; `-- dump` checks that and nothing stronger. The energy balance the
> columns cannot check — that the hot side receives the lift plus the work — is pinned offline by
> `HeatPumpTests.TheHotSideGetsTheLiftPlusTheWork`.
>
> A coefficient below one is not a fault. Lifting across a wide gap costs more electricity than it
> moves heat, which is what the Carnot relation says: the 2026-08-20 fleet dump has a pump at 0.21,
> drawing 20 kW to move 4.3 kW.


Until this was added, a report could tell you a grid held *n* coolant loops and nothing else about
them. A field dump showed four ships each carrying a closed pumped ring, eight radiators and four
heat pumps, and could not say whether any of it moved a single watt — while the session summary line
`coolant loops created` read `0`, because nothing had ever incremented that counter.

Per grid, when the grid carries any:

```
    coolant and heat pumps
      loop temperature K          339.0 / 412.6 / 498.1 (sd 41.2, n 233)
      drawn from blocks W         0 / 18,402 / 41,118 (sd 9,004, n 233)
      shed into blocks W          0 / 17,988 / 40,233 (sd 8,911, n 233)
      net into the fluid W        414
      pipes per loop              72 / 72 / 72 (sd 0, n 233)
      links per loop              80 / 80 / 80 (sd 0, n 233)
      heat pumps                  4 / 4 / 4 (sd 0, n 233)
      of which running            3 / 3.2 / 4 (sd 0.4, n 233)
      idle: nothing on a face     233 samples
      lifting W                   0 / 18,811 / 60,000 (sd 4,102, n 700)
      drawing W                   0 / 20,000 / 20,000 (sd 0, n 700)
      rejecting W                 0 / 38,811 / 80,000 (sd 4,102, n 700)
      coefficient                 0.94 / 2.31 / 8.00 (sd 1.88, n 700)
      bound by its rating         41.2 % of running samples
      bound by Carnot or heat     58.8 % of running samples
```

**Drawn and shed are gross and are deliberately not summed.** A loop doing its job draws heat off a
reactor at one sink and sheds it into a radiator at another, so its *net* is around zero exactly when
it is carrying its full load. A single net figure would report a loop moving 40 kW as an idle one.
The net is printed as well, because a loop still warming up is a different state from one in balance.

**A pump that is doing nothing is reported by the reason, in the order a player can fix them:**
nothing bolted to a face, then switched off, then given no power. A pump can only be one of these,
and a pump that clears all three appears in the `lifting` figures instead.

**Which limit bound the pump is the block's whole character.** At its rating the machine ran out
before the physics did — the gap is small and the pump is cheap. Short of its rating the Carnot cost
or the heat available in the cold block stopped it, which is what makes a wide lift ruinous. A mean
coefficient below 1 is called out in the report: those pumps spend more energy than they move.

Two lines flag the failures that look like success. A grid whose every loop carried nothing gets a
`!!` line, because a warm loop that never moves heat looks healthy on a temperature readout. So does
a grid where no heat pump ran at all.

The session summary reports `grids with a coolant loop` and `grids with a heat pump`, each with how
many of them were actually working — which is the figure that would have shown, at a glance, that two
of six identical ships had no loop.

Why a loop did not form is a separate question, answered by `ThermalSimulation.DiagnoseLoops` rather
than by the periodic sample, because it needs a walk rather than a reading. See
[blocks.md](blocks.md#when-a-ring-does-not-become-a-loop).

## Tests

Most of the module reads `Sandbox.*` and `VRage.Game` types, which cannot load in [`tests/`](../tests).
Its decision logic does not, and lives in files that reference nothing but `System`:

| File | What it holds |
| --- | --- |
| `TelemetryStats.cs` | `RunningStat`, `Histogram`, `TimingStat` |
| `TelemetryAnomalies.cs` | anomaly classification per node and per grid, the sampling gate, `AnomalyRegistry` |
| `TelemetryFormat.cs` | report and CSV formatting |
| `WorldSettings.cs` | flattening the game's serialised session settings, and the mod-feature conflicts |
| `OverlayTelemetry.cs` | the block overlay's cost accumulators |
| `FrameCost.cs` | the per-frame cost tracker and worst-frame ring |

`Thermodynamics.Tests` links those directly — the same files the game compiles, not a
copy — and covers them in `TelemetryStatsTests.cs`, `TelemetryAnomalyTests.cs`,
`TelemetryFormatTests.cs`, `AnomalyRegistryTests.cs` and `GridHealthTests`.

`AnomalyRegistry` was pulled out of `Telemetry` for exactly this reason: the rule that decides
whether a problem is recorded at all was wrong for a long time, and it was wrong where nothing
could reach it. It is now the same shape as the classifier beside it — a decision with no game
types in it, and tests on both directions of every branch.

The instrumentation hook itself is tested in `ProfilerTests.cs`, on the model side: every stage
is bracketed, an idle update reports no solver stage, a block placement reports a topology
rebuild, and — the two that matter most — an instrumented run and an uninstrumented one produce
**identical temperatures**, with and without diagnostics.

What is not covered is the wiring: which game fields each hook reads, and whether the report is
written successfully during shutdown. That is the same boundary the rest of `tests/` accepts, and
it can only be checked by loading a world. **The coolant and heat pump section above is inside that
boundary** — `GridTelemetry` and `TelemetryReport` both read `Sandbox.*`, so the section's rendering
is compile-checked and not test-covered. What it reads is covered: `CoolantLoopTests` pins the loop
watt figures and `HeatPumpTests` pins each of the three idle states the section classifies.

`dotnet build Thermodynamics/Thermodynamics.csproj --no-incremental` is the only check that compiles this module at all —
a green `tests/` test run says nothing about it, because `tests/` links four of its files and skips the
rest.

## Settings

```xml
<EnableTelemetry>false</EnableTelemetry>
<TelemetrySampleStride>4</TelemetrySampleStride>
```

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-20 | Document fault-isolated teardown after the live Telemetry.Reset unload exception. |
| 2026-09-18 | Added bounded client thermal-probe telemetry, outcome and CPU summaries, recent model/viewpoint events and tester notes in the normal report. |
| 2026-08-25 | Two headings described what a row *used to* be rather than what it is (`R12`). `build` is a root of its own; the per-mechanism watts row is the one hardest to make free. |
| 2026-08-22 | Renamed `game_temperature` to `game_comfort`. `GetTemperatureInPoint` returns a 0..1 fraction and zero wherever there is no oxygen, so the old name claimed two things it is not ([backlog.md](backlog.md) `B23`). A dump written under the old name is still read, and `TheComfortColumnIsReadableUnderEitherName` keeps it that way. |
| 2026-08-22 | Added the standard header and this change log. |
| 2026-08-21 | Gave every substep count the step length it was counted against, and filed one overheat event per block per step rather than per substep — which had been multiplying the reported critical-block count by the grid's substep demand. |
| 2026-08-20 | Recorded faults whether or not collection is running, and caught a grid that has gone numerically bad without collection on. Measured the per-grid visit instead of inferring it, and attributed the eighth of a grid's update that had belonged to nothing. |
| 2026-08-19 | Reported the convection coefficient after the atmosphere blend rather than before it, showed what a grid vents against what it makes, and measured the room pressure sweep. |
| 2026-08-12 | Opened the module: off by default, switchable at runtime, and costing a handful of static bool reads when off. |
