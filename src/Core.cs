using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(CabbySteps.Core), "CabbySteps", "0.6.0", "SirCabby & Slylos", null)]
[assembly: MelonGame("DefaultCompany", "BabySteps")]

namespace CabbySteps;

/// <summary>
/// The mod entry point. Builds the subsystems, routes input to whatever is active
/// (gameplay, or the full map and its guide) and draws the overlay.
/// </summary>
public class Core : MelonMod
{
	private ModContext _ctx;

	private Gamepad _pad;

	private FullMapScreen _fullMap;

	private HelpScreen _help;

	private MinimapRenderer _minimap;

	private HudArrow _hud;

	private HeadArrow3D _headArrow;

	private CheatBadge _badge;

	private SaveSync _saveSync;

	private MemoryDiagnostics _memory;

	private GcWatchdog _gcWatchdog;

	private bool _mapOpen;

	private bool _minimapHidden;

	private bool _paused;

	private float _prevTimeScale = 1f;

	/// <summary>The map closed because the game's menu took over: leave the time scale to the game.</summary>
	private bool _leaveTimeScale;

	private bool _arrowHeightDirty;

	/// <summary>Another mod named BabyStepsMinimap is loaded too: this one under its old name, or the one it grew from.</summary>
	private bool _oldModLoaded;

