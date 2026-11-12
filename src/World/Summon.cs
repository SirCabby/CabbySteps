using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Brings a movable item (a hat, a key or quest item, a chick or duckling) to Nate, and puts it
/// down on open ground beside him (<see cref="SummonSpot"/>).
/// <para>
/// How the game keeps these items, read from its code: each one has a loader
/// (<c>GlobalObjectLoader</c>, under BigManagerPrefab/GlobalObjectParent/Savables) that stands
/// where the item is. <c>ObjectLooper.LoopList</c> loads the item (an Addressables instance, made
/// a child of the loader) once the world round the loader has loaded, and unloads it when the
/// world round the item goes, moving the loader to where the item was. On loading, the item goes
/// where the loader is, then where the save last had it (<c>SaveFile.objTs</c>, by the loader's
/// <c>loadKey</c>), if anywhere.
/// </para>
/// <para>
/// So a loaded item is moved itself, the way the developers' own cheat
/// (<c>DevCheatsGoHere.BringForthGrabable</c>) does it: switched off, moved, switched on again,
/// then the physics scene synced. Its physics starts afresh there, with nothing drawn in between;
/// its animations carry on where they were (a chick would otherwise forget it had woken up). An
/// unloaded one has its loader, and its place in the save, moved beside Nate; the game loads it
/// there, and it's put down properly once it has. Either way it arrives still, standing on the
/// ground. A loader riding a turtle (<c>LoaderFollowTurtle</c>) stops riding it. An item whose
/// loader the game hasn't switched on (the Winter Hat's starts off) isn't out in the world yet,
/// so it can't be brought.
/// </para>
/// <para>
/// The game saves where items are as usual, though only once they've been picked up
/// (<c>Grabable.beenTouched</c>): one that's never been picked up goes back to its own place when
/// the game is loaded again, as the game's untouched items always do. Chicks and ducklings are
/// always saved. It all works with the map open and the game frozen: the game keeps loading.
/// </para>
/// </summary>
public class Summoner
{
	private sealed class Job
	{
		public Poi Poi;

		public GlobalObjectLoader Loader;

		/// <summary>Times the loader has been moved to Nate (the item wasn't loaded).</summary>
		public int Moves;

		public float Started;

		/// <summary>"Bringing ..." was said.</summary>
		public bool Announced;

		/// <summary>The loader was found switched on.</summary>
		public bool Checked;
	}

	/// <summary>Give up waiting for the game to load an item after this many seconds.</summary>
	private const float Timeout = 20f;

	/// <summary>For this many seconds a spot an item was put in is kept clear of the next ones.</summary>
	private const float TakenFor = 10f;

	/// <summary>Move an unloaded item's loader to Nate at most this many times (the game might move it back).</summary>
	private const int MaxMoves = 3;

	/// <summary>An unloaded item's loader this far (m) from Nate hasn't been moved to him, or has been put back.</summary>
	private const float AwaySq = 8f * 8f;

	/// <summary>Size (m) assumed for an item that isn't loaded yet, from its middle across, and tall.</summary>
	private const float GuessRadius = 0.3f;

	private const float GuessHeight = 0.5f;

	/// <summary>A spot an item was just put in, or is being loaded in: kept clear of the next ones for a while (the physics can't see an item that's still loading).</summary>
	private sealed class TakenSpot
	{
		public Vector3 At;

		public float Since;

		/// <summary>The item on its way there, or null once it has arrived.</summary>
		public Job By;
	}

	private readonly ModContext _ctx;

	private readonly List<Job> _jobs = new List<Job>();

	private readonly List<TakenSpot> _taken = new List<TakenSpot>();

	/// <summary>The taken spots another item has to keep clear of, gathered for each search.</summary>
	private readonly List<Vector3> _keepClear = new List<Vector3>();

	private PhysicsProbe _probe;

	private int _errors;

	public Summoner(ModContext ctx)
	{
		_ctx = ctx;
	}

