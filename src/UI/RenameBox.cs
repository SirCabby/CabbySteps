using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Naming a kept saved location, over the full map (hold A on its pin, or F2). A keyboard types
/// straight in; a pad picks characters off the on-screen keyboard (<see cref="NameEntry"/>) with the
/// D-pad or the left stick and A, and the mouse clicks them. Enter, RT or Done keeps the name; Esc,
/// B or a right-click leaves the location as it was. A name left empty numbers it ("Location 3").
/// </summary>
public class RenameBox
{
	/// <summary>A direction held on the D-pad or the stick moves once, then again after this long, then this often.</summary>
	private const float RepeatDelay = 0.4f;

	private const float RepeatEvery = 0.09f;

	/// <summary>X held deletes once, then again after this long, then this often.</summary>
	private const float DeleteDelay = 0.45f;

	private const float DeleteEvery = 0.06f;

	/// <summary>How far the left stick has to be pushed to count as a direction.</summary>
	private const float StickOn = 0.55f;

	private const string PadHint = "A = type   X = delete   Y = space   LT = shift   RT = done   B = cancel";

	private const string MouseHint = "Click the keys, or type   Enter = done   Esc or right-click = cancel";

	private const string KeysHint = "Type the name   Backspace = delete   Enter = done   Esc = cancel";

	private static readonly Color Dim = Gfx.C(0f, 0f, 0f, 0.5f);

	private static readonly Color FieldFill = Gfx.C(0.97f, 0.92f, 0.79f, 1f);

	private static readonly Color KeyFill = Gfx.C(0.76f, 0.63f, 0.42f, 1f);

	private static readonly Color KeyEdge = Gfx.C(0.16f, 0.11f, 0.06f, 0.6f);

	private static readonly Color ShiftOn = Gfx.C(0.95f, 0.75f, 0.3f, 1f);

	private static readonly Color DoneFill = Gfx.C(0.56f, 0.78f, 0.42f, 1f);

	private readonly ModContext _ctx;

	private readonly NameEntry _entry = new NameEntry(SavedLocations.MaxNameLength);

	/// <summary>The location being named, or null when the box is shut.</summary>
	private SavedLocation _location;

	// What the box says for this location: "RENAME LOCATION 3" (a named one: "RENAME LOCATION"), "Location 3" faded in an empty field, and the note under it.
	private string _title;

	private string _placeholder;

	private string _emptyNote;

	// A direction held on the D-pad or the stick, for repeats.
	private bool _waitForRelease;

	private int _heldDx;

	private int _heldDy;

	private float _heldSince;

	private float _repeatedAt;

	// X held to delete, for repeats.
	private bool _deleting;

	private float _deleteSince;

	private float _deletedAt;

	/// <summary>When the name last changed: the caret stays lit while typing and blinks once it stops.</summary>
	private float _editedAt;

	// Where each key was last drawn (screen px), for the mouse.
	private readonly Rect[] _charKeys = new Rect[NameEntry.CharRows * NameEntry.Columns];

	private readonly Rect[] _bottomKeys = new Rect[NameEntry.BottomCount];

	private bool _laidOut;

	// The text in the game as GUIContents, replaced only when it changes.
	private readonly LabelText _titleText = new LabelText();

	private readonly LabelText _nameText = new LabelText();

	private readonly LabelText _noteText = new LabelText();

	private readonly LabelText _countText = new LabelText();

	private readonly LabelText _hintText = new LabelText();

	private int _countFor = -1;

	private string _countLine;

	/// <summary>The name's width in its style, measured when it changes (-1: measure again).</summary>
	private float _nameWidth = -1f;

	private GUIStyle _titleStyle;

	private GUIStyle _nameStyle;

	private GUIStyle _placeholderStyle;

	private GUIStyle _noteStyle;

	private GUIStyle _countStyle;

	private GUIStyle _keyStyle;

	private GUIStyle _wideKeyStyle;

	private GUIStyle _hintStyle;

	private float _builtScale = -1f;

