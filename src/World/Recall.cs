using System;
using System.Runtime.CompilerServices;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppRootMotion.Dynamics;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Sends Nate to a saved location with the game's own teleport, <c>Menu.Teleport</c>: what the
/// game uses to put him where a save left him, and what its developers' "Load Nate Pos" cheat
/// calls (<c>DevCheatsGoHere</c>, which saves <c>PlayerMovement.hipPivot</c>'s position, as
/// <see cref="TryGetNatePosition"/> does). The game streams in the world round the spot first,
/// finds the ground under it (up to 100 m down), stands Nate up there, and gives him back his hat
/// and whatever he was holding; <see cref="StanceRestore"/> then puts those back in place, locked
/// to his hands and head afresh (left to the teleport, a held item could be flung and Nate thrown
/// down). The teleport only takes what's on him when it switches him off, so while the trip holds
/// him (see below) what he holds and wears is kept on him even when he's down, and anything
/// dropped on the way all the same is given back there (<see cref="CarryGuard"/>). Followers stay
/// where they were.
/// <para>
/// A recall asked for on the map waits until the map has closed and the game runs again. First
/// person is left for the trip (see <see cref="GameBridge.SuspendFirstPerson"/>) and comes back
/// once Nate is there. Where Nate was becomes the "where you were" location, so a recall can be
/// undone.
/// </para>
/// <para>
/// Nate faces the way he did when the location was saved, and so does the camera, as when the
/// game loads its own save (<c>Menu.LoadSave</c> sets <c>camAng</c> from the saved facing and turns
/// <c>anim</c>'s transform to it, and <c>camRight</c>, before teleporting). The teleport places his
/// feet by that transform's turn (<c>FindBestFootSpots</c>), in the frame after it has switched his
/// ragdoll off (<c>PuppetMaster.mode</c> Disabled); so he's turned in that frame, not before (a
/// ragdoll still following the animation would be wrenched round). The game sets the camera again
/// once he's stood up (<c>FinishCutscening</c>), so it's turned back on arrival.
/// </para>
/// <para>
/// He's stood back up exactly as he stood when saved (<see cref="StanceRestore"/>): feet on their
/// spots and turned as they were, hips, the character's and the ragdoll's roots and every ragdoll
/// part where they were. The teleport keeps the saved height instead of dropping him to the ground
/// under the spot (its <c>dead</c> flag: one look down, keeping the height unless it's under a
/// metre above the ground). A location saved with neither foot planted gets the game's own
/// stand-up, dropped to the ground. After such a trip, how he stands is logged a few times.
/// </para>
/// <para>
/// Nate can be sent off while he's down after a fall: the game's teleport stands him up wherever
/// it takes him (it's what the game's own menu uses whatever state he's in).
/// </para>
/// <para>
/// A gym's time trial carries on through a trip (<see cref="GymGuard"/>): the teleport switching
/// his body back on inside the gym's room would otherwise count as walking in, which starts the
/// run over.
/// </para>
/// <para>
/// He arrives steady (<see cref="ArrivalGate"/>, <see cref="Ragdoll"/>): the teleport is held
/// (its loader's "world loaded" flag kept down) until the world round the spot has loaded and gone
/// quiet, there's ground under his saved feet, and he has been held still for a few physics steps
/// where he was; and on arrival, what his ragdoll remembers of its motion is wiped. Without that,
/// the game handed him back on arrival the motion he had when he set off: trips taken while he
/// tumbled or fell, and long trips (the ground he stood on can unload while the world round the
/// spot loads), threw him down as he arrived. If he still goes down or is thrown in the first
/// moments there, he's sent again, once.
/// </para>
/// </summary>
public class Recall
{
	private enum Phase
	{
		Idle,
		/// <summary>Asked for; waiting for the map to close.</summary>
		Pending,
		/// <summary>The game is moving Nate (Menu.teleporting).</summary>
		Moving,
		/// <summary>He's there; a moment for things to settle before first person comes back, watching that he stays up.</summary>
		Settling
	}

	/// <summary>Give up waiting on the game's teleport after this many seconds (it loads the world round the spot first).</summary>
	private const float Timeout = 60f;

	/// <summary>Seconds after arriving before first person comes back.</summary>
	private const float SettleTime = 0.3f;

	/// <summary>Seconds after arriving in which Nate going down, or being thrown, is the trip's doing.</summary>
	private const float ArrivalWatch = 0.6f;

	/// <summary>
	/// In that time, his pelvis further than this (m) from where it was put, or from his hips' pivot
	/// (the controller's, normally 3 cm from it, and left behind when the ragdoll is flung): thrown.
	/// </summary>
	private const float ThrownDistance = 0.75f;

	/// <summary>The most arrival samples kept for the log, should he be thrown.</summary>
	private const int TraceLines = 40;

	/// <summary>A planted foot higher than this above Nate's hips (m) isn't believed.</summary>
	private const float MaxFootAboveHip = 0.6f;

	/// <summary>A planted foot further than this below his hips (m) isn't believed.</summary>
	private const float MaxFootBelowHip = 2f;

	/// <summary>A planted foot further than this from his hips sideways (m) isn't believed.</summary>
	private const float MaxFootSpread = 1.5f;

	private readonly ModContext _ctx;

	private Phase _phase;

	private SavedLocation _target;

	private string _targetTitle;

	/// <summary>The target in a sentence: "Location 3", "your quick save", "where you were".</summary>
	private string _targetPhrase;

	/// <summary>The target's kind: a kept location, the quick save, or "where you were".</summary>
	private LocationSlot _slot;

