# Project performance review, 2026-10-28

This review covers the simulation, thermal rendering, ordinary heat cues, mod API boundaries,
world lifecycle, and SE2 portability at `60677d35` plus the changes described here. It records
implemented improvements and remaining architectural limits. Measurements are small offline
CPU examples, not native frame-time, GPU, multiplayer or SE2 compatibility results.

| Looking for | Go to |
| --- | --- |
| Reproducible small benchmarks | [Performance tools](../tools/thermal-performance/README.md) |
| Commands and validation outcome | [Validation receipt](../tests/benchmarks/project-review-2026-10-28.txt) |
| Rendering and appearance limits | [Thermal vision audit](thermal-vision-optimization-audit.md), [heat glow](thermal-glow.md) |
| Installed-engine evidence and port constraints | [Engine notes](engine-notes.md), [SE2 lattice labs](se2-labs.md) |

## Implemented simulation improvements

The solver reuses the node stability rate already calculated while applying the thermal-mass
floor, avoiding a second equivalent node walk when that floor is enabled. The ordinary unfloored
path retains its existing work. This does not enable flooring or change any world defaults.

Coolant exchange reads fill and stagnant-flow factors once per loop, resolves a parcel once per
contact, and uses a bounded wrap for normal pipe indices. The general modulo path remains for
external indices. Well-mixed loops clear and scan scratch storage for their actual parcel count,
which is one, instead of their pipe count. Uninitialized publicly mutable loops are guarded.
Exchange order, heat capacities, clamps and energy accounting are preserved.
Loop replacement reuses lookup storage for carried temperatures, fill and pump state. A single
survivor pass replaces the old nested loop search, and scratch references are cleared after use.
Passing the solver's own loop list remains supported through a retained alias buffer. A two-loop
fixture checks reorder, disappearance, reappearance, spill/reclaim and alias ownership.
The [two-loop allocation checkpoint](../tools/thermal-performance/results-core-loop-rebuild.json)
measures 712 to 64 bytes per replacement, with exact captured state. Median batch time falls
from 0.029005 to 0.014848 ms per 96 replacements; these very small timings are less persuasive
than the allocation count. This checkpoint still allocates two temporary pipe-port lists and
excludes loop discovery, native adapter work and the renderer.
The [direct-port checkpoint](../tools/thermal-performance/results-core-loop-ports.json) removes
those last 64 bytes: the same fixture now allocates zero warm bytes per replacement, with exact
state and a 0.029827 to 0.011823 ms median per 96 calls against the original baseline.
Discovery separately transforms only the authored ports it inspects, eliminating transformed
lists along the ring walk. A four-pipe frozen-builder comparison checks order, pump direction,
duplicates, opening, repair and diagnostics; discovery has no new timing claim.

High-flow advection now sums post-clamp parcel temperatures during their existing application
pass, reducing application/mixing from three parcel walks to two. One four-parcel frozen
arithmetic comparison covers flow direction, fractional movement, minimum temperature and
resized buffers. This reduces work without changing the atomic publication boundary; no
additional timing claim is made for this later change.

The [paired core checkpoint](../tools/thermal-performance/results-core-continuation.json) compares the
Core after source slicing and room paging against the frozen baseline in one process. It uses an AMD Ryzen 9 9950X,
optimized .NET 9.0.20, tiered compilation disabled, nine alternating samples of 96 calls.
The fixtures contain 512 ordinary nodes or 128 coolant nodes with 256 contacts; no corpus is read.

| Workload | Original median ms / 96 calls | Changed median ms / 96 calls | Interpretation |
| --- | ---: | ---: | --- |
| Stability preparation with flooring | 0.207287 | 0.142853 | 31.1% less time for this preparation path |
| Resolved coolant full steps | 1.420466 | 1.194875 | 15.9% less time in this fixture |
| Well-mixed coolant full steps | 0.337635 | 0.312868 | 7.3% less time in this fixture |
| Ordinary unfloored full steps | 24.045077 | 24.097227 | No meaningful change |
| Clamped coolant full steps | 0.357925 | 0.351603 | No material speedup claim |

