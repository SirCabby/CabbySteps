using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using UnityEngine;

namespace CabbySteps;

/// <summary>
/// The POI list, pois.json in Mods/CabbySteps/ (tools/georef/build_pois.py): Map Genie's
/// places, each with where it's drawn on the game's painted map (u, v in the ending's units) and
/// where it is in the world. The map itself is the game's (<see cref="MapArt"/>).
/// </summary>
public class MapAssets
{
	private readonly MelonLogger.Instance _log;

	public List<Poi> Pois { get; } = new List<Poi>();

	public bool Ready => Pois.Count > 0;

	public MapAssets(MelonLogger.Instance log)
	{
		_log = log;
	}

	/// <summary>Read pois.json, placing each POI on the map with <paramref name="projection"/>.</summary>
	public void Load(MapProjection projection)
	{
		try
		{
			if (!File.Exists(ModPaths.Pois))
			{
				_log.Error("pois.json not found at " + ModPaths.Pois);
				return;
			}
			Pois.Clear();
			int exact = 0;
			int linked = 0;
			int movable = 0;
			int unplaced = 0;
			foreach (Dictionary<string, object> item in MiniJson.ParseArray(File.ReadAllText(ModPaths.Pois)))
			{
				if (!item.ContainsKey("u") || !item.ContainsKey("v"))
				{
					unplaced++;
					continue;
				}
				Vector2 px = projection.UnitsToMap((float)MiniJson.Num(item, "u"), (float)MiniJson.Num(item, "v"));
				Poi poi = new Poi
				{
					Id = (long)MiniJson.Num(item, "id"),
					Cat = (int)MiniJson.Num(item, "cat"),
					CatName = MiniJson.Str(item, "catName"),
					Icon = MiniJson.Str(item, "icon"),
					Title = MiniJson.Str(item, "title"),
					Px = px.x,
					Py = px.y,
					World = new Vector3((float)MiniJson.Num(item, "wx"), (float)MiniJson.Num(item, "wy"), (float)MiniJson.Num(item, "wz")),
					HasHeight = item.ContainsKey("wy"),
					Item = MiniJson.Str(item, "item")
				};
				if (MiniJson.List(item, "found") is List<object> found)
				{
					poi.Found = found.OfType<string>().ToArray();
					linked++;
				}
				if (MiniJson.List(item, "gone") is List<object> gone)
				{
					poi.Gone = gone.OfType<string>().ToArray();
				}
				if (item.TryGetValue("best", out object bestValue) && bestValue is Dictionary<string, object> best
					&& MiniJson.Str(best, "key") is string bestKey && MiniJson.Num(best, "under") > 0.0)
				{
					poi.BestKey = bestKey;
					poi.BestUnder = (float)MiniJson.Num(best, "under");
					linked++;
				}
				poi.ReferenceOnly = Array.IndexOf(Poi.ReferenceCategories, poi.CatName) >= 0;
				poi.HomePx = poi.Px;
				poi.HomePy = poi.Py;
				poi.HomeWorld = poi.World;
				poi.HomeHasHeight = poi.HasHeight;
				if (poi.HasHeight)
				{
					exact++;
				}
				if (poi.Item != null)
				{
					movable++;
				}
				Pois.Add(poi);
			}
			_log.Msg($"Loaded {Pois.Count} POIs ({exact} placed by the game's own projection, {Pois.Count - exact} on Map Genie's marker; {linked} ticked off by the game's own records, {movable} movable)");
			if (unplaced > 0)
			{
				_log.Warning($"{unplaced} POIs in pois.json have no map position (u, v), so they're left out: is pois.json from an older version of the mod?");
			}
		}
		catch (Exception ex)
		{
			_log.Error("Failed to load pois.json: " + ex);
		}
	}
}