	public RenameBox(ModContext ctx)
	{
		_ctx = ctx;
	}

	public bool IsOpen => _location != null;

	/// <summary>Start naming <paramref name="location"/> (a kept one): an empty field if it's numbered, else its name to edit.</summary>
	public void Open(SavedLocation location)
	{
		_location = location;
		// What it's called with the field left empty: its own number's name, or a named one's next.
		string numbered = _ctx.Locations.NameIfCleared(location);
		_title = location.IsNamed ? "RENAME LOCATION" : "RENAME " + numbered.ToUpperInvariant();
		_placeholder = numbered;
		_emptyNote = (location.IsNamed ? "Left empty, it becomes " : "Left empty, it's called ") + numbered;
		_entry.Reset(location.IsNamed ? location.Title : "");
		// The stick or D-pad may still be held from moving about the map.
		_waitForRelease = true;
		_heldDx = _heldDy = 0;
		_deleting = false;
		_editedAt = Frame.Now;
		_laidOut = false;
		_nameWidth = -1f;
	}

	/// <summary>Shut it, leaving the location's name as it was.</summary>
	public void Close()
	{
		_location = null;
		_laidOut = false;
	}

	/// <summary>Typing, picking keys, done or cancel. Only while it's open.</summary>
	public void Update()
	{
		if (_location == null)
		{
			return;
		}
		Gamepad pad = _ctx.Controls.Pad;
		Pointer pointer = _ctx.Pointer;
		string before = _entry.Text;
		TypeFromKeyboard();
		if (pad.Pressed(Gamepad.B) || Controls.KeyDown(KeyCode.Escape) || pointer.RightClicked)
		{
			Close();
			return;
		}
		if (pad.Pressed(Gamepad.RT) || Controls.KeyDown(KeyCode.Return) || Controls.KeyDown(KeyCode.KeypadEnter))
		{
			Finish();
			return;
		}
		float now = Frame.Now;
		Navigate(pad, now);
		// The mouse picks the key it's on, and a click (or A) presses it.
		bool clicked = false;
		if (pointer.Active && _laidOut && KeyUnder(pointer.Position, out int row, out int column))
		{
			_entry.Select(row, column);
			clicked = pointer.Clicked;
		}
		if ((pad.Pressed(Gamepad.A) || clicked) && _entry.Press())
		{
			Finish();
			return;
		}
		if (pad.Pressed(Gamepad.Y))
		{
			_entry.Type(' ');
		}
		if (pad.Pressed(Gamepad.LT))
		{
			_entry.ToggleShift();
		}
		DeleteHeld(pad, now);
		if (!ReferenceEquals(before, _entry.Text))
		{
			_editedAt = now;
		}
	}

	/// <summary>Keep the name (an empty one gives the location its own back) and shut the box.</summary>
	private void Finish()
	{
		SavedLocation location = _location;
		string name = _entry.Text;
		Close();
		_ctx.RenameLocation(location, name);
	}

	/// <summary>What the keyboard typed: characters as its layout and Shift make them, and Backspace.</summary>
	private void TypeFromKeyboard()
	{
		if (!Controls.AnyKey)
		{
			return;
		}
		string typed = Controls.Typed();
		bool deleted = false;
		for (int i = 0; i < typed.Length; i++)
		{
			char c = typed[i];
			if (c == '\b')
			{
				_entry.Backspace();
				deleted = true;
			}
			else if (c != '\n' && c != '\r')
			{
				// Enter is seen as a key (Update); anything else that isn't for a name, Type turns away.
				_entry.Type(c);
			}
		}
		if (!deleted && Controls.KeyDown(KeyCode.Backspace))
		{
			// In case Backspace doesn't come through as a character.
			_entry.Backspace();
		}
	}

