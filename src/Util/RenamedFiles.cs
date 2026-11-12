using System;
using System.IO;

namespace CabbySteps;

/// <summary>
/// Before it was CabbySteps the mod was BabyStepsMinimap, and its files were named after it:
/// UserData/BabyStepsMinimap_config.json and _locations.json, and BabyStepsMinimap_completed&lt;slot&gt;.json
/// beside the game's saves. Each takes its new name the first time the mod wants it, unless a file
/// with the new name is already there. Game-free, checked offline.
/// </summary>
public static class RenamedFiles
{
	/// <summary>The mod's old name: the start of its old file names, and its old MelonInfo name.</summary>
	public const string OldName = "BabyStepsMinimap";

	public enum Outcome
	{
		/// <summary>No file with the old name.</summary>
		None,
		/// <summary>The old file has the new name now.</summary>
		Moved,
		/// <summary>Both are there: the new one is used, and the old one is left as it is.</summary>
		Both,
		/// <summary>The old file couldn't be renamed (see the error), and is left where it was.</summary>
		Failed
	}

	/// <summary>
	/// Give <paramref name="oldPath"/> the name <paramref name="path"/> if only the old one exists. It's
	/// moved, not copied, so this happens once: a copy left behind would be taken again whenever the
	/// new file is gone (a new game retires a slot's completed POIs), bringing old marks back.
	/// </summary>
	public static Outcome CarryOver(string oldPath, string path, out string error)
	{
		error = null;
		if (!File.Exists(oldPath))
		{
			return Outcome.None;
		}
		if (File.Exists(path))
		{
			return Outcome.Both;
		}
		try
		{
			File.Move(oldPath, path);
			return Outcome.Moved;
		}
		catch (Exception ex)
		{
			error = ex.Message;
			return Outcome.Failed;
		}
	}
}
