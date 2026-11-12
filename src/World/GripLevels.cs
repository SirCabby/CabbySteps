namespace CabbySteps;

/// <summary>How much of the Grippy feet cheat is on (F7 steps through them).</summary>
public enum GripLevel
{
	Off,
	/// <summary>Grippy feet: planted feet can't slip, and he keeps his balance while a foot is down.</summary>
	Grippy,
	/// <summary>Extreme grip: Grippy feet, and a foot put down against anything holds there.</summary>
	Extreme
}

/// <summary>The cheat's levels: the order F7 steps through them, and how the config file keeps them. Game-free, checked offline.</summary>
public static class GripLevels
{
	/// <summary>The level F7 goes to from <paramref name="level"/>: off, Grippy feet, Extreme grip, off again.</summary>
	public static GripLevel Next(GripLevel level)
	{
		switch (level)
		{
			case GripLevel.Off:
				return GripLevel.Grippy;
			case GripLevel.Grippy:
				return GripLevel.Extreme;
			default:
				return GripLevel.Off;
		}
	}

	/// <summary>The config file's "grippyFeet": "off", "grippy" or "extreme"; true and false from before Extreme grip; 0 to 2.</summary>
	public static GripLevel Read(object value)
	{
		switch (value)
		{
			case bool on:
				return on ? GripLevel.Grippy : GripLevel.Off;
			case double level:
				return (level >= 1.5) ? GripLevel.Extreme : ((level >= 0.5) ? GripLevel.Grippy : GripLevel.Off);
			case string name:
				switch (name.Trim().ToLowerInvariant())
				{
					case "grippy":
					case "on":
						return GripLevel.Grippy;
					case "extreme":
						return GripLevel.Extreme;
				}
				break;
		}
		return GripLevel.Off;
	}

	/// <summary>How the config file says a level: "off", "grippy" or "extreme".</summary>
	public static string Name(GripLevel level)
	{
		switch (level)
		{
			case GripLevel.Grippy:
				return "grippy";
			case GripLevel.Extreme:
				return "extreme";
			default:
				return "off";
		}
	}
}