All compared temperatures, demand and energy tallies match exactly. Warm measured allocations
are zero in both versions. The JSON retains all samples, best values and source fingerprints.
These ratios do not establish the same benefit in SE1's .NET Framework runtime.
The current coolant fixture sets block conductivity to zero to isolate coolant exchange from
the corrected inactive-conduction behavior. The [initial receipt](../tools/thermal-performance/results-core-hot-path.json)
retains the earlier fixture and source fingerprints; its coolant timings are not directly
comparable with this revised fixture. Room initialization is outside the timed stepping batches.

Point-source accumulation now yields between source/block visits rather than doing all sources
after the last budgeted environment block. Source-major summation order and diagnostics stay
bit-identical. Four blocks and three sources demonstrate the change: a one-unit allowance used
to reach a 13-unit environment/source burst; the new source stage respects that allowance.
The tiny checks also cover three substeps, source removal/reintroduction and an abandoned step.
This bounds source accumulation, not every other phase of the solver.
The active step also owns a private source snapshot, reused across steps. Direct Core callers
can reuse or edit their input arrays between slices without changing half of a step's source
values. Only the valid source prefix is copied; ordinary zero-source steps allocate nothing.
Copying that prefix remains an unsliced setup cost. The shipped game adapter already waits
until a step is idle before resampling its array.

Coolant contact accumulation now yields inside each loop, including its clamp conductance and
relaxation reductions. Room contacts, diagnostic clears and individual pumps have resumable
cursors too. Work estimates count contacts and parcel passes, preserving the original operation
order; whole steps use direct range loops. Four blocks cover frozen exchange equations, exact
work accounting, disabled devices, multiple substeps and abandoned reductions. Replacing loops,
rooms or pumps, or changing room pressure, abandons suspended work before invalidating its data.
This is not transactional rollback: coupled temperatures already applied by earlier substeps
retain their existing behavior. Apply/advection and completed-temperature publication are unchanged.
Parcel-watts clearing is also sliced within that coupled stage; room watts reset during room
setup. Conduction diagnostics reset inside the existing environment walk, avoiding another
whole-node sweep. Diagnostic intent is frozen per step, so a UI toggle takes effect on the next
step without changing its budget halfway through. Appended blocks wait for the next step.
Demand queries observe suspended scratch without replacing it or the active environment;
explicit temperature resets and room restores cancel pending work before writing.
Environment row totals and sun/wind publication now use the same active node set. A block
appended between substeps joins on the next prepared step, including a deferred shadow refresh;
it cannot make cached heat gain disappear or extend a sweep beyond prepared storage.
The public sun-sweep drain also avoids cursor overflow when completing a partial sweep.
Exposure sweeps likewise bound remaining work before advancing the cursor: a public caller
can drain a partial pass with `int.MaxValue` or remove its remaining nodes without overflowing
or decreasing work counters. The two adjacent-block cases pass in 30 ms after a clean analyzer build.
Settings revisions cancel suspended work before rescaling block, coolant and room-air heat
capacities. The adapter waits for a fresh environment sample before beginning replacement work;
an additive Core update overload distinguishes missing samples from valid default-valued ones.
Existing two-argument callers retain their behavior. Heat-cue queries read published node state,
including newly appended nodes and external temperature writes, instead of in-flight scratch.

The [contact-slicing CPU checkpoint](../tools/thermal-performance/results-core-contact-slices.json)
repeats the same bounded paired fixture after these changes. All eight states match exactly,
with zero warm allocations. Median flooring preparation is 0.208829 to 0.137994 ms per
96 calls (34% less); resolved coolant stepping is 1.432698 to 1.161540 ms (19% less).
Ordinary unfloored stepping remains unchanged within noise (24.063761 to 24.106052 ms).
This validates that checkpoint's whole-step path; its source hashes do not cover later edits.

