using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2Cpp;
using UnityEngine;
using UnityEngine.Playables;

namespace CabbySteps;

/// <summary>
/// Ties POIs to the running game, using the links in pois.json (tools/georef/find_links.py).
/// All three read the save in memory, so they always match whichever save is loaded.
/// <list type="bullet">
/// <item>Found: POIs the save records as done are ticked off by themselves. That covers fruit
/// and cutscenes seen, payphone calls, cans knocked over, nights at camp and trophy achievements.
/// A cutscene is ticked off as soon as it starts: the game only records it when its timeline ends,
/// and the conversations Nate walks on through (the "Onward and Upward" mornings) carry on for
/// minutes after he can move, longer with the map open (it pauses the game).</item>
/// <item>Movable: hats, items, the ice cream and ducklings are loaded from BigManagerPrefab/
/// GlobalObjectParent/Savables. Once one has left its starting spot, its POI follows the real
/// object; while the game has it switched off (the ice cream not handed out yet, or melted), or
/// it's back at its spot on the map (the ice cream at Mitchell's cart), the POI stays at its spot.
/// When the game moves one with Nate nowhere near, a toast and the log say so; and the log says
/// where the items are once a save has loaded. Items are never ticked off by themselves: finding
/// one and carrying it about doesn't make it done.</item>
/// <item>Gone: an item that has left the game (a trophy handed in, the vase broken) comes off the map.</item>
/// <item>Best times: The Greatest is done once the gym's best time, which the save keeps as
/// "GymPB" (<c>SaveFile.miscNums</c>), is under the designer's: the game records nothing else of it.
/// The Invisible Gym writes its runs under the same key, so a slower one can replace that best;
/// the POI is marked as well (<see cref="PoiProgress.Keep"/>) to stay completed with the save.</item>
/// </list>
/// </summary>
public class GameLinks
{
	private const float PollInterval = 0.5f;

	/// <summary>How far (m) an object has to be from its starting spot, and from its spot on the map, before its POI follows it.</summary>
	private const float MovedSq = 4f * 4f;

	/// <summary>Within this distance (m) of Nate, a movable item counts as with him.</summary>
	private const float WithNateSq = 3f * 3f;

	/// <summary>
	/// An item that goes this far (m) between two looks, half a second apart, with Nate nowhere
	/// near either end, was moved by the game: Mitchell taking the ice cream back to his cart, say.
	/// </summary>
	private const float JumpSq = 40f * 40f;

	/// <summary>Within this distance (m) of Nate, a jump is his doing: carried on a trip, or brought to him.</summary>
	private const float NearNateSq = 8f * 8f;

	/// <summary>
	/// The game shifts far objects by whole north-south loops (<c>ObjectLooper</c>: 3328 m north and
	/// 1300 m up); a jump of over half of one is that, not a move.
	/// </summary>
	private const float HalfLoopZ = 3328f * 0.5f;

	/// <summary>Where some items start, by loader, for saying so when the game puts one back there.</summary>
	private static readonly Dictionary<string, string> StartNames = new Dictionary<string, string>
	{
		["IceTrophy_Loader"] = "Mitchell's cart",
	};

	private readonly ModContext _ctx;

	private readonly List<Poi> _linked = new List<Poi>();

	/// <summary>POIs done once a best time the save keeps is under their time (The Greatest).</summary>
	private readonly List<Poi> _bestPois = new List<Poi>();

	/// <summary>The best times the save keeps (<c>SaveFile.miscNums</c>), by key, as last read: NaN when it has none.</summary>
	private readonly Dictionary<string, float> _bests = new Dictionary<string, float>();

	private readonly string[] _bestKeys;

	/// <summary>Item loaders by object name. Created in Poll, so a game update that breaks the type can't stop the mod starting.</summary>
	private Dictionary<string, GlobalObjectLoader> _loaders;

	/// <summary>Some item's loader wasn't found at the last search.</summary>
	private bool _missing;

	/// <summary>Each movable item as last seen this save, by POI id: gone from here while it's switched off or has no loader.</summary>
	private readonly Dictionary<long, Sighting> _sightings = new Dictionary<long, Sighting>();

	/// <summary>Where items are is to be logged once this save has settled.</summary>
	private bool _sightingsDue;

	/// <summary>Items seen moved by the game in this poll, said once it's done (see <see cref="ReportJumps"/>).</summary>
	private readonly List<Jump> _jumps = new List<Jump>();

