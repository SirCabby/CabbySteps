using System.Text;

namespace CabbySteps;

/// <summary>
/// A name being typed for a saved location, and the on-screen keyboard a pad types it on (the mouse
/// can click it too; a real keyboard types straight in). The keyboard is four rows of ten characters
/// (digits, then QWERTY) over a row of Shift, Space, Delete and Done, and moving off one edge comes
/// back on the other. Shift is for the next character only, and comes on by itself while the name
/// is empty, so a name starts with a capital. Plain logic with nothing from the game, so the offline
/// tests drive it; <see cref="RenameBox"/> reads the input and draws it.
/// </summary>
public sealed class NameEntry
{
	public enum Key
	{
		/// <summary>A character: a digit, a letter or a symbol.</summary>
		Char,
		Shift,
		Space,
		Delete,
		Done
	}

	public const int Columns = 10;

	/// <summary>Rows of character keys; the row of Shift, Space, Delete and Done comes after them.</summary>
	public const int CharRows = 4;

	public const int Rows = CharRows + 1;

	/// <summary>The row of Shift, Space, Delete and Done.</summary>
	public const int BottomRow = CharRows;

	// The character keys, row by row, as they are and with Shift (digits stay digits).
	private const string LowerKeys = "1234567890" + "qwertyuiop" + "asdfghjkl'" + "zxcvbnm-.!";

	private const string UpperKeys = "1234567890" + "QWERTYUIOP" + "ASDFGHJKL\"" + "ZXCVBNM_,?";

	// The bottom row: each key, the column it starts at and how many columns it spans.
	private static readonly Key[] BottomKeys = { Key.Shift, Key.Space, Key.Delete, Key.Done };

	private static readonly int[] BottomStarts = { 0, 2, 6, 8 };

	private static readonly int[] BottomSpans = { 2, 4, 2, 2 };

	// Each character key's face as a string made once, so drawing the keys makes none.
	private static readonly string[] LowerFaces = Faces(LowerKeys);

	private static readonly string[] UpperFaces = Faces(UpperKeys);

	private readonly StringBuilder _text = new StringBuilder();

	public NameEntry(int maxLength)
	{
		MaxLength = maxLength;
	}

	public int MaxLength { get; }

	/// <summary>The name so far.</summary>
	public string Text { get; private set; } = "";

	/// <summary>The next character picked on the screen is a capital, or a symbol key's other symbol.</summary>
	public bool Shift { get; private set; }

	/// <summary>The selected key's row.</summary>
	public int Row { get; private set; }

	/// <summary>The selected key's column; in the bottom row, any of the columns its key spans.</summary>
	public int Column { get; private set; }

	/// <summary>The selected key.</summary>
	public Key Selected => KeyAt(Row, Column);

	public static int BottomCount => BottomKeys.Length;

	/// <summary>The bottom row's key <paramref name="index"/> (0 = Shift ... 3 = Done).</summary>
	public static Key BottomKey(int index)
	{
		return BottomKeys[index];
	}

	/// <summary>The column the bottom row's key <paramref name="index"/> starts at.</summary>
	public static int BottomStart(int index)
	{
		return BottomStarts[index];
	}

	/// <summary>How many columns the bottom row's key <paramref name="index"/> spans.</summary>
	public static int BottomSpan(int index)
	{
		return BottomSpans[index];
	}

	public static Key KeyAt(int row, int column)
	{
		return (row == BottomRow) ? BottomKeys[BottomIndex(column)] : Key.Char;
	}

	/// <summary>Start over with <paramref name="text"/> (an empty name, or the one to edit), on Q.</summary>
	public void Reset(string text)
	{
		_text.Clear();
		if (!string.IsNullOrEmpty(text))
		{
			_text.Append(text);
		}
		Text = _text.ToString();
		Shift = _text.Length == 0;
		Row = 1;
		Column = 0;
	}

