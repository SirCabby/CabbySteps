using System.Collections.Generic;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// The full-screen map (View button / M): the game's own painted map sheets (<see cref="MapArt"/>),
/// with everything put on them as the game itself puts Nate there (<see cref="MapProjection"/>):
/// POIs, the tracked target and Nate's arrow; D-pad Left
/// (J) opens the Hero's Journey over it (<see cref="JourneyScreen"/>). With a pad or the keyboard
/// the map pans and zooms under a crosshair fixed at the screen centre; with the mouse it's drag to pan and scroll to zoom where the
/// pointer is. The POI under the crosshair or the pointer is picked and labelled, and can be
/// tracked or marked completed. Saved locations are pins on top: they can be tracked too, and a
/// button held on one sends Nate there (<see cref="Recall"/>) or forgets it; A held on a kept one
/// (or F2) names it (<see cref="RenameBox"/>), and its pin then shows the name's initials instead
/// of its number. The button that sends Nate to a location,
/// held on a movable item (a hat, a key or quest item, a chick or duckling), brings it to Nate
/// (<see cref="Summoner"/>).
/// </summary>
public class FullMapScreen
{
	private enum InputMode
	{
		Pad,
		Mouse,
		Keys
	}

	/// <summary>Crosshair pan speed in screen pixels per second at full stick.</summary>
	private const float PanScreenSpeed = 1100f;

	/// <summary>How near the crosshair has to be to a POI to pick it (reference px).</summary>
	private const float CrosshairReach = 30f;

	/// <summary>How near the pointer has to be to a POI to pick it (reference px).</summary>
	private const float PointerReach = 13f;

	/// <summary>The map opens this close: screen pixels per metre of map.</summary>
	private const float OpenScreenPxPerMetre = 3.84f;

	/// <summary>The closest zoom: screen pixels per metre of map (three per texel of the game's sheets).</summary>
	private const float MaxScreenPxPerMetre = 10.24f;

	private const string PadHelp = "Zoom {0:0.0}x   Dpad-Up/Dn=zoom  L-stick=pan  A=track  Y=mark completed  B=clear  X=find Nate  R3={1} completed  L3=save location  LT/RT=locations  Dpad-Left=Hero's Journey  Dpad-Right=guide  LB/RB=UI  View=close";

	private const string MouseHelp = "Zoom {0:0.0}x   Drag=pan  Wheel=zoom  Click=track  Right-click=mark completed  Backspace=clear  C=find Nate  H={1} completed  F5=save  F9=quick load  Q/E=locations  J=Hero's Journey  /=guide  ,/.=UI  M=close";

	private const string KeyHelp = "Zoom {0:0.0}x   WASD=pan  PgUp/PgDn=zoom  Enter=track  Space=mark completed  Backspace=clear  C=find Nate  H={1} completed  F5=save  F9=quick load  Q/E=locations  J=Hero's Journey  /=guide  ,/.=UI  M=close";

	/// <summary>Completed POIs: the dot fades and gets a tick.</summary>
	private static readonly Color DoneDot = Gfx.C(1f, 1f, 1f, 0.42f);

	public static readonly Color DoneTick = Gfx.C(0.45f, 0.95f, 0.4f, 1f);

	/// <summary>A saved location's pin width on the map (reference px); the picked one is drawn bigger.</summary>
	private const float PinSize = 22f;

	private const float PickedPinScale = 1.3f;

	/// <summary>
	/// How wide the text on a pin's head can be, in pin widths: just inside the rim (the head's
	/// fill is 0.65 of the pin across).
	/// </summary>
	private const float BadgeRoom = 0.62f;

	/// <summary>Seconds a button has to be held on a saved location to go there or forget it, or on an item to bring it.</summary>
	private const float HoldTime = 0.8f;

	/// <summary>The hold's ring only shows after this long, so a tap doesn't flash it.</summary>
	private const float HoldRingDelay = 0.12f;

	private static readonly Color HoldGo = Gfx.C(0.45f, 0.95f, 0.4f, 1f);

	private static readonly Color HoldForget = Gfx.C(1f, 0.36f, 0.3f, 1f);

	private static readonly Color HoldRename = Gfx.C(1f, 0.85f, 0.42f, 1f);

	private static readonly Color HoldUnlit = Gfx.C(0f, 0f, 0f, 0.4f);

	private static readonly Color PinShadow = Gfx.C(0f, 0f, 0f, 0.35f);

	private static readonly Color Backdrop = Gfx.C(0f, 0f, 0f, 0.92f);

	private static readonly Color TargetRing = Gfx.C(1f, 0.9f, 0.2f, 1f);

	private static readonly Color Crosshair = Gfx.C(1f, 1f, 0.2f, 0.95f);

	private static readonly Color PickedShade = Gfx.C(0f, 0f, 0f, 0.45f);

	private static readonly Color LabelTick = Gfx.C(0.12f, 0.5f, 0.12f, 1f);

	private static readonly Color PointerShadow = Gfx.C(0f, 0f, 0f, 0.35f);

	private enum HoldAction
	{
		None,
		Go,
		Forget,
		/// <summary>Bring the movable item to Nate.</summary>
		Summon,
		/// <summary>Name the kept location.</summary>
		Rename
	}

	/// <summary>What's being held: the pad's Y, X or A, Space, Delete, or the right mouse button.</summary>
	private enum HoldInput
	{
		PadY,
		PadX,
		PadA,
		Space,
		Delete,
		MouseRight
	}

	private readonly ModContext _ctx;

	/// <summary>Screen pixels per map pixel.</summary>
	private float _zoom = 1f;

	private float _minZoom = 0.1f;

	private float _maxZoom = 5f;

	/// <summary>Map pixel under the crosshair (the screen centre).</summary>
	private Vector2 _cursor;

	/// <summary>The POI under the crosshair or the pointer, or null.</summary>
	private Poi _selected;

	// A button held on a saved location or a movable item: what it will do, which button, on what, since when.
	private HoldAction _hold;

	private HoldInput _holdInput;

	private Poi _holdTarget;

	private float _holdStart;

	/// <summary>The last frame the hold was looked at; a gap (the guide was open, say) drops it.</summary>
	private int _holdFrame;

	/// <summary>Where the header and footer panels were last drawn; the pointer doesn't pick POIs through them.</summary>
	private Rect _headerRect;

	private Rect _footerRect;

	/// <summary>The Hero's Journey, shown over the map while open.</summary>
	private readonly JourneyScreen _journey;

