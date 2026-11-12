using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Resolution-independent UI sizing. Layout numbers are written for a 1080p
/// screen and scaled by screen height times the user's UI size setting.
/// </summary>
public static class Ui
{
	public const float RefHeight = 1080f;

	public static float Scale = 1f;

	/// <summary>The user's UI size multiplier (config uiScale, LB/RB on the map).</summary>
	public static float UserMul = 1f;

	/// <summary>Recompute Scale; called at the start of every OnGUI.</summary>
	public static void Refresh()
	{
		Scale = Fast.Clamp(Frame.ScreenHeight / RefHeight, 0.75f, 4f) * Fast.Clamp(UserMul, 0.5f, 3f);
	}

	public static float S(float v)
	{
		return v * Scale;
	}

	public static int Font(float v)
	{
		return Fast.Max(9, Fast.RoundToInt(v * Scale));
	}
}
