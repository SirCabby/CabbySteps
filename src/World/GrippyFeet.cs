using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRootMotion.Dynamics;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// The Grippy feet cheat (F7): a planted foot can't slip, and Nate keeps his balance however he
/// leans, so a narrow perch holds him. Read from the game's machine code (<c>PlayerMovement</c>):
/// <para>
/// <b>Slipping</b> is worked out in <c>HipForces</c>, every physics step, for each planted foot. The
/// push the foot gives sideways is set against what it can hold, the push into the ground times the
/// foot's traction (<c>FootData.curTractionCoef</c>, set by <c>GetGrounded</c> from the ground under
/// it: about 0.5 to 1.5, less on mud and ice and on slopes). When the sideways push wins for more
/// than two steps running (<c>slipTimer</c>), the foot gathers speed (<c>slipVel</c>) and the game
/// moves its target by it: the foot slides, and the slide sound plays. While the cheat is on, before
/// every <c>HipForces</c> each planted foot's slide is cancelled (<c>slipVel</c> and
/// <c>slipTimer</c> zero), so a slide can never get going, whatever the ground; and a foot on the
/// ground has traction at least <see cref="Traction"/>, which also lets it push off fully on ice
/// (<c>DoPushOffForce</c>, which caps the push-off at 0.5 anyway).
/// </para>
/// <para>
/// <b>Balance</b> is the game's own tests of where his weight is (<c>centerOfMass</c>, worked out from
/// his body every step) against the outline of his planted feet (<c>GetPolySupport</c>, built in
/// <c>HipForces</c>; see <see cref="SupportPolygon"/>). Beyond his feet: each planted foot's
/// strength fades out as the line from his weight down to the nearest edge of his feet leans past
/// 13° (gone at 28°), so his legs give and he topples; with both feet down, a timer drains it too
/// (<c>magicFallOffTimer</c>); his arms windmill; and <c>DeadCheck</c> counts him off balance
/// (<c>offBalanceDeathTimer</c>), or sitting on his hip (<c>hipHoldingYouUp</c>), and makes him
/// fall (<c>Die</c>). While the cheat is on, right after the outline is built, his weight is counted
/// just inside it if it was beyond it, so every one of those tests finds him balanced. A planted leg
/// also tires as it strains (<c>FootData.fatigue</c>): a tired foot's strength is capped lower
/// (<c>torqueFatigueCurve</c>), and past a point the game jolts his hips at random (the leg
/// shaking). While the cheat is on, a planted leg starts every step rested, and the drain timer
/// starts again too.
/// </para>
/// <para>
/// <b>Falls</b>: everything that knocks Nate down goes through <c>Die</c>. As a safety net, while a
/// foot is planted, the falls the game's own posture rules call for are stopped: from
/// <c>DeadCheck</c> (off balance, legs splayed wider than 1.95 leg lengths, hips sunk to his feet, a
/// planted foot above his hips, propped on his hip) and from <c>HipForces</c> (his hip hitting
/// something hard). So is his ragdoll going limp by itself when a part is knocked far out of place
/// (<c>BehaviourPuppet.SetState(Unpinned)</c> without the game having made him fall). What stays the
/// game's: falling through the air with neither foot down (falling fast, tipping over in the air,
/// landing hard), the things that floor him whatever his footing (cacti, boulders), and continuing a
/// save in which he was down (<c>Menu.UnPause</c>).
/// </para>
/// <para>
/// The hooks go in the first time the cheat is switched on, on a Harmony instance of their own: if
/// any can't be made (after a game update, say), none stays and the cheat stays off.
/// </para>
/// <para>
/// F7 steps through three levels: off, Grippy feet (all of the above), and Extreme grip, which
/// adds footholds anywhere: a foot put down against a wall, a cliff face or the side of a rock
/// holds right there (<see cref="ExtremeGrip"/>).
/// </para>
/// </summary>
public static class GrippyFeet
{
	/// <summary>The least traction a foot on the ground has while the cheat is on (the game's own is about 0.5 to 1.5).</summary>
	public const float Traction = 3f;

	/// <summary>How far inside the outline of his feet his weight is counted when it was beyond it, in metres.</summary>
	public const float Margin = 0.02f;

	private static MelonLogger.Instance _log;

	private static bool _installTried;

	private static int _failures;

	/// <summary>The game's posture rules are running: a fall called for now is one from losing his footing.</summary>
	private static bool _inDeadCheck;

	private static bool _inHipForces;

	// Nate's two feet (0 right, 1 left), looked up once a physics step (before HipForces) for every
	// hook to share: each look at PlayerMovement.feet makes fresh interop wrappers.
	private static FootData _foot0;

	private static FootData _foot1;