	/// <summary>Naming a kept location, over the map while open.</summary>
	private readonly RenameBox _rename;

	private readonly List<(string text, GUIStyle style)> _headerLines = new List<(string, GUIStyle)>(4);

	/// <summary>The header's lines by position, as GUIContents (see <see cref="LabelText"/>).</summary>
	private readonly LabelText[] _headerText = { new LabelText(), new LabelText(), new LabelText(), new LabelText() };

	// The "Tracking:" line, rebuilt only when what it says changes.
	private Poi _trackTarget;

	/// <summary>The target's name when the line was made (a location can be renamed while tracked).</summary>
	private string _trackTitle;

	private int _trackMetres = int.MinValue;

	private bool _trackHint;

	private string _trackLine;

	// The footer's help, rebuilt only when what it says changes.
	private readonly LabelText _footerText = new LabelText();

	private int _footerKey = int.MinValue;

	private string _footerLine;

	private string _completedLine;

	private int _completedVersion = -1;

	private bool _completedAttached;

	private bool _completedHidden;

	// The label for _selected, rebuilt only when what it shows changes.
	private Poi _tipPoi;

	private int _tipVersion = -1;

	private InputMode _tipMode;

	private int _tipDistance = -1;

	private bool _tipTarget;

	private bool _tipLive;

	private int _tipLocationsVersion = -1;

	private float _tipScale = -1f;

	private bool _tipDone;

	private string _tipTitle;

	private string _tipInfo;

	private string _tipAction;

	private Vector2 _tipTitleSize;

	private Vector2 _tipInfoSize;

	private Vector2 _tipActionSize;

	// The label's lines as GUIContents in the game, for measuring and drawing without new strings each frame.
	private readonly LabelText _tipTitleText = new LabelText();

	private readonly LabelText _tipInfoText = new LabelText();

	private readonly LabelText _tipActionText = new LabelText();

	private GUIStyle _title;

	private GUIStyle _line;

	private GUIStyle _track;

	private GUIStyle _tip;

	private GUIStyle _credit;

	private GUIStyle _labelTitle;

	private GUIStyle _labelInfo;

	private GUIStyle _labelAction;

	/// <summary>The number or initials on a saved location's pin, and on the picked one, at their usual sizes.</summary>
	private GUIStyle _badge;

	private GUIStyle _badgeBig;

	private int _badgeSize;

	private int _badgeBigSize;

	/// <summary>The badge style's own padding, left and right (px): a measured width includes it.</summary>
	private float _badgePadding;

	/// <summary>Each pin text's badge style, on a pin and on the picked one: smaller for wide ones (<see cref="BadgeFor"/>).</summary>
	private readonly Dictionary<string, GUIStyle> _badgeFit = new Dictionary<string, GUIStyle>();

	private readonly Dictionary<string, GUIStyle> _badgeFitBig = new Dictionary<string, GUIStyle>();

	/// <summary>Badge styles smaller than the usual, by font size.</summary>
	private readonly Dictionary<int, GUIStyle> _badgeSmaller = new Dictionary<int, GUIStyle>();

	private float _builtScale = -1f;

	public FullMapScreen(ModContext ctx)
	{
		_ctx = ctx;
		_journey = new JourneyScreen(ctx);
		_rename = new RenameBox(ctx);
	}

	/// <summary>The Hero's Journey is up (it has its own controls, and the guide stays shut).</summary>
	public bool JourneyOpen => _journey.IsOpen;

	/// <summary>A location's name is being typed: the keyboard is for that alone (no map keys, no M, F9 or guide).</summary>
	public bool Renaming => _rename.IsOpen;

	private Vector2 ScreenCenter => Fast.V2(Frame.ScreenWidth * 0.5f, Frame.ScreenHeight * 0.5f);

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

	/// <summary>Reset the zoom and centre on the tracked target, else on Nate, else on the middle of the map.</summary>
	public void Open()
	{
		MapProjection projection = _ctx.Projection;
		_minZoom = (Frame.ScreenHeight - Ui.S(80f)) / projection.Height;
		_maxZoom = MaxScreenPxPerMetre / projection.PxPerMetre;
		if (_ctx.Target != null)
		{
			_cursor = Fast.V2(_ctx.Target.Px, _ctx.Target.Py);
		}
		else if (_ctx.CanLocate)
		{
			_cursor = _ctx.Projection.WorldToMap(_ctx.Player.WorldPos);
		}
		else
		{
			_cursor = Fast.V2(projection.Width * 0.5f, projection.Height * 0.5f);
		}
		_cursor = projection.Clamp(_cursor);
		_zoom = Fast.Clamp(OpenScreenPxPerMetre / projection.PxPerMetre, _minZoom, _maxZoom);
		_selected = null;
		CancelHold();
		_rename.Close();
	}

	/// <summary>Shut, along with the Journey and a name being typed (which is left as it was).</summary>
	public void Close()
	{
		_selected = null;
		CancelHold();
		_journey.Close();
		_rename.Close();
	}