	private HashSet<long> _found = new HashSet<long>();

	private HashSet<long> _nextFound = new HashSet<long>();

	private HashSet<long> _gone = new HashSet<long>();

	private HashSet<long> _nextGone = new HashSet<long>();

	private IntPtr _savePtr;

	// The save's "dialogs played" set as it was at the last check of the keys. A key only appears
	// when the set grows, or with another save: checking 120-odd keys twice a second regardless
	// handed the game 250 strings a second.
	private IntPtr _keysSave;

	private IntPtr _keysSet;

	private int _keysCount = -1;

	private float _nextStaleCheck;

	/// <summary>Every found key that is a cutscene's timeline name, or might be: all of them.</summary>
	private readonly HashSet<string> _foundKeys = new HashSet<string>();

	/// <summary>
	/// Found keys of cutscenes that have started since the save loaded. The game records a cutscene
	/// (<c>BBConvoStarter.OnEnd</c> adds its timeline's name) only when the timeline stops.
	/// </summary>
	private readonly HashSet<string> _seen = new HashSet<string>();

	/// <summary>
	/// The cutscene the game was last seen playing (<c>Menu.playingCutscene</c>), by pointer. Kept
	/// through loads: only a change to another one is a cutscene starting.
	/// </summary>
	private IntPtr _playing;

	/// <summary>A recheck is due: one didn't finish, or a cutscene started.</summary>
	private bool _recheckWanted;

	/// <summary>Polls since the save object last changed; changes right after a load aren't announced.</summary>
	private int _pollsSinceLoad;

	private float _nextPoll;

	private float _nextLoaderSearch;

	private int _errors;

	public GameLinks(ModContext ctx)
	{
		_ctx = ctx;
		foreach (Poi poi in ctx.Map.Pois)
		{
			if (poi.Found != null || poi.Gone != null || poi.Item != null || poi.BestKey != null)
			{
				_linked.Add(poi);
			}
			if (poi.Found != null)
			{
				_foundKeys.UnionWith(poi.Found);
			}
			if (poi.BestKey != null)
			{
				_bestPois.Add(poi);
				_bests[poi.BestKey] = float.NaN;
			}
		}
		_bestKeys = new List<string>(_bests.Keys).ToArray();
	}

	/// <summary>
	/// A movable item's loader by its object name (a POI's <see cref="Poi.Item"/>), or null when the
	/// game has none: no save loaded yet, or a game update renamed it.
	/// </summary>
	public GlobalObjectLoader Loader(string name)
	{
		if (_loaders == null || name == null || !_loaders.TryGetValue(name, out GlobalObjectLoader loader))
		{
			return null;
		}
		return loader;
	}

	/// <summary>Is the movable item for this POI with Nate (carried or worn)?</summary>
	public bool WithNate(Poi poi)
	{
		if (poi == null || !poi.Live || !_ctx.Player.Found)
		{
			return false;
		}
		Vector3 d = WorldLoop.Delta(_ctx.Player.WorldPos, poi.World);
		return d.x * d.x + d.y * d.y + d.z * d.z < WithNateSq;
	}

	public void Update()
	{
		float now = Frame.Now;
		if (now < _nextPoll || _linked.Count == 0)
		{
			return;
		}
		_nextPoll = now + PollInterval;
		try
		{
			Poll(now);
		}
		catch (Exception ex)
		{
			// Most likely a game update changed these types; POIs then keep their map spots.
			if (_errors++ < 3)
			{
				_ctx.Log.Warning("Can't read the game's progress, so nothing is ticked off automatically: " + ex.Message);
			}
		}
	}

