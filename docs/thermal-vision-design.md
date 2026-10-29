# Thermal vision design

Architecture: thermal vision consumes simulation outputs through the SE1 adapter.
Its offline algorithms live in `Presentation/ThermalVision`, not the simulation
core; see [the architectural boundary](architecture.md#thermal-vision-is-presentation-not-simulation).

The 2026-10-28 performance pass bounds the default fleet renderer and adds four
neutral native-depth context bands for opaque characters, voxels and terrain.
These categories show scene shape with **unknown temperature**; simulated grid
fields supply heat and distant planet discs supply climate estimates. Budget
pressure uses complete coarse cells before falling back to unknown context. The
[bounded-renderer audit](thermal-vision-optimization-audit.md#bounded-default-renderer-2026-10-28)
documents limits, fidelity and the installed renderer evidence. Preview acquisition
is limited to eight new grids per frame. Current bounds select the nearest 128
grids each frame; admission overflow is explicitly marked as unknown shape
context. At most 192 recent snapshots remain cached after admission, and inactive
preparation work is disposed immediately. Returning snapshots expire after two
seconds since their last admission; topology changes cancel suspended refresh
work before it resumes. Cross-grid occlusion and same-grid subtree occlusion have
separate conservative work caps with live candidate validation and visible
fallback after exhaustion. World frustum scanning remains O(N),
outside a hard time deadline; sorting covers at most 128 entries. Native rendering
is retained because the compositor depends on its depth. Full quantitative
coverage and native GPU cost remain unverified.

The independent fleet's display samples retain the 100,000 K measured/autorange
ceiling. Higher finite Core temperatures use a bounded saturated display marker
and do not expand autorange. Absent/nonfinite input is unknown: a wholly empty
sample set removes previous heat regions instead of substituting room temperature
or refreshing stale values. Valid data returning later rebuilds the view; when it
arrives between the detail and mean scans, the populated mean supplies coarse
geometry rather than leaving an empty reusable order. This
presentation policy never clamps simulation state; partially missing fields retain
their existing interpolation estimates from valid samples.

Current test controls: **Ctrl+Shift+T** toggles grey vision. Use
`/thermal vision bind H` to change the chord to Ctrl+Shift+H (saved locally).
`/thermal vision grey`, `/thermal vision colour`, and `/thermal vision off` remain available.


Activation is now persistent within the loaded world: chat and cursor visibility
do not hide the passive overlay, camera switches preserve the selected palette,
and HUD reconnection rebuilds only HUD elements. Ineligible third-person/dead or
missing-camera views suspend rendering without clearing the request; returning
to first-person or a working camera resumes it. Explicit off and world unload
clear the request. Render failures retry and log once per consecutive failure
sequence. Hotkeys remain blocked during text entry. This supersedes the automatic
deactivation and menu-suppression behavior in the historical experiments below.


This is a proposed visual and interaction specification for thermal vision, with a source-based feasibility assessment against the installed Space Engineers assemblies and WeaponCore-StarCore. Opt-in aimed-block and bounded multi-block scene probes are implemented to test the rendering route; full-scene thermal vision is not implemented or validated in game. The accompanying interactive concept uses a schematic scene and illustrative temperatures.

| Looking for | Go to |
| --- | --- |
| Why thermal vision is wanted | [document-of-intent.md](document-of-intent.md#thermal-vision--wanted-method-unknown) |
| Existing rendering and readouts | [architecture.md](architecture.md) |
| Open research item | [backlog.md](backlog.md) B24 |

## Required final coverage

Thermal vision applies to the **entire visible world scene**, in both colour and greyscale. This includes functional blocks, armour, characters, terrain, asteroids, floating objects and the scene background, with appropriate treatment of transparent surfaces and effects. It is not restricted to the crosshair target, the player's ship, or objects already represented by thermal block nodes. The game HUD remains readable as an instrument layer.

The aimed-block probe is acceptable only as a rendering experiment. Its 15 m reach, unsupported deformed armour and unchanged scenery are prototype limitations, not reductions of the finished feature's scope. Probe revision 3 adds undeformed armour through live grid parts; covering all visible surfaces and completing scene presentation remain separate requirements.

Full visual coverage does not imply that every surface already has a simulated temperature. Objects outside the existing simulation need an explicit temperature model, a documented estimate, or a non-quantitative thermal-view presentation. Missing thermal data must not silently become a cold reading or leave an ordinary-colour hole in the finished view.

### Distribution requirement and renderer blocker

The user explicitly requires full-scene rendering **without a client plugin**. A plugin may optionally improve the result, but a reduced-coverage Workshop fallback is not accepted. Both distributions must meet the coverage requirement above. Do not reinterpret permission to use an optional plugin as permission to require one for terrain, characters, greyscale scene presentation, or any other required surface.

The installed assemblies were inspected on 2026-09-18. The current evidence is:

| Inspected surface | Finding | Consequence |
| --- | --- | --- |
| `Sandbox.ModAPI.MyAPIGateway` and `VRage.Game.ModAPI.IMySession` | No custom scene-buffer or shader service identified. | No established Workshop entry point for a temperature-mask/fullscreen resolve pass. |
| `VRage.Game.ModAPI.IMyReflection` | Type-relation queries, not arbitrary renderer invocation. | This is not an alternative renderer access API. |
| `SpaceEngineers.Game.MySpaceGameDefaultIlChecker` | Postprocess settings value types are allowed; no corresponding live wrapper/proxy permission identified in this audit. | A settings type alone does not establish permission to control the live renderer. |
| `MySector` environment initialization | Copies definition settings into the live postprocess wrapper and marks it dirty during initialization. | Mutating a definition is not a demonstrated per-client, live thermal toggle. |
| `MyVisualScriptLogicProvider` screen effects | Screen colour APIs apply a flat fade; highlight APIs supply an entity highlight colour. | Neither establishes full-scene temperature mapping; see the deeper highlight audit below for its interior blend. |
| `IMyWeatherEffects`, also checked against the official API documentation | Overrides control fog, sun intensity and particles; no scene-colour transform or temperature mask is exposed. | Visual Overrides API-style weather control is not the missing thermal render pass. |
| Installed highlight pixel shader | Emits `Outline.Color`. | No temperature sampling or palette lookup is supplied by this shader. |
| Current billboard renderer and synthetic lab | Reconstructs selected block surfaces within explicit work limits; other scene categories remain unrendered. | More extraction/culling optimization cannot by itself close the full-scene rendering gap. |

This audit does not prove that every possible Workshop technique is impossible. It does establish that the implemented route cannot satisfy the accepted scope and that no replacement route has been demonstrated. Feature completion is blocked on a supported standalone rendering route, not another user dump. Keep the prototype and offline fixtures available, but do not call the feature complete, install a plugin as a workaround, or request repeated manual reloads to settle this architectural gap.

Public cross-checks: Keen's [camera API](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/VRage.ModAPI.IMyCamera.html) and [weather-effects API](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/VRage.Game.ModAPI.IMyWeatherEffects.html). The installed assemblies remain the authority for this game installation. Physics raycasts are another possible input for a reconstructed image, but collision hits are not rendered pixels: that path has no established surface/material fidelity or real-time cost, and is not a demonstrated solution to the final requirements.

### Ordered native-depth layers: new engine experiment

The next experiment uses **ordered, depth-tested fullscreen billboards after postprocessing**.
It is implemented as `/thermal vision depth grey` or `/thermal vision depth colour`, with the
existing first-person suit/camera gate and `/thermal vision off` cleanup. This is a live
full-viewport **distance diagnostic, not temperature vision**. It uses neither the floating
survey window nor a collision snapshot. It changes no game binary, shader, paint or environment
definition.

Evidence from the installed assemblies and shader files, inspected on 2026-09-18:

* `MyBillboardRenderer.PrepareList` explicitly skips sorting buckets 3 and 4 (`LDR` and `PostPP`),
  preserving submission order within the dynamic billboard stream.
* The 2026-10-28 inspection also confirms that `AddTriangleBillboard` stores its
  `parentID` unchanged, while `GatherInternal` looks up an actor for every ID other
  than `uint.MaxValue`. `CreateBillboard` uses that sentinel for unattached quads.
  All five thermal triangle call sites already supply world-space vertices and now
  use the same sentinel, avoiding one redundant actor lookup per submitted triangle.
  The default fleet/sky budgets bound this saving to at most 18,432 lookups per frame;
  existing backdrop and face quads already use the sentinel. Native timing is unmeasured.
  Sky discs reuse identical centre/seam vertices without changing triangle order,
  and reuse a camera snapshot captured once per draw, including their facing
  normal; projection math and public helper signatures stay unchanged.
* `RenderPostPP` binds `BlendAlphaPremult`, `DefaultDepthState` and the original resolved scene
  depth via `DsvRoDepth`. `MyDepthStencil.OnDeviceInit` creates that view with `ReadOnlyDepth`.
  Read-only behavior comes from the view: the default state itself can have a write-enabled
  mask. See Microsoft's [depth-view flags](https://learn.microsoft.com/en-us/windows/win32/api/d3d11/ne-d3d11-d3d11_dsv_flag).
  This is not the depth-disabled additive-top path; native composition still needs validation.
* `MyRender11.DrawGameScene` invokes it after tone mapping, highlights, FXAA and chromatic
  aberration. The target is its sRGB view. The usual billboard helper converts input sRGB to
  linear RGB; the experiment uses this helper rather than bypassing colour conversion.
* `Transparent/Billboards.hlsl` reads scene depth for soft particles. `Math/Math.hlsli` clips
  particles behind the surface, then fades by `saturate((surfaceDistance-planeDistance)/
  (softFactor*0.3))` in positive camera-distance notation. This is an indirect GPU depth query,
  not a way to read those values or object IDs in C#.
* `MyHudCameraOverlay.DrawFullScreenSprite`, also checked, only draws a supplied texture. A
  camera overlay alone does not provide scene-colour sampling.

The diagnostic submits 48 frustum-sized planes **near to far**, logarithmically spaced from
1.05 times the camera near plane to 5 km (increased from the initial 150 m). A plane behind an opaque surface is rejected; a plane
in front replaces the previous colour. The last visible plane therefore describes the pixel's
depth interval. Reversing the order would let the nearest plane erase depth information.
Colours mean near-bright/far-dark, with a final black plane for distant/background pixels.
Following the user's screenshot, non-background layers use the upper 65% of the palette so
distant silhouettes remain visible. Layer quads also overscan their nominal frustum by 25% on
each half-extent as a mitigation for the visible left-edge gap. Viewport size, offset and
projection coefficients are now recorded on activation. The gap's root cause and the mitigation
remain unverified in game. Increasing reach with the same layer count reduces depth precision;
it does not increase thermal sensing range, because this diagnostic still samples no temperatures.
Material softness is 0.02 (a 6 mm transition), avoiding division by zero. Camera rotation, zoom,
aspect and projection offsets determine geometry anew each draw. Submission is bounded at 96
triangles, with no CPU raycasts or source mesh extraction.

This offers a route to **native-resolution silhouettes and motion**, independent of collision
tessellation and armour triangle counts. That prediction still needs native validation. It does
not imply thermal detail or a working temperature assignment. Depth precision is 48 intervals;
surfaces between the near plane and the first layer remain uncovered. Glass, smoke, flames and
other effects without depth writes cannot be assumed to have their own silhouettes. Distant
geometry merges with the black background. HUD layering, stereo, other mods' later billboards,
disabled billboard settings and the engine's shared billboard limit also need native checks.

GPU fill cost is unresolved: at 2566×1440 the worst-case plane coverage is about 177 million
pixel-layer opportunities per frame before depth rejection. A small triangle count does not
mean a cheap effect. Telemetry records submission CPU time, errors and a distance-only identity;
it does **not** measure GPU time.

#### Temperature-field extension

The next candidate is a temperature field on ordered slices. Each slice draws a neutral
unknown-data base, then coloured regions from a read-only spatial index of simulated thermal
cells. Scene depth chooses the last visible slice. Cell temperatures use the existing palettes
and adaptive window; unmodelled objects need explicit estimates or an unknown presentation.

The unresolved assignment problem is that the last slice lies **in front of** the visible
surface, potentially outside its thermal cell. Simply expanding cell bounds toward the camera
by one layer interval can colour a neighbouring or intervening cold object. Rotated blocks,
touching hot/cold cells and thin foreground occluders are mandatory counterexamples. Local
slice refinement at cell boundaries and conservative ambiguity masks are candidate mitigations,
not demonstrated solutions. Native depth supplies visibility, not a thermal object-ID lookup.
Do not relabel depth colour as heat or treat unknown background plus partial hot overlays as
acceptance of reduced coverage.

#### Discriminating native test

The proposed temperature assignments have now been tested offline using
`thermal-vision-volumes /tmp/thermal-vision-volumes` (intentionally exits 2). The fixture uses
independent analytic opaque geometry for reference depth and surface ownership; candidates get
only depth and a coarse thermal-cell box. It is not a native benchmark. A 600 K hot object is
tested alone, behind a 280 K foreground plate, and as an open frame with a physically separate
cold object in its empty centre. Even perfect depth is assumed, making this an optimistic test.

| Assignment | Isolated hot pixels recovered | Cold foreground pixels incorrectly hot | Cold pixels inside open frame incorrectly hot |
| --- | ---: | ---: | ---: |
| Previous depth slice | 0 / 1600 | 0 / 720 | 0 / 360 |
| Expand field across the depth interval | 1600 / 1600 | 720 / 720 | 360 / 360 |
| Exact cell boundaries, ideal per-pixel ordering | 1600 / 1600 | 0 / 720 | 360 / 360 |

The last method is an upper bound on cell-boundary compositing, not an implementation of native
polygon ordering. Its remaining error is ownership, not depth precision: a thermal cell includes
empty space that another object's surface can occupy. No solid-body intersection is required.
These counterexamples reject these three assignments as complete solutions; they do not prove
every possible Workshop renderer impossible. A further candidate must provide surface ownership
or a conservative, explicit unknown-data policy and demonstrate the accepted coverage. The
existing model-triangle path provides some ownership but still has the previously documented
geometry/coverage limits. Neither failing path is silently promoted into the finished feature.

After reloading, enable `/thermal telemetry on`, then `/thermal vision depth grey`. Move and
rotate through the ship scene; inspect armour edges, foreground occlusion, terrain and a moving
character. Expect a live fullscreen distance view with a readable HUD, not a frozen scope.
Colour mode tests the same compositor with Cividis. An off/on frame-rate comparison and dump
should accompany this experiment: a uniform screen, ordinary-colour holes, detached shapes or
large GPU slowdown rejects this implementation. Third-person or a changed camera must disable
it; `off` must restore the normal view immediately. This opt-in experiment tests a new concrete
engine route; it is not finished thermal vision.

#### First native result

`Thermodynamics_Telemetry_20260919_004513.log` (manual dump ending 00:47:07 UTC) records
627 active depth-diagnostic frames: 441 WhiteHot and 186 Cividis, all submitting 96 triangles,
with zero render errors. Mean adapter CPU time was 0.026 ms for WhiteHot and 0.031 ms for
Cividis; maxima were 3.276 ms and 1.936 ms respectively. The view was a first-person suit,
near layer 0.0525 m, and manual off was recorded at session second 110. There were 1,830
menu-suppressed draws, excluded from the active-frame statistics.

The user reports that it **looks good**. This is positive native visual feedback for the
compositor, not independent confirmation of every scene category, camera gating, thermal
accuracy or GPU performance. No new screenshot or off/on GPU measurement accompanied this
dump. Retain this compositor as the current candidate and next address temperature assignment;
do not repeat the frozen survey development loop. The report's old generic 15 m header was
misleading for depth mode and has been corrected to identify mode-specific limits.

### Native material replacement candidate

Further inspection found a different route to investigate: let the original rendered surfaces
carry thermal materials rather than assigning temperatures from depth or coarse spatial cells.
Unlike volume assignment, this would keep native geometry, animation, deformation and surface
ownership. It is a candidate architecture, not an implemented mode or a proven full-scene route.

The inspected implementation paths are:

| Path | Installed-code evidence | Remaining gate |
| --- | --- | --- |
| Entity material overrides | `MyRenderComponentBase.UpdateRenderTextureChanges` sends an instance's material-name dictionary to `ChangeMaterialTexture`; the renderer handles both `GetRenderable()` and `GetInstance()` pipelines. `MyTextureChange` has colour/metal, normal/gloss, extensions and alpha-mask slots. | Enumerate materials through permitted APIs or a supported asset catalogue; preserve original alpha masks, skins and per-subpart state. The focused override snippet passes the installed analyzer; complete adapter/session validation remains. |
| Surface temperature colour | Standard material shading takes emission from the extensions texture's green channel multiplied by instance emissivity; colour derives from its colour texture, optional HSV paint mask and instance colour multiplier. | Emission is not automatically an unlit palette: lighting, tone mapping, bloom and exposure must be controlled or calibrated. Merely recolouring paint is insufficient. |
| Terrain/asteroid material updates | `VRage.Game.MyVoxelMaterialDefinition.UpdateVoxelMaterial()` submits its `RenderParams` through `MyRenderProxy.UpdateRenderVoxelMaterials`. `FeedOutputTriplanar` uses `ext.y * pixel.emissive`, so voxel shading has an emissive input as well. | Direct texture-field access is rejected by the installed analyzer; the XML/base-builder and render-state transfer pattern below passes. Updates are per material definition, not per visible voxel. Distinct temperatures at locations sharing a material need another mechanism or an explicitly non-quantitative treatment. |
| Armour | Native cube instancing has its own render data and texture indices; an entity override does not prove per-slim-block material control. | Establish a render-only per-cell route without changing saved/networked paint or skin state. This remains a coverage gate. |

The 48 opaque depth planes would hide these native colours if left over them. A material-based
candidate must replace that composition strategy, or use a separately demonstrated mask; simply
enabling both paths is not a solution. Sky, particles, transparent materials and distant LODs
still need explicit coverage. No entity, voxel definition, world paint or game asset was changed
during this audit.

#### Material API permission results

The installed `VRage.Scripting.Analyzers.WhitelistDiagnosticAnalyzer` now runs in an offline
tool against C# 6 snippets. It uses the installed game's game/API whitelist registrations,
with a deliberately limited framework subset to avoid .NET Framework/.NET 9 overload changes.
It is not a full session compiler or a rendering test. The reproducible runner is
`tools/thermal-api-check/run.py`; see the [lab guide](thermal-vision-lab.md#native-material-api-check).

| Snippet | Compiler errors | Whitelist errors |
| --- | ---: | ---: |
| Entity `UpdateRenderTextureChanges` through `MyRenderComponentBase` | 0 | 0 |
| Copy `RenderParams` between definition objects, restore and call `UpdateVoxelMaterial` | 0 | 0 |
| Initialise a scratch voxel definition from an allowed base builder, retaining index/context | 0 | 0 |
| Obtain that base builder through `SerializeFromXML<MyObjectBuilder_Definitions>` and `VoxelMaterials[0]` | 0 | 0 |
| Directly edit `RenderParams.TextureSets[0].ColorMetalY` | 0 | 2 |
| Name `Medieval.ObjectBuilders.Definitions.MyObjectBuilder_Dx11VoxelMaterialDefinition` in script | 0 | 1 |

This establishes a candidate permission-compatible pattern: deserialize a normal voxel
definition document through the public utilities API, retain its builder as
`MyObjectBuilder_DefinitionBase`, initialise a separate definition with the existing index and
context, then transfer only its render settings to the live definition. Snapshot the original
settings in a separate definition for restoration. Do not call `AssignIndex` or `ResetIndexing`,
change the live definition's mining/physics properties, or mutate the snapshot's shared arrays.
The analyzer checks the pattern's syntax and API use; successful runtime deserialization,
initialisation, texture loading and restoration still require native validation.

The renderer's newer model pipeline caches material variants first by dictionary identity and
then by content hash (`MyModel.GetInstance`). Use immutable override dictionaries; editing a
previously submitted dictionary can return its cached old appearance. Bound the number of
palette variants rather than creating a new texture/material combination for every float-valued
temperature. Neither this finding nor the permission result resolves per-cell armour overrides.

#### Restoration requirements

`MyTextureChangeManager.ChangeMaterialTexture(null)` removes pending changes and clears the old
renderable pipeline, but the inspected null branch does not call the newer instance pipeline.
`MyInstance.AddTextureChanges` replaces its stored dictionary and obtains a corresponding model
instance. Consequently, a candidate rollback should explicitly cancel/clear pending overrides,
then submit the complete saved dictionary (a non-null empty dictionary when appropriate), and
validate restoration in **both** pipelines. This is a source-based rollback proposal, not native
proof. Concurrent skin updates must be preserved rather than overwritten with a stale snapshot;
entity closure, new render IDs, mode changes and unloading also need handling.

The `IMyEntity.SetTextureChangesForSubparts(Dictionary<string, MyTextureChange>)` implementation
inspected here constructs a converted dictionary but does not apply it. The `MyStringId` overload
does assign the immediate subparts' render dictionaries. Do not rely on the string-key overload
or assume either call captures/restores every descendant's independent original appearance.

The next bounded feasibility exercise is a render-only material replacement and restoration on
one model plus a separately permission-checked voxel material update. It must establish native
surface ownership, colour stability, preservation of alpha cutouts and complete rollback before
scaling up. This does not relax the standalone/full-scene requirement or justify another user
reload before the candidate is concrete.

### Decal and hologram alternatives

Native decals were also examined because `MyDecals.AddDecal` accepts render-object IDs. In
`MyScreenDecals`, those IDs support attachment, parent transforms and coarse visibility. The
inspected pixel shader (`Shaders/Decals/Decals.hlsl`) reconstructs position from scene depth,
clips to the decal volume and compares normals. It has no per-pixel owner-ID input; the render
pass uses `DepthTestReadOnly` without an owner-specific stencil. Thus a large thermal decal can
still colour a foreign surface inside its volume. The supplied IDs do not solve the ownership
counterexample by themselves.

The native hologram path is also unsuitable as a direct thermal substitute: `Hologram` explicitly
clips pixels against an 8×8 dither texture and adds time-dependent flicker/scan patterns. It
retains material sampling and only adds a small emissive contribution in the standard shader.
It does not supply a solid, temperature-faithful whole-scene image.

### Native highlight interior-blend audit

A deeper inspection of the installed `VRage.Render11.dll` corrects an overly narrow interpretation of the highlight effect: `VRageRender.MyHighlight.BlendHighlight` has both an additive outer-stencil blend and a transparent **inner-stencil** blend. Therefore, it is not accurate to dismiss it as exclusively an outline. A caller could supply an already palette-mapped colour; the absence of temperature lookup in the shader is not itself a blocker.

However, `MyHighlightPass.Begin` selects `WriteHighlightStencil`, whose description in `VRage.Render11.Resources.MyDepthStencilStateManager` explicitly disables depth testing (`IsDepthEnabled = false`, comparison `Always`). There is a separate depth-equal overlapping-model stencil pass, but `MyHighlight.DrawOverlappingObjects` processes an explicit `m_overlappingActors` collection, not a demonstrated automatic all-scene occlusion mask. `MyHighlightSystem.MakeLocalHighlightChange` supplies render-object/subpart IDs and highlight properties; this inspected path does not establish full-scene occluder registration or voxel coverage. The drawing loop handles renderable/instance components, and cannot be assumed to cover every scene category merely because it accepts entity IDs.

This native surface-fill lead is consequently not a demonstrated depth-correct thermal renderer. No engine binaries, depth states, or highlight ownership were changed. A valid route would have to establish supported client-local control, correct occlusion and complete scene coverage before using it in the product.

## Workarounds under investigation

The [thermal survey scope](thermal-vision-survey.md) is now an implemented alternative prepared for testing: a framed, explicitly static collision-image capture, with measured block temperatures and hatched unknown surfaces. It trades live fullscreen navigation for inspection. Its complete controls, work limits, lifecycle and acceptance checks are documented separately; it does not supersede the original requirement without the user's acceptance.

Following the user's request to explore corners that can be cut, the strongest alternative to native scene shading is a **reconstructed thermal sensor image**. Cast view rays, resolve the nearest physical surface, sample thermal block data where available, explicitly estimate or mark unknown temperatures elsewhere, and compose an opaque low-resolution image behind the instrument HUD. This avoids needing to recolour the game's original scene buffer, but also replaces the original visual detail. A successful fullscreen HUD composition path still needs to be established; the offline fixture does not demonstrate it.

Keen's [physics API](https://keensoftwarehouse.github.io/SpaceEngineersModAPI/api/VRage.Game.ModAPI.IMyPhysics.html) provides nearest-hit queries and asynchronous ray queries. Ordinary `CastRay` must not be called from a parallel thread. `CastRayParallel` permitting parallel calls is not evidence of unlimited throughput or a guaranteed callback thread. Future integration must bound outstanding work, discard callbacks from obsolete views/worlds, and avoid unsafe simulation reads in callbacks. `CastLongRay` is documented for long rays and voxel storage; it does not establish that distant collision geometry exactly matches rendered terrain.

| Candidate compromise | Benefit | Cost / acceptance concern |
| --- | --- | --- |
| 64×36 or 96×54 sensor image, enlarged to the viewport | Query cost independent of source mesh triangle count; physics-visible terrain and objects can participate | Pixelated silhouettes; small or thin hot objects may disappear entirely. |
| Lower sensor refresh rate, with depth reprojection researched separately | Fewer new scene queries per second | Stale images, newly exposed holes and moving-object ghosts; never reuse a previous camera's image. |
| Collision geometry in place of exact visual meshes | Avoids model extraction, skinned-mesh and deformed-mesh copying | Armour details, characters, glass and non-colliding effects may differ from the visible scene. |
| Documented environmental/character estimates | Gives scenery a meaningful thermal treatment without another world-scale solver | Estimated and simulated readings must remain distinguishable; no invented precision. |
| Adaptive sampling around depth/temperature changes and known small emitters | Potentially recovers some detail within a query budget | Coarse samples can miss an object entirely, so edge refinement alone cannot guarantee detection. |

The [sensor study](thermal-vision-lab.md#reconstructed-sensor-study) now provides concrete synthetic comparisons and workload arithmetic. This is a possible change in visual fidelity, not a demonstrated complete workaround. No proposed compromise is yet accepted as a replacement for the original full-scene quality requirements, and the current game renderer has not been switched to it.

## Development workflow

Use the [offline thermal vision lab](thermal-vision-lab.md) before another user-run test. Revision 8 is an internal candidate, not a request for another reload. The lab executes the production mesh extraction/projection core, palette, adaptation and state logic, then checks an independent software depth oracle. Full-scene readiness is explicitly incomplete; passing unit tests must not be substituted for that gate.

## Trying the scene probe (revision 8)

Reload the world, enable telemetry, then use `/thermal vision scene colour` or `/thermal vision scene grey`. The HUD must say `SCENE PROBE 8`. Aim-independent drawing considers simulated grids within 100 m, filters block bounding spheres against the camera frustum, and submits the same depth-tested model surfaces as the aimed-block probe. The existing first-person suit / working local camera restriction applies unchanged. `/thermal vision off` disables either probe; `/thermal vision colour` or `grey` returns to the aimed-block probe.

Scene entry enables adaptive `AUTO`: a shared kelvin window follows current candidate temperatures, with 25 K target bins, padding and a minimum 50 K span. Expansion responds quickly (0.4 s time constant), while contraction is gradual (3 s), so hot objects leaving view no longer leave the range permanently stretched. Both palettes use the same window. Adaptation uses elapsed real time, capped at 0.25 s per draw to avoid jumps after menus or stalls. Empty or invalid-only sample sets preserve the previous range. `/thermal vision range auto` resets adaptation, and `/thermal vision range -100 450` locks a manual Celsius window. Switching between scene palettes preserves the range. The candidate set includes occluded blocks; this is **not** a validated visible-pixel exposure algorithm. Missing/invalid temperatures are never assigned a cold colour.

This is still a **partial grid-scene experiment**, not finished full-scene thermal vision. Terrain, characters, asteroids, floating objects, sky, unsupported materials and damaged armour remain outside coverage. Revision 5 reuses discovery results for up to 10 draw calls, refreshing sooner after more than 5 m of camera translation or approximately 15 degrees of rotation. Cached blocks are checked for removal, current range and current frustum membership every draw; newly entering blocks may wait until the next refresh. Sorting is refreshed with discovery. Discovery stops at 256 grids / 4,096 scanned blocks or a soft 1 ms discovery budget. Candidates are sorted nearest first within each pass. Revision 6 gives grid-owned armour a first pass capped at 384 blocks, 8,192 triangles and a soft 1 ms after discovery and progressive extraction; entity models then receive the remaining frame budget. Total drawing attempts at most 512 blocks / 32,768 triangles with a soft 4 ms total budget. Model loops check the time limit every 64 triangles. The higher block cap permits more inexpensive tiles without increasing the total triangle/time budgets. Single operations can exceed a soft time budget. Reaching a limit or missing geometry is marked `PARTIAL`; the dump reports scanned/candidate/attempted/submitted counts. Discovery order and budgets can omit visible blocks and cause coverage to vary between frames. Hidden candidates can also spend drawing budget even though depth testing hides their submitted pixels.

Revision 7 extracts uncached scene models progressively: one retained job advances by at most 8,192 source triangles and a soft 1 ms per draw, up to the 65,536-triangle model cap. The job continues even if its block leaves the candidate list, and publishes only a completed geometry array. Other models wait their turn. Turning the probe off, switching modes or unloading discards unfinished work. One pending model (up to 65,536 source triangles of scratch capacity) is retained in addition to the bounded completed cache. Extraction no longer permanently rejects models solely because they exceed the per-frame build allowance. Revision 8 builds conservative plane bounds for 64-triangle batches during extraction and rejects wholly back-facing batches before applying the triangle examination budget. It preserves individual checks for uncertain groups and bypasses this optimization for mirrored/singular transforms. Completed models’ surviving batches must still fit the remaining scene draw budget; very detailed surfaces can still be omitted. Initial scene coverage can grow over several frames while geometry loads. Visual alignment, occlusion, moving grids/subparts and frame cost must be checked in game before expanding the scene budget.

## Trying the surface probe

### Capturing an in-game test

After reloading the updated mod, run these commands on the **viewing client**, not a dedicated server:

```text
/thermal telemetry on
/thermal vision colour
```

Aim at functional blocks and armour, test a moving door, switch to `/thermal vision grey`, and test leaving first person or switching cameras. Add observations with, for example, `/thermal vision note Reactor looks good; armour has no overlay`. Then run `/thermal dump` before leaving the world. The existing `Thermodynamics_Telemetry_<session timestamp>.log` in world mod storage contains a `THERMAL VISION PROBE` section; the game log records the output filename. See [telemetry.md](telemetry.md#thermal-vision-test-results) for its fields and limits. Notes preserve case, replace line breaks and cap their text length.

Telemetry records CPU cost and renderer submissions, not screenshots, GPU timing or pixel visibility. A report can explain missing geometry and frame cost; visual notes or captures are still needed to judge alignment, depth artifacts and palette appearance. Collection is opt-in and local; nothing is uploaded automatically. Repeated dumps snapshot cumulative session results rather than starting a new test.

### Probe commands

Reload the world to load the new script and transparent material. With Rich HUD Master registered, stand in first person within 15 m of a simulated block with a model (a reactor or door is a useful starting point), or view it through a working camera block:

| Command | Result |
| --- | --- |
| `/thermal vision colour` (or `color`) | Cividis overlay on the aimed-at block's supported model surfaces. |
| `/thermal vision grey` (or `gray`) | White-hot overlay on the same surfaces. |
| `/thermal vision range -50 50` | Lock both palettes to a Celsius window; default −50–50 °C. Use `range 20 600` for hot machinery. |
| `/thermal vision off` | Disable and discard the probe's cached geometry. |

The HUD explicitly says `SURFACE PROBE 3`, reports submitted triangles and the current locked Celsius window (default −50–50 °C), and marks missing/partial coverage, clipped temperatures and an over-limit target in text. It requires Rich HUD so those limitations are visible. Range changes preserve activation and switching palettes preserves the range; world/HUD reset restores the default. There is no default key binding or saved enable state. Leaving an eligible viewpoint, changing camera, death, HUD reset or world unload clears the request. Menus temporarily hide it. Returning does not reactivate it.

The probe draws the aimed-at block only. It uses opaque `MESH` triangles from the entity model and visible subparts, with their current world transforms. It intentionally skips glass, alpha cutouts, skinned geometry and other unsupported techniques. Slim armour now uses `MyCubeGrid.TryGetCube` and the actual `MyCube.Parts` model geometry, transformed by each live instance matrix and the grid world matrix. This preserves the game’s tile topology for cubes, slopes and corners. Deformed armour is explicitly skipped with `armour-deformation-unsupported`: bone deformation is not yet reproduced. The depth buffer handles occlusion; adjacent tile seams, construction stages, grid motion and game whitelist acceptance of this new path still require in-game verification. All subparts inherit their owning block's temperature. Temperatures are the existing local simulation values; the probe does not add a new server resynchronization or stale-data detector.

The renderer retains geometry across target changes, caps each model at 65,536 triangles, caps the cache at 262,144 triangles / 128 models, and caps per-frame examination at 98,304 triangles. Cache capacity pressure recycles the geometry cache. It visits at most 32 entity parts with a depth limit. The status refreshes every six draw calls. It uses an unlit, depth-tested material with zero alpha saturation, linear-light palette inputs and a 2 mm surface offset. These are experimental choices, not measured final quality or performance settings. Existing heat glow, game exposure and bloom may still affect the result. World scenery retains its ordinary appearance in both palettes.

Offline validation covers compilation against the installed assemblies, palette brightness ordering, invalid-data rejection, clamping and viewpoint-state transitions. The repository's whitelist test checks framework types only; it does not certify every game API access. **User-reported in-game result (2026-09-18):** the probe looks good on functional blocks; armour blocks do not work. This establishes a useful functional-block proof of concept, not validation of every palette, subpart, occlusion case or viewpoint transition. The geometry/depth checks below remain required. A caught rendering error switches the probe off and writes the exception to the game log.

## Measured probe run — 2026-09-18

The ToastyBugs world-closing report (session started 20:24:58 UTC) captured 4,099 frames with zero render errors: 2,295 had no fat-block mesh, 1,149 partial coverage, 282 submitted and 373 no target. Sampled temperatures were 236.90–263.50 K, entirely below the original 293.15–873.15 K window. This motivated the colder default and explicit range command. Only Cividis was captured. An ineligible-viewpoint automatic shutdown was recorded; other gating paths and white-hot visuals still need testing.

CPU mean was 0.182 ms, maximum 8.241 ms on a cache miss; cockpit frames averaged 1.024 ms. These are not GPU timings and do not establish full-scene performance. Target/model churn evicted 431 historical events; telemetry v2 separates important events from geometry diagnostics. Each history remains bounded and reports its own eviction count.

Installed API inspection exposes `IMySlimBlock.CalculateCurrentModel(out Matrix)` for the current model path and orientation. The implementation selects construction-stage models and otherwise returns the definition model for a slim block. This alone does not establish access to the final grid-rendered armour surface or its deformation. Further inspection found the live grid-owned `MyCube.Parts` path used by revision 3. The same part-instance × grid-world transform is used by the installed game’s block-intersection implementation. A static model must not be presented as a faithful damaged hull overlay.

### Revision 3 dump — session 20:56:41 UTC

The manual dump at 21:02:00 UTC recorded 5,998 attempted frames, no rendering exceptions, and 1,020 armour-submitted frames spanning 19 definitions across both palettes. Outcomes also included 1,619 partial functional-block frames, 393 submitted frames and 2,966 no-target frames. This validates runtime access and submission through the armour path, not visual alignment or occlusion. The reskinned Gatling turret examined 23,384 triangles and averaged 2.344 ms per frame; overall mean was 0.207 ms with a 9.997 ms cache-miss maximum. The previously rejected sci-fi hydrogen thruster variants were not established by these rows. Both palettes still used the default −50–50 °C lock despite much hotter targets; revision 4 adds the shared expanding scene window and aim-independent bounded grid coverage.

### Revision 4 scene baseline — session 21:18:06 UTC

The 21:20:27 UTC dump captured 1,436 white-hot scene frames with no render errors. Every frame reached a discovery/draw limit. Average candidate count was 1,066; average blocks with submissions was 62 (candidates include hidden blocks, so this is not a pixel-coverage fraction). CPU mean/max were 4.143/25.519 ms; the peak had no geometry cache miss. AUTO HOLD stayed at 325–700 K. Revision 5 caches discovery between refreshes and rejects back-facing model triangles before world-space vertex transforms. It retains the original world-space path for mirrored/singular transforms and checks degeneracy there. New discovery/sort and candidate-validation/draw timings separate likely causes; improvement is not yet measured in game. Triangle/draw limits are unchanged, and occluded front-facing surfaces can still consume budget.

### Revision 5 comparison — session 22:08:41 UTC

The manual dump at 22:10:54 UTC captured 1,582 white-hot frames with no render errors. Mean/max CPU were 3.069/19.408 ms, versus revision 4's 4.143/25.519 ms. Submitted blocks averaged 86 rather than 62. These runs are not controlled identical-camera benchmarks. Discovery refreshed 177 times, averaging 0.127 ms across all frames; candidate validation/drawing averaged 2.934 ms and contained the 19.351 ms stage peak. All frames remained budget limited; examined triangles averaged 32,350 of 32,768. The adaptive window varied at both ends (low 325–357.32 K, high 643.58–700 K), confirming it was updating.

Revision 6 addresses budget distribution with a bounded armour-first pass, retaining nearest-first order within each category and reserving most triangle/time capacity for entity models. It adds in-loop deadline checks and overlapping discovery/block/triangle/time/model-build/armour-quota counters, plus separate armour/entity submission counts. It does not add occlusion culling, remove the large uncached model limitation, or establish full-scene completeness. Increased coverage and lower spikes must be measured in game.

### Revision 6 result — session 22:15:07 UTC

The 22:17:21 UTC dump recorded 593 colour-mode frames, no render errors, and average submissions of 238 blocks (194 armour, 45 entity models; rounded averages). Mean CPU was 4.163 ms, maximum 211.435 ms; the maximum occurred with no cache miss, in the validation/draw stage. This is a stall measurement, not evidence of a particular cause. All frames remained limited: discovery 593, blocks 305, triangles 516, time 172, model-build 584 and armour quota 586 (overlapping counters).

The model-build count despite zero cache-miss frames exposed indefinite deferral of models over the old 8,192-triangle extraction allowance. Revision 7 replaces this with a resumable bounded job and reports extraction time/progress separately. Slow per-model draw events (at least 8 ms elapsed) identify the model and submitted count without claiming whether the cause was rendering, GC or scheduling. Geometry that exceeds the scene draw budget remains an explicit limitation.

## Recommended appearance

Thermal vision should feel like another mode of the suit optics or camera. Preserve the scene's silhouettes, slopes, openings, moving parts and occlusion. Temperature follows visible surfaces. A reactor behind armour is invisible; the armour shows its own temperature. No boxes around blocks, wireframes, through-wall silhouettes, animated scanning bands or decorative static.

Keep the existing game HUD readable and outside the temperature mapping. Add only a small `THERMAL / COLOUR` or `THERMAL / WHITE HOT` mode label, a narrow temperature scale, and a temperature beside the existing crosshair when it resolves a simulated surface. Reuse the game's font and blue-white HUD vocabulary, with the existing Rich HUD dependency for text. Exact placement must be checked against the native suit HUD, camera overlay, toolbar, FOV and UI scaling in game.

The view has two palettes with the same temperature window:

| Mode | Direction | Purpose |
| --- | --- | --- |
| Colour | Cividis: dark blue → neutral grey → pale yellow | Colour-vision-friendly temperature inspection, with increasing brightness carrying the ordering. |
| Greyscale | Near-black → mid-grey → white | Clean white-hot inspection; the hottest surface is the brightest. |

Colour-blind friendliness is a requirement of the default colour mode. Use the published **Cividis** lookup table rather than a hand-picked heat gradient. It is optimized for normal vision and red-green colour vision deficiency, with increasing lightness; see [Nuñez, Anderton and Renslow (2018)](https://arxiv.org/abs/1712.01662). The concept embeds the 256-entry table from Matplotlib 3.10.3, and its base palette has been checked for strictly increasing relative luminance across all 255 adjacent steps. The scene and legend use the same mapping.

Both modes carry temperature through brightness. An over-limit crosshair target additionally gets `OVER LIMIT` text; palette colour alone never indicates failure. Keep temperature numbers and warning text on a dark backplate so they remain readable over bright surfaces. Surface shading must be restrained so it does not overwhelm temperature differences; the concept defaults to 4% detail contrast.

This replaces the initial uncalibrated violet/orange art-direction ramp. Cividis is specifically supported by evidence for red-green deficiencies, not a guarantee of identical perception for every viewer. Before shipping, inspect final game captures with protanopia, deuteranopia, tritanopia and monochrome simulations, check brightness ordering after bloom/exposure and compositing, and obtain feedback from colour-blind players. Greyscale remains available, but players should not need to select it to read the colour mode.

Use measured node temperature, not temperature divided by each block's failure threshold: two blocks at the same temperature must get the same base palette value. Block limits belong in the target readout. Convert kelvin to the player's display unit only for labels. Retain a little geometric detail for navigation without making sunlit cold metal look hotter than shaded hot metal.

## Temperature window and data

- Start with automatic low/high limits derived from visible, valid thermal surfaces. Exclude sky and unsimulated scenery from the statistics. Use robust percentiles, a minimum span, and roughly one-second smoothing as initial tuning candidates; these are not measured final constants.
- Provide a range lock so inspecting a subsystem does not continually remap its colours. Show `AUTO` or `LOCK` with the actual low/high temperatures. Switching palette preserves the window.
- Indicate clipping at the scale endpoints. A very hot block must not silently flatten all useful contrast across the rest of the ship.
- Update temperature samples and text around 10 Hz initially; transform and draw at frame rate. Interpolate presentation without changing simulation values or hiding a newly crossed failure threshold.
- Missing or stale data is not zero kelvin. Mark the aimed-at surface `NO DATA` or `STALE`, and exclude it from autorange. Do not invent uniform human, terrain or asteroid temperatures: the current block simulation does not supply those fields.
- The crosshair reports the frontmost resolved block. Existing `Crosshair.Resolve` has a 15 m interaction range; camera inspection needs a separately specified, bounded sensing range and precision check.

## View eligibility

The interpretation of “suit or camera” here is an on-foot first-person character view or the live view from a functional camera block. A seated cockpit view, turret view, spectator camera and third-person chase view are excluded. No extra helmet-closed requirement is introduced.

| Current view | Can activate? | On leaving this view |
| --- | --- | --- |
| Living local character, on foot, first person | Yes | Switch off immediately |
| Working camera block actively viewed by this local player | Yes | Switch off immediately |
| Third person, cockpit, turret, spectator, death/respawn | No | No thermal rendering |
| Dedicated server | No | No visual resources |

Evaluate eligibility before every draw, not just at the keypress. For suit mode check the current camera controller, its entity identity against the local character, first-person state, and on-foot/alive state. For camera mode use `IMyCameraBlock.IsActiveLocal` with the actual current camera context and working/closing state. `IsActive` alone can describe another player's camera usage and is insufficient.

Maintain requested state separately from palette preference. Ineligible views clear the requested-on state; returning to first person does not unexpectedly reactivate it. Retain the selected palette. Temporarily suppress the presentation while menus obscure gameplay and ignore hotkeys while chat, terminal or another input field has focus. Bind toggle and palette switch through the existing input/settings system; select defaults only after checking conflicts. Camera terminal actions may set the local viewing preference, but cannot grant vision from a non-camera viewpoint.

On deactivation, death, camera loss, world unload, missing dependencies or renderer failure, hide all thermal UI and release any temporary rendering changes. Do not repaint block skins or mutate their saved colours. Keep the existing diagnostic overlay a separate tool.

## What the inspected sources establish

Research date: 2026-09-18. Game installation: `/home/gauge/Steam/SteamLibrary/steamapps/common/SpaceEngineers`. Decompilation here is an offline research tool, not something mod scripts can do at runtime.

| Evidence | What it establishes | What it does not establish |
| --- | --- | --- |
| `VRage.Game.xml`: `IMyModel.GetTriangle`, `GetVertex`, `GetDrawTechnique` | Model geometry can be read through the mod interface. | Complete render geometry for every slim armour block, animated subpart, construction stage and modded model. |
| WeaponCore `Ui/Hud/HudDraw.cs`, `HudFields.cs` and `Ui/Targeting/TargetUiDraw.cs` | Textured triangle billboards, pooled HUD requests and camera-oriented `PostPP` drawing are practical patterns. | A custom full-screen shader or access to the scene colour buffer. `PostPP` is a draw category, not a programmable post-process. |
| `SpaceEngineers.Game.MySpaceGameDefaultIlChecker.AllowSandboxNamespaces` | `MyPostprocessSettings` and its `Layout` are explicitly allowed types; the layout includes saturation, exposure and other grading fields. | That holding these values gives a mod a permitted way to change the live renderer. |
| `Sandbox.Game.World.MySector.InitEnvironmentSettings` | Environment post-process settings are copied into `VRageRender.MyPostprocessSettingsWrapper.Settings` and marked dirty at initialization. | Live per-player updates by editing the environment definition. |
| Whitelist inspected above | No allowance identified for `MyPostprocessSettingsWrapper` or `MyRenderProxy`; allowing the settings structure is not allowing its renderer entry point. | A universal proof that every indirect route is impossible. A supported runtime route still requires a compiling, in-game probe. |
| `Content/Shaders/Transparent/Billboards.hlsl` | Billboard shading uses its own texture, colour and depth-related inputs. | A mod-supplied fragment shader that samples and recolours the whole scene. |
| `Sandbox.Common.xml`: `IMyCameraBlock.IsActiveLocal`; `VRage.Game.xml`: `IMyCameraController` | Local camera usage and first-person state are available. | That a generic first-person flag alone distinguishes a suit from a cockpit. |

WeaponCore reference revision: `98ce92d32037fd361a676494e5e3cc1bdd1ff6ab`.

- [HUD triangle drawing](https://github.com/StarCoreSE/WeaponCore-StarCore/blob/98ce92d32037fd361a676494e5e3cc1bdd1ff6ab/Data/Scripts/CoreSystems/Ui/Hud/HudDraw.cs)
- [HUD pools and blend selection](https://github.com/StarCoreSE/WeaponCore-StarCore/blob/98ce92d32037fd361a676494e5e3cc1bdd1ff6ab/Data/Scripts/CoreSystems/Ui/Hud/HudFields.cs)
- [Camera-oriented target HUD](https://github.com/StarCoreSE/WeaponCore-StarCore/blob/98ce92d32037fd361a676494e5e3cc1bdd1ff6ab/Data/Scripts/CoreSystems/Ui/Targeting/TargetUiDraw.cs)

## Implementation decision

**Do not commit to a full-screen thermal renderer on the evidence so far.** A screen tint cannot desaturate arbitrary RGB imagery, and desaturating ordinary illumination still does not produce a temperature image. Full greyscale thermal vision requires both control of scene presentation and a temperature signal assigned to its visible surfaces.

The first Workshop-compatible experiment is the small, depth-tested aimed-block probe above. It caches geometry by model identity, transforms by block/subpart, and colours from thermal nodes. Verify its material's blend and depth behavior rather than assuming an enum name guarantees it. Start with a reactor and door; revision 3 adds grid-owned cube and slope armour parts, with in-game validation pending. The existing glow's flat exposed-face quads and the diagnostic boxes do not meet the final visual standard.

This experiment can establish surface fidelity, occlusion and cost. It does **not** by itself meet the full-scene greyscale requirement: ordinary scenery remains ordinary scenery. Label that result a surface-overlay prototype, not finished thermal vision. Existing exposed-face metadata is useful for culling but is insufficient for first-person interiors; room-facing surfaces and door states also matter.

Investigate a supported client-local full-scene rendering route. The user has now rejected a plugin requirement or reduced-coverage standalone fallback: a plugin-only route cannot satisfy acceptance. An optional plugin would still need a thermal surface/object mask and depth integration; a generic colour filter is insufficient. No plugin installation or game-file shader replacement is part of this design.

## Integration and acceptance

Suggested separation: a client view-state controller, a read-only thermal sampler, shared palette/window logic, a renderer, and a Rich HUD presenter. Hook them into the session's existing draw lifecycle. Reuse the solver's temperatures and multiplayer stream; do not add another solver. Account for existing natural heat glow so it cannot overwhelm the thermal palette. Keep all visual work off dedicated servers and avoid scanning all blocks when disabled.

Before declaring a rendering route viable, validate:

1. **Geometry and coverage:** the entire visible world scene, including armour, functional blocks, characters, terrain, asteroids and floating objects. Check slopes, corners, windows, interiors, incomplete construction, animated doors and rotor/piston subgrids. No cube shells, gaps, flicker, detached overlays or temperature leakage through opaque hulls. Crosshair selection must not determine which surfaces receive the final effect.
2. **Meaning:** identically hot materials agree regardless of paint or sunlight; palette changes preserve temperature order and window limits. Unknown scenery is not presented as measured cold scenery.
3. **Gating:** every transition in the eligibility table; camera disconnect or destruction; respawn; remote camera use by another player. Off means no remaining tint or overlay.
4. **Presentation:** daylight, darkness, space, planets, fog, smoke, bloom, antialiasing, camera zoom, ultrawide and HUD scaling. The native toolbar, health and oxygen stay legible. Transparent glass needs an explicit gameplay/rendering policy after testing its depth behavior.
5. **Performance:** record added CPU/GPU frame time, triangle count, allocation and cache-build stalls in representative ships and crowded views. Set the budget from these measurements. Prefer bounded visible geometry and stable detail reduction; do not silently draw through walls when a budget runs out.
6. **Compatibility:** client/server temperature mismatch and stale data, other visual mods, Rich HUD availability, repeated activation, cleanup and world reload. Validate in the actual mod compiler as well as a local build.

The approval point for the visual direction is the palette and restrained HUD. The technical go/no-go point is a real in-game geometry-and-greyscale test. The concept is not evidence that this rendering route already works.

## Native-depth and measured-surface composite experiment

`/thermal vision composite colour` or `grey` enables a new **experimental** combination;
`/thermal vision off` removes it. It requires the same first-person suit/local camera eligibility
as the other probes. Switching viewpoint, death, HUD reset and exceptions use the existing shutdown path.
There are no persistent entity, skin, voxel-definition or gameplay mutations to restore.

Two ordered PostPP planes first replace ordinary scene colour: a near plane gives opaque native
geometry a constant dark neutral value; a black plane at 5 km replaces sky and more distant returns.
This background has **no temperature or distance gradient**. Original-model triangles then draw in
the same ordered PostPP bucket using simulated temperatures, adaptive range and Cividis or white-hot.
Those triangles retain native depth testing. Their colour is supplied in display space because this
pass follows tone mapping. A separate material has a 0.03 mm soft-intersection band, compared with the
existing 2 mm geometry offset, to avoid diluting measured colours at the original surface.

This tests an ownership-preserving alternative to assigning heat to coarse spatial volumes. It also
reduces the context pass from 48 fullscreen planes to two; **GPU improvement has not been measured**.
Discovery reach becomes 5 km in this mode, while existing bounded CPU, block and triangle budgets
remain in place. Telemetry identifies composite frames and records scene coverage/budget limits.

**Not the completed requested feature:** missing, unsupported, deformed or budget-excluded geometry
remains neutral/unmeasured. Terrain and characters have silhouettes but no measured temperature.
Transparent surfaces/effects, LOD mismatch, grazing-angle offsets, distant depth precision and the
previous viewport-edge leak remain native validation gates. Two-plane composition is inferred from
the installed renderer and successful depth experiment, not yet confirmed in-game. A neutral silhouette
must never be interpreted as a cold reading. This does not supersede the rejected reduced-coverage
fallback or relax the full-scene acceptance requirement.

One consolidated native check after reload:

1. Enable telemetry and `composite grey`; view known hot machinery behind a cold foreground block.
   The hot shape should use the real model, disappear behind the foreground, and move with the ship.
2. Switch to `composite colour`; compare the same hot/cold locations and test automatic/manual ranges.
3. Move beyond 100 m, test a camera and suit, and inspect every viewport edge. Record which surfaces
   remain neutral; a bright unmeasured surface or ordinary-colour strip is a failure.
4. Switch to third person, then re-enable in first person and use `off`. Confirm complete visual removal.
   Capture on/off GPU timings in the same view and one telemetry dump for the whole sequence.

The installed analyzer also now confirms voxel-definition enumeration is permitted, while concrete
`MyModel.GetMeshList()` material enumeration is prohibited. The native-material candidate remains
unimplemented pending a supported arbitrary-model material-name source and armour-instance control.

## Composite dump: 2026-09-19 01:34:51 UTC

The manual dump ending 01:36:45 UTC records 1,297 WhiteHot composite frames and no render errors.
All frames are partial and discovery-limited; 1,077 hit the time limit, 716 the triangle limit and
291 have a model build pending. Average candidates/submitted blocks are 1,979/176; these are not
pixel-visible coverage percentages. Adapter CPU averages 3.692 ms, maximum 17.588 ms, excluding GPU.
This confirms execution, not correct native appearance or completed scene coverage.

Two reporting defects were exposed: the scene header retained the old 100 m label despite composite
using 5 km, and four neutral context triangles prevented a row from reporting zero thermal submissions.
Telemetry v9 labels both reaches and excludes context triangles from composite submission rows.
The original report remains unchanged.

Source inspection also found whole-cache clearing at the 128-model/262,144-triangle capacity. The
cache now evicts only least-recently-requested entries until sufficient capacity is available, retaining
per-model unsupported-surface flags. Both count and triangle limits remain unchanged. Offline tests
cover recency, triangle pressure, empty rejected models, oversize rejection and reset. Reduced rebuild
work is expected, but has not yet been measured in-game. Discovery starvation, unsupported materials
and per-frame draw limits remain unresolved; this change does not establish complete coverage.

## Composite visual rejection: 2026-09-19 01:41:48 UTC

The user's screenshot accompanying `Thermodynamics_Telemetry_20260919_014148.log` shows nearly
black silhouettes over most of the view and a narrow isolated patch of measured geometry. This
**fails professional presentation and full-scene thermal coverage**. The composite remains a
rendering diagnostic, not an accepted product route. Do not request another manual test merely
for palette brightness, cache sizing or redistribution of this incomplete overlay.

The v9 dump records 761 WhiteHot composite frames, zero render errors, 15 frames with zero measured
triangles and 761 partial/discovery-limited frames. Average candidates/submitted blocks are
2,655/179, with 24 entity blocks submitted on average. Time limits occur on 731 frames and triangle
limits on 463. CPU averages 3.975 ms, maximum 13.978 ms; GPU cost is not measured. The screenshot's
22 FPS / 0.55 simulation speed has no matched thermal-off baseline, so it cannot establish the
compositor's contribution to overall slowdown.

Only 24 frames have a pending model build (versus 291/1,297 in the prior dump). This is consistent
with less cache work, but different camera paths and warm-up periods prevent a controlled performance
comparison. Nearly all frames remain limited even with warm geometry, so cache tuning is insufficient.

Source-level selection defect: `DrawScene` clears candidates and scans `LiveGrids` from index zero
at every refresh. Its global scan/time bound aborts the outer traversal, and the next refresh starts
over. Consequently later grids can remain excluded indefinitely. Nearest-first drawing then spends
its independent triangle/time budget on a prefix of the discovered candidates. The screenshot is
consistent with this concentration, although telemetry does not identify individual rendered grids.
Round-robin discovery would address indefinite exclusion; simply rotating per-frame submissions would
trade persistent gaps for flicker because submitted geometry is not retained on screen between frames.
Neither change alone meets the full-scene requirement.

Before another native acceptance request, an alternative needs an offline coverage/workload argument
that accounts for arbitrary visible grids, supported material techniques, changing viewpoint and native
occlusion. Increasing caps, brightening unmeasured silhouettes, or assigning distance as temperature
does not resolve this rejection. Full-scene standalone thermal rendering remains unresolved.

## Accepted approximation and regional rendering direction

On 2026-09-19 the user clarified that professional appearance and locating a generally hot group
of blocks for extinguisher cooling matter more than exact per-surface temperature. Some spatial
error is acceptable. This supersedes the strict ownership interpretation used to reject coarse
volumes; it does **not** waive standalone operation, both palettes, viewpoint gating, full-scene
presentation or stable live feedback. Patchy copied geometry and distance-as-heat remain unsuitable.

The next candidate is a thermal field made of small block-group regions, displayed through original
scene depth. An entering boundary paints the region temperature and an exiting boundary restores
neutral context. Six quad faces (12 submitted triangles) per region replace potentially thousands
of copied model triangles. Geometry silhouettes still come from the native depth buffer. Cividis and
white-hot share the same adaptive temperature range. Temperature aggregation should preserve useful
hotspots rather than average them away; group size must remain useful for extinguisher targeting.
The grouping rule, maximum acceptable spill and overlapping-region arbitration are not yet validated.

The executable `ordered-box-faces` lab now models actual centre-depth-sorted boundary polygons,
rather than assuming an ideal per-pixel field lookup. Initial analytic fixtures recover 1,600/1,600
isolated hot pixels and 880/880 visible hot pixels behind a separate foreground object, with zero
false heat on its 720 foreground pixels. A cold object inside the hot region inherits heat on all
360 of its visible pixels. That is localized region-assignment error, not proof of acceptable visual
quality. These fixtures use a single axis-aligned region and do not justify native deployment yet.
The old strict lab continues to exit 2; approximation acceptance is a separate unresolved gate.

Before a combined native trial, test rotated and adjacent groups, overlapping grids, camera-inside
views, coincident boundaries, sky behind regions and moving cameras. In particular, centre-depth
sorting may not reproduce per-pixel boundary order for overlapping regions. Interior-camera views
need an initial field value before boundary submission. Neither case is covered by the initial proof.
Avoid frame-to-frame rotating coverage as a budget workaround: a professional live view must remain
stable. CPU/GPU cost, fair whole-scene field updates and native blending remain unmeasured.

Parallel line of investigation (read-only, no runtime change): allowed binary game-content reading
can obtain material descriptors without prohibited model enumeration. The new offline material audit
reads indexed MWM metadata and skips index buffers, identifying texture slots and draw techniques.
It must follow `GeometryDataAsset` references for wrapper models; Camera.mwm is one such example.
A scan of the installed Cubes tree parsed 21,239 files and rejected eight legacy files without indexed
version headers. This is a metadata inventory, not a complete arbitrary-model importer. The installed
analyzer accepts the binary-reader API path but rejects `MyCubeGridRenderData`, so direct armour
render-instance access remains blocked. Regional rendering avoids depending on either model path.

## Region overlap and inside-camera preparation

The next stress fixtures reproduce missing heat with naive boundary overwrites: exiting a contained
hot region clears the enclosing hot region, and a camera already inside a region never encounters its
entry face. These are rendering failures even under the relaxed block-group accuracy target.

`ThermalVisionRegionPartition` now prepares disjoint axis-aligned regions with maximum-temperature
arbitration. Overlaps split into up to six residual slabs; the hotter value is preserved. Additions
are transactional: capacity exhaustion returns false and leaves the previous field intact. This is
not an incremental world sampler, a moving-grid solution, or a bounded-time render loop. Pathological
fragmentation and preparation cost still need scene-scale measurement. No silent coverage truncation
or approximate success claim is attached to a capacity failure.

The same core now clips a near-plane viewport quad against a region's six bounds, providing polygon
vertices for initializing camera-inside views. Tests compare its polygon coverage against independent
point-in-region checks across three camera rotations, including an empty intersection. The native
adapter must still use the correct near-plane offset/material and submit the cap before boundaries.

The stress lab uses the real partitioner for nested/adjacent regions. Its near-plane seed remains an
analytic lookup; the separately tested clipped-cap implementation has not been rendered natively.
The isolated overlap and inside-camera corrections pass synthetic checks. General polygon ordering
across many disjoint regions, temperatures at shared boundaries, motion, near-plane precision,
preparation latency and GPU cost remain open. No new in-game command is enabled by this work.

## Spatial ordering preparation

`ThermalVisionRegionOrder` builds a bounded binary spatial partition from the disjoint thermal field.
Regions crossing a split plane are split into fragments with unchanged temperatures. Camera traversal
visits the eye-side child first, establishing near-to-far order along rays without centre-distance
sorting. Within each convex fragment, the intended submission sequence is its clipped near cap,
entering faces, then exiting faces. Shared boundaries must use the same coordinates; native depth and
soft-intersection precision at those boundaries still require testing.

Split planes are chosen from existing region boundaries. An initial centre-midpoint implementation
failed a small overlapping-input fixture by repeatedly cutting toward touching boundaries; the
regression now exercises the corrected preparation path. Leaf count, node count and depth are bounded,
and failed preparation returns no partial tree. The caller must explicitly handle failure rather than
silently drawing a subset. No world sampler or runtime render mode consumes this tree yet.

Independent tests check interval ordering along 1,200 deterministic rays from three viewpoints,
including a point inside the field. They also compare 19,200 point samples against the original
partition and enforce at most one containing fragment. A 512-region aligned fixture retains exactly
512 leaves and 1,023 tree nodes. These are structural/workload counts, not native GPU or preparation-time
measurements. Arbitrary fields can fragment more heavily and hit the configured limit.

Next integration gates remain: fair whole-scene temperature aggregation at useful spatial resolution,
complete snapshot publication and moving-grid handling, native polygon/near-cap submission, and measured
preparation/draw cost. No additional game reload is needed for these offline changes.

## Fair regional sampling and publication

`ThermalVisionRegionScan` now rasterizes stable source bounds into a world-aligned cell field. Each
work unit advances one source enumerator, deposits one cell, or stages one output cell. Sources rotate
after each unit, including during rasterization of a large bound. Maximum temperature wins within a
cell to preserve small hotspots for extinguisher targeting; the resulting field is deliberately a
block-group approximation. Cell-aligned boundaries are half-open, including negative coordinates.

The previous published field remains visible to callers until an entire new generation has scanned
and staged successfully. Source exceptions, invalid samples and source/cell capacity limits cancel
the generation without replacing that field. Cancellation disposes all source enumerators. Runtime
integration must enforce a maximum age and must not silently keep stale moving-grid heat indefinitely.
The scanner cannot bound time spent in an external enumerator or guarantee coherent moving-grid
positions; immutable source snapshots or explicit movement/version invalidation remain adapter work.
Logical work limits also do not claim a hard wall-time bound for allocation and collection cleanup.

The synthetic workload mirrors the order of magnitude of the dump: 113 sources with 2,348 samples
each (265,324 samples), mapped to 1,808 occupied cells. At 4,096 logical work units per update, completing
and publishing takes 532,570 units / 131 updates. At one update per rendered frame this would be about
2.18 seconds at 60 FPS or 3.97 seconds at 33 FPS, before ordering/preparation. These are workload
arithmetic, not native timings. Fairness is solved for finite stable sources, but this refresh delay
is not yet accepted for interactive cooling. Incremental temperature changes, separate preparation
cadence or a measured larger work budget need evaluation before live integration.

Tests cover the last source being reached, every source contributing to the large completed field,
hotspot preservation, bounded per-call logical work, negative cell boundaries, deferred publication,
capacity rejection and source failure/disposal. No new game mode is enabled by this sampling work.

## Regional renderer: ready for the first native test

Reload the mod/world, then use `/thermal telemetry on` followed by
`/thermal vision regions grey` or `/thermal vision regions colour`. Use `/thermal vision off` to close.
This is the new regional renderer, not `composite`, `depth` or the survey snapshot. It remains an
experiment, not a completed feature or a validated performance improvement.

The adapter scans simulated grids within 5 km fairly and rasterizes conservative block bounds into
world-aligned hot groups. A complete field feeds the spatial ordering tree directly without overlap
repartitioning. Native depth supplies silhouettes and foreground occlusion. For each ordered region,
the renderer submits a clipped near cap, entering thermal faces, then neutral exiting faces. A final
black far plane clears regions over sky. Both palettes use the same temperature field and adaptive
range; existing manual-range controls also apply. The neutral context is visibly brighter than in the
rejected composite. Neutral grey is unmeasured, not a cold-temperature reading.

The cell limit is 1,536, with matching ordered-fragment limit; worst-case box faces plus clipped caps
and background are 30,722 billboards, below the installed renderer's 32,768-entry ceiling. Other mods
share that buffer, so this is not a guarantee against global engine truncation. Groups start at 5 m. As of the capacity fix below, a full working map merges into cells twice as
large and continues the same scan, through 20 m and beyond if needed, up to 5,120 m. The actual group
size is displayed. Coarse output may be unsuitable for extinguisher targeting; successful acquisition
is not itself a spatial-quality pass. No prefix of grids is published instead.

Sampling has a 32,768 logical-unit ceiling per draw and a soft 2 ms allowance, checked in batches of
128 units. Enumerator work and completed-field tree preparation can exceed that soft time allowance.
A scan older than 12 seconds is abandoned. Published fields expire after eight seconds and are
invalidated earlier if grid block counts change, a grid closes, or conservative pose displacement
exceeds a quarter of the group size. These checks prevent indefinitely painting old grid positions;
fast moving grids can still prevent successful acquisition. Frame/field age is shown rather than
hidden. Temperature changes can lag the scan, and a snapshot is not an instantaneous world sample.

Activation retains suit/camera eligibility checks. Menus suppress drawing. Off, viewpoint loss,
HUD reset, exceptions and world unload cancel sampling and clear client-only state. No entity paint,
render-material overrides, game binaries, world data or plugins are modified.

One consolidated test:

1. In first person, enable telemetry and `regions grey`. Wait for `LIVE GROUP ESTIMATE` and note group
   size and age. If it stays in acquisition or reports a capacity failure, capture that status.
2. Look at the same fleet and move the viewpoint around it. Hot groups should cover native silhouettes
   rather than narrow copied-geometry patches; unrelated foreground objects should occlude them.
3. Aim the extinguisher at a hot group and cool it. For an interpretable cooling comparison, lock the
   temperature range with `/thermal vision range 0 500`; automatic exposure otherwise changes contrast.
4. Switch to `regions colour`, test inside/near a structure and a working camera, then verify third-person
   disables the mode and `/thermal vision off` restores normal rendering.
5. Send one screenshot and `/thermal dump`. Report whether the highlighted group is useful for aiming,
   whether heat noticeably follows cooling, and any flashes or broad false-hot areas.

Events report acquisition size/counts, field publication, region/triangle submissions, age and failures.
Rows identify group size and count boundary triangles rather than model geometry. CPU adapter timing
includes preparation but excludes GPU completion. This native test must establish appearance, cooling
latency, shared-boundary precision and performance; offline tests cannot establish those outcomes.

## First regional dump: capacity dead end and correction

`Thermodynamics_Telemetry_20260919_023107.log` confirms zero thermal triangles in all 1,706 captured
frames and zero render errors. Acquisition failed at 5 m, 10 m and 20 m, then repeatedly hit `cell-limit`
at 20 m. The screenshot's neutral silhouettes were the backdrop of a renderer that had never obtained
a complete field, not evidence of invisible submitted thermal polygons. Adapter CPU averaged 0.213 ms
because most frames were waiting between failed attempts.

The scanner now has optional bounded in-generation coarsening. At capacity it merges all accumulated
cells into their parents using maximum temperature, remaps pending sample bounds, and continues without
restarting source enumeration. Negative coordinates use floor division. Default scanner construction
retains fixed-resolution rejection semantics for existing callers. Region mode opts into a maximum
5,120 m cell size. This fixes the 20 m acquisition dead end but deliberately does not claim that arbitrarily
coarse fields meet the targeting requirement. Adaptive spatial detail remains a separate quality issue.

A new 8,000-sample fixture cannot fit in the native 1,536-cell budget at 20 m. The adaptive scan completes,
retains every source sample and matches an independently computed maximum-temperature map at its final
resolution. Coarsening copies at most the configured cell capacity in one operation, so logical work
counts do not imply constant per-unit execution time; the adapter's 2 ms allowance remains soft.

The normal scene now stays visible whenever no valid field is available. Acquisition notifications
show processed sample count and current group size, including when the Rich HUD panel is not visible.
A neutral fullscreen backdrop is drawn only with a completed field. The same existing `regions grey`
and `regions colour` commands select the corrected implementation after a world/mod reload.

## Local temperature detail after the 40 m test

The `20260919_031531` dump recorded 4,048 frames without render errors, 764 coarse cells at 40 m,
and 268,411 samples from 126 grids per scan. This establishes functioning coverage, but the user's
screenshot shows broad uniform patches that are too coarse for nearby cooling decisions.

Regional mode now reserves 896 cells for distant context and up to 512 cells for a 20 m cube of
2.5 m temperature groups around the nearby aimed block. The existing crosshair resolver reaches
15 m; without a target, the detail cube is centred approximately 25 m forward. Its boundaries align
to the 2.5 m lattice. Fine samples are collected during the same global block pass, then rasterized
under the existing update budget. This does not require a second enumeration of all grids.

The fine cube replaces the coarse field inside its bounds, including cooler readings and gaps.
A cooled block must not inherit the hottest temperature from its former 40 m group. Adaptive exposure
includes the fine temperatures so that the cool end of a mixed neighbourhood remains represented.
The combined field retains the 1,536-fragment ceiling. If focus preparation or ordering exceeds capacity,
the completed coarse field remains usable and the panel reports `NO AIM DETAIL`; telemetry records
`focus=False`. Fine sample collection is capped at 32,768 inputs. Grids intersecting the focus volume
use a stricter 0.625 m conservative pose-displacement limit to reject stale fine data.

Reload the world/mod, then use `/thermal telemetry on` and `/thermal vision regions grey` (or `colour`).
Aim at a nearby mixed hot/cold group within 15 m, allow a scan to finish, and look for `AIM DETAIL 2.5 m`.
Cool part of that group with the extinguisher and verify the local patch changes after refresh while
its hot neighbour remains distinct. Use `/thermal vision range 0 500` for a fixed Celsius comparison,
then `/thermal vision range auto` to restore adaptation. Capture `/thermal dump` after the test.

This remains a world-aligned block-group estimate: rotated bounds can spread heat and small-grid
blocks can share one 2.5 m cell. Aim changes wait for the next scan; native visual quality and cooling
latency still require this test. Navigation shading is deliberately unchanged at the user's request.

## Distance-based temperature LOD

The user confirmed that cooled patches were distinguishable in the fine-focus test. The
`20260919_033547` dump recorded 5,050 frames, no render errors, 2.582 ms average adapter CPU,
and active fine focus in all retained publication events. This is the accepted temperature baseline;
indoor navigation remains deferred.

The next test replaces aim-only focus with three nested camera-centred, world-aligned boxes. Detail
therefore resolves on approach without crosshair targeting and works with either thermal palette.
These are cubic bands, not exact radial thresholds; grid alignment shifts their edges by up to one
initial cell width. Distances below describe approximate half-widths along the world axes.

| Approximate vicinity | Initial temperature cell | Independent cell budget |
| --- | --- | --- |
| Within 10 m | 2.5 m | 512 |
| Within 40 m | 10 m | 192 |
| Within 160 m | 40 m | 192 |
| Remaining scene out to 5 km | Adaptive, starts at 5 m | 384 |

Each band collects clipped block bounds during the existing single global enumeration. It can double
its cell size independently if its budget fills, up to its box width. Close detail cannot be consumed
by a distant fleet. These sizes are targets, not guaranteed resolutions in dense scenes. Outside the
bands the global estimate can be coarser than 40 m. Minimum detail remains 2.5 m; small-grid blocks
and rotated block bounds can still share a cell. Distant regions use their maximum sampled temperature,
so a small hotspot can colour a much larger area until closer detail separates it.

Publication replaces outer estimates with complete inner fields, including cool cells and empty gaps.
Coarsened cells are clipped to their band's exact boundary. The combined field still has a hard
1,536-fragment ceiling. If partition/order preparation cannot fit, outer detail bands are omitted before
close detail; the final fallback is the complete coarse field. Each band's source list is capped at
32,768 inputs; overflowing bands are omitted rather than publishing a prefix of samples.

The panel and publication telemetry report actual `LOD near/mid/far` cell sizes or `fallback`.
Movement invalidation uses the smallest requested cell size intersecting each grid. Scans and exposure
retain their existing scheduling, expiry and time smoothing. Spatial LOD currently changes at completed
scan boundaries, with no crossfade; expect roughly the previous 1–1.3 s scan latency in comparable loads,
but the new workload must be measured. Visual popping and performance remain native test gates.

Reload and enable `/thermal telemetry on`, then `/thermal vision regions grey` or `colour`. Approach
a mixed hot/cold grid from over 160 m through 40 m to under 10 m, then back away. Repeat while looking
off-centre to verify detail is not aim-dependent. Cool a nearby patch, confirm the cold patch remains
separate, and capture `/thermal dump`. For comparable colours at different distances, temporarily use
`/thermal vision range 0 500`; restore `/thermal vision range auto` afterward. Navigation appearance
is unchanged.

## LOD screenshot and contrast follow-up

The `20260919_035021` dump records 9,043 frames, zero render errors and 2.019 ms average
adapter CPU. Retained publication events show 2.5/10/40 m LOD active with 80 m outer context.
The screenshot still shows faint local temperature differences; it does not establish the visible
surface temperature spread. Bands marked active can also contain no sampled surfaces.

Automatic exposure now samples rendered regions intersecting the camera frustum, rather than all
scanned grids within 5 km. This prevents offscreen hot/cold grids from expanding the display range.
It preserves the existing 50 K minimum span and temporal adaptation. Frustum intersection is an
approximation: hidden regions behind foreground surfaces can still affect exposure. Manual ranges
are unchanged. Draw telemetry now includes `visible-K` (in-frustum region extrema) and `display-K`
so future captures can distinguish narrow local temperatures from a broad display range. These are
region estimates, not per-pixel measurements, and do not prove visual hotspot quality.

## Viewport grid restriction

Regional acquisition now includes only grids whose world bounds intersect the camera frustum at
scan start. Each draw rechecks published grids against the current camera, then rejects thermal
regions unless both the region and an overlapping grid intersect the viewport. Automatic exposure
uses only the surviving regions. Draw events include `viewport-grids` to diagnose camera turns.
Newly visible grids can wait for the next complete scan; offscreen grids stop contributing draw
regions immediately, except where a shared coarse region also overlaps an in-view grid.

This is bounding-box/frustum culling, not a pixel-exact grid mask or an occlusion query. The existing
native-depth overlay can still affect a non-grid surface inside the same temperature region. It does
not change entity textures or paint. Fullscreen neutral/sky context remains part of thermal mode.

## Surface-anchored detail correction

The `20260919_040002` dump records 4,106 frames without render errors. Its final retained field has
47,683 sampled blocks but only 62 ordered fragments; the in-frustum temperature range is approximately
190–676 K with display range 175–700 K. The screenshot remains visually coarse. Prior `2.5 m` status
reported the configured cell size even when the fine volume contained no cells, so it did not prove
that a visible ship received fine detail.

The fine volume now targets the nearest in-frustum sampled block bound discovered during the previous
completed scan. Initial acquisition still uses the camera-centred volume; the following scan moves the
fine budget onto the sampled surface. A camera move over 20 m or an offscreen anchor discards that hint.
This uses conservative block bounds, not a ray-confirmed visible surface: occluded blocks can still be
selected. Other context bands and the viewport draw filter remain active.

Fine target cells are 2.5 m within 40 m of the anchor, 5 m through 80 m, 10 m through 160 m,
20 m through 320 m and 40 m beyond that. The focus box remains eight cells wide, so its spatial coverage
increases with distance. It retains its 512-cell budget and replaces cooler cells correctly, even when
it crosses the outer context bands. This is local surface detail, not full-grid per-block resolution.
LOD transitions remain discrete and can wait an additional scan to acquire a new anchor.

HUD and publication events now include populated cell counts for each band, plus nearest sampled-bound
distance in telemetry. The approach regression now includes a hot/cold pair 30 m from the camera,
outside the former 20 m camera-centred fine box; both retain their own temperatures after composition.
Reload and use the same regional-mode commands, pause for two scans near a ship, then capture a dump.
Native visual improvement is not established by the offline regression.

## Next-renderer investigation: gradient first

The user rejected regional flat filling as sufficient by itself, asked to preserve it in the lab,
and clarified that a clear temperature gradient matters more than perfect surface detail.
`/thermal vision lab regions colour|grey` now identifies that preserved baseline explicitly.

Two distinct problems must be evaluated separately:

* Spatial aggregation uses maximum temperature. A single hot block can flatten a whole coarse region
  to that value before any palette or contrast adjustment occurs. More palette contrast cannot recover
  a gradient that was already discarded. A future estimator should preserve both neighbourhood
  temperature and a separate peak indicator, with surface-local refinement for extinguishing.
* Global extrema can reserve almost all display contrast for unusual cold/hot regions. A future range
  estimator should compare bounded local/percentile windows with a visible scale and saturation markers.
  It must not erase a dangerous outlier or silently make a cooled patch appear hotter.

Candidate A is a surface-sampled continuous thermal field: gather bounded depth-tested surface samples,
interpolate only within the same grid and compatible depth/normal neighbourhood, and keep unmeasured
areas distinct. Native geometry supplies silhouette/occlusion where available; temperature samples,
not ordinary texture brightness, supply the gradient. Investigate whether a sparse sample field can
cover useful visible areas without returning to the rejected pixel survey or costly full mesh copy.
Gates: hot/cold adjacent blocks, a thin hot pipe, cooled patch, front cold plate over hot machinery,
rotated armour, empty-space gaps, camera motion, sampling latency and dense-scene budget. This route
is designed for investigation, not implemented or established feasible.

Candidate B is native local thermal lighting. Existing `ThermalGlow` uses `MyLights.AddLight`, light
colour/intensity/range and symmetric `RemoveLight` cleanup. That provides a permitted engine path that
preserves material normals, but ordinary material colour, sunlight and light spill alter the observed
thermal gradient. Therefore it is secondary structural support at most, not the primary temperature
encoding. No new lighting probe has been enabled.

Rechecked installed `Content/Shaders/Transparent/Billboards.hlsl` and the local decompilation of
`MyBillboardRenderer.RenderPostPP`: the billboard path reads scene depth and its own texture/colour,
then uses premultiplied alpha blending. Making the current volume entry/exit faces translucent does
not preserve correct region ownership: an exit face cannot restore the overwritten scene colour.
Do not ship that shortcut as thermal fusion. Native material overrides remain an alternative under
investigation, with the earlier unresolved armour coverage/material ownership limitations.

The next comparison must report gradient separability and cooling localization, not just surface
appearance. The goal is still plugin-independent colour-blind-friendly colour and white-hot rendering
with first-person suit/camera gating. This investigation has not yet produced a replacement renderer.

## Gradient partition investigation result

The reproducible [gradient study](thermal-vision-lab.md#temperature-gradient-partition-study) compares
fixed cells, an adaptive octree and temperature-directed binary splits without changing the native lab.
Directed splits preserve simple gradients, isolated peaks and cooled patches with fewer regions, but
are not generally sufficient: diagonal gradients do not improve and a rounded hotspot has higher mean
error. A finer uniform grid or a universally applied binary splitter is therefore not yet justified.

This narrows the next investigation to local continuous-temperature fitting with peak/cooling residuals,
and surface-aware allocation. Native-depth rendering may still be reusable as a compositor, but the
archived max-cell estimator and opaque presentation remain a lab, not the proposed finished feature.
No new manual game test is needed for this offline result.

## Renewed per-block route

The corpus audit shows exact block-local ordering is feasible for all three panel ships without
changing their temperatures. Oriented quads halve face submission count relative to the earlier
triangle path. A [bounded native block lab](thermal-vision-lab.md#exact-block-bound-native-probe)
is now connected, initially for one complete grid with at most 1,536 blocks/fragments. It has not
been visually validated in-game and does not satisfy full-scene coverage yet. Its per-block sources
are exact simulation values, while its volume-to-surface assignment remains an approximation.

The renderer budget is therefore a scaling constraint, not a universal proof that per-block thermal
rendering is impossible. Remaining work includes fleet ownership/ordering, native quad winding and
soft-intersection verification, live preparation cost, larger-grid budgets and appearance approval.
The original region estimator remains preserved as a separate lab; no game binaries or plugins are
required by this probe.

## All viewport grids must receive thermal coverage

User feedback after the first per-block native success establishes that neutral secondary grids are
not acceptable. The [viewport fleet extension](thermal-vision-lab.md#viewport-fleet-extension-after-the-three-grid-screenshot)
now reserves coverage for every eligible in-view grid within the game camera’s view distance before allocating
nearby detail. Native per-block appearance remains the preferred close-view treatment; distant grids
can use local temperature groups. The common-basis approximation for differently rotated grids,
capacity fallback and acquisition delays remain explicit lab limitations pending the next dump.

The fleet now follows the active camera frustum and far plane; native mesh LOD supplies the depth
silhouette while thermal block/group budgets remain independent. See the [view-distance and LOD audit](thermal-vision-lab.md#game-view-distance-and-native-lod) for API evidence and remaining transition limitations.

The [selected mixed-detail candidate](thermal-vision-lab.md#selected-candidate-mixed-block-detail-with-mean-groups) now powers the plain colour/grey commands. It preserves exact nearby readings while averaging coarse groups; corpus evaluation and outstanding native acceptance limits are recorded with the implementation.

The latest native feedback rejects flat-cell banding. The new [softness target](thermal-vision-lab.md#appearance-correction-soften-heat-locations-rather-than-enlarge-flat-groups) keeps heat locations stable and softens detail with distance; the mixed-detail implementation remains a lab candidate, not an accepted final presentation.

The [billboard reduction pass](thermal-vision-lab.md#billboard-reduction-without-temperature-coarsening) removes redundant shared boundaries and offscreen faces while retaining the existing thermal field; it is groundwork for the smooth renderer rather than further temperature grouping.

## Change log

| Date | Change |
| --- | --- |
| 2026-10-28 | Recorded bounded whole-cell rendering, coarse fallback, neutral depth context and the remaining temperature/transparent-surface limitations. |
| 2026-09-19 | Preserved toggle selection across camera/HUD changes, removed chat/cursor drawing suppression, and retained selection after render failures. |
| 2026-09-19 | Separated thermal-vision presentation from the simulation model, with an independent project and assembly isolation checks. |
| 2026-09-19 | Made viewport-wide grid coverage mandatory; connected fleet budgeting and complete-field fallback. Reopened per-block rendering with exact local bounds and quad submission; connected a bounded single-grid lab and documented corpus counts. Compared fixed, octree and directed partitions; recorded gains and radial/diagonal failure cases without promoting a new renderer. Archived regional presentation as a labelled lab; prioritized gradient separability, identified max-aggregation loss and documented surface-field versus native-lighting candidates. Corrected empty-space fine LOD by anchoring local detail to sampled surfaces; added populated-cell/distance telemetry and approach regressions. Restricted regional acquisition and per-frame thermal submission to viewport-intersecting grids; added viewport-grid telemetry and documented bounding-box limitations. Reviewed the LOD dump; restricted automatic contrast to in-frustum regions and added source/display temperature-range telemetry. Recorded successful cooled-patch feedback and added camera-centred distance LOD with independently budgeted bands, inner-field replacement and approach/dense-scene regressions. Added 2.5 m local temperature detail with coarse-field replacement, capacity fallback and cooling regressions after the 40 m test. Deferred navigation shading. Diagnosed the first regional dump (1,706 zero-submission frames); added in-scan capacity coarsening, normal-view acquisition and an 8,000-sample overflow regression. Connected `regions colour/grey` to native depth with ordered fields, near caps, adaptive group sizing, stale/movement invalidation and telemetry; added the first consolidated test protocol. Added fair, resumable region sampling with atomic publication and a 265,324-sample workload/latency check. Added bounded spatial ordering, independent ray/field preservation checks and a 512-region fragmentation fixture; corrected repeated midpoint subdivision. Added overlap/inside-camera stress fixtures, transactional disjoint-region preparation and rotated near-plane clipping tests. Accepted approximate block-group heat as the revised accuracy target; added ordered-boundary synthetic fixtures and documented remaining region-ordering gates. Audited binary model metadata access and blocked armour render data. Recorded visual rejection and the second composite dump (761 partial frames), traced discovery-prefix starvation and distinguished warm-cache draw limits from rebuild cost. Reviewed the first composite dump (1,297 partial frames), corrected context/reach telemetry and replaced whole-cache flushes with bounded LRU eviction. |
| 2026-09-18 | Added the two-plane neutral-context/measured-geometry composite experiment, 5 km discovery, PostPP colour handling and a narrow surface intersection band. Confirmed material enumeration is prohibited; excluded offline checker code from the game build. |
| 2026-09-18 | Validated four material API patterns and two rejection controls with the installed analyzer; added a reproducible offline checker and documented the voxel XML/base-builder path and immutable material-cache requirement. |
| 2026-09-18 | Audited entity and voxel material-update paths as a new ownership-preserving candidate; recorded rollback/subpart hazards, unresolved permissions and armour coverage. Checked decal owner IDs and native hologram shaders without changing game state. |
| 2026-09-18 | Added synthetic ownership counterexamples for depth slices, expanded slices and ideal exact-cell boundaries. All three remain incomplete; no new in-game temperature mode is enabled. |
| 2026-09-18 | Responded to the depth screenshot: extended diagnostic reach to 5 km, raised distant-silhouette brightness and added viewport telemetry/overscan. Hotspot presentation remains unimplemented; distance shading cannot reveal heat. |
| 2026-09-18 | Recorded positive user feedback and the first native depth-layer dump: both palettes, 627 active frames, zero render errors; temperature assignment and GPU cost remain unvalidated. |
| 2026-09-18 | Added the ordered PostPP depth-layer experiment after auditing native depth reads, blend order and render scheduling; documented temperature assignment and GPU fill cost as unresolved gates. |
| 2026-09-18 | Tested temporal reuse offline; corrected the highlight audit to acknowledge interior blending and documented its disabled depth test and explicit overlap-mask limitation. |
| 2026-09-18 | Investigated reconstructed sensor imagery as a standalone workaround; documented resolution, update rate, collision fidelity and temperature-estimation compromises with an offline comparison. |
| 2026-09-18 | Recorded the explicit full-scene, plugin-independent acceptance requirement and installed-API audit. Reduced-coverage fallback is rejected; no complete standalone rendering route is established. |
| 2026-09-18 | Added revision 8 conservative batch rejection, shared synthetic dense-hull/differential tests and culling telemetry. Added revision 7 progressive geometry extraction and slow-model diagnostics after the next dump exposed indefinite model deferral. Added revision 6 bounded armour-first submission, in-loop deadline checks and budget-reason counters. Added revision 5 discovery reuse, early backface rejection and stage timings against the revision 4 baseline, plus time-smoothed adaptive range expansion and contraction. Added revision 4 bounded aim-independent grid scene mode, shared AUTO HOLD window and scene telemetry after reviewing the next dump. Added revision 3 live undeformed armour parts, larger model budgets, and geometry reuse across aim changes (in-game checks pending). Recorded the first telemetry run, added a configurable cold-scene window and protected important-event history. Added the two-palette proposal, source-based feasibility assessment and prototype criteria. Made colour-blind friendliness a default-mode requirement using Cividis. Added the opt-in aimed-block surface probe, commands, bounded geometry cache, viewpoint gating and offline tests. Recorded the user's successful functional-block visual test and missing armour, and clarified that final coverage includes the entire visible world scene. |
