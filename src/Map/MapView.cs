using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Screen &lt;-&gt; map pixel conversion for a view that draws map pixel <c>centerPx</c> at
/// screen point <c>screenCenter</c>, at <c>scale</c> screen pixels per map pixel.
/// </summary>
public static class MapView
{
	public static Vector2 ToScreen(Vector2 px, Vector2 screenCenter, float scale, Vector2 centerPx)
	{
		return Fast.V2(screenCenter.x + (px.x - centerPx.x) * scale, screenCenter.y + (px.y - centerPx.y) * scale);
	}

	public static Vector2 ToMap(Vector2 screen, Vector2 screenCenter, float scale, Vector2 centerPx)
	{
		return Fast.V2(centerPx.x + (screen.x - screenCenter.x) / scale, centerPx.y + (screen.y - screenCenter.y) / scale);
	}

	/// <summary>
	/// The centre pixel after zooming from <paramref name="oldScale"/> to <paramref name="newScale"/>
	/// such that the map stays put under screen point <paramref name="anchor"/>.
	/// </summary>
	public static Vector2 ZoomAbout(Vector2 anchor, Vector2 screenCenter, float oldScale, float newScale, Vector2 centerPx)
	{
		Vector2 fixedPx = ToMap(anchor, screenCenter, oldScale, centerPx);
		return Fast.V2(fixedPx.x - (anchor.x - screenCenter.x) / newScale, fixedPx.y - (anchor.y - screenCenter.y) / newScale);
	}
}