	/// <summary>
	/// Add <paramref name="c"/> to the name: one typed on a keyboard, or picked on the screen. False,
	/// and nothing added, when the name is full or it's not something a name has: a control
	/// character, half of an emoji, or a space at the start or after another.
	/// </summary>
	public bool Type(char c)
	{
		if (char.IsControl(c) || char.IsSurrogate(c) || _text.Length >= MaxLength)
		{
			return false;
		}
		if (char.IsWhiteSpace(c))
		{
			if (_text.Length == 0 || _text[_text.Length - 1] == ' ')
			{
				return false;
			}
			c = ' ';
		}
		_text.Append(c);
		Text = _text.ToString();
		Shift = false;
		return true;
	}

	/// <summary>Take the last character off. Emptied, the name's next character is a capital again.</summary>
	public bool Backspace()
	{
		if (_text.Length == 0)
		{
			return false;
		}
		_text.Length--;
		Text = _text.ToString();
		if (_text.Length == 0)
		{
			Shift = true;
		}
		return true;
	}

	public void ToggleShift()
	{
		Shift = !Shift;
	}

	/// <summary>Move the selection a key left or right (<paramref name="dx"/>) or up or down (<paramref name="dy"/>, down positive), coming back round at the edges.</summary>
	public void Move(int dx, int dy)
	{
		if (dy != 0)
		{
			// The column stays: from the bottom row, up lands above where the move into it came from.
			Row = Wrap(Row + dy, Rows);
		}
		if (dx == 0)
		{
			return;
		}
		if (Row == BottomRow)
		{
			int key = Wrap(BottomIndex(Column) + dx, BottomKeys.Length);
			// Onto the side of the key nearest where the move came from.
			Column = (dx > 0) ? BottomStarts[key] : BottomStarts[key] + BottomSpans[key] - 1;
		}
		else
		{
			Column = Wrap(Column + dx, Columns);
		}
	}

	/// <summary>Select the key at <paramref name="row"/>, <paramref name="column"/> (the mouse is on it).</summary>
	public void Select(int row, int column)
	{
		Row = Clamp(row, 0, Rows - 1);
		Column = Clamp(column, 0, Columns - 1);
	}

	/// <summary>The key at <paramref name="row"/>, <paramref name="column"/> is the selected one (a bottom-row key whichever of its columns).</summary>
	public bool IsSelected(int row, int column)
	{
		if (row != Row)
		{
			return false;
		}
		return (row == BottomRow) ? BottomIndex(column) == BottomIndex(Column) : column == Column;
	}

	/// <summary>Press the selected key: type its character, or Shift, Space or Delete. True when it's Done.</summary>
	public bool Press()
	{
		switch (Selected)
		{
		case Key.Shift:
			ToggleShift();
			return false;
		case Key.Space:
			Type(' ');
			return false;
		case Key.Delete:
			Backspace();
			return false;
		case Key.Done:
			return true;
		default:
			Type(CharAt(Row, Column));
			return false;
		}
	}

	/// <summary>The character the key at <paramref name="row"/>, <paramref name="column"/> types now (Shift decides which).</summary>
	public char CharAt(int row, int column)
	{
		return (Shift ? UpperKeys : LowerKeys)[row * Columns + column];
	}

	/// <summary>The face of the character key at <paramref name="row"/>, <paramref name="column"/> now, as a string made once.</summary>
	public string Face(int row, int column)
	{
		return (Shift ? UpperFaces : LowerFaces)[row * Columns + column];
	}

	/// <summary>Which of the bottom row's keys spans <paramref name="column"/>.</summary>
	private static int BottomIndex(int column)
	{
		for (int key = BottomKeys.Length - 1; key > 0; key--)
		{
			if (column >= BottomStarts[key])
			{
				return key;
			}
		}
		return 0;
	}

	private static int Wrap(int i, int count)
	{
		return (i % count + count) % count;
	}

	private static int Clamp(int value, int min, int max)
	{
		return (value < min) ? min : ((value > max) ? max : value);
	}

	private static string[] Faces(string keys)
	{
		string[] faces = new string[keys.Length];
		for (int i = 0; i < keys.Length; i++)
		{
			faces[i] = keys[i].ToString();
		}
		return faces;
	}
}
