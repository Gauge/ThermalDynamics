# Thermal rendering: synthetic optimization audit

## Bounded default renderer, 2026-10-28

The default independent-fleet view now reserves at most 16,384 grid triangles and
2,048 celestial triangles per frame, plus ten triangles for four native-depth
context planes and the far backdrop. HUD submissions and other mods are outside
these limits. Reservations are conservative, so actual submissions can be lower.
Each visible grid gets a share before later grids can consume its capacity; unused
capacity carries forward. Fleet preparation also shares an approximately 512-region
partition target (power-of-two tiers, minimum 16 per grid), replacing 512 per grid.
The existing two-millisecond cooperative preparation deadline remains a soft limit.

A cell is admitted as a complete volume, including entry, exit and near-cap faces.
Shared exits are retained because a neighboring cell may not fit the budget. A
partial volume can incorrectly colour unrelated objects behind it. When detailed
patches do not fit, the renderer tries one patch per whole face and a simple cap
fan, retaining exact corner samples and temporal endpoints. This loses spatial
detail between samples; the HUD reports limited heat detail. If even the complete
coarse volume does not fit, native-depth context still supplies its silhouette.
Both cap paths consume the exact list returned by clipping: an early empty result
can live in the other scratch buffer while the original still holds the viewport
rectangle. A tiny off-axis volume case covers that buffer-identity counterexample.
The cap is a resource bound, not a guarantee of a particular frame time or of full
temperature detail in arbitrarily large scenes.

Four blue-grey ordered depth bands make opaque characters, terrain, asteroids and
grids without temperature detail visible with no CPU mesh extraction, entity scan
or raycast. Their shading means **shape/depth context, temperature unknown**.
Celestial discs remain climate estimates. The character subsystem stores a server
side suit-interior value only while suit damage is enabled; treating this as a
client-visible exterior surface temperature would be incorrect. Transparent effects
without native depth writes are still outside the demonstrated coverage. Separate
objects within the same depth band can merge visually; this is coarse context,
not guaranteed separation of every silhouette. Four full-screen layers have a GPU fill cost; no GPU measurement accompanies this pass.

The installed `VRage.Render11.dll` was reinspected with `ilspycmd`:
`MyBillboardRenderer.PrepareList` preserves PostPP bucket ordering, and
`RenderPostPP` binds `ResolvedDepthStencil.DsvRoDepth`, premultiplied blending and
the default depth state. In `VRage.Game.dll`, `MyRenderComponentBase.Visible` sends
visibility changes through child render objects. Hiding these objects would remove
the depth needed by the compositor. No supported colour-only suppression retaining
complete depth was demonstrated; native render/physics state is unchanged.

The same installed-source audit confirms that `AddTriangleBillboard` forwards
`parentID` unchanged and `MyBillboardRenderer.GatherInternal` performs an actor
lookup unless that ID is `uint.MaxValue`, the sentinel used by `CreateBillboard`
for unattached quads. All five thermal triangle call sites already transform their
vertices to world space, so they now pass the sentinel instead of zero. This
removes exactly one actor lookup per submitted triangle, at most 16,384 grid plus
2,048 celestial lookups in a default frame (18,432 total). Existing quad helpers
already use the sentinel. No geometry, material, depth, ordering or legitimate
attachment ID changed; native frame-time savings remain unmeasured.

Sky drawing also captures camera world/projection matrices, viewport height and
the facing normal once per draw. Vertex projection and triangle submission reuse
that snapshot, removing repeated camera property reads inside both loops without
changing the public projection helper, arithmetic order or tessellation. Region
faces build world-space culling bounds only when the existing local-frustum path
is unavailable; the fallback bounds and game-camera check are unchanged.
Each sky disc also reuses its identical centre and seam vertices in the existing
scratch array. For `R` rings and `S` sectors, normal/projection/climate/UV evaluations
drop from `(R+1)*(S+1)` to `1+R*S`, saving `S+R` evaluations while retaining every
triangle and its order. This halves the one-ring sun's sampling work. The centre
still uses the original ring calculation, including its degenerate-radius numeric
behavior; this change introduces no substitute normal or temperature.

Inspected SHA-256 values:

- `VRage.Render11.dll`: `b4a6502f913b70e53c8f715bfc6dfebef2ae9e87be52a95c84eb7c5fedcdde3b`
- `VRage.Game.dll`: `f0970c2b60335f7690e158a485cd528703717c8c5002f2713556e04f09c508e1`

Uniform current/prior surface lattices now skip repeated face interpolation/error
checks. One tiny fixture (125 lattice samples, 32 rebuilds) measured 0.1073 → 0.0208
ms median; allocated bytes fell from 115,240 to 36,904 per batch by reserving
exactly one patch per uniform face instead of the default capacity of four. Exact patch positions,
temperatures, transition endpoints and ordering matched the frozen reference for
a uniform transition, a cooling-hotspot counterexample and three extreme finite
value cases (current/prior/negative). Values outside 0–100,000 K keep the ordinary
interpolation path. This is about 81% less CPU and 68% fewer allocated bytes for uniform patch preparation, not overall rendering. See
[`results-uniform-surfaces.json`](../tools/thermal-performance/results-uniform-surfaces.json)
for host and method, and the tool README for `--uniform-surfaces`. Independent
fleet faces also use their already-built local frustum before world transforms;
no native CPU saving is claimed for that change.

Tiny regression cases cover atomic cell reservation, paired exits, capped shares,
coarse face samples, preparation tiers and ordered context bands. Native camera
intersections, animation, transparency, all scene categories and GPU cost still
need in-game validation. This pass does not claim complete quantitative thermal
vision for characters or voxels.

### Discovery and occlusion follow-up

Initial field/tree creation previously ran once for every newly visible grid
before the preparation timer started. It now admits at most eight new grid
previews per rendered frame. Other grids keep native-depth context while later
frames acquire their fields. The HUD counts deferred previews. Indexed traversal
of the live-grid list avoids its interface-enumerator allocation; each discovered
view records its frame AABB for occlusion; admission uses the entity ID for stable
distance ties.

Nearby occluder discovery examines at most the nearest 64 visible grids, still
limited to 500 live block reads and 32 accepted solid cubes. Each nearby view
caches candidate positions for 15 active frames, invalidating on camera cell,
topology revision or frame-counter restart. Every reused position is looked up
again and checked for intact, undeformed, completed base-game cube armor before
it can hide anything. Current grid transforms are used each frame. Newly repaired
cubes can take up to 15 active frames to improve culling; this causes extra drawing,
not stale occlusion. Empty candidate caches need no inverse matrix or block reads.

