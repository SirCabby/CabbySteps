using System;
using System.Text;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// The mod's input: controller 0 (read through XInput, so it works whatever the game's
/// own input is doing) plus keyboard equivalents. A binding is one pad button and up to
/// three keys; pass 0 for "no button". Buttons are Gamepad's bits, the triggers included.
/// <para>
/// Every Input.GetKey/GetKeyDown is a call into the game that leaves a boxed bool on its heap
/// (see <see cref="Fast"/>), and the mod checks dozens of keys a frame. So each frame starts by
/// asking once whether any key or mouse button is down at all (<see cref="BeginFrame"/>); when
/// none is, which is nearly always with a pad, no key is asked about.
/// </para>
/// </summary>
public class Controls
{
	public readonly Gamepad Pad;

	/// <summary>Some key or mouse button went down this frame.</summary>
	public static bool AnyKeyDown { get; private set; }

	/// <summary>Some key or mouse button is held this frame.</summary>
	public static bool AnyKey { get; private set; }

	public Controls(Gamepad pad)
	{
		Pad = pad;
	}

	/// <summary>Once a frame, before any key is checked.</summary>
	public static void BeginFrame()
	{
		AnyKeyDown = Input.anyKeyDown;
		AnyKey = AnyKeyDown || Input.anyKey;
	}

	/// <summary>The key went down this frame.</summary>
	public static bool KeyDown(KeyCode key)
	{
		return AnyKeyDown && key != KeyCode.None && Input.GetKeyDown(key);
	}

	/// <summary>The key is held.</summary>
	public static bool Key(KeyCode key)
	{
		return AnyKey && key != KeyCode.None && Input.GetKey(key);
	}

	/// <summary>
	/// What the keyboard typed this frame (Input.inputString): characters as its layout and Shift
	/// make them, "\b" for Backspace (repeating while it's held, as the system repeats keys), "\n"
	/// for Enter. "" when no key is held. It's a new string from the game each time, so it's only
	/// asked for while a name is being typed and a key is down.
	/// <para>
	/// The game's own code never asks for it, so it isn't in its build: Il2CppInterop calls the
	/// engine's internal function directly, as it does for Input.anyKeyDown. Should that ever fail,
	/// the keys themselves are read instead (<see cref="TypedByKeys"/>).
	/// </para>
	/// </summary>
	public static string Typed()
	{
		if (!AnyKey)
		{
			return "";
		}
		if (!_inputStringFailed)
		{
			try
			{
				return Input.inputString ?? "";
			}
			catch (Exception ex)
			{
				_inputStringFailed = true;
				KeepAlive.Log?.Warning("Can't read typed text from the game (" + ex.Message + "): names take letters, digits, spaces and - . ' only.");
			}
		}
		return TypedByKeys();
	}

	private static bool _inputStringFailed;

	private static readonly StringBuilder _typedByKeys = new StringBuilder(8);

	/// <summary>Letters, digits, a space and - . ' from the keys that went down this frame: no repeats, no other layouts.</summary>
	private static string TypedByKeys()
	{
		if (!AnyKeyDown)
		{
			return "";
		}
		bool shift = Key(KeyCode.LeftShift) || Key(KeyCode.RightShift);
		_typedByKeys.Clear();
		for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
		{
			if (Input.GetKeyDown(key))
			{
				_typedByKeys.Append((char)((shift ? 'A' : 'a') + (key - KeyCode.A)));
			}
		}
		for (KeyCode key = KeyCode.Alpha0; key <= KeyCode.Alpha9; key++)
		{
			if (Input.GetKeyDown(key))
			{
				_typedByKeys.Append((char)('0' + (key - KeyCode.Alpha0)));
			}
		}
		if (Input.GetKeyDown(KeyCode.Space))
		{
			_typedByKeys.Append(' ');
		}
		if (Input.GetKeyDown(KeyCode.Minus))
		{
			_typedByKeys.Append('-');
		}
		if (Input.GetKeyDown(KeyCode.Period))
		{
			_typedByKeys.Append('.');
		}
		if (Input.GetKeyDown(KeyCode.Quote))
		{
			_typedByKeys.Append('\'');
		}
		return _typedByKeys.ToString();
	}

	/// <summary>The binding went down this frame.</summary>
	public bool Pressed(uint button, KeyCode k1 = KeyCode.None, KeyCode k2 = KeyCode.None, KeyCode k3 = KeyCode.None)
	{
		return Pad.Pressed(button) || KeyDown(k1) || KeyDown(k2) || KeyDown(k3);
	}

	/// <summary>The binding is held.</summary>
	public bool Held(uint button, KeyCode k1 = KeyCode.None, KeyCode k2 = KeyCode.None, KeyCode k3 = KeyCode.None)
	{
		return Pad.Down(button) || Key(k1) || Key(k2) || Key(k3);
	}

	/// <summary>Left stick, or WASD / arrow keys; each axis in -1..1, y up.</summary>
	public Vector2 Move()
	{
		float x = Pad.LX;
		float y = Pad.LY;
		if (AnyKey)
		{
			if (Key(KeyCode.A) || Key(KeyCode.LeftArrow))
			{
				x -= 1f;
			}
			if (Key(KeyCode.D) || Key(KeyCode.RightArrow))
			{
				x += 1f;
			}
			if (Key(KeyCode.W) || Key(KeyCode.UpArrow))
			{
				y += 1f;
			}
			if (Key(KeyCode.S) || Key(KeyCode.DownArrow))
			{
				y -= 1f;
			}
		}
		return Fast.V2(Fast.Clamp(x, -1f, 1f), Fast.Clamp(y, -1f, 1f));
	}

	/// <summary>
	/// The left stick or the pan keys moved this frame, which hands the map back from the mouse
	/// to the crosshair. Buttons don't: with a mouse and a pad together (a Steam Deck's trackpad,
	/// say), A or Y should act on what the pointer is on.
	/// </summary>
	public bool CrosshairInput()
	{
		Vector2 move = Move();
		return move.x != 0f || move.y != 0f;
	}


}
