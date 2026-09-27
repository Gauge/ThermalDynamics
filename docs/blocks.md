# Blocks and items

All block definitions live in [Data/CubeBlocks/](../Thermodynamics/Content/Data/CubeBlocks) with one file per block;
the extinguisher tool is in [Data/Extinguisher/](../Thermodynamics/Content/Data/Extinguisher). Display names and
descriptions are localisation keys resolved from
[Data/Localization/MyTexts.resx](../Thermodynamics/Content/Data/Localization/MyTexts.resx).

Both grid sizes are provided for every functional block, prefixed `Gauge_LG_` (large) and
`Gauge_SG_` (small).

| Looking for | Go to |
| --- | --- |
| Whether a block is worth building | [balance.md](balance.md) |
| The equations behind each block | [thermal-model.md](thermal-model.md) |
| The properties each block declares | [definitions.md](definitions.md) |
| The settings that scale them | [configuration.md](configuration.md) |

## Coolant pipes

| Subtype | Type | Size (LG / SG) | Sink faces |
| --- | --- | --- | --- |
| `Gauge_*_CoolantPipe_Straight` | `CubeBlock` | 1×1×1 | none |
| `Gauge_*_CoolantPipe_Straight_SingleSink` | `CubeBlock` | 1×1×1 | Right |
| `Gauge_*_CoolantPipe_Straight_DoubleSink` | `CubeBlock` | 1×1×1 | Left, Right |
| `Gauge_*_CoolantPipe_Corner` | `CubeBlock` | 1×1×1 | none |
| `Gauge_*_CoolantPipe_Corner_SingleSink` | `CubeBlock` | 1×1×1 | Up |
| `Gauge_*_CoolantPipe_Corner_DoubleSink` | `CubeBlock` | 1×1×1 | Backward, Right |
| `Gauge_LG_CoolantPump` | `UpgradeModule` | 1×1×1 | none |
| `Gauge_SG_CoolantPump` | `UpgradeModule` | 1×1×**3** | none |