An armor candidate is also rejected if its full native cube bounds touch or cross
the current camera near plane. Testing only the smaller occlusion inset was not
enough: the native front face could be clipped while the inset still blocked all
CPU test rays. This conservative rejection serves both cross-grid and same-grid
culling. Clipped positions remain candidates and are checked again every frame,
so camera movement within a cached cell can immediately restore eligibility.
Candidate caches still retain at most 32 positions. Two tiny cases cover the
clipped-front counterexample and a moved camera with an independent transformed
corner-depth reference; native close-wall appearance still needs in-game checking.

Cross-grid occlusion stops after 2,048 target/cube tests per frame and keeps the
remaining targets visible. A target's camera and eight world-AABB corners are
transformed lazily once per blocker-owner group, instead of repeating the same
transforms for each solid cube. The original slab tests and corner order are
unchanged. Three tiny regressions compare moved/rotated cases with the retained
uncached path, count lazy corner reuse, and check candidate-cache invalidation.
Same-grid subtree traversal shares at most 4,096 node/cube tests across the entire
frame; exhausted work retains frustum-visible leaves in their original order.
Existing public unlimited traversal overloads retain their behavior. A two-cell
case checks a blocker that covers one leaf but not the parent, exhausting the
shared budget at the parent without losing either leaf. The cap bounds occlusion
work, not the remaining tree traversal or drawing.
No native frame-time or allocation percentage is claimed for this follow-up.
Disabled telemetry also avoids preparing the fleet publication/frame and sky
strings and counting uniform face patches; the non-telemetry fleet identity uses
existing constant strings.

### Bounded grid admission and snapshot retention

Each frame now selects the nearest 128 eligible grids using a fixed-capacity max
heap, then sorts only those candidates. Current world bounds, frustum visibility
and camera distances are reread every frame, so moving grids never rely on a
previous frame's visibility or order. Distance ties use entity ID. Farther eligible
grids retain unknown-depth context; the HUD explicitly reports their count as
"grids: shape only" with limited heat detail, separately from previews that are
still refining. Preview construction stays limited to eight per frame and now
starts with the nearest candidates rather than live-list order.

The snapshot cache retains at most 192 views after each draw's admission phase,
with up to eight new previews temporarily coexisting during acquisition. Views
no longer admitted immediately dispose their unfinished preparation iterator.
Their completed snapshots remain for two seconds after the last admitted frame
to smooth camera turns; re-admission checks expiry before updating the timestamp,
including after a suspended view resumes. Oldest inactive snapshots are removed first when capacity is
exceeded. Admitted snapshots are never evicted by retention pruning. Closing a
grid or unloading the world releases its entry, and disabling the view pauses
all work. Three tiny cases compare nearest order with an independent full sort,
including ties and moving candidates, and exercise pause, expiry, eviction,
return, close and clear behavior.

The independent draw no longer aliases a grid's field dictionary through the
legacy `smoothCorners` static. When the last rendered grid left the viewport,
that alias could retain its surface fields after the two-second cache expiry,
until another measured grid rendered or vision stopped. It retained one grid's
prepared fields, not the native grid entity. Independent faces already receive
their field explicitly and caps call `DrawCapTriangle` directly, so removing the
alias preserves legacy BlockLab ownership and the separate `previousSurface`
transition binding. This is a reference-lifetime correction; no native timing
claim or artificial wrapper test accompanies it.

A topology change also disposes an admitted view's suspended refresh iterator
before it can resume a mutated block enumeration. The next job uses the current
revision, while the existing complete snapshot remains available and is marked
as refining until replacement. A single dictionary-backed iterator case checks
cancellation/disposal and successful continuation of the replacement epoch.

The independent view distinguishes missing samples from finite over-range heat.
The existing measured/autorange input ceiling remains 100,000 K. Finite hotter
Core values become a bounded 1,000,000 K **display marker** before field sampling,
above all allowed range highs and excluded from autorange. This prevents accepted
extreme temperatures from disappearing and keeps cap interpolation sums finite;
it never changes simulation temperatures. An initial preview with no usable data
does not invent 293.15 K. A completed empty refresh clears the old thermal regions
and previous fade data, leaving native unknown context and a "no temperature data"
HUD count. Recovery rebuilds geometry and starts the fade from the new samples.
If data first appears between the detail and mean scans, the populated mean
supplies coarse geometry instead of publishing an empty reusable order. A
one-block regression checks that transition through detail selection and ordering.
One block exercises normal, extreme finite, absent/nonfinite and recovered input
through the shared production policy, region scan, field sample, palette and range.
This fixes wholly empty sample sets; partially missing regions still use the
existing spatial interpolation/fallback estimates from remaining valid samples.

**Remaining discovery costs:** the world-grid frustum scan is still O(N); nearest
admission is O(N log 128) in the worst case and rejects farther overflow in
constant work. Sorting is bounded to 128 entries. These are count bounds, not a
hard draw deadline or a byte-accurate memory budget: snapshot size and individual
preparation iterator steps still depend on grid complexity. No native CPU/GPU
percentage is claimed for admission or retention.

### Surface refresh scratch reuse

Minimum-patch planning now reuses its two 625-byte memoization arrays across one
grid refresh job instead of allocating them independently for each qualifying
cell. Costs are cleared before each new planning face; cut decisions are read
only through the current cost table. Published fields retain no scratch reference.
Prior-temperature lattices allocate their six-face holder only if faces are built
or requested, so sampling-only prior lattices avoid that array. The original
`BuildFaces` overload remains available and every refresh iterator yield is
unchanged. One tiny comparison exercises three changing/prior lattices with
fresh and shared scratch, checking exact patch order, positions, temperatures,
diagonals and saved-patch counts. No new timing percentage is claimed.

Stable legend inputs now bypass the per-frame enum/number string-key formatting;
changed inputs still use the original one-decimal key, preserving the existing
label refresh boundary. Multi-patch faces reuse their first local-frustum result
instead of repeating the same bounds/plane test before patch traversal.

### Sky climate conversion cache