	public void Update(float dt)
	{
		if (dt <= 0f)
		{
			dt = 0.016f;
		}
		Controls c = _ctx.Controls;
		Pointer pointer = _ctx.Pointer;
		if (_journey.IsOpen)
		{
			_journey.Update(dt);
			return;
		}
		if (_rename.IsOpen)
		{
			// The box has the keyboard, the pad and the mouse until it's shut.
			_rename.Update();
			return;
		}
		if (c.Pressed(Gamepad.DPAD_LEFT, KeyCode.J))
		{
			_selected = null;
			CancelHold();
			_journey.Open();
			return;
		}
		UpdateZoom(dt);
		if (pointer.Dragging)
		{
			_cursor = _ctx.Projection.Clamp(Fast.V2(_cursor.x - pointer.Delta.x / _zoom, _cursor.y - pointer.Delta.y / _zoom));
		}
		Vector2 move = c.Move();
		float step = PanScreenSpeed * dt / _zoom;
		_cursor = _ctx.Projection.Clamp(Fast.V2(_cursor.x + move.x * step, _cursor.y - move.y * step));
		if (!pointer.Active)
		{
			_selected = PickAt(ScreenCenter, Ui.S(CrosshairReach));
		}
		else if (Gfx.Contains(_headerRect, pointer.Position) || Gfx.Contains(_footerRect, pointer.Position))
		{
			_selected = null;
		}
		else
		{
			_selected = PickAt(pointer.Position, Ui.S(PointerReach));
		}
		UpdateHold(_selected);
		if (_rename.IsOpen)
		{
			// A held long enough on a kept location: the box takes over from the next frame.
			return;
		}
		// After the hold: forgetting a location unpicks it.
		SavedLocation location = _selected as SavedLocation;
		if (Controls.KeyDown(KeyCode.F2))
		{
			StartRename(location);
			if (_rename.IsOpen)
			{
				return;
			}
		}

		string pickHint = pointer.Active ? "point at a landmark" : "put the crosshair on a landmark";
		// A tracks what's picked. On a kept location the pad's A is held to rename it, so there a
		// tap tracks it once A comes back up (UpdateHold).
		bool padA = c.Pad.Pressed(Gamepad.A);
		if (padA && location != null && location.IsKept && _hold == HoldAction.None)
		{
			StartHold(HoldAction.Rename, HoldInput.PadA, location);
		}
		else if (padA || Controls.KeyDown(KeyCode.Return) || Controls.KeyDown(KeyCode.KeypadEnter) || (pointer.Clicked && _selected != null))
		{
			if (_selected != null)
			{
				_ctx.Track(_selected);
			}
			else
			{
				_ctx.Toasts.Show("Nothing to track here - " + pickHint + " first");
			}
		}
		// Y, Space or a right-click: mark a POI completed; held on a saved location, go there; held
		// on a movable item, bring it to Nate.
		bool padY = c.Pad.Pressed(Gamepad.Y);
		bool space = Controls.KeyDown(KeyCode.Space);
		bool rightClick = pointer.RightClicked && _selected != null;
		if (padY || space || rightClick)
		{
			HoldInput input = padY ? HoldInput.PadY : (space ? HoldInput.Space : HoldInput.MouseRight);
			if (location != null)
			{
				StartHold(HoldAction.Go, input, location);
			}
			else if (_selected != null && _selected.Item != null)
			{
				if (_ctx.Links.WithNate(_selected))
				{
					_ctx.Toasts.Show(_selected.Title + " is with Nate already");
				}
				else
				{
					StartHold(HoldAction.Summon, input, _selected);
				}
			}
			else if (_selected != null)
			{
				_ctx.ToggleDone(_selected);
			}
			else
			{
				_ctx.Toasts.Show("To mark something completed, " + pickHint + " first");
			}
		}
		if (c.Pressed(Gamepad.B, KeyCode.Backspace) && _ctx.Target != null)
		{
			_ctx.Target = null;
			_ctx.Toasts.Show("Tracking cleared");
		}
		// X finds Nate; held on a saved location it forgets it instead (a tap still finds Nate).
		bool padX = c.Pad.Pressed(Gamepad.X);
		if (padX && location != null && _hold == HoldAction.None)
		{
			StartHold(HoldAction.Forget, HoldInput.PadX, location);
		}
		else if (padX || Controls.KeyDown(KeyCode.C))
		{
			FindNate();
		}
		if (Controls.KeyDown(KeyCode.Delete) && location != null)
		{
			StartHold(HoldAction.Forget, HoldInput.Delete, location);
		}
		if (c.Pressed(Gamepad.L3, KeyCode.F5))
		{
			_ctx.SaveLocation();
		}
		if (c.Pressed(Gamepad.LT, KeyCode.Q))
		{
			StepLocation(-1);
		}
		if (c.Pressed(Gamepad.RT, KeyCode.E))
		{
			StepLocation(1);
		}
		if (c.Pressed(Gamepad.R3, KeyCode.H))
		{
			_ctx.ToggleHideCompleted();
		}
		if (c.Pressed(Gamepad.LB, KeyCode.Comma))
		{
			_ctx.AdjustUiScale(-0.1f);
		}
		if (c.Pressed(Gamepad.RB, KeyCode.Period))
		{
			_ctx.AdjustUiScale(0.1f);
		}
	}

	private void FindNate()
	{
		if (_ctx.CanLocate)
		{
			_cursor = _ctx.Projection.Clamp(_ctx.Projection.WorldToMap(_ctx.Player.WorldPos));
		}
		else
		{
			_ctx.Toasts.Show("Nate isn't on the map yet");
		}
	}

	/// <summary>Put the crosshair on the next (1) or previous (-1) saved location.</summary>
	private void StepLocation(int direction)
	{
		SavedLocation next = _ctx.Locations.Step(_selected as SavedLocation, direction);
		if (next == null)
		{
			_ctx.Toasts.Show("No saved locations yet - " + ((Mode == InputMode.Pad) ? "L3" : "F5") + " saves where Nate is");
			return;
		}
		_cursor = _ctx.Projection.Clamp(Fast.V2(next.Px, next.Py));
		_ctx.Pointer.UseCrosshair();
		_selected = next;
	}

	/// <summary>F2: name the kept location that's picked, or say why it can't be.</summary>
	private void StartRename(SavedLocation location)
	{
		if (location == null)
		{
			_ctx.Toasts.Show("To rename a saved location, " + (_ctx.Pointer.Active ? "point at its pin" : "put the crosshair on its pin") + " first");
			return;
		}
		if (!location.IsKept)
		{
			_ctx.Toasts.Show(location.IsQuick ? "The quick save can't be renamed: every F5 saves over it" : "\"Where you were\" can't be renamed: every trip moves it");
			return;
		}
		CancelHold();
		_rename.Open(location);
	}

	private void StartHold(HoldAction action, HoldInput input, Poi target)
	{
		if (_hold != HoldAction.None)
		{
			return;
		}
		_hold = action;
		_holdInput = input;
		_holdTarget = target;
		_holdStart = Frame.Now;
		_holdFrame = Frame.Count;
	}

	private void CancelHold()
	{
		_hold = HoldAction.None;
		_holdTarget = null;
	}

	/// <summary>Seconds the current hold has lasted.</summary>
	private float HoldElapsed => Frame.Now - _holdStart;

	private bool HoldInputDown()
	{
		switch (_holdInput)
		{
		case HoldInput.PadY:
			return _ctx.Controls.Pad.Down(Gamepad.Y);
		case HoldInput.PadX:
			return _ctx.Controls.Pad.Down(Gamepad.X);
		case HoldInput.PadA:
			return _ctx.Controls.Pad.Down(Gamepad.A);
		case HoldInput.Space:
			return Controls.Key(KeyCode.Space);
		case HoldInput.Delete:
			return Controls.Key(KeyCode.Delete);
		default:
			return _ctx.Pointer.RightHeld;
		}
	}

