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

    def test_near_rank_sets_retain_every_candidate_without_projection(self):
        points = np.array([[0, 0, 0], [1, 0, 0], [0, 1e-9, 0], [1, 1e-9, 0]])
        kept, masks = aeroenvelope.reduce_vertices(points, list(range(4)))
        self.assertEqual(4, len(kept))
        for point, mask in zip(kept, masks):
            np.testing.assert_array_equal(point, points[mask])

    def test_resource_limit_still_fails_loudly(self):
        with patch.object(aeroenvelope, 'VERTEX_LIMIT', 2), self.assertRaisesRegex(RuntimeError, 'budget'):
            aeroenvelope.maximum(fixture([[1, 0, 0], [0, 1, 0]], [100, 100]))

    def test_known_resultant_survives_a_near_rank_fixed_ray_lp(self):
        axes = np.array([[1, 0, 0], [0, 1, 0], [0, 1, 1e-8]], dtype=float)
        axes /= np.linalg.norm(axes, axis=1)[:, None]
        result = aeroenvelope.maximum(fixture(axes, [100] * 3))
        self.assertAlmostEqual(np.linalg.norm(100 * axes.sum(axis=0)), result['capacity_n'])
        self.assertEqual('unresolved-near-rank', result['directional_lp']['status'])
        self.assertLessEqual(abs(result['duality_gap_n']), result['tolerance_n'])
        np.testing.assert_allclose(result['vertex_force_n'], 100 * axes.sum(axis=0), atol=1e-12)

    def test_nearly_parallel_and_coplanar_sets_match_exhaustive_vertices(self):
        rng = np.random.default_rng(20260929)
        maximum_error = 0
        for plane in [False, True]:
            for _ in range(20):
                axes = rng.normal(size=(8, 3))
                axes[:, 2] *= 1e-8
                if not plane:
                    axes[:, 1] *= 1e-8
                axes /= np.linalg.norm(axes, axis=1)[:, None]
                ratings = rng.uniform(1, 1000, 8)
                expected = max(np.linalg.norm(np.array(bits) @ (axes * ratings[:, None]))
                               for bits in itertools.product([0, 1], repeat=8))
                result = aeroenvelope.maximum(fixture(axes, ratings))
                error = abs(expected - result['capacity_n'])
                maximum_error = max(maximum_error, error)
                self.assertLessEqual(error, result['tolerance_n'])
                self.assertGreater(expected, 0)
        print('Near-rank global oracle: 40/40 nonzero cases; max discrepancy %.9g N' % maximum_error)

    def test_support_bound_and_lp_disagreement_cannot_be_ignored(self):
        with self.assertRaisesRegex(ValueError, 'binary'):
            aeroenvelope.resultant_certificate(np.array([[1, 0, 0]]), np.array([100]), np.array([0.5]), np.array([0, 0, 1]))
        with self.assertRaisesRegex(RuntimeError, 'support-plane'):
            aeroenvelope.resultant_certificate(np.array([[1, 0, 0], [1, 0, 0]]), np.array([100, 100]), np.array([1, 0]), np.array([0, 0, 1]))
        data = fixture([[1, 0, 0]], [100])
        with patch.object(aeroenvelope.aerothrust, 'capacity', return_value=dict(capacity_n=120)):
            with self.assertRaisesRegex(RuntimeError, 'disagrees'):
                aeroenvelope.maximum(data)
        with patch.object(aeroenvelope.aerothrust, 'capacity', side_effect=RuntimeError('solver fault')):
            with self.assertRaisesRegex(RuntimeError, 'solver fault'):
                aeroenvelope.maximum(data)


if __name__ == '__main__':
    unittest.main()