Each planet wrapper now lazily retains one sky-only climate conversion. Its key
tracks the definition reference, supplied-field flags, night/day/pole values,
world-settings presence and the corresponding three world values. These are all
mutable inputs consumed by `ClimateModel.Target` in the sky pass. A miss still
uses the existing catalog conversion and clamps; a missing definition remains
unknown. In-place definition or world-setting edits invalidate the cached result.
Camera, sun and latitude sampling remain live. The cache ends with its planet
wrapper and does not alter the simulation's environment caches. One tiny cache
case covers definition replacement, each scalar change, flags, world override
presence and unknown definitions. This removes repeated conversion allocations;
no frame-time percentage is claimed.

Unknown sky discs use the same blue-grey context colour as the legend. Reducing
sky rings or sectors to fit a share now reports limited heat detail even when the
complete reduced disc fits; triangle rejection is not required to flag that limit.

### Character highlight reinspection

Installed renderer inspection also rules out simply adding native character
highlights over the current compositor. `MyRender11.DrawGameScene` calls
`MyHighlight.Run` before `MyBillboardRenderer.RenderPostPP`; the opaque thermal
PostPP bands would overwrite that earlier highlight output. Independently,
`MyHighlightPass.Begin` uses `WriteHighlightStencil`, which disables depth tests
and writes bit 64. Ordinary terrain/grid depth alone cannot reject a hidden
highlighted character. `WriteOverlappingHighlightStencil` is depth-equal, but
applies only to explicitly registered renderable/instance actors and writes bit
128. The inner blend accepts `(stencil & 192) == 64`: registering the character
itself would reject its visible pixels (192), while hidden pixels could still
pass (64). Native character code registers the locally controlled suit and held
items for overlap suppression, not an automatic scene-wide occlusion mask.
Thickness zero removes a highlight; there is no zero-thickness fill shortcut.
Positive thickness adds mesh redraw, depth copy, blur and fullscreen blends.

