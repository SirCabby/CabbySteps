using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Which way Nate and the camera faced when a location was saved: what the game keeps in its own
/// save to put him back when it's loaded (<c>SaveFile.continueRot</c> from
/// <c>PlayerMovement.hipAng</c>, <c>cameraRight</c> from <c>camRight</c>), plus the camera's own
/// yaw (<c>camAng</c>), which the game's load sets to his facing instead. And where his planted
/// feet were, so going back stands him on that very footing (see <c>StanceRestore</c>).
/// </summary>
public struct SavedPose
{
	/// <summary>Recorded at all (locations saved by older versions have only a position).</summary>
	public bool Known;

	/// <summary>Nate's facing, degrees (0 = +z, clockwise from above): PlayerMovement.hipAng.</summary>
	public float Facing;

	/// <summary>The camera's yaw, degrees: PlayerMovement.camAng.</summary>
	public float Camera;

	/// <summary>The camera looks over his right shoulder: PlayerMovement.camRight.</summary>
	public bool CameraRight;

	/// <summary>
	/// Which feet were planted (bit 0: the game's <c>PlayerMovement.feet[0]</c>, bit 1: feet[1]),
	/// with their spots in <see cref="Foot0"/> and <see cref="Foot1"/>. 0 when neither was: Nate
	/// was in the air or down, or the location was saved before feet were kept.
	/// </summary>
	public int Feet;

	/// <summary>Where feet[0] stood (its target, <c>FootData.targetPos</c>), from the saved position.</summary>
	public Vector3 Foot0;

	/// <summary>Where feet[1] stood, from the saved position.</summary>
	public Vector3 Foot1;

	/// <summary>Which planted feet's turns are known (bits as <see cref="Feet"/>): their targets' rotations.</summary>
	public int FeetTurned;

	public Quaternion Foot0Turn;

	public Quaternion Foot1Turn;

	/// <summary>Nate's whole body as it stood, or null (not standing, or saved by an older version).</summary>
	public SavedBody Body;

	public SavedPose(float facing, float camera, bool cameraRight)
	{
		Known = true;
		Facing = facing;
		Camera = camera;
		CameraRight = cameraRight;
		Feet = 0;
		Foot0 = default;
		Foot1 = default;
		FeetTurned = 0;
		Foot0Turn = default;
		Foot1Turn = default;
		Body = null;
	}

	public bool HasFoot(int index)
	{
		return (Feet & (1 << index)) != 0;
	}

	public bool HasFootTurn(int index)
	{
		return (FeetTurned & (1 << index)) != 0;
	}

	/// <summary>Foot <paramref name="index"/>'s spot, from the saved position.</summary>
	public Vector3 Foot(int index)
	{
		return (index == 0) ? Foot0 : Foot1;
	}

	public Quaternion FootTurn(int index)
	{
		return (index == 0) ? Foot0Turn : Foot1Turn;
	}

	/// <summary>Record foot <paramref name="index"/> (0 or 1) as planted <paramref name="offset"/> from the saved position.</summary>
	public void SetFoot(int index, Vector3 offset)
	{
		if (index == 0)
		{
			Foot0 = offset;
		}
		else
		{
			Foot1 = offset;
		}
		Feet |= 1 << index;
	}

	/// <summary>Record the turn of planted foot <paramref name="index"/>'s target.</summary>
	public void SetFootTurn(int index, Quaternion turn)
	{
		if (index == 0)
		{
			Foot0Turn = turn;
		}
		else
		{
			Foot1Turn = turn;
		}
		FeetTurned |= 1 << index;
	}
}

/// <summary>
/// Nate's body as he stood when a location was saved, so going back puts every part of him just
/// where it was rather than standing him up afresh (see <c>StanceRestore</c>). Positions are from
/// the saved position (his hips' pivot), so they move with it round the world loop.
/// </summary>
public sealed class SavedBody
{
	/// <summary>The animated character's root (<c>PlayerMovement.anim</c>'s transform): where, and its turn.</summary>
	public Vector3 Root;

	public Quaternion RootTurn;

	/// <summary>The ragdoll's own root (<c>PlayerMovement.pm</c>, the PuppetMaster's transform).</summary>
	public Vector3 Puppet;

	/// <summary>Each ragdoll part (the PuppetMaster's muscles, in order): where, and its turn.</summary>
	public Vector3[] Parts;

	public Quaternion[] PartTurns;
}