	// The outline of his feet, copied out of the game for SupportPolygon.
	private static float[] _xs = new float[32];

	private static float[] _zs = new float[32];

	// Since the cheat was switched on, for the log.
	private static int _steps;

	private static int _steadied;

	private static int _slips;

	private static int _offBalance;

	private static int _slumped;

	private static int _splayed;

	private static int _hipHit;

	private static int _limp;

	/// <summary>Whether each foot's grip was giving way at the end of the last step (so a slide held off counts once).</summary>
	private static readonly bool[] _giving = new bool[2];

	private static float _notedAt = float.MinValue;

	private static int _unnoted;

	/// <summary>How much of the cheat is on.</summary>
	public static GripLevel Level { get; private set; }

	/// <summary>The cheat is on (Grippy feet or Extreme grip).</summary>
	public static bool On => Level != GripLevel.Off;

	/// <summary>Extreme grip is on: a foot put down against anything holds there.</summary>
	public static bool Extreme => Level == GripLevel.Extreme;

	/// <summary>The hooks are in.</summary>
	public static bool Installed { get; private set; }

	public static void Init(MelonLogger.Instance log)
	{
		_log = log;
		ExtremeGrip.Init(log);
	}

	/// <summary>Switch the cheat to a level. Null if done; otherwise why it can't be (the level stays as it was).</summary>
	public static string Switch(GripLevel level)
	{
		if (level != GripLevel.Off && !Install())
		{
			return "the game couldn't be hooked, so it stays off (the log says why)";
		}
		if (level == GripLevel.Extreme && !ExtremeGrip.Install())
		{
			return "extreme grip couldn't hook the game's footing (the log says why)";
		}
		if (level == Level)
		{
			return null;
		}
		GripLevel was = Level;
		Level = level;
		_foot0 = null;
		_foot1 = null;
		if (was == GripLevel.Extreme)
		{
			// His feet are the game's to plant again: one on a wall comes away from it.
			ExtremeGrip.LetGoAll();
		}
		if (was == GripLevel.Off)
		{
			Array.Clear(_giving, 0, _giving.Length);
			_steps = _steadied = _slips = _offBalance = _slumped = _splayed = _hipHit = _limp = 0;
			_notedAt = float.MinValue;
			_unnoted = 0;
		}
		if (level == GripLevel.Extreme)
		{
			ExtremeGrip.StartCounting();
		}
		switch (level)
		{
			case GripLevel.Grippy:
				_log?.Msg((was == GripLevel.Extreme)
					? "Extreme grip off, Grippy feet still on. While it was on, it had " + ExtremeGrip.Summary() + "."
					: "Grippy feet on: Nate's planted feet can't slip, and he keeps his balance (F7 again for extreme grip).");
				break;
			case GripLevel.Extreme:
				_log?.Msg("Extreme grip on: Grippy feet, and a foot put down against anything (a wall, a cliff face, the side of a rock) holds right there, so Nate can walk up sheer walls (F7 turns it off).");
				break;
			default:
				_log?.Msg("Grippy feet off. While it was on, " + Summary() + ((was == GripLevel.Extreme) ? "; and Extreme grip had " + ExtremeGrip.Summary() : "") + ".");
				break;
		}
		return null;
	}

	/// <summary>Once a frame: the game's rules aren't running between frames' physics steps (should one have stopped half-way, it's forgotten).</summary>
	public static void Tick()
	{
		_inDeadCheck = false;
		_inHipForces = false;
		ExtremeGrip.Tick();
	}

	private static bool Install()
	{
		if (Installed || _installTried)
		{
			return Installed;
		}
		_installTried = true;
		HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("CabbySteps.GrippyFeet");
		try
		{
			Patch(harmony, typeof(PlayerMovement), nameof(PlayerMovement.GetGrounded), null, nameof(AfterGetGrounded));
			Patch(harmony, typeof(PlayerMovement), nameof(PlayerMovement.HipForces), nameof(BeforeHipForces), nameof(AfterHipForces));
			Patch(harmony, typeof(PlayerMovement), nameof(PlayerMovement.GetPolySupport), null, nameof(AfterGetPolySupport));
			Patch(harmony, typeof(PlayerMovement), nameof(PlayerMovement.DeadCheck), nameof(BeforeDeadCheck), nameof(AfterDeadCheck));
			Patch(harmony, typeof(PlayerMovement), nameof(PlayerMovement.Die), nameof(BeforeDie), null);
			Patch(harmony, typeof(BehaviourPuppet), nameof(BehaviourPuppet.SetState), nameof(BeforeSetState), null);
			Installed = true;
		}
		catch (Exception ex)
		{
			_log?.Warning("Grippy feet: can't hook the game's footing and balance, so the cheat stays off: " + ex.Message);
			try
			{
				harmony.UnpatchSelf();
			}
			catch (Exception ex2)
			{
				_log?.Warning("Grippy feet: couldn't take the hooks that were made back out: " + ex2.Message);
			}
		}
		return Installed;
	}

