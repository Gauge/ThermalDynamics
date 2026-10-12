# Mod API

Thermodynamics is meant to be built on. Everything the simulation knows is readable, everything
it does is switchable, and both heat sources and temperature events can be driven from another mod.

> The rule argued here is stated canonically in [rules.md](rules.md): `R9`, and the principle
> P9 it follows from.

| Looking for | Go to |
| --- | --- |
| What the values mean physically | [thermal-model.md](thermal-model.md) |
| The settings these calls address by name | [configuration.md](configuration.md) |
| Block, planet and loop properties | [definitions.md](definitions.md) |

Space Engineers mods cannot reference one another's assemblies, so the contract is a dictionary of
delegates passed by mod message. Bind to it once and hold the delegates you need.

* **Channel:** `2985582372` (the mod's workshop id)
* **Payload:** `Dictionary<string, Delegate>`
* **Version:** `1` — read `ApiVersion` and refuse a major you were not written against.

## Binding

Register a handler and send anything on the channel to ask for the table. Thermodynamics also
broadcasts once at session start, so either order works.

```csharp
const long ThermalChannel = 2985582372;

Func<IMySlimBlock, float> getTemperature;

public override void LoadData()
{
    MyAPIGateway.Utilities.RegisterMessageHandler(ThermalChannel, OnThermalApi);
    MyAPIGateway.Utilities.SendModMessage(ThermalChannel, null);   // ask, in case we loaded late
}

private void OnThermalApi(object payload)
{
    var api = payload as Dictionary<string, Delegate>;
    if (api == null) return;

    var version = api["ApiVersion"] as Func<int>;
    if (version == null || version() != 1) return;

    getTemperature = api["GetBlockTemperature"] as Func<IMySlimBlock, float>;
}
```

Unregister the handler in `UnloadData`.

## Reading

| Key | Signature | Returns |
| --- | --- | --- |
| `ApiVersion` | `Func<int>` | API major version. |
| `GetBlockTemperature` | `Func<IMySlimBlock, float>` | Kelvin, or 0 when the block is not simulated. |
| `GetBlockThermals` | `Func<IMySlimBlock, MyTuple<float,float,float,int>>` | Temperature K, heat capacity J/K, critical temperature K, exposed faces. |
| `GetGridSummary` | `Func<IMyCubeGrid, MyTuple<float,float,int,int>>` | Hottest block K, ambient K, blocks over critical, coolant loops. |
| `GetRoom` | `Func<IMyCubeGrid, Vector3I, MyTuple<bool,float,float,float>>` | Is a sealed room, air temperature K, pressure 0..1, volume m³. |
| `GetGridHeatBalance` | `Func<IMyCubeGrid, MyTuple<float,float>>` | Watts the grid is venting, watts it is making. Venting reads zero while a grid is net absorbing. |
| `GetGridFrictionWatts` | `Func<IMyCubeGrid, float>` | Watts the grid is taking from the air by aerodynamic friction. **One term of the second figure above, not a third one** — adding them counts it twice. |
| `GetGridAeroForces` | `Func<IMyCubeGrid, MyTuple<Vector3,Vector3>>` | Latest provider-reported aerodynamic forces in **world newtons**: drag first, then lift. Thermodynamics forwards the registered provider; without that delegate both vectors are zero. Force application and preparation belong to Aerodynamics. |
| `GetGridAeroMoments` | `Func<IMyCubeGrid, MyTuple<Vector3,Vector3>>` | Provider-reported restoring and rotational-damping moments in **world N·m**, separately; zero without that delegate. |
| `GetGridGeometryVersion` | `Func<IMyCubeGrid, long>` | Geometry/mass/face-weight revision, or -1 while unavailable. Cheap poll; fetch geometry only when changed. |
| `GetGridGeometry` | `Func<IMyCubeGrid, Dictionary<string, object>>` | Detached raw geometry, schema 1, documented below. No aerodynamic computation. |
| `GetGridEnvironment` | `Func<IMyCubeGrid, MyTuple<bool, Vector3, float, float, float, Vector3>>` | Planet available, world wind m/s, density kg/m³, ambient K, terrain height m, world radial up. |
| `GetEnvironmentAt` | `Func<Vector3D, MyTuple<bool, Vector3, float, float, float, Vector3>>` | Same environment tuple at a character/world position. Nearby grid climate is reused; remote fallback uses planetary daytime temperature and sampled wind. |
| `GetGridWindCeiling` | `Func<IMyCubeGrid, float>` | Native planet wind ceiling at this grid, m/s. |
| `GetGridThermalUpstream` | `Func<IMyCubeGrid, Vector3>` | Last thermal upstream direction, grid-local; may lag current physics. |
| `RegisterAerodynamics` | `Func<long, Dictionary<string, Delegate>, bool>` | Claim the external aerodynamic provider with a nonzero owner id; null dictionary releases that owner's claim. One provider at a time. |
| `GetGridAeroProfile` | `Func<IMyCubeGrid, Dictionary<string,object>>` | Detached snapshot of the last fully prepared aerodynamic profile, schema below. Null until first preparation or after explicit reset; replacement keeps the last completed profile readable. Allocates copies only when called; never builds geometry in the getter. |
| `SetGridAeroProfileConsumer` | `Func<IMyCubeGrid, long, bool, bool>` | Registers/releases a consumer id on a simulated grid; returns false if unavailable. Repeating an id is idempotent. Forwarded to the provider, which owns preparation and registration lifecycle; false without that delegate. Each add-on uses its own stable id (e.g. workshop id) and releases every registered grid when finished; client and server register independently. |
| `GetGridAeroStatus` | `Func<IMyCubeGrid, MyTuple<bool,bool,int,int>>` | Latest profile readiness, numerical-guard binding, construction work units and centre-update face count. Readiness does not certify native or population validation. |
| `GetCoolantLoop` | `Func<IMySlimBlock, MyTuple<bool,float,float,float,int>>` | The coolant loop a pipe block belongs to: found, mean temperature K, hottest pipe K, coldest pipe K, pipe count. `GetGridSummary` gives the loop *count*; this reads a loop. Keyed on a pipe block, not a loop index, because a rebuild reorders the list but the block a player placed is stable. Hottest and coldest are given beside the mean because a working loop has a gradient the mean hides. Found is false for a block that is not a pipe, is on no loop, or is not simulated. |
| `SetBlockDragProfile` | `Func<IMySlimBlock, float[], bool>` | Six multipliers in face order, each 0..1, telling the aerodynamic terms this block is more slippery on some faces than its area says — a nacelle is slippery nose-on and blunt side-on, and an axis-aligned face count cannot tell. **A profile may only reduce**: anything over 1, under 0, or not a number is clamped to 1, which is *no change*. Touches friction heating and the wind's convection and **not** the sun; the rebuilt force reads it too. |
| `SetBlockLiftProfile` | `Func<IMySlimBlock, float[], bool>` | Eight floats for an authored lifting surface: block-local normal, block-local span direction, planform area in m², span in metres. Replaces that block's aerodynamic pressure geometry; skin friction remains. Rejects invalid geometry. Apply by block definition in a consumer mod when the mesh differs from occupied cells. |
| `ClearBlockLiftProfile` | `Func<IMySlimBlock, bool>` | Removes the authored lifting surface and rebuilds automatic geometry. |
| `ClearBlockDragProfile` | `Func<IMySlimBlock, bool>` | Removes a profile, so the block's faces read as their own area again. |

A block that is not simulated — excluded by `ExcludeFromSimulation`, on a grid without physics, or not yet
registered — reads as zero rather than throwing.

```csharp
var aeroForces = api["GetGridAeroForces"] as Func<IMyCubeGrid, MyTuple<Vector3, Vector3>>;
var forces = aeroForces(grid);
Vector3 dragNewtons = forces.Item1;   // along the relative wind
Vector3 liftNewtons = forces.Item2;   // perpendicular to it

var coolantLoop = api["GetCoolantLoop"] as Func<IMySlimBlock, MyTuple<bool, float, float, float, int>>;
var loop = coolantLoop(pipeBlock);
if (loop.Item1)                       // this block is on a loop
{
    float mean = loop.Item2, hottest = loop.Item3, coldest = loop.Item4;
    int pipes = loop.Item5;
}
```

### Aerodynamic profile snapshot, schema 1

Every dictionary and array belongs to the caller. Editing or retaining it cannot alter the live
profile, and it remains unchanged across later hull/centre updates. Call profile and geometry
accessors on the game thread. The external provider owns profile preparation, publication and
consumer scheduling. Read occasionally and cache it: a profile copy uses O(direction count)
storage, while the raw geometry getter copies O(blocks + occupied cells) data. Thermodynamics
forwards profile calls and returns null without the delegate; registration alone does not
make a profile available synchronously.

| Key | Runtime type | Meaning |
| --- | --- | --- |
| `Schema` | `int` | 1. Consumers must reject versions they do not understand. |
| `Directions` | `Vector3[]` | N upstream unit vectors in grid axes, N = 26 or 98. |
| `Areas` | `float[]` | N consecutive rows of five values: silhouette, cosine-squared facing area, separated base area, wetted area (m²), then dimensionless fineness. Includes registered face multipliers. |
| `FirstMoments` | `Vector3[]` | N rows of four first moments matching the four areas above, in m³ about the grid origin. These are weighted sums, not pressure-centre positions. |
| `InterpolationExponent` | `float` | Exponent p for weights max(0, upstream·direction)^p; discard weights below 1e-6 and normalize the remaining sum. Interpolate each area, moment and fineness independently. |
| `Mass` | `float` | Model mass in kg, which may differ from native welded-body mass. |
| `CentreOfMass` | `Vector3` | Latest prepared centre in grid-local metres. |
| `Inertia` | `Vector3` | Model diagonal inertia, kg·m², with the approximations in aerodynamics.md. |
| `DampingCentre` | `Vector3` | Centre used for the cached damping integrals; it can differ from CentreOfMass within the existing 1 mm cache tolerance. |
| `DampingMoments` | `float[]` | Six nonnegative surface integrals (m⁵), in +X, −X, +Y, −Y, +Z, −Z spin order. Per-axis moment is −0.5·density·RotationalDrag·integral·ω·abs(ω). |
| `RotationalDrag` | `float` | Dimensionless coefficient for those damping integrals. |

Schema 1 retains its original drag fields and adds optional lifting geometry:
`LiftNormals`, `LiftCentres`, `LiftSizes`, and `LiftSpanAxes` are detached `Vector3[]` arrays in
grid axes/metres. `LiftGeometry` is a `float[]` with area, authored span and authored chord per
surface (m², m, m). A zero span axis means an automatically detected axis-aligned rectangle,
whose extent is `LiftSizes`; otherwise span axis and its cross product with the normal define
the authored rectangle. `LiftBodyAreas`, `LiftBodyFirstMoments` and `LiftBodyDampingMoments`
use the original layouts with lifting-surface pressure/damping removed. Original fields retain
the drag-only control; do not add lift to their incidence-dependent pressure drag. Consumers
must keep their own separation histories. This is geometry, not live stall state, a native
inertia tensor, or a force-ownership agreement. A separate force mod can
register demand, have the host turn `EnableForces` off in Aerodynamics, and read this geometry while
owning its own forces. Releasing a consumer does not change the world's force setting. Turning off
forces and releasing the final consumer clears the prepared geometry on the next physics tick.

## Writing

| Key | Signature | Effect |
| --- | --- | --- |
| `SetBlockTemperature` | `Func<IMySlimBlock, float, bool>` | Sets a block's temperature outright. |
| `AddBlockHeat` | `Func<IMySlimBlock, float, bool>` | Adds joules. Converted through the block's own heat capacity, so the same energy warms a girder more than a battery. |
| `SetRoomPressure` | `Func<IMyCubeGrid, Vector3I, float, bool>` | Sets how full of air the room containing a cell is, 0..1. |

Each returns false when the target does not exist. Prefer `AddBlockHeat` for anything physical: it
is the only one of the three that conserves energy.

## Heat sources

A point source radiates like a small sun: the mod resolves it to `P / (4π r²)` at each grid, turns
it into a direction in that grid's frame, and the solver weights it by which faces look at it.

| Key | Signature | Effect |
| --- | --- | --- |
| `AddHeatSource` | `Func<IMyEntity, float, float, int>` | Follows an entity. Watts, range in metres. Returns an id, or 0. |
| `AddHeatSourceAt` | `Func<Vector3D, float, float, int>` | Fixed at a world position. |
| `UpdateHeatSource` | `Func<int, float, bool>` | Changes the output of a registered source. |
| `RemoveHeatSource` | `Func<int, bool>` | Removes it. |

A source bound to an entity disappears when the entity does, so a source on a projectile or a
burning wreck needs no cleanup. Beyond `range` a source is not sampled at all — keep it to the
distance the effect should actually be felt at, because every source costs one pass over the
exposed blocks of every grid within it.

```csharp
var addSource = api["AddHeatSource"] as Func<IMyEntity, float, float, int>;
int id = addSource(missile, 5e6f, 300f);       // 5 MW, felt out to 300 m
```

**There is a worked example of this in the mod itself.** The
[Debug Heat Source](blocks.md#debug-heat-source) block is two terminal sliders over `Add`, `Update`
and `Remove` on the same registry these four keys reach — so a session that shows that block warming
a hull has shown what a mod calling these will get. Build one to see the falloff before writing
against it.

## Thresholds

Register a temperature; get called when any block crosses it.

| Key | Signature |
| --- | --- |
| `AddThreshold` | `Func<float, int, Action<IMySlimBlock,int,float,float,bool>, int>` |
| `RemoveThreshold` | `Func<int, bool>` |

`AddThreshold(temperature, direction, callback)` where direction is `0` rising, `1` falling, `2`
both. Returns an id for `RemoveThreshold`. The callback receives the block, the threshold id, the
threshold, the temperature reached, and whether the crossing was upward.

```csharp
var addThreshold = api["AddThreshold"]
    as Func<float, int, Action<IMySlimBlock, int, float, float, bool>, int>;

addThreshold(450f, 0, (block, id, threshold, temperature, rising) =>
{
    // 450 K on the way up
});
```

Thresholds are registered against the mod, not a grid, so they apply to grids created later too.
Crossings are raised once per step, after the step completes, on the server and on clients alike.
A callback that throws is dropped and logged rather than allowed to stop the simulation — do not
rely on being called again after throwing.

## Settings

Every setting is live: writing one takes effect on the next step of every grid, with no reload.

| Key | Signature |
| --- | --- |
| `ListSettings` | `Func<List<string>>` |
| `GetSetting` | `Func<string, float>` |
| `SetSetting` | `Func<string, float, bool>` |

Switches are 0 and 1. `GetSetting` returns `float.NaN` for a name that does not exist, and
`SetSetting` returns false. Changes are not written to the config file — they last for the session
unless an administrator saves them with `/thermal save`. The names are listed in
[configuration.md](configuration.md).

## In-process extension

The core `ThermalSolver.TryRemovePhysicalHeat(block, joules, minimumKelvin, out removedJoules)`
operation removes a bounded physical-equivalent energy between steps. It requires exclusive caller
access and returns false without mutation for an in-flight step, invalid inputs or a block outside
that solver. Successful zero removal is possible at the floor or below temperature resolution.
The returned joules use the actual stored temperature change and `ThermalMass × HeatTimeScale`;
they never exceed the request. See the [cooling protocol](blocks.md#cooling-energy-boundary--implementation-protocol).
This is a core method, not a new public message/delegate endpoint. It does not alter the existing
`AddBlockHeat` API's solver-joule convention or validate game-side authority.


A mod that wants more than the message API can go further, at the cost of coupling to this one's
source. The simulation core in
[Thermodynamics/Core](../Thermodynamics/Core) has no dependency on the
game session, and its two seams are meant to be used:

* **`IBlockAdjacency`** — supply "which blocks touch this one" from a better index than a cell map.
* **`ThermalSimulation`** — `AddBlock`, `RemoveBlock`, `RefreshBlock`, `RefreshBlockSealing`,
  `Update`, `StepExact`, `Save`, `Load`, and read back node temperatures, overheat events and
  threshold crossings. This is the whole surface a host needs; the test harness under
  [tests/](../tests) drives it with plain numbers and the game adapter drives it from the session.

## Guarantees

* No API call throws into the caller. Bad arguments return `false`, `0` or `NaN`, and so does an
  internal failure: every entry in the table is wrapped, so what reaches you is the return type's
  default and what reaches this mod's telemetry is the exception. That is structural rather than a
  promise about each implementation — it was a promise until 2026-08-28, and `GetSetting` had been
  breaking it, throwing where its `SetSetting` sibling returned `false`. Asking for a setting before
  this mod has loaded its own is safe now, and answers with the defaults rather than with zero.
* The API does not enforce a thread or update phase, but it does not synchronize live grid
  collections. Call engine-facing getters and mutations on the game thread; detached snapshot
  arrays may be processed elsewhere. Exception guards do not provide thread safety.
* Delegate signatures use whitelisted types only, so scripts and mods can both bind.
* Keys will not change meaning within a major version. New keys may be added; missing keys mean an
  older build, so test for null after casting.

**What moves the major, stated so a caller knows what the number is worth.** It moves when a caller
written against the previous major could still bind and then be wrong: a key that goes away, a key
whose signature changes — a widened `Func`, a `MyTuple` grown a field — or a key whose meaning or
units change while its signature does not. A key that is *added* does not move it. The first two are
checked against a recorded surface in `tests/Thermodynamics.Tests/ApiSurface.txt`, which also fails
the suite if the version moves without a break, because a caller refused for nothing is a break too.
See [document-of-intent.md](document-of-intent.md#what-the-version-number-governs-and-what-moves-it).

**This number governs the delegate table and nothing else.** The mod carries no version of its own,
and the three promises it makes to a world rather than to a caller — the save format, the retired
definition names, a setting's name — sit under no number at all, because they are *never* rather
than *not within a major*.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-10-08 | Correct provider forwarding, snapshot costs, thread-safety limits and exception behavior. |
| 2026-09-27 | Added authored lift overrides, actual submitted lift reporting and optional lifting/residual geometry in profile snapshots. |
| 2026-09-27 | Documented bounded physical-energy extraction and its exclusive-access, completed-step contract. |
| 2026-09-27 | Added detached schema-1 drag-profile snapshots and explicit per-grid consumer registration. Documented all fields, units, lifetime, copy cost and force-off behavior; added matching drop-in client bindings. |
| 2026-09-27 | Added current applied drag, separate restoring/damping moments and preparation status to the grid API and drop-in client; registered face overrides now reach profile construction. |
| 2026-09-24 | `GetGridAeroForces` returns zero vectors: the aerodynamic force it reports was stripped (`K25` step 0), and the key's meaning — the force this mod applies — is unchanged, so the major stays 1. `SetBlockDragProfile` touches friction heating and convection until the force returns. |
| 2026-09-05 | **Two shipped systems gained the read accessors an audit found missing** (additions, so the major stays 1). `GetGridAeroForces` returns the drag and lift force *vectors* in world newtons, where only the scalar friction watts had been exposed — a flight mod could read the heat and not the force. `GetCoolantLoop` reads a coolant loop's mean, hottest and coldest pipe temperatures and its pipe count off any pipe block, where `GetGridSummary` had given only the loop count — the cooling feature the mod is named for was write-only-invisible to a consumer. |
| 2026-08-28 | **The first guarantee is enforced rather than promised, and it had been false.** `GetSetting` dereferenced `Settings.Instance` where its `SetSetting` sibling guarded the same field, so a consumer asking for a setting before this mod loaded its own got a `NullReferenceException` in their session. Every entry is wrapped now, `GetSetting` reads through `Settings.EnsureLoaded`, and `ModApiShapeTests` fails on an unwrapped entry by name. |
| 2026-08-25 | Said what moves the major version, which the page had told a caller to trust without saying what it was worth ([backlog.md](backlog.md) `B37`). It moves when a caller written against the previous major could still bind and then be wrong; an added key does not move it. Two of the three cases are now checked against a recorded surface. |
| 2026-08-22 | Added the standard header and this change log. |
| 2026-08-21 | Checked every link on this page against the files and headings it names. |
| 2026-08-19 | Added what a grid vents against what it makes. Renamed the definition dials that said the wrong thing, and the API entries with them. |
| 2026-08-13 | Opened the page as the contract: `EveryModApiEntryIsDocumented` fails when the delegate table carries a key this page does not name. |

## Standalone Aerodynamics integration

Load both mod packages. Thermodynamics performs no aerodynamic force writes. Existing force,
profile, status and lift-authoring API calls forward to the registered Aerodynamics module;
without it they return zero/false/null. Drag face weights remain geometry data and invalidate
raw geometry revisions. No mod-specific CLR types cross this boundary. Thermodynamics broadcasts
`false` on the API channel when unloading; consumers must drop delegates and stop force writes.
The standalone consumer retries discovery and waits for server settings before applying forces.

Raw geometry keys: `Schema` (int 1), `Revision` (long), `GridSize` (float metres), `Min` and
`MaxExclusive` (Vector3I[] block bounds), `Mass` (float[] kg), `Cells` (Vector3I[] flattened occupied
cells), `CellStarts` (int[], block count + 1 offsets into Cells), `FaceWeights` (float[], six per
block in the same face order as drag authoring). All arrays are copies; mutating them never changes
Thermodynamics. Export is O(blocks + cells), requested only after a revision changes. Profile
construction, surface reconstruction, lift detection and numerical guards belong to Aerodynamics.

The provider dictionary must supply `SurfaceHeating` as
`Func<float,float,float,float,float,float,float,float>`: effective exposed upstream area m²,
air-density fraction, airspeed m/s, ambient K, surface K, heat capacity J/K, integration duration s;
return watts. Thermodynamics integrates the returned source and sanitizes invalid values.
An optional `SurfaceHeatingGate` (`Func<float,bool>`, airspeed m/s to active) skips heating work
when the provider is disabled or below its threshold. Without that optional callback, a registered
provider is considered active at positive speed. The heating callback must also enforce its own
switches. Without any registered provider the gate is false.
No provider means zero aerodynamic heating. The registered provider also supplies the existing
force/profile/status/lift-authoring delegates with their existing signatures. Registration is
local to each server/client session and released on unload. A `SurfaceHeating` exception disables registration, logs a fault and announces unavailability;
discovery must succeed again before forwarding resumes. A `SurfaceHeatingGate` exception logs
a fault and returns false for that sample; it does not release the provider. Exceptions in
forwarded force/profile delegates are caught by the API guard without releasing registration.
Heating callbacks run inside the solver and can execute concurrently on different grid workers
when `ParallelGrids` is enabled. Keep them allocation-free where possible, thread-safe, and
independent of engine reads or writes. The current heating-exception path itself invokes
telemetry and mod messaging inline; worker-safe recovery remains a review finding.

Changelog: 2026-09-27 — added raw geometry/environment and external-provider contracts; aerodynamic
runtime extracted to a separate package while preserving existing delegate signatures.