	private static string HoldInputName(HoldInput input)
	{
		switch (input)
		{
		case HoldInput.PadY:
			return "Y";
		case HoldInput.PadX:
			return "X";
		case HoldInput.PadA:
			return "A";
		case HoldInput.Space:
			return "Space";
		case HoldInput.Delete:
			return "Delete";
		default:
			return "the right button";
		}
	}

	/// <summary>
	/// Carry on with a button held on a saved location or a movable item: once it's been held long
	/// enough, go there, forget it, name it or bring the item to Nate. Let go early, it was a tap.
	/// Moving off it, or anything that stopped the map looking (the guide over it, say), calls it off.
	/// </summary>
	private void UpdateHold(Poi picked)
	{
		if (_hold == HoldAction.None)
		{
			return;
		}
		int frame = Frame.Count;
		bool interrupted = frame - _holdFrame > 1;
		_holdFrame = frame;
		if (interrupted || picked != _holdTarget)
		{
			CancelHold();
			return;
		}
		HoldAction action = _hold;
		HoldInput input = _holdInput;
		SavedLocation location = picked as SavedLocation;
		if (!HoldInputDown())
		{
			CancelHold();
			if (input == HoldInput.PadX)
			{
				FindNate();
			}
			else if (input == HoldInput.PadA)
			{
				// A tapped on a kept location tracks it, as it does anything else.
				_ctx.Track(picked);
			}
			else
			{
				_ctx.Toasts.Show("Hold " + HoldInputName(input) + " to " + HoldPurpose(action, picked));
			}
			return;
		}
		if (HoldElapsed < HoldTime)
		{
			return;
		}
		CancelHold();
		switch (action)
		{
		case HoldAction.Go:
			// Core closes the map; the game moves Nate once it's running again.
			_ctx.GoTo(location);
			break;
		case HoldAction.Forget:
			_ctx.ForgetLocation(location);
			_selected = null;
			break;
		case HoldAction.Rename:
			_rename.Open(location);
			break;
		default:
			// The map stays open: the game loads the item while it's frozen, and its dot moves to Nate.
			_ctx.Summon(picked);
			break;
		}
	}

	/// <summary>What holding the button on <paramref name="target"/> does, for "Hold Y to ...".</summary>
	private static string HoldPurpose(HoldAction action, Poi target)
	{
		switch (action)
		{
		case HoldAction.Go:
			SavedLocation location = (SavedLocation)target;
			return (location.IsKept ? "go to " : "go back to ") + location.Phrase;
		case HoldAction.Forget:
			return "forget " + ((SavedLocation)target).Phrase;
		case HoldAction.Rename:
			return "rename " + ((SavedLocation)target).Phrase;
		default:
			return "bring " + target.Title + " to Nate";
		}
	}

	private void UpdateZoom(float dt)
	{
		Controls c = _ctx.Controls;
		Pointer pointer = _ctx.Pointer;
		Vector2 center = ScreenCenter;
		float dir = 0f;
		if (c.Held(Gamepad.DPAD_UP, KeyCode.PageUp, KeyCode.Equals, KeyCode.KeypadPlus))
		{
			dir += 1f;
		}
		if (c.Held(Gamepad.DPAD_DOWN, KeyCode.PageDown, KeyCode.Minus, KeyCode.KeypadMinus))
		{
			dir -= 1f;
		}
		if (Fast.Abs(pointer.Wheel) > 0.01f)
		{
			// The wheel zooms where the pointer is.
			ZoomBy(Fast.Pow(1.2f, pointer.Wheel), pointer.Active ? pointer.Position : center);
		}
		if (dir != 0f)
		{
			ZoomBy(Fast.Pow(2.2f, dir * dt), center);
		}
	}

	private void ZoomBy(float factor, Vector2 anchor)
	{
		float zoom = Fast.Clamp(_zoom * factor, _minZoom, _maxZoom);
		_cursor = _ctx.Projection.Clamp(MapView.ZoomAbout(anchor, ScreenCenter, _zoom, zoom, _cursor));
		_zoom = zoom;
	}

	/// <summary>What's under <paramref name="screen"/>: a saved location's pin (they're drawn on top), else a POI.</summary>
	private Poi PickAt(Vector2 screen, float reach)
	{
		SavedLocation location = LocationAt(screen, reach);
		return (location != null) ? location : PoiAt(screen, reach);
	}

	/// <summary>The saved location whose pin (its point or its head) is nearest <paramref name="screen"/>, within <paramref name="reach"/> screen px.</summary>
	private SavedLocation LocationAt(Vector2 screen, float reach)
	{
		Vector2 center = ScreenCenter;
		float headRise = PinHeadRise(Ui.S(PinSize));
		SavedLocation best = null;
		float bestSq = reach * reach;
		foreach (SavedLocation location in _ctx.Locations.All)
		{
			Vector2 s = MapView.ToScreen(Fast.V2(location.Px, location.Py), center, _zoom, _cursor);
			float dx = s.x - screen.x;
			float dy = s.y - screen.y;
			float headDy = dy - headRise;
			float distSq = Fast.Min(dx * dx + dy * dy, dx * dx + headDy * headDy);
			if (distSq <= bestSq)
			{
				bestSq = distSq;
				best = location;
			}
		}
		return best;
	}

	/// <summary>The POI whose dot is nearest <paramref name="screen"/>, within <paramref name="reach"/> screen px.</summary>
	private Poi PoiAt(Vector2 screen, float reach)
	{
		Vector2 center = ScreenCenter;
		Poi best = null;
		float bestSq = reach * reach;
		foreach (Poi poi in _ctx.Map.Pois)
		{
			if (_ctx.Hidden(poi))
			{
				continue;
			}
			Vector2 s = MapView.ToScreen(Fast.V2(poi.Px, poi.Py), center, _zoom, _cursor);
			float dx = s.x - screen.x;
			float dy = s.y - screen.y;
			float distSq = dx * dx + dy * dy;
			if (distSq <= bestSq)
			{
				bestSq = distSq;
				best = poi;
			}
		}
		return best;
	}

