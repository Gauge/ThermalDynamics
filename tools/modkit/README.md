# The mod kit: a drop-in client for the Thermodynamics API

What this folder holds: a single C# file another mod copies in to talk to Thermodynamics without
touching the raw mod-message protocol. It is not part of this mod — the packed mod holds only what
is under `Thermodynamics/`, so this file never runs here — it is a template for *consumers*.

| File | What it is |
| --- | --- |
| `ThermodynamicsApi.cs` | The drop-in client. One class that registers for the API, requests it, version-checks it, binds every delegate, and exposes them as typed methods returning friendly structs. Copy it into your mod's `Data/Scripts`, optionally rename its `namespace`, and call it. |

## Using it

```csharp
using Thermodynamics.Client;

[MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
public class MyMod : MySessionComponentBase
{
    private readonly ThermodynamicsApi thermal = new ThermodynamicsApi();

    public override void LoadData()      { thermal.Init(); }
    protected override void UnloadData() { thermal.Dispose(); }

    private void Example(IMySlimBlock block)
    {
        float kelvin = thermal.GetBlockTemperature(block);   // 0 until bound, never throws
    }
}
```

Every method is safe to call before the API has bound and against a target the simulation does not
know: it returns the documented default — `0`, `false`, `NaN`, an empty struct — never an
exception. Check `IsReady`, or pass a callback to `Init`, if you need the moment it binds. Read
`MissingKeys` to see whether a key you use is absent on the player's build of Thermodynamics.

The full walkthrough — setup, the binding lifecycle, a worked task per system, and the multiplayer
and threading rules — is [docs/api-guide.md](../../docs/api-guide.md). The key-by-key contract the
client wraps is [docs/api.md](../../docs/api.md).

## Why it does not drift

A copy of an API's shape rots: add a key or change a signature on the mod side and a stale client
casts to null in a player's session with no message anywhere. `ThermodynamicsClientTests` in the
mod's own suite parses this file's bindings and holds them identical to `ThermalApi.cs` — every key,
same signature, both directions, plus the channel id and the major version — so a copy taken from
here agrees with the contract it was taken against. The file is also compiled against the game
assemblies by `Modkit.csproj`, a member of the test solution, as C# 6 and through MDK2's copy of the
game's script whitelist, which is what proves it is valid C# 6 a consumer's mod can compile.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | Renamed from `ThermalDynamicsApi` / `ThermalDynamics.Client` to `ThermodynamicsApi` / `Thermodynamics.Client`. The channel and keys did not change, so copies under the old name still work. |
| 2026-09-05 | Written. `ThermodynamicsApi.cs` is the drop-in client requested for consumer mods: `Init`/`Dispose` and typed methods over the delegate table, held identical to the API by `ThermodynamicsClientTests` and compile-checked by `Generic.csproj`. |
