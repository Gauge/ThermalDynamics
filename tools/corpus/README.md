# Corpus tools

What to do with the CSVs a corpus sweep leaves behind. Everything here reads a data directory; the
run of 2026-08-21 is in `out/corpus-2026-08-21`, its census in `out/census-2026-08-21` and its dial
sweep in `out/knobs-2026-08-21`. None of those directories is committed — they are gigabytes — so
each of these prints what it found and renders without the parts that are absent.

> The rules argued here are stated canonically in [rules.md](../../docs/rules.md): `E5` `M10`.

| Looking for | Go to |
| --- | --- |
| The criteria these scripts score, and why each was written | [balance-lab.md](../../docs/balance-lab.md) |
| What the population actually said | [balance.md](../../docs/balance.md) |
| Running the walks that produce these datasets | [tests/README.md](../../tests/README.md) |
| Every open item, one line each | [backlog.md](../../docs/backlog.md) |

```
python3 tools/corpus/verdict.py out/corpus-2026-08-21     # the criteria, on the terminal
python3 tools/corpus/cap.py out/cap-2026-08-25 \
    --csv tools/corpus/summary-cap-2026-08-25.csv         # C3: what a per-block cap buys and costs
python3 tools/corpus/floor.py out/floor-2026-08-29 \
    --csv tools/corpus/summary-floor-2026-08-29.csv       # C30: what an over-budget floor buys and costs
python3 tools/corpus/cruise.py out/census-2026-08-31/census.csv \
    --csv tools/corpus/summary-cruise-2026-08-31.csv      # K12: does a derived top speed land where the authored one does
python3 tools/corpus/cruise.py out/census-2026-08-31/census.csv --cd 1.54 \
    --csv tools/corpus/summary-cruise-shape-2026-08-31.csv # K22: the same, with the hull shape term on
python3 tools/corpus/dragfit.py out/census-2026-08-31/census.csv --sweep \
    --csv tools/corpus/summary-dragfit-shape-2026-08-31.csv # K22: does the drag milestone's criterion still hold
python3 tools/corpus/knob.py out/knobs-2026-08-30 --knob oxygen-waste \
    --csv tools/corpus/summary-knob-oxygen-2026-08-30.csv # what one dial does to the standing panel
python3 tools/corpus/reproduce.py out/cap-2026-08-24 out/cap-2026-08-25  # did a restart reproduce?
python3 tools/corpus/censusdiff.py out/census-2026-08-21 out/census-2026-08-25 \
    --expect waste_full_w=8:10 --expect ships=0:0            # A13: did the re-take do what was predicted?
python3 tools/corpus/pace.py out/cap-2026-08-25/progress.txt \
    --reference out/air-corpus-2026-08-24/progress.txt \
    --outcomes  out/air-corpus-2026-08-24/outcomes.csv   # what a running walk will cost
./tools/corpus/build-report.sh out/corpus-2026-08-21      # the survey as one page
python3 tools/corpus/panel.py out/census-2026-08-25/census.csv \
                             out/survey-2026-08-25/outcomes.csv   # rebuild the standing panel
./tools/corpus/build-bench.sh out/corpus-2026-08-21 \
                             out/census-2026-08-21 out/knobs-2026-08-21   # every dataset, one page
```

## Directional aerodynamic thrust inputs

`aerothrust.py` consumes schema-1 JSON from the C# `aero-inputs` command and uses SciPy's HiGHS
linear-program solver to maximize force along each of the six saved main-grid headings. Every
answer includes throttle fractions, the force-balance residual, a recomputed dual support bound
and its gap. An optimizer success without valid numerical certificates fails the command.
A lone oblique thruster cannot supply pure forward force; positive projections are not summed.

This tool requires NumPy and SciPy; the validated versions are pinned in
`requirements-aerothrust.txt`. They are optional dependencies of this command, not of the existing
CSV scorers. The JSON output records both versions, the tool's source hash, input-file hash,
blueprint hash and harness MVID. Inputs must retain the six opposed orthonormal headings and the
sea-level convention. Thruster unit vectors are normalized only within the 1e-5 allowance for
blueprint float roundoff. The solver normalizes force by the largest rating (at least 1 N), then
requires force residual and absolute dual gap at most 1e-7 of that scale. Boundary results within
that numerical tolerance must remain unresolved in any later population scorer.

After the solution build, run each command under the memory cap required by `O1`:

```bash
dotnet run --no-build --project tests/Thermodynamics.Sim -- aero-inputs /path/to/bp.sbc --json > /tmp/aero-inputs.json
python3 tools/corpus/aerothrust.py /tmp/aero-inputs.json > /tmp/aero-thrust.json
python3 -m unittest discover -s tools/corpus -p test_aerothrust.py
```

`test_aerothrust.py` checks hand solutions, rotation/order/splitting/scale invariance, 80 seeded
nonzero comparisons against independent vertex enumeration, malformed inputs and injected
primal/dual faults. It also tests the machine-input contract. The C# `AeroPopulationInputTests`
check the JSON export against installed ratings and the engine's force/exhaust direction.
These are rated-force diagnostics with unlimited power/fuel and torque compensation. They do not
reserve simultaneous hovering thrust, establish native control feasibility, or score P1/P2.

## The core corpus — a walk of the population in eleven minutes

`core.py` draws a **stratified sample with weights**, committed as
[`core-corpus.csv`](core-corpus.csv). The downloaded blueprints all stay where they are; the core
corpus is a *selection over* them, so nothing is lost and a full walk is still one environment
variable away.

> **The costs this page used to quote were about four times too high, and slicing is why.**
> They said 36 minutes in air and 78 in the cap walk's paired arms. Walked in **one hold** on
> 2026-08-28 the same corpus took **11 m 19 s** and **21 m 32 s**. Nothing about the corpus or the
> solver changed between those figures; what changed is that the walks they were calibrated from
> were taken in nine slices. **Resume skips only what *finished***, so every slice killed by its
> deadline discards whatever its 31 workers had in flight — and the corpus is walked largest-first,
> so those are the most expensive ships in the population, redone by the next slice. The 2026-08-28
> air walk ran its first 625 files at 0.9–2.3 files a minute and its last 6,048 at 165.
>
> **So the first rule of a cheap walk is to finish it in one hold**, and the second is that a cost
> measured across slices is not a cost. Every figure on this page is now a single-hold one and says
> so (`E5`, `M1`).

```
python3 tools/corpus/core.py out/air-2026-08-28 --out out/core-selection.txt   # build the list
THERMAL_CORPUS_ONLY=$PWD/out/core-selection.txt ...                            # walk it
python3 tools/corpus/core.py --score out/<core-dataset>                        # read it
```

**The cost is not spread the way anyone guesses, and that is the whole design.** Measured on
`out/air-2026-08-28`:

| blocks | ships | share of the walk's cost | runs over the allowance |
| --- | ---: | ---: | ---: |
| under 10,000 | 7,199 (88.5 %) | **20.8 %** | **0** |
| 10,000 and over | 925 (11.5 %) | **79.2 %** | **712, all of them** |

**So the obvious cut is the wrong one, and it is wrong by a measured 82 %.** Dropping every ship
over 10,000 blocks gives a 63-minute walk in which `G6`'s cost half reads *zero runs over the
allowance* and its p99 lands **82 % below** the population's. A size-truncated corpus is not a
cheaper corpus, it is a corpus with the finding deleted — which is the same lesson as *never quote a
partial corpus result*, arrived at from the other end.

**What works is keeping the cheap strata whole and sampling the giants hard.** Every ship under
2,000 blocks is 63.9 % of the corpus and 4.5 % of the walk, so it is kept entire; above that the
sample is one in ten, one in eight and one in twenty, systematic within narrow bands rather than
random, and each kept ship carries the number of ships it stands for. Twenty runs in the whole
population go critical in air and they come from **eight ships** costing 2.3 % between them, so
those eight are a take-all stratum — at any fraction that makes the giants affordable, `G1` reads
zero, not because the population moved but because the event is rarer than the sample.

