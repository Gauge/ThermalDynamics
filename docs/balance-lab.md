# The balance lab

A rig for deciding what good balance *is*, from a population of real ships rather than from a
hull this repository built for itself.

> The rules argued here are stated canonically in [rules.md](rules.md): `E1` `E2` `E7` `E8`
> `M1` `M2` `M3` `M8` `M10` `D1` `O5` `R3` `J3`.

| Looking for | Go to |
| --- | --- |
| What the lab has found | [balance.md](balance.md) |
| The tooling that reads its output | [tools/corpus/README.md](../tools/corpus/README.md) |
| The harness the lab runs on | [tests/README.md](../tests/README.md) |
| The censoring every peak is subject to | [known-issues.md](known-issues.md#deliberate-limits) |

## Why

Every number this mod ships was chosen against a synthetic rig. [`Census`](../tests/Thermodynamics.Harness/Census.cs)
is honest about the cost of that in its own summary — its tiers come from the telemetry dump of one
1,381-block ship, and *"one ship is one ship"*. The reactor retune in
[balance.md](balance.md#reactor-waste-heat) is the same shape of argument one level up: a fraction
measured against two rigs, both of which this repository invented.

That is enough to say *a ship like this overheats*. It is not enough to say *eleven per cent of the
ships people actually build overheat*, and only the second kind of statement can decide a default.

The lab exists to make the second kind of statement.

## The process

The original sketch was: pull ten thousand grids, run them through scenarios, derive guidelines
from the matrix. That is right in outline and has a gap in the middle — step three has no verdict
function. A matrix of temperatures does not say which of them are *good*, and criteria invented
after seeing the data are criteria fitted to the data.

So the refined process puts the criteria first, and stages the runs so the expensive ones are only
paid where they buy something.

### 0. Define good balance, before collecting anything

Eight criteria, each a number the matrix can produce and each able to fail. These are proposals to
argue with — the point is that they are written down before the data is, so a run that fails them
is a finding rather than an excuse to move a threshold.

| # | Criterion | Fails when | Why it matters |
| --- | --- | --- | --- |
| **G1** | **Idle is safe.** A ship at rest in the environment it was built for does not overheat. | more than ~1 % of the corpus goes critical at idle | A mod that kills parked ships is uninstalled, whatever else it does right. |
| **G2** | **Load bites.** Under sustained full power, a meaningful share of uncooled ships reach a warning state. | fewer than ~20 % ever get warm | Heat that never arrives is scenery. This is the criterion the current build most likely fails. |
| **G3** | **Cooling works.** Fitting radiators or a loop moves the outcome. | median Δpeak from a standard cooling fit is small | If the answer does not respond to the one lever the player has, the mechanic is decoration. |
| **G4** | **Design decides, not size.** Outcome correlates with things a builder controls — exposed area per watt, radiator count, how buried the reactor is — more than with block count or grid size. | rank correlation with block count exceeds that with exposure | Otherwise the mod taxes big ships rather than rewarding good ones. |
| **G5** | **No death spiral.** A ship past critical that throttles to idle returns below critical in bounded time. | recovery time unbounded, or damage continues after the load stops | A player must be able to react to a warning. |
| **G6** | **Affordable across the population.** Substep demand and step cost at p95/p99 of the corpus, not at the mean. | p99 substep demand exceeds what the shipped caps grant, **or p99 step work exceeds what the shipped element-visit allowance can do in real time** | The census hull is one point; the tail is what stutters. **Two notes it has earned.** Its marker is a *fidelity* marker, not a cost one — a demand above the cap is the cap doing its job, and the step gets cheaper rather than dearer; what the excess buys is approximation, which the verdict prices beside the failure. And the *step cost* half of its own sentence is now written down, above and below, having never been produced before (`C23`). |
| **G7** | **A ship the game spawns survives arrival.** Every vanilla prefab, idle, in the environment its category spawns into, for five simulated minutes, loses no block. | any prefab loses a block | The stated compatibility floor. A mod that destroys the game's own cargo ships as they arrive is broken however good its physics is, and this is the one criterion measured on ships nobody chose to put in a corpus. |
| **G9** | **Heat matters to most of the fleet.** Under sustained full electrical load, a large majority of uncooled vanilla ships reach a warning state. | fewer than ~70 % ever get warm | `G2` asks whether the mod does anything at all, at 20 %. This asks whether it is *present* for the player who did not go looking — a mod that bites a fifth of ships is a curiosity. **It is a feeling rather than a threshold and the number is a stake in the ground, not a measurement.** |
| **G10** | **Activity is what moves it.** A hull's peak under load and under thrust is far above its peak at rest, and rest is quiet. | the load-to-idle ratio is near 1 on a large share of hulls, or idle is not quiet | Heat that drifts while nothing is happening is weather. Heat that answers what the player just did is a system they can steer, which is the whole of *the player figures it out*. |
| **G11** | **The warning buys thinking time.** Between the first cue and the first block lost there is time to read the situation, decide, and push on if it is worth it. | the median ship gives its pilot less time than it takes to notice, or the first loss is unrecoverable | An alarm that demands an instant reaction trains a reflex rather than an understanding, and it makes heat a twitch mechanic. Pushing through has to stay a choice with a price, not a death. |
| **G8** | **The significant event lands in the window, and the ship is usable again inside a session.** Under sustained full electrical load, the median time from load to the first block crossing critical falls in 120–300 simulated seconds — and from a full burn throttled to idle, the median hull finishes cooling within an hour. | the crossing median falls outside 120–300 s, or the recovery median exceeds 3,600 s | The mod's own stated balance target, which had never been a scored criterion. Both halves are one criterion because one clock governs both: the dial that puts the block in the window pushes the hull out of the session, and a route that satisfies either half alone is not a route. |

**`G6`'s cost half, written down before it is scored** (`E11`, `E1`). The criterion has always
said *substep demand **and step cost***, and only the demand has ever been produced, because the
corpus carries no timing column — deliberately, since a per-ship millisecond figure taken across
thousands of hulls measures the machine. So the cost is stated as **work**, in
the solver's own unit, and compared against a bound the mod already ships.

* **The statistic.** A step's work in element visits, in the unit the allowance is denominated in:
  `substeps × (links + 4 × nodes)`, which is `ThermalSimulation.SubstepCost` and is what a step's
  length is divided by when the bound decides whether to shorten it. None of it is a clock. **The
  walk records that cost per run** — the mod's own figure for the assembly's most expensive grid,
  since the allowance is per grid — rather than the scorer rebuilding it from columns.

  > **It said *derivable from `blocks`, `joints` and a substep column* until 2026-08-24, and it is
  > not.** `joints` is the mechanical joints between grids and is nought on most blueprints, so that
  > derivation dropped the link term entirely. See the correction below.

  > **The weight was 2.125 until 2026-08-24, and that is a real figure about the solver that is the
  > wrong one here.** It is `ThermalSolverStep.SubstepWork` — the buffers cleared, the environment
  > pass, the apply pass and one more walk — which is the unit a step is cut into *frame-sized
  > slices* in. The allowance is spent in the other one. Scoring in the pacing unit against a bound
  > stated in the budget unit compares two currencies 1.45× apart on a census hull, and it halved
  > every figure below. Corrected in place (`P3`), with the old numbers kept visible.
* **The bound.** `MaxElementVisitsPerStep`, which ships at **4,000,000** (2,000,000 until `C27`).
  It is not an invented threshold: it is the mod's own statement of what one grid's step may cost,
  and a step that exceeds it is not refused — it is spread over more frames, so the grid's simulated
  time runs slower than real time. A ship that cannot keep up is the definition of unaffordable. The
  bound follows the default rather than being pinned here, so this criterion scores the
  configuration that ships.
* **The marker.** `G6`'s cost half fails when **p99 step work over the corpus exceeds the
  allowance**, which is the same shape as its demand half and the same percentile.
* **What it does not cover.** One grid at a time. A fleet's cost is the sum over grids and the
  allowance is per grid, so this says a *ship* is affordable rather than that a *session* is —
  which is `D19`'s question and is measured on a fleet rather than on a population.

> **Every step-work figure below is withdrawn: the link count was the joint count.**
>
> **Corrected 2026-08-24.** The unit is `links + 4 × nodes` and the corpus has never carried a link
> column, so `verdict.py` was handed `joints` — which is the count of *mechanical joints between
> grids*, a rotor or a piston, and is nought on almost every blueprint. `links + 4 × nodes` was
> therefore evaluating to `4 × nodes`, and **every step-work figure this repository has published is
> the node half of the unit alone.** On a 2,000-block census hull that is **1.51× low** — 2.06 links
> a block — and on any other hull it is low by that hull's own link-to-node ratio, which is why no
> factor is applied here and the walk records the cost instead (`StepWorkUnitTests`).
>
> **What it does to the verdicts: nothing, and it makes the failure worse.** A hull's ratio is
> between nought and about three links a block, so the true figure is between 1.0× and 1.75× what is
> written below. The air walk's p99 of 5,812,731 is really between 5.8 M and 10.2 M against a
> 4,000,000 allowance — already failing, and failing harder. The vacuum survey's 881,279 is between
> 0.88 M and 1.54 M against the same allowance — still holding. **No verdict on this page moves; every
> number on it does**, and the numbers cannot be recovered from the datasets that produced them
> because the count they need was never recorded (`P2`). They are replaced by a walk carrying the
> `substep_cost` column, and until it lands `verdict.py` reports `G6`'s cost half as *unmeasured*
> rather than reprinting the old arithmetic.
>
> This is the same class of error as the 2.125-against-4 correction below, one level down: that one
> had the right count in the wrong currency, this one has the wrong count. The figures are left as
> written rather than deleted (`E11`, `E10`).

> **The walk predates `C21`'s waste correction by one minute, and it costs the demand half nothing.**
> `f91e5dc` took twenty-seven `ConsumerWasteEnergy` fractions from 0.9 to 1.0 at 21:06 and this walk
> finished at 21:07, so it ran on the old values throughout. Compared row for row against the cap
> walk's control arm, which is this walk repeated on the current definitions
> ([reproduce.py](../tools/corpus/reproduce.py), 1,236 shared runs): **substep demand is identical
> on every one of them, to the last bit**, because demand is conductance over capacity and carries
> no waste term. What moved is generation — **+0.60 % at the median, +4.2 % at p95, +11.0 % at
> worst** — and peaks with it, at **0 % median and 2.1 % worst**. So the demand figures below stand
> as measured; the watt and peak figures are low by those amounts.

**Scored in air, and the cost half fails.** Over the 32,575 runs of the 2026-08-24 air walk — the
whole corpus, four scenarios, at the pair that ships: **demand p50 7.9, p95 30.2, p99 34.8** against
64 granted, so the demand half passes with a wide margin; and **step work p50 45,323, p95 1,652,491,
p99 5,812,731** against the 4,000,000 the allowance grants, which is **1.45× over and a failure**.
**222 ships of 8,144 — 2.73 % — pass the allowance in at least one air scenario**, the smallest at
28,781 blocks and the median at 55,631. Per scenario the work p99 is 8,451,031 in re-entry,
7,321,903 parked in a storm, 4,663,327 at hot noon and 2,551,719 in the dark, so **the criterion
fails in three of the four worlds and holds only in vacuum**. `C27`'s doubling of the allowance was
necessary and not sufficient: at the 2,000,000 that shipped this morning the same p99 is 2.9× over.
What to do about it is a decision and is deliberately not taken in the commit that carries the data
(`E11`).

**And `G1` holds in air**, which the corpus had never been asked: 0.02 % of runs have a block over
critical against 0.22 % in vacuum, because air cools.

**Previously, scored in vacuum, where it holds.** Over the 40,656 runs of the 2026-08-21 survey that carry all three
columns: **p50 10,909 element visits, p95 269,153, p99 881,279** against the 4,000,000 the allowance
grants — the ninety-ninth percentile is a little over a fifth of what a step may cost. **29 runs are
past it, and they are 8 ships**, the smallest at 159,449 blocks and the largest at 628,524. So the
allowance covers a ship of about a hundred and sixty thousand blocks in vacuum, and the ships that
exceed it run their simulated time slower than real time rather than being refused anything.

> At the 2,000,000 that shipped until `C27`, the same runs read **102 past it across 28 ships from
> 74,160 blocks up**. The work figures are unchanged; what moved is the bound, and it moved because
> the simulated time a shortened step gives up was priced for the first time (`E11`).

> Corrected twice on 2026-08-24. The figures moved a third of a per cent when the percentile did:
> `verdict.py` took `values[int(q × n)]` where `air.py` interpolated, and the two are printed side
> by side on these pages — a corpus p99 against a panel p99. On forty thousand samples they agree
> to 0.3 %; on **forty** they do not agree at all, because `int(0.99 × 40)` is 39 and the fortieth
> of forty is the *maximum*. One definition now, the interpolating one, which is what every panel
> figure this repository publishes was computed with (`P5`). Before that, and corrected when the
> unit was: **p50 7,293, p95 174,996, p99 446,707, fifteen runs past it and three ships**. What moved is the weight per node, from 2.125 to the 4 the
> allowance is actually divided by, and the substep column, from granted to demanded — granted is
> already clamped by `MaxSubsteps` and by the allowance itself, so it is the answer rather than the
> question. The verdict is unchanged.

> **The dataset is the survey as it was walked**, at `ConductionScale` 2.4 and `HeatTimeScale` 225
> and in the five **vacuum** scenarios, which is the same restriction that let this criterion's
> *demand* half read as passing for months (`C19`). `C24` multiplies a vacuum demand by about 1.6,
> which would put p99 near 1,410,000 — still inside the allowance. **In air it is level with it**:
> the 40-hull panel's p99 demand is 2.81× its vacuum one at the pair that ships, which projects a
> corpus p99 near **4.0 million**, against the 4,000,000 the allowance grants since `C27` and twice
> the 2,000,000 it granted before. Both are projections of a projection and neither is a
> measurement; what would settle it is walking the corpus in air, which is `F11`, and eight hours
> (`P6`). What the scorer needs is any walk's `blocks`, `joints` and a substep column, so it prices
> whichever run it is handed. *(Corrected 2026-08-24: what it needs is `substep_cost` and a substep
> column. `joints` is not the link count and no walk before that date carries one — see the
> withdrawal above.)*

### What the corpus in air is expected to say, written before it says it

`CorpusAirWalk` is running as this is written. The prediction goes here first, with the numbers
that would falsify it, because a projection that is only compared to the data after the fact is a
projection that will be found to have been right (`E1`, `P3`).

**The two comparisons the walk makes, and why the anchor is in it.** The 2026-08-21 survey was
walked at `ConductionScale` 2.4 and `HeatTimeScale` 225; the air walk runs at the pair that ships.
So the air walk against the old survey moves *two* things at once and settles nothing on its own
(`P6`). What it carries instead is `vacuum-shadow` alongside its three atmospheric scenarios, on the
same ships in the same run:

* **air against vacuum, at the shipped pair** — the walk's own `reentry` against its own
  `vacuum-shadow`. Everything but the world held equal. This is the comparison `F11` exists for.
* **the pair, in vacuum** — the walk's `vacuum-shadow` against the survey's `idle`, which is the
  same simulation under another name (`M8`). Everything but the configuration held equal.

**The prediction.** From the 40-hull panel at the pair that ships, air multiplies a p99 substep
demand by **2.81** — `reentry` 35.12 against `vacuum-shadow` 12.51 — and `C24` multiplies a vacuum
demand by about 1.6. Carried onto the survey's own figures:

| | vacuum, old pair | expected vacuum, shipped pair | expected re-entry, shipped pair |
| --- | ---: | ---: | ---: |
| demand p99 | 3.58 | ~5.7 | ~16 |
| step work p99 | 881,279 | ~1.41 M | **~3.96 M** |

> **Measured 2026-08-24, and the projection is falsified.** Re-entry work p99 is **8,451,031** and
> the whole-dataset p99 **5,812,731**, against a stated falsifier of "below 3 M or above 5 M". **The
> assumption named below as most likely to be wrong was not the one that broke**: the panel's
> air-to-vacuum ratio transferred well, 3.05 measured on the corpus against 2.81 on the panel. What
> did not transfer is the *retune* factor. The corpus's own vacuum demand p99 went 3.58 to **11.4**
> between the two walks, a factor of **3.19**, where these pages projected 1.6 — and the 1.6 was the
> panel's own figure, 12.51 over 8.20. So the panel's **air** ratio describes the population and its
> **retune** ratio does not, by about two: `C24` hits the corpus's tail roughly twice as hard as it
> hits forty typical hulls, which is what a tail made of conduction-limited hulls does when
> conduction is multiplied by four. Every projection below is left as written (`P3`).

**So the cost half is predicted to land within about 30 % of the 4,000,000 allowance, either side.**
That is the whole point of writing it down: the projection cannot distinguish pass from fail, and
whichever it is, it is a finding rather than a confirmation.

* **The demand half is predicted to pass comfortably** — about 16 against 64 granted, a quarter of
  the cap. A p99 anywhere near 64 would mean the panel's air multiplier does not describe the
  population at all.
* **What would falsify the projection**: a re-entry work p99 below 3 M or above 5 M. The panel is
  forty hulls chosen to be typical, not a random sample, so its air multiplier transferring to eight
  thousand is the assumption under every number above and the one most likely to be wrong.
* **If the cost half fails**, `C27`'s doubling was necessary and not sufficient, and the next
  question is the size of the tail — 29 ships or two thousand — which decides whether the allowance
  is a setting a few worlds raise or a default that is still wrong.
* **If it passes**, the projection was pessimistic, and the reason will be worth having: the corpus
  median hull is 1,110 blocks against the panel's selection, and a small hull's demand is set by its
  stiffest fitting rather than by its size.

### What a station is, and what its extra room is worth, written before it is measured

`F27` says a base is intended to be a thermal problem and that nothing in the lab measures one.
Every scenario in the library is a ship, a rig or a component, and the corpus is published
blueprints, which are overwhelmingly ships. The question, the shape, the statistic and the
falsifiers go here before the scenario runs (`E1`, `E11`).

**The intent asserts two things and only one of them is a measurement.** [The document of
intent](document-of-intent.md#a-base-is-a-thermal-problem-too-and-the-place-cooling-can-be-done-properly)
says a station heats, has to be managed, and *in exchange* gets the room to run elaborate coolant
systems and radiator farms far better than a ship can.

* **(a) A station is harder to cool than a ship.** This is geometry and nothing else. A hull sheds
  through exposed faces; a station is mostly interior, so at the same block count it has less skin
  to shed through and must run hotter for the same generation. Measurable with no authored choice in
  it, which is what makes it the half worth pinning.
* **(b) A station can be cooled better, because it can afford more plant.** This is not a
  measurement. *What a station can afford* is a design assertion, and a scenario that assumed a
  number would be measuring the assumption. So the lab does not ask whether the room exists — it
  **prices** it: how much radiator area does the station need to reach the ship's settling
  temperature? That converts the hope into a figure without inventing an affordance.

**The shape.** One block count, one generation figure, two grids, and nothing else different
(`P6`). The ship is `GridShapes.Ship(40, 9, 12)`, the hull the library already uses, at **3,550**
cells. The station is `GridShapes.Station((17, 15, 19), (3, 3, 3))` at **3,549** — one cell apart,
which is luck rather than tuning and is worth having, because it means nothing has to be normalised.
Both are run in space and on a planet surface, because a station's second claim is about radiators
and a radiator on a planet is a different block.

**The pair's geometry is settled before any of it is simulated, and it is exact.** Flood-filled from
outside, the ship presents **3,452** external faces and the station **1,726** — a ratio of **2.00**,
0.97 faces a cell against 0.49. So the subject of the comparison is one number: the station has
exactly half the area to shed through, at the same block count.

**Which sharpens prediction 2 before it is run, and the sharper form is the one to judge.** In
vacuum the shedding is radiative, so halving the area doubles the `T⁴` each remaining face must
carry and the *absolute* settling temperature should rise by `2^0.25 = 1.19×` — not by 2. On a
planet, convection is linear in `ΔT` rather than quartic, so the same halving should roughly
**double** the rise above ambient instead. Two different exponents from one geometric ratio is a
falsifiable claim about which mechanism is carrying the heat, and it is a stronger test than the
factor-of-two band below.

**What is predicted, with the number that falsifies each.**

| # | Prediction | Falsified by |
| --- | --- | --- |
| 1 | The station settles **hotter** than the ship at the same block count and the same watts. | The station settling at or below the ship. |
| 2 | The gap is driven by **exposed faces per block**, not by block count: the ratio of settling rises should track the ratio of exposed area to within a factor of two. **Sharpened before the run** to `2^0.25` on absolute temperature in vacuum and about `2×` on the rise above ambient in air, from the exact 2.00 area ratio above. | A gap that does not move with exposed area; or a vacuum rise that is quartic-shaped in air, or linear-shaped in vacuum, which would mean the mechanism carrying the heat is not the one the geometry says. |
| 3 | Room air **narrows** the gap rather than widening it, because a sealed compartment couples interior blocks to a mass that a bare interior block does not have. | The station running hotter still with room air on than with it off. |
| 4 | The radiator area that brings the station to the ship's temperature is **more than the ship carries** and less than the station's own footprint — a cost, not an impossibility. | Either bound: no area sufficing, or the ship's own area sufficing. |

**And one thing this cannot answer.** Whether a *player's* station is like this station. The corpus
is ships, so there is no published population to check the shape against, and that is `F27`'s own
observation rather than something the scenario fixes. The scenario is a specimen and is reported as
one (`E2`).

### What it said: two of four hold, and the two that fail agree on why

Run 2026-08-25, every arm flat to 0.0000 K over its last two samples.

| # | Prediction | Reading | |
| --- | --- | --- | --- |
| 1 | The station settles hotter | **284.4 K** above ambient against the ship's **236.1 K**, holding **1.205×** the heat | holds |
| 2 vac | `2^0.25 = 1.189×` on absolute temperature | **1.203×** | holds, to 1.2 % |
| 2 air | about `2×` on the rise above ambient | **6.01×** | **fails** |
| 3 | Room air narrows the gap | narrows it, by **0.31 K** in 284 across 48 pressurised compartments | holds, and is negligible |
| 4 | Bolted-on radiator area reaches the ship's figure | the roof saturates at **153** radiators — **43 %** of the station's own block count — at **243.9 K** against 236.1 | **fails** |

**Prediction 2's air half fails for a real reason, not a numerical one.** The first suspicion was
that 6.01× was two near-zero numbers being divided, since at 600 kW both grids settle within a few
kelvin of ambient. Repeated at ten times the load it reads **6.013×** — 34.6 K against 5.8 K, the
same ratio to three figures across a factor of ten. So the area argument is right in vacuum and
wrong in air by a factor of three, and the scenario reports skin mean against interior mean to say
where the difference sits:

| | skin | interior | drop |
| --- | --- | --- | --- |
| station, vacuum | 11.5 °C | 15.9 °C | **+4.4 K** over 2,019 blocks |
| ship, vacuum | −34.0 °C | −35.5 °C | −1.5 K over 855 blocks |
| station, air at 6 MW | 21.9 °C | 61.8 °C | **+39.9 K** over 2,019 blocks |
| ship, air at 6 MW | 16.7 °C | 12.8 °C | −3.9 K over 855 blocks |

**In air, convection pins the skin near ambient and the binding resistance stops being the skin.**
What is left is the conduction path from the middle of the grid out to it, and the station has 2,019
interior blocks against the ship's 855. Its interior sits 39.9 K above its own skin where the ship's
sits *below* its skin. That is the whole of the missing factor of three, and it is the finding this
scenario exists to have produced: **a base's thermal problem on a planet is getting heat out of the
middle, not off the surface.**

**Which is why prediction 4 fails the way it does.** Radiators standing on the roof do work — 284.4,
274.8, 267.7, 263.6, 257.3, 246.7, 243.9 K across 0, 8, 16, 32, 64, 128 and the 153 the roof holds —
monotonically, and never enough. Adding 43 % of the station's block count in radiators does not
reach a ship of the same size, because area was never the binding term.

**The intent is vindicated and its emphasis is not.** [The document of
intent](document-of-intent.md#a-base-is-a-thermal-problem-too-and-the-place-cooling-can-be-done-properly)
pairs *elaborate coolant systems* with *radiator farms*. The measurement says the first is the
load-bearing half: a coolant loop is a transport path that bypasses the conduction bottleneck, and a
radiator farm addresses a bottleneck that is not the binding one in air. A base's advantage is the
room to run **transport**, and the surface area is what the transport delivers to.

**Two rig errors were found by these predictions failing, and both are recorded where they
happened.** The first draft read the *hottest block* and reported the station cooler than the ship,
which is true and measures the wrong thing — the hottest block is a source, and what sets its
temperature is the structure it is bolted to rather than the grid's skin. The second bolted
radiators to a side face; `Catalog.Radiator()` carries mount surfaces on its top and bottom faces
only, so they touched the hull, shaded the skin they covered and conducted nothing, and the ladder
read *hotter with every radiator added*. `ScenarioClaimTests.RadiatorsHelpWhenTheyStandClearAndHurtWhenTheyDoNot`
had already pinned that flush panels hurt; this was the same fact arriving from the other direction.


### What the room sweep costs at station scale, written before it is measured

`A4` says `SweepRoomPressure` is the one whole-grid pass in the mod that is not a rota or a budgeted
slice: two game API calls per compartment, every eight steps, bounded by compartment count rather
than block count. It was measured at **0.054 % of real time on twelve compartments** and the rota
was then deliberately *not* built, on the grounds that it is a station-scale risk with no evidence
behind it — because there was no station. `F27` built one.

**What the lab can settle and what it cannot.** The two calls per room are `GameOxygenAt` and
`IsRoomAtPositionAirtight`, both host API, so their cost is a session question and stays one — it is
`F5`'s shape, not something a harness can fake. What the lab owns is the two things that actually
decide whether the risk is real:

* **How many compartments a station has**, which is the multiplier on everything, and which nothing
  has ever measured because every grid in the library and the corpus is a ship.
* **The core half of the sweep**, `ThermalSimulation.SetRoomPressure` per room, which rebuilds each
  room's links to the blocks around it and is this repository's own code.

**What is predicted, with the number that falsifies each.**

| # | Prediction | Falsified by |
| --- | --- | --- |
| 1 | Compartments scale with **blocks**, not with area or with the square of anything: a station's rooms come from its volume and so do its blocks. The `F27` station is 3,549 blocks in 48 compartments, so about **74 blocks a room**, and the ratio should hold within 25 % across a size ladder. | A ratio that drifts with size — which would mean the count is bounded by something other than volume and the extrapolation below is wrong. |
| 2 | So a **100,000-block station carries on the order of 1,350 compartments**, against the twelve the shipped figure was taken on — two orders of magnitude, and 2,700 game calls every eight steps. | A ladder that extrapolates to materially fewer, which would retire the risk rather than confirm it. |
| 3 | The **core half is linear in rooms and small**: `SetRoomPressure` per room should cost microseconds, so the whole non-API sweep stays well under a per cent of a frame even at that count. | A superlinear curve, or a per-room cost large enough that the core half alone justifies the rota. |
| 4 | A **ship of the same block count carries far fewer rooms**, so this is a station problem specifically rather than a grid-size problem. | Comparable counts, which would mean `A4` was never about stations. |

**And what this cannot decide.** Whether to build the rota. That needs the per-call cost, which is a
session, and the decision is a trade against the sweep's own latency. What it can do is replace *a
station-scale risk with no evidence* with a count.

#### What it said: the count is real, the core half is not the problem

`RoomSweepLab`, run 2026-08-25 on an idle machine.

| shape | blocks | rooms | blocks/room | map ms | core sweep | % real time |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ship 40×9 | 3,550 | **3** | 1,183 | 50.9 | 0.6 µs | 0.0000 |
| station 17×15×19 | 3,549 | **48** | 73.9 | 19.0 | 12.2 µs | 0.0006 |
| station 21×19×23 | 6,477 | 100 | 64.8 | 36.4 | 3.9 µs | 0.0002 |
| station 27×23×27 | 11,907 | 180 | 66.2 | 57.6 | 10.2 µs | 0.0005 |
| station 35×31×35 | 25,879 | 448 | 57.8 | 84.0 | 47.7 µs | 0.0024 |
| station 43×39×43 | 47,811 | **900** | 53.1 | 173.9 | 206.1 µs | 0.0103 |

| # | prediction | reading | |
| --- | --- | --- | --- |
| 1 | blocks a room holds within 25 % across the ladder | 73.9 → **53.1**, a drift of 28 % | **just fails** |
| 2 | about 1,350 compartments on a 100,000-block station | 53 blocks a room and still falling gives about **1,900** | holds, and understates |
| 3 | core half linear in rooms and small | **small** — 0.0103 % of real time at 900 rooms — but **not linear** | half fails |
| 4 | a ship of the same block count carries far fewer | **3 against 48**, a factor of 16 | holds |

**The count is the finding and it is worse than predicted.** A station carries a compartment for
every 53 to 74 blocks where a ship carries one per 1,183. At the same 3,550 blocks that is 48 rooms
against 3, and the shipped 0.054 % was measured on **twelve** — so the multiplier on the two host
calls is one to two orders of magnitude larger than anything that has been measured in a session.

**Prediction 1 fails in a direction that makes prediction 2 worse.** Blocks a room drifts *down* with
size, because a bigger box is proportionally more room and less wall, so the extrapolation is
conservative rather than optimistic.

**Prediction 3's failure is the one to keep.** The core half is not linear: 180 → 900 rooms is 5×
the rooms and 20× the time, because the per-room cost itself grows with the grid — 0.106 µs a room
at 448, 0.229 µs at 900. `SetRoomPressure` rebuilds a room's links, and that gets dearer as the grid
around it does. It is still only 0.0103 % of real time at 900 rooms, so **the core half does not
justify the rota** — but it is not a figure that can be extrapolated to 1,900 rooms by doubling.

**So `A4` keeps its shape and loses its excuse.** The rota is still a judgement about the two host
calls and still needs a session to price them (`F5`). What has changed is that the multiplier is no
longer unknown: it is roughly **a compartment per 60 blocks of station**, and a large base is a
four-figure count rather than a two-figure one.

**Scope.** One synthetic shape, at one compartment size, on one machine (`E2`). There is no
published station population to check it against — the corpus is ships — so this is a specimen and
the blocks-a-room figure belongs to this geometry rather than to bases in general.

### What re-taking the census will move, written before it is re-taken

`A13` fixed the blueprint reader and left every dataset on disk measured through the broken one.
The census is the cheap half — it costs a build rather than a simulation, so the whole corpus is
minutes — and it is what `panel.py`, `provenance.py` and every composition figure read. What it
should say is predicted here first, from the 400-ship parse that priced the defect (`E1`).

**The question.** By how much does the corrected reader move the population's composition, and is
the 400-ship stride sample it was priced on representative of the 8,142?

**Four predictions, each with what would falsify it.**

| | prediction | falsified by |
| --- | --- | --- |
| the cost | full-load waste rises **8 to 10 %** over the 2026-08-21 census, bracketing the sample's 8.89 % | a rise under 6 % or over 13 % |
| the reach | **60 to 75 %** of ships hold at least one corrected block, bracketing the sample's 68.8 % | a share outside that band |
| the population | the ship count is **unchanged**, because no ship in the sample changed side on the vanilla filter | any change in how many ships the census admits |
| the type | `OxygenGenerator` appears with an empty-subtype row for the first time, and it is the **most common** generator in the file | the vanilla generator still absent, or outnumbered by `OxygenGeneratorSmall` |

> **A fifth prediction, added before the run and after the first four** (`E11`). The same pass
> found the other half of `A13` — three subtypes are claimed by two types, and every extended
> piston in the corpus was built 1x2x1 where the game says 1x3x1 — so the re-census carries that
> too. **Prediction: it moves the geometry columns and not the heat.** A piston's components,
> power and thermal entry are identical between the two types, so `waste_full_w` cannot move;
> `exposed_area_m2` and the block-cell counts can. Falsified by any change in a ship's full-load
> waste that is attributable to a piston, and by `exposed_area_m2` not moving at all on the ships
> that carry one, which would mean the size correction never reached the model.

The cost band is wider than the sample's point estimate because a stride sample of 400 is 4.9 % of
the population and the quantity is a sum over blocks, not a per-ship mean — a handful of large hulls
carrying many gravity generators moves it. **If it lands outside the band the sample was not
representative**, and every figure priced on that sample — the ones now standing in `A13`'s row and
on three pages — is a figure about 400 ships and has to say so (`P1`).

**How it is scored.** `censusdiff.py` compares the two censuses per column and per block type and
takes the bands above on the command line, so the verdict is computed rather than read off (`E5`):

```bash
python3 tools/corpus/censusdiff.py out/census-2026-08-21 out/census-2026-08-25 \
    --expect waste_full_w=8:10 --expect ships=0:0
```

It reports the ships only one census holds rather than dropping them, because a comparison over the
ships two datasets share prints a clean number when one of them lost half the population.

**What this does not re-take.** The stepped walks: the survey's outcome rows, the air walk, the cap
walk, the floor walk. Those are hours each and they are what `G1`, `G2`, `G5` and `G6` are scored
on, so **`A13` is not closed by this run** — a corrected census tells the balance criteria nothing
until the walks are re-run against it.


### What it did: A13 is worth twice what the sample said, and two predictions failed on the instrument rather than on the population

`CorpusCensus` re-ran in **2 m 1 s** over 8,137 ships and 120,992 block rows, and was taken twice —
the second run reproduces the first on every column of every ship, which is a determinism check
this got for free by having to be repeated.

**The headline: `A13` is worth 15.76 % of what the population makes, not the 8.89 % the sample
said.** Summed over exactly the rows a base variant writes, the eleven block kinds the reader used
to build as armour carry **14,532,166,957 W** against a population that makes 92,217,649,681 W
without them. The 400-ship stride sample understated it by nearly half, which is the reading its
registration specified in advance: *if it lands outside the band the sample was not representative,
and every figure priced on that sample is a figure about 400 ships and has to say so* (`P1`). Where
the watts are:

| type | watts it adds | ships carrying one |
| --- | ---: | ---: |
| `GravityGenerator` | 6,974,791,556 | 2,973 |
| `OxygenGenerator` | 6,242,800,000 | 5,184 |
| `AirVent` | 1,312,250,000 | 4,636 |
| `OxygenTank` | 2,325,400 | 5,094 |

The other nine — the doors, the passage, the ladder, the turrets and the guns — add mass and
geometry and no heat at all, which is why the sample and the population disagree so much: the
sample's 400 ships carried a representative share of *blocks* and an unrepresentative share of
**gravity generators**, and a gravity generator is 6.9 GW on its own.

**The five predictions, scored.**

| | prediction | outcome |
| --- | --- | --- |
| the cost | full-load waste rises 8–10 % | **fails at 41.14 %, and the instrument is what failed** — see below |
| the reach | 60–75 % of ships hold a corrected block | **not scorable from a census.** `composition.csv` records heat-making blocks only, and nine of the eleven make none. 46.5 % of ships carry a base variant *that makes heat*; the sample's 68.8 % for all kinds is still the only figure for the question as written (`P2`) |
| the population | the ship count is unchanged | **fails, 8,141 → 8,137**, and correctly — see below |
| the type | `OxygenGenerator` appears for the first time and is the most common generator | **holds, emphatically**: **31,214** instances against 5,421 of every other generator combined, six to one |
| the piston | it moves the geometry columns and not the heat | **holds on the half a census can see** — block, grid and joint counts are identical to the row, and the geometry columns moved — but the move cannot be attributed to the piston rather than to the doors and passages beside it, so this is *consistent with* rather than *evidence for* |

**The cost prediction failed because the comparison did not hold everything but the subject equal
(`P6`), and that is my error rather than the population's.** The two censuses are four days apart
and four definition changes apart: the jump drives moved from 0.15 to their derived 0.2 and 0.1 on
2026-08-23, twenty-seven computer and screen fractions moved from 0.9 to 1.0 on the 24th, the
oxygen generator moved from 0.6 to 0.40 today, and `C24` moved the clock. The 41.14 % is the sum of
all of it — the drives alone are +16.0 GW of the +31.1 GW — and **a census-to-census diff cannot
isolate a reader fix.** What can is the composition, which names the rows a base variant writes;
that is the 15.76 % above, and it is the figure to quote.

**The ship count moved by four and both directions are the fix working.** Seven ships left and
three arrived. The seven hold a block the game does not have: a `Door` with an empty subtype on a
**small** grid, a small-grid `GravityGenerator` — the game's empty-subtype door and gravity
generator are large-grid only. Those used to become a small armour cube, so the hull passed the
vanilla filter carrying a block nothing could identify, and was measured as though it were vanilla.
They are now rejected, which is what the filter is for. The three that arrived were being rejected
for a block the pair now resolves. **Net −4 of 8,141, 0.05 %, and it is a `J3` cost worth having.**

**What this changes for `C21`.** The oxygen generator's per-ship share was published from the
sample as a median 10.4 % over 64.5 % of ships. On the population it is a median **14.4 %** over
**64.5 %** of ships — the carrier share is exact and the median was understated by a third. p75 is
43.4 %, p90 **80.8 %**. The decision does not move: it was taken on the rig.

*Population: the 8,137-ship census of 2026-08-25, 120,992 block rows, taken at commit `b255a43`
with the definition hashes recorded beside it — the first dataset in this repository that records
its own build, because the file that does it had never been written (`A13`'s own change log).*

### What the survey's first 60 % says, and what it cannot

The `A13` re-survey is still running. What it holds is **not a sample of the population**: the walk
is largest-first, so its 698 finished hulls are the corpus's 698 *biggest* — 8.6 % of it by count
and **60 % of it by bytes**. Every absolute figure below is a statement about giants (`P1`).

**Scored on that set, two criteria fail.** `G1` holds at 0.43 % critical at idle and `G2` at 92.5 %
warm under load; **`G5` fails at 88.8 % recovered** against the 95 % it asks, and `G6`'s cost half
fails hard — step work p99 **16,243,373** element visits against 4,000,000 granted, on 277 of 3,480
runs.

**Paired against the same hulls in the 2026-08-21 survey, the size bias cancels, and every one of
them moved the right way:**

| on the same 698 hulls | 2026-08-21 | now |
| --- | ---: | ---: |
| idle runs over critical | 8 | **3** |
| full-electrical runs over critical | 566 | **539** |
| full-electrical peak, p50 | 1,607 K | **1,190 K** |
| recovery runs over critical | 135 | **78** |
| `G5` recovered | 80.7 % | **88.8 %** |

So **`G5`'s failure here is not new and not a regression — it is an improvement that has not gone
far enough.** These hulls have always failed it; the current model recovers eight points more of
them than the baseline did. And because `G5` held at ~100 % on the *whole* 2026-08-21 population,
what this really says is that **the population figure was carried by the small hulls all along** and
nobody had looked at the giants separately.

**What it cannot say, and neither can the finished walk.** The comparison is paired but it is not
attributable: four days and four changes separate the two datasets — `C24`'s conduction and clock,
the drive fractions, the twenty-seven computer fractions, and `A13` — so this is *the model as it
stands against the model as it stood*, not what any one of them did (`P6`). Isolating `A13` needs
the same walk on the old reader, which is not a thing that exists.

*Basis: `out/survey-2026-08-25`, assembled across six slices whose `provenance.txt` records six
commits, **two `Cubes.xml` hashes and two `Loops.xml` hashes** — corrected 2026-08-28. This
paragraph said "identical hashes across all four" and rested the dataset's integrity on that; the
fifth slice, on 2026-08-26, carried `C36`, `C42` and `C43` and moved both files, so the claim was
true when written and false when the walk finished (`E10`).*

*It is still one dataset, for the reason the paragraph already gave and now measured rather than
asserted. The only `Cubes.xml` change in the window is the emissivity of `Gauge_SG_Radiator` and
`Gauge_LG_Radiator`, and `Loops.xml` describes coolant: **no ship in the corpus carries a radiator,
a coolant pipe, a pump or a heat pump** — 207 distinct block types across the whole census and not
one of them a block this mod adds. Note that `Ship.IsVanilla` would not have caught one, since the
mod's blocks are definitions too; this is a count, not an argument from what a workshop blueprint
ought to hold.*

*`provenance.py` and `verdict.py` both read only the **last** hash until 2026-08-28, which is why
nobody saw the split: the writer had been appending a block per slice since it was written and its
own summary said so. `spans_several_definitions` is the lookup now, and a summary carries
`<file> versions` and `<file> all` beside the value.*

### What re-taking the corpus in air will move, written before it is re-taken

`CorpusAirWalk` is running again as this is written, into `out/air-2026-08-28`, because the dataset
`F11`'s verdict rests on — `out/air-corpus-2026-08-24` — was collected through the blueprint reader
`A13` fixed and **has no `provenance.txt` at all**, so nothing records what build it saw. The
prediction goes here first, with what would falsify it (`E1`, `P3`).

**What `F11` found, and what is being re-asked.** The demand half passed with room — p99 34.8
against 64 granted. The cost half failed: **step work p99 5,812,731** against the 4,000,000 the
allowance grants, 1.45× over, with **222 ships of 8,144 (2.73 %)** past it in at least one air
scenario.

**The mechanism, and why it points down rather than up.** Step work is `substeps × (links + 4 ×
nodes)` — a *structural* quantity. `A13` changed what eleven kinds of block **are**, which moves
three things and only two of them reach this figure:

* **Heat: up 15.76 % on the population, and irrelevant here.** Step work counts element visits, not
  watts. The census re-take already showed this asymmetry from the other side — the stiffness walk
  came back with every headline percentile identical to the digit, because *a block-identity error
  can be worth a sixth of a population's heat and nothing whatever to its cost*.
* **Mass: up, and it lowers substeps if it moves them at all.** A gravity generator is eight tonnes
  against an armour cube's five hundred kilos, so the corrected blocks are the *least* stiff things
  on the ship. The stiffness walk says this is worth nothing measurable.
* **Links: down, and this is the one that reaches it.** The eleven kinds were built as armour cubes,
  which mount on six faces. Doors, hangar doors, passages and ladders do not. Fewer mount faces is
  fewer conduction links, and links are the term step work is dominated by on a large hull.

**The prediction.** Step work p99 falls, by less than the margin it needs.

| | 2026-08-24, broken reader | predicted, fixed reader | falsified by |
| --- | ---: | ---: | --- |
| step work p99 | 5,812,731 | **4.6 M to 5.8 M** | outside that band |
| `G6` cost half | fails, 1.45× over | **still fails** | holding |
| ships past the allowance | 222 of 8,144 (2.73 %) | **1.5 % to 2.9 %** | outside that band |
| demand p99 | 34.8 of 64 | **within 10 % of 34.8** | moving more than that |

**A fall of more than 21 % falsifies it**, because that is what closing the gap to 4.0 M would take
and the mechanism above does not offer it: the corrected blocks are a minority of any hull and each
loses at most a few of six faces.

**And the reverse falsifies it harder.** If step work *rises*, the reasoning is wrong in a way worth
knowing: it would mean the eleven kinds carry more mount faces than an armour cube, not fewer, and
every link figure this repository has published through the old reader is low rather than high.

*The comparison is `verdict.py out/air-2026-08-28 --baseline tools/corpus/summary-air-2026-08-24.csv`.
Both walks run the same four scenarios on the same corpus at the pair that ships, so the reader is
the only thing that moved — which is the one clean comparison `A13` has offered so far, and the
opposite of the survey's, where 94 commits touched `Data/` between the two datasets and the pair is
a trend rather than a measurement (`M7`).*

### What re-taking the corpus in air actually moved

The walk finished on 2026-08-28: **8,137 ships, 32,548 outcome rows, whole rather than partial**,
303.6 minutes of walking across nine slices, and `tools/corpus/summary-air-2026-08-28.csv` is the
committed source. The prediction above is scored here, and one of its four rows turned out not to be
scoreable at all.

**The demand half did not move, and it did not move to three decimals.**

| | cap walk control arm, broken reader | air 2026-08-28, fixed reader |
| --- | ---: | ---: |
| demand p50 | 7.876 | 7.888 |
| demand p95 | 30.195 | **30.195** |
| demand p99 | 34.779 | **34.779** |
| demand max | 36.614 | **36.614** |

The prediction allowed 10 % of movement and got none. This is the stiffness walk's finding arriving
a second time by a different route: *a block-identity error can be worth a sixth of a population's
heat and nothing whatever to its cost.*

**The cost half's prediction cannot be scored, because the dataset it named cannot supply the
figure.** The row predicted step work p99 falling from **5,812,731** into 4.6–5.8 M. That baseline
was withdrawn on 2026-08-24 for being scored without the link term — and
`out/air-corpus-2026-08-24` **has no `links` column at all**, so it cannot be re-scored with today's
instrument to recover a corrected one. Re-running `verdict.py` over it returns `G6 verdict
unanswered` and no work figure. **The comparison the prediction named was never available**; it was
registered against a number that had already been retracted, and nobody noticed because a
withdrawn figure in prose reads exactly like a live one (`P2`, `E9`).

**The comparison that *is* available is the cap walk's control arm**, and it is a better one. That
arm ran the same four scenarios at the same 64-substep cap with `MaxSubstepsPerBlock` off — which is
this walk — and it carries the link column, so one instrument scores both. It differs from this walk
by the reader `A13` fixed, and by whatever else landed in three days, so it is not a single-variable
comparison and is not offered as one (`M7`).

| | cap 2026-08-25 | air 2026-08-28 | change |
| --- | ---: | ---: | ---: |
| work p99 | 7,293,904 | **7,199,204** | **−1.30 %** |
| runs over the allowance | 733 | **712** | −21 |
| re-entry work p99 | 10,916,631 | 10,544,511 | −3.41 % |
| storm-parked work p99 | 9,547,433 | 9,214,679 | −3.49 % |
| surface-hot-noon work p99 | 6,060,511 | 6,041,049 | −0.32 % |
| vacuum-shadow work p99 | 3,219,372 | 3,225,316 | +0.18 % |
| work max | 122,261,839 | 122,261,809 | −0.00002 % |

**The mechanism was right and the magnitude was not the mechanism's to get wrong.** Step work falls,
it falls hardest in the two windy scenarios, and it falls by **1.3 %** where closing the gap to the
4,000,000 allowance would take 21 %. So the prediction's verdict row — *`G6`'s cost half still
fails* — **holds**, and holds by a wide margin: **7,199,204 against 4,000,000 is 1.80× over**, with
**712 of 32,548 runs** past it. The band 4.6–5.8 M was wrong because it was anchored to a withdrawn
figure in the wrong unit, not because the reasoning about mount faces was wrong.

**The one thing that did move is `G1`, and it moved the way the heat did.** Idle-critical share in
air goes **0.0246 % → 0.0737 %**, three times over, against the same statistic in the old air walk —
a comparison that *is* like-for-like, because it needs no column the old dataset lacks. `A13` is
worth 15.76 % more heat on the population, and that is where it shows up: in what gets hot, not in
what costs. `G1` still holds — the criterion fails above about 1 %.

**What this leaves.** `F11`'s verdict is unchanged and is now resting on a dataset with provenance:
`G6`'s demand half passes with room and its cost half fails at 1.80× the allowance. The lesson worth
keeping is procedural rather than physical — **a prediction registered against a withdrawn figure is
not a registered prediction**, and the check that would have caught it is that a baseline must name
the committed summary it comes from rather than a number in a paragraph (`E5`).

### The runaway target, written before anything is dialled

`C34` found that a ship runs away if and only if it carries a block that cannot cool itself by
about threefold, and named the blocks. This is the target the per-block pass is worked against, and
it goes here before a single fraction moves (`E1`).

**The target: every block with real reach at a self index of 3 or below.** It is derived from the
population rather than chosen — the runaway rate is 0.4 % under 1, **1.4 % from 1 to 3**, and
**19.0 % from 3 to 10**, so 3 is where the cliff is and not a round number somebody liked. *Real
reach* means a type the census says a fleet actually carries; a block nobody builds is triaged out
rather than fixed.

**Why the self index and not the index.** The index assumes every face bolted to armour *held at
ambient* — an infinite sink reached by conduction — and the population says that sink does not
exist: of the corpus ships carrying a jump drive, **909 have more than the armour area the index
says is needed and all 909 still lose a block**, because block-to-block conductance is about
112 W/K and moving megawatts even one block needs thousands of kelvin. So the index's own bound is
unreachable in practice, and **the self index is the honest one**.

**Four levers, and what each has to do.** `SelfIndex = watts / (ε · σ · A · (T⁴ − T_ambient⁴))`, so
to divide it by *k*:

| lever | what it must do | what it costs |
| --- | --- | --- |
| waste fraction | **÷ k** — linear | the drives' fractions are *derived* from the game's own `PowerEfficiency` (`C21`), so moving one is overriding a sourced number with an invention |
| emissivity | **× k** — linear, and capped at 1 | a surface property; the drives sit well below 1, so there is room |
| exposed surface | **× k** — linear | `ExposedSurfaceMultiplier` is already an admitted fudge, so this is cheap and honest |
| critical temperature | **× k^¼** — quartic | derived from build cost, and the mildest of the four: threefold self index is 1.32× the rating |

**The quartic is the finding.** Getting a jump drive from 9.9 to 3 needs its waste cut to a third,
or its emissivity or area tripled — **or its rating raised by 1.32×**, from 689 K to 909 K. The last
is the smallest move by far, and it is the one that says *this block is built to run hot* rather
than *this block makes less heat than the game says it does*.

**What would falsify the target.** If bringing the named blocks under 3 does not move the
population's runaway rate, then the self index is a correlate rather than a cause and the whole
approach is wrong. That is checkable on the retest set without a full walk.

### Where `G9`, `G10` and `G11` stand today

All three are new, all three are feelings written as criteria, and **all three already have a
reading** — from data on disk rather than from a run. Every figure below is the 8,132-ship survey of
2026-08-21 unless it says otherwise, which is **before `C24` moved the pair and before `A13`**
(`P1`), so each is provisional and the direction is given where a paired read is possible.

| | criterion | reading | against |
| --- | --- | --- | --- |
| `G9` | heat matters to most of the fleet | **75.1 %** reach 400 K under load | ~70 % — **holds** |
| `G10` | activity is what moves it | idle **178 K** → sunlit 247 → load **632** → burn **1,087** | ordering right — **holds** |
| `G11` | the warning buys thinking time | **40 s** median from cue to first block lost, **18 s** at p10 | — **the one in doubt** |

**`G9` holds and is not at risk from the retune.** Paired on the 963 hulls both surveys hold, the
warm share moved **90.9 % → 91.5 %** and block loss came *down*, 78.4 % → 74.5 % — so `C24` made
ships cooler without making heat stop mattering, which was the obvious way this could have broken.

**`G10` holds, with a weak tail worth naming.** The load-to-idle ratio on the same hull is **2.84×**
at the median and **1.19× at p10**: a tenth of the fleet barely notices what its pilot is doing. That
tenth is where the criterion is nearest to failing and nothing has looked at what those hulls have in
common.

**`G11` is the one to work on, and it is not obviously met.** 40 seconds from cue to first loss is
time to read a gauge; **18 seconds at p10 is not time to decide anything**. And the crossing itself
arrives at a median of **9.0 s** under full load — the alarm is early relative to the loss, by about
four times, which is the right shape — but the whole event is compressed into under a minute.
Worse, the figures are from `out/corpus-2026-08-23-loss`, 798 ships, taken **before `C24`**, and
nothing has re-measured them since the clock moved.

**So the balance pass has a direction it did not have an hour ago**: heat already matters to three
quarters of the fleet and already answers activity, and what it does not do is give the player time.
The dial that governs the whole timescale is `HeatTimeScale`, which `C24` already moved from 225 to
90 for a different reason — and `G8`'s 2–5 minute significance window is the other criterion pulling
on the same number. **`G11` and `G8` are the same question asked at two scales**, and neither has
been measured at the pair that ships.

### What drag does to a ship's ability to fly, written before it is measured

**`G7` says a ship the game spawns survives arriving. A ship the game spawns must also still be able
to *fly*, and `K1`'s drag is the obvious way to break that** — a coefficient nobody has tuned,
applied to every hull in atmosphere. This is `K5`, and the criterion goes here before the number
does (`E1`), the way `C3`'s did.

**The cruise half is already measured and is not what is registered here.** `K12` solved
`T = ½ C_d ρ A v²` over the census: 6,352 of 8,137 hulls carry thrust and area, and their sea-level
cruise is p5 18.8 m/s, p50 70.4, p95 122.3. That distribution exists, so a prediction about it would
be a prediction about a number already in the tree, which is not a prediction.

**What is not measured is deceleration, which is what *able to fly* actually turns on.** A cruise
speed says where a ship stops accelerating; how badly drag hurts is `a = F/m`, and the census does
not carry mass. Adding it is a column and a two-minute re-run rather than a walk.

**The criterion, and what falsifies it.**

| | prediction | falsified by |
| --- | --- | --- |
| the floor | **no more than 1 %** of thrust-carrying hulls decelerate harder than **1 g** at their own cruise speed in sea-level air | more than 1 % |
| the shape | deceleration at cruise **falls with hull size** — a big ship has more mass per unit of frontal area than a small one | a flat or rising trend against block count |
| the survivable case | the **median** hull decelerates at under **0.2 g**, which is a firm push rather than a wall | a median over 0.2 g |

**Why a gravity is the unit and why 1 g is the line.** A ship that loses speed faster than it falls
is a ship the air is doing more to than the planet is, which is the point at which a player stops
experiencing drag as air and starts experiencing it as a wall. It is also the number a player has an
intuition for, which a coefficient does not.

**The shape prediction is the one that would reveal a modelling error rather than a tuning one.**
Drag goes as frontal area and mass goes as volume, so deceleration should fall roughly as the
reciprocal of a ship's linear size — a big hull should be *less* troubled than a fighter. If the
trend is flat or rising, the exposed-area term is behaving as though hulls were hollow shells all
the way up, and that is a defect rather than a dial.

**What this cannot answer.** Whether a ship is *fun* to fly under it, which no corpus pass reaches,
and whether the game's own respawn ships are in the corpus at all — they are not published
blueprints, so `G7`'s literal subject is outside this measurement and the population stands in for
it.

### What it did: the criterion was a tautology, and the area was wrong

**Both halves of `K5` went wrong and in different ways, which is why this section is longer than the
result.**

**The criterion I registered could not fail on drag, because it was thrust-to-weight in disguise.**
It asked how hard a hull decelerates *at its own cruise speed* — and at cruise, drag equals thrust
by definition, so `a = F/m` is exactly `T/m`. Measured, the two agree to the digit: p50 4.275 g and
p95 10.347 g for both. A criterion that returns the same number whatever the drag model says is not
a measurement of the drag model. **A prediction has to be falsifiable by the thing it is about**, and
this one was falsifiable only by Space Engineers ships having less thrust than they do.

| | prediction | measured | |
| --- | --- | --- | --- |
| the floor | no more than 1 % of hulls over 1 g | **88.93 %** | fails — but of thrust-to-weight, not of drag |
| the survivable case | median under 0.2 g | **4.275 g** | the same tautology |
| the shape | deceleration falls with hull size | 6.18 → 5.51 → 3.54 → **1.72 g** across four size bands | holds, and means big ships have lower thrust-to-weight |

**And chasing why the numbers were so large found a real error in `K12`'s figures.** The census
records a hull's **total exposed area** — every exposed face, whichever way it points — and
`½ C_d ρ A v²` wants the **frontal projection**. `cruise.py` used the total. The two differ by
**Cauchy's formula**: the mean projection of a convex body over all orientations is exactly a
quarter of its surface area, which is also what the solver's own incidence weighting computes.

**So every cruise speed published on 2026-08-30 was low by a factor of two and every drag high by
four**, and the conclusion that most ships are drag-limited was inverted:

| | as published | corrected for the projection | corrected for the coefficient |
| --- | ---: | ---: | ---: |
| median sea-level cruise | 70.4 m/s | 140.9 m/s | **199.2 m/s** |
| inside RTS's 60–110 band | 54.8 % | 19.0 % | **7.6 %** |
| drag-limited below the engine's 100 m/s | 84.5 % | 22.4 % | **11.6 %** |

**The third column is a second correction, found 2026-08-31, and its cause is not the first one's.**
`cruise.py` defaulted `--cd` to 1.0 and the documented invocation passes no `--cd`, while `K5` had
moved `DragCoefficient` to 0.5 the day before — so the middle column describes **twice the drag the
mod applies**. Nothing caught it because nothing could: the constant lived in Python and the setting
in C#, and the two agreed only by somebody remembering. `SHIPPED_DRAG_COEFFICIENT` now pins it and
`TheCruiseToolScoresTheCoefficientTheModShips` fails when the two drift.

**What survives the correction and what does not.** `K12`'s claim that a derived speed lands *in the
same neighbourhood* as an authored one survives in shape and weakens in strength: the median is now
above RTS's band rather than inside it. `K11`'s conclusion — that the retarding force is unnecessary
— **does not survive as stated**: it rested on most ships being drag-limited, and most are not. What
remains true is that the mechanism is unnecessary *for the fifth of ships that are*, and that top
speed becomes altitude-dependent, which no mass curve produces.

**`K5` is still open, and what it needs is a criterion about drag.** The candidate is the one this
exercise stumbled on: **drag alone at a fixed reference speed**, which does not cancel against
thrust. At 100 m/s in sea-level air the corrected median is **2.09 g**, which is a firm push rather
than a wall — but that figure is now in the tree, so a prediction about it would not be one.

### The criterion `K5` actually needs, written before it is measured

**What went wrong the first time is the thing to design against: the quantity has to be one drag can
move.** Thrust cancelled out of the last one. Two candidates survive that test, and both are about
what a *pilot* would feel rather than what a solver computes.

| | prediction | falsified by |
| --- | --- | --- |
| the fraction | on **no more than 5 %** of thrust-carrying hulls does drag at 100 m/s in sea-level air exceed the ship's **own thrust** — that is, the ship cannot hold 100 m/s at full power | more than 5 % |
| the ceiling | the **p99** hull still reaches **60 m/s** at full thrust in sea-level air, which is a working speed rather than a crawl | a p99 ceiling under 60 m/s |

**The first is a ratio and the second is a speed, deliberately.** A ratio says how many ships the air
beats; a speed says how badly it beats the worst of them. Neither cancels: drag at a *fixed* speed
is `½ C_d ρ A v²` with `v` chosen by the criterion rather than by the ship, so a change to `C_d` or
to the exposure model moves both figures and a change to thrust moves only the first.

**Why 100 m/s and why 60.** A hundred is the engine's own large-grid cap — the speed a world without
this mod flies at — so *drag exceeds thrust at 100 m/s* is exactly *this mod took away a speed the
game gave you*. Sixty is two thirds of it, which is the point at which a player would notice they are
slow rather than notice they are stuck.

**And what would falsify the model rather than the tuning**: if the fraction is large *and* the
ceiling is high, drag is too strong on small hulls only, which is the exposure model treating a
fighter as a sail. If both are bad together, the coefficient is simply too big and `DragCoefficient`
is the dial. The pair separates a modelling error from a tuning one, which the single figure could
not.

#### What it did: the shipped coefficient fails, and 0.5 passes

**Scope correction, 2026-09-27:** the historical census supplied nominal `ForceMagnitude`, not
available thrust after the engine's atmospheric-effectiveness curve. Its self-lifting selection
and ceilings below therefore describe the nominal-rating rig. Installed ion thrusters deliver
only 20 % of nominal at relative air density 1; atmospheric and hydrogen definitions follow their
own curves. The recorded percentages and speeds are preserved as that historical calculation,
not evidence that those hulls can supply that thrust in sea-level air. The rebuilt model's P1/P2
walk must use explicitly stated thrust assumptions; [population input validation](aerodynamics.md#population-input-validation)
now retains those curves and subgrid world frames. No threshold or coefficient is changed here.

**The population had to be corrected first, and by reading the criterion rather than the data.**
Scored over every thrust-carrying hull, the worst ceilings are `Moat Tower`, `International Space
Station`, `Asteroid Base`, `Modern House #3` — **stations with one or two thrusters**, thrust-to-
weight of 0.001 to 0.017 g, median 0.325 g against the population's 4.275. A hull that cannot lift
itself is not a ship drag broke; it never flew. `K5`'s own words are *must also still be able to
fly*, so the population is hulls that can lift themselves — **5,649 of 6,352, 88.9 %** — and that is
a reading of the criterion, not a subset chosen because it passes.

| | `C_d` 1 (shipped) | `C_d` 0.5 | registered |
| --- | ---: | ---: | --- |
| drag at 100 m/s beats the ship's own thrust | **14.06 %** | **3.13 %** | no more than 5 % |
| p1 hull's ceiling at full thrust | **55.7 m/s** | **78.8 m/s** | at least 60 m/s |

**So the shipped coefficient fails both halves and 0.5 passes both, and the default moves to 0.5.**
The threshold did not move; the configuration did, which is the direction `E11` permits.

#### And re-scored with the shape term on, 2026-08-31

**The criterion had no scorer until now.** It was computed by hand once, and the two figures it
turned on lived in the paragraph above and nowhere else — one definition, no consumer, which is
`P5`. [`dragfit.py`](../tools/corpus/dragfit.py) is that consumer, and the control it was proven
against is this table: on the same census it reads **14.06 % and 55.7 m/s** at `C_d` 1 and **3.13 %
and 78.8 m/s** at 0.5, to the digit, over the same 5,649 hulls.

| on the 2026-08-31 census, self-lifting hulls | beats thrust | p1 ceiling | verdict |
| --- | ---: | ---: | --- |
| `C_d` 0.5, no shape — **what ships** | 3.13 % | 78.8 m/s | pass |
| `C_d` 0.75, no shape | 7.75 % | 64.4 m/s | fail |
| `C_d` 0.5, shape on | 0.04 % | 137.2 m/s | pass |
| **`C_d` 1.54, shape on** | **3.42 %** | **78.2 m/s** | **pass** |
| `C_d` 2.0, shape on | 5.75 % | 68.6 m/s | fail |

**The shape term is calibration-neutral at `C_d` 1.54, and both statistics agree on it
independently.** The population median shape factor is 0.3250, so the coefficient that restores the
drag is `0.5 / 0.325 ≈ 1.54` — and at 1.54 the criterion reads 3.42 % and 78.2 m/s against the
shipped 3.13 % and 78.8 m/s, while [`summary-cruise-shape-2026-08-31.csv`](../tools/corpus/summary-cruise-shape-2026-08-31.csv)
reproduces the shipped altitude medians exactly. Two criteria that were never fitted to each other
land on the same number.

**What moves is which ships, and that is the whole point.** At matched calibration the two
configurations condemn similar counts — 176 hulls unshaped against 192 shaped — but only **151 are
the same ships**, a 69.6 % overlap. The **25 the shape term rescues have a median shape factor of
0.243**, well below the population's 0.325; the **41 it condemns read 0.438**, well above it. So the
term takes drag off slippery hulls and puts it back on blunt ones at constant population statistics,
which is discrimination rather than a rescale — and it is the direction the physics asks for.

**What this does not settle** is whether 1.54 is the right *default*, only that it is the
calibration-neutral one. A world may want the shape term to make ships faster rather than to
redistribute which ones are fast, and that is a balance question rather than a measurement.

**Half is also where the physics puts it, which is worth more than the fit.** What the coefficient
multiplies is a *Newtonian flat-plate* projection — every exposed face weighted by its incidence,
with no wake and no pressure recovery behind the hull. That over-predicts a real bluff body at the
speeds a ship flies, so the coefficient that matches reality is *below* the one an aerodynamicist
would quote for the shape. Landing at half of it is the expected size of that correction rather than
a number chosen to pass.

### What flooring an over-budget grid does, written before it is measured

`CorpusFloorWalk` is built and **has produced nothing**. The question, the statistic, the decision
rule and the numbers that would falsify each prediction go here first (`E1`, `E11`).

**The question.** `C30` built `FloorBlocksWhenOverBudget` and shipped it off. When a grid cannot
afford the substeps its demand asks for, it floors the stiffest blocks to what the allowance grants
instead of shortening the step. Should that be the default?

**Why 294 ships and not 8,144.** The mechanism engages only where the budget binds, so 7,850
published hulls never reach the branch. The 294 are every ship whose step work exceeds the allowance
in at least one air scenario, read out of the cap walk's control arm; the rule is in
`out/floor-2026-08-25/selection.txt` beside the dataset (`M10`). They run 18,855 to 641,711 blocks,
median 42,623. **A figure from this walk is a figure about the ships the allowance binds on and
about no others**, and every statement of it has to say so (`P1`).

**Four predictions, each with what would falsify it.**

| | prediction | falsified by |
| --- | --- | --- |
| the clock | the floored arm keeps its whole step: **no run loses simulated time** where the control does | any floored run still shortening its step |
| the reach | the floor holds back **fewer** blocks than `C3`'s fixed cap of 6 did, because the grant is larger than 6 | a share at or above the 5.83 % that cap reached in air |
| the cost | Δpeak p99 **under 0.03 K** — the figure this mod already accepts — because the grant is a gentler cap than 6 and 6 cost 0.024 K at rest | a p99 over 0.28 K, which is what the fixed cap cost across the whole population |
| the safety | the floor never makes a grid stiffer, and never floors anything in the control arm | either, once |

The cost prediction is the one with an argument rather than a measurement behind it, and the
argument is that a cap of `budget` approximates less than a cap of 6 on the same hull — on the
64,463-block rig `C30` measured, the budget was 10. **If the population disagrees, the reason will be
that the ships the allowance binds on are not the ships the cap walk's percentiles were drawn from**,
which is the whole reason this is a separate walk rather than an inference.

**The decision rule, fixed now.** The same two calibration points the mod already has: **0.03 K** is
accepted as the price of the substep ceiling's breach and **0.6 K** is what kept a fixed cap out of
the defaults.

* **p99 Δpeak at or under 0.03 K** — ship it on. It costs what the mod already accepts, and what it
  buys is a third to two thirds of a grid's clock back on the hulls that are losing it.
* **p99 Δpeak at or over 0.6 K** — it stays off, and `G6`'s cost half stays open with no lever left.
* **between them** — a judgement, argued in the open, and **not decided by whether it rescues a
  criterion** (`E11`). It does rescue one; that is set aside in advance, as it was for `C3`.

**What this walk cannot say.** It measures the ships the allowance binds on, so it says nothing
about the 96 % it does not touch — which is the point of the mechanism and also the reason its
percentiles must never be quoted as population figures.

### The floor's re-take on a corrected reader: what it should find, written before it runs

**`out/floor-2026-08-25` is the last dataset `A13` has not re-taken.** Its 294 ships were selected
out of `out/cap-2026-08-25` — the control arm of a walk measured through the blueprint reader that
built eleven kinds of block as plain armour — and `C30` was decided on it.

**The selection moved by thirteen ships and that is itself a reading.** Drawn again from
`out/air-2026-08-28`, the corrected full air walk, the rule *every ship whose step work exceeds the
allowance in at least one air scenario* now names **283 ships against 294, with 282 in both**. Twelve
leave and one arrives. `A13` is worth 15.76 % of the population's heat and about a per cent of its
cost, so a selection made on **cost** barely moves — which is the same finding the stiffness walk,
the air walk's demand percentiles and the cap walk's two arms have each already given, arriving a
fourth time. The rule is written into `out/floor-2026-08-29/selection.txt` beside the dataset (`M10`).

| | prediction | falsified by |
| --- | --- | --- |
| the clock | unchanged: no run loses simulated time, 0 cells | any cell whose arms ran different clocks |
| the safety | unchanged: never stiffens, never floors in the control | any occurrence of either |
| the reach | **11–15 %** of blocks, straddling the 13.05 % the broken reader measured | outside that band |
| the cost | Δpeak p99 stays **far over 0.6 K**, so `C30`'s decision does not invert | a p99 under 0.6 K |

**The cost band is deliberately one-sided and the reason is what the row already found.** 27.76 K is
three orders of magnitude past the threshold that would ship the mechanism, so a prediction with a
tight two-sided band would be a prediction about the third significant figure of a number whose
first one settles the question. What is worth registering is the direction: `A13` adds heat and a
Δpeak is a transient, so the cap walk's own Δpeak p99 **doubled** on the corrected reader — 0.2820 K
to 0.5660 K. If the floor's does the same it lands near 55 K and the decision is unmoved. **A p99
under 0.6 K is the only reading that would reopen `C30`**, and nothing in the evidence argues for it.

**One reading is registered because it is nearly free.** This walk's control arm is the same four
`PairLab.AirScenarios` on the same build as `out/air-2026-08-28`, and neither arm floors, so on the
ships they share the two must agree to the last bit. Any disagreement is a defect in one of them and
not a finding about the population (`E7`) — the same check the cap walk's re-take passed on 21,852
rows with a worst relative difference of `0.00e+00`.

**What this walk cannot answer, said before it runs.** It walks the ships the allowance binds on and
no others, so nothing it produces is a population figure and none of it describes the 96 % the
mechanism never reaches (`P1`, `P2`). That was true of the 2026-08-25 walk and is true of this one.

### What the re-take found: all four hold, and the floor's cost more than doubled

**283 ships, 1,132 paired cells** — `out/floor-2026-08-29`, summarised in
[`summary-floor-2026-08-29.csv`](../tools/corpus/summary-floor-2026-08-29.csv), which is the first
committed source this row has ever had. Every prediction registered above holds.

| | prediction | measured | |
| --- | --- | --- | --- |
| the clock | unchanged: 0 cells on different clocks | **0** of 1,132 | **holds** |
| the safety | unchanged: never stiffens, never floors in the control | **0** and **0** | **holds** |
| the reach | 11–15 %, straddling 13.05 % | **13.14 %** | **holds** |
| the cost | far over 0.6 K, so the decision does not invert | **60.20 K** | **holds** |

**Read the two walks with one estimator, which is the only way the difference means anything.**
`floor.py` scores both, so what follows is the same arithmetic over two datasets rather than a
figure from a page against a figure from a tool (`M7`, `E5`):

| statistic | 2026-08-25, broken reader | 2026-08-29, corrected | |
| --- | ---: | ---: | --- |
| reach | 12.93 % | **13.14 %** | +1.6 % |
| Δpeak p50 | 0.0060 K | **0.1600 K** | ×27 |
| Δpeak p95 | 2.1351 K | **13.0832 K** | ×6.1 |
| Δpeak p99 | 27.6656 K | **60.1977 K** | **×2.18** |
| Δpeak max | 48.207 K | **276.343 K** | ×5.7 |
| cells over 0.03 K | 229 of 738 | **485 of 717** | ×2.1 |
| cells over 0.6 K | 62 of 738 | **232 of 717** | ×3.7 |
| decision | switch | **switch** | unchanged |

**The reach did not move and the cost doubled, which is `A13`'s signature and now its fourth
sighting.** The floor engages on a *cost* criterion — a grid that cannot afford its demand — and
block identity is worth about a per cent of the population's cost, so the share of blocks it holds
back moves by 1.6 %. The price is a *temperature* difference, and block identity is worth 15.76 % of
the population's heat, so it more than doubles. The cap walk's own Δpeak p99 doubled over the same
correction — 0.2820 K to 0.5660 K, ×2.0 — which is the same ratio on a mechanism that shares nothing
with this one but the population it is measured on.

**`FloorBlocksWhenOverBudget` stays off, and the reason it closed is untouched.** The rule refuses at
0.6 K and the reading is a hundred times that. What the re-take changes is the margin: the tail is
worse than it was thought to be in every quantile, and a third of the engaged cells now exceed the
figure the mod refuses rather than a twelfth.

**One figure on this page was 0.3 % out and is corrected.** The 2026-08-25 walk's p99 was scored by
hand as **27.76 K**; the same dataset through `floor.py` is **27.6656 K**. Nothing decided on it
moves — it is a hundredth of the way to a threshold two orders of magnitude away — and it is exactly
what `E5` predicts of a figure with no source in the tree: not wrong enough to notice, and not
checkable.

**And the partial dataset lied in the direction `P1` says it will.** At 280 of 283 ships the reach
read **8.51 %**, which is outside the band registered for it; the three ships missing were the
largest, and the floored blocks concentrate in exactly those. The finished walk reads 13.14 %. A
partial corpus result is not a small result, it is the wrong end of one.

### What it did: the floor is safe, exact about the clock, and far too expensive to default

`CorpusFloorWalk` finished 2026-08-25 — 291 ships, 1,164 paired cells, 2 h 42 m. Scored against the
rule fixed above, before the data.

| | prediction | reading | |
| --- | --- | --- | --- |
| the clock | no run loses simulated time | **0** cells of 1,164 ran the two arms on different clocks | holds |
| the safety | never stiffens, never floors in the control | **0** and **0** | holds |
| the reach | fewer blocks than `C3`'s cap of 6 reached, 5.83 % | **13.05 %** of blocks, more than double | **fails** |
| the cost | Δpeak p99 **under 0.03 K** | **27.76 K**, and a maximum of 48.21 K | **fails, by three orders of magnitude** |

**So the decision rule decides it: `FloorBlocksWhenOverBudget` stays off.** The rule said p99 at or
over 0.6 K and it stays off with `G6`'s cost half open and no lever left. p99 is 27.76 K. That it
would have rescued a criterion was set aside in advance and is set aside now.

**The cost prediction's argument was wrong in a way worth keeping.** It reasoned that a cap of
*budget* approximates less than a fixed cap of 6, from the 64,463-block rig where the budget was 10.
The budget is not small on this population — the median grant where the floor engages is **17**
substeps and only 7 % of engaged cells are granted fewer than 6 — so the reasoning was right and the
conclusion still wrong. What it missed is that the cost is not a function of the grant at all.

**The median cell is free and the tail is ruinous.** Δpeak is **0.006 K** at the median and 0.44 K
at p90; 31 % of engaged cells exceed 0.03 K and 8.5 % exceed 0.6 K. And the tail is not predictable
from how far the floor has to lift: correlating cost against over-subscription over 729 cells gives
**r = 0.305** in log-log, and every band has a bad tail — cells barely over budget, under 1.05×,
still reach a p99 of 32 K.

**Which forecloses the obvious repair.** If the damage were confined to grids the floor has to lift
a long way, a gate would fix it. Every gate tried either fails to reach 0.6 K or gates the mechanism
out of existence:

| gate | cells kept of 729 | p99 |
| --- | ---: | ---: |
| none, as built | 729 | 27.76 K |
| air scenarios only | 675 | 5.78 K |
| over-subscription under 1.25× | 316 | 25.65 K |
| floor under 1 % of the grid | 543 | 20.47 K |
| grids under 100k blocks | 556 | 17.68 K |
| floor under 0.1 % of the grid | 179 | 0.79 K |
| air **and** under 0.1 % floored | 172 | 0.69 K |
| air, under 0.1 %, and under 1.25× | 100 | **0.27 K** |
| fewer than 10 blocks floored | 59 | **0.37 K** |

Nothing reaches the threshold while keeping more than **14 %** of the cells the mechanism engages
on. A mechanism that is safe only where it does almost nothing is not a default, and it is not a
refinement waiting to be built either — that is the finding, and it is why this row closes rather
than becoming a smaller one.

**Scope.** These are the ships the element-visit allowance binds on and no others: 294 selected out
of 8,144, of which 291 produced paired cells. Nothing here is a population figure and none of it
describes the 96 % the mechanism never reaches (`P1`, `P2`). The block-weighted 13.05 % is
especially narrow — **15 % of the floored blocks come from the 16 largest cells alone**, where a
grant of 1 substep floors 91 % of the grid; per cell the median share floored is **0.32 %**.

### What a per-block cap does to the population, written before it is measured

`CorpusCapWalk` **finished on 2026-08-25 at 05:08**, all 8,144 blueprints in 3 h 51 m, after being
launched on 2026-08-24, stopped at 69 blueprints on an estimate that turned out not to be one, and
restarted from the beginning. The question, the statistic, the decision rule and the numbers that
would falsify each prediction were written here **before** it ran (`E1`, `E11`), because a saving
measured after the decision to ship it has been taken is a saving that will be found to be cheap.
They are left exactly as written and the result is [below](#what-it-did-the-walk-finished-and-three-of-the-four-predictions-hold).

> **What stopping it measured was the estimator, and the estimator was wrong.** The walk was
> abandoned at 45 minutes on a projection of *past ten hours*, taken from the share of the
> population's blocks it had covered divided by the rate it was covering them at — 0.14 % a minute
> over the last interval against 0.24 % averaged from the start, a rate that was *falling*.
>
> **The corpus is walked largest first, so that rate falls throughout every healthy run.** The fall
> is a property of the ordering rather than of the walk, and a progress mark is ten files, which on
> a largest-first corpus can be one capital hull or ten fighters. Run the same estimator over
> `CorpusAirWalk`, which finished in **104 minutes**, and over that walk's own first 35 minutes it
> projects **104 to 428 minutes, median 154** — a five-fold spread around an answer already known
> ([pace.py](../tools/corpus/pace.py), `P4`). It is not an instrument, and nothing should have been
> decided on it.
>
> **What the cost actually is: three and a half hours.** The cap walk is the air walk's four
> scenarios with a second arm on each, and the two arms share one blueprint parse, so the second arm
> can only add what it simulates and **2x is a ceiling by construction**. Over the fifty files the
> two walks' progress records share, the measured ratio is **1.95x**, and 1.95 x 104 minutes is
> 3.4 hours. The design note's original 3.5 hours was right; it was taken from a 40-ship stride
> sample, which is the part of the corpus where the per-ship overhead is hidden, so it was right for
> a reason that does not support it — and the figure that overturned it was worse.
>
> **So no cheaper design was built, and that is a decision rather than an omission.** A stratified
> sample would have to defend its selection rule (`M10`) and would answer the reach prediction — a
> share of *all* blocks in the population — only under that rule's own assumptions; dropping the
> vacuum anchor would save a quarter of a walk and cost the control that says air cannot make a hull
> softer. Neither is worth an hour and a half against a population walk that answers all four
> predictions as written.
>
> **The 69 blueprints of the abandoned run were not resumed onto**, because three commits touched
> the solver and the harness between that run and this one, and none of the three was checked for
> behaviour — two were documentation passes over comments and one closed a definition-id collision,
> which is exactly the shape of change that looks inert and is not. Restarting costs 35 minutes of the 3.4 hours and buys a dataset collected under one
> build (`M1`) — and the new run's first 69 ships are then a reproduction check on the old partial
> for nothing (`E7`).
>
> **That check has run and it reproduces exactly.** All **552 shared rows** agree on all 22 compared
> columns, worst relative difference **zero** — peak, demand, step cost, run seconds, links, floored
> nodes and every watt column ([reproduce.py](../tools/corpus/reproduce.py)). So the three commits
> were inert after all, which is now a measurement rather than the assumption it would have been had
> the run resumed.

**The question, and why it is one question rather than two.** [backlog.md](backlog.md) `C3` asks
whether `MaxSubstepsPerBlock 6` should be a default; `G6`'s cost half fails in air. They are the
same question. The cap is the one lever that lowers a step's *work* — it raises the mirrored heat
capacity of any element demanding more substeps than the cap grants, so the grid's demand becomes
`min(demand, cap)` and the work falls with it — and `C3` is undecided only because what it costs has
been measured on **one hull**.

**Why not simply raise the allowance.** It is the other lever and it is the wrong one, and saying so
before the data is part of the pre-registration. `G6` asks whether a *ship* is affordable. Raising
`MaxElementVisitsPerStep` until the p99 fits does not make a ship cheaper; it moves the same work
into the frame, and the criterion would then pass by having been re-pointed at a bound chosen to let
it pass — which is `E11` exactly. The allowance moves when what a shortened step gives up is priced
against what the frame costs, which is what `C27` did this morning and is not what this walk is
about.

**The design, and the one thing it has to get right.** Every ship, the four `PairLab.AirScenarios`,
two arms: the cap off as it ships, and `MaxSubstepsPerBlock 6`. **The two arms run to the same
simulated clock**, which is the whole reason this is a new walk rather than a second dataset joined
to `F11`'s. `Battery.Run` stops when the hottest block moves less than 0.25 K in a 60-second chunk,
so two arms of the same ship stop at different instants — and the difference the cap is expected to
make is a hundredth of a kelvin, two orders of magnitude *under* that stopping tolerance. So the
uncapped arm runs first and its elapsed clock is handed to the capped one (`M1`, `P6`). Comparing
two settle-stopped runs would have measured the stopping rule.

**Four predictions, each with what would falsify it.**

| | prediction | falsified by |
| --- | --- | --- |
| the identity | capped demand is `min(uncapped demand, 6)` on every ship and scenario | any run differing by more than 1 % |
| the benefit | work p99 **≈ 2.5 M** against the 4,000,000 allowance — `G6`'s cost half passes | a capped p99 over 4 M, or under 1.5 M |
| the cost | Δpeak p99 **under 1 K**, max under 10 K | a p99 over 1 K |
| the reach | the cap holds back **3–10 %** of all blocks in air | outside that band |

The benefit figure is arithmetic rather than a guess, and it is stated with its own weakness. Taking
the 2026-08-24 air walk's 32,575 rows and replacing each demand with `min(demand, 6)` gives a work
p99 of 1,679,952 in the unit those rows were scored in — which is the node half alone, so the
corrected figure is that times a hull's link-to-node ratio, about 1.5. Hence 2.5 M, and hence a
range of 1.7–2.9 M rather than a number. **A projection of the same dataset is not a measurement of
it**: the substitution assumes the identity in the row above, which is the first thing the walk
checks.

The cost prediction is the weak one and is the reason for the walk. Its only evidence is **one
hull**: 0.028 K on the worst-placed block of a driven census hull in thick air, and 0.017 K on the
hottest ([backlog.md](backlog.md) `C3`). A population is not one hull, and the ships this cap
reaches hardest are the ones with the stiffest fittings, which is not what a census hull is built
from. A p99 in the tenths of a kelvin would be unsurprising; a p99 in whole kelvin would mean the
cap re-masses hulls a player watches heat move through, which is what `stiffness.md` warns of below
its own cliff.

**A reading about the instrument, written down while the walk is still running and the verdict is
not known.** The two arms run to the same simulated clock, which is what stops the *stopping rule*
being part of the difference. It does not make that clock an equilibrium. `Battery.Run` stops when
the hottest block moves less than 0.25 K in a sixty-second chunk, and a hull drifting at exactly
that rate passes the test for ever — held for the rest of an 1,800 s scenario it is another 7.5 K.
So two arms can both be *settled* and still be tens of kelvin apart, because each is still
travelling at its own speed and a cap changes that speed: it raises the mirrored capacity of the
stiffest elements, which is a change to the **rate** and not to where the run ends up.

Measured on the walk's first 360 ships — the wrong end of the population, so the numbers below are
not a result — the pairs more than a kelvin apart have a control still moving at a median
**0.03 K/s**, seven times the tolerance, against a population median of 0.001. Splitting the same
deltas on whether the control's own peak had stopped moving when it was read separates them by two
orders of magnitude.

**So `cap.py` prints that split beside the registered statistic and not instead of it.** The
decision rule below was written against the unsplit p99 and is scored against the unsplit p99;
re-pointing it at a subset chosen after the data is `E11` exactly. What the split is for is the
argument that follows the number: *how far apart two arms are on the way somewhere* and *what the
approximation costs at equilibrium* are both real and are not the same quantity, and a reader
deciding whether a saving is worth taking needs to be told which one they are looking at.

**The decision rule, fixed now.** The mod ships fidelity by default and a saving as a switch, and
this repository has two calibration points for what *imperceptible* means: **0.028 K** is accepted
as the price of the substep ceiling's breach, and **0.607 K** is what keeps `MaxSubstepsPerBlock`
out of the defaults today. So:

* **p99 Δpeak at or under 0.03 K** — ship the cap as the default. It costs what the mod already
  accepts elsewhere and it closes `G6`'s cost half.
* **p99 Δpeak at or over 0.6 K** — it stays a switch. That is the number that made it one, measured
  on a population instead of a hull, and `G6`'s cost half stays open with the allowance as the only
  remaining lever.
* **between them** — a judgement, argued when the number is in, and argued in the open (`E11`).
  Nothing about that band is decided here except that it will not be decided by whether the cap
  happens to rescue a criterion.

### What it did: the walk finished, and three of the four predictions hold

**8,144 blueprints, four scenarios, two arms, 3 h 51 m** — `out/cap-2026-08-25`, summarised in
[`summary-cap-2026-08-25.csv`](../tools/corpus/summary-cap-2026-08-25.csv). The walk's own
assertions passed, which is the identity, the clock, the reach and the control.

| | prediction | measured | |
| --- | --- | --- | --- |
| the identity | capped demand is `min(uncapped, 6)` | **0 of 32,576 pairs** differ by more than 1 %; the worst is 0.0000 substeps | **holds** |
| the benefit | work p99 1.7–2.9 M, `G6`'s cost half passes | **2,180,352** against the 4,000,000 granted, from 7,293,904 uncapped | **holds** |
| the cost | Δpeak p99 under 1 K, max under 10 K | p99 **0.2820 K**, max **48.2190 K** | **fails on the max** |
| the reach | 3–10 % of blocks in air | **5.83 %** | **holds** |

**The benefit prediction was arithmetic and it was right.** 2.5 M was projected by substituting
`min(demand, 6)` into the air walk's rows and multiplying by a hull's link-to-node ratio; the walk
measured 2.18 M. Uncapped, the same population is 7.29 M — **1.82× the allowance**, with 733 of
32,575 runs past it; capped, 131. So the cap does what it was proposed for.

**The cost prediction fails on its second half, and the tail is not censored.** Ten pairs are more
than 14 K apart and the control is past nothing on any of them: `NX-01 (Vanila)` reads 484 K
uncapped and 533 K capped at hot noon. What they have in common is size — every one of the ten is
between 79,460 and 155,010 blocks.

### The re-take on a corrected reader: what it should find, written before it finds it

**`A13` is why this walk is being run again.** Every figure in the two sections above was measured
through a blueprint reader that built eleven kinds of block as plain armour, so every vanilla oxygen
generator, gravity generator, door, hangar door, passage, ladder, air vent, oxygen tank and large
turret in `out/cap-2026-08-25` was 500 kg of steel drawing no power and making no heat. The reader
is fixed. The dataset is not, and `C3` was decided on it.

**The re-take walks the core corpus, not the population, and that is a choice with a price.** The
whole corpus in paired arms is about eleven hours; [`core.py`](../tools/corpus/core.py)'s
stratified sample is **78 minutes** and estimates this walk's own work p99 to within **0.9 %**
out of sample. What it cannot do is a maximum — it keeps one giant in twenty, and a maximum is one
observation — so **the cost prediction's *max under 10 K* half is unscoreable on this dataset and
is reported `?` rather than passed** (`E8`). The 2026-08-25 walk failed on exactly that half. So the
re-take can confirm or overturn the *decision* and cannot re-score that failure, and saying which
before the run is the point of writing this down.

**What is predicted, and what would falsify it.** The reasoning is one fact measured twice: `A13`
is worth **15.76 %** of the population's heat and **nothing whatever** to its cost. The re-taken
stiffness walk moved only in `Census.Corpus`'s last place, and the re-taken air walk's substep
demand — p99 34.779, p95 30.195, max 36.614 — is identical to this walk's control arm to three
decimals. Heat is a sum over blocks and stiffness is a maximum over them, and the corrected blocks
are heavy: a gravity generator is eight tonnes, so it makes megawatts and demands almost no
substeps.

| | prediction | falsified by |
| --- | --- | --- |
| the identity | unchanged: capped demand is `min(uncapped, 6)` on every pair | any pair differing by more than 1 % |
| the benefit | weighted capped work p99 in **1.9–2.5 M**, straddling the 2,180,352 the broken reader measured | outside that band |
| the cost | weighted Δpeak p99 stays in the **0.03–0.6 K judgement band**, so `C3`'s decision does not invert | a p99 at or under 0.03 K, or at or over 0.6 K |
| the reach | still **3–10 %** of blocks in air | outside that band |

**The benefit band is the sampling error, not a new physical claim.** Work is substep cost times
demand; block identity moves neither the node count nor the link count nor the demand, and the air
re-take put the movement at **−1.30 %**. The band is that figure plus `core.py`'s measured
out-of-sample error on this very walk's control arm — work p99 −0.9 %, p95 −3.5 % — widened to a
round 1.9–2.5 M. **A prediction that cannot be falsified by a sampling error is not a prediction
about the population**, which is what makes the width honest rather than convenient.

**The cost prediction is the one that decides something.** `C3` stays closed the way it closed if
the p99 lands in the band; it reopens either way out. A p99 *under* 0.03 K would say the broken
reader had inflated the cap's cost and the cap should ship — and it is the direction the physics
argues against, because `A13` adds heat and heat is what a transient is made of. A p99 *over* 0.6 K
would say the corrected blocks make hulls the cap re-masses visibly, and the switch becomes a
refusal rather than a default deferred.

**One more reading is registered here because it is nearly free.** This walk's control arm is the
same four scenarios on the same build as [`out/air-2026-08-28`](../tools/corpus/summary-air-2026-08-28.csv),
so on the ships they share the two must agree to the last bit — same solver, same definitions, same
scenarios, and neither arm caps. Any disagreement is a defect in one of them and not a finding about
the population (`E7`). [reproduce.py](../tools/corpus/reproduce.py) is the check.

**What this walk does not answer, said before it runs**: the maximum, any rate below about half a
per cent, and the per-ship table in the section below — *6,548 of 8,144 ships pay in full and
collect nothing* is a count over the whole population, and a sample estimates its share and not its
count. The judgement that closed `C3` rests on that share, so the re-take tests the share and
leaves the count to the dataset that has one.

### What the re-take found: all four hold, and the cost doubled

**5,463 ships, four scenarios, two arms, 21 m 32 s** — `out/cap-core-2026-08-28`, summarised in
[`summary-cap-core-2026-08-28.csv`](../tools/corpus/summary-cap-core-2026-08-28.csv). Every
prediction registered above holds, and the one that decides something moved a long way inside its
band.

| | prediction | measured | |
| --- | --- | --- | --- |
| the identity | `min(uncapped, 6)` on every pair | **0 of 21,848 pairs** differ by more than 1 %; the worst is 0.0000 substeps | **holds** |
| the benefit | weighted capped work p99 in 1.9–2.5 M | **1,927,404**, from 7,223,225 uncapped | **holds** |
| the cost | weighted Δpeak p99 stays in the 0.03–0.6 K judgement band | **0.5660 K** | **holds** |
| the reach | 3–10 % of blocks in air | **5.98 %** | **holds** |

**The registered reproduction check is exact.** This walk's control arm is the same four scenarios
on the same build as `out/air-2026-08-28`, and on the **21,852 rows they share it agrees on all 22
columns, worst relative difference 0.00e+00** ([reproduce.py](../tools/corpus/reproduce.py)). The
control arm *is* the air walk repeated, which is what makes the capped arm's difference the cap and
nothing else (`E7`, `M1`).

**`C3`'s decision does not invert, and it is much closer to inverting than it was.** The p99 Δpeak
was **0.2820 K** through the broken reader and is **0.5660 K** through the corrected one — it
doubled, and the rule's refusal threshold is 0.6 K. The share of pairs moving more than the 0.03 K
the mod already accepts went from 9.58 % to **18.24 %**, and the share past the 0.6 K it refuses
from 0.50 % to **0.84 %**. So the cap is *worse* than it was thought to be, in the direction the
pre-registration argued it would be: `A13` is 15.76 % more heat, heat is what a transient is made
of, and a cap changes the rate a transient travels at.

**The verdict stands and it stands for a better reason.** `MaxSubstepsPerBlock` stays 0. It was
kept a switch on the argument that the error is charged per block and the benefit collected per
grid, and that argument does not depend on the figure that moved; what the re-take adds is that the
figure moved *toward* the threshold that would have refused it outright, so the decision has more
room under it than it did, not less.

**The benefit is unchanged, which is the stiffness finding arriving by a third route.** Capped work
p99 is 1,927,404 against 2,180,352 through the broken reader, and the uncapped arm is 7,223,225
against 7,293,904 — a movement of a per cent or so where the heat moved by a sixth. Block identity
is worth nothing to a step's cost. That is now measured on the stiffness walk, on the air walk's
demand percentiles and on this walk's two arms.

**Two things this dataset does not say, and they were named before it ran.** The cost prediction's
*max under 10 K* half is **unscored**: the sample's largest delta is 276.41 K, on `Wymsageu` in
`storm-parked`, and a sample of one giant in twenty has no population maximum. And the judgement
that closed `C3` rests on *6,548 of 8,144 ships pay in full and collect nothing*, which is a count
over the population; this walk tests the share and leaves the count to the dataset that has one.

**Eight rows were dropped as strays, and chasing them found a defect.** They carry `workshop_id` 0,
and on the air walk **fourteen distinct ships share that id** — a workshop *collection* puts the
blueprint one folder deeper than an ordinary item, so the id was read off the ship's name, parsed to
nothing and fell back to 0. Anything keyed by workshop id counted the fourteen as one ship. The
reader is fixed and the case is pinned; `cap.py` dropping them rather than inventing a weight of 1
is what made them visible at all.

### And the trade is the wrong way round: the cost is per block, the benefit is per grid

This is the finding, and it is not one the pre-registration anticipated.

| blocks | ships | runs | over the allowance uncapped | capped | Δpeak p99 | blocks floored |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| under 1,000 | 3,830 | 15,360 | **0.00 %** | 0.00 % | 0.1230 K | **7.44 %** |
| 1,000–5,000 | 2,718 | 10,872 | **0.00 %** | 0.00 % | 0.2790 K | 7.52 % |
| 5,000–20,000 | 1,115 | 4,468 | 0.07 % | 0.00 % | 0.5610 K | 5.79 % |
| 20,000–60,000 | 354 | 1,416 | **28.04 %** | **0.00 %** | 0.8160 K | 5.12 % |
| 60,000+ | 115 | 460 | **72.39 %** | **28.48 %** | 26.9770 K | **3.14 %** |

**Stiffness is a property of a block and the allowance is a property of a grid.** A light fitting
demands the same substeps on a fighter as on a dreadnought, so the cap *binds* on 86–98 % of runs at
every size and re-masses **7.4 % of the blocks on the smallest hulls against 3.1 % on the largest**.
The allowance only binds on hulls with enough blocks to spend it: **no run under 5,000 blocks is
over it, capped or not**.

So **6,548 of 8,144 ships — 80 % of the corpus — are charged the cap's error in full and can collect
none of its benefit.** The 469 ships over 20,000 blocks collect all of it.

### The judgement, argued in the open

The p99 is **0.2820 K**, which is between the 0.03 K that would have shipped it and the 0.6 K that
would have kept it a switch — the band the rule reserved for a judgement, on the condition that it
*would not be decided by whether the cap happens to rescue a criterion*. It does rescue one. That is
set aside, as promised.

**`MaxSubstepsPerBlock` stays 0, and the reason is who pays rather than how much.** 0.282 K is not
too much on its own; the mod has accepted 0.03 K elsewhere and refused 0.607 K, and this sits under
the figure it refused. What decides it is the table above: the error is charged per block and the
benefit is collected per grid, so **6,548 of 8,144 ships pay in full and collect nothing**. A saving
four fifths of the population cannot use is the exact case
[fidelity is the default; a saving is a switch](document-of-intent.md#fidelity-is-the-default-a-saving-is-a-switch)
exists for, and shipping it would be taking a fifth of the population's problem out of the other
four fifths' fidelity.

> **Re-measured on the corrected reader, and the argument is the one that survived.** `A13`'s
> re-take put the p99 at **0.5660 K** rather than 0.282 K — still inside the judgement band, but a
> hair under the 0.6 K that refuses outright rather than half way. **The decision does not move,
> because it never rested on the figure**: it rests on who pays, and the who has not changed. What
> the re-take does change is that the *how much* is no longer comfortable, so a future world cap
> arrives with less room than this paragraph originally described
> ([the re-take](#what-the-re-take-found-all-four-hold-and-the-cost-doubled)).



**And leaving it as a switch is not an answer either**, because `G6`'s cost half then stays failing
with the only lever that moves it behind a setting nobody will find.

**What the measurement points at is a third thing, and the pre-registration did not consider it.**
The allowance does not approximate — `ThermalSimulation.AffordableStepSeconds` **shortens the step**
when a grid cannot afford its demand, so an over-budget grid falls behind real time. That is a
fidelity loss too, and an unbounded one. The cap is an approximation with a measured price:
**0.024 K at p99 on the pairs whose control had stopped moving**, under the 0.03 K this mod already
accepts. And the grid already knows which case it is in, because the budget is evaluated per grid
per step.

So the route for `G6`'s cost half is a cap that engages **when a grid is over its allowance**:
nothing on the four fifths that never reach it, the whole benefit on the 469 ships over 20,000
blocks, and a bounded approximation in place of an unbounded lag. What has *not* been measured is
the comparison that decides it — a shortened step against a floored one, on the same hull, at the
same clock — and that is [backlog.md](backlog.md) `C30` rather than a conclusion here.

### What the swap is worth, measured on the hulls where the budget binds

`C30`'s question is whether replacing a shortened step with a floored one is an improvement or a
different loss. `CapVersusAllowanceTests` runs both on one hull at one allowance, at the sizes `C3`
found the allowance actually binds at, in flight:

| blocks | allowance | cap | demand | granted | clock | what the lost clock stands at |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 32,800 | 4,000,000 | off | 27.49 | 20 | **72.5 %** | 9.081 K |
| 32,800 | 4,000,000 | 6 | 6.00 | 20 | **100.0 %** | 0 |
| 64,463 | 4,000,000 | off | 27.49 | 10 | **36.2 %** | 36.976 K *(past the ladder)* |
| 64,463 | 4,000,000 | 6 | 6.00 | 10 | **100.0 %** | 0 |

**A shortened step is not an approximation, it is a slow clock**, and the mechanism `F23` priced.
The 64,463-block hull keeps **36.2 %** of real time at the allowance that ships: its thermal
simulation runs at a third speed for as long as the load lasts. Priced on the measured rate ladder
that is **36.98 K standing**, and the deficit is past the last rung the ladder measured, so the
figure is a floor rather than a reading.

**The cap removes the deficit entirely** because it removes the cause: demand falls from 27.49 to
6.00, which is inside the 10 substeps the budget grants, so nothing is shortened and the grid keeps
its whole clock. What it charges instead is the population figure from `C3` — **0.024 K at p99** on
the pairs whose control had stopped moving.

**So the swap is worth nine to thirty-seven kelvin against twenty-four thousandths of one.** That is
three orders of magnitude and it is not a close call.

**The scope both halves carry** (`P1`). The kelvin price of a lost clock is read off
`AllowanceLab.PriceRates`, which runs the mechanism on its own rig under a **moving** load — which
is where a clock error shows at all, since `F23` measured 0.00 K under a steady one. So these are
upper bounds for a ship whose load keeps changing and say nothing about a settled grid. And the
cap's 0.024 K is a population figure rather than this hull's. The two sides are therefore compared
in order of magnitude rather than subtracted.

**And the walk rescores `G6` itself.** Its uncapped arm is the first corpus dataset to carry
`substep_cost`, so it replaces the withdrawn figures above rather than merely being compared with
them — and because it repeats `F11`'s four scenarios on the same ships, its demand column is a
reproduction check on that walk (`E7`).

**G7 holds.** All 705 prefabs, 461,428 blocks, idle: not one crosses critical, let alone loses a
block. The same 705 flown hard lose 616, which is the control rather than the criterion — see
[balance.md](balance.md#the-compatibility-floor-holds).

**G8 was written here before the sweep that tests it**, on the same terms as `G7`, and its five
decisions could each have gone the other way:

* **Ships, not block types.** The reading that opened [backlog.md](backlog.md) `C12` timed each of
  the 72 block types that cannot cool themselves from 293 K, alone, and found none in the window. A
  player never meets a block alone — they meet it bolted to a hull that conducts heat out of it —
  and the whole projected route to the window works *through* that conduction. A criterion over
  isolated blocks would be insensitive to the dial the answer is expected to be.
* **The crossing, not the loss.** The crossing is when a readout changes and a player can act; the
  loss is a median 37 s later ([balance.md](balance.md#how-long-a-block-has-after-it-crosses)). The
  window is about the event a player is meant to notice and respond to, so it is the crossing.
* **Full electrical load.** The state `G2` is already scored in, and the one a player reaches by
  turning everything on rather than by flying in a particular direction. Thrust would make the
  answer a property of a heading.
* **The median, not a share.** A window is two-sided, so it needs a point statistic rather than a
  count, and the median is what the population tables already carry.
* **An hour for the hull, stated as a number rather than as "too long".** A ship that is still
  cooling long after the load came off is a ship a player cannot use, and the shipped clock brings
  one back in about 1,320 s. An hour is the round figure inside a session and outside the shipped
  value by more than a factor of two, so it can fail without being a restatement of the status quo.
  *(The scenario this is scored in was corrected before the grid was read — see below.)*

**G8's second half was corrected once, before the grid was scored against it**, and the direction
matters: the change made the criterion *stricter*, not looser. It was written as *at idle, the median
time for a hull to reach equilibrium stays under an hour*, and idle turned out to be a scenario that
cannot answer it. A hull at idle in vacuum shadow has no equilibrium — it cools toward the vacuum
floor — and the settling figure is *seconds until the hottest block came within 5 K of where it
ended*, which at low `HeatTimeScale` a hull satisfies at the first sample because it has barely
moved. Measured: the median runs 1,230 s, 2,370 s, 3,450 s as the clock falls 225 → 112 → 56, then
reads **120 s** at 25, 15 and 11 with 26 to 36 of 40 hulls sitting on the floor. Under the old
wording four cells passed on a column that was a blind spot; under this one **none of them does**.
The reading is pinned by `SettleReadingTests`, and recovery — from a full burn, throttled to idle —
is the scenario where the hull is driven somewhere and back, so the same figure is a real duration
there and is the one a player actually waits through.

**G8 is measured, and it holds — at a configuration the mod does not ship.** The paired sweep ran
twenty-five cells of conduction against the clock on the forty-hull retest set, and four of them
satisfy both halves: conductivity ×4 at `HeatTimeScale` 120, 100, 90 and 80, crossing at 124–186 s
and recovering in 2,220–3,270 s, at 1.36–2.04× the shipped substep demand and keeping `G1`, `G2`
and `G5`. The projection that opened the question — ×4 with the clock near 15 — was out by a factor
of five; the composition rule behind it was not, and holds to 1 %. The grid, the two curves that
locate the answer and what excludes conductivity ×8 are in
[balance.md](balance.md#two-dials-at-once-the-window-is-reachable-at-conductivity-4-with-the-clock-near-100).
**Whether to ship it is a separate decision** and is [backlog.md](backlog.md) `C12`: retuning moves
every temperature figure in the repository.

**The first reading of that grid was wrong, and the criterion is what caught it.** The crossing
median had been taken over the hulls that crossed rather than over the hulls that were loaded, and
two cells at conductivity ×8 scored as satisfying `G8` on a population where 27 of 40 hulls never
reach critical at all. A hull that never crossed is censored above, not absent (`E9`) — the same
treatment the settling half already gave a hull that never settled — and read that way ×8 has no
median at any clock. The rule is pinned by `tools/corpus/test_scoring.py`.

**G7 was written here before it was measured**, which is the whole of `E11` — a criterion added after
the numbers arrive is not a criterion. Four choices in it were decisions rather than conveniences,
and each could have gone the other way:

* **Losing a block, not crossing critical.** The floor is that a spawned ship does not *fall apart*.
  The two are different events and a block that has just crossed is losing nothing; measuring the
  crossing would fail ships that never lose anything. See
  [balance.md](balance.md#how-long-a-block-has-after-it-crosses).
* **Idle, not loaded.** Arrival is what is being tested. A ship the player has not touched is not
  charging a jump drive, and what happens once they fly it is `G2`'s question.
* **Five simulated minutes.** Long past where a corpus ship that is going to lose a block has lost
  it — the crossing-to-loss gap is under a minute at p90 — and inside the clocks the lab already
  runs.
* **The environment its category spawns into.** Planetary encounters on a planet surface at noon,
  everything else in sunlit vacuum. Running every prefab in both would be a harder test than the
  truth and would fail ships for a place they are never put.

**G3 is answered, and the answer is that the two fits fail in opposite ways.** `retrofit` parses a
real workshop hull, runs it under full electrical load to find where its heat actually is, and puts
the mod's own blocks in the cells that hull left free — so the constraint a player is under is part
of the measurement rather than assumed away.

```bash
dotnet run --project Thermodynamics.Sim -- retrofit --ships 500 --csv out/
```

Over 427 real ships, 332 of which got warm enough under load for cooling them to mean anything:

| fit | fits on | median | p90 | helped >1 % | hurt >1 % |
| --- | ---: | ---: | ---: | ---: | ---: |
| **bolted** — radiators against the hot block | 281 of 332 | **−0.11 %** | 2.45 % | 60 | **85** |
| **plumbed** — a ring, a pump and a sink face | **49 of 332** | **+1.35 %** | 8.94 % | 26 | 5 |

**Bolting fits nearly everywhere and does not work.** It makes more ships worse than better, by the
mechanism [blocks.md](blocks.md) states and the `coolers` ladder found on a rig: a block against a
face is a face that was radiating to the sky and now radiates into a neighbour. The worst hull here
went from 2,941 K to 4,319 K for having seven radiators bolted to its jump drive.

**Plumbing works and hardly ever fits.** Where a ring lands it helps five ships for every one it
hurts, and its ninetieth percentile is nearly four times the bolted one — but a closed loop of free
cells with one of them face-adjacent to the hot block exists on **fifteen per cent** of warm hulls.
A finished ship does not leave a ring of empty cells around the thing that gets hot.

> **The sink face is the whole difference, and leaving it out inverts the result.** The first run of
> this lab built rings without requesting one, which couples a ring to a hot block through ordinary
> block-to-block conduction — the bolted case with extra pipes. It reported a median of 0.13 % over
> 108 fits. Requiring the sink halved what could be fitted and doubled what it bought. That is the
> 1,000 W/K against 167 in [balance.md](balance.md#the-three-findings), arriving as a retrofit result.

So the mechanic is real and the ships people have built cannot receive it. That is a balance
decision rather than a defect — whether cooling should be fittable to a finished hull, or whether
it is something you design in — and it is the one G3 was asked to surface.

**G6 is not a balance criterion and is here on purpose.** The corpus answers the open questions in
[stiffness.md](stiffness.md) and the D-series of the [backlog](backlog.md) — lumping, multirate,
the substep cap — better than any synthetic ladder can, because those decisions turn entirely on
the *shape of the tail* of the stiffness distribution, and nobody has ever seen it.

### 1. Acquire a corpus

The target is ten thousand popular unmodded designs. See [Acquisition](#acquisition) below for what
that actually costs — it is the one step with constraints outside this repository.

The corpus is a directory of `bp.sbc` files. Nothing downstream cares how they got there, so the
acquisition route can change without touching the lab.

### 2. Screen everything (cheap)

Every ship in the corpus, no solver:

* block census by type, mass and derived thermal properties — this is what replaces `Census`;
* installed power, thrust and their ratio to hull mass;
* exposed area, buried fraction, room count;
* cooling fitted: radiators, loops, heat pumps, vents;
* **stiffness demand** — conductance over capacity per block, and the distribution's tail, in two
  worlds: `stiff` is the ship in vacuum and `air` is the same ship at sea level.

Stiffness is the one measurement here that is not a property of the ship. Half of it is what a
block exchanges with the world over its exposed area, and in a vacuum that half is radiation
alone — so a hull that demands 2.5 substeps in orbit demands 14.3 over a planet, set by a light
fitting either way. Both are reported because a ship is flown in both, and which one a specimen is
selected on is a decision rather than a default. **It is currently the vacuum figure**, which is
the conservative choice for a feature the panel is stratified on and is worth revisiting once the
battery has run against both.

This pass alone answers G6 and re-founds `Census` on a population. It also produces the strata for
what follows.

### 3. Run the scenario battery

The battery is built along **four independent axes**, and a scenario is a point on all four at
once. Nothing is included because it seems interesting: a scenario earns its place by answering a
question no other scenario answers, and where two would answer the same one the cheaper is kept.

| Axis | What varies | Why it is its own axis |
| --- | --- | --- |
| **Environment** | vacuum, sun, air, weather, ground | External heating and external cooling are the *same* axis — a planet's surface does both depending on the hour |
| **Motion** | speed, and direction of travel | Friction heats the leading face and airflow cools every face; which wins is a question of speed, and *where* it lands is a question of heading |
| **Load** | idle, full electrical, thrust per direction, weapons, everything | Where heat is made decides where it concentrates |
| **Configuration** | as built, against the same ship with cooling fitted | The only axis the player controls directly, and the one G3 is about |

**Directional cases are enumerated, not sampled.** A ship is not symmetric: most designs have their
thrusters and their thin armour on different faces, so running only the forward case would miss the
thing the case exists to find. Both thrust and travel expand to six directions.

#### The scenarios

*Environment, with the ship idle* — anything reached here came from outside it, which is what makes
these the controls for everything below.

| Scenario | The question |
| --- | --- |
| `vacuum-shadow` | The cold reference: nothing in, nothing out but radiation |
| `vacuum-sunlit` | External heating at its worst — one face held to the sun |
| `orbit-cycling` | Thermal inertia: does a hull average the day or chase it |
| `surface-hot-noon` | External heating in air: hot ground, high sun, still |
| `surface-cold-night` | External cooling: does anything freeze below a usable floor |
| `surface-windy` | Forced convection at rest — what wind alone is worth |
| `underground` | Buried: no sun, no wind, rock ambient |

*Load, run in shadow on purpose* — with no sun or air to argue about, anything reached here is the
ship heating itself, which is the only way to attribute it.

| Scenario | The question |
| --- | --- |
| `idle` | **G1**: does a parked ship survive doing nothing |
| `full-electrical` | **G2**: every consumer at rating, reactors supplying what they ask |
| `all-peak` | Where heat concentrates when everything runs at once |
| `burn-{forward,backward,left,right,up,down}` | **Hot spots.** A ship burning forward heats the thrusters at its stern and nothing at its bow, and which blocks those are is a property of the design |

*Motion* — friction against airflow. The hull feels one scalar, the relative wind, so a session
with both a storm and a moving ship cannot attribute a heat to either. The battery holds each
contributor still while the other moves, then runs the two compositions where the sum does
something neither part does. `FrictionIsolationTests` pins the same matrix at unit scale.

| Scenario | The question |
| --- | --- |
| `flight-50`, `flight-100` | Velocity alone, in still air: friction against airflow, at which speed |
| `flight-300` | The same, at the speed limit the servers this mod is played on actually run |
| `storm-parked` | Wind alone: a parked hull in a 100 m/s gale heats exactly as `flight-100` does |
| `storm-300` | Wind alone at the raised limit, so the airspeed can be attributed at 300 too |
| `flight-headwind` | Composition: 40 of wind against 60 of speed trips a threshold neither reaches alone |
| `flight-downwind` | Composition: 80 of speed in a 60 m/s tailwind is 20 of airflow — no friction at full throttle |
| `reentry` | The leading face at terminal speed in thick air |

*Transient*

| Scenario | The question |
| --- | --- |
| `recovery` | **G5**: from a full burn, throttled to idle, does it come back |

#### What every run records

A single final temperature cannot judge balance, and the reason is spatial. Two ships settling at
the same peak are different ships if one is uniformly warm and the other is cold everywhere except
a 900 K knot around its thrusters — the first has a cooling problem, the second a **layout**
problem, and only one of them is fixed by adding radiators.

So [`ScenarioOutcome`](../tests/Thermodynamics.Harness/ScenarioOutcome.cs) records the distribution
and where its top end is:

| Group | Fields |
| --- | --- |
| Where it ended up | peak, mean, median, p95, min kelvin |
| How unevenly | gradient (peak − min), **hot spot** (peak − mean) |
| Where the hot spot is | hottest block subtype and cell, and how many blocks are within 50 K of it — one block is a definition problem, fifty is a layout problem |
| Whether it survived | blocks over critical, share, margin at the peak block, seconds to first critical |
| How it got there | seconds to settle within 5 K of final, fastest rate K/s |
| What put the heat there | watts by mechanism — radiation, convection, solar, friction, generation |
| Balance | watts made against watts vented |
| Cost | substeps demanded and granted, energy drift |

**The per-mechanism shares are what make an outcome interpretable.** A hull hot from solar gain
wants shading or a lower absorptivity; one hot from friction wants to slow down; one hot from its
own reactors wants radiators. The temperature alone says none of that.

### 4. Sweep the settings space (expensive, small sample)

Roughly 50 ships spanning the strata, across a settings space. The output is not one answer but a
**region**: every settings vector that satisfies G1–G6, with its cost. The shipped configuration
should be a point inside it, and today that is asserted rather than measured.

Axes worth sweeping: `HeatTimeScale`, `MaxSubsteps`, `MaxSubstepsPerBlock`,
`MaxElementVisitsPerStep`, and the definition-side dials the derivation exposes — the waste
fractions and the critical-temperature blend.

Coordinate sweeps rather than a full grid: the space is large and the axes are close to separable
in their effect on cost, if not on balance.

## Acquisition

**This is the step with constraints this repository cannot remove.** Three routes, and they trade
off effort against scale:

| Route | Needs | Scale | Notes |
| --- | --- | --- | --- |
| **Subscribe in game** | nothing | tens to hundreds | Steam unpacks subscribed items to `steamapps/workshop/content/244850`, already in the layout the lab reads. Free, no credentials, no rate limit. This is what the lab runs on today. |
| **Steam Web API + direct UGC** | a free Web API key | thousands, if it works | `IPublishedFileService/QueryFiles` lists popular blueprints by tag and sort order. Whether the items expose a downloadable `file_url` is **unverified** — newer workshop items often do not, and it has to be probed rather than assumed. |
| **SteamCMD** | a Steam account owning the game | ten thousand | `workshop_download_item 244850 <id>` in batches. Anonymous login generally fails for a paid title. Hours of downloading, and worth rate-limiting out of courtesy. |

**The route taken is the second for listing and the third for fetching**, built as
[`CorpusFetch`](../tests/Thermodynamics.Sim/CorpusFetch.cs):

```bash
# list only: writes out/corpus/manifest.csv, downloads nothing
dotnet run --project Thermodynamics.Sim -- corpus-fetch --key <webapi> --top 10000 --list-only

# list and fetch
dotnet run --project Thermodynamics.Sim -- corpus-fetch --key <webapi> --user <steam-account> --top 10000
```

Ranked by **total unique subscriptions**, not by votes or recency: the corpus is meant to be the
designs people build and fly, and a subscription is the closest signal the workshop has to that.
Ranking by vote would over-weight the spectacular and by date the untested. Items tagged `Mod`
alongside `Blueprint` are dropped at listing rather than downloaded and rejected.

**No credential passes through this tool.** SteamCMD is invoked with a user name and no password,
which works once the account has been logged in by hand:

```bash
steamcmd +login <user> +quit      # once, interactively; SteamCMD caches it
```

Anything else would mean this program handling a password, and it has no business doing that.

The run is resumable and rate-limited: pages pause, fetches go in batches of fifty, the manifest is
reused if it is already long enough, and anything already on disk is skipped. SteamCMD unpacks into
the layout `Blueprints` already reads, so the corpus directory is pointed at rather than assembled.
A nonzero SteamCMD exit is not treated as failure — on a corpus this size an item being deleted or
made private between listing and fetching is routine.

Two filters apply whatever the route:

* **Unmodded only**, and strictly: one unresolved subtype disqualifies a ship, because there is no
  way to tell whether the block that failed to resolve was a decorative panel or the reactor.
* **Designs, not fragments.** Ships under 25 blocks are cockpits, doors and test rigs.

## The pieces

| Piece | What it does |
| --- | --- |
| [`GameBlocks`](../tests/Thermodynamics.Harness/GameBlocks.cs) | Reads every definition out of the installed game — size, mounts, sealing, build cost. |
| [`Blueprints`](../tests/Thermodynamics.Harness/Blueprints.cs) | Turns a `bp.sbc` into ships the solver can run. Models are derived and shared across the corpus. |
| [`CorpusLab`](../tests/Thermodynamics.Harness/CorpusLab.cs) | Corpus yield and size distribution. `-- corpus [--path <dir>]`. |
| [`BlueprintTests`](../tests/Thermodynamics.Tests/BlueprintTests.cs) | Tests on the yield, all synthetic. A real subscribed ship building a simulation that steps is `CorpusSurvey`, which does it for every ship in the corpus. |
| [`CorpusFetch`](../tests/Thermodynamics.Sim/CorpusFetch.cs) | Lists and fetches the corpus. `-- corpus-fetch`. Unexercised against a real key. |
| [`ShipProfile`](../tests/Thermodynamics.Harness/ShipProfile.cs) | Step 2. Every ship measured without stepping it. `-- screen`. |
| [`Specimens`](../tests/Thermodynamics.Harness/Specimens.cs) | Cuts a corpus to a panel that covers it. See [Specimens](#specimens) below. |
| [`ShipLoad`](../tests/Thermodynamics.Harness/ShipLoad.cs) | What a ship has switched on, thrust per direction. |
| [`Battery`](../tests/Thermodynamics.Harness/Battery.cs), [`ScenarioOutcome`](../tests/Thermodynamics.Harness/ScenarioOutcome.cs) | Step 3. `-- battery`. |
| Steps 0 and 4 | Criteria written above; the settings sweep is designed and unbuilt. |

The corpus these run against is 9,981 blueprints yielding **8,142 hulls**, of which 8,132 are
distinct name-and-id pairs. A subscription list yields far less — mostly mods, and mostly under the
size floor — which is why the acquisition route matters.

## Specimens

Ten thousand ships is the right number to *start* with and the wrong number to keep running. A
corpus of popular workshop designs is enormously redundant — five hundred small-grid fighters differ
in silhouette and not in anything this simulation can see — and every one of them costs the same to
run as the one ship that taught you something.

**The useful measure of a ship is not how typical it is but how much it says that nothing else
says.** So the panel is chosen by *coverage*, not by frequency:

* the extremes of every feature axis first, because an axis with no example at its end is an axis
  the panel cannot speak about at all;
* then greedy maximin — repeatedly take the ship furthest from everything already taken, which
  spreads a small sample through a space without reproducing its density.

Picking the most popular ships instead would produce a panel of near-duplicates from the densest
part of the design space and no examples of anything unusual, which is exactly backwards: the
interior of a cluster is predictable from its edges, and the edges are where a balance figure fails
first.

Two numbers come out of it. **Fidelity** is the distance from the worst-served ship in the corpus to
its nearest panel member — how well the panel covers what it came from. **Redundancy** is how many
ships sit on top of another, which is what says when a corpus has stopped being worth growing.

**This is a hypothesis and it has to be checked.** The claim is that two ships close together in
feature space behave the same way under the battery. Until the battery has been run over a full
corpus once and the panel's verdicts compared against it, a reduced panel is a guess about what
matters. Fidelity is a statement about the feature space rather than about behaviour, so it is
necessary and not sufficient.

## Running the lab: parallel and linear

Two modes, and the distinction is not a preference.

| Mode | For | Why |
| --- | --- | --- |
| **Parallel** (default) | balance and data collection | Every result is a settling problem that is a pure function of a ship and a scenario. Nothing about a temperature changes because another core was busy, so going wide is free accuracy. |
| **Linear** (`--linear`) | anything whose figure is a *duration* | The moment a result is a time, every other core is noise — cache pressure, memory bandwidth, turbo headroom, the scheduler. A benchmark run alongside thirty others measures contention, not the solver. |

A balance figure taken linearly is the same figure. A cost figure taken in parallel is a different
one, and there is no way to tell from the number itself.

**The run is the unit of parallelism, not the ship.** Every simulation built from one ship shares
that ship's `BlockInstance` objects and the load is written onto them, so two scenarios on one ship
at once would overwrite each other — which does not throw and does not look wrong in a report. Each
job reads the blueprint again for grid state of its own; block *models* stay cached and shared, so
only the per-block instances are rebuilt, at a fraction of the settling run it frees. Without that,
a panel of six ships would use six cores of however many the machine has.

Measured on a 32-core machine, 3 specimens through 20 scenarios:

| | Wall clock | Per run |
| --- | --- | --- |
| linear | 545.3 s | 9.09 s |
| parallel | **118.7 s** | 1.98 s |

**The two matrices were identical to the last digit.** `ParallelAndLinearProduceTheSameMatrix` is
the standing cheap version of that comparison, so a change reintroducing shared state fails in the
suite rather than in a report nobody re-runs linearly. Results keep the order of their inputs in
both modes, so two runs can be diffed.

Two other things made it faster without touching accuracy: runs stop on equilibrium rather than on
the clock — most are flat long before their ceiling — and the definition caches are built once
before the workers start rather than by whichever arrives first. Screening 32 ships is now 0.1 s.

## A block with no exit is the lab's own failure mode

A block with no exposed face and no conduction has nowhere at all to send its heat. It climbs until
the run stops — no exception, no NaN, no warning, just a number nobody has a prior for. It is the
single most misleading result this lab can produce, and every instance of it so far has been the
harness reading a definition wrongly rather than the mod being wrong. That is `D1`, and it is why
`ScreeningTests` carries the standing form of each:

| What a definition says | What it does not mean |
| --- | --- |
| a `ForceMagnitude` element | thrust in newtons — on a gyro it is torque in newton-metres, and reading it as thrust turned one gyro into 33.6 MW and a hull into 342,510 K |
| no `MountPoints` | a block that mounts nowhere — six per cent of the game's definitions leave them out and let the game derive them from model geometry, and an undeclared set means every face mounts |
| a rated input *and* a rated output | both at once — a store covers a shortfall, and counting it as a co-generator drawing its full rating at the same time doubles a ship's load |
| a block that does not seal | a block with no mounts — sealing and mounting are separate properties, and filling the mount bits from a definition that declared none zeroes them |

`hotspot` is the tool for asking why a block is the hottest thing on a ship: it dumps the top of the
distribution with the three figures that decide where each landed — what it generates, what it can
radiate through its own faces, and what it can conduct into its neighbours. A large generation
against a small conductance and no exposure is a *layout* result; a generation with **no** exit at
all is a defect.

**Sealed blocks are bounded rather than asserted to zero, and the bound is a share for a reason.**
Over the 2026-08-21 corpus: **1,184 blocks across 331 ships of 45.2 million**, which is 0.0026 %.
`CorpusSurvey` holds the share so a regression to the earlier scale fails while a handful of ships
do not hold the suite hostage.

> **This page said twenty-four, all on one ship, of 1.15 million blocks until 2026-08-24.** That
> was a figure from a sample carried forward as a figure about the population — the share was right
> to within a rounding, which is why nobody caught it, and every other number in the sentence was
> wrong by one to two orders of magnitude (`E2`).

**What they are, measured with `bench sealed`, and there are two kinds.**

The larger kind is **not armour**: it is `LargeBlockGyro`, 307 of the 330 on the seven worst ships.
The game's own definition declares exactly one mount point, `Bottom`, so a gyro whose bottom face
looks at empty space and whose other five are buried has no joint to conduct through and no face
onto the outside to radiate from. That is the model's own rule — *blocks that touch without mount
surfaces on both sides conduct nothing*, [thermal-model.md](thermal-model.md) — working exactly as
written, on a block that **makes heat**. It is a consequence of the design rather than a fault in
it, and it is what the rule costs.

The smaller kind is the one this page called impossible, and it is not. An armour cube standing
alone inside an **interior void** mounts on all six faces and has no neighbour on any of them —
and none of those six neighbouring cells is *external*, so `SurfaceMap.GetExposedFaces` counts no
exposed face, by the definition it is written to. The synthetic grid of two disconnected blocks
cannot reproduce it because two blocks in open space have external neighbours; what the case needs
is an enclosing hull, and the corpus has hulls.

> **And the count is a property of the scenario as well as of the ship.** Air is a block's third
> exit — `BuildRoomLinks` gives every node with a face onto a room a link to that room's air — but
> a lab hull is built unpressurised, so `bench sealed` reports *0 of 623 rooms hold air* and the
> third exit is not there to be had. The same blocks in a pressurised compartment are not sealed.
> The test counts air where there is any; on the corpus battery there is none.

## Open questions

* **The fetcher has never run against a real key.** Its failure paths are checked — a missing key,
  a rejected key and a missing account name all report and stop cleanly — but whether the query
  parameters return what is wanted can only be settled by a run.
* **Does the panel reproduce the corpus?** The claim in [Specimens](#specimens) is a hypothesis:
  that two ships close together in feature space behave the same way under the battery. Settling it
  needs one full battery run over a whole corpus and a comparison against the panel's verdicts —
  which is the expensive thing the panel exists to avoid, so it is paid once.
* **Whether `Census` should be replaced or kept beside the corpus.** Its tiers are a hypothesis the
  corpus can now test; if they hold, that is worth knowing, and if they do not, every scale figure
  taken on them wants re-reading.
* ~~One unexplained ship in the sealed-block bound~~ — **settled 2026-08-24**, and it was neither
  one ship nor unexplained: 1,184 blocks over 331 ships, mostly gyros bolting to nothing because
  the game gives a gyro one mount face. See the sealed-block paragraph above.
* **The 25-block size floor is an unexamined constant.** It has never been varied to see whether it
  changes a population figure.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Corrected the scope of historical K5 self-lifting and ceiling results: the census used nominal ForceMagnitude without atmospheric effectiveness. Preserved the figures as a nominal-rating calculation; they do not validate current sea-level P1/P2 (E10). |
| 2026-08-31 | **The drag milestone's criterion has a scorer, and it was proven against the hand computation before it was used.** [`dragfit.py`](../tools/corpus/dragfit.py) reproduces 14.06 % / 55.7 m/s at `C_d` 1 and 3.13 % / 78.8 m/s at 0.5 over the same 5,649 self-lifting hulls — to the digit. Re-scored with the shape term on, **`C_d` 1.54 is calibration-neutral**: 3.42 % and 78.2 m/s, and the cruise medians reproduce exactly. The two criteria were never fitted to each other and land on the same number. What moves is *which* ships: 69.6 % overlap, the rescued reading a median shape factor of 0.243 and the condemned 0.438. |
| 2026-08-31 | **The 2026-08-30 cruise figures needed a second correction, and this one was a tool default rather than a formula.** `cruise.py` scored at `C_d` 1.0 — its own default — while the mod ships 0.5, so the corrected column published under `K12` was itself twice the drag the mod applies. Median sea-level cruise is **199.2 m/s**, not 140.9; **7.6 %** of ships fall inside RTS's authored band, not 19.0; **11.6 %** are drag-limited below the engine cap, not 22.4. Pinned now, and guarded. |
| 2026-08-28 | **The air re-take finished and its cost-half prediction turned out to be unscoreable.** 8,137 ships, 32,548 rows, whole. The demand half did not move to three decimals (p99 34.779, p95 30.195, max 36.614, all identical to the cap walk's control arm), and `G1` in air tripled — 0.0246 % to 0.0737 % — which is `A13`'s 15.76 % of extra heat arriving where heat goes and not where cost does. The cost half was predicted against **5,812,731**, a figure withdrawn four days earlier for omitting the link term, and `out/air-corpus-2026-08-24` **has no `links` column**, so it cannot be re-scored to recover a corrected baseline: the comparison the prediction named never existed (`P2`). Scored against the cap walk's control arm instead, which carries the column, work p99 falls **1.30 %** to 7,199,204 where closing the gap needs 21 % — so the verdict row holds, `G6`'s cost half fails at **1.80×** the allowance on 712 of 32,548 runs. **A prediction registered against a number in a paragraph is not registered**; a baseline names a committed summary (`E5`). |
| 2026-08-28 | **Registered what re-taking the corpus in air will move, before it is re-taken**, and corrected the survey's basis line where it was published (`E10`). That line rested the dataset's integrity on *identical hashes across all four* slices; the fifth carried `C36`, `C42` and `C43` and moved `Cubes.xml` and `Loops.xml`, so it was true when written and false when the walk finished. It is still one dataset for the reason the line already gave, now **measured**: 207 distinct block types in the census and not one of them a block this mod adds. |
| 2026-08-25 | **Re-took the census, and `A13` is worth twice what the sample said.** 8,137 ships in 2 m 1 s, run twice and reproducing on every column. Summed over the rows a base variant writes, the eleven block kinds the reader built as armour carry **15.76 %** of what the population makes without them, against the 400-ship sample's 8.89 % — the sample carried a representative share of blocks and an unrepresentative share of gravity generators, and a gravity generator is 6.9 GW on its own. **Two of five predictions fail and one of the failures is the instrument**: a census-to-census diff spans four days and four definition changes, so its 41.14 % is the sum of the drives' 0.15 → 0.2, the computer fractions' 0.9 → 1.0, the oxygen generator's 0.6 → 0.40 and `C24`'s clock, and cannot isolate a reader fix (`P6`). The ship count moved by four — seven hulls rejected for holding an empty-subtype door or gravity generator on a small grid, which the game has only on large, and three admitted that the type-and-subtype pair now resolves — and both directions are the fix working (`J3`). The reach prediction is not scorable from a census at all: `composition.csv` holds heat-making blocks only, and nine of the eleven make none (`P2`). |
| 2026-08-25 | **Every dataset this page reports on was taken through a reader that built eleven kinds of vanilla block as armour** ([backlog.md](backlog.md) `A13`). The game gives thirteen definitions no `SubtypeId`; the blueprint reader turned all of them into a plain armour cube, so every oxygen generator, air vent, oxygen tank, gravity generator, door, hangar door, passage, ladder and large turret in every walk drew no power and made no heat. The reader is fixed and nothing here has been re-measured. Priced by parse on a 400-ship stride sample: **275 of 400 ships** change, and the sample's full-load waste rises **8.89 %** on this page's own basis — full electrical, no thrust, stores in reserve — and **no ship changes side on the vanilla filter**, so the fix moves what a walk measures rather than which hulls it measures. Until a re-run, **a figure on this page about anything but armour is a lower bound**.
*The census half of that re-run happened the same day and is the entry above; the stepped walks
have not.* |
| 2026-08-25 | **Measured `C30`'s comparison: a shortened step against a floored one, on one hull at one allowance.** At the shipped 4,000,000 a 64,463-block hull in flight keeps **36.2 % of real time** — a slow clock standing at **36.98 K**, past the last rung the price ladder measured — and a cap of 6 takes its demand from 27.49 to 6.00, inside the granted 10, so it keeps **100 %**. Against the cap's own **0.024 K**. Three orders of magnitude, and not a close call. |
| 2026-08-25 | **Decided `C3`: `MaxSubstepsPerBlock` stays 0.** The p99 landed in the judgement band at 0.2820 K, and the rule reserved that band on the condition it would not be decided by the cap rescuing a criterion — it does rescue one, and that is set aside. What decides it is who pays: the error is per block and the benefit is per grid, so four fifths of the population is charged in full and collects nothing. `G6`'s cost half therefore stays failing, and its route is named rather than left open — a cap that engages when a grid is over its allowance, which is `C30`. |
| 2026-08-25 | **`CorpusCapWalk` finished: 8,144 blueprints, four scenarios, two arms, 3 h 51 m, and three of its four registered predictions hold.** The identity is exact on all 32,576 pairs, the benefit lands at **2,180,352** inside the 1.7–2.9 M projected and takes `G6`'s cost half from 1.82× the allowance to 0.55×, and the reach is **5.83 %** inside the 3–10 % band. The cost prediction fails on its second half: p99 **0.2820 K** as predicted, max **48.22 K** against the 10 predicted, on giants and not on censored runs. **The finding nobody predicted is the shape of the trade**: stiffness is per block and the allowance is per grid, so the cap re-masses 7.4 % of the blocks on the smallest hulls and 3.1 % on the largest while no hull under 5,000 blocks is over the allowance at all — 80 % of the corpus pays in full and collects nothing. |
| 2026-08-25 | **Recorded a limit of the paired design while the walk was still running and the verdict was not known.** Both arms run to the same clock, which stops the stopping rule being part of the difference and does not make that clock an equilibrium: the settle test tolerates 0.25 K a minute, which over an 1,800 s scenario is 7.5 K, and a cap changes the *rate* a hull approaches its answer at. On the walk's first 360 ships the pairs more than a kelvin apart have a control still moving at a median 0.03 K/s against a population median of 0.001. `cap.py` prints the deltas split on whether the control had stopped moving, **beside** the registered statistic and not instead of it — the decision rule is scored on what it was written against (`E11`). |
| 2026-08-25 | **The 2026-08-24 air walk predates `C21`'s waste correction by one minute, and `reproduce.py` found it by comparing the walk with the cap walk's control arm.** `f91e5dc` took twenty-seven `ConsumerWasteEnergy` fractions from 0.9 to 1.0 at 21:06; the walk finished at 21:07 and had loaded the old ones. Over 1,236 shared runs the **substep demand is identical to the last bit** — demand is conductance over capacity and has no waste term — while generation moved +0.60 % at the median and +11.0 % at worst, and peaks 0 % at the median and 2.1 % at worst. The demand figures stand; the watt and peak ones are low by that much and are marked rather than withdrawn (`E10`). |
| 2026-08-25 | **The cap walk's restart reproduced the abandoned run exactly**, which is the return on having restarted rather than resumed: 552 shared rows, 22 columns, worst relative difference zero. Three commits had touched the solver and the harness in between and none had been checked for behaviour; they were inert, and that is now measured rather than assumed (`E7`). [reproduce.py](../tools/corpus/reproduce.py) is the check, with `test_reproduce.py` pinning that a comparison with nothing in common is not a reproduction. |
| 2026-08-25 | **The estimate that abandoned `CorpusCapWalk` was checked against a walk whose answer is known, and it fails the check.** The walk was stopped at 45 minutes on *past ten hours*, projected from the share of the population's blocks covered over the rate they were being covered at. The corpus is walked largest first, so that rate falls throughout every healthy run, and a progress mark is ten files — one capital hull or ten fighters. Run over `CorpusAirWalk`, which finished in **104 minutes**, the same estimator projects **104 to 428 minutes, median 154** over that walk's own first 35 (`P4`). The estimate that survives is the ratio: a second arm sharing one parse is **2x by construction** and **1.95x** measured over the fifty files the two walks share, so the walk costs **3.4 hours**. [pace.py](../tools/corpus/pace.py) is that arithmetic, with `test_pace.py` pinning it; no cheaper design was built, and the paragraph above says why. The walk was restarted rather than resumed, because three commits touched the solver and the harness in between (`M1`). |
| 2026-08-24 | **One definition of a percentile, where there were two.** `air.py` interpolated between the two ranks a quantile falls between and `verdict.py` took `values[int(q × n)]`, and these pages print the two side by side — a corpus p99 against a panel p99. On forty thousand samples they agree to a third of a per cent, which is why nobody noticed; on **forty** they do not agree at all, because `int(0.99 × 40)` is 39 and the fortieth of forty is the maximum. A p99 that is the largest reading in the set is not a percentile, and a forty-hull panel is a set this repository scores. The interpolating one survives, because every published panel figure was computed with it; the corpus figures move by up to 0.3 % and are re-quoted (`P5`, `P3`). |
| 2026-08-24 | **Settled the sealed-block anomaly, and corrected a sample figure that had been standing as a population one.** This page said twenty-four sealed blocks, all on `UNSC Panama`, of 1.15 million — the 2026-08-21 corpus says **1,184 across 331 ships of 45.2 million**, and the *share* was right to a rounding, which is why nobody caught the rest (`E2`). Measured with `bench sealed`, there are two kinds and neither is impossible: `LargeBlockGyro`, 307 of the 330 on the seven worst ships, whose game definition declares one mount point so a gyro with its bottom face against empty space bolts to nothing while being buried — the model's *touch without mounts conducts nothing* rule working, on a block that makes heat; and an armour cube alone in an interior void, whose six free faces all look into cells that are not external and so count as unexposed by definition. The synthetic two-block grid could never reproduce the second because two blocks in open space have external neighbours. Also: air is a third exit and the sealed test never looked at it, so `HotSpotLab.IsSealed` is now one definition shared with `CorpusSurvey` and the report prints how many rooms hold air — on an unpressurised lab hull, none. |
| 2026-08-24 | **`CorpusCapWalk` was launched, ran 69 of 8,144 blueprints and was stopped, and the finding is the walk's own cost.** At 45 minutes it had covered 10 % of the corpus's blocks with the rate *falling* — 0.14 % a minute over the last interval against 0.24 % averaged from the start — which extrapolates past **ten hours** rather than the 3.5 the design note projected from `F11`'s wall-clock. The projection was wrong because it was taken on a 40-ship stride sample, and a stride sample is exactly where the per-*ship* overhead hides: pairing costs about twice an unpaired walk only while ships are large, and once the giants are cleared this walk pays a fixed per-ship cost 8,144 times over eight runs each where `F11` paid it over four. The resume record is kept (`O3`) and nothing is scored: **a partial sweep is not a result** (`E4`), and resuming is now a decision rather than a default. |
| 2026-08-24 | **Wrote down what a per-block cap is expected to do to the population, before building the walk that measures it** (`E1`, `E11`). `C3` — whether `MaxSubstepsPerBlock 6` ships — and `G6`'s failing cost half are one question, because the cap is the only lever that lowers a step's work and `C3` is undecided only because its cost has been measured on one hull. Four predictions with their falsifiers, and a decision rule fixed against the two calibration points this repository already has for *imperceptible*: 0.028 K accepted, 0.607 K refused. Also recorded, before the data: raising the allowance is the other lever and is the wrong one, because it moves the work into the frame rather than making a ship affordable, and a criterion that passes by being re-pointed at a bound chosen to let it pass has not passed. The design's one hard requirement is that both arms run to the **same simulated clock** — the effect is a hundredth of a kelvin and `Battery.Run`'s settle tolerance is a quarter of one, so two settle-stopped arms would measure the stopping rule (`M1`, `P6`). |
| 2026-08-24 | **The link count in `G6`'s cost half was the *joint* count, so every step-work figure this repository has published is the node half of the unit alone.** The unit is `links + 4 × nodes`; the corpus carried no link column and `verdict.py` was handed `joints`, which counts rotors and pistons *between grids* and is nought on almost every blueprint — so the expression evaluated to `4 × nodes`. On a 2,000-block census hull that is **1.51× low**, at 2.06 links a block; on any other hull it is low by that hull's own ratio, so no factor is applied and the walk records `ThermalSimulation.SubstepCost` per run instead. **No verdict moves**: the air walk's p99 is really 5.8–10.2 M against a 4,000,000 allowance and was already failing, and the vacuum survey's is 0.88–1.54 M and still holds. The figures cannot be recovered from the datasets that produced them, so `verdict.py` reports the cost half as *unmeasured* on any walk without the column rather than reprinting the old arithmetic (`E8`, `P2`), and `G6` reads as one half unscored rather than as a failure. `StepWorkUnitTests` pins the harness side and `number()` now reads a missing column as absent rather than crashing (`C8`). |
| 2026-08-24 | **Re-scored `G6`'s cost half in the currency the allowance is spent in, and against the bound that ships after `C27`.** The work was measured with `2.125 × nodes + links`, which is `SubstepWork` — the unit a step is cut into frame-sized *slices* in — and compared against a bound denominated in `links + 4 × nodes`, which is what a step's length is divided by when the allowance decides whether to shorten it. Two currencies, 1.45× apart on a census hull, either side of one comparison; and the substep column was granted where the allowance reads demanded. Re-scored: p50 10,909, p95 269,153, **p99 881,279**, 29 runs past the 4,000,000 across 8 ships from 159,449 blocks up, where it read p99 446,707 and fifteen runs across three ships against 2,000,000. **The verdict is unchanged and the restriction under it is now stated where the figure is**: five vacuum scenarios, which is exactly what let this criterion's *demand* half read as passing for months, and projecting into air puts the corpus p99 level with the bound. |
| 2026-08-24 | **Wrote down `G6`'s cost half, before scoring anything against it** (`E11`). The criterion has always said *substep demand and step cost* and only the demand had ever been produced. The cost is stated as **work** rather than as time — `substeps × (2.125 × nodes + links)`, the solver's own charge, derivable from every corpus walk already taken — and the bound is `MaxElementVisitsPerStep` at 2,000,000, which is the mod's own statement of what a grid's step may cost and the point past which a grid's simulated time runs slower than real time. *(Both figures moved later the same day: the unit to `links + 4 × nodes`, which is the one the allowance is spent in, and the bound to 4,000,000 — see the rows below.)* It fails at p99, the same percentile and shape as the demand half. [backlog.md](backlog.md) `C23`. |
| 2026-08-24 | Two notes on `G6`, neither of which moves it (`P3`). Its marker is a fidelity marker rather than a cost one — a demand above the cap is the cap bounding cost, and the shipped breach *buys* 1.15× of the step for 0.028 K. And the step-cost half of its own sentence has never been produced, because the corpus carries no timing column; that gap is now `C23` ([backlog.md](backlog.md) `C19`). |
| 2026-08-22 | Removed *subgrids are read as separate ships* from the open questions. It was not true and had not been for as long as `ShipAssembly` existed: a blueprint's grids are built as one machine and bridged at their mechanical joints. 747 of the first 1,002 ships of the 2026-08-22 sweep hold more than one grid and 695 resolved joints, 29,604 of them. What was genuinely missing is that nothing checked a bridge *moves heat* — `CorpusSurvey` counted them — and `SubgridBridgeTests` does. |
| 2026-08-23 | **`G8` is measured and it holds**, at conductivity ×4 with `HeatTimeScale` 80–120 — four of twenty-five cells, `G1`, `G2` and `G5` all kept, 1.36–2.04× the shipped substep demand. **The criterion caught a defect in its own scorer first**: the crossing median was taken over the hulls that crossed rather than over the hulls that were loaded, which reported two conductivity ×8 cells as satisfying `G8` on a population where 27 of 40 hulls never reach critical. Censored above as `E9` requires, ×8 has no median at any clock. Nothing in `G8` moved. |
| 2026-08-23 | **Corrected `G8`'s second half in the open, before scoring anything against it** (`E11`). It asked for a settling time *at idle*, and idle has no equilibrium in vacuum shadow — the hull cools toward the floor — so at low `HeatTimeScale` the figure reads 120 s, its own floor, for 26 to 36 of 40 hulls. The old wording is above; it is replaced by the recovery time, which is a real duration and the one a player waits through. **The correction is stricter**: four cells passed the old half and none passes this one. |
| 2026-08-23 | Added `G8`, the significance window, **before the sweep that tests it** (`E11`): under sustained full electrical load the median crossing falls in 120–300 s, and at idle the median hull settles inside an hour. The mod's own balance target had never been a scored criterion, which is [backlog.md](backlog.md) `C12`. Both halves are one criterion because one clock governs both time constants, and the five decisions inside the wording are written out beside it. |
| 2026-08-22 | Added `G7`, the compatibility floor, **before measuring it** (`E11`): every vanilla prefab, idle, in the environment its category spawns into, for five simulated minutes, loses no block. The stated intent that a ship the game spawns must survive arrival had never been a scored criterion and had never been measured, which is [backlog.md](backlog.md) `C10`. The four decisions inside the wording are written out beside it, because a criterion whose terms are settled after the data is not one. |
| 2026-08-22 | Said that `BlueprintTests` is synthetic throughout. The real-ship case it used to end on was demoted to an uncalled helper when `CorpusSurvey` absorbed it, and has now been deleted ([backlog.md](backlog.md) `H4`); the claim is `CorpusSurvey`'s step probe, over every ship rather than one. |
| 2026-08-22 | Finished the split this page began: the three sections still narrating what an early run found are gone. *The first full cycle* reported 32 subscribed ships as a provisional read of G1, G2 and G5, which the 8,142-hull survey in [balance.md](balance.md#the-population) has since answered over a population — quoting the small run beside the large one is `E4` in slow motion. *Where the numbers stand* was the same 32-ship run on one hull. *Sealed blocks: three harness faults* narrated three defects that are fixed and pinned; what survives is the standing hazard, restated as what a definition does **not** mean, which is the form `D1` and `ScreeningTests` hold it in. Three struck-through entries left *Open questions*, and *What exists now* stopped quoting a 32-ship yield as the corpus. |
| 2026-08-22 | Moved the readings of the corpus datasets to [balance.md](balance.md), so this page is the lab's design and that one is what the lab found. Added the standard header and this change log. |
| 2026-08-22 | Answered G3 by fitting cooling to ships people actually built, and said which half of the cooling criterion the ladder answers and which it does not. |
| 2026-08-21 | Measured the speed limit the mod is actually played at. |
| 2026-08-20 | Isolated wind from velocity and composed them where the game does. Measured a ship's stiffness in the world it flies in. Opened the lab: the criteria written down before any data was collected, the staging from cheap screening to the full battery, and the corpus acquisition. |