	private static void Patch(HarmonyLib.Harmony harmony, Type type, string method, string prefix, string postfix)
	{
		System.Reflection.MethodInfo original = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.Name, method);
		harmony.Patch(original,
			prefix: (prefix != null) ? new HarmonyMethod(AccessTools.Method(typeof(GrippyFeet), prefix)) : null,
			postfix: (postfix != null) ? new HarmonyMethod(AccessTools.Method(typeof(GrippyFeet), postfix)) : null);
	}

	// The hooks run inside the game's physics step, so none of them lets an exception out.

	/// <summary>A foot on the ground grips: traction at least <see cref="Traction"/>.</summary>
	private static void AfterGetGrounded(FootData foot, bool __result)
	{
		if (!On || !__result)
		{
			return;
		}
		try
		{
			if (!ReferenceEquals(foot, null) && foot.curTractionCoef < Traction)
			{
				foot.curTractionCoef = Traction;
			}
		}
		catch (Exception ex)
		{
			Fail("couldn't give a foot its grip", ex);
		}
	}

	/// <summary>
	/// Before the step: no planted foot starts it sliding, nor counts towards starting to; no planted
	/// leg is tired; and the timer that drains his feet while his weight is beyond them starts again.
	/// </summary>
	private static void BeforeHipForces(PlayerMovement __instance)
	{
		_inHipForces = true;
		if (!On)
		{
			return;
		}
		try
		{
			LookUpFeet(__instance);
			if (__instance.dead)
			{
				return;
			}
			_steps++;
			bool planted = Steady(_foot0);
			planted |= Steady(_foot1);
			if (planted)
			{
				__instance.magicFallOffTimer = 0f;
			}
		}
		catch (Exception ex)
		{
			Fail("couldn't keep a foot from slipping", ex);
		}
	}

	/// <summary>Counts the slides held off: a planted foot whose grip gave way this step (the game counted a step of slipping), once per go.</summary>
	private static void AfterHipForces()
	{
		_inHipForces = false;
		if (!On)
		{
			return;
		}
		try
		{
			for (int i = 0; i < 2; i++)
			{
				FootData foot = (i == 0) ? _foot0 : _foot1;
				bool giving = Planted(foot) && foot.slipTimer > 0f;
				if (giving && !_giving[i])
				{
					_slips++;
				}
				_giving[i] = giving;
			}
		}
		catch (Exception ex)
		{
			Fail("couldn't look at his feet", ex);
		}
	}

	/// <summary>Right after the game has outlined his planted feet: his weight counted just inside the outline, if it was beyond it.</summary>
	private static void AfterGetPolySupport(PlayerMovement __instance, bool includeLiftedFeet)
	{
		if (!On || includeLiftedFeet)
		{
			return;
		}
		try
		{
			if (__instance.dead || !AnyPlanted(__instance))
			{
				return;
			}
			Il2CppStructArray<Vector3> outline = __instance.polySupport;
			if (ReferenceEquals(outline, null))
			{
				return;
			}
			int n = Math.Min(__instance.polySupportLength, outline.Length);
			if (n < 2)
			{
				return;
			}
			if (_xs.Length < n)
			{
				_xs = new float[n * 2];
				_zs = new float[n * 2];
			}
			for (int i = 0; i < n; i++)
			{
				Vector3 p = outline[i];
				_xs[i] = p.x;
				_zs[i] = p.z;
			}
			Vector3 weight = __instance.centerOfMass;
			if (SupportPolygon.Steady(_xs, _zs, n, weight.x, weight.z, Margin, out float x, out float z))
			{
				__instance.centerOfMass = Fast.V3(x, weight.y, z);
				_steadied++;
			}
		}
		catch (Exception ex)
		{
			Fail("couldn't keep Nate's weight over his feet", ex);
		}
	}

	private static void BeforeDeadCheck()
	{
		_inDeadCheck = true;
	}

	private static void AfterDeadCheck()
	{
		_inDeadCheck = false;
	}

	/// <summary>
	/// A fall the game's posture rules call for, with a foot planted, doesn't happen; the timers that
	/// led to it start again. Every other fall (in the air, cacti, boulders, a save he was down in) goes ahead.
	/// </summary>
	private static bool BeforeDie(PlayerMovement __instance)
	{
		if (!On || (!_inDeadCheck && !_inHipForces))
		{
			return true;
		}
		try
		{
			if (__instance.dead || !AnyPlanted(__instance))
			{
				return true;
			}
			string why;
			if (_inHipForces)
			{
				_hipHit++;
				why = "his hip hit something hard";
			}
			else if (__instance.offBalanceDeathTimer > __instance.offBalanceDeathTime)
			{
				_offBalance++;
				why = "off balance";
			}
			else if (__instance.deathTimer > __instance.deathTime)
			{
				_slumped++;
				why = "slumped: hips sunk to his feet, a foot above his hips, or propped on his hip";
			}
			else
			{
				_splayed++;
				why = "legs splayed too wide";
			}
			__instance.deathTimer = 0f;
			__instance.offBalanceDeathTimer = 0f;
			__instance.hipHoldingYouUp = false;
			Note("kept Nate on his feet (the game had him falling: " + why + ")");
			return false;
		}
		catch (Exception ex)
		{
			Fail("couldn't keep Nate on his feet", ex);
			return true;
		}
	}

	/// <summary>Nate's ragdoll doesn't go limp by itself (a part knocked far out of place) while a foot is planted; the game making him fall still unpins it.</summary>
	private static bool BeforeSetState(BehaviourPuppet __instance, BehaviourPuppet.State newState)
	{
		if (!On || newState != BehaviourPuppet.State.Unpinned)
		{
			return true;
		}
		try
		{
			PlayerMovement pm = PlayerMovement.me;
			BehaviourPuppet puppet = ReferenceEquals(pm, null) ? null : pm.bp;
			if (ReferenceEquals(puppet, null) || puppet.Pointer != __instance.Pointer || pm.dead || !AnyPlanted(pm))
			{
				return true;
			}
			_limp++;
			Note("kept Nate's ragdoll from going limp (a part of him was knocked out of place)");
			return false;
		}
		catch (Exception ex)
		{
			Fail("couldn't keep Nate's ragdoll up", ex);
			return true;
		}
	}

	/// <summary>Look up Nate's feet for this physics step's hooks.</summary>
	private static void LookUpFeet(PlayerMovement pm)
	{
		Il2CppReferenceArray<FootData> feet = pm.feet;
		int count = ReferenceEquals(feet, null) ? 0 : feet.Length;
		_foot0 = (count > 0) ? feet[0] : null;
		_foot1 = (count > 1) ? feet[1] : null;
	}

	private static bool Planted(FootData foot)
	{
		return !ReferenceEquals(foot, null) && foot.planted;
	}

	/// <summary>A planted foot starts the step with no slide under way, none building up, and rested. False if the foot isn't planted.</summary>
	private static bool Steady(FootData foot)
	{
		if (!Planted(foot))
		{
			return false;
		}
		foot.slipTimer = 0f;
		foot.slipVel = default;
		foot.fatigue = 0f;
		return true;
	}

	/// <summary>Whether a foot of his is planted (his feet as looked up this step, or now if they haven't been yet).</summary>
	private static bool AnyPlanted(PlayerMovement pm)
	{
		if (ReferenceEquals(_foot0, null) && ReferenceEquals(_foot1, null))
		{
			LookUpFeet(pm);
		}
		return Planted(_foot0) || Planted(_foot1);
	}

	/// <summary>A line in the log for something the cheat did: at most one every 10 s, with the totals so far.</summary>
	private static void Note(string what)
	{
		float now = Frame.Now;
		if (now - _notedAt < 10f)
		{
			_unnoted++;
			return;
		}
		_log?.Msg("Grippy feet: " + what + ((_unnoted > 0) ? $", and {_unnoted} more since the last note" : "") + ". So far it has " + Summary() + ".");
		_notedAt = now;
		_unnoted = 0;
	}

	/// <summary>"kept him on his feet 3 times (off balance 2, legs splayed 1), held off 4 slides, ..." for the log.</summary>
	private static string Summary()
	{
		StringBuilder text = new StringBuilder(160);
		int falls = _offBalance + _slumped + _splayed + _hipHit;
		text.Append("kept him on his feet ").Append(falls).Append(falls == 1 ? " time" : " times");
		if (falls > 0)
		{
			text.Append(" (off balance ").Append(_offBalance).Append(", slumped ").Append(_slumped).Append(", legs splayed ").Append(_splayed)
				.Append(", hip hit ").Append(_hipHit).Append(')');
		}
		text.Append(", kept his ragdoll from going limp ").Append(_limp).Append(_limp == 1 ? " time" : " times");
		text.Append(", held off ").Append(_slips).Append(_slips == 1 ? " slide" : " slides");
		text.Append(", and counted his weight over his feet in ").Append(_steadied).Append(" of ").Append(_steps).Append(" physics steps");
		return text.ToString();
	}

	private static void Fail(string what, Exception ex)
	{
		if (_failures++ < 3)
		{
			_log?.Warning("Grippy feet: " + what + ": " + ex.Message);
		}
	}
}
