# Engine notes

What the two Space Engineers engines actually provide: the SE1 APIs this mod either reimplements by
hand or could be taking advantage of, and what an SE2 adapter would bind to.

**Every SE1 API quoted here is compile-verified.** A probe file using all of it builds at
`LangVersion 6` / `net48` against the real DLLs, so the namespaces, accessibility and signatures are
correct as written; where *behaviour* rather than existence is in question, that is called out.
**Everything in the SE2 half marked Verified was read by reflection** over the real assemblies with
`System.Reflection.MetadataLoadContext`; everything marked Inferred is reasoning from shipped
content and is flagged where it matters.

| Looking for | Go to |
| --- | --- |
| The design these findings feed | [scale-design.md](scale-design.md) |
| What the mod builds against, and how | [development.md](development.md) |
| The equations these APIs supply inputs to | [thermal-model.md](thermal-model.md), [environment.md](environment.md) |

---

# Part 1 — Space Engineers 1

Surveyed in `~/Steam/SteamLibrary/steamapps/common/SpaceEngineers/Bin64`.

## Build corrections

The mod compiles against the real assemblies with **zero warnings**. Two things were wrong in the
mod project as it first stood:

| Problem | Fix |
| --- | --- |
| `VRage.Native.dll` is an **unmanaged** PE — referencing it produced `MSB3246: PE image does not have metadata`. | Reference removed. |
| The project targeted `net472`, but `VRage.Platform.Windows` (and its RestSharp dependency) target **.NET Framework 4.8**, so they never resolved. | `TargetFramework` raised to `net48`. |

Reference points:

* Game assemblies live in `Bin64`; 84 DLLs, of which the managed ones are `VRage*`, `Sandbox*`
  and `SpaceEngineers*`.
* XML documentation ships for `Sandbox.Common`, `Sandbox.Game`, `SpaceEngineers.Game`,
  `VRage`, `VRage.Game`, `VRage.Library`, `VRage.Math` and `VRage.Scripting` — worth pointing
  your IDE at, it gives IntelliSense docs for the whole modding surface.
* `Pulsar/` alongside `Bin64` is a third-party loader, not part of the game's own assemblies.
  Build against `Bin64`.
* Mod scripts are compiled by `VRage.Scripting.MyScriptCompiler` against a whitelist with three
  targets (`MyWhitelistTarget.Ingame`, `.ModApi`, `.Both`). Everything below is `ModApi`
  surface — it is reachable from a mod's scripts, unlike the in-game programmable block sandbox.

---

## The game already computes airtight rooms — **highest value**

`ThermalGridMapper` is 725 lines that classify every cell as external, sealed structure, or an
interior room, restarting the whole flood fill on every block change. The engine already does
this for pressurisation, incrementally, and exposes it on the mod API:

```csharp
IMyGridGasSystem gas = grid.GasSystem;              // VRage.Game.ModAPI.IMyCubeGrid

List<IMyOxygenRoom> rooms = new List<IMyOxygenRoom>();
gas.GetRooms(rooms);

Vector3I cell = block.Position;
IMyOxygenRoom room = gas.GetOxygenRoomForCubeGridPosition(ref cell);   // null outside any room

bool sealed_    = room.IsAirtight;
float oxygen    = room.OxygenLevel(grid.GridSize);
float outside   = room.EnvironmentOxygen;
HashSetReader<Vector3I> cells = room.Blocks;

gas.OnProcessingDataComplete += RefreshExposure;    // same signal as SurfaceCheckComplete
bool busy = gas.IsProcessingData;
gas.ForcePressurize = true;
```

The engine's own airtightness test is public too — the logic
`CalculateBlockSurfaceStates` reimplements, including every door special case:

```csharp
bool airtight = MyGridGasSystem.IsAirtightBlock(block, cell, normal);   // Sandbox.Game.GameSystems
```

What this could replace: the surface bit flags, the external flood fill, the room detection, and
the per-block `BeginCrawl` restart that is finding **P1** in
[known-issues.md](known-issues.md). `GetOxygenRoomForCubeGridPosition` is an O(1)
lookup where the mod currently maintains its own dictionary and queue machinery.

It also unlocks something the mod cannot currently model at all: `OxygenLevel` and
`EnvironmentOxygen` per room mean **interior air can have a temperature and convect**, instead of
sealed interior faces simply dropping out of the exposed count.

**Verify in game before committing to it:**