**How good it is, measured rather than claimed.** Drawn from the air walk and scored against two
walks it was *not* drawn from — the cap walk's uncapped arm, on a different build, and the survey's
five vacuum scenarios:

| statistic | in sample | cap walk | survey |
| --- | ---: | ---: | ---: |
| work p50 | +0.9 % | +0.9 % | −0.0 % |
| work p95 | −1.8 % | −3.5 % | −0.7 % |
| work p99 | **+0.3 %** | **−0.9 %** | **−4.5 %** |
| demand p99 | 0.0 % | 0.0 % | −0.3 % |
| over the allowance | −5.8 % | −3.0 % | −23.7 % |
| critical rate | +0.2 % | −19.8 % | −1.5 % |

**The pattern is the same in all three columns and it is the one sampling predicts: what is common
is estimated well and what is rare is not.** The survey's over-allowance rate is 0.68 % of runs and
reads 24 % low; the air walk's is 2.19 % and reads 5.8 % low. So the core corpus answers `G6`'s
percentiles, the demand half and the medians, and it does **not** answer a maximum — `work max` is
76 % low, because a maximum is one ship and cannot be sampled — nor any rate below about half a
per cent.

**Read a core walk with `core.py --score` and nothing else.** `verdict.py` has no weights, and a
core dataset read without them describes a population that is mostly small ships, which is not the
corpus and is not anything; it recognises such a dataset and stops rather than printing figures
shaped like population figures.

**The one exception is `cap.py`, because a paired walk is a comparison and not a population.** It
applies the same weights from the same file and recognises a core dataset by the same test, so a
core cap walk is read with `cap.py` exactly as a full one is and the report says on its first line
which it has. Three things follow, and they are the difference between the two reports:

* Every population figure is weighted, and its name says so — `C3 weighted dpeak p99`, never
  `C3 dpeak p99`. A core figure and a full-walk figure can then never be put side by side as
  though they were one reading.
* **The identity check is deliberately not weighted.** *Did the capped arm cap* is an assertion
  about the walk, and every pair walked is one observation of it; weighting that count would hide
  a broken pair on a ship of weight 1 behind twenty good ones on a ship of weight 20.
* **The maxima are unscored rather than passed.** The cost prediction is a p99 under 1 K *and* a
  max under 10 K, and a sample of one giant in twenty has no maximum — the core corpus reads 76 %
  under the population's `work max`. That half prints `?` on a sampled walk, and an unscorable
  half never rescues a failing one (`E8`).

`cap.py --csv` writes the figures as `statistic,value,unit` beside the dataset's provenance, from
the same `provenance.summary_rows` `verdict.py` uses (`P5`), which is what makes a cap figure on a
page checkable against the walk that produced it rather than against another page (`E5`).

**When to walk the whole corpus anyway.** When the finding is a maximum, a rare rate, or a tail
that has to be exact rather than estimated — and when a population figure is going to be published
as one. The core corpus is for the other ninety per cent of runs, where the question is *did this
change move anything* and the answer arrives in half an hour instead of overnight.

## Ask what the smallest run that answers the question is, before launching one

**A full sweep is meant to be rare.** It is hours of the machine, it blocks every other walk while it
runs, and its evidence is almost always carried by a small part of the population. So the first step
of designing a walk is working out the subset that can answer the question, and the second is saying
what that subset cannot answer. Reach for the whole population when the *distribution over the
population* is the finding — `G5`'s stress bound and `C3`'s who-pays argument both were — and not
otherwise.

Three levers, in the order worth trying:

| Lever | What it does |
| --- | --- |
| `THERMAL_CORPUS_ONLY=<file>` | Walks a named selection. This is the strong one: `CorpusFloorWalk` walks 294 blueprints rather than 8,144 because the mechanism only engages where the allowance binds, and the rule that picked them lives in the selection file (`M10`). |
| `THERMAL_CORPUS_SHIPS=N` | Walks a stride sample. Right for *does this walk work*, wrong for a tail — a p99 read off a sample of 40 rests on one observation. |
| Stopping early | The corpus is walked largest first, so a walk whose finding lives in the large hulls has it long before it ends — the same fact as *read progress in bytes, not files* below, used as a lever rather than as a warning. |

**The third lever is usually the biggest and it is the one nobody reaches for.** Selection saves less
than it looks, because cost goes with block count and the ships that carry a finding are the
expensive ones: dropping every ship under 60,000 blocks from the floor walk's selection removes 196
of its 294 ships — two thirds by count — and only **34 %** of the work. Whereas the floor walk had
38 of the 47 ships over 100,000 blocks measured **79 ships in**, a quarter of the way through.

**Which band matters is a question the walk itself answers, and it answered it here.** The floor's
error on the first 241 floored cells is a median of 0.027 K with a p99 of 28 K, and the tail is not
spread across the population: **nothing under 60,000 blocks exceeds 0.58 K**, every one of the 20
cells over 1 K is above 100,000, and `vacuum-shadow` supplies 11 of the 21 such cells from 37. A
walk that has covered its band has its finding whatever is left in the queue.

**Read the partial dataset before deciding to wait.** `outcomes.csv` is written per batch, so the
question *has the finding stopped moving* can be asked of a walk in flight. That is not a licence to
quote a partial run as a population figure — `P1` and `P2` still bind, and a figure from a stopped
walk carries the count it was stopped at.

## The three walks over the whole population

`CorpusSurvey` runs five scenarios and every one of them is vacuum. `CorpusAirWalk` runs the four
[`PairLab.AirScenarios`](../../tests/Thermodynamics.Harness/PairLab.cs) — `vacuum-shadow` as the
anchor, then `surface-hot-noon`, `storm-parked` and `reentry` — because both halves of `G6` are
decided in air and the survey has never seen any (`F11`). `CorpusCapWalk` runs those same four
**twice**, with `MaxSubstepsPerBlock` off and at 6, which is `C3` and `G6`'s cost half as one
experiment. They are separate walks with separate resume records and separate data directories, so a
figure quoted from one is not silently a figure from a run of the other (`M1`).

```bash
THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=$PWD/out/air-2026-08-24 \
    THERMAL_CORPUS_PROGRESS=$PWD/out/air-2026-08-24/progress.txt \
    systemd-run --user --scope -p MemoryMax=24G -p MemorySwapMax=0 --quiet \
    dotnet test -c Release --no-build tests/Thermodynamics.Tests \
        --filter "FullyQualifiedName~CorpusAirWalk"

THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=$PWD/out/cap-2026-08-25 \
    THERMAL_CORPUS_PROGRESS=$PWD/out/cap-2026-08-25/progress.txt \
    systemd-run --user --scope -p MemoryMax=24G -p MemorySwapMax=0 --quiet \
    dotnet test -c Release --no-build tests/Thermodynamics.Tests \
        --filter "FullyQualifiedName~CorpusCapWalk"
```

**A paired walk costs about twice an unpaired one, and not because it runs twice.** Every run in
`CorpusAirWalk` stops at equilibrium, and in air that is usually two chunks of an 1,800-second
scenario; the capped arm cannot stop there, because it has to stop where its own control stopped
(`M1`). `THERMAL_CORPUS_SHIPS=40` walks a stride sample instead of the population, which is how to
see that a walk works before committing the hours — the sample for this one was 320 runs in 77 s.

**A walk's cost is a property of the build, not just of the corpus.** The 2026-08-28 air re-take is
**2.94x per file** over the same file range as the 2026-08-24 walk it replaces — about 5.1 hours
against 104 minutes — and the reason is `A13`: the eleven block kinds that reader built as armour
are oxygen generators, gravity generators, doors and turrets now, and this walk runs to equilibrium.
**A dataset taken through a broken reader was cheaper to collect than the truth.** So the 1.95x
figure below is a ratio between two walks *of one build*, and re-using it across a build that
changed what the corpus contains would understate the cost.

