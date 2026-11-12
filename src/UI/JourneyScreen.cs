using System;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Hero's Journey (D-pad Left or J on the map): Nate's path painted the way the game's ending
/// paints it, from the game's own record for the save that's loaded (<see cref="JourneyRecord"/>).
/// The ending's set (<see cref="MapArt"/>, the map's own sheets): its six painted map sheets on the wooden board,
/// seen through its camera, which keeps the pen in the middle of the view as it climbs (never
/// lower than where it starts). Its pen (<see cref="JourneyPath"/>): red dots at its spacing and
/// size, the bigger dot on the tip, strokes broken at the world loop's seam, 13 points a second
/// (faster for long journeys, never longer than 11 minutes).
/// <para>
/// Beyond the ending: pause, rewind and fast-forward, skip to the end, look up and down the board,
/// and zoom out as far as the whole board. The panels hide while it paints, and come back with
/// any input.
/// </para>
/// </summary>
public class JourneyScreen
{
	private enum Stage
	{
		Closed,
		/// <summary>Black, waiting for the paintings.</summary>
		Unrolling,
		Showing
	}

	private enum InputMode
	{
		Pad,
		Mouse,
		Keys
	}

	/// <summary>The longest wait for the paintings before painting starts on whatever has arrived.</summary>
	private const float UnrollTimeout = 4f;

	/// <summary>Seconds to fade in from black once the paintings are up (the ending takes 3).</summary>
	private const float FadeTime = 1.2f;

	/// <summary>The ending keeps the pen's tip within this of the middle of the view, up and down (its units).</summary>
	private const float Tether = 0.05f;

	/// <summary>The panels hide this long after the last input while painting.</summary>
	private const float PanelLinger = 4f;

	/// <summary>Smallest dot radii on screen (px), so a zoomed-out path still shows.</summary>
	private const float MinDotPx = 1.15f;

	private const float MinBigDotPx = 2.8f;

	/// <summary>Zoomed out, dots closer than this on screen (px) are thinned out.</summary>
	private const float MinSpacingPx = 2.6f;

	/// <summary>Full stick rewinds or fast-forwards the whole journey in this many seconds...</summary>
	private const float ScrubWholeSeconds = 20f;

	/// <summary>...but at least this many points a second.</summary>
	private const float ScrubMinSpeed = 60f;

	/// <summary>Looking up and down: views a second at full stick.</summary>
	private const float PanViewsPerSecond = 1.2f;

	/// <summary>Room round the board when zoomed all the way out (its units).</summary>
	private const float BoardMargin = 0.25f;

	private const string PadHelp = "A=pause/play   L-stick ←→=rewind/fast-forward   L/R-stick ↑↓=look   X=follow the pen   Y=finish   Dpad-Up/Dn=zoom   B=back to the map   View=close";

	private const string KeyHelp = "Space=pause/play   A/D=rewind/fast-forward   W/S=look   C=follow the pen   F=finish   PgUp/PgDn=zoom   Esc=back to the map   M=close";

	private const string MouseHelp = "Click=pause/play   Drag=look   Wheel=zoom   A/D=rewind/fast-forward   F=finish   Right-click=back to the map   M=close";

	/// <summary>The ending's ink.</summary>
	private static readonly Color Ink = Gfx.C(0.8f, 0f, 0f, 1f);

	private static readonly Color Night = Gfx.C(0.03f, 0.025f, 0.02f, 1f);

	/// <summary>The ending's warm light, on the paintings.</summary>
	private static readonly Color Lamp = Gfx.C(1f, 0.957f, 0.839f, 1f);

	private static readonly Color PlainWood = Gfx.C(0.33f, 0.23f, 0.15f, 1f);

	private static readonly Color PlainPaper = Gfx.C(0.79f, 0.68f, 0.5f, 1f);

	private static readonly Color PaperEdge = Gfx.C(0.55f, 0.43f, 0.28f, 1f);

	private static readonly Color SheetShadow = Gfx.C(0f, 0f, 0f, 0.45f);

	private readonly ModContext _ctx;

	private readonly MapArt _art;

	private readonly JourneyPath _path = new JourneyPath();

	private Stage _stage;

	/// <summary>Why there's no journey to show (no game loaded...), or null.</summary>
	private string _problem;

	private float _openedAt;

	private float _shownAt;

	private float _lastInput;

	/// <summary>How far the pen has got, in segments: 0 = nothing drawn, <see cref="JourneyPath.Segments"/> = all of it.</summary>
	private float _progress;

