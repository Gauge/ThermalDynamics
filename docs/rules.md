# The rules

The standing rules this repository is bound by, in one place, reduced to the principles they
follow from.

They were not invented here. Every one was already written down somewhere — in
[development.md](development.md), in [balance-lab.md](balance-lab.md), in a test class's own
summary, in the header of a tools page, or in a note kept outside the tree entirely — and that
was the problem. A rule stated in the one document its author was writing at the time is a rule
the next reader finds by accident. This page is the canonical statement of each; where another
page argues one at length, this page states it and points there.

## The one observation

Every rule here converts a *silent* failure into a loud one.

That is not a stylistic claim, it is what the evidence behind these rules has in common. A drifted
copy does not throw. A check that judged nothing reports success. A partial sweep prints a clean
number. Two runs stopped on different criteria produce two comparable-looking figures. A renamed
benchmark case arrives as two healthy rows. A parallel run that shares a mutable block instance
finishes without complaint. A block with no exposed face climbs to 342,510 K and looks like
physics. A dead link reads as though somebody checked.

So the test for whether something belongs on this page is not *is it good practice* — it is
**would breaking it be noticed**. If a violation announces itself, the compiler or the suite is
already the rule, and it does not need a sentence here.

## How to read a rule

Each entry is one sentence a change can be measured against, the failure it exists to prevent,
and four fields:

