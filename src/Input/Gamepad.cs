using System.Runtime.InteropServices;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Reads controller 0 straight from XInput, independently of the game's own
/// input system (Rewired), so the mod's buttons work in any game state.
/// </summary>
public class Gamepad
{
	private struct XINPUT_GAMEPAD
	{
		public ushort wButtons;

		public byte bLeftTrigger;

		public byte bRightTrigger;

		public short sThumbLX;

		public short sThumbLY;

		public short sThumbRX;

		public short sThumbRY;
	}

	private struct XINPUT_STATE
	{
		public uint dwPacketNumber;

		public XINPUT_GAMEPAD Gamepad;
	}

	// XINPUT_GAMEPAD.wButtons bits.
	public const ushort DPAD_UP = 1;

	public const ushort DPAD_DOWN = 2;

	public const ushort DPAD_LEFT = 4;

	public const ushort DPAD_RIGHT = 8;

	public const ushort START = 16;

	/// <summary>The View / Back button.</summary>
	public const ushort BACK = 32;

	public const ushort L3 = 64;

	public const ushort R3 = 128;

	public const ushort LB = 256;

	public const ushort RB = 512;

	public const ushort A = 4096;

	public const ushort B = 8192;

	public const ushort X = 16384;

	public const ushort Y = 32768;

	// The triggers, as buttons: past TriggerOn they're down, below TriggerOff up again.
	public const uint LT = 0x10000;

	public const uint RT = 0x20000;

	private const byte TriggerOn = 140;

	private const byte TriggerOff = 70;

	private uint _buttons;

	private uint _prev;

	/// <summary>0 = untried, 1 = xinput1_4, 2 = xinput9_1_0, -1 = neither available.</summary>
	private int _api;

	[DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
	private static extern int XInputGetState14(int index, ref XINPUT_STATE state);

	[DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
	private static extern int XInputGetState91(int index, ref XINPUT_STATE state);

	public bool Connected { get; private set; }

	// Sticks, -1..1 with a 0.2 dead zone. Y is up-positive.
	public float LX;

	public float LY;

	public float RX;

	public float RY;

	public void Poll()
	{
		_prev = _buttons;
		XINPUT_STATE st = default(XINPUT_STATE);
		int result = TryGet(ref st);
		Connected = result == 0;
		if (!Connected)
		{
			_buttons = 0;
			LX = LY = RX = RY = 0f;
			return;
		}
		_buttons = st.Gamepad.wButtons | Trigger(st.Gamepad.bLeftTrigger, LT) | Trigger(st.Gamepad.bRightTrigger, RT);
		LX = Dz(st.Gamepad.sThumbLX);
		LY = Dz(st.Gamepad.sThumbLY);
		RX = Dz(st.Gamepad.sThumbRX);
		RY = Dz(st.Gamepad.sThumbRY);
	}

	private int TryGet(ref XINPUT_STATE st)
	{
		if (_api == -1)
		{
			return -1;
		}
		try
		{
			if (_api == 0 || _api == 1)
			{
				int result = XInputGetState14(0, ref st);
				_api = 1;
				return result;
			}
		}
		catch
		{
			_api = 2;
		}
		try
		{
			int result = XInputGetState91(0, ref st);
			_api = 2;
			return result;
		}
		catch
		{
			_api = -1;
			return -1;
		}
	}

	/// <summary>The trigger's bit if it counts as pressed: past TriggerOn, or still past TriggerOff once pressed.</summary>
	private uint Trigger(byte value, uint bit)
	{
		bool wasDown = (_prev & bit) != 0;
		return (value >= TriggerOn || (wasDown && value > TriggerOff)) ? bit : 0u;
	}

	private static float Dz(short v)
	{
		float f = (float)v / 32767f;
		if (!(Fast.Abs(f) < 0.2f))
		{
			return Fast.Clamp(f, -1f, 1f);
		}
		return 0f;
	}

	/// <summary>Button held this frame.</summary>
	public bool Down(uint b)
	{
		return (_buttons & b) != 0;
	}

	/// <summary>Button went down this frame.</summary>
	public bool Pressed(uint b)
	{
		if ((_buttons & b) != 0)
		{
			return (_prev & b) == 0;
		}
		return false;
	}
}
