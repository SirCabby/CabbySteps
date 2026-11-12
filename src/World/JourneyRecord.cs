using System;
using System.IO;
using System.Runtime.CompilerServices;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;

namespace CabbySteps;

/// <summary>
/// Reads the game's own record of where Nate has been, for the save that's loaded: the record
/// the ending draws (see <see cref="JourneyPath"/>). <c>SaveGod</c> fills <c>positions</c> and
/// writes it out as <c>SaveGod.GetPositionsPath(slot, n)</c> in the save folder; full files stay
/// behind as <c>positionFileIndex</c> moves on. So the files before that index are read from the
/// disk, and the one being filled from the game's buffer (up to <c>positionsIndex</c>), which is
/// ahead of what's been written; from its file if the buffer is empty (just loaded, say).
/// </summary>
public static class JourneyRecord
{
	/// <summary>
	/// Fill <paramref name="path"/> from the game, dots and all. Null when it worked (even with
	/// nothing recorded yet), else why it couldn't be read, to show.
	/// </summary>
	public static string Load(JourneyPath path, MelonLogger.Instance log)
	{
		path.Clear();
		try
		{
			return Read(path, log);
		}
		catch (Exception ex)
		{
			log.Warning("Hero's Journey: can't read the game's record of Nate's path: " + ex.Message);
			return "the game's record of Nate's path can't be read (see the log)";
		}
		finally
		{
			path.BuildDots();
		}
	}

	// Kept out of line so that, if a game update breaks these interop types, the failure surfaces
	// inside Load's try block.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string Read(JourneyPath path, MelonLogger.Instance log)
	{
		SaveGod god = SaveGod.me;
		if (god == null)
		{
			return "no game is loaded yet";
		}
		PlatformService platform = god.platformService;
		string dir = (platform != null) ? platform.GetRootSaveDirectory() : null;
		if (string.IsNullOrEmpty(dir))
		{
			return "the game's save folder isn't known yet";
		}
		int slot = god.slot;
		int current = god.positionFileIndex;
		int filled = god.positionsIndex;
		int files = 0;
		for (int n = 0; n < current; n++)
		{
			if (ReadFile(path, dir, slot, n))
			{
				files++;
			}
			else
			{
				log.Warning($"Hero's Journey: part {n + 1} of the game's record ({FileName(dir, slot, n)}) is missing; drawing the rest.");
			}
		}
		Il2CppStructArray<ushort> live = god.positions;
		int liveCount = ReferenceEquals(live, null) ? 0 : Math.Min(filled, live.Length);
		if (liveCount > 0)
		{
			ushort[] raw = new ushort[liveCount];
			for (int i = 0; i < liveCount; i++)
			{
				raw[i] = live[i];
			}
			path.Add(raw, liveCount);
		}
		else if (ReadFile(path, dir, slot, current))
		{
			files++;
		}
		log.Msg($"Hero's Journey: {path.Count} points from save slot {slot}'s record ({files} file{((files == 1) ? "" : "s")}{((liveCount > 0) ? $" + {liveCount / 2} in memory" : "")}).");
		return null;
	}

	private static bool ReadFile(JourneyPath path, string dir, int slot, int n)
	{
		string file = FileName(dir, slot, n);
		if (!File.Exists(file))
		{
			return false;
		}
		byte[] bytes = File.ReadAllBytes(file);
		ushort[] raw = new ushort[bytes.Length / 2];
		Buffer.BlockCopy(bytes, 0, raw, 0, raw.Length * 2);
		path.Add(raw, raw.Length);
		return true;
	}

	private static string FileName(string dir, int slot, int n)
	{
		string name = SaveGod.GetPositionsPath(slot, n);
		return Path.IsPathRooted(name) ? name : Path.Combine(dir, name);
	}
}