**A pump draws power, and all of it becomes heat.** 50 kW on a large grid and 10 kW on a small one,
linear in the speed slider, through an ordinary resource sink — so a loop is not free to run and a
pump the grid cannot feed circulates proportionally slower rather than stopping. The figures are
derived in [thermal-model.md](thermal-model.md#coolant-loops); the short version is that it is well
under a per cent of what the ring carries, where a heat pump pays a third.

*Sink faces* are the sides that transfer heat between the coolant and the block pressed against
them. A pipe with no sinks is plumbing only.

> **Sink faces are deliberately few, and that is a design constraint rather than an oversight.** A
> pipe carries at most two, a pump carries none, and a plain pipe is plumbing. The loop is meant to
> be a thing you route and commit space to, not a coating you wrap a hot block in until it stops
> being a problem — so *add more sink faces* is not a balance lever, and the pickup is raised or
> lowered through the coolant's own coefficient instead. See balance.md, *What a jump drive costs in
> radiator*, where the difference decides whether a 6.4 MW block has an answer at all. Plumbing is declared per subtype in
[ThermalCoolantShapes.cs](../Thermodynamics/Game/ThermalCoolantShapes.cs) as a
`CoolantShape`: two link ports, any number of sink ports, and whether the block is a pump. **A new
pipe subtype must be added there.** Ports carry the cell they sit on as well as their direction, so
a block of any size or orientation works without a special case — the small-grid pump is three
cells long and needs no code of its own.

Connection directions (before block orientation is applied):

| Shape | Links |
| --- | --- |
| Straight (all variants) and pump | Forward ↔ Backward |
| Corner (all variants) | Forward ↔ Left |

The pipes and pump are grouped into the `CoolantGroupLarge` / `CoolantGroupSmall` block variant
groups ([BlockVarientGroups.sbc](../Thermodynamics/Content/Data/CubeBlocks/BlockVarientGroups.sbc)) so they cycle on
one toolbar slot.

## Coolant loop rules

[CoolantLoopBuilder](../Thermodynamics/Core/Loops/CoolantLoopBuilder.cs) re-finds
every loop on the grid whenever the block layout changes. A ring becomes a loop when **all** of
these hold:

1. Following link ports from block to block returns to the starting block — the run must be a
   **closed ring**. A dead end forms no loop.
2. The ring contains at least one block whose shape is marked as a pump.
3. Every block in the ring declares coolant link ports.

Rings are keyed by their member set, so each is found once whichever block the search starts from,
and the order pipes were built in does not matter. Breaking a ring removes its loop; closing it
again restores one, and the heat comes back with it — a loop is identified by an order-independent
hash of its members, so its temperature survives the rebuild.

Once formed, the loop is one lumped coolant mass shared by every segment: it draws heat from
the pipe blocks themselves and, through each sink face, from whatever block is mounted against
that face. See [thermal-model.md](thermal-model.md#coolant-loops) for the transfer equations.

Practical build advice:

* Run the loop *through* your heat sources with sink faces against reactors, thrusters and
  batteries, then out to radiators or a cold hull section.
* **Loop length increases both the transfer and the fluid**, and a longer ring cools strictly
  better. Each pipe gets its own full-strength link to the fluid, so coupling grows with the ring —
  measured with a single sink face on the same 500 kW block, 56,250 W/K at 8 pipes and 181,250 W/K
  at 28, taking that block from **722.7 K to 577.5 K**. Nothing divides by segment count.
  Pinned by `LongerRingsDeliverColderBlocks`.

  > **This advice used to say the fluid mass does *not* grow with the ring, and that both a 32-pipe
  > and an 8-pipe ring carry the same 500 kg.** That was true when the charge was per *loop*; it is
  > per *pipe* now, so a longer ring is a bigger buffer as well as a harder coupling — which is why
  > the reason given here is the coupling and not the mass. The coupling figures moved too, by the
  > 6.25× `C42` put on the pickup coefficient.
* A loop's temperature is saved and restored by member hash, so reloading cannot swap two loops'
  heat and rebuilding a ring does not reset it.
* **Spread your sources around the ring; do not bother splitting it.** Four sources bunched into one
  stretch of a 32-pipe ring settle at 111.3 C; the same four spread evenly around the same ring
  settle at 66.8 C. Dividing that ring into four separate rings with a pump each lands at 67.4 C — no better than
  spreading, for four times the pumps. What saturates a loop is several sources dumping into one short
  run of pipe, not the length of the ring. Measured by the `loop-layout` scenario.
* **A pump fitted the wrong way round is not broken — it drives the loop backwards**, and a loop
  driven backwards cools exactly as well. A pump pushes fluid out of its outlet port; whether that
  faces one way round the ring or the other is all that changes.
* **Two pumps facing each other cancel.** Their demands subtract before the square root, so a ring
  with three pumps one way and one the other circulates at the rate of two, and a ring with one each
  way does not circulate at all — while both pumps go on drawing their full power. If a loop has
  pumps and no flow, check that they agree.
* One large ring is also the more robust arrangement. Flow rises with the square root of combined
  pumping, so a ring with four pumps that loses one still circulates at 87 %; a ring with one pump that
  loses it stops circulating altogether and becomes a local heat buffer.

### When a ring does not become a loop

A broken ring's only symptom is that no loop appears, which is the one thing the loop list cannot
report. `ThermalSimulation.DiagnoseLoops` re-runs the search and names the reason for every coolant
block that ended up in no loop:

| Reason | What to look for |
| --- | --- |
| open end | A port faces empty space. The run has a free end. |
| a port faces a block with no plumbing | The run walks into armour or a conveyor. |
| pipes touch but their ports do not line up | The hardest one to see: the run looks continuous and carries nothing. Rotate one of the two blocks. |
| closed ring with no pump | Reported against every block in the ring, because the fix is to the ring. |
| a branch or crossing, not a ring | Three pipes meeting, or a figure of eight. A loop is a simple cycle. |
| the run doubles back on itself | The walk returned to its start through the port it left by. |

It is opt-in and costs one extra walk per unclaimed run, so an ordinary rebuild does not pay for a
readout nobody opened. Each reason carries up to four example cells, capped so a grid of broken
plumbing cannot flood a readout while the counts stay exact.

## Radiator

`Gauge_LG_Radiator` / `Gauge_SG_Radiator` — a plain `CubeBlock`, 1×5×2 (LG), mounting only on
Top and Bottom.

It has **no script behaviour**. It is purely a definition-driven heat shedder: aluminium's specific
heat (900 J/(kg K)), conductivity 1, high emissivity (0.35 against the 0.125 default) and a 1.25×
surface area scaler, so it radiates faster than any armour block of comparable mass. Against a slab
of light armour of the same shape on the same load it is about 26× better per tonne, which is what
earns it its place.

**Plumb it; do not bolt it.** A coolant sink face couples to the panel at about 6,250 W/K, and on
the same load plumbing a panel rather than bolting it is worth **73.5 K** — more than doubling its
area, and more than any surface property a definition would reach for first. What a loop buys is
*reach*: a joint carries heat one block, and a ring carries it wherever the ring goes.

> **The joint itself is no longer the weak end**, and that is a change. It carried about 167 W/K
> against the sink's 1,000 when this was written; a joint is solid conduction and the pace of that
> is four times what it was since `C24`, so it carries **1,168 W/K** — as hard as the fluid, and a
> little harder. **So a steel bolt out-couples a water-cooled plate, face for face, and that is
> kept** (`C25`, decided 2026-08-24). It is a statement about this world's conduction pace rather
> than about steel and water: solid conduction runs at 9.6× real materials because `G8`'s
> significance window was bought with it, while the loop's coupling is a heat transfer coefficient in
> W/(m²·K) — 1,000 while the pump runs and 160 while it does not, since `C42` — which is what
> the transfer physically is. Pacing the fluid with `ConductionScale` too would put a coefficient no
> fluid has into the model and give the game back the second conduction pace `C20` removed — and it
> was measured during `C12`: it recovers a coolant sink from 73.3 K to 108.2 K against the best
> surface dial's 135.3 K, so it pays for a plumbed hull's substep demand and still does not restore
> the ordering it was for. **The guidance holds because of reach rather than because of rate**, and
> that is now the whole of the claim: a joint carries heat one block, and a ring carries it wherever
> the ring goes.

A panel bolted straight onto a hot block is limited by what it can radiate rather than by what
reaches it, which is why the second one you bolt on is worth 3 K and the eighth is worth nothing. Past a certain load it goes further than useless: bolted to a block already saturated,
a panel makes it *hotter*, because it covers faces that were radiating and cannot carry off what it
blocked. Keep the panel's own faces exposed to vacuum either way. Measured in
[balance.md](balance.md).

## Heat pump

`Gauge_LG_HeatPump` (1×1×1) / `Gauge_SG_HeatPump` (3×3×1) — an `UpgradeModule`, so it has a
terminal and an on/off switch. It carries no upgrades; the type is there for the switch.

The only block that moves heat **against** a gradient. It draws heat out of whatever is bolted to
its front face and rejects it into whatever is behind it, and pays for the privilege in
electricity. Placement is the whole of its configuration: put its cold face on what you want cooled
and its hot face on a radiator, or on a coolant pipe's sink face.

| | Lifts up to | Draws up to |
| --- | --- | --- |
| Large grid | 60 kW | 20 kW |
| Small grid | 12 kW | 4 kW |

Three limits decide what it actually achieves each step, and which one binds is the block's whole
character:

* **Carnot.** Efficiency is `0.4 × Tcold / (Thot − Tcold)`, capped at 8. Lifting heat across a
  small gap is nearly free; across a large one it is ruinous.
* **Its rating.** Against a small gap it runs out of machine before it runs out of efficiency, and
  draws less than its maximum because it cannot use power it has no capacity to move.
* **What is there.** It cannot take more heat out of a block than the block has.

Two consequences worth knowing before building around it. The hot side gains **more** than the cold
side loses — the lift plus the work that lifted it — so a pump does not reduce a ship's heat, it
concentrates it somewhere you can radiate it away from. And nothing clamps its cold side at a floor:
the cost of a kelvin simply rises without limit as that side approaches absolute zero, so the
block's own electrical rating stops it long before the temperature does.

### As air conditioning

Yes, with one indirection. A heat pump binds to two **blocks**, and a room's air is not a block, so it
cannot draw from a compartment directly. Put its cold face on a block that *bounds* the compartment
and the wall goes cold; the air touching that wall gives up its heat to it, and the room follows. The
hot face goes outward, into a radiator.

Two consequences. It only works on a **pressurised** room — with no air there is nothing coupling the
compartment to its walls, and the pump is just chilling a piece of hull. And the rate is limited by
how much wall the room has in contact with the cooled block, not by the pump's rating: one wall of a
large cabin is a small window to pull heat through.

Measured by the `air-conditioning` scenario: a sealed cabin with a 15 kW source inside settles at
−59.6 C with the pump off and −109.1 C with it on.

The terminal shows what it is moving, what it is drawing, and the coefficient between them. A pump
with nothing bolted to one of its faces says so rather than silently doing nothing.

A pump pairs naturally with a radiator on its hot side: it concentrates a ship's heat somewhere that
can be shed, which is the one thing radiators alone cannot do when the block you need cooled is
already cooler than what surrounds it.

The efficiency fraction and the cap are tuned for the whole mod in
[configuration.md](configuration.md#heat-pumps); the two ratings are per block and live in
[ThermalHeatPumpShapes](../Thermodynamics/Game/ThermalHeatPumpShapes.cs).

Its waste-energy fractions in `Cubes.xml` are deliberately zero. The simulation already puts every
watt the block draws into the hot side; a waste fraction on top would charge the same energy twice.

## Debug heat source

`Gauge_LG_HeatSource` (1×1×1) / `Gauge_SG_HeatSource` (3×3×1) — an `UpgradeModule`, for the same
reason the heat pump is one: it needs a terminal and an on/off switch. It carries no upgrades.

**A block that radiates like a small sun**, so the point-source path can be seen in a session
rather than only in the harness. It registers itself through
[ThermalHeatSources](../Thermodynamics/Game/ThermalHeatSources.cs) — the same entry
point the mod API's `AddHeatSource` hands out and the same one `/thermal heat` drives — so a
session that shows this block working has shown that path working for every consumer of it. Its
output falls off as `P / 4πr²` with a hard cutoff at its range, exactly as a source registered by
another mod does; see [api.md](api.md#heat-sources).

| Terminal control | Range | Default |
| --- | --- | --- |
| **Output** | 0 W – 1 GW, six decades of travel | 5 MW |
| **Range** | 10 m – 1,000 m | 200 m |
| On/off | the block's own | on |

**Zero is a real setting on the output dial, and it is the one that costs nothing.** A registered
source is charged one pass over the exposed blocks of every grid in its range, every sampled step,
before anything looks at how many watts it carries — so a source turned down to nothing would go on
costing what a gigawatt costs. At zero the block takes its entry out of the registry instead, which
is `P8` applied to a dial rather than to a switch: turning it off removes its own cost. The terminal
writes `off` beside the slider there, not `0 W`.

That is also why the dial is not the game's own logarithmic slider, which needs a floor above zero.
The curve is `floor × (10^6ᵖ − 1)` over a 0–1 position — a logarithm everywhere it matters, finite
and exactly zero at the bottom. Six decades of travel, because a kilowatt is where the question is
whether a face is looking at the source at all and a gigawatt is a second sun; a linear dial over
the same span cannot be put on 5 MW, which is half a per cent of its travel.

| Position | 0 | 0.1 | 0.25 | 0.5 | 0.75 | 0.9 | 1 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Output | **off** | 3 kW | 31 kW | 1 MW | 32 MW | 251 MW | 1 GW |

Both dials are replicated, and unlike the coolant pump's speed and the heat pump's throttle they are
**saved with the world** — a debug rig is built once and then left, so a slider that reset on reload
would be a rig that had to be rebuilt every session.

> The output control is the one slider in this mod whose terminal *value* is not the quantity it is
> named after: it carries the 0–1 position, and the watts are what the panel writes beside it. A
> script reading `Gauge_HeatSourceLevel` gets the position, which is why it is named that.

Some scale for the output dial, since a power is not an intuition. What a surface actually receives
is the figure spread over the sphere at whatever distance it is read at:

| Output | at 32 m | at 200 m | at 1 km |
| --- | ---: | ---: | ---: |
| 1 MW | 78 W/m² | 2.0 W/m² | 0.08 W/m² |
| 1 GW | 78 kW/m² | 2.0 kW/m² | 80 W/m² |

The sun is 1,000 W/m² at the `SolarEnergy` default, so roughly a gigawatt at 280 m is a second sun.

> **It draws no power and takes nothing from the grid: its energy comes from nowhere, and that is
> deliberate.** Every other heat term in this mod is conserved against something — power drawn,
> power produced, thrust, a gradient a pump paid to climb. This block's job is to put a *known*
> number of watts into a world so that cooling can be measured against it, and a fixture that
> browned out when the reactors did would be measuring the reactors. That is why its name says
> debug, and why the block is the one thing on this page a survival balance argument does not
> apply to.

The terminal's detail panel says whether the dial is doing anything, which is the one thing echoing
the sliders back cannot say. A source set to 5 MW delivers nothing when the block is off, and
nothing when the world has `EnableHeatSources` off — and neither changes the number on the slider.
Both are named in the panel, along with the irradiance the setting puts at 50 m.

It shares the heat pump's model and a hue-shifted copy of its icon. The block exists to make a
mechanism visible and has no art of its own; a mesh for it would be a re-export of a family for a
fixture. See `ShippedIdentityTests.TheModelTreeKeepsItsShape` — a model added moves the pinned
digest, and this block deliberately adds none.

Where it fits against the other two ways of making one:

| | Saved with the world | Placed by | Moves with a ship |
| --- | --- | --- | --- |
| This block | yes | building it | yes |
| `/thermal heat <watts>` | no | a chat command, ahead of the camera | no |
| `AddHeatSource` | the caller's business | another mod | if bound to an entity |

## Extinguisher (hand tool)

A rifle-class hand item. Today it is a **thermal scanner**; the intent is that it also **cools a
block rapidly, using expendable ammunition, as damage mitigation** — the thing you reach for when a
block is about to go. **The two halves are priced**: undoing a crossing by hand is twenty-six
five-kilogram CO2 bottles at the median block and twenty at once to beat its window, which is a
large radiator with a trigger; but pulling that same block *ten kelvin back from its rating* is half
a bottle, because damage tracks the overshoot rather than the heat. See
[document-of-intent.md](document-of-intent.md#acting-on-heat-by-hand--damage-mitigation-and-priced) and
[balance.md](balance.md#what-a-hand-tool-would-have-to-be-worth).

| Definition | File |
| --- | --- |
| `PhysicalGunObject/ExtinguisherGunItem` | [PhysicalItems.sbc](../Thermodynamics/Content/Data/Extinguisher/PhysicalItems.sbc) |
| `WeaponDefinition/ExtinguisherGun` | [Weapons.sbc](../Thermodynamics/Content/Data/Extinguisher/Weapons.sbc) |
| `AutomaticRifle/ExtinguisherGun` (hand item) | [HandItems.sbc](../Thermodynamics/Content/Data/Extinguisher/HandItems.sbc) |
| `AmmoMagazine/BottleMag`, `AmmoDefinition/Bottle` | [AmmoMagazines.sbc](../Thermodynamics/Content/Data/Extinguisher/AmmoMagazines.sbc), [Ammos.sbc](../Thermodynamics/Content/Data/Extinguisher/Ammos.sbc) |
| `AudioDefinition/FireExtinguisher` | [Audio.sbc](../Thermodynamics/Content/Data/Extinguisher/Audio.sbc) |
| `CubeBlock/Extinguisher` (decorative wall block) | [Extinguisher.sbc](../Thermodynamics/Content/Data/Extinguisher/Extinguisher.sbc) |
| Character animation overrides | [AC_Astronaut.sbc](../Thermodynamics/Content/Data/Extinguisher/AC_Astronaut.sbc) |

The `Bottle` ammo does zero damage, has zero trajectory and zero impulse — firing only produces
the snow particle effect and the extinguisher sound.

The functional part is in `ThermalHud.DrawToolHud`
([ThermalHud.cs:57](../Thermodynamics/ThermalHud.cs#L57)): while the tool is
equipped, a 15 m camera raycast finds the block you are looking at, prints its temperature in
°C at the lower left, and draws heat-coloured billboards over that block and each of its
thermal neighbours using the `GaugeThermalTexture` transparent material
([TransparentMaterials.sbc](../Thermodynamics/Content/Data/TransparentMaterials.sbc)).

The proposed cooling integration now has an [installed-game API probe](../tools/thermal-api-check/README.md#extinguisher-projectile-boundary).
A hit interceptor can read projectile/ammo identity and target geometry, with a server-only gate;
its compiler/whitelist admission is recorded. Native static inspection places that callback before
the positive-damage branch, so the ammo can retain zero damage. This is not a working extinguisher:
the shipped trajectory is still zero, and cooling, crafting, discharge accounting and native-session
validation remain open. The next implementation must derive heat removal from server-observed
ammunition use, preserve missed-shot costs, and synchronize with any outstanding thermal solve.
No client-supplied target temperature or removed-energy value is an authority.

## The performance panel

`ThermalHud.DrawGridHud` draws a compact readout of what the simulation is doing across **every live
grid**, toggled with **ctrl+shift+P** and off by default:

```
grids     7          blocks    7,976
links     3,294      loops     4
substeps  6 / 21.4   starved   72%
visits/s  395,280    floored   122
ambient   -270 C     peak      634 C
critical  0          clock     90 / 4
```

It replaced a panel that appeared only while a player was seated in a block and showed five
temperature lines. That is the wrong shape for the question people actually have — whether the mod
is costing them frames, and why — and it could not be answered from a cockpit, because the grid
that is struggling is usually not the one being flown.

**Every figure is a count, not a clock.** Milliseconds depend on the machine and on what else is
running, so two players comparing notes would be comparing hardware. Substeps, link visits and
floored blocks are properties of what has been *built*, so they mean the same thing to everyone and
read directly against [load-and-hitching.md](load-and-hitching.md#in-the-field).

Two lines carry colour, and only when they mean trouble:

* **`starved`** — how much of the substep demand is being refused. This is the number that predicts
  failure: everything that has ever diverged in this mod was refused the substeps it asked for. Any
  figure above zero is worth acting on, by raising `MaxSubsteps`, raising `MaxSubstepsPerBlock`, or
  raising `Frequency` to cut the demand per step.
* **`critical`** — blocks past their rating right now.

`substeps` reads *granted / demanded* and `clock` reads `HeatTimeScale / Frequency`, whose ratio is
the safety rail described in [configuration.md](configuration.md). `floored` is how many blocks the
per-block cap is holding back — on a real ship that is usually the lightest fittings, and
[stiffness.md](stiffness.md) is about getting it down.

Both HUD elements are drawn by the Rich HUD Framework; without Rich HUD Master the framework never
registers and no text appears. The extinguisher's temperature billboard is drawn through the mod API
directly and does not care.

## The terminal readout

Selecting any simulated block in the terminal fills its **detail info** panel — the pane under the
block's name — and it says only what applies to *that* block. The pane is a few lines tall and shares
them with whatever else the block reports, so a line reading `Waste heat: 0.0 kW` on a block that
generates none is a line spent saying nothing. Every section is conditional.

A reactor:

```
Temp     584°C  +0.00 K/s
Critical 927°C
Waste    500.0 kW
```

A coolant pump, which adds its loop's flow rate and coolant temperatures:

```
Temp     169°C  +0.00 K/s
Critical 727°C

Coolant  133°C  (109 - 157)
Flow     -10.0m/s
Transfer 142.9 kW in  142.9 kW out
```

A heat pump reports how far its conditions are from letting it reach full output:

```
Pump     23.1 kW for 20.0 kW   x1.15
Optimal  -48°C
```

A debug heat source reports what it is registering, and — the part the sliders cannot say — whether
anything is receiving it:

```
Source   5.00 MW  to 200 m
At 50m   159.2 W/m²
```

Switched off it says `off`; at an output of zero it says `output at zero, not registered`, because
those are different fixes; and in a world with `EnableHeatSources` off it says so in words rather
than showing a zero, because that fix is a world setting and not anything reachable from the panel.

**`Optimal` is the gap this pump has left before it stops reaching its rating**, and it goes negative
once the gap is past that. `-48°C` means the two sides are forty-eight degrees further apart than they
need to be — bring either one that far toward the other and the pump reaches its rating. A positive
figure is headroom: how much further apart they could drift before output starts falling.

The pump saturates while `coefficient × power ≥ rating`, and the coefficient is
`fraction × Tcold / gap`, so the widest gap that still saturates it is
`fraction × Tcold × power / rating` — 40 K at a 300 K cold side on the shipped figures. Throttling the
pump narrows that allowance in proportion, and the line follows.

It replaced a sentence naming which limit was binding. That only named a state; this one can be acted
on, which is the difference between a readout and a diagnosis.

An idle pump carries one word for which of three reasons applies — `no block on one face`, `off` or
`unpowered` — because those want three different fixes and no figure separates them.

Coolant is given as a mean with the **range across the ring** in brackets, because with the fluid
carried round in parcels a loop is not one temperature. A wide spread with the pump running means the
flow cannot keep up with the load; a wide spread with no flow means nothing is circulating.

Flow is in **metres per second** — parcels per second is the solver's unit and nobody has any
intuition for it — and the **sign is the direction**, because a ring driven the other way is a working
ring. **A stopped ring reports `0m/s` and nothing else.** It has several causes, pumps switched off,
pumps unpowered, pumps fighting each other, and naming them would cost a line each to say what is
already legible from the figure and the pumps you built.

`Waste`, `Critical` and `Room` appear only when they apply.

The one thing still spelled out is a run of pipe that formed **no loop at all**, which reports the
reason from [the fault table above](#when-a-ring-does-not-become-a-loop). There is no figure to read
in that case: the absence is the whole symptom.

It goes there rather than into a terminal control because the terminal's controls are single-line
fields: a text box handed fifteen lines shows one and a half and clips the rest.
[ThermalTerminal.cs](../Thermodynamics/ThermalTerminal.cs) needs no HUD framework at
all, so it is the readout that works in any world.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Recorded the admitted native projectile boundary for future extinguisher cooling; scanner-only behavior and remaining accounting/session work stay explicit. |
| 2026-09-08 | **Added the debug heat source** ([backlog.md](backlog.md) `B11`), which is a block with two sliders that registers through `ThermalHeatSources` and nothing else — so what a session sees it do is what every consumer of that path gets. Its dials are saved with the world, which the coolant pump's speed and the heat pump's throttle are not. Its output dial runs from zero to a gigawatt over its own six-decade curve rather than the game's logarithmic slider, which cannot have a floor of zero — and zero **removes the registration** rather than emptying it, so a source turned down costs nothing rather than costing what a gigawatt costs. It draws no power by decision, and says so in three places, because a heat term that is not conserved against anything is the sort of thing that reads as a defect to whoever finds it next. |
| 2026-08-26 | **The build advice on ring length was written against a model that has changed twice**, and every number in it was wrong. It said the fluid mass does not grow with the ring and that a 32-pipe and an 8-pipe ring carry the same 500 kg — true when the charge was per loop and false since it became per pipe — and it quoted 32,000 W/K against 8,000, which predates `C42`'s 6.25× on the pickup. It also cited `LongerRingsCoupleHarderAndCarryTheSameFluid`, a test that no longer exists. Re-measured: 56,250 W/K at 8 pipes and 181,250 at 28, taking a 500 kW block from **722.7 K to 577.5 K**, and the reason is the coupling rather than the mass. Pinned by `LongerRingsDeliverColderBlocks`, which is what actually asserts it. |
| 2026-08-24 | **Re-quoted every measured figure on this page at `C24`'s pair**, which moved the ones a bolt joint is in. A radiator bolted to a source is worth 228.6 K where it was 42.9 K and 26× armour per tonne where it was 48×, because solid conduction runs four times faster: the stack now keeps paying to the eighth panel instead of saturating at the second. *Plumb it, do not bolt it* holds on **reach** rather than on rate — plumbing a panel is worth 73.5 K over bolting it, while the joint itself now carries 1,168 W/K against a sink face's 1,000 ([backlog.md](backlog.md) `C25`). Ring, layout and air-conditioning figures re-read from their own scenarios. |
| 2026-08-23 | Re-quoted the `loop-layout` and `air-conditioning` figures after `C4`: the scenario catalogue's blocks derive from the ones they stand in for now, and the rigs state their load in watts of heat rather than in a reactor's output. Bunched-against-spread is 140 C against 91 C, four rings 93 C; the cabin settles at −60 C with the pump off and −106 C with it on. |
| 2026-08-22 | Said that a coolant pump draws power — 50 kW large, 10 kW small, all of it becoming heat ([backlog.md](backlog.md) `C13`). It drew nothing until now. |
| 2026-08-22 | Removed a trailing *Radiators and the heat pump* section that restated the [Radiator](#radiator) and [Heat pump](#heat-pump) sections above it in weaker form — the emissivity, the multiplier, the absent coolant ports and the Carnot cost were each already stated once. The one thing it said that they did not, that a pump pairs with a radiator on its hot side, moved into the heat pump's own section. |
| 2026-08-22 | Added the standard header and this change log. |
| 2026-08-19 | Corrected two claims on this page that measurement contradicted, and answered two build questions with measurements rather than intuition: several small loops do **not** beat one big one, and a heat pump does work as air conditioning through a wall. Let pumps drive a ring either way, so a backwards pump still works. Reported a coolant block's loop — and why it has none — in its own terminal, and reported flow in metres per second. |
| 2026-08-17 | Moved the readouts off Text HUD API onto Rich HUD, and replaced thermal vision with the x-ray block overlay. |
| 2026-08-12 | Opened the page against `Data/CubeBlocks/`. |
