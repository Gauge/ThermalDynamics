"""Independent coordinate controls and fail-closed primary-figure extraction checks."""
import csv
import pathlib
import tempfile
import unittest
import plate_reference as reference


class PlateReferenceTests(unittest.TestCase):
    def setUp(self):
        self.plot = dict(figure=3, coefficient='CL', left=100, right=400, top=50, bottom=150,
                         low=-1, high=1, legend=(300, 50, 400, 75))
        self.marker = dict(x0=249.05, x1=250.95, top=99.05, bottom=100.95,
                           width=1.9, height=1.9, stroke=True, fill=False)
        self.bar = dict(x0=250, width=0, height=20, top=90, bottom=110)

    def test_axis_endpoints_midpoint_and_error_bar_have_hand_computed_values(self):
        self.assertEqual((-10, 1), reference.coordinates(self.plot, 100, 50))
        self.assertEqual((50, -1), reference.coordinates(self.plot, 400, 150))
        row = reference.observation(self.plot, self.marker, [self.bar], 1)
        self.assertEqual((20, 0), (row['alpha_deg'], row['value']))
        self.assertAlmostEqual(-0.2, row['measurement_lower_95'])
        self.assertAlmostEqual(0.2, row['measurement_upper_95'])
        self.assertEqual(0.1, row['extraction_alpha_bound_deg'])
        self.assertEqual(0.01, row['extraction_value_bound'])
        self.assertEqual('unspecified', row['sweep_branch'])

    def test_legends_and_non_marker_paths_do_not_become_observations(self):
        self.assertIsNone(reference.marker_centre(dict(self.marker, x0=349.05, x1=350.95, top=59.05, bottom=60.95), self.plot))
        self.assertIsNone(reference.marker_centre(dict(self.marker, width=100), self.plot))
        self.assertIsNone(reference.marker_centre(dict(self.marker, x0=500, x1=501.9), self.plot))
        with self.assertRaisesRegex(ValueError, 'style'):
            reference.marker_centre(dict(self.marker, fill=True), self.plot)

    def test_missing_and_ambiguous_error_bars_fail(self):
        for bars in [[], [self.bar, dict(self.bar)], [dict(self.bar, x0=251)]]:
            with self.assertRaisesRegex(ValueError, 'one primary error bar'):
                reference.observation(self.plot, self.marker, bars, 1)

    def test_uninspected_source_fails_before_loading_a_pdf_parser(self):
        with tempfile.TemporaryDirectory() as folder:
            path = pathlib.Path(folder) / 'changed.pdf'
            path.write_bytes(b'not the inspected source')
            with self.assertRaisesRegex(ValueError, 'hash'):
                reference.extract(path)

    def test_reference_retains_both_quantities_intervals_and_multivalued_region(self):
        path = pathlib.Path(__file__).resolve().parents[2] / 'tests/benchmarks/aerodynamics/plate-reference-mizoguchi-2016.csv'
        with path.open() as handle:
            rows = list(csv.DictReader(handle))
        self.assertEqual(246, len(rows))
        for coefficient in ['CL', 'CD']:
            for aspect, count in [('0.5', 60), ('1', 63)]:
                selected = [r for r in rows if r['coefficient'] == coefficient and r['aspect_ratio'] == aspect]
                self.assertEqual(count, len(selected))
                for row in selected:
                    self.assertLessEqual(float(row['measurement_lower_95']), float(row['value']))
                    self.assertGreaterEqual(float(row['measurement_upper_95']), float(row['value']))
                    self.assertGreater(float(row['extraction_value_bound']), 0)
                    self.assertEqual('unspecified', row['sweep_branch'])
        near_stall = [float(r['value']) for r in rows if r['coefficient'] == 'CL' and r['aspect_ratio'] == '1'
                      and 36.5 < float(r['alpha_deg']) < 37.5]
        self.assertEqual(2, len(near_stall))
        self.assertGreater(max(near_stall) - min(near_stall), 0.4)


if __name__ == '__main__':
    unittest.main()
