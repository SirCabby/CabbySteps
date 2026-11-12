using System.Collections.Generic;
using UnityEngine;

namespace CabbySteps;

/// <summary>Small procedural textures used by the IMGUI overlay, made once and kept alive.</summary>
public static class GuiTextures
{
	public static Texture2D White;

	public static Texture2D Arrow;

	public static Texture2D Ring;

	public static Texture2D Disc;

	/// <summary>White tick with a dark outline (tint with GUI.color): a completed POI.</summary>
	public static Texture2D Check;

	/// <summary>Mouse pointer, tip at <see cref="PointerTip"/> (texture pixels from the top left).</summary>
	public static Texture2D Pointer;

	public const int PointerSize = 32;

	public const float PointerTip = 2f;

	/// <summary>
	/// A saved location's map pin: a round head over a point, white with a dark outline (tint with
	/// GUI.color). Sizes are texture pixels from the top left; the point is at <see cref="PinTipY"/>.
	/// </summary>
	public static Texture2D Pin;

	public const int PinWidth = 32;

	public const int PinHeight = 40;

	public const float PinHeadY = 13f;

	public const float PinHeadRadius = 12f;

	public const float PinTipY = 38.5f;

	private static readonly Dictionary<int, Texture2D> _dotByCat = new Dictionary<int, Texture2D>();

	/// <summary>Dot colour per Map Genie category id (see pois.json "cat").</summary>
	private static readonly Dictionary<int, Color> CatColors = new Dictionary<int, Color>
	{
		{
			13870,
			new Color(1f, 0.35f, 0.75f)
		},
		{
			13873,
			new Color(0.32f, 0.48f, 0.8f)
		},
		{
			13885,
			new Color(0.18f, 0.78f, 0.75f)
		},
		{
			13886,
			new Color(0.24f, 0.8f, 0.47f)
		},
		{
			13880,
			new Color(1f, 0.62f, 0.24f)
		},
		{
			13883,
			new Color(0.9f, 0.31f, 0.31f)
		},
		{
			13882,
			new Color(0.71f, 0.39f, 0.86f)
		},
		{
			13878,
			new Color(0.94f, 0.82f, 0.27f)
		},
		{
			13879,
			new Color(0.31f, 0.78f, 0.9f)
		},
		{
			13881,
			new Color(0.9f, 0.55f, 0.75f)
		},
		{
			13891,
			new Color(0.94f, 0.78f, 0.31f)
		},
		{
			13890,
			new Color(0.59f, 0.86f, 0.31f)
		}
	};

	public static void Init()
	{
		if (White != null)
		{
			return;
		}
		White = Solid(new Color(1f, 1f, 1f, 1f));
		Arrow = MakeArrow();
		Ring = MakeRing(24);
		Disc = MakeDisc(64);
		Check = MakeCheck(32);
		Pointer = MakePointer();
		Pin = MakePin();
	}

	public static Color CategoryColor(int cat)
	{
		if (!CatColors.TryGetValue(cat, out var color))
		{
			return Color.white;
		}
		return color;
	}

	public static Texture2D Dot(int cat)
	{
		if (_dotByCat.TryGetValue(cat, out var tex))
		{
			return tex;
		}
		tex = MakeDot(16, CategoryColor(cat));
		_dotByCat[cat] = tex;
		return tex;
	}

	private static Texture2D Solid(Color c)
	{
		Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
		tex.SetPixel(0, 0, c);
		tex.Apply();
		tex.wrapMode = TextureWrapMode.Clamp;
		KeepAlive.Persist(tex);
		return tex;
	}

