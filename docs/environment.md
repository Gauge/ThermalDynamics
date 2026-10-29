# Environment

The air, ground, sun and wind outside a grid: what each is at a given place and hour, how it is
computed, and what evidence stands behind every figure. Everything here is a property of the world
rather than of the ship in it — the ship's response to it is in
[thermal-model.md](thermal-model.md#environment-inputs).

> The rules argued here are stated canonically in [rules.md](rules.md): `E3` `E5` `E7` `D6`.

| Looking for | Go to |
| --- | --- |
| How a grid *responds* to these figures — radiation, convection, friction | [thermal-model.md](thermal-model.md#the-mechanisms) |
| The settings that scale every term | [configuration.md](configuration.md#environment) |
| The dumps that measure it | [telemetry.md](telemetry.md#climate-dump) |
| The per-planet figures themselves | [Planets.xml](../Thermodynamics/Content/Data/Planets.xml), derived in [Planet properties](#planet-properties) |

---

## What the solver computes

One sample per grid per environment refresh, from position, weather and the hour. Every factor is
`1` when it has nothing to say, so a reading is attributed by reading the columns rather than by
arguing about them.

```
ambient   = target(latitude, ground, altitude, air, weather, depth)
            lagged once, last, by AmbientLagShareOfDay x dayLength

speed     = ceiling(planet, altitude)          // the engine's rating, used as a scale only
          × band(latitude, weather, place)     // WindField    — circulation and weather
          × profile(height above ground)       // WindProfile  — logarithmic rise
          × diurnal(heating, height)           // WindProfile  — daily cycle, reversing with height
          × speedUp(relief)                    // WindTerrain  — exposure
          × shelter(upwind horizon)            // WindTerrain  — wind shadow
          × burial(height above ground)        // WindSolver   — fades to nothing under the surface
          + slope(fall line, hour, height)     // WindSlope    — added as a velocity, not a factor

bearing   = band(latitude)                     // WindField    — the circulation
            steered by channel(valley axis)    // WindTerrain
            turned by the slope wind added to it
```

The ambient is a target chased by a first-order lag, and **the lag is applied once, to a finished
target**. Applying it to a partially scaled value compounds it against itself on every step;
`ThinAirDoesNotCompoundAgainstTheLag` pins the ordering.

`EnvironmentSample.ComposeRelativeWind` is the single place the ambient wind and the grid's own
velocity meet. Friction, its threshold and the forced-convection bonus read nothing but its result,
so a hull flying with the wind at the wind's own speed is in calm air at full ground speed, and one
flying into a 40 m/s wind at 40 m/s trips the 50 m/s friction threshold that neither reaches alone.

---

## Moving grid shadows

Full grid shadows carry rays from receiver cells into blocker cells. The transform therefore
contains the receiver grid size divided by the blocker grid size: `0.2` for a small grid shaded
by a large grid, `5` in the opposite direction, and `1` for matching sizes. Direction comparisons
normalize this scale before applying the existing `0.9994` cosine threshold. Previously an
unchanged `0.2` transform looked changed every refresh, while a `5` transform could hide rotation.
Translation still invalidates beyond one blocker cell; scale changes also invalidate because they
change ray coordinates, allowing only relative squared-length roundoff of `1e-10`. Degenerate or
nonfinite transforms invalidate conservatively.

Each adapter cache also captures the blocker's model version when it publishes the transform.
Adding or removing a blocker block, or replacing its model under the same entity ID, invalidates
the receiving grid's shadow on its next occluder refresh. Reading the version from two live model
references would miss those changes. `ThermalOccluderTransformTests` covers three cell scales and
one blocker with no native world or render loop.

Disabling solar heat, self-shadowing or full grid shadows clears the adapter's blocker list and
captured versions on the next ordinary environment sample, including samples that skip solar rays.
The native overlap query still runs only on the established occlusion-test cadence. Temporary
entity-overlap and blocker-model lists are cleared after their final use, including failed native
queries, while retaining list capacity for reuse.

`MarkSunOccludersChanged` also releases the sun and wind maps' copied blocker references and
parks any unfinished ray pass. It retains the last completed map and cached skin, so pending
per-node publication, including `FinishSunLit`, can still finish against that completed result.
The next active shadow update restarts with the current blocker list and reuses the skin when the
receiving topology is unchanged. This also releases references while solar heat is disabled or
wind is calm, without waiting for either mechanism to run. A two-block `SunShadowReuseTests` case
covers both maps, interrupted rays, old-result publication and the subsequent unblocked result.

## Ambient temperature

`ClimateModel.Target` gives a place the temperature it is heading toward; `ClimateModel.Follow`
chases it.

[EnvironmentSolver](../Thermodynamics/Core/Simulation/EnvironmentSolver.cs) is a pure
function from a host sample to the state a step consumes:

```
weather   = weatherAtFullStrength faded by weatherIntensity
offset    = groundOffset + weather.temperature
swing     = groundSwing × (0.5 + 0.5 × weather.solar)

drop      = PoleTemperatureDrop × (1 − cos(latitude))
mean      = (NightTemperature + DayTemperature)/2 − drop
half      = (DayTemperature − NightTemperature)/2 × swing

target    = mean − half + 2×half × max(0, sin(sunElevation)) + offset
target    = target − AmbientLapseRate × altitude/1000
target    = VacuumTemperature + (target − VacuumTemperature) × (1 − (1 − density)⁸)
target    = depth > 0 ? underground(target, depth, radius) : target

lag       = AmbientLagShareOfDay × dayLength            // measured from the sun; see below
ambient   = hasHistory
              ? ambient + (target − ambient) × (1 − e^(−dt / lag))
              : target
ambient   = max(VacuumTemperature, ambient)
```

### The lag is a share of the day

A first-order lag needs a time constant, and it was 45 absolute seconds against a rotation a server
sets to anything. That attenuates a four-minute day to less than half of its intended swing and does
essentially nothing to a default two-hour one, so **the same authored figure meant a different
climate on every world it was applied to**. Physically it is a fraction: Earth's air peaks about two
hours after noon out of twenty-four, which is where `AmbientLagShareOfDay` = 0.083 comes from.

**The day is measured, not asked for.** `MySectorWeatherComponent.RotationInterval` exists and would
answer directly, but whether the script whitelist admits it cannot be established outside a session
and a type the in-game compiler rejects takes the mod down at world load. The sun's direction is
already sampled once a frame for the solar term, and the angle it sweeps between two samples is the
same fact — so [DayLength](../Thermodynamics/Core/Simulation/DayLength.cs) needs no new
type, survives a server changing the interval mid-session, and works on a world whose rotation
nothing publishes. It offers nothing until the sun has swept a tenth of a turn, and until then the
authored `AmbientLagSeconds` runs, which is also what a world with a static sun gets.

Two details are numerical rather than physical, and both were found by the test rather than
reasoned: the swept angle is `atan2(|a × b|, a · b)` and not `acos(a · b)`, because a frame moves
the sun about a thousandth of a radian and the cosine of that is one to within a few float epsilons;
and a sample claiming to have taken more than a second is dropped, because a world load or a paste
moves the sun as far as many ordinary frames and would read as a much faster rotation.

**Everything is a target, and the lag is applied to it exactly once, last.** That ordering is
load-bearing rather than stylistic: scaling the *running* ambient compounds the scale against the
lag on every step. A factor of 0.977 applied four times a second against a 45-second lag settles at

```
f·k / (1 − f + f·k)   where k = 1 − e^(−dt/τ)
```

which is 14% of the intended temperature rather than 98% of it.

`hasHistory` is false on the step a grid arrives at a planet, when the only previous ambient
available is the vacuum every state is seeded with. Chasing a 290 K climate up from 2.7 K at 45
seconds a decade takes three minutes of play, during which every block on the ship is dragged toward
absolute zero.

With no planet nearby, or with planets switched off, ambient is `VacuumTemperature` (2.7 K) and
there is no convection.

| Term | What it does | Setting |
| --- | --- | --- |
| **Latitude** | A planet's `DayTemperature` and `NightTemperature` are its *equatorial* figures. `PoleTemperatureDrop` is the span to its poles, interpolated on cos(latitude) because that is how squarely the sun strikes a band. Zero gives one climate for a whole world. | `PlanetPoleTemperatureDrop` |
| **Ground** | The voxel material under the grid shifts the air and widens or narrows its day ([GroundTemperature.cs](../Thermodynamics/Core/Definitions/GroundTemperature.cs)). Snow −14 K at 0.7× swing; sand +8 K at 1.7×, because dry ground holds nothing overnight. Matched on the *word* in the material name rather than the exact subtype, so other worlds' spellings land; anything unrecognised leaves the planet untouched. | `ClimateGroundInfluence` |
| **Lag** | First-order, applied once to a finished target. The day's peak then lands after noon and the low before dawn without either being written down. **A share of the day rather than a count of seconds** — see [The lag is a share of the day](#the-lag-is-a-share-of-the-day). | `PlanetAmbientLagSeconds` |
| **Altitude** | A lapse rate cools the air as it rises, which is what makes a mountain colder than its valley. | `PlanetAmbientLapseRate` |
| **Thin air** | Ambient fades toward vacuum on `1 − (1 − d)⁸`, blunter than the `1 − (1 − d)⁴` curve convection and solar decay run on: density decides when there stops being air to have a temperature at all, and that happens at the edge of space rather than gradually all the way up. | — |
| **Weather** | Colder in rain and snow, warmer in a sandstorm; the sun dimmer, the wind the weather's own, and wet air stripping heat faster. See [Weather](#weather). | `ClimateWeatherInfluence` |
| **Depth** | Underground the day damps out over tens of metres, then the rock warms toward the core below the sea-level deadzone. See [Underground](#underground). | `PlanetUndergroundDampingDepth` |

### Altitude and density are two facts, not one

**`AmbientLapseRate`** does the work of altitude: air cools as it rises because it expands, at
6.5 K/km on Earth and a default 4 K/km here, because the ground table already makes mountains snowy
and the two at full strength put a 5.6 km peak near −50 °C.

**Density** decides something else entirely — when there stops being air to have a temperature at
all. That happens at the edge of space rather than gradually all the way up, so ambient runs on
`1 − (1 − d)⁸` rather than the `1 − (1 − d)⁴` `atmosphereFactor` that convection and solar decay
use. At two thirds density it is still 99.9%; half is gone by a twelfth. The top of Earth's
troposphere holds a third of sea level's air and sits at 217 K, not at a third of 288.

### Three reference sites

Predicted in clear air for the three sites the model is regression-checked against, beside the real
places they resemble:

| Site | Latitude | Altitude | Night | Noon | Swing | Resembles |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| Desert, `Sand_02` | 6.9° | −42 m | 14.0 °C | 32.9 °C | 18.9 K | Sahara, 5–38 °C |
| Grass, `Grass bare` | 24.5° | 293 m | 5.2 °C | 16.3 °C | 11.1 K | temperate, 8–22 °C |
| Snow, `Snow` | 40.7° | 5,588 m | −34.5 °C | −26.7 °C | 7.8 K | high alpine, −25 to −15 °C |

**The snow row is the one to check first, and the one most likely to be too cold.** The lapse rate
and the snow ground offset both say "it is cold up here" and only one of them has evidence behind
it — see [Limits](#limits-and-open-questions).

---

## Weather

`MyVisualScriptLogicProvider.GetWeather` returns the effect's subtype name, and every effect in the
game's `WeatherEffects.sbc` carries three authored modifiers:

| | `TemperatureModifier` | `SolarOutputModifier` | `WindOutputModifier` |
| --- | ---: | ---: | ---: |
| `SnowHeavy` | −2 | 0.10 | 2.00 |
| `RainHeavy` | 0.4 | 0.30 | 1.45 |
| `SandStormHeavy` | 3 | 0.10 | 2.25 |
| `FogHeavy` | 0.3 | 0.15 | 0.10 |
| `ExtremeHeat` | 2 | 1.75 | 0.10 |

[WeatherResponse.cs](../Thermodynamics/Core/Definitions/WeatherResponse.cs) is that
table, converted. The two multipliers pass straight through — one onto solar irradiance, one onto
the wind the field produces. `TemperatureModifier` is a factor on the game's 0..1 comfort figure
rather than a temperature, so it becomes kelvin as `clamp(m − 1, −3, 3) × 6 K`: heavy snow lands at
−18 K, a sandstorm at +12 K. The clamp exists for the alien weathers, which run to 11.

Matching is on the kind word rather than the exact subtype, exactly as the ground table is. There
are 33 weathers in the base game and most are the same handful of kinds with a prefix —
`AlienRainHeavy`, `MarsStormLight` — so thirteen words cover all of them, and a mod that adds
`RainHeavier` gets rain behaviour without annotating anything. A name carrying `light` gets half the
departure from calm, which is about what the game's own light/heavy pairs differ by. Order matters
in one place: `lowwind` is tested before `wind`, or `LowWinds` reads as a gale.

Two responses are this mod's own rather than the game's, and both are opinions in the way the ground
table is:

* **Convection.** Nothing in the definitions records that rain is *wet*, and wet air pulls heat off
  a hull far faster than dry air of the same speed — a fog barely moves and still carries heat away,
  which the wind term alone cannot express. Heavy rain 2.5×, hail 2.8×, snow 2.2×, sandstorm 1.4×,
  fog 1.3×.
* **The swing.** Cloud that keeps the sun off by day keeps the heat in at night, so it is one fact
  and not two: the day-night swing follows the solar multiplier rather than getting a column of its
  own, halving under full overcast and untouched in clear air.

Converted, the five kinds above land at:

| | heavy snow | heavy rain | sandstorm | fog | heat wave |
| --- | ---: | ---: | ---: | ---: | ---: |
| ambient | −18 K | −3.6 K | +12 K | −4.2 K | +6 K |
| solar | ×0.10 | ×0.30 | ×0.10 | ×0.15 | ×1.75 |
| wind | ×2.00 | ×1.45 | ×2.25 | ×0.10 | ×0.10 |
| convection | ×2.2 | ×2.5 | ×1.4 | ×1.3 | ×1.0 |

Intensity fades the whole response in from calm, so a weather at a tenth of strength is a tenth of
the way toward itself rather than all of it a tenth of the time.

`ClimateWeatherInfluence` scales the lot, and `0` leaves weather affecting nothing but the wind.
Clear air costs one float compare: an intensity of zero softens the whole response to calm, whose
every term is the identity.

---

## Underground

```
buried    = min(1, depth / UndergroundDampingDepth)
ambient   = surface + (UndergroundTemperature − surface) × buried

deadzone  = meanRadius − SealevelDeadzone
descended = max(0, 1 − radius / deadzone)
ambient   = ambient + (CoreTemperature − ambient) × descended
```

Two things happen going down, at very different scales.

**The day stops.** Rock is slow, so the further into it a tunnel goes the less of the surface's
swing reaches it. `PlanetUndergroundDampingDepth` (20 m) is where that finishes: at the surface a
buried block feels the whole day, halfway down it feels half, and below it there is no day left and
the air is simply `UndergroundTemperature`. Sun, weather and the ground table all damp out with that
one term, for free — a cold night and a hot noon converge on the same rock, which is the point of
it.

**The planet is hot inside.** Below `SealevelDeadzone` the rock warms linearly toward
`CoreTemperature`, reaching it at the centre. On an earthlike world's 60 km radius with the shipped
3000 K core that is about 46 K/km, roughly twice Earth's crustal gradient.

**The deadzone is the world's own deepest ground**, not a round number. `HillParams.Min` times the
radius is the depth of a world's lowest natural valley floor, and below that there is nothing but
rock — so the flat band is exactly the band where ground can be, and a shaft driven under the lowest
natural surface starts to warm. It ranges from **285 m** on the small moons through 600 m on the
earthlike worlds and 750 m on Pertam to **2 km on Triton**, which really is that deep.

It used to be 2 km everywhere, and that put the core term out of reach in ordinary play on seven of
the eight worlds — the field dump's buried rows, 1 to 99 m down, all read a flat
`UndergroundTemperature`. At the derived depths a shaft 500 m below the deadzone reads about
**+23 K**, which is a base a player would notice being warm.

The deadzone is measured from **sea level** and not from the surface, which is the detail that makes
it behave: a tunnel bored a kilometre into a mountainside stays cold however far in it goes, because
it is deep in the rock and still four kilometres above the hot part, while a shaft sunk from a beach
reaches the same depth and starts warming.

Depth costs nothing. The surface height under a grid is already looked up for the ground material
and cached on the same 40 m movement rule, so between refreshes depth is the difference of two radii
— a subtraction, exact for a shaft sunk straight down, and anything moving far enough sideways for
it not to be has already tripped the resample.

**Rock is a far worse heat sink than air.** A buried grid used to exchange at the planet's own
*convection* coefficient — the figure for moving air, 50 W/(m²·K) — because rock contact was not
modelled and that was the nearest number available. It was wrong by a factor of twenty-five and in
the flattering direction: digging in was the best cooling in the game. For a body of size D buried
far from a surface the conduction shape factor gives `h = 2k/D`, and rock at 2.5 W/(m·K) over a
2.5 m block is **2 W/(m²·K)**. That is `UndergroundConvectionCoefficient`, authored per planet like
its surface counterpart.

```
inRock  = min(1, depth / UndergroundContactDepth)          // 5 m, a hull's own depth
h       = air + (rock − air) × inRock
```

A crossover rather than a step, because a ship breaking the surface should not have its cooling
change twenty-five-fold between one metre and the next. Neither the wind bonus nor the weather
multiplies the rock end: there is no wind under fifty metres of stone.

---

## Wind

The engine supplies a scalar rating and no direction (see
[What the engine supplies](#what-the-engine-supplies)), so the wind is modelled outright.
[WindSolver.cs](../Thermodynamics/Core/Simulation/WindSolver.cs) composes six factors
and one added velocity over the engine's rating.

**It is a map, not a simulation.** The properties worth keeping are that it is steady, cheap,
different in different places, and recognisable from a cockpit.

### Circulation

[WindField.cs](../Thermodynamics/Core/Simulation/WindField.cs) lays Earth's bands over
the planet — trades blowing west to 30°, westerlies to 60°, polar easterlies beyond. Speed is a
fraction of the ceiling: about an eighth in fair weather, half in the worst weather the game
reports, times a steady per-place variation so one valley is windier than the next.

**The weather's own wind modifier scales how fast the share climbs, not the finished share.** The
intensity says how much weather there is and the modifier says what kind it is, so multiplying one
by the other counted the same storm twice — a sandstorm's 2.25 on a share that already meant *the
worst weather* put the wind past the planet's own ceiling before the profile or the terrain had
touched it. A windier kind now reaches the storm share at a lower intensity rather than passing it,
a modifier of 1 is exactly neutral, and the whole spread between the windiest and the stillest
weather is `StormFraction / CalmFraction`, because the calm share is a floor.

**One signal decides both components.** `sin(6 × distance from the equator)` is +1 in the middle of
a westward band, −1 in the middle of an eastward one, and zero at the equator, at 30°, at 60° and at
the pole. The zonal component is that signal; the meridional component is the same signal times a
tilt, so the sideways term cannot outlive the along-track one and reverse on its own. The tilt is
the tangent of thirty degrees at its largest, which is about how far off due east or west Earth's
trades and westerlies actually run, and it fades to nothing at the equator and at the pole.

**A band edge is a calm, not a seam.** The signal is zero there, so `BandStrength` is zero, so there
is no wind — the doldrums, the horse latitudes and the polar front. The direction does reverse
across those latitudes, because the band on the other side blows the other way; what makes that a
turn rather than a wall is that the wind has died before it happens. `WindField.Direction` is a unit
vector and reverses; the *velocity*, which is what a ship stands in, passes smoothly through zero.
A consumer must multiply the two — `WindSolver` does, and `TheWindSwingsThroughTheCalmsRatherThanReversingAcrossALine`
measures the product rather than the bearing, which is what it was doing when it missed `B14`.

**This replaced a rotating bearing**, which kept the direction continuous by construction but could
only express a meridional component pointing one way: poleward. Air therefore diverged from the
equator where Earth's trades converge, and the wind flipped end for end across latitude 0 at full
strength.

### The vertical profile

In the atmospheric surface layer wind speed rises logarithmically with height:

```
u(z) = (u* / κ) · ln(z / z₀)
```

`u*` is the friction velocity, `κ ≈ 0.4` the von Kármán constant, and **z₀ the roughness length** —
the height at which the wind extrapolates to zero, roughly a tenth of the height of the obstacles
covering the ground. Open water is 0.0002 m, grassland 0.03 m, forest or a built-up area about
0.5 m. Expressed against a reference height the constants cancel:

```
f(z) = ln((z + z₀)/z₀) / ln((z_ref + z₀)/z₀)
```

**z₀ comes from the ground the grid is standing on.** The voxel material under a grid is already
classified for the temperature model, and roughness is the one figure in that table with a published
table behind it rather than a judgement — ice 0.0002 m, snow 0.0005, sand 0.003, grass 0.03, broken
rock 0.05, forest 0.5. It costs nothing: the material was already read for the temperature offset
and is cached with it. `WindRoughnessLength` is what ground the table does not classify gets, which
is an airless world, a modded voxel, or a grid over no surface at all.

Ten metres is the reference, because that is the height the world's weather stations measure at. The
power law `u ∝ z^α` with `α = 1/7` is the common shortcut and the wrong one here: the 1/7 exponent
is only reasonable over open ground, and gives poor answers exactly where terrain and cover matter.

The profile holds through the surface layer and no further. Above the boundary layer the wind is set
by the pressure field rather than by friction with the ground, so the curve flattens at
`WindGradientHeight`. Combined with the engine's density falloff the full shape is a rise, a
plateau, and a fall.

### The daily cycle, which runs two ways at once

By day the sun heats the ground, convection mixes the boundary layer, and momentum from aloft is
dragged down to the surface, so **surface wind peaks in the afternoon**. After sunset the ground
cools, the mixing stops, and the air above **decouples from the surface friction that was holding it
back**. It accelerates — often past the geostrophic speed — into a **nocturnal low-level jet**,
typically 50–1000 m up, peaking in the pre-dawn hours, while the surface below goes calm. This is
the Blackadar mechanism; the Holton mechanism adds a contribution from sloping terrain.

Surface wind and wind aloft are therefore **anticorrelated over the day**, and between them is a
crossover height (`WindDiurnalCrossover`) where the daily variation vanishes. A player who lands at
dusk into still air, climbs to two hundred metres and meets a gale is seeing the documented
behaviour of a real atmosphere.

The phase matters: the peak is mid-afternoon rather than noon, because it follows the *ground* being
warm rather than the sun being high. The model gets that free by driving the cycle through the same
first-order lag the ambient temperature uses, so the windiest part of the afternoon lines up with
the warmest part by construction — no clock, sun-rotation interval or day-length setting is needed.

### Slope winds

Every other terrain effect is **mechanical** — what terrain does to a wind that was already blowing.
Slope winds are **thermal**: the ground creates them out of sunlight and gravity, and they blow on a
day when nothing else does.
[WindSlope.cs](../Thermodynamics/Core/Simulation/WindSlope.cs) adds them as a velocity
rather than a factor, from the terrain ring's first harmonic.

* **Anabatic, by day.** Sunlight heats a slope; the air against it warms, becomes buoyant, and rises
  *along the ground* rather than straight up, so the flow runs toward the summit. **3–5 m/s**,
  hundreds of metres deep, starting after sunrise and strongest in the afternoon.
* **Katabatic, by night.** The slope radiates heat to the sky; the air against it cools, grows
  dense, and drains downhill to pool in the valley. **3–8 m/s** — up to 50 over Antarctic ice — and
  **far shallower, 10–100 m**, roughly a twentieth of the drop it has fallen.

Both are weak-wind phenomena: they form under calm, clear, high-pressure conditions and a real
synoptic wind overruns them, so the model suppresses them as the ambient wind rises. They are
bounded by the ceiling, so they are not a new way through it. `WindSlopeStrength` scales them, and
they cost **+46 ns a sample** because the terrain they need is already read.

### Terrain

Three mechanical effects, all from one ring of **eight compass bearings at two radii — sixteen
height lookups** — each giving the ground's height relative to the site's own
([WindTerrain.cs](../Thermodynamics/Core/Simulation/WindTerrain.cs)):

| Effect | Read from the ring as | Physical basis |
| --- | --- | --- |
| **Speed-up** | the **mean**: the site standing proud of the land around it, as a slope | Air driven over a rise is squeezed between the hill and the flow above and must accelerate. Linearised flow theory — Jackson and Hunt, the basis of WAsP and every wind atlas since — gives a fractional speed-up of roughly `2H/L`. UK summits commonly run two to three times the wind of the valley below. |
| **Shelter** | the **upwind arc**, interpolated between the two samples straddling the upwind bearing, taking the steeper of the two radii so a near obstruction shelters more than a far one of the same height | Behind a ridge is a wind shadow. The natural measure is the greatest upward angle from the site to the terrain upwind of it — the `Sx` index used in snow science. |
| **Channelling** | the **second harmonic**: fit `h(θ) ≈ mean + A·cos(2(θ − φ))`, the shape a valley or ridge makes when you walk a circle round a point in one | Wind is channelled along valleys and through gaps, and this modifies direction substantially, almost always into the along-valley direction. Where a valley narrows the flow accelerates through the constriction. |

A harmonic rather than picking the lowest of four opposite pairs, because pairs quantise the answer
to 45° and a wind that snaps between eight directions as you walk looks like a bug.

**The fall line is the ring's *first* harmonic**, where the valley axis is its second, and the two
are genuinely different shapes: a symmetric valley is high on both sides and so has no first
harmonic at all — correctly, since a valley floor has no single downhill direction — while a
hillside has no second. Fitting both means a site on a valley wall gets an along-valley axis *and* a
fall line, which is what it really has. It costs eight multiply-adds over heights already in memory.

**The channelling rule is "turn toward the lowest ground", and it deliberately gives two different
answers.** In a valley the lowest ground is along the floor, so the wind runs along it. On a ridge
crest the lowest ground is down either side, so the wind is left crossing the ridge — which is what
air going over a ridge does. A ridge is not a valley upside down as far as steering goes.

The ring is re-read only when a grid has moved a quarter of `WindTerrainRadius`, so sixteen lookups
buy a few hundred metres of travel. All three effects fade linearly to nothing at
`WindGradientHeight`: they are surface-layer phenomena, and steering a ship two kilometres up along
a valley it is nowhere near would be worse than not modelling terrain at all.

### Under the surface

Height above ground is signed, and `WindSolver.Burial` turns it into the share of the wind that
reaches the grid: 1 at or above the surface, 0 once the whole body is under, linear between. The
fade length is the grid's own reach — half its bounding box — supplied by the adapter, so a large
ship in a shallow scrape still blows and a small one in a shaft does not.

The fade is over the hull rather than at the rim because a grid is a body and the height is measured
at its centre: a ship in the trench it has just dug has its midpoint under the surface and its deck
still open to the sky, so a test on the centre alone would switch the wind off while half the hull
was still in the open. The game's `IsUnderGround` answers for a point and is not what decides this —
it reads true the moment a grid's centre passes the surface, which is exactly the case the fade
exists for.

`wind_burial` in the environment dump is the share that survived;
`dotnet run --project tests/Thermodynamics.Sim -- descent` prints the whole descent.

### Looking at it

**Ctrl+Shift+W** cycles a wind map: a lattice of arrows around you, then the whole globe with its
bands. A needle under the crosshair gives the wind where you are, and `DebugWindRaycast` draws the
relative wind each grid is flying through — the same field minus the grid's own velocity. All three
are described in [configuration.md](configuration.md#the-wind-map).

That view is where any argument about this pattern should start. The band count, the rotating
bearing, the eighth of the ceiling in fair weather and the 900 m variation scale are choices, not
findings, because the game supplies nothing to fit them to. They were picked to be plausible and
cheap.

---

## Planet properties

Every figure in [Planets.xml](../Thermodynamics/Content/Data/Planets.xml) is derived from the world's own generator
definition by
[PlanetThermalDerivation.cs](../Thermodynamics/Core/Definitions/PlanetThermalDerivation.cs),
which is pure and tested and serves twice: it generates the shipped entries offline, and it is the
fallback for a **modded** planet nobody has authored an entry for. The property names and defaults
are in [definitions.md](definitions.md#group-thermalplanetproperties).

### The derivation

| Figure | How |
| --- | --- |
| **Mean temperature** | Five authored anchors, one per level: **100 K** (Titan 94, Europa 102), **215 K** (Mars), **288 K** (Earth), **325 K** (a hot desert), **450 K** (between Mercury's day side and Venus). Not evenly spaced — spread evenly, `Cozy` would land at 275 K, below freezing. |
| **Day–night swing** | From air. 11 K with a full atmosphere (Earth's equatorial range), 220 K with none (the Moon runs 100 K to 390 K). Falls off faster than linearly, because the first tenth of an atmosphere does most of the damping. |
| **Lapse rate** | `Γ = g/c_p`, times two thirds for the environmental rate. **The one true derivation here.** An earthlike world comes out at 6.44 K/km against Earth's measured 6.5. `c_p` is 1005 for breathable air and 850 for unbreathable, taken as CO₂. |
| **Pole drop** | From air, which is what carries heat polewards: 40 K with a full atmosphere, 120 K with none. |
| **Ambient lag** | Scales with air — a bare rock answers the sun almost at once. 45 s at full density, floored at 5. |
| **Solar decay** | The engine's `SolarRadiationProtectionFactor`, scaled so 1.8 → 0.30, about what Earth's atmosphere really absorbs and scatters. Mars's 0.2 → 0.03. |
| **Convection** | 50 W/(m²·K) in proportion to air density; zero on an airless world. |
| **Interior** | Mostly not derived. Nothing in a planet generator definition says anything about a planet's *inside*, so damping depth and core temperature carry the defaults through. The **sea-level deadzone is derived**: it is `HillParams.Min × radius`, the world's own deepest natural ground, because below the lowest place ground can be there is nothing but rock. |

### Its inputs

Every figure read from `PlanetGeneratorDefinitions.sbc`, with the object builder's defaults filled
in where a field is not authored (`Density 1`, `OxygenDensity 1`, `LimitAltitude 2`,
`SolarRadiationProtectionFactor 1`, `DefaultSurfaceTemperature Cozy`):

| World | Level | Gravity | Air | Breathable | Solar protection |
| --- | --- | ---: | ---: | --- | ---: |
| EarthLike | *Cozy (default)* | 1.00 g | 1.00 | yes | 1.80 |
| Alien | *Cozy (default)* | 1.10 g | 1.20 | yes | 1.80 |
| Mars | *Cozy (default)* | 0.90 g | 1.00 | no | 0.20 |
| Pertam | **Hot** | 1.20 g | 1.00 | yes | 0.75 |
| Triton | **ExtremeFreeze** | 1.00 g | 1.00 | yes | 1.80 |
| Europa | **ExtremeFreeze** | 0.25 g | 1.00 | no | 0.00 |
| Titan | *Cozy (default)* | 0.25 g | 1.00 | yes | 1.80 |
| Moon | *Cozy (default)* | 0.25 g | **none** | – | 1.00 |

Two things follow. **Every world with air has density exactly 1**, so anything derived from air
density gives the same answer for seven of eight and only the Moon differs. And **SE's Triton is a
breathable, full-density, 1 g world** with nothing in common with the real Triton but its name.

**This mod does not author its own densities against the engine's, and will not.** The temptation is
obvious — a Titan at full density is a world with no thin air anywhere to test convection against —
but air density is a thing the game already decides and `C9` says this model reads the game's answer
rather than forming its own. Overriding it would mean a grid whose air density disagreed with the
oxygen system, the wind ceiling and the jetpack, all of which read the engine's figure, and the
disagreement would surface as three mechanics contradicting each other rather than as a better
climate. **What is left is not a gap in the model.** The differentiation between atmospheric worlds
really is gravity and solar protection alone, because those are the only two things the definitions
vary — and a planet pack that authors a thin atmosphere gets everything derived from density for
free, since none of it is hard-coded to 1.

### Where the shipped entries depart from the derivation

**The rule: an authored level is followed; an unauthored one is not treated as intent.**

Where a definition authors `DefaultSurfaceTemperature`, the game has made a decision and it stands,
whatever the real body does — SE's Triton is breathable with 1 g and full air, the definition says
`ExtremeFreeze`, so it is 100 K and no override applies.

Where a definition is **silent** and the world is named after a real place, silence is an omission
rather than a statement. Reading `Cozy` out of it would put Titan at 288 K and have players landing
on an ice moon in shirtsleeves. Three worlds are anchored to their real analogues instead:

| World | Derived | Shipped | Why |
| --- | --- | --- | --- |
| **Mars** | 282–294 K | **185–245 K** | Unauthored. Anchored to Mars's measured 215 K mean and its ~60 K daily range, which its thin real air cannot damp. |
| **Titan** | 282–294 K | **92–96 K** | Unauthored. Anchored to Titan's measured 94 K, with the very small daily range a thick cold nitrogen atmosphere gives it. |
| **Moon** | 178–398 K | **100–390 K** | Unauthored. The derivation already gets the enormous airless swing right; this pins the mean to the Moon's measured extremes rather than the `Cozy` default. |

Each departure is written into the generated file beside the entry it affects, under
`DEPARTS FROM THE DERIVATION`, and `PlanetThermalTests` fails if one loses its reason.

### The file is generated

`Data/Planets.xml` is produced by `PlanetLab.Xml()`, not typed:

```bash
dotnet run --project tests/Thermodynamics.Sim -- planets                        # the table
dotnet run --project tests/Thermodynamics.Sim -- planets --write Thermodynamics/Content/Data/Planets.xml
```

`TheShippedPlanetsFileIsWhatThisCodeGenerates` fails if the file on disk drifts from what the code
produces, and names the first line that differs. The reasoning behind every figure lives in code
beside a test, and a number that cannot be regenerated is a number nobody can check.

Other tests hold that every entry carries every property the mod reads (a missing one silently takes
a reader default), that the fallback entry is byte-for-byte the earthlike climate the mod always
shipped, and that no combination of definition inputs — including ones no shipped world uses —
produces a negative, NaN or inverted climate.

### When the file does not reach the mod

The entries are read through Draygo's BlockExtensions API, which is a second mod answering on a
message. Two rules follow, and a field dump found both the hard way — an earthlike world reading
2.7 K of ambient at 0.93 air density, no convection, no solar decay, and 232,000 points of heat
damage behind it.

**A definition overrides only what it carried.** `PlanetProperties.Merge` takes only the fields the
read actually supplied; the rest keep the earthlike defaults on `PlanetThermalProperties`. Otherwise
a planet with no thermal group — or a pack authoring three values of eleven — becomes a vacuum.

**A lookup that is not up yet is not an answer.** The first grid to tick can ask before the other
mod has sent its handlers. The lookup returns null until it is ready, and nothing caches a null.

The report names both: the Climate section prints the climate each planet is being simulated with
and which fields its definition supplied, and the World section lists the mods loaded — which is
where a missing BlockExtensions shows up. `DefinitionFileTests` strict-parses everything
`definitionextensions.txt` names, so a file the importer would reject fails the suite instead of a
session.

---

## Measuring it

The model is a pure function of position, weather and the hour, so it is measurable three ways —
and all three write the **same columns**, so they can be put side by side without translating
anything. The probes and the offline model both call `WindSolver.Solve`, the same function the grids
do, so none of them can drift from the model without the model itself changing.

| Source | What it is | Reach |
| --- | --- | --- |
| **Grid telemetry** | Every grid samples its own position. `Thermodynamics_Environment_*.csv`. | Wherever somebody parked. |
| **Planet probes** | **72 fixed points** — every latitude from −80° to +80° including the equator, eight longitudes each — at five heights, on an interval, whether or not anything is standing there. Wind and climate together, since they share every input. `Thermodynamics_PlanetProbes_*.csv`. Set `TelemetryPlanetProbes` to an interval in diagnostic ticks, six per simulated second; needs `EnableTelemetry`. | The whole planet, over a real day. |
| **The offline model** | `dotnet run --project tests/Thermodynamics.Sim -- wind`. A synthetic planet with the engine's own wind function reproduced from its decompiled source, a heightmap with features from forty kilometres down to four hundred metres, and a full day at 73 times of day. Add `--csv out/`, `--flat` or `--weather 1`. | Everything, in about a second. |

### The columns

| Column | What it says |
| --- | --- |
| `wind_ceiling` | The engine's figure — `MaxWindSpeed × airDensity`. The scale everything else works against. |
| `wind_band_share` | Share of that ceiling the circulation band and the weather were blowing, 0..1. |
| `wind_agl_m` | Metres above the **ground**, signed — negative below the surface — which is what the profile is a function of, as against `altitude_surface`, which is the grid's own height. |
| `wind_burial` | Share of the wind left after being under the surface, 0..1. |
| `wind_profile` | Vertical profile × time of day, as a multiple of the reference-height wind. |
| `wind_heating` | Lagged share of the day's heating, 0..1: 0 at the coldest hour, 1 at peak afternoon. The phase of the daily cycle. |
| `wind_speedup` | Terrain exposure. Above 1 on a rise, below 1 in a hollow. |
| `wind_shelter` | Terrain sheltering. 1 in the open, less behind an obstruction. |
| `wind_channel_deg` | Degrees the terrain turned the wind away from the band's own bearing. |
| `wind_speed` | The relative wind the solver used, m/s. |
| `wind_bearing_deg` | Where it was going, degrees east of the planet's north. |
| `ambient_target_k` / `ambient_k` | The target and the lagged value, side by side on purpose: **the difference between them is the lag**, and it is the only way to see that the day's peak lands after noon rather than at it. |

Alongside `latitude_deg`, `sun_elevation_deg`, `altitude_surface`, `altitude_sealevel`,
`air_density`, `depth_m`, `weather`, `weather_intensity`, `convection_coeff`, `surface_material` and
`game_comfort`, that is enough to check every claim on this page against a real world. Audit a
dump with `dotnet run --project Thermodynamics.Sim -- dump`.

### Terrain saturates rather than clipping

Each terrain factor is a linear law from the wind-engineering literature — speed-up is the `2H/L` of
linearised flow over a hill, shelter is the angle of the upwind horizon against a full-shelter
angle, channelling is a valley's confinement against a full-channelling slope. Each was then
**clipped** to a cap, for the reason the codes cap them: the linear theory stops being true on
exactly the steep ground that produces the largest numbers.

On Earth's slopes a clip almost never binds. On SE's it binds everywhere, and a clipped model has no
middle: every rise read speed-up 1.60, every sheltered site read 0.250, every valley channelled its
limit. The caps had stopped bounding a model and become one.

Each factor is now bent onto its bound instead:

```
saturated = bound × tanh(linear / bound)
```

which keeps the two things the clip got right and restores the one it did not. The gradient at zero
is the literature's own, so gentle ground is unchanged to within a per cent and the borrowed
constants still mean what they were measured to mean. The bound is the same figure, so nothing that
relied on it moves. And in between, a ridge twice as steep as another is no longer the same ridge.

It is also closer to the flow: a crest steep enough separates, and the speed-up stops growing rather
than stopping suddenly.

**One consequence is worth stating.** A bound approached is never reached, so the nominal floor is
not a value the terrain can sit on — the worst shelter any ground can produce is `1 − 0.75 ×
tanh(90/40)` = **0.267**, not 0.250, because an upwind horizon cannot stand at more than ninety
degrees.

### The scenario matrix

`dotnet run --project tests/Thermodynamics.Sim -- wind scenarios` runs the wind model over **28
scenarios** — every shipped world at its usual size, four sizes plus two modded extremes, ten
settings pushed to their ends, and four degenerate worlds (airless, no wind rating, flat, and a day
shorter than the lag that follows it) — at fifteen latitudes chosen to hit the equator, the band
middles and the band edges exactly, and twelve heights from the ground to 20 km. About three million
samples. `WindScenarioTests` runs the same matrix at a coarse day as part of the suite, and holds:

* **not one NaN, infinity or negative wind anywhere** across the whole matrix;
* every terrain factor inside its declared bound on every world, and none of them sitting
  exactly on it;
* airless worlds and zero-rated worlds produce exactly zero, and nothing divides by it;
* a flat world leaves every terrain factor exactly 1, and so does `WindTerrainInfluence = 0`;
* the engine's derivations reproduce exactly, including Triton's peaks-above-air;
* **nothing over the engine's own figure on any scenario**, which is the one property that figure
  was adopted for;
* a storm reaching the friction threshold near the ground, and ordinary weather nowhere near it on
  any world at any height.

### What the field data reaches, and what it does not

The largest dump to date is 4,371 climate rows over one earthlike world, 146 grids, six ground
materials, across a sunrise. What it settles:

| Column | What the dump says |
| --- | --- |
| `depth_m` | 42 buried rows, 46–169 m. Every one reads 280.00 K — a flat `UndergroundTemperature`, since all of them are inside the 2 km deadzone. The model is in force and the core gradient is unreachable, exactly as [Underground](#underground) argues. |
| `weather` | `RainLight` on 672 rows and nothing else. The offset is −1.8 K per unit of intensity on every one of them, so the table figure is being faded in by intensity and applied once. |
| `convection_coeff` | 0–166 W/(m²·K), mean 59. Zero on all 906 airless rows and non-zero on all 3,465 rows with air. |
| `game_comfort` | 0.000–0.494 where there is oxygen, zero on every airless row **and on sixty-three that have air** — 374 zeros of 597 rows. |

**The game's own comfort figure cannot check anything here.** Over the 286 rows of this dump that
carry air it correlates **+0.11** with the sun's elevation and **+0.20** with this model's ambient —
weak against both, so it confirms nothing thermal in either direction. An earlier reading of a
larger dump put those at +0.86 and +0.12 and read it as a daylight figure; that dataset is not in
this repository and the figures above are the ones its own fixture gives, which is `E5`. What both
readings agree on is the conclusion: it is a comfort fraction and it is not a check on anything this
model computes.

**What no field dump has reached:** one planet, one weather kind, one latitude band, and no still
air anywhere — every row with air also had wind, so the convection coefficient has never been
observed at its unmodified value. The wind data is worse: the largest run was 2.9 minutes at dusk,
so `wind_heating` was 0 for 95% of samples and peaked at 0.286, the ground was nearly flat, and
exactly one grid — parked on a mountain at 5,987 m — saw the terrain model do anything real
(speed-up 1.370, 41° of channelling). A single offline run covers all four gaps; a probe sweep
covers them in the real world.

A validation world should park several grids for a few in-game days: a valley floor and the ridge
directly above it, to separate speed-up from shelter and to see whether channelling turns the valley
grid along the valley; a site at each of several latitudes; and one grid parked with another
hovering at 200 m in the same place, which is the only way to see the crossover and the nocturnal
jet directly — `wind_speed` on the two should diverge overnight and converge by afternoon.

---

## What the engine supplies

The survey the whole model is built on. Everything below is decompiled from the shipped
`Sandbox.Game.dll` or read from the definitions.

### What is usable

| Wanted | The engine's answer | Usable? |
| --- | --- | --- |
| Ambient temperature | `MyVisualScriptLogicProvider.GetTemperatureInPoint` | **No.** See below. |
| Air density | `MyPlanet.GetAirDensity` | Yes, and used directly. |
| Ground material | `MyVoxelBase.GetMaterialAt` → `MyVoxelMaterialDefinition.Id.SubtypeName` | Yes. Sampled below the surface point; on the boundary it answers about air as often as ground. |
| Wind speed | `MyPlanet.GetWindSpeed` | **No.** A rating, not a wind. See below. |
| Wind direction | nothing | **No.** Invented — see [Wind](#wind). |
| Weather intensity | `MyVisualScriptLogicProvider.GetWeatherIntensity` | Yes, as a 0..1 intensity. |
| Which weather | `MyVisualScriptLogicProvider.GetWeather` → the effect's subtype name | Yes. `RainHeavy`, `SandStormLight`, and thirty-one others. |
| What that weather does | `WeatherEffects.sbc` per-effect modifiers | Yes, and the source of the whole response table. |
| Depth below ground | `MyPlanet.GetClosestSurfacePointGlobal`, against the grid's own radius | Yes, and already cached for the ground material. |
| Is it night here | `MySectorWeatherComponent.IsThereNight` | Not needed; the sun vector answers it. |
| Sun direction | `MyVisualScriptLogicProvider.GetSunDirection` | Yes, and it points **toward** the sun — `MySector.DirectionToSunNormalized` is the same vector. |

### Temperature: two things, and neither is a temperature

```csharp
public static float LevelToTemperature(MyTemperatureLevel level) => level switch
{
    MyTemperatureLevel.ExtremeFreeze => 0f,
    MyTemperatureLevel.Freeze        => 0.25f,
    MyTemperatureLevel.Cozy          => 0.5f,
    MyTemperatureLevel.Hot           => 0.75f,
    MyTemperatureLevel.ExtremeHot    => 1f,
    _                                => 0.5f,
};
```

`DefaultSurfaceTemperature` is five levels on a comfort scale with no kelvin anywhere. **This is the
entire statement the game makes about how hot a planet is**, and four of the eight shipped worlds do
not author it, taking `Cozy` by omission.

```csharp
float oxygenInPoint = MyOxygenProviderSystem.GetOxygenInPoint(worldPoint);
if (oxygenInPoint < 0.01f) return 0f;
...
return MathHelper.Lerp(0f, value2, oxygenInPoint);
```

`GetTemperatureInPoint` returns **0..1, not kelvin**, and **returns zero wherever there is no
oxygen**. Mars, Europa and the Moon are all unbreathable, so it is flatly zero on three of the eight
worlds and near it on a fourth. The `game_comfort` telemetry column is this figure, and it is named
for what it is rather than for what it looked like.

**Orbital distance does not exist.** One sun, one intensity, and no field anywhere saying how far a
planet sits from it. A world cannot be cold *because* it is far away — the level is the whole
answer, which is why [the derivation](#the-derivation) has judgement calls in it.

### Wind: the whole of it

```csharp
public float GetAirDensity(Vector3D worldPosition)
{
    if (Generator == null) return 0f;
    if (Generator.HasAtmosphere)
    {
        double num = (worldPosition - base.WorldMatrix.Translation).Length();
        return (float)MathHelper.Clamp(
            1.0 - (num - (double)AverageRadius) / (double)AtmosphereAltitude, 0.0, 1.0)
            * Generator.Atmosphere.Density;
    }
    return 0f;
}

public float GetWindSpeed(Vector3D worldPosition)
{
    if (Generator == null) return 0f;
    float airDensity = GetAirDensity(worldPosition);
    return Generator.Atmosphere.MaxWindSpeed * airDensity;
}
```

Seven facts follow, and they decide everything in [Wind](#wind):

1. **There is no direction.** It returns a scalar. Nothing in the mod API exposes a wind vector; the
   only directional wind in the engine is `MyRenderProxy.Settings.WindStrength`, a foliage animation
   parameter that carries no bearing either.
2. **There is no time.** Midnight and noon return the same number.
3. **There is no latitude or longitude.** A pole and the equator are identical at a given altitude.
4. **There is no terrain.** The altitude term is measured against `AverageRadius` — the planet's
   mean sphere — not against the ground, so a valley floor 500 m *below* the average radius returns
   a **higher** wind than the ridge above it, which is backwards.
5. **The altitude falloff is linear, not exponential**, and reaches exactly zero at
   `AtmosphereAltitude`.
6. **It is a rating, not a wind.** `MaxWindSpeed` is the figure wind turbines are balanced against.
   On an earthlike world it is 80 m/s and `Atmosphere.Density` is 1, so sea level returns 80 m/s —
   290 km/h, a strong hurricane, everywhere, permanently. Used directly it puts every parked ship
   over the friction threshold and runs convection at nearly twice its still-air rate planet-wide.
7. **The one thing it is good for** is being a per-planet scale. It is the only figure in the engine
   that says "this planet is windier than that one", it is authored per planet, modded planets set
   it, and it costs one subtraction and a clamp.

**Verdict: unusable as a wind, worth keeping as a ceiling.**

### World size, and why every borrowed constant lands oddly

Space Engineers planets are not small Earths. Everything the engine builds is a fraction of the
radius:

```
maxHillHeight      = HillParams.Max × radius
minHillHeight      = HillParams.Min × radius
OuterRadius        = radius + maxHillHeight
InnerRadius        = radius + minHillHeight
AtmosphereAltitude = maxHillHeight × Atmosphere.LimitAltitude
```

| World | Radius | Hills | Atmosphere | 30° band | Horizon @2 m |
| --- | ---: | ---: | ---: | ---: | ---: |
| EarthLike / Alien / Mars | 60 km | −600 … 7,200 m | 14,400 m | 31 km | 490 m |
| Triton | 40 km | −2,000 … 8,000 m | **3,760 m** | 21 km | 400 m |
| Pertam | 30 km | −750 … 750 m | 1,500 m | 16 km | 346 m |
| Europa / Titan / Moon | 9.5 km | ±285 m | 285 m (Moon: none) | 5 km | 195 m |
| *Earth, for scale* | *6,371 km* | *−11,000 … 8,849 m* | *~100,000 m* | *3,336 km* | *5,048 m* |

Five consequences:

1. **SE terrain is about eighty times steeper than real terrain** relative to its world — hills at
   12% of the radius against Earth's 0.14%. Every terrain constant borrowed from wind engineering is
   operating far outside the range it was fitted in, so speed-up, shelter and channelling all reach
   for their bounds on ordinary SE ground where on Earth's slopes they would almost never bind.
   **They saturate onto those bounds rather than clipping to them**, which is the difference between
   a bound and a model: a clip made every site read exactly the cap, so terrain stopped telling one
   place from another — the only thing it is there to do. See
   [Terrain saturates rather than clipping](#terrain-saturates-rather-than-clipping).
2. **A circulation band is 31 km wide**, not 3,300. The general circulation is a local feature you
   can fly across in a minute.
3. **The horizon from head height is 490 m** on the largest world in the game. The local wind map
   reaches five kilometres — ten times past the horizon.
4. **Triton's peaks stand in vacuum.** 20% of its radius in mountain against `LimitAltitude` 0.47,
   so the air runs out less than half way up its highest ground. Its mean modelled wind is 1.5 m/s
   against an earthlike world's 7.1, because most of its surface is not in air.
5. **On a small moon the boundary layer would be taller than the whole atmosphere.**
   `WindGradientHeight` is 600 m; Titan's air is 285 m deep. The gradient height is capped by the
   air standing over the ground under the grid — `MyPlanet.AtmosphereAltitude` measured from the
   mean radius, minus wherever the ground actually is — so the profile stops climbing where the air
   stops rather than going on into vacuum. A mountain therefore has a shallower boundary layer than
   the valley beside it, which is the engine's own geometry rather than a choice.

---

## Limits and open questions

Stated plainly, because none of it came from evidence.

**Unevidenced by construction.** The ground offsets, the convection multipliers, the circulation
bands, every constant in `WindTerrain` (0.6 speed-up, 0.45 slow-down, 0.75 shelter, the 40°
full-shelter angle, the one-in-three full-channelling slope), and the diurnal amplitude and
crossover (0.35 and 80 m, plausible middles of wide real ranges) are all chosen to be plausible and
cheap. The game supplies nothing to fit them to, and the cross-check they were to be fitted against
does not exist — `game_comfort` correlates weakly with everything and is not a check on any of it.

**Known faults, tracked in the [backlog](backlog.md):**

| | |
| --- | --- |
| C7 | The 4 K/km lapse rate and the ground table both say mountains are cold. The honest fix is probably that ground offsets should shrink as the lapse rate grows, since a site is only snowy *because* it is high. **The snow reference site is where that will show.** |
| F4 | No latitude spread and no weather variety in any field data: three sites spanning 7°–41°, one weather kind, no still air. A polar grid and `/weather SnowHeavy` would settle the first two. |

**Three mechanisms are not built, and that is a decision rather than an oversight.** A *sea breeze*
needs a coastline, which means knowing where water is; *gap-wind acceleration* needs a valley
recognised as a valley rather than a fall line sampled at a radius; and *lee turbulence* is unsteady
by definition, which this field is not — it is a steady map, and a gusting term would be the one
thing in it that changed from moment to moment for no reason a player could see. Nothing else on
this page depends on any of them.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-10-28 | Describe planet probes in diagnostic ticks at six per simulated second after correcting their session cadence; normalize mixed-grid shadow comparisons, capture blocker topology/model identity, and release disabled adapter caches and query scratch independently of ray cadence. Release dormant Core blocker copies while preserving completed shadows and reusable skin. |
| 2026-08-22 | Closed `B19`. The terrain factors saturate onto their bounds rather than clipping to them, so SE's ground — eighty times steeper than the literature's — no longer makes every site read exactly the cap. The gradient at zero and the bound at infinity are both unchanged; what comes back is the middle, and terrain telling one place from another. Pertam and an earthlike world now differ, where under the clip both read the cap. |
| 2026-08-22 | Said *why* the three unbuilt wind mechanisms are unbuilt, which is what made their absence a decision rather than a note, and took the closed `C5` off the known-faults table. Closes `B28`, which was a backlog row recording something this page already recorded. |
| 2026-08-22 | Closed `B22` as a decision rather than a change: this mod will not author air densities against the engine's, because `C9` says the game's own answer is read and not overridden — and a density that disagreed with the engine's would put this model at odds with the oxygen system, the wind ceiling and the jetpack at once. The lack of differentiation between atmospheric worlds is the definitions', not the model's. |
| 2026-08-22 | Closed `C5`. The sea-level deadzone is derived from each world's own deepest natural ground — `HillParams.Min × radius`, 285 m to 2 km across the eight — rather than a round 2 km that put the core gradient out of reach on seven of them. Opened the cross-check that should have existed with the transcription: `PlanetReferenceTests` compares the eight worlds against the installed definitions, and found on its first run that two of them are written under a different element name than the other six. |
| 2026-08-22 | Closed `C6`: the climate's lag is a share of the world's own day rather than 45 absolute seconds, and the day is measured from the sun the model already samples rather than read from a type whose whitelist status cannot be established outside a session. |
| 2026-08-22 | Closed `B16`: roughness length comes from the ground material under the grid rather than being one number for a whole world. It is the one figure in the ground table with a published table behind it, and it was already being looked up for the temperature offset. |
| 2026-08-22 | Closed `B23`: the telemetry column is `game_comfort`, named for what it is. Re-measured its correlations against the fixture this repository actually holds — +0.11 with the sun's elevation and +0.20 with this model's ambient, against the +0.86 and +0.12 quoted from a dataset that is not in the tree (`E5`). The conclusion is unchanged and the numbers are now checkable. |
| 2026-08-22 | Closed `B20`. The boundary layer is capped by the air over the ground under the grid, so the vertical profile is no longer evaluated at heights with no atmosphere at them — Titan's air is 285 m deep against a 600 m configured gradient. Derived from `MyPlanet.AtmosphereAltitude` rather than authored, so it follows every world including modded ones, and the offline lab computes it the same way. |
| 2026-08-22 | Closed `A16`. A buried grid exchanged with rock at the coefficient for moving air, which made digging in the best cooling in the game; it is `2k/D` — 2 W/(m²·K) against 50 — authored per planet as `UndergroundConvectionCoefficient` and crossed over the first five metres so the surface is not a wall. Neither wind nor weather reaches it. |
| 2026-08-22 | Closed `B29` the same day it was opened: the convection wind factor runs from 1 upward rather than from 0.5 to 1, so a wind cools a hull at every speed instead of warming it below about 50 m/s. Same two-to-one contrast between a windward face and a lee one; corrected floor. |
| 2026-08-22 | Closed `B17` and `B18`. Two of the three causes were faults and are fixed: the weather's own wind modifier multiplied a share that already meant *the worst weather*, so one storm was counted twice, and nothing bounded the composed speed. A storm went from 187 m/s to 74, and from 143 m/s to 66 within a hundred metres of the ground; `>ceil` is now zero on every scenario in the matrix. The third is not a fault — a storm at the planet's own ceiling is over the friction threshold, `v_rel` is relative wind by design, and `StormHeatingTests` measures the cost as degrees rather than hundreds. Opened `B29`, which came out of trying to write that test: below about 50 m/s the model's directional convection factor makes wind a net *warmer*. |
| 2026-08-22 | Fixed `B14`, the largest known fault in the wind pattern. The meridional component follows the band rather than the hemisphere — equatorward in the trades and the polar easterlies, poleward in the westerlies — and comes off the same signal as the zonal one, so a band edge is a calm rather than a place where the wind reverses at full strength. The test that should have caught it measured the east component of a unit bearing; it now measures the velocity. Two things fell out of the same pass: a terrain influence outside 0..1 could make a shelter factor negative and a speed with it, and the wind lab's day-against-night ratio was dividing two averages taken over different sets of latitudes (`E6`). |
| 2026-08-22 | Merged `planet-climate.md`, `planet-thermals.md` and `wind-model.md` into this page, named for the subsystem it describes. Converted to present tense, with the measurement narrative moved into this log. Corrected the claim that slope winds were not built — `WindSlope` is built, wired, settable and tested. Promoted the composed model above the engine survey it is justified by. |
| 2026-08-21 | Recorded the two defects that stopped any world receiving a per-planet climate: the generated file carried `--` inside an XML comment and was rejected wholesale by the strict importer, and the lookup was keyed on the planet entity's `DefinitionId` (`MyObjectBuilder_Planet/(null)`) rather than `Entity.Generator.Id`. Added the strict-parse check over every file `definitionextensions.txt` names. |
| 2026-08-20 | Derived a climate per shipped world from its own generator definition, and made the same derivation the fallback for modded planets. Added the wind model: circulation, vertical profile, daily cycle, terrain and slope winds, with the 28-scenario matrix behind it. Took the wind away from a buried grid — it had been clamped to ground level. Stopped a planet whose definition did not load being simulated as a vacuum. Recorded what the 4,371-row field dump settles and what it cannot reach. |
| 2026-08-19 | Reported the convection coefficient after the atmosphere blend rather than before it, and made thin air convect like thin air. |
| 2026-08-18 | Added weather as a thermal input rather than a wind input only, driven from the game's own authored `WeatherEffects.sbc` modifiers. Added the underground model — damping depth and the sea-level deadzone — against `CoreTemperature` and `SealevelDeadzone`, which had been defined and read but never used. |
| 2026-08-17 | Added latitude, ground material and altitude as terms, and fixed the thin-air scale compounding against the lag: it was applied to the running ambient rather than to the target, which held a 5.6 km site at 36 K for an entire run. Fixed a session's first step seeding ambient at 2.7 K and taking three minutes to recover. Pinned the ordering with `ThinAirDoesNotCompoundAgainstTheLag`. |