	/// <summary>The D-pad or the left stick moves the selection a key at a time, repeating while it's held.</summary>
	private void Navigate(Gamepad pad, float now)
	{
		int dx = (pad.Down(Gamepad.DPAD_LEFT) || pad.LX <= -StickOn) ? -1 : ((pad.Down(Gamepad.DPAD_RIGHT) || pad.LX >= StickOn) ? 1 : 0);
		int dy = (pad.Down(Gamepad.DPAD_UP) || pad.LY >= StickOn) ? -1 : ((pad.Down(Gamepad.DPAD_DOWN) || pad.LY <= -StickOn) ? 1 : 0);
		if (dx != 0 && dy != 0)
		{
			// One way at a time: the one already going, else the stick's stronger.
			if (_heldDx != 0)
			{
				dy = 0;
			}
			else if (_heldDy != 0)
			{
				dx = 0;
			}
			else if (Fast.Abs(pad.LX) >= Fast.Abs(pad.LY))
			{
				dy = 0;
			}
			else
			{
				dx = 0;
			}
		}
		if (_waitForRelease)
		{
			// Held from before the box opened: it counts once it's been let go.
			_waitForRelease = dx != 0 || dy != 0;
			return;
		}
		if (dx != _heldDx || dy != _heldDy)
		{
			_heldDx = dx;
			_heldDy = dy;
			if (dx != 0 || dy != 0)
			{
				_heldSince = _repeatedAt = now;
				Step(dx, dy);
			}
		}
		else if ((dx != 0 || dy != 0) && now - _heldSince >= RepeatDelay && now - _repeatedAt >= RepeatEvery)
		{
			_repeatedAt = now;
			Step(dx, dy);
		}
	}

	private void Step(int dx, int dy)
	{
		_entry.Move(dx, dy);
		// The pad has the keyboard now; the mouse picks again once it moves.
		_ctx.Pointer.UseCrosshair();
	}

	/// <summary>X deletes the last character, and keeps deleting while it's held.</summary>
	private void DeleteHeld(Gamepad pad, float now)
	{
		if (pad.Pressed(Gamepad.X))
		{
			_entry.Backspace();
			_deleting = true;
			_deleteSince = _deletedAt = now;
		}
		else if (_deleting && pad.Down(Gamepad.X))
		{
			if (now - _deleteSince >= DeleteDelay && now - _deletedAt >= DeleteEvery)
			{
				_deletedAt = now;
				_entry.Backspace();
			}
		}
		else
		{
			_deleting = false;
		}
	}

	/// <summary>The key under the pointer, as drawn last.</summary>
	private bool KeyUnder(Vector2 p, out int row, out int column)
	{
		for (int i = 0; i < _charKeys.Length; i++)
		{
			if (Gfx.Contains(_charKeys[i], p))
			{
				row = i / NameEntry.Columns;
				column = i % NameEntry.Columns;
				return true;
			}
		}
		for (int key = 0; key < _bottomKeys.Length; key++)
		{
			if (Gfx.Contains(_bottomKeys[key], p))
			{
				row = NameEntry.BottomRow;
				column = NameEntry.BottomStart(key);
				return true;
			}
		}
		row = column = 0;
		return false;
	}

