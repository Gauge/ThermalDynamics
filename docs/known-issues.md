# Known issues and limits

What this mod deliberately does not model, what is still open, and the failure patterns worth
carrying forward. Open work is tracked one line each in [backlog.md](backlog.md); this page carries
the argument behind each entry.

> The rules argued here are stated canonically in [rules.md](rules.md): `E2` `E9` `D2` `D3` `D5`
> `D6` `C9`, and the principle P14 the deliberate limits follow from.

| Looking for | Go to |
| --- | --- |
| Every open item, categorised, one line each | [backlog.md](backlog.md) |
| What the model does now | [thermal-model.md](thermal-model.md) |
| What a grid costs, and what makes it stutter | [benchmarks.md](benchmarks.md), [load-and-hitching.md](load-and-hitching.md) |
| What the corpus survey found about balance | [balance.md](balance.md) |

---

## Deliberate limits

Each of these is a simplification taken on purpose, with the price written down. A limit is not a
defect; a limit nobody wrote down is.

**Heat leaves the world with a block that leaves it, and arrives at ambient with one that is
built.** Energy conservation is one of the three solver invariants and it is a statement about a
*step*: within a step nothing is created or lost, to the last bit. The moment the block population
changes it is not a statement about anything. A destroyed block takes its energy with it, a block
ground down takes its energy with it, and a welded block arrives at the world's ambient temperature
whatever it is bolted to — so energy leaves and enters with no accounting at all.

**This is on purpose and the alternative is worse.** Conserving it means a grinder that heats the
ship around it and a welder that chills it, which is a mechanism a player would never connect to a
cause, at the cost of a redistribution pass on every block change (`P14`).

**The case it used to cost — a hull losing blocks in a fire — is the one that is now conserved**,
and it is the only one: the redistribution pass runs when a departing node is past its critical
temperature and not otherwise, so a grinder still costs nothing and still heats nothing. What the
limit costs as it stands is a block a player takes away below its rating, where the heat that leaves
with it is the heat that was in it.

`EnergyIsNotConservedWhenTheBlockPopulationChanges` pins both halves — the total falls by exactly
the departing node's energy, no neighbour moves, and a welded block arrives at ambient — because a
limit that is only described is a limit somebody rediscovers as a bug (`D5`).

**With one exception, and the exception is where the limit became an exploit.** A block that leaves
the world **above its critical temperature** hands its energy to the neighbours it was bolted to
instead of taking it away. The argument above is about a block a *player takes away*; a node past
critical did not leave, it **failed in place**, and the mod is what destroyed it. Letting its energy
go makes overheating a reward — cook a cheap block and the world is that much cooler, for free and
repeatably, which is [backlog.md](backlog.md) `B42`'s remaining half: the sacrificial block, the
grind-and-reweld timer on a glowing block, and the crudest version that needs no grinder at all
because the mod destroys the block for you.

**The test is the temperature rather than the cause**, because the cause is not knowable where the
decision is made: the game removes a block and the mod is told, with nothing to say whether a
grinder or a fire did it. Reading the temperature answers all three variants at once and leaves a
cool block ground off exactly as it was — so the grinder that heats the ship around it, which is why
this limit exists, still does not exist.

The energy is spread **by heat capacity**, so every neighbour takes the same temperature rise: that
is the mixing answer, where conduction would have carried them given time, rather than a guess at a
rate. Spreading by conductance would put more into whichever neighbour happened to have the fattest
joint, which is a statement about the path and not about where the energy ends up. A node with **no
neighbours** keeps the plain limit — a lone block that cooks itself really does take its heat with
it, and inventing a recipient would be worse than the limit (`E8`).

`OverheatSpillTests` pins it, including the exploit run as a player would run it: cook, let the mod
destroy it, weld a fresh one back, five times over, and the hull is no cooler for it.

**The lab never destroys a block, so a peak temperature above critical is not a prediction.** The
solver raises an `OverheatEvent` when a node passes its critical temperature, but applying that
damage is `bound.Block.DoDamage` in `ThermalGridSimulation` — the game layer, which no harness runs.
In the lab an overheating block is therefore never removed: it keeps generating, keeps conducting to
neighbours, and keeps climbing for whatever remains of the clock. In game it would be gone in
seconds and would stop producing.

That censors every number drawn from the right tail. The corpus survey reports one ship at
541,648 K and a parked mobile base at 31,151 K; neither is a temperature the mod can reach, because
the block that got there does not survive to be measured. **Read any peak above critical as "this
block dies" and nothing further**, and treat the population's peak statistics — p95, p99, max — as
describing the harness rather than the mod. `over_critical`, `over_share` and `seconds_to_critical`
are unaffected: they are decided at the crossing, before the divergence matters.

`seconds_to_first_loss` is unaffected for the same reason and only for the **first** loss. Up to the
moment a block's hit points run out the harness and the game agree exactly, because nothing has been
removed from either. After it they part company, which is why the column records the first loss and
no count of losses: a second one would be measured on a ship the game would no longer have.

Closing it properly means the harness modelling destruction — removing the node, re-deriving the
graph, and stopping the source — which is a solver-wide change to answer a question the censored
reading already answers.

**A refused substep demand is an approximation on every path now, and on two of them it was a
divergence.** Fixed 2026-08-24, and it was [backlog.md](backlog.md) `A10`. The pairwise overshoot
clamp bounds one exchange at the energy that brings *that pair* to equilibrium, which is the whole
bound a block needs and half the bound a lumped mass needs: a coolant parcel carries a link to every
pipe on it and a room's air a link to every surface bounding it, so their links together could take
several times the energy that equalises them. The other end had the same shape — a pipe with a sink
face is a node pulled on by the parcel and by everything it is bolted to. Each bound held and the
node went past both. Measured on the fixture where the plumbing sets the demand, at 9.7× over-
subscribed with the ring mixing: **1.3 × 10²⁵ K before, 1,799 K after**, against 335 K granted in
full. The room path was the same defect and never had a test on it: a thin room refused one substep
of thirty reached **3,839 K** of spread on a hull that started 300 K apart with nothing making heat.

The fix is the per-node relaxation the conduction pass already used, applied to the coupled passes
too — every exchange at a node scaled so their sum cannot exceed the energy that equalises it, which
makes a substep a convex combination of the temperatures pulling on that node and so unable to leave
the range they span. It is inert while the demand is granted, which is every step on every grid this
mod ships to except the ones over `MaxSubsteps`, and the block ladder below is unmoved by it to
three decimal places. `RefusingTheRingsDemandApproximatesRatherThanDiverging`,
`NoNodeIsDrivenPastTheHottestThingPullingOnIt`, `RefusingTheAirsDemandApproximatesRatherThanDiverging`
and `TheClampIsInertWhileTheDemandIsGranted` pin the four halves of that.

**The global substep ceiling used to bind in thick air at flying speed, and `C24` closed it.**
`MaxSubsteps` grants 64. In vacuum a 49-ship panel of real hulls asked 7.1 at p95 and nothing was
close to it; at 200 m/s in thick air the same panel's p99 demand was **73.4 and 14 of 50 hulls were
refused**, every one at 1.14–1.15× over-subscribed. A refused step is integrated at the ceiling with
the overshoot clamps bounding each exchange, and it cost **0.028 K** on the hottest block over 600
simulated seconds.