	/// <param name="interactive">False while the guide is over the map: no POI label then.</param>
	public void Draw(bool interactive)
	{
		if (_journey.IsOpen)
		{
			_journey.Draw();
			return;
		}
		EnsureStyles();
		float sw = Frame.ScreenWidth;
		float sh = Frame.ScreenHeight;
		Vector2 center = Fast.V2(sw * 0.5f, sh * 0.5f);
		Rect screen = Gfx.R(0f, 0f, sw, sh);
		GUI.color = Backdrop;
		GUI.DrawTexture(screen, GuiTextures.White);
		GUI.color = Gfx.White;
		MapDraw.DrawMap(_ctx.Art, screen, center, _zoom, _cursor);
		bool renaming = _rename.IsOpen;
		// While a location is being named, its pin stays picked under the box.
		Poi selected = (interactive || renaming) ? _selected : null;
		DrawPois(center, sw, sh, selected);
		DrawLocations(center, sw, sh, selected as SavedLocation);

		Poi target = _ctx.Target;
		if (target != null)
		{
			Vector2 s = MapView.ToScreen(Fast.V2(target.Px, target.Py), center, _zoom, _cursor);
			float size = Ui.S(32f);
			GUI.color = TargetRing;
			GUI.DrawTexture(Gfx.R(s.x - size * 0.5f, s.y - size * 0.5f, size, size), GuiTextures.Ring);
			GUI.color = Gfx.White;
		}

		if (_ctx.CanLocate)
		{
			PlayerTracker player = _ctx.Player;
			Vector2 s = MapView.ToScreen(_ctx.Projection.WorldToMap(player.WorldPos), center, _zoom, _cursor);
			GUI.matrix = Gfx.Rotation(MapProjection.MapAngle(player.Forward), s);
			float size = Ui.S(18f);
			GUI.color = Theme.Player;
			GUI.DrawTexture(Gfx.R(s.x - size * 0.5f, s.y - size * 0.5f, size, size), GuiTextures.Arrow);
			GUI.color = Gfx.White;
			GUI.matrix = Gfx.Identity;
		}

		// The crosshair is the pad's and keyboard's pointer; the mouse brings its own.
		if (!_ctx.Pointer.Active)
		{
			float arm = Ui.S(16f);
			float thick = Ui.S(2f);
			GUI.color = Crosshair;
			GUI.DrawTexture(Gfx.R(center.x - thick * 0.5f, center.y - arm, thick, arm * 2f), GuiTextures.White);
			GUI.DrawTexture(Gfx.R(center.x - arm, center.y - thick * 0.5f, arm * 2f, thick), GuiTextures.White);
			GUI.color = Gfx.White;
		}

		DrawHeader(center.x);
		DrawFooter(sw, sh);
		if (selected != null && !renaming)
		{
			DrawLabel(selected, center, sw, sh);
		}
		if (renaming)
		{
			_rename.Draw();
		}
	}

	/// <summary>POI dots. Completed ones fade and get a tick; the picked one is drawn larger, ringed and on top.</summary>
	private void DrawPois(Vector2 center, float sw, float sh, Poi selected)
	{
		PoiProgress progress = _ctx.Progress;
		float dotSize = Ui.S(12f);
		float tickSize = Ui.S(15f);
		foreach (Poi poi in _ctx.Map.Pois)
		{
			if (poi == selected || _ctx.Hidden(poi))
			{
				continue;
			}
			Vector2 s = MapView.ToScreen(Fast.V2(poi.Px, poi.Py), center, _zoom, _cursor);
			if (s.x < -20f || s.x > sw + 20f || s.y < -20f || s.y > sh + 20f)
			{
				continue;
			}
			DrawDot(poi, s, dotSize, tickSize, progress.IsDone(poi));
		}
		if (selected != null && !(selected is SavedLocation))
		{
			Vector2 s = MapView.ToScreen(Fast.V2(selected.Px, selected.Py), center, _zoom, _cursor);
			float ring = Ui.S(27f);
			GUI.color = PickedShade;
			GUI.DrawTexture(Gfx.R(s.x - ring * 0.5f, s.y - ring * 0.5f, ring, ring), GuiTextures.Disc);
			GUI.color = Gfx.White;
			GUI.DrawTexture(Gfx.R(s.x - ring * 0.5f, s.y - ring * 0.5f, ring, ring), GuiTextures.Ring);
			DrawDot(selected, s, dotSize * 1.4f, tickSize * 1.3f, progress.IsDone(selected));
			if (_hold == HoldAction.Summon && _holdTarget == selected && HoldElapsed >= HoldRingDelay)
			{
				float held = Fast.Clamp01((HoldElapsed - HoldRingDelay) / (HoldTime - HoldRingDelay));
				DrawHoldRing(s, ring * 0.5f + Ui.S(7f), held, HoldGo);
			}
		}
	}

	/// <summary>Saved locations' pins, over the POIs; the picked one bigger, with the ring of a button being held on it.</summary>
	private void DrawLocations(Vector2 center, float sw, float sh, SavedLocation selected)
	{
		float width = Ui.S(PinSize);
		foreach (SavedLocation location in _ctx.Locations.All)
		{
			if (location == selected)
			{
				continue;
			}
			Vector2 s = MapView.ToScreen(Fast.V2(location.Px, location.Py), center, _zoom, _cursor);
			if (OnScreen(s, sw, sh))
			{
				DrawPin(location, s, width, BadgeFor(location.Badge, width, _badge, _badgeSize, _badgeFit));
			}
		}
		if (selected == null)
		{
			return;
		}
		Vector2 tip = MapView.ToScreen(Fast.V2(selected.Px, selected.Py), center, _zoom, _cursor);
		float big = width * PickedPinScale;
		DrawPin(selected, tip, big, BadgeFor(selected.Badge, big, _badgeBig, _badgeBigSize, _badgeFitBig));
		if (_hold != HoldAction.None && _holdTarget == selected && HoldElapsed >= HoldRingDelay)
		{
			float progress = Fast.Clamp01((HoldElapsed - HoldRingDelay) / (HoldTime - HoldRingDelay));
			float headRadius = big * GuiTextures.PinHeadRadius / GuiTextures.PinWidth;
			Color lit = (_hold == HoldAction.Go) ? HoldGo : ((_hold == HoldAction.Rename) ? HoldRename : HoldForget);
			DrawHoldRing(Fast.V2(tip.x, tip.y - PinHeadRise(big)), headRadius + Ui.S(7f), progress, lit);
		}
	}

	/// <summary>How far above its point a pin <paramref name="width"/> wide has the middle of its head.</summary>
	private static float PinHeadRise(float width)
	{
		return (GuiTextures.PinTipY - GuiTextures.PinHeadY) * width / GuiTextures.PinWidth;
	}