	/// <summary>Frames spent in <see cref="Phase.Pending"/> since the map closed.</summary>
	private int _frames;

	private float _since;

	private bool _resumeFirstPerson;

	/// <summary>Which way Nate and the camera are to face there (unknown for a location saved before facings were kept).</summary>
	private SavedPose _pose;

	/// <summary>Nate has been turned the saved way (once his ragdoll was off), or there's no way to turn him.</summary>
	private bool _faced;

	/// <summary>This trip puts his feet back on their saved spots (<see cref="StanceRestore"/>).</summary>
	private bool _precise;

	/// <summary>He was down after a fall when sent off (for the log).</summary>
	private bool _wasDown;

	private int _errors;

	/// <summary>The stance this trip put Nate into, for <see cref="StartCheck"/>.</summary>
	private StanceRestore.Stance _stance;

	// After a trip into a saved stance, or with something carried, a few looks at how Nate stands and
	// where what he carries is, for the log.
	private static readonly float[] CheckTimes = { 0f, 0.5f, 1.5f, 3f };

	private bool _checking;

	/// <summary>The stance the checks compare him with, or null (a trip without one).</summary>
	private StanceRestore.Stance _checkStance;

	private float _checkStart;

	private int _checkStep;

	/// <summary>When the teleport may put Nate down: held until the spot is ready and he's still.</summary>
	private readonly ArrivalGate _gate = new ArrivalGate();

	/// <summary>The teleport has switched Nate off where he was: he's no longer held still there.</summary>
	private bool _offSeen;

	/// <summary>Where this trip puts him (the saved spot's copy in the loop he set off in).</summary>
	private Vector3 _destination;

	/// <summary>This is the trip's second go, after he was thrown on arriving the first time.</summary>
	private bool _retry;

	/// <summary>Watching, in the first moments after arriving, for him going down or being thrown.</summary>
	private bool _watching;

	/// <summary>What he was like in those moments, for the log should he be thrown.</summary>
	private readonly System.Text.StringBuilder _trace = new System.Text.StringBuilder(2048);

	private int _traceCount;

	public Recall(ModContext ctx)
	{
		_ctx = ctx;
	}

	/// <summary>A recall is under way: asked for, or Nate is on his way.</summary>
	public bool Busy => _phase != Phase.Idle;

	/// <summary>A recall was asked for on the map; it starts once the map has closed.</summary>
	public bool Pending => _phase == Phase.Pending;

	/// <summary>
	/// Where Nate is, as the game's teleport understands it: PlayerMovement.hipPivot, or failing
	/// that the ragdoll root the map shows.
	/// </summary>
	public bool TryGetNatePosition(out Vector3 position)
	{
		try
		{
			if (HipPosition(out position))
			{
				return true;
			}
		}
		catch (Exception ex)
		{
			Warn("can't read Nate's position from the game: " + ex.Message);
		}
		position = _ctx.Player.WorldPos;
		return _ctx.Player.Found;
	}

	/// <summary>Which way Nate and the camera face now, as the game's own save records it.</summary>
	public bool TryGetNatePose(out SavedPose pose)
	{
		try
		{
			if (ReadPose(out pose))
			{
				return true;
			}
		}
		catch (Exception ex)
		{
			Warn("can't read which way Nate faces: " + ex.Message);
		}
		pose = default;
		return false;
	}

	/// <summary>Why Nate can't be moved right now, or null if he can.</summary>
	public string Blocker()
	{
		try
		{
			return GameBlocker();
		}
		catch (Exception ex)
		{
			Warn("can't read the game's state: " + ex.Message);
			return "the mod can't talk to the game (see the log)";
		}
	}

	/// <summary>Ask to send Nate to <paramref name="target"/>. Null if it will happen, else why not.</summary>
	public string Request(SavedLocation target)
	{
		if (Busy)
		{
			return "Nate is already on his way somewhere";
		}
		string why = Blocker();
		if (why != null)
		{
			return why;
		}
		_target = target;
		_targetTitle = target.Title;
		_targetPhrase = target.Phrase;
		_slot = target.Slot;
		_phase = Phase.Pending;
		_frames = 0;
		_retry = false;
		return null;
	}

	/// <summary>Every frame. <paramref name="mapOpen"/>: the full map is open (the game is frozen).</summary>
	public void Update(bool mapOpen)
	{
		GymGuard.Tick(!mapOpen, Frame.Dt);
		if (_checking)
		{
			Check();
		}
		switch (_phase)
		{
		case Phase.Pending:
			if (!mapOpen)
			{
				Start();
			}
			break;
		case Phase.Moving:
			Watch();
			break;
		case Phase.Settling:
			Settle();
			break;
		}
	}

	/// <summary>
	/// Every physics step. While a trip waits for the teleport to switch Nate off, he's held still
	/// where he is: whatever he was doing is over before he goes, and he can't fall through ground
	/// that unloads under him while the world round the spot loads.
	/// </summary>
	public void FixedUpdate()
	{
		if (_phase != Phase.Moving || _offSeen)
		{
			return;
		}
		try
		{
			if (Ragdoll.SwitchedOff())
			{
				SeeSwitchedOff();
				return;
			}
			float fastest = Ragdoll.HoldStill();
			if (fastest >= 0f)
			{
				_gate.Step(Frame.Now, fastest);
			}
		}
		catch (Exception ex)
		{
			// No more holding him still; the stance is put back whenever the game stands him up.
			Warn("can't hold Nate still for the trip: " + ex.Message);
			_offSeen = true;
			StanceRestore.Release();
		}
	}

