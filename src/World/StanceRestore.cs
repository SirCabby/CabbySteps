using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRootMotion.Dynamics;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Stands Nate back exactly as he stood when a location was saved. The game's teleport
/// (<c>Menu.TeleportCo</c>) stands him up afresh, all in one frame: its own pick of footing
/// (<c>PlayerMovement.FindBestFootSpots</c> into <c>sampledFeetPos</c>), both feet turned one
/// way (<c>getUpEndRot</c>, from how he was lying), his feet nudged out of anything they're in
/// (<c>FootCollisionSimple</c>), his hips put a metre above their middle, then
/// <c>OnStandUp</c> twice. On a narrow perch that leaves him sitting on it, or off it.
/// <para>
/// While a recall is under way (<see cref="Arm"/>), three hooks put back what was saved:
/// <list type="bullet">
/// <item>after FindBestFootSpots: the saved feet spots, which the teleport plants his feet on;</item>
/// <item>before each OnStandUp (which takes its targets from them): his hips' pivot, the
/// animated character's root (place and turn), the ragdoll's root, and each foot's target
/// (place and turn);</item>
/// <item>after each OnStandUp: every part of the ragdoll where and as it was, still (the game's
/// own "redo last get-up" restores a body the same way), so no physics step ever sees the
/// teleport's pose. The second OnStandUp is the teleport's last word on him.</item>
/// </list>
/// The camera and the loading of the world round the spot are still the teleport's. OnStandUp and
/// FindBestFootSpots also run when Nate gets up after a fall; the hooks only act while armed. If
/// the game has changed and they can't be made, trips still work, with the game's own stand-up.
/// </para>
/// <para>
/// What he holds and wears comes with him: the teleport lets go of both hands' items and knocks his
/// hat off as it sets off, and gives them back once he's there. The game holds an item with a locked
/// joint from the hand (<c>PlayerMovement.handJoints</c>, <c>PutGrabableInHand</c>) and a hat with one
/// from the head (<c>headJoint</c>), and keeps a copy of the hat's collider on his head (the
/// HatColliders object, posed where the hat is when it's put on). It does all that while his ragdoll
/// is still on its way (the teleport moves the ragdoll's root, every part with it, before
/// <c>FinishCutscening</c> puts the parts back on the animation), and this restore moves the parts
/// again. Locked to a hand or head that has since moved, the item was yanked after it, flinging it
/// and throwing Nate (a held ice cream ended up 48 m away). So after every stand-up while armed,
/// once the game is out of its cutscene state, whatever he holds and wears is let go, set where his
/// hand or head now is the game's own way (<c>Grabable.PlaceInHand</c>,
/// <c>PlaceCurrentHatOnHead</c>), stilled, and held again with each joint made afresh
/// (<c>PutGrabableInHand</c>; the head joint locked as the game locks it). The teleport's last
/// stand-up is its last word on him, so that's where they stay. This happens on every trip, with or
/// without a saved stance. Before it, anything he set off with that the teleport didn't give back
/// (dropped while the trip held him) is given back the game's own way (<see cref="CarryGuard"/>).
/// </para>
/// <para>
/// After every stand-up of a trip, last of all, what his ragdoll remembers of its motion is wiped
/// (<see cref="Ragdoll.ForgetMotion"/>): the puppet hands it back to him when it comes back on, and
/// <c>FinishCutscening</c> switches it on straight after its own stand-up. The hooks only act once
/// the teleport has switched him off (<see cref="Release"/>): a trip is held, Nate still where he
/// is, until the spot is ready, and getting up from a fall meanwhile is left to the game.
/// </para>
/// </summary>
public static class StanceRestore
{
	/// <summary>Where Nate's body is to go, in world coordinates (the saved stance carried to the destination).</summary>
	public sealed class Stance
	{
		public Vector3 Hip;

		/// <summary>Which feet are known (bit 0: feet[0], bit 1: feet[1]), and which of those have turns.</summary>
		public int Feet;

		public int FeetTurned;

		public Vector3 Foot0;