	/// <summary>
	/// The style for <paramref name="text"/> on a pin <paramref name="width"/> wide: <paramref name="style"/>
	/// (font <paramref name="size"/>) when the text fits inside the head's rim, else smaller until it
	/// does, though no smaller than can be read (initials like "WM" are wide). Measured once per text,
	/// and again when the UI's size changes (EnsureStyles empties <paramref name="fits"/>).
	/// </summary>
	private GUIStyle BadgeFor(string text, float width, GUIStyle style, int size, Dictionary<string, GUIStyle> fits)
	{
		if (text.Length == 0)
		{
			return style;
		}
		if (fits.TryGetValue(text, out GUIStyle fit))
		{
			return fit;
		}
		fit = style;
		float room = width * BadgeRoom;
		int smallest = Fast.Max(7, Fast.RoundToInt(8f * Ui.Scale));
		GUIContent content = Gfx.Fixed(text);
		float wide = style.CalcSize(content).x - _badgePadding;
		MemoryDiagnostics.GuiContents++;
		if (wide > room && size > smallest)
		{
			// Text widens with its font size, near enough: start there, and go down until it fits.
			int next = Fast.Clamp((int)(size * room / wide), smallest, size - 1);
			while (true)
			{
				fit = SmallerBadge(next);
				wide = fit.CalcSize(content).x - _badgePadding;
				MemoryDiagnostics.GuiContents++;
				if (wide <= room || next <= smallest)
				{
					break;
				}
				next--;
			}
		}
		fits[text] = fit;
		return fit;
	}

	/// <summary>The badge style at font size <paramref name="size"/>, made the first time it's wanted.</summary>
	private GUIStyle SmallerBadge(int size)
	{
		if (!_badgeSmaller.TryGetValue(size, out GUIStyle style))
		{
			style = new GUIStyle(_badge)
			{
				fontSize = size
			};
			_badgeSmaller[size] = style;
		}
		return style;
	}

	/// <summary>
	/// A saved location's pin with its point at <paramref name="tip"/>, and its number or initials on
	/// the head when <paramref name="badge"/> is given (the minimap leaves them off).
	/// </summary>
	public static void DrawPin(SavedLocation location, Vector2 tip, float width, GUIStyle badge)
	{
		float height = width * GuiTextures.PinHeight / GuiTextures.PinWidth;
		Rect r = Gfx.R(tip.x - width * 0.5f, tip.y - height * GuiTextures.PinTipY / GuiTextures.PinHeight, width, height);
		float shadow = Fast.Max(1f, width * 0.07f);
		GUI.color = PinShadow;
		GUI.DrawTexture(Gfx.R(r.m_XMin + shadow, r.m_YMin + shadow, width, height), GuiTextures.Pin);
		GUI.color = Theme.PinColor(location);
		GUI.DrawTexture(r, GuiTextures.Pin);
		GUI.color = Gfx.White;
		if (badge != null && location.Badge.Length > 0)
		{
			// Twice the head's size, centred on it: the label's own padding can't push the text off centre.
			float box = width * 2f * GuiTextures.PinHeadRadius / GuiTextures.PinWidth * 2f;
			float headY = tip.y - PinHeadRise(width);
			GUI.Label(Gfx.R(tip.x - box * 0.5f, headY - box * 0.5f, box, box), Gfx.Fixed(location.Badge), badge);
		}
	}

	/// <summary>A ring of dots round a pin's head, lit clockwise from the top as a held button nears its time.</summary>
	private static void DrawHoldRing(Vector2 centre, float radius, float progress, Color lit)
	{
		const int dots = 20;
		int on = Fast.CeilToInt(progress * dots);
		float size = Ui.S(5.5f);
		for (int i = 0; i < dots; i++)
		{
			float a = (-90f + 360f * i / dots) * Fast.Deg2Rad;
			float x = centre.x + Fast.Cos(a) * radius;
			float y = centre.y + Fast.Sin(a) * radius;
			GUI.color = (i < on) ? lit : HoldUnlit;
			GUI.DrawTexture(Gfx.R(x - size * 0.5f, y - size * 0.5f, size, size), GuiTextures.Disc);
		}
		GUI.color = Gfx.White;
	}

	/// <summary>A POI's dot (GUI.color white on the way in and out).</summary>
	private static void DrawDot(Poi poi, Vector2 s, float dotSize, float tickSize, bool done)
	{
		Rect dot = Gfx.R(s.x - dotSize * 0.5f, s.y - dotSize * 0.5f, dotSize, dotSize);
		if (!done)
		{
			GUI.DrawTexture(dot, GuiTextures.Dot(poi.Cat));
			return;
		}
		GUI.color = DoneDot;
		GUI.DrawTexture(dot, GuiTextures.Dot(poi.Cat));
		GUI.color = DoneTick;
		GUI.DrawTexture(Gfx.R(s.x - tickSize * 0.5f, s.y - tickSize * 0.5f, tickSize, tickSize), GuiTextures.Check);
		GUI.color = Gfx.White;
	}

