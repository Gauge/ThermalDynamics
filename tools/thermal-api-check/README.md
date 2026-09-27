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
*expected* to be refused (`occlusion-concrete-rejected`, `grid-render-data`, `model-materials`,
`voxel-direct` and `voxel-builder`): they record routes the thermal-vision work tried and the game
refuses, so a game update that admits one of them fails the run, and so does one that starts refusing
a route the mod relies on.

The snippets cover thermal vision and the aerodynamic engine boundary; see
[thermal-vision-lab.md](../../docs/thermal-vision-lab.md). To ask a new question, add an entry to
`cases` in `Program.cs`, and to the `rejected` list if the expected answer is no.

The aerodynamic checks admit the public physics inputs, world-force and world-angular calls,
and reject direct `HkRigidBody.InertiaTensor` access. The offline bootstrap explicitly registers
`System.Boolean` alongside its other framework primitives; omitting it made a Boolean local fail
under the .NET 9 host, independently of the physics member. The runner records SHA-256 hashes
of the inspected game assemblies and both probe sources. This proves whitelist admission only;
it does not execute native physics. The additional static contract check decompiles
`MyPhysicsBody` and verifies the timestep and argument conversions used by the adapter:
world force takes N, while the world-angular path takes N·m despite its enum name. Changed
semantics fail the run. The decompiled file stays in the temporary work directory.

Run `python3 -m unittest discover -s tools/thermal-api-check -p 'test_*.py'` for
`test_contract.py`: `AeroContractTests` checks a synthetic contract and rejects changed timestep,
linear/angular units, dispatch and missing methods. Neither static check proves native handling.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-26 | Added aerodynamic physics-input and write-path checks, the rejected inertia accessor, binary/source fingerprints, and the missing Boolean primitive in the offline bootstrap. |
| 2026-09-24 | Page written. The tool had no README, which `EveryToolIsNamedByItsReadme` reports. Run against the installed game the same day: all fourteen snippets get their expected verdict and the exit code is 0. |
