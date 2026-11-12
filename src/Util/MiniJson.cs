using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CabbySteps;

/// <summary>
/// Minimal JSON reader for the mod's own files. Objects become
/// Dictionary&lt;string, object&gt;, arrays List&lt;object&gt;, numbers double.
/// It trusts its input: no validation and little error recovery.
/// </summary>
public static class MiniJson
{
	/// <summary>Parse a top-level array, keeping only its object elements.</summary>
	public static List<Dictionary<string, object>> ParseArray(string json)
	{
		List<Dictionary<string, object>> result = new List<Dictionary<string, object>>();
		int i = 0;
		if (!(ParseValue(json, ref i) is List<object> items))
		{
			return result;
		}
		foreach (object item in items)
		{
			if (item is Dictionary<string, object> obj)
			{
				result.Add(obj);
			}
		}
		return result;
	}

	public static Dictionary<string, object> ParseObject(string json)
	{
		int i = 0;
		return ParseValue(json, ref i) as Dictionary<string, object>;
	}

	public static double Num(Dictionary<string, object> o, string key)
	{
		if (o != null && o.TryGetValue(key, out var value) && value is double d)
		{
			return d;
		}
		return 0.0;
	}

	public static string Str(Dictionary<string, object> o, string key)
	{
		if (o != null && o.TryGetValue(key, out var value) && value is string s)
		{
			return s;
		}
		return null;
	}

	public static bool Bool(Dictionary<string, object> o, string key)
	{
		if (o != null && o.TryGetValue(key, out var value) && value is bool b)
		{
			return b;
		}
		return false;
	}

	public static List<object> List(Dictionary<string, object> o, string key)
	{
		if (o != null && o.TryGetValue(key, out var value) && value is List<object> list)
		{
			return list;
		}
		return null;
	}

	private static object ParseValue(string s, ref int i)
	{
		SkipWs(s, ref i);
		switch (s[i])
		{
		case '{':
			return ParseObj(s, ref i);
		case '[':
			return ParseArr(s, ref i);
		case '"':
			return ParseStr(s, ref i);
		case 't':
			i += 4;
			return true;
		case 'f':
			i += 5;
			return false;
		case 'n':
			i += 4;
			return null;
		default:
			return ParseNum(s, ref i);
		}
	}

	private static Dictionary<string, object> ParseObj(string s, ref int i)
	{
		Dictionary<string, object> obj = new Dictionary<string, object>();
		i++;
		SkipWs(s, ref i);
		if (s[i] == '}')
		{
			i++;
			return obj;
		}
		while (true)
		{
			SkipWs(s, ref i);
			string key = ParseStr(s, ref i);
			SkipWs(s, ref i);
			i++; // ':'
			obj[key] = ParseValue(s, ref i);
			SkipWs(s, ref i);
			if (s[i] != ',')
			{
				break;
			}
			i++;
		}
		i++; // '}'
		return obj;
	}

	private static List<object> ParseArr(string s, ref int i)
	{
		List<object> list = new List<object>();
		i++;
		SkipWs(s, ref i);
		if (s[i] == ']')
		{
			i++;
			return list;
		}
		while (true)
		{
			list.Add(ParseValue(s, ref i));
			SkipWs(s, ref i);
			if (s[i] != ',')
			{
				break;
			}
			i++;
		}
		i++; // ']'
		return list;
	}

	private static string ParseStr(string s, ref int i)
	{
		StringBuilder sb = new StringBuilder();
		i++;
		while (s[i] != '"')
		{
			char c = s[i++];
			if (c == '\\')
			{
				char esc = s[i++];
				switch (esc)
				{
				case 'n':
					sb.Append('\n');
					break;
				case 't':
					sb.Append('\t');
					break;
				case 'r':
					sb.Append('\r');
					break;
				case 'b':
					sb.Append('\b');
					break;
				case 'f':
					sb.Append('\f');
					break;
				case '/':
					sb.Append('/');
					break;
				case '"':
					sb.Append('"');
					break;
				case '\\':
					sb.Append('\\');
					break;
				case 'u':
				{
					int code = int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
					sb.Append((char)code);
					i += 4;
					break;
				}
				default:
					sb.Append(esc);
					break;
				}
			}
			else
			{
				sb.Append(c);
			}
		}
		i++;
		return sb.ToString();
	}

	private static double ParseNum(string s, ref int i)
	{
		int start = i;
		while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-' || s[i] == '+' || s[i] == '.' || s[i] == 'e' || s[i] == 'E'))
		{
			i++;
		}
		return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
	}

	private static void SkipWs(string s, ref int i)
	{
		while (i < s.Length && (s[i] == ' ' || s[i] == '\t' || s[i] == '\n' || s[i] == '\r'))
		{
			i++;
		}
	}
}
