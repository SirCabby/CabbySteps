#!/usr/bin/env python3
"""Link Map Genie POIs to what the game itself records about them.

    python tools/georef/find_links.py --game "/path/to/Baby Steps" [--out tools/georef/game_links.json]

Needs UnityPy (pip install UnityPy) and an installed copy of the game. Run
find_anchors.py first; build_pois.py then copies the links into pois.json.

Four kinds of link, all keys in the save's "dialogs played" set unless noted:

* found: the POI is done, so the mod ticks it off by itself.
    - A cutscene starter (BBConvoStarter) records its timeline's name when it has played.
      That covers fruit, cutscenes, payphone calls and trophy achievements. (Points of
      Interest, camps included, are for reference only: nothing ticks them off.) This tool reads each starter's timeline name from the game,
      so only the starters' object names are written down here.
    - A can pile (KinematicUntilTouchedByPlayerGroupDad) records "CANS<x>.<z>" once
      knocked over: its position (x modulo 512, z modulo 3328), rounded.
    - A hat's dream (Hat.dream, a HatDream name such as "stetson"), which plays when
      Nate sleeps at a campfire wearing it.
* item: the loader of a movable object (hats, items, ducklings), under
  BigManagerPrefab/GlobalObjectParent/Savables. The mod follows the real object, so the
  POI moves when the object does. Finding an item and carrying it about doesn't make it
  done; only a hat's dream ticks one off.
* gone: the item has left the game (a trophy handed in, the vase broken), so the mod
  takes its POI off the map.
* best: a best time the save keeps (SaveFile.miscNums, by key) and the time it has to be
  under: then the POI is done (The Greatest: the gym's best time under the designer's). Only
  the BEST_TIMES table below; the game's code holds the times, not its scenes.

It also records where each linked cutscene is set off, so the map doesn't depend on where
Map Genie put the marker ("at", "atFrom"): the campfire that plays it (the morning and evening
scenes, Checkpoint.morningStarter / eveningStarter), else a trigger script pointing at the
starter (BBConvoTrigger and the like: its trigger collider's centre), else the starter's own
trigger collider, else the starter's own position. And every campfire, for the camps.
"""
import argparse
import glob
import json
import multiprocessing as mp
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from find_anchors import _qmul, _qrot, _scene_bundle, scene_objects  # noqa: E402

LOOP_X = 512.0
LOOP_Z = 3328.0   # the can piles' save keys wrap z by this too

