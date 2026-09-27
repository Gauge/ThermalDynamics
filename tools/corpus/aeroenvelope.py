"""Maximum resultant rated thrust over all directions, with retained feasible vertex witnesses."""
import numpy as np
from scipy.spatial import ConvexHull
import aerothrust


VERTEX_LIMIT = 200000


def reduce_vertices(points, masks):
    """Keep extreme points in the numerically resolved affine span; reject uncertain reductions."""
    points, unique = np.unique(points, axis=0, return_index=True)
    masks = [masks[i] for i in unique]
    if len(points) <= 1:
        return points, masks
    _, singular, basis = np.linalg.svd(points - points[0], full_matrices=False)
    floor = np.finfo(float).eps * max(points.shape) * singular[0]
    rank = int(np.count_nonzero(singular > floor))
    if rank and singular[rank - 1] / singular[0] < 1e-7:
        raise RuntimeError("Unresolved near-rank-deficient thrust polytope")
    if rank == 0:
        keep = [0]
    else:
        projected = points @ basis[:rank].T
        if rank == 1:
            keep = np.unique([np.argmin(projected[:, 0]), np.argmax(projected[:, 0])])
        else:
            hull = ConvexHull(projected)
            equations = hull.equations
            batch = max(1, 1000000 // len(equations))
            tolerance = 1e-8 * max(1, float(np.max(np.linalg.norm(points, axis=1))))
            for start in range(0, len(points), batch):
                violation = projected[start:start + batch] @ equations[:, :-1].T + equations[:, -1]
                if np.max(violation) > tolerance:
                    raise RuntimeError("Thrust hull reduction failed containment")
            keep = hull.vertices
    return points[keep], [masks[i] for i in keep]


def maximum(data):
    """Find the strongest heading in a frame-invariant main-grid convention; verify its ray by LP."""
    headings = {h['name']: h['direction'] for h in data['headings']}
    frame = np.asarray([headings[n] for n in ['right', 'up', 'backward']], dtype=float)
    axes = np.asarray([t['direction'] for t in data['thrusters']], dtype=float).reshape((-1, 3))
    ratings = np.asarray([t['effective_n'] for t in data['thrusters']], dtype=float)
    if not np.isfinite(axes).all() or not np.isfinite(ratings).all() or (ratings < 0).any():
        raise ValueError('Invalid rated thrust inputs')
    if frame.shape != (3, 3) or not np.allclose(frame @ frame.T, np.eye(3), atol=1e-5, rtol=0):
        raise ValueError('Invalid main-grid frame')
    lengths = np.linalg.norm(axes, axis=1)
    if (abs(lengths - 1) > 1e-5).any():
        raise ValueError('Thrust axes must be unit vectors')
    axes = axes / lengths[:, None]
    scale = max(1, float(np.max(ratings, initial=0)))
    groups = {}
    members = []
    for i, (axis, rating) in enumerate(zip(axes, ratings)):
        if rating == 0:
            continue
        key = tuple(axis)
        if key not in groups:
            groups[key] = [0.0, len(members)]
            members.append([])
        groups[key][0] += rating
        members[groups[key][1]].append(i)
    points = np.zeros((1, 3))
    masks = [0]
    for axis, (rating, index) in groups.items():
        step = np.asarray(axis) * (rating / scale)
        candidates = np.concatenate((points, points + step))
        candidate_masks = masks + [m | (1 << index) for m in masks]
        points, masks = reduce_vertices(candidates, candidate_masks)
        if len(points) > VERTEX_LIMIT:
            raise RuntimeError('Thrust polytope exceeds the declared vertex budget')
    norms = np.linalg.norm(points, axis=1)
    best = float(np.max(norms))
    tied = np.flatnonzero(norms >= best - aerothrust.RELATIVE_TOLERANCE)
    chosen = min(tied, key=lambda i: tuple(np.round((points[i] @ frame.T) / max(norms[i], 1e-300), 9)))
    throttle = np.zeros(len(ratings))
    for group, indices in enumerate(members):
        if masks[chosen] & (1 << group):
            throttle[indices] = 1
    vector = (axes * ratings[:, None]).T @ throttle
    norm = float(np.linalg.norm(vector))
    direction = vector / norm if norm else frame[2]
    certificate = aerothrust.capacity(axes, ratings, direction)
    if abs(certificate['capacity_n'] - norm) > certificate['tolerance_n'] * 2:
        raise RuntimeError('Global thrust vertex disagrees with directional LP')
    return dict(certificate, direction=direction.tolist(), vertex_throttles=throttle.tolist(),
                vertex_force_n=vector.tolist(), vertex_norm_n=norm, vertices=len(points),
                heading='maximum-resultant', tolerance_n=certificate['tolerance_n'] * 2)
