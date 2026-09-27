# Game whitelist probe

**`run.py` compiles short C# snippets with the installed game's own script compiler and its own
whitelist analyzer, and says which API members a mod may use.** A green mod build proves a member
exists; it does not prove the game will accept it (`rules.md` `C2`). This asks the game.

```bash
python3 tools/thermal-api-check/run.py ~/Steam/SteamLibrary/steamapps/common/SpaceEngineers
```

It needs .NET 9 and `ilspycmd` (`dotnet tool install -g ilspycmd`). Nothing is executed in, or
written to, the game or this repository: the work directory is a fresh temporary folder, named on
the first line of output, holding the generated bootstrap, the project and `report.log`.

## How it works

1. `ilspycmd` decompiles `SpaceEngineers.Game.MySpaceGameDefaultIlChecker` out of the installed
   `SpaceEngineers.Game.dll`. That class is where the game declares its whitelist.
2. `run.py` renames it `GameWhitelistBootstrap` and adapts the few calls that need a running game,
   failing loudly if the decompiled text no longer contains exactly what it expects to replace.
3. `Program.cs` builds the whitelist through that bootstrap, then compiles each snippet as C# 6
   against the game's assemblies and runs the game's `WhitelistDiagnosticAnalyzer` over it with the
   `ModApi` target.

## Reading the output

One line per snippet, `compiler=N, whitelist=M`, then each diagnostic. **The exit code is the
verdict**: it is non-zero if any snippet fails to compile, raises a diagnostic other than
`ProhibitedMemberRule`, or gets the whitelist verdict it is not expected to get. Six snippets are
*expected* to be refused (`aero-physics-inertia`, `occlusion-concrete-rejected`, `grid-render-data`, `model-materials`,
`voxel-direct` and `voxel-builder`): they record routes the thermal-vision work tried and the game
refuses, so a game update that admits one of them fails the run, and so does one that starts refusing
a route the mod relies on.

The snippets cover thermal vision, the aerodynamic engine boundary and the extinguisher projectile API; see
[thermal-vision-lab.md](../../docs/thermal-vision-lab.md). To ask a new question, add an entry to
`cases` in `Program.cs`, and to the `rejected` list if the expected answer is no.

The aerodynamic checks admit the public physics inputs, world-force and world-angular calls,
the detached dictionary/array snapshot delegates and their synchronization lock,
and reject direct `HkRigidBody.InertiaTensor` access. The offline bootstrap uses a subset of framework permissions because the game's .NET Framework
reflection setup cannot run unchanged on .NET 9. `profile_framework_bootstrap` checks the installed
game's explicit declarations before admitting the array, primitive and Func types needed here.
It preserves the game's restriction on Delegate creation and admits only GetInvocationList from
its allowed members: enumerating .NET 9's additional span overloads breaks the old analyzer.
The first profile probe falsely refused arrays and delegates because that subset omitted them;
[the recorded rerun](../../tests/benchmarks/aerodynamics/profile-api-whitelist-2026-09-27.txt)
includes both instrument failures and the corrected result. This is a targeted probe, not a claim
that every .NET 9 framework member matches the game's framework. The runner records SHA-256 hashes
of the inspected game assemblies and both probe sources. This proves whitelist admission only;
it does not execute native physics. The additional static contract check decompiles
`MyPhysicsBody` and verifies the timestep and argument conversions used by the adapter:
world force takes N, while the world-angular path takes N·m despite its enum name. Changed
semantics fail the run. The decompiled file stays in the temporary work directory.

Run `python3 -m unittest discover -s tools/thermal-api-check -p 'test_*.py'` for
`test_contract.py`: `AeroContractTests` checks a synthetic contract and rejects changed timestep,
linear/angular units, dispatch and missing methods. Neither static check proves native handling. `ProfileFrameworkTests` also rejects missing or changed
framework permissions before a compatibility bootstrap can grant them.

## Extinguisher projectile boundary

`extinguisher-projectile-hit` checks registration/removal of a hit interceptor, the server gate,
projectile ammo/weapon identities, owner and player fields, origin/range, hit position/normal,
and grid-cell lookup. The installed compiler and whitelist admit these members. An initial
failure was a missing `ulong` in the offline compatibility subset; the native whitelist explicitly
admits it, and a mutation control rejects a missing declaration before the bootstrap can grant it.
The runner fingerprints Sandbox.Common.dll as well as the other relevant game assemblies.

The [recorded probe](../../tests/benchmarks/hand-cooling-projectile-api-2026-09-27.txt)
retains the failed and corrected runs. Static inspection of native `MyProjectile.OnHit` puts the
interceptor before the positive-damage branch, so zero-damage ammo is not excluded by that branch.
This proves API admission and the inspected dispatch order, not ammo consumption, callback thread
ownership, native cooling behavior or multiplayer correctness. The extinguisher still only scans.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Added the extinguisher projectile-hit probe, a source-verified ulong permission and Sandbox.Common fingerprinting. Native cooling remains unimplemented. |
| 2026-09-27 | Added the profile API probe and corrected omitted framework permissions against installed-game declarations. Recorded two instrument failures and the successful rerun; added four mutation cases that reject changed permissions. |
| 2026-09-26 | Added aerodynamic physics-input and write-path checks, the rejected inertia accessor, binary/source fingerprints, and the missing Boolean primitive in the offline bootstrap. |
| 2026-09-24 | Page written. The tool had no README, which `EveryToolIsNamedByItsReadme` reports. Run against the installed game the same day: all fourteen snippets get their expected verdict and the exit code is 0. |
