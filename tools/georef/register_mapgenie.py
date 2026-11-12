#!/usr/bin/env python3
"""Measure where Map Genie's map sits on the game's own painted map sheets.

    python tools/georef/register_mapgenie.py --map ../BabyStepsMap/map.png --game "/path/to/Baby Steps"
    python tools/georef/register_mapgenie.py --map map.png --paintings DIR

Map Genie's map is the game's six painted map sheets (the ending's Map0..Map5, see
find_journey.py), stacked and scaled down. Its POI markers (mapgenie_pois.json) are pixels on
it, so build_pois.py needs to know exactly how those pixels sit on the sheets. This finds out by
lining the two images up: the sheets are stacked as journey.json lays them out, warped by the
current guess, and every 192 px tile of Map Genie's map is matched against them (cross-correlation
of image gradients, to a fraction of a pixel); the guess is refitted to the matches until it
stops moving. The start comes from the anchors (anchors.json): game objects whose world position
the game's projection puts on the sheets, against Map Genie's marker for each.

Writes tools/georef/mapgenie.json: Map Genie pixel = (sx * u + ox, oy - sy * v), with (u, v) in
the ending's units, in Map Genie's canonical pixels (its 2048-wide map; the 4096-wide one is 2x).

--map is Map Genie's map image at either size. The paintings are read from the game (--game:
every bundle is scanned for the textures journey.json names, about 2 minutes) or from a folder of
PNGs named after them (--paintings, e.g. Assemble00.png). Needs numpy and Pillow (and UnityPy for
--game). Nothing here is needed to build the mod: the result only changes if Map Genie redraws.
"""
import argparse
import glob
import json
import math
import multiprocessing as mp
import os
import sys

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
CANONICAL_WIDTH = 2048      # Map Genie's map.png; their hi-res one is twice that
SHEET_TEXELS = 2048         # each painting is 2048 x 2048
TILE = 192
ITERATIONS = 12
SIN, COS = 0.5, math.cos(math.radians(30.0))   # the game's projection (see build_pois.py)


def _find_textures(job):
    path, wanted = job
    import UnityPy
    try:
        env = UnityPy.load(path)
    except Exception:
        return []
    found = []
    for o in env.objects:
        if o.type.name != "Texture2D":
            continue
        try:
            t = o.read()
        except Exception:
            continue
        if t.m_Name in wanted:
            found.append((t.m_Name, path))
    return found


def paintings_from_game(game, names, out_dir):
    """Export the named paintings from the game's bundles to out_dir as PNG."""
    import UnityPy
    bundles = sorted(glob.glob(os.path.join(game, "BabySteps_Data", "StreamingAssets", "aa", "StandaloneWindows64", "*.bundle")))
    print(f"scanning {len(bundles)} bundles for {', '.join(sorted(names))}...", file=sys.stderr)
    where = {}
    with mp.Pool() as pool:
        for hits in pool.imap_unordered(_find_textures, [(b, set(names)) for b in bundles], chunksize=32):
            for name, path in hits:
                where.setdefault(name, path)
    missing = set(names) - set(where)
    if missing:
        sys.exit("not found in the game's bundles: " + ", ".join(sorted(missing)))
    for name, path in where.items():
        for o in UnityPy.load(path).objects:
            if o.type.name == "Texture2D":
                t = o.read()
                if t.m_Name == name:
                    t.image.save(os.path.join(out_dir, name + ".png"))
                    break


def stack_sheets(journey, paintings):
    """The sheets as one image, as the ending lays them out, and that image's frame (units)."""
    sheets = journey["sheets"]
    px_per_unit = SHEET_TEXELS / sheets[0]["w"]
    left = min(s["x"] - s["w"] / 2 for s in sheets)
    top = max(s["y"] + s["h"] / 2 for s in sheets)
    bottom = min(s["y"] - s["h"] / 2 for s in sheets)
    width = int(round(max(s["w"] for s in sheets) * px_per_unit))
    height = int(round((top - bottom) * px_per_unit))
    img = Image.new("RGBA", (width, height), (0, 0, 0, 255))
    for s in sheets:
        p = Image.open(os.path.join(paintings, s["painting"] + ".png")).convert("RGBA")
        if p.size != (SHEET_TEXELS, SHEET_TEXELS):
            p = p.resize((SHEET_TEXELS, SHEET_TEXELS), Image.LANCZOS)
        img.alpha_composite(p, (int(round((s["x"] - s["w"] / 2 - left) * px_per_unit)),
                                int(round((top - (s["y"] + s["h"] / 2)) * px_per_unit))))
    return np.asarray(img.convert("L"), np.float32), px_per_unit, left, top


