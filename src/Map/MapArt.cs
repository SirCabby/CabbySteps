using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using MelonLoader;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace CabbySteps;

/// <summary>
/// The game's own painted map: the six sheets its ending pins up a wooden board (Map0 at the
/// bottom to Map5 at the summit), and the board, read from journey.json
/// (tools/georef/find_journey.py writes it from the ending scene). The sheets are the map and the
/// minimap (<see cref="MapDraw"/>) and the Hero's Journey's paper (<see cref="JourneyScreen"/>);
/// the game itself draws Nate's path on them in the units journey.json uses
/// (<see cref="MapProjection"/>).
/// <para>
/// None of the game's art comes with the mod: each painting is its material's texture, loaded
/// through the game's own Addressables by the key its catalog lists. The sheets are asked for once
/// the game is under way and kept (six 2048 x 2048 compressed textures, about 34 MB); the board
/// only while the Journey is open. Until a painting has arrived, or if it can't be had
/// (journey.json missing, a game update), its sheet is plain paper.
/// </para>
/// </summary>
public sealed class MapArt
{
	/// <summary>A sheet or the board: where it is, and its painting once loaded.</summary>
	public sealed class Piece
	{
		public string Name;

		/// <summary>The material's Addressables key, and the texture property holding the painting.</summary>
		public string Material;

		public string TextureProperty;

		/// <summary>Centre and size, in the ending's units.</summary>
		public float X;

		public float Y;

		public float W;

		public float H;

		/// <summary>The drop shadow behind a sheet, in the sheet's own units (none: W == 0).</summary>
		public float ShadowX;

		public float ShadowY;

		public float ShadowW;

		public float ShadowH;

		/// <summary>A sheet's place on the map, in map pixels (whole ones, so the sheets meet without a seam).</summary>
		public float MapX;

		public float MapY;

		public float MapW;

		public float MapH;

		/// <summary>The painting, or null while it loads or if it can't be had.</summary>
		public Texture Painting;

		internal AsyncOperationHandle<Material> Handle;

		internal bool Requested;

		internal bool Settled;
	}

	private readonly MelonLogger.Instance _log;

	private int _errors;

	/// <summary>Paintings asked for and not yet arrived (or failed).</summary>
	private int _pending;

	private bool _sheetsReported;

	public readonly List<Piece> Sheets = new List<Piece>();

	public Piece Board;

	/// <summary>The camera's starting height, its distance from the dots and its vertical field of view (degrees).</summary>
	public float CameraY = 0.305f;

	public float CameraDistance = 0.883f;

	public float Fov = 60f;

	/// <summary>Where the sheets are on the map: map pixels against the ending's units.</summary>
	public MapProjection Projection { get; private set; } = MapProjection.Plain;

	/// <summary>The sheets' paintings have been asked for (they stay for the session).</summary>
	public bool SheetsRequested { get; private set; }

	public MapArt(MelonLogger.Instance log)
	{
		_log = log;
	}

	/// <summary>Height of the ending's view at the dots (its units).</summary>
	public float ViewHeight => 2f * CameraDistance * MathF.Tan(Fov * 0.5f * Fast.Deg2Rad);

	/// <summary>Every painting asked for has arrived or failed.</summary>
	public bool Settled => _pending == 0;

	public int PaintingsLoaded
	{
		get
		{
			int count = (Board != null && Board.Painting != null) ? 1 : 0;
			foreach (Piece sheet in Sheets)
			{
				if (sheet.Painting != null)
				{
					count++;
				}
			}
			return count;
		}
	}