	/// <summary>Filled circle of colour c with a dark 1.2 px outline.</summary>
	private static Texture2D MakeDot(int size, Color c)
	{
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		float radius = (float)size * 0.5f - 1f;
		float cx = (float)size * 0.5f;
		float cy = (float)size * 0.5f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = (float)x + 0.5f - cx;
				float dy = (float)y + 0.5f - cy;
				float r = Mathf.Sqrt(dx * dx + dy * dy);
				if (r <= radius)
				{
					tex.SetPixel(x, y, c);
				}
				else if (r <= radius + 1.2f)
				{
					tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0.85f));
				}
				else
				{
					tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
				}
			}
		}
		tex.Apply();
		tex.wrapMode = TextureWrapMode.Clamp;
		KeepAlive.Persist(tex);
		return tex;
	}

	/// <summary>Anti-aliased white disc (tint with GUI.color).</summary>
	private static Texture2D MakeDisc(int size)
	{
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		float radius = (float)size * 0.5f - 1f;
		float cx = (float)size * 0.5f;
		float cy = (float)size * 0.5f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = (float)x + 0.5f - cx;
				float dy = (float)y + 0.5f - cy;
				float r = Mathf.Sqrt(dx * dx + dy * dy);
				float a = Mathf.Clamp01(radius - r + 0.5f);
				tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
			}
		}
		tex.Apply();
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Bilinear;
		KeepAlive.Persist(tex);
		return tex;
	}

	/// <summary>3 px white ring (tint with GUI.color).</summary>
	private static Texture2D MakeRing(int size)
	{
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		float outer = (float)size * 0.5f - 1f;
		float inner = outer - 3f;
		float cx = (float)size * 0.5f;
		float cy = (float)size * 0.5f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = (float)x + 0.5f - cx;
				float dy = (float)y + 0.5f - cy;
				float r = Mathf.Sqrt(dx * dx + dy * dy);
				tex.SetPixel(x, y, (r <= outer && r >= inner) ? Color.white : new Color(0f, 0f, 0f, 0f));
			}
		}
		tex.Apply();
		tex.wrapMode = TextureWrapMode.Clamp;
		KeepAlive.Persist(tex);
		return tex;
	}

	/// <summary>White arrow pointing +u (right, i.e. 0 degrees), 4x4 supersampled.</summary>
	private static Texture2D MakeArrow()
	{
		int size = 128;
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				int hits = 0;
				for (int sy = 0; sy < 4; sy++)
				{
					for (int sx = 0; sx < 4; sx++)
					{
						float u = ((float)x + ((float)sx + 0.5f) / 4f) / (float)size;
						float v = ((float)y + ((float)sy + 0.5f) / 4f) / (float)size;
						if (InArrow(u, v))
						{
							hits++;
						}
					}
				}
				float a = (float)hits / 16f;
				tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
			}
		}
		tex.Apply();
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Bilinear;
		KeepAlive.Persist(tex);
		return tex;
	}

	/// <summary>A tick: a white stroke with a dark outline, 4x4 supersampled.</summary>
	private static Texture2D MakeCheck(int size)
	{
		// The tick's corners in unit space, y down.
		Vector2 a = new Vector2(0.15f, 0.53f);
		Vector2 b = new Vector2(0.40f, 0.78f);
		Vector2 c = new Vector2(0.86f, 0.24f);
		const float stroke = 0.085f;
		const float outline = 0.065f;
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				int fill = 0;
				int edge = 0;
				for (int sy = 0; sy < 4; sy++)
				{
					for (int sx = 0; sx < 4; sx++)
					{
						Vector2 p = new Vector2(((float)x + ((float)sx + 0.5f) / 4f) / (float)size, ((float)y + ((float)sy + 0.5f) / 4f) / (float)size);
						float d = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, c));
						if (d <= stroke)
						{
							fill++;
						}
						else if (d <= stroke + outline)
						{
							edge++;
						}
					}
				}
				tex.SetPixel(x, size - 1 - y, Blend(fill, edge, 0.9f));
			}
		}
		tex.Apply();
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Bilinear;
		KeepAlive.Persist(tex);
		return tex;
	}

	/// <summary>The classic arrow pointer: white with a dark outline, tip near the top left, 4x4 supersampled.</summary>
	private static Texture2D MakePointer()
	{
		const int size = PointerSize;
		const float o = PointerTip;
		// Outline of the arrow in texture pixels, y down, starting at the tip.
		Vector2[] shape =
		{
			new Vector2(o, o),
			new Vector2(o, o + 21f),
			new Vector2(o + 4.6f, o + 16.8f),
			new Vector2(o + 8f, o + 25f),
			new Vector2(o + 12.4f, o + 23.2f),
			new Vector2(o + 9f, o + 15.4f),
			new Vector2(o + 14.8f, o + 15.4f)
		};
		const float border = 1.5f;
		Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				int fill = 0;
				int edge = 0;
				for (int sy = 0; sy < 4; sy++)
				{
					for (int sx = 0; sx < 4; sx++)
					{
						Vector2 p = new Vector2((float)x + ((float)sx + 0.5f) / 4f, (float)y + ((float)sy + 0.5f) / 4f);
						float d = float.MaxValue;
						for (int i = 0; i < shape.Length; i++)
						{
							d = Mathf.Min(d, SegmentDistance(p, shape[i], shape[(i + 1) % shape.Length]));
						}
						if (InPolygon(p, shape))
						{
							if (d > border)
							{
								fill++;
							}
							else
							{
								edge++;
							}
						}
						else if (d <= 0.5f)
						{
							edge++;
						}
					}
				}
				tex.SetPixel(x, size - 1 - y, Blend(fill, edge, 1f));
			}
		}
		tex.Apply();
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Bilinear;
		KeepAlive.Persist(tex);
		return tex;
	}

	/// <summary>The map pin (see <see cref="Pin"/>), 4x4 supersampled.</summary>
	private static Texture2D MakePin()
	{
		const int w = PinWidth;
		const int h = PinHeight;
		Vector2 head = new Vector2(w * 0.5f, PinHeadY);
		Vector2 tip = new Vector2(w * 0.5f, PinTipY);
		// The outline: the tip, then round the head from one tangent point over the top to the other.
		// y is down, so the angle PI/2 points at the tip.
		float phi = Mathf.Acos(PinHeadRadius / (tip.y - head.y));
		float from = Mathf.PI * 0.5f + phi;
		float to = Mathf.PI * 2.5f - phi;
		const int arcSteps = 48;
		Vector2[] shape = new Vector2[arcSteps + 2];
		shape[0] = tip;
		for (int i = 0; i <= arcSteps; i++)
		{
			float a = from + (to - from) * i / arcSteps;
			shape[i + 1] = new Vector2(head.x + PinHeadRadius * Mathf.Cos(a), head.y + PinHeadRadius * Mathf.Sin(a));
		}
		const float border = 1.6f;
		Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false);
		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				int fill = 0;
				int edge = 0;
				for (int sy = 0; sy < 4; sy++)
				{
					for (int sx = 0; sx < 4; sx++)
					{
						Vector2 p = new Vector2((float)x + ((float)sx + 0.5f) / 4f, (float)y + ((float)sy + 0.5f) / 4f);
						float d = float.MaxValue;
						for (int i = 0; i < shape.Length; i++)
						{
							d = Mathf.Min(d, SegmentDistance(p, shape[i], shape[(i + 1) % shape.Length]));
						}
						if (InPolygon(p, shape))
						{
							if (d > border)
							{
								fill++;
							}
							else
							{
								edge++;
							}
						}
						else if (d <= 0.5f)
						{
							edge++;
						}
					}
				}
				tex.SetPixel(x, h - 1 - y, Blend(fill, edge, 1f));
			}
		}
		tex.Apply();
		tex.wrapMode = TextureWrapMode.Clamp;
		tex.filterMode = FilterMode.Bilinear;
		KeepAlive.Persist(tex);
		return tex;
	}

	/// <summary>Average of <paramref name="fill"/> white and <paramref name="edge"/> dark samples out of 16.</summary>
	private static Color Blend(int fill, int edge, float edgeAlpha)
	{
		int covered = fill + edge;
		if (covered == 0)
		{
			return new Color(0f, 0f, 0f, 0f);
		}
		float white = (float)fill / (float)covered;
		float alpha = ((float)fill + (float)edge * edgeAlpha) / 16f;
		return new Color(white, white, white, alpha);
	}

	private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
	{
		float abx = b.x - a.x;
		float aby = b.y - a.y;
		float t = Mathf.Clamp01(((p.x - a.x) * abx + (p.y - a.y) * aby) / (abx * abx + aby * aby));
		float dx = p.x - (a.x + abx * t);
		float dy = p.y - (a.y + aby * t);
		return Mathf.Sqrt(dx * dx + dy * dy);
	}

	private static bool InPolygon(Vector2 p, Vector2[] poly)
	{
		bool inside = false;
		for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
		{
			if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
			{
				inside = !inside;
			}
		}
		return inside;
	}

	/// <summary>Arrow shape in unit UV space: a shaft up to u = 0.42, then a triangular head to the tip at u = 1.</summary>
	private static bool InArrow(float u, float v)
	{
		const float headStart = 0.42f;
		const float shaftHalfWidth = 0.17f;
		float dv = Mathf.Abs(v - 0.5f);
		if (u >= headStart)
		{
			float halfWidth = 0.5f * (1f - u) / (1f - headStart);
			return dv <= halfWidth;
		}
		return dv <= shaftHalfWidth;
	}
}
