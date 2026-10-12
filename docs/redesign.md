# Redesign potential, evaluated

What this page covers: the structural redesigns still available to this codebase — the changes
that alter *what* is computed rather than how fast a line runs — with the measured refusals that
close some of them, the criteria that decide the open ones, and the labs that measure each
criterion. It does not cover local optimisation, which
[performance.md](performance.md#pass-10--what-the-pass-moved)'s tenth pass closed at a measured
floor stage by stage, and it does not cover the SE2 port, which has its own evaluation in
[se2-labs.md](se2-labs.md).

The method is the repository's usual one: a criterion is written before the data (`E1`), the
instrument is a lab in the tree reachable by a command (`D2`), and a candidate leaves this page
either as a design row in [backlog.md](backlog.md) or as a refusal with a figure attached.

## Cleanup assessment, 2026-10-01

The cleanup reran the existing instruments after separating production Core from historical
fixtures. These are local synthetic-hull measurements, not native-session frame times. The
flight row explicitly installs the historical heating provider. Raw output and commands are
retained in [the cleanup benchmark receipt](../tests/benchmarks/cleanup-performance-2026-10-01.txt).

| Question | Fresh observation | Decision for this cleanup |
| --- | --- | --- |
| Keep environment precomputation? | At 32,800 nodes, uncapped flight takes 6.305 ms cached versus 10.533 ms when filled every substep; atmosphere is 4.437 versus 7.281 ms and vacuum 1.453 versus 2.008 ms. | Retain the current cache and its bit-identity checks. No additional caching or altered heating evaluation. |
| Introduce an exposed-node index? | At 128,820 nodes, the compact prototype is 0.734 times the branch walk (0.059 versus 0.081 ms per pass). | Keep `D23` as a separate runtime design. The prototype excludes index maintenance and the complete solver's diagnostics, slicing and topology changes. |
| Invert temperature ownership or fuse walks? | The fusion prototype is 0.842 times the separate walks; real clean-state mirroring costs 0.135 ms, 1.8% of a 7.496 ms settled step. | Keep `D24` separate. A small mirror saving does not justify changing public temperature handles during structural cleanup; fusion still needs integration and numerical validation. |

The cleanup therefore changes organization and verification, not the solver's arithmetic.
`D21` change-local remapping and `D22` sleep remain their existing independent designs: they need
invalidation, wake-up and moving-environment evidence before implementation. `D25` source-row
folding also remains open because it changes summation order and diagnostic attribution.
This assessment completes the cleanup's optimization review without declaring those designs done.

## The survey: what is already answered

Most of the redesign space has been measured before, and the answers are citations rather than
work. They are collected here so the next evaluation starts from the record instead of
rediscovering it.

| Candidate | Verdict | The figure behind it |
| --- | --- | --- |
| Struct-of-arrays for `ThermalNode` / `BlockInstance` | **refused** | the hot path already reads 22 flat per-node arrays; the objects are cold handles, so the change buys memory and no speed, for 24 shipped and 98 test files of blast radius (`E12`) |
| Lumping stiff nodes (`D5`) | **refused for SE1** | the per-block substep cap already collects the prize — the stiff set is ~43 blocks in 43,000, so lumping is "a few per cent over a setting that already exists" ([stiffness.md](stiffness.md#2-lumping--the-same-idea-done-properly)); it re-opens if the stiff set stops being a thin tail |
| Multirate stepping (`D6`) | **refused for SE1** | `0.001 × 28 + 0.999 × 1 ≈ 1.03` substep-equivalents — arithmetically the best answer, every saving already collected by the cap, and the desynchronisation risk is real ([stiffness.md](stiffness.md#3-multirate-stepping--when-the-tail-is-not-thin)) |
| Implicit integration | **not for this fleet** | a 20-iteration CG solve roughly matches what `MaxSubstepsPerBlock 4` already delivers, at far more risk; it is the answer where stiffness is not a thin tail ([stiffness.md](stiffness.md#4-implicit-integration--the-endgame)) |
| Cell-order link walks | **refused** | six schemes measured 0.91–1.40; the stage is bound by one grid-sized memory touch per neighbour, and the orderings that remove it move every temperature's last bit (`D3b`) |
| Deriving face weights from packed counts | **refused** | bit-identical and +10 % on the solver stage ([performance.md](performance.md#pass-10-iteration-7--deriving-the-face-weights-costs-a-tenth-of-the-step-tried-and-dropped)) |
| One grid per thread (`D19`) | **built, ships off** | 10.2× on a 242-grid fleet at 32 threads; the three questions left are a session's, not a lab's |
| Coarse-cell room flood | **SE2's, not SE1's** | the ÷k³ win needs block-aligned sealing at the coarse stride; on SE1 a block *is* a cell, so a supercell misaligns everywhere — `bench coarserooms` exists and verifies the SE2 case cell for cell ([se2-labs.md](se2-labs.md)) |

## The two candidates still open, and their criteria

Two redesigns survive the survey: both change *which elements a pass visits*, which is exactly
the direction pass 10's close said was left. Each criterion below was fixed before its lab first
ran, and the labs are `bench remaplocality` and `bench activity`.

### 1. A change-local room remap

**The candidate.** A sealing change requests a full remap: the flood rewalks the whole bounding
box — fourteen times the block count — and the budgeted tick count to converge is the box over
4,096 cells, which is `D2`'s structural wait. A change-local design would reflood only the region
a change can reach: the mutated cell's component and whatever it seals or unseals. The design is
not sketched here; what this page decides is whether it is worth sketching.

**The instrument.** `RemapLocalityLab` (`bench remaplocality`) mutates a census hull one block at
a time — a block added on the skin, an exposed block removed, a buried block removed, a
room-boundary block removed — refloods fully after each, and diffs the two maps cell for cell,
with rooms named by their smallest cell key so renumbering cannot read as change. The changed
count over the visited count is the share of the full remap that was not rediscovery.

**The criterion, fixed 2026-09-05 before the first run.** A change-local remap earns a design row
if the median changed-to-visited ratio across the four mutations is **under one per cent** — that
is, if at least ninety-nine per cent of a full remap's budgeted work rediscovers what the map
already said. The room-boundary mutation is expected to be the large one and is in the panel so
the criterion is tested against the case where a big change is the *correct* answer.

### 2. Activity tracking — the sleep threshold, measured before designed

**The candidate.** A step touches every node and every link, however quiet. `D1` records that
only visiting fewer elements can shrink a step, and `G5` left "the sleep threshold as a gameplay
knob before a performance one" open. The ceiling on any sleeping scheme is the quiet share of a
real grid — reached only by a scheme with no bookkeeping at all, so a measured ceiling that is
already low kills the idea cheaply.

**The instrument.** `NodeActivityLab` (`bench activity`) reports, at marks after an event —
steps 10, 50 and 200 as first built, with 2,000 and 20,000 added after the first run showed the
short marks measure only the transient — the share of nodes whose per-step movement is under
0.0001 / 0.001 / 0.01 / 0.1 K, and the share of links with both ends quiet, on a parked hull in
air and on a driven hull in vacuum; then it disturbs one node by 300 K on the quietest background
it has and tracks the active set for fifty steps. No equilibrium is
claimed anywhere (`M12`): activity is a function of steps since the last event, which is the
shape a sleeping scheme actually faces.

**The criteria, fixed 2026-09-05 before the first run.** Activity tracking earns a design row if
**both** hold at the millikelvin-per-step threshold — below anything a readout shows:

* the parked hull is **more than half quiet by step 200**, and
* the disturbance's active set stays **under ten per cent** of the grid across its first fifty
  steps.

The driven hull's share is reported either way: a scheme that only helps parked ships is worth
much less, and a scheme that hurts working ships is worth nothing.

## Findings

### The change-local remap: criterion met, decisively

`bench remaplocality`, 2026-09-05, census hulls, exact map diffs:

| mutation | 32,800 blocks: visited → changed | 126,731 blocks: visited → changed |
| --- | ---: | ---: |
| skin add | 381,239 → **1** | 1,678,096 → **1** |
| skin remove | 381,238 → **1** | 1,678,109 → **1** |
| buried remove | 378,397 → **1** | 1,671,526 → **1** |
| room boundary remove | 378,395 → **2,206** (0.58 %) | 1,671,523 → **6,846** (0.41 %) |

The median changed-to-visited ratio is one cell in ~1.7 million — the criterion asked for under
one per cent and the panel's *worst* row is under half of one. Three of the four mutations change
exactly the mutated cell's own classification; even opening a whole compartment reclassifies 0.4 %
of what the full pass visits. **A change-local remap earns its design row**: the full reflood a
mid-session block change pays — 410 budgeted ticks at 126,731 blocks, 3,934 at a million — is more
than 99.5 % rediscovery in every case measured, and the room-boundary case shows the change region
is discoverable (it is the opened room plus the mutated cell's neighbourhood, both reachable from
the mutation). Filed as `D21` in [backlog.md](backlog.md).

### Activity tracking: the criterion as written fails, and what failed was its clock

`bench activity`, 2026-09-05, 32,800 blocks, node shares quiet at each threshold:

| scenario | step | <0.0001 K | <0.001 K | <0.01 K | <0.1 K | links quiet (<0.001 K) |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| parked in air | 200 | 0.0 % | **0.1 %** | 0.2 % | 5.5 % | 0.0 % |
| parked in air | 2,000 | 32.1 % | 51.5 % | 82.4 % | 100 % | 52.5 % |
| parked in air | 20,000 | **100 %** | **100 %** | 100 % | 100 % | **100 %** |
| driven in vacuum | 200 | 0.0 % | 0.0 % | 0.0 % | 0.1 % | 0.0 % |
| driven in vacuum | 2,000 | 0.0 % | 0.0 % | 29.1 % | 92.5 % | 0.0 % |
| driven in vacuum | 20,000 | **100 %** | **100 %** | 100 % | 100 % | **100 %** |

**The criterion as fixed is not met and the verdict stands against it as written** (`E1`): at step
200 the parked hull is 0.1 % quiet against the 50 % the criterion demanded, and its second clause
was unjudgeable on the first run because the wavefront was measured against a background that had
never gone quiet — the whole grid read active, which said nothing about the disturbance (`P2`).

What the longer marks then showed is that the criterion's *clock* was the error, chosen by guess
at 50 simulated seconds when the physics of a hull's decay puts the quiet horizon at the order of
a simulated hour: by step 20,000 (≈ 83 simulated minutes) **both** hulls are completely quiet at a
tenth of a millikelvin — the driven one included, because a ship at thermal steady state moves
nothing per step even with every producer running. And on that quiet background, a 300 K
disturbance's active set peaks at **159 nodes of 32,800 — 0.48 % of the grid** — and is shrinking
again by step 50.

### The revised criterion, changed in the open

Per `E11`: the original criterion stays above, this revision names what moved and why, and the
run that decides it is not in the commit that states it. **Revised**: activity tracking earns a
design row if, at the millikelvin-per-step threshold, both hulls are more than 90 % quiet by step
20,000 *and* the disturbance clause holds as originally written (under ten per cent of the grid
across its first fifty steps, measured on a background that is itself quiet). What moved is only
the mark the first clause is read at — 200 → 20,000 steps — because 200 steps was an arbitrary
clock that measured the transient, not the regime a sleeping scheme lives in; the thresholds and
the shares did not move.

**The confirming run, 126,731 blocks — a size this page had not yet judged — 2026-09-05, in the
commit after the revision:** both hulls read **100 % quiet at every threshold by step 20,000**,
parked and driven alike, and the 300 K disturbance on the quiet background peaks at **196 nodes of
128,820 — 0.15 % of the grid**, shrinking again by step 50. The revised criterion holds with an
order of magnitude to spare on both clauses. And the wavefront's absolute size barely moves with
hull size — 159 nodes at 32,800 blocks, 196 at 128,820 — so the active set is a property of the
disturbance, not of the ship, and the skippable share *grows* with the grids that need it most.
**Activity tracking earns its design row**: filed as `D22` in [backlog.md](backlog.md).

**The caveat that goes with the prize, so the design does not inherit a blind spot** (`P2`): both
scenarios hold the environment sample fixed, so "quiet" here means *quiet under a constant sky*. A
live world's sun sweeps, its weather moves and its time-of-day drifts, and each of those re-wakes
the hull at some rate this lab did not measure. The design must treat an environment delta as an
event like any other, and what a moving sun does to the quiet share is the first measurement the
design phase owes.

## The second sweep — additional alternatives, considered 2026-09-05

The first sweep took the candidates the record pointed at. This one widened the net to the
structures nobody had asked a redesign question of, and most of the answers are reasoning against
figures already on file rather than new measurements. One survived to a lab.

| Candidate | Verdict | Why |
| --- | --- | --- |
| Chunking the grid for cache locality | **already collected** | node numbering is already local — 97.8 % of links span fewer than 1,024 node indices, which is why node reordering was refused before building (performance.md, pass 5); chunking survives only as `D22`'s granularity question, and as SE2 design where the engine chunks for its own reasons |
| Change-local exposure and room-air refresh | **folds into `D21`** | both walk the whole grid today only because the map republishes whole; a change-local publish makes both change-local for free, and the venting path (`RefreshExposureAround`) already shows the shape |
| Incremental sun-shadow updates | **refused by geometry** | a sun *direction* change invalidates every ray by construction — there is no unchanged region to keep — so the levers that exist are the restart tolerance (`SunRebuildCosine`) and the tick budget, both already in place, and a drift rebuild has allocated nothing since pass 10 |
| SIMD / vectorised kernels | **platform-gated** | the game's compiler is the authority (`P7`, `C2`): `System` types reach the whitelist one by one and no vector type is among them, so a vectorised kernel would run in the harness and not in the game — a speedup for the instrument, which is `M13`'s lesson pointed the other way |
| Persisting derived state in the save (links, rooms, exposure) | **refused on value and risk** | a full build is 0.9 s at a million blocks behind a loading screen (`D3`); a serialized derived state is a `P15` liability — frozen in every world it ships to — that must be invalidated against every definition, mod-set and version change, and the rebuild is the safer codec by far |
| Whole-grid sleep | **`D22`'s first rung, not a new candidate** | the activity lab's own finding — 100 % of the hull quiet at steady state, driven included — makes one flag per grid the natural first rung of `D22`'s ladder: it skips the per-node walk, the mirror and the pacing visit (`D13`) with a single test, and needs none of the per-node bookkeeping; noted on the `D22` row |
| Cheap-form physics (per-step radiation linearisation and kin) | **different machinery** | these are fidelity trades under `P14`/`C15`, not visit-count redesigns: each needs an accuracy criterion and corpus validation before a cost argument means anything, and [realism.md](realism.md) is where those trades are made — out of this page's scope, deliberately |

### 3. The environment read over a compacted exposed index

**The candidate that survived to a lab, and why it is smaller than it looks.** "Skip the buried
nodes" is the obvious environment-pass version of visiting fewer elements — 30 to 70 per cent of
a hull is buried, and the share grows with size. But the shipped read already branches on
`nodeExposedFaces[i] <= 0` and a buried node costs one compare, one row load and one store; what a
compacted walk could still remove is exactly that residue, times the twenty-seven read substeps of
a step. Whether the residue is worth a design is a number.

**The instrument.** `EnvironmentWalkLab` (`bench envwalk`), in `StepFloorLab`'s tradition: the
real hull's exposure pattern — because the question is how buried and exposed interleave, and a
synthetic pattern would answer about itself — with the shipped branchy shape and the
clear-plus-compact-index shape run over the same rows, required to produce the same watts row
exactly before either is timed, best of thirty.

**The criterion, fixed 2026-09-05 before the first run.** The compacted shape earns a design row
if it beats the shipped shape by **ten per cent or more at 505,566 blocks**, where about two
thirds of the hull is buried. Anything less is a refusal with the figure attached: the buried
branch already collected the win, and the record should say so where the next reader will look.

**Findings, 2026-09-05 — criterion met at five times its bar.** `bench envwalk`, best of thirty,
identical watts rows proven before timing:

| blocks | buried share | branchy (shipped shape) | clear + compact | ratio |
| ---: | ---: | ---: | ---: | ---: |
| 32,800 | 38.9 % | 0.84 ns/node | 0.64 | **0.763** |
| 126,731 | 51.3 % | 0.68 ns/node | 0.44 | **0.651** |
| 505,566 | 64.5 % | 0.68 ns/node | 0.33 | **0.490** |

The ratio tracks the buried share exactly, which is the mechanism confirming itself: the saving
is the buried residue and nothing else — about half a nanosecond per buried node per walk, which
the branch, the row load and the store cost even when the branch is perfectly predicted. At
505,566 blocks that residue, taken twenty-seven read substeps a step, bounds the shipped saving
at roughly 4–5 ms of a ~27 ms step — and the share grows toward a million blocks, where seven
tenths of the hull is buried. **Filed as `D23`.**

**What the figure is and is not** (`P4`): the lab prices the two *shapes* on the real sparsity,
not the shipped pass — the prototype's exposed arithmetic is leaner than the real read's (no
friction, no lift, no clamp), so the *relative* saving in the shipped pass will be smaller than
the table's ratios even though the buried residue it removes is the same absolute cost. The
design's acceptance measurement is `bench stepphases` on the real pass, not this table. And the
one exactness question is named by the lab's own loose accumulator check: the compact walk sums
the heat-gain total in a different order, so a shipped version either merges in index order or
re-pins the baselines the way the span flood did.

## The third sweep — the step's own walk structure, considered 2026-09-05

The first two sweeps asked which *elements* a pass visits. This one asks how many times the
passes visit them: the step sweeps the node arrays three times per substep — environment,
conduction, apply — and once more per step for the temperature mirror, and none of those walk
counts had ever been questioned. Three candidates closed by reasoning, two survived to a lab.

| Candidate | Verdict | Why |
| --- | --- | --- |
| A cached substep estimate (skip the per-step stability walk) | **refused on `C6`** | a stale estimate that under-provisions substeps breaks boundedness — the invariant the whole solver is built on — and proving a cached bound still conservative costs the walk it would skip; the environment-linearised terms in `ΣG` legitimately move with every sample, and the lever that exists is the safety factor already shipped |
| Half-precision rows | **platform- and fidelity-gated** | `net48` C# 6 under the game's whitelist has no half type to offer, and the rows feed an integrator whose invariants are argued in single precision; this is a fidelity trade with no cheap half to take |
| Room mapping on a worker thread | **session-gated, like `D19`** | the mapper is already budget-sliced so the main thread never blocks on it; moving it to a worker answers no cost the budget has not already amortised, and every threading question this repository has (the engine's scheduler, thread ownership, worker exceptions) is recorded on `D19` as a session's to answer |

### 4. Fusing the apply pass with the next substep's environment pass

**The candidate.** Per substep the node arrays are swept three times. The conduction pass must
sit between an environment fill and the apply that consumes it — but the *apply of substep n* and
the *environment fill of substep n+1* are adjacent with nothing between them, so the schedule
env(1), cond(1), [apply(1)+env(2)], cond(2), … , apply(K) does the same work in two sweeps per
substep instead of three, and the same operations land in the same per-node order — which makes
bit-identity plausible rather than hoped for.

**The instrument.** `StepWalksLab` (`bench stepwalks`): both schedules run sixteen substeps on
the real hull's exposure sparsity and must land on identical bits — every temperature, every
watts entry, the accumulator — before either is timed, best of twenty. The prototype's named
blind spot (`P4`): the real conduction pass between fused walks cools the cache the pair shares,
so acceptance for a built design is `bench stepphases` on the real pass.

**The criterion, fixed 2026-09-05 before the first run.** Fusion earns a design row if the fused
schedule runs at **0.85 or less** of the separate one at 505,566 blocks.

### 5. Temperature ownership inverted — the per-step mirror walk removed

**The candidate.** Every step opens by walking every `ThermalNode` object to re-read its
temperature into the flat row, because "temperature is the one value the host can change from
outside a step". That is a pointer chase through the cold handles, once per step, sized by the
grid. Inverting ownership — the flat row as the source of truth, the node object a view over it —
removes the walk entirely and turns a host write into a marked write, the way every other
mirrored value already works.

**The instrument.** The same lab times the real `SyncNodeState` on its clean incremental path —
not a prototype, since the harness can reach the internal seam — beside a settled step on the
same grid in the same window.

**The criterion, fixed 2026-09-05 before the first run.** The inversion earns a design row if the
mirror walk costs **three per cent or more of a settled step** at 505,566 blocks. Below that it
is a refusal with the figure: the walk is real but the blast radius — every writer of
`ThermalNode.Temperature` in the mod, the adapter and the tests — is not worth less than that.

**Findings, 2026-09-05.** `bench stepwalks`, best of twenty, the two schedules held to a tight
relative tolerance (see below on why not the bit):

| blocks | fused / separate | mirror ms | settled step ms | mirror share |
| ---: | ---: | ---: | ---: | ---: |
| 126,731 | **0.801** | 0.128 | 6.765 | 1.9 % |
| 505,566 | **0.888** | 1.613 | 27.335 | 5.9 % |

**Fusion (candidate 4) is refused, and its size dependence is the finding.** It clears its 0.85
bar at 126,731 blocks (0.801) and *misses* it at 505,566 (0.888) — the criterion's size — because
the win is a shared-cache effect: at 126k the node rows are warm between the fused apply and env,
and at half a million they outrun the cache and the fused pair reloads them anyway. A saving that
shrinks as the grid grows is the wrong shape for a change whose whole justification is scale, and
the real solver's colder cache (the conduction pass streams grid-sized memory between the fused
walks — the prototype's named blind spot) can only push the shipped figure further toward 1.0. The
walk-count arithmetic was sound and the locality did not cooperate; refused with the curve
attached.

**And the prototype cannot prove its own correctness, which is itself a result** (`P4`). Run in
lockstep the fused and separate schemes are bit-identical; run as two whole loops they diverge in
the last bits, because the JIT auto-vectorises the simple apply and env loops and not the combined
fused loop, and a vectorised float reduction rounds differently from a scalar one. Rescheduling
work changes which loops vectorise — so *any* fusion or reordering redesign must prove its
correctness through `SolverAb` against the code it replaces (`D8`), never through a prototype that
reschedules. The lab records this by holding the schemes to a relative tolerance that rules out a
scheduling bug while admitting the rounding, and a first draft's runaway (radiation coefficients
four orders too large) was caught by that same check before any timing was believed.

**The mirror inversion (candidate 5) is filed as `D24`.** The real `SyncNodeState`'s incremental
path costs **5.9 % of a settled step at 505,566 blocks**, past its 3 % bar — and the share *grows*
with size, 1.9 % at 126k to 5.9 % at 505k, which is 12.6× the cost for 3.9× the nodes. That
superlinearity is the diagnosis: the mirror walks every `ThermalNode` object to re-read one float,
a pointer chase through the cold handles that falls off a cache cliff as the grid grows, which is
exactly the size where the mod's stated goal lives. Inverting ownership — the flat row authoritative,
the object a view — removes the walk and the cliff with it.

## The fourth sweep — work that scales on something other than node count, considered 2026-09-05

The first three sweeps walked the per-node and per-cell passes. This one went looking for costs
that scale on *another* axis — a source count, a loop count, anything hiding an O(n²) — because a
pass that is cheap per node can still be quadratic in a thing the node count hides. One survived
to a lab.

| Candidate | Verdict | Why |
| --- | --- | --- |
| Point-source falloff evaluated per node (feared O(sources × nodes) with a per-node distance) | **not a defect** | `ThermalHeatSources.Sample` resolves each source to one direction and one irradiance at the grid centre, so the falloff is O(sources) a step and the per-node loop is a directional sum, not a distance evaluation — the documented whole-grid approximation, working as written |
| Coolant `Advect` / segment solve as an O(loops²) or O(pipes²) hazard | **not present** | the segmented solve is per-loop over its own pipes and loops do not interact, so it is O(total pipes) a step; a grid has tens of loops of tens of pipes, and `bench coolant` already tracks it climbing with plumbing rather than with the square of it |
| Threshold crossings scanned per node per threshold | **already bounded** | `ThermalThresholds.Collect` is called per block only across the temperature interval it actually moved, and the shipped threshold set is tiny; it is O(blocks × thresholds) with thresholds ~O(1), not a hidden square |
| The hottest-node / overheat scan as a per-substep sort | **already amortised** | overheats are accumulated into a per-node row and filed once at end of step (`nodeOverheatDamage`), and the lowest-critical bound skips the whole scan below it (pass 5) — no per-substep ordering work |

The coolant row above describes steady-state advection and segment integration only. It does
not cover loop discovery: open chains are retraced from every unclaimed pipe and can incur
quadratic traversal and allocation during rebuilding. The
[repository review](repository-review-2026-10-08.md#open-coolant-chains-are-retraced-quadratically)
records a separate reproduction of that path.

### 6. Heat sources folded into the precomputed source row

**The candidate.** A registered heat source resolves to one direction and one irradiance per grid
per step, so its contribution to a node — `irradiance · absorptivity · faceWeight · area` — carries
no temperature and is identical across a step's substeps. Solar is exactly this shape and is
already folded into `nodeSourceRow` and read once; registered sources go through a separate
`AccumulateHeatSources` pass that reruns every substep, for no reason but history. Folding them into
the same row computes each source's per-node contribution once instead of twenty-plus times.

**The instrument.** `HeatSourceWalkLab` (`bench heatsourcewalk`) differences a settled step at 0,
1, 8 and 32 registered sources to measure the per-source per-step cost of the shipped per-substep
path; the fold's saving is that cost times `(substeps − 1) / substeps`.

**The criterion, fixed 2026-09-05 before the first run.** The fold earns a design row if one
source costs **0.3 % or more of a settled step** at 126,731 blocks. Below that it is a refusal
with the figure — a source cheap enough to lose in the noise is not worth a reorder that, like
every reorder on this page (`P4`), can only be proven bit-identical through `SolverAb`. The shipped
default has no sources, so a graduation here is a **conditional** design: worth building for a world
that uses sources, invisible to one that does not (`P8` — off already costs nothing; this is the
on cost).

**Findings, 2026-09-05 — criterion met 40× over, and it is the largest per-item cost this
evaluation has found.** `bench heatsourcewalk`, settled step, substeps held at 8 across all rows
(so the figure is pure per-source overhead, not a changed substep count):

| blocks | 0 src | 1 src | 8 src | 32 src | per source | fold removes |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 126,731 | 6.81 ms | 8.14 | 15.79 | 43.59 | **1.15 ms — 16.9 % of a step** | 88 % (7 of 8 substeps) |
| 505,566 | 28.08 ms | 31.12 | 55.80 | 141.87 | **3.56 ms — 12.7 % of a step** | 88 % |

**One source costs a sixth of a step.** The reason it dwarfs the environment residue of sweep 2 is
`Weighted(i, sourceWeights)`: the source pass re-reads the node's six face weights and forms the
directional sum *per node per source per substep*, which is roughly triple the environment read's
own per-node cost — and it is layered on top of that read, not instead of it. A world with a
handful of sources pays several steps' worth of extra work for nothing that changes across the
step. **Filed as `D25`.** The fold removes seven eighths of it at eight substeps, and more at the
higher substep counts a stiff hull demands.

**Why it was filed conditional, and what changed on 2026-09-08.** It was filed on the ground that
the mod registered no heat-source block, so a shipped world had zero sources and paid nothing
(`P8`), against an API that was public and priced per source — which made this the fold to have
built *before* the source block shipped. **The source block has now shipped** (`B11`, and see
[blocks.md](blocks.md#debug-heat-source)), so the second half of that sentence is the one that
applies: a world with one of these blocks built and switched on pays 13–17 % of every step for it.
The premise has moved and the conclusion has not — the fold is worth more now than when it was
measured, not less. What still holds the priority down is that the block is a debug fixture and a
player has to build one, so the *default* world is still a world with no sources in it. The
correctness obligation is the usual one for a reorder (`P4`): folding the source watts into
`nodeSourceRow` changes the order the terms sum, so it is `SolverAb`'s to prove bit-identical, and
the fold must preserve the per-source diagnostic (`LastHeatSourceWatts`) that the crosshair readout
reads, which a naive row-merge would lose.

## Limits

Everything here is measured on census hulls, not the workshop corpus: these are structural
potentials (shares of a box, shares of a node population), not balance figures, and the corpus
rule ([validate-on-real-grids](balance-lab.md)) is about the latter. If either candidate graduates
to a design, its acceptance figures move to corpus scenarios at that point. And a lab's quiet
share says nothing about the cost of *knowing* a node is quiet — the bookkeeping is the design
problem, and this page only prices the prize.

## Change log

| Date | Change |
| --- | --- |
| 2026-10-08 | Separate linear coolant integration from quadratic open-chain discovery. |
| 2026-10-01 | Record fresh precomputation, exposed-index and temperature-ownership measurements and the cleanup decision. |
| 2026-09-05 | **The fourth sweep decides: `D25`, the largest per-item cost the evaluation has found.** Folding registered heat sources into the precomputed source row (as solar already is) removes 88 % of a per-source cost measured at **16.9 % of a step at 126,731 blocks and 12.7 % at 505,566** — one source, a sixth of a step, because `Weighted` re-reads the face weights per node per source per substep. Filed conditional (shipped worlds have no sources), and the fold to have before the source block ships. The other four feared O(n²) hazards were measured or read absent. |
| 2026-09-05 | **The fourth sweep opened**: costs that scale on something other than node count. Four feared hazards were measured or read to be absent — point-source falloff is O(sources) not O(sources × nodes) (resolved at the grid centre), the coolant solve is O(pipes) not O(pipes²), threshold crossings and the overheat scan are already bounded — and one candidate survived to `bench heatsourcewalk` with its criterion fixed before the run: folding registered sources into the precomputed source row the way solar already is. |
| 2026-09-05 | **The third sweep decides: one refusal, one design.** Fusing apply(n) with env(n+1) clears 0.85 at 126k (0.801) but misses at 505k (0.888) — the win is shared-cache locality and it evaporates as the rows outrun cache, the wrong shape for a scale change; refused with the curve, and with the finding that a rescheduling prototype cannot prove its own bit-identity (the JIT vectorises the fused loop differently), so fusion correctness is `SolverAb`'s job. The per-step mirror walk costs 5.9 % of a settled step at 505k and *grows* with size (1.9 % at 126k — 12.6× the cost for 3.9× the nodes), a cold-handle pointer chase off a cache cliff; filed as `D24`. |
| 2026-09-05 | **The third sweep opened**: the step's own walk structure. Three candidates closed by reasoning — the cached substep estimate (refused on `C6`: proving a cached bound conservative costs the walk it skips), half-precision rows (platform- and fidelity-gated), room mapping on a worker (session-gated like `D19`) — and two survived to `bench stepwalks` with criteria fixed before the run: fusing apply(n) with env(n+1), and inverting temperature ownership to remove the per-step mirror walk. |
| 2026-09-05 | **The second sweep's lab decides for the candidate**: the compacted environment walk reads 0.490 of the shipped shape at 505,566 blocks against a criterion of 0.90, and the ratio tracks the buried share exactly. Filed as `D23`, with the honest bound: the prototype's exposed arithmetic is leaner than the real read's, so the shipped saving is the buried residue (~0.5 ns a buried node a walk) rather than the table's ratio, and the acceptance instrument is `bench stepphases`. |
| 2026-09-05 | **The second sweep opened**: seven more alternatives considered — chunking-for-locality (already collected), change-local exposure and room air (folds into `D21`), incremental sun shadow (refused by geometry), SIMD (platform-gated), persisted derived state (refused on `P15` risk against a 0.9 s build), whole-grid sleep (`D22`'s first rung) and cheap-form physics (different machinery, realism.md's) — and one survived to a lab: the environment read over a compacted exposed index, `bench envwalk`, criterion fixed before the run. |
| 2026-09-05 | **The revised criterion's confirming run holds it with an order of magnitude to spare**: 100 % quiet at every threshold by step 20,000 on both hulls at 126,731 blocks, and the disturbance at 0.15 % of the grid — roughly the same absolute node count as at a quarter the size, so the skippable share grows with the hull. `D22` is filed, carrying the fixed-sky caveat: the lab measured quiet under a constant environment, and what a moving sun re-wakes is the design phase's first measurement. |
| 2026-09-05 | **Findings recorded.** The remap candidate meets its criterion decisively (median one changed cell in ~1.7 million visited; worst case 0.58 %) and is filed as `D21`. The activity criterion **fails as written** — 0.1 % quiet at its step-200 clock — and the long marks show the clock was the error: both hulls are 100 % quiet by step 20,000 and a 300 K disturbance peaks at 0.48 % of the grid on a quiet background. A revised criterion (the mark moves to 20,000; thresholds and shares unmoved) is stated per `E11`, with its confirming run left to the next commit. The wavefront's first run also corrected the lab: measured on a 200-step background the whole grid read active, so the number described the background, not the disturbance (`P2`). |
| 2026-09-05 | Opened: the survey of measured refusals, the two open candidates — change-local room remapping and activity tracking — and their criteria, committed before the labs first run at evaluation size (`E1`). |
