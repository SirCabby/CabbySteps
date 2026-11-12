using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>State and services shared by the screens.</summary>
public class ModContext
{
	public MelonLogger.Instance Log;

	public ModConfig Config;

	/// <summary>The POIs (pois.json).</summary>
	public MapAssets Map;

	/// <summary>The game's own painted map sheets: the map, the minimap and the Hero's Journey's paper.</summary>
	public MapArt Art;

	/// <summary>World position to map pixel, as the game itself puts Nate on those sheets.</summary>
	public MapProjection Projection;

	public PlayerTracker Player;

	public Controls Controls;

	/// <summary>The mouse on the full map.</summary>
	public Pointer Pointer;

	/// <summary>POIs marked completed, kept with the game's save.</summary>
	public PoiProgress Progress;

	/// <summary>What the game records as found, and where movable items really are.</summary>
	public GameLinks Links;

	public Toasts Toasts;

	/// <summary>Places Nate was saved at, and where he was before the last recall.</summary>
	public SavedLocations Locations;

	/// <summary>Sends Nate back to a saved location.</summary>
	public Recall Recall;

	/// <summary>Brings movable items (hats, key and quest items, chicks and ducklings) to Nate.</summary>
	public Summoner Summoner;

	/// <summary>The POI being tracked, or null.</summary>
	public Poi Target;

	/// <summary>1 = map only, 2 = + minimap, 3 = + guide arrow.</summary>
	public int HelpLevel => Config.HelpLevel;

	/// <summary>Nate has been found, so he can be shown on the map.</summary>
	public bool CanLocate => Player.Found;

	/// <summary>Where the target is relative to Nate, taking the short way round the world loop.</summary>
	public Vector3 TargetDelta()
	{
		return WorldLoop.Delta(Player.WorldPos, Target.World);
	}

	public float TargetDistance()
	{
		return WorldLoop.FlatDistance(Player.WorldPos, Target.World);
	}

	public void Track(Poi poi)
	{
		Target = poi;
		Toasts.Show((HelpLevel < 3) ? ("Tracking: " + poi.Title + "  (set help to Lv3 for the arrow)") : ("Tracking: " + poi.Title));
	}

	/// <summary>Mark a POI completed, or not; completing the tracked POI stops tracking it.</summary>
	public void ToggleDone(Poi poi)
	{
		if (Progress.IsFound(poi))
		{
			Toasts.Show(poi.Title + ": the game records it as found, so it stays ticked off");
			return;
		}
		if (poi.ReferenceOnly)
		{
			Toasts.Show(poi.Title + " is a landmark, just for reference: nothing to tick off");
			return;
		}
		if (Progress.IsUnmarkable(poi))
		{
			Toasts.Show(poi.Found != null
				? poi.Title + " ticks itself off once its cutscene has played"
				: poi.Title + " moves about, so it isn't ticked off: its point shows where it is");
			return;
		}
		bool done = Progress.Toggle(poi);
		string message = (done ? "Completed: " : "Not completed: ") + poi.Title;
		if (done && Target == poi)
		{
			Target = null;
			message += "  (tracking cleared)";
		}
		if (!Progress.Attached)
		{
			message += "  - no save loaded, so it won't be kept";
		}
		Toasts.Show(message);
	}

	/// <summary>Show or hide completed POIs on the map and the minimap.</summary>
	public void ToggleHideCompleted()
	{
		Config.HideCompleted = !Config.HideCompleted;
		Config.Save();
		Toasts.Show(Config.HideCompleted ? "Completed points hidden" : "Completed points shown");
	}

	/// <summary>
	/// F7: the Grippy feet cheat's next level: off, Grippy feet (planted feet can't slip, and Nate
	/// keeps his balance), Extreme grip (and a foot put down against anything holds there), off.
	/// It's kept in the config, so it stays as it is next time the game starts.
	/// </summary>
	public void CycleGrippyFeet()
	{
		GripLevel next = GripLevels.Next(GrippyFeet.Level);
		string why = GrippyFeet.Switch(next);
		if (why != null && next == GripLevel.Extreme)
		{
			// Extreme grip can't be had: on round to off rather than sticking at Grippy feet.
			Toasts.Show("Extreme grip: " + why);
			next = GripLevel.Off;
			why = GrippyFeet.Switch(next);
		}
		if (why != null)
		{
			Toasts.Show("Grippy feet: " + why);
			return;
		}
		Config.GrippyFeet = next;
		Config.Save();
		switch (next)
		{
			case GripLevel.Grippy:
				Toasts.Show("Grippy feet ON - planted feet can't slip, and Nate keeps his balance (F7: extreme grip)");
				break;
			case GripLevel.Extreme:
				Toasts.Show("EXTREME GRIP - put a foot down against anything and it holds: walk up walls (F7: off)");
				break;
			default:
				Toasts.Show("Grippy feet off");
				break;
		}
	}

	/// <summary>
	/// F8: the Hold tight cheat on or off: what Nate holds stays in his hands and his hat stays on,
	/// whatever falls, the wind or a camp's night would do; swapping still works. It's kept in the
	/// config, so it stays as it is next time the game starts.
	/// </summary>
	public void ToggleHoldTight()
	{
		bool next = !HoldTight.On;
		string why = HoldTight.Switch(next);
		if (why != null)
		{
			Toasts.Show("Hold tight: " + why);
			return;
		}
		Config.HoldTight = next;
		Config.Save();
		Toasts.Show(next
			? "Hold tight ON - falls, wind and camps can't take what Nate holds or his hat (swapping still works)"
			: "Hold tight off");
	}