Stability and thermal-mass flooring now count only enabled exchange mechanisms. Previously,
disabled block conduction still demanded substeps and could inflate effective heat capacity,
slowing unrelated source heating. Cached conductance flags follow settings toggles and
incremental block edits; enabled coolant and room contacts still contribute independently.
Turning the mass floor off restores real capacity even when no node was otherwise dirty.
Four two/three-block checks cover these transitions and a 12.5 K analytic heating result.

Large sparse room bounds use lazily allocated bit and sealing pages; ordinary hulls keep dense
storage and the same flood traversal. The immutable sealing snapshot and completed-map
publication rules are preserved. Pages recycle across resets instead of retaining every
historical spatial key. For two blocks at `(0,0,0)` and `(1000,1000,1000)`, the old initial
arrays imply 1,387,412,179 payload bytes before ranks/frontiers. The new one-work-unit fixture
allocates less than 64 KiB. It deliberately never floods that billion-cell extent.
Exact dense/paged room comparisons use one enclosed cell and repeated map reuse.
Topology contacts and room exposure/contact queries now use the existing exact cell dictionary
when a dense occupancy prefilter would be wasteful. Shadow bitsets use the same paged storage,
and the debug normal query probes only local neighbors. Three blocks with a 1000-cell coordinate
extent exercise query/restart allocation below 64 KiB without visiting the empty volume.
Shadow publication still swaps completed buffers; a three-block ray comparison covers dense,
paged and cleared-map reuse. Clearing shadows also releases other-grid references.
Shadow preparation enumerates only a multi-cell block's six outside slabs, retaining the
original cell walk for legacy models with nonpositive dimensions. Unchanged grid identity and
topology reuse the unique air-cell skin across sun/wind direction changes; each pass still
refreshes its direction and other-grid occluders. The pending list already retained its capacity,
so keeping its contents does not add a second hull-sized allocation.
Other-grid shadow invalidation captures topology versions at publication, so stationary edits
and same-ID model replacement invalidate the map. Rotation checks normalize their axes before
comparison, preserving the same threshold across 0.2, 1 and 5 cell-size ratios. Disabled caches
and temporary native query lists release references without requiring another raycast.
Invalidation also parks unfinished shadow passes and clears their copied blocker lists, while
retaining completed results for pending publication and cached skin geometry for the next pass.
Paging defers visited memory as the flood advances; it does not remove eventual whole-volume
traversal, high-water pool memory, or unsliced structural snapshot/rank work.

## Thermal vision

The default independent-grid renderer replaces its unlimited submission path with a 16,384
triangle grid budget and a separate 2,048 triangle sky budget. Each remaining grid receives a
share, unused shares carry forward, and fleet preparation reduces each grid's target region
count as the visible fleet grows. This bounds submission, not every source of CPU or GPU cost.

Discovery creates at most eight new preview snapshots per frame. Occlusion considers at most
64 candidate grids and performs at most 2,048 cross-grid tests; exhaustion leaves geometry
visible. Nearby solid-cell candidates cache for 15 active frames but every accepted block is
rechecked for removal, construction, damage and deformation. Target corners transform lazily
once per blocker owner instead of once per armor cube. Disabled telemetry skips formatting.
An occluder's full native cube must also lie beyond the current camera near plane; testing
only its inset could hide thermal data behind a native face that the camera had clipped away.
The full grid frustum scan still scales with the scene. Thermal detail admits the nearest 128
eligible grids using a bounded heap and sorts only that admitted set. Overflow remains visible
as unknown-temperature depth context and is labeled separately from preparation in progress.
The snapshot cache retains at most 192 views after trimming (up to eight acquisitions during
the frame); inactive work is stopped immediately, with recent snapshots kept for two seconds.
Small independent-sort and moving-grid/cache cases verify admission order and retirement.
Re-admission checks the same two-second expiry before refreshing a snapshot's last-use time.
Suspended preparation is disposed as soon as its topology revision changes, avoiding stale
dictionary enumerators and unnecessary completion of obsolete fields. Stale published topology
is marked as refining until its replacement is ready.
Same-grid subtree occlusion has a separate shared limit of 4,096 tests per frame. Once that
allowance is spent, traversal keeps frustum-visible regions; it does not drop uncertain geometry.