	private bool _playing;

	/// <summary>The middle of the view, up the board (its units); across it's always the middle of the sheets.</summary>
	private float _camY;

	/// <summary>How much more than the ending's view is shown (1 = the ending's).</summary>
	private float _zoom = 1f;

	/// <summary>The view follows the pen (until looked about by hand).</summary>
	private bool _follow = true;

	// This frame's view, for drawing dots.
	private float _scale;

	private float _centreX;

	private float _centreY;

	private float _dotPx;

	private readonly Action<float, float> _drawDot;

	// Panels.
	private GUIStyle _title;

	private GUIStyle _line;

	private GUIStyle _tip;

	private float _builtScale = -1f;

	private readonly LabelText _statusText = new LabelText();

	private readonly LabelText _footerText = new LabelText();

	private int _statusKey = int.MinValue;

	private string _statusLine;

	public JourneyScreen(ModContext ctx)
	{
		_ctx = ctx;
		_art = ctx.Art;
		_drawDot = DrawDotAt;
	}

	public bool IsOpen => _stage != Stage.Closed;

	private InputMode Mode
	{
		get
		{
			if (_ctx.Pointer.Active)
			{
				return InputMode.Mouse;
			}
			return _ctx.Controls.Pad.Connected ? InputMode.Pad : InputMode.Keys;
		}
	}

	/// <summary>Read the game's record afresh, ask for the board's painting (the sheets are the map's, already in), and start from black.</summary>
	public void Open()
	{
		_problem = JourneyRecord.Load(_path, _ctx.Log);
		_art.RequestSheets();
		_art.RequestBoard();
		_stage = Stage.Unrolling;
		_openedAt = Frame.Now;
		_lastInput = Frame.Now;
		_progress = 0f;
		_playing = _path.Segments > 0;
		_zoom = 1f;
		_follow = true;
		_camY = _art.CameraY;
		_statusKey = int.MinValue;
	}

	/// <summary>Back to the map, and the game gets the board's painting back.</summary>
	public void Close()
	{
		if (_stage == Stage.Closed)
		{
			return;
		}
		_stage = Stage.Closed;
		_art.ReleaseBoard();
	}

	public void Update(float dt)
	{
		Controls c = _ctx.Controls;
		Pointer pointer = _ctx.Pointer;
		if (c.Pressed(Gamepad.B, KeyCode.Escape, KeyCode.Backspace) || c.Pressed(Gamepad.DPAD_LEFT, KeyCode.J) || pointer.RightClicked)
		{
			Close();
			return;
		}
		if (_stage == Stage.Unrolling)
		{
			if (_art.Settled || Frame.Now - _openedAt > UnrollTimeout)
			{
				_stage = Stage.Showing;
				_shownAt = Frame.Now;
				_lastInput = Frame.Now;
				int painted = _art.PaintingsLoaded;
				_ctx.Log.Msg($"Hero's Journey: {_path.Count} points, {_path.DotCount} dots; {painted} of {_art.Sheets.Count + 1} paintings from the game{((painted == 0) ? " (plain paper)" : "")}.");
			}
			return;
		}
		int segments = _path.Segments;
		bool input = false;
		if (c.Pressed(Gamepad.A, KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter) || pointer.Clicked)
		{
			input = true;
			if (_progress >= segments)
			{
				// From the start again.
				_progress = 0f;
				_playing = segments > 0;
			}
			else
			{
				_playing = !_playing;
			}
			_follow = true;
		}
		if (c.Pressed(Gamepad.Y, KeyCode.F, KeyCode.End))
		{
			input = true;
			_progress = segments;
			_playing = false;
		}
		if (c.Pressed(Gamepad.X, KeyCode.C, KeyCode.Home))
		{
			input = true;
			_follow = true;
		}
		Vector2 move = c.Move();
		float scrub = move.x;
		if (Fast.Abs(scrub) > 0.15f && segments > 0)
		{
			input = true;
			float speed = Fast.Max(segments / ScrubWholeSeconds, ScrubMinSpeed);
			_progress = Fast.Clamp(_progress + scrub * Fast.Abs(scrub) * speed * dt, 0f, segments);
			_follow = true;
		}
		float look = move.y;
		Gamepad pad = c.Pad;
		if (Fast.Abs(pad.RY) > Fast.Abs(look))
		{
			look = pad.RY;
		}
		float viewHeight = _art.ViewHeight * _zoom;
		if (Fast.Abs(look) > 0.15f)
		{
			input = true;
			_follow = false;
			_camY += look * PanViewsPerSecond * viewHeight * dt;
		}
		if (pointer.Dragging && _scale > 0f)
		{
			input = true;
			_follow = false;
			_camY += pointer.Delta.y / _scale;
		}
		float zoomDir = 0f;
		if (c.Held(Gamepad.DPAD_UP, KeyCode.PageUp, KeyCode.Equals, KeyCode.KeypadPlus))
		{
			zoomDir -= 1f;
		}
		if (c.Held(Gamepad.DPAD_DOWN, KeyCode.PageDown, KeyCode.Minus, KeyCode.KeypadMinus))
		{
			zoomDir += 1f;
		}
		if (zoomDir != 0f || Fast.Abs(pointer.Wheel) > 0.01f)
		{
			input = true;
			float factor = Fast.Pow(2.2f, zoomDir * dt) * Fast.Pow(1.2f, -pointer.Wheel);
			_zoom = Fast.Clamp(_zoom * factor, 1f, MaxZoom());
		}
		if (input || pointer.Delta.x != 0f || pointer.Delta.y != 0f)
		{
			_lastInput = Frame.Now;
		}
		if (_playing && segments > 0)
		{
			_progress += dt * _path.Speed;
			if (_progress >= segments)
			{
				_progress = segments;
				_playing = false;
				_lastInput = Frame.Now;
			}
		}
		FollowPen();
	}

