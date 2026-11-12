using System;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// The Hold tight cheat (F8): what Nate holds and the hat he wears stay with him. Falls, knocks
/// and the wind don't take them, and neither does a camp's night. Swapping still works: picking up
/// something else with a full hand, or putting on another hat, lets go of the old one as usual.
/// <para>
/// Read from the game's machine code, every place that makes him let go of a thing
/// (<c>PlayerMovement.DropHandItem</c>) or takes his hat off (<c>KnockOffHat</c>). The ones this
/// cheat stops (<see cref="LetGo"/>):
/// </para>
/// <list type="bullet">
/// <item><b>Jolts while he's down</b>: <c>DeadUpdate</c>, every physics step he's down, drops a
/// thing when his head's speed changes by more than 4 m/s from one step to the next (or the hand's
/// by its own limit), and knocks his hat off by a measure of the hat's own.</item>
/// <item><b>Hits while he's down</b>: <c>BodyPartHit</c> drops a thing when a hand or an arm hits
/// something hard.</item>
/// <item><b>The Baby Trophy</b> wriggles out of his hand as he goes down, to run off
/// (<c>RunawayTrophy.FixedUpdate</c>).</item>
/// <item><b>The wind</b> lifts the propeller hat off his head when it blows harder than the hat's
/// <c>liftOffWind</c> (<c>PropHat.FixedUpdate</c>, which marks it flying first, then has it knocked
/// off). Kept on, it's marked not flying again, so it spins on his head as in a light wind.</item>
/// <item><b>A camp's morning</b> takes his hat off and sets it down by the fire
/// (<c>Menu.MorningCampFireCo</c>, at the camp's <c>hatDropSpot</c>). Kept on, the spot is hidden
/// from that step of the morning, so the hat on his head isn't moved there.</item>
/// <item><b>A vase in his hand</b> breaks when it's knocked hard while he's down (or in the first
/// second after he picks it up): <c>BreakVase.CheckCollision</c> is skipped while it's held.</item>
/// </list>
/// <para>
/// What stays the game's: swaps (<c>GrabGrabable</c>, reaching for a thing, <c>ForceGiveGrabable</c>,
/// <c>WearHat</c>, putting a hat from his hand on his head), handing things over in a conversation
/// (<c>BBConvoHoldingItemTrigger</c>), using them up (a potion drunk, the camera's one photo), the
/// lantern going out or being moved by the story, the ice cream melting away, a vase that breaks
/// once it's out of his hands, trips (the teleport lets go and gives back; <see cref="CarryGuard"/>),
/// the ending, and the developers' keys.
/// </para>
/// <para>
/// How: the game's own letting go is already hooked for trips (<see cref="CarryGuard"/>), and those
/// hooks ask <see cref="KeepsInHand"/> and <see cref="KeepsHatOn"/>. This cheat's hooks, made the
/// first time it's switched on (a Harmony instance of its own), only note which of the game's
/// methods is running (before and after each), so the letting go can be told apart by its cause.
/// Putting on a hat counts as a swap even while he's down. If the core hooks can't be made (after a
/// game update, say), none stays and the cheat stays off; if one of the others can't, only that
/// case is left to the game.
/// </para>
/// </summary>
public static class HoldTight
{
	private static MelonLogger.Instance _log;

	private static bool _installTried;

	private static int _failures;

	// Which of the game's methods that take things from him is running now (set before it, cleared
	// after it; all cleared once a frame should one have stopped half-way).
	private static bool _jolting;

	private static bool _hitting;

	private static bool _wriggling;

	private static bool _morning;

	/// <summary>The menu whose camp morning is running (the coroutine's owner), while the cheat is on; null otherwise.</summary>
	private static Menu _morningMenu;

	/// <summary>The propeller hat whose <c>FixedUpdate</c> is running, while the cheat is on; null otherwise.</summary>
	private static PropHat _windHat;

