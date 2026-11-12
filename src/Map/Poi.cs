using UnityEngine;

namespace CabbySteps;

/// <summary>A point of interest from pois.json (Map Genie's list, placed on the game's own map, with world positions).</summary>
public class Poi
{
	/// <summary>Map Genie's location id. It never changes, so completion marks are kept by it.</summary>
	public long Id;

	public int Cat;

	public string CatName;

	public string Icon;

	public string Title;

	/// <summary>
	/// Where it's drawn, in map pixels (texels of the game's painted sheets, see <see cref="MapProjection"/>):
	/// where the game's own projection puts its world position, Map Genie's marker when the game has no
	/// place for it, or a movable object's live spot.
	/// </summary>
	public float Px;

	public float Py;

	/// <summary>Where it is in the world (x in the canonical loop).</summary>
	public Vector3 World;

	/// <summary>
	/// True when World comes from the game (a game object, a trigger, a campfire), so World.y is real.
	/// Otherwise the position was read back off Map Genie's marker and y is unknown.
	/// </summary>
	public bool HasHeight;

	/// <summary>
	/// Keys in the save's "dialogs played" set that mean this POI is done (cutscene timelines,
	/// "CANS&lt;x&gt;.&lt;z&gt;"...). Null when the game keeps no record. Movable items have none:
	/// finding one and carrying it about doesn't make it done.
	/// </summary>
	public string[] Found;

	/// <summary>Keys that mean this item has left the game (a trophy handed in, the vase broken), or null.</summary>
	public string[] Gone;

	/// <summary>
	/// A best time the save keeps (<c>SaveFile.miscNums</c>, by this key) that makes this POI done
	/// once it's under <see cref="BestUnder"/> seconds, or null. The Greatest: the gym's best time
	/// ("GymPB") under the designer's, which is all the game records of that achievement.
	/// </summary>
	public string BestKey;

	/// <summary>The time (s) <see cref="BestKey"/>'s best has to be under.</summary>
	public float BestUnder;

	/// <summary>The loader object of a movable item (hat, item, duckling), or null.</summary>
	public string Item;

	/// <summary>A landmark for reference (Points of Interest, Graffiti): not an objective, so never ticked off or counted.</summary>
	public bool ReferenceOnly;

	/// <summary>Map Genie categories that are landmarks rather than objectives.</summary>
	public static readonly string[] ReferenceCategories = { "Point of Interest", "Graffiti" };

	/// <summary>
	/// Counts towards progress: not a landmark for reference, and not a movable item unless the
	/// game itself can tick it off (a hat's dream).
	/// </summary>
	public bool Completable => !ReferenceOnly && (Item == null || Found != null);

	/// <summary>
	/// Can be ticked off by hand. Landmarks are for reference, and movable items show where they
	/// are instead: finding one and carrying it about doesn't make it done.
	/// </summary>
	public bool HandMarkable => !ReferenceOnly && Item == null;

	/// <summary>Px, Py, World and HasHeight as pois.json has them; a movable item's POI returns here when it can't follow the object.</summary>
	public float HomePx;

	public float HomePy;

	public Vector3 HomeWorld;

	public bool HomeHasHeight;

	/// <summary>The position currently follows the real object.</summary>
	public bool Live;

	public void GoHome()
	{
		Px = HomePx;
		Py = HomePy;
		World = HomeWorld;
		HasHeight = HomeHasHeight;
		Live = false;
	}
}
