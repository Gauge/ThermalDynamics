# Definition reference

Thermodynamics reads none of its tuning values from its own C# constants. Everything lives in
`<ModExtensions>` groups attached to game definitions, read at runtime through Draygo's
**Definition Extensions** mod. That means any mod — including one that does not depend on this
one — can declare thermal properties for its own blocks, and this mod will pick them up.

The three files that ship with the mod are listed in
[Data/definitionextensions.txt](../Thermodynamics/Content/Data/definitionextensions.txt), which is how Definition
Extensions discovers them:

```
Cubes.xml
Planets.xml
Loops.xml
```

> The rules argued here are stated canonically in [rules.md](rules.md): `D3` `E3` `E6` `E11` `R8`.

| Looking for | Go to |
| --- | --- |
| Where the shipped planet figures come from | [environment.md](environment.md#planet-properties) |
| What each property does physically | [thermal-model.md](thermal-model.md) |
| World settings, as against per-definition values | [configuration.md](configuration.md) |
| What a block is worth once tuned | [balance.md](balance.md) |

## Group: `ThermalBlockProperties`

Read by `ThermalCellDefinition.GetDefinition`
([Definitions/ThermalCellDefinition.cs](../Thermodynamics/Definitions/ThermalCellDefinition.cs)).

| Property | Type | Clamp | Meaning |
| --- | --- | --- | --- |
| `ExcludeFromSimulation` | Bool | — | `true` excludes the block from the simulation entirely: no cell is created, it conducts nothing and blocks nothing. |
| `Conductivity` | Decimal | `≥ 0` | Thermal conductivity in **real W/(m·K)** — the number a materials table gives. Mild steel 50, stainless 15, glass 1, aluminium 237, copper 400. The game's pace is set once, globally, in `ThermalConstants.ConductionScale`, so this stays a description of the material. |
| `SpecificHeat` | Decimal | `≥ 0` | Heat capacity in **real J/(kg·K)** — look the material up. Steel 450, copper 385, aluminium 900, graphite 710, water 4184. Higher = slower to heat and to cool. See [the note below](#specific-heat-is-real-and-the-clock-is-not). |
| `Emissivity` | Decimal | `≥ 0` | Fraction of blackbody radiation **emitted**. Physically `0 … 1`. |
| `SolarAbsorptivity` | Decimal | `≥ 0` | Fraction of arriving radiation **absorbed** — the sun, and every point heat source. Physically `0 … 1`. **Omit it and it follows `Emissivity`**, which is what every block did before this property existed, so nothing already authored changes. See [Emissivity and absorptivity are two numbers](#emissivity-and-absorptivity-are-two-numbers). |
| `ExposedSurfaceMultiplier` | Decimal | `≥ 0` | Multiplies the geometric face area. Use `> 1` for finned or folded surfaces (the radiator uses `1.25`). It scales **every** external path, so a large value also multiplies solar gain and reentry friction — and it buys much less than it looks: see [balance.md](balance.md). |
| `ProducerWasteEnergy` | Decimal | `≥ 0` | Fraction of *generated* power converted to heat. |
| `ConsumerWasteEnergy` | Decimal | `≥ 0` | Fraction of *consumed* power converted to heat. |
| `CriticalTemperature` | Decimal | `≥ 0` | Kelvin above which the block takes damage. |
| `OverheatDamagePerKelvin` | Decimal | `≥ 0` | Damage per Kelvin of overshoot, per second. |
| `HeatSourceWatts` | Decimal | `≥ 0` | Watts the block makes because of **what it is**, not because of power crossing it — decay heat in spent fuel, a forge, a wreck still burning. Every other heat term is a fraction of watts passing through, which describes a reactor and a thruster and nothing else. [Omitted, and correctly so, by every block that is not one](#every-property-but-one-must-be-declared). |

### Emissivity and absorptivity are two numbers

A surface is not obliged to absorb what it emits. Real spacecraft radiators are *selective*: high
emissivity in the thermal infrared where their own heat leaves, low absorptivity in the visible
where the sun arrives. Second-surface mirrors reach `α ≈ 0.08` against `ε ≈ 0.8`; white paint is
around `0.2` against `0.9`. That is a factor of ten between the two, and one number cannot express
it — which is why, while emissivity did both jobs, **a good radiator was forced to be a good
absorber** and improving one improved the other exactly as much.

They are separate now, and the separation is what makes surface finish a design decision rather
than a constant. **One block takes it up**: the radiator declares `SolarAbsorptivity 0.1` against
its emissivity of 0.35, which is worth 10.9 K to a sunlit stack and nothing in shadow — and the
measurement beside it says why raising the emissivity instead would have bought 1.9 K in sun. See
[balance.md](balance.md#what-a-selective-surface-is-worth). The default is unchanged in both directions: an entry that does not mention
`SolarAbsorptivity` absorbs at its emissivity, and a block with no entry at all derives one number
from its build components and uses it for both.

**Omission means "follow", not "zero".** It is the second property where that is true, and for a
different reason from [`HeatSourceWatts`](#every-property-but-one-must-be-declared): here zero is a
real answer — a perfect reflector — so the sentinel is a negative value rather than an absent one,
resolved in a single place on `BlockThermalProperties`.

### Every property but one must be declared

An omitted property arrives as **zero**, not as the value of the entry it stands in for, and zero
is catastrophic for all but one of them: a block with no `SpecificHeat` reaches any temperature
instantly, and one with no `CriticalTemperature` is above critical the moment it is placed. A
*type* entry is the exception in the other direction — it declares only what a block's function
decides and leaves the materials to the component derivation, which is what the `Declared` bitmask
is for.

`HeatSourceWatts` is the one property where omission is the right default: zero watts of intrinsic
heat is exactly what a block that is not a heat source should have.
`ShippedDefinitionTests.EveryEntryInCubesDeclaresEveryPropertyTheGameReads` holds the rest.

### Retired property names

These were renamed because they said the wrong thing. **Both spellings are read**, current name
first, so a definition written before the rename still means what it said — Definition Extensions
matches on the string, and dropping the old name would silently revert third-party definitions to
the shipped defaults with no error and no log line.

| Retired | Current | Why |
| --- | --- | --- |
| `CriticalTemperatureScaler` | `OverheatDamagePerKelvin` | Read as "multiply the critical temperature", which is not remotely what it does |
| `SurfaceAreaScaler` | `ExposedSurfaceMultiplier` | Said what it multiplied but not that the scope is the *outside* faces only |
| `IgnoreThermals` | `ExcludeFromSimulation` | Read as "ignores heat damage" rather than "is not simulated at all" |
| `PlateSurfaceAreaScaler` | `SinkContactMultiplier` | Nothing else in the mod calls a sink face a plate |
| `PipeSurfaceAreaScaler` | `PipeContactMultiplier` | Pairs with the above; the two are siblings and now look it |
| `MassPerPipe` | `CoolantMassPerPipe` | Mass of what, in a file full of blocks |
| `SegmentsPerSecondAtFullFlow` | `LargeGridFlowRate` / `SmallGridFlowRate` | Parcels per second is the solver's unit, not a player's; and one parcel is one pipe block, so a single figure meant 10 m/s on a large grid and 2 m/s on a small one. Converted on read, per grid. |
| `FlowRate` | `LargeGridFlowRate` / `SmallGridFlowRate` | One rate for both grid sizes. Already metres per second, so it is read into both rather than converted — split for the reason the row above gives. |

New definitions should use the current names. The old ones are not planned for removal.

### `Conductivity` is in real W/(m·K)

Type the figure a materials table gives: mild steel 50, stainless 15, glass 1, aluminium 237,
copper 400. `ThermalLink` multiplies it by `ThermalConstants.ConductionScale`, which is one global
constant setting the game's pace, so the value here stays a description of the material rather than
a balance dial.

This was not always true. It used to be a 0…1 quality value against a 200 W/(m·K) reference, where
a tabulated figure clamped to 1 and quietly meant "the best there is" — which made every metal
conduct like every other metal. **A coolant loop's `Conductivity` still works the old way**, and
deliberately: fluid-to-wall transfer is convective, and the honest dial for it is a heat transfer
coefficient in W/(m²·K), which is a change to the loop equations rather than to a number.

**What that conversion moved**, because it is the substance of [backlog.md](backlog.md) `C2`.
`ConductionScale` was 2.4 because mild steel's 50 lands on the 120 the old default's 0.6 gave it, so
ordinary armour was exactly unmoved by the conversion and everything else was not. **It is 9.6
now** — `C24` multiplied the pace by four for `G8`'s timing, which moves every block including
armour and is a balance decision rather than a conversion; the calibration is what makes the
conversion checkable, and the two are separate numbers on purpose. **Before the conversion every block
in the game took one of exactly two conductances** — the file then held twenty-two definitions and
derivation from build components arrived after it — which is what makes the old world recoverable
and the change measurable.

The four families this file authors:

| Block | Old quality | Old effective | Now | New effective | Change |
| --- | ---: | ---: | ---: | ---: | ---: |
| armour and anything undeclared | 0.6 | 120 | 50, mild steel | 120 | **1.00×** |
| coolant pipe | 1 | 200 | 400, copper | 960 | **4.80×** |
| radiator | 1 | 200 | 237, aluminium | 568.8 | **2.84×** |
| the two pumps and the two heat pumps | 1 | 200 | 50, steel casing | 120 | **0.60×** |

**And every vanilla block, through derivation, which is the larger half and which this table used to
omit.** It also said *thruster … 0.60×*; that is true of the hydrogen thrusters and of nothing else,
because a thruster's conductance now comes from what it is built out of and the three families are
built out of different things. Measured 2026-08-23 off the shipped definitions by
`ConductanceRetest.Moves`, printed by `dotnet run --project Thermodynamics.Sim -- conductance`, and
pinned by `ModHardwareRetestTests`:

| Block | Before | Now | Change |
| --- | ---: | ---: | ---: |
| light and heavy armour | 120 | 120.0 | **1.00×** — the calibration |
| large ion thruster | 200 | 45.3 | **0.23×** |
| large hydrogen thruster | 200 | 120.0 | **0.60×** |
| large atmospheric thruster | 200 | 254.5 | **1.27×** |
| large reactor | 200 | 103.2 | **0.52×** |
| battery | 120 | 61.2 | **0.51×** |
| jump drive | 120 | 421.1 | **3.51×** |
| solar panel | 120 | 88.4 | **0.74×** |
| gyro | 120 | 121.3 | **1.01×** |
| cockpit | 120 | 108.1 | **0.90×** |
| conveyor | 120 | 163.4 | **1.36×** |
| large cargo container | 120 | 140.7 | **1.17×** |

The two ends of that table are the two ends of what a player feels. **The ion thruster's 0.23× is the
largest loss the conversion caused** — a burning hull now holds heat where its thrusters are — and
**the jump drive's 3.51× is the largest gain**, on the block type carrying 71.3 % of the population's
full-load waste. What both did to a real ship is measured in
[balance.md](balance.md#what-the-real-unit-conversion-moved).

**Every authored figure now says where it came from**, and `AuthoredMaterialTests` holds it to that:
either it names a material [`ReferenceMaterials`](../Thermodynamics/Core/Definitions/ReferenceMaterials.cs)
prices and matches it within five per cent — the rounding the environment default's 450 against mild
steel's 466 needs — or it says `invented`, which four figures do: the two lamps and the camera, none
of which is one substance. A figure with no provenance at all fails, which is what stops the check
decaying as values are added.

### Specific heat is real, and the clock is not

`SpecificHeat` is in the units you would find in a materials table, so a definition reads as a
description of what the block is made of:

| Material | J/(kg·K) | Used by |
| --- | --- | --- |
| Copper | 385 | coolant pipes |
| Steel | 450 | armour, most blocks, pumps, thrusters |
| Graphite-shielded steel | 600 | reactors |
| Aluminium | 900 | radiators |
| Water-glycol | 3400 | the coolant fluid itself |

A real ship at real heat capacity is also a *slow* ship: a hot hull takes hours to cool, which
is accurate and not much of a game. The pace comes from one global setting,
`HeatTimeScale` ([configuration.md](configuration.md)), which divides every heat capacity —
blocks and coolant alike — by the same number. Dividing capacity is exactly running thermal time
faster: every mechanism is a rate over that capacity, so **equilibrium temperatures, the balance
between conduction and radiation, and the ratios between block types are all unchanged**. Only
the clock moves.

That separation is the point. Tune *what a block is* here, in real units, and tune *how fast the
game feels* there, in one place.

The shipped default is **90**. It was 225 until `C24`, and 225 is the value that makes steel's real
450 J/(kg·K) behave the way the flat game value of `2` used to — the calibration the conversion in
this page was checked against, and two and a half times faster than what a world now runs. What
moved it is `G8`'s significance window; see
[balance.md](balance.md#the-route-is-chosen-and-it-is-the-one-the-cost-column-argued-against).

### Where a block's properties come from

**Almost nothing is authored.** A block's material properties are *derived* from its build
components — the list of steel plates, glass, power cells and motors the game already publishes for
every block that exists. `BlockMaterials` gives each of the game's thirty-two components real
material figures, and `BlockThermalDerivation` blends them by mass.

That means a window is glass, a battery is lithium, a medical bay is mostly water and a plushie is
fabric, without anyone having written a line for any of them — and the same is true of every block
of every other mod, which previously all fell through to one entry describing mild steel.

| Derived from components | Taken from the block's type | 
| --- | --- |
| `Conductivity`, `SpecificHeat`, `Emissivity`, `CriticalTemperature` | `ProducerWasteEnergy`, `ConsumerWasteEnergy`, `ExposedSurfaceMultiplier`, `OverheatDamagePerKelvin` |

…with one exception, below: where a block's own definition states a `PowerEfficiency`, its
`ConsumerWasteEnergy` is derived from that rather than taken from its type.

`SolarAbsorptivity` is in neither column: it follows the emissivity — derived or declared — until
somebody writes one, because a build cost says what a surface is made of and nothing about how it
was finished.


The split is not arbitrary. What a block is made of cannot say what it does with power: two blocks
of identical construction, one a thruster and one a girder, differ entirely in what they put into
the ship. The functional half is a table keyed by block type in `BlockThermalDerivation`, and it is
the only place opinions are left.

**One functional property is not an opinion, because the game publishes it.** A jump drive's
definition carries `PowerEfficiency` — 0.8 on the vanilla drive and its reskin, 0.9 on the two
prototech ones — and the power a charging block draws and does not store is heat. So
`ConsumerWasteEnergy` for a drive is `1 − PowerEfficiency`, derived per block rather than authored
per type, and `BlockThermalDerivation.WasteFromEfficiency` is the rule. It is the only family in the
whole game that states an efficiency; twelve thruster definitions state `1.0`, which is about thrust
rather than about heat and is not read.

It sits **above the type entry and below a subtype entry**: a type entry describes a family and
cannot say both 0.8 and 0.9, while a subtype entry describes one block and is a deliberate override.
A zero is silence, not total loss — every other block in the game omits the field, and reading its
absence as *stores none of what it draws* would make all of them heaters.

**What it corrected.** The drive was authored at `0.15` with the note *"storage is efficient; the
dump is not"* — an assertion, and wrong for all four drives in both directions. It is now 0.2 and
0.1, which matters more than it sounds: `LargeJumpDrive` carries **71.3 %** of the corpus's
full-load waste heat, so the one number in the file with the largest reach was the one with no
source. A modded drive nobody has written an entry for is now right too.

### Every waste fraction says where it came from, and most of them say "invented"

The derivation above covers what a block is *made of*. What it *does with power* is the other half of
a definition, and it is authored — which is why every fraction in `Cubes.xml` states where it came
from and a check holds it to what it says. Nothing did before this, and that is how the jump drive
came to sit at `0.15` under a note that read as an argument.

Each fraction now claims one of four provenances, and `AuthoredWasteTests` holds it to the claim:

| Claim | What it means | What checks it |
| --- | --- | --- |
| `waste: <conversion>` | A named class of machine in `ReferenceEfficiencies` | The value is inside that conversion's band |
| `derived: <field>` | A field the game's own definitions state | Recomputed from every definition of the type |
| `no producer: …` | Nothing multiplies this fraction | No definition of the type declares power output |
| `invented: …` | An admitted opinion | Nothing, and saying so is the point |

**Bands rather than point values, because that is what the literature gives.** A motor's efficiency
is a range over frame sizes and duty points, so `ReferenceEfficiencies` carries the range and the
check asks whether the authored value sits inside it. Six conversions have a source worth quoting:
an electric motor at 0.05–0.15 waste, a lithium-ion store at 0.02–0.06 one way, a spark-ignition
engine at 0.55–0.70, a radio transmitter at 0.60–0.85, a solid-state laser at 0.50–0.90, and *all of
it* — the first law's bound on a device that does no work outside itself and radiates nothing away.

**The counts are the finding.** Of the 232 waste fractions, **45** name a conversion, **1** is derived
from the game, **110** are fractions nothing multiplies, and **76** are
inventions. Of the 122 that anything ever multiplies, **76 are opinions** — but counting fractions
and weighting them by the heat they carry disagree about how much that matters:

| | Share of the fractions | Share of the corpus's full-load waste heat |
| --- | ---: | ---: |
| derived from the game | 0.4 % | **76.4 %** |
| sourced to a conversion | 19.4 % | 8.8 % |
| invented | 32.8 % | 14.8 % |

> **The heat column does not move when this mod adds a block of its own**, and that is not an
> oversight. It is measured over the workshop corpus, which is ships built by players who did not
> have this mod installed — so every fraction the mod authors for its own blocks carries 0 % of it,
> the radiator and the two pumps included.

> **27 fractions moved from *invented* to *sourced* on 2026-08-24**, when `C21`'s
> computer-and-screen third closed: the counts were 15 / 1 / 108 / 104 and the heat shares 8.4 % and
> 15.3 %. Every one of them was a device that does no work outside itself, whose own note already
> said *the first law says 1.0* and then set 0.9. They are 1.0 against the `all of it` conversion
> now — a band with no width, because it is a bound rather than a measurement. The two lamps that
> point out of the hull stayed invented and now say why they are under 1.0.
>
> **The oxygen generator followed on 2026-08-25**, when the last of the three closed: the counts
> were 42 / 1 / 108 / 77 and the heat shares 8.5 % and 15.2 %. It is `waste: water electrolysis` at
> 0.40 now, and it moved because 0.6 put two of the six vanilla generators past critical in open
> space rather than because the gap was the file's largest. **It is also the one restatement in this
> table that is a balance change**: 0.6 to 0.40 is a third off, where the twenty-seven before it
> were worth 0.025 % of a fleet's waste between them.

*Population: the 8,142-ship census of 2026-08-21, 109,312 block rows. Basis: full electrical load
with every jump drive charging, no thrust — a bound rather than a duty cycle (`E3`). The drives are
restated at the fractions derived on 2026-08-23 rather than the 0.15 the census measured, and the
oxygen generator at the 0.40 decided on 2026-08-25 rather than the 0.6 it measured.
`tools/corpus/provenance.py` computes it.*

> **A fleet share is not a ship's share.** Every figure in the table above is a ratio of aggregates
> over the whole census, so a charging jump drive is three quarters of its denominator and a type
> carrying 0.38 % of it looks like a rounding error. Asked only of the ships that carry one, the
> oxygen generator is a median **14.4 %** of that ship's own full-load waste, with p90 at 80.8 %, and
> **64.5 %** of ships carry one — 5,184 of 8,032. Both statistics are right and they answer
> different questions; the second is the one about the player who built the block (`E6`).
> `provenance.py out/census-2026-08-25/composition.csv --type OxygenGenerator` computes it.
>
> **The 2026-08-21 census said 2,277 carriers and a median 48.1 %, and it could not see the block**
> (corrected 2026-08-25, `E10`): the blueprint reader built every empty-`SubtypeName` block as
> armour, and the vanilla large oxygen generator is one of the thirteen definitions the game gives
> no subtype, so that census holds none of the **31,214** the corpus actually carries. The reader
> is fixed and the census re-taken (`A13`); the stepped walks have not been.

**So the file is mostly opinion and the heat mostly is not**, because one derived block carries three
quarters of it. And the invented sixth is not spread over a hundred blocks either — four types carry
almost all of it: artificial mass at 4.0 %, the reactor at 3.6 %, the refinery at 3.0 % and the
assembler at 2.9 %. Everything else in the file, added together, is under two per cent of a loaded
fleet's heat.

**Three inventions had a real figure sitting beside them and did not use it, and all three are now
closed** — two of them by moving and one by staying. Every computer, screen and sensor wasted 0.9
where the first law says 1.0, since a device that does no work outside itself has nowhere else to
put what it draws; they are 1.0 now. The oxygen generator wasted 0.6 where water electrolysis
sources 0.20–0.40, which was the largest gap in the file; **it is 0.40 now, and what moved it was
not the gap.** At 0.6, two of the six vanilla generators sit past their own critical temperature
alone in open space at the draw their own definition rates them at, which is a block that cannot be
built rather than a balance choice — see
[balance.md](balance.md#oxygen-generator-waste-heat-written-before-it-is-measured), where the rule
was registered before the rig ran. And the reactor's 0.01 is a hundredth where a real thermal cycle
rejects about two thirds; that one is deliberate and measured, because at 0.02 the two smaller
reactors cook themselves bare in vacuum, and `C28` decided to keep it. Moving any of them is a
balance change and belongs in its own commit (`E11`), not in the pass that gave them provenance —
which is how both of the two that moved were done.

Of the three derived blends, only specific heat is exact — heat capacity is additive, so the
mass-weighted mean is the right answer rather than an approximation of one. Conductivity and
critical temperature are mass-weighted because a build cost does not say how the phases are
arranged. Emissivity is a property of the *surface*, so it comes from the heaviest component rather
than from a blend.

Run the sim harness's `blocks` command to print the whole table, every type and every subtype that
deviates from it.

### Lookup and fallback

An entry in `Cubes.xml` **overrides** the derivation. For a block with definition id
`TypeId/SubtypeId`, `GetDefinition` resolves in this order:

1. `TypeId/SubtypeId` — an exact per-block entry.
2. `TypeId/DefaultThermodynamics` — a per-type entry.
3. `EnvironmentDefinition/DefaultThermodynamics` — the global fallback.

The probe for steps 1 and 2 is whether the definition id is indexed **and** exposes
`ExcludeFromSimulation`, so a group without that bool is not an entry at all and falls through.

Two rules matter more than the order:

* **A partial entry is merged, not substituted.** Whatever an entry declares wins; whatever it omits
  is derived. So an author who wants a block to run hotter writes one line — a
  `CriticalTemperature` and nothing else — and keeps the material the block's components imply.
  Before this, an omitted property read as *zero*, and a zero `SpecificHeat` is a block with no heat
  capacity while a zero `CriticalTemperature` is a block above critical the moment it is placed.
  `EveryEntryInCubesDeclaresEveryPropertyTheGameReads` guards the shipped file against that.
* **Landing on the global fallback counts as no answer.** That entry describes mild steel, which was
  the best available guess before a block's own build cost could be read and is a worse one now, so
  it does not override a derivation that actually describes the block. It applies to a block with no
  priced components at all.

### Shipped values ([Data/Cubes.xml](../Thermodynamics/Content/Data/Cubes.xml))

`Cubes.xml` no longer carries an entry per block type. It holds only deliberate deviations from the
derivation:

| Definition | What it overrides and why |
| --- | --- |
| Coolant pipes and pumps (LG + SG) | Copper: conductivity 400, specific heat 385. The block exists to move heat. |
| Heat pumps (LG + SG) | Both waste fractions **0** — the solver already puts every watt the pump draws into its hot side, so a fraction on top would charge the same energy twice. |
| Radiators (LG + SG) | Emissivity 0.35 and 1.25× surface against the default 0.15 and 1×. Shedding is the block's whole purpose, so these are design decisions rather than consequences of a build cost. |
| `EnvironmentDefinition/DefaultThermodynamics` | The floor for a block with no priced components. |

Every vanilla block's properties now come from the derivation instead. Notable results, all of them
consequences rather than choices:

| Type | k W/(m·K) | c J/(kg·K) | ε | Critical K | Why |
| --- | --- | --- | --- | --- | --- |
| `CubeBlock` (armour) | 45 | 535 | 0.15 | 868 | steel |
| Windows | 8 | 788 | 0.92 | 814 | glass conducts two orders of magnitude worse and radiates six times better |
| `BatteryBlock` | 34 | 704 | 0.85 | **735** | lithium cells are the least heat-tolerant thing on a ship |
| `MedicalRoom` | 28 | 964 | 0.90 | **592** | mostly water, plastics and fluids |
| `Reactor` | 43 | 553 | 0.25 | 1,086 | fuel in a graphite and steel assembly |
| `HydrogenEngine` | 98 | 550 | 0.25 | 1,121 | carries a prototech cooling unit |
| `JumpDrive` | **173** | 460 | 0.20 | 745 | forty per cent superconductor by mass |
| `Thrust` | 56 | 472 | 0.40 | 1,115 | refractory nozzle alloy |
| Plushies | **0.05** | **1,300** | 0.95 | 500 | fabric, not the steel they used to be |

> A reactor's heat runs through `ProducerWasteEnergy` and nothing else. It delivers power through
> `MyResourceSourceComponent`, so its consumer fraction is dead text — getting that backwards is how
> every reactor in the game ran at 0 W. See [balance.md](balance.md#reactor-waste-heat).

## Group: `ThermalPlanetProperties`

Read by `PlanetDefinition.GetDefinition`
([Definitions/PlanetDefinition.cs](../Thermodynamics/Definitions/PlanetDefinition.cs)),
keyed on a `PlanetGeneratorDefinition` id, falling back to
`PlanetGeneratorDefinition/DefaultThermodynamics`.

| Property | Default | Meaning |
| --- | --- | --- |
| `NightTemperature` | 283.15 K | Ambient with the sun on the far side. **Equatorial, at sea level.** |
| `DayTemperature` | 294.261 K | Ambient with the sun directly overhead. **Equatorial, at sea level.** |
| `PoleTemperatureDrop` | 40 K | How much colder a pole is than the equator. Earth's is about 40. 0 gives one climate for a whole world. |
| `AmbientLagSeconds` | 45 s | How long the air takes to answer the sun. Without it the day's peak is exactly noon. **The fallback**: it runs until the mod has measured this world's day, and on a world whose sun does not move. |
| `AmbientLagShareOfDay` | 0.083 | The same lag as a share of this world's own day, which is what it should be — Earth's air peaks about two hours after noon out of twenty-four. Used wherever the day's length is known, which is within a tenth of a turn of a session starting. Zero leaves the absolute figure above in charge. |
| `AmbientLapseRate` | 4 K/km | How much colder a kilometre above sea level is. Earth's is 6.5; lower here because the ground table already makes mountains snowy. 0 switches altitude off. |
| `UndergroundTemperature` | 280 K | Ambient deep enough underground that the surface's day no longer reaches; also forces solar occlusion. |
| `UndergroundDampingDepth` | 20 m | Metres of rock that blunt the surface's day-night swing to nothing. Above it a buried block still feels part of the day; below it, none. |
| `CoreTemperature` | 3000 K | Temperature at the planet's centre. The rock warms toward it below `SealevelDeadzone`. |
| `SealevelDeadzone` | 2000 m | Depth **below sea level** at which core heating starts. Measured from sea level, so a tunnel into a mountain stays cold however deep it goes. Lower it to make reachable mining depths hot. |
| `SolarDecay` | 0.5 | Fraction of solar energy lost in a full-density atmosphere. |
| `ConvectionCoefficient` | 50 | W/(m²·K) base heat transfer into the air. |
| `UndergroundConvectionCoefficient` | 2 | W/(m²·K) for a grid buried in rock. Rock is a far worse heat sink than moving air: `2k/D` for rock at 2.5 W/(m·K) over a 2.5 m block. Crossed over the first five metres of burial, and neither wind nor weather multiplies it. |

The last five rows carry the model's own defaults rather than zero when the definition omits them,
unlike the rows above. They were added after the planet definitions were written, and zero is a
real setting for every one of them — no latitude, no lag, no lapse, no damping, no core — so a
planet file predating them would otherwise silently ask for all five to be switched off.

Weather is **not** in this group. The mod reads the weather's name from the game and looks it up in
[WeatherResponse](../Thermodynamics/Core/Definitions/WeatherResponse.cs), whose figures
are derived from Keen's own `WeatherEffects.sbc`, so a mod adding a weather called `RainHeavier`
gets rain behaviour without annotating anything. See
[environment.md](environment.md#weather).

Per-planet values are set by adding a definition with that planet's `SubtypeId`. The shipped
[Data/Planets.xml](../Thermodynamics/Content/Data/Planets.xml) carries **nine entries** — the `DefaultThermodynamics`
fallback plus one for each of Alien, EarthLike, Europa, Mars, Moon, Pertam, Titan and Triton — each
derived from that world's own generator definition rather than typed. **The file is generated**;
see [environment.md](environment.md#planet-properties) for the derivation, the three worlds whose
shipped entries depart from it, and how to regenerate it.

## Group: `ThermalLoopProperties`

Read by `ThermalLoopDefintion.GetDefinition`
([Definitions/ThermalLoopDefinition.cs](../Thermodynamics/Definitions/ThermalLoopDefinition.cs)).
There is currently one loop definition and every loop uses it:
`EnvironmentDefinition/DefaultThermodynamicsLoop` in [Data/Loops.xml](../Thermodynamics/Content/Data/Loops.xml).

| Property | Default | Clamp | Meaning |
| --- | --- | --- | --- |
| `CoolantKilogramsPerCubicMetre` | 33 | `≥ 0` | Coolant per cubic metre of the cell a pipe occupies, so a ring's charge scales with its length **and** with the grid it is built on — 515.6 kg a pipe on a large grid, 4.1 kg on a small one. A density rather than a flat mass because a flat one has no grid size in it; see balance.md, `C43`. |
| `CoolantMassPerPipe` | 0 | `≥ 0` | A flat coolant mass per pipe block, kg, overriding the density above. **Zero means "use the density"**, which is what ships. The name is kept rather than repurposed: a third-party file stating 50 here means fifty kilograms in a pipe, and reading it as a density would give a large-grid ring sixteen times the fluid it asked for. |
| `HeatTransferCoefficient` | 1000 | `≥ 0` | How well heat crosses between the fluid and the wall it touches, **W/(m²·K)**, for both the pipe and the sink-face exchange. Convective, so there is no thickness in it — the resistance is the boundary layer against the wall. It was a 0…1 quality against a reference conductivity divided by half a cell, which gave the game a second conduction pace and made the coefficient it implied 160 on a large grid and 800 on a small one for the same fluid ([backlog.md](backlog.md) `C20`). |
| `SpecificHeat` | 3400 | `≥ 0` | Coolant heat capacity in real J/(kg·K). Water-glycol is about 3400, which is why a loop carries so much more heat than the steel around it. Scaled by `HeatTimeScale` exactly as a block is. |
| `PipeContactMultiplier` | 1 | `≥ 0` | Contact area between fluid and the pipe block it runs through. |
| `SinkContactMultiplier` | 1 | `≥ 0` | Contact area between fluid and a block pressed against a sink face. |
| `LargeGridFlowRate` | 10 | `≥ 0` | How fast the coolant moves on a large grid with one pump at full power, **m/s** — the unit the terminal reports. Flow rises with the square root of combined pumping, so four pumps carry twice this, not four times. |
| `SmallGridFlowRate` | 10 | `≥ 0` | The same for a small grid. Split because it is a balance dial, not a physical constant: a small-grid pump is a much smaller machine driving a much shorter ring. Shipped equal. A small-grid pipe is a fifth as long, so the same speed is five times the parcel rate and the ring levels out sooner. |
| `StagnantTransferFraction` | 1 | `0 … 1` | Share of the fluid-to-wall coupling that survives with nothing circulating. Convective transfer depends on the flow, so a stopped ring conducts into its coolant more slowly than a pumped one; carrying that heat anywhere stops dead regardless, in `Advect`. |

## Adding thermal properties for another mod's blocks

Create a definition file listing the block ids you want to describe, register it in your own
`definitionextensions.txt`, and Definition Extensions will index it — no dependency on Thermal
Dynamics' assembly required:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Definitions xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
             xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <CubeBlocks>
    <Definition>
      <Id>
        <TypeId>Refinery</TypeId>
        <SubtypeId>MyMod_BigSmelter</SubtypeId>
      </Id>
      <ModExtensions>
        <Group Name="ThermalBlockProperties">
          <Bool    Name="ExcludeFromSimulation"           Value="false" />
          <Decimal Name="Conductivity"             Value="0.8"   />
          <Decimal Name="SpecificHeat"             Value="500"   />
          <Decimal Name="Emissivity"               Value="0.2"   />
          <Decimal Name="ExposedSurfaceMultiplier"        Value="1"     />
          <Decimal Name="ProducerWasteEnergy"      Value="0"     />
          <Decimal Name="ConsumerWasteEnergy"      Value="0.35"  />
          <Decimal Name="CriticalTemperature"      Value="1100"  />
          <Decimal Name="OverheatDamagePerKelvin" Value="0.5"  />
        </Group>
      </ModExtensions>
    </Definition>
  </CubeBlocks>
</Definitions>
```

Tuning guidance:

* Always declare **every** property in the group. Unset properties default to `0`, and a
  `SpecificHeat` of 0 produces a divide-by-zero in the cell constants.
* `SpecificHeat` × block mass is the real knob for thermal inertia. Heavy blocks are already
  slow; do not double-count by also raising specific heat.
* Set `ExcludeFromSimulation` to `true` for decorative, zero-mass or projector-only blocks.
* Waste-energy fractions above ~0.3 make a block a serious heat source; the vanilla default is
  0.05.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-08 | The debug heat source's two subtypes bring the file to **674 authored values and 232 waste fractions**, and the counts are 45 / 1 / 110 / 76. Nothing was invented: its producer fractions are `no producer:` like every other `UpgradeModule`'s, and its consumer fractions are `waste: all of it` — the first law on a block that does no work leaving it by any path power could take. It has no resource sink at all, so nothing multiplies them today; they are authored so that they stay right if one is ever attached. See [blocks.md](blocks.md#debug-heat-source). |
| 2026-08-26 | `CoolantKilogramsPerCubicMetre` is the coolant charge in `Loops.xml`, and `CoolantMassPerPipe` is kept as a flat-mass override defaulting to zero rather than repurposed (`P15`): a third-party file stating 50 there means fifty kilograms in a pipe, and reading it as a density would give a large-grid ring sixteen times the fluid it asked for. `HeatTransferCoefficient`'s default in the table was corrected from 160 to the 1,000 `C42` shipped. |
| 2026-08-26 | Gave `StagnantTransferFraction` the leg it describes. It was authored for the segment-to-segment transport, which `Advect` already stops dead on zero flow, so the field multiplied nothing anywhere in the simulation — found by `LoopDialReachTests`, which enumerates this table's fields by reflection rather than reciting them. It now scales the fluid-to-wall coupling, where a flow dependence is physically real. Same name, same range, same default of 1. |
| 2026-08-25 | **Corrected the ship's-share figure beside the heat table, because the census could not see the block it is about** (`E10`). It said 2,277 carriers and a median 48.1 %; the blueprint reader built every empty-`SubtypeName` block as armour, and the vanilla large oxygen generator is one of thirteen definitions the game gives no subtype. A 400-ship parse through the fixed reader put it at 64.5 % of ships and a median 10.4 %; the re-censused population settles it at **64.5 % and a median 14.4 %**, over **31,214** vanilla generators the old census held none of. The heat table above still rests on the 2026-08-21 census and the stepped walks still do, which is `A13`. |
| 2026-08-25 | **`C21`'s last open invention closed: the oxygen generator is `waste: water electrolysis` at 0.40, and the counts are 43 / 1 / 108 / 76.** It moved because 0.6 put two of the six vanilla generators past their own critical temperature *bare* at their rated draw — a block that cannot be built — and not because the gap between 0.6 and the sourced band was the largest in the file. `water electrolysis` is a new conversion in `ReferenceEfficiencies`, banded 0.20-0.40 against alkaline and PEM electrolysers. The heat-share table moved with it, and it carries the one restatement in this page that is a balance change rather than a correction. **And this page's fleet share of 0.38 % was answering a different question from the one a player asks**: on the 2,277 ships that carry a generator it is a median 48.1 % of their own waste, which is now stated beside it (`E6`). The rule that decided it was registered before the run, in [balance.md](balance.md#oxygen-generator-waste-heat-written-before-it-is-measured). |
| 2026-08-23 | **The coolant loop's fluid coupling is a heat transfer coefficient in W/(m²·K), and the game has one conduction pace again** ([backlog.md](backlog.md) `C20`). It was a 0…1 quality times a reference conductivity of 200, divided by half a cell — which made the coefficient it implied depend on grid size: 160 on a large grid and 800 on a small one, for the same fluid against the same wall. Convection has no length in it. 160 is what a large grid was already running at, so nothing there moves; a small-grid loop couples a fifth as hard as it did. |
| 2026-08-23 | The radiator declares `SolarAbsorptivity 0.1`, which is the first shipped block to use the split at all ([backlog.md](backlog.md) `C15`). Worth 10.9 K to a sunlit stack, nothing in shadow, and emissivity untouched. |
| 2026-08-23 | **Gave every waste fraction a provenance, and measured how much of a fleet's heat rests on the ones that have none** ([backlog.md](backlog.md) `C21`). All 228 `ProducerWasteEnergy` and `ConsumerWasteEnergy` values in `Cubes.xml` now claim a source, a derivation, a statement that nothing reads them, or an admitted invention, and `AuthoredWasteTests` holds each claim to its evidence — a band in `ReferenceEfficiencies`, the game's own `PowerEfficiency`, or the game declaring no output for the type. The counts are [the finding](#every-waste-fraction-says-where-it-came-from-and-most-of-them-say-invented): 15 sourced, 1 derived, 104 invented — and weighted by the heat they actually carry that is 8.4 %, 76.3 % and 15.3 %, so the file is mostly opinion and the heat mostly is not. No value moved; three inventions that have a real figure beside them are recorded rather than retuned (`E11`). |
| 2026-08-23 | **Derived the jump drive's waste fraction from the efficiency the game states, instead of asserting it.** `PowerEfficiency` is 0.8 on the vanilla drive and its reskin and 0.9 on the two prototech ones, so their `ConsumerWasteEnergy` is 0.2 and 0.1; it was `0.15` for all four, by a comment rather than a source. It is the only family in the game that publishes an efficiency, and it is the block carrying **71.3 %** of the corpus's full-load waste heat — the number in the file with the largest reach was the one with no provenance. The rule is one function in `Core` with three readers, because a harness that disagreed with the mod about it would be measuring a mod nobody runs. |
| 2026-08-23 | **Corrected [what the conversion moved](#conductivity-is-in-real-wmk).** The table stated *thruster … 0.60×*, which is true of the hydrogen thrusters and of nothing else — the large ion thruster is **0.23×** and the atmospheric **1.27×**, because a thruster's conductance is now derived from what it is built out of and the three families are built out of different things. The table also described only the four families this file authors and omitted every vanilla block, which is the larger half of the change and holds both of its extremes: the ion thruster's 0.23× and the jump drive's **3.51×**. Measured off the shipped definitions and pinned by `ModHardwareRetestTests` rather than stated. |
| 2026-08-23 | Made every authored material figure say where it came from, and checked the ones that name a material. `AuthoredMaterialTests` holds all 46 `Conductivity` and `SpecificHeat` values in `Cubes.xml` against `ReferenceMaterials` or against an explicit `invented`; four had no provenance at all and now have it, one of which — the emissive block's 1 and 840 — turned out to be soda-lime glass exactly and never said so. Wrote down [what the conversion to real units actually moved](#conductivity-is-in-real-wmk), because [backlog.md](backlog.md) `C2` had the radiator backwards: it is 2.84× stiffer, not half, and the blocks that lost are the thruster and the two pumps at 0.60×. |
| 2026-08-22 | Added `AmbientLagShareOfDay`. The climate's lag was 45 absolute seconds against a rotation a server sets to anything, so one authored figure meant a different climate on every world ([backlog.md](backlog.md) `C6`). |
| 2026-08-22 | Added `UndergroundConvectionCoefficient`. A buried grid exchanged with rock at the coefficient for moving air, which made digging in the best cooling in the game ([backlog.md](backlog.md) `A16`). |
| 2026-08-22 | Split `SolarAbsorptivity` off `Emissivity`. One number did both jobs, so a good radiator was forced to be a good absorber — the one combination real spacecraft radiators exist to avoid ([backlog.md](backlog.md) `B27`). Omitting it follows the emissivity, so nothing already authored changes, and zero stays a real answer rather than reading as unset. |
| 2026-08-22 | Corrected the claim that `Data/Planets.xml` defines only the fallback, so every planet runs Earthlike numbers. The file carries nine entries — `DefaultThermodynamics` plus one per shipped world — generated from each world's own generator definition. Added the standard header and this change log. |
| 2026-08-22 | Checked all three definition readers against this reference rather than only the block one, and put the newly working heat-source property in it. |
| 2026-08-20 | Let the derivation reach every vanilla block. |
| 2026-08-19 | Renamed the definition dials that said the wrong thing, gave each grid size its own coolant flow rate, and gave the decorative blocks definitions of their own. |
| 2026-08-12 | Took specific heat in real J/(kg·K), with pace as one explicit setting. |