	/// <summary>The box, centred over the dimmed map: its title, the name being typed, the keyboard and what the buttons do.</summary>
	public void Draw()
	{
		if (_location == null)
		{
			return;
		}
		EnsureStyles();
		float sw = Frame.ScreenWidth;
		float sh = Frame.ScreenHeight;
		GUI.color = Dim;
		GUI.DrawTexture(Gfx.R(0f, 0f, sw, sh), GuiTextures.White);
		GUI.color = Gfx.White;
		float keyW = Ui.S(52f);
		float keyH = Ui.S(44f);
		float gap = Ui.S(6f);
		float gridW = keyW * NameEntry.Columns + gap * (NameEntry.Columns - 1);
		float gridH = keyH * NameEntry.Rows + gap * (NameEntry.Rows - 1);
		float padX = Ui.S(26f);
		float titleH = Ui.S(40f);
		float fieldH = Ui.S(48f);
		float noteH = Ui.S(26f);
		float hintH = Ui.S(28f);
		float w = gridW + padX * 2f;
		float h = Ui.S(16f) + titleH + fieldH + noteH + Ui.S(8f) + gridH + Ui.S(12f) + hintH + Ui.S(10f);
		float left = (sw - w) * 0.5f;
		float top = Fast.Max(Ui.S(12f), (sh - h) * 0.5f);
		Rect box = Gfx.R(left, top, w, h);
		Theme.Panel(box, fill: true);
		Theme.CornerStuds(box);
		float x = left + padX;
		float y = top + Ui.S(16f);
		GUI.Label(Gfx.R(left, y, w, titleH), _titleText.Get(_title), _titleStyle);
		y += titleH;
		DrawField(Gfx.R(x, y, gridW, fieldH));
		y += fieldH;
		DrawNote(Gfx.R(x, y, gridW, noteH));
		y += noteH + Ui.S(8f);
		DrawKeys(x, y, keyW, keyH, gap);
		y += gridH + Ui.S(12f);
		GUI.Label(Gfx.R(left, y, w, hintH), _hintText.Get(Hint()), _hintStyle);
	}

	/// <summary>The name with its caret, or the location's own name faded while it's empty.</summary>
	private void DrawField(Rect r)
	{
		GUI.color = Theme.Ink;
		GUI.DrawTexture(Gfx.Expand(r, Ui.S(2f)), GuiTextures.White);
		GUI.color = FieldFill;
		GUI.DrawTexture(r, GuiTextures.White);
		GUI.color = Gfx.White;
		float inset = Ui.S(12f);
		Rect inner = Gfx.R(r.m_XMin + inset, r.m_YMin, r.m_Width - inset * 2f, r.m_Height);
		string name = _entry.Text;
		GUIContent content = _nameText.Get(name);
		if (_nameText.Changed || _nameWidth < 0f)
		{
			_nameWidth = (name.Length > 0) ? _nameStyle.CalcSize(content).x : 0f;
			MemoryDiagnostics.GuiContents++;
		}
		float caretW = Ui.S(2.5f);
		// A name too long for the field shows its end, where the caret is.
		float shift = Fast.Min(0f, inner.m_Width - caretW - _nameWidth);
		GUI.BeginGroup(inner);
		if (name.Length > 0)
		{
			GUI.Label(Gfx.R(shift, 0f, _nameWidth + Ui.S(8f), inner.m_Height), content, _nameStyle);
		}
		else
		{
			GUI.Label(Gfx.R(0f, 0f, inner.m_Width, inner.m_Height), Gfx.Fixed(_placeholder), _placeholderStyle);
		}
		// Lit while typing; blinking once it stops.
		if (((int)((Frame.Now - _editedAt) * 2f) & 1) == 0)
		{
			float caretH = inner.m_Height * 0.56f;
			GUI.color = Theme.Ink;
			GUI.DrawTexture(Gfx.R(shift + _nameWidth, (inner.m_Height - caretH) * 0.5f, caretW, caretH), GuiTextures.White);
			GUI.color = Gfx.White;
		}
		GUI.EndGroup();
	}

	/// <summary>Under the field: what an empty name means, and how much room is left.</summary>
	private void DrawNote(Rect r)
	{
		int count = _entry.Text.Length;
		if (count != _countFor || _countLine == null)
		{
			_countFor = count;
			_countLine = count + " / " + _entry.MaxLength;
		}
		GUI.Label(r, _noteText.Get(_emptyNote), _noteStyle);
		GUI.Label(r, _countText.Get(_countLine), _countStyle);
	}

