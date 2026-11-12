#!/usr/bin/env python3
"""Find the true world position of Map Genie POIs in Baby Steps' own scene data.

    python tools/georef/find_anchors.py --game "/path/to/Baby Steps" [--out tools/georef/anchors.json]

Needs numpy and UnityPy (pip install UnityPy numpy) and an installed copy of the game.

1. Reads the main scene (the level that holds BigManagerPrefab: hats, key items and
   other savables) and the 104 world-chunk scenes in the Addressables bundles, and
   composes transforms to get world positions.
2. Pairs POIs with game objects: by the NAMES table below, and for repeated landmarks
   (cans, camps, fire towers, payphones) by the instance nearest each POI's predicted
   position. PINNED pairs are taken as they are, where nothing can predict them. So is a
   movable item's loader, however far Map Genie's marker is from it: there's one of each,
   so it can't be the wrong object, and it's where the game starts the item. The world loops every LOOP_X metres east-west, and the map's torn edges
   show a little of the other side, so each pairing also picks which copy (-1/0/+1
   loops) the POI's map spot shows.
3. Writes anchors.json: POI id, map pixel, game object, world position. That file is
   all build_pois.py needs from the scenes, so the game is only required to refresh it.
"""
import argparse
import glob
import json
import multiprocessing as mp
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tps  # noqa: E402

LOOP_X = 512.0          # world chunks are 4 x 128 m wide and loop east-west
SMOOTHING = 0.003       # TPS smoothing for predicting where Map Genie's markers are in the world