	// Out of line so that a game update breaking these interop types fails inside Update's try.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void Poll(float now)
	{
		SaveFile save = SaveGod.theSave;
		if (save == null)
		{
			Reset();
			return;
		}
		if (save.Pointer != _savePtr)
		{
			_savePtr = save.Pointer;
			_pollsSinceLoad = 0;
			// A save just loaded has only its own records: cutscenes seen before it don't count. (The
			// game may still point at the last one from before: only a new one starting counts.)
			_seen.Clear();
			// Nor do its items' moves: they were in another save.
			_sightings.Clear();
			_sightingsDue = true;
		}
		_pollsSinceLoad++;
		FindLoaders(now);
		var keys = save.dialogsPlayed;
		IntPtr keysSet = (keys != null) ? keys.Pointer : IntPtr.Zero;
		int keysCount = (keys != null) ? keys.Count : -1;
		bool recheck = _recheckWanted || save.Pointer != _keysSave || keysSet != _keysSet || keysCount != _keysCount;
		if (WatchCutscene())
		{
			recheck = true;
		}
		if (ReadBests(save))
		{
			recheck = true;
		}
		if (recheck)
		{
			// Until this recheck has got through every POI: one that fails is tried again next time.
			_recheckWanted = true;
			_nextFound.Clear();
			_nextGone.Clear();
		}
		foreach (Poi poi in _linked)
		{
			bool gone = recheck ? (poi.Gone != null && AnyRecorded(poi.Gone)) : _gone.Contains(poi.Id);
			if (gone)
			{
				if (recheck)
				{
					_nextGone.Add(poi.Id);
				}
				if (poi.Live)
				{
					poi.GoHome();
				}
				_sightings.Remove(poi.Id);
				continue;
			}
			if (poi.Item != null && _loaders.TryGetValue(poi.Item, out GlobalObjectLoader loader) && loader != null)
			{
				Follow(poi, loader);
			}
			else
			{
				if (poi.Live)
				{
					poi.GoHome();
				}
				_sightings.Remove(poi.Id);
			}
			if (recheck && ((poi.Found != null && (AnySeen(poi.Found) || AnyRecorded(poi.Found))) || (poi.BestKey != null && Beaten(poi))))
			{
				_nextFound.Add(poi.Id);
			}
		}
		// Otherwise nothing in the save's records changed since the last check.
		if (recheck)
		{
			_keysSave = save.Pointer;
			_keysSet = keysSet;
			_keysCount = keysCount;
			_recheckWanted = false;
			bool foundChanged = !_nextFound.SetEquals(_found);
			bool goneChanged = !_nextGone.SetEquals(_gone);
			if (foundChanged || goneChanged)
			{
				Announce();
				(_found, _nextFound) = (_nextFound, _found);
				(_gone, _nextGone) = (_nextGone, _gone);
				_ctx.Progress.SetFound(_found);
				_ctx.Progress.SetGone(_gone);
				if (_ctx.Target != null && _ctx.Progress.IsGone(_ctx.Target))
				{
					_ctx.Target = null;
				}
			}
		}
		// Only now is _found this save's: a best time's POI is marked too, to stay completed with it.
		foreach (Poi poi in _bestPois)
		{
			if (_found.Contains(poi.Id))
			{
				_ctx.Progress.Keep(poi, (long)save.Pointer);
			}
		}
		if (_jumps.Count > 0)
		{
			ReportJumps();
		}
		// A couple of seconds in, once the game has put its items where the save has them.
		if (_sightingsDue && _pollsSinceLoad >= 4)
		{
			_sightingsDue = false;
			LogSightings();
		}
	}

	/// <summary>
	/// Reads the best times the save keeps (one call into the game per key); true if one has changed
	/// since the last read. A new best replaces the old entry, so the dictionary's size doesn't show it.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool ReadBests(SaveFile save)
	{
		if (_bestKeys.Length == 0)
		{
			return false;
		}
		var nums = save.miscNums;
		bool changed = false;
		foreach (string key in _bestKeys)
		{
			float best = (nums != null && nums.TryGetValue(key, out float value)) ? value : float.NaN;
			float last = _bests[key];
			if (best == last || (float.IsNaN(best) && float.IsNaN(last)))
			{
				continue;
			}
			_bests[key] = best;
			changed = true;
			if (float.IsNaN(best))
			{
				continue;
			}
			foreach (Poi poi in _bestPois)
			{
				if (poi.BestKey == key)
				{
					_ctx.Log.Msg($"{poi.Title}: the save's best time {key} is {best:0.00} s, {(best < poi.BestUnder ? "under" : "not under")} the {poi.BestUnder:0.##} s it takes.");
				}
			}
		}
		return changed;
	}

	/// <summary>Is the POI's best time, as last read, under its time? (Never when the save has none.)</summary>
	private bool Beaten(Poi poi)
	{
		return _bests.TryGetValue(poi.BestKey, out float best) && best < poi.BestUnder;
	}

