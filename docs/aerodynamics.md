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
| **Lift** | Comes from circulation around **lifting surfaces** at an angle of attack. The baseline small-angle approximation is `CL ≈ 2πα·AR/(2+√(AR²+4))`; stall depends on geometry and flow, and induced drag is `CL²/(πeAR)` on the same reference area. | The sideways remainder of the Newtonian sum. It can never exceed drag and cancels on any symmetric hull (median lift-to-drag 0.057). | A flat-plate curve on the whole outline, `(1−cos 2πx)/2`, with a lift constant about 330× its drag constant, plus authored per-block normals. |
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

**The baseline approximation needs planform area and span:**
`CL_α = 2π·AR / (2 + √(AR² + 4))` (Helmbold). Its low-aspect-ratio limit is
`CL_α ≈ (π/2)·AR`; it is not a validated universal law for boxes or arbitrary lifting bodies.
[Saltzman, Wang and Iliff's flight analysis](https://ntrs.nasa.gov/citations/19990052613)
compares body and wing-body lift slopes with theory and shows why reference area must be stated.
The paper's discussion of Figure 2 also reports lifting bodies above and below the theoretical
relationships. Body shape and vortex lift therefore remain limits of this approximation, to be
checked on independent rigs. The former 12–15° stall statement was an unsourced generic
estimate, not a measured boundary for the plates or block hulls modelled here. Stall must be
validated over the intended geometry and flow range. The candidate separated-flow plate
approximation is `CN ≈ 1.2·sin α`, subject to the joint force-accounting check below. On the
drag side, AR11 then charges for the lift. Requirement: body lift is computed from the planform
the air sees, with that law, and replaces the Newtonian transverse sum. **`K23`'s four criteria are
re-registered, not inherited.** Its "p95 lift-to-drag ≤ 2" was written against a model that cannot
make lift. Real aircraft reach 10–20, so the bound becomes a rig check against the formula
(a flat plate at 10° gives `CN` within 10 % of the textbook value). The population criterion that
keeps bricks from flying without thrust stays.

The primary [Ortiz, Rival and Wood plate experiment](https://www.mdpi.com/1996-1073/8/4/2438)
is a candidate independent post-stall check. Its [eight normal-incidence measurements](../tests/benchmarks/aerodynamics/plate-reference-ortiz-2015.csv)
and [provenance](../tests/benchmarks/aerodynamics/plate-reference-ortiz-2015.txt) retain the full
aspect-ratio range and observed transverse residuals. Figures were visually inspected; no graph
ordinates have yet been digitized. These finite-thickness, low-Reynolds-number plates do not
establish the accuracy of a ship-scale body-lift law or locate its attached-flow stall boundary.
The `1.2·sin α` expression above remains an approximation requiring a combined lift-and-drag
comparison, not an experimentally verified law over every post-stall angle.

A second primary control is [Mizoguchi, Kajikawa and Itoh (2016)](https://doi.org/10.2322/tjsass.59.56):
rectangular plates, AR 0.5–1.5, Re 52,000, chord 90 mm, thickness 1 mm and turbulence 1.4%.
The experiment sweeps incidence both ways. It reports peak lift near 20° for AR 1.3–1.5,
28° at AR 1.2, and stall hysteresis over AR 0.7–1.2. Reported CL/CD uncertainty is ±0.03
at AR 1.5 and ±0.05 at AR 0.5 (95% intervals). These are scoped observations, not
ship-scale coefficient assignments; [source and inspection record](../tests/benchmarks/aerodynamics/plate-reference-mizoguchi-2016.txt).

**Consequence for the planned instrument:** a direction-only static profile cannot represent
both branches of a hysteresis loop. Before promoting lift, the lab must test increasing and
decreasing incidence separately and retain their discrepancy. A single fitted curve cannot be
called validated merely because it passes through one branch. A stateful stall mechanism or
an explicitly measured approximation remains necessary; this evidence does not establish its
coefficients for the game's Reynolds-number range. The primary Figure 3/4 symbols have now
been digitized under the protocol below; no lift code has been tuned to them.

#### Plate-figure extraction protocol

The primary circles and squares in Mizoguchi Figures 3 and 4 are vector objects, distinct from
the older comparison curves. Exploratory inspection identified their geometry and the legend
objects; it did not fit a force law. The extraction will be bound to the publisher PDF hash,
retain marker centres and error-bar endpoints in page coordinates, calibrate against the plot
frame, and exclude legends and comparison lines explicitly. Report the plotted angle rather
than snapping to integer incidence: the plotted centres do not lie exactly on integer degrees.
Retain every primary marker, including repeated near-stall angles, without inferring an
incidence-sweep branch not labelled by its symbol. Store CL and CD as separate observations.

Before using these observations for a model comparison, require analytical coordinate-transform
controls, rejection of changed source/layout or ambiguous error bars, visual overlay of all
retained points, and inspection of the source figure's scale and legend. Use a conservative
0.5 PDF-point extraction bound in each coordinate, reported separately from the figure's 95%
measurement interval. This is graph digitization, not access to the experiment's raw samples;
no model acceptance criterion or tuning follows from producing the dataset.

The [retained reference CSV](../tests/benchmarks/aerodynamics/plate-reference-mizoguchi-2016.csv)
contains 246 observations: 63 AR=1 and 60 AR=0.5 points for each coefficient. The
[source-bound extractor](../tools/corpus/plate_reference.py) and its analytical/fault controls
are reproducible from the publisher PDF. A full-page overlay was visually checked: every retained
marker lies on a primary symbol; neither legend markers nor the older comparison curves are
selected. The 0.5-point bounds correspond to about 0.184° and 0.00790 CL or 0.00615 CD,
separate from measurement confidence intervals. Printed digits preserve the coordinate conversion,
not experimental precision. All 235 Python checks pass, with one unrelated historical-data
integration check skipped. This validates extraction, not an aerodynamic model.

#### Small-angle compatibility lab, registered before computation

Evaluate the already proposed unfitted Helmbold law `CL = CL_α·α` against every retained CL
observation whose plotted incidence centre satisfies `1° ≤ |α| ≤ 10°`, for both available
aspect ratios. Use radians in the law. Propagate the recorded incidence extraction bound
through the linear slope, and expand each measured 95% interval by its separate coefficient
extraction bound. Report interval overlap and signed centre residual for every selected row;
do not fit a slope, offset, vortex term or stall angle, average away a branch, or extrapolate
outside this subset. Missing/nonfinite inputs, empty coverage or absent aspect/sign strata
invalidate the diagnostic.

This is a pointwise compatibility check of a pre-existing proposal, not a new population or
flight-model acceptance threshold and not a confidence statement about simultaneous coverage.
The source plots were already inspected during extraction; this is not a blinded experiment.
An implementation matching its own formula still needs this independent physical comparison.
An incompatible observation is retained as evidence of model scope, not deleted or reclassified
as a bad measurement. Drag and hysteresis remain separate required validation work.

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

**Force accounting is part of that requirement.** [NASA Glenn's definition](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/induced-drag-coefficient/)
uses the same wing-area basis for lift and induced drag, and adds induced drag to **zero-lift**
drag. The current profile contains incidence-dependent pressure drag. It cannot simply be
relabeled zero-lift drag. Before connecting lift, the rig must explicitly separate the attached-flow
contribution from separated-flow pressure drag, and check the sum against independent plate
lift **and** drag observations. A post-stall normal force already has a streamwise component;
counting that component again as another drag term would double-count the same force.
This is a required model-accounting check, not evidence that the current drag-only code is faulty.

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
   term off. Its original no-force checks were replaced by force-write ownership and API reach
   checks when the step-1 adapter landed.
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

### Round 3: fails its own data and its held-out bodies, and the new data says why

| | Registered | Result |
| --- | --- | --- |
| T1 calibration within ±12 % | all | **fails** — cylinder `L/d` 2 +15.0 %, cube −13.7 %, cylinder `L/d` 0.5 −12.1 % |
| T2 ellipsoids within 0.5–1.75× | all five | **fails** — every one 3.6–4.2× White's turbulent value (0.773, 0.718, 0.542, 0.411, 0.332) |
| T3 long ellipsoids below the sphere | both | holds — 0.411 and 0.332 against 0.718 |
| S3, S4 (reported) | — | 0.683; 0.978 against 0.978 |

**The forebody law is what is wrong, and the ellipsoids are what showed it.** Every version so far
made a surface's pressure `C·cosᵐφ` — never negative. A rounded nose is low-drag because of
**suction on its shoulders**: potential flow puts `Cp = −1.25` on a sphere's equator, and that
suction on forward-facing surfaces cancels most of the stagnation pressure. A law that cannot go
negative prices a rounded nose like a blunt one, which is what 0.718 against 0.2 is. The calibration
cones already said what the law is: White's cone drags run close to linear in `sin²θ` — 1.15 at 90°
falling by about 0.88·cos²θ — which is a local `Cp = P₀ + P₂·cos²φ` with nothing stopping `P₀` from
being negative.

**One question is recorded rather than decided, because deciding it now would move a criterion after
seeing the result** (`E11`). White's *turbulent* ellipsoid values are smooth bodies past the drag crisis.
Whether a hull built of cells — stepped where it is not built of slope blocks — ever reaches that
regime, or sits nearer a rough sphere's 0.47 at the same Reynolds number, is a physical question this
lab cannot answer. It is noted so the next round's choice of comparator can be judged.

### Round 4, registered before it is run

**The forebody law becomes `Cp = P₀ + P₂·cos²φ`**, where `φ` is the angle between a surface's
reconstructed normal and the oncoming flow, and `P₀` may be negative. That is the form White's cones
take in the calibration data — drag close to linear in `sin²θ` — and the form that can put suction on a
rounded shoulder. How bluff a front is, for the base term, becomes the same measure: `b = Σ a·cos²φ / A`.
Base suction is round 3's; leeward surfaces are round 2's. Six coefficients: `P₀`, `P₂`, `C_att`,
`C_sep`, `λ_r`, and the lab's skin friction. Calibration is round 3's set.

**Held out, never used by any round**, from two sources outside White:

| # | Body | Reference | Falsified by |
| --- | --- | --- | --- |
| V1 | the calibration set itself | every body within ±12 % | any outside |
| V2 | a hemisphere, curved side into the flow | 0.42 (Engineering ToolBox) | outside 0.27–0.57 (±35 %) |
| V3 | a hemisphere, flat side into the flow | 1.17 (Engineering ToolBox) | outside 0.99–1.35 (±15 %) |
| V4 | a sphere | 0.47, a rough sphere at Re 10⁶ (Wikipedia, *Drag coefficient*) | outside 0.31–0.63 (±35 %) |

**Why a rough sphere and not a smooth one**: a hull of cells is stepped wherever it is not built of slope
blocks, which is roughness at the scale of the body, and a smooth sphere's 0.1 at Re 10⁶ is the drag
crisis a rough one does not reach. This is chosen before V4 is run and *after* round 3 read the
ellipsoids against smooth-turbulent values — so it is stated here as a choice, and round 3's T2 stays a
failure on the page. The ellipsoids are reported beside the result, against both White columns.

### Round 4: fails, and the instrument, not the drag law, is most of why

| | Registered | Result |
| --- | --- | --- |
| V1 calibration within ±12 % | all | **fails** — cone 10° +18.4 %, cylinder `L/d` 2 +16.8 %, cube −15.2 %, and three more |
| V2 hemisphere, curve first | 0.27–0.57 | **0.690, fails** |
| V3 hemisphere, flat first | 0.99–1.35 | **0.980, fails** |
| V4 sphere | 0.31–0.63 | **0.675, fails** |

**The fitted suction term came out positive, +0.20, and that sent the investigation to the geometry.**
The cones' drag is linear in the *true* `sin²θ`, but the model never sees the true angle — it sees the
normal reconstructed from cells. Measured against exact geometry, with no drag data involved
(`AeroIncidenceTests`), the reconstructed mean `cos²φ` over each cone's front:

| cone | exact | radius 8 | radius 16 | radius 32 |
| --- | ---: | ---: | ---: | ---: |
| 10° | 0.030 | 0.063 | 0.060 | 0.060 |
| 30° | 0.250 | 0.208 | 0.217 | 0.220 |
| 60° | 0.750 | 0.503 | 0.619 | 0.709 |
| 75° | 0.933 | 0.390 | 0.640 | 0.786 |
| hemisphere | 0.500 | 0.487 | 0.499 | 0.505 |

**Two separate limits, and only one of them costs anything.** On blunt bodies the reading converges as
the rig gets finer: a 75° cone eight cells in radius is two cells deep, so nearly every cell is a rim
cell and the radius-one gradient there points diagonally — an artefact of the rig, not of the air. On
slender bodies it does not converge — a 10° slope reads as twice its `cos²φ` at every size, which is the
radius-one reconstruction's known floor — but `cos²φ` is small there, so it moves little drag. **Four
rounds were fitted partly to the first limit.** The rigs were eight cells in radius from the start, and
nothing had measured the instrument before the coefficients were fitted through it.

### Round 5, registered before it is run: every body is used, and each is predicted without itself

**Every independent held-out set is spent**, and a fifth holdout drawn from the same pages would be a
pretence. The method that stays honest on a small dataset is **leave-one-out cross-validation**: fit
the six coefficients to every body but one, predict that one, and repeat for each body. Each body's
prediction is then out of sample, and the in-sample fit is reported beside it.

* **The instrument**: every rig is 24 cells in radius, where the blunt-cone reading has converged to
  within about 15 % of exact and the hemisphere to 1 %.
* **The model**: round 4's, unchanged — forebody `P₀ + P₂·cos²φ`, base suction by bluffness and
  reattachment, leeward separation between 12° and 22°.
* **The data**: White's cones from 10° to 90° (90° is the disk, 1.17 — Hoerner gives the same 1.17 with
  a rear-side pressure of −0.42), the five flat-faced cylinders, the cube face-on 1.07 and edge-on 0.81,
  the square plate 1.18, the ellipsoids' **subcritical** column (0.5, 0.47, 0.27, 0.25, 0.2 — its sphere
  is the rough sphere's 0.47, which is round 4's stated reason for the comparator), and the two
  hemispheres (0.42, 1.17).

| # | Prediction, out of sample | Falsified by |
| --- | --- | --- |
| W1 | at least 80 % of the bodies predicted within ±25 % of their reference | fewer |
| W2 | no body predicted worse than ±50 % | any |
| W3 | the tapered tail below the blunt base by at least 5 % — seen before, so reported, and the one quantity a player shaping a tail would feel | not |

**If W1 or W2 fails, the model is recorded as unable to reach textbook drag on cell-built bodies at
this resolution, and step 1 proceeds with the best in-sample fit and its errors stated beside every
force**, rather than with a sixth round. The owner's S1 and S2 stay as recorded: tested once, failed.

### Round 5: the bars are missed, the fallback is taken, and the errors ship with the model

| | Registered | Result |
| --- | --- | --- |
| W1 bodies within ±25 %, out of sample | at least 18 of 22 | **14 — fails** |
| W2 no body worse than ±50 % | none | **three — fails**: hemisphere curve-first +84 %, ellipsoids `L/d` 0.75 +56 % and 2 +58 % |
| W3 tapered tail below blunt base by 5 % | yes | **0.691 against 0.824, 16 % lower — holds** |

**The fitted suction term is negative now**, −0.082, which is the shoulder suction the physics said
was missing; resolving the geometry was what let the fit find it. **What remains is one systematic
bias**: stubby rounded bodies read too draggy — the hemisphere curve-first 0.72 in-sample against
0.42 — because a pressure law applied locally to cell-scale normals recovers only part of a rounded
shoulder's suction. Blunt bodies, which is most of what players build, sit mostly within 15 %.

**So, as registered, the model is recorded as unable to reach textbook drag on cell-built bodies at
this resolution, and step 1 proceeds with the in-sample fit**, `AeroCoefficients.Fitted`:

| `P₀` | `P₂` | `C_att` | `C_sep` | `λ_r` |
| ---: | ---: | ---: | ---: | ---: |
| −0.08209 | 0.54126 | 0.33405 | 0.69401 | 1.13172 |

**What a player will feel from the bias**: a rounded or pointed nose helps, and less than it would in
air; a tapered tail helps by about as much as a real one; a brick is about as draggy as a brick is.
`AeroCalibrationTests` holds the shipped constants to the fit and pins round 5's outcome, so that an
improvement fails a test and gets read rather than passing silently. Skin friction at ship scale is
Prandtl's `0.074·Re^−0.2`, not the lab's fitted 0.00408, which stands in for the wind-tunnel Reynolds
numbers the reference data were taken at.

### Step 1b, first flight: the calibration does not transfer to the scale players build at

The profile (`AeroProfile`: 26 or 98 directions, an `AeroSummary` each, interpolated) and the flight lab
(`aero-flight`, a rigid body at 60 Hz) were built to run S5–S8. **Two faults came out before any
criterion could be read, and neither is the air's.**

1. **The guard held the restoring moment.** It capped any torque opposing the spin at what stops it, so
   at each swing's extreme — spin near zero — the aerodynamic moment that should turn the body back was
   cut to nothing, and the shuttlecock hung at 18° with the guard binding fifteen times. The guard now
   caps the damping alone, which is what `AR22` is for; a restoring moment reverses a turn by right.
2. **The in-flight damping was missing.** Only a hull turning in still air was damped. In flight the
   dominant term is the tail swinging across a 50 m/s flow, which tilts the local air — `AR6`'s `ω × r`
   — and the drag is now evaluated in the air at the centre of pressure, turn included.

**Then the probe that says why the shuttlecock still decays too slowly.** At 50 m/s its drag is
136 kN: a drag area of 88.6 m² for a 7×7 plate of 306 m², a coefficient of **0.29 where a plate reads
1.17**. A 7×7 plate is nearly all rim, and the block normal is the occupancy gradient at radius one, which
at a rim points diagonally. Round 5 calibrated at radius 24, where rims are a few per cent of a face;
**the scale a hull is built at is radius 3 to 8, where they are most of it.** So the model is right-ish
where nobody flies and wrong where everyone does, and no criterion registered so far could see that.

### Round 6, registered before it is run — and why there is one

**Round 5 said there would be no sixth round, and this is one.** The stopping rule was written against
fitting the same held-out data a sixth time. What forces this round is not a drag result but a
**defect in the instrument, found by a game-scale rig**, and the fix is judged against exact geometry
before any drag data touches it. The calibration has to be redone because the coefficients are fitted
through the instrument; the optimism that repeated model selection adds to leave-one-out estimates is
stated here rather than hidden.

**The change: a face's surface normal is read from the surface around that face, not from the block's
occupancy gradient.** For each exposed face, the surface's depth along the face normal is read in the
3×3 columns around it, up to two cells in or out; a column past an edge has no surface and is skipped,
not read as a slope; a plane is fitted to what was found. A flat face is then flat to its rim, and a
staircase reads as the slope it is.

| # | Prediction | Falsified by |
| --- | --- | --- |
| X1 | every front face of a square plate and of a cube face-on reads cos²φ = 1, rims included | any below 0.999 |
| X2 | a 45° wedge's staircase reads cos²φ = 0.5 ± 0.02 on its interior faces | outside |
| X3 | a hemisphere's mean cos²φ within 0.03 of the exact 0.5 at radius 8 | outside |
| X4 | **resolution invariance**: the square plate, the cube face-on, the 30° cone and the hemisphere, each at radius 4, 8 and 24, read `C_D` within ±10 % of their radius-24 value | any outside |
| X5 | round 5's W1 and W2 on the new instrument | as before |
| S5–S8 | as registered | as registered |

### Round 6, and the moment rigs: what step 1's drag and moments are

**The instrument.** X1 holds — every front face of a 7×7 plate and an 8-cell cube reads cos²φ = 1,
rims included — and X2 holds, a 45° staircase reading 0.5 within 0.02. **X3 failed on the first
implementation**: a radius-8 hemisphere read 0.537 against 0.5, because a column whose surface lay
more than two cells down was skipped as an edge, and a steep shoulder read as flat. The search now looks
six cells in or out, so a column with no surface that close is an edge and one with a surface further
down is a steep drop; X3 then holds. **Because that revision was made after X3 was read, it was judged
again on geometry it was not tuned on** — hemispheres at radius 4, 6, 12 and 16, a sphere, cones at 30°,
45° and 60° at two radii — and every one reads within 0.044 of exact (`AeroIncidenceTests`). The old
normal read the 60° cone at radius 8 as 0.503 against 0.75.

**The drag.** Refitted through the new instrument, round 5's bars are missed again — **15 of 22 bodies
within 25 % out of sample, three past 50 %**, all rounded (the hemisphere curve-first +67 %, the
ellipsoids at `L/d` 4 −51 % and 8 +70 %). **X4 holds, and it is the property that matters for a game**:
from radius 4 to 24 the square plate and the cube move by 0.1 %, the hemisphere by 2.9 % and the 30°
cone by 9.2 % against a 10 % bar. The 7×7 plate that read 0.29 reads 1.04. The shipped constants are this
fit, `AeroCoefficients.Fitted`:

| `P₀` | `P₂` | `C_att` | `C_sep` | `λ_r` |
| ---: | ---: | ---: | ---: | ---: |
| −0.08793 | 0.56124 | 0.35951 | 0.56396 | 1.21170 |

**The moments**, `aero-flight`, 60 Hz, sea-level air, 50 m/s:

| # | Registered | Result |
| --- | --- | --- |
| S5 | back within 5° after a 20° kick, and still there after 30 s | **holds** — last outside 5° at 29.0 s, 1.24° after 60 s. By one second: a thin margin, recorded as one |
| S6 | flown plate-first, diverges past 45° | **holds** — swings round to 180° |
| S7 | spin halves in finite time, sooner in thicker air | **holds** — 324 s at density 1, 649 s at 0.5 |
| S8 | the guard binds on none of them | **holds** — zero binds |

**What step 1 has in the offline library.** Drag from a hull's own silhouette, forebody and base, calibrated in
the open and held invariant across the scales hulls are built at; a centre of pressure the drag acts
through, so a hull with its area behind its centre of mass flies straight and one with it ahead turns
round; damping of a turn in flight and at rest; and a guard that never binds in a resolved case. **What it
does not have**: lift, so a finned hull is still step 2's rig; and a rounded nose reads up to 67 % too
draggy, so streamlining is under-rewarded. `AeroCalibrationTests` and `AeroFlightTests` pin all of it.

**The game adapter now submits drag and moments each physics tick once its profile is ready.**
The engine boundary and remaining validation are recorded below. Native handling and P1/P2
population validation remain open under `K25`.
The earlier round-6 validation passed the complete solution build and 2,494 offline cases on
2026-09-26, with corpus walks not opted in. The flight command reproduces S5–S8; this is synthetic-rig evidence, not a live-game
handling observation. Multi-cell diagonal inertia is independently checked below; the flight
results above still concern the named synthetic rigs only.

### Flight-profile mass properties

`AeroProfile` treats each block as a uniform cuboid with its mass spread across its oriented
bounding box. Its centre of mass is the mass-weighted mean of the block centres. The diagonal
inertia about that centre adds each cuboid's central inertia and its parallel-axis contribution:
`I_x = Σ m ((size_y² + size_z²)/12 + r_y² + r_z²)`, with cyclic permutations for Y and Z.
Dimensions and offsets are in metres, so inertia is in kg·m². Changing the lattice or splitting
one uniform cuboid into equal-density cells preserves these mass properties.

**The independent check found a defect.** The previous implementation used a one-cell cube's
central inertia for every block. A 12,000 kg block occupying 1×2×3 cells at 2.5 m pitch read
12,500 kg·m² on all axes instead of **81,250, 62,500 and 31,250**. Five of seven analytic,
rotation and decomposition checks failed before the correction; all seven pass afterward.
`AeroProfileMassTests` also checks a small-grid cuboid, unequal masses and translation. The
existing S5–S8 rigs use single-cell blocks and their acceptance results are unchanged.

**Limits of this instrument.** Mass is uniform inside each block's bounding box; internal machinery
and inventory distribution are not resolved. Only the diagonal is stored, so the free-flight lab
is suitable for bodies whose principal inertia axes coincide with the grid axes, including its
cube and shuttlecock. Arbitrary asymmetric hulls need the products of inertia before their
coupled rotations can be claimed accurate. This is not a replacement for the game's mass tensor.

### Flight-lab validity and integration checks

`AeroFlightLab` rejects empty or nonfinite mass properties, invalid inputs and durations that do
not contain a positive tick or cannot fit its step counter. A nonfinite force, torque, spin or
heading aborts the measurement before it can be interpreted as stability. A spin that has not
halved within the observation limit returns positive infinity, meaning censored, not zero.
The duration is rounded to the nearest whole tick; the default is 1/60 s.

The validity checks reproduce three earlier failures: an empty or zero-duration flight could
look stable, negative spin could appear to halve immediately, and an infinite duration could
run for billions of ticks. The interrupted infinite-duration reproduction is not a completed
test run. Half-life now compares the magnitude of either spin direction and timestamps a
crossing after the tick that crossed it, removing a one-tick underestimate.

`AeroFlightTests` checks both the physical controls and the integrator. In vacuum the initial
20° kick persists for all 3,600 ticks. At 120 and 240 Hz the restoring rig still meets S5 without
the numerical guard, with final heading within 0.1° and the last 5° crossing within 0.1 s of
60 Hz. The spin clock also agrees within two default ticks with an independent ODE solution:
for `I dω/dt = -ρ C K ω|ω|/2`, the half-life is `2 I/(ρ C K |ω₀|)`.

For the four-cell cube used by that check, each cell is 2.5 m across and the half-width is
`a = 5 m`. Face-centre quadrature gives
`K = 2 × 4 × 6.25 × Σ |z|(a² + z²) = 17,968.75 m⁵`, with
`z = {-3.75, -1.25, 1.25, 3.75} m`. This validates the clock against the model's stated spatial
quadrature; it does not claim that face-centre quadrature is the exact continuous-surface drag.

### Profile construction and tick cost

`aero-profile` measures synthetic solid cubes of 64, 512 and 4,096 blocks at both profile
resolutions. Construction starts with occupancy cached, so it excludes the grid's cold occupancy
build. Four rows separate construction, the first force evaluation, 1,000 settled evaluations,
and a centre-of-mass shift followed by evaluation. Each reports fastest, median, slowest, spread,
repeat count, stopping reason and maximum allocated bytes per repeat, alongside host, UTC and
the core binary's MVID. It is not population or live-frame evidence.

**The question, before measurement:** does the supposedly constant-time tick hide a surface walk
on its first use or when the centre of mass changes? The source puts `MeasureDamping` behind a
lazy check, so the prediction is that first and shifted evaluations allocate and grow with surface
size while settled evaluations allocate nothing. Construction is also synchronous; its measured
cost determines what must be budgeted before AR19 reaches the game. No coefficient or handling
criterion is selected from these timings. `AeroProfileLabTests` checks the instrument's sample
and work counts rather than imposing timing thresholds on a shared test machine.

**Measured 2026-09-27 UTC on `fedora`, .NET 9.0.19.** The
[controlled run](../tests/benchmarks/aerodynamics/profile-cost-tiering-disabled-2026-09-27.txt)
uses `DOTNET_TieredCompilation=0`, so the runtime does not change compilation tiers during the
ladder. At 98 directions, construction reads **3.387/3.398 ms** fastest/median for 64 blocks,
**13.646/13.714 ms** for 512, and **68.062/68.754 ms** for 4,096. Their repeat spreads are
1.93 %, 4.99 % and 2.40 %. The largest build allocates **6,332,632 bytes** and even its fastest
repeat exceeds a 16.7 ms frame. A 26-direction build at that size reads 16.970/17.129 ms,
with 4.92 % spread and its minimum not confirmed before the repeat cap.

The 98-direction settled evaluation remains **1.20–1.21 μs fastest, 1.21–1.22 μs median per
tick** across the ladder, measured in batches of 1,000, with **zero allocated bytes**. First
and shifted evaluations allocate **64 bytes each at every size**. Their one-call timings are
noisier, as the artefact states; the allocation and the source's explicit surface walk are the
evidence for the hidden work, not a marginal timing ratio.

The [initial runtime-default run](../tests/benchmarks/aerodynamics/profile-cost-runtime-default-2026-09-27.txt)
is retained as a different runtime configuration. Its smallest cases have a warmup discontinuity;
its 4,096-block, 98-direction build reads 21.039/21.496 ms. Neither run licenses an optimization
ratio against the other. **Both require budgeted construction before game integration.** The
implementation below prepares initial damping outside force evaluation and verifies identical
force and torque results; simply hiding the first-use walk in a cost average is insufficient.

### Budgeted profiles

`AeroProfileBuilder` replaces the synchronous construction internals. It computes block mass and
bounds, then reads each exposed face and its reconstructed normal once, and streams visibility
rays into the directional sums. `Advance(budget, revision)` charges each block, cell/face visit,
ray step and phase transition; even a ray crossing a long empty gap yields between cells. A work
unit has a bounded amount of geometry work, including a fixed 26/98-direction loop or the fixed
normal-fitting stencil. It does not request a whole-grid occupancy or bounds rebuild.
`AeroProfile.Build` remains a synchronous wrapper for offline callers.

A result is private until construction completes. Grid topology version changes reject unfinished
and finished results; callers must also supply a monotonic input revision for mass or registered face-profile changes, which
`GridModel.Version` does not track. These are single-threaded jobs: the caller must not mutate the
grid concurrently inside `Advance`. Changing input cancels the job; a fresh job rebuilds it.
A cancelled or incomplete job cannot publish a profile.

Damping moments are accumulated alongside the visible skin, so the first force evaluation at
the built centre has no deferred surface walk and allocates zero bytes. A centre change before
the first positive-density damping read must still refresh the moments, even below the later
1 mm cache threshold; the original lazy implementation had not established its cache yet.
`ASubMillimetreShiftBeforeTheFirstReadKeepsLegacyDamping` guards this distinction, found while
adding centre-update tests after the budgeted builder merge. `SetCentreOfMass` retains its previous lazy
refresh and threshold semantics: a later damping evaluation can still cost one pass over exposed
faces. Its basis vectors are read directly, removing the scratch-array allocation. Budgeting engine mass-centre
updates remains necessary before integration; no constant-cost centre-change claim is made here.

**Acceptance fixed before measurement:** all summary fields, force, restoring torque, damping,
pressure centre and mass properties must equal the previous implementation bit for bit across
five hulls (cuboid, shuttlecock, translated multi-cell blocks, enclosed cavity and cone), both
resolutions, and budgets 1/17/4096. `AeroProfileBuilderTests` compares against `LegacyAeroProfile`,
the pre-change arithmetic from `4abfb6dc`, retained only in tests under D8. It also checks budget
accounting, cancellation, empty input and zero first-evaluation allocation. `VoxelCursorTests`
checks 3,000 fixed-seed rays against the unchanged whole-ray walker, with nonzero hit and miss
counts, and a 999-cell gap that cannot finish in one work unit. Physical calibration and flight
criteria remain unchanged.

`aero-profile` also reports `slice-peak-26/98`: each sample is the **slowest** `Advance` call in a
complete build, at 256 units per call. Sample count therefore counts builds, and allocation is
the maximum of individual calls. Constructor work (fixed-size table allocation) is excluded.
The report includes work and slice counts and rejects disagreement between repetitions. List
capacity growth, garbage collection and OS scheduling can still pause a slice: a work budget is
not a wall-clock deadline. Synthetic observed peaks do not establish population or game-frame
performance, and are not compared as speed ratios to the earlier runtime/binary measurements.

**Measured after the implementation, 2026-09-27 UTC:** the
[budgeted profile run](../tests/benchmarks/aerodynamics/profile-cost-budgeted-2026-09-27.txt)
records source `90c74f0`, host `fedora`, .NET 9.0.19, tiering disabled and the core MVID.
For the 4,096-block, 98-direction cube, the per-build peak slice takes **0.0770/0.0839 ms**
fastest/median over 21 builds; the largest observed slice is **0.1184 ms**, a 53.77 % spread.
Each build consumes **319,078 work units in 1,247 slices**. The corresponding 26-direction
peaks are 0.0274/0.0302 ms, with a 0.0541 ms maximum and 97.45 % spread; its minimum remains
unconfirmed at the 40-build cap. Both resolutions allocate up to **147,528 bytes in one slice**,
so the list-growth caveat is material even though the measured slices are short.

Synchronous 98-direction construction on the largest cube reads **7.3370/7.4429 ms**
fastest/median, 3.37 % spread over 40 builds, and 541,920 allocated bytes. First, settled and
centre-shifted evaluations report **zero allocated bytes throughout the ladder**. The largest
cube's centre-shifted evaluation still takes 0.0349/0.0367 ms fastest/median (15.76 % spread,
minimum unconfirmed at 40 repeats), consistent with its retained surface pass. None of these
numbers is a before/after speed ratio. The game-target build and **2,539 offline tests pass**;
corpus walks were not enabled. Engine scheduling, mass-centre updates and P1/P2 population
validation remain open.

### Budgeted centre updates

`PrepareCentreOfMass(centre, faceBudget, out used)` prepares at most the requested number of
exposed faces per call. The old centre and damping remain visible until every face is ready;
completion publishes both together. Changing the requested centre restarts partial preparation,
and the legacy synchronous setter cancels it. The existing cumulative 1 mm cache threshold is
preserved. The caller serializes preparation and evaluation and must wait for success before
using the requested centre for physics; rapid
mass changes can postpone completion. This is preparation, not a new model of inertia.

Acceptance before timing: `AeroCentreUpdateTests` requires bit-identical force, pressure centre,
restoring torque and damping against the legacy implementation for both resolutions and face
budgets 1/7/100000; the cube must visit exactly its 54 exterior faces. Partial work must leave
all four live outputs unchanged. Additional checks cover target replacement, legacy cancellation,
the cumulative threshold, invalid input, zero budget and zero allocation through interrupted
preparation followed by its first evaluation. `aero-profile` adds `centre-slice-peak-26/98`,
whose samples each record the slowest preparation call for a completed shift at 256 exposed
faces per call. Profile construction is outside those samples; face/slice counts must agree
between repeats. `AeroProfileLabTests` checks those counts on a cube with 54 exterior faces.
Engine integration remains open.

The [initial centre-cost run](../tests/benchmarks/aerodynamics/centre-cost-initial-2026-09-27.txt)
from `1b4a704` is retained with an instrument limitation: construction and evaluation were warmed,
but centre preparation was first invoked inside a timed sample. The first 64-block/26-direction
case has a 0.692 ms maximum against 0.0021/0.0022 ms fastest/median (32,852 % spread, 13 repeats).
It cannot isolate warmed geometry work from first-invocation cost. The instrument now exercises
centre preparation before collecting samples; this corrects the setup, not an acceptance threshold.

The [warmed centre-cost run](../tests/benchmarks/aerodynamics/centre-cost-warmed-2026-09-27.txt)
records source `ce1c8fc`, host `fedora`, .NET 9.0.19, tiering disabled and core MVID. On the
4,096-block/98-direction cube, 1,536 exposed faces take six calls at a budget of 256. The
per-update peak is **0.0061/0.0065 ms fastest/median**, with a **0.0100 ms maximum**, 63.93 %
spread across 40 repeats and the minimum unconfirmed at the cap. The 26-direction case reads
0.0059/0.0064 ms, maximum 0.0077 ms, spread 30.51 %, also capped at 40. All centre slices in
all six cube/resolution cases allocate **zero bytes**. These are synthetic .NET 9 costs with
observed scheduling noise, not engine deadlines or population validation. The game-target build
and 2,553 offline tests pass; the corrected timing setup also has focused instrument checks.

### Engine boundary and lifecycle

`EnableAeroForces` defaults on; `AeroProfileDirections` selects 98 directions by default or 26.
They own new protocol slots 165/166. Off suppresses physics writes on the next tick. Without
explicit API consumers it also clears geometry and suppresses centre/profile preparation. The force path runs on server and client
in the main-thread publish phase, before the thermal-step early return. Aerodynamic exceptions
are logged separately so they do not suppress thermal damage and post-step publication. It reads current world
velocity, angular velocity and atmospheric density. Air density is the engine's relative value
times 1.225 kg/m³. Ambient wind uses the latest environmental wind sample in world coordinates;
relative velocity and orientation are recomputed every physics tick. Wind sampling cadence and
client/server wind history remain native validation concerns; no block temperature enters force.

`AeroRuntime` invalidates on grid identity, topology, direction count and input revision. The
adapter increments that revision on an observed block-mass change or an API face-profile update.
Construction gets at most 4,096 work units and centre preparation 256 exposed faces per grid per
tick. Those are per-grid budgets, not a bound on total fleet time. The runtime anchors a pending centre update within the existing 1 mm damping-cache tolerance,
then publishes the latest centre after preparation. Alternating ±0.01 mm input noise prevented
completion before this policy; its regression now completes the 54-face rig in eight seven-face
slices. Material changes still restart preparation. An unprepared grid submits
zero force; construction or frequent changes can therefore create a force gap. Cold-start and
edit-under-flight latency must be checked in game before closing AR19. Close and disable without external consumers release
the retained state. Current engine mass guards translation; rotational guarding still uses the
profile's cuboid diagonal inertia. This is an approximation, particularly for cargo, off-diagonal
mass distributions and welded bodies; native spin and attachment cases remain unverified.

The adapter uses `CenterOfMassWorld` transformed into grid coordinates, so a rigid body's origin
is not mistaken for the grid origin. It submits force at the engine's centre of mass and submits
the complete restoring/damping moment separately, avoiding a second pressure-centre lever arm.
The installed engine's `APPLY_WORLD_FORCE` path ignores torque. The
`APPLY_WORLD_IMPULSE_AND_WORLD_ANGULAR_IMPULSE` path applies its torque argument times the engine
timestep and simulation-speed factor internally. Accordingly the adapter makes separate calls with force in N and torque in
N·m; it does not multiply that torque by the timestep a second time. The
[installed-game probe](../tools/thermal-api-check/README.md) checks admission and statically pins
these conversions; it rejects direct native inertia access. The [saved engine probe](../tests/benchmarks/aerodynamics/engine-boundary-2026-09-27.txt) carries
assembly and source fingerprints. Native application itself is not executed by that probe.

API face multipliers now live on `BlockInstance`, with `ThermalNode.Drag` forwarding to that
single value. The builder applies them to front, base and wetted areas and their moments, and to
each exposed face's rotational damping. A custom profile therefore changes effective fineness
as well as pressure area; it is an authored override rather than reference-body calibration.
Unregistered faces remain bit-identical to the earlier model. Clearing an override restores the
original force and damping. `GetGridAeroForces` now reports submitted drag; lift remains zero.
`GetGridAeroMoments` and `GetGridAeroStatus` expose moments, readiness, work and guard binding
for native diagnosis. The API major stays 1 because existing keys retain their meaning.

Acceptance checks in `AeroRuntimeTests` cover invalidation, bounds, off/on allocation, partial
centre readiness, rotational covariance, linear density dependence, impulse units and face
registration/clearing. The world-coordinate adapter is also flown against the independent
body-coordinate control at 60/120 Hz: the S5 threshold remains unchanged, and final angle and
recovery time must agree within 0.1°/0.1 s. The `aero-flight` command uses the world adapter for
its moment rigs; its native counterpart, multiplayer prediction, population P1/P2 and the lift portion of the
profile table remain open. These checks establish offline behavior, not native acceptance.

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

## Public profile access

`GetGridAeroProfile` exports the prepared drag geometry as a detached schema-1 dictionary; field
layout, units and interpolation are specified in [api.md](api.md#aerodynamic-profile-snapshot-schema-1).
It does not force synchronous preparation. Explicit `SetGridAeroProfileConsumer` registrations
keep the normal bounded preparation running for another force mod while `EnableAeroForces` is off.
Without external consumers, off still discards geometry and does no profile work. Each consumer
releases its own stable id; grid close drops all registrations. Preparation and copies use the same
per-grid lock so centre publication cannot interleave with an API copy. Native dispatch and
multiplayer consumer lifecycle remain session checks; this does not claim those have run.

### Profile API verification criteria

Before running the snapshot tests: both 26- and 98-direction exports must reconstruct all nine
interpolated summary fields for non-axis winds, and signed damping must reproduce the evaluator
within float-rounding tolerance. Mutating every exported array must leave the live profile and a
second export unchanged. A retained snapshot must survive topology and centre changes unchanged.
A getter during partial preparation must return null and do no work. Repeated registration of one
consumer must not accumulate demand; two consumers must release independently. External demand
with force writes disabled must still finish within the same work budgets, and releasing all demand
must restore zero-work, zero-allocation disabled frames. Native API availability is checked by the
installed whitelist probe; a build alone is insufficient. These checks do not close native flight,
multiplayer or the lift portion of AR19.

[Recorded validation](../tests/benchmarks/aerodynamics/profile-api-2026-09-27.txt): clean game build,
2,571 full offline cases and 47 final focused checks pass. The installed-game whitelist probe
[admits the profile contract](../tests/benchmarks/aerodynamics/profile-api-whitelist-2026-09-27.txt);
its two earlier instrument failures and the framework-subset correction are retained in that record.

## Population input validation

The old census records nominal installed thrust, and its reader did not retain grid world poses.
A directional profile comparison must first retain those inputs. The new flight-input reader keeps
nominal thrust unchanged and adds the engine's effectiveness curve as a separate quantity. Before
measuring corpus hulls, require: synthetic ion/atmospheric/default curves reproduce hand-computed
endpoints, midpoint and clamping; equal influence endpoints follow the engine's multiplier-one
branch; atmosphere-required thrusters use their minimum effectiveness in vacuum. Exhaust-forward
must produce force backward. A rotated subgrid must rotate that force into the blueprint world
frame; common rigid rotation must preserve magnitudes and relative directions. Missing, nonfinite
or degenerate poses must be reported rather than silently replaced by identity. Tests must cover
both Forward/Up and quaternion forms and their XML assignment order. Installed definition checks
must pin a nonzero ion, atmospheric and hydrogen example to independently read SBC values.

The inspection command `aero-inputs <blueprint>` reports full-power rated thrust vectors at relative
air density 1, with atmosphere present. It assumes unlimited power/fuel, functioning blocks and
thrust multiplier one; it does not infer cargo, current enable/override state or resource sufficiency.
It is an input audit. The population runner below connects directional thrust feasibility,
self-lifting population selection, multiple-grid geometry and resumable corpus coverage.
Historical nominal-thrust census results retain their original scope. No drag coefficients or P1/P2
thresholds change with this input work.

[Recorded input validation](../tests/benchmarks/aerodynamics/population-inputs-2026-09-27.txt) pins
the installed engine/SBC sources and two blueprint audits. The full offline suite passes 2,589
cases; 56 final focused checks pass after strengthening the pose tests. The multi-grid example
has all six thrusters on its main grid; rotated subgrid force directions are covered synthetically.

## Directional thrust capacity

The implementation is `tools/corpus/aerothrust.py`, fed by `aero-inputs <blueprint> --json`.
[Tool usage and dependencies](../tools/corpus/README.md#directional-aerodynamic-thrust-inputs)
record the pinned NumPy/SciPy environment and output contract. Input mass is the unloaded model
mass, not a native physics or cargo measurement.

The population instrument must solve for usable force along a heading, not add positive
projections of individual thrusters: a lone angled thruster has a forward projection but cannot
produce pure forward force. For rated vectors F_i and requested unit direction d, solve
`maximize λ`, subject to `Σ throttle_i F_i = λ d`, `0 ≤ throttle_i ≤ 1`, `λ ≥ 0`.
This assumes independent throttling, unlimited power/fuel and torque compensation. It does not
prove a native controller can realize the command, nor does it reserve thrust for hovering while
travelling. Those are separate flight requirements; the registered P1/P2 comparison is a rated
force/drag diagnostic.

Acceptance fixed before measurement: aligned axes, a lone oblique thruster, unequal opposed
oblique pairs and rank-deficient sets must match hand calculations. Permuting, splitting or
uniformly scaling thrusters and rotating the whole fixture must preserve the corresponding result.
Seeded small configurations must match an independent enumeration of feasible vertices, with
nonzero cases counted. Every accepted numerical result must supply bounded throttle settings,
force-balance residual and a separately recomputed support-function upper bound from the solver's
dual normal; a fake solver success with invalid primal or dual data must be rejected. The tolerance
is 1e-7 times the largest thruster rating (minimum scale 1 N), reported in newtons. Results inside
that tolerance of a later acceptance boundary are unresolved, never silently rounded into a pass.

The certificates are numerical checks, not exact-arithmetic proofs. Resolvable active-contribution
singular values must have condition ratio at most 1e7; otherwise the result is explicitly unresolved.
A nearly aligned lone thruster exposed why this guard is necessary: the initial optimizer returned
100 N with a 1e-8 N sideways residual even though its exact pure-forward capacity was zero. The
new regression rejects that result, including when unused orthogonal thrusters make the full input
matrix appear well conditioned. Ratios below the double-precision rank resolution are not resolved
by this instrument. Exported thruster directions retain double precision, matching the reference
headings rather than introducing float-rounding misalignment at the format boundary.

For a normal n with n·d = 1, every feasible throttle vector satisfies
`λ = n·Σ throttle_i F_i ≤ Σ max(0, n·F_i)`. The right side is the recomputed dual upper bound.
Force residual, active conditioning and the difference between primal force and this bound are
checked separately; a small gap alone cannot certify feasibility.

This solver adds no gravity, torque, drag or population-selection assumptions to the optimization.
The P1/P2 runner below states those choices and retains complete corpus coverage and resume
provenance. No P1/P2 threshold or physical drag coefficient changes here.

[Recorded validation](../tests/benchmarks/aerodynamics/directional-thrust-2026-09-27.txt): clean game
build, 2,591 offline C# cases and 209 Python corpus-tool tests pass. All 80 independent nonzero
LP controls agree within 2.14e-13 N. The two blueprint diagnostics also pass their numerical
certificates. Their archived [three-grid inputs](../tests/benchmarks/aerodynamics/directional-thrust-2026-09-27-2232450039-inputs.json)
and [results](../tests/benchmarks/aerodynamics/directional-thrust-2026-09-27-2232450039-result.json),
and [single-grid inputs](../tests/benchmarks/aerodynamics/directional-thrust-2026-09-27-554650209-inputs.json)
and [results](../tests/benchmarks/aerodynamics/directional-thrust-2026-09-27-554650209-result.json)
retain all six headings and throttle settings. These specimens are the earlier input-audit hulls,
not a population selection. The three-grid hull supplies about 2.88 kN in each reference direction;
the atmospheric single-grid hull has no downward rated capacity under this model. Neither finding
scores drag or P1/P2.

### Blueprint definition upgrades found by the population walk

The frozen walk found a blueprint with fourteen `LargeMissileTurret/AutoCannonTurret` blocks.
The installed definition is `LargeGatlingTurret/AutoCannonTurret`. Inspection of the installed
engine's `MyCubeGrid.UpgradeCubeBlock` and `FindDefinitionUpgrade` shows a subtype-based
upgrade when the original type/subtype pair is absent. The offline reader previously selected
its first subtype match but still marked the block ambiguous, so the population gate withheld it.

The correction resolves a unique installed subtype when the saved type is itself represented
in installed definitions. Exact type/subtype matches retain priority. Missing or unknown saved
types and competing subtype matches stay unresolved; a replacement cannot change grid size.
Ambiguous matches no longer build geometry from whichever definition the dictionary happened
to contain. Upgraded blocks are counted separately and exported with population inputs.
The already running frozen corpus generation is unchanged; it cannot mix receipts from the
corrected reader into its older manifest.

[Recorded investigation](../tests/benchmarks/aerodynamics/population-definition-upgrade-2026-09-27.txt)
retains engine and harness hashes, the failing regression before correction and 73 passing
functional/documentation checks afterward. The [corrected specimen export](../tests/benchmarks/aerodynamics/population-definition-upgrade-2026-09-27-inputs.json)
counts all fourteen upgrades across twenty grids. The complete suite must run after the frozen
walk finishes; these focused checks do not establish population acceptance.

## Population runner protocol (registered before the walk)

The candidate population uses the existing corpus convention: every recursively discovered
`bp.sbc`, vanilla/known definitions, at least 25 blocks. Unknown-definition hulls and small hulls
are counted exclusions. Unreadable files, ambiguous definitions, invalid frames, failed optimization
and other unresolved numerical cases remain unresolved; they cannot silently shrink the denominator.
Every file gets a content hash and byte count. Ordering is descending bytes then relative path.
Receipts are atomic per completed blueprint; resume requires identical inputs, executable/library
hashes, tool sources, installed SBC data and numerical-package versions. Interrupted work has no
receipt and is retried. Failed records require an explicit retry option; progress reports bytes as
well as files. A partial or unresolved population gets no unqualified P1/P2 verdict.

Self-lifting means maximum available resultant rated thrust at least unloaded model mass × 9.81.
The six reference headings cannot establish this: two perpendicular thrusters may lift together
even when neither axis alone does. The force set is the Minkowski sum of each throttle segment
`[0,F_i]`; its maximum Euclidean norm lies at a vertex. Build that convex polytope in at most three
dimensions and retain vertex throttle witnesses. Check each hull reduction's containment numerically,
and stop unresolved if the retained set exceeds 200,000 points. The first generation also
stopped when its resolved span had condition number above 10⁷; the correction below preserves
those candidates instead. Cross-check the selected ray through the existing independently checked directional LP. Ties
within the force tolerance use lexicographically smallest normalized main-grid coordinates rounded
to nine decimal places. This is a deterministic benchmark convention, not a drag-favouring choice.

Drag is measured along that strongest-thrust heading, in each subgrid's saved orientation, with
zero spin at 1.225 kg/m³, 98-direction profiles and unchanged fitted coefficients. Subgrid drag sums
match the current adapter's independent-grid model; mutual subgrid shielding is not added. The rig
assumes rigid relative poses, unlimited power/fuel and torque compensation, and does not reserve
simultaneous hover thrust or claim native piloting feasibility. Nominal census results remain a
separate historical scope. This static equilibrium calculation does not apply per-tick impulse
guards.

P1 counts drag at 100 m/s exceeding available thrust; within numerical tolerance is unresolved.
P2 uses the existing interpolated p1 definition on terminal-speed brackets. For monotone drag,
bisect to 0.001 m/s through 128 m/s. A hull still accelerating at 128 m/s is explicitly right-censored,
not assigned a measured ceiling of 128. Negative pressure-only drag area makes this monotonic
bracket unjustified and is unresolved. Thrust tolerances and a float-force allowance of
`2e-6 × max(1 N, force)` widen the speed bracket. At least 20 measured self-lifting hulls are
required for P1 share resolution (one hull is five percentage points). At least 100
self-lifting hulls and complete resolved coverage are required to resolve the p1 statistic. Numerical
uncertainty at self-lifting, P1 or P2 boundaries is retained rather than rounded into a pass.
A P1/P2 failure is a finding; no coefficient or acceptance threshold is fitted to these results.

Before the corpus run, require independent exhaustive throttle-vertex controls for small force sets,
rotation and rank-deficiency controls, an analytic quadratic-drag root, comparison with the actual
profile evaluator on rotated multiple grids, and runner tests for partial coverage, duplicate/missing
receipts, changed provenance, interrupted writes, exclusions and threshold/quantile boundaries.

### Near-degenerate thrust sets: conservative retention and a known-resultant certificate

The first frozen corpus generation exposed failures on nearly parallel subgrid thrust axes.
An intermediate Minkowski sum can be nearly flat even when later generators span three
well-resolved dimensions. Rejecting that intermediate set needlessly prevents a result. The
correction retains **all** its candidate points, without projecting or discarding any, until a
well-resolved hull reduction is possible. The 200,000-point budget remains enforced. The new
auxiliary count is `retained_points`, since the retained set can include non-extreme force points;
historical `vertices` fields keep their original meaning in the first-generation receipts.

For the selected binary-throttle witness, compute its resultant `v` by compensated summation
and define the test heading as `d = v/|v|`. This heading is feasible by construction. The support
bound `Σ max(0, d·F_i)` independently bounds force on that ray. Require its gap from `|v|` and
the reconstructed force residual to fit the existing force tolerance. This is not a certificate
for an arbitrary requested heading. The fixed-heading LP retains its conditioning guard; its
near-rank rejection is recorded as an unresolved cross-check when the known-resultant proof
succeeds. Other solver errors and any disagreement remain fatal.

Before promoting the change, require exhaustive binary-throttle agreement on forty additional
seeded nearly parallel/coplanar fixtures, exact point retention, support-bound and LP fault
injection, the original independent controls, and reproduction of the six diagnosed hulls.
Those hulls informed this correction and are not held-out validation. No thrust or drag
coefficient, force tolerance, acceptance threshold or population exclusion changes.

[Validation record](../tests/benchmarks/aerodynamics/population-envelope-stability-2026-09-27.txt)
and [six complete input/certificate pairs](../tests/benchmarks/aerodynamics/population-envelope-stability-2026-09-27.json)
record source `060c101`: 227 Python checks passed and one unrelated historical-census check
was skipped because its external dataset was absent in the isolated checkout. All forty new
exhaustive controls and all six diagnosed specimens passed; the largest new-control discrepancy
was 9.10×10⁻¹³ N. One specimen retains an unresolved fixed-heading LP cross-check explicitly.
These are instrument checks, not a population acceptance verdict.

### Directional LP coordinate conditioning investigation

The continuing frozen walk rejected workshop hull `573772127` because its independently
recomputed duality gap was 2.3873 N, above the unchanged 0.72 N internal tolerance. Tightening
HiGHS feasibility settings or disabling presolve did not resolve it. Rotating the same problem
into the main-grid, target-aligned QR or force-SVD frame did resolve it in diagnostic trials.
These trials informed the correction and are not independent validation. The independently
constructed resultant/support certificate gives 182,603,998.36925 N with a 2.98×10⁻⁸ N gap.

Before implementation, register a target-aligned orthonormal QR change of coordinates for the
LP only. Transform the returned dual normal back and recompute every primal residual, support
bound and conditioning check in the original force coordinates. Keep all acceptance tolerances
and rejection paths. Require the existing independent LP/global oracles, forty fresh seeded
rotations of analytically solved six-axis force boxes spanning rating scales 10⁻³ through 10⁹,
original-coordinate certificate reconstruction, the diagnosed specimen, and the near-aligned
unbalanced-force rejection. Any failing case remains unresolved; no subset yields a population
verdict.

The implementation passes 229 Python checks, with the same unrelated historical-census
integration check skipped. The new specimen regression fails against the previous implementation
and passes with the coordinate transform. Forty analytic rotated boxes agree to a maximum
relative error of 1.50×10⁻¹⁵. The [archived specimen and certificate](../tests/benchmarks/aerodynamics/population-lp-frame-2026-09-27.json)
are consumed by that regression; the [validation log](../tests/benchmarks/aerodynamics/population-lp-frame-2026-09-27.txt)
records the before/after check. Population acceptance remains unscored.
A [later specimen, 853505259](../tests/benchmarks/aerodynamics/population-lp-frame-followup-2026-09-27.json),
also rejected by the original LP, passes the committed correction without retuning: the
known-resultant capacity is 3,302,402.92323 N, with zero reported support gap; the independent
LP gap is 0.00906 N within its unchanged tolerance. This is another specimen check, not a
population finding.

### Runtime-authored data provenance

A binary supplied through `--binary` can resolve authored definitions from a different checkout
than the Python runner. The runner now fingerprints `authored_data` reported by that binary's
`aero-environment` command, alongside `game_content`; inferring the authored root from Python's
source path was incorrect in that configuration. Missing, relative or nonexistent authored
roots fail before a run starts. Older binaries must be rebuilt rather than silently falling
back to the runner's checkout. The original frozen generation used matching paths and is
unaffected by this correction. Runtime and definition search paths are also re-enumerated
before and after each blueprint: newly added files stop the run just as modified or deleted
files do. Hashing only the original inventory would miss additions.

The regression builds separate temporary runtime and authored-data directories, verifies the
actual data file is fingerprinted, verifies the unrelated checkout is absent, and detects a
mutation of the actual data, plus added authored definitions, game definitions and runtime
assemblies. The wrong-root check fails before the fix and passes after. An actual isolated binary
launched from the original checkout reports the isolated authored root. The complete Python
suite runs 231 checks: 230 pass and the unrelated historical-census check is skipped. The isolated
solution builds with zero warnings/errors and 73 focused C# checks pass. Full-suite validation
remains deferred until the frozen corpus walk finishes, avoiding timing contention.
The [validation record](../tests/benchmarks/aerodynamics/population-runtime-provenance-2026-09-27.txt)
retains the concrete command and environment output.

### Instrument validation and end-to-end pilot

[Recorded validation](../tests/benchmarks/aerodynamics/population-runner-2026-09-27.txt) at
source `abe9c33`: the game build is clean, all 2,603 offline C# cases and 224 Python corpus-tool
checks pass. The global-thrust oracle independently enumerates every binary-throttle combination
on 80 seeded nonzero fixtures; maximum discrepancy is 2.51e-12 N. Analytic quadratic roots,
rotated two-grid legacy comparisons, selection faults, receipt corruption, interrupted replacement,
coverage, uncertainty and quantile boundaries are covered.

The [archived pilot](../tests/benchmarks/aerodynamics/population-runner-2026-09-27-pilot.json)
reuses the two earlier input-audit hulls to exercise the complete pipeline and resume. It is not
population evidence. The 47-block three-grid ion craft has 4.988 kN maximum resultant thrust
against 3,428 kg unloaded mass and is not self-lifting. The atmospheric craft has 1.862 MN
maximum resultant thrust against 56,429 kg; along that heading, drag at 100 m/s is 451.2 kN.
Its ceiling is above the 128 m/s bracket and remains right-censored. Stopping after one receipt
withholds population verdicts; resuming completes the second file without changing the first
receipt's bytes. With just one self-lifting hull, P1 and P2 correctly remain unresolved even when
pilot coverage is complete. The full corpus walk remains outstanding.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Added reproducible primary-symbol digitization of plate CL/CD with separate measurement intervals and extraction bounds; no model fitting or acceptance verdict. |
| 2026-09-27 | Withdrew the generic plate-stall angle and recorded primary bidirectional-incidence evidence; a static lift profile cannot silently claim hysteresis coverage. |
| 2026-09-27 | Corrected authored-data provenance for a runtime loaded from a different checkout; absent runtime metadata now fails explicitly. |
| 2026-09-27 | Registered target-aligned LP coordinates and original-frame certificate checks after a separate numerical-gap rejection. |
| 2026-09-27 | Added conservative retention for nearly flat thrust sets and an independent certificate for the selected resultant heading. Fixed-heading LP guards stay in place; cross-check uncertainty is explicit. |
| 2026-09-27 | Documented the native definition-upgrade path exposed by an unresolved corpus hull and the constrained reader correction. Frozen receipts remain tied to their original reader. |
| 2026-09-27 | Archived all eight primary flat-plate normal-incidence reference rows, including residuals and measurement basis, for independent lift/drag validation. No model fit or acceptance verdict is claimed. |
| 2026-09-27 | Corrected AR8's claim that Helmbold is a universal body-lift law, citing NASA flight comparisons. Clarified AR11's common reference area and zero-lift drag basis before lift is implemented; no coefficients or acceptance thresholds changed. |
| 2026-09-27 | Registered and implemented the resumable population protocol, global resultant-thrust selection, static profile-drag brackets and coverage-aware P1/P2 scorer. Recorded independent controls and a two-specimen pipeline/resume pilot; full corpus scoring remains outstanding. |
| 2026-09-27 | Added a directional rated-thrust optimizer and schema-1 machine input export. Hand solutions and 80 nonzero vertex-enumeration controls validate force balance and independently recomputed dual bounds; complete population scoring remains open. |
| 2026-09-27 | Added retained blueprint world frames and engine-relative-density thrust ratings for the population lab. The aero-inputs command audits rated world-space thrust vectors; nominal census fields are unchanged and no P1/P2 verdict is claimed. |
| 2026-09-27 | Published drag-profile geometry and damping through a detached snapshot API. External consumers can request bounded preparation with force writes disabled. Six offline cases cover geometry reconstruction, damping signs, snapshot ownership and demand lifecycle; native consumer dispatch and lift data remain open. |
| 2026-09-27 | Connected bounded profiles to the engine adapter and documented engine force/angular conversion, centre jitter, API face overrides and remaining native limits. Recorded [adapter evidence](../tests/benchmarks/aerodynamics/engine-adapter-2026-09-27.txt): 2,565 offline cases pass; S5 recovers at 29.0 s, S6 diverges, S7 density scaling holds, with no guards in these flight rigs. |
| 2026-09-26 | Added bounded centre preparation with atomic publication, exact legacy-equivalence checks and zero allocation. Recorded initial and warmed timing runs, including the warmup correction, repeat caps and observed maxima. |
| 2026-09-26 | Corrected the budgeted builder cache for sub-millimetre centre changes before the first damping read. The legacy implementation measured at that first-read centre; the new preparation had incorrectly reused the initial centre. Added a bit-identity regression and corrected the first-use cost scope in place (E10). |
| 2026-09-26 | Recorded source-identified maximum-slice measurements and zero evaluation allocations, with list-growth and centre-change costs still explicit. Full offline suite: 2,539 passing cases. |
| 2026-09-26 | Added resumable profile construction, per-cell ray stepping and stale-result rejection. Registered exact legacy-equivalence checks and maximum-slice timing before measurement. Initial damping is prepared during construction; centre changes still require a surface pass. |
| 2026-09-26 | Recorded both profile-cost runs with source, binary and runtime provenance. Construction exceeds a frame on a 4,096-block cube in both configurations; settled ticks allocate zero, but first and centre-shifted evaluations allocate 64 bytes. This makes budgeted construction and prepared damping prerequisites for AR19 integration. |
| 2026-09-26 | Added the reachable `aero-profile` cost lab to separate profile construction, lazy first evaluation, settled ticks and centre changes before integrating AR19 into the adapter. |
| 2026-09-26 | Hardened the flight instrument against empty, nonfinite and unbounded runs; corrected signed-spin detection and crossing timestamps. Added vacuum, tick-refinement and analytic quadratic-drag controls without changing S5–S8 acceptance criteria. |
| 2026-09-26 | Corrected the flight profile's multi-cell inertia against analytic cuboids: five of seven new checks failed first, then all seven passed. Recorded the uniform-density and diagonal-tensor limits; the existing single-cell flight rigs retain their results. |
| 2026-09-26 | Independently revalidated the inherited round-6 work: full solution build, 32 aerodynamics tests, 2,494 offline cases and the flight lab. Clarified that the profile remains an offline library and recorded the multi-cell inertia check still needed. |
| 2026-09-25 | **Round 6 and the moment rigs recorded.** The surface-read normal holds X1 and X2; X3 failed on first implementation, was revised, and was re-judged on untuned geometry, all within 0.044 of exact. The drag misses round 5's bars again (15 of 22) and holds resolution invariance (X4) — the 7×7 plate reads 1.04 where it read 0.29. S5 to S8 all hold, S5 by one second. The constants shipped are round 6's. |
| 2026-09-25 | **Round 6 registered, against round 5's own stopping rule, and the page says why.** The trigger is an instrument defect found at game scale, not a drag result; the new normal is read from the surface around each face and is judged on exact geometry first. Resolution invariance across radius 4 to 24 is a new requirement, because it is the property round 5 lacked and nothing had asked for. |
| 2026-09-25 | **Step 1b's first flight found two faults and one finding.** The guard was capping the restoring moment; the in-flight damping was missing. And a 7×7 plate reads a drag coefficient of 0.29 against 1.17, because the block normal tilts every rim cell and a game-scale plate is mostly rim: round 5's calibration does not transfer to the scale hulls are built at. |
| 2026-09-25 | **Round 5 recorded: W1 and W2 fail, W3 holds, and the registered fallback is taken.** The in-sample fit ships as `AeroCoefficients.Fitted` with its errors stated beside it: blunt bodies mostly within 15 %, stubby rounded ones up to 72 % too draggy. `AeroCalibrationTests` pins it; `AeroIncidenceTests` pins the geometry's convergence. |
| 2026-09-25 | **Round 4 recorded as failing, and the geometry measured against exact truth for the first time.** The reconstructed normal converges on blunt bodies as the rig gets finer and has a floor on slender ones; four rounds were partly fitted to the radius-8 rigs' rim artefact. **Round 5 registered**: leave-one-out cross-validation over every sourced body, at radius 24, with a stated fallback if it fails. |
| 2026-09-25 | **Round 4 registered before it runs**: a forebody pressure that can go negative, `P₀ + P₂·cos²φ`, from the cones' own shape, held out on two hemispheres and a rough sphere no round has used. The rough-sphere comparator is chosen after round 3's smooth-turbulent miss and says so. |
| 2026-09-25 | **Round 3 recorded: it misses its own calibration and reads the ellipsoids 3.6–4.2× high.** The cause is the forebody law, which could never go negative and so could not price shoulder suction; the cone data are linear in sin²θ, which is the law the next round needs. Whether a stepped hull reaches the drag crisis White's turbulent values assume is recorded as open, not decided. |
| 2026-09-25 | **Round 3 registered before it runs**, on data no round has seen: White's turbulent ellipsoids. Base suction becomes a function of the front's bluffness and of reattachment length rather than of fineness alone; calibration is every body the earlier rounds saw, and the fit must meet its own data within ±12 %. |
| 2026-09-25 | **Round 2 recorded: S3 and S4 pass on the letter, by 0.007 and 0.1 %, and are not counted as evidence.** The fit misses its own calibration cube by 14.8 % and puts no base suction on any body longer than half its width, so a tail cannot matter. The diagnosis is that fineness conflated front-edge separation with attached flow. |
| 2026-09-25 | **Round 2 of step 1 registered before it runs.** Leeward surfaces count as base only where the flow separates (12° to 22°, sourced), and base suction is calibrated on the square plate and cube with long cylinders for the reattached end. The owner's S1 and S2 become calibration and are recorded as tested once and failed; S3 and S4 stay held out, and a failure ends revisions on this data. |
| 2026-09-25 | **Round 1 of step 1 fails, recorded as run.** The profile's geometry is built and checked against hand geometry (`AeroGeometryTests`); the drag model fitted to White's round bodies predicts all four held-out bodies outside their bands. The diagnosis is written beside the numbers: a round section reattaches sooner than a square one, and the model counted a boat-tail's attached surfaces as base. |
| 2026-09-24 | **Step 1's criteria are registered before any of it is built.** The reference is White's Table 7.3; calibration is the cones and the flat-faced cylinders, and the owner's cube and plate criteria are held out with two more (edge-on cube, boat-tail pair), so none can pass by construction. The finned-hull rig moves to step 2, because a fin restores through lift, not drag; step 1 tests the drag-stabilised case, a shuttlecock. `K5`'s population pair is re-registered unchanged, and a failure is recorded as a realism-against-playability conflict for the owner rather than retuned away. |
| 2026-09-24 | **Step 0 is built.** The force application, the lift and pressure plumbing, the top-speed port and its nineteen settings, and the shape factor on the friction row are removed; the retired `ProtoMember` numbers are recorded in `NoSettingReusesANumberThatWasDeliberatelyRetired`. `RtsApi.cs` is vendored unchanged from RelativeTopSpeed `0b4b21a` under `Thermodynamics/RelativeTopSpeed/`, and `RelativeTopSpeedLink` guards every call to it. **One departure from the inventory**: the solver's normal buffer went too, because nothing in a step read it (see the table). **Backlog rows retired into `K25`**: `K10` (the port, now removed); `K6` and `K22` (the shape term — its wake is `AR4`'s base drag); `K23` (lift — its criteria are re-registered under `AR8`); `K24` (the degenerate plate — `AR9`'s wing detector); and `K16` (the centre-of-pressure indicator — its caveat ends when `AR5` ships). What each measured stays in [thermal-model.md](thermal-model.md#change-log). |
| 2026-09-24 | **Owner's decisions recorded** (see *Decisions taken*): cruise curve then drag; no SEDrag block extensions; re-entry heating configurable; forces simulated on server and client; the existing aerodynamics is stripped and rebuilt with switches; the embedded RelativeTopSpeed port is removed in favour of the original mod's read-only API (13 getters, channel 2772681332). The strip-and-rebuild inventory and switch table were added. |
| 2026-09-24 | Page created. The owner asked for aerodynamics in line with SEDrag, then clarified the goal: *as realistic as possible, and learn what we can from SEDrag*. The SEDrag source (571920453) was read in full: `SEDrag.cs`, `GridCalculation.cs`, `DragBase.cs`, `Core.cs`, `LiftDefinition`, `AeroProperties.xml`. Twenty-two parts were evaluated against this mod and against subsonic physics. The drag and terminal-speed tables are derived from the source by hand, not measured in a session. An earlier reading in the same conversation, that SEDrag's drag was about 20× *weaker* than this mod's, was wrong: it missed `SpeedMod`, and above 27 m/s the relation inverts to 163×. |
