"""Population coverage, censored quantiles and crash-safe provenance controls."""
import copy
import json
import pathlib
import tempfile
import unittest
from unittest.mock import patch
import aeropopulation as population
import test_aerothrust


def dataset(count=100):
    entries = [dict(path='%d/bp.sbc' % i, bytes=i + 1, sha256='a' * 64) for i in range(count)]
    plan = dict(files=entries, identity_sha256='f' * 64)
    records = [dict(entry=e, identity_sha256=plan['identity_sha256'], status='measured',
                    drag=dict(ceiling_lower_mps=70, ceiling_upper_mps=70.001, right_censored=False, p1='holds')) for e in entries]
    return plan, records


class PopulationTests(unittest.TestCase):
    def test_complete_and_partial_coverage(self):
        plan, rows = dataset()
        full = population.score(plan, rows)
        self.assertEqual(('holds', 'holds'), (full['p1'], full['p2']))
        self.assertEqual(5050, full['bytes_recorded'])
        for partial in [rows[:-1], [], rows[:20]]:
            result = population.score(plan, partial)
            self.assertFalse(result['complete_resolved'])
            self.assertEqual(('unresolved', 'unresolved'), (result['p1'], result['p2']))

    def test_unresolved_and_explicit_exclusions(self):
        for status in ['excluded_small', 'excluded_modded', 'not_self_lifting', 'unresolved']:
            plan, rows = dataset(101)
            rows[-1]['status'] = status
            result = population.score(plan, rows)
            self.assertEqual(100, result['self_lifting_measured'])
            self.assertEqual(status != 'unresolved', result['complete_resolved'])
            self.assertEqual('unresolved' if status == 'unresolved' else 'holds', result['p2'])

    def test_p1_boundary_and_uncertainty(self):
        plan, rows = dataset()
        for row in rows[:5]:
            row['drag']['p1'] = 'overpowered'
        self.assertEqual('holds', population.score(plan, rows)['p1'])
        rows[5]['drag']['p1'] = 'unresolved'
        self.assertEqual('unresolved', population.score(plan, rows)['p1'])
        rows[5]['drag']['p1'] = 'overpowered'
        self.assertEqual('fails', population.score(plan, rows)['p1'])

    def test_interpolated_quantile_and_censoring(self):
        plan, rows = dataset()
        rows[0]['drag'].update(ceiling_lower_mps=10, ceiling_upper_mps=10.001)
        rows[1]['drag'].update(ceiling_lower_mps=60, ceiling_upper_mps=60.001)
        result = population.score(plan, rows)
        self.assertAlmostEqual(59.5, result['p1_speed_lower_mps'])
        self.assertEqual('fails', result['p2'])
        for row in rows[1:]:
            row['drag'].update(ceiling_lower_mps=128, ceiling_upper_mps=None, right_censored=True)
        result = population.score(plan, rows)
        self.assertIsNone(result['p1_speed_upper_mps'])
        self.assertEqual('holds', result['p2'])
        self.assertEqual(99, result['right_censored'])

    def test_insufficient_resolution_and_boundary_bracket(self):
        for count in [0, 1, 19, 20, 99]:
            plan, rows = dataset(count)
            result = population.score(plan, rows)
            self.assertEqual('unresolved', result['p2'])
            self.assertEqual('holds' if count >= 20 else 'unresolved', result['p1'])
        plan, rows = dataset()
        for row in rows:
            row['drag'].update(ceiling_lower_mps=59.999, ceiling_upper_mps=60.001)
        self.assertEqual('unresolved', population.score(plan, rows)['p2'])

    def test_invalid_records_cannot_be_scored(self):
        plan, rows = dataset(1)
        with self.assertRaises(ValueError):
            population.score(plan, rows * 2)
        for change in [dict(status='forgotten'), dict(identity_sha256='other'), dict(entry=dict(path='other'))]:
            bad = copy.deepcopy(rows)
            bad[0].update(change)
            with self.assertRaises(ValueError):
                population.score(plan, bad)
        for change in [dict(ceiling_lower_mps=float('nan')), dict(ceiling_upper_mps=69),
                       dict(ceiling_upper_mps=None), dict(p1='pass')]:
            bad = copy.deepcopy(rows)
            bad[0]['drag'].update(change)
            with self.assertRaises(ValueError):
                population.score(plan, bad)

    def test_atomic_receipts_resume_and_provenance(self):
        plan, rows = dataset(2)
        rows = [population.seal(r) for r in rows]
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            population.start(root, plan)
            path = root / 'receipts' / population.receipt_name(plan['files'][0])
            population.atomic(path, rows[0])
            self.assertEqual(rows[:1], population.read_receipts(root, plan))
            self.assertEqual(plan, population.start(root, copy.deepcopy(plan)))
            with self.assertRaises(ValueError):
                population.start(root, dict(plan, identity_sha256='changed'))
            with patch.object(population.os, 'replace', side_effect=OSError('interrupted')):
                with self.assertRaises(OSError):
                    population.atomic(path, rows[1])
            self.assertEqual(rows[0], json.loads(path.read_text()))
            self.assertEqual([path], list(path.parent.iterdir()))
            (path.parent / 'interrupted.tmp').write_text('unfinished')
            self.assertEqual(rows[:1], population.read_receipts(root, plan))
            population.atomic(path.parent / 'unexpected.json', rows[0])
            with self.assertRaises(ValueError):
                population.read_receipts(root, plan)

    def test_receipt_corruption_is_not_silently_scored(self):
        plan, rows = dataset(1)
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / 'receipts' / population.receipt_name(plan['files'][0])
            record = population.seal(rows[0])
            record['drag']['ceiling_lower_mps'] = 1
            population.atomic(path, record)
            with self.assertRaisesRegex(ValueError, 'checksum'):
                population.read_receipts(folder, plan)
            record = population.seal(dict(rows[0], identity_sha256='wrong'))
            population.atomic(path, record)
            with self.assertRaisesRegex(ValueError, 'provenance'):
                population.read_receipts(folder, plan)

    def test_measurement_transitions_and_failed_stage_receipt(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            blueprint = root / 'bp.sbc'
            blueprint.write_text('mock blueprint')
            entry = dict(path='bp.sbc', bytes=blueprint.stat().st_size, sha256=population.digest(blueprint))
            plan = dict(binary='mock.dll', corpus=folder, identity_sha256='f' * 64)
            for status in ['excluded_small', 'excluded_modded']:
                with patch.object(population, 'invoke', return_value=dict(status=status)):
                    self.assertEqual(status, population.measure(plan, entry, root)['status'])
            data = test_aerothrust.InputContractTests.inputs()
            data.update(blueprint_sha256=entry['sha256'], core_mvid='core')
            candidate = dict(status='candidate', input=data)
            for mass, expected in [(100, 'not_self_lifting'), (100 / 9.81, 'unresolved')]:
                data['mass_kg'] = mass
                with patch.object(population, 'invoke', return_value=candidate):
                    self.assertEqual(expected, population.measure(plan, entry, root)['status'])
            data['mass_kg'] = 1
            result = dict(blueprint_sha256=entry['sha256'], harness_mvid=data['harness_mvid'], core_mvid='core')
            with patch.object(population, 'invoke', side_effect=[candidate, result]) as invoke:
                self.assertEqual('measured', population.measure(plan, entry, root)['status'])
                self.assertEqual(2, invoke.call_count)
            result['core_mvid'] = 'changed'
            with patch.object(population, 'invoke', side_effect=[candidate, result]):
                receipt = population.measure(plan, entry, root)
                self.assertEqual('unresolved', receipt['status'])
                self.assertIn('provenance mismatch', receipt['error'])
            with patch.object(population, 'invoke', side_effect=RuntimeError('harness fault')):
                self.assertIn('harness fault', population.measure(plan, entry, root)['error'])
            blueprint.write_text('changed')
            with self.assertRaisesRegex(ValueError, 'changed'):
                population.measure(plan, entry, root)

    def test_manifest_fingerprints_the_runtime_authored_data_root(self):
        with tempfile.TemporaryDirectory() as folder:
            root = pathlib.Path(folder)
            corpus, content, authored, runtime = [root / name for name in ['corpus', 'game', 'other-checkout-data', 'runtime']]
            for path in [corpus, content / 'CubeBlocks', authored / 'CubeBlocks', runtime]:
                path.mkdir(parents=True)
            (corpus / 'bp.sbc').write_text('blueprint')
            (content / 'Components.sbc').write_text('components')
            (content / 'CubeBlocks' / 'game.sbc').write_text('game definitions')
            data = authored / 'Cubes.xml'
            data.write_text('runtime authored properties')
            binary = runtime / 'test.dll'
            binary.write_text('runtime')
            environment = dict(game_content=str(content), authored_data=str(authored))
            with patch.object(population, 'invoke', return_value=environment):
                plan = population.manifest(corpus, binary)
            self.assertIn(str(data), plan['fingerprint'])
            self.assertEqual(population.digest(data), plan['fingerprint'][str(data)])
            local = pathlib.Path(population.__file__).resolve().parents[2] / 'Thermodynamics/Content/Data/Cubes.xml'
            self.assertNotIn(str(local), plan['fingerprint'])
            data.write_text('changed runtime authored properties')
            with self.assertRaisesRegex(ValueError, 'changed'):
                population.verify_fingerprint(plan, {})
            for invalid in [dict(game_content=str(content)), dict(environment, authored_data=str(root / 'missing'))]:
                with patch.object(population, 'invoke', return_value=invalid):
                    with self.assertRaisesRegex(ValueError, 'authored'):
                        population.manifest(corpus, binary)

    def test_runtime_changes_are_detected_even_with_equal_size(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / 'runtime'
            path.write_text('abc')
            plan = dict(fingerprint={str(path): population.digest(path)})
            stamps = {}
            population.verify_fingerprint(plan, stamps)
            population.verify_fingerprint(plan, stamps)
            path.write_text('def')
            with self.assertRaisesRegex(ValueError, 'changed'):
                population.verify_fingerprint(plan, stamps)


if __name__ == '__main__':
    unittest.main()