# Cutscene POIs (and the like) -> the BBConvoStarter object(s) whose timeline marks them done.
# Matched by place and subject, checked one by one. Any one of several starters counts
# (e.g. the two routes up to the top of the cliff).
STARTERS = {
    # cutscenes
    488852: ["JimTimeline"],                    # Meeting Jim
    488853: ["JimCapCutsceneREDUX"],            # Exiting Cave
    488861: ["JimHole"],                        # Mud Slide
    488864: ["ShoeSeller"],                     # Free Shoes
    488868: ["MikeCutscene"],                   # Onward and Upward (first camp, morning)
    488871: ["JimPitCutscene"],                 # Flea Pit
    488874: ["HoraceEncounter"],                # Horace
    488878: ["JimPortaPotty"],                  # Portaloo
    488882: ["JimInjuryTimeline (1)"],          # Screaming
    488889: ["JimCairn"],                       # Rock
    488891: ["MikeGrappling"],                  # Grapple Point
    488893: ["EthanTimeline"],                  # Onward and Upward (forest camp, morning)
    488907: ["BearTrapCutscene (1)"],           # Bear Trap
    488912: ["MikeTower1"],                     # Bottom of the Tower
    488913: ["MikeTower2"],                     # Partway up the Tower
    488930: ["Gnomes"],                         # Retirement (Nate sits on a gnome)
    488932: ["MikeLemmy", "MikeBark"],          # Jumping
    488945: ["MikeOuthouse"],                   # Outhouse
    488951: ["JesseCampMorning"],               # Onward and Upward (slopes camp, morning)
    488953: ["BeachMorning"],                   # Onward and Upward (beach camp, morning)
    488957: ["SecondJesseEncounter"],           # Staircase
    488962: ["MitchellIceCreamStart"],          # Ice Cream
    488968: ["JimPool"],                        # Pool
    488970: ["JimBucket"],                      # Blue Bucket
    488974: ["MikeSunBathing"],                 # Sunbathing
    488979: ["JimLanternCutscene"],             # Jim's Lantern
    488980: ["MooseLanternCutscene"],           # Inspector Gadget's Lantern
    488984: ["ForetressPartyTimeline", "WishRoom"],  # Wishing Camp
    488985: ["FortressMorning"],                # Onward and Upward (fortress camp, morning)
    488987: ["MooseIceCreamTruck"],             # Onward and Upward (hills camp, morning)
    488995: ["Frolic"],                         # Childish Games
    488996: ["EthanMilkLake"],                  # Pergola
    489005: ["JimWell"],                        # Well
    489013: ["MikeJenga"],                      # Tower
    489021: ["QuarryMorning"],                  # Onward and Upward (quarry camp, morning)
    489027: ["JimQuarryHole"],                  # Danger
    489035: ["JimStairBottom"],                 # Bottom of the Staircase
    489036: ["MikeOnManbreaker"],               # Partway up the Cliff
    489037: ["JimStairsLose", "JimStairsWin"],  # Top of the Cliff (by the cliff or the stairs)
    489039: ["SnowMorning"],                    # Onward and Upward (snow camp, morning)
    489040: ["CarrieTimeline"],                 # Carrie
    489041: ["Snowman"],                        # Snowman
    489047: ["MikeCrushed"],                    # Between a Rock and a Hard Place
    489053: ["EndingCutscene", "EndingCutsceneSkipgameAlternate"],  # Ending
    489054: ["MountaintopCutscene"],            # Secret Ending (the summit)
    503561: ["Mitchell_ChestScene"],            # Chest opening
    # payphones
    488896: ["DadCall"],
    488978: ["DadCallDesert"],
    489048: ["DadCallTrain"],
    # fruit Map Genie misses an anchor for: Mike eats the rambutan before Nate can
    489033: ["MikeAteTheFruit"],
    # trophies handed in: the achievements (the quest items go, see GONE)
    488876: ["TrophyNormalDropoffCutscene"],        # World's Best Dad
    488918: ["TrophyKeyringDropoffCutscene (1)"],   # Most Perceptive Person
    488964: ["TrophyIceDropoffCutscene"],           # Coolest Hand
    489011: ["TrophyRunAwayDropoffCutscene"],       # Most Responsible Person
    489025: ["TrophyPigeonDropoffCutscene"],        # Mister Falconer
    489003: ["InvisibleTrophyCutscene"],            # Invisible Trophy
}

# Keys recorded by other code than a cutscene starter, or by a starter whose timeline
# lives in another asset bundle than its scene (this tool only reads the scene's own).
LITERAL = {
    488851: ["StartGameTimeline_New"],  # Intro Cutscene
    488937: ["TrophyFragileDropOff"],   # Most Careful Person (TrophyFragileDropoffCutscene)
}

# Where to place those: the starter that plays it, by object name.
LITERAL_STARTERS = {
    488937: ["TrophyFragileDropoffCutscene"],  # Most Careful Person: its hand-in trigger
}

# Movable objects: POI -> the loader of the real object.
ITEMS = {
    # hats
    488854: "StetsonHat_Loader", 488856: "PropellerHat_Loader", 488879: "BucketHat_Loader",
    488888: "Crown_Loader", 488898: "CrashHelmet_Loader", 488903: "FrogHat_Loader",
    488923: "BoxHat_Loader", 488939: "PinocchioHat_Loader", 488941: "DonkeyHat_Loader",
    488954: "TrophyHat_Loader", 489002: "SunglassesHat_Loader", 489018: "PaperHat_Loader",
    489034: "SafetyHat_Loader", 489043: "SecretHat_Loader", 489045: "PinkBonnetHat_Loader",
    # key items
    488857: "Pedometer_Loader", 488900: "AlarmClock_Loader", 488929: "Binocular_Loader",
    488981: "Lantern_Grabable_Loader", 488997: "Polaroid_Loader",
    # The ice cream (an IceMelter; save key "IceTrophy"): Mitchell's IceTrophyHelper at the cart
    # keeps it switched off there until a MitchellIceCream cutscene hands it to Nate, and
    # puts it back at the cart for the next one once it has melted or been left behind.
    488982: "IceTrophy_Loader",
    # quest items
    488860: "DadCup_Loader", 488909: "KeyringTrophy_Loader", 488935: "BreakablePot_Loader",
    488989: "TreasureKey_Loader", 489010: "WalkingTrophy_Loader", 489026: "PigeonTrophy_Loader",
    # followers
    488870: "Huey_Loader", 488920: "Dewie_Loader", 500232: "Louie_Loader",
}