	/// <summary>Putting on a hat (<c>WearHat</c>) is under way: it takes off the one he wears, which is a swap.</summary>
	private static int _swapping;

	// A camp's spot for the hat, hidden while that step of its morning runs, and the camp it's from.
	private static Checkpoint _spotCamp;

	private static Transform _spot;

	// Since the cheat was switched on, for the log.
	private static int _jolts;

	private static int _hits;

	private static int _wriggles;

	private static int _hatJolts;

	private static int _camps;

	private static int _winds;

	private static int _vases;

	/// <summary>A vase was kept whole during this fall (counted once a fall).</summary>
	private static bool _vaseThisFall;

	private static float _notedAt = float.MinValue;

	private static int _unnoted;

	/// <summary>The cases left to the game because their hooks couldn't be made ("the wind can still lift the propeller hat off"), or null.</summary>
	private static string _missing;

	/// <summary>The cheat is on.</summary>
	public static bool On { get; private set; }

	/// <summary>The hooks are in.</summary>
	public static bool Installed { get; private set; }

	public static void Init(MelonLogger.Instance log)
	{
		_log = log;
	}

	/// <summary>Switch the cheat on or off. Null if done; otherwise why it can't be (it stays off).</summary>
	public static string Switch(bool on)
	{
		if (on && !Install())
		{
			return "the game couldn't be hooked, so it stays off (the log says why)";
		}
		if (on == On)
		{
			return null;
		}
		On = on;
		if (on)
		{
			_jolts = _hits = _wriggles = _hatJolts = _camps = _winds = _vases = 0;
			_vaseThisFall = false;
			_notedAt = float.MinValue;
			_unnoted = 0;
			_log?.Msg("Hold tight on: what Nate holds stays in his hands and his hat stays on, whatever falls, the wind or a night at a camp would do; picking up something else or putting on another hat still swaps (F8 turns it off)."
				+ ((_missing != null) ? " Left to the game: " + _missing + "." : ""));
		}
		else
		{
			_windHat = null;
			PutSpotBack();
			_log?.Msg("Hold tight off. While it was on, it " + Summary() + ".");
		}
		return null;
	}

	/// <summary>Once a frame: none of the game's methods is running between frames' physics steps (should one have stopped half-way, it's forgotten).</summary>
	public static void Tick()
	{
		_jolting = false;
		_hitting = false;
		_wriggling = false;
		_morning = false;
		_morningMenu = null;
		_windHat = null;
		_swapping = 0;
		if (!ReferenceEquals(_spot, null))
		{
			PutSpotBack();
		}
	}

	private static bool Install()
	{
		if (Installed || _installTried)
		{
			return Installed;
		}
		_installTried = true;
		if (!CarryGuard.Installed)
		{
			_log?.Warning("Hold tight: the game's letting go of what Nate holds isn't hooked (the warning above says why), so the cheat stays off.");
			return false;
		}
		HarmonyLib.Harmony harmony = new HarmonyLib.Harmony("CabbySteps.HoldTight");
		try
		{
			Patch(harmony, typeof(PlayerMovement), nameof(PlayerMovement.DeadUpdate), nameof(BeforeDeadUpdate), nameof(AfterDeadUpdate));
			Patch(harmony, typeof(PlayerMovement), nameof(PlayerMovement.BodyPartHit), nameof(BeforeBodyPartHit), nameof(AfterBodyPartHit));
			Patch(harmony, typeof(PlayerMovement), nameof(PlayerMovement.WearHat), nameof(BeforeWearHat), nameof(AfterWearHat));
		}
		catch (Exception ex)
		{
			_log?.Warning("Hold tight: can't hook the game's falls, so the cheat stays off: " + ex.Message);
			try
			{
				harmony.UnpatchSelf();
			}
			catch (Exception ex2)
			{
				_log?.Warning("Hold tight: couldn't take the hooks that were made back out: " + ex2.Message);
			}
			return false;
		}
		Installed = true;
		// The rest each on its own: one that can't be made leaves just that case to the game.
		Extra(harmony, typeof(RunawayTrophy), nameof(RunawayTrophy.FixedUpdate), nameof(BeforeRunaway), nameof(AfterRunaway),
			"the Baby Trophy still wriggles free when he falls");
		Extra(harmony, typeof(PropHat), nameof(PropHat.FixedUpdate), nameof(BeforePropHat), nameof(AfterPropHat),
			"the wind can still lift the propeller hat off");
		Extra(harmony, typeof(Menu._MorningCampFireCo_d__205), nameof(Menu._MorningCampFireCo_d__205.MoveNext), nameof(BeforeMorning), nameof(AfterMorning),
			"a camp's morning still sets his hat down by the fire");
		Extra(harmony, typeof(BreakVase), nameof(BreakVase.CheckCollision), nameof(BeforeVaseKnock), null,
			"a vase in his hand can still break when he falls");
		return true;
	}

