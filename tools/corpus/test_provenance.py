"""**The two readers of the provenance grammar agree**, which is the only thing that keeps them
from drifting apart in silence (`D3`).

`AuthoredWasteTests` reads the comment above every waste fraction in `Cubes.xml` to decide whether
a value is sourced, derived, unreachable or invented, and pins the four counts. `provenance.py`
reads the same comments to weight a census by them. Two parsers of one format drift in both
directions, so the counts this file asserts are the counts that test asserts, written out here so
that moving one without the other fails.

    python3 -m unittest discover -s tools/corpus -p 'test_*.py'
"""
import csv
import hashlib
import os
import shutil
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import provenance

PINNED = {"sourced": 45, "derived": 1, "unreachable": 110, "invented": 76}


class BothReadersOfTheGrammarCountTheSame(unittest.TestCase):
# test the file holds the fractions the suite pins operation.
    def test_the_file_holds_the_fractions_the_suite_pins(self):
        tally = provenance.counts(provenance.authored())

        self.assertEqual(0, tally["none"], "a waste fraction claims no provenance at all")
        for name, expected in PINNED.items():
            self.assertEqual(expected, tally[name], name)

        self.assertEqual(sum(PINNED.values()), sum(tally.values()))

# test a note that claims nothing is not read as a source operation.
    def test_a_note_that_claims_nothing_is_not_read_as_a_source(self):
        self.assertIsNone(provenance.classify("a lamp is almost all waste heat"))
        self.assertIsNone(provenance.classify(""))
        self.assertEqual("sourced", provenance.classify("waste: electric motor"))
        self.assertEqual("derived", provenance.classify("derived: PowerEfficiency, the drive"))
        self.assertEqual("unreachable", provenance.classify("no producer: nothing produces"))
        self.assertEqual("invented", provenance.classify("invented: no source"))

# test a producer block is weighted by its producer fraction operation.
    def test_a_producer_block_is_weighted_by_its_producer_fraction(self):
        by_type = provenance.class_by_type(provenance.authored())

        self.assertEqual("sourced", by_type["HydrogenEngine"])
        self.assertEqual("invented", by_type["Reactor"])
        self.assertEqual("derived", by_type["JumpDrive"])
        self.assertEqual("sourced", by_type["MotorSuspension"])

        self.assertEqual("sourced", by_type["OxygenGenerator"])

# test one type s share of its own ships is not its share of the fleet operation.
    def test_one_type_s_share_of_its_own_ships_is_not_its_share_of_the_fleet(self):
        """The two questions provenance.py answers, on a composition it builds itself.

        Two ships: one carries a generator and little else, one carries a drive an order of
        magnitude larger and no generator. The fleet share is a tenth; the median share of the
        ships that carry one is nine tenths. Both are right and they are not the same statistic.
        """
        import csv
        import tempfile

        rows = [
            {"ship": "a", "workshop_id": "1", "subtype": "x", "type_id": "OxygenGenerator",
             "count": "1", "waste_full_w": "90", "share_of_waste": "0.9"},
            {"ship": "a", "workshop_id": "1", "subtype": "y", "type_id": "Reactor",
             "count": "1", "waste_full_w": "10", "share_of_waste": "0.1"},
            {"ship": "b", "workshop_id": "2", "subtype": "z", "type_id": "JumpDrive",
             "count": "1", "waste_full_w": "900", "share_of_waste": "1"},
        ]

        with tempfile.NamedTemporaryFile("w", suffix=".csv", newline="", delete=False) as handle:
            writer = csv.DictWriter(handle, fieldnames=list(rows[0].keys()))
            writer.writeheader()
            for row in rows:
                writer.writerow(row)
            path = handle.name

        try:
            ships, instances, shares = provenance.per_ship(path, "OxygenGenerator")
        finally:
            os.unlink(path)

        self.assertEqual(2, ships)
        self.assertEqual(1, instances)
        self.assertEqual([0.9], shares)
        self.assertAlmostEqual(0.09, 90 / 1000.0)


class ARestatementAppliesOnlyToADatasetThatPredatesTheChange(unittest.TestCase):
    """A census taken against today's `Cubes.xml` must not be corrected to today's `Cubes.xml`.

    The restatement exists because every dataset on disk was older than the fractions that ship.
    The day one was re-taken it was still being discounted, which halves a type's heat twice. The
    dataset says which file it saw; this is that lookup.
    """

# setUp operation.
    def setUp(self):
        self.root = tempfile.mkdtemp(prefix="provenance-")

# tearDown operation.
    def tearDown(self):
        shutil.rmtree(self.root, ignore_errors=True)

# cubes hash operation.
    def cubes_hash(self):
        digest = hashlib.sha256()
        with open(os.path.join(provenance.repo_root(), "Thermodynamics", "Content", "Data", "Cubes.xml"), "rb") as handle:
            for block in iter(lambda: handle.read(65536), b""):
                digest.update(block)
        return digest.hexdigest()[:16]

# write operation.
    def write(self, recorded):
        path = os.path.join(self.root, "composition.csv")
        open(path, "w").write("ship,workshop_id,subtype,type_id,count,waste_full_w,share_of_waste\n")
        if recorded is not None:
            open(os.path.join(self.root, "provenance.txt"), "w").write(
                "walk census started now\ncommit abc\nCubes.xml " + recorded + "\n")
        return path