	private void Start()
	{
		if (_frames++ == 0)
		{
			string why = Blocker();
			if (why != null)
			{
				Fail(why);
				return;
			}
			_resumeFirstPerson = GameBridge.SuspendFirstPerson();
			if (_resumeFirstPerson)
			{
				// Give the game a frame to leave first person before it takes Nate's hat off.
				return;
			}
		}
		Go();
	}

	/// <summary>
	/// Start the game's teleport. <paramref name="retry"/>: a second go at the same spot, after Nate
	/// was thrown on arriving the first time (same destination and stance; "where you were" stays
	/// where he first set off from).
	/// </summary>
	private void Go(bool retry = false)
	{
		string why = Blocker();
		if (why != null)
		{
			Fail(why);
			return;
		}
		if (!TryGetNatePosition(out Vector3 nate))
		{
			Fail("Nate isn't here yet");
			return;
		}
		SavedPose was = default;
		if (!retry)
		{
			// Which way he faces now, for "where you were"; and the way he's to face there, read before
			// "where you were" moves (going back there is going to that very spot).
			TryGetNatePose(out was);
			_pose = _target.Pose;
			// The copy of the spot in the loop Nate is in now: the same place, without a 512 m detour.
			_destination = WorldLoop.NearestCopy(_target.World, nate.x);
		}
		_wasDown = NateDown();
		float speed = DepartureSpeed();
		Vector3 destination = _destination;
		// His feet go back on their saved spots (carried to that copy too), and he keeps the saved
		// height so his hips come down over them. What he holds and wears is put back on him there
		// either way.
		_precise = (_pose.Feet != 0 || _pose.Body != null) && StanceRestore.Installed;
		_stance = _precise ? StanceAt(destination, _pose) : null;
		StanceRestore.Arm(_stance);
		string carrying = Carrying();
		// What he holds and wears stays on him while the trip holds him, and is given back there if
		// it was dropped all the same (on a second go: what he set off with the first time).
		CarryGuard.Guard(again: retry);
		// A gym's time trial carries on through the trip: Nate's body coming back on in the gym's
		// room would otherwise start it over.
		GymGuard.Arm();
		// Held until the spot is ready (and there's ground under his saved feet) and he's still.
		_gate.Begin(Frame.Now, _stance != null && _stance.Feet != 0);
		_offSeen = false;
		_watching = false;
		bool started;
		try
		{
			started = Teleport(destination, keepHeight: _precise);
		}
		catch (Exception ex)
		{
			StanceRestore.Disarm();
			GymGuard.Disarm();
			_gate.Abandon(Frame.Now);
			_ctx.Log.Error("Recall: the game's teleport failed: " + ex);
			Fail("the game's teleport didn't work (see the log)");
			return;
		}
		if (!started)
		{
			StanceRestore.Disarm();
			GymGuard.Disarm();
			_gate.Abandon(Frame.Now);
			Fail("the game wouldn't move Nate just now");
			return;
		}
		_faced = !_pose.Known;
		_phase = Phase.Moving;
		_since = Frame.Now;
		string motion = (speed >= 0f) ? $", going {speed:0.0} m/s" : "";
		if (retry)
		{
			_ctx.Log.Msg($"Recall: {_targetTitle} again, to ({destination.x:0.0}, {destination.y:0.0}, {destination.z:0.0}) from ({nate.x:0.0}, {nate.y:0.0}, {nate.z:0.0}){(_wasDown ? ", Nate down" : "")}{motion}.");
			return;
		}
		// Only now: a recall that never happened shouldn't move "where you were".
		_ctx.Locations.SetBack(nate, was);
		// Arriving is what tracking it was for (and "where you were" has just moved to where Nate set off).
		bool tracked = _ctx.Target == _target;
		if (tracked)
		{
			_ctx.Target = null;
		}
		_ctx.Toasts.Show(((_slot == LocationSlot.Kept) ? "Going to " : "Going back to ") + _targetPhrase + "..." + (tracked ? "  (tracking cleared)" : ""));
		_ctx.Log.Msg($"Recall: {_targetTitle}, to ({destination.x:0.0}, {destination.y:0.0}, {destination.z:0.0}){(_pose.Known ? $" facing {_pose.Facing:0}°" : "")}{(_precise ? $" as saved ({FeetText(_pose.Feet)}{((_pose.Body != null) ? $", body of {_pose.Body.Parts.Length} parts" : "")})" : "")} from ({nate.x:0.0}, {nate.y:0.0}, {nate.z:0.0}){(_wasDown ? ", Nate down" : "")}{motion}{((carrying != null) ? ", with " + carrying : "")}.");
	}

	/// <summary>How fast Nate's pelvis is going as he sets off (m/s), for the log; -1 if it can't be read.</summary>
	private float DepartureSpeed()
	{
		try
		{
			return Ragdoll.Pelvis(out Vector3 _, out float speed) ? speed : -1f;
		}
		catch (Exception ex)
		{
			Warn("can't see how fast Nate is going: " + ex.Message);
			return -1f;
		}
	}

