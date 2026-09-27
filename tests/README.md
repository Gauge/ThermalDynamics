# Isolated simulation environment

Runs the thermal simulation outside Space Engineers, so it can be built, tested, profiled and
debugged in seconds instead of by loading a world.

> The rules argued here are stated canonically in [rules.md](../docs/rules.md): `E7` `D2` `D4`
> `D5` `D8` `C1` `C2` `C5` `M11` `R10`.

| Looking for | Go to |
| --- | --- |
| What the simulation does | [thermal-model.md](../docs/thermal-model.md) |
| Building and deploying the mod | [development.md](../docs/development.md) |
| The performance report and the scale ladder | [benchmarks.md](../docs/benchmarks.md) |
| The balance lab this harness runs | [balance-lab.md](../docs/balance-lab.md) |

## Layout

`Thermodynamics.Core` builds only the simulation model. Thermal-vision algorithms
compile separately in `Thermodynamics.Presentation`, linking
`Thermodynamics/Presentation/**/*.cs`. The harness, visual benchmarks
and tests reference presentation explicitly; the model does not. The SE1 renderer
is compile-checked by the mod project, not linked into either library.
`CoreIsolationTests` prevents presentation types from returning to the simulation
assembly and checks both libraries' permitted assembly dependencies.

> **Hulls are built from a measured block census.**
> [`Census.cs`](Thermodynamics.Harness/Census.cs) holds the block population of a real ship —
> eight bands by heat capacity, plus the share that generate waste heat — read out of a telemetry
> dump's block-type table. Every benchmark builds from it, because a step takes as many substeps
> as the *stiffest* block needs, so a hull's cost is decided by its lightest block. The old mix of
> heavy armour and gratings asked for three substeps where a real ship asks for twenty-one to
> thirty-one, which made every scale figure about six times too cheap. `CensusFidelityTests`
> fails if the census drifts away from the field observations recorded beside it in
> `Census.Field`; refresh both when a new dump arrives.

> **A field dump is kept beside the census.**
> [`benchmarks/field-dump/`](benchmarks/field-dump) holds three CSVs of one real telemetry dump —
> 264 climate rows, 242 grids, 560 block types — under the names the mod writes them with.
> `DumpAuditTests` audits them on every run, so the claims the model makes are checked against
> numbers a world produced rather than numbers a test invented. It found two faults on its first
> pass: a grid's stages exceeding the update they nest inside, and a block type's peak temperature
> sitting below its own maximum. Both are fixed and the fixture still carries them, because a dump
> records what the mod was when it was taken. Refresh it when a dump arrives, from `-- dump`.

