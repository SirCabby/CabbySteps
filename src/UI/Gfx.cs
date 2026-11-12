using System;
using System.Collections.Generic;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Rect and Color without calls into the game, for the IMGUI code (see <see cref="Fast"/>). Unity's
/// Rect keeps its numbers in the fields m_XMin, m_YMin, m_Width and m_Height; its x, width, xMax...
/// are methods, and each read through Il2CppInterop leaves a boxed float on the game's heap.
/// </summary>
public static class Gfx
{
	public static readonly Color White = C(1f, 1f, 1f, 1f);

	/// <summary>No transformation. Core sets GUI.matrix to it at the start of each repaint, so drawing code never has to read GUI.matrix back (a Matrix4x4 boxed on the game's heap each time).</summary>
	public static readonly Matrix4x4 Identity = Fast.TurnAbout(0f, default);

	/// <summary>
	/// The GUI matrix that turns what's drawn <paramref name="degrees"/> clockwise about
	/// <paramref name="pivot"/>, a point on the screen: what GUIUtility.RotateAroundPivot makes. In
	/// this game's build RotateAroundPivot isn't a game method but Il2CppInterop's stand-in, which
	/// makes ten calls into the game (GUI.matrix, Matrix4x4.TRS, Quaternion.Euler...), each leaving a
	/// boxed value on its heap: hundreds of bytes per call, and the map called it for every segment
	/// of the trail it used to draw, gigabytes a minute. Set it with GUI.matrix = ..., and set it back
	/// to <see cref="Identity"/> after. Inside a GUI group, pass the pivot's screen position (the
	/// group's own position plus the pivot), as RotateAroundPivot unclips it.
	/// </summary>
	public static Matrix4x4 Rotation(float degrees, Vector2 pivot)
	{
		return Fast.TurnAbout(degrees, pivot);
	}

	public static Rect R(float x, float y, float width, float height)
	{
		Rect r = default;
		r.m_XMin = x;
		r.m_YMin = y;
		r.m_Width = width;
		r.m_Height = height;
		return r;
	}

	public static Color C(float r, float g, float b, float a = 1f)
	{
		Color c = default;
		c.r = r;
		c.g = g;
		c.b = b;
		c.a = a;
		return c;
	}

	/// <summary>The colour with another alpha.</summary>
	public static Color A(Color c, float a)
	{
		c.a = a;
		return c;
	}

	public static float Right(Rect r)
	{
		return r.m_XMin + r.m_Width;
	}

	public static float Bottom(Rect r)
	{
		return r.m_YMin + r.m_Height;
	}

	public static Vector2 Center(Rect r)
	{
		return Fast.V2(r.m_XMin + r.m_Width * 0.5f, r.m_YMin + r.m_Height * 0.5f);
	}

	public static bool Contains(Rect r, Vector2 p)
	{
		return p.x >= r.m_XMin && p.x < r.m_XMin + r.m_Width && p.y >= r.m_YMin && p.y < r.m_YMin + r.m_Height;
	}

	/// <summary>The rect grown by <paramref name="px"/> on every side.</summary>
	public static Rect Expand(Rect r, float px)
	{
		return R(r.m_XMin - px, r.m_YMin - px, r.m_Width + px * 2f, r.m_Height + px * 2f);
	}

	private static readonly Dictionary<string, GUIContent> _fixed = new Dictionary<string, GUIContent>();

	/// <summary>
	/// A GUIContent for text that doesn't change (titles, pin numbers), made once. Drawing a label
	/// from a string hands the game a fresh copy of it every frame; from a GUIContent it doesn't.
	/// </summary>
	public static GUIContent Fixed(string text)
	{
		if (!_fixed.TryGetValue(text, out GUIContent content))
		{
			content = new GUIContent(text);
			_fixed[text] = content;
		}
		return content;
	}
}

/// <summary>
/// One label's text, kept in a GUIContent in the game and replaced only when the text changes
/// (see <see cref="Gfx.Fixed"/> for text that never changes).
/// </summary>
public sealed class LabelText
{
	private string _text;

	private GUIContent _content;

	/// <summary>Set when the text changed at the last <see cref="Get"/>, so sizes measured for it are stale.</summary>
	public bool Changed { get; private set; }

	public GUIContent Get(string text)
	{
		text ??= "";
		if (_content == null)
		{
			_content = new GUIContent(text);
			_text = text;
			Changed = true;
			return _content;
		}
		Changed = !string.Equals(_text, text, StringComparison.Ordinal);
		if (Changed)
		{
			_content.text = text;
			_text = text;
		}
		return _content;
	}
}