/// <summary>Which kind of saved location one is.</summary>
public enum LocationSlot
{
	/// <summary>
	/// Saved on the map and kept until it's forgotten: numbered ("Location 3") until it's given a
	/// name, which takes the number's place.
	/// </summary>
	Kept,

	/// <summary>The quick save: there's only ever one. F5 while playing saves over it, F9 goes back to it.</summary>
	Quick,

	/// <summary>Where Nate was just before the last recall, so a recall can be undone.</summary>
	Back
}

/// <summary>
/// A place Nate was saved at. It's a POI of its own kind, so it can be tracked like any other
/// (guide arrows, minimap), and Nate can be sent back to it (<see cref="Recall"/>), facing the
/// way he did. It never counts towards progress and can't be ticked off.
/// </summary>
public class SavedLocation : Poi
{
	/// <summary>Kept (saved on the map), the quick save, or "where you were".</summary>
	public LocationSlot Slot;

	/// <summary>
	/// The number in a kept location's name ("Location 3"). 0 once it's been given a name (the number
	/// goes to the next new location), and for the quick save and "where you were".
	/// </summary>
	public int Number;

	/// <summary>Which way Nate and the camera faced there.</summary>
	public SavedPose Pose;

	/// <summary>When it was saved (UTC), or DateTime.MinValue if that isn't known (older versions).</summary>
	public DateTime Saved;

	/// <summary>
	/// What's printed on its pin: a kept location's number, or once it has a name, the name's
	/// initials (<see cref="SavedLocations.Initials"/>); "Q" for the quick save; "" for <see cref="IsBack"/>.
	/// </summary>
	public string Badge;

	/// <summary>Saved on the map and kept until it's forgotten: numbered, or given a name.</summary>
	public bool IsKept => Slot == LocationSlot.Kept;

	/// <summary>A kept location that's been given a name: no number, and the name's initials on its pin.</summary>
	public bool IsNamed => IsKept && Number == 0;

	/// <summary>The quick save (F5 while playing saves over it, F9 goes back to it).</summary>
	public bool IsQuick => Slot == LocationSlot.Quick;

	/// <summary>Where Nate was just before the last recall, so a recall can be undone.</summary>
	public bool IsBack => Slot == LocationSlot.Back;

	/// <summary>How it's named in a sentence: "Location 3" (or the name it was given), "your quick save", "where you were".</summary>
	public string Phrase => IsBack ? "where you were" : (IsQuick ? "your quick save" : Title);
}

/// <summary>
/// The saved locations, kept in UserData/CabbySteps_locations.json: places where Nate was
/// saved on the map (kept until forgotten; numbered until they're given a name), the one quick save
/// (F5 while playing saves over it), and where he was before the last recall. They're the
/// player's, not a save slot's, so every save sees the same ones. Positions are world coordinates
/// with x folded into the canonical loop (<see cref="WorldLoop"/>), so they stay right when the
/// game shifts the world by whole loops.
/// </summary>
public class SavedLocations
{
	/// <summary>The POI category of saved locations (Map Genie's are all positive).</summary>
	public const int Category = -1;

	public const string Kind = "Saved location";

	public const string QuickTitle = "Quick save";

	public const string QuickKind = "F5 while playing saves over it, F9 goes back";

	public const string QuickBadge = "Q";

	public const string BackTitle = "Where you were";

	public const string BackKind = "before your last recall";

	/// <summary>The longest name a kept location can be given on the map.</summary>
	public const int MaxNameLength = 32;

	/// <summary>POI id of the "where you were" spot; kept locations are -1, -2, ... as they're made.</summary>
	private const long BackId = -1000000;

	/// <summary>POI id of the quick save.</summary>
	private const long QuickId = -1000001;

	private readonly string _path;

	private readonly Func<Vector3, Vector2> _toMap;

	private readonly MelonLogger.Instance _log;

	/// <summary>Kept locations: the numbered ones by number, then the named ones A to Z (<see cref="Before"/>).</summary>
	private readonly List<SavedLocation> _kept = new List<SavedLocation>();

	/// <summary>The quick save, the kept locations, then the "where you were" spot.</summary>
	private readonly List<SavedLocation> _all = new List<SavedLocation>();

	/// <summary>
	/// The next kept location's POI id. Not its number, which a name gives up for another to take:
	/// an id stays with its location for the session (nothing keeps ids beyond it).
	/// </summary>
	private long _nextId = -1;

