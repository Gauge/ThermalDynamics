# Documentation index

Every page under `docs/`, grouped by what a reader is trying to do. The [README](../README.md) is
the mod's front page for players; this is the way in for anyone working on it.

Every page opens with what it covers and closes with a change log; history lives there rather than
in the prose. The conventions are in
[development.md](development.md#documentation-conventions) and are checked by
`EveryPageHasAChangeLog`; that every page is listed here is checked by `EveryDocumentIsInTheIndex`.

**Start here**

| Document | Contents |
| --- | --- |
| [document-of-intent.md](document-of-intent.md) | What the mod is for and the goals it is measured against, where those goals conflict with the code, and where no intent has been stated at all. |
| [rules.md](rules.md) | The standing rules, in one place: fifteen principles, the rules that follow from them, and whether each is absolute, conditional or not worth keeping. |
| [architecture.md](architecture.md) | Component layout, update order, grid lifecycle, persistence. |
| [backlog.md](backlog.md) | Every open item across these documents, categorised, one line each. |

**The simulation**

| Document | Contents |
| --- | --- |
| [thermal-model.md](thermal-model.md) | Every equation the simulation evaluates, and the surface geometry every area term reads. |
| [aerodynamics.md](aerodynamics.md) | Current external-provider ownership, local validation boundary and redirects to archived aerodynamic requirements. |
| [environment.md](environment.md) | The air, ground, sun and wind outside a grid: how each is computed and what evidence stands behind it. |
| [scale-design.md](scale-design.md) | Where the model is going: variable block sizes, and grids to a million blocks. |
| [se2-labs.md](se2-labs.md) | What Space Engineers 2's architecture costs each structure of the model, measured: the lattice tax, the room map at supercell stride, and the conduction kernel in job shapes. |

**Using it**

| Document | Contents |
| --- | --- |
| [blocks.md](blocks.md) | The blocks and items this mod ships, and the coolant loop build rules. |
| [configuration.md](configuration.md) | Every setting, its default, the runtime commands, and where the settings surface is going. |
| [realism.md](realism.md) | How far the model is from physics, measured, and what each departure costs. |
| [definitions.md](definitions.md) | Block, planet and loop properties, and how to add support for another mod's blocks. |
| [api.md](api.md) | The mod API: reading, writing, heat sources, thresholds, settings. |
| [api-guide.md](api-guide.md) | How to bind another mod or script to the API and drive it: setup, the binding lifecycle, a task per system, and the multiplayer and threading rules. |

**Measurement and evidence**

| Document | Contents |
| --- | --- |
| [telemetry.md](telemetry.md) | Session data collection and what the report contains. |
| [benchmarks.md](benchmarks.md) | The performance report: cost by size, feature and configuration; what a substep costs; and the trend across passes. |
| [performance.md](performance.md) | The performance passes: what each iteration measured, changed and was worth, and how every claim was checked. |
| [load-and-hitching.md](load-and-hitching.md) | What a grid costs as it grows, what makes it stutter, and what live worlds measure. |
| [stiffness.md](stiffness.md) | Why a handful of light fittings sets the cost of a capital ship, and what to do about it. |
| [memory.md](memory.md) | Where a grid's memory goes, and what can be given back. |
| [redesign.md](redesign.md) | The structural redesigns still available: measured refusals, the two open candidates, and the labs and criteria that decide them. |
| [balance-lab.md](balance-lab.md) | Deciding good balance from a population of real ships: criteria, staging, and the corpus. |
| [balance.md](balance.md) | Every block costed against the vanilla blocks it competes with, and what 8,132 real ships say about the targets. |

**Working on it**

| Document | Contents |
| --- | --- |
| [repository-review-2026-10-08.md](repository-review-2026-10-08.md) | Current documentation reconciliation, prioritized implementation risks, reproduction and validation limits. |
| [development.md](development.md) | Building, deploying, repo layout, conventions. |
| [known-issues.md](known-issues.md) | Deliberate limits, open defects, and the failure patterns worth carrying forward. |
| [engine-notes.md](engine-notes.md) | What both engines actually provide, and what an SE2 adapter would bind to. |
| [thermal-glow.md](thermal-glow.md) | Natural overheating glow, reference evidence, acceptance criteria and rendering limits. |
| [thermal-vision-lab.md](thermal-vision-lab.md) | Selected mixed-detail thermal candidate, corpus appearance comparisons, archived experiments and explicit readiness gaps. |
| [thermal-vision-optimization-audit.md](thermal-vision-optimization-audit.md) | Synthetic CPU/allocation measurements, retained optimizations, rejected strategies and validation limits. |
| [thermal-vision-survey.md](thermal-vision-survey.md) | Implemented snapshot survey-scope alternative, compromises, work limits and consolidated in-game test procedure. |
| [thermal-vision-design.md](thermal-vision-design.md) | Proposed colour/greyscale vision, suit/camera eligibility, rendering evidence and prototype acceptance criteria. |
| [tests/README.md](../tests/README.md) | The isolated simulation environment: running the tests and scenarios. |
| [tools/corpus/README.md](../tools/corpus/README.md) | The corpus tooling: the verdict script and the report builder. |

**Historical records**

Archived pages preserve dated results, failed experiments and decisions. They are not current
implementation guides or authorization to maintain another repository.

| Record | Contents |
| --- | --- |
| [Aerodynamics history](archive/aerodynamics-history.md) | Former combined-mod requirements, calibration/population studies and extraction records. |
| [Backlog priority history](archive/backlog-priority-history.md) | Superseded priority rationale and its original measurements. |

## Change log

| Date | Change |
| --- | --- |
| 2026-10-08 | Index the repository documentation and performance review. |
| 2026-10-01 | Distinguish current integration guidance from explicitly archived development evidence. |
| 2026-09-24 | Indexed the aerodynamics requirements page. |
| 2026-09-20 | Index the natural overheating glow design and validation record. |
| 2026-09-18 | Indexed the thermal vision design and feasibility assessment. |
| 2026-08-22 | Opened, taking the documentation index off the [README](../README.md). The README is written to be pasted into the workshop and read by a player, and an index of twenty-one developer pages is not something that audience needs ([backlog.md](backlog.md) `H5`). |
