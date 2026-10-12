# Aerodynamics integration boundary

Thermodynamics simulates heat. Aerodynamics and RelativeTopSpeed are separate projects and are
outside this repository's maintenance scope. No flight model or top-speed implementation belongs
in a Thermodynamics cleanup.

## Current implementation status

Thermodynamics exports copied grid geometry, revisions and environment samples through the
[Thermal API](api.md#standalone-aerodynamics-integration). It integrates heat returned by a registered
provider. Without one, aerodynamic heating is zero and Thermodynamics applies no aerodynamic
forces. Existing force/profile/lift API entries forward to the provider when available.

Drag face weights remain part of the exported geometry. Wind and climate remain thermal
inputs. Thermal settings, persistence, room air, coolant loops and damage remain owned here.
The read-only RelativeTopSpeed adapter is retained here as an extraction leftover; deciding
whether to remove unused local code is separate from modifying RTS.

## Development and verification

Use [the local validation workflow](development.md#building). The normal solution contains only
projects in this repository. The explicit historical fixture assembly links selected sibling
source files read-only; the ordinary Core assembly has no such dependency. Cross-mod flight checks carry
`scope=integration` and run explicitly with `bash tools/validate.sh --integration`. Neither
validation mode builds, deploys or edits a sibling project.

The [API contract](api.md) owns signatures, units, missing-provider behavior and snapshot lifetime.
The [telemetry guide](telemetry.md#aerodynamic-flight-recording) explains the split between thermal
and flight recording. Live load-order and multiplayer checks remain distinct from offline tests.

## Extraction audit, 2026-09-30

The audit's sibling-repository source edits were reverted at the owner's direction. Its earlier
receipt is marked superseded. Historical findings and measurements are retained in the
[audit record](archive/aerodynamics-history.md#extraction-audit-2026-09-30); they do not authorize
work in other repositories or certify the current external flight implementation.

## Historical requirements and experiments

The former flight-model design, calibration rounds, population experiments and extraction notes
are preserved in [the historical record](archive/aerodynamics-history.md). Backlog `K25` and `AR`
identifiers are historical references, not an active Thermodynamics flight-development plan.
The headings below preserve earlier links; their content is explicitly historical.

## Core completion implementation, registered 2026-09-27

[Historical record](archive/aerodynamics-history.md#core-completion-implementation-registered-2026-09-27).

## The regime, and why the current model is wrong about it

[Historical record](archive/aerodynamics-history.md#the-regime-and-why-the-current-model-is-wrong-about-it).

## What SEDrag's numbers are, so nobody copies them

[Historical record](archive/aerodynamics-history.md#what-sedrags-numbers-are-so-nobody-copies-them).

## Part by part

[Historical record](archive/aerodynamics-history.md#part-by-part).

### AR1 — No speed scaling; compressibility only where the speed limit reaches it

[Historical record](archive/aerodynamics-history.md#ar1--no-speed-scaling-compressibility-only-where-the-speed-limit-reaches-it).

### AR2 — Density in kg/m³, entering linearly

[Historical record](archive/aerodynamics-history.md#ar2--density-in-kgm-entering-linearly).

### AR3 — The force sees the silhouette

[Historical record](archive/aerodynamics-history.md#ar3--the-force-sees-the-silhouette).

### AR4 — Drag as forebody plus base plus skin friction

[Historical record](archive/aerodynamics-history.md#ar4--drag-as-forebody-plus-base-plus-skin-friction).

### AR5 — The force acts at the centre of pressure

[Historical record](archive/aerodynamics-history.md#ar5--the-force-acts-at-the-centre-of-pressure).

### AR6 — Rotation is damped by the air, per surface

[Historical record](archive/aerodynamics-history.md#ar6--rotation-is-damped-by-the-air-per-surface).

### AR7 — No small-grid couple (refused)

[Historical record](archive/aerodynamics-history.md#ar7--no-small-grid-couple-refused).

### AR8 — Body lift: aspect ratio, angle of attack, stall

[Historical record](archive/aerodynamics-history.md#ar8--body-lift-aspect-ratio-angle-of-attack-stall).

#### Plate-figure extraction protocol

[Historical record](archive/aerodynamics-history.md#plate-figure-extraction-protocol).

#### Small-angle compatibility lab, registered before computation

[Historical record](archive/aerodynamics-history.md#small-angle-compatibility-lab-registered-before-computation).

### AR9 — Lifting surfaces found from the hull, authored where they must be

[Historical record](archive/aerodynamics-history.md#ar9--lifting-surfaces-found-from-the-hull-authored-where-they-must-be).

### AR10 — No lift fade with speed (refused)

[Historical record](archive/aerodynamics-history.md#ar10--no-lift-fade-with-speed-refused).

### AR11 — Induced drag

[Historical record](archive/aerodynamics-history.md#ar11--induced-drag).

### AR12 — Ground effect with the right sign

[Historical record](archive/aerodynamics-history.md#ar12--ground-effect-with-the-right-sign).

### AR13 — Wind: ours is better; check two gaps

[Historical record](archive/aerodynamics-history.md#ar13--wind-ours-is-better-check-two-gaps).

### AR14 — Wind turbines turn in our wind

[Historical record](archive/aerodynamics-history.md#ar14--wind-turbines-turn-in-our-wind).

### AR15 — Top speed belongs to RelativeTopSpeed; this mod reads it

[Historical record](archive/aerodynamics-history.md#ar15--top-speed-belongs-to-relativetopspeed-this-mod-reads-it).

### AR16 — Heating bounded by recovery temperature

[Historical record](archive/aerodynamics-history.md#ar16--heating-bounded-by-recovery-temperature).

### AR17 — Engineers have drag

[Historical record](archive/aerodynamics-history.md#ar17--engineers-have-drag).

### AR18 — Water, when Water Mod is present

[Historical record](archive/aerodynamics-history.md#ar18--water-when-water-mod-is-present).

### AR19 — Forces every tick, from a cached profile

[Historical record](archive/aerodynamics-history.md#ar19--forces-every-tick-from-a-cached-profile).

### AR20 — Simulated on server and client

[Historical record](archive/aerodynamics-history.md#ar20--simulated-on-server-and-client).

### AR21 — Indicators

[Historical record](archive/aerodynamics-history.md#ar21--indicators).

### AR22 — A numerical guard, not a physical clamp

[Historical record](archive/aerodynamics-history.md#ar22--a-numerical-guard-not-a-physical-clamp).

## Decisions taken, 2026-09-24

[Historical record](archive/aerodynamics-history.md#decisions-taken-2026-09-24).

## Strip and rebuild

[Historical record](archive/aerodynamics-history.md#strip-and-rebuild).

### What goes, what stays, what is rebuilt

[Historical record](archive/aerodynamics-history.md#what-goes-what-stays-what-is-rebuilt).

### Switches in the rebuild

[Historical record](archive/aerodynamics-history.md#switches-in-the-rebuild).

### Build order

[Historical record](archive/aerodynamics-history.md#build-order).

## Step 1's criteria, registered before anything is measured

[Historical record](archive/aerodynamics-history.md#step-1s-criteria-registered-before-anything-is-measured).

### Round 1: the registered design fails its held-out bodies

[Historical record](archive/aerodynamics-history.md#round-1-the-registered-design-fails-its-held-out-bodies).

### Round 2, registered before it is run

[Historical record](archive/aerodynamics-history.md#round-2-registered-before-it-is-run).

### Round 2: passes on the letter, and the fit says the model is wrong

[Historical record](archive/aerodynamics-history.md#round-2-passes-on-the-letter-and-the-fit-says-the-model-is-wrong).

### Round 3, registered before it is run

[Historical record](archive/aerodynamics-history.md#round-3-registered-before-it-is-run).

### Round 3: fails its own data and its held-out bodies, and the new data says why

[Historical record](archive/aerodynamics-history.md#round-3-fails-its-own-data-and-its-held-out-bodies-and-the-new-data-says-why).

### Round 4, registered before it is run

[Historical record](archive/aerodynamics-history.md#round-4-registered-before-it-is-run).

### Round 4: fails, and the instrument, not the drag law, is most of why

[Historical record](archive/aerodynamics-history.md#round-4-fails-and-the-instrument-not-the-drag-law-is-most-of-why).

### Round 5, registered before it is run: every body is used, and each is predicted without itself

[Historical record](archive/aerodynamics-history.md#round-5-registered-before-it-is-run-every-body-is-used-and-each-is-predicted-without-itself).

### Round 5: the bars are missed, the fallback is taken, and the errors ship with the model

[Historical record](archive/aerodynamics-history.md#round-5-the-bars-are-missed-the-fallback-is-taken-and-the-errors-ship-with-the-model).

### Step 1b, first flight: the calibration does not transfer to the scale players build at

[Historical record](archive/aerodynamics-history.md#step-1b-first-flight-the-calibration-does-not-transfer-to-the-scale-players-build-at).

### Round 6, registered before it is run — and why there is one

[Historical record](archive/aerodynamics-history.md#round-6-registered-before-it-is-run--and-why-there-is-one).

### Round 6, and the moment rigs: what step 1's drag and moments are

[Historical record](archive/aerodynamics-history.md#round-6-and-the-moment-rigs-what-step-1s-drag-and-moments-are).

### Flight-profile mass properties

[Historical record](archive/aerodynamics-history.md#flight-profile-mass-properties).

### Flight-lab validity and integration checks

[Historical record](archive/aerodynamics-history.md#flight-lab-validity-and-integration-checks).

### Profile construction and tick cost

[Historical record](archive/aerodynamics-history.md#profile-construction-and-tick-cost).

### Budgeted profiles

[Historical record](archive/aerodynamics-history.md#budgeted-profiles).

### Budgeted centre updates

[Historical record](archive/aerodynamics-history.md#budgeted-centre-updates).

### Engine boundary and lifecycle

[Historical record](archive/aerodynamics-history.md#engine-boundary-and-lifecycle).

## What this changes elsewhere

[Historical record](archive/aerodynamics-history.md#what-this-changes-elsewhere).

## Public profile access

[Historical record](archive/aerodynamics-history.md#public-profile-access).

### Profile API verification criteria

[Historical record](archive/aerodynamics-history.md#profile-api-verification-criteria).

## Population input validation

[Historical record](archive/aerodynamics-history.md#population-input-validation).

## Directional thrust capacity

[Historical record](archive/aerodynamics-history.md#directional-thrust-capacity).

### Blueprint definition upgrades found by the population walk

[Historical record](archive/aerodynamics-history.md#blueprint-definition-upgrades-found-by-the-population-walk).

### Completed first population generation

[Historical record](archive/aerodynamics-history.md#completed-first-population-generation).

## Population runner protocol (registered before the walk)

[Historical record](archive/aerodynamics-history.md#population-runner-protocol-registered-before-the-walk).

### Near-degenerate thrust sets: conservative retention and a known-resultant certificate

[Historical record](archive/aerodynamics-history.md#near-degenerate-thrust-sets-conservative-retention-and-a-known-resultant-certificate).

### Directional LP coordinate conditioning investigation

[Historical record](archive/aerodynamics-history.md#directional-lp-coordinate-conditioning-investigation).

### Runtime-authored data provenance

[Historical record](archive/aerodynamics-history.md#runtime-authored-data-provenance).

### Instrument validation and end-to-end pilot

[Historical record](archive/aerodynamics-history.md#instrument-validation-and-end-to-end-pilot).

## Change log

| Date | Change |
| --- | --- |
| 2026-10-01 | Move historical aerodynamic compiler inputs out of the ordinary Core assembly. |
| 2026-10-01 | Separate the current integration boundary from archived flight-model history; preserve historical anchors. |