Budget admission covers a complete thermal volume: entry faces, exit faces and near-camera
closing triangles. Shared exits remain because a neighboring region might not be drawn.
If detailed patches do not fit, the renderer tries a complete coarse volume using sampled corner
temperatures. It drops that thermal volume only if the coarse representation also cannot fit.
This avoids the incorrect alternative of leaving a hot entry without its exit and colouring
unrelated surfaces behind it. Coarse interpolation can conceal a small hotspot; the HUD reports
limited detail.
Coarse near-camera fallback uses the polygon actually returned by clipping, including an empty
alternate scratch buffer. Reusing the caller's older viewport rectangle could otherwise draw a
false hot cap when the true near-plane intersection was entirely outside the viewport.

Uniform current and previous temperature lattices bypass redundant face refinement. Local
frustum rejection avoids unnecessary world-space transforms and repeated camera calls.
The tiny frozen-reference surface benchmark uses a 125-sample lattice and checks generated
patches and transition values before timing. Capacity-one flat-face lists also avoid surplus
patch storage. The final run measures 0.1073 to 0.0208 ms per 32 rebuilds (80.6% less CPU for
that uniform case), with allocations falling from 115,240 to 36,904 bytes (68% less); see the
[surface receipt](../tools/thermal-performance/results-uniform-surfaces.json).
That receipt predates the additional allocation reductions: sequential field builds now reuse
one face-planning workspace per refresh job, and sample-only previous fields allocate no face
holder. Three changing lattices verify exact patch/transition equivalence with fresh scratch.
Unchanged legend inputs skip formatting, and face-frustum checks reuse their existing result.
Planet-disc climate properties cache against the exact definition/override inputs consumed by
sky shading, including in-place edits. Unknown definitions remain unknown; no native planet
state or simulation environment cache is changed.
Sky rings reuse the shared center and closing seam samples, reducing evaluations from
`(R + 1) * (S + 1)` to `1 + R * S` per disc without changing triangle submission.
Display sampling rejects invalid temperatures and gives extreme finite inputs a bounded hot
marker. Empty completed scans clear stale heat; returning samples rebuild the field, including
the case where data returns between the detailed and coarse scans. Unknown geometry remains
native-depth context rather than acquiring an invented temperature.

Four neutral blue-grey depth bands preserve opaque native scene context independently of the
heat-detail budget. This requires no character/voxel mesh copy or per-object physics raycast.
Coverage and temperature knowledge are different:

| Scene content | Treatment |
| --- | --- |
| Simulated grids | Sampled solver temperatures with smooth surfaces, coarse fallback and explicit budget limits |
| Characters | Native animated depth silhouette; exterior temperature is unknown |
| Asteroids and nearby voxel terrain | Native depth silhouette; local temperature is unknown |
| Distant planets | Bounded climate-estimate discs; no per-voxel thermal simulation |
| Grid regions that cannot fit the budget | Neutral native-depth context rather than fabricated temperature |

Characters' existing server-only suit-interior damage state is not an exterior temperature
measurement and is not available to every client. The new context makes unknown geometry
visible; it does **not** complete quantitative character or terrain thermography.
Objects sharing a depth band can merge into the same flat shade, so this is also not a guarantee
that a character remains distinguishable against nearby terrain. Fully readable scene-wide
thermal vision remains an open product requirement.

Installed `VRage.Render11.dll` billboard code and `Content/Shaders/Transparent/Billboards.hlsl`
use the native resolved depth buffer for this compositor. Hiding entity renderers removes the
surface depth it needs; some visibility controls also recurse into children. No native object
or physics component is disabled. Skipping normal shading while retaining a complete depth pass
would require a verified engine rendering hook; none is established by this review. The four
fullscreen bands still cost GPU fill, which remains unmeasured.

