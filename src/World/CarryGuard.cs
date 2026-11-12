using System;
using System.Runtime.CompilerServices;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Keeps what Nate holds and wears with him through a trip.
/// <para>
/// The game's teleport takes along what is in his hands and on his head at the moment it switches
/// him off, and nothing else (<c>TeleportCo</c> notes <c>handItems</c> and <c>currentHat</c>, lets
/// go of them, and gives them back with <c>GrabGrabable</c> and <c>WearHat</c> once he's there).
/// While Nate is down, though, the game has him let go of what he holds, and his hat come off,
/// whenever he's jolted: <c>PlayerMovement.DeadUpdate</c> drops an item when his head's speed
/// changes by more than 4 m/s from one physics step to the next (or the hand's by more than its own
/// limit) and knocks the hat off by a measure of the hat's own, and <c>BodyPartHit</c> drops an item
/// when a part of him hits something hard. A trip holds Nate still where he is until the spot is
/// ready (<see cref="Recall"/>), stopping every part of him each physics step, and to the game that
/// is one jolt after another. A trip taken while he was down, or a long one during which he went
/// down (the ground under him can unload while the far spot loads), left what he held where he set
/// off: a Chest Key, twice (log 2026-11-10 14:05 and 14:06, 935 m trips), fell through the unloaded
/// ground there and the teleport had nothing in his hands to take.
/// </para>
/// <para>
/// So while a trip holds him (<see cref="Guard"/> until <see cref="Release"/>), two Harmony hooks,
/// before <c>DropHandItem</c> and before <c>KnockOffHat</c>, keep the game from having him let go
/// while he's down with his body on. The teleport's own letting go happens with his body switched
/// off, so it isn't stopped, and neither is letting go of a thing that has gone from the world (an
/// ice cream that melts away is switched off before it's dropped). Anything kept in his hand that
/// goes from the world afterwards (a vase that breaks) is let go of then (<see cref="Tend"/>), so
/// the teleport doesn't take it along.
/// </para>
/// <para>
/// And whatever he set off with that isn't on him as he's stood up at the spot, but is still out in
/// the world, is given back to him the game's own way (<see cref="BringBack"/>, from the stand-up
/// hook, before what he holds and wears is put in place; and once more on arriving): in the hand it
/// was in (<c>GrabGrabable</c>), the hat on his head (<c>WearHat</c>). The game doesn't unload items
/// while it teleports (<c>ObjectLooper.LoopList</c> leaves them while <c>Menu.teleporting</c>), so
/// a thing dropped on the way is still there to give back. If the hooks can't be made (after a game
/// update, say), trips work as before.
/// </para>
/// <para>
/// The same two hooks serve the Hold tight cheat (<see cref="HoldTight"/>): when a trip isn't
/// keeping a thing on him, they ask the cheat, which keeps it if a fall, the wind or a camp's night
/// is what's taking it.
/// </para>
/// </summary>
public static class CarryGuard
{
	/// <summary>A trip is holding Nate: what he holds and wears stays on him while he's down.</summary>
	private static bool _guarding;

	// What he set off with: the item in each hand (0 right, 1 left) and his hat; null for none.
	private static Grabable _had0;

	private static Grabable _had1;

	private static Hat _hadHat;

	private static int _failures;

	private static MelonLogger.Instance _log;

	/// <summary>The hooks are in.</summary>
	public static bool Installed { get; private set; }

	/// <summary>Since the last <see cref="Guard"/>: how often the game would have had him drop what he holds while he was down.</summary>
	public static int DropsKept { get; private set; }

	/// <summary>Since the last <see cref="Guard"/>: how often the game would have knocked his hat off while he was down.</summary>
	public static int HatKept { get; private set; }

	/// <summary>Since the last <see cref="Guard"/>: things kept in his hand that went from the world meanwhile, and were let go of.</summary>
	public static int Gone { get; private set; }

	/// <summary>Since the last <see cref="Guard"/>: what was given back to him ("TreasureKey in his right hand"), for the log; null if nothing.</summary>
	public static string Brought { get; private set; }

