# CabbySteps

A [MelonLoader](https://github.com/LavaGang/MelonLoader) mod for *Baby Steps*: a
full-screen map (pad, keyboard or mouse) and a minimap on the game's own painted map (the
sheets its ending shows, with Nate put on them exactly as the game does it), a "you are here" marker,
waypoint tracking with guide arrows, completed POIs (ticked off from the game's own
records where it keeps one, by hand otherwise, and kept with each game save), movable
items shown where they really are and brought to Nate on request, saved locations (named as you
like, and a quick save) Nate can be sent back to, a first-person toggle, the "Hero's Journey": Nate's path painted the
way the game's ending paints it, from the game's own record, and two cheats: Grippy feet (planted feet can't slip, and Nate
keeps his balance), with a second level, Extreme grip (a foot put down against anything holds there, so he can walk up
sheer walls), and Hold tight (what he holds and his hat stay with him, whatever falls, the wind or a camp's night would do).

CabbySteps is based on **[Baby Steps MiniMap and QOL Features](https://www.nexusmods.com/babysteps/mods/14)**
by **[Slylos](https://www.nexusmods.com/profile/Slylos?gameId=8244)**: the full map and the minimap,
tracking with guide arrows, the help levels, the first-person view and the Hero's Journey all
started there. That mod shipped without source, so it was decompiled and recreated first (see
[History](#history)), then redesigned so that **Nate's position is accurate without any
calibration**, and a good deal was added: see [Changes from the original](#changes-from-the-original).
The player-facing readme (install steps, controls) is [`package/README.txt`](package/README.txt), and
the mod's Nexus Mods description is [`README_BBCode.txt`](README_BBCode.txt).

## Changes from the original

Compared with Baby Steps MiniMap and QOL Features 1.2 (its DLL says 0.6.0):

**The map**
- **It's the game's own map.** The map and the minimap are the six painted map sheets the game's
  ending shows, loaded from your copy of the game while you play, in place of Map Genie's map image.
  Nothing of the game's ships with the mod, and its map data went from about 32 MB to about 80 KB.
- **No calibration.** Nate is put on the sheets with the game's own projection (the one it uses to
  paint his journey), height and all, so the marker is where he is. The calibration screen, its pins,
  the nudge and the heading trim are gone.
- **Places from the game.** Hats and items sit where the game keeps them, cutscenes at their triggers,
  camps at their campfires, read from the game's own files; the rest come from Map Genie's list. Hats,
  items, chicks and ducklings are shown where they really are, and followed as they move.
- **The mouse on the full map**: hover to see what a point is, click to track it, right-click to tick
  it off, drag to pan (the wheel zooms, as before). The map draws its own pointer, so it works with a
  grabbed cursor (gamescope, say).
- The world's east-west loop is handled everywhere, the minimap and the 3D arrow included.
- The map won't open over the game's own menu, and closes if that menu opens.

**Progress**
- **Completed points, kept with each save slot.** Most tick themselves off from the game's own
  records: fruit, cutscenes, payphone calls, stacks of cans, trophy hand-ins, a hat once Nate has
  dreamt about it, The Greatest by your best time in the gym. Tick the rest by hand. R3 (or H)
  hides what's done, labels count each kind, and a new game starts with none.

**Getting around**
- **Saved locations**, as many as you like, saved and named on the map, plus a **quick save** (F5)
  and **quick load** (F9). Nate comes back facing the same way and standing exactly as he stood, with
  what he holds and wears. A trip waits for the place to load, so he isn't flung on arrival, and a
  gym's time trial keeps running through it.
- **Bring things to Nate**: hold Y on any hat, key or quest item, chick or duckling on the map. This
  takes the place of the follower teleport (L3 + R3, or G).
- L3 switches to first person at once (it no longer waits to see whether R3 follows).

**Hero's Journey**
- It's the game's own record of where Nate has been (each save its own, from the save's start),
  painted dot by dot onto the painted sheets the way the game's ending paints it, with pause,
  rewind, fast-forward and zoom. It replaces the mod's own trail recording (and F8's clearing it).

**Cheats (new)**
- **Grippy feet** (F7): planted feet don't slip, and Nate keeps his balance while a foot is down.
  F7 again for **Extreme grip**: a foot put down against a wall holds there, so he can walk up it.
- **Hold tight** (F8): falls, the wind and camp mornings can't take what he holds or his hat;
  swapping still works.

**Fixes**
- **Memory.** Long sessions ran out of memory and crashed: the game's own garbage collector can
  stall, and the mod's drawing left garbage on the game's heap every frame (hundreds of MB a minute
  in play, gigabytes with the map open). The per-frame allocations are gone, and a watchdog gets the
  game's collector going again if it stalls (see [Memory](#memory-the-games-gc-stalls-and-the-mods-garbage)).
- What the original's calibration got wrong (pins one world loop away, duplicate pins, wrapping the
  map in pixels) went with it; [History](#history) has the list.

**Names**
- The mod is `CabbySteps.dll`, with its data in `Mods/CabbySteps/`. Files kept under the old name
  (`UserData/BabyStepsMinimap_config.json` and `_locations.json`, and `BabyStepsMinimap_completed<slot>.json`
  beside the game's saves) are given the new name the first time the mod wants them. An old
  `Mods/BabyStepsMinimap.dll` has to go: it would load alongside, with a second map on the same
  buttons (the mod warns, and `make install` takes it out).

## Credits

- **[Slylos](https://www.nexusmods.com/profile/Slylos?gameId=8244)**, for the original mod,
  [Baby Steps MiniMap and QOL Features](https://www.nexusmods.com/babysteps/mods/14), which CabbySteps
  grew from.
- **Map Genie**, for the list of places (all rights theirs). Their map image isn't used any more.
- **Baby Steps** itself, for the painted map sheets the map is made of (loaded from your copy of the
  game, not shipped).
- **[MelonLoader](https://github.com/LavaGang/MelonLoader)** (LavaGang), the mod loader.

## How positioning works

**The map is the game's own**: the six painted map sheets its ending pins up a wooden board
(`Map0` at the bottom to `Map5` at the summit), the ones the ending paints Nate's journey on.
They're loaded from your copy of the game while it runs (see [The sheets](#the-sheets)), so no
map art comes with the mod. Older versions shipped Map Genie's map image instead, with a
projection fitted to it; Map Genie's map turns out to be these very sheets (below).

**So is the projection.** When the game notes where Nate goes, for the ending to paint his
path (`SaveGod.SaveForever`, read from its machine code; see [Hero's Journey](#heros-journey)),
it works out where he is on those sheets:

```
u = (x mod 512) / 512               across the sheets: the world loops every 512 m
v = (sin 30° · y + cos 30° · z) / 512  up them
```

in the ending's units (the sheets are 1.17188 of them square, `journey.json`). The sheets
are the world seen from the south, looking down at 60°, so **height moves a place up the
map** as well as distance north: 10 m higher is drawn 5 m of map further up, 10 m north
8.66 m. The mod puts Nate, every POI with a place in the game, saved locations and movable
items on the sheets with exactly that (`src/Map/MapProjection.cs`). A map pixel is a texel
of the sheets (2048 across each: 3.4 a metre across, and 3.4 a metre of the map up it, so
about 3 a metre north on level ground), with (0, 0) at the top sheet's top-left corner. There's nothing fitted and nothing to calibrate.

Before this, the mod fitted a smoothing thin-plate spline from world **(x, z)** to Map
Genie's pixels, with 128 game objects as anchors. That can't know about height: it bent to
follow the mountain's average slope, but a ledge, a cliff top or a cave put things metres off
(a median 8.6 Map Genie pixels, about 6–7 m, for an anchor the fit hadn't seen; 40 px at
worst). The game's formula has no such error, and the data agree:

- **Map Genie's map is the sheets.** Stacked as the ending lays them out and scaled to 3/8
  (655.36 Map Genie pixels a unit across, 655.31 up), the sheets line up with every 192 px
  tile of Map Genie's map to 0.02 px (`tools/georef/register_mapgenie.py` →
  `tools/georef/mapgenie.json`).
- **The game's projection puts the game objects on Map Genie's markers**: the 137 objects
  behind them land a median **3.6 m** of map from the hand-placed marker, 90% within 12.6 m,
  with no fitting at all. What's left is how the markers were placed by hand (and a few big
  objects: the Always Tired Sand Dunes are an area; and three hats Map Genie marked well
  off, below). Against the spline, a POI is now drawn
  a median 3.3 m of map from where it was; the biggest moves are where height fooled the
  spline: the Sand Dunes 73 m, the Lantern 39 m, Bottom of the Staircase 35 m, the Rambutan
  30 m, Top of the Cliff 27 m.

**POIs.** `pois.json` (`tools/georef/build_pois.py`) gives every POI where it's drawn on the
sheets (`u`, `v`) and where it is in the world (`wx`, `wy`, `wz`), with Map Genie's marker
kept as `mgPx`/`mgPy`:

1. **Where the game has a place for it, it's there**, drawn by the game's projection: where
   Nate's arrow is when he's there. That's 173 of the 214: the game object behind it
   (`find_anchors.py`: hats, key items and trophies under
   `BigManagerPrefab/GlobalObjectParent/Savables`, and the fruit, can stacks, campfires,
   payphones and landmarks in the 104 streamed world chunks), or better, where the game sets
   it off (below).

   A hat or item is its loader, where the game starts it, however far Map Genie's marker is
   (there's one loader of each, so it can't be the wrong object). Three markers are well
   off: the **Paper Hat** starts in the river, on the rapids at x 481 (it's a paper boat: the
   prefab has a buoyant `WaterObject`, kinematic until knocked), and the marker is 60 m
   along the river at x 29, across the loop's seam; the **Donkey Hat** is in Colin's room,
   the marker 55 m off by a cave; the **Trophy Hat** rides on a tortoise
   (`LoaderFollowTurtle` keeps its loader on it) that walks round the whole world
   (`MoveInDirectionAlongGround`: 11 waypoints at z 800–825, 1.45 m/s), and the marker is
   one spot on its way, 206 m from where it starts. Once an item leaves its start, its
   POI follows it (see "Ticked off by the game, and movable items").
2. **The other 41 stay on Map Genie's marker**, which is on the same painting, converted with
   `mapgenie.json`. They're mostly landmarks (Points of Interest, graffiti), a few
   achievements and Easter eggs, and the two chicks and the duckling (left out on purpose:
   they wander). Their world x comes from the marker, and z from the marker and a height guessed
   from the nearest POIs that have one (no `wy`: the mod treats their height as unknown).

Distances, the compass, the overhead marker and the 3D arrow all work in world space.

### Cutscenes and camps where the game has them

Map Genie's markers were placed by hand, and some are well off: the fortress camp,
its wishing party and its morning are drawn at the fortress gate, 80–90 m of map from the
campfire where they happen. So the map takes these from the game's own scenes
(`find_links.py` writes them into `game_links.json` as `at`, and `atFrom` says what):

- A cutscene (`BBConvoStarter`) is placed where the game sets it off, best first:
  1. the campfire (`Checkpoint`) whose `eveningStarter` or `morningStarter` it is.
     The "Onward and Upward" mornings and the evenings play where Nate sleeps.
  2. a trigger script pointing at it (`BBConvoTrigger`, `BBConvoWaitingTrigger`,
     `BBConvoHoldingItemTrigger` for trophy hand-ins, `BBConvoFallingTrigger`,
     `StepDetector`, `SoccerGoalTrigger`): the centre of its trigger collider;
  3. its own trigger collider;
  4. its own position. That isn't always where it happens: the fortress's cutscenes
     are all at one group pivot, the campfire.

  That covers all 57 POIs linked to cutscenes: cutscenes, payphones, trophy hand-ins
  and the rambutan Mike eats. The Intro has no starter in the world chunks, so it stays
  where Map Genie put it.
- A camp is placed at the game's nearest campfire (8 of each, all within 80 m).

`build_pois.py` puts each such POI there, `at` naming what placed it, and prints the ones
more than 10 m of map from Map Genie's marker: the fortress camp, its wishing party and
morning (80–88 m), Inspector Gadget's Lantern (34 m, the trigger `MooseTrig`), the Secret
Ending (24 m, `MountaintopTrigger`), Meeting Jim, the Screaming cutscene, Jim's Lantern,
Between a Rock and a Hard Place (11–16 m).

Markers that end up on the same spot are fanned out about 5 m of map round it, so the
crosshair can pick each: what there is to do keeps the spot (of those, one the game has a
place for), items and landmarks step aside. That's a camp with its morning (and at the
fortress its evening), a chest and the cutscene of opening it, a fire tower and the trophy
handed in there. World positions stay exact.

Why not find them while playing? The world streams in as 104 chunks, and the running game
only holds the ones near Nate. The tools read every chunk from the game's files: the same
data the game loads. After a game update, `make links pois` (below) refreshes it.

**The world loops east-west every 512 m** (four 128 m chunk columns), and the game
also shifts everything by whole loops to keep coordinates small. Nate's raw x can
therefore be in any copy of the world. `src/World/WorldLoop.cs` folds x into
[0, 512) and measures distances the short way round, and the projection wraps x the same
way the game does. The sheets are a little wider than the loop: their torn edges show 44 m
of the other side at each end. Nate is always drawn in the main drawing, and so is every POI
the game has a place for, even one Map Genie marked on a margin copy (the Grapple Point and
Chest opening cutscenes, the Soccer Field, Red Light Green Light and the Boulder). Cassie's
Trophy, an achievement parked in the blank sky above the summit with no game object behind
it, stays where Map Genie drew it, in the margin. Map Genie also put a second Pear in the
margin, but the game has only one, so `build_pois.py` leaves that marker out (`DROP`). The
mod assumes the world only loops east-west, which matches every position seen so far.

### The sheets

`journey.json` (`find_journey.py`, from the ending scene) says where the ending lays its six
sheets out and the Addressables key of each one's material. Once the game is under way (Nate
is found, or the map is opened), `MapArt` asks the game's own Addressables for those materials
(`Addressables.LoadAssetAsync<Material>`; the painting is the material's `_AlbedoMap`) and keeps
them for the session: six 2048 × 2048 DXT5 textures with their mipmaps, about 34 MB of video
memory (Map Genie's 4096 × 11264 image was loaded uncompressed, well over 100 MB, and kept twice:
on the GPU and a CPU copy).
The map and the minimap draw the sheets the view reaches (`MapDraw`), each at whole map pixels
so they meet without a seam, and the Hero's Journey uses the same ones. A sheet whose painting
hasn't arrived, or can't (after a game update, say), is plain paper: Nate and the points are
still in the right places. The log says `Map: 6 of 6 painted sheets from the game.`

## Completed POIs and the game's saves

Marks belong to the game's save, not to the mod. Each save slot has its own, a new
game starts with none, and they are only written to disk when the game writes its
save, so quitting and continuing gives back exactly the marks of the last save.

How the game saves (from its code, `Il2Cpp.SaveGod`): the current save is the
static `SaveGod.theSave` (`SaveFile`), the slot is `SaveGod.me.slot`, and the save
folder is `PlatformService.GetRootSaveDirectory()`, i.e.
`%USERPROFILE%/AppData/LocalLow/DefaultCompany/Babysteps/<steam id>/save/`. While
you play, the game rewrites `save<slot>.sav` there every few seconds (backups
rotate alongside). It loads the slot's save at the title screen, and again after
you switch or delete a slot (both restart the scene). Deleting renames the save to
`saveDELETED<slot>.sav`. A fresh `SaveFile` has `timePlayed` 0, and the value
only grows within a playthrough.

How the mod follows it (`src/World/SaveSync.cs`, polled four times a second):
- **Load**: a new `theSave` object means the game loaded a save or started a new
  game, so the marks are reloaded for that save. The game assigns `theSave` only when
  loading; saving updates it in place. It sets the slot number before the new save
  has loaded, so a slot change on its own is ignored.
- **Save**: when `save<slot>.sav`'s timestamp changes, the marks are written to
  `CabbySteps_completed<slot>.json` beside it. That save's own `timePlayed` is
  used, and the check runs before the load check, so a write just before a reload
  still counts.

`src/Map/PoiProgress.cs` keeps, per slot, snapshots of the marked POI ids, each
tagged with the save's `timePlayed`. A new snapshot is added only when the marks
changed since the last one. Loading a save at play time *t* gives the last snapshot
at or before *t*, and drops anything after it:
- continuing the latest save gives its marks;
- if the game falls back to an older backup, you get that backup's marks;
- marks made after the last game save are gone, as the game's own progress is.

A new game (`timePlayed` 0) starts empty. At the game's first save in it, the old
file is renamed to `CabbySteps_completed<slot>.deleted.json`, as the game
keeps `saveDELETED<slot>.sav`.

### Ticked off by the game, and movable items

Much of what Map Genie lists, the game records itself, so `src/World/GameLinks.cs`
ticks it off with no input (twice a second, from the save in memory). These ticks
aren't written to the marks file. They come from the game's save, so they always
match whichever save is loaded. Each POI's records are keys in the save's
`dialogsPlayed` set, listed in `pois.json` as `found` (one exception, a best time, follows
the table):

| Record | POIs |
| --- | --- |
| a cutscene timeline's name (a `BBConvoStarter` adds `PD.playableAsset.name` when its cutscene ends; the mod ticks it off as soon as it starts, see below) | fruit, cutscenes, payphone calls, trophy achievements (the hand-in cutscene) |
| `CANS<x>.<z>` (`KinematicUntilTouchedByPlayerGroupDad`: the pile's position, x modulo 512, z modulo 3328, rounded) | stacks of cans, once knocked over |
| a hat's dream: the `HatDream` name in the hat prefab's `Hat.dream` (`stetson`, `goPro`, `boat`…), played when Nate sleeps at a campfire wearing it | 13 hats; the Pacifier and the Winter Hat have none |

**The Greatest** ("Beat the designer's time in the gym") leaves nothing in `dialogsPlayed`.
At the finish of the Underwater Gym's course with every checkpoint passed,
`JesseGym.ProcessCheckpoint` keeps the run's time in the save as `miscNums["GymPB"]` if it's a
best, then unlocks the achievement if that best is under `JesseGym.developerTime`, 78.24 s (set
by the class's static constructor, so read from the game's code: `find_links.py` `BEST_TIMES`).
`pois.json` has it as `"best": {"key": "GymPB", "under": 78.24}`, and `GameLinks` reads the
save's best each poll (a new best replaces the entry, so the dictionary's size doesn't change);
the log says what it read ("The Greatest: the save's best time GymPB is 52.83 s, under the
78.24 s it takes"). The Invisible Gym is a `JesseGym` too and writes its runs under the same key,
and its first finish after a load always counts as a best (it never loads the saved one), so a
slower run there can replace the Underwater Gym's best. So this POI, unlike the others, is also
marked (`PoiProgress.Keep`, only for the save the record was read from) and the mark is kept with
the save like a hand mark: it stays ticked off, and can then be unticked by hand. An Invisible Gym
run under 78.24 s ticks it off too: the game itself then awards the achievement at the next
finish of the Underwater Gym, as that gym loads `GymPB` as its best.

The game writes a cutscene's record only when its timeline stops (`BBConvoStarter.OnEnd`,
the director's `stopped` event). For the conversations Nate walks on through, that's minutes
after he can move again: the "Onward and Upward" mornings, for one. The mod's map pauses the
game, timelines included, so it can be longer still. In one session the start of chapter 3
(`EthanForestMorning`) was recorded some six minutes after Nate was walking about the camp,
three of them with the map open. So
`GameLinks` also ticks a cutscene off as soon as it **starts**. Every cutscene starts through
`BBConvoStarter.PlayCutscene`, which calls `Menu.OnCutscene` with its director, kept in
`Menu.playingCutscene`. When that changes, the director's timeline name (the key the game will
record) joins a "seen" set that counts like a record until the next save load. Only a change of
director counts, so a director left over from before a load doesn't tick anything. Items' `gone`
records still wait for the game, so an item doesn't leave the map mid-cutscene. A cutscene
quit before its end isn't in the save, so it's open again when that save is loaded, just as the
game sees it.

Points of Interest and Graffiti are landmarks for reference (`Poi.ReferenceOnly`). They
can't be ticked off, nothing ticks them off, and they don't count towards progress.

Hats, key items, quest items and the chicks and ducklings can't be ticked off by hand
(`Poi.HandMarkable`). Finding one and carrying it about doesn't make it done; their
POIs show where they are instead. Only a hat's dream ticks one off, and items the game
can't tick off don't count (`Poi.Completable`). Old hand marks on them are dropped at
the next game save. They're loaded by `GlobalObjectLoader`s under
`BigManagerPrefab/GlobalObjectParent/Savables` (`pois.json` `item` names the loader).
Once an object is more than 4 m from where it started (`defPos`) and from its spot on the
map, its POI follows it: the loaded object while it's near, the loader (which keeps its
last spot) while not. An object back at its spot on the map, or one the game has loaded
but switched off, isn't out in the world, and its POI stays at its own spot. A quest item
that leaves the game comes off the map and out of the counts (`gone`: its hand-in
cutscene, or `VaseBroken` for the vase).

When the game moves an item with Nate nowhere near (over 40 m between two looks half a
second apart, and over 8 m from him at both ends, so carrying it on a trip or bringing it
to him doesn't count), a toast and the log say so: "Ice Cream: the game put it back at
Mitchell's cart". Several at once is the game putting its whole world back to load another
save (`Menu.RestartScene` returns every loader to its start), so is a scene restart; those
only get a log line. Two seconds after a save loads, the log lists the items away from
where they start: where they are, and whether the game has them loaded.

The ice cream is one of these items: the `IceTrophy_Loader`'s object, an `IceMelter`, under
save key `IceTrophy`. Mitchell's `IceTrophyHelper` at the cart keeps it switched off there
until one of his twelve `MitchellIceCream` cutscenes hands it to Nate (`ForceGiveGrabable`;
`miscInts` `IceTrophiesGotten` counts them). It melts as he carries it (`miscNums`
`MeltPercent`, 600 s to melt away), and switches itself off once melted
(`MeltedAwayToNothing`). The helper only runs while the cart's part of the world is loaded.
Once the ice cream has melted, or is left behind unloaded, or is out while Nate is over
20 m from the cart, it readies the next cutscene and moves the ice cream's loader to the
cart (its own spot); one left behind unloaded goes there in the save as well. In play
that loader is at the cart already, so a carried ice cream stays with Nate. But an object
loaded from a save hangs from its loader (`GlobalObjectLoader.Load` instantiates it as
the loader's child, and only `DropHandItem` lets go), and the loader starts at the item's
saved spot. So after a reload, an ice cream left out goes back to the cart with its
loader, as soon as the cart's part of the world is loaded and Nate is over 20 m from the
cart. The save keeps the old spot (`GlobalObjectLoader.Save` only saves an item that is
away from its loader). The ice cream is then the next one Mitchell hands over. Its POI is
at the cart until he has one, and follows it while he does. Handed in (Coolest Hand), the
game records the hand-in (`TrophyIceDropOff`) and `IceTrophy_used`, and the ice cream comes
off the map.

`tools/georef/find_links.py` builds these links from the game files into
`tools/georef/game_links.json`, which `build_pois.py` merges into `pois.json`. It
reads each cutscene starter's timeline name and each can pile's position from the
scenes. Its tables only say which starter (by object name) or loader belongs to which
POI, each hat's dream (read from the hat prefabs), and what makes an item gone. They
were matched by place and subject, and checked against real saves.

## Saved locations

Where Nate is can be saved, and he can be sent back there. There are two kinds: locations saved
on the map, as many as you like, numbered until you name them and kept until you forget them;
and one quick save, which F5 saves over while you play, so practising a tricky bit doesn't pile
up saves.

- **Save a location**: L3 on the map (F5 there on the keyboard) saves where Nate is as
  "Location 1", "Location 2"... (the lowest free number), with which way he and the camera face,
  where his planted feet are and how his body stands. Each is a red numbered pin on the map and
  the minimap.
- **Quick save**: F5 while playing saves the same into the one quick save, a blue "Q" pin. Each
  F5 saves over it: the pin moves (tracking it follows) and nothing piles up. Locations saved on
  the map are never touched by it. There's no pad button for it outside the map.
- **Quick load**: F9, at any time (playing or on the map), sends Nate back to the quick save.
  It's checked like any trip (not during a cutscene...), works while Nate is down after a fall,
  and leaves "Where you were" too. With no quick save yet, it says so rather than going to a
  location saved on the map.
- **Pick**: LT/RT (Q/E) put the crosshair on the previous or next pin: the quick save, the
  numbered ones in order, the named ones A to Z, then "Where you were". The crosshair or the
  pointer on a pin picks it too; pins are picked before the POIs under them. A (Enter, a click)
  tracks it like any POI, guide arrows included.
- **Go there**: hold Y (Space, the right mouse button) on it for 0.8 s. The map closes and the
  game moves Nate. A ring fills round the pin while the button is held; let go early and
  nothing happens. Tracking the location stops once Nate goes there.
- **Forget**: hold X (Delete) on it, the quick save included. A tap of X still brings the
  crosshair back to Nate.
- **Rename**: hold A on a red pin for 0.8 s (a gold ring fills), or press F2 with the
  crosshair or the pointer on it. A box opens over the map. A keyboard types straight in
  (Backspace deletes, Enter keeps the name, Esc cancels). A pad picks characters off the box's
  on-screen keyboard with the D-pad or the left stick and A, with X to delete, Y for a space, LT
  for Shift (one character; it comes on by itself at the start of a name), RT or the Done key to
  keep the name, and B to cancel. The mouse clicks the keys, and a right-click cancels. Names are
  up to 32 characters. A named location has no number any more: its pin shows the first letter
  of each of the name's first two words, in capitals ("UG" for "Underwater Gym", "T" for
  "Tower"; smaller when they're wide), and its number goes to the next location saved. Named
  ones come after the numbered ones, A to Z. One left empty is numbered again, with the lowest
  free number ("Location 3"). A tap of A still tracks the location, once A comes back up. The
  quick save and "Where you were" can't be renamed, since they're saved over. While the box is
  open the keyboard only types: M, F9 and the map's keys wait until it's shut. The pad's View
  still closes the map, leaving the name as it was.
- Each trip leaves a grey **"Where you were"** pin where Nate set off from, so a trip can be
  undone. The next trip moves it.

### How Nate is moved

The mod doesn't move Nate's ragdoll itself. It calls the game's own teleport,
`Menu.Teleport(Vector3 cp, bool dead)`, which the game uses to put Nate where a save left
him (`Menu.LoadSave`). The developers' own cheats call it too ("Save Nate Pos" and "Load Nate
Pos" in `DevCheatsGoHere`). Their save half records `PlayerMovement.me.hipPivot.position`,
and so does the mod. Read from the game's
machine code (call graph from MelonLoader's xref cache, field offsets from Cpp2IL's dummy
assemblies):

- `Teleport` does nothing during a non-interactive cutscene, and queues the position if a
  teleport is already running (`teleporting`, `qedTeleport`). Otherwise it starts the
  coroutine `TeleportCo`.
- `TeleportCo` points the world streaming at the spot (`Menu.loadingTargetTransform`, high
  background-loading priority) and waits, a frame at a time, until the spot has loaded. It
  then switches Nate's puppet off, takes his hat off and empties his hands.
- It casts a ray down from 1 m above the spot, up to 100 m. If that hits nothing it starts 1 m
  higher each time, up to 500 times. His hips go 1 m above what it hits. A spot saved in
  mid-air therefore puts him on the ground below it. With `dead` set (the only thing that flag
  changes) it looks down once and keeps the spot's own height, unless that's less than 1 m above
  the ground.
- It picks his footing round there (`PlayerMovement.FindBestFootSpots`, which leaves its pick in
  `sampledFeetPos`), plants his feet on it (`FootData.targetPos`), stands him up
  (`OnStandUp`), nudges each foot out of anything it would be inside (`FootCollisionSimple`)
  and moves his hips 1 m above the middle of his feet (if that's within 2 m of where they
  were going).
- It puts the hat back on, gives back what he held, zeroes his velocity, switches the camera
  back on and clears `teleporting`. On the way, it moves the ragdoll's root to the animated
  character's (every ragdoll part with it) and only then wears the hat (`WearHat`) and takes the
  items back (`GrabGrabable`). `FinishCutscening` then puts the ragdoll back on the animation
  (`FixMusclePositionsAndRotations`), stands him up, sits the hat on his head and puts each item
  back in its hand (`PutGrabableInHand`). Last comes a second `OnStandUp`.

The mod adds:

- **The world loop.** The spot's x is moved to the copy of the 512 m loop nearest Nate
  (`WorldLoop.NearestCopy`). The game's `ObjectLooper.LoopThisPosAround` does the same, and
  only in x.
- **Timing.** The trip starts the frame after the map has closed, since the game has to be
  running. Nothing starts while the game's menu is open or it's loading, while a teleport is
  running, during a cutscene, while Nate sleeps at a campfire or dreams, or during the intro
  (`Menu.paused`, `loading`, `restartingScene`, `teleporting`, `inCutscene`,
  `inNonInteractiveCutscene`, `inDream`, `waitingToSleep`, `playingIntro`;
  `PlayerMovement.inCutscene`). Nate being down after a fall (`PlayerMovement.dead`) doesn't
  stop it: the teleport switches his puppet off and stands him up wherever it takes him
  (`OnStandUp` clears `dead`), which is also what the game's own menu does whatever state he's
  in. The map won't open while Nate is on his way.
- **First person.** It's set aside for the trip and comes back after
  (`GameBridge.SuspendFirstPerson`). The teleport takes off whatever hat Nate wears and puts
  it back, and the mod's first person hands the game a virtual GoPro that isn't a real hat.

**Facing.** Nate arrives facing the way he did when the location was saved, with the camera
where it was, as when the game loads its own save. That save keeps his facing
(`SaveFile.continueRot`, from `PlayerMovement.hipAng`) and the camera's shoulder
(`cameraRight`, from `camRight`). On loading, `Menu.LoadSave` sets the camera's yaw (`camAng`)
to that facing, turns Nate's animator (`anim.transform.rotation`) to it and puts back
`camRight`/`camRightAmount`, then teleports him. A saved location also keeps the camera's own
yaw, so the view comes back as it was rather than straight behind him. The mod does the same,
with the timing the teleport needs:

- `TeleportCo` places his feet by the animator's turn (`FindBestFootSpots`), and only after
  switching his ragdoll off (`PuppetMaster.SwitchToDisabledMode`, which just sets `mode` to
  Disabled), with a frame between the two. Turning the animator any earlier would wrench round
  a ragdoll that still follows it. So `Recall` turns Nate (`hipAng` and the animator) in the
  frame it first sees `mode` Disabled, the frame before he's placed.
- `TeleportCo` sets the camera again as it stands him up (`FinishCutscening`), so the camera's
  yaw and shoulder are put back on arrival.

**Standing as he stood.** The teleport stands Nate up afresh, all in one frame: its own pick
of footing (`FindBestFootSpots` into `sampledFeetPos`), both feet turned one way (`getUpEndRot`,
worked out by `SampleGetUpStuff` from how he was lying), each foot nudged out of anything it's
in (`FootCollisionSimple`), his hips' pivot 1 m above the middle of his feet and the character's
root 1 m below that, then `OnStandUp` twice (which takes the controller's targets from the hip
pivot). On a precise footing (a pillar, a narrow ledge, a gap between rocks) that leaves him
sitting on it, or off it. So a saved location keeps, as offsets from the saved position (his
hip pivot) and turns:

- each planted foot's target (`FootData.targetTrans`: place and turn; feet in the air aren't
  kept);
- his whole body while he stands: the animated character's root (`anim`'s transform), the
  ragdoll's root (`PuppetMaster`'s transform) and every ragdoll part (each muscle's rigidbody).
  Nothing is kept while he's down.

Going back, three Harmony hooks (`StanceRestore`), active only while a trip is under way:

- after `FindBestFootSpots`: the saved spots go into `sampledFeetPos`, which the teleport plants
  his feet on and puts his hips over;
- before each `OnStandUp`: the hip pivot, both roots and each foot's target are set to the
  saved ones, so the stand-up (and the controller's targets) start from them;
- after each `OnStandUp`: every ragdoll part is put where and as it was, still (velocities
  zeroed, `Physics.SyncTransforms`), as the game's own "redo last get-up" (`RedoLastGetUp`) puts
  back a body. The teleport's second `OnStandUp` is its last word on him, so no physics step
  sees its own pose.

The trip also sets the teleport's `dead` flag (one look down, keeping the saved height unless
it's under 1 m above the ground), rather than putting his hips 1 m above the first ground under
the spot. The spots and body go to the same copy of the world loop as the position. A location
with no planted foot (or saved before these were kept) gets the game's own stand-up, dropped to
the ground. On arrival the log says what was put back, then how he stands at 0, 0.5, 1.5 and 3 s
(hips, pelvis and feet against the saved ones, and whether he's down). If the hooks can't be made
(a game update), trips still work with the game's own stand-up.

**What he holds and wears.** The game holds an item in a hand with a locked joint from the hand
(`PlayerMovement.handJoints[i]`, made in `PutGrabableInHand` right after `Grabable.PlaceInHand`
sets it in the grip), and a hat with one from the head (`headJoint`). It also keeps a copy of
the hat's collider on his head: the HatColliders object, which `WearHat` poses where the hat's
own collider is. The teleport sets these up while his ragdoll parts are on their way (see above),
and this mod's stance restore moves the parts again. Locked to a hand or head that has since
moved, a held thing gets yanked after it. A held ice cream came down 48 m from Nate, and he was
thrown down on arrival (log 2026-11-10 01:01:30). That trip was the only one to Location 1 to go
wrong, and the only one with something in his hand. Hats went the same way: several trips that
left him down were followed by bringing his hat back from the map.
So after every `OnStandUp` while a trip is under way, saved stance or not, once the game is out
of its cutscene state (`PlayerMovement.inCutscene`, which `FinishCutscening` clears before its
own stand-up), the hook after it also does this (`StanceRestore.PutCarriedBack`, after the body):

- it lets go of each held item and the hat (each joint's `connectedBody` = null);
- it sets each where the game would: in the grip (`PlaceInHand` on `handRbs[i]`), the hat on
  his head (`PlaceCurrentHatOnHead`, with its body not kinematic, as `FinishCutscening` does), and
  the HatColliders copy on the hat's collider (as `WearHat` does);
- it stills them (velocities and `Savable.lastVelocity` zeroed) and syncs the physics scene;
- it holds them again the game's way: `PutGrabableInHand`, and the head joint connected to the
  hat's body with every motion locked. Each joint is made afresh where things now are.

The teleport's last `OnStandUp` is its last word, so that's where they stay. `handItems[0]` is
the right hand (`HeldState.rightHand`). The log says what he set off with ("with IceTrophy in his
right hand") and what was put back on arrival. The checks after the trip also give how far each
held thing is from its hand or head ("IceTrophy 0.12 m from his right hand"). One metres away
has been thrown or dropped.

**Nothing left behind.** The teleport takes along only what's in his hands and on his head at the
moment it switches him off (`TeleportCo` notes `handItems` and `currentHat`, then lets go of them,
and once he's there gives them back with `GrabGrabable` and `WearHat`). While Nate is down, though,
the game has him let go whenever he's jolted: `PlayerMovement.DeadUpdate`, every physics step he's
down, drops a held item when his head's speed changes by more than 4 m/s from the step before (or
the hand's by more than its own limit) and knocks his hat off by a measure of the hat's own, and
`BodyPartHit` drops an item when a part of him hits something hard while he's down. A trip holds
him still until the spot is ready (below), stopping every part of him each physics step, and to
the game that's one jolt after another. On 2026-11-10 a Chest Key was left behind twice that way
(14:05 and 14:06, 935 m trips taken standing): while each trip waited for the far spot to load,
the ground under him unloaded, he fell for 1.13 s and went down, he let go of the key there, and
the teleport had nothing in his hands to take. The key fell through the unloaded ground (seen
226 m below the spot afterwards). So (`World/CarryGuard.cs`):

- two more Harmony hooks, before `DropHandItem` and before `KnockOffHat`, keep the game from having
  him let go while a trip holds him and he's down with his body on (the teleport lets go itself
  with his body switched off, so that isn't stopped; nor is letting go of something that has left
  the world, like an ice cream that melts away, which is switched off before it's dropped). A
  thing kept in his hand that leaves the world afterwards (a vase that breaks) is let go of then;
- whatever he set off with that isn't on him as the teleport stands him up there, but is still
  about, is given back to him the game's own way, before the rest is put in place: in the hand it
  was in (`GrabGrabable`), the hat on his head (`WearHat`), and once more on arriving. The game
  doesn't unload items while it teleports (`ObjectLooper.LoopList` leaves them while
  `Menu.teleporting`), so anything dropped on the way is still there to give back. Which hands get
  what is checked offline (`World/CarryPlan.cs`).

The log says, when he's switched off, how often the game would have had him let go ("kept what he
holds in his hands (the game would have dropped it 37x while he was down)", "kept his hat on"),
on arrival what was given back ("TreasureKey in his right hand given back to him (dropped on the
way)"), and warns if he still arrived without something he set off with ("Nate arrived without
TreasureKey (switched off: gone from the world), which he set off with").

Locations saved before facings were kept have none: Nate keeps the one he has, as before. Those saved before his feet and body were kept get the game's own stand-up; save them again to have them kept.

**Arriving steady.** Of 280 trips in the logs of 2026-11-09, 13 threw Nate down the moment he
arrived: at the first look, one frame on, he was down and his pelvis 0.5 to 25 m from where it
was put, mostly with his hips' pivot (the controller's, normally 3 cm from the pelvis) still on
the spot. Six were short trips taken while he was down after a fall (of about 150 such: the ones
taken while he still tumbled, by the look of them); five were the long trips (83 to 191 m, every
one of those taken standing), which wait up to 3.4 s for the world round the spot to load while
Nate stays where he was, and the ground under him can unload meanwhile; one was a quick load
taken 51 m below the spot, likely mid-fall; one an 18 m trip. Long trips taken while he lay still
went fine, and a second go always worked. What can carry his motion over, read from the game's
code:

- `TeleportCo`, once the spot has loaded, switches off Nate's whole body (`Dudest`, the
  `PlayerMovement` object, which holds the `PuppetMaster` too) and asks the puppet to go
  `Disabled`. Coming back on, the puppet takes that mode as its own (`PuppetMaster.OnEnable`), and
  `FinishCutscening` switches it to `Active` straight after its own stand-up
  (`SwitchToActiveMode`, then `SwitchModes`). That is `DisabledToActive`, which sets every part's
  speed to the speed its animated target was last seen going (`Muscle.mappedVelocity`,
  `mappedAngularVelocity`). Going limp (`AliveToDead`) does the same. The teleport's own stand-up
  stills him again after the first; nothing does after the second.
- That stored speed is worked out as the frame ends (`CalculateMappedVelocity`) from how far the
  targets moved since it was last worked out, which, the first time after a trip, spans the trip
  itself: for one frame after arriving it's the trip's distance over a frame or two. Should the
  game have him go limp in that frame, that's the speed he's given.
- The game's own record of how his hips and torso move (`PlayerMovement.hipVel`, `torsoVel`, how
  long both feet have been off the ground...) is as old as the moment he was switched off, and
  his balance and falling are judged on it.
- The teleport waits only for its loader's word (`BestRegionLoader.fullyLoaded`, worked out
  afresh each frame as the camera renders: the world chunks, the props' objects and their data
  round the loading spot). Nate isn't held meanwhile.

Which of these threw him isn't proven (that needs a throw caught in the log below); the mod deals
with all of them.

So a trip now:

- **holds the teleport** (the loader's flag is put down again each frame before the teleport
  looks at it) until the loader has said the world is in for 6 frames and 0.15 s in a row (its
  first word after the loading spot moves can be a frame stale), nothing has been loading for 4
  frames (`BestRegionLoader.somebodyLoading`; far-off scenery that keeps loading is given 1 s at
  most), there's ground under each foot he's to stand on (a ray down from 30 cm above the foot's
  target to 45 cm below it, or anything round the bottom of the foot; the game's
  `LayerCache.PropTerrainMask`; given up on after 2.5 s, in case what he stood on has moved), and
  he has been still for 8 physics steps. It's never held more than 20 s. A short trip with Nate
  standing is held about 0.2 s (`World/ArrivalGate.cs`, checked offline);
- **holds Nate still where he is** every physics step until the teleport switches him off (every
  part's speed and spin zeroed, from the mod's `OnFixedUpdate`), so whatever he was doing is over
  before he goes, and he can't fall where the world unloads round him (`Ragdoll.HoldStill`). The
  stance hooks keep out of it until then: should he get up from a fall while held, they leave
  that to the game. What he holds and wears stays on him meanwhile, even while he's down (see
  **Nothing left behind** above);
- **wipes what his ragdoll remembers of its motion** after every stand-up of the trip, the one
  before the puppet comes back on included (every part still, and `Muscle.ClearVelocities` on
  each, the game's own reset: stored speeds zero, and its targets' last-seen place taken where
  they are now, so the first speed worked out after the trip isn't the trip itself);
- **watches the first 0.6 s** there. If he goes down, or his pelvis gets 0.75 m from where it
  was put or from his hips' pivot, the log gets how he was each frame since arriving and he's sent
  again, once ("Nate lost his footing arriving - putting him back..."). If that goes wrong too,
  he's left there ("Nate couldn't keep his footing at ...").

The log says how fast he was going as he set off ("going 7.3 m/s"), how long the trip was held and
what for ("let go: held 0.22 s: world in after 0.10 s, quiet after 0.17 s, footing there after
0.12 s, held still for 11 physics steps (fastest part 0.3 m/s)"), how his ragdoll was when
switched off and at the first check after arriving ("puppet Active Alive, stored speeds up to
0.0 m/s, hips going 0.0 m/s, torso 0.0 m/s, both feet off the ground 0.00 s, up"), and how often
his motion was wiped ("his motion wiped 3x").

**Gym time trials.** The game's gym runs (`JesseGym`: the Underwater Gym's is on its room,
`RoomControl`) start over whenever anything comes into the room's trigger. Its `OnTriggerEnter`
zeroes the time and forgets the checkpoints passed, whatever came in, and the start line
(`GymCheckpoint` 0) does the same while Nate stands in it. The teleport switches Nate's ragdoll
off and back on where it puts him, and his body coming back on inside the room counts as coming
in, so every trip within the gym (F9 back to a quick save on the course) threw the run away. A
sixth Harmony hook (`GymGuard`), on that `OnTriggerEnter`, is active from the start of a trip
until the game has run 1.5 s after Nate arrives. The room still switches its things on as usual,
but its time and checkpoints are put back as they were just before. The clock keeps running
through the trip, so a run's time includes the trips it took. Walking into the room and the
start line still start a run over. The log says once per trip when a run was kept.

Followers (ducklings) stay where they were; bring them after him from the map (see
[Bringing items to Nate](#bringing-items-to-nate)). The game keeps saving where Nate is, so
after a trip its save has him at the new spot.

Locations are kept in `UserData/CabbySteps_locations.json` in world metres, with x
folded into the loop, plus `facing` and `camera` (degrees), `cameraRight`, `feet` (each
planted foot as `[x, y, z]` metres from the position, `null` for one in the air), `body` and
`saved` (UTC). The ones saved on the map are the `locations` list, the quick save is `quick` and
"where you were" is `back` (`null` when there's none). They're the same in every save slot. Each
of the `locations` has its `"name"`, and a numbered one its number `n` too ("Location 3" and 3).
Changing a name there works as it does on the map: a name of its own lets the number go, and an
empty one numbers it again (the map cuts names to 32 characters, the file doesn't). An
unreadable file is set aside as `CabbySteps_locations.unreadable.json`, not overwritten.

Offsets in the file further than 3 m from the position aren't believed (a foot, the
character's root, a ragdoll part), except the ragdoll's own root (`PuppetMaster`'s transform):
it doesn't follow Nate about (one was saved 22 m from his hips), so any offset within 10 km is
kept. Until this was allowed for, every body was dropped when the file was read, so after a
restart trips stood Nate on his saved feet only.

## Bringing items to Nate

Every movable item (hats, key items including the ice cream, quest items, the chicks and the
duckling: each POI with an `item` in `pois.json`) can be brought to Nate from the map. Put the crosshair or the pointer on
one and hold Y (Space, the right mouse button) for 0.8 s, as for going to a saved location. The
map stays open, and the item's dot moves to Nate. It's put down standing on open ground beside
him: in front of him if there's room (where the camera looks), else round his sides, behind him
last. Not while he's carrying or wearing it (the label says "with Nate": within 3 m), not once
it has left the game, and not while a saved location trip, a cutscene, his sleep or the game's
menu is under way (the same checks as for trips).

### How the game keeps them, and how they're moved

Read from the game's code (`ObjectLooper`, `GlobalObjectLoader`, `Savable`, `Grabable`,
`Duckling` and the developers' cheat `DevCheatsGoHere.BringForthGrabable`):

- Each item has a loader (`GlobalObjectLoader`) standing where the item is.
  `ObjectLooper.LoopList` loads the item when the world round the loader has loaded
  (`BestRegionLoader.IsHereLoaded`). The item is an Addressables instance made a child of the
  loader. It's unloaded when the world round the item goes, and the loader moves to where it was
  (`UnLoad(takeOnPos)`).
- On loading, the item goes where its loader is, then where the save last had it
  (`SaveFile.objTs`, by the loader's `loadKey`; `Savable.Load`).
- The developers' cheat moves a loaded item by switching it off, moving it and switching it on
  again, then syncing the physics scene (`Physics.SyncTransforms`). An unloaded one has its loader
  moved. That's the model here, with more care:
  - A **loaded** item is moved that way (`src/World/Summon.cs`). Its physics then starts afresh
    there, with no in-between frames drawn even if it was interpolated. Its animators keep their
    state through it (`keepAnimatorStateOnDisable`, else a chick's animation would restart).
    Its velocities are zeroed, as is `Savable.lastVelocity`, which the game gives back to the body
    when its physics switches on. A duckling's `lastJumpPos` (it judges its next hop from it)
    moves too.
  - An **unloaded** item has its loader moved beside Nate, and its `objTs` entry too when it has
    one; otherwise `Savable.Load` would put it straight back where the save had it. The game loads
    it there within a second or two, and it's then put down like a loaded one. If the game unloads
    it again before it settles, the loader is moved again (up to three times).
  - It all works with the map open and the game frozen: loading runs every frame regardless of
    the time scale.
- An item no one has picked up yet stays kinematic: `Grabable.Update` only runs the physics
  (`Savable.PhysicsUpdate`) once `beenTouched`. So it isn't dropped. It's stood on the ground:
  its size and where its pivot sits come from its colliders' bounds, and it goes 2 cm above the
  highest ground under it.
- The spot (`src/World/SummonSpot.cs`, checked offline against a made-up world) is tried in
  front of Nate, then at 35°, 70°, 110° and 150° either side, then behind. It's tried just clear
  of him first, then 0.8 m further out. A spot needs:
  - nothing solid on the line from his hips;
  - ground under it no more than 2.4 m below his hips (nothing goes down a drop);
  - ground no steeper than 45°, with no step over 25 cm under the item's footprint;
  - room for the item's box, with nothing in it: ground, props, other items or Nate;
  - to be clear of spots items were put in during the last 10 s (a loading item can't be seen
    yet).

  Failing that, the first spot in sight of him with ground under it will do. Failing that, it's
  where the developers' cheat puts things, in front of him at hip height. The ground is what Nate
  stands on, with the game's own layers: `LayerCache.PropTerrainNonDynamicMask`, which is the game
  teleport's `PropTerrainMask` without picked-up items.
- A loader riding a turtle (`LoaderFollowTurtle`: the Trophy Hat's, on the tortoise that walks
  round the world) follows it until the save has a held state for the item. Brought to Nate, it
  stops riding.
- A loader the game keeps switched off isn't out in the world yet (the Winter Hat's starts off),
  so its item can't be brought. Nor can an item the game has switched off: used up for good, or
  the ice cream before Mitchell hands it over or once it has melted ("it isn't out in the world
  just now").

The game's autosave keeps the new place: `GlobalObjectLoader.Save` calls the item's `Save` once
it's more than 0.3 m from its loader. `Grabable.Save` only saves once the item has been picked up
(`beenTouched`, which also adds its entry in `SaveFile.heldStates`). An item brought to Nate but
never picked up goes back to its own place when the game is loaded again, as the game's untouched
items always do. Pick it up once and it stays. Chicks and ducklings are always saved.

Items that move by themselves keep doing it: the Baby Trophy runs off and the "Falcon" can fly
off again. A chick that hasn't met Nate yet imprints on him, as it would on finding him
(`Duckling.FixedUpdate` imprints within `imprintDist`, with the same animator trigger as on
loading, which is why its animator state has to survive the move).
Tracking an item stops once it's been brought.

## Grippy feet (a cheat)

F7 (keyboard, while playing or on the map) steps it through three levels: off, Grippy feet,
[Extreme grip](#extreme-grip), and off again. With Grippy feet:

- **A planted foot can't slip.** Mud, ice, wet rock, a steep slope or a stream: once a foot is
  down it stays exactly where it was put, and it pushes off as well as on dry rock.
- **Nate keeps his balance while a foot is down.** Lean as far as he'll go, stand on one leg as
  long as you like, splay his legs or sink into a crouch: he doesn't topple, his legs don't tire
  or shake, and his arms don't windmill. A narrow perch (a pillar top, a post) holds him.
- **What still floors him**: going over an edge with neither foot down, which is a fall as usual
  (falling fast, tipping over in the air or landing hard still knock him down), and the things
  that knock him down whatever his footing: cacti and boulders. Continuing a save he was down in
  still has him down.

A small "GRIPPY FEET" tag in the top-left corner shows while it's on ("EXTREME GRIP" at the second
level; not on the map). It's kept in the config (`grippyFeet`: `"off"`, `"grippy"` or `"extreme"`;
an older file's `true` reads as `"grippy"`), so it's still on next time the game starts, until F7
turns it off. Switching it off mid-lean hands his balance back to the game, so he may topple then.

There's no pad button for it. In play the game reads the pad itself, through Rewired, with
bindings kept in its scene data that couldn't be checked offline, so any button might already do
something. On the map every button is taken.

### How it works

Read from the game's machine code (`PlayerMovement`, Cpp2IL's field offsets, the disassembly).
The hooks go in the first time the cheat is switched on, on a Harmony instance of their own
(`World/GrippyFeet.cs`). If any can't be made, none stays and the cheat stays off.

**Slipping.** Every physics step, `HipForces` sets each planted foot's sideways push against what
it can hold: the push into the ground times the foot's traction (`FootData.curTractionCoef`,
which `GetGrounded` sets from the surfaces under the foot's points and the slope, about 0.5 to
1.5). When the sideways push wins for more than two steps running (`slipTimer`), the foot gathers
speed (`slipVel`, slowed by a drag that depends on the surface), its target is moved by it, and
the slip sound plays (`PlayerCollisions.StartSlipping`). The cheat zeroes each planted foot's
`slipVel` and `slipTimer` before every `HipForces`, so a slide never gets going. It also gives a
grounded foot at least 3 traction after `GetGrounded`. Traction's only other use is push-off
(`DoPushOffForce`: at most push into the ground × traction × 0.5, and never more than 0.5), so
pushing off works on ice as on rock, and never beyond the game's own maximum.

**Balance.** The game sets his centre of mass (`centerOfMass`, worked out from his body every step
by `UpdateCenterOfMass`) against the outline of his planted feet (`GetPolySupport`, called from
`HipForces`, into `polySupport`, `polySupportLength`), with `InsidePolySup` (left of every edge,
in x/z; there's no edge back from the last point to the first, since the outline repeats its first
point) and `ClosestPtOnPolySupport` (the nearest point of those edges, even from inside). With his
weight beyond his feet:

- each planted foot's strength (`FootData.maxExert`, the cap on the torque it applies) fades as
  the line from his weight down to the nearest point of the outline leans past 13°, and is gone
  at 28°, so his legs give;
- with both feet down and his weight more than 5 cm beyond them, `magicFallOffTimer` runs, and
  the `magicFallOff` curve drains that strength too;
- `HipForces` sets `hipHoldingYouUp` (his hip resting on something while he's off balance), and
  `DeadCheck` counts `offBalanceDeathTimer` up, both of which end in `Die`;
- his arms windmill (`SetWindmilling`).

So right after `GetPolySupport` (a postfix), whenever his weight is beyond the outline it's counted
2 cm inside it, from the nearest point towards the middle of his feet, and every one of those
tests finds him balanced. The next step works it out from his body afresh.
`World/SupportPolygon.cs` mirrors the game's two tests exactly and is checked offline. A planted
leg also tires as it strains (`FootData.fatigue`): a tired foot's strength is capped lower
(`torqueFatigueCurve`), and past a point `HipForces` jolts his hips at random (the leg shaking).
Before every `HipForces`, the planted legs are rested and `magicFallOffTimer` starts again.

**Falls.** Nate is knocked down by `PlayerMovement.Die`, the only code in the game that sets his
`dead` flag (`OnStandUp` clears it). It's called by:

- `DeadCheck`, every step. With a foot planted: at once if his feet are more than 1.95 leg lengths
  apart; after `deathTime` with his hips sunk almost to a planted foot (or both feet up level with
  his hips), a planted foot above his hips (`footTooHighDistToHip`, counting a third as fast),
  propped on his hip, or in deep water (`shouldWaterDie`); after `offBalanceDeathTime` off
  balance. With neither foot down: a hard hit to his hip (`hipCollisionDeathThresh`), his body
  tipped more than 60° from upright, or falling faster than 11 m/s.
- `HipForces`: his hip hitting something hard (an upward jolt of more than 2 m/s), or, with
  neither foot down, a fall that has stalled.
- Cacti (`CactusDie`, `KillerCactus`), boulders (`KillerBoulder`), continuing a save in which he
  was down (`Menu.UnPause`, "saved dead state killed u"), and the developers' `RedoLastGetUp`.

While the cheat is on and a foot is planted, a `Die` from inside `DeadCheck` or `HipForces` is
skipped (a prefix), and the timers that led to it start again. The ragdoll can also give way by
itself: `BehaviourPuppet.OnFixedUpdate` unpins it (`SetState(Unpinned)`) when a part is knocked
too far from where it should be. That's skipped too while a foot is planted and Nate isn't down.
`Die` marks him down before it unpins the ragdoll, so the game's own falls still go limp.

**The log** says when the cheat is switched on and off, and on switching off what it did meanwhile
("kept him on his feet 3 times (off balance 2, slumped 0, legs splayed 1, hip hit 0), kept his
ragdoll from going limp 0 times, held off 4 slides, and counted his weight over his feet in 812
of 9644 physics steps"). Each fall it stops gets a line too, at most one every 10 s, with the
totals so far.

### Extreme grip

F7 once more from Grippy feet. Everything above still holds, and footholds can be anywhere: **put a
foot down while it's pressed against something** (a wall, a cliff face, the side of a rock, a slope
too steep to stand on) **and it holds right there**, as if on a little ledge. Lift the other foot
and Nate stands on it, so he can walk up a sheer wall one foothold at a time, or down one. To climb:
lift a foot as high as it goes (its trigger all the way), push it into the wall with the stick, let
go of the trigger, lift the other foot, and so on. Pulling a foot's trigger lets that foot go, once
his weight is off it, as on the ground.

- A foot put down without touching anything drops to the ground as usual, and a foot that finds
  real ground under it is an ordinary step. Only a foot being put down takes hold: one held up
  (its trigger pulled) moves freely along a wall.
- It holds whatever it's against: things that move (a loose item) can move away and leave the
  foothold in the air.
- F7 again turns the whole cheat off, and a foot on a wall comes away (with both up there, he falls).
- A quick save on a wall is meant to work while Extreme grip is on: going back puts his feet where
  they were, pressed against the wall, so they take hold again.

#### How Extreme grip works

Read from the game's machine code, decompiled with Ghidra (types from Cpp2IL's dummies) and checked
against the disassembly. `World/ExtremeGrip.cs` hooks four `PlayerMovement` methods, on a Harmony
instance of its own, the first time Extreme grip is switched on; if any can't be made, none stays
and F7 goes on to off. The rule for each foot is `World/FootGrip.cs`, game-free and checked offline.
Only methods that take objects and plain numbers are hooked: Il2CppInterop's native-to-managed
trampoline (1.5.1, `Il2CppDetourMethodPatcher`) reads a struct passed by reference as if it were an
object pointer, which would break `TargetDistFromGround(foot, out RaycastHit)`, so that's left alone.

**What counts as planted.** Every physics step `FixedUpdate` calls `GroundedCheck(i)` for both feet,
which asks `GetGrounded(foot)`. That casts the foot's box (`FootData.coll`, at its target
`targetTrans`, 99% size, from 5 cm up) straight down with `LayerCache.PropTerrainMask`
(`TargetDistFromGround`) and wants ground within 3.5 cm (7.5 cm for a planted foot not being lifted);
then it casts 9.5 cm down from points across the sole (`FillGroundFootPoints`) and wants a surface
facing up (normal.y above 0.2) under at least one, averaging their traction. A wall never passes. A
foot that passes is planted (`planted`; `GroundedCheck` is the only code that sets it), and
`GroundedCheck` snaps it down onto that ground (`targetPos += down × TargetDistFromGround`, 999 m if
there's none within 100 m) and finds its pivot (`pivotPt`, `originalPlantSpot`: `GetPivotPt(foot,
true)`, a thin box cast 30 cm down from the sole, then from higher up, which also notes what it
stands on and calls `CactusDie` on a cactus). A foot that fails is taken up (`planted` false), and a
foot in the air whose trigger is let go (`TriggerMove`: trigger at most 0.05) drops at `curDropSpd`
until something passes. (The foot's own box is a trigger while he's up: `FixedUpdate` sets
`coll.isTrigger = !dead`.)

**Taking hold.** Right after `GetGrounded` (a postfix) for a foot it found no ground under, the
foot takes hold if it's being put down (trigger at most 0.05, `isLifting` false) and its box at
its target, 1 cm bigger all round, overlaps anything in `PropTerrainMask` (`OverlapBoxNonAlloc`,
triggers ignored, the foot's own box and what he's holding skipped: `IsHeldItemColl`). The answer
is made yes, with `plantNormal` straight up and traction at least 3, so `GroundedCheck` plants it as
on flat ground, snapping it down and casting for its pivot from down there. Right after (a
`GroundedCheck` postfix) the foot is put back where it took hold (`targetPos`), its pivot at the
bottom of its box, standing on no moving body (`steppedOnRb`); in between, `CactusDie` is skipped (a
prefix), so a cactus far below the foothold can't count. `GroundedCheck` can hold off planting a
foot for a step (a "soft plant" while the trigger was only just let go); then the hold is let go and
taken again next step.

**Standing on it.** The hips go where the planted feet's pivots say: `GetDesHipHeight` takes, over
the planted feet, the lowest of pivot height plus the leg's reach to the hips' target
(`GetAngledLegHeight`, at least 0.57 m), and `HipForces` pushes the hips along each planted leg
towards it (`legP`/`legD`, at most `legVStrength` through `legVStrengthExtendCurve`), against the
game's own gravity on `hipVel`. A pivot up a wall is a ledge to it: with the lower foot lifted, his
hips rise over the one that holds, and the lifted foot (raised up to `maxLegHeight` from his hips,
`GetFootTargetY`) can reach the next foothold. His balance and falls are Grippy feet's.

**Holding on.** Each later `GetGrounded` from `GroundedCheck` keeps a holding foot planted, whatever
the game's casts find, until the game lifts it (`isLifting`, set by `GetIsLifting` once his weight allows),
finds real ground under it, has him down or in a cutscene (`dead`, `inCutscene`), or stands him up
(`OnStandUp` asks `GetGrounded` for both feet, after a fall or a trip). `PlantedFootRotation`, which
rolls a planted foot heel to toe and keeps a rolling foot on the ground under it, is skipped for
it (a prefix). Nothing else moves a planted foot: its slide (`slipVel`, `currentSlipVector`) is
Grippy feet's to stop.

**The log** says when Extreme grip goes on and off, and what it did meanwhile ("taken 14 footholds
(the highest 6.2 m above the ground), let go 13 times (lifted 12, on solid ground again 1)"). A foothold taken gets a line, at most one every 10 s: which
foot, what it took hold of, where, and how high above the ground under it.

## Hold tight (a cheat)

F8 (keyboard, while playing or on the map) switches it on and off. While it's on, **what Nate holds
stays in his hands and his hat stays on**:

- **Falls**: going down no longer shakes things loose. Jolts and knocks while he's down don't make
  him drop what he holds or knock his hat off, the Baby Trophy can't wriggle out of his hand to run
  off as he falls, and a vase in his hand doesn't break when it's knocked about.
- **The wind** can't lift the propeller hat off his head (it spins there, as in a light breeze).
- **A night at a camp**: the morning no longer takes his hat off and sets it down by the fire.
- **Swapping still works.** Picking up something else with a full hand drops what was in it, and
  putting on another hat takes off the one he wore, as usual (even while he's down).
- **What stays the game's**: handing things over in a conversation, using them up (a potion drunk,
  the camera's one photo), the lantern going out or being taken by the story, the ice cream melting
  away, a vase breaking once it's out of his hands, trips (they keep what he holds and wears
  themselves: "What he holds and wears" under [How Nate is moved](#how-nate-is-moved)), and the
  ending.

A small "HOLD TIGHT" tag in the top-left corner shows while it's on (under Grippy feet's, if that's
on too; not on the map). It's kept in the config (`holdTight`: `true` or `false`), so it's still on
next time the game starts, until F8 turns it off.

### How it works

Read from the game's machine code: every call of `PlayerMovement.DropHandItem` (which lets go of
the thing in one hand: unjoints it, gives it its colliders back, unparents it) and of `KnockOffHat`
(the same for the hat). The ones that are losses:

| Where | What the game does | Hold tight |
| --- | --- | --- |
| `PlayerMovement.DeadUpdate` (each physics step he's down, unless he's floating) | drops a thing when his head's speed changes by more than 4 m/s in a step, or a hand's by its own limit; knocks the hat off by `hatOffThresh` times the hat's `knockOffThreshMult` | kept |
| `PlayerMovement.BodyPartHit` (he's down, not floating, not getting up) | drops what's in a hand when that hand or arm hits something hard (`velDelt` over 0.12, or 0.24) | kept |
| `RunawayTrophy.FixedUpdate` | while he's down, lets go of the Baby Trophy, which then runs off | kept |
| `PropHat.FixedUpdate` | marks the propeller hat flying and knocks it off when the weather's wind passes its `liftOffWind`; while flying it's pushed along | kept on, and marked not flying again |
| `Menu.MorningCampFireCo` (the camp's morning) | knocks the hat off and puts it at the camp's `hatDropSpot` | kept on; the spot is hidden from that step of the morning, so the hat isn't moved to it |
| `BreakVase.CheckCollision` | breaks a held vase knocked harder than its `breakThreshold` while he's down (or in the first second after he picks it up) | the check is skipped while it's held |

Everything else that calls them is left alone: `GrabGrabable`, reaching for a thing
(`SetReaching`, which grabs inline) and `ForceGiveGrabable` let go of what the hand held (a swap),
`WearHat` takes off the hat he wore (a swap; putting a hat from his hand on his head goes through
it too, `CheckHatPutOn`, even while he's down), `BBConvoHoldingItemTrigger` (a conversation that
takes the thing), `Polaroider.TakePhoto` (the camera's one photo), `LanternHandler` and
`LanternHelper` (the lantern going out, or moved by the story), `IceMelter.MeltedAwayToNothing`,
`BreakVase.BreakMe` (it broke), `DrinkPotion`, `EndingConditionalLoader`, the teleport
(`Menu.TeleportCo`, which lets go with his body switched off and gives everything back), and the
developers' `DebugStuff`. No joint breaks by force: the hands' and head's joints have no break
force set, so nothing else takes a thing off him.

The hooks: `src/World/CarryGuard.cs` already hooks `DropHandItem` and `KnockOffHat` (prefixes, for
trips), and asks the cheat when a trip isn't keeping the thing itself. `src/World/HoldTight.cs`
hooks `DeadUpdate`, `BodyPartHit` and `WearHat` (before and after: only to note which of them is
running), the first time it's switched on, on a Harmony instance of its own; if those can't be
made, none stays and the cheat stays off. The others (`RunawayTrophy.FixedUpdate`,
`PropHat.FixedUpdate`, the morning coroutine's `MoveNext`, `BreakVase.CheckCollision`) are made
one by one: one that fails leaves only its case to the game, and the log says which. The rule
itself (which cause keeps what, swaps, things gone from the world) is `src/World/HoldRules.cs`,
game-free and checked offline. A thing that's gone from the world (switched off, destroyed) is
always let go of.

**The log** says when it goes on and off, and what it did meanwhile ("stopped the game taking what
he holds 37 times (jolted 30, hit 7, the Baby Trophy 0), his hat 3 times (jolted 2, camp 1, wind 0),
and breaking a vase in his hand 0 times"). Those are the game's tries: it tries again every physics
step the cause lasts (every step he's down and jolted, every step the wind blows hard), so one fall
can count dozens. A thing kept gets a line, at most one every 10 s: what it was, where, and what
would have taken it.

## Hero's Journey

D-pad Left (J) on the map opens it: the path Nate has walked in the loaded save, painted the way
the game's own ending paints it, on the ending's own painted map sheets. The mod no longer
records a trail of its own (`UserData/BabyStepsMinimap_trail.txt` is no longer read; the mod
logs that it can be deleted), and the map and minimap no longer draw one.

**The record** is the game's (`SaveGod.SaveForever`, read from its machine code): every 5 s or
so, once Nate has moved a metre, two numbers go into `SaveGod.positions`, which is written out
as `SaveGod.GetPositionsPath(slot, n)` (`path<slot>_<n>.sav` in the save folder, 50000 numbers
a file, zeros after the last). The first is his x across the 512 m loop at 63.999 a metre (its
top bit: a ray from 2 m over his head towards the map's viewpoint, up 60° to the south, hit
something within 100 m, so the painting can't show him), the second how far up the sheets he is
(half his height plus 0.866 of his z, wrapped at 3631 m) at 13.107 a metre. That's the map's
projection (see [How positioning works](#how-positioning-works)). `JourneyRecord` reads the
files before `positionFileIndex` from the disk and the one being filled from the game's buffer
(up to `positionsIndex`, ahead of its file). `JourneyPath` decodes them as the ending's
`EndCredits.LoadAllPositions` does: both numbers over 512 (so x runs 0..1 across the sheets and
y up them), skipping unwritten pairs and points that haven't moved.

**The drawing** is the ending's (`EndCredits.DrawShapes`/`DrawLine`, Shapes discs): each stroke
starts with a dot on its first point, then red dots (0.8, 0, 0) of radius 0.002 every 0.01, each
a step from the last dot towards the next point, the remainder carried on; a dot of 0.004 on the
pen's tip, part way along the segment being drawn. A step of more than 0.75 across is the world
loop's seam: a big dot ends the stroke and a new one starts on the other side. The pen moves on
13 points a second, faster in proportion past 8640 points (no journey takes longer than 11
minutes). The camera keeps the tip within 0.05 of the middle of the view, up and down, never
going below where it starts (`Utils.TetherTo`, `bottom`). The view is the ending camera's:
60° high at 0.883 from the dots, so about 1.02 units tall, centred across the sheets.

**The set** is the ending scene's (`Moose_Cabin_Prefab/EndingCutscene/Ending` in a world chunk by
the summit): six painted map sheets (`Map0` at the bottom to `Map5`, paintings `Assemble05` to
`Assemble00`, 1.17 units square) up a wooden board, each on a drop shadow, lit warm.
`tools/georef/find_journey.py` reads their places, the camera and the Addressables key of each
sheet's material into `package/Mods/CabbySteps/journey.json`. The sheets are the map's
own, already loaded ([The sheets](#the-sheets)); `MapArt` asks the game's Addressables for the
board's material while the Journey is open and lets it go when it closes, so none of the game's
art is shipped. A sheet or board whose painting hasn't arrived (or can't, after a game update)
is plain paper.

**Beyond the ending:** pause and play (A, Space, a click; at the end it starts again), rewind
and fast-forward (left stick across, A/D: the whole journey in 20 s at full tilt), look up and
down (sticks up/down, W/S, drag), follow the pen again (X, C), finish now (Y, F), and zoom out
as far as the whole board (D-pad Up/Down, PgUp/PgDn, the wheel). The header and footer hide
while it paints and come back with any input. B (Esc, a right-click) goes back to the map.

## Memory: the game's GC stalls, and the mod's garbage

Sessions were ending with systemd-oomd killing Steam (2026-11-09: a 58 GB game heap after
an hour). Two things combined.

**The game's garbage collector can stop collecting by itself.** It's Unity's Boehm GC (bdwgc,
incremental) inside `GameAssembly.dll`. After each collection it decides when to collect next
from an estimate of what it must scan, which includes the total size of all thread stacks
(`stack_base - sp` per thread, summed in `GC_push_all_stacks` with no check that `sp` is inside
the stack). Read from the running game under Proton: at collection 591 that total came out as
**-2.9 GiB**. The threshold computed from it wrapped round to about 3 exabytes
(`last_min_bytes_allocd = 3074457344600215314`), so no automatic collection ever triggered
again. Collection wasn't switched off (`GC_dont_gc` 0) or stuck mid-cycle (`GC_mark_state` 0),
and the heap grew by everything allocated from then on. Which thread was out of its stack
wasn't caught before the game was killed. An explicit `GC.Collect()` still works and measures
the stacks afresh.

**The mod made that fatal by producing garbage fast.** Through Il2CppInterop, every Unity or
game method the mod calls goes through `il2cpp_runtime_invoke`, and the game boxes any value
it returns: `Mathf.Sqrt`, `Rect.x`, `Color.white`, `Time.realtimeSinceStartup`,
`Input.GetKey`, `Vector2 + Vector2`... each leaves a small object on the game's heap. Done per
POI, per trail point and per frame, that was about **175 MB a minute** in play (the minimap)
and **11 GB a minute** with the map open (the Hero's Journey trail the map drew then, four boxes
per segment for 7,600 points, drawn twice a frame). While the game collected (several times a minute), that
was only churn; once it stopped, it was an OOM within the hour.

The fixes:

- `src/Util/GcWatchdog.cs`: when the game's collection count stands still for 2 minutes while
  its heap grows by 512 MB, the mod asks for one collection (`GC.Collect()`) and logs it as
  `[gc]` in `MelonLoader/Latest.log`. While collection works it never does anything.
- The mod's own garbage is gone from its hot paths. `Util/Fast.cs` has plain C# maths and
  builds `Vector2`/`Vector3` field by field, and `UI/Gfx.cs` does the same for `Rect`/`Color`
  (Unity keeps them in `m_XMin`... and `r`/`g`/`b`/`a`). Labels are drawn from `GUIContent`s
  that only change when their text does (`LabelText`, `Gfx.Fixed`). `Util/Frame.cs` reads the
  time, frame count and screen size once a frame, from the .NET clock where possible.
  `Controls` asks once a frame whether any key is down (`Input.anyKeyDown` is a direct call
  that doesn't box) before asking about single keys. Drawing happens only in IMGUI's Repaint
  pass. GameLinks checks the save's dialog keys only when the set changes, and first person
  keeps its camera between frames.
- The rest was turning things on the screen. `GUIUtility.RotateAroundPivot` isn't in this
  game's build, so Il2CppInterop supplies a stand-in that rebuilds Unity's own code. That
  stand-in makes about ten calls into the game (`GUI.matrix`, `Matrix4x4.TRS`,
  `Quaternion.Euler`, `Matrix4x4 * Matrix4x4`...), each leaving a boxed value of up to 64 bytes
  on the game's heap. The map turned every segment of its Hero's Journey trail with it, so the
  map alone still made 2–4 GB a minute (the `[mem]` reports of 2026-11-09), and the minimap and
  HUD arrows several MB more. The matrix is now built in plain C# (`Fast.TurnAbout`,
  `Gfx.Rotation`) and set with `GUI.matrix = ...`: a call that returns nothing, so it boxes
  nothing. Core starts every repaint with the identity matrix, so the old matrix is never read
  back. Inside a GUI group the pivot goes in screen coordinates, as `RotateAroundPivot`'s
  unclipping did (the minimap's arrow).

### The memory report

`src/Util/MemoryDiagnostics.cs` is on while `"debugMemory": true` is in
`UserData/CabbySteps_config.json` (the default for now). It logs two `[mem]` lines
every `debugMemoryInterval` seconds (default 60) to `MelonLoader/Latest.log`, each figure
with its change per minute:

- **process**: working set and private bytes, as Windows sees them inside Proton;
- **game GC heap**: the game's managed objects, and the number of collections so far. A count
  that stands still while the heap climbs is the stall above;
- **Unity allocated / reserved**: native memory for textures, meshes and so on;
- **FMOD**: the game's audio engine;
- **mod .NET heap**: the mod's own managed memory;
- **objects**: counts of Unity objects by type, every fifth report (counting makes the game
  build arrays of every object, a megabyte or two each time);
- **mod, per min**: how often the mod's suspects ran, the share of time the map was open,
  **what the mod itself allocated on the game's heap** (read around its update, its minimap
  and HUD, and the map; a reading with a collection in it is skipped; the readings themselves
  cost about 0.5 MB a minute), and how many collections the watchdog forced.

**F10** logs a report now. **F11** forces a full collection first (the game's GC, .NET's,
and `Resources.UnloadUnusedAssets`) and reports two seconds later, so what's left is
really held on to rather than garbage.

To narrow a problem down, `"debugDisable"` switches parts off: `links`, `savesync`,
`minimap`, `hud`, `headarrow`, `mappause`, `gcwatchdog`. For example,
`"debugDisable": ["links"]`.

## Layout

| Path | What it is |
| --- | --- |
| `src/` | The mod (`CabbySteps.csproj`): `Core.cs` plus `Map/`, `World/`, `UI/`, `Input/`, `Util/` |
| `tests/MapTests/` | Offline checks: the game's projection and the map's frame (journey.json), Map Genie's map against the sheets, every POI drawn where the game puts it (or on Map Genie's marker), the map view maths, the completed-POI snapshots (continue, backup, new game, other slot), saved locations (numbering, names and their initials, the quick save, the file, feet and body, the world loop), typing a name on the on-screen keyboard, where summoned items are put down (walls, drops, rocks, items already there), Grippy feet's balance outline (the game's own inside and nearest-point tests, and where his weight is counted), Extreme grip's rule for each foot (when it takes hold, keeps hold and lets go), Hold tight's rule (what falls, the wind and camps may no longer take, swaps, things gone from the world, its config value), the Hero's Journey (decoding the game's record, the ending's dots, seams and pace), and files kept under the mod's old name (renamed once, never brought back after a new game) |
| `tools/georef/` | Builds the map data from the game: `find_anchors.py` (anchors, with `tps.py` to predict where Map Genie's markers are), `find_links.py` (save records, and where cutscenes and campfires are), `find_journey.py` (the ending's painted sheets: the map, and the Hero's Journey set), `register_mapgenie.py` (where Map Genie's map sits on the sheets: `mapgenie.json`), `build_pois.py` (`pois.json`), Map Genie's POI export |
| `tools/ilcompare.py` | Type-by-type IL comparison of two assemblies (uses `ilspycmd`) |
| `package/` | The release archive's other files: `Mods/CabbySteps/` (`pois.json`, `journey.json`: no map art, the map is the game's), `UserData/` (starter config), `README.txt` |
| `README_BBCode.txt` | The mod's description for its Nexus Mods page |
| `reference/` | Not in the repo: the original mod's DLL (`BabyStepsMinimap.dll`, 1.2) and its recreated source (`original-source/`, see History). Both are Slylos's work, so they're kept locally only (`.gitignore`); `make il-diff` and `make install-original` need the DLL, from [its Nexus page](https://www.nexusmods.com/babysteps/mods/14) |

## Building, testing, installing

You need a .NET SDK that can target `net6.0` (the .NET 8, 9 or 10 SDK all work),
and Baby Steps with MelonLoader 0.7 installed **and launched once**. The first
launch generates `MelonLoader/Il2CppAssemblies/`, which the build compiles against.

```sh
make build              # -> build/CabbySteps.dll
make test               # projection, map frame, world loop, data, completed-POI, saved-location, naming, trip-hold, carried-things, summon-spot, balance-outline, foothold, hold-tight, journey and old-name checks (no game needed)
make install            # build + test, copy into the game, verify the copy
make install-original   # put the original mod's DLL back in place of CabbySteps (needs reference/, and Map Genie's map images, neither shipped)
make clean
```

The game folder defaults to the usual Steam library for your OS. If yours is
elsewhere, copy `GameDir.local.props.example` to `GameDir.local.props` and set
`<GameDir>`, or pass `GAME_DIR='/path/to/Baby Steps'` to make.

`make install` copies the DLL and any changed map data, and adds the starter config
only when the game has none, under either name (the mod renames a `BabyStepsMinimap_config.json`
itself). It never overwrites your settings or saved locations, and it refuses while the game is
running (`FORCE=1` overrides). It takes out `Mods/BabyStepsMinimap.dll`, the mod's old name (or
the original mod), which would load alongside. The old data folder, `Mods/BabyStepsMinimap/`
(with Map Genie's map images, `map.png` and `map_hi.png`, if the original was installed), is left
in place; nothing in it is read any more, and the mod logs that it can be deleted. The DLL embeds
its PDB, so exceptions in `MelonLoader/Latest.log` carry file and line numbers.

## Regenerating the map data

Only needed if a game update moves things, or to improve the matching.

```sh
python3 -m venv .venv && .venv/bin/pip install UnityPy numpy pillow
make anchors PYTHON=.venv/bin/python      # reads the installed game, ~20 s
make links PYTHON=.venv/bin/python        # what the game records per POI, where cutscenes and campfires are, ~20 s
make journey PYTHON=.venv/bin/python      # the ending's painted sheets (the map), board and camera, ~2 min
make pois                                 # pois.json: places POIs by the game's projection, merges the links
make test && make install
```

`make mapgenie MAP=/path/to/map.png PYTHON=.venv/bin/python` re-measures where Map Genie's map
sits on the sheets (`mapgenie.json`, used to put the POIs with no game object on their marker).
That only changes if Map Genie redraws its map. Their map image isn't in this repo; the
sibling site `../BabyStepsMap` has it (4096 wide, which the tool handles).

`find_anchors.py` prints every POI it couldn't anchor and why. To anchor more,
add the Map Genie title and the game object's name to its `NAMES` table (or
`REPEATED` for landmarks that appear many times; `PINNED` for a pair nothing can
predict). A hat's or item's loader is kept however far Map Genie's marker is from it
(it prints those, as `item:` lines). `find_links.py` fails if a linked cutscene has no starter or no place.
`build_pois.py` prints which POIs the game places more than 10 m of map from Map Genie's
marker, which markers it fanned out, and how far Map Genie's markers are from the game's
projection of their game objects.

## Code map

| File | Role |
| --- | --- |
| `Core.cs` | `MelonMod` entry: builds everything, gameplay controls (help level, arrow style, first person, F7 for Grippy feet's levels, F8 for Hold tight), opens/closes the map, pauses the game while it's open, dispatches drawing; warns if an old `BabyStepsMinimap.dll` is loaded too |
| `ModContext.cs` | Shared state and services handed to the screens (config, POIs, the map's sheets, projection, player, controls, pointer, completed POIs, saved locations, recall, summoner, toasts, tracked target) |
| `Map/MapProjection.cs` | World → map pixels, the game's own way (`u = x / 512`, `v = (0.5 y + 0.866 z) / 512`), the map's frame round the sheets, and the on-map angle of a world direction (game-free, checked offline) |
| `Map/MapArt.cs` | The game's painted map sheets (and the Journey's board) from `journey.json`, their paintings loaded through the game's Addressables and kept for the session |
| `Map/MapAssets.cs` | `pois.json`: each POI's place on the sheets and in the world |
| `Map/Poi.cs` | A POI: Map Genie id, category, title, map pixel, world position, and its links to the game (`found` records, movable `item`) |
| `Map/PoiProgress.cs` | POIs marked completed, as play-time-tagged snapshots per save slot |
| `Map/SavedLocations.cs` | Saved locations (a kind of POI): numbers, and names that take their place (with the initials on the pin), which way Nate and the camera faced (`SavedPose`), the quick save (F5 while playing saves over it, F9 goes back), the "where you were" spot, the file |
| `Map/MapDraw.cs`, `Map/MapView.cs` | Draws the sheets the view reaches around a given map pixel (plain paper until a painting arrives); screen ↔ map pixel maths and zooming about a point |
| `World/WorldLoop.cs` | The 512 m east-west loop: wrapping and short-way-round distances |
| `World/PlayerTracker.cs` | Finds Nate (`Dudest/PuppetMaster/root.x`); position and camera-based facing |
| `World/JourneyPath.cs` | Hero's Journey, game-free: decodes the game's record and works out the ending's dots, strokes and pace |
| `World/JourneyRecord.cs` | Reads the game's record of Nate's path for the loaded save (its files, and its buffer in memory) |
| `World/GameBridge.cs` | Game hooks: first person (a virtual GoPro hat plus Cinemachine solo camera, set aside while Nate is moved), and whether the game's own menu is open |
| `World/Recall.cs` | Sends Nate to a saved location with the game's own teleport (`Menu.Teleport`), once the map has closed and when the game allows (down after a fall included), facing the saved way (turned once the teleport has switched his ragdoll off), at the saved height; holds the teleport until the spot is ready and Nate is still, and sends him again once if he's thrown arriving |
| `World/ArrivalGate.cs` | When a trip may put Nate down (game-free, checked offline): the world loaded and quiet, ground under his saved feet, him still, never held more than 20 s |
| `World/Ragdoll.cs` | Nate's ragdoll for trips: holding him still, wiping the motion it remembers (`Muscle.ClearVelocities`), ground under his saved feet, how it is (for the log) |
| `World/StanceRestore.cs` | Harmony hooks on `PlayerMovement.FindBestFootSpots` and `OnStandUp`: during a trip (once the teleport has switched him off), the teleport stands Nate up exactly as he was saved (feet, hips, roots, every ragdoll part), what he holds and wears is put back in his hands and on his head, its joints made afresh, and his ragdoll's remembered motion is wiped |
| `World/CarryGuard.cs` | Harmony hooks on `PlayerMovement.DropHandItem` and `KnockOffHat`: while a trip holds Nate and he's down, the game doesn't have him let go of what he holds or lose his hat; whatever he set off with that the teleport didn't bring is given back to him there, the game's own way. Otherwise they ask Hold tight |
| `World/CarryPlan.cs` | Which hands get back what they held, and whether his hat goes back on (game-free, checked offline) |
| `World/HoldTight.cs` | The Hold tight cheat: Harmony hooks on `PlayerMovement.DeadUpdate`, `BodyPartHit` and `WearHat`, `RunawayTrophy.FixedUpdate`, `PropHat.FixedUpdate`, the camp morning's coroutine and `BreakVase.CheckCollision`, made the first time it's switched on: falls, the wind and a camp's night don't take what he holds or his hat, and a vase in his hand doesn't break; swaps go ahead |
| `World/HoldRules.cs` | Hold tight's rule, game-free and checked offline: which cause keeps what, swaps and things gone from the world let go, the config value |
| `World/GrippyFeet.cs` | The Grippy feet cheat: Harmony hooks on `PlayerMovement.GetGrounded`, `HipForces`, `GetPolySupport`, `DeadCheck`, `Die` and `BehaviourPuppet.SetState`, made the first time it's switched on: planted feet don't slide or tire, his weight is counted over his feet, and falls from losing his footing don't happen while a foot is down |
| `World/SupportPolygon.cs` | The outline of his planted feet, game-free and checked offline: the game's own inside and nearest-point tests, and where Grippy feet counts his weight |
| `World/ExtremeGrip.cs` | Grippy feet's second level, Extreme grip: Harmony hooks on `PlayerMovement.GroundedCheck`, `GetGrounded`, `PlantedFootRotation` and `CactusDie`, made the first time it's switched on: a foot put down against anything (a wall, a cliff face) is planted right there, with its pivot at the bottom of the foot, until it's lifted |
| `World/FootGrip.cs` | Extreme grip's rule for one foot, game-free and checked offline: when it takes hold, keeps hold and lets go |
| `World/GymGuard.cs` | Harmony hook on `JesseGym.OnTriggerEnter`: during a trip and 1.5 s after, a gym's time trial isn't started over by Nate's body coming back on in its room |
| `World/SaveSync.cs` | Follows the game's saves (`SaveGod`): loads the marks when a save loads, writes them when the game saves |
| `World/GameLinks.cs` | Ticks off POIs the save records as found (and The Greatest by the gym's best time), and moves movable items' POIs to the real objects (it keeps their loaders), saying when the game moves one |
| `World/Summon.cs` | Brings a movable item to Nate: moves the loaded item (or its loader and its place in the save), puts it down standing, still and in sight of him |
| `World/SummonSpot.cs` | Where a summoned item goes: open, level ground beside Nate, from the game's physics (or a test's made-up world) |
| `UI/FullMapScreen.cs` | The full map: pan/zoom (crosshair or mouse), picking and labelling the POI or saved location under the crosshair or pointer, tracking, marking completed, saving, going to, forgetting and renaming locations, pins with their numbers or initials (a font fitted to each), bringing items to Nate (held buttons), Nate, header and footer; opens the Hero's Journey and the rename box over it |
| `UI/RenameBox.cs`, `UI/NameEntry.cs` | Naming a location saved on the map: the box over the map (keyboard typing, the pad's and mouse's input, drawing), and its game-free half (the name, Shift, the on-screen keyboard's layout and moves) |
| `UI/JourneyScreen.cs` | The Hero's Journey: the ending's set, camera, pen and pace, plus pause, rewind/fast-forward, looking about, zoom |
| `UI/MinimapRenderer.cs` | Top-right minimap (help level 2+) and the tracking label |
| `UI/HudArrow.cs` | Arrow styles 0 and 1: marker over the target, or a compass at the top of the screen |
| `UI/HeadArrow3D.cs` | Arrow style 2: a 3D arrow above Nate |
| `UI/HelpScreen.cs`, `UI/Toasts.cs` | The guide pages, and short status messages |
| `UI/CheatBadge.cs` | The "GRIPPY FEET" (or "EXTREME GRIP") and "HOLD TIGHT" tags in the top-left corner while those cheats are on |
| `UI/GuiTextures.cs`, `UI/Theme.cs`, `UI/Ui.cs` | Procedural textures, the parchment look, resolution scaling |
| `Input/Gamepad.cs`, `Input/Controls.cs` | XInput reader (the triggers count as buttons); bindings as "pad button + keys" |
| `Input/Pointer.cs` | The mouse on the map: unlocks the cursor and restores the game's on close, click vs drag, wheel, and whether the mouse or the crosshair is in use (stick or pan keys switch back; buttons don't). The OS cursor stays hidden; the map draws its own pointer. The map won't open over the game's own menu (`Menu.paused`), and closes if that menu opens |
| `Util/MiniJson.cs`, `Util/KeepAlive.cs`, `ModConfig.cs`, `ModPaths.cs` | JSON reader, keeping Il2Cpp objects alive, settings (Grippy feet's level and Hold tight included), file locations |
| `Util/RenamedFiles.cs` | Files kept under the mod's old name (`BabyStepsMinimap_...`) take the new one, once (game-free, checked offline) |
| `Util/MemoryDiagnostics.cs` | Leak hunting: periodic `[mem]` reports of process, game GC, Unity, FMOD and object counts, the mod's own activity and its own allocations on the game's heap |
| `Util/GcWatchdog.cs` | Asks the game's GC for a collection when it has stopped collecting by itself (see Memory) |
| `Util/Fast.cs`, `Util/Frame.cs`, `UI/Gfx.cs` | Allocation-free maths, vectors, rects, colours, turning matrices (in place of `RotateAroundPivot`) and label text; time, frame count and screen size read once a frame |

Files the mod writes: `UserData/CabbySteps_config.json` and
`UserData/CabbySteps_locations.json`, plus
`CabbySteps_completed<slot>.json` in the game's save folder (see above). Before the mod was
CabbySteps they were named `BabyStepsMinimap_...`; the mod gives such a file the new name the first
time it wants it, if there's none with the new name yet (`Util/RenamedFiles.cs`). It moves the file
rather than copying it, so it happens once: a copy left behind would be taken again after a new
game retires a slot's marks. Older versions also wrote
`UserData/BabyStepsMinimap_calibration.json` and `UserData/BabyStepsMinimap_trail.txt`; they're
no longer read, and the mod logs that they can be deleted.

## History

The original is Slylos's [Baby Steps MiniMap and QOL Features](https://www.nexusmods.com/babysteps/mods/14),
1.2 on Nexus Mods (its `MelonInfo` says 0.6.0). Its DLL (`reference/BabyStepsMinimap.dll`, sha256
`c91e570f…f75f0f`) was
decompiled with ILSpy and cleaned up by hand: real names, named constants, the
original file layout and member order. Every step was checked by IL comparison.
Compiled with Roslyn 4.14 (the compiler family the original was built with), all 27
types were **IL-identical** to the shipped DLL. That source is kept in
`reference/original-source/` as the readable reference for what 0.6.0 does.
`make il-diff` shows how the current build differs from it. Neither is in this repo: they're
Slylos's work (see [Layout](#layout)).

The redesign then removed calibration (the R3 screen, map pins, nudge and heading
trim, the confirm dialogs) in favour of the fixed projection above. It also fixed
what that baseline got wrong:
- Calibration pins taken one world loop away were never wrapped, so a single such
  pin wrecked the fit. One user's "Pedometer" pin sat exactly 512 m off.
- The calibration screen recorded duplicate pins, and the reported RMS included
  the manual nudge.
- The map wrapped in pixel space over the parchment's 767 px width, but one loop
  is about 661 px of the drawing.
- The minimap and 3D arrow weren't loop-aware.
- On the map, A and B leaked into the pin confirm dialogs.
- The camera lookup was meant to be throttled but wasn't.

Later, the mouse on the map, completed POIs (kept with each game save, and ticked
off from the game's own records), movable items and saved locations (with a quick save, and names given on the map) were added. The follower teleport
(L3 + R3, or G, which warped imprinted ducklings to Nate) was removed. L3 now
switches to first person as soon as it's clicked, with no wait for a possible R3. Bringing
items to Nate from the map took its place, for every movable item, chicks and ducklings
included.

Later still, Map Genie's map image and the thin-plate spline fitted to it (from world x and z
only) gave way to the game's own painted sheets and the game's own projection, height and all
(see [How positioning works](#how-positioning-works)). The shipped files went from 32 MB to
about 80 KB, and the map's memory from well over 100 MB to 34 MB of compressed textures.

Then the mod got its own name, CabbySteps (`CabbySteps.dll`, `Mods/CabbySteps/`,
`CabbySteps_*` files; see [Changes from the original](#changes-from-the-original)).

The startup log line still reads `CabbySteps 0.5.0 ready.` while `MelonInfo`
says 0.6.0; both version numbers are as the original shipped them.
