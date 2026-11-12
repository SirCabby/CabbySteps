[size=6][b]CabbySteps[/b][/size]

A [b]map, minimap and more[/b] for [b]Baby Steps[/b]: the game's own painted map with Nate on it exactly where he stands, places that tick themselves off as you play, saved locations and a quick save to send Nate back to, hats, items and ducklings brought to him on request, his Hero's Journey painted the way the game's ending paints it, and a few cheats for when the footing gets silly.

CabbySteps is based on [url=https://www.nexusmods.com/babysteps/mods/14]Baby Steps MiniMap and QOL Features[/url] by [url=https://www.nexusmods.com/profile/Slylos?gameId=8244]Slylos[/url]. The full map, the minimap, tracking with guide arrows, the help levels, first person and the Hero's Journey all started there - thank you, Slylos! What has changed since is listed below.

This mod is open source! Check it out at [url=https://github.com/SirCabby/CabbySteps]https://github.com/SirCabby/CabbySteps[/url]

[size=5][b]What's new compared with the original[/b][/size]
[b]The map[/b]
[list]
[*][b]The game's own map[/b] - the map and the minimap are the six painted map sheets the game's ending shows, loaded from your copy of the game, in place of Map Genie's map image. Nothing of the game's comes with the mod, and its map data went from about 32 MB to about 80 KB.
[*][b]No calibration[/b] - Nate is put on the map the way the game itself does it (the maths it uses to paint his journey), height and all, so the marker is where he is. The calibration screen, its pins, the nudge and the heading trim are gone.
[*][b]Places from the game[/b] - hats and items sit where the game keeps them, cutscenes at their triggers, camps at their campfires, read from the game's own files; the rest come from Map Genie's list. Hats, items, chicks and ducklings are shown where they really are, and followed as they move.
[*][b]The mouse on the map[/b] - hover to see what a point is, click to track it, right-click to tick it off, drag to pan (the wheel zooms, as before). The map draws its own pointer, so it works with a grabbed cursor too.
[*]The world's east-west loop is handled everywhere, the minimap and the 3D arrow included, and the map won't open over the game's own menu.
[/list]
[b]Progress[/b]
[list]
[*][b]Completed points, kept with each save slot[/b] - most tick themselves off from the game's own records: fruit, cutscenes, payphone calls, stacks of cans, trophy hand-ins, a hat once Nate has dreamt about it, The Greatest by your best time in the gym. Tick the rest off by hand. R3 (or H) hides what's done, labels count each kind, and a new game starts with none.
[/list]
[b]Getting around[/b]
[list]
[*][b]Saved locations[/b] - as many as you like, saved and named on the map, plus a [b]quick save[/b] (F5) and [b]quick load[/b] (F9). Nate comes back facing the same way and standing exactly as he stood, with what he holds and wears. A trip waits for the place to load, so he isn't flung on arrival, and a gym's time trial keeps running through it.
[*][b]Bring things to Nate[/b] - hold Y on any hat, key or quest item, chick or duckling on the map. This takes the place of the follower teleport (L3 + R3, or G).
[*]L3 switches to first person at once (it no longer waits to see whether R3 follows).
[/list]
[b]Hero's Journey[/b]
[list]
[*]It's the game's own record of where Nate has been (each save its own, from the save's start), painted dot by dot onto the painted sheets the way the game's ending paints it, with pause, rewind, fast-forward and zoom. It replaces the mod's own trail recording (and F8's clearing it).
[/list]
[b]Cheats (new)[/b]
[list]
[*][b]Grippy feet[/b] (F7) - planted feet don't slip, and Nate keeps his balance while a foot is down. Press F7 again for [b]Extreme grip[/b]: a foot put down against a wall holds there, so he can walk up it.
[*][b]Hold tight[/b] (F8) - falls, the wind and camp mornings can't take what he holds or his hat; swapping still works.
[/list]
[b]Fixes[/b]
[list]
[*][b]Memory[/b] - long sessions no longer run out of memory and crash: the mod's drawing no longer leaves garbage on the game's heap every frame (it was hundreds of MB a minute, gigabytes with the map open), and the mod gets the game's own garbage collector going again if that stalls.
[*]What calibration got wrong (pins taken one world loop away, duplicate pins, wrapping the map in pixels) went with it.
[/list]

[size=5][b]Requirements[/b][/size]
[list]
[*]Baby Steps on Steam
[*][url=https://github.com/LavaGang/MelonLoader/releases]MelonLoader[/url] v0.7 or later - run its installer, point it at [i]BabySteps.exe[/i], then launch the game once so it finishes setting up, and close it.
[/list]

[size=5][b]Install[/b][/size]
[list]
[*][b]Vortex[/b]: install this archive and deploy. It maps the Mods and UserData folders into the game.
[*][b]By hand[/b]: extract the archive into your Baby Steps folder (the one with [i]BabySteps.exe[/i]; in Steam, right-click the game > Manage > Browse local files) and merge, so that you have:
[/list]
[code]<Baby Steps>\Mods\CabbySteps.dll
<Baby Steps>\Mods\CabbySteps\pois.json (and journey.json)
<Baby Steps>\UserData\CabbySteps_config.json[/code]
Launch the game normally. A one-time guide appears the first time you open the map; reopen it any time with D-pad Right (or / on the keyboard).

[size=5][b]Upgrading from Baby Steps MiniMap and QOL Features[/b][/size]
The original (and this mod, before it had its own name) is [i]BabyStepsMinimap.dll[/i]. With the game closed, delete:
[list]
[*][b]Mods\BabyStepsMinimap.dll[/b] - left in, it runs too, with a second map on the same buttons (CabbySteps warns you if it finds it).
[*][b]Mods\BabyStepsMinimap\[/b] - the old data folder, Map Genie's map images included: none of it is read now.
[*][b]UserData\BabyStepsMinimap_calibration.json[/b] and [b]UserData\BabyStepsMinimap_trail.txt[/b], if you have them: they're no longer used.
[/list]
Your settings, saved locations and completed points come along by themselves: the first time the mod needs one, it gives the old file (BabyStepsMinimap_...) its new name (CabbySteps_...). To keep your old settings, don't copy the archive's UserData folder when you upgrade.

[size=5][b]Controls[/b][/size]
[code]Xbox controller, while playing
  View/Select ............ open / close the full map
  D-pad Left/Right ....... help level 1 / 2 / 3
  D-pad Up ............... the guide arrow's style
  L3 (left-stick click) .. first-person view on / off

On the full map
  Left stick ............. move the cursor    D-pad Up/Down ... zoom
  A ...................... track the point under the cursor
  Y ...................... mark it completed (again to unmark)
  R3 ..................... hide / show completed points
  B ...................... clear tracking
  X ...................... bring the cursor back to Nate
  L3 ..................... save where Nate is (a new numbered location)
  LT / RT ................ previous / next saved location
  hold Y on a location ... go there
  hold X on a location ... forget it
  hold A on a red pin .... rename it
  hold Y on a hat, item,
    chick or duckling .... bring it to Nate
  LB / RB ................ smaller / bigger UI
  D-pad Left ............. watch your Hero's Journey
  D-pad Right ............ the in-game guide

Keyboard
  M map, [ and ] help level, H arrow style, F first person,
  F5 quick save, F9 quick load, F7 Grippy feet (off, on, extreme),
  F8 Hold tight (on, off)
  On the map: WASD or arrows pan, PgUp/PgDn zoom, Enter track,
  Space mark completed, H hide/show completed, Backspace clear,
  C find Nate, J journey, / guide, F5 save a location, Q/E previous or
  next location, hold Space on one to go there, hold Delete to forget
  it, F2 rename it, hold Space on a hat, item, chick or duckling to
  bring it to Nate

Mouse, on the map
  hover: what it is, click: track it, right-click: mark it completed,
  drag: pan, wheel: zoom. Hold the right button on a location to go
  there, or on a hat, item, chick or duckling to bring it to Nate.[/code]
Help level 1 = map only, 2 = + minimap, 3 = + a guide arrow to what you're tracking: a marker over it, a compass at the top of the screen, or a 3D arrow above Nate.

[size=5][b]Good to know[/b][/size]
[list]
[*]Completed points belong to your save: each save slot has its own, a new game starts with none, and they're written when the game saves (quit before it saves again and the newest ticks go, just like the game's own progress). Points of Interest and Graffiti are landmarks, there for reference.
[*]Hats count once Nate has dreamt about them (wear one to sleep at a campfire). Hats, items and ducklings can't be ticked off by hand; the Pacifier and the Winter Hat have no dream, so they never count.
[*]The cheats show a small tag in the top-left corner while they're on, and stay as you left them (next time you play too). They have no controller button.
[*]Saved locations are the same in every save slot ([i]UserData\CabbySteps_locations.json[/i]).
[/list]

[size=5][b]If it does not work[/b][/size]
The mod says what it found and did in [b]MelonLoader\Latest.log[/b] in the game's folder (the lines from CabbySteps). Send that log with a report. If the game can't hand over its map sheets (after a game update, say), the map is plain paper, with everything still in its place.

[size=5][b]Credits[/b][/size]
[list]
[*][b]Original mod[/b]: [url=https://www.nexusmods.com/babysteps/mods/14]Baby Steps MiniMap and QOL Features[/url] by [url=https://www.nexusmods.com/profile/Slylos?gameId=8244]Slylos[/url], which CabbySteps grew from.
[*][b]Map[/b]: Baby Steps' own painted map sheets, loaded from your copy of the game.
[*][b]Places[/b]: Map Genie's list of locations (all rights theirs).
[*][b]Mod loader[/b]: [url=https://github.com/LavaGang/MelonLoader]MelonLoader[/url] (LavaGang).
[/list]