The inspected MDK 2.1.16 whitelist admits the visual-script provider's exact
`Sandbox.Game` namespace, but not `MyRenderProxy` or `MyHighlightSystem` in
`Sandbox.Game.SessionComponents`. Installed `SetHighlightLocal(name, ...)` stays
local with the default player ID; ordinary `SetHighlight(name, ...)` instead
raises a reliable server-invoked broadcast event. This resolves API locality,
not the rendering blockers. No in-game invocation or state change was made; see
[engine notes](engine-notes.md#character-highlights-do-not-solve-thermal-view-readability).

The inspected `Sandbox.Game.dll` SHA-256 is
`de04e9162bcf87fc07cec377213d99533bcfe1c34c9e1e2231168144c9d67edf`;
the renderer hash is recorded above. There is still no demonstrated cheap native
character-only, depth-correct mask after PostPP. Four depth bands can merge a
character with terrain at similar depth; this fidelity limitation remains open.

### Character proxy feasibility review

An oriented box around a character could use the same complete entry/exit volume
as a grid: twelve face triangles plus a near-plane cap, with a separate categorical
unknown colour. Native depth would supply the actual visible silhouette where it
falls within that box. It would also colour any terrain or hull surface within
the box, because billboard depth has no entity-owner filter. `MyBillboard.ParentID`
provides attachment transforms, not an owner-specific scene-depth mask.

The installed character implementation sets its local AABB from collision width
and standing or crouching height. Its render component expands that box by
(1.5, 2, 1.5) for render culling. Thus the collision box is not a demonstrated
animation envelope; expanding it increases nearby surface contamination, while
a single capsule cannot represent every limb, pose or modded character. The
public character interface exposes neither a ready skinned silhouette nor a
client exterior temperature in Kelvin. `GetOutsideTemperature` is normalized
environmental temperature, so it cannot supply the missing thermal measurement.

Ordering creates an additional correctness issue. Drawing character volumes
after the grid pass resets grid heat behind their projected exit faces. For a
proxy interval [1, 2] and a hot wall at depth 3, both proxy faces pass native depth
and the exit leaves the wall unknown. Drawing proxies first preserves that wall
but lets any overlapping grid volume colour the character as grid heat. A simple
extra character pass therefore cannot provide correct ownership in all scenes.

A limited experiment could draw a bounded nearest-character set before the grid
pass and reject proxies intersecting admitted grid volumes. This would improve
open terrain and space while explicitly excluding characters in or beside ships;
it is not complete character coverage. Native pruning provides reusable-list
sphere queries covering NPC entities, but topmost-only queries can omit parented
characters and the all-entity variant traverses child hierarchies. Neither query
has a work/result cap; a radius and refresh cadence would limit typical cost,
not worst-case discovery time. Live transforms, closure, near clipping and
frustum checks would still be required for cached entries. The sphere overloads
have not been whitelist-compiled in this review. No character proxy or native
render-state modification was enabled.

## Scope and reproduction

### Nearest-block query traversal

The nearest-block search calculated each child's bounding-box distance to order
the children, then calculated it again on entry to that child. The traversal now
passes the computed bound into the recursive call. Pruning, tie order, leaf
distances and returned temperatures are unchanged.

On 16,384 outside-kernel queries over 4,096 blocks, the frozen reference took
4.826 ms and the optimized query took 3.919 ms (18.8% less CPU). Every returned
temperature matched exactly; neither query loop allocates (40 measured bytes are
the stopwatch). The immediately preceding current implementation measured 4.826
ms in a separate run as well. This targets field refresh sampling, not billboard
submission, and is not a forecast of whole-frame improvement. Reproduce using
`--nearest`; results are in `tools/thermal-performance/results-nearest-query.json`.

### General overhead pass

Near-camera subdivision now evaluates the centre only when none of the edge
samples already requires a split. A frozen-reference differential benchmark
checks exact positions, temperatures, triangle count and ordering across 1,024
caps and five fade positions: 54,466 identical triangles, median CPU time
2.986 → 2.441 ms (18.2% reduction). Sampling allocates no objects with the
scratch list preallocated; the measured 40 bytes are the stopwatch. Reproduce
with the performance project's `--caps` option; results are in
`tools/thermal-performance/results-cap-sampling.json`.

Removed the independent renderer's second per-region world-AABB/camera-frustum
test after local-space BSP visibility traversal. The surviving leaves already
passed the tighter grid-local test. Moving/rotating-grid tests compare surviving
leaves against independent eight-corner world bounds over 80 camera positions.
Per-face and per-patch culling remain. No temperature tolerance, LOD, range or
billboard cap changed. Native frame-time savings for this removal are unmeasured.

### Near-camera closing surfaces

Inspection of the installed `VRage.Render11.dll` (`MyBillboardRenderer`) confirms
PostPP bucket 4 retains submission order; triangle UV0/UV1/UV2 are forwarded to
the shader. `Transparent/Billboards.hlsl` uses linear texture sampling and the
PostPP path uses premultiplied blending. Both thermal ramp DDS files contain
opaque alpha. This does not rule out geometry/depth intersection artifacts.

Near-plane caps previously sampled only each fan triangle's three corners. They
now sample edge midpoints and the centroid, subdividing where temperature differs
from linear interpolation by more than 20 K. Samples include the current temporal
blend. Recursion is limited to three levels (64 triangles per original fan triangle),
with a reused scratch list. This is a sampled approximation, not a global error
guarantee or model-surface reconstruction. Flat caps remain one triangle.

A synthetic cooling saddle checks dense barycentric samples, positive winding,
total area conservation and collapse to a single triangle when uniform. The live
`cap-triangles` counter records submitted cap geometry so its contribution can be
measured. Native appearance and additional per-frame cost remain unverified.

### Close-up leaf-gradient refinement

The close-up screenshot shows diagonal wedges and cloudy transitions. Inspection
found that the minimum lattice cell stopped subdivision even when its bilinear
saddle exceeded the surface error tolerance. The native shader interpolates UVs
per triangle; the palette DDS files have fully opaque alpha. These checks identify
a real approximation issue, but do not establish it as the sole screenshot cause.

At the highest partition tier (512), single-cell patches now subdivide according
to `ceil(sqrt(abs(A-B+C-D)/(4*20K)))`, limited to eight subdivisions per axis.
Both temporal endpoints determine subdivision. Each new vertex samples the same
scalar lattice, preserving source temperatures. Merged multi-cell crease patches
are excluded. Lower detail tiers, exposure, depth materials and billboard quota
policy are unchanged. There is no guarantee of 20 K error for extreme saddles
that reach the eight-subdivision limit, or of error relative to true block heat.

Synthetic 300/700 K saddle tests reduce maximum error against the lattice from
200 K to 12.5 K using 16 patches instead of one. Heating and cooling fades are
checked across dense sample points. Flat cells remain one patch. This deliberately
spends extra triangles on nearby gradient fidelity; native appearance and added
cost still require verification. Geometry overlap and overall darkness remain
separate possibilities, not claimed resolved by this change.

### Visible-grid preparation priority (September 19 live dump)

Follow-up dump `20260920_011346`: 4,150 captured frames, zero render errors,
1,079 preview frames (26%). Renderer CPU mean 25.644 ms, peak 434.291 ms.
These are different camera trajectories, not a controlled before/after benchmark.
The retained event window shows two visible grids: the nearby 3,026-block ship
refreshes in 1.40–1.67 s and the farther 999-block ship in 3.49–3.73 s.
Most preparation events hit exactly 256 iterator steps at well below the 2 ms
deadline. Raised the defensive step ceiling to `max(4096, visibleGrids * 128)`
so small visible fleets can consume their available time. Deadline, idle exit,
priority policy and rendering fidelity are unchanged. Background preparation
still took 5.485 ms in one reported frame; the cooperative deadline cannot
interrupt an expensive iterator step. This change does not eliminate that
existing limitation. Regression tests cover
remaining-time progress, deadline, idle exit and the finite step ceiling.

The 23:53:25 dump contained 2,379 frames with at least one preview out of
4,045 captured frames. Small distant grids repeatedly completed temperature
refreshes while initial detail elsewhere remained pending. The old equal
round-robin preparation did not prioritize distance despite sorting the draw list.

Preparation now assigns approximately 80% of measured iterator time to the nearest
visible grid awaiting its first detailed snapshot. Once initial snapshots are ready,
the nearest eligible refresh gets that share. The remaining 20% rotates through
visible grids. Priority is recomputed each frame and on foreground completion;
viewport/occlusion filtering runs first. Existing snapshots remain visible and jobs
survive camera turns. Service debt persists across frames so a costly cooperative
step cannot repeatedly consume every frame's allocation and starve background work.

The existing 2 ms soft preparation deadline and uncapped stress-test submission
policy are unchanged. New `independent preparation` events report foreground grid,
distance, measured foreground/background time and steps. This changes convergence
order, not total mesh cost: the dump's mean renderer CPU cost was 38.115 ms and
peak was 613.561 ms, so priority alone cannot promise instantaneous fleet detail.
Synthetic unequal-cost service tests check the 80/20 allocation and background
progress; live latency still requires a world reload and another dump.

These are deterministic synthetic blocks and surface lattices, not captured game
ships. They exercise production core code against a frozen pre-pass reference in
`tools/thermal-performance/BaselineBlockField.cs`. No GPU rendering, billboard
submission or simulation scheduling is measured. Timings are local .NET 9 Release
medians of nine runs after four warmups, with tiered compilation disabled. They are
not predicted frame rates. Allocation figures are bytes allocated per operation,
not retained memory. The timer contributes 40 bytes to each measurement.

Run from the repository root:

```sh
DOTNET_TieredCompilation=0 dotnet run --project tools/thermal-performance/ThermalPerformance.csproj -c Release -p:ThermalBuildRoot=/tmp/thermal-perf-build/
```

Raw measurements: `tools/thermal-performance/results.json`.

For block fixtures, the measured operation constructs the temperature index and
samples every block corner. Dense/hull/sparse/multi-cell use up to 2,880 blocks;
dense-large has 23,040 blocks and 184,320 queries. Shared corners intentionally
exercise the reuse encountered at adjacent thermal cells. Actual BSP cell query
locality differs, so savings must still be checked in live dumps.

## Results

| Fixture | Before ms | After ms | Reduction | Allocated MB before / after |
| --- | ---: | ---: | ---: | ---: |
| dense | 9.999 | 1.681 | 83.2% | 2.38 / 1.76 |
| hull | 1.600 | 0.585 | 63.4% | 1.05 / 0.96 |
| sparse | 1.996 | 1.868 | 6.4% | 3.19 / 1.79 |
| multi-cell | 11.632 | 1.913 | 83.6% | 2.61 / 1.87 |
| dense-large | 90.246 | 16.451 | 81.8% | 18.67 / 11.13 |
| surface-patches | 11.000 | 1.972 | 82.1% | 6.54 / 6.54 |
| traversal-10 | 0.176 | 0.035 | 80.2% | 0.00 / 0.00 |
| traversal-40 | 0.252 | 0.080 | 68.5% | 0.00 / 0.00 |
| traversal-100 | 0.235 | 0.059 | 74.7% | 0.00 / 0.00 |

The surface fixture constructs 400 irregular lattices and their patches. Traversal
fixtures use 8,000 regions, with respectively 632, 7,960 and 8,000 visible leaves.
The traversal benchmark compares hierarchical filtering with filtering each ordered
leaf; the native renderer retains its additional world-AABB camera check, so these
are core traversal measurements, not total native draw costs.

## Retained changes

- Precompute each heat kernel's centre and scale once. Preserve division and
  summation order; no reciprocal approximation or temperature quantization.
- Store kernel indices in spatial buckets instead of duplicating full records.
- Use smaller buckets only when block-count/bounds density warrants it. Keep wider
  buckets for sparse shapes; the density estimate affects speed, not heat values.
- Preallocate from the known block count, with bounded initial capacities.
- Cache exact repeated sample positions, with at most 4,096 entries. Refresh the
  bounded cache when full to retain locality through large grids; disable it after
  a low-hit-rate trial. Clear it on source mutation and bypass caller-dependent
  fallback cases. Independent grids and refreshes never share cache entries.
- Read known face lattice vertices directly and average the four face samples for
  cell centres instead of repeatedly reconstructing trilinear coordinates.
- Prune off-screen BSP subtrees; descendants fully inside the local frustum avoid
  additional tree-level plane checks. Surviving leaf order is unchanged.
- Pre-size publication dictionaries. Earlier temperature-only refresh and release
  of expired transition lattices remain in place.

## Validation

The runner checks exact baseline equality for all block-corner results and 12,000
additional queries across small/large block sizes, variable blur radii, dense/sparse
bucket selection and nonuniform bounds. The 400 surface cases had identical patch
counts, positions and temperatures in this run (the harness permits at most 0.001 K
vector error to account for floating-point coordinate reconstruction). This is
strong synthetic evidence, not a proof for all inputs at merge thresholds.

Regression tests cover cache bounds, invalidation, sparse-query shutoff and
caller-dependent fallback values. Moving/rotating grid and camera cases compare
hierarchical traversal with ordered leaf filtering. Existing thermal tests cover
palette behavior, field bounds, transitions, interior occupancy and occlusion.

## Other strategies assessed

| Strategy | Disposition |
| --- | --- |
| 2026-09-19 | Reviewed the new stress dump and added conservative same-grid subtree occlusion with visibility and removal checks. |
| Always halve bucket width | Rejected: sparse build allocations increased roughly threefold in the exploratory run. Density-based selection retained. |
| Unbounded vertex memoization | Rejected: sparse queries became slower and allocated unnecessarily. Bounded adaptive cache retained. |
| Keep only the first 4,096 cached points | Rejected: poor locality on large ships; refreshing the bounded cache reduced the large-fixture total substantially. |
| More aggressive merging / temperature quantization | Not applied: changes the approved gradients and risks the rejected banding. |
| Distant render quotas | Not applied: user explicitly requested uncapped stress testing and every visible grid covered. |
| Reduce refresh frequency | Not applied: reduces thermal responsiveness; this pass removes redundant work instead. |
| Reuse transformed world vertices across frames | Not applied: moving grids and cameras invalidate them; grid-local geometry already persists. |
| Share temperature caches across grids | Rejected: risks mixing independently heated grids. |
| Background game-entity reads | Not applied: requires a verified thread-safe snapshot boundary; game collections are mutable. |
| Pool all refresh objects | Deferred: needs lifecycle/memory profiling to justify complexity around suspended iterators and retained snapshots. Preallocation is measured and retained. |
| Combine wall blockers / portal occlusion | Deferred: must conservatively preserve holes, windows and partial blocks; current single-solid occlusion remains. |
| Fully opaque mesh rendering / GPU temperature lookup | Requires renderer facilities not established for a standalone Workshop mod; synthetic CPU timings cannot validate it. |
| GPU fill-rate, overdraw and engine billboard capacity | Requires native measurement; core benchmark cannot establish GPU savings or eliminate the shared engine cap. |

This completes the measured candidates in this pass, not a claim that every possible
optimization is exhausted. Further work should target native draw/submission costs
and lifetime profiling using the new cache and visibility telemetry, rather than
extrapolating these CPU microbenchmarks into an FPS promise.

## Follow-up: index allocations and refresh starvation

The nearest-source search tree no longer creates sample lists for empty children.
Lists are allocated only when a leaf receives data and released when it becomes
an internal node. Occupied leaves allocate their eight-sample capacity once,
avoiding the intermediate four-sample array. Differential reference checks still
pass. Compared with the preceding optimized run, allocated bytes for the large
fixture fell from 11,129,151 to 9,822,771 (about 11.7%); the dense fixture fell from
1,760,997 to 1,597,913. These are per-operation allocations, not retained memory.
Raw follow-up results are in `tools/thermal-performance/results-memory.json`.

The native preparation deadline previously used elapsed time since the start of
all thermal drawing, including discovery and occlusion. At 2 ms of preceding work,
refresh would not advance. It now measures its existing 2 ms window from preparation
start. This may add up to roughly 2 ms of preparation to a frame previously starved
by discovery; it prioritizes continued thermal updates rather than claiming a total
frame-time reduction. The deadline remains cooperative, not a hard bound on one
iterator step. No billboard quota was added.

A synthetic scheduling regression supplies 3–7 ms of discovery overhead on every
frame for 60 frames and checks that all 32 grids progress fairly while the
preparation window, idle stop and per-grid iteration bounds remain enforced.
Publication telemetry adds `refresh-wall-ms` (including time between yields, not
CPU time). `discovery-ms` now excludes the separately reported occlusion duration.

## Celestial sampling follow-up

Distant sky discs now prepare their coordinate basis once per body and their sphere
intersection once per ring. A reusable azimuth table replaces per-vertex trigonometry.
The small planet list uses an allocation-free insertion sort instead of a captured
comparison delegate. Climate properties are deliberately still read each frame so
world climate setting changes are not hidden by stale cached definitions.

The 32-disc fixture samples 40,352 vertices with unchanged tessellation. One local
release run measured 1.3421 ms for the original sampler versus 0.0978 ms for prepared
rings (92.7% less sampling time). This is CPU geometry work only: triangle submission,
depth interaction and GPU overdraw are unchanged, and it is not an FPS estimate.
[Recorded results](../tools/thermal-performance/results-celestial.json) include the
other existing benchmark fixtures. The reported 40 allocated bytes per measurement
come from the benchmark stopwatch, not vertex sampling.

An independent reference sampler is retained for numerical regression. Tests cover
100 deterministic body sizes and orientations, 13 rings and 96 azimuths, checking
ray agreement within 1e-12 and surface normal agreement within 1e-9. Projection and
limb tests remain in place. No triangle caps or quality reductions were introduced.

## Interior near-plane allocation follow-up

The live independent renderer now clips near-plane caps into two reusable 16-vertex
buffers. The convex starting quad can gain at most one vertex per clipping plane
(ten vertices for six planes), so the buffers do not grow in ordinary clipping.
The array-returning API remains for lab callers. Temperature blending no longer
samples the current field twice at each cap vertex, and the camera inverse is
computed once per frame instead of once per visible grid.

A frozen pre-change cap implementation checks exact vertex order and coordinates
for 4,096 rotated cameras, alternating full, partial and empty intersections.
Measured cap generation fell from 0.4885 to 0.2357 ms for the whole synthetic batch;
allocated bytes fell from 3,668,984 to 40 (the benchmark stopwatch). A warmed-up
unit test independently verifies zero clipping allocations and buffer reuse across
empty/nonempty cases. Existing near-plane membership tests verify the geometry.
See [the recorded run](../tools/thermal-performance/results-near-cap.json).

This removes CPU and GC work without changing triangles, temperatures or LOD.
The batch is a stress fixture, not an estimate of the number of caps in a live
frame. GPU overdraw and the cloudy interior appearance still need native evaluation.

## Visible face traversal follow-up

The live gradient draw loop now indexes the `IList<Patch>` returned by `Face`.
Previously `foreach` used the interface enumerator and boxed the list's enumerator
once per face. Because the enumerator stores the large current patch struct, this
allocated 160 bytes per face in the offline runtime. Indexed traversal removes it.
Neutral exit faces also reuse the world-space tangent vectors and centre already
computed for their frustum bounds, avoiding three repeated vector transforms.

The benchmark traverses the same 400 synthetic fields 16 times (38,400 faces),
checking identical accumulated temperatures and retaining patch order. It measured
2.9393 ms and 6,144,040 allocated bytes before, versus 1.4511 ms and 40 bytes after.
The remaining 40 bytes are the timing harness. This is traversal only, without
game billboard submission; native runtime timings and allocation sizes may differ.
[Recorded results](../tools/thermal-performance/results-face-traversal.json) retain
all existing benchmark fixtures. Geometry, temperatures and billboard count are
unchanged. Build and the 171-test thermal/whitelist/documentation suite pass.

## Temperature lattice sampling follow-up

Surface interpolation now computes the base corner index, row/layer strides and
complementary weights once. Explicit corner accumulation retains the original
multiplication association and summation order. It changes neither smoothing nor
the auto-range; the same sampler serves refresh transitions and near-plane caps.

The frozen reference and optimized sampler agree exactly for 102,400 deterministic
samples across 400 synthetic fields, including clamped exterior samples. One local
run measured 2.5912 ms before and 1.6575 ms after (36% less sampling CPU time).
Both paths allocate no sampling memory; the 40 reported bytes are the stopwatch.
[Recorded results](../tools/thermal-performance/results-lattice-sampling.json) retain
all fixtures. An independent trilinear polynomial regression covers all 64 lattice
shapes (one through four subdivisions per axis), with 200 samples per shape and
boundary clamping. This benchmark excludes engine rendering and is not an FPS claim.

## Deferred patch-coordinate construction

Temperature error checks now run before computing patch corner positions. Candidate
patches that split do not need those positions: only retained leaves create them.
No visibility-based omission was applied, because a face needed after camera motion
must remain available. The temperature checks, recursion order, extrema and patch
limits are unchanged.

The benchmark retains the immediately preceding implementation in
`tools/thermal-performance/BaselineSurfaceBuild.cs` to isolate this change from
older optimizations. Across 400 deterministic fields it verifies every patch's
coordinates, order, current/previous temperatures and the complete field range.
A local run measured 2.2414 ms before and 2.0039 ms after (10.6% less construction
CPU time); allocations were unchanged at 6,537,104 bytes for the batch.
[Recorded results](../tools/thermal-performance/results-patch-build.json) include
the other fixtures. These are synthetic CPU results, not native frame timings.

## Live fleet dump: projected-size spatial detail

`Thermodynamics_Telemetry_20260919_193657.log` from ToastyBugs, manually dumped at
19:47:45 UTC, records 10,075 captured frames and zero render exceptions. One retained
frame reports 128 visible grids, 13,764 regions, 191,343 billboards and 278.834 ms in
draw, versus 0.066 ms discovery and 2.796 ms preparation. Several small-grid refresh
publications took tens of seconds wall time; a 999-block topology rebuild took
385.9 seconds including time between yields. These are observations, not timings
attributable exclusively to any one optimization. The report contains both lighter
and heavier views; its aggregate is not a controlled before/after comparison.

The live path formerly assigned 512 detail regions regardless of apparent size.
It now estimates diameter in pixels from the grid bounding-box diagonal, camera
focal scale and distance to the closest world bounds. This conservative estimate
can overestimate rotated or elongated grids. Demand is `(diameterPixels / 8)^2`,
rounded up to powers of two between 16 and 512. Existing levels are held while
demand lies between 60% and 140% of the current level. Interior views retain 512.
A level change invalidates partition reuse, while the prior complete snapshot
remains visible until atomic publication and the existing 0.3-second temperature
transition. This does not implement native mesh LOD access or guarantee invisible
geometry changes; native appearance must still be checked.

This is spatial preparation LOD, not a billboard submission cap. All visible grids
remain eligible, and the requested uncapped stress-test path remains uncapped.
Temperature fields still sample block heat continuously; no palette quantization
was added. Large nearby fleets can still submit excessive billboards. The change
specifically targets unnecessary distant detail rather than guaranteeing a global
rendering limit. Publication events now include `detail-budget` and `diameter-px`.
Tests cover monotonic approach/retreat, jitter around a threshold and complete
source-block coverage at region budgets 16 through 512. Live savings are unmeasured.

## Shared face normals

The live smooth-patch path now computes facing, normalization and the float normal
once per planar face instead of for each patch. Patches produced by the rectangular
surface builder have positive area and consistent winding; they share this normal.
Near-plane caps retain their separate triangle calculation. Triangle positions, UVs,
submission order and count remain unchanged. This does not reduce the 191,343
billboard workload observed in the large-fleet dump.

The calculation uses transformed face tangents, which already exist for culling,
instead of subtracting translated patch positions. This reduces cancellation at
large world coordinates. A regression checks 200 rigid orientations, both viewing
sides and 16 patches per face at million-metre coordinates against independent
world-triangle normals, plus degenerate-face rejection. No native FPS improvement
or timing percentage is claimed for this change.

## Partial-face viewport culling

The independent renderer now checks individual rectangular gradient patches when
their parent face crosses the camera frustum. Previously a face touching the
viewport submitted every patch on it. Single-patch faces bypass this extra work;
fully contained faces perform one classification and then skip per-patch tests.
Rejected patches skip interpolation, coordinate transforms and both billboard
submissions. Local bounds have 4 mm padding around the patch, including the
existing 1 mm camera-facing offset. The game face-level frustum check remains.

A synthetic wide face with sixteen patches at ten metres retains four patches:
eight submitted triangles rather than thirty-two. Independent clip-coordinate
sample checks cover twenty rotated camera orientations and displaced vertices,
and verify that visible samples are never classified as off-screen. This is a
fixture result, not a predicted fleet-wide reduction. No occluded-but-on-screen
surfaces are removed by this change, and billboard submission remains uncapped.
The dump adds `offscreen-patches` to the independent draw event. Build and all 181
thermal/whitelist/documentation tests pass; live savings remain unmeasured.

## Triangle reduction with selective partition search

The live surface builder now optionally searches all lattice-aligned binary cuts
of a face, choosing the smallest rectangular patch partition that passes the same
vertex and cell-centre checks. The existing 20 K tolerance and unit-cell stopping
rule are unchanged. The search is bounded by the 4x4 face lattice (at most 100
nonempty rectangles), with memoized costs and an early exit at two patches for a
rejected parent. It cannot increase the baseline patch count. This is a minimum
among tested guillotine partitions, not arbitrary triangle meshes.

Unconditional search was rejected: in the 400-field noisy benchmark it reduced
52,794 triangles to 52,704 but increased construction time from 2.37 to 4.36 ms.
The retained version first builds the existing partition, skips flat faces and
faces already using at least 75% of their unit rectangles, and searches only the
remaining structured faces. On that noisy fixture, it retains 52,794 triangles,
unchanged allocations, and comparable timings (2.33 versus 2.31 ms; not a speedup
claim). Two 625-byte state arrays are allocated only when search is attempted and
reused across that field's faces. Structured refreshes can take extra CPU work in
exchange for reduced recurring draw submissions.

Production geometry exports demonstrate a hot strip going from 10 to 6 triangles
and a cooled patch from 18 to 14. A linear gradient remains at 2. Rendering each
export at 400x400 with a shared temperature range gives a maximum temperature
image difference below 3e-13 K for those fixtures. Other accepted partitions may
differ within the existing error checks; these examples do not prove lossless
simplification for every field. The source lattice, extrema and hotspot samples
are retained, and transitions still use the prior field at the new patch corners.

[Comparison image](../tools/thermal-performance/patch-study/comparison.png),
[exported geometry](../tools/thermal-performance/patch-study/examples.json) and
[benchmark results](../tools/thermal-performance/results-selective-patches.json)
are reproducible with the performance executable and `render_patch_comparison.py`.
`prepared-tri-saved` in grid publication events counts all six prepared faces,
not actual visible billboard savings. Live fleet savings remain unmeasured.

## Transition-safe simplification

The live builder now tests candidate merges against both the newly sampled lattice
and the previous lattice used for the temperature fade. Previously, a new uniform
field could collapse a cooling hotspot to one rectangle before the old endpoint
had finished fading. The old hotspot then had no interior vertex to represent it.
A shared partition now preserves both endpoints at the existing vertex/centre
checks; the convex temporal blend preserves those checked-sample error bounds.
The unit-cell stopping rule and 20 K tolerance remain unchanged.

This may temporarily retain more triangles during heating/cooling. Once both
snapshots are uniform, the next completed refresh returns the face to two
triangles. Stable gradients retain the selective partition savings. The previous
lattice must match bounds and subdivisions (the live resampling already does so);
mismatched input is rejected. The build-only reference is released after planning.
A regression follows an off-centre hotspot through five fade fractions and verifies
its temperature, final uniform simplification and mismatched-lattice rejection.
This is a fidelity correction supporting triangle reduction, not a claimed extra
performance gain.

## Alternate patch diagonals

Selective partition search can now choose the BD diagonal when the usual AC
split fails and BD passes the existing lattice-vertex and cell-centre checks.
Both transition endpoints must accept the same diagonal. The selected diagonal
is stored in the patch and honored by native triangle winding and UV submission.
Default/fallback patches retain AC. The 20 K criterion and unit-cell exception
have not been loosened; this is not a global continuous-surface error guarantee.

A synthetic diagonal crease now uses 2 triangles instead of 20. A sharper crease
is rejected, and an incompatible old temperature field prevents a premature merge.
The new image is not lossless relative to the old triangulation: the 400x400 export
comparison differs by up to 29.7 K in the crease strip, although the candidate
passes the existing 20 K checks against the source lattice. Comparing two different
approximations is distinct from comparing either one to the source. The other three
fixtures remain equivalent to floating-point precision, with their previous counts.

[Four-fixture comparison](../tools/thermal-performance/diagonal-study/comparison.png)
and [geometry export](../tools/thermal-performance/diagonal-study/examples.json)
include the diagonal flag and can be reproduced with `render_patch_comparison.py`.
[Benchmark run](../tools/thermal-performance/results-diagonal-patches.json) retains
the noise rejection fixture. These are synthetic surfaces; live fleet savings and
appearance require verification. Regression coverage includes a dense analytic
crease check, sharper-crease rejection and incompatible transition endpoints.

## Uniform quad investigation (not enabled)

Installed `VRage.Game.MyTransparentGeometry` inspection confirms that the oriented
quad helper allocates from the engine pool, calls `CreateBillboard` and fixes UV
size to one. It does not expose arbitrary per-corner gradient UVs. `CreateBillboard`
also converts tint to linear RGB, whereas the triangle overload stores tint directly.
Replacing the texture with a CPU tint without validating these differences would
risk visible seams. Custom billboards expose more control but require allocation
and render-thread lifetime handling that has not been validated here.

Exact uniform temperatures at both fade endpoints are eligible for a potential
constant-colour quad; temperature quantization is not used. The synthetic hot strip
has one eligible patch (6 to 5 potential billboard submissions), and the cooled
patch has three (14 to 11). Linear/diagonal gradient patches are not eligible.
A quad still renders two GPU triangles. This proposal targets shared billboard
buffer pressure, not GPU triangle reduction. No native quad path is enabled.
[Opportunity counts](../tools/thermal-performance/quad-opportunity.json) distinguish
current/potential submissions and unchanged GPU triangles.

The live draw diagnostic now counts `uniform-patches` after viewport rejection,
only on the once-per-30-frame diagnostic sample. It conservatively checks both
stored endpoints even after a fade has finished. This lets a future dump quantify
the opportunity without altering appearance or billboard counts. Tests cover
uniform endpoints, tiny differences and nonfinite inputs.

## Nearby solid-wall rejection within a grid

The `Thermodynamics_Telemetry_20260920_051131.log` stress dump captured 5,208
frames with no render errors or menu suppression. Renderer CPU averaged
37.959 ms (maximum 732.836 ms). A retained draw event submitted 124,439
billboards across 73 grids: 193.121 ms drawing versus 2.001 ms preparation.
Near-camera cap triangles were zero in that event. Submission remains the
main stress-scene cost; the user-requested uncapped mode remains enabled.

Whole-grid rejection already used nearby, revalidated solid armor. The same
blockers now prune fully hidden subtrees within their own grid before thermal
sampling and billboard submission. Only intact, undeformed vanilla full armor
cubes qualify, with inset bounds to preserve edge visibility. Every corner must
be behind a single solid blocker; separate blockers are never combined across
gaps. Blockers are refreshed each frame, so removal and grid movement do not
leave persistent visibility results. No new engine dependencies enter the
simulation model or presentation library.

The synthetic fixture retains 848 of 4,096 regions, matching the independent
transformed-box reference and preserving near-to-far order. Removing the blocker
restores all original visible regions. Hierarchical rejection took 7.525 ms for
100 traversals, compared with 39.135 ms for flat per-region rejection. These are
CPU traversal measurements, not native FPS or billboard-count predictions.
The telemetry counter `own-grid-hidden-subtrees` counts pruned tree nodes,
not hidden regions. The thermal test selection passed 224 tests.

This optimization targets hidden geometry inside or close to a ship. It does
not reduce legitimately visible ships in open space, and conservative rejection
can leave partially hidden regions submitted. Live savings require a world
reload and a new dump from an interior or nearby-wall view.

## Follow-up dump: persistent activation

`Thermodynamics_Telemetry_20260920_053259.log` records 1,800 rendered frames,
120 UI-suppressed frames, and zero render errors. Chat signals were active from
179.27 through 181.86 seconds, including a period when the framework chat signal
outlasted the game GUI signal. This explains a temporary overlay disappearance;
there is no retained automatic-off event in this dump.

Chat/cursor visibility no longer suppresses passive rendering. Hotkey input
remains separately gated during text entry. Camera changes and HUD reconnection
preserve the request, and a failing render frame no longer disables it. Original
first-person/camera eligibility still suspends rendering in an unsupported view.
Consecutive exception logging is deduplicated to avoid per-frame log/notification
spam while retrying.

Renderer CPU averaged 34.318 ms, maximum 190.948 ms. These are different camera
samples from the prior dump and are not an optimization comparison. Sampled
same-grid hidden-subtree counts were all zero: no live benefit of that rejection
is established here. Open-space draw events still submitted 104,196–124,956
billboards and spent approximately 142–154 ms drawing. Uncapped stress-test
submission remains unchanged. The report also flags inconsistent simulation
clocks; simulation totals must not be treated as reliable frame attribution.

## Change log

| Date | Change |
| --- | --- |
| 2026-10-28 | Cached exact sky-climate conversion inputs, reused surface planning scratch and stable legend inputs, and documented native character-highlight/proxy limits. Bounded nearest-grid admission and recent snapshot retention. Bounded preview discovery and occlusion, cached/revalidated nearby candidates, reused exact corner transforms and avoided disabled telemetry formatting. Bounded default fleet/sky submissions, added complete-cell coarse fallback and explicit unknown-depth context, and measured the uniform-lattice fast path with a tiny frozen-reference fixture. |
| 2026-09-19 | Reused nearest-search child distance bounds; exact-output query benchmark measured 18.8% less CPU. |
| 2026-09-19 | Short-circuited redundant cap samples with exact-output benchmark and removed duplicate region visibility checks. |
| 2026-09-19 | Inspected installed billboard ordering/UV path and added bounded adaptive thermal sampling to near-camera caps. |
| 2026-09-19 | Refined high-detail single-cell gradient saddles across both fade endpoints; added heating/cooling approximation checks. |
| 2026-09-19 | Reviewed follow-up priority dump and removed small-fleet preparation underuse while retaining the time deadline. |
| 2026-09-19 | Investigated uniform quad packing; added conservative diagnostic eligibility and synthetic opportunity counts, leaving native rendering unchanged. |
| 2026-09-19 | Added transition-compatible alternate diagonals under existing error checks, with rendered approximation comparison. |
| 2026-09-19 | Made surface simplification preserve both temperature-transition endpoints to avoid premature hotspot flattening. |
| 2026-09-19 | Added selective minimum-patch search with unchanged temperature tolerance, rendered comparisons and prepared-triangle telemetry. |
| 2026-09-19 | Added padded per-patch viewport rejection for partially visible faces, fast paths and telemetry. |
| 2026-09-19 | Shared planar-face normals across patches and tested rigid-motion/winding equivalence at large coordinates. |
| 2026-09-19 | Used live fleet dump to add projected-size partition detail with hysteresis, coverage tests and per-grid LOD telemetry. |
| 2026-09-19 | Deferred corner construction until patch acceptance; verified exact patch output against the preceding builder. |
| 2026-09-19 | Reduced repeated lattice index/weight work with exact-reference benchmarks and independent interpolation checks. |
| 2026-09-19 | Removed boxed visible-face enumeration and reused neutral-face transforms; recorded synthetic allocation and CPU comparison. |
| 2026-09-19 | Reused live near-plane clipping buffers, removed redundant cap sampling and measured exact-output allocation savings. |
| 2026-09-19 | Added prepared celestial geometry sampling, independent equivalence tests and recorded CPU measurements. |
| 2026-09-19 | Added reproducible synthetic thermal CPU and allocation benchmarks, reference-output checks and optimization tradeoffs. |