# Map Genie title -> name of the game object that marks it. Followers are left out on
# purpose: the ducklings wander, so their spawn point is not where the map marks them.
# Hats and items are their loaders (see is_item in main), taken even where Map Genie's marker
# is well off: the Paper Hat sits in the river (it floats off once knocked), and the marker is
# 60 m downstream; the Donkey Hat is in Colin's room, the marker 55 m off at a cave; the Trophy
# Hat rides on a tortoise that walks round the world (MoveInDirectionAlongGround: 11 waypoints
# at z 800-825, 1.45 m/s; LoaderFollowTurtle keeps the loader on it), and Map Genie marked one
# spot on its way, 200 m from where it starts.
NAMES = {
    # hats (BigManagerPrefab/GlobalObjectParent/Savables/*_Loader)
    "Propeller Hat": "PropellerHat_Loader", "Cowboy Hat": "StetsonHat_Loader", "Bucket Hat": "BucketHat_Loader",
    "Crown Hat": "Crown_Loader", "Frog Hat": "FrogHat_Loader", "Bike Helmet with Camera": "CrashHelmet_Loader",
    "Box Hat": "BoxHat_Loader", "Straw Hat with Feather": "PinocchioHat_Loader", "Donkey Hat": "DonkeyHat_Loader",
    "Paper Hat": "PaperHat_Loader", "Sunglasses": "SunglassesHat_Loader", "Hard Hat": "SafetyHat_Loader",
    "Pacifier": "SecretHat_Loader", "Winter Hat": "PinkBonnetHat_Loader", "Trophy Hat": "TrophyHat_Loader",
    # key and quest items
    "Pedometer": "Pedometer_Loader", "Clock": "AlarmClock_Loader", "Binoculars": "Binocular_Loader",
    "Ice Cream": "IceCreamCart", "Lantern": "Lantern_Grabable_Loader", "Polaroid Camera": "Polaroid_Loader",
    "Cup": "DadCup_Loader", "Keys": "KeyringTrophy_Loader", "Vase": "BreakablePot_Loader",
    "Chest Key": "TreasureKey_Loader", "\"Falcon\"": "PigeonTrophy_Loader", "Baby Trophy": "WalkingTrophy_Loader",
    # fruit (world-chunk scenes)
    "Apple": "FruitCutsceneApple", "Pear": "FruitCutscenePear", "Orange": "FruitCutsceneOrange",
    "Persimmon": "FruitCutscenePersimmon", "Avocado": "FruitCutsceneAvocado", "Banana": "BananaCutscene",
    "Cherimoya": "FruitCutsceneCherimoya", "Peach": "PeachCutscene", "Papaya": "FruitCutscenePapaya",
    "Fig": "FigCutscene", "Mango": "FruitCutsceneMango", "Pomegranate": "FruitCutscenePomegranate",
    "Kiwi": "KiwiCutscene", "Wax Jambu": "FruitCutsceneWaterApple", "Starfruit": "StarfruitCutscene",
    "Tamarillo": "FruitCutsceneTamarillo", "Watermelon": "FruitCutsceneWatermelon",
    "Passion Fruit": "FruitCutscenePassionfruit", "Cacao": "FruitCutsceneCacao", "Lemon": "FruitCutsceneLemon",
    # landmarks and cutscenes
    "Barn": "Barn", "Carrosel": "CarouselPole", "Ferris Wheel": "FerrisSlice", "Horace Cutscene": "HoraceEncounter",
    "Flea Pit": "JimPitCutscene", "Flea Pit Cutscene": "JimPitCutscene", "Chest": "ChestTop", "Chest opening": "ChestTop",
    "Frog Temple": "Frog", "Grapple Point Cutscene": "GrapplePoint", "Bear Trap Cutscene": "A_Trap_Bear_01.2",
    "Invisible Cubes": "InvisCubesForEgg", "Egg-shaped Stone": "Egg_Brown_LOD0", "Egg Statue": "Egg_Brown_LOD0",
    "Trophy Cove": "TrophyCave", "Ice Cream Stand": "IceCreamCart", "Ice Cream Cutscene": "IceCreamCart",
    "Blue Bucket Cutscene": "HistoricallySignificantBucket", "Holy Bucket": "HistoricallySignificantBucket",
    "Sunbathing Cutscene": "MikeSunBathing", "Jim's Lantern Cutscene": "JimLanternCutscene",
    "Pergola Cutscene": "PergolaIvy", "Norman's House": "Placard_NormanHouse", "Well": "WellTrig", "Seesaw": "SeeSaw",
    "Invisible Trophy": "InvisibleTrophy", "Salt Mine": "MineCart", "Tower Cutscene": "TooHardTower",
    "Tower \"Big and Small\"": "TooHardTower", "Free Shoes Stand": "ShoesClose", "Free Shoes Cutscene": "ShoesClose",
    "Invisible Gym": "GymkhanaSecretInvis", "Meeting Jim Cutscene": "JimHome", "Carrie Cutscene": "CarrieTimeline",
    "Snowman Cutscene": "Snowman", "Moose's Cabin": "Moose_Cabin_Prefab", "Ending Cutscene": "EndingCutscene",
    "Cave": "CaveIndoorWeatherTrig", "Train": "TrainChallenge", "Boulder": "RollingBoulderSnow_Loader",
    "Dry Pool": "Pool_LOD0", "Pool Cutscene": "JimPool", "Outhouse Cutscene": "MikeOuthouse",
    "Soccer Field": "SoccerLine_25", "Soccer/Football Field": "SoccerLine_16",
    "Red Light Green Light": "traffic_light-1B_LOD0", "Always Tired Sand Dunes": "Sand Zone",
    "Mattress": "Old_Mattress_Pile_1", "Box Hell": "Underbox (90)",
}
# Repeated landmarks: each POI takes a different instance (one of these names).
REPEATED = {
    "Stack of Cans": ["PeachCanPile"],
    "Camp": ["CampFirePrefab", "Campfire Trigger", "RollingHIllsCampFirePrefab", "CampFirePrefabQuarry"],
    "Fire Tower": ["FireTower", "FireTower (1)", "FireTower (2)", "FireTower (3)"],
    "Payphone": ["PhoneBooth"],
}
# Pairs taken as they are, though nothing near them can predict their map spot: above Moose's
# Cabin the drawing turns into a stylised peak, which no anchor reached, so the fit only
# extrapolated there. The summit's own trigger is where Map Genie marked the top of the peak.
PINNED = {
    "Secret Ending Cutscene": "MountaintopTrigger",   # the summit: Nate pees off the top
}