**Twice was offered as a ceiling and the finished walks breach it.** The reasoning still holds —
the cap walk is the air walk's four scenarios with a second arm on each, the two arms share one
blueprint parse, so the second arm can only add what it simulates. The number did not: 1.95x was
measured over the **fifty files** the two walks' progress records share, and over the whole corpus
the same two walks are **228.8 minutes against 104.5**, which is **2.19x** (`E10`). A fifty-file
sample of a corpus walked largest-first is fifty capital hulls, and the second arm costs relatively
more on the small ships that make up the rest. So the cap walk is **2.19x the air walk**, and on
the re-taken air walk's 303.6 minutes that is about **eleven hours**, not three and a half.

**Do not estimate a walk from the rate it is covering blocks at.** That is what abandoned the first
cap walk, and it is not an instrument: the corpus is walked largest first, so blocks-per-minute
falls throughout every healthy run, and a mark is ten files, which here can be one capital hull or
ten fighters. `pace.py` prints that estimate as the spread it has and then runs it over the
*finished* air walk, where the answer is known — over that walk's own first 35 minutes it projects
104 to 428 minutes, median 154, against the 104 it took (`P4`).

**`THERMAL_CORPUS_DATA` must be absolute.** The test host's working directory is the test project's
output directory, not the repository, so a relative path writes the dataset somewhere nobody will
look for it. Everything else on this page applies unchanged: no hang timeout, one walk at a time, a
memory cap, and relaunch to resume.

---

## A figure a page quotes has a source in the tree

The datasets are gigabytes and are not committed, so until 2026-08-24 every corpus number in the
documentation was a number with no source anybody could check it against — and one of them drifted
by two orders of magnitude before it was compared back (`F14`: *twenty-four sealed blocks on one
ship* against a dataset saying 1,184 across 331).

`verdict.py --csv <path>` writes the figures it prints as `statistic,value,unit`. That file is
kilobytes and is committed:
[`summary-2026-08-21.csv`](summary-2026-08-21.csv) is the vacuum survey.

```bash
python3 tools/corpus/verdict.py out/corpus-2026-08-21 --csv tools/corpus/summary-2026-08-21.csv
python3 tools/corpus/verdict.py out/air-2026-08-24 --baseline tools/corpus/summary-2026-08-21.csv
```

`--baseline` prints what moved, which is how one walk is read against another — the same corpus in
vacuum and in air. It reports **every statistic on either side**, so one the other dataset does not
carry shows as an em dash rather than being dropped: a scenario present in one walk and absent from
the other is the finding, not a gap to be joined away.

**Every dataset says what build it came from.** `provenance.txt` is written beside the outcomes on a
walk's first batch: the walk's name and start, the commit `HEAD` pointed at, and a digest of
`Cubes.xml`, `Loops.xml` and `Planets.xml` — the three files whose contents decide what a walk
measures and which a commit hash says nothing about when they are edited and not committed. A
resumed walk appends a second block rather than overwriting, so a dataset assembled across two
builds says so.

`verdict.py` prints it and `--csv` records it, so a figure quoted from a committed summary carries
the world it was measured in. A dataset with no such file says so rather than being assumed current.

**And a reader has to read all of it, which took until 2026-08-28.** The writer had appended
correctly since the day it was written and its own summary said so; `provenance.py` read only the
*last* `Cubes.xml` line, so a walk resumed across a definition change reported as measured against
the current file — which it half was. The 2026-08-25 survey ran in five slices and records two
`Cubes.xml` hashes and two `Loops.xml` hashes, because `C36`, `C42` and `C43` landed between the
fourth and the fifth. One format, two readers, and they had drifted (`D3`).

`provenance.py` now prints `SPANS n VERSIONS of <file>` above its figures, and
`spans_several_definitions` is the lookup. It says the dataset is mixed; it does not say whether
that reaches the figures, because that depends on which blocks moved and whether the population
carries them. For that survey it does not: the only `Cubes.xml` change in the window is the
emissivity of `Gauge_SG_Radiator` and `Gauge_LG_Radiator`, and **no ship in the corpus carries a
radiator, a coolant pipe, a pump or a heat pump**. `Loops.xml` describes coolant, which is the same
argument. Both halves of the dataset are comparable, and now a reader can see the question rather
than having to think of it.

That last claim is a measurement, not an argument from what a workshop blueprint ought to contain —
`Ship.IsVanilla` means every block named a definition, and the mod's blocks *are* definitions, so it
would not have caught one. It is 207 distinct types across the whole census and none of them one
this mod adds:

```bash
cut -d, -f4 out/census-2026-08-25/composition.csv | tr -d '"' | sort -u \
    | grep -icE 'radiator|coolant|heatpump'      # 0, of 207 distinct types
```

It exists because the alternative was paid for once. The 2026-08-24 air walk finished **one minute
after** a commit that took twenty-seven `ConsumerWasteEnergy` fractions from 0.9 to 1.0, and
establishing that took comparing its rows against a later walk with `reproduce.py` and then reading
the git log for the window between them. Datasets collected before 2026-08-25 have no such file.

**A paired dataset is scored on the arm that ships.** `verdict.py` says so on the first line when it
meets one, and leaves the other arm to `cap.py`. It did not always: keyed by ship and scenario, the
capped arm read as *912 duplicate rows* and half the dataset was dropped under a note about a resume
that had not happened. It kept the right arm by accident, which is the worst way to be right — found
on a dry run before the walk landed, and pinned by `test_scoring.py`.

**Read `dataset ships` first.** The corpus is sorted largest-first, so a walk stopped early is not
a small sample of the population — it is the wrong end of one, and every percentile it reports is a
percentile of the biggest ships in the corpus. A partial dataset carries exactly the columns a
finished one does. The count is taken from the outcomes rather than from `ships.csv`, which a walk
may legitimately not write.

---

`verdict.py` evaluates the criteria written down in [balance-lab.md](../../docs/balance-lab.md)
before the data was collected, so a run that fails them is a finding rather than an excuse to move a
threshold. It reports G1, G2, G5 and G6, says plainly that G3 and G4 are not answerable from this
dataset rather than skipping them, and then prints the three things the criteria do not cover: how
much of the population sits in the censored tail, how fast damage arrives, and which block types
drive it.

`build-report.sh` packs the same data with `pack.py` and assembles `shell.html` + `body.html` +
`script.html` into one self-contained page. The page defaults to the per-block critical temperatures
the run actually used; moving its threshold slider switches it to a single global threshold and says
so, because that is a proxy for re-tuning and not a re-simulation.

`panel.py` chooses the standing panel of ships the dial sweep runs on, because re-simulating
8,142 ships for every value of every dial is hours a dial. **Every ship is picked by a named rule
and the rule is written into `panel.csv` beside it**, so the panel can be audited and rebuilt
rather than trusted. `KnobSweep` reads that file, and reads each ship's blueprint path out of
`ships.csv` — without a path the sweep walks and fully parses all 9,981 corpus blueprints to find
fifty ships, with every worker opening quarter-gigabyte files at once, which is how earlier runs
died.

`build-bench.sh` is the whole-corpus counterpart to `build-report.sh`: it packs the survey, the
census, the composition and the dial sweep with `pack-bench.py` and assembles `bench-shell.html` +
`bench-body.html` + `bench-script.html` into one page. The sweep is optional and the page says
`pending` where it is absent, so the bench exists before the sweep finishes.

**Every figure a page states about its dataset comes from that dataset.** The bench's header
counts were written into the HTML by hand until the panel grew from 36 ships to 50 — and to 52 on 2026-08-28 — and the page
went on saying 36. A reader who catches one wrong count stops believing the right ones.

## The ways a full sweep dies

A full sweep is hours of compute. Five things went wrong on the run of 2026-08-21, all of them
avoidable, and all five cost time rather than data; the two below them cost a dataset and a session
instead, and were found later. Recorded here because a run that dies at hour seven is the most
expensive mistake this repository can make.

**Never set `--blame-hang-timeout` on a corpus test.** It killed a healthy run at exactly eight
hours. The survey is one test that legitimately runs longer than any timeout worth setting, and
VSTest cannot tell slow from deadlocked.

**Filter to the survey alone** — `--filter "FullyQualifiedName~CorpusSurvey"`. A broader filter pulls
in `SunlightPanelWalk`, which is 200 ships × 7 runs on a fixed 1,800 s clock, and under
`maxParallelThreads: 1` that blocks the deliverable for hours.

