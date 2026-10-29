# Architecture

The mod is in two halves, and the line between them is the point of the design.

| Half | Where | Knows about |
| --- | --- | --- |
| **The model** | [`Thermodynamics/Core/`](../Thermodynamics/Core) | Blocks, heat, geometry. One Space Engineers assembly, `VRage.Math`, for vectors. No session, no entity, no `MyAPIGateway`. |
| **The adapter** | everything else under [`Thermodynamics/`](../Thermodynamics) | Definitions, entity events, raycasts, damage, storage, HUD, terminal, mod API. |

The model is what [`tests/`](../tests) builds and tests outside the game; the adapter is what the game
compiles around it. Everything the game supplies crosses one of three boundaries — block layout, an
environment sample, and results out — and nothing else.

### Repository and provider boundary

Thermodynamics owns the thermal model, SE1 adapter, thermal presentation and its public API.
Aerodynamics and RelativeTopSpeed are external projects and are not built or maintained here.
`ThermalApiAerodynamics` exports copied geometry/environment inputs and dispatches registered
heating and flight-readout delegates. `ThermalGridGeometryApi` tracks geometry revisions.
No force application occurs in Thermodynamics. See [the integration boundary](aerodynamics.md).

The production model and presentation remain separate. `Thermodynamics.Core` now compiles the
same production Core sources and has no fixture or flight implementation. Historical rigs opt
into `HistoricalThermalSettings` in `Thermodynamics.Fixtures`; that assembly owns read-only flight
sources and adaptation to thermal grids. No provider is installed by a normal Core constructor.

### Responsibilities after cleanup

| Area | Source ownership |
| --- | --- |
| Session | `Session.cs` owns startup, tick, input and unload; `SessionCommands.cs` owns chat dispatch and replies. |
| Settings | `Settings.cs` owns serialized fields, defaults, migration and model application; `SettingsAccess.cs` owns the ordered read/write catalogue and client ownership; `SettingsStorage.cs` owns world persistence. |
| Settings UI | `ThermalSettingsMenuLayout.cs` owns labels, ranges and page metadata; the menu and window own live controls and rendering. |
| Solver | `ThermalSolver.cs` owns shared state and block access. `Topology` owns links, `Devices` owns loop/room/pump construction, `Environment` owns environmental terms and shadow refresh, `Coupling` owns conduction/loop/room exchange, `Stability` owns substep demand, and `State` owns synchronization and publication. Existing `Step` and `Cues` partials retain execution and event sequencing. |
| Telemetry | Collectors retain their measurements. `TelemetryReport.cs` coordinates writing and general sections; `TelemetryReportPerformance.cs` owns cost calculations, `TelemetryReportEnvironment.cs` owns climate summaries, and `TelemetryReportCsv.cs` owns CSV serialization. |
| Thermal vision | `ThermalVisionProbeGeometry.cs` owns geometry preparation and model drawing. The probe owns cached geometry and pending builds: `Stop` clears those, `ResetHud` releases controls while preserving the active view, and `Reset` additionally clears fleet and mode state. |

Partial files share their original classes; this adds no dispatch or per-step allocation.
No solver term or summation order changes as part of the split.

### Thermal vision is presentation, not simulation

Thermal vision consumes temperatures; its palettes, exposure, LOD, surface
approximations and camera/HUD policies do not belong to the physical model.
These sources live under `Presentation/ThermalVision/`, in namespace
`Thermodynamics.Presentation`, and compile separately as `Thermodynamics.Presentation`.
They were previously misplaced under `Core/Simulation`; being testable without
the game was not sufficient reason to include them in the simulation assembly.

The dependency direction is:

```text
SE1 adapter → simulation Core (layout, environment, temperatures)
SE1 adapter → Presentation (copied bounds/temperatures, camera inputs, draw geometry)
Presentation → framework + VRage.Math only
Core → framework + VRage.Math only
```