	public override void OnInitializeMelon()
	{
		KeepAlive.Log = LoggerInstance;
		GuiTextures.Init();
		// The mod was BabyStepsMinimap before it was CabbySteps: settings and saved locations kept
		// under that name take the new one (a save slot's completed POIs do as its save loads).
		ModPaths.CarryOver(ModPaths.OldConfig, ModPaths.Config, LoggerInstance);
		ModPaths.CarryOver(ModPaths.OldLocations, ModPaths.Locations, LoggerInstance);
		ModConfig config = ModConfig.Load(LoggerInstance);
		Ui.UserMul = config.UiScale;
		_pad = new Gamepad();
		// The map is the game's own: its ending's painted sheets, laid out as journey.json says, with
		// everything put on them as the game itself puts Nate there.
		MapArt art = new MapArt(LoggerInstance);
		art.LoadLayout(ModPaths.Journey);
		_ctx = new ModContext
		{
			Log = LoggerInstance,
			Config = config,
			Map = new MapAssets(LoggerInstance),
			Art = art,
			Projection = art.Projection,
			Player = new PlayerTracker(),
			Controls = new Controls(_pad),
			Pointer = new Pointer(),
			Progress = new PoiProgress(LoggerInstance),
			Toasts = new Toasts()
		};
		_ctx.Map.Load(_ctx.Projection);
		_ctx.Progress.SetUnmarkable(_ctx.Map.Pois.Where(p => !p.HandMarkable).Select(p => p.Id));
		_ctx.Links = new GameLinks(_ctx);
		_ctx.Locations = new SavedLocations(ModPaths.Locations, _ctx.Projection.WorldToMap, LoggerInstance);
		_ctx.Locations.Load();
		_ctx.Recall = new Recall(_ctx);
		_ctx.Summoner = new Summoner(_ctx);
		_fullMap = new FullMapScreen(_ctx);
		_help = new HelpScreen(_ctx);
		_minimap = new MinimapRenderer(_ctx);
		_hud = new HudArrow(_ctx);
		_headArrow = new HeadArrow3D(LoggerInstance);
		_badge = new CheatBadge();
		_saveSync = new SaveSync(LoggerInstance, _ctx.Progress);
		_gcWatchdog = new GcWatchdog(LoggerInstance);
		if (config.DebugMemory)
		{
			_memory = new MemoryDiagnostics(LoggerInstance, _ctx, config.DebugMemoryInterval, _gcWatchdog);
			LoggerInstance.Msg($"Memory debugging on: a [mem] report every {config.DebugMemoryInterval:0} s in this log, F10 for one now, F11 to force a full collection first.");
		}
		if (config.DebugDisable.Count > 0)
		{
			LoggerInstance.Msg("Switched off for testing (debugDisable): " + string.Join(", ", config.DebugDisable));
		}
		GameBridge.Init(LoggerInstance);
		// Going back to a saved location stands Nate on the footing he was saved on, keeps what he
		// holds and wears with him, and doesn't start a gym's time trial over.
		StanceRestore.Install(HarmonyInstance, LoggerInstance);
		CarryGuard.Install(HarmonyInstance, LoggerInstance);
		GymGuard.Install(HarmonyInstance, LoggerInstance);
		// The Grippy feet cheat (F7) hooks the game only once it's first switched on, and is on from
		// the start (at the level it was left at) if it was left on.
		GrippyFeet.Init(LoggerInstance);
		if (config.GrippyFeet != GripLevel.Off)
		{
			string why = GrippyFeet.Switch(config.GrippyFeet);
			if (why != null && config.GrippyFeet == GripLevel.Extreme)
			{
				LoggerInstance.Warning("Extreme grip was left on, but " + why + "; Grippy feet is on instead.");
				why = GrippyFeet.Switch(GripLevel.Grippy);
			}
			if (why != null)
			{
				LoggerInstance.Warning("Grippy feet was left on, but " + why + ".");
			}
		}
		// The Hold tight cheat (F8) likewise: hooked once it's first switched on, on from the start if
		// it was left on. Its letting go goes through CarryGuard's hooks, made above.
		HoldTight.Init(LoggerInstance);
		if (config.HoldTight)
		{
			string why = HoldTight.Switch(true);
			if (why != null)
			{
				LoggerInstance.Warning("Hold tight was left on, but " + why + ".");
			}
		}
		if (File.Exists(ModPaths.LegacyCalibration))
		{
			LoggerInstance.Msg("Calibration is no longer used: positions come from the game's own coordinates. UserData/" + Path.GetFileName(ModPaths.LegacyCalibration) + " can be deleted.");
		}
		if (File.Exists(ModPaths.LegacyTrail))
		{
			LoggerInstance.Msg("The mod no longer records its own trail: the Hero's Journey is the game's own record, drawn as its ending draws it. UserData/" + Path.GetFileName(ModPaths.LegacyTrail) + " can be deleted.");
		}
		LoggerInstance.Msg("CabbySteps 0.5.0 ready.");
		LoggerInstance.Msg("Controller: View=map, L3=first-person, D-pad L/R=help level, D-pad Up=arrow style.");
		LoggerInstance.Msg("In map: D-pad U/D=zoom, L-stick=pan, A=track POI, Y=mark completed, B=clear, X=find Nate, Dpad-Left=Hero's Journey, Dpad-Right=guide, View=close.");
		LoggerInstance.Msg("Mouse in map: hover=name, click=track, right-click=mark completed, drag=pan, wheel=zoom. R3/H=hide completed. Completed POIs are kept with each game save.");
		LoggerInstance.Msg("Saved locations: L3 in the map (F5 there) saves where Nate is, which way he faces and where his feet stand as a new location (a red pin, numbered until it's named), kept until forgotten; F5 while playing is the quick save (one slot, saved over each time) and F9 anywhere (even when he's down after a fall) goes back to it; on the map LT/RT (Q/E) picks one, hold Y (Space/right-click) on it to go there, hold X (Delete) to forget it, hold A (F2) on a red one to name it (its pin then shows the name's initials).");
		LoggerInstance.Msg("Movable items (hats, key and quest items, chicks and ducklings): on one in the map, hold Y (Space/right-click) to bring it to Nate.");
		LoggerInstance.Msg("Cheat: F7 (playing or on the map) steps Grippy feet through off, Grippy feet (planted feet can't slip, and Nate keeps his balance while a foot is down) and Extreme grip (and a foot put down against anything, a wall or a cliff face, holds right there, so he can walk up sheer walls). It stays as it was left (config grippyFeet); a GRIPPY FEET or EXTREME GRIP tag shows while it's on.");
		LoggerInstance.Msg("Cheat: F8 (playing or on the map) switches Hold tight on or off: what Nate holds stays in his hands and his hat stays on, whatever falls, the wind or a camp's night would do (picking up something else or putting on another hat still swaps). It stays as it was left (config holdTight); a HOLD TIGHT tag shows while it's on.");
		LoggerInstance.Msg($"Help level {config.HelpLevel}, UI scale {config.UiScale:0.0}x, map=the game's {_ctx.Art.Sheets.Count} painted sheets, saved locations={_ctx.Locations.Count}, quick save={((_ctx.Locations.Quick != null) ? "yes" : "none yet")}.");
	}

