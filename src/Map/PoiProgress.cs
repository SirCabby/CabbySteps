using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using MelonLoader;

namespace CabbySteps;

/// <summary>
/// The POIs the player has marked completed, kept with the game's save.
/// <para>
/// Each save slot has its own file next to the game's save. It is only written from
/// <see cref="Commit"/>, which runs right after the game has written its save, so the marks
/// on disk always match a save the game really made. The file holds snapshots tagged with the
/// game's play time at each save. Whichever save the game then loads (the latest one, a backup,
/// an older one put back by hand) gets the marks as they were at that save, and a new game
/// starts with none.
/// </para>
/// <para>
/// POIs the game itself records as done (fruit or cutscenes seen, hats picked up...) are
/// handed in by <see cref="GameLinks"/> through <see cref="SetFound"/>. They count as completed
/// but aren't stored here: they come from the game's own save, so they always match it. The
/// exception is a record the game can write over later (the gym's best time, for The Greatest):
/// such a POI is marked as well (<see cref="Keep"/>), so it stays completed with the save.
/// </para>
/// </summary>
public class PoiProgress
{
	/// <summary>Play-time slack (s) when matching snapshots to a save.</summary>
	private const float Slack = 0.5f;

	/// <summary>Snapshots kept per slot. Old ones only matter if the game goes back to a much older save.</summary>
	private const int MaxSnapshots = 64;

	private sealed class Snapshot
	{
		public float TimePlayed;

		public long[] Ids;
	}

	private readonly MelonLogger.Instance _log;

	/// <summary>Marked by the player.</summary>
	private readonly HashSet<long> _done = new HashSet<long>();

	/// <summary>Found according to the game's save.</summary>
	private readonly HashSet<long> _found = new HashSet<long>();

	/// <summary>Items that have left the game according to its save: off the map, not counted.</summary>
	private readonly HashSet<long> _gone = new HashSet<long>();

	/// <summary>POIs that can't be ticked off by hand: movable items, which show where they are instead.</summary>
	private readonly HashSet<long> _unmarkable = new HashSet<long>();

	/// <summary>Oldest first.</summary>
	private readonly List<Snapshot> _history = new List<Snapshot>();

	private string _path;

	private string _saveName;

	/// <summary>Which of the game's save objects the marks are for (as handed to <see cref="Attach"/>; 0 if not said).</summary>
	private long _save;

	/// <summary>The history in memory differs from the file.</summary>
	private bool _dirty;

	/// <summary>A new game started in this slot; the old file is retired at the game's first save.</summary>
	private bool _retire;

	public PoiProgress(MelonLogger.Instance log)
	{
		_log = log;
	}

	/// <summary>A game save is loaded, so the marks are kept with it.</summary>
	public bool Attached => _path != null;

	/// <summary>POIs completed, by either the player or the game (items that left the game aside).</summary>
	public int Count
	{
		get
		{
			int count = 0;
			foreach (long id in _done)
			{
				if (!_gone.Contains(id))
				{
					count++;
				}
			}
			foreach (long id in _found)
			{
				if (!_done.Contains(id) && !_gone.Contains(id))
				{
					count++;
				}
			}
			return count;
		}
	}

	/// <summary>How many items have left the game.</summary>
	public int GoneCount => _gone.Count;

	/// <summary>Changes whenever the marks change, so views can cache what they show.</summary>
	public int Version { get; private set; }

	/// <summary>Files written (for the tests).</summary>
	public int Writes { get; private set; }

	public bool IsDone(Poi poi)
	{
		return poi != null && (_done.Contains(poi.Id) || _found.Contains(poi.Id));
	}

	/// <summary>The item has left the game (handed in, broken...), so its POI is off the map.</summary>
	public bool IsGone(Poi poi)
	{
		return poi != null && _gone.Contains(poi.Id);
	}

	/// <summary>The items the game's save says have left the game (replaces the previous set).</summary>
	public void SetGone(HashSet<long> gone)
	{
		if (_gone.SetEquals(gone))
		{
			return;
		}
		_gone.Clear();
		_gone.UnionWith(gone);
		Version++;
	}