	/// <summary>Zoomed out this far, the whole board fits.</summary>
	private float MaxZoom()
	{
		MapArt.Piece board = _art.Board;
		float height = (board != null) ? (board.H + BoardMargin * 2f) : 8f;
		return Fast.Max(1f, height / _art.ViewHeight);
	}

	/// <summary>The ending's camera: it keeps the tip near the middle, never lower than where it starts. Then kept on the board.</summary>
	private void FollowPen()
	{
		float viewHeight = _art.ViewHeight * _zoom;
		if (_follow && _path.Count > 0)
		{
			PenAt(out float _, out float tipY);
			float tether = Tether * _zoom;
			if (_camY < tipY - tether)
			{
				_camY = tipY - tether;
			}
			else if (_camY > tipY + tether)
			{
				_camY = tipY + tether;
			}
			_camY = Fast.Max(_camY, _art.CameraY + (viewHeight - _art.ViewHeight) * 0.5f);
		}
		MapArt.Piece board = _art.Board;
		if (board == null)
		{
			return;
		}
		float low = board.Y - board.H * 0.5f - BoardMargin + viewHeight * 0.5f;
		float high = board.Y + board.H * 0.5f + BoardMargin - viewHeight * 0.5f;
		_camY = (low > high) ? board.Y : Fast.Clamp(_camY, low, high);
	}

	/// <summary>Where the pen is: the tip of the segment being drawn, or the last point.</summary>
	private void PenAt(out float x, out float y)
	{
		int segments = _path.Segments;
		if (segments == 0)
		{
			x = (_path.Count > 0) ? _path.X(0) : 0.5f;
			y = (_path.Count > 0) ? _path.Y(0) : _art.CameraY;
			return;
		}
		Head(out int segment, out float along);
		_path.Tip(segment, along, out x, out y);
	}

	/// <summary>The segment being drawn and how far along it (1 when the whole journey is drawn).</summary>
	private void Head(out int segment, out float along)
	{
		int segments = _path.Segments;
		if (_progress >= segments)
		{
			segment = segments;
			along = 1f;
			return;
		}
		int whole = (int)_progress;
		segment = whole + 1;
		along = _progress - whole;
	}

	public void Draw()
	{
		EnsureStyles();
		float sw = Frame.ScreenWidth;
		float sh = Frame.ScreenHeight;
		GUI.color = Night;
		GUI.DrawTexture(Gfx.R(0f, 0f, sw, sh), GuiTextures.White);
		GUI.color = Gfx.White;
		if (_stage == Stage.Unrolling)
		{
			DrawPanels(sw, sh);
			return;
		}
		float viewHeight = _art.ViewHeight * _zoom;
		_scale = sh / viewHeight;
		_centreX = sw * 0.5f;
		_centreY = sh * 0.5f;
		DrawSet(sw, sh);
		DrawDots(sw, sh);
		float fade = 1f - (Frame.Now - _shownAt) / FadeTime;
		if (fade > 0f)
		{
			GUI.color = Gfx.A(Night, fade);
			GUI.DrawTexture(Gfx.R(0f, 0f, sw, sh), GuiTextures.White);
			GUI.color = Gfx.White;
		}
		if (!_playing || Frame.Now - _lastInput < PanelLinger || _path.Count == 0 || _problem != null)
		{
			DrawPanels(sw, sh);
		}
	}