		public Vector3 Foot1;

		public Quaternion Foot0Turn;

		public Quaternion Foot1Turn;

		/// <summary>The whole body, or null: the character's root, the ragdoll's root and every part.</summary>
		public bool HasBody;

		public Vector3 Root;

		public Quaternion RootTurn;

		public Vector3 Puppet;

		public Vector3[] Parts;

		public Quaternion[] PartTurns;
	}

	private static Stance _stance;

	/// <summary>A trip is under way: what Nate holds and wears is looked after, and his stance put back if there's one.</summary>
	private static bool _armed;

	/// <summary>
	/// The trip is waiting to set off (held until the spot is ready, Nate still where he was): the
	/// hooks leave him alone, should he get up from a fall meanwhile, until the teleport has
	/// switched him off (<see cref="Release"/>).
	/// </summary>
	private static bool _waiting;

	private static int _failures;

	private static MelonLogger.Instance _log;

	/// <summary>The hooks are in: saved stances can be put back.</summary>
	public static bool Installed { get; private set; }

	/// <summary>Since the last <see cref="Arm"/>: the game asked where his feet go (the saved spots were given).</summary>
	public static int SpotsGiven { get; private set; }

	/// <summary>Since the last <see cref="Arm"/>: the game stood him up (and his targets were set first).</summary>
	public static int StandUps { get; private set; }

	/// <summary>Since the last <see cref="Arm"/>: his whole body was put back (every ragdoll part).</summary>
	public static int BodiesPut { get; private set; }

	/// <summary>Why the body couldn't be put back, if it couldn't (part count changed...), for the log.</summary>
	public static string BodyProblem { get; private set; }

	/// <summary>Since the last <see cref="Arm"/>: what Nate holds and wears was put back on him (once per stand-up out of the cutscene state).</summary>
	public static int Reseated { get; private set; }

	/// <summary>What was put back on him the last time, for the log ("IceTrophy in his right hand"), or null.</summary>
	public static string Carried { get; private set; }

	/// <summary>Since the last <see cref="Arm"/>: what his ragdoll remembers of its motion was wiped (once per stand-up).</summary>
	public static int MotionWiped { get; private set; }

	public static void Install(HarmonyLib.Harmony harmony, MelonLogger.Instance log)
	{
		_log = log;
		try
		{
			harmony.Patch(AccessTools.Method(typeof(PlayerMovement), nameof(PlayerMovement.FindBestFootSpots)),
				postfix: new HarmonyMethod(AccessTools.Method(typeof(StanceRestore), nameof(AfterFindBestFootSpots))));
			harmony.Patch(AccessTools.Method(typeof(PlayerMovement), nameof(PlayerMovement.OnStandUp)),
				prefix: new HarmonyMethod(AccessTools.Method(typeof(StanceRestore), nameof(BeforeOnStandUp))),
				postfix: new HarmonyMethod(AccessTools.Method(typeof(StanceRestore), nameof(AfterOnStandUp))));
			Installed = true;
		}
		catch (Exception ex)
		{
			log.Warning("Saved locations: can't hook the game's stand-up, so going back to one stands Nate up the game's own way, not exactly as he was: " + ex.Message);
		}
	}

	/// <summary>
	/// From now until <see cref="Disarm"/>, a trip is under way: what Nate holds and wears is put back
	/// on him as he stands up, and the game stands him up as <paramref name="stance"/> says (null: the
	/// game's own way).
	/// </summary>
	public static void Arm(Stance stance)
	{
		_stance = stance;
		_armed = true;
		_waiting = true;
		SpotsGiven = 0;
		StandUps = 0;
		BodiesPut = 0;
		BodyProblem = null;
		Reseated = 0;
		Carried = null;
		MotionWiped = 0;
	}

	public static void Disarm()
	{
		_stance = null;
		_armed = false;
		_waiting = false;
	}

	/// <summary>The teleport has switched Nate off where he was: from now on it's placing him, and the hooks do their work.</summary>
	public static void Release()
	{
		_waiting = false;
	}

