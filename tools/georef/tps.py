"""Thin-plate spline from world (x, z) metres to Map Genie's map pixels, for find_anchors.py.

The model is  f(p) = a0 + a1*s*x + a2*s*z + sum_i w_i * U(|s*p - s*c_i|),  U(r) = r^2 ln r,
with s = SCALE (metres -> km, for conditioning). find_anchors.py fits one to the anchors it's
sure of, to predict where the rest of Map Genie's markers are in the world and pick the right
game object for each. The mod itself no longer uses a spline: it puts things on the game's own
painted map exactly as the game does (src/Map/MapProjection.cs, build_pois.py).
"""
import numpy as np

SCALE = 0.001  # metres -> kilometres


def _u(r):
    with np.errstate(divide="ignore", invalid="ignore"):
        u = r * r * np.log(r)
    return np.nan_to_num(u)


def fit(world_xz, map_px, smoothing):
    """Fit a smoothing TPS. world_xz: (n, 2) metres; map_px: (n, 2) pixels."""
    c = np.asarray(world_xz, float) * SCALE
    n = len(c)
    k = _u(np.linalg.norm(c[:, None] - c[None], axis=2)) + smoothing * np.eye(n)
    p = np.c_[np.ones(n), c]
    lhs = np.zeros((n + 3, n + 3))
    lhs[:n, :n], lhs[:n, n:], lhs[n:, :n] = k, p, p.T
    rhs = np.zeros((n + 3, 2))
    rhs[:n] = map_px
    sol = np.linalg.solve(lhs, rhs)
    return {"centers": np.asarray(world_xz, float), "weights": sol[:n], "affine": sol[n:]}


def evaluate(model, world_xz):
    q = np.atleast_2d(np.asarray(world_xz, float)) * SCALE
    c = model["centers"] * SCALE
    return _u(np.linalg.norm(q[:, None] - c[None], axis=2)) @ model["weights"] + np.c_[np.ones(len(q)), q] @ model["affine"]


def invert(model, map_px, guess):
    """World (x, z) whose projection is map_px, by Newton iteration from guess."""
    w = np.array(guess, float)
    target = np.asarray(map_px, float)
    for _ in range(50):
        f = evaluate(model, w)[0] - target
        if np.hypot(*f) < 1e-4:
            break
        h = 0.01
        j = np.column_stack([(evaluate(model, w + d)[0] - evaluate(model, w - d)[0]) / (2 * h) for d in ([h, 0], [0, h])])
        w = w - np.linalg.solve(j, f)
    return w


def leave_one_out(world_xz, map_px, smoothing):
    """Pixel error at each anchor when the model is fitted without it."""
    world_xz, map_px = np.asarray(world_xz, float), np.asarray(map_px, float)
    errs = np.zeros(len(world_xz))
    for i in range(len(world_xz)):
        keep = np.arange(len(world_xz)) != i
        m = fit(world_xz[keep], map_px[keep], smoothing)
        errs[i] = np.linalg.norm(evaluate(m, world_xz[i])[0] - map_px[i])
    return errs