	private static void Extra(HarmonyLib.Harmony harmony, Type type, string method, string prefix, string postfix, string otherwise)
	{
		try
		{
			Patch(harmony, type, method, prefix, postfix);
		}
		catch (Exception ex)
		{
			_missing = (_missing == null) ? otherwise : _missing + "; " + otherwise;
			_log?.Warning("Hold tight: can't hook " + type.Name + "." + method + ", so " + otherwise + ": " + ex.Message);
		}
	}

	private static void Patch(HarmonyLib.Harmony harmony, Type type, string method, string prefix, string postfix)
	{
		System.Reflection.MethodInfo original = AccessTools.Method(type, method) ?? throw new MissingMethodException(type.Name, method);
		harmony.Patch(original,
			prefix: (prefix != null) ? new HarmonyMethod(AccessTools.Method(typeof(HoldTight), prefix)) : null,
			postfix: (postfix != null) ? new HarmonyMethod(AccessTools.Method(typeof(HoldTight), postfix)) : null);
	}

	// The hooks run inside the game (its physics step, a coroutine), so none of them lets an
	// exception out. The before/after pairs only note which of the game's methods is running.

	private static void BeforeDeadUpdate()
	{
		_jolting = true;
	}

	private static void AfterDeadUpdate()
	{
		_jolting = false;
	}

	private static void BeforeBodyPartHit()
	{
		_hitting = true;
	}

	private static void AfterBodyPartHit()
	{
		_hitting = false;
	}

	private static void BeforeWearHat()
	{
		_swapping++;
	}

	private static void AfterWearHat()
	{
		if (_swapping > 0)
		{
			_swapping--;
		}
	}

	private static void BeforeRunaway()
	{
		_wriggling = true;
	}

	private static void AfterRunaway()
	{
		_wriggling = false;
	}

	private static void BeforePropHat(PropHat __instance)
	{
		_windHat = On ? __instance : null;
	}

	private static void AfterPropHat()
	{
		_windHat = null;
	}

	private static void BeforeMorning(Menu._MorningCampFireCo_d__205 __instance)
	{
		_morning = true;
		_morningMenu = null;
		if (!On)
		{
			return;
		}
		try
		{
			_morningMenu = __instance.__4__this;
		}
		catch (Exception ex)
		{
			Fail("couldn't see which camp's morning it is", ex);
		}
	}

	private static void AfterMorning()
	{
		_morning = false;
		_morningMenu = null;
		if (!ReferenceEquals(_spot, null))
		{
			PutSpotBack();
		}
	}

