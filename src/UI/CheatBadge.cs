using UnityEngine;

namespace CabbySteps;

/// <summary>
/// Small tags in the top-left corner while playing with a cheat on ("GRIPPY FEET", or "EXTREME
/// GRIP" at its second level; "HOLD TIGHT"), one under the other, so none is ever forgotten: each
/// changes how the game plays, and stays on from one session to the next.
/// </summary>
public class CheatBadge
{
	private const string GrippyText = "GRIPPY FEET";

	private const string ExtremeText = "EXTREME GRIP";

	private const string HoldText = "HOLD TIGHT";

	private GUIStyle _style;

	private float _builtScale = -1f;

	// Each tag's width for the current UI scale, worked out once per text (null: not yet).
	private float _grippyWidth = -1f;

	private float _extremeWidth = -1f;

	private float _holdWidth = -1f;

	public void Draw()
	{
		if (!GrippyFeet.On && !HoldTight.On)
		{
			return;
		}
		if (_style == null || _builtScale != Ui.Scale)
		{
			_builtScale = Ui.Scale;
			_grippyWidth = _extremeWidth = _holdWidth = -1f;
			MemoryDiagnostics.StyleBuilds++;
			_style = new GUIStyle(GUI.skin.label)
			{
				fontSize = Ui.Font(13f),
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter,
				normal =
				{
					textColor = Theme.InkText
				}
			};
		}
		float y = Ui.S(16f);
		if (GrippyFeet.On)
		{
			if (GrippyFeet.Extreme)
			{
				Tag(ExtremeText, ref _extremeWidth, y);
			}
			else
			{
				Tag(GrippyText, ref _grippyWidth, y);
			}
			y += Ui.S(30f);
		}
		if (HoldTight.On)
		{
			Tag(HoldText, ref _holdWidth, y);
		}
	}

	private void Tag(string label, ref float width, float y)
	{
		GUIContent text = Gfx.Fixed(label);
		if (width < 0f)
		{
			MemoryDiagnostics.GuiContents++;
			width = _style.CalcSize(text).x + Ui.S(20f);
		}
		Rect r = Gfx.R(Ui.S(16f), y, width, Ui.S(24f));
		Theme.Panel(r, fill: true);
		GUI.Label(r, text, _style);
	}
}
