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
| `GetGridAeroForces` | `Func<IMyCubeGrid, MyTuple<Vector3,Vector3>>` | The aerodynamic forces this mod applies to the grid, in **world newtons**: drag first, then lift. Drag is the force applied in the latest physics tick; lift remains zero until its separate model is implemented. Disabled, static and unprepared grids report zero. Its meaning and the major are unchanged ([aerodynamics.md](aerodynamics.md#strip-and-rebuild)). |
| `GetGridAeroMoments` | `Func<IMyCubeGrid, MyTuple<Vector3,Vector3>>` | Latest restoring and rotational-damping moments in **world N·m**, separately. |
| `GetGridAeroProfile` | `Func<IMyCubeGrid, Dictionary<string,object>>` | Detached snapshot of the last fully prepared drag profile, schema below. Null while unavailable, rebuilding or preparing a new centre. Allocates copies only when called; never builds geometry in the getter. |
| `SetGridAeroProfileConsumer` | `Func<IMyCubeGrid, long, bool, bool>` | Registers/releases a consumer id on a simulated grid; returns false if unavailable. Repeating an id is idempotent. Each add-on uses its own stable id (e.g. workshop id) and releases every registered grid when finished. Any remaining consumer keeps bounded profile preparation running even with forces off (including static grids with physics), without enabling force writes. Registrations are session-local and removed on grid close; client and server register independently. |
| `GetGridAeroStatus` | `Func<IMyCubeGrid, MyTuple<bool,bool,int,int>>` | Latest profile readiness, numerical-guard binding, construction work units and centre-update face count. Readiness does not certify native or population validation. |
| `GetCoolantLoop` | `Func<IMySlimBlock, MyTuple<bool,float,float,float,int>>` | The coolant loop a pipe block belongs to: found, mean temperature K, hottest pipe K, coldest pipe K, pipe count. `GetGridSummary` gives the loop *count*; this reads a loop. Keyed on a pipe block, not a loop index, because a rebuild reorders the list but the block a player placed is stable. Hottest and coldest are given beside the mean because a working loop has a gradient the mean hides. Found is false for a block that is not a pipe, is on no loop, or is not simulated. |
| `SetBlockDragProfile` | `Func<IMySlimBlock, float[], bool>` | Six multipliers in face order, each 0..1, telling the aerodynamic terms this block is more slippery on some faces than its area says — a nacelle is slippery nose-on and blunt side-on, and an axis-aligned face count cannot tell. **A profile may only reduce**: anything over 1, under 0, or not a number is clamped to 1, which is *no change*. Touches friction heating and the wind's convection and **not** the sun; the rebuilt force reads it too. |
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
profile, and it remains unchanged across later hull/centre updates. Copying is serialized with the
adapter's preparation; callers need not select an update phase. Read occasionally and cache it:
this getter allocates O(direction count) storage on every successful call. Registration schedules
work on subsequent physics ticks; it does not make the profile ready synchronously. Poll the getter
for availability. With forces disabled and no registered consumers, no profile work runs.

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

Schema 1 contains the implemented drag geometry only. It contains no lift planform/span,
compressibility, native inertia tensor or force-ownership agreement. A separate force mod can
register demand, turn `EnableAeroForces` off through the settings API, and read this geometry while
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
* No API call is required to be made on a particular thread or update phase.
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