**Do not let the walks run concurrently.** Four walks × 31 workers is 124 threads on 32 cores, and it
measured **17× slower** than sequential — hidden behind a 93 % CPU reading, because the cores were
busy thrashing cache rather than working. `xunit.runner.json` is set to `maxParallelThreads: 1`;
leave it, and see [backlog.md](../../docs/backlog.md) F8 for the narrower form that would let the
rest of the suite run in parallel again.

**Watch what a `pkill` pattern matches.** `pkill -f "filter CorpusSurvey"` matched the relaunch that
had just started, killing the recovery along with the corpse. Use a pattern that cannot match the new
invocation.

**And watch what a `pgrep` pattern matches**, which is the same trap wearing the other hat. A shell
loop written as `until ! pgrep -f CorpusCapWalk; do sleep 120; done` contains the string it is
looking for, so it matches *itself* and never exits — and a later `pgrep` run to ask whether the
walk is still going answers **yes** long after it finished. On 2026-08-25 five such waiters were
left running after a walk that had ended cleanly. Wait on something the walk owns rather than on its
name: the last line of `progress.txt`, or the `Passed!` in its log.

**Cap the memory.** An uncapped run has taken the machine down with it; wrap it in
`systemd-run --scope -p MemoryMax=…` so the run dies instead of the session.

**Relaunch to resume; there is nothing to pass.** A walk records each blueprint in
`done-<walk>.txt` in the data directory once it has finished with it, and reads that record on the
next start, so a killed run picks up exactly where it stopped. Deleting the file starts the walk
over. This turned a lost eight hours into a seventeen-minute finish, and it replaced a skip counted
in files by hand: files were counted as they were handed to a batch while rows were written as each
ship finished, so a resume both re-emitted the interrupted batch and lost the ships it had not
reached. The 2026-08-21 dataset carries 50 duplicate rows from that; nothing collected since can.

**A run that has no record can be given one.** The resume reads `done-<walk>.txt`, and a dataset
collected before that file existed does not have one — but `ships.csv` carries each ship's blueprint
path, so the record can be rebuilt from it:

```bash
python3 -c "import csv,sys;[print(r['path']) for r in csv.DictReader(open(sys.argv[1]))]" \
    out/corpus-2026-08-22/ships.csv | sort -u > out/corpus-2026-08-22/done-survey.txt
```

One caveat, and it is the reason this is a recovery rather than the normal path: a blueprint holding
several ships that was interrupted part way through appears in `ships.csv` and would be skipped with
ships still to do. That is one file of nine thousand, against a whole run.

**And one that costs the dataset rather than the run: do not rebuild the configuration a walk is
using while it is slicing.** A walk runs `-c Release --no-build`, so every slice loads whatever is
in the Release output at the moment it starts. A `dotnet build -c Release` or a `dotnet publish`
between two slices — for a benchmark, say, or to pin a binary — silently gives the second half of
the dataset a different build from the first. The provenance record makes that *visible* rather than
harmless: it appends a block per slice and `provenance.py` reports `SPANS n VERSIONS`, which is how
the 2026-08-25 survey's split was found. Debug builds are fine; they are not what a walk loads.

**And one that costs a session rather than a run: do not read the suite while a walk is running.** The tests that assert an elapsed time are
measuring a machine a walk is using every core of, and they fail on it: on 2026-08-25 the full suite
was green before a cap walk started (1,864 of 1,864) and `LoadTests.SolverCostPerLinkStaysProportional`
failed during it at **3.14×** its own limit, reporting 17.63 ns a link visit against 5.62. Nothing is
wrong with the mod when that happens. `[Collection("alone")]` keeps those tests from colliding with
the rest of the suite and can do nothing about a walk in another process, which is the same
observation as `O4` one process up. The fast lane is fine to run; a full pass waits for the walk.

**Read progress in bytes, not files.** The corpus is sorted largest-first, so file 500 of 9,981 is
5 % of the files and 50 % of the work. `THERMAL_CORPUS_PROGRESS` reports both.

The environment variables a run takes: `THERMAL_CORPUS_TESTS`, `THERMAL_CORPUS_DATA`,
`THERMAL_CORPUS_PROGRESS`, `THERMAL_CORPUS_SHIPS` and `THERMAL_CORPUS_MAX_MB`. Off is spellable in
`THERMAL_CORPUS_TESTS` every way anyone reaches for — empty, `0`, `no`, `off`, `false` — which is
the standing rule [rules.md](../../docs/rules.md) `C8`, and was not true until `CorpusGuardTests`
existed to say so.

---

## What is in the corpus directory, and what is beside it

Pruned 2026-08-23, from 68 GB in 33,773 files to 32 GB in 8,144 blueprints. **Every blueprint that
has ever produced a ship is still there**, and the three directories say what each holds:

| Directory | Holds | Why |
| --- | --- | ---: |
| `corpus` | 8,144 `bp.sbc` | Every blueprint known to yield a usable ship. 32 GB |
| `corpus-barren` | 1,840 `bp.sbc` | Parsed and yields nothing: 1,742 rejected as modded, 53 under the 25-block floor, 3 the parser cannot read. 12 GB |
| `corpus-oversized` | 8 `bp.sbc` | Over `THERMAL_CORPUS_MAX_MB`, so a walk has never read them and a machine with room can. 4.5 GB |

**20.6 GB of it was the download's leavings and is deleted rather than moved**: 7,205
`.sbcB1`–`.sbcB5` autosave backups, 3,904 `_legacy.bin` archives, 6,209 thumbnails, 185 `.sbcPB`
script backups and a handful of image-editor files. Nothing has ever read any of them, and the
`.bin` were checked to be redundant rather than assumed to be — see below.

**The barren set is moved rather than deleted, and the reason is a rule this repository already
has.** Pruning them from disk would bake today's two filters — `IsVanilla` and the 25-block floor —
into the population, and [backlog.md](../../docs/backlog.md) `F14` records that the floor has never
been varied to see whether it moves a population figure. Moving costs a rename to undo; deleting
costs a re-download.

**And the inference was checked rather than trusted, which is the whole reason to keep this
paragraph.** The barren set was first identified by subtracting the paths in `ships.csv` from the
blueprints on disk — an inference from two runs, one of which predates the per-blueprint resume
record and could therefore have *lost* ships it never reached. Parsing the 1,852 said so: **12 were
usable ships**, and they are back in the corpus. `dotnet run --project tests/Thermodynamics.Sim --
corpus --list --path <dir>` is what names them, because a scan that answers *twelve* and cannot say
which twelve is an answer that has to be taken on trust.

**Three of the 3,904 legacy archives had never been unpacked, and nothing said so.** A blueprint
published before Steam's current UGC system arrives as a zip holding `bp.sbc` and a thumbnail;
three of them have lost the leading characters of their entry names — `p.sbc`, `.sbc`,
`humb.png` — and `Unpack`'s exact test for `bp.sbc` skipped all three silently. It matches the
extension now (`Blueprints.IsLegacyBlueprintEntry`, checked by `CorpusArchiveTests`), and two of
the three recovered blueprints are usable ships that had never been in any run.

---

## The retest set

`panel.py` picks extremes and `typical.py` picks the middle, and they answer different questions.

```
python3 tools/corpus/typical.py out/census-2026-08-21/census.csv \
                               out/corpus-2026-08-21/outcomes.csv
```

A dial sweep needs the largest lever it can find, so the panel carries the most buried hull, the
least surface per kilowatt and the hulls where one block type is most of the heat. **A retest asks
whether a change broke the ships people actually fly**, and a regression judged only on outliers is
a regression judged on hulls nobody built on purpose. `typical.py` writes `typical.csv`: 40 ships
that each carry thrust, power, an airtight room and 100 kW of waste at full load, that fit the same
30,000-block budget, and that sit nearest the population median on the four quantities the census
found decide an outcome — scored on their *worst* axis rather than their mean, because a hull median
on three and extreme on the fourth is not typical. Drawn across four size bands per grid size, so a
retest is not accidentally all frigates. 3,816 of 8,141 ships pass the six tests.

