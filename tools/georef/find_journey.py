#!/usr/bin/env python3
"""Read how Baby Steps' ending lays out its Hero's Journey, for the mod's replay of it.

    python tools/georef/find_journey.py --game "/path/to/Baby Steps" [--out package/Mods/CabbySteps/journey.json]

Needs UnityPy (pip install UnityPy) and an installed copy of the game.

The ending (the EndCredits behaviour on Moose_Cabin_Prefab/EndingCutscene/Ending, in a world
chunk near the summit) paints Nate's path in red dots over six painted map sheets (Map0, the
bottom one, to Map5) pinned on a wooden board (Wood), filmed by EndingCamera. This finds that
scene in the Addressables bundles and writes, in the ending's own units (a path point (x, y) is
drawn at (x - 0.5, y) from Ending; x runs 0..1 across the world loop, y up the mountain):

  - each sheet's and the board's centre and size, and the Addressables key (the asset GUID the
    game's catalog lists) of its material, with the texture property holding the painting;
  - the camera: its starting height, its distance from the dots and its field of view.

The mod loads the materials through the game's own Addressables at run time, so none of the
game's art is copied anywhere. Nothing here is needed to build the mod: it only refreshes
journey.json after a game update.
"""
import argparse
import glob
import json
import multiprocessing as mp
import os
import struct
import sys

SHEETS = ["Map0", "Map1", "Map2", "Map3", "Map4", "Map5"]   # bottom to top
BOARD = "Wood"
PAINTING = "_AlbedoMap"        # the sheets' and board's colour texture (the shader's _MainTex is a stand-in)
CINEMACHINE_STREAMING_VERSION = 20170927  # CinemachineVirtualCamera.m_StreamingVersion, just before its lens


def _subfiles(env):
    out = []
    for f in env.files.values():
        out += [f] if hasattr(f, "objects") else [x for x in (getattr(f, "files", {}) or {}).values() if hasattr(x, "objects")]
    return out


def _scan(path):
    """(bundle path, its CAB names, {container key: path id}, has the EndCredits script)."""
    import UnityPy
    try:
        env = UnityPy.load(path)
    except Exception:
        return None
    cabs, container, ending = [], {}, False
    for sf in _subfiles(env):
        cabs.append(sf.name.split(".")[0])
        for pid, o in sf.objects.items():
            t = o.type.name
            try:
                if t == "AssetBundle":
                    for key, info in o.read_typetree().get("m_Container", []):
                        if info["asset"]["m_FileID"] == 0:
                            container[key] = info["asset"]["m_PathID"]
                elif t == "MonoScript" and o.read().m_ClassName == "EndCredits":
                    ending = True
            except Exception:
                pass
    return path, sorted(set(cabs)), container, ending