# Hats -> their dream: each hat prefab's Hat.dream (HatDream enum), read from the game's
# asset bundles. The save records the enum's name once that dream has played. The Pacifier
# (SecretHat) and the Winter Hat (PinkBonnetHat) have none, so nothing ticks them off.
HAT_DREAMS = {
    488854: "stetson",       # Cowboy Hat (StetsonHat)
    488856: "propeller",     # Propeller Hat
    488879: "bucket",        # Bucket Hat
    488888: "crown",         # Crown Hat (Crown)
    488898: "goPro",         # Bike Helmet with Camera (CrashHelmet)
    488903: "frog",          # Frog Hat
    488923: "box",           # Box Hat
    488939: "pinnochio",     # Straw Hat with Feather (PinocchioHat)
    488941: "donkey",        # Donkey Hat
    488954: "trophy",        # Trophy Hat
    489002: "glases",        # Sunglasses (SunglassesHat)
    489018: "boat",          # Paper Hat (shares the boat dream with the NavyHat)
    489034: "construction",  # Hard Hat (SafetyHat)
}

# Items that leave the game, and the record that says so: cutscene starters by object
# name, or (prefixed "=") literal keys.
GONE = {
    488860: ["TrophyNormalDropoffCutscene"],                  # Cup, handed in
    488909: ["TrophyKeyringDropoffCutscene (1)"],             # Keys, handed in
    488935: ["=TrophyFragileDropOff", "=VaseBroken"],         # Vase: handed in, or broken (SaveGod.BREAK_VASE)
    # Ice Cream, handed in: the hand-in's cutscene, or the "<save key>_used" record that marks a
    # quest item used up (Savable.Load switches the item off for it; IceTrophyHelper reads it)
    488982: ["TrophyIceDropoffCutscene", "=IceTrophy_used"],
    488989: ["Mitchell_ChestScene"],                          # Chest Key, used on the chest
    489010: ["TrophyRunAwayDropoffCutscene"],                 # Baby Trophy, handed in
    489026: ["TrophyPigeonDropoffCutscene"],                  # "Falcon", handed in
}

# Done once a best time the save keeps (SaveFile.miscNums, by key) is under a time, in seconds.
# The Greatest ("Beat the designer's time in the gym"): JesseGym.ProcessCheckpoint, at the
# finish of the Underwater Gym's course with every checkpoint passed, keeps the run's time as
# "GymPB" if it's a best, then unlocks the achievement (THE_GREATEST) if that best is under
# JesseGym.developerTime, which the class's static constructor sets to 78.24 (read from the
# game's code, build of 12.12.2025). No other record is kept: no cutscene, nothing in
# dialogsPlayed. The Invisible Gym is a JesseGym too and writes its own finishes to "GymPB".
BEST_TIMES = {
    488949: ("GymPB", 78.24),  # The Greatest
}


def _read_str(raw, off):
    n = struct.unpack_from("<i", raw, off)[0]
    if n < 0 or n > 4096:
        raise ValueError("bad string length")
    s = raw[off + 4:off + 4 + n].decode("utf8", "replace")
    return s, (off + 4 + n + 3) & ~3


# Scripts that set a cutscene starter off, and where in their serialized fields (bytes after
# the MonoBehaviour's m_Name) the starter's PPtr is. A campfire (Checkpoint) plays its evening
# and morning starters when Nate sleeps there: onFire, offFire (PPtr), onFireState, offFireState
# (enum), hatDropSpot (PPtr), id (int), eveningStarter, morningStarter (PPtr).
SETTERS_OFF = {
    "BBConvoTrigger": [(0, "trigger")],
    "BBConvoWaitingTrigger": [(0, "waiting trigger")],
    "BBConvoHoldingItemTrigger": [(0, "hand-in trigger")],
    "BBConvoFallingTrigger": [(0, "falling trigger")],
    "SoccerGoalTrigger": [(0, "goal trigger")],
    "StepDetector": [(4, "step trigger")],       # after its float rad
}
CHECKPOINT_STARTERS = [(48, "evening"), (60, "morning")]
COLLIDERS = ("BoxCollider", "SphereCollider", "CapsuleCollider", "MeshCollider")