	/// <summary>What Nate holds and wears as he sets off ("IceTrophy in his right hand"), for the log; null if nothing.</summary>
	private string Carrying()
	{
		try
		{
			return DescribeCarrying();
		}
		catch (Exception ex)
		{
			Warn("can't see what Nate is carrying: " + ex.Message);
			return null;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string DescribeCarrying()
	{
		PlayerMovement pm = PlayerMovement.me;
		if (pm == null)
		{
			return null;
		}
		System.Text.StringBuilder text = new System.Text.StringBuilder(64);
		Il2CppReferenceArray<Grabable> items = pm.handItems;
		for (int i = 0; i < 2 && !ReferenceEquals(items, null) && i < items.Length; i++)
		{
			Grabable item = items[i];
			if (item != null)
			{
				text.Append((text.Length > 0) ? ", " : "").Append(StanceRestore.NameOf(item)).Append((i == 0) ? " in his right hand" : " in his left hand");
			}
		}
		Hat hat = pm.currentHat;
		if (hat != null && hat.gameObject.activeSelf)
		{
			text.Append((text.Length > 0) ? ", " : "").Append(StanceRestore.NameOf(hat)).Append(" on his head");
		}
		return (text.Length > 0) ? text.ToString() : null;
	}

	private static Vector3 Offset(Vector3 position, Vector3 offset)
	{
		return Fast.V3(position.x + offset.x, position.y + offset.y, position.z + offset.z);
	}

	private static string FeetText(int feet)
	{
		return (feet == 3) ? "both feet" : ((feet == 0) ? "no foot planted" : "one foot");
	}

	/// <summary>The saved stance carried to <paramref name="hip"/> (the saved position's copy in the loop Nate is in).</summary>
	private static StanceRestore.Stance StanceAt(Vector3 hip, SavedPose pose)
	{
		StanceRestore.Stance stance = new StanceRestore.Stance
		{
			Hip = hip,
			Feet = pose.Feet,
			FeetTurned = pose.FeetTurned & pose.Feet,
			Foot0 = Offset(hip, pose.Foot0),
			Foot1 = Offset(hip, pose.Foot1),
			Foot0Turn = pose.Foot0Turn,
			Foot1Turn = pose.Foot1Turn
		};
		SavedBody body = pose.Body;
		if (body != null && body.Parts != null && body.PartTurns != null && body.Parts.Length == body.PartTurns.Length)
		{
			stance.HasBody = true;
			stance.Root = Offset(hip, body.Root);
			stance.RootTurn = body.RootTurn;
			stance.Puppet = Offset(hip, body.Puppet);
			stance.Parts = new Vector3[body.Parts.Length];
			stance.PartTurns = new Quaternion[body.Parts.Length];
			for (int i = 0; i < body.Parts.Length; i++)
			{
				stance.Parts[i] = Offset(hip, body.Parts[i]);
				stance.PartTurns[i] = body.PartTurns[i];
			}
		}
		return stance;
	}

	private void Watch()
	{
		if (!_offSeen)
		{
			bool off;
			try
			{
				off = Ragdoll.SwitchedOff();
			}
			catch (Exception ex)
			{
				// Without knowing, the stance is put back whenever the game stands him up.
				Warn("can't tell whether the teleport has switched Nate off: " + ex.Message);
				off = false;
				StanceRestore.Release();
			}
			if (off)
			{
				SeeSwitchedOff();
			}
			else
			{
				if (_gate.Holding)
				{
					Hold();
				}
				// Last thing before the teleport may look at his hands this frame.
				CarryGuard.Tend();
			}
		}
		if (!_faced)
		{
			try
			{
				_faced = TurnOnceRagdollOff(_pose);
			}
			catch (Exception ex)
			{
				Warn("can't turn Nate the saved way: " + ex.Message);
				_faced = true;
			}
		}
		bool moving;
		try
		{
			moving = Teleporting();
		}
		catch (Exception ex)
		{
			Warn("can't tell whether Nate has arrived: " + ex.Message);
			moving = false;
		}
		float elapsed = Frame.Now - _since;
		if (moving && elapsed < Timeout)
		{
			return;
		}
		if (_gate.Holding)
		{
			// The teleport ended (or was given up on) while the trip was still held.
			_gate.Abandon(Frame.Now);
			_ctx.Log.Msg("Recall: the trip ended while still held (" + _gate.Summary() + ").");
		}
		_offSeen = true;
		StanceRestore.Disarm();
		CarryGuard.Release();
		// Anything he set off with that still isn't on him is given back now, if it can be.
		string missing = null;
		if (!moving)
		{
			try
			{
				missing = CarryGuard.Arrived();
			}
			catch (Exception ex)
			{
				Warn("can't see whether Nate has all he set off with: " + ex.Message);
			}
		}
		// Gyms stay guarded a moment longer: his body comes back on in the next physics steps.
		GymGuard.Arrived();
		string footing = !_precise ? "" : ((StanceRestore.StandUps == 0)
			? " (the game didn't stand him up through its usual steps, so he's as it left him)"
			: $", stood as saved (footing given {StanceRestore.SpotsGiven}x, targets set {StanceRestore.StandUps}x, body put back {StanceRestore.BodiesPut}x{((StanceRestore.BodyProblem != null) ? ": " + StanceRestore.BodyProblem : "")})");
		if (CarryGuard.Brought != null)
		{
			footing += $", {CarryGuard.Brought} given back to him (dropped on the way)";
		}
		if (StanceRestore.Reseated > 0)
		{
			footing += $", {StanceRestore.Carried} put back in place ({StanceRestore.Reseated}x)";
		}
		footing += (StanceRestore.MotionWiped > 0) ? $", his motion wiped {StanceRestore.MotionWiped}x" : ", his motion NOT wiped (the game didn't stand him up)";
		StartCheck();
		if (moving)
		{
			_ctx.Log.Warning($"Recall: the game's teleport still hasn't finished after {Timeout:0} s; carrying on regardless.");
		}
		else
		{
			_ctx.Log.Msg($"Recall: arrived after {elapsed:0.0} s{footing}{(_pose.Known && !_faced ? " (Nate couldn't be turned the saved way)" : "")}.");
		}
		if (missing != null)
		{
			_ctx.Log.Warning($"Recall: Nate arrived without {missing}, which he set off with.");
		}
		// Watch the first moments there: going down or being thrown then is the trip's doing. (Not
		// if the teleport never finished: he isn't there to watch.)
		_watching = !moving;
		_trace.Clear();
		_traceCount = 0;
		if (_pose.Known)
		{
			// The game set the camera when it stood him up: back to the way it looked when saved.
			try
			{
				SetCamera(_pose);
			}
			catch (Exception ex)
			{
				Warn("can't turn the camera the saved way: " + ex.Message);
			}
		}
		_phase = Phase.Settling;
		_since = Frame.Now;
	}

	/// <summary>Log how Nate stands a few times over the next 3 s, against the stance he was put into, and where what he carries is.</summary>
	private void StartCheck()
	{
		_checking = true;
		_checkStance = _stance;
		_checkStart = Frame.Now;
		_checkStep = 0;
	}

	private void Check()
	{
		float t = Frame.Now - _checkStart;
		if (t < CheckTimes[_checkStep])
		{
			return;
		}
		try
		{
			// The first look also says how his ragdoll and the game's record of his motion are.
			_ctx.Log.Msg($"Recall check +{t:0.0} s: {Describe(_checkStance)}{((_checkStep == 0) ? "; " + DescribeRagdoll() : "")}.");
		}
		catch (Exception ex)
		{
			Warn("can't see how Nate stands: " + ex.Message);
			_checking = false;
			_checkStance = null;
			return;
		}
		if (++_checkStep >= CheckTimes.Length)
		{
			_checking = false;
			_checkStance = null;
		}
	}

	/// <summary>
	/// Where Nate's hips, pelvis and feet are against the stance (if there's one), how far what he
	/// holds and wears is from his hand or head, and whether he's down.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string Describe(StanceRestore.Stance stance)
	{
		PlayerMovement pm = PlayerMovement.me;
		if (pm == null)
		{
			return "Nate isn't there";
		}
		System.Text.StringBuilder text = new System.Text.StringBuilder(200);
		if (stance != null)
		{
			DescribeStance(pm, stance, text);
		}
		DescribeCarried(pm, text);
		text.Append(pm.dead ? ", DOWN" : ", up");
		return (text.Length > 2 && text[0] == ',') ? text.ToString(2, text.Length - 2) : text.ToString();
	}

	/// <summary>", IceTrophy 0.12 m from his right hand, StetsonHat 0.09 m from his head": a held thing far from its hand has been thrown or dropped.</summary>
	private static void DescribeCarried(PlayerMovement pm, System.Text.StringBuilder text)
	{
		Il2CppReferenceArray<Grabable> items = pm.handItems;
		Il2CppReferenceArray<Rigidbody> hands = pm.handRbs;
		for (int i = 0; i < 2 && !ReferenceEquals(items, null) && !ReferenceEquals(hands, null) && i < items.Length && i < hands.Length; i++)
		{
			Grabable item = items[i];
			Rigidbody hand = hands[i];
			if (item != null && hand != null)
			{
				text.Append(", ").Append(StanceRestore.NameOf(item)).Append(' ').Append(Apart(item.transform.position, hand.position)).Append((i == 0) ? " from his right hand" : " from his left hand");
			}
		}
		Hat hat = pm.currentHat;
		Transform head = pm.headRB;
		if (hat != null && head != null && hat.gameObject.activeSelf)
		{
			text.Append(", ").Append(StanceRestore.NameOf(hat)).Append(' ').Append(Apart(hat.transform.position, head.position)).Append(" from his head");
		}
	}

	private static string Apart(Vector3 a, Vector3 b)
	{
		Vector3 d = From(a, b);
		return $"{Fast.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z):0.00} m";
	}

	/// <summary>Where Nate's hips, pelvis and feet are against the stance.</summary>
	private static void DescribeStance(PlayerMovement pm, StanceRestore.Stance stance, System.Text.StringBuilder text)
	{
		Transform hip = pm.hipPivot;
		if (hip != null)
		{
			text.Append("hips ").Append(Off(From(stance.Hip, hip.position)));
		}
		if (stance.HasBody)
		{
			PuppetMaster puppet = pm.pm;
			Il2CppReferenceArray<Muscle> muscles = ReferenceEquals(puppet, null) ? null : puppet.muscles;
			Rigidbody pelvis = (ReferenceEquals(muscles, null) || muscles.Length == 0 || ReferenceEquals(muscles[0], null)) ? null : muscles[0]._rigidbody_k__BackingField;
			if (!ReferenceEquals(pelvis, null))
			{
				text.Append(", pelvis ").Append(Off(From(stance.Parts[0], pelvis.position)));
			}
		}
		Il2CppReferenceArray<FootData> feet = pm.feet;
		for (int i = 0; i < 2 && !ReferenceEquals(feet, null) && i < feet.Length; i++)
		{
			FootData foot = feet[i];
			if (ReferenceEquals(foot, null))
			{
				continue;
			}
			text.Append(", foot ").Append(i).Append(foot.planted ? " planted" : " up");
			if ((stance.Feet & (1 << i)) != 0)
			{
				Rigidbody body = foot.rb;
				if (!ReferenceEquals(body, null))
				{
					text.Append(' ').Append(Off(From((i == 0) ? stance.Foot0 : stance.Foot1, body.position))).Append(" from its spot");
				}
			}
		}
	}

	/// <summary>An offset as "+3 cm up, 5 cm aside".</summary>
	private static string Off(Vector3 d)
	{
		return $"{d.y * 100f:+0;-0;0} cm up, {Fast.Sqrt(d.x * d.x + d.z * d.z) * 100f:0} cm aside";
	}

	private void Finish()
	{
		_phase = Phase.Idle;
		StanceRestore.Disarm();
		CarryGuard.Forget();
		ResumeFirstPerson();
		_ctx.Toasts.Show((_slot == LocationSlot.Back) ? "Back where you were" : ((_slot == LocationSlot.Quick) ? "Back at your quick save" : ("At " + _targetTitle)));
		_target = null;
		_retry = false;
		_watching = false;
	}

	private void Fail(string why)
	{
		_phase = Phase.Idle;
		StanceRestore.Disarm();
		CarryGuard.Forget();
		GymGuard.Disarm();
		_gate.Abandon(Frame.Now);
		_offSeen = true;
		_watching = false;
		ResumeFirstPerson();
		_ctx.Toasts.Show("Can't go to " + _targetPhrase + ": " + why);
		_ctx.Log.Msg("Recall to " + _targetTitle + " didn't happen: " + why + ".");
		_target = null;
		_retry = false;
	}

	/// <summary>The teleport has switched Nate off where he was: holding him still there is over.</summary>
	private void SeeSwitchedOff()
	{
		_offSeen = true;
		// From here the teleport is placing him: the stance hooks take over, and letting go of what
		// he holds is the teleport's.
		StanceRestore.Release();
		CarryGuard.Release();
		if (_gate.Holding)
		{
			// It can't go on while the trip is held; something else let it.
			_gate.Abandon(Frame.Now);
			_ctx.Log.Msg("Recall: the game moved Nate on before the trip was let go (" + _gate.Summary() + ").");
		}
		// What his ragdoll keeps of its motion now is what it's handed back when he's switched on
		// at the spot, unless the stand-up there wipes it (see StanceRestore).
		_ctx.Log.Msg("Recall: Nate switched off for the trip: " + DescribeRagdoll() + CarryGuard.Kept() + ".");
	}

	/// <summary>
	/// While the trip is held: keep the teleport waiting (its loader's flag down) until the world
	/// round the spot is in and quiet, his saved footing is there and he's still.
	/// </summary>
	private void Hold()
	{
		try
		{
			bool loaderReady = LoaderSaysLoaded();
			bool loading = SomethingLoading();
			bool footing = _gate.WantsFooting && FootingThere();
			_gate.Update(Frame.Now, loaderReady, loading, footing);
			if (_gate.Holding)
			{
				// The teleport goes on as soon as this says the world round the spot is in; the
				// loader works it out again before the teleport next looks.
				KeepLoaderWaiting();
			}
			else
			{
				_ctx.Log.Msg("Recall: let go: " + _gate.Summary() + ".");
			}
		}
		catch (Exception ex)
		{
			Warn("can't hold the trip until the spot is ready: " + ex.Message);
			_gate.Abandon(Frame.Now);
		}
	}

	/// <summary>Ground under each foot he's to stand on there (always so without a saved footing).</summary>
	private bool FootingThere()
	{
		StanceRestore.Stance stance = _stance;
		if (stance == null || stance.Feet == 0)
		{
			return true;
		}
		return ((stance.Feet & 1) == 0 || Ragdoll.GroundUnder(stance.Foot0)) && ((stance.Feet & 2) == 0 || Ragdoll.GroundUnder(stance.Foot1));
	}

	/// <summary>
	/// After arriving: watch the first moments for Nate going down or being thrown (then he's sent
	/// again, once), and bring first person back.
	/// </summary>
	private void Settle()
	{
		float t = Frame.Now - _since;
		if (_watching)
		{
			string thrown;
			try
			{
				thrown = ThrownOnArrival(t);
			}
			catch (Exception ex)
			{
				Warn("can't see whether Nate stayed on his feet: " + ex.Message);
				thrown = null;
				_watching = false;
			}
			if (thrown != null)
			{
				OnThrown(thrown, t);
				return;
			}
			if (t >= ArrivalWatch)
			{
				_watching = false;
			}
		}
		if (!_watching && t >= SettleTime)
		{
			Finish();
		}
	}

	/// <summary>
	/// "knocked down" or "thrown 2.3 m" if Nate went down so soon after arriving, or his pelvis is
	/// well away from where it was put or from his hips' pivot; else null. Keeps a line on how he
	/// is, for the log.
	/// </summary>
	private string ThrownOnArrival(float t)
	{
		bool down = Ragdoll.Down();
		bool hasBody = _stance != null && _stance.HasBody && _stance.Parts != null && _stance.Parts.Length > 0;
		float fromPut = -1f;
		float fromHips = -1f;
		bool found = Ragdoll.Pelvis(out Vector3 pelvis, out float speed);
		if (found && hasBody)
		{
			fromPut = Distance(_stance.Parts[0], pelvis);
		}
		if (found && HipPosition(out Vector3 hips))
		{
			fromHips = Distance(hips, pelvis);
		}
		if (_traceCount < TraceLines)
		{
			_traceCount++;
			_trace.Append("\n    +").Append(t.ToString("0.00")).Append(" s: pelvis ");
			if (!found)
			{
				_trace.Append("not found");
			}
			else
			{
				if (fromPut >= 0f)
				{
					_trace.Append(fromPut.ToString("0.00")).Append(" m from where it was put, ");
				}
				_trace.Append(fromHips.ToString("0.00")).Append(" m from his hips' pivot");
			}
			_trace.Append(", going ").Append(speed.ToString("0.0")).Append(" m/s; ");
			Ragdoll.Describe(_trace);
		}
		if (down)
		{
			return "knocked down";
		}
		float off = Math.Max(fromPut, fromHips);
		if (off > ThrownDistance)
		{
			return $"thrown {off:0.0} m";
		}
		return null;
	}

	private static float Distance(Vector3 a, Vector3 b)
	{
		Vector3 d = From(a, b);
		return Fast.Sqrt(d.x * d.x + d.y * d.y + d.z * d.z);
	}

	/// <summary>Nate was thrown or knocked down arriving: log how, and send him again (once).</summary>
	private void OnThrown(string how, float t)
	{
		_watching = false;
		// This arrival's checks are moot now.
		_checking = false;
		_checkStance = null;
		_ctx.Log.Warning($"Recall: Nate was {how} {t:0.00} s after arriving at {_targetTitle}{(_retry ? " on the second go" : "")}. How he was since arriving:{_trace}");
		if (!_retry)
		{
			_retry = true;
			_ctx.Toasts.Show("Nate lost his footing arriving - putting him back...");
			Go(retry: true);
			return;
		}
		_phase = Phase.Idle;
		StanceRestore.Disarm();
		CarryGuard.Forget();
		ResumeFirstPerson();
		_ctx.Toasts.Show("Nate couldn't keep his footing at " + _targetPhrase);
		_target = null;
		_retry = false;
	}

	/// <summary>How his ragdoll and the game's record of his motion are, for the log.</summary>
	private string DescribeRagdoll()
	{
		try
		{
			System.Text.StringBuilder text = new System.Text.StringBuilder(160);
			Ragdoll.Describe(text);
			return text.ToString();
		}
		catch (Exception ex)
		{
			return "(can't see his ragdoll: " + ex.Message + ")";
		}
	}

	/// <summary>The game's loader last said the world round its loading spot is in (BestRegionLoader.fullyLoaded, worked out as the camera renders).</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool LoaderSaysLoaded()
	{
		return BestRegionLoader.fullyLoaded;
	}

	/// <summary>Something is loading right now (a world chunk, props, an item...: BestRegionLoader.somebodyLoading).</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool SomethingLoading()
	{
		return BestRegionLoader.somebodyLoading;
	}

	/// <summary>Tell the teleport the world isn't in yet (it looks after every Update; the loader works it out afresh each frame).</summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void KeepLoaderWaiting()
	{
		BestRegionLoader.fullyLoaded = false;
	}

	private void ResumeFirstPerson()
	{
		if (_resumeFirstPerson)
		{
			_resumeFirstPerson = false;
			GameBridge.ResumeFirstPerson();
		}
	}

	private void Warn(string message)
	{
		if (_errors++ < 3)
		{
			_ctx.Log.Warning("Saved locations: " + message);
		}
	}

	// The game calls below are kept out of line so that, if a game update breaks these interop
	// types, the failure surfaces inside the callers' try blocks.

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool HipPosition(out Vector3 position)
	{
		PlayerMovement pm = PlayerMovement.me;
		Transform hip = (pm != null) ? pm.hipPivot : null;
		if (hip == null)
		{
			position = default;
			return false;
		}
		position = hip.position;
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static string GameBlocker()
	{
		Menu menu = Menu.me;
		if (menu == null)
		{
			return "the game isn't ready";
		}
		if (menu.paused || menu.loading || menu.restartingScene)
		{
			return "the game's menu is open";
		}
		if (menu.teleporting)
		{
			return "Nate is already on his way somewhere";
		}
		if (menu.inCutscene || menu.inNonInteractiveCutscene)
		{
			return "not during a cutscene";
		}
		if (menu.inDream || menu.waitingToSleep)
		{
			return "not while Nate's sleeping";
		}
		if (menu.playingIntro)
		{
			return "not during the intro";
		}
		PlayerMovement pm = PlayerMovement.me;
		if (pm == null)
		{
			return "Nate isn't here yet";
		}
		if (pm.inCutscene)
		{
			return "not during a cutscene";
		}
		// Down after a fall is fine: the teleport stands him up where it takes him.
		return null;
	}

	/// <summary>Nate is down after a fall (PlayerMovement.dead), as far as can be told.</summary>
	private bool NateDown()
	{
		try
		{
			return Down();
		}
		catch (Exception ex)
		{
			Warn("can't tell whether Nate is down: " + ex.Message);
			return false;
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool Down()
	{
		PlayerMovement pm = PlayerMovement.me;
		return pm != null && pm.dead;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool ReadPose(out SavedPose pose)
	{
		PlayerMovement pm = PlayerMovement.me;
		if (pm == null)
		{
			pose = default;
			return false;
		}
		pose = new SavedPose(pm.hipAng, pm.camAng, pm.camRight);
		ReadFeet(pm, ref pose);
		if (pose.Feet != 0)
		{
			pose.Body = ReadBody(pm);
		}
		return true;
	}

	/// <summary>
	/// Nate's whole body, from his hips' pivot: the animated character's root, the ragdoll's root
	/// and every ragdoll part (the PuppetMaster's muscles), places and turns. Kept only while he
	/// stands (a foot planted, not down): it's what going back puts him into.
	/// </summary>
	private static SavedBody ReadBody(PlayerMovement pm)
	{
		Transform hip = pm.hipPivot;
		Animator anim = pm.anim;
		PuppetMaster puppet = pm.pm;
		Il2CppReferenceArray<Muscle> muscles = ReferenceEquals(puppet, null) ? null : puppet.muscles;
		if (hip == null || anim == null || puppet == null || ReferenceEquals(muscles, null) || muscles.Length == 0 || muscles.Length > 64)
		{
			return null;
		}
		Vector3 hips = hip.position;
		Transform root = anim.transform;
		SavedBody body = new SavedBody
		{
			Root = From(hips, root.position),
			RootTurn = root.rotation,
			Puppet = From(hips, puppet.transform.position),
			Parts = new Vector3[muscles.Length],
			PartTurns = new Quaternion[muscles.Length]
		};
		for (int i = 0; i < muscles.Length; i++)
		{
			Muscle muscle = muscles[i];
			Rigidbody part = ReferenceEquals(muscle, null) ? null : muscle._rigidbody_k__BackingField;
			if (ReferenceEquals(part, null))
			{
				return null;
			}
			body.Parts[i] = From(hips, part.position);
			body.PartTurns[i] = part.rotation;
			if (Fast.Abs(body.Parts[i].x) > 3f || Fast.Abs(body.Parts[i].y) > 3f || Fast.Abs(body.Parts[i].z) > 3f)
			{
				// A part metres from his hips: not a body to stand him back into.
				return null;
			}
		}
		return body;
	}

	private static Vector3 From(Vector3 origin, Vector3 at)
	{
		return Fast.V3(at.x - origin.x, at.y - origin.y, at.z - origin.z);
	}

	/// <summary>
	/// Where Nate's planted feet are (their targets, <c>FootData.targetPos</c>: where the game's
	/// teleport plants them too), from his hips (<c>hipPivot</c>, the saved position). None while
	/// he's down (the spots his feet last had are stale then), none for a foot that isn't planted,
	/// and none for one implausibly far from his hips.
	/// </summary>
	private static void ReadFeet(PlayerMovement pm, ref SavedPose pose)
	{
		if (pm.dead)
		{
			return;
		}
		Transform hip = pm.hipPivot;
		Il2CppReferenceArray<FootData> feet = pm.feet;
		if (hip == null || ReferenceEquals(feet, null))
		{
			return;
		}
		Vector3 hips = hip.position;
		for (int i = 0; i < 2 && i < feet.Length; i++)
		{
			FootData foot = feet[i];
			if (ReferenceEquals(foot, null) || !foot.planted)
			{
				continue;
			}
			Transform target = foot.targetTrans;
			if (target == null)
			{
				continue;
			}
			Vector3 offset = From(hips, target.position);
			if (offset.y > MaxFootAboveHip || offset.y < -MaxFootBelowHip || offset.x * offset.x + offset.z * offset.z > MaxFootSpread * MaxFootSpread)
			{
				continue;
			}
			pose.SetFoot(i, offset);
			pose.SetFootTurn(i, target.rotation);
		}
	}

	/// <summary>
	/// Turn Nate's body (and the camera) the saved way, once the teleport has switched his ragdoll
	/// off: true when done (or nothing to turn), false to try again next frame. Fields only, read
	/// and written straight on the game's objects, except the one transform turn.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool TurnOnceRagdollOff(SavedPose pose)
	{
		PlayerMovement pm = PlayerMovement.me;
		if (ReferenceEquals(pm, null))
		{
			return true;
		}
		PuppetMaster puppet = pm.pm;
		if (!ReferenceEquals(puppet, null) && puppet.mode != PuppetMaster.Mode.Disabled)
		{
			return false;
		}
		pm.hipAng = pose.Facing;
		Animator anim = pm.anim;
		if (!ReferenceEquals(anim, null))
		{
			anim.transform.rotation = Yaw(pose.Facing);
		}
		SetCamera(pm, pose);
		return true;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void SetCamera(SavedPose pose)
	{
		PlayerMovement pm = PlayerMovement.me;
		if (!ReferenceEquals(pm, null))
		{
			SetCamera(pm, pose);
		}
	}

	private static void SetCamera(PlayerMovement pm, SavedPose pose)
	{
		pm.camAng = pose.Camera;
		pm.camRight = pose.CameraRight;
		pm.camRightAmount = pose.CameraRight ? 1f : 0f;
	}

	/// <summary>A turn of <paramref name="degrees"/> about the vertical: Quaternion.Euler(0, degrees, 0), made here.</summary>
	private static Quaternion Yaw(float degrees)
	{
		float half = degrees * Fast.Deg2Rad * 0.5f;
		Quaternion q = default;
		q.y = Fast.Sin(half);
		q.w = Fast.Cos(half);
		return q;
	}

	/// <summary>
	/// Start the game's teleport. True if it did (it marks itself as teleporting straight away).
	/// <paramref name="keepHeight"/>: keep the destination's height unless it's under a metre above
	/// the ground below (the teleport's <c>dead</c> flag; it changes nothing else), rather than
	/// putting the hips a metre above the first ground under the spot.
	/// </summary>
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool Teleport(Vector3 destination, bool keepHeight)
	{
		Menu menu = Menu.me;
		if (menu == null)
		{
			return false;
		}
		menu.Teleport(destination, keepHeight);
		return menu.teleporting;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool Teleporting()
	{
		Menu menu = Menu.me;
		return menu != null && menu.teleporting;
	}
}