	/// <summary>Leave this POI off the map: the item has left the game, or it's completed and completed POIs are hidden.</summary>
	public bool Hidden(Poi poi)
	{
		return Progress.IsGone(poi) || (Config.HideCompleted && Progress.IsDone(poi));
	}

	/// <summary>
	/// On the map (L3, F5): save where Nate is as a new numbered location, kept until it's
	/// forgotten. Null if he can't be saved right now.
	/// </summary>
	public SavedLocation SaveLocation()
	{
		if (!ReadNateToSave(out Vector3 position, out SavedPose pose))
		{
			return null;
		}
		SavedLocation location = Locations.Add(position, pose);
		Toasts.Show("Saved " + location.Title + ((Locations.Count == 1) ? "  - it stays on the map until you forget it" : ""));
		Log.Msg($"Saved {location.Title} at ({position.x:0.0}, {position.y:0.0}, {position.z:0.0}){PoseNote(pose)}.");
		return location;
	}

	/// <summary>
	/// F5 while playing: save where Nate is as the quick save, over the one there was (there's
	/// only ever one), for F9 to go back to. Null if he can't be saved right now.
	/// </summary>
	public SavedLocation QuickSave()
	{
		if (!ReadNateToSave(out Vector3 position, out SavedPose pose))
		{
			return null;
		}
		bool first = Locations.Quick == null;
		SavedLocation quick = Locations.SetQuick(position, pose);
		Toasts.Show(first ? "Quick saved  - F9 brings Nate back here" : "Quick saved");
		Log.Msg($"Quick saved at ({position.x:0.0}, {position.y:0.0}, {position.z:0.0}){PoseNote(pose)}.");
		return quick;
	}

	/// <summary>Where Nate is, which way he and the camera face and how he stands, for saving. False (with a toast saying why) if he can't be saved now.</summary>
	private bool ReadNateToSave(out Vector3 position, out SavedPose pose)
	{
		pose = default;
		if (Recall.Busy)
		{
			position = default;
			Toasts.Show("Nate's on his way somewhere - save once he's there");
			return false;
		}
		if (!Recall.TryGetNatePosition(out position))
		{
			Toasts.Show("Nate isn't here yet - nothing to save");
			return false;
		}
		// Which way he and the camera face too, so going back puts him there just so.
		Recall.TryGetNatePose(out pose);
		return true;
	}

	private static string PoseNote(SavedPose pose)
	{
		return (pose.Known ? $", facing {pose.Facing:0}°, camera {pose.Camera:0}°" : "") + FeetNote(pose);
	}

	private static string FeetNote(SavedPose pose)
	{
		string body = (pose.Body != null) ? $", body of {pose.Body.Parts.Length} parts" : "";
		switch (pose.Feet)
		{
		case 3:
			return ", both feet planted" + body;
		case 1:
		case 2:
			return ", one foot planted" + body;
		default:
			return pose.Known ? ", no foot planted" : "";
		}
	}

	/// <summary>Forget a saved location; forgetting the tracked one stops tracking it.</summary>
	public void ForgetLocation(SavedLocation location)
	{
		if (!Locations.Remove(location))
		{
			return;
		}
		string message = "Forgot " + location.Phrase;
		if (Target == location)
		{
			Target = null;
			message += "  (tracking cleared)";
		}
		Toasts.Show(message);
		Log.Msg("Forgot " + location.Title + ".");
	}

	/// <summary>
	/// Give a kept location the name typed for it on the map; with an empty one, it's numbered
	/// again ("Location 3"). Says what it's called now.
	/// </summary>
	public void RenameLocation(SavedLocation location, string name)
	{
		string was = location.Title;
		if (!Locations.Rename(location, name))
		{
			Toasts.Show("Name unchanged: " + location.Title);
			return;
		}
		Toasts.Show(was + " is now called " + location.Title);
		Log.Msg($"Renamed \"{was}\" to \"{location.Title}\".");
	}

	/// <summary>F9: send Nate back to the quick save. Says why if it can't.</summary>
	public void GoToQuickSave()
	{
		SavedLocation quick = Locations.Quick;
		if (quick == null)
		{
			Toasts.Show("No quick save to go back to yet - F5 while playing makes one");
			return;
		}
		GoTo(quick);
	}

	/// <summary>Send Nate to a saved location. False (with a toast saying why) if that can't happen now.</summary>
	public bool GoTo(SavedLocation location)
	{
		if (Summoner.Busy)
		{
			// It's being put down beside Nate where he is now.
			Toasts.Show("Can't go there now: wait until " + Summoner.Current.Title + " has turned up");
			return false;
		}
		string why = Recall.Request(location);
		if (why != null)
		{
			Toasts.Show("Can't go there now: " + why);
			return false;
		}
		return true;
	}

	/// <summary>
	/// Bring a movable item (a hat, a key or quest item, a chick or duckling) to Nate. It says so
	/// once it's there; false (with a toast saying why) if it can't come now.
	/// </summary>
	public bool Summon(Poi poi)
	{
		string why = Summoner.Request(poi);
		if (why != null)
		{
			Toasts.Show("Can't bring " + poi.Title + ": " + why);
			return false;
		}
		return true;
	}

	public void AdjustUiScale(float delta)
	{
		Config.UiScale = Fast.Clamp(Config.UiScale + delta, 0.5f, 3f);
		Ui.UserMul = Config.UiScale;
		Config.Save();
		Toasts.Show($"UI size {Config.UiScale:0.0}x");
	}
}
