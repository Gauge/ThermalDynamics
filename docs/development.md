# Development

> The rules argued here are stated canonically in [rules.md](rules.md): `C1` `C2` `C3` `C4`
> `M9` `R2` `R3` `R4` `R5` `R6` `R12` `R13`, and the principles P7, P11 and P12 they follow from.

| Looking for | Go to |
| --- | --- |
| How the two halves fit together | [architecture.md](architecture.md) |
| Running the tests and scenarios | [tests/README.md](../tests/README.md) |
| What the engine provides | [engine-notes.md](engine-notes.md) |
| Open work | [backlog.md](backlog.md) |

## Thermal-vision boundary

Production simulation sources live under `Core/`; the offline Core assembly builds those same
sources plus test visibility attributes. Historical heating and aerodynamic inputs live in the
separate `Thermodynamics.Fixtures` assembly, selected explicitly by historical rigs. The separate
`tests/Thermodynamics.Presentation` project links `Presentation/`, which contains
thermal-vision approximation and display policies, with no solver or engine-runtime
reference. Both libraries currently use the documented `VRage.Math` exception.
SE1 billboard, HUD, camera and input integration remains adapter code. See
[architecture.md](architecture.md#thermal-vision-is-presentation-not-simulation).
Do not put display behavior back into `Core/Simulation` merely to test it offline.

## Prerequisites

* Space Engineers installed (the project references DLLs from `Bin64/` directly).
* .NET SDK 9 or newer for the offline projects; the mod targets .NET Framework 4.8 through
  the reference-assemblies package. Runtime sources remain C# 6.
* Cached/restored NuGet packages. Run `dotnet restore tests/Thermodynamics.slnx` when dependencies
  change; restore operates on projects in this repository.
* The historical fixture assembly reads selected sources from a sibling Aerodynamics
  checkout. It neither builds nor modifies that project. Production Core needs no sibling source.
* The mod's own runtime dependencies for testing in game: Definition Extensions
  (`2756894170`) and Rich HUD Master (`1965654081`).

## Opening the MDK2 project

In MDK Hub, choose **Add Existing Project** and select
[`Thermodynamics/Thermodynamics.csproj`](../Thermodynamics/Thermodynamics.csproj).
The adjacent [`mdk.ini`](../Thermodynamics/mdk.ini) identifies this as a mod;
the NuGet package references alone are not sufficient for Hub recognition.
Keep this shared configuration in Git. Put machine-specific game and output
paths in `Thermodynamics/mdk.local.ini`, which stays ignored, or configure them
in MDK Hub. Build `Thermodynamics.sln` to compile the mod and isolated projects.

## Building

**`Thermodynamics/Thermodynamics.csproj` is the mod project, and it is an MDK2 project.** Space
Engineers compiles the mod's sources itself at world load; the assembly this project produces is
not shipped or loaded. What the project gives is three MDK2 packages:

| Package | What it does here |
| --- | --- |
| `Mal.Mdk2.References` | Finds the installed game and references its assemblies from `Bin64/`. |
| `Mal.Mdk2.ModAnalyzers` | Checks every source against the game's script whitelist and reports a prohibited type or member as `MDK01`, an error — `rules.md` `C2`. |
| `Mal.Mdk2.ModPackager` | Packs the mod into the game's local `Mods/Thermodynamics` unless `MdkBuildConfiguration=NoPackage` is set; see [Deploying to the game](#deploying-to-the-game). |

**It is a member of `tests/Thermodynamics.slnx`, so the suite's own build compiles it** — building
the test solution is what checks the mod, and `rules.md` `C11` is why. The test projects link
`Core/`, `Presentation/` and a handful of adapter files; everything else that touches the game's
assemblies is compiled here and nowhere else, so leaving this project out of the build would make a
rename in `Core` invisible until a world load.

Use the repository-local validation entry point from the root:

```bash
bash tools/validate.sh                   # compile all local projects; thermal checks
bash tools/validate.sh --fast            # thermal checks excluding speed=slow
bash tools/validate.sh --integration     # include cross-mod flight checks
bash tools/validate.sh --build-only      # compile and whitelist checks only
```

The script disables MDK packaging, uses one MSBuild worker, and writes test results to
`out/validation/validation.trx`. On Linux it runs tests in a user systemd scope capped at 24 GB
with no swap; `THERMAL_TEST_MEMORY_LIMIT` can select a different ceiling. It expects restored
packages and a working user systemd session. A failed build prevents any tests from running.

The equivalent compile-only command is:

```bash
dotnet build tests/Thermodynamics.slnx --no-restore -m:1 -nr:false -p:MdkBuildConfiguration=NoPackage
```

Both solutions and every project reference stay inside Thermodynamics, checked by
`SolutionsAndProjectReferencesStayInsideThisRepository`. Read-only source links are confined to the historical
fixture project. Do not add a sibling mod project to either solution.

Build the solution for acceptance: tests alone do not compile `Game/`, the session, definitions
or HUD. If a test assembly appears stale during development, rebuild its project with
`--no-incremental`, then rerun the solution check. That diagnostic does not replace the mod's
whitelist build. `ProjectFileTests` also checks that each MSBuild file is readable.

MDK2 discovers the installed game assemblies. Target `net48`, and exclude the unmanaged
`VRage.Native.dll` from managed references. XML API documentation next to the installed DLLs
is available to IntelliSense; [engine-notes.md](engine-notes.md) records the surveyed APIs.

Constraints that matter when writing code for this project:

* **C# 6 only** (`<LangVersion>6</LangVersion>`). No tuples, no `switch` expressions, no
  string interpolation beyond what C# 6 supports, no `out var`.
* **No `#pragma`.** The game's script compiler refuses the directive outright, so a warning
  suppression that keeps the local build quiet stops a world loading the mod. Scope a warning in
  [.editorconfig](../.editorconfig) instead; `ScriptPreprocessorTests` holds it.
* Space Engineers' script whitelist applies: no reflection, no file I/O outside
  `MyAPIGateway.Utilities`, no threading. The mod build reports a prohibited member as `MDK01`,
  from MDK2's copy of the whitelist; [tools/thermal-api-check](../tools/thermal-api-check/README.md)
  asks the installed game directly.
* **The whitelist covers exception types too.** Confirmed prohibited: `IndexOutOfRangeException`,
  `ArgumentOutOfRangeException`. Confirmed allowed: `Exception`, `ArgumentException`,
  `ArgumentNullException`, `InvalidOperationException`, `FormatException`. Prefer bounds-checking
  over catching an out-of-range throw — see `ThermalStorageCodec.TryDecodeVersion2`, which
  validates each record count against the remaining payload rather than reading and catching.
* Anything that allocates per frame will show up. The existing code pools the raycast result
  lists (`_overlapResultPool`, `_gridPool`) and caches `kA` arrays for this reason.

### Testing

The simulation core under [Thermodynamics/Core/](../Thermodynamics/Core) ships with the mod — the
game compiles it — and the projects under [tests/](../tests) link the same files so it can be built,
tested and profiled outside the game. What each of them is, how to run the suite and how to run the
scenarios and benchmarks is [tests/README.md](../tests/README.md).

## Deploying to the game

**Building the mod project deploys it.** `Mal.Mdk2.ModPackager` copies the sources under
`Thermodynamics/` into the game's local `Mods/Thermodynamics/Data/Scripts/Thermodynamics`, and the
content under `Thermodynamics/Content/` beside them, leaving out what
[mdk.ini](../Thermodynamics/mdk.ini)'s `ignores` names — `bin/`, `obj/` and `*.debug.cs`. On this
machine the `Mods` folder is the Proton prefix's
`drive_c/users/steamuser/AppData/Roaming/SpaceEngineers/Mods`, configured in MDK Hub; a
machine-specific path belongs in `Thermodynamics/mdk.local.ini`, which stays ignored.
The `output` value names the parent `Mods` folder; MDK2 appends `Thermodynamics`.
Other projects are outside this repository's build and deployment scope.

Use `-p:MdkBuildConfiguration=NoPackage` for compile-only validation. To package somewhere
else, pass `-p:MdkArguments="-output /absolute/path/to/staging"` explicitly. Thermodynamics uses MDK2 directly; no zip/archive step is required.

Then enable "Thermodynamics" in the world's mod list alongside the two dependencies. Script changes
take effect on world reload; definition XML changes also require a reload.

**Builds with packaging enabled redeploy.** The validation script and builds with
`MdkBuildConfiguration=NoPackage` only compile; they leave the game's local copy unchanged.

## Repository layout

```
Thermodynamics/
├── Thermodynamics/               The mod project (MDK2); everything here that mdk.ini does not ignore ships
│   ├── Thermodynamics.csproj
│   ├── mdk.ini                   Shared MDK2 configuration
│   ├── Core/                     The simulation: no game type crosses its surface (C5)
│   ├── Presentation/             Thermal-vision and glow policies, linked into their own test project
│   ├── Game/                     The adapter: entity components, sync, storage, the thermal-vision renderer
│   ├── Telemetry/                Session data collection
│   ├── Definitions/              Definition Extensions readers
│   ├── RichHudFramework/         Vendored (R6)
│   ├── NetworkAPI/               Vendored (R6)
│   ├── DefinitionExtensionsAPI.cs  Vendored (R6)
│   ├── *.cs                      Session, settings, HUD, terminal, mod API
│   └── Content/
│       ├── Data/                 Cubes.xml, Planets.xml, Loops.xml, block and item definitions, localisation
│       ├── Models/               .mwm models — do not restructure, LOD paths are baked in
│       ├── Textures/             Block, decal and particle textures
│       └── Audio/                FireExtinguisher.wav
├── Workshop/                     modinfo.sbmi and metadata.mod — the workshop identity (R5)
├── tests/                        Isolated build, tests and scenarios
├── tools/                        Corpus scoring, the API client for consumer mods, and one-off labs
└── docs/                         This documentation
```

Two things in that tree are not to be touched: `Content/Models/` is not restructured, and the
workshop identity files are not regenerated. Both have their own sections below.

## Repo conventions

* **The mod folder is what ships**, not the repository: MDK2 packs `Thermodynamics/` and nothing
  outside it, so `tests/`, `tools/`, `docs/` and the corpus never reach the workshop. What must not
  ship must not be under `Thermodynamics/` (`rules.md` `P11`).
* [Workshop/metadata.mod](../Workshop/metadata.mod) and [Workshop/modinfo.sbmi](../Workshop/modinfo.sbmi)
  carry the workshop identity — `modinfo.sbmi` holds the Steam workshop id `2985582372`. Do not
  regenerate them, or the mod will publish as a new item.

## Cleanup sequence

All maintenance, validation outputs and deployment decisions are scoped to this repository.
Aerodynamics and RelativeTopSpeed remain external dependencies. Preserve existing working changes,
public API signatures, serialized identifiers, Workshop identity, assets and vendored libraries.

| Phase | Scope | Acceptance |
| --- | --- | --- |
| 1 | Local build boundary and baseline | No solution/project references build sibling projects; record the baseline before changes. |
| 2 | Current documentation and evidence | Reconcile current guides and backlog; archive dated narratives with working links. |
| 3 | Production/test separation | Remove automatic fixture behavior and external flight sources from the ordinary Core assembly; retain explicit historical/integration fixtures. |
| 4 | Settings and session responsibilities | Consolidate metadata and separate lifecycle/commands without changing persistence, permissions or defaults. |
| 5 | Solver organization | Separate topology, coupling, environmental terms and publication while preserving numerical order and allocations. |
| 6 | Telemetry and presentation | Separate collection/calculation/formatting and clarify cache/lifecycle ownership. |
| 7 | Measured optimization | Evaluate precomputation, exposed-node iteration and temperature ownership; design remapping/sleep separately. |

Phases 1–6 are implemented; phase 7 is assessed in the
[fresh benchmark review](redesign.md#cleanup-assessment-2026-10-01). The source split preserves
solver arithmetic and step order.
The settings catalogue retains name ordering, protobuf identifiers, defaults, permissions and
setter side effects; the suite compiles its real accessors against field declarations and tests
read/write behavior, flags, unknown names and overlay/telemetry notifications. The review retains
current precomputation and leaves topology/sleep, exposed indexing and temperature ownership as
separate designs; no speculative runtime optimization is included.

The [completion receipt](../tests/benchmarks/cleanup-refactor-2026-10-01.txt) records the
compatibility audit, full-suite result and build limitations.

The ordinary Core can be built alone with
`dotnet build tests/Thermodynamics.Core/Thermodynamics.Core.csproj --no-restore`.
The full harness still requires the read-only sibling sources used by the fixture assembly.
Other long measurement pages retain their evidence. Native-session validation and the product
features in the backlog remain separate from this behavior-preserving cleanup.

## Documentation conventions

Every page under `docs/`, and every README, follows one shape. The point is that a reader can pick
up any page and know where to look, and that a page cannot quietly become a historical document
while still reading as a description of the code.

1. **A title, then one paragraph saying what the page covers and what it does not.** A page that
   cannot state its scope in a paragraph is two pages.
2. **A rules banner where the page argues a standing rule**, citing it by identifier — the rule is
   stated canonically in [rules.md](rules.md) and argued at length here, never the other way round.
3. **A "Looking for / Go to" table** where a reader might reasonably be on the wrong page. This is
   what stops the same subject being explained twice in two places. **Four pages have none and
   should not**: the two READMEs, [backlog.md](backlog.md) and [rules.md](rules.md) are indexes
   rather than subjects, so a reader is never on them by mistake. Every other page carries one.
4. **The body in the present tense**, describing what the code does now. **Not** what it used to do,
   what was tried, or what a past session found. **Two things read like history and are not**, and
   both stay: a measurement's before/after table is present-tense evidence, and a correction to
   something this repository published has to sit where the wrong figure sat, naming what it said
   (`E10`). What goes is the narrative around them — *this section used to be organised differently*,
   *that field is gone now*. Swept 2026-08-25: thirty-six sentences in page bodies still say *used
   to* and every one is a correction or a before/after; the seven that were neither are rewritten.
5. **Most important information first.** What the thing *is* precedes how it was arrived at; the
   evidence and the engine survey that justify a model come after the model.
6. **A `## Change log` last**, newest first, one row per date: `| Date | Change |`. This is the only
   place historical revisions belong. Entries record what changed and why, including corrections to
   things this repository previously published — a finding corrected somewhere other than where it
   was published is not corrected (`E10`).

Checks that hold this in place: `EveryDocumentIsInTheIndex`, `EveryPageHasAChangeLog`,
`EveryRelativeLinkResolves`, `EveryAnchorNamesAHeading`, `NoPageNamesATestThatHasBeenRenamed`,
`EveryQuotedSuiteSizeIsCurrent`, `EveryRuleCitedByAPageExists`,
`TheRulesPageIndexesEveryRuleItStates` and `EveryCitedIdentifierResolves` — the last of which reads
`.cs` and `.py` files rather than markdown, because a rule or a backlog row cited in a comment is
a reference that rots exactly like a dead link and nothing was reading those.

### And in the code

A comment follows the same split, stated in
[document-of-intent.md](document-of-intent.md#what-a-code-comment-is-for) and held by `R14`: it
**names** the definition it sits on, and where it would argue, it names the page that argues.

Write that pointer as plain text — `See stiffness.md, A per-block substep cap.` — and never as a
relative markdown link. A link inside a `.cs` file renders nowhere, so nobody clicks it and nothing
notices when it breaks; `EveryAnchorNamesAHeading` reads markdown only. Both of the two that existed
in the tree had rotted, one of them into a directory that does not exist.

**This is `R16` and it is checked now.** It was a convention nothing enforced for three days and 155
more links accumulated across 91 files in that time, including one in `Settings.cs` pointing four
directories above where it sat. `NoPointerInCodeIsWrittenAsALink` fails on any of them.

The exception is a **test class summary**, which `R10` makes the canonical statement of what that
class is for — "in its own summary, not in an index". There is no page to move it to, so it stays
where it is.

**And the two-line limit is about a comment inside a body, not about a summary.** Measured over the
tree: 2,347 running `//` comments, 73 % of them one or two lines and 72 over five — the limit
describes those, and they are the ones that rot, because they sit beside code that moves. The 4,838
`///` summaries run to 24,380 lines and 62 % are longer than two lines; a summary sits on a name,
which does not move under it, and the longest of them carry an experiment's controls. The test on a
summary is `R14`'s: does it state what the thing is, or argue what a page argues?

## Do not restructure `Models/`

[Models/note.txt](../Thermodynamics/Content/Models/note.txt) says it:

> don't change the folder structure of /models/ otherwise the block LODs break.

LOD and build-stage model paths are baked into the `.mwm` binaries at export time. Moving or
renaming a folder under `Models/` silently breaks LOD switching in game, and the only fix is
re-exporting the models.

Naming pattern under `Content/Models/Gauge/{LG,SG}/`:

| Suffix | Meaning |
| --- | --- |
| *(none)* | Full-detail model |
| `_LOD1` … `_LOD3` | Progressively lower detail |
| `_BS1` … `_BS3` | Build-stage models, referenced from `<BuildProgressModels>` |

## Vendored third-party code

Do not hand-edit these; replace them wholesale when the upstream author publishes a new version.

| Path | Upstream |
| --- | --- |
| [RichHudFramework/](../Thermodynamics/RichHudFramework) | Zach Hembree's Rich HUD Framework client module |
| [DefinitionExtensionsAPI.cs](../Thermodynamics/DefinitionExtensionsAPI.cs) | Draygo's Definition Extensions client |
| [NetworkAPI/](../Thermodynamics/NetworkAPI) | SENetworkAPI |

## Scenarios

The harness in [tests/](../tests) runs the model without the game. `dotnet run --project
tests/Thermodynamics.Sim -- list` names them; `run <name|all>` runs them, `--csv <dir>` writes the
full series.

Each one states a conclusion in its summary line, and
[ScenarioClaimTests](../tests/Thermodynamics.Tests/ScenarioClaimTests.cs) and
[SelfShadowScenarioTests](../tests/Thermodynamics.Tests/SelfShadowScenarioTests.cs) assert those
conclusions cannot quietly invert — a scenario whose headline can flip is worse than none, because
it reads like evidence.

Two are about the sun rather than about heat flow:

| Scenario | Answers |
| --- | --- |
| `self-shadow` | Which faces of a solid slab are lit, as a share per direction, with a recess cut into it. Its claim test recomputes the same shares by ray-versus-cube against the same geometry, so the scenario cannot agree with a model that has drifted. |
| `shadow-cost` | What self-shadowing costs: the per-tick cost with the walk idle, and the cost of a whole pass, best of three each. Its claim test fails if the walk ever migrates onto the stepping path. |

## Debugging

* **In game:** the debug toggles in [configuration.md](configuration.md#presentation) cover
  temperature, solar intensity, exposed surfaces, friction, and the solar/wind rays. The
  crosshair readout in [Debug.cs](../Thermodynamics/Debug.cs) dumps a block's full
  thermal state including raw surface bits.
* **Logs:** everything logs to the SE log with the `[Thermodynamics]` prefix via
  `MyLog.Default.Info`.
* **Timing:** switch telemetry on with `/thermal telemetry on` and take a report with
  `/thermal dump`. The cost section breaks the update into topology rebuild, room mapping,
  exposure refresh and solver, which is the breakdown you want before optimising anything. See
  [telemetry.md](telemetry.md).
* **Auditing a dump:** `dotnet run --project tests/Thermodynamics.Sim -- dump` reads the newest
  environment CSV out of the game's saves and checks it against the claims the model makes about
  itself — that the wind decomposes into the factors reported beside it, that convection is
  reported only where there is air, that a positive depth means a buried grid. `--path` names a
  folder or one CSV, `THERMAL_DUMPS` names a folder for good. Non-zero exit means a defect check
  failed; the counts against open questions are printed as observations and never fail. Columns a
  dump predates are skipped by name rather than passed silently.
* **Outside the game:** most questions are faster to answer in [`tests/`](../tests) —
  `dotnet test`, or `dotnet run --project Thermodynamics.Sim -- run perf` for throughput.

## Where to start when adding a feature

| Goal | Touch |
| --- | --- |
| New thermal property on blocks | `ThermalCellDefinition` and `BlockThermalProperties`, the copy in `ThermalBlockCatalog.ToThermalProperties`, then `Content/Data/Cubes.xml` |
| New pipe or pump shape | `ThermalCoolantShapes`, a new SBC in `Content/Data/CubeBlocks/`, an entry in `Cubes.xml`, and the block variant group. The table is linked into the test project, so add a case to `HostAdapterTests` |
| Change how heat moves between blocks | `ConductionBuilder` in `Core/Simulation/ThermalLink.cs` |
| Change environmental response | `EnvironmentSolver`, and `ThermalGrid.Sample` for what the world feeds it |
| Change what counts as exposed | `SurfaceMap.GetExposedFaces`, and `BlockSurfaceBuilder` for what a block declares |
| Change what the game tells a block | `ThermalBlock.Attach` |
| Per-block sun shadowing | `ThermalNode`'s directional weighting is the hook; the solver resolves the sun into six per-face weights once per step |
| New instrumentation | `ISimulationProfiler` for a stage, `GridTelemetry` for a figure, `TelemetryReport` for a row |
| Multiplayer sync | `SettingsSync` for anything the whole world shares, a `NetSync<T>` on the block's own component for anything one block owns, and `SettingsRequests` — on the engine's *secure* handler — for anything gated on who is asking. See [architecture.md](architecture.md#networking) |


## Publishing

**What ships is the folder MDK2 packs, not this repository.** Everything under `Thermodynamics/`
that [mdk.ini](../Thermodynamics/mdk.ini) does not ignore reaches the workshop, and nothing outside
it does, so a publish needs no cleanup step — and a cleanup step that has to be remembered is one
that eventually is not.

| What | Where it lives | Why |
| --- | --- | --- |
| The mod project's build output | `Thermodynamics/bin/`, `Thermodynamics/obj/` | The game compiles the sources itself; these exist only for compile-checking. Left out of the pack by `mdk.ini`'s `ignores`. |
| Every other project's build output | beside its project under `tests/` and `tools/` | Outside the packed folder. |
| The blueprint corpus | `~/.local/share/thermal-dynamics/corpus` | Ten thousand of other people's ships is over a hundred gigabytes, and none of it belongs in a mod. Override with `THERMAL_CORPUS`. |

The packed folder measured 111 MB and 555 files on 2026-09-24 — `Models`, `Textures`, `Audio`, and
`Data` holding the definitions and every C# source file the game compiles.

**No credential is ever written into this tree.** The corpus fetcher takes a Steam Web API key from
the command line or the environment and redacts it from anything it prints; nothing writes it to a
file. Machine-specific paths belong in `Thermodynamics/mdk.local.ini`, which `.gitignore` keeps out
of the repository.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-10-11 | Clarify compile-only builds do not deploy; move the model-table repair entry into this log. |
| 2026-10-08 | Repair the model-suffix table and root-relative dump command. |
| 2026-10-01 | Document local compile-only validation, repository boundaries, SDK requirements and the staged cleanup sequence. |
| 2026-09-24 | **Rewritten for the MDK2 layout.** The mod project is `Thermodynamics/Thermodynamics.csproj` rather than `Generic.csproj`; building it packs and deploys the mod, so *this repository is the mod folder* and the symlink it was deployed by are gone, and so is the root `Directory.Build.props` that sent build output to a sibling directory — the packer's `ignores` do that job now. The whitelist is checked by the build (`MDK01`) and `#pragma` is refused. Two change-log rows that a merge had spliced into the model-naming table are back in this log. |
| 2026-09-19 | Separated thermal-vision presentation from the simulation model, with an independent project and assembly isolation checks. |
| 2026-08-27 | **The mod project had not built for three commits and nothing said so.** Pass 9's fourth iteration wrote a `--` into an XML comment in `Generic.csproj`; MSBuild refuses the file, so it left the build rather than failing it, and every other command kept passing. Fixed, and `ProjectFileTests` now asserts that every MSBuild file in the tree parses — `C11`'s solution membership catches a rename, not an unreadable project. |
| 2026-08-25 | Said which two things read like history and stay — a before/after measurement, and an `E10` correction sitting where the wrong figure sat — because a flat *present tense only* reading of this convention deletes exactly the evidence the rules require. Also scoped the two-line comment limit to a comment inside a body, measured. |
| 2026-08-22 | Took the repository layout tree and the build-and-test instructions off the [README](../README.md), which is written to be pasted into the workshop and read by a player ([backlog.md](backlog.md) `H5`). Nothing in them was wrong; they were in the wrong place, and the documentation index moved to [docs/README.md](README.md) for the same reason. |
| 2026-08-22 | Added the comment convention to [Documentation conventions](#documentation-conventions): a comment names a definition and points at the page that argues it, and the pointer is plain text rather than a relative markdown link, because a link inside a `.cs` renders nowhere and nothing checks it. Both of the two that existed had rotted. |
| 2026-08-22 | Corrected the multiplayer row, which said no commands were registered on the network channel; three replicated properties and a second secure channel exist. |
| 2026-08-22 | Added the standard header and this change log. The documentation conventions this repository follows are stated in [Documentation conventions](#documentation-conventions) below. |
| 2026-08-21 | Checked the documentation's own links and fixed the sixteen that were dead. |
| 2026-08-20 | Moved build output outside the mod folder, so the repository stays publishable with no cleanup step. Added auditing a field dump rather than reading one. |
| 2026-08-17 | Moved the readouts off Text HUD API onto Rich HUD, and recorded the dependency as optional. |
| 2026-08-12 | Opened the page on the rebuilt mod. |
