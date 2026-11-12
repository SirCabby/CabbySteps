using UnityEngine;

namespace CabbySteps;

/// <summary>
/// The Baby Steps world loops east-west: walk far enough sideways and you arrive back
/// where you started. The game also shifts everything by whole loops now and then to
/// keep coordinates small, so a raw x can be in any copy of the world. Canonical x is
/// [0, Width); every distance and direction here takes the short way round.
/// </summary>
public static class WorldLoop
{
	/// <summary>Loop width in metres (4 chunks of 128 m): the game's own map wraps x by this too (SaveGod.SaveForever).</summary>
	public static float Width = 512f;

	public static float WrapX(float x)
	{
		float wrapped = x % Width;
		if (wrapped < 0f)
		{
			wrapped += Width;
		}
		return wrapped;
	}

	/// <summary>Signed x distance from fromX to toX the short way round, in [-Width/2, Width/2].</summary>
	public static float DeltaX(float fromX, float toX)
	{
		float d = (toX - fromX) % Width;
		if (d > Width * 0.5f)
		{
			d -= Width;
		}
		else if (d < -Width * 0.5f)
		{
			d += Width;
		}
		return d;
	}

	/// <summary>Vector from one world position to another, the short way round.</summary>
	public static Vector3 Delta(Vector3 from, Vector3 to)
	{
		return Fast.V3(DeltaX(from.x, to.x), to.y - from.y, to.z - from.z);
	}

	/// <summary>Horizontal (x/z) distance, the short way round.</summary>
	public static float FlatDistance(Vector3 a, Vector3 b)
	{
		Vector3 d = Delta(a, b);
		return Fast.Sqrt(d.x * d.x + d.z * d.z);
	}

	/// <summary>
	/// The copy of <paramref name="pos"/> nearest <paramref name="aroundX"/>: the same place, in
	/// whichever loop the game has the world in round there (the game's own
	/// ObjectLooper.LoopThisPosAround does the same, in steps of 512 m).
	/// </summary>
	public static Vector3 NearestCopy(Vector3 pos, float aroundX)
	{
		return Fast.V3(aroundX + DeltaX(aroundX, pos.x), pos.y, pos.z);
	}
}