	public override void OnLateInitializeMelon()
	{
		// Every mod in Mods/ is loaded by now. Before it was CabbySteps this mod was
		// Mods/BabyStepsMinimap.dll, as was the one it grew from; a copy of either left there runs
		// alongside, with a second map on the same buttons.
		MelonBase old = MelonBase.RegisteredMelons.FirstOrDefault(m => m != this && m.Info?.Name == RenamedFiles.OldName);
		if (old != null)
		{
			_oldModLoaded = true;
			string file = Path.GetFileName(old.MelonAssembly?.Location);
			if (string.IsNullOrEmpty(file))
			{
				file = RenamedFiles.OldName + ".dll";
			}
			LoggerInstance.Warning($"Mods/{file} ({old.Info.Name} {old.Info.Version} by {old.Info.Author}) is loaded as well: it's this mod under its old name, or the mod it grew from, and it draws a second map on the same buttons. Delete Mods/{file} and the Mods/{RenamedFiles.OldName} folder while the game is closed.");
		}
		else if (Directory.Exists(ModPaths.OldAssetDir))
		{
			LoggerInstance.Msg($"Mods/{RenamedFiles.OldName}/ is from before the mod was called CabbySteps, and nothing in it is read any more (the mod's data is in Mods/CabbySteps/, and the map is the game's own): it can be deleted.");
		}
	}

	public override void OnFixedUpdate()
	{
		// A trip waiting to set off holds Nate still where he is, every physics step.
		_ctx?.Recall?.FixedUpdate();
	}

	public override void OnApplicationQuit()
	{
		// The game may have saved on the way out; if so, keep the marks with that save.
		_saveSync?.Flush();
	}

	public override void OnUpdate()
	{
		// Time, screen size and whether any key is down, read once for the whole frame.
		Frame.Begin();
		Controls.BeginFrame();
		GrippyFeet.Tick();
		HoldTight.Tick();
		long allocStart = _memory?.ProbeStart() ?? 0;
		_ctx.Player.Update();
		_pad.Poll();
		// The map's paintings: asked for once the game is under way (they stay), picked up as they arrive.
		if (!_ctx.Art.SheetsRequested && (_ctx.Player.Found || _mapOpen))
		{
			_ctx.Art.RequestSheets();
		}
		_ctx.Art.Update();
		ModConfig config = _ctx.Config;
		_memory?.Update(_mapOpen);
		if (!config.Off("gcwatchdog"))
		{
			_gcWatchdog.Update();
		}
		if (!config.Off("savesync"))
		{
			_saveSync.Update();
		}
		if (!config.Off("links"))
		{
			_ctx.Links.Update();
		}
		bool gameMenu = GameBridge.GameMenuOpen();
		if (_mapOpen && gameMenu)
		{
			// The game's own menu opened (pause, a reload): get out of its way.
			SetMapOpen(open: false, gameTookOver: true);
		}
		// A recall asked for on the map starts here, the frame after the map has closed.
		_ctx.Recall.Update(_mapOpen);
		// Items on their way to Nate (the map can stay open: the game keeps loading while frozen).
		_ctx.Summoner.Update();
		// While a location's name is being typed on the map, the keyboard is for that: M is a letter,
		// and F9, F7 and F8 wait. (The pad's View still closes the map, leaving the name as it was.)
		bool typing = _mapOpen && _fullMap.Renaming;
		// F9, in play or on the map: back to the quick save (F5 while playing saves it). On the map,
		// it closes below once the recall is pending.
		if (!typing && Controls.KeyDown(KeyCode.F9))
		{
			_ctx.GoToQuickSave();
		}
		// F7, in play or on the map: the Grippy feet cheat's next level (off, Grippy feet, Extreme grip).
		if (!typing && Controls.KeyDown(KeyCode.F7))
		{
			_ctx.CycleGrippyFeet();
		}
		// F8, in play or on the map: the Hold tight cheat on or off.
		if (!typing && Controls.KeyDown(KeyCode.F8))
		{
			_ctx.ToggleHoldTight();
		}
		if (_ctx.Controls.Pressed(Gamepad.BACK, typing ? KeyCode.None : KeyCode.M))
		{
			if (!_mapOpen && gameMenu)
			{
				_ctx.Toasts.Show("Close the game's menu first, then open the map");
			}
			else if (!_mapOpen && _ctx.Recall.Busy)
			{
				// The map would freeze the game while it's moving Nate.
				_ctx.Toasts.Show("Nate's on his way - open the map once he's there");
			}
			else
			{
				SetMapOpen(!_mapOpen);
			}
		}
		GameBridge.MaintainFirstPerson();
		if (_mapOpen)
		{
			UpdateMap();
		}
		else
		{
			UpdateGameplay();
		}
		if (_mapOpen && _ctx.Recall.Pending)
		{
			// Going to a saved location: the game has to be running to move Nate.
			SetMapOpen(open: false);
		}
		bool show3DArrow = _ctx.HelpLevel >= 3 && _ctx.Target != null && !_mapOpen && _ctx.Config.ArrowStyle == 2 && !config.Off("headarrow");
		_headArrow.Sync(show3DArrow, _ctx.Player, _ctx.Target, _ctx.Config.HeadArrowHeight);
		UpdatePause();
		_memory?.ProbeEnd(allocStart, MemoryDiagnostics.Section.Update);
	}

