using System;
using System.Collections.Generic;

namespace CabbySteps;

/// <summary>
/// The Hero's Journey as the game's ending draws it (<c>EndCredits</c>), worked out from the
/// game's machine code. Nothing here calls into the game, so it's checked offline.
/// <para>
/// <b>The record.</b> The game itself notes where Nate goes (<c>SaveGod.SaveForever</c>): every
/// 5 s or so, once he's moved a metre, two numbers into <c>SaveGod.positions</c>, written out as
/// <c>path&lt;slot&gt;_&lt;n&gt;.sav</c> (50000 numbers a file, zeros after the last). The first is
/// his x across the 512 m world loop at 63.999 a metre (its top bit: nothing above could see him),
/// the second how far up the mountain he is (half his height plus 0.866 of his z, wrapped at
/// 3631 m) at 13.107 a metre. The ending (<c>LoadAllPositions</c>) divides both by 512, skips
/// pairs whose second number is 0 (never written) and points that haven't moved, and so do we.
/// </para>
/// <para>
/// <b>The drawing.</b> In those units x runs 0..1 across and y up the painted map sheets. Each
/// stroke starts with a dot on its first point; then red dots (0.8, 0, 0) of radius 0.002 every
/// 0.01, each a step from the last dot towards the next point (the remainder carries on to the
/// next point, so corners are cut a little); a bigger dot (0.004) on the pen's tip, part way
/// along the segment being drawn. A step of more than 0.75 across is the world loop's seam: the
/// stroke ends with a big dot on its last point and a new one starts on the other side.
/// </para>
/// <para>
/// <b>The pace.</b> The pen moves on 13 points a second; with more than 8640 points, faster in
/// proportion, so no journey takes longer than 8640 would (11 minutes).
/// </para>
/// </summary>
public sealed class JourneyPath
{
	public const float XPerMetre = 63.999f;

	public const float YPerMetre = 13.107f;

	/// <summary>Metres per unit of the ending's drawing.</summary>
	public const float MetresPerUnit = 512f;

	public const float DotSpacing = 0.01f;

	public const float DotRadius = 0.002f;

	/// <summary>The pen's tip, and the end of a stroke at the seam.</summary>
	public const float BigDotRadius = 0.004f;

	public const float SeamJump = 0.75f;

	public const float PointsPerSecond = 13f;

	public const int PaceCount = 8640;

	private readonly List<float> _x = new List<float>();

	private readonly List<float> _y = new List<float>();

	// The dots of the whole path, in drawing order: where, big or not, and the segment they
	// belong to (segment s runs from point s - 1 to point s; a stroke's first dot belongs to the
	// segment that reaches its point, the very first point's to "segment 0", drawn from the start).
	private readonly List<float> _dotX = new List<float>();

	private readonly List<float> _dotY = new List<float>();

	private readonly List<bool> _dotBig = new List<bool>();

	private readonly List<int> _dotSegment = new List<int>();

	// Per segment s: the index of its first dot, whether it crosses the seam, and where the last
	// dot before it was (where its dots step from).
	private readonly List<int> _segmentFirstDot = new List<int>();

	private readonly List<bool> _segmentSeam = new List<bool>();

	private readonly List<float> _segmentFromX = new List<float>();

	private readonly List<float> _segmentFromY = new List<float>();

	public int Count => _x.Count;

	public int DotCount => _dotX.Count;

	public float X(int i)
	{
		return _x[i];
	}

	public float Y(int i)
	{
		return _y[i];
	}

	/// <summary>How many segments there are to draw: Count - 1, or 0.</summary>
	public int Segments => Math.Max(0, _x.Count - 1);

	/// <summary>Points a second the pen moves on (the ending's pace for this many points).</summary>
	public float Speed => PointsPerSecond * ((_x.Count > PaceCount) ? (_x.Count / (float)PaceCount) : 1f);

	public float DotX(int i)
	{
		return _dotX[i];
	}

	public float DotY(int i)
	{
		return _dotY[i];
	}

	public bool DotBig(int i)
	{
		return _dotBig[i];
	}

	public int DotSegment(int i)
	{
		return _dotSegment[i];
	}

	public void Clear()
	{
		_x.Clear();
		_y.Clear();
		ClearDots();
	}

	/// <summary>
	/// Add the points in the first <paramref name="count"/> numbers of <paramref name="raw"/>, one
	/// of the game's records (a file's or the live buffer). Call <see cref="BuildDots"/> after the last.
	/// </summary>
	public void Add(IReadOnlyList<ushort> raw, int count)
	{
		count = Math.Min(count, raw.Count);
		float lastX = (_x.Count > 0) ? _x[_x.Count - 1] : 0f;
		float lastY = (_y.Count > 0) ? _y[_y.Count - 1] : 0f;
		for (int i = 0; i + 1 < count; i += 2)
		{
			int up = raw[i + 1];
			if (up == 0)
			{
				continue;
			}
			float x = (raw[i] & 0x7FFF) / XPerMetre / MetresPerUnit;
			float y = up / YPerMetre / MetresPerUnit;
			// The ending keeps a point only if it's moved from the last one kept (from (0, 0) at first).
			float dx = x - lastX;
			float dy = y - lastY;
			if (dx * dx + dy * dy <= 0f)
			{
				continue;
			}
			_x.Add(x);
			_y.Add(y);
			lastX = x;
			lastY = y;
		}
	}

