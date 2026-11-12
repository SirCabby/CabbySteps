using System.Diagnostics;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Time and screen size, read once a frame. Time.realtimeSinceStartup, Time.unscaledDeltaTime,
/// Time.frameCount and Screen.width/height are each a call into the game that leaves a boxed
/// number on its heap (see <see cref="Fast"/>); the mod used to ask dozens of times a frame. Time
/// here comes from the .NET clock, which never calls into the game.
/// </summary>
public static class Frame
{
	private static readonly long Origin = Stopwatch.GetTimestamp();

	private static long _last;

	/// <summary>Real seconds since the mod loaded (not slowed or frozen by the game's time scale).</summary>
	public static float Now => (float)((Stopwatch.GetTimestamp() - Origin) / (double)Stopwatch.Frequency);

	/// <summary>Real seconds since the previous frame, at most 0.1.</summary>
	public static float Dt { get; private set; } = 0.016f;

	/// <summary>Frames since the mod loaded.</summary>
	public static int Count { get; private set; }

	public static int ScreenWidth { get; private set; } = 1920;

	public static int ScreenHeight { get; private set; } = 1080;

	/// <summary>At the start of each frame (Core.OnUpdate).</summary>
	public static void Begin()
	{
		long now = Stopwatch.GetTimestamp();
		if (_last != 0)
		{
			Dt = Fast.Clamp((float)((now - _last) / (double)Stopwatch.Frequency), 0.0001f, 0.1f);
		}
		_last = now;
		// The screen size hardly ever changes: looked at twice a second or so.
		if (Count++ % 30 == 0)
		{
			ScreenWidth = Screen.width;
			ScreenHeight = Screen.height;
		}
	}
}