`ConductanceRetestWalk` is what reads it. The walk runs the set through seven scenarios in the
shipped world and in four counterfactual ones, each restoring part of `Data/Cubes.xml` as it stood
before conductance became real W/(m·K), and `retest.py` reads what comes back.

```
THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=$PWD/out/retest-2026-08-23 \
    dotnet test --filter "FullyQualifiedName~ConductanceRetestWalk"
python3 tools/corpus/retest.py out/retest-2026-08-23
```

Two hours on a 32-core machine, streamed per ship with a `done-retest.txt` beside the data, so
relaunching resumes and there is nothing to pass. It writes `reach.csv` before it runs anything —
how many blocks each arm actually retunes — because an arm that reaches nothing reports *no change*
in exactly the shape of an arm that reached everything and changed nothing, and one of the four
reaches nothing here by construction: the corpus filters admit vanilla ships, so not one of the
forty carries a coolant pipe or a radiator. Those two are measured on a rig instead —
`dotnet run --project Thermodynamics.Sim -- conductance`.

`PairSweep` reads the same set for a different question. `G8` — the significance window — is a claim
about two dials interacting, and every sweep before it moves one; this runs a grid of conduction
against the clock and `pairs.py` scores `G8` on every cell beside the criteria a cell must not break
to be usable.

```
THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=$PWD/out/pairs-2026-08-23 \
    dotnet test --filter "FullyQualifiedName~EveryPairOfConductionAndClockGetsAMeasuredCell"
python3 tools/corpus/pairs.py out/pairs-2026-08-23
```

**And the load pass, which is the third question the class answers.** It runs three load cases. `full-electrical`
charges every jump drive for the whole run and `full-electrical-charged` never charges one; drives
are 71.3 % of the corpus's full-load waste heat, so those two bracket what a loaded ship makes.
**`jump-charge` is the one `G8` is scored on**: a drive fills in 421.9 s — 3 MWh at 32 MW with 0.8
efficiency, every figure off the game's own definition — so the scenario charges for exactly that
and then holds, which is the event rather than a bound on it. Conduction and the clock are
both transport; `EveryLoadAndClockPairGetsAMeasuredCell` moves how much heat a ship makes against
the clock instead, and `load.py` scores the same criteria. Ten minutes on the retest set. It imports
`pairs.py`'s thresholds and censoring rather than restating them, because two scorers that drift
apart disagree about the same criterion silently.

```
THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=$PWD/out/load-2026-08-23 \
    dotnet test --filter "FullyQualifiedName~EveryLoadAndClockPairGetsAMeasuredCell"
python3 tools/corpus/load.py out/load-2026-08-23
```

**The same class carries the air pass, and it is a different question with a different filter.**
`G6` is a cost criterion and the grid above is three vacuum scenarios, where substeps are cheap;
the budget is spent on convection. `EveryCandidateCellIsPricedInAir` runs the shipped pair and the
four cells that satisfy `G8` through four atmospheric scenarios, and `air.py` scores `G6` on each.
Two minutes on the retest set, five on the panel — air runs are short because the scenarios are.

```
THERMAL_CORPUS_TESTS=1 THERMAL_CORPUS_DATA=$PWD/out/air-2026-08-23 \
    dotnet test --filter "FullyQualifiedName~EveryCandidateCellIsPricedInAir"
python3 tools/corpus/air.py out/air-2026-08-23
```

It was run on both populations on purpose. The two answer within 0.3 % of each other, which is what
says the `G6` breach it found is a property of the configuration rather than of a sample — the one
check available while [backlog.md](../../docs/backlog.md) `F11` stands and the corpus itself has
never seen air. Point `THERMAL_PANEL` at `tools/corpus/panel.csv` for the second population.

Under two hours for the first sixteen cells, resumable the same way, and the grid grew to
twenty-five in two searches guided by what the earlier cells measured. **Not a full grid**: the two
edges are the single-dial curves, so the interaction is testable as *is the interior what the edges
predict* rather than assumed; the interior is the equal-cost diagonal — substep demand goes as
conductivity × clock — plus the pair [balance.md](../../docs/balance.md) projected and the four
cells bracketing it; and the two corners are where the measured curves said a cell satisfying `G8`
would have to be. **Run length scales with the clock**, capped at twice `G8`'s own bound, because
a fixed ceiling reports a still-climbing hull as settled at the ceiling and a censored settling time
cannot fail a criterion about settling.

**A hull that never crossed critical is censored above, not dropped.** The first reading of this
grid took the crossing median over the hulls that crossed and reported two cells at conductivity ×8
as satisfying `G8` — where 27 of 40 hulls never reach critical at all. `pairs.py` now orders a
non-crosser past every crosser, exactly as it already ordered a non-settler past the recovery bound,
so a cell where fewer than half ever cross prints `censored` and has no median (`E9`).