	/// <summary>The picked POI's label: name, category and progress, distance, and what the buttons do.</summary>
	private void DrawLabel(Poi poi, Vector2 center, float sw, float sh)
	{
		BuildLabel(poi);
		Vector2 s = MapView.ToScreen(Fast.V2(poi.Px, poi.Py), center, _zoom, _cursor);
		float pad = Ui.S(10f);
		float gap = Ui.S(2f);
		float swatch = Ui.S(11f);
		float indent = swatch + Ui.S(6f);
		float tick = _tipActionSize.y;
		float actionIndent = _tipDone ? tick + Ui.S(4f) : 0f;
		float w = Fast.Max(_tipTitleSize.x, Fast.Max(_tipInfoSize.x + indent, _tipActionSize.x + actionIndent)) + pad * 2f;
		float h = _tipTitleSize.y + _tipInfoSize.y + _tipActionSize.y + gap * 2f + pad * 2f;
		float margin = Ui.S(8f);
		float offset = Ui.S(14f);
		// Above and to the right of the POI, clear of the pointer, which hangs down and right.
		float left = s.x + offset;
		float top = s.y - offset - h;
		if (left + w > sw - margin)
		{
			left = s.x - offset - w;
		}
		if (top < margin)
		{
			top = s.y + Ui.S(30f);
		}
		left = Fast.Clamp(left, margin, Fast.Max(margin, sw - w - margin));
		top = Fast.Clamp(top, margin, Fast.Max(margin, sh - h - Ui.S(64f)));
		Theme.Panel(Gfx.R(left, top, w, h), fill: true);
		float x = left + pad;
		float y = top + pad;
		float inner = w - pad * 2f;
		GUI.Label(Gfx.R(x, y, inner, _tipTitleSize.y), _tipTitleText.Get(_tipTitle), _labelTitle);
		y += _tipTitleSize.y + gap;
		if (poi is SavedLocation location)
		{
			float pinW = swatch * 0.95f;
			float pinH = pinW * GuiTextures.PinHeight / GuiTextures.PinWidth;
			GUI.color = Theme.PinColor(location);
			GUI.DrawTexture(Gfx.R(x + (swatch - pinW) * 0.5f, y + (_tipInfoSize.y - pinH) * 0.5f, pinW, pinH), GuiTextures.Pin);
			GUI.color = Gfx.White;
		}
		else
		{
			GUI.DrawTexture(Gfx.R(x, y + (_tipInfoSize.y - swatch) * 0.5f, swatch, swatch), GuiTextures.Dot(poi.Cat));
		}
		GUI.Label(Gfx.R(x + indent, y, inner - indent, _tipInfoSize.y), _tipInfoText.Get(_tipInfo), _labelInfo);
		y += _tipInfoSize.y + gap;
		if (_tipDone)
		{
			GUI.color = LabelTick;
			GUI.DrawTexture(Gfx.R(x, y, tick, tick), GuiTextures.Check);
			GUI.color = Gfx.White;
		}
		GUI.Label(Gfx.R(x + actionIndent, y, inner - actionIndent, _tipActionSize.y), _tipActionText.Get(_tipAction), _labelAction);
	}

	private void BuildLabel(Poi poi)
	{
		PoiProgress progress = _ctx.Progress;
		InputMode mode = Mode;
		bool withNate = _ctx.Links.WithNate(poi);
		int distance = (_ctx.CanLocate && !withNate) ? Fast.RoundToInt(WorldLoop.FlatDistance(_ctx.Player.WorldPos, poi.World)) : -1;
		bool target = _ctx.Target == poi;
		if (poi == _tipPoi && progress.Version == _tipVersion && mode == _tipMode && distance == _tipDistance && target == _tipTarget
			&& poi.Live == _tipLive && Ui.Scale == _tipScale && _ctx.Locations.Version == _tipLocationsVersion)
		{
			return;
		}
		_tipLocationsVersion = _ctx.Locations.Version;
		_tipLive = poi.Live;
		_tipPoi = poi;
		_tipVersion = progress.Version;
		_tipMode = mode;
		_tipDistance = distance;
		_tipTarget = target;
		_tipScale = Ui.Scale;
		if (poi is SavedLocation location)
		{
			BuildLocationLabel(location, mode, distance, target);
			MeasureLabel();
			return;
		}
		_tipDone = progress.IsDone(poi);
		// This kind's progress, counting only what can be ticked off.
		int doneInCat = 0;
		int total = 0;
		foreach (Poi other in _ctx.Map.Pois)
		{
			if (other.Cat != poi.Cat || progress.IsGone(other) || !other.Completable)
			{
				continue;
			}
			total++;
			if (progress.IsDone(other))
			{
				doneInCat++;
			}
		}
		_tipTitle = poi.Title;
		string where = withNate ? "  ·  with Nate" : ((distance >= 0) ? $"  ·  {distance} m away" : "");
		if (poi.Live && !withNate)
		{
			where += " (moved)";
		}
		_tipInfo = ((poi.Completable && total > 0) ? $"{poi.CatName}  ·  {doneInCat}/{total} completed" : poi.CatName) + where;
		string track;
		string mark;
		string bring;
		switch (mode)
		{
		case InputMode.Mouse:
			track = "Click: track";
			mark = _tipDone ? "Right-click: unmark" : "Right-click: mark completed";
			bring = "Hold right-click: bring to Nate";
			break;
		case InputMode.Keys:
			track = "Enter: track";
			mark = _tipDone ? "Space: unmark" : "Space: mark completed";
			bring = "Hold Space: bring to Nate";
			break;
		default:
			track = "A: track";
			mark = _tipDone ? "Y: unmark" : "Y: mark completed";
			bring = "Hold Y: bring to Nate";
			break;
		}
		// A movable item can be brought to Nate, unless it's with him.
		bring = (poi.Item != null && !withNate) ? ("     " + bring) : "";
		if (progress.IsFound(poi))
		{
			// The game's own record: nothing to unmark.
			_tipAction = "Found in the game" + (target ? "     Tracking" : "") + bring;
		}
		else if (progress.IsUnmarkable(poi))
		{
			// A movable item (tracked, never ticked off) or a landmark.
			_tipAction = (target ? "Tracking" : track) + bring;
		}
		else
		{
			_tipAction = (_tipDone ? "Completed" : (target ? "Tracking" : track)) + "     " + mark;
		}
		MeasureLabel();
	}

	/// <summary>A saved location's label: its name, how far, and the buttons for track, rename, go and forget.</summary>
	private void BuildLocationLabel(SavedLocation location, InputMode mode, int distance, bool target)
	{
		_tipDone = false;
		_tipTitle = location.Title;
		_tipInfo = location.CatName + ((distance >= 0) ? $"  ·  {distance} m away" : "");
		string go = location.IsKept ? "go here" : "go back";
		string track;
		string rename;
		string hold;
		switch (mode)
		{
		case InputMode.Mouse:
			track = "Click: track";
			rename = "F2: rename";
			hold = "Hold right-click: " + go + "     Hold Del: forget";
			break;
		case InputMode.Keys:
			track = "Enter: track";
			rename = "F2: rename";
			hold = "Hold Space: " + go + "     Hold Del: forget";
			break;
		default:
			track = "A: track";
			rename = "Hold A: rename";
			hold = "Hold Y: " + go + "     Hold X: forget";
			break;
		}
		// Only kept locations take a name: the quick save and "where you were" are saved over.
		_tipAction = (target ? "Tracking" : track) + (location.IsKept ? "     " + rename : "") + "     " + hold;
	}

	private void MeasureLabel()
	{
		_tipTitleSize = _labelTitle.CalcSize(_tipTitleText.Get(_tipTitle));
		_tipInfoSize = _labelInfo.CalcSize(_tipInfoText.Get(_tipInfo));
		_tipActionSize = _labelAction.CalcSize(_tipActionText.Get(_tipAction));
		MemoryDiagnostics.GuiContents += 3;
	}