	/// <summary>The stance to put him in now: none while the trip waits to set off.</summary>
	private static Stance Current => _waiting ? null : _stance;

	// The hooks run inside the game, so none of them lets an exception out.

	private static void AfterFindBestFootSpots(PlayerMovement __instance)
	{
		Stance stance = Current;
		if (stance == null || stance.Feet == 0)
		{
			return;
		}
		try
		{
			Il2CppStructArray<Vector3> spots = __instance.sampledFeetPos;
			if (ReferenceEquals(spots, null) || spots.Length < 2)
			{
				return;
			}
			if ((stance.Feet & 1) != 0)
			{
				spots[0] = stance.Foot0;
			}
			if ((stance.Feet & 2) != 0)
			{
				spots[1] = stance.Foot1;
			}
			SpotsGiven++;
		}
		catch (Exception ex)
		{
			Fail("couldn't give the game Nate's saved footing", ex);
		}
	}

	private static void BeforeOnStandUp(PlayerMovement __instance)
	{
		Stance stance = Current;
		if (stance == null)
		{
			return;
		}
		try
		{
			Transform hip = __instance.hipPivot;
			if (!ReferenceEquals(hip, null))
			{
				hip.position = stance.Hip;
			}
			if (stance.HasBody)
			{
				Animator anim = __instance.anim;
				if (!ReferenceEquals(anim, null))
				{
					Transform root = anim.transform;
					root.position = stance.Root;
					root.rotation = stance.RootTurn;
				}
				PuppetMaster puppet = __instance.pm;
				if (!ReferenceEquals(puppet, null))
				{
					puppet.transform.position = stance.Puppet;
				}
			}
			Il2CppReferenceArray<FootData> feet = __instance.feet;
			for (int i = 0; i < 2 && !ReferenceEquals(feet, null) && i < feet.Length; i++)
			{
				if ((stance.Feet & (1 << i)) == 0)
				{
					continue;
				}
				FootData foot = feet[i];
				Transform target = ReferenceEquals(foot, null) ? null : foot.targetTrans;
				if (ReferenceEquals(target, null))
				{
					continue;
				}
				target.position = (i == 0) ? stance.Foot0 : stance.Foot1;
				if ((stance.FeetTurned & (1 << i)) != 0)
				{
					target.rotation = (i == 0) ? stance.Foot0Turn : stance.Foot1Turn;
				}
			}
			StandUps++;
		}
		catch (Exception ex)
		{
			Fail("couldn't set Nate's targets before he stood up", ex);
		}
	}

	private static void AfterOnStandUp(PlayerMovement __instance)
	{
		if (!_armed || _waiting)
		{
			return;
		}
		Stance stance = _stance;
		if (stance != null && stance.HasBody)
		{
			try
			{
				PutBodyBack(__instance, stance);
			}
			catch (Exception ex)
			{
				Fail("couldn't put Nate's body back as it was", ex);
			}
		}
		// Anything he set off with that the teleport didn't give back (dropped on the way), the game's
		// own way first, so it's put in place with the rest.
		try
		{
			if (!__instance.inCutscene)
			{
				CarryGuard.BringBack(__instance);
			}
		}
		catch (Exception ex)
		{
			Fail("couldn't give back what Nate set off with", ex);
		}
		// Last, so it's done where his hands and head have ended up.
		try
		{
			PutCarriedBack(__instance);
		}
		catch (Exception ex)
		{
			Fail("couldn't put back what Nate holds and wears", ex);
		}
		// He arrives still: nothing of how he was moving before the trip is handed back to him when
		// his ragdoll comes back on (FinishCutscening switches it on straight after its own stand-up,
		// so this runs before that too).
		try
		{
			Ragdoll.ForgetMotion(__instance);
			MotionWiped++;
		}
		catch (Exception ex)
		{
			Fail("couldn't wipe how Nate was moving before the trip", ex);
		}
	}