	/// <summary>Some item is on its way to Nate.</summary>
	public bool Busy => _jobs.Count > 0;

	/// <summary>The item on its way, if one is (for "wait until ... has turned up").</summary>
	public Poi Current => (_jobs.Count > 0) ? _jobs[0].Poi : null;

	/// <summary>Ask for a movable item to be brought to Nate. Null if it will happen, else why not.</summary>
	public string Request(Poi poi)
	{
		if (poi == null || poi.Item == null)
		{
			return "it isn't something that can be carried";
		}
		if (_ctx.Progress.IsGone(poi))
		{
			return "it has left the game";
		}
		foreach (Job job in _jobs)
		{
			if (job.Poi == poi)
			{
				return "it's on its way already";
			}
		}
		if (_ctx.Recall.Busy)
		{
			return "Nate's on his way somewhere";
		}
		string why = _ctx.Recall.Blocker();
		if (why != null)
		{
			return why;
		}
		if (!_ctx.Recall.TryGetNatePosition(out Vector3 _))
		{
			return "Nate isn't here yet";
		}
		Job added;
		try
		{
			added = Start(poi);
		}
		catch (Exception ex)
		{
			_ctx.Log.Error("Summon: can't reach " + poi.Title + " in the game: " + ex);
			return "the mod can't reach it in the game (see the log)";
		}
		if (added == null)
		{
			// No loader: no save loaded yet, or a game update renamed it.
			return "the game hasn't placed it yet";
		}
		Step(added);
		return null;
	}