	/// <summary>Work out every dot of the finished drawing, stroke by stroke, as the ending would.</summary>
	public void BuildDots()
	{
		ClearDots();
		int n = _x.Count;
		if (n == 0)
		{
			return;
		}
		// Segment 0 doesn't exist; it's kept so segment s is at index s.
		_segmentFirstDot.Add(0);
		_segmentSeam.Add(false);
		_segmentFromX.Add(_x[0]);
		_segmentFromY.Add(_y[0]);
		AddDot(_x[0], _y[0], big: false, segment: 0);
		float lastX = _x[0];
		float lastY = _y[0];
		for (int s = 1; s < n; s++)
		{
			float ax = _x[s - 1];
			float ay = _y[s - 1];
			float bx = _x[s];
			float by = _y[s];
			bool seam = Math.Abs(bx - ax) > SeamJump;
			_segmentFirstDot.Add(_dotX.Count);
			_segmentSeam.Add(seam);
			if (seam)
			{
				// The stroke ends on the last point before the seam; a new one starts after it.
				_segmentFromX.Add(bx);
				_segmentFromY.Add(by);
				AddDot(ax, ay, big: true, segment: s);
				AddDot(bx, by, big: false, segment: s);
				lastX = bx;
				lastY = by;
				continue;
			}
			_segmentFromX.Add(lastX);
			_segmentFromY.Add(lastY);
			Step(ref lastX, ref lastY, bx, by, s);
		}
	}

	/// <summary>The index of segment <paramref name="segment"/>'s first dot (DotCount past the last).</summary>
	public int FirstDotOf(int segment)
	{
		if (segment < 1)
		{
			return 0;
		}
		return (segment < _segmentFirstDot.Count) ? _segmentFirstDot[segment] : _dotX.Count;
	}

	public bool IsSeam(int segment)
	{
		return segment >= 1 && segment < _segmentSeam.Count && _segmentSeam[segment];
	}

	/// <summary>Where the pen's tip is when segment <paramref name="segment"/> is <paramref name="along"/> (0..1) drawn.</summary>
	public void Tip(int segment, float along, out float x, out float y)
	{
		segment = Math.Clamp(segment, 1, Math.Max(1, _x.Count - 1));
		if (_x.Count < 2)
		{
			x = (_x.Count > 0) ? _x[0] : 0f;
			y = (_y.Count > 0) ? _y[0] : 0f;
			return;
		}
		float ax = _x[segment - 1];
		float ay = _y[segment - 1];
		x = ax + (_x[segment] - ax) * along;
		y = ay + (_y[segment] - ay) * along;
	}

	/// <summary>
	/// The dots of segment <paramref name="segment"/> drawn only as far as (<paramref name="toX"/>,
	/// <paramref name="toY"/>), the tip: stepping from where its dots step from, as the ending
	/// does each frame. <paramref name="dot"/> gets each one's position; returns how many.
	/// </summary>
	public int PartialDots(int segment, float toX, float toY, Action<float, float> dot)
	{
		if (segment < 1 || segment >= _segmentFromX.Count || _segmentSeam[segment])
		{
			return 0;
		}
		float x = _segmentFromX[segment];
		float y = _segmentFromY[segment];
		int count = 0;
		while (true)
		{
			float dx = toX - x;
			float dy = toY - y;
			float distSq = dx * dx + dy * dy;
			if (distSq <= DotSpacing * DotSpacing)
			{
				return count;
			}
			float inv = DotSpacing / MathF.Sqrt(distSq);
			x += dx * inv;
			y += dy * inv;
			dot(x, y);
			count++;
		}
	}

	/// <summary>Lowest and highest y of the points (0, 0 when there are none).</summary>
	public void YRange(out float low, out float high)
	{
		low = 0f;
		high = 0f;
		for (int i = 0; i < _y.Count; i++)
		{
			float y = _y[i];
			if (i == 0 || y < low)
			{
				low = y;
			}
			if (i == 0 || y > high)
			{
				high = y;
			}
		}
	}

	private void Step(ref float x, ref float y, float toX, float toY, int segment)
	{
		while (true)
		{
			float dx = toX - x;
			float dy = toY - y;
			float distSq = dx * dx + dy * dy;
			if (distSq <= DotSpacing * DotSpacing)
			{
				return;
			}
			float inv = DotSpacing / MathF.Sqrt(distSq);
			x += dx * inv;
			y += dy * inv;
			AddDot(x, y, big: false, segment);
		}
	}

	private void AddDot(float x, float y, bool big, int segment)
	{
		_dotX.Add(x);
		_dotY.Add(y);
		_dotBig.Add(big);
		_dotSegment.Add(segment);
	}

	private void ClearDots()
	{
		_dotX.Clear();
		_dotY.Clear();
		_dotBig.Clear();
		_dotSegment.Clear();
		_segmentFirstDot.Clear();
		_segmentSeam.Clear();
		_segmentFromX.Clear();
		_segmentFromY.Clear();
	}
}