	/// <summary>
	/// For the hook before <c>DropHandItem</c> (<see cref="CarryGuard"/>): true if the cheat keeps
	/// what hand <paramref name="hand"/> holds in it, so the game's letting go is skipped. Never throws.
	/// </summary>
	public static bool KeepsInHand(PlayerMovement pm, int hand)
	{
		if (!On || (!_jolting && !_hitting && !_wriggling))
		{
			return false;
		}
		try
		{
			LetGo cause = _jolting ? LetGo.Jolted : (_hitting ? LetGo.Hit : LetGo.Wriggled);
			Il2CppReferenceArray<Grabable> items = pm.handItems;
			if (ReferenceEquals(items, null) || hand < 0 || hand >= items.Length)
			{
				return false;
			}
			Grabable item = items[hand];
			if (!HoldRules.KeepInHand(On, cause, _swapping > 0, CarryGuard.OutInTheWorld(item)))
			{
				return false;
			}
			switch (cause)
			{
				case LetGo.Jolted:
					_jolts++;
					break;
				case LetGo.Hit:
					_hits++;
					break;
				default:
					_wriggles++;
					break;
			}
			if (Noting())
			{
				Note("kept " + StanceRestore.NameOf(item) + ((hand == 0) ? " in his right hand" : " in his left hand") + " (" + Why(cause, hat: false) + ")");
			}
			return true;
		}
		catch (Exception ex)
		{
			Fail("couldn't keep what Nate holds in his hand", ex);
			return false;
		}
	}

	/// <summary>
	/// For the hook before <c>KnockOffHat</c> (<see cref="CarryGuard"/>): true if the cheat keeps his
	/// hat on, so the game's taking it off is skipped. Never throws.
	/// </summary>
	public static bool KeepsHatOn(PlayerMovement pm)
	{
		if (!On || (!_jolting && !_morning && ReferenceEquals(_windHat, null)))
		{
			return false;
		}
		try
		{
			LetGo cause = _jolting ? LetGo.Jolted : (_morning ? LetGo.Camp : LetGo.Blown);
			Hat hat = pm.currentHat;
			if (!HoldRules.KeepOnHead(On, cause, _swapping > 0, CarryGuard.OutInTheWorld(hat)))
			{
				return false;
			}
			switch (cause)
			{
				case LetGo.Jolted:
					_hatJolts++;
					break;
				case LetGo.Camp:
					// The morning goes on to put the hat by the fire: there's no spot for it this time.
					HideSpot();
					_camps++;
					break;
				default:
					// The game marked it flying just before taking it off: on his head, it isn't.
					_windHat.flying = false;
					_winds++;
					break;
			}
			if (Noting())
			{
				Note("kept " + StanceRestore.NameOf(hat) + " on his head (" + Why(cause, hat: true) + ")");
			}
			return true;
		}
		catch (Exception ex)
		{
			Fail("couldn't keep Nate's hat on", ex);
			return false;
		}
	}

	/// <summary>A vase in his hand isn't broken by a knock: the game's check is skipped while it's held.</summary>
	private static bool BeforeVaseKnock(BreakVase __instance, Collision __0)
	{
		if (!On)
		{
			return true;
		}
		try
		{
			Grabable vase = __instance.grabber;
			bool held = !ReferenceEquals(vase, null) && vase.heldState != HeldState.none;
			if (!HoldRules.KeepWhole(On, held))
			{
				return true;
			}
			// For the log: a knock the game would have broken it with (it checks a held vase only
			// while he's down, or in the first second after he picks it up), once a fall.
			PlayerMovement pm = PlayerMovement.me;
			bool down = !ReferenceEquals(pm, null) && pm.dead;
			if (!down)
			{
				_vaseThisFall = false;
			}
			if ((down || vase.timeSincePickup < 1f) && !_vaseThisFall && !ReferenceEquals(__0, null))
			{
				Vector3 v = __0.relativeVelocity;
				if (v.x * v.x + v.y * v.y + v.z * v.z > __instance.breakThreshold * __instance.breakThreshold)
				{
					_vaseThisFall = down;
					_vases++;
					if (Noting())
					{
						Note("kept " + StanceRestore.NameOf(vase) + " whole in his hand (the game would have broken it: knocked hard" + (down ? " while he was down)" : " just after he picked it up)"));
					}
				}
			}
			return false;
		}
		catch (Exception ex)
		{
			Fail("couldn't keep a vase whole", ex);
			return true;
		}
	}

