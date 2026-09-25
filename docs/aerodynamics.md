# Aerodynamics requirements

**The goal, stated 2026-09-24: aerodynamics as realistic as the game allows, and learn what we can
from SEDrag to get there.** SEDrag is the *Aerodynamic Physics* workshop mod
([571920453](https://steamcommunity.com/sharedfiles/filedetails/?id=571920453)), the mod players
compare this one against. This page takes it apart, puts each part beside what this mod does and
beside the real physics, and turns the result into requirements. Where SEDrag is right, we copy the
structure. Where it is a gameplay invention, we write down why and refuse it. Where neither mod is
right, the physics wins.

**This page replaces the aerodynamic intent that came before it.** *Not a flight model* in
[document-of-intent.md](document-of-intent.md#what-this-mod-deliberately-is-not) was reversed by
decision for lift on 2026-08-31 (`K23`), and the reason given for the reversal was narrow. Now the
whole claim is reversed: the mod is a flight model, and it is judged on handling. **What does not
change**: the surface model is still the mod, and heat stays load-bearing. Every aerodynamic
quantity below is computed from the same per-node exposure, area and reconstructed normal the
thermal solver already builds. That shared data is the reason aerodynamics lives in this mod at all
(see the purpose section of the document of intent).

The requirements are numbered `AR1` onward. A requirement's number is its name and never moves,
following the same rule as [backlog.md](backlog.md). The milestone that builds them is `K25`.

---

## The regime, and why the current model is wrong about it

**Every speed the game allows is subsonic and incompressible.** The engine's cap is 100 m/s, and
`SpeedLimit` ships at 140 m/s, which is Mach 0.41 at sea level. A 10 m hull at 100 m/s has a
Reynolds number near 7 × 10⁷. That is turbulent, separated flow around a bluff body.

**What the mod computes is Newtonian impact theory**: pressure on a windward element goes as
`2q sin²θ`, leeward faces feel nothing, and drag is the sum. That law is right in free-molecular,
hypersonic flow and wrong everywhere a ship in this game actually flies
([thermal-model.md](thermal-model.md#aerodynamic-friction), `K6`, `K22`). **Most of what follows is
a consequence of that mismatch**, and none of it is visible in SEDrag, because SEDrag never tries
to be physical.

In the subsonic regime, real flow has four properties:

| | What real subsonic flow does | What this mod does | What SEDrag does |
| --- | --- | --- | --- |
| **Drag** | Pressure drag from the **frontal silhouette**, set mainly by how the flow *leaves* the body (base, boat-tail), plus skin friction on the wetted area. A cube's Cd is about 1.05, a plate square to the flow about 1.17–1.28, and a streamlined body 0.05–0.3. | Windward exposed area × `sin²θ`, with no wake and no skin friction, derived from the friction watts. | Silhouette area × a per-axis coefficient from block normals, with a fictional speed scaling (below). |
| **Lift** | Comes from circulation around **lifting surfaces** at an angle of attack. It is linear up to stall, `CL ≈ 2πα·AR/(2+√(AR²+4))`, stalls at 12–15° on a plate, and brings induced drag `CL²/(πeAR)`. | The sideways remainder of the Newtonian sum. It can never exceed drag and cancels on any symmetric hull (median lift-to-drag 0.057). | A flat-plate curve on the whole outline, `(1−cos 2πx)/2`, with a lift constant about 330× its drag constant, plus authored per-block normals. |
| **Moments** | The force acts at the centre of pressure, and a rotating body sees `ω × r` on every surface. The result is weathervaning, plus damping of rotation. | Everything is applied at the centre of mass, so there is **no torque at all**. | Applied at a centre of lift built from the outline, plus a small-grid stabilising couple. |
| **Heating** | Bounded by the **recovery temperature**, `T_amb + r·v²/(2c_p)` with r ≈ 0.89: +4.4 K at 100 m/s, +8.7 K at 140 m/s, +40 K at 300 m/s. | A `v³` source on the windward faces with nothing bounding it. | Six counters, one per side, driven by the scaled speed. Re-entry starts at 127 m/s. |

## What SEDrag's numbers are, so nobody copies them

SEDrag feels the way it does mainly because of one mechanism, and it isn't physics. **`SpeedMod`
is `5000 / max speed`**: 33.3 at SEDrag's default 150 m/s cap. It multiplies the velocity before
the velocity is squared, and it is blended in as `(v/100)⁴`. The game's 100 m/s is treated as
though it were Mach 5, so drag rises with roughly the sixth power of speed near the cap. On top of
that, density enters cubed.

Drag per square metre of frontal area at sea level. SEDrag is at its defaults; this mod uses
`DragCoefficient` 1.54 and the population's median shape factor of 0.325:

| Speed, m/s | 10 | 20 | 27 | 40 | 60 | 75 | 100 | 140 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SEDrag, N/m² | 4 | 41 | 185 | 1,730 | 19,200 | 72,800 | 408,000 | 800,000 |
| This mod, N/m² | 25 | 100 | 182 | 400 | 900 | 1,410 | 2,500 | 4,900 |
| Ratio | 0.16× | 0.41× | **1.0×** | 4.3× | 21× | 52× | **163×** | 163× |

Terminal speed in sea-level air:

| Ship | SEDrag | This mod | Real bluff body (Cd 1.05 on the silhouette) |
| --- | --- | --- | --- |
| 500 t large grid, 156 m² frontal, 5 MN | **65 m/s** | 358 m/s | ≈ 224 m/s |
| 20 t small grid, 6 m² frontal, 0.4 MN | **74 m/s** | 516 m/s | ≈ 322 m/s |

**SEDrag's speed wall is invented.** A real ship with that much thrust is not limited by drag below
the game's cap, and that stays true after every requirement on this page is met. Two parts of this
mod are nonetheless not realistic either. Its drag is about 2.5× too low for a blunt hull: the
missing `1.225 kg/m³` accounts for some of it (`AR2`), and a Newtonian `sin²θ` that rewards any
slope on the nose accounts for the rest (`AR4`). And it has no moments at all (`AR5`). **Those two
gaps, not the wall, are why this mod flies worse than SEDrag.**

Tables derived by hand from `SEDrag.cs` `ThreadUpdate` and `Core.cs` `CalculateMach5`, not
measured in a session. The flat-plate lift figures in this page are derived the same way from
`DragBase.getLiftCI`.

---

## Part by part

Verdicts: **Adopt** means we copy SEDrag's approach. **Adapt** means we keep what SEDrag gets right
and fix the physics. **Keep ours** means this mod is already better. **Refuse** means the part is
fiction, and the reason is recorded.

| # | Part | SEDrag | This mod | Verdict |
| --- | --- | --- | --- | --- |
| AR1 | Speed scaling | 100 m/s treated as Mach 5; drag ∝ v⁶ near the cap | None | **Refuse**. Compressibility only where the speed limit reaches it. |
| AR2 | Density law | ρ³ | ρ as a 0–1 fraction | **Refuse** ρ³; carry ρ in kg/m³ |
| AR3 | Frontal area | Silhouette per axis | Every exposed windward face; shielding ships off | **Adopt** the silhouette for force |
| AR4 | Drag breakdown | Per-axis coefficient from block normals | Newtonian `sin²θ` on the forebody | **Adapt**: forebody + base + skin friction |
| AR5 | Where force acts | Centre of lift (outline) | Centre of mass, zero torque | **Adopt**, using our per-node centre of pressure |
| AR6 | Rotational damping | Axis offset scaled by speed | None | **Adapt**: `ω × r` per surface |
| AR7 | Small-grid stabilising couple | ±0.25·lift at the half-extents | None | **Refuse**; covered by AR5 and AR6 |
| AR8 | Body lift | Flat plate on the outline, 330× the drag constant | Newtonian transverse sum ≤ drag | **Adapt**: aspect-ratio lift with stall |
| AR9 | Lifting surfaces | Authored `SurfaceNormal` per block (about 100 vanilla blocks) | Reconstructed normals, no surfaces | **Adapt**: detect surfaces from our normals; our own override only (SEDrag's XML not read) |
| AR10 | Lift fade 100→127 m/s | Yes | None | **Refuse**; stall is the real limit |
| AR11 | Induced drag | None | None | **Add** |
| AR12 | Ground effect | Drag ×1–3 and lift ×1–1.5 within 20 m | None | **Adapt**: right sign and right reach |
| AR13 | Wind | Perlin map, weather multiplier, corner raycasts | Boundary layer, terrain, slope, diurnal, burial | **Keep ours**; check the weather gap |
| AR14 | Wind turbines | Output from its own wind | Vanilla | **Adopt**: drive turbines from our wind |
| AR15 | Speed limit | Raises the engine cap to 150; clamps the remote-control slider | `K10` port of RTS, `SpeedLimit` 140 | **Remove the port**; read the original RTS's API; cruise curve, then drag |
| AR16 | Aerodynamic heating | Six counters, scaled speed, damage above 750 | Full thermal model, `v³` source | **Keep ours**, bounded by recovery temperature; re-entry heating a switch |
| AR17 | Character drag | Yes | None | **Adopt** |
| AR18 | Water | Water Mod: fraction submerged, water surface for ground effect | None | **Adopt**, optional |
| AR19 | Update rate | Every tick, from a cached per-grid profile | The solver's last step | **Adopt** |
| AR20 | Multiplayer | Server and clients both apply | Server only | **Adopt**: simulated on server and client |
| AR21 | Indicators | CoM/CoL markers in build mode, lift debug arrows | `AeroOverlay` | **Keep ours**; add a build-mode view |
| AR22 | Force clamp | `drag ≤ mass × speed` | None | **Adapt** as a numerical guard only |

### AR1 — No speed scaling; compressibility only where the speed limit reaches it

The game speed is the real speed. `SpeedMod` is how SEDrag makes a 150 m/s game feel like a
hypersonic one, and it is the single largest thing a player notices about that mod. We don't copy
it: a world's speed is not an alias for some other speed. **What is real and missing is the
transonic drag rise**, which sets in above about Mach 0.7. No shipped world reaches it, but
`SpeedLimit` is a dial. Requirement: above Mach 0.7 at the local speed of sound, the drag
coefficient follows a drag-divergence curve. Below that it is untouched. The cost is one branch
that a shipped world never takes.

### AR2 — Density in kg/m³, entering linearly

The game's `GetAirDensity` is a fraction of the planet's sea-level density. We multiply it by
`1.225 kg/m³` (a dial per planet if a planet definition says otherwise), so that every coefficient
on this page is a textbook value rather than one fitted to the population. SEDrag's ρ³ makes
altitude a strong lever: half density gives an eighth of the drag. Real drag goes as ρ, and ρ³ is
refused. **Doing this moves `DragCoefficient` by 1/1.225.** The retune goes with it, or every hull
gets 22 % draggier with no other change.

### AR3 — The force sees the silhouette

Frontal area is what the air meets. SEDrag computes the silhouette per axis and gets that right.
This mod counts every exposed windward face, so a hull with a ridge or detail behind its nose is
charged for area the air never reaches. Windward shielding exists (`EnableWindwardShielding`) but
ships off because of its cost as a heat pass. **The force table (`AR19`) needs the silhouette only
when the hull changes, not every step**, so for the force it is affordable where it wasn't for the
heat. Requirement: aerodynamic force is computed from the shielded, windward-visible area. Heat
keeps its own switch.

### AR4 — Drag as forebody plus base plus skin friction

Subsonic drag on a bluff body has three parts, and this mod computes one of them, in the wrong
regime:

1. **Forebody pressure.** Nose shape matters, but much less than Newtonian theory claims. A
   Newtonian `sin²θ` removes up to two thirds of a real hull's drag (median shape factor 0.325).
   In subsonic flow, a pointed or rounded nose on a blunt body removes perhaps a third. The
   reconstructed normals stay, but their effect is bounded to a subsonic forebody relief.
2. **Base drag**: the wake. This is the half `K22` identified as a second, force-only channel,
   because its energy goes into the wake rather than the plating. Its input is the leeward
   silhouette and how gradually the hull closes toward it (boat-tailing). The leeward normals are
   already reconstructed, so they supply it.
3. **Skin friction**: `Cf ≈ 0.074·Re^−0.2`, about 0.002, applied to the wetted area. It is small on
   a brick and dominant on a streamlined hull, and it is the part that deposits heat (see AR16).

**This makes the force a channel separate from the friction watts.** Today
`DragForce.Newtons = frictionWatts × Cd / (2·FrictionScale·v)` derives every newton from the heat.
Base drag cannot be expressed that way (`K22`), and neither can lift, induced drag, or moments.
Requirement: **force and heat read the same surface data and are computed independently.** The
heat keeps its own coefficient, so a world that tuned its temperatures has not retuned its handling
(the reason `DragCoefficient` and `FrictionScale` are separate dials already). This also answers
the question from earlier in this session about splitting aerodynamics into its own mod: once
force is its own channel over published surface data, it *can* be split.

**Validation rigs:** a cube reads Cd 1.05 ± 0.1; a plate square to the flow reads 1.17–1.28; a
boat-tailed hull beats a blunt-based one of the same frontal area. That last pair is the one
`DragShapeTests` currently cannot tell apart on the leeward side.

### AR5 — The force acts at the centre of pressure

This is the largest handling difference and the most realistic lesson SEDrag teaches. It applies
drag at a centre of lift, and that is what makes a ship with fins at the back fly nose-first.
**This mod already computes a better centre of pressure than SEDrag does**:
`AeroOverlay.CentreOfPressure` weights each node by its own force, where SEDrag averages the
extreme cells of its outline. Today that point is only drawn, with the caption *no torque is
applied from it* (`K16`). Requirement: the aerodynamic force on each grid is applied at that grid's
centre of pressure, and a constraint group gets one force per grid rather than one at the group's
centre of mass. **`K16`'s caveat is then withdrawn**, because the indicator describes the force the
mod actually applies. Rig: a hull with fins behind its centre of mass returns to heading after a
yaw kick; the same fins ahead of it diverge.

### AR6 — Rotation is damped by the air, per surface

A rotating hull sees extra relative wind `ω × r` at every surface. That is the physical source of
aerodynamic damping, and without it AR5 only produces oscillation: a hull with its centre of
pressure behind the centre of mass would swing through its heading forever. SEDrag fakes the
damping by moving its application point along the dominant axis as speed rises. Requirement: the
force profile (`AR19`) carries each grid's second moment of area about the centre of mass per
direction, and the damping torque is `−½ρ·Cd·Σ(A·r²)·ω|ω|`-shaped. That is evaluated from the
table, not per node per tick. Rig: a free hull given a spin in air decays, and decays faster in
thicker air.

### AR7 — No small-grid couple (refused)

SEDrag adds a pair of opposing forces at the half-extents of small grids, sized at a quarter of the
lift, to cancel the lift's roll and pitch. It is a hand-tuned stabiliser standing in for AR5 and
AR6. Once the force acts where it should and rotation is damped, there is nothing left for it to
correct. Revisit only if a session shows small grids tumbling after AR5 and AR6.

### AR8 — Body lift: aspect ratio, angle of attack, stall

SEDrag gives every hull a flat-plate lift from its outline. The shape is right: lift appears at an
angle of attack and falls away beyond it. The size is fiction: 12.25·ρ³·v² against a drag constant
of 0.037, so any tilted brick is a wing. The Newtonian transverse sum this mod uses is wrong the
other way: it cancels on any symmetric hull, and lift can never exceed drag.

**The real law needs two numbers the surface data already has, planform area and span:**
`CL_α = 2π·AR / (2 + √(AR² + 4))` (Helmbold). That one formula covers a slender box (low aspect
ratio, lift ∝ `(π/2)·AR·α`) and a proper wing. Stall comes at 12–15° for a high aspect ratio and
much later for a low one, and past stall the plate's normal force goes to `CN ≈ 1.2·sin α`. On the
drag side, AR11 then charges for the lift. Requirement: body lift is computed from the planform
the air sees, with that law, and replaces the Newtonian transverse sum. **`K23`'s four criteria are
re-registered, not inherited.** Its "p95 lift-to-drag ≤ 2" was written against a model that cannot
make lift. Real aircraft reach 10–20, so the bound becomes a rig check against the formula
(a flat plate at 10° gives `CN` within 10 % of the textbook value). The population criterion that
keeps bricks from flying without thrust stays.

### AR9 — Lifting surfaces found from the hull, authored where they must be

SEDrag knows which blocks are wings because a file says so: `AeroProperties.xml` gives about a
hundred vanilla blocks a `SurfaceNormal`, a two-sided flag and a multiplier. Any block a mod adds is
invisible to it until someone writes more XML. **This mod reconstructs a normal for every block
already**, so it can find lifting surfaces itself. A lifting surface is a connected, exposed region
that is thin across its own normal. That is exactly the shape `K24` reads badly as drag (a bare
one-cell plate): the case that breaks the drag term is the case that detects a wing. Requirement:
lifting surfaces are detected from occupancy and normals, each with its own span and area feeding
AR8's law. A per-definition override exists for blocks whose model is not their cells (a wing
block with a thin mesh inside a full cube). The API already carries `SetBlockDragProfile`, and the
override is its lift sibling. **SEDrag's `Aerodynamic` `ModExtensions` are not read (decided
2026-09-24).** Those values were tuned against SEDrag's lift constant, so under a realistic law a
wing authored for SEDrag would not fly the way its author tuned it. Compatibility that behaves
badly is worse than none.

### AR10 — No lift fade with speed (refused)

SEDrag fades lift out between its "Mach 5" (100 m/s) and its ablation speed (127 m/s), to 2 % above
that. It is a consequence of AR1's scaling and has no counterpart at Mach 0.3. The physical limit
on lift is stall, which AR8 already has.

### AR11 — Induced drag

Lift costs drag: `CDi = CL² / (π·e·AR)`, with e ≈ 0.7–0.9. Neither mod has it. Without it, a
lifting hull glides further than anything real, and AR12 has nothing to act on. Requirement: every
lifting surface and the body pay induced drag from their own aspect ratio.

### AR12 — Ground effect with the right sign

SEDrag multiplies *drag* by up to three within 20 m of terrain or water. The real effect goes the
other way: close to the ground, the downwash is blocked, so **induced drag falls and lift rises**,
and the effect reaches about one span, not a fixed 20 m. Wieselsberger's factor,
`φ = (16h/b)² / (1 + (16h/b)²)`, scales induced drag at height `h` for span `b`. Requirement:
ground effect reduces AR11 by that factor and raises lift to match, and it uses height above the
terrain `PlanetProbes` already samples.

### AR13 — Wind: ours is better; check two gaps

SEDrag's wind is a Perlin map per planet that changes on a timer and is scaled by the active weather
effect's `WindOutputModifier`. It is occluded by eight raycasts from the grid's bounding-box
corners. This mod's field is richer: boundary layer, terrain speed-up and shelter, slope winds,
diurnal cycle and burial ([environment.md](environment.md#wind)). **Two things SEDrag has that
ought to be checked here**: whether an active weather effect scales our wind (the game's own
weather says it should), and whether *another grid* shelters a ship (a hangar, a carrier deck).
Terrain shelter exists; shelter by grids does not.

### AR14 — Wind turbines turn in our wind

A turbine producing power in a calm while our field says a gale is blowing, or the reverse, is the
mod contradicting itself on the one block a player uses to see the wind. SEDrag already drives
turbine output from its own wind. Requirement: when `EnableWind` is on, a wind turbine's output
follows this mod's wind speed at the block. This isn't a force. It's consistency.

### AR15 — Top speed belongs to RelativeTopSpeed; this mod reads it

**Decided 2026-09-24: the embedded RelativeTopSpeed port (`K10`) is removed, and this mod becomes
compatible with the original mod's API instead**
([RelativeTopSpeed](https://github.com/Gauge/RelativeTopSpeed), repository at
`../RelativeTopSpeed`). A world wanting per-mass cruise speeds installs RTS. That mod already
handles combined physical-group mass, the boost, the remote-control *Speed Limit* slider clamp
(`RemoteControl.cs`, so SEDrag's version of it is not needed here), and its own settings menu.
Two copies of one mechanism is one copy too many.

**The composition is *cruise curve, then drag*.** RTS's resistance force holds a ship under the
cruise speed its mass earns. This mod's aerodynamic force acts on the ship as well, so in
atmosphere a ship reaches whichever limit is lower: its RTS cruise ceiling, or its aerodynamic
terminal speed. Neither mod stands down for the other, and neither scales its force by the
other's. Order within a tick does not matter for forces, because the engine sums them. It matters
only for RTS's non-boost mode, which removes excess velocity by impulse. There the cruise curve
acts first by construction, and drag then works on what is left.

**What the API is used for.** RTS publishes 13 read-only getters on mod-message channel
`2772681332` (`RtsApi.cs`, copied unchanged into this mod as its client, per RTS's own
instructions). Nothing in it writes, so compatibility means reading:

- **Legibility**: *why will this ship not go faster* now has two possible answers, and the
  aerodynamics readout names the binding one. It shows RTS's `GetEffectiveCruiseSpeed` and
  `GetEffectiveSpeedCeiling` beside this mod's own terminal speed at the current density, and says
  which is lower. `IsGoverned` decides whether RTS's line is shown at all.
- **The world cap**: `GetWorldSpeedLimit` replaces this mod's own `SpeedLimit` wherever a speed
  is scaled or plotted. **Without RTS installed, the engine's own cap applies (100 m/s vanilla).**
  This mod no longer raises it, which is a change for worlds that relied on `SpeedLimit` 140 and
  is recorded in the configuration change log when the strip ships.
- **Bind lifecycle**: `Load` at session load, `Unload` at unload, every call guarded by
  `IsReady`. An absent RTS is the normal case and costs one unanswered mod message.

*The detection argument in the document of intent is superseded.* It refused seeing RTS on the
grounds that a mod standing down for a rival takes a working world's drag away. Reading an API to
explain a limit takes nothing away, and nothing here stands down.

### AR16 — Heating bounded by recovery temperature

SEDrag's heat is six counters, fed by the scaled speed, with damage above 750 and smoke and glow
effects. It is a spectacle, and this mod's thermal model is the better half of the comparison.
**The realism lesson runs the other way:** at game speeds, real aerodynamic heating is small. A
surface cannot be heated by the air past the recovery temperature, which is +8.7 K at 140 m/s and
+40 K at 300 m/s. Today the friction term is a `v³` source with no bound, so it is unmeasured
whether a hull ever exceeds that. Requirement: aerodynamic heating is convection toward the
recovery temperature (raise the convection term's ambient to `T_amb + r·v²/(2c_p)` on exposed
faces), or a source provably bounded by it. **This changes shipped temperatures**, so under `K9`'s
rule it waits for a population walk, like `EnableShapeDrag` did.

**Re-entry heating is configurable (decided 2026-09-24)**, as a gameplay layer on top of the
realistic bound, not a replacement for it. `EnableReentryHeating` (off by default, because realism
is the default) adds heating above `ReentryStartSpeed` (m/s), scaled by `ReentryHeatingScale`, on
the windward faces the force profile already identifies. It is labelled in the menu as not real
at the game's speeds. A world that raises its speed cap far enough gets realistic re-entry from the
recovery temperature alone, with the switch off.

### AR17 — Engineers have drag

SEDrag applies drag to characters. A person falling belly-down has `Cd·A ≈ 0.7 m²` and a terminal
speed near 53 m/s. An engineer in this game falls as if in vacuum. Requirement: a fixed character
profile (no surface model; the suit is one body) gives drag in atmosphere. Rig: terminal speed in
Earth-like sea-level air is 50–60 m/s.

### AR18 — Water, when Water Mod is present

SEDrag scales drag by the fraction of the grid out of the water and counts the water surface as
ground for ground effect. Water is 816 times denser than sea-level air, so realistically, a
submerged hull is dominated by it. Requirement: optional integration through Water Mod's API.
Submerged nodes use water's density, and AR12 treats the water surface as ground. Low priority.
It is a dependency on another mod's API, and the same rules apply as for `K17`.

### AR19 — Forces every tick, from a cached profile

SEDrag recomputes its outline when blocks change and evaluates the force every tick, in a
background thread. This mod applies the force the solver computed on its last step, and a step
does not run every frame, so the force lags the ship's attitude. With AR5's torque, lag becomes
oscillation. **Requirement: an aerodynamic profile per grid, rebuilt when the hull changes, and
evaluated every physics tick against the current relative wind and rotation.** The profile is
a table over wind directions (26 or 98, interpolated). Each entry holds the silhouette area, base
area, wetted area, centre of pressure, planform/span for lift, and the damping moment. All of these
are geometry, so they change when the hull does and not when the wind does (`K22`'s argument for
the normals). The tick then does a lookup and a few multiplies per grid, independent of block
count. **This is also the object a separate aerodynamics mod would read**, and publishing it
through the API is how that becomes possible.

### AR20 — Simulated on server and client

**Decided 2026-09-24: the force is simulated on both**, as SEDrag does
(`SERVER_ONLY_PHYSICS = false`). A client flying a grid predicts the force instead of being
corrected into it, which matters once AR5 adds torque. The server stays authoritative, so any
difference is corrected by the engine's own grid sync. **This puts one constraint on the inputs:**
the force may depend only on what both machines compute identically: hull geometry (the AR19
profile), velocity and rotation, planet, density, and the wind field. It may not depend on
temperatures, which clients are allowed to drift (`C10`). The friction watts drift, and that is
one more reason the force cannot be derived from them (AR4). Damage stays server-only. A session
test checks that a pilot on a client feels no correction jitter at the cruise ceiling.

### AR21 — Indicators

`AeroOverlay` is ahead of SEDrag's markers: it draws wind, drag and lift vectors, the centre of
pressure, and a line naming every shut gate. What SEDrag has that this mod doesn't is the **build-mode**
view: centre of mass and centre of lift markers visible while a player is building, which is when
*will this fly* gets answered. Requirement: the overlay can be shown on a grid under construction
for a chosen test airspeed and direction, read from the AR19 profile.

### AR22 — A numerical guard, not a physical clamp

SEDrag caps drag at `mass × speed` newtons. It is not physics; it stops one tick's force from
reversing the ship. The need is real: a light grid in dense air at a coarse step can overshoot.
Requirement: the impulse applied in a tick may bring the relative airspeed to zero along the
flow and no further. It is a guard on the integrator, documented as one, and it is never binding
in a correctly resolved case.

---

## Decisions taken, 2026-09-24

| # | Question | Answer | Where it lands |
| --- | --- | --- | --- |
| 1 | Does drag alone set top speed in atmosphere? | **Cruise curve, then drag.** Both act. | AR15 |
| 2 | Read SEDrag's block extensions? | **No.** They were tuned for its lift constant and would behave badly under a realistic law. | AR9 |
| 3 | Re-entry heating at game speeds? | **Configurable**: a switch on top of the realistic bound, off by default. | AR16 |
| 4 | Server-only forces, or client prediction? | **Simulated on server and client.** | AR20 |
| 5 | Ship in steps, or behind one switch? | **Strip the existing aerodynamics out and rebuild it**, with switches where they make sense. | [Strip and rebuild](#strip-and-rebuild) |
| 6 | *(added by the owner)* The embedded RelativeTopSpeed port? | **Strip it out, and be compatible with the original mod's API instead.** | AR15 |

## Strip and rebuild

**The existing model is removed rather than evolved.** Every part of it is either derived from the
friction watts (the force, the lift, the centre of pressure) or built to correct that derivation
(the shape factor, the coefficient at 1.54). The rebuild computes the force from a different
object (the AR19 profile), so evolving it in place would carry two models through every
intermediate state. The strip lands first as its own change: the mod then has aerodynamic
*heating* and no aerodynamic *force*, which is a coherent, testable world. The rebuild then adds
back one switch at a time.

### What goes, what stays, what is rebuilt

| Piece | Files | Fate |
| --- | --- | --- |
| Force application | `Game/ThermalGridDrag.cs`, `Game/AeroGroupForces.cs`, `Core/Simulation/DragForce.cs`, `Core/Simulation/LiftForce.cs` | **Removed.** Rebuilt as the profile evaluator (AR19) and a per-tick force pass on server and client (AR20). |
| Top-speed port | `Game/ThermalGridTopSpeed.cs`, `Core/Simulation/TopSpeedForce.cs`, `Core/Simulation/CruiseCurve.cs`, the Top speed menu page | **Removed**, replaced by the RTS API client (AR15). |
| Pressure accumulation in the solver | `ThermalSolver` `pressureAccumulator`, `LastPressureWatts`, `plan.LiftEnabled`, `plan.ShapeEnabled`, `ShapeFactorOf` on the friction row | **Removed** from the thermal step. The solver stops computing anything that exists only for a force. |
| Friction heating row | `ThermalSolver` `nodeFrictionRow`, `frictionScale`, `FrictionWatts` | **Kept for now**, then rebuilt under AR16 (recovery temperature + the re-entry switch). It is heat, and heat stays load-bearing. |
| Reconstructed normals | `Core/Model/ShapeNormal.cs` | **Kept as surface data.** It stops being a `sin²θ` on the friction row and becomes an input to the profile (AR4 forebody, AR9 lifting surfaces). The solver's per-node buffer and its refresh budget were planned to stay and did not: once the friction row stopped reading them nothing in a step did, and a mechanism nobody reads must cost nothing (`P8`). The profile is rebuilt when the hull changes, which is when a normal changes, so it reconstructs them itself; what that costs is in [load-and-hitching.md](load-and-hitching.md). |
| Windward shielding | `windShadow`, `nodeWindLit`, `EnableWindwardShielding` | **Kept for heat.** Its slicing is reused, per profile direction, to compute the silhouette at profile-rebuild time (AR3). |
| Block drag profiles | `Core/Model/DragProfile.cs`, `SetBlockDragProfile` / `ClearBlockDragProfile` | **Kept.** They scale a block's contribution to the profile, which is what they already mean. |
| Centre of pressure and overlay | `AeroOverlay.cs`, `CentreOfPressureTests` | **Rebuilt** to draw the profile's centre of pressure and forces, which are now the forces applied. It also gains the RTS line (AR15) and the build-mode view (AR21). |
| API keys | `GetGridFrictionWatts`, `GetGridAeroForces` | **Kept, same meaning**, reimplemented. Friction watts are the aerodynamic heating, and aero forces are the force this mod applies. Between the strip and the force's return, `GetGridAeroForces` returns zero vectors, which is true. No major-version change (`ApiSurface.txt`). |

**Retired settings.** Removed fields keep their `ProtoMember` numbers retired forever, so a saved
world's value is ignored and never read into something else:
`EnableDrag` 135, `DragCoefficient` 136, `EnableShapeDrag` 138, `EnableLift` 139,
`LiftCoefficient` 140, and the whole top-speed block 145–163 (`EnableTopSpeed`, `SpeedLimit`,
`EnableSpeedBoost`, the twelve curve points, both boost speeds and both resistances). Kept:
`EnableFriction` 18, `FrictionAtSpeedsAbove` 42, `FrictionScale` 43, `EnableWindwardShielding`
137, `DebugAeroOverlay` 164. **What a world notices when the strip ships**: no aerodynamic force,
and the engine's own speed cap instead of 140 m/s unless RTS is installed.

**Tests.** Removed with their subject: `DragForceTests`, `LiftTests`, `TopSpeedForceTests`,
`CruiseCurveTests`, `ShapeDragTests` and `DragShapeTests` (their force halves), and `DragGroupingTests`.
`StormHeatingTests`, `FrictionIsolationTests`, `GridFrictionTotalTests` and `WindShieldingTests`
stay, because they test heat. The rigs listed under each AR replace the rest. The corpus walks'
drag columns (`CorpusShapeWalk`, the census `shape_factor`) are retired with the model they scored.

### Switches in the rebuild

**Fidelity is the default and a saving is a switch.** So a switch exists where a player might
reasonably want the mechanism off, or where it costs something measurable. Not every AR gets one.

| Switch | Covers | Default | Why a switch |
| --- | --- | --- | --- |
| `EnableAeroForces` | AR2–AR6, AR19, AR22: drag, moments, damping | on | The master for writing to physics. A world pairing this mod with another force mod turns it off. |
| `EnableLift` | AR8, AR9, AR11 | on | Lift is the part most likely to surprise a world built on a flight model without it. |
| `EnableGroundEffect` | AR12 | on | Costs a terrain height read per grid. |
| `EnableCharacterDrag` | AR17 | on | Touches characters, which nothing else in the mod does. |
| `EnableReentryHeating` + `ReentryStartSpeed`, `ReentryHeatingScale` | AR16's gameplay layer | off | A choice against realism, per decision 3. |
| `AeroProfileDirections` | AR19's table resolution, 26 or 98 | 98 | A cost dial for huge hulls. The coarse rung is the saving. |
| *(no switch)* | AR1 compressibility, AR10's removal, AR13, AR14, AR21 | — | Either costless below Mach 0.7, a removal, or a readout. |
| *(automatic)* | AR18 water | when Water Mod is loaded | A dependency, not a preference. |

Coefficients (`Cf`, the base-drag and forebody-relief factors, `e`, stall angle) are dials with
textbook defaults, not switches. That is the same rule `DragCoefficient` followed: a balance dial
is a number, not a rung.

### Build order

Each step ships on its own and names its rig checks. Population walks are proposed before they run
([corpus/README](../tools/corpus/README.md)).

0. **Strip — done 2026-09-24.** Everything in the *Removed* rows above, plus the retired settings,
   with the RTS API client added in the same change so the speed readout never goes dark: the aero
   debug view prints RelativeTopSpeed's cruise speed and ceiling, or the engine's cap without it.
   Rigs: `AerodynamicStripTests` — the friction row bit-identical to the old code with the shape
   term off, nothing in the mod writing to a grid's motion, and `GetGridAeroForces` returning zero.
1. **Profile and moments.** AR19 (profile, per-tick evaluation), AR5 (centre of pressure), AR6
   (damping), AR22 (guard), AR20 (server and client), with drag at textbook coefficients (AR2–AR4)
   from the start rather than a placeholder. Rigs: cube Cd, plate Cd, boat-tail pair, a finned hull
   recovering, a spin decaying. Population: terminal speeds against the engine cap, re-registered
   from `K11`.
2. **Lift.** AR8, AR9, AR11. Rigs: plate `CN(α)` against the textbook, a glider's lift-to-drag in a
   plausible range, a brick at any attitude cannot hold itself up. Population: `K23`'s
   self-lifting criterion, re-registered.
3. **Near the ground and beyond ships.** AR12, AR17, AR14.
4. **Heat.** AR16 (population walk, since it changes temperatures) and the re-entry switch, then
   AR1's compressibility.
5. **Optional.** AR18, AR21's build-mode view, AR13's two checks.

## Step 1's criteria, registered before anything is measured

Written 2026-09-24, before the profile or its coefficients exist (`E1`). A threshold here moves only in
the open (`E11`).

**The reference data is one textbook table.** White, *Fluid Mechanics* (4th ed.), Table 7.3, "Drag of
three-dimensional bodies at Re ≥ 10⁴", `C_D` on frontal area: cube face-on **1.07**, cube edge-on
**0.81**, disk **1.17**; cones by half-angle θ, 10° to 90°: **0.30, 0.40, 0.55, 0.65, 0.80, 1.05,
1.15**; flat-faced cylinders by `L/d`, 0.5 to 8: **1.15, 0.90, 0.85, 0.87, 0.99**; rectangular plates
by aspect ratio 1 to 20: 1.18, 1.2, 1.3, 1.5.

**Calibration and validation are disjoint.** The drag model's free coefficients — forebody pressure,
its falloff with a surface's inclination, base pressure against fineness — are fitted to the **cones
from 10° to 75°** and the **flat-faced cylinders**, built from cells the way a player would build
them, and nothing else. Everything below is predicted by the fitted model, never fitted to:

| # | Held out | Prediction | Falsified by |
| --- | --- | --- | --- |
| S1 | a cube, flow square to a face | `C_D` 1.05 ± 0.1 (registered with this page; White 1.07) | outside 0.95–1.15 |
| S2 | a one-cell-thick square plate, flow square to it | `C_D` 1.17–1.28 (registered with this page) | outside that band |
| S3 | a cube, flow along a face diagonal | `C_D` 0.81 ± 0.15 (White) | outside 0.66–0.96 |
| S4 | a hull with a tapered tail against the same hull with a blunt base, equal frontal area | the tapered one has the lower `C_D` | equal or higher |
| S5 | a shuttlecock: frontal area concentrated behind the centre of mass, flown at 50 m/s in sea-level air, kicked 20° in yaw | back within 5° of its heading, and still there after 30 s | not |
| S6 | the same body flown the other way round | diverges past 45° from the heading it was kicked off | stays within 45° |
| S7 | a free hull spun at 1 rad/s in air | its spin halves in finite time, and faster at density 1 than at 0.5 | not, or not faster |
| S8 | every rig above at 60 Hz | the numerical guard (`AR22`) binds on none of them | binds on any |

**The finned-hull rig the build order named moves to step 2, and the reason is physics.** Drag acts
along the flow at the centre of the windward projected area. At a small yaw β that centre is the
nose's, with weight cos β, while a fin's side contributes at sin β — so fin area restores only at
second order and a finned body flown nose-first is destabilised at first order. What makes a real fin
restore is its *lift*, a normal force proportional to β (`AR8`, `AR9`). A body stabilised by drag
alone is a shuttlecock, which is what S5 and S6 test; the finned hull is step 2's rig.

**The population criterion is `K5`'s, re-registered unchanged** ([balance-lab.md](balance-lab.md#the-criterion-k5-actually-needs-written-before-it-is-measured)),
over hulls that can lift themselves, in sea-level air at 1.225 kg/m³:

| # | Prediction | Falsified by |
| --- | --- | --- |
| P1 | on no more than 5 % of them does drag at 100 m/s exceed the ship's own thrust | more than 5 % |
| P2 | the p1 hull still reaches 60 m/s at full thrust | a p1 ceiling under 60 m/s |

**A failure of P1 or P2 is a finding and not a retune.** They were written for a model whose coefficient
was a balance dial; this one's coefficients are textbook figures, so if realistic drag fails a
playability criterion, that is recorded as the conflict it is and decided by the owner, not by moving
a coefficient off its source.

### Round 1: the registered design fails its held-out bodies

Run 2026-09-24 (`dotnet run --project tests/Thermodynamics.Sim -- aero-calibrate`). The model is
`C_D·A = C_front·Σ a·cosᵐφ + C_base(λ)·Σ b·cosᵐψ + C_f·S_wet`: forebody pressure on the windward
projected area falling off with each surface's inclination, base suction on the leeward area falling
from `C_near` to `C_far` over a fineness `λ = L/√A`, and skin friction on the wetted area. Six
coefficients were fitted to the eleven calibration bodies, residual 0.032 in squared log error, every
body within ±10 %:

| Held out | Registered | Predicted | |
| --- | --- | ---: | --- |
| S1 cube face-on | 0.95–1.15 | 0.869 | **fails** |
| S2 square plate | 1.17–1.28 | 1.374 | **fails** |
| S3 cube edge-on | 0.66–0.96 | 0.637 | **fails** |
| S4 tapered tail against blunt base | lower | 0.899 against 0.894 | **fails** |

**The fit is what says why.** It put base suction at 2.6 falling to nothing over a fineness of 0.23,
which no body has: the eleven round bodies could only be matched by a suction that vanishes the moment
a body has any length. That is White's own contrast read backwards. **A round section reattaches
sooner than a square one**: the flat-faced cylinder at `L/d` 1 is 0.90 where the cube is 1.07, with
the same forebody, so the difference is base suction the flow had already recovered from on the round
body. Fitted on round bodies, the model cannot transfer to square ones, and every hull this game builds
is square-sectioned. The tapered tail failed for a second reason: every leeward surface counted as base,
however shallowly it closed, where a boat-tail's flow stays attached and carries no base suction.

### Round 2, registered before it is run

**Two changes, each a response to one half of round 1's diagnosis and neither to a number.**

1. **A leeward surface is base only where the flow has left it.** Its weight runs from 0 when its
   surface meets the flow at 12° or less to 1 at 22° or more, smoothly between: boat-tail flow forms a
   separation bubble from 12° to 20° and is fully separated at 22°, with the least drag near 15°
   (grooved boat-tail study; Heriot-Watt heavy-vehicle boat-tails). The angle is read off the
   reconstructed normal, as the forebody's is.
2. **Base suction is calibrated on square sections, because the hulls are square.** The calibration
   set becomes the cones from 10° to 75° (forebody falloff), White's **square plate** 1.18 and
   **cube** 1.07 (base suction of a sharp-edged square body, short), and the flat-faced cylinders at
   `L/d` 2, 4 and 8 (long bodies, where the flow has reattached and the section's shape matters less —
   an assumption, stated here so it can be found).

**What that costs the criteria, said plainly.** S1 and S2 were predictions in round 1 and failed. In
round 2 the cube and the plate are calibration, so matching them is by construction and is **not
evidence**; the owner's two criteria are recorded as having been tested once and failed, not as
passing. What remains held out, with its band unchanged:

| # | Held out in round 2 | Falsified by |
| --- | --- | --- |
| S3 | cube edge-on, `C_D` 0.81 ± 0.15 — seen in round 1, never fitted | outside 0.66–0.96 |
| S4 | tapered tail against blunt base, equal frontal area | tapered not lower |
| R1 | flat-faced cylinders at `L/d` 0.5 and 1, reported only | — a known gap: round sections reattach sooner than the square ones this is fitted to |

**If round 2 also fails S3 or S4, there is no round 3 on these bodies.** Two looks at one held-out set
is already one more than a clean test allows; a third would be fitting to it. A further revision would
need held-out data neither round has seen.

### Round 2: passes on the letter, and the fit says the model is wrong

| | Registered | Result |
| --- | --- | --- |
| S3 cube edge-on | 0.66–0.96 | **0.667** — inside by 0.007 |
| S4 tapered tail against blunt base | lower | **0.934 against 0.935** — lower by 0.1 % |
| R1 cylinders `L/d` 0.5, 1 (reported) | — | 1.118 (−2.8 %), 0.950 (+5.6 %) |

**Both pass as written and neither is evidence.** A pass by 0.7 % of a band and a boat-tail worth a
tenth of a per cent are inside any honest noise, and the fit that produced them misses its own
calibration cube by **−14.8 %**. Its coefficients say why: base suction falls from 2.04 to 0.004
over a fineness of 0.24, so any body longer than about half its width carries all its drag on the
forebody, and nothing about its tail can matter. That is not a subsonic body — a flat-faced cylinder's
base carries a tenth or more of its drag at any length — and it is why the tapered tail cannot help.

**The cause is structural, and it is one variable doing two jobs.** Fineness stood in for *has the flow
recovered by the time it reaches the base*, and the answer depends on where the flow left the body. On
a cone it stays attached down the surface and leaves at the base edge, so the base sees a gentle
wake at any length; on a flat-fronted body it separates at the front rim and reaches the base only if
it has reattached along the sides first. The slender cones pulled suction down with length to be slow,
and that starved the cube. Round 3 separates the two, and tests on data neither round has seen.

### Round 3, registered before it is run

**One structural change: base suction depends on where the flow left the body.**
`C_base = C_att + b·(C_sep − C_att)·R(λ)`, where `b` is how bluff the front is — the forebody's own
pressure-weighted share of the silhouette, `Σ a·cosᵐφ / A`, one for a flat front and small for a
slender cone — and `R(λ) = 1 / (1 + (λ/λ_r)⁸)` is whether the body is too short for flow that left a
front rim to reattach along its sides. A flat-fronted body shorter than `λ_r` keeps the full separated
suction; a longer one, or one whose front never shed its flow, sees the attached value. Leeward surfaces
count as base only where separated (round 2's 12° to 22°). Six coefficients again: `C_front`, `m`,
`C_att`, `C_sep`, `λ_r`, the lab's skin friction.

**Calibration is every body the earlier rounds used**, since all of them have now been seen: the cones
from 10° to 75°, all five flat-faced cylinders, the square plate and the cube.

**Held out, and never used by any round: White's turbulent ellipsoids** — `L/d` 0.75, 1, 2, 4, 8 at
**0.2, 0.2, 0.13, 0.1, 0.08**, flown along the axis. They are the attached-flow, gradually-closing body
S4 is about, and a hull with a rounded nose and a tapered tail is exactly what a player builds to go
fast.

| # | Prediction | Falsified by |
| --- | --- | --- |
| T1 | every calibration body within ±12 % of White — a fit that misses its own data is not a model | any outside |
| T2 | each ellipsoid within 0.5× to 1.75× of White's turbulent value | any outside |
| T3 | the ellipsoids at `L/d` 4 and 8 read below the one at `L/d` 1 | either not below |

S3 and S4 are reported beside these and not counted: both have been seen twice.

## What this changes elsewhere

- [document-of-intent.md](document-of-intent.md): *Not a flight model* is reversed as a whole and
  points here. The RelativeTopSpeed section ("detection is refused", "one or the other") is
  superseded by AR15.
- [backlog.md](backlog.md): `K25` is the milestone. `K10` is retired by the strip. `K16`'s caveat is
  withdrawn when AR5 ships. `K22`'s wake is AR4's base drag. `K23`'s criteria are re-registered
  under AR8. `K24`'s degenerate plate becomes AR9's wing detector.
- [configuration.md](configuration.md): the Top speed section and the drag and lift rows are
  removed with the strip. New rows arrive with each rebuild step.
- **RelativeTopSpeed's README** says to disable this mod's `EnableTopSpeed` when RTS owns speed
  control, and links a `docs/thermal-dynamics.md` that is not in that repository. Both go stale
  when the strip ships. That repository is not edited from here.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-25 | **Round 3 registered before it runs**, on data no round has seen: White's turbulent ellipsoids. Base suction becomes a function of the front's bluffness and of reattachment length rather than of fineness alone; calibration is every body the earlier rounds saw, and the fit must meet its own data within ±12 %. |
| 2026-09-25 | **Round 2 recorded: S3 and S4 pass on the letter, by 0.007 and 0.1 %, and are not counted as evidence.** The fit misses its own calibration cube by 14.8 % and puts no base suction on any body longer than half its width, so a tail cannot matter. The diagnosis is that fineness conflated front-edge separation with attached flow. |
| 2026-09-25 | **Round 2 of step 1 registered before it runs.** Leeward surfaces count as base only where the flow separates (12° to 22°, sourced), and base suction is calibrated on the square plate and cube with long cylinders for the reattached end. The owner's S1 and S2 become calibration and are recorded as tested once and failed; S3 and S4 stay held out, and a failure ends revisions on this data. |
| 2026-09-25 | **Round 1 of step 1 fails, recorded as run.** The profile's geometry is built and checked against hand geometry (`AeroGeometryTests`); the drag model fitted to White's round bodies predicts all four held-out bodies outside their bands. The diagnosis is written beside the numbers: a round section reattaches sooner than a square one, and the model counted a boat-tail's attached surfaces as base. |
| 2026-09-24 | **Step 1's criteria are registered before any of it is built.** The reference is White's Table 7.3; calibration is the cones and the flat-faced cylinders, and the owner's cube and plate criteria are held out with two more (edge-on cube, boat-tail pair), so none can pass by construction. The finned-hull rig moves to step 2, because a fin restores through lift, not drag; step 1 tests the drag-stabilised case, a shuttlecock. `K5`'s population pair is re-registered unchanged, and a failure is recorded as a realism-against-playability conflict for the owner rather than retuned away. |
| 2026-09-24 | **Step 0 is built.** The force application, the lift and pressure plumbing, the top-speed port and its nineteen settings, and the shape factor on the friction row are removed; the retired `ProtoMember` numbers are recorded in `NoSettingReusesANumberThatWasDeliberatelyRetired`. `RtsApi.cs` is vendored unchanged from RelativeTopSpeed `0b4b21a` under `Thermodynamics/RelativeTopSpeed/`, and `RelativeTopSpeedLink` guards every call to it. **One departure from the inventory**: the solver's normal buffer went too, because nothing in a step read it (see the table). **Backlog rows retired into `K25`**: `K10` (the port, now removed); `K6` and `K22` (the shape term — its wake is `AR4`'s base drag); `K23` (lift — its criteria are re-registered under `AR8`); `K24` (the degenerate plate — `AR9`'s wing detector); and `K16` (the centre-of-pressure indicator — its caveat ends when `AR5` ships). What each measured stays in [thermal-model.md](thermal-model.md#change-log). |
| 2026-09-24 | **Owner's decisions recorded** (see *Decisions taken*): cruise curve then drag; no SEDrag block extensions; re-entry heating configurable; forces simulated on server and client; the existing aerodynamics is stripped and rebuilt with switches; the embedded RelativeTopSpeed port is removed in favour of the original mod's read-only API (13 getters, channel 2772681332). The strip-and-rebuild inventory and switch table were added. |
| 2026-09-24 | Page created. The owner asked for aerodynamics in line with SEDrag, then clarified the goal: *as realistic as possible, and learn what we can from SEDrag*. The SEDrag source (571920453) was read in full: `SEDrag.cs`, `GridCalculation.cs`, `DragBase.cs`, `Core.cs`, `LiftDefinition`, `AeroProperties.xml`. Twenty-two parts were evaluated against this mod and against subsonic physics. The drag and terminal-speed tables are derived from the source by hand, not measured in a session. An earlier reading in the same conversation, that SEDrag's drag was about 20× *weaker* than this mod's, was wrong: it missed `SpeedMod`, and above 27 m/s the relation inverts to 163×. |