	/// <summary>Has a cutscene recorded under one of these keys started since the save loaded?</summary>
	private bool AnySeen(string[] keys)
	{
		if (_seen.Count == 0)
		{
			return false;
		}
		foreach (string key in keys)
		{
			if (_seen.Contains(key))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Has a POI's cutscene just started? Every cutscene starts through
	/// <c>BBConvoStarter.PlayCutscene</c>, which hands the game its director
	/// (<c>Menu.OnCutscene</c> sets <c>Menu.playingCutscene</c>); the timeline's name is what the
	/// game records once it ends. True if that's a POI's key not seen yet. Only a change of director
	/// is looked into: naming its timeline is a call into the game.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private bool WatchCutscene()
	{
		IntPtr pointer = PlayingPointer(out PlayableDirector playing);
		if (pointer == _playing)
		{
			return false;
		}
		_playing = pointer;
		if (pointer == IntPtr.Zero)
		{
			return false;
		}
		string name;
		try
		{
			PlayableAsset timeline = playing.playableAsset;
			name = ReferenceEquals(timeline, null) ? null : timeline.name;
		}
		catch (Exception)
		{
			// A director gone with its scene.
			return false;
		}
		return name != null && _foundKeys.Contains(name) && _seen.Add(name);
	}

	/// <summary>The director of the cutscene the game last started, and its pointer (zero if none).</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static IntPtr PlayingPointer(out PlayableDirector playing)
	{
		// Plain reference checks: fields read straight from the game's objects, no calls into it.
		Menu menu = Menu.me;
		playing = ReferenceEquals(menu, null) ? null : menu.playingCutscene;
		return ReferenceEquals(playing, null) ? IntPtr.Zero : playing.Pointer;
	}

	/// <summary>Is any of these keys in the save's "dialogs played" set?</summary>
	private static bool AnyRecorded(string[] keys)
	{
		foreach (string key in keys)
		{
			MemoryDiagnostics.DialogKeyChecks++;
			if (SaveGod.HasDialogKey(key))
			{
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Move the POI to its object once the object has left its starting spot. An object the game
	/// has loaded but switched off isn't out in the world: the ice cream before Mitchell hands it
	/// over or once it has melted (he has the next one), or an item used up for good. Its POI shows
	/// where it's to be had. So does an object at its own spot on the map: the ice cream's loader
	/// starts somewhere else, but it's had at Mitchell's cart.
	/// </summary>
	private void Follow(Poi poi, GlobalObjectLoader loader)
	{
		// The loaded object while it's near; the loader holds its last spot while it's not.
		Transform loaded = loader.loadedTransform;
		bool isLoaded = loaded != null;
		Vector3 live;
		if (isLoaded)
		{
			GameObject instance = loader.loadedInstance;
			if (!ReferenceEquals(instance, null) && !instance.activeSelf)
			{
				if (poi.Live)
				{
					poi.GoHome();
				}
				_sightings.Remove(poi.Id);
				return;
			}
			live = loaded.position;
		}
		else
		{
			live = loader.transform.position;
		}
		bool moved = !Within(loader.defPos, live, true);
		bool isOut = moved && !Within(poi.HomeWorld, live, poi.HomeHasHeight);
		Sight(poi, live, isLoaded, isOut, moved);
		if (!isOut)
		{
			if (poi.Live)
			{
				poi.GoHome();
			}
			return;
		}
		poi.World = Fast.V3(WorldLoop.WrapX(live.x), live.y, live.z);
		poi.HasHeight = true;
		Vector2 px = _ctx.Projection.WorldToMap(live);
		poi.Px = px.x;
		poi.Py = px.y;
		poi.Live = true;
	}

	/// <summary>Is <paramref name="at"/> within <see cref="MovedSq"/> of <paramref name="spot"/> (across, when the spot's height isn't known)?</summary>
	private static bool Within(Vector3 spot, Vector3 at, bool hasHeight)
	{
		Vector3 d = WorldLoop.Delta(spot, at);
		return d.x * d.x + (hasHeight ? d.y * d.y : 0f) + d.z * d.z < MovedSq;
	}

	/// <summary>Is <paramref name="at"/> close enough to Nate for a jump there or from there to be his doing?</summary>
	private bool NearNate(Vector3 at)
	{
		Vector3 d = WorldLoop.Delta(_ctx.Player.WorldPos, at);
		return d.x * d.x + d.y * d.y + d.z * d.z < NearNateSq;
	}

	/// <summary>
	/// Note where an item is, and say so when the game has moved one that was out: a jump of over
	/// 40 m in half a second, nowhere near Nate. Mitchell's stand puts the ice cream back at his
	/// cart (<c>IceTrophyHelper</c> moves its loader there, and a fresh load from a save hangs from
	/// its loader); a lost hat may go back where it starts. Carrying an item on a trip, or bringing
	/// one to Nate, lands it by him, so that isn't counted.
	/// </summary>
	private void Sight(Poi poi, Vector3 at, bool loaded, bool isOut, bool moved)
	{
		if (_sightings.TryGetValue(poi.Id, out Sighting last) && last.Out && _ctx.Player.Found)
		{
			Vector3 d = WorldLoop.Delta(last.At, at);
			float distSq = d.x * d.x + d.y * d.y + d.z * d.z;
			if (distSq > JumpSq && Fast.Abs(d.z) < HalfLoopZ && !NearNate(last.At) && !NearNate(at))
			{
				_jumps.Add(new Jump { Poi = poi, From = last.At, To = at, Distance = Fast.Sqrt(distSq), BackAtStart = !isOut, Loaded = loaded });
			}
		}
		_sightings[poi.Id] = new Sighting { At = at, Loaded = loaded, Out = isOut, Moved = moved };
	}

	/// <summary>
	/// Say what the game has moved: a log line and a toast. Several at once is the game putting its
	/// whole world back (<c>Menu.RestartScene</c> returns every loader to its start before loading
	/// another save), so is the scene restarting; neither gets a toast.
	/// </summary>
	private void ReportJumps()
	{
		bool restart = _jumps.Count > 1 || RestartingScene();
		foreach (Jump jump in _jumps)
		{
			Poi poi = jump.Poi;
			string what;
			if (jump.BackAtStart)
			{
				what = "put it back " + ((poi.Item != null && StartNames.TryGetValue(poi.Item, out string start)) ? "at " + start : "where it starts");
			}
			else
			{
				what = $"moved it {Fast.RoundToInt(jump.Distance)} m";
			}
			_ctx.Log.Msg($"{poi.Title}: moved {jump.Distance:0} m from {Place(jump.From)} to {Place(jump.To)}{(jump.Loaded ? "" : " (not loaded)")}, with Nate nowhere near: "
				+ (restart ? $"the game is putting its world back ({_jumps.Count} items at once)." : $"the game {what}."));
			if (!restart)
			{
				_ctx.Toasts.Show($"{poi.Title}: the game {what}");
			}
		}
		_jumps.Clear();
	}

	/// <summary>Is the game restarting its scene (to load another save)?</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool RestartingScene()
	{
		Menu menu = Menu.me;
		return !ReferenceEquals(menu, null) && menu.restartingScene;
	}

	/// <summary>
	/// Where the save's items are, once it has settled: those away from where the game first puts
	/// them, whether the game has them loaded, and those back at their spot on the map (the ice
	/// cream at Mitchell's cart).
	/// </summary>
	private void LogSightings()
	{
		var text = new System.Text.StringBuilder();
		int count = 0;
		foreach (Poi poi in _linked)
		{
			if (poi.Item == null || !_sightings.TryGetValue(poi.Id, out Sighting sighting) || !sighting.Moved)
			{
				continue;
			}
			text.Append((count++ == 0) ? "" : "; ").Append(poi.Title);
			if (WithNate(poi))
			{
				text.Append(" with Nate");
				continue;
			}
			text.Append(" at ").Append(Place(sighting.At)).Append(sighting.Loaded ? "" : " (not loaded)");
			if (!sighting.Out)
			{
				text.Append(", at its spot on the map");
			}
		}
		_ctx.Log.Msg((count == 0) ? "Items: all where they start." : $"Items away from where they start ({count}): {text}.");
	}

	private static string Place(Vector3 p)
	{
		return $"({p.x:0.0}, {p.y:0.0}, {p.z:0.0})";
	}

	/// <summary>Where a movable item was at the last look, and whether the game had it loaded.</summary>
	private struct Sighting
	{
		public Vector3 At;

		public bool Loaded;

		/// <summary>Out in the world: away from its starting spot and its spot on the map, so its POI follows it.</summary>
		public bool Out;

		/// <summary>Away from where the game first puts it (the loader's <c>defPos</c>).</summary>
		public bool Moved;
	}

	/// <summary>An item the game moved between two looks.</summary>
	private struct Jump
	{
		public Poi Poi;

		public Vector3 From;

		public Vector3 To;

		public float Distance;

		/// <summary>It went back to its starting spot (or its spot on the map).</summary>
		public bool BackAtStart;

		public bool Loaded;
	}

	/// <summary>
	/// The item loaders, found again whenever the scene has been reloaded or one is still
	/// missing. Inactive ones count: some (the winter hat's) start switched off.
	/// </summary>
	private void FindLoaders(float now)
	{
		_loaders ??= new Dictionary<string, GlobalObjectLoader>();
		bool stale = _loaders.Count == 0 || _missing;
		// Whether a scene reload took the loaders with it: asking each is a call into the game, so
		// only every few seconds (a lost loader in between is only skipped by the null check in Poll).
		if (!stale && now >= _nextStaleCheck)
		{
			_nextStaleCheck = now + 5f;
			foreach (GlobalObjectLoader loader in _loaders.Values)
			{
				if (loader == null)
				{
					stale = true;
					break;
				}
			}
		}
		if (!stale || now < _nextLoaderSearch)
		{
			return;
		}
		_loaders.Clear();
		MemoryDiagnostics.LoaderSearches++;
		foreach (GlobalObjectLoader loader in Resources.FindObjectsOfTypeAll<GlobalObjectLoader>())
		{
			// FindObjectsOfTypeAll also returns prefabs; only scene objects are real.
			if (loader != null && loader.gameObject.scene.IsValid())
			{
				_loaders[loader.gameObject.name] = loader;
			}
		}
		_missing = false;
		foreach (Poi poi in _linked)
		{
			if (poi.Item != null && !_loaders.ContainsKey(poi.Item))
			{
				_missing = true;
				break;
			}
		}
		_nextLoaderSearch = now + (_missing ? 15f : 5f);
	}

	/// <summary>A toast for what just happened in play (not for what a save loads with).</summary>
	private void Announce()
	{
		if (_pollsSinceLoad <= 2)
		{
			return;
		}
		string message = null;
		int count = 0;
		foreach (long id in _nextGone)
		{
			if (_gone.Contains(id))
			{
				continue;
			}
			Poi poi = Find(id);
			count++;
			message = poi?.Title + " has left the game - taken off the map";
			if (_ctx.Target != null && _ctx.Target.Id == id)
			{
				_ctx.Target = null;
				message += ", tracking cleared";
			}
		}
		foreach (long id in _nextFound)
		{
			if (_found.Contains(id))
			{
				continue;
			}
			Poi poi = Find(id);
			count++;
			// An item ticks off through its cutscene (a hat's dream), not by being found.
			message = (poi?.Item != null) ? poi.Title + ": cutscene seen - ticked off"
				: (poi?.BestKey != null) ? $"{poi.Title}: best time {_bests[poi.BestKey]:0.00} s, under {poi.BestUnder:0.##} s - ticked off"
				: "Found " + poi?.Title + " - ticked off";
			if (_ctx.Target != null && _ctx.Target.Id == id)
			{
				_ctx.Target = null;
				message += ", tracking cleared";
			}
		}
		if (count > 1)
		{
			message = count + " points ticked off or taken off the map";
		}
		if (message != null)
		{
			_ctx.Toasts.Show(message);
		}
	}

	private Poi Find(long id)
	{
		foreach (Poi poi in _linked)
		{
			if (poi.Id == id)
			{
				return poi;
			}
		}
		return null;
	}

	private void Reset()
	{
		_seen.Clear();
		_sightings.Clear();
		_jumps.Clear();
		_sightingsDue = false;
		foreach (string key in _bestKeys)
		{
			_bests[key] = float.NaN;
		}
		_recheckWanted = false;
		_savePtr = IntPtr.Zero;
		_keysSave = IntPtr.Zero;
		_keysSet = IntPtr.Zero;
		_keysCount = -1;
		if (_found.Count > 0 || _gone.Count > 0)
		{
			_found.Clear();
			_gone.Clear();
			_ctx.Progress.SetFound(_found);
			_ctx.Progress.SetGone(_gone);
		}
		foreach (Poi poi in _linked)
		{
			if (poi.Live)
			{
				poi.GoHome();
			}
		}
	}
}