* **Applies to** — the scope. A rule with no scope is a rule that will one day forbid something
  useful. Most errors found while assembling this page were rules stated more broadly than the
  evidence behind them. **A scope does not make a rule conditional**: nearly every rule here names
  a subject, and what puts one in the [conditional](#conditional) pile is that inside its subject
  there are cases where the right thing to do is the other thing.
* **Checked by** — the test, script or build setting that fails when the rule is broken. A dash
  means nothing checks it; that is not a defect in every case, but an unchecked rule is a hope,
  and marking it says so. *Reported by* is weaker than *checked by* and is written as such: a
  script that prints a verdict and exits zero has told a human something, not enforced anything.
* **Retires when** — the condition under which the rule stops being worth following. Present only
  where such a condition exists. A rule that works around a defect names the fix that would
  delete it.
* **From** — where it is argued in full.

Rules are cited by identifier (`E3`, `M6`) in commit messages and in review. The letter is the
subject — **E**vidence, **M**ethod, **D**efect, **C**ode, **R**epository, **O**perations,
**J**udgement, **W**orld-outside — and is historical: the classification below is orthogonal to it.

> **The letters collide with [backlog.md](backlog.md)'s, and fifteen identifiers are currently both
> a rule and an open item.** `C3` is *target `net48`, and never reference the native assembly* on
> this page and *whether to ship `MaxSubstepsPerBlock 6`* on that one; `E2`, `E4`, `C7`, `C8` and
> `D1`–`D6` are the same. A reader resolves it from context and a check cannot. Nothing is renamed
> here, because the backlog's numbers are its rows' names and never move, and remapping this page's
> three colliding letters would touch 362 citations in code alone — that is a decision, and it is
> [backlog.md](backlog.md) `H8` rather than something taken in passing. **What is fixed is that it
> stops growing**: `W` was chosen for the four rules added on 2026-08-25 precisely because it
> collides with nothing, and any letter issued here from now on is checked against both pages
> first.

## The three categories

| Category | Test for membership |
| --- | --- |
| **Absolute** | It never stands down. Inside the subject it names there is no case where the right thing to do is the other thing, so a change that breaks it is wrong without further argument. |
| **Conditional** | It stands down when a stated condition is false, and that condition genuinely varies. Written as an absolute, each of these forbids work that is ordinary and correct — the condition *is* the rule, and quoting the sentence without it is the failure mode. |
| **Low value** | It is not earning its keep: it costs more than it prevents, it restates rules that already exist, or it works around a defect that is cheaper to fix than to obey. Each names its disposition. |

**The first two are one question and the third is a different one**, which is worth saying because
the categories did not read that way until 2026-08-25. *Absolute* against *conditional* asks
whether the rule ever stands down. *Low value* asks whether it is worth having at all, and a rule
that failed that test would be dropped whichever of the first two it belonged to.

The first category was called **load-bearing** and its test was the consequence of breaking it —
*remove it and something breaks that cannot be seen breaking*. That is true of nearly every rule
here and it is the wrong axis to sort on: it made the boundary with *conditional*, which is about
scope, a comparison between two different things. Re-audited under the sharper test, **not one rule
changed category**, which is the useful result: the boundary was right and its stated reason was
not. The consequence is still what makes a rule worth writing down — it is the
[one observation](#the-one-observation) above — it is simply not what separates these two piles.

**A rule is not low value merely because nothing checks it.** Fourteen of the seventy-one rules
that are still rules are unchecked and say so, and three more are *reported* rather than checked,
which is weaker and is written as such. Restructuring `Models/` is caught by nothing and costs a
re-export of every block model; a promote-level check gated on a forgeable field is caught by
nothing and is a privilege escalation. The category is about what the rule buys, not about who
enforces it.

---

## The principles

Fifteen principles account for every rule on this page. They are the reduction: if the rule list
were lost, these are what would have to be re-derived, and each rule below is one of them applied
to a specific artefact.

**Fourteen held for eleven rules and then one arrived that none of them generated.** Every rule
added after the first reduction landed under a principle that already existed — six of them, which
was the only evidence available that fourteen was the right number — until a sweep of the tree for
unstated intent produced `W1` and `W2`: a save must load on the builds either side of the one
that wrote it, and a name something outside this repository addresses may never be repurposed.
Neither follows from *one definition and one consumer*, from *the repository is the publish*, or
from *do not edit what you cannot regenerate*. What they have in common is `P15`, and stating it
moved `R5` — the workshop identity files — out of `P12`, where it had been filed for the wrong
reason. See [testing the reduction](#testing-the-reduction).

| # | Principle | Rules |
| --- | --- | --- |
| **P1** | **A figure carries its scope.** A number without its population, its basis and its clock is not a number; a sample stands for a population only by a written rule — and a duration carries the machine it was taken on, and the build it was taken from. | `E2` `E3` `E6` `M10` `M11` `M13` `J3` |
| **P2** | **What the instrument could not see is part of the result.** Censoring, the noise floor, an unfinished sweep, a check that judged nothing, a discarded exception and a place nobody looked are the same failure: reading a blind spot as a value. | `E4` `E8` `E9` `M4` `M5` `M12` `D4` `D9` |
| **P3** | **The claim is fixed before the data and corrected in place after.** A criterion that can move once the numbers are in is not a criterion; a finding that is corrected somewhere other than where it was published is not corrected. | `E1` `E10` `E11` `M9` `D5` `R12` |
| **P4** | **Nothing is its own oracle.** A test that asks the model the same question twice agrees with whatever the model does; a harness fault looks exactly like physics. | `E7` `D1` `D7` `D8` |
| **P5** | **One definition, and at least one consumer.** Two definitions drift, and the drift is silent in both directions; zero consumers means the thing does not exist however well it is written and tested. | `E5` `D2` `D3` `M6` `R7` `R8` `R9` `R10` `R11` `R13` `R14` `R15` `R16` `R17` |
| **P6** | **A comparison holds everything but the subject equal.** Two numbers are comparable only when the stop criterion, the machine, the key and the baseline were the same. | `M1` `M2` `M3` `M7` `O4` |
| **P7** | **The game is the authority.** The local build and the suite are an approximation of a compiler and a whitelist neither of them can see, and where the game already answers a question — what is sealed, what is destroyed, who sent this — the mod reads that answer instead of forming its own. | `C1` `C2` `C3` `C11` `C9` `C10` `W3` |
| **P8** | **Off means off, and costs nothing.** A mechanism nobody is using must cost nothing, and the way to turn it off must be unambiguous. | `C4` `C7` `C8` `C15` |
| **P9** | **The core is a library the game happens to call.** That is what makes it testable in seconds, profilable, drivable by other mods and portable to another engine — and a library is also something that must not throw into a caller who never knew it was there. | `C5` `R9` `W4` |
| **P10** | **The solver's three invariants are the definition of correctness.** Order independence, energy conservation, boundedness — everything else is tuning. | `C6` |
| **P11** | **The mod folder is the publish.** Everything under `Thermodynamics/` that the packer does not ignore reaches the workshop, so what must not ship must not be there. | `R2` `R3` |
| **P12** | **Do not edit what this repository cannot regenerate.** Model binaries and vendored code have their source of truth outside this tree. | `R4` `R6` |
| **P13** | **A long run is designed for its own death.** It will be killed — by the OOM killer, a timeout, a mistake or a power cut — so cap it, resume it, and never let a timer guess its duration. | `O1` `O2` `O3` `O5` |
| **P14** | **Unobservable fidelity is cost.** Take the cheap form where the difference cannot be perceived, say what it gives up, and write the price down. Where it *can* be perceived, the cheap form is a rung on the feature's own ladder rather than the default. | `D6` `M8` `O4` `C15` |
| **P15** | **What is already in someone else's world is frozen.** A save, a setting name, a serialization number, a property name and a workshop id exist where this repository cannot reach them: add to them, never repurpose them. | `W1` `W2` `R5` |

### Testing the reduction

Two questions decide whether fifteen is the reduction rather than a number that happened. Is each
principle **necessary** — does it generate a rule none of the others generates? And is the set
**sufficient** — does every rule follow from one of them?

Necessity was tested by attempting every merger that looks available from the table.

* **P1 with P2.** Both say a figure is incomplete without its extent, and a merged principle would
  read *a measurement carries what it covered and what it could not see*. Rejected: the failure
  modes are opposites. P1 fails by mislabelling a scope somebody knew about — the wrong population,
  the wrong clock. P2 fails by reading a scope nobody looked at — a censored tail, a noise floor, a
  sweep that stopped, a check that judged nothing. Merged they make an eleven-rule principle whose
  second half reads as a qualification of the first, and the second half is the one that gets
  forgotten.
* **P7 with P11 and P12.** All three are about authority sitting outside this repository: the
  game's compiler, the workshop the repository publishes to, the model exporter and the upstream
  authors. Rejected: the shared idea generates no rule. What is actionable is the difference —
  *the game decides*, *everything here ships*, *do not touch what you cannot rebuild* — and a
  principle that has to be unpacked into three before it can be applied is a heading, not a
  principle.

One merger did succeed, and it came from the completeness audit rather than from the table. `C9`
and `C10` arrived needing *the game decides what is sealed* and *the server decides what is
destroyed*. Neither is about compiling, and both are the same idea P7 already held, so P7 widened
from **the game is the authority on what compiles** to **the game is the authority**. That is the
one piece of evidence available that the fourteen are the right fourteen: six rules were added to
this page after the reduction, from four sources that had never been read for rules, and every one
of them landed under a principle that already existed.

Sufficiency was tested by reading every markdown file in the tree for a sentence a change could
violate, and matching each against a rule. Most matched. Six did not:

| Added | Was stated only in |
| --- | --- |
| `C9` the game's answer is read, never overridden | [document-of-intent.md](document-of-intent.md#what-this-mod-deliberately-is-not) and `RoomPressure`'s summary |
| `C10` the server is authoritative over damage | [document-of-intent.md](document-of-intent.md#what-the-mod-owes-a-multiplayer-client) and one comment in `ThermalGridSimulation` |
| `D7` measure before replacing what the counts accuse | [document-of-intent.md](document-of-intent.md#what-correctness-means), as one of two standing instructions from the developer |
| `D8` an optimisation is pinned against the code it replaced | [tests/README.md](../tests/README.md), as the title of a row in the suite index |
| `R12` a page states its scope, describes the present, and logs its changes | [development.md](development.md#documentation-conventions) |
| `R13` a standing rule is stated here once, and argued elsewhere | [development.md](development.md#documentation-conventions), as an aside inside `R12`'s convention |

Two things that audit cannot do. It reads the tree, so a rule that lives only in someone's head is
invisible to it — which is how the operational rules `O1` to `O5` came to be written down late, and
they were recovered from notes rather than from the repository. And it cannot tell a rule from a
finding stated forcefully: the pages are full of bolded sentences that are conclusions about heat
rather than constraints on work, and the test applied to each was the one at the top of this
page — *would breaking it be noticed?*

Three rules from the previous revision are gone as rules, absorbed into the principles above:
they stated a premise rather than something a change could violate. `R1` is P11, `J1` is P14, and
`J2` was a four-part slogan whose checkable halves are `C4`, `C5`, `C7` and `R9`. They are listed
under [low value](#low-value) so a citation to them does not dangle.

#### The 2026-08-25 pass, and what it found

The set was re-tested from scratch: every merger the table offers at the principle level, and every
pair of rules that looked like one act written twice.

**Two more mergers attempted at the principle level, and both rejected.**

* **P8 with P14.** Both are about cost — *a mechanism nobody uses must cost nothing* and
  *unobservable fidelity is cost* — and a merged principle would read *cost is only justified by
  something observable*. Rejected on the page's own test: the merged sentence generates neither
  side's rules. P8's are about a **mechanism** and its switch, and produce `C4`, `C7` and `C8`;
  P14's are about an **approximation** and its price, and produce `D6`, `M8`, `O4` and `C15`. The
  merged form is a heading that has to be unpacked before it can be applied to either.
* **P10 alone.** It generates exactly one rule, `C6`, which is the shape a principle takes when it
  is really just a rule wearing a hat. Kept anyway, and the reason is the second half of its own
  sentence: *the solver's three invariants are the definition of correctness, and everything else
  is tuning*. That second clause is what makes every other argument on this page safe to have — a
  change that holds the three is a tuning question and a change that breaks one is a defect whatever
  it buys — and it is a statement no rule carries.

**Four rule-level mergers attempted, and all four rejected**, which is the more useful half of the
result: it says the rule list is already at its irreducible size rather than merely asserting it.

| Attempted | Rejected because |
| --- | --- |
| `M4` + `M5` — the noise floor | One is how to measure and the other is how to read. *Keep the fastest of N and publish the spread* is an instruction to a runner; *a figure inside the floor has not moved* is an instruction to a reader, and the reader is usually not the runner. |
| `E10` + `E11` — changing something published | One is about a finding that turned out **wrong** and one about a criterion that is **not wrong**. The second carries two obligations the first does not: keep the old value visible, and do not put the change in the commit that carries the run it would have failed. |
| `C9` + `C10` + `W3` — an authority elsewhere | The shared idea is P7 and is already stated there. What is actionable is the difference: what is sealed, what is destroyed, who sent this. A rule that has to be unpacked into three before it can be applied is a principle, and this one already exists. |
| The seven join rules under P5 — `R7` `R8` `R9` `R10` `R11` `R13` `R14` | These *are* one idea on seven artefacts, and that idea is P5. Merging them would collapse seven subjects and seven distinct checks into one rule whose *Checked by* field is a list, which is the structure the page uses principles to avoid. |

**Two rules were re-filed, and one of those is what produced P15.**

* **`R5` from P12 to P15.** The workshop identity files had been filed under *do not edit what this
  repository cannot regenerate*, and a `modinfo.sbmi` is not hard to regenerate — it is impossible
  to **un-publish**. The reason on the rule and the principle above it disagreed, and the rule was
  right: regenerating it publishes the mod as a new item and every subscriber stays on the old one.
* **`D4` from P4 to P2.** *Test the state before the first step, and the artefacts the mod writes*
  had been filed under *nothing is its own oracle*, and its own argument is about neither oracles
  nor duplication: **both are where a fault survives longest, because nobody looks there.** That is
  a blind spot read as a value, which is P2. `D8` beside it is genuinely P4 and says so in its own
  words — *the old code is the only oracle that answers the question actually being asked* — which
  is what made the mis-filing visible.

**Two rules gained a second principle**, on the precedent `R9` already set. `O4` — corpus walks run
alone — is P14 for its corpus half, because four walks at once cost seventeen times and buy
nothing, and P6 for its wall-clock half, because a duration measured on a contended machine is not
comparable to one that was not. `C15` is P14 for *realistic is the default* and P8 for the ladder
itself, being, in the words of the commit that added it, `C7` grown a dimension.

**Sufficiency was re-tested against the material added since**, which was the intent sweep of
2026-08-24. It produced five rules — `W1`, `W2`, `W3`, `W4`, `D9` — and the split of where they
landed is the evidence: three under principles that already existed, and two under nothing, which
is P15. **Four of the five were already enforced by tests that no rule cited**, and that is a gap in
the *one place* claim running in the direction nothing checks. `R11` fails when a rule names a check
that has stopped running; nothing fails when a live check enforces a rule nobody has written down.
That asymmetry is not closed and is recorded here rather than left implicit.

---

## The index

| # | Rule | Category | From | Checked by |
| --- | --- | --- | --- | --- |
| **E1** | Criteria before data | absolute | P3 | reported by `verdict.py` |
| **E2** | Measure the population, not the specimen | conditional | P1 | `CensusFidelityTests` |
| **E3** | Name the population and the basis of every figure | absolute | P1 | — |
| **E4** | A partial sweep is not a result | absolute | P2 | reported by `verdict.py` |
| **E5** | Every figure on a page comes from the dataset the page is about | absolute | P5 | `EveryQuotedDatasetCountIsCurrent` |
| **E6** | Pair the terms before dividing | conditional | P1 | — |
| **E7** | Check a claim against something that is not the model | absolute | P4 | `LegacyFormulas` `Reference` `DumpAuditTests` |
| **E8** | A check that judged nothing has not passed | absolute | P2 | `CorpusSurvey` |
| **M12** | A settled figure proves it settled | absolute | P2 | `TheRigReachesASteadyStateAndSaysSo` |
| **E9** | Read a censored column as censored | absolute | P2 | reported by `verdict.py` |
| **E10** | Correct a published finding in place | absolute | P3 | — |
| **E11** | A criterion changes only in the open | absolute | P3 | — |
| **M1** | Compare only runs that were stopped the same way | conditional | P6 | `SunlightPanelWalk` |
| **M2** | Parallel for values, linear for durations | absolute | P6 | `ParallelAndLinearProduceTheSameMatrix` |
| **M3** | The run is the unit of parallelism | conditional | P6 | `ParallelAndLinearProduceTheSameMatrix` |
| **M4** | Keep the fastest of N and the median, and publish the noise floor | absolute | P2 | `PerformanceReportTests` `StageLabTests` |
| **M5** | A figure inside the noise floor has not moved | absolute | P2 | `PerformanceReportTests` |
| **M6** | The case key is the contract | absolute | P5 | `BenchmarkBaselineTests` |
| **M7** | Two figures are comparable only if they were taken in one window | absolute | P6 | `SampleStatisticTests` |
| **M8** | A scenario earns its place by answering what nothing else answers | absolute | P14 | — |
| **M9** | A scenario's conclusion is pinned so it cannot invert | absolute | P3 | `ScenarioClaimTests` |
| **M10** | Specimens are chosen by coverage, and every pick names its rule | absolute | P1 | `panel.csv` carries the rule |
| **M11** | The synthetic ship is refreshed against the field | absolute | P1 | `CensusFidelityTests` |
| **M13** | A timing is taken on the build that ships | absolute | P1 | `OptimisedBuildTests` |
| **D1** | Disbelieve a plausible number | absolute | P4 | `ScreeningTests` `LabInvariantTests` |
| **D2** | Hunt for what is built, documented and reached by nothing | absolute | P5 | `SimCommandTests` `DocumentationTests` |
| **D3** | Where one thing exists twice, a test compares the two | absolute | P5 | `BothParsersKnowTheSamePropertyNames` |
| **D4** | Test the state before the first step, and the artefacts the mod writes | absolute | P2 | `DumpAuditTests` `FieldDumpTests` |
| **D5** | A known defect is pinned by a test, not only described | absolute | P3 | `CorpusSurvey` |
| **D6** | A deliberate simplification is recorded as a limit | absolute | P14 | — |
| **D7** | Measure before replacing what the counts accuse | conditional | P4 | — |
| **D8** | An optimisation is pinned against the code it replaced | absolute | P4 | `SolverAb` and the five bit-identity suites |
| **D9** | A fault is recorded whether or not collection is running | absolute | P2 | `AnomalyRegistryTests` |
| **C1** | C# 6 only | absolute | P7 | `LangVersion` on the core project |
| **C2** | The whitelist covers types the local build accepts | absolute | P7 | the mod build (`MDK01`) |
| **C3** | Target `net48`, and never reference the native assembly | absolute | P7 | the build |
| **C11** | Every file the game compiles is compiled by the suite's own build | absolute | P7 | the build |
| **C4** | Nothing allocates on the stepping path | absolute | P8 | `StageLabTests` |
| **C5** | The core speaks no game type | absolute | P9 | `CoreIsolationTests` |
| **C6** | The solver's three invariants hold | absolute | P10 | `ConductionTests` `StabilityTests` `ConductionClampGateTests` |
| **C7** | Every mechanism has a switch that removes its own cost | absolute | P8 | `FeatureToggleTests` |
| **C15** | A feature's configuration runs from `off` to `realistic` | absolute | P8 P14 | `EveryCoreSettingIsReachableFromAWorldsConfiguration` |
| **C8** | Absent and empty mean the same thing | absolute | P8 | `CorpusGuardTests` |
| **C9** | The game's own answer is read, never overridden | absolute | P7 | `RoomPressureTests` |
| **C10** | The server is authoritative over damage | absolute | P7 | — |
| **W3** | An authority check reads what the engine supplies, never what the sender wrote | absolute | P7 | — |
| **W4** | No call across the mod's API throws into its caller | absolute | P9 | `ModApiShapeTests` |
| **W1** | A saved world loads on the build that wrote it, and on the ones either side | absolute | P15 | `StorageAndSettingsTests` |
| **W2** | A name something outside this repository addresses is never repurposed | absolute | P15 | `TheRetiredPropertyNamesAreStillRead` `NoSettingReusesANumberThatWasDeliberatelyRetired` |
| **R1** | *The repository is the mod folder* | low value | P11 | absorbed into P11 |
| **R2** | Build output and the corpus live outside it | absolute | P11 | `mdk.ini` |
| **R3** | No credential is written into the tree | absolute | P11 | `CredentialScanTests` |
| **R4** | `Models/` is not restructured | absolute | P12 | `TheModelTreeKeepsItsShape` |
| **R5** | The workshop identity files are not regenerated | absolute | P15 | `ShippedIdentityTests` |
| **R6** | Vendored code is replaced, never edited | absolute | P12 | — |
| **R7** | Every document is indexed, and every link resolves | absolute | P5 | `DocumentationTests` |
| **R8** | Every setting is documented, wired, and read by something | absolute | P5 | `ConfigurationDocTests` `SettingsWiringTests` |
| **R17** | Every dial is asserted to move an outcome, not only to be read | absolute | P5 | `SettingsDialReachTests` `LoopDialReachTests` `PlanetDialReachTests` `BlockDialReachTests` |
| **R9** | The API page is part of the contract | absolute | P5 P9 | `EveryModApiEntryIsDocumented` `ModApiShapeTests` |
| **R10** | Every test class says what it is for | absolute | P5 | `EveryTestClassSaysWhatItIsFor` |
| **R11** | A check is cited only if it runs | absolute | P5 | `EveryCheckCitedByTheRulesPageResolves` |
| **R15** | An identifier cited anywhere resolves to something that exists | absolute | P5 | `EveryCitedIdentifierResolves` |
| **R16** | A pointer in code is plain text, never a link | absolute | P5 | `NoPointerInCodeIsWrittenAsALink` |
| **R12** | A page states its scope, describes the present, and logs its changes | absolute | P3 | partly |
| **R13** | A standing rule is stated here once, and argued elsewhere | absolute | P5 | `EveryRuleCitedByAPageExists` |
| **R14** | A comment names something that is there | absolute | P5 | the build (`CS1574` and its family) `NoDocCommentDescribesSomethingThatIsNotThere` |
| **O1** | Every long run is capped | absolute | P13 | — |
| **O2** | A run with no bounded duration gets no hang timeout | conditional | P13 | — |
| **O3** | Long sweeps resume, and progress is measured in bytes | absolute | P13 | — |
| **O4** | Corpus walks run alone | absolute | P6 P14 | `EveryCorpusWalkDeclaresThatItRunsAlone` |
| **O5** | A lab streams, and counts what it did not measure | absolute | P13 | — |
| **J1** | *Game mod first* | low value | P14 | absorbed into P14 |
| **J2** | *Light, isolated, tested, open* | low value | — | absorbed into `C4` `C5` `C7` `R9` |
| **J3** | The corpus filters are strict, and their cost is recorded | conditional | P1 | `BlueprintTests` |

Seventy-four rows: **sixty-four absolute, seven conditional, three low value.** The last three are
retired as rules and kept only so a citation to them does not dangle, so seventy-one of these are
rules a change can be measured against. Fourteen of them are unchecked and say so, and three more
are *reported* rather than checked, which is weaker and is written as such.

---

## Absolute

### P1 — A figure carries its scope

#### E3 — Name the population and the basis of every figure

**Say which population a statistic is over, and put the basis in a column rather than a
footnote.**

The corpus is 8,142 hulls, 8,132 distinct name-and-id pairs and 8,054 distinct names, and a
statistic over one of the three is not a statistic over another. The same applies to units and
clocks: substep demand is proportional to step length, so a figure taken at `Frequency 4` is
double the same figure at `Frequency 8`, and seconds in a scenario table are simulated seconds
rather than seconds a player waits.

*Applies to:* every reported figure.
*Checked by:* — nothing checks that a page names its population.
*From:* [balance.md](balance.md), [benchmarks.md](benchmarks.md).

#### M10 — Specimens are chosen by coverage, and every pick names its rule

**A panel is cut by taking the extremes of every feature axis and then the ship furthest from
everything already taken, and the rule that selected each ship is written beside it.**

Picking the most popular ships yields near-duplicates from the densest part of the design space
and no examples of anything unusual, which is backwards: the interior of a cluster is predictable
from its edges, and a balance figure fails at the edges first. Writing the rule down is what lets
a panel be audited and rebuilt rather than trusted.

*Applies to:* any reduced sample standing in for the corpus. The claim that a panel reproduces
the corpus is a hypothesis and is still unchecked.
*Checked by:* `panel.csv` carries a `rule` and a `why` column per ship.
*From:* [balance-lab.md](balance-lab.md), [tools/corpus/README.md](../tools/corpus/README.md).

#### M11 — The synthetic ship is refreshed against the field

**When a new dump arrives, the census tiers and the field observations beside them are both
updated.**

Every benchmark builds from a measured block population rather than an invented one, because a
step takes as many substeps as the stiffest block needs and a hull's cost is therefore set by its
lightest block. The old invented mix asked for three substeps where a real ship asks for twenty
to thirty, which made every scale figure about six times too cheap.

*Applies to:* `Census` and `Census.Field`.
*Checked by:* `CensusFidelityTests`.
*From:* [benchmarks.md](benchmarks.md#keeping-it-honest), [tests/README.md](../tests/README.md).

#### M13 — A timing is taken on the build that ships

**Every project the harness times compiles optimised, in every configuration, and a check reads the
attribute off the built assembly.**

`dotnet run` and `dotnet test` build `Debug`, and a Debug assembly carries
`DebuggableAttribute(DisableOptimizations)`, which the JIT obeys: no register allocation across
statements, no bounds-check elimination, no inlining. Nothing set `<Optimize>` under `tests/`, so
**every millisecond this repository published before 2026-08-26 was measured on code the game never
runs** — a step 3.4× dearer than the same commit optimised.

**It is not a constant, which is why it is a rule and not a footnote.** A uniform factor would leave
every ratio intact, and a benchmark exists for ratios. Measured on one commit in one window, the row
fill is 5× and the cost of being measured 7.7× where the clamped conduction loop is 2.6×: the
instrument exaggerated exactly the work — a store through a field, a load hoisted out of a loop —
that an optimisation pass spends its time on. Every *share of a step* on
[benchmarks.md](benchmarks.md) taken before that date is a share of the wrong step.

*Applies to:* every project under `tests/`, and any future harness that reports a duration.
*Checked by:* `OptimisedBuildTests`, which reads `DebuggableAttribute` off the built core, harness
and test assemblies. It was proven against an unoptimised `--no-incremental` build before it was
believed — an incremental build ignores a `-p:Optimize=false` on the command line, so the first
attempt to break it passed.
*From:* [performance.md](performance.md#iteration-1--the-harness-measured-unoptimised-code).

### P2 — What the instrument could not see is part of the result

#### E4 — A partial sweep is not a result

**A corpus run is quoted whole, or quoted with the words "partial" and the count attached.**

The corpus is walked largest-first, so an interrupted run has measured capital ships and nothing
else. At 500 of 8,142 ships the load criterion read 95 % against a true 75 %, the affordability
criterion read *failing* when it passes, and the medians were three times high.

*Applies to:* anything drawn from a sweep that has not finished.
*Checked by:* `verdict.py`, which counts the blueprints the corpus holds and prints
`*** PARTIAL: n of m ***` above everything else — and says the population is **unknown** where the
corpus is not on the machine, rather than assuming a dataset is whole (`P2`). `test_scoring.py`
pins the threshold and the collection case. It is *reported* rather than enforced: nothing stops a
reader quoting the figures under the banner.
*From:* [balance.md](balance.md).

#### M12 — A settled figure proves it settled

**A temperature read off a rig that has not reached equilibrium is a reading of how fast it is
climbing, and nothing about the rig says which one it gave you.**

`LoopCoolantMassTests` ran a ring with a 125 kW source, the environment disabled and no sink of any
kind. Every arm climbed linearly and for ever. Read at step 400 the four arms said 715.5 K and
387.0 K on a large grid and 661.5 K and 912.1 K on a small one, and [balance.md](balance.md)
published them as what the coolant density is worth; read at step 25,600 the same arms say 26,836 K
and 5,908 K. **What the rig measured was the ratio of two heat capacities**, which is exactly what
coolant mass is — so the answer looked like physics, had the right shape, pointed the right way, and
was a stopwatch reading. Given a sink, the correction is worth 6.9 K and 0.8 K.

The check is one line and it is the run's own: read the figure, double the run, read it again. A
number that moved is a rate.

*Applies to:* any figure quoted as where something settles, ends up, or reaches.
*Checked by:* `TheRigReachesASteadyStateAndSaysSo`, and by
`AWorkingLoopReportsGrossFlowNotItsNearZeroNet` stepping to a balance criterion with a bound rather
than to a fixed count. Nothing checks the labs that were not written this way.
*From:* [balance.md](balance.md#the-coolant-mass-is-doing-an-undeclared-job-and-the-measurement-that-said-so-was-wrong),
[backlog.md](backlog.md) `C43`.

#### E8 — A check that judged nothing has not passed

**Every invariant reports how many cases it judged, and a run where a claim was never evaluated
fails rather than passes.**

An equilibrium gate that admits nothing is silent. One was written that admitted zero of 88
ships, and the guard on the judged count is what found it. The same shape appears in the
documentation tests, which assert they saw more than a thousand test names before believing they
found none missing.

*Applies to:* every invariant, audit and survey.
*Checked by:* `CorpusSurvey` — its balance claim asserts a non-zero judged count, and the
population claims fail when a reader subsystem goes dark. Not by `verdict.py`, which prints `?`
for a criterion its dataset cannot answer and exits zero either way; that is a report to a human,
not a gate.
*From:* [balance-lab.md](balance-lab.md).

#### E9 — Read a censored column as censored

**The lab never destroys an overheating block, so a peak temperature above critical is not a
prediction.**

Anything past critical keeps generating for the rest of the clock; the 541,648 K reading is
1,800 s of an undamped source, not a temperature the mod can reach. Peaks above about 1,500 K say
*ran away* and nothing finer, and population statistics on the right tail describe the harness.
Crossing times and shares are decided before the divergence and are unaffected — the findings
rest on those.

*Applies to:* every peak, p95, p99 and maximum drawn from a lab run.
*Checked by:* reported by `verdict.py`, which prints the censored share beside the criteria.
*From:* [known-issues.md](known-issues.md#deliberate-limits).

#### M4 — Keep the fastest of N and the median, and publish the noise floor

**Every timed case is measured several times and both the fastest and the middle reading kept, and
every report opens with the spread between fastest and slowest of a repeated case.**

Timing noise is one-sided: a sample is the true cost plus whatever else the machine was doing, so
averaging it in measures the operating system. Without the floor printed beside them, several
features cost less than the spread and would read as making the solver faster.

**And N is enough only when the fastest has been reproduced.** The one-sidedness that makes the
minimum the right statistic also gives it a long lower tail, and a fixed N samples that tail to an
unpredictable depth: `bench stages` kept the fastest of fifteen for six passes, and three runs of
one binary on one hull spread **48 %, 28 % and 67 %** across its surface, room and link stages. The
solver stage was the exception at 3.5 %, because each of its repeats was twenty steps and it was
quietly taking three hundred samples. A repeat count is a claim about convergence, so it has to be
one that was checked — by requiring several readings to agree with the best rather than by choosing
a number. Two passes' worth of rejected optimisations were judged at effect sizes smaller than the
uncertainty this hid.

**And reproducing the fastest is not enough either, which took a fifth pass on this rule to find
out.** Four runs of one binary at 126,731 blocks, four hundred repeats each, every candidate summary
compared: the minimum reproduces to **4.5 %** on the room pass and to **97 %** on `register`, and
the *median* reproduces to **0.9 %** on exposure where the minimum manages 30 %. **No summary is
best for more than three of the eight stages, and for two of them nothing tried reproduces at all.**
What separates them is whether a stage has a fast mode it reaches rarely: where the minimum sits at
45 % of the median, best-of-N samples that mode to a depth that is an independent draw per run, and
four hundred repeats do not fix it. Where the distribution is one mode with additive noise — the
room pass, at 85 % — the minimum is right.

So **a stage reports its minimum and its median, and a ratio is believed only where both legs agree
on both.** The one-sided-noise argument still holds and still rules out the mean; what it does not
establish is that the minimum of a *bimodal* sample is reproducible, and that was assumed for nine
passes rather than measured.

**And which of the two reproduces is a property of the session, not of the stage — a sixth pass on
this rule.** The four runs above were one window. Two days later, twelve runs of bit-identical
exposure code in one held window put its minimum within 20–30 % of the earlier session's and its
median at **less than half**: `best/med` was 0.45 then and 0.81–0.87 now. The stage that the table
above assigns the median is the stage whose median is the figure that did not survive the session.
That is why the rule is *report both and refuse marginal ratios* rather than *read this column for
this stage*, and it is why `M7` is scoped to any two figures compared rather than to a pass.

*Applies to:* every millisecond in a report.
*Checked by:* `PerformanceReportTests`, and `StageLabTests.EveryStageStopsForAReasonItCanName` and
`EveryRowCarriesItsMedianAndItsShape` for the sampling. **The reproducibility itself is not checked
and cannot usefully be**: it is a property of the machine, and a test demanding it inside an
eight-way parallel suite failed and passed on consecutive runs of unchanged code. It is measured on
a held machine with `bench samplestats`, which is what the figures above come from.
*From:* [benchmarks.md](benchmarks.md), [performance.md](performance.md#pass-9-iteration-5--no-single-statistic-reproduces-and-two-stages-have-none).

#### M5 — A figure inside the noise floor has not moved

**A change to a number that was never measurable is not a regression and is not an improvement.**

Making the solver leaner lifts several feature costs out of the noise at once, and each arrives in
a diff as a large percentage increase over a number that never meant anything. The first run of
the comparison against a real optimisation reported sixteen regressions, fifteen of them figures
that had previously been unmeasurable.

*Applies to:* every benchmark comparison.
*Checked by:* `PerformanceReportTests` — the comparison declines to count them.
*From:* [benchmarks.md](benchmarks.md#reading-a-comparison).

#### D9 — A fault is recorded whether or not collection is running

**A caught exception, and a grid that has gone numerically bad, are written down even when telemetry
is off.**

Everything else the mod observes costs something on every healthy frame and is rightly off unless
somebody is reading it. A fault is not: it costs nothing until the mod has already failed, and by
then it is the only evidence there will be. Gated together with observation, all twenty-two `catch`
blocks in the adapter discarded their exception, wrote nothing to any file, and left the grid
running in whatever state the throw abandoned it in — including the guard around `ThermalGrid.Tick`,
whose own comment said an exception named there was worth more than a crash dump and which named it
nowhere. Only the first occurrence of each kind is logged as it happens, because a throw inside a
step runs once per grid per frame; the count keeps rising and reaches the closing summary.

*Applies to:* every `catch` in the mod, and the NaN guard on a grid.
*Checked by:* `AnomalyRegistryTests`.
*From:* [telemetry.md](telemetry.md#faults-are-recorded-whether-or-not-collection-is-running).

#### D4 — Test the state before the first step, and the artefacts the mod writes

**Both are where a fault survives longest, because both look like output rather than behaviour
and neither changes a temperature.**

Three faults found in one pass over a field dump had exactly that shape: a grid's substep demand
was conduction-only until it had stepped, a grid's one-off build was charged to no row in the
cost table, and a block type's peak temperature could sit below its own maximum. Every test
measures a grid *after* stepping it, and nothing read the report the mod writes about itself.

*Applies to:* new instrumentation, new report rows, and anything read outside a step.
*Checked by:* `DumpAuditTests`, `FieldDumpTests`.
*From:* [tests/README.md](../tests/README.md).

### P3 — The claim is fixed before the data and corrected in place after

#### E1 — Criteria before data

**Write the criterion, and the number that fails it, before the run that tests it.**

Criteria invented after seeing the data are criteria fitted to the data. G1–G6 were written
before a corpus existed, which is what makes a run that fails them a finding rather than an
excuse to move a threshold.

*Applies to:* population and balance claims. A synthetic rig may be built to answer a question
that arose this morning; a criterion may not.
*Checked by:* reported by `tools/corpus/verdict.py`, which evaluates the criteria as written and
names the ones a dataset cannot answer instead of skipping them. It prints; it does not fail.
**A dataset too coarse to state a criterion cannot answer it either**, and since 2026-08-25 it says
so: a criterion given as a share of the corpus needs a corpus that can tell its two sides apart, so
on thirteen ships — where one ship is 7.7 % — *nothing critical* and *one per cent critical* are the
same reading and `G1` is unanswered rather than holding. `scoring.resolves` is the rule and it
follows from the threshold each criterion already states rather than from a floor typed anywhere.
It is a resolution test and not a confidence one: a partial walk that passes it is still a partial
walk (`P1`).
*From:* [balance-lab.md](balance-lab.md).

#### E10 — Correct a published finding in place

**When a finding turns out to be wrong, the correction goes where the finding is, in its own
commit, naming what was wrong and what the number actually is.**

The alternative is a document that is right on its newest page and wrong on the one a reader
found first. Two corrections in an earlier pass followed this shape: a ratio that was not a ratio,
and a verdict script quoting a figure it could compute. This page is held to it too, and its own
corrections are in the [change log](#change-log).

*Applies to:* anything already written down, including this page.
*Checked by:* — judgement.
*From:* the commit history.

#### E11 — A criterion changes only in the open

**A balance criterion or a threshold may be changed, but only in a commit that says why, keeps
the old value visible, and does not carry the run that failed it.**

`E1` is worth nothing without this one. A criterion written before the data and then quietly
relaxed the week a run fails it has been fitted to the data by a slower route, and the commit
history is the only place that is visible. Separating the two commits is what makes the change
reviewable: a criterion moved for a stated reason is a decision, and a criterion moved in the same
diff as the numbers it now admits is a result.

*Applies to:* G1–G6, every threshold in `verdict.py` and in the lab invariants, and every bound
in a pinned defect test.
*Checked by:* — judgement. A commit touching both a threshold and a dataset is the shape to look
for in review.
*From:* the gap it closes is `E1`'s.

#### M9 — A scenario's conclusion is pinned so it cannot invert

**Every scenario states a conclusion in its summary line, and a test asserts that conclusion
cannot quietly flip.**

A scenario whose headline can invert unnoticed is worse than none, because it reads like
evidence.

*Applies to:* every scenario in the harness.
*Checked by:* `ScenarioClaimTests`, `SelfShadowScenarioTests`, `CoolingScenarioClaimTests`.
*From:* [development.md](development.md).

#### D5 — A known defect is pinned by a test, not only described

**A defect that is understood and not yet fixed gets a test asserting the behaviour it currently
has, so that fixing it fails a test rather than moving a number nobody is watching.**

Bound rather than asserted to zero where the scale is what matters: the sealed-block ratio fails
on a regression to the earlier scale without letting one unexplained ship hold the suite hostage.

*Applies to:* every entry in the defect list that is not fixed in the same pass.
*Checked by:* the pinned tests themselves. The sealed-block bound now lives in `CorpusSurvey`
rather than in the standalone walk that used to carry it.
*From:* [tests/README.md](../tests/README.md), [known-issues.md](known-issues.md).

#### R12 — A page states its scope, describes the present, and logs its changes

**Every page opens by saying what it covers and what it does not, its body is in the present tense,
and the only place a revision is recorded is a `## The identifier namespace this page shares with the backlog

**Eleven identifiers mean one thing here and another in [backlog.md](backlog.md)** — `C3`, `C7`,
`C8`, `D1` to `D6`, `E2` and `E4` — because the two pages were numbered independently and both use a
letter and a number. `C3` is *target `net48`, and never reference the native assembly* here and
*whether to ship `MaxSubstepsPerBlock 6`* there.

**It is frozen rather than fixed, and that was costed.** The letters on this page are historical —
the categorisation above is orthogonal to them — so remapping this side is the cheap direction. But
the eleven are cited 215 times, 78 in code and 137 in prose, and every one has to be resolved to a
page by hand before it can be renamed: a citation resolved wrongly reads exactly like one resolved
rightly, so the diff's errors would be silent. Against an ambiguity a reader resolves from context
and nothing is recorded as having got wrong, that is `P14`'s trade taken in the other direction.

**What is checked is that it does not grow.**
`TheTwoPagesShareNoIdentifierTheyDidNotAlreadyShare` lists the eleven and fails when a new rule or a
new backlog row takes an identifier the other page already uses. `W` was chosen for the rules added
on 2026-08-25 because it collides with nothing; that check is what makes it a rule rather than a
habit. The set shrinks as backlog rows retire, and a shrink is not a failure.

---

## Change log` at the end.**

A page that narrates what was tried reads as a description of the code long after it has stopped
being one, and nothing about it looks wrong — the prose is accurate about a repository that no
longer exists. Present tense is what makes that drift visible, and the change log is where `E10`'s
correction goes: a finding corrected somewhere other than where it was published is not corrected.
A measurement's before-and-after table is present-tense evidence and stays; the story around it
does not. A page that cannot state its scope in a paragraph is two pages.

*Applies to:* every markdown file in the tree, this one included.
*Checked by:* `EveryPageHasAChangeLog` and `EveryDocumentIsInTheIndex`. The scope paragraph and the
present tense are judgement — nothing reads prose for tense.
*From:* [development.md](development.md#documentation-conventions).

### P4 — Nothing is its own oracle

#### E7 — Check a claim against something that is not the model

**A test that asks the model the same question twice agrees with whatever the model happens to
do.**

So the independent oracles are kept and used: `LegacyFormulas` is a verbatim copy of the original
mod's equations, `Reference` works sun visibility out the slow obvious way by intersecting rays
with every cell, and `benchmarks/field-dump/` is three CSVs a real world produced. The
self-shadow scenario's claim test recomputes its shares by ray-versus-cube rather than by calling
the model.

*Applies to:* any test whose subject is the model's own arithmetic.
*Checked by:* `DumpAuditTests`, `SelfShadowScenarioTests`, and the oracles themselves.
*From:* [tests/README.md](../tests/README.md).

#### D1 — Disbelieve a plausible number

**A harness fault looks exactly like physics, so a figure with no prior is investigated before it
is explained.**

Three faults were found this way and not one was caught by a test: a gyro's `ForceMagnitude` read
as thrust rather than torque, turning one gyro into 33.6 MW and a hull into 342,510 K; a
definition that declares no mount points read as a block that mounts nowhere, giving batteries no
conduction and no exposure; and a store counted as charging and discharging at once. Each was a
definition read wrongly, so **a definition misread is the first hypothesis, not the last**. A
block with no exposed face and no conduction has nowhere to send its heat and climbs without
symptom — that shape is a harness fault until proven otherwise.

*Applies to:* any figure from the harness that nobody can bracket in advance.
*Checked by:* `ScreeningTests` holds the standing versions of that disbelief —
`OnlyAThrusterCarriesThrust`, `ABlockThatDeclaresNoMountPointsStillConducts`,
`AStoreIsNotBothChargingAndDischarging` — and `LabInvariantTests` holds the units guard, that no
definition claims a preposterous amount of power.
*From:* [balance-lab.md](balance-lab.md).

#### D8 — An optimisation is pinned against the code it replaced

**A change whose stated purpose is cost rather than behaviour is asserted bit-identical to what it
replaced, on a fixture that proves it exercised something.**

The old code is the only oracle that answers the question actually being asked — *did this change
anything?* — and a benchmark cannot answer it, because a figure that moved and a figure that broke
look the same. Five suites are held to this shape: the precomputed environment rows, the fixed
source row, the gated conduction clamp, the batched diagnostics and a step spread across frames.

The second half is the half that is easy to leave out. `Hulls.Driven` asserts its own
postconditions — producers running, temperatures spanning at least 100 K — because two runs of a
hull that built nothing agree perfectly, and agreement is the whole assertion. That is `E8` at the
one place where a passing comparison is least trustworthy.

*Applies to:* any rewrite offered as a saving.
*Checked by:* `SolverAb.AssertIdentical` and the suites that call it —
`PrecomputedEnvironmentTests`, `FixedSourceRowTests`, `WattsClearFusionTests`,
`ConductionClampGateTests`, `DiagnosticBatchingTests`, `SpreadStepTests` — over `Hulls.Driven`,
which checks the fixture it built.
*From:* [tests/README.md](../tests/README.md), [benchmarks.md](benchmarks.md).

### P5 — One definition, and at least one consumer

#### E5 — Every figure on a page comes from the dataset the page is about

**No count, share or total is written into prose by hand when the data behind it can be read.**

The balance bench stated 36 panel ships for as long as the panel had 50 — it is 52 now — because
the header had been typed rather than generated. A reader who catches one wrong count stops believing the right
ones. *(The panel is 52 now — `C32` re-picked it on the corrected census. This sentence has to
name the current figure as well as the historical one, or the check below excuses it forever:
recording a wrong count is allowed, and only while the same sentence proves it knows the right
one.)*

*Applies to:* every page that reports on a dataset, including this one's own claims about the
suite.
*Checked by:* `EveryQuotedSuiteSizeIsCurrent` for a stale suite size and
`EveryQuotedDatasetCountIsCurrent` for a count of the panel, of the values authored in `Cubes.xml`
or of the suite's classes; the pack scripts generate the figures the report pages carry. **It reads
source comments as well as pages since 2026-08-25**, which is where the same drift had been sitting
unwatched — `KnobSweep`'s summary went on describing a panel of 36 for a day after every page had
been corrected to 50, and two more stale counts came out of the extension. A count of a dataset that is not in this repository
— the corpus, the game's own definitions — is still caught by nobody, and neither is a count written
in a form other than *`N` `<noun>`*: `a 49-ship panel` is how the tree scopes a past measurement to
the dataset it was taken on (`P1`), and no check can tell that apart from a stale claim.
*From:* [tools/corpus/README.md](../tools/corpus/README.md).

#### D2 — Hunt for what is built, documented and reached by nothing

**The highest-yield defect class here is a thing that exists, is described accurately, and is
called by nothing.**

The code is careful and the prose is careful, so the defects are not wrong logic. Four turned up
in one pass: a 297-line lab answering an open balance criterion with no command and no test; a
documented and fully tested block property the in-game parser had no name for, so it worked in
every test and in no world; six harness commands in no help text; and a Save and a Reset button
the menu had not had for months. Two more turned up in this one: `BlockThermalProperties.Validate`
and `ThermalSettings.Validate` produce diagnostics no caller in the shipped code asks for, and
five retired lab invariants sit in the suite with no caller at all. Anything declared once and
mentioned nowhere else is the place to look, and a name-frequency scan finds them in seconds —
though it gives a false negative on a misspelled declaration.

**And a machine looks for it now, which nothing did until 2026-08-27.** This entry used to end by
naming that gap in the test project, and closing it found two on the first run:
`Scenarios.OpenSides`, a documented six-face count nothing called, and `IncandescenceTests.
Luminance`, a photopic integration written from first principles and then never run — which took
`Photopic` with it. The compiler cannot do this: an unused private *field* is a warning and an
unused private *method* is not, so these compile, get maintained, and read to the next person as
something the suite relies on. **The shipped code is scanned on the same terms and is clean**, which
is a result rather than an omission — every private helper under `Thermodynamics/` is reached — with
the vendored framework excluded because `R6` forbids editing the one uncalled method in it.

*Applies to:* every pass over the repository.
*Checked by:* `SimCommandTests` — including `EveryLabThatProducesAReportIsReachable` —
`DocumentationTests`, `EverySettingIsReadBySomething`, and
`UncalledCodeTests.NoPrivateHelperInTheTreeIsCalledByNothing` over every private helper in the
tree. That last one counts identifiers across `Data/` and `tests/`, so an *overload* of a name something else
calls is still invisible to it — the same false negative as a misspelled declaration, and the same
reason a pass still has to look.
*From:* the defect record kept with the session notes; [backlog.md](backlog.md) A19.

#### D3 — Where one thing exists twice, a test compares the two

**One file format read by two parsers will drift, and the drift is silent in both directions.**

This repository has two of most things by necessity: two definition parsers, one that needs a
session and one that must not; a harness and a game adapter; a page and the code it describes; a
committed baseline and the report that produces it. Each pair can drift and neither half looks
wrong on its own.

*Applies to:* any name list, format or figure that exists in two places.
*Checked by:* `BothParsersKnowTheSamePropertyNames`, `DefinitionFileTests`,
`CriticalTemperatureMirrorTests`, `BenchmarkBaselineTests`, and the documentation tests.
*From:* [known-issues.md](known-issues.md).

#### M6 — The case key is the contract

**A benchmark case is renamed only deliberately, because a rename turns one regression into two
unrelated rows.**

The report is keyed on `section / case / metric`, and that key is the only thing joining this
run to the last. A silently renamed case is the one way a benchmark suite lies about itself.

*Applies to:* the performance report, and by extension any keyed artefact that is diffed —
including the corpus CSVs.
*Checked by:* `BenchmarkBaselineTests` compares the committed baseline's keys against a fresh
run's; `EveryFigureHasAUniqueKey` guards the keys themselves.
*From:* [benchmarks.md](benchmarks.md).

#### R7 — Every document is indexed, and every link resolves

**A page under `docs/` is listed in the README's table, every relative link points at a file that
exists, every anchor names a heading that exists, and no page cites a test by a name it no longer
has.**

A dead link is worse than a missing one: it reads as though somebody checked. A rename left nine
dead links behind, three pages had fallen out of the index entirely, and three more pointed at
scripts that stopped existing when this became its own repository.

*Applies to:* every markdown file in the tree.
*Checked by:* `DocumentationTests` — `EveryDocumentIsInTheIndex`, `EveryRelativeLinkResolves`,
`EveryAnchorNamesAHeading`, `NoPageNamesATestThatHasBeenRenamed`, `EveryTestClassIsInTheIndex`.
*From:* [tests/README.md](../tests/README.md).

#### R8 — Every setting is documented, wired, and read by something

**A setting exists in `Settings.cs`, in the reference table, in the menu, and in code that reads
it — or it does not exist.**

Twenty-one settings had accumulated that were in the code and not the reference. The opposite
also happened: a toggle with a menu entry, a config field, a label and no reader at all.

**And *wired* is five places, not four.** The world's copy and the solver's are two classes bridged
by a hand-written list of assignments; a field on the solver's that the bridge does not copy is a
setting that is documented, named, clamped, replicated and left at its default in every world, with
every test green, because a test builds the solver's copy directly and never crosses the bridge.

*Applies to:* every world setting.
*Checked by:* `ConfigurationDocTests` and `SettingsWiringTests`, including
`EverySettingIsReadBySomething`, `NoTwoSettingsShareAProtoMemberNumber`,
`EverySettingIsClampedOrDeliberatelyNot`, `EverySolverSettingIsCopiedFromTheWorldsCopy` and
`NothingIsCopiedIntoTheSolverThatTheSolverDoesNotHave`.
*From:* [configuration.md](configuration.md).

#### R17 — Every dial is asserted to move an outcome, not only to be read

**"Read by something" is a grep, and a value can be read into a variable that nothing multiplies
by.**

That is not hypothetical: `LoopStagnantTransferFraction` was authored, documented, exposed to
players on a slider, clamped, copied into the running properties **and read** — and no line of the
simulation multiplied anything by it. `R8` was satisfied throughout. A dial in that state does not
announce itself, because the curve it produces comes back flat and *this dial does not matter* is
indistinguishable from *this dial is not wired to anything*.

**The check enumerates rather than recites**, because a list is what fails: every field on every
properties class the simulation reads is swept and asserted to change something. A field added
tomorrow is checked tomorrow. Where a rig cannot see a dial, the answer is a rig — the exemptions
are asserted to be inert from the other side, so one that stops being true fails rather than
quietly excusing a live dial.

**Most of the cost is building a rig that can see the dial**, and every field that read inert on
the first pass turned out to be live: a coefficient cap is invisible except across a two-kelvin
lift, an overshoot clamp is invisible unless the step is coarse *and* the work budget is not, a
damage rate is invisible until something crosses its rating, and a ceiling is invisible until it is
swept to a level that binds rather than scaled.

*Applies to:* every field on `ThermalSettings`, `LoopThermalProperties`, `PlanetThermalProperties`
and `BlockThermalProperties`.
*Checked by:* `SettingsDialReachTests`, `LoopDialReachTests`, `PlanetDialReachTests` and
`BlockDialReachTests`; `DialReachTests` covers the eighteen `KnobLab` sweeps from the other side.
*From:* [backlog.md](backlog.md) `C33` `C37`.

#### R9 — The API page is part of the contract

**The mod API is a dictionary of strings to delegates, so a name that has moved is discovered at
run time, in someone else's session, by a cast that fails.**

`api.md` is the only place those names and signatures are written down for a reader, which makes
it part of the contract rather than a description of one. **A failed cast returns null rather than
throwing**, so the symptom is not an error but a feature of somebody else's mod that quietly does
nothing — which is why the signature is as much of the contract as the name.

*Applies to:* every entry in the delegate table, its signature, the worked examples that cast with
it, and the version number beside it.
*Checked by:* `EveryModApiEntryIsDocumented` for the names, `ModApiShapeTests` for the signatures in
both directions and for the page's own examples casting the way its tables say.
*From:* [api.md](api.md).

#### R10 — Every test class says what it is for

**In its own summary, not in an index — and the index says only where to look.**

*Applies to:* every test class.
*Checked by:* `EveryTestClassSaysWhatItIsFor`, `EveryTestClassIsInTheIndex`.
*From:* [tests/README.md](../tests/README.md).

#### R11 — A check is cited only if it runs

**A page names a test as the thing that enforces a rule only when that test is live: an
executable case, or a build setting the compiler reads. A superseded check is deleted, not left
in the tree for a page to point at.**

An unchecked rule marked as unchecked is honest. An unchecked rule marked as *checked* is worse
than either, because it ends the search. Three citations on the previous revision of this page
failed that test — a check named after a file rather than a class, three invariants attributed to
the wrong class, and a defect bound cited to a method that stopped running when it was demoted to
an uncalled helper. All three read as enforcement and none of them was.

`NoPageNamesATestThatHasBeenRenamed` does not catch this. It fires only when a cited name shares
a three-word prefix with a real test, and it only looks at names of four camel-case words or more,
so a two-word citation like a file name is invisible to it, and a name that never existed at all
is invisible unless it happens to look like a rename.

*Applies to:* the *Checked by* field on this page, and any page that names a test as evidence.
*Checked by:* `EveryCheckCitedByTheRulesPageResolves`, which resolves every name in a *Checked by*
field to a live test case, a class holding one, a type or member the code refers to somewhere other
than its own declaration, or a file that exists. It would have caught all three stale citations.
Two limits it states rather than hides: it reads this page's fields only, not every page that names
a test, and *referred to somewhere* is not *reached at run time*, so a helper called only by another
dead helper still resolves.
*From:* the three stale citations above, generalised.

#### R13 — A standing rule is stated here once, and argued elsewhere

**A page that depends on a rule cites it by identifier and does not restate it. This page holds the
sentence; the page that has the evidence holds the argument.**

This is `D3` applied to prose, and it is why this page exists. A rule written into the one document
its author happened to be writing is a rule the next reader finds by accident: eleven pages each
stated one in passing, several operational rules lived only in notes outside the tree, and two
notes contradicted each other outright over `--blame-hang-timeout` because neither knew the other
was a rule. Restating a rule in two places is the same defect in slower motion — both copies read
as authoritative, and the drift between them is silent in both directions.

*Applies to:* every page, and every rule.
*Checked by:* `EveryRuleCitedByAPageExists` reads each page's banner and fails on a citation this
page does not state, and on a banner that cites nothing. `TheRulesPageIndexesEveryRuleItStates`
holds the page's own halves together: a rule stated here is indexed here and follows from a
principle, unless it is filed as low value. Neither can see a rule that has never been written
down, which is what the audit is for.
*From:* [development.md](development.md#documentation-conventions); the assembly of this page.

#### R14 — A comment names something that is there

**A comment describes a definition that exists, in the space a name needs; where it would argue,
it names the page that argues.**

A member moved or deleted leaves its comment behind, and the comment comes to rest on whatever is
below it. Nothing complains: it compiles, it reads as documentation, and it describes a different
thing or no thing at all. Twenty-three had accumulated, several still describing code that had been
replaced — a coolant advection scheme that is now a rotation, a setting renamed into a different
unit, a per-profile definition overlay that no longer exists. That is `D2`'s defect class in prose:
written, accurate-looking, and attached to nothing.

**Not arguing is the other half, and it is what prevents the first.** A comment that argues a topic
has to be maintained against a subject it does not sit next to, so it rots where a name does not —
and the argument belongs on a page, where it can be found by somebody who is not already looking at
that line.

> **This said *the length limit* until 2026-08-25, and length is the proxy rather than the fault.**
> [development.md](development.md#and-in-the-code)'s two-line limit is about a comment inside a
> body, where 73 % of the tree's 2,347 already sit and where the code beside them moves. A *summary*
> sits on a name, which does not move under it: 62 % of the 4,838 run longer than two lines, `R10`
> makes a test class's the canonical statement of what that class is for, and the longest of them
> carry an experiment's controls. Sorting summaries by length finds the careful ones.

*Applies to:* every comment under `Data/Scripts` and `tests`, excluding vendored code.
*Checked by:* **the compiler first, and a test for what it cannot see.**

The compiler resolves every `<see cref>`, `<param>` and `<paramref>` in this tree, because
`GenerateDocumentationFile` is on and `CS1574`, `CS1580`, `CS1581`, `CS1584`, `CS1710`, `CS1572`,
`CS1734`, `CS1587` and `CS1570` are errors. That is name binding, with generics, overloads and
inherited members, which no test here could do without reimplementing it; it had simply never been
asked. Twenty-five failures were in the tree when it was, four of them in the shipped mod — a
`[ProtoMember]` whose comment described a deleted field, a method's parameters documented onto the
constant that had been inserted above it, a `<paramref>` on a class. `CS1573` and `CS0419` are
deliberately not errors: a missing param tag and an ambiguous overload reference are coverage and
precision, not a name that resolves to nothing. `CS1570` and `CS1572` are errors everywhere except
[the mod project](../Thermodynamics/Thermodynamics.csproj), which is the only project that compiles the three vendored
paths and where `R6` forbids fixing the five they carry; `.editorconfig` cannot narrow it, because
`WarningsAsErrors` overrides those severities, which was tried and measured.

And `NoDocCommentDescribesSomethingThatIsNotThere` for the orphan the compiler accepts, which fails
on **two signatures, and they are different faults**. *Closed then reopened* — a summary closed on one line and another
opened on the next — is a member that has gone, leaving its comment on the one below. *Opened
twice* — a second summary opened before the first is closed — is a member **inserted into the middle
of somebody else's comment**, which breaks two comments rather than one: the tail of the first now
hangs under the newcomer and describes the member after it. Either way C# allows one summary per
member, so neither shape can be innocent. It states rather than hides its limit: an orphan landing
somewhere with no comment of its own is invisible to it — though `CS1587` now takes the case where
it lands on nothing at all, which is how two of the twenty-five were found. Nothing checks the
length, which is judgement.
*From:* [document-of-intent.md](document-of-intent.md#what-a-code-comment-is-for).

#### R15 — An identifier cited anywhere resolves to something that exists

**A rule or a backlog row cited in a comment, a test summary or a script is one that is still on the
page it belongs to.**

`R11` is this rule pointed the other way — it fails when a rule names a check that has stopped
running — and between them there was a hole big enough to hide in. `EveryRuleCitedByAPageExists`
reads documentation banners; **425 citations of the same shape live in `.cs` and `.py` files** and
nothing read those at all. A backlog row is deleted when it closes, so every comment citing it
becomes a dead reference that reads exactly like a live one, and the check found thirteen on its
first run: `C20` in six files including three the game compiles, and `C13` in three more.

*Applies to:* every `.cs` and `.py` file outside the vendored paths.
*Checked by:* `EveryCitedIdentifierResolves`. It cannot say *which* page a citation means, because
the two share a namespace — [backlog.md](backlog.md) carries that as H8 — and it says instead that
the citation resolves to one of them, which is what stops a dropped identifier rotting in a comment.
*From:* this page, and the pass that added it.

#### R16 — A pointer in code is plain text, never a link

**A comment that names a page writes the page's name — `See stiffness.md, A per-block substep
cap.` — and never a relative markdown link.**

A link inside a `.cs` file **renders nowhere**. Nobody clicks it, so nobody finds out it is wrong,
and `EveryRelativeLinkResolves` reads markdown only — so the one form of cross-reference here that
nothing checked was the one written in the syntax that looks checked. `Settings.cs` carried
`[backlog.md](backlog.md)`, a relative path from `Data/Scripts/Thermodynamics` to a file four
directories above it.

The convention was written down in [development.md](development.md#and-in-the-code) after two such
links were found rotted, one into a directory that does not exist. **155 more had accumulated since,
across 91 files**, which is what an unchecked convention does. Flattening them cost nothing, because
every link text was already the page's own name.

*Applies to:* every `.cs` file outside the vendored paths.
*Checked by:* `NoPointerInCodeIsWrittenAsALink`.
*From:* [development.md](development.md#and-in-the-code).

### P6 — A comparison holds everything but the subject equal

#### M2 — Parallel for values, linear for durations

**A settling temperature is a pure function of a ship and a scenario, so it may be measured
alongside thirty others. A duration may not.**

The moment a result is a time, every other core is noise — cache pressure, memory bandwidth,
turbo headroom, the scheduler. A balance figure taken linearly is the same figure; a cost figure
taken in parallel is a different one, and nothing in the number says which it is.

*Applies to:* every lab and benchmark run.
*Checked by:* `ParallelAndLinearProduceTheSameMatrix`, which fails when shared state returns.
*From:* [balance-lab.md](balance-lab.md).

#### M7 — Two figures are comparable only if they were taken in one window

**Any two timings put in a ratio are taken minutes apart on one held machine — which for a pass
means a report at its starting commit and one at its tip, and for an A/B means the two legs
alternated inside a single window.**

The committed baseline is pinned at an old commit, so a diff against it spans every commit since
and attributes all of them to whoever ran it. That was the whole of this rule until the session
became measurable. **It is now the smaller half.** The same exposure code, the same machine, the
same command, two days apart: a stage median of 11.40 ms and one of 4.75, with the source, the
instrument, the build configuration and the stage order each eliminated in turn. Nothing inside a
window can see that — every run of a session is on the same side of it — so it is not a thing more
repeats or more processes will fix, and a *before* and an *after* quoted from two days' tables is
not a measurement however carefully each was taken. Pass 4 saw the same effect at thirty per cent
on a commit that was the *after* leg of one iteration and the *before* leg of the next, and left it
as a caution in prose; five passes on it is a factor of two and it has been silently re-deciding
which statistic a stage is read with, which is why it is a rule with an artefact behind it now.

The corollary is the useful one: a ratio taken as a pair inside one window is unharmed, because
whatever the session is doing is doing it to both legs. Alternate the legs rather than blocking
them, so drift within the window is common to both as well, and carry an untouched stage as a
control.

**And the control has to be somewhere the change cannot reach, which is narrower than it sounds.**
A stage that runs *after* the changed stage in the same process is not a control: settling between
stages reclaims the garbage and leaves the heap it was allocated into, so a stage carries its
predecessors' allocation history. Measured — the room pass reads 4.5 % apart between two binaries
when two stages ran before it, and **0.3 % apart when it runs alone**, and what differed between the
binaries was how much the first of those stages allocates. Give each stage its own process
(`bench stages --isolate`), or put the control before the change.

*Applies to:* any two timings put in a ratio — a pass against its start, an A/B's two legs, a
figure on a page against a figure on another.
*Checked by:* `StageLabTests.ARowSurvivesBeingWrittenAndReadBack` and
`AnArtefactMissingAColumnIsRefusedRatherThanMisread` for the isolation the second half needs, and
`SampleStatisticTests` — `RunsAreOneWindowOnlyIfTheyWereTakenInsideOne`,
`ARunWithNoReadableStampIsNotOneWindowWithAnything` and
`TheStampIsWrittenToTheArtefactAndReadBackFromIt`. Every artefact `bench stages` writes carries the
stamp those read, and `bench samplestats` says above its table whether the runs it is comparing are
one window, how far apart they were taken, or that they are unstamped. Whether a human then compares
two windowed figures anyway is procedure.
*From:* [benchmarks.md](benchmarks.md#the-iteration-log),
[performance.md](performance.md#pass-9-iteration-6--the-one-write-exposure-change-and-the-session-it-was-measured-in).

### P7 — The game is the authority

Breaking one of these does not fail a test here. It fails at world load, or in someone else's
session. The first three are the compiler and the whitelist; the last two are the game answering a
question this mod could answer for itself and should not.

#### C1 — C# 6 only

**No tuples, no `switch` expressions, no `out var`, no string interpolation beyond what C# 6
supports.**

The game compiles the mod's sources itself at world load with its own compiler. They live under
`Thermodynamics/` here, and the MDK2 packer ships them as `Data/Scripts`.

*Applies to:* every `.cs` file under `Thermodynamics/`. The test projects link the same files, so
the constraint is theirs too.
*Checked by:* `LangVersion` is pinned to 6 on the core project.
*From:* [development.md](development.md).

#### C2 — The whitelist covers types the local build accepts

**A green build and a green suite are necessary and not sufficient.**

Space Engineers applies a type whitelist that neither the local build nor the tests can see.
`IndexOutOfRangeException` and `ArgumentOutOfRangeException` are both prohibited in game and both
compile happily here; `Exception`, `ArgumentException`, `ArgumentNullException`,
`InvalidOperationException` and `FormatException` are confirmed allowed. Prefer bounds-checking
to catching a throw. No reflection, no file I/O outside `MyAPIGateway.Utilities`, no threading.

**Since the MDK2 move the mod build runs a whitelist analyzer**, which reports a prohibited type or
member as `MDK01`, an error; it was proven on 2026-09-24 against `System.IO.File` and
`IndexOutOfRangeException` before being cited here. It reads MDK2's own snapshot of the whitelist
rather than the installed game's, so a game update can move ahead of it. The game-whitelist probe in
`tools/thermal-api-check` asks the installed game directly, for the snippets it holds.

*Applies to:* everything the game compiles.
*Checked by:* the build — `Thermodynamics/Thermodynamics.csproj`, through MDK2's analyzer.
*From:* [development.md](development.md), [tests/README.md](../tests/README.md).

#### C3 — Target `net48`, and never reference the native assembly

**`VRage.Platform.Windows` and its dependencies are built against .NET Framework 4.8 and silently
fail to resolve at 4.7.2. `VRage.Native.dll` is unmanaged and produces `MSB3246`.**

*Applies to:* the compile-check project and the test projects.
*Checked by:* the build.
*From:* [development.md](development.md).

#### C11 — Every file the game compiles is compiled by the suite's own build

**Every `.cs` file under `Thermodynamics/` is what the game compiles at world load, and the one
command the workflow already runs — `dotnet build tests/Thermodynamics.slnx` — has to compile all
of it.**

The test projects link `Core/` and `Presentation/` in full and a named handful of `Game/` and
`Telemetry/` files, and nothing else. The rest of the adapter is every file that touches the game's
own assemblies, and it was once compiled by a mod project that was built by hand. So a rename in `Core` could pass the
whole suite and leave the mod unable to load — and did: `C20` renamed `Conductivity` to
`HeatTransferCoefficient`, left one copy of the old name in `ThermalBlockCatalog`, and the mod did
not compile for two commits while the whole suite passed.

**It is the rung below `C2`.** There, a green build is not the game's check; here, a green *suite*
was not even the local build.

*Applies to:* every `.cs` file under `Thermodynamics/`.
*Checked by:* the build — `Thermodynamics/Thermodynamics.csproj`, the MDK2 mod project, is a
member of `tests/Thermodynamics.slnx`, so the solution build fails on a break the suite cannot see.
*From:* [development.md](development.md#building), and the commit that fixed the break above.

#### C9 — The game's own answer is read, never overridden

**Where the game already decides something — whether a room is sealed, how much oxygen it holds —
this model reads that answer rather than forming its own, and every source that *answers* may veto
air while none may require it. Silence is not an answer.**

Three things can empty a room: a world with oxygen or pressurisation off, the game's own sealing
test, and a reported level. Each is a veto and none is a requirement. **The asymmetry is because
the game owns pressurisation and this model has no standing to overrule it** — every veto is
somebody saying no, and there is nothing for a requirement to be built out of. The models disagree
by construction, because this model's rooms are pieces of the game's and its cells are coarser than
a sloped block, so what matters is the direction of a disagreement rather than its existence.

> **This rule used to justify itself by the size of the two errors, and that was measured false**
> (`P3`, `C22`, `F21`). It said air is heat capacity, so a room wrongly *given* air drags every
> bounding surface along while a room wrongly *denied* it loses only some interior inertia. A
> link's conductance carries no pressure term, so room air is a **mixer rather than a sink**: on a
> settled 2,000-block census hull the hottest block reads 1,502.83 K at every pressure from 0.2 to
> 1.0 and **1,706.08 K** with the air gone, while the hull *mean* moves 3.8 K. The two errors are
> the same size — about 203 K, on the block overheat damage is taken off — and they differ only in
> sign. The rule survives on its first reason; what did not survive was extending it to the case
> where **nothing answered at all**, which was reading a lookup that missed as a fourth veto.

*Applies to:* room pressure, air density, and anything else the game already answers.
*Checked by:* `RoomPressureTests` — one case per veto — and `RoomAirPressureTests`, which reports
the one direction that is a fault: air in the game and none here.
*Also settles:* whether this mod should author its own air densities, since seven of the eight
shipped worlds have density exactly 1 and nothing derived from it distinguishes them. It should not:
a density this model invented would disagree with the oxygen system, the wind ceiling and the
jetpack at once. See [environment.md](environment.md#its-inputs).
*From:* [document-of-intent.md](document-of-intent.md#what-this-mod-deliberately-is-not),
[thermal-model.md](thermal-model.md), and `RoomPressure`'s own summary.

#### C10 — The server is authoritative over damage

**Clients run the same simulation from the same inputs and never apply what it concludes.**

Temperatures are deliberately not reconciled — the mod sends as little as it can and tolerates
drift — which makes damage the one conclusion that must not be reached twice. A client that
applied its own overheat damage would not throw, would not desync visibly, and would destroy blocks
a server that disagreed by a degree was keeping.

*Applies to:* overheat damage, and any future conclusion with a world-visible consequence.
*Checked by:* — nothing. The guard is one `IsServer` early-out in
`ThermalGridSimulation.ApplyOverheatDamage`, and the suite runs no session.
*Retires when:* nothing retires it. How far a client may drift before it is corrected is still
undecided, and that question does not touch this rule.
*From:* [document-of-intent.md](document-of-intent.md#what-the-mod-owes-a-multiplayer-client),
[known-issues.md](known-issues.md).

#### W3 — An authority check reads what the engine supplies, never what the sender wrote

**Whether a message may do something is decided from a value the engine filled in, and never from
one carried in the payload.**

`SENetworkAPI`'s sender id is a field the sender writes, so a promote-level check gated on it is a
check a client can pass by asserting that it should. The engine's own secure handler supplies the
sender and a from-the-server flag that a client cannot set, which is why the settings-request path
and the temperature channel are on it and the ordinary state channel is not. Same idea one step
along: a client must not be able to write temperatures onto another client's simulation, and the
from-the-server flag is the only thing that says a packet is the server's.

*Applies to:* every check that decides whether a received message may change something.
*Checked by:* — nothing; the paths it guards exist only in a session.
*From:* [architecture.md](architecture.md#networking),
[document-of-intent.md](document-of-intent.md#to-a-client-the-server-trusts-nothing-the-client-asserts-about-itself).

### P8 — Off means off, and costs nothing

#### C4 — Nothing allocates on the stepping path

**Anything allocating per frame shows up in the report, and a settled step is asserted to allocate
nothing at all.**

The raycast result lists and the grid list are pooled and the `kA` arrays cached for this reason.

*Applies to:* the solver, the environment pass and everything a step reaches.
*Checked by:* `StageLabTests`, which reads `GC.GetTotalAllocatedBytes` around a settled step of a
census hull and fails above four kilobytes — it was measured rather than asserted until 2026-08-27,
which for a rule about a quantity that should be *zero* is a gap a report cannot close. `bench
report` still carries the per-feature figures.
*From:* [development.md](development.md).

#### C7 — Every mechanism has a switch that removes its own cost

**Switching a feature off removes exactly its own cost, on the next step, with no reload.**

A readout, diagnostic or overlay that is off costs nothing. This is what makes the feature table
in the benchmark report readable and what lets a server operator pay only for what they use.

*Applies to:* every mechanism, readout and diagnostic.
*Checked by:* `FeatureToggleTests` and the benchmark feature table for what a switch *costs*;
`ConfigurationDocTests.EveryMechanismInTheLadderInventoryHasASwitchOrSaysItHasNoLadder` for whether
one exists at all. The second is there because the first cannot find a mechanism that has no switch
to test — wind had none for months, with a row in the inventory saying so, and every check passed.
*From:* [README.md](../README.md), [configuration.md](configuration.md).

#### C15 — A feature's configuration runs from `off` to `realistic`

**A mechanism is configured as a list of options whose two ends are `off` and `realistic`;
`realistic` is the most faithful thing the model can do and is the default, and every rung between
them carries the price of what it gives up.**

`C7` makes a feature's cost removable. This says removing it should not be the only alternative on
offer: a player whose machine cannot afford the faithful model, given nothing but a boolean, turns
the feature off — and a feature switched off is not in the game. A two-rung ladder is a complete
ladder where no cheaper form is worth having; what the rule forbids is a cheaper form that exists in
the model and is not on the list. `WellMixedCoolant` was one for as long as it existed: read by the
solver, exercised by the suite, documented in two pages as a choice a world makes, and settable by
nothing.

It also fixes the *direction*, which is the half a boolean cannot get wrong and a dial can. Four
integration dials currently run three ways — `MaxSubstepsPerBlock` 0 is the faithful end,
`MaxSubsteps` up is the faithful end, `MaxElementVisitsPerStep` 0 means uncapped — so a reader has
to be told, per setting, which way is which.

*Applies to:* every mechanism a world can switch. Not to physical constants, balance dials or
diagnostics: a diagnostic's ladder is `C7`'s off-unless-read.
*Checked by:* `EveryCoreSettingIsReachableFromAWorldsConfiguration` holds the operative half — a
rung the solver reads and no world can set fails it. `EveryMechanismSwitchIsClassifiedInTheLadderInventory`
holds that every mechanism says what rungs it has. Whether a missing rung is worth building is
judgement, and [configuration.md](configuration.md#every-mechanism-and-the-rungs-it-has) records the
answer per feature.
*From:* [document-of-intent.md](document-of-intent.md#a-switch-is-a-ladder-and-its-two-ends-are-off-and-realistic).

#### C8 — Absent and empty mean the same thing

**A gate read from the environment or from configuration treats an empty value as absent, so the
obvious way to write "off" is off.**

`THERMAL_CORPUS_TESTS` gates the opt-in corpus walks, and both of its guards test the variable
against `null`. On Linux an exported variable with an empty value is not null, so
`THERMAL_CORPUS_TESTS= dotnet test` — the obvious way to spell *not this time* — enables every
corpus walk instead, and the failure mode is a suite that appears to hang. The general form is the
rule, because a gate whose off position is unreachable by the obvious spelling will eventually be
switched on by somebody trying to switch it off.

*Applies to:* every environment variable and configuration value used as a gate.
*Checked by:* `CorpusGuardTests.OffMeansOffHoweverItIsSpelled`, over every spelling of off anyone
reaches for. `CorpusFixture.OptedIn` is the one decision and `LabInvariantTests` calls it rather
than repeating it.
*From:* [backlog.md](backlog.md) H3, generalised.

### P9 — The core is a library the game happens to call

A library is used by code its author never sees, which is two obligations rather than one: it must
be *reachable* without dragging the session in, and it must be *safe to call* — a library that
throws into its caller has moved its own failure into somebody else's program.

#### C5 — The core speaks no game type

**The simulation references exactly one Space Engineers assembly, `VRage.Math`, and no public
surface in it takes or returns a game type.**

That is what lets the model be built, tested and profiled in seconds instead of by loading a
world, and it is the boundary an SE2 adapter would bind to.

*Applies to:* `Thermodynamics/Core`.
*Checked by:* `CoreIsolationTests`.
*From:* [tests/README.md](../tests/README.md), [architecture.md](architecture.md).

#### W4 — No call across the mod's API throws into its caller

**A bad argument comes back as `false`, `0` or `NaN`, and a subscriber that throws is dropped rather
than allowed to stop the simulation.**

An exception crossing a mod boundary lands in somebody else's session with this mod's name on it,
and the caller cannot catch what it did not know it was calling. The same reasoning runs the other
way for the callbacks this mod invokes: one misbehaving consumer of a threshold must not stop heat
moving for everything else in the world, so `RaiseThreshold` catches, records and unsubscribes.

**Discipline was the check until 2026-08-28, and it had already missed one.** `GetSetting` was
`name => Settings.Instance.GetValue(name)`, four lines above a `SetSetting` that guarded the same
field and returned `false` — so the writer was safe to call before this mod had loaded its settings
and the reader threw a `NullReferenceException` into whoever asked. Nothing would have reported it:
an exception crossing a mod boundary lands in somebody else's session. **Every entry goes through
`ThermalApi.Guard` now**, which returns the type's default and records the failure through the same
`Telemetry.Exception` that `RaiseThreshold` uses, so the property is structural rather than a
promise about eighteen bodies. `GetSetting` itself reads through `Settings.EnsureLoaded`, which is
the method written for callers who cannot control load order.

*Applies to:* every delegate in the table, and every callback the mod invokes.
*Checked by:* `ModApiShapeTests.EveryEntryInTheTableIsWrappedSoItCannotThrowIntoItsCaller`, which
reads the source because the table binds game types no test project references. It checks that
everything is inside a `Guard`, not that each body is careful — one thing to see, and it cannot be
true of seventeen entries and false of the eighteenth without saying which.
*From:* [api.md](api.md#guarantees).

### P10 — The solver's three invariants are the definition of correctness

#### C6 — The solver's three invariants hold

**Order independence, energy conservation, and boundedness.**

Every exchange reads the temperatures at the start of the substep and writes into an accumulator,
so iteration order cannot change the result; every internal exchange is applied equally and
oppositely; and with the overshoot clamp on, no pairwise exchange can carry more than the energy
that brings the pair to equilibrium. A change that breaks the first also removes the only reason
the pass could ever be parallelised.

*Applies to:* the solver.
*Checked by:* `ConductionTests` and `StabilityTests` — both in `PhysicsTests.cs`, which is a file
and not a class — and `ConductionClampGateTests`.
*From:* [thermal-model.md](thermal-model.md).

### P11 — The mod folder is the publish

MDK2 packs the mod project's folder, `Thermodynamics/`, into the game's local `Mods/Thermodynamics`
on every build — the sources as `Data/Scripts`, the content folders beside them — and that folder
is what a workshop publish uploads. So everything under `Thermodynamics/` that `mdk.ini` does not
ignore ships, and nothing outside it does.

#### R2 — Build output and the corpus live outside it

**So that publishing needs no cleanup step, because a cleanup step that has to be remembered is
one that eventually is not.**

The mod project's own `bin/` and `obj/` sit inside `Thermodynamics/` and are left out by
`mdk.ini`'s `ignores`; every other project builds under `tests/` or `tools/`, outside the packed
folder. The blueprint corpus lives in the user data directory and is overridable by
`THERMAL_CORPUS`. They were 394 MB and over a hundred gigabytes respectively. A blame hang dump, at
1.9 GB, was one commit from shipping before `TestResults/` was ignored.

*Applies to:* anything generated or fetched.
*Checked by:* `mdk.ini`'s `ignores` for the mod project's output; `.gitignore` and the corpus path
for the rest. Neither stops a new generated directory from appearing under `Thermodynamics/`.
*From:* [development.md](development.md).

#### R3 — No credential is written into the tree

**A Steam Web API key is taken from the command line or the environment, redacted from anything
printed, and never written to a file. SteamCMD is invoked with a user name and no password.**

Anything else would mean this program handling a password, and it has no business doing that.

*Applies to:* the corpus fetcher and anything else that talks to an external service.
*Checked by:* `CredentialScanTests`, which scans every file a person writes for a private key
block, a provider-prefixed token, a credential assigned a literal, and a Steam key beside a word
saying it is one — and whose second case checks the scan fires on each of those and on none of the
things this repository legitimately writes, `--key <key>` among them.
*From:* [development.md](development.md), [balance-lab.md](balance-lab.md).

### P12 — Do not edit what this repository cannot regenerate

#### R4 — `Models/` is not restructured

**LOD and build-stage model paths are baked into the `.mwm` binaries at export time.**

Moving or renaming a folder under `Models/` silently breaks LOD switching in game, and the only
fix is re-exporting the models — which needs the source scene, not this repository.

*Applies to:* `Thermodynamics/Content/Models/` and everything under it.
*Checked by:* `ShippedIdentityTests.TheModelTreeKeepsItsShape`, which pins a digest of the sorted
set of model paths — a **set** rather than a count, because a file added is a normal day and a file
*moved* is the failure, and only the set tells those apart. `Models/note.txt` says so as well.
*From:* [development.md](development.md).

#### R6 — Vendored code is replaced, never edited

**Rich HUD Framework, the Definition Extensions client and SENetworkAPI are replaced wholesale
when their authors publish a new version.**

An edit here is lost at the next update, and silently: the replacement compiles.

*Applies to:* the three vendored paths.
*Checked by:* — judgement.
*From:* [development.md](development.md).

### P13 — A long run is designed for its own death

Every rule here was bought with a lost day.

#### O1 — Every long run is capped

**Wrap `dotnet test`, a benchmark or a sweep in a cgroup with a hard memory ceiling.**

```bash
systemd-run --user --scope -p MemoryMax=24G -p MemorySwapMax=0 --quiet dotnet test ...
```

An uncapped validation run grew to about 80 GB resident on a 91 GB machine and tripped the global
OOM killer, which took the editor down with it and lost uncommitted work. A cap turns that into
one dead process and a clear signal. Choose the ceiling above the run's measured working set
rather than by habit, and sample resident size during a long run rather than learning the peak
from a kernel log. Several capped runs at once still sum on one box.

*Applies to:* anything that runs for minutes or reads the corpus.
*Checked by:* — procedure.
*From:* the operations record kept with the session notes.

#### O3 — Long sweeps resume, and progress is measured in bytes

**Corpus order is deterministic and data is written per ship, so a killed run resumes by skipping
what is already on disk — which turned a lost eight hours into a seventeen-minute finish.**

Progress must be counted in bytes rather than files: the walk is largest-first, so file 500 of
9,981 is 5 % of the files and 50 % of the work. A kill pattern must not match the relaunch that
has just started.

**The resume is a record of finished blueprints, not a count** (corrected 2026-08-25). This rule
carried a retirement condition saying it *currently* counted files — an interrupted batch re-emitted,
the shipped dataset 50 duplicate rows — and that had already been fixed: `CorpusFixture` writes a
path to `done-<walk>.txt` only once every ship in it has been recorded, so a path present there has
nothing left to do. The 2026-08-21 dataset still carries its 50 rows and nothing collected since
can, which is why `verdict.py` still drops duplicates and prints the count: it is a **guard** now
rather than a workaround, and the count is also how a reader learns which kind of dataset they are
holding.

*Applies to:* every corpus sweep.
*Checked by:* — procedure.
*From:* the operations record, [balance.md](balance.md).

#### O5 — A lab streams, and counts what it did not measure

**A parsed ship holds every grid and every block, so a corpus read in one pass is what takes the
machine down. Batch it, and report the count of what was skipped rather than dropping it.**

The two halves are one rule: batching is what keeps the run alive, and counting the skips is what
keeps the result honest about the population it actually covered — `P1` and `P2` applied to a run
that had to be cut down to fit in memory.

*Applies to:* every lab that reads the corpus.
*Checked by:* — the labs do this; nothing enforces it.
*From:* [balance-lab.md](balance-lab.md).

### P14 — Unobservable fidelity is cost

#### D6 — A deliberate simplification is recorded as a limit

**A shortcut taken on purpose is written into the limits list, with what it costs, so nobody
rediscovers it as a bug.**

Build state not changing a block's thermal properties, a surface described by two constants
rather than a spectrum, point sources not occluded, a room with no vent holding no air: each is a
decision, and each is recorded with the machinery that would undo it. This is the operative half of P14 — the shortcut
is free to take and not free to leave unwritten.

*Applies to:* every decision to approximate.
*Checked by:* — judgement.
*From:* [known-issues.md](known-issues.md#deliberate-limits).

#### M8 — A scenario earns its place by answering what nothing else answers

**Nothing is in the battery because it seems interesting; where two scenarios answer the same
question, the cheaper is kept, and directional cases are enumerated rather than sampled.**

`idle` and `vacuum-shadow` were the same simulation — 27 ships ran under both and differed by
0.000 K — and one was cut. `all-peak` was cut from the corpus for the opposite reason: it puts
96 % of ships over critical, and a melting grid never settles, so it starves the invariant it is
run for. A ship is not symmetric, so both thrust and travel expand to six directions.

*Applies to:* the scenario battery.
*Checked by:* — judgement.
*From:* [balance-lab.md](balance-lab.md).

---

#### O4 — Corpus walks run alone

**A test that walks the corpus, or that asserts on wall-clock time, declares the collection that
disables parallelism; nothing else in the suite does.**

Four walks across thirty-one workers on thirty-two cores measured seventeen times slower than
running them one at a time, and it hid behind a 93 % CPU reading because the cores were busy
thrashing each other's cache. Each walk is already internally parallel over thousands of
blueprints, so two at once are two thread pools competing for one memory bus.

**The rule was right and its implementation was three times too broad.** `xunit.runner.json` set
`maxParallelThreads: 1` for the whole project, so every run of every test paid for the isolation of
four opt-in walks that most runs never execute. Measured on 2026-08-24 over 1,825 cases: **1 m 41 s
at one worker, 38 s at eight, 1 m 47 s at thirty-two** — one worker per core is no faster than
serial, which is the same cache effect one rung up. The walks and the wall-clock tests now declare
`[Collection("alone")]` and the project runs at eight.

*Applies to:* the opt-in corpus walks, and any test whose assertion is about elapsed time.
*Checked by:* `EveryCorpusWalkDeclaresThatItRunsAlone`, which finds a walk by the one thing every
walk does — ask `CorpusFixture` for its files. The wall-clock half is judgement: `StaggerTests`
compares two cache regimes a few per cent apart, and its own noise guard is what caught it.
*Retires when:* the walks stop being internally parallel, or a machine stops having a shared cache.
*From:* the operations record, and [backlog.md](backlog.md) `F8`.

**Why absolute.** A project-wide setting cannot be forgotten and an attribute on a class can:
a new walk written without it does not fail, it runs beside another walk and takes the suite's
duration with it. That is a silent failure, which is what this page is for.

### P15 — What is already in someone else's world is frozen

A save file, a setting name, a definition property name, an API key and a workshop id all exist in
worlds this repository will never see and cannot reach. They may be **added to**; they may not be
repurposed or quietly dropped. That is a different idea from P12 — P12 says do not edit what you
cannot rebuild, and this says do not break what you cannot recall — and it is why `R5` sits here
rather than there: a workshop id is not hard to regenerate, it is impossible to un-publish.

#### W1 — A saved world loads on the build that wrote it, and on the ones either side

**The storage format grows by adding a section rather than by changing its marker, and every older
format it has ever written is still read.**

A save that fails to load is a world's heat lost; a save that loads *partially* is worse, because
nothing says so. Version 2 grew a room-air section and a reader that predates it skips what it does
not recognise, so a save written now still loads on an older build — and a version 1 payload still
decodes on this one. The same shape holds for anything else written into world storage: append, and
read what you used to write.

*Applies to:* `ThermalStorageCodec` and anything written into `MyModStorageComponent`.
*Checked by:* `LegacyPayloadsStillLoad`, `AReaderThatDoesNotKnowAboutRoomsStillReadsBlocksAndLoops`,
`APayloadWithNoRoomSectionDecodesToNoRooms`, `EveryTruncationOfAValidPayloadIsRejected`.
*From:* [architecture.md](architecture.md#persistence).

#### W2 — A name something outside this repository addresses is never repurposed

**A setting name, a serialization number, a definition property name and an API key are addresses
held by worlds and mods that will never be re-read here. Alias them, add to them, and never point
one at something else.**

The failure is silent in the worst way available. Definition Extensions matches on the property
name string, so dropping a retired spelling reverts every third-party definition written before the
rename to the shipped defaults — no error, no log line, and a mod whose blocks have stopped being
what they say they are. A reused `ProtoMember` number decodes an old save's value into the wrong
field. A renamed setting breaks `/thermal set`, the API and the settings sync at once, for
everybody who had tuned it.

*Applies to:* setting names and their `ProtoMember` numbers, `<ModExtensions>` property names, and
the mod API's keys and signatures.
*Checked by:* `TheRetiredPropertyNamesAreStillRead` (the harness parser only, and it says so),
`NoSettingReusesANumberThatWasDeliberatelyRetired`, `EveryNamedSettingCanBeReadAndWritten`,
`EveryEntryHasTheSignatureTheApiPageGivesIt`.
*From:* [definitions.md](definitions.md#retired-property-names),
[document-of-intent.md](document-of-intent.md#to-an-existing-world-nothing-a-player-already-has-is-quietly-lost).

#### R5 — The workshop identity files are not regenerated

**`modinfo.sbmi` holds the workshop id; regenerating it publishes the mod as a new item, and
every subscriber stays on the old one.**

*Applies to:* `modinfo.sbmi`, `metadata.mod`.
*Checked by:* `ShippedIdentityTests`, which pins the workshop id and the owner and asserts the
second file is still there. Changing the pinned id is the deliberate act, and it is one line in a
diff.
*From:* [development.md](development.md).

## Conditional

Seven rules hold only inside a stated scope. Every one of them was first written as an absolute,
and written that way each forbids work that is ordinary and correct. The scope is the rule.

### D7 — Measure before replacing what the counts accuse

**A subsystem suspected on aggregate counts is compared against ground truth before it is
changed.**

More than one thing here has looked guilty from the counts alone and been innocent. The room map
is the standing example: it was suspected of losing compartments, on a count of rooms that looked
too low, and when the comparison was finally run it was right about twelve of twelve. A rewrite
would have been judged against nothing, would have passed, and would have replaced working code
with a change nobody could argue about afterwards.

The counts are worth trusting as a place to look and not as a verdict, which is `D1` seen from the
other side: `D1` is disbelieving a number that looks right, this is disbelieving one that looks
wrong.

*Applies to:* a suspicion raised by aggregates rather than by a reproduction.
*Checked by:* — judgement.
*From:* [document-of-intent.md](document-of-intent.md#what-correctness-means), one of two standing
instructions from the developer; [known-issues.md](known-issues.md).

**Why conditional.** As an absolute it forbids ordinary correct work: a crash with a stack trace, a
dead link, a typo in a label are all fixed on sight, and nothing is bought by measuring first. The
rule bites exactly where the evidence is a count and the proposed fix is a replacement.

### E2 — Measure the population, not the specimen

**A claim about what players build is measured against the workshop corpus in the lab. A figure
from a running session is one sample and is labelled as one.**

One ship is one ship. The tiers every benchmark rests on came from a single 1,381-block hull,
and the corpus later showed that hull to be a 96th-percentile ship for heat. A session's figure
also cannot be re-examined: the world is gone and what else was true of it is unrecorded.

*Applies to:* population, balance and cost claims. It does **not** forbid in-game measurement —
several things are only observable in a session, including the heat pump's electrical hookup,
the whitelist's verdict on a type, the settings menu's multiplayer path, and the cost of terrain
occlusion. Those are recorded as in-game observations, and a lab claim is not built on them.
*Checked by:* `CensusFidelityTests`, which fails when the synthetic hull drifts from the field
observations recorded beside it.
*From:* [balance-lab.md](balance-lab.md), [known-issues.md](known-issues.md).

**Why conditional.** The short form — *do not trust in-game numbers* — would retire three test
suites that check the mod against a world, and would forbid measuring the several properties that
have no other observer.

### E6 — Pair the terms before dividing

**A ratio is formed from two measurements of the same thing. Two extrema drawn from different
members of a population do not form one.**

A hull's air peak divided by its vacuum peak was published as sensitivity to air, at 1.02 against
a real 1.73. They are different blocks — the air peak an exposed fitting, the vacuum peak a
buried heavy block — so the quotient described no block at all. Asked per block, the same hull is
ordinary. Take the aggregate of the per-unit ratio; before changing a shared fixture on a derived
number, read the underlying terms once by hand.

*Applies to:* any quotient of aggregates, and any figure derived from one.
*Checked by:* — judgement.
*From:* [benchmarks.md](benchmarks.md), and the correction in commit `46ba54f`.

**Why conditional.** A ratio of two aggregates over the *same* subject — total watts vented over
total watts made, for one grid — is exactly the right figure, and the absolute form of this
sentence bans it.

### M1 — Compare only runs that were stopped the same way

**A quantity read at the end of an adaptively-stopped run is comparable only to one from a run
that stopped on the same criterion at the same point.**

Runs stop when the hottest block flattens, so two ships warming from the same start are sampled
at different points on their curves. One ship in five appeared to violate *sunlight never cools*
that way, with the sunlit run's bulk still falling at megawatts. The fix is a cold start at the
vacuum floor on equal fixed clocks.

*Applies to:* quantities read at the end of a run.
*Checked by:* `SunlightPanelWalk` runs its comparison on fixed clocks.
*From:* [balance-lab.md](balance-lab.md).

**Why conditional.** It does not apply to quantities decided before the stop — crossing times,
shares, seconds-to-critical — which are comparable across runs that stopped differently. Most of
the corpus findings rest on exactly those.

### M3 — The run is the unit of parallelism

**Two scenarios on one ship must not run at once.**

Every simulation built from a ship shares that ship's `BlockInstance` objects, and the load is
written onto them, so concurrent runs overwrite each other — which does not throw and does not
look wrong in a report. Each job reads the blueprint again for grid state of its own; block
models stay cached and shared.

*Applies to:* the harness's own scheduling.
*Checked by:* `ParallelAndLinearProduceTheSameMatrix`.
*Retires when:* the load is applied without mutating the block instance. This is a constraint
imposed by mutable shared state, not by physics, and it is the only rule on this page that
describes an implementation rather than a fact about the world.
*From:* [balance-lab.md](balance-lab.md).

**Why conditional.** Outside the harness's scheduler it says nothing, and inside it, it stops
being true the moment the mutation does.

### O2 — A run with no bounded duration gets no hang timeout

**`--blame-hang-timeout` kills a healthy run at exactly the timeout, and the test runner cannot
tell slow from deadlocked.**

It ended a corpus survey at eight hours. On an ordinary suite run, where every test has a
bounded duration, `--blame-crash --blame-hang --blame-hang-timeout` is worth having, because a
kill then names the test that was in progress.

*Applies to:* the corpus walks and any single test that legitimately runs for hours. It does not
apply to the ordinary suite.
*Checked by:* — procedure.
*From:* the operations record. This resolves a direct contradiction between two standing notes,
one requiring the flag and one forbidding it; the scope above is the reconciliation.

**Why conditional.** Both absolute forms are wrong, and both were written down. The flag is
correct for the suite and destructive for a sweep.

### J3 — The corpus filters are strict, and their cost is recorded

**One unresolved subtype disqualifies a ship, and ships under 25 blocks are fragments.**

There is no way to tell whether a block that failed to resolve was a decorative panel or the
reactor, so a softer rule would mean the corpus measures *nearly* vanilla balance. The cost is
real and is recorded rather than hidden: no corpus ship carries one of this mod's cooling blocks,
which is why the cooling criterion had to be answered by a retrofit lab instead of by the survey.

*Applies to:* corpus acquisition and screening, while the question is vanilla balance.
*Checked by:* `BlueprintTests` covers the yield.
*Retires when:* the yield falls to where the survivors stop being a population — currently about
four in five, so not soon. The 25-block floor is an unexamined constant that has never been varied
to see whether it changes a population figure.
*From:* [balance-lab.md](balance-lab.md).

**Why conditional.** The retrofit lab deliberately adds this mod's blocks to corpus ships. Applied
there, the filter would reject its own input.

---

## Low value

Three entries, all of them rules that turned out not to be rules at all. They are kept here, rather
than deleted, so that a citation to one resolves to its disposition rather than to nothing — and so
that the reasoning survives the next person who thinks of writing them again.

`O4` was the fourth and is no longer here: it costed measurably more than it prevented *as
implemented*, and narrowing the implementation to the four tests it was about made it a rule worth keeping.

### R1 — The repository is the mod folder

**Absorbed into P11.** It stated a fact about how the game loaded this directory, not something a
change can violate — and since the MDK2 move it is not even the fact: the packer's folder is what
ships, and P11 now says so. Everything that *can* be violated because of it is already written down:
`R2` keeps generated bulk out, `R3` keeps credentials out, `R5` protects the identity files.

### J1 — Game mod first

**Absorbed into P14.** *Where a simplification costs nothing a player can perceive and avoids real
expense, take the shortcut, and say plainly when the shortcut would be visible.* That is a prior
that decides what gets built; it is not a rule a diff can be measured against. Its operative half
is `D6`: take the shortcut, and record it as a limit.

### J2 — Light, isolated, tested, open

**Absorbed into `C4`, `C5`, `C7` and `R9`.** Four claims in one sentence, each of which already
has its own rule and its own check. As a rule it could not be broken independently of the four,
and a rule that cannot be broken on its own is a summary. It remains a good description of the
mod on the [README](../README.md); it is not a rule.

---

## Where these came from

Nothing on this page is invented here except `E11`, `C8` and `R11`, and those are generalisations of
defects the tree already records. Every other rule was already written down somewhere — in a page,
in a test class's own summary, in the header of a tools page, or in a note kept outside the tree —
and that was the problem it exists to fix. Each rule's *From* field names where it came from.

Those pages keep the argument and the evidence. **When a rule and its source disagree, the source is
right and this page is stale**; say so and fix it here.

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-24 | **The page follows the MDK2 layout, and `C2` has a check.** The mod's sources moved from `Data/Scripts` to `Thermodynamics/`, the mod project from `Generic.csproj` to `Thermodynamics/Thermodynamics.csproj`, and publishing from *this directory is the mod* to *MDK2 packs `Thermodynamics/` into the game's `Mods` folder* — so `P11` is the mod folder rather than the repository, and `R2` is held by `mdk.ini`'s `ignores` rather than by a root `Directory.Build.props` that no longer exists. **`C2` is checked by the mod build**: MDK2's analyzer reports a prohibited member as `MDK01`, an error, proven against `System.IO.File` and `IndexOutOfRangeException` before being cited. **`C8`'s index row said `—` while its body named `CorpusGuardTests.OffMeansOffHoweverItIsSpelled`**, which runs; the index is corrected. **The unchecked count was wrong in two places** — both sentences said eighteen, and the index counted sixteen before today's two — and it is fourteen, counted from the index rather than carried forward (`E5`). `R14`'s compiler check had been lost in the move — the new mod project did not generate documentation, so a dangling `cref` in a file only it compiles was unchecked — and is restored, proven against a dangling `cref` in `Crosshair.cs`. |
| 2026-08-28 | **`W4` is checked now, and closing it found the defect it was written about.** Its *Checked by* field read "— nothing… that is discipline", and discipline had missed `GetSetting`: `name => Settings.Instance.GetValue(name)`, four lines above a `SetSetting` guarding the same field, so a consumer asking for a setting before this mod loaded its own got a `NullReferenceException` in their session with this mod's name on it. Every table entry goes through `ThermalApi.Guard` now — structure rather than a promise about eighteen bodies — and `GetSetting` reads through `Settings.EnsureLoaded`. |
| 2026-08-28 | **`M7` gains the half that was luck rather than method: a control the change can reach is not a control.** Pass 9's tenth iteration measured a stage nothing had touched moving 4.5 % between two binaries — and 0.3 % when run on its own. Settling between stages reclaims the garbage and leaves the heap it was allocated into, so a stage carries its predecessors' allocation, and the change under test had cut exactly that. `bench stages --isolate` gives each stage its own process. |
| 2026-08-27 | **`D2`'s own stated gap is closed: the test tree scans itself.** The entry ended by saying nothing looked for an uncalled `internal static` invariant in the test project, which is how four survived a pass. `UncalledCodeTests` looks now — over the shipped code as well, where it comes back clean — and found two on its first run — a documented six-face count in `Scenarios` and a photopic luminance integration in `IncandescenceTests` that was written from first principles and never called. The compiler cannot find these: an unused private field is a warning and an unused private method is not. |
| 2026-08-27 | **`M7` is now *two figures are comparable only if they were taken in one window*, and `M4` says which statistic to read is a property of the session.** The same exposure code measured a 4.75 ms median in one held window and 11.40 two days earlier — `best/med` 0.83 against 0.45 — with the source, the instrument, the build configuration and the stage order each eliminated in turn. The minimum roughly travels between sessions and the median does not, which is the reverse of what `M4`'s within-window table assigns this stage. Pass 4 saw the same effect at thirty per cent and left it as prose; it has an artefact behind it now, `taken_utc` on every run the stage lab writes. |
| 2026-08-27 | **`M4` takes the median as well as the fastest, and says the reproducibility is not checkable.** Four runs of one binary at 126,731 blocks, four hundred repeats a stage, every candidate summary compared: the minimum reproduces to 4.5 % on the room pass and 97 % on `register`; the median to 0.9 % on exposure where the minimum manages 30 %. No summary is best for more than three of the eight and two have none. Pass 8's fix — sample until the best is reproduced — was right and does not reach a stage whose best is a rare draw. Unchecked rules: eighteen, unchanged. |
| 2026-08-27 | **`R14` is checked by the compiler.** Turning on `GenerateDocumentationFile` and making `CS1574` and its family errors makes every `<see cref>`, `<param>` and `<paramref>` in this tree resolve to something that exists — real name binding, which no test here could do without reimplementing it, and which had never been asked for. Twenty-five failures were waiting, four in the shipped mod, including a `[ProtoMember]` whose comment described a deleted field and a method's parameters documented onto the constant inserted above it. `CS1570` and `CS1572` stay warnings in `Generic.csproj` alone, for the five a vendored file carries and `R6` forbids fixing. Unchecked rules: eighteen, unchanged — `R14` was already cited to a test, and now has the stronger check in front of it. |
| 2026-08-27 | `M4` says what makes N enough: the fastest reading has to have been reproduced. Keeping the fastest of a fixed fifteen had not converged for any stage but the solver — three runs of one binary spread 48 %, 28 % and 67 % — and two passes of optimisations were judged at effect sizes smaller than that. |
| 2026-08-27 | `C4` is asserted rather than measured. *Nothing allocates on the stepping path* was checked by reading a benchmark, which for a quantity whose correct value is **zero** is not a check at all: any figure at all reads as a small number. `StageLabTests` reads `GC.GetTotalAllocatedBytes` around a settled step of a census hull and fails above four kilobytes. Unchecked rules: eighteen, unchanged — this one was cited to a report and is now cited to a test. |
| 2026-08-26 | **Added `M13` — a timing is taken on the build that ships — after finding that no timing ever had been.** Nothing under `tests/` set `<Optimize>`, and every command this repository runs builds `Debug`, which tells the JIT not to optimise. A step is 3.4× dearer that way and the factor is not uniform — 5× on the row fill, 7.7× on the diagnostics surcharge, 2.6× on the clamped conduction loop — so the *shares* this repository published were wrong as well as the absolutes, and they were wrong in favour of exactly the work an optimisation pass looks at. It lands under `P1` because it is the same failure as a duration quoted without its machine: the figure's stated scope was untrue. Unchecked rules: eighteen, unchanged — `M13` arrived with `OptimisedBuildTests`. |
| 2026-08-26 | `R4` and `R5` were both *judgement* and are checked now, by `ShippedIdentityTests`. They are the two things here that editing back does not undo: regenerating `modinfo.sbmi` publishes the mod as a new workshop item and leaves every subscriber on the old one, and moving a folder under `Models/` costs a re-export of every block that names a path in it. The model pin is a digest of the sorted **set** of paths, because a file added is a normal day and a file moved is the failure. Unchecked rules: twenty-one down to eighteen. |
| 2026-08-26 | `E4` has a check, having been unchecked since it was written. `verdict.py` counts the corpus's blueprints and prints `*** PARTIAL: n of m ***` above everything else — the 2026-08-25 survey reads 46.2 % — and says the population is *unknown* where the corpus is not on the machine rather than assuming a dataset is whole. It is *reported* rather than enforced, and the index says so. |
| 2026-08-26 | `R8` gained its fifth place: the bridge from the world's settings to the solver's. A field on `ThermalSettings` the bridge does not copy satisfies every other clause of the rule and is still left at its default in every world, and the suite cannot see it because a test builds the solver's copy directly. Checked in both directions now. |
| 2026-08-26 | Added `M12` and `R17`, both from defects found the same day, and both under a principle that already existed — which is the fifteen still holding. **`M12` — a settled figure proves it settled**: `LoopCoolantMassTests` ran a ring with a source and no sink of any kind, so every arm climbed for ever and the four temperatures this repository published for the coolant density were that ramp read at step 400. Doubling the run is the whole check. **`R17` — every dial is asserted to move an outcome, not only to be read**: `R8`'s *read by something* is a grep, and `LoopStagnantTransferFraction` satisfied it while nothing multiplied by it. Four reflection-driven reach tests now enumerate every field on every properties class the simulation reads. |
| 2026-08-25 | `E1`'s check now refuses a criterion its dataset cannot resolve. A partial survey slice of thirteen ships reported `G1` as *holding* — one ship is 7.7 % of it and the criterion turns on 1 %, so both sides of the line read the same. The rule is derived from each criterion's own threshold rather than being a sample-size floor somebody chose, and it is stated as a resolution test so that passing it is not mistaken for a population. Found by leaving a partial dataset on disk and pointing the scorer at it.

| 2026-08-25 | Corrected `O3`'s retirement condition, which described a defect that had been fixed. It said the resume *currently* counts files and that a workaround and three pages' caveats retire with it, and cited a backlog row closed on 2026-08-22; the resume records finished blueprints and has since before this page was written. What `verdict.py` still does is a guard rather than a workaround, and the two pages that carry the 2026-08-21 dataset's 50 duplicate rows already scope them to it.

| 2026-08-25 | `E5`'s check reads source comments as well as pages. The gap was demonstrated rather than argued: the panel grew from 36 ships to 50, every page was corrected, and `KnobSweep`'s own summary — the file a reader opens to find out what the sweep does — went on saying 36. Extending it found two more stale counts in comments. What it still cannot do is now written into the rule: it matches one phrasing, because the other phrasing in the tree is how a figure is correctly scoped to the run it came from. |

| 2026-08-25 | Said what this page's identifiers share with [backlog.md](backlog.md)'s and what was decided about it (`H8`): eleven collide, 215 citations would have to be resolved by hand to remap them, and the errors of that diff would be silent — so the set is frozen by a check rather than paid off. |
| 2026-08-25 | **`R16`, and the 155 links it found.** *A pointer in code is plain text, never a markdown link* was written into [development.md](development.md#and-in-the-code) after two such links were found rotted, and nothing checked it — a link inside a `.cs` file renders nowhere, so nobody clicks it, nobody finds out it is wrong, and `EveryRelativeLinkResolves` reads markdown only. The one form of cross-reference here that nothing checked was the one written in the syntax that looks checked, and 155 had accumulated across 91 files. Flattening them cost nothing, because every link text was already the page's own name. `NoPointerInCodeIsWrittenAsALink` holds it, and it demonstrated that it works by failing on the first draft of its own summary, where the example was quoted verbatim. |
| 2026-08-25 | **`R15`, and thirteen dead citations in shipped code on its first run.** `R11` fails when a rule names a check that has stopped running; nothing failed when a *citation* named a rule or a backlog row that had stopped existing, and `EveryRuleCitedByAPageExists` reads documentation banners while **425 citations of the same shape live in `.cs` and `.py` files**. A backlog row is deleted when it closes, so every comment citing it becomes a dead reference that reads exactly like a live one: `C20` in six files, three of them compiled by the game, and `C13` in three more. All thirteen now name the page that holds the argument, in plain text as the comment convention asks. |
| 2026-08-25 | **The four rules added today are `W1`–`W4`, and the letter was chosen because everything else collides.** [backlog.md](backlog.md) and this page share a letter-and-number namespace and fifteen identifiers are currently both a rule and an open item — `C3` is *target `net48`* here and *whether to ship `MaxSubstepsPerBlock 6`* there. They were first issued as `C16`–`C19`, which are four live backlog rows, and moved before the commit landed. Nothing else is renamed: the backlog's numbers are its rows' names and never move, and remapping this page's three colliding letters would touch 362 citations in code alone. That is a decision and it is [backlog.md](backlog.md) H8. What is fixed is that it stops growing. |
| 2026-08-25 | **The categories are absolute, conditional and low value, and the first two are now one question.** The first category was *load-bearing* and its test was the consequence of breaking it, which is true of nearly every rule here and is the wrong axis to sort on: it made the boundary with *conditional*, which is about scope, a comparison between two different things. Re-audited under *does it ever stand down*, **not one rule changed category** — the boundary was right and its stated reason was not. |
| 2026-08-25 | **Re-tested the reduction from scratch, and the rejections are the result.** Two more principle-level mergers attempted and both rejected: `P8` with `P14` produces a heading that has to be unpacked before it applies to either side, and `P10` generates one rule but carries the clause that makes every other argument here safe — *everything else is tuning*. Four rule-level mergers attempted and all four rejected, which says the list is at its irreducible size rather than asserting it: `M4`+`M5` are an instruction to a runner and one to a reader, `E10`+`E11` differ by two obligations, `C9`+`C10`+`W3` share only their principle, and the seven join rules under `P5` *are* that principle applied to seven artefacts with seven checks. **Two rules were re-filed**: `R5` to `P15`, and `D4` from `P4` to `P2` — *both are where a fault survives longest, because nobody looks there* is a blind spot read as a value, not an oracle problem, and `D8` beside it is genuinely `P4` and says so in its own words. **Two gained a second principle** on the precedent `R9` set: `O4` is `P6` for its wall-clock half and `C15` is `P8` for the ladder itself. |
| 2026-08-25 | **Five rules the repository already enforced and had never stated, and the fifteenth principle two of them needed.** A sweep of the tree for unstated intent found them; four were already held by tests that no rule cited, which is the *one place* claim failing in the direction nothing checks — `R11` catches a rule naming a dead check and nothing catches a live check enforcing an unstated rule. `W1` a saved world loads on the builds either side of the one that wrote it; `W2` a name something outside this repository addresses is never repurposed; `W3` an authority check reads what the engine supplies, never what the sender wrote; `W4` no call across the mod's API throws into its caller; `D9` a fault is recorded whether or not collection is running. `W3` landed under `P7` and `D9` under `P2`, which is more evidence for those two. `W4` widened `P9`: a library is not only reachable without the session, it is safe to call. `W1` and `W2` landed under nothing, and `P15` is what they needed — *what is already in someone else's world is frozen* — which also moved `R5` out of `P12`, where a workshop id had been filed as *hard to regenerate* when what it actually is, is impossible to un-publish. |
| 2026-08-24 | Added `C15`, which is `C7` grown a dimension: a mechanism's configuration runs from `off` to `realistic` rather than being a boolean, and a cheaper form that exists in the model belongs on that list. Stated after the intent it comes from, and it found one violation on the day it was written — `WellMixedCoolant`, read by the solver and settable by nothing. |
| 2026-08-24 | Added `C11`, after the mod failed to compile for two commits while the whole suite passed. Two thirds of the adapter under `Game/` was compiled by nothing the workflow runs, so a rename in `Core` was invisible until a world load. `Generic.csproj` is in the test solution now, and its twenty-seven hard-coded Steam paths resolve through `$(SEBinPath)` — being unbuildable anywhere but this machine was the reason it could not be in the build in the first place ([backlog.md](backlog.md) `F25`). |
| 2026-08-22 | `R9` covers the signature as well as the name, and `ModApiShapeTests` checks it. A failed cast returns null rather than throwing, so a signature that moves on one side alone gives another mod a feature that silently does nothing ([backlog.md](backlog.md) `F1`). |
| 2026-08-22 | `C9` now names air density among the things the game answers, which is what settles whether this mod should author its own against the engine's ([backlog.md](backlog.md) `B22`). |
| 2026-08-22 | `R3` and `E5` are checked rather than judged ([backlog.md](backlog.md) `F9`). The tree is scanned for four credential shapes, and the scan is itself checked against a value of each shape and against the text this repository legitimately writes. Counts a page states about the panel, about `Cubes.xml` and about the suite's own classes are compared with those datasets; a change log is exempt, because `R12` makes it a record of what was true rather than a claim about now. It found two stale figures on its first run — 432 authored values against 654, and 135 test classes against 160. |
| 2026-08-24 | **`R14`'s check learned the second signature, and it found a live orphan in shipped solver code.** It had only ever looked for a summary closed and immediately reopened — a member that has *gone*. The other shape is a member that has *arrived*: a second summary opened before the first is closed, which is what a newcomer pasted into the middle of an existing comment leaves behind. In `ThermalSolver`, `NodeConductanceTotal` had landed inside `NodeSubstepDemand`'s summary, so one comment was cut in half and the other half — *per-block-type telemetry can attribute a grid's substep count to specific definitions* — hung under `NodeConductanceTotal` describing a method it is not about. Both are put back. Run against a deliberate orphan in both spellings of the new signature before being believed, and it reports the line of the *first* summary, which is the one that lost its member. |
| 2026-08-22 | Added `R14`, from the standard [document-of-intent.md](document-of-intent.md#what-a-code-comment-is-for) states and a pass that applied it: twenty-four comments were found describing a member that no longer exists, having come to rest on the one below. `NoDocCommentDescribesSomethingThatIsNotThere` catches the shape, and was run against a deliberate orphan in both spellings before being believed — the first version caught only one of the two and missed a real orphan in `CoolantLoopTests`. |
| 2026-08-22 | Moved *What the extraction changed* into this log, where a record of a revision belongs (`R12`). It held four things, each still true and each now recorded once. **Three *Checked by* citations named something that does not run:** `C6` cited `PhysicsTests`, which is a file whose classes are `ConductionTests` and `StabilityTests`; `D1` attributed three invariants to `LabInvariantTests` that live in `ScreeningTests`; and `D5` cited `SealedBlocksAreRare`, which commit `991d4d9` demoted to an uncalled helper when `CorpusSurvey` absorbed the standalone walks. `R11` is the rule those three produced. **`E1` and `E8` overstated `verdict.py`**, which prints `HOLDS`, `FAILS` or `?` and exits zero either way — both fields now say reported rather than checked. **Two rules came out of the reduction rather than an incident:** `E11` closes `E1`'s hole, and `C8` generalises the gate whose off position cannot be spelled. **Three rules stopped being rules and one changed category** — `R1`, `J1` and `J2` are premises rather than things a change can violate, and `O4` was reclassified low value against a measurement taken the same day. All four dispositions are in [Low value](#low-value). |
| 2026-08-22 | Put this page under the checks it asks of every other page. `EveryCheckCitedByTheRulesPageResolves` resolves every name in a *Checked by* field to something that runs, which retires `R11`'s unchecked state and closes [backlog](backlog.md) F10; `EveryRuleCitedByAPageExists` fails on a page citing a rule this one does not state; `TheRulesPageIndexesEveryRuleItStates` holds the index, the body and the principle table together. Each was run against a deliberate violation before being believed. |
| 2026-08-22 | Read every page in the tree against the rule list and added the six rules it was missing: `C9` `C10` `D7` `D8` `R12` `R13`, each of which existed only in the one page or the one type summary that needed it. Widened `P7` from *the game is the authority on what compiles* to *the game is the authority*, which is where `C9` and `C10` belong. Recorded the necessity and sufficiency tests the principle list was put through in [Testing the reduction](#testing-the-reduction). Corrected `O4`, which quoted 135 test classes against a project that now holds more than 150; the figure is now stated without a hand-typed count, per `E5`. |
| 2026-08-22 | Repointed the *From* fields at the pages that absorbed the ones they cited: `corpus-shape.md` into [balance.md](balance.md), `iterations.md` into [benchmarks.md](benchmarks.md). Added this change log. The rule inventory itself is unchanged. |
| 2026-08-22 | Extracted the standing rules from the eleven pages that each stated one in passing, reduced them to fourteen principles, and classified every rule as load-bearing, conditional or low value. Marked the sixteen load-bearing rules that nothing checks as unchecked rather than leaving the gap implicit. |
