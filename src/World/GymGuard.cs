using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;

namespace CabbySteps;

/// <summary>
/// Keeps a gym's time trial going through a trip. The game's gym runs (<c>JesseGym</c>: the
/// Underwater Gym's sits on its room) start over whenever anything comes into the room's trigger:
/// <c>OnTriggerEnter</c> zeroes the time and forgets the checkpoints passed, whatever it was that
/// came in. The game's teleport switches Nate's ragdoll off and back on where it puts him, and his
/// body coming back on inside the room counts as coming in, so a trip within the gym (F9 back to a
/// quick save on the course, say) threw the run away.
/// <para>
/// While a trip is under way and for a moment after Nate arrives (<see cref="Arm"/>,
/// <see cref="Arrived"/>), the room still switches its things on as it always does, but its time
/// and checkpoints are put back as they were just before. The clock keeps running through the
/// trip. Walking into the room still starts the run over, and so does the start line. If the hook
/// can't be made (after a game update, say), trips work as before.
/// </para>
/// </summary>
public static class GymGuard
{
	/// <summary>
	/// For this long after Nate arrives (seconds the game runs, not counting time frozen under the
	/// map), a gym still can't start over: his body comes back on in the physics steps that follow.
	/// </summary>
	private const float Grace = 1.5f;

	private static bool _armed;

	private static bool _arrived;

	private static float _graceLeft;

	// What the gym had as something came in, put back once it's been through.
	private static bool _saved;

	private static float _time;

	private static bool[] _passed = new bool[16];

	private static int _passedCount;

	private static int _failures;

	private static MelonLogger.Instance _log;

	/// <summary>The hook is in: trips leave gym runs alone.</summary>
	public static bool Installed { get; private set; }

	/// <summary>Since the last <see cref="Arm"/>: how often a gym's run was put back after a trip started it over.</summary>
	public static int Kept { get; private set; }

	public static void Install(HarmonyLib.Harmony harmony, MelonLogger.Instance log)
	{
		_log = log;
		try
		{
			harmony.Patch(AccessTools.Method(typeof(JesseGym), nameof(JesseGym.OnTriggerEnter)),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(GymGuard), nameof(BeforeEnter))),
				postfix: new HarmonyMethod(AccessTools.Method(typeof(GymGuard), nameof(AfterEnter))));
			Installed = true;
		}
		catch (Exception ex)
		{
			log.Warning("Saved locations: can't hook the gyms' timers, so a trip within a gym starts its run over, as the game's own teleport does: " + ex.Message);
		}
	}

	/// <summary>A trip is starting: until Nate has arrived and a moment after, no gym starts its run over.</summary>
	public static void Arm()
	{
		_armed = true;
		_arrived = false;
		_graceLeft = Grace;
		_saved = false;
		Kept = 0;
	}

	/// <summary>Nate is there: gyms are left alone for <see cref="Grace"/> seconds more of the game running.</summary>
	public static void Arrived()
	{
		if (_armed)
		{
			_arrived = true;
			_graceLeft = Grace;
		}
	}

	/// <summary>The trip didn't happen after all: gyms behave as usual again.</summary>
	public static void Disarm()
	{
		_armed = false;
		_arrived = false;
		_saved = false;
	}

	/// <summary>Every frame. <paramref name="running"/>: the game isn't frozen under the map.</summary>
	public static void Tick(bool running, float dt)
	{
		if (_armed && _arrived && running)
		{
			_graceLeft -= dt;
			if (_graceLeft <= 0f)
			{
				Disarm();
			}
		}
	}

	// The hooks run inside the game's physics, so neither lets an exception out.

	private static void BeforeEnter(JesseGym __instance)
	{
		_saved = false;
		if (!_armed)
		{
			return;
		}
		try
		{
			Il2CppStructArray<bool> passed = __instance.checkpointsPassed;
			int count = ReferenceEquals(passed, null) ? 0 : passed.Length;
			if (count > _passed.Length)
			{
				_passed = new bool[count];
			}
			for (int i = 0; i < count; i++)
			{
				_passed[i] = passed[i];
			}
			_passedCount = count;
			_time = __instance.time;
			_saved = true;
		}
		catch (Exception ex)
		{
			Fail("couldn't read a gym's run", ex);
		}
	}

	private static void AfterEnter(JesseGym __instance)
	{
		if (!_saved)
		{
			return;
		}
		_saved = false;
		try
		{
			bool changed = __instance.time != _time;
			__instance.time = _time;
			Il2CppStructArray<bool> passed = __instance.checkpointsPassed;
			int count = ReferenceEquals(passed, null) ? 0 : Math.Min(passed.Length, _passedCount);
			int passedCount = 0;
			for (int i = 0; i < count; i++)
			{
				if (passed[i] != _passed[i])
				{
					passed[i] = _passed[i];
					changed = true;
				}
				if (_passed[i])
				{
					passedCount++;
				}
			}
			if (changed && Kept++ == 0)
			{
				_log?.Msg($"Recall: the gym's run kept going through the trip ({_time:0.0} s, {passedCount} of {count} checkpoints passed).");
			}
		}
		catch (Exception ex)
		{
			Fail("couldn't put a gym's run back", ex);
		}
	}

	private static void Fail(string what, Exception ex)
	{
		if (_failures++ < 3)
		{
			_log?.Warning("Saved locations: " + what + ": " + ex.Message);
		}
	}
}