All world-space thermal triangles use the engine's unattached parent sentinel. This avoids
one actor lookup per submitted triangle: at most 16,384 grid plus 2,048 sky triangles in the
default view. Sky vertices and normals share one camera snapshot, and the local-frustum path
skips unused world-bound construction. Native batch submission was inspected, but its retained
object lifetime prevents safely recycling transient billboard objects on a fixed frame delay;
the engine-owned triangle pool remains in use.
The independent renderer no longer aliases its last grid's fields into a legacy global cache;
turning toward empty sky can now release those fields when the view expires.

## Ordinary heat cues

Normal view retains warm emissive warning surfaces, bounded surface glow and at most 32
shadowless local lights. It now uses expected-linear set membership instead of a nested scan
to remove stale emissives, caches cue-derived geometry/colour/light regions on demand, shares
face transforms within each grid, and culls with conservative bounds that include the glow halo.
The engine's light setters already track changes, so forced dirty messages are removed.
Grids at least 320 metres beyond their bounding sphere skip the two hot-cue scans used to
position a light: even the maximum 120-metre light radius cannot reach the 200-metre viewing
range. Existing far lights are removed immediately, preserving same-pass slot availability.

Full thermal views suppress the ordinary glow quads and lights. Disabling glow clears emissives
even while audio warnings remain enabled; cooldown restores working, disabled or damaged block
states as appropriate. Grid close and world unload clear cue state and release the warning emitter.
These changes reduce work and stale effects without promising that every third-party material
has compatible emissive slots. Shape-matching of glow on slopes and subparts, sunlight readability
and shadowless light leakage still need visual acceptance. See the heat-glow guide for limits.

## API and lifecycle review

Public delegate signatures, serialization identifiers, Workshop identity, model paths and
vendored libraries are preserved. Existing exception guards, server damage ownership, secure
network sender checks and deferred provider-failure handling remain in place. Detached geometry
snapshots retain caller ownership instead of returning shared mutable arrays as an optimization.
The geometry serializer allocates its cell array at its final size and copies each block directly
into it, eliminating the growing temporary list and final duplicate cell array. An empty grid
and two mixed-size blocks check offsets, cell ordering and detached ownership.

Point-source registration now rejects nonfinite power/range/coordinates; nonfinite updates leave
the source unchanged, and invalid/overflowing sample positions cannot inject NaN heat. A sample
reads an entity's world position once. The heat-source switch also removes adapter sampling work.
Suit settings are converted once per player pass, and player references are cleared even when
enumeration or stepping fails. Suit state and planet lists are cleared across world unload;
planet event handlers are unsubscribed, and nonplanet removals avoid a closure and list scan.

Multiplayer temperature synchronization samples each remote player's position once per pass,
uses the engine entity lookup for incoming grid IDs, and clears scratch lists on failure/unload.
When no remote players remain, it drops their schedules before enumerating live grids.
Suit updates and new-grid initialization read the already-applied shared Core settings.
Previously those reads called `ToCore`, which reapplied settings, advanced the revision,
published synchronization, marked storage for saving and rebuilt a definition fingerprint.
Explicit `Apply`/`ToCore` behavior is preserved; ordinary reads no longer trigger those effects.
Heat-pump input and demand publication build one reusable lookup per pass, replacing a full
device-list search for every registered pump. First-match reference identity and native setter
order are preserved. Each pass observes the public mutable Core list afresh and clears retained
device references afterward; the public linear query remains unchanged.
Native room-pressure sweeps wait while exposure rebuilding leaves the new room map ahead of
its air-node table. Once coherent, unchanged pressures skip the setter's repeated linear room
lookup. A deferred sweep resumes on the existing eight-step schedule; direct Core callers still
own map/table coordination when supplying maps or querying during transitional preparation.
Diagnostic probes and heat-source pulse expiry now run on the six-Hz clock their elapsed-time
argument represents; they previously advanced ten times too fast. Debug expiry tracks only
timed pulses. Mechanical heat bridges honor the conduction switch and discard removed endpoints.