def _read_str(raw, off):
    n = struct.unpack_from("<i", raw, off)[0]
    return raw[off + 4: off + 4 + n].decode("utf8", "replace"), (off + 4 + n + 3) & ~3


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--game", required=True)
    ap.add_argument("--out", default=os.path.join(here, "..", "..", "package", "Mods", "CabbySteps", "journey.json"))
    args = ap.parse_args()
    import UnityPy

    data = os.path.join(args.game, "BabySteps_Data")
    bundles = sorted(glob.glob(os.path.join(data, "StreamingAssets", "aa", "StandaloneWindows64", "*.bundle")))
    print(f"scanning {len(bundles)} bundles...", file=sys.stderr)
    with mp.Pool() as pool:
        scans = [s for s in pool.imap_unordered(_scan, bundles, chunksize=32) if s]
    by_cab = {cab: (path, container) for path, cabs, container, _ in scans for cab in cabs}
    scene_paths = [path for path, cabs, _, ending in scans if ending]

    # The scene: the bundle with the EndCredits script whose scene file uses it.
    found = None
    for path in scene_paths:
        env = UnityPy.load(path)
        files = _subfiles(env)
        scripts = {(sf.name, pid) for sf in files for pid, o in sf.objects.items()
                   if o.type.name == "MonoScript" and o.read().m_ClassName == "EndCredits"}
        for sf in files:
            if sf.name.endswith(".sharedAssets"):
                continue
            ext = [e.path.split("/")[-1] for e in sf.externals]
            for pid, o in sf.objects.items():
                if o.type.name != "MonoBehaviour":
                    continue
                raw = o.get_raw_data()
                fid, spid = struct.unpack_from("<iq", raw, 16)
                if fid and (ext[fid - 1], spid) in scripts:
                    found = (sf, raw, struct.unpack_from("<iq", raw, 0)[1])
                    break
            if found:
                break
        if found:
            break
    if not found:
        sys.exit("the ending (EndCredits) wasn't found in the bundles")
    scene, ending_raw, ending_go = found
    ext = [e.path.split("/")[-1].split(".")[0] for e in scene.externals]

    names, comps, tr = {}, {}, {}
    for pid, o in scene.objects.items():
        if o.type.name == "GameObject":
            d = o.read()
            names[pid] = d.m_Name
            comps[pid] = [(c.file_id, c.path_id) for c in d.m_Components]
        elif o.type.name == "Transform":
            d = o.read()
            tr[pid] = d
    go_tr = {d.m_GameObject.path_id: pid for pid, d in tr.items()}
    ending_t = tr[go_tr[ending_go]]
    children = {names[tr[c.path_id].m_GameObject.path_id]: tr[c.path_id] for c in ending_t.m_Children if c.path_id in tr}

    def material(child):
        """(Addressables key, material name, painting's texture name) of a child's first material."""
        for fid, cp in comps[child.m_GameObject.path_id]:
            o = scene.objects.get(cp) if fid == 0 else None
            if o is None or o.type.name != "MeshRenderer":
                continue
            m = o.read().m_Materials[0]
            path, container = by_cab[ext[m.file_id - 1]]
            key = next(k for k, v in container.items() if v == m.path_id)
            msf = next(sf for sf in _subfiles(UnityPy.load(path)) if m.path_id in sf.objects)
            mat = msf.objects[m.path_id].read_typetree()
            tex = dict(mat["m_SavedProperties"]["m_TexEnvs"])[PAINTING]["m_Texture"]
            if tex["m_FileID"] == 0:
                tsf = msf
            else:
                tcab = msf.externals[tex["m_FileID"] - 1].path.split("/")[-1].split(".")[0]
                tsf = next(sf for sf in _subfiles(UnityPy.load(by_cab[tcab][0])) if tex["m_PathID"] in sf.objects)
            return key, mat["m_Name"], tsf.objects[tex["m_PathID"]].read().m_Name
        sys.exit(f"{names[child.m_GameObject.path_id]} has no material")

    def quad(name):
        t = children[name]
        p, s = t.m_LocalPosition, t.m_LocalScale
        key, mat_name, tex_name = material(t)
        print(f"  {name}: material {mat_name} ({key}), painting {tex_name}", file=sys.stderr)
        return {"name": name, "material": key, "texture": PAINTING, "painting": tex_name,
                "x": round(0.5 + p.x, 5), "y": round(p.y, 5), "w": round(s.x, 5), "h": round(s.y, 5)}

    sheets = [quad(n) for n in SHEETS]
    for sheet in sheets:
        # Each sheet's drop shadow: a child quad a touch bigger, in the sheet's own units.
        for c in children[sheet["name"]].m_Children:
            t = tr.get(c.path_id)
            if t is not None and names[t.m_GameObject.path_id].endswith("Shadow"):
                sheet["shadow"] = {"x": round(t.m_LocalPosition.x, 5), "y": round(t.m_LocalPosition.y, 5),
                                   "w": round(t.m_LocalScale.x, 5), "h": round(t.m_LocalScale.y, 5)}
    board = quad(BOARD)

    # The camera: EndCredits.camT (after its drawer's one field), and its virtual camera's lens.
    cam_t = tr[struct.unpack_from("<iq", ending_raw, 36)[1]]
    cam_p = cam_t.m_LocalPosition
    fov = None
    for fid, cp in comps[cam_t.m_GameObject.path_id]:
        o = scene.objects.get(cp) if fid == 0 else None
        if o is not None and o.type.name == "MonoBehaviour":
            raw = o.get_raw_data()
            at = raw.find(struct.pack("<i", CINEMACHINE_STREAMING_VERSION))
            if at >= 0:
                # m_Priority, m_StandbyUpdate, m_LookAt, m_Follow, then m_Lens.FieldOfView.
                fov = struct.unpack_from("<f", raw, at + 4 + 4 + 4 + 12 + 12)[0]
    if fov is None or not 5 < fov < 120:
        sys.exit("the ending camera's field of view wasn't found")
    camera = {"y": round(cam_p.y, 5), "distance": round(-cam_p.z - 0.01, 5), "fov": round(fov, 3)}

    out = {
        "about": "CabbySteps: the game's ending Hero's Journey layout, from tools/georef/find_journey.py. "
                 "Units are the ending's: a path point (x, y) sits at (x, y) here, x 0..1 across the world loop, "
                 "y up the mountain. material = the game's Addressables key; texture = its painting's property.",
        "camera": camera, "board": board, "sheets": sheets,
    }
    with open(args.out, "w") as f:
        json.dump(out, f, indent=2)
        f.write("\n")
    print(f"wrote {args.out}: {len(sheets)} sheets, camera {camera}", file=sys.stderr)


if __name__ == "__main__":
    main()
