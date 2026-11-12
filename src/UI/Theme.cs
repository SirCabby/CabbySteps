using UnityEngine;

namespace CabbySteps;

/// <summary>The parchment-and-gold look shared by every panel.</summary>
public static class Theme
{
	public static readonly Color Ink = new Color(0.16f, 0.11f, 0.06f, 1f);

	public static readonly Color InkText = new Color(0.2f, 0.13f, 0.06f, 1f);

	public static readonly Color Parchment = new Color(0.83f, 0.71f, 0.49f, 1f);

	public static readonly Color ParchmentDark = new Color(0.72f, 0.59f, 0.39f, 1f);

	public static readonly Color Gold = new Color(0.86f, 0.66f, 0.24f, 1f);

	public static readonly Color GoldBright = new Color(1f, 0.85f, 0.42f, 1f);

	public static readonly Color Player = new Color(0.25f, 0.95f, 1f, 1f);

	/// <summary>A saved location's pin.</summary>
	public static readonly Color Pin = new Color(0.86f, 0.2f, 0.16f, 1f);

	/// <summary>The quick save's pin.</summary>
	public static readonly Color QuickPin = new Color(0.2f, 0.45f, 0.92f, 1f);

	/// <summary>The "where you were" pin.</summary>
	public static readonly Color BackPin = new Color(0.82f, 0.82f, 0.8f, 1f);

	/// <summary>A saved location's pin colour: red for a kept one, blue for the quick save, grey for "where you were".</summary>
	public static Color PinColor(SavedLocation location)
	{
		switch (location.Slot)
		{
		case LocationSlot.Quick:
			return QuickPin;
		case LocationSlot.Back:
			return BackPin;
		default:
			return Pin;
		}
	}

	private static readonly Color PanelShadow = Gfx.C(0f, 0f, 0f, 0.38f);

	public static Rect Expand(Rect r, float px)
	{
		return Gfx.Expand(r, px);
	}

	/// <summary>Drop shadow, ink border, gold rule and (optionally) a parchment fill.</summary>
	public static void Panel(Rect r, bool fill)
	{
		float border = Ui.S(3.5f);
		float rule = Ui.S(1.5f);
		float shadowOffset = Ui.S(4f);
		GUI.color = PanelShadow;
		Rect shadow = Expand(r, border);
		shadow.m_XMin += shadowOffset;
		shadow.m_YMin += shadowOffset;
		GUI.DrawTexture(shadow, GuiTextures.White);
		GUI.color = Ink;
		GUI.DrawTexture(Expand(r, border), GuiTextures.White);
		GUI.color = Gold;
		GUI.DrawTexture(Expand(r, rule), GuiTextures.White);
		if (fill)
		{
			GUI.color = Parchment;
			GUI.DrawTexture(r, GuiTextures.White);
		}
		GUI.color = Gfx.White;
	}

	public static void CornerStuds(Rect r)
	{
		float size = Ui.S(9f);
		float inset = Ui.S(2.5f);
		float left = r.m_XMin - inset;
		float top = r.m_YMin - inset;
		float right = Gfx.Right(r) + inset;
		float bottom = Gfx.Bottom(r) + inset;
		DrawStud(left, top, size);
		DrawStud(right, top, size);
		DrawStud(left, bottom, size);
		DrawStud(right, bottom, size);
	}

	private static void DrawStud(float cx, float cy, float s)
	{
		GUI.color = Ink;
		GUI.DrawTexture(Gfx.R(cx - s * 0.5f, cy - s * 0.5f, s, s), GuiTextures.Disc);
		GUI.color = GoldBright;
		float inner = s * 0.55f;
		GUI.DrawTexture(Gfx.R(cx - inner * 0.5f, cy - inner * 0.5f, inner, inner), GuiTextures.Disc);
		GUI.color = Gfx.White;
	}

	/// <summary>
	/// A label with a soft drop shadow. <paramref name="shadow"/> is the same style in black
	/// (see <see cref="ShadowOf"/>), made once: swapping the text colour back and forth each frame
	/// would cost calls into the game.
	/// </summary>
	public static void Shadowed(Rect r, GUIContent text, GUIStyle style, GUIStyle shadow)
	{
		float offset = Ui.S(1.5f);
		GUI.Label(Gfx.R(r.m_XMin + offset, r.m_YMin + offset, r.m_Width, r.m_Height), text, shadow);
		GUI.Label(r, text, style);
	}

	/// <summary>A copy of the style in translucent black, for <see cref="Shadowed"/> (stronger alpha for text over the game world).</summary>
	public static GUIStyle ShadowOf(GUIStyle style, float alpha = 0.55f)
	{
		return new GUIStyle(style)
		{
			normal =
			{
				textColor = Gfx.C(0f, 0f, 0f, alpha)
			}
		};
	}
}
