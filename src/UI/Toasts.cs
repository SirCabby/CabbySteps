using UnityEngine;

namespace CabbySteps;

/// <summary>A short message near the bottom of the screen that fades out.</summary>
public class Toasts
{
	private const float Duration = 2.4f;

	private const float FadeTime = 0.6f;

	private string _text;

	private readonly LabelText _content = new LabelText();

	private float _until;

	private GUIStyle _style;

	private float _builtScale = -1f;

	/// <summary>The box's width for the current text and UI scale (-1: measure again).</summary>
	private float _width = -1f;

	public void Show(string text)
	{
		_text = text;
		_until = Frame.Now + Duration;
	}

	public void Draw()
	{
		if (string.IsNullOrEmpty(_text))
		{
			return;
		}
		float now = Frame.Now;
		if (now >= _until)
		{
			_text = null;
			return;
		}
		if (_style == null || _builtScale != Ui.Scale)
		{
			_builtScale = Ui.Scale;
			_width = -1f;
			MemoryDiagnostics.StyleBuilds++;
			_style = new GUIStyle(GUI.skin.label)
			{
				fontSize = Ui.Font(18f),
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
				normal =
				{
					textColor = Theme.InkText
				}
			};
		}
		float alpha = Fast.Clamp01((_until - now) / FadeTime);
		GUIContent content = _content.Get(_text);
		float sw = Frame.ScreenWidth;
		if (_content.Changed || _width < 0f)
		{
			// Wide enough for long POI names, but never off the screen.
			MemoryDiagnostics.GuiContents++;
			_width = Fast.Min(Fast.Max(Ui.S(560f), _style.CalcSize(content).x + Ui.S(40f)), sw - Ui.S(40f));
		}
		float w = _width;
		Rect r = Gfx.R((sw - w) * 0.5f, Frame.ScreenHeight - Ui.S(150f), w, Ui.S(40f));
		GUI.color = Gfx.A(Theme.Ink, 0.92f * alpha);
		GUI.DrawTexture(Theme.Expand(r, Ui.S(3.5f)), GuiTextures.White);
		GUI.color = Gfx.A(Theme.Gold, alpha);
		GUI.DrawTexture(Theme.Expand(r, Ui.S(1.5f)), GuiTextures.White);
		GUI.color = Gfx.A(Theme.Parchment, alpha);
		GUI.DrawTexture(r, GuiTextures.White);
		GUI.color = Gfx.A(Gfx.White, alpha);
		GUI.Label(r, content, _style);
		GUI.color = Gfx.White;
	}
}