At the pair that now ships the same 40-hull panel's worst p99 is **35.12 of 64** — 55 % of the cap,
**0 of 40 hulls refused** in every environment, and 60 % projected to 300 m/s. The demand this
criterion is decided by is convection-limited, so it came down with the clock, and `G6` passes on
the shipped configuration. What went the other way is vacuum, where the same hulls demand 1.6× what
they did: nothing near the cap, but the element-visit allowance is a different bound and the retune
does reach it. **`C27` measured that and found the premise the wrong way round** — the allowance
binds in *air*, at about a third the grid size vacuum needs, and what it costs was priced and the
default doubled. See [configuration.md](configuration.md#what-a-shortened-step-costs).

The trade the old breach represented is kept in
[configuration.md](configuration.md#the-approximation-that-shipped-on-and-no-longer-does), because
it is the reasoning a future retune would need again rather than a fact about what ships.

**A grid the element-visit allowance binds runs its thermal clock slow, and that is the largest
approximation this mod ships.** `MaxElementVisitsPerStep` grants 4,000,000 element visits to one
grid's step, and a step that costs more is spread over more frames rather than being refused
anything — so nothing is *coarsened*, no exchange is clamped, and every substep is exactly as
faithful as it would have been. What is lost is time: the grid's heat advances slower than the
world's.

**It sat outside this list until 2026-08-24 because "no accuracy is lost" reads as "nothing is
lost", and `C27` priced the difference.** A slow clock is worth **0.00 K on a parked ship** — two
hulls heading for the same equilibrium agree once they arrive — and under a load that is *moving*
it is **1.19 K standing at a 5 % deficit and 36.98 K at 60 %**. Beside 0.028 K for the substep
ceiling this world accepts and 0.607 K for the per-block cap it refuses to ship, that makes this the
biggest thing the mod gives up by default and the last of the three to be measured.

**Where it binds is air, not vacuum, and at about a third the grid size.** A settled, driven census
hull kept all of real time to about 32,000 blocks in vacuum, 16,000 on a planet surface and 9,000 in
flight at the 2,000,000 this setting carried before `C27`; seven ships in eight in the corpus are
under 8,904 blocks and never reach it in any world. `0` removes the bound and is the faithful end of
the ladder — the shipped default is deliberately not that end, which is the one place the defaults
are not the most faithful configuration the model has, and
[document-of-intent.md](document-of-intent.md#where-the-goals-and-the-code-disagree) carries why.
See [configuration.md](configuration.md#what-a-shortened-step-costs).

**Planet and asteroid shadow is per grid; only other grids shade individual faces.** A grid's own
shadow is per face (`SolarSelfShadowing`) and so is another grid's (`SolarGridShadows = full`), but a
planet's or an asteroid's dims the whole grid by the share of sampled rays that were blocked
(`SolarOcclusionSamples`). A capital ship crossing a terminator therefore ramps rather than
switching, but never carries a shadow edge across its own hull.

**It is the top rung of [backlog](backlog.md) `A9`, and it is now priced.** The cost objection that
justified the limit — *per-block would need the ray count to scale with block count* — survives for
terrain and voxels and does **not** hold for the planet: `OcclusionMath.IsOccludedBySphere` is a
normalise, a dot and an `atan` with no ray in it, so evaluating it per exposed face is arithmetic on
a pass the self-shadow already walks. What was never measured is what it would be worth, and read
per block that is **linear in hull length at about 0.0018 K a metre**: 0.07 K on a 25 m hull, 0.29 K
at 150 m, 1.11 K at 600 m and 4.53 K at 2,500 m, on the worst-placed block of a crossing. The
cadence the rung does not touch adds about half a kelvin whatever the hull, so **below 300 m the
interval is the larger half of the error and above it the geometry is**. *(Every figure here scales with
`HeatTimeScale` and moved when `C24` took it from 225 to 90: the geometry did not change, the kelvin
a second of sunlight buys did. The change log below carries what they were.)* The limit therefore stays taken, with a
number on it rather than an argument: see [configuration.md](configuration.md#external-shadow).

**Point sources are not occluded.** A registered heat source heats through walls and through other
ships. Occlusion is left to the host, which can simply not register a source it knows is hidden.

**A room that changes shape loses its air temperature.** Rooms are matched across rebuilds by their
lowest cell. Building inside a compartment gives it a fresh air mass at the temperature of its
walls. The same key carries air across a save, so a compartment rebuilt while the world was closed
comes back at the temperature of its walls rather than the one it was saved at.

**Build state does not change a block's thermal properties.** A block at 10% construction has the
same mass, heat capacity, conductivity and mounting as a finished one, and nothing notifies the
simulation when a block finishes building. This is a deliberate simplification rather than an
omission: a partially-built block is a transient a player watches for seconds, the thermal
difference would be invisible next to the heat its neighbours carry, and tracking it would mean a
per-block event on the construction path plus a rule for what a half-built block conducts. The
machinery to support it exists — `RefreshBlock` handles a geometry change correctly and cheaply —
so this can be revisited by hooking build state to it, and nothing else would need to change.

**The *same mass* half of that is an assumption rather than a measurement**, and it may already be
false. `SweepMass` polls `IMySlimBlock.Mass` every eight steps, which is a poll rather than the
event this paragraph says nothing raises — so if the game reports a partially-built block as
lighter, its heat capacity is already following build state and this limit is describing a mod that
no longer exists. Conductivity and mounting are unaffected either way. It is
[backlog.md](backlog.md) `F24`, and only a session answers it.

**A surface is two constants, not a spectrum.** Emission and absorption are separate numbers now —
`Emissivity` and `SolarAbsorptivity`, the second following the first unless a definition declares it
— so a block *can* be made shiny to the sun and black to space ([backlog](backlog.md) `B27`, closed).
What is still a simplification is that each is one constant: a real selective surface is a curve
against wavelength, and this is that curve reduced to a value in the visible and a value in the
thermal infrared. No shipped block declares the two apart yet.

**A coolant loop's `Conductivity` is a 0…1 quality against a 200 W/(m·K) reference**, where a
block's is the real W/(m·K) figure a materials table gives. The fluid-to-wall path is convective,
and its honest dial is a heat transfer coefficient in W/(m²·K) — a change to the loop equations
rather than to a number. See [definitions.md](definitions.md#conductivity-is-in-real-wmk).

**Mount coverage is combined as independent fractions.** Where both sides of a joint are only partly
mounted, the bolted area is `coverage_a × coverage_b`. The model records coverage per face, not per
cell, so it cannot know whether two partial mounts line up.

**Radiators cannot be inline loop segments, and `C40` says what that is worth.** A radiator sheds
heat when a pipe's sink face is pressed against it, which works and is what the `radiator` scenario
measures. It has no coolant ports of its own, so a loop cannot run *through* one. The block is 1×5×2
with mount points only on its top and bottom, so the ports would go there and the geometry would
need checking against the model.

**What stopped this being built is the price rather than the plumbing.** Running coolant through a
radiator is a way of getting heat *into* it faster, which is internal transport — and `C40` measured
the ceiling on every internal path at **9.13 % of the peak**, because the hottest block on a plumbed
hull already sheds **1,457 W/K** into the hull it is welded to and stands only 39.2 K above its
grid's median. The shipped loop reaches 2.43 % of that ceiling; the `C38` candidate raises the loop's
40 W/K to 250 against the hull's 1,457. An inline radiator competes in the same term, so it cannot
be worth more than a fraction of nine per cent however good the coupling is.

*That is a bound and not a measurement of this change: nobody has run it.* It is written here so the
next reader prices it before building it, which is the mistake `C36` records three independent levers
making — the panel's emissivity, the fluid's coupling and the coolant's mass all measured large on a
bench with no hull and nothing on a real one.

**The underground core gradient is out of reach in ordinary play.** Below `SealevelDeadzone` the
rock warms toward `CoreTemperature`, and the shipped deadzone is 2 km below sea level — deeper than
SE's voxels go. The model is right and the tuning lever is documented; as shipped, every reachable
depth reads a flat `UndergroundTemperature`. Whether the default should be a few hundred metres is a
balance question, not a code one.

---

### Failed telemetry teardown can poison the next world load

The 2026-09-20 session log records Telemetry.Reset throwing NullReferenceException at 12:25:31.720
inside Session.UnloadData, followed by the engine's "Failed to cleanly unload session" report.
The next load emits duplicate definitions, then fails in MyBlockVariantGroup.ResolveBlocks at
12:25:37.532. The same process completes block-group initialization at 12:10:40 and 12:17:48.
This sequence strongly implicates incomplete teardown; it is not proof of which object was null
inside Reset, because the game stack has no source line and may inline calls.

The installed ResolveBlocks consumes its pending ID array and sets it to null. Calling it again
on an already-resolved object can produce the observed exception. Both variant groups shipped by
this mod have nonempty Blocks arrays and all 14 references resolve against the shipped cube-block
SBCs. The pre-existing cross-size pairing warnings also occur on successful loads, so changing
block IDs or variant membership is not justified by this crash.

Session teardown now runs each cleanup stage independently and logs each failure through MyLog,
so a failed report/reset cannot skip light removal, callback unregistration, definition cleanup
or the base unload. Telemetry detachment tolerates null grid entries and absent simulations;
these are defensive checks, not a claim to have reproduced the exact null from the game.
SessionCleanupTests injects a reset failure and a second cleanup failure, verifies later stages
still run and both errors are reported, and checks the actual Session.UnloadData routes through
that boundary. The helper requires a nonthrowing reporting callback; the adapter uses MyLog.

Offline validation: the full Release solution compiles with zero errors against the installed
SE1 assemblies; 91 selected tests pass with zero failures/skips, including three SessionCleanupTests
and documentation, whitelist-syntax, telemetry and glow checks. The command uses the filter
`FullyQualifiedName~SessionCleanupTests|FullyQualifiedName~DocumentationTests|FullyQualifiedName~ScriptWhitelistTests|FullyQualifiedName~TelemetryAnomalyTests|FullyQualifiedName~TelemetryFormatTests|FullyQualifiedName~ThermalVisionTelemetryTests|FullyQualifiedName~HeatGlowStyleTests`
with the build/test procedure in [development.md](development.md#building). Local output is in
`/tmp/thermal-unload-build.log`, `/tmp/thermal-unload-tests.log` and
`/tmp/thermal-glow-results/unload-regression.trx`. The injected failures exercise the shared cleanup
runner, not the live engine or the unidentified null inside Reset; no frame-time claim is made.

Recovery from an already-aborted unload requires a complete game exit and fresh launch, rather
than another reload in the same process. A post-fix live restart/reload remains to be observed.
The subsequent AccessViolationException in this incident does not, by itself, establish damaged
hardware or a corrupt save. Evidence source: SpaceEngineers_20260920_121018166.log.

### Heat glow is a surface approximation

Armour and blocks without emissive materials receive the drawn soft heat glow. Only the additional
emissive-material write depends on model support. Radial patches use exposed bounding faces;
they do not follow slopes, holes, deformation or moving subparts exactly. Depth testing prevents
ordinary through-wall patches, while shadowless heat lights can leak through walls. The 4,000-quad
and 32-light limits can omit effects in overloaded scenes. No live game visual acceptance is
claimed for this replacement; see [thermal-glow.md](thermal-glow.md) for its verification scope.

### The whitelist is not the assemblies, and the build checks a copy of it

The mod project builds the mod's sources against the installed game's own assemblies, which is
what makes it a real compile rather than a guess — and compiling is **not** the check the game
applies. Space Engineers compiles a mod with a Roslyn analyzer over a positive whitelist, so a type
can exist in `Bin64`, resolve, compile clean, and be refused in a session.

**Since the MDK2 move the build runs a whitelist analyzer too**: `Mal.Mdk2.ModAnalyzers` reports a
prohibited type or member as `MDK01`, an error (`rules.md` `C2`). It reads MDK2's own snapshot of the
whitelist rather than the installed game's, so a game update can move ahead of it;
[tools/thermal-api-check](../tools/thermal-api-check/README.md) asks the installed game directly.

**Measured, from the game's own registration.** `SpaceEngineers.Game.MySpaceGameDefaultIlChecker`
is where the whitelist is built, and the shape of it is what matters:

* **`System` itself is not an allowed namespace.** Only the types named one by one in its
  `AllowTypes` call are permitted — `object`, `string`, `Math`, `Enum`, the primitives, `DateTime`,
  `TimeSpan`, `Array`, `Nullable<>`, `IComparable`, `IEquatable<>`, `Action`/`Func` and so on.
  **`IFormatProvider` is not among them**, which is the one this was found by: `Units.Watts` took
  one as a parameter and the mod would not compile in game.
* Whole namespaces *are* allowed: `System.Collections`, `System.Collections.Generic`, `System.Text`,
  `System.Text.RegularExpressions`, `System.Globalization` and `System.Linq`, plus
  `Sandbox.Game.Entities`, `Sandbox.Game.EntityComponents`, `Sandbox.ModAPI`, `VRage.Game.Entity`,
  `VRageMath` and the rest of the mod-facing surface.
* So `CultureInfo` is fine and the interface it implements is not, which is not a distinction
  anything on this machine would have drawn.

**`ScriptWhitelistTests` is the guard**, and it would have caught this one. It reads every file the
game would compile, finds the names written in a *type* position — which is the only way to tell a
type from a field called `Uri` — and reports any framework type the whitelist refuses. It judges the
framework surface alone and drops any name the game or the mod also declares, because a `Color` is
`VRageMath.Color` in nearly every line here and `System.Drawing.Color` in none of them; that blind
spot is the price of not burying every real finding under three hundred false ones.

## Open defects

**Temperatures were not reconciled between server and clients. They are now, and the transport is
the one part of it no test can reach.** Clients run their own simulation from the same inputs, and a
client that joins mid-session starts from saved temperatures; nothing corrected the difference.
`ThermalGridSync` does: a client that has finished building a grid asks the server to state it, the
server answers with every block, and after that it states the near-critical band every
`TemperatureSyncInterval` seconds. Both halves and their interval come from the measurements below,
and the protocol classes are `HotTailMessage`, `HotTailSchedule` and `HotTailState` under
`Core/Sync`, all of which are game-free and tested.

**What is still open is the session.** Registration, addressing, the sync-distance gate and the send
itself are host code that cannot run outside a live server, so none of it is covered by a test and
none of it can be. What stands in for one is a pair of counters: `/thermal sync` prints what this
machine has sent, asked for, refused and applied, and running it on both sides is what says which
half is not moving. The mechanism has a switch for the same reason — `EnableTemperatureSync` — and
the failure it guards against is the one this repository keeps finding: a mechanism correct
everywhere it is exercised and inert everywhere it runs.

Damage and settings remain server authoritative, so nothing a client believes changes what happens
to the ship. What a client believes is what it is *shown*, which is the whole point.

**Measured, and it is not as cosmetic as it reads.** `-- drift` runs the same hull twice from states
a stated distance apart and watches the disagreement decay — the model is dissipative, so a client
converges on the server without anybody correcting it, and the question is how fast. On the census
hull in shadow, a client joining sixty simulated seconds behind:

| Hull | Wrong by, at the join | Under 10 K | Under 1 K | Wrong about *critical* |
| --- | ---: | ---: | ---: | ---: |
| 1,500 blocks | 37 K | 75 s | 190 s | **150 s** |
| 8,904 blocks | 112 K | 200 s | 375 s | **305 s** |

Air is much faster than vacuum — 75 s to agree within a kelvin on a planet surface against 190 s in
shadow — because convection is a far stronger path to a shared ambient than radiation is. Staleness
saturates: a state from thirty minutes ago is no worse than one from five, because the hull had
settled by then.

**The last column is the finding.** For two and a half to five minutes a client's readout is on the
wrong side of a block's critical temperature — and [balance.md](balance.md#how-long-a-block-has-after-it-crosses)
measures the whole event, from the load to the first block gone, at a median 37 s. A client can
therefore show *safe* for the entire lifetime of the event that destroyed the block, several times
over, which is the readout being wrong about the one thing it is for.

**And a per-grid correction would not fix it.** The error is not a uniform offset: at the join the
worst block is 105 K out against a mean of 20 K, so a single scalar per grid would correct the
armour and leave the blocks that matter wrong. What has to be replicated is the near-critical tail —
1,700 to 2,000 blocks of 8,904 on this hull, which makes 12.1 kW a block against a real median of
335 W and is therefore an upper bound on how long that tail is. What was built from it, and what
is still untested about it, is [backlog](backlog.md) `B30`.

### The protocol is measured, and the near-critical tail alone is not it

`HotTailCodec` is the packet: the blocks inside the band the glow already draws — the last 100 K
before each block's own critical temperature — at ten bytes each, keyed by grid position rather than
by node index, because node indices come from block insertion order and two machines do not build a
grid in the same order. `-- drift --correct` runs it against a server and charges it for the drift
it allows: every reading is taken at the end of an interval rather than after an update, and the run
advances by the finer of the two cadences, both of which were flattering the protocol before they
were fixed.

**On the census hull, correcting the tail on an interval does not close the gap**, and the reason is
not the interval:

| what is sent | interval | misreading | wrong blocks, mean / worst | bytes a second |
| --- | ---: | ---: | ---: | ---: |
| nothing | — | 560 s | 75.0 / 648 | 0 |
| the band | 5 s | 145 s | 7.3 / 648 | 6,151 |
| the band | 60 s | 185 s | 10.6 / 648 | 513 |
| **every block** | 5 s | **5 s** | 5.4 / 648 | 18,861 |
| **the whole hull once, then the band** | 60 s | **0 s** | **0.0 / 0** | **670** |

Replicating every block continuously fixes it and costs thirty times the bandwidth, which is what
says the residual is **the un-replicated hull rather than the update rate**: the corrected blocks
conduct to neighbours that are still stale, and a block crossing into the band arrives with its
client-side twin far behind. Stating the whole hull **once**, when the client joins, removes the
same residual for one packet — 94 KB on a 9,430-node hull — after which the band is tracking rather
than repairing, and even a sixty-second interval leaves the readout right for the whole run. The
steady cost is the band alone; the join packet amortises to 157 B/s over ten minutes and to nothing
over a session.

**A budget makes it worse, not cheaper.** Capping an update at 1,000 blocks on a hull whose band is
3,075 takes the misreading from 145 s to 240 s, because the 2,075 blocks cut are exactly the ones
the correction existed to carry. The budget's own blind spot is counted and printed rather than
inferred (`P2`); what it is for is a hull whose band does not fit a packet at all, and on this hull
it does.

### What was built from that measurement, and what it costs

The shipped protocol is the last row of the table above and nothing else. `ThermalGridSync` runs a
pass every thirty frames — half a second, deliberately coarser than a frame and finer than the
interval it serves — and for each live grid states it to each client that is owed something and is
inside the world's sync distance. Outside that distance there is no grid on the client to correct.

**The client asks; the server does not offer.** Only the client knows when it has finished building
a grid, and a snapshot that arrives before it has is a snapshot every record of which is dropped for
naming a block that does not exist yet. A request that goes unanswered is repeated with a doubling
backoff capped at a minute, because a client that gave up would hold a stale hull for the rest of
the session — the exact defect this closes. A request repeated inside five seconds is refused, so a
client asking in a loop cannot make the server transmit a 94 KB hull per frame.

**A hull larger than one message is sent as several, and each is complete.** There is no sequence
number and nothing to reassemble: a slice is a whole legal packet, applied on arrival, so a lost one
costs its own records rather than the hull. The bound is 2,048 records — under 21 KB — and it is a
packet-size limit rather than a rate limit, because a message the transport refuses is a correction
that never arrives.

**No budget.** Capping an update is measured above to make the misreading *worse*, from 145 s to
240 s, because the blocks a cap drops are exactly the ones the correction is for. What a hull too
large for one message gets instead is more messages.

**An empty band is not transmitted.** A ship with nothing near failing has nothing to correct, and
the alternative is a header per grid per client per interval for the whole population of a server,
forever. So the steady cost is the size of the emergency rather than the size of the world: nothing
on a quiet ship, and about 6 KB/s per client in range on the hottest hull in the corpus while it
burns.

**On its own secure channel**, `30325`, for the reason `SettingsRequests` is on `30324`: the shared
channel's sender id is a field the sender wrote, and the engine's secure handler supplies one the
transport verified plus a from-the-server flag a client cannot forge. Temperatures that did not come
from the server are dropped, and a request that reaches a client rather than the server is dropped —
either would be one client writing onto another's simulation.

**What the envelope costs.** Every figure in the table above is the codec's bytes. The wire adds ten
bytes per message for a marker, a kind and the grid's entity id — the grid has to be named, because
block positions are only an identity inside one grid — which at the shipped interval is 2 B/s.

### A client's inputs are a different defect from a client's state, and only one of them decays

Everything above is a **perturbation** — one wrong initial state — and it decays because the model
is dissipative. A client's *inputs* are not perturbations. `-- inputs` degrades each one a client
drives its own simulation from, alone and together, and separates the two by whether the
disagreement is still there at the end of the run:

A 2,000-block hull in sunlit vacuum, ten minutes, the load alternating every two minutes so a lag
has something to lag. **Still wrong at the end** is the mean disagreement over the final third.
`thrust error` and `speed error` are absent because this hull is not flying and they read zero here;
their figures are below, on the `burn` scenario that is theirs:

| what is degraded | peak | still wrong at the end | misread, uncorrected → corrected | why that is what the engine does |
| --- | ---: | ---: | ---: | --- |
| nothing | 0.0 K | 0.00 K | 0 → 0 blocks | the control: two clients on one world must agree exactly |
| joined 60 s stale | 20.0 K | **0.00 K** | 85 s → **5 s** | restoring a save; it happens once and decays |
| environment sampled 2 s late | 1.3 K | 0.41 K | 30 s → 25 s | position and orientation replicate on their own schedule |
| sun 5° off | 1.9 K | 1.18 K | 210 s → 130 s | replicated orientation trails the server's |
| thinner air | 0.0 K | 0.00 K | 0 → 0 | nothing to disagree about in vacuum; a planet run is what tests this |
| compartments a fifth emptier | 1.9 K | 0.90 K | 4 → 3 blocks | the game's gas system answering differently, which is worth almost nothing until it reaches zero |
| block power 5 % out | 38.0 K | **24.40 K** | 166 → 62 blocks | whatever the game's own block replication rounds |
| block power 2 s late | 245.5 K | **30.05 K** | 397 → 399 blocks | a throttle change reaching the client late |
| loses 5 s of every 30 | 233.9 K | **49.68 K** | 395 → 397 blocks | a machine running fewer simulation ticks, and simulated time is counted in ticks |
| its clock runs 10 % slow | 25.6 K | **18.69 K** | 120 → 54 blocks | sim speed below 1.0 on one machine and not the other — every input right, and elsewhere on the same curve |
| a heat source it never heard about | 1.1 K | 0.73 K | 7 → 4 blocks | another mod's API registration, which nothing replicates |
| shade for 3 s of every 30 | 16.4 K | 3.52 K | 12 → 11 blocks | its own raycast on its own budget; wrong by the whole solar term while it lasts |
| block masses 20 % out | 43.2 K | **32.11 K** | 184 → 86 blocks | `SweepMass` is a rota on both machines, and mass is heat capacity |
| never got the settings | 160.0 K | **120.30 K** | 625 → 273 blocks | a fetch that never landed; other physics entirely |
| no room map for 60 s | **474.7 K** | 0.01 K | 527 → 282 blocks | the flood fill has not published, so the hull has no interior — and then it has, and the client is right |
| same blocks, different order | 0.0 K | 0.00 K | 0 → 0 blocks | two machines do not build a grid in the same order, and the packet is keyed on position rather than index |
| a tenth of the hull missing for 60 s | **1,151.7 K** | 0.08 K | 1,078 → 656 blocks, 223 absent | a paste still streaming, or a subgrid that has not attached; it is a different ship, not a worse reading of this one |
| a tenth of producers off | 385.0 K | **245.87 K** | 333 → 169 blocks | a switch on the wrong side, which is wrong by *all* of that block's heat |
| all of it at once | 1,154.9 K | **214.76 K** | 1,248 → 670 blocks | the union of the rows above, computed not written |

The sweep reports one more column than the table above: **absent**, the most blocks the server had
that the client did not. Only the topology row has a number in it, and that is what the row is —
every other degradation here is a client holding a wrong value for a block both machines have.

**The hull's compartments hold air, and giving them the air a crewed ship has took about a seventh
off every standing error in this table.** The census hull is built with four sealed rooms and the
host owns whether they are pressurised (`C9`), so a harness that never says leaves them in vacuum —
which is what every figure before 2026-08-24 was measured on. Air is a **mixer** rather than a sink:
it couples every surface bounding a compartment to every other at 30 kW/K, so a per-block error gets
averaged across a room before it is read. Measured on the same hull settled under load, the air
moves the *hottest block* 203 K and the hull mean 3.8 K, and overheat damage is taken off the
hottest block.

**The third column is the finding.** A stale join settles at nothing whatever it peaked at; a wrong
input settles where the input puts it and stays there. So the convergence argument that made this
defect look cosmetic covers exactly one of these rows, and it is the one that was measured first.

**The correction narrows a bias without removing it.** Against the combined case it takes the
standing error from 214.8 K to 142.6 K and cuts how much of the hull is misread, 1,248 blocks to 670 —
but the client's inputs are still wrong, so it re-diverges between updates. Against a bias the
interval is the lever and five seconds is not enough; against a perturbation the join packet is the
whole answer, 85 s to 5 s.

**Read the seconds column with the block counts beside it.** *At least one block wrong* is a harsh
binary on a hull of two thousand: `never got the settings` reads 490 s both uncorrected and
corrected while the blocks misread at once fall from 625 to 273. The count is the figure that moved.

**The control row found a defect in the correction itself.** With the packet applied unconditionally,
a client that was *exactly* right went to misreading one block for ten seconds of the run — because
the wire carries tenths of a kelvin, and writing a received value onto a node that already matches
it moves the node by up to half a quantum, which is enough to flip a block sitting on its own
critical temperature. A block already agreeing to within the packet's own resolution is now left
alone: the correction has to be able to do nothing. It is pinned by `HotTailTests`, and it is why
the control row is run at all.

**What none of this models**, stated rather than assumed: two instances of one solver in one
process, so there is no latency, no loss, no send queue, and no engine behaviour behind any degraded
input. Every magnitude in that table is a knob the lab turns, not a figure measured from a session.
What it answers is the shape — which inputs bias, which perturb, and whether the correction reaches
each — and the shape is what decides the protocol.

### The sweep is not the whole input surface, and here is what is missing

The simulation reads its state from three places: an `EnvironmentSample` the client builds itself
from the world around it, block state the adapter reads off the game's own blocks, and the room map
it floods locally. Enumerated against the code rather than remembered, **the sweep covers twenty
inputs and leaves one**:

| input | read from | covered | |
| --- | --- | --- | --- |
| initial temperatures | the save | yes | `stale join` |
| step schedule | `SimulationScheduler` | yes | `hitching` |
| settings | replicated | yes | `wrong settings` |
| sun direction | grid orientation | yes | `sun angle` |
| ambient and air density | `PlanetManager` | yes | `thinner air` |
| block electrical power | `MyResourceSourceComponent` | yes | `power lag`, `power error` |
| whole-sample staleness | the client's own tick | yes | `environment lag` |
| thrust | `block.CurrentThrust` | yes | `thrust error`, on the `burn` scenario — physics state, *predicted* on a client rather than replicated, and a separate heat term from electrical power |
| grid velocity and relative wind | `EnvironmentSample.GridVelocity` | yes | `speed error`, on the `burn` scenario — the classic multiplayer prediction error, and it drives both convective cooling and aerodynamic friction |
| solar occlusion | raycast against voxels and grids | yes | `wrong shadow` — a *binary* flag over the whole solar input, resolved against world state a client holds differently |
| block mass and integrity | `SweepMass`, a rota | yes | `mass error` — swept every 8 steps and capped at 4,096 blocks, so a large grid's masses lag by design on *both* machines and the two rotas are not in step |
| block enabled and functional state | the game's block | yes | `blocks off` — a block turned off on one machine and not the other, which is a binary *per block* rather than per hull |
| altitude, depth, latitude | grid position | yes | `position lag`, on the `descent` scenario — position is *predicted* on a client like velocity, and on a ship holding station a stale position is the right position, which is why this needed a scenario that comes down |
| weather and its intensity | the game's weather | yes | `wrong weather` — server-driven world state with no prediction behind it, and **the largest single-input divergence the sweep can express** |
| **the ten wind fields** | terrain and the wind solver | **no** | shelter, burial and channelling are all voxel-derived |
| **room air pressure** | the game's gas system | yes | `room pressure` — `C9` says the mod reads the game's answer, so this is the input the mod least owns, and it is *binary*: worth almost nothing until it reaches zero |
| **the room map itself** | a local flood fill | yes | `room map lag` — publishes atomically, so a client mid-pass holds no interior at all; `D2` measures the pass at 3,934 ticks — about eleven minutes — on a million blocks |
| **topology and subgrid attach** | block add and remove | yes | `build order`, `blocks missing` — placement order changes the index space and nothing else, and a missing block changes the conduction graph rather than a value in it |
| **coolant loop identity** | loop signatures over topology | yes | `CoolantLoopTests` — the signature is an order-independent hash of the ring, so build order cannot move it and one pipe more is a different loop |
| **registered heat sources** | the mod API | yes | `missing source` — a registry another mod writes into, with no replication behind it, so a client can be beside a furnace it does not know exists |
| **simulation speed** | the host's own tick rate | yes | `slow clock` — the mod counts simulated time in *simulation ticks*, so a machine running fewer of them has a thermal clock that runs slow; `hitching` is the same deficit in lumps |

**Two of the three closed on 2026-08-28, and they answer opposite ways.**

*Position is covered and does not matter.* It needed a scenario that changes altitude before it
could be measured at all — on every scenario the sweep had, a client a few seconds behind about
*where* is not behind about *what the air is doing* — so `descent` brings a ship down for the length
of the run and a five-second lag is fifty metres, a fifth of a kelvin of ambient at four kelvin a
kilometre. The hull follows it: **0.8 K at worst and 0.00 K standing**, a perturbation that decays.

*Weather is covered and is the worst thing in the table.* A client that has not been told what the
sky is doing cannot work it out — unlike velocity and position, there is nothing to predict from —
and snow is −18 K on the target, a tenth of the sunlight and 2.2× the convection. Measured at
**168.8 K at worst and 87.1 K standing** on the 2,000-block descent, against a stale join's 0.00:
it is a bias, it is an order of magnitude past every other environment input, and no amount of
patience fixes it.

*(Both figures are from a run where nothing on the server went past critical, so the readout columns
judged nothing and the table says so. The kelvin columns are what stand.)*

The row still open is the ten wind fields, tracked as [backlog.md](backlog.md) `F19`: shelter,
burial and channelling are all voxel-derived, so a client computes them from terrain it holds rather
than reading them from anywhere. It does not change the protocol — the correction overwrites state
and so does not care which input produced the disagreement — it changes how much correcting there is
to do.

**A speed error reaches the cubic term, not the saturating one.** A client's velocity is predicted
rather than replicated, and it feeds two terms of very different shape: forced convection saturates,
so a fifth more speed is a few per cent more cooling, while aerodynamic friction goes as the *cube*
of airspeed, so the same fifth is 1.7× the heating. Measured, a 20 % speed error settles a flying
client **13.8 K** from the server — a bias, and the asymmetry the
[300 m/s constraint](balance.md#the-300-ms-constraint) is about, arriving as a client's guess.

**The binary input is the loudest per step and among the quietest in what it leaves behind.** A
client whose raycast puts the hull in shade for three seconds of every thirty peaks **16.4 K** from
the server and settles at **3.5 K**: wrong by the entire solar term while it lasts, and gone
afterwards, because the model is dissipative and the disagreement ends. The 5-second correction
barely touches it — 3.52 K to 3.51 K — since it decays on its own anyway. A client that is
*permanently* on the wrong side is the bound rather than the description, and it is a bias: peak
12.4 K against a standing 11.9 K on the smaller rig the tests use.

**Thrust is the worst input *per unit of error* in the sweep, and it is not close.** Compared at the
same 5 % error on a flying hull, a wrong thrust settles the client **17.5 K** from the server against
block power's **1.8 K** — an order of magnitude, on the term that is *also* the largest on a burning
ship. And it is a pure bias: at 20 % its peak and its standing error are the same 70.1 K, so it never
decays at all, and the 5-second correction only halves it. A hull at rest shows none of it, which is
what says the knob reaches the thrust term and nothing else. The reason is in the table above — the
engine predicts physics state rather than sending it, so a client's thrust is its own guess about a
ship whose physics it is not running. `ClientInputTests` pins both halves.

**A wrong switch is the worst of the sweep's own rows, and it is not an error in a number.** A block
that is off draws no power and makes no waste heat, so a client holding the switch on the wrong side
is not wrong by a percentage of that block's heat — it is wrong by *all* of it, on some blocks and
not others. Only a *bound* beats it: a room map that never lands settles at 290.95 K against this
row's 245.87 K, and that is a client which never finishes a flood fill rather than one that is 60 s
behind. A tenth of the producers silenced peaks **385.0 K** and
settles at **245.9 K**, ahead of `wrong settings` at 120.3 K, and it is most of what `all at once` is
made of. **Where the error lands is what decides that, not how much of it there is**: at equal
missing wattage — a tenth of the producers making nothing against every producer making a tenth less
— the concentrated case settles **54.7 K** out against the spread case's **13.5 K** on the smaller
rig the tests use. The hull cannot conduct fast enough to average a dead thruster away, and the
readout is per block.

**A mass error is the one input that is not an error in a heat flow at all.** Mass is heat capacity,
so it moves the divisor rather than the watts — and capacity does not appear in the balance a hull
settles at, only in how long it takes to get there. So its shape is set by the *load* rather than by
the input: 20 % out settles **32.11 K** under the sweep's alternating load, and on the smaller rig
33.8 K under that same load against **0.0 K** when the load stops moving, from a 14.6 K peak. Both
of those runs are in the dark with the same degradation and differ only in the load script, so the
difference cannot be anything else. It is also the only degradation in the sweep that comes from a
rota running on *both* machines rather than from something the client alone gets wrong.

**Mass is the only channel block condition has into the model, and whether anything comes down it
is unsettled.** No path in the mod reads a build ratio or an integrity figure — `ClientInputTests`
pins that by scanning the mod's sources, because a claim about what code does *not* do rots the moment
somebody adds the line — so the sweep has a mass knob rather than a separate damage one. What that
leaves open is the engine end: `SweepMass` polls `IMySlimBlock.Mass`, and whether the game moves it
with build progress or with damage decides whether block condition is an input at all. **The
deliberate limit above assumes it does not** — *a block at 10 % construction has the same mass* —
and that assumption predates the rota, which is a poll rather than the event the limit says nothing
raises. Nothing offline can settle it; it is [backlog.md](backlog.md) `F24`.

**Room air pressure is a binary input wearing the clothes of a continuous one, and the last one per
cent of it is worth more than the first ninety-nine.** Pressure is the game's answer rather than
this model's (`C9`), and it reaches the simulation twice: it scales the air's heat capacity, and it
decides whether the room has air *at all*. Only the second of those moves anything that lasts — a
link's conductance is `RoomConvectionCoefficient × faces × cellFaceArea` and carries **no pressure
term**, so a compartment at a fiftieth of an atmosphere couples its walls exactly as hard as a full
one and differs only in inertia, which is the `mass error` finding arriving through another input.
Measured on the smaller rig: a client that believes the compartments are a fifth empty settles
**0.40 K** out, 99 % empty settles **1.96 K** out, and *empty* settles **9.11 K** out. On the sweep
hull the same knob is 0.90 K at a fifth and **110.3 K** at zero. So the input the mod least owns
turns out not to be graded at all: it costs nothing until it crosses zero, and then it costs the
whole coupling. `RoomAirCouplingTests` pins the mechanism and `ClientInputTests` the discontinuity.

**A room map that has not landed is the loudest input in the sweep and leaves the least behind.**
The mapper publishes atomically — `RoomMapper.Map` is never partially built — so a client mid-pass
is not holding a rough map, it is holding the previous one, which on a grid it has just built is
*empty*. `RoomMap.IsExternal` answers true for every cell it has no room for, so the whole interior
becomes sky: **17,762.5 m² of exposed skin against 14,012.5 m², 26.8 % more**, and no compartment
holding air. A client 60 s behind its own flood fill peaks **474.7 K** from the server in vacuum and
**912.7 K** in air — the largest single-input peak measured anywhere in this lab, and larger in air
because the extra skin *convects* rather than only radiating — and then the pass lands and it
settles at **0.01 K**. It is a perturbation, and the shape is the opposite of `blocks off`: the
loudest thing here is the one the correction has least reason to chase.

**The bound is the other way round, and it out-settles every other input.** A client whose pass
never lands — `D2` measures the flood fill at 3,934 ticks, about eleven minutes, on a million blocks, and a grid that
restarts faster than it finishes publishes nothing — is a bias, and at 290.95 K standing on the
sweep hull it is worse than the wrong switch's 245.87 K. Its two halves decompose: losing the air is
110.3 K of it and believing in a quarter more skin is the remaining 180.7 K, so **the skin is the
larger half**. That is what makes the room map unlike every other row here — the others are wrong
about a number both machines hold, and this one is wrong about how much hull there is.

**The same ship received in a different order is no difference at all, and that is a design
decision rather than luck.** A node's index is its arrival order, and two machines have no reason to
share one — a client takes blocks in whatever order the engine streams them, a server has them in
the order they were welded or pasted. `HotTailCodec` spends eight of its ten bytes a block on a
position key for exactly this. Measured against the alternative rather than asserted: on a hull
whose 1,003 of 1,004 blocks changed index, the position-keyed packet leaves the client **1.2e-4 K**
out and the same packet applied by index leaves it **492.0 K** out. The residual is float summation
order rather than physics — a node accumulates from its links and float addition is not associative
— and it is an eight-hundredth of one quantum of the wire it travels on. **The order independence
the threading work rests on is a claim about physics, not about bits**, and this is where the two
part company.

**A block a client has not been told about is not a block it is wrong about.** Every other row in
the sweep degrades a number both machines hold; a client still receiving a pasted blueprint, or one
whose subgrid has not attached, holds *fewer numbers* — a different node set, a different conduction
graph, and hull surfaces open to the sky where the missing blocks would have covered them. A tenth
of the hull missing for 60 s is the **loudest row in the sweep at 1,151.7 K**, and most of that peak
is not the missing hull at all: it is the *arrival*. A block appearing on a grid starts at the
world's default temperature, because the simulation has no history for it and nothing tells it what
its neighbours hold, so a tenth of a hull at 1,000 K gains a tenth of itself at 293 K in one step.
That decays, to 0.08 K. **And the correction cannot reach what is absent**: it takes the misreading
from 235 s to 80 s and the blocks misread at once from 1,078 to 656, and the 223 absent blocks are
223 both times, because the packet carries temperatures for blocks and a block that is not there
takes none of them.

**The bound is a bias and it is the worst input measured anywhere in this lab.** A tenth of the hull
that never arrives settles the client **380.5 K** out in vacuum and **740.7 K** flying, ahead of a
room map that never lands at 290.95 K and a wrong switch at 245.87 K. Which is the ordering worth
reading: **the three worst inputs are the three that are not errors in a number**, and they get
worse in that order as the disagreement moves from how much heat a ship makes to what shape it is.

**Coolant loop identity is the one place the mod keys state on shape, and it behaves the way the
topology rows say it should.** A loop's signature is an order-independent hash of every pipe
position in its ring, so build order cannot move it and two machines arrive at the same identity —
and a ring one pipe longer is a *different* loop, whose temperature a save keyed on the old shape
does not land on. That is intended: an index-keyed loop would let a reload put one loop's coolant
into another. `CoolantLoopTests` pins both halves.

**A rate difference is worth exactly what the load is doing and nothing else, and it is the one
degradation where the client's inputs are all correct.** The mod advances a fixed sixtieth of a
simulated second per *simulation tick* rather than per real second — `ThermalGridScheduler` passes a
constant frame length and `Session` runs on `MyUpdateOrder.Simulation` — so simulated time is
counted in ticks, and a machine executing fewer of them per real second has a thermal clock that
runs slow. That is correct on one machine, where the whole world slows together, and a divergence
between two. The client is not wrong about anything; it is *elsewhere on the same trajectory*, which
is why no amount of dissipation closes it. Measured on the smaller rig, a 10 % clock error settles
**19.25 K** out under a load that keeps moving and **0.00 K** under one that stops, from a 0.55 K
peak — both runs in the dark with the same degradation, differing only in the load script. Two hulls
heading to the same equilibrium at different speeds agree once they arrive.

**And `hitching` is that same deficit arriving in lumps, which changes the peak and not the
settling.** Five seconds lost of every thirty *is* five-sixths rate. At an equal deficit the lumpy
case peaks **206.0 K** against the smooth one's **39.7 K** and the two settle at 39.6 K and 33.7 K —
so the average deficit decides where a client ends up and the delivery decides how far wrong it gets
on the way.

**The mechanism written beside `hitching` for months was one the mod cannot perform**, and that is
worth recording as a defect rather than as a correction. It said the client dropped a solver backlog
at `SimulationScheduler.StepsDue`'s per-frame cap. `ThermalSimulation.Update` is what paces a step:
it banks work credit against the frame it is handed and discards credit above one step's worth,
which at a constant sixtieth cannot bind at any legal `Frequency`. The accumulator that *did* drop a
backlog was a second, parallel one on `SimulationScheduler`, called by no shipped path, tested on its
own terms, and pointed at by two labs and two pages as though it were the live one. It is removed,
and the step-rate tests now run against the path the game drives — nothing in `Data/Scripts` broke
when it went, which is the whole of the evidence that it was dead.

**A heat source the client never heard about is a small standing bias, and it is the only input here
that comes from outside this mod.** `ThermalHeatSources` is a registry another mod writes into
through the API; a registration is a call made on whichever machine that mod runs its logic on, and
nothing replicates it. A source worth a tenth of the sun settles a client **0.73 K** out and stays
there.

Settings and pump controls *are* replicated. `SENetworkAPI` 2.0 runs on channel `30323` with three
properties on it: the world's settings and the two pump throttles.

The settings property is seeded with the loaded settings at construction rather than left at null,
because **a null value is never transmitted** — a property sitting at null answers a joining
client's fetch with silence, and since the server publishes only when a setting *changes*, a world
where nobody touched the config would leave every client on the shipped defaults for the whole
session.

A received value is **copied into the live settings object rather than swapped for it**. A grid
takes its core settings once, at construction — `new ThermalSimulation(Settings.Instance.ToCore(),
Model)` — and notices later changes only through that object's `Revision`, so replacing
`Settings.Instance` would leave every grid already on the client running the settings it was born
with. The copy goes through `Settings.Names()`, the same list the settings menu uses, minus
`Settings.ClientOwned` — the four presentation switches a client owns for itself, since a server has
no business choosing which overlay is on someone else's screen. That leaves 77 of the 85 serialized
fields replicated; of the other eight, seven are the client-owned presentation switches and the
last is the config file's `Version`, which describes the file rather than the world.

**Admin changes from a client travel on their own secure channel, deliberately.** SENetworkAPI
registers the game's non-secure message handler, so every sender id it reports is a field the sender
wrote — a modified client can claim to be anyone, and the API's own documentation says not to gate
admin actions on it. Settings requests use the engine's `RegisterSecureMessageHandler` on a channel
one above the shared one, where the transport supplies the sender identity and a from-the-server
flag that cannot be forged. Replies are ignored unless that flag is set, so a client cannot fake the
server's answer to another client.

**The heat pump's electrical hookup is only checkable in game.** The simulation half is under test
offline. The half that makes it cost anything — a `MyResourceSinkComponent` attached in code during
`Init`, because an upgrade module has no definition field for one — cannot be exercised without a
session, so whether the grid's resource distributor picks the sink up is unverified.

**A grid saved with the old heat pump block loses it on load.** The heat pump was a `CubeBlock` and
is now an `UpgradeModule`, because only a functional block has a terminal to switch it from. Nothing
was lost by doing it: the old block had no behaviour at all.

**`SweepRoomPressure` is per room per cadence, unbudgeted** — measured, instrumented, and cheaper
than it was. It makes two game API calls per compartment every eight steps, bounded by compartment
count rather than block count. Every other whole-grid pass in the mod is a rota or a budgeted slice;
this is the one that is not.

A field dump (six grids, 7,976 blocks, 118.5 s) puts it below the noise: the sweep runs in
`AfterSteps`, outside every row of the cost table, and backing it out of the totals leaves **64 ms
in 118.5 s — 0.054% of real time** for the sweep *plus* the mass sweep, overheat damage, threshold
crossings, the heat-pump publish and the hottest-node scan together. The rota is **deliberately not
built**: the measurement says it is a station-scale risk with no evidence behind it, and the sweep
is now timed as `of which room pressure` with counters for compartments visited, game calls made,
vent scans and vents walked — so the next dump from a large station answers the question with a
number rather than an argument.

**The first step of a grid's life is about one and a half times an ordinary one** — 12.5 ms against
a steady 8.3 at half a million blocks, measured with the JIT warm — since the step prologue moved
onto the tick that rebuilds the topology (2026-09-04, `bench firststep`). The "first touch of every
flat array" this paragraph used to blame was measured at one to two milliseconds of a 26 ms spike:
the bulk was the full node mirror and the link-mass fill, which now land with the rebuild, plus
~11.6 ms of JIT that is per process rather than per grid. **The sunlit case closed the same day**: the
sun-shadow sets are bitsets published by swap, so the first sunlit step is 26.0 ms and 6.5 MB
(a pending list bought once and kept) and a sun-drift rebuild allocates nothing. `D4` is done.

**A grid holds about 723 bytes a block, against a design budget of ~110 bytes a node.** Measured
2026-09-04 at 126,731 blocks: 88.9 MB retained, 100.8 MB peak — from the 213 and 278 this
paragraph carried, which predated the passes that keyed the tables on packed longs, turned the
sets into bitsets and recycled the room map. Nothing is indexed by *bounding volume* any more;
what still scales with enclosed volume, the room cells, is 21 B/block between 126k and 505k. [memory.md](memory.md)
has the breakdown and the changes that roughly halve it — none of which help SE2, where the problem
is that three structures are indexed per cell rather than per block.

**Block storage is still per cell, which is what stands between the model and SE2.** The geometry,
the conduction graph and the integrator all work from integer AABBs and cost the same whatever a
block's volume — `Se2LatticeTests` pins that. `GridModel.blocksByCell`, `SurfaceMap`'s cell table and
`BlockInstance.Cells` do not: they are one entry per occupied cell, so a 5 m block on SE2's 0.25 m
lattice would cost 16,000 dictionary entries and an 8,000-element array. See
[scale-design.md](scale-design.md#cell-centric--boundary-centric).

**The room map floods the bounding volume, which a hull fills about a fifteenth of.** It is
budgeted, so the cost is ticks rather than a stall — but at a million blocks it is 7,000 ticks to
converge, which is twenty minutes on a stale map. Bounded and wrong is better than unbounded and
wrong; it is still wrong. See [scale-design.md](scale-design.md#room-mapping-is-the-one-that-has-to-change-shape).

**There is no thermal view, and one is wanted.** Mods get no shader, no post-process and no frame
buffer, so the only way to recolour the world is to blank it and redraw every body as a billboard —
which gives coarse terrain, discs for asteroids, and nothing at all for anything the mod does not
draw. An earlier heat overlay was built on that basis and removed; the x-ray block overlay keeps the
part of that work worth keeping, since a debug view *wants* to see through a hull.

**This is an obstacle rather than a decision**, and it was recorded here as a deliberate limit for a
while, which was wrong: the difficulty is real and the intent to have one stands. The routes worth
investigating are in [document-of-intent.md](document-of-intent.md#thermal-vision--wanted-method-unknown),
and none of them is straightforward. Tracked as [backlog](backlog.md) B24.

**Nothing gives a player feedback without an instrument.** There is no sound emitter, no particle
effect and no emissive code anywhere in the mod, so a ship that is overheating looks and sounds
exactly like one that is not until somebody opens a terminal. Against a measured **8.9 s** median
from full electrical load to critical, that is the gap between a warning and a post-mortem. Tracked
as [backlog](backlog.md) B25.

**The cost of terrain occlusion has never been measured in game.** It is ten height lookups per
sample per interval for grids near a surface, which should be well under the voxel raycast it sits
beside, but no dump has confirmed it. The `solar occlusion` timing in a telemetry report is where it
would show.

---

## Failure patterns worth remembering

**This section is evidence, not rules.** Each entry is the defect that produced a standing lesson,
and where that lesson is a rule this repository is bound by, the rule is stated once in
[rules.md](rules.md) and this is the page it points back to (`R13`). Reading it the other way round
is what the heading used to invite — it said *each of these is stated as the rule it produced* — and
two of them had drifted into restating a rule's own sentence beside it.

They are grouped by the shape of the failure rather than by the subsystem, because the shape is what
repeats. Four produced a rule and name it; the rest are engine behaviour or model behaviour that
cost a defect once and is worth not paying for twice. Dates are in the [change log](#change-log).

### A mechanism can be correct everywhere it is exercised and inert everywhere it runs

**Room air was tested only through the door nobody used.** Every test and the mod API set a room's
pressure through `ThermalSimulation.SetRoomPressure`, which rebuilds the room's links to the blocks
bounding it, seeds air appearing for the first time from the temperature of those walls, and
recomputes the conductance totals. The game's own sweep assigned `room.Pressure` directly and called
`RefreshThermalMass` by hand — the only caller in the codebase that did — so in a live world a
pressurised room got its air mass, **no links at all**, and whatever temperature the last rebuild
left behind, which for a ship in vacuum is 2.7 K. The whole suite passed over a feature that did
nothing in game. The room dump now reports a `links` column, so air with mass and no coupling says
so.

**Every block this mod ships ran on the default thermal properties, in game only.** All eighteen of
the mod's own entries in [Cubes.xml](../Thermodynamics/Content/Data/Cubes.xml) declared themselves under
`<TypeId>CubeBlocks</TypeId>`. There is no such object builder type — the blocks are `CubeBlock`
(pipes, radiators) and `UpgradeModule` (pumps, heat pumps) — so Definition Extensions matched none of
them and every one took the fallback path to `DefaultThermodynamics`:

| Block | Authored | Ran as |
| --- | --- | --- |
| Radiator | emissivity 0.35, area ×1.25, specific heat 900 | 0.125, ×1, 450 |
| Heat pump | waste 0 / 0 | 0.05 / 0.05 |
| Coolant pipes | conductivity 1 | 0.6 |

The radiator is the block that mattered: emissivity and the area multiplier *are* the block, so it
was shedding at 28% of the authored rate with none of the area bonus. The heat pump was worse than
wrong — the solver already puts every watt it draws into the hot side, so a 0.05 consumer fraction
on a pump drawing its full 20 kW invented 1 kW of heat a second time.

**No test could see it**, because the solver suite builds its own `BlockThermalProperties` in code
and was right about every equation; the numbers it was handed in game came from a file nobody parsed
offline. `ShippedDefinitionTests` now reads the shipped XML and cross-checks each entry's `TypeId`
against the block's own `.sbc`. Its radiator assertions compare against the default entry rather
than against literals, because silently *becoming* the default is the failure being guarded.

**A settings toggle can have a menu entry, a config field, a label and no reader.**
`DebugWindRaycast` sat in the file, in `Names()`, in `ClientOwned` and on the Debug page reading
"Draw wind vector" from the day the wind field was written, and nothing anywhere read it. Switching
it on drew nothing. Found by grepping for *readers* of a setting rather than by using it — which is
the check worth running over the whole settings list, and is now `SettingsWiringTests`.

*The rule it produced:* `D2` — hunt for what is built, documented and reached by nothing.

### One definition read by two parsers drifts, silently, in both directions

Block thermal properties are read twice: `ThermalCellDefinition` asks Definition Extensions for them
in a live session, and `ShippedBlocks` reads the XML directly so the harness can build from what the
mod ships. Neither can be the other — the first needs a session and the second must run without one.

`HeatSourceWatts`, the term for a block that is hot because of what it *is* rather than because of
power crossing it, was only ever known to the offline reader: it has a class of tests, a place in
the properties type, a clamp and a conservation proof, and a mod author declaring a smouldering
wreck would have got 1,234 W in every test in this repository and **0 W in a world**.
`ExcludeFromSimulation` went the other way — the in-game reader has always read it and the offline
one never did, so a block type excluded in `Cubes.xml` would have been simulated by every benchmark,
every corpus run and every scenario, and by nothing in a game.

`BothParsersKnowTheSamePropertyNames` compares the two name lists in both directions. It is textual,
because the in-game reader cannot be linked into the test project — which is the same reason the two
parsers exist, and therefore the reason a check on them has to be.

*The rule it produced:* `D3` — where one thing exists twice, a test compares the two.

### A number can be right in the solver and attached to nothing

**Every reactor in the game made no heat, and the whole suite was green over it.** `Cubes.xml` gave
the `Reactor` type `ProducerWasteEnergy` 0 and `ConsumerWasteEnergy` 0.25. A reactor delivers power
through `MyResourceSourceComponent`, so only the producer fraction can ever reach it: the largest
heat source a ship has was inert.

Nothing in the solver was wrong, so no test of the solver could see it — the simulation correctly
integrated a load of zero. Which of the two fractions applies is decided by the game's component
wiring, not by the definition, so an entry can be internally consistent and still be attached to
nothing. The check that catches it asserts on the *type's* producer fraction rather than on any
block's behaviour, because behaviour was never the thing that broke. See
[balance.md](balance.md#reactor-waste-heat).

**`LargePrototechReactor` is the same shape, unfixed.** The game gives it the TypeId
`HydrogenEngine`, so the derivation charges a 400 MW plant a combustion engine's 0.60 waste
fraction: 240 MW of heat out of a 3×2×2 block. It needs a per-subtype override — see
[backlog.md](backlog.md).

*The rule it produced:* `D2`, from the other side: the number was reached by nothing.

### A diagnostic that reports zero while working is worse than no diagnostic

**The substep mass floor was computed from its own previous answer.** `ApplyThermalMassFloor` raises
a light block's *integration* capacity so it stops demanding more substeps than `MaxSubstepsPerBlock`
allows. It compared each node against the mirrored row `nodeThermalMass[i]` — but `SyncNodeState`
only refreshes a row whose node is dirty, so on a settled grid the row still held the floor this
same pass wrote last step. Two consequences:

* `FlooredNodes`, reported as **blocks raised by cap**, counted only the nodes a pass *moved*. After
  the first step there were none, so it read `0` on every step while the floor was doing all of its
  work — on the same page as its own projection that the configured cap of 3 raises 804 blocks.
* Because the pass could only ever raise, the floor became a high-water mark, re-raised against its
  own previous output rather than sized from the block. A 20 kg fitting on heavy armour settled at
  **1.94 substeps demanded against a cap of 3** — wasted accuracy in the other direction.

**The behavioural half is smaller than it looks, and worth stating so nobody re-derives an alarm
from it.** A node's stability rate is conduction plus radiation, and on a real ship conduction
dominates — a live dump attributes 100% of its substep demand to conduction. Conduction is
temperature-independent, so the stale row usually held the same floor the block deserved: a 16 kg
fitting in light armour on a grid that had been at 1200 K and one that never left 400 K integrated
**bit-identically**. The ratchet was real, is fixed, and was inert wherever conduction sets the
floor.

`AFlooredGridDemandsExactlyItsCapAndNotLess` reads the raw demand through `NodeSubstepDemand`, which
divides by the block's real capacity, so the floor is not asked to confirm its own work.

**Convection was reported before the atmosphere blend, not after** — a reporting fault only, and the
first attempt at fixing it was wrong. A field dump showed `convection W/m2K 50.0` beside
`air density 0.0000` at 44 km, which reads as a hull convecting in a vacuum. It was not:
`EnvironmentState.ConvectionCoefficient` is the planet's figure scaled by wind and weather, and the
solver blends it by `AtmosphereFactor` at the point of transfer, because the same factor weights
radiation *down* as it weights convection *up*. Measured on one 200 kW block, the transfer was
correct throughout — convective watts fall 50,000 → 49,401 → 30,562 → 412 → 0 as density falls
1 → 0.25 → 0.01 → 0.0001 → 0, while radiation rises to take over.

Applying the factor at the coefficient would have been a *second* application and squared the blend:
0.47 instead of 0.68 at quarter density. A settled-temperature test written to catch the imagined
defect failed against correct code, which is what exposed the mistake — **thinner air does not make
a block hotter** over most of the range, because thin air is also much colder (294 K at sea level
against 101 K at a twentieth), and a weak coupling to a cold sink beats a strong coupling to a warm
one. The same block settles at 321 K at sea level, 241 K at a twentieth, and 553 K only in true
vacuum. `ASettledTemperatureIsNotMonotonicInAirDensity` pins that so the alarm is not re-derived.

### An invariant documented at three call sites will be missing from the fourth

**A grid welded past its buffer capacity went NaN, whole.** The per-node arrays — temperatures,
mirrored heat capacities, the watts a substep is accumulating — grow when the node count passes
their capacity, and growing reallocates every one of them. They are refilled by `SyncNodeState`,
which runs when a step *begins*. A step already in flight carried on over the zeroed rows, divided
its watts by a heat capacity of zero, and published the result onto every block on the grid.

The trigger is a block placed on a frame the step is not finished with, on a grid with no headroom
left in its buffers. A step spans fifteen frames at the shipped settings, so the window is most of
the time; the buffers grow by a quarter plus sixteen, so a ship being welded crosses the boundary
regularly. `BufferGrowthTests` reproduces it in twenty milliseconds. Three call sites documented
*"a step in flight is abandoned when the grid changes shape"* and the fourth, which changes the
grid's shape most violently, did not.

### Two models of the same thing disagree, and losing the argument is silent

**This model's rooms are pieces of the game's.** Sealing here comes from each definition's
pressurisation table read cell by cell; the game's test knows the real shape of a sloped block. Where
they differ the flood fill walks in from outside, the compartment stops existing, and because
pressurisation is only ever asked about rooms the map already found, nothing notices.

Three separate defects came out of that gap, each measured before it was fixed:

* **A vent could only speak for the room it stands in.** Pressurisation read the air vents and gave
  air to the compartments a vent physically touched. Measured: twelve mapped rooms on one ship, the
  game holding air in nine, two with a vent against them, seven left empty. The level now comes from
  `IMyCubeGrid.GasSystem.GetOxygenRoomForCubeGridPosition` per room, which answers for every
  compartment whether or not anything is bolted to it; the vents are the fallback when the gas
  system cannot be read.
* **A vent reported to one room and it was not always the right one.** `ReadVents` stopped at the
  first room found on the first cell of the vent, so a vent in a bulkhead between two compartments
  gave one of them air and the other nothing — decided by the order the six faces are indexed in.
  Measured on a ship with two vents in the same bulkhead: an eight-cell space took the air and the
  thirty-cell cabin, with both vents on it and the game reporting it sealed and 99% full, ran at
  zero pressure. A vent now reports to every room it touches.
* **Sealed is not full.** The first version of the dry-room flag asked `IsRoomAtPositionAirtight`
  and `IMyAirVent.IsPressurized`, both of which answer *is this room sealed*, and treated the answer
  as *should this room have air*. On a ship in vacuum every sealed cupboard nobody had piped air
  into came back airtight and empty — correct in both models — so eight of twelve compartments were
  flagged as faults and painted magenta, burying the one that mattered.

**When the comparison was finally run, the map was right**: 12 compartments found, zero held only by
the game, 11 of 12 agreeing with `IsRoomAtPositionAirtight`. That is the argument for measuring
before fixing — the sealing test looked guilty from the counts alone and was not. The comparison now
runs every dump and is reported per compartment. See
[thermal-model.md](thermal-model.md#diagnostics).

*The rule it produced:* `C9` — the game's own answer is read, never overridden.

### A guard has to test what it claims to test

**A lag needs to know it has no history.** Ambient started each session at the `VacuumTemperature`
its state was seeded with and took three minutes of play to reach the real climate, dragging every
block on every grid with it — one measured grid fell from 257 K to 103 K in nineteen seconds.
`ClimateModel.Follow` guarded against this with `if (current <= 0f) return target`, which never
fired, because 2.7 is not zero. A guard against an uninitialised value has to test whether the value
*was* initialised, not whether it looks unreasonable.

**A scale applied to a lagged value compounds against the lag.** Ambient chased its target with a
45-second first-order lag and was then multiplied by an air-density factor — but the *scaled* value
was what the next step chased from, so the factor reapplied every step. A snowfield 5.6 km up
reported 36 K for an entire session while two sea-level sites nearby were correct to a tenth of a
kelvin, because their density rounded the factor to 1.0000 and hid it. Everything that decides a
temperature now produces a *target*, and the lag is applied to that target exactly once, last.
Pinned by `ThinAirDoesNotCompoundAgainstTheLag`. The arithmetic is in
[environment.md](environment.md#ambient-temperature).

**Config defaults belong on the fields, not in a factory method.** A world's config file has no
element for a setting added after that file was written, and the XML reader leaves such fields at
`default(T)` — so every setting added since a world was first loaded ran as `false` or `0` in that
world, silently. A test world reported `SolarOcclusionPlanets False`, `SolarTerrainRange 0` and a
room overlay span of one kelvin while its owner had changed none of them. Bumping the file version
would only have papered over it, and thrown away real customisation each time. The defaults now live
on the field declarations, where a reader that finds nothing leaves them alone.

### Block placement is not a main-thread-only path

The game builds pasted and projected grids on worker threads, so everything reachable from
`ThermalGrid.AddBlock` runs concurrently with itself. Four separate unguarded collections were found
that way, and the field symptom is always the same: `NullReferenceException` out of
`Dictionary.Insert`, and every block of the affected type silently failing to become a node.

**Two shape caches on the block-placement path were unguarded.** `ThermalBlockCatalog` locks its
model dictionary and deliberately builds outside that lock, so as not to serialise the worker
threads the game pastes grids on — and what it builds calls into `ThermalCoolantShapes.Get` and
`ThermalHeatPumpShapes.Get`, both of which wrote to a plain `Dictionary` with no lock at all. A
field dump logged three such exceptions in the first tenth of a second of a world load. Both caches
now take a lock on the same discipline as the catalogue: probe under the lock, build outside it,
publish under it.

The same fact produced the definition catalog's shared scratch list and the shared telemetry
registries, both fixed the same way.

### Assigning `NeedsUpdate` from a grid logic component breaks ship control

`[MyEntityComponentDescriptor(typeof(MyObjectBuilder_CubeGrid), true)]` makes the component's
`NeedsUpdate` property the *grid entity's* update flags. Assigning to it clears whatever the grid set
for itself, and `MyCubeGrid` re-arms `EACH_FRAME` only when its scheduled-update queue goes from
empty to non-empty — so clearing it once stops that queue being drained for the rest of the session.
The visible symptom is ship control: `MyGroupControlSystem` recalculates the controlling cockpit from
that queue, so sitting down gives "Someone else is using this ship!" forever.

**Use `|=`, and stop work with a flag of your own rather than by taking the entity's updates away.**

### A repair has to cost what changed, and no more

**A block whose mounting changed kept stale conduction links** — and it was the opposite of what a
first reading suggested. `RefreshBlock` did not rebuild the conduction graph. It did not touch it: it
refreshed the surface bits and set the topology flag, and the flag's handler only rebuilds links when
a full rebuild is already due or nodes are queued for their first link, which a refresh sets neither
of. Contact area is the product of both ends' mount fractions, so every link touching a refreshed
block went on carrying a conductance derived from geometry the block no longer had, for the rest of
the session. Meanwhile the *expensive* half of the flag — a full room flood fill — was charged on
every call.

Both halves are now proportional to what changed. `ThermalSolver.RefreshBlockLinks` drops the node's
links through the same intrusive chains removal walks and requeues it for the incremental link build
a placed block takes, which costs the node's degree. The remap is asked for only when the block's
*structural* sealing bits actually moved, or when a door's live bits moved and the mapper has no
portal for it. The dirty flag is split accordingly — `MarkTopologyDirty` for anything that can move a
wall, `MarkLayoutDirty` for anything that cannot. `RefreshingABlockCostsItsOwnDegreeRatherThanTheGrid`
holds the repair to one node's links.

### The step budget has to count everything a step does

`MaxElementVisitsPerStep`, formerly `MaxLinkVisitsPerStep`, bounded a step by substeps times links,
while the environment pass is per node per substep — radiation, convection, solar with six face
weights each — which the budget could not see. A grid with few links per node therefore got a more
generous allowance than one with many, for the same real cost.

The weight was measured rather than guessed: a node is worth **2.8 links at four thousand nodes, 3.3
at a hundred thousand and 7.5 at a quarter of a million**, and an exposed face between a tenth and a
half of a link. Per-link cost is flat across a hundredfold size range because links stream; per-node
cost triples because the node state stops fitting in cache. See
[benchmarks.md](benchmarks.md#what-a-substep-costs).

The budget now counts `links + 4 × nodes` and ignores faces, four being the low end of the range over
the sizes where the bound binds at all.

What this retires is the claim that only grids past a hundred thousand blocks reach the default. A
step's cost is size times stiffness, and `TheShippedAllowanceFitsAGridAndAHalvedOneDoesNot` pins
both halves: counting links alone a rig's links buy more substeps than it asks for, so the budget
does nothing at all; counting nodes as well, a substep over a 64,000-block hull costs about 383,000
element visits and the shipped allowance of four million covers it while half of it does not.
**Whether the allowance binds is a question about the step rate and the world rather than about
block count** — a hull asks twice as many substeps of a quarter-second step as of an eighth-second
one, which is why the allowance moved with `Frequency`, and three to four times as many in air as in
vacuum, which is what `C27` found and what doubled it.

> The figures here were 8,904 nodes, 20,779 links and a demand of 23 at the shipped rate. Both
> defaults moved on 2026-08-24 (`C24`) and the census hull with them (`C26`), so the rig that
> demonstrates the point is a 32,000-block hull now rather than an 8,000-block one: the same hull
> demands 6.67 substeps in vacuum where it demanded 23. A world whose config predates the rename takes the new default rather than
importing its old number, which would be a value in the wrong unit; the load path logs when it drops
one.

### A face bolted to something that does not seal is still exposed

Exposure rejected any cell face where two mount surfaces met, regardless of what the neighbour was.
The ordering is what made that wrong: the sealing test runs first, so every joint against a block
that seals was already gone, and the mount test could only ever reach faces bolted to something that
does *not* seal — a grating, a catwalk, a ladder. The room mapper calls the cell beyond one of those
external, because air floods through it, and exposure threw the face away anyway. **A hull panel with
a catwalk bolted flat against it lost 100% of its radiation and solar gain while the mod's own room
map said it was outdoors.**

The mount test is gone. `FaceExposure.Mounted` survives as a *subset* of `Exposed` rather than a
rejection, so the surface dump's `mounted` column answers how much of a ship conducts and radiates
through the same face. `ASealingNeighbourStillBuriesTheFaceWhateverItsMounts` pins the ordering
argument the fix rests on — a solid hull cannot be opened up by this change.

---

## Testing gaps

The suite covers the model thoroughly and the adapter barely. `HostAdapterTests` exercises what can
be reached without a session; the rest of `Game/` is exercised only in the game.

| Gap | Why it is hard | What would close it |
| --- | --- | --- |
| The vent sweep, the terminal readout and the mod API's delegate table | need a live session | the API's *shape* is checkable without one and is worth pinning |
| The wind map and the wind indicator | whether an arrow lands where it should on screen is answerable only by looking | the arithmetic under them is in `WindCompass` and pinned by `WindCompassTests`, including the handedness — the half a drawing cannot argue with |
| The mass sweep's rota | the sweep asks the game for a block's mass, and a harness has no game block to ask | covered today only by arithmetic tests on its slice function |
| The block glow and the cockpit cue | `MyCubeBlock.UpdateEmissiveParts` needs a model with an emissive material and a live render object, and `MyEntity3DSoundEmitter` needs an audio device | both compile against the installed assemblies, which says the members exist and nothing about what they do; the state they decide is `HeatCue`, and that is pinned end to end by `HeatCueScanTests` and `HeatWarningTests`. [backlog](backlog.md) `F15` |

`LoadTests` closes part of the adapter gap for cost rather than for correctness, and asserts work
counters rather than milliseconds so it holds on any machine.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-20 | Trace the block-variant load crash to an earlier failed telemetry unload; document cleanup isolation and restart recovery. |
| 2026-09-20 | Correct the obsolete armour-emissive limitation; record bounding-face glow and shadowless-light limits. |
| 2026-09-04 | **Re-took the memory paragraph**: 723 B/block retained (88.9 MB at 126,731 blocks) against the 1.8 KB and 213 MB it carried — the figure predated most of the passes on [memory.md](memory.md). |
| 2026-09-04 | **Corrected the first-step warm-up in place (`E10`)**: the spike was never mostly first touch — measured, the faults are one to two milliseconds of it — and since the step prologue moved onto the rebuild tick the first step is ~1.5× a steady one, not several times. The sunlit sun-shadow build is the remaining tail. |
| 2026-08-28 | **Corrected the room map's convergence figure, which had been stale for nine days and was quoted here from `D2`** (`E10`, `E5`). It read 7,207 ticks — twenty minutes — on a million blocks; re-measured by `bench scale --max 1000000` it is **3,934 ticks, about eleven minutes**, on 1,000,294 blocks and a 14,278,796-cell box. The 7,207 predated the 2026-08-26 word skip and the 2026-08-27 span flood, both of which `D2`'s own body already recorded — the headline outlived the paragraph that superseded it. **And the figure is structural**: convergence is the box over a 4,096-cell tick budget, so no work on milliseconds a cell can move it, which `RoomMapConvergenceIsTheBoxDividedByItsBudget` now pins. |
| 2026-08-28 | **The input sweep covers twenty of twenty-one inputs; it covered eighteen of twenty-one.** Position needed a scenario that changes altitude before it could be measured at all, and on the new `descent` it is **0.8 K at worst and 0.00 K standing** — covered, and it does not matter. Weather needed nothing but asking, and it is **168.8 K at worst and 87.1 K standing**, the largest single-input divergence the sweep can express and a bias rather than a perturbation. Both were inert on the first attempt because `EnvironmentSample` is a **struct** and the helpers were mutating copies, which is why the sweep now marks a case its scenario cannot express rather than printing the zero that looked identical. |
| 2026-08-28 | Priced the inline-radiator limit against `C40` instead of leaving it as plumbing waiting to be done. Running coolant through a radiator is internal transport, and the ceiling on every internal path is **9.13 % of the peak** — the hull already carries 1,457 W/K against the loop's 40. Stated as a bound rather than a measurement, because nobody has run this one. |
| 2026-08-25 | **The block-population limit above is a limit and no longer a leak.** It says heat leaves with a block that leaves; it did not say that breaking a coolant ring destroyed two thirds of the *surviving* pipes' heat as well ([backlog.md](backlog.md) `A12`), which is not a population change at all — the same blocks were still there. Fixed in [thermal-model.md](thermal-model.md#coolant-loops): a pipe now holds the parcel it absorbed, capacity and all. `HeatLaunderingTests` measures what a broken ring costs now — one parcel out of `N`, nothing for a split — instead of pinning the old fraction. |
| 2026-08-25 | Wrote down the limit that had never been written down anywhere ([backlog.md](backlog.md) `F26`): heat leaves the world with a block that leaves it and arrives at ambient with one that is built. Energy conservation is an invariant about a step and says nothing across a change in the population. Pinned by a test, so it is not rediscovered as a bug. |
| 2026-08-25 | Refreshed the per-face shadow figures, which had been quoted from a page rather than from the lab and had gone stale when `C24` moved the clock: 0.0018 K a metre against a cadence of about half a kelvin, where this page said 0.0045 against 1.47. The table they come from is now pinned to `OcclusionLadderTests`. |
| 2026-08-24 | **Bounded the two coupled paths, which closes `A10`.** The pairwise overshoot clamp is the whole bound a block needs and half the bound a lumped mass needs: a parcel carries a link to every pipe on it and a room's air one to every surface bounding it, and the node on the other end of a sink face is pulled on by the fluid and by everything it is bolted to. Each bound held and the node went past both. The per-node relaxation the conduction pass already used now applies to the coupled passes too, which makes every substep a convex combination of the temperatures around a node. Measured where the plumbing sets the demand: **1.3e25 K before, 1,799 K after** at 9.7× over-subscribed, and a thin room refused one substep of thirty went from 3,839 K of spread to inside the 300 K it started at. Inert while the demand is granted — the block ladder is unchanged to three decimals — and `bench ceiling --fixture rings|pressurised` is what draws the other two ladders. |
| 2026-08-24 | **The coolant path has no overshoot clamp** ([backlog.md](backlog.md) `A10`), so a refused substep demand approximates on a block and diverges on a loop — and on a hull carrying nothing stiffer the loop is what sets the demand, nine substeps where the same nine blocks unplumbed ask for one. Orderly to 4.5× over-subscribed, 1.7e11 K at 9×, and nothing shipped reaches it. Found by attempting `C24`, whose clock change put a test fixture's deliberately-refused ring past the cliff. |
| 2026-08-24 | **The clock is in the sweep, and the mechanism the sweep had been naming turned out not to exist** ([backlog.md](backlog.md) `F23`). Simulated time is counted in simulation ticks, so a machine below 1.0 sim speed has a thermal clock that runs slow: a 10 % error settles 18.69 K out under a moving load and 0.00 K under a steady one, because two hulls heading to the same equilibrium at different speeds agree once they arrive. `hitching` is the same deficit in lumps — at an equal deficit it peaks 206.0 K against a slope's 39.7 K and settles within a fifth of it — and the backlog drop written beside it for months was `SimulationScheduler.StepsDue`, a second step-credit accumulator no shipped path called. Removed, with the step-rate tests moved onto `ThermalSimulation.Update`. Also added `missing source`, the one input that comes from outside this mod: 0.73 K standing for a registration worth a tenth of the sun. |
| 2026-08-24 | **Topology is in the sweep, and it is the half that changes which numbers exist** ([backlog.md](backlog.md) `F22`). `build order` gives the client the same blocks in a different arrival order and reads 1.2e-4 K, which is float summation order rather than physics — and the same packet applied by index rather than by position leaves 492.0 K, which is what the codec's eight-byte key is buying, measured rather than asserted. `blocks missing` takes a tenth of the hull away: the loudest row in the sweep at 1,151.7 K, most of it the *arrival* rather than the absence, decaying to 0.08 K — and its bound is a bias at 380.5 K in vacuum and 740.7 K flying, the worst input measured anywhere here. The correction narrows what a partial hull misreads and cannot touch the 223 blocks that are absent. The comparison itself moved to block position from node index, which is a no-op on two hulls built alike and the only comparison that means anything on two that are not. |
| 2026-08-24 | **The room map and its air are in the sweep, and they are the two ends of its own axis** ([backlog.md](backlog.md) `F21`). Pressure is *binary*: the link conductance carries no pressure term, so a fifth of the air missing is 0.90 K and all of it is 110.3 K, and the last one per cent is worth more than the first ninety-nine. An unconverged room map is the loudest input measured here — 474.7 K in vacuum, 912.7 K in air, because an empty map makes the whole interior sky and the hull believes in 26.8 % more skin — and it settles at 0.01 K, so the loudest is also the one the correction has least reason to chase. The bound, a pass that never lands, is a bias at 290.95 K and out-settles the wrong switch. **Two rig changes came with it and every figure above was re-measured**: the sweep hull's four compartments now hold air, which is worth about a seventh off every standing error, and the suite's own rig moved from 400 blocks to 600 because the census hull grows its first sealed room between the two and both room knobs would otherwise have judged nothing (`E8`). |
| 2026-08-24 | **Block state is in the sweep, and a wrong switch is the worst input in it outright** ([backlog.md](backlog.md) `F20`). A tenth of the producers on the wrong side of their own switch settles a client 281.0 K out against `wrong settings` at 142.8 K, because a block that is off is wrong by *all* of its heat rather than by a share of it — and at equal missing wattage the concentrated error is 61.0 K against 13.5 K spread. Mass is the opposite kind of input: it is capacity rather than watts, so it stands under a moving load and decays under a steady one, 38.45 K against 0.3 K. Integrity turned out to reach the model through mass and nothing else, so the row's three inputs are two knobs. |
| 2026-08-23 | **Grid velocity is in the sweep** ([backlog.md](backlog.md) `F19`), which is the consequential third of that row. A 20 % speed error settles a flying client 14.0 K out — a bias, and it lands on friction rather than on convection, because one goes as the cube of airspeed and the other saturates. Position and weather are still unmodelled and the row says so. |
| 2026-08-23 | **Solar occlusion is in the sweep** ([backlog.md](backlog.md) `F18`), which completes the environment half of the input surface. It is the one binary input and behaves like one: intermittent disagreement peaks 17.3 K and settles at 2.8 K — a perturbation, where thrust is a bias — and a client permanently on the wrong side settles at 10.8 K, which is the solar term itself and the bound. |
| 2026-08-23 | **Thrust is in the degraded-input sweep, and it is the worst input there is** ([backlog.md](backlog.md) `F17`). At the same 5 % error on a flying hull it settles a client 17.8 K out against block power's 1.8 K, and at 20 % its peak and standing error are the same 71.1 K — a bias that never decays, on the one input the engine predicts rather than replicates. The sweep needed a scenario that flies: a hull at rest makes the knob measure nothing. |
| 2026-08-23 | Priced the per-grid planet shadow limit rather than leaving it argued: resolving the planet per face is worth about 0.0045 K a metre of hull on the worst-placed block, which is 0.73 K on a 150 m ship against a 1.47 K cadence floor it does not touch. The limit stays, with a figure. |
| 2026-08-23 | **Built `B4`'s transport**, which was the half of that row no lab could reach. `ThermalGridSync` is the session it happens in; `HotTailMessage`, `HotTailSchedule` and `HotTailState` are the wire, the timing and the bookkeeping, all game-free and covered by `HotTailSyncTests`. The protocol is the one the measurement chose and nothing more: the whole hull once when a client asks, then the band every five seconds, no budget, empty bands unsent, on a secure channel of its own. Two settings ship with it, `EnableTemperatureSync` and `TemperatureSyncInterval`. What no test reaches is registration, addressing and the send, so `/thermal sync` prints counters on both sides instead. |
| 2026-08-23 | **Measured the fix for `B4` rather than only the defect, and both halves changed what the row said.** The near-critical tail alone does not close the gap on the census hull — 560 s of misreading to 145 s, and the residual is the un-replicated hull rather than the update rate, because a corrected block conducts to stale neighbours. Stating the whole hull **once at the join** and then tracking the band takes it to **0 s at every interval down to sixty seconds**, for one 94 KB packet and 513 B/s after it. And `-- inputs` found that the convergence argument this row rested on covers one of eight causes: a stale join settles at 0.25 K, while a wrong input — power 2 s late, a dropped backlog, settings that never arrived — settles at 44, 58 and 116 K and stays there. A bias does not decay, and the correction narrows one without removing it. |
| 2026-08-23 | Built the guard the entry above asked for ([backlog.md](backlog.md) `F16`), so this is now a limit with a check under it rather than a warning to remember. |
| 2026-08-23 | Recorded that the mod project's build is not the game's check, after `Units.Watts` took an `IFormatProvider` and the mod failed to compile in a session while building clean here. The whitelist was read out of `SpaceEngineers.Game.MySpaceGameDefaultIlChecker` rather than guessed at: `System` is not an allowed namespace, only a named list of its types, and `IFormatProvider` is not on it. Opened `F16` for the check that would have caught it. |
| 2026-08-23 | The glow is back to incandescence, which leaves the limit above where it was: a model with no emissive material still cannot show it. |
| 2026-08-23 | The glow is now a block's distance from its own rating rather than an absolute temperature, which does not change the limit above: a model with no emissive material still cannot show it. |
| 2026-08-23 | Recorded two limits that came with natural feedback ([backlog.md](backlog.md) `B25`, `F15`): a block with no emissive material in its model cannot glow, which is most structural blocks, and the two engine calls the feature makes have never run in a session — they compile, which says the members exist and nothing more. |
| 2026-08-22 | Withdrew the burning-ship divergence. It was never a divergence: run ten times longer the rig is flat to the last digit from 600 s to 6,000 s, its energy balances to a part in ten thousand, and two integrators refused wildly different substep counts land one kelvin apart. The 11,279 K is a converged conduction-limited interior temperature — the hottest block has no exposed face and pushes 2.22 MW out through 1,317 W/K of conduction — and the peak among blocks that can radiate is 2,822 K. The defect it left behind is on [realism.md](realism.md): a divergence column that was a threshold on a temperature, which cannot tell a converged extreme from a diverged one. |
| 2026-08-22 | Measured the client divergence that had been recorded as cosmetic, with `-- drift`. It converges on its own — the model is dissipative — but it is on the wrong side of a block's critical temperature for two and a half to five minutes, against a whole damage event that is a median 8.9 s long, and the error is not a uniform offset so a per-grid correction would not reach it ([backlog.md](backlog.md) `B4`). Corrected the count of replicated settings, which said 44 of 49 against 77 of 85. |
| 2026-08-22 | Said what the destruction limit does and does not reach in the new `seconds_to_first_loss` column: the first loss is exact, and there is deliberately no count of losses after it. |
| 2026-08-22 | Reopened the per-grid shadow limit as designed work. It was recorded as a simplification taken on purpose, which `D6` is satisfied by, but the cost argument behind it treated three occluders as one: the planet's test is analytic and costs no ray, so the per-block objection was never true of the one occluder a player notices. Now [backlog](backlog.md) `A9`. |
| 2026-08-22 | Filed the burning-ship divergence as an open defect. It had been carried on [realism.md](realism.md) as a starved-integrator finding; re-measuring it showed 0% starved, so the explanation is withdrawn and the defect stands with its cause unknown. |
| 2026-08-22 | Repointed the step-budget paragraph at the renamed test and at the shipped rate, which moved from eight steps a second to four when the settings profiles were removed. |
| 2026-08-25 | **The failure-pattern section is evidence and now says so.** Its heading claimed *each of these is stated as the rule it produced*, which is `R13` inverted — a rule is stated once, in [rules.md](rules.md), and argued on the page that holds the evidence. Two entries had drifted into restating a rule's own sentence beside it. The four that produced a rule name it (`D2` twice, `D3`, `C9`); the rest are engine or model behaviour that cost a defect once and is worth not paying for twice. |
| 2026-08-24 | **Added the element-visit allowance to the deliberate limits, which is where the mod's largest shipped approximation should have been all along** (`D6`). It was missing because *the budget costs no accuracy* is true — a bounded step is shortened rather than coarsened — and reads as *nothing is lost*. What is lost is time, and `C27` priced it: 0.00 K on a parked ship, 1.19 K standing at a 5 % deficit and 36.98 K at 60 % under a moving load, against 0.028 K for the substep ceiling this world accepts and 0.607 K for the per-block cap it refuses. A limit nobody wrote down is the defect this section exists to prevent, and this one had been described three times elsewhere as a defect history and never once as a limit. |
| 2026-08-22 | Moved the thermal view out of the deliberate limits. It was filed there on the grounds that mods get no shader — which is a statement about difficulty, not a simplification taken on purpose, and a limit is only a limit when it is chosen (`D6`). It is an open problem, and the intent to have one is stated in [document-of-intent.md](document-of-intent.md#thermal-vision--wanted-method-unknown). Recorded the absence of any non-instrument feedback beside it. |
| 2026-08-22 | Absorbed `bugs-and-performance.md`, the record of the first extraction pass. Every one of its thirty-two findings is resolved in the current code — including the eleven whose headings carried no *fixed* marker, each re-verified against the source during this pass — so the page survives as the dated entries below and the patterns above rather than as a defect list. Restructured around the shape of each failure rather than its subsystem; promoted the deliberate limits to the top; moved the corpus balance findings to [balance.md](balance.md), which is where the dataset they come from is described. Removed two limits that the per-room gas-system read had already retired ("a room with no air vent holds no air" and "pressurisation is only known through air vents") and corrected a third: block `Conductivity` is real W/(m·K), and it is the *coolant loop's* that is still a 0…1 quality. Merged the two sections both titled "Fixed, worth remembering". |
| 2026-08-21 | Recorded the buffer-growth NaN, the unguarded shape caches on the block-placement path, and the substep mass floor computing from its own previous answer. Recorded the censoring limit that makes every peak above critical a statement about the harness. |
| 2026-08-20 | Recorded that every reactor in the game made no heat, and that all eighteen of the mod's own `Cubes.xml` entries were filed under a TypeId that does not exist. Recorded the step budget counting links but not nodes, and the exposure test that buried a face bolted to an open lattice. |
| 2026-08-19 | Recorded the three room-air defects — links never built through the game's sweep, a vent reporting to one room of two, and a vent speaking only for the room it stands in — and the reporting fault that showed convection before the atmosphere blend. |
| 2026-08-18 | Recorded the ambient scale compounding against the lag, and the lag with no history test. |
| 2026-08-12 | Opened the register against the pre-rewrite implementation: 32 findings across model physics, code and performance, from extracting the simulation into `tests/` and putting it under test. The model defects — conduction ignoring thermal mass, conduction not conserving energy, friction computed and discarded, damage scaling with the update rate, specific heat 250× below physical — are what made a rewrite worth doing rather than a patch. |