SEED_REJECT_PX = 60     # seed pairs further than this from a robust affine fit are dropped
MATCH_MAX_PX = 45       # final pairs further than this from the seed TPS prediction are dropped


# --- reading Unity scenes -------------------------------------------------------------

def _qmul(a, b):
    ax, ay, az, aw = a
    bx, by, bz, bw = b
    return (aw*bx + ax*bw + ay*bz - az*by, aw*by - ax*bz + ay*bw + az*bx,
            aw*bz + ax*by - ay*bx + az*bw, aw*bw - ax*bx - ay*by - az*bz)


def _qrot(q, v):
    x, y, z, w = q
    vx, vy, vz = v
    ix, iy, iz = w*vx + y*vz - z*vy, w*vy + z*vx - x*vz, w*vz + x*vy - y*vx
    iw = -x*vx - y*vy - z*vz
    return (ix*w - iw*x - iy*z + iz*y, iy*w - iw*y - iz*x + ix*z, iz*w - iw*z - ix*y + iy*x)


def scene_objects(path):
    """[(name, x, y, z, hierarchy path)] for every GameObject in a scene file or bundle."""
    import UnityPy
    rows = []
    env = UnityPy.load(path)
    files = []
    for f in env.files.values():
        files += [f] if hasattr(f, "objects") else [x for x in (getattr(f, "files", {}) or {}).values() if hasattr(x, "objects")]
    for sf in files:
        tr, names = {}, {}
        for pid, o in sf.objects.items():
            t = o.type.name
            try:
                if t == "GameObject":
                    names[pid] = o.read().m_Name
                elif t in ("Transform", "RectTransform"):
                    d = o.read()
                    p, r, s = d.m_LocalPosition, d.m_LocalRotation, d.m_LocalScale
                    father = d.m_Father.path_id if d.m_Father.file_id == 0 else 0
                    tr[pid] = ((p.x, p.y, p.z), (r.x, r.y, r.z, r.w), (s.x, s.y, s.z), father, d.m_GameObject.path_id)
            except Exception:
                pass
        cache = {}

        def world(tid):
            chain = []
            while tid and tid in tr and tid not in cache:
                chain.append(tid)
                tid = tr[tid][3]
            parent = cache.get(tid)
            for t in reversed(chain):
                p, r, s, _, _ = tr[t]
                if parent is None:
                    cache[t] = (p, r, s)
                else:
                    fp, fr, fs = parent
                    rp = _qrot(fr, (p[0]*fs[0], p[1]*fs[1], p[2]*fs[2]))
                    cache[t] = ((fp[0]+rp[0], fp[1]+rp[1], fp[2]+rp[2]), _qmul(fr, r), (fs[0]*s[0], fs[1]*s[1], fs[2]*s[2]))
                parent = cache[t]
            return cache[chain[0]] if chain else parent

        for tid, (_, _, _, _, go) in tr.items():
            name = names.get(go)
            if name is None:
                continue
            (x, y, z), _, _ = world(tid)
            hp, t = [], tid
            while t and t in tr and len(hp) < 40:
                hp.append(names.get(tr[t][4], "?"))
                t = tr[t][3]
            rows.append((name, x, y, z, "/".join(reversed(hp))))
    return rows


def _scene_bundle(path):
    import UnityPy
    try:
        env = UnityPy.load(path)
        names = [k for f in env.files.values() for k in (getattr(f, "files", {}) or {})]
        return path if any(n.endswith(".sharedAssets") for n in names) else None
    except Exception:
        return None


def _chunk_rows(path):
    return [r for r in scene_objects(path) if "SampleScene_CHUNK_" in r[4]]


