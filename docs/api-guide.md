# Building on Thermodynamics from another mod

What this page covers: how to bind another Space Engineers mod or in-game script to Thermal
Dynamics' API and drive it — the setup, the binding lifecycle, a worked task for each system, and
the multiplayer and threading rules that decide where a call is safe to make. It is the *guide*;
[api.md](api.md) is the *reference* — the exact key-by-key contract, its version rule, and its
guarantees. Where the two overlap, api.md is authoritative on signatures and this page is
authoritative on how to use them.

| Looking for | Go to |
| --- | --- |
| The exact delegate for one key | [api.md](api.md) |
| What a value means physically | [thermal-model.md](thermal-model.md) |
| The settings a call addresses by name | [configuration.md](configuration.md) |
| Extending the core in-process, past the message API | [api.md](api.md#in-process-extension) |

## The fast path: the drop-in client

If you want to be calling the API in five minutes, copy one file —
[`tools/modkit/ThermodynamicsApi.cs`](../tools/modkit/ThermodynamicsApi.cs) — into your mod's
`Data/Scripts` folder. It does all of the setup and teardown below for you and exposes the whole
API as typed methods that return friendly structs instead of raw tuples. The rest of this guide is
what that file does and why, for when you want to understand or extend it; you do not need any of
it to use the client.

```csharp
using Thermodynamics.Client;   // or whatever you rename the namespace to

[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
public class MyMod : MySessionComponentBase
{
    private readonly ThermodynamicsApi thermal = new ThermodynamicsApi();

    public override void LoadData()      { thermal.Init(); }     // register + request, once
    protected override void UnloadData() { thermal.Dispose(); }  // unregister, once

    private void Somewhere(IMySlimBlock block)
    {
        // Safe before the API has bound and against a block the sim does not know: you get the
        // documented default (0 / false / NaN), never an exception.
        float kelvin = thermal.GetBlockTemperature(block);

        BlockThermals t = thermal.GetBlockThermals(block);   // t.Temperature, t.HeatCapacity, ...
        if (t.HeatCapacity > 0f && t.Temperature > t.Critical) { /* overheating */ }
    }
}
```

Everything is on the one instance: `thermal.GetGridSummary(grid)`, `thermal.AddBlockHeat(block,
joules)`, `thermal.AddHeatSource(entity, watts, range)`, `thermal.AddThreshold(450f,
ThresholdDirection.Rising, e => { ... })`, `thermal.GetCoolantLoop(pipe)`,
`thermal.GetGridAeroForces(grid)`, `thermal.GetSetting(name)`. Check `thermal.IsReady` (or pass a
callback to `Init`) if you need the moment it binds; read `thermal.MissingKeys` if you want to know
whether a key you use is absent on the player's build of Thermodynamics.

Change the `namespace Thermodynamics.Client` line to anything you like if it clashes with your
own. The file is plain C# 6 and references only game types, so it compiles wherever your mod does,
and Thermodynamics' own test suite holds it identical to the API it wraps, so a copy you take
today will not silently disagree with the contract.

The rest of this page is the manual path — use it to understand the client, or if you would rather
bind the delegate table yourself.

## What the API is, in one paragraph

Space Engineers mods cannot reference one another's compiled assemblies, so Thermodynamics
publishes its surface as a `Dictionary<string, Delegate>` sent over a mod-message channel. You
register a handler on the channel, receive the dictionary, and cast the delegates you want to
their documented `Func<...>` types. Every delegate uses only whitelisted types, so an in-game
Programmable Block script binds the same way a mod does. Nothing you call throws into your code,
and nothing you do reaches the simulation except through these delegates.

* **Channel:** `2985582372` (the mod's workshop id — it cannot collide)
* **Payload:** `Dictionary<string, Delegate>`
* **Version:** `1`

## Setup

### 1. A minimal consumer

This is a complete session component that binds at load and one field it uses. Everything else in
this guide is a variation on it.

```csharp
using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
public class ThermalConsumer : MySessionComponentBase
{
    private const long ThermalChannel = 2985582372;
    private const int RequiredMajor = 1;

    private Dictionary<string, Delegate> api;
    private Func<IMySlimBlock, float> getTemperature;

    public override void LoadData()
    {
        MyAPIGateway.Utilities.RegisterMessageHandler(ThermalChannel, OnThermalApi);

        // Ask for the table, in case Thermodynamics loaded before us and its one-time
        // broadcast has already gone out. Any non-dictionary payload is treated as a request,
        // so null is fine, and re-requesting is idempotent.
        MyAPIGateway.Utilities.SendModMessage(ThermalChannel, null);
    }

    protected override void UnloadData()
    {
        MyAPIGateway.Utilities.UnregisterMessageHandler(ThermalChannel, OnThermalApi);
        api = null;
        getTemperature = null;
    }

    private void OnThermalApi(object payload)
    {
        var table = payload as Dictionary<string, Delegate>;
        if (table == null) return;   // our own request echoing back, or someone else's

        var version = table["ApiVersion"] as Func<int>;
        if (version == null || version() != RequiredMajor) return;   // not a build we understand

        api = table;
        getTemperature = api["GetBlockTemperature"] as Func<IMySlimBlock, float>;
    }
}
```

### 2. The three things that setup gets wrong

* **Binding too early.** If you cast delegates in `LoadData` before the table has arrived, they
  are null. Bind in the message handler, as above, and treat "not yet bound" as a normal state
  your code tolerates until `OnThermalApi` fires.
* **Not requesting.** Thermodynamics broadcasts once at session start. If you loaded after that,
  the broadcast is gone and you will wait forever unless you send a request. Always send one in
  `LoadData`.
* **Not null-checking the cast.** A key you did not expect, a delegate whose signature changed
  across a major, or a build that does not carry a key all produce **null** from the `as` cast,
  never an exception. Test for null after every cast and after every `ApiVersion` check.

### 3. Hold the delegates, or hold the table

Either works. Casting once in the handler and holding the `Func<...>` fields (as above) is
marginally faster and makes a missing key obvious at bind time. Holding the whole dictionary and
casting at each call site is fine too — the cast is cheap — and is easier when you use many keys.
Do not, however, cache a *return value* across frames: temperatures, forces and loop states are
live and change every step.

## Versioning: bind defensively

Read `ApiVersion` first and refuse a major you were not written against. The major moves only when
a caller written against the previous major could bind and then be wrong — a key removed, a
signature changed, or a meaning changed without the signature. A key that is merely *added* does
not move it, so a newer Thermodynamics than you were built against still satisfies a correct
`== RequiredMajor` consumer, and you simply will not see the keys added since. Never test `>=` on
the major: a higher major is, by definition, one your casts may now get wrong.

## The tasks

Every snippet below assumes `api` is the bound dictionary from setup. Signatures are copied from
[api.md](api.md); if they ever disagree, the reference wins.

### Read a block's heat

```csharp
var getThermals = api["GetBlockThermals"]
    as Func<IMySlimBlock, MyTuple<float, float, float, int>>;

var t = getThermals(block);
float kelvin       = t.Item1;   // temperature
float heatCapacity = t.Item2;   // J/K
float critical     = t.Item3;   // K, or 0 if the block has no critical limit
int   exposedFaces = t.Item4;   // faces open to the environment
```

A block that is not simulated — excluded by definition, on a grid without physics, or not yet
registered — reads as all zeros rather than throwing. There is no "is this block simulated" call;
a zero heat capacity is the signal, since a real block never has one.

### Read a grid

`GetGridSummary` is the cheap overview; `GetGridHeatBalance` is the thermal budget.

```csharp
var summary = api["GetGridSummary"] as Func<IMyCubeGrid, MyTuple<float, float, int, int>>;
var s = summary(grid);   // hottest block K, ambient K, blocks over critical, coolant loop count

var balance = api["GetGridHeatBalance"] as Func<IMyCubeGrid, MyTuple<float, float>>;
var b = balance(grid);   // watts vented, watts generated  (equal when the ship is in balance)
```

`GetGridFrictionWatts` is one term *inside* the second figure of `GetGridHeatBalance`, not a third
number — add them and you count friction twice.

### Add and control heat sources

A point source radiates like a small sun: `P / (4π r²)` resolved at each grid, weighted by which
faces look at it. Bind a source to an entity and it disappears with the entity — no cleanup on a
projectile or a burning wreck.

```csharp
var addSource    = api["AddHeatSource"]    as Func<IMyEntity, float, float, int>;
var updateSource = api["UpdateHeatSource"] as Func<int, float, bool>;
var removeSource = api["RemoveHeatSource"] as Func<int, bool>;

int id = addSource(missile, 5e6f, 300f);   // 5 MW, felt out to 300 m; 0 means it was not added
updateSource(id, 2e6f);                    // throttle it down
removeSource(id);                          // or drop it (unnecessary if the entity dies)
```

Keep `range` to the distance the effect should actually be felt: every source within range of a
grid costs that grid one pass over its exposed blocks per step (this is `D25` on
[redesign.md](redesign.md) — the fold that would cut it is designed but conditional on a world
using sources, which yours would be). `AddHeatSourceAt(Vector3D, watts, range)` fixes a source at
a world position instead of following an entity.

### Subscribe to temperature thresholds

Register a temperature once; get called for every block that crosses it, on every grid, including
grids created later.

```csharp
var addThreshold = api["AddThreshold"]
    as Func<float, int, Action<IMySlimBlock, int, float, float, bool>, int>;
var removeThreshold = api["RemoveThreshold"] as Func<int, bool>;

int id = addThreshold(450f, 0, (block, thresholdId, threshold, reached, rising) =>
{
    // 450 K crossed on the way up (direction 0 = rising, 1 = falling, 2 = both)
});
```

The callback is raised once per step, after the step completes, on the server and on clients
alike. **A callback that throws is dropped and logged** — it will not be called again, and one
misbehaving subscriber cannot stall the simulation for the rest. Keep it short and defensive; do
the real work by flagging state your own update loop reads, not inside the callback.

### Read and write settings

Every setting is live: a write takes effect on the next step of every grid.

```csharp
var list = api["ListSettings"] as Func<List<string>>;
var get  = api["GetSetting"]   as Func<string, float>;
var set  = api["SetSetting"]   as Func<string, float, bool>;

float coefficient = get("RoomConvectionCoefficient");   // NaN if there is no such setting
set("EnableSolarSelfShadowing", 1f);                    // switches are 0 and 1; false if unknown
```

`GetSetting` returns `float.NaN` for a name that does not exist (and answers with the defaults, not
zero, if Thermodynamics has not finished loading its own config yet). A write is not saved to the
config file — it lasts the session unless an administrator runs `/thermal save`. The names are in
[configuration.md](configuration.md).

### Read a coolant loop

`GetGridSummary` gives the loop count; this reads a loop, keyed on any pipe block in it.

```csharp
var coolantLoop = api["GetCoolantLoop"]
    as Func<IMySlimBlock, MyTuple<bool, float, float, float, int>>;

var loop = coolantLoop(pipeBlock);
if (loop.Item1)   // this block is on a loop
{
    float mean = loop.Item2, hottest = loop.Item3, coldest = loop.Item4;
    int pipeCount = loop.Item5;
    // A working loop has a gradient: it picks heat up at one end and sheds it at the other,
    // so hottest - coldest is the spread the mean hides.
}
```

Key it on a *block*, not a loop index: a topology rebuild reorders the loop list, but the pipe a
player placed is stable.

### Read the aerodynamic forces

`GetGridFrictionWatts` gives the air's heating *power*; this gives the drag and lift *vectors*
this mod applies, in world newtons.

```csharp
var aeroForces = api["GetGridAeroForces"] as Func<IMyCubeGrid, MyTuple<Vector3, Vector3>>;
var f = aeroForces(grid);
Vector3 dragNewtons = f.Item1;   // along the relative wind
Vector3 liftNewtons = f.Item2;   // perpendicular to it
```

**Both are zero at present**: the mod's aerodynamic force was stripped so it can be rebuilt from a
surface profile of its own ([aerodynamics.md](aerodynamics.md)), and zero is the force it applies.
When the rebuilt force ships, this reads the force already on the grid — apply it again only if you
mean to.

### Tell the aerodynamic terms a block's shape

The friction term's only shape is a projected area, so it cannot tell a jet nacelle from a box.
If your block is slippery nose-on and blunt side-on, say so with six per-face multipliers.

```csharp
var setDrag   = api["SetBlockDragProfile"]   as Func<IMySlimBlock, float[], bool>;
var clearDrag = api["ClearBlockDragProfile"] as Func<IMySlimBlock, bool>;

setDrag(nacelle, new[] { 0.2f, 0.2f, 1f, 1f, 1f, 1f });   // slippery on the first two faces
```

Exactly six entries in face order, each `0..1`. **A profile may only reduce** — anything over 1,
under 0, or `NaN` is clamped to 1 (no change), because a registration must never throw into the
simulation's loop and *no change* is the direction that cannot break a ship. It touches friction
heating and the wind's convection, not the sun, and the rebuilt force reads it too.

## Multiplayer

Thermodynamics simulates on the server and on each client (a client predicts locally rather than
having every temperature replicated to it), and the **server is authoritative over damage**. That
sets where your calls belong:

* **Reads are safe on any machine.** They report that machine's current simulation, which on a
  well-synchronised session is the same everywhere; a freshly joined or hitching client may read a
  value that has not caught up yet.
* **Do authoritative writes on the server.** `SetBlockTemperature`, `AddBlockHeat` and
  `SetRoomPressure` change the local simulation. Called on the server, the change is authoritative
  and reaches clients through the normal path; called only on a client, it will drift from the
  server and may be corrected against you. Guard them with `MyAPIGateway.Multiplayer.IsServer`
  unless you specifically want a client-local cosmetic effect and understand it will not persist.
* **Threshold callbacks fire on both.** A crossing is raised on the server and on clients alike,
  so if you react to one with a world change, gate that change to the server yourself or it happens
  once per machine.
* **Heat sources** registered through the API are local to the machine that registered them; if a
  source should exist for everyone, register it on every machine (e.g. in a component that runs on
  both), or register it on the server and let the heat it produces replicate as temperature.

## Threading and timing

* **No call requires a particular thread or update phase.** Call from any update order; the API
  does not touch the simulation's stepping schedule.
* **Nothing is required to be called every frame.** Bind once, call when you have reason to.
* **Return values are per-step live.** A temperature or a force read this frame is this frame's;
  do not cache it across frames.

## Guarantees you can rely on

These are stated in full, and enforced, in [api.md](api.md#guarantees). In short:

* No call throws into you. A bad argument or an internal failure returns the type's default —
  `false`, `0`, `NaN`, an empty tuple — and the exception is recorded in Thermodynamics'
  telemetry, not yours.
* Keys do not change meaning within a major version. New keys may appear; a missing key means an
  older build, so test for null after casting.
* Every signature uses whitelisted types, so a Programmable Block script binds exactly as a mod
  does.

## When the message API is not enough

A mod willing to couple to Thermodynamics' source, rather than only its message table, can drive
the simulation core directly — it has no dependency on the game session and its seams
(`IBlockAdjacency`, `ThermalSimulation`) are meant to be used. That path, and its cost, is in
[api.md](api.md#in-process-extension).

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | The aerodynamic-force section says the force is zero while it is rebuilt, and the drag-profile section says what a profile touches now. |
| 2026-09-24 | **Renamed the drop-in client** to `ThermodynamicsApi` in namespace `Thermodynamics.Client` (file `tools/modkit/ThermodynamicsApi.cs`), matching the mod's name. The message channel and table keys are unchanged, so a copy taken under the old name keeps working; only a fresh copy needs its `using` and type name updated. |
| 2026-09-05 | **Added the drop-in client** ([`tools/modkit/ThermodynamicsApi.cs`](../tools/modkit/ThermodynamicsApi.cs)) and the fast-path section: one file a consumer copies in, `Init`/`Dispose` and typed methods returning structs, held identical to the API table by `ThermodynamicsClientTests`. The manual binding walkthrough stays below it. |
| 2026-09-05 | Written, alongside an audit that added `GetGridAeroForces` and `GetCoolantLoop` to close the two shipped systems that had no read accessor. Covers setup, the binding lifecycle, a task per system, and the multiplayer and threading rules; the key-by-key contract stays in [api.md](api.md). |
