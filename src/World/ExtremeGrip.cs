using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Grippy feet's second level, Extreme grip (F7 again): a foot put down against anything holds
/// there, so Nate can walk up a sheer wall one foothold at a time. Everything Grippy feet does
/// still applies. Read from the game's machine code (<c>PlayerMovement</c>):
/// <para>
/// <b>What counts as planted.</b> Every physics step <c>GroundedCheck</c> asks <c>GetGrounded</c>
/// about each foot. That casts the foot's box (<c>FootData.coll</c>, at its target) straight down
/// (<c>TargetDistFromGround</c>) and needs ground within 3.5 cm (7.5 cm once planted), then casts
/// down from points across the sole and needs a surface facing up (normal.y above 0.2) under at
/// least one. A wall, a cliff face or a slope steeper than about 78° never passes, so a foot put
/// down against one slides down it to whatever is below.
/// </para>
/// <para>
/// <b>Taking hold.</b> When the game finds no ground under a foot that's being put down (its
/// trigger let go: <c>TriggerMove</c> drops a foot whose trigger is at most 0.05), the foot's box,
/// 1 cm bigger all round, is checked against everything the game's feet collide with
/// (<c>LayerCache.PropTerrainMask</c>, not triggers, not what he's holding). If it touches
/// something, <c>GetGrounded</c>'s answer is made yes: the foot counts as standing on flat ground
/// right where it is (its <c>plantNormal</c> straight up, a firm grip). <c>GroundedCheck</c> then
/// plants it, snapping it down onto the ground below and looking for its pivot from there; right
/// after (a postfix) the foot is put back where it took hold, its pivot (<c>pivotPt</c>, which sets
/// how high his hips stand) at the bottom of the foot, standing on nothing that moves. While it's
/// planted that way, the game's cactus test under the snapped spot (<c>GetPivotPt</c> calling
/// <c>CactusDie</c>) is skipped: whatever is far below can't hurt him. From then on he stands on it
/// as on a ledge: <c>GetDesHipHeight</c> raises his hips over it and <c>HipForces</c> pushes him up
/// along the leg, so lifting the other foot steps him up.
/// </para>
/// <para>
/// <b>Holding on.</b> While it holds, the foot stays planted whatever is under it, and isn't rolled
/// heel to toe (<c>PlantedFootRotation</c>, which keeps a rolling foot on the ground under it, is
/// skipped for it). It lets go when the game lifts it (<c>isLifting</c>: its trigger, once his
/// weight is off it), when the game finds real ground under it (an ordinary step from then on),
/// when he goes down or into a cutscene, and when the game stands him up anywhere (a fall, a trip:
/// <c>OnStandUp</c> asks <c>GetGrounded</c> too).
/// </para>
/// <para>
/// Only methods taking objects and plain numbers are hooked: Il2CppInterop's native-to-managed
/// trampoline (1.5.1) mishandles a struct passed by reference (it reads it as an object pointer),
/// so <c>TargetDistFromGround(foot, out RaycastHit)</c> is left alone. The hooks go in the first
/// time Extreme grip is switched on, on a Harmony instance of their own: if any can't be made, none
/// stays and the cheat doesn't go to Extreme grip.
/// </para>
/// </summary>
public static class ExtremeGrip
{
	/// <summary>How near something a put-down foot's box must come to take hold of it, in metres.</summary>
	public const float Reach = 0.01f;

	private static MelonLogger.Instance _log;

	private static bool _installTried;

	private static int _failures;

	/// <summary>The game is in its grounded check (<c>GroundedCheck</c>) for a foot.</summary>
	private static bool _stepping;

	private struct Hold
	{
		/// <summary>The foot holds on where it is.</summary>
		public bool On;

		/// <summary>Taken this step: <c>GroundedCheck</c> is planting it.</summary>
		public bool Fresh;

		/// <summary>Where the foot's target was when it took hold.</summary>
		public Vector3 At;

		/// <summary>The bottom of the foot when it took hold: its pivot.</summary>
		public Vector3 Pivot;
	}

	/// <summary>Each foot's hold (0 right, 1 left).</summary>
	private static readonly Hold[] _holds = new Hold[2];

	// Nate's two feet as last looked up (the game's objects), to tell which foot the game asks about.
	private static IntPtr _foot0;

	private static IntPtr _foot1;

	private static Il2CppReferenceArray<Collider> _hits;

	private static int _mask;

	// Since Extreme grip was switched on, for the log.
	private static int _taken;

	private static int _heldOff;

	private static int _cactiSkipped;

	/// <summary>Holds let go, by why (<see cref="FootGrip.Why"/>).</summary>
	private static readonly int[] _letGo = new int[8];