def gradient(a):
    gx = np.zeros_like(a)
    gy = np.zeros_like(a)
    gx[:, 1:-1] = a[:, 2:] - a[:, :-2]
    gy[1:-1, :] = a[2:, :] - a[:-2, :]
    return np.hypot(gx, gy)


def warp(stack, frame, size, t):
    """The stacked sheets as they'd look on Map Genie's map under transform t."""
    px_per_unit, left, top = frame
    # Map Genie (X, Y) -> (u, v) = ((X - ox) / sx, (oy - Y) / sy) -> sheet texel ((u - left) k, (top - v) k)
    k = px_per_unit
    coeffs = (k / t["sx"], 0.0, (-t["ox"] / t["sx"] - left) * k,
              0.0, k / t["sy"], (top - t["oy"] / t["sy"]) * k)
    out = Image.fromarray(stack).transform(size, Image.AFFINE, coeffs, resample=Image.BILINEAR, fillcolor=0)
    return np.asarray(out, np.float32)


def shift(a, b):
    """(dx, dy, peak): b looks like a moved by (dx, dy), to a fraction of a pixel."""
    a = a - a.mean()
    b = b - b.mean()
    window = np.outer(np.hanning(a.shape[0]), np.hanning(a.shape[1]))
    cross = np.fft.fft2(b * window) * np.conj(np.fft.fft2(a * window))
    cross /= np.abs(cross) + 1e-6
    r = np.real(np.fft.ifft2(cross))
    iy, ix = np.unravel_index(np.argmax(r), r.shape)

    def refine(i, n, line):
        lo, mid, hi = line[(i - 1) % n], line[i], line[(i + 1) % n]
        d = lo - 2 * mid + hi
        return i + (0.5 * (lo - hi) / d if d else 0.0)

    dy = refine(iy, r.shape[0], r[:, ix])
    dx = refine(ix, r.shape[1], r[iy, :])
    if dy > r.shape[0] / 2:
        dy -= r.shape[0]
    if dx > r.shape[1] / 2:
        dx -= r.shape[1]
    return dx, dy, r.max()