**`provenance.py` weights a census by where its heat's numbers came from.** Every waste fraction in
`Cubes.xml` states a provenance ([definitions.md](../../docs/definitions.md#every-waste-fraction-says-where-it-came-from-and-most-of-them-say-invented)),
and counting those says how much of the *file* is sourced rather than how much of the *heat* is. It
reads a census `composition.csv` — per ship, per subtype, watts wasted at full electrical load — and
prints both, plus which block types carry the invented share.

```
python3 tools/corpus/provenance.py out/census-2026-08-21/composition.csv
```

Seconds, on a census that already exists. A block that generates power is weighted by its producer
fraction and everything else by its consumer one, which is the difference between a hydrogen engine
reading as sourced and as invented — six per cent of the corpus's waste heat.

**`cellsize.py` asks which cell size is the harder one to cool**, which this repository had
answered from the arithmetic of a cell face rather than from any ship. It splits a census `census.csv`
by the `large` flag and reports every column that bears on it, with the hull path read *per kilowatt*
— a small-grid ship's hottest block has 0.77× the conductance a large one's has and a tenth of the
heat, so the raw column says *behind* about a term it is **19.75×** ahead on.

```
python3 tools/corpus/cellsize.py out/census-2026-08-25/census.csv
```

Seconds, on a census that already exists, and it adds nothing to the census's basis. `CellSizeLab`
is the other half — per block, from the definitions — and [balance.md](../../docs/balance.md#what-a-small-cell-is-behind-on-and-what-it-is-not)
holds the finding the two make together.

```
python3 -m unittest discover -s tools/corpus -p 'test_*.py'
```

`reproduce.py` answers the other question about two datasets: not whether the population moved, which
is `--baseline`, but whether two runs that overlap wrote the same numbers on the ships they share
(`E7`). A walk is restarted rather than resumed whenever the code moved underneath it (`M1`), and the
overlap with what the old run finished is then a free reproduction check — on 2026-08-25 the cap
walk's first **552 rows matched the abandoned partial exactly**, on all 22 compared columns, which
turned *three commits touched the harness and none was checked for behaviour* from an assumption
into a measurement. It reports rows only one side has rather than dropping them, because two
datasets with nothing in common otherwise print a perfect reproduction.

`test_cellsize.py` pins the one thing a bug in `cellsize.py` could invert without a symptom — which
cell size a column favours — on rows written to know the answer, including the hull-path column whose
obvious reading is the wrong way round. `test_scoring.py` pins that rule and the two thresholds `G8` is scored at, `test_provenance.py`
pins the four provenance counts against the ones `AuthoredWasteTests` pins, so the two readers of
one grammar cannot drift apart quietly (`D3`), `test_pace.py` pins what a progress file can be
asked — that a repeated final line is not a stall, that the ratio is taken over the files two walks
share rather than the time they ran, and that the block-share estimate is reported as the spread it
has, `test_reproduce.py` pins that a comparison
with nothing in common is not a reproduction, `test_tool_imports.py` pins that a consolidation
cannot delete a name a sibling tool still imports — the break that left `load.py` unable to import
for six days under a green suite, caught by parsing rather than importing because several tools run
a report at module level, `test_knob.py` pins the two ways a dial sweep is read wrongly — a level compared against the population rather than against *the same ship* at the shipped level, and a dial with an `OnlyType` judged over every ship it was run on rather than the ones it can reach, which reports its reach as its effect and in the direction that makes every dial look harmless. `test_cruise.py` pins the derivation `K12` rests on — that the speed it returns is the one where drag equals thrust, that it is a square law in both thrust and area, and that a ship with no thrust has *no* cruise speed rather than one of nought, which would put 1,785 stations into the bottom of the distribution and make every quantile below the median a statement about stations. `test_dragfit.py` pins what the drag milestone's criterion *is* — that only hulls able to lift themselves are in the population, since the worst ceilings in the whole census are stations with one or two thrusters and a hull that never flew is not one drag broke; that both halves must hold or neither does, because a large share with a high ceiling is a modelling error and a bad pair is a tuning one; and that a hull whose ceiling sits exactly on the criterion speed is exactly the hull whose drag there equals its thrust, which is what says the two halves are the same model. `test_floor.py` pins the two ways the floor report could quietly say the wrong thing — mixing the
arms, which would report the mechanism against itself and read as *the floor is free*, and averaging
the price over cells the floor never touched, which on the 2026-08-29 walk halves it (p99 60.9 K
over the engaged cells against 28.3 K over every cell walked) — and that `C30`'s registered bands
live in `scoring.py` rather than in a sentence. `test_cap.py` pins the ways the cap report could read
a sampled walk as a population — a stray ship dropped rather than given a default weight of 1, a
weighted giant moving a percentile exactly as the twenty rows it stands for would, the identity
check deliberately left unweighted because it is an assertion about the walk, and a verdict a
sample cannot reach recorded as `unscored` rather than as a pass — and `test_core.py` pins the two
properties the core corpus is exact on — that a weight of one is the unweighted percentile and that an integer weight
means what repeating the row means, which an earlier draft satisfied the first of and not the
second — together with the guard that stops a sampled dataset being read as a population. Changing any of them fails a check rather than moving a number nobody is watching. They are the
only checks over the scorers and are not part of the `dotnet test` suite; run them when a scorer
changes.

---

**Read the peak columns with the censoring in mind.** The harness never destroys an overheating
block, so anything above critical kept generating for the rest of the clock. See the deliberate
limit in [known-issues.md](../../docs/known-issues.md).

---

## Change log

| Date | Change |
| --- | --- |
| 2026-09-27 | Added aerothrust.py, its optional pinned numerical dependencies and independent directional-capacity controls. Documented certificates, tolerances and the absence of a population verdict. |
| 2026-09-24 | **The shape walk is retired with the shape term it measured.** `K25`'s strip removed the Newtonian shape factor from the friction row and the aerodynamic force derived from it, so `CorpusShapeWalk`, `shape.py` and `test_shape.py` are deleted and the census no longer carries `shape_factor` or `lift_over_drag`. The committed [`summary-shape-2026-08-31.csv`](summary-shape-2026-08-31.csv) stays as the record the pages quote. `cruise.py` and `dragfit.py` keep working on a census without the column — every hull reads as shape 1 — and `cruise.py`'s default coefficient is renamed `RETIRED_DRAG_COEFFICIENT`, because no coefficient ships until the rebuilt force does. Separately, `provenance.py` read `Cubes.xml` from its pre-MDK2 path and four of its checks had been failing, and `test_cruise.py` had pinned the coefficient at 0.5 for two weeks after it became 1.54; 205 checks pass. |
| 2026-08-28 | **`cap.py` reads a sampled walk with weights, and says which halves a sample cannot answer.** `A13`'s last open half is the cap walk, and the core corpus walks it in 78 minutes rather than eleven hours — but it is a *weighted* sample, and this report had no weights: read unweighted it would have printed a complete-looking population whose giants are outnumbered twenty to one. The dataset is recognised by `scoring.is_core_walk` rather than by a flag, every population figure is weighted and named `weighted`, and the identity check deliberately is not — *did the capped arm cap* is an assertion about the walk, and weighting it would hide a broken pair on a ship of weight 1 behind twenty good ones on a ship of weight 20. **Two halves report `?` rather than passing**: a maximum is one observation and cannot be sampled, so the cost prediction's max-under-10-K half is unscoreable on a sampled walk, and an unscorable half never rescues a failing one (`E8`). `--csv` gives the walk a committed source, from the same `provenance.summary_rows` `verdict.py` uses (`P5`, `E5`); the 2026-08-25 summary reproduces byte for byte through it. `core.py --score` now splits a paired walk to the arm that ships, which it had not — a mixture of two configurations has every column one configuration has (`M1`, `P6`). |
| 2026-08-28 | **`pace.py`'s ratio reads every mark of a resumed walk, not just its first slice.** The morning's fix made a resumed walk's counts cumulative, and cumulative counts do not land on the reference's ten-file grid — so the intersection was empty after the first resume and **21 of 268** marks were in use. The reference is interpolated at the subject's own counts now, bounded to five marks because the corpus is largest-first. The air re-take reads 2.94x over 3,166 files against 3.05x over its first 400. |
| 2026-08-28 | **The standing panel is re-picked** on the corrected census and the re-surveyed outcomes (`C32`, unblocked by `A13`'s survey half finishing). 52 ships from 50, `oxygen-heavy` added, and 16 of 24 rules pick different hulls — the census and the outcomes both moved under it, so this is not an isolation of the rule fix. The commands above now name the datasets it was built from. **No dial sweep taken before it is comparable with one taken after** (`M1`). |
| 2026-08-28 | **`pace.py` reads a walk taken in slices.** Relaunching to resume is the documented normal path and it writes many blocks into one progress file, each restarting its own file count; read literally, the air re-take's ninetieth file of its sixth slice sat at 272 minutes after the first slice's first mark, and the file written to refuse a bad estimate reported **9.45x** per file where the honest figure is 3.05x. Counts continue from the resume line, the gap between slices is not walked time, and a slice's clock starts at its resume line — the work before a slice's first mark is real walking at the walk's own rate, sixty-four minutes of it over five boundaries. Pinned by a sliced fixture and the same walk in one run reporting the same elapsed and the same ratio. |
| 2026-08-28 | Added the sixth way a sweep dies, which costs the dataset rather than the run: **do not rebuild Release while a walk is slicing.** Slices load `--no-build`, so a build between two of them gives the dataset's halves different builds. `provenance.txt` makes that visible rather than harmless, which is the only reason it is recoverable. |
| 2026-08-28 | Said that a walk's cost belongs to the build as well as to the corpus. The `A13` air re-take is **1.78x per file** over the walk it replaces, because the blocks that reader built as armour are generators and doors now and this walk runs to equilibrium — so the 1.95x cap-to-air ratio is a ratio *within one build* and understates the cap walk's cost across a build that changed what the corpus contains. |
| 2026-08-28 | **A dataset that spans two builds says so, and the reader that could not see it is fixed.** `provenance.txt` has appended a block per slice since it was written, and its writer's summary said as much; `provenance.py` read only the last `Cubes.xml` line, so the 2026-08-25 survey — five slices, two `Cubes.xml` hashes, two `Loops.xml` hashes — reported as measured against the current file. One format, two readers, drifted (`D3`). `spans_several_definitions` is the lookup and the report prints it. **It does not decide whether the split matters**: for that survey it does not, because the only change in the window is the two radiators' emissivity and no corpus ship carries a radiator or any other block the mod adds. Also corrected: this page named `Materials.xml` as the second hashed file for months, and the three the code hashes are `Cubes.xml`, `Loops.xml` and `Planets.xml`. |
| 2026-08-26 | **`verdict.py` says when a dataset is partial**, which `E4` has asked for since it was written and nothing did. It counts the corpus's blueprints — recursively, because a workshop item can be a collection of several and counting one level deep misses fourteen of this corpus's and reports a population *smaller* than the walk that covered it — and prints `*** PARTIAL: n of m ***` above everything else. Where the corpus is not on the machine it says the population is **unknown** rather than assuming the dataset is whole; `--population <n>` states it. The threshold is 95 %, because the blueprint filters reject about one hull in a thousand and demanding equality would call every finished walk partial. |
| 2026-08-26 | Added [`cellsize.py`](cellsize.py) and `test_cellsize.py`: which cell size is the harder one to cool, split off a census rather than argued off a cell face. Every column favours small grids — 2.47× the exposed skin per kilowatt, a fifth as much of it buried, **19.75×** the hull path per watt for the hottest block — which is the opposite of what [balance.md](../../docs/balance.md) had written down and what `C43` was blocked on. The hull path is read per kilowatt because the raw column says the reverse, and the test is built around that one column. |
| 2026-08-25 | `verdict.py` refuses to score a criterion its dataset is too coarse to state, and `scoring.resolves` is the rule. A criterion given as a share of the corpus needs a corpus that can tell its two sides apart: on the thirteen ships of a partial survey slice one ship is 7.7 %, so *nothing critical* and *one per cent critical* are the same reading, and `G1` came back `[HOLDS]`. It reads `[  ?  ]` now, with what the dataset would need. It is a resolution test rather than a confidence one and says so — a partial walk that passes it is still a partial walk. The 8,142-ship dataset is unaffected. |
| 2026-08-25 | `panel.py`'s per-type rules key on `type_id` rather than on a substring of the subtype, and print each pool's size so an empty one is said rather than inferred (`E8`). No vanilla reactor's subtype contains the word *reactor*, so `reactor-heavy` had been choosing from 12 ships out of 5,728 — and the twelve were `LargePrototechReactor`, which the game types as a `HydrogenEngine` and which wastes 0.60 where that rule's own note says 0.01. Added `oxygen-heavy`; dropped the dead `thrust` share, since `thrust-heavy` reads the census's `thrust_n`. `test_panel.py` holds the mapping. `panel.csv` is unchanged until a survey is re-run against the corrected census. |
| 2026-08-25 | `provenance.py` restates a census only when the census predates the change, read from the `provenance.txt` beside it rather than assumed. Every dataset on disk was older than the fractions that ship, so restating unconditionally was right until the day one was re-taken — and then it discounted the oxygen generator twice, on a census that had already measured 0.40. A dataset with no provenance still reads as older, which is what every dataset taken before that file existed is. |
| 2026-08-25 | Added `censusdiff.py` and `test_censusdiff.py`. `reproduce.py` asks whether two walks that saw the same ship wrote the same row; this asks the opposite question, for a census re-taken because something was *meant* to move — per column, per block type, and scored against bands given on the command line so a registered prediction is computed rather than read off by eye (`E5`). Ships only one side holds are reported rather than dropped, because a re-take that lost half the population would otherwise print a clean delta over the half it kept, and the tests cover that case rather than the subtraction. |
| 2026-08-25 | `provenance.py` takes `--type <TypeId>` and reports that type's share of the waste of the ships that carry it, because the share it already printed was answering a different question. The oxygen generator is **0.38 %** of a loaded fleet's waste and a median **48.1 %** of the waste of the 2,277 census ships that carry one — a ratio of aggregates against an aggregate of ratios (`E6`), and only the second is about the player who built the block. The restatement table is now applied in one place and covers that type's 0.6 to 0.40. |
| 2026-08-25 | Wrote down that a full sweep is rare by intent and that the smallest run which answers the question comes first, with the three levers and which is actually worth reaching for. The measured part is that **selection is the weakest of them**: cutting the floor walk's 294 ships to the 98 over 60,000 blocks — the only band where the floor's error exceeds 1 K — drops two thirds of the ships and 34 % of the work, because cost goes with blocks. Stopping early is the strong lever, since largest-first puts the informative hulls at the front. |
| 2026-08-25 | Recorded the `pgrep` half of the pattern-matching trap beside the `pkill` half: a shell loop that waits on a walk by name contains the name, so it matches itself, never exits, and makes a later `pgrep` answer that the walk is still running after it has finished. |
| 2026-08-25 | `cap.py` splits its cost figure on whether the control had actually stopped moving, beside the registered statistic. The settle test is an average over a chunk and tolerates 0.25 K a minute; the split uses the instantaneous rate at the end. On the cap walk's first 360 ships the two differ by two orders of magnitude, which is a statement about the instrument rather than about the cap. |
| 2026-08-25 | `cap.py` now scores all four registered predictions rather than three. The benefit had been scored only as *does `G6` pass*, which is the criterion and not the prediction — the pre-registration's falsifier runs both ways, and a p99 under 1.5 M would falsify the projection while the criterion passed. The cost had printed its figures and judged nothing. Both bands live in `scoring.py` beside the decision rule, and the two are kept apart: a prediction can be falsified while the decision is unchanged. |
| 2026-08-25 | `cap.py` scores the reach prediction over the air scenarios, which is how it was written — *the cap holds back 3-10 % of all blocks in air*. It had been counting the vacuum anchor too, where the cap is expected to bind least, which dilutes the share by a quarter and scores a band nobody registered. Both figures print; the band is read against the air one. |
| 2026-08-25 | `verdict.py` prints a dataset's `provenance.txt` and records it into `--csv`, so a figure quoted from a committed summary carries the build it was measured on. A dataset without one is named as such rather than assumed current. |
| 2026-08-25 | Every walk now writes `provenance.txt` beside its outcomes: the commit and a digest of the two definition files whose contents decide what it measured. Hooked into `CorpusFixture.Sweep` rather than into each walk, because a walk that has to remember is a walk that will not. |
| 2026-08-25 | Renamed `air.py`'s ratio column from *vs shipped* to *vs baseline*, and made it print which cell ships. The column compares every cell with the sweep's own control, conductivity x1 at a clock of 225 — which stopped being the shipped configuration when `C24` shipped x4 at 90, a row of the same table. The arithmetic was never wrong; the word was. |
| 2026-08-25 | Fixed `verdict.py` reading a paired walk's second arm as duplicate rows. It keyed a row by ship and scenario, so `CorpusCapWalk`'s capped arm looked like the same run written twice: half the dataset was dropped, under a note blaming a resume that had not happened, and the arm it kept was the right one by accident. The arm is part of a row's identity now, and a paired dataset is scored on the one that ships with both named on the first line (`M1`, `P6`). |
| 2026-08-25 | Added [`reproduce.py`](reproduce.py): whether two walks that overlap wrote the same numbers on the ships they share. Its first use was the cap walk's restart, whose 552 shared rows matched the abandoned partial exactly on all 22 compared columns. |
| 2026-08-28 | Added [`core.py`](core.py) and the committed [`core-corpus.csv`](core-corpus.csv): a stratified, weighted sample that walks in 36 minutes where the corpus takes 5.1 hours. The measurement that shaped it is that the 11.5 % of ships over 10,000 blocks carry **79 % of the cost and all 712 of the over-allowance runs**, so the obvious cut deletes the finding — a size-truncated corpus reads `G6`'s work p99 **82 % low** and its over-allowance count as zero. Scored out of sample against two walks it was not drawn from, work p99 lands within 0.9 % and 4.5 %. |
| 2026-08-28 | **Corrected the cap-to-air ratio, which the finished walks refute** (`E10`). 1.95x was measured over the fifty files two walks' progress records share; over the whole corpus the same two walks are 228.8 minutes against 104.5, so **2.19x**, and the cap walk on the re-taken air walk is about eleven hours rather than three and a half. A fifty-file sample of a largest-first walk is fifty capital hulls. |
| 2026-08-25 | Added the one way a sweep costs a session rather than a run: a walk in progress fails the suite's wall-clock tests, because they are measuring a machine the walk is using every core of. Measured — the suite was green before the cap walk and `SolverCostPerLinkStaysProportional` was 3.14× its limit during it. |
| 2026-08-25 | Added [`pace.py`](pace.py): what a running walk will cost, and the check that says whether the estimate means anything. The block-share rate that abandoned the first cap walk is reported as the spread it has and then run over the finished air walk, where the answer is known. |
| 2026-08-25 | Added the *Looking for* table. A reader arriving here often wants the criteria these scripts score rather than the scripts. |
| 2026-08-24 | **`cap.py` scores the paired walk against the four predictions registered before it ran**, and the decision rule itself is in `scoring.cap_decision` rather than in prose — the two thresholds it compares against are the ones this repository already set for other reasons, 0.03 K accepted by `C19` and 0.6 K refused by `C3`, and having them under test is what stops a threshold being chosen once the data is in (`E1`, `E11`). Four sections: the identity the predicted benefit rests on, the work percentiles per arm against the shipped allowance, the peak deltas per scenario, and the cap's reach with the control's floored count printed beside it as the check that a control is a control. A row with no partner is counted and dropped rather than compared against a default. |
| 2026-08-24 | **`CorpusCapWalk`: the same four air scenarios, run twice, with `MaxSubstepsPerBlock` off and at 6.** `C3` and `G6`'s failing cost half are one question — the cap is the only lever that lowers a step's work — and `C3` was undecided only because its cost had been measured on one hull. **Both arms run to the same simulated clock**, which is why it is a walk rather than a join onto `F11`'s dataset: `Battery.Run` stops at equilibrium, in air that is usually 120 s of 1,800, and the effect being measured is a hundredth of a kelvin against a stopping tolerance of a quarter of one. `Battery.RunForSeconds` takes the control's elapsed clock; `outcomes.csv` gains `run_seconds` and `cap` so a reader can see where a run stopped and which arm it is. |
| 2026-08-24 | **`scoring.step_work` takes the walk's `substep_cost` column, and the two committed summaries have lost their `G6 work` rows.** The unit is `links + 4 × nodes` and no walk before today recorded a link count; the scorer was handed `joints`, which counts rotors and pistons *between grids*, so the expression evaluated to `4 × nodes` and every step-work figure published from either dataset is the node half alone — 1.51× low on a 2,000-block census hull. `outcomes.csv` carries `links` and `substep_cost` now, the second being `ThermalSimulation.SubstepCost` for the assembly's worst grid, since the allowance is per grid. Neither existing dataset can be rescored, so both summaries were regenerated and their work rows are simply gone: a statistic that vanishes between two runs is what `--baseline` reports as a finding, which is the right reading here (`P2`). `G6` prints as one half unmeasured rather than as a failure, and the per-scenario table keeps its demand columns with an em dash in the work ones. |
| 2026-08-24 | `scoring.censored_median` is the one definition of the statistic `G8` is scored on, and `pairs.py` and `load.py` both call it — two copies of it had already drifted at the even-length boundary. Added `joint_median_stability`, the bootstrap that says how often a resampled fleet still satisfies a two-halved criterion, drawn once so the two medians stay paired; `load.py` prints it as a `holds` column. It is what decided `C12`: two cells with the same median were 37 % and 76 % likely to hold. |
| 2026-08-24 | **`THERMAL_CORPUS_DATA` is resolved against the test host's working directory, not the repository root.** A relative path writes a whole dataset into `.build/…/bin/Release/net9.0/out/` and the run passes, having recorded nothing where anyone will look for it. Pass an absolute path. |
| 2026-08-23 | Gave the load pass `jump-charge`, the case `G8` is scored on: the drives charge for the 421.9 s their own definition implies, finish, and the ship holds. `Battery.Scenario` gained `Then` and `ThenAfterSeconds` for it, which also generalises the name-matched special case `recovery` had been carried by since it was written. |
| 2026-08-23 | Gave the load pass its second load case, `full-electrical-charged`, which is what answers `F13`: with the drives full nothing in either of `C12`'s routes has a median crossing at all. The dial that makes it possible found a defect on the way — the jump drive was filed as a *tool*, so `State.Consumers` never reached it — and the change is neutral on 1,794 rows of the previous dataset. |
| 2026-08-23 | Added the load pass and `load.py`: the load against the clock, which is the dial that is not transport. Three grids share one run loop, one row format and one resume-record-per-pass now; the row format gained a `waste` column, and a cell that does not move the load keeps the name the two earlier grids wrote so their records still match the cells they were taken on. |
| 2026-08-23 | Added the air pass and `air.py`: the same cells `pairs.py` scores for `G8`, priced in the environment `G6` is decided in. It found the shipped configuration over the substep cap at 200 m/s and every atmospheric figure in [balance.md](../../docs/balance.md) exactly half, taken before `Frequency` went 8 to 4 ([backlog.md](../../docs/backlog.md) `C19`). The two sweeps share one run loop and one row format; what differs is which cells and which scenarios, and each keeps its own resume record so one cannot mark a ship done for the other. |
| 2026-08-23 | Scored `G8` and found it satisfied at conductivity ×4 with `HeatTimeScale` 80–120. Fixed the defect that had to be fixed first: `pairs.py` took the crossing median over the hulls that crossed, so conductivity ×8 — where 27 of 40 hulls never reach critical — read as the grid's best cell. Non-crossers are censored above now, `test_scoring.py` pins it, and `verdict.py`'s time-to-critical table says in words that its quantiles are over the ships that reached. |
| 2026-08-23 | Pruned the corpus directory from 68 GB to 32 GB and wrote down [what is in it](#what-is-in-the-corpus-directory-and-what-is-beside-it). 20.6 GB of leavings deleted, 1,840 barren blueprints and 8 oversized ones moved aside rather than deleted because `F14` still has the 25-block floor untested. The inference that a blueprint is barren was checked by parsing all 1,852 — 12 were usable ships and are back — which is also what added `corpus --list`. Fixed `Unpack`, which had silently skipped three legacy archives whose entry names are truncated. |
| 2026-08-23 | Added `PairSweep` and `pairs.py`: conduction against the clock, sixteen cells, scoring `G8` ([backlog.md](../../docs/backlog.md) `C12`). The three sweeps' CSV reading, blueprint resolution and resume record are one `ShipSet` now rather than three copies. |
| 2026-08-23 | Wired the retest set to something: `ConductanceRetestWalk` runs it against the world before conductance became real units, and `retest.py` reads the result against G1, G2 and G5 as `verdict.py` already computes them ([backlog.md](../../docs/backlog.md) `C2`). |
| 2026-08-23 | `verdict.py` prices a `G6` breach beside the verdict — the marker stays where it was written (`E11`), and a reader sees whether a failure is 0.03 K or a hull integrated wrong. The arithmetic moved to `scoring.py` so a test can call it without running a report. `air.py`'s footer no longer says a refused step floors a block's capacity; that mechanism ships off. |
| 2026-08-23 | Added `provenance.py` and `test_provenance.py`: how much of a fleet's waste heat rests on a fraction nobody sourced. [backlog.md](../../docs/backlog.md) `C21`. |
| 2026-08-23 | Added `typical.py` and [the retest set](#the-retest-set): the panel picks extremes for a dial sweep, this picks the middle for a regression. |
| 2026-08-22 | Split the crossing from the loss everywhere the pages had run them together. The survey report's headline said a ship *loses its first block* after nine seconds where it meant *crosses critical*, its table's `First loss` column was the crossing and its `Blocks lost` column was blocks over critical, and the bench page repeated all three. `seconds_to_first_loss` is packed and shown beside the crossing, and reads as absent on every dataset collected before it existed. |
| 2026-08-22 | The resume is a record rather than a count. A walk writes `done-<walk>.txt` as it finishes each blueprint and reads it on the next start, so relaunching is the whole procedure and `THERMAL_CORPUS_SKIP` is gone ([backlog.md](../../docs/backlog.md) `H2`). Said that off is now spellable in `THERMAL_CORPUS_TESTS` every way anyone reaches for (`H3`). |
| 2026-08-22 | Wrote down [the ways a full sweep dies](#the-ways-a-full-sweep-dies), which [balance.md](../../docs/balance.md) had been pointing at [backlog.md](../../docs/backlog.md) for and which no page in the tree carried — it had survived only as a note kept outside the repository, which is the failure [rules.md](../../docs/rules.md) exists to prevent. |
| 2026-08-22 | Added this change log. |
| 2026-08-22 | Made every recorded corpus figure read by something, and closed the pass. |
| 2026-08-21 | Opened the page against the 2026-08-21 datasets: what each script reads, what it prints, and why the panel's every pick names its own rule. |