	private void DrawKeys(float left, float top, float keyW, float keyH, float gap)
	{
		for (int row = 0; row < NameEntry.CharRows; row++)
		{
			for (int column = 0; column < NameEntry.Columns; column++)
			{
				Rect r = Gfx.R(left + column * (keyW + gap), top + row * (keyH + gap), keyW, keyH);
				_charKeys[row * NameEntry.Columns + column] = r;
				DrawKey(r, _entry.IsSelected(row, column), KeyFill, Gfx.Fixed(_entry.Face(row, column)), _keyStyle);
			}
		}
		float bottom = top + NameEntry.CharRows * (keyH + gap);
		for (int key = 0; key < NameEntry.BottomCount; key++)
		{
			int start = NameEntry.BottomStart(key);
			int span = NameEntry.BottomSpan(key);
			Rect r = Gfx.R(left + start * (keyW + gap), bottom, span * keyW + (span - 1) * gap, keyH);
			_bottomKeys[key] = r;
			NameEntry.Key which = NameEntry.BottomKey(key);
			Color fill = (which == NameEntry.Key.Done) ? DoneFill : ((which == NameEntry.Key.Shift && _entry.Shift) ? ShiftOn : KeyFill);
			DrawKey(r, _entry.IsSelected(NameEntry.BottomRow, start), fill, Gfx.Fixed(Face(which)), _wideKeyStyle);
		}
		_laidOut = true;
	}

	/// <summary>A key: the selected one gold with a heavy ink edge.</summary>
	private static void DrawKey(Rect r, bool selected, Color fill, GUIContent face, GUIStyle style)
	{
		if (selected)
		{
			GUI.color = Theme.Ink;
			GUI.DrawTexture(Gfx.Expand(r, Ui.S(3f)), GuiTextures.White);
			GUI.color = Theme.GoldBright;
		}
		else
		{
			GUI.color = KeyEdge;
			GUI.DrawTexture(Gfx.Expand(r, Ui.S(1.5f)), GuiTextures.White);
			GUI.color = fill;
		}
		GUI.DrawTexture(r, GuiTextures.White);
		GUI.color = Gfx.White;
		GUI.Label(r, face, style);
	}

	private static string Face(NameEntry.Key key)
	{
		switch (key)
		{
		case NameEntry.Key.Shift:
			return "Shift";
		case NameEntry.Key.Space:
			return "Space";
		case NameEntry.Key.Delete:
			return "Delete";
		default:
			return "Done";
		}
	}

	/// <summary>What the buttons do, for what's in use: the mouse, a pad, or just the keyboard.</summary>
	private string Hint()
	{
		if (_ctx.Pointer.Active)
		{
			return MouseHint;
		}
		return _ctx.Controls.Pad.Connected ? PadHint : KeysHint;
	}

	private void EnsureStyles()
	{
		if (_titleStyle != null && _builtScale == Ui.Scale)
		{
			return;
		}
		_builtScale = Ui.Scale;
		MemoryDiagnostics.StyleBuilds++;
		_titleStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(24f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.UpperCenter,
			normal =
			{
				textColor = Theme.InkText
			}
		};
		_nameStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(22f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleLeft,
			wordWrap = false,
			clipping = TextClipping.Overflow,
			normal =
			{
				textColor = Theme.InkText
			}
		};
		_placeholderStyle = new GUIStyle(_nameStyle)
		{
			fontStyle = FontStyle.Italic,
			normal =
			{
				textColor = Gfx.C(0.2f, 0.13f, 0.06f, 0.38f)
			}
		};
		_noteStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(14f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleLeft,
			wordWrap = false,
			normal =
			{
				textColor = Gfx.C(0.45f, 0.3f, 0.12f)
			}
		};
		_countStyle = new GUIStyle(_noteStyle)
		{
			alignment = TextAnchor.MiddleRight
		};
		_keyStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(21f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			wordWrap = false,
			clipping = TextClipping.Overflow,
			normal =
			{
				textColor = Theme.InkText
			}
		};
		_wideKeyStyle = new GUIStyle(_keyStyle)
		{
			fontSize = Ui.Font(16f)
		};
		_hintStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = Ui.Font(15f),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			wordWrap = false,
			normal =
			{
				textColor = Gfx.C(0.3f, 0.22f, 0.12f)
			}
		};
		// The name's width depends on its style.
		_nameWidth = -1f;
	}
}