	private SavedLocation _quick;

	private SavedLocation _back;

	/// <param name="path">The file they're kept in.</param>
	/// <param name="toMap">World position to map pixel (<see cref="MapProjection.WorldToMap"/>).</param>
	public SavedLocations(string path, Func<Vector3, Vector2> toMap, MelonLogger.Instance log)
	{
		_path = path;
		_toMap = toMap;
		_log = log;
	}

	/// <summary>
	/// Every saved location: the quick save if there is one, the kept ones (numbered by number, then
	/// named A to Z), then "where you were" if there is one.
	/// </summary>
	public IReadOnlyList<SavedLocation> All => _all;

	/// <summary>How many kept locations there are, numbered or named (the quick save and "where you were" aren't).</summary>
	public int Count => _kept.Count;

	/// <summary>The quick save (F5 while playing saves over it, F9 goes back to it), or null.</summary>
	public SavedLocation Quick => _quick;

	/// <summary>Where Nate was before the last recall, or null.</summary>
	public SavedLocation Back => _back;

	/// <summary>Changes whenever a location is added, moved or removed.</summary>
	public int Version { get; private set; }

	/// <summary>Successful writes of the file (for the tests).</summary>
	public int Writes { get; private set; }

	/// <summary>Read the file. A missing file means none; an unreadable one is set aside, not overwritten.</summary>
	public void Load()
	{
		_kept.Clear();
		_quick = null;
		_back = null;
		try
		{
			if (File.Exists(_path))
			{
				Dictionary<string, object> root = MiniJson.ParseObject(File.ReadAllText(_path));
				if (root == null)
				{
					throw new FormatException("not a JSON object");
				}
				List<Dictionary<string, object>> unnumbered = new List<Dictionary<string, object>>();
				if (MiniJson.List(root, "locations") is List<object> list)
				{
					foreach (object item in list)
					{
						if (!(item is Dictionary<string, object> o) || !TryReadPosition(o, out Vector3 world))
						{
							continue;
						}
						int number = (int)MiniJson.Num(o, "n");
						string name = MiniJson.Str(o, "name")?.Trim() ?? "";
						// A name of its own makes it a named one, with no number: one an older
						// version kept with it is let go.
						if (name.Length > 0 && !string.Equals(name, DefaultName(number), StringComparison.Ordinal))
						{
							Insert(Read(LocationSlot.Kept, 0, name, o, world));
							continue;
						}
						if (number < 1 || Find(number) != null)
						{
							unnumbered.Add(o);
							continue;
						}
						Insert(Read(LocationSlot.Kept, number, null, o, world));
					}
				}
				// Entries without a name or a usable number (added by hand, say) get the first free numbers.
				foreach (Dictionary<string, object> o in unnumbered)
				{
					TryReadPosition(o, out Vector3 world);
					Insert(Read(LocationSlot.Kept, FreeNumber(), null, o, world));
				}
				if (root.TryGetValue("quick", out object quick) && quick is Dictionary<string, object> q && TryReadPosition(q, out Vector3 quickWorld))
				{
					_quick = Read(LocationSlot.Quick, 0, null, q, quickWorld);
				}
				if (root.TryGetValue("back", out object back) && back is Dictionary<string, object> b && TryReadPosition(b, out Vector3 backWorld))
				{
					_back = Read(LocationSlot.Back, 0, null, b, backWorld);
				}
			}
		}
		catch (Exception ex)
		{
			_kept.Clear();
			_quick = null;
			_back = null;
			string aside = Path.ChangeExtension(_path, ".unreadable.json");
			try
			{
				File.Move(_path, aside, overwrite: true);
				_log?.Warning("Couldn't read " + Path.GetFileName(_path) + " (" + ex.Message + "): starting with no saved locations. The old file is kept as " + Path.GetFileName(aside) + ".");
			}
			catch (Exception moveEx)
			{
				_log?.Warning("Couldn't read " + Path.GetFileName(_path) + " (" + ex.Message + ") or set it aside (" + moveEx.Message + "): starting with no saved locations.");
			}
		}
		Changed(write: false);
	}

