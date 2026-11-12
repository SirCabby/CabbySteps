using UnityEngine;

namespace CabbySteps;

/// <summary>
/// The guide shown over the full map: once automatically the first time the map opens,
/// and again whenever D-pad Right is pressed there.
/// </summary>
public class HelpScreen
{
	private static readonly string[,] Pages = new string[3, 2]
	{
		{ "WELCOME, FELLOW STUMBLER", "Nate's life ain't perfect, but at least now you'll know where he's standing.\n\nThe map is the game's own: the painted sheets its ending shows, read from your copy of the game. Nate is put on them exactly as the game itself does it (it notes where he goes, to paint his journey at the end), height and all: the sheets look down on the mountain from the south, so a ledge above him is drawn a little higher up the map. Nothing to set up. The list of places comes from Map Genie, a community site: huge thanks to them." },
		{ "FINDING YOUR WAY", "•  Left stick pans, D-pad Up/Down zooms, LB/RB changes the size of everything. With a mouse, drag to pan and scroll to zoom.\n•  Put the crosshair or the mouse on a landmark to see what it is. A (or a click) tracks it, B stops tracking, X brings the crosshair back to Nate.\n•  Fruit, cutscenes, cans and the like tick themselves off as you find them, and a hat once Nate has dreamt about it (wear it to sleep at a campfire). Y (or a right-click) ticks off the rest; points of interest and graffiti are just landmarks. R3 (or H) hides what's done. Things you can carry show where they really are. Ticks are saved with your game: each save slot keeps its own, and a new game starts with none.\n•  Help levels (D-pad Left/Right while walking): 1 = map only, 2 = adds a minimap, 3 = adds a guide arrow to whatever you're tracking. D-pad Up while walking changes the arrow's style." },
		{ "THE FANCY STUFF", "A few toys beyond the map:\n•  Saved locations: L3 here (F5 on the keyboard) saves where Nate is standing, which way he faces and just how he stands, as a red numbered pin that stays until you forget it. While playing, F5 is a quick save instead: one blue Q pin, saved over by every F5, and F9, any time (even when he's down after a fall), takes him straight back to it, standing exactly as he was. LT/RT (Q/E) hops between pins. On one, hold Y (Space, or the right mouse button) to go back there, or hold X (Delete) to forget it; A tracks it like any landmark. Hold A (or press F2) on a red one to give it a name: its pin then shows the name's initials instead of a number. Going somewhere leaves a grey \"Where you were\" pin, so you can always go back.\n•  Lost a chick, or fancy a hat? Put the crosshair or the mouse on any hat, item, chick or duckling and hold Y (Space, or the right mouse button): it's brought to Nate and put down beside him.\n•  Hero's Journey: press D-pad Left here (J) to watch your path so far painted onto the game's own map sheets, dot by red dot, just as the game paints it.\n•  First person: click L3 while playing — lovely for fiddly footwork.\n•  Grippy feet, a cheat: F7 on the keyboard, playing or here. Planted feet can't slip, on mud, ice or a steep slope, and Nate keeps his balance however he leans while a foot is down, so a narrow perch holds him. Stepping off into thin air still drops him, and cacti still get him. A GRIPPY FEET tag shows in the corner while it's on.\n•  F7 again is EXTREME GRIP: put a foot down while it's pressed against anything (a wall, a cliff face, the side of a rock) and it holds right there like a ledge, so he can walk up sheer walls one foothold at a time. Pull that foot's trigger to let go. F7 a third time turns the cheat off, and a foot on a wall comes away.\n•  Hold tight, another cheat: F8 on the keyboard, playing or here. What Nate holds stays in his hands and his hat stays on: a fall, a knock, the wind (the propeller hat) or a night at a camp can't take them, the Baby Trophy can't wriggle free, and a vase in his hand doesn't break. Picking up something else, or putting on another hat, still swaps; handing things over, using them up and the ice cream melting are the game's as ever. A HOLD TIGHT tag shows in the corner while it's on.\n\nPop this guide open again any time with D-pad Right (or / on the keyboard)." }
	};

	private readonly ModContext _ctx;

	private int _page;

	private bool _open;

	private GUIStyle _title;

	private GUIStyle _body;

	private GUIStyle _foot;

	private float _builtScale = -1f;

	private static readonly Color Dim = Gfx.C(0f, 0f, 0f, 0.55f);

