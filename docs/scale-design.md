# Design: variable block sizes and grids to a million blocks

Where the model is going, in two parts: the boundary-centric geometry that lets one simulation
model serve both Space Engineers 1 and 2, and the machinery a single grid of **10⁶ blocks** — the
stress bound, against a design target of 250,000 (`G5`) — needs to
tick inside a frame budget. Portability here means *the model*, not a shared binary — the two games
get separate builds and separate adapters.

> **Part 1 is partly built; Part 2 is design only.** Each part says which is which — Part 1 opens
> with an implementation status table, and nothing in Part 2 is implemented.

| Looking for | Go to |
| --- | --- |
| What SE2's assemblies actually contain | [engine-notes.md](engine-notes.md) |
| What a grid costs today, measured | [load-and-hitching.md](load-and-hitching.md), [benchmarks.md](benchmarks.md) |
| Where a grid's memory goes today | [memory.md](memory.md) |
| Why the stiffest block sets the cost | [stiffness.md](stiffness.md) |

---

# Part 1 — Variable block sizes

Goal: one simulation model that serves SE1 and SE2, is as fast as the physics allows, and does not
waste memory.

## Implementation status

| Change | Status |
| --- | --- |
| Analytic contact area and conduction from block bounds | **done** — [`BoxGeometry`](../Thermodynamics/Core/Util/BoxGeometry.cs), [`ConductionBuilder`](../Thermodynamics/Core/Simulation/ThermalLink.cs) |
| Conduction path length from real block depth | **done** |
| Sealing and mounting as a per-face fraction | **done** — `BlockModel.LocalFaceMountFraction`, `BlockInstance.MountFraction` |
| Adjacency behind a host port | **done** — [`IBlockAdjacency`](../Thermodynamics/Core/Model/IBlockAdjacency.cs), `ThermalSolver.Adjacency` |
| Exposure walks surfaces, not volumes | **done** — `SurfaceMap.GetExposedFaces` |
| Block *storage* stops enumerating cells | **not done** — `GridModel.blocksByCell`, `SurfaceMap`'s cell table and `BlockInstance.Cells` are still one entry per occupied cell. This is the one thing between the model and an SE2 grid; the maths above is already there. `Se2LatticeTests` marks the line between them. |
| Room mapping off the block lattice | not done |
| Struct-of-arrays solver state | **done for the step** — the substep loop reads flat arrays mirrored from the nodes, refreshed only for nodes that changed. The node objects remain the public face. |
| One grid per thread | **measured and built, shipping off** — `ParallelGrids`; `bench parallel`: 10.17x on a 242-grid fleet at 32 threads, 7.09x at eight, 3.35x on an uneven fleet, hand-off 1.6-6.8 us. See [One grid per thread, measured and built](#one-grid-per-thread-measured-and-built) |
| Stiffness / multirate | not done — but now **measured**: `LastStepWasClamped` is reported per grid as "steps clamped by substep cap" |
| Land `Core/` in the live mod | **done** — the adapter is [`Game/`](../Thermodynamics/Game); the legacy per-cell path is deleted |
| Instrument | **done** — [`ISimulationProfiler`](../Thermodynamics/Core/Simulation/SimulationProfiler.cs) times topology, room mapping, exposure and solver; see [telemetry.md](telemetry.md) |

### Measured effect

Counting the bolted contact area of one joint between two N×N×N blocks:

| N | cells/block | before | after | speedup |
| --- | --- | --- | --- | --- |
| 1 | 1 | 0.3 µs | 0.070 µs | 4× |
| 8 | 2.5 µs | 0.013 µs | 192× |
| 64 | 22.5 µs | 0.013 µs | 1 778× |
| 8 | 512 | 126.1 µs | 0.013 µs | 9 683× |
| 16 | 4 096 | 986.8 µs | 0.015 µs | **67 127×** |

The new cost is **flat** — the same 13 nanoseconds whatever the block size — because it is
arithmetic on two bounding boxes. The old cost grew superlinearly: it walked every cell of block
A, and for each contacting cell searched every cell of block B for a matching mount, so it was
O(V<sub>A</sub> × 6 + contactArea × V<sub>B</sub>).

At SE2's finest lattice a 5 m block occupies 8 000 cells, which would have put the old
implementation at roughly 2 ms *per joint*, on a grid with millions of joints.

Whole-stage costs for 216 blocks, showing what is fixed and what is not:

| cells/block | `RebuildLinks` | `SurfaceMap.Rebuild` |
| --- | --- | --- |
| 1 | 0.208 ms | 0.099 ms |
| 64 | 1.522 ms | 4.194 ms |
| 4 096 | 15.967 ms | 144.792 ms |

`RebuildLinks` now grows with block *surface* and sublinearly at that. `SurfaceMap.Rebuild` still
grows with block *volume*, because it writes one dictionary entry per occupied cell — that is the
"not done" row above, and it is the next thing to remove.

---

## The constraint that drives everything

SE1 has two block sizes and they never mix on one grid: 2.5 m or 0.5 m, fixed per grid.

SE2 ships blocks at 0.25, 0.5, 1.0, 1.25, 1.5, 2.5, 3.5 and 5.0 m, on what appears to be a shared
0.25 m lattice. A 5 m block therefore spans **8 000 lattice cells**.

The current model is **cell-centric**. Every one of these is keyed by `Vector3I` cell:

| Structure | Entries | Cost for one 5 m block |
| --- | --- | --- |
| `GridModel.blocksByCell` | one per occupied cell | 8 000 |
| `SurfaceMap`'s cell table | one per occupied cell | 8 000 |
| `BlockInstance.gridCells[]` + `gridSurfaces[]` | one per occupied cell | 8 000 × 16 B |
| `RoomMap.solid` / `RoomMap.roomCells` | one **bit** per cell in the bounding volume | grows as the cube of ship size |
| `SurfaceMap.GetExposedFaces` | iterates cells × 6 faces | 48 000 iterations |
| `GridModel.Neighbours` | iterates cells × 6 faces, with `List.Contains` | 48 000 iterations |

None of that survives SE2. Keen reached the same conclusion: `CubeBlockDefinition` stores
`OccupiedGridCellsGroups` as an `ImmutableArray<BoundingBoxI>` — **boxes, not cells** — and the
octree stores a large block as one leaf.

> **The redesign is one idea: stop enumerating cells. Work on block boundaries.**

Everything below follows from that. It is also a large win in SE1, where it deletes two
dictionaries and the per-cell surface map.

---

## Cell-centric → boundary-centric

The thermal model never actually needed interior cells. It needs four things, and all four are
computable analytically from integer AABBs.

### Contact area between two blocks — O(1)

Two blocks touch across axis `a` when `A.MaxExclusive[a] == B.Min[a]` (or the mirror). The contact
rectangle is the AABB overlap on the other two axes:

```
overlap[b] = min(A.MaxExclusive[b], B.MaxExclusive[b]) - max(A.Min[b], B.Min[b])
contactCells = overlap[b1] * overlap[b2]          // lattice cells²,  0 if either ≤ 0
contactArea  = contactCells * lattice * lattice   // m²
```

Constant time regardless of block size. Replaces `ConductionBuilder.CountContactFaces` and
`GridModel.SharedFaceCount`, both of which walk every cell.

### Conduction path length — falls out of the AABB

```
L_A = (A.Extents[a] * lattice) / 2         // half the block's thickness along the contact axis
G   = contactArea / (L_A / k_A + L_B / k_B)
```

This is strictly better than what `ThermalLink` does today. The current formula divides by
`LargestFaceArea`, a heuristic that exists only because the original had no real notion of path
length. With variable block sizes the physical form is *required* — a 5 m block genuinely does
conduct more slowly end-to-end than a 0.25 m one, and the heuristic does not capture that.

### Exposed area per face direction — O(neighbours)

```
faceArea[f]  = product of the two extents perpendicular to f      // lattice cells²
exposed[f]   = faceArea[f] - Σ contactCells(neighbours on f) - sealed[f]
```

Note this becomes an **area**, not a face count. `ThermalNode.ExposedFaces` is `int[6]` today
because every cell face has the same area; with variable sizes that stops being true. Six floats.

### Sealing — a fraction, not a bit

SE2 exposes `EffectivePermeability` as a float driven by build progress. Store exposure as a
`float` per face and SE1 simply supplies 0 or 1. Costs nothing, buys SE2 and half-welded walls.

---

## Feature inventory

Every feature in the current mod, what it *must* deliver, and what can be cut or approximated.
"Cost" is the shape of the work per simulation step or per topology change.

### Solver core

| # | Feature | Must have | Shortcut available | Cost |
| --- | --- | --- | --- | --- |
| F1 | **Conduction** between touching blocks | Symmetric, energy-conserving; area and path length must respond to block size | None — this is the mod | O(links)/step |
| F2 | **Radiation** to ambient | `εσA(T⁴ - T_amb⁴)` on exposed area | Precompute `εσA` per block (already done). `T_amb⁴` once per grid per step (already done) | O(nodes)/step |
| F3 | **Convection** in atmosphere | Blend with radiation by air density; still air must still convect | Coefficient is per-grid, not per-block | O(nodes)/step |
| F4 | **Solar gain** | Per-face directional weighting against sun vector | Per-grid occlusion flag is enough (per-block self-shadowing is built and switchable — `SolarSelfShadowing`) | O(nodes)/step |
| F5 | **Aerodynamic friction** | v³ on windward faces | Only active above a speed threshold — skip the whole loop when `!FrictionActive` | O(nodes)/step, usually skipped |
| F6 | **Waste heat** from power/thrust | Watts per block | Recompute only on host notification, never per step (already done) | O(1)/step |
| F7 | **Overheat damage** | Per-second, not per-step | Only nodes above critical enter the list; most grids produce zero | O(nodes)/step, branch-predicted |
| F8 | **Coolant loops** | Lumped ring temperature, conducts to pipes and sink plates | Loop count is tiny; keep as a separate small array | O(loop links)/step |

F2–F5 all iterate every node and all are cheap arithmetic. They should be **one fused pass over
SoA arrays**, not four passes — which is what `AccumulateEnvironment` already does. Keep that.

### Topology and geometry

| # | Feature | Must have | Shortcut available | Cost |
| --- | --- | --- | --- | --- |
| F9 | **Neighbour discovery** | Which blocks touch which | **Ask the host.** SE1: block neighbours. SE2: `GetConnectedCubeBlocks` / `HasBlockConnection` — the octree already maintains this graph | O(blocks) on change |
| F10 | **Contact area / conductance** | Per link | Analytic AABB overlap — see [Cell-centric → boundary-centric](#cell-centric--boundary-centric) | O(links) on change |
| F11 | **Surface / mount bits** | Whether a joint is bolted or just abutting, and whether a face seals | Per *face direction of a block*, not per cell. 6 values, not 6 × cells | O(blocks) on change |
| F12 | **Room / external detection** | "Does this face see space or a room?" | See [Room mapping](#room-mapping-is-the-one-that-has-to-change-shape) — biggest single saving | see below |
| F13 | **Grid split / merge** | Carry temperatures across | Key on position; SE2 has `UnionFind` split groups already | rare |

### Host-facing

| # | Feature | Must have | Shortcut available | Cost |
| --- | --- | --- | --- | --- |
| F14 | **Environment sampling** | Ambient, air density, wind, sun, underground | Once per grid per step, already a single struct (`EnvironmentSample`). Sun raycast can be cached across many frames — the solar occlusion interval | O(1)/grid/step |
| F15 | **Scheduling** | Fractional step accumulation | Already rate-independent (`SimulationScheduler`). Poll at `EACH_10TH_FRAME` in SE1 | O(1) |
| F16 | **Persistence** | Temperature per block, per loop, per room | Keyed by position, loop signature and room anchor; already v2 with a v1 reader | on save |
| F17 | **Thermal mass tracking** | Respond to build progress and damage | Event-driven only (`BlockHealthChanged`, `BlockBuildProgressChanged`) — never poll | on change |
| F18 | **Definitions** | Per-block-type thermal properties | Shared immutable `BlockThermalProperties` per type; never per instance | load time |
| F19 | **HUD / debug** | Temperature readout and colouring | Client-only, draw-rate not step-rate. Terminal `AppendingCustomInfo` avoids the HUD dependency | per draw |
| F20 | **Networking** | Currently unimplemented | Server-authoritative; sync only what is displayed, on demand | — |

### Things to delete

* ~~`ThermalNode.LinkIndices`~~ — **done**. It was a `List<int>` per block, written on every
  rebuild and never read by the solver; it is now a plain `LinkCount`, which is all its one
  reader wanted.
* ~~`ThermalRadiationNode.cs`~~, ~~`MyFreeList<T>`~~ and the whole legacy per-cell path —
  **done**, deleted when the live mod moved onto the model.
* ~~The alternating sweep direction and `LastTemprature` bookkeeping~~ — **done**, gone with it.

---

## Room mapping is the one that has to change shape

`RoomMapper` floods the grid's whole bounding volume, cell by cell. At SE1's 2.5 m lattice a
100 m ship is 40³ = 64 000 cells. At SE2's 0.25 m lattice the same ship is 400³ = **64 million**.
It cannot be ported as-is.

The must-have is small: *for each exposed block face, does it radiate to space or into a room?*

Ranked options:

1. **Use the host's pressurisation system.** SE1 has `IMyGridGasSystem.GetOxygenRoomForCubeGridPosition`
   — an O(1) lookup, incrementally maintained, with the engine's own door handling
   ([engine-notes.md](engine-notes.md#the-game-already-computes-airtight-rooms--highest-value)). SE2 almost certainly has an equivalent; it is
   open question 4 in the research notes. This deletes 725 lines in SE1 and the whole problem in
   SE2, **and** unlocks room air temperature, which the mod cannot model at all today.
2. **Flood a coarse lattice, not the block lattice.** Rooms are human-scale; nothing thermal
   depends on resolving a void pocket below ~1 m. Flood at a fixed 2.5 m coarse cell in both
   games. Cost becomes independent of block size and of which game is hosting. A coarse cell is
   solid when the sealing blocks overlapping it cover it.
3. **Do nothing** — treat every uncovered face as external. Cheapest, and what the original mod
   did before rooms existed. Wrong for ship interiors, but it is a legitimate low setting.

Recommendation: **1 with 2 as the fallback**, exactly the arrangement
[engine-notes.md](engine-notes.md) already proposes for SE1. The existing `RoomMapper`
becomes option 2 after being retargeted from the block lattice onto the coarse lattice — the
algorithm is unchanged, only the cell size it walks.

---

## Variable block size breaks the integrator

Explicit stability needs substeps proportional to the stiffest node, `max(ΣG / C)`:

* Heat capacity `C ∝ mass ∝ size³`
* Conductance `G ∝ area / length ∝ size² / size = size`

So `G/C ∝ 1/size²`. **Small blocks are quadratically stiffer.**

| Pair | Size ratio | Stiffness ratio |
| --- | --- | --- |
| SE1 large ↔ small grid (never mixed today) | 5× | 25× |
| SE2 0.25 m ↔ 5 m block, **on the same grid** | 20× | **400×** |

A `MaxSubsteps` of 64, as shipped, would clamp permanently on any SE2 grid mixing sizes — the
ratio above is 400×, so no cap a grid can afford closes it.
`ClampExchange` keeps the result *bounded* — it will not blow up — but bounded is not accurate,
and `LastStepWasClamped` would be true forever.

Three ways out:

* **(a) Implicit solver.** Conjugate gradient on the sparse conductance matrix. Unconditionally
  stable, deletes `MaxSubsteps` and `ClampExchange` entirely. Correct endgame; a big change.
* **(b) Multirate integration.** Substep only the stiff subset; most of the grid steps once. Keeps
  the current solver, adds a per-node substep tier and a partition pass.
* **(c) Lumping.** When a small block's coupling time constant is far below the step, it is
  already at equilibrium with its large neighbour — merge them into one node. Physically
  justified, and it *shrinks* the problem instead of growing it.

**Recommendation: (c) now, (a) if it is not enough.** Lumping is cheap, reduces node count on
exactly the grids that would otherwise be slowest, and is a topology-time decision rather than a
per-step one. Instrument `LastStepWasClamped` in SE1 first to find out whether real grids ever hit
the cap today.

---

## Proposed data structures

### Layout: struct-of-arrays, indexed by node

The solver is already half SoA — `nodeWatts`, `nodeTemperatures`, `nodeConductanceTotal` are flat
arrays — and pays for the other half. Every substep it copies temperature *out* of 8 000 class
objects and writes it back:

```csharp
for (int i = 0; i < nodeCount; i++) {
    nodeWatts[i] = 0f;
    nodeTemperatures[i] = nodes[i].Temperature;   // pointer-chase
}
...
node.Temperature = updated;                        // and back
```

Move state into the arrays and that disappears.

```csharp
// hot: touched every substep
float[]  temperature;
float[]  thermalMass;            // = SpecificHeat * Mass
float[]  radiationCoefficient;   // = ε σ A_exposed
float[]  heatGenerationWatts;
float[]  exposedArea;            // total, m²
float[]  exposedAreaByFace;      // stride 6 — used by solar and wind
float[]  watts;                  // scratch
byte[]   propertyIndex;          // → shared BlockThermalProperties table

// cold: topology only
int[]    minX, minY, minZ;       // or a packed Int3[]
int[]    extX, extY, extZ;       // extents in lattice cells
long[]   positionKey;
object[] hostHandle;             // IMySlimBlock / CubeBlockComponent, adapter-owned

// links
struct ThermalLink { int A; int B; float Conductance; float ContactArea; }   // 16 B
ThermalLink[] links;
```

`ThermalNode` survives as a **view struct** over the arrays (`readonly struct NodeRef { int i;
ThermalSolver s; }`) so the HUD, scenarios and tests keep reading `node.Temperature` unchanged.
The public API does not have to get worse to make the layout better.

### Adjacency is a host port, not a mod-owned index

Do not maintain `blocksByCell`. Both games already have a spatial index — SE1's per-cell block
array, SE2's octree — and both are better than anything the mod would build.

```csharp
interface IBlockTopologySource
{
    void GetNeighbours(int nodeIndex, List<int> results);   // or a struct visitor
    bool IsFaceSealed(int nodeIndex, int face);
    float Permeability(int nodeIndex, int face);            // SE1 returns 0 or 1
}
```

The test harness implements this over a plain dictionary; the size of that dictionary is a test
concern, not a runtime one.

This is the change that makes the model genuinely portable — the model stops caring how blocks are
stored and only consumes *(who touches whom, over what area)*.

### Surface state: per block face, not per cell

`SurfaceMap`'s dictionary goes away. `CellSurface`'s 24-bit layout stays useful but is stored once
per block, not once per cell — six sealing fractions and six mount bits.

---

## Memory at SE2 block sizes

Minor consideration, per the brief, but the numbers are worth having. 8 000 blocks, 22 800 links.

| | Current | Proposed |
| --- | --- | --- |
| Per-node state | class + `int[6]` + `List<int>` ≈ 3 heap objects, ~150 B | ~64 B in arrays, 0 heap objects |
| `blocksByCell` | ~40 B/cell | gone |
| `SurfaceMap`'s cell table | ~30 B/cell, holding both layers packed since 2026-08-26 | gone (6 floats + 6 bits per block) |
| `RoomMap` | 3 collections over the bounding volume | coarse lattice, ~1/1000 the cells in SE2 |
| Links | `List<ThermalLink>` 16 B | `ThermalLink[]` 16 B (unchanged) |
| **Heap objects** | ~24 000 for nodes alone | ~12 arrays total |

The heap-object count matters more than the bytes: SE1 runs a non-server GC on the game thread,
and 24 000 live objects per grid is collection pressure the mod does not need to create.

---

## One grid per thread, measured and built

> **Built 2026-08-24, and it ships off.** `ParallelGrids` fans the solving half of a frame across
> the engine's own workers; the two halves either side of it — the world sample and the pump state
> before, the damage, the sweeps and every shared telemetry total after — stay on the game thread,
> which is the boundary `ParallelTickTests` holds as text because no harness can construct a game
> component to hold it any other way. What a session still has to answer is in
> [configuration.md](configuration.md#solving-a-fleet-in-parallel): the engine's scheduler is not
> the framework's, a mod shares a machine with the game it runs inside, and a worker's exception has
> never had to reach a log.

[backlog.md](backlog.md) `D19` asks for the machine to be used and the game thread to be left
alone, and says *measure before adopting* — because the two figures available pointed opposite
ways. Eight thousand blocks solve in 0.128 ms, which might be under the cost of a hand-off; a
242-grid fleet spent 25.9 % of real time in the solver. Those are answers to different questions,
and `bench parallel` answers the second: **many grids, one per work item, joined every step**, which
is the shape *solve in parallel, apply on the game thread* takes.

**The hand-off is not the obstacle.** Fanning out over 242 items and joining costs **1.6–6.8 µs** a
fleet-step — 0.5 % of one grid's own step at one grid, and under a twentieth of a per cent from four
grids up. A single grid stepped through the fan-out comes back at **0.99×** what it costs stepped
directly, so the doubt the row recorded is answered: at 1,004 nodes a grid's step is 0.54 ms and a
hand-off is three parts in a thousand of it.

**What a fleet buys**, identical 1,004-node driven hulls, 32 threads:

| grids | serial | parallel | speed-up |
| ---: | ---: | ---: | ---: |
| 1 | 0.51 ms | 0.51 ms | 0.99× |
| 8 | 4.32 ms | 1.07 ms | 4.06× |
| 32 | 17.18 ms | 1.84 ms | 9.35× |
| 128 | 70.30 ms | 7.13 ms | 9.86× |
| **242** | **131.30 ms** | **12.92 ms** | **10.17×** |

**And what the thread count buys**, at 242 grids:

| threads | 2 | 4 | 8 | 16 | 32 |
| --- | ---: | ---: | ---: | ---: | ---: |
| speed-up | 1.96× | 3.87× | 7.09× | 8.09× | 10.17× |

Near-linear to eight, then 8.09× at sixteen and 10.17× at thirty-two — the signature of a pass that is memory-bandwidth bound
rather than arithmetic bound — the solver walks flat arrays and does little per element. **A
server with eight cores gets most of what a server with thirty-two gets**, which is the useful half
of that: the change does not need a big machine to pay.

**The largest grid is the floor, and a real fleet has one.** Twenty grids from 8,904 nodes down to
about 150 — a capital ship among frigates — give **3.35×** at 32 threads and 3.37× at sixteen, not ten, because the parallel step
cannot finish before its biggest item does and that one grid is 4.3 ms of a 17.1 ms serial fleet.
Per-grid parallelism therefore trades a fleet's cost for its largest ship's cost, and splitting one
grid across threads is the separate change that would move *that* floor.

**Applied to the figure that opened the question**: a 242-grid fleet at 25.9 % of real time becomes
about **2.5 %** at 32 threads and **3.7 %** at eight, before any of the other work on this page.

**Four things this does not measure**, each of which could take some of it back:

* **The engine's scheduler, not the framework's.** The mod would fan out through
  `MyAPIGateway.Parallel`, backed by `ParallelTasks`; this measures `System.Threading.Tasks`. The
  hand-off column is a lower bound on the engine's rather than a prediction of it.
* **The game-thread half.** Reading a grid's power and applying damage still has to happen on the
  game thread, and none of that is in these numbers. What is measured is the solver, which is what
  the 25.9 % was.
* **A machine of its own.** The figures were taken on a 32-thread machine with about four cores
  otherwise busy. Every row is the fastest of five repeats and background load costs the parallel
  side more than the serial one, so the speed-ups are conservative.
* **Grids that are not alike.** The ladder is identical hulls, which is the best case for any
  scheduler; the uneven row is the one to read for a server.

**What makes it safe is already true.** `FleetParallelTests` asserts that a fleet stepped one grid
per thread lands bit-identically where the same fleet stepped in order lands, and that every piece
of static state in `Core` is named with the reason two grids may share it — nineteen fields, all of
them lookup tables, geometry constants or off the stepping path. A new static field fails that test
until somebody writes its reason down, which is the shape of the defect that would otherwise arrive
as a cache and land as a race.

## Order of work

1. ~~**Land `Core/` in SE1 on the main thread, unchanged.**~~ **Done.** The live mod runs the
   model behind an adapter that mirrors block layout in, samples the world once per step, and
   takes temperatures and overheat events back out. The per-cell path, the frame quota scheduler
   and the alternating sweep are gone.
2. ~~**Instrument.**~~ **Done.** Stage costs, substeps, clamped steps, link counts and mapper
   queue depth are all in the report, and collection is switchable at runtime so a test session
   costs nothing to a shipped one. The integrator question is now one for data rather than arithmetic.
3. ~~**Boundary-centric geometry.**~~ **Done for conduction and exposure** — see [Implementation status](#implementation-status). What
   remains is block *storage*: `GridModel` and `SurfaceMap` still key on occupied cells, which is
   fine at SE1 block sizes and is not at SE2's. Replacing them means an interval or octree index
   keyed on block bounds rather than a cell dictionary.
4. **Room mapping via the host gas system, coarse flood as fallback.**
5. **Struct-of-arrays conversion.** Mechanical once step 3 has removed the per-cell arrays.
6. ~~**Adjacency as a host port.**~~ **Done** — `IBlockAdjacency`. An SE2 adapter now supplies
   neighbours from the octree without the mod maintaining any index of its own.
7. **Stiffness handling** — only once SE2 modding exists and real mixed-size grids can be
   measured.

Steps 3–6 each stand alone in SE1 and each pay for themselves there. That matters, because SE2
modding does not exist yet and this should not become a bet on it.

---

## What the variable-size work does not solve

* **SE2 has no mod API.** Adapter timing is outside our control ([engine-notes.md](engine-notes.md#platform)).
* **The lattice assumption is unverified.** If SE2 does not use one shared lattice, the AABB
  overlap arithmetic above needs a scale factor per block — a small change, but confirm before building
  on it.
* **The spread of thermal mass.** Specific heat is real J/(kg·K) under one global clock, which
  settles the unit question — but variable block sizes still put thermal mass over four orders of
  magnitude on one grid, which is what the integrator section above is about.
* **Threading.** Still not the bottleneck. The solver is about 0.14 ms per step for 8 000 blocks
  and 22 800 links, against roughly 64 ms to build and map that grid once; the costs are all
  topology. Revisit only after real in-game numbers exist.

---

# Part 2 — Scale to a million blocks

**Design only — nothing in this part is implemented.** The figure this part is designed around is a
single grid of **10⁶ blocks** that ticks inside a frame budget without stalling the game, in either
SE1 or SE2.

> **It is a stress bound rather than the target, decided 2026-08-24 from the population** (`G5`,
> [§9](#9-risks-and-open-questions)): not one of 8,132 published workshop blueprints reaches a
> million blocks in a grid, and the ninety-ninth percentile is 70,141. The design target is
> **250,000**. This part is kept as written, because a bound is what a scale design should be sized
> against and because what the population cannot see is a station grown in one world over months.

---

## 1. What a million blocks costs if nothing changes

Extrapolated from the measured 8 000-block benchmark in
[known-issues.md](known-issues.md) (22 800 links, 0.128 ms/step, 51 ms build).

| Blocks | Links | Solver step | Full topology rebuild |
| --- | --- | --- | --- |
| 8 000 | 22 800 | 0.13 ms | 0.05 s |
| 100 000 | 285 000 | 1.60 ms | 0.64 s |
| **1 000 000** | **2 850 000** | **16 ms** | **6.4 s** |

Linear extrapolation is optimistic. At 8 000 blocks the working set is ~0.5 MB and lives in L2; at
10⁶ it is ~114 MB and lives in main memory. The floor is then memory bandwidth, not arithmetic:

```
per substep:  nodes 1e6 × 44 B  +  links 2.85e6 × 28 B   ≈ 124 MB
at ~20 GB/s effective                                    ≈ 6.2 ms per substep
```

So a step is 6–16 ms *before* any stiffness substepping, against a 16.6 ms frame at 60 fps, for
**one grid**. And a single block placed triggers a 6.4-second rebuild, because `AddBlock` sets
`linksDirty` and `RebuildLinks` is O(all nodes).

Three conclusions:

1. **You cannot touch every node every step.** Not at any constant factor. The arithmetic is
   already near the bandwidth floor.
2. **You cannot rebuild topology globally.** Ever.
3. **Node count must stop being the unit of work.** Thermal *activity* has to be.

---

## 2. The one idea

> Work must scale with the number of blocks whose temperature is **changing**, not with the number
> of blocks that **exist**.

In a **steady environment** the overwhelming majority of blocks sit at equilibrium with identical
neighbours: no gradient, no heat source, nothing to compute. They are not *cheap* to simulate today
— they cost exactly as much as a reactor face — but they are *worthless* to simulate.

If 2 % of a grid is thermally active, the active set is 20 000 nodes and ~57 000 links: **0.3 ms
per step**, comfortably inside budget, on a grid 125× larger than the current benchmark.

> **Correction.** An earlier draft of this section justified that 2 % by claiming most blocks are
> *interior*. Measurement says otherwise — see [§10](#10-grid-shape-changes-the-arithmetic). On a
> solid cube 27 % of blocks are exposed; on a ship it is **73–82 %**. Ships are mostly skin.
>
> Exposure is not the same as activity, so the steady-state argument survives: an exposed block in
> a constant environment is still settled. But the *exposed* fraction is exactly the set that wakes
> when ambient conditions change, so on a real hull a day/night transition or an atmospheric entry
> wakes four fifths of the grid, not a thin shell. Staggering (§5) is therefore **mandatory, not an
> optimisation** — it is the difference between the design working and not.

Everything in the rest of this document is machinery to make that true and keep it correct.

---

## 3. Three structural changes

### 3.1 Chunking — the addressing change

Stop treating a grid as one flat node array. Partition the lattice into fixed **chunks**
(64 × 64 × 64 lattice cells — 16 m at SE2's 0.25 m lattice, 160 m at SE1's 2.5 m). A block belongs
to the chunk containing its `Min`. Every SE2 block size fits inside one chunk (5 m = 20 cells).

Nodes are stored **sorted by chunk**, so a chunk is a contiguous index range. That single ordering
decision buys spatial locality for free: neighbours in space are neighbours in memory.

Per chunk:

```
int   firstNode, nodeCount        // contiguous range
int   firstInternalLink, count    // both endpoints inside this chunk
int   firstBoundaryLink, count    // crossing into a neighbouring chunk
float meanTemperature, minT, maxT // aggregate state
float activity                    // max |ΔT| observed last update
int   lastUpdatedStep
byte  flags                       // topologyDirty, exposureDirty, sleeping, lumped
```

What chunking unlocks:

* **Incremental topology.** Placing a block dirties one chunk and at most six neighbours' boundary
  link sets. The 6.4-second global rebuild becomes O(1) amortised. This alone is the difference
  between usable and unusable.
* **A scheduling unit.** Chunks are the granularity for sleeping, budgeting and prioritisation.
* **A parallelism unit.** Internal links touch only their own chunk, so chunk interiors are
  embarrassingly parallel; only boundary links need care.
* **A persistence unit.** Only dirty chunks are re-serialised.
* **A natural fit to SE2.** The octree is already a spatial hierarchy; chunks can align to octree
  nodes rather than being an independent partition.

### 3.2 Activity tracking — the work elimination

A chunk **sleeps** when its `activity` stays below a threshold for N consecutive updates. Sleeping
chunks are skipped entirely.

Wake conditions, all local:

| Trigger | Mechanism |
| --- | --- |
| Neighbour chunk grew a gradient | Boundary link ΔT exceeds threshold → wake the sleeper |
| A block's heat generation changed | Host event → wake owning chunk |
| Topology changed | Add/remove/damage → wake owning chunk |
| Exposure changed | Room map pass altered an exposed face → wake |
| Environment changed materially | See §5 — must **not** wake everything at once |

Wake propagates across chunk boundaries at roughly the speed heat actually diffuses, which is
physically correct rather than a hack: a reactor coming online wakes an expanding shell, not the
whole ship.

**Granularity: per chunk, not per node.** Per-node activity bookkeeping costs about as much as the
work it saves, and heat diffusion is spatially coherent, so chunk-level is the right resolution.

### 3.3 Lumping — the memory and precision win

A sleeping chunk is, by definition, near-uniform in temperature. Collapse it to **one lumped node**
carrying the chunk's total thermal mass, coupled to the outside world through its boundary links
only. ~4 000 nodes become 1.

On wake, expand: every node in the chunk was within the sleep threshold of the mean, so restoring
`meanTemperature` to all of them is accurate to within that threshold by construction. If you want
exactness, store a per-node `sbyte` delta against the mean — 1 byte per node instead of 4, and it
compresses to nothing when uniform.

Lumping is what makes a 10⁶-block grid genuinely *small* rather than merely *skipped*: it shrinks
the resident working set, not just the per-step iteration.

This is adaptive mesh coarsening. The block octree is the natural hierarchy to hang it on when SE2
modding arrives.

---

## 4. Multirate stepping unifies two problems

[Part 1](#variable-block-size-breaks-the-integrator) establishes that variable block sizes make small blocks
quadratically stiffer (`G/C ∝ 1/size²`; 400× between SE2's 0.25 m and 5 m blocks), so a global step
must be sized for the stiffest node on the grid.

That is the same problem as scale, seen from the other end. Both are solved by letting **different
regions advance at different rates**.

Assign each chunk a **rate tier** — a power-of-two multiple of the base step:

```
tier 0 : every step        stiff, active, small blocks
tier 1 : every 2 steps
tier 2 : every 4 steps
...
tier k : every 2^k steps   quiescent bulk structure
```

A chunk's tier is `min(stability tier, activity tier)`: stiffness sets a ceiling on the step it can
take, activity sets how large a step it *needs*.

Two rules keep this conservative:

* **Adjacent chunks differ by at most one tier.** Standard multirate practice; prevents a stiff
  region from being fed by a wildly stale neighbour.
* **A boundary link is integrated at the finer of its two tiers**, and the energy it moves is
  applied to both sides at that rate. Because `ThermalSolver` already accumulates watts and applies
  them together, an exchange stays exactly equal and opposite regardless of tier — energy
  conservation survives, which is the property that matters most.

This replaces global `MaxSubsteps` clamping, which at 10⁶ mixed-size blocks would otherwise be
permanently saturated.

---

## 5. Per-subsystem requirements at scale

| Subsystem | Breaks at 10⁶ because | Required change |
| --- | --- | --- |
| **Topology rebuild** | `RebuildLinks` is O(all nodes), triggered by one block | Per-chunk incremental rebuild; dirty set, never global |
| **Node removal** | `RemoveBlock` does `List.RemoveAt` + reindexes every later node — O(n) per removal, and invalidates every cached link index | Stable indices with a free list and tombstones; compact per chunk, offline |
| **Conduction pass** | 2.85 M links, bandwidth bound | Active chunks only; CSR adjacency (§6) for sequential access |
| **Environment pass** | 10⁶ nodes × 5 terms | Active chunks only; already one fused pass — keep it fused |
| **Room mapping** | Floods the bounding volume; ~10⁸ cells at SE2 lattice | Host gas system, else coarse lattice **and** per-chunk incremental flood with boundary reconciliation ([Part 1](#room-mapping-is-the-one-that-has-to-change-shape)) |
| **Exposure refresh** | `RefreshExposure` loops every node on every room pass | Only nodes in chunks the pass actually changed |
| **Environment sampling** | One `EnvironmentSample` for a 1 km structure is physically wrong, and a per-grid solar occlusion flag is meaningless at that size | Sample per chunk, interpolated from a coarse per-grid field; occlusion per chunk |
| **Environment change** | A day/night transition dirties every exposed chunk simultaneously — the worst-case wake storm | Treat ambient as a slowly-varying field; wake exposed chunks **staggered** across the tier cycle, never in one frame |
| **Persistence** | 12 MB raw, 16 MB base64 per grid | Per-chunk; sleeping uniform chunk = one float; RLE over lumped regions; only dirty chunks rewritten |
| **Scheduling** | A work quota cannot bound frame time | Wall-clock budget: process chunks by priority until the budget is spent (§7) |
| **Split / merge** | Rebuilding both sides is 6.4 s each | Chunks move wholesale between grids; SE2's octree `UnionFind` already yields split groups |
| **Damage events** | Fine — inherently sparse | None |
| **Networking** | 10⁶ temperatures cannot replicate | Server authoritative; clients receive per-chunk aggregates for display, full detail only for the chunk under the crosshair |
| **Diagnostics** | `TotalEnergy` sums 10⁶ float32 — catastrophic cancellation | Accumulate in `double`, or per-chunk then combine |

---

## 6. Data structures

### Zero managed references in the hot arrays

At 10⁶ blocks, an `object[] hostHandle` is 10⁶ references the GC must **trace on every
collection**. In SE1 that GC runs on the game thread.

Keep the simulation arrays entirely blittable — `float[]`, `int[]`, `byte[]` and structs of those.
The core then contains no GC roots at all and is invisible to tracing. The adapter owns the
`int index → IMySlimBlock / CubeBlockComponent` mapping, and pays for it once, outside the
simulation.

This is a stronger constraint than the struct-of-arrays change in
[Part 1](#proposed-data-structures), and
at this scale it matters more than the byte count.

### CSR adjacency instead of a link list

Store links as compressed sparse row, sorted by node within chunk:

```
int[]   linkStart;      // per node, offset into the arrays below   (nodeCount + 1)
int[]   linkTarget;     // other endpoint
float[] linkConductance;
```

Each link appears twice (once per endpoint), which costs ~40 % more memory than the current
symmetric single-entry list but makes traversal purely sequential per node and removes the
scattered read-modify-write on `nodeWatts` that the current `for (links)` loop performs. At
bandwidth-bound scale that trade is worth taking — and CSR is also exactly the format a conjugate
gradient solver wants, if the implicit route in
[Part 1](#variable-block-size-breaks-the-integrator) is ever taken.

Keep `contactArea` out of the hot arrays; it is diagnostic only.

### Memory budget

| Array | Bytes/node | 10⁶ nodes |
| --- | --- | --- |
| `temperature`, `thermalMass`, `radiationCoefficient`, `heatGenerationWatts`, `exposedArea`, `watts` | 24 | 24 MB |
| `exposedAreaByFace` (6 × float) | 24 | 24 MB |
| `propertyIndex` (→ shared property table) | 1 | 1 MB |
| AABB: `Min` packed 21-bit long + `Extents` 3 × byte | 11 | 11 MB |
| CSR `linkStart` | 4 | 4 MB |
| **Node subtotal** | **64** | **64 MB** |
| CSR `linkTarget` + `linkConductance`, 2 × 2.85 M × 8 B | | 46 MB |
| Chunk records (~4 000 chunks × 64 B) | | negligible |
| **Total** | | **~110 MB** |

Reductions available, in order of value:

* **Lumping (§3.3)** is the big one — a sleeping chunk needs `meanTemperature` plus an optional
  `sbyte` delta per node, not 64 B. A grid that is 90 % asleep drops to ~20 MB resident.
* `exposedAreaByFace` at half precision, or derived on wake rather than stored, saves 12–24 MB.
* Blocks of identical type in identical exposure share derived values — `radiationCoefficient` is a
  pure function of `(propertyIndex, exposedArea)` and could be a small lookup.

110 MB for a maximal, fully-awake 10⁶ grid is acceptable. The design should be judged on the
*sleeping* figure, because that is the steady state.

---

## 7. Scheduling: budget, not quota

`SimulationScheduler` accumulates fractional step credit and answers "how many steps are due". At
10⁶ blocks that question is unanswerable, because one step is not affordable.

Replace with a **wall-clock budget**:

```
budget = 2 ms per frame per grid (configurable)
order chunks by priority = activity × (now - lastUpdatedStep) × tierWeight
process until the budget is spent
chunks not reached accumulate owed time and rise in priority
```

Properties this needs:

* **Bounded frame cost by construction**, independent of grid size. This is the property the
  current quota scheduler cannot provide.
* **Starvation-free** — owed time raises priority monotonically, so nothing is skipped forever.
* **Graceful degradation** — an over-subscribed server slows heat propagation rather than dropping
  frames. Heat visibly diffusing a little slower is a far better failure mode than a stutter.
* **Determinism must be chosen, not assumed.** A wall-clock budget is non-deterministic across
  machines. For multiplayer, either the server alone simulates (recommended — see the networking
  row in §5), or the budget is expressed in work units rather than milliseconds on all peers.

---

## 8. Invariants worth stating

Properties the design must preserve as it gets clever. These are the regression tests.

1. **Energy is conserved** across every pairwise exchange, at every tier boundary, and across
   lump/expand cycles. This is the one property that makes the rest safe to approximate.
2. **Order independence.** Results must not depend on chunk processing order — already true of the
   accumulate-then-apply solver, and it must survive multirate and parallelism.
3. **Sleep is invisible.** A sleeping chunk and an awake one at equilibrium must produce the same
   answer. A test that sleeps half a grid and compares against the fully-awake result is the
   single highest-value test in this design.
4. **Lump/expand round-trips within threshold.** Collapse and restore must not move total energy.
5. **Wake is local.** No event may wake more than its neighbourhood in one frame — including
   day/night, which is the one that would otherwise wake a whole hemisphere.
6. **Topology work is proportional to what changed**, never to grid size.

---

## 9. Risks and open questions

* ~~**Is a 10⁶-block grid a real target, or a stress bound?**~~ **Answered 2026-08-24 by the
  population: a stress bound.** The question was worth deciding before building and had no evidence
  under it; the corpus is evidence. Over **8,132 published workshop blueprints, not one reaches a
  million blocks in a grid.** The largest is 641,711, ten ships pass a quarter of a million, 49 pass
  a hundred thousand, and the ninety-ninth percentile is **70,141**. So *if real grids top out at
  10⁵* is very nearly the measured answer — p99 is 70k and p99.9 is 265k — and chunking plus
  activity tracking is the design the population calls for, with lumping the complexity it does not.

  **What blueprints cannot say, and it is the reason the bound stays uncapped**: a station grown in
  one world over months is never published, and neither is a hull welded out of several ships — both
  reach sizes nobody posts. So this population has a ceiling on ambition rather than on possibility,
  a million-block grid is something a player can build, and the ladder keeps running there as a
  stress bound. What changes is only which of the two numbers the design is *for*.

  **And the bound is now measurable on real blocks.** `bench franken` reads the corpus's largest
  ships and tiles their main grids into one grid until it reaches a target, so a figure at the bound
  describes a real block mixture — a capital ship's proportion of armour to machinery, its conveyor
  runs, its thruster banks — rather than census tiers dealt into a shape. See `FrankenHull`.

  **A second measurement points the same way and bites much lower.** At the configuration that ships
  after `C27`, a driven census hull in air stops keeping real time between 32,000 and 64,000 blocks
  — the element-visit allowance shortens its step — so the tuning problem the 250k figure names is
  already live an order of magnitude below it. See
  [benchmarks.md](benchmarks.md#what-the-allowance-is-worth).
* **Wake storms are the failure mode.** Environment transitions, a ship entering atmosphere, a
  large explosion, a blueprint paste. Each needs an explicit staggering strategy; without one, the
  worst case is *worse* than having no sleeping at all, because it pays wake bookkeeping on top of
  full work.
* **Sleep threshold is a gameplay knob, not just a performance one.** Too high and slow heat soak
  through a hull stops happening at all — the exact behaviour a thermal mod exists to model. It
  needs to be tunable and documented alongside `Frequency`.
* **Chunk size interacts with block size.** 64³ lattice cells is 16 m in SE2 and 160 m in SE1. It
  probably wants to be defined in metres and converted, not in cells.
* **SE2's octree may make chunking redundant** — if octree nodes are exposed to mods with stable
  identity, align to them instead of imposing a second partition. Unknowable until the mod API
  ships.
* **None of this is worth building before `Core/` runs in SE1 and produces real numbers.** Every
  figure in this document is extrapolated from bench measurements on synthetic grids.

---

## 10. Grid shape changes the arithmetic

Measured with `GridShapes` and `GridMetrics` in the harness, ~8 000 blocks of light armour each
(`l-junction` is larger; per-block columns are normalised).

| shape | nodes | bbox fill | exposed | links/node | diameter | build ns/blk | step ns/blk |
|---|---|---|---|---|---|---|---|
| solid-cube | 8 000 | 100 % | **27 %** | **2.85** | **57** | 4.87 | 20.16 |
| hollow-box | 7 778 | 15 % | 100 % | 2.00 | 108 | 7.99 | 17.99 |
| stick | 8 000 | 100 % | 100 % | 1.00 | **7 999** | 5.28 | 12.76 |
| plate | 8 010 | 100 % | 100 % | 1.98 | 177 | 2.99 | 15.49 |
| dumbbell | 6 910 | 44 % | 36 % | 2.78 | 97 | 3.26 | 19.73 |
| l-junction | 19 257 | **7 %** | 49 % | 2.71 | 398 | 8.11 | 15.82 |
| truss | 4 900 | 28 % | 100 % | 1.14 | **707** | 2.06 | 9.30 |
| **ship** | 5 125 | **13 %** | **79 %** | 2.36 | 81 | 3.19 | 12.94 |
| accreted | 8 000 | 21 % | 40 % | 2.49 | 55 | 3.91 | 14.14 |

The solid cube — the shape every existing benchmark and nearly every existing test uses — is the
**best case on three of the four axes that this design depends on**, and the worst case on the
fourth:

* **Exposed fraction 27 %, against 79 % for a ship.** This is the correction in §2. The
  environment pass does real work for three times as many blocks on a real hull, and three times
  as many blocks wake when ambient conditions change.
* **Bounding fill 100 %, against 13 % for a ship and 7 % for an L.** The room mapper floods the
  bounding volume, so per block it does **~8× more work on a ship** and ~14× more on an L-shape
  than the cube benchmark suggests. This is the strongest evidence yet for
  [Part 1's room mapping section](#room-mapping-is-the-one-that-has-to-change-shape) — the flood
  fill must not be volume-proportional.
* **Diameter 57, against 707 for a truss and 7 999 for a stick.** Diameter sets how many
  conduction hops a transient needs to cross the grid, and therefore how many steps chunks stay
  awake. An activity-based scheduler is far less effective on elongated structures — which is what
  station spines and long ships are.
* **Links per node 2.85 is the cube's one pessimism** — every other shape is sparser, so the
  conduction pass is *cheaper* per block than the benchmark implies. This partly offsets the
  above, but only partly, and not on the axes that scale worst.

Practical consequence: **the 51 ms build / 0.128 ms step figures are a floor, not a typical
case.** Any future benchmark should report the ship and truss shapes alongside the cube, and the
scale design should be validated against `stick` and `truss` specifically, because they are where
sleeping and chunking are least effective.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-08-24 | **Answered §9's first open question from the population: 10⁶ is a stress bound, not the target.** Not one of 8,132 published workshop blueprints reaches a million blocks in a grid; the largest is 641,711, ten pass a quarter of a million, and p99 is 70,141. This page's own conditional — *if real grids top out at 10⁵* — is very nearly the measured answer, so chunking plus activity tracking is the design the population calls for and lumping is not. The design target on [document-of-intent.md](document-of-intent.md#the-scale-target) is 250,000 now; Part 2 is kept as written, because a bound is what a scale design should be sized against ([backlog.md](backlog.md) `G5`). |
| 2026-08-23 | **Measured one grid per thread** ([backlog.md](backlog.md) `D19`), which the page listed as a route and nothing had priced. 10.17× on a 242-grid fleet at 32 threads, 7.09× at eight, 3.35× on an uneven fleet where the largest ship is the floor, and a hand-off of 1.6–6.8 µs against a grid's own 0.54 ms. `bench parallel` is the run and `FleetParallelTests` pins the bit-identity that makes it safe. |
| 2026-08-22 | Corrected the substep cap in the integrator argument: it named 16, which was the default when the section was written and is now 64. The argument is unchanged — a 400× stiffness ratio exceeds any cap a grid can afford. |
| 2026-08-22 | Merged `model-redesign.md` into this page as Part 1: both documents are design for the same model, and the data structures, room mapping and per-cell storage arguments were being made twice. Replaced the numbered section references with named links, so a cross-reference survives a section being added. Corrected three stale notes carried in from the older page — per-block self-shadowing is built and switchable rather than unimplemented, the sun raycast is on an interval, and specific heat is real J/(kg·K) rather than 250× below physical. Added the standard header and this log. |
| 2026-08-18 | Recorded the measured effect of boundary-centric geometry: contact area for one joint is flat at ~13 ns whatever the block size, against 987 µs at 16³ cells for the cell-walking form. |
| 2026-08-12 | Opened both design documents: the one idea (stop enumerating cells, work on block boundaries) and the three structural changes scale needs (chunking, activity tracking, lumping), with multirate stepping unifying two of them. |
