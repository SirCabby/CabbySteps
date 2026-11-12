#!/usr/bin/env python3
"""Write the mod's POI list: where each POI is in the world and on the game's painted map.

    python3 tools/georef/build_pois.py

The map is the game's own: the six painted sheets its ending shows (journey.json), and the game
puts places on them itself, when it records where Nate goes for that ending
(SaveGod.SaveForever, read from its machine code):

    u = (x mod 512) / 512                      across the sheets (the world loops every 512 m)
    v = (0.5 y + 0.866 z) / 512                up them: sin 30 * height + cos 30 * z

The sheets are painted as seen from the south, looking down at 60 degrees, so a place's height
moves it up the map as much as its distance north does (half as much, metre for metre). The
mod draws Nate and everything with a world position by that projection (src/Map/MapProjection.cs),
in the ending's units (u, v) like these.

Reads tools/georef/anchors.json (find_anchors.py), mapgenie_pois.json (Map Genie's POIs: pixels
on their map), mapgenie.json (register_mapgenie.py: where their map sits on the sheets) and
game_links.json (find_links.py), and writes package/Mods/CabbySteps/pois.json, every POI
with:

  u, v        where it's drawn: the game's projection of its world position, where the game has
              one for it (its game object, where its cutscene is set off, its campfire); else
              Map Genie's marker, which is on the same painting
  mgPx, mgPy  Map Genie's marker (canonical pixels of their 2048-wide map)
  wx, wy, wz  where it is in the world. Without wy (no game object behind it), x comes from the
              marker and z from the marker and a height guessed from the POIs round it
  plus the links from game_links.json: what the save records when a POI is done (or the best
  time that makes it done) or an item has left the game, and movable objects.

Markers that land on one spot are fanned out round it, so each can be picked. Needs Python 3 only.
"""
import argparse
import json
import math
import os
import statistics

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
ASSETS = os.path.join(ROOT, "package", "Mods", "CabbySteps")

LOOP_X = 512.0               # the world loops east-west every 512 m (four 128 m chunk columns)
METRES_PER_UNIT = 512.0      # the ending's units: u 0..1 across the loop
SIN = 0.5                    # the game's map: v = (sin 30 * y + cos 30 * z) / 512
COS = math.cos(math.radians(30.0))

# Map Genie markers left out of pois.json, with the reason.
DROP = {
    488869: "a second Pear: drawn in the parchment's torn right edge (which repeats the world's west end) "
            "45 m from the real one; the game has a single pear (FruitCutscenePear, POI 893837)",
}

CAMP_MAX_M = 150.0      # a camp's campfire is the nearest one, within this
STACKED_U = 0.006       # markers closer than this (units: about 3 m of map) are on the same spot...
FAN_U = 0.009           # ...and are fanned out this far round it
HEIGHT_NEIGHBOURS = 6   # a POI with no game object takes its height from this many round it

REFERENCE_CATEGORIES = ("Point of Interest", "Graffiti")  # landmarks (the mod's Poi.ReferenceCategories)


def project(x, y, z):
    """The game's own map position of a world position: (u, v) in the ending's units."""
    return (x % LOOP_X) / METRES_PER_UNIT, (SIN * y + COS * z) / METRES_PER_UNIT


def loop_du(du):
    """An across-the-map difference the short way round the loop."""
    return (du + 0.5) % 1.0 - 0.5


def loop_distance(ax, az, bx, bz):
    """Horizontal metres between two places, the short way round the world's east-west loop."""
    dx = (ax - bx) % LOOP_X
    return math.hypot(min(dx, LOOP_X - dx), az - bz)


def stack_rank(q):
    """Which marker of a stack keeps the spot: something to do first, then an item, a landmark last;
    of those, one the game has a place for before one on Map Genie's marker."""
    kind = 2 if q["catName"] in REFERENCE_CATEGORIES else (1 if q.get("item") else 0)
    return kind, 0 if "wy" in q else 1