| Project | What it is |
| --- | --- |
| `Thermodynamics.Core` | The simulation itself. Sources live in [`../Thermodynamics/Core`](../Thermodynamics/Core) so the game compiles them as part of the mod; this project links the same files. |
| `Thermodynamics.Harness` | Synthetic block catalogue, grid builder, canned environments, scenario library. |
| `Thermodynamics.Sim` | Command line front end for the scenarios. |
| `Thermodynamics.Tests` | xUnit suite. |
| `../Thermodynamics/Thermodynamics.csproj` | The mod project (MDK2): the mod's own compile check and whitelist check, and **not** part of the isolated environment. It is in the solution so that building the solution compiles every file the game compiles; see [rules.md](../docs/rules.md) `C11`. Building it also deploys the mod to the game's local `Mods` folder; see [development.md](../docs/development.md#deploying-to-the-game). It runs nothing and `dotnet test` skips it. |
| `../tools/modkit/Modkit.csproj` | Compiles the drop-in API client as a consumer mod would, as C# 6 through the whitelist; see [tools/modkit/README.md](../tools/modkit/README.md). |

## What makes it isolated

The four projects above reference exactly one Space Engineers assembly: `VRage.Math.dll`, for
`Vector3I`, `Vector3`, `Matrix` and `Base6Directions`. That is pure managed maths and loads fine on
.NET on Linux. There is no `Sandbox.*`, no `VRage.Game`, no `MyAPIGateway`, no session, no entity.

The mod project is the exception and is deliberately outside this: it references the whole engine,
targets `net48`, and exists to fail when the adapter no longer compiles. Nothing links against it
and no test loads it.

Everything the game would supply crosses one of three boundaries:

| Boundary | Type | Supplied by the game as |
| --- | --- | --- |
| Block layout | `GridModel` / `BlockInstance` / `BlockModel` | `MyCubeGrid` and `IMySlimBlock` |
| Environment | `EnvironmentSample` | planets, sun raycasts, weather, grid velocity |
| Output | `OverheatEvent`, node temperatures | damage calls, HUD, block colouring |

A test fills those in with plain numbers; the game adapter fills them in from the session. The
solver cannot tell the difference, which is the point.

## Where the defects have actually been

Three faults found in one pass over a field dump had the same shape, and it is worth stating
because it says where to write the next test rather than where the last one was.

| Fault | Why the suite missed it |
| --- | --- |
| A grid's substep demand was conduction-only until it had stepped | every test measures a grid *after* stepping it |
| A grid's one-off build was charged to no row in the cost table | nothing read the report the mod writes |
| A block type's peak temperature could sit below its own maximum | nothing read the report the mod writes |

**The suite tests the simulation, and the things around it were untested**: the state a grid is in
before its first step, and the artefacts the mod produces about itself. Both are exactly where a
fault survives longest, because both look like output rather than behaviour and neither changes a
temperature. `DumpAuditTests` and `FieldDumpTests` close the second; the first is a habit — when a
figure is read outside a step, test it outside a step.

The adapter under `Game/` remains the largest uncovered surface; see F1 in the
[backlog](../docs/backlog.md).

`LangVersion` is pinned to 6 on the core project — the same version the in-game script compiler
accepts — so the language features used here are ones the game accepts.

That is not the whole contract, though: the game also applies a **type whitelist** that this
project cannot check. `IndexOutOfRangeException` and `ArgumentOutOfRangeException` are both
prohibited in game and both compile happily here, so a green test run is necessary but not
sufficient. See [development.md](../docs/development.md#repo-conventions) for the list.

## Long walks, and builds that lie

**Build the test project, not the solution, before `--no-build`.** `dotnet build` from the repo root
leaves `Thermodynamics.Tests.dll` stale — measured at **25 minutes** behind the sources in one case,
with `-m:1 --no-incremental` and no error printed. A `dotnet test --no-build` then reports the
*previous* edit's results, which reads as a test that mysteriously still fails after being fixed, or
worse, one that passes after being broken. It cost three false readings in a single session. Build
`tests/Thermodynamics.Tests` by name, and if a result looks impossible, check the DLL's timestamp
before believing it. **And an incremental build does not notice a property passed on the command
line**: `dotnet build -p:Optimize=false` over an up-to-date tree recompiled nothing and left the
optimised assemblies in place, so a check proven against a *flag* has to be proven against
`--no-incremental` as well — which is how `OptimisedBuildTests` was shown to fail before it was
believed:

```bash
dotnet build tests/Thermodynamics.Tests -v q --nologo
ls -la --time-style=+%H:%M:%S .build/Thermodynamics.Tests/bin/Debug/net9.0/Thermodynamics.Tests.dll
```

**Clean up after a build.** `dotnet build-server shutdown` does *not* reap the `nodeReuse` MSBuild
worker nodes; they idle out after ten to fifteen minutes and hold about 128 MB each until they do.
Kill them if the machine is wanted.

**An hours-long walk is taken in slices, not in one hold.** Every corpus sweep resumes exactly — a
blueprint is written to `done-<walk>.txt` only once every ship in it has been recorded (`O3`), so a
killed slice loses the batch it was in and nothing else, so a walk too long for one sitting can be
relaunched until it finishes:

```bash
THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=out/survey-2026-08-25 \
  dotnet test --filter CorpusSurvey                                # repeat until it completes
```

Do not delete the data directory between slices — that is what starts the walk over.

**But slice only if you must, because a sliced walk costs about four times a single-hold one.**
Resume skips what *finished*; a slice killed by its deadline discards whatever its workers had in
flight, and the corpus is walked largest-first, so those are the most expensive ships in the
population and the next slice does them again. Measured on the same corpus, in one hold against the
nine-slice walk the published costs were calibrated from: **11 m 19 s against 35.6 minutes** in air,
**21 m 32 s against 78** in the cap walk's paired arms. Ask for a hold long enough to finish, and
treat a cost measured across slices as not a cost at all (`M1`).

**Or walk the core corpus and finish inside one hold.** `tools/corpus/core.py` draws a weighted
sample that costs a tenth of the corpus — **11 m 19 s in air and 21 m 32 s in the cap walk's paired
arms, both measured in one hold on 2026-08-28** — and answers
`G6`'s percentiles, the demand half and the medians to within a few per cent, measured out of
sample. It does not answer a maximum or a rate under about half a per cent, and it is read with
`core.py --score` rather than `verdict.py`, which has no weights and stops rather than printing a
sampled dataset as a population.

```bash
python3 tools/corpus/core.py out/air-2026-08-28 --out $PWD/out/core-selection.txt
THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=$PWD/out/core-2026-08-28 \
  THERMAL_CORPUS_ONLY=$PWD/out/core-selection.txt \
  dotnet test --filter CorpusAirWalk
python3 tools/corpus/core.py --score out/core-2026-08-28
```

**The paired cap walk is the one core walk `cap.py` reads instead**, because a paired dataset is a
comparison rather than a population and `cap.py` is the tool that compares arms. It applies the
same weights from the same file and recognises a core dataset by the same test, so nothing changes
in the command except the filter:

```bash
python3 tools/corpus/core.py out/air-2026-08-28 --out $PWD/out/core-selection.txt
THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=$PWD/out/cap-core-2026-08-28 \
  THERMAL_CORPUS_ONLY=$PWD/out/core-selection.txt \
  THERMAL_CORPUS_PROGRESS=$PWD/out/cap-core-2026-08-28/progress.txt \
  dotnet test --filter CorpusCapWalk
python3 tools/corpus/cap.py out/cap-core-2026-08-28 \
  --csv tools/corpus/summary-cap-core-2026-08-28.csv
```

The report says on its first line that it has a core dataset, weights every population figure and
names it `weighted`, and reports the halves a sample cannot answer as `?` rather than passing them
— a maximum is one observation, and the core corpus reads 76 % under the population's `work max`.

The whole corpus stays on disk and a full walk is one variable away; see
[tools/corpus/README.md](../tools/corpus/README.md#the-core-corpus--a-walk-of-the-population-in-eleven-minutes)
for what the sample can and cannot answer, and why dropping the giants outright would have deleted
`G6`'s finding rather than made it cheaper.

**A slice shorter than one blueprint does no durable work.** A path is recorded only when it is
finished, so a slice killed part-way through a hull loses that hull's work entirely — and with 31
workers in flight, a slice shorter than the batch loses all of them. Measured on 2026-08-25: the
first 13 blueprints took 23 minutes between them, an 8-minute slice after them completed **none**,
and a 22-minute slice completed **one**. Twenty-five minutes is a floor for the first hour of a
survey, not a default to shorten.

**Progress is read in bytes; a finishing time is not read from them.** Those 13 files are 0.16 % of
the corpus by count and 4.06 % of it by size, and the second is the honest statement of *how far in*
a walk is:

```bash
for f in $(cat out/survey-2026-08-25/done-survey.txt); do stat -c%s "$f"; done |
  paste -sd+ | bc            # how far in, against the corpus total
```

**Dividing that by elapsed time is not an estimate of the remainder**, and `pace.py` exists because
that mistake abandoned a walk once already. The corpus is walked largest first, so the rate falls
throughout every healthy run and is a property of the ordering rather than of the walk: on
2026-08-25 the same survey ran at 56 MB/min over its first 13 hulls and under 3 MB/min over its
fourteenth. **The estimate that means something is a finished walk of the same shape, times the
ratio of work per ship** — `pace.py --reference`, and read its output on a walk that is over before
trusting it on one that is not.

**A killed build node is not a test failure, and it reads exactly like one.** Twice on 2026-08-25 a
queued suite came back non-zero having run no tests at all:

```
MSBUILD : error MSB4166: Child node "3" exited prematurely. Shutting down.
```

That is a build worker being reaped under memory pressure — the run never reached a test, and a log
skimmed for `Failed!` shows nothing either way. **A suite that reports no totals ran nothing**; read
the head of the log before believing a non-zero exit is about the code. `-m:1` avoids the
multi-node build entirely.

## Running it

```bash
cd tests

dotnet test                                    # the whole suite, two and a half minutes
dotnet test --filter "speed!=slow"             # the fast lane, six seconds
dotnet run --project Thermodynamics.Sim -- list
dotnet run --project Thermodynamics.Sim -- run reactor
dotnet run --project Thermodynamics.Sim -- run all --csv out/
```

The game's assemblies are found by MDK2's `Mal.Mdk2.References`, for the test projects and the mod
project alike; the build prints `Loading Space Engineers assemblies from …` with the path it chose.
See [development.md](../docs/development.md#building).

### The two lanes, and the rule that sorts them

**A class costing more than about two seconds of the suite carries `[Trait("speed", "slow")]`; the
fast lane is everything else.** That is a cost rule rather than a subject rule, and it is the second
attempt: the first said *the scenario batteries, each stepping whole ships for tens of seconds*, and
a subject rule only sorts the classes whose subject somebody remembered to look at.

**It had rotted by a factor of fifteen and nothing noticed.** Measured 2026-08-24 on this machine,
the fast lane was **3 m 45 s** — against the fifteen seconds this page claimed — because the labs
built for `C24`, `C26`, `C27` and `D19` step whole hulls and none of them was tagged. One class,
`ClientInputTests`, was 143 s of it on its own: twenty-eight tests, each a 480-second run of a
1,004-node hull, and `C26` doubled that clock from 240 s the same week. Nineteen classes were tagged
on 2026-08-24, and the lane rotted again inside two days: on 2026-08-26 it measured **37 s**, with
`DesignedHullTests` alone 35 s of test time and ten more classes past two seconds, none of them
tagged. Thirty classes carry the trait now and the fast lane is **4 s over 1,582 of the 2,001
cases** — 5.5, 5.6, 6.2 s of wall clock across three runs, so the figure is the fastest of three
and the spread is under a second (`M4`). The whole suite is 1 m 22 s.

**Something checks it now**: [`tools/lanes/lanes.py`](../tools/lanes/README.md) reads a run's trx
and reports both directions — a class over the threshold without the trait, which is what makes the
lane slow and exits non-zero, and a class tagged while costing almost nothing, which is the drift
nobody looks for. Run on 2026-08-31 it found **nothing untagged and heavy** and **thirteen tagged and
free**: classes tagged when they *were* expensive and left tagged after the performance passes made
them cheap. Untagging them put **107 cases** back in the fast lane at no change to its four seconds,
so the lane is **1,875 of 2,212** now.

*Nothing checked it for months*, and that is why it rotted. The honest check would be a class's own measured
cost, which a test inside that class cannot read; the naming rule that looks available — *a class
that drives a harness `…Lab`* — sorts the tree worse than the cost rule does, selecting eleven
classes that cost nothing and missing `ShapeTests` and `ProfileSuiteTests`, which are the second and
third most expensive things in the suite. So the rule is stated, the measurement is above, and the
refresh is: take the durations, tag what crossed two seconds (`R11` — an unchecked rule is a hope,
and marking it says so).

**The whole suite is 1 m 22 s** (2026-08-26, optimised build, 2,001 cases), and the fast lane is
what a change is iterated against. It was
published here as 5 m 23 s until 2026-08-25 and had not been that for some time — the first
measurement of the day, before anything was changed, was 2 m 55 s over the same 1,864 cases. The suite
also spent months at five minutes for a different reason — one test read the whole blueprint corpus
on every run despite its own class's opt-in rule, 4 m 57 s of a 5 m 4 s suite. It is behind
`THERMAL_CORPUS_TESTS` now, where the rest of its class already was.

### The suite runs eight at a time, and a few classes run alone

`xunit.runner.json` sets `maxParallelThreads: 8`, and the classes that must not share a machine
declare `[Collection("alone")]`: the opt-in corpus walks, and every test whose assertion is about
elapsed time.

**Eight rather than one per core, measured.** On this repository's 32-core machine, over the 1,825
cases the suite held on 2026-08-24, it is **1 m 41 s at one worker, 38 s at eight, and 1 m 47 s at
thirty-two** — one per core is no faster than serial, because the tests are memory-bound and
thirty-two of them thrash each other's cache. *(The suite is 1 m 22 s at the same eight workers on the optimised build, 2026-08-26: what
bounds it is still one class at a time — xUnit parallelises collections, a class is a collection,
and the largest classes are tens of seconds of serial work each.)*
That is the same effect, one rung up, that made the corpus walks need isolation in the first place:
four walks across thirty-one workers ran seventeen times slower than one at a time, behind a 93 %
CPU reading.

Two things go in the collection. A **corpus walk** is already internally parallel over thousands of
blueprints, so two of them at once are two thread pools competing for one memory bus;
`EveryCorpusWalkDeclaresThatItRunsAlone` finds them by the one thing they all do, which is ask
`CorpusFixture` for its files. A **wall-clock assertion** on a contended machine is measuring the
other tests — `StaggerTests` compares two cache regimes a few per cent apart and its own noise guard
caught it on the first parallel burn-in. Everything else runs beside everything else: each test
builds its own grid and shares no mutable state with another.

The rule is `O4` in [rules.md](../docs/rules.md); the row that asked for the narrowing is
[backlog.md](../docs/backlog.md) `F8`.

## Scenarios

| Name | Question it answers |
| --- | --- |
| `vacuum-soak` | How fast does a hot block shed heat into empty space? |
| `reactor` | Where does a buried reactor settle in vacuum? |
| `atmosphere` | How much does sea-level convection change that? |
| `daynight` | How far does an airless hull swing over a rotation? |
| `reentry` | Does aerodynamic heating reach the leading face? |
| `coolant` | Does a pumped ring actually move heat out of a reactor? |
| `sealed-room` | Do sealed interiors stop radiating? |
| `meltdown` | When does overheating start doing damage, and how much? |
| `radiator` | Do panels earn their mass — and what does bolting them flat against the hull cost? |
| `airlock` | Does opening a door actually let a sealed room start radiating? |
| `coolant-failure` | What happens to a cooled reactor when the pump is destroyed? |
| `welding` | Does an unfinished block swing further than a finished one? |
| `first-room` | The shape a player builds first — a shell with a door, welded a block at a time. Does it map as a room? |
| `stiff` | How many substeps does a very light block bolted to a very heavy one force, and where does it clamp? |
| `units` | The same block at three thermal clocks: what `HeatTimeScale` actually buys. |
| `perf` | Throughput on a settled solid cube, and what coalesced room mapping saves while welding. |
| `capital` | A 40,000 cell bulkheaded ship: what the one-shot rebuilds cost against a step. |
| `fleet` | Twenty ships stepped together — is the cost per cell or per grid? |
| `interior` | A hot appliance with no exposed face at all, against the same one on the skin. |
| `solver` | What a step actually costs when every link carries a gradient and the grid substeps. |
| `self-shadow` | Which faces of a solid hull are lit, and by how much, when the grid shadows itself. |
| `shadow-cost` | What keeping a self-shadow map costs on a large grid, and how often a pass runs. |
| `weather` | Does a storm reach the temperature model — the air, the sun and the convection, not just the wind? |
| `underground` | How far down does the day survive, and where does the rock start warming toward the core? |
| `cooling-plant` | **The general case.** Loop, pumps and radiators as one chain on a loaded ship — how much does the whole plant buy over the same ship without it? |
| `loop-faults` | Every way a ring fails to become a loop, on one grid, each named with its reason. |
| `loop-dry` | A loop that formed correctly and cools nothing, because no sink face meets anything. Every figure looks healthy. |
| `heatpump-backwards` | A pump installed the wrong way round — a rotation, not an obvious mistake. What does it cost? |
| `heatpump-limits` | One pump across four gap widths: which of its three limits binds, and what the coefficient costs. |
| `cooling-runaway` | More heat than the radiators can shed. Where is the knee, and does it reach damage? |
| `loop-stiffness` | A long ring is the stiffest thing a player can build cheaply. Does the substep estimate see it? |
| `loop-layout` | One ring or several, and where to put the sinks? Splitting buys nothing; spreading the sources buys 41 K. |
| `air-conditioning` | Can a heat pump cool a room? Yes, through a wall, and only a pressurised one. |
| `station` | Is a base harder to cool than a ship of the same size? Yes, and in air the reason is depth rather than area: its interior sits 39.9 K above its own skin. |

> **`perf` and `solver` are not the same measurement.** `perf` steps a settled cube of one block
> type, where nearly every link joins two cells at the same temperature — and the conduction
> loop's first act is to skip a link whose ends agree, so most of its headline figure is the cost
> of *not* conducting. It also solves in one substep, so it never exercises the loop that
> dominates a real ship. `solver` seeds a 250-750 K spread across a hollow ship built of two
> block types and reports **nanoseconds per link visit**, which is comparable across grid sizes
> and substep counts. Where the two disagree, `solver` is the one describing the game.

Scenarios are deterministic: no clock, no randomness, no dependence on iteration order. The same
scenario produces byte-identical output on every run, which is what lets them double as
regression tests (`ScenarioTests`).

## The descent

```
dotnet run --project Thermodynamics.Sim -- descent
dotnet run --project Thermodynamics.Sim -- descent --csv out/
```

A ship digging from the surface to the core, read at every depth: the sun, the wind, the day
damping out in the rock, and the rock's own heat below the deadzone. It is the one place the four
hand over to each other, and each of them was written on its own.

The columns match the environment dump's, so a descent flown in game can be laid against the
modelled one. It found the wind fault A15 — a buried grid was still in the ground-level wind — and
it is where the unmodelled rock contact (A16) is visible.

## The balance commands

Five commands, none of them a scenario or a benchmark. Each answers one balance question, each
reads the shipped definitions at run time rather than a hand-written catalogue, and each has its
conclusions pinned by a test and argued on a page. **The findings are on those pages** — this table
is what to run and where to read the answer.

| Command | The question | Pinned by | Argued in |
| --- | --- | --- | --- |
| `balance [--csv out/]` | What is every block this mod ships worth against the vanilla blocks it competes with? Five tables: every block costed and measured, what the vanilla heat sources put in, what a panel delivers end to end, which dial moves that number, and the heat pump and coolant rings across their ranges. | `BalanceTests` | [balance.md](../docs/balance.md#block-balance) |
| `coolers` | Which block in the whole game is the best cooling in it, stacked one to thirty-two against the largest reactor at plate rating, in shadow? | `CoolingLadderTests` | [balance.md](../docs/balance.md#the-same-question-asked-of-the-whole-game) |
| `reactors` | What fraction of a reactor's output should become heat, across three orders of magnitude of rating? Every vanilla reactor at six fractions and three loads, in two rigs — **bare** in shadow on a 2.7 K sky, and **skinned** under one cell of light armour. | `ReactorWasteHeatTests` | [balance.md](../docs/balance.md#reactor-waste-heat) |
| `basevariants [--ships N] [--type T]` | What are the blocks a blueprint spells with an empty `SubtypeName` worth? A parse, not a simulation, so a 400-ship stride sample is seconds. It exists because the reader built all thirteen of them as armour until 2026-08-25 — see [backlog.md](../docs/backlog.md) `A13` — and it is how a corpus figure taken before that is priced without re-running a census. `--type` reports one type's share of the waste of the ships that carry it, and `--file` parses one named blueprint and prints the reader's own counters *even when the ship is discarded* — which is the state a ship that resolved to nothing is in, and the only way to see why. | `BlueprintTests` | [backlog.md](../docs/backlog.md) `A13` |
| `triage [--top N] [--census F]` | Which blocks should a balance pass look at, and in what order? No simulation: `BlockHeatIndex` says whether a block can survive itself from its definition alone, a census says how much of a fleet's heat its type carries, and the ranking is the two multiplied. Turns *rig 1,503 blocks* into *rig the twenty this names*. `--levers N` inverts the index instead: what each of the four dials would have to do to bring a block to a self index of N, which is the per-block pass in one screen. `--csv` writes the ranking as a table. | `BlockHeatIndexTests` | [balance.md](../docs/balance.md#one-number-says-whether-a-block-can-survive-itself) |
| `oxygen` | What fraction of an oxygen generator's *draw* should become heat? The same two rigs as `reactors`, pointed at a consumer: six vanilla generators at five fractions and three draws, where the draws are the game's own `StandbyPowerConsumption` and `OperationalPowerConsumption` and one observed field duty. It is what decided 0.6 to 0.40, and it found that **a skin cools a small heat source where it cooks a reactor**. | `OxygenGeneratorWasteHeatTests` | [balance.md](../docs/balance.md#oxygen-generator-waste-heat-written-before-it-is-measured) |
| `retrofit --ships 500 [--csv out/]` | Can cooling be fitted to ships people actually built? A real hull is parsed, run under load to find where its heat is, and the mod's blocks go into the cells that hull left free — bolted and plumbed. | `RetrofitTests` | [balance-lab.md](../docs/balance-lab.md#0-define-good-balance-before-collecting-anything) |
| `stiffness [--csv out/]` | What does a ship's stiffest block demand of a step, asked of 8,098 workshop hulls rather than one save? **Three minutes, and it is what re-baselines `Census.Corpus` whenever a default moves** — the cheapest way to ask a population anything. Nothing is stepped: stiffness is a property of a built grid and the world it is asked about. | `DecorativeStiffnessTests` | [stiffness.md](../docs/stiffness.md#the-same-question-asked-of-eight-thousand-real-ships) |

```bash
dotnet run --project Thermodynamics.Sim -- balance --csv out/
dotnet run --project Thermodynamics.Sim -- coolers
dotnet run --project Thermodynamics.Sim -- reactors
dotnet run --project Thermodynamics.Sim -- oxygen
dotnet run --project Thermodynamics.Sim -- triage --top 25
dotnet run --project Thermodynamics.Sim -- basevariants --ships 400
dotnet run --project Thermodynamics.Sim -- retrofit --ships 500 --csv out/
dotnet run --project Thermodynamics.Sim -- stiffness --csv out/     # ~3 min
```

Two things about how these read the world are the harness's business rather than the finding's.

**`coolers` carries a top-K column**, because a stack goes flat two ways and they read identically
without it: a stack that saturated is hot at its far end, and a stack the heat never reached is
*colder than it was built*, having radiated to the sky instead.

**`stiffness` streams the corpus in batches and counts what it skipped.** A blueprint is held in
memory as every grid and every block in it, so a fifty-gigabyte corpus parsed at once is how an
uncapped run takes a machine down. Blueprints over 64 MB of XML are skipped and **counted**, as is
everything else that does not reach the table (`O5`).

## The block catalog

`blocks` is the authoring side of the definition pass: what every block in the game derives to from
its build components, and which subtypes deviate far enough from their type to be worth naming.

```bash
dotnet run --project Thermodynamics.Sim -- blocks
```

It reads the installed game's own `CubeBlocks/*.sbc` rather than a transcription — there is no
honest way to hand-copy 1,503 definitions — and prints nothing useful without an install. The
derivation it reports is the same one that runs in game, in `BlockThermalDerivation`, so this is a
view of live behaviour rather than of a generator. See
[definitions.md](../docs/definitions.md#where-a-blocks-properties-come-from).

## Thermal corpus appearance review

`thermal-corpus-export <output>` exports per-block temperatures and block bounds from three fixed
corpus ships using existing electrical/recovery scenarios. The offline Python renderer produces
white-hot/Cividis exterior and cutaway comparison sheets, with an identical camera and scale across
estimators. See [the visual panel protocol](../docs/thermal-vision-lab.md#corpus-visual-evaluation-panel)
for reproduction, provenance, renderer checks and the limits of block-bound proxies.

## The blueprint corpus

`corpus` reads real ships out of Space Engineers blueprint files and reports what came back.

```bash
dotnet run --project Thermodynamics.Sim -- corpus                 # subscribed workshop items
dotnet run --project Thermodynamics.Sim -- corpus --path <dir>    # any directory of bp.sbc files
```

Everything this repository measures, it measures on a hull it built itself, and `Census` says so
in its own summary: "one ship is one ship". A corpus replaces that hypothesis with a population.
`corpus-fetch` builds one from the workshop, ranked by subscriptions:

```bash
dotnet run --project Thermodynamics.Sim -- corpus-fetch --key <webapi> --top 10000 --list-only
dotnet run --project Thermodynamics.Sim -- corpus-fetch --key <webapi> --user <account> --top 10000
```

It never handles a password — log the account in once with `steamcmd +login <user> +quit` and
SteamCMD caches it. See [balance-lab.md](../docs/balance-lab.md) for what the lab is for and how it
is staged.

## Screening and the battery

`screen` measures every ship in a corpus without stepping it, and cuts the population to a panel of
specimens chosen to cover the design space rather than to be typical of it. `battery` puts that
panel through every scenario.

```bash
dotnet run --project Thermodynamics.Sim -- screen  --panel 24
dotnet run --project Thermodynamics.Sim -- battery --panel 6
dotnet run --project Thermodynamics.Sim -- battery --panel 6 --linear
```

`hotspot` answers why one block is the hottest thing on a ship, which a matrix cannot:

```bash
dotnet run --project Thermodynamics.Sim -- hotspot --ship Atlas --scenario full-electrical
```

It dumps the blocks at the top of the distribution with the three figures that decide where each
landed — what it generates, what it can radiate through its own faces, and what it can conduct into
its neighbours. See
[balance-lab.md](../docs/balance-lab.md#a-block-with-no-exit-is-the-labs-own-failure-mode) for what
those three separate, and why a block with no exit is the lab's own failure mode rather than a
physics result.

**`--linear` matters.** Balance collection runs concurrently, because a settling problem is a pure
function of a ship and a scenario and nothing about a temperature changes because another core was
busy. Anything whose figure is a *duration* has to run one at a time, because there every other
core is contention rather than speed — and no number carries a note saying which it was. The two
modes produce identical matrices; only the clock differs. See
[balance-lab.md](../docs/balance-lab.md#running-the-lab-parallel-and-linear).

## How far the model is from physics

`profiles`, `sweep` and `features` are the whole-system counterpart to `balance`: not "is this block
worth building" but "how far is this model from physics, and what does each departure cost". They
live in the harness, change nothing that ships, and are the only thing in the repository still called
a profile.

```bash
dotnet run --project Thermodynamics.Sim -- profiles          # one rig, four worlds
dotnet run --project Thermodynamics.Sim -- sweep --csv out/  # every scenario and worst case
dotnet run --project Thermodynamics.Sim -- features          # mechanism switches in combination
```

Realism turns out to be the cheapest configuration and the least responsive; the substep estimate
explains nearly every failure; and both the arcade end of the comparison and the shipped default
diverge on cases a player can build. See [realism.md](../docs/realism.md).

## Load benchmarks

Scenarios answer what the simulation does. The `bench` command answers what it costs, at sizes no
scenario would sit through — up to a million blocks in one grid.

```bash
dotnet run --project Thermodynamics.Sim -- bench scale                 # the ladder
dotnet run --project Thermodynamics.Sim -- bench spike --size 125000   # one block placed, split by stage
dotnet run --project Thermodynamics.Sim -- bench weld  --size 125000   # a block welded every tick
dotnet run --project Thermodynamics.Sim -- bench hitch --size 125000   # per-tick distribution
dotnet run --project Thermodynamics.Sim -- bench load  --size 1000000  # world load, before the first tick
dotnet run --project Thermodynamics.Sim -- bench floor --size 42000    # what a per-block substep cap buys, and costs
dotnet run --project Thermodynamics.Sim -- bench ceiling --size 4000    # what refusing a substep demand costs, in air
dotnet run --project Thermodynamics.Sim -- bench ceiling --fixture rings # the same, where the plumbing sets the demand
dotnet run --project Thermodynamics.Sim -- bench parallel --size 600    # one grid per thread: does a fleet pay for it
dotnet run --project Thermodynamics.Sim -- bench surface                # what a selective surface on the radiator is worth
dotnet run --project Thermodynamics.Sim -- bench stagger --size 600     # whole steps against spread ones: what locality costs
dotnet run --project Thermodynamics.Sim -- bench steppath              # a step at the solver, against a step through the host
dotnet run --project Thermodynamics.Sim -- bench stages --size 500000  # one stage of a grid's life on its own clock, best of fifteen
dotnet run --project Thermodynamics.Sim -- bench stepphases --size 125000  # and where the time inside one step goes
dotnet run --project Thermodynamics.Sim -- bench smallgrids            # what one grid costs before any of its blocks do
dotnet run --project Thermodynamics.Sim -- bench wattsclear            # what zeroing the watts row costs, up a size ladder
dotnet run --project Thermodynamics.Sim -- bench rowfill               # what the first substep of a step pays over a later one
dotnet run --project Thermodynamics.Sim -- bench allowance             # what the element-visit allowance costs, and what it buys
dotnet run --project Thermodynamics.Sim -- bench franken --size 1000000  # a million-block grid welded out of real workshop ships
dotnet run --project Thermodynamics.Sim -- bench floor --frequency 4   # the substep cap swept; the rate is part of the answer
dotnet run --project Thermodynamics.Sim -- bench report --csv benchmarks              # the full report
dotnet run --project Thermodynamics.Sim -- bench report --baseline benchmarks/performance.csv   # and the diff
```

| Benchmark | Question it answers |
| --- | --- |
| `scale` | What does each stage cost at 8k, 32k, 125k, 500k and 1M blocks? |
| `spike` | A block is placed. Which stage stalls, on which tick, and how many things did it touch? |
| `weld` | A block welded every tick for 120 ticks — sustained construction, which never gets a quiet tick to recover in. |
| `hitch` | 300 ticks of a settled grid with one block welded and one ground off. Reports median, p95, p99, max and the spike ratio. |
| `load` | Building the simulation for a grid this size, which a player sees as the loading screen or as a blueprint paste. |
| `report` | Everything at once, as a diffable CSV — including the worst cases a plain hull never reaches: air in the compartments, plumbing, blocks past their rating, three hull shapes in three worlds, and fleets of up to a hundred grids. Every scenario row carries what it actually built, so one that builds nothing cannot report a cost of zero. Also: the ladder, every feature measured both marginally and in isolation, and the substep cap across its range — plus a noise floor so a reader can tell a small cost from no cost. `--baseline <csv>` compares against an earlier run. See [benchmarks.md](../docs/benchmarks.md). |
| `floor --driven` | The same sweep on a ship held at temperature by forty 250 kW sources instead of by a seeded spread — the case that says whether a player would notice, and the one that reports the peak temperature overheat damage is decided by. |
| `coolant` | The segmented fluid model against the well-mixed one it replaced, on the same grid, at rising amounts of pipe. |
| `steppath` | The same step driven straight at the solver and then through the host's entry point, at three substep caps. Everything else here drives the solver, so work the host does around a step is invisible to it — which is how a duplicate stability estimate survived a section written to attribute fixed cost. Both timed runs re-seed the temperature spread; conduction skips a link whose ends agree, so whichever runs second on a flatter grid reads cheaper for no reason but its order. |
| `stepphases` | **Where a step's time goes**: the environment pass, conduction, the coupled bodies, turning watts into temperatures and publishing, each on its own clock inside one settled step, best of fifteen, with the element visits the solver charges each. A step had been reported as one number by three performance passes, two of which changed the link stream's layout on a guess about which part was expensive and reverted it on measurement. `StepPhaseLabTests` holds the instrument to not changing what it measures — the same hull profiled and unprofiled reaches the same temperatures to the bit. See [performance.md](../docs/performance.md#pass-5-iteration-1--a-step-is-measured-by-its-parts). |
| `stages` | Placing blocks, registering them, surfaces, links, rooms, exposure, **the room air rebuild** and a settled step, each timed on its own on one prebuilt grid, fastest of fifteen, with the stage's work counter beside it — the instrument that resolves a change to one stage where the build ladder's summed column cannot. A work figure that moves between repeats aborts the row, because two readings of different walks are not a comparison. `--stages surfaces,rooms` narrows it, `--repeats N` sets the floor on repeats and `--trace` prints every one of them. **A stage repeats until its fastest reading has been reproduced** — five readings within two per cent of it, on a floor of a hundred — because best-of-fifteen had not converged: three runs of the same binary spread 48 %, 28 % and 67 % on the surface, room and link stages ([performance.md](../docs/performance.md#pass-8-iteration-2--every-stage-but-one)). The `roomair` stage fills every room before the clock starts, because a room at zero pressure has no air and no links, and a stage that timed *that* would time an empty outer loop. See [performance.md](../docs/performance.md#how-a-pass-is-run). |
| `smallgrids` | A 200-grid fleet swept from one block a grid upwards, each row run whole, again paced the way the host drives it, and a third time on a zero-length frame to measure the per-grid visit on its own. The report's fleet rows stop at eight hundred blocks a grid because that is the smallest the ship generator builds; a real world is mostly smaller than that, and the per-grid fixed cost is what decides its price. Phases alternate order between repeats — whichever ran second inherited a settled grid and read five times faster than the phase it is a superset of. |
| `franken` | A single grid of a target size welded out of real workshop ships — the corpus's largest, tiled on a lattice until they add up. **No published blueprint is a million blocks** (the largest of 8,132 is 641,711 and p99 is 70,141), so every figure this repository has at the scale bound was taken on census tiers dealt into a shape. This measures it on a real block mixture instead, which is the thing a synthetic ladder cannot invent. Not a population sample and not readable as one: it is the biggest ships there are, repeated. `--ships` points at a survey's `ships.csv`, which is where the paths come from. |
| `allowance` | `MaxElementVisitsPerStep` swept across grid size and world, through the host's frame-paced entry point — the one path where the bound is in force, so it is the only benchmark that can see it at all. Reports what each allowance costs a frame beside the share of real time the grid keeps, and converts the second into kelvin off a ladder of clock errors run through `ClientInputLab`'s own slow-clock mechanism rather than extrapolated from `F23`'s single point. A deficit past the last measured rung is printed with a `>` rather than answered. |
| `floor` | `MaxSubstepsPerBlock` swept: what each cap does to the substep count and the clock, how many blocks it moves, and how far it moves them. Builds from the block census, because the population's *shape* is what decides how far a cap reaches. |

`--shape ship|cube|truss` picks the shape, `--max N` stops the ladder early, `--ticks N` sets the
run length, `--csv <dir>` writes the ladder as a table.

### What segmenting the coolant cost

`bench coolant` runs both fluid models on one grid. Best of five passes per model per row, because
the first version of it reported a four-ring grid as faster than a one-ring grid — a mean was
measuring the machine rather than the work.

```
  rings  pipes  loops   blocks    links   segmented   well-mixed   ratio  substeps
      1     24      1    1,253    2,835      0.2306       0.2307    1.00   23 / 23
      4     96      4    1,349    2,931      0.2532       0.2528    1.00   23 / 23
     16    384     16    1,733    3,315      0.3421       0.3400    1.01   23 / 23
     48   1152     48    2,757    4,339      0.5901       0.5908    1.00   23 / 23
```

**Within one percent, on a grid that is 42 % coolant pipe** — against a field ship measured at 21 %.
The ratio does not climb with the plumbing, which was the thing worth checking: a fixed multiple is a
tuning question, a multiple that grows with what a player builds is a design problem.

It is that cheap because **the link count is identical**. One link per pipe plus one per sink face
exists in both models; what changed is which temperature each link reads. The cost of a coolant loop
was always its links, and there are exactly as many as before.

> **The comparison is only honest because the well-mixed path is genuinely single-mass.** The first
> version of `WellMixedCoolant` reproduced the old *behaviour* by carrying a parcel per pipe and
> levelling them after every substep — so a setting whose only purpose is to be cheaper kept the whole
> cost of the thing it was meant to avoid, and the benchmark was comparing the new model against
> itself. A well-mixed ring is now literally a ring with one parcel holding all of its coolant, so it
> costs one accumulator and one integration however long it is. `TheWellMixedRingIsOneParcelHoldingEverything`
> pins that.

The substep column is the other half of the answer: 23 either way, set by the hull's light blocks, so
loops are nowhere near the stiffest thing on the grid. And they cannot become so by circulating
faster — carrying the fluid is a rotation of which parcel sits in which pipe, which is exact at any
speed. `FlowSpeedDoesNotCostSubsteps` asserts that at 5,000 parcels per second.

`WellMixedCoolant` therefore exists for the choice rather than for the cost.

**The harness runs on .NET 9 and the game runs .NET Framework 4.8**, so absolute milliseconds here
are optimistic against the game. Every project under `tests/` compiles optimised in every
configuration — `tests/Directory.Build.props` sets `<Optimize>true</Optimize>` — because `dotnet run`
and `dotnet test` build Debug, and an unoptimised Debug assembly was what every figure before
2026-08-26 was taken on. See [performance.md](../docs/performance.md#iteration-1--the-harness-measured-unoptimised-code). Ratios and shapes of curve carry across; a millisecond figure
does not. `--diagnostics` turns on the per-node watt figures that switching telemetry on turns on
in a live world, so a benchmark can be compared against a field report that includes the cost of
being measured — about 2 % on a 42,000-block hull.

**The ship is the default shape on purpose.** A solid cube is the best case on nearly every axis
the simulation cares about — see [scale-design.md §10](../docs/scale-design.md#10-grid-shape-changes-the-arithmetic).

Two figures matter and they are not the same figure. The **steady cost** is what a tick costs when
nothing changed; it degrades gracefully, because twice the cost is half the simulation rate. The
**spike cost** is what a tick costs when something did; it degrades catastrophically, because a
player does not perceive a one-second frame as a slow simulation. What each benchmark measured,
and what was changed because of it, is in
[load-and-hitching.md](../docs/load-and-hitching.md).

## Writing a scenario

```csharp
GridBuilder builder = GridBuilder.Large();
builder.Fill(Catalog.LightArmor(), Vector3I.Zero, new Vector3I(3, 3, 3));
builder.Place(Catalog.Reactor(), new Vector3I(3, 0, 0))
       .Producing(15f * ThermalConstants.MegawattsToWatts);

ThermalSimulation simulation = builder.BuildSimulation(new ThermalSettings(), 293.15f);

ScenarioRunner runner = new ScenarioRunner(simulation);
runner.Environment = t => Worlds.PlanetSurface(1f, timeOfDay: 0.5f);
runner.Track("reactor", builder.Last);
runner.Run(seconds: 3600f, sampleIntervalSeconds: 60f);

Console.WriteLine(runner.ToCsv());
```

Coolant rings are laid out by `PipeFitter`, which searches the 24 block orientations for one
whose ports face the right way — building rings by hand means deriving rotations by hand, and
that is how you end up with a scenario that silently tests nothing.

> **It had that failure itself.** A pump carries no sink ports; every rectangle's first straight run
> is index 1; `BuildRing` put the pump there by default, and a sink requested on index 1 was dropped
> without a word. Every ring in this repository asked for exactly that, so **no test or scenario had
> ever exercised a sink face** — they passed on ordinary block-to-block conduction between the hot
> block and the pipe above it, which happens whether a sink exists or not.
> `BuildRing` now moves an automatically chosen pump aside, and an explicitly named pump index that
> collides with a sink is an error. `PipeFitterTests` pins both, and the loop tests now assert the
> link count they expect instead of assuming the sink arrived.

```csharp
var ring = PipeFitter.RectangleXZ(Vector3I.Zero, width: 4, depth: 3);
PipeFitter.BuildRing(builder, ring);      // pump goes on the first straight run
```

## Test coverage

2,303 tests. **What each class is for is stated in its own summary, not here** —
the index below says where to look, and `EveryTestClassSaysWhatItIsFor` fails when a class arrives
without saying. This table is checked by `EveryTestClassIsInTheIndex`, so a suite cannot be added
and left off it.

**The vanilla lane and the modded lane are separate, and keeping them so is what makes both
usable.** Everything measured on the corpus is measured on hulls built out of the *game's* blocks —
the filter rejects a ship with anything it cannot resolve, which is what makes a population figure a
statement about Space Engineers rather than about this mod. Everything about the mod's own blocks —
radiators, pipes, pumps, heat pumps, coolant — is measured in synthetic rigs, because no published
ship carries one. `RetrofitLab` is the deliberate bridge and says so: it fits the mod's blocks into
the cells a real hull left free.

The practical consequence is worth stating, because it is not obvious and it is load-bearing: **a
change to the mod's own blocks cannot move a corpus figure**, so a modded feature can be built while
a vanilla walk is running, and a vanilla walk cannot be invalidated by one. The reverse does not
hold — `A13` was a change to how *vanilla* blocks are read, and it moved the population by 15.76 %.

| Subject | Suites |
| --- | --- |
| **Block geometry and the grid model** | `FaceTests` `FaceSpanTests` `BoxGeometryTests` `GridMathTests` `CellBitsetTests` `BlockOrientationTests` `BlockOrientationCacheTests` `BlockInstanceTests` `BlockInstanceOneCellTests` `GridModelTests` `GridModelAdjacencyTests` `BlockSurfaceBuilderTests` `Se2LatticeTests` `Se2RefineTests` `ShapeTests` |
| **Surfaces, rooms and air** | `SurfaceMapTests` `SurfaceMapPackingTests` `RoomMapperTests` `DoorSealingTests` `RoomPortalTests` `IncrementalRoomTests` `RoomMapFreezeTests` `RoomMapSnapshotTests` `RoomMapSolidTests` `RoomSpanFloodTests` `RoomMapCompletionTests` `RoomMapRecyclingTests` `GridOccupancyTests` `RoomCellStorageTests` `RoomAuditTests` `UnmappedRoomTests` `RoomAirTests` `RoomAirCanonicalTests` `RoomAirCouplingTests` `RoomPressureTests` `RoomAirPressureTests` `ExposureAuditTests` `ExposureFastPathTests` `CoarseRoomFloodTests` |
| **Conduction and the integrator** | `ConductionTests` `StabilityTests` `ConductionClampGateTests` `CoupledConductanceCacheTests` `NodeIndexTests` `FacePackingTests` `SubstepDemandTests` `SubstepFloorTests` `SubstepCeilingTests` `SubstepScaleTests` `HeatTimeScaleTests` |
| **Environment: air, climate, weather** | `EnvironmentSolverTests` `RadiationTests` `ConvectionSolarFrictionTests` `FrictionIsolationTests` `GridFrictionTotalTests` `DragShapeTests` `ShapeNormalGeometryTests` `AerodynamicStripTests` `AeroGeometryTests` `AeroCalibrationTests` `AeroIncidenceTests` `AeroFlightTests` `ShapeNormalOneCellTests` `GridCapacityTests` `SurfaceRebuildScratchTests` `RoomAirPoolTests` `WindShieldingCostTests` `WindShieldingTests` `CentreOfPressureTests` `DragProfileTests` `ClimateModelTests` `GroundRoughnessTests` `DayLengthTests` `WeatherAndDepthTests` `UndergroundContactTests` `PlanetThermalTests` `PlanetReferenceTests` `PlanetPropertyMergeTests` `DescentTests` |
| **Sun, shadow and occlusion** | `SunShadowMapTests` `SolarSelfShadowingTests` `SunLitSliceTests` `SolarOcclusionTests` `SolarOcclusionSamplerTests` `OcclusionLadderTests` `OcclusionMathTests` `SolarSymmetryTests` `GridShadowTests` `TerrainHorizonTests` `SelfShadowScenarioTests` `FaceWeightPairingTests` |
| **Wind** | `WindFieldTests` `WindProfileTests` `GradientHeightTests` `WindSlopeTests` `WindTerrainTests` `WindCompassTests` `StormHeatingTests` `WindScenarioTests` `WindSolverContractTests` `WindLabTests` |
| **Heat sources, damage and thresholds** | `HeatGenerationTests` `OverheatSpillTests` `DamageTests` `CriticalTemperatureTests` `CriticalTemperatureMirrorTests` `OverheatEventTests` `SuitThermalTests` `IncandescenceTests` `HeatWarningTests` `HeatCueScanTests` `ThresholdTests` `HeatSourceTests` `HeatSourceMathTests` `HeatSourceCommandTests` `HeatSourceBlockTests` `CustomHeatSourceTests` `MultiCellAndDamageTests` `ReactorWasteHeatTests` `GridHeatBalanceTests` `HottestNodeTests` `GlowGeometryTests` `HeatGlowStyleTests` `SessionCleanupTests` `ThermalVisionTests` `ThermalVisionTelemetryTests` `ThermalVisionLabTests` `ThermalVisionRegionOrderTests` `ThermalVisionRegionScanTests` `ThermalVisionCelestialTests` `ThermalVisionHudProjectionTests` `ThermalVisionOcclusionTests` |
| **Coolant loops and heat pumps** | `CoolantFillTests` `CoolantLoopTests` `PumpPowerTests` `CoolantFlowTests` `CoolantFaultTests` `HeatLaunderingTests` `PipeFitterTests` `HeatPumpTests` `CoolingScenarioClaimTests` |
| **What a step costs, and what it must not change** | `LoadTests` `StepBudgetTests` `AllowanceTests` `CapVersusAllowanceTests` `StepWorkUnitTests` `PairedRunTests` `FrankenHullTests` `StepFixedCostTests` `StepPrologueTests` `StepPacingTests` `StepTermsTests` `SpreadStepTests` `StaggerTests` `PaceEquivalenceTests` `SweepSliceTests` `BufferGrowthTests` `IncrementalTopologyTests` `BlockRefreshTests` `CostRollupTests` `SolverReportingTests` `StressFindingsTests` |
| **Bit-identity: an optimisation against what it replaced** | `PrecomputedEnvironmentTests` `HeatGainHoistTests` `CanonicalLinkOrderTests` `FixedSourceRowTests` `WattsClearFusionTests` `ConductionClampGateTests` `DiagnosticBatchingTests` `ExposureSkipTests` |
| **Settings, storage and definitions** | `DialReachTests` `SettingsDialReachTests` `LoopDialReachTests` `PlanetDialReachTests` `BlockDialReachTests` `LoopBeforeTests` `LoopCoolantMassTests` `CellSizeTests` `DesignedHullTests` `SettingsTests` `SettingsDefaultsTests` `SettingsWiringTests` `ShippedIdentityTests` `ValidationReportingTests` `StorageCodecTests` `SchedulerTests` `DefinitionTests` `DefinitionFileTests` `ShippedDefinitionTests` `AuthoredMaterialTests` `AuthoredWasteTests` `BlockDerivationTests` `SolarAbsorptivityTests` `SelectiveSurfaceTests` `MaterialOverrideTests` `FeatureToggleTests` `DefaultSettingsTests` `ProfileSuiteTests` `ProfileClockTests` `WorldSettingsTests` |
| **Readouts a player sees** | `TemperatureScaleTests` `UnitsTests` |
| **Telemetry, reports and overlays** | `RunningStatTests` `HistogramTests` `TimingStatTests` `TelemetryFormatTests` `TelemetryAnomalyTests` `SampleGateTests` `GridHealthTests` `AnomalyRegistryTests` `FrameCostTests` `ProfilerTests` `RescanGateTests` `OverlayBudgetTests` `PerformanceReportTests` `BenchmarkBaselineTests` `OptimisedBuildTests` `CensusBoltTests` `StageLabTests` `FirstStepLabTests` `RemapLocalityLabTests` `NodeActivityLabTests` `EnvironmentWalkLabTests` `StepWalksLabTests` `HeatSourceWalkLabTests` `SampleStatisticTests` `StepPhaseLabTests` `LinkSpanProbe` `ProjectFileTests` `EnvironmentReadoutTests` |
| **Field dumps: the mod checked against a world** | `DumpAuditTests` `FieldDumpTests` `CensusFidelityTests` |
| **End to end, and the host boundary** | `SimulationIntegrationTests` `ScenarioTests` `ScenarioClaimTests` `HostAdapterTests` `CoreIsolationTests` `FleetParallelTests` `ParallelTickTests` |
| **Balance, and the ships it is decided on** | `BalanceTests` `CoolingLadderTests` `RetrofitTests` `BlockHeatIndexTests` `HandCoolingTests` `BuildCostTests` `GlowChannelTests` `SuspendedRulesTests` `KnobBaselineTests` `LocalisationSurfaceTests` `CorpusProvenanceTests` `TimeToLossTests` `CatalogDriftTests` `ModHardwareRetestTests` `RetestSetTests` `SettleReadingTests` `SettleExtrapolationTests` `DecorativeStiffnessTests` `ElementCostFitTests` `ScreeningTests` `BlueprintTests` `SubgridBridgeTests` `PrefabWalk` `CorpusCapWalk` `CorpusGuardTests` `CorpusArchiveTests` `CorpusRecordTests` `ClientDriftTests` `ClientInputTests` `HotTailTests` `HotTailSyncTests` `AirCostTests` `ConductionPaceTests` `LoadDialTests` `WorstCaseTests` `LabRunTests` `LabInvariantTests` |
| **Corpus walks** (opt-in, `THERMAL_CORPUS_TESTS`) | `CorpusSurvey` `CorpusAirWalk` `CorpusFloorWalk` `CorpusCensus` `KnobSweep` `ConductanceRetestWalk` `PairSweep` `SunlightPanelWalk` `BlockAccountingWalk` `DeterminismWalk` |
| **The documentation itself** | `DocumentationTests` `ModApiShapeTests` `ThermodynamicsClientTests` `ConfigurationDocTests` `SimCommandTests` `CredentialScanTests` `ScriptWhitelistTests` `ScriptPreprocessorTests` `UncalledCodeTests` `FidelityEndTests` `CorpusConstantTests` `ConsolidationTests` |

> **The bit-identity suites share one fixture.** Five of them pin an optimisation against the
> thing it replaced — the precomputed environment rows, the fixed source row, the gated
> conduction clamp, the batched diagnostics, a step spread across frames — and all five build from
> `Hulls.Driven`: a census hull, its producers running, its temperatures spread across 250-750 K.
> That fixture checks its own postconditions, because two runs of a hull that built nothing agree
> perfectly and agreement is the whole assertion. `SolverAb` holds the capture and the comparison.

> **Where a claim is checked against something other than the model.** `LegacyFormulas` is a
> verbatim copy of the original mod's equations, so a test can show exactly how the rewrite
> differs rather than asserting that it differs. `Reference` works sun visibility out the slow,
> obvious way — intersect the ray with every cell's cube — so the shadow map is compared against
> geometry rather than against itself. `benchmarks/field-dump/` is three CSVs a real world
> produced. A test that asks the model the same question twice agrees with whatever the model
> happens to do.

> **Two divergence defects are pinned as present**, so that fixing either fails a test rather than
> moving a number nobody is watching.

---

## Change log

The depth-layer additions to `ThermalVisionTests` cover ordered plane distances, neutral terminal
background, camera rotation, off-centre projection, aspect ratios and a scalar blend-order
counterexample. These are not native GPU or thermal-coverage acceptance tests.

| Date | Change |
| --- | --- |
| 2026-09-20 | Add teardown fault-injection and session-boundary regression checks. |
| 2026-09-20 | Index the natural-glow fade and allocation regression tests. |
| 2026-09-19 | Separated thermal-vision presentation from the simulation model, with an independent project and assembly isolation checks. |
| 2026-09-18 | Added `thermal-vision-volumes <out>` synthetic temperature-ownership study (exits 2: incomplete) and `ThermalVisionLabTests` regressions for missing hot surfaces, foreground heat leakage and foreign surfaces inside an open frame's thermal cell. |
| 2026-09-18 | Added `thermal-vision-reuse <out>` temporal reuse/footprint experiments and stationary-control/query-bound/occluder counterexample checks in `ThermalVisionLabTests`. Added shared survey scheduling and optics checks: out-of-order/duplicate/foreign/stale completions, reset-safe work caps, complete-image publication, off-centre and rotated-camera rays, unknown hatching, reticle-clear layout and detailed capture work limits. Added `thermal-vision-rays <out>` synthetic sensor study with palette previews, resolution aliasing and query-workload arithmetic; tests check nearest-hit occlusion and estimate/no-return semantics. Indexed `ThermalVisionTests`: palette luminance ordering, cold-scene contrast, invalid samples/windows, configurable-window bounds and viewpoint loss for the opt-in rendering probe. Shared eligibility regressions cover client context, character ownership, death, third-person and camera failure with explicit reactivation. Telemetry tests cover independently bounded important-event history under target churn and bounded scene coverage reporting. Scene autorange tests cover missing-data rejection, cold/hot coverage, smooth expansion/contraction, frame-rate independence, empty views and reset. Geometry tests compare early rejection with transformed world winding, including mirrored/singular fallback; scene telemetry tests cover refresh counts, stage timing samples and overlapping budget reasons and progressive-build reporting. |
| 2026-09-04 | **`RoomMapRecyclingTests` pins `D20`.** The mapper's three-slot rotation, the two-publish read window caught while the recycling pass is in flight, cell-for-cell identity of a recycled map against a fresh one through the shared `RoomMapAssert`, and the warm-pass allocation. The snapshot suite's private map comparison moved into `RoomMapAssert` so the two suites cannot drift apart (`P5`). |
| 2026-08-31 | **The lane rule has a checker, and the drift it found was the direction nobody looks.** `tools/lanes/lanes.py` reads a trx and cross-references the traits. Nothing was over two seconds and untagged — the lane had not rotted — but **thirteen classes were tagged while costing under half a second each**, tagged when they were expensive and left that way after the performance passes made them cheap. The thirteen run together in **1 s for 107 cases**, and untagging them left the fast lane at **4 s** while taking it from 1,768 to **1,875 cases**. Only the slow direction exits non-zero: an over-tagged class costs coverage rather than time, and failing on it would make the checker refuse to pass on a machine that ran quickly. |
| 2026-08-26 | The fast lane had rotted to 37 s inside two days of being re-sorted, and the rule refresh brought it back to 4 s: `DesignedHullTests` was 35 s of it on its own and ten more classes had crossed two seconds untagged — `DialReachTests`, `ModHardwareRetestTests`, `SettingsDialReachTests`, `LoopDialReachTests`, `LoopCoolantMassTests`, `ScenarioTests`, `ScriptWhitelistTests`, `HeatTimeScaleTests`, `CoolantLoopTests` and `DocumentationTests`. The whole suite is 1 m 22 s over 2,001 cases on the optimised build, from 2 m 34 s over 1,884 ([performance.md](../docs/performance.md)). |
| 2026-08-26 | The test tree compiles optimised in every configuration. Nothing set `<Optimize>`, `dotnet run` and `dotnet test` build Debug, and a Debug assembly tells the JIT not to optimise — so every timing the harness ever produced was of code the game never runs, at 3.4× a step and up to 7.7× on the diagnostics surcharge ([performance.md](../docs/performance.md)). |
| 2026-08-26 | Added `ShippedIdentityTests`, which checks the two rules that were *judgement* because nothing could see them break: the workshop id in `modinfo.sbmi` (`R5` — a regenerated file publishes the mod as a new item and every subscriber stays on the old one, with a green build and a correct-looking repository) and the shape of `Models/` (`R4` — a `.mwm` path is baked into the `.sbc` that names it, so a move is a re-export of the source this repository does not hold). The model pin is a digest of the sorted set of paths: a file added is a normal day, a file moved is the failure, and only the set tells them apart. |
| 2026-08-26 | `SettingsWiringTests` covers the bridge between the world's settings and the solver's, which was thirty-nine hand-written assignments nothing read. **A field added to `ThermalSettings` and not to that list is a setting that is documented, wired, named, clamped, replicated and left at its default in every world** — and every test passes, because a test builds a `ThermalSettings` directly and never crosses the bridge, and `SettingsDialReachTests` asks whether the *core* field reaches the solver, which it does. Checked in both directions: nothing missing from the list, and nothing on it the solver no longer has. |
| 2026-08-26 | `DocumentationTests.EveryClaimAPageSaysIsPinnedNamesSomethingThatExists`: every page that says a claim is *pinned by*, *checked by* or *measured by* something names something that exists. rules.md's `*Checked by:*` fields already had that check over one page; every other page cites in prose and nothing read those. Written after blocks.md's ring-length advice was found citing a test that had stopped existing, with three model changes' worth of stale figures above it — `NoPageNamesATestThatHasBeenRenamed` missed it because the rename changed the third camel word. **The dead name is deliberately not written in the new test's own comment**: a citation resolves if the code mentions it twice anywhere, so naming it there made the citation under test resolve, which is how the first attempt to prove the check works passed. |
| 2026-08-26 | Added `BlockDialReachTests`, which completes the set: **every number this mod carries is now enumerated and asserted to reach something** — the coolant's, the world's, the planet's and a block's. Seven of the eleven block properties were already covered as `KnobLab` dials and the other four had a dedicated test class each, which is not the same thing: the point of enumerating is the field written tomorrow. Two needed a rig that did not exist — a *drawing* subject rather than a producing one, because the two waste fractions are read off different terms, and a load that actually cooks the subject past its rating, because at a tenth of it nothing crosses and both the rating and the damage rate read inert. |
| 2026-08-26 | Added `PlanetDialReachTests`: every float on `PlanetThermalProperties` changes what `EnvironmentSolver.Solve` produces, over thirteen places. The planet's thirteen dials are the largest group `C33` counts as unswept and they had no reach check either. All thirteen reach, and **three needed a place that did not exist**: the damping depth is invisible below about twenty metres because it clamps, the ambient lag does nothing at all unless the sample carries where ambient was and how long ago, and the *absolute* lag is used only where the day length is unknown — which is what the game passes for a world whose rotation it could not read. Each of those is a branch nothing was exercising. |
| 2026-08-26 | Added `SettingsDialReachTests`: **every settable field on `ThermalSettings` changes something the solver computes**, enumerated by reflection over thirteen rigs. `DialReachTests` covered the eighteen dials `KnobLab` sweeps and thirteen of those are material dials, so five world settings had a reach check and thirty-five had none — the silent half of [backlog.md](../docs/backlog.md) `C33`. All forty reach; four are named as inert with reasons (a version number and three derived readouts), and the test asserts those really are inert, so an exemption that stops being true fails from the other side. **Building it was mostly building rigs**, and each one is a statement about what a dial needs to be visible: a raking sun on an asymmetric hull for `SolarSelfShadowing`, an environment point source for `EnableHeatSources`, a frame-paced run for `SimulationSpeed`, a two-kelvin lift for `HeatPumpMaxCoefficient`, a suit the regulator loses for `SuitHeatCapacity`, a census hull at two substeps for the overshoot clamps and the same hull under a tight element budget for `FloorBlocksWhenOverBudget`. Caps are swept to a level that binds rather than scaled, because a ceiling above what a rig demands is a ceiling that does nothing. |
| 2026-08-26 | `HeatLaunderingTests` reaches the coolant spill again, and by a route nobody was looking for: **turning `EnableCoolantLoops` off** dissolves every loop with every pipe still on the grid, which is what the spill needs and what a grind stopped being at `B44`. `A12`'s boundedness bound is under test again (`F28`), with conservation and the reclaim-on-switch-back beside it and a pin on the two-port count that makes a split impossible. Two defects the same reading found: `AGrindLeavesNoPipeHoldingCoolantBecauseTheRingDrained` had lost its `[Fact]` and was not running — the suite reported it as an xUnit warning and nothing read it — and `HeldCoolantSurvivesASaveAndLoad` was saving a ring that vents, so it compared eight cold pipes against eight cold pipes. |
| 2026-08-26 | `LoopCandidateTests` is `LoopBeforeTests` and `LoopCandidate` is `LoopBefore`: all three dials the candidate proposed have shipped, so a class still calling itself a candidate would have left every lab with two identical arms reporting *the package is worthless* in the voice it would use if it were. The arms hold the **before** now (`D8`). Added `CellSizeTests`: which cell size is the harder one to cool, per block and from the definitions. `LoopCoolantMassTests` ran its ring with the environment disabled — a source and no sink — so it published a ramp read at step 400 as a temperature; it asserts the rig settles before reading anything off it now, and `CoolantLoopTests` steps to a balance criterion rather than to a fixed count for the same reason. |
| 2026-08-25 | The 8.89 % below is what a 400-ship sample said and the population says **15.76 %** — `BaseVariantLab`'s stride sample carried a representative share of blocks and an unrepresentative share of gravity generators, which are 6.9 GW on their own. The entry below is left as written (`R12`); this is the figure to quote, and [balance-lab.md](../docs/balance-lab.md) carries how the sample missed it. The stiffness walk over the same corrected population moved by **0.00 %**, because heat is a sum over blocks and stiffness is a maximum over them. |
| 2026-08-25 | **The blueprint reader built eleven kinds of vanilla block as armour, and does not now.** The game gives thirteen definitions no `SubtypeId`; `BaseSubtypeOf` turned every one into a plain armour cube — wrong mass, wrong material, no power draw, no heat — and the ship still parsed with the right block count, so nothing looked wrong. It resolves by type now, the model cache and every lookup that resolves a *placed* block key on a unique name, and `ABlockWithNoSubtypeNameIsItsOwnTypesBaseVariantRatherThanArmour` fails if it goes back. **The same pass found the other half of it**: three subtypes are claimed by two types each, and `LargePistonBase` belongs to both `PistonBase` and `ExtendedPistonBase` — same components, same power, sizes 1x2x1 and 1x3x1 — so every extended piston in the corpus was built a cell short. Blocks resolve on the pair now. Added `BaseVariantLab` and `basevariants` to price what it cost: on 400 ships, 17,079 blocks on 275 of them change identity and the sample's full-load waste rises **8.89 %**. Every dataset in `out/` was taken under the defect ([backlog.md](../docs/backlog.md) `A13`). |
| 2026-08-25 | Added `OxygenGeneratorLab` and the `oxygen` command, and moved the two bounds `ReactorLab` had into `SoloBlockRig` so both labs run the same rig on the same clock. It decided the oxygen generator's waste fraction — 0.6 to 0.40 — on the finding that two of six vanilla generators were past critical *bare* at their rated draw, and it falsified the reading that bare is a ceiling and skinned a floor: at three orders of magnitude less waste than a reactor, the shell is the larger radiator and skinning **cools**. Also fixed the two `Vanilla` drift tests, which matched a reference row to a game definition by subtype alone — thirteen of the game's definitions carry no subtype, so the vanilla oxygen generator resolved to a door. |
| 2026-08-25 | Added the `station` scenario, which is the first thing in the library that is not a ship, a rig or a component ([backlog.md](../docs/backlog.md) `F27`). It runs a station against a ship matched to one cell with exactly half the external faces, in vacuum and in air at two loads, and prices what roof radiators are worth. `ScenarioClaimTests` pins both halves of its conclusion, and `TheStationAndTheShipAreMatchedOnBlocksAndHalvedOnArea` checks the pair off the shapes rather than off the scenario, so a change that quietly unmatches them fails there rather than moving every figure `F27` rests on. |
| 2026-08-25 | **A walk launched from a git worktree recorded `unknown` for its commit**, which is what a walk on a machine with no repository records — so the one thing provenance exists to make loud was silent for anyone building on a branch checkout. `.git` is a directory in a clone and a *file* naming one in a worktree; `CorpusRecord.Commit` now follows it, and looks for a loose ref in the common directory a worktree shares with its clone before falling back to `packed-refs`. Found by `AWalkWritesWhatBuildItRanOn`, which was written to catch exactly this and had never had a worktree to catch it on. Its own summary also claimed a `dirty` marker no line of the method produced; the claim is gone and the reason it is not cheap to have is written down instead. |
| 2026-08-25 | Re-measured both lanes on an idle machine, because the figures here had gone stale in the cheap direction: the whole suite is **2 m 34 s** over 1,884 cases where this page said 5 m 23 s, and the fast lane is **4 s** over 1,585 where it said 6 s over 1,554. Fastest of three with the spread quoted (`M4`). The stale figure was not caught by anything, which is the same reason the lane rule rotted: a suite's own cost is a number a test inside it cannot read. |
| 2026-08-24 | The suite size on this page is 1,829 rather than 1,769. `EveryQuotedSuiteSizeIsCurrent` allows a page to fall a tenth behind and it had not, so this is bringing a figure current rather than fixing a break. |
| 2026-08-24 | **The fast lane had stopped being fast, by a factor of fifteen, and the rule that sorts the two lanes is a cost rule now.** Measured: 3 m 45 s against the fifteen seconds this page claimed, because every lab built for `C24`, `C26`, `C27` and `D19` steps whole hulls and none of them carried the trait — `ClientInputTests` alone was 143 s of it, twenty-eight tests each running a 1,004-node hull for the 480 simulated seconds `C26` doubled it to. Nineteen classes tagged; the lane is **6 s over 1,554 cases** and the whole suite is 5 m 23 s. The old rule named a subject — *the scenario batteries* — and a subject rule only sorts what somebody remembered to look at. Nothing checks the new one either, and the section says so and says why. |
| 2026-08-24 | The suite runs eight at a time. The isolation the corpus walks need is theirs now — `[Collection("alone")]`, with `EveryCorpusWalkDeclaresThatItRunsAlone` to keep it — rather than `maxParallelThreads: 1` for every class in the project: 1 m 41 s to 38 s over 1,825 cases. Eight rather than one per core, because thirty-two workers measured no faster than one ([backlog.md](../docs/backlog.md) `F8`). |
| 2026-08-23 | Extended `LoadDialTests` to the charge duration, which is the number `G8` is now scored against: a drive holds 3 MWh, draws 32 MW and keeps 80 % of it, so it fills in 421.9 s. Every figure is read off the game's own definition, because a duration invented here would be the criterion being scored against an assumption. |
| 2026-08-23 | Indexed `LoadDialTests`, which checks that the pair grid's third axis reaches the blocks and reaches nothing else — a sweep dial that reached nothing would report *no change* in exactly the shape of one that reached everything and changed nothing, which is the failure the retest set's `reach.csv` exists for. Conductivity above all: the whole point of that axis is that it is not transport. |
| 2026-08-23 | Indexed `ConductionPaceTests`, which guards a ratio rather than a number: the game has two conduction paces — one for solids, one for the coolant loop's fluid coupling — and nothing made them agree. Moving one alone weakens every loop relative to the structure it competes with, and the only thing that said so when it happened was four balance tests failing for what read like unrelated reasons. |
| 2026-08-23 | Indexed `AirCostTests`, which pins the arithmetic under `C19`: demand is a conductance times the step over a capacity, so it is exactly proportional to the step — which is why every atmospheric figure taken at `Frequency` 8 was half — and lowering the clock divides every stiffness term while raising conductivity restores only conduction. Two rigs, one conduction-limited and one convection-limited, because a hull mixes the two in proportions nobody chose. The population figures it stands beside cannot be pinned here and are not; they are scored by `tools/corpus/air.py`. |
| 2026-08-23 | Indexed `HotTailSyncTests`, which covers the protocol around the packet rather than the packet: which grid a message is about, when a server sends one, when a client asks again, and what a ledger drops when a player leaves. It is here because a session is the one place none of it can be checked — registration and addressing are host code, the policy is not, and two of its cases are failures that are silent by construction (`P2`). |
| 2026-08-23 | Indexed `HotTailTests` and `ClientInputTests`, which cover the two halves of `B4`'s fix: the packet a server sends a client about the blocks that are about to fail, and the claim that a degraded client *input* is a standing bias rather than a perturbation. Both carry their own rig guards as tests — an undegraded client must agree exactly, and a run where nothing on the server ever failed must say so rather than report that the readout agreed (`E8`). |
| 2026-08-23 | Indexed `ScriptWhitelistTests`, which is the one class here that judges the mod against the *game* rather than against the model: it reads `Data/Scripts` for framework types the game's script whitelist refuses, which a green mod build cannot tell you. It is also why Roslyn is a package reference — syntax only, to tell a type from a field of the same name. |
| 2026-08-23 | Indexed the three natural-feedback classes. `IncandescenceTests` is the unusual one: it carries a numerical integration of Planck's law against the CIE observer and re-derives every constant the shipped glow uses, so nothing in it compares the mod to a figure the mod produced (`E7`). |
| 2026-08-22 | Condensed the five balance commands from a section each to one table. Each of them restated a conclusion argued in full on [balance.md](../docs/balance.md), [balance-lab.md](../docs/balance-lab.md) or [stiffness.md](../docs/stiffness.md), which is how the reactor fraction below came to be wrong here and right there. This page is now what to run and where the answer is argued, and keeps only what is the harness's own business rather than the finding's. |
| 2026-08-22 | Corrected the reactor's shipped waste fraction, quoted here and on [thermal-model.md](../docs/thermal-model.md) as 0.02 against the 0.01 in `Cubes.xml` and on [balance.md](../docs/balance.md#reactor-waste-heat). |
| 2026-08-22 | Added the standard header and this change log. |
| 2026-08-22 | Fitted cooling to ships people actually built, closing the retrofit gap the balance criteria depended on. |
| 2026-08-21 | Brought the quoted suite size onto something the suite checks, so a count in prose cannot silently become a historical curiosity. Gated the one corpus test that was running ungated — 4 m 57 s of every run since its fixture landed. |
| 2026-08-20 | Opened the page as the suite's index: every class of tests filed under a subject, checked by `EveryTestClassIsInTheIndex`, with what each suite is *for* living in its own summary where it cannot drift from the code. |

## Offline thermal vision lab

Run `dotnet run --project tests/Thermodynamics.Sim -- thermal-vision /tmp/thermal-vision-lab` to generate a workload report, synthetic Cividis/white-hot depth fixtures and an adaptation trace. Add `--require-complete` to enforce the currently failing full-scene readiness gate. See [the lab guide](../docs/thermal-vision-lab.md) for scope, reproducibility and remaining engine checks. `ThermalVisionLabTests` exercises the same extraction and projection core consumed by the mod, including the large-model deferral regression, 26-view dense-hull face-oracle comparison and batched/unbatched image equivalence.

The Python `ContinuousSurfaceTests` in `tools/thermal-gradient-study/test_continuous_surface.py` validate the offline continuous-temperature appearance prototype: thermal extrema, field isolation, face cancellation, budget reporting, scalar interpolation and depth ownership. Run with `python3 -m unittest discover -s tools/thermal-gradient-study -p 'test_*.py'`.

`SurfaceOptimizationTests` in `tools/thermal-gradient-study/test_optimize_surface.py` checks coplanar mesh reduction against uniform fields, linear gradients, hot/cooled extrema and disconnected geometry. It runs with the same Python discovery command.

`DistanceLodTests` covers projected size, nearest-first scene budgets, coarse cell bounds, minimum coverage and transition costs. `tools/thermal-gradient-study/test_distance_preview.cjs` executes the gallery scheduler against its generated HTML to guard the global limit and transitions during travel.
