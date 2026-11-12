namespace CabbySteps;

/// <summary>
/// The outline of Nate's planted feet on the ground, as the game keeps it for his balance
/// (<c>PlayerMovement.polySupport</c>, its first <c>polySupportLength</c> points, in x/z), and where
/// <see cref="GrippyFeet"/> counts his weight so that it's always over his feet. Game-free, checked
/// offline.
/// <para>
/// The game's own two tests are mirrored exactly, read from its machine code, so that "inside" here
/// is "inside" to the game:
/// <list type="bullet">
/// <item><c>InsidePolySup</c>: the point is on the left of every edge from point i to point i + 1, for
/// i up to n - 2: (b - a) × (p - a) ≥ 0 in x/z. There's no edge from the last point back to the
/// first (the game's outline repeats its first point at the end), and with fewer than two points
/// every point counts as inside.</item>
/// <item><c>ClosestPtOnPolySupport</c>: the nearest point of those same edges, in x/z
/// (<c>ClosestPtOnLineXZ</c>, the segment's ends included). It's on an edge even for a point inside.</item>
/// </list>
/// </para>
/// </summary>
public static class SupportPolygon
{
	/// <summary>The game's <c>InsidePolySup</c>: whether (px, pz) counts as over the first <paramref name="n"/> points' outline.</summary>
	public static bool Inside(float[] xs, float[] zs, int n, float px, float pz)
	{
		for (int i = 0; i < n - 1; i++)
		{
			int j = i + 1;
			float cross = (xs[j] - xs[i]) * (pz - zs[i]) - (px - xs[i]) * (zs[j] - zs[i]);
			if (cross < 0f)
			{
				return false;
			}
		}
		return true;
	}

	/// <summary>The game's <c>ClosestPtOnPolySupport</c> in x/z: the nearest point of the outline's edges to (px, pz). False with no edge (fewer than two points).</summary>
	public static bool Closest(float[] xs, float[] zs, int n, float px, float pz, out float cx, out float cz)
	{
		cx = px;
		cz = pz;
		bool found = false;
		float best = float.MaxValue;
		for (int i = 0; i < n - 1; i++)
		{
			int j = i + 1;
			float ex = xs[j] - xs[i];
			float ez = zs[j] - zs[i];
			float length2 = ex * ex + ez * ez;
			float t = (length2 > 1e-12f) ? Fast.Clamp01(((px - xs[i]) * ex + (pz - zs[i]) * ez) / length2) : 0f;
			float qx = xs[i] + ex * t;
			float qz = zs[i] + ez * t;
			float dx = qx - px;
			float dz = qz - pz;
			float d2 = dx * dx + dz * dz;
			if (d2 < best)
			{
				best = d2;
				cx = qx;
				cz = qz;
				found = true;
			}
		}
		return found;
	}

	/// <summary>
	/// Where to count Nate's weight so that it's over his feet. Unchanged (false) when (px, pz) is
	/// already inside the outline, or there's no outline to speak of. Otherwise (true) the nearest point
	/// of the outline, moved <paramref name="margin"/> towards the middle of his feet (at most halfway
	/// there): just inside the edge his weight went over, so the game's own tests see him balanced
	/// there, with no doubt left to rounding. If even the middle doesn't count as inside (an outline
	/// the game itself would never call him balanced on), it's left alone.
	/// </summary>
	public static bool Steady(float[] xs, float[] zs, int n, float px, float pz, float margin, out float x, out float z)
	{
		x = px;
		z = pz;
		if (n < 2 || Inside(xs, zs, n, px, pz) || !Closest(xs, zs, n, px, pz, out float cx, out float cz))
		{
			return false;
		}
		// The middle of his feet: the outline's points, its closing repeat of the first counted once.
		int count = n;
		if (n > 2 && xs[n - 1] == xs[0] && zs[n - 1] == zs[0])
		{
			count--;
		}
		float gx = 0f;
		float gz = 0f;
		for (int i = 0; i < count; i++)
		{
			gx += xs[i];
			gz += zs[i];
		}
		gx /= count;
		gz /= count;
		float dx = gx - cx;
		float dz = gz - cz;
		float d = Fast.Sqrt(dx * dx + dz * dz);
		if (d > 1e-6f)
		{
			float step = Fast.Min(margin, d * 0.5f);
			float tx = cx + dx / d * step;
			float tz = cz + dz / d * step;
			if (Inside(xs, zs, n, tx, tz))
			{
				x = tx;
				z = tz;
				return true;
			}
		}
		if (Inside(xs, zs, n, gx, gz))
		{
			x = gx;
			z = gz;
			return true;
		}
		return false;
	}
}
