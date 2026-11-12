using System;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// World position -> map pixel, exactly as the game itself puts Nate on its painted map. The map is
/// the game's own: the six painted sheets its ending shows (<see cref="MapArt"/>). When the game
/// notes where Nate goes, for the ending to paint his path on those sheets
/// (<c>SaveGod.SaveForever</c>, read from its machine code), it works out
/// <code>
///   u = (x mod 512) / 512              across the sheets (the world loops every 512 m)
///   v = (sin 30 * y + cos 30 * z) / 512   up them
/// </code>
/// in the ending's units. The sheets are the world seen from the south, looking down at 60 degrees,
/// so height moves a place up the map too: half as much as the same distance north, metre for
/// metre (a ledge 10 m over Nate's head is drawn 5 m of map above him). No calibration, no fitting.
/// <para>
/// Map pixels are the sheets' own texels: a sheet is 2048 across, (0, 0) is the top-left corner of
/// the top sheet, and y grows down the map. The frame comes from where the ending lays its sheets
/// out (journey.json). Pure arithmetic, checked offline.
/// </para>
/// </summary>
public sealed class MapProjection
{
	/// <summary>Metres per unit of the ending's drawing: a unit across is the world loop.</summary>
	public const float MetresPerUnit = 512f;

	/// <summary>A sheet's painting is this many texels across: a map pixel is one of them.</summary>
	public const float SheetTexels = 2048f;

	/// <summary>The map's up is the game's forward turned 30 degrees up: v takes sin 30 of a height and cos 30 of a z.</summary>
	public const float UpOfHeight = 0.5f;

	public const float UpOfNorth = 0.8660254f;

	/// <summary>The sheets as the game's 1.0 ending lays them out (journey.json), for when that file can't be read.</summary>
	public static readonly MapProjection Plain = new MapProjection(-0.08594f, 7.13344f, 1.08594f, 0.10216f, SheetTexels / 1.17188f);

	/// <summary>Units at map pixel (0, 0): u of the sheets' left edge, v of their top edge.</summary>
	public readonly float Left;

	public readonly float Top;

	/// <summary>Map pixels per unit of the ending's drawing.</summary>
	public readonly float PxPerUnit;

	/// <summary>The sheets' size, in map pixels.</summary>
	public readonly float Width;

	public readonly float Height;

	public MapProjection(float left, float top, float right, float bottom, float pxPerUnit)
	{
		Left = left;
		Top = top;
		PxPerUnit = pxPerUnit;
		Width = (right - left) * pxPerUnit;
		Height = (top - bottom) * pxPerUnit;
	}

	/// <summary>
	/// The map round sheets laid out as the ending has them (journey.json: centres and sizes, in its
	/// units): from the left edge of the leftmost to the right of the rightmost, the top of the
	/// highest to the bottom of the lowest, a map pixel being a texel of the first sheet.
	/// </summary>
	public static MapProjection ForSheets(float[] x, float[] y, float[] w, float[] h)
	{
		float left = float.MaxValue;
		float right = float.MinValue;
		float bottom = float.MaxValue;
		float top = float.MinValue;
		for (int i = 0; i < x.Length; i++)
		{
			left = Fast.Min(left, x[i] - w[i] * 0.5f);
			right = Fast.Max(right, x[i] + w[i] * 0.5f);
			bottom = Fast.Min(bottom, y[i] - h[i] * 0.5f);
			top = Fast.Max(top, y[i] + h[i] * 0.5f);
		}
		return new MapProjection(left, top, right, bottom, SheetTexels / w[0]);
	}

	/// <summary>
	/// A sheet's place on the map (centre and size in the ending's units), in whole map pixels:
	/// each edge is rounded on its own, so sheets that touch share an edge exactly and no seam shows.
	/// </summary>
	public void Place(float x, float y, float w, float h, out float mapX, out float mapY, out float mapW, out float mapH)
	{
		float x0 = MathF.Round((x - w * 0.5f - Left) * PxPerUnit);
		float x1 = MathF.Round((x + w * 0.5f - Left) * PxPerUnit);
		float y0 = MathF.Round((Top - (y + h * 0.5f)) * PxPerUnit);
		float y1 = MathF.Round((Top - (y - h * 0.5f)) * PxPerUnit);
		mapX = x0;
		mapY = y0;
		mapW = x1 - x0;
		mapH = y1 - y0;
	}

	/// <summary>Map pixels per metre of the map: across the sheets, or up them per metre of v.</summary>
	public float PxPerMetre => PxPerUnit / MetresPerUnit;

	/// <summary>Across the sheets, in the ending's units: 0..1 over the world loop, for x in any copy of it.</summary>
	public static float U(Vector3 world)
	{
		return WorldLoop.WrapX(world.x) / MetresPerUnit;
	}

	/// <summary>Up the sheets, in the ending's units: how far up the mountain the game draws this height and z.</summary>
	public static float V(Vector3 world)
	{
		return (UpOfHeight * world.y + UpOfNorth * world.z) / MetresPerUnit;
	}

	/// <summary>Map pixel of a world position, in the main drawing (never the sheets' torn margins).</summary>
	public Vector2 WorldToMap(Vector3 world)
	{
		return UnitsToMap(U(world), V(world));
	}

	/// <summary>Map pixel of a point in the ending's units.</summary>
	public Vector2 UnitsToMap(float u, float v)
	{
		return Fast.V2((u - Left) * PxPerUnit, (Top - v) * PxPerUnit);
	}

	/// <summary>
	/// Screen rotation in degrees (0 = pointing right, clockwise, as <see cref="Gfx.Rotation"/> takes it)
	/// of a world direction drawn on the map. It's the same everywhere: north is straight up, east
	/// right, and a level heading north is drawn shorter than one east (the map looks down at 60 degrees).
	/// </summary>
	public static float MapAngle(Vector3 direction)
	{
		return Fast.Atan2(-(UpOfHeight * direction.y + UpOfNorth * direction.z), direction.x) * Fast.Rad2Deg;
	}

	/// <summary>A map pixel kept on the sheets.</summary>
	public Vector2 Clamp(Vector2 px)
	{
		return Fast.V2(Fast.Clamp(px.x, 0f, Width), Fast.Clamp(px.y, 0f, Height));
	}
}
