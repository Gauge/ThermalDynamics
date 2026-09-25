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
`ProhibitedMemberRule`, or gets the whitelist verdict it is not expected to get. Five snippets are
*expected* to be refused (`occlusion-concrete-rejected`, `grid-render-data`, `model-materials`,
`voxel-direct` and `voxel-builder`): they record routes the thermal-vision work tried and the game
refuses, so a game update that admits one of them fails the run, and so does one that starts refusing
a route the mod relies on.

The snippets are the thermal-vision work's questions; see
[thermal-vision-lab.md](../../docs/thermal-vision-lab.md). To ask a new question, add an entry to
`cases` in `Program.cs`, and to the `rejected` list if the expected answer is no.

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | Page written. The tool had no README, which `EveryToolIsNamedByItsReadme` reports. Run against the installed game the same day: all fourteen snippets get their expected verdict and the exit code is 0. |