	private void UpdateMap()
	{
		// Typing a name, WASD are letters: they don't hand the map back from the mouse.
		_ctx.Pointer.Update(!_fullMap.Renaming && _ctx.Controls.CrosshairInput());
		if (_help.Visible)
		{
			_help.Update();
		}
		else if (!_fullMap.JourneyOpen && !_fullMap.Renaming && _ctx.Controls.Pressed(Gamepad.DPAD_RIGHT, KeyCode.Slash))
		{
			_help.Open();
		}
		else
		{
			_fullMap.Update(Frame.Dt);
		}
	}

	private void UpdateGameplay()
	{
		HandleFirstPerson();
		Controls c = _ctx.Controls;
		ModConfig config = _ctx.Config;
		// Hold X + D-pad Up/Down to raise/lower the 3D arrow; saved on release.
		bool adjustingHeight = _ctx.HelpLevel >= 3 && config.ArrowStyle == 2 && _pad.Down(Gamepad.X);
		if (adjustingHeight)
		{
			float dt = Frame.Dt;
			if (dt <= 0f)
			{
				dt = 0.016f;
			}
			bool up = _pad.Down(Gamepad.DPAD_UP);
			bool down = _pad.Down(Gamepad.DPAD_DOWN);
			if (up)
			{
				AdjustArrowHeight(1.2f * dt);
			}
			if (down)
			{
				AdjustArrowHeight(-1.2f * dt);
			}
			if (!up && !down)
			{
				CommitArrowHeight();
			}
			return;
		}
		CommitArrowHeight();
		if (_ctx.HelpLevel >= 3 && config.ArrowStyle == 2)
		{
			if (Controls.KeyDown(KeyCode.Quote))
			{
				AdjustArrowHeight(0.1f);
				CommitArrowHeight();
			}
			if (Controls.KeyDown(KeyCode.Semicolon))
			{
				AdjustArrowHeight(-0.1f);
				CommitArrowHeight();
			}
		}
		if (c.Pressed(Gamepad.DPAD_RIGHT, KeyCode.RightBracket))
		{
			CycleLevel(1);
		}
		if (c.Pressed(Gamepad.DPAD_LEFT, KeyCode.LeftBracket))
		{
			CycleLevel(-1);
		}
		if (c.Pressed(Gamepad.DPAD_UP, KeyCode.H))
		{
			CycleArrowStyle();
		}
		if (Controls.KeyDown(KeyCode.F6))
		{
			_minimapHidden = !_minimapHidden;
		}
		// F5 while playing: the quick save, saved over each time. (On the map, F5 or L3 saves a
		// numbered location instead, kept until it's forgotten.)
		if (Controls.KeyDown(KeyCode.F5))
		{
			_ctx.QuickSave();
		}
	}

	/// <summary>L3 (or F) toggles first person.</summary>
	private void HandleFirstPerson()
	{
		if (_pad.Pressed(Gamepad.L3) || Controls.KeyDown(KeyCode.F))
		{
			if (_ctx.Recall.Busy)
			{
				// First person is set aside while the game moves Nate, and comes back after.
				_ctx.Toasts.Show("First person: wait until Nate's there");
				return;
			}
			_ctx.Toasts.Show(GameBridge.ToggleFirstPerson(out var _));
		}
	}

	private void AdjustArrowHeight(float delta)
	{
		ModConfig config = _ctx.Config;
		float height = Fast.Clamp(config.HeadArrowHeight + delta, 0.3f, 3.5f);
		if (Fast.Approximately(height, config.HeadArrowHeight))
		{
			return;
		}
		config.HeadArrowHeight = height;
		_arrowHeightDirty = true;
		_ctx.Toasts.Show($"Arrow height {height:0.00} m");
	}

	private void CommitArrowHeight()
	{
		if (!_arrowHeightDirty)
		{
			return;
		}
		_arrowHeightDirty = false;
		_ctx.Config.Save();
	}

