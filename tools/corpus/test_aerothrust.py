"""Directional LP controls: hand solutions, independent vertex enumeration and certificate faults."""
import copy
import itertools
import json
import pathlib
import math
import os
import sys
import types
import unittest
from unittest.mock import patch

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import aerothrust


def vertices(axes, ratings, target):
    """Small-fixture oracle: enumerate box vertices with at most rank-1 free throttles."""
    vectors = np.asarray(axes, dtype=float).T * np.asarray(ratings)
    target = np.asarray(target, dtype=float)
    target /= np.linalg.norm(target)
    rank = np.linalg.matrix_rank(vectors, tol=1e-10)
    if rank == 0 or np.linalg.norm(vectors @ np.linalg.lstsq(vectors, target, rcond=None)[0] - target) > 1e-9:
        return 0.0
    best = 0.0
    count = len(ratings)
    for free in itertools.combinations(range(count), rank - 1):
        fixed = [i for i in range(count) if i not in free]
        basis = np.column_stack([vectors[:, i] for i in free] + [-target])
        if np.linalg.matrix_rank(basis, tol=1e-10) < rank:
            continue
        for bits in itertools.product((0, 1), repeat=len(fixed)):
            rhs = -(vectors[:, fixed] @ np.asarray(bits))
            state = np.linalg.lstsq(basis, rhs, rcond=None)[0]
            if np.linalg.norm(basis @ state - rhs) > 1e-8:
                continue
            if (state[:-1] >= -1e-9).all() and (state[:-1] <= 1 + 1e-9).all():
                best = max(best, float(state[-1]))
    return best


class DirectionalCapacityTests(unittest.TestCase):
    def test_aligned_rank_one_and_no_thrust(self):
        for axes, ratings, expected in [([], [], 0), ([[1, 0, 0]], [0], 0),
                                       ([[1, 0, 0], [-1, 0, 0]], [100, 30], 100),
                                       ([[-1, 0, 0]], [100], 0)]:
            with self.subTest(axes=axes):
                result = aerothrust.capacity(axes, ratings, [1, 0, 0])
                self.assertAlmostEqual(expected, result["capacity_n"])
                self.assertLessEqual(result["force_residual_n"], result["tolerance_n"])

    def test_oblique_projection_is_not_available_thrust(self):
        c = math.sqrt(0.5)
        self.assertAlmostEqual(0, aerothrust.capacity([[c, c, 0]], [100], [1, 0, 0])["capacity_n"])
        result = aerothrust.capacity([[c, c, 0], [c, -c, 0]], [100, 50], [1, 0, 0])
        self.assertAlmostEqual(100 * c, result["capacity_n"])
        np.testing.assert_allclose(result["throttles"], [0.5, 1], atol=1e-9)
        self.assertLess(result["capacity_n"], 150 * c)
        self.assertAlmostEqual(100 * c, vertices([[c, c, 0], [c, -c, 0]], [100, 50], [1, 0, 0]))

    def test_rank_two_cannot_supply_out_of_plane_force(self):
        result = aerothrust.capacity([[1, 0, 0], [0, 1, 0]], [100, 100], [1, 0, 1])
        self.assertAlmostEqual(0, result["capacity_n"])

    def test_rotation_permutation_splitting_and_scaling(self):
        axes = np.array([[1, 0, 0], [-1, 0, 0], [0, 1, 0], [0, -1, 0], [0, 0, 1], [0, 0, -1]])
        ratings = np.array([100, 50, 30, 40, 60, 70])
        target = np.array([1, 2, 3])
        expected = aerothrust.capacity(axes, ratings, target)["capacity_n"]
        self.assertGreater(expected, 0)
        rotation = np.array([[0, -1, 0], [0, 0, 1], [-1, 0, 0]])
        self.assertAlmostEqual(expected, aerothrust.capacity(axes @ rotation, ratings, target @ rotation)["capacity_n"])
        self.assertAlmostEqual(expected, aerothrust.capacity(axes[::-1], ratings[::-1], target)["capacity_n"])
        self.assertAlmostEqual(expected, aerothrust.capacity(np.repeat(axes, 2, axis=0), np.repeat(ratings / 2, 2), target)["capacity_n"])
        for scale in [1e-3, 1e9]:
            self.assertAlmostEqual(expected, aerothrust.capacity(axes, ratings * scale, target)["capacity_n"] / scale, places=7)

    def test_seeded_nonzero_fixtures_match_independent_vertices(self):
        random = np.random.default_rng(20260927)
        nonzero = 0
        max_error = 0
        for _ in range(80):
            basis = random.normal(size=(3, 3))
            basis /= np.linalg.norm(basis, axis=1)[:, None]
            axes = np.concatenate([basis, -basis])
            ratings = random.uniform(1, 100, size=6)
            target = random.normal(size=3)
            expected = vertices(axes, ratings, target)
            actual = aerothrust.capacity(axes, ratings, target)
            error = abs(expected - actual["capacity_n"])
            max_error = max(max_error, error)
            self.assertLessEqual(error, actual["tolerance_n"])
            nonzero += expected > 1e-3
        self.assertEqual(80, nonzero)
        print("Directional LP oracle: 80/80 nonzero cases; max absolute discrepancy %.9g N" % max_error)

    def test_rotated_force_boxes_match_analytic_capacity_and_original_frame_certificate(self):
        rng = np.random.default_rng(20260930)
        maximum_relative_error = 0
        for scale in [1e-3, 1, 1e3, 1e9]:
            for _ in range(10):
                rotation = np.linalg.qr(rng.normal(size=(3, 3)))[0]
                positive = rng.uniform(1, 100, 3) * scale
                negative = rng.uniform(1, 100, 3) * scale
                local = rng.uniform(-1, 1, 3)
                local /= np.linalg.norm(local)
                bounds = np.where(local >= 0, positive, negative)
                expected = np.min(bounds / np.abs(local))
                axes = np.concatenate((np.eye(3), -np.eye(3))) @ rotation
                ratings = np.concatenate((positive, negative))
                target = local @ rotation
                result = aerothrust.capacity(axes, ratings, target)
                error = abs(expected - result['capacity_n'])
                maximum_relative_error = max(maximum_relative_error, error / expected)
                self.assertLessEqual(error, result['tolerance_n'])
                force = (axes * ratings[:, None]).T @ np.array(result['throttles'])
                self.assertLessEqual(np.linalg.norm(force - result['capacity_n'] * target), result['tolerance_n'])
                normal = np.array(result['dual_normal'])
                self.assertAlmostEqual(1, normal @ target)
                upper = sum(max(0, float(r * (a @ normal))) for a, r in zip(axes, ratings))
                self.assertLessEqual(abs(upper - expected), result['tolerance_n'])
        print('Rotated analytic force boxes: 40/40; max relative error %.9g' % maximum_relative_error)

    def test_diagnosed_rotated_box_has_a_valid_original_frame_certificate(self):
        path = pathlib.Path(__file__).resolve().parents[2] / 'tests/benchmarks/aerodynamics/population-lp-frame-2026-09-27.json'
        record = json.loads(path.read_text())
        data = record['input']
        axes = [t['direction'] for t in data['thrusters']]
        ratings = [t['effective_n'] for t in data['thrusters']]
        witness = np.array(record['result']['vertex_throttles'])
        vectors = np.array(axes) * np.array(ratings)[:, None]
        vector = witness @ vectors
        expected = np.linalg.norm(vector)
        direction = vector / expected
        support = sum(max(0, f @ direction) for f in vectors)
        self.assertLess(abs(support - expected), 1e-5)
        result = aerothrust.capacity(axes, ratings, direction)
        self.assertLessEqual(abs(expected - result['capacity_n']), result['tolerance_n'])
        self.assertLessEqual(abs(result['duality_gap_n']), result['tolerance_n'])

    def test_invalid_inputs_fail_loudly(self):
        cases = [([[2, 0, 0]], [1], [1, 0, 0]), ([[1, 0, 0]], [-1], [1, 0, 0]),
                 ([[1, 0, 0]], [float('nan')], [1, 0, 0]), ([[1, 0, 0]], [1], [0, 0, 0]),
                 ([[1, 0, 0]], [1], [float('inf'), 0, 0]), ([[1, 0, 0]], [1, 2], [1, 0, 0])]
        for args in cases:
            with self.subTest(args=args), self.assertRaises(ValueError):
                aerothrust.capacity(*args)

    def test_nearly_aligned_unbalanced_force_is_unresolved_not_certified(self):
        for axes, ratings in [([[1, 1e-10, 0]], [100]),
                              ([[1, 1e-10, 0], [0, 1, 0], [0, 0, 1]], [100, 100, 100])]:
            with self.subTest(axes=axes), self.assertRaisesRegex(RuntimeError, "Near-rank-deficient"):
                aerothrust.capacity(axes, ratings, [1, 0, 0])

    def test_fake_success_cannot_bypass_primal_or_dual_checks(self):
        for state, normal in [([2, 2], [1, 0, 0]), ([1, 0.5], [1, 0, 0]),
                              ([0.5, 0.5], [1, 0, 0]), ([1, 1], [0, 1, 0]),
                              ([float('nan'), 1], [1, 0, 0]), ([1, 1], [float('inf'), 0, 0])]:
            result = types.SimpleNamespace(success=True, x=state, eqlin=types.SimpleNamespace(marginals=normal))
            with self.subTest(state=state, normal=normal), patch.object(aerothrust, "linprog", return_value=result):
                with self.assertRaises(RuntimeError):
                    aerothrust.capacity([[1, 0, 0]], [100], [1, 0, 0])


