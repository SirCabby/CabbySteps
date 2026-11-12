using UnityEngine;

namespace CabbySteps;

/// <summary>Drawing the game's painted map sheets into a screen rectangle.</summary>
public static class MapDraw
{
	/// <summary>Plain paper, where a sheet's painting hasn't arrived (or can't be had).</summary>
	private static readonly Color PlainPaper = Gfx.C(0.79f, 0.68f, 0.5f, 1f);

	/// <summary>
	/// Draw the sheets so that map pixel <paramref name="centerPx"/> lands on screen point
	/// <paramref name="screenCenter"/>, at <paramref name="scale"/> screen pixels per map pixel,
	/// clipped to <paramref name="clip"/>. Sheets out of view aren't drawn.
	/// </summary>
	public static void DrawMap(MapArt art, Rect clip, Vector2 screenCenter, float scale, Vector2 centerPx)
	{
		if (art == null || scale <= 0f)
		{
			return;
		}
		// Inside the group, coordinates are relative to the clip rect.
		GUI.BeginGroup(clip);
		float originX = screenCenter.x - centerPx.x * scale - clip.m_XMin;
		float originY = screenCenter.y - centerPx.y * scale - clip.m_YMin;
		float width = clip.m_Width;
		float height = clip.m_Height;
		foreach (MapArt.Piece sheet in art.Sheets)
		{
			float x0 = originX + sheet.MapX * scale;
			float y0 = originY + sheet.MapY * scale;
			float x1 = originX + (sheet.MapX + sheet.MapW) * scale;
			float y1 = originY + (sheet.MapY + sheet.MapH) * scale;
			if (x1 <= 0f || y1 <= 0f || x0 >= width || y0 >= height)
			{
				continue;
			}
			Rect r = Gfx.R(x0, y0, x1 - x0, y1 - y0);
			Texture painting = sheet.Painting;
			if (!ReferenceEquals(painting, null))
			{
				GUI.DrawTexture(r, painting);
			}
			else
			{
				GUI.color = PlainPaper;
				GUI.DrawTexture(r, GuiTextures.White);
				GUI.color = Gfx.White;
			}
		}
		GUI.EndGroup();
	}
}