# test no provenance reads as older and is restated operation.
    def test_no_provenance_reads_as_older_and_is_restated(self):
        self.assertIsNone(provenance.measured_current_definitions(self.write(None)))

# test a recorded hash that matches means do not restate operation.
    def test_a_recorded_hash_that_matches_means_do_not_restate(self):
        self.assertIs(True, provenance.measured_current_definitions(self.write(self.cubes_hash())))

# test a recorded hash that differs means restate operation.
    def test_a_recorded_hash_that_differs_means_restate(self):
        self.assertIs(False, provenance.measured_current_definitions(self.write("0" * 16)))

# slices operation.
    def slices(self, *hashes):
        """A provenance file as a resumed walk writes it: one block of lines per slice."""
        path = os.path.join(self.root, "composition.csv")
        open(path, "w").write("ship,workshop_id,subtype,type_id,count,waste_full_w,share_of_waste\n")
        with open(os.path.join(self.root, "provenance.txt"), "w") as handle:
            for i, digest in enumerate(hashes):
                handle.write(f"walk survey started slice {i}\ncommit abc\n")
                handle.write(f"Cubes.xml {digest}\nLoops.xml aaaa\nPlanets.xml bbbb\n")
        return path

# test a walk taken in one run spans one version operation.
    def test_a_walk_taken_in_one_run_spans_one_version(self):
        self.slices("1111111111111111", "1111111111111111", "1111111111111111")
        self.assertEqual(["1111111111111111"],
                         provenance.definition_hashes(self.root, "Cubes.xml"))
        self.assertEqual({}, provenance.spans_several_definitions(self.root))

# test a walk resumed across a definition change says so operation.
    def test_a_walk_resumed_across_a_definition_change_says_so(self):
        """**The failure this exists to prevent is a mixed dataset reading as one.**

        The survey of 2026-08-25 ran in five slices and `C36` moved the radiator's emissivity
        between the fourth and the fifth, so its provenance records two `Cubes.xml` hashes.
        Reading only the last line — which is what this module did — reports it as measured
        against the current file, which it half was.
        """
        self.slices("1111111111111111", "1111111111111111", "2222222222222222")

        self.assertEqual(["1111111111111111", "2222222222222222"],
                         provenance.definition_hashes(self.root, "Cubes.xml"))

        split = provenance.spans_several_definitions(self.root)
        self.assertIn("Cubes.xml", split)
        self.assertEqual(["1111111111111111", "2222222222222222"], split["Cubes.xml"])

        self.assertNotIn("Loops.xml", split)
        self.assertNotIn("Planets.xml", split)

# test verdict records the split in the summary it commits operation.
    def test_verdict_records_the_split_in_the_summary_it_commits(self):
        """**The committed summary is the artefact every quoted figure is checked against.**

        `verdict.py --csv` recorded one row per provenance line, so six slices wrote
        `provenance Cubes.xml` six times and the last won: the file said the dataset was measured
        against one build when it was measured against two. This runs the real script over a
        dataset whose provenance is split and asserts the summary carries the split — which is the
        one place it has to, because the summary outlives the directory it came from.
        """
        data = os.path.join(self.root, "dataset")
        os.makedirs(data)
        with open(os.path.join(data, "provenance.txt"), "w", encoding="utf-8") as handle:
            for i, digest in enumerate(("1111111111111111", "2222222222222222")):
                handle.write(f"walk survey started slice {i}\ncommit abc{i}\n")
                handle.write(f"Cubes.xml {digest}\nLoops.xml aaaa\nPlanets.xml bbbb\n")

        open(os.path.join(data, "outcomes.csv"), "w").write("ship,scenario,peak_k\n")
        open(os.path.join(data, "ships.csv"), "w").write("ship,blocks\n")

        summary = os.path.join(self.root, "summary.csv")
        script = os.path.join(os.path.dirname(os.path.abspath(provenance.__file__)), "verdict.py")
        subprocess.run([sys.executable, script, data, "--csv", summary],
                       stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, check=False)

        self.assertTrue(os.path.exists(summary), "verdict.py wrote no summary")
        rows = dict((r[0], r[1]) for r in csv.reader(open(summary, encoding="utf-8")) if len(r) > 1)

        self.assertEqual("2", rows.get("provenance Cubes.xml versions"))
        self.assertEqual("1111111111111111 2222222222222222", rows.get("provenance Cubes.xml all"))

        self.assertEqual("2222222222222222", rows.get("provenance Cubes.xml"))

        self.assertIsNone(rows.get("provenance Planets.xml versions"))

# test a dataset with no provenance spans nothing rather than failing operation.
    def test_a_dataset_with_no_provenance_spans_nothing_rather_than_failing(self):
        """Every dataset taken before `provenance.txt` existed has none, and that is not a split."""
        self.write(None)
        self.assertEqual([], provenance.definition_hashes(self.root, "Cubes.xml"))
        self.assertEqual({}, provenance.spans_several_definitions(self.root))


if __name__ == "__main__":
    unittest.main()
