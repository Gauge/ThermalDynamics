# Balance

What every block this mod ships is worth against the vanilla blocks it competes with, and what a
population of 8,132 real ships says about whether the balance targets hold.

> The rules argued here are stated canonically in [rules.md](rules.md): `E1` `E3` `E4` `E5` `E9` `E11` `M10` `P1` `P2`.

| Looking for | Go to |
| --- | --- |
| The design of the lab and the eight criteria | [balance-lab.md](balance-lab.md) |
| The equations behind every figure here | [thermal-model.md](thermal-model.md) |
| What the blocks are and how to build with them | [blocks.md](blocks.md) |
| The censoring limit every peak temperature is subject to | [known-issues.md](known-issues.md#deliberate-limits) |

```bash
cd tests
dotnet run --project Thermodynamics.Sim -- balance             # the per-block report
dotnet run --project Thermodynamics.Sim -- balance --csv out/  # also a diffable table
dotnet run --project Thermodynamics.Sim -- reactors            # the reactor waste fraction sweep
dotnet run --project Thermodynamics.Sim -- oxygen              # the same, for the oxygen generator
dotnet run --project Thermodynamics.Sim -- coolers             # the whole-game cooling ladder
python3 tools/corpus/verdict.py out/corpus-2026-08-21          # the population criteria
```

---

## The three findings

1. **A coolant sink face is the stiffest way to move heat into a panel, and nothing else is
   close.** A bolt joint carries 167 W/K; a sink face carries 6,250 W/K. Every surface property of
   the radiator put together is worth a quarter of what plumbing it is worth. **The radiator is not
   a block you bolt to a hot thing — it is a block you plumb.**
2. **Nothing in the game solves a reactor by being bolted to it**, and two common blocks make it
   worse. Cooling has to *carry heat away*, because radiative equilibrium puts temperature on the
   fourth root of area: halving a ship's temperature needs sixteen times the surface.
3. **The population is bimodal, not spread.** A ship either sits near ambient indefinitely or runs
   away in under ten seconds, and the 2–5 minute window the balance target asks for lands in the
   gap between them. One block type decides which side most ships fall on.

---

## Block balance

### Where the numbers come from

Nothing here is transcribed except the vanilla figures, and those are checked.

| Source | What it supplies |
| --- | --- |
| [`ShippedBlocks`](../tests/Thermodynamics.Harness/ShippedBlocks.cs) | The mod's own blocks, read from `Data/CubeBlocks/*.sbc` and `Data/Cubes.xml` at run time — size, mount faces, components, thermal properties |
| [`Vanilla`](../tests/Thermodynamics.Harness/Vanilla.cs) | Component masses and the comparison blocks, transcribed from Space Engineers' own `Content/Data` |
| [`BalanceLab`](../tests/Thermodynamics.Harness/BalanceLab.cs) | The measurements |
| [`SoloBlockRig`](../tests/Thermodynamics.Harness/SoloBlockRig.cs) | One block alone in shadow, bare or under one cell of armour — the two bounds both fraction sweeps are decided on |
| [`ReactorLab`](../tests/Thermodynamics.Harness/ReactorLab.cs) | The reactor waste fraction sweep |
| [`OxygenGeneratorLab`](../tests/Thermodynamics.Harness/OxygenGeneratorLab.cs) | The same, for a consumer: the oxygen generator's fraction |
| [`BalanceTests`](../tests/Thermodynamics.Tests/BalanceTests.cs) | The conclusions, pinned |
| [`ReactorWasteHeatTests`](../tests/Thermodynamics.Tests/ReactorWasteHeatTests.cs) | The reactor conclusions, pinned |
| [`OxygenGeneratorWasteHeatTests`](../tests/Thermodynamics.Tests/OxygenGeneratorWasteHeatTests.cs) | The oxygen generator conclusions, pinned |

**A block's mass is the sum of its components**, priced through `Vanilla.ComponentMasses`. That
matters more than it looks: heat capacity is `mass × specific heat`, so a wrong mass scales every
temperature that block ever reaches.

`Vanilla` is checked in because most machines running this suite have no game install. On a machine
that does have one, `TheVanillaReferenceStillMatchesTheInstalledGame` re-derives every figure and
fails if it has drifted — a transcription nobody checks becomes fiction.

### What is measured, and why each one can bind

| | Measure | Unit | What it answers |
| --- | --- | --- | --- |
| 1 | **Capacity** | J/K | how much heat the block swallows before it warms. No solver involved. |
| 2 | **Shedding** | W at 600 K into a 2.7 K sky | what the block gets rid of |
| 3 | **The joint** | W/K through the block's mount faces | what can *reach* it |
| 4 | **Delivered** | steady state under a real load | all three at once, and the only one a player experiences |

Loads are stated as **watts of heat**, not watts of power, and are driven with a block whose waste
fraction is 1 — otherwise every row would be a fraction of its heading.

### The radiator is a block you plumb

One 200 kW source, one shipped radiator, one dial changed at a time:

| Dial | Change | Source settles | Gain |
| --- | --- | --- | --- |
| *(shipped)* | as built | 740.2 K | — |
| `Emissivity` | 0.35 → 0.80 | 729.5 K | 10.7 K |
| `ExposedSurfaceMultiplier` | 1.25 → 5.0 | 723.0 K | 17.2 K |
| `ExposedSurfaceMultiplier` | 1.25 → 10.0 | 715.6 K | 24.6 K |
| panel depth | 1×5×2 → 1×1×2, same mass | 698.8 K | 41.5 K |
| **coolant sink** | **fed by a loop, not bolted** | **544.9 K** | **195.3 K** |

**The surface dials disappoint because they work on the wrong end.** Multiplying the area makes the
panel *colder* — 338 K down to 213 K across that sweep — and radiation falls away as the fourth
power, so eight times the surface moves only 25% more heat. Meanwhile the drop across the joint
grows from 402 K to 503 K: the panel is starving, not saturating.

Shortening the panel is the only geometry change that helps, and only a little. Conduction runs
centre-to-interface, so the shipped panel's five-cell height puts 6.25 m of metal between its middle
and the joint; flattening it to one cell triples the joint to 500 W/K — but costs most of the
surface, and the two nearly cancel.

### The same question asked of the whole game

The `coolers` ladder takes the largest reactor the game ships, at its plate rating, in shadow, with
**every block that has a plausible claim to being the best cooling in the game** stacked against it
in a column, one to thirty-two. Candidates are chosen by the derivation rather than by taste —
anything whose thermal properties give it more surface than an ordinary cube of the same size — plus
the mod's radiator and a plain armour block as the control.

The bare reactor settles at **889.9 K**. What bolting things to it buys, at the count where each
stops improving:

| Bolted on | Best saving | Of 889.9 K |
| --- | ---: | ---: |
| Wind turbine | 27.2 K | 3.1% |
| Exhaust pipe | 3.7 K | 0.4% |
| **`Gauge_LG_Radiator`** | **2.1 K** | **0.24%** |
| Heat vent | 0.4 K | 0.05% |
| Ladder shaft | 0.3 K | 0.03% |
| Armour cube *(control)* | **−0.6 K** | worse |
| Large thruster | **−16.6 K** | worse |

**Two things make it worse**, because a block against a face is a face that was radiating to the sky
and now radiates into a neighbour. The mod's own radiator is beaten by an exhaust pipe and a wind
turbine, both of which win on having more exposed surface *and* a shorter path to the joint — the
same handicap the panel-depth row measures. `CoolingLadderTests` pins the conclusions, not the
figures.

**A stack saturates by about eight.** Thirty-two radiators save no more than eight do: the top of a
thirty-two stack sits at 61 K, having shed to the sky everything the joints below could deliver. It
is a fin, and it has a length.

> **Two candidates cannot be stacked at all**, and the far-end column is what says so rather than
> leaving it to look like saturation. An exhaust pipe and a wind turbine above the first one come
> back at 34 K and 17 K — *below* the 293 K they were built at — so no heat ever reached them. Their
> n > 1 rows are a fact about the block rather than a rung on a ladder.

### The radiator still earns its place

Against a slab of ordinary light armour of the same shape, in the same position, on the same load:

| Fit | Count | Settles | Saved | Mass | K per tonne |
| --- | --- | --- | --- | --- | --- |
| bare source | 0 | 783.2 K | — | — | — |
| radiators | 1 | 492.8 K | 290.4 K | 600 kg | 484.0 |
| radiators | 2 | 463.5 K | 319.7 K | 1,200 kg | 266.4 |
| radiators | 8 | 442.3 K | 340.9 K | 4,800 kg | 71.0 |
| armour slab | 1 | 710.9 K | 72.3 K | 5,000 kg | 14.5 |
| armour slab | 8 | 690.9 K | 92.3 K | 40,000 kg | 2.3 |

Four times the cooling for an eighth of the mass — about **33× better per tonne**, pinned by
`TheRadiatorBeatsTheArmourItDisplaces`. The second radiator is worth 29 K and the eighth is worth
5 K: one joint feeds them all, and how much that joint carries is what decides where the stack
saturates.

> **This table has moved twice, both times because the panel changed and not the ladder.** At
> `C24`'s conduction pace the first panel was worth 228.6 K; the pace went to 9.6 and it became
> 290.4 K at `C36`'s emissivity. The column that has never moved is the shape: the second panel is
> worth an eighth of the first and the eighth is worth a fiftieth, at every setting either dial has
> been read at. That is the finding — see below.

**Past a certain load the radiator turns negative.** At 2 MW into one cell the panels still help —
130 K for the first — but the *armour* goes negative, costing 8.4 K, because it covers faces that
were radiating and cannot carry away what it blocked.

#### The joint, not the panel

`balance`'s block table sheds a large radiator at **1,561,621 W** at 600 K and carries **2,336 W/K**
across its mounts. Those pair as `shed/reach = 6.7`: across a 100 K gradient the panel can throw
away nearly seven times what the joint it is bolted through can deliver to it. Everything else on
this page follows from that ratio:

| Change to one panel on a 200 kW source | Source K | Gain | Through the joint |
| --- | ---: | ---: | ---: |
| as built | 492.8 K | — | 173,872 W |
| emissivity 0.85 → 1.00 | 482.0 K | 10.8 K | 176,080 W |
| fake surface ×8 | 373.3 K | 119.5 K | 191,392 W |
| mount on 6 faces, not 2 | 492.8 K | 0.0 K | 173,872 W |
| fed by a coolant loop, not bolted | 441.3 K | 51.5 K | — |

The joint carries about 174 kW and no surface dial moves it by a tenth. `C36` multiplied the
radiator's emitting power by 2.43 and the first rung of the ladder rose while the saturation point
stayed at two panels; the corpus retrofit, bolting panels onto real hulls, moved the median ship
**0.1 %** for the same change. Two labs and a population all report the same wall. **Emission was
never the limit. Transport is** — which is what `F27` found for bases from the other end, and what
`blocks.md`'s *plumb it, do not bolt it* has been saying without a number on it.

#### What the loop can be worth, and what it costs

> **This section is how the decision was reached, and its *shipped* column is now history.** It
> sweeps upward from `h` 160, which was the coefficient when it was written; `C42` shipped 1,000
> while the pump runs, with 160 kept as the stopped value. The sweep is left as it was taken because
> the argument is the shape of it — see *What a jump drive costs in radiator* below for what
> decided it, and the change log for what moved.

The last row above is the only one that moves the joint, so the dials under it are the ones with
room. A sink face carries `h · A`, and on a large grid `160 × 6.25` was that 1,000 W/K exactly.
Swept on the same source, the same panel and the same ring:

| The loop feeding one panel | Source K | Gain | Sink W/K | Drop across it | Substeps |
| --- | ---: | ---: | ---: | ---: | ---: |
| shipped, `h` 160 | 441.3 K | 51.5 K | 1,000 | 117.2 K | 11.88 |
| `h` 400 | 410.6 K | 82.2 K | 2,500 | 81.2 K | 12.68 |
| `h` 1,000 | 386.0 K | 106.8 K | 6,250 | 52.9 K | 14.67 |
| `h` 2,000 | 374.6 K | 118.2 K | 12,500 | 40.3 K | 17.99 |
| `h` 1,000, 515 kg/pipe | 376.3 K | 116.5 K | 6,250 | 42.5 K | 14.67 |

**The drop across the sink falls from 117 K to 40 K while the panel runs hotter**, which is the
signature worth trusting: the loop is ceasing to be the resistance and the panel is being fed enough
to work. 160 W/(m²·K) is a slow flow — the definition says so — and a pumped water-glycol ring is
forced convection, which any handbook puts in the low thousands. The dial's own in-game slider
already goes to 2,000.

**The coolant mass is a transport term, not a buffer**, which is not obvious and is why it is in the
table. A ring carries `ṁ·c_p` past a point, so the loop is two resistances in series — the sink face
and the ring's carrying rate — and at the shipped `h` the sink binds so hard that quadrupling the
coolant is worth 0.9 K. Fix the sink and the carrying rate becomes the limit: at `h` 1,000 the same
change is worth **9.7 K**, and it costs the integrator nothing at all, because a segment's substep is
sized from `SegmentConductance / SegmentThermalMass` and this moves only the denominator.

It saturates by about 500 kg, and 515 kg is where the derivation lands independently: a bore one
fifth of the cell across, down the middle of a 2.5 m cube, is 0.49 m³ of water-glycol. **It shipped
as a flat 50 kg with no grid size in it** — 3.2 kg/m³ in a large cell, which is a gas, against
400 kg/m³ in a small one, which is a liquid — and `C43` replaced that with a density of 33 kg/m³, the
same correction `HeatTransferCoefficient` had already had. `CoolantKilogramsPerCubicMetre` is the
dial; `CoolantMassPerPipe` is still read and still means kilograms in a pipe, for a file that states
one.

> **The cost column is honest and narrow.** +23 % substeps at `h` 1,000 is paid by the block the
> sink touches, not by the fluid, so more coolant cannot buy it back. It also lands only on grids
> that build a loop — the corpus is vanilla-only and carries none — so no population figure on this
> page moves with these dials. That is the vanilla and modded lanes doing their job, and it is also
> the reason `G3` has never been measured on a real hull.

#### The bound that closes this line

Measured on the hulls rather than the bench, the package is worth **nothing**: `carried` and
`plumbed` come back identical at every quantile, while the instrument says it applied exactly as
designed — worst sink 40 W/K to **250 W/K**, every ring circulating. The bench said the sink face
binds. On a ship it does not, and the reason is one number:

| On a plumbed hull, at the hottest block | |
| --- | ---: |
| already sheds into the hull it is welded to | **1,457 W/K** |
| the shipped loop adds | 40 W/K |
| the candidate adds | 250 W/K |
| stands above the median block on its own grid | **39.2 K** |

**A block on a finished ship is already welded to a hull that conducts.** The reference rig has no
hull — its source is isolated, so the loop is the only path it has and improving that path is worth
65 K. A real hot block has 1,457 W/K of alternatives before any cooling is fitted, so a sink face is
a 3 % change to its conductance at the shipped figure and a 17 % change under the candidate, against
a background that was already carrying the heat away.

**And the heat has nowhere to go.** The hot block stands 39.2 K above the median block on its own
grid, so equalising it with its own hull — which is the most that *any* path moving heat inside the
grid can ever achieve — is worth **9.13 % of the peak**. The plumbed ring gets 2.43 % of that bound.

> **The bound is the finding, not the shortfall.** A perfect internal path is still single figures.
> It does not matter how much the loop is improved, because the ship is nearly as hot as the block
> and there is nowhere inside the grid left to put the heat. That is why three independent levers
> this session — the panel's emissivity, the fluid's coupling, the coolant's mass — all measured
> large on a bench and nothing on a hull. **They are all internal, and internal is bounded at 9 %.**

What is not bounded that way is **area to the sky**, which is the only thing that takes heat off the
grid entirely — and the bolted arm, which adds exactly that, is the only one that ever reaches 7.9 %.
Even that is single figures, because these ships make far more heat than any bolt-on can shed: it is
`C34`'s finding from the fitting end, where a block that cannot cool itself threefold is not going to
be saved by a panel bolted beside it.

**So `G3` cannot be met by retrofitting.** That is not a defeat — it is
[document-of-intent.md](document-of-intent.md)'s stated position, *cooling designed in*, arriving as
a measurement rather than as a preference.

#### And designed in, it works

Every rig this repository owned had an **exposed** source, which is why nothing could see the
difference: a panel bolted to a source standing in the open radiates wherever it is put. Bury the
source in solid armour — which is how one is installed — and a bolted panel is buried with it.

200 kW into the centre cell of a 7-cube of light armour, in shadow, **both fits carrying the same
two panels** (`designed`):

| Fit | Source settles | Saved | Panels | On the skin |
| --- | ---: | ---: | ---: | ---: |
| buried, bare | 375.4 K | — | 0 | 0 |
| bolted, buried | 387.8 K | **−12.4 K** | 2 | 0 |
| plumbed, to the skin | 307.9 K | **+67.5 K** | 2 | 2 |

**A panel bolted to a buried source is worse than no panel at all.** It displaces armour that was
conducting heat away and puts in its place a block whose one talent it cannot use, having no face on
the sky. And a ring carrying that same heat out to the hull, with the same two panels on the outside
of it, takes **18 % of the peak** off — twice the ceiling on everything that stays inside the grid,
because it is the one fit that takes heat *off* the grid rather than moving it about.

> **This is the whole mod in one table**, and it had never been measured. `blocks.md`'s *plumb it, do
> not bolt it* is right — but for a reason no previous rig could show, and not the reason the page
> gave: the loop's virtue is not that its joint carries more than a bolt joint, it is that the joint
> can be somewhere else. `DesignedHullTests` pins the sign of both rows, and levels the panel count
> between the arms so the comparison is about *where* the heat is put and nothing else (`P6`).

#### What a jump drive costs in radiator, and why the shipped loop cannot pay it

`C34` named the runaway blocks by *self index* — heat made over what their own skin sheds — and the
jump drive leads the fleet at 9.9, settling at 1,211 K against a 689 K rating on 2,254 ships. The
triage table's other column is the one that says whether anything can be done: a block's **index**,
against every face radiating and a path to armour at ambient, is **0.27** for the jump drive and
below one for all but one of 323 heat-making blocks. **So the runaways are coolable in principle**,
and the question is what it costs.

6.4 MW buried in the same 7-cube, in shadow, against a 689 K rating (`designed --sweep`):

| Loop | Sink W/K | Gradient it forces | Source, 3 panels |
| --- | ---: | ---: | ---: |
| none | — | — | 2,047.8 K |
| shipped, 1 sink face | 1,000 | **6,400 K** | 1,496.0 K |
| shipped, 3 sink faces | 3,000 | 2,133 K | 1,386.0 K |
| candidate, 1 sink face | 6,250 | 1,024 K | 1,163.8 K |
| candidate, 3 sink faces | **18,750** | **341 K** | 979.3 K |

**Watts over the pickup is the gradient the source is forced to sit at**, whatever is hung off the
other end — and that single quantity decides whether a fit can work at all. At the shipped
coefficient one sink face forces **6,400 K** on a 6.4 MW block, ten times its rating, so no amount of
radiator can help and the panel column duly saturates: panels 2 to 8 are worth 100 K between them
against the first panel's 506 K. **A 6.4 MW block is uncoolable as the mod ships, at any radiator
count.** Three sink faces and the `C38` package together take that forced gradient to 341 K, below
the rating, and only then do panels start earning again.

> **This is the case for the package, and it is the opposite of the retrofit's.** On a fitted hull
> `C38` was worth nothing, because the block had 1,457 W/K of hull to conduct into and the sink was a
> rounding error beside it. On a buried source the sink is the *only* path there is, so the same
> change is the difference between a block that cannot be cooled and one that can. Both measurements
> are right; they are about different ships.

**It still does not save the drive.** Run out to twelve skin panels the best arm reaches 807.7 K,
with each panel worth less than the last — 59 K for the second, 11 K for the twelfth — as the limit
moves once more, to what the ring can carry to panels far along it. What cooling design buys a jump
drive is **2,047.8 K down to about 808 K**: not survival, but the difference between a block that is
197 % over its rating and one that is 17 % over, which is the difference between losing it at once
and taking slow damage a player can see coming and decide about.

> **The face count is not a lever, by design.** A pipe carries at most two sink faces, a pump none,
> and the loop is meant to be routed and paid for in space rather than wrapped around a hot block
> until it stops being one — see [blocks.md](blocks.md#coolant-pipes). The three-face row above is
> what a ring already gives you for free when a rectangle runs past a one-cell source; it is a
> measurement of the geometry, not a proposal to loosen it. **So the pickup has exactly one dial,
> the coolant's coefficient**, and the table says what that dial decides.

Taking the constraint as fixed and reading the same arithmetic the other way — what a realistic run
of one to four sink faces can carry against a 6.4 MW block rated 689 K:

| Sink faces | Shipped, W/K | Forces | Candidate, W/K | Forces |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 1,000 | 6,400 K | 6,250 | 1,024 K |
| 2 | 2,000 | 3,200 K | 12,500 | 512 K |
| 3 | 3,000 | 2,133 K | 18,750 | 341 K |
| 4 | 4,000 | 1,600 K | 25,000 | 256 K |

**At `h` 160 no realistic plumbing brought a 6.4 MW block under its rating** — four sink faces still
forced 1,600 K against 689 K, and wrapping a one-cell source in all six it geometrically has only
reached 1,067 K. At 1,000, two faces are enough for the pickup to stop being what binds.

#### What was decided, and what it cost

**`h` is now 1,000 while the ring circulates and `StagnantTransferFraction` is 0.16, so a stopped
ring carries `1000 × 0.16 = 160` — exactly what every ring in this mod carried before.** The pair is
pinned as a product by `AStoppedRingCarriesWhatEveryRingCarriedBeforeTheRetune`, because moving one
without the other silently makes a stopped ring better or worse than it has ever been.

It was decided on one argument. The pickup has a single dial, since the sink-face count is fixed by
design; watts over the pickup is the gradient a buried source is forced to sit at whatever is hung
off the far end; and at 160 that gradient was **ten times the rating** of the block carrying 65.5 %
of a loaded fleet's heat on 2,254 published ships. A block with no answer at any radiator count is a
permanent runaway, and avoiding runaways is what the mod is for — so the shipped value contradicted
the goal, and the design thesis `C41` measured could not actually be built.

Measured after the change, on the same rigs:

| | Before | After |
| --- | ---: | ---: |
| sink face, large grid | 1,000 W/K | **6,250 W/K** |
| gradient forced on 6.4 MW, one face | 6,400 K | **1,024 K** |
| that block under four skin panels | 1,480.7 K | **1,285.8 K** |
| a stopped ring, any grid | 160 W/(m²·K) | **160 W/(m²·K)** |
| corpus retrofit, plumbed p50 | 2.43 % | **2.44 %** |
| 500 kW block, 28-pipe ring | 740.9 K | **588.3 K** |

**The population did not move**, which is the point rather than a disappointment: `C40` bounded every
internal path at 9 % of a hull's peak, and a retrofit is an internal path. What changed is what a
player who plumbs deliberately can build — and, at the other end, nothing at all for a ship whose
pumps are off.

> **What it does not do is save the drive.** 1,024 K is still above 689 K, and twelve skin panels
> reached 807.7 K in the sweep that argued for this. The block goes from *no answer exists* to *an
> answer exists and costs a lot of plumbing*, which is the difference the mod is about.

#### The coolant mass is doing an undeclared job, and the measurement that said so was wrong

The coolant charge shipped as a flat 50 kg a pipe with no grid size in it — **3.2 kg/m³ in a 2.5 m
cube, which is a gas, against 400 kg/m³ in a 0.5 m one, which is a liquid**, and on a small grid it
is more fluid than the 32 kg pipe block carrying it weighs. That is the same defect the coefficient
was corrected for when it stopped being a conductivity divided by half a cell, and correcting it the
same way means a fixed density with the mass following the cell: 33 kg/m³ is a bore a fifth of the
cell across, filled with water-glycol, which gives 515.6 kg on a large grid and 4.1 kg on a small
one. **That is `CoolantKilogramsPerCubicMetre`, and it ships**; `CoolantMassPerPipe` is still read
and still means kilograms in a pipe, for a file or a world that states one (`P15`).

**The four figures this section published for that correction were readings of a ramp.** They were
taken on a ring with a 125 kW source, the environment disabled and nothing else — so the rig had a
heat source and no sink of any kind, nothing settled, and every arm climbed linearly and forever.
Read at step 400 they said 715.5 K and 387.0 K on a large grid and 661.5 K and 912.1 K on a small
one. Read at step 25,600 the same arms say 26,836 K and 5,908 K. What that rig measured was the
ratio of two heat capacities, which is exactly what coolant mass *is* — so the answer looked like
physics and was a stopwatch reading, and it looked like a 328 K gain and a 250 K loss because more
fluid warms more slowly (`P2`).

With the environment on the ring radiates and every arm settles by about step 1,600. Measured there,
on the same rig:

| | Coolant | Ring mean | Hottest segment | Swing | Substeps |
| --- | ---: | ---: | ---: | ---: | ---: |
| large grid, the flat charge | 50 kg | 767.7 K | 836.6 K | 100.2 K | 14.51 |
| large grid, **as it ships** | 515.6 kg | **774.6 K** | **779.9 K** | **10.0 K** | 14.51 |
| small grid, the flat charge | 50 kg | 1,551.9 K | 1,558.1 K | 7.7 K | 2.38 |
| small grid, **as it ships** | 4.1 kg | **1,552.7 K** | **1,630.6 K** | **94.6 K** | 2.38 |

*Mean and swing at ten times the reference load, where a swing is large enough to read; the substep
columns are at 125 kW. `LoopCoolantMassTests` holds all of it.*

**Coolant mass buffers a ring; it does not decide where the ring runs.** At steady state the mean is
set by what comes in against what radiates out, and a capacity that has finished charging is in
neither — the mean moves 6.9 K on a large grid and 0.8 K on a small one. What moves is the *swing*
between the sink face and the far side of the ring, and it moves the way a fixed density has to move
it: a large pipe gains fluid and buffers ten times better, a small one loses fluid and buffers twelve
times worse. That is the correction's real effect, and it is the half a player can see, because the
hottest segment is the face a bolted block is coupled to.

**So the objection that blocked it is gone, and it is applied.** The objection was *half of this is a
regression on the grid size that is already the harder case*, and both halves of that are measured
false: the loss is 12.6 K at the reference load rather than 250 K, and the harder case is the next
section.

#### What a small cell is behind on, and what it is not

This page argued, from the arithmetic of a cell face alone, that "the arithmetic that decides
everything else on this page is against small grids by about 2.5×": a small cell face is 0.25 m²
against 2.5 m², so the pickup is 25× worse, while a small-grid block makes perhaps a tenth of what
its large counterpart does. **The first half is exact, the guess in the second half is very nearly
right — the median paired block makes a *eleventh* — and the conclusion drawn from the two is wrong**,
because every path heat takes off a block scales with a different power of the cell edge and the
sentence quietly applied one of them to all of them.

Measured per block over the eighty families the game ships at both sizes, from the definitions alone
(`CellSizeLab`), as a handicap per watt where 1 is parity and above 1 is behind:

| Term | Scales as | Small-grid handicap |
| --- | --- | ---: |
| a block's own radiating skin | `g²` | 2.12 |
| the loop pickup, `h·A` over a sink face | `g²` | **2.28** |
| conduction into the hull, `kA/L` | `g` | **0.41** |

**The area terms are behind by about 2.2× and the conduction term is 2.5× ahead**, and conduction is
the path the shipped mod actually uses: a block exports into the hull it is welded to, and the hull
radiates. `tools/corpus/cellsize.py` reads the other half off the 8,137-ship census and says the same
thing louder — every column favours the small grid.

| Over 4,061 small-grid and 4,076 large-grid ships | small p50 | large p50 | small/large |
| --- | ---: | ---: | ---: |
| exposed area per kilowatt, m² | 7.94 | 3.21 | **2.47×** |
| flux over that skin, W/m² | 124.5 | 283.1 | 0.44× |
| share of blocks with no exposed face | 0.048 | 0.211 | 0.23× |
| cells from the deepest heat source to air | 1 | 3 | 0.33× |
| the hottest block's path to its hull, W/K per kW wasted | 18.76 | 0.95 | **19.75×** |

*Basis: the census's own — full electrical load, every jump drive charging, no thrust, stores in
reserve. Nothing restated.*

**A small-grid ship carries two and a half times the skin per kilowatt, buries a fifth as much of its
heat behind it, and gives its hottest block twenty times the path out per watt.** The last row is the
one worth reading twice, and it is the trap the original sentence fell into: the raw conductance
column says 0.77× and reads as *small grids behind*, because it is a ratio of two things that scale
differently. Per watt it is the reverse by a factor of twenty.

#### So small grids do not get their own pickup coefficient

The handicap that survives is the pickup's, and `C42` fixed the test for whether it matters: watts
over the pickup is the gradient a block is forced to sit at whatever is hung off the far end, and a
gradient past its own rating is a block no radiator count can reach.

| Sink faces | Small-grid families with no answer | Large-grid |
| ---: | ---: | ---: |
| 1 | 9 | 3 |
| 2 | 5 | 0 |
| **3** | **1** | **0** |

Three is what a routed rectangle gives past a one-cell source — `C42`'s figure, and not a proposal to
loosen anything, since a pipe carries at most two sink faces and a pump none. **At that face count 79
of the 80 paired families have a cooling answer on a small grid exactly where they have one on a
large grid.** The exception is the small prototech jump drive, whose large twin is this page's
standing example of a block that survives only as slow damage.

**Buying that one block costs a ×5.63 coefficient on every small-grid loop in the game**, against a
median block that needs ×2.28 — so it overshoots parity by two and a half times for everything else.
And the handicap is not a grid-size effect to begin with: it runs from **0.83 to 25** across the
paired families, because it tracks how much less heat the game's authors gave each small variant. The
small reactor is already *better* off than the large one; the small battery is eight times worse. A
grid-size dial cannot fix a per-block spread.

> **The one rung here that is honest physics is worth nothing, and is refused on that.** Fluid-to-wall
> transfer goes as `Nu·k/D`, and under Dittus-Boelter at the flow the mod ships that is `h ∝ D^-0.2`;
> a small-grid bore is a fifth of a large one, so its coefficient is `5^0.2` = **1.38× larger for the
> same fluid**. Applied, it takes the families with no answer at one sink face from nine to eight and
> changes no verdict at three. Unobservable fidelity is cost (`P14`), so it is priced here and not
> built. `CellSizeTests` holds the derivation and the count.

#### Where the balance work goes

A retrofit is bounded at 9 % and a design is not, so the lever with headroom is whatever makes
players build the loop — installability, and what it is worth once built — rather than any dial on
the fluid's *transport*. But the fluid's **coefficient** is a different matter: with the face count
fixed by design, it is the only thing that sets the pickup, and the pickup is what decides whether a
block has an answer at all.

**A pump should be what makes this true.** Fluid-to-wall transfer is convective, so it depends on
the flow, and nothing in the model expressed that until `C37`: the coefficient applied whole whether
or not anything circulated. `LoopStagnantTransferFraction` now scales it, so a raised `h` can be the
*pumped* number with today's 160 as the stopped one, and a pump earns its power by making the ring
conduct rather than only by mixing it. It ships at 1 and nothing behaves differently yet.

### What a selective surface is worth

A surface is not obliged to absorb what it emits, and a radiator is the one block where that matters
most: its job is to emit in the infrared without collecting in the visible. Real ones are finished
for exactly that — a second-surface mirror runs `α ≈ 0.08` against `ε ≈ 0.8`, white paint about 0.2
against 0.9. The mod's radiator was authored at `ε 0.35` with no absorptivity, so it absorbed a
third of the sunlight that landed on it. Both halves are now authored: `α 0.10`, `ε 0.85`.

Measured on a source under eight radiators, 75 kW of heat, sun across the stack against the same rig
in shadow — `dotnet run --project tests/Thermodynamics.Sim -- bench surface`:

| Surface | ε | α | Sunlit | Shadow | The sun costs |
| --- | ---: | ---: | ---: | ---: | ---: |
| shipped, before | 0.35 | *follows* | 355.6 K | 319.0 K | 36.5 K |
| **shipped, now** | **0.35** | **0.10** | **344.7 K** | **319.0 K** | **25.7 K** |
| second-surface mirror | 0.80 | 0.10 | 335.3 K | 310.8 K | 24.5 K |
| emissive only | 0.80 | *follows* | 353.7 K | 310.8 K | 42.8 K |

**The selective finish is worth 10.9 K in sunlight and exactly nothing in shadow**, which is what
says the rig is measuring the surface rather than the geometry. It is authored on both radiators, so
the block stops absorbing sunlight a real one would not.

> The table's `shipped, now` row was taken when only the absorptivity had been authored. The
> shipped block is the `second-surface mirror` row to within the 0.05 between them.

**The last row is the one worth reading twice.** Raising emissivity alone — making the radiator a
better *emitter*, which is the obvious improvement — buys 8.2 K in shadow and **1.9 K in sunlight**,
because a better emitter that is also a better absorber gives almost all of it back: the sun's
penalty rises from 36.5 K to 42.8 K. The two changes are complementary rather than additive, and
doing only the obvious one is nearly worthless where a radiator is most often used.

**The emissivity is now authored too, at `C36`.** It was the last line of this section for two
passes — *0.35 is low for a radiator and 0.8 is what a real one reaches* — held back as a balance
change rather than a fidelity correction. It is both, and the fidelity half decided it: a selective
surface is low-α **and** high-ε, the definition already named the two finishes it is modelled on,
and it had taken the absorptivity from them while leaving emissivity at a number nearer bare
aluminium. The block was emitting 41 % of what its own stated finish emits. It ships at **0.85**.

What it bought, measured rather than argued:

| Where | Before | After |
| --- | ---: | ---: |
| one panel on the 200 kW source | 228.6 K saved | 290.4 K saved |
| one panel on the ladder's reactor | 20.9 K saved | 27.6 K saved |
| thirty-two panels on that reactor | 25.6 K | 31.0 K |
| corpus hulls, bolted retrofit, p50 | −0.32 % | −0.20 % |

A third more cooling per panel on a synthetic source, and a tenth of a per cent on a real hull. That
gap is the finding, not the gain — see *The joint, not the panel* above. `SelectiveSurfaceTests`
pins both halves.

### The heat pump

Large grid, cold side 300 K. Which of the three limits binds changes twice across an ordinary range,
which is the block's whole character:

| Gap | Coefficient | Lifted | Drawn | Binding |
| --- | --- | --- | --- | --- |
| 5 K | 8.00 | 60 kW | 7.5 kW | coefficient cap |
| 20 K | 6.00 | 60 kW | 10 kW | rating |
| 40 K | 3.00 | 60 kW | 20 kW | rating |
| 100 K | 1.20 | 24 kW | 20 kW | carnot |
| 400 K | 0.30 | 6 kW | 20 kW | carnot |

Pinned by `TheHeatPumpPassesThroughAllThreeOfItsLimits`.

### Coolant rings

A 500 kW block with one sink face, rings of rising size:

| Pipes | Coupling | Settles |
| --- | --- | --- |
| 8 | 56,250 W/K | 722.7 K |
| 12 | 81,250 W/K | 667.0 K |
| 20 | 131,250 W/K | 613.3 K |
| 28 | 181,250 W/K | 577.5 K |

Each pipe adds 6,250 W/K of its own **and its own charge of fluid**, so a longer ring is a harder
coupling and a bigger buffer at once and **is strictly better**. Pinned by
`LongerRingsDeliverColderBlocks`.

> **Re-measured twice.** `C42` multiplied the pumped coefficient by 6.25 and took the whole table
> with it: the same rings were 9,000 to 29,000 W/K settling at 857.2 K down to 740.9 K. `C43` then
> gave a large-grid pipe 10.31× the coolant, which **left the coupling column identical** — mass is
> not in a conductance — and moved the settled column by 4.4 K at eight pipes and 10.8 K at
> twenty-eight, from 727.1/673.0/622.1/588.3. The shape is unchanged through both, and it is the
> shape the claim rests on.
>
> The sentence above used to read *the fluid mass does not grow*, which was true when the charge was
> per loop and has not been since it became per pipe. Nothing about the conclusion depended on it.

### What the real-unit conversion moved

`Conductivity` used to be a 0…1 quality against a 200 W/(m·K) reference and is now the figure a
materials table gives, times `ThermalConstants.ConductionScale` = 2.4 — calibrated so mild steel
lands exactly where it was ([definitions.md](definitions.md#conductivity-is-in-real-wmk)). The
question [backlog.md](backlog.md) `C2` held open was what that did to the balance a player meets, and
it is answered below: the two figures it turned on are censored and everything else is unmoved.

**The old world is recoverable exactly, which is what makes this a measurement.** `Data/Cubes.xml`
at `4f6b44a^` held twenty-two definitions and derivation from build components arrived after the
conversion, so before it every block in the game took one of two conductances: **120 W/(m·K)** from
the 0.6 fall-through, and **200** for `Thrust`, `Reactor` and the mod's own nineteen blocks at
quality 1.

**Where it landed, block by block.** Measured off the shipped definitions; the full table is in
[definitions.md](definitions.md#conductivity-is-in-real-wmk) and these are its ends:

| Block | Before | Now | Change |
| --- | ---: | ---: | ---: |
| light and heavy armour | 120 | 120.0 | **1.00×** — the calibration |
| large ion thruster | 200 | 45.3 | **0.23×** |
| large reactor | 200 | 103.2 | **0.52×** |
| battery | 120 | 61.2 | **0.51×** |
| jump drive | 120 | 421.1 | **3.51×** |

The published table had four rows and described only the families `Cubes.xml` authors, so both of
those extremes were unrecorded — and one of the four rows was wrong, because *thrusters went 0.6×*
is true of the hydrogen ones and of nothing else.

#### The two moves the corpus cannot see

Coolant pipes went 4.8× and radiators 2.84×, and **not one of the forty ships in the retest set
carries either**: the corpus filters admit vanilla hulls, so the walk's own reach column reports that
arm as reaching **0 of 177,822 blocks**. Both are priced on a rig instead —
`dotnet run --project Thermodynamics.Sim -- conductance`, one family moved per rig with everything
else left shipped, a large reactor at 300 kW in shadow:

| Rig | Before | Now | Source settles | Saved |
| --- | ---: | ---: | ---: | ---: |
| 1 radiator | 200 | 568.8 | 333.8 → 328.4 K | **5.34 K** |
| 8 radiators | 200 | 568.8 | 328.7 → 316.6 K | **12.07 K** |
| 32 radiators | 200 | 568.8 | 328.6 → 316.2 K | **12.44 K** |
| 12-pipe ring | 200 | 960 | 333.2 → 333.1 K | **0.13 K** |
| 20-pipe ring | 200 | 960 | 319.9 → 319.8 K | **0.06 K** |
| 28-pipe ring | 200 | 960 | 310.7 → 310.7 K | **0.02 K** |

**The 4.8× on coolant pipes is the largest number the conversion produced and the smallest effect it
had.** The fluid couples to its pipe through `LoopThermalProperties.Conductivity`, a 0…1 quality
against `ThermalConstants.ReferenceConductivity` that the conversion never touched; the pipe's own
material decides only what crosses between the pipe block and whatever is bolted to it. Copper made
the pipes better conductors and left the loop carrying exactly what it carried.

**The radiator's 2.84× is real and it saturates.** It is worth 5.3 K on one radiator and 12.4 K by
sixteen, and it stops there because a stack's limit is the area it radiates from rather than the
rate heat reaches it — the visible sign is that the far end of the stack is now *hotter* (43.0 K
against 59.0 K at thirty-two), which is the heat getting further up before it leaves.

Pinned by `ModHardwareRetestTests`, which first holds `Catalog`'s pipe and radiator conductances
against the shipped ones, so a rig cannot quietly become a measurement of a block the mod does not
ship (`C4`).

#### What it did to the ships people fly

Forty hulls from [the retest set](../tools/corpus/README.md#the-retest-set) — each carrying thrust,
power, an airtight room and 100 kW of load, nearest the population median on the four quantities the
census found decide an outcome — run through seven scenarios in the shipped world and in four
counterfactual ones. 1,400 runs, about thirty-five minutes, `ConductanceRetestWalk` and
`tools/corpus/retest.py`. **17.8 % of the 177,822 blocks on the set changed conductance at all**;
the rest derive as steel and sit where the calibration put them.

Shipped minus pre-conversion, median over the forty. Positive means the shipped world is the hotter
one:

| Scenario | composite | vanilla blocks only | thrust and reactors only | mod blocks |
| --- | ---: | ---: | ---: | ---: |
| idle | +0.00 K | −0.11 K | +0.08 K | 0.00 K |
| vacuum-sunlit | +0.03 K | −0.02 K | +0.01 K | 0.00 K |
| recovery | +0.11 K | −0.06 K | +0.14 K | 0.00 K |
| **full-electrical** | **−16.28 K** | **−16.27 K** | +5.12 K | 0.00 K |
| **burn-forward** | **+34.73 K** | −3.04 K | **+44.90 K** | 0.00 K |
| flight-100 | +26.40 K | −2.40 K | +28.35 K | 0.00 K |
| flight-300 | +17.94 K | −2.08 K | +19.46 K | 0.00 K |

**Read the two bold rows as censored, because they are.** The lab never destroys an overheating
block, so from the moment anything on a hull is past its own rating the peak is 1,800 s of an
undamped source rather than a temperature (`E9`). In `burn-forward` that is **34 of the 40 hulls**
and in `full-electrical` **29 of 40** — the two rows the table sets in bold are the two where the
peak has stopped being a prediction. The censoring report in `retest.py` said 9 % of the run was
censored because it tested the peak against 1,500 K rather than against each block's own rating;
that is corrected, and it is the reason the two figures below were read as balance rather than as
artefacts.

**Where they come from, for what it is worth.** Thrusters and reactors alone account for 44.9 K of
the burn row, and the vanilla blocks gaining conductance claw about ten of that back; the load row is
the vanilla blocks entirely — the jump drive at 3.51× is the block type carrying 71.3 % of the
population's full-load waste, and it now spreads what it makes. Nothing moves at idle, in sun, or in
recovery, because none of those is a hull with a hot block in it — and those three rows are the
uncensored ones.

**What actually changed for a player is not the peak, it is the spread.** Blocks over critical,
summed across the set:

| Scenario | shipped | pre-conversion | change |
| --- | ---: | ---: | ---: |
| full-electrical | 1,461 | 1,269 | **+192**, on 21 of 40 hulls |
| burn-forward | 1,354 | 1,611 | **−257**, on 25 of 40 hulls |

Both rows run against their own peak column. Under load the peak fell 16 K and 192 *more* blocks
went over critical, because a jump drive that conducts 3.5× better pushes its heat into blocks that
used to stay cold. Under thrust the peak rose 35 K and 257 *fewer* did, because an ion thruster at
0.23× keeps its heat to itself. **The conversion traded peak temperature for spread, in both
directions at once**, and a headline temperature alone would have reported each of those backwards.

**Both totals are dominated by single hulls and the per-hull split is a wash.** Under thrust the
shipped world is better on 14 hulls, worse on 11 and unchanged on 15, with a median of exactly zero
and one hull carrying −88 of the −257 (`P1`: a sum over a population is not a statistic about its
members).

**Every statistic that survives the censoring is unmoved**, which is the finding the peaks were
standing in front of. Shipped minus pre-conversion, medians over the forty:

| | burn-forward | full-electrical |
| --- | ---: | ---: |
| seconds to the first block over critical | **−0.13 s** | +0.75 s |
| seconds to the first block lost | **+0.50 s** | +1.13 s |
| share of blocks over critical | **0.000** | 0.000 |
| peak temperature *(censored)* | +34.73 K | −16.28 K |

The crossing and the loss are decided before anything diverges, and both are inside a second and a
half against a damage event that runs a median 37 s
([how long a block has](#how-long-a-block-has-after-it-crosses)). The share of blocks past their
rating does not move at all. **So the conversion moved the one column that is not a prediction and
left every column that is** — which is what `C2` was deciding about, and is why it is closed.

**And no criterion moved.** G1, G2 and G5 read identically in all five worlds — 0 of 40 critical at
idle, 40 of 40 warm under load, 39 of 40 recovering. The set is not the population and its G2 is
100 % against the corpus's 65.5 % because membership requires 100 kW of waste; what the identical
columns say is that a change worth tens of kelvin did not reach a threshold the balance is judged
on.

**The null arm is a control and it earned its fifth of the run.** The arm for the mod's own blocks
retunes nothing on a vanilla corpus, and all 280 of its rows came back bit-identical to the shipped
world — so the walk's zero is a measured zero rather than an arm that failed to install (`E8`), and
the harness is deterministic across independently built worlds.

**Read the peaks against the censoring.** One run in eleven on this set — 8.9 % of the shipped
world's 280 — ends past 1,500 K, which says *ran away* and nothing finer, so the findings above rest
on medians and counts and not on any hull's extreme.

### Reactor waste heat

A reactor's fraction has to hold across three orders of magnitude of rated output — 0.5 MW on a
small-grid small generator against 300 MW on a large-grid large one — while the block it heats is
the same size in both grids. It cannot be picked by analogy with the thruster's 0.25, which was
chosen against a 33 MW draw, so it is measured in two rigs that bracket the answer:

* **bare** — one reactor alone in shadow, every face radiating to a 2.7 K sky. The coolest a reactor
  can possibly run, so a fraction that cooks here cooks in every build and no plumbing reaches it.
  **This sets the ceiling.**
* **skinned** — the same reactor under one cell of light armour, which is how one is installed. A
  fraction the skinned rig survives at full rating is a fraction nobody ever has to cool. **This
  sets the floor.**

> **Ceiling and floor are true of a reactor and not of the rig** (corrected 2026-08-25, `E10`). At
> a 300 MW reactor's watts the conductance out of the block is the bottleneck, so one cell of armour
> traps more than it sheds and skinned is the hotter of the two. Three orders of magnitude down, it
> is not: an oxygen generator at rated draw settles **267 K cooler** skinned than bare, because the
> shell is a radiator several times the block's own area and conduction into it is nowhere near
> binding. The rigs are *no hull* and *one cell of hull*; which is hotter is a property of the block
> under test. See [Oxygen generator waste heat](#what-it-did-the-invention-was-the-value-that-could-not-be-built-and-two-of-four-predictions-fail).

Both rigs at four candidate fractions, at full rating, against a 1,200 K critical temperature. Bold
is past critical:

| Fraction | Small gen (15 MW) | Large gen (300 MW) | SG small (0.5 MW) | SG large (14.75 MW) |
| --- | --- | --- | --- | --- |
| 0.01 bare | 728.8 K | 889.9 K | 696.4 K | 937.0 K |
| 0.01 skinned | 603.5 K | **1,245.0 K** | 495.4 K | 971.0 K |
| 0.02 bare | 866.7 K | 1,058.2 K | 828.1 K | 1,114.2 K |
| 0.02 skinned | 800.3 K | **1,809.2 K** | 603.1 K | **1,240.5 K** |
| 0.05 bare | 1,089.9 K | **1,330.7 K** | 1,041.3 K | **1,401.1 K** |
| 0.25 bare | **1,629.7 K** | **1,989.8 K** | **1,557.1 K** | **2,095.1 K** |

0.05 and above put a reactor past critical *bare*, which is unbuildable — there is no arrangement
cooler than open space. **0.01 is the fraction where both bounds hold**: every reactor survives at
full rating with its faces on open space, and — at the pace this table was taken on — the 300 MW one
went past critical once wrapped in hull.

**The fraction stays at 0.01 and what it buys has shrunk** (`C28`, decided 2026-08-24). Those are
two separate findings and the second is the interesting one.

*The fraction stays* because it is still the only value where both bounds hold, and `C24` did not
move which value that is: re-measured at the pair that ships, 0.02 cooks two of the four reactors
**bare** — 1,114.2 K and 940.9 K against their own derived ratings — which is the bound this section
calls unbuildable, and lowering it only makes a small signal smaller. It is kept because the
alternative was measured, not for want of one.

*What it buys has shrunk.* This paragraph used to end: **"That makes where a reactor is installed a
decision rather than a detail, and it is the first thing in the mod that makes a player want a
coolant loop for a reason other than curiosity."** At `ConductionScale` 9.6 burying a 300 MW reactor
costs **50.7 K** rather than 355 K and both installations survive — about five per cent of the
block's headroom. Where a reactor goes is still a decision; it is no longer one that endangers
anything, and nothing about a reactor now makes a player want a loop. **The population had already
said so and nobody had read it against this claim**: `reactor-waste` swept sixteen-fold, from 0.50
to 8.00, moves the corpus peak p50 by 3 %. The bite is thrusters and drives — a charging jump drive
is 71.3 % of a loaded fleet's heat — and that is where the install decision and the loop both live.

> **The floor has gone, and `C24` is what removed it.** The table above was taken at
> `ConductionScale` 2.4; at the 9.6 that now ships, the armour a reactor is buried in carries its
> heat away and radiates it from its own faces, so the skinned 300 MW reactor settles at **940.6 K
> against a critical of 1,090.1 K** where it settled at 1,245.0 K. Burying it costs **50.7 K rather
> than 355 K**, and both installations survive. **Raising the fraction does not put the shape back**:
> at 0.02 the skinned case does cook, at 1,214.9 K, and so do two of the four *bare* — 1,114.2 K and
> 940.9 K against their own derived ratings — which is the bound this section calls unbuildable. So
> the signal is smaller rather than moved. It is a block-level signal either way: `reactor-waste`
> from 0.5 to 8.0 moves the corpus peak p50 by 3 %, and `G2` holds at 100 % of the retest set at the
> pair that ships. `C28` is decided on that evidence, above.

An idling ship is deliberately not a cooling problem: at 10% of rating every reactor stays clear of
critical in both rigs. **Heat arrives when power is drawn.**

**It is balance, not efficiency.** 0.01 implies a 99% efficient reactor, which no fission plant
approaches, and the figure should not be read as one. Space Engineers rates a 3×3×3 block at
300 MW — a power density about three orders of magnitude past any real plant — so a real plant's
efficiency of about a third would put 600 MW of waste heat into a 73-tonne box, settling near
2,000 K bare in vacuum: every large reactor in every world destroys itself the moment it is switched
on, in a build no player can improve. **Applying a real efficiency to a fictional rating compounds
the fiction rather than correcting it.** The fraction is chosen so the *consequences* land where
they should, which is the honest way round when one of the two inputs is already invented.

> The table above is the pre-derivation measurement, taken against a 1,200 K critical temperature
> typed into `Cubes.xml` by hand. Deriving a reactor's critical temperature from its own build cost
> instead puts the four of them between 938 and 1,090 K — fuel and graphite in a steel assembly, not
> a round number — and at 0.02 the two smaller reactors then cook themselves bare in vacuum, which
> is a state no build can improve on. A balance figure resting on a number nobody had checked is not
> a balance figure.

### Oxygen generator waste heat, written before it is measured

`C21` gave every waste fraction in `Cubes.xml` a provenance and left three inventions with a real
figure sitting beside them. One of the three closed on 2026-08-24 and one — the reactor's — was
decided and kept by `C28`. **The third is the largest gap in the file and is open**: an oxygen
generator wastes **0.6** of what it draws where water electrolysis runs 0.60–0.80 efficient and the
sourced figure is **0.20–0.40**. The question, the instrument, the decision rule and the numbers
that would falsify each prediction go here before the run (`E1`, `E11`).

**The question.** Should `OxygenGenerator.ConsumerWasteEnergy` move from the invented 0.6 into the
sourced band, and if so to which value in it?

**The instrument is the reactor rig pointed at a consumer.** `OxygenGeneratorLab` runs the same two
bounds — **bare**, one generator alone in shadow with every face radiating to a 2.7 K sky, and
**skinned**, the same block under one cell of light armour — because the same two bounds are what
decide it: a fraction that cooks bare cooks in every build, and a fraction the skinned rig survives
at full draw is one nobody ever has to cool. What changes is where the watts come from. A reactor's
heat is a fraction of what it *produces*; a generator's is a fraction of what it *draws*, and the
game states that draw per definition as `OperationalPowerConsumption` — 0.5 MW for the large-grid
block, into two cells.

**Three draws, and only one of them is invented.** `standby` and `operational` are both figures the
game's own definition states, so they bracket the block without an authored duty cycle in between.
The third is `observed` — the mean draw across 346 large oxygen generators in the 2026-08-21 field
dump, **31.5 kW of a 500 kW rating, a duty of 6.3 %** — which is one sample of what players
actually run and is labelled as one sample wherever it is quoted.

**Four predictions, each with what would falsify it.**

| | prediction | falsified by |
| --- | --- | --- |
| the ceiling | at 0.6 and operational draw the large-grid generator sits **within 100 K of its own critical temperature bare** | a bare margin over 100 K |
| the install | at 0.6 the same block is **past critical skinned** | a skinned margin at or above 0 K |
| the relief | at 0.30 it clears critical **skinned by more than 100 K** | a skinned margin under 100 K |
| the population | on the census ships that carry one, the generator is **under 5 % of that ship's full-load waste** at the median | a median at or above 5 % |

100 K is not a round number chosen here: it is where `ThermalGlow` starts, so it is the temperature
at which a block stops being a detail and starts telling the player about itself. A margin inside it
is a block a player watches.

**The decision rule, fixed now.** It has to settle both halves — whether to move, and where to — and
the two precedents in this repository point opposite ways. `C21`'s computer third **moved** because
the sourced value changed nothing a player could see: 0.9 to 1.0 over twenty-seven types was worth
0.025 % of a loaded fleet's waste. `C28` **kept** an unsourced 0.01 because the sourced figure made
every large reactor destroy itself in a build no player could improve.

1. **Unbuildable beats sourced and beats balance.** If 0.6 puts the block past critical *bare* at
   operational draw, it is not a balance choice but a block that cannot be built, and it moves
   whatever else holds — to the **highest** value in the sourced band that survives both rigs.
2. **Otherwise, provenance wins unless the move removes a decision.** It moves to **0.30**, the
   band's midpoint, chosen as the centre because the source gives a range and nothing distinguishes
   a point inside it.
3. **The move removes a decision** — and 0.6 is kept, with the reason recorded here as `C28`'s was —
   if at operational draw 0.6 is within 100 K of critical *skinned* and 0.30 is not. That is the
   case where where a generator is installed decides whether it survives at 0.6 and stops deciding
   anything at 0.30.

**What this rig cannot say.** It is four blocks in two synthetic arrangements: it says what a
generator does to itself, not what the population does with it, and the population half is the
census's alone. Neither half is a corpus walk, and no corpus walk is being run for this row —
`reactor-waste` swept sixteen-fold moved the corpus peak p50 by 3 % on a type carrying **3.65 %** of
a loaded fleet's waste, and the oxygen generator carries **0.38 %** with a change of half of that, so
a walk would be spending hours to measure a number smaller than the one already measured as very
nearly inert (`P2` — this is what the instrument was not asked, stated rather than hidden).

### What it did: the invention was the value that could not be built, and two of four predictions fail

**The fraction is 0.40 and the provenance is `waste: water electrolysis`.** The rule's first clause
fired, and it fired on something nobody was looking for: **at 0.6, two of the six vanilla oxygen
generators are past their own critical temperature bare, at the draw their own definition rates
them at.** `OxygenGeneratorSmall` and `SmallBlockOxygenGeneratorLab` settle at **905.3 K** against
criticals of 862.8 K and 848.6 K, alone in shadow with every face on a 2.7 K sky. There is no
cooler arrangement, so no build improves on it and no plumbing reaches it. That is the *unbuildable*
bound `C28` named for the reactor, arrived at from the other side: there the sourced figure was the
one that cooked and the invention was kept; here the invention was the one that cooked.

0.40 rather than the midpoint because the rule says the **highest** value in the band that survives
both rigs, and 0.40 does — with 30.5 K and 44.8 K on the two small-grid blocks and 99.9 K on the
vanilla large one. Those margins are inside the hundred kelvin `ThermalGlow` starts at, so a
generator run flat out glows. That is the outcome to want: the block tells the player it is working
hard, and does not then destroy itself.

At the shipped 0.40, one generator at its own rated draw, four hours to steady state:

| Block | cells | rated | bare K | margin | skinned K | margin |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `OxygenGenerator` (no subtype) | 2 | 0.50 MW | 783.2 | 99.9 | 533.8 | 349.3 |
| `IrrigationSystem` | 2 | 0.25 MW | 658.6 | 215.9 | 441.1 | 433.3 |
| `LargeBlockOxygenGeneratorLab` | 4 | 0.50 MW | 696.4 | 178.7 | 504.6 | 370.5 |
| `LargeBlockPrototechOxygenGenerator` | 6 | 1.00 MW | 643.1 | 722.1 | 577.6 | 787.6 |
| `OxygenGeneratorSmall` | 18 | 0.10 MW | 818.1 | 44.8 | 653.6 | 209.2 |
| `SmallBlockOxygenGeneratorLab` | 18 | 0.10 MW | 818.1 | 30.5 | 654.3 | 194.3 |

**Two of the four predictions hold and the two that fail are the interesting ones.**

| | prediction | outcome |
| --- | --- | --- |
| the ceiling | within 100 K of critical bare at 0.6 | **holds**, at **16.4 K** — and the prediction was aimed at the wrong block: two others were already past it |
| the install | past critical *skinned* at 0.6 | **falsified**, and inverted: skinned is **267 K cooler** than bare, not hotter |
| the relief | clears critical skinned by more than 100 K at 0.30 | **holds** at 390.4 K, trivially, once the install prediction inverted |
| the population | under 5 % of its ship's full-load waste at the median | **falsified by an order of magnitude**: the median is **48.1 %** |

**A skin cools a small heat source and cooks a large one, and this page said otherwise.** *Reactor
waste heat* above calls bare the ceiling and skinned the floor, and that reading is true of the
block it was measured on and not of the rig. At a 300 MW reactor's watts the block-to-block
conductance out of the block is the bottleneck, so one cell of armour traps more than it sheds. An
oxygen generator wastes three orders of magnitude less, conduction into the shell is nowhere near
binding, and the shell is a radiator with several times the block's own area — so it sheds. **The
two rigs are *no hull* and *one cell of hull*; which of them is hotter is a property of the block
under test.** `SkinningASmallHeatSourceCoolsItWhereSkinningAReactorDoesNot` pins it, because a
falsification that lives only in prose is one the next reader repeats.

**The population prediction failed because it was asked of the fleet and answered by the ship.** The
oxygen generator is **0.38 %** of a loaded fleet's full-load waste — the figure
[definitions.md](definitions.md#every-waste-fraction-says-where-it-came-from-and-most-of-them-say-invented)
quotes, and the reason this looked like a rounding error worth correcting rather than a balance
move. Asked of the ships that carry one, it is not: a ratio of aggregates has a charging jump drive
for three quarters of its denominator, and an aggregate of ratios over carriers only is the figure
about the player who built the block (`E6`). That much is the finding and it stands.

> **The figures this paragraph first carried were 48.1 % over 2,277 census ships, and they were
> measured through an instrument that could not see the block** (corrected 2026-08-25, `E10`).
> `Blueprints` resolved every empty `SubtypeName` to an armour cube, and the vanilla large oxygen
> generator is one of thirteen definitions the game gives no subtype — so **not one of them exists
> anywhere in the census**, and the 2,277 "carriers" were the ships carrying a small-grid or DLC
> generator instead. That is `A13`, fixed the same day and pinned by
> `ABlockWithNoSubtypeNameIsItsOwnTypesBaseVariantRatherThanArmour`. **The corpus was re-censused
> the same day** — 8,137 ships, taken twice and reproducing — and on the population: **5,184 of
> 8,032 ships carry a generator, 64.5 %, not 28.5 %**, and their median share is **14.4 %**, p75
> **43.4 %**, p90 **80.8 %**, p99 95.6 %. The 400-ship stride sample this was first corrected to
> agreed on the carrier share to the decimal and put the median at 10.4 %, a third low; the sample
> figures are superseded rather than wrong, and balance-lab.md records why a sample of 400 missed
> it. The claim that first stood here — *on an ordinary ship with no drive, an oxygen generator is
> most of the heat there is* — is **withdrawn**: it is most of the heat for the top tenth and a
> seventh of it in the middle.

So this is a balance change rather than the correction the row was filed as, and it is smaller than
the first reading of it said: **a third off a block that is a seventh of the median carrier's
full-load waste and four fifths of the top tenth's**.
`python3 tools/corpus/provenance.py out/census-2026-08-25/composition.csv --type OxygenGenerator`
computes it, and `Thermodynamics.Sim -- basevariants --ships 400 --type OxygenGenerator` is the
parse that answered it before the census was re-taken.

**The registration's reason for not running a walk was wrong, and it is left standing above.** It
argued that a type carrying 0.38 % of a fleet's waste cannot be worth hours of the machine when a
type carrying 3.65 % was measured as very nearly inert — and that is the fleet statistic this
section has just shown is the wrong one to reason from. The paragraph is not edited, because a
criterion and its reasoning are what a run is judged against and rewriting them afterwards is how
`E1` is defeated slowly (`E11`). **The walk is now an open row rather than a refused one**:
[backlog.md](backlog.md) `C31`, which also records that the standing panel cannot answer it —
6 of its 50 ships carry a generator by the census's reckoning, and the census cannot see the type's
largest member at all (`A13`), so the panel has to be rebuilt before it can be swept.

**What this did not measure.** No corpus walk was run, and
the census figures above are a *composition* rather than a simulation — they say what a ship's
blocks would waste at full electrical load, not what any hull settles at. The peaks are the rig's
six blocks alone in shadow and nothing else. Whether a third off a median 48 % moves `G2` on the
population is unmeasured and is the one question a walk would answer.

---

## The population

What the corpus datasets say when read for balance. Every number is measured and the measurement is
named; where the data cannot answer a question this says so rather than estimating.

> **Quote the figures from `verdict.py`, not from here or from memory.** The corpus is 8,142 hulls,
> 8,132 distinct name-and-id pairs and 8,054 distinct names, and every statistic is computed over
> one of the three. Naming which one is not pedantry — three pages of this documentation once
> carried three different figures for the jump drive's share of population waste, and all three were
> arithmetically correct over three different populations.

### The datasets

| Dataset | Covers | Does not cover |
| --- | --- | --- |
| `out/corpus-2026-08-21/` | 8,132 ships × 5 scenarios = 40,660 outcomes | any atmosphere, any motion, any cooling fitted |
| `out/census-2026-08-21/` | 8,141 ships screened, 109,312 composition rows | anything the solver does |
| `out/knobs-2026-08-21/` | 49-ship panel × 20 dials × 4–6 levels = 12,700 rows | any two dials at once |
| `out/blockheat/blockheat.csv` | 323 block types from the definitions alone | any hull, any arrangement |

All five corpus scenarios — `idle`, `vacuum-sunlit`, `full-electrical`, `burn-forward`, `recovery` —
are **in vacuum**. The knob sweep is the only dataset with air, wind or motion in it, and it runs on
49 ships rather than 8,132.

Two caveats apply to every figure below:

* **The 2026-08-21 dataset carries 50 duplicate rows.** A resumed run re-emitted the batch it was
  interrupted in, because the skip was counted in files while the writing was per ship, so ten ships
  are written twice. `verdict.py` drops them and prints the count. It is a tenth of a per cent and
  changes no finding, but a population statistic that silently double-weights part of its population
  is the failure this lab exists to prevent. The resume is now a record of finished blueprints
  rather than a count, so nothing collected since can carry them.
* **Peaks past critical are not physics.** The harness never destroys an overheating block, so a
  ship past critical keeps generating for the rest of the clock; the 541,648 K reading is 1,800 s of
  an undamped source. Crossing times and shares are unaffected. See
  [known-issues.md](known-issues.md#deliberate-limits).

### The shape, in one table

Deduplicated, the 2026-08-21 vacuum survey, at the settings that shipped when it was run:
`ConductionScale` 2.4 and `HeatTimeScale` 225. **That is not the pair that ships now** — `C24` moved
it to 9.6 and 90 on 2026-08-24 — so every seconds column below is a reading of a configuration this
mod no longer has, kept because the *shape* it shows is what the argument for the retune rests on
and no walk has yet re-read it in vacuum. Seconds are simulated seconds; at `SimulationSpeed` 1 they
are the seconds a player waits.

| Scenario | peak p50 | critical | s to critical p50 | settle p50 | substeps p50/p95/p99 |
| --- | ---: | ---: | ---: | ---: | --- |
| `idle` | 178 K | 0.22% | 104.5 | 1,560 | 2.31 / 3.25 / 3.58 |
| `vacuum-sunlit` | 247 K | 0.22% | 102.7 | 600 | 2.34 / 3.25 / 3.62 |
| `full-electrical` | 632 K | 43.2% | 8.9 | 300 | 2.36 / 4.30 / 6.02 |
| `burn-forward` | 1,087 K | 67.4% | 5.6 | 120 | 2.38 / 3.80 / 4.89 |
| `recovery` | 207 K | 3.0% | 4.0 | 1,320 | 2.31 / 3.25 / 3.63 |

**The distribution is bimodal, not spread.** A ship either sits near ambient indefinitely or runs
away in under ten seconds, and the 2–5 minute window the balance target asks for lands in the gap.

### The compatibility floor holds

`G7` asks that a ship the game spawns survives arriving. Every prefab in the install — 705 files,
461,428 blocks — run idle in the environment its category spawns into, for five simulated minutes:

| | Prefabs | Cross critical | Lose a block |
| --- | ---: | ---: | ---: |
| **arriving** (idle) — this is `G7` | 705 | **0** | **0** |
| flown hard (everything on) — the control | 705 | 630 | **616** |

**The second row is what makes the first one a measurement.** A floor that can only ever pass has
not been tested, and running the same ships under load is how this one is shown to be able to fail.
The distance between the two rows is the whole of what the mod is for: a ship that arrives is safe,
and a ship that is worked is not.

**Where the failures land under load** is by category, and it is not uniform: 22 of 23 cargo ships,
45 of 47 drones and 2 of 2 respawn ships lose a block, against 28 of 41 unknown signals and 32 of 46
planetary encounters. The planetary ones are the mildest, which is convection doing its work.

**Two things the walk cannot see, and they are part of the result.** 1,503 blocks are subtypes the
installed game no longer defines — coloured legacy armour, every one in `LegacyContent` — so they
are not simulated at all. And one prefab is stored gzipped; the parser un-gzips it now, and before
that it was one ship the floor had never been measured on.

Run it with `dotnet run --project tests/Thermodynamics.Sim -- prefabs`, and `--load` for the
control. `PrefabWalk` holds both in the suite over a stride of 140.

### How fast a ship crosses critical

Of the ships that cross critical at all, seconds from the start of the run to the crossing —
computed over the deduplicated outcomes, counting only ships that cross:

| Scenario | Ships crossing | p10 | Median |
| --- | ---: | ---: | ---: |
| `idle` | 18 | 17.6 s | 104.5 s |
| `full-electrical` | 3,516 | 3.5 s | **8.9 s** |
| `burn-forward` | 5,481 | 2.9 s | **5.6 s** |

**These are seconds of play, not of physical time.** `HeatTimeScale` was 225 when this was measured
and is 90 now, so nine seconds at the controls was about thirty-four minutes of real heating and is
about fourteen today. That compression is the point of the dial — heat is meant to happen on a human
scale — and this is the first measurement of where it put the hottest designs.

**Lowering the dial is not the answer**, because it moves the inert ships too and the spread between
the two groups is only about twelvefold. What separates them is per-block: a producer whose waste
watts are large against its own heat capacity crosses critical almost immediately whatever the clock
says.

**The crossing is not the damage, and for a long time this page said it was.** It is the moment the
damage rate leaves zero — the solver's rule is `(T − critical) × OverheatDamagePerKelvin`, which at
the crossing is nothing at all. What a player loses is a block, and that is the section below.

### How long a block has after it crosses

Measured on `out/corpus-2026-08-23-loss`, 798 ships, the first dataset to carry
`seconds_to_first_loss` — the figure the harness has computed since 2026-08-22 and no run had
recorded. Paired per ship before the difference is taken (`E6`), because the gap between two
percentiles of two populations is not a percentile of the gap:

| Scenario | Crossing p50 | First loss p50 | Own gap p10 | Own gap p50 | Own gap p90 |
| --- | ---: | ---: | ---: | ---: | ---: |
| `full-electrical` | 9.0 s | **37.0 s** | 7.7 s | **24.2 s** | 54.8 s |
| `burn-forward` | 5.5 s | **23.0 s** | 4.2 s | **13.0 s** | 66.6 s |

**Add the warning's own lead and the window is what the goal asked for.** `HeatWarning` sounds three
seconds before the crossing, so the median ship under full electrical load gives its pilot **40
seconds** between the first cue and the first block gone, and 26 seconds in a hard burn. At p10 it
is 18 and 12 seconds. `G5`'s rationale — *a player must be able to react to a warning* — holds when
it is measured against the event that costs the player something.

**The crossing still predicts the loss**, which is what makes it a fair thing to warn on: of the
ships that cross, 98.7 % under full electrical load and 97.0 % in a burn go on to lose a block
before the clock runs out. It arrives about four times too early, not wrongly.

**Why the sample is a population figure.** 798 ships against the 8,054 of the full survey, taken as
a stride through the size-sorted corpus, and it reproduces the run it is a sample of on the quantity
both measure: `burn-forward` crossings at p10/p50/p90 of 2.8/5.5/30.3 s against 2.9/5.6/31.0 s, and
`full-electrical` at a median of 9.0 s against 8.9 s. The one place it does not is `idle`, where two
ships cross rather than eighteen and nothing can be read from either.

**The damage dial is the reason the window exists, and it was not chosen for the rule it now runs
under.** The solver used to apply the whole overshoot as damage on *every update*, so at the shipped
`Frequency` of 8 it bit eight times harder than it does now — and `OverheatDamagePerKelvin` was
authored under that rule. Re-running the 72 shipped types that cross at eight times the dial says
what restoring their authored intent would cost:

| Damage per kelvin | p10 | Median | p90 |
| --- | ---: | ---: | ---: |
| as shipped, per simulated second | 8.7 s | **25.1 s** | 238.4 s |
| as authored, per step at `Frequency` 8 | 2.8 s | **6.5 s** | 35.3 s |

**So the authored values stay.** Under the rule they were written for the median block's entire life
past its rating is six and a half seconds, which is the failure `C11` was opened for and `G5`
forbids. This is recomputed rather than scaled, because the dial is not a square root: a block that
settles just over its limit grinds down at a constant rate, so the slow end loses 6.8× where the
fast end loses 3.1×. Pinned by `TheAuthoredDamageRuleWouldPutTheWholeEventInsideAboutTenSeconds`.

The per-block-type counterpart is [`BlockHeatIndex.SecondsFromCriticalToLoss`](../tests/Thermodynamics.Harness/BlockHeatIndex.cs),
which integrates the same damage rule against a block's own hit points with the block alone in the
dark: of the 72 shipped types that cross at all, the median survives **24.7 s** past its rating and
the tenth percentile 8.7 s. A ship lasts longer than its worst block because its neighbours are
taking heat off it, and shorter than its median block because the first loss is the fastest of
thousands.

### One block type decides the load criterion

The jump drive's effect on the outcome is binary, and the figures depend on which population is
meant:

| Population | Blocks | Ships | Share of full-load waste | Per block |
| --- | ---: | ---: | ---: | ---: |
| `LargeJumpDrive` alone | 10,567 | 2,183 | **67.1%** | 4.80 MW |
| all `JumpDrive` subtypes | 10,954 | 2,253 | **71.3%** | — |

Read over the 8,132 deduplicated `full-electrical` outcomes, with membership taken from
`composition.csv` rather than inferred from whichever block ended up hottest:

| | Ships | Reach 400 K | Lose a block |
| --- | ---: | ---: | ---: |
| Carrying any jump drive | 2,249 | **100.0%** | **100.0%** |
| Carrying none | 5,883 | 65.5% | 21.5% |

**The mechanism is the load state rather than the block.** `ShipLoad.State.Full` sets `Tools = 1`,
so every drive on the hull charges at its full draw for the whole 1,800 s clock. A real drive
charges and then stops. At idle the two groups are indistinguishable — 0.2% critical either way,
medians of 179 and 177 K — because a jump drive is classed as a tool and tools are off when parked.

G2 is not invalidated: it holds at **65.5% on ships with no drive at all**, well past its 20% gate.
But "44% of the corpus loses a block under load" is really "every jump-drive ship, plus 22% of
everyone else", and **any tuning read off the aggregate will be tuning for one block.** Whether
charging a drive *should* be a thermal event is a design question; that it should be an unsurvivable
one for every hull that mounts one is probably not.

### A global scale is not the lever

Cutting every waste fraction by half moves the median hull's peak from 1,382 K to 1,177 K on the
panel — a 15% change that leaves both populations exactly where they were. The measured per-type
dials say the same:

| Dial | Effect on `burn-forward` peak p50 across its whole sweep |
| --- | --- |
| `thruster-waste` 0.10 → 2.00 | 652 K → 1,448 K — **the strongest dial in the set** |
| `consumer-waste` 0.25 → 4.00 | 776 K → 1,736 K |
| `producer-waste` 0.25 → 4.00 | 1,177 K → 1,240 K |
| `reactor-waste` 0.50 → 8.00 | 1,205 K → 1,242 K — **very nearly inert** |
| `engine-conductivity` 1 → 8 | 1,205 K → 1,204 K — **inert** |

`reactor-waste` moving sixteen-fold for a 3% change in outcome is worth noting on its own: the
reactor retune argued above is not a population-level lever.

### What actually causes a hot spot is local watts per local square metre

The census measures arrangement, not just totals: burial depth in conduction hops out from the
nearest radiating node, the Clark-Evans nearest-neighbour index of the heat sources, watt-weighted
spread about their centroid, and the worst watts-per-exposed-area found in any 5×5×5 neighbourhood.
Correlated against the hot spot (peak minus mean) **with total waste heat partialled out** — which
has to be done, because every raw correlation here is mostly "this ship makes more heat":

| Term | Partial ρ vs hot spot | vs peak |
| --- | ---: | ---: |
| worst local W/m² | **+0.369** | **+0.552** |
| spread of sources about their centroid | −0.216 | −0.406 |
| conductance away from the hottest source | −0.137 | −0.116 |
| mean burial depth of the sources | +0.111 | +0.018 |
| **Clark-Evans clumping index** | **−0.033** | **+0.076** |

**The clumping index does not work, and that is worth recording.** It measures how evenly spaced the
sources are, and evenness is not the mechanism: two 5 MW generators ten metres apart score as
dispersed and still cook the bay they share. Local power against local radiating area is what a
block actually experiences, and it is an order of magnitude more predictive.

Banded, the local figure is a design rule rather than a correlation — share of the 8,132
`full-electrical` ships losing a block:

| Worst local W/m² | Ships | Lose a block | Median peak |
| --- | ---: | ---: | ---: |
| under 500 | 2,033 | 0.5% | 184 K |
| 500 – 2,000 | 1,202 | 6.9% | 545 K |
| 2,000 – 10,000 | 2,356 | 42.7% | 656 K |
| 10,000 – 50,000 | 1,447 | 93.2% | 1,287 K |
| 50,000 – 200,000 | 465 | 95.5% | 1,700 K |
| over 200,000 | 629 | 98.9% | 2,541 K |

**Spreading the generators helps, up to a point.** Within matched bands of total waste heat, the
most-spread third against the tightest third: at 100 kW–1 MW, 21.7% lose a block against 45.2%; at
1–10 MW, 61.6% against 91.5%. Past about 10 MW installed it stops mattering — 98% to 100% either way
— so arrangement buys a builder a great deal in the middle of the range and nothing at the top. The
raw banding of spread says the opposite, because spread correlates with hull size and so with total
heat; **it has to be read within a waste band or not at all.**

The failure is per-block, not per-hull: the hot spot is a median of **4 blocks on a median
1,110-block ship**, and of the 436 ships that go critical in under five seconds under full
electrical load, **82.3% do so on a `LargeHydrogenEngine`**.

### One number says whether a block can survive itself

`BlockHeatIndex` computes, from the definition alone and with no simulation, the heat a block makes
at full rating over the most it could possibly shed while staying at its own critical temperature.
The denominator is deliberately the best case that exists — every face radiating to deep space *and*
every face bolted to armour held at ambient, both at once.

* **index** — against radiation and conduction together. Above 1 the block is impossible: no hull,
  no arrangement and no cooling saves it, and only the definition can be changed.
* **self index** — against its own skin alone. Above 1 the block cannot cool itself and depends
  entirely on exporting into the hull, which is a much weaker position than it sounds.

**One of the game's 323 heat-making blocks is impossible, and 70 cannot cool themselves** (re-read 2026-08-26; it was four and 72 before `C21` and `C24`). `Thermodynamics.Sim -- triage` prints them ranked by severity against the share of a censused fleet's heat the type carries, which is the order a balance pass should work in — the one impossible block is `LargePrototechReactor` at an index of **6.33**, and almost nobody builds one, while a jump drive is survivable and is **65.5 %** of a loaded fleet's waste. Banded
against the corpus, the index predicts the outcome well enough to use as a gate:

| Worst index on the ship | Ships | Lose a block |
| --- | ---: | ---: |
| under 0.25 | 3,458 | 8.8% |
| 0.25 – 0.50 | 1,639 | 47.0% |
| 0.50 – 0.75 | 2,059 | 84.6% |
| 0.75 – 1.00 | 39 | 100.0% |
| over 1 | 799 | 82.6% |

**0.5 is the practical threshold and 1.0 the theoretical one.** `BlockHeatIndexTests` gates on the
set of blocks above 1, so a new block or a changed waste fraction that crosses the line fails the
suite.

**`LargePrototechReactor` is a definition accident and the worst block in the game.** The game gives
it the TypeId `HydrogenEngine`, so the derivation charges a 400 MW plant a combustion engine's 0.60
waste fraction: **240 MW of heat out of a 3×2×2 block, an index of 17.9 and a self index of 46.** At
the reactor family's own 0.01 it would make 4 MW and be unremarkable. This is the same shape as the
reactor-waste defect above — the derivation keys on the type, and the type is decided by the game's
wiring rather than by what the block is called. It needs a per-subtype override.

**The jump drive is not impossible, and that correction matters.** Its index is 0.75: bolted to
armour that stays at ambient it survives, because superconductors conduct extremely well. Its *self*
index is 7.4 — its own skin sheds an eighth of what it makes — so it lives or dies on the hull
taking the rest. **The hull never does.** Of the corpus ships carrying one, 909 have more than the
19,068 m² of 400 K armour the index says is needed to absorb its export, and **all 909 still lose a
block**, because total hull area is irrelevant when the heat cannot travel: block-to-block
conductance is about 112 W/K, so moving megawatts even one block needs thousands of kelvin. **Area
has to be *near* the source to count.**

### What refilling a coolant loop has to cost, written before it is measured

[backlog.md](backlog.md) `B43` is a decision with three routes and no evidence under it: a conveyor
port and ice, a coolant component eaten from the build list, or energy and time. `B44` already
settled what is true of all three — an empty loop stays a loop, the cost is **per pipe** so the
exchange rate does not depend on ring size, and fill is binary so a half-empty loop never presents
a small capacity to the integrator. What is missing is the number that says which currency can
carry it, and that number is **the rate the exploit runs at** (`E1`).

**The question.** Grinding a pipe out of a live ring takes that pipe's coolant parcel out of the
world, and rewelding it brings the block back at ambient. What is that worth per second of welding,
and can an energy price cancel it?

**Why the rate rather than the joules.** `HeatLaunderingTests` already measures the joules exactly —
one parcel per grind, neither more nor less. A quantity of heat is not an exploit; a quantity of
heat *per second* competes with a radiator, and that is the comparison the decision turns on.

**Three predictions, each with what would falsify it.**

| | prediction | falsified by |
| --- | --- | --- |
| the size | one grind of an eight-pipe large-grid ring at 100 K above ambient removes **over 1 MJ** | under 1 MJ |
| the rate | at one welder — the pipe's own `BuildTimeSeconds` of 8 s — that is **over 3 MW**, which is what the game's largest reactor makes at the fraction this mod ships | under 3 MW |
| the currency | the refill power needed to cancel it **exceeds the installed electrical power of the median corpus ship**, so route 3 cannot carry the cost on its own | a required power the median ship could supply |

**The decision rule, fixed now.**

* **If the rate is under what the mod's own cooling achieves**, the row is tidiness and the
  smallest route wins — route 3, energy and time, no component and nothing to haul.
* **If the rate is over it and the cancelling power is one a ship can spend**, route 3 still wins,
  because it is the only route that needs no new block surface, and the physics is already right:
  a pump's waste fraction is 1, so the energy spent refilling lands back in the ship as heat.
* **If the cancelling power is one no ship can spend**, energy cannot be the currency at the rate
  the exploit runs, and the cost has to be a material the player hauls — route 1, ice through an
  inventory the pump attaches in code, exactly as it already attaches its power sink.

**What this cannot settle.** Whether a conveyor port is the right shape for an
`UpgradeModuleDefinition`, and what a player thinks of hauling ice — both are game-side and neither
is a number. The rule above chooses a currency; it does not choose an interface.

### What it did: the exploit was priced out by a fix aimed at something else

**All three predictions fail, in the same direction and by two orders of magnitude.** Grinding one
pipe out of a warm eight-pipe ring removes **188,889 J**, and at the pipe's own 8-second build time
that is **23,611 W** — **0.79 %** of what the game's largest reactor makes at the fraction this mod
ships. On a ship already at 900 K it is **143,284 W**, 4.78 %.

> **Every figure in this section is at the flat 50 kg a pipe carried when it was measured, and
> `C43` multiplied that by 10.31.** At the density that ships a parcel is 1,947,916 J and the grind
> is **243,490 W**, which is 8.15 % of that reactor rather than 0.79 % — an exploit ten times the
> size of the one this section priced. **It is left as it was measured** (`E10`), because the
> conclusion it reached does not depend on the size: `B44`'s vent takes the whole ring rather than
> one parcel, and a ring that has to be refilled at the break-even excess is neutral in heat at any
> charge. The prediction table below is a record of what was registered against what was measured,
> and rewriting its numbers would make it a record of neither.

| | prediction | outcome |
| --- | --- | --- |
| the size | over 1 MJ per grind | **fails at 0.19 MJ** |
| the rate | over 3 MW at one welder | **fails at 0.024 MW** |
| the currency | the cancelling power exceeds the median ship's installed power | **fails**: 23,611 W against a median **14,750,000 W**, which is 0.16 % of it |

**The reason is `A12`, and it was a fix aimed at something else.** `B42` was written against a
**190 MJ** case where grinding a *pump* dissolved a whole loop and dumped its heat. A ring now
spills into its pipes and a pipe keeps the parcel it absorbed, so **a grind costs one parcel** —
one eighth of an eight-pipe ring, and one fourteenth of a fourteen-pipe one. The exploit the row
exists to price was priced out before the row was decided.

**And it cannot be scaled**, which is what makes this a bound rather than a reading. A grind costs
one parcel *whatever the ring's length*, so the rate is capped at one parcel per pipe-build-time per
grinder however much coolant a ship carries. Building a bigger loop buys the exploit nothing; it
only buys more heat that stays put.

**The decision: route 3, energy and time.** The registered rule's second branch fires — the rate is
far under what the mod's own cooling has to handle, and the power that cancels it is 23,611 W,
which the median ship supplies six hundred times over. So the cost is a number of joules and a
number of seconds, per pipe (`B44`), and no route needs a conveyor port, a component in a build
list, or ice to haul.

**What that leaves for the implementation.** The physics is already right and needs nothing new: a
pump's waste fraction is **1** — *a circulator does no work that leaves the system* — so the energy
a refill spends lands back in the ship as heat, and the exchange rate is self-limiting without a
single authored threshold. Venting is still the smaller change `B44` describes, zeroing
`HeldCoolantCapacity` for the pipes of a ring a grinder opened.

> **This closes `B43`'s currency and not `B42`.** `B42` asks that mass ejection cost something; what
> this says is that the *coolant* case is worth kilowatts rather than megawatts, so the cost can be
> small. It says nothing about jettisoning a hot block that is not a pipe, which is the general case
> and is still free.

### What actually runs away, and the one number that predicts it

**Runaway is the failure that matters**, and it turns out to be predictable from a definition alone.
Crossing the block index against the 2026-08-21 population, 7,994 ships, by the **worst self index**
any block on the ship carries — heat made over what that block's own skin can shed at its rating:

| worst self index on the ship | ships | peak over 1,500 K | never recovers |
| --- | ---: | ---: | ---: |
| under 1 — cools itself | 1,338 | **0.4 %** | 1.3 % |
| 1 – 3 | 1,627 | **1.4 %** | 0.5 % |
| 3 – 10 | 4,230 | **19.0 %** | 3.9 % |
| over 10 | 799 | **64.2 %** | 6.4 % |

**A ship runs away if and only if it carries a block that cannot cool itself by a factor of about
three.** Below that the rate is one per cent and indistinguishable from nothing; above ten it is two
ships in three. The index is computed from the definition with no simulation at all, so **this is a
runaway predictor that costs nothing to evaluate** and can be read before a ship is ever built.

**And it names the blocks.** Of everything with a self index over 3 that real fleets actually carry:

| block | self | settles bare | its rating | share of fleet heat | ships |
| --- | ---: | ---: | ---: | ---: | ---: |
| `SmallPrototechJumpDrive` | 23.6 | 1,892 K | 862 K | 65.5 % | 2,254 |
| `LargeJumpDrive` | 9.9 | 1,211 K | 689 K | 65.5 % | 2,254 |
| `LargePrototechReactor` | 46.0 | 3,033 K | 1,166 K | 5.8 % | 1,361 |
| `LargeHydrogenEngine` | 10.7 | 1,541 K | 856 K | 5.8 % | 1,361 |

**So the runaway problem is the jump drive and the hydrogen engine, and almost nothing else.** Both
are on thousands of published ships, both settle hundreds of kelvin past their own rating with every
face on open space, and between them they are three quarters of a loaded fleet's waste heat. This
page already said the drive *lives or dies on the hull taking the rest, and the hull never does* —
what is new is that the same sentence, applied across the population, accounts for essentially every
runaway in it.

> **The clock is the wrong dial for this.** `HeatTimeScale` sets how fast a block reaches where it
> is going and changes nothing about where that is: a self index above one is a steady state above
> critical, and slowing the approach buys time without changing the destination. The clock is `G8`'s
> and `G11`'s dial. **Runaway is a definition problem** — watts made, area, emissivity, and the
> temperature the block is rated to — and it is fixed per block or not at all.

*Population: the 8,132-ship survey of 2026-08-21, which predates `C24` and `A13`; the paired read on
the hulls the new survey has reached says the model got cooler, so these rates are an upper bound
rather than a current reading. The **structure** — self index predicting runaway — is a property of
the model rather than of the run.*

### The runaway and the retrofit are the same block, and the lever follows from `P7`

Two lines arrived at the jump drive from opposite ends and neither knew about the other.

**From the cooling side**, above: a retrofit is *either unnecessary or impossible, with almost
nothing in between* — 57.7 % of ships need one radiator or fewer and 19.2 % need more than sixteen —
and the factor of 17.6 between p50 and p75 is the drive. **From the runaway side** (`C34`): a ship
runs away if and only if it carries a block that cannot cool itself threefold, and the drive is that
block on 2,254 ships.

**Measured on real hulls, cooling does not currently answer either.** `retrofit` over 16 corpus
ships under full electrical load in shadow:

| fit | ships | no room | p10 | p50 | p90 |
| --- | ---: | ---: | ---: | ---: | ---: |
| bolted | 13 | 0 | −1.32 % | **−0.32 %** | 1.25 % |
| plumbed | 4 | **9** | 0.03 % | 0.15 % | 4.32 % |

A bolted fit takes **nothing** off the median hull and makes some *worse*; a plumbed one **cannot be
installed at all on nine of thirteen**. So `G3` does not merely lack a measurement — the measurement
it lacks looks like a failure. *(16 ships, a stride sample, one run — a direction rather than a
population figure.)*

**Which means the runaway is not a bug to nerf away.** Heat mattering is the point, and a vanilla
ship that has never heard of this mod overheating under full load is the mod working. What is broken
is that **the player has no answer** — and the answer is blocked by the same block.

**The lever follows from `P7`, and it rules out three of the four.**

* **Exposed surface ×3.3 — out.** The radiator, whose entire job is surface, uses **1.25**. A jump
  drive more finned than the radiator is not a claim this mod can make.
* **Emissivity ×3.3 — out, and for the same reason.** `emissivity` and `exposed-surface` are
  measured on this page as *the same dial*, and the radiator's own emissivity is **0.35**. A drive at
  0.66 is twice as radiative as the block built to radiate.
* **Waste ÷3.3 — out on `P7`.** The drive's 0.2 is *derived from the game's own* `PowerEfficiency`
  of 0.8. Overriding it says the game is wrong about its own block, which is the one thing this
  repository does not get to say.
* **Critical temperature ×1.35 — the honest one.** 689 K to **928 K**. It is the mildest move of the
  four by a wide margin because the law is quartic, and — the reason it wins — **the game has no
  block temperatures at all**. A rating is the mod's own invention end to end, so tuning it
  contradicts nothing the game states. It is the only one of the four levers that is not an argument
  with Space Engineers.

**What would falsify it.** If the drive at 928 K does not move the population's runaway rate, the
self index is a correlate rather than a cause. And if it moves the runaway rate but leaves the
retrofit distribution as bimodal as it is now, then the drive was never what stood between a player
and a working radiator, and the cooling blocks themselves are the row.

### What the mod's blocks cost to build

[backlog.md](backlog.md) `B33`: eighteen definitions in `Cubes.xml` carry components, build times
and PCU, and none of the three had been derived or defended. **There is no comparator to derive them
from** — the game ships no block whose job is to move heat, so pricing a radiator against the block
it competes with is not available, and choosing one anyway would be choosing it to reach a number
(`M10`).

**What is available is the population.** The game prices 1,434 of its own blocks with all four of
mass, volume, PCU and build time declared, and how it prices matter and volume is a distribution a
new block can be placed inside. The three ratios are deliberately shape-free — they say nothing
about what a block does and everything about how much of a ship it is:

| | p01 | p50 | p99 |
| --- | ---: | ---: | ---: |
| kilograms a cubic metre | 2.07 | 46.21 | 2,112 |
| seconds of welding a kilogram | 0.003 | 0.042 | 0.316 |
| PCU a block | 1 | **1** | 190 |

**All eighteen sit inside the game's own range on all three**, between the 6th and 91st percentile on
mass, the 19th and 89th on welding, and at the 53rd or 95th on PCU — the two pumps at 100 PCU and
everything else at 1, which is the game's own median. So the answer to *what is this lever meant to
cost* is: **what the game charges for a block of that size and mass**, which is `game mod first`
applied to the economy rather than to the physics. The mod does not invent one.
`BuildCostTests` holds it.

**One ratio was checked and is now reported instead, and the change is here rather than quiet**
(`E11`). PCU *per cubic metre* was the third check and it flagged exactly one block, the large
radiator, at 1 PCU over 156 m³. Two things say the ratio is wrong rather than the price: PCU is a
per-entity budget, spent by the block and not by the space it occupies, and the game's own median
block is 1 PCU; and the vanilla blocks at the bottom of that ratio are vivariums, platforms and
support beams — 1 PCU over three to four hundred cubic metres each — which is the company a large
hollow panel belongs in. Dividing by volume prices the radiator's mechanism as though it were a cost.

**And the component list is not in conflict with itself.** `B33`'s sharpest point is that a recipe is
already a thermal dial: mass is the sum of the components and capacity is mass times specific heat.
Measured on the biggest reactor the game ships under eight of the mod's large radiators, in shadow,
with only the radiators' mass multiplied:

| radiator mass | source settles | far radiator settles | and reaches it in |
| --- | ---: | ---: | ---: |
| ×1 — 4,800 kg | 864.32 K | 219.08 K | 24 s |
| ×2 — 9,600 kg | 864.32 K | 219.08 K | 50 s |
| ×4 — 19,200 kg | 864.32 K | 219.07 K | 112 s |

**A recipe moves when a block gets there, not where it ends up**, and that is arithmetic rather than
a result: radiation is `εσA(T⁴ − T⁴)` and conduction is `kA/d × ΔT`, and mass is in neither. Every
figure this page prices a block on is a steady-state figure, so a recipe can be set for cost reasons
without moving any of them.

**The nineteenth block is the one nothing checks.** The decorative `Extinguisher` wall block lives in
[Data/Extinguisher/Extinguisher.sbc](../Thermodynamics/Content/Data/Extinguisher/Extinguisher.sbc) rather than in
`Cubes.xml`, carries ten steel plates, and declares **neither `PCU` nor `BuildTimeSeconds`** — so two
of the three costs are the engine's defaults rather than anything authored. It is not simulated and
not priced here; naming it is the whole of what this measurement can say about it.

### What a hand tool would have to be worth

The same index answers a question that is not about balance at all:
[document-of-intent.md](document-of-intent.md#acting-on-heat-by-hand--damage-mitigation-and-priced) had
no position on whether a player should be able to act on heat with their hands, and it could not
take one without a number. `HandCoolingLab` supplies a physical-capacity bottle comparison.
**Corrected 2026-09-27:** this section previously claimed the clock could not move the comparison.
Only the steady cooling/waste power ratio has that property. `ReturnJoules` restores physical
capacity by multiplying the index capacity by its clock, but `WindowSeconds` retains the index's
game-time damage window. Consequently the finite-window power estimate depends on that clock.
A fixed energy applied directly to the solver uses capacity `mass × specific heat / HeatTimeScale`;
it causes a temperature change larger by `HeatTimeScale` than the physical bottle calculation.
For an analytic 100 kg block with 450 J/(kg K) capacity, 4,500 J changes temperature by 0.1 K at
clock 1, 9 K at 90 and 22.5 K at 225. `HandCoolingTests` checks this distinction and the window
term against independently calculated examples. These figures do not select a shipped ammunition
energy, discharge duration or transfer efficiency.

The reference tool is a five-kilogram CO2 extinguisher: **3.3 MJ** and **165 kW** while it
discharges, assuming every gram of it lands on the block, which is not how an extinguisher works.
Against the 72 vanilla block types that reach their own critical temperature under their own waste:

| | physical-capacity bottles to return the block to ambient | physical return energy / game-time window, in bottle power units |
| --- | ---: | ---: |
| easiest block in the game | 1.6 | 2.8 |
| median | **26** | **20** |
| ninetieth percentile | 461 | 77 |

The window is the median **32 s** between the crossing and the loss, from
[How long a block has after it crosses](#how-long-a-block-has-after-it-crosses). Twenty bottles at
once is 3.3 MW under that mixed-basis estimate; it is not a clock-independent requirement for
the unimplemented tool. The historic table is retained with its basis rather than repurposed as
a prediction of direct solver-energy injection.

**And 71 of the 72 have no surplus at all in the best case.** With every face radiating to deep
space and every face bolted to armour held at ambient, all but the prototech reactor shed everything
they make at their own critical temperature. They cook because the hull cannot lose their heat for
them, which is why a tool aimed at the block is at the wrong place rather than merely too small —
and which is [Cooling is designed
in](document-of-intent.md#cooling-is-designed-in--and-a-vanilla-ship-still-has-to-survive) as
arithmetic over the vanilla definitions rather than as a claim.

### Cooling has to remove watts, not add area

The model behaves as clean radiative equilibrium, so **T scales as the fourth root of area**:

* `exposed-surface` ×2 moves the panel's `burn-forward` peak 1,205 K → 1,019 K, a 15.4% drop against
  an ideal radiative prediction of 2^−0.25 = 15.9%.
* `emissivity` ×2 gives 1,020 K — the same curve. The two dials are the same dial.

A fourth-root law cannot produce a moderate retrofit: the first radiator does almost nothing and the
hundredth does less. **A cooling block that matters has to remove watts linearly** — a loop that
carries heat somewhere, or a pump with a rating.

Priced against the population, one `Gauge_LG_Radiator` (1×5×2 cells, 265.6 m² effective, emissivity
0.35) sheds:

| Panel temperature | 400 K | 500 K | 600 K | 700 K | 800 K |
| --- | ---: | ---: | ---: | ---: | ---: |
| Watts shed | 96 kW | 291 kW | 644 kW | 1.23 MW | 2.12 MW |

and the population needs, at a 600 K panel:

| Ship | Full-load waste | Radiators to break even |
| --- | ---: | ---: |
| p50 | 337 kW | **0.5** |
| p75 | 5.94 MW | 9.2 |
| p90 | 20.5 MW | 31.8 |
| p99 | 125 MW | 194.6 |

57.7% of ships need one or fewer; 19.2% need more than sixteen. **The retrofit is either unnecessary
or impossible, with almost nothing in between**, and the factor of 17.6 between p50 and p75 is the
jump drive again. Fixing the jump drive is what creates the middle of that distribution — nothing
else in the census moves it.

### No block lands in the 2–5 minute window, and none can be made to at `HeatTimeScale` 225

> **This section and the three below it are the argument that ended in `C24`**, and every
> *(shipped)* in them means the pair that shipped while they were written: `ConductionScale` 2.4
> with the clock at 225. What ships now is 9.6 with the clock at 90, which is where the argument
> arrives — see [The route is chosen](#the-route-is-chosen-and-it-is-the-one-the-cost-column-argued-against).

Of the 72 block types that generate more heat than their own skin can shed, time from 293 K to their
own critical temperature, alone:

| Band | Blocks | Share |
| --- | ---: | ---: |
| under 10 s | 15 | 20.8% |
| 10–60 s | 54 | 75.0% |
| 60–120 s | 3 | 4.2% |
| **120–300 s (the target)** | **0** | **0%** |
| over 300 s | 0 | 0% |

Median 16.9 s, worst 0.5 s, best 87.1 s.

**Waste fractions cannot fix this.** Solving for the waste heat that would produce a 180 s crossing,
per block, the required self index is **1.000 to three decimals for 90% of them** — the band between
"never overheats" and "overheats in under a minute" is about a tenth of a per cent wide. That is a
direct consequence of T⁴: radiated power rises so steeply that a block is either comfortably under
its limit or running away.

The dial that does reach the window is the one that sets capacity, and the scaling is exactly
linear:

| `HeatTimeScale` | corpus `full-electrical` s-to-critical p50 | vacuum substeps p50 |
| ---: | ---: | ---: |
| 225 (shipped then; 90 now) | 8.9 | 2.41 |
| 112 | ~17.8 | 1.21 |
| 56 | ~35.8 | 0.60 |
| 25 | ~80 | 0.27 |
| **~11** | **~180** | **~0.12** |

**But one clock governs two time constants about a hundredfold apart.** A hot block's rise is set by
its own small mass; a hull's response to sun or air is set by its whole mass. At 225 the block
crosses in 17 s while the hull takes 1,560 s to settle at idle. Dividing the clock by 20 puts the
block in the window and pushes the hull to eight hours.

Closing that gap needs conduction, which couples the block to the hull's mass:

| `conductivity` | `burn-forward` s-to-critical p50 | peak p50 | substeps p50 |
| ---: | ---: | ---: | ---: |
| 0.25 | 4.2 | 1,296 K | 0.77 |
| 1.00 (shipped then; ×4 now) | 4.8 | 1,205 K | 2.75 |
| 2.00 | 9.2 | 1,107 K | 5.23 |
| 4.00 | 13.5 | 1,004 K | 9.93 |

Substep demand is conductance over capacity, so it is proportional to `conductivity ×
HeatTimeScale`. **Raising conduction and lowering the clock together moves the crossing into the
window at constant or lower cost:** conductivity ×4 with `HeatTimeScale` ~15 projects to a ~200 s
crossing at ~0.7 substeps in vacuum, against 4.8 s at 2.75 today.

**That projection was an extrapolation across an interaction the sweep never measured**, it has now
been measured, and **it was wrong by a factor of five**: conductivity ×4 with `HeatTimeScale` 15
gives a 991 s crossing, not ~200 s. What was wrong was not the composition rule — the two dials do
multiply, to within 1 % — but the two curves fed into it, which came from different scenarios and
from a statistic taken over the ships that crossed rather than over the ships that were loaded. The
grid that replaces it is below.

### Two dials at once: the window is reachable at conductivity ×4 with the clock near 100

Twenty-five cells of conduction against the clock, on the forty-hull retest set, each scenario's run
length stretched by the clock ratio so every cell is stopped the same way (`M1`). `PairSweep` runs
it and `tools/corpus/pairs.py` scores it.

**`G8` is satisfied, and by four cells.** Under sustained full electrical load the median hull
crosses critical inside the 120–300 s window while a hull driven to full burn and throttled back
recovers inside the hour:

| `conductivity` | `HeatTimeScale` | crossing p50 | recovery p50 | substep demand | G1 | G2 | G5 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 4 | 120 | 124.0 s | 2,220 s | 2.04× shipped | 0 % | 100 % | 100 % |
| 4 | 100 | 148.9 s | 2,640 s | 1.70× | 0 % | 100 % | 100 % |
| 4 | 90 | 165.4 s | 2,940 s | 1.53× | 0 % | 100 % | 100 % |
| 4 | 80 | 186.0 s | 3,270 s | 1.36× | 0 % | 100 % | 100 % |

None of the four breaks a criterion it has to keep: no hull goes critical at idle, every hull gets
warm under load, and every hull comes back. **The price is 1.36–2.04× the shipped substep demand**,
which is what `G6` is about and is a price rather than a bound — whether it is affordable is a
question about the shipped caps and the tail, not about the median in that column.

**Both dials behave exactly, which is why four cells were enough to find.** Two curves came out of
the twenty-one cells that ran first, and each holds across every cell that has a median:

* **The crossing goes as one over the clock.** `crossing × clock` is a constant per conductivity —
  2,302 at ×1 over six cells, 5,418 at ×2 over three, 14,880 at ×4 over nine — held to within
  1.5 %, 0.4 % and 0.3 %. So a conductivity *is* that constant, and the clocks that put it in the
  window are the constant divided by 300 and by 120.
* **Recovery is set by the clock and barely moves with conduction.** At the shipped clock it is
  1,320 s at ×1 and 1,230 s at ×8; the bound of an hour falls between clock 70 (3,660 s) and clock
  80 (3,210 s) whatever the conduction.

The two bands overlap at exactly one conductivity. ×1 wants a clock of 8–19 and ×2 wants 18–45,
both far under the clock recovery needs; ×4 wants 50–124, which reaches it. The four cells above are
that overlap, and their crossings were predicted from the constant to within 0.2 % before they were
run.

**Conductivity ×8 is out, and not because it is slow.** At ×8 only 13 of 40 hulls ever reach
critical, so there is no median crossing time at any clock — the 50th percentile of the population
sits in the censored tail. Conduction spreads a block's heat into the hull it is bolted to, and past
some point the hull absorbs the whole event. That is a defensible mod and it is not the one `G8`
describes, so the rung is excluded by the criterion rather than by cost. The share is a property of
conduction alone: 72 % at ×1, 68 % at ×2, 62 % at ×4, 32 % at ×8, flat across every clock.

**This corrected a scoring defect before it corrected the answer.** The first reading of the grid
took the crossing median over the hulls that crossed, which reported two ×8 cells as satisfying
`G8` — a cell where two thirds of the population never overheats read as the grid's best. Read as
`E9` requires, with a hull that never crossed censored above rather than dropped, none of them does.
The rule is pinned by `tools/corpus/test_scoring.py`.

**What this is not.** It is a measured configuration, not a shipped one. It is taken on the forty
hulls of the retest set rather than on the standing panel, so it is not the published single-dial
rows re-derived (`M1`); what is compared is the interior of this grid against its own edges.
Retuning to it would move every temperature figure in this repository, and the cost column has been
read at the median only. See [backlog.md](backlog.md) `C12`.

### The load reaches the window too, and costs the bite instead of the levers

Conduction and the clock are both *transport*. The load is not: it changes how much heat a ship
makes without changing how the heat moves. Eighteen cells of a waste-heat multiplier against the
clock, same forty hulls, same three scenarios, same stop rule — `PairSweep` runs it and
`tools/corpus/load.py` scores it.

**`G8` is satisfied by four cells, at conductivity ×1.** Waste ×0.5 with `HeatTimeScale` 110, 100,
90 or 80:

| `waste` | `HeatTimeScale` | crossing p50 | crossed | recovery p50 | ratio | G1 | G2 | G5 |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0.5 | 110 | 122.4 s | 22/40 | 2,670 s | 21.8 | 0 % | 100 % | 97.5 % |
| **0.5** | **100** | **134.5 s** | **22/40** | **2,880 s** | **21.4** | **0 %** | **100 %** | **97.5 %** |
| 0.5 | 90 | 149.5 s | 22/40 | 3,180 s | 21.3 | 0 % | 100 % | 97.5 % |
| 0.5 | 80 | 168.1 s | 22/40 | 3,570 s | 21.2 | 0 % | 100 % | 97.5 % |
| *(then shipped)* | *225* | *10.4 s* | *29/40* | *1,320 s* | *127.2* | *0 %* | *100 %* | *97.5 %* |

**The ratio column is the finding.** `G8` wants a crossing over 120 s and a recovery under 3,600 s,
and both go as one over the clock — so the clock slides the pair along and cannot change
`recovery / crossing`. At the shipped load that ratio is 127, and the window needs about 30 or less.
**The load is the dial that moves it**: 315 at waste ×2, 127 at ×1, 21 at ×0.5, and within each load
it barely moves across the clock at all. Conduction moves it the same way — that is why ×4 worked —
and the load moves it without touching a transport term.

**The mechanism is a change of population, not of rate.** Cutting the load makes every hull heat
more slowly *and* pushes the marginal hulls below their own critical temperature entirely, so the
set that crosses shrinks from 29 of 40 to 22 and the survivors are the slow ones near their
equilibrium. The median moves from 10.4 s to 59.9 s at the same clock because it is a median of a
different set. That is also why the deeper cuts fail: at ×0.25 only 8 of 40 hulls cross and at
×0.125 only 1, so the 50th percentile sits in the censored tail and there is no median at any clock
— the same exclusion conductivity ×8 met (`E9`).

**Both composition rules hold exactly**, which is what makes the band a band rather than four
lucky cells: `crossing × clock` is 13,455 across clocks 225, 110, 100, 90, 80 and 45 to **0.2 %**,
and `recovery × clock` is about 286,000 to 3 %. Those put the window at clock 45–112 and the
recovery bound at clock 80 or above; the four cells above are the overlap, and the interior was
measured rather than read off the fit.

**The cell would be ×0.5 / 100**, by the widest margin on its worst criterion — the same rule the
conduction grid was read with. Clock 110 sits 3 % from the window edge and clock 80 sits 1 % from
the recovery bound; 100 is 16 % clear of both. **Would be, because every row of this table is
conditional on the load case it was taken in**, and measured against the other one it does not hold
— see [neither route survives the other bound](#the-event-has-a-length-and-measured-across-it-g8-selects-the-same-cells).

#### What it costs, and it is not free either

| | Shipped | ×0.5 / 100 |
| --- | ---: | ---: |
| median peak under full load | 999.0 K | **734.5 K** |
| median blocks over critical under full load | 4 | **1** |
| hulls that ever cross critical | 29/40 | **22/40** |
| median substep demand | 5.99 | **2.53**, 0.43× |

**It costs the bite.** A median hull under sustained full load peaks 264 K cooler and loses one
block instead of four, and a quarter of the hulls that used to overheat no longer do. `G2` still
passes at 100 % — every hull still reaches 400 K — so the mod still bites; it bites less hard.

**What it does not cost is a lever.** Conductivity does not move, so a coolant sink still
out-performs every surface dial, bolting still loses, a buried reactor still cooks, and the
atmosphere still sets the stiffest block's pace. And it is **cheaper**: substep demand goes as
`conductivity × clock`, so 0.43× — which also takes the atmospheric p99 of `C19` from 115 % of the
substep cap to about 50 %.

**So the window costs something whichever dial reaches it.** Conduction buys the timing with three
of the mod's levers; the load buys it with a quarter of the damage. That is the trade, and it is now
measured on both sides rather than argued.

#### And the load route halves a number that is sourced

A waste multiplier looked like the cheaper dial partly because the fractions it scales read as
guesses. Weighted by the heat they actually carry, they are not. Every fraction in `Cubes.xml` now
states its provenance ([definitions.md](definitions.md#every-waste-fraction-says-where-it-came-from-and-most-of-them-say-invented)),
and over the 8,142-ship census at full electrical load:

| Where a loaded fleet's waste heat comes from | Share |
| --- | ---: |
| a fraction derived from the efficiency the game itself states | **76.3 %** |
| a fraction sourced to a real conversion | 8.5 % |
| a fraction somebody invented | 15.2 % |

*Population: the census of 2026-08-21, 109,312 block rows, drives restated at their derived
fractions. Basis: full electrical load with every drive charging, no thrust (`E3`).
`tools/corpus/provenance.py`.*

**So ×0.5 is an admitted balance knob and not a correction.** Three quarters of what it would halve
is `1 − PowerEfficiency` on the jump drive, which the game publishes; the mod would be declaring that
a charging drive is twice as efficient as its own definition says. That is a legitimate thing for a
`ConductionScale`-shaped dial to do, and it has to be argued as one rather than as fixing a guess.
The 15.2 % that *is* invented sits on four block types — artificial mass, the reactor, the refinery
and the assembler — and retuning those four reaches a sixth of the load, not the window. (8.4 / 15.3
until 2026-08-24, when 27 fractions the first law fixes at 1.0 moved from invented to sourced; the
four types above are untouched by that and are still where the invented heat is.)

#### The event has a length, and measured across it `G8` selects the same cells

A jump drive holds `PowerNeededForJump` 3 MWh, draws `RequiredPowerInput` 32 MW while filling and
keeps `PowerEfficiency` 0.8 of it, so it fills in **421.9 s**. Every one of those figures is read off
the definition the game ships; none is chosen here. That is the length of the largest thermal event
most ships have, and `jump-charge` is the scenario that runs it: the drives charge for 421.9 s,
finish, and the ship holds.

| | crossing p50 across the charge | crossed | recovery p50 | `G8` |
| --- | ---: | ---: | ---: | :---: |
| *(then shipped)* ×1 / 225 | **10.4 s** | 29/40 | 1,320 s | — |
| the load route, ×0.5 / 110 | 122.4 s | 22/40 | 2,670 s | **pass** |
| the load route, ×0.5 / 100 | **134.5 s** | 22/40 | 2,880 s | **pass** |
| the load route, ×0.5 / 90 | 149.5 s | 22/40 | 3,180 s | **pass** |
| the load route, ×0.5 / 80 | 168.1 s | 22/40 | 3,570 s | **pass** |
| the conduction route, ×4 / 120 | 124.0 s | 25/40 | 2,220 s | **pass** |
| the conduction route, ×4 / 100 | 148.9 s | 25/40 | 2,640 s | **pass** |
| the conduction route, ×4 / 90 | 165.4 s | 25/40 | 2,940 s | **pass** |

**So `G8` can be scored, and it selects the cells the two grids already found.** The transient
reproduces the charging bound to the last figure, because every hull that crosses does so in the
first minute of a seven-minute charge — which is itself the clearest statement of the defect:
**a player who starts charging a jump drive has a block past its rating about ten seconds in, and
the charge runs for seven minutes.**

**This corrects the section below.** `full-electrical-charged` is not a bound on the event; it is a
ship not having one, and reading *fewer than half the hulls cross* there as evidence against the two
routes was reading the absence of an event as a short one. The two real bounds on the event are its
length and its absence, and the length is what `G8` is about.

**Two things this does not settle.** The ×0.5 cells cross on 22 of 40 hulls, which is barely over
the half the median needs, so the pass is not robust to a different population. And ×4 / 80 lost
seven hulls to run failures in this dataset and is reported on 33; its `full-electrical` figure of
186.0 s over 40 stands and its `jump-charge` figure does not.

#### Measured: with the drives full there is no event to time

The confound below was measured rather than left as a caveat. `full-electrical-charged` is the same
load with the jump drives full instead of charging, and every cell of both routes was run through
both cases on the same forty hulls:

| | crossing p50, drives **charging** | crossed | crossing p50, drives **full** | crossed |
| --- | ---: | ---: | ---: | ---: |
| *(then shipped)* ×1 / 225 | 10.4 s | 29/40 | **censored** | 12/40 |
| the load route, ×0.5 / 100 | **134.5 s** | 22/40 | **censored** | 3/40 |
| the conduction route, ×4 / 100 | **148.9 s** | 25/40 | **censored** | 6/40 |

**With the drives full, fewer than half the hulls cross critical at all in any of the three**, so
the 50th percentile sits in the censored tail and there is no median. The shipped configuration is
in the same position: 12 of 40. **Read correctly that is not a failure of the routes** — it is a
ship that is not doing the thing, and a criterion about an event cannot be scored where there is no
event. It is kept because it is the other half of the bracket and because it says how much of a
loaded ship's heat one block type is.

**So the mod is both too fast and too slow, and which one depends on one block.** With drives
charging the median hull crosses in 10 s, far under the window; with them full it never crosses.
Drives are 71.3 % of population waste, and a ship carries either state at different minutes of the
same session.

**`G8` asks for *the most significant thermal event* to land in 2–5 minutes, and on a real ship that
event is the drive charging** — a transient with a beginning and an end, which neither of these two
cases is. That is what `jump-charge` above measures, and it is where the criterion is scored.

**This dataset reproduced both grids** : the conduction route's four cells came out at 124.0, 148.9, 165.4 and
186.0 s in this dataset against 124.0, 148.9, 165.4 and 186.0 s in the pair grid, which is two
independent runs agreeing to the last figure.

**One defect found underneath it.** The jump drive was filed as a *tool* in `ShipLoad`, so
`State.Tools` governed it and `State.Consumers` — the dial that reads as the ship's electrical load
— never reached the block carrying 71.3 % of the load's heat. No published figure moves: the two
shares agree in all four states that existed, checked against 1,794 rows of the previous dataset,
of which none moved. What it cost was a dial nobody could use, and it is why the first run of this
comparison produced two identical tables.

#### The confound this rests on, and it is a known gap

`full-electrical` charges every jump drive continuously, and drives are 71.3 % of population waste
([backlog.md](backlog.md) `F13`). So the load scenario is a **bound rather than a steady state**, and
a waste multiplier of 0.5 is within the distance between that bound and a realistic load. There are
therefore two readings of the table above, and they are not distinguishable from this dataset:

* the shipped mod makes twice as much heat as it should, and ×0.5 is a retune; or
* the shipped mod is right and the *scenario* over-states the load, in which case `G8` may already
  be satisfied against a realistic one and nothing needs retuning at all.

The second would be much the better outcome — no balance change, a corrected measurement — and it
is one run away: the same grid at waste ×1 under a load case that does not charge every drive. That
makes `F13` the next thing to measure rather than a footnote, and it is why this page recommends a
cell without recommending that it ship.

#### What the retune was measured to cost, and why it is not shipped

The pair was built and the suite was run against it: `ConductionScale` 2.4 → 9.6 and
`HeatTimeScale` 225 → 100, which is exactly the ×4 / 100 cell. **Forty tests failed, and they are
not stale figures — most of them are the mod's own levers going away.**

| What was measured | Shipped | At ×4 / 100 |
| --- | ---: | ---: |
| a coolant sink, against the best surface dial | **195.3 K vs 41.5 K** | **73.3 K vs 135.3 K** |
| …with the loop's own pace moved ×4 as well | — | **108.2 K vs 135.3 K** |
| thirty-two radiators *bolted* to a reactor | under 1 % of it | **25.6 K off 890 K** |
| plain armour bolted to a reactor | loses — it buries a radiating face | **saves 2.42 K** |
| a 300 MW reactor under one cell of armour | overheats, asks to be cooled | **settles at 940.6 K, inside its 1,090.1 K** |
| the census hull's stiffest block, in air against vacuum | 1.04× or more, as real ships are | **1.00× — it stops responding to air** |

**The first row is the finding.** *The radiator is a block you plumb* is this page's own headline,
and it inverts: at four times the solid conduction pace a bolt joint is no longer the bottleneck, so
shortening a panel buys more than plumbing it does. Moving the loop's fluid coupling with the solid
pace recovers a third of that and does not restore the ordering, because the surface dial gained too.

**The last row is the one that decides it.** Conduction at ×4 dominates every other transport the
model has. A hull shares heat well enough that burying a reactor solves it, bolting works, and the
atmosphere stops mattering to the stiffest block on the ship — which is the conductivity ×8 failure
mode, *the hull absorbs the whole event*, arriving three rungs early. The mod's purpose is that heat
is a resource with a lever; ×4 removes three of the levers to buy the timing of one.

**So `G8` and `G3` cannot both be satisfied by moving conduction**, and the sweep that found ×4
never scored `G3` — it scored `G1`, `G2`, `G5`, `G8` and cost. That is the gap, not the cell.

**What the search has not tried.** Both dials moved so far act on *transport*. The crossing is when
a block passes critical, and it depends on what the block is *made to absorb* as much as on how fast
the heat leaves: `crossing × clock` is a constant per conductivity, and waste heat and the critical
temperatures move that constant without touching the conduction pace at all — so without touching
plumbing, radiation geometry or air. Recovery is set by the clock alone, so that half is unaffected.
That is where `C12` goes next, and it is a sweep the same machinery runs.

**One trap this pass found on the way.** The game has two conduction paces —
`ThermalConstants.ConductionScale` for solids and `ThermalConstants.ReferenceConductivity` for the
loop's fluid coupling — and nothing made them agree. Raising the first alone makes a coolant loop
four times weaker *relative to the structure it competes with*, silently; it is the whole difference
between the first and second rows above. `ConductionPaceTests` now fails if one moves without the
other.

### Air is where the substep budget goes, and the shipped pair does not fit it

**Every atmospheric figure this section carried was exactly half what the shipped configuration
demands.** The table below was taken when `Frequency` was 8; the shipped step became a quarter of a
second when it went to 4, which doubles the demand — substep demand is a conductance times the step
over a capacity — and the table was never re-derived. Re-measured on the same 49-ship panel, all
eight of its figures come out at **2.000×** what it published, in four environments at once, and
`AirCostTests` pins the proportionality the correction rests on.

| Environment | substeps p50 | p95 | as published | |
| --- | ---: | ---: | ---: | --- |
| `vacuum-shadow` | 4.83 | 7.12 | 2.41 / 3.56 | ×2.00 |
| `surface-hot-noon` (still air) | 28.28 | 33.31 | 14.15 / 16.66 | ×2.00 |
| `storm-parked` (100 m/s) | 51.86 | 61.61 | 25.94 / 30.81 | ×2.00 |
| `reentry` (200 m/s) | 61.67 | 73.37 | 30.87 / 36.71 | ×2.00 |

**Read against the cap, the shipped configuration fails `G6` in air.** `MaxSubsteps` grants 64. At
200 m/s in thick air the panel's p99 demand is **73.4**, and **14 of 49 hulls are over the cap**;
the median hull is at 61.7, which is 96 % of it. The same run on the forty-hull retest set gives
73.6 and the same verdict, so this is a property of the configuration rather than of a population.
Exceeding the cap is not a slow step: the solver integrates the step at the ceiling instead of
dividing it as finely as the estimate asked. **What that costs is now measured and it is 0.028 K**
— on the hottest block of a driven census hull over 600 simulated seconds, in the same thick air at
200 m/s, against a run granted everything it asked for. Every hull over the cap in either
population is at 1.14–1.15× over-subscribed, which is the first rung of a ladder that stays free to
about 2× and breaks between 2× and 3×; the worst block on that rung is 0.041 K out. See
[stiffness.md](stiffness.md#what-refusing-the-demand-costs).

**Two claims this section carried are corrected.** The mechanism named — the solver flooring a
block's heat capacity — is `MaxSubstepsPerBlock`, which ships at zero and does not run; what bounds
the refused step is the two overshoot clamps. And *simulated wrong rather than slowly* is not what
the measurement says: refusing 1.15× the demand buys 1.15× of the step for three hundredths
of a kelvin. **`G6` still fails as written** — the criterion's marker is demand exceeding what the
caps grant, and it does — but the failure now has a price on it, and it is smaller than the
instrument that reads it.

The criterion had been scored in vacuum, where the same panel demands 7.1 at p95 and nothing is
close to anything. **`G6` was read where the money is not being spent.**

#### The retune `C12` is about costs less here, not more

`G8`'s four candidate cells were priced at 1.36–2.04× the shipped demand, and that column is a
vacuum column. In air the same cells are **cheaper**, and the mechanism is exact:

> Substep demand is a conductance over a heat capacity. `HeatTimeScale` divides every capacity and
> touches nothing else, so lowering it divides *every* stiffness term at once; multiplying
> conductivity restores the conduction term and only that one.

A hull in vacuum is conduction-limited, so it gets dearer by `conductivity × clock / 225`. A hull in
air is convection-limited, that term is not restored, and it gets cheaper by nearly `clock / 225`.
Both halves are pinned on rigs that isolate one term each.

Measured over the 49-ship panel, worst environment per cell, against the 64 the caps grant:

| cell | reentry p99 | of cap | `G6` | projected at 300 m/s, p50 / p95 | of cap | `G6` |
| --- | ---: | ---: | :---: | ---: | ---: | :---: |
| shipped, ×1 / 225 | 73.4 | 115 % | **fail** | 69.2 / 82.4 | 129 % | **fail** |
| ×4 / 120 | 46.4 | 72 % | pass | 43.6 / 51.2 | 80 % | pass |
| ×4 / 100 | 38.6 | 60 % | pass | 36.3 / 42.6 | 67 % | pass |
| ×4 / 90 | 34.8 | 54 % | pass | 32.7 / 38.4 | 60 % | pass |
| ×4 / 80 | 30.9 | 48 % | pass | 29.1 / 34.1 | 53 % | pass |

**So the retune is cheaper here than the cost column made it look**, and its vacuum price is paid
in the environment where demand is 12 % of the cap rather than 115 % of it. It was read as *the fix
for a defect that already exists*; that reading is withdrawn — the defect is worth 0.028 K on the
hottest block ([stiffness.md](stiffness.md#what-refusing-the-demand-costs)), so the retune has to be
justified by `G8`'s timing after all, not by this. `PairSweep.EveryCandidateCellIsPricedInAir` is the run and `tools/corpus/air.py` scores it.

**What this does not measure.** The 300 m/s columns are a projection — demand is linear in the
convection coefficient, fitted per cell on its own three atmospheric points, and validated against a
held-out point below. `storm-parked` at clocks 80–100 hits the sweep's 7,200 s ceiling rather than
its stretched length, so its column is read at a slightly earlier state than the rule asks for;
`reentry` is not affected and is the binding case. And the demand recorded is the one the *last*
step needed, which is the same statistic `verdict.py` scores `G6` on.

### The route is chosen, and it is the one the cost column argued against

> **Applied 2026-08-24.** `ConductionScale` 9.6 and `HeatTimeScale` 90 are what a default world now
> runs, and what shipping them cost is at the end of this section. Two criteria moved with it: `G8`
> goes from holding on 1 % of resampled fleets to 76 %, and `G6` — which was failing in air, the one
> approximation the defaults shipped on — passes, because the demand it is scored on is
> convection-limited and came down with the clock. `G1`, `G2`, `G5` and `G7` were re-scored and
> hold. [backlog.md](backlog.md) `C24` is closed; `C25`, `C26`, `C27` and `C28` are what it opened.

Both routes reach `G8`'s window on the forty-hull retest set, so the choice was never about which
one works. **Resampled from the same population, they are not equally likely to keep working**:

| cell | crossing p50 | crossed | recovery p50 | `G8` holds on a resampled fleet |
| --- | ---: | ---: | ---: | ---: |
| shipped, ×1 / 225 | 10.4 s | 29/40 | 1,320 s | 1 % |
| waste ×0.5 / 110 | 122.4 s | 22/40 | 2,670 s | 24 % |
| waste ×0.5 / 100 | 134.5 s | 22/40 | 2,880 s | 37 % |
| waste ×0.5 / 90 | 149.5 s | 22/40 | 3,180 s | 33 % |
| waste ×0.5 / 80 | 168.1 s | 22/40 | 3,570 s | 27 % |
| conduction ×4 / 120 | 124.0 s | 25/40 | 2,220 s | 50 % |
| **conduction ×4 / 100** | 148.9 s | 25/40 | 2,640 s | **76 %** |
| **conduction ×4 / 90** | 165.4 s | 25/40 | 2,940 s | **76 %** |
| conduction ×4 / 80 | 186.0 s | 25/40 | 3,270 s | 72 % |

**The cliff is what separates them, and the median cannot show it.** `G8`'s crossing median is
censored above, so it exists only while more than half the hulls cross. The waste route puts 22 of
40 past critical and the conduction route 25 — two hulls either way is the difference between a
criterion satisfied and a criterion with *no median at all*, and this repository has already been
caught by exactly that once, at conductivity ×8 where 13 of 40 crossed and the cell read as the
grid's best. The share is printed beside every median for that reason; what it does not say is how
close a cell sits to the boundary, and the bootstrap does. Both halves of `G8` are drawn from one
resample of hulls rather than tested separately, because two medians over the same forty hulls are
not independent. `tools/corpus/scoring.py` holds it and `test_scoring.py` pins it.

**The cost column that argued for the waste route decides nothing, and that is now measured rather
than projected.** The air grid priced only the conduction cells, so the two routes were being
compared on a cost measured for one and projected for the other, which is the comparison `P6`
forbids. Measured on the same 49-ship panel, worst environment per cell, against the 64 the caps
grant:

| cell | worst p99 | of cap | `G6` | projected at 300 m/s, p95 | of cap |
| --- | ---: | ---: | :---: | ---: | ---: |
| shipped, ×1 / 225 | 73.58 | 115 % | **fail** | 82.36 | 129 % |
| waste ×0.5 / 100 | 32.70 | 51 % | pass | 36.60 | 57 % |
| waste ×0.5 / 90 | 29.43 | 46 % | pass | 32.94 | 51 % |
| conduction ×4 / 100 | 39.02 | 61 % | pass | 42.63 | 67 % |
| conduction ×4 / 90 | 35.12 | 55 % | pass | 38.37 | 60 % |

The waste route is about 16 % cheaper in air at the same clock. Both take a configuration that is
at 115 % of the cap to 41–73 % of it, so the difference between them is spent where nothing binds —
which is the same shape as the vacuum cost column that started this, and the reason that column
could not decide anything either.

**So the route is conduction ×4 with `HeatTimeScale` 90, and it is a trade rather than a free
win** — what it costs is below. It centres the window it exists to
reach — 165.4 s in 120–300 — holds it on 76 % of resampled fleets against the waste route's best of
37 %, recovers in 2,940 s against a 3,600 s bound, keeps `G1` at 0 %, `G2` at 100 % and `G5` at
100 %, and is the cheapest of its band in the environment `G6` is decided in. Between it and ×4/100
the two are indistinguishable on stability, and 90 has the more central crossing and the lower air
demand.

**And the provenance argument that blocked this row is moot.** It was an argument against the waste
route — that halving a fraction derived from the game's own stated efficiency is a balance knob
rather than a correction — and the route it was blocking is the worse one on the criterion itself.
Conduction ×4 departs from a pace that is already an admitted 2.4× the world's, which is what
`ConductionScale` is for.

**What shipping it costs, sized by attempting it, and the attempt is what the three levers turn out
to mean.** Setting `ConductionScale` to 9.6 and `HeatTimeScale` to 90 failed **52 checks across 28
classes** when it was attempted on 2026-08-24. Reading them rather than counting them, most are figures that legitimately move
with the pair — and four are the mod's own guidance failing, each in the words its own test uses:

* *"a coolant sink bought 73.5 K and the best surface dial bought 135.3 K; the surface dials have
  caught up and the guidance needs revisiting"* — a coolant sink stops out-performing every surface
  dial, which is what [blocks.md](blocks.md) tells a player to build for.
* *"thirty-two bolted radiators took 25.6 K off 890 K, which is more than a bolt joint should buy"* —
  bolting starts working, and *plumb it, do not bolt it* is guidance this contradicts.
* *"the census hull's stiffest block is 1.00 times stiffer in air than out of it … the hull has
  stopped responding to the air"* — the stiffest fitting on a hull stops feeling the atmosphere.
* *"a plain armour block bolted to the reactor saved 2.42 K; the control is meant to lose"* — the
  control in the cooling ladder stops being a control.

**So the cost column that called this route *three of the mod's levers* was not rhetorical**, and
this page's own earlier sentence — that the provenance argument made the choice easy — understated
it. The route is still the one the criterion selects, by 76 % against 37 %; what is now on the table
is that it buys that by making conduction strong enough to blunt three of the design signals the mod
is built around. **That is a trade to take deliberately or not at all**, and it is why `C24` is a
separate row rather than a follow-up commit.

**Both of the things it needed are measured.** `G7` — the stated compatibility floor, the one
criterion measured on ships nobody chose to put in a corpus — was re-scored on the 705 prefabs
before the defaults were committed: **idle, five simulated minutes, 0 of 705 crossing critical**,
and its control still bites at **481 of 705 losing a block flown hard** against 616 at the pair
before. And every temperature figure in this repository is measured at the shipped pair and moved
with it, which is 47 tests read one at a time rather than counted.

**What the reading found, beyond the four behaviours above**, each with a row of its own. A bolt
joint now conducts as hard as a coolant sink face — 1,168 W/K against 1,000, where the design
statement the guidance rests on had it six to one the other way (`C25`). The census hull has left
the population it stands in for at the one cap that ships, flooring 6.91 % of its own blocks against
a real population's 0.92 % (`C26`). Solver cost moved from air to vacuum: a settled grid there
demands 1.6x what it did, so the shipped element-visit allowance covers a smaller grid than it did
there. Measured since, and the interesting half is the other one: the allowance binds in *air*
first, at a third the size, and what a shortened step costs was priced and the default doubled
(`C27`). And the reactor's waste fraction was chosen against a bound this retune
removes, which no fraction restores (`C28`).

**Two things got better.** Three blocks came off the known-impossible list — a hydrogen engine that
could not shed its own heat at an index of 1.54 is at 0.43, because *shed* includes conduction into
the hull and the hull now takes four times as much. And the population's stiffness landscape
flattened: the two modes a single figure could not describe are 2.3x apart where they were 5.9x, and
half the corpus sits between them where six per cent did.

**The attempt also found a defect that has nothing to do with the retune.** The coolant path has no
overshoot clamp, so where a refused substep demand *approximates* on a block it *diverges* on a
loop, which on a hull carrying nothing stiffer is what sets the demand. It is
[backlog.md](backlog.md) `A10`,
nothing shipped reaches it, and it is recorded because
[stiffness.md](stiffness.md#what-refusing-the-demand-costs)'s refusal ladder reads as though it
described a whole grid.

### The 300 m/s constraint

Vanilla caps a grid at 100 m/s; the servers this mod is played on commonly run 300. The two terms
scale in opposite directions, and that is the whole finding:

The friction scaling and `FrictionScale` sweep below are historical fixture results. Production
Thermodynamics delegates heating to the registered Aerodynamics provider and has no
`FrictionScale` setting; these figures must not be used as its current heating law.

* **Friction goes as the cube of airspeed.** 100 → 300 m/s is **27×** the friction heat.
* **Forced convection saturates.** `h = h₀ × (1 + 0.1 √v)`, so the same change moves cooling from
  2.00× still air to 2.732× — **1.37×**.

Tripling the speed limit therefore multiplies the heating term by 27 and the cooling term by 1.37.
Whether that destroys atmospheric flight is set by `FrictionScale`, and friction is not the binding
term at 100 m/s: sweeping `FrictionScale` 0 → 4 moves the `flight-100` peak only 856 K → 878 K,
because convection dominates. At `reentry` it does bite: 300 K → 521 K over the same sweep.

Substep demand is linear in the convection coefficient. Fitting the two measured points at h=1
(`surface-hot-noon`) and h=2 (`storm-parked`) and testing against the held-out 200 m/s `reentry`
measurement — re-derived at the shipped quarter-second step, which is what the section above
corrects:

| | Fit | Predicted at 200 m/s | Measured at 200 m/s | Error | **Projected at 300 m/s** |
| --- | --- | ---: | ---: | ---: | ---: |
| p50 | 4.70 + 23.58h | 61.63 | 61.67 | −0.06% | **69.2** |
| p95 | 5.01 + 28.30h | 73.33 | 73.37 | −0.05% | **82.4** |

**300 m/s demands about 69 substeps at the median and 82 at p95, against `MaxSubsteps` 64.** It does
not fit: at the speed these servers actually run, the *median* ship in atmosphere is over the cap.
That is the same defect the section above measures at 200 m/s, one speed further on. This is a
projection from a validated fit, not a measurement; `flight-300` and `storm-300` exist in
[`Battery.All()`](../tests/Thermodynamics.Harness/Battery.cs) to measure it.

---

## What the data settles, and what it does not

**Settled.**

* **Design decides more than size.** Spearman rank correlation of peak against `w_per_m2` is +0.56
  at idle and `local_w_per_m2_max` +0.89 under full load, against +0.26 and +0.49 for block count.
  G4 holds.
* **The failure is per-block, not per-hull**, and it strands in one or two block types.
* **Timing is linear in `HeatTimeScale` and unaffected by anything that changes equilibrium**;
  equilibrium is unaffected by `HeatTimeScale`. The two axes are cleanly separable.
* **G1, G2 and G5 hold on shipped defaults. `G6` does not, in air.** It holds comfortably in
  vacuum — panel p95 7.1 against the 64 the caps grant — and fails at 200 m/s in thick air, where
  p99 is 73.4 and fourteen of forty-nine hulls are over the cap. It had only ever been scored in
  vacuum. See [Air is where the substep budget goes](#air-is-where-the-substep-budget-goes-and-the-shipped-pair-does-not-fit-it).

**Not settled.**

* **G3 has never been measured.** No corpus ship carries a cooling block — the filter rejects
  non-vanilla blocks — so the single highest-value missing run is the retrofit pass, and two of the
  five balance goals depend entirely on it.
* **Whether to ship the retune `G6` and `G8` now agree on.** Conductivity ×4 with the clock at
  100 satisfies `G8`, keeps `G1`, `G2` and `G5`, and takes the atmospheric demand from 115 % of the
  cap to 60 %. What is unbuilt is the pass itself, which re-quotes every temperature figure on this
  page ([backlog.md](backlog.md) `C12`).
* **The corpus has never seen air.** Five vacuum scenarios on 8,132 ships; air only on the 49-ship
  panel, so every atmospheric statement here rests on 49 hulls.
* **`full-electrical` charges every jump drive continuously**, so the scenario is a bound rather
  than a steady state — and since drives are 71.3% of population waste, that choice sets most of the
  shape of the load results.

### Open items in the block report

* ~~**The harness catalogue is not the blocks it stands in for.**~~ **Fixed 2026-08-23.** The six
  stand-ins derive from `Vanilla`'s build costs and the shipped derivation now, so a scenario
  reactor weighs 4,793 kg and wastes a hundredth of what it makes exactly as a player's does. Four
  of the six had been out by more than five per cent — the large thruster by **4.32×** at 10,000 kg
  against a real 43,200, the battery by 3.70× because it carried the *small-grid* battery's mass
  under a large-grid name — and `ReactorThermal` had said a quarter of a reactor's output becomes
  heat against the shipped hundredth. `CatalogDriftTests` now fails if a hand-typed mass reappears.
  **What it moved**: rigs whose source was incidental state their heat in watts through
  `GridBuilder.Wasting` and carry the same load they always did, so their figures are unchanged;
  the three scenarios that are *about* a reactor — `reactor`, `atmosphere`, `meltdown` — run a real
  reactor at a real rating and their temperatures moved with it.

---

## Reproducing this

```bash
python3 tools/corpus/verdict.py out/corpus-2026-08-21     # criteria and distributions
tools/corpus/build-report.sh                              # the browsable page
THERMAL_CORPUS_DATA=$PWD/out/blockheat dotnet test \
  tests/Thermodynamics.Tests/Thermodynamics.Tests.csproj \
  --filter BlockHeatIndexTests                            # block index, with pace
```

See [tools/corpus/README.md](../tools/corpus/README.md#the-ways-a-full-sweep-dies) for the
ways a full sweep dies, and [backlog.md](backlog.md) for what is still open.

---

## What the oxygen generator's fraction is worth on real ships

**`C21` took a third off the oxygen generator's waste on six blocks in two synthetic rigs.** That is
the right evidence for *can this block be built* and none at all for what it does to a fleet, which
is what [backlog.md](backlog.md) `C31` was opened to get. Measured 2026-08-30 with the cheapest
instrument the row named — one dial over the standing panel, not a walk — and read with
[knob.py](../tools/corpus/knob.py) against
[`summary-knob-oxygen-2026-08-30.csv`](../tools/corpus/summary-knob-oxygen-2026-08-30.csv).

| `oxygen-waste` | reached | Δpeak p50 | Δpeak p95 | Δpeak max |
| --- | ---: | ---: | ---: | ---: |
| 0.5× | 86 of 156 | **0.657 K** | **48.85 K** | 196.44 K |
| 1.5× | 90 | 0.733 K | 46.65 K | 127.22 K |
| 2× | 91 | 1.607 K | 78.17 K | 402.13 K |

**The dial's shape is the type's shape.** The generator is a median 14.4 % of the full-load waste of
the ships that carry one and 80.8 % at p90, and its dial moves the median reached ship by *under a
kelvin* while p95 is nearly fifty. Almost nothing on most hulls and tens of kelvin on a few, which
is the profile the composition predicted and which no rig of six blocks could have shown.

**Against `reactor-waste`, the precedent this row was told to expect nothing from**, measured by the
same instrument on the same panel in the same sweep:

| at 0.5× | reached | Δpeak p50 | Δpeak p95 |
| --- | ---: | ---: | ---: |
| `oxygen-waste` | 86 of 156 | 0.657 K | 48.85 K |
| `reactor-waste` | 86 of 156 | 0.180 K | 18.17 K |

**So the oxygen generator is 3.6× the reactor at the median and 2.7× at p95, on identical reach.**
The precedent held that a waste dial on one type moves nothing; it moves 18 K at p95 even for
reactors, and the type `C21` actually changed moves nearly three times that. The row was right to
refuse the precedent, and right about why: `reactor-waste` is a precedent about a type carrying
3.65 % of a *fleet's* waste, which is the statistic `C21` proved is the wrong one to reason from.

**What this does not do is reopen `C21`.** The fraction was decided on whether the block can be
built, and nothing here speaks to that. What it changes is that the population cost of the decision
is now known rather than assumed — under a kelvin for most ships, tens for the ships that are mostly
generator.

**Scope.** Fifty-two panel ships across three scenarios, of which 33 carry a generator at all — the
panel re-picked on the corrected census, where the old one held 6 of 50 because the reader built
31,214 vanilla generators as armour. It is a panel and not a population (`P1`), and a dial sweep and
not a walk: it says what the dial is worth on ships chosen for spread, which is what an hours-long
instrument can say and a days-long one would say better.


## Change log

| Date | Change |
| --- | --- |
| 2026-10-08 | Mark cubic-law heating sweeps as historical fixture evidence. |
| 2026-09-27 | Corrected the hand-cooling clock-independence claim and labelled the physical-energy/game-time window estimate. Analytic controls distinguish fixed joules, physical inventory and steady power; all 32 targeted hand-cooling and documentation cases passed under a 12 GiB/no-swap cap. |
| 2026-08-26 | *Coolant rings* re-measured after `C43`: the coupling column is **identical**, because mass is not in a conductance, and the settled column moved 4.4 K to 10.8 K. Corrected the sentence under it, which said the fluid mass does not grow with the ring — true when the charge was per loop, false since it became per pipe — and dropped a citation to `LongerRingsCoupleHarderAndCarryTheSameFluid`, a test that no longer exists. |
| 2026-08-26 | Noted on *What it did: the exploit was priced out by a fix aimed at something else* that every figure in it is at the flat 50 kg a pipe carried when it was measured, and that `C43` multiplied that by **10.31**: the grind is 243,490 W rather than 23,611 W, 8.15 % of the largest reactor rather than 0.79 %. **Left as measured** (`E10`) — the section is a record of registered predictions against outcomes, and the conclusion does not depend on the size, because `B44`'s vent takes the whole ring and a refill at the break-even excess is neutral at any charge. |
| 2026-08-26 | **The coolant density is applied** (`C43`). `CoolantKilogramsPerCubicMetre` is 33 kg/m³ — 515.6 kg a pipe on a large grid, 4.1 kg on a small one — and `CoolantMassPerPipe` stays as a flat-mass override defaulting to zero, so nothing that states a per-pipe mass is reinterpreted (`P15`). It moves a settled ring's mean by 6.9 K and 0.8 K, costs no substeps, and rights the swing between the sink face and the far side of the loop. `LoopCandidate` becomes `LoopBefore`: all three of its dials have shipped, and a candidate identical to the default is a lab arm that reports its own package as worthless. |
| 2026-08-26 | **The correction that blocked `C43` was measured on a rig with no sink.** *The coolant mass is doing an undeclared job* published four temperatures off a ring with a 125 kW source and the environment disabled — nothing settled, every arm climbed linearly, and at step 25,600 the same arms read 26,836 K and 5,908 K rather than the 715.5 K and 387.0 K printed at step 400. It measured a ratio of heat capacities and looked like a temperature. Re-taken with the ring radiating, the density correction moves the **mean** 6.9 K on a large grid and 0.8 K on a small one and the **swing** 100.2 K → 10.0 K and 7.7 K → 94.6 K: coolant mass buffers a ring, it does not decide where the ring runs. `LoopCoolantMassTests` now asserts the rig settles before reading anything off it. |
| 2026-08-26 | **Small grids are not the harder case, and this page had said they were on arithmetic rather than on evidence.** Per block over the eighty families the game ships at both sizes, a small variant is 2.12× behind on its own skin and 2.28× behind on the loop pickup and **2.5× ahead on conduction into the hull**; over the 8,137-ship census every column favours it, including **19.75×** the hull path per watt for the hottest block. Added *What a small cell is behind on, and what it is not* and *So small grids do not get their own pickup coefficient*: at three sink faces 79 of 80 families match large-grid answerability, closing the last one costs ×5.63 against a median need of ×2.28, and the handicap runs 0.83 to 25 because it tracks the game's per-block waste authoring rather than the cell. `CellSizeLab`, `CellSizeTests` and `tools/corpus/cellsize.py`. |
| 2026-08-26 | **Raised the coolant's pumped coefficient to 1,000 W/(m²·K) and made 160 the stopped value** (`C42`). The pickup — sink faces times `h · A` — is the only thing deciding whether a buried block has an answer, the face count is fixed by design, and at 160 a 6.4 MW jump drive was forced 6,400 K above its surroundings against a 689 K rating: no answer at any radiator count, on the block carrying 65.5 % of a loaded fleet's heat. A sink face now carries **6,250 W/K**, the forced gradient is **1,024 K**, and `StagnantTransferFraction` 0.16 keeps a stopped ring at exactly the 160 it always had, so nothing anywhere is worse. The corpus retrofit does not move (p50 2.43 % to 2.44 %), because `C40` bounds every internal path at 9 % — what changed is what a deliberate plumbing job can build. Re-measured the ring table, the bolt-to-sink ratio and every published sink figure. |
| 2026-08-26 | **Authored the radiator's emissivity at 0.85 and measured what it did not fix.** The block was a selective surface with only its low half; `What a selective surface is worth` had carried the missing half as an open decision for two passes and it is now taken, on fidelity grounds its own definition already stated. It multiplies the panel's radiating power by 2.43 and is worth a third more cooling per panel — and **0.1 %** to the median corpus hull. Added [The joint, not the panel](#the-joint-not-the-panel): a large radiator sheds 1.56 MW at 600 K through a joint carrying 2,336 W/K, and every surface dial in the sensitivity table leaves the ~174 kW crossing that joint within a tenth of where it was, while feeding the same panel from a coolant loop is worth 51.5 K. Re-measured the armour comparison and the ladder against the new panel; the saturation point is unchanged at two panels, which is the finding. |
| 2026-08-26 | Added `triage`, which ranks every heat-making block by severity times reach — the block index says whether a block is wrong and a census says whether being wrong matters, and the two had never been put together. Re-read the index while doing it: **one** block is impossible and **70** cannot cool themselves, where this page said four and 72 (`E10`). Three goals were also turned into criteria with a reading each — `G9`, `G10` and `G11` on [balance-lab.md](balance-lab.md) — and the one in doubt is `G11`, the time a warning buys.

| 2026-08-25 | **Decided `B43`'s currency: energy and time, because the exploit is worth 23,611 W.** All three registered predictions fail by two orders of magnitude and in the same direction. Grinding a pipe from a warm eight-pipe ring removes 188,889 J, which at the pipe's 8-second build time is **0.79 %** of the largest reactor's waste; on a ship at 900 K it is 4.78 %. The power that cancels it is 0.16 % of the median corpus ship's installed power. **`A12` priced the exploit out** — the row was written against a 190 MJ pump grind, and a grind now costs one parcel — and the rate cannot be scaled by building a bigger ring, since a grind costs one parcel whatever the length. So no component, no conveyor and no ice. |
| 2026-08-25 | **Put the oxygen generator's population figures on the re-censused population**, replacing the 400-ship parse that stood in while the census was broken: **64.5 % of ships carry one — the sample had that exactly — and their median share is 14.4 %** rather than 10.4 %, with p90 at 80.8 %. The sample was representative of carriers and not of the middle of the distribution; balance-lab.md carries why. |
| 2026-08-25 | **Corrected this page's oxygen-generator population figures in place, because the instrument behind them could not see the block** (`E10`). `Blueprints` built every empty-`SubtypeName` block as an armour cube, and the vanilla large oxygen generator is one of the thirteen definitions the game gives no subtype — so the census holds none of them and the *2,277 carriers, median 48.1 %* was measured over the ships carrying some other generator. Through the fixed resolver, on a 400-ship stride sample: **64.5 % of ships carry one and their median share is 10.4 %**, p90 61.4 %. *An oxygen generator is most of the heat there is on an ordinary ship* is withdrawn. The decision itself is untouched — it was made on the rig, which builds its blocks from the definitions and never went through the blueprint reader. The defect is `A13`. |
| 2026-08-25 | **Decided `C21`'s last open invention on the rig, and it went the opposite way to `C28`: the oxygen generator's fraction is 0.40, sourced.** The registered rule's first clause fired on a finding nobody was looking for — at the 0.6 that shipped, two of the six vanilla generators are past their own critical temperature *bare* at the draw their own definition rates, which is a block that cannot be built rather than a balance choice. 0.40 is the top of the band electrolysis sources and the highest value where all six survive both rigs. **Two of four predictions fail.** A skin *cools* a small heat source where it cooks a reactor, so this page's *ceiling and floor* is corrected in place (`E10`); and the population half was asked of the fleet and answered by the ship — 0.38 % of a loaded fleet's waste, a **median 48.1 %** of the waste of the 2,277 ships that carry one, and 60.0 % of the 2,003 carriers with no jump drive (`E6`). So it is a balance change and not the correction the row was filed as. |
| 2026-08-25 | **Registered the criterion for `C21`'s last open invention before measuring it**, in [Oxygen generator waste heat](#oxygen-generator-waste-heat-written-before-it-is-measured) (`E1`, `E11`). An oxygen generator wastes 0.6 of what it draws where electrolysis sources 0.20-0.40, which is the largest gap in `Cubes.xml`. The rule settles both halves — whether to move and where to — against the two precedents that point opposite ways, `C21`'s computer third that moved and `C28`'s reactor that did not, and four predictions carry the numbers that falsify them. Nothing has been run. |
| 2026-08-25 | `G6`'s cost half was rescored on a walk that carries the link count rather than the joint count, over all 8,144 blueprints: step work p99 **7,293,904** against the 4,000,000 granted, **1.82× over**, with 733 of 32,575 runs past it. The demand half passes at p99 34.8 of 64 and reproduces `F11` exactly. A per-block cap of 6 would take the cost half to 0.55× and is not being shipped, for the reason in [backlog.md](backlog.md) `C3`. |
| 2026-08-25 | Added [What the mod's blocks cost to build](#what-the-mods-blocks-cost-to-build), closing [backlog.md](backlog.md) `B33`. All eighteen `Cubes.xml` definitions sit inside the range the game prices its own 1,434 blocks over, on three shape-free ratios; PCU per cubic metre was a fourth and is now reported rather than judged, because it flagged only the large radiator and the vanilla blocks beneath it are vivariums and platforms. And a recipe reaches the transient and not the steady state: four times a radiator stack's mass moves the settled source by a hundredth of a kelvin and its settling time from 24 s to 112 s. |
| 2026-08-25 | Added [What a hand tool would have to be worth](#what-a-hand-tool-would-have-to-be-worth), which the block index could answer all along and nobody had asked: 26 five-kilogram CO2 bottles to return the median cooking block to ambient, 20 at once to do it inside its own window, and 71 of 72 with no surplus at all in the best case. It closes [backlog.md](backlog.md) `B32`, the original claim that `HeatTimeScale` cannot move it was corrected on 2026-09-27: physical bottle inventory and game-time window power have different clock dependence. |
| 2026-08-25 | Scoped the 2026-08-21 vacuum survey's tables to the pair they were taken at. *The shape, in one table* said **shipped settings, `HeatTimeScale` 225** and *How fast a ship crosses critical* said `HeatTimeScale` **is** 225; `C24` moved the pair to 9.6 and 90 on 2026-08-24, so both read as current and were not (`P1`). Nothing was re-measured — no walk has re-read the population in vacuum at the pair that ships — so the figures stand with their scope on them rather than being withdrawn. |
| 2026-08-24 | **Decided `C28`: the reactor fraction stays at 0.01, and the claim it supported comes down.** Re-measured at the pair that ships, 0.02 still cooks two of the four reactors *bare*, which is the bound this page calls unbuildable — so `C24` did not move which fraction is the only one where both bounds hold, and 0.01 is kept because the alternative was measured rather than for want of one. What did move is what it buys: burying a 300 MW reactor costs **50.7 K** rather than 355 K, about five per cent of the block's headroom, so *where a reactor is installed is a decision rather than a detail, and the first thing that makes a player want a coolant loop* is corrected in place. The population had already said the same and nobody had read it against the claim — `reactor-waste` swept sixteen-fold moves the corpus peak p50 by 3 %. The bite is thrusters and drives. |
| 2026-08-24 | Corrected this page's own account of `C12`'s choice, from the attempt at shipping it. The conduction route's *three levers* are four named behaviours — a coolant sink stops out-performing surface dials, bolting starts working, the stiffest fitting stops responding to air, and the cooling ladder's control stops losing — so the route is a trade taken deliberately rather than the easy choice the previous entry implied. |
| 2026-08-24 | **Shipped it, and read what it cost.** `ConductionScale` 9.6 and `HeatTimeScale` 90 are the defaults; `G7` was re-scored first — 0 of 705 prefabs crossing critical at idle, 481 of them still losing a block flown hard — and `G6` passes on the shipped configuration for the first time, at 55 % of the substep cap in the worst environment against 115 %. Reading the 47 tests the pair moved found four rows rather than four figures: a bolt joint conducts as hard as a coolant sink face (`C25`), the census hull has left the population at the shipped cap (`C26`), solver cost moved from air to vacuum (`C27`), and the reactor's fraction was chosen against a bound this removes (`C28`). Three blocks came off the known-impossible list and the population's two stiffness modes closed to 2.3× apart. |
| 2026-08-24 | **Chose `C12`'s route, and the choice inverted on evidence the cost column could not carry.** Both routes reach `G8`'s window; resampled from the same population, conduction ×4 holds it on 76 % of fleets and waste ×0.5 on 33–37 %, because the censored median has a cliff at half the hulls crossing and the waste route sits two hulls from it. Priced the waste route in air for the first time — the two routes had been compared on a cost measured for one and projected for the other — and both take `G6` from 115 % of the cap to 41–73 %, so the cost decides nothing. The route is conduction ×4 with `HeatTimeScale` 90; shipping it is `C24`. |
| 2026-08-23 | **The loop's fluid coupling is stated as a heat transfer coefficient** ([backlog.md](backlog.md) `C20`): 160 W/(m²·K), which is exactly what the old quality-against-a-reference came to on a large grid, so every cooling figure on this page still describes the loop it was measured on. What moves is small grids, where the old formula implied 800 W/(m²·K) for the same fluid because it divided by half a cell — a small-grid ring now couples a fifth as hard, worth 45.7 K on a four-by-four rig. |
| 2026-08-23 | **Authored the mod's first selective surface and measured what it is worth** ([backlog.md](backlog.md) `C15`). The radiator absorbed sunlight at its emissivity because nothing declared otherwise; `SolarAbsorptivity 0.1` is what a second-surface mirror or white paint gives, and it is worth **10.9 K** to a sunlit stack and nothing in shadow. The finding beside it: raising emissivity alone would buy 1.9 K in sunlight, because a better emitter that is also a better absorber gives it back — the two changes are complementary, and the obvious one alone is nearly worthless in the sun. |
| 2026-08-23 | **Closed `C4`: the scenario catalogue is the blocks it stands in for.** The six stand-ins derive from `Vanilla`'s transcribed build costs and the shipped derivation rather than being hand-typed, so a scenario reactor wastes the hundredth a player's does instead of the quarter the catalogue asserted, and the large thruster weighs 43,200 kg instead of 10,000. Rigs that only wanted a heat source now state it in watts of heat (`GridBuilder.Wasting`) and are unchanged; the scenarios that are about a reactor moved. |
| 2026-08-23 | **Closed `C2`: its two headline figures are censored and every uncensored one is unmoved.** A hull with anything past its rating keeps generating undamped for the rest of the clock, so its peak is a harness artefact (`E9`) — and that is 34 of 40 hulls in `burn-forward` and 29 of 40 under load, which is both of the rows the finding rested on. Crossing moves −0.13 s, first loss +0.50 s and the share over critical 0.000. The block-count totals are a wash per hull as well: 14 better, 11 worse, 15 unchanged, one hull carrying a third of the −257. `retest.py` had been testing the peak against 1,500 K rather than against each block's own rating, which reported 9 % of the run censored where 31 % was, and 85 % in the scenario the finding came from. |
| 2026-08-23 | **Priced the load route's dial against the provenance of what it scales, and it argues against the route.** Every waste fraction in `Cubes.xml` now states where it came from, and weighted by the heat each carries over the census, **76.3 %** of a loaded fleet's waste comes through a fraction derived from the game's own `PowerEfficiency`, 8.4 % through a sourced conversion and 15.3 % through an invention. So waste ×0.5 halves a sourced number — an admitted balance knob rather than the correction of a guess — and the invented sixth sits on four block types that between them cannot reach the window. |
| 2026-08-23 | **Gave `G8` a load case that is an event, and it selects the cells the grids already found.** A jump drive holds 3 MWh, draws 32 MW and keeps 80 % of it, so it fills in **421.9 s** — every figure off the game's own definition — and `jump-charge` runs exactly that: charge, finish, hold. Measured across it, both routes satisfy `G8` and the shipped pair does not, at 10.4 s. **This corrects the entry below**: `full-electrical-charged` is a ship not having an event, not a bound on one, and reading its censored medians as evidence against the routes was reading an absence as a short crossing. The transient reproduces the charging bound to the last figure, because every hull that crosses does so in the first minute of a seven-minute charge — which is the defect in one sentence. |
| 2026-08-23 | **Measured `F13`, and it withdraws both of `C12`'s routes.** `full-electrical` charges every jump drive for the whole run; run against the other bound — the same load with the drives full — the conduction route crosses on 6 of 40 hulls, the load route on 3, and the shipped configuration on 12. All three are censored, so none has a median and none satisfies `G8`. **The mod is both too fast and too slow depending on one block**: 10 s to cross with drives charging, never with them full. That is a fact about the criterion rather than the dials — `G8`'s *most significant thermal event* is the drive charging, which is a transient, and both scenarios model it as permanent or absent. What it needs is a duty-cycled load. The comparison also reproduced the pair grid's four cells to the last figure across two independent runs, and found that the jump drive was filed as a *tool*, so `State.Consumers` never reached 71.3 % of the load's heat — neutral on every published figure, checked against 1,794 rows of which none moved. |
| 2026-08-23 | **The significance window is reachable without touching transport.** Eighteen cells of the load against the clock: `G8` is satisfied by waste ×0.5 at `HeatTimeScale` 110, 100, 90 and 80, at conductivity ×1 — so plumbing, radiation geometry and air are all untouched — while keeping `G1`, `G2` and `G5` and costing 0.43× the substep demand, which also takes `C19`'s atmospheric breach from 115 % of the cap to about 50 %. **The ratio is the quantity**: `recovery / crossing` is 127 at the shipped load and needs 30 or less, the clock cannot change it because both halves go as one over the clock, and the load can — 315 at ×2, 127 at ×1, 21 at ×0.5. It costs the bite rather than the levers: the median hull peaks 264 K cooler and loses one block instead of four. **And it rests on a confound**: `full-electrical` charges every jump drive continuously (`F13`), so a ×0.5 multiplier is within the distance between that bound and a realistic load, and the alternative reading is that nothing needs retuning at all. |
| 2026-08-23 | **Built `C12`'s retune, measured it, and did not ship it.** `ConductionScale` 2.4 → 9.6 with `HeatTimeScale` 225 → 100 satisfies `G8` and fixes `C19`, and costs three of the mod's four levers: a coolant sink stops out-performing the best surface dial (195.3 K vs 41.5 K becomes 73.3 K vs 135.3 K), bolting starts working, a 300 MW reactor buried in armour settles inside its rating, and the stiffest block on the census hull stops responding to air at all. The sweep that chose ×4 scored `G1`, `G2`, `G5`, `G8` and cost, and never scored `G3`. Also found the trap underneath it: the game has two conduction paces and nothing made them agree, so raising the solid one alone weakens every coolant loop by four relative to the structure it competes with — `ConductionPaceTests` now fails if one moves without the other. |
| 2026-08-23 | **Priced `C12`'s candidate cells in air, and the answer inverted the question.** The cost column that made the retune a decision was a *vacuum* column, where demand is 12 % of what the caps grant; in air it is 115 % of it. Two findings came out of the same run. **Every atmospheric figure this page carried was exactly half**, taken when `Frequency` was 8 and never re-derived when the shipped step became a quarter second — measured on the same 49-ship panel, all eight are 2.000× what was published. **And the shipped configuration fails `G6` in air**: p99 substep demand at 200 m/s is 73.4 against 64, fourteen of forty-nine hulls are over the cap, and at the 300 m/s servers run the *median* ship is. Read there, all four candidate cells pass and the shipped pair is the only one that does not, because lowering the clock divides every stiffness term while raising conductivity restores only conduction. The retune is the fix for a defect rather than a cost to be justified. |
| 2026-08-23 | **Measured the two dials together and found the significance window**, which is `C12` and the one thing every sweep before it could not answer: `G8` is satisfied by conductivity ×4 at `HeatTimeScale` 80–120, crossing at 124–186 s and recovering in 2,220–3,270 s, at 1.36–2.04× the shipped substep demand and with `G1`, `G2` and `G5` all kept. **Corrected the projection this page carried**, which put the window at ×4 with the clock near 15 — measured, that cell crosses at 991 s, five times its prediction, because the two curves multiplied together came from different scenarios and from a median over the ships that crossed. The composition rule itself holds to 1 %. **Conductivity ×8 is excluded by the criterion rather than by cost**: only 13 of 40 hulls ever cross there, so the population has no median crossing at any clock. |
| 2026-08-23 | **Measured what the real-unit conversion did to the ships people fly**, which is what was left of `C2`. Forty retest hulls through seven scenarios in the shipped world and four counterfactual ones recovered exactly from `Cubes.xml` at `4f6b44a^`: a burning hull is **+34.7 K** hotter than before the conversion and a loaded one **−16.3 K** cooler, the arms attribute each to one family, and **no criterion moves** — G1, G2 and G5 are identical in all five worlds. What changed for a player is the spread rather than the peak: under load the peak fell 16 K and **192 more** blocks went over critical, under thrust the peak rose 35 K and **257 fewer** did. Also corrected the published per-block table, which described only the four families `Cubes.xml` authors and said *thrusters went 0.6×* — true of the hydrogen ones alone, against **0.23×** for ion and **1.27×** for atmospheric. |
| 2026-08-23 | Settled one of `C2`'s three claims with a measurement rather than a retune: `OverheatDamagePerKelvin` was authored for the per-step damage rule, which at `Frequency` 8 bit eight times harder, and restoring that intent would put the median block's whole life past its rating at **6.5 s**. The authored values stay. |
| 2026-08-23 | Answered the damage-timing question with the event that costs a player something. The section had been called *"damage arrives too fast to be played around"* and quoted a **median 8.9 s** that is the *crossing* — the moment the damage rate leaves zero. The first block is lost at a median **37.0 s** under full electrical load, the median ship's own crossing-to-loss gap is **24.2 s**, and with the cue's three-second lead the window is 40 s. `G5`'s rationale holds. Split into [the crossing](#how-fast-a-ship-crosses-critical) and [the loss](#how-long-a-block-has-after-it-crosses), which is the section `BlockHeatIndex` has cited since it was written and which did not exist. |
| 2026-08-22 | Measured the catalogue drift this page had recorded as *up to 4×*: it is **4.32×** at worst, on the large thruster, and four of six blocks are out rather than three. `CatalogDriftTests` pins all of it, armour included — matching exactly is what makes the rest a measurement ([backlog.md](backlog.md) `C4`). |
| 2026-08-22 | Added [The compatibility floor holds](#the-compatibility-floor-holds). Every one of the 705 prefabs the game ships has now been simulated — the criterion `G7` was written down first ([balance-lab.md](balance-lab.md)) — and none loses a block arriving, against 616 of 705 that do when flown hard. |
| 2026-08-22 | Restored the damage-timing finding, which the merge of `corpus-shape.md` and the register had dropped, and corrected its idle figure: the median time to critical at idle is **104.5 s**, matching this page's own shape table, where the prose had carried 112 s — one of the eighteen values rather than their median. |
| 2026-08-22 | Merged `corpus-shape.md` and the corpus findings from `known-issues.md` into this page, so the block-level argument and the population that tests it sit together. **Corrected the jump-drive figures**, which stood at three different values across three pages: recomputed from `composition.csv` and `outcomes.csv`, `LargeJumpDrive` alone is 10,567 blocks on 2,183 ships and 67.1% of full-load waste, while all `JumpDrive` subtypes together are 10,954 on 2,253 and 71.3% — both previous figures were right over populations neither page named. The outcome split is 2,249 ships carrying one against 5,883 without, at 100.0% and 65.5% reaching 400 K; the register had 2,255/5,887 and quoted G2 as both 67.1% and 65.6% in adjacent paragraphs. **Corrected the local-W/m² banding table**, which dropped 894 of 8,132 ships and omitted the 50,000–200,000 band entirely. The block-index tables, the cooling ladder and the reactor sweep were re-verified against the datasets and are unchanged. |
| 2026-08-22 | Fitted cooling to ships people actually built, and said which half of the cooling criterion the ladder answers and which it does not. |
| 2026-08-21 | Read the first complete corpus survey — 8,142 ships — for balance: the bimodal distribution, the per-block failure, the local-W/m² rule, and the block heat index as a gate. Recorded that the clumping index does not predict hot spots and the local figure does. |
| 2026-08-20 | Measured the reactor waste fraction in two bracketing rigs rather than picking it by analogy, and settled on 0.01. Added the whole-game cooling ladder, which found nothing in the game solves a reactor by being bolted to it. |
| 2026-08-19 | Opened the block report: capacity, shedding, the joint and delivered steady state, each costed against the vanilla block it displaces. Established that a coolant sink face carries six times what a bolt joint does. |