def scene_scripts(path):
    """[(class, object name, x, y, z, timeline, extra)] for the cutscene starters, can piles and
    campfires in a scene. A starter's extra is where it's set off from, best first:
    {"rank", "kind", "object", "at"} (rank 0 the campfire that plays it, 1 a trigger script
    pointing at it, 2 its own trigger, 3 just its own position). A campfire's extra is
    {"at", "evening", "morning"} (the starters' object names)."""
    import UnityPy
    out = []
    env = UnityPy.load(path)
    files = []
    for f in env.files.values():
        files += [f] if hasattr(f, "objects") else [x for x in (getattr(f, "files", {}) or {}).values() if hasattr(x, "objects")]
    by_name = {sf.name: sf for sf in files}
    for sf in files:
        if sf.name.endswith(".sharedAssets"):
            continue
        ext = [e.path.split("/")[-1] for e in sf.externals]

        def resolve(file_id, path_id):
            if file_id == 0:
                return sf.objects.get(path_id)
            target = by_name.get(ext[file_id - 1]) if file_id - 1 < len(ext) else None
            return target.objects.get(path_id) if target else None

        tr, names, directors, behaviours, triggers = {}, {}, {}, [], {}
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
                elif t == "PlayableDirector":
                    d = o.read()
                    directors[pid] = (d.m_PlayableAsset.file_id, d.m_PlayableAsset.path_id)
                elif t == "MonoBehaviour":
                    raw = o.get_raw_data()
                    # MonoBehaviour base: m_GameObject (PPtr), m_Enabled (+align), m_Script (PPtr), m_Name
                    _, go = struct.unpack_from("<iq", raw, 0)
                    sfile, spid = struct.unpack_from("<iq", raw, 16)
                    behaviours.append((pid, go, sfile, spid, raw))
                elif t in COLLIDERS:
                    d = o.read()
                    if getattr(d, "m_IsTrigger", False):
                        c = getattr(d, "m_Center", None)
                        triggers.setdefault(d.m_GameObject.path_id, (c.x, c.y, c.z) if c is not None else (0.0, 0.0, 0.0))
            except Exception:
                pass
        go_transform = {v[4]: k for k, v in tr.items()}
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

        def point(go, local=(0.0, 0.0, 0.0)):
            """World position of a point given in a game object's own space."""
            (x, y, z), r, s = world(go_transform[go])
            o = _qrot(r, (local[0]*s[0], local[1]*s[1], local[2]*s[2]))
            return (x + o[0], y + o[1], z + o[2])

        def trigger_or_position(go):
            return point(go, triggers[go]) if go in triggers else point(go)

        classes = {}
        for pid, go, sfile, spid, raw in behaviours:
            if go not in go_transform:
                continue
            script = resolve(sfile, spid)
            try:
                cls = script.read().m_ClassName if script is not None and script.type.name == "MonoScript" else None
            except Exception:
                cls = None
            classes[pid] = (cls, go, raw)

        def starter_ref(raw, rel):
            _, off = _read_str(raw, 28)
            fid, target = struct.unpack_from("<iq", raw, off + rel)
            return target if fid == 0 and classes.get(target, (None,))[0] == "BBConvoStarter" else None

        set_off = {}  # starter pid -> its best way of being set off

        def offer(target, rank, kind, obj, at):
            if target is not None and (target not in set_off or rank < set_off[target]["rank"]):
                set_off[target] = {"rank": rank, "kind": kind, "object": obj, "at": [round(v, 3) for v in at]}

        for pid, (cls, go, raw) in classes.items():
            try:
                if cls == "Checkpoint":
                    at = trigger_or_position(go)
                    ends = {}
                    for rel, when in CHECKPOINT_STARTERS:
                        target = starter_ref(raw, rel)
                        offer(target, 0, f"campfire ({when})", names.get(go), at)
                        ends[when] = names.get(classes[target][1]) if target is not None else None
                    (x, y, z) = at
                    out.append(("Checkpoint", names.get(go), x, y, z, None, {"at": [round(v, 3) for v in at], **ends}))
                for rel, kind in SETTERS_OFF.get(cls, ()):
                    offer(starter_ref(raw, rel), 1, kind, names.get(go), trigger_or_position(go))
            except Exception:
                pass

        for pid, (cls, go, raw) in classes.items():
            if cls not in ("BBConvoStarter", "KinematicUntilTouchedByPlayerGroupDad"):
                continue
            (x, y, z), _, _ = world(go_transform[go])
            timeline = extra = None
            if cls == "BBConvoStarter":
                # First serialized field after the base: PD, the starter's PlayableDirector.
                _, off = _read_str(raw, 28)
                dfile, dpid = struct.unpack_from("<iq", raw, off)
                if dfile == 0 and dpid in directors:
                    asset = resolve(*directors[dpid])
                    if asset is not None:
                        timeline, _ = _read_str(asset.get_raw_data(), 28)
                if go in triggers:
                    offer(pid, 2, "own trigger", names.get(go), point(go, triggers[go]))
                offer(pid, 3, "position", names.get(go), (x, y, z))
                extra = set_off[pid]
            out.append((cls, names.get(go), x, y, z, timeline, extra))
    return out