The `ThermalVisionProbe` partials live under `Game/ThermalVision/` as SE1 adapter code: they read live
grids/nodes, supply scalar values and bounds to presentation helpers, schedule
refreshes and submit engine billboards. They do not change solver temperatures.
Their camera, HUD, input, physics and billboard APIs would need an SE2 adapter.
The presentation approximation is not a replacement simulation or authoritative
temperature source. Neither presentation nor SE1 rendering is needed to build or
run `Thermodynamics.Core`.

`CoreIsolationTests` checks both the core's assembly dependencies and the absence
of thermal-vision types inside it, and checks the presentation assembly has no
engine-runtime dependency. The current math dependency remains the documented
`VRage.Math` exception; SE2 uses different math types, so this is separation for
porting, not a claim of a drop-in SE2 build or finished SE2 support.

Within presentation, `ThermalVisionBlockField` indexes source heat and answers
temperature queries; `ThermalVisionSurfaceField` prepares interpolated surfaces,
transitions and near-camera caps. These are separate source files and share no
SE1 entity state. The performance tool references presentation directly and does
not reference the simulation assembly. Archived visual experiments remain available
alongside the SE1 adapter; grouping them does not enable or remove any lab mode.

> The rules argued here are stated canonically in [rules.md](rules.md): `C5` `P9`.

| Looking for | Go to |
| --- | --- |
| The equations the model evaluates | [thermal-model.md](thermal-model.md) |
| What the adapter samples from the world | [environment.md](environment.md) |
| Where the design is going | [scale-design.md](scale-design.md) |
| Building and deploying it | [development.md](development.md) |

## The model