* Whether room data is populated when `EnableOxygenPressurization` is off in world settings, and
  exactly what `ForcePressurize` does about that. There must be a fallback — the extracted
  `RoomMapper` in `Core/` is that fallback, and it is already tested.
* Whether "not in any room" reliably means "open to space" for the exposed-surface rule. The
  gas system cares about pressure, the thermal model cares about line of sight to vacuum; they
  agree in the normal case but may differ for open lattice blocks.

## Parallelism is available to mods

`ThermalGridSimulation.ProcessCellsSequentially` carries the comment *"parallel processing not
available in Space Engineers"*. That is not correct:

```csharp
MyAPIGateway.Parallel.For(0, nodeCount, i => { /* ... */ });
MyAPIGateway.Parallel.Start(work, completionCallback);
MyAPIGateway.Utilities.InvokeOnGameThread(action, "Thermodynamics", 0, 1);
```

`VRage.Game.ModAPI.IMyParallelTask` offers `For`, `ForEach`, `Do` and `Start`, backed by
`ParallelTasks` in `VRage.Library`.

This matters because of how the rewritten solver works: it accumulates watts from
start-of-step temperatures and applies them together, so the pass has no order dependence and is
safe to split across threads. Reads must not race with the game mutating the grid, so the natural
split is *solve in parallel, apply on the game thread* via `InvokeOnGameThread`.