def main():
    ap = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    ap.add_argument("--game", required=True, help="Baby Steps folder (the one with BabySteps.exe)")
    here = os.path.dirname(os.path.abspath(__file__))
    ap.add_argument("--out", default=os.path.join(here, "game_links.json"))
    args = ap.parse_args()

    pois = {p["id"]: p for p in json.load(open(os.path.join(here, "mapgenie_pois.json")))}
    anchors = {a["poi"]: a for a in json.load(open(os.path.join(here, "anchors.json")))["anchors"]}

    data = os.path.join(args.game, "BabySteps_Data")
    bundles = sorted(glob.glob(os.path.join(data, "StreamingAssets", "aa", "StandaloneWindows64", "*.bundle")))
    with mp.Pool() as pool:
        scenes = [p for p in pool.imap_unordered(_scene_bundle, bundles, chunksize=32) if p]
        rows = [r for rs in pool.imap_unordered(scene_scripts, scenes) for r in rs]
    starters, set_off = {}, {}
    for cls, name, x, y, z, timeline, extra in rows:
        if cls == "BBConvoStarter":
            if timeline:
                starters.setdefault(name, set()).add(timeline)
            if name not in set_off or extra["rank"] < set_off[name]["rank"]:
                set_off[name] = extra
    piles = [(x, z) for cls, name, x, y, z, _, _ in rows if cls == "KinematicUntilTouchedByPlayerGroupDad"]
    campfires = sorted(({"object": name, **extra} for cls, name, x, y, z, _, extra in rows if cls == "Checkpoint"),
                       key=lambda c: (c["at"][2], c["at"][0]))
    print(f"{len(scenes)} scenes: {len(starters)} cutscene starters, {len(piles)} can piles, {len(campfires)} campfires")

    level = next((lvl for lvl in sorted(glob.glob(os.path.join(data, "level*"))) if not lvl.endswith(".resS")
                  and any(r[0] == "BigManagerPrefab" for r in scene_objects(lvl))), None)
    loaders = {r[0] for r in scene_objects(level) if "/Savables/" in r[4]} if level else set()

    links, problems = {}, []

    def place(poi, objects):
        """Where the game sets the POI's cutscene off: the best of its starters' ways (a campfire,
        a trigger, the starter's own trigger, the starter), the first listed on a tie."""
        ways = [(set_off[o]["rank"], i, set_off[o]) for i, o in enumerate(objects) if o in set_off]
        if not ways:
            return
        way = min(ways, key=lambda w: w[:2])[2]
        link = links.setdefault(poi, {"poi": poi, "title": pois[poi]["title"]})
        link["at"] = [round(v, 2) for v in way["at"]]
        link["atFrom"] = f"{way['kind']} {way['object']}"

    def add(poi, found=None, item=None, gone=None, best=None, how=""):
        link = links.setdefault(poi, {"poi": poi, "title": pois[poi]["title"]})
        if found:
            link["found"] = sorted(set(link.get("found", [])) | set(found))
        if gone:
            link["gone"] = sorted(set(link.get("gone", [])) | set(gone))
        if item:
            link["item"] = item
        if best:
            link["best"] = {"key": best[0], "under": best[1]}
        link["how"] = (link.get("how", "") + "; " + how).strip("; ")

    for poi, objects in STARTERS.items():
        keys = sorted({t for o in objects for t in starters.get(o, ())})
        missing = [o for o in objects if o not in starters]
        if missing:
            problems.append(f"{poi} {pois[poi]['title']}: no cutscene starter named {missing}")
        if keys:
            add(poi, keys, how="cutscene " + "/".join(objects))
        place(poi, objects)
    for poi, keys in LITERAL.items():
        add(poi, keys, how="recorded by the game")
        if poi in LITERAL_STARTERS:
            place(poi, LITERAL_STARTERS[poi])
    for poi, dream in HAT_DREAMS.items():
        add(poi, [dream], how="hat dream " + dream)
    for poi, p in pois.items():
        if p["catName"] == "Fruit" and poi not in STARTERS:
            # A fruit is its anchor's starter, or (for a duplicate marker) the same fruit's.
            obj = anchors[poi]["object"] if poi in anchors else next(
                (a["object"] for a in anchors.values() if a["title"] == p["title"] and a["category"] == "Fruit"), None)
            if obj in starters:
                add(poi, sorted(starters[obj]), how="fruit " + obj)
            else:
                problems.append(f"{poi} {p['title']}: no fruit cutscene found")
        elif p["catName"] == "Stack of Cans":
            a = anchors.get(poi)
            if not a:
                problems.append(f"{poi} {p['title']}: not anchored")
                continue
            x, z = a["x"], a["z"]

            def gap(q):
                dx = (q[0] - x) % LOOP_X
                return min(dx, LOOP_X - dx) ** 2 + (q[1] - z) ** 2
            px, pz = min(piles, key=gap)
            if gap((px, pz)) > 1.0:
                problems.append(f"{poi} {p['title']}: nearest can pile is {gap((px, pz)) ** 0.5:.1f} m away")
                continue
            # The game: Format("CANS{0}.{1}", RoundToInt(Utils.Mod(x, 512)), RoundToInt(Utils.Mod(z, 3328)))
            add(poi, [f"CANS{round(px % LOOP_X)}.{round(pz % LOOP_Z)}"], how="can pile")
    for poi, loader in ITEMS.items():
        if loader not in loaders:
            problems.append(f"{poi} {pois[poi]['title']}: no loader {loader} in the main scene")
            continue
        add(poi, item=loader, how="moves")
    for poi, records in GONE.items():
        keys = set()
        for r in records:
            if r.startswith("="):
                keys.add(r[1:])
            elif r in starters:
                keys |= starters[r]
            else:
                problems.append(f"{poi} {pois[poi]['title']}: no cutscene starter named {r}")
        add(poi, gone=sorted(keys), how="gone after " + "/".join(r.lstrip("=") for r in records))
    for poi, (key, under) in BEST_TIMES.items():
        add(poi, best=(key, under), how=f"best time {key} under {under} s")

    for line in problems:
        print("  ! " + line)
    unplaced = [f"{poi} {pois[poi]['title']}" for poi in STARTERS if "at" not in links.get(poi, {})]
    if unplaced:
        problems.append("no place found for: " + ", ".join(unplaced))
    out = {
        "note": "Links from Map Genie POIs to the game's own records (keys in the save's dialogsPlayed): 'found' "
                "means the POI is done, 'gone' that the item has left the game; 'best' is a best time the save "
                "keeps (miscNums 'key') that makes the POI done once it's under 'under' seconds; 'item' is the "
                "loader of a movable object; 'at' is where the game sets the POI's cutscene off (world x, y, z) and 'atFrom' what does "
                "it. 'campfires' are the game's campfires (Checkpoint): where, and the starters of the evening and "
                "morning they play. Generated by find_links.py; merged into pois.json by build_pois.py.",
        "links": [links[k] for k in sorted(links)],
        "campfires": campfires,
    }
    with open(args.out, "w") as f:
        json.dump(out, f, indent=1, ensure_ascii=False)
        f.write("\n")
    cats = {}
    for k, link in links.items():
        if "found" in link or "best" in link:
            cats[pois[k]["catName"]] = cats.get(pois[k]["catName"], 0) + 1
    print(f"wrote {args.out}: {len(links)} POIs linked; found automatically: " + ", ".join(f"{c} {n}" for c, n in sorted(cats.items()))
          + f"; movable {sum('item' in link for link in links.values())}, can leave the game {sum('gone' in link for link in links.values())}"
          + f"; placed where the game sets them off {sum('at' in link for link in links.values())}; {len(campfires)} campfires")
    if problems:
        sys.exit(1)


if __name__ == "__main__":
    main()
