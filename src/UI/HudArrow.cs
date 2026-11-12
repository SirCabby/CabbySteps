using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Arrow styles 0 and 1: a 2D guide arrow to the tracked POI, either floating over the
/// target itself (overhead) or as a compass at the top centre of the screen.
/// </summary>
public class HudArrow
{
	private static readonly Color ArrowColor = Gfx.C(0.35f, 1f, 0.3f, 1f);

	private static readonly Color ArrowShadow = Gfx.C(0f, 0f, 0f, 0.5f);

	private readonly ModContext _ctx;

	private Camera _cam;

	private float _nextCameraSearch;

	private GUIStyle _name;

	private GUIStyle _dist;

	private GUIStyle _nameShadow;

	private GUIStyle _distShadow;

	private readonly LabelText _nameText = new LabelText();

	private readonly LabelText _distText = new LabelText();

	private int _shownMetres = -1;

	private string _distString;

	private float _builtScale = -1f;

	public HudArrow(ModContext ctx)
	{
		_ctx = ctx;
	}

	public void Draw(bool overhead)
	{
		Poi target = _ctx.Target;
		PlayerTracker player = _ctx.Player;
		if (target == null || !player.Found)
		{
			return;
		}
		EnsureStyles();
		Vector3 delta = _ctx.TargetDelta();
		float dist = Fast.Sqrt(delta.x * delta.x + delta.z * delta.z);
		GUIContent name = _nameText.Get(target.Title);
		int metres = Fast.RoundToInt(dist);
		if (metres != _shownMetres || _distString == null)
		{
			_shownMetres = metres;
			_distString = metres + " m";
		}
		GUIContent distance = _distText.Get(_distString);
		// Overhead falls back to the compass when the target is off screen or behind the camera.
		if (overhead && TryDrawOverhead(player, target, delta, name, distance))
		{
			return;
		}
		// Compass: 0 degrees = straight ahead, so the arrow (drawn pointing right) turns -90.
		float bearing = Fast.Atan2(delta.x, delta.z) * Fast.Rad2Deg;
		float angleDeg = bearing - player.HeadingDeg - 90f;
		float sw = Frame.ScreenWidth;
		Vector2 pivot = Fast.V2(sw * 0.5f, Ui.S(46f));
		float size = Ui.S(60f);
		DrawArrow(pivot, size, angleDeg);
		float textY = pivot.y + size * 0.5f + Ui.S(4f);
		Theme.Shadowed(Gfx.R(0f, textY, sw, Ui.S(28f)), name, _name, _nameShadow);
		Theme.Shadowed(Gfx.R(0f, textY + Ui.S(26f), sw, Ui.S(24f)), distance, _dist, _distShadow);
	}

	/// <summary>Bobbing down-arrow over the target's on-screen position. False if it isn't comfortably on screen.</summary>
	private bool TryDrawOverhead(PlayerTracker player, Poi target, Vector3 delta, GUIContent name, GUIContent distance)
	{
		Camera cam = GetCamera();
		if (cam == null)
		{
			return false;
		}
		try
		{
			// The copy of the target in the same world loop as Nate, 2 m above it (or
			// above Nate's height when the POI's height is unknown).
			Vector3 nate = player.WorldPos;
			float y = (target.HasHeight ? target.World.y : nate.y) + 2f;
			Vector3 screen = cam.WorldToScreenPoint(Fast.V3(nate.x + delta.x, y, nate.z + delta.z));
			if (screen.z <= 0.5f)
			{
				return false;
			}
			float sw = Frame.ScreenWidth;
			float sh = Frame.ScreenHeight;
			float x = screen.x;
			float top = sh - screen.y;
			float margin = Ui.S(70f);
			if (x < margin || x > sw - margin || top < margin || top > sh - margin)
			{
				return false;
			}
			float bob = Fast.Sin(Frame.Now * 3.2f) * Ui.S(6f);
			float size = Ui.S(50f);
			Vector2 pivot = Fast.V2(x, top - size * 0.7f + bob);
			DrawArrow(pivot, size, 90f);
			float textY = pivot.y - size * 0.6f - Ui.S(46f);
			Theme.Shadowed(Gfx.R(x - Ui.S(220f), textY, Ui.S(440f), Ui.S(26f)), name, _name, _nameShadow);
			Theme.Shadowed(Gfx.R(x - Ui.S(220f), textY + Ui.S(24f), Ui.S(440f), Ui.S(22f)), distance, _dist, _distShadow);
			return true;
		}
		catch
		{
			// Most likely the camera went with a scene change: look for it again.
			_cam = null;
			return false;
		}
	}

	/// <summary>The game camera, looked up at most twice a second until found.</summary>
	private Camera GetCamera()
	{
		// A plain reference check: Unity's own == would be a call into the game every frame. A
		// destroyed camera throws when used, and TryDrawOverhead then drops it.
		if (!ReferenceEquals(_cam, null))
		{
			return _cam;
		}
		float now = Frame.Now;
		if (now < _nextCameraSearch)
		{
			return null;
		}
		_nextCameraSearch = now + 0.5f;
		try
		{
			_cam = Camera.main;
			if (_cam == null)
			{
				Il2CppReferenceArray<Camera> all = Camera.allCameras;
				if (all != null && all.Length > 0)
				{
					_cam = all[0];
				}
			}
		}
		catch
		{
			_cam = null;
		}
		return _cam;
	}

	/// <summary>Draw the arrow texture (points right at 0 degrees) with a drop shadow.</summary>
	private static void DrawArrow(Vector2 pivot, float size, float angleDeg)
	{
		GUI.matrix = Gfx.Rotation(angleDeg, pivot);
		GUI.color = ArrowShadow;
		GUI.DrawTexture(Gfx.R(pivot.x - size * 0.5f + Ui.S(2f), pivot.y - size * 0.5f + Ui.S(2f), size, size), GuiTextures.Arrow);
		GUI.color = ArrowColor;
		GUI.DrawTexture(Gfx.R(pivot.x - size * 0.5f, pivot.y - size * 0.5f, size, size), GuiTextures.Arrow);
		GUI.color = Gfx.White;
		GUI.matrix = Gfx.Identity;
	}

	private void EnsureStyles()
	{
		if (_name != null && _builtScale == Ui.Scale)
		{
			return;
		}
		_builtScale = Ui.Scale;
		MemoryDiagnostics.StyleBuilds++;
		_name = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(22f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.UpperCenter,
			normal =
			{
				textColor = Gfx.C(1f, 0.85f, 0.2f)
			}
		};
		_dist = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(18f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.UpperCenter,
			normal =
			{
				textColor = Gfx.C(0.85f, 0.95f, 1f)
			}
		};
		_nameShadow = Theme.ShadowOf(_name, 0.7f);
		_distShadow = Theme.ShadowOf(_dist, 0.7f);
	}
}
