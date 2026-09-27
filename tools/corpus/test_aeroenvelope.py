"""Global thrust controls against exhaustive binary-throttle enumeration."""
import itertools
import unittest
from unittest.mock import patch
import numpy as np
import aeroenvelope
import test_aerothrust


def fixture(axes, ratings):
    data = test_aerothrust.InputContractTests.inputs()
    data['thrusters'] = [dict(direction=list(a), effective_n=float(r)) for a, r in zip(axes, ratings)]
    return data


class EnvelopeTests(unittest.TestCase):
    def test_perpendicular_combination_exceeds_every_reference_axis(self):
        result = aeroenvelope.maximum(fixture([[1, 0, 0], [0, 1, 0]], [100, 100]))
        self.assertAlmostEqual(np.sqrt(2) * 100, result['capacity_n'])
        self.assertEqual([1, 1], result['vertex_throttles'])

    def test_seeded_exhaustive_independent_oracle(self):
        rng = np.random.default_rng(20260928)
        maximum_error = 0
        for count in range(1, 9):
            for _ in range(10):
                axes = rng.normal(size=(count, 3))
                axes /= np.linalg.norm(axes, axis=1)[:, None]
                ratings = rng.uniform(1, 1000, count)
                expected = max(np.linalg.norm(np.array(bits) @ (axes * ratings[:, None]))
                               for bits in itertools.product([0, 1], repeat=count))
                result = aeroenvelope.maximum(fixture(axes, ratings))
                error = abs(expected - result['capacity_n'])
                maximum_error = max(maximum_error, error)
                self.assertLessEqual(error, result['tolerance_n'])
                self.assertGreater(result['capacity_n'], 0)
        print('Global thrust oracle: 80/80 nonzero cases; max discrepancy %.9g N' % maximum_error)

    def test_ties_rotate_with_main_frame_and_ignore_thruster_order(self):
        axes = np.concatenate((np.eye(3), -np.eye(3)))
        data = fixture(axes, [100] * 6)
        original = aeroenvelope.maximum(data)
        np.testing.assert_allclose(original['direction'], [-1 / np.sqrt(3)] * 3)
        angle = 0.4321
        rotation = np.array([[np.cos(angle), -np.sin(angle), 0], [np.sin(angle), np.cos(angle), 0], [0, 0, 1]])
        for h in data['headings']:
            h['direction'] = (np.array(h['direction']) @ rotation).tolist()
        for t in data['thrusters']:
            t['direction'] = (np.array(t['direction']) @ rotation).tolist()
        data['thrusters'].reverse()
        result = aeroenvelope.maximum(data)
        self.assertAlmostEqual(original['capacity_n'], result['capacity_n'])
        np.testing.assert_allclose(result['direction'], np.array(original['direction']) @ rotation, atol=1e-12)

    def test_rank_zero_one_two_and_duplicate_axes(self):
        for axes, ratings, expected in [([], [], 0), ([[1, 0, 0]], [0], 0),
                                       ([[1, 0, 0], [1, 0, 0], [-1, 0, 0]], [30, 40, 60], 70),
                                       ([[1, 0, 0], [0, 1, 0], [-1, 0, 0]], [3, 4, 2], 5)]:
            with self.subTest(axes=axes):
                self.assertAlmostEqual(expected, aeroenvelope.maximum(fixture(axes, ratings))['capacity_n'])

    def test_uncertain_rank_and_resource_limit_remain_unresolved(self):
        with self.assertRaisesRegex(RuntimeError, 'near-rank'):
            aeroenvelope.maximum(fixture([[1, 0, 0], [1, 1e-8, 0]], [100, 100]))
        with patch.object(aeroenvelope, 'VERTEX_LIMIT', 2), self.assertRaisesRegex(RuntimeError, 'budget'):
            aeroenvelope.maximum(fixture([[1, 0, 0], [0, 1, 0]], [100, 100]))


if __name__ == '__main__':
    unittest.main()
