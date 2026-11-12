using System.IO;
using MelonLoader;
using MelonLoader.Utils;

namespace CabbySteps;

/// <summary>
/// Where the mod's files live. Shipped data is under Mods/CabbySteps/; everything the mod writes
/// goes to UserData/ (and a save slot's completed POIs beside the game's save, see SaveSync).
/// </summary>
public static class ModPaths
{
	public static string AssetDir => Path.Combine(MelonEnvironment.ModsDirectory, "CabbySteps");

	public static string Pois => Path.Combine(AssetDir, "pois.json");

	public static string Config => Path.Combine(MelonEnvironment.UserDataDirectory, "CabbySteps_config.json");

	/// <summary>Where the game's ending puts its painted map sheets and camera (tools/georef/find_journey.py): the map's layout.</summary>
	public static string Journey => Path.Combine(AssetDir, "journey.json");

	/// <summary>Saved locations (<see cref="SavedLocations"/>).</summary>
	public static string Locations => Path.Combine(MelonEnvironment.UserDataDirectory, "CabbySteps_locations.json");

	/// <summary>A save slot's completed POIs, kept in the game's save folder beside its save.</summary>
	public static string CompletedFile(int slot) => "CabbySteps_completed" + slot + ".json";

	// From before the mod was called CabbySteps (RenamedFiles): the same files under the old name.
	// The shipped data folder held Map Genie's map images too, which the mod no longer reads.

	public static string OldAssetDir => Path.Combine(MelonEnvironment.ModsDirectory, RenamedFiles.OldName);

	public static string OldConfig => Path.Combine(MelonEnvironment.UserDataDirectory, RenamedFiles.OldName + "_config.json");

	public static string OldLocations => Path.Combine(MelonEnvironment.UserDataDirectory, RenamedFiles.OldName + "_locations.json");

	public static string OldCompletedFile(int slot) => RenamedFiles.OldName + "_completed" + slot + ".json";

	/// <summary>The trail older versions recorded themselves; the Hero's Journey now comes from the game's own record.</summary>
	public static string LegacyTrail => Path.Combine(MelonEnvironment.UserDataDirectory, RenamedFiles.OldName + "_trail.txt");

	/// <summary>Written by versions that needed manual calibration; no longer read.</summary>
	public static string LegacyCalibration => Path.Combine(MelonEnvironment.UserDataDirectory, RenamedFiles.OldName + "_calibration.json");

	/// <summary>
	/// Give a file kept under the mod's old name its new name, if there's no file with the new name
	/// yet (see <see cref="RenamedFiles.CarryOver"/>), and say what happened in the log.
	/// </summary>
	public static void CarryOver(string oldPath, string path, MelonLogger.Instance log)
	{
		string oldName = Path.GetFileName(oldPath);
		string name = Path.GetFileName(path);
		switch (RenamedFiles.CarryOver(oldPath, path, out string error))
		{
		case RenamedFiles.Outcome.Moved:
			log?.Msg($"{oldName} is now {name}: the mod is called CabbySteps now.");
			break;
		case RenamedFiles.Outcome.Both:
			log?.Msg($"{oldName} is from before the mod was called CabbySteps and isn't read any more ({name} is): it can be deleted.");
			break;
		case RenamedFiles.Outcome.Failed:
			log?.Warning($"Couldn't rename {oldName} to {name} ({error}), so what it holds isn't used. Rename it yourself while the game is closed.");
			break;
		}
	}
}