	/// <summary>
	/// POIs that can't be ticked off by hand: landmarks for reference (Points of Interest,
	/// Graffiti), and movable items (hats, items, ducklings), since finding one or carrying it
	/// about doesn't make it done; their POIs show where they are instead. The game can still
	/// tick off an item it records (a hat's dream) through <see cref="SetFound"/>.
	/// </summary>
	public void SetUnmarkable(IEnumerable<long> ids)
	{
		_unmarkable.Clear();
		_unmarkable.UnionWith(ids);
	}

	public bool IsUnmarkable(Poi poi)
	{
		return poi != null && _unmarkable.Contains(poi.Id);
	}

	/// <summary>The game's save says this POI is done, so the player can't unmark it.</summary>
	public bool IsFound(Poi poi)
	{
		return poi != null && _found.Contains(poi.Id);
	}

	/// <summary>The POIs the game's save says are done (replaces the previous set).</summary>
	public void SetFound(HashSet<long> found)
	{
		if (_found.SetEquals(found))
		{
			return;
		}
		_found.Clear();
		_found.UnionWith(found);
		Version++;
	}

	/// <summary>
	/// Mark or unmark a POI; returns true if it's now completed. Reaches the disk at the game's
	/// next save. A POI the game records as found stays completed.
	/// </summary>
	public bool Toggle(Poi poi)
	{
		if (_found.Contains(poi.Id))
		{
			return true;
		}
		if (_unmarkable.Contains(poi.Id))
		{
			return false;
		}
		bool done = !_done.Remove(poi.Id);
		if (done)
		{
			_done.Add(poi.Id);
		}
		Version++;
		return done;
	}

	/// <summary>
	/// The game's save says this POI is done, but may not keep saying so: the gyms keep their best
	/// times under one key, so a slower run of the Invisible Gym's course can be written over the
	/// Underwater Gym's best. The POI is marked as well, so it stays completed with this save (the
	/// mark reaches the disk at the game's next save). Only taken for the save the marks are for:
	/// <paramref name="save"/> is the one the game's record was read from, as given to <see cref="Attach"/>.
	/// </summary>
	public void Keep(Poi poi, long save)
	{
		if (poi == null || _path == null || _save == 0 || save != _save || _unmarkable.Contains(poi.Id))
		{
			return;
		}
		if (_done.Add(poi.Id))
		{
			Version++;
		}
	}

	/// <summary>
	/// The game has loaded a save (or started a new game) whose marks live in <paramref name="path"/>.
	/// The marks become those of the last snapshot taken at or before <paramref name="timePlayed"/>.
	/// </summary>
	/// <param name="saveName">The game's save file, for the log and the file's description.</param>
	/// <param name="save">Which save object this is (any number that tells the game's saves apart, 0 for none), for <see cref="Keep"/>.</param>
	public void Attach(string path, string saveName, float timePlayed, bool newGame, long save = 0)
	{
		_path = path;
		_saveName = saveName;
		_save = save;
		_history.Clear();
		_done.Clear();
		_dirty = false;
		_retire = false;
		if (newGame)
		{
			// The game deleted this slot's save (or never had one); old marks belong to that playthrough.
			_retire = File.Exists(path);
		}
		else
		{
			Read();
			// Snapshots after this save are from a future that the game has gone back on.
			if (_history.RemoveAll(s => s.TimePlayed > timePlayed + Slack) > 0)
			{
				_dirty = true;
			}
			if (_history.Count > 0)
			{
				_done.UnionWith(_history[_history.Count - 1].Ids);
				// Old marks on what can't be ticked off any more; the next game save drops them.
				_done.ExceptWith(_unmarkable);
			}
		}
		Version++;
	}

