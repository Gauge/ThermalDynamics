# Thermodynamics

A Space Engineers mod that simulates heat as a first-class resource, built as a framework other
mods can stand on. Every block is a thermal node with mass, specific heat, conductivity, emissivity
and a critical temperature. Heat conducts between touching blocks, radiates into space, convects
into atmosphere and into the air of sealed rooms, arrives from the sun, and — if you let it build up
— destroys your ship.

* **Steam Workshop ID:** `2985582372` (see [modinfo.sbmi](Workshop/modinfo.sbmi))
* **Namespace:** `Thermodynamics`
* **Target:** Space Engineers 1 (`net48`, C# 6)

## Design goals

* **Light.** A grid's cost is one pass over its links per substep, and nothing else. Every readout,
  diagnostic and overlay is off unless something is reading it.
* **Isolated.** Every mechanism has its own switch, and switching one off removes exactly its own
  cost. Switches take effect on the next step, with no reload.
* **Tested.** The simulation is a pure library with no dependency on the game session, built and
  tested outside it — 2,335 tests, 33 deterministic scenarios and a load benchmark that goes to a
  million blocks in one grid.
* **Open.** Everything the simulation knows is readable and everything it does is drivable from
  another mod, through a delegate table passed by mod message. See [docs/api.md](docs/api.md).

## What it does

| System | Summary |
| --- | --- |
| Conduction | Heat flows between blocks over the area where both carry a mount surface, as two conductors in series. Crosses rotors and pistons between grids. |
| Radiation | Every exposed face radiates to ambient by Stefan–Boltzmann. |
| Convection | Exposed faces exchange with the surrounding air; wind and grid velocity raise the coefficient. |
| Room air | Sealed rooms hold an air mass that couples every surface bounding them — the only path between two walls that do not touch. Pressure comes from air vents. |
| Solar | Grids raycast to the sun; unoccluded faces absorb energy weighted by facing angle. |
| Climate | Ambient follows latitude, the ground underfoot, the hour, and how far above sea level it is, chasing its target with a lag so the day peaks after noon. |
| Weather | Rain, snow, storms and fog cool or warm the air, dim the sun, drive the wind and strip heat off a hull faster — read from the game's own authored figures per weather type. |
| Underground | Depth blunts the day out over tens of metres, then the rock warms toward the planet's core below the sea-level deadzone. |
| Point sources | Other mods register heat sources bound to an entity or a position; they radiate like small suns. A **Debug Heat Source** block drives the same path from the terminal, so the mechanism can be seen without writing a second mod. |
| Aerodynamic friction | Fast atmospheric flight heats leading surfaces with the cube of relative airspeed. |
| Aerodynamic forces | **Drag and moments rebuilt; native validation is still open.** The default-on force channel uses cached geometry and current motion each physics tick. Lift and stall remain unfinished; `EnableAeroForces` disables the channel. A ship's top speed is the game's cap, or [RelativeTopSpeed](https://github.com/Gauge/RelativeTopSpeed)'s if you run that mod. |
| Waste heat | Power producers and consumers convert a configurable share of throughput into heat; thrusters heat with throttle, which is what makes hydrogen thrusters run hot. |
| Coolant loops | Closed rings of coolant pipe with a pump form a shared fluid mass that pulls heat out of adjacent blocks; radiators shed it to space. |
| Heat pumps | The one block that moves heat *up* a gradient, for an electrical cost set by Carnot: cheap across a small difference, ruinous across a large one. |
| Damage and thresholds | Blocks above their critical temperature take continuous damage. Any other temperature can be watched by another mod. |
| Airtightness mapping | A per-grid flood fill classifies every cell as external, structure or room, which decides what counts as an exposed surface. Doors are portals, so cycling one costs a walk over the doors rather than a remap. |
| Multiplayer | Every machine simulates the same ship, and the server tells each client what its blocks are actually at: the whole ship once when the ship arrives, then whatever is near failing, a few seconds apart. Without that a client can show a block **safe** for the whole time it is burning. |
| Readouts | Terminal panel per block, cockpit summary, crosshair readout, and an x-ray block overlay — every block of the ship in front of you drawn as a box coloured by temperature, exposed faces or friction watts, a solar view that shades the ship's skin face by face — plus a room view that draws the mapped air itself, cycled with Ctrl+Shift+=, with a readout panel of the figures behind whichever view is up. |

## Required dependencies

| Mod | Used for | Workshop ID |
| --- | --- | --- |
| **Definition Extensions** (Draygo) | Reads all thermal properties out of `<ModExtensions>` blocks in the definitions. Required — without it block lookups throw. | `2756894170` |
| **Rich HUD Master** (Zach Hembree) | Every piece of text the mod draws on screen: cockpit summary, extinguisher readout, settings menu, debug readout. Degrades quietly if absent — the simulation, the terminal readouts, the chat commands and the overlay all still work. | `1965654081` |

## Quick start

1. Subscribe to Definition Extensions and Rich HUD Master, and enable all three mods.
2. Load a world and build. Heat simulates immediately; nothing is repainted and nothing is drawn
   until you ask for it. Fast flight through air heats a hull; the game's own flight model and
   speed cap are untouched.
3. Sit in a cockpit for ambient, peak temperature, peak rate of change, critical block count and
   coolant loop count.
4. Open any block's terminal for its own temperature, rate of change, exposed area, waste heat and
   the room it bounds.
5. To cool a hot subsystem, build a **closed** ring of coolant pipes containing at least one
   **Coolant Pump**, with sink faces pressed against the blocks to cool — and, if you want the heat
   gone rather than moved, against a **Radiator** with a clear view of space.
6. To cool something below what surrounds it, sandwich a **Heat Pump** between it and a radiator:
   the pump's front face draws heat out, its back face rejects that heat plus the power it took.
7. Press **Ctrl+Shift+=** to cycle the block overlay through its views, and again to switch it off.
8. Press **Ctrl+Shift+S** for the settings menu: every value in the config file, with a slider or a
   switch and a description of what it does.
9. `/thermal settings` lists every switch; `/thermal set <name> <value>` changes one live. There is
   **one configuration** and it is the most faithful one the model has: every mechanism on, nothing
   approximated, and a clock run fast enough that heat is something you can watch. The settings menu
   has a **Defaults** button if you want it all back. See
   [docs/configuration.md](docs/configuration.md).

## Documentation

**[docs/](docs/) is the documentation**, indexed by what you are trying to do in
[docs/README.md](docs/README.md). If you are here to build on this mod rather than to play with it,
[docs/api.md](docs/api.md) is the contract and [docs/development.md](docs/development.md) is how the
repository is built and laid out.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Connected rebuilt aerodynamic drag and moments to the game adapter, with an off switch, resolution settings and API readouts. Native handling and population validation remain open. |
| 2026-09-24 | The aerodynamic forces and the top-speed curve are gone while the flight model is rebuilt; a ship flies as the game flies it, and RelativeTopSpeed is the mod for per-mass top speeds. |
| 2026-08-23 | Added multiplayer to **What it does**: temperatures now replicate, and a client that used to guess is told. |
| 2026-08-22 | Moved the repository layout tree, the build-and-test section and the documentation index off this page. The stated audience is a workshop reader, with one section for modders, and all three were written for somebody who has cloned the repository ([docs/backlog.md](docs/backlog.md) `H5`). The layout and the build are now in [docs/development.md](docs/development.md); the index is [docs/README.md](docs/README.md). |
| 2026-08-22 | Brought the quoted suite size to 1,533, after a pass that added four documentation checks. |
| 2026-08-22 | Rebuilt the documentation index around what a reader is trying to do rather than the order pages were written, after a defragmentation pass took the documentation from 31 pages to 21. Every page now opens with its scope and closes with a change log; the conventions are in [docs/development.md](docs/development.md#documentation-conventions) and checked by `EveryPageHasAChangeLog`. |
| 2026-08-21 | Made the quoted suite size a claim the suite checks, and fixed the sixteen dead documentation links a first check found. |
| 2026-08-20 | Pointed the READMEs at the corpus and the lab. |
| 2026-08-12 | Opened the repository. |