	/// <param name="gameTookOver">The game's menu opened over the map, so it now owns the time scale.</param>
	private void SetMapOpen(bool open, bool gameTookOver = false)
	{
		if (open == _mapOpen)
		{
			return;
		}
		_mapOpen = open;
		_help.Hide();
		if (open)
		{
			_fullMap.Open();
			_ctx.Pointer.Open();
			if (_oldModLoaded)
			{
				_ctx.Toasts.Show("Two maps? Delete Mods/" + RenamedFiles.OldName + ".dll: an older map mod is running too");
			}
		}
		else
		{
			_fullMap.Close();
			_ctx.Pointer.Close();
			_leaveTimeScale = gameTookOver;
		}
	}

	private void CycleLevel(int delta)
	{
		int level = Fast.Clamp(_ctx.HelpLevel + delta, 1, 3);
		if (level == _ctx.HelpLevel)
		{
			return;
		}
		_ctx.Config.HelpLevel = level;
		_ctx.Config.Save();
		_ctx.Toasts.Show("Help: " + LevelName(level));
		LoggerInstance.Msg("Help level -> " + level);
	}

	private void CycleArrowStyle()
	{
		ModConfig config = _ctx.Config;
		config.ArrowStyle = (config.ArrowStyle + 1) % 3;
		config.Save();
		string hint = (_ctx.HelpLevel < 3) ? "  (set help to Lv3 to see it)" : "";
		_ctx.Toasts.Show("Lv3 arrow: " + ArrowStyleName(config.ArrowStyle) + hint);
		LoggerInstance.Msg("Arrow style -> " + config.ArrowStyle);
	}

	private static string ArrowStyleName(int style)
	{
		switch (style)
		{
		case 0:
			return "Overhead marker (on target)";
		case 1:
			return "2D compass (top-center)";
		case 2:
			return "3D arrow (above Nate)";
		default:
			return style.ToString();
		}
	}

	private static string LevelName(int level)
	{
		switch (level)
		{
		case 1:
			return "Lv1 - Map only";
		case 2:
			return "Lv2 - Map + Minimap";
		default:
			return "Lv3 - Map + Minimap + Arrow";
		}
	}

	/// <summary>Freeze the game (timeScale 0) while the full map is open.</summary>
	private void UpdatePause()
	{
		if (_mapOpen && !_paused && !_ctx.Config.Off("mappause"))
		{
			_prevTimeScale = Time.timeScale;
			Time.timeScale = 0f;
			_paused = true;
		}
		else if (!_mapOpen && _paused)
		{
			if (!_leaveTimeScale)
			{
				Time.timeScale = _prevTimeScale;
			}
			_leaveTimeScale = false;
			_paused = false;
		}
	}

	public override void OnGUI()
	{
		// IMGUI calls OnGUI for layout and for every input event as well as to draw, and drawing
		// only does anything in the Repaint pass: everything else was the same work for nothing.
		if (Event.current.type != EventType.Repaint)
		{
			return;
		}
		MemoryDiagnostics.OnGuiCalls++;
		long allocStart = _memory?.ProbeStart() ?? 0;
		// A known start, so drawing never reads GUI.matrix back (see Gfx.Rotation).
		GUI.matrix = Gfx.Identity;
		Ui.Refresh();
		if (!_ctx.Map.Ready)
		{
			GUI.Box(new Rect(10f, 10f, 260f, 46f), "CabbySteps");
			GUI.Label(new Rect(20f, 32f, 320f, 20f), "Assets missing - check Mods/CabbySteps");
			return;
		}
		if (_mapOpen)
		{
			_fullMap.Draw(interactive: !_help.Visible);
			if (_help.Visible)
			{
				_help.Draw();
			}
		}
		else
		{
			if (_ctx.HelpLevel >= 2 && !_minimapHidden && !_ctx.Config.Off("minimap"))
			{
				_minimap.Draw((_ctx.HelpLevel >= 3) ? _ctx.Target : null);
			}
			_badge.Draw();
			if (_ctx.HelpLevel >= 3 && _ctx.Target != null && !_ctx.Config.Off("hud"))
			{
				if (_ctx.Config.ArrowStyle == 0)
				{
					_hud.Draw(overhead: true);
				}
				else if (_ctx.Config.ArrowStyle == 1)
				{
					_hud.Draw(overhead: false);
				}
			}
		}
		_ctx.Toasts.Draw();
		if (_mapOpen && _ctx.Pointer.Active)
		{
			FullMapScreen.DrawPointer(_ctx.Pointer.Position);
		}
		_memory?.ProbeEnd(allocStart, _mapOpen ? MemoryDiagnostics.Section.MapGui : MemoryDiagnostics.Section.Gui);
	}
}
