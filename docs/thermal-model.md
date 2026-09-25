# The thermal model

Every equation the simulation evaluates, with the file that evaluates it, and the surface geometry
every area term in them reads.

All temperatures are Kelvin, all rates are Watts, all areas are m², all masses are kg. The solver
holds one state variable per node — temperature — and derives everything else. A step accumulates
watts per node from every mechanism, then applies them all at once.

> The rules argued here are stated canonically in [rules.md](rules.md): `C6` `E7`.

| Looking for | Go to |
| --- | --- |
| What the air, ground, sun and wind outside the grid are | [environment.md](environment.md) |
| Which component calls the solver, and when | [architecture.md](architecture.md#update-order) |
| The settings that scale every term | [configuration.md](configuration.md) |
| The block and planet properties these equations read | [definitions.md](definitions.md) |

---

## The solver

### Nodes

Three kinds of thermal mass exist. They integrate identically; only their capacity differs.

| Node | Capacity, J/K | Built by |
| --- | --- | --- |
| Block | `SpecificHeat × Mass / HeatTimeScale` | [ThermalNode.cs](../Thermodynamics/Core/Simulation/ThermalNode.cs) |
| Coolant loop | `SpecificHeat × Mass / HeatTimeScale` | [CoolantLoop.cs](../Thermodynamics/Core/Loops/CoolantLoop.cs) |
| Room air | `Volume × RoomAirDensity × Pressure × 1005 / HeatTimeScale` | [RoomAir.cs](../Thermodynamics/Core/Surfaces/RoomAir.cs) |

Capacity is floored at `ThermalConstants.MinimumThermalMass` so a zero-mass block cannot divide by
zero. `SpecificHeat` is in real J/(kg·K); `HeatTimeScale` is the single global divisor that turns
real thermal time into playable thermal time — see
[configuration.md](configuration.md#time-and-pace).

### Integration

[ThermalSolver.Step](../Thermodynamics/Core/Simulation/ThermalSolver.cs) advances
`deltaSeconds` of simulated time:

```
substeps = ceil(deltaSeconds × maxRate / 0.5)        clamped to [1, MaxSubsteps]
h        = deltaSeconds / substeps

repeat substeps times:
    watts[] = 0
    accumulate environment, conduction, coolant loops, room air
    T[i] += watts[i] × h / capacity[i]
    T[i]  = max(T[i], 0)
```

`maxRate` is the stiffest node on the grid: its total conductance plus its linearised radiative and
convective coupling, divided by its capacity.

```
rate = Σ G_links + 4 ε σ A T³ + h_conv A_exposed
```

**A handful of light fittings therefore set the substep count for a whole capital ship.** That is
the dominant performance fact about this model and it has its own page —
[stiffness.md](stiffness.md).

### The three invariants

These are the definition of correctness; everything else is tuning.

| Invariant | Why it holds | Checked by |
| --- | --- | --- |
| **Order independence** | Every exchange reads the temperatures at the start of the substep and writes into an accumulator, so no node sees another's new value. Iteration order cannot change the result, and the pass could be parallelised without changing it either. | `ConductionTests` |
| **Energy conservation** | Every internal exchange is applied equally and oppositely. `Solver.TotalEnergy` is constant on a closed grid. | `ConductionTests` |
| **Boundedness** | With `ClampConductionOvershoot` on, every pairwise exchange is capped at the energy that brings the pair to their shared equilibrium: `E_max = ΔT × (m_a m_b) / (m_a + m_b)`. | `StabilityTests`, `ConductionClampGateTests` |

Substepping keeps the answer accurate; the clamp keeps it sane when substepping alone cannot.
Reaching `MaxSubsteps` (64 as shipped) is reported as `LastStepWasClamped` and appears in the
telemetry.

---

## The mechanisms

### Conduction

Between two touching blocks, over the area where both carry a mount surface
([ThermalLink.cs](../Thermodynamics/Core/Simulation/ThermalLink.cs)):

```
A_contact = cells_shared × coverage_a × coverage_b × gridSize²
G         = A_contact / (L_a / k_a + L_b / k_b)          W/K
watts     = G × (T_b − T_a)
```

Two conductors in series: centre of A to the interface, interface to centre of B. `L` is each
block's half-depth along the contact axis, so a long block conducts more slowly end to end than a
cube does. `k = Conductivity × ThermalConstants.ConductionScale`, where `Conductivity` is the
definition's figure in **real W/(m·K)** — mild steel 50, glass 1, copper 400 — and `ConductionScale`
(**9.6**) is one global constant setting the game's pace, so the definition stays a description of
the material rather than a balance dial. It was 2.4 until `C24`, which is the value that puts mild
steel exactly where the pre-conversion world put it; the shipped pace is four times that, and what
bought it is `G8`'s significance window — see
[balance.md](balance.md#the-route-is-chosen-and-it-is-the-one-the-cost-column-argued-against).

**A coolant loop's coupling is not this.** It is a heat transfer coefficient in W/(m²·K) — 160,
what the transfer physically is — and no conduction pace touches it, because it is a fluid against
a wall rather than a solid against a solid (`C20`). This paragraph said it was still the older 0…1
quality against a 200 W/(m·K) reference until 2026-08-24, and pointed at a page that already said
otherwise. One consequence is deliberate and is `C25`: a bolt joint carries 1,168 W/K where a sink
face carried 1,000 before `C42` raised the pumped coefficient to 6,250, so a steel bolt used to
out-couple a water-cooled plate face for face and no longer does.
See [definitions.md](definitions.md#conductivity-is-in-real-wmk) and
[blocks.md](blocks.md).

Blocks that touch without mount surfaces on both sides conduct **nothing**. This is what makes
armour skins, offset blocks and open frames behave differently from a solid slab.

Links are rebuilt only when the block layout changes, never per step. Adjacency comes from
`IBlockAdjacency`, which defaults to the grid's own cell map and can be replaced by a host with a
better index.

**Across a mechanical joint.** Two blocks either side of a rotor or piston belong to different grids
and therefore different solvers, so their link lives outside both, in
[ThermalBridges.cs](../Thermodynamics/Game/ThermalBridges.cs). Bridges exchange once
per ten-frame tick, under the same clamped, energy-conserving rule.

### Radiation

Every exposed face radiates to the ambient sky:

```
watts = −ε σ A_exposed × (T⁴ − T_ambient⁴)
```

σ = 5.670374419e-8. `A_exposed` is the count the surface mapper produced — see
[Exposure](#exposure). **A block with no exposed face neither radiates nor absorbs.**

**What arrives is a different coefficient from what leaves.** Emission uses `ε`; the sun and every
point source use `α`, the block's `SolarAbsorptivity`. Undeclared, `α = ε` — the grey-body
assumption, which is where this model started and what every block still does unless somebody
authors otherwise. Declaring the two apart is what a selective surface is: a real radiator sheds in
the thermal infrared at `ε ≈ 0.8` and takes in the visible at `α ≈ 0.1`, and while one number did
both jobs that surface could not be described at all. See
[definitions.md](definitions.md#emissivity-and-absorptivity-are-two-numbers).

### Convection

Into the surrounding atmosphere:

```
watts = −h_eff × A_exposed × windFactor × (T − T_ambient)
h_eff = ConvectionCoefficient × (1 + 0.1 √v_rel)
windFactor = 1 + faceWeight(wind)                (1.0 in still air)
```

`faceWeight(d)` is the exposure-weighted average of `max(0, faceNormal · d)` over the block's six
faces, so a face turned into the airflow sheds twice what one in the lee does.

**A wind never sheds less than still air.** Forced convection adds to natural convection rather
than replacing it, so the factor runs from 1 upward and a lee face keeps what it has. A floor of
0.5 gives the same two-to-one contrast and the wrong answer: most of a closed hull's exposed faces
do not point into the wind, the geometric term then loses more than the speed term gains, and a wind
under about 50 m/s is a net **warmer** — a hull making 2 MW settles 0.9 K hotter in a 40 m/s wind
than in still air.

Radiation and convection are blended by how fluid the atmosphere is:

```
total = (1 − atmosphereFactor) × radiation + atmosphereFactor × convection
atmosphereFactor = 1 − (1 − airDensity)⁴
```

A block in vacuum is pure radiation; a block at sea level is pure convection. The curve saturates
quickly: at a quarter density the air already behaves 68% like sea level.

### Room air

A sealed room holds one well-mixed air mass that exchanges with every surface bounding it
([ThermalSolver.AccumulateRoomAir](../Thermodynamics/Core/Simulation/ThermalSolver.cs)):

```
G     = RoomConvectionCoefficient × faces × gridSize² × ExposedSurfaceMultiplier
watts = G × (T_air − T_block)
```

This is the only path heat has between two walls of a compartment that do not touch. Air has little
capacity and a great deal of contact area, so on a pressurised ship it is usually what sets the
substep count.

How a room gets its air, and what can take it away, is in [Room air](#room-air-1) below.

### Solar and point sources

Solar gain, when the grid is not occluded:

```
watts = solarEnergy × ε × faceWeight(sun) × A_exposed × litFraction
solarEnergy = SolarEnergy × (1 − occludedShare) × (1 − SolarDecay × atmosphereFactor)
```

Occlusion against the rest of the world is resolved **per grid**, every `SolarOcclusionInterval`
steps. Planets are tested analytically by angular size; terrain by walking ground heights along the
sun ray ([TerrainHorizon.cs](../Thermodynamics/Core/Simulation/TerrainHorizon.cs)), for
grids near a surface and only once the planet's own horizon test says the sun is up; voxels by a
physics raycast; other grids by a ray against their blocks. Each of the three is a separate switch,
because each costs a different amount. Being underground forces full occlusion.

The result is a fraction, not a flag: `SolarOcclusionSamples` points spread through the hull are each
tested, and `solarEnergy` is scaled by the share that reached the sun. One sample — the default — is
a single ray from the grid's centre and gives 0 or 1.

`litFraction` is **the grid's shadow on itself**, and is 1 for every block when `SolarSelfShadowing`
is off. When it is on, [SunShadowMap.cs](../Thermodynamics/Core/Simulation/SunShadowMap.cs)
walks toward the sun from the air just outside each block face, one cell at a time, until the ray
leaves the grid's bounding box: cross anything solid and that face is shadowed. It is a standard
voxel traversal, so the ray visits every cell it passes through and cannot slip diagonally between
two blocks that touch.

**The question is asked of a face, not of a block, and that distinction is the whole model.** A wall
two cells thick has an inner layer that cannot see the sun from its own centre — but the inner
layer's side faces are on the outside of the same wall, looking out of the same flank of the ship,
in full sunlight. Ask per block and a solid hull ends up lit along a single row with the rest dark,
which is wrong in the direction that matters: those flanks are most of the area.

At `SolarGridShadows = full`, other grids cast their shadows through the same walk. Each nearby grid
is folded into a single matrix — this grid's cells to metres, metres to the world, world to the
occluder's metres, its metres to its cells — and the ray is carried into that frame and walked
against its blocks. Two lattices that share no axis, origin or scale are then the same problem as
one.

A pass starts only when the sun has moved more than 2°, the grid's blocks have changed, or a
neighbouring grid has moved — seconds apart on a planet — and is spread over ticks in slices of
`SunShadowBudget` cells, with the previous answer readable until the new one completes, the same way
the room mapper spreads its flood fill.

`faceWeight` and `litFraction` answer different questions per face and both are needed: the first is
how square that face is to the sun, the second is whether anything of the ship stands in the way.
The [self-shadow scenario](../tests/Thermodynamics.Harness/Scenarios.cs) measures both on a solid
slab: the face turned to the sun is lit whole, the flanks around 80%, a recess cut into the hull 0%.

**Point sources** registered by other mods use the same equation with their own direction and
irradiance:

```
watts = irradiance × ε × faceWeight(source) × A_exposed
```

The host reduces a source to a direction and an irradiance before the solver sees it. For a source
of `P` watts at distance `r`,
[ThermalHeatSources.cs](../Thermodynamics/Game/ThermalHeatSources.cs) uses
`irradiance = P / (4π r²)`, with `r` floored at 1 m. See [api.md](api.md#heat-sources).

### Aerodynamic friction

```
if airDensity > 0.01 and v_rel > FrictionAtSpeedsAbove:   # the floor ships at 0
    watts = FrictionScale × airDensity × v_rel³ × A_exposed × faceWeight(wind)
```

The v³ law matches the scaling of convective heating in hypersonic flow; `FrictionScale` is a
game-feel coefficient. `v_rel` is the wind minus the grid's own velocity, composed in
`EnvironmentSample.ComposeRelativeWind`, so a stationary ship in a storm heats like a fast ship in
still air. The wind is the mod's own field rather than the engine's rating — see
[environment.md](environment.md#wind).

**This is heat, and the mod applies no aerodynamic force.** The drag, lift and top-speed force the
mod applied until 2026-09-24 were derived from these watts, and were stripped so that the force can
be rebuilt from a surface profile of its own; [aerodynamics.md](aerodynamics.md) holds the
requirements and the plan. What this row computes is untouched by that, apart from the shape factor
described below.

**The term is live at every speed, and the cooling-to-heating crossover is emergent.**
`FrictionAtSpeedsAbove` shipped at 50 m/s for ten months as a guard against low-speed heating, and
the v³ law makes the guard redundant: at 10 m/s the term is about 1 W per windward m² against
1,000 at 100 m/s, so slow flight is cool by arithmetic rather than by a gate. Convection removes
`h·A·(T − T_ambient)` and grows with `√v` ([environment.md](environment.md#wind)), friction adds
`∝ v³`, so slow air net-cools a hull that is warmer than ambient and fast air net-heats it — at a
speed that depends on how hot the hull is, which is why no constant could name it. A hull *at*
ambient is heated by any airspeed, because there is nothing for the convection half to remove. The
floor remains a dial for worlds that want the legacy cut, and the aero debug view
(`DebugAeroOverlay`) reports the net air watts and the crossover speed for the hull as it stands.

**The expression is drag power, and that is why it cannot also be the force.** Real drag power is
`½ C_d ρ A v³` — the same expression, with `FrictionScale` in the place of `½ C_d` and
`A_exposed × faceWeight` in the place of the frontal area. Written out, `FrictionScale = ½ · C_d · η`,
where `η` is the share of the work done against drag that ends up in the *surface* rather than in the
wake; at the shipped 0.001 against a bluff body's `C_d ≈ 1`, `η` is about 0.002, a game-feel figure
rather than a measured one. Base drag leaves its energy in the wake, so it heats nothing and cannot be
read off this row, which is one of the reasons the rebuilt force is a channel of its own
(aerodynamics.md, `AR4`).

**The row sees a projected area, and a projected area is not a shape.** What it sums is exposed cell
faces weighted by `max(0, dot(faceNormal, wind))` over the six axis directions, so a stair-stepped
wedge presents exactly the wind-facing faces of the brick that shares its frontal cross-section.
`DragShapeTests` builds that pair — 64 blocks against 40 — and the two take **identical** friction
watts, 172,800 W each at 120 m/s in thick air, while their real drag coefficients differ by about ten
times. For heat that is an accepted limit; for a force it is the first thing the rebuild has to pass.

**Windward shielding is a switch, and it moves temperatures.** `EnableWindwardShielding` runs the
sun's self-shadowing pass aimed at the relative wind, so a face in another block's lee contributes
only what the wind can reach of it. It ships **off**, because the six-face sum it modifies is read by
the *convection* factor as well as the friction row: sheltering a face reduces the forced convection
over it, which is physically right and measurably large — on a sheltered pair driven in thick moving
air the shielded hull runs **7.4 K hotter**, 316.1 K against 323.5 K. Its cadence is not the sun's:
the wind direction is grid-local, so it moves when the *ship* turns, and at the sun's 2° threshold a
capital hull's 1.75 s pass would restart five times a second under a 10 °/s yaw and never complete.
The threshold is 20°, worth 2.0 % of faces in staleness, and a running pass is never restarted.

### The surface normal, and why the friction row no longer uses it

**`ShapeNormal` reconstructs the direction a block's surface faces from the occupancy around it** —
the negative gradient over a radius of one — so a 3×3×3 neighbourhood straddling a slope has its
occupied half on one side and the sum lands on the diagonal. The cells down the wedge's windward
staircase come back as `(0, 0.707, 0.707)` exactly; a 45° slope reads the same at radius one, two and
three, and radius one cannot tell a 26.6° slope from an 18.4° one, while wider radii start
overshooting. `ShapeNormalGeometryTests` and `ShapeNormalOneCellTests` pin the geometry.

**It is surface data now rather than a term.** From 2026-08-31 to 2026-09-24 the friction row was
multiplied by the Newtonian `sin²θ` of each node's normal against the wind, which took the brick to
100,800 W and the wedge to 82,215 W, and a median 4.56 K off a published hull at re-entry. That factor
existed to correct the force derived from these watts, and it left with the force: the row is the
projected area again, pinned bit for bit against the code that computed it with the factor off
(`AerodynamicStripTests`). The solver keeps no normals; the force profile computes them when a hull
changes, which is when they change (aerodynamics.md, `AR4` and `AR9`).

### Waste heat

Recomputed only when the game reports a change, never per step
([ThermalNode.RefreshHeatGeneration](../Thermodynamics/Core/Simulation/ThermalNode.cs)):

```
watts = produced × ProducerWasteEnergy + (consumed + thrust) × ConsumerWasteEnergy
```

* `produced` and `consumed` are electrical watts, from `MyResourceSourceComponent.OutputChanged` and
  `MyResourceSinkComponent.CurrentInputChanged`. **Which of the two fractions applies to a block is
  decided by the game, not by the definition.** A reactor delivers through the source component, so
  only its producer fraction can ever heat it and its consumer fraction is dead text; a thruster is
  the reverse. This is the one thing a definition can get wrong with no other symptom — a reactor
  with `ProducerWasteEnergy` 0 reports 0 W forever — so
  `EveryPowerProducerConvertsSomeOfItsOutputToHeat` checks the producer types by name.
* `thrust = ForceMagnitude × (CurrentThrust / MaxThrust)` — the thruster's force in newtons used as
  a watt-equivalent. A balance proxy rather than a conversion, and what makes hydrogen thrusters
  heat: they draw no electricity, so thrust is the only term that can represent them.

Both fractions are balance figures rather than efficiencies. The reactor's is the clearest case:
Space Engineers rates a 3×3×3 block at 300 MW, so a real plant's efficiency applied to it would
destroy every large reactor in the game in a build no player could improve. See
[balance.md](balance.md#reactor-waste-heat) for how the shipped 0.01 was measured.

### Coolant loops

A closed ring of pipe blocks carries one parcel of coolant per pipe
([CoolantLoopBuilder.cs](../Thermodynamics/Core/Loops/CoolantLoopBuilder.cs)). It links
to the pipe blocks it runs through, and to whatever block sits behind each pipe's sink faces:

```
G_pipe  = Conductivity × ReferenceConductivity × A_pipe  / L
G_plate = Conductivity × ReferenceConductivity × A_plate / L
watts   = G × (T_loop − T_block)
```

Each parcel exchanges only with its own pipe and the blocks on that pipe's sink faces, so heat
reaches the far side of the ring only by being carried there:

```
parcels/s = (flowRate / cellSize) × sqrt(sum of pump speed × power supplied)
flowRate  = LargeGridFlowRate or SmallGridFlowRate, m/s, by the grid the ring is on
pipe i reads parcel (i − round(parcels carried)) mod N
```

Carrying the fluid is a rotation of the ring's origin rather than a shuffle of its contents. Because
a pipe's index is a whole number, the rounded offset collapses to one integer shift shared by every
pipe, which makes the mapping a bijection at any speed: no parcel is read twice and none is skipped.
It is therefore exactly conservative and costs no substeps however fast the pump runs.

It has a resolution limit, which is not the same thing. A rotation advancing by a constant `k`
parcels per substep means pipe `i` only ever reads parcels in the subgroup `k` generates modulo `N`,
so whenever `gcd(k, N) > 1` the ring silently splits into that many disjoint sets and heat cannot
cross between them. Above one parcel per substep the fluid is therefore also mixed toward the ring's
mean by `1 − 1/parcels`: nothing at one parcel per substep, half at two, complete as the rate runs
away. That is the physically right limit — a ring lapping far faster than it is observed *is* well
mixed on that timescale — and it removes the aliasing outright, because mixing couples every parcel
to every other. It is also plug flow with no numerical diffusion: a hot parcel arrives at the
radiator still hot, smoothed only by the pipes it passed through.

**Flow goes as the square root of combined pump demand**, which is real parallel-pump behaviour
against a fixed circuit where turbulent pressure loss rises with the square of flow: four pumps
carry twice one pump's flow, not four times.

Demands are **signed** by which way each pump faces — a pump drives fluid out of its outlet port, so
whether that port opens onto the next pipe round the ring or the previous one sets the sign. They
subtract before the square root is taken. A pump fitted the wrong way round therefore drives the
ring backwards rather than failing, a backwards ring transports exactly as well, and two opposed
pumps cancel to a standstill while both go on drawing power.

**A pump's own draw is linear in its speed**, which is what stops pump count being a discount. The
affinity law — power with the cube of speed, which is what a real centrifugal pump does — is an
exploit here rather than a trade: a given flow from N pumps needs each at speed `K/N`, and cubed
power makes the bill fall as `1/N²`, so ten pumps idling cost a hundredth of one pump working.
Linear closes it exactly, because flow `F` needs `Σ speed = (F/base)²` and the bill is then
`maxPower × (F/base)²` — a function of the flow alone, with the pump count cancelled out. Doubling
flow costs four times the power however it is arranged, and a second pump buys redundancy and
headroom rather than a discount.

**A large-grid pump is rated at 50 kW, and the figure is derived rather than chosen.** The ring
moves 200 kg/s of coolant — four two-and-a-half-metre parcels a second at fifty kilograms each —
and pushing that against two bar of head at seventy per cent efficiency is `ṁ ΔP / (ρ η)` = 57 kW.
The small-grid block is a fifth. **All of it becomes heat**: a circulator does no work that leaves
the system, so its shaft power dissipates as friction in the coolant it is pushing and its motor
losses stay in the block, which is why its `ConsumerWasteEnergy` is 1 where a reactor's is 0.01.

Against that, the ring carries **680 kW for every kelvin** of difference around it, so a circulator
costs well under a per cent of what it moves where a heat pump pays a third. That contrast is why
both blocks exist. A pump the grid cannot feed circulates proportionally slower rather than
stopping.

With no pump running, `parcels/s` is zero and nothing is carried. The ring still holds its coolant
and still exchanges with what it touches, so the coolant beside a reactor saturates while the
coolant at the radiator stays cold — which is what a stopped pump does. `WellMixedCoolant` reverts
to the older single-mass fluid, where the whole ring is one temperature and heat crosses it
instantly whether anything is circulating or not.

Both exchanges are clamped and energy-conserving like every other. Each loop accumulates what it
drew out of blocks and what it pushed back into them over a step, reported as `LastWattsAbsorbed`
and `LastWattsRejected`. **They are kept apart rather than summed** because a loop in balance —
drawing off a reactor at one sink and shedding into a radiator at another — has a net of about zero
exactly when it is carrying its full load, so a single net figure would describe a working loop as
an idle one.

Rings are traced from the ports each block declares, so any block size or orientation works without
special cases, and each ring is found once whichever pipe the search starts from. A loop keeps its
heat across a rebuild through an order-independent hash of its members.

**When a ring stops being a ring, its coolant stays in the pipes that were holding it.** Grinding
out a pipe, or the pump — which is a ring member itself — leaves a chain rather than a ring, and the
builder traces rings only, so the loop has no successor and its parcels have nowhere to live. The
pipe each parcel was inside takes it: the node comes to the mixed temperature of the two,
`mixed = (T_n·M_n + T_s·M_s) / (M_n + M_s)`, **and its heat capacity becomes `M_n + M_s`**, because
the fluid is still in the block. That mass is *held coolant* — carried on the node, saved with the
grid, and handed straight back the moment a ring runs through that pipe again, which is exact
because a mixture is at one temperature and splitting it at that temperature conserves energy
term by term.

Both halves are needed and neither alone works. Taking the mixed temperature without the mass
destroys `M_s / (M_n + M_s)` of the ring's heat, which is the accident this replaced. Taking the
mass without the temperature is the same energy at the wrong place. Pouring the parcel's energy into
the node at its own capacity would conserve it and destroy the pipe: energy conserved by breaking
boundedness, which is not a trade this solver makes anywhere else.

**Both of those get worse as the fluid gets heavier, and `C43` made it ten times heavier.** A
large-grid pipe node holds 941 J/K on the solver's clock; its parcel held 1,889 at the flat 50 kg
charge and holds **19,479** at the density that ships. So the temperature-only spill destroyed
67.9 % of a ring's heat then and would destroy **95.4 %** now, and the unbounded pour put a 900 K
parcel into that pipe at 2,106 K then and at **12,854 K** now. The two figures this page carried
were measured before the correction and are kept beside the current ones rather than replaced,
because the ratio moving by an order of magnitude is the point: **a bound is worth what the thing
it bounds is worth, and that is not a constant.**

**What this predicts, written before it was run.** On the eight-pipe large-grid ring
`HeatLaunderingTests` builds, with every parcel at 900 K and every pipe at ambient, popping one pipe
and rewelding it must lose **one eighth** of the ring's heat above ambient — the parcel that left
with the block — against the 67.9 % the mixed-temperature-only spill lost at the coolant mass of the
day, and the loss must be
within a per cent of `1/N` for any ring length `N`. Breaking a ring into two rings, where no block
leaves, must lose **nothing**. No pipe may end hotter than the parcel it absorbed. These are the
falsifiers: a figure that is not `1/N`, a split that loses heat, or a pipe above its parcel.

**It ran. Three of the four hold and the fourth could not be built.** The loss is `1/N` at ring
lengths 8, 10 and 14 — 12.50 %, 10.00 % and 7.14 %, each of them 1,146,272 J to the joule, which is
one parcel. The reweld returns the broken ring's heat exactly: 14,901,537 J before it and
14,901,539 J after.

> **The bound's own figure was read off the grind and belongs to a path a grind no longer takes.**
> It was 698.19 K against a 900 K parcel, measured before `B44` made a broken ring vent and before
> `C43` gave a large-grid pipe ten times the fluid. On the constructor that reaches the spill
> today — switching the mechanism off, below — it is **872.0 K against 900 K**, and the form that
> is not bounded would reach **12,854 K**. `TheBoundHoldsWhenTheMechanismIsTurnedOff` computes the
> second rather than pinning it, so both move when the capacities do.

**The split falsifier is not constructible and is withdrawn rather than quietly dropped.** Breaking
a ring into two rings needs a pipe with three ports, and the shipped pipes have two — straight and
corner — so no block a player can add makes the builder trace one ring as two. What stands in its
place tests the same claim harder: grinding one pipe out and welding it back, repeatedly. The first
grind costs one parcel, **the second grind of the same pipe costs nothing at all** because the
reweld handed that pipe an ambient parcel, and the next hot pipe costs one parcel again. Draining a
ring is therefore a walk round it at one parcel a pipe, which is the price grinding any block pays.
The figures are in `HeatLaunderingTests`, which measures them rather than pinning the old fraction.

A pipe destroyed with the ring takes no share and holds nothing, which is right — that coolant left
with the block, and it is the one loss on this path that is a decision rather than an artefact.


Radiators are ordinary blocks with high emissivity and a surface-area multiplier. A loop dumps heat
into space by pressing a sink face against one: the panel takes the loop's heat by conduction and
sheds it by radiation from its exposed faces.


#### Coolant is a consumable: level, energy and fill rate

[backlog.md](backlog.md) `B43` decided the currency — **joules and seconds, per pipe, with nothing
to haul** — and `B44` fixed the constraints. This is the design that follows from both, written
before it is built (`E1`). Three parts, and only the third is anybody's opinion.

**1. A loop has a coolant level, and the level costs the integrator nothing.** `FillFraction` runs
0 to 1 per ring. A part-full ring holds proportionally less coolant **and couples proportionally
less**, because both the parcel capacity and every link's conductance scale with it:

```
SegmentThermalMass = fill × SpecificHeat × MassPerPipe(cell) / HeatTimeScale
G_pipe, G_plate    = fill × (as above)
```

where `MassPerPipe(cell)` is `CoolantKilogramsPerCubicMetre × cell³` — a density times the volume of
the cell the pipe occupies, so a 2.5 m pipe carries 515.6 kg and a 0.5 m one 4.1 kg — or a flat
`CoolantMassPerPipe` where a definition or a world states one. It was a flat 50 kg at both sizes
until `C43`, which is a gas in a large cell and outweighs the pipe block in a small one.

**The point of scaling both is that their ratio is what the integrator sizes a substep from**, and
the ratio is therefore invariant in fill:

```
demand = SegmentConductance / SegmentThermalMass          — unchanged at any level
```

That is `B44`'s stiffness cliff removed rather than avoided. Scaling capacity alone would make a
5 %-full loop **twenty times stiffer** on an element that already competes to be a grid's worst,
against a cap of 64. It is also the physical answer: half the fluid touching a wall carries half the
heat through it. **A dry ring is still a ring** — it exists, holds nothing, transports nothing, and
can be refilled, which is the state `B44` requires and the shape this area has failed in twice.

**2. Refilling costs energy, and the amount is derived rather than chosen.** The pump draws it, and
a pump's `ConsumerWasteEnergy` is **1** — *a circulator does no work that leaves the system* — so
every joule spent refilling lands back in the ship as heat. That is what makes the exchange rate
self-limiting without a single authored threshold, and it is why `B43` chose this currency.

The energy to restore one kilogram is the heat that kilogram holds at a stated excess:

```
J/kg = SpecificHeat × LoopRefillEquivalentKelvin / HeatTimeScale
```

At the shipped 3,400 J/(kg·K), 100 K and 90, that is **3,778 J/kg** — so a large-grid pipe's 515.6 kg
parcel costs **1,947,916 J** to restore. **That is exactly what venting it removes at 100 K above
ambient**, measured on both sides of the same arithmetic by
`VentingAndRefillingAtTheBreakEvenExcessIsNeutralInHeat`. So a vent-and-refill cycle at 100 K over is
**exactly neutral in heat** and loses on power and time, which is the exploit benefit erased by
construction rather than by a number picked to be large enough.

> The figure was **188,889 J** before `C43`, when a pipe carried a flat 50 kg. The neutrality does
> not depend on it: both sides are the same fluid at the same excess, so the identity holds at any
> charge and only the size of the number moves.

Above that excess venting still pays and below it costs, which is the behaviour to want: **dumping
coolant is worth doing when the coolant is genuinely hot and worthless as a pump**. 100 K is not
chosen here either — it is where `ThermalGlow` starts, the same excess this repository already
treats as the point a block is worth telling the player about.

**3. A fill rate, and this one is a choice.** `LoopRefillKilogramsPerSecond` bounds how fast a ring
comes back, so the cycle cannot be run faster than the fill however many grinders are on the ship.
At the shipped rate the pump draws `J/kg × kg/s` while filling and nothing when full.

**What drives it.** A ring short of full advertises `RefillDemandWatts`, the pump adds it to what
it asks the grid for, and the fill advances with the *step* rather than with the frame — coolant is
a simulated quantity and a frame is not simulated time. **No *running* pump, no refill**: something
has to drive the fluid in, so a pumpless ring holds coolant and fills none, and neither does a ring
whose pumps are switched off or turned down to zero. **No power, no refill either**: a pump the grid
could not supply fills by the share it was given, which is the rule every other draw in this mod
follows. Measured on an empty eight-pipe large-grid ring, the pump draws **18,889 W** while filling
and nothing when full — 38 % of a large-grid pump's own 50 kW rating, and nearly twice a small-grid
pump's 10 kW.

> **The pump did not actually add it to what it asks the grid for, until 2026-08-26.** The refill's
> watts went onto the block's *drawn* power, which is what turns them into heat, and were never put
> into the resource sink's request — so `PowerAvailable`, which is supplied over requested, was
> computed against circulation alone and a ship with nothing to spare refilled anyway. **The hole
> was widest where it mattered least to notice**: a pump switched *off* asks for nothing, a sink
> asked for nothing reports full supply, and the ring refilled at full rate for free. Both halves
> are closed — `HasDrivingPump` on the loop, the refill inside `DemandMegawatts` and inside the
> sink's ceiling on the block — and this paragraph described the first of them for as long as the
> feature has existed. See backlog.md `B44`.

> **This paragraph described the intent and not the code, for as long as the feature has existed.**
> The refill was called from the frame-paced update alone. That path is the game — but it is not the
> lane any figure on [balance.md](balance.md) is read in: every lab, benchmark and scenario advances
> through `StepExact`, and on that path a vented ring stayed empty for ever and the pump was never
> charged. So the consumable worked in a session and did not exist in a measurement, which is the
> worse half of the two, because the exploit `B43` was written to close was still open in every
> number the mod is tuned against. Both paths call it now, and
> `AVentedRingRefillsAcrossSteppedTimeAndThePumpPaysForIt` asserts the stepped one directly.
> Found by `LoopDialReachTests`, which reported both refill dials as reaching nothing at all.

#### A pump makes the ring conduct, not only circulate

Fluid-to-wall transfer is convective, so it depends on the flow: a pumped ring is forced convection
and a stopped one is natural convection against the same wall, which is most of an order of
magnitude in any handbook. Nothing in the model expressed that. `HeatTransferCoefficient` applied
whole whether or not anything was moving, so a pump earned its power by evening the ring out and
never by making the ring conduct — and a ship whose pumps had all stopped lost its circulation and
kept its coupling.

`LoopStagnantTransferFraction` is the share that survives with nothing circulating, and it now
scales every link the fluid has:

```
G_link = fill × StagnantTransferFraction^(flow == 0) × h · A
```

> **The field is older than this use and multiplied nothing before it.** It was authored for the
> segment-to-segment transport, which `Advect` already stops dead by returning on zero flow, so as
> written it could only ever be a no-op — a slider a player could move that did nothing, parsed from
> XML, carried through settings, clamped, and read by no line of the simulation. `LoopDialReachTests`
> enumerates the definition by reflection rather than reciting a list, which is what found it and
> what will find the next one. Its name, its range and what the menu promises are unchanged.

**It ships at 1**, so no ring behaves differently today. What it buys is a lever on the one leg the
measurements say is binding — see [balance.md](balance.md), *The joint, not the panel* — and a
reason for a pump to be switched on beyond mixing.

**What triggers it.** A ring that dissolves **having lost a pipe** vents: a grinder opened a hole in
a pressurised loop and the fluid left through it, taking its heat rather than spilling into the
pipes. A ring that dissolves with all its pipes still on the grid does not — no fluid can have
escaped, which is why `A12`'s spill is still the right answer there. **That is the coolant mechanism
being switched off rather than a split**: a split needs a three-port pipe and there is none. The ring's
signature carries the empty state across the rebuild, so welding the pipe back returns the ring to
the signature it had **and to a fill of nothing**, which it then pays to restore.

> **Measured end to end**: an eight-pipe ring at 100 K over holds 15,583,328 J, a grind drains all of
> it, and refilling spends 15,583,425 J — **a ratio of 1.0000**. Before the vent a grind cost one
> parcel, which is 1,947,916 J at the charge that ships and was 188,889 J at the flat 50 kg `B43`
> priced it on. The exploit is not small now; it is nothing.
>
> **And `A12`'s bound lost its test to this, and has it back.** A spilled parcel must never heat
> its pipe past itself, and the only dissolve that still spills is one that loses no pipe — which
> is not a split, because the shipped pipes have two ports and no block a player can add opens a
> closed ring. **It is the mechanism being switched off.** `EnableCoolantLoops = false` dissolves
> every loop with every pipe still on the grid and nowhere for fluid to have gone, which is a thing
> an admin does to a live world; the eight-pipe ring spills 14,025,000 J/K into its pipes and the
> hottest reaches 872.0 K against 900 K of fluid. [backlog.md](backlog.md) `F28`, closed.

**Venting is instant and refilling is not, and that asymmetry is the mechanic.** An emergency dump
buys relief now and is paid back gradually while the radiators work — useful once, useless on a
timer. It is the only part of this with no derivation under it, so it is a setting and it says so.


### Heat pumps

Every other mechanism here moves heat down a gradient. A heat pump moves it up one, and pays an
energy price to do so ([HeatPump.cs](../Thermodynamics/Core/Devices/HeatPump.cs)).

```
COP   = min(MaxCoefficient, CarnotFraction × T_cold / (T_hot − T_cold))
lift  = min(RatedWatts, COP × PowerWatts, (T_cold − T_min) × mass_cold / h)
work  = lift / COP
watts_cold = −lift
watts_hot  = +lift + work
```

The hot side gains the lift **and** the work, which is why a pump concentrates a ship's heat rather
than reducing it. The work is electricity, so this is the one place energy enters the grid from
outside it; a host that bills for `LastDemandWatts` is charging for exactly the energy that
appeared.

Three limits bind in turn, and Carnot is the interesting one. As the cold side falls,
`T_cold / (T_hot − T_cold)` falls with it, so each further kelvin costs more power than the last.
The block runs out of electricity long before its cold side runs out of temperature — which is why
nothing clamps the cold side at a floor and nothing needs to. The remaining limit,
`(T_cold − T_min) × mass / h`, is not a balance decision but a substep guard: it stops a large
enough rating taking more heat out of a node in one substep than the node contains.

Reported figures are per step, not per substep: energy is accumulated across the substeps and
divided by the step length at the end.

### Critical temperature and thresholds

```
if T > CriticalTemperature:
    damage = (T − CriticalTemperature) × OverheatDamagePerKelvin × h
```

With `DamageIsPerSecond` on, damage is per second of simulated time and independent of `Frequency`.
Damage is applied by the server only and is not replicated: every machine derives it from its own
simulation.

Any other temperature can be watched through
[ThermalThresholds.cs](../Thermodynamics/Core/Simulation/ThermalThresholds.cs). A
crossing is measured against the temperature at the top of the step, so a block that crosses and
recrosses within one step reports once, in the direction it ended up going. Thresholds are half open
— a block resting exactly on one cannot report twice. With none registered, the check is a single
integer comparison per step. See [api.md](api.md#thresholds).

---

## Surfaces, rooms and air

What counts as an exposed surface decides what radiates, what convects and what a room holds. Every
`A_exposed` above is this section's output.

### Surface bits

One integer per grid cell, four face-indexed groups of six
([CellSurface.cs](../Thermodynamics/Core/Model/CellSurface.cs)):

```
bits  0-5   self airtight       this cell's face seals
bits  6-11  neighbour airtight  the adjacent cell's facing side seals
bits 12-17  self mount          this cell's face carries a mount surface
bits 18-23  neighbour mount     the adjacent cell's facing side carries one
```

Faces are indexed in one canonical order — Forward, Left, Up, Down, Right, Backward — chosen so that
`face + opposite == 5`.

The self half comes from the block. The neighbour half is **always derived** by
[SurfaceMap.cs](../Thermodynamics/Core/Surfaces/SurfaceMap.cs) from the adjacent cell's
self bits, never authored, so the two halves cannot drift apart.

A block type's bits are built once per definition by `BlockSurfaceBuilder` from two things every
host can describe: which faces seal (the definition's pressurisation table) and where the mount
rectangles are. `BlockInstance` rotates them into grid space when a block is placed.

### Two layers

`SurfaceMap` answers about every cell twice, from one entry: the live state in the low half of a
`long` and the structural one in the high half.

| Layer | Doors read as | Asked by |
| --- | --- | --- |
| Live | whatever they are doing | exposure — an open doorway does radiate |
| Structural | shut | the room mapper — a door swinging must not change the shape of the ship |

Both are written from the same block in the same call, so they stay in step — and since 2026-08-26
in the same *entry*, which is what makes them impossible to write independently
([performance.md](performance.md#iteration-5--the-surface-maps-two-layers-in-one-dictionary)).
**This split is what makes a door cheap:** rooms are a property of how the ship is *built*, so
cycling a door does not invalidate them.

### Exposure

`GetExposedFaces` counts, per face direction, how many of a block's cell faces are open to the
outside. A face counts when it is on the block's boundary, nothing on the far side seals against it,
and the cell beyond is external.

```
A_exposed = exposedFaces × gridSize² × ExposedSurfaceMultiplier
```

That area is what radiation, convection, solar gain, point sources and friction all multiply.

**A mount joint is deliberately not one of the tests.** Any joint against a block that seals is
already rejected by the second rule, so the only joints a mount test could reach are those against a
block that does *not* seal — a grating, a catwalk, a ladder. Air floods through those, which is why
the room map calls the space beyond them external, and a hull panel under a catwalk goes on
radiating and taking sunlight. The joint conducts as well; both are true at once. The audit counts
those faces as `bolted`, a subset of the exposed ones, so the population can be measured on a real
ship.

Only the block's six boundary slabs are walked, never its interior: the cost is a block's surface,
not its volume.

### The room map

[RoomMapper.cs](../Thermodynamics/Core/Surfaces/RoomMapper.cs) classifies every cell in
the grid's padded bounding box as external space, solid structure, or part of an enclosed room, in
two phases:

1. **External** — flood fill from a corner of the padded box, which is guaranteed to be outside the
   grid, crossing any face that does not seal structurally.
2. **Interior** — scan for unvisited cells; each one seeds a room, or is recorded as solid when it
   seals on all six faces.

The pass is **resumable**. `Step(cellBudget)` does a bounded amount of work and returns, so a large
grid spreads its mapping over frames. The budget scales with the grid's volume — `volume / 60`,
clamped to 64…4096 cells — so a big grid maps in roughly constant wall-clock time. Restart requests
coalesce, so welding a thousand blocks in a second costs one pass rather than a thousand.

Maps are double buffered: the mapper builds a fresh `RoomMap` and swaps it in only when the pass
completes, so readers never see a half-filled one. Before the first pass the published map treats
everything as external — which **fails safe**, because a block that radiates when it should not is
visible, and one that cooks silently is not.

Door cells are never classified as solid, even when a shut airtight door seals on all six faces. A
door is a volume that can open, and a portal needs a region on the door's own side to join to.

### Portals and venting

Every face of every door that opens is recorded once, at map time, as a
[RoomPortal.cs](../Thermodynamics/Core/Surfaces/RoomPortal.cs): the door, the face, and
the region either side of it. Whether it is currently open is read live from the door.

`RoomMap.RefreshVenting` resolves the portals into which rooms currently reach open air: union-find
over the rooms plus one node standing for open air, merging the two regions of every open portal.
Any room that ends up in open air's set is vented, and `IsExternal` then answers true for its cells —
so what faces it faces outdoors.

**The cost is the number of doors, not the number of cells.** On a forty-thousand block ship with
thirty doors, cycling an airlock costs a walk over thirty portals and an exposure refresh of the
blocks facing the rooms that changed, rather than a flood fill of the bounding box and a pass over
every node. Portals are found by walking the grid's doors, which `GridModel` keeps in their own list
for exactly this reason.

A door welded on since the last pass has no portal yet, so the map cannot answer for it and a full
remap is requested instead.

### Room air

Each sealed, unvented room can hold an air mass
([RoomAir.cs](../Thermodynamics/Core/Surfaces/RoomAir.cs)):

```
Volume   = cells × gridSize³
AirMass  = Volume × RoomAirDensity × Pressure
Capacity = AirMass × 1005 / HeatTimeScale          J/K
```

The air links to every block bounding the room, with conductance
`RoomConvectionCoefficient × faces × cellFaceArea`. Links are built by walking the room's own cells
and looking at their six neighbours, so the cost is the size of the room rather than the size of the
ship.

`Pressure` starts at zero and stays there until the host reports otherwise — the simulation has no
way to know whether a compartment is pressurised, and a room at zero pressure has no mass, no links
and no cost.

**Pressure is the game's answer, not this model's**, and
[RoomPressure.cs](../Thermodynamics/Core/Surfaces/RoomPressure.cs) is the rule for
reading it. Three things can empty a room and each can veto air on its own:

| Source | Question | Answer when it says no |
| --- | --- | --- |
| `SessionSettings.EnableOxygen` and `EnableOxygenPressurization` | does this world pressurise at all | no room anywhere holds air |
| `IMyCubeGrid.IsRoomAtPositionAirtight` | does the game call this room sealed | that room holds nothing |
| `IMyAirVent.GetOxygenLevel`, or `Depressurize` | how full is it | that much, or nothing |

**None of them can insist on air, only refuse it**: the game owns pressurisation (`C9`) and this
model has no standing to overrule it, so an answer that says *no air* is taken. **An absence of an
answer is not one of them**, and was read as one until 2026-08-24 — see below.

**The reason once given for that was that the two mistakes are unequal, and measurement says they
are unequal the other way round.** Air was described as heat capacity — a room wrongly given it
dragging its walls along, a room wrongly denied it losing only a little inertia. It is not mainly
capacity. A link's conductance is `RoomConvectionCoefficient × faces × cellFaceArea` and carries no
pressure term at all, so pressure decides whether a compartment's walls are coupled and, above zero,
nothing else. Measured on a 2,000-block census hull settled under load in vacuum: the hottest block
runs at **1,502.83 K** at every pressure from 0.2 to 1.0 — identical to two decimals across a
fivefold change in air mass — and at **1,706.08 K** with the air gone, while the hull *mean* moves
3.8 K. Room air is a **mixer, not a sink**: it barely changes the hull's energy balance and moves
its hot spot by 203 K, which is the number overheat damage is taken off.

**So the two errors are the same size and differ in sign.** A room wrongly denied air is computed
203 K too hot, and a room wrongly given it 203 K too cool. What is not symmetric is what each costs
a player: too hot destroys a block that should have survived, and too cool fails to threaten one
that should have been. The chain can only ever deny, so its bias is toward the first.

**The chain stays and the fallback moved** (`C22`, 2026-08-24). It stays because `C9` and not the
error cost is what justifies it — every veto is the game or the world *answering*, and there is
nothing for a requirement to be built out of. What did not survive is treating **silence** as a
fourth veto: a compartment the game calls airtight, on a world where pressurisation is on, that no
lookup found a level for is a lookup that missed rather than an answer of empty — and a miss is
exactly what two models with different room shapes produce. `RoomPressure.Level` returns
`AssumedWhenUnanswered` there. Any positive value would behave identically, since the measurement
above says the level does not matter above zero; what the constant decides is whether the room mixes
at all. `RoomPressureTests` pins that silence does not defeat a veto, `RoomAirCouplingTests` the
pressure-independence and `ClientInputTests` the discontinuity a client's disagreement about
pressure produces.

The game's sealing test is consulted rather than this model's own room map because the two disagree,
and the game is right: it knows the real shape of a sloped or half block where the room mapper knows
only whether a cell's faces seal. The room map still decides the *geometry* — which cells are one
room, and which surfaces face indoors — since that is what exposure needs, and it is unaffected by
whether the world models oxygen.

How full a room is comes from the game's own gas system, **per room**:

```csharp
IMyOxygenRoom room = grid.GasSystem.GetOxygenRoomForCubeGridPosition(ref cell);
float level = room.OxygenLevel(grid.GridSize);
```

Not from the air vents. A vent can only speak for the room it stands in, and **the game's rooms are
the whole connected volume where this model's are pieces of it**, because the game's sealing test is
finer than a cell and splits nothing where this splits often. Giving air only to the pieces a vent
physically touched left every other piece of the same compartment in vacuum — measured on one ship
as twelve mapped rooms, of which the game held air in nine, and only two with a vent against them:
seven compartments in hard vacuum with the doors open onto a pressurised cabin. Reading it per room
also makes the vent's own position irrelevant, which is the correct model: a cabin with no vent of
its own, joined through a doorway to one that has, is full.

The vents remain as a fallback for a world whose gas system cannot be read, and only run when
something goes unanswered. There the old limit still applies — a sealed compartment nobody ever
piped air into is indistinguishable from one nobody can measure.

Continuity across rebuilds is by anchor: a room is identified by its lexicographically lowest cell,
which is stable while the room's shape is, so building elsewhere on the ship does not cost the
compartment its heat. Air appearing in a room for the first time starts at the average temperature
of the surfaces around it — it has been sitting in there with them — rather than at a placeholder
that would make a new compartment a heat sink.

Opening a door vents the room, which removes its air in the same frame; shutting the door gives it
back, at the temperature of the walls.

### Diagnostics

`RoomAudit` checks a published map against the grid and reports disagreements: cells classified as
external that are enclosed, rooms that should have merged, and so on. Nothing in the simulation
reads it, and it is never called unless something is asking.

`DebugTextOnScreen` reports, for the cell under the crosshair, its classification, its six
neighbours' classifications, whether each face between them seals, and the raw surface bits. A hull
block that reads "external" on an inside face is the leak.

**Both of those check this model against itself**, and the failure mode that matters is one neither
can see. This model decides sealing from each definition's pressurisation table, cell by cell; the
game decides it from its own test, which knows the real shape of a sloped block where this knows a
cell. When the two disagree the fill walks in from outside and a whole compartment stops existing —
no room, no air, nothing drawn in the room overlay, and no complaint anywhere, because
**pressurisation is only ever asked about rooms this model already found**.

[UnmappedRooms.cs](../Thermodynamics/Core/Surfaces/UnmappedRooms.cs) closes it by
asking the other model. Every cell the map calls external is offered to
`MyCubeGrid.IsRoomAtPositionAirtight`, and the cells it calls airtight are grouped into connected
regions — each one a compartment this model lost. Per region it reports the air vents standing in it
and whether they say `IsPressurized`, which is the identity a player can quote, and the block
subtypes across the faces this model leaves open, which is the list the fix is made from. A face
leaving a region that this model *does* seal is not reported: there the two agree and the region
simply ends.

**The test is oxygen, not airtightness.** Both `IsRoomAtPositionAirtight` and
`IMyAirVent.IsPressurized` mean *sealed*; neither means *full*. A cupboard nobody ever piped air
into, on a ship in vacuum, is airtight and empty and both models are right about it. The level comes
from the grid's own gas system, which answers for every compartment including the ones with no vent
to ask. The report counts these under **found, dry, air in game**.

The room view draws three states, because a room with no air and a room that was never found must
not look alike:

| State | Drawn as |
| --- | --- |
| Air in it | Solid, on the temperature ramp, edged in the room's own colour |
| Dry, and the game has no air in it either | Faint grey outline, the room's colour at low alpha |
| **Dry, and the game has air in it** | **Magenta, filled, heavy edge** |
| Not found at all, and the game calls it sealed | Red, filled |

Magenta and red are deliberately off the temperature ramp. The one thing a room the model failed to
fill must never look like is a cold room.

It is a diagnostic and drives nothing. Pressurisation still comes from the map, deliberately — the
fix belongs in the surface bits, and this is the measurement that says which blocks to fix and by
how much. See [telemetry.md](telemetry.md#room-dump) for the columns and the overlay.

---

## Environment inputs

The solver consumes four figures per grid per environment refresh — ambient temperature, air
density, the relative wind vector, and solar irradiance with its occluded share. How each is decided
from latitude, ground, altitude, hour, weather and depth is in
[environment.md](environment.md#what-the-solver-computes), which also carries the equations
`EnvironmentSolver` evaluates.

---

## Where this model differs from the one it replaced

The previous per-cell implementation is preserved verbatim in
[tests/Thermodynamics.Tests/LegacyFormulas.cs](../tests/Thermodynamics.Tests/LegacyFormulas.cs), and
several tests compare against it so the differences stay pinned rather than remembered.

| Then | Now |
| --- | --- |
| Two conductances per joint, disagreeing by block size | One symmetric series conductance |
| Contact counted in whole grid faces | Contact area from block bounds and mount coverage |
| Gauss–Seidel sweep with alternating direction to hide order dependence | Order-independent accumulation |
| Fixed step, unbounded exchange | Substepping from the stiffest node, plus an equilibrium clamp |
| Damage scaled with `Frequency` | Damage per second of simulated time |
| `SpecificHeat` a flat game number | Real J/(kg·K) with one global clock |

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | **The aerodynamic force is stripped, and the friction row is heat again** (`K25` step 0; [aerodynamics.md](aerodynamics.md#strip-and-rebuild)). The drag and lift derived from the friction watts, the pressure vector the solver summed for lift, the top-speed port and the Newtonian shape factor on the friction row are removed, and so is the solver's per-node normal buffer and its refresh budget: nothing in a step reads a normal now. The row is pinned bit for bit against the old code with the shape term off, and it raises friction heating back to the projected area — 1.71× on the four-cube brick, 2.10× on the wedge, and the median 4.56 K at re-entry that the shape term had taken off. The two sections that described the force, the shape term and lift in the present tense are rewritten to describe what remains; what they measured stays in the rows below. |
| 2026-09-09 | **The aerodynamic model is what the mod ships now**: `EnableDrag`, `EnableShapeDrag` and `EnableLift` all default on, with `DragCoefficient` at **1.54** rather than 0.5. The coefficient is not a separate decision — the shape term multiplies the projected area by a `sin²θ` whose population median is 0.3250, so the pairing that reproduces what 0.5 delivered without it is `0.5 / 0.325 ≈ 1.54`, and that is the pairing `K22` re-scored the drag milestone's criterion at (3.42 % and 78.2 m/s against 5 % and 60 m/s). **The heat side moves with it and downward**: the friction watts the shape term scales are what warm a hull, so atmospheric flight is a median 4.56 K cooler than it was. **One geometry reads badly and it was found by this change** — a bare plate one cell thick has no occupancy gradient along the flow, so the reconstruction charges it 0.111 where a flat plate square to the flow is the one case Newtonian and a projected area agree on exactly; the `reentry` scenario's 3×3×1 fixture is that shape and its nose rise fell from 35.2 K to 2.6 K. Solid hulls read 0.583 at 4³ and 0.843 at 12³, and hull skins are not the case, because the ship behind them breaks the symmetry ([backlog.md](backlog.md) `K24`). |
| 2026-08-31 | **Lift built as the transverse half of the pressure sum** ([backlog.md](backlog.md) `K23`). Newtonian pressure acts along `−n̂`; the solver sums that magnitude per node and `ShapeNormal.Factor` collapses it to a scalar, keeping only the axial part as drag. `LastPressureWatts` is the same sum with the normals left on, and `LiftForce` takes what is perpendicular to the flow. Added rather than re-derived, so drag does not move when lift is switched on. Over 8,137 hulls the median lift-to-drag is **0.057** and p95 **0.148**, and lift never exceeds a hull's own weight — a ship symmetric about its flight axis cancels most of the transverse sum, which is why a cube makes none. |
| 2026-08-31 | **Measured the shape term's radius instead of arguing it, and it corrects a claim this page had already published.** A 45° slope reads `sin²45°` **exactly at radius one, two and three alike**, and a 26.6° slope reads 0.134 against an ideal 0.200 at all three — so a wider read does not improve the accuracy it was wanted for. It buys separating slopes below about 27°, where it then overshoots. **The cost was stated as the cube of the radius and is not**: 2.07× at radius two and 3.28× at three, on 13,824 nodes with the result consumed. The first rig was four cells across, narrower than a radius-two neighbourhood, and read a lateral normal off its own edges on a ramp uniform in that axis — the rig is sixteen wide now and the test asserts that component is nought, which is what says the rest of the reading is about the slope. |
| 2026-08-31 | **The shape term's population reading: a median 4.56 K cooler at `reentry` over 500 ships, and the control did not move.** A paired walk on one clock — 496 of 500 cooler, none hotter, largest −15.40 K, and the effect shrinking with hull size from −5.27 K to −3.24 K. `vacuum-shadow` carries no air and moved on none of them, which is what says the factor reaches the friction row and nothing else. **The two-hull rig overstated it by about two**, because it was run at 300 m/s against the corpus's 200 and `v³` is 3.4 times smaller there. |
| 2026-08-31 | **Measured what the shape term does to temperatures, which is the half of the default question the population re-score could not answer.** At `reentry` air a settled brick runs **9.96 K cooler** with the term on and the stair-stepped wedge **11.36 K** — both above the 7.4 K windward shielding is worth, so this is firmly a change to the shipped answer rather than an addition. The sign is the opposite of shielding's because the factor is applied to the friction row alone and not to the convection factor the same six-face sum feeds; `ShapeDragTests` pins the sign as well as the size. |
| 2026-08-31 | **Every cruise figure this page published was taken at twice the drag the mod applies, and they are corrected in place** (`P1`, `P3`). `cruise.py` defaulted `--cd` to 1.0 while `DragCoefficient` has shipped 0.5 since `K5` moved it on 2026-08-30, and the documented invocation passes no `--cd` — so the altitude medians read **140.9 / 201.3 / 284.7 / 493.1 m/s** where the shipped configuration gives **199.2 / 284.7 / 402.6 / 697.3**, and 22.4 % of hulls drag-limited where it is 11.6 %. Re-taken on a fresh 8,137-ship census as [`summary-cruise-2026-08-31.csv`](../tools/corpus/summary-cruise-2026-08-31.csv). The tool now pins `SHIPPED_DRAG_COEFFICIENT` and `TheCruiseToolScoresTheCoefficientTheModShips` fails when it drifts from the setting — the guard that was missing, across a language boundary where `P5` could not reach. |
| 2026-08-31 | **The shape term's remaining half is a fork, not an addition, and the reason is the drag milestone's own simplification.** `DragForce.Newtons` is one division on the friction watts, so every newton of drag is assumed to leave a fixed share of its work in the surface. Base drag leaves none — its energy goes into the wake — so a wake term inside the friction row heats leeward faces for a drag that warms nothing, and one outside it cannot reach the force. A wake needs a second, force-only channel: two aerodynamic quantities where the milestone deliberately has one. Recorded before anyone builds it, because the obvious implementation is the wrong one and it fails quietly — as heat on the lee of every hull in the world. |
| 2026-08-31 | **Built the shape term as `EnableShapeDrag`, and it separates the pair that has stood as this model's counter-example since the drag milestone.** `ShapeNormal` reconstructs an effective normal per node from the occupancy around it and applies the Newtonian `sin²θ` a projected area is missing. The brick and the stair-stepped wedge read **172,800 W each** off and **100,800 W against 82,215 W** on — 0.816 where there was 1.000 — and the staircase cells reconstruct to `(0, 0.707, 0.707)` exactly. **It ships off**: the brick's own drag falls to 0.583 because a four-cell cube is nearly all edge, so the absolute calibration moves and `DragCoefficient` needs re-scoring on the population first. Bounded to 0..1 so it may only reduce, applied to the friction row and not to the convection factor, and rebuilt on the grid's version rather than on the wind — a normal is geometry, so it does not turn with the ship. Still open: a radius wider than one, and a wake term, without which a boat-tailed hull and a bluff-based one are still identical. |
| 2026-08-31 | **Wrote down why the shape term cannot be improved in place** ([backlog.md](backlog.md) `K22`). The section had said a projected area is not a shape and left it there; it now says why no further weighted sum over the same six faces recovers one — the leeward projection of a closed hull equals its windward one, and the streamwise depth is absent from the sum — and what the linear-versus-cubic incidence gap is against Newtonian impact theory. Also names what closing it would move: `FrictionScale` is `½ C_d η`, so the geometry term is a product with the heat dial, and the arithmetic half of `K8`'s lift refusal goes with it. |
| 2026-08-30 | **The drag and grid-speed milestones close, and the aerodynamics one all but.** `K1`: the friction term's energy is now taken out of the ship's motion — `DragForce` derives the newtons by one division from the watts the solver publishes, `ThermalGridDrag` applies them once per physical constraint group at its centre of mass, server-only and behind `EnableDrag`, which ships off. `K3` settled the coefficient as authored rather than derived, because a projected area is not a shape — a brick and a stair-stepped wedge of one frontal cross-section compute the same drag. `K5` scored it on the population against a criterion registered first and **moved `DragCoefficient` from 1 to 0.5**: at 1, drag at 100 m/s beats a ship's own thrust on 14.06 % of hulls that can lift themselves and the worst percentile holds 55.7 m/s, against a 5 % and 60 m/s criterion; at 0.5 it is 3.13 % and 78.8 m/s, which is also where a Newtonian flat-plate projection with no wake should land. `K10`: absorbing RelativeTopSpeed turned out not to mean porting its retarding force — a ship now stops where thrust balances drag, so top speed is an outcome with altitude in it (a median 140.9 m/s at sea level rising to 493.1 in very thin air) rather than a curve interpolated through authored mass points. `K13` and `K14` drew the boundary: this mod replaces the physics and not the ruleset, and the answer to two mods both slowing a ship is the switch rather than detection. `K6`: `K7` built windward shielding with a cadence its own measurement forced — 20° rather than the sun's 2°, and never restart a running pass — and `K9` measured what it costs the heat model, 7.4 K on a hull, which is why it ships off. `K8` refused lift on the principle that drag corrects something the mod computes wrongly while lift would assert something it does not compute at all. `K17` gave a block the means to say it is a different shape than its faces suggest, bounded so a profile may only reduce. **Three figures published during the work were wrong and are corrected in place**: the projected-area error that made every cruise speed low by two and every drag high by four, a `K5` criterion that measured thrust-to-weight rather than drag, and a test that pinned a default instead of the identity it existed for. |
| 2026-08-26 | The two surface layers are one packed entry per cell rather than two dictionaries. Nothing about the model changes — the same two answers, written in the same call — and it is here because this page is where the split is described. |
| 2026-08-26 | *Coolant is a consumable* restated at the charge that ships: a large-grid parcel costs **1,947,916 J** to restore rather than 188,889, and an eight-pipe ring holds **15,583,328 J** at 100 K over rather than 1,511,111. The neutrality is unchanged and cannot change — both sides are the same fluid at the same excess — which is now said, because the numbers moving without the conclusion moving is what makes the identity worth stating. |
| 2026-08-26 | *Coolant loops* carried a pipe parcel at 1,889 J/K, which is what it held at the flat 50 kg charge; `C43`'s density makes it **19,479**. The two figures that ratio decides move with it — the temperature-only spill destroyed 67.9 % of a ring's heat and would now destroy **95.4 %**, and the unbounded pour reached 2,106 K and now reaches **12,854 K**. Both are stated beside the old ones rather than replacing them, because a bound is worth what the thing it bounds is worth. |
| 2026-08-26 | **The refill's watts are inside what the pump asks the grid for**, which *Coolant is a consumable* had said since the feature existed and the code had never done ([backlog.md](backlog.md) `B44`). They were billed to the block's drawn power — which is what makes them heat — and never requested, so a ship with no power to spare refilled anyway; and a pump *switched off* asked for nothing at all, which a sink reports as full supply, so a ring whose pumps were off refilled at full rate and free. `HasDrivingPump` gates the loop side and the demand is inside `DemandMegawatts` and the sink's ceiling on the block side. |
| 2026-08-26 | **`A12`'s boundedness bound is under test again, and the case that reaches it is the coolant mechanism being switched off** ([backlog.md](backlog.md) `F28`). Not a split — the shipped pipes have two ports, so no block a player can add opens a closed ring, which is why the split falsifier was withdrawn and why looking for one again found nothing. The 698.19 K on this page was read off a grind before `B44` made a broken ring vent and before `C43` changed the coolant's capacity; on the constructor that works it is 872.0 K against 900 K of fluid, where the unbounded form reaches 12,854 K. |
| 2026-08-26 | A segment's thermal mass takes `MassPerPipe(cell)` — a density times the volume of the cell the pipe occupies — rather than a flat mass at both grid sizes. `C43`. |
| 2026-08-26 | Corrected *Coolant is a consumable*, which described the refill advancing with the step when the code advanced it with the frame alone — so the consumable worked in a session and was invisible to every lab, benchmark and test, which is the lane every figure on balance.md is read in. Added *A pump makes the ring conduct, not only circulate*: fluid-to-wall transfer is convective and so depends on the flow, and nothing expressed that until `LoopStagnantTransferFraction` was wired to the leg it names. Both found by `LoopDialReachTests`. |
| 2026-08-25 | **A broken ring keeps its coolant's heat, and the two thirds it used to destroy were an accident of two capacities** ([backlog.md](backlog.md) `A12`). The spill mixed each parcel into its pipe at `(T_n·M_n + T_s·M_s) / (M_n + M_s)` and then left the node at `M_n`, so `M_s / (M_n + M_s)` of the ring's heat — **67.9 %** on a large grid, 941 J/K of pipe against 1,889 of parcel — landed nowhere. The pipe now takes the parcel's heat capacity along with its temperature and hands both back when a ring re-forms through it, so grinding a pipe out of an eight-pipe ring costs **one eighth**, which is the parcel that left inside the block, and splitting a ring costs nothing. **Predicted before it was run** and the prediction stands at three ring lengths. Two further defects came out of the same code and are fixed with it: under `WellMixedCoolant` the spill handed *every* pipe the whole ring's fluid, and `SegmentTemperature`/`SetSegmentTemperature` bounded a **pipe** index by the **parcel** count, so in that model every pipe after the first read and wrote nothing. |
| 2026-08-25 | Said what the friction expression is: drag power, with `FrictionScale` standing in for `½ C_d`. The model computes what the air takes from a ship's energy and returns none of it to the ship's motion — a median 5.05 MW on the published population at `reentry`, which is 16.8 kN never applied. Whether it should be is [backlog.md](backlog.md) `K1`, and the coefficient is why it is not obvious. |
| 2026-08-25 | Stated the wind factor's floor as present-tense evidence rather than as what it *used to be* (`R12`), and absorbed the measured consequence — a 2 MW hull settling 0.9 K hotter in a 40 m/s wind — from the twelve-line comment in `ThermalSolver` that had been carrying it. The comment names this section now. |
| 2026-08-24 | **Silence stopped being a veto** (`C22`). `RoomPressure.Level` treated *nothing reported* and *reported empty* identically, though the parameter's own documentation said they were distinct. The three vetoes are each the game or the world answering; a compartment the game calls airtight, on a pressurised world, that no lookup found a level for is a lookup that missed — which is what two models with different room shapes produce — and it now takes `AssumedWhenUnanswered`. Also corrected the error-size claim above: the two mistakes are the *same* size, about 203 K, and differ in sign; what is asymmetric is that too hot destroys a block which should have survived. |
| 2026-08-24 | Corrected the reason given for the pressure veto chain. It was justified by air being heat capacity, so that denying air wrongly cost only a little inertia; measured, the link conductance carries no pressure term, so denying air removes the whole coupling and costs 203 K on the hottest block while every pressure above zero is identical to two decimals. The chain stays — `C9` justifies it — and whether the fallback should deny on uncertainty is now [backlog.md](backlog.md) `C22` ([backlog.md](backlog.md) `F21`). |
| 2026-08-22 | Wrote down what a coolant pump costs, now that it costs anything: 50 kW on a large grid, derived from the loop's own mass flow against two bar of head, all of it becoming heat because a circulator does no work that leaves the system ([backlog.md](backlog.md) `C13`). |
| 2026-08-22 | The convection wind factor runs from 1 upward rather than from 0.5 to 1. Forced convection adds to natural convection; the old floor made a wind under about 50 m/s a net warmer, because most of a closed hull's faces do not point into it ([backlog.md](backlog.md) `B29`). The two-to-one contrast between a windward face and a lee one is unchanged. |
| 2026-08-22 | Radiation in and radiation out are two coefficients. Emission keeps the emissivity; the sun and point sources read `SolarAbsorptivity`, which follows the emissivity unless authored, so the grey-body behaviour is the default rather than the only option ([backlog.md](backlog.md) `B27`). |
| 2026-08-22 | Corrected the reactor's shipped waste fraction, which this page and [tests/README.md](../tests/README.md) both quoted as 0.02 against the 0.01 in `Cubes.xml` and on [balance.md](balance.md#reactor-waste-heat). 0.02 is the value the sweep rejected: it puts a 300 MW reactor past critical *bare* in vacuum, which is a state no build can improve on. |
| 2026-08-22 | Corrected the substep cap quoted beside `LastStepWasClamped`: it read 16 and ships 64. |
| 2026-08-22 | Absorbed `surface-mapping.md`, whose subject is the geometry every area term on this page reads. Moved the environment equations to [environment.md](environment.md), leaving one home for them instead of two. Converted to present tense, with the defect narratives moved to [known-issues.md](known-issues.md) and this log. Promoted the three solver invariants into a table of their own. |
| 2026-08-20 | Made reactors generate the heat they always should have: which waste fraction applies is decided by the game's component, not by the definition, so a reactor's consumer fraction was dead text. Added the check over producer types by name. |
| 2026-08-19 | Took block conductivity in real W/(m·K) rather than a 0..1 quality figure. Reported the convection coefficient after the atmosphere blend rather than before it. |
| 2026-08-18 | Added room air as a node kind: one well-mixed mass per sealed room, coupling every surface bounding it — the only path between two walls that do not touch. Read pressure from the game's gas system per room rather than from air vents, which had left every compartment without a vent of its own in vacuum. |
| 2026-08-17 | Added self-shadowing per face rather than per block, and folded other grids' shadows into the same voxel walk. A projected-and-bucketed shadow map was measured against it first and is wrong in both directions at once: on a 76-cell structure at a real planetary sun angle it lit 38 cells where 28 are lit, ten of them behind something. |
| 2026-08-12 | Rebuilt the model per block rather than per cell: one symmetric series conductance per joint, contact area from block bounds and mount coverage, order-independent accumulation, substepping from the stiffest node, and real specific heat under one global clock. |