def load_world_objects(game):
    data = os.path.join(game, "BabySteps_Data")
    main = None
    for lvl in sorted(glob.glob(os.path.join(data, "level*")), key=lambda p: int(p.rsplit("level", 1)[1].split(".")[0] or 0)):
        if lvl.endswith(".resS"):
            continue
        rows = scene_objects(lvl)
        if any(r[0] == "BigManagerPrefab" for r in rows):
            main = [r for r in rows if r[4].startswith("BigManagerPrefab/GlobalObjectParent/")]
            print(f"main scene: {os.path.basename(lvl)} ({len(main)} global objects)")
            break
    if main is None:
        sys.exit("find_anchors: no level with BigManagerPrefab found")
    bundles = sorted(glob.glob(os.path.join(data, "StreamingAssets", "aa", "StandaloneWindows64", "*.bundle")))
    with mp.Pool() as pool:
        scenes = [p for p in pool.imap_unordered(_scene_bundle, bundles, chunksize=32) if p]
        chunks = [r for rows in pool.imap_unordered(_chunk_rows, scenes) for r in rows]
    print(f"world chunks: {len(scenes)} scene bundles, {len(chunks)} objects")
    return main + chunks


# --- pairing ----------------------------------------------------------------------------

def robust_affine(world, pixels, iters=2000, tol=40.0, seed=7):
    rng = np.random.default_rng(seed)
    a = np.c_[world, np.ones(len(world))]
    best = None
    for _ in range(iters):
        idx = rng.choice(len(world), 3, replace=False)
        coef = np.linalg.lstsq(a[idx], pixels[idx], rcond=None)[0]
        inl = np.linalg.norm(a @ coef - pixels, axis=1) < tol
        if best is None or inl.sum() > best.sum():
            best = inl
    return np.linalg.lstsq(a[best], pixels[best], rcond=None)[0]


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--game", required=True, help="Baby Steps install folder (contains BabySteps_Data)")
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument("--pois", default=os.path.join(here, "mapgenie_pois.json"))
    ap.add_argument("--out", default=os.path.join(here, "anchors.json"))
    args = ap.parse_args()

    pois = json.load(open(args.pois))
    objects = load_world_objects(args.game)
    by_name = {}
    for o in objects:
        by_name.setdefault(o[0], []).append(o)

    # 1. Seeds: named pairs with exactly one candidate object, fitted with a robust affine.
    seeds = []
    for p in pois:
        name = NAMES.get(p["title"])
        if name and len(by_name.get(name, [])) == 1:
            seeds.append((p, by_name[name][0]))
    world = np.array([[o[1], o[3]] for _, o in seeds])
    pixels = np.array([[p["px"], p["py"]] for p, _ in seeds])
    coef = robust_affine(world, pixels)
    for _ in range(2):  # pick the loop copy each seed's map spot shows, then refit
        shift = np.array([min((-1, 0, 1), key=lambda k: np.linalg.norm(np.r_[w + [k * LOOP_X, 0], 1] @ coef - px))
                          for w, px in zip(world, pixels)])
        shifted = world + np.c_[shift * LOOP_X, np.zeros(len(world))]
        err = np.linalg.norm(np.c_[shifted, np.ones(len(world))] @ coef - pixels, axis=1)
        coef = np.linalg.lstsq(np.c_[shifted, np.ones(len(world))][err < SEED_REJECT_PX], pixels[err < SEED_REJECT_PX], rcond=None)[0]
    keep = err < SEED_REJECT_PX
    for (p, o), e in zip(seeds, err):
        if e >= SEED_REJECT_PX:
            print(f"  seed rejected ({e:.0f}px from affine): {p['title']} <- {o[0]}")
    seed_model = tps.fit(shifted[keep], pixels[keep], SMOOTHING)
    print(f"seeds: {keep.sum()} of {len(seeds)} kept")

    # 2. All pairs, each POI to its nearest candidate instance/loop copy by predicted pixel.
    def candidates(names):
        return [(o, k) for n in names for o in by_name.get(n, []) for k in (-1, 0, 1)]

    anchors, rejected = [], []

    def add(p, o, k, d):
        anchors.append({"poi": p["id"], "title": p["title"], "category": p["catName"], "px": p["px"], "py": p["py"],
                        "object": o[0], "path": o[4], "x": round(o[1], 3), "y": round(o[2], 3), "z": round(o[3], 3),
                        "loop": k, "seedErrorPx": round(d, 1)})

    def is_item(name):
        """A movable item's loader: the one object of that name, among the main scene's savables."""
        objs = by_name.get(name, [])
        return len(objs) == 1 and "/GlobalObjectParent/Savables/" in objs[0][4]

    for p in pois:
        name = NAMES.get(p["title"])
        if not name:
            continue
        cands = candidates([name])
        if not cands:
            rejected.append(f"{p['title']}: no object named {name!r}")
            continue
        pred = tps.evaluate(seed_model, [[o[1] + k * LOOP_X, o[3]] for o, k in cands])
        dist = np.linalg.norm(pred - [p["px"], p["py"]], axis=1)
        i = int(np.argmin(dist))
        if dist[i] > MATCH_MAX_PX:
            if not is_item(name):
                rejected.append(f"{p['title']} ({p['px']:.0f},{p['py']:.0f}) <- {name}: {dist[i]:.0f}px from prediction")
                continue
            add(p, *cands[i], float(dist[i]))
            anchors[-1]["pinned"] = True
            print(f"item: {p['title']} <- {name}, its loader, though Map Genie's marker is {dist[i]:.0f}px from the prediction")
            continue
        add(p, *cands[i], float(dist[i]))

    for title, name in PINNED.items():
        ps = [p for p in pois if p["title"] == title]
        objs = by_name.get(name, [])
        if len(ps) != 1 or len(objs) != 1:
            rejected.append(f"{title} <- {name}: pinned, but {len(ps)} such POIs and {len(objs)} such objects")
            continue
        p, o = ps[0], objs[0]
        k = min((-1, 0, 1), key=lambda k: abs(o[1] + k * LOOP_X - 256.0))  # the map shows the copy in [0, 512)
        pred = tps.evaluate(seed_model, [[o[1] + k * LOOP_X, o[3]]])[0]
        add(p, o, k, float(np.linalg.norm(pred - [p["px"], p["py"]])))
        anchors[-1]["pinned"] = True
        print(f"pinned: {title} <- {name} ({anchors[-1]['seedErrorPx']:.0f}px from the extrapolated prediction)")

    for title, names in REPEATED.items():
        ps = [p for p in pois if p["title"] == title]
        cands = candidates(names)
        pred = tps.evaluate(seed_model, [[o[1] + k * LOOP_X, o[3]] for o, k in cands])
        pairs = sorted((float(np.linalg.norm(pred[j] - [p["px"], p["py"]])), i, j) for i, p in enumerate(ps) for j in range(len(cands)))
        used_p, used_o = set(), set()
        for d, i, j in pairs:
            o, k = cands[j]
            key = (round(o[1], 1), round(o[3], 1))
            if d > MATCH_MAX_PX or i in used_p or key in used_o:
                continue
            used_p.add(i)
            used_o.add(key)
            add(ps[i], o, k, d)
        for i, p in enumerate(ps):
            if i not in used_p:
                rejected.append(f"{title} ({p['px']:.0f},{p['py']:.0f}): no {'/'.join(names)} within {MATCH_MAX_PX}px")

    anchors.sort(key=lambda a: -a["py"])
    with open(args.out, "w") as f:
        json.dump({"loopX": LOOP_X, "note": "Generated by tools/georef/find_anchors.py from the game's scene data.",
                   "anchors": anchors}, f, indent=1)
        f.write("\n")
    print(f"wrote {len(anchors)} anchors to {args.out}")
    for r in rejected:
        print("  not anchored:", r)


if __name__ == "__main__":
    main()