	private static void PutBodyBack(PlayerMovement pm, Stance stance)
	{
		PuppetMaster puppet = pm.pm;
		Il2CppReferenceArray<Muscle> muscles = ReferenceEquals(puppet, null) ? null : puppet.muscles;
		if (ReferenceEquals(muscles, null) || muscles.Length != stance.Parts.Length)
		{
			BodyProblem = $"the ragdoll has {(ReferenceEquals(muscles, null) ? 0 : muscles.Length)} parts, the saved body {stance.Parts.Length}";
			return;
		}
		Vector3 still = default;
		for (int i = 0; i < muscles.Length; i++)
		{
			Muscle muscle = muscles[i];
			if (ReferenceEquals(muscle, null))
			{
				continue;
			}
			Rigidbody body = muscle._rigidbody_k__BackingField;
			Transform part = muscle._transform_k__BackingField;
			Vector3 at = stance.Parts[i];
			Quaternion turn = stance.PartTurns[i];
			if (!ReferenceEquals(body, null))
			{
				body.position = at;
				body.rotation = turn;
				body.velocity = still;
				body.angularVelocity = still;
			}
			if (!ReferenceEquals(part, null))
			{
				part.position = at;
				part.rotation = turn;
			}
		}
		Physics.SyncTransforms();
		BodiesPut++;
	}

	/// <summary>
	/// Put what Nate holds and wears back where his hands and head are now, locked there afresh (see
	/// the class notes). Nothing until the game is out of its cutscene state: until then it hasn't
	/// given anything back, and wears the hat on the animated head.
	/// </summary>
	private static void PutCarriedBack(PlayerMovement pm)
	{
		if (pm.inCutscene)
		{
			return;
		}
		Il2CppReferenceArray<Grabable> items = pm.handItems;
		Il2CppReferenceArray<ConfigurableJoint> joints = pm.handJoints;
		Il2CppReferenceArray<Rigidbody> hands = pm.handRbs;
		Grabable item0 = HeldIn(items, joints, hands, 0);
		Grabable item1 = HeldIn(items, joints, hands, 1);
		Hat hat = Worn(pm);
		if (item0 == null && item1 == null && hat == null)
		{
			return;
		}
		ConfigurableJoint headJoint = (hat != null) ? pm.headJoint : null;
		// Let go of everything first, so nothing tugs while it's moved.
		if (item0 != null)
		{
			joints[0].connectedBody = null;
		}
		if (item1 != null)
		{
			joints[1].connectedBody = null;
		}
		if (hat != null)
		{
			headJoint.connectedBody = null;
		}
		// Each where the game puts it: in the hand as it grips it, the hat sat on his head (as
		// FinishCutscening does), with the copy of the hat's collider his head carries posed where
		// the hat now is (as WearHat does).
		if (item0 != null)
		{
			item0.PlaceInHand(hands[0].transform, 0);
			Still(item0);
		}
		if (item1 != null)
		{
			item1.PlaceInHand(hands[1].transform, 1);
			Still(item1);
		}
		if (hat != null)
		{
			hat.rbs[0].isKinematic = false;
			pm.PlaceCurrentHatOnHead();
			PoseHatColliders(pm, hat);
			Still(hat);
		}
		Physics.SyncTransforms();
		// Held again the game's own way, each joint made where things now are.
		if (item0 != null)
		{
			pm.PutGrabableInHand(0, item0);
		}
		if (item1 != null)
		{
			pm.PutGrabableInHand(1, item1);
		}
		if (hat != null)
		{
			headJoint.connectedBody = hat.rbs[0];
			Lock(headJoint);
		}
		Physics.SyncTransforms();
		Reseated++;
		Carried = DescribeCarried(item0, item1, hat);
	}

	/// <summary>The item in hand <paramref name="i"/> (0 right, 1 left), if there's one to put back: alive, out in the world, with its joint and hand.</summary>
	private static Grabable HeldIn(Il2CppReferenceArray<Grabable> items, Il2CppReferenceArray<ConfigurableJoint> joints, Il2CppReferenceArray<Rigidbody> hands, int i)
	{
		if (ReferenceEquals(items, null) || ReferenceEquals(joints, null) || ReferenceEquals(hands, null) || i >= items.Length || i >= joints.Length || i >= hands.Length)
		{
			return null;
		}
		Grabable item = items[i];
		if (item == null || joints[i] == null || hands[i] == null || !HasBody(item) || !item.gameObject.activeInHierarchy)
		{
			return null;
		}
		return item;
	}