	/// <summary>Read journey.json. Without it the map still works, on a layout of plain paper (see <see cref="UsePlainLayout"/>).</summary>
	public void LoadLayout(string file)
	{
		Sheets.Clear();
		Board = null;
		try
		{
			if (!File.Exists(file))
			{
				_log.Warning("Map: " + Path.GetFileName(file) + " is missing, so the map is plain paper (Nate and the points are still in the right places).");
				UsePlainLayout();
				return;
			}
			Dictionary<string, object> root = MiniJson.ParseObject(File.ReadAllText(file));
			if (root != null && root.TryGetValue("camera", out object cameraValue) && cameraValue is Dictionary<string, object> camera)
			{
				CameraY = (float)MiniJson.Num(camera, "y");
				CameraDistance = Fast.Max((float)MiniJson.Num(camera, "distance"), 0.05f);
				Fov = Fast.Clamp((float)MiniJson.Num(camera, "fov"), 10f, 120f);
			}
			if (root != null && root.TryGetValue("board", out object boardValue) && boardValue is Dictionary<string, object> board)
			{
				Board = ReadPiece(board);
			}
			if (MiniJson.List(root, "sheets") is List<object> sheets)
			{
				foreach (object item in sheets)
				{
					if (item is Dictionary<string, object> sheet)
					{
						Sheets.Add(ReadPiece(sheet));
					}
				}
			}
			if (Sheets.Count == 0 || Board == null || Sheets.Exists(s => s.W <= 0f || s.H <= 0f))
			{
				throw new FormatException("no sheets, a sheet of no size, or no board");
			}
			Frame();
		}
		catch (Exception ex)
		{
			_log.Warning("Map: couldn't read " + Path.GetFileName(file) + " (" + ex.Message + "), so the map is plain paper (Nate and the points are still in the right places).");
			UsePlainLayout();
		}
	}

	/// <summary>The ending's layout (as of the game's 1.0 release), without its paintings.</summary>
	public void UsePlainLayout()
	{
		Sheets.Clear();
		CameraY = 0.305f;
		CameraDistance = 0.883f;
		Fov = 60f;
		Board = new Piece { Name = "Wood", X = 0.497f, Y = 3.68f, W = 1.4291f, H = 9.375f };
		for (int i = 0; i < 6; i++)
		{
			Sheets.Add(new Piece { Name = "Map" + i, X = 0.5f, Y = 0.6881f + 1.17188f * i, W = 1.17188f, H = 1.17188f, ShadowW = 1.00788f, ShadowH = 1f });
		}
		Frame();
	}

	/// <summary>The map's frame from the sheets (a map pixel is a texel of one), and each sheet's place on it.</summary>
	private void Frame()
	{
		int n = Sheets.Count;
		float[] x = new float[n];
		float[] y = new float[n];
		float[] w = new float[n];
		float[] h = new float[n];
		for (int i = 0; i < n; i++)
		{
			x[i] = Sheets[i].X;
			y[i] = Sheets[i].Y;
			w[i] = Sheets[i].W;
			h[i] = Sheets[i].H;
		}
		Projection = MapProjection.ForSheets(x, y, w, h);
		foreach (Piece sheet in Sheets)
		{
			Projection.Place(sheet.X, sheet.Y, sheet.W, sheet.H, out sheet.MapX, out sheet.MapY, out sheet.MapW, out sheet.MapH);
		}
	}

	private static Piece ReadPiece(Dictionary<string, object> o)
	{
		Piece piece = new Piece
		{
			Name = MiniJson.Str(o, "name"),
			Material = MiniJson.Str(o, "material"),
			TextureProperty = MiniJson.Str(o, "texture"),
			X = (float)MiniJson.Num(o, "x"),
			Y = (float)MiniJson.Num(o, "y"),
			W = (float)MiniJson.Num(o, "w"),
			H = (float)MiniJson.Num(o, "h")
		};
		if (o.TryGetValue("shadow", out object shadowValue) && shadowValue is Dictionary<string, object> shadow)
		{
			piece.ShadowX = (float)MiniJson.Num(shadow, "x");
			piece.ShadowY = (float)MiniJson.Num(shadow, "y");
			piece.ShadowW = (float)MiniJson.Num(shadow, "w");
			piece.ShadowH = (float)MiniJson.Num(shadow, "h");
		}
		return piece;
	}

	/// <summary>Ask the game for the sheets' paintings, once; they arrive over the next frames (see <see cref="Update"/>) and stay.</summary>
	public void RequestSheets()
	{
		if (SheetsRequested)
		{
			return;
		}
		SheetsRequested = true;
		foreach (Piece sheet in Sheets)
		{
			Request(sheet);
		}
		if (_pending == 0)
		{
			ReportSheets();
		}
	}

