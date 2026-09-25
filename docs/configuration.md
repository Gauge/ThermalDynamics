# Configuration

Settings live in `ThermodynamicsConfig.cfg` in world storage, written with defaults the first time
a world loads and regenerated when `Version` does not match.

> The rules argued here are stated canonically in [rules.md](rules.md): `R8` `C7` `C8`.

| Looking for | Go to |
| --- | --- |
| Per-block, per-planet and per-loop properties | [definitions.md](definitions.md) |
| How far the model is from physics | [realism.md](realism.md) |
| What each setting is scaling | [thermal-model.md](thermal-model.md), [environment.md](environment.md) |
| Driving settings from another mod | [api.md](api.md#settings) |

Every setting can also be changed while the world is running. A change is written into the settings
object every grid already holds and picked up on the next step: heat capacities are rescaled,
coolant loops and room air are rebuilt, and each mechanism reads its own switch. Nothing is written
to disk unless asked.

## Runtime control

| Command | Effect |
| --- | --- |
| `/thermal status` | Collection state, sample stride, live grids, block models, bridges, and how many validation problems have been reported. |
| `/thermal problems` | Every distinct problem the definition and settings validators found this session, once each. The same lines are in the game log. |
| `/thermal settings` | Every setting and its current value. |
| `/thermal set <name> <value>` | Changes one setting for this session. Switches take `on`/`off` or `1`/`0`. On a multiplayer client this asks the server, which answers whether it was allowed. |
| `/thermal save` | Writes the current values to the config file. |
| `/thermal sync` | Digest of every replicated setting, to compare a client against the server by eye. Run it on both; the strings must match. |
| `/thermal sync fetch` | Client only: asks the server for the settings again. |
| `/thermal overlay` | Cycles the block overlay. Same as Ctrl+Shift+=. |
| `/thermal menu` | Opens the settings menu. Same as Ctrl+Shift+S. |
| `/thermal telemetry on` / `off` | Starts and stops data collection. |
| `/thermal stride <n>` | Telemetry sample stride. |
| `/thermal dump` | Writes a telemetry report without closing the world. |

Settings are world state. A client at space master or above may change them — the request goes to
the server, which decides — and the four presentation switches belong to the client outright. The
same names are reachable from other mods, see [api.md](api.md#settings).

## The settings menu

**Ctrl+Shift+S** opens it and closes it again, as does the window's own close button and
**Escape**. `/thermal menu` opens it too — it does not close it, because a command that did would
have to be typed into a chat box the window is covering.

The menu is a tree rather than one scroll: **Statistics**, **Debug** and **Defaults** at the root,
then six folders.

| Folder | Pages |
| --- | --- |
| **Solver** | Cost limits, Pace |
| **Heat transfer** | Ambient exchange, Sunlight |
| **Ship systems** | Coolant loops, Heat pumps, Room air, Heat made and damage, Suit |
| **Aerodynamics** | Friction heating |
| **Multiplayer** | Temperatures |
| **World** | Climate, Underground, Wind |

**Every setting is on one of those pages.** A setting the layout tables do not name still gets a
control, on a final **Other** page — that floor stays, because a setting added to the config and
forgotten here should be reachable rather than invisible. It is empty now and
`EverySettingIsOnAMenuPageThatNamesIt` keeps it that way: eleven settings had stayed on it,
including the whole suit subsystem, each labelled with its own field name and given a slider from 0
to 1,000 whatever it was — which for a suit's heat capacity of 240,000 J/K is a control that cannot
express its own default.

A hundred settings on a single scroll is a list to be searched by eye, and an administrator
usually arrives wanting one part of it. **One system to a page, with its own switch at the top.**
Grouping the switches together would be filing by part of speech: switching convection off belongs
above the convection dials, where a reader can see what it governs.

**Fourteen pages where there were twenty-three.** Conduction, Radiation and Convection were a page
each holding a single switch, because a block's conductivity, emissivity and exposed area are its
own and live in `Cubes.xml` — so each page said "the rest of this is elsewhere" and had nothing else
to say. They sit on **Ambient exchange** with the switch that governs all three, which is not the
filing-by-part-of-speech mistake above: these have no dials to be kept beside. Waste heat, Point
sources and Overheat damage merged the same way into **Heat made and damage**, Solar and Occlusion
into **Sunlight**, and Threading into **Cost limits**, where
spreading a frame's grids across cores belongs with everything else that decides what a step costs.

**Each page is in two tiers.** The dials a world reaches for are at the top; below an **Advanced**
line are the ones it tunes once or never — the fluid's specific heat, the lapse rate, the terrain
read radius. Both tiers are on the page that owns them, so nothing has moved out of reach, and
`EverySettingIsOnAMenuPageThatNamesIt` still holds over both.

A page whose system still keeps most of its numbers in a definition file says so, rather than
looking broken with one switch on it. A setting named on no page still gets a control, on a final
**Other** page — a setting added to the config and forgotten here is reachable rather than
invisible.

**The menu is a window this mod draws, not a page in the Rich HUD terminal.** The terminal takes a
control nowhere but inside a *tile*: a fixed 300×250 box with a border of its own and a scroll bar
under the row it sits in, created and drawn inside Rich HUD Master, whose client API offers
`AddControl` and `Enabled` and nothing else. So a page there was a grid of panels that no mod could
restyle or escape, and a section longer than three controls drew over itself. The framework's HUD
element library is a different matter — it compiles into this mod and draws from here, which is
what the cockpit readouts and the debug panel are already made of — so
[`ThermalSettingsWindow.cs`](../Thermodynamics/ThermalSettingsWindow.cs) builds the
window and [`ThermalSettingsMenu.cs`](../Thermodynamics/ThermalSettingsMenu.cs) keeps
what it says.

A setting therefore sits on the page itself rather than inside a panel: its name down the left, its
control in a column of one width, and the value it is at on the right, so a page of forty dials
reads as columns. The pages are listed down the left under their folder headings, the window drags
and resizes, and it closes on Escape, on its close button,
or on the keystroke that opened it.

It still needs **Rich HUD Master** (`1965654081`) enabled, because the HUD tree is rooted in it and
this mod registers as a client of it; without it the keystroke says so and the chat commands remain
the way in. What the window looks like is this mod's own decision.

**The mod keeps one page in the Rich HUD terminal, and it holds a button rather than settings.**
That list is where a player looks for a mod's settings, and a mod absent from it reads as a mod
with nothing to configure — so the page under **Thermodynamics** carries one button, which closes
the terminal and opens the window, and says the keystroke that does the same without coming there
at all.

**Some settings are typed, not dragged.** A slider offers about two hundred distinguishable
positions, which suits a fraction between 0 and 1 and suits nothing else this mod has. The step
budget spans eight million, so one position is forty thousand element visits; the friction scale spans
a hundredth, so every position shows the same number. Seven settings therefore get a field to type
a value into — the step budget, terrain range, solar energy, heat time scale, vacuum temperature,
the friction threshold and the friction scale — and the rest keep their sliders.

**Ctrl and a click on a slider types the value instead.** A slider has about two hundred positions,
so a range it can *nearly* divide is the awkward one: it looks as though it reached 6.5 and it is at
6.47. Hold Ctrl, click the slider, and it becomes a field on the number it is at — Enter or a click
elsewhere commits, Escape leaves the setting where it was. The click is taken by an overlay rather
than by the slider, so the value does not jump to wherever the pointer landed on the way to typing a
different one. The settings that are *always* typed are the ones below, whose ranges a slider cannot
divide at all.

The range in a typed field's tooltip is what the slider *would* have spanned, not a limit. A typed
value goes through the same clamp as `/thermal set` and the mod API, so a step budget of nine
million is yours to try. Anything unreadable puts the setting's own value back rather than guessing.

**Statistics is the page the menu opens on, and everything the menu can report is on it.** It is
prose rather than controls, so it is the one page that can say what a figure is measured over. It carries four things: what this world is set to — the mod version, how many
settings differ from the shipped defaults, the settings digest to compare against the server's
`/thermal sync`, and any conflict spelled out in full; what is running — grids, blocks, solver nodes,
links, the hottest block and how many are over critical; the energy the world is moving — heat made,
vented, exchanged with the ambient and taken from the air by friction; and what the solver is
spending — substeps granted against substeps asked for, steps shortened to fit the budget, blocks
floored by the per-block cap, element visits against the budget, and the simulation rate. It closes
with every changed setting and the shipped value beside it.

**The figures are read from the running grids rather than computed from the settings that produced
them**, and where a fleet has to become one number it is the worst grid rather than the mean: a world
is as starved as its most starved grid. They refresh about twice a second while the window is open,
and not at all while it is closed.

**Frame cost is reported only when telemetry is recording**, because the frame timer is wound in
telemetry's branch of the session update and nowhere else. With telemetry off the page says so rather
than showing a nought, which would be a reading of an instrument that never ran.

Settings changed from the shipped defaults are also dotted in front of their own label on their own
page, so the count on Statistics has somewhere to lead.

**Defaults** is the menu's one bulk action on a page of its own: a button that returns every world
setting to the value a fresh install ships. The four presentation switches are left alone: what is
drawn on a player's own screen is theirs. It is a page of its own rather than a paragraph on
Statistics so that the one irreversible action in the menu is somewhere a reader arrives
deliberately.

**Settings save themselves, and there is no per-control Reset.** A change applies to the running
session as you make it and reaches the config file about a second later, so a value you can see on
screen is the value the world has and the value it will still have after a reload. A menu that asks
you to confirm what you already did is asking you to do it twice, and a setting that reverts on
reload because a button was missed is worse than either. Starting over is the one case that needs an
action of its own, which is what Defaults is.

**A section is one tile, and a page is its sections.** The framework takes a control nowhere but a
tile — page, category, tile, control, with no accessor for anything else — so the tile cannot be got
rid of, but there is exactly one per section rather than a grid of them. Pages used to pack controls
three to a tile, two tiles to a row, and continue a section of more than six into a second headed
group, which put a lattice of boxes on every page and left the last one part empty.

The menu is generated from the same name list the chat commands and the mod API use, so a setting
added to the config file appears in it without anyone maintaining a second list. A setting the
menu's layout table does not describe still gets a control, under **Other**.

On a multiplayer client the simulation controls are visible but disabled, for the same reason
`set` is refused there: the config is world state and belongs to the server. The four presentation
switches stay editable, because they only change what that client draws.

## Every mechanism, and the rungs it has

**The intent is that a feature is configured as a list of options from `off` to `realistic`**, with
`realistic` the default and every rung between them a cheaper approximation carrying its measured
price — see
[document-of-intent.md](document-of-intent.md#a-switch-is-a-ladder-and-its-two-ends-are-off-and-realistic).
This is the inventory of how far the settings surface is from that, kept here because the gap is
per-feature and the answer to *should this one have a middle rung* is not the same twice.

A two-rung ladder is a complete ladder: some mechanisms have no cheaper form that is worth having,
and for those `off` and `realistic` are the whole list. **The column that matters is the last one** —
whether a cheaper rung is known to be possible and nobody has built it.

| Mechanism | Configured by | Rungs today | A cheaper rung |
| --- | --- | ---: | --- |
| Solar gain and shadow | `EnableSolarHeat`, `ShadowDetail`, `SolarOcclusionSamples`, `SolarOcclusionInterval`, `SolarTerrainRange` | 4 | **The one full ladder, and it is now a ladder rather than a scatter.** `ShadowDetail` runs `none` → `planets` → `the world` → `everything`, which is the shape this page is arguing for; it was eight settings, five of them switches whose sixteen combinations nobody had named. The unbuilt rung is *above* the top one — planet shadow per face, priced at 0.0018 K a metre in [backlog.md](backlog.md) `A9`. |
| Coolant transport | `EnableCoolantLoops`, `WellMixedCoolant` | 3 | off → well-mixed → parcels round a ring. Complete, and the middle rung reached no world until 2026-08-24. |
| Wind | `EnableWind` + `WindTerrainInfluence`, `WindSlopeStrength`, `WindDiurnalAmplitude` | 2 + dials | Each influence runs 0 to 1, so the ladder above the switch is a dial rather than a list. **The switch was missing until 2026-08-24**, which made this the one mechanism a world could not turn off (`C7`): the nearest thing was zeroing those three and knowing which three, and that removes the modulations rather than the wind. Off is no wind anywhere — the game exposes a ceiling and not a wind, so every direction and speed is this model's — and it costs nothing, because it takes the same path a planet with no air over it already takes. |
| Planet climate | `EnablePlanets`, `ClimateGroundInfluence`, `ClimateWeatherInfluence` | 2 + dials | Off means ambient is `VacuumTemperature` everywhere. The two influences are 0-to-1 dials on top of the switch rather than rungs under it. |
| Integration fidelity | `Frequency`, `MaxSubsteps`, `MaxSubstepsPerBlock`, `MaxElementVisitsPerStep` | scalars | Not a feature and the exception that proves the point: **its four dials run in three different directions.** `MaxSubstepsPerBlock` 0 is the *faithful* end and a number is the cheap one; `MaxSubsteps` is the opposite; `MaxElementVisitsPerStep` 0 means uncapped; `Frequency` up is dearer and more faithful. **Since 2026-08-28 the menu says so too**: `FidelityEnds` declares each dial's faithful end and every tip carries the same sentence, so a reader is told rather than having to learn four differently worded ones — and `FidelityEndTests` holds the table against the four claims in this cell. **Two of the four do not ship at their faithful end and that is deliberate**: `Frequency` at 4 is the quarter-second step every substep figure here is quoted on, and the step budget at four million is `G6` asking a grid to keep up with real time. |
| Conduction | `EnableConduction` | 2 | None known. Conduction between touching blocks is one multiply-add per link; there is no cheaper form of it that is still conduction. |
| *(group switch)* | `EnableEnvironment` | — | Not a mechanism and not a rung: it turns radiation and convection off together. A switch that removes two mechanisms at once is a convenience over their own switches, and it is listed here so the inventory accounts for every `Enable*` there is. |
| Radiation | `EnableRadiation` | 2 | None known, for the same reason. |
| Convection | `EnableConvection` | 2 | None known. |
| Waste heat | `EnableWasteHeat` | 2 | None known: it is a per-block fraction of a wattage the game already reports. |
| Point heat sources | `EnableHeatSources` | 2 | The inverse square is already cut off by range. A rung that sampled the registry less often is possible and has never been wanted, because the registry is usually empty. |
| Aerodynamic friction | `EnableFriction` | 2 + dials | `FrictionScale` and `FrictionAtSpeedsAbove` are balance dials rather than fidelity rungs — they change how much friction there is, not how well it is modelled. |
| Windward shielding | `EnableWindwardShielding` | 2 | On or off. There is no cheaper form that is still the mechanism — the pass is already sliced and already budgeted — and the rung below it is the unshielded model, which is what off means. |
| Room air | `EnableRoomAir` | 2 | **A cheaper rung is possible and unbuilt.** Room air is a well-mixed body already; what is expensive is the flood fill that finds the rooms, and a coarser or less frequent map is a rung. `D2` measures the fill at **3,934 ticks — about eleven minutes — on a million blocks** (re-measured 2026-08-28 by `bench scale --max 1000000`; the 7,207 this row carried predates the 2026-08-26 word skip and the 2026-08-27 span flood). **What the rung would buy is latency rather than CPU**: the mapper is budgeted per tick and capped at 4,096 cells, so convergence is the bounding volume divided by that cap and the performance passes moved the milliseconds without moving the wait. A *coarser* map shortens it; a *less frequent* one makes it worse, so only one of the two rungs this row names is the rung. |
| Heat pumps | `EnableHeatPumps` | 2 | None known. Off makes them ordinary blocks. |
| Overheat damage | `EnableDamage`, `DamageIsPerSecond` | 2 | `DamageIsPerSecond` is a correctness switch rather than a rung — off makes damage scale with `Frequency`, which is wrong rather than cheap. |
| The suit | `EnableSuitDamage` | 2 | None known. One character, one pass. |
| Temperature replication | `EnableTemperatureSync`, `TemperatureSyncInterval` | 2 + interval | The interval is a real rung and behaves like one: the cost is bandwidth and the price is measured in [Replicating temperatures](#replicating-temperatures). |
| Unattended grids | — | 1 | **The rung that does not exist at all.** [document-of-intent.md](document-of-intent.md#what-an-unattended-grid-gets) says full simulation is the default and a per-grid rate tier is a switch; the switch is unbuilt, so today there is one rung and it is `realistic`. |

**Read the count column with the last one.** Nine mechanisms have two rungs and only two of those are
places a cheaper rung is known to be possible — room air's map, and the unattended-grid tier that was
already intended. The rest have two rungs because two is all there is.

The rows that are gaps are tracked as [backlog.md](backlog.md) `B31`.

## Mechanisms

Each switch removes exactly its own mechanism and its own cost.

| Setting | Default | Effect |
| --- | --- | --- |
| `EnableEnvironment` | `true` | Master switch for radiation and convection. |
| `EnableConduction` | `true` | Heat flow between touching blocks. |
| `EnableRadiation` | `true` | Radiative exchange with the ambient sky. |
| `EnableConvection` | `true` | Convective exchange with the surrounding air. |
| `ShadowDetail` | 3 (everything) | How much work a shadow is worth, as one level rather than five switches. `0` nothing shadows anything: a face pointing at the sun is lit. `1` planets only — night, and a world's shadow seen from orbit; analytic, an angle against the planet's radius, no raycast, and nearly free. `2` the world: planets, terrain (ground-height lookups along the sun ray, only within 15 km of mean radius), asteroids (a physics raycast per candidate voxel per sample), and the ship shadowing itself (a walk from each cell toward the sun each time the sun moves more than 2°, sliced over ticks); another grid casts one whole-grid shadow. `3` everything: as `2`, and another grid's shadow lands on the faces it actually covers, for a walk through the occluder's blocks per face. **The five switches this replaced** (`SolarSelfShadowing`, `SolarOcclusionPlanets`, `SolarOcclusionTerrain`, `SolarOcclusionVoxels` and the three-way `SolarGridShadows`) had sixteen combinations, of which one was documented and none were tested; the levels are ordered by cost, so the answer to "this is too expensive" is the next one down. The solver still carries the switches and the world derives them from here. |
| `SolarTerrainRange` | 4000 m | How far along the sun ray the terrain walk looks. Near ground is what shadows you — the cliff two hundred metres off — and far ground almost never does, so this is short by design. |
| `SolarOcclusionSamples` | 1 | Points across the grid tested for shadow, 1..9. One is a single ray from the middle: the whole ship is lit or dark together, and flips the moment its centre crosses a shadow. More points spread through the hull turn that step into a ramp, at the cost of one full query each. |
| `EnableSolarHeat` | `true` | Solar gain and the sun occlusion raycast. |
| `EnableHeatSources` | `true` | Gain from point sources registered by other mods. |
| `EnableWasteHeat` | `true` | Heat from power production, power draw and thrust. |
| `EnablePlanets` | `true` | Planetary climate. Off means ambient is always `VacuumTemperature`. |
| `EnableFriction` | `true` | Aerodynamic heating at speed in atmosphere. |
| `EnableWind` | `true` | The wind field and everything that shapes it: the boundary-layer profile, terrain speed-up and shelter, slope channelling, the diurnal cycle and burial. Off is no wind anywhere rather than the game's wind unmodelled, because the game exposes a ceiling rather than a wind. A grid still feels its own motion through the air. |
| `EnableDamage` | `true` | Damage above a block's critical temperature. |
| `EnableCoolantLoops` | `true` | Coolant loop heat transport. |
| `WellMixedCoolant` | `false` | The cheaper transport rung: a loop's fluid as one well-mixed mass rather than one parcel per pipe travelling round the ring, so heat picked up at a sink reaches every other pipe in the same step instead of arriving as it flows. **It was reachable by nothing until 2026-08-31** — in the config file, copied into the core and read by the solver, but absent from `Names()`, so no menu page, no `/thermal set` and no API call could reach it and only hand-editing a world's XML would do. |
| `EnableRoomAir` | `true` | Sealed rooms hold an air mass that couples their surfaces. |
| `EnableHeatPumps` | `true` | Heat pumps move heat against a gradient for an electrical cost. Off makes them ordinary blocks. |
| `EnableSuitDamage` | `true` | Heat can hurt a player, not only a block. Off leaves the suit unsimulated and the pass does nothing. |

## Solver

`MaxElementVisitsPerStep` is the one to reach for when a very large grid stutters, and it is worth
understanding before changing it.

A step's cost is not its length: it is the number of substeps it takes times the number of links
on the grid. The substep count is set by the stiffest node, which moves as the grid heats — so a
large ship produced steps costing 15 ms most of the time and 70 ms occasionally, with nothing a
player could see or avoid changing between them.

When a step would exceed the budget, the step is made **shorter** rather than its substeps
coarser. Coarsening substeps would take steps too large for the grid's stiffness and lean on
`ClampOvershoot` to stay bounded, which loses accuracy. Shortening the step advances
less simulated time at exactly the same accuracy: heat moves more slowly, and nothing else about
it changes.

So this setting buys **smoothness with simulation rate**. Lower it and a large grid takes smaller,
more even steps and its heat evolves more slowly; raise it or set it to zero and it runs at full
rate with the spikes back. Grids below roughly a hundred thousand blocks never reach the default
and are unaffected either way. The telemetry report says what rate each grid is actually keeping.

### `Frequency` is not the cost dial it looks like

Worth understanding before tuning a busy world, because it works on some grids and not at all on
others.

A step is subdivided twice. `Frequency` cuts a simulated second into steps, and then the solver
cuts each step into as many **substeps** as it needs to stay numerically stable — enough that no
block's step is longer than half its own thermal time constant. Substeps advance no extra
simulated time; they are passes over the same interval, bought so the integration does not
diverge. A substep is what actually costs: one walk over every node and every link.

```
substeps a step needs = StepSeconds × max over blocks of (ΣG / C) / safety
                      = (1 / Frequency) × r_max / 0.5
```

and what you pay per real second is that times the number of steps:

```
substeps per real second = Frequency × (1 / Frequency) × r_max / 0.5
                         = r_max / 0.5
```

**`Frequency` cancels.** Doubling it halves what each step needs and runs twice as many. What sets
the bill is how much simulated time you asked for and how stiff the stiffest block is.

That holds while the estimate is above one substep. It stops holding at the two ends, and the ends
are where most grids live:

* **A soft grid** needs a fraction of a substep, and the solver still charges a whole one because
  it cannot run less. There `Frequency` *is* the cost, one pass per step, linear. Halving it halves
  the bill. This is a smaller population than it sounds: in the field dump only 37 of 189 stepping
  grids sat at one substep, and between them they held **182 cells** — they are debris, not ships.
  A 790-cell corvette needed five.
* **A grid at `MaxSubsteps`** is being refused what it asked for, so raising `Frequency` shortens
  the step until the estimate fits again. That is a real accuracy gain, and it costs.

Practically: **lowering `Frequency` saves on debris and does nothing on ships.** Measured across
the 189 stepping grids of the field dump, by the substeps each asked for:

| substeps | grids | cells between them |
| ---: | ---: | ---: |
| 1 | 37 | 182 |
| 2–4 | 77 | 2,021 |
| 5–6 | 72 | 46,605 |
| 11 | 3 | 126,157 |

Eighty per cent of grids, and better than ninety-nine per cent of the blocks, are above one
substep — which is the regime where `Frequency` cancels out of the bill entirely. That is what
`MaxSubstepsPerBlock` is for.

**Measured, not just derived.** `dotnet run --project Thermodynamics.Sim -- frequency` sweeps a
289-block grid with a real stiffness spread, `MaxSubsteps` high enough that the estimate is always
granted, warmed up and best-of-three:

| Freq | Substeps/step | Substeps/s | Demanded | ms/step | **ms/sim second** | Settled K |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1 | 14.00 | 14.0 | 13.4 | 0.0375 | **0.038** | 945.3 |
| 2 | 7.00 | 14.0 | 6.7 | 0.0193 | 0.039 | 945.3 |
| 3 | 5.00 | 15.0 | 4.5 | 0.0140 | 0.042 | 945.3 |
| 4 | 4.00 | 16.0 | 3.3 | 0.0111 | 0.044 | 945.3 |
| 6 | 3.00 | 18.0 | 2.2 | 0.0091 | 0.055 | 945.3 |
| 8 | 2.00 | 16.0 | 1.7 | 0.0065 | 0.052 | 945.3 |
| 12 | 2.00 | 24.0 | 1.1 | 0.0065 | 0.078 | 945.3 |
| 16 | 1.00 | 16.0 | 0.8 | 0.0040 | 0.064 | 945.3 |
| 24 | 1.00 | 24.0 | 0.6 | 0.0041 | 0.099 | 945.3 |
| 32 | 1.00 | 32.0 | 0.4 | 0.0039 | 0.126 | 945.3 |
| 60 | 1.00 | 60.0 | 0.2 | 0.0039 | 0.234 | 945.3 |

`substeps/s` sits at 14–16 from Frequency 1 to 8: that column is the grid's stiffness rather than a
setting. Past that it gets *worse*, because **substeps are an integer**. At Frequency 12 the estimate
asks for 1.1 and pays 2 — 24 substeps a second against a true demand of 13 — while at Frequency 16
it asks for 0.8, pays 1, and drops back to 16. **The curve is not monotonic**, and every frequency
whose demand lands just above an integer pays for a whole substep it does not need.

**It is not a propagation dial either.** Time for the source to reach 90% of its total rise is 18 s
at every frequency from 1 to 60, and the settled temperature is 945.3 K on all eleven rows. A
sixty-fold change in step rate moves neither the transient nor the equilibrium measurably — which is
what substepping is for: the integrated transfer over a second is the same however the second is
chopped up.

**The exception is when substeps are refused.** With `MaxSubsteps` at 1 the step is deliberately too
long and the overshoot clamp decides how much crosses — the most a substep can carry, by definition.
There each step moves a fixed maximum and more steps a second really does move more heat. That is
propagation bought by being wrong, and the shipped `MaxSubsteps` of 64 is what keeps this world out
of it.

**So what should it be?** Not 1, despite the table: `Frequency` is also how often damage lands, how
often the HUD moves, and how quickly a change is felt. What the sweep rules out is the idea that
raising it buys performance. **The shipped value is 4** — a quarter-second step, which is the basis
every substep figure in this documentation is quoted on, and four times a second of responsiveness.

Two things depend on it and move with it. Demand *per step* is proportional to step length, so this
rig asks 3.3 substeps at `Frequency 4` and about 6.7 at `Frequency 2` — enough to clip a small
`MaxSubsteps`, though not the shipped 64. And a step is spread across the frames of its own window,
so **halving `Frequency` halves the per-frame cost of a given step budget**; that is why
`MaxElementVisitsPerStep` doubled when `Frequency` went from 8 to 4.

### The approximation that shipped on, and no longer does

The defaults are the most faithful configuration the model has, and
`TheDefaultsAreTheMostFaithfulConfiguration` holds them to it. **`MaxSubsteps` was the one
exception, and `C24` closed it** — the section is kept rather than deleted because the reasoning is
what would be needed again if a future retune re-opened it.

**What the breach was.** `MaxSubsteps` grants 64. In vacuum nothing was close to it: a 49-ship panel
of real workshop hulls asked 7.1 substeps at p95. In thick air at 200 m/s the same panel's p99
demand was **73.4 and 14 of 50 hulls were refused**, every one at 1.14–1.15× over-subscribed —
a ceiling rather than a tail, because the stiffest block class is the same fitting on every ship and
its demand in air is set by the convection coefficient. Refusing that cost **0.028 K** on the hottest
block of a driven census hull over 600 simulated seconds, which is the trade `P14` exists to take.

**What closed it.** `C24` divides every heat capacity by 0.4 and multiplies conduction by four, and
the demand this criterion is decided by is convection-limited — so it came down with the clock. On
the same 40-hull panel in the same four environments, at the pair that now ships: worst p99 **35.12
of 64**, 55 % of the cap, **0 of 40 hulls refused in every environment**, and projected to the
300 m/s that servers commonly run, p95 38.37 — 60 %.

| environment | p50 | p95 | p99 | of cap | over cap |
| --- | ---: | ---: | ---: | ---: | ---: |
| vacuum, shadow | 8.12 | 11.36 | 12.51 | 20 % | 0/40 |
| planet surface, hot noon | 15.23 | 18.78 | 20.30 | 32 % | 0/40 |
| storm, parked | 20.99 | 30.09 | 30.53 | 48 % | 0/40 |
| re-entry, 200 m/s in thick air | 23.34 | 34.78 | 35.12 | 55 % | 0/40 |

**Vacuum is where it went instead**, and it is the one row above that rose: 1.60× what the same
hulls demanded before, because vacuum is all conduction. Nothing there is near the cap either — 20 %
at p99 — but the element-visit allowance is a different bound and the retune does reach it. **`C27`
measured that and found the premise the wrong way round**: the allowance binds in *air* first, at
about a third the grid size, and vacuum is the generous column. See
[What a shortened step costs](#what-a-shortened-step-costs).

**`MaxSubstepsPerBlock` is still an approximation and still not a default**, and the number that
made that an easy call has moved. It cost **0.607 K** on the worst-placed block, twenty times what
the ceiling's breach cost, so
[the fidelity rule](document-of-intent.md#fidelity-is-the-default-a-saving-is-a-switch) made it a
switch and the ceiling the model. Re-measured on 2026-08-24 at the pair `C24` ships and the hull
`C26` refreshed, a cap of 6 in thick air at 200 m/s costs **0.028 K** on the worst-placed block and
0.017 K on the hottest — the same size as the breach that was called imperceptible. **The decision
has not moved**, because moving a default is its own commit with its own reasoning (`E11`);
[backlog.md](backlog.md) `C3` carries it and now carries a number that argues the other way.

**And `G6` passes.** The criterion's marker is *p99 demand exceeds what the caps grant*, it was
written before the data, and it was failing on the configuration that shipped — which
[backlog.md](backlog.md) `C19` closed by keeping the cap and letting it fail. What moved is the
configuration rather than the criterion (`E11`, `P3`). See
[stiffness.md](stiffness.md#what-refusing-the-demand-costs) for what a refusal costs on each of the
three paths that carry heat, which is the question that outlives this one.

### What a shortened step costs

`MaxSubsteps` refuses a demand and the overshoot clamps carry the difference, which is the
approximation the section above is about. `MaxElementVisitsPerStep` does something else: it makes
the step **shorter** rather than coarser, so every step is exactly as faithful as it was and there
are fewer of them. Nothing is approximated. What is lost is *time* — the grid's thermal clock runs
slow against the world's.

**It is the largest thing the mod gives up**, and what it gives up is measured rather than argued:
**1.19 K standing at a 5 % deficit, rising to 36.98 K at 60 %**, on a load that keeps moving. The
ladder behind those two figures is
[benchmarks.md](benchmarks.md#what-the-rate-it-trades-away-is-worth), which is where `bench
allowance` runs and where the curve's convexity is the point; this page states what it costs and
that page states how it was measured.

Under a load that is *not* moving it is **0.00 K**, because two hulls heading to the same
equilibrium at different speeds agree once they arrive — so this is what a burn or a charging drive
costs and nothing at all is owed by a parked ship.

**Where the bound is reached is air, not vacuum.** A settled, driven census hull keeps all of real
time to about 32,000 blocks in vacuum, 16,000 on a planet surface and **9,000 in flight** at the
2,000,000 this setting carried until `C27` — air puts a convection term on every exposed node, so
the demand is three to four times the vacuum one and reaches the same ceiling at a third of the
size.

**So it moved to 4,000,000**, which is the whole of that trade and not a millisecond of anything
else:

* **What it buys.** A 32,800-block hull on a planet keeps 100 % of real time where it kept 52.7 %,
  the same hull in flight 72.5 % against 36.2 %, and a 16,558-block hull in flight stops being
  touched at all.
* **What it costs.** A frame is bounded at 266,667 element visits instead of 133,333 — about
  +0.12 ms a frame on the harness for a grid that is actually reaching the bound, and **nothing at
  all for one that is not** (`P8`). On the 8,142-ship corpus, seven ships in eight are under 8,904
  blocks and never reach it in any world.
* **Why that is the right way round.** The mod refuses to ship `MaxSubstepsPerBlock 6` as a default
  because it costs 0.607 K, and accepts the substep ceiling's breach because it costs 0.028 K. This
  cost more than sixty times the first, as a default, and had never been put beside them.

**What is not measured is the game-side figure**, and it matters (`P2`). The per-frame costs above
are harness milliseconds; the runtime is 6–8× slower per element visit, which puts a throttled
grid's frame at roughly 1.7–2.2 ms in game against 0.84–1.12 ms before. The only in-session
measurement of this bound predates both the frame-spreading and the pass removal that made a visit
nine times cheaper, so there is no current one. A world that cannot afford it lowers the setting,
which is what it is for.

### Solving a fleet in parallel

`ParallelGrids` is off, and it is the one setting in this file whose default is a **gap rather than
a choice**.

**What it does.** A frame's grids are prepared on the game thread, solved on the engine's own
worker threads, and applied on the game thread — solve in parallel, apply on the game thread, which
is the shape `MyAPIGateway.Parallel` is built for and the one the solver's invariants allow: order
independence is one of the three, so a fleet stepped one grid per work item lands on the same
numbers as a fleet stepped in order. What may not move off the game thread is anything that reads
or writes the game, and that is exactly what the two halves either side of the solve are: the world
sample, the pump state, the damage, the block writes and every shared telemetry total.

**What it is worth, measured before it was built.** A 242-grid fleet of 1,004-node grids:

| threads | fleet | one grid | uneven fleet |
| --- | ---: | ---: | ---: |
| 32 | **10.17×** | 0.99× | 3.35× |
| 8 | **7.09×** | — | — |

The hand-off costs 1.6–6.8 µs against a grid's own 0.54 ms, so a single-grid world pays nothing
measurable and a fleet is bounded by its largest grid — which is why an uneven fleet gives 3.35×
and why splitting *one* grid is a separate question rather than a substitute.

**What a session has to answer before it ships on**, none of which a harness can:

* The engine's own scheduler is not the framework's. `MyAPIGateway.Parallel` hands work to the
  game's pool, which is also running the game.
* How many threads a mod may take on a machine it shares with the thing it is running inside.
* Whether an exception on a worker reaches a log the way one on the game thread does. The mod holds
  it and reports it on the game thread for that reason, and that path has never run in a session.

Until then it is a switch a server operator can turn on, and
[backlog.md](backlog.md) `D19` carries what closing it needs.

### `MaxSubstepsPerBlock`

A step is divided into as many substeps as the **stiffest** block on the grid needs, and every
other block pays for all of them. On a real ship that stiffest block is almost never anything
interesting: on a 42,051-block capital ship, forty-three sixteen-kilogram light fittings asked for
twenty-eight substeps while the five hundred kilogram armour around them asked for one, and the ship
ran at 35 % of real time to pay for the lights.

Physically, a 32 J/K fitting bolted to armour reaches the armour's temperature in about eighteen
milliseconds. At a quarter-second step it is not an independent temperature at all — it is a
reading off the block it is bolted to. This setting says so: any block that would demand more than
N substeps has its heat capacity raised to the least that keeps it inside N. "Demand" counts
everything the stability estimate does — conduction to neighbours, coolant loops, room air, and
the linearised radiation and convection with the sky — so the cap means what it says: set it to
one and the grid takes one substep.

What it costs is **that block's own transient, and only that block's**. It warms and cools more
slowly than a sixteen kilogram object would. It ends up at the same temperature, because where
something settles is decided by the watts cancelling and has nothing to do with heat capacity, so
the difference decays as the grid settles rather than accumulating. Everything the block is bolted
to is untouched, and the block's real heat capacity is still what the terminal, the overlay and
the mod API report.

**A cap is a statement about a node's time constant against the step, not a number of substeps**, so
it cannot be quoted without a `Frequency`: a cap of N at `Frequency` 8 reaches the same blocks as a
cap of 2N at `Frequency` 4. **The value is chosen by how many blocks it reaches, not by the error** —
the error stays far below anything a player can see across the whole useful range, while the
population the cap reaches moves suddenly once it stops being a handful of fittings and starts being
ordinary armour. [stiffness.md](stiffness.md#1-a-per-block-substep-cap--the-one-that-is-built)
carries the sweep at both shipped step lengths, driven and diffusing, and the population argument
that puts the knee in 2–4.

Hence a rule worth remembering: **while `MaxSubstepsPerBlock <= MaxSubsteps` the overshoot clamps
never engage**, and every step is genuinely short enough for the grid it is integrating. Raising one
without the other starts refusing steps instead.

**It is off by default, and since 2026-08-25 that is a measurement rather than a principle.**
`CorpusCapWalk` ran all 8,144 published blueprints through four scenarios twice, with the cap off
and at 6, and the answer is in the shape of the trade rather than in the size of the error: a cap of
6 costs a p99 of **0.2820 K** across the population — under the 0.607 K that had kept it out — but
**stiffness is a property of a block and this allowance is a property of a grid**. A light fitting
demands the same substeps on a fighter as on a dreadnought, so the cap re-masses **7.4 % of the
blocks on hulls under a thousand and 3.1 % on hulls over sixty thousand**, while **no run under
5,000 blocks is over `MaxElementVisitsPerStep` at all**. Four fifths of the ships people publish
would pay the error and collect none of the throughput. See
[balance-lab.md](balance-lab.md#the-judgement-argued-in-the-open).

It is also an approximation, and a mod that models heat should not make
one on a player's behalf without being asked. On a world with large ships in it, turning it on is
the single largest thing that can be done for frame time — and unlike `MaxElementVisitsPerStep`, it
buys the throughput back rather than trading it away: a ship that stops needing more substeps than
the visit budget allows stops being throttled at all.

**It does not have to be guessed.** A telemetry dump taken with the cap off reports, per grid and
for the world, exactly what each cap would do to the substep count and how many blocks it would
raise — see [telemetry.md](telemetry.md#substeps). The projection is the same arithmetic the
setting uses, and `SubstepFloorTests` asserts the two agree, so one baseline dump answers the
question for that world without running the experiment.

### `FloorBlocksWhenOverBudget`

**A grid that cannot afford its demand has to spend less, and there are two ways.** Today it
shortens its step: `MaxElementVisitsPerStep` bounds a step's work by making the step cover less
simulated time, so the grid advances slower than real time and its whole thermal clock runs behind
for as long as the load lasts. That is not an approximation with a price on one block — it is the
entire simulation running slow, and nothing bounds how slow.

**The other way is to lower the demand.** Flooring the stiffest blocks to what the budget grants
leaves the step whole and charges an error on the blocks it re-masses. Measured on one hull at the
shipped allowance ([backlog.md](backlog.md) `C30`):

| hull, in flight | demand | granted | clock | what that stands at |
| --- | ---: | ---: | ---: | ---: |
| 32,800 blocks, step shortened | 27.49 | 20 | 72.5 % | 9.08 K |
| 32,800 blocks, blocks floored | 6.00 | 20 | **100 %** | the cap's 0.024 K |
| 64,463 blocks, step shortened | 27.49 | 10 | 36.2 % | 36.98 K, past the ladder |
| 64,463 blocks, blocks floored | 6.00 | 10 | **100 %** | the cap's 0.024 K |

**Nine to thirty-seven kelvin against twenty-four thousandths of one.** The kelvin price of a lost
clock is read off a measured ladder run under a *moving* load, which is the only place a clock error
shows at all, so it is an upper bound for a ship whose load keeps changing and says nothing about a
settled one.

**It is not `MaxSubstepsPerBlock` under another name**, and the difference is the whole of why
[backlog.md](backlog.md) `C3` refused that one as a default. `MaxSubstepsPerBlock` is a world
setting and reaches every hull; measured over all 8,144 published blueprints, **no run under 5,000
blocks is over the allowance at all**, so four fifths of the ships people build would pay its error
and collect none of the throughput. This engages per grid and per step, only where the budget binds,
and the cap it applies is exactly what that grid can afford rather than a number chosen in advance.
The two compose: with both on, the tighter cap wins.

**Off by default, and now measured on a population rather than argued from one hull.**
`CorpusFloorWalk` walked all 294 published ships the allowance binds on — 291 produced paired cells,
1,164 of them — against a decision rule fixed before the data: p99 Δpeak at or under 0.03 K ships it
on, at or over 0.6 K keeps it off.

**It came in at 27.76 K, with a maximum of 48.21 K.** So it stays off. Two of the four predictions
held perfectly — no run lost simulated time, nothing was ever floored in the control arm, and the
floor never made a grid stiffer — and the two that failed are the two that decide it: it reaches
**13.05 %** of blocks where a fixed cap of 6 reached 5.83 %, and it costs three orders of magnitude
more than predicted.

**The median cell is free and the tail is ruinous**, 0.006 K against a p99 of 27.76 K, and the tail
is not predictable from how far the floor has to lift — cells barely over budget still reach a p99
of 32 K. Nothing gates it into safety without gating it out of usefulness: every filter tried either
misses the 0.6 K threshold or keeps under 14 % of the cells the mechanism engages on. The figures
are in [balance-lab.md](balance-lab.md#what-it-did-the-floor-is-safe-exact-about-the-clock-and-far-too-expensive-to-default),
and they describe the ships the allowance binds on and no others.

The switch stays, because a world that wants its clock more than its accuracy can still have it.

## Changing settings from a client

Every setting except the four presentation switches is world state, owned by the server. A client
at **space master** or above can change one anyway: the settings menu and `/thermal set` send the
change to the server as a request, the server checks the asker's promote level and applies it, and
the result comes back as a chat line. An accepted change then replicates to everyone as part of the
ordinary settings sync, so the value moving is its own confirmation.

A player below that level is refused, and told so — silence would be indistinguishable from a lost
packet.

**The request travels on its own channel, not the one the rest of the mod uses.** SENetworkAPI
registers the game's non-secure message handler, where the sender's id is a field the *sender*
wrote; its own documentation says not to gate admin actions on it. `SettingsRequests` uses
`RegisterSecureMessageHandler`, where the transport supplies the sender and a from-the-server flag
that a client cannot forge. That is the whole reason for the separate channel.

## The definition files, from the menu

`Loops.xml` and `Planets.xml` were settings nobody could reach in game: "how fast does coolant
move" is a settings question whose answer lived in a file the settings menu had never mentioned.
Their nineteen values are world settings now — saved, replicated, reachable from `/thermal set` and
the mod API, and laid out on the **Coolant loops**, **Climate** and **Underground** pages.

They behave as an override rather than a copy. A value still equal to what a fresh install ships
leaves the definition alone, so an untouched world reads whatever `Loops.xml` and `Planets.xml`
say. Move one and it wins from then on, for that world.

`Cubes.xml` is not here: its properties are per block subtype, which a flat setting cannot
express. Edit the file itself.

## Checking a client has the server's settings

Only the server reads the config file. A client is sent the world's settings when it joins and
whenever one changes, and until that arrived it would be simulating the shipped defaults — the same
physics inputs producing different temperatures on the two machines, with nothing on screen to say
so.

`/thermal sync` prints a digest over the 44 replicated settings, plus the five figures most likely
to differ. Run it on the server and on a client: **the digests must match**. If they do not,
`/thermal sync fetch` asks again, and running the first command a second time says whether that
worked.

The four presentation switches — the debug text, the two raycast overlays and the block overlay —
are deliberately outside the digest. A client owns what is drawn on its own screen, so those are
allowed to differ and a server does not overwrite them.

## Replicating temperatures

Settings replicate. **Temperatures now do too**, and until this existed they did not: a client
re-simulated from its own inputs, and a client joining mid-session started from whatever the world
was last *saved* at. The model is dissipative, so a client converges on its own — but it spends 150
to 305 s on the wrong side of a block's critical temperature while it does, against a damage event
that runs a median 37 s from the load to the first block lost. A client could show **safe** for the
entire lifetime of the event that destroyed the block, several times over.

**The protocol is the whole hull once, then the blocks near failing.** When a client has finished
building a grid it asks the server to state it; the server answers with every block's temperature,
in as many messages as the hull needs. After that the server states only the blocks inside the
warning band — the same last 100 K the glow already draws — every `TemperatureSyncInterval`
seconds, and says nothing at all about a hull with nothing near failing.

Both halves are load-bearing, and that is measured rather than assumed. Correcting the band alone
leaves a client misreading for 145 s of a 560 s run *whatever the interval*, because a corrected
block conducts to neighbours that are still stale; the hull once and then the band takes it to
nothing, for one packet — 94 KB on a 9,430-block hull — and 513 bytes a second after it. Replicating
every block continuously does the same job for thirty times the bandwidth. The evidence is in
[known-issues.md](known-issues.md#the-protocol-is-measured-and-the-near-critical-tail-alone-is-not-it).

| Setting | Default | Effect |
| --- | --- | --- |
| `EnableTemperatureSync` | `true` | Whether the server states block temperatures to its clients at all. Off is what the mod did before this existed: every client guessing, and able to show a block safe for the whole time it is burning. |
| `TemperatureSyncInterval` | 5 s | Seconds between band updates once a client has the hull. The whole hull is stated once whatever this says. Sixty is enough to keep a client right about a *stale join*, which decays; five is chosen for a client whose **inputs** are wrong, which does not decay and re-diverges between updates. |

**The cost is the size of the emergency.** A quiet ship sends nothing — an empty band is not
transmitted. A burning one sends ten bytes per block near failing per interval per client in range,
which on the hottest hull in the corpus is about 6 KB/s while it burns. Blocks outside a client's
sync distance are never sent, because there is no grid on that machine to correct.

**It travels on its own secure channel**, two above the shared one, for the reason
[Changing settings from a client](#changing-settings-from-a-client) gives: the engine's secure
handler supplies a sender the transport verified and a from-the-server flag a client cannot forge.
A client must not be able to write temperatures onto another client's simulation, and the server
must not serve a hull to a player id somebody else named. A snapshot asked for twice inside five
seconds is refused, so a client asking in a loop cannot make the server transmit a hull per frame.

`/thermal sync` prints what this has sent, asked for and applied on the machine it is run on. Run it
on both: registration and addressing cannot be tested outside a session, and the pair of counters is
what says which half is not moving.

**What is still wrong after it.** The correction overwrites this machine's guess with the server's
answer, so it fixes a client that started from the wrong state. It does not fix a client whose
*inputs* are wrong — block power arriving late, a dropped solver backlog, settings that never landed
— because those re-diverge as soon as the packet is applied. Against the measured combined case the
correction more than halves the standing error and does not remove it. Those are tracked separately;
see [backlog.md](backlog.md) `F17` to `F23`.

## What the dials trade against each other

There is one configuration and no presets, so tuning a world is moving individual settings — and two
relationships bound what any move can buy. Neither can be tuned around.

**Heat spreads as the square root of the arithmetic you spend on it.** Diffusion is a square-root
process: a front crosses blocks at a rate proportional to `sqrt(substeps per second)`, while cost is
proportional to substeps per second outright. Measured, holding everything else: 1 substep/s gave
0.5 blocks/s, 4 gave 1.0, 8 gave 1.4, 16 gave 2.0 — square root to two figures. **Doubling how
responsive a world feels costs four times as much.**

**But where you spend the substeps changes the exchange rate by about three times.** There are two
ways to move more heat per second. The accurate one raises `HeatTimeScale` and grants the extra
substeps its stiffness demands. The approximate one raises `HeatTimeScale` *and refuses* the substeps
with `MaxSubsteps`, letting the overshoot clamps decide how much crosses — which is the most a
substep can carry, by definition. Measured: the clamped route delivered 0.125 blocks/s per substep/s
against 0.045 for the accurate one. **The shipped configuration takes the accurate route almost
everywhere**, which is what `MaxSubsteps 64` is for — but not quite everywhere: in thick air at
200 m/s about a fifth of a real population demands 73.4 and is refused. What that refusal costs has
been measured and it is **0.028 K** on the hottest block over 600 simulated seconds, because 1.15×
over-subscribed is the free end of that trade. See
[stiffness.md](stiffness.md#what-refusing-the-demand-costs).

**The far end is a wall, not a slope.** Past roughly `HeatTimeScale / Frequency = 4000` the clamps
are carrying the entire step and blocks start being driven to the ambient floor. The shipped ratio is
56. `Validate()` warns above 4,000, and the settings menu shows it.

So, knob by knob:

* **`Frequency` sets responsiveness and cost together — but only while `MaxSubsteps` is 1**, or the
  grid is soft enough that the estimate never rises above one substep. On a grid stiff enough to ask
  for real substeps it cancels out of the cost entirely; reach for `MaxSubstepsPerBlock` there
  instead. See [above](#frequency-is-not-the-cost-dial-it-looks-like).
* **`HeatTimeScale` sets how much a substep carries.** Raise it until the clamps engage; past that it
  buys nothing, because the clamp is already moving all it can.
* **`MaxSubsteps` chooses accuracy or speed.** High means the estimate is always granted and nothing
  clamps, which is what ships. `1` means every step is deliberately too long and the clamps carry it.
* **Keep `HeatTimeScale / Frequency` under 4000.** This is the safety rail. Everything else is taste.
* **`EnableRoomAir` and the ship shadowing itself are the two mechanisms that cost most** for what a
  player notices, and both ship on — the second at `ShadowDetail` 2 and above.

`ClampOvershoot` must stay on for any of this. It is what makes a deliberately-too-long step bounded
instead of divergent — with it off, a fast clock at one substep reaches 10^22 K in twenty seconds.

**Starting over is one action.** The settings menu's **Defaults** page carries a button that
returns every world setting to the value a fresh install ships, leaving the four presentation
switches alone. There is no per-control reset and no Save button, because a change applies as it is
made and reaches the config file a second later.

## Trading step rate for heat transfer

A natural idea, and the measurement that retired a setting. The world used to carry a
`SimulationSpeed` multiplier beside `Frequency`, and the obvious trade was to halve it and double
`HeatTimeScale`: half as many steps each second, heat moving twice as fast in each, and a ship
cooling at the same rate a player watching it would see.

**The pace half is exactly true.** `HeatTimeScale` divides every heat capacity, which is precisely
equivalent to running the clock faster, so any pairing with the same
`SimulationSpeed x HeatTimeScale` produces the same temperatures against the wall clock —
measured at 0.02 % apart across quarter speed, half speed and double speed
(`PaceEquivalenceTests`).

**The saving half mostly is not.** A step costs its substeps, and the substeps a grid needs are
proportional to the step length times the stiffness — which is what `HeatTimeScale` is. Buying
half the steps with twice the stiffness leaves the substeps roughly where they were. Measured on a
hull, quartering the speed and quadrupling the transfer took the substep count from 72 to 60.

What saving there is comes from somewhere else: **fewer, longer steps**, which amortise the passes
that run once per step whatever its length — mirroring node state, estimating the substep count,
publishing the result. That is worth having, and there is a simpler way to ask for it.

*Taken at the clock that shipped when the sweep was run, which was 225; it is 90 now, and what the
table is about is the relationship between the three dials rather than the value of one of them.*

| speed | heatScale | freq | steps | substeps | ms / real s | cooled |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1.00 | 225 | 4 | 80 | 240 | 75.6 | 46.37 K |
| 0.50 | 450 | 4 | 40 | 200 | 58.7 | 46.44 K |
| 0.25 | 900 | 4 | 20 | 200 | 57.0 | 46.56 K |
| **1.00** | **225** | **1** | 20 | 200 | **57.5** | **46.56 K** |
| 1.00 | 225 | 16 | 320 | 320 | 106.2 | 46.30 K |

*127,000 blocks, 20 real seconds, work budget off so the effect is not hidden. `bench pace`.*

**Lowering `Frequency` alone reaches the same place** — the fourth row is the third row's cost and
the third row's answer, with the multiplier and `HeatTimeScale` left alone. It is one knob instead
of two and it does not move the world's clock, which is why `SimulationSpeed` is no longer a world
setting: it only ever multiplied `Frequency`, and `Frequency` says the same thing in units this page
can price. The solver still carries it, and the pace labs still sweep it. Going the other way costs:
`Frequency 16` is nearly twice `Frequency 1` for a result 0.5 % different.

Two things to watch when lowering it. A longer step needs more substeps, so a stiff grid can reach
`MaxSubsteps` and start clamping — the report's **steps clamped by substep cap** is where that
shows, and it should stay at zero. And on a grid large enough for `MaxElementVisitsPerStep` to bind,
that budget is already shortening steps and is the constraint that matters; lowering `Frequency`
will not move it much.

| Setting | Default | Effect |
| --- | --- | --- |
| `Frequency` | 4 | Solver steps per simulated second. The integration step is `1/Frequency`, so 4 is a quarter-second step — the basis every substep figure in this documentation is quoted on. Whether lowering it cuts cost depends on the grid — see below. |
| `HeatTimeScale` | 90 | How much faster than real physics heat moves. Divides every heat capacity. It was 225 until `C24`, which moved it to put the most significant thermal event inside the 2–5 minute window `G8` asks for — see [balance.md](balance.md#the-route-is-chosen-and-it-is-the-one-the-cost-column-argued-against). |
| `MaxElementVisitsPerStep` | 4000000 | Most element visits one step may make — substeps times its links plus four times its nodes — before the step is shortened to fit. 0 removes the bound. **Moves with `Frequency`**: a step is spread across the frames of its window, so this figure and the step rate together set the per-frame cost — at `Frequency` 4 it bounds a frame at 266,667 visits. It was 2,000,000 until `C27` priced what a shortened step costs; see [What a shortened step costs](#what-a-shortened-step-costs). |
| `MaxSubstepsPerBlock` | 0 (off) | Most substeps any single block may demand of the whole grid before it is treated as heavier than it is. The cheapest large win there is on a real ship. See below. |
| `FloorBlocksWhenOverBudget` | `false` | When a grid cannot afford the substeps its demand asks for, floor its stiffest blocks to what `MaxElementVisitsPerStep` grants instead of shortening its step. Engages per grid and per step, only where the budget binds. See below. |
| `ParallelGrids` | `false` | Solve a frame's grids on the engine's worker threads rather than one after another on the game thread. Measured at **10.17×** on a 242-grid fleet and 0.99× on a single grid. **Ships off**, and what a session has to answer first is [Solving a fleet in parallel](#solving-a-fleet-in-parallel). |
| `MaxSubsteps` | 64 | Most substeps one step may be cut into, whatever the grid asks for. A grid refused here integrates a step too long for its stiffest block, and the overshoot clamps carry the difference. **It bound in thick air at flying speed until `C24`, and no measured hull reaches it now** — see [The approximation that shipped on](#the-approximation-that-shipped-on-and-no-longer-does). |
| `ClampOvershoot` | `true` | Stops a substep carrying a node past what it is exchanging with: each conduction exchange is capped at the energy that equalises the pair, and radiation and convection may not carry a block past ambient. It is what bounds a step that is deliberately far too long; off reproduces the original unbounded solver. Skipped, at no change to the result, on any step short enough that nothing can overshoot — see [benchmarks.md](benchmarks.md#the-overshoot-clamp-ab). **One switch where there were two**: `ClampConductionOvershoot` and `ClampEnvironmentOvershoot` were the same decision asked twice, both shipped on and both documented "leave on". The solver still clamps in two places and this sets both. |
| `DamageIsPerSecond` | `true` | Overheat damage per second of simulated time. Off applies it per step, which makes damage scale with `Frequency`. |

## Environment

| Setting | Default | Effect |
| --- | --- | --- |
| `ClimateGroundInfluence` | 1.0 | How much the ground a grid is parked on shifts the air above it, 0..1. At 1, snow is about 14 K colder than the planet's own figure with a flatter day, and sand about 8 K warmer with nearly twice the swing. At 0 the ground is ignored. |
| `ClimateWeatherInfluence` | 1.0 | How much the weather standing over a grid changes the air around it, 0..1. At 1 the game's own authored figures apply in full — a heavy snowstorm about 18 K colder with a tenth of the sun and twice the wind, a sandstorm 12 K warmer. At 0 the weather affects nothing but the wind, which is what it did before. |
| `VacuumTemperature` | 2.7 K | Ambient in space, and the floor for planetary ambient. |
| `SolarEnergy` | 1000 W/m² | Solar irradiance above the atmosphere. |
| `FrictionAtSpeedsAbove` | 0 m/s | Relative airspeed below which friction heating is off. **Zero since 2026-09-02**: the v³ law makes low-speed friction vanish on its own (~1 W/m² at 10 m/s), so the old 50 m/s guard bought nothing but a step in the heat and the force at the speed it named, and it kept lift and drag from existing below it. The cooling-to-heating crossover the guard imitated is emergent — convection removes `h·A·(T−T_ambient)` growing with √v while friction adds ∝ v³ — and depends on how hot the hull is; the aero debug view reports it live. Set above zero to restore the legacy cut. Worlds that saved a config while the default was 50 keep their saved 50 until they change it. |
| `FrictionScale` | 0.001 | Coefficient on the v³ friction term. |
| `EnableWindwardShielding` | 0 | A block behind another is sheltered from the wind, for heat — the sun's self-shadowing pass aimed at the relative wind. **Off**: it is a second sliced pass and 3 MB on a large hull, and unshielded is the conservative answer, so a world without it is heated and dragged at least as much as it should be. Its rebuild threshold is 20° rather than the sun's 2°, because the wind direction is grid-local and moves when the *ship* turns. |
| `RoomConvectionCoefficient` | 8 W/(m²·K) | Coupling between a room's air and the surfaces facing it. Lower than the planetary figure because room air is still. **It carries no pressure term**, so a compartment at a fiftieth of an atmosphere couples its walls as hard as a full one and pressure decides only whether the coupling exists — see [thermal-model.md](thermal-model.md#room-air). |
| `RoomAirDensity` | 1.225 kg/m³ | Air density in a fully pressurised room. |
| `SolarOcclusionInterval` | 12 | Solver steps between solar occlusion raycasts. The raycast is the most expensive thing a grid does and the sun moves slowly. |

## Coolant loops

These ship in [`Loops.xml`](../Thermodynamics/Content/Data/Loops.xml), which the settings menu never showed — so "how fast
does coolant move" was a settings question whose answer lived in a file nobody could reach from the
game. They are world settings: saved, replicated and editable like any other.

**A value left at its shipped figure does not override the definition.** The file still decides.
Move one and it wins from then on, across every loop definition in the world.

| Setting | Default | Effect |
| --- | --- | --- |
| `LoopCoolantKilogramsPerCubicMetre` | 33 kg/m³ | Coolant per cubic metre of the cell a pipe occupies — 515.6 kg a pipe on a large grid and 4.1 kg on a small one. More is more capacity for the same coupling: a heavier ring takes longer to saturate and longer to shed, and swings less between its sink face and the far side of the loop. **A density rather than a flat mass because a flat one has no grid size in it**: the 50 kg this shipped until `C43` is 3.2 kg/m³ in a 2.5 m cube and 400 kg/m³ in a 0.5 m one, where it outweighs the pipe block carrying it. |
| `LoopRefillEquivalentKelvin` | 100 K | The excess a refill is priced at. Restoring a kilogram costs the heat that kilogram holds this far above ambient, and a pump wastes **all** of what it draws, so the energy lands back in the ship — which makes this **the excess at which venting and refilling exactly break even**. Above it a vent pays, below it costs. 100 K is where the glow starts. |
| `LoopRefillKilogramsPerSecond` | 5 kg/s | How fast a vented ring comes back. Venting is instant and refilling is not, and that is what stops a dump being repeatable: a full eight-pipe large-grid ring is 80 s. The one figure here with no derivation under it. |
| `LoopSpecificHeat` | 3400 J/(kg·K) | The coolant's specific heat. Water-glycol is about 3,400. |
| `LoopHeatTransferCoefficient` | 1000 | How well heat crosses between the fluid and the wall it touches **while the ring is circulating**, W/(m²·K). Convective, so there is no thickness in it. A few hundred is a slow liquid flow and a few thousand a fast one; `C42` moved it from 160 because at 160 one sink face forced a 6.4 MW block to sit 6,400 K above its surroundings. A stopped ring still carries 160, from `LoopStagnantTransferFraction`. |
| `LoopContactMultiplier` | 1.0 | Scales the coupling between the coolant and the metal it touches, at the pipe wall and at a sink face alike — the dial that decides whether plumbing beats bolting. **One trim where there were two**: a pipe multiplier and a sink multiplier were two dials on one coefficient, both shipped at 1. A fluid that should behave differently at the two joints says so in `Loops.xml`, which still carries them apart. |
| `LoopFlowRate` | 10 m/s | How fast coolant moves with one pump at full speed. Flow costs no substeps — carrying the fluid is a rotation of which parcel sits in which pipe, exact at any speed — so this is free to be set for feel. **One rate where there were two**: the large- and small-grid rates shipped identical and were never moved apart, and `Loops.xml` still carries both. |
| `LoopStagnantTransferFraction` | 1.0 | What a stopped ring still carries across the fluid-to-wall joint, 0..1. Fluid-to-wall transfer is convective, so it depends on the flow: a pumped ring is forced convection and a stopped one is natural convection against the same wall. 0 makes a pump failure total. |
| `WellMixedCoolant` | `false` | The cheap rung of coolant transport. Off — the default — is the realistic form: the fluid is a ring of parcels, so a stopped pump leaves the coolant cold at the radiator and hot at the reactor, and where a sink sits round the loop matters. On collapses the ring to one temperature, which is cheaper and makes a loop's layout stop mattering. **It existed in the solver and reached no world until 2026-08-24**: the model read it, the tests exercised it, and nothing a player could touch set it. |

## Planet climate

These ship in [`Planets.xml`](../Thermodynamics/Content/Data/Planets.xml) and follow the same rule: one entry ships, so
these address it, and a world with several authored planet types still reads them from the file
until a value is moved off its shipped figure. See [environment.md](environment.md) for the
model they parameterise.

| Setting | Default | Effect |
| --- | --- | --- |
| `PlanetDayTemperature` | 294.261 K | Equatorial daytime air. |
| `PlanetNightTemperature` | 283.15 K | Equatorial night air. |
| `PlanetPoleTemperatureDrop` | 40 K | Span from equator to pole, interpolated on cos(latitude). |
| `PlanetAmbientLapseRate` | 4 K/km | How fast the air cools with altitude. |
| `PlanetAmbientLagSeconds` | 45 s | First-order lag on the ambient target, which is what makes the day peak after noon. **The fallback**: the lag is a share of this world's own day wherever the day has been measured, and this is what runs until it has been. See [The lag is a share of the day](environment.md#the-lag-is-a-share-of-the-day). |
| `PlanetConvectionCoefficient` | 50 W/(m²·K) | Convective coupling in full atmosphere, scaled down with air density and up with wind. |
| `PlanetUndergroundConvectionCoefficient` | 2 W/(m²·K) | The same for a grid buried in rock, which is a far worse heat sink than moving air: `2k/D` for rock at 2.5 W/(m·K) over a 2.5 m block. Crossed over the first five metres of burial, and neither wind nor weather multiplies it. |
| `PlanetSolarDecay` | 0.5 | How fast sunlight is attenuated through the atmosphere. |
| `PlanetUndergroundTemperature` | 280 K | The rock's own temperature below the damping depth and above the deadzone. |
| `PlanetUndergroundDampingDepth` | 20 m | Depth over which the day's swing, the weather and the ground table all damp out. Below it, ambient is simply `PlanetUndergroundTemperature`. |
| `PlanetSealevelDeadzone` | 2000 m | Depth below which the rock starts warming toward the core. Deeper than SE's voxels reach, so every reachable depth currently reads flat. |
| `PlanetCoreTemperature` | 3000 K | Temperature at the planet's centre, approached linearly from the deadzone. |

## Heat pumps

The two ratings a heat pump has — how much heat it can move and how much electricity it can draw —
belong to the block and live in
[ThermalHeatPumpShapes](../Thermodynamics/Game/ThermalHeatPumpShapes.cs). What is
tunable here is the physics between them, which is the same for every pump in the world.

| Setting | Default | Effect |
| --- | --- | --- |
| `HeatPumpCarnotFraction` | 0.4 | How much of the Carnot limit a pump achieves, 0..1. A real domestic heat pump manages about 0.4; 1 would be a thermodynamically perfect machine. This one number is the whole balance of the block. |
| `HeatPumpMaxCoefficient` | 8 | Ceiling on the coefficient of performance. Carnot's figure runs to infinity as the two sides converge, and a real machine is limited by its compressor long before that. |

Raising the fraction does not change what a pump can do — the shape of the cost curve is Carnot's
and is not negotiable — only how far up it the block sits. Lowering the ceiling makes pumps
predictable near equilibrium at the cost of making cheap, small-gap cooling less rewarding.

## The suit

A player in a burning compartment is the one place heat would otherwise stop being consequential. These
settings are the suit that decides otherwise, and they describe a machine rather than a threshold:
the occupant is held at body temperature by a cooler, so a hot room is survivable while the cooler
keeps up and lethal past the point where it does not.

| Setting | Default | Effect |
| --- | --- | --- |
| `SuitConductance` | 2.5 W/K | How well the room reaches the occupant through a sealed suit. With the rating below, this is what sets the hottest room a player can stand in indefinitely. |
| `SuitHeatCapacity` | 240,000 J/K | The occupant and the suit together — about eighty kilograms of mostly water. Divided by `HeatTimeScale` like every block's, so a player heats on the same clock as the ship. It is the whole of how long a dash through a hot room can be. |
| `SuitCoolingWatts` | 500 W | Heat the suit can move, either way. A real EMU's sublimator handles about this. |
| `SuitCriticalTemperature` | 315.15 K | Interior temperature above which the occupant is being hurt. 42 °C, where heat stroke becomes life-threatening, and five degrees above where the suit holds them. |
| `SuitDamagePerKelvin` | 1 | Hit points a second per kelvin of overshoot. The same shape a block is damaged with. |

**The survivable room temperature is derived, not authored.** It is where the rating exactly cancels
what leaks in — `310 + 500/2.5` = **510 K**, about 237 °C — so moving either of the first two moves
it, and `SuitThermal.SurvivableKelvin` is the one place it is computed.

**An open helmet is a second heat path, not a second rule.** Breathing puts the room against the
lung surface, which no suit wall stands in the way of, so the conductance goes up tenfold and the
same cooler is asked to shift ten times as much. There is no separate setting and no separate
threshold: the survivable temperature falls out at `310 + 500/25` = **330 K**, about 57 °C, which is
where breathing hot air stops being merely unpleasant.

**Only room air is read.** A player outside a pressurised compartment the mod has mapped is not
simulated — no planet surface, no vacuum, no open-frame ship. That is the scope of what room air
already carries, and the rest is [backlog](backlog.md) `C17`.

**Cold does not hurt, and that is a decision with a number behind it** (`C18`). The suit regulates
both ways — a suit that could only cool would leave a player to freeze in shadow — so the cold side
is simulated and only the consequence is missing. The two ends of its window are not symmetric. The
suit holds its occupant while the leak fits inside one rating, `310 ± 500/conductance`, and an open
helmet pulls **both** ends in tenfold: the hot end lands at 330 K, which a player only meets when
something has gone wrong, and the cold end at **290 K — 17 °C**, which is an ordinary compartment. A
floor at hypothermia would therefore fire in any room below about 15 °C, on this mod's accelerated
clock, in ordinary play, where the ceiling never fires until a ship is already burning. `C16`
reinforces it: the cooler cannot fail, so a player is never cold *because their suit ran out*, which
is the situation freezing would be for. `TheColdEndOfTheSuitsWindowIsAnOrdinaryRoomAndTheHotEndIsNot`
pins where the two ends land, and fails if a change to the rating or the conductance moves the cold
one out of habitable range and with it the reason for having no floor.

**The cooler is free**, which is a limit rather than a decision: `IMyCharacter` exposes
`SuitEnergyLevel` to read and nothing to write, so no mod can charge a player for running it. What
the mod can see is a flat suit, and a flat suit does not regulate.

## Wind

The game's own wind figure is `MaxWindSpeed × airDensity` — one number per planet, scaled linearly
by altitude, identical at the pole and the equator, with no direction and no time of day. These
settings drive the model that replaces it. See [environment.md](environment.md) for what each one is
and where it comes from.

| Setting | Default | Effect |
| --- | --- | --- |
| `WindRoughnessLength` | 0.03 m | Roughness length z0 — about a tenth of the height of whatever covers the ground. Sets how steeply wind strengthens with height near the surface. **The ground under the grid answers first**: the material table carries a roughness for every surface it classifies — 0.0002 ice, 0.0005 snow, 0.003 sand, 0.03 grass, 0.05 rock, 0.5 forest — and this setting is what unclassified ground gets, which is an airless world, a modded voxel or a grid over no surface at all. |
| `WindGradientHeight` | 600 m | Height at which wind stops strengthening: the top of the boundary layer. Above it the profile is flat and the air density takes it down from there. **Capped by the air there is** — a boundary layer cannot be taller than the atmosphere over the ground under the grid, and several shipped worlds have less than 600 m of it. |
| `WindDiurnalAmplitude` | 0.35 | How far the daily cycle swings wind either side of its mean. Surface wind peaks in the afternoon; wind above the crossover peaks before dawn. 0 disables the cycle. |
| `WindDiurnalCrossover` | 80 m | Height at which the daily cycle vanishes. Below it the surface cycle, above it the nocturnal jet, fully reversed by twice this height. |
| `WindTerrainInfluence` | 1 | How much the shape of the ground affects wind: speed-up over rises, shelter behind ridges, steering along valleys. 0 leaves the wind ignorant of terrain. |
| `WindTerrainRadius` | 300 m | How far out the land around a point is read. The scale of landform the wind is allowed to notice. |
| `WindSlopeStrength` | 1 | Slope winds, 0..1: air running **up** a mountain by day and draining back **down** it at night. A thermal flow the ground makes rather than something it does to an existing wind, so it blows on a still day — and a real wind overruns it. Costs about 46 ns a sample, because the terrain it needs is already read. |

## Presentation

All client side, and all off by default but three: the two natural-feedback switches and the wind
indicator, which are what a player meets while playing rather than diagnostics for debugging.

| Setting | Default | Draws |
| --- | --- | --- |
| `DebugTextOnScreen` | `false` | Crosshair readout: temperature, per-mechanism watts, block constants, environment, grid totals, room classification, raw surface bits. Switching it on also makes the solver record per-mechanism watts, which is not free. |
| `DebugSolarRaycast` | `false` | Draws the sun ray from each grid, white when lit and red when occluded. |
| `DebugWindRaycast` | `false` | Draws the relative wind each grid is flying through, as a line from the grid scaled by its speed: green in still air, red once the grid is over `FrictionAtSpeedsAbove` and the leading face is heating. |
| `DebugAeroOverlay` | `false` | The aerodynamics debug view, on the grid being flown or looked at: the group's centre of mass in yellow, the relative wind in blue, and in orange the centre of the air's heating with the arm from the centre of mass drawn. The printed lines give the wind, the air density, the friction watts, the speed at which the air stops cooling the hull and starts heating it, and the ship's top speed — RelativeTopSpeed's cruise speed and ceiling where that mod governs the grid, the engine's own cap otherwise. **No aerodynamic force is applied while the force model is rebuilt** (see [aerodynamics.md](aerodynamics.md)), and a red line names the gates that stop friction heating. `/thermal aero` toggles it. Arms per-node diagnostics while on, like the crosshair readout. |
| `DebugWindOverlay` | 0 | Which view the wind map opens a session on: 0 off, 1 the lattice around you, 2 the whole planet. |
| `DebugWindIndicator` | `true` | The wind needle and speed under the crosshair. |
| `ShowEnvironmentReadout` | `true` | **One line, bottom centre, always on**: the air temperature around your ship and one word for the ship against its own rating — `cool`, `warm` or `hot`. `hot` begins exactly where the glow does, so the line and the block cannot disagree. It is what makes the mod visible in a world where nothing is going wrong; everything else it draws is a warning. Off costs a comparison and walks no grid. |
| `HeatGlow` | `true` | Blocks glow over the last 100 K before their own critical temperature, full at it and above. |
| `HeatTerminalPanel` | `true` | The thermal readout in a block's terminal detail pane. Off draws no text and refreshes nothing; the block's own controls are untouched. |
| `HeatWarningSound` | `true` | A cue in the cockpit as a block comes up on its own rating and as it crosses it. Heard only by the player at the controls. |
| `RoomOverlayMinKelvin` | 253.15 K | Bottom of the room view's colour span, −20 °C. |
| `RoomOverlayMaxKelvin` | 323.15 K | Top of the room view's colour span, 50 °C. |
| `DebugBlockOverlay` | 0 | Which view the block overlay opens a session on: 0 off, 1 temperature, 2 solar watts, 3 exposed faces, 4 friction watts, 5 rooms. |
| `DebugOverlayMaxBoxes` | 12000 | Boxes the block overlay may draw in one frame. Beyond it the overlay draws the part of the grid nearest the camera. 0 draws nothing. |

### Natural feedback

Two channels, and between them they say how close a block is to failing and how hot it got.

**`HeatTerminalPanel` is the one output that had no switch until 2026-08-25.** Every other thing the
mod draws was already behind something — the glow and the cue behind their own settings, the
crosshair readout behind `DebugTextOnScreen`, the performance panel behind a chat toggle, the
extinguisher's overlay behind holding the tool — and the terminal panel was on for everybody always.
It has one now for two reasons that are each sufficient: `C7` says every mechanism has a switch that
removes its own cost, and
[document-of-intent.md](document-of-intent.md#to-another-mod-that-also-simulates-heat-nothing-and-the-switches-are-the-answer)
answers a second heat mod in the world with *turn this one's outputs off and keep its API*, which
until now left two panels on every terminal. **It gates the text and not the controls**: a coolant
pump's throttle is something a player operates rather than something the mod says.

**`HeatGlow` is the last hundred kelvin before a block's own critical temperature.** Nothing below
that, a straight ramp through it, full at critical and above. It is a narrow window on purpose: a
glow means this block is about to go, not that it is warm. The band is a fixed 100 K rather than a
share of the rating, so the same distance from failure looks the same on a decorative block and on a
large thruster. Nothing the game ships glows from the weather — the hottest planet in `Planets.xml`
runs a 390 K day, and the coolest block type is rated 583 K, so its band starts at 483 K.

**The colour is separate and stays physical**: the Planckian locus by absolute temperature, so a
block glowing at 500 K is deep red and one at 2,000 K is orange. Brightness says how close to
failing, colour says how hot it actually got. See
[document-of-intent.md](document-of-intent.md#natural-feedback--built) for why the brightness is not
incandescence.

The surface cue uses HDR soft depth-tested patches with grazing-angle and distance fades, plus
bounded nearby heat lights. `HeatGlow=false` removes both; `true` is the most faithful implemented
form, with no separate cheaper mode. The geometry and lighting limits, including the 4,000-quad
and 32-light caps, are documented in [thermal-glow.md](thermal-glow.md). `/thermal status` reports
hot cues, exposed/in-range blocks visited, submitted glow quads and active lights for diagnosis.

**And the colour is a between-blocks signal rather than a within-block one**, measured: across one
block's hundred-kelvin band the colour moves less than a just-noticeable difference for 88 of the
101 block types the game ships, while the coolest-rated block against the hottest is ΔE 13.72. So
nothing a player has to act on is carried by hue — see
[document-of-intent.md](document-of-intent.md#who-the-glow-is-for--brightness-and-colour-as-a-refinement).

**`HeatWarningSound` is the same warning in sound**: a cue about three seconds before a block
crosses its rating and a distinct one as it crosses, heard only by the player at the controls. It
reaches blocks that are hidden, off screen, or outside the visual draw budget. Models without
emissive materials still receive the drawn glow.

The lead is a forecast rather than a straight line: a block levelling off below its rating is never
cued, however fast it is warming at the moment. A block heating *faster* than it was has no
equilibrium to read, so a straight line answers there, which warns early rather than late.

**With both off, the cost is nothing**, and with both on and nothing hot it is one comparison per
grid per second — the hottest block against the lower of where the coolest block would start glowing
and where it would start being watched.

### The wind map

**Ctrl+Shift+W** cycles it: off → local → planet → off. `/thermal wind` does the same from chat.

The game has no wind field — `MyPlanet.GetWindSpeed` returns the planet definition's maximum scaled
by air density, the same figure at the pole and the equator — so this mod invents one, and until
this view there was no way to look at it. See [environment.md](environment.md#wind) for what
the field is; this is how you see it.

**Local** drapes arrows over the ground itself, out to five kilometres in every direction — a disc of
about thirteen hundred, 250 m apart, each projected onto the terrain under it and lifted ten metres
clear. Five kilometres is chosen against the field's own 900 m variation scale: it takes several
turnovers of that variation to read as a pattern rather than as one gust, and a lattice small enough
to fit on a landing pad shows a single value repeated.

Because the arrows lie on the terrain, **terrain hides them**. From standing height most of a five
kilometre field is below the horizon or behind a hill — that is the field being drawn honestly, and
it is why this view is worth gaining some altitude for. Look down on a valley from a few hundred
metres up and the whole disc is visible at once.

The lattice is anchored to the world rather than to you, snapped to a whole number of 250 m steps, so
it stays put as you walk instead of sliding along underfoot. It is rebuilt when you cross into the
next cell, and the rebuild is spread over frames — about nine of them — because the terrain lookup
per arrow is the one expensive call in the view. The arrows already up stay up until the replacement
is complete, so a resample is invisible rather than a blink.

**Planet** draws arrows over the whole globe on a latitude and longitude lattice, sized against the
planet's radius and floating above its highest terrain. This is the view for the circulation itself:
easterly trades either side of the equator, westerlies in the middle latitudes, easterly again at the
poles. Fly out far enough to see a hemisphere. Arrows on the far side are dropped rather than drawn
through the planet, since nothing occludes transparent geometry.

An arrow points **where the wind blows**, is longer and redder the harder it blows, and is scaled
against the storm end of the ramp rather than against the planet's ceiling — calm air is about a
tenth of that ceiling, so scaling against it would draw every ordinary day as a field of stubs.

Arrow *width* is held on the screen rather than in the world, between a floor and the arrow's own
length. A single lattice spans two orders of magnitude of distance — the arrow at your feet and the
one five kilometres away are the same arrow — and a fixed width in metres would draw the near one as
a slab and lose the far one entirely.

Two things it does not do. **Weather is sampled once, where you are standing, and applied to every
arrow**: asking per arrow costs a string allocation and a lookup for each of several hundred points
every resample, and on the globe view it would be reading one storm's weather at points thousands of
kilometres away regardless. And the lattice is resampled every twenty frames rather than every frame,
so an arrow can be a third of a second out of date — which is far finer than anything in the field
can actually move.

### The wind indicator

A needle under the crosshair, with the speed beneath it, whenever there is wind where you are.

Screen up is the way you are facing, so the needle points where the wind is pushing you: straight up
is a tailwind, straight down is a wind in your face, and a needle on its side is the crosswind that
carries a ship off its line. This is the opposite of the meteorological convention, where a wind is
named for where it comes from — the question here is which way you are being pushed, not what to call
the weather.

**In a cockpit it shows the wind the ship is flying through, not the wind over the ground.** Those
are the same parked and quite different at speed, and the relative one is what the solver heats the
hull with. On foot there is no grid to ask, so the field's own wind is used. It draws nothing in
space, nothing in still air, and nothing while a menu or the chat box is open.

### The block overlay

**Ctrl+Shift+=** cycles it: off → temperature → solar watts → exposed faces → friction watts → rooms
→ off.
`/thermal overlay` does the same from chat. A mod cannot add a rebindable control, so the chord is
fixed; it is ignored while the chat box or a menu is open.

Every block of the grid you are controlling and the grid you are looking at is drawn as a translucent
box coloured by the selected value, using the same ramp as the HUD and the terminal. The whole grid,
at any distance: a debug view that faded out at some radius would read as a cold far end rather than
as an undrawn one. Only those two grids are picked up, which is what keeps the cost bounded. The boxes are drawn *through* the hull — a reactor buried mid-ship is visible from
outside, which is the point of a debug view and the reason this was a poor thermal camera.

While a view is up, a readout panel sits at the top left with the figures behind the picture: what
the grid's coldest, mean and hottest blocks are in the temperature view, how many watts of sunlight
it is taking and whether the sun is occluded in the solar view, its exposed face count and area,
its speed against the friction threshold, or a table of rooms with their cell counts, air
temperatures and seal state. It follows whatever grid the overlay is drawing and disappears with it.
The panel needs Rich HUD Master, the same as the settings menu; the overlay itself does not.

### What self-shadowing costs

Measured by the `shadow-cost` scenario, which reports both halves because they are different kinds
of work:

| | cheap model | self-shadowed |
| --- | --- | --- |
| Per tick, 8000-block grid, sun still | 0.010 ms | 0.010 ms |
| One full pass, 8000-block solid cube | — | 2400 air cells, 2.4 ms |
| One full pass, 4440-block hull with decks | — | 7848 air cells, 4.3 ms |

Between passes it costs nothing measurable: one extra multiply per face inside the solar term. The
pass is the cost, and it is spread over ticks in slices of `SunShadowBudget` (2048 cells) — about
1.1 ms per slice on both grids above, landing on the grid's ten-frame tick.

A pass runs when the sun has moved 2° in the grid's own frame. On a planet that is tens of seconds
of play. In space it is the *ship's* rotation that moves the sun, so a grid spinning fast rebuilds
continuously — a sustained ~1 ms per tick on a mid-size ship rather than an occasional one. If that
ever matters, `ShadowDetail` 1 is the answer, and it is free.

### External shadow

`SolarOcclusionInterval` decides how often any of it is re-tested; `ShadowDetail` decides what is
tested at all, and each occluder is priced differently:

| Occluder | How it is tested | Cost |
| --- | --- | --- |
| Planet | angle against the planet's radius | arithmetic, no ray |
| Terrain | ground height sampled along the sun ray | ten height lookups, near a surface only |
| Voxel | physics raycast against the asteroid | one raycast per candidate |
| Grid | one ray, or a walk per face | none below `ShadowDetail` 2, one block ray per candidate at 2, one walk per face at 3 |

At `full`, other grids are dropped from the whole-grid ray and answered per face instead: counting
them twice would shade an entire ship for a shadow across one corner. The
neighbours are gathered on the occlusion interval, and a new shadow pass only starts when one of
them has actually moved — more than a cell, or turned more than about two degrees. Two ships docked
together never move relative to each other and cost nothing after the first pass.

The planet and terrain tests answer different halves of the same question. The planet's is the ball:
is the sun below the horizon of a smooth world. Terrain's is everything the ball ignores, which is
what a player on the ground can see — a base in a canyon stays cold for an hour after the ball says
dawn. The walk runs only when the ball says the sun is up, so night costs nothing extra.

Shadow is now a fraction rather than a flag: `SolarOcclusionSamples` points are cast from inside the
hull, and solar gain is scaled by the share that reached the sun. At the default of one sample that
share is 0 or 1 and behaves exactly as before. Raise it and a kilometre-long ship crossing a
terminator dims over the crossing instead of switching off when its centre passes.

**The sample count is the cheapest rung, and it is measurably not the dial worth spending on.**
`SolarOcclusionSamples` and `ShadowDetail` compose into a ladder, and the second already ships at
the top. The third ships at one ray from the
grid's centre, which [backlog.md](backlog.md) `A9` ranked second of everything open on the ground
that a ship flipping between fully lit and fully dark is a difference a player can see. **Measured,
it is the wrong dial**, and `dotnet run --project tests/Thermodynamics.Sim -- occlusion` is the
measurement.

Flying hulls of every size through an Earthlike's terminator at the shipped cadence, averaged over
where the test schedule falls:

| Hull | Surplus sunlight, 1 sample | at 9 samples | Worst error, 1 → 9 | Partial readings a crossing, at 9 |
| --- | ---: | ---: | ---: | ---: |
| 25 m | 1.51 s | 1.54 s | 0.977 → 0.968 | 0.08 |
| 300 m | 1.53 s | 1.50 s | 0.889 → 0.769 | 0.62 |
| 600 m | 1.56 s | 1.53 s | 0.778 → 0.551 | 1.21 |
| 2,500 m | 1.41 s | 1.46 s | 0.551 → 0.333 | 4.92 |

**Nine times the raycasts changes the sunlight a hull absorbs by nothing.** A single ray from the
centre is *unbiased*: it goes on reporting light after the leading end is dark and reports dark
before the trailing end is, and over a crossing the two cancel. What the extra samples fix is the
**worst error** column — how wrong the reading is at a moment, which is what a player sees — and
only on a hull long enough that a test lands while it is still straddling. Below about 600 m it
almost never does.

**The surplus is the interval, and it is exactly half of it.** A test that ran before the crossing
keeps saying *lit* until the next one, and a grid meets every phase of that schedule equally often:

| `SolarOcclusionInterval` | Every | Surplus sunlight a crossing |
| ---: | ---: | ---: |
| 1 step | 0.25 s | 0.10 s |
| 4 steps | 1.00 s | 0.48 s |
| 12 steps *(shipped)* | 3.00 s | 1.53 s |
| 48 steps | 12.00 s | 5.96 s |

So the two dials cost the same thing — raycasts — and per raycast spent **the interval buys strictly
more fidelity than the sample count**. Neither default moves here, because both raise a cost that has
never been measured in a session: `A9` needs `F5` first. What has changed is which dial the answer
is expected to be.

**And the rung above both is worth about a two-hundredth of a kelvin a metre of hull.** Neither dial
reaches the error that comes from applying one answer to a whole ship — the leading end told it is
lit while it is dark and the trailing end the reverse — and resolving the planet's shadow *per face*
is the only thing that would. Read per block instead of per hull, tested every step so the cadence
contributes nothing, that error is:

| Hull | Worst block, geometry alone | at the shipped 12-step cadence |
| --- | ---: | ---: |
| 25 m | 0.07 K | 0.59 K |
| 150 m | 0.29 K | 0.79 K |
| 600 m | 1.11 K | 1.63 K |
| 2,500 m | 4.53 K | 5.02 K |

*One lit face of a 500 kg steel-plate block gains 0.36 K a second of sunlight at the shipped clock,
which is the conversion every figure above rests on.* **The whole table moves with `HeatTimeScale`,
and it has**: it read 0.905 K a second and 0.73 K at 150 m until `C24` took the clock from 225 to 90,
which is 0.4 of the thermal ground covered in the seconds a hull is told the wrong thing about.
`OcclusionLadderTests` prints these four rows, so the table has a source rather than a history.

**It is linear in length and it is under a kelvin for anything under 550 m**, so on the ships people
build it is a fraction of what the cadence already costs — below 300 m the cadence is the larger half
of the error, and above it the geometry is. That is what decides the top rung: it is a change about
how long ships are rather than about how good the model is, and nothing in the shipped configuration
moves for it. `OcclusionLadderTests` pins the rate and the crossover.

The solar settings stack as a choice of cost. `EnableSolarHeat` off is free and models no sunlight
at all. On with `ShadowDetail` at `1` or below is the cheap model: a face is lit whenever it points
at the sun. `2` and above is the accurate one: the grid shadows itself, for one pass over its cells
whenever the sun moves.

**Solar watts** does not draw boxes. Sunlight lands on a face, not on a block, so it draws the
grid's skin — one quad per exposed face — shaded by that face's own irradiance: the sun's energy
scaled by how square the face is to it, and zero when the mechanism is off or the grid is in shadow.
A wall dark because it turned away from the sun then looks plainly different from a wall dark
because the ship is eclipsed. Faces pointing away from the camera are dropped, so what you see is
the near skin rather than both sides at once. The panel still reports the watts.

**Solar watts** and **friction watts** are the per-mechanism figures the solver normally does not
bother to write down. Selecting either makes it record them for as long as that view is up, the same
way the crosshair readout does, and stop when you cycle past. Without that they would draw every
grid uniformly at zero, which reads as "no solar heating" rather than as "not measured".

**Rooms** is the one view that draws cells rather than blocks: a box in every cell the mapper put in
a room, **coloured by that room's air temperature** on a tighter span than blocks use
(`RoomOverlayMinKelvin`/`RoomOverlayMaxKelvin`, −20 °C to 50 °C by default), because room air lives
in a narrow band where the block ramp makes every compartment the same shade. Which room is which
rides on the wireframe instead — a colour per room, faint when vented — so identity never costs the
temperature its clarity.

A room holding no air is drawn as an empty outline: there is no temperature to show, and an
unpressurised compartment is usually the thing being hunted for. It is also how you find out whether
two compartments you think are separate came back as one room, and where the leak is when a room you
think is sealed reads as vented.

Everything about it is client side and per frame: nothing is written to the grid, nothing
replicates, and switching it off leaves no trace.

A box is six transparent quads and twelve lines, so a forty thousand block ship drawn whole asks
the renderer for over seven hundred thousand billboards a frame. Two things bound that. Boxes
outside the camera's view cone are not drawn, which changes nothing on screen. What is left is held
to `DebugOverlayMaxBoxes` by a radius fitted each frame to the count, so a large ship is drawn out
to the distance that fits the budget and a small one is drawn whole. The report's *Block overlay*
section records what it cost and how much was held back. It replaces the four `Debug*BlockColors` modes,
which called `MyCubeGrid.ColorBlocks` and showed heat by permanently overwriting every player's
paint. `DebugBlockOverlay` is a server-side setting like the rest of the file, so it only sets what
a session starts on — the keybind is how each client drives it.

## Telemetry

| Setting | Default | Effect |
| --- | --- | --- |
| `EnableTelemetry` | `false` | Session-long data collection. See [telemetry.md](telemetry.md). |
| `TelemetryPlanetProbes` | 0 | Solver steps between planet-wide probe sweeps, or 0 for none. A sweep reads the **wind and the climate** at 72 fixed points — every latitude from −80° to +80° including the equator, eight longitudes each — at five heights, whether or not anything is standing there. Writes `Thermodynamics_PlanetProbes_*.csv`. 360 is a sweep a minute at the shipped clock. |
| `TelemetrySampleStride` | 4 | Fraction of each grid's blocks sampled per step for the per-definition statistics — `1/n`. Every block is still seen once per `n` steps. |


### Top speed

**This mod does not set a ship's top speed.** [RelativeTopSpeed](https://github.com/Gauge/RelativeTopSpeed)
does, where a world installs it; without it the engine's own cap applies, which is 100 m/s in a
vanilla world. The mod reads RelativeTopSpeed's API to say which limit a ship is under — the
aerodynamics debug view prints its cruise speed and ceiling — and writes nothing to it. The port of
that mod which lived here, and the nineteen settings it carried, are retired; see
[aerodynamics.md](aerodynamics.md#ar15--top-speed-belongs-to-relativetopspeed-this-mod-reads-it).

## Time and pace

Two settings affect pace and they do different things:

```
StepSeconds    = 1 / Frequency              simulated seconds in one solver step
StepsPerSecond = Frequency                  steps run per real second
Capacity       = SpecificHeat × Mass / HeatTimeScale
```

* **`Frequency` is both accuracy and rate** — how finely a simulated second is integrated, and how
  many of those steps run per real second. It is linear in CPU on a soft grid and free on a stiff
  one; see [Frequency is not the cost dial it looks like](#frequency-is-not-the-cost-dial-it-looks-like).
  The `SimulationSpeed` multiplier that used to sit beside it was retired on 2026-09-01: it did
  nothing but scale this.
* **`HeatTimeScale` is how fast heat moves within a simulated second.** Free in CPU terms, and the
  reason the definitions can carry real material values without a hull taking hours to cool.
  Dividing every capacity by *k* is exactly running thermal time at *k*×: every rate scales
  together, so equilibrium temperatures, the balance between mechanisms and the ratios between
  block types are all unchanged. Only the clock moves.

Total acceleration over real physics is `HeatTimeScale`. **The shipped value is
90**, and 225 is what made steel's real 450 J/(kg·K) behave the way the old flat value of 2 did —
the calibration the real-unit conversion was checked against, and two and a half times faster than
what ships. `C24` slowed it, and multiplied the conduction pace by four at the same time, because
`G8`'s window is not reachable by either dial alone: the clock that puts a block in the window puts
the hull outside the session.

`HeatTimeScale` costs stability margin rather than time: higher values make the system stiffer, so
the solver takes more substeps. When a grid is stiff enough to hit the substep cap the telemetry
report says so.

Block tuning — conductivity, specific heat, critical temperatures — is not in this file. It lives in
the definition XML; see [definitions.md](definitions.md).

---

## Where the settings surface is going

The menu, the config file and the definition files are one surface to a player and three to the
code. Two of the three definition files are settings now — [the loop and planet values](#the-definition-files-from-the-menu)
— and what is left is the third, the config file's own shape, and a Status page worth opening.

**Per-subtype block overrides.** `Cubes.xml` is 114 entries — 96 per-type defaults and 18 per-subtype
— and it is different in kind from the other two: 674 authored values, whose interesting ones belong
to the block a player is looking at rather than to a list they scroll. Properties are cached per definition in `ThermalBlockCatalog`, so
changing one at runtime needs the cache invalidated and every node of that type refreshed. A mod
folder is read-only in a workshop install, so this writes a per-world override layer into world
storage like the other two, covering any subtype rather than a fixed list.

**Statistics grows the per-stage half.** It now carries the world's settings, what is running, the
energy being moved, what the solver is spending and the frame cost. What it does not yet carry is the
breakdown:

* **Cost**, per stage rather than as one number: topology, room mapping, exposure, solver, room
  pressure, with the same figures the telemetry report carries.
* **Load**, as a projection rather than an instant: substeps demanded against granted, what the
  per-block cap is flooring, and what a change to the caps would cost — the sweep
  [load-and-hitching.md](load-and-hitching.md#in-the-field) does by hand.
* **Faults**: settings that cancel each other, grids running below real time, compartments the game
  seals and this model does not, pumps in a ring that oppose each other, definition entries that
  fell back to the default because their `TypeId` was wrong. **Every one of these has been a real
  defect at least once, and each was found by reading a dump rather than by the mod saying so.**

Its form is still open and wants prototypes rather than a decision on paper. Rich HUD has no chart
control: a sparkline can be drawn as text in a `TextPage`, which is cheap and honest; a real plot
means a custom HUD element, which is a different size of job.

**The config file follows the menu.** The file is flat — every element in one list, in the order
they were added. If the menu is organised by system then the file should be too, because they are
read by the same person for the same reason:

```xml
<Solver>
  <Frequency>8</Frequency>
  <MaxSubsteps>64</MaxSubsteps>
</Solver>
<CoolantLoops enabled="true">
  <LargeGridFlowRate>10</LargeGridFlowRate>
</CoolantLoops>
```

Two things this must not break:

* **A world's existing values.** Defaults live on the fields, so a reader that finds nothing leaves
  them alone — which means a restructure silently resets every tuned world unless the old flat shape
  is read first and migrated. That is the whole risk of this step.
* **The names.** `/thermal set`, the mod API and the sync all address settings by name. Grouping may
  change where a name sits in the file; it must not change the name.

Each step stands alone and each is separately revertible, which is why the file restructure — the
one with a migration risk — is late rather than first.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | **The aerodynamic force and the top-speed port are retired** (`K25` step 0; [aerodynamics.md](aerodynamics.md#strip-and-rebuild)). Twenty-four settings are gone: `EnableDrag`, `DragCoefficient`, `EnableShapeDrag`, `EnableLift`, `LiftCoefficient`, and the Top speed page's nineteen — `EnableTopSpeed`, `SpeedLimit`, `EnableSpeedBoost`, the twelve cruise-curve points, both boost ceilings and both resistances. Their `ProtoMember` numbers — 135, 136, 138 to 140 and 145 to 163 — are retired and never reissued (`W2`), so a saved world's value is ignored rather than read into something else, and `/thermal get` answers a retired name with nothing rather than a number. **What a world notices**: no aerodynamic force on any ship; the engine's own speed cap in place of 140 m/s unless RelativeTopSpeed is installed; and, because the Newtonian shape factor left the friction row with the force it was fitted for, friction heating at the projected area again — 1.71× on a four-cube brick and 2.10× on a wedge, and a median 4.56 K at re-entry by `K22`'s population measurement. `EnableWindwardShielding` stays and moves to the Friction heating page. |
| 2026-09-20 | Add HDR glow emission and runtime glow counters to `/thermal status`. |
| 2026-09-20 | Document the soft glow and rendering caps; correct the sound cue description. |
| 2026-09-04 | **The aero overlay honours the anchored-group veto, and names it as a gate.** The overlay's summation was a hand copy of the drag pass's that did not know a group with a static member takes no force, so a base standing in wind drew drag and lift arrows the server never applies — against the overlay's own promise of being the force applied rather than a second opinion. Both now read one summation (`AeroGroupForces`); an anchored group shows its centre of mass and wind, zero force, and *anchored* in the red gate line. |
| 2026-09-02 | **`FrictionAtSpeedsAbove` ships at 0: the aerodynamic term — heating, drag and lift — is live at every speed.** The 50 m/s guard was created against low-speed heating the v³ law already prevents by arithmetic (~1 W/m² at 10 m/s), and it cost a discontinuity in heat and force at 50 and the absence of lift and drag below it. The cooling-to-heating crossover the guard imitated is emergent (convection ∝ √v·(T−T_amb) against friction ∝ v³, so it depends on hull temperature) and the aero debug view now reports the hull's net air watts and its own crossover speed. The dial remains for worlds wanting the legacy cut; a force is never applied to a group with a static grid in it, which is what keeps bases standing in wind free now that the term reaches them. Also added `DebugAeroOverlay` (this page, above) and the `EnableDrag` dependency to `EnableLift`'s row, which was documented as needing only `EnableShapeDrag`. |
| 2026-08-31 | **The menu's *Other* page is empty, and three defects were found emptying it.** Eleven settings had no layout entry and lived there with their field name for a label and a 0..1,000 slider whatever they were — the six suit dials, `ParallelGrids`, `FloorBlocksWhenOverBudget`, `ShowEnvironmentReadout`, `DebugOverlayMaxBoxes` and `PlanetUndergroundConvectionCoefficient`. They have pages now: **Suit** under Ship systems and **Threading** under Solver are new. **`IsFlag` missed six switches**, three of them already on the Debug page and drawn as *sliders from 0 to 1* — it decides the menu's control, whether `/thermal` prints `on` or a number, and whether telemetry records a bool, so each was wrong three ways. **And `WellMixedCoolant` was reachable by nothing**: a documented rung of the coolant ladder, in the config and read by the solver, but missing from `Names()`, so only hand-editing a world's XML could set it. Two guards added — every setting reaches a page, and every `bool` is a flag and nothing else is. |
| 2026-08-31 | **Reworked the menu: Aerodynamics is its own folder, Overview is gone, and a page is its sections rather than a lattice of tiles.** `EnableDrag`, `DragCoefficient` and `EnableWindwardShielding` had no layout entry, so the force half of the friction term sat unlabelled on the *Other* page while the heat half was a Ship systems page; both halves are now the **Aerodynamics** folder. **Overview is removed** — a count, a three-word conflict flag and three clipped figures, every one of them a worse version of a line on the page that wraps — and **Status is now Statistics**, carrying the world's settings, what is running, the energy it is moving, what the solver is spending and the frame cost. **Its figures were never refreshing**: `Refresh` was called from the settings sync and nowhere else, so a category headed *Right now* held whatever the world was doing when the menu was built; `ThermalSettingsMenu.Tick` now re-reads it twice a second while the terminal is open. **A section is one tile now** rather than tiles of three packed two to a row. Also fixed the subheader, which compared a page name against the `Display` *category* constant that no page is called — so on a client every page claimed to be server side, Debug included. |
| 2026-08-28 | **Corrected the room map's convergence figure, which had been stale for nine days and was quoted here from `D2`** (`E10`, `E5`). It read 7,207 ticks — twenty minutes — on a million blocks; re-measured by `bench scale --max 1000000` it is **3,934 ticks, about eleven minutes**, on 1,000,294 blocks and a 14,278,796-cell box. The 7,207 predated the 2026-08-26 word skip and the 2026-08-27 span flood, both of which `D2`'s own body already recorded — the headline outlived the paragraph that superseded it. **And the figure is structural**: convergence is the box over a 4,096-cell tick budget, so no work on milliseconds a cell can move it, which `RoomMapConvergenceIsTheBoxDividedByItsBudget` now pins. |
| 2026-08-28 | Added `ShowEnvironmentReadout`, **on by default** — the first readout that is. One line, bottom centre: the air around your ship and one word for the ship against its own rating. `hot` begins exactly where the glow does, so the two cannot disagree. `B41`. |
| 2026-08-28 | **The menu says which way each integration dial points**, which until now only this page did. `FidelityEnds` declares the faithful end once and every per-setting tip carries the same sentence; `FidelityEndTests` holds it against the four claims in the rungs table and against the shipped defaults. Two of the four do not ship faithful, deliberately, and those are the two the sentence is for. |
| 2026-08-26 | `LoopCoolantKilogramsPerCubicMetre` replaces `LoopCoolantMassPerPipe` as the coolant charge, and the old dial stays as a flat-mass override with a default of zero meaning *use the density* (`P15`). A flat 50 kg is 3.2 kg/m³ in a large cell and 400 kg/m³ in a small one, where it outweighs the pipe block; 33 kg/m³ gives 515.6 kg and 4.1 kg. The config version moved to 7, so an existing world is replaced with defaults rather than half-migrated. Also corrected `LoopHeatTransferCoefficient`'s default in this table, which still read 160 after `C42` moved it to 1,000 — nothing checks the numbers in this column, only the names. |
| 2026-08-26 | Corrected what `LoopStagnantTransferFraction` does. It was described as scaling what a stopped ring carries between parcels, which `Advect` already reduces to nothing, and it reached no line of the simulation at all; it now scales the fluid-to-wall coupling, so a stopped ring conducts into its coolant more slowly than a pumped one. The default is unchanged at 1. |
| 2026-08-25 | Added `LoopRefillEquivalentKelvin` and `LoopRefillKilogramsPerSecond`, the two dials of coolant being a consumable ([backlog.md](backlog.md) `B43`). The first is derived rather than chosen — it is the excess at which venting and refilling break even, because a pump wastes all of what it draws — and the second is the only figure in the feature that was picked, which both it and [thermal-model.md](thermal-model.md) say.

| 2026-08-25 | **`FloorBlocksWhenOverBudget` stays off, decided on 294 ships rather than on one hull** ([backlog.md](backlog.md) `C30`). Against a rule fixed before the data — 0.03 K ships it on, 0.6 K keeps it off — the population p99 is **27.76 K**. Its safety half is perfect: no lost clock, nothing floored in the control, never stiffer. Its cost half is not, and no gate rescues it without gating it out of existence. The switch stays for a world that would rather have its clock. |
| 2026-08-25 | Added `FloorBlocksWhenOverBudget` ([backlog.md](backlog.md) `C30`): when a grid cannot afford its demand, floor its stiffest blocks to what the allowance grants instead of shortening its step. Off by default. The measurement behind it is nine to thirty-seven kelvin of lost clock against the cap's own 0.024 K. |
| 2026-08-25 | `MaxSubstepsPerBlock` stays off by default, decided on all 8,144 published blueprints rather than on one hull ([backlog.md](backlog.md) `C3`). The population p99 is 0.2820 K, under the figure that had kept it out; what decides it is that the error is charged per block and the throughput is collected per grid, so four fifths of the ships people publish would pay and collect nothing. |
| 2026-08-25 | Added `HeatTerminalPanel` ([backlog.md](backlog.md) `B40`), the switch for the one output of the mod a world could not turn off. Default `true`, so nothing a player has changes; client-owned, like the other two presentation switches. It gates the panel's text and its refresh and leaves the block's own controls alone. |
| 2026-08-25 | Corrected the fourth site of `A9`'s per-metre figure, which the pass earlier the same day missed: the rungs inventory said 0.0045 K a metre where the lab says 0.0018. The check added that morning reads the per-face *table* and could not see a figure quoted in prose, which is the limit of that kind of check and is why the number is now stated in one place and pointed at from the other. |
| 2026-08-25 | Said what the colour channel is a signal *about*, which [backlog.md](backlog.md) `B34` needed measured: within one block's glow band it moves less than a just-noticeable difference for 88 of 101 block types, and between the coolest and hottest rated blocks it is ΔE 13.72. It distinguishes blocks, not moments. |
| 2026-08-25 | **`A9`'s per-face table had gone stale with the clock, on three pages, while the test that produces it printed the right figures throughout.** `C24` took `HeatTimeScale` from 225 to 90 and every kelvin in that table is seconds of sunlight times a rate that moves with the clock, so the rung is worth **0.0018 K a metre, not 0.0045** — 0.29 K on a 150 m hull against 0.73 K. The crossover is unmoved at about 300 m, because both halves scaled together. `OcclusionLadderTests` now reads this table out of this page and fails when it does not match the lab, so the next clock change is loud. |
| 2026-08-25 | Three sentences in the body described a past layout rather than the present one (`R12`): the menu's grouping, the suit's opening, and the allowance's cost ladder. Each states what is now the case; the ladder's own table moved to [benchmarks.md](benchmarks.md#what-the-rate-it-trades-away-is-worth), which owns the measurement. |
| 2026-08-25 | **The allowance's cost ladder existed here and in [benchmarks.md](benchmarks.md#what-the-rate-it-trades-away-is-worth), which is two copies of one measurement.** This page states what it costs — 1.19 K at a 5 % deficit rising to 36.98 K at 60 % — and that page owns the table, the rig it was taken on and the convexity that made a ladder necessary rather than one point and a slope. A measurement written down twice is two things that can drift (`D3`). |
| 2026-08-24 | **`MaxElementVisitsPerStep` is 4,000,000, from 2,000,000, because what it gives up was priced for the first time.** The bound makes a step shorter rather than coarser, so nothing is approximated and the grid's thermal clock runs slow instead — worth 1.19 K standing at a 5 % deficit and 36.98 K at 60 % under a moving load, and 0.00 K under a steady one. It also binds in **air** rather than in vacuum, at about a third the grid size: at the old value a driven census hull kept all of real time to 32,000 blocks in vacuum, 16,000 on a planet and 9,000 in flight. Beside 0.028 K for the substep ceiling this world accepts and 0.607 K for the per-block cap it refuses to ship as a default, that made this the largest approximation shipped and the only one never measured. A frame is bounded at 266,667 element visits instead of 133,333. Old value kept visible here and in [What a shortened step costs](#what-a-shortened-step-costs) (`E11`), and [backlog.md](backlog.md) `C27` carries the reasoning. |
| 2026-08-24 | **The approximation the defaults shipped on is gone, and `HeatTimeScale` is 90.** `C24` applied `C12`'s retune — `ConductionScale` 2.4 → 9.6 and the clock 225 → 90 — and the substep ceiling it was breaching is decided by a convection-limited demand, so that demand came down with the clock: the 40-hull panel's worst p99 is **35.12 of 64**, 55 % of the cap, with **0 of 40 hulls refused** in every environment measured, where 8 of 40 were refused in re-entry before. `G6` passes on the configuration that ships. Renamed the section to say so, kept the reasoning, and recorded the one row that went the other way: vacuum demands 1.6× what it did ([backlog.md](backlog.md) `C27`). |
| 2026-08-24 | Corrected [The approximation that shipped on](#the-approximation-that-shipped-on-and-no-longer-does), which described what a refused step costs in terms that were true of blocks and not of a plumbed or pressurised ship, and `ClampConductionOvershoot`'s row, which described half of what the clamp now does. Both are the same fix: an exchange is bounded pairwise *and* every exchange arriving at one node, parcel or room is bounded together ([backlog.md](backlog.md) `A10`). |
| 2026-08-24 | Added [Every mechanism, and the rungs it has](#every-mechanism-and-the-rungs-it-has), the inventory `C15` asks for: what each feature's ladder is today and whether a cheaper rung is known to be possible. Nine mechanisms have two rungs and only two of those are gaps. **Wired `WellMixedCoolant` into a world's configuration**, which it had never been: the solver read it, the suite exercised it and two pages called it a choice a world makes, with no field in `Settings.cs` and no line in `Apply`. |
| 2026-08-24 | Added [The approximation that shipped on](#the-approximation-that-shipped-on-and-no-longer-does). The global substep ceiling binds on about a fifth of a real population in thick air at flying speed, which no page said out loud, and it stays at 64 because refusing 1.15× of the demand buys 1.15× of the step for 0.028 K — the trade `P14` exists to take. Recorded why the per-block cap is a switch and this one is not: 0.607 K against 0.028 K ([backlog.md](backlog.md) `C19`). |
| 2026-08-24 | Recorded that `RoomConvectionCoefficient` carries no pressure term, so a room's pressure decides whether its walls are coupled and, above zero, nothing else ([backlog.md](backlog.md) `F21`). |
| 2026-08-23 | **Priced `A9`'s unbuilt top rung**, which had been described and never measured: resolving the planet's shadow per face removes an error linear in hull length, about a two-hundredth of a kelvin a metre — 0.73 K on a 150 m hull against the 1.97 K the cadence already costs it, and 11.33 K on a 2,500 m one. Below 300 m the cadence is the larger half. Added the table to [External shadow](#external-shadow). |
| 2026-08-23 | `HeatGlow` is the last 100 K before a block's own critical temperature, superseding the two entries below it. The colour is unchanged and still absolute. |
| 2026-08-23 | `HeatGlow` is back to incandescence — brightness and colour both functions of temperature alone — and the rating-keyed form of the entry below is withdrawn. |
| 2026-08-23 | `HeatGlow` is a block's distance from its own rating rather than an absolute temperature: nothing at comfortable temperatures, full at critical and above. The colour is unchanged and still absolute. |
| 2026-08-23 | Added `HeatGlow` and `HeatWarningSound`, the two natural-feedback switches ([backlog.md](backlog.md) `B25`). Both client side and both on by default, which makes them the first presentation entries here that are on because a player is meant to meet them rather than because they were asked for. |
| 2026-08-22 | `PlanetAmbientLagSeconds` is the fallback rather than the whole answer: the lag is a share of the world's own day wherever the day has been measured ([backlog.md](backlog.md) `C6`). |
| 2026-08-22 | `WindRoughnessLength` is the fallback rather than the whole answer: the ground material under a grid now sets its own roughness, which is the one figure in that table with a published table behind it ([backlog.md](backlog.md) `B16`). |
| 2026-08-22 | Said that `WindGradientHeight` is capped by the atmosphere over the grid's own ground ([backlog.md](backlog.md) `B20`). |
| 2026-08-22 | Added `PlanetUndergroundConvectionCoefficient`. A buried grid exchanged at the coefficient for moving air, which made digging in the best cooling in the game ([backlog.md](backlog.md) `A16`). |
| 2026-08-22 | Added [The suit](#the-suit) and its five settings, plus `EnableSuitDamage`. A player in a burning compartment was the one place heat stopped being consequential ([backlog.md](backlog.md) `B10`). The survivable temperature is derived from the rating and the conductance rather than authored, and the three things the model deliberately does not do are `C16`, `C17` and `C18`. |
| 2026-08-22 | Added `/thermal problems`, and the count of them to `/thermal status`. The two validators the mod carried had never been called from anywhere the game runs, so an emissivity above one or a substep long enough to clamp away a whole step was diagnosed correctly and told to nobody ([backlog.md](backlog.md) `A19`). |
| 2026-08-22 | Said in [External shadow](#external-shadow) that the shipped occlusion default is the cheapest rung of the ladder those three settings compose, and pointed at [backlog.md](backlog.md) `A9` for the ladder and the default it asks for. The settings table is unchanged — it describes what ships, and what ships has not moved. |
| 2026-08-22 | **An existing world keeps the values in its config file.** The file's `Version` is unchanged, because the shape did not change and regenerating it would throw away real customisation — so a world created before this pass still runs at `Frequency` 8 and the old visit budget until someone moves them, and the menu's **Defaults** button is the one action that takes it to the new values. |
| 2026-08-22 | **Removed the five settings profiles.** There is one configuration now, and it is the most faithful one the model has: every mechanism on, no approximation switched on for anybody, `SimulationSpeed` 1 and `Frequency` **4** against the 8 it shipped at. `MaxSubstepsPerBlock` stays 0 and `MaxSubsteps` 64, so nothing is refused the substeps it asks for. `/thermal profile` is gone and the menu's profile buttons are one **Defaults** button, which is what applying a profile was actually being used for. `TheDefaultsAreTheMostFaithfulConfiguration` holds the claim so it cannot quietly stop being true. |
| 2026-08-22 | Moved `MaxElementVisitsPerStep` from 1,000,000 to **2,000,000** with `Frequency`, keeping the per-frame cost identical rather than the per-step one. A step is spread across the frames of its own window, and a frame does `budget × frameSeconds × Frequency` of work, so halving the rate halves what a given budget costs per frame — and leaving the budget alone would have throttled an 8,904-block ship to 73 % of real time where it previously ran at 100 %, which is an approximation nobody asked for. Old value kept visible here (`E11`). |
| 2026-08-22 | The tuning guidance that went to `profiles.md` last pass comes back as [What the dials trade against each other](#what-the-dials-trade-against-each-other), which is settings guidance and now has nowhere else to be. |
| 2026-08-22 | Gave up this page's second account of the profiles to [realism.md](realism.md), which is the page about them: the measured cost ladder, *Where the extremes lie* and *Designing your own* all moved, and with them a duplicate of the `HeatTimeScale` substep-demand table. What stays here is the four settings each preset sets. Replaced `MaxSubstepsPerBlock`'s sweep table with a pointer to [stiffness.md](stiffness.md#1-a-per-block-substep-cap--the-one-that-is-built), which carries the same sweep at both shipped step lengths — the copy here had the same floored-block counts against different speeds and errors, which is a table that had drifted from the one it was taken from. Cut *The settings surface, and where it is going* down to what is left of it: three of its five subsections described the menu this page already documents two screens above. |
| 2026-08-22 | Corrected two statements about `Frequency` that contradicted this page's own reference table: the prose called 4 the shipped value where the table says 8, and read a rig's demand as though it were the shipped configuration. The nested-config example showed a `MaxSubsteps` of 16 rather than the shipped 64. |
| 2026-08-22 | Absorbed `settings-redesign.md`, whose subject is this page's subject, as [The settings surface](#where-the-settings-surface-is-going), with the completed steps restated as what the menu now is rather than as a plan. Took the `Frequency` sweep from `field-tuning.md` into the section that already argued the arithmetic, so the derivation and the measurement sit together. Added the standard header and this log. |
| 2026-08-22 | Brought the loop and planet definitions into the menu as world settings, replicated and reachable from `/thermal set` and the mod API. |
| 2026-08-19 | Documented the twenty-one settings the reference had never listed — the whole `Loop*` and `Planet*` families, `MaxSubsteps` and `ClampEnvironmentOvershoot` — and added `ConfigurationDocTests`, which fails when a setting exists in one place and not the other. Let an admin change world settings from a client, over a secure channel rather than the shared one. |
| 2026-08-18 | Added the five profiles as a ladder on two axes, and fixed the clamp defect that had to be fixed before they were safe. *(The profiles were removed on 2026-08-22; the clamp fix stands — see [realism.md](realism.md).)* |
| 2026-09-09 | **The aerodynamic model ships on: `EnableDrag`, `EnableShapeDrag`, `EnableLift` and `EnableTopSpeed` are all on by default, and `DragCoefficient` moves 0.5 → 1.54 with them.** Every one of them was built, measured and scored against a criterion registered before its walk, and every one was left off pending a decision rather than evidence; the decision is taken. **The coefficient is not a separate change** — the shape term multiplies the projected area by a `sin²θ` whose population median is 0.3250, so 0.5 would have made every ship in the game three times slipperier than the coefficient was ever measured to make it, and 1.54 is what holds the population where `K22` scored it (3.42 % and 78.2 m/s against a 5 % and 60 m/s criterion). **What a player will notice**, in order: the world speed cap becomes `SpeedLimit` 140 m/s and each ship gets a cruise speed from its own mass; air slows ships down and pushes them sideways a little; and hulls run a median **4.56 K cooler** in atmosphere than they did, because the shape term takes friction off as well as drag. `EnableWindwardShielding` stays **off** — it was not part of the configuration `K22` scored, and shipping it on would ship a population figure nobody has measured. The one shape the term reads badly is a bare one-cell plate ([backlog.md](backlog.md) `K24`). |
| 2026-09-09 | **Two defects in the top-speed port, both found by the aero wiring trace on 2026-09-02 and both fixed.** `EnableSpeedBoost` was **inert**: the switch was read one line after a `speed <= cruise` return had already made its condition unreachable, so all three documented rungs behaved as the third and no world ever got the hard cap at cruise. What it decides is the ceiling handed to the engine, and that choice now lives in `TopSpeedForce.Ceiling` where `TopSpeedForceTests` can ask what the switch changes — `ThermalGridTopSpeed` needs a session, a physics component and a grid group, which is why nothing caught it. And `LargeGridMaxBoostSpeed` / `SmallGridMaxBoostSpeed` were documented and labelled as **newtons**: the value is `AddForce`'s `maxSpeed`, which clamps a body's velocity, so they are **m/s** and always were. The numbers did not move; what they are called did. |
| 2026-09-01 | **RelativeTopSpeed absorbed** (`K10`): the world's speed cap, the per-mass cruise curve and the boost are world settings on a new **Top speed** page, and a server-side force holds each physical group under the cruise speed its combined mass earns. Off by default, like the drag switch and for the same reason. The curve is the original's arithmetic, quirks included, so a world moving over flies the same. |
| 2026-09-01 | **Ctrl and a click on a slider turns it into a field**, for the ranges a slider can nearly divide but not exactly. |
| 2026-09-01 | **Thirteen settings became four**, at the same behaviour a fresh install ships. `ShadowDetail` replaces five solar-occlusion switches with one level ordered by cost; `ClampOvershoot` replaces the two overshoot clamps, which were the same decision asked twice; `LoopFlowRate` and `LoopContactMultiplier` replace two pairs that shipped identical; the per-pipe coolant override is gone, since zero meant "use the density" and `Loops.xml` still carries it; and `SimulationSpeed` is gone, because it only ever multiplied `Frequency`. The solver keeps every one of the switches these derive, and the definition files keep the pairs. The config version moved to 8, so an existing world is replaced with defaults rather than half-migrated. |
| 2026-09-01 | The menu is fifteen pages instead of twenty-three — five pages that held a single switch each folded into the page whose mechanism they belong to — and each page is in two tiers, with the dials a world tunes once or never below an **Advanced** line. Every label carries its unit where it has one, and every tooltip is one sentence: the longest was five, and the terminal's clipping was what had kept the labels short. |
| 2026-09-01 | Kept one page in the Rich HUD terminal, holding a button that opens the window and the keystroke that does the same. The mod had disappeared from the terminal's mod list entirely, which reads as a mod with nothing to configure. |
| 2026-09-01 | Took the menu out of the Rich HUD terminal and into a window this mod draws itself. The terminal put a fixed bordered tile around every control, with a scroll bar under each row, and offered the client no way to turn either off — and a section longer than three controls overran the box and drew over itself. Same pages, same settings, same keystroke; the panels are gone and a setting sits on the page. |
| 2026-08-17 | Moved the settings menu onto Rich HUD at Ctrl+Shift+S. |
| 2026-08-12 | Opened the reference against `ThermodynamicsConfig.cfg`. |
