using UnityEngine;

namespace CabbySteps;

/// <summary>
/// The mouse while the full map is open. The game hides and locks the cursor for play, so
/// opening the map unlocks it and closing the map puts back whatever the game last wanted.
/// The OS cursor stays hidden and the map draws its own pointer (which also shows up when a
/// compositor such as gamescope wouldn't draw the system one). The mouse becomes the input in
/// use when it moves, clicks or scrolls; the stick or the pan keys hand back to the crosshair.
/// Positions are GUI coordinates (origin top left).
/// </summary>
public class Pointer
{
	/// <summary>Movement (px) after which a held left button is a drag rather than a click.</summary>
	private const float DragThreshold = 6f;

	/// <summary>Frames to ignore after opening, while the unlocked cursor settles.</summary>
	private const int SettleFrames = 3;

	private bool _open;

	private int _settle;

	private bool _pressed;

	private Vector2 _pressPosition;

	private CursorLockMode _gameLock;

	private bool _gameVisible;

	/// <summary>The mouse is the input in use: its pointer is drawn and it picks POIs.</summary>
	public bool Active { get; private set; }

	public Vector2 Position { get; private set; }

	/// <summary>Movement since the last frame.</summary>
	public Vector2 Delta { get; private set; }

	/// <summary>The left button is held and has moved past the drag threshold.</summary>
	public bool Dragging { get; private set; }

	/// <summary>The left button came up this frame without dragging.</summary>
	public bool Clicked { get; private set; }

	/// <summary>The right button went down this frame.</summary>
	public bool RightClicked { get; private set; }

	/// <summary>The right button is held.</summary>
	public bool RightHeld { get; private set; }

	/// <summary>Scroll this frame, in notches (up positive).</summary>
	public float Wheel { get; private set; }

	public void Open()
	{
		_open = true;
		_gameLock = Cursor.lockState;
		_gameVisible = Cursor.visible;
		_settle = SettleFrames;
		_pressed = false;
		Active = Dragging = Clicked = RightClicked = RightHeld = false;
		Delta = default;
		Wheel = 0f;
		Apply(Cursor.lockState, Cursor.visible);
	}

	public void Close()
	{
		if (!_open)
		{
			return;
		}
		_open = false;
		Active = Dragging = Clicked = RightClicked = RightHeld = false;
		Cursor.lockState = _gameLock;
		Cursor.visible = _gameVisible;
	}

	/// <param name="crosshairInput">The stick or the pan keys were used this frame.</param>
	public void Update(bool crosshairInput)
	{
		if (!_open)
		{
			return;
		}
		// Anything other than what Apply set came from the game (its pause menu, say): that's
		// what to put back on close.
		CursorLockMode lockState = Cursor.lockState;
		bool visible = Cursor.visible;
		if (lockState != CursorLockMode.None || visible)
		{
			_gameLock = lockState;
			_gameVisible = visible;
		}
		Vector3 mouse = Input.mousePosition;
		Vector2 position = Fast.V2(mouse.x, Frame.ScreenHeight - mouse.y);
		if (_settle > 0)
		{
			_settle--;
			Position = position;
			Apply(lockState, visible);
			return;
		}
		Delta = Fast.V2(position.x - Position.x, position.y - Position.y);
		Position = position;
		Wheel = Input.mouseScrollDelta.y;
		// Mouse buttons only when some button is down (see Controls): a release shows as no longer held.
		bool held = Controls.AnyKey && Input.GetMouseButton(0);
		bool down = Controls.AnyKeyDown && Input.GetMouseButtonDown(0);
		bool up = _pressed && !held;
		RightClicked = Controls.AnyKeyDown && Input.GetMouseButtonDown(1);
		RightHeld = Controls.AnyKey && Input.GetMouseButton(1);
		Clicked = false;
		if (down)
		{
			_pressed = true;
			_pressPosition = position;
			Dragging = false;
		}
		if (_pressed && !Dragging)
		{
			float dx = position.x - _pressPosition.x;
			float dy = position.y - _pressPosition.y;
			Dragging = dx * dx + dy * dy > DragThreshold * DragThreshold;
		}
		if (up && _pressed)
		{
			Clicked = !Dragging;
			_pressed = false;
			Dragging = false;
		}
		bool used = Delta.x * Delta.x + Delta.y * Delta.y > 0.5f || down || RightClicked || Fast.Abs(Wheel) > 0.01f;
		if (used)
		{
			Active = true;
		}
		else if (crosshairInput)
		{
			Active = false;
		}
		Apply(lockState, visible);
	}

	/// <summary>Hand the map back to the crosshair (it was moved by a button, not by the mouse) until the mouse is used again.</summary>
	public void UseCrosshair()
	{
		Active = false;
	}

	/// <summary>Free the cursor and hide it, given what it is now.</summary>
	private static void Apply(CursorLockMode lockState, bool visible)
	{
		if (lockState != CursorLockMode.None)
		{
			Cursor.lockState = CursorLockMode.None;
		}
		if (visible)
		{
			Cursor.visible = false;
		}
	}
}
