# Stiffness is the cost

Why a handful of light fittings sets the simulation cost of a capital ship, what that costs across
eight thousand real hulls, and what can be done about it. This page is about the **steady** cost,
which is the only thing left once the spikes are proportional to what changed.

> The rules argued here are stated canonically in [rules.md](rules.md): `E2` `E3` `E6` `M11` `D1`.

| Looking for | Go to |
| --- | --- |
| What made the spikes proportional, and what a live world costs | [load-and-hitching.md](load-and-hitching.md) |
| The machinery this page argues is needed | [scale-design.md](scale-design.md) |
| The repeatable cost report | [benchmarks.md](benchmarks.md) |
| The settings that cap it | [configuration.md](configuration.md#maxsubstepsperblock) |

---

## The dump

`ThermalDynamics_Thermodynamics/Thermodynamics_Telemetry_20260818_222511.log`, a 199-grid save
with three 42,051-block capital ships in it, running in vacuum with every mechanism switched on.

| | |
| --- | --- |
| grids | 199, of which 3 are 42k blocks and 196 average 220 |
| mean frame, all grids | **18.4 ms** |
| frames over a 60 fps budget | **41 %** |
| worst frame | 275 ms |
| solver share of a grid's update | **92 %** |
| the three capital ships' share of all simulation cost | **81 %** |
| their simulation rate | **35.6 %** — 72.7 s of simulated time not advanced |
| their substeps per step | 11.00, sd **0.00**, 0 clamped |

Everything the previous round of work fixed stayed fixed: topology rebuild is 338 ms across the
whole session, room mapping 289 ms, exposure 97 ms. Against 67.7 s of solver. **There is nothing
left to fix that is not arithmetic**, and the arithmetic is being throttled: a substep count with
zero variance is `MaxElementVisitsPerStep` holding it, and a 35 % simulation rate is the price.

So the question is not "why is the solver slow". It is **why does this ship need thirty substeps
for a quarter-second step**.

---

## Why none of the settings that existed could help

A step of `dt` simulated seconds needs `dt · r_max / safety` substeps, where `r_max` is the
largest `ΣG / C` on the grid. A substep costs one pass over the nodes and links. So

```
work per real second  ∝  (simulated seconds per real second) × r_max
```

Only enabled exchange mechanisms contribute to `ΣG`: block conduction, coolant and room air
are gated independently. Turning block conduction off therefore removes its substep demand and
mass floor while preserving enabled coolant or room coupling. Independent heat sources retain
their real heat capacity when no active exchange requires a floor. Settings changes invalidate the
cached conductance totals; disabling the floor also restores capacities already raised by it.

Both settings that look like they control cost cancel out of that:

* **`Frequency`** halves the step and doubles the number of steps, so it cancels out of the
  expression above entirely. It only bites at the two ends: a grid soft enough to need less than
  one substep is charged a whole one anyway, so there `Frequency` is the cost outright — and a
  grid pinned at `MaxSubsteps` is being refused, so raising it buys accuracy. Neither end is where
  these ships are.
* **`HeatTimeScale`** divides every block's heat capacity — `ThermalMass = specificHeat · mass /
  heatTimeScale` — so halving it halves `r_max` and halves the heat advanced per real second in
  exactly the same proportion. No change.
* **`MaxElementVisitsPerStep`** shortens the step when it will not fit, which is what these ships are
  living on. It buys smoothness, not throughput: the same work per unit of heat time, delivered
  more evenly and therefore more slowly. **What *more slowly* costs is measured**: the 35 %
  simulation rate above is a thermal clock 65 % slow, past the end of the ladder that priced it,
  whose last point is **36.98 K** standing at 60 % under a moving load — and 0.00 K under a steady
  one ([benchmarks.md](benchmarks.md#what-the-allowance-is-worth)).

That is not a defect in the settings. It is what an explicit integrator is: **you pay for the
stiffest node, on every node, for as long as you want heat to move.** The only ways down are to
lower `r_max`, to stop applying it to everyone, or to stop being explicit. The rest of this page
is those three, and the first of them is now `MaxSubstepsPerBlock`.

---

## The stiffness is a thin tail, and that is the good news

Measured two ways, and they agree.

### The harness cannot reproduce it from a synthetic hull, which is itself a finding

`ΣG / C` per node on the standard ship shape the benchmarks use — heavy armour with one grating
fitting in eight:

| percentile | rate 1/s | τ | substeps a 0.25 s step needs |
| --- | ---: | ---: | ---: |
| min | 0.045 | 22 s | — |
| median | 0.18 | 5.5 s | 0.09 |
| p90 | 3.0 | 0.33 s | 1.5 |
| max | 4.5 | 0.22 s | **2.25** |

Two populations, exactly as the design predicted — 87.5 % of the grid at τ ≈ 5.5 s and the
fittings at τ ≈ 0.22 s — and **the whole ship asks for two substeps**. The field ship asks for
thirty-one. The benchmark ship is not stiff enough to have the problem, and every scale figure in
[load-and-hitching.md](load-and-hitching.md) was measured on it.

The reason is one number. The harness's lightest block is a 200 kg grating; the block the field
dump found setting the substep count is a **16 kg** light fitting. Twelve times the mass is twelve
times the heat capacity is a twelfth of the stiffness, and the synthetic ship has nothing on it
that a real ship has.

### The field ship, from its own block table

The same calculation over the block types the dump reports on `UNSC Infinity`, at large-grid
geometry:

| blocks | heat capacity | τ | substeps a 0.25 s step needs |
| ---: | ---: | ---: | ---: |
| 43 × `SmallLight` | 32 J/K | 17.8 ms | **28** |
| 1 × `LargeCameraBlock` | 81 J/K | 45 ms | 15 |
| 138 cumulative | ≥ 120 J/K | 67 ms | 7.5 |
| 225 cumulative | ≥ 232 J/K | 129 ms | 4 |
| 1,576 cumulative | ≥ 400 J/K | 222 ms | 2 |

The dump's own numbers say the ship asks for about 31 substeps for a full step — 11 substeps at
35.6 % of the step. The table says 28 from the lights alone, and the linearised radiation term
accounts for the rest. **The measurement and the arithmetic agree on the culprit.**

> The surface CSV is truncated at 300,000 rows, so this table is built from the subtypes it
> reached. It is a lower bound on the tail: there may be lighter blocks it did not list. It does
> not need to be complete to make the point, because the top of it already explains the whole
> substep count.

So: **forty-three sixteen-kilogram light fittings set the simulation rate of a forty-two-thousand
block capital ship.** Removing the top 225 nodes of 42,051 — half a percent — would take it from
31 substeps to 4.

### Reproducing it synthetically

`bench floor` builds its hull from the measured block [`Census`](../tests/Thermodynamics.Harness/Census.cs)
— the eight-band population of a real ship, read out of a dump's block-type table — and it landed
on **22.97 substeps** in vacuum and 43.9 in flight, against a field range of 21 to 31. Close
enough that the synthetic ship could be used to answer questions about the real one, which it
could not before.

**Both ends of that comparison have since moved and the hull is still inside the population.** The
field range is two ships measured at a pair that no longer ships and cannot be re-taken; the hull
asks **7.35 substeps in vacuum and 24.97 in thick air at 200 m/s** at the pair that does, on tiers
that now carry the mount points of the blocks they stand for (`C26`). What holds it in place is the
corpus rather than the dump: 12.71 substeps in still sea-level air against a population running 6.20
to 22.41, which `CensusFidelityTests` asserts.

> The first version of this reproduced the field by adding a single 16 kg fitting at one in a
> thousand. That got the substep count right and the *distribution* wrong — every cap touched the
> same forty-three blocks, where a real ship has a continuum of armour tips and panels between the
> fittings and the hull. The census replaced it, and the cap tables below moved a long way as a
> result: cap 2 reaches 21 % of a census hull against 2 % of the fitting hull.

---

## The field says it is not a trade at all

A clean single-ship dump of 19 August, `MaxSubstepsPerBlock` off, `MaxSubsteps 16`, a 1,293-block
ship with thrusters running:

```
substeps required   18.75 / 21.34 / 21.35
solver substeps     16.00 / 16.00 / 16.00 (sd 0.00)
steps clamped       1,867  of 1,867
set by              LargeBlockLight_1corner [X:9, Y:-3, Z:-2] (48 J/K), 100 % conduction
```

**Every step of that session was clamped.** The ship asks for 21.35 substeps, `MaxSubsteps` grants
16, and the remainder is absorbed by `ClampConductionOvershoot` — which keeps the answer bounded
but is, by the setting's own documentation, "approximate rather than wrong". The grid was at
100 % simulation rate, so nothing else was binding: this is the shipped default, on an ordinary
ship, approximating on every step and saying so only in a counter nobody reads.

Against that baseline the cap is not a trade:

| | substeps | clamped | cost per simulated second |
| --- | ---: | ---: | ---: |
| as shipped | 16 | **every step** | 100 % |
| `MaxSubstepsPerBlock 16` | 16 | **never** | 100 % |
| `MaxSubstepsPerBlock 8` | 8 | never | **38 %** |
| `MaxSubstepsPerBlock 4` | 4 | never | **19 %** |

Cap 16 costs nothing and removes the clamping, because it makes the demand fit the ceiling instead
of leaning on the clamp to survive being refused. Cap 8 removes the clamping *and* runs at 2.6
times the rate. There is no column in which the uncapped configuration wins.

That gives the pair a useful invariant worth stating plainly:

> **While `MaxSubstepsPerBlock <= MaxSubsteps`, the overshoot clamps never engage.** Every step is
> short enough for the grid it is integrating, which is what the clamps exist to fake.

That invariant is now load-bearing rather than an observation. The conduction clamp is a per-link
branch and two extra reads in the hottest loop in the mod, and it used to run whether or not it
could do anything: 4.17 ms of an 8.52 ms step on a 32,800-block hull. The solver now settles the
same stability test once per step — `h * G > C` for a node, `h * conductance >` the reduced mass
for a link, neither of which reads a temperature — and skips the clamped arithmetic on any step
where the answer is no. A resolved grid halves its step; a refused one pays one comparison to find
out. See [benchmarks.md](benchmarks.md#the-overshoot-clamp-ab).

Sixteen blocks of 1,381 are raised at cap 8. Two, at cap 16.

---

## What refusing the demand costs

`MaxSubsteps` is the other cap, and it is the one that ships bound. The per-block floor below is
off by default; the global ceiling is 64 and the 49-ship panel's p99 demand in thick air at 200 m/s
is **73.4**, so the shipped configuration asks for more than it is granted on about a fifth of a
real population. [balance.md](balance.md#air-is-where-the-substep-budget-goes-and-the-shipped-pair-does-not-fit-it)
records that as a `G6` failure; what nobody had measured is what the refusal actually does.

**What happens is not the floor below.** `ApplyThermalMassFloor` returns without raising capacity when
`MaxSubstepsPerBlock` is zero, which is what ships, so no capacity is raised and no block is
approximated on purpose. The step is simply integrated at the ceiling, with the two overshoot
clamps — `ClampConductionOvershoot` and `ClampEnvironmentOvershoot`, both on by default — bounding
every exchange it makes at the energy that brings the pair to equilibrium. The result is damped and
bounded rather than unstable.

**The quantity is the over-subscription, not the substep count.** A step demanding 72 and granted 62
and a step demanding 36 and granted 31 are the same question asked of the integrator, and they cost
the same to four significant figures — measured at two step lengths on the same hull, which is what
lets a rig answer for a population it is not a member of.

`bench ceiling --size 4000 --ticks 2400 --driven`, a 4,386-node census hull in thick air at 200 m/s
with its producers running, 600 simulated seconds, error against the run granted everything it asked
for:

| granted | over-subscribed | speed | peak error | worst block | rms |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 35 (its demand) | — | 1.00× | — | — | — |
| 30 | **1.15×** | 1.15× | **0.028 K** | 0.041 K | 0.004 K |
| 23 | 1.50× | 1.47× | 0.068 K | 0.099 K | 0.010 K |
| 17 | 2.03× | 1.99× | 0.101 K | 0.148 K | 0.016 K |
| 11 | 3.13× | 1.80× | 0.137 K | **5.116 K** | 0.084 K |
| 8 | 4.31× | 2.48× | 0.185 K | 12.012 K | 0.269 K |
| 4 | 8.61× | 4.49× | 2.062 K | 325.232 K | 25.719 K |

**Every figure in that table is a *block* figure, and blocks are the one element whose exchanges
are all pairwise.** The ladder is gentle because the overshoot clamps bound each exchange at the
energy that brings a pair to equilibrium — and a pairwise bound is the whole bound a block needs and
half the bound a lumped mass needs. A coolant parcel carries a link to every pipe on it and a room's
air a link to every surface bounding it, so their links together could take several times the energy
that equalises them; the node on the other end of a sink face is pulled on by the fluid *and* by
everything it is bolted to. Each bound held, and the node went past both. That was
[backlog.md](backlog.md) `A10`, and until it was fixed a refused step approximated on a block and
diverged on the two paths that carry heat in bulk.

**The other two ladders, measured.** `bench ceiling --fixture rings` builds the hull where the
plumbing sets the demand — a reactor under a ring's sink face, which asks nine substeps where the
same blocks unplumbed ask one — and runs it at 64 parcels a second, the regime where a ring stops
carrying and starts mixing. Two rings, 300 simulated seconds at `Frequency` 1, hottest block:

| granted | over-subscribed | before the fix | after |
| ---: | ---: | ---: | ---: |
| 39 (its demand) | — | 335.2 K | 335.2 K |
| 34 | 1.15× | 335.2 K | 335.2 K |
| 26 | 1.50× | 335.2 K | 335.2 K |
| 19 | 2.05× | 335.2 K | 335.2 K |
| 13 | 3.00× | 721.2 K | 636.6 K |
| 9 | 4.33× | 1,434.2 K | 888.0 K |
| 4 | 9.74× | **1.3 × 10²⁵ K** | 1,799.0 K |

Room air is the same defect and had never had a measurement on it: a thin compartment at 2 %
pressure, walls hot and air cold, refused one substep of the thirty it asks for, reached **3,839 K**
of spread on a hull that started 300 K apart with nothing in it making heat. After the fix it stays
inside the 300 K it started at, which is the whole of what a bounded integrator promises.

**What bounds them is the per-node relaxation the conduction pass already used.** Every exchange at
a node is scaled so that the ones arriving together cannot exceed the energy that equalises it, which
makes the substep a convex combination of the temperatures pulling on that node — and a convex
combination cannot leave the range they span, whatever the substep length is. The same factor is
taken on the parcel and on the room, and one exchange takes the stricter of the two ends, so what
leaves the fluid still enters the block. **It is inert while the demand is granted**: the factor is
`mass / (h × conductance)` held at one and the substep estimate is that same ratio with the safety
factor in it, so a granted step is one where every factor is one by construction. The block table
above is unchanged by it to three decimal places, which is the check that says so.

**Nothing shipped ever reached the cliff** — 64 substeps granted against a plumbed hull's nine — so
this is a bound the defaults did not need and a world that lowers `MaxSubsteps` or raises
`Frequency` did.

**Three things to read out of it.**

**The approximation is free to about twice over-subscribed**, and the knee is between 2× and 3×:
the worst block goes from a seventh of a kelvin to five kelvin across that one rung, while the
hottest block — the number overheat damage is taken off — barely moves until 9×.

**The shipped breach is the first rung.** Every hull over the cap in either population sits between
1.14× and 1.15×: 14 of 50 on the panel and 8 of 40 on the retest set, and the maximum in both is
73.4 against 64. That is not a tail, it is a ceiling — the stiffest block class is the same fitting
on every ship and its demand in thick air at 200 m/s is set by the convection coefficient rather
than by the hull. So the population is not spread across this table; it is all on one row of it.

**The saving is proportional and the error is not.** Refusing 1.15× of the demand buys 1.15× of the
step for 0.03 K on the hottest block. Granting it instead — a ceiling of 128 — costs that 15 % back,
in thick air at flying speed only, since the same hull demands 7 substeps in vacuum and the ceiling
never binds there.

Measured over 100 simulated seconds the same rung reads 0.003 K rather than 0.028 K, so the error
grows with the run and grows slowly; it is a lag rather than a divergence, which is what the clamps
being live predicts. `SubstepCeilingTests` pins the first rung, the ninth, and the claim that the
ratio rather than the count is what sets the error.

## What to do about it

Four routes, cheapest first. The first is built and measured; the other three are re-ranked
against what it turned out to be worth, which is most of what was on offer. That is the thin tail
paying off: when half a percent of the blocks are the problem, the cheapest thing that reaches
exactly those blocks collects nearly the whole prize, and the general machinery has to justify
itself against a much smaller remainder than it expected to.

### 1. A per-block substep cap — the one that is built

`MaxSubstepsPerBlock` is the setting. Zero, the default, leaves every block's heat capacity alone
and the solver is bit-identical to what it was. Set to N, the mirrored heat capacity of any node
whose conduction would demand more than N substeps is raised to the least that keeps it inside:

```
C_min(node) = ΣG(node) · StepSeconds / (safety · N)
```

What that says physically is that **a node whose time constant is far below the step is not an
independent node**. A 32 J/K light bolted to a 500 kg armour block reaches its temperature in
eighteen milliseconds; at a quarter-second step it is a reading off the block it is bolted to, and
resolving its approach to that reading is arithmetic nobody can observe.

`ΣG` is everything the stability estimate reads: the conduction graph, the coolant loops and room
air a block is bolted to, and the linearised radiation and convection it exchanges with the sky.
It has to be the same rate, or the cap would bound a number nobody is looking at — a block on a
windy planet can be stiffer through convection than through six faces of armour, and a
conduction-only floor would leave it setting the substep count.

Only the solver's mirrored row moves. `ThermalNode.ThermalMass` keeps the block's real capacity,
so the terminal readout, the energy figure and the mod API all still describe the block rather
than the approximation used to integrate it.

**What the cap cannot reach** is room air and the coolant loops themselves, which are elements in
the estimate but are not blocks. On the field ship the stiffest room air asks for about two
substeps against the lights' twenty-eight, so it does not bind — but a ship with one tiny
pressurised cupboard could be different, and the telemetry now reports both so it stops being a
guess.

**What it buys and what it costs**, from `bench floor --size 42000` — a 43,232-node ship with the
field's proportion of light fittings, temperatures spread 250–750 K, error measured against the
uncapped run.

**Read the step length first.** A step of `dt` needs `dt · r_max / safety` substeps, so the demand
— and with it which blocks a given cap reaches and what that cap is worth — is *proportional to
the step length*. There is no single table here; there is one per step length. **The shipped
`Frequency` is 4**, so the second table is the one a default world reads; the first is kept because
raising the rate is a legitimate way out of a budget and this is what it costs.

At **Frequency 8**, an eighth-second step:

| cap | substeps | ms | speed | blocks raised of 43,232 | worst error | rms error |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| off | 11.48 | 696 | 1.0x | 0 | — | — |
| 32 | 11.48 | 689 | 1.0x | 0 | 0 | 0 |
| 16 | 11.48 | 680 | 1.0x | 0 | 0 | 0 |
| 8 | 8.00 | 508 | 1.4x | 172 (0.4 %) | 0.056 K | 0.016 K |
| **6** | 6.00 | 436 | **1.6x** | 459 (1.1 %) | 0.113 K | 0.033 K |
| 4 | 4.00 | 327 | **2.1x** | 461 (1.1 %) | 0.225 K | 0.067 K |
| 3 | 3.00 | 285 | 2.4x | 1,450 (3.4 %) | 0.345 K | 0.095 K |
| 2 | 2.00 | 235 | 3.0x | 3,648 (8.4 %) | 0.606 K | 0.152 K |
| 1 | 1.00 | 191 | 3.6x | 9,283 (21.5 %) | 1.887 K | 0.449 K |

At the shipped **Frequency 4**, a quarter-second step, which asks twice as much of the integrator.
**Re-measured 2026-08-24** at `C24`'s pair on the census hull `C26` refreshed — 41,119 nodes, and
the table it replaces is below it:

| cap | substeps | ms | speed | blocks raised of 41,119 | worst error | rms error |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| off | 7.35 | 386 | 1.0x | 0 | — | — |
| 32 | 7.35 | 406 | 1.0x | 0 | 0 | 0 |
| 16 | 7.35 | 390 | 1.0x | 0 | 0 | 0 |
| 8 | 7.35 | 392 | 1.0x | 0 | 0 | 0 |
| **6** | 6.00 | 369 | 1.0x | 1,422 (3.5 %) | 0.030 K | 0.010 K |
| 4 | 4.00 | 290 | 1.3x | 4,818 (11.7 %) | 1.375 K | 0.048 K |
| 3 | 3.00 | 244 | 1.6x | 9,046 (22.0 %) | 4.276 K | 0.140 K |
| 2 | 2.00 | 203 | 1.9x | 12,055 (29.3 %) | 13.034 K | 0.451 K |
| 1 | 1.00 | 173 | 2.2x | 22,791 (55.4 %) | 43.798 K | 1.763 K |

**A cap of 8 is inert in vacuum now, where it bought 2.3× before.** The hull demands 7.35 substeps
where it demanded 22.97 — `C26` took the six joints off its light fittings and `C24` put 1.6× back
— so nothing above six binds at all, and what a cap buys in vacuum has collapsed with it. The
figures at the pair and hull that produced the original table:

| cap | substeps | ms | speed | blocks raised of 43,232 | worst error | rms error |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| off | 22.97 | 627 | 1.0x | 0 | — | — |
| 16 | 16.00 | 452 | 1.4x | 172 (0.4 %) | 0.052 K | 0.015 K |
| 8 | 8.00 | 269 | **2.3x** | 461 (1.1 %) | 0.221 K | 0.066 K |
| **6** | 6.00 | 223 | **2.8x** | 1,450 (3.4 %) | 0.341 K | 0.094 K |
| 1 | 1.00 | 108 | 5.8x | 14,138 (32.7 %) | 5.915 K | 1.518 K |

**And the population says the tables above are the wrong hull to decide on.** Every figure here is
one hull; `CorpusCapWalk` put a cap of 6 against all 8,144 published blueprints in four scenarios on
2026-08-25, and the census hull's **0.028 K worst block** turns out to be near the population's
*median*, not its worst. Across 32,576 paired runs the delta-peak p99 is **0.2820 K** and the max is
**48.22 K**, and the ten furthest-apart pairs are all hulls between 79,460 and 155,010 blocks.

**The reach curve below is also the wrong way round for deciding a default.** It reads as though a
cap touches more of a big ship; measured over the population it is the reverse, because stiffness is
a block's property and not a hull's — 7.4 % of the blocks on hulls under a thousand against 3.1 % on
hulls over sixty thousand. What scales with hull size is not the reach but the *benefit*: no run
under 5,000 blocks is over its element-visit allowance at all, and 72 % of runs over 60,000 blocks
are. See [balance-lab.md](balance-lab.md#and-the-trade-is-the-wrong-way-round-the-cost-is-per-block-the-benefit-is-per-grid).

**And in air, which is where a floor has most to reach and where the tables above do not look.**
Convection is what makes a light block stiff, so a vacuum sweep understates both what a cap reaches
and what it buys. The same 20,916-node hull, driven, in thick air at 200 m/s —
`bench floor --size 20000 --speed 200 --driven`:

| cap | substeps | speed | blocks raised of 20,916 | peak error | worst error | rms |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| off | 24.97 | 1.0x | 0 | — | — | — |
| 16 | 16.00 | 1.5x | 21 (0.1 %) | 0.004 K | 0.005 K | 0.002 K |
| 8 | 8.00 | 2.5x | 232 (1.1 %) | 0.012 K | 0.019 K | 0.005 K |
| **6** | **6.00** | **3.1x** | **708 (3.4 %)** | **0.017 K** | **0.028 K** | 0.006 K |
| 4 | 4.00 | 3.9x | 3,584 (17.1 %) | 0.014 K | 0.143 K | 0.007 K |
| 1 | 1.00 | 6.7x | 11,571 (55.3 %) | −7.043 K | 10.503 K | 2.276 K |

**The demand is 24.97 in air against 7.35 in vacuum, and a cap of 6 reaches thirty-four times as
many blocks there** — 3.4 % against 0.1 % at a cap of 16, and in vacuum nothing above six binds at
all — because a block bolted to nothing much is still exchanging with the air around it. What that
buys is the whole of what the mechanism is worth: 3.1× in air against nothing in vacuum.

> **Re-measured 2026-08-24** at `C24`'s pair on the hull `C26` refreshed, and the shape of the
> answer moved with them. The demand was 34.44 in air against 22.97 in vacuum; the retune divides
> every capacity by 0.4 and the refresh takes six joints off a light fitting, so air came down by a
> quarter and vacuum by two thirds. **What a cap of 6 costs fell by twenty times** — 0.028 K on the
> worst-placed block against 0.607 K — which is the figure [backlog.md](backlog.md) `C3` is decided
> on, and it is now the same size as the ceiling breach `C19` was closed over.

**The peak barely moves, and the peak is the number damage is taken off.** At a cap of 6 the hottest
block on the hull is seven thousandths of a kelvin from where an uncapped run leaves it, while the
worst-placed block is 0.6 K out. That is the shape of the whole mechanism: it moves the blocks whose
own time constant is far below the step, and those are not the blocks anything is watching.

> **Two figures in the first air run were vacuum figures wearing an atmospheric label.** The demand
> and the floored count were both read before the grid had met its air — the stability estimate and
> the floor both read the environment — so the table said 22.97 and 765 in a run that stepped at
> 34.44 and floored 1,378. Both are now read after a step, and the count comes from the solver's own
> `FlooredNodes` rather than from a conduction-only sum the lab kept beside it, which could not see
> convection at all.

`bench floor` takes `--frequency` and prints the step length above the table, because a cap table
under no step length reads as though it described the default and is out by a factor of two if it
does not.

The two tables are the same measurement at two step lengths, and they line up: a cap of N at
Frequency 8 raises the same blocks as a cap of 2N at Frequency 4, because both leave the same
`C_min = ΣG · StepSeconds / (safety · N)`. **A cap is a statement about a node's time constant
against the step, not a number of substeps**, which is why it cannot be quoted without one.

The substep column lands on the cap exactly, which is the point of the floor being computed from
the same rate the estimate reads. A cap of one means one substep, not "about one".

Three things to read out of that table.

**Down to a cap of four, it touches forty-three blocks in forty-three thousand** and buys five and
a half times the throughput. Below four it starts reaching into the grating and then the armour,
and the error grows with the population rather than with the approximation.

**The error is confined to the blocks it moves.** The worst error anywhere and the worst error on a
floored block are the same number from cap 4 down; at 16 and 8 they differ by about a fiftieth of
a kelvin, which is the floored block's neighbour feeling it warm differently for a moment.

**The error decays.** Over five simulated seconds a cap of 4 is about a kelvin out; over fifty it
is a fifth of one. Heat capacity decides how fast a node gets somewhere, not where it ends up, so the
difference is a transient rather than a bias — `SubstepFloorTests` asserts exactly that, along
with the cap actually bounding the estimate, the untouched nodes staying untouched, and the block
keeping its real capacity for every readout.

**What it means for the field ship.** 31 substeps down to 4 is not only a fifth of the arithmetic:
it is below the 11 substeps `MaxElementVisitsPerStep` was granting, so the budget stops binding and
the grid stops being throttled. The ship runs at **full simulation rate at about a third of what
it now spends running at 35 %** — which is between a threefold and a fifteenfold improvement in
heat moved per millisecond, depending on which of the two you were unhappy about.

### Driven, and the caveat the harness only found at scale

The sweep above diffuses a seeded spread. A ship held at temperature by its own power — which is
what a field ship is — is the case that decides whether a player ever sees it, and it is what
`bench floor --driven` measures: the census share of heat producers, at the census wattage, run
until the hull settles. On a 43,232-block hull over 500 simulated seconds, at the shipped
**`Frequency 4`** — the same quarter-second step as the second table above:

| cap | speed | blocks raised of 43,232 | peak K | peak error | worst error |
| ---: | ---: | ---: | ---: | ---: | ---: |
| off | 1.0x | 0 | 2,345.5 | — | — |
| 16 | 1.3x | 172 | 2,345.6 | +0.10 K | 0.12 K |
| 8 | 2.4x | 461 | 2,345.6 | +0.06 K | 0.15 K |
| 6 | 3.2x | 1,450 | 2,345.5 | −0.02 K | 0.28 K |
| 4 | 4.7x | 3,648 | 2,344.6 | **−0.93 K** | 1.22 K |
| 3 | 5.6x | 5,219 | 2,343.3 | **−2.20 K** | 2.43 K |
| 2 | 8.0x | 9,283 | 2,337.2 | **−8.33 K** | 9.08 K |
| 1 | 12.3x | 14,138 | 2,310.1 | **−35.4 K** | 38.8 K |

> Re-run after the census landed and reproduced every temperature to three decimal places, which
> is the benchmark saying it is deterministic: the hull, the sources and the seeded state are all
> derived rather than sampled, so two runs differ only by the clock.

At `Frequency 8`, over the same 500 simulated seconds:

| cap | speed | blocks raised of 43,232 | peak K | peak error | worst error |
| ---: | ---: | ---: | ---: | ---: | ---: |
| off | 1.0x | 0 | 2,345.5 | — | — |
| 32 | 1.0x | 0 | 2,345.5 | 0.000 K | 0 |
| 16 | 1.0x | 0 | 2,345.5 | 0.000 K | 0 |
| 8 | 1.3x | 172 | 2,345.6 | +0.124 K | 0.138 K |
| 6 | 1.6x | 459 | 2,345.6 | +0.137 K | 0.167 K |
| **4** | **2.0x** | 461 | 2,345.6 | +0.085 K | 0.167 K |
| 3 | 2.5x | 1,450 | 2,345.5 | +0.006 K | 0.261 K |
| 2 | 3.1x | 3,648 | 2,344.6 | −0.909 K | 1.200 K |
| 1 | 4.1x | 9,283 | 2,337.2 | −8.305 K | 9.068 K |

**Both tables reach 2,345.5 K uncapped**, which is the two runs agreeing on where the hull settles
and therefore agreeing that they are the same experiment at two step lengths. The peak errors then
line up one cap apart exactly as the diffusing sweep does: Frequency 8 at cap 2 reads −0.909 K
against Frequency 4 at cap 4's −0.93 K, and cap 1 reads −8.305 K against cap 2's −8.33 K.

**The safe cap moves with the rate.** The `Frequency` 4 table puts the first visible movement at
cap 4; at `Frequency` 8 the peak is unmoved down to **cap 3**, and cap 4 costs +0.085 K for twice
the throughput. A recommendation quoted without its rate is conservative by a factor of two at one
end and unsafe at the other.

> **This table could not be produced at all until the overheat list was fixed.** `bench floor
> --driven --ticks 4000` was OOM-killed at 14 GB, twice: a burning block filed an overheat event
> every *substep*, and the harness's batched path keeps every step's events for a whole run, so
> this sweep held on the order of 440 million records. The solver now accumulates a block's damage
> and files one event a step. Same run, same 12 GB cap, completes. See
> [benchmarks.md](benchmarks.md#one-overheat-event-per-block-per-step).

**Down to a cap of 6 at the shipped `Frequency 4`, and a cap of 3 at `Frequency 8`, the peak is
unmoved.**
Below that it is not, and the sign says why: the capped hull is *cooler*, which is what a hull that
has not finished climbing looks like. The cap adds heat capacity, and a hull with more capacity
takes longer to reach the same place — at cap 1 it is 35 K short after five hundred simulated
seconds at the quarter-second step, and 8 K short at the eighth-second one.

That matters more than the number suggests, because **overheat damage is a threshold crossing**.
A hull that will eventually burn but takes twice as long to get there is a different game from one
that burns on schedule.

So the claim is narrow, and deliberately: **the cap does not change where a hull settles, and above
about 6 it does not visibly change when it gets there either.** Below 6 it delays the approach, in
proportion to how much mass it added. The equilibrium argument is safe *in the limit*, and the limit
is further away than a five-hundred-second run can see — which is why the driven tables above are
read for their sign rather than only their magnitude.

### At a million blocks it helps and is not enough

`bench floor --size 1000000 --ticks 10`, 1,000,294 blocks and 2.1 M links. **Measured on the
single-fitting hull that preceded the census**, so the block counts in the fourth column are far
lower than a census hull would give; the timings and the conclusion are unaffected, because both
are set by the substep count rather than by how many blocks were raised to reach it:

| cap | substeps | ms / step | speedup | blocks raised | peak K |
| ---: | ---: | ---: | ---: | ---: | ---: |
| off | 28.13 | 1,089 | 1.0x | 0 | 719.5 |
| 16 | 16 | 651 | 1.7x | 999 | 719.5 |
| 8 | 8 | 370 | 2.9x | 1,000 | 719.5 |
| 6 | 6 | 279 | 3.9x | 1,000 | 719.5 |
| 4 | 4 | 219 | 5.0x | 1,000 | 719.5 |
| 3 | 3 | 191 | **5.7x** | 1,000 | 719.5 |
| 2 | 2 | 135 | 8.1x | 7,049 | 719.6 |
| 1 | 1 | 105 | **10.4x** | 126,036 | 719.6 |

The same shape as everywhere else — the thousand fittings are the whole tail, and the cliff is
still between 3 and 2 — but **the speedup is smaller than at 42,000 blocks**: 10.4x here against
14.9x there. Once the substeps are gone what is left is the fixed per-step work, and at a million
nodes syncing state, publishing results and walking the stability estimate are no longer a
rounding error. The cap removes the multiplier; it cannot remove the pass.

Put through the measured 7x runtime penalty and spread across the fifteen frames of a step's
window, that is what a single million-block grid would cost a player:

| | per step, harness | per step, in game | per frame, in game |
| --- | ---: | ---: | ---: |
| uncapped | 1,089 ms | ~7.6 s | ~508 ms |
| cap 3 | 191 ms | ~1.3 s | ~89 ms |
| cap 1 | 105 ms | ~0.7 s | ~49 ms |

**A million-block grid is not playable at any cap.** Even at 1, one grid alone is three 60 fps
frames deep. This is exactly what [scale-design.md](scale-design.md) is for: the cap attacks the
substep multiplier and nothing else, and at a million blocks the pass itself is the problem —
which needs activity tracking and chunking, so that a step stops touching every node.

Where the cap does land the ship is at capital-ship scale. The same arithmetic on the
43,232-block hull: 22.4 ms per step uncapped becomes 2.9 ms at cap 3, or about **10.5 ms of a
frame in game becoming 1.4 ms**.

### The value is chosen by population, not by error

The error barely moves between 16 and 2 — 0.035 K to 0.061 K on the peak — so it cannot be what
picks the setting. What moves, and moves suddenly, is **how many blocks the cap reaches**:

| cap | harness ship, 8,904 blocks | field ship, 1,381 blocks | 199-grid world, 174,873 blocks |
| ---: | ---: | ---: | ---: |
| 16 | 8 | 2 (0.1 %) | 480 (0.3 %) |
| 8 | 8 | 16 (1.2 %) | 1,017 (0.6 %) |
| 4 | 8 | 83 (6.0 %) | 6,057 (3.5 %) |
| 3 | **8** | between | between |
| 2 | **201** | 327 (23.7 %) | 49,638 (28.4 %) |
| 1 | 1,121 | 540 (39.1 %) | 72,492 (41.5 %) |

The distribution is bimodal, and that is the whole shape of the decision. There is a thin tail of
genuinely tiny blocks — lights, cameras, panel tips — and then a cliff where **ordinary armour**
begins. Above the cliff the cap is a few fittings and free; below it, the cap is re-massing the
hull a player watches heat move through.

On the harness ship the cliff sits between 3 and 2: cap 3 reaches the same eight blocks that cap
16 does, and costs 7.8x less than uncapped. On a real ship it sits somewhere inside the 2–4 band,
which is why the projection now evaluates 3 and 6 as well — the previous list jumped straight over
the only interesting part of the range.

**Risks worth watching in game, which the harness cannot see.** Overheat damage is a per-node
threshold crossing, and a fattened node crosses later — on a 16 kg fitting bolted to armour that
is the same argument as before, but it is an argument rather than a measurement. And a cap set
aggressively enough to reach ordinary blocks changes how a hull warms, which is the thing the mod
exists to show. Four is the value the measurement supports; one is an arcade setting.

### 2. Lumping — the same idea, done properly

Merge a node into its dominant neighbour at topology time when the pair's coupling time constant is
far below the step. The lump carries the sum of the two heat capacities, the union of their exposed
faces, and the outside links of both; the absorbed block reads the lump's temperature.

Better than the cap in two ways: it is exact rather than approximate — the lump's capacity is the
sum of what it absorbed, so no energy is invented — and it removes the node *and* its links from
every pass rather than only softening its stability demand. Against the cap's five and a half
times, the extra is whatever the node and link count buys: on the field ship the lights are forty
three of forty-two thousand, so it is nothing at all.

That is the honest verdict for SE1: **lumping is the right shape and the cap already collects the
whole prize.** It becomes worth building when the population that needs collapsing is large — a
grating in eight rather than a light in a thousand, or SE2's small blocks — because then the node
count matters as much as the stiffness.

This is [scale-design §3.3](scale-design.md#33-lumping--the-memory-and-precision-win) and
[scale-design.md](scale-design.md#variable-block-size-breaks-the-integrator), which recommended it and could not say how much it was
worth. Now we can: on this fleet, a few per cent over a setting that already exists.

**What it needs designing:** which pairs qualify (mass ratio, coupling constant, both not heat
sources, both not independently exposed to very different environments); how a lumped block reports
its temperature to the terminal and the overlay; how overheat damage is attributed; how a lump
survives save/load and a block being ground off the middle of it. The last is the real work —
unlumping on change has to be as incremental as removal now is.

### 3. Multirate stepping — when the tail is not thin

Give each node, or each chunk, a substep tier and integrate the stiff subset more often than the
bulk. [scale-design §4](scale-design.md#4-multirate-stepping-unifies-two-problems) has the design
and the two rules that keep it conservative.

On *this* fleet it is worth less than the cap and costs far more. The stiff set is forty-three
blocks in forty-three thousand, so multirate's bill is `0.001 × 28 + 0.999 × 1 ≈ 1.03`
substep-equivalents against 28 — arithmetically the best answer on the page — but every one of
those savings is already collected by a floor that is forty lines and cannot desynchronise
anything. Multirate earns its complexity when the stiff set is large enough that flooring it would
change the simulation people came for, which is what SE2's mixed block sizes will produce and this
fleet does not.

### 4. Implicit integration — the endgame

Backward Euler on the sparse conductance matrix, solved with preconditioned conjugate gradient over
CSR adjacency. Unconditionally stable: one step of the whole 0.25 s, no substeps, no
`MaxSubsteps`, no `ClampConductionOvershoot`, no `MaxElementVisitsPerStep` shortening the step, and
`r_max` stops being in the cost model at all. Cost becomes iterations × one pass, and iterations
depend on how well-conditioned and well-preconditioned the system is rather than on the worst
block on the ship.

Against the field ship's 31 substeps, a CG solve that converges in twenty iterations is a third of
the work *and* runs at full rate instead of 35 %. That is roughly what `MaxSubstepsPerBlock 4`
already delivers, for a fraction of the risk — so the case for implicit is no longer this fleet.
It is the grid where the stiffness is *not* a thin tail: a hull of genuinely mixed block sizes,
where there is no small set to floor and the cap would have to fatten half the ship.

It is also the only route that fixes SE2's 400× stiffness ratio without a per-grid judgement call.

**What it costs:** the solver stops being a loop anyone can read. Energy conservation becomes a
property of the formulation plus the residual tolerance rather than of a visibly equal-and-opposite
exchange, and the non-linear terms — radiation's `T⁴`, the overshoot clamps, the coolant loops and
room air — have to be either linearised into the matrix or split out and handled explicitly, which
brings back a stability limit for whatever is left outside. It is a rewrite of
`ThermalSolver`, and it should not be attempted before 1 and 2 have said how much is left to win.

---

## An orthogonal lever: nobody is on two of those ships

Three identical capital ships cost 81 % of the world's thermal budget and at most one of them has a
player on it. Nothing in the mod knows that.

A per-grid rate tier driven by observation — distance to the nearest player, whether anything is
reading the grid's temperatures, whether it is powered — would take two thirds of this world's cost
away without touching the solver. The model already permits it: steps are energy-conserving and
order-independent, so a grid can advance in coarse steps or catch up in one long one when a player
approaches, exactly as it does now after a load.

What it needs is a decision about what a player is entitled to find when they arrive. A ship that
has been idling at one step a minute and then catches up in one 60-second step reaches the right
steady state and a different transient. For a derelict that is right; for a ship the player left
with a reactor overheating, it is the difference between finding it destroyed and finding it fine.
The honest version couples the tier to whether anything on the grid is *doing* anything, not only
to whether anyone is watching.

---

## How close the synthetic tests are to a real ship

Four differences, three now measured and one closed.

**Runtime: the game is 6–8x slower per element visit.** Measured by dividing solver milliseconds
by substep passes times elements, the same way at both ends — 3.97 ns on the harness at 8,904
blocks against 24.9 ns on a 1,293-block field ship whose entire working set fits in L2. Cache
pressure explains none of that; it is .NET Framework 4.8 against .NET 9. See
[load-and-hitching.md](load-and-hitching.md#measuring-it).

**Stiffness: the harness had none, and this was the important one.** The synthetic block
catalogue's lightest block was a 200 kg grating, so the benchmark ship asked for 2.25 substeps
where a real ship asked for 21 to 31 at the pair that shipped then. Every scale figure in this repository was measured on a hull
an order of magnitude softer than the ships it was meant to describe — the scale ladder's full
step at a million blocks was six times cheaper than it should have been. Closed by the census.
See [load-and-hitching.md](load-and-hitching.md#the-ladder-is-measured-on-a-census-hull-not-an-armour-cube).

**Population: closed, by measuring it.** The harness's blocks were 3,300 kg armour, 200 kg
gratings and one 16 kg fitting — a gap where a real ship has hundreds of armour corners, tips and
panels between 120 and 240 J/K. That gap mattered because it is what decides how many blocks a cap
reaches: cap 2 raised 2.3 % of the old benchmark hull and 23.7 % of a field ship.

Every benchmark hull is now built from [`Census`](../tests/Thermodynamics.Harness/Census.cs), the
eight-band block population of a real 1,381-block ship read out of a dump's block-type table, with
each band carrying the mount points its own block declares (`C26`). The census hull asks for **12.71
substeps in still sea-level air against a population running 6.20 to 22.41**, and its cap curve
tracks that population's — 15.3 % against 23.2 % at a cap of 4, 29.0 % against 40.3 % at 2, 57.3 %
against 75.9 % at 1. `CensusFidelityTests` holds it there.

**The reference moved from the dump to the corpus, and it had to.** The constants in `Census.Field`
are two ships from two vanished sessions, measured at a pair the mod no longer runs; the corpus is
8,098 ships and can be walked again in six minutes whenever a default changes. Where a test
still reads the dump it says which pace each side was taken at (`P6`).

**Shape and the adapter, both already known.** Bounding-box fill, exposed fraction and diameter
all vary by an order of magnitude with hull shape
([scale-design §10](scale-design.md#10-grid-shape-changes-the-arithmetic)), and the harness has no
game blocks at all, so the mass sweep, the power and thrust events and the room pressure API are
absent from every synthetic figure.

## The same question asked of eight thousand real ships

Everything above rests on two ships in two live sessions. A figure a running game reported is the
one kind of evidence that cannot be re-examined — the ships are gone, the world is gone, and what
else was true of them is unrecorded — so the same measurement was taken in the lab over the
workshop corpus, where every input is visible and the run repeats in four minutes:

```bash
dotnet run --project Thermodynamics.Sim -- stiffness            # 8,098 ships, ~6 min
dotnet run --project Thermodynamics.Sim -- stiffness --csv out/ # one row per ship
```

Nothing is stepped: a block's stiffness is a property of the grid it is bolted into and the world
it is asked about, so it can be read straight off a built ship. Everything below is **substeps
demanded of a quarter-second step**, which is the shipped `Frequency 4` and the basis the field
figures were taken on; `Frequency 8` would ask half of each. The air is still, at sea level, at noon —
the field sessions flew in wind, and wind raises the convection these numbers are mostly made of,
so these are a lower bound.

**Walked again on 2026-08-24 at the pair `C24` ships**, which is the table below; the one it
replaces was taken at `ConductionScale` 2.4 with the clock at 225 and is kept under it, because a
substep demand is a conductance over a capacity and both defaults moved.

| | min | p10 | p50 | p90 | p95 | max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| vacuum | 0.00 | 5.85 | 7.40 | 10.01 | 10.40 | 13.35 |
| **air** | 0.13 | 6.20 | **7.90** | 18.42 | 18.75 | 22.41 |

At the pair before, on 8,102 ships of the same corpus:

| | min | p10 | p50 | p90 | p95 | max |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| vacuum | 0.01 | 3.71 | 4.79 | 6.34 | 6.54 | 8.64 |
| **air** | 0.16 | 4.26 | **6.61** | 33.09 | 33.30 | 34.45 |

**The distribution did not simply scale, and that is the finding.** Four times the conduction pace
raises a conduction-limited demand and a clock two and a half times slower lowers every demand, so
the soft end came up and the stiff end came down: the bottom decile went from 4.26 to 6.20 and the
top from 34.45 to 22.41. A population that was two modes with a gap is now most of a continuum.

**The two field ships can no longer be placed.** They were taken in live sessions at the pair that
shipped then — 21.35 at the 63rd percentile of the old table and 31.25 at the 85th, ordinary ships,
which is what made them usable evidence. Against the new one they read past the maximum, and the
reason is the pace rather than the ships. Re-taking them needs a session on the new pair;
`TheFieldObservationsAreFromAPaceThatNoLongerShips` says so and fails when a dump arrives.

### What `A13` does to this population, written before the walk

Every figure in this section was measured through a blueprint reader that built eleven kinds of
vanilla block as an armour cube — [backlog.md](backlog.md) `A13`. A stiffness figure is a
conductance over a capacity, and the corrected blocks differ from armour in **both**: a gravity
generator is 8 tonnes where a light armour cube is 500 kg, a passage and a door carry different
mount faces, and a piston base is now three cells rather than two. So this walk has to be re-run,
and it costs three minutes rather than the survey's hours. What it should say goes here first
(`E1`).

| | prediction | falsified by |
| --- | --- | --- |
| the population | **8,101 ships**, four fewer than 8,105: seven hulls now rejected for holding an empty-subtype block on a grid size the game does not have it on, three admitted that the type-and-subtype pair now resolves | any other count |
| the modes | `LitShare` stays within **0.01** of 0.4496 — a light or a camera sets the demand on 45 % of hulls, and none of the eleven corrected types is a light or a camera | a move of more than 0.01 |
| the medians | `AirP50` and `VacuumP50` move by **under 5 %** each, because 0.53 % of blocks changed identity and a median is not a sum | a move of 5 % or more in either |
| the tail | `AirMax` moves **more than the median does**, because the corrected blocks are heavy and well-connected and the maximum is one block on one hull | the maximum moving by less than the median |

The medians prediction is the one with an argument rather than a measurement behind it, and the
argument is that stiffness is a per-block property read at a percentile, where `A13`'s cost to the
population was a **sum** over blocks and was therefore dominated by the few types that make
megawatts. **If the medians move more than 5 % the reason will be that the corrected blocks are
stiffer than armour rather than merely more numerous**, which is a different finding and worth
having.

### What it did: the heat moved a sixth and the stiffness did not move at all

Walked in **352 s** over **8,098** ships. Every headline percentile came back **identical to the
digit** — air p50 7.90, p90 18.42, max 22.41; vacuum p50 7.40, max 13.35 — and the constants in
`Census.Corpus` moved only in their last place.

| | prediction | outcome |
| --- | --- | --- |
| the population | 8,101 ships | **fails at 8,098.** The walk now reports **7 modded** where it reported none, which is exactly the seven `A13` rejects; the three the *census* gained were never excluded from this walk, so they cannot be gained here. The prediction merged two filters' bookkeeping |
| the modes | `LitShare` within 0.01 of 0.4496 | **holds** at **0.4474**, a move of 0.0022 |
| the medians | air and vacuum p50 move under 5 % | **holds**, and not marginally: **0.00 %** on both |
| the tail | the maximum moves more than the median | **fails**, degenerately — both moved by nothing |

**The failure that matters is the tail's, and it is the finding.** The prediction assumed the
corrected blocks were heavy *and well-connected*, so they would show up at the top of the
distribution. They are heavy and they are therefore **soft**: a substep demand is a conductance
over a capacity, and a gravity generator is eight tonnes of capacity against light armour's five
hundred kilograms. It makes megawatts and demands almost nothing. What sets a hull's demand is
still a `SmallLight` on 33.9 % of ships and an armour cube on 31.3 %, and none of the eleven
corrected types appears anywhere in that table.

**So the same fix is worth 15.76 % of the population's heat and 0.00 % of its stiffness**, because
[heat is a sum over blocks and stiffness is a maximum over them](balance-lab.md). That is worth
holding on to when the next dataset has to be re-taken: **a block-identity error reaches a total and
need not reach an extreme**, and which of the two a page is quoting decides whether it has to be
re-measured at all. The stepped walks are still open (`A13`) and they are scored on peaks, which
this says something about and does not settle.

### The population had two modes and almost nothing between them, and `C24` closed the gap

A median of 6.61 with a ninetieth percentile of 33.09 was not a long tail. It was two populations,
and it is now two clusters with the space between them filled in:

| what sets the ship's substep count | ships | share | median in air, now | before `C24` |
| --- | ---: | ---: | ---: | ---: |
| a light or a camera | 3,644 | **45.0 %** | **16.83** | 28.51 |
| armour or structure | 4,461 | 55.0 % | **7.23** | 4.81 |

**The share did not move at all — 44.96 % against 45.0 % — and the distance between the two did**:
5.9× apart, and 2.3× now. Between 8 and 28 substeps there were about six per cent of ships and
there are **49 %**. So a statement of the form "a real ship asks for about *n*" describes rather
more of the workshop than it used to, and the pages here that refuse to quote one are the ones to
re-read. What decides which cluster a ship is in has not changed: whether the builder put a light
on the outside — `SmallLight` alone sets the count on 34.5 % of all hulls.

That is the same finding [C9 in the backlog](backlog.md) reached from one save, now measured over
a population, and it is why the per-block cap is the lever this page argues for: the two modes are
separated by a handful of block types, not by ship design.

### The census hull sits in the trough, and it took a refresh to keep it there

The hull every benchmark here is built on landed at 23.39 in air before `C24` — the 65th
percentile, and *in the trough between the two modes*, a value about six per cent of real ships
had. For a benchmark that was a fair choice: neither of the two things a ship usually was, but
between them rather than outside them.

**`C24` put it outside the population and finding out why is `C26`.** At the pair that ships the
hull asked for **36.75** substeps against a corpus running 6.20 to 22.41 — 1.64 times the stiffest
of 8,098 real ships — and at a per-block cap of 8 it floored 6.91 % of its own blocks against a real
population's 0.92 %. The retune did not cause that; it made it visible, because a buried block's
demand is all conduction and quadrupled with the pace while a real ship's exposed one is mostly
convection and fell with the clock.

**The cause was mount points, and it is exactly the kind of thing `M11` exists for.** Every census
tier was built as a solid cube that mounts on all six faces, so the lightest band — 20 kg against
neighbours of 440 — carried six joints. `SmallLight` declares **one** mount point; the three shaped
armour bands declare three, four and five. A light bolted into a hull by six faces is a block no
builder can place and the stiffest thing on any hull that has one.

Each band now carries what the block it was measured from declares, and a hull is laid out the way
the game makes a player lay one out — **every block bolted to something**, since the game will not
let you place one that attaches to nothing, and the lightest band on the surface rather than dealt
wherever a hash puts it. The result, measured the same way as the population:

| | before `C26` | after | the population |
| --- | ---: | ---: | --- |
| substeps demanded in air | 36.75 | **12.71** | 6.20 – 22.41, median 7.90 |
| its stiffest block's own air ÷ vacuum | 1.00 | **1.97** | p10 1.00, median 1.07, p90 2.52 |
| exposed faces on that block | 0 | **4** | 3.46 on average |
| share of blocks a cap of 8 reaches | 6.91 % | **0 %** | 0.92 % |

Inside the population on every row, at about its 60th percentile for stiffness. The last row is the
one that is still not the population's, in the other direction and much smaller: a cap of 8 reaches
the *tail* — the ships whose stiffest block asks 18 to 22 substeps — and one median hull does not
have a tail. A synthetic hull cannot be both a median ship and a population, and
`TheShippedCapReachesThePopulationsTailAndNotATypicalHull` says which of the two this is.

How much of that stiffness comes from the air is the second question, and it has to be asked of
**one block**:

| | its own air ÷ its own vacuum | before `C24` |
| --- | ---: | ---: |
| a real ship, p10 | **1.00** | 1.04 |
| a real ship, median | **1.07** | 2.34 |
| a real ship, p90 | **2.52** | 6.89 |
| the census hull | **1.97** | 1.20 – 1.50 |

**Air has stopped making much difference to *stiffness*, for every hull in the game.** The median
ship's stiffest block is 1.07 times stiffer in air than out of it where it was 2.34, and a tenth of
them are exactly 1.00 — the pace `C24` ships makes a block's neighbours, rather than the sky, decide
what it demands. What this does *not* say is that air has stopped cooling a hull: the environment
terms are untouched, and this is a statement about which term sets a substep count. The census hull
is above the population's median on this row rather than below it, because its stiffest block is now
an exposed light like a real ship's.

> **It has to be the same block, and that is `E6`.** A hull's *air peak* over its *vacuum peak* is
> 1.02, which reads as a hull that does not notice air at all — and describes no block, because the
> air peak is an exposed fitting and the vacuum peak is a buried heavy block that conducts hard and
> does not care about air. The lab reports the same-block ratio as its own column for that reason.

### The census hull is a 96th-percentile ship for heat

The population answers a second question the two field ships could not. Both of the census's
generation constants come from that one ship, and both sit near the top of the corpus:

| | census | percentile of 8,141 real ships |
| --- | ---: | ---: |
| share of blocks making heat | 0.109 | **87th** |
| watts each one makes | 111 kW | **88th** |
| **waste heat per block** | **12.1 kW** | **96th** |

They multiply. A real hull under full electrical load makes **335 W a block** at the median and
6.1 kW at the ninetieth percentile; the census hull makes 12.1 kW — thirty-six times the median
ship.

**This reaches no stiffness figure.** Substep demand is capacity, conduction and exposure, and no
watt appears in it, so everything above this section stands. It reaches every *temperature* figure.

**Decided 2026-08-24 (`C14`): it is both, by property, and that is the right shape for a benchmark
hull rather than a contradiction.** The hull is *typical* in the thing that decides cost and
*extreme* in the thing that decides temperature, and those are two different properties of one hull
rather than two answers to one question:

* **Stiffness — typical, deliberately.** `C26` refreshed the tiers to carry the mount points of the
  blocks they stand for precisely so the hull would sit in the population's trough: 12.71 substeps
  in air at about the 60th percentile, four exposed faces against a real ship's mean of 3.46, an air
  ratio of 1.00 against a corpus median of 1.07. A cost figure has to describe what a server
  actually pays, so this one is a hull in the middle of the population.
* **Heat — the 96th percentile, deliberately.** A temperature figure has to be a ceiling somebody
  can rely on. Softening the tiers toward the median ship would make every temperature in this
  repository describe a hull that is thirty-six times cooler per block, which is not a bound at all.

**So the rule that follows is about quoting rather than about the hull.** A temperature taken on
the census hull is an **upper bound**, and it is worth noticing that every approximation this mod
has accepted on such a figure gets *safer* under that reading, not shakier: `C19`'s ceiling breach
costs at most 0.028 K, and `MaxSubstepsPerBlock 6` at most 0.607 K. A cost or substep figure taken
on it describes the population and needs no such caveat.

`TheCensusHullMakesFarMoreHeatThanARealShip` pins the heat characterisation, and
`TheCensusHullIsInsideThePopulationItStandsIn` with `TheCensusHullFeelsAirLikeARealHullDoes` pin the
stiffness one — so the set fails if either half moves quietly and the decision above stops being
true.

### The cap curve holds where the cap is actually set

`MaxSubstepsPerBlock` is chosen from how much of a hull a cap holds back, and that curve came from
the 189 stepping grids of one telemetry dump. Over **2.4 million blocks of 8,102 workshop ships**:

| per-block cap | corpus, at the pair that ships | corpus, before `C24` | one field dump | the census hull |
| ---: | ---: | ---: | ---: | ---: |
| 8 | **0.92 %** | 1.59 % | 1.16 % | 0 % |
| 4 | **23.22 %** | 7.20 % | 6.01 % | 15.25 % |
| 2 | **40.33 %** | 35.54 % | 23.68 % | 29.02 % |
| 1 | **75.89 %** | 52.53 % | 39.10 % | 57.25 % |

**They agree where the choice is made and part company where it is not.** At the cap anyone would
ship, all three columns are inside half a percentage point of each other, so the reach of a shipped
cap is confirmed rather than corrected — and it is the one row `C24` left alone. Below it the curve
steepened sharply: a cap of 4 now reaches a quarter of all blocks where it reached a fourteenth,
because the population's soft mode has come up to meet its stiff one. The dump understates the
aggressive end on both pairs — one save is one builder's habits — and it cannot be re-taken at this
one.

`TheFieldCapCurveMatchesTheCorpusWhereTheCapIsActuallySet` holds the agreement at the top, because
if that ever parts company the shipped cap was chosen against a population it does not describe.

## What is measured and found not to matter

**Diagnostics.** Telemetry switches `CollectDiagnostics` on, which adds a per-node clear, five
field writes per exposed node per substep, and two random-access read-modify-writes per link into
managed `ThermalNode` objects — none of it charged to the step's work estimate. It looked like it
might be a large share of what a field dump measures. It is not: `bench scale --diagnostics`
against the same run without it costs **7 %** at 32,800 blocks on the harness. Worth knowing, worth
keeping in mind for a .NET 4.8 runtime with a colder cache, not worth acting on.

The uncharged part is still a real if small defect — `ClearConductionDiagnostics` walks every node
per substep and `SubstepWork` does not count it, so a diagnostics-on grid paces itself slightly
slow.

---

## Giving the light fittings a definition fixes the conduction half only

`Cubes.xml` had no entry for any decorative or electronic type, so every light, neon tube and camera
fell through to `DefaultThermodynamics` and inherited **mild steel's 50 W/(m·K)** on a 16 kg body.
That is what made the least massive block on a ship the stiffest thing on it. Four per-type entries
now say what these blocks are made of — a light is a plastic housing around a glass lens, and
plastic is about 0.2 W/(m·K) against steel's 50:

| Type | Conductivity | Specific heat | Conduction demand before | After |
| --- | ---: | ---: | ---: | ---: |
| `InteriorLight` | 2 | 900 | 21.4 | **0.43** |
| `ReflectorLight` | 2 | 900 | — | — |
| `EmissiveBlock` | 1 | 840 | 5.8 | **0.06** |
| `CameraBlock` | 5 | 800 | 9.3 | **0.52** |

**In vacuum the definitions do what they were written to do. In air they used to do very little,
and `C24` changed that.** `DecorativeStiffnessTests` reproduces it on one 16 kg fitting bolted to an
armour bar:

| | Demand, at the pair that ships | Conduction share | Demand before `C24` | Share before |
| --- | ---: | ---: | ---: | ---: |
| steel, vacuum | 8.41 | 89 % | 6.56 | 71 % |
| light definition, vacuum | **0.70** | 41 % | **1.00** | 18 % |
| steel, air | 18.29 | 41 % | 31.68 | 15 % |
| light definition, air | **5.69** | 5 % | **13.68** | 1 % |

A block's stability demand is its conductance over its heat capacity, and the conductance has two
halves: what it is bolted to, and what its exposed surface exchanges with the sky. **The definitions
address the first**, and how much that is worth depends entirely on which half dominates. At the
pace the conversion calibrated to, convection over the cell's exposed area was 85 % of a steel
fitting's rate in dense air and removing the conduction half removed almost nothing. At four times
that pace conduction is 41 % of it, and writing a real conductivity onto the fitting takes **69 %**
of its demand off in air as well as in vacuum. So the sentence this section is named for was true of
one pair and is not true of the one that ships.

The prediction that the definitions would take uncapped demand from 21.4 substeps to about 5.6 was
arithmetic on `conductivity / (mass × specific heat)` — the conduction term alone. It was right
about that term and silent about the one that dominates. A fleet flying in air averaging 0.73
density at 93 W/(m²·K) convection is what made it visible; two earlier runs in thinner air did not.

### The knob that reaches the other half is exposed area

A `SmallLight` on a large grid presents 23.6 m² of exposed surface against 64 J/K of heat capacity,
because **exposed area comes from the cell a block occupies rather than from the block**. A light
fitting is not a 2.5 m cube of radiating and convecting surface. `ExposedSurfaceMultiplier` is the
per-type knob for exactly this, and every decorative entry leaves it at 1. At 0.1 the test fitting
fell from 13.68 substeps to below the armour it is bolted to — a factor of nine on the term that was
left. **It is no longer the only knob that reaches a fitting in air**, since the definitions now
reach the larger half there too; what it is still the only knob for is the environment term itself.

**Not applied.** It also changes how much heat the block exchanges with its surroundings, so it is a
balance decision rather than a free one, and the figure is recorded so the decision can be made
against one.

The plushies at the top of a fleet's stiffness table are a different problem: 1 kg of steel at
conductivity 50 is conduction-stiff, and a material definition would fix them outright.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-10-28 | Exclude disabled block, coolant and room exchange from stability demand, clamps and mass flooring, including after incremental topology edits. Turning the floor off restores real block capacities. Tiny two- and three-block regressions cover toggles, placement/removal and independent source heating against disconnected blocks. |
| 2026-08-25 | Put the population beside the one-hull sweeps ([backlog.md](backlog.md) `C3`). The census hull's 0.028 K worst block is near the population's median rather than its worst — the p99 over 32,576 paired runs is 0.2820 K and the max 48.22 K — and the reach runs the opposite way to what these tables suggest, because stiffness is a block's property and not a hull's. |
| 2026-08-24 | **Decided `C14`: the census hull is typical in stiffness and extreme in heat, on purpose.** The section above framed that as an open question — a worst case or a typical ship — and the two are properties rather than answers. A cost figure has to describe what a server pays, which is why `C26` put the hull in the population's trough; a temperature figure has to be a ceiling, which is why it stays at the 96th percentile for heat. The rule that follows is about quoting: a temperature taken on this hull is an upper bound, and every approximation accepted on such a figure — `C19`'s 0.028 K, `MaxSubstepsPerBlock 6`'s 0.607 K — is safer under that reading rather than shakier. |
| 2026-08-24 | **Re-ran both per-block cap sweeps at the pair and hull that now ship, and what the cap is worth inverted.** In vacuum the hull demands 7.35 substeps rather than 22.97, so no cap above six binds and a cap of 6 buys nothing; in thick air at 200 m/s it demands 24.97 rather than 34.44 and a cap of 6 buys 3.1× for **0.028 K** on the worst-placed block against the 0.607 K that made it a switch. That is the same size as the ceiling breach `C19` accepted, so the number separating the two mechanisms is gone — [backlog.md](backlog.md) `C3`. |
| 2026-08-24 | **Refreshed the census tiers against the blocks they were measured from, which closes [backlog.md](backlog.md) `C26`.** Every tier was a solid cube mounting on all six faces, and `SmallLight` declares one mount point while the three shaped-armour bands declare three, four and five — so the hull's lightest band carried six joints where the block it stands for carries one. The hull demanded 36.75 substeps in air against a population running 6.20 to 22.41 and now demands **12.71**, its stiffest block has four exposed faces against a real 3.46, and its air ratio is 1.97 against a population median of 1.07. Blocks are also laid out the way the game makes a player lay them out: every one bolted to something, and the lightest band on the surface. |
| 2026-08-25 | **Walked the corpus again after `A13`, and the stiffness did not move.** 8,098 ships in 352 s — seven fewer, and the seven are the hulls that hold an empty-subtype `Door` or `GravityGenerator` on a small grid, which the game has only on large and which used to resolve to a small armour cube. Every headline percentile is identical to the digit and the constants move only in their last place, against the same fix being worth **15.76 %** of the population's full-load heat. A demand is a conductance over a capacity, so the corrected blocks — a gravity generator is eight tonnes — are exactly the blocks that make heat and demand nothing. Two of four registered predictions hold; the tail prediction fails and is where the finding is. |
| 2026-08-24 | **Walked the corpus again at `C24`'s pair, and the population changed shape rather than scale.** 8,105 ships in 190 s: the two modes closed from 5.9× apart to 2.3×, half the population now sits between them where six per cent did, and air has stopped making much difference to *stiffness* anywhere — the median hull's stiffest block is 1.07 times stiffer in air where it was 2.34. The census hull went the other way and is now stiffer than every ship in the corpus, flooring 6.91 % of its own blocks at the shipped cap against a real 0.92 % ([backlog.md](backlog.md) `C26`). The two field observations cannot be placed against any of it: they were taken in sessions at the pair before. |
| 2026-08-24 | **Bounded the coupled paths and measured their ladders**, which closes [backlog.md](backlog.md) `A10`. The pairwise clamp is half the bound a lumped mass needs — a parcel carries a link to every pipe on it, a room's air one to every surface, and the node on the other end of a sink face is pulled on by both the fluid and its neighbours. The per-node relaxation now applies to both coupled passes, which makes a substep a convex combination of the temperatures around a node. On the fixture where the plumbing sets the demand, 9.7× over-subscribed: **1.3e25 K before, 1,799 K after**; a thin room refused one substep of thirty went from 3,839 K of spread to inside the 300 K it started at. The block ladder is unmoved to three decimals. `bench ceiling` grew `--fixture census|plumbed|pressurised|rings`, because the ladder had been a block ladder for as long as it had existed and said so nowhere. |
| 2026-08-24 | Recorded that the refusal ladder is a *block* ladder. The coolant path has no overshoot clamp and on a hull carrying nothing stiffer is what sets the demand, so refusing it is orderly to about 4.5× and reaches 1.7e11 K at 9× — a cliff where the block path has a slope ([backlog.md](backlog.md) `A10`). |
| 2026-08-23 | **Measured the per-block cap in air, which is where it has most to reach** ([backlog.md](backlog.md) `C3`, `C19`). Demand is 34.44 there against 22.97 in vacuum, a cap of 6 reaches 6.6 % of blocks against 3.4 %, and it buys **4.2×** for 0.607 K on the worst-placed block and 0.007 K on the hottest. Also corrected two columns that were vacuum figures in an air run: the demand and the floored count are read after a step now, and the count comes from the solver rather than from a conduction-only sum that could not see convection. |
| 2026-08-23 | **Measured what the *global* ceiling costs when it refuses a demand**, which nothing had — the page had a floor sweep and no ceiling sweep, and [backlog.md](backlog.md) `C19` was answering the question by naming the floor, which ships off. Added [What refusing the demand costs](#what-refusing-the-demand-costs) and `bench ceiling`: free to about 2× over-subscribed, breaking between 2× and 3×, and the shipped breach of 1.15× worth 0.028 K on the hottest block over 600 simulated seconds. The error follows the ratio rather than the substep count, measured at two step lengths, which is what lets a rig answer for a population. |
| 2026-08-22 | Labelled the two cap tables by their step length alone, and named the shipped one. They were labelled by which settings profiles ran at each rate, and the profiles are gone; the shipped `Frequency` is 4, so the quarter-second table is now the one a default world reads. |
| 2026-08-22 | Gave up *A number in the report that does not add up* to [telemetry.md](telemetry.md#whether-the-report-agrees-with-itself), which carries the same defect, the invariants the Consistency section now states and the fix — this page was a second, older account of a report it does not own. Moved four historical asides into this log, keeping what each of them was *for*: that a cap table has to name its step length, that a recommendation quoted without its rate is out by two, that the equilibrium claim is safe only in the limit, and that a ratio is formed from two measurements of the same block (`E6`). |
| 2026-08-22 | Took the decorative-block findings from `field-tuning.md` — the definitions fixing the conduction half only, and exposed area as the knob that reaches the other half — since this page is where that subject lives. Added the standard header and this log. |
| 2026-08-22 | Corrected a finding published two commits earlier: it divided a hull's air peak by its vacuum peak, which are two different blocks, and reported the census hull as insensitive to air. The benchmark fixture changed on it is reverted and the lab now reports the same-block ratio as its own column. **Take an aggregate of a ratio, never a ratio of aggregates.** |
| 2026-08-22 | Established that the census hull is a 96th-percentile ship for heat and sits in the trough between the population's two stiffness modes, and said so where every figure taken on it is quoted. |
| 2026-08-21 | Asked the same question of 8,102 real workshop hulls rather than one save, which found the population is bimodal — a light sets the substep count on 45% of hulls at a median of 28.5, armour on the rest at 4.8, with almost nothing between. |
| 2026-08-20 | Measured a ship's stiffness in the world it flies in rather than in vacuum, which is what showed the air term dominating. |
| 2026-08-18 | Opened the page from the 199-grid field dump: the stiffness is a thin tail, the harness could not reproduce it, and none of the settings that existed could help. Built the per-block substep cap and measured it. |