	/// <summary>Ask for the board's painting (the Hero's Journey is opening).</summary>
	public void RequestBoard()
	{
		if (Board != null)
		{
			Request(Board);
		}
	}

	/// <summary>Let the game have the board's painting back (the Journey has closed). The sheets stay: they're the map.</summary>
	public void ReleaseBoard()
	{
		if (Board == null || !Board.Requested)
		{
			return;
		}
		if (!Board.Settled)
		{
			_pending--;
		}
		Board.Painting = null;
		Board.Requested = false;
		Board.Settled = false;
		try
		{
			Let(Board.Handle);
		}
		catch (Exception ex)
		{
			Warn("can't let go of the painting " + Board.Name + ": " + ex.Message);
		}
		Board.Handle = null;
	}

	private void Request(Piece piece)
	{
		if (piece.Requested || string.IsNullOrEmpty(piece.Material) || string.IsNullOrEmpty(piece.TextureProperty))
		{
			return;
		}
		piece.Requested = true;
		_pending++;
		try
		{
			piece.Handle = Start(piece.Material);
		}
		catch (Exception ex)
		{
			Settle(piece, null);
			Warn("can't ask the game for the painting " + piece.Name + ": " + ex.Message);
		}
	}

	/// <summary>Every frame: pick up paintings that have arrived. Nothing to do once they're all in.</summary>
	public void Update()
	{
		if (_pending == 0)
		{
			return;
		}
		if (Board != null)
		{
			Poll(Board);
		}
		foreach (Piece sheet in Sheets)
		{
			Poll(sheet);
		}
		if (!_sheetsReported && SheetsRequested && SheetsSettled())
		{
			ReportSheets();
		}
	}

	private void Poll(Piece piece)
	{
		if (!piece.Requested || piece.Settled)
		{
			return;
		}
		try
		{
			if (!Done(piece.Handle))
			{
				return;
			}
			Settle(piece, PaintingOf(piece.Handle, piece.TextureProperty));
			if (piece.Painting == null)
			{
				Warn("the game didn't hand over the painting " + piece.Name + " (" + piece.Material + "); it's plain paper");
			}
		}
		catch (Exception ex)
		{
			Settle(piece, null);
			Warn("can't get the painting " + piece.Name + ": " + ex.Message);
		}
	}

	private void Settle(Piece piece, Texture painting)
	{
		piece.Painting = painting;
		piece.Settled = true;
		_pending--;
	}

	private bool SheetsSettled()
	{
		foreach (Piece sheet in Sheets)
		{
			if (sheet.Requested && !sheet.Settled)
			{
				return false;
			}
		}
		return true;
	}

	private void ReportSheets()
	{
		_sheetsReported = true;
		int painted = 0;
		foreach (Piece sheet in Sheets)
		{
			if (sheet.Painting != null)
			{
				painted++;
			}
		}
		string message = $"Map: {painted} of {Sheets.Count} painted sheets from the game";
		if (painted == Sheets.Count)
		{
			_log.Msg(message + ".");
		}
		else
		{
			_log.Warning(message + "; the rest are plain paper (Nate and the points are still in the right places).");
		}
	}

	private void Warn(string message)
	{
		if (_errors++ < 4)
		{
			_log.Warning("Map: " + message + ".");
		}
	}

	// The game calls are kept out of line so that, if a game update breaks these interop types,
	// the failure surfaces inside the callers' try blocks.

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static AsyncOperationHandle<Material> Start(string key)
	{
		return Addressables.LoadAssetAsync<Material>(key);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static bool Done(AsyncOperationHandle<Material> handle)
	{
		return handle == null || handle.IsDone;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static Texture PaintingOf(AsyncOperationHandle<Material> handle, string property)
	{
		if (handle == null || handle.Status != AsyncOperationStatus.Succeeded)
		{
			return null;
		}
		Material material = handle.Result;
		return (material != null) ? material.GetTexture(property) : null;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void Let(AsyncOperationHandle<Material> handle)
	{
		if (handle != null)
		{
			Addressables.Release<Material>(handle);
		}
	}
}