	public static void Install(HarmonyLib.Harmony harmony, MelonLogger.Instance log)
	{
		_log = log;
		try
		{
			harmony.Patch(AccessTools.Method(typeof(PlayerMovement), nameof(PlayerMovement.DropHandItem)),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(CarryGuard), nameof(BeforeDropHandItem))));
			harmony.Patch(AccessTools.Method(typeof(PlayerMovement), nameof(PlayerMovement.KnockOffHat)),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(CarryGuard), nameof(BeforeKnockOffHat))));
			Installed = true;
		}
		catch (Exception ex)
		{
			log.Warning("Saved locations: can't hook the game's letting go of what Nate holds, so a trip during which he's down may leave it behind: " + ex.Message);
		}
	}

	/// <summary>
	/// A trip sets off: until <see cref="Release"/>, what he holds and wears stays on him while he's
	/// down. <paramref name="again"/>: the same trip's second go (after he was thrown arriving), which
	/// gives back what he set off with the first time.
	/// </summary>
	public static void Guard(bool again)
	{
		_guarding = true;
		DropsKept = 0;
		HatKept = 0;
		Gone = 0;
		Brought = null;
		if (again)
		{
			return;
		}
		_had0 = null;
		_had1 = null;
		_hadHat = null;
		try
		{
			Note();
		}
		catch (Exception ex)
		{
			Fail("couldn't see what Nate holds and wears", ex);
		}
	}

	/// <summary>The teleport has switched him off (or the trip is over): letting go is the game's again.</summary>
	public static void Release()
	{
		_guarding = false;
	}

	/// <summary>The trip is over: what he set off with is forgotten.</summary>
	public static void Forget()
	{
		_guarding = false;
		_had0 = null;
		_had1 = null;
		_hadHat = null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Note()
	{
		PlayerMovement pm = PlayerMovement.me;
		if (pm == null)
		{
			return;
		}
		Il2CppReferenceArray<Grabable> items = pm.handItems;
		_had0 = InHand(items, 0);
		_had1 = InHand(items, 1);
		Hat hat = pm.currentHat;
		_hadHat = OutInTheWorld(hat) ? hat : null;
	}

	/// <summary>The thing in hand <paramref name="i"/>, if it's out in the world.</summary>
	private static Grabable InHand(Il2CppReferenceArray<Grabable> items, int i)
	{
		if (ReferenceEquals(items, null) || i >= items.Length)
		{
			return null;
		}
		Grabable item = items[i];
		return OutInTheWorld(item) ? item : null;
	}

	/// <summary>Not destroyed, switched on, with a body: a thing that can be held or worn.</summary>
	internal static bool OutInTheWorld(Grabable thing)
	{
		if (thing == null || !thing.gameObject.activeInHierarchy)
		{
			return false;
		}
		Il2CppReferenceArray<Rigidbody> bodies = thing.rbs;
		return !ReferenceEquals(bodies, null) && bodies.Length > 0 && bodies[0] != null;
	}

	/// <summary>A thing's id for <see cref="CarryPlan"/>: its address, 0 for none (or destroyed).</summary>
	private static long Id(Grabable thing)
	{
		return (thing == null) ? 0L : thing.Pointer.ToInt64();
	}

	/// <summary>
	/// Whatever has him let go now is the game's doing while he's down: he's down, with his body on
	/// (the teleport lets go with it switched off).
	/// </summary>
	private static bool Keeping(PlayerMovement pm)
	{
		return pm != null && pm.dead && pm.gameObject.activeSelf;
	}

	// The hooks run inside the game, so neither lets an exception out; false skips the game's call.
	// A trip keeps what it keeps first (its counts drive its own letting go of things gone from the
	// world); whatever it lets through, the Hold tight cheat may still keep.

	private static bool BeforeDropHandItem(PlayerMovement __instance, int __0)
	{
		if (_guarding && TripKeepsInHand(__instance, __0))
		{
			return false;
		}
		return !HoldTight.KeepsInHand(__instance, __0);
	}

	private static bool BeforeKnockOffHat(PlayerMovement __instance)
	{
		if (_guarding && TripKeepsHatOn(__instance))
		{
			return false;
		}
		return !HoldTight.KeepsHatOn(__instance);
	}

	/// <summary>While a trip holds him: whether what hand <paramref name="hand"/> holds stays in it (he's down, with his body on, and it's out in the world).</summary>
	private static bool TripKeepsInHand(PlayerMovement pm, int hand)
	{
		try
		{
			if (!Keeping(pm))
			{
				return false;
			}
			Il2CppReferenceArray<Grabable> items = pm.handItems;
			if (ReferenceEquals(items, null) || hand < 0 || hand >= items.Length || !OutInTheWorld(items[hand]))
			{
				return false;
			}
			DropsKept++;
			return true;
		}
		catch (Exception ex)
		{
			Fail("couldn't keep what Nate holds in his hand", ex);
			return false;
		}
	}

	/// <summary>While a trip holds him: whether his hat stays on (he's down, with his body on, and it's out in the world).</summary>
	private static bool TripKeepsHatOn(PlayerMovement pm)
	{
		try
		{
			if (!Keeping(pm) || !OutInTheWorld(pm.currentHat))
			{
				return false;
			}
			HatKept++;
			return true;
		}
		catch (Exception ex)
		{
			Fail("couldn't keep Nate's hat on", ex);
			return false;
		}
	}

	/// <summary>
	/// Every frame while the trip holds him: a thing kept in his hand that has since gone from the
	/// world (switched off: a vase that broke) is let go of, so the teleport doesn't take it along.
	/// </summary>
	public static void Tend()
	{
		if (!_guarding || DropsKept == 0)
		{
			return;
		}
		try
		{
			LetGoOfGone();
		}
		catch (Exception ex)
		{
			Fail("couldn't let go of something gone from Nate's hand", ex);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void LetGoOfGone()
	{
		PlayerMovement pm = PlayerMovement.me;
		if (pm == null)
		{
			return;
		}
		Il2CppReferenceArray<Grabable> items = pm.handItems;
		for (int i = 0; i < 2 && !ReferenceEquals(items, null) && i < items.Length; i++)
		{
			Grabable item = items[i];
			if (item != null && !item.gameObject.activeInHierarchy)
			{
				// The guard lets a switched-off thing go.
				pm.DropHandItem(i);
				Gone++;
			}
		}
	}

	/// <summary>
	/// Give back to Nate, the game's own way, whatever he set off with that isn't on him now but is
	/// still out in the world: each item in the hand it was in (<c>GrabGrabable</c>: put in his grip and
	/// held), the hat on his head (<c>WearHat</c>). For the stand-up hook (once the game is out of its
	/// cutscene state) and for arriving; doing it again does nothing.
	/// </summary>
	public static void BringBack(PlayerMovement pm)
	{
		if (pm == null || (ReferenceEquals(_had0, null) && ReferenceEquals(_had1, null) && ReferenceEquals(_hadHat, null)))
		{
			return;
		}
		StringBuilder text = null;
		Il2CppReferenceArray<Grabable> items = pm.handItems;
		if (!ReferenceEquals(items, null) && items.Length >= 2)
		{
			long had0 = Id(_had0);
			long had1 = Id(_had1);
			long has0 = Id(items[0]);
			long has1 = Id(items[1]);
			for (int i = 0; i < 2; i++)
			{
				Grabable had = (i == 0) ? _had0 : _had1;
				if (ReferenceEquals(had, null) || !CarryPlan.Refill(i, had0, had1, has0, has1, OutInTheWorld(had)))
				{
					continue;
				}
				pm.GrabGrabable(had, i);
				if (i == 0)
				{
					has0 = had0;
				}
				else
				{
					has1 = had1;
				}
				text = Add(text, StanceRestore.NameOf(had) + ((i == 0) ? " in his right hand" : " in his left hand"));
			}
		}
		if (!ReferenceEquals(_hadHat, null) && CarryPlan.Rewear(Id(_hadHat), Id(pm.currentHat), OutInTheWorld(_hadHat)))
		{
			pm.WearHat(_hadHat);
			text = Add(text, StanceRestore.NameOf(_hadHat) + " on his head");
		}
		if (text != null)
		{
			Brought = (Brought == null) ? text.ToString() : Brought + ", " + text;
		}
	}

	/// <summary>
	/// On arriving: anything he set off with that still isn't on him is given back if it can be
	/// (<see cref="BringBack"/>); then what's still missing, for the log ("TreasureKey (gone from the
	/// world)"), or null.
	/// </summary>
	public static string Arrived()
	{
		if (ReferenceEquals(_had0, null) && ReferenceEquals(_had1, null) && ReferenceEquals(_hadHat, null))
		{
			return null;
		}
		PlayerMovement pm = PlayerMovement.me;
		if (pm == null)
		{
			return null;
		}
		BringBack(pm);
		StringBuilder text = null;
		Il2CppReferenceArray<Grabable> items = pm.handItems;
		long has0 = (ReferenceEquals(items, null) || items.Length < 1) ? 0L : Id(items[0]);
		long has1 = (ReferenceEquals(items, null) || items.Length < 2) ? 0L : Id(items[1]);
		for (int i = 0; i < 2; i++)
		{
			Grabable had = (i == 0) ? _had0 : _had1;
			// Held in both hands: one thing, said once.
			if (ReferenceEquals(had, null) || (i == 1 && !ReferenceEquals(_had0, null) && had.Pointer == _had0.Pointer))
			{
				continue;
			}
			long id = Id(had);
			if (id != 0 && (id == has0 || id == has1))
			{
				continue;
			}
			text = Add(text, Whereabouts(had, pm));
		}
		if (!ReferenceEquals(_hadHat, null))
		{
			long id = Id(_hadHat);
			if (id == 0 || id != Id(pm.currentHat))
			{
				text = Add(text, Whereabouts(_hadHat, pm));
			}
		}
		return text?.ToString();
	}

	/// <summary>"TreasureKey (gone from the world)", "StetsonHat (3.2 m away)".</summary>
	private static string Whereabouts(Grabable thing, PlayerMovement pm)
	{
		if (thing == null)
		{
			return "something (gone from the world)";
		}
		string name = StanceRestore.NameOf(thing);
		if (!thing.gameObject.activeInHierarchy)
		{
			return name + " (switched off: gone from the world)";
		}
		Transform hip = pm.hipPivot;
		if (hip == null)
		{
			return name;
		}
		Vector3 a = thing.transform.position;
		Vector3 b = hip.position;
		float dx = a.x - b.x;
		float dy = a.y - b.y;
		float dz = a.z - b.z;
		return $"{name} ({Fast.Sqrt(dx * dx + dy * dy + dz * dz):0.0} m away)";
	}

	private static StringBuilder Add(StringBuilder text, string what)
	{
		if (text == null)
		{
			return new StringBuilder(what);
		}
		return text.Append(", ").Append(what);
	}

	/// <summary>", kept what he holds in his hands (the game would have dropped it 37x while he was down)...", or "": for the log.</summary>
	public static string Kept()
	{
		if (DropsKept == 0 && HatKept == 0 && Gone == 0)
		{
			return "";
		}
		StringBuilder text = new StringBuilder(160);
		if (DropsKept > 0)
		{
			text.Append(", kept what he holds in his hands (the game would have dropped it ").Append(DropsKept).Append("x while he was down)");
		}
		if (HatKept > 0)
		{
			text.Append(", kept his hat on (the game would have knocked it off ").Append(HatKept).Append("x)");
		}
		if (Gone > 0)
		{
			text.Append(", let go of ").Append(Gone).Append((Gone == 1) ? " thing" : " things").Append(" gone from the world");
		}
		return text.ToString();
	}

	private static void Fail(string what, Exception ex)
	{
		if (_failures++ < 3)
		{
			_log?.Warning("Saved locations: " + what + ": " + ex.Message);
		}
	}
}