	/// <summary>The game has just written its save at <paramref name="timePlayed"/>: keep the marks with it.</summary>
	public void Commit(float timePlayed)
	{
		if (_path == null)
		{
			return;
		}
		if (_history.RemoveAll(s => s.TimePlayed > timePlayed + Slack) > 0)
		{
			_dirty = true;
		}
		Snapshot last = (_history.Count > 0) ? _history[_history.Count - 1] : null;
		if ((last == null) ? (_done.Count > 0) : !SameAsDone(last.Ids))
		{
			long[] ids = new long[_done.Count];
			_done.CopyTo(ids);
			Array.Sort(ids);
			_history.Add(new Snapshot
			{
				TimePlayed = timePlayed,
				Ids = ids
			});
			if (_history.Count > MaxSnapshots)
			{
				_history.RemoveRange(0, _history.Count - MaxSnapshots);
			}
			_dirty = true;
		}
		if (_dirty || _retire)
		{
			Write();
		}
	}

	private bool SameAsDone(long[] ids)
	{
		if (ids.Length != _done.Count)
		{
			return false;
		}
		foreach (long id in ids)
		{
			if (!_done.Contains(id))
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>Where a retired file goes, as the game keeps a deleted save as saveDELETED&lt;n&gt;.sav.</summary>
	public static string RetiredPath(string path)
	{
		return Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + ".deleted" + Path.GetExtension(path));
	}

	private void Read()
	{
		try
		{
			if (!File.Exists(_path))
			{
				return;
			}
			List<object> snapshots = MiniJson.List(MiniJson.ParseObject(File.ReadAllText(_path)), "snapshots");
			if (snapshots == null)
			{
				return;
			}
			foreach (object item in snapshots)
			{
				if (!(item is Dictionary<string, object> snapshot) || !(MiniJson.List(snapshot, "completed") is List<object> list))
				{
					continue;
				}
				List<long> ids = new List<long>(list.Count);
				foreach (object id in list)
				{
					if (id is double d)
					{
						ids.Add((long)d);
					}
				}
				ids.Sort();
				_history.Add(new Snapshot
				{
					TimePlayed = (float)MiniJson.Num(snapshot, "timePlayed"),
					Ids = ids.ToArray()
				});
			}
			// Oldest first; OrderBy is stable, so equal times keep the file's order.
			Snapshot[] sorted = _history.OrderBy(s => s.TimePlayed).ToArray();
			_history.Clear();
			_history.AddRange(sorted);
		}
		catch (Exception ex)
		{
			_history.Clear();
			_log?.Warning("Couldn't read " + Path.GetFileName(_path) + " (" + ex.Message + "); starting " + _saveName + " with no completed POIs.");
		}
	}

	private void Write()
	{
		try
		{
			if (_retire)
			{
				if (File.Exists(_path))
				{
					File.Move(_path, RetiredPath(_path), overwrite: true);
				}
				_retire = false;
			}
			if (_history.Count > 0 || File.Exists(_path))
			{
				string tmp = _path + ".tmp";
				File.WriteAllText(tmp, Serialize());
				File.Move(tmp, _path, overwrite: true);
				Writes++;
				_log?.Msg($"Saved {_done.Count} POI{((_done.Count == 1) ? "" : "s")} marked completed with {_saveName}.");
			}
			_dirty = false;
		}
		catch (Exception ex)
		{
			_log?.Warning("Couldn't save completed POIs: " + ex.Message);
		}
	}

	private string Serialize()
	{
		StringBuilder sb = new StringBuilder(256 + _history.Count * 96);
		sb.Append("{\n");
		sb.Append("  \"about\": \"CabbySteps: Map Genie ids of the POIs marked completed in ").Append(_saveName)
			.Append(", as of each game save (timePlayed is that save's play time in seconds).\",\n");
		sb.Append("  \"snapshots\": [");
		for (int i = 0; i < _history.Count; i++)
		{
			Snapshot s = _history[i];
			sb.Append((i == 0) ? "\n" : ",\n");
			sb.Append("    { \"timePlayed\": ").Append(s.TimePlayed.ToString("R", CultureInfo.InvariantCulture)).Append(", \"completed\": [");
			for (int j = 0; j < s.Ids.Length; j++)
			{
				if (j > 0)
				{
					sb.Append(", ");
				}
				sb.Append(s.Ids[j].ToString(CultureInfo.InvariantCulture));
			}
			sb.Append("] }");
		}
		sb.Append((_history.Count > 0) ? "\n  ]\n}\n" : "]\n}\n");
		return sb.ToString();
	}
}
