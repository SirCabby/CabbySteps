using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Help level 2+: a square minimap in the top-right corner, on the game's painted map, centred on Nate (north up),
/// with POIs, saved locations, the tracked target and a "Tracking" label with the distance below it.
/// </summary>
public class MinimapRenderer
{
	/// <summary>Panel size in reference (1080p) pixels.</summary>
	public float BasePanel = 320f;

	/// <summary>How many metres of map the panel spans (across; up it, metres of the map's slanted view).</summary>
	public float CropMetres = 128f;

	public float BaseMargin = 12f;

	private static readonly Color DoneDot = Gfx.C(1f, 1f, 1f, 0.42f);

	private static readonly Color TargetRing = Gfx.C(1f, 0.9f, 0.2f, 1f);

	private readonly ModContext _ctx;

	private readonly LabelText _trackTitle = new LabelText();

	private readonly LabelText _trackDistance = new LabelText();

	/// <summary>The distance last shown under the minimap, in whole metres, and its text (rebuilt only when it changes).</summary>
	private int _shownMetres = -1;

	private string _distanceText;

	private GUIStyle _hint;

	private GUIStyle _trackName;

	private GUIStyle _trackDist;

	private float _builtScale = -1f;

	public MinimapRenderer(ModContext ctx)
	{
		_ctx = ctx;
	}

	/// <param name="target">POI to ring and label, or null.</param>
	public void Draw(Poi target)
	{
		MapAssets map = _ctx.Map;
		EnsureStyles();
		float size = Ui.S(BasePanel);
		float margin = Ui.S(BaseMargin);
		Rect rect = Gfx.R(Frame.ScreenWidth - size - margin, margin, size, size);
		Theme.Panel(rect, fill: false);
		if (!_ctx.CanLocate)
		{
			GUI.color = Theme.Parchment;
			GUI.DrawTexture(rect, GuiTextures.White);
			GUI.color = Gfx.White;
			Theme.CornerStuds(rect);
			GUI.Label(rect, Gfx.Fixed("Waiting for Nate..."), _hint);
			return;
		}
		GUI.color = Theme.ParchmentDark;
		GUI.DrawTexture(rect, GuiTextures.White);
		GUI.color = Gfx.White;
		PlayerTracker player = _ctx.Player;
		Vector2 here = _ctx.Projection.WorldToMap(player.WorldPos);
		float crop = CropMetres * _ctx.Projection.PxPerMetre;
		float scale = size / crop;
		MapDraw.DrawMap(_ctx.Art, rect, Gfx.Center(rect), scale, here);
		GUI.BeginGroup(rect);
		Vector2 pivot = Fast.V2(size * 0.5f, size * 0.5f);
		float half = crop * 0.5f;
		float dotSize = Ui.S(15f);
		float tickSize = Ui.S(16f);
		PoiProgress progress = _ctx.Progress;
		foreach (Poi poi in map.Pois)
		{
			float dx = poi.Px - here.x;
			float dy = poi.Py - here.y;
			if (Fast.Abs(dx) > half || Fast.Abs(dy) > half || _ctx.Hidden(poi))
			{
				continue;
			}
			float sx = pivot.x + dx * scale;
			float sy = pivot.y + dy * scale;
			// Completed POIs fade and get a tick, as on the full map.
			if (progress.IsDone(poi))
			{
				GUI.color = DoneDot;
				GUI.DrawTexture(Gfx.R(sx - dotSize * 0.5f, sy - dotSize * 0.5f, dotSize, dotSize), GuiTextures.Dot(poi.Cat));
				GUI.color = FullMapScreen.DoneTick;
				GUI.DrawTexture(Gfx.R(sx - tickSize * 0.5f, sy - tickSize * 0.5f, tickSize, tickSize), GuiTextures.Check);
				GUI.color = Gfx.White;
			}
			else
			{
				GUI.DrawTexture(Gfx.R(sx - dotSize * 0.5f, sy - dotSize * 0.5f, dotSize, dotSize), GuiTextures.Dot(poi.Cat));
			}
		}
		// Saved locations: small pins, without their numbers or initials.
		float pinWidth = Ui.S(13f);
		foreach (SavedLocation location in _ctx.Locations.All)
		{
			float dx = location.Px - here.x;
			float dy = location.Py - here.y;
			if (Fast.Abs(dx) <= half && Fast.Abs(dy) <= half)
			{
				FullMapScreen.DrawPin(location, Fast.V2(pivot.x + dx * scale, pivot.y + dy * scale), pinWidth, null);
			}
		}
		if (target != null)
		{
			float dx = target.Px - here.x;
			float dy = target.Py - here.y;
			if (Fast.Abs(dx) <= half && Fast.Abs(dy) <= half)
			{
				float ringSize = Ui.S(26f);
				GUI.color = TargetRing;
				GUI.DrawTexture(Gfx.R(pivot.x + dx * scale - ringSize * 0.5f, pivot.y + dy * scale - ringSize * 0.5f, ringSize, ringSize), GuiTextures.Ring);
				GUI.color = Gfx.White;
			}
		}
		// Inside the group: the turn is about the pivot's place on the screen.
		GUI.matrix = Gfx.Rotation(MapProjection.MapAngle(player.Forward), Gfx.Center(rect));
		float arrowSize = Ui.S(16f);
		GUI.color = Theme.Player;
		GUI.DrawTexture(Gfx.R(pivot.x - arrowSize * 0.5f, pivot.y - arrowSize * 0.5f, arrowSize, arrowSize), GuiTextures.Arrow);
		GUI.color = Gfx.White;
		GUI.matrix = Gfx.Identity;
		GUI.EndGroup();
		Theme.CornerStuds(rect);
		if (target != null)
		{
			DrawTrackingBar(rect, target);
		}
	}