**Measured, and the hand-off is not the obstacle.** Fanning out over 242 work items and joining
costs 1.6–6.8 µs a step against a 1,004-node grid's own 0.54 ms, so a single grid stepped through
the fan-out comes back at 0.99× — and a 242-grid fleet at 10.17× on 32 threads, 7.09× on eight.
The 0.128 ms figure is real and is still three hundred times a hand-off. See
[scale-design.md](scale-design.md#one-grid-per-thread-measured-and-built).

## Planets already describe their own climate

[Data/Planets.xml](../Thermodynamics/Content/Data/Planets.xml) defines only the `DefaultThermodynamics` fallback, so
**every planet in the game is treated as Earthlike** — 283–294 K, `SolarDecay` 0.5. The vanilla
planet definitions already say otherwise:

| Planet | `DefaultSurfaceTemperature` | `SolarRadiationProtectionFactor` | `Atmosphere.Density` | `MaxWindSpeed` |
| --- | --- | --- | --- | --- |
| EarthLike | *(unset)* | 1.8 | 1.0 | 80 |
| Alien | *(unset)* | 1.8 | 1.2 | 80 |
| Mars | *(unset)* | 0.2 | 1.0 | 80 |
| Europa | ExtremeFreeze | 0 | *(none)* | 30 |
| Triton | ExtremeFreeze | 1.8 | 1.0 | 80 |
| Pertam | Hot | 0.75 | 1 | 80 |

All reachable at runtime:

```csharp
MyPlanetGeneratorDefinition generator = planet.Generator;      // Sandbox.Definitions

MyTemperatureLevel level   = generator.DefaultSurfaceTemperature;   // ExtremeFreeze .. ExtremeHot
float solarProtection      = generator.SolarRadiationProtectionFactor;
float surfaceGravity       = generator.SurfaceGravity;

MyPlanetAtmosphere air     = generator.Atmosphere;
float density              = air.Density;
float maxWind              = air.MaxWindSpeed;
float oxygenDensity        = air.OxygenDensity;
bool breathable            = air.Breathable;
float limitAltitude        = air.LimitAltitude;

double altitude            = planet.GetHeightFromSurface(position);   // for the unfinished
float localDensity         = planet.GetAirDensity(position);          // underground/core model
float windSpeed            = planet.GetWindSpeed(position);
float localOxygen          = planet.GetOxygenForPosition(position);
```

The obvious move: derive `PlanetThermalProperties` from the planet's own definition, and let a
`ThermalPlanetProperties` ModExtensions group override it. Pertam runs hot and Europa freezes
without anyone authoring a line of XML, and modded planets get sensible defaults for free.

`SolarRadiationProtectionFactor` is the engine's version of the mod's `SolarDecay`.
`GetHeightFromSurface` is what the underground model needed and now uses; the `//TODO: implement
underground core temparatures` this section was written against is gone, and what the model does
below the surface is in [environment.md](environment.md#underground). What is still open there is
`A16` — a buried grid convects with the planet's air coefficient, because rock contact is not
modelled.

## Block mass and build state can be tracked

**Mass is tracked now.** This section was written against a `ThermalCell.PrecalculateVariables` that
read `Block.Mass` once at construction, so a half-built or heavily damaged block kept the thermal
mass of a complete one. A mass rota has since closed it: `ThermalBlock.RefreshMass` re-reads
`Block.Mass` and calls `ThermalNode.RefreshThermalMass` when it moves, on a sweep every eight steps,
and because the engine's own `Mass` counts installed components a welding block warms up as it is
built. What is *not* read is the rest of the group below — build ratio, integrity and damage never
reach the model, so a shot-up block conducts and radiates as an intact one of the same mass.

```csharp
float mass       = block.Mass;
float buildRatio = block.BuildLevelRatio;   // 0..1 while welding
float integrity  = block.Integrity;
float maxIntegrity = block.MaxIntegrity;
float damageRatio  = block.DamageRatio;
bool destroyed     = block.IsDestroyed;
```

with the event to hang it on:

```csharp
grid.OnBlockIntegrityChanged += slim => node.RefreshThermalMass();
```

`ThermalNode.RefreshThermalMass()` in `Core/` already exists for exactly this; it just needs
wiring.

## Damage should go through the damage system properly

The mod calls `Block.DoDamage(amount, hash, sync: false)`, so every machine computes and applies
its own damage independently. The full signature supports server-authoritative damage:

```csharp
block.DoDamage(amount, MyStringHash.GetOrCompute("Thermal"), sync: true, hitInfo: null, attackerId: 0L);
```

and the session exposes handlers so *other* mods can see, scale or veto thermal damage:

```csharp
IMyDamageSystem system = MyAPIGateway.Session.DamageSystem;
system.RegisterBeforeDamageHandler(0, (object target, ref MyDamageInformation info) => {
    if (info.Type == MyStringHash.GetOrCompute("Thermal")) info.Amount *= 0.5f;
});
system.RegisterAfterDamageHandler(0, (target, info) => { });
system.RegisterDestroyHandler(0, (target, info) => { });
```

Registering a documented `Thermal` damage type is what lets shield and armour mods interoperate
with this one instead of fighting it.

## Grid events that coalesce

The mod restarts its room crawl from `OnBlockAdded` / `OnBlockRemoved`, i.e. once per block. The
engine offers a settled-state event:

```csharp
MyCubeGrid.OnBlocksChangeFinishedGlobally += (a, b) => { };   // note: static, fires for all grids
grid.OnConnectionChangeCompleted += (g, link) => { };          // better than piston/rotor hooks
grid.OnGridBlockDamaged += (slim, damage, hit, attacker) => { };
```

`OnBlocksChangeFinishedGlobally` being static means one subscription for the whole session, with
the affected grids passed as arguments — a natural place to trigger one coalesced rebuild per
grid per batch of changes.

## Cheaper update cadence

`ThermalGrid.Init` requests `MyEntityUpdateEnum.EACH_FRAME` for every grid, so every grid runs
scheduling logic 60 times a second to perform 4 solver steps. The enum has coarser options:

```csharp
NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;    // 6 Hz, enough for Frequency = 4
// also: EACH_100TH_FRAME, EACH_FRAME_AFTER, SIMULATE, BEFORE_NEXT_FRAME
```

At the default `Frequency = 4` a grid needs servicing 4 times a second; `EACH_10TH_FRAME` gives 6.
That is a 10× cut in per-grid dispatch overhead for free, and it composes with the
`SimulationScheduler` in `Core/`, which already accumulates fractional step credit and therefore
does not care how often it is polled.

## Terminal readout without a HUD dependency

The mod requires Rich HUD Master for all of its on-screen text. Per-block information has a
first-party route:

```csharp
terminalBlock.AppendingCustomInfo += (b, sb) => sb.Append("Temperature: 412 C\n");
terminalBlock.RefreshCustomInfo();
```

That puts temperature in the terminal detail panel for any functional block, with no dependency —
which is why the terminal readout works in a world with no HUD framework at all. It is also the only
route that takes a paragraph: `IMyTerminalControlTextbox` is a one-line editable field and clips
anything longer, whatever its content. The framework is
still the right tool for the always-on cockpit overlay, but the per-block readout does not need it.

`MyAPIGateway.TerminalControls` is also available if you want a per-block thermal limit slider or
a "vent coolant" action.

## The Definition Extensions dependency is optional

Mods can register their own definition types, so the thermal properties could be read from the
mod's own SBCs without a third-party mod in the load order:

* `VRage.Game.Definitions.MyDefinitionTypeAttribute`
* `VRage.ObjectBuilders.MyObjectBuilderDefinitionAttribute`

The ModExtensions approach has a real advantage though — *other* mods can annotate their blocks
without depending on this one — so this is a trade, not a straight win. Worth noting that a hard
dependency currently means a missing Definition Extensions install throws inside
`ThermalCellDefinition.GetDefinition` rather than degrading.

## Richer weather data

**Partly taken up.** The mod now uses `MyVisualScriptLogicProvider.GetWeather(position)`, which
returns the effect's subtype name and is on the same whitelisted class as the intensity call it
already used. That is enough to distinguish a dust storm from rain, and
[WeatherResponse](../Thermodynamics/Core/Definitions/WeatherResponse.cs) turns the name
into a temperature offset, a solar multiplier, a wind multiplier and a convective multiplier.

The response table's figures are **transcribed** from Keen's `WeatherEffects.sbc`, not read from it.
The definition type is there —

```csharp
Sandbox.Definitions.MyWeatherEffectDefinition   // Sandbox.Game.dll
  .WindOutputModifier / .SolarOutputModifier / .TemperatureModifier / .OxygenLevelModifier
```

— and reading it through `MyDefinitionManager` would pick up modded weathers' own authored numbers
instead of falling back on a word match. It was not done because a whitelist rejection is a
*compile* failure at world load rather than a catchable exception, so there is no way to try it and
degrade gracefully. Worth confirming in a throwaway mod before adopting.

`MySectorWeatherComponent` has more still, including the game's own resolved multipliers and the
sun's rotation period:

```csharp
MySectorWeatherComponent.GetSolarMultiplier(position);        // instance
MySectorWeatherComponent.GetTemperatureMultiplier(position);  // instance
MySectorWeatherComponent.GetWindMultiplier(position);         // instance
MySectorWeatherComponent.RotationInterval                     // the sun's period
```

All instance members on a session component in `Sandbox.Game.SessionComponents`, with the same
whitelist question. `RotationInterval` is the interesting one: it would let `AmbientLagSeconds` be
expressed as a share of a day rather than as absolute seconds, which is the outstanding defect in
[environment.md](environment.md#limits-and-open-questions).

---

## Things that are not there

* **No engine-side temperature.** `MyTemperatureLevel` is a five-value classification for
  planet definitions, not a simulation. Nothing in the game tracks block temperature; this mod
  remains the source of truth.
* **No generic per-block emissive helper on the mod API.** `SetEmissiveState*` exists on concrete
  `MyCubeBlock` subclasses in `Sandbox.Game`, not on `IMyCubeBlock`. Making hot blocks glow
  instead of repainting them needs either the concrete types or a billboard overlay like the
  extinguisher tool and the block overlay already draw. `ColorBlocks` is no longer used anywhere
  in the mod: it overwrote players' paint permanently.
* **`MySessionComponentBase` has no 10th/100th-frame hooks** — only
  `UpdateBeforeSimulation`, `Simulate`, `UpdateAfterSimulation` and `Draw`. The coarser cadences
  are entity-component only, via `MyEntityUpdateEnum`.

---

## Re-running the survey

The survey used a throwaway reflection tool over `Bin64` built on
`System.Reflection.MetadataLoadContext`, plus a probe file compiled against the real DLLs. To
repeat it, point `MetadataLoadContext` at every DLL in `Bin64`, load the `VRage*` / `Sandbox*` /
`SpaceEngineers*` assemblies, and enumerate. The XML documentation files next to those DLLs cover
most of the same ground if you would rather read than reflect.

The single most useful habit: before hand-rolling a subsystem, grep the ModAPI namespaces
(`VRage.Game.ModAPI`, `Sandbox.ModAPI`) for it. The room mapper is 725 lines of code that the
engine was already computing.

## Entity updates are NOT staggered across frames — measured

`ThermalGrid` polls on `UpdateBeforeSimulation10`, and the question is whether every grid in the
world lands on the same frame.

**They do.** A field run of a 203-grid save measured it directly: of 13,915 frames, **1,392 did
any thermal work at all** — one in ten, exactly — and every one of the worst frames reports **183
grids** on it. Those frames averaged 117 ms with a worst of 611 ms, and two in three exceeded a
60 fps frame. See the frame cost section of [telemetry.md](telemetry.md#frame-cost-and-hitching)
for where those numbers come from.

> **An earlier version of this section said the opposite**, on the strength of reading
> `MyDistributedTypeUpdater<MyEntity>(10)` in `VRage.Library`: it computes
> `m_step = ceil(Count / UpdateInterval)` and its enumerator walks `[m_updateIndex, m_updateIndex +
> m_step)`, which is a stagger. Whatever that machinery does in this build, the observable
> behaviour of `UpdateBeforeSimulation10` on grid entities is that they all fire together, and the
> measurement is what counts. The reading was plausible and wrong, and it was nearly the basis for
> deciding no fix was needed.

The mod therefore paces its own grids: see `ThermalGridScheduler`. Every grid ticks on every
frame, and each does its share of the solver step it is part way through — a fifteenth of it at
`Frequency 4`. Bucketing was the first answer and was not enough: it spreads the grids across the
cycle but not the work inside any one of them, so a single large grid on its own frame stayed a
stutter. Spreading the step itself subsumes it, and the buckets are gone.

## `MyCubeGrid` clears `EACH_FRAME` from its own update flags

The obvious way to pace a component per frame is to ask the entity for `EACH_FRAME` and gate on a
phase inside the callback. On a cube grid that does not work:

```csharp
base.NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;      // MyCubeGrid, on its own schedule
```

`MyCubeGrid` drives its scheduled work off `EACH_FRAME` and **clears the flag when its queue
empties**, re-arming it only when the queue goes from empty to non-empty. A mod hanging its
cadence on that flag stops running, silently, whenever the grid has nothing of its own to do.

This is the same property recorded under "never assign `NeedsUpdate`" in
[known-issues.md](known-issues.md) — that entry is about not *clearing* the grid's flags, and this
is the other half: not *depending* on them either. Anything that must happen on a schedule the mod
controls belongs on the session component's per-frame call, which nothing else edits.


---

# Part 2 — Space Engineers 2

Surveyed in `~/Steam/SteamLibrary/steamapps/common/SpaceEngineers2/Game2`, gathered for the question
"can the thermal model serve both games". Version stamp seen in shipped `.def` files: **Game2
2.0.1.2232** (some older assets say 2.0.1.1811).

## Platform

| | SE1 | SE2 |
| --- | --- | --- |
| Runtime | `net48`, `LangVersion 6` (script compiler) | **`net9.0`** |
| Math namespace | `VRageMath` (VRage.Math.dll) | **`Keen.VRage.Library.Mathematics`** (VRage.Library.dll) |
| Integer vector | `Vector3I` | `Vector3I` — same name, different namespace |
| Architecture | `MyGameLogicComponent` on entities | **DCS**, Keen's ECS: `Keen.VRage.DCS.Components.Entity` + `GameComponent` |
| Grid type | `MyCubeGrid` | `CubeGridComponent` |
| Block type | `IMySlimBlock` / `MyCubeBlock` | `CubeBlockComponent` (a component on an `Entity`) |
| Mod API | `Sandbox.ModAPI` + whitelist | **none public yet** |

There is no `VRage.Math.dll` in SE2 at all.

### Modding status

Only UGC *management* types are public (`Keen.Game2.Client.UI.Shared.UGC.Mods.*` — view models
and configuration). `VRage.Scripting` contains `ScriptWhitelist`, `IScriptWhitelistProvider`,
`WhitelistDiagnosticAnalyzer` and `ScriptCompiler`, so a scripting surface is clearly planned,
but nothing equivalent to `Sandbox.ModAPI` exists today. **An SE2 adapter cannot be written
yet.** This does not affect the model design, only when it can be hosted.

---

## Blocks live on an integer lattice — but at many scales

This is the finding that drives the redesign.

### Verified

`CubeBlockComponent`:

```csharp
BoundingBoxI      AABB;                     // integer AABB in grid space
IntegerOrientation BlockOrientation;
BoundingBox       LocalBounds;              // float
CubeBlockDefinition Definition;
CubeGridComponent Grid;
float             AbsoluteHealth, HealthIntegrity, EffectiveIntegrity;
float             BuildProgress, EffectiveBuildProgress;
float             EffectivePermeability;    // note: float, not bool
bool              Generated;
```

`CubeBlockDefinition`:

```csharp
float                             Mass;
BoundingBoxI                      BoundingBox;
ImmutableArray<BoundingBoxI>      OccupiedGridCellsGroups;   // cells as BOXES, not a cell list
BlockSizeDefinition               RelativeBlockSize;
bool                              Permeability;
float                             GetBlockPermeability(float buildProgress);
ListDictionaryReader<Base6Directions.Direction, MountPointsGroupData> MountPointsGroupsPerDirection;
float                             DisplaySize;
float?                            DisplaySizeOverride;
CubeBlockDensityDefinition        Density;
CubeBlockFragilityDefinition      Fragility;
float                             DeformationResistance, DestructionThreshold, WeakThreshold;
OccupiedGridCellsEnumerator       GetTransformedOccupiedCellGroups(RelativeTransform);
(BoundingBoxI, IntegerOrientation) ComputeBlockBoundsAndRotation(RelativeTransform);
```

`BlockSizeDefinition.RelativeBlockSize` is an **`Int32`**. Four are shipped, in
`GameData/Vanilla/Content/UI/Screens/GScreen/BlockSizes/`, with values **1, 2, 3, 4**. These are
size *tiers* (linear, not powers of two).

### Inferred

`GameData/Vanilla/Content` groups blocks into directories named for a number, and those numbers
line up with centimetres:

| Directory | Block defs | Reading |
| --- | --- | --- |
| `25` | 4 | 0.25 m |
| `50` | 47 | 0.5 m |
| `100` | 1 | 1.0 m |
| `125` | 8 | 1.25 m |
| `150` | 1 | 1.5 m |
| `250` | 102 | 2.5 m |
| `350` | 3 | 3.5 m |
| `500` | 14 | 5.0 m |

(Armour, for example, ships in `Armors/Half Long Slope/250/` and `.../50/` — the same 2.5 m and
0.5 m as SE1's large and small grid, plus six more sizes.)

The GCD of that set is **25 cm**, so a single common lattice exists at 0.25 m. `AABB` and
`OccupiedGridCellsGroups` being `BoundingBoxI` is consistent with all blocks being expressed on
one lattice.

*Not verified:* that the lattice really is 0.25 m rather than each `RelativeBlockSize` tier
having its own coordinate space, and whether all eight sizes can genuinely coexist on one grid.
`GameData` also contains a stray `"CellSize": 64` whose meaning was not tracked down (likely
voxel or texture, not block lattice). **Confirm both in game before committing.**

### Why it matters

If the lattice is 0.25 m, a 5 m block spans **20 × 20 × 20 = 8 000 lattice cells**. The current
model stores one dictionary entry per occupied cell in `GridModel.blocksByCell` *and* one in
`SurfaceMap`'s cell table, and `BlockInstance` materialises a `Vector3I[]` of every cell it occupies.
One 5 m block would cost 16 000 dictionary entries and an 8 000-element array.

**Per-cell enumeration is not survivable in SE2.** That is the single constraint the redesign has
to answer.

Note also `OccupiedGridCellsGroups` is an array of *boxes*, not a cell list — Keen made the same
call. The engine never enumerates a block's cells either.

---

## The octree

`Keen.Game2.Simulation.WorldObjects.CubeGrids.BlockOctrees.BlockOctreeComponent`
— `Component`, implements `ICubeBlockStorage` and `IInSceneListener`.

```csharp
// ICubeBlockStorage
bool  IsPositionEmpty(Vector3I);
bool  IsAreaEmpty(BoundingBoxI);
void  GetCubeBlocks(BoundingBoxI, BufferReference<CubeBlockComponent>);
void  GetConnectedCubeBlocks(BoundingBoxI, Vector3I, CubeBlockComponent, BufferReference<CubeBlockComponent>);
CubeBlockComponent TryGetCubeBlock(Vector3I);
Span<CubeBlockComponent> GetAllCubeBlocks();
BoundingBoxI Boundary { get; }
int CubeBlockCount { get; }
```

Internals worth knowing:

```csharp
FreeList<BlockOctreeNode>   _nodes;
FreeList<CubeBlockComponent> _cubeBlocks;
FreeList<BlockShape>        _largeIndex;
UnionFind                   _disconnectedGroups;
HashSet<ulong>              _dirtyEdges;        // Pack2NodesAndDirection(int, int, Direction)
HashSet<int>                _dirtyNodes;

struct BlockOctreeNode { bool IsEmpty; int LeafData; int FirstChild; /* packed in TheeOneInt */ }
struct BlockShape       { int Block; int Shape; }   // leaf → block index + shape index

bool HasBlockConnection(Vector3I, Base6Directions.Direction);
bool CanTraverse(Vector3I, Base6Directions.Direction);
void ComputeConnectivity(int);
void UpdateDirtyNeighborConnections();
```

Two things follow.

**The octree is a spatial index, not a replacement for the lattice.** Queries are still
`Vector3I` and `BoundingBoxI`. The tree exists so that a 5 m block is *one leaf* instead of 8 000
cells, and so range queries do not scan empty space.

**The engine already maintains a block adjacency graph.** `ComputeConnectivity`,
`TryConnectNodes`, `GroupFaceSharingNeighbors`, `_dirtyEdges` keyed on (nodeA, nodeB, direction),
union-find for split detection — that is structurally the same graph `ThermalSolver.links` is.
`HasBlockConnection(cell, direction)` and `GetConnectedCubeBlocks(...)` expose it.

The mod should **query** that graph, not rebuild one.

---

## Grid-level API

```csharp
// CubeGridComponent
ICubeBlockStorage CubeBlockStorage { get; }
Entity            GetBlock(Vector3I);
WorldTransform    GetWorldTransform(Vector3I);
bool              IsAreaEmpty(BoundingBoxI);
void              CommitBlockChanges();
void              RemoveBlock(CubeBlockComponent);
void              BlockDestroyed(CubeBlockComponent);
void              BlockHealthChanged(CubeBlockComponent);
void              BlockBuildProgressChanged(CubeBlockComponent);
void              BlockColliderChanged(CubeBlockComponent);
void              GridsConnectionChanged(CubeGridComponent, CubeGridComponent,
                                         ImmutableArray<ConnectionGroupDefinition>, bool);
IEnumerable<Entity> GetAllGridsInShip();
void VisitAllBlocksWithComponent<T, TVisitor>(ref TVisitor, bool);
void VisitAllBlocksWithComponent<T>(Action<T>, bool);
```

Compared with SE1 this is strictly better for this mod:

* `CommitBlockChanges` is the batch-settled signal. SE1 finding **P1** (room map restarts once per
  block) exists because SE1 has no such hook; SE2 hands it over.
* `BlockHealthChanged` / `BlockBuildProgressChanged` are exactly what
  `ThermalNode.RefreshThermalMass()` needs, and SE1 only offers `OnBlockIntegrityChanged`.
* `GridsConnectionChanged` replaces the per-piston/per-rotor `AttachedEntityChanged` hooks.

`VisitAllBlocksWithComponent<T, TVisitor>(ref TVisitor, bool)` — a **by-ref struct visitor** — is
the house idiom. SE2 code is allocation-averse throughout (`FreeList<T>`, `PooledList<T>`,
`BufferReference<T>`, `Span<T>`, `ListReader<T>`). A mod that allocates per block per frame will
look wrong there.

---

## Permeability replaces airtightness

SE1 airtightness is a boolean per face, and `ThermalGridMapper` reimplements the engine's rule.

SE2 has `CubeBlockDefinition.Permeability` (bool), `GetBlockPermeability(float buildProgress)`
(float), and `CubeBlockComponent.EffectivePermeability` (float). Sealing is **continuous and
build-progress dependent** — a half-welded wall is partly leaky.

For the thermal model this is an opportunity rather than a problem: an exposure *fraction* per
face is strictly more general than an exposed/not-exposed bit, and it degrades to the SE1 boolean
by treating it as 0 or 1. Designing the model around a float exposure factor costs nothing in SE1
and buys SE2 for free.

---

## What is tested today

`Se2LatticeTests` and `CoreIsolationTests` hold the SE2-facing properties, so a change that breaks
them fails the build rather than being discovered when an adapter is written.

| Test | Property |
| --- | --- |
| `TheCoreReferencesNothingButMathsAndTheFramework` | the simulation depends on `VRage.Math` and the framework, nothing else |
| `NoPublicApiInTheCoreSpeaksAGameType` | and no game type reaches its public surface indirectly either |
| `AHostCanDriveTheSimulationThroughTheCoreAlone` | layout in, sample in, temperatures out — written the way an adapter would write it |
| `GeometryIsAnsweredFromBoundsAndNotFromCells` | contact, face, surface and depth for a pair of 64-million-cell boxes, exact and in under a millisecond |
| `ContactBetweenDifferentSizesIsTheOverlapAndIsSymmetric` | a 0.5 m block on a 5 m face shares four cells, both ways round |
| `EveryBlockSizeSe2ShipsCoexistsOnOneLattice` | all eight shipped sizes on one 0.25 m grid, linked and simulated |
| `AJointBetweenTheSmallestAndLargestBlockConservesEnergy` | the 1-cell to 20-cell joint, which is the asymmetry the old model leaked at |
| `TheSubstepEstimateRespondsToTheBlockSizeRatio` | the 1/size² stiffness reaches the integrator |
| `OverALongStepTheSmallBlockForcesMoreSubstepsThanTheLargeOne` | and turns into substeps rather than staying a number |
| `TheStiffestPairingOnTheLatticeStaysBounded` | 400 steps of the worst pairing without diverging |
| `IncrementalTopologyHoldsOnAMixedSizeLattice` | placing blocks one at a time builds the same graph as building whole |
| `GrindingAMixedSizeLatticeLeavesTheSameGraphAsARebuild` | and removing them unpicks it correctly |
| `ASpreadStepIsIdenticalOnAMixedSizeLattice` | a step spread across frames is bit-identical on mixed sizes |

The last three exist because the solver was rebuilt around incremental topology and frame-spread
stepping while this document was open, and neither change had been exercised on anything but
one-cell blocks.

**What none of them claim is that SE2 is supported.** They cover the *model*: the geometry, the
integrator and the graph all work from integer AABBs and are indifferent to a block's volume. The
*storage* is not there — `GridModel`, `SurfaceMap` and `BlockInstance` still hold one entry per
occupied cell, so one 5 m block costs 16,000 dictionary entries and an 8,000-element array. That is
[the block-storage section of scale-design.md](scale-design.md#cell-centric--boundary-centric) and it is the whole remaining distance.

## Reproducing this survey

```bash
# net9.0 console app, PackageReference System.Reflection.MetadataLoadContext 9.0.0
var files = Directory.GetFiles(@"...\SpaceEngineers2\Game2", "*.dll");
var mlc = new MetadataLoadContext(new PathAssemblyResolver(
    files.Concat(Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location), "*.dll"))));
// then LoadFromAssemblyPath + GetTypes, catching ReflectionTypeLoadException and using e.Types
```

Most SE2 types of interest are `internal`, so enumerate with
`BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly`.
Shipped content is JSON `.def` files under `GameData/Vanilla/Content`, keyed by `$Type` naming the
object-builder type.

---

## Open questions to settle in game

1. Is the shared lattice really 0.25 m? Read `CubeBlockComponent.AABB` for a 0.5 m and a 5 m block
   on the same grid and compare extents.
2. Can all eight block sizes coexist on one grid, or does `RelativeBlockSize` partition them?
3. What is the largest block size a modder can define — is 4 a hard tier cap?
4. Does SE2 have a pressurisation/room system, and does it expose rooms the way SE1's
   `IMyGridGasSystem` does? If so the room mapper can be deleted in both games.
5. What does `"CellSize": 64` in `GameData` refer to?

---

## Change log

| Date | Change |
| --- | --- |
| 2026-08-22 | Corrected two survey entries that read as open work and are not. The underground core temperature is built and the `//TODO` this page quoted no longer exists; block mass is re-read by a rota through `ThermalBlock.RefreshMass`, against the claim here that it was read once at construction. Both now state what is built and what is left — a survey of what the engine offers is worth nothing if a reader cannot tell which offers have been taken. |
| 2026-08-22 | Merged `engine-api-notes.md` and `se2-research.md` into this page as Parts 1 and 2: both answer "what does the engine give us", and the design that consumes them treats them as one survey. Dropped the section numbering in favour of named anchors. Added the standard header and this log. |
| 2026-08-20 | Wrote down what the engine gives and what was invented, which is what the wind and climate models are justified against. |
| 2026-08-19 | Measured that entity updates are **not** staggered across frames, and that `MyCubeGrid` clears `EACH_FRAME` from its own update flags — the two engine behaviours the scheduler had been assuming its way around. |
| 2026-08-18 | Added the SE2 lattice and octree findings, and what is testable today. |
| 2026-08-12 | Opened both surveys: the ten SE1 APIs worth using, with the airtight-room system as the highest-value one, and the first reflection pass over the SE2 assemblies. |
