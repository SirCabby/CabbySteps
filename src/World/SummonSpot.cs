using System.Collections.Generic;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// What finding a place for a summoned item needs to know about the world round Nate: the game's
/// physics in play (<see cref="Summoner"/>), a made-up world in the offline checks.
/// </summary>
public interface ISpotProbe
{
	/// <summary>Something solid (the ground, a rock, a wall) is on the straight line from a to b.</summary>
	bool Blocked(Vector3 a, Vector3 b);

	/// <summary>
	/// The first solid ground straight down from <paramref name="from"/>, at most
	/// <paramref name="depth"/> below it, and how level it is there (the y of its normal: 1 is flat).
	/// </summary>
	bool Ground(Vector3 from, float depth, out Vector3 point, out float level);

	/// <summary>Anything solid (ground, props, items, Nate) is inside the box.</summary>
	bool Occupied(Vector3 centre, Vector3 halfSize);
}

/// <summary>
/// Where to put an item brought to Nate: on open, fairly level ground just beside him. In front
/// of him if there's room (where the camera looks), else round the sides, behind him last; then
/// the same a little further out. A spot has to be in plain sight of him (nothing solid on the line
/// from his hips), have ground under all of the item with no edge or step in it and no drop below
/// him, have room for the item, and keep clear of where other items were just put. Failing all
/// that, the first spot in sight of him with ground under its middle will do.
/// </summary>
public static class SummonSpot
{
	/// <summary>Space (m) between Nate's middle and the near side of the item.</summary>
	public const float Gap = 0.9f;

	/// <summary>How much further out (m) the second round of spots is.</summary>
	public const float RingStep = 0.8f;

	/// <summary>The ground has to be within this far (m) below Nate's hips: nothing goes down a drop.</summary>
	public const float MaxDrop = 2.4f;

	/// <summary>The steepest ground an item is put on, as the y of its normal (0.7 is about 45 degrees).</summary>
	public const float MinLevel = 0.7f;

	/// <summary>The biggest step (m) in the ground under an item; more is an edge or a rock.</summary>
	public const float MaxStep = 0.25f;

	/// <summary>Space (m) left between the ground and the bottom of the item.</summary>
	public const float Lift = 0.02f;

	/// <summary>Space (m) above the ground from which the item's room is checked: clear of grass-high bumps between the points sampled.</summary>
	public const float Clearance = 0.04f;

	/// <summary>Directions tried, in degrees from the way Nate faces (positive to his right): in front first, behind last.</summary>
	private static readonly float[] Angles = { 0f, 35f, -35f, 70f, -70f, 110f, -110f, 150f, -150f, 180f };

	/// <summary>
	/// A spot beside Nate for an item <paramref name="radius"/> m across from its middle and
	/// <paramref name="height"/> m tall. <paramref name="nate"/> is his hips, <paramref name="facing"/>
	/// the way he faces (level, unit length), <paramref name="taken"/> where items were just put.
	/// <paramref name="spot"/> is the ground under the middle of the item, at the height of the
	/// highest ground under it. False if no spot near him will do.
	/// </summary>
	public static bool Find(ISpotProbe probe, Vector3 nate, Vector3 facing, float radius, float height, IReadOnlyList<Vector3> taken, out Vector3 spot)
	{
		for (int pass = 0; pass < 2; pass++)
		{
			bool strict = pass == 0;
			for (int ring = 0; ring < 2; ring++)
			{
				float distance = Gap + radius + ring * RingStep;
				foreach (float angle in Angles)
				{
					Vector3 dir = Turn(facing, angle);
					Vector3 at = Fast.V3(nate.x + dir.x * distance, nate.y, nate.z + dir.z * distance);
					if (Fits(probe, nate, at, radius, height, taken, strict, out spot))
					{
						return true;
					}
				}
			}
		}
		spot = default;
		return false;
	}

	/// <summary>Will the item fit at <paramref name="at"/> (level with Nate's hips)? Not <paramref name="strict"/>: only in sight of him, with ground under its middle.</summary>
	private static bool Fits(ISpotProbe probe, Vector3 nate, Vector3 at, float radius, float height, IReadOnlyList<Vector3> taken, bool strict, out Vector3 spot)
	{
		spot = default;
		if (probe.Blocked(nate, at))
		{
			return false;
		}
		if (!probe.Ground(at, MaxDrop, out Vector3 ground, out float level))
		{
			return false;
		}
		float apart = Fast.Max(0.8f, radius * 2f + 0.3f);
		foreach (Vector3 other in taken)
		{
			float dx = other.x - at.x;
			float dz = other.z - at.z;
			if (dx * dx + dz * dz < apart * apart)
			{
				return false;
			}
		}
		if (!strict)
		{
			spot = ground;
			return true;
		}
		if (level < MinLevel)
		{
			return false;
		}
		// The ground under the item's footprint, a little in from its edges: no edge, no rock in it.
		float top = ground.y;
		float r = radius * 0.8f;
		for (int i = 0; i < 4; i++)
		{
			float ox = (i == 0) ? r : ((i == 1) ? (0f - r) : 0f);
			float oz = (i == 2) ? r : ((i == 3) ? (0f - r) : 0f);
			if (!probe.Ground(Fast.V3(at.x + ox, at.y, at.z + oz), MaxDrop, out Vector3 under, out float _) || Fast.Abs(under.y - ground.y) > MaxStep)
			{
				return false;
			}
			top = Fast.Max(top, under.y);
		}
		if (probe.Occupied(Fast.V3(at.x, top + Clearance + height * 0.5f, at.z), Fast.V3(radius, height * 0.5f, radius)))
		{
			return false;
		}
		spot = Fast.V3(at.x, top, at.z);
		return true;
	}

	/// <summary><paramref name="v"/> turned <paramref name="degrees"/> about the vertical, clockwise seen from above (as Unity's yaw turns).</summary>
	public static Vector3 Turn(Vector3 v, float degrees)
	{
		float a = degrees * Fast.Deg2Rad;
		float cos = Fast.Cos(a);
		float sin = Fast.Sin(a);
		return Fast.V3(v.x * cos + v.z * sin, v.y, v.z * cos - v.x * sin);
	}
}