	// The body's height for this page and UI scale, measured once rather than every frame.
	private int _measuredPage = -1;

	private float _measuredScale = -1f;

	private float _bodyHeight;

	private readonly LabelText _footText = new LabelText();

	private int _footKey = -1;

	private string _footLine;

	public HelpScreen(ModContext ctx)
	{
		_ctx = ctx;
	}

	/// <summary>Open on request, or because the guide hasn't been seen yet.</summary>
	public bool Visible => _open || !_ctx.Config.SeenIntro;

	public void Open()
	{
		_open = true;
		_page = 0;
	}

	public void Hide()
	{
		_open = false;
	}

	public void Update()
	{
		Controls c = _ctx.Controls;
		int pages = Pages.GetLength(0);
		if (c.Pressed(Gamepad.DPAD_RIGHT, KeyCode.RightArrow))
		{
			_page = (_page + 1) % pages;
		}
		if (c.Pressed(Gamepad.DPAD_LEFT, KeyCode.LeftArrow))
		{
			_page = (_page + pages - 1) % pages;
		}
		// The mouse: a click turns the page (and closes after the last), a right-click closes.
		Pointer pointer = _ctx.Pointer;
		if (pointer.Clicked && _page < pages - 1)
		{
			_page++;
		}
		else if (pointer.Clicked || pointer.RightClicked || c.Pressed(Gamepad.A, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space) || c.Pressed(Gamepad.B, KeyCode.Escape))
		{
			_open = false;
			if (!_ctx.Config.SeenIntro)
			{
				_ctx.Config.SeenIntro = true;
				_ctx.Config.Save();
			}
		}
	}

	public void Draw()
	{
		EnsureStyles();
		int pages = Pages.GetLength(0);
		_page = Fast.Clamp(_page, 0, pages - 1);
		GUIContent title = Gfx.Fixed(Pages[_page, 0]);
		GUIContent body = Gfx.Fixed(Pages[_page, 1]);
		float sw = Frame.ScreenWidth;
		float sh = Frame.ScreenHeight;
		float padX = Ui.S(28f);
		float w = Ui.S(680f);
		float textW = w - padX * 2f;
		float titleH = Ui.S(40f);
		float gap = Ui.S(14f);
		if (_page != _measuredPage || Ui.Scale != _measuredScale)
		{
			_measuredPage = _page;
			_measuredScale = Ui.Scale;
			_bodyHeight = _body.CalcHeight(body, textW);
			MemoryDiagnostics.GuiContents++;
		}
		float bodyH = _bodyHeight;
		float footH = Ui.S(30f);
		float h = Fast.Min(Ui.S(20f) + titleH + gap + bodyH + gap + footH + Ui.S(16f), sh - Ui.S(40f));
		float left = (sw - w) * 0.5f;
		float top = (sh - h) * 0.5f;
		Rect r = Gfx.R(left, top, w, h);
		GUI.color = Dim;
		GUI.DrawTexture(Gfx.R(0f, 0f, sw, sh), GuiTextures.White);
		GUI.color = Gfx.White;
		Theme.Panel(r, fill: true);
		Theme.CornerStuds(r);
		float y = top + Ui.S(16f);
		GUI.Label(Gfx.R(left, y, w, titleH), title, _title);
		y += titleH + gap;
		GUI.Label(Gfx.R(left + padX, y, textW, bodyH), body, _body);
		int footKey = _page * 2 + (_ctx.Pointer.Active ? 1 : 0);
		if (footKey != _footKey || _footLine == null)
		{
			_footKey = footKey;
			string close = !_ctx.Pointer.Active ? "A = got it" : ((_page < pages - 1) ? "Click = next   Right-click = close" : "Click = got it");
			_footLine = (pages > 1) ? $"◄  {_page + 1} / {pages}  ►      {close}" : close;
		}
		GUI.Label(Gfx.R(left, top + h - Ui.S(36f), w, footH), _footText.Get(_footLine), _foot);
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
			fontSize = Ui.Font(26f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.UpperCenter,
			normal =
			{
				textColor = Theme.InkText
			}
		};
		_body = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(16f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.UpperLeft,
			wordWrap = true,
			normal =
			{
				textColor = Gfx.C(0.24f, 0.16f, 0.07f)
			}
		};
		_foot = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(16f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			normal =
			{
				textColor = Gfx.C(0.13f, 0.5f, 0.15f)
			}
		};
	}
}
