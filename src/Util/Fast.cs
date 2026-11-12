using System;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Plain C# stand-ins for Unity's maths and vector constructors.
/// <para>
/// Through Il2CppInterop, every Unity method the mod calls is a call into the game
/// (<c>il2cpp_runtime_invoke</c>), and the game boxes whatever the method returns: Mathf.Sqrt,
/// Rect.x, Color.white or Vector2 + Vector2 each leave a small object on the game's heap. Done per
/// POI or per point of a path, several times a frame, that came to about 175 MB a minute in play and
/// 11 GB a minute with the map open. Struct constructors return nothing, so they don't allocate,
/// but each is still a slow call into the game. Nothing here leaves the mod.
/// </para>
/// </summary>
public static class Fast
{
	public const float Rad2Deg = 57.29578f;

	public const float Deg2Rad = 0.017453292f;

	public static Vector2 V2(float x, float y)
	{
		Vector2 v = default;
		v.x = x;
		v.y = y;
		return v;
	}

	public static Vector3 V3(float x, float y, float z)
	{
		Vector3 v = default;
		v.x = x;
		v.y = y;
		v.z = z;
		return v;
	}

	/// <summary>
	/// A matrix turning points <paramref name="degrees"/> about <paramref name="pivot"/> in the xy
	/// plane (clockwise on the screen, where y points down): what Unity makes of
	/// Matrix4x4.TRS(pivot, Quaternion.Euler(0, 0, degrees), one) * Matrix4x4.TRS(-pivot, identity, one).
	/// </summary>
	public static Matrix4x4 TurnAbout(float degrees, Vector2 pivot)
	{
		float a = degrees * Deg2Rad;
		float c = MathF.Cos(a);
		float s = MathF.Sin(a);
		Matrix4x4 m = default;
		m.m00 = c;
		m.m01 = 0f - s;
		m.m03 = pivot.x - c * pivot.x + s * pivot.y;
		m.m10 = s;
		m.m11 = c;
		m.m13 = pivot.y - s * pivot.x - c * pivot.y;
		m.m22 = 1f;
		m.m33 = 1f;
		return m;
	}

	public static float Abs(float f)
	{
		return (f < 0f) ? (0f - f) : f;
	}

	public static float Min(float a, float b)
	{
		return (a < b) ? a : b;
	}

	public static float Max(float a, float b)
	{
		return (a > b) ? a : b;
	}

	public static int Min(int a, int b)
	{
		return (a < b) ? a : b;
	}

	public static int Max(int a, int b)
	{
		return (a > b) ? a : b;
	}

	public static float Clamp(float value, float min, float max)
	{
		return (value < min) ? min : ((value > max) ? max : value);
	}

	public static int Clamp(int value, int min, int max)
	{
		return (value < min) ? min : ((value > max) ? max : value);
	}

	public static float Clamp01(float value)
	{
		return Clamp(value, 0f, 1f);
	}

	public static float Sqrt(float f)
	{
		return MathF.Sqrt(f);
	}

	public static float Atan2(float y, float x)
	{
		return MathF.Atan2(y, x);
	}

	public static float Sin(float f)
	{
		return MathF.Sin(f);
	}

	public static float Cos(float f)
	{
		return MathF.Cos(f);
	}

	public static float Acos(float f)
	{
		return MathF.Acos(f);
	}

	public static float Pow(float f, float p)
	{
		return MathF.Pow(f, p);
	}

	/// <summary>Rounds half to even, as Mathf.RoundToInt does.</summary>
	public static int RoundToInt(float f)
	{
		return (int)MathF.Round(f);
	}

	public static int CeilToInt(float f)
	{
		return (int)MathF.Ceiling(f);
	}

	public static bool Approximately(float a, float b)
	{
		return Abs(b - a) < Max(1E-06f * Max(Abs(a), Abs(b)), float.Epsilon * 8f);
	}
}