	/// <summary>The camp's spot for the hat is hidden from the rest of this step of its morning, so the hat stays on his head.</summary>
	private static void HideSpot()
	{
		if (!ReferenceEquals(_spot, null))
		{
			return;
		}
		Menu menu = _morningMenu ?? Menu.me;
		Checkpoint camp = ReferenceEquals(menu, null) ? null : menu.morningFire;
		if (ReferenceEquals(camp, null))
		{
			return;
		}
		Transform spot = camp.hatDropSpot;
		if (ReferenceEquals(spot, null))
		{
			return;
		}
		_spotCamp = camp;
		_spot = spot;
		camp.hatDropSpot = null;
	}

	/// <summary>The camp gets its spot for the hat back.</summary>
	private static void PutSpotBack()
	{
		Checkpoint camp = _spotCamp;
		Transform spot = _spot;
		_spotCamp = null;
		_spot = null;
		if (ReferenceEquals(camp, null) || ReferenceEquals(spot, null))
		{
			return;
		}
		try
		{
			camp.hatDropSpot = spot;
		}
		catch (Exception ex)
		{
			Fail("couldn't give the camp back its spot for the hat", ex);
		}
	}

	/// <summary>"the game would have had him let go: jolted while he was down", for the log.</summary>
	private static string Why(LetGo cause, bool hat)
	{
		switch (cause)
		{
			case LetGo.Jolted:
				return hat ? "the game would have knocked it off: jolted while he was down" : "the game would have had him let go: jolted while he was down";
			case LetGo.Hit:
				return "the game would have had him let go: his hand or arm hit something hard while he was down";
			case LetGo.Wriggled:
				return "it would have wriggled free as he went down, to run off";
			case LetGo.Camp:
				return "the camp's morning would have set it down by the fire";
			case LetGo.Blown:
				return "the wind would have lifted it off";
			default:
				return "?";
		}
	}

	/// <summary>Whether a note goes in the log now: at most one every 10 s (the rest are counted, and said with the next).</summary>
	private static bool Noting()
	{
		if (Frame.Now - _notedAt < 10f)
		{
			_unnoted++;
			return false;
		}
		return true;
	}

	/// <summary>A line in the log for something the cheat did, with the totals so far.</summary>
	private static void Note(string what)
	{
		_log?.Msg("Hold tight: " + what + ((_unnoted > 0) ? $", and {_unnoted} more since the last note" : "") + ". So far it has " + Summary() + ".");
		_notedAt = Frame.Now;
		_unnoted = 0;
	}

	/// <summary>
	/// "stopped the game taking what he holds 37 times (jolted 30, hit 7, the Baby Trophy 0), ..." for
	/// the log. The game tries again every physics step the cause lasts (each step he's down and
	/// jolted, each step the wind blows), so these are its tries, not separate falls.
	/// </summary>
	private static string Summary()
	{
		StringBuilder text = new StringBuilder(220);
		int hands = _jolts + _hits + _wriggles;
		text.Append("stopped the game taking what he holds ").Append(hands).Append((hands == 1) ? " time" : " times");
		if (hands > 0)
		{
			text.Append(" (jolted ").Append(_jolts).Append(", hit ").Append(_hits).Append(", the Baby Trophy ").Append(_wriggles).Append(')');
		}
		int hats = _hatJolts + _camps + _winds;
		text.Append(", his hat ").Append(hats).Append((hats == 1) ? " time" : " times");
		if (hats > 0)
		{
			text.Append(" (jolted ").Append(_hatJolts).Append(", camp ").Append(_camps).Append(", wind ").Append(_winds).Append(')');
		}
		text.Append(", and breaking a vase in his hand ").Append(_vases).Append((_vases == 1) ? " time" : " times");
		return text.ToString();
	}

	private static void Fail(string what, Exception ex)
	{
		if (_failures++ < 3)
		{
			_log?.Warning("Hold tight: " + what + ": " + ex.Message);
		}
	}
}