	private void DrawHeader(float cx)
	{
		Poi target = _ctx.Target;
		PoiProgress progress = _ctx.Progress;
		List<(string text, GUIStyle style)> lines = _headerLines;
		lines.Clear();
		bool hidden = _ctx.Config.HideCompleted;
		if (progress.Version != _completedVersion || progress.Attached != _completedAttached || hidden != _completedHidden)
		{
			_completedVersion = progress.Version;
			_completedAttached = progress.Attached;
			_completedHidden = hidden;
			_completedLine = $"Completed: {progress.Count} / {CompletableCount()}" + (hidden ? "  (hidden)" : "")
				+ (progress.Attached ? "" : "  - no save loaded, not kept");
		}
		lines.Add((_completedLine, _line));
		if (target != null)
		{
			int metres = _ctx.Player.Found ? Fast.RoundToInt(_ctx.TargetDistance()) : -1;
			bool hint = _ctx.HelpLevel < 3;
			if (target != _trackTarget || !ReferenceEquals(target.Title, _trackTitle) || metres != _trackMetres || hint != _trackHint || _trackLine == null)
			{
				_trackTarget = target;
				_trackTitle = target.Title;
				_trackMetres = metres;
				_trackHint = hint;
				_trackLine = "Tracking: " + target.Title + ((metres >= 0) ? $"  ({metres} m)" : "") + (hint ? "  -  set Help Lv3 for the guide arrow" : "");
			}
			lines.Add((_trackLine, _track));
		}
		if (!_ctx.CanLocate)
		{
			lines.Add(("Waiting for Nate...", _line));
		}
		float titleH = Ui.S(38f);
		float lineH = Ui.S(30f);
		float w = Ui.S(680f);
		float left = cx - w * 0.5f;
		Rect r = Gfx.R(left, Ui.S(10f), w, titleH + lines.Count * lineH + Ui.S(14f));
		_headerRect = r;
		Theme.Panel(r, fill: true);
		float y = r.m_YMin + Ui.S(6f);
		GUI.Label(Gfx.R(left, y, w, titleH), Gfx.Fixed("BABY STEPS MAP"), _title);
		y += titleH;
		for (int i = 0; i < lines.Count && i < _headerText.Length; i++)
		{
			GUI.Label(Gfx.R(left, y, w, lineH), _headerText[i].Get(lines[i].text), lines[i].style);
			y += lineH;
		}
	}

	/// <summary>POIs that count: not landmarks for reference, no items the game can't tick off, none gone from the game.</summary>
	private int CompletableCount()
	{
		int count = 0;
		foreach (Poi poi in _ctx.Map.Pois)
		{
			if (poi.Completable && !_ctx.Progress.IsGone(poi))
			{
				count++;
			}
		}
		return count;
	}

	private void DrawFooter(float sw, float sh)
	{
		Rect r = Gfx.R(Ui.S(24f), sh - Ui.S(52f), sw - Ui.S(48f), Ui.S(32f));
		_footerRect = r;
		Theme.Panel(r, fill: true);
		InputMode mode = Mode;
		bool hidden = _ctx.Config.HideCompleted;
		// What the help says depends on these; the zoom is shown to one decimal.
		int key = (Fast.RoundToInt(_zoom * 10f) * 4 + (int)mode) * 2 + (hidden ? 1 : 0);
		if (key != _footerKey || _footerLine == null)
		{
			_footerKey = key;
			string format = (mode == InputMode.Mouse) ? MouseHelp : ((mode == InputMode.Pad) ? PadHelp : KeyHelp);
			_footerLine = string.Format(format, _zoom, hidden ? "show" : "hide");
		}
		GUI.Label(r, _footerText.Get(_footerLine), _tip);
		GUI.Label(Gfx.R(Ui.S(12f), sh - Ui.S(18f), Ui.S(520f), Ui.S(16f)), Gfx.Fixed("Map: the game's own painted sheets  -  places: Map Genie"), _credit);
	}

	/// <summary>The mouse pointer, drawn last so it's over everything (the map hides the OS cursor).</summary>
	public static void DrawPointer(Vector2 position)
	{
		float size = Ui.S(26f);
		float tip = GuiTextures.PointerTip * size / GuiTextures.PointerSize;
		Rect r = Gfx.R(position.x - tip, position.y - tip, size, size);
		GUI.color = PointerShadow;
		GUI.DrawTexture(Gfx.R(position.x - tip + Ui.S(2f), position.y - tip + Ui.S(2f), size, size), GuiTextures.Pointer);
		GUI.color = Gfx.White;
		GUI.DrawTexture(r, GuiTextures.Pointer);
	}

	private static bool OnScreen(Vector2 s, float sw, float sh)
	{
		return s.x > -40f && s.x < sw + 40f && s.y > -40f && s.y < sh + 40f;
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
				textColor = Gfx.C(0.34f, 0.22f, 0.1f)
			}
		};
		_track = new GUIStyle(GUI.skin.label)
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
		_credit = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(13f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.LowerLeft,
			normal =
			{
				textColor = Gfx.C(0.78f, 0.78f, 0.82f, 0.5f)
			}
		};
		_labelTitle = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(19f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleLeft,
			wordWrap = false,
			normal =
			{
				textColor = Theme.InkText
			}
		};
		_labelInfo = new GUIStyle(_labelTitle)
		{
			fontSize = Ui.Font(15f),
			normal =
			{
				textColor = Gfx.C(0.34f, 0.22f, 0.1f)
			}
		};
		_labelAction = new GUIStyle(_labelTitle)
		{
			fontSize = Ui.Font(14f),
			normal =
			{
				textColor = Gfx.C(0.45f, 0.3f, 0.12f)
			}
		};
		_badgeSize = Ui.Font(11.5f);
		_badgeBigSize = Ui.Font(11.5f * PickedPinScale);
		_badge = new GUIStyle(GUI.skin.label)
		{
			fontSize = _badgeSize,
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			wordWrap = false,
			clipping = TextClipping.Overflow,
			normal =
			{
				textColor = Gfx.White
			}
		};
		_badgeBig = new GUIStyle(_badge)
		{
			fontSize = _badgeBigSize
		};
		RectOffset padding = _badge.padding;
		_badgePadding = (padding != null) ? padding.left + padding.right : 0f;
		_badgeFit.Clear();
		_badgeFitBig.Clear();
		_badgeSmaller.Clear();
		// Sizes in the label cache depend on these styles.
		_tipScale = -1f;
	}
}
