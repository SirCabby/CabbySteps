// Minimal stand-ins for the Unity and MelonLoader types used by the mod files under test.
// Only what those files touch is here; the semantics match the real types.

namespace UnityEngine
{
	public struct Vector2
	{
		public float x;

		public float y;

		public Vector2(float x, float y)
		{
			this.x = x;
			this.y = y;
		}
	}

	public struct Vector3
	{
		public float x;

		public float y;

		public float z;

		public Vector3(float x, float y, float z)
		{
			this.x = x;
			this.y = y;
			this.z = z;
		}

		// Unity's == is "within 1e-5": the same here.
		public static bool operator ==(Vector3 a, Vector3 b)
		{
			float dx = a.x - b.x, dy = a.y - b.y, dz = a.z - b.z;
			return dx * dx + dy * dy + dz * dz < 9.99999944E-11f;
		}

		public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);

		public override bool Equals(object o) => o is Vector3 v && this == v;

		public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() * 397) ^ (z.GetHashCode() * 7919);
	}

	public struct Quaternion
	{
		public float x;

		public float y;

		public float z;

		public float w;

		public Quaternion(float x, float y, float z, float w)
		{
			this.x = x;
			this.y = y;
			this.z = z;
			this.w = w;
		}

		// Unity's == compares the turns (q and -q are the same turn); the tests want the numbers.
		public static bool operator ==(Quaternion a, Quaternion b) => a.x == b.x && a.y == b.y && a.z == b.z && a.w == b.w;

		public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);

		public override bool Equals(object o) => o is Quaternion q && this == q;

		public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() * 397) ^ (z.GetHashCode() * 7919) ^ (w.GetHashCode() * 31);
	}

	public struct Matrix4x4
	{
		public float m00, m10, m20, m30, m01, m11, m21, m31, m02, m12, m22, m32, m03, m13, m23, m33;
	}

	public static class Mathf
	{
		public const float Rad2Deg = 57.29578f;

		public static float Atan2(float y, float x) => (float)System.Math.Atan2(y, x);

		public static float Sqrt(float f) => (float)System.Math.Sqrt(f);
	}
}

namespace MelonLoader
{
	public class MelonLogger
	{
		public class Instance
		{
			public void Msg(string s) => System.Console.WriteLine("  [log] " + s);

			public void Warning(string s) => System.Console.WriteLine("  [warn] " + s);

			public void Error(string s) => System.Console.WriteLine("  [error] " + s);
		}
	}
}
