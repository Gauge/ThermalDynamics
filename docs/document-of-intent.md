# Document of intent

What this mod is for, what it is trying to be, and the standing goals every decision is measured
against. Each statement here was already made somewhere — in the README's design goals, in a
criterion written down before a corpus run, in a block's own page, in a code comment, or in a
decision recorded and never revisited — and that was the problem: an intent stated once in the
document its author happened to be writing is an intent the next reader finds by accident.

**This page states intent; it does not argue it.** Where a goal is argued at length, this page names
it and points there — the same relationship [rules.md](rules.md) has with the pages that argue its
rules. Where two statements of intent conflict, this page says which the code currently follows.
Where there is no intent at all, this page says that too, because an undeclared intent is decided by
whoever touches the file next.

**Four kinds of thing are on this page, and they are kept apart on purpose.** Most of it is
**intent** — what the mod is for and what it is trying to be. [One
section](#where-the-goals-and-the-code-disagree) is **conflicts**: places where two statements this
repository makes disagree, with a column saying which the implementation currently follows, which is
evidence about intent rather than a decision taken on the developer's behalf. [One
is](#where-there-is-no-intent-at-all) **voids**: subjects the mod already ships or already refuses
with no statement anywhere about whether that is right. And [the
last](#open-questions-with-a-stated-intent) is **open questions**, where the position is stated and
the route is not. A void and an open question look alike from a distance and are not the same work.

> The rules argued here are stated canonically in [rules.md](rules.md): `E3` `E5` `E10` `E11` `D6`
> `D7` `C7` `C9` `C10` `C15` `R14`, and the principles P3, P7, P10, P13 and P14 they follow from.

| Looking for | Go to |
| --- | --- |
| The rules a change is measured against | [rules.md](rules.md) |
| Open work, one line each | [backlog.md](backlog.md) |
| What the simulation does now | [thermal-model.md](thermal-model.md) |
| The evidence behind the balance goals | [balance.md](balance.md), [balance-lab.md](balance-lab.md) |

---

## The purpose

> **The scope widened on 2026-08-25 and this section widened with it.** The mod was written as a
> thermal mod and the three commitments below were written about heat. Drag, lift and a ship's top
> speed are none of those things, and they arrived because the mod already computes what they need
> — so *the surface model is the mod* and heat is one thing done with it. **The name no longer fits
> and is due to change**; what cannot change with it is any identifier a world already holds — the
> block subtypes, the mod-storage GUID, the workshop id — because a world that has them is owed
> them (`P15`, `W2`). Renaming is a display and documentation change only.

**A grid's surfaces meet an environment, and what happens there is a resource a player reasons
about.** Every block has a temperature and an exposure; both are consequential; and a player whose
ship is overheating, or will not accelerate, can find out why and do something about it.

**Heat is one consequence of that model and not the whole of it.** The same per-face walk that
decides what a block radiates, absorbs and convects also decides what the air pushes against — one
exposure map, several readers. That is why aerodynamic force belongs here rather than in a mod of
its own, and it is the only reason: *we already have the data* is an engineering argument, and this
is the design one.

> **A stronger claim was made here for a few hours on 2026-08-25 and withdrawn**: that every
> consequence could be switched off entirely, heat included, and cost nothing when off. It was
> retracted once its implication was read. Heat being removable would force the surface model out
> of the thermal solver into something both it and the force model consume — and **the shared walk
> is the whole reason aerodynamic force belongs in this mod at all**. Unpicking it to make one half
> optional would have cost the argument that put them together. Heat stays load-bearing; the
> per-mechanism switches are what a world tunes with.

**Consequential includes the player.** Heat that only ever damages blocks stops at the airlock, and
a burning compartment that a person can stand in is the mod saying the temperature does not really
matter. The suit is what closes that — a machine that holds its occupant and can be beaten, rather
than a threshold — and it is described in [configuration.md](configuration.md#the-suit).

Three commitments follow, and they are the test for whether a feature belongs. **They were written
about heat and generalised on 2026-08-25 by changing one noun**, which is the evidence that the
widening is honest rather than convenient — a scope that needed the tests loosened would be a scope
that did not belong:

1. **It has to be legible.** A player must be able to ask "is this ship able to cool itself at all",
   or "why will this ship not go faster", and get an answer. That question is why the grid heat
   balance exists — `made` against `vented`, in watts, on the cockpit panel and through the API —
   and it is why a centre of lift is drawn beside a centre of mass rather than on its own.
2. **It has to be caused.** The outcome must follow from what a builder chose, not from how big the
   ship is. That is `G4` below, and it is the criterion the corpus most clearly passes.
3. **It has to be answerable.** There must be a lever — a radiator, a loop, a heat pump, a different
   place to put the reactor, a different shape — that visibly changes the outcome. That is `G3`,
   and the lever that works is plumbing rather than bolting.

**It is a framework as well as a mod.** Everything the simulation knows is readable and everything
it does is drivable from another mod, through a delegate table passed by mod message. The intent is
that a second mod can make heat mean something without forking this one — see
[api.md](api.md).

---

## What the README is for

**The [README](../README.md) is the mod's front page for players, not the repository's front page
for developers.** It is written to be copied whole into the Steam workshop description or handed
over as a guide, and to be read end to end by someone who has never opened a source file — so it
explains what the mod does, what it adds, and how to configure it, in that order and in plain
language.

It carries one technical section on purpose: **instructions for modders who want to build on this
framework.** That is the API and what a second mod can do with it, written for someone who has
decided to write code against this one. Both audiences are served by the same page because both
arrive at the same place — a workshop item — and neither should have to be told to go and read a
repository.

**What it is not.** It does not explain how the mod works inside. No solver, no substeps, no
architecture, no measurements, and no defence of a design decision. Everything of that kind lives on
the pages this one sits among, reached through the README's
[documentation index](../README.md#documentation) rather than summarised in the README itself: a
paragraph about the integrator is a paragraph a workshop reader skips and a developer would rather
read in full elsewhere.

**It therefore does not behave the way a repository README does.** The conventions in
[development.md](development.md#documentation-conventions) hold — it states its scope, describes the
present, and keeps a change log — but the audience test overrides the developer one wherever they
disagree: a section earns its place by being something a player or a modder needs, not by being what
a repository is conventionally expected to publish.

---

## What a code comment is for

**A comment names a definition; it does not teach a topic.** Its job is to tell a reader what a
thing is for in as little space as possible, and nothing beyond that: the explanation of *why* the
thing works the way it does belongs on the documentation page that covers it, where it can be
argued at length and found by someone who is not already looking at that line.

So a comment is worth writing in two places — on a definition, and over a chunk of code that is
genuinely complex or abstract. Code that is neither is read faster without one.

This is `R14`, and the check on it catches the failure the length limit prevents: a comment that
argues a topic is maintained against a subject it does not sit beside, and one whose subject is
deleted stays behind and describes whatever is below it.

Three tests, in the developer's words:

* **It must stand on its own.** A comment that expects the reader to hold context living in another
  file is a bad comment. Either it says enough to be understood where it sits, or it should be a
  pointer to the page that does.
* **Two lines is the limit** — *for a comment inside a body.* A running comment longer than that is
  a documentation entry that ended up in the wrong file, and it sits beside code that moves, which
  is what makes it rot.
* **It describes, it does not lecture.** Detail is what the documentation files are for; this page
  and the ones it sits among exist so that no comment has to carry an argument.

**The limit had no scope on it until 2026-08-25, and measuring the tree is what put one there.**
Stated flatly it is a rule two thirds of the repository breaks. Counted: **2,347 running `//`
comments, 73 % of them one or two lines and 72 over five** — the limit describes those accurately
and they are the ones that rot. **4,838 `///` summaries over 24,380 lines, 62 % of them longer than
two lines** — and those are not the same object. A summary sits on a *name*, which does not move
under it; `R10` already makes a test class's summary the canonical statement of what that class is
for, with nowhere else to put it; and the summaries that run longest are carrying an experiment's
controls, where a reader who does not have them cannot trust the code. So the test on a summary is
not its length. It is the third bullet: **does it state what this thing is, or argue what a page
argues?** Where it would argue, it names the page — which is `R14`, and is the half a length limit
was standing in for.

---

## How a player perceives heat

Legibility is the first of the three commitments above, and it has three layers. **The first two are
built; the third is not.**

### Clear indicators, and the player figures it out

**Decided 2026-08-25, and it is the strongest statement on this page about what the readouts are
for.** The mod's job is to show clearly what is happening. It is *not* to diagnose, to advise, or to
name the fix. A player who sees a block glowing and hears a cockpit warning has enough to know their
grid is overheating; working out why, and what to change, is the part they came for. **That is the
Engineering in Space Engineers**, and a mod that hands over the answer has taken the game away.

Two things follow that are otherwise easy to get wrong.

**The mod has to announce that it is there.** A player who adds it to a healthy world should be able
to tell, without waiting for something to go wrong. Today they cannot: the glow starts a hundred
kelvin below a block's rating, the cue fires as a block approaches its own, the terminal panel needs
a terminal opened, the extinguisher's readout needs the tool in hand, and the one always-available
figure — ambient temperature — lives on a performance panel behind a chat command that ships off. So
the mod is invisible until something is already wrong, which is the opposite of the intent. **Basic
environmental information should be on screen as a matter of course** ([backlog.md](backlog.md)
`B41`).

**Built 2026-08-28, and the half that can be judged outside a session is judged.**
`ShowEnvironmentReadout` ships on: one line, bottom centre, carrying the air temperature around the
player's ship and one word for the ship against its own rating. What it *says* is a decision, so it
lives in `Core` where a test can reach it — `hot` begins exactly where the glow begins, so the
sentence and the block a player is looking at cannot disagree, and `warm` is half the climb from the
air rather than half the rating, because a hull sitting in a furnace is not warming up. What it
*looks like* is the half only a session can judge, and that is still open.

**And "clear" is a higher bar than "present".** An indicator a player has to be taught is a
diagnosis in disguise. The test is whether somebody who has never read this repository can look at
the screen and tell that their ship is getting hotter — not whether the number is correct.

### Instruments — built

The terminal panel per block, the cockpit summary, the crosshair readout, the x-ray block overlay
and the room view. These answer a question a player already knows to ask.

**The intent for all of them is that they belong to the game's HUD rather than sit on top of it.**
The readouts are drawn through Rich HUD so they can adopt the game's own placement, scaling and
styling, and the goal is that a player reads a temperature the same way they read a power figure —
without a mod-shaped panel announcing itself. Anything that looks bolted on has failed this even if
every number in it is right.

### Natural feedback — built

**A player should learn their ship is overheating without looking at an instrument.** This is the
layer that makes heat a felt resource rather than a readout. It is two channels.

**The glow is the last hundred kelvin before a block fails.** Nothing until a block is within 100 K
of its own critical temperature, then a straight ramp to full at that temperature and above. The
window is narrow on purpose: a glow means *this block is about to go*, not *this block is warm*, so
it stays dark through every temperature a ship runs at in ordinary play and comes on only at the
end. Because the band is a fixed hundred kelvin rather than a share of the rating, the same distance
from failure looks the same on a decorative block and on a large thruster — and it is a longer
warning on a block that heats slowly than on one that heats fast, which is the right way round.

**The colour is physics and stays absolute**: the Planckian locus, computed from Planck's law
against the CIE 1931 observer. A block glowing at 500 K is deep red and one at 2,000 K is orange,
whatever either is rated for. So brightness says how close to failing and colour says how hot it
actually got, and a decorative block dying dull red beside a thruster dying orange reads correctly.

> **The brightness is deliberately not incandescence, and the measurement is why.** A real solid
> emits no visible light below the Draper point at 798 K, and the shipped definitions put **26 % of
> block types below that** — keyed to the physics, a quarter of the game would fail with no visual
> warning at all, and a decorative block rated 583 K would go from dark to destroyed with nothing in
> between. [Game mod first](#the-governing-prior-game-mod-first) settles it: take the physical form
> where the difference cannot be perceived, and here the difference is the whole point of the
> channel. The colour keeps the physics, because nothing is lost by being right about it.

**The glow is drawn, because the emissive path could not carry it.** `UpdateEmissiveParts` writes to
a named material in the block's model, and of the 1,992 base cube models the game ships **418 hold
one — of 479 armour models, four**; where the material exists at all it is a status lamp rather than
a skin. The glow is a soft radial additive patch over each exposed face, coloured by the locus
and brightened by the warning ramp, with smooth grazing-angle and distance fades. At most one
nearby dynamic light per grid, capped at 32 lights across the scene, makes the heat fall on its
surroundings. The emissive write remains for models that provide it. The effect is attached to the
object and depth-tested; its bounding-face geometry is an approximation, not a mesh emissive.
The evidence, work limits and outstanding live visual checks are in [thermal-glow.md](thermal-glow.md).

**The cue is the same warning in sound.** A short sound about three seconds before a block crosses
its critical temperature and a distinct one as it crosses, heard only by the player at the controls,
because they are the only one who can act. It reaches what the glow cannot — a block behind another,
a block off screen, a player looking the other way — which is a different set from the one it used
to cover, now that a block with no emissive material glows like any other.

Both channels are properties of the object rather than marks on the screen, which is what makes them
read as physics instead of as UI.

**The three-second lead is a prediction, and a straight line is the wrong one.** A block warming
toward an equilibrium slows down as it approaches, so projecting its current rate forward crosses
thresholds the block never reaches — every hot-running reactor in the game would be warned about,
once, for as long as it was warming up, and a cue that cries wolf is worse than no cue. `HeatWarning`
reads the equilibrium off the rate instead: heat flow out of a block grows with the gap between it
and everything around it, so the rate decays by a fixed factor each interval and two rates one
interval apart give both the time constant and the temperature the block is heading for. **A block
heading somewhere below its rating is not warned about at all**, which is the whole difference. A
block whose rate is *rising* has no equilibrium to read, so the straight line answers there — it
under-estimates a runaway, which warns early, and early is the safe direction to be wrong in.

**It runs when nothing is wrong.** Every other presentation feature is off unless something reads
it. This one has to be watching in order to warn, which is the tension resolved in the
[conflicts table](#where-the-goals-and-the-code-disagree): the reader is the block's own state, and
the cost on a ship where nothing is hot is **one comparison for the whole grid** — the hottest
block, which the observation pass already caches, against the lower of where the coolest block would
start glowing and where it would start being watched. Both come off that block's own rating, so the
floor follows what the ship is made of.

### Who the glow is for — brightness, and colour as a refinement

**The warning is legible without colour, and that is now a property rather than an accident.** The
glow carries two facts in two channels — brightness says how close to failing, colour says how hot —
and the question `B34` asked was who can read both. Measured, almost nobody can read the second one,
including a player with ordinary colour vision.

**The colour channel says nothing about one block over time, and something about one block against
another.** The two had never been told apart, and they answer differently:

* **Within a block, it is invisible.** A block glows across the hundred kelvin below its own rating,
  and the colour at the bottom of that band differs from the colour at the top by less than a
  just-noticeable difference — ΔE76 2.3, on flat patches side by side, which a glowing block on a
  hull is not — for **88 of the 101** types in the installed catalogue. The best any block manages
  is a gyro at **ΔE 4.76**, and the best hundred kelvin anywhere in the locus is **5.16**, from
  about 1,100 K. Twenty-eight are pinned under the Draper point by construction; the finding is that
  the other seventy-three are barely better off.
* **Between blocks, it is plain.** The coolest-rated block in the game at 583 K against the
  hottest at 1,159 K is **ΔE 13.72**, about six just-noticeable differences. *A decorative block
  dying dull red beside a thruster dying orange* is true, and it is a statement about which block
  this is rather than about how it is doing.

`GlowChannelTests` holds both, and the sRGB-to-L\*a\*b\* conversion is checked against known
values first.

**So the position is: brightness carries the fact and colour carries the flavour.** Everything the
glow has to say that a player must act on — *this block is close to failing* — is a monotone ramp
that reaches full at the rating and says the same thing in any colour. What colour adds is that two
blocks failing side by side do not look identical, which is verisimilitude and not information. Red
against orange is the distinction a common form of colour blindness does not make; it is exactly the
distinction this channel carries, and it is exactly the one that carries nothing actionable.

**The sound cue is a redundant channel and is now meant to be one.** It was added to reach a block
behind another, a block off screen, and a player looking the other way; that it also covers a player
who reads no colour was an accident, and it is a commitment from here.

**What this forbids.** No fact a player must act on may be carried by hue alone. If the colour channel is ever made to
say something brightness does not — a different failure mode, a different mechanism — it needs a
redundant non-colour encoding shipped with it, and the measurement above is the reason: a channel
worth two just-noticeable differences at its very best cannot be the only carrier of anything.

### Thermal vision — wanted, method unknown

**A thermal camera view is an aspiration, not a rejected idea.** An earlier heat overlay was built
and removed, and the limit recorded from it — *mods get no shader, no post-process and no frame
buffer* — is true and is the obstacle rather than the answer. The x-ray block overlay keeps the part
of that work worth keeping, since a debug view *wants* to see through a hull, but it is a debug view
and not the thing.

**The route is genuinely unknown and is expected to be indirect.** Candidates worth investigating,
none of them straightforward: whether any shader or material parameter is reachable from a mod at
all; billboards or transparent materials, which is how the extinguisher overlay already draws;
particle effects as a rendering surface; and per-block emissive, which is the one path the engine
definitely exposes and which overlaps with the glow described above.

This is the one place where the [governing prior](#the-governing-prior-game-mod-first) does not
settle the question. The prior says take the cheap form where the difference cannot be perceived —
here the difference is *entirely* perception, so the cheap form is not obviously the right one.

### Acting on heat by hand — damage mitigation, and priced

**A player can act on heat, and the tool for it is the extinguisher.** It fires expendable
ammunition that cools a block rapidly, at a cost in that ammunition. It is **damage mitigation, not
cooling**: the thing you reach for when a block is about to go, not a way to run a ship hot.

> **This section said the opposite for a few hours on 2026-08-25 and the measurement is what
> changed.** It concluded that hand-scale action was out of scope because there is no hand-sized way
> to cool a ship — which is true, and is the wrong question. The tool is not asked to undo a
> crossing; it is asked to buy time on one block.

**The two questions differ by an order of magnitude, which is what makes the design work.** Against
a five-kilogram CO2 bottle — 3.3 MJ if every gram lands, which is not how an extinguisher works —
over the 72 vanilla block types that reach their own critical temperature, using their physical
heat capacity. These are reference inventory costs, not the effect of injecting 3.3 MJ into the
accelerated solver; the [clock-basis correction](balance.md#what-a-hand-tool-would-have-to-be-worth)
explains the distinction:

| | median block | ninetieth percentile | worst |
| --- | ---: | ---: | ---: |
| return it from its rating to ambient | 26 bottles | 461 | 7,818 |
| pull it 10 K back from its rating | **0.5 bottles** | 15.9 | 90 |
| pull it 50 K back | **2.4 bottles** | 79.4 | 448 |

The solver damages an overheating block at `(T − critical) × OverheatDamagePerKelvin` a second, so
what stops the damage is removing the *overshoot* rather than the heat — and an overshoot is tens of
kelvin where the rise from ambient is hundreds.

**The intended mechanic is to save ordinary blocks while preserving the need to build for heat.**
The physical reference supports that distinction, but a working tool must still demonstrate it with
its chosen ammunition energy, transfer efficiency and clock conversion. The scanner-only tool has
not yet demonstrated either outcome.

**What it must not become is a cooling system.** The earlier argument that twenty simultaneous
bottles proves this used physical return energy divided by a game-time damage window; it does not
establish the delivered power required by a future implementation. `HandCoolingTests` preserves
the reference calculations and distinguishes their clock bases. Ammunition balance remains open.

### Thermal vision — wanted, method unknown

**A thermal camera view is an aspiration, not a rejected idea.** An earlier heat overlay was built
and removed, and the limit recorded from it — *mods get no shader, no post-process and no frame
buffer* — is true and is the obstacle rather than the answer. The x-ray block overlay keeps the part
of that work worth keeping, since a debug view *wants* to see through a hull, but it is a debug view
and not the thing.

**The route is genuinely unknown and is expected to be indirect.** Candidates worth investigating,
none of them straightforward: whether any shader or material parameter is reachable from a mod at
all; billboards or transparent materials, which is how the extinguisher overlay already draws;
particle effects as a rendering surface; and per-block emissive, which is the one path the engine
definitely exposes and which overlaps with the glow described above.

This is the one place where the [governing prior](#the-governing-prior-game-mod-first) does not
settle the question. The prior says take the cheap form where the difference cannot be perceived —
here the difference is *entirely* perception, so the cheap form is not obviously the right one.


---

## The governing prior: game mod first

> *Where a simplification costs nothing a player can perceive and avoids real expense, take the
> shortcut, and say plainly when the shortcut would be visible.*

This is the prior that decides what gets built. The mod exists to make heat a real resource, not to
be a finite element solver: **fidelity no player can observe is cost** — in frame time, in code
paths, and in the number of things that can go wrong — bought with nothing.

It is deliberately not a rule, because it is a disposition rather than something a diff can violate.
Its operative half *is* a rule: `D6` — take the shortcut, and record it as a deliberate limit in
[known-issues.md](known-issues.md#deliberate-limits), with the price written down, so nobody
rediscovers it as a bug.

**Realism wins where the difference is visible.** The reactor waste fraction is the worked example
and is worth reading as the pattern: a real plant's efficiency applied to a 300 MW 3×3×3 block
destroys every large reactor in every world, so the fraction is chosen so the *consequences* land
where they should. Applying a real number to a fictional rating compounds the fiction rather than
correcting it. See [balance.md](balance.md#reactor-waste-heat).

**The oxygen generator is the same test taken the other way, and it is why the prior is about
consequences rather than about invention** (2026-08-25). Its 0.6 was an admitted opinion and water
electrolysis sources 0.20–0.40, which is the largest gap in the file — an argument for the real
number on provenance alone. What decided it was neither: at 0.6 **two of the game's six oxygen
generators sit past their own critical temperature alone in open space**, at the draw their own
definition rates them at, which is a block that cannot be built rather than a fraction that is
merely unsourced. It is 0.40 now, the top of the sourced band. So the reactor kept an invention and
the generator lost one, on the same test applied in both directions: **the question is never whether
a figure is real, it is where its consequences land.** See
[balance.md](balance.md#what-it-did-the-invention-was-the-value-that-could-not-be-built-and-two-of-four-predictions-fail).

### Where something is modelled, it is modelled as a mechanism rather than as a threshold

The prior above says when to take a shortcut. This says what the thing that is *not* shortened looks
like:

> *Prefer a machine with inputs and a limit over a number with a comparison. A threshold is what is
> left when nobody could find the mechanism.*

It is a disposition rather than a rule, and it is stated because it is the single most consistent
habit in the code and was written down nowhere:

* **The suit is a cooler, not a survivable temperature.** The occupant is held at body temperature
  while a 500 W rating keeps up with what leaks in through 2.5 W/K, so the survivable room is
  **derived** — `310 + 500/2.5` = 510 K — and moving either input moves it. An open helmet is a
  tenfold conductance rather than a second rule, and the second threshold falls out at 330 K on its
  own.
* **A coolant pump is a motor.** It draws power and makes waste heat like any other motor, and its
  50 kW rating is `ṁ ΔP / (ρ η)` rather than a chosen figure. Its waste fraction is 1 because a
  circulator does no work that leaves the system.
* **A heat pump is priced by Carnot** — cheap across a small difference and ruinous across a large
  one — which is what makes it a different block from a radiator rather than a stronger one.
* **The glow's colour is the Planckian locus**, computed from Planck's law against the CIE 1931
  observer, so 500 K is deep red and 2,000 K is orange whatever the block is rated for.
* **The warning cue reads an equilibrium off a rate** rather than extrapolating a straight line, so
  a block heading somewhere below its rating is not warned about at all.

**Where the mechanism is the wrong answer, it says so and why.** The glow's *brightness* is
deliberately not incandescence, because keying it to the physics would leave a quarter of the game's
block types dying with no visual warning at all —
[the measurement is under Natural feedback](#natural-feedback--built). That is the prior above
overriding this one, and the two are in the order they are written in: perceptibility first,
mechanism second.

### Fidelity is the default; a saving is a switch

The prior above decides what gets *built*. This decides what gets **shipped on**:

> *The simulation mirrors real behaviour as closely as it can. Where a cost-saving approximation is
> worth having, it is a switchable feature rather than the default.*

The two are consistent because P14 is about differences a player **cannot perceive** — where the
difference is invisible the shortcut simply is the model, and there is nothing to switch. Where the
difference *is* perceptible, the cheap form does not become the default by being cheap; it becomes
an option, and the player or the server admin chooses it.

This is what decides the unattended-ship question. A per-grid rate tier driven by observation would
take two thirds of a world's thermal budget away, and it changes what a player finds when they
return: a ship left with a reactor overheating comes back destroyed or fine depending on how coarsely
it was stepped. That is perceptible, so **full simulation is the default and the rate tier is a
switch** — see [What an unattended grid gets](#what-an-unattended-grid-gets).

### A switch is a ladder, and its two ends are `off` and `realistic`

The section above says a saving is a switch. This says what shape a switch has:

> *Every feature is configured as a list of options running from `off` to `realistic`. `realistic`
> is the most faithful thing the model can do and is the default; `off` removes the mechanism and
> its cost entirely; everything in between is a cheaper approximation with its price written down.*

**A boolean is the degenerate case of that list, not a different kind of setting.** A feature with
no cheaper middle rung has a two-rung ladder — `off` and `realistic` — and that is a complete
ladder. What the intent rules out is a feature where the only alternative to the faithful model is
having no model, *when a cheaper model is possible and nobody built it*: that leaves a player whose
machine cannot afford the top rung with nothing to choose but to turn the feature off, and a feature
switched off is a feature that is not in the game.

**Three things follow from stating it this way.**

*The ends are named rather than numbered.* `off` and `realistic` mean the same thing on every
feature, so a player who has met one ladder has met all of them, and a server admin can say what a
world runs without a table of which direction each dial goes.

*The middle is where the measurement goes.* A rung earns its place by what it costs and what it
gives up, and both are numbers — so a ladder is the natural home for the price `D6` requires a
deliberate limit to carry. A rung with no measured price is a rung nobody can choose between.

*It is a shape the settings surface does not have yet, and that is the gap rather than the rule.*
Three features are already ladders — the external shadow runs `none` → `basic` → `full`, coolant
transport runs `off` → well-mixed → plug flow, and the wind and climate influences are continuous
dials from 0 to 1 — and the rest of the mechanism switches are bare booleans. Which of those have a
cheaper rung worth building is a per-feature question with a per-feature answer, and
[configuration.md](configuration.md#every-mechanism-and-the-rungs-it-has) holds the inventory.

**This is `C7`'s rule grown a dimension.** *Every mechanism has its own switch* is what makes the
cost of a feature removable; this says the removal should not be the only choice on offer.

---

## The four design goals

Stated on the [README](../README.md) and refined here, each with what currently holds it up.

### Light — a mechanism nobody uses costs nothing

Every readout, diagnostic and overlay is off unless something is reading it. Telemetry defaults to
off and costs a handful of static bool reads when it is. Nothing allocates on the stepping path.

**Checked by** `C4` (`bench report`), and by the feature switches under `C7`.

> **The README's formulation is out of date and the intent behind it is not.** It reads *"a grid's
> cost is one pass over its links per substep, and nothing else"*. A substep also runs the
> environment pass, which is per **node** — measured at 3 to 8 links' worth of work at the sizes
> where it matters, which is why the step budget counts `links + 4 × nodes`. The goal is that a
> substep is one pass over the grid and no more; the link-only wording predates the measurement. See
> [benchmarks.md](benchmarks.md#what-a-substep-costs).

### Isolated — every mechanism has its own switch

Switching one mechanism off removes exactly its own cost, takes effect on the next step, and needs
no reload. Absent and empty mean the same thing.

**And a switch is a ladder** — see
[A switch is a ladder](#a-switch-is-a-ladder-and-its-two-ends-are-off-and-realistic). Removing a
feature's cost is what this goal is about; what the ladder adds is that removing it should not be
the only way to afford it.

**Checked by** `C7` (`FeatureToggleTests`), `C8`, and `C15`
(`EveryCoreSettingIsReachableFromAWorldsConfiguration`).

### Tested — the simulation is a library the game happens to call

The core speaks no game type, so it builds and runs outside the session in seconds. That is what
makes it testable, profilable, drivable by another mod, and portable to another engine — the four
are one property, not four.

**Checked by** `C5` (`CoreIsolationTests`). The suite is 2,603 tests, 33 deterministic scenarios and
a load benchmark that reaches a million blocks in one grid.

### Open — the API is part of the contract

The delegate table is the mod's interface to every other mod, and it is a dictionary of strings to
delegates, so a caller finds a wrong name out at run time in someone else's session.
[api.md](api.md) is therefore part of the contract rather than a description of it.

**Checked by** `R9` (`EveryModApiEntryIsDocumented`).

---

## What correctness means

**The solver's three invariants are the definition of correctness; everything else is tuning.**

| Invariant | Meaning |
| --- | --- |
| **Order independence** | Iteration order cannot change the result. |
| **Energy conservation** | Every internal exchange is applied equally and oppositely. |
| **Boundedness** | No pairwise exchange can carry a pair past their shared equilibrium. |

This is principle P10, and it is what lets everything else be argued about freely: a change that
holds all three is a tuning question, and a change that breaks one is a defect whatever it buys.
See [thermal-model.md](thermal-model.md#the-three-invariants).

**How claims are made is itself intent.** The evidence discipline — a figure carries its scope, a
blind spot is part of the result, a criterion is fixed before the data, nothing is its own oracle —
is stated in full in [rules.md](rules.md) and is not restated here. Two standing instructions from
the developer sit alongside it:

* **Validate on real grids.** Do not trust in-game numbers as the basis for a claim. A figure a
  running session reported cannot be re-examined — the ships are gone and the world is gone. Build
  the lab and measure the workshop corpus instead, where every input is visible and the run repeats.
  A field dump is one sample; say that it is one.
* **Measure before fixing.** More than one thing on this project has looked guilty from the counts
  alone and been innocent. The room map is the standing example: it was suspected of losing
  compartments, and when the comparison was finally run it was right about twelve of twelve.

---

## Balance goals

Eight criteria, each written down before the data that scores it, so that a run which fails one is a
finding rather than an excuse to move a threshold (`E1`, `E11`). They are argued in
[balance-lab.md](balance-lab.md#0-define-good-balance-before-collecting-anything) and measured in
[balance.md](balance.md). Six are in the table below; `G7` and `G8` came later and are stated under
it.

**Read every percentage below as the population as it was measured, not as the population under the
configuration that ships.** `G1`, `G2`, `G4` and `G5` were scored on the 2026-08-21 corpus at the
pair the mod shipped until 2026-08-24, and re-scored on the forty-hull retest set at the pair that
ships now, where they hold. `G6` is the one whose status has moved twice since, and the row says
where it stands. `G7` and `G8` were written after this table and are stated below it rather than in
it.

**The window has a floor under it, and it is the stronger of the two statements.** The most
significant thermal event lands in two to five minutes — long enough to notice, short enough to
matter — and **nothing may be destroyed in the first minute of a world**. A player who adds this mod
to a world they already have, or starts a new one with it, must not lose a block before they have
understood that heat is now a thing. The compatibility floor measures one half of that already: 705
prefabs, arriving idle, not one crossing critical. What it does not cover is a world somebody has
already built into, and the population's idle result is the nearest evidence — not one of 8,144
published hulls goes critical at idle.

**Six criteria became eight, and both additions came from a gap this table made visible.** `G7` is
the compatibility floor — *a ship the game spawns survives arrival* — and `G8` is the significance
window, which had been the mod's own stated balance target for as long as it had been unscored. That
is the pattern worth keeping: a target that is not a criterion is a target nothing can fail.

| # | Goal | Status against 8,132 corpus ships |
| --- | --- | --- |
| **G1** | **Idle is safe.** A ship at rest in the environment it was built for does not overheat. | **Holds, and it holds in air too.** 0.22 % go critical at idle in vacuum against a ~1 % gate — and **0.02 %** across the 2026-08-24 air walk, which the corpus had never been asked, because air cools. |
| **G2** | **Load bites.** Under sustained full power a meaningful share of uncooled ships reach a warning state. | **Holds.** 65.5% of ships carrying no jump drive reach 400 K under full electrical load, against a ~20% gate. |
| **G3** | **Cooling works.** Fitting radiators or a loop moves the outcome. | **Answered.** Plumbing works — a sink face carries 6,250 W/K against a bolt joint's 167 — and bolting makes more ships worse than better. It fits on 15% of *finished* hulls, which is a fact about retrofits rather than about the mechanic, now that cooling is stated as designed in. |
| **G4** | **Design decides, not size.** Outcome follows what a builder controls. | **Holds, strongly.** Peak correlates +0.89 with worst local W/m² under load against +0.49 with block count. |
| **G5** | **No death spiral.** A ship past critical that throttles to idle returns below critical in bounded time. | **Holds as written.** Its stated reason does not — see below. |
| **G6** | **Affordable across the population**, at p95/p99 rather than at the mean. | **The demand half passes with room; the cost half fails in air, and its figures are withdrawn pending a re-walk.** *Demand*: corpus p99 **34.8 substeps against 64 granted** over the 32,575 runs of the 2026-08-24 air walk, where the panel had read 115 % of the cap before `C24` and 55 % after it. *Cost*: `F11` walked the corpus in air and the criterion failed in three of the four worlds, holding only in vacuum, with **2.73 % of published hulls past the element-visit allowance** in at least one air scenario — the smallest at 28,781 blocks. **Every step-work number this repository has published is withdrawn**, because the unit is `links + 4 × nodes` and the scorer was handed the *joint* count in place of the link count, so it was evaluating the node half alone — 1.51× low on a census hull. That makes the air figure worse rather than better and moves no verdict; the corrected measurement is the paired walk under `C3`. See [balance-lab.md](balance-lab.md), [backlog.md](backlog.md) `F11`, `C3`, `C23`, `C27`. |

**G6 is not a balance criterion and is on the list on purpose.** The population's stiffness tail is
what decides lumping, multirate stepping and the substep cap, and no synthetic ladder can show its
shape.

**The two later criteria, and why each was added.**

* **`G7` — a ship the game spawns survives arrival.** Every vanilla prefab, idle, in the environment
  its category spawns into, for five simulated minutes, loses no block. It was added because the
  compatibility floor was a promise this page made and nothing scored, and it **holds**: 705
  prefabs, 461,428 blocks, not one crossing critical. See
  [Cooling is designed in](#cooling-is-designed-in--and-a-vanilla-ship-still-has-to-survive).
* **`G8` — the significant event lands in the window, and the ship is usable again inside a
  session.** The median time from load to the first block crossing critical falls in 120–300
  simulated seconds, *and* the median hull finishes cooling within an hour. It was added because
  [the pace the mod is meant to be played at](#the-pace-the-mod-is-meant-to-be-played-at) had been
  the stated balance target for as long as it had been unscored — and once scored it failed, at a
  median crossing of 10.4 s, which is what moved the defaults. Both halves are one criterion because
  one clock governs both.

### Cooling is designed in — and a vanilla ship still has to survive

**The intent is that players design cooling in.** A hull that was laid out with heat in mind is the
ship the mod is for, and that is why the sink face carries 6,250 W/K against a bolt joint's 167.

**It must not follow from that that a vanilla design is unusable.** This game has been out a long
time, and two cases are load-bearing:

* **A player adds this mod to an existing world and loads it.** Their fleet must not begin falling
  apart. This is the case `G1` protects, and it holds: 0.22% of the corpus goes critical at idle.
* **The game spawns a vanilla prefab.** Cargo ships, drones, encounters and respawn ships must not
  disintegrate on arrival. This is `G7`, and **it holds**.

So the balance target is a floor as well as a ceiling. `G2` asks that load bites; this asks that it
does not bite an unmodified ship that is simply *there*. The two are not in tension at idle, and
they are the two ends of the same dial under load.

> **All 705 prefabs the game ships have now been simulated.** Idle, in the environment each
> category spawns into, for five simulated minutes: **461,428 blocks across 705 ships, and not one
> of them crosses its critical temperature, let alone loses a block.** Run `-- prefabs`.
>
> **And the same 705 flown hard lose 616.** That is the control rather than the criterion — a floor
> that can only ever pass has not been tested — and the distance between the two numbers is the
> whole of what this mod is: a ship that arrives is safe, and a ship that is worked is not.
>
> Two things the walk cannot see, which are part of the result. **1,503 blocks are subtypes the
> installed game no longer defines** — coloured legacy armour, every one of them in
> `LegacyContent` — so they are not simulated. And one prefab is stored gzipped, which the parser
> now un-gzips; before that it was one ship the floor was never measured on.

### A base is a thermal problem too, and the place cooling can be done properly

**Decided 2026-08-25.** A station is not something that quietly works while ships are the
interesting case. It heats, it has to be managed, and a player who builds a refinery hall without
thinking about it should find that out. **What a base gets in exchange is room**: it can run
elaborate coolant systems and radiator farms far more effectively than a ship can, and that is the
intent rather than an accident of the model.

**The advantage is layout, not size.** A base can be *rearranged* around its cooling in a way a ship
cannot: nothing else constrains its shape. A ship's form is decided by thrust, mass, silhouette and —
once drag ships — by the air it moves through, so cooling competes with all of them for where a block
goes. A base has none of those constraints, so a player can put the refinery where the loop wants it
rather than where the hull allows, run the ring the long way round, and face the radiators away from
the sun for good. So *cooling is designed in* is most true where there is freedom to design, and a
base is where the mod's own lever is most available rather than where it stops mattering.

**Measured 2026-08-25, and the first half is confirmed while the emphasis of the second is wrong.**
The `station` scenario runs a station against a ship matched to one cell, 3,549 against 3,550, with
the station carrying exactly half the external faces. It settles **284.4 K** above ambient against
the ship's **236.1 K**, so a base is harder to cool and the claim above stands.

**What it gets in exchange is room to run *transport*, and that is not the same as room for
radiators.** In vacuum the penalty is the halved area and behaves like it. In air it is three times
larger than area explains, because convection pins the skin near ambient and what binds is the
conduction path from the middle out: the station's interior sits **39.9 K above its own skin** where
the ship's sits below it. Radiators standing on its roof help monotonically and never enough —
**153 of them, 43 % of the station's own block count, still does not reach a ship of the same
size**. So *elaborate coolant systems* is the load-bearing half of the sentence above and *radiator
farms* is the half that reads better than it measures: a loop is a path out of the middle, and area
is what a path delivers to. The figures are in
[balance-lab.md](balance-lab.md#what-it-said-two-of-four-hold-and-the-two-that-fail-agree-on-why).

**Room air is the part a base leans on hardest and it moves almost nothing here**: 0.31 K in 284,
across 48 pressurised compartments. That is a specimen and not a population — the corpus is ships,
so there is no published station population to check the shape against.

### Who it is balanced for

**Vanilla play, in the knowledge that worlds and blocks get modded.** The mod has to be a jack of
all trades, because who runs it and in what situation is unknowable: the intent is to **minimise its
potential to blow up in a player's face while not being a drag on performance**, and those two
together are the whole balance problem.

**The population it is balanced against is the workshop corpus, decided 2026-08-25.** 8,144
published blueprints is the best real-world dataset available and it carries both the general case
and the edge cases; every population figure in this repository is over it. **Its skew is known and
accepted rather than corrected**: published hulls are finished, large and often combat-oriented,
where a single-player world is mostly a starter ship, some welding and a base that grew over months.
So the corpus is the hardest honest proxy for what people build, not a census of what worlds
contain — which is why the scale target is a ceiling on ambition rather than on possibility.

The long-run intent is regimented tests covering every scenario a grid can find itself in, collecting
every figure that could bear on balance. That is what the lab and the corpus are for, and it is why
`G6` — a cost criterion — sits on a list of balance criteria.

**The evidence is not there yet, and the gap is one-sided.** All five corpus scenarios are in vacuum;
air costs about six times what vacuum costs and is where the substep budget is actually spent, and
the atmospheric evidence is 49 hulls against 8,132. Relatedly, the servers this mod is played on
commonly run a 300 m/s speed limit against vanilla's 100, which multiplies the friction term by 27
and the cooling term by 1.37.

### A cooling system costs power, and makes heat doing it

**A pump is a motor.** It draws electricity and it makes waste heat like any other motor, and being
part of a coolant loop does not exempt it. This mirrors the physics, which is the default under
[fidelity](#fidelity-is-the-default-a-saving-is-a-switch).

**Built.** A coolant pump draws through a `MyResourceSinkComponent` attached in code during `Init`,
because an upgrade module has no definition field for one — the same shape `ThermalHeatPumpBlock`
already used. The heat comes free with the cost: `ThermalBlock` subscribes to whatever sink an
entity carries and turns its draw into `PowerConsumedWatts`, which the waste-heat model converts
through the block's own fraction. That fraction is **1** for a pump, and the reason is physics
rather than balance: a circulator does no work that leaves the system, so its shaft power dissipates
as friction in the coolant it is pushing and its motor losses stay in the block.

**The rating is derived, not chosen.** A large-grid ring moves 200 kg/s of coolant — four
two-and-a-half-metre parcels a second at fifty kilograms each — and pushing that against two bar of
head at seventy per cent efficiency is `ṁ ΔP / (ρ η)` = 57 kW. Fifty is the rating; the small-grid
block is a fifth, on the same reasoning the heat pump's rating uses.

**The asymmetry with the heat pump is now a stated design rather than an accident.** The ring
carries 680 kW for every kelvin of difference around it, so a circulator costs well under a per cent
of what it moves, where a heat pump pays a third. Both blocks exist because those two prices are
different, and that could not be said while one of them was free.

An under-supplied pump circulates proportionally slower rather than stopping, so a ship whose
reactors are failing loses its cooling gradually rather than all at once.

**There is one cooler this does not reach, and the engine is why.** A player's suit regulates in both
directions and charges nothing for it: `IMyCharacter` exposes `SuitEnergyLevel` to read and nothing
to write, so a mod cannot take a player's suit charge. That is a limit rather than a decision — the
coupling exists in the one direction the game allows, since what the mod *can* see is a flat suit and
a flat suit does not regulate. It is recorded here rather than left as an inconsistency somebody
finds in the code, and it is worth revisiting only if a setter ever appears
([backlog.md](backlog.md) `C16`).

### What a block costs to build — what the game charges, and nothing invented

**The lever costs what the game charges for a block of that size and mass.** There is no comparator
to derive it from — the game ships nothing whose job is to move heat — so the mod's eighteen
`Cubes.xml` definitions are placed inside the distribution the game prices its own 1,434 blocks over,
on three ratios that say nothing about what a block does: kilograms a cubic metre, seconds of welding
a kilogram, and PCU a block. **All eighteen sit inside it**, and the two pumps at 100 PCU against
everything else at 1 is the only place the mod charges above the game's own median. See
[balance.md](balance.md#what-the-mods-blocks-cost-to-build); `BuildCostTests` holds it.

That is [game mod first](#the-governing-prior-game-mod-first) applied to the economy rather than to
the physics: **the mod does not invent one.** A recipe is a balance lever the game already has, and
using it would mean charging differently from every comparable block a player already knows the
price of.

**And the recipe is not secretly a thermal dial as well.** A block's mass is the sum of its
components and capacity is `mass × specific heat`, so the same number does serve both purposes — but
it reaches only half the behaviour. Measured with a radiator stack's mass multiplied by four, the
source settles within a hundredth of a kelvin of where it settled at ×1 and the stack takes 24, 50
and 112 seconds to get there. Radiation and conduction have no mass term; **a recipe moves when a
block gets there, not where it ends up**, and every figure the mod is balanced on is a steady-state
figure. So a component list can be set for what it should cost.

### Every thermal property is a dial

**A dial is anything that changes how heat moves through the system**, and a block's thermal
properties are all dials — conductivity, specific heat, emissivity, exposed-surface multiplier,
critical temperature, the waste fractions. Balancing the mod means turning them until the population
behaves; there is no separate class of "physical constants" that is off limits.

**The convention is where they start, not whether they may move.** A block's `Conductivity` and
`SpecificHeat` are written as the figure a materials table gives, because a definition that reads as
a description of the material is one a third-party author can write without asking anyone. The waste
fractions are chosen for their consequences and say so. Both are dials; they differ in what a
sensible starting value looks like.

This is why the settings and the definition files are converging into one surface — see
[configuration.md](configuration.md#where-the-settings-surface-is-going). A player asking how
fast coolant moves is asking a balance question, and it should not matter to them which file the
answer lives in.

### The pace the mod is meant to be played at

`HeatTimeScale` is the clock and 1 is real. **A world runs the clock at 90** — with full
integration accuracy, since real thermal time is physically honest and far too slow to play.
Dividing every heat capacity by *k* is exactly running thermal time at *k*×, so equilibrium
temperatures and every ratio between mechanisms are untouched; only the clock moves.

**It ran at 225 until 2026-08-24, and what moved it was `G8`.** The most significant thermal event
has to land in a 2–5 minute window, and at 225 nothing did: the median hull crossed its rating in
10.4 seconds. The clock alone cannot fix that — slowing it enough puts the hull outside the hour
`G8`'s other half allows for recovery — so the clock moved with the conduction pace, which is the
one dial that lengthens a crossing without lengthening a recovery by as much.
[balance.md](balance.md#the-route-is-chosen-and-it-is-the-one-the-cost-column-argued-against) has
the evidence and [backlog.md](backlog.md) `C12` and `C24` are the decision and the applying.

**There is one configuration, and it is the most faithful one the model has.** Presets were tried and
removed: a preset makes a block's behaviour depend on which one a world happens to run, and it puts
an approximation in front of a player who did not ask for one. What a world may still do is move an
individual setting. See [configuration.md](configuration.md), and [realism.md](realism.md) for how
far the model is from physics whatever the settings say.

---

## Performance intent

**A grid's steady cost is proportional to the grid, and a change to it is proportional to the
change.** Placing a block costs that block's degree, not the graph; the room map is budgeted and
resumable; a step is spread across the frames of its window rather than landing whole on one.

### The scale target

**A single grid of 250,000 blocks running at `SimulationSpeed` 1.0**, in either game. Stress bounds
above that are uncapped — the ladder is allowed to go wherever it goes, and it goes to a million —
but a quarter of a million at real pace is the figure the design is for.

> **This said 10⁶ until 2026-08-24, and the population is what moved it** (`G5`). The two numbers
> were both on this page, one as the target and one as *the realistic figure*, and nothing decided
> between them — which mattered, because they are answered by different work: 250k is a tuning
> problem and a million is a structural one. **Over 8,132 published workshop blueprints, not one
> reaches a million blocks in a grid**: the largest is 641,711, ten pass a quarter of a million, and
> the ninety-ninth percentile is 70,141. **What a blueprint population cannot see is what a world
> contains**: a station grown over months, or several hulls welded into one, reaches sizes nobody
> publishes — so this is a ceiling on ambition rather than on possibility, and a million-block grid
> is something a player can build even though nobody has posted one. That is exactly why it stays an
> uncapped stress bound, and why `bench franken` now welds the corpus's largest ships into a single
> grid: a bound is worth measuring on a real block mixture rather than on synthetic census tiers.
> [backlog.md](backlog.md) `G5`, [scale-design.md](scale-design.md#9-risks-and-open-questions).

Measured against the current ladder, on hulls built from the block census:

| Blocks | Full step | Tick | Substeps granted of 12 demanded | Against a 16.7 ms frame |
| ---: | ---: | ---: | ---: | --- |
| 126,731 | 22.29 ms | **2.43 ms** | 1 | comfortable |
| 505,566 | 67.06 ms | **13.18 ms** | 1 | inside |
| 1,000,294 | 118.20 ms | **25.87 ms** | 1 | **1.55× over** |

**The target is met on the ladder above and is not met in air**, which is the more useful reading of
it since `C27` priced what the allowance gives up. At a million blocks — the stress bound now — the
tick is over a 60 fps frame *and* the step is shortened by `MaxElementVisitsPerStep` to 1 substep
against the 12 the grid's stiffness asks for, so simulated time is not advancing at 1.0 either.
Closing that means not touching every node every step: activity tracking, chunking and multirate
stepping, all designed in [scale-design.md](scale-design.md) and none of it built. **And the tuning
problem bites an order of magnitude below the target**: a driven census hull in air stops keeping
real time between 32,000 and 64,000 blocks at the configuration that ships, which is where the work
actually is ([benchmarks.md](benchmarks.md#what-the-allowance-is-worth)).

### Use the machine, and stay off the game thread

**The mod should use as much of the CPU as it can while taking as little of the main thread as
possible.** The two halves are one goal: the game's simulation stability is set by what happens on
its own thread, so work moved off it buys frame-time stability *and* lets this mod do more.

**The fleet half is built and ships off.** A grid's tick splits into prepare on the game thread,
solve anywhere and publish on the game thread, and `ThermalGridScheduler` either walks the three per
grid or fans the solves out through `MyAPIGateway.Parallel`. `ParallelGrids` ships `false` until a
session answers three things a lab cannot — see [backlog.md](backlog.md) `D19`. So the paragraphs
below are what made it buildable rather than a description of what is missing:

* **The solver was already safe to split.** Order independence is one of the three invariants: every
  exchange reads start-of-step temperatures and writes into an accumulator, so no node sees
  another's new value and iteration order cannot change the result. That property was kept for
  correctness and pays for parallelism for free.
* **The engine allows it.** `MyAPIGateway.Parallel` offers `For`, `ForEach`, `Do` and `Start`,
  backed by `ParallelTasks`. See [engine-notes.md](engine-notes.md#parallelism-is-available-to-mods).
* **The shape is decided by what may race.** Reads must not race with the game mutating a grid, so
  the natural split is *solve in parallel, apply on the game thread* through `InvokeOnGameThread`.

**Where the payoff is depends on size, and the fleet half is now measured.** Per-grid parallelism
across many grids and splitting one huge grid across threads are different changes, and the first is
the one the fleet figure argued for. It pays: a 242-grid fleet steps **10.17× faster on 32 threads
and 7.09× on eight**, the hand-off that was expected to eat it is 1.6–6.8 µs against a grid's own
0.54 ms, and a fleet stepped one grid per thread is bit-identical to one stepped in order. What
bounds it is the largest ship — an uneven fleet gives 3.35× because a step cannot finish before its
biggest grid does — which is what makes splitting one grid the *second* change rather than a
substitute for the first. A million-block step is still 118 ms and atomic. See
[scale-design.md](scale-design.md#one-grid-per-thread-measured-and-built).

Two things must survive it, and both are already rules: the three invariants (`C6`), and that
nothing allocates on the stepping path (`C4`).

### What an unattended grid gets

**The same simulation as an attended one.** A grid nobody is looking at is stepped like any other,
because what a player finds when they return is exactly the kind of difference they can perceive —
and [fidelity is the default](#fidelity-is-the-default-a-saving-is-a-switch).

The saving is real and is worth having as an option: three identical capital ships took 81% of one
world's thermal budget with at most one player aboard, and a per-grid rate tier driven by distance,
by whether anything is reading the grid, or by whether anything on it is *doing* anything would take
two thirds of that away. The model already permits it — steps are energy-conserving and
order-independent, so a grid can advance coarsely and catch up in one long step.

**It is a switch, not a default**, and what it gives up is stated: a coarse-stepped grid reaches the
right steady state and a different transient. For a derelict that is correct. For a ship the player
left with a reactor overheating it is the difference between finding it destroyed and finding it
fine.

**A long run is designed for its own death** (P13): capped, resumable, streaming, and counting what
it did not measure. This is intent about the harness rather than the mod, and it exists because an
uncapped corpus sweep has taken a machine down.

---

## What the mod does when it cannot afford itself

**Decided 2026-08-25: it runs slow, and it does not approximate to keep up.** When a grid cannot
afford the substeps its demand asks for, the step is shortened and the grid's thermal clock falls
behind real time — measured at 36 % of real on a 64,000-block hull in flight at the shipped
allowance. The alternative was built the same day and ships **off**: flooring the stiffest blocks to
what the budget grants keeps the clock whole for about 0.024 K.

**And the mod does not apologise for its cost.** There is no guided troubleshooting, no *what should
I change* readout, and no intention to build one: the dials are documented, the status page says what
is happening, and a player who dislikes the trade can turn mechanisms off or remove the mod. That is
a legitimate outcome rather than a failure. **The performance criterion is the author's own play** —
this mod is used heavily by the person writing it and development continues until it meets that bar,
which is a harder and more honest test than a support surface.

**The reason for choosing the slow clock is that the player has the dials and the mod does not have
their intent.** An approximation applied on their behalf is the mod deciding that a number they
cannot see matters less than a frame rate they can, and this page refuses that elsewhere. **What
makes it a fair choice rather than a silent one is that the degradation is visible**: the settings
menu's status reads `substeps N of M asked, X floored, Y% rate`, so a world running behind says so
and the dials that fix it are on the same screen.

**It slows down. It does not stutter, and it does not lie.**

This is the failure mode the design chooses, and it is chosen rather than inherited: an
over-subscribed grid gets a *shorter step at the same fidelity* rather than a coarser one, so its
thermal clock runs behind the world's and nothing about the physics changes.
[scale-design.md](scale-design.md#7-scheduling-budget-not-quota) states it for a scheduler that does
not exist yet — *heat visibly diffusing a little slower is a far better failure mode than a stutter*
— and `MaxElementVisitsPerStep` is that intent already shipped.

**What was not stated is that it has a price, and it is the largest one the mod pays.** A slow clock
is invisible on a parked ship, because two hulls heading for the same equilibrium agree once they
arrive; under a load that is *moving* it is worth 1.19 K at a 5 % deficit and 36.98 K at 60 %. So
this is not a free guarantee bought with an approximation nobody can see, and the tension with
[fidelity is the default](#fidelity-is-the-default-a-saving-is-a-switch) is real — it is the first
row of the [conflicts table](#where-the-goals-and-the-code-disagree) that neither side wins outright.
The resolution the code follows is: **frame stability outranks fidelity, and the price is measured
and paid down rather than denied.** `C27` doubled the allowance the day the price was first put in
kelvin.

**Determinism is chosen, not assumed.** The solver is order-independent by construction and a fleet
stepped one grid per thread is bit-identical to one stepped in order, which is what makes
parallelism safe to add. A wall-clock budget would end that — two machines would spend different
budgets and reach different answers — so the scheduling design records the choice it would force:
either the server alone simulates, or the budget is expressed in work units rather than in
milliseconds. Nothing in the mod spends a wall-clock budget today.

**A failure records itself whether or not anyone asked.** Everything else the mod observes is off
unless something is reading it; a caught exception is not, because it costs nothing until the mod
has already failed and by then it is the only evidence there will be. The first occurrence of each
kind reaches the game log, the count reaches the closing summary, and a grid that has gone
numerically bad counts as a fault too — a NaN destroys a save rather than degrading a frame. The
guard exists so that a defect in this mod does not take the session with it, and the record exists
so that the guard is not also a way of hiding the defect.

---

## When the game's own rules are suspended

**Heat runs unchanged, because creative suspends scarcity and heat is physics.** A ship spawned
whole, a block placed instantly, a player in god mode, a world with no grinding and no components —
the simulation behaves identically through all of it, and that is the decision rather than an
accident of nothing having been branched.

**The game itself draws the line in the same place.** In a creative world blocks still take damage,
gravity still pulls, thrusters still push and collisions still happen; what stops is being charged
for any of it. Heat is on the near side of that line. It is a property of a ship — its surfaces, its
mass, what it is running — rather than of how the ship was paid for, and a hull that would cook in
survival cooks in creative because it is the same hull.

**The lever is a setting rather than a mode.** A builder who wants heat simulated and harmless has
`EnableDamage`; one who wants a mechanism gone has its own switch, and off costs nothing
([Isolated](#isolated--every-mechanism-has-its-own-switch), `P8`). Those are the right levers
because they are the player's decision, made once and visible in the world's configuration, rather
than something inferred from the world's mode and silently different from the world next door.

**Two of the four sub-cases already have criteria and they agree.** A ship the game spawns is `G7` —
705 prefabs, 461,428 blocks, arriving idle, not one crossing critical — and a world somebody adds
the mod to is `G1`. The third, a block that appears whole, is the same rule in both modes: a welded
block arrives at ambient, and creative changes when that happens rather than what happens. The
fourth, god mode, has no thermal meaning: a player who cannot be hurt is not a heat sink.

`SuspendedRulesTests` pins it, because a claim about what code does *not* do rots the moment
somebody adds the line: nothing in the mod's sources reads the game mode, the creative flags or the
creative tools.

---

## What language the mod speaks

**The mod speaks the player's language where it sits inside the game's HUD, and English where it is
the mod talking about itself.** The boundary is not a compromise between the two — it follows from a
commitment this page already made: the readouts are drawn through Rich HUD *so that a player reads a
temperature the way they read a power figure, with nothing mod-shaped announcing itself*. The game's
own HUD is localised. A readout that is not is mod-shaped by definition.

Counted over the mod's sources, the distinct strings the mod puts in front of a person are:

| Surface | Distinct strings | Language |
| --- | ---: | --- |
| Block names and descriptions | 30 | **localised**, and already done |
| The crosshair readout, cockpit summary, terminal panel and cues | **47** | **localised** — inside the game's HUD |
| The settings menu | 250 | English |
| Chat replies | 90 | English |
| Debug overlays | 113 | English |
| Telemetry files | 604 | English, and not on screen at all |

**So the surface that has to be translatable is forty-seven strings**, **4.3 %** of what the mod
writes, and the mechanism is already proven by the thirty block names in
[MyTexts.resx](../Thermodynamics/Content/Data/Localization/MyTexts.resx). `LocalisationSurfaceTests` counts the table above
and holds the claim as a band, because telling a sentence from an identifier without a compiler is
approximate and pinning the exact count would pin the heuristic rather than the finding.

**And the rest is English on purpose rather than by neglect.** A settings label is the name of a
value that appears in the world's configuration file, in [configuration.md](configuration.md) and in
this repository in English; translating the label without the file makes the two harder to connect,
not easier. Chat replies answer commands that are typed in English. Debug overlays and telemetry are
read by whoever is diagnosing the mod, which is the same audience as the source.

**One thing is unverified and blocks the work rather than the decision**: whether a mod's own
`MyTexts` keys resolve at runtime from C#, as opposed to inside a definition where they demonstrably
do. Nothing outside a session can answer it, and shipping a readout that draws a key name instead of
a temperature is worse than shipping one in English — so the forty-seven wait on that check the same
way `A9` waits on `F5`. See [backlog.md](backlog.md) `B39`.

---

## What the mod promises the things around it

The mod is not alone. It sits inside a world somebody has already been playing, beside blocks it has
never heard of, under mods written against it, and across a wire from a client it does not control.
**Every one of those relationships has been honoured consistently in the code and stated nowhere**,
which is how it comes to be a section rather than a rule: a promise nobody wrote down is a promise
the next change breaks by accident.

### To an existing world: nothing a player already has is quietly lost

> **These promises bind from release, and the mod is not released yet (2026-08-25).** Before then a
> behaviour change is checked against the corpus — which is what the corpus is for — rather than
> against a compatibility promise, and *the player suffers* is an acceptable answer to a change that
> makes the mod better. **Two things follow that are easy to get backwards.** The identifiers a
> world holds — block subtypes, the mod-storage GUID, the retired definition names — are *reserved*
> rather than depended upon, so a rename that includes them is available now and will not be after
> release; `modinfo.sbmi` carries workshop id 2985582372 today, which reserves the entry without
> making anyone's world depend on it. And the machinery below is built and worth keeping regardless:
> it costs nothing to have, and the day it starts mattering is a day nobody gets to choose.

**The mod is added to worlds that are years old and returns to worlds it has already saved.** Both
directions matter, and the pattern the code follows is the same in all four places it appears:
*read what the old thing meant, and add rather than replace.*

* **A save from an older build loads, and a save from a newer one loads on an older build.**
  `ThermalStorageCodec` reads version 1, and version 2 grew **by adding a section rather than by
  changing its marker**, so a reader skips what it does not recognise. A room whose shape changed
  while the world was closed simply starts from its surfaces, exactly as it would have done
  mid-session.
* **A renamed definition property keeps its old spelling.** Both are read, current first. Definition
  Extensions matches on the string, so dropping the retired name would silently revert a
  third-party definition to the shipped defaults **with no error and no log line** — which is the
  worst shape a break can take. The old names are not planned for removal.
* **A setting's name is its address.** `/thermal set`, the mod API and the settings sync all reach a
  setting by name. Where the file is organised may change; what a setting is called may not.
* **A world's tuned values survive a change to the file that holds them.** Defaults live on the
  fields, so a reader that finds nothing leaves them alone — which is exactly why regrouping the
  config file has to migrate the old flat shape first, and why that step is last rather than first
  in [where the settings surface is going](configuration.md#where-the-settings-surface-is-going).

**This is not a promise of forever.** The API's own wording is the honest form of it — *keys will
not change meaning within a major version, new keys may be added, and a missing key means an older
build* — and the same reading applies to the other three.

### What the version number governs, and what moves it

**`ThermalApi.Version` governs the delegate table and nothing else.** It is `1`, it is served as
`ApiVersion`, and [api.md](api.md#binding) tells a caller to read it and refuse a major it was not
written against. The mod itself carries no number: `modinfo.sbmi` has a workshop id and no version,
no build is stamped, and the only other version string in the tree belongs to the vendored
`NetworkAPI`. That is the right shape — a number exists where somebody outside has to make a
decision from it, and nowhere else.

**The major moves when a caller written against the previous major could still bind and then be
wrong.** That one sentence decides every case that used to be undeclared:

| Change | Major moves | Why |
| --- | --- | --- |
| a key goes away | **yes** | the caller's lookup fails, or it silently keeps a stale delegate |
| a key's signature changes — a widened `Func`, a `MyTuple` grown a field | **yes** | a caller casts to the exact delegate type |
| a key's meaning or units change while its signature does not | **yes** | the worst case: it binds, runs and is wrong |
| a key is added | no | a caller that has never heard of it is unaffected |

**Two of the three are checked** (`ApiVersionMovesWhenTheSurfaceBreaks`): the published surface is
recorded in `ApiSurface.txt` beside the major it belongs to, and a removal or a reshape fails the
suite unless `ThermalApi.Version` has gone up in the same commit. A version that moves *without* a
break fails too, because a caller refused for nothing is also a break. The third — meaning without
signature — is the one no test can see, and it is written into that file so the whole rule lives in
one place rather than only the half a check can reach.

**The other three promises sit under no number, and they are stronger for it.** The save format
grows by adding sections rather than by moving its marker, the retired definition names are read for
as long as anyone is reading, and a setting's name is its address. None of those has a boundary
because none of them is planned to have one: they are *never* rather than *not within a major*. If
one ever needs to break, it needs its own statement here, not a bump to a number that governs
something else.

### To a block it has never heard of: derive, do not guess

**A definition is optional.** A block from any mod gets thermal properties derived from what the
game already says it is built of, and `Cubes.xml` carries only deliberate deviations from that
derivation rather than an entry per type. Two consequences are the intent rather than the mechanism:

* **A partial entry is merged, not substituted.** An author who wants one block to run hotter writes
  a `CriticalTemperature` and nothing else, and keeps the material its components imply. That is
  what makes a third-party definition a one-line change instead of a table.
* **Landing on the global fallback counts as no answer.** The fallback describes mild steel, which
  was the best guess available before build cost could be read and is a worse one now — so it does
  not override a derivation that actually describes the block.

This is the same disposition as the naming convention in
[every thermal property is a dial](#every-thermal-property-is-a-dial): a definition should read as a
description of the material, so that somebody can write one without asking anyone.

### To another mod: the API is a contract with four guarantees

**Decided 2026-08-25: the API is open and unbounded, and that is a position rather than an
oversight.** Any mod may call any endpoint and register whatever it needs to tell this one how it
behaves — a control surface, a drag profile, a heat source, a threshold. Registrations are **not
capped, not vetted and not refused for being unbalanced**: a player chooses their mod list, and a
mod that ruins their game is a choice they made. The intent is that an add-on has *as much* control
over this system's inputs and as much visibility of its outputs as can be given.

**And the mod is judged on its own.** It has to be self-contained and to give a good experience with
nothing else installed; what an add-on does to balance or to frame time is the player's business and
not a constraint on this design. So every criterion here is a claim about this mod alone, by
intention rather than by caveat.

**What follows from that is a smaller duty than it first looked.** If the mod will not refuse a
registration, it has to be able to say what registrations are doing — otherwise every handling
complaint, every frame-time complaint and every "my ship exploded" arrives as this mod's bug. So the
diagnostics would be easier to read if they could attribute a registration to the mod that made it
([backlog.md](backlog.md) `K21`) — but that is a convenience for whoever is debugging rather than a
requirement, because the balance question it would have answered is one this page has just declined
to ask.

[api.md](api.md#guarantees) states them and this is what they are for. **No call throws into the
caller** — bad arguments come back as `false`, `0` or `NaN`, because an exception crossing a mod
boundary lands in somebody else's session with this mod's name on it. **No call is bound to a thread
or an update phase.** **Delegate signatures use whitelisted types only**, so an in-game script and a
mod can both bind. And **keys do not change meaning within a major version**.

Together they are what makes the table safe to publish at all — the *why* under
[Open](#open--the-api-is-part-of-the-contract), which states the goal and the check.

### To another mod that also slows a ship down: the same answer, and it is stronger here

> **Superseded 2026-09-24 by decision** ([aerodynamics.md](aerodynamics.md) `AR15`): this mod's embedded
> RelativeTopSpeed port is removed, and the original mod owns top speed. This mod reads RTS's read-only API to
> explain which limit binds, and both forces act (cruise curve, then drag). The section below is kept as the
> record of the earlier position.

**`EnableDrag` ships off, so adding this mod to a world running
[RelativeTopSpeed](https://github.com/Gauge/RelativeTopSpeed) changes no force at all.** That is the
whole of the collision handled, and it is handled by the same switch the section below argues for
rather than by a special case: the collision cannot happen by accident, only by a world turning it
on, which is a decision rather than a surprise.

**Detection is refused, and here it would actually be possible**, which is why it is worth saying
no to explicitly. RTS ships an API and uses the same `SENetworkAPI` this repository vendors, so
unlike a second heat mod it *could* be seen. It still should not be: a mod that stood down because
it saw something it thought was a rival would take a working world's drag away, and it would do so
depending on load order and on another mod's version. That is the same argument as below and it does
not weaken for being about force instead of heat.

**And the composition a world wants is one or the other, not both** — which is a measurement rather
than a preference. RTS holds each grid under a cruise speed *because it has no drag*; this model has
drag, and **11.6 % of published ships balance below the 100 m/s the engine already enforces**
([backlog.md](backlog.md) `K11`, corrected twice: for a projected-area error under `K12`, and on
2026-08-31 for a scoring tool that defaulted to twice the coefficient the mod ships). So where the air
is thick the two forces overlap, and where it is thin RTS's acts alone — a world that wants either
can have it by switching the other off. What would be better than
both is `K17`: one mod owns the force on a constraint group and the rest contribute to its inputs.
That is an interface nobody has built yet, and until somebody does, the switch is the answer.

### To another mod that also simulates heat: nothing, and the switches are the answer

**The mod behaves as though it is alone, because it cannot tell that it is not.** There is no
registry of thermal mods, no engine mechanism for discovering one, and nothing to defer to if it
found one. Guessing would be worse than not looking: a mod that stood down because it saw something
it thought was a rival would take a working world's heat simulation away.

**What cannot collide is everything the mod owns outright.** The API channel is a number this mod
chose (`2985582372`), the block storage is a GUID (`f7cd64ae-…`), and the components it attaches are
its own types. A second heat mod has its own of each and neither can reach the other's.

**What collides is everything a block has only one of**, and it is four things:

| | What a player sees |
| --- | --- |
| two block temperatures | two readouts disagreeing, with no way to say which is the block's |
| two damage sources over one threshold | a block failing about twice as fast as either mod intends |
| two glows on one emissive | flicker, or whichever mod wrote last |
| two panels on one terminal | clutter — the only one of the four that is merely untidy |

**The answer is the switches, and it makes coexistence a world's decision rather than a code path.**
[Every mechanism has its own switch](#isolated--every-mechanism-has-its-own-switch) and off costs
nothing, so a world running two heat mods turns this one's `EnableDamage`, `EnableSuitDamage`,
`HeatGlow` and `HeatWarningSound` off and keeps its simulation and its API — which is the
composition that actually works: one mod owns the consequences, the other is read through
`ThermalApi`. **Two mods both applying consequences is unsupported**, and that is now a sentence
rather than an unexamined case.

**The better answer than detection is an interface, and it is on the list.** Two mods each applying
their own force to one ship is a collision; one mod owning the force while the others contribute to
its inputs is an architecture — a control surface registering a surface, a jet engine declaring a
smaller profile nose-on. That is [backlog.md](backlog.md) `K17`, and it is a stronger coupling than
the heat API has ever asked for: a registered heat source changes a temperature, a registered
surface changes how a ship flies.

**One mod beside this one can be detected, and it is worth naming because it is the exception.**
[RelativeTopSpeed](https://github.com/Gauge/RelativeTopSpeed) publishes an API and speaks the same
`SENetworkAPI` this repository vendors, so *is something else already slowing ships down* is a
question with an answer — unlike *is something else already simulating heat*, which is not. The current decision is `AR15`: RTS owns the cruise curve and this mod owns atmospheric drag;
both may act. `EnableAeroForces` can disable this mod's force channel for a different composition.
The embedded top-speed port remains removed.

**Every output can be turned off, and the last one that could not was fixed for this.** The
terminal panel had no setting of its own until 2026-08-25 — every other thing the mod draws was
already behind something — so `HeatTerminalPanel` exists and the *clutter* row above can be turned
off with the rest ([backlog.md](backlog.md) `B40`). It gates the panel's text and not the block's
own controls.

### To a client: the server trusts nothing the client asserts about itself

The split of what is replicated is in [the section below](#what-the-mod-owes-a-multiplayer-client);
this is the part that is about trust rather than about traffic, and it is the reason two of the
three channels exist.

* **An administrator's authority is checked against something the sender did not write.** A
  setting change from a client goes over the engine's own secure handler, because `SENetworkAPI`'s
  sender id is a field the sender fills in and a promote-level check cannot be gated on it.
* **A client may not write temperatures onto another client's simulation.** Same reasoning one
  channel along: the from-the-server flag is the only thing that says a packet is the server's.
* **The server is authoritative over damage** (`C10`). Every machine simulates, and exactly one
  machine destroys — so the one conclusion with a world-visible consequence is never reached twice.

---

## What the mod owes a multiplayer client

**Single player is the design centre, decided 2026-08-25.** Multiplayer has to work and is not what
the mod is tuned against: where the two compete for attention, the single-player experience wins.
Two consequences follow and are better stated than discovered. The replication layer **has never run
in a session** and that is an accepted risk rather than an urgent one. And the stress case for a
server is written down as a number — **50 players connected, up to 20 of them watching the same 20
grids** — so that when it is built it is built against something ([backlog.md](backlog.md) `K20`).

**As little traffic as possible, and deviation is acceptable to a point.** Keeping every block's
temperature in sync across the network would degrade the thing the mod is trying to protect, so the
server sends the minimum that keeps the world coherent and lets the rest drift.

| | Where it lives |
| --- | --- |
| **Damage** | Server. It propagates damage and it is authoritative over what a block loses. |
| **Settings and pump controls** | Server, replicated. 44 of the 49 serialized fields reach every client. |
| **Temperatures** | Each machine's own simulation, from the same inputs. Not reconciled. |

**Drift is the design, not a defect.** The original shape was to seed a client's temperatures once
at load, let them diverge, and re-sync a grid in full when it was worth doing. That is probably not
the final answer, but the implication holds: **transmit as little as possible, and tolerate deviation
up to a point.**

**What is not yet decided is where that point is.** Nothing states how far a client may drift before
it must be corrected, or what triggers a re-sync — and the answer matters because divergence stops
being cosmetic the moment a player makes a decision from a readout the server will not honour. A
client watching a block sit below critical while the server destroys it is the case to design
against.

---

## Portability intent

**One simulation model serves Space Engineers 1 and 2 — the model, not a shared binary.** The two
games get separate builds and separate adapters, and the line between model and adapter is the point
of the architecture: everything the game supplies crosses one of three boundaries — block layout, an
environment sample, and results out — and nothing else.

The remaining distance is storage rather than mathematics. The geometry, the conduction graph and
the integrator already work from integer AABBs and cost the same whatever a block's volume;
`GridModel.blocksByCell`, `SurfaceMap`'s cell table and `BlockInstance.Cells` are still one entry per
occupied cell, which is what a 0.25 m lattice cannot afford. See
[scale-design.md](scale-design.md#cell-centric--boundary-centric).

---

## What this mod deliberately is not

**Nothing is refused yet, and that is sequencing rather than permissiveness.** Every mechanism has an
off switch, every dial takes the value it is given, and the API accepts any registration. Whether
there are combinations the mod should refuse — configurations that are silently broken rather than
merely slow — is a question that needs the hard boundaries found first. **Soft limits come after
extensive testing has said where the end state actually breaks down**, because a guardrail placed
before the edge is known is a guess that will be in the wrong place and will be trusted anyway.

Recorded so nobody rediscovers a decision as a bug. Each carries its price in
[known-issues.md](known-issues.md#deliberate-limits).

**A thermal camera is not on this list.** It was, on the grounds that mods get no shader and no
frame buffer. That is a statement about difficulty, not about intent — it is wanted. See
[Thermal vision](#thermal-vision--wanted-method-unknown).

* **Not a per-block radiative transfer model.** A face radiates to the sky or to nothing: two hot
  blocks facing each other across a gap do not see each other, and no view factors exist. A surface
  *can* now be shiny to the sun and black to space — `SolarAbsorptivity` is separate from
  `Emissivity` — but each is one constant rather than a curve against wavelength.
* ~~**Not a flight model.**~~ **It is one, as of 2026-09-24, by decision: aerodynamics as realistic
  as the game allows, learning from SEDrag where it is right** — the requirements are
  [aerodynamics.md](aerodynamics.md) `AR1`–`AR22`, built as [backlog.md](backlog.md) `K25`. The
  reasoning below argued against a flight model on the grounds that the solver's Newtonian sum is
  wrong in the regime ships fly in. That is still true, and it is now the reason to replace the sum
  with a subsonic force channel rather than the reason to stay out of handling. What follows is kept
  as the record, superseded.
  **Reversed for lift 2026-08-31, by decision.** This bullet refused lift for ten
  months of change log and one day of calendar, and the refusal is lifted because the owner asked
  for aerodynamic lift implemented and configurable ([backlog.md](backlog.md) `K23`). **That is a
  choice about what the mod is for, not a measurement that overturned one**, and it is recorded that
  way so nobody later reads it as evidence. What changed on the evidence side is narrower and real:
  `K22` closed the arithmetic gap the refusal named — every node now carries a reconstructed surface
  normal, so an angle-of-attack response exists where six axis face weights could not give one — and
  lift turns out not to be a new model at all but the transverse component of a sum the mod already
  computes and discards. **The reasoning that argued against it still stands and is kept below**,
  because it is what the switch defaults to off for and what the criterion in `K23` is written
  against. Drag is applied because it closes an energy hole; **lift was refused because it opens a
  handling claim**. The two are not the same kind
  of change, and the test that separates them is *does the mechanism correct something the mod
  already computes wrongly, or assert something it does not compute at all*. Drag is the first: the
  energy is computed, the momentum is discarded, and the discrepancy is a defect. Lift is the
  second: **lift does no work**, so no energy accounting demands it — nothing in this model is
  inconsistent for its absence — and supplying it would need a coefficient nobody has and an
  angle-of-attack response the six face weights cannot give.
  **And the two fail differently when wrong.** What the solver has is a Newtonian flat-plate model,
  right in free-molecular hypersonic flow and wrong everywhere a ship actually flies; a heat model
  wants the energy right and does not care where the force points, while a flight model is judged on
  handling. A wrong heat term is invisible. A wrong lift term is a ship that flies badly, and a
  player would rightly call that a broken mod rather than an approximate one. **A centre of
  pressure is still worth computing**, because drag applied at the centre of mass produces no
  torque and the offset between the two is the price of that simplification — a diagnostic, not
  a force.
* **Not a build-state simulator.** A block at 10% construction carries its full thermal properties.
  The machinery to change that exists and the difference would be invisible next to the heat a
  block's neighbours carry.
* **Not a destruction model, in the lab.** The harness never removes an overheating block, so every
  peak above critical describes the harness rather than the mod. Crossing times are unaffected.
* **Not authoritative over the game's own systems.** Room pressure is the game's answer, not this
  model's, and none of the three sources that can empty a room may insist on air — only refuse it.
  ~~**The mod reads a grid's velocity and writes nothing to its physics.**~~ **It did from
  2026-08-30, was stripped on 2026-09-24, and now has a separate per-tick drag/moment adapter** —
  native handling and population validation remain open under `K25` ([aerodynamics.md](aerodynamics.md) `AR4`,
  `AR19`, `AR20`). It was built as `EnableDrag` under [backlog.md](backlog.md) `K1`. *Whether
  the mod should push a ship at all* was the first question in that milestone rather than an
  assumption inside it, and the answer turns on what kind of change it is: the friction term already
  computes the rate at which the air does work on a hull, turns it into heat, and takes nothing from
  the motion — so **energy enters the world with nothing paying for it**, a median 5.05 MW on a
  published hull at reentry. Applying the force does not add a claim; it closes an accounting hole
  the mod already had.

  **That was the line, and it is why `K8` refused lift at first.** Drag corrects something the mod
  computes wrongly; lift asserted something the mod did not then compute at all. `K22`'s
  reconstructed normals made lift the transverse component of a sum already computed, and `K23`
  reversed the refusal by decision — see *Not a flight model*, below, where the reversal and the
  reasoning it keeps are recorded.

---

## Where the goals and the code disagree

Each row is a conflict between two statements this repository makes, or between a statement and what
ships. **The resolution column says which the implementation currently follows**, which is evidence
about intent rather than a decision on the developer's behalf.

| Conflict | Resolution |
| --- | --- |
| **The element-visit allowance against *fidelity is the default*.** `MaxElementVisitsPerStep` ships at 4,000,000 rather than at `0`, which is its faithful end — so the shipped default is not the most faithful configuration the model has, which is what [that section](#fidelity-is-the-default-a-saving-is-a-switch) and `C15` both ask for. `TheDefaultIsFrameBounded` states the reason as *the budget costs no accuracy*. | **The reason as stated is true and incomplete, and the default is right anyway.** The bound shortens a step rather than coarsening it, so nothing is *approximated* — but the grid's thermal clock then runs behind the world's, and `C27` priced that — [slow down, do not stutter](#what-the-mod-does-when-it-cannot-afford-itself) carries the figures and how they compare with the two approximations this mod already weighs. So it is the largest approximation the mod ships, not a free one. **What the code follows is that frame stability outranks fidelity**: a mod that drops frames is uninstalled whatever its physics is, and the honest form of the promise is [slow down, do not stutter](#what-the-mod-does-when-it-cannot-afford-itself). The price is paid down rather than denied — the allowance doubled the day it was first put in kelvin. What the comment left out was not that there is no cost but that the cost it names, *less simulated time*, had never been converted into anything a player would notice; it carries the figure now. |
| **`G6`'s cost half against the unit it was scored in.** The criterion compares a step's element visits against what the allowance grants, in `links + 4 × nodes`. Every figure published for it was computed with the corpus's `joints` column standing in for the link count. | **The measurement wins, the criterion does not move, and the figures are withdrawn.** A joint is a mechanical joint *between grids* — a rotor or a piston — and there are none on almost every blueprint, so `links + 4 × nodes` was evaluating to `4 × nodes`: the node half alone, **1.51× low** on a 2,000-block census hull and low by its own link-to-node ratio on any other. Neither existing dataset can be rescored, because the count was never recorded, so `verdict.py` reports the cost half as *unmeasured* rather than reprinting the old arithmetic and `G6` reads as one half unscored rather than as failing (`E8`, `P2`). No verdict changes: the vacuum figure stays inside the allowance and the air one was already outside it. This is the same class of error as the `2.125`-against-`4` correction of the same week, one level down — that one had the right count in the wrong currency. |
| **The corpus as the population against the corpus as *what people publish*.** Every population figure this repository quotes is over 8,142 workshop blueprints, and the scale target, the stiffness tail and four of the eight criteria are decided on them. | **The code follows the blueprints and the pages say what that cannot see.** A blueprint is the closest signal available to *the designs people build and fly* — ranked by unique subscriptions rather than by votes, which would over-weight the spectacular, or by date, which would over-weight the untested. What it cannot see is a world: a station grown over months, or several hulls welded into one, reaches sizes nobody publishes. That is exactly why `G5`'s answer moved the scale *target* to 250,000 and left the million as an uncapped stress bound rather than dismissing it. A population of blueprints is a ceiling on ambition, not on possibility. |
| **`G5`'s rationale against the measured damage timing.** `G5` is *"a player must be able to react to a warning"*. Of ships that cross critical under full electrical load the median crosses at **8.9 s**, and at p10 at 3.5 s. | **Resolved: the goal holds, and the conflict was a mis-measurement.** The crossing is the moment the damage rate leaves *zero*, not the moment a block is lost — the first block goes at a median **37.0 s**, and with the cue's three-second lead the median pilot has **40 s** from the first warning. `G5` holds as written and its rationale holds too. See [balance.md](balance.md#how-long-a-block-has-after-it-crosses). |
| **The 2–5 minute significance window against `HeatTimeScale` 225.** The balance target asks for the most significant thermal event to land in a 2–5 minute window. **No block in the game lands in it** at the shipped pair, and none can be made to at 225 by the clock alone: of the 72 block types that cannot cool themselves, 0 fall in 120–300 s. | **Resolved 2026-08-24 by choosing, and the choice is a trade rather than a free win.** Both routes reach the window on the measured fleet — across the 421.9 s jump-drive charge the shipped pair puts the median hull past its rating at **10.4 s**, conduction ×4 at 165.4 s and waste ×0.5 at 134.5 s — so the question was never which works. **Resampled from the same population, conduction ×4 holds `G8` on 76 % of fleets and waste ×0.5 on 33–37 %**: the crossing median is censored above, it exists only while more than half the hulls cross, and the waste route puts 22 of 40 past critical against conduction's 25. Two hulls either way is a criterion satisfied or a criterion with no median at all. So the provenance argument that had settled this *against* conduction was an argument against the route that turns out to be worse on the criterion itself. **What conduction costs is the conflict this row is about, and attempting to ship it priced it**: at ×4 a coolant sink stops out-performing the best surface dial (73.5 K against a dial's 135.3 K), bolting starts working, the stiffest fitting stops responding to air, and the cooling ladder's control stops losing — *there must be a lever* is what buys `G8`'s timing. **The route is chosen and applied**, on 2026-08-24: the defaults are `ConductionScale` 9.6 and `HeatTimeScale` 90, `G7` was re-scored first at 0 of 705 prefabs crossing critical, and `G6` passed as a side effect — the demand it is scored on is convection-limited and came down with the clock, from 115 % of the substep cap to 55 %. Reading the 47 tests the pair moved opened four rows: [backlog.md](backlog.md) `C25`, `C26`, `C27`, `C28`. Previously, and corrected — the load route was read as the better trade on cost and provenance, before either was measured against how often the criterion holds: | The load route is the better trade, and the provenance it was held up on is now settled against it: weighted by the heat they carry, **76.3 %** of a loaded fleet's waste comes through a fraction derived from the efficiency the game itself states and 15.3 % through an invention, so waste ×0.5 halves a sourced number and is an admitted balance knob rather than a correction. [backlog.md](backlog.md) `C12`, `C21`. Previously, and corrected: | With every jump drive charging the median hull crosses critical in 10.4 s; with them full fewer than half the hulls ever cross, in the shipped configuration and in both retunes alike. Drives are 71.3 % of the load's heat, so the mod is both too fast and too slow depending on one block. `G8`'s *most significant thermal event* is the drive charging, which is a transient, and both bounds model it as permanent or absent — so what is missing is a duty-cycled load case ([backlog.md](backlog.md) `F13`), not a dial. Previously read as a choice between two retunes: | Two dials reach the window. Conduction ×4 with `HeatTimeScale` 80–120 reaches it and **costs three levers**: a coolant sink stops out-performing the best surface dial, bolting starts working, a buried 300 MW reactor settles inside its rating, and the stiffest block stops responding to air — which is *there must be a lever* going away to buy `G8`'s timing. The load — waste ×0.5 with the clock at 80–110, at conductivity ×1 — reaches the same window with every lever untouched, at 0.43× the substep demand, and **costs a quarter of the bite** instead: the median hull peaks 264 K cooler and loses one block rather than four. What decides between them, or says neither is needed, is that the load scenario charges every jump drive continuously ([backlog.md](backlog.md) `F13`) and half the waste heat is inside the distance between that bound and a realistic load. Full detail: [balance.md](balance.md#the-load-reaches-the-window-too-and-costs-the-bite-instead-of-the-levers). Previously: | Conductivity ×4 with `HeatTimeScale` 80–120 reaches the window — crossing 124–186 s, recovery inside the hour, `G1`, `G2` and `G5` kept — and costs *less* than what ships in air, where the budget is actually spent. **Built and measured 2026-08-23, and it is not the price that stops it.** At ×4 a coolant sink stops out-performing the best surface dial, bolting a radiator starts working, a 300 MW reactor buried in armour settles inside its rating, and the stiffest block on the census hull stops responding to air. That is `G3`'s lever and this page's *there must be a lever* going away to buy `G8`'s timing, and the sweep that chose ×4 never scored `G3`. **What has not been tried is a dial that is not transport** — waste heat and the critical temperatures move the crossing without touching the conduction pace at all. [backlog.md](backlog.md) `C12`. |
| **`G2` "the criterion the current build most likely fails"** against the corpus, where G2 passes at 65.5%. | **The corpus is correct; the prediction was written before it.** The prediction stands in [balance-lab.md](balance-lab.md) as a criterion's original wording, which is right — a criterion is not edited once the data arrives (`E11`). Read the status from [balance.md](balance.md), not from the prediction. |
| **"The radiator is a block you plumb"** against the retrofit measurement, where a plumbed ring fits on **15%** of warm hulls and bolting — which fits nearly everywhere — makes more ships worse than better. | **Resolved: cooling is designed in.** A hull laid out with heat in mind is the ship the mod is for, so a 15% retrofit rate is not the failure it looks like — it is the measurement of how many finished hulls happen to have room. What the answer does *not* license is a balance that breaks unmodified ships, which is the floor stated in [Cooling is designed in](#cooling-is-designed-in--and-a-vanilla-ship-still-has-to-survive). `G3` should be scored against designed-in cooling, not against retrofits. |
| **"Light: a grid's cost is one pass over its links per substep"** against the measured per-node cost of the environment pass. | **The measurement wins and the goal is unchanged.** The step budget already counts `links + 4 × nodes`. The README's wording predates the measurement and understates what a substep does. |
| **`MaxSubstepsPerBlock 6` recommended in the field** against the shipped default of `0` (off). | **The shipped default stands, and the argument that set it has inverted twice.** It was made a switch because the cap cost 0.607 K on the worst-placed block — twenty times what the substep ceiling's breach costs, and on the other side of what a player can see. Re-measured at `C24`'s pair on the hull `C26` refreshed, the same cap costs **0.028 K**, which is the same size as the breach the mod accepts: the argument that separated the two mechanisms is gone. And `F11` gave the cap a second reason to exist that it did not have — `G6`'s cost half fails in air, and the cap is the only lever that lowers a step's *work*. **Being measured on the population now**, paired arm against arm on one clock, with the predictions and the decision rule fixed first ([backlog](backlog.md) `C3`, [balance-lab.md](balance-lab.md#what-a-per-block-cap-does-to-the-population-written-before-it-is-measured)). Until that lands the default is unchanged, because a default moves in its own commit with its own evidence. |
| **Natural feedback against the Light goal.** The README says *"every readout, diagnostic and overlay is off unless something is reading it"*. [Natural feedback](#natural-feedback--built) is the first presentation feature meant to be **on** by default — a player who has not opened anything is exactly who it is for. | **Both stand, and the resolution is a definition rather than a compromise.** Feedback that only runs when a block is near its limit *is* "something reading it" — the reader is the block's own state, not a player with a panel open. What it must not do is cost anything on a ship where nothing is hot, which makes the trigger a threshold test on a ratio the solver already computes, and `C7` still applies: it needs its own switch like every other mechanism. **Built, and the cost is one comparison per grid** — the hottest block against the lower of the glow's floor and the coolest block's watch point — with `HeatGlow` and `HeatWarningSound` as the two switches. |
| ~~**The README's stated audience against what it currently carries.**~~ | **Resolved 2026-08-22, and this row was left standing for two days after the change it asked for was made.** The layout tree and the build section are under [development.md](development.md), the index is [docs/README.md](README.md), and the README carries one link to each. Kept as a row rather than deleted because the *shape* of the mistake is worth seeing: a conflicts table is a list of live disagreements, and a resolved row that reads as live is the same defect as a stale figure. |
| **The census hull as "a worst case" against "what a ship does".** It makes 12.1 kW a block against a real median of 335 W — the 96th percentile. | **Resolved 2026-08-24: it is both, by property, and the conflict was in the quoting.** The hull is *typical* in stiffness and *extreme* in heat, and each is what its own kind of figure needs — `C26` put it in the population's trough because a cost figure has to describe what a server pays, and it stays a 96th-percentile heat maker because a temperature figure has to be a ceiling. So a temperature taken on it is an **upper bound** and a cost figure taken on it describes the population; every approximation this mod has accepted on such a temperature is safer under that reading, not shakier. `TheCensusHullMakesFarMoreHeatThanARealShip` pins the heat half and `TheCensusHullIsInsideThePopulationItStandsIn` the stiffness half, so the pair fails if either moves quietly. [backlog.md](backlog.md) `C14`. |

---

## Where there is no intent at all

**As of 2026-08-25 there is nowhere, and the list below is kept as the record of the eight that were
here.**

**An undeclared intent is decided by whoever touches the file next**, which is the reason this page
exists, so the subjects nobody has stated a position on belong on it as plainly as the ones that are
settled. Eight were found, by reading the tree for what it *does* and asking what said why: each
named something the mod already shipped or already refused, with no statement anywhere about whether
that was right.

**All eight were answered on 2026-08-25, and six of them by measuring rather than by choosing.**
That is the finding worth keeping: a void is usually a question nobody had put a number to, and the
number decided it. Pricing a hand tool said there is no hand-sized version of it. Placing the mod's
block prices inside the game's own distribution said the lever costs what the game charges. Reading
the glow's colour channel in ΔE said it distinguishes blocks and not moments. Counting the strings
said the translatable surface is forty-seven of about eleven hundred. Only two — what heat does when
the game's rules are suspended, and what happens beside a second heat mod — were settled by argument
alone, and both were settled by following where the *game* draws a line rather than by preference.

**Their numbers are not reused**, because other pages cite them by number:

| | Void | Where the position lives now |
| --- | --- | --- |
| 1 | What a player does with their hands | [Acting on heat by hand](#acting-on-heat-by-hand--damage-mitigation-and-priced) |
| 2 | What the mod's blocks cost to build | [What a block costs to build](#what-a-block-costs-to-build--what-the-game-charges-and-nothing-invented) |
| 3 | Creative mode, and the tools that skip the game | [When the game's own rules are suspended](#when-the-games-own-rules-are-suspended) |
| 4 | What a version boundary would be for | [What the version number governs](#what-the-version-number-governs-and-what-moves-it) |
| 5 | The visual channel, and who can read it | [Who the glow is for](#who-the-glow-is-for--brightness-and-colour-as-a-refinement) |
| 6 | What language the mod speaks | [What language the mod speaks](#what-language-the-mod-speaks) |
| 7 | Heat that leaves the world | [known-issues.md](known-issues.md#deliberate-limits), as a deliberate limit |
| 8 | Another mod that also simulates heat | [To another mod that also simulates heat](#to-another-mod-that-also-simulates-heat-nothing-and-the-switches-are-the-answer) |

**This section is not retired.** It is the shape of question this page is worst at noticing, and an
empty list is a claim that wants re-testing rather than a job finished: the way all eight were found
was to read the tree for what it does and ask what says why, and that reading is worth repeating
whenever a mechanism is added.

---

## Open questions with a stated intent

The difference from the section above is that these have a position and lack a route. Each is
tracked in [backlog.md](backlog.md); what is here is why the answer is not obvious.

### 1. How a thermal camera could be built

The intent is stated and the route is not known. Mods get no shader, no post-process and no frame
buffer; the candidates are per-block emissive (the one path the engine definitely exposes),
transparent materials, particle effects as a rendering surface, and whether any material parameter
is reachable at all. **This is a research task before it is a design task.**
[backlog](backlog.md) `B24`.

### 2. The warning cue itself

The shape is decided and built, and the projection is no longer the problem it was: a block heading
somewhere below its rating is not warned about, because the forecast reads the equilibrium off the
rate rather than extrapolating a straight line. **What is still open is the sound**, which is
currently two of the game's own destruction cues standing in for a lead chirp and an event chirp,
and which wants hearing in a cockpit rather than deciding on paper. **Three seconds is a defensible
lead now that the window is measured**: the median ship loses its first block 37 s after the load
and 24 s after the crossing, so the cue is early rather than late. [backlog](backlog.md) `F15`.

### 3. How far a client may drift

**Two of the three unknowns are answered and the one this item is about is not.** *What measures it*
is `-- inputs`, which degrades every input a client drives its own simulation from and reports the
disagreement it settles at; *what a re-sync costs* is measured — the whole hull once at the join is
one 94 KB packet, and the near-critical band after it is 513 B/s. The input surface is **eighteen
inputs covered and three open**, the three being a ship's position, the weather and the ten wind
fields ([known-issues.md](known-issues.md#the-sweep-is-not-the-whole-input-surface-and-here-is-what-is-missing)).

What is still undecided is *where the point is*: the sweep says the three worst inputs are the three
that are not errors in a number — a hull the client has not been told about (380.5 K), a room map
that never lands (290.95 K), a switch on the wrong side (245.87 K) — and nothing says which of those
is a drift a player should be allowed to see. See
[What the mod owes a multiplayer client](#what-the-mod-owes-a-multiplayer-client).

### 4. What "as much of the CPU as possible" means concretely

The direction is unambiguous and the target is not: how many threads, and whether the mod may
saturate a machine a server is sharing with other mods.

**The *where* is answered.** Across grids before within one: fanning a 242-grid fleet out one grid
per work item costs 1.6–6.8 µs of hand-off against a 1,004-node grid's own 0.54 ms, which is
**10.17×** on 32 threads and 7.09× on eight, and it is bit-identical to stepping in order. A single
grid through the same fan-out is 0.99×, so the hand-off is neither free nor a barrier. What bounds
it is the largest ship — an uneven fleet gives 3.35× — which is the argument for splitting one grid
as a *second* change rather than as a substitute. [backlog](backlog.md) `D19`.

### 5. Two that were on this list and are settled

* **Whether the compatibility floor becomes a scored criterion.** It is `G7`, written down before
  the prefabs were first run, and it holds: 705 prefabs, 461,428 blocks, not one crossing critical,
  against the same 705 flown hard losing 616.
* **Where the visual ramp starts.** A hundred kelvin below the block's own critical temperature, and
  full at it — a fixed band rather than a share of the rating, so the same distance from failure
  looks the same on a decorative block and on a large thruster.


## Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Scoped hand-cooling figures to physical reference inventory and withdrew the untested claim that those figures establish the implemented tool balance. |
| 2026-09-27 | Refreshed the offline suite count to 2,603 cases after adding aerodynamic population controls. |
| 2026-09-27 | Updated the current aerodynamic composition and implementation status for the rebuilt engine adapter; preserved the superseded design as historical context. |
| 2026-09-24 | *Not authoritative over the game's own systems* says the mod writes nothing to a ship's physics again: `K25`'s first step stripped the aerodynamic force, and the owner's decisions recorded the same day (aerodynamics.md) are what brings it back. The reasoning around it is unchanged. |
| 2026-09-20 | Describe soft heat patches, bounded lights and explicit visual verification limits. |
| 2026-08-28 | *Basic environmental information should be on screen as a matter of course* is built, and the page says which half of it is judged: what the readout **says** is a decision in `Core` and pinned by tests; whether it reads as the game's own HUD is the half only a session can settle. |
| 2026-08-25 | Gave *realism wins where the difference is visible* its second worked example, which is the reactor's taken the other way. The oxygen generator's 0.6 was an invention with a sourced 0.20-0.40 sitting beside it, and what moved it was not the provenance: at 0.6 two of the game's six generators are past their own critical temperature alone in open space at their rated draw. The reactor kept an invention because the real number destroyed it; the generator lost one because the invention destroyed it. The prior reads better with both, because one case alone reads as *inventions are fine*.

| 2026-08-25 | **Measured the base claim and kept it, with its emphasis corrected.** A station really is harder to cool than a ship of the same block count — 284.4 K above ambient against 236.1 — and *what a base gets in exchange* is room to run **transport** rather than room for surface. On a planet its penalty is three times what its halved area explains, because the binding resistance is the path from its middle to its skin: 39.9 K of interior-to-skin drop where a ship has none. Radiator area helps and cannot close it, at 43 % of the station's blocks. So this page's pairing of *elaborate coolant systems and radiator farms* keeps the first and demotes the second, on evidence rather than on preference. |
| 2026-08-25 | **Corrected the hand-tool position the same day it was written, and the measurement is what changed it.** The extinguisher fires expendable ammunition that cools a block as *damage mitigation*; the earlier closure priced *cooling a ship*, which is a different question and impossible. Damage tracks the overshoot, so half a bottle pulls the median block 10 K back from its rating where 26 bottles would be needed to return it to ambient — and 90 bottles do nothing for the worst block, which is what keeps it an emergency tool. Also: a base's advantage is **layout freedom rather than size**, and **nothing is refused yet by sequencing** — soft limits wait until testing has found the hard boundaries. |
| 2026-08-25 | **A base is a thermal problem, and the place cooling can be done properly** — it heats and has to be managed, and what it gets in exchange is room, so elaborate loops and radiator farms work far better there than on a ship. None of it is measured: every scenario is a ship or a rig, the corpus is blueprints, and room air is the part a base leans on hardest with the least evidence under it (`F27`). **And the compatibility promises bind from release, which has not happened** — before then a behaviour change is checked against the corpus rather than against a promise, and the identifiers are reserved rather than depended upon, so a rename that includes them is available now and will not be later. |
| 2026-08-25 | **Three more decisions.** The readouts exist to *show*, not to diagnose — a player who sees a glow and hears a warning has enough, and working out the fix is the Engineering in Space Engineers. Two consequences: the mod has to announce it is there, which today it does not (`B41`), and *clear* is a higher bar than *present*. The workshop corpus is the population the mod is balanced against, its skew known and accepted. And the mod does not apologise for its cost: no guided troubleshooting, and the performance criterion is the author's own play. Also narrowed the open-API position — the mod is judged self-contained, so what an add-on does to balance is the player's business. |
| 2026-08-25 | **Three more intent decisions, and one retraction.** Single player is the design centre and multiplayer is a low priority that still has to work, with the server stress case written as a number rather than a mood. The two-to-five-minute window keeps a floor under it: **nothing may be destroyed in the first minute of a world**. The API is open and unbounded — any mod may register anything, because a player chooses their mod list — which converts the mod's duty from *bounding* add-ons to *attributing* them, and means every published balance figure is a claim about this mod alone. Retracted: the claim that heat could be switched off entirely, because it would have unpicked the shared surface walk that is the reason force belongs in this mod at all. |
| 2026-08-25 | Recorded the answer to a neighbouring mod that is better than detecting it: an interface. [backlog.md](backlog.md) `K17` has one mod own the force on a constraint group while others contribute to its inputs, which is what a control-surface or jet-engine mod needs anyway — and a stronger coupling than the heat API has asked for, because a registered surface changes how a ship flies rather than what temperature it reaches. |
| 2026-08-25 | Named the one neighbouring mod that *can* be detected, which is the exception that makes the rule above readable: [RelativeTopSpeed](https://github.com/Gauge/RelativeTopSpeed) publishes an API and speaks the same network layer this repository vendors. Today it and this model do not collide — it applies a force and no heat, this applies heat and no force — and [backlog.md](backlog.md) `K1` would end that. |
| 2026-08-25 | Named the line the two new feature milestones cross. The mod reads a grid's velocity and writes nothing to its physics; [backlog.md](backlog.md) `K1` would apply the drag the friction term already computes the energy of, which is the first time it would move something the game moves. Recorded beside *not authoritative over the game's own systems* rather than left for whoever writes the code to decide. |
| 2026-08-25 | Gave the language table a source. `LocalisationSurfaceTests` counts the five surfaces and holds the claim the decision rests on — that the surface inside the game's HUD is a small fraction of what the mod writes — as a band rather than as a number, because the count is heuristic (`E5`). |
| 2026-08-25 | Recorded the one cooler *a cooling system costs power* does not reach and why ([backlog.md](backlog.md) `C16`): the suit regulates for free because `IMyCharacter` exposes `SuitEnergyLevel` to read and nothing to write. An engine limit rather than an inconsistency, and now stated beside the goal it qualifies. |
| 2026-08-25 | Made the position on a second heat mod deliverable rather than nearly: `HeatTerminalPanel` is the switch the terminal panel never had, so a world running two can turn every output of this one off and keep the simulation and the API. |
| 2026-08-25 | **Took a position on a second heat mod in the same world, which was the eighth and last void — and with it this page has none.** The mod behaves as though it is alone because it cannot tell that it is not, and guessing would be worse: everything it owns outright cannot collide, and the four things a block has only one of can. The answer is the switches — a world running two turns this one's consequences off and keeps its simulation and its API — so two mods both applying consequences is unsupported and said so. One switch is missing for that to be complete, which is `B40`. |
| 2026-08-25 | **Wrote the seventh void down where it belongs, which was the whole of what it asked for.** Heat leaves the world with a block that leaves it and arrives at ambient with one that is built; energy conservation is an invariant about a step and says nothing across a change in the population. It is a deliberate limit — the alternative is a grinder that heats the ship around it — and it now sits in [known-issues.md](known-issues.md#deliberate-limits) with a test pinning both halves. |
| 2026-08-25 | **Said what the version number governs and what moves it, which was the fourth void.** `ThermalApi.Version` governs the delegate table and nothing else, and the major moves when a caller written against the previous major could still bind and then be wrong — which decides a removed key, a reshaped signature and a changed meaning alike, and leaves an added key alone. Two of the three are now checked against a recorded surface; the third is written where the rule lives. The other three promises sit under no number because they are *never* rather than *not within a major*. |
| 2026-08-25 | **Took a position on what heat does when the game's own rules are suspended, which was the third void.** Creative suspends scarcity and heat is physics: the game keeps damaging, accelerating and colliding blocks in a creative world and only stops charging for them, so a hull that would cook in survival cooks in creative because it is the same hull. The lever is a setting rather than a mode. `SuspendedRulesTests` pins that nothing in `Data/Scripts` reads the game mode, the creative flags or the creative tools. |
| 2026-08-25 | **Took a position on what language the mod speaks, which was the sixth void.** The boundary follows from a commitment already on this page rather than from a preference: the readouts are drawn through Rich HUD so that nothing mod-shaped announces itself, and the game's own HUD is localised. Counted, the surface inside the game's HUD is **47 distinct strings** — four per cent of what the mod writes — against 248 in the settings menu, 90 in chat replies, 113 in debug overlays and 604 in telemetry files, all of which are the mod talking about itself and stay English. The work is blocked on one thing only a session can answer, which is `B39`. |
| 2026-08-25 | **Took a position on who the glow is for, which was the fifth void, and the measurement made it an easier question than it looked.** The colour channel moves less than a just-noticeable difference across their own glow band for **88 of the 101** block types in the installed catalogue — not only the 28 pinned under the Draper point — and the best any block manages is ΔE 4.76. So brightness is the channel and colour is a refinement, the warning is legible without colour, the sound cue is a redundant channel on purpose from here, and no fact may be carried by hue alone. |
| 2026-08-25 | **Took a position on what a block costs to build, which was the second void.** There is no vanilla comparator for a block that moves heat, so the eighteen `Cubes.xml` definitions are placed inside the distribution the game prices its own 1,434 blocks over — kilograms a cubic metre, seconds of welding a kilogram, PCU a block — and all eighteen sit inside it. The position: the lever costs what the game charges for a block of that size and mass, and the mod does not invent an economy. The recipe is also not secretly a thermal dial: at four times a radiator stack's mass the source settles within a hundredth of a kelvin and only the transient moves, 24 to 112 s. |
| 2026-08-25 | **Took a position on what a player does with their hands, which was the first of the voids and had no number under it.** `HandCoolingLab` prices the tool against a real five-kilogram CO2 bottle: undoing one crossing is 26 bottles at the median of the 72 vanilla types that cook themselves and 20 discharging at once to beat the block's own 32 s window, which is 3.3 MW — a large radiator with a trigger. And 71 of the 72 shed everything they make in their own best case, so the block is not where the problem is. The position: a player perceives heat and does not act on it by hand, the extinguisher is the instrument that names the block to change, and what would reopen it is a block-scale intervention rather than a bigger bottle. |
| 2026-08-24 | **A full sweep of the tree for intent, and it found four subjects the code had always followed and no page had ever stated.** Added [what the mod promises the things around it](#what-the-mod-promises-the-things-around-it) — to an existing world, to a block it has never heard of, to another mod, and to a client — which gathers the save format's forward compatibility, the retired definition names, a setting's name as an address, the derive-don't-guess rule for third-party blocks, the API's four guarantees, and the two trust boundaries that decide which network channel a message takes. Added [what the mod does when it cannot afford itself](#what-the-mod-does-when-it-cannot-afford-itself): it slows down rather than stuttering, determinism is chosen rather than assumed, and a fault records itself whether or not anyone asked. Added [where something is modelled, it is modelled as a mechanism rather than as a threshold](#where-something-is-modelled-it-is-modelled-as-a-mechanism-rather-than-as-a-threshold), which is the most consistent habit in the code and had been written down nowhere. |
| 2026-08-24 | **Checked every claim in the new voids section against the code rather than the documentation, and two of them changed.** The version void was overstated and is corrected in the row below. The visual-channel void was *understated*: `Incandescence`'s colour table starts at 800 K with a pure-red first entry and clamps below it, and the 26 % of block types rated under the Draper point at 798 K are exactly the blocks whose colour therefore never moves through their whole glow band. So the two-channel design is a one-channel design on a quarter of the game **for every player**, by construction — which the code says where `DraperKelvin` is defined and no page had carried. The rest verified as written: no code path in the mod knows what creative mode is, `RemoveBlock` drops a node with no redistribution of its energy, the extinguisher's ammo does zero damage, zero trajectory and zero impulse, and the settings menu's labels and descriptions are English string literals. |
| 2026-08-24 | **Corrected the version void, which I had overstated by reading the documentation instead of the code.** It said nothing in the repository defines a version. `ThermalApi.Version` is `1`, served as `ApiVersion`, and [api.md](api.md#binding) already tells a caller to read it and refuse a major it was not written against — so the API's guarantee has a referent. What is actually missing is smaller and sharper: what would move that number, whether the save format, the retired definition names and the setting names sit under it or under nothing, and a check relating a change in the delegate table's shape to the number a caller is told to trust. |
| 2026-08-24 | **Rewrote *what is still undecided* as two sections, because it was conflating two different things.** [Where there is no intent at all](#where-there-is-no-intent-at-all) is eight subjects the mod already ships or already refuses with no statement anywhere about whether that is right — what a player does with their hands, what the blocks cost to build, creative mode, where a version boundary is, the visual channel and a player who cannot use it, what language the mod speaks, heat that leaves the world, and another mod that also simulates heat. [Open questions with a stated intent](#open-questions-with-a-stated-intent) is the four that have a position and lack a route. Two entries that were settled are marked as such rather than left reading as open. |
| 2026-08-24 | **Three conflicts added and two rows corrected.** The element-visit allowance against *fidelity is the default* — the shipped default is not the faithful end, and `TheDefaultIsFrameBounded`'s stated reason, *the budget costs no accuracy*, is true about a step and silent about the consequence, which `C27` later priced at up to 36.98 K; what the code follows is that frame stability outranks fidelity and the price is paid down rather than denied. `G6`'s cost half against the unit it was scored in. And the corpus as the population against the corpus as what people publish. The README-audience row was resolved two days earlier and left standing, which is the same defect as a stale figure; the `MaxSubstepsPerBlock` row carries an argument that has now inverted twice. `G6`'s status is rewritten and `G1` gains its air reading; six criteria are stated as the eight they became. |
| 2026-08-24 | **The fleet half of *use the machine* is built, and ships off.** A grid's tick splits into prepare on the game thread, solve anywhere, publish on the game thread, and a frame's solves can be fanned out through the engine's own workers — 10.17× on a 242-grid fleet, 0.99× on one grid. It ships `false` because what is left is not a measurement but three questions only a session answers, and this page's own rule is that an approximation nobody asked for does not go in front of a player: a mod taking threads on a shared machine is that shape ([backlog.md](backlog.md) `D19`). |
| 2026-08-24 | **The largest approximation the mod shipped turned out to be the one nobody had priced, and the default moved.** `MaxElementVisitsPerStep` shortens a step rather than coarsening it, so a grid that reaches it is not less accurate — its thermal clock runs slow. Measured, that is worth 1.19 K standing at a 5 % deficit and 36.98 K at 60 % under a moving load, against the 0.028 K this world accepts for the substep ceiling and the 0.607 K that keeps `MaxSubstepsPerBlock` out of the defaults. *Fidelity is the default and a saving is a switch* is what decided it: the allowance is 4,000,000, from 2,000,000 ([backlog.md](backlog.md) `C27`). |
| 2026-08-24 | **Recorded that the fleet half of *use the machine* is built.** This page still said nothing in the mod was threaded, three commits after `D19` built the three-phase tick and the scheduler that fans a frame's solves out. |
| 2026-08-24 | **The step-work figures above are withdrawn: `G6`'s cost half was scored with the joint count in place of the link count.** The unit is `links + 4 × nodes` and the corpus carried no link column, so the expression evaluated to `4 × nodes` — 1.51× low on a census hull. No verdict moves; the vacuum figure stays inside the allowance and the air one was already outside it. The walk records `ThermalSimulation.SubstepCost` now, and a dataset without it reports the cost half as unmeasured ([backlog.md](backlog.md)). |
| 2026-08-24 | **`G6`'s cost half is scored for the first time and holds.** The criterion has always read *substep demand and step cost*, and the corpus carries no timing column on purpose, so the cost is stated as work — a step's element visits against what `MaxElementVisitsPerStep` grants, which is the point past which a grid's simulated time runs slower than real time. p99 is 881,279 over 40,656 runs, and against the 4,000,000 that ships since `C27` the 8 ships past it are 159,000 blocks and up ([backlog.md](backlog.md) `C23`). The figure this row first carried, 446,707, was scored in the unit a step is *paced* in rather than the one the allowance is spent in — corrected 2026-08-24. |
| 2026-08-24 | **The significance window is reached, and the clock a world runs is 90.** `C24` applied the retune `C12` chose: `ConductionScale` 2.4 → 9.6 and `HeatTimeScale` 225 → 90. `G8` holds on 76 % of resampled fleets against 1 %, and `G6` — the one approximation the defaults shipped on — passes, because the demand it is scored on is convection-limited and came down with the clock. `G7` was re-scored before the defaults were committed and holds at 0 of 705 prefabs.** What it cost is four of this page's own statements**, each re-measured rather than dropped: a coolant sink no longer beats every surface dial, bolting a radiator buys 2.9 % of a reactor rather than 0.5 %, the cooling ladder's control saves 2.42 K where it used to lose, and a 300 MW reactor buried in armour survives — burying it costs 50.7 K where it cost 355 K. *There must be a lever* still holds: plumbing a panel is worth 73.5 K over bolting it, because a joint carries heat one block and a loop carries it wherever the ring goes. |
| 2026-08-24 | **Stated that a switch is a ladder**: a feature is configured as a list of options from `off` to `realistic`, `realistic` is the default, and every rung between carries the price of what it gives up. It is `C7` grown a dimension — removing a feature's cost should not be the only way to afford it — and it is `C15` in [rules.md](rules.md). Corrected three things this page said that its own evidence had overtaken: `C12`'s resolution, which read the load route as the better trade before either route was measured for how often the criterion holds; *whether the compatibility floor becomes a scored criterion*, which is `G7` and holds; and *what across-grid parallelism is worth*, which is measured at 10.17× rather than pointing two ways. |
| 2026-08-23 | **The waste dial's provenance is settled, and it argues against the dial.** Every waste fraction in `Cubes.xml` now states where it came from, and weighted by the heat each carries over the 8,142-ship census, **76.3 %** of a loaded fleet's waste comes through a fraction derived from the game's own stated efficiency against 15.3 % through an invention. Halving waste is therefore an admitted balance knob on a sourced number, not a correction of a guess. [backlog.md](backlog.md) `C21`. |
| 2026-08-23 | **The significance window is reachable and reaching it that way costs *there must be a lever*.** The retune was built and run against the suite: at conductivity ×4 the coolant sink, the bolt joint's disadvantage, the buried reactor and the atmosphere all stop mattering the way this page says they must. So the conflict between `G8` and the shipped clock is now a conflict between `G8` and `G3`, and it is recorded here as one rather than resolved on the developer's behalf. |
| 2026-08-23 | **`G6` moved from *holds* to *fails in air*, and the significance window's price with it.** The criterion had only ever been scored in vacuum, where corpus p99 demand is 6.02 against the 64 the caps grant; at 200 m/s in thick air the panel's p99 is 73.4, fourteen of forty-nine hulls are over the cap, and at 300 m/s the median ship is. The atmospheric figure this page carried, 36.7 at p95, was taken at `Frequency` 8 and is exactly half. Read where the budget is actually spent, all four cells that satisfy `G8` cost *less* than what ships, so the retune resolves the goal conflict rather than trading against it. |
| 2026-08-23 | The significance window is no longer a goal the code cannot reach. Measured by the paired sweep: conductivity ×4 with `HeatTimeScale` 80–120 lands the median crossing at 124–186 s and still returns the hull inside the hour, for 1.36–2.04× the shipped substep demand. The disagreement stays on the page because the mod does not ship it — what changed is that it is now a price rather than an impossibility. |
| 2026-08-23 | Resolved the `G5` conflict by measuring the event it is about. The crossing is where the damage rate leaves *zero*; the first block is lost at a median **37.0 s** under full electrical load, a median **24.2 s** after the crossing, so with the cue's lead the median pilot has 40 s. `G5` and its rationale both hold. |
| 2026-08-23 | The glow is drawn rather than written to an emissive material, after a session found no glowing blocks. Measured: 418 of 1,992 base cube models hold an emissive material and four of 479 armour models do, and where it exists it is a status lamp. Armour now glows, which changes what the audio cue is for. |
| 2026-08-23 | Settled where the glow's brightness comes from, superseding the three entries below it. It is the **last hundred kelvin before a block's own critical temperature**: nothing below that, full at it and above, a straight line between. A fixed band rather than a share, so the same distance from failure looks the same on every block, and narrow enough that a glow means a block is about to go rather than that it is warm. The colour is unchanged and stays absolute, so brightness says how close to failing and colour says how hot. |
| 2026-08-23 | Reverted the glow to incandescence: both brightness and colour are functions of temperature alone, and the rating-keyed ramp of the entry below is withdrawn. The reason is that it says what the game already says — the engine puts damage effects on a damaged block, and a player works out how close a block is to its limit from playing — while costing the property the channel exists for, that the same temperature looks the same on every block. The 26 % measurement stands as the stated limit rather than as an argument: a quarter of block types fail before they glow, which is what the audio cue is for. |
| 2026-08-23 | The glow is keyed to each block's own rating after all, and the entry below it is superseded. Nothing glows at comfortable temperatures; brightness is the share of the way to critical, cubed, and is full at critical and above. The measurement that argued for the absolute form is unchanged and is now the argument *for* this one: 26 % of block types are rated below the Draper point, so a physical glow would leave a quarter of the game going from dark to destroyed with nothing in between. The colour stays absolute and stays physical, so brightness reads as danger and colour reads as temperature. |
| 2026-08-23 | Natural feedback is built ([backlog.md](backlog.md) `B25`), and building it withdrew a statement on this page. The visual ramp is **not** keyed to each block's critical temperature: it is incandescence, absolute, from the Draper point at 798 K up the Planckian locus, because block ratings run from 500 K to 1,522 K and a relative ramp shows the same colour at 400 K and at 1,200 K. The consequence is stated rather than hidden — 26 % of block types fail before they glow — and it is the reason the audio cue is keyed to the rating instead. The three-second lead now reads a block's equilibrium off its own rate, so a block levelling off short of its rating is never warned about; undecided items 2 and 6 move with it. |
| 2026-08-22 | A coolant loop costs power and makes heat doing it ([backlog.md](backlog.md) `C13`). The rating is derived from the loop the model already describes — 200 kg/s against two bar of head — and the asymmetry with the heat pump is now a stated design: a circulator costs well under a per cent of what it moves against a heat pump's third. |
| 2026-08-22 | The compatibility floor is measured. All 705 prefabs the game ships, idle, in the environment each category spawns into: 461,428 blocks and not one crossing critical. The same 705 flown hard lose 616, which is what makes the first number a measurement rather than a formality ([backlog.md](backlog.md) `C10`, criterion `G7`). |
| 2026-08-22 | Said that *consequential* includes the player. Heat that only damages blocks stops at the airlock, and a burning compartment somebody can stand in is the mod contradicting its own purpose ([backlog.md](backlog.md) `B10`, closed). |
| 2026-08-22 | Stated that the comment standard here is `R14`, and that the check on it catches the failure the length limit prevents. Twenty-four comments in the tree described a member that was no longer there. |
| 2026-08-22 | Recorded what a code comment is for: it names a definition or an especially complex chunk of code, stands on its own without context from another file, and stays inside two lines — anything longer is a documentation entry in the wrong file. |
| 2026-08-22 | Recorded what the README is for: a workshop-pasteable front page for players, with one section for modders building on the framework, and deliberately not an explanation of how the mod works inside. Recorded the conflict this creates with the three developer-facing sections the page carries today. |
| 2026-08-22 | Renamed from `document_of_intent.md` to match the kebab-case every other page uses. **Answered all eight areas that had no stated intent**, which are now stated in the body: cooling is designed in but a vanilla ship still has to survive; the scale target is 10⁶ blocks at `SimulationSpeed` 1.0 with ~250,000 as the realistic figure and uncapped stress bounds; a coolant loop costs power and makes waste heat, because a pump is a motor; an unattended grid gets the same simulation as an attended one, with the saving available as a switch; the mod is balanced for vanilla play in the knowledge that worlds get modded; the warning is a cockpit-only lead cue with a linear visual ramp keyed to each block's threshold; every thermal property is a dial; and a client is owed the minimum traffic that keeps the world coherent, with drift tolerated. Added the principle those answers share — **fidelity is the default and a saving is a switch** — which is what P14 leaves undecided once a difference *is* perceptible. The voids section is replaced by the six smaller questions the answers left open. |
| 2026-08-22 | Recorded four intents stated by the developer after this page was first written. **A thermal camera is wanted** — it had been recorded here and in [known-issues.md](known-issues.md) as a deliberate limit on the grounds that mods get no shader, which is a statement about difficulty rather than about intent. Added [How a player perceives heat](#how-a-player-perceives-heat), covering the three layers of it: instruments that belong to the game's HUD rather than sit on top of it, natural feedback through subtle audio and in-world visuals, and thermal vision as an open problem with no known route. Added the threading target to [Performance intent](#performance-intent): use as much of the CPU as possible while taking as little of the game thread as possible. Void 6 is rewritten — the form of a warning is now stated, and its timing is what remains undefined. |
| 2026-08-22 | Created, by gathering the statements of intent scattered across the README's design goals, `rules.md`'s judgement rules, the balance criteria, the deliberate limits, the profile ladder and the developer's standing instructions, and reconciling each against what the code does. Seven conflicts are resolved against the implementation; eight areas are recorded as having no stated intent at all. Two figures were corrected on the way: the median time to critical at idle is 104.5 s rather than the 112 s the register's prose carried, and the damage-timing finding it belonged to — dropped in an earlier merge — is restored to [balance.md](balance.md#how-fast-a-ship-crosses-critical). |