def first_guess(anchors_path, mg_scale):
    """Scale and offset from the anchors: the game's projection of each against Map Genie's marker."""
    anchors = json.load(open(anchors_path))["anchors"]
    u = np.array([(a["x"] + a.get("loop", 0) * 512.0) / 512.0 for a in anchors])
    v = np.array([(SIN * a["y"] + COS * a["z"]) / 512.0 for a in anchors])
    px = np.array([a["px"] for a in anchors]) * mg_scale
    py = np.array([a["py"] for a in anchors]) * mg_scale
    (sx, ox), *_ = np.linalg.lstsq(np.c_[u, np.ones_like(u)], px, rcond=None)
    (nsy, oy), *_ = np.linalg.lstsq(np.c_[v, np.ones_like(v)], py, rcond=None)
    return {"sx": sx, "ox": ox, "sy": -nsy, "oy": oy}


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--map", required=True, help="Map Genie's map image (2048 or 4096 wide)")
    src = ap.add_mutually_exclusive_group(required=True)
    src.add_argument("--game", help="the game folder, to read the paintings from (needs UnityPy)")
    src.add_argument("--paintings", help="a folder of the paintings as PNGs named after them")
    ap.add_argument("--journey", default=os.path.join(ROOT, "package", "Mods", "CabbySteps", "journey.json"))
    ap.add_argument("--anchors", default=os.path.join(HERE, "anchors.json"))
    ap.add_argument("--out", default=os.path.join(HERE, "mapgenie.json"))
    args = ap.parse_args()
    Image.MAX_IMAGE_PIXELS = None

    journey = json.load(open(args.journey))
    paintings = args.paintings
    if args.game:
        import tempfile
        paintings = tempfile.mkdtemp(prefix="bsm-paintings-")
        paintings_from_game(args.game, [s["painting"] for s in journey["sheets"]], paintings)
    stack, px_per_unit, left, top = stack_sheets(journey, paintings)
    frame = (px_per_unit, left, top)

    mg_img = Image.open(args.map).convert("L")
    to_canonical = CANONICAL_WIDTH / mg_img.width
    # Work at the image's own size; report in canonical pixels.
    mg = np.asarray(mg_img, np.float32)
    mg_grad = gradient(mg)
    t = first_guess(args.anchors, 1.0 / to_canonical)
    print(f"from the anchors: sx {t['sx'] * to_canonical:.3f} ox {t['ox'] * to_canonical:.3f} "
          f"sy {t['sy'] * to_canonical:.3f} oy {t['oy'] * to_canonical:.3f}", file=sys.stderr)
    tile = int(round(TILE / to_canonical))
    cols = np.flatnonzero(mg.max(axis=0) > 0)   # the drawn sheets; the rest of the image is black
    x_lo, x_hi = int(cols[0]), int(cols[-1])
    for it in range(ITERATIONS):
        warped = warp(stack, frame, mg_img.size, t)
        warped_grad = gradient(warped)
        centres, moves = [], []
        for y0 in range(0, mg.shape[0] - tile, tile // 2):
            for x0 in range(x_lo, x_hi - tile, tile // 2):
                a = warped_grad[y0:y0 + tile, x0:x0 + tile]
                b = mg_grad[y0:y0 + tile, x0:x0 + tile]
                if a.std() < 3 or b.std() < 3 or (warped[y0:y0 + tile, x0:x0 + tile] == 0).mean() > 0.2:
                    continue
                dx, dy, peak = shift(a, b)
                if peak < 0.08 or abs(dx) > tile / 4 or abs(dy) > tile / 4:
                    continue
                centres.append((x0 + tile / 2, y0 + tile / 2))
                moves.append((dx, dy))
        centres = np.array(centres)
        moves = np.array(moves)
        # A sheet feature drawn at c under t is at c + move on Map Genie's map: refit t to that.
        u = (centres[:, 0] - t["ox"]) / t["sx"]
        v = (t["oy"] - centres[:, 1]) / t["sy"]
        target = centres + moves
        (sx, ox), *_ = np.linalg.lstsq(np.c_[u, np.ones_like(u)], target[:, 0], rcond=None)
        (nsy, oy), *_ = np.linalg.lstsq(np.c_[v, np.ones_like(v)], target[:, 1], rcond=None)
        residual = np.hypot(target[:, 0] - (sx * u + ox), target[:, 1] - (nsy * v + oy)) * to_canonical
        t = {"sx": sx, "ox": ox, "sy": -nsy, "oy": oy}
        print(f"pass {it + 1}: {len(centres)} tiles, mean move {np.abs(moves).mean() * to_canonical:.3f} px, "
              f"residual median {np.median(residual):.3f} px, worst {residual.max():.3f} px", file=sys.stderr)

    out = {
        "about": "CabbySteps: where Map Genie's map sits on the game's six painted map sheets (journey.json), "
                 "from tools/georef/register_mapgenie.py. Map Genie's map is those sheets stacked and scaled down; "
                 "a pixel of it (canonical: the 2048-wide map.png; the 4096-wide one is 2x) is "
                 "(sx * u + ox, oy - sy * v), with (u, v) in the ending's units (journey.json): u = x / 512 across "
                 "the world loop, v = (0.5 y + 0.866 z) / 512 up the sheets.",
        "width": CANONICAL_WIDTH,
        "sx": round(t["sx"] * to_canonical, 4), "ox": round(t["ox"] * to_canonical, 4),
        "sy": round(t["sy"] * to_canonical, 4), "oy": round(t["oy"] * to_canonical, 4),
        "tiles": int(len(centres)),
        "residualMedianPx": round(float(np.median(residual)), 3),
        "residualMaxPx": round(float(residual.max()), 3),
    }
    with open(args.out, "w") as f:
        json.dump(out, f, indent=1)
        f.write("\n")
    print(f"wrote {args.out}: sx {out['sx']} ox {out['ox']} sy {out['sy']} oy {out['oy']}", file=sys.stderr)


if __name__ == "__main__":
    main()