	/// <summary>Every frame: carry on with items on their way.</summary>
	public void Update()
	{
		for (int i = _jobs.Count - 1; i >= 0; i--)
		{
			if (i < _jobs.Count)
			{
				Step(_jobs[i]);
			}
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private Job Start(Poi poi)
	{
		GlobalObjectLoader loader = _ctx.Links.Loader(poi.Item);
		if (loader == null)
		{
			return null;
		}
		Job job = new Job
		{
			Poi = poi,
			Loader = loader,
			Started = Frame.Now
		};
		_jobs.Add(job);
		return job;
	}

	/// <summary>One frame of an item's trip; it's taken off the list once it has arrived or can't.</summary>
	private void Step(Job job)
	{
		try
		{
			Advance(job);
		}
		catch (Exception ex)
		{
			if (_errors++ < 5)
			{
				_ctx.Log.Error("Summon: moving " + job.Poi.Title + " failed: " + ex);
			}
			Finish(job, "Can't bring " + job.Poi.Title + ": the game wouldn't move it (see the log)");
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void Advance(Job job)
	{
		GlobalObjectLoader loader = job.Loader;
		string title = job.Poi.Title;
		if (!job.Checked)
		{
			// Asked once: the game keeps some switched off until they're meant to be found.
			job.Checked = true;
			if (loader == null || !loader.gameObject.activeInHierarchy)
			{
				Finish(job, "Can't bring " + title + ": the game hasn't put it out yet");
				return;
			}
		}
		// A field, so no call into the game: it can be read every frame.
		LoadState state = loader.loadstate;
		bool act = state == LoadState.loaded || (state == LoadState.unloaded && NeedsMove(job));
		bool late = Frame.Now - job.Started > Timeout;
		// While it's only waiting, the checks below (calls into the game) every few frames are enough.
		if (!act && !late && Frame.Count % 6 != 0)
		{
			return;
		}
		if (loader == null)
		{
			// Unity's null: destroyed with its scene.
			Finish(job, "Can't bring " + title + ": the game has reloaded");
			return;
		}
		// Nothing moves while the game is busy with Nate (a cutscene, a teleport, his menu).
		string why = _ctx.Recall.Blocker();
		if (why != null)
		{
			if (late)
			{
				Finish(job, "Can't bring " + title + ": " + why);
			}
			return;
		}
		if (state == LoadState.loaded)
		{
			Arrive(job);
			return;
		}
		if (act)
		{
			if (job.Moves >= MaxMoves)
			{
				Finish(job, "Can't bring " + title + ": the game keeps putting it back");
				return;
			}
			MoveLoader(job);
			if (!job.Announced)
			{
				job.Announced = true;
				_ctx.Toasts.Show("Bringing " + title + " to Nate...");
			}
			return;
		}
		// Waiting for the game to load it where its loader now is, or to finish loading or unloading it.
		if (late)
		{
			_ctx.Log.Warning($"Summon: the game still hasn't loaded {title} after {Timeout:0} s (loader {loader.gameObject.name}, {state}).");
			Finish(job, (job.Moves > 0) ? (title + " should turn up by Nate once the game has loaded it") : ("Can't bring " + title + ": the game is still busy with it, try again"));
		}
	}

	/// <summary>An unloaded item's loader isn't with Nate: not moved yet, or the game moved it back.</summary>
	private bool NeedsMove(Job job)
	{
		if (job.Moves == 0)
		{
			return true;
		}
		// Asking where the loader is is a call into the game: not every frame. A loader gone with
		// its scene is noticed by the caller.
		if (Frame.Count % 10 != 0 || job.Loader == null || !_ctx.Recall.TryGetNatePosition(out Vector3 nate))
		{
			return false;
		}
		Vector3 d = WorldLoop.Delta(nate, job.Loader.transform.position);
		return d.x * d.x + d.y * d.y + d.z * d.z > AwaySq;
	}

	/// <summary>The item isn't loaded: move its loader, and its place in the save, beside Nate. The game loads it there.</summary>
	private void MoveLoader(Job job)
	{
		GlobalObjectLoader loader = job.Loader;
		Vector3 spot = FindSpot(GuessRadius, GuessHeight, job, out bool found);
		// The item's real size is known once it has loaded, and it's put down properly then.
		Vector3 position = found ? Fast.V3(spot.x, spot.y + SummonSpot.Lift + GuessHeight * 0.5f, spot.z) : spot;
		StopRiding(loader);
		Transform t = loader.transform;
		Vector3 from = t.position;
		t.position = position;
		bool saved = MoveSavedPosition(loader.loadKey, position);
		job.Moves++;
		_ctx.Log.Msg($"Summon: {job.Poi.Title} isn't loaded; moved its loader {loader.gameObject.name} from ({from.x:0.0}, {from.y:0.0}, {from.z:0.0}) to ({position.x:0.0}, {position.y:0.0}, {position.z:0.0}){(saved ? ", and its place in the save" : "")}.");
	}

	/// <summary>The item is loaded: put it down beside Nate, unless he has it.</summary>
	private void Arrive(Job job)
	{
		GlobalObjectLoader loader = job.Loader;
		string title = job.Poi.Title;
		GameObject item = loader.loadedInstance;
		if (item == null)
		{
			return;
		}
		if (!item.activeSelf)
		{
			// Switched off by the game: used up for good (handed in, broken), or not out yet (the
			// ice cream before Mitchell hands it over, or melted: he has the next one).
			Finish(job, "Can't bring " + title + ": it isn't out in the world just now");
			return;
		}
		Savable savable = loader.loadedSavable;
		Grabable grabable = (savable != null) ? savable.TryCast<Grabable>() : null;
		if (grabable != null && grabable.heldState != HeldState.none)
		{
			Finish(job, (grabable.heldState == HeldState.head) ? (title + " is on Nate's head already") : (title + " is in Nate's hands already"));
			return;
		}
		Duckling duckling = (savable != null) ? savable.TryCast<Duckling>() : null;
		StopRiding(loader);
		Vector3 to = PutDown(job, item, savable, grabable, duckling);
		_ctx.Log.Msg($"Summon: put {title} ({loader.gameObject.name}) down by Nate at ({to.x:0.0}, {to.y:0.0}, {to.z:0.0}){((job.Moves > 0) ? " once the game had loaded it" : "")}.");
		string message = title + " is with Nate";
		if (_ctx.Target == job.Poi)
		{
			// Fetching it is what tracking it was for.
			_ctx.Target = null;
			message += "  (tracking cleared)";
		}
		Finish(job, message);
	}

	/// <summary>
	/// Move the loaded item onto open ground beside Nate, standing on it, and still. Like the
	/// developers' cheat, it's switched off while it moves, so the physics takes it as put there
	/// rather than flung there. Returns where it went (its pivot).
	/// </summary>
	private Vector3 PutDown(Job job, GameObject item, Savable savable, Grabable grabable, Duckling duckling)
	{
		Transform t = item.transform;
		Vector3 pivot = t.position;
		// Its size and where its pivot sits in it, from its colliders, while they're still on.
		Il2CppArrayBase<Collider> colliders = (grabable != null) ? grabable.colls : ((duckling != null) ? duckling.colls : null);
		if (colliders == null || colliders.Length == 0)
		{
			colliders = item.GetComponentsInChildren<Collider>();
		}
		Measure(colliders, pivot, out float radius, out float height, out float below, out float offsetX, out float offsetZ);
		// Switching it off would reset its animations (a chick's would forget it had woken up).
		Il2CppArrayBase<Animator> animators = item.GetComponentsInChildren<Animator>();
		bool[] changed = KeepAnimations(animators);
		Vector3 to;
		item.SetActive(false);
		try
		{
			// Switched off, its colliders can't get in the way of finding it a spot.
			Vector3 spot = FindSpot(radius, height, job, out bool found);
			to = found ? Fast.V3(spot.x - offsetX, spot.y + SummonSpot.Lift + below, spot.z - offsetZ) : spot;
			t.position = to;
		}
		finally
		{
			item.SetActive(true);
			RestoreAnimations(animators, changed);
		}
		Vector3 still = default;
		if (savable != null)
		{
			// What the game gives back to the body when its physics comes on again.
			savable.lastVelocity = still;
			Still(savable.rb);
		}
		if (grabable != null)
		{
			Il2CppReferenceArray<Rigidbody> bodies = grabable.rbs;
			if (bodies != null)
			{
				foreach (Rigidbody body in bodies)
				{
					Still(body);
				}
			}
		}
		if (duckling != null)
		{
			// Where its last hop started: it judges how hard to hop next from it.
			duckling.lastJumpPos = to;
		}
		Physics.SyncTransforms();
		return to;
	}

	/// <summary>Have the animators keep their state while the item is switched off. True where that changed one, to be put back after.</summary>
	private static bool[] KeepAnimations(Il2CppArrayBase<Animator> animators)
	{
		if (animators == null || animators.Length == 0)
		{
			return null;
		}
		bool[] changed = new bool[animators.Length];
		for (int i = 0; i < animators.Length; i++)
		{
			Animator animator = animators[i];
			if (animator != null && !animator.keepAnimatorStateOnDisable)
			{
				animator.keepAnimatorStateOnDisable = true;
				changed[i] = true;
			}
		}
		return changed;
	}

	private static void RestoreAnimations(Il2CppArrayBase<Animator> animators, bool[] changed)
	{
		if (changed == null)
		{
			return;
		}
		for (int i = 0; i < changed.Length && i < animators.Length; i++)
		{
			Animator animator = animators[i];
			if (changed[i] && animator != null)
			{
				animator.keepAnimatorStateOnDisable = false;
			}
		}
	}

	private static void Still(Rigidbody body)
	{
		if (body == null || body.isKinematic)
		{
			return;
		}
		Vector3 zero = default;
		body.velocity = zero;
		body.angularVelocity = zero;
	}

	/// <summary>
	/// An item's size (<paramref name="radius"/> across from its middle, <paramref name="height"/>
	/// tall), how far its pivot is above its bottom, and how far its middle is from its pivot,
	/// from the bounds of its solid colliders. A guess if it has none.
	/// </summary>
	private static void Measure(Il2CppArrayBase<Collider> colliders, Vector3 pivot, out float radius, out float height, out float below, out float offsetX, out float offsetZ)
	{
		bool any = false;
		float minX = 0f, minY = 0f, minZ = 0f, maxX = 0f, maxY = 0f, maxZ = 0f;
		if (colliders != null)
		{
			foreach (Collider collider in colliders)
			{
				if (collider == null || !collider.enabled || collider.isTrigger)
				{
					continue;
				}
				Bounds b = collider.bounds;
				Vector3 c = b.m_Center;
				Vector3 e = b.m_Extents;
				if (e.x <= 0f && e.y <= 0f && e.z <= 0f)
				{
					continue;
				}
				if (!any)
				{
					any = true;
					minX = c.x - e.x;
					minY = c.y - e.y;
					minZ = c.z - e.z;
					maxX = c.x + e.x;
					maxY = c.y + e.y;
					maxZ = c.z + e.z;
					continue;
				}
				minX = Fast.Min(minX, c.x - e.x);
				minY = Fast.Min(minY, c.y - e.y);
				minZ = Fast.Min(minZ, c.z - e.z);
				maxX = Fast.Max(maxX, c.x + e.x);
				maxY = Fast.Max(maxY, c.y + e.y);
				maxZ = Fast.Max(maxZ, c.z + e.z);
			}
		}
		if (!any)
		{
			radius = 0.25f;
			height = 0.4f;
			below = 0.2f;
			offsetX = 0f;
			offsetZ = 0f;
			return;
		}
		radius = Fast.Clamp(Fast.Max(maxX - minX, maxZ - minZ) * 0.5f, 0.08f, 0.8f);
		height = Fast.Clamp(maxY - minY, 0.05f, 1.6f);
		below = Fast.Clamp(pivot.y - minY, -2f, 2f);
		offsetX = (minX + maxX) * 0.5f - pivot.x;
		offsetZ = (minZ + maxZ) * 0.5f - pivot.z;
	}

	/// <summary>
	/// A spot beside Nate for an item that size, kept clear of the next ones. If there's no good
	/// one (<paramref name="found"/> false), where the developers' cheat puts things: in front of
	/// him at the height of his hips, to drop from there.
	/// </summary>
	private Vector3 FindSpot(float radius, float height, Job job, out bool found)
	{
		if (!_ctx.Recall.TryGetNatePosition(out Vector3 nate))
		{
			throw new InvalidOperationException("Nate's position is unknown");
		}
		Vector3 facing = _ctx.Player.Found ? _ctx.Player.Forward : Fast.V3(0f, 0f, 1f);
		float now = Frame.Now;
		_taken.RemoveAll(t => t.By == null && now - t.Since > TakenFor);
		_keepClear.Clear();
		foreach (TakenSpot taken in _taken)
		{
			// Its own spot (taken when its loader was moved) doesn't count against it.
			if (taken.By != job)
			{
				_keepClear.Add(taken.At);
			}
		}
		_probe ??= new PhysicsProbe();
		found = SummonSpot.Find(_probe, nate, facing, radius, height, _keepClear, out Vector3 spot);
		if (!found)
		{
			float ahead = SummonSpot.Gap + radius;
			spot = Fast.V3(nate.x + facing.x * ahead, nate.y, nate.z + facing.z * ahead);
			_ctx.Log.Msg("Summon: no open ground beside Nate; putting " + job.Poi.Title + " in front of him.");
		}
		_taken.RemoveAll(t => t.By == job);
		_taken.Add(new TakenSpot
		{
			At = spot,
			Since = now,
			By = job
		});
		return spot;
	}

	private void Finish(Job job, string message)
	{
		_jobs.Remove(job);
		foreach (TakenSpot taken in _taken)
		{
			if (taken.By == job)
			{
				// The item is sitting there now: the spot stays taken a while longer.
				taken.By = null;
				taken.Since = Frame.Now;
			}
		}
		if (message != null)
		{
			_ctx.Toasts.Show(message);
		}
	}

	/// <summary>A loader riding a turtle follows it until the item's been picked up; one brought to Nate stays with him.</summary>
	private void StopRiding(GlobalObjectLoader loader)
	{
		LoaderFollowTurtle ride = loader.GetComponent<LoaderFollowTurtle>();
		if (ride != null && ride.enabled)
		{
			ride.enabled = false;
			_ctx.Log.Msg("Summon: " + loader.gameObject.name + " no longer rides its turtle.");
		}
	}

	/// <summary>Where the save has the item, if it has it, moves too: the item goes there when it loads.</summary>
	private static bool MoveSavedPosition(string key, Vector3 position)
	{
		SaveFile save = SaveGod.theSave;
		if (save == null || string.IsNullOrEmpty(key))
		{
			return false;
		}
		var places = save.objTs;
		if (places == null || !places.ContainsKey(key))
		{
			return false;
		}
		SavePosData place = places[key];
		if (place == null)
		{
			return false;
		}
		place.pos = position;
		return true;
	}

	/// <summary>The game's physics, asked with the game's own layers: what Nate stands on (as its teleport does), and what an item mustn't be put into.</summary>
	private sealed class PhysicsProbe : ISpotProbe
	{
		private readonly int _ground;

		private readonly int _solid;

		private readonly Il2CppReferenceArray<Collider> _hits = new Il2CppReferenceArray<Collider>(4);

		public PhysicsProbe()
		{
			// The ground without items lying on it: the game's teleport finds the ground with
			// PropTerrainMask, and items are on the dynamic props layer once picked up.
			_ground = LayerCache.PropTerrainNonDynamicMask.m_Mask;
			if (_ground == 0)
			{
				_ground = LayerCache.PropTerrainMask.m_Mask;
			}
			if (_ground == 0)
			{
				// The game's layer numbers (ConstGenConstants._LAYERS): Default, Terrain, Props, PropsNoCamera.
				_ground = 1 | (1 << 8) | (1 << 16) | (1 << 23);
			}
			// Everything solid: the ground, props, items (picked up or not) and Nate himself.
			_solid = _ground | LayerCache.PropTerrainMask.m_Mask | LayerCache.PropsMask.m_Mask | LayerCache.RagdollMask.m_Mask
				| (1 << LayerCache.PropsDynamicLayer) | (1 << LayerCache.PropsNoCameraLayer) | (1 << LayerCache.PropsLayer) | (1 << LayerCache.TerrainLayer) | 1;
		}

		public bool Blocked(Vector3 a, Vector3 b)
		{
			float dx = b.x - a.x;
			float dy = b.y - a.y;
			float dz = b.z - a.z;
			float length = Fast.Sqrt(dx * dx + dy * dy + dz * dz);
			if (length < 0.001f)
			{
				return false;
			}
			return Physics.Raycast(a, Fast.V3(dx / length, dy / length, dz / length), length, _ground, QueryTriggerInteraction.Ignore);
		}

		public bool Ground(Vector3 from, float depth, out Vector3 point, out float level)
		{
			if (Physics.Raycast(from, Fast.V3(0f, -1f, 0f), out RaycastHit hit, depth, _ground, QueryTriggerInteraction.Ignore))
			{
				point = hit.m_Point;
				level = hit.m_Normal.y;
				return true;
			}
			point = default;
			level = 0f;
			return false;
		}

		public bool Occupied(Vector3 centre, Vector3 halfSize)
		{
			Quaternion upright = default;
			upright.w = 1f;
			return Physics.OverlapBoxNonAlloc(centre, halfSize, _hits, upright, _solid, QueryTriggerInteraction.Ignore) > 0;
		}
	}
}