class InputContractTests(unittest.TestCase):
    @staticmethod
    def inputs():
        return {"schema": 1, "kind": "aero-population-inputs", "relative_air_density": 1, "atmosphere": True,
                "mass_kg": 100, "grids": 1, "blueprint": "fixture", "blueprint_sha256": "a" * 64,
                "harness_mvid": "00000000-0000-0000-0000-000000000001",
                "thrusters": [{"direction": [1, 0, 0], "effective_n": 100}],
                "headings": [{"name": name, "direction": vector} for name, vector in [
                    ("right", [1, 0, 0]), ("left", [-1, 0, 0]), ("up", [0, 1, 0]),
                    ("down", [0, -1, 0]), ("forward", [0, 0, -1]), ("backward", [0, 0, 1])]]}

    def test_machine_contract_has_exactly_one_powered_heading(self):
        result = aerothrust.audit(self.inputs())
        self.assertEqual(6, len(result["headings"]))
        for row in result["headings"]:
            self.assertAlmostEqual(100 if row["heading"] == "right" else 0, row["capacity_n"])
        self.assertIn("no P1/P2 verdict", result["scope"])

    def test_malformed_provenance_environment_or_headings_are_rejected(self):
        base = self.inputs()
        changes = [("schema", 2), ("mass_kg", float("nan")), ("grids", 0),
                   ("blueprint_sha256", "unknown"), ("harness_mvid", "unknown"),
                   ("relative_air_density", 0), ("atmosphere", False),
                   ("headings", base["headings"][:-1])]
        for key, value in changes:
            data = copy.deepcopy(base)
            data[key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                aerothrust.audit(data)
        data = copy.deepcopy(base)
        data["headings"][1]["direction"] = [1, 0, 0]
        with self.assertRaises(ValueError):
            aerothrust.audit(data)


if __name__ == "__main__":
    unittest.main()
