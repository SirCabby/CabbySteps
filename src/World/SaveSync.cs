using System;
using System.IO;
using System.Runtime.CompilerServices;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Keeps the completion marks (<see cref="PoiProgress"/>) in step with the game's own saves.
/// <list type="bullet">
/// <item>When the game loads a save, or starts a new game, the marks for that save are loaded.
/// The game does this at the title screen, and again after switching or deleting a save slot.</item>
/// <item>Each time the game writes its save file (<c>save&lt;slot&gt;.sav</c>, every few seconds of
/// play), the marks are written beside it as <c>CabbySteps_completed&lt;slot&gt;.json</c>.</item>
/// </list>
/// Marks made after the game's last save are lost if the game quits without saving again, just
/// like the game's own progress.
/// </summary>
public class SaveSync
{
	/// <summary>Seconds between looks at the game's save state.</summary>
	private const float PollInterval = 0.25f;

	private readonly MelonLogger.Instance _log;

	private readonly PoiProgress _progress;

	/// <summary>The game's save object (SaveGod.theSave) the marks were loaded for. Kept so its play
	/// time can still be read after the game has moved on to another save.</summary>
	private SaveFile _save;

	private IntPtr _savePtr;

	/// <summary>The game's save file for the attached slot.</summary>
	private string _savePath;

	private DateTime _saveStamp;

	private float _nextPoll;

	private int _errors;

	public SaveSync(MelonLogger.Instance log, PoiProgress progress)
	{
		_log = log;
		_progress = progress;
	}

	public void Update()
	{
		float now = Frame.Now;
		if (now < _nextPoll)
		{
			return;
		}
		_nextPoll = now + PollInterval;
		Poll();
	}

	/// <summary>Look now: catches a save the game wrote just before quitting.</summary>
	public void Flush()
	{
		Poll();
	}

	private void Poll()
	{
		try
		{
			PollGame();
		}
		catch (Exception ex)
		{
			// Most likely a game update changed its save code. Marks then only last the session.
			if (_errors++ < 3)
			{
				_log.Warning("Can't follow the game's saves, so completed POIs won't be kept: " + ex.Message);
			}
		}
	}

	// Kept out of line so that, if a game update breaks these interop types, the failure
	// surfaces inside Poll's try block rather than when Poll itself is compiled.
	[MethodImpl(MethodImplOptions.NoInlining)]
	private void PollGame()
	{
		// The game wrote the attached save's file: the marks go with it, at that save's own play
		// time. Checked first, so a write just before a reload still counts.
		if (_savePath != null && _save != null)
		{
			DateTime stamp = File.GetLastWriteTimeUtc(_savePath);
			if (stamp != _saveStamp)
			{
				_saveStamp = stamp;
				// A missing file is the save being deleted, not written.
				if (File.Exists(_savePath))
				{
					_progress.Commit(_save.timePlayed);
				}
			}
		}

		// A different save object means the game loaded a save or started a new game. The game
		// assigns theSave only when loading (saving updates it in place). It sets the slot number
		// earlier, while the old save is still loaded, so a slot change on its own means nothing.
		SaveFile save = SaveGod.theSave;
		if (save == null || save.Pointer == _savePtr)
		{
			return;
		}
		SaveGod god = SaveGod.me;
		if (god != null)
		{
			Attach(god, save);
		}
	}

	private void Attach(SaveGod god, SaveFile save)
	{
		PlatformService platform = god.platformService;
		if (platform == null)
		{
			// The game's storage isn't set up yet; try again at the next poll.
			return;
		}
		int slot = god.slot;
		string dir = platform.GetRootSaveDirectory();
		string saveName = SaveGod.GetSavePath(slot);
		if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(saveName))
		{
			return;
		}
		float timePlayed = save.timePlayed;
		_save = save;
		_savePtr = save.Pointer;
		_savePath = Path.Combine(dir, saveName);
		_saveStamp = File.GetLastWriteTimeUtc(_savePath);
		// A fresh save object (new game, or the slot's save was deleted) starts at zero play time.
		bool newGame = timePlayed <= 0f;
		string marks = Path.Combine(dir, ModPaths.CompletedFile(slot));
		// Kept under the mod's old name until now: it takes the new one (once; see RenamedFiles).
		ModPaths.CarryOver(Path.Combine(dir, ModPaths.OldCompletedFile(slot)), marks, _log);
		_progress.Attach(marks, saveName, timePlayed, newGame, (long)save.Pointer);
		if (newGame)
		{
			_log.Msg("Save slot " + slot + ": new game, so no POIs are completed yet.");
		}
		else
		{
			_log.Msg($"Save slot {slot} loaded ({FormatPlayTime(timePlayed)} played): {_progress.Count} POI{((_progress.Count == 1) ? "" : "s")} marked completed.");
		}
	}

	private static string FormatPlayTime(float seconds)
	{
		int minutes = (int)(seconds / 60f);
		return $"{minutes / 60}h {minutes % 60:00}m";
	}
}