	private static float _highest;

	private static float _notedAt = float.MinValue;

	private static int _unnoted;

	/// <summary>The hooks are in.</summary>
	public static bool Installed { get; private set; }

	/// <summary>Extreme grip is on and hooked in.</summary>
	private static bool Active => Installed && GrippyFeet.Extreme;

	/// <summary>A foot that has just taken hold is being planted (between its <c>GetGrounded</c> and the end of its <c>GroundedCheck</c>).</summary>
	private static bool Planting => _holds[0].Fresh || _holds[1].Fresh;

	public static void Init(MelonLogger.Instance log)
	{
		_log = log;
	}

	/// <summary>Hook the game, the first time it's asked. False if it can't be (the log says why).</summary>
	public static bool Install()
	{
		if (Installed || _installTried)
		{
			return Installed;
		}
		_installTried = true;
		HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("CabbySteps.ExtremeGrip");
		try
		{
			Patch(harmony, nameof(PlayerMovement.GroundedCheck), nameof(BeforeGroundedCheck), nameof(AfterGroundedCheck));
			Patch(harmony, nameof(PlayerMovement.GetGrounded), null, nameof(AfterGetGrounded));
			Patch(harmony, nameof(PlayerMovement.PlantedFootRotation), nameof(BeforePlantedFootRotation), null);
			Patch(harmony, nameof(PlayerMovement.CactusDie), nameof(BeforeCactusDie), null);
			Installed = true;
		}
		catch (Exception ex)
		{
			_log?.Warning("Extreme grip: can't hook the game's footing, so it can't be switched on: " + ex.Message);
			try
			{
				harmony.UnpatchSelf();
			}
			catch (Exception ex2)
			{
				_log?.Warning("Extreme grip: couldn't take the hooks that were made back out: " + ex2.Message);
			}
		}
		return Installed;
	}

	private static void Patch(HarmonyLib.Harmony harmony, string method, string prefix, string postfix)
	{
		System.Reflection.MethodInfo original = AccessTools.Method(typeof(PlayerMovement), method) ?? throw new MissingMethodException(nameof(PlayerMovement), method);
		harmony.Patch(original,
			prefix: (prefix != null) ? new HarmonyMethod(AccessTools.Method(typeof(ExtremeGrip), prefix)) : null,
			postfix: (postfix != null) ? new HarmonyMethod(AccessTools.Method(typeof(ExtremeGrip), postfix)) : null);
	}

	/// <summary>Forget every hold (Extreme grip switched off: the game decides his footing again, and a foot on a wall comes away).</summary>
	public static void LetGoAll()
	{
		_holds[0] = default;
		_holds[1] = default;
	}

	/// <summary>Start the log's count again (Extreme grip switched on).</summary>
	public static void StartCounting()
	{
		_taken = 0;
		_heldOff = 0;
		_cactiSkipped = 0;
		Array.Clear(_letGo, 0, _letGo.Length);
		_highest = 0f;
		_notedAt = float.MinValue;
		_unnoted = 0;
	}

	/// <summary>
	/// Once a frame: the game's checks aren't running between frames' physics steps (should one have
	/// stopped half-way, it's forgotten; a hold it was planting is kept only if the foot did get planted).
	/// </summary>
	public static void Tick()
	{
		_stepping = false;
		_holds[0].Fresh = false;
		_holds[1].Fresh = false;
	}

	// The hooks run inside the game's physics step, so none of them lets an exception out.

	private static void BeforeGroundedCheck()
	{
		_stepping = true;
	}

	/// <summary>
	/// After the game has planted a foot that has just taken hold: put back where it took hold (the
	/// game snapped it down onto the ground below), its pivot at the bottom of the foot, standing on
	/// nothing that moves. If the game held off planting it this step (a foot can wait a step while
	/// his weight settles), the hold is let go, to be taken again next step.
	/// </summary>
	private static void AfterGroundedCheck(PlayerMovement __instance, int i)
	{
		_stepping = false;
		if (i < 0 || i > 1 || !_holds[i].Fresh)
		{
			return;
		}
		_holds[i].Fresh = false;
		try
		{
			Il2CppReferenceArray<FootData> feet = __instance.feet;
			FootData foot = (!ReferenceEquals(feet, null) && i < feet.Length) ? feet[i] : null;
			if (ReferenceEquals(foot, null) || !foot.planted)
			{
				_holds[i].On = false;
				_heldOff++;
				return;
			}
			foot.targetPos = _holds[i].At;
			foot.pivotPt = _holds[i].Pivot;
			foot.originalPlantSpot = _holds[i].Pivot;
			foot.steppedOnRb = null;
		}
		catch (Exception ex)
		{
			_holds[i] = default;
			Fail("couldn't plant a foot where it took hold", ex);
		}
	}