def fan_out(out):
    """Markers closer than STACKED_U stack up: the first by stack_rank keeps its spot, the others
    go round it, FAN_U out (more for a big stack), until no two are that close. Returns the
    stacks, as lists of indices into out, the keeper first."""
    home = [(q["u"], q["v"]) for q in out]
    group_of = list(range(len(out)))  # each marker's stack, by the index of one of its markers

    def stack(i):
        while group_of[i] != i:
            i = group_of[i]
        return i

    def close(a, b):
        return math.hypot(loop_du(a[0] - b[0]), a[1] - b[1]) < STACKED_U

    near = [(i, j) for i in range(len(out)) for j in range(i + 1, len(out)) if close(home[i], home[j])]
    stacks = []
    for _ in range(6):
        for i, j in near:
            group_of[stack(i)] = stack(j)
        by_stack = {}
        for i in range(len(out)):
            by_stack.setdefault(stack(i), []).append(i)
        stacks = [sorted(g, key=lambda j: (stack_rank(out[j]), out[j]["id"])) for g in by_stack.values() if len(g) > 1]
        for g in stacks:
            ku, kv = home[g[0]]
            out[g[0]]["u"], out[g[0]]["v"] = ku, kv
            others = g[1:]
            radius = FAN_U * max(1.0, len(others) / 4.0)
            for n, j in enumerate(others):
                angle = 2 * math.pi * n / len(others)
                out[j]["u"], out[j]["v"] = ku + radius * math.sin(angle), kv + radius * math.cos(angle)
        near = [(i, j) for i in range(len(out)) for j in range(i + 1, len(out))
                if stack(i) != stack(j) and close((out[i]["u"], out[i]["v"]), (out[j]["u"], out[j]["v"]))]
        if not near:
            break
    return stacks