	private float ScreenX(float x)
	{
		return _centreX + (x - 0.5f) * _scale;
	}

	private float ScreenY(float y)
	{
		return _centreY - (y - _camY) * _scale;
	}

	/// <summary>A piece of the set (its centre and size) as a screen rectangle.</summary>
	private Rect ScreenRect(float x, float y, float w, float h)
	{
		float left = ScreenX(x - w * 0.5f);
		float top = ScreenY(y + h * 0.5f);
		return Gfx.R(left, top, w * _scale, h * _scale);
	}

	private static bool Visible(Rect r, float sw, float sh)
	{
		return r.m_XMin < sw && r.m_XMin + r.m_Width > 0f && r.m_YMin < sh && r.m_YMin + r.m_Height > 0f;
	}

	/// <summary>The board, then each sheet on its shadow: the game's paintings where they've arrived, plain paper where not.</summary>
	private void DrawSet(float sw, float sh)
	{
		MapArt.Piece board = _art.Board;
		if (board != null)
		{
			Rect r = ScreenRect(board.X, board.Y, board.W, board.H);
			if (Visible(r, sw, sh))
			{
				GUI.color = (board.Painting != null) ? Lamp : PlainWood;
				GUI.DrawTexture(r, board.Painting ?? GuiTextures.White);
			}
		}
		// The ending's light falls from the front: the shadow peeks out a little down and to the right.
		float drop = Fast.Max(0.004f * _scale, 1.5f);
		foreach (MapArt.Piece sheet in _art.Sheets)
		{
			Rect r = ScreenRect(sheet.X, sheet.Y, sheet.W, sheet.H);
			if (!Visible(Gfx.Expand(r, drop * 2f), sw, sh))
			{
				continue;
			}
			if (sheet.ShadowW > 0f)
			{
				Rect s = ScreenRect(sheet.X + sheet.ShadowX * sheet.W, sheet.Y + sheet.ShadowY * sheet.H, sheet.W * sheet.ShadowW, sheet.H * sheet.ShadowH);
				s.m_XMin += drop;
				s.m_YMin += drop;
				GUI.color = SheetShadow;
				GUI.DrawTexture(s, sheet.Painting ?? GuiTextures.White);
			}
			if (sheet.Painting != null)
			{
				GUI.color = Lamp;
				GUI.DrawTexture(r, sheet.Painting);
			}
			else
			{
				GUI.color = PaperEdge;
				GUI.DrawTexture(r, GuiTextures.White);
				GUI.color = PlainPaper;
				GUI.DrawTexture(Gfx.Expand(r, -Fast.Max(0.006f * _scale, 1f)), GuiTextures.White);
			}
		}
		GUI.color = Gfx.White;
	}

	/// <summary>The ending's pen: every dot drawn so far, the segment being drawn, and the tip.</summary>
	private void DrawDots(float sw, float sh)
	{
		JourneyPath path = _path;
		if (path.Count == 0 || _progress <= 0f)
		{
			return;
		}
		Head(out int segment, out float along);
		bool done = _progress >= path.Segments;
		// The segment being drawn crosses the seam: the ending draws the stroke's end and the next
		// one's start at once, and no tip.
		bool seam = path.IsSeam(segment);
		// Dots finished for good: those before the segment being drawn (with a seam, its end and
		// new start too; when it's all drawn, every one).
		int end = done ? path.DotCount : path.FirstDotOf(seam ? segment + 1 : segment);
		float small = Fast.Max(JourneyPath.DotRadius * _scale, MinDotPx);
		float big = Fast.Max(JourneyPath.BigDotRadius * _scale, MinBigDotPx);
		int every = Fast.Max(1, Fast.CeilToInt(MinSpacingPx / (JourneyPath.DotSpacing * _scale)));
		// What's on screen, in the drawing's units, with a dot's width to spare.
		float halfW = sw * 0.5f / _scale + JourneyPath.BigDotRadius * 2f;
		float halfH = sh * 0.5f / _scale + JourneyPath.BigDotRadius * 2f;
		float xLow = 0.5f - halfW;
		float xHigh = 0.5f + halfW;
		float yLow = _camY - halfH;
		float yHigh = _camY + halfH;
		GUI.color = Ink;
		for (int i = 0; i < end; i++)
		{
			bool isBig = path.DotBig(i);
			if (!isBig && i % every != 0)
			{
				continue;
			}
			float y = path.DotY(i);
			if (y < yLow || y > yHigh)
			{
				continue;
			}
			float x = path.DotX(i);
			if (x < xLow || x > xHigh)
			{
				continue;
			}
			float r = isBig ? big : small;
			GUI.DrawTexture(Gfx.R(ScreenX(x) - r, ScreenY(y) - r, r * 2f, r * 2f), GuiTextures.Disc);
		}
		if (!seam)
		{
			// The segment being drawn, worked out afresh each frame as the ending does, and the tip.
			path.Tip(segment, along, out float tipX, out float tipY);
			if (!done)
			{
				_dotPx = small;
				path.PartialDots(segment, tipX, tipY, _drawDot);
			}
			GUI.DrawTexture(Gfx.R(ScreenX(tipX) - big, ScreenY(tipY) - big, big * 2f, big * 2f), GuiTextures.Disc);
		}
		GUI.color = Gfx.White;
	}