	/// <summary>Nate's hat, if he wears one to put back: alive, out in the world, with the head joint and head to put it on.</summary>
	private static Hat Worn(PlayerMovement pm)
	{
		Hat hat = pm.currentHat;
		if (hat == null || pm.headJoint == null || pm.headRB == null || !HasBody(hat) || !hat.gameObject.activeInHierarchy)
		{
			return null;
		}
		return hat;
	}

	private static bool HasBody(Grabable grabable)
	{
		Il2CppReferenceArray<Rigidbody> bodies = grabable.rbs;
		return !ReferenceEquals(bodies, null) && bodies.Length > 0 && bodies[0] != null;
	}

	/// <summary>The copy of the hat's collider on Nate's head goes where the hat's own (first) collider is, as <c>WearHat</c> puts it.</summary>
	private static void PoseHatColliders(PlayerMovement pm, Hat hat)
	{
		MeshCollider copy = pm.hatMeshColl;
		Il2CppReferenceArray<Collider> own = hat.colls;
		if (copy == null || ReferenceEquals(own, null) || own.Length == 0 || own[0] == null)
		{
			return;
		}
		Transform from = own[0].transform;
		Transform to = copy.transform;
		to.position = from.position;
		to.rotation = from.rotation;
	}

	/// <summary>Still, as put: no speed in its bodies, nor any kept for when its physics comes back on (<c>Savable.lastVelocity</c>).</summary>
	private static void Still(Grabable grabable)
	{
		Vector3 zero = default;
		grabable.lastVelocity = zero;
		Il2CppReferenceArray<Rigidbody> bodies = grabable.rbs;
		if (ReferenceEquals(bodies, null))
		{
			return;
		}
		foreach (Rigidbody body in bodies)
		{
			if (body != null && !body.isKinematic)
			{
				body.velocity = zero;
				body.angularVelocity = zero;
			}
		}
	}

	/// <summary>Every motion locked, as the game locks the head joint on a hat.</summary>
	private static void Lock(ConfigurableJoint joint)
	{
		joint.xMotion = ConfigurableJointMotion.Locked;
		joint.yMotion = ConfigurableJointMotion.Locked;
		joint.zMotion = ConfigurableJointMotion.Locked;
		joint.angularXMotion = ConfigurableJointMotion.Locked;
		joint.angularYMotion = ConfigurableJointMotion.Locked;
		joint.angularZMotion = ConfigurableJointMotion.Locked;
	}

	/// <summary>"IceTrophy in his right hand, StetsonHat on his head", for the log.</summary>
	private static string DescribeCarried(Grabable item0, Grabable item1, Hat hat)
	{
		System.Text.StringBuilder text = new System.Text.StringBuilder(64);
		if (item0 != null)
		{
			text.Append(NameOf(item0)).Append(" in his right hand");
		}
		if (item1 != null)
		{
			text.Append((text.Length > 0) ? ", " : "").Append(NameOf(item1)).Append(" in his left hand");
		}
		if (hat != null)
		{
			text.Append((text.Length > 0) ? ", " : "").Append(NameOf(hat)).Append(" on his head");
		}
		return text.ToString();
	}

	/// <summary>An item's object name without Unity's "(Clone)".</summary>
	public static string NameOf(Component item)
	{
		string name = item.gameObject.name ?? "?";
		return name.EndsWith("(Clone)", StringComparison.Ordinal) ? name.Substring(0, name.Length - 7).TrimEnd() : name;
	}

	private static void Fail(string what, Exception ex)
	{
		if (_failures++ < 3)
		{
			_log?.Warning("Saved locations: " + what + ": " + ex.Message);
		}
	}
}