def guess_height(u, v, known):
    """Height (m) at map spot (u, v): inverse-distance weighted over the nearest POIs whose height is known."""
    nearest = sorted((math.hypot(loop_du(u - ku), v - kv), y) for ku, kv, y in known)[:HEIGHT_NEIGHBOURS]
    if nearest[0][0] < 1e-6:
        return nearest[0][1]
    weights = [1.0 / (d * d) for d, _ in nearest]
    return sum(w * y for w, (_, y) in zip(weights, nearest)) / sum(weights)


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--anchors", default=os.path.join(HERE, "anchors.json"))
    ap.add_argument("--pois", default=os.path.join(HERE, "mapgenie_pois.json"))
    ap.add_argument("--mapgenie", default=os.path.join(HERE, "mapgenie.json"))
    ap.add_argument("--links", default=os.path.join(HERE, "game_links.json"))
    ap.add_argument("--out", default=os.path.join(ASSETS, "pois.json"))
    args = ap.parse_args()

    anchors = json.load(open(args.anchors))["anchors"]
    mg = json.load(open(args.mapgenie))
    links, campfires = {}, []
    if os.path.exists(args.links):
        link_file = json.load(open(args.links))
        links = {link["poi"]: link for link in link_file["links"]}
        campfires = link_file.get("campfires", [])
    by_poi = {}
    for a in anchors:
        by_poi.setdefault(a["poi"], a)

    def marker_units(px, py):
        return (px - mg["ox"]) / mg["sx"], (mg["oy"] - py) / mg["sy"]

    out, dropped = [], 0
    world = {}      # POI id -> (x, y, z, what): where the game has it
    marker = {}     # POI id -> (u, v) of Map Genie's marker
    agreement = []  # (metres between Map Genie's marker and the game's projection of the object, title)
    for p in json.load(open(args.pois)):
        if p["id"] in DROP:
            dropped += 1
            continue
        q = {k: p[k] for k in ("id", "cat", "catName", "icon", "title", "lat", "lng") if k in p}
        q["mgPx"], q["mgPy"] = p["px"], p["py"]
        marker[p["id"]] = marker_units(p["px"], p["py"])
        a = by_poi.get(p["id"])
        if a:
            q["anchor"] = a["object"]
            world[p["id"]] = (a["x"] % LOOP_X, a["y"], a["z"], None)
            # Map Genie's marker against the game's projection, in the copy of the loop it's in.
            mu, mv = marker[p["id"]]
            u, v = project(a["x"] + a.get("loop", 0) * LOOP_X, a["y"], a["z"])
            u += a.get("loop", 0)
            agreement.append((math.hypot(mu - u, mv - v) * METRES_PER_UNIT, p["title"]))
        link = links.get(p["id"], {})
        for key in ("item", "found", "gone", "best"):
            if link.get(key):
                q[key] = link[key]
        if "at" in link:
            world[p["id"]] = (*link["at"], link["atFrom"])
        out.append(q)

    # A first guess at the world spot of every POI the game has no place for: x from Map Genie's
    # marker, and z from it and the height round there.
    known = [(*project(x, y, z), y) for x, y, z, _ in world.values()]

    def from_marker(q):
        u, v = marker[q["id"]]
        y = guess_height(u, v, known)
        return (u * METRES_PER_UNIT) % LOOP_X, (v * METRES_PER_UNIT - SIN * y) / COS

    # Camps: the game's campfire nearest each (where Nate sleeps, and its evening and morning play).
    camps = [q for q in out if q["title"] == "Camp" and not (q["id"] in world and world[q["id"]][3])]
    spots = {q["id"]: (world[q["id"]][0], world[q["id"]][2]) if q["id"] in world else from_marker(q) for q in camps}
    pairs = sorted((loop_distance(*spots[q["id"]], c["at"][0], c["at"][2]), i, j)
                   for i, q in enumerate(camps) for j, c in enumerate(campfires))
    used_q, used_c = set(), set()
    for d, i, j in pairs:
        if d > CAMP_MAX_M or i in used_q or j in used_c:
            continue
        used_q.add(i)
        used_c.add(j)
        world[camps[i]["id"]] = (*campfires[j]["at"], "campfire " + campfires[j]["object"])
    for i, q in enumerate(camps):
        if i not in used_q:
            print(f"  ! camp {q['id']} has no campfire within {CAMP_MAX_M:.0f} m")

    # Where the game has a POI, it's drawn by the game's projection: where the mod shows Nate when
    # he's there. The rest stay on Map Genie's marker (on the same painting), with a world spot
    # read back off it.
    known = [(*project(x, y, z), y) for x, y, z, _ in world.values()]
    placed, guessed, far = 0, 0, []
    for q in out:
        place = world.get(q["id"])
        if place:
            x, y, z, what = place
            q["u"], q["v"] = project(x, y, z)
            q["wx"], q["wy"], q["wz"] = round(x % LOOP_X, 2), round(y, 2), round(z, 2)
            if what:
                q["at"] = what
                placed += 1
            mu, mv = marker[q["id"]]
            off = math.hypot(loop_du(mu - q["u"]), mv - q["v"]) * METRES_PER_UNIT
            if what and off > 10.0:
                far.append((off, q["title"], what))
        else:
            q["u"], q["v"] = marker[q["id"]]
            x, z = from_marker(q)
            q["wx"], q["wz"] = round(x, 2), round(z, 2)
            guessed += 1

    # Markers on one spot (a camp, and the evening and morning played at its campfire; a chest and
    # the cutscene of opening it) are fanned out, so the crosshair can pick each: what there is to
    # do keeps the spot, landmarks and items step aside. World positions stay exact.
    groups = fan_out(out)
    if groups:
        print(f"fanned out {len(groups)} stacks of markers (the first keeps the spot): "
              + "; ".join(" + ".join(out[j]["title"] for j in g) for g in groups))

    order = ("id", "cat", "catName", "icon", "title", "lat", "lng", "u", "v", "mgPx", "mgPy", "wx", "wy", "wz",
             "anchor", "at", "item", "found", "gone", "best")
    rows = []
    for q in out:
        q["u"], q["v"] = round(q["u"], 6), round(q["v"], 6)
        rows.append({k: q[k] for k in order if k in q})
    with open(args.out, "w") as f:
        json.dump(rows, f, indent=1, ensure_ascii=False)
        f.write("\n")

    print(f"wrote {os.path.relpath(args.out, ROOT)}: {len(rows)} POIs ({len(rows) - guessed} where the game has them, "
          f"{guessed} on Map Genie's marker; {dropped} duplicate left out; "
          f"{sum('found' in q or 'best' in q for q in rows)} found automatically, {sum('item' in q for q in rows)} movable)")
    print(f"placed {placed} POIs where the game sets them off (cutscene triggers, campfires)")
    for d, title, what in sorted(far, reverse=True):
        print(f"  {d:5.0f} m of map from Map Genie's marker: {title} ({what})")
    if agreement:
        agreement.sort(reverse=True)
        metres = [m for m, _ in agreement]
        print(f"Map Genie's markers against the game's projection of their {len(metres)} game objects: "
              f"median {statistics.median(metres):.1f} m of map, 90% within {sorted(metres)[int(len(metres) * 0.9) - 1]:.1f} m, "
              f"worst {metres[0]:.0f} m ({agreement[0][1]})")


if __name__ == "__main__":
    main()