	private void DrawDotAt(float x, float y)
	{
		float r = _dotPx;
		GUI.DrawTexture(Gfx.R(ScreenX(x) - r, ScreenY(y) - r, r * 2f, r * 2f), GuiTextures.Disc);
	}

	private void DrawPanels(float sw, float sh)
	{
		string status = StatusLine();
		float titleH = Ui.S(38f);
		float lineH = Ui.S(30f);
		float w = Fast.Min(Ui.S(760f), sw - Ui.S(40f));
		float left = (sw - w) * 0.5f;
		Rect header = Gfx.R(left, Ui.S(10f), w, titleH + lineH + Ui.S(14f));
		Theme.Panel(header, fill: true);
		float y = header.m_YMin + Ui.S(6f);
		GUI.Label(Gfx.R(left, y, w, titleH), Gfx.Fixed("HERO'S JOURNEY"), _title);
		GUI.Label(Gfx.R(left, y + titleH, w, lineH), _statusText.Get(status), _line);
		if (_stage == Stage.Unrolling)
		{
			return;
		}
		Rect footer = Gfx.R(Ui.S(24f), sh - Ui.S(52f), sw - Ui.S(48f), Ui.S(32f));
		Theme.Panel(footer, fill: true);
		InputMode mode = Mode;
		GUI.Label(footer, _footerText.Get((mode == InputMode.Mouse) ? MouseHelp : ((mode == InputMode.Pad) ? PadHelp : KeyHelp)), _tip);
	}

	/// <summary>What the header says under the title, rebuilt only when it changes.</summary>
	private string StatusLine()
	{
		int segments = _path.Segments;
		int percent = (segments > 0) ? (int)(100f * _progress / segments) : 0;
		int key = (((int)_stage * 2 + (_playing ? 1 : 0)) * 2 + ((_progress >= segments) ? 1 : 0)) * 101 + percent;
		if (key == _statusKey && _statusLine != null)
		{
			return _statusLine;
		}
		_statusKey = key;
		if (_stage == Stage.Unrolling)
		{
			_statusLine = "Unrolling the map sheets...";
		}
		else if (_problem != null)
		{
			_statusLine = "Nothing to paint: " + _problem;
		}
		else if (_path.Count < 2)
		{
			_statusLine = "Nothing to paint yet: the game notes where Nate goes every few seconds once he's moving";
		}
		else if (_progress >= segments)
		{
			_statusLine = $"Your journey so far: {_path.Count:N0} places Nate has been";
		}
		else
		{
			_statusLine = $"{percent}%  of {_path.Count:N0} places{(_playing ? "" : "   -   paused")}";
		}
		return _statusLine;
	}

	private void EnsureStyles()
	{
		if (_title != null && _builtScale == Ui.Scale)
		{
			return;
		}
		_builtScale = Ui.Scale;
		MemoryDiagnostics.StyleBuilds++;
		_title = new GUIStyle(GUI.skin.label)
		{
			fontStyle = FontStyle.Bold,
			fontSize = Ui.Font(26f),
			alignment = TextAnchor.UpperCenter,
			normal =
			{
				textColor = Theme.InkText
			}
		};
		_line = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(20f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.UpperCenter,
			normal =
			{
				textColor = Gfx.C(0.56f, 0.34f, 0.05f)
			}
		};
		_tip = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(15f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			normal =
			{
				textColor = Gfx.C(0.3f, 0.22f, 0.12f)
			}
		};
	}
}