	/// <summary>
	/// Save a new location at <paramref name="world"/>, facing <paramref name="pose"/>, numbered with
	/// the lowest free number. <paramref name="saved"/>: when (now, unless a test says otherwise).
	/// </summary>
	public SavedLocation Add(Vector3 world, SavedPose pose = default, DateTime? saved = null)
	{
		SavedLocation location = Make(LocationSlot.Kept, FreeNumber(), null, world);
		location.Pose = pose;
		location.Saved = saved ?? DateTime.UtcNow;
		Insert(location);
		Changed(write: true);
		return location;
	}

	/// <summary>Forget a location (a kept one, the quick save or "where you were").</summary>
	public bool Remove(SavedLocation location)
	{
		bool removed;
		if (location != null && location == _quick)
		{
			_quick = null;
			removed = true;
		}
		else if (location != null && location == _back)
		{
			_back = null;
			removed = true;
		}
		else
		{
			removed = _kept.Remove(location);
		}
		if (removed)
		{
			Changed(write: true);
		}
		return removed;
	}

	/// <summary>
	/// Quick save Nate at <paramref name="world"/>, facing <paramref name="pose"/>, over the quick
	/// save there was: there's only ever one, so they don't pile up. The existing one moves (so
	/// tracking it follows), or one is made. <paramref name="saved"/>: when (now, unless a test
	/// says otherwise).
	/// </summary>
	public SavedLocation SetQuick(Vector3 world, SavedPose pose = default, DateTime? saved = null)
	{
		_quick = Spot(_quick, LocationSlot.Quick, world, pose, saved ?? DateTime.UtcNow);
		Changed(write: true);
		return _quick;
	}

	/// <summary>
	/// Give a kept location a new name, tidied up (<see cref="CleanName"/>). A named one has no
	/// number: its pin shows the name's initials, and its number goes to the next new location. One
	/// left empty is numbered again, with the lowest free number. It moves to its place in the order
	/// (<see cref="Before"/>); its id stays. False, and nothing written, if it isn't one of the kept
	/// locations or is called that already.
	/// </summary>
	public bool Rename(SavedLocation location, string name)
	{
		if (location == null || !location.IsKept || !_kept.Contains(location))
		{
			return false;
		}
		string clean = CleanName(name);
		bool same = (clean.Length > 0) ? string.Equals(clean, location.Title, StringComparison.Ordinal) : !location.IsNamed;
		if (same)
		{
			return false;
		}
		_kept.Remove(location);
		Name(location, clean, FreeNumber());
		Insert(location);
		Changed(write: true);
		return true;
	}

	/// <summary>
	/// What <paramref name="location"/> (a kept one) would be called with its name left empty: its
	/// own number's name, or for a named one, the lowest free number's.
	/// </summary>
	public string NameIfCleared(SavedLocation location)
	{
		return DefaultName(location.IsNamed ? FreeNumber() : location.Number);
	}

	/// <summary>What a kept location is called until it's given a name: "Location 3".</summary>
	public static string DefaultName(int number)
	{
		return "Location " + number.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// What a named location's pin shows: the first letter or digit of each of the name's first two
	/// words, in capitals ("Underwater Gym" is "UG", "Tower" is "T", "Camp 2" is "C2"). A word with
	/// neither ("-", "&amp;") is passed over; a name with none at all shows its first character.
	/// </summary>
	public static string Initials(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return "";
		}
		char first = '\0';
		char fallback = '\0';
		// Still looking for this word's first letter or digit.
		bool wordStart = true;
		foreach (char c in name)
		{
			if (char.IsWhiteSpace(c))
			{
				wordStart = true;
				continue;
			}
			if (fallback == '\0' && !char.IsSurrogate(c))
			{
				fallback = c;
			}
			if (!wordStart || !char.IsLetterOrDigit(c))
			{
				continue;
			}
			wordStart = false;
			char upper = char.ToUpperInvariant(c);
			if (first == '\0')
			{
				first = upper;
				continue;
			}
			return new string(new[] { first, upper });
		}
		if (first != '\0')
		{
			return first.ToString();
		}
		return (fallback != '\0') ? fallback.ToString() : "";
	}