| Area | Types |
| --- | --- |
| Layout | `GridModel`, `BlockInstance`, `BlockModel`, `BlockOrientation`, `CellSurface`, `BlockSurfaceBuilder`, `CoolantShape`, `IBlockAdjacency` |
| Simulation | `ThermalSimulation`, `ThermalSolver`, `ThermalNode`, `ThermalLink`, `SimulationScheduler`, `ThermalThresholds`, `EnvironmentSample` / `EnvironmentState` / `EnvironmentSolver`, `ISimulationProfiler` |
| Climate | `ClimateModel` (latitude, ground, lag), `WindField` (the wind map the game lacks), `TerrainHorizon` (ground shadowing the sun), `SolarOcclusionSampler` (where to cast occlusion rays from) |
| Shadow | `SunShadowMap` (a grid's own shadow, and its neighbours'), `VoxelWalk` (ray through a grid's cells, from any point) |
| Surfaces | `SurfaceMap`, `RoomMapper`, `RoomMap`, `RoomPortal`, `RoomAirNode`, `RoomPressure`, `SurfaceAudit` |
| Loops | `CoolantLoop`, `CoolantLoopBuilder` |
| Devices | `HeatPumpShape`, `HeatPumpDevice` |
| Definitions | `BlockThermalProperties`, `LoopThermalProperties`, `PlanetThermalProperties`, `GroundTemperature`, `ThermalSettings` |
| Storage | `ThermalStorageCodec` |
| Maths | `BoxGeometry`, `GridMath`, `Face`, `OcclusionMath`, `TemperatureScale`, `ThermalConstants` |

`ThermalSimulation` is the whole surface a host needs: `AddBlock`, `RemoveBlock`, `RefreshBlock`,
`RefreshBlockSealing`, `Update`, `StepExact`, `Save`, `Load`, `SetRoomPressure`, and read back node
temperatures, overheat events and threshold crossings.

## The adapter

| File | Type | Role |
| --- | --- | --- |
| [Session.cs](../Thermodynamics/Session.cs) | `MySessionComponentBase` | Loads settings, boots the Definition Extensions client, the HUD, the terminal controls and the mod API. Runs cross-grid conduction and the debug overlay; chat dispatch lives in `SessionCommands.cs`. |
| [PlanetManager.cs](../Thermodynamics/PlanetManager.cs) | `MySessionComponentBase` | Which planet is closest to a point. |
| [Game/ThermalGrid.cs](../Thermodynamics/Game/ThermalGrid.cs) | `MyGameLogicComponent` | The per-grid root: mirrors the game grid into a `GridModel`, owns the `ThermalSimulation`, wires block and grid events. `partial`, split across four more files. |
| [Game/ThermalGridSimulation.cs](../Thermodynamics/Game/ThermalGridSimulation.cs) | `partial` | The tick: sample, step, apply damage, raise crossings, refresh readouts. |
| [Game/ThermalGridEnvironment.cs](../Thermodynamics/Game/ThermalGridEnvironment.cs) | `partial` | Builds the `EnvironmentSample`: planet, air, wind, sun, occlusion, registered heat sources. |
| [Game/ThermalGridStorage.cs](../Thermodynamics/Game/ThermalGridStorage.cs) | `partial` | Save and load through the model's codec. |
| [Game/ThermalBlock.cs](../Thermodynamics/Game/ThermalBlock.cs) | class | One placed block bound to one solver node. Pushes power, thrust, door state and mass into the model by event. |
| [Game/ThermalBlockCatalog.cs](../Thermodynamics/Game/ThermalBlockCatalog.cs) | static | Block definition → `BlockModel`, once per definition per session. |
| [Game/ThermalCoolantShapes.cs](../Thermodynamics/Game/ThermalCoolantShapes.cs) | static | Subtype → coolant plumbing. |
| [Game/ThermalHeatPumpShapes.cs](../Thermodynamics/Game/ThermalHeatPumpShapes.cs) | static | Subtype → heat-pump faces and ratings. |
| [Game/ThermalHeatPumpBlock.cs](../Thermodynamics/Game/ThermalHeatPumpBlock.cs) | `MyGameLogicComponent` | The electrical half of a heat pump: the resource sink it draws through, and the switch that runs it. |
| [Game/HeatPumpDeviceLookup.cs](../Thermodynamics/Game/HeatPumpDeviceLookup.cs) | static | Reuses a per-pass identity lookup for electrical input and demand publication, avoiding a device-list scan per native pump. |
| [Game/ThermalBridges.cs](../Thermodynamics/Game/ThermalBridges.cs) | static | Conduction across a rotor or piston, between two grids. |
| [Game/ThermalHeatSources.cs](../Thermodynamics/Game/ThermalHeatSources.cs) | static | Registered point heat sources, and their irradiance at a grid. |
| [Game/ThermalGridScheduler.cs](../Thermodynamics/Game/ThermalGridScheduler.cs) | static | Gives every grid its share of every frame, so a step is spread rather than landed whole. |
| [Game/ThermalGridRoomDiagnostics.cs](../Thermodynamics/Game/ThermalGridRoomDiagnostics.cs) | `partial` | The room and surface dumps, and the comparison against the game's own sealing test. |
| [Game/ThermalCoolantPumpBlock.cs](../Thermodynamics/Game/ThermalCoolantPumpBlock.cs) | `MyGameLogicComponent` | A coolant pump's terminal switch and speed, replicated as a `NetSync<float>`. |
| [Game/HeatSourceCommand.cs](../Thermodynamics/Game/HeatSourceCommand.cs), [Game/HeatSourceMath.cs](../Thermodynamics/Game/HeatSourceMath.cs), [Game/ThermalHeatSourceDebug.cs](../Thermodynamics/Game/ThermalHeatSourceDebug.cs) | static | Driving and drawing point sources from chat, for exercising the API path in a session. |
| [Game/PlanetProbes.cs](../Thermodynamics/Game/PlanetProbes.cs) | static | The 72-point planet-wide climate and wind sweep. See [environment.md](environment.md#measuring-it). |
| [ThermalApi.cs](../Thermodynamics/ThermalApi.cs) | static | The mod-facing delegate table. See [api.md](api.md). |
| [ThermalTerminal.cs](../Thermodynamics/ThermalTerminal.cs) | static | Thermal readout in every block's terminal. |
| [ThermalHud.cs](../Thermodynamics/ThermalHud.cs) | static | Cockpit summary and extinguisher readout, on Rich HUD, plus the extinguisher billboard. |
| [ThermalDebugView.cs](../Thermodynamics/ThermalDebugView.cs) | static | The x-ray overlay: a coloured box per block, or per room cell, cycled with Ctrl+Shift+=. |
| [ThermalSettingsMenu.cs](../Thermodynamics/ThermalSettingsMenu.cs) | static | What the settings menu holds: pages, controls and write-back, generated from `Settings.Names()`. Opened with Ctrl+Shift+S. Owns the framework registration. |
| [ThermalSettingsWindow.cs](../Thermodynamics/ThermalSettingsWindow.cs) | `WindowBase` | The window the menu is drawn in, built from the framework's HUD elements rather than from terminal pages. See [configuration.md](configuration.md#the-settings-menu). |
| [ThermalDebugPanel.cs](../Thermodynamics/ThermalDebugPanel.cs) | static | The Rich HUD readout beside the overlay: per-view figures for the grid being drawn. |
| [Debug.cs](../Thermodynamics/Debug.cs) | static | The crosshair readout. |
| [Settings.cs](../Thermodynamics/Settings.cs) | partial class | Serialized schema, defaults, migration and model application. Access and persistence live in `SettingsAccess.cs` and `SettingsStorage.cs`. |
| [SettingsSync.cs](../Thermodynamics/SettingsSync.cs) | static | Replicates the world's settings to every client, over `SENetworkAPI`. |
| [SettingsRequests.cs](../Thermodynamics/SettingsRequests.cs) | static | An administrator changing a setting from a client, over the engine's secure handler. |
| [Game/ThermalGridSync.cs](../Thermodynamics/Game/ThermalGridSync.cs) | static | Replicates block temperatures to clients: the whole hull once, then the near-critical band. Plumbing only — the protocol is `Core/Sync`. |
| [WindOverlay.cs](../Thermodynamics/WindOverlay.cs) | static | The wind map and the crosshair wind needle, cycled with Ctrl+Shift+W. |
| [OverlayBudget.cs](../Thermodynamics/OverlayBudget.cs) | static | Fits the block overlay's draw radius to `DebugOverlayMaxBoxes` each frame. |
| [Definitions/](../Thermodynamics/Definitions) | classes | Typed readers over Definition Extensions. |
| [Telemetry/](../Thermodynamics/Telemetry) | static + records | Data collection. See [telemetry.md](telemetry.md). |
| [DefinitionExtensionsAPI.cs](../Thermodynamics/DefinitionExtensionsAPI.cs), [NetworkAPI/](../Thermodynamics/NetworkAPI), [RichHudFramework/](../Thermodynamics/RichHudFramework) | vendored | Third-party API clients. Do not edit; replace wholesale when upstream updates. |

## Update order

```
Session.Simulate()                      every frame
  ├─ chat command registration
  ├─ keybind poll                       client only, Ctrl+Shift+= overlay, Ctrl+Shift+S menu
  ├─ ThermalBridges.Update()            every 10th frame: conduction across rotors and pistons
  └─ Debug.ShowDebugInfo()              client only, behind DebugTextOnScreen

Session.Draw()                          client only
  ├─ ThermalHud.Draw()
  ├─ ThermalDebugView.Draw()            client only, off unless a mode is selected
  └─ ThermalDebugPanel.Update()         the readout beside it; sweeps the grid a few times a second

ThermalGridScheduler.Tick()             every frame, every grid
  ├─ Simulation.NeedsEnvironmentSample?  no → skip sampling entirely
  ├─ Sample()                           planet, air, wind, sun, occlusion, heat sources
  ├─ push heat pump state                switch and available power, before the step spends it
  ├─ Simulation.Update(dt, sample)
  │    ├─ settings revision check       rescale capacities, rebuild loops and room air
  │    ├─ topology rebuild              only after a block change
  │    ├─ room mapping                  one budgeted slice
  │    ├─ exposure + room air refresh   only after a mapping pass completes
  │    └─ solver.Step() × steps due     substepped for stability
  └─ AfterSteps()
       ├─ apply overheat damage         server only
       ├─ raise threshold crossings     to registered mods
       ├─ publish heat pump demand      what each pump wants to draw, into its resource sink
       ├─ mass sweep                    every 8 steps
       ├─ room pressure sweep           every 8 steps, grids with air vents only
       ├─ hottest block                 every 4 steps, and only if something will read it
       ├─ telemetry                     only while collection is on
       └─ debug colouring               only while a debug toggle is on
```

Every grid is visited every frame and does the share of its current step that one frame is of the
step's window, so the cost of a step is spread rather than landing whole on one frame. How often the
simulation *steps* is `ThermalSimulation.Update`'s business — it banks `StepWorkUnits × frameSeconds
× StepsPerSecond` of work credit each frame and spends it a slice at a time, so a step completes
after `Frequency × SimulationSpeed` steps' worth of frames and never more than one per frame.
`SimulationScheduler` counts completed steps and sizes the resumable passes' budgets, and holds no
step-credit of its own: one accumulator paces the simulation and it is the one above.

**The frame length is a constant sixtieth**, not measured real time, and `Session` runs on
`MyUpdateOrder.Simulation` — so **simulated time is counted in simulation ticks rather than in real
seconds**. A machine running below 1.0 sim speed executes fewer ticks per real second and its
thermal clock runs slow with the rest of its world, which is correct on one machine and a divergence
between two. The scheduler is driven from the session
component rather than from the grid entity, because `MyCubeGrid` clears `EACH_FRAME` from its own
update flags whenever its scheduled-work queue empties. See
[load-and-hitching.md](load-and-hitching.md#what-keeps-the-spike-proportional) for what the spreading is worth.

## Grid lifecycle

`ThermalGrid.Init` attaches:

| Event | Effect |
| --- | --- |
| `OnBlockAdded` | Resolves the model from the catalogue, builds a `BlockInstance`, adds a node, subscribes to power, thrust, door and attachment events, registers air vents and heat pumps. |
| `OnBlockRemoved` | Stores the temperature in `RecentlyRemoved`, unsubscribes, removes the node. |
| `OnGridSplit` | Copies temperatures out of the parent's `RecentlyRemoved` onto the child's blocks. |
| `OnGridMerge` | Copies temperatures from the absorbed grid onto matching positions, mapped through world space. |

`UpdateOnceBeforeFrame` disables the component when the grid has no physics (projections,
blueprints), adds any blocks that already existed, runs one full `RebuildAll`, and loads saved
temperatures. Building everything once is cheaper than replaying the incremental path per block, and
it leaves the room map complete before the first step rather than after it.

Blocks whose definition sets `ExcludeFromSimulation` never become nodes.

Queued overheat damage and threshold callbacks verify the original `BlockInstance` before delivery.
If another callback replaces a block at the same cell, its replacement does not receive the removed
block's queued damage or threshold crossing.

A block only tells the simulation something when it changes: power, thrust, door state and
attachment are all events. Mass is the exception — the game raises nothing a mod can hook for build
progress or damage — so it is swept every eight steps, alongside room pressure.

## Persistence

[Game/ThermalGridStorage.cs](../Thermodynamics/Game/ThermalGridStorage.cs) writes one
base64 blob into the grid's `MyModStorageComponent`, under the GUID registered in
[EntityComponents.sbc](../Thermodynamics/Content/Data/EntityComponents.sbc). `IsSerialized()` triggers the save.
Saving starts only after `UpdateOnceBeforeFrame` has added existing blocks, rebuilt the simulation
and loaded their stored temperatures. If native serialization runs earlier, the existing blob is
left intact instead of being replaced by an uninitialised simulation's empty state.
The installed engine permits this ordering: `MyEntities.CreateFromObjectBuilder` completes `Init`
before returning, while the first-frame callback is dispatched separately. An immediate
`GetObjectBuilder` call, including from an entity-added callback, serializes the component container
and calls `IsSerialized` before storage is copied into the object builder.

The codec's v2 format carries block, loop and room air temperatures, keyed by 64-bit position, by
the loop's own signature rather than by its index, and by the room's anchor cell — so a rebuilt loop
keeps its heat and distant blocks cannot alias. It reads v1, so old saves load, and it grows by
adding a section rather than by changing its marker, so a save written now still loads on a build
that predates the section.

Current block and held-coolant records match the block's minimum cell exactly, so a removed block's
record cannot attach to the interior of a larger replacement. Legacy block records keep their
occupying-cell lookup: the original writer used `SlimBlock.Position`, which can differ from `Min`
on rotated or multicell blocks. Neither format identifies a replacement at the same saved anchor.
Nonfinite temperature, fill and held-capacity records are skipped while valid records still load;
finite values retain their existing clamps. Direct room-temperature restoration applies the same
finite check. The save layout and public codec overloads are unchanged.

Room air loads onto rooms the map already holds, which is why `UpdateOnceBeforeFrame` runs
`RebuildAll` before `Load`. The restored air is marked initialised: pressurisation arrives later,
from the vent sweep, and filling a room for the first time is what would otherwise take its
temperature from the walls. Air that was never filled is not written at all — its figure is a
placeholder rather than a measurement. A room with a different anchor starts from its surfaces.
The format stores only the anchor, so a changed room that retains its lowest cell can still inherit
the saved temperature; the anchor is not a full room-shape identity.

## Definition loading

Thermal properties are declared inside `<ModExtensions>` groups and read through Draygo's Definition
Extensions API. `ThermalBlockCatalog` reads them **once per block definition** and builds a
`BlockModel` every placed block of that type shares: size, mass, thermal properties, per-cell
surface bits from the definition's airtightness table and mount points, and coolant plumbing.
Placing a block is then a dictionary hit and a rotation. See [definitions.md](definitions.md).

## Networking

The simulation itself is not replicated. Clients run their own `ThermalGrid` components from the same
inputs and reach their own temperatures; only the server applies overheat damage, so the one
conclusion with a world-visible consequence is never reached twice.

**Their answers are corrected rather than their work replaced.** A client re-simulating from its own
inputs is measurably on the wrong side of a block's critical temperature for longer than the damage
event lasts, so the server states the truth over the top of it — the whole hull once when the client
has built the grid, then the blocks near failing on an interval. The decision of what to send and
when is in `Core/Sync` and is game-free; `ThermalGridSync` is the part that needs a session.

What *is* replicated travels two ways, and the split is deliberate:

| Channel | Carries | Why this channel |
| --- | --- | --- |
| `SENetworkAPI`, `30323` | The world's settings ([SettingsSync.cs](../Thermodynamics/SettingsSync.cs)) and the two block throttles — a coolant pump's speed and a heat pump's power limit | Ordinary state, server → client, seeded at construction so a joining client's fetch is answered |
| The engine's secure handler, `30324` | An administrator's request to change a setting from a client ([SettingsRequests.cs](../Thermodynamics/SettingsRequests.cs)) | SENetworkAPI's sender id is a field the sender wrote; a promote-level check cannot be gated on it |
| The engine's secure handler, `30325` | Block temperatures, server → client, and a client's request for a grid's hull ([Game/ThermalGridSync.cs](../Thermodynamics/Game/ThermalGridSync.cs)) | The same reason one channel along: a client must not be able to write temperatures onto another client's simulation, and the from-the-server flag is the only thing that says so. A second id rather than a second handler on `30324`, because one id registered twice delivers every message twice |

See [configuration.md](configuration.md#changing-settings-from-a-client) for the request path,
[configuration.md](configuration.md#replicating-temperatures) for the temperature protocol, and
[known-issues.md](known-issues.md#open-defects) for what it does and does not reach.

## Extending it

Other mods bind to the delegate table in [ThermalApi.cs](../Thermodynamics/ThermalApi.cs);
see [api.md](api.md). A host that wants to drive the model directly implements `IBlockAdjacency` and
pumps `ThermalSimulation` — which is exactly what the test harness does.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-10-28 | Resolve heat-pump bindings once per adapter pass with exact reference identity and cleared scratch ownership; keep the public mutable Core query unchanged. |
| 2026-10-28 | Validate original block identity before applying queued overheat damage, matching threshold callback ownership. |
| 2026-10-28 | Skip nonfinite persistence records, enforce exact current-format block anchors while retaining legacy reference-cell restores, and document the anchor-only room identity limit. |
| 2026-10-28 | Preserve stored temperatures when native serialization precedes initial block loading. |
| 2026-10-01 | Map session, settings, solver, telemetry and thermal-vision responsibilities after the refactor. |
| 2026-10-01 | State production, test-fixture and external-provider ownership separately. |
| 2026-09-19 | Grouped SE1 thermal vision under Game, split heat lookup from surface preparation, and removed the performance tool's unused model reference. |
| 2026-09-19 | Separated thermal-vision presentation from the simulation model, with an independent project and assembly isolation checks. |
| 2026-09-01 | Added `ThermalSettingsWindow`, and said what the settings menu file now is: the menu moved out of the Rich HUD terminal into a window this mod draws. |
| 2026-08-25 | Said what `SimulationScheduler` holds rather than what it no longer holds (`R12`). The removed accumulator is a revision and belongs in a change log, which is where `F23` recorded it. |
| 2026-08-24 | Corrected the update order and the pacing paragraph. The tick calls `Simulation.NeedsEnvironmentSample`, not `scheduler.WouldStep`, and step pacing is `ThermalSimulation.Update`'s work credit rather than `SimulationScheduler`'s — whose parallel step-credit accumulator no shipped path called and has been removed. Added what the constant frame length means: simulated time is counted in simulation ticks, so a machine below 1.0 sim speed has a thermal clock that runs slow ([backlog.md](backlog.md) `F23`). |
| 2026-08-23 | Added the temperature replication: a third channel, a component in the adapter table, and a correction to the **Networking** claim that clients simply reach their own answers. They still do; the server now states the truth over the top of it. |
| 2026-08-22 | Corrected two statements this page had gone on making after the code stopped supporting them. **Networking** said no `NetSync` property and no command was registered and that nothing replicates; three properties and a second, secure channel exist, and the section now says what each carries and why the split. **Update order** filed the per-grid block under "every 10th frame" while naming the scheduler that runs every grid every frame two lines below. Completed the adapter table, which named 22 of the 33 files under `Data/Scripts/Thermodynamics` — the scheduler, the settings sync and request paths, the room diagnostics, the coolant pump block, the wind overlay, the overlay budget, the planet probes and the three heat-source debug files were all absent. |
| 2026-08-22 | Added the standard header and this change log. |
| 2026-08-20 | Brought the page onto the blueprint-running path and the publishable mod folder. |
| 2026-08-18 | Spread a step across the frames of its window rather than landing it whole on one frame, and documented the scheduler that does it. |
| 2026-08-17 | Documented the Rich HUD readouts, the settings menu and the x-ray block overlay that replaced thermal vision. |
| 2026-08-12 | Opened the page on the rebuilt mod: the model and the adapter, and the three boundaries between them. |
