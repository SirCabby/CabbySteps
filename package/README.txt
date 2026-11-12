CABBYSTEPS - MAP, MINIMAP & MORE FOR BABY STEPS
===============================================

Nate's life ain't perfect, but at least now you'll know where he's standing.

Based on "Baby Steps MiniMap and QOL Features" by Slylos:
  https://www.nexusmods.com/babysteps/mods/14
(see WHAT'S NEW COMPARED WITH THE ORIGINAL, and CREDITS, at the end).
Source code: https://github.com/SirCabby/CabbySteps

This adds a full-screen map and an optional minimap - the game's own
painted map, the sheets its ending shows - a "you are here" marker,
waypoint tracking with a guide arrow, ticking places off as completed (kept
with your save game), saved locations (named as you like) and a quick save
you can go back to, bringing hats, items and ducklings to Nate, a
first-person toggle, the "Hero's Journey": your path painted onto the
game's own map sheets, the way the game itself paints it, and two cheats:
Grippy feet, for when the footing gets silly (and Extreme grip, for walking
up walls), and Hold tight, so a fall never costs you your hat or what's in
your hands.


REQUIREMENT (install this FIRST)
--------------------------------
* MelonLoader (v0.7+)  -  https://github.com/LavaGang/MelonLoader/releases
  Run the MelonLoader installer, point it at BabySteps.exe, and let it install.
  Launch the game once so MelonLoader finishes setting up, then close it.


INSTALL (two options)
---------------------
Option A - Mod manager (Vortex):
  Just install this archive through Vortex and deploy. It maps the Mods and
  UserData folders into your game automatically.

Option B - Manual:
  Extract this archive into your Baby Steps folder (the one containing
  BabySteps.exe) so that:
     <Baby Steps>\Mods\CabbySteps.dll
     <Baby Steps>\Mods\CabbySteps\pois.json (etc.)
     <Baby Steps>\UserData\CabbySteps_config.json
  Choose "merge/replace" if Windows asks about the Mods / UserData folders.

  Typical path:
     C:\Program Files (x86)\Steam\steamapps\common\Baby Steps
  (or wherever your Steam library lives)


UPGRADING (from Baby Steps MiniMap and QOL Features, or BabyStepsMinimap)
-------------------------------------------------------------------------
This mod used to be BabyStepsMinimap.dll, as was the one it grew from.
Delete these, with the game closed:
  Mods\BabyStepsMinimap.dll     (left in, it runs too: two maps on the same
                                buttons; the mod warns you if it finds it)
  Mods\BabyStepsMinimap\        (the old data folder, Map Genie's map
                                images included: none of it is read now)
  UserData\BabyStepsMinimap_calibration.json and
  UserData\BabyStepsMinimap_trail.txt (if you have them: no longer used)
Your settings, saved locations and completed points come along by
themselves: the first time the mod needs one of them it gives the old file
(BabyStepsMinimap_...) its new name (CabbySteps_...). To keep your old
settings, don't copy this archive's UserData folder when you upgrade.


FIRST LAUNCH
------------
Launch Baby Steps normally. A one-time guide appears the first time you open
the map - reopen it any time with D-pad Right.


CONTROLS (Xbox controller)
--------------------------
  View/Select ............ open / close the full map
  D-pad Left/Right ....... (in-game) change help level 1 / 2 / 3
  D-pad Up ............... (in-game) change the guide arrow's style
  L3 (left-stick click) .. toggle first-person view

  In the full map:
    D-pad Up/Down ........ zoom          Left-stick ... pan cursor
    (put the crosshair on a point to see what it is)
    A .................... track the point under the cursor
    Y .................... mark it completed (press again to unmark)
    R3 ................... hide / show completed points
    B .................... clear tracking
    X .................... bring the cursor back to Nate
    L3 ................... save where Nate is (a new numbered location)
    LT / RT .............. previous / next saved location
    hold Y on a location . go there
    hold X on a location . forget it
    hold A on a red pin .. rename it
    hold Y on a hat, item,
      chick or duckling .. bring it to Nate
    LB / RB .............. smaller / bigger UI
    D-pad Left .......... watch your Hero's Journey
    D-pad Right ......... open the in-game guide

  Mouse, on the map: hover over a point to see what it is, click to
  track it, right-click to mark it completed, drag to pan, scroll to zoom.
  Hold the right button on a saved location to go there, or on a hat,
  item, chick or duckling to bring it to Nate.

  Keyboard: M map, [ ] help level, H arrow style, F first person,
  F5 quick save (one slot: each F5 saves over the last), F9 quick load
  (back to the quick save, any time), F7 Grippy feet: off, on, extreme
  grip, off (a cheat, see below; playing or on the map), F8 Hold tight
  on/off (another cheat, see below; playing or on the map).
  On the map: WASD/arrows pan, PgUp/PgDn zoom,
  Enter track, Space mark completed, H hide/show completed, Backspace
  clear, C find Nate, J journey, / guide, F5 save where Nate is (a new
  numbered location), Q/E previous/next saved location, hold Space on
  one to go there, hold Delete to forget it, F2 to rename a red one,
  hold Space on a hat, item, chick or duckling to bring it to Nate.

  Level 1 = map only   Level 2 = + minimap   Level 3 = + guide arrow


COMPLETED POINTS (kept with your save)
--------------------------------------
Completed points show faded with a green tick, on the map and the minimap,
and the label shows how many of each kind you've done. R3 (or H) hides
them altogether.

Most points tick themselves off, straight from the game's own records:
fruit and cutscenes as soon as they start, payphone calls answered, stacks
of cans knocked over, trophy achievements once the trophy is handed in, The
Greatest once your best time in the gym beats the designer's (78.24 s), and
hats once their cutscene has played (wear one when you sleep at a campfire
and Nate dreams about it). Mark the rest yourself with Y or a right-click.

Points of Interest and Graffiti are landmarks, there for reference: they
can't be ticked off and don't count towards your progress.

Hats, items and the chicks and ducklings move about, so finding or carrying
one doesn't tick it off, and you can't tick them by hand; only a hat's dream
does. Once one has left its starting spot, its point follows it on the map
(the label says "with Nate" while you're carrying or wearing it). A quest
item that leaves the game - a trophy handed in, the vase broken - comes off
the map. The Pacifier and the Winter Hat have no dream, so they never count.

The ticks belong to your save game, not to the mod:
  * each save slot has its own ticks;
  * delete a save and start over, and the new game starts with none;
  * they're written when the game saves (every few seconds while you
    play), so quitting and continuing gives you exactly the ticks you
    had at the game's last save. Tick something and quit before the
    game saves again, and that tick goes, just like the game's own
    progress would.
They live next to the game's save, in
  %USERPROFILE%\AppData\LocalLow\DefaultCompany\Babysteps\<your id>\save\
as CabbySteps_completed0.json (1, 2... for the other slots).


SAVED LOCATIONS
---------------
Save where Nate is standing and send him back there later. He comes back
facing the way he was, with the camera as it was, standing exactly as he
stood - feet on the very spots, hips and all - so a tricky footing holds.
(Places saved with an older version: save them again to get that.)

  * Quick save: F5 while playing. There's just the one quick save, a blue
    "Q" pin: each F5 saves over it, so practising a tricky bit doesn't
    pile up saves. F9, any time - even when Nate's down after a fall -
    takes him straight back to it.
  * Saved locations: L3 on the map (F5 on the keyboard there) saves a new
    numbered location, a red pin - as many as you like, kept until you
    forget them.

Every saved place is a pin on the map and the minimap. Put the crosshair
or the mouse on one, or hop between them with LT/RT (Q/E), then:
  * hold Y (Space, or the right mouse button) to go there: the map closes
    and the game moves Nate, hat and whatever he's holding included;
  * hold X (Delete) to forget it;
  * press A (Enter, or click) to track it like any landmark;
  * hold A (or press F2) on a red one to rename it.
Every trip leaves a grey "Where you were" pin, so you can always go back.
Ducklings don't come along (bring them after him: see below). The game
saves where Nate is as usual, so after a trip your save has him at the
new place.

Renaming opens a box over the map. Type on a keyboard (Backspace deletes,
Enter keeps the name, Esc cancels), or pick letters on its on-screen
keyboard with the D-pad or left stick and A: X deletes, Y is a space, LT is
Shift, RT (or Done) keeps the name, B cancels. The mouse can click the keys
too. A name is up to 32 characters. A named location's pin shows the first
letters of the name's first two words ("UG" for Underwater Gym), and its
number goes to the next location you save; clear the name and it's numbered
again. Named ones come after the numbered ones on LT/RT, A to Z. The quick
save and "Where you were" keep their names.

Trips wait for cutscenes to finish, and don't happen while Nate is asleep
at a campfire or the game's menu is open.

Nate arrives steady. A trip waits until the place has finished loading,
with the ground under his feet there, and he's held still where he is
first, so going while he's tumbling or falling doesn't fling him on
arrival, nor does a long trip that has to load a far-off place. A short
trip waits about a fifth of a second for this, a long one until the place
is in. If he's still knocked over as he arrives, he's put back once more
by himself. What he holds and wears comes with him even if he's knocked
over while the trip waits (the game would have him drop it where he set
off), and anything that slips from his hands on the way is given back to
him there.

A trip doesn't start a gym's time trial over (the Underwater Gym's, say):
F9 back to a quick save on the course and the run carries on, its clock
still running and the checkpoints you'd passed still passed. Walking into
the gym and crossing the start line start it over as usual.

The places are kept in UserData\CabbySteps_locations.json, and are
the same in every save slot. A red one can also be renamed by changing its
"name" there.


HERO'S JOURNEY
--------------
D-pad Left on the map (J on the keyboard) paints Nate's path so far, dot by
red dot, onto the game's own painted map sheets on their wooden board, at
the game's own pace, the view climbing with the pen - just as the game
itself shows it. It's the game's own record of where Nate has been (it notes
his position every few seconds), so each save slot has its own, and it
covers everything since the save was started.

  A (Space, or a click) ........ pause / play (at the end: again)
  L-stick left/right (A/D) ..... rewind / fast-forward
  L or R-stick up/down (W/S),
    or drag with the mouse ..... look up and down the board
  X (C) ........................ follow the pen again
  Y (F) ........................ finish the painting now
  D-pad Up/Down (PgUp/PgDn, wheel) zoom out to the whole board and back
  B (Esc, right-click) ......... back to the map

The paintings are the map's own (see ACCURACY below), loaded from your copy
of the game. If they can't be (after a game update, say), the sheets are
plain paper and the path is still drawn. The mod no longer records its own trail: an old
UserData\BabyStepsMinimap_trail.txt can be deleted.


GRIPPY FEET (A CHEAT)
---------------------
F7 on the keyboard, while playing or on the map, steps it through three
levels: off, Grippy feet, Extreme grip, and off again.

Grippy feet:
  * a planted foot can't slip - mud, ice, a steep slope, a stream: once
    a foot is down it stays exactly where you put it, and it pushes off
    as well as on dry rock;
  * Nate keeps his balance while a foot is down - lean as far as he'll
    go, stand on one leg as long as you like, splay his legs or sink into
    a crouch: he doesn't topple, his legs don't tire or shake, and a
    narrow perch (a pillar top, a post) holds him.
Stepping off an edge into thin air is still a fall (fall fast, tip over
in the air or land hard and he goes down as usual), and cacti and
boulders still get him.

Extreme grip (F7 again) is all of that, and footholds anywhere: put a
foot down while it's pressed against something - a wall, a cliff face,
the side of a rock, a slope too steep to stand on - and it holds right
there, as if on a little ledge. Lift the other foot and Nate stands on it,
so step by step he can walk up a sheer wall (or down one). To climb: lift
a foot as high as it goes, push it into the wall, let go of its trigger,
then lift the other foot, and so on. Pulling a foot's trigger lets that
foot go. A foot put down without touching anything drops to the ground
as usual, and a foot that finds real ground is an ordinary step.

A small GRIPPY FEET (or EXTREME GRIP) tag shows in the top-left corner
while it's on. It stays as you left it - next time you play too - until
F7 turns it off. Turn it off mid-lean and the game's balance takes over
again, so he may topple then; turn it off with a foot on a wall and that
foot comes away. There's no controller button for it.


HOLD TIGHT (A CHEAT)
--------------------
F8 on the keyboard, while playing or on the map, turns it on and off.
While it's on, what Nate holds stays in his hands and his hat stays on:
  * a fall can't shake them loose - tumble down a mountain and he still
    has the key in his hand and the hat on his head; the Baby Trophy
    can't wriggle free to run off, and a vase in his hand doesn't break;
  * the wind can't lift the propeller hat off his head;
  * a night at a camp no longer leaves his hat by the fire in the
    morning.
Swapping still works: pick up something else with a full hand and what
was in it drops, put on another hat and the old one comes off, as usual.
Handing things over to someone, using them up, the ice cream melting and
the story taking the lantern are still the game's.

A small HOLD TIGHT tag shows in the top-left corner while it's on (under
the GRIPPY FEET one if that's on too). It stays as you left it - next
time you play too - until F8 turns it off. There's no controller button
for it.


BRINGING THINGS TO NATE
-----------------------
Lost a chick, or fancy a hat without the climb? On the map, put the cursor
on any hat, key item, quest item, chick or duckling - found or not - and
hold Y (Space, or the right mouse button). The map stays open and the
point jumps to Nate: it's put down on open ground beside him, in front of
him if there's room.

Not while Nate is holding or wearing it (the label says "with Nate"), not
once it has left the game (a trophy handed in), and not during cutscenes,
while Nate sleeps at a campfire or while the game's menu is open.

Things that move by themselves carry on: the Baby Trophy runs off, the
"Falcon" can fly off again, and a chick that hasn't met Nate yet takes to
him as if he'd found it.

The game remembers where things are once Nate has picked them up. Bring
over something he has never touched and load the game again before he
picks it up, and it'll be back where it started, like anything untouched.


ACCURACY (no calibration needed)
--------------------------------
The map is the game's own: the six painted map sheets its ending shows,
loaded from your copy of Baby Steps while you play (nothing of the game's
comes with this mod). If the game can't hand them over (after a game
update, say), the map is plain paper, with everything still in its place.

Nate is put on those sheets exactly the way the game itself does it - it
notes where he goes, to paint his journey at the end. The sheets look down
on the mountain from the south, at an angle, so height counts: a ledge or a
cliff top above Nate is drawn a little higher up the map than him, and
standing under a stack of cans puts him just below its icon. Hats, fruit,
cans, campfires, cutscenes, landmarks and the like are placed the same way,
from where the game has them. The rest of the places come from the list
Map Genie (a community site) made on the very same painted map.

Cutscenes, payphone calls, trophy hand-ins and camps are where the game
itself sets them off (read from the game's own files): a morning cutscene
at the campfire you slept by, the others at their trigger. Where several
land on the same spot, they're spread a little so each can be picked.

There is nothing to set up, and the world's east-west loop is handled for
you: walk off one edge of the map and you come back on the other.


WHAT'S NEW COMPARED WITH THE ORIGINAL
-------------------------------------
CabbySteps grew out of Slylos's "Baby Steps MiniMap and QOL Features" (1.2).
The full map, the minimap, tracking with the guide arrows, the help levels,
first person and the Hero's Journey all started there. Since then:

  The map
  * It's the game's own: the painted map sheets of its ending, loaded from
    your copy of the game, in place of Map Genie's map image (the mod's map
    data went from about 32 MB to about 80 KB).
  * No calibration: Nate is put on the map the way the game itself does
    it, height and all, so the marker is where he is. The calibration
    screen, its pins, the nudge and the heading trim are gone.
  * Hats, items, cutscenes and camps are where the game has them, read from
    its own files; hats, items, chicks and ducklings that move are shown
    where they really are.
  * The mouse works on the map: hover, click to track, right-click to tick
    off, drag to pan (the wheel zooms, as before).
  * The world's loop is handled everywhere, the minimap and the 3D arrow
    included; the map won't open over the game's own menu.

  Progress
  * Completed points, kept with each save slot, most of them ticked off
    from the game's own records (see COMPLETED POINTS); R3 / H hides them.

  Getting around
  * Saved locations, named as you like, plus a quick save (F5) and quick
    load (F9): Nate comes back standing exactly as he stood, with what he
    holds and wears.
  * Bring any hat, item, chick or duckling to Nate from the map. This takes
    the place of the follower teleport (L3 + R3, or G).
  * L3 switches to first person at once.

  Hero's Journey
  * Now the game's own record of where Nate has been, painted the way the
    game's ending paints it (it replaces the mod's own trail, and F8's
    clearing it).

  Cheats (new)
  * Grippy feet / Extreme grip (F7) and Hold tight (F8): see above.

  Fixes
  * Long sessions no longer run out of memory and crash: the mod's drawing
    no longer leaves garbage on the game's heap every frame, and it gets the
    game's own garbage collector going again if that stalls.


CREDITS
-------
  Original mod ..... "Baby Steps MiniMap and QOL Features" by Slylos
                     https://www.nexusmods.com/babysteps/mods/14
                     https://www.nexusmods.com/profile/Slylos?gameId=8244
  CabbySteps ....... SirCabby - https://github.com/SirCabby/CabbySteps
  Map .............. Baby Steps' own painted map sheets, from your copy of
                     the game
  Places ........... Map Genie's list of locations (all rights theirs)
  Framework ........ MelonLoader (LavaGang)

Slylos made the original "so my girlfriend and I would stop losing our
ducklings and getting lost". It grew from there. Enjoy!