	private void DrawTrackingBar(Rect above, Poi target)
	{
		float barH = Ui.S(30f);
		Rect bar = Gfx.R(above.m_XMin, Gfx.Bottom(above) + Ui.S(11f), above.m_Width, barH);
		Theme.Panel(bar, fill: true);
		float pad = Ui.S(9f);
		float iconSize = Ui.S(15f);
		Rect icon = Gfx.R(bar.m_XMin + pad, bar.m_YMin + (barH - iconSize) * 0.5f, iconSize, iconSize);
		GUI.color = Theme.Gold;
		GUI.DrawTexture(icon, GuiTextures.Ring);
		GUI.color = Gfx.White;
		float distW = Ui.S(62f);
		GUI.Label(Gfx.R(Gfx.Right(icon) + Ui.S(6f), bar.m_YMin, bar.m_Width - iconSize - pad * 2f - Ui.S(6f) - distW, barH), _trackTitle.Get(target.Title), _trackName);
		int metres = Fast.RoundToInt(_ctx.TargetDistance());
		if (metres != _shownMetres || _distanceText == null)
		{
			_shownMetres = metres;
			_distanceText = metres + " m";
		}
		GUI.Label(Gfx.R(Gfx.Right(bar) - distW - pad, bar.m_YMin, distW, barH), _trackDistance.Get(_distanceText), _trackDist);
	}

	private void EnsureStyles()
	{
		if (_hint != null && _builtScale == Ui.Scale)
		{
			return;
		}
		_builtScale = Ui.Scale;
		MemoryDiagnostics.StyleBuilds++;
		_hint = new GUIStyle(GUI.skin.label)
		{
			alignment = TextAnchor.MiddleCenter,
			wordWrap = true,
			fontSize = Ui.Font(15f),
			fontStyle = FontStyle.Bold,
			normal =
			{
				textColor = Theme.InkText
			}
		};
		_trackName = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(17f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleLeft,
			clipping = TextClipping.Clip,
			normal =
			{
				textColor = Theme.InkText
			}
		};
		_trackDist = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(15f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleRight,
			normal =
			{
				textColor = new Color(0.45f, 0.28f, 0.08f)
			}
		};
	}
}