	/// <summary>The game has said whether a foot is on the ground: a foot put down against something takes hold of it, and a foot that holds stays planted.</summary>
	private static void AfterGetGrounded(PlayerMovement __instance, FootData foot, ref bool __result)
	{
		if (!Active || ReferenceEquals(__instance, null) || ReferenceEquals(foot, null))
		{
			return;
		}
		try
		{
			int i = IndexOf(__instance, foot);
			if (i < 0)
			{
				return;
			}
			bool down = __instance.dead || __instance.inCutscene;
			switch (FootGrip.Next(_stepping, down, __result, _holds[i].On, foot.planted, foot.isLifting, out FootGrip.Why why))
			{
				case FootGrip.Move.Hold:
					Steady(foot);
					__result = true;
					break;
				case FootGrip.Move.LetGo:
					_holds[i] = default;
					if ((int)why < _letGo.Length)
					{
						_letGo[(int)why]++;
					}
					break;
				case FootGrip.Move.Consider:
					if (FootGrip.PutDown(Trigger(__instance, i)) && Touching(__instance, foot, out Collider what))
					{
						Take(i, foot, what);
						Steady(foot);
						__result = true;
					}
					break;
			}
		}
		catch (Exception ex)
		{
			Fail("couldn't work out a foot's hold", ex);
		}
	}

	/// <summary>A foot that holds isn't rolled heel to toe (the game keeps a rolling foot on the ground under it).</summary>
	private static bool BeforePlantedFootRotation(int i)
	{
		return !(Active && i >= 0 && i < 2 && _holds[i].On);
	}

	/// <summary>While a foot that has just taken hold is planted, the game's test for a cactus under it (from far below, where it snapped the foot to) doesn't count.</summary>
	private static bool BeforeCactusDie()
	{
		if (!Planting)
		{
			return true;
		}
		_cactiSkipped++;
		return false;
	}

	/// <summary>A foot that holds stands as on flat ground, with a firm grip (the game's answer of no ground set its traction to nothing).</summary>
	private static void Steady(FootData foot)
	{
		foot.plantNormal = Fast.V3(0f, 1f, 0f);
		if (foot.curTractionCoef < GrippyFeet.Traction)
		{
			foot.curTractionCoef = GrippyFeet.Traction;
		}
	}

	/// <summary>Which of Nate's feet this is (0 right, 1 left), or -1.</summary>
	private static int IndexOf(PlayerMovement pm, FootData foot)
	{
		IntPtr p = foot.Pointer;
		if (p != IntPtr.Zero)
		{
			if (p == _foot0)
			{
				return 0;
			}
			if (p == _foot1)
			{
				return 1;
			}
		}
		// Not seen before: a new Nate (a scene loaded), so his old feet's holds go.
		Il2CppReferenceArray<FootData> feet = pm.feet;
		int count = ReferenceEquals(feet, null) ? 0 : feet.Length;
		FootData f0 = (count > 0) ? feet[0] : null;
		FootData f1 = (count > 1) ? feet[1] : null;
		IntPtr p0 = ReferenceEquals(f0, null) ? IntPtr.Zero : f0.Pointer;
		IntPtr p1 = ReferenceEquals(f1, null) ? IntPtr.Zero : f1.Pointer;
		if (p0 != _foot0 || p1 != _foot1)
		{
			_foot0 = p0;
			_foot1 = p1;
			LetGoAll();
		}
		return (p == _foot0) ? 0 : ((p == _foot1) ? 1 : -1);
	}

	/// <summary>How far foot <paramref name="i"/>'s trigger is pulled (0 to 1).</summary>
	private static float Trigger(PlayerMovement pm, int i)
	{
		Il2CppStructArray<float> trigs = pm.trigs;
		return (!ReferenceEquals(trigs, null) && i < trigs.Length) ? trigs[i] : 0f;
	}