API heat/temperature/pressure/threshold inputs reject nonfinite values. A throwing threshold
callback removes its definition as well as its delegate, avoiding orphan per-grid scans; crossings
for replaced blocks are discarded. Identical normalized drag multipliers no longer invalidate
geometry exports. Remote environment queries skip wind sampling when wind is disabled, while
grid sampling retains velocity-driven convection and skips the unused native wind ceiling query.
A skipped buried/disabled solar ray pass no longer reuses the previous sample's tested flag.
Removed hot blocks invalidate both surface and light-region caches, with batched pruning before
the next read. Empty hull snapshots acknowledge clients without overwriting local temperatures;
malformed synchronization counts are bounded against available bytes before multiplication.
Native serialization waits for initial block/temperature loading before writing storage.
Closing a grid removes its client snapshot ledger entry, allowing a replacement with the same
entity ID to request fresh data. Queued overheat damage checks original block identity before
application, and API block handles must still refer to the live native block at that position.
Interleaved API temperature writes retain the existing additive merge at step publication,
with the minimum-temperature invariant enforced on that merged value.
Zero-emissivity nodes skip the radiation fourth-power calculation, preventing zero times infinity
from producing NaN at extreme finite input temperatures. Substep demand is capped before integer
conversion. These narrow guards do not make arbitrary extreme inputs safe for every active float
equation or add an artificial physical temperature ceiling.
Restoration skips nonfinite records while keeping healthy records from the same save.
Direct Core temperature imports also skip nonfinite records, preserving finite floor and
quantization-tolerance behavior. The wire decoder already produces only finite temperatures.
Current saves require exact minimum-cell anchors; legacy saves retain their original
reference-cell lookup for rotated multi-cell blocks. Direct room restoration also rejects
nonfinite input. Four one-block/one-room cases cover those recovery and compatibility paths.

## Largest remaining limits

| Priority | Finding | Required design work |
| --- | --- | --- |
| High: sparse grid completion/memory | Room and shadow initialization avoid dense sparse-bound allocations; external flood visits still grow with bounding volume, pools retain their high-water mark, and completion/rank work is unsliced. The public explicit dense occupancy API remains dense. | Replace whole-volume flood traversal with exact sparse/coarse structures and stage snapshot/completion work. Cell/rank counters remain `int`; extreme full-domain extent products can exceed `long`. |
| High: very large coupled systems | Source/contact accumulation and parcel reductions yield, but setup, coupled temperature application, coolant mixing/advection and completion still contain atomic walks. | Stage internal work where safe; changing coupled publication requires an explicit state/rollback design rather than exposing partly applied public temperatures. |
| High: SE2 geometry | Solver state scales with nodes/links, but occupancy, surface extraction, contacts and room mapping still expand fine lattice cells. | Replace the geometry/room representation for mixed 25 cm, 50 cm and 2.5 m blocks; adapt math and host APIs. Do not mechanically apply the SE1 cell representation to SE2. |
| Medium: renderer CPU/memory | Admission, retained views, preview creation and occlusion are bounded; the full grid scan and individual preparation advances can still exceed nominal preparation timing. | Profile the smallest reproducing crowded scene and stage expensive individual preparation operations. |
| Medium: consumer-driven copies | Geometry exports now avoid a temporary cell copy, but still copy blocks, occupied cells and arrays; inventory changes can invalidate them repeatedly. | Consumers should cache by revision; evaluate coalescing or a new additive snapshot schema only with a reproducer. |

The installed SE2 game/SDK still confirms .NET 9, DCS, block octrees and a different math
namespace. Those are engine internals, not proof of an admitted mod API. A perfect SE2 port
cannot be certified from SE1 tests. The core/presentation separation is retained, and the
engine-notes update distinguishes reusable physics from the required adapter and geometry work.

## Validation and smallest native follow-up