	/// <summary>
	/// A name as it's kept: no control characters or halves of emoji, every run of spaces, tabs or
	/// new lines made one space, none at either end, and no more than <see cref="MaxNameLength"/>
	/// characters. "" when nothing's left.
	/// </summary>
	public static string CleanName(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return "";
		}
		StringBuilder sb = new StringBuilder(Math.Min(name.Length, MaxNameLength));
		bool space = false;
		foreach (char c in name)
		{
			if (char.IsWhiteSpace(c))
			{
				space = sb.Length > 0;
				continue;
			}
			if (char.IsControl(c) || char.IsSurrogate(c))
			{
				continue;
			}
			if (space)
			{
				// A space that would be the last character is left off with what follows it.
				if (sb.Length + 1 >= MaxNameLength)
				{
					break;
				}
				sb.Append(' ');
				space = false;
			}
			if (sb.Length >= MaxNameLength)
			{
				break;
			}
			sb.Append(c);
		}
		return sb.ToString();
	}

	/// <summary>Remember where Nate was before a recall, and which way he faced. The existing spot moves (so tracking it follows), or one is made.</summary>
	public void SetBack(Vector3 world, SavedPose pose = default)
	{
		_back = Spot(_back, LocationSlot.Back, world, pose, DateTime.UtcNow);
		Changed(write: true);
	}

	/// <summary><paramref name="spot"/> moved to <paramref name="world"/> with a new pose and time, or a new one if there's none.</summary>
	private SavedLocation Spot(SavedLocation spot, LocationSlot slot, Vector3 world, SavedPose pose, DateTime saved)
	{
		if (spot == null)
		{
			spot = Make(slot, 0, null, world);
		}
		else
		{
			Place(spot, world);
		}
		spot.Pose = pose;
		spot.Saved = saved;
		return spot;
	}

	/// <summary>
	/// The location after (<paramref name="direction"/> 1) or before (-1) <paramref name="from"/>,
	/// going round; from none, the first or the last. Null when there are none.
	/// </summary>
	public SavedLocation Step(SavedLocation from, int direction)
	{
		int count = _all.Count;
		if (count == 0)
		{
			return null;
		}
		int index = (from != null) ? _all.IndexOf(from) : -1;
		if (index < 0)
		{
			return _all[(direction >= 0) ? 0 : (count - 1)];
		}
		return _all[((index + direction) % count + count) % count];
	}

	/// <summary>The kept location numbered <paramref name="number"/>, or null.</summary>
	private SavedLocation Find(int number)
	{
		foreach (SavedLocation location in _kept)
		{
			if (!location.IsNamed && location.Number == number)
			{
				return location;
			}
		}
		return null;
	}

	/// <summary>The lowest number no kept location has: named ones have none.</summary>
	private int FreeNumber()
	{
		// The numbered ones come first, by number, so the first gap is the first free number.
		int number = 1;
		foreach (SavedLocation location in _kept)
		{
			if (location.IsNamed || location.Number != number)
			{
				break;
			}
			number++;
		}
		return number;
	}

	/// <summary>A kept location into its place in the order (<see cref="Before"/>); among equals, after the ones already there.</summary>
	private void Insert(SavedLocation location)
	{
		int i = 0;
		while (i < _kept.Count && !Before(location, _kept[i]))
		{
			i++;
		}
		_kept.Insert(i, location);
	}

	/// <summary>Kept location <paramref name="a"/> comes before <paramref name="b"/>: numbered ones first, by number, then named ones A to Z.</summary>
	private static bool Before(SavedLocation a, SavedLocation b)
	{
		if (a.IsNamed != b.IsNamed)
		{
			return !a.IsNamed;
		}
		if (!a.IsNamed)
		{
			return a.Number < b.Number;
		}
		return string.Compare(a.Title, b.Title, StringComparison.InvariantCultureIgnoreCase) < 0;
	}

	/// <summary>
	/// A kept location called <paramref name="name"/>, or with none (null or ""), numbered
	/// <paramref name="number"/>: its number, title and pin.
	/// </summary>
	private static void Name(SavedLocation location, string name, int number)
	{
		if (!string.IsNullOrEmpty(name))
		{
			location.Number = 0;
			location.Title = name;
			location.Badge = Initials(name);
		}
		else
		{
			location.Number = number;
			location.Title = DefaultName(number);
			location.Badge = number.ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <summary>
	/// A new location of kind <paramref name="slot"/>. <paramref name="name"/> and <paramref name="number"/>
	/// are only for kept ones: called <paramref name="name"/>, or with none, numbered <paramref name="number"/>.
	/// </summary>
	private SavedLocation Make(LocationSlot slot, int number, string name, Vector3 world)
	{
		SavedLocation location = new SavedLocation
		{
			Slot = slot,
			Cat = Category,
			ReferenceOnly = true
		};
		switch (slot)
		{
		case LocationSlot.Quick:
			location.Id = QuickId;
			location.CatName = QuickKind;
			location.Title = QuickTitle;
			location.Badge = QuickBadge;
			break;
		case LocationSlot.Back:
			location.Id = BackId;
			location.CatName = BackKind;
			location.Title = BackTitle;
			location.Badge = "";
			break;
		default:
			location.Id = _nextId--;
			location.CatName = Kind;
			Name(location, name?.Trim(), number);
			break;
		}
		Place(location, world);
		return location;
	}

	/// <summary>
	/// A location from the file (<paramref name="name"/> and <paramref name="number"/> as for
	/// <see cref="Make"/>): its position, and its facing and saving time if it has them.
	/// </summary>
	private SavedLocation Read(LocationSlot slot, int number, string name, Dictionary<string, object> o, Vector3 world)
	{
		SavedLocation location = Make(slot, number, name, world);
		if (o.TryGetValue("facing", out object facing) && facing is double f && Finite(f)
			&& o.TryGetValue("camera", out object camera) && camera is double c && Finite(c))
		{
			SavedPose pose = new SavedPose((float)f, (float)c, MiniJson.Bool(o, "cameraRight"));
			// "feet": per foot of the game's, [x, y, z] from the saved position and its turn
			// [qx, qy, qz, qw] (older files: no turn), or null if it wasn't planted.
			if (MiniJson.List(o, "feet") is List<object> feet)
			{
				for (int i = 0; i < 2 && i < feet.Count; i++)
				{
					if (TryReadPlace(feet[i], out Vector3 offset, out bool turned, out Quaternion turn))
					{
						pose.SetFoot(i, offset);
						if (turned)
						{
							pose.SetFootTurn(i, turn);
						}
					}
				}
			}
			if (o.TryGetValue("body", out object body) && body is Dictionary<string, object> b)
			{
				pose.Body = ReadBody(b);
			}
			location.Pose = pose;
		}
		string saved = MiniJson.Str(o, "saved");
		if (saved != null && DateTime.TryParse(saved, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime when))
		{
			location.Saved = when;
		}
		return location;
	}

	private void Place(SavedLocation location, Vector3 world)
	{
		Vector3 canonical = new Vector3(WorldLoop.WrapX(world.x), world.y, world.z);
		Vector2 px = _toMap(canonical);
		location.World = location.HomeWorld = canonical;
		location.HasHeight = location.HomeHasHeight = true;
		location.Px = location.HomePx = px.x;
		location.Py = location.HomePy = px.y;
	}

	private static bool TryReadPosition(Dictionary<string, object> o, out Vector3 world)
	{
		world = default;
		if (!(o.TryGetValue("x", out object x) && x is double dx && o.TryGetValue("y", out object y) && y is double dy && o.TryGetValue("z", out object z) && z is double dz))
		{
			return false;
		}
		if (!Finite(dx) || !Finite(dy) || !Finite(dz))
		{
			return false;
		}
		world = new Vector3((float)dx, (float)dy, (float)dz);
		return true;
	}

	/// <summary>
	/// A spot from the saved position, [x, y, z] (each within <paramref name="maxOffset"/>: a few
	/// metres, unless said otherwise), optionally followed by its turn [qx, qy, qz, qw] (made unit
	/// length; one that's nowhere near a turn isn't believed).
	/// </summary>
	private static bool TryReadPlace(object value, out Vector3 offset, out bool turned, out Quaternion turn, double maxOffset = MaxOffset)
	{
		offset = default;
		turned = false;
		turn = default;
		if (!(value is List<object> list) || (list.Count != 3 && list.Count != 7))
		{
			return false;
		}
		double[] n = new double[list.Count];
		for (int i = 0; i < n.Length; i++)
		{
			if (!(list[i] is double d) || !Finite(d))
			{
				return false;
			}
			n[i] = d;
		}
		if (Math.Abs(n[0]) > maxOffset || Math.Abs(n[1]) > maxOffset || Math.Abs(n[2]) > maxOffset)
		{
			return false;
		}
		offset = new Vector3((float)n[0], (float)n[1], (float)n[2]);
		if (n.Length == 7)
		{
			double length = Math.Sqrt(n[3] * n[3] + n[4] * n[4] + n[5] * n[5] + n[6] * n[6]);
			if (length < 0.5 || length > 2.0)
			{
				return false;
			}
			turn = new Quaternion((float)(n[3] / length), (float)(n[4] / length), (float)(n[5] / length), (float)(n[6] / length));
			turned = true;
		}
		return true;
	}

	/// <summary>"body": {"root": [7 numbers], "puppet": [3], "parts": [[7], ...]}, or null if any of it doesn't read.</summary>
	private static SavedBody ReadBody(Dictionary<string, object> o)
	{
		if (!o.TryGetValue("root", out object root) || !TryReadPlace(root, out Vector3 rootAt, out bool rootTurned, out Quaternion rootTurn) || !rootTurned
			|| !o.TryGetValue("puppet", out object puppet) || !TryReadPlace(puppet, out Vector3 puppetAt, out bool _, out Quaternion _, MaxPuppetOffset)
			|| !(MiniJson.List(o, "parts") is List<object> parts) || parts.Count == 0 || parts.Count > MaxParts)
		{
			return null;
		}
		SavedBody body = new SavedBody
		{
			Root = rootAt,
			RootTurn = rootTurn,
			Puppet = puppetAt,
			Parts = new Vector3[parts.Count],
			PartTurns = new Quaternion[parts.Count]
		};
		for (int i = 0; i < parts.Count; i++)
		{
			if (!TryReadPlace(parts[i], out body.Parts[i], out bool turned, out body.PartTurns[i]) || !turned)
			{
				return null;
			}
		}
		return body;
	}

	/// <summary>A spot further than this (m, on any axis) from the saved position isn't believed.</summary>
	private const double MaxOffset = 3.0;

	/// <summary>
	/// The ragdoll's own root (the PuppetMaster's transform) doesn't follow Nate about the way his
	/// parts do: one was saved 22 m from his hips. Going back puts it at the same offset, as a
	/// trip within the session does, so any offset within the world is believed.
	/// </summary>
	private const double MaxPuppetOffset = 10000.0;

	/// <summary>More ragdoll parts than this isn't Nate.</summary>
	private const int MaxParts = 64;

	private static bool Finite(double d)
	{
		return !double.IsNaN(d) && !double.IsInfinity(d) && Math.Abs(d) < 1e7;
	}

	private void Changed(bool write)
	{
		_all.Clear();
		if (_quick != null)
		{
			_all.Add(_quick);
		}
		_all.AddRange(_kept);
		if (_back != null)
		{
			_all.Add(_back);
		}
		Version++;
		if (write)
		{
			Write();
		}
	}

	private void Write()
	{
		try
		{
			string tmp = _path + ".tmp";
			File.WriteAllText(tmp, Serialize());
			File.Move(tmp, _path, overwrite: true);
			Writes++;
		}
		catch (Exception ex)
		{
			_log?.Warning("Couldn't save the saved locations: " + ex.Message);
		}
	}

	private string Serialize()
	{
		StringBuilder sb = new StringBuilder(256 + _kept.Count * 96);
		sb.Append("{\n");
		sb.Append("  \"about\": \"CabbySteps: places Nate was saved at (world metres; x is in the 512 m world loop), which way he and the camera faced (degrees), where his planted feet were and how each part of him stood (metres from x, y, z, then turns as quaternions; a foot in the air is null) and when (UTC). locations: the ones saved on the map, numbered (n) until they're given a name - there (hold A on its pin, or F2) or by changing name here - and numbered again if it's left empty; quick: the quick save, which F5 while playing saves over and F9 goes back to; back: where Nate was before the last trip.\",\n");
		sb.Append("  \"locations\": [");
		for (int i = 0; i < _kept.Count; i++)
		{
			SavedLocation location = _kept[i];
			sb.Append((i == 0) ? "\n" : ",\n");
			sb.Append("    { ");
			// A named one has no number.
			if (!location.IsNamed)
			{
				sb.Append("\"n\": ").Append(location.Number.ToString(CultureInfo.InvariantCulture)).Append(", ");
			}
			sb.Append("\"name\": ");
			AppendString(sb, location.Title);
			AppendPosition(sb, location.World);
			AppendPose(sb, location);
			sb.Append(" }");
		}
		sb.Append((_kept.Count > 0) ? "\n  ],\n" : "],\n");
		AppendSpot(sb, "quick", _quick, last: false);
		AppendSpot(sb, "back", _back, last: true);
		sb.Append("}\n");
		return sb.ToString();
	}

	/// <summary>The quick save's or "where you were"'s line: <c>"key": { x, y, z, pose, saved }</c>, or null when there's none.</summary>
	private static void AppendSpot(StringBuilder sb, string key, SavedLocation spot, bool last)
	{
		sb.Append("  \"").Append(key).Append("\": ");
		if (spot != null)
		{
			sb.Append('{');
			AppendPosition(sb, spot.World);
			AppendPose(sb, spot);
			sb.Append(" }");
		}
		else
		{
			sb.Append("null");
		}
		sb.Append(last ? "\n" : ",\n");
	}

	private static void AppendPosition(StringBuilder sb, Vector3 world)
	{
		sb.Append((sb[sb.Length - 1] == '{') ? " " : ", ");
		sb.Append("\"x\": ").Append(world.x.ToString("R", CultureInfo.InvariantCulture));
		sb.Append(", \"y\": ").Append(world.y.ToString("R", CultureInfo.InvariantCulture));
		sb.Append(", \"z\": ").Append(world.z.ToString("R", CultureInfo.InvariantCulture));
	}

	private static void AppendPose(StringBuilder sb, SavedLocation location)
	{
		SavedPose pose = location.Pose;
		if (pose.Known)
		{
			sb.Append(", \"facing\": ").Append(pose.Facing.ToString("R", CultureInfo.InvariantCulture));
			sb.Append(", \"camera\": ").Append(pose.Camera.ToString("R", CultureInfo.InvariantCulture));
			sb.Append(", \"cameraRight\": ").Append(pose.CameraRight ? "true" : "false");
			if (pose.Feet != 0)
			{
				sb.Append(", \"feet\": [");
				for (int i = 0; i < 2; i++)
				{
					sb.Append((i == 0) ? "" : ", ");
					if (!pose.HasFoot(i))
					{
						sb.Append("null");
						continue;
					}
					AppendPlace(sb, pose.Foot(i), pose.HasFootTurn(i), pose.FootTurn(i));
				}
				sb.Append(']');
			}
			SavedBody body = pose.Body;
			if (body != null && body.Parts != null && body.PartTurns != null && body.Parts.Length == body.PartTurns.Length)
			{
				sb.Append(", \"body\": { \"root\": ");
				AppendPlace(sb, body.Root, true, body.RootTurn);
				sb.Append(", \"puppet\": ");
				AppendPlace(sb, body.Puppet, false, default);
				sb.Append(", \"parts\": [");
				for (int i = 0; i < body.Parts.Length; i++)
				{
					sb.Append((i == 0) ? "" : ", ");
					AppendPlace(sb, body.Parts[i], true, body.PartTurns[i]);
				}
				sb.Append("] }");
			}
		}
		if (location.Saved != DateTime.MinValue)
		{
			sb.Append(", \"saved\": \"").Append(location.Saved.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)).Append('"');
		}
	}

	/// <summary>[x, y, z], or [x, y, z, qx, qy, qz, qw] with a turn.</summary>
	private static void AppendPlace(StringBuilder sb, Vector3 at, bool turned, Quaternion turn)
	{
		sb.Append('[').Append(at.x.ToString("R", CultureInfo.InvariantCulture))
			.Append(", ").Append(at.y.ToString("R", CultureInfo.InvariantCulture))
			.Append(", ").Append(at.z.ToString("R", CultureInfo.InvariantCulture));
		if (turned)
		{
			sb.Append(", ").Append(turn.x.ToString("R", CultureInfo.InvariantCulture))
				.Append(", ").Append(turn.y.ToString("R", CultureInfo.InvariantCulture))
				.Append(", ").Append(turn.z.ToString("R", CultureInfo.InvariantCulture))
				.Append(", ").Append(turn.w.ToString("R", CultureInfo.InvariantCulture));
		}
		sb.Append(']');
	}

	private static void AppendString(StringBuilder sb, string s)
	{
		sb.Append('"');
		foreach (char c in s)
		{
			switch (c)
			{
			case '"':
				sb.Append("\\\"");
				break;
			case '\\':
				sb.Append("\\\\");
				break;
			case '\n':
				sb.Append("\\n");
				break;
			case '\r':
				sb.Append("\\r");
				break;
			case '\t':
				sb.Append("\\t");
				break;
			default:
				if (c < ' ')
				{
					sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
				}
				else
				{
					sb.Append(c);
				}
				break;
			}
		}
		sb.Append('"');
	}
}