	/// <summary>
	/// Whether the foot's box at its target, <see cref="Reach"/> bigger all round, touches anything
	/// the game's feet collide with (not triggers, not the foot itself, not what he's holding).
	/// </summary>
	private static bool Touching(PlayerMovement pm, FootData foot, out Collider what)
	{
		what = null;
		Transform target = foot.targetTrans;
		BoxCollider box = foot.coll;
		if (ReferenceEquals(target, null) || ReferenceEquals(box, null))
		{
			return false;
		}
		if (_mask == 0)
		{
			_mask = LayerCache.PropTerrainMask.m_Mask;
			if (_mask == 0)
			{
				// The game's layer numbers (ConstGenConstants._LAYERS): Default, Terrain, Props, PropsNoCamera.
				_mask = 1 | (1 << 8) | (1 << 16) | (1 << 23);
			}
		}
		if (ReferenceEquals(_hits, null))
		{
			_hits = new Il2CppReferenceArray<Collider>(8);
		}
		Vector3 centre = target.TransformPoint(box.center);
		Vector3 size = box.size;
		Vector3 half = Fast.V3(size.x * 0.5f + Reach, size.y * 0.5f + Reach, size.z * 0.5f + Reach);
		int n = Physics.OverlapBoxNonAlloc(centre, half, _hits, target.rotation, _mask, QueryTriggerInteraction.Ignore);
		n = Math.Min(n, _hits.Length);
		IntPtr own = box.Pointer;
		for (int k = 0; k < n; k++)
		{
			Collider c = _hits[k];
			if (ReferenceEquals(c, null) || c.Pointer == own || pm.IsHeldItemColl(c))
			{
				continue;
			}
			what = c;
			return true;
		}
		return false;
	}

	/// <summary>The foot takes hold where it is, its pivot at the bottom of its box.</summary>
	private static void Take(int i, FootData foot, Collider what)
	{
		Transform target = foot.targetTrans;
		BoxCollider box = foot.coll;
		Vector3 centre = target.TransformPoint(box.center);
		Vector3 pivot = Fast.V3(centre.x, centre.y - box.size.y * 0.5f, centre.z);
		_holds[i].On = true;
		_holds[i].Fresh = true;
		_holds[i].At = foot.targetPos;
		_holds[i].Pivot = pivot;
		foot.pivotPt = pivot;
		_taken++;
		float drop = Drop(pivot);
		if (drop > _highest)
		{
			_highest = drop;
		}
		float now = Frame.Now;
		if (now - _notedAt < 10f)
		{
			_unnoted++;
			return;
		}
		string name = "something";
		try
		{
			name = what.name;
		}
		catch (Exception)
		{
		}
		_log?.Msg($"Extreme grip: {FootGrip.FootName(i)} took hold of {name} at ({pivot.x:0.0}, {pivot.y:0.0}, {pivot.z:0.0}), "
			+ (float.IsPositiveInfinity(drop) ? "with no ground under it within 100 m" : $"{drop:0.0} m above the ground under it")
			+ ((_unnoted > 0) ? $", and {_unnoted} more since the last note" : "") + ". So far it has " + Summary() + ".");
		_notedAt = now;
		_unnoted = 0;
	}

	/// <summary>How far below a spot the ground is (what the game's feet stand on), for the log; infinity if there's none within 100 m.</summary>
	private static float Drop(Vector3 at)
	{
		Vector3 from = Fast.V3(at.x, at.y - 0.02f, at.z);
		if (Physics.Raycast(from, Fast.V3(0f, -1f, 0f), out RaycastHit hit, 100f, _mask, QueryTriggerInteraction.Ignore))
		{
			return hit.m_Distance + 0.02f;
		}
		return float.PositiveInfinity;
	}

	/// <summary>"taken 12 footholds (the highest 7.4 m above the ground), let go 11 times (lifted 10, ...)" for the log.</summary>
	public static string Summary()
	{
		StringBuilder text = new StringBuilder(160);
		text.Append("taken ").Append(_taken).Append(_taken == 1 ? " foothold" : " footholds");
		if (_taken > 0)
		{
			text.Append(" (the highest ").Append(float.IsPositiveInfinity(_highest) ? "with no ground within 100 m under it" : (_highest.ToString("0.0") + " m above the ground")).Append(')');
		}
		int letGo = 0;
		foreach (int n in _letGo)
		{
			letGo += n;
		}
		if (letGo > 0)
		{
			text.Append(", let go ").Append(letGo).Append(letGo == 1 ? " time (" : " times (");
			bool first = true;
			for (int why = 0; why < _letGo.Length; why++)
			{
				if (_letGo[why] > 0)
				{
					text.Append(first ? "" : ", ").Append(FootGrip.Describe((FootGrip.Why)why)).Append(' ').Append(_letGo[why]);
					first = false;
				}
			}
			text.Append(')');
		}
		if (_heldOff > 0)
		{
			text.Append(", ").Append(_heldOff).Append(" held off a step by the game");
		}
		if (_cactiSkipped > 0)
		{
			text.Append(", ").Append(_cactiSkipped).Append(_cactiSkipped == 1 ? " cactus far below ignored" : " cacti far below ignored");
		}
		return text.ToString();
	}

	private static void Fail(string what, Exception ex)
	{
		if (_failures++ < 3)
		{
			_log?.Warning("Extreme grip: " + what + ": " + ex.Message);
		}
	}
}
