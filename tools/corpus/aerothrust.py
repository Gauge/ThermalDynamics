"""Directional rated-thrust LP with independently recomputed primal/dual checks.

Input: schema-1 JSON from `aero-inputs <blueprint> --json`. This is not a P1/P2 scorer.
Requires NumPy and SciPy (HiGHS); see README.md and aerodynamics.md.
"""
import argparse
import hashlib
import json
import pathlib
import uuid
import sys

import numpy as np
import scipy
from scipy.optimize import linprog


RELATIVE_TOLERANCE = 1e-7


def capacity(directions, ratings, direction):
    """Maximize pure directional force with independent throttles, retaining a checked certificate."""
    axes = np.asarray(directions, dtype=float)
    if axes.size == 0:
        axes = axes.reshape((0, 3))
    if axes.ndim != 2 or axes.shape[1] != 3:
        raise ValueError("Expected an N-by-3 array of thruster directions")
    limits = np.asarray(ratings, dtype=float)
    target = np.asarray(direction, dtype=float)
    if limits.shape != (len(axes),) or target.shape != (3,):
        raise ValueError("Expected N unit directions, N ratings and one three-vector")
    if not (np.isfinite(axes).all() and np.isfinite(limits).all() and np.isfinite(target).all()):
        raise ValueError("Thrust inputs must be finite")
    length = np.linalg.norm(target)
    if not np.isfinite(length) or length <= 0 or (limits < 0).any():
        raise ValueError("Use a nonzero direction and nonnegative thrust ratings")
    target = target / length
    lengths = np.linalg.norm(axes, axis=1)
    if (np.abs(lengths - 1) > 1e-5).any():
        raise ValueError("Thruster directions must be unit vectors within blueprint float roundoff")
    axes = axes / lengths[:, None]
    scale = max(1.0, float(np.max(limits, initial=0)))
    tolerance = RELATIVE_TOLERANCE * scale
    forces = axes * (limits / scale)[:, None]
    # All-zero thrust still goes through the same LP and certificate checks.
    equality = np.column_stack((forces.T, -target))
    objective = np.zeros(len(axes) + 1)
    objective[-1] = -1
    result = linprog(objective, A_eq=equality, b_eq=np.zeros(3),
                     bounds=[(0, 1)] * len(axes) + [(0, None)], method="highs",
                     options={"primal_feasibility_tolerance": 1e-9,
                              "dual_feasibility_tolerance": 1e-9})
    if not result.success:
        raise RuntimeError("Directional thrust optimization failed: " + result.message)
    state = np.asarray(result.x, dtype=float)
    if state.shape != (len(axes) + 1,) or not np.isfinite(state).all():
        raise RuntimeError("Invalid primal solution")
    if (state[:-1] < -1e-9).any() or (state[:-1] > 1 + 1e-9).any() or state[-1] < -1e-9:
        raise RuntimeError("Primal solution violates throttle/capacity bounds")
    throttle = np.clip(state[:-1], 0, 1)
    value = max(0, float(state[-1]))
    residual = float(np.linalg.norm(forces.T @ throttle - value * target)) * scale
    condition = 1.0
    if value > 0:
        active = throttle > 0
        contributions = np.column_stack((forces[active].T * throttle[active], -value * target))
        singular = np.linalg.svd(contributions, compute_uv=False)
        floor = np.finfo(float).eps * max(contributions.shape) * singular[0]
        resolved = singular[singular > floor]
        if len(resolved):
            condition = float(resolved[0] / resolved[-1])
        if condition > 1 / RELATIVE_TOLERANCE:
            raise RuntimeError("Near-rank-deficient thrust certificate is unresolved at the declared tolerance")
    normal = np.asarray(result.eqlin.marginals, dtype=float)
    if normal.shape != (3,) or not np.isfinite(normal).all():
        raise RuntimeError("Invalid dual normal")
    denominator = float(normal @ target)
    if denominator <= 0 or not np.isfinite(denominator):
        raise RuntimeError("Dual normal does not bound the requested ray")
    normal = normal / denominator
    upper = float(np.maximum(forces @ normal, 0).sum()) * scale
    newtons = value * scale
    gap = upper - newtons
    if not np.isfinite([residual, upper, newtons, gap]).all() or residual > tolerance or abs(gap) > tolerance:
        raise RuntimeError("Primal residual or independent dual gap exceeds tolerance")
    return {"capacity_n": newtons, "throttles": throttle.tolist(),
            "force_residual_n": residual, "active_condition": condition, "dual_normal": normal.tolist(),
            "dual_upper_n": upper, "duality_gap_n": gap, "tolerance_n": tolerance}


def audit(data):
    """Evaluate each saved main-grid heading; preserve the input's provenance and assumptions."""
    if data.get("schema") != 1 or data.get("kind") != "aero-population-inputs":
        raise ValueError("Expected schema-1 aerodynamic population inputs")
    if data.get("relative_air_density") != 1 or data.get("atmosphere") is not True:
        raise ValueError("This audit expects the registered sea-level input convention")
    if not isinstance(data.get("grids"), int) or data["grids"] < 1:
        raise ValueError("Expected a positive grid count")
    mass = float(data["mass_kg"])
    if not np.isfinite(mass) or mass <= 0:
        raise ValueError("Expected finite positive model mass")
    digest = data["blueprint_sha256"]
    if len(digest) != 64 or any(c not in "0123456789abcdef" for c in digest):
        raise ValueError("Missing or malformed blueprint digest")
    uuid.UUID(data["harness_mvid"])
    thrusters = data["thrusters"]
    directions = [t["direction"] for t in thrusters]
    ratings = [t["effective_n"] for t in thrusters]
    headings = data["headings"]
    names = {h["name"] for h in headings}
    if len(headings) != 6 or names != {"forward", "backward", "left", "right", "up", "down"}:
        raise ValueError("Expected the six main-grid headings")
    by_name = {h["name"]: np.asarray(h["direction"], dtype=float) for h in headings}
    basis = np.asarray([by_name[n] for n in ["right", "up", "backward"]])
    if basis.shape != (3, 3) or not np.isfinite(basis).all() or not np.allclose(basis @ basis.T, np.eye(3), atol=1e-5, rtol=0):
        raise ValueError("Reference headings are not an orthonormal frame")
    for positive, negative in [("right", "left"), ("up", "down"), ("backward", "forward")]:
        other = by_name[negative]
        if other.shape != (3,) or not np.isfinite(other).all() or not np.allclose(by_name[positive], -other, atol=1e-5, rtol=0):
            raise ValueError("Reference heading pairs are not opposed")
    results = []
    for heading in headings:
        result = capacity(directions, ratings, heading["direction"])
        result["heading"] = heading["name"]
        result["direction"] = heading["direction"]
        results.append(result)
    return {"schema": 1, "kind": "aero-directional-thrust", "scope": "rated thrust only; no P1/P2 verdict",
            "numpy": np.__version__, "scipy": scipy.__version__,
            "source_sha256": hashlib.sha256(pathlib.Path(__file__).read_bytes()).hexdigest(),
            "input_provenance": {key: data[key] for key in ["blueprint_sha256", "harness_mvid", "blueprint", "grids", "mass_kg"]},
            "headings": results}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("inputs", type=pathlib.Path)
    args = parser.parse_args()
    try:
        raw = args.inputs.read_bytes()
        result = audit(json.loads(raw))
        result["input_sha256"] = hashlib.sha256(raw).hexdigest()
        print(json.dumps(result, indent=2, allow_nan=False))
    except (ValueError, KeyError, TypeError, RuntimeError, OSError) as error:
        print("aerothrust: " + str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
