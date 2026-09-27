"""Analytical uncertainty and coverage controls for the unfitted plate-lift diagnostic."""
import math
import unittest
import plate_lift_check as check


def observation(aspect=1, angle=5, value=0):
    return dict(coefficient='CL', aspect_ratio=aspect, alpha_deg=angle, value=value,
                measurement_lower_95=value - 0.01, measurement_upper_95=value + 0.01,
                extraction_alpha_bound_deg=0, extraction_value_bound=0)


class PlateLiftCheckTests(unittest.TestCase):
    def test_slope_special_case_limits_and_odd_angle(self):
        self.assertAlmostEqual(math.pi, check.slope(8 / 3))
        self.assertAlmostEqual(math.pi / 2, check.slope(1e-6) / 1e-6, places=10)
        self.assertAlmostEqual(2 * math.pi, check.slope(1e12), places=10)
        a = check.compare(observation(angle=5))['predicted_cl']
        self.assertAlmostEqual(-a, check.compare(observation(angle=-5))['predicted_cl'])

    def test_measurement_and_extraction_intervals_remain_distinct(self):
        row = observation(aspect=8 / 3, angle=0, value=0.04)
        row.update(extraction_alpha_bound_deg=1, extraction_value_bound=0.02)
        result = check.compare(row)
        self.assertEqual(0, result['predicted_cl'])
        self.assertAlmostEqual(math.pi * math.pi / 180, result['predicted_upper'])
        self.assertAlmostEqual(0.01, result['observed_lower'])
        self.assertAlmostEqual(0.07, result['observed_upper'])
        self.assertTrue(result['intervals_overlap'])
        row.update(extraction_alpha_bound_deg=0, extraction_value_bound=0)
        self.assertFalse(check.compare(row)['intervals_overlap'])

    def test_invalid_uncertainty_or_nonfinite_values_fail(self):
        for change in [dict(value=float('nan')), dict(aspect_ratio=0),
                       dict(measurement_lower_95=1), dict(extraction_value_bound=-1),
                       dict(extraction_alpha_bound_deg=float('inf'))]:
            with self.assertRaises(ValueError):
                check.compare(dict(observation(), **change))

    def test_all_strata_and_every_selected_row_are_required(self):
        rows = [observation(ar, angle) for ar in (0.5, 1) for angle in (-5, 5)]
        result = check.evaluate(rows + [observation(1, 20), dict(observation(), coefficient='CD')])
        self.assertEqual(5, result['total_lift_observations'])
        self.assertEqual(4, result['selected_observations'])
        self.assertEqual(4, sum(r['nonoverlapping'] for r in result['summaries']))
        for invalid in [[], rows[:-1], rows + rows[:1], rows + [observation(2, 5)]]:
            with self.assertRaises(ValueError):
                check.evaluate(invalid)


if __name__ == '__main__':
    unittest.main()