The NoPackage solution build passes, including the mod whitelist analyzer, with the existing
obsolete triangle-billboard warning. The focused regression run passes 42 cases in 79 ms of
test execution. A broad baseline run was stopped at the user's request and supplies no pass
claim. No corpus, full suite, packaging, deployment or native gameplay result is claimed.
After the final vision corrections, its nine affected cases pass again in 16 ms. Commands and
scope are recorded in the validation receipt.

The continuation validates 41 focused cases: the main run passed 40 and exposed a test shell
with no interior cell; correcting its exclusive bound and rerunning the nine affected room and
inactive-mechanism cases passed (32 ms). The final NoPackage build passes with zero errors and
the same existing warning (4.55 s). The largest coordinate extent executes just one room work
unit; the completed-map comparison contains one interior cell. No heavy validation was added.

The next checkpoint passes 34 tiny contact/cache/API/query cases in 33 ms, then 20 affected
invalidation/paged-query/surface-scratch cases in 54 ms after additional edits. These overlap;
they are not 54 unique tests. The corresponding NoPackage builds pass the game whitelist with
the same existing warning. Native appearance and GPU timing remain untested.

Subsequent small checkpoints pass seven persistence/paused-step cases, eight coolant,
shadow-skin, near-plane and streaming cases, then 17 shadow/publication/numeric/vision and
documentation cases. A separate two-node source-mutation reproducer failed before its fix;
the next build and 14 affected source/conductance/query cases pass, including that reproducer.
These runs overlap and do not constitute a full-suite result. The latest test execution is
348 ms; its build took 173.19 s on the slow host. Later scan reductions have no new timing claim.

The next checkpoint passes nine new behavior cases and the class-summary check in 434 ms.
Its index check exposed one omitted test-class entry; adding it and rerunning only that check
passes in 179 ms. The analyzer build passes after correcting a local variable-name collision.
Fixtures contain at most two blocks/parcels, one displayed cell or one transform pair.
The settings-cancellation/vision-recovery checkpoint then passes six targeted cases in
468 ms; its NoPackage build passes in 18.59 s with the same known warning. It includes the
worker API-boundary and test documentation checks, without a broad suite or native run.
The following adapter/port/shadow batch passes nine targeted cases in 55 ms, with the analyzer
build passing in 12.06 s and the same warning. The only repeated performance workload is the
two-node loop replacement case described above, whose measured warm allocation reaches zero.

The smallest useful native scene is two adjacent blocks at different temperatures, one cold
occluding plate, one character, nearby voxel terrain and a visible distant planet. Check both
palettes, move the camera across a hot volume's near plane, then cool/disable/delete the hot
block and reload once. Use identical scene/camera/settings for any later GPU timing comparison.
This scene verifies visual ownership, coverage and cleanup before any large-world experiment.

## Change log

| Date | Change |
| --- | --- |
| 2026-10-28 | Reuse loop-replacement scratch and record the two-loop allocation checkpoint; cancel settings revisions before rescaling, require fresh replacement samples, preserve published cue state, and fix scale/topology shadow invalidation and missing-temperature recovery. |
| 2026-10-28 | Preserve sliced-step source ownership and active node boundaries; reuse shadow skins, fuse coolant apply/mix preparation, hoist loop conductance factors and remove a redundant rebuild scan. Fix persistence anchors, external-write floors, clipped occlusion and cached vision job lifetimes with tiny targeted regressions. |
| 2026-10-28 | Add resumable coupled contact work, mutation invalidation, sparse topology/shadow/diagnostic queries, bounded view admission/retirement, shared face scratch and API/cache fixes. Preserve checkpoint-specific timing scope. |
| 2026-10-28 | Continue with source slicing, active-mechanism stability and floor restoration, lazy/recycled room storage, bounded preview/occlusion work, exact-sized geometry copies and adapter clock/network cleanup. |
| 2026-10-28 | Review simulation/rendering/lifecycle/port boundaries; implement bounded rendering, hot-path optimizations, lightweight cue caching and cleanup; retain tiny reproducible validation and explicit remaining limits. |
