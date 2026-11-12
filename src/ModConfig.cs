using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using MelonLoader;

namespace CabbySteps;

/// <summary>User settings, stored in UserData/CabbySteps_config.json.</summary>
public class ModConfig
{
	private MelonLogger.Instance _log;

	/// <summary>1 = map only, 2 = + minimap, 3 = + guide arrow.</summary>
	public int HelpLevel = 2;

	public float UiScale = 1f;

	/// <summary>0 = overhead marker on the target, 1 = 2D compass, 2 = 3D arrow above Nate.</summary>
	public int ArrowStyle;

	/// <summary>Height of the 3D arrow above Nate, in metres.</summary>
	public float HeadArrowHeight = 1.7f;

	public bool SeenIntro;

	/// <summary>Leave completed POIs off the map and the minimap.</summary>
	public bool HideCompleted;

	/// <summary>
	/// The Grippy feet cheat (F7): off, Grippy feet (planted feet can't slip and Nate keeps his
	/// balance), or Extreme grip (and a foot put down against anything holds there). Kept as it was
	/// left. In the file: "off", "grippy" or "extreme" (older files' true/false read as grippy/off).
	/// </summary>
	public GripLevel GrippyFeet;

	/// <summary>
	/// The Hold tight cheat (F8): what Nate holds stays in his hands and his hat stays on, whatever
	/// falls, wind or camps would do (swaps still work). Kept as it was left.
	/// </summary>
	public bool HoldTight;

	/// <summary>Log where memory goes once a minute (see MemoryDiagnostics). On while a leak is hunted.</summary>
	public bool DebugMemory = true;

	/// <summary>Seconds between memory reports.</summary>
	public float DebugMemoryInterval = 60f;

	/// <summary>
	/// Parts of the mod to switch off, to see which one a problem comes from: "links" (ticking off
	/// from game records, moving items), "savesync" (marks kept with the save), "minimap", "hud"
	/// (the 2D guide arrows), "headarrow" (the 3D arrow),
	/// "mappause" (freezing the game while the map is open), "gcwatchdog" (asking the game's GC to
	/// collect when it has stopped by itself, see GcWatchdog).
	/// </summary>
	public HashSet<string> DebugDisable = new HashSet<string>();

	public bool Off(string part)
	{
		return DebugDisable.Contains(part);
	}

	/// <summary>Load the config, writing a default file if there is none. Out-of-range values keep their defaults.</summary>
	public static ModConfig Load(MelonLogger.Instance log)
	{
		ModConfig config = new ModConfig
		{
			_log = log
		};
		try
		{
			if (File.Exists(ModPaths.Config))
			{
				Dictionary<string, object> o = MiniJson.ParseObject(File.ReadAllText(ModPaths.Config));
				if (o != null)
				{
					int helpLevel = (int)MiniJson.Num(o, "helpLevel");
					if (helpLevel >= 1 && helpLevel <= 3)
					{
						config.HelpLevel = helpLevel;
					}
					double uiScale = MiniJson.Num(o, "uiScale");
					if (uiScale > 0.1)
					{
						config.UiScale = (float)uiScale;
					}
					if (o.ContainsKey("arrowStyle"))
					{
						int arrowStyle = (int)MiniJson.Num(o, "arrowStyle");
						if (arrowStyle >= 0 && arrowStyle <= 2)
						{
							config.ArrowStyle = arrowStyle;
						}
					}
					if (o.ContainsKey("headArrowHeight"))
					{
						double height = MiniJson.Num(o, "headArrowHeight");
						if (height >= 0.2 && height <= 4.0)
						{
							config.HeadArrowHeight = (float)height;
						}
					}
					if (o.ContainsKey("seenIntro"))
					{
						config.SeenIntro = MiniJson.Bool(o, "seenIntro");
					}
					if (o.ContainsKey("hideCompleted"))
					{
						config.HideCompleted = MiniJson.Bool(o, "hideCompleted");
					}
					if (o.TryGetValue("grippyFeet", out object grip))
					{
						config.GrippyFeet = GripLevels.Read(grip);
					}
					if (o.TryGetValue("holdTight", out object hold))
					{
						config.HoldTight = HoldRules.Read(hold);
					}
					if (o.ContainsKey("debugMemory"))
					{
						config.DebugMemory = MiniJson.Bool(o, "debugMemory");
					}
					double interval = MiniJson.Num(o, "debugMemoryInterval");
					if (interval >= 10.0)
					{
						config.DebugMemoryInterval = (float)interval;
					}
					if (MiniJson.List(o, "debugDisable") is List<object> parts)
					{
						foreach (object part in parts)
						{
							if (part is string name)
							{
								config.DebugDisable.Add(name.Trim().ToLowerInvariant());
							}
						}
					}
				}
			}
			else
			{
				config.Save();
			}
		}
		catch (Exception ex)
		{
			log?.Error("Config load failed: " + ex);
		}
		return config;
	}

	public void Save()
	{
		try
		{
			StringBuilder sb = new StringBuilder();
			sb.Append("{\n");
			sb.Append("  \"helpLevel\": ").Append(HelpLevel.ToString(CultureInfo.InvariantCulture)).Append(",\n");
			sb.Append("  \"uiScale\": ").Append(UiScale.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
			sb.Append("  \"arrowStyle\": ").Append(ArrowStyle.ToString(CultureInfo.InvariantCulture)).Append(",\n");
			sb.Append("  \"headArrowHeight\": ").Append(HeadArrowHeight.ToString("0.###", CultureInfo.InvariantCulture)).Append(",\n");
			sb.Append("  \"seenIntro\": ").Append(SeenIntro ? "true" : "false").Append(",\n");
			sb.Append("  \"hideCompleted\": ").Append(HideCompleted ? "true" : "false").Append(",\n");
			sb.Append("  \"grippyFeet\": \"").Append(GripLevels.Name(GrippyFeet)).Append("\",\n");
			sb.Append("  \"holdTight\": ").Append(HoldTight ? "true" : "false").Append(",\n");
			sb.Append("  \"debugMemory\": ").Append(DebugMemory ? "true" : "false").Append(",\n");
			sb.Append("  \"debugMemoryInterval\": ").Append(DebugMemoryInterval.ToString("0", CultureInfo.InvariantCulture)).Append(",\n");
			sb.Append("  \"debugDisable\": [");
			bool first = true;
			foreach (string part in DebugDisable)
			{
				sb.Append(first ? "\"" : ", \"").Append(part).Append('"');
				first = false;
			}
			sb.Append("]\n");
			sb.Append("}\n");
			File.WriteAllText(ModPaths.Config, sb.ToString());
		}
		catch (Exception ex)
		{
			_log?.Error("Config save failed: " + ex);
		}
	}
}
