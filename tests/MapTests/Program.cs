using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CabbySteps;
using MelonLoader;
using UnityEngine;

// Checks the mod's projection code against the shipped data, and its pure logic (map view,
// completed POIs kept with the game's save). Exit code 0 = all passed.
// usage: dotnet run --project tests/MapTests -- <repo root>

string root = args.Length > 0 ? args[0] : FindRoot();
string assets = Path.Combine(root, "package", "Mods", "CabbySteps");
int failures = 0;

void Check(bool ok, string what)
{
	Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what);
	if (!ok)
	{
		failures++;
	}
}

Console.WriteLine("world loop");
WorldLoop.Width = 512f;
Check(Near(WorldLoop.WrapX(-1f), 511f) && Near(WorldLoop.WrapX(1025f), 1f) && Near(WorldLoop.WrapX(647.9f), 135.9f), "WrapX folds any copy into [0, 512)");
Check(Near(WorldLoop.DeltaX(510f, 2f), 4f) && Near(WorldLoop.DeltaX(2f, 510f), -4f) && Near(WorldLoop.DeltaX(100f, 300f), 200f), "DeltaX takes the short way round");
Check(Near(WorldLoop.FlatDistance(new Vector3(647.9f, 0f, 200f), new Vector3(135.6f, 50f, 200f)), 0.3f), "a position one loop over is the same place");
Vector3 copy = WorldLoop.NearestCopy(new Vector3(10f, 5f, 700f), 1030f);
Check(Near(copy.x, 1034f) && Near(copy.y, 5f) && Near(copy.z, 700f), "NearestCopy puts a spot in the loop Nate is in");
Check(Near(WorldLoop.NearestCopy(new Vector3(500f, 0f, 0f), -20f).x, -12f) && Near(WorldLoop.NearestCopy(new Vector3(300f, 0f, 0f), 290f).x, 300f),
	"and takes the short way round the seam, or no detour at all");

Console.WriteLine("Fast (plain C# stand-ins for Unity's maths)");
Vector2 v2 = Fast.V2(1.5f, -2f);
Vector3 v3 = Fast.V3(1f, 2f, 3f);
Check(v2.x == 1.5f && v2.y == -2f && v3.x == 1f && v3.y == 2f && v3.z == 3f, "V2 and V3 build vectors field by field");
Check(Fast.RoundToInt(2.5f) == 2 && Fast.RoundToInt(3.5f) == 4 && Fast.RoundToInt(-2.5f) == -2 && Fast.RoundToInt(2.6f) == 3, "RoundToInt rounds half to even, as Mathf.RoundToInt does");
Check(Fast.Clamp(5f, 0f, 3f) == 3f && Fast.Clamp(-1f, 0f, 3f) == 0f && Fast.Clamp(7, 1, 3) == 3 && Fast.Clamp01(1.5f) == 1f && Fast.Abs(-2f) == 2f, "Clamp, Clamp01 and Abs");
Check(Fast.CeilToInt(1.01f) == 2 && Fast.CeilToInt(-1.5f) == -1 && Near(Fast.Atan2(1f, 0f) * Fast.Rad2Deg, 90f, 0.001f) && Near(Fast.Sqrt(9f), 3f), "CeilToInt, Atan2 and Sqrt");
Check(Fast.Approximately(1f, 1f + 1e-7f) && !Fast.Approximately(1f, 1.001f), "Approximately");
(float x, float y) Turned(Matrix4x4 m, float x, float y) => (m.m00 * x + m.m01 * y + m.m03, m.m10 * x + m.m11 * y + m.m13);
Matrix4x4 quarter = Fast.TurnAbout(90f, new Vector2(10f, 20f));
var quarterRight = Turned(quarter, 11f, 20f);
var pivotStays = Turned(quarter, 10f, 20f);
Matrix4x4 unturned = Fast.TurnAbout(0f, new Vector2(10f, 20f));
var unmoved = Turned(unturned, 37f, -4f);
Matrix4x4 slant = Fast.TurnAbout(-30f, new Vector2(400f, 300f));
var along = Turned(slant, 500f, 300f);
Check(Near(quarterRight.x, 10f) && Near(quarterRight.y, 21f) && Near(pivotStays.x, 10f) && Near(pivotStays.y, 20f) && Near(unmoved.x, 37f) && Near(unmoved.y, -4f)
	&& Near(along.x, 400f + 100f * MathF.Cos(-30f * Fast.Deg2Rad), 0.001f) && Near(along.y, 300f + 100f * MathF.Sin(-30f * Fast.Deg2Rad), 0.001f)
	&& quarter.m22 == 1f && quarter.m33 == 1f && quarter.m02 == 0f && quarter.m32 == 0f,
	"TurnAbout turns about the pivot, clockwise on the screen, as GUIUtility.RotateAroundPivot does");

Console.WriteLine("projection (the game's own: SaveGod.SaveForever)");
// The ending's layout, as journey.json has it: the map's frame and each sheet's place on it.
Dictionary<string, object> journeyFile = MiniJson.ParseObject(File.ReadAllText(Path.Combine(assets, "journey.json")));
List<Dictionary<string, object>> sheetList = MiniJson.List(journeyFile, "sheets").Cast<Dictionary<string, object>>().ToList();
float[] SheetField(string key) => sheetList.Select(s => (float)MiniJson.Num(s, key)).ToArray();
float[] sheetX = SheetField("x"), sheetY = SheetField("y"), sheetW = SheetField("w"), sheetH = SheetField("h");
MapProjection projection = MapProjection.ForSheets(sheetX, sheetY, sheetW, sheetH);
Check(sheetList.Count == 6 && Near(projection.Width, 2048f, 0.01f) && Near(projection.Height, 6f * 2048f, 1f),
	$"the six sheets make a map 2048 texels across and six sheets high ({projection.Width:F1} x {projection.Height:F1})");
Check(Near(projection.Left, MapProjection.Plain.Left, 1e-5f) && Near(projection.Top, MapProjection.Plain.Top, 1e-5f) && Near(projection.PxPerUnit, MapProjection.Plain.PxPerUnit, 0.01f)
	&& Near(projection.Width, MapProjection.Plain.Width, 0.05f) && Near(projection.Height, MapProjection.Plain.Height, 0.05f),
	"the built-in layout (for when journey.json can't be read) is journey.json's");
float[] mapY = new float[6], mapH = new float[6];
bool across = true;
for (int i = 0; i < 6; i++)
{
	projection.Place(sheetX[i], sheetY[i], sheetW[i], sheetH[i], out float mx, out mapY[i], out float mw, out mapH[i]);
	across &= mx == 0f && mw == 2048f;
}
bool meet = Enumerable.Range(0, 5).All(i => mapY[i] == mapY[i + 1] + mapH[i + 1]);
Check(across && meet && mapY[5] == 0f && mapY[0] + mapH[0] == MathF.Round(projection.Height) && mapH.All(hh => hh == 2048f),
	$"each sheet is a whole 2048 x 2048 texels, Map5 (the summit) on top, and they meet with no seam (tops {string.Join(", ", mapY.Select(v => v.ToString("0")))})");

Check(Near(MapProjection.U(new Vector3(256f, 100f, 1000f)), 0.5f, 1e-6f) && Near(MapProjection.V(new Vector3(256f, 100f, 1000f)), (50f + 866.0254f) / 512f, 1e-6f),
	"u = x / 512 across, v = (0.5 y + 0.866 z) / 512 up, as the game records Nate for its ending");
Vector2 a = projection.WorldToMap(new Vector3(135.6f, 20f, 200.1f));
Vector2 b = projection.WorldToMap(new Vector3(135.6f + 512f * 3f, 20f, 200.1f));
Vector2 bw = projection.WorldToMap(new Vector3(135.6f - 512f, 20f, 200.1f));
Check(Near(a.x, b.x, 0.01f) && Near(a.y, b.y, 0.01f) && Near(a.x, bw.x, 0.01f), "WorldToMap is the same in every copy of the loop");
Vector2 lowSpot = projection.WorldToMap(new Vector3(300f, 400f, 1200f));
Vector2 highSpot = projection.WorldToMap(new Vector3(300f, 410f, 1200f));
Vector2 north = projection.WorldToMap(new Vector3(300f, 400f, 1210f));
float pxPerM = projection.PxPerMetre;
Check(Near(highSpot.x, lowSpot.x, 1e-3f) && Near(lowSpot.y - highSpot.y, 5f * pxPerM, 0.01f) && Near(lowSpot.y - north.y, 8.660254f * pxPerM, 0.01f),
	$"10 m higher is drawn 5 m of map further up, 10 m north 8.66 m: the sheets look down at 60 degrees ({pxPerM:F3} px a metre)");
Vector2 corner = projection.UnitsToMap(projection.Left, projection.Top);
Vector2 farCorner = projection.UnitsToMap(projection.Left + projection.Width / projection.PxPerUnit, projection.Top - projection.Height / projection.PxPerUnit);
Check(Near(corner.x, 0f) && Near(corner.y, 0f) && Near(farCorner.x, projection.Width, 0.01f) && Near(farCorner.y, projection.Height, 0.01f),
	"map pixel (0, 0) is the top sheet's top-left corner, and y grows down the map");
Vector2 clamped = projection.Clamp(Fast.V2(-5f, 99999f));
Check(clamped.x == 0f && clamped.y == projection.Height, "Clamp keeps a map pixel on the sheets");
float facingNorth = MapProjection.MapAngle(new Vector3(0f, 0f, 1f));
float facingEast = MapProjection.MapAngle(new Vector3(1f, 0f, 0f));
float facingSouthEast = MapProjection.MapAngle(new Vector3(1f, 0f, -1f));
Check(Near(facingNorth, -90f, 1e-3f) && Near(facingEast, 0f, 1e-3f) && Near(facingSouthEast, MathF.Atan2(0.8660254f, 1f) * Fast.Rad2Deg, 1e-3f),
	$"facing north points straight up the map, east right, and south-east {facingSouthEast:F1} degrees below right (north is foreshortened)");

Console.WriteLine("Map Genie's map is the game's sheets (tools/georef/mapgenie.json)");
Dictionary<string, object> mg = MiniJson.ParseObject(File.ReadAllText(Path.Combine(root, "tools", "georef", "mapgenie.json")));
double mgSx = MiniJson.Num(mg, "sx"), mgOx = MiniJson.Num(mg, "ox"), mgSy = MiniJson.Num(mg, "sy"), mgOy = MiniJson.Num(mg, "oy");
Check(Math.Abs(mgSx - 0.375 * projection.PxPerUnit) < 0.05 && Math.Abs(mgSy - 0.375 * projection.PxPerUnit) < 0.1 && MiniJson.Num(mg, "residualMedianPx") < 0.1,
	$"it's the sheets scaled to 3/8 ({mgSx:F2} and {mgSy:F2} px a unit; lined up to {MiniJson.Num(mg, "residualMedianPx"):F2} px)");
(double u, double v) MarkerUnits(double px, double py) => ((px - mgOx) / mgSx, (mgOy - py) / mgSy);

Console.WriteLine("anchors (game objects behind Map Genie's markers)");
Dictionary<string, object> anchorFile = MiniJson.ParseObject(File.ReadAllText(Path.Combine(root, "tools", "georef", "anchors.json")));
List<double> metresOff = new List<double>();
foreach (object item in MiniJson.List(anchorFile, "anchors"))
{
	Dictionary<string, object> anchor = (Dictionary<string, object>)item;
	// The marker's copy of the loop: a few are in the sheets' torn margins.
	double loops = MiniJson.Num(anchor, "loop");
	Vector3 at = new Vector3((float)MiniJson.Num(anchor, "x"), (float)MiniJson.Num(anchor, "y"), (float)MiniJson.Num(anchor, "z"));
	var marker = MarkerUnits(MiniJson.Num(anchor, "px"), MiniJson.Num(anchor, "py"));
	double du = MapProjection.U(at) + loops - marker.u;
	double dv = MapProjection.V(at) - marker.v;
	metresOff.Add(Math.Sqrt(du * du + dv * dv) * MapProjection.MetresPerUnit);
}
metresOff.Sort();
double medianOff = metresOff[metresOff.Count / 2];
double p90Off = metresOff[(int)(metresOff.Count * 0.9) - 1];
Check(metresOff.Count > 100 && medianOff < 5.0 && p90Off < 15.0,
	$"the game's projection puts {metresOff.Count} game objects on Map Genie's markers: median {medianOff:F1} m of map, 90% within {p90Off:F1} m (the markers were placed by hand)");

Console.WriteLine("pois.json");
List<Dictionary<string, object>> pois = MiniJson.ParseArray(File.ReadAllText(Path.Combine(assets, "pois.json")));
Check(pois.Count > 200 && pois.All(p => p.ContainsKey("u") && p.ContainsKey("v") && p.ContainsKey("wx") && p.ContainsKey("wz") && p.ContainsKey("mgPx") && !p.ContainsKey("px")),
	$"all {pois.Count} POIs have a place on the game's map (u, v), a world position and Map Genie's marker");
Vector2 Drawn(Dictionary<string, object> p) => projection.UnitsToMap((float)MiniJson.Num(p, "u"), (float)MiniJson.Num(p, "v"));
Vector3 WorldOf(Dictionary<string, object> p) => new Vector3((float)MiniJson.Num(p, "wx"), (float)MiniJson.Num(p, "wy"), (float)MiniJson.Num(p, "wz"));
double Px(Vector2 p, Vector2 q) => Math.Sqrt((p.x - q.x) * (p.x - q.x) + (p.y - q.y) * (p.y - q.y));
// Off its own spot only when fanned out of a stack of markers: up to build_pois.py's STACKED_U
// from the one that keeps the spot, and FAN_U round that (in map pixels).
float fan = (0.006f + 0.009f) * projection.PxPerUnit * 1.01f;
List<Dictionary<string, object>> placedByGame = pois.Where(p => p.ContainsKey("wy")).ToList();
double FromWorld(Dictionary<string, object> p) => Px(Drawn(p), projection.WorldToMap(WorldOf(p)));
List<Dictionary<string, object>> astray = placedByGame.Where(p => FromWorld(p) > fan).ToList();
Check(placedByGame.Count >= 160 && astray.Count == 0,
	$"every POI the game has a place for is drawn where the game's projection puts it, where Nate's arrow is when he's there ({placedByGame.Count}; "
	+ (astray.Count == 0 ? $"{placedByGame.Count(p => FromWorld(p) < 0.5)} exactly, the rest fanned out of stacks)" : "astray: " + string.Join(", ", astray.Select(p => MiniJson.Str(p, "title"))) + ")"));
List<Dictionary<string, object>> fromMarkers = pois.Where(p => !p.ContainsKey("wy")).ToList();
double OffMarker(Dictionary<string, object> p)
{
	var m = MarkerUnits(MiniJson.Num(p, "mgPx"), MiniJson.Num(p, "mgPy"));
	return Px(Drawn(p), projection.UnitsToMap((float)m.u, (float)m.v));
}
Check(fromMarkers.Count > 0 && fromMarkers.All(p => OffMarker(p) <= fan) && fromMarkers.All(p =>
	{
		var m = MarkerUnits(MiniJson.Num(p, "mgPx"), MiniJson.Num(p, "mgPy"));
		return Math.Abs(WorldLoop.DeltaX((float)(m.u * 512.0), (float)MiniJson.Num(p, "wx"))) < 0.05;
	}),
	$"the {fromMarkers.Count} with no game object are drawn on Map Genie's marker (on the same painting), and their x is the marker's");
Dictionary<string, object> boringCans = pois.Single(p => (long)MiniJson.Num(p, "id") == 488910);
Vector3 nateUnderCans = new Vector3(176.63f, 274.08f, 574.84f);
Vector2 nateAtCans = projection.WorldToMap(nateUnderCans);
Vector2 cansIcon = Drawn(boringCans);
float below = (float)MiniJson.Num(boringCans, "wy") - nateUnderCans.y;
Check(below > 3f && Px(nateAtCans, cansIcon) < 3.5f * pxPerM && nateAtCans.y > cansIcon.y,
	$"standing {below:F1} m under the can stack by the Boring Tower, Nate is drawn just below its icon ({Px(nateAtCans, cansIcon) / pxPerM:F1} m of map): it's on the ledge above him");
Dictionary<string, object> grapple = pois.Single(p => MiniJson.Str(p, "anchor") == "GrapplePoint");
Dictionary<string, object> chest = pois.Single(p => MiniJson.Str(p, "title") == "Chest opening");
Dictionary<string, object> chestLandmark = pois.Single(p => MiniJson.Str(p, "title") == "Chest");
Check(FromWorld(grapple) < 0.5 && MiniJson.Num(grapple, "u") < 0.5 && MarkerUnits(MiniJson.Num(grapple, "mgPx"), 0).u > 1.0,
	"the Grapple Point, marked in the east margin, is drawn where Nate is at it, on the west side");
Check(FromWorld(chest) <= fan && Px(Drawn(chest), Drawn(chestLandmark)) < 2f * fan && MarkerUnits(MiniJson.Num(chest, "mgPx"), 0).u < 0.0,
	"the Chest opening is drawn at the chest, not in the west margin");

// Where the game has a POI (its cutscene's trigger, its campfire), the POI is: not where Map Genie
// put the marker. Drawn where Nate is when he's there, give or take the fan-out of stacked markers.
List<Dictionary<string, object>> placed = pois.Where(p => p.ContainsKey("at")).ToList();
double worstPlaced = placed.Max(p => FromWorld(p));
Check(placed.Count >= 60 && worstPlaced <= fan && placed.All(p => p.ContainsKey("wy")),
	$"{placed.Count} POIs are where the game sets them off or has its campfire, drawn there (worst {worstPlaced:F1} px, fanned)");
Dictionary<string, object> PoiById(long id) => pois.Single(p => (long)MiniJson.Num(p, "id") == id);
Dictionary<string, object> fortressCamp = PoiById(488983), wishing = PoiById(488984), fortressMorning = PoiById(488985);
double Apart(Dictionary<string, object> p, Dictionary<string, object> q) => WorldLoop.FlatDistance(
	new Vector3((float)MiniJson.Num(p, "wx"), 0f, (float)MiniJson.Num(p, "wz")), new Vector3((float)MiniJson.Num(q, "wx"), 0f, (float)MiniJson.Num(q, "wz")));
Check(MiniJson.Str(fortressCamp, "at") == "campfire FortressCampFirePrefab" && Apart(fortressCamp, wishing) < 1.0 && Apart(fortressCamp, fortressMorning) < 1.0
	&& Math.Abs(MiniJson.Num(fortressCamp, "wz") - 1296.3) < 1.0,
	"the fortress camp, its wishing party and its morning are at the fortress's campfire, 80 m from Map Genie's gate");
Dictionary<string, object> secret = PoiById(489054);
var peak = MarkerUnits(MiniJson.Num(secret, "mgPx"), MiniJson.Num(secret, "mgPy"));
Check(Math.Abs(MiniJson.Num(secret, "wz") - 3197.4) < 1.0 && FromWorld(secret) < 0.5 && Px(Drawn(secret), projection.UnitsToMap((float)peak.u, (float)peak.v)) < 30f * pxPerM,
	"the Secret Ending is at the summit's trigger, drawn by the game's projection on the painted peak");
// A hat or item is drawn at its loader, where the game starts it, however far Map Genie's marker
// is (the ice cream is had at Mitchell's cart; the chicks and the duckling wander). The Paper Hat
// starts in the river at x 481, and Map Genie marked it 60 m along the river, across the seam.
List<Dictionary<string, object>> atLoaders = pois.Where(p => MiniJson.Str(p, "item") != null && MiniJson.Str(p, "catName") != "Follower" && MiniJson.Str(p, "item") != "IceTrophy_Loader").ToList();
Check(atLoaders.Count == 26 && atLoaders.All(p => MiniJson.Str(p, "anchor") == MiniJson.Str(p, "item") && p.ContainsKey("wy") && FromWorld(p) <= fan),
	$"every hat and item ({atLoaders.Count}) is drawn at its loader, where the game starts it");
Dictionary<string, object> paperHat = PoiById(489018);
var paperMarker = MarkerUnits(MiniJson.Num(paperHat, "mgPx"), MiniJson.Num(paperHat, "mgPy"));
float paperOff = Math.Abs(WorldLoop.DeltaX((float)(paperMarker.u * 512.0), (float)MiniJson.Num(paperHat, "wx")));
Check(Math.Abs(MiniJson.Num(paperHat, "wx") - 481.15) < 0.05 && Math.Abs(MiniJson.Num(paperHat, "wz") - 1761.96) < 0.05 && FromWorld(paperHat) < 0.5 && paperOff > 50f,
	$"the Paper Hat is drawn in the river where it starts, not on Map Genie's marker {paperOff:F0} m across from it");
List<Vector2> drawn = pois.Select(Drawn).ToList();
double closest = drawn.SelectMany((p, i) => drawn.Skip(i + 1).Select(q => Px(p, q))).Min();
Check(closest >= 0.006f * projection.PxPerUnit * 0.99f, $"no two markers are on the same spot, so the crosshair can pick each (closest {closest:F1} px, {closest / pxPerM:F1} m of map)");

Console.WriteLine("links to the game's records (pois.json found/item)");
string[] Found(Dictionary<string, object> p) => MiniJson.List(p, "found")?.OfType<string>().ToArray();
foreach (string cat in new[] { "Fruit", "Cutscene", "Stack of Cans", "Phone" })
{
	List<Dictionary<string, object>> inCat = pois.Where(p => MiniJson.Str(p, "catName") == cat).ToList();
	Check(inCat.Count > 0 && inCat.All(p => Found(p)?.Length > 0), $"every {cat} ({inCat.Count}) is ticked off by a game record");
}
List<Dictionary<string, object>> things = pois.Where(p => new[] { "Hat", "Key Item", "Quest Item", "Follower" }.Contains(MiniJson.Str(p, "catName"))).ToList();
Check(things.Where(p => MiniJson.Str(p, "catName") != "Hat").All(p => Found(p) == null), "key items, quest items and followers are never ticked off by themselves");
string[] dreams = { "stetson", "boat", "pinnochio", "frog", "box", "goPro", "bucket", "trophy", "propeller", "crown", "donkey", "construction", "glases" };
List<Dictionary<string, object>> hats = things.Where(p => MiniJson.Str(p, "catName") == "Hat").ToList();
Check(hats.Count(p => Found(p)?.Length == 1 && dreams.Contains(Found(p)[0])) == 13 && hats.Where(p => Found(p) == null).Select(p => MiniJson.Str(p, "title")).OrderBy(t => t).SequenceEqual(new[] { "Pacifier", "Winter Hat" }),
	"13 hats tick off when their dream plays; the Pacifier and Winter Hat have none");
Check(things.All(p => MiniJson.Str(p, "item") != null), $"every hat, key and quest item and follower ({things.Count}) follows its real object");
Check(pois.Where(p => new[] { "Point of Interest", "Graffiti" }.Contains(MiniJson.Str(p, "catName"))).All(p => Found(p) == null), "Points of Interest and Graffiti (for reference only) are never ticked off");
string[] Gone(Dictionary<string, object> p) => MiniJson.List(p, "gone")?.OfType<string>().ToArray();
// The ice cream Mitchell hands out is the IceTrophy loader's object (an IceMelter). Handed in, the
// game records its hand-in cutscene and "IceTrophy_used" (what IceTrophyHelper checks).
Dictionary<string, object> iceCream = things.SingleOrDefault(p => MiniJson.Str(p, "title") == "Ice Cream");
Check(iceCream != null && MiniJson.Str(iceCream, "item") == "IceTrophy_Loader" && Found(iceCream) == null
	&& Gone(iceCream) is string[] iceGone && iceGone.OrderBy(k => k, StringComparer.Ordinal).SequenceEqual(new[] { "IceTrophy_used", "TrophyIceDropOff" }),
	"the Ice Cream follows the IceTrophy loader's object and leaves the map once handed in");
List<Dictionary<string, object>> quests = pois.Where(p => MiniJson.Str(p, "catName") == "Quest Item").ToList();
Check(quests.All(p => Gone(p)?.Length > 0 && MiniJson.Str(p, "item") != null), $"each quest item ({quests.Count}) leaves the map once handed in");
List<string> canKeys = pois.Where(p => MiniJson.Str(p, "catName") == "Stack of Cans").Select(p => Found(p)[0]).ToList();
Check(canKeys.Distinct().Count() == canKeys.Count && canKeys.All(k => System.Text.RegularExpressions.Regex.IsMatch(k, "^CANS[0-9]+[.][0-9]+$")),
	"each can pile has its own CANS<x>.<z> key");
Check(new[] { "CANS55.197", "CANS111.257", "CANS286.212", "CANS371.226" }.All(canKeys.Contains), "the can keys seen in real saves are among them");
Check(pois.All(p => (Found(p) ?? new string[0]).Concat(Gone(p) ?? new string[0]).All(k => k.Length > 0 && !k.StartsWith("$"))),
	"every record is a plain key in the save");
Check(pois.Where(p => MiniJson.Str(p, "item") != null).All(p => MiniJson.Str(p, "item").Contains("_Loader")), "movable items name their loader");
// The Greatest: the game keeps nothing but the gym's best time ("GymPB" in the save's miscNums);
// the achievement is a best under JesseGym.developerTime, 78.24 s. A real save had 52.83.
List<Dictionary<string, object>> bests = pois.Where(p => p.ContainsKey("best")).ToList();
Dictionary<string, object> greatestBest = (bests.Count == 1 && MiniJson.Str(bests[0], "title") == "The Greatest") ? bests[0]["best"] as Dictionary<string, object> : null;
Check(greatestBest != null && MiniJson.Str(greatestBest, "key") == "GymPB" && Math.Abs(MiniJson.Num(greatestBest, "under") - 78.24) < 1e-9 && Found(bests[0]) == null,
	"The Greatest (only it) is ticked off by the gym's best time GymPB under the designer's 78.24 s");

Console.WriteLine("found by the game vs marked by hand");
{
	string temp2 = Path.Combine(Path.GetTempPath(), "cabbysteps-tests-" + Guid.NewGuid().ToString("N"));
	Directory.CreateDirectory(temp2);
	try
	{
		string file = Path.Combine(temp2, "CabbySteps_completed0.json");
		Poi apple = new Poi { Id = 488867, Title = "Apple" };
		Poi graffiti = new Poi { Id = 488858, Title = "Graffiti" };
		PoiProgress p = new PoiProgress(new MelonLogger.Instance());
		p.Attach(file, "save0.sav", 10f, newGame: false);
		int before = p.Version;
		p.SetFound(new HashSet<long> { apple.Id });
		Check(p.IsDone(apple) && p.IsFound(apple) && p.Count == 1 && p.Version != before, "a POI the game records counts as completed");
		Check(p.Toggle(apple) && p.IsDone(apple), "and can't be unmarked by hand");
		p.Toggle(graffiti);
		Check(p.Count == 2 && !p.IsFound(graffiti), "hand marks and game finds add up");
		p.Commit(20f);
		string written = File.ReadAllText(file);
		Check(written.Contains(graffiti.Id.ToString()) && !written.Contains(apple.Id.ToString()), "only hand marks go in the marks file; the save holds the rest");
		p.SetFound(new HashSet<long>());
		Check(!p.IsDone(apple) && p.IsDone(graffiti), "a save without the record (say, an older one) has the POI open again");
		Poi cup = new Poi { Id = 488860, Title = "Cup", Item = "DadCup_Loader" };
		p.Toggle(cup);
		int done = p.Count;
		p.SetGone(new HashSet<long> { cup.Id });
		Check(p.IsGone(cup) && p.GoneCount == 1 && p.Count == done - 1, "an item that left the game drops out of the count");

		// Movable items can't be ticked off by hand, and old hand marks on them are dropped.
		p.SetUnmarkable(new[] { cup.Id });
		p.Commit(30f);
		PoiProgress later = new PoiProgress(new MelonLogger.Instance());
		later.SetUnmarkable(new[] { cup.Id });
		later.Attach(file, "save0.sav", 30f, newGame: false);
		Check(!later.IsDone(cup) && later.IsDone(graffiti), "an old hand mark on an item is dropped on load");
		Check(!later.Toggle(cup) && !later.IsDone(cup), "an item can't be ticked off by hand");

		// A record the game can write over (the gym's best time, for The Greatest) is kept as a mark,
		// but only for the save it was read from.
		Poi greatest = new Poi { Id = 488949, Title = "The Greatest", BestKey = "GymPB", BestUnder = 78.24f };
		string file2 = Path.Combine(temp2, "CabbySteps_completed1.json");
		PoiProgress kept = new PoiProgress(new MelonLogger.Instance());
		kept.Keep(greatest, 7);
		Check(!kept.IsDone(greatest), "nothing is kept before a save is loaded");
		kept.Attach(file2, "save1.sav", 40f, newGame: false, save: 7);
		kept.SetFound(new HashSet<long> { greatest.Id });
		int version = kept.Version;
		kept.Keep(greatest, 8);
		kept.SetFound(new HashSet<long>());
		Check(!kept.IsDone(greatest) && kept.Version == version + 1, "a record read from another save object isn't kept");
		kept.SetFound(new HashSet<long> { greatest.Id });
		kept.Keep(greatest, 7);
		kept.SetFound(new HashSet<long>());
		Check(kept.IsDone(greatest) && !kept.IsFound(greatest), "once the save has had it, it stays completed when the record goes (a slower run written over the best)");
		kept.Commit(50f);
		Check(File.ReadAllText(file2).Contains(greatest.Id.ToString()), "and goes in the marks file, with the save");
		PoiProgress reloaded = new PoiProgress(new MelonLogger.Instance());
		reloaded.Attach(file2, "save1.sav", 50f, newGame: false, save: 9);
		Check(reloaded.IsDone(greatest), "so it's still completed when that save is loaded again");
		reloaded.Attach(file2, "save1.sav", 45f, newGame: false, save: 10);
		Check(!reloaded.IsDone(greatest), "but not in an older save from before it");
	}
	finally
	{
		Directory.Delete(temp2, recursive: true);
	}
}

Console.WriteLine("saved locations");
{
	string temp3 = Path.Combine(Path.GetTempPath(), "cabbysteps-tests-" + Guid.NewGuid().ToString("N"));
	Directory.CreateDirectory(temp3);
	try
	{
		string file = Path.Combine(temp3, "CabbySteps_locations.json");
		Func<Vector3, Vector2> toMap = w => projection.WorldToMap(w);
		SavedLocations locations = new SavedLocations(file, toMap, new MelonLogger.Instance());
		locations.Load();
		Check(locations.Count == 0 && locations.All.Count == 0 && locations.Quick == null && locations.Back == null && !File.Exists(file), "no file means no saved locations");
		Check(locations.Step(null, 1) == null, "nothing to step through yet");

		SavedLocation one = locations.Add(new Vector3(135.6f, 42.5f, 200.1f));
		SavedLocation two = locations.Add(new Vector3(135.6f + 512f * 2f, 80.25f, 900f));
		SavedLocation three = locations.Add(new Vector3(-20f, 120f, 1500f));
		Check(one.Title == "Location 1" && two.Title == "Location 2" && three.Title == "Location 3" && locations.Count == 3, "locations are numbered 1, 2, 3");
		Check(File.Exists(file) && locations.Writes == 3, "each one is written straight away");
		Check(Near(two.World.x, 135.6f, 0.01f) && Near(three.World.x, 492f, 0.01f), "x is kept in the canonical loop");
		Vector2 px = projection.WorldToMap(new Vector3(135.6f, 80.25f, 900f));
		Check(Near(two.Px, px.x, 0.01f) && Near(two.Py, px.y, 0.01f) && two.HasHeight, "a location sits on the map where its world position projects");
		Check(one.Id < 0 && two.Id < 0 && one.Id != two.Id && !one.Completable && !one.HandMarkable && one.Cat == SavedLocations.Category,
			"a location is a POI of its own: never a Map Genie id, never counted or ticked off");
		Check(one.IsKept && !one.IsNamed && !one.IsQuick && !one.IsBack && one.Number == 1 && one.Phrase == "Location 1" && one.Badge == "1", "a location saved on the map is kept, and numbered");
		Check(locations.Step(null, 1) == one && locations.Step(null, -1) == three && locations.Step(three, 1) == one && locations.Step(one, -1) == three && locations.Step(one, 1) == two,
			"stepping goes round them in order");

		Check(locations.Remove(two) && !locations.Remove(two) && locations.Count == 2, "a location can be forgotten, once");
		SavedLocation again = locations.Add(new Vector3(300f, 10f, 400f));
		Check(again.Title == "Location 2" && locations.All[1] == again, "a new one takes the lowest free number");

		locations.SetBack(new Vector3(600f, 33.5f, 1234.5f));
		SavedLocation back = locations.Back;
		Check(back != null && back.IsBack && back.Title == SavedLocations.BackTitle && back.Badge == "" && back.Phrase == "where you were" && locations.All[locations.All.Count - 1] == back && locations.Count == 3,
			"\"where you were\" comes last, and isn't one of the kept ones");
		Check(Near(back.World.x, 88f, 0.01f), "it's kept in the canonical loop too");
		locations.SetBack(new Vector3(10f, 1f, 20f));
		Check(locations.Back == back && Near(back.World.z, 20f), "the next recall moves the same spot (tracking it follows)");

		SavedLocations reloaded = new SavedLocations(file, toMap, new MelonLogger.Instance());
		reloaded.Load();
		Check(reloaded.Count == 3 && reloaded.All.Select(l => l.Title).SequenceEqual(new[] { "Location 1", "Location 2", "Location 3", SavedLocations.BackTitle }),
			"they're all there after a restart, in order");
		SavedLocation reloadedOne = reloaded.All[0];
		Check(reloadedOne.World.x == one.World.x && reloadedOne.World.y == one.World.y && reloadedOne.World.z == one.World.z, "positions survive the file exactly");
		Check(reloaded.Back != null && reloaded.Back.World.z == 20f, "so does where you were");
		Check(reloaded.Remove(reloaded.Back) && reloaded.Back == null && reloaded.All.Count == 3, "\"where you were\" can be forgotten too");

		// Edited by hand: a renamed one, one without a number, a duplicate number, junk.
		File.WriteAllText(file, "{ \"locations\": [ { \"n\": 2, \"name\": \"Top of the \\\"big\\\" climb\", \"x\": 1, \"y\": 2, \"z\": 3 }, { \"x\": 4, \"y\": 5, \"z\": 6 }, { \"n\": 2, \"x\": 7, \"y\": 8, \"z\": 9 }, { \"n\": 5, \"x\": \"nope\" }, 42 ], \"quick\": 42, \"back\": null }");
		SavedLocations edited = new SavedLocations(file, toMap, new MelonLogger.Instance());
		edited.Load();
		Check(edited.Count == 3 && edited.All[2].Title == "Top of the \"big\" climb" && edited.All[2].IsNamed && edited.All[2].Number == 0 && edited.All[2].Badge == "TO",
			"a name changed by hand makes a named one, after the numbered ones, without its number");
		Check(edited.Quick == null && edited.All.Count == 3, "a quick save that isn't one is ignored, not the file");
		Check(edited.All[0].Number == 1 && Near(edited.All[0].World.z, 6f) && edited.All[1].Number == 2 && Near(edited.All[1].World.z, 9f),
			"the named one's number is free for another; entries without a usable one get free ones; broken entries are skipped");
		edited.Add(new Vector3(0f, 0f, 0f));
		SavedLocations renamed = new SavedLocations(file, toMap, new MelonLogger.Instance());
		renamed.Load();
		Check(renamed.Count == 4 && renamed.All[2].Number == 3 && renamed.All[3].Title == "Top of the \"big\" climb" && renamed.All[3].IsNamed,
			"and written back with it (quotes and all)");
		string editedText = File.ReadAllText(file);
		Check(editedText.Contains("{ \"name\": \"Top of the \\\"big\\\" climb\", \"x\"") && editedText.Contains("{ \"n\": 3, \"name\": \"Location 3\", \"x\""),
			"a named one is written without a number");

		// Which way Nate and the camera faced, and when: for going back just so.
		File.Delete(file);
		SavedLocations posed = new SavedLocations(file, toMap, new MelonLogger.Instance());
		posed.Load();
		DateTime morning = new DateTime(2026, 11, 9, 9, 0, 0, DateTimeKind.Utc);
		posed.Add(new Vector3(10f, 1f, 10f), new SavedPose(-135.25f, 44.5f, true), morning);
		posed.Add(new Vector3(20f, 2f, 20f), new SavedPose(90f, 92.75f, false), morning.AddMinutes(5));
		posed.SetBack(new Vector3(5f, 5f, 5f), new SavedPose(12.5f, 13.5f, true));
		SavedLocations posedAgain = new SavedLocations(file, toMap, new MelonLogger.Instance());
		posedAgain.Load();
		SavedLocation earlyAgain = posedAgain.All[0];
		Check(earlyAgain.Pose.Known && earlyAgain.Pose.Facing == -135.25f && earlyAgain.Pose.Camera == 44.5f && earlyAgain.Pose.CameraRight
			&& posedAgain.All[1].Pose.Facing == 90f && !posedAgain.All[1].Pose.CameraRight,
			"facing, camera and shoulder survive the file exactly");
		Check(earlyAgain.Saved == morning && posedAgain.All[1].Saved == morning.AddMinutes(5), "so does when each was saved");
		Check(posedAgain.Back.Pose.Known && posedAgain.Back.Pose.Facing == 12.5f && posedAgain.Back.Pose.CameraRight, "\"where you were\" keeps which way Nate faced too");

		// Where his planted feet were, from the saved position: for standing him back on that footing.
		SavedPose bothFeet = new SavedPose(30f, 31f, false);
		bothFeet.SetFoot(0, new Vector3(-0.125f, -0.96875f, 0.25f));
		bothFeet.SetFoot(1, new Vector3(0.1875f, -1.03125f, -0.0625f));
		SavedPose oneFoot = new SavedPose(40f, 41f, true);
		oneFoot.SetFoot(1, new Vector3(0.5f, -0.75f, 0f));
		SavedLocation stood = posedAgain.Add(new Vector3(40f, 4f, 40f), bothFeet, morning.AddMinutes(6));
		SavedLocation balanced = posedAgain.Add(new Vector3(50f, 5f, 50f), oneFoot, morning.AddMinutes(7));
		posedAgain.SetBack(new Vector3(6f, 6f, 6f), bothFeet);
		SavedLocations footed = new SavedLocations(file, toMap, new MelonLogger.Instance());
		footed.Load();
		SavedPose stoodPose = footed.All.First(l => l.Number == stood.Number).Pose;
		SavedPose balancedPose = footed.All.First(l => l.Number == balanced.Number).Pose;
		Check(stoodPose.Feet == 3 && stoodPose.Foot0 == new Vector3(-0.125f, -0.96875f, 0.25f) && stoodPose.Foot1 == new Vector3(0.1875f, -1.03125f, -0.0625f) && stoodPose.Facing == 30f,
			"both planted feet survive the file exactly");
		Check(balancedPose.Feet == 2 && !balancedPose.HasFoot(0) && balancedPose.HasFoot(1) && balancedPose.Foot(1) == new Vector3(0.5f, -0.75f, 0f),
			"a foot that wasn't planted stays unknown; the other is kept");
		Check(footed.Back.Pose.Feet == 3 && footed.Back.Pose.Foot0 == bothFeet.Foot0, "\"where you were\" keeps its footing too");

		// Each foot's turn, and the whole body (each ragdoll part, the roots), for standing him back exactly.
		bool SameTurn(Quaternion a, Quaternion b) => Near(a.x, b.x, 1e-6f) && Near(a.y, b.y, 1e-6f) && Near(a.z, b.z, 1e-6f) && Near(a.w, b.w, 1e-6f);
		SavedPose turned = new SavedPose(50f, 51f, false);
		turned.SetFoot(0, new Vector3(-0.0625f, -0.75f, 0.0625f));
		turned.SetFootTurn(0, new Quaternion(0f, 0.6f, 0f, 0.8f));
		turned.SetFoot(1, new Vector3(0.0625f, -0.75f, -0.0625f));
		turned.SetFootTurn(1, new Quaternion(0.28f, 0.96f, 0f, 0f));
		turned.Body = new SavedBody
		{
			Root = new Vector3(0f, -0.875f, 0.125f),
			RootTurn = new Quaternion(0f, 0.6f, 0f, 0.8f),
			// The ragdoll's own root doesn't follow him about: one in the game was 22 m from his hips.
			Puppet = new Vector3(21.875f, -0.5f, -3.25f),
			Parts = new[] { new Vector3(0f, 0.125f, 0f), new Vector3(0.25f, -0.5f, 0f), new Vector3(-0.25f, -0.5f, 0.125f) },
			PartTurns = new[] { new Quaternion(0f, 0f, 0f, 1f), new Quaternion(0.6f, 0f, 0f, 0.8f), new Quaternion(0f, 0f, 0.8f, 0.6f) }
		};
		SavedLocation perched = posedAgain.Add(new Vector3(60f, 6f, 60f), turned, morning.AddMinutes(8));
		SavedLocations bodies = new SavedLocations(file, toMap, new MelonLogger.Instance());
		bodies.Load();
		SavedPose perchedPose = bodies.All.First(l => l.Number == perched.Number).Pose;
		SavedBody perchedBody = perchedPose.Body;
		Check(perchedPose.FeetTurned == 3 && SameTurn(perchedPose.Foot0Turn, turned.Foot0Turn) && SameTurn(perchedPose.FootTurn(1), turned.Foot1Turn),
			"each planted foot's turn survives the file");
		Check(perchedBody != null && perchedBody.Parts.Length == 3 && perchedBody.Parts[2] == turned.Body.Parts[2] && SameTurn(perchedBody.PartTurns[1], turned.Body.PartTurns[1])
			&& perchedBody.Root == turned.Body.Root && SameTurn(perchedBody.RootTurn, turned.Body.RootTurn) && perchedBody.Puppet == turned.Body.Puppet,
			"and so does the whole body: every part where and as it was, and both roots");
		Check(bodies.All.First(l => l.Number == stood.Number).Pose.FeetTurned == 0 && bodies.All.First(l => l.Number == stood.Number).Pose.Body == null,
			"a location saved with feet but no turns or body has none");
		Check(footed.All.First(l => l.Number == 1).Pose.Feet == 0, "a location saved without feet has none");
		File.WriteAllText(file, "{ \"locations\": [ { \"n\": 1, \"x\": 1, \"y\": 2, \"z\": 3, \"facing\": 5, \"camera\": 6, \"feet\": [[1, 2], [0.1, -9, 0]] },"
			+ " { \"n\": 2, \"x\": 1, \"y\": 2, \"z\": 3, \"facing\": 5, \"camera\": 6, \"feet\": [null, [0.25, -1, 0.5], [9, 9, 9]] },"
			+ " { \"n\": 3, \"x\": 1, \"y\": 2, \"z\": 3, \"facing\": 5, \"camera\": 6, \"feet\": \"left\" },"
			+ " { \"n\": 4, \"x\": 1, \"y\": 2, \"z\": 3, \"facing\": 5, \"camera\": 6, \"feet\": [[0, -0.75, 0, 0, 0, 0, 0], [0, -0.75, 0, 0, 0, 0, 2]],"
			+ " \"body\": { \"root\": [0, -1, 0, 0, 0, 0, 1], \"puppet\": [0, -1, 0], \"parts\": [[0, 0, 0, 0, 0, 0, 1], [0, 0, 0]] } } ], \"back\": null }");
		SavedLocations oddFeet = new SavedLocations(file, toMap, new MelonLogger.Instance());
		oddFeet.Load();
		Check(oddFeet.Count == 4 && oddFeet.All[0].Pose.Known && oddFeet.All[0].Pose.Feet == 0 && oddFeet.All[1].Pose.Feet == 2
			&& oddFeet.All[1].Pose.Foot1 == new Vector3(0.25f, -1f, 0.5f) && oddFeet.All[2].Pose.Feet == 0,
			"feet that aren't three numbers, or are metres away, are ignored, not the location");
		Check(oddFeet.All[3].Pose.Feet == 2 && oddFeet.All[3].Pose.FeetTurned == 2 && Near(oddFeet.All[3].Pose.Foot1Turn.w, 1f) && oddFeet.All[3].Pose.Body == null,
			"a turn that's no turn at all drops that foot; turns are made unit length; a body with a part missing its turn is dropped whole");
		File.WriteAllText(file, "{ \"locations\": [ { \"n\": 1, \"x\": 1, \"y\": 2, \"z\": 3, \"facing\": 5, \"camera\": 6, \"feet\": [[0.25, -0.75, 0.125, 0, 0, 0, 1], null],"
			+ " \"body\": { \"root\": [0, -0.8, 0, 0, 0, 0, 1], \"puppet\": [21.875, -0.5, -3.25], \"parts\": [[0, 0.03125, 0, 0, 0, 0, 1]] } },"
			+ " { \"n\": 2, \"x\": 1, \"y\": 2, \"z\": 3, \"facing\": 5, \"camera\": 6, \"feet\": [[0.25, -0.75, 0.125, 0, 0, 0, 1], null],"
			+ " \"body\": { \"root\": [0, -0.8, 0, 0, 0, 0, 1], \"puppet\": [20000, 0, 0], \"parts\": [[0, 0.03125, 0, 0, 0, 0, 1]] } } ], \"back\": null }");
		SavedLocations farPuppet = new SavedLocations(file, toMap, new MelonLogger.Instance());
		farPuppet.Load();
		SavedBody farBody = farPuppet.All[0].Pose.Body;
		Check(farBody != null && farBody.Puppet == new Vector3(21.875f, -0.5f, -3.25f) && farBody.Parts.Length == 1,
			"the ragdoll's own root, metres from his hips (it doesn't follow him about), loads with the body");
		Check(farPuppet.All[1].Pose.Body == null && farPuppet.All[1].Pose.Feet == 1, "one off beyond the world drops the body, not the footing");
		File.WriteAllText(file, "{ \"locations\": [ { \"n\": 1, \"name\": \"Old\", \"x\": 1, \"y\": 2, \"z\": 3 }, { \"n\": 3, \"x\": 4, \"y\": 5, \"z\": 6 } ], \"back\": { \"x\": 7, \"y\": 8, \"z\": 9 } }");
		SavedLocations older = new SavedLocations(file, toMap, new MelonLogger.Instance());
		older.Load();
		Check(older.Count == 2 && !older.All[0].Pose.Known && !older.Back.Pose.Known && older.All[0].Saved == DateTime.MinValue && older.All[0].Number == 3
			&& older.All[1].Title == "Old" && older.All[1].IsNamed,
			"a file from before facings were kept still loads: no facing, no saving time");
		Check(older.Quick == null && older.All.Count == 3, "a file from before the quick save loads with none");

		// The quick save: one spot, saved over by every F5 while playing, so they don't pile up.
		File.Delete(file);
		SavedLocations quicks = new SavedLocations(file, toMap, new MelonLogger.Instance());
		quicks.Load();
		SavedLocation first = quicks.Add(new Vector3(1f, 2f, 3f));
		SavedPose quickPose = new SavedPose(15f, 16f, true);
		quickPose.SetFoot(0, new Vector3(-0.125f, -1f, 0.25f));
		SavedLocation quick = quicks.SetQuick(new Vector3(100f + 512f, 20f, 300f), quickPose, morning);
		Check(quick != null && quicks.Quick == quick && quick.IsQuick && !quick.IsKept && !quick.IsNamed && !quick.IsBack && quick.Number == 0 && quick.Title == SavedLocations.QuickTitle
			&& quick.Badge == SavedLocations.QuickBadge && quick.Phrase == "your quick save" && quick.Id < 0 && quick.Id != first.Id && !quick.Completable && quick.Cat == SavedLocations.Category,
			"F5 makes the quick save: a pin of its own, never counted or ticked off");
		Check(quicks.Count == 1 && quicks.All.Count == 2 && quicks.All[0] == quick && quicks.All[1] == first, "it comes first, and isn't one of the kept ones");
		Check(Near(quick.World.x, 100f, 0.01f) && quick.Pose.Feet == 1 && quick.Saved == morning, "it's kept in the canonical loop, with how Nate stood and when");
		int quickWrites = quicks.Writes;
		SavedLocation again2 = quicks.SetQuick(new Vector3(110f, 21f, 310f), new SavedPose(25f, 26f, false), morning.AddMinutes(1));
		Check(again2 == quick && quicks.Quick == quick && quicks.All.Count == 2 && quicks.Count == 1 && quicks.Writes == quickWrites + 1,
			"F5 again saves over it: the same spot moves (tracking it follows), and nothing piles up");
		Check(Near(quick.World.x, 110f) && Near(quick.World.z, 310f) && quick.Pose.Facing == 25f && quick.Pose.Feet == 0 && quick.Saved == morning.AddMinutes(1),
			"its place, pose and time are all the new ones");
		Vector2 quickPx = projection.WorldToMap(new Vector3(110f, 21f, 310f));
		Check(Near(quick.Px, quickPx.x, 0.01f) && Near(quick.Py, quickPx.y, 0.01f), "its pin moves with it");
		SavedLocation second = quicks.Add(new Vector3(4f, 5f, 6f));
		Check(second.Number == 2 && second.Title == "Location 2", "kept locations go on numbering without it");
		quicks.SetBack(new Vector3(7f, 8f, 9f));
		Check(quicks.Step(null, 1) == quick && quicks.Step(null, -1) == quicks.Back && quicks.Step(quick, -1) == quicks.Back && quicks.Step(quicks.Back, 1) == quick && quicks.Step(quick, 1) == first,
			"stepping: the quick save first, then the kept ones, \"where you were\" last");
		SavedLocations quicksAgain = new SavedLocations(file, toMap, new MelonLogger.Instance());
		quicksAgain.Load();
		SavedLocation quickAgain = quicksAgain.Quick;
		Check(quickAgain != null && quickAgain.IsQuick && quickAgain.Title == SavedLocations.QuickTitle && quickAgain.World == quick.World && quickAgain.Pose.Known
			&& quickAgain.Pose.Facing == 25f && quickAgain.Saved == morning.AddMinutes(1) && quicksAgain.Count == 2 && quicksAgain.All.Count == 4 && quicksAgain.All[0] == quickAgain,
			"it's there after a restart, as last saved, apart from the kept ones");
		Check(quicksAgain.Remove(quickAgain) && quicksAgain.Quick == null && quicksAgain.All.Count == 3 && quicksAgain.Count == 2 && !quicksAgain.Remove(quickAgain),
			"it can be forgotten, once");
		SavedLocations quicksGone = new SavedLocations(file, toMap, new MelonLogger.Instance());
		quicksGone.Load();
		Check(quicksGone.Quick == null && quicksGone.Count == 2 && quicksGone.Back != null, "and stays forgotten");
		SavedLocation fresh = quicksGone.SetQuick(new Vector3(1f, 1f, 1f));
		Check(fresh != null && fresh.IsQuick && quicksGone.All[0] == fresh && quicksGone.All.Count == 4, "the next F5 makes a new one");

		// Naming a kept location on the map (A held on its pin, or F2): the name takes its number's place.
		File.Delete(file);
		SavedLocations names = new SavedLocations(file, toMap, new MelonLogger.Instance());
		names.Load();
		SavedLocation named = names.Add(new Vector3(1f, 2f, 3f));
		SavedLocation other = names.Add(new Vector3(4f, 5f, 6f));
		SavedLocation third = names.Add(new Vector3(7f, 8f, 9f));
		Check(!named.IsNamed && !other.IsNamed && named.Number == 1 && SavedLocations.DefaultName(3) == "Location 3", "a new location is called by its number");
		long namedId = named.Id;
		int namesWrites = names.Writes;
		int namesVersion = names.Version;
		Check(names.Rename(named, "  Top of the  \"big\"\tclimb \\o/ ") && named.Title == "Top of the \"big\" climb \\o/" && named.IsNamed && named.Phrase == named.Title,
			"renaming tidies the spaces and keeps the rest, quotes and all");
		Check(named.Number == 0 && named.Badge == "TO" && named.Id == namedId && named.IsKept && names.Writes == namesWrites + 1 && names.Version != namesVersion,
			"a named one has no number: its pin shows its first two words' first letters; the file is written straight away and the map redraws");
		Check(names.All.SequenceEqual(new[] { other, third, named }) && names.Count == 3, "named ones come after the numbered ones");
		SavedLocation fourth = names.Add(new Vector3(10f, 11f, 12f));
		Check(fourth.Number == 1 && fourth.Title == "Location 1" && fourth.Id != namedId && names.All.SequenceEqual(new[] { fourth, other, third, named }),
			"its number goes to the next new one");
		Check(!names.Rename(named, "Top of the \"big\" climb \\o/ ") && names.Writes == namesWrites + 2, "the same name again changes nothing and writes nothing");
		Check(!names.Rename(other, "Location 2") && !names.Rename(other, "  ") && !other.IsNamed && other.Title == "Location 2" && names.Writes == namesWrites + 2,
			"nor does a numbered one's own name, or none");
		Check(names.Rename(other, "Café crème") && other.Badge == "CC" && names.All.SequenceEqual(new[] { fourth, third, other, named }), "accents are fine; named ones go A to Z");
		SavedLocations namesAgain = new SavedLocations(file, toMap, new MelonLogger.Instance());
		namesAgain.Load();
		Check(namesAgain.All.Select(l => l.Title).SequenceEqual(new[] { "Location 1", "Location 3", "Café crème", "Top of the \"big\" climb \\o/" })
			&& namesAgain.All[2].IsNamed && namesAgain.All[2].Number == 0 && namesAgain.All[2].Badge == "CC" && namesAgain.All[3].Badge == "TO",
			"names survive a restart, without numbers");
		Check(names.NameIfCleared(other) == "Location 2" && names.NameIfCleared(third) == "Location 3",
			"left empty, a named one would take the lowest free number; a numbered one keeps its own");
		Check(names.Rename(other, " \t ") && other.Title == "Location 2" && other.Number == 2 && other.Badge == "2" && !other.IsNamed && other.Phrase == "Location 2"
			&& names.All.SequenceEqual(new[] { fourth, other, third, named }),
			"an empty name numbers it again, with the lowest free number, in its place");
		Check(names.Rename(third, new string('x', 40)) && third.Title == new string('x', SavedLocations.MaxNameLength) && third.Badge == "X"
			&& names.All.SequenceEqual(new[] { fourth, other, named, third }), "a name is cut to 32 characters");
		Check(SavedLocations.CleanName("a\u0001b\nc  d\u00e9 ") == "ab c d\u00e9" && SavedLocations.CleanName(null) == "" && SavedLocations.CleanName("\U0001F986 duck") == "duck",
			"control characters and emoji go; new lines and runs of spaces become one space");
		Check(SavedLocations.CleanName(new string('y', 31) + "  z") == new string('y', 31), "a name cut short doesn't end on a space");
		Check(SavedLocations.Initials("Underwater Gym") == "UG" && SavedLocations.Initials("Tower") == "T" && SavedLocations.Initials("camp 2 by the lake") == "C2"
			&& SavedLocations.Initials("Nate's tent") == "NT" && SavedLocations.Initials("élan vital") == "ÉV",
			"initials: the first letter or digit of each of the first two words, in capitals");
		Check(SavedLocations.Initials("  \"big\"   climb ") == "BC" && SavedLocations.Initials("Top - Camp") == "TC" && SavedLocations.Initials("Mid-way camp") == "MC",
			"marks before a word's first letter, and words with no letters at all, are passed over");
		Check(SavedLocations.Initials("!!!") == "!" && SavedLocations.Initials("? ?") == "?" && SavedLocations.Initials("\U0001F986") == "" && SavedLocations.Initials("") == ""
			&& SavedLocations.Initials(null) == "",
			"a name with no letters or digits shows its first character (not half an emoji); none, nothing");
		names.SetQuick(new Vector3(7f, 8f, 9f));
		names.SetBack(new Vector3(10f, 11f, 12f));
		int beforeSpots = names.Writes;
		Check(!names.Rename(names.Quick, "Mine") && names.Quick.Title == SavedLocations.QuickTitle && !names.Rename(names.Back, "Mine") && names.Back.Title == SavedLocations.BackTitle
			&& !names.Rename(null, "Mine") && names.Writes == beforeSpots && !names.Quick.IsNamed && names.Quick.Badge == SavedLocations.QuickBadge,
			"the quick save and \"where you were\" keep their names: they're saved over");

		// A file from the last version: names kept beside their numbers.
		File.WriteAllText(file, "{ \"locations\": [ { \"n\": 1, \"name\": \"Location 1\", \"x\": 1, \"y\": 2, \"z\": 3 }, { \"n\": 3, \"name\": \"Tower\", \"x\": 4, \"y\": 5, \"z\": 6 },"
			+ " { \"n\": 6, \"name\": \"Location 6\", \"x\": 7, \"y\": 8, \"z\": 9 }, { \"n\": 9, \"name\": \"Underwater Gym\", \"x\": 1, \"y\": 1, \"z\": 1 } ], \"quick\": null, \"back\": null }");
		SavedLocations lastVersion = new SavedLocations(file, toMap, new MelonLogger.Instance());
		lastVersion.Load();
		Check(lastVersion.All.Select(l => l.Badge).SequenceEqual(new[] { "1", "6", "T", "UG" }) && lastVersion.All[2].Number == 0 && lastVersion.All[3].IsNamed,
			"one from the last version loads with its named ones' numbers let go: their pins show initials");
		Check(lastVersion.Add(new Vector3(2f, 2f, 2f)).Title == "Location 2" && lastVersion.Add(new Vector3(3f, 3f, 3f)).Title == "Location 3",
			"and new ones take the numbers they had");
		SavedLocation gone = lastVersion.All.First(l => l.IsNamed);
		lastVersion.Remove(gone);
		Check(!lastVersion.Rename(gone, "Ghost") && gone.Title != "Ghost" && lastVersion.Count == 5, "a forgotten named one can't be renamed");
		gone = names.All.First(l => l.IsKept);
		names.Remove(gone);
		Check(!names.Rename(gone, "Ghost") && gone.Title != "Ghost", "a forgotten location can't be renamed");

		File.WriteAllText(file, "{ not json");
		SavedLocations broken = new SavedLocations(file, toMap, new MelonLogger.Instance());
		broken.Load();
		string aside = Path.Combine(temp3, "CabbySteps_locations.unreadable.json");
		Check(broken.Count == 0 && File.Exists(aside) && !File.Exists(file), "an unreadable file is set aside, not lost, and there are none");
		broken.Add(new Vector3(1f, 2f, 3f));
		Check(File.Exists(file) && File.ReadAllText(aside) == "{ not json", "saving again starts a new file and leaves the old one alone");
	}
	finally
	{
		Directory.Delete(temp3, recursive: true);
	}
}

Console.WriteLine("naming a location (the on-screen keyboard)");
{
	NameEntry entry = new NameEntry(SavedLocations.MaxNameLength);
	entry.Reset("");
	Check(entry.Text == "" && entry.Shift && entry.Row == 1 && entry.Column == 0 && entry.Selected == NameEntry.Key.Char && entry.CharAt(1, 0) == 'Q' && entry.Face(1, 0) == "Q",
		"an empty name starts on Q, with a capital to come");
	entry.Press();
	Check(entry.Text == "Q" && !entry.Shift && entry.Face(1, 0) == "q", "a capital first, then small letters");
	entry.Move(1, 0);
	entry.Press();
	Check(entry.Text == "Qw", "right of Q is W");
	entry.Move(-2, 0);
	Check(entry.Row == 1 && entry.Column == 9 && entry.CharAt(1, 9) == 'p', "left off the edge comes back on the right");
	entry.Move(0, -2);
	Check(entry.Row == NameEntry.BottomRow && entry.Selected == NameEntry.Key.Done, "up off the top comes back on the bottom row: under P is Done");
	entry.Move(-1, 0);
	Check(entry.Selected == NameEntry.Key.Delete && entry.Column == 7, "left of Done is Delete");
	entry.Move(0, -1);
	Check(entry.Row == 3 && entry.Column == 7 && entry.CharAt(3, 7) == '-', "up from Delete lands above where it was entered");
	entry.Move(0, 1);
	entry.Move(-1, 0);
	Check(entry.Selected == NameEntry.Key.Space && entry.IsSelected(NameEntry.BottomRow, 2) && entry.IsSelected(NameEntry.BottomRow, 5) && !entry.IsSelected(NameEntry.BottomRow, 6)
		&& !entry.IsSelected(3, 5), "a wide key is selected whichever of its columns");
	entry.Press();
	Check(entry.Text == "Qw ", "Space adds a space");
	Check(!entry.Type(' ') && entry.Text == "Qw ", "but not two in a row");
	entry.Move(-1, 0);
	entry.Press();
	Check(entry.Selected == NameEntry.Key.Shift && entry.Shift && entry.Face(2, 9) == "\"", "Shift shows the other symbols");
	entry.Select(3, 9);
	entry.Press();
	Check(entry.Text == "Qw ?" && !entry.Shift, "Shift is for one character");
	entry.Select(NameEntry.BottomRow, 6);
	for (int i = 0; i < 5; i++)
	{
		entry.Press();
	}
	Check(entry.Selected == NameEntry.Key.Delete && entry.Text == "" && entry.Shift, "Delete takes characters off; emptied, a capital comes next again");
	Check(!entry.Backspace() && !entry.Type(' ') && !entry.Type('\u0007') && !entry.Type('\uD83E') && entry.Text == "",
		"nothing to delete, and no space first, control characters or halves of emoji");
	entry.Type('1');
	Check(entry.Text == "1" && !entry.Shift, "digits stay digits, and use up the Shift");
	for (int i = 0; i < 40; i++)
	{
		entry.Type('a');
	}
	Check(entry.Text.Length == SavedLocations.MaxNameLength, "no more than 32 characters");
	entry.Select(NameEntry.BottomRow, 9);
	Check(entry.Press() && entry.Selected == NameEntry.Key.Done && entry.Text.Length == SavedLocations.MaxNameLength, "Done says so and leaves the name be");
	entry.Reset("Pillar");
	Check(entry.Text == "Pillar" && !entry.Shift && entry.Row == 1 && entry.Column == 0, "a name to edit starts as it is, on Q, small letters next");
	Check(entry.Type('S') && !entry.Type('\t') && entry.Text == "PillarS", "a keyboard types straight in (Tab doesn't)");
	Check(NameEntry.KeyAt(NameEntry.BottomRow, 0) == NameEntry.Key.Shift && NameEntry.KeyAt(NameEntry.BottomRow, 4) == NameEntry.Key.Space
		&& NameEntry.KeyAt(NameEntry.BottomRow, 9) == NameEntry.Key.Done && NameEntry.KeyAt(0, 9) == NameEntry.Key.Char,
		"the bottom row is Shift, Space, Delete and Done");
	int spans = 0;
	for (int k = 0; k < NameEntry.BottomCount; k++)
	{
		Check(k == 0 || NameEntry.BottomStart(k) == NameEntry.BottomStart(k - 1) + NameEntry.BottomSpan(k - 1), "bottom key " + NameEntry.BottomKey(k) + " starts where the one before ends");
		spans += NameEntry.BottomSpan(k);
	}
	Check(spans == NameEntry.Columns, "the bottom row is as wide as the rest");
}

Console.WriteLine("map view");
Vector2 screenCenter = new Vector2(960f, 540f);
Vector2 viewCenter = new Vector2(1000f, 3000f);
Vector2 somePx = new Vector2(1012.5f, 2990.25f);
Vector2 roundTrip = MapView.ToMap(MapView.ToScreen(somePx, screenCenter, 2.5f, viewCenter), screenCenter, 2.5f, viewCenter);
Check(Near(roundTrip.x, somePx.x, 0.01f) && Near(roundTrip.y, somePx.y, 0.01f), "ToMap undoes ToScreen");
Vector2 pointerAt = new Vector2(1500f, 200f);
Vector2 underBefore = MapView.ToMap(pointerAt, screenCenter, 2.5f, viewCenter);
Vector2 underAfter = MapView.ToMap(pointerAt, screenCenter, 4f, MapView.ZoomAbout(pointerAt, screenCenter, 2.5f, 4f, viewCenter));
Check(Near(underBefore.x, underAfter.x, 0.01f) && Near(underBefore.y, underAfter.y, 0.01f), "zooming keeps the map still under the pointer");

Console.WriteLine("completed POIs, kept with the game's save");
string temp = Path.Combine(Path.GetTempPath(), "cabbysteps-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
	MelonLogger.Instance log = new MelonLogger.Instance();
	string slot0 = Path.Combine(temp, "CabbySteps_completed0.json");
	string slot1 = Path.Combine(temp, "CabbySteps_completed1.json");
	Poi hat = new Poi { Id = 488900, Title = "Cowboy Hat" };
	Poi cans = new Poi { Id = 488901, Title = "Stack of Cans" };
	Poi fruit = new Poi { Id = 488902, Title = "Pear" };

	PoiProgress progress = new PoiProgress(log);
	progress.Attach(slot0, "save0.sav", 100f, newGame: false);
	Check(progress.Attached && progress.Count == 0 && !File.Exists(slot0), "a save with no marks file has none");
	progress.Toggle(hat);
	Check(progress.IsDone(hat) && !File.Exists(slot0), "a mark stays in memory until the game saves");
	progress.Commit(110f);
	Check(File.Exists(slot0) && progress.Writes == 1, "the game saving writes the marks");
	progress.Commit(120f);
	Check(progress.Writes == 1, "a game save with nothing new writes nothing");
	progress.Toggle(cans);
	progress.Commit(130f);
	progress.Toggle(fruit);

	// Quit without the game saving again: the last mark never reached a save.
	PoiProgress next = new PoiProgress(log);
	next.Attach(slot0, "save0.sav", 130f, newGame: false);
	Check(next.IsDone(hat) && next.IsDone(cans) && !next.IsDone(fruit), "continuing gets the marks as of the last game save");
	next.Attach(slot0, "save0.sav", 115f, newGame: false);
	Check(next.IsDone(hat) && !next.IsDone(cans), "an older save (a backup) gets the marks it was made with");
	next.Attach(slot0, "save0.sav", 105f, newGame: false);
	Check(next.Count == 0, "a save from before the first mark has none");

	// Carrying on from the older save replaces what came after it.
	next.Attach(slot0, "save0.sav", 115f, newGame: false);
	next.Toggle(fruit);
	next.Commit(140f);
	next.Attach(slot0, "save0.sav", 140f, newGame: false);
	Check(next.IsDone(hat) && next.IsDone(fruit) && !next.IsDone(cans), "playing on from an older save drops the marks made after it");

	// Unmarking is kept too.
	next.Toggle(hat);
	next.Commit(150f);
	next.Attach(slot0, "save0.sav", 150f, newGame: false);
	Check(!next.IsDone(hat) && next.IsDone(fruit), "unmarking is kept with the save");

	// Deleting the save and starting over.
	next.Attach(slot0, "save0.sav", 0f, newGame: true);
	Check(next.Count == 0 && File.Exists(slot0), "a new game starts with no marks (the old file waits for the game's first save)");
	next.Commit(20f);
	Check(!File.Exists(slot0) && File.Exists(PoiProgress.RetiredPath(slot0)), "the new game's first save retires the old marks");
	next.Attach(slot0, "save0.sav", 20f, newGame: false);
	Check(next.Count == 0, "continuing the new game keeps it fresh");

	// Another slot.
	next.Attach(slot1, "save1.sav", 50f, newGame: false);
	next.Toggle(cans);
	next.Commit(55f);
	next.Attach(slot0, "save0.sav", 20f, newGame: false);
	Check(next.Count == 0, "marks in one slot don't show up in another");
	next.Attach(slot1, "save1.sav", 55f, newGame: false);
	Check(next.IsDone(cans) && next.Count == 1, "each slot keeps its own marks");

	// The game's play time is a float; it must survive the file exactly.
	float playTime = 3781.38330078125f;
	next.Toggle(hat);
	next.Commit(playTime);
	PoiProgress exact = new PoiProgress(log);
	exact.Attach(slot1, "save1.sav", playTime, newGame: false);
	Check(exact.IsDone(hat), "the save's play time round-trips through the file");

	File.WriteAllText(slot0, "{ not json");
	exact.Attach(slot0, "save0.sav", 30f, newGame: false);
	Check(exact.Count == 0, "an unreadable file means no marks, not a crash");
}
finally
{
	Directory.Delete(temp, recursive: true);
}

Console.WriteLine("files kept under the mod's old name (BabyStepsMinimap)");
{
	string renamedDir = Path.Combine(Path.GetTempPath(), "cabbysteps-tests-" + Guid.NewGuid().ToString("N"));
	Directory.CreateDirectory(renamedDir);
	try
	{
		Check(RenamedFiles.OldName == "BabyStepsMinimap", "the old name is the one the old files and the old DLL had");
		string oldConfigFile = Path.Combine(renamedDir, "BabyStepsMinimap_config.json");
		string newConfigFile = Path.Combine(renamedDir, "CabbySteps_config.json");
		Check(RenamedFiles.CarryOver(oldConfigFile, newConfigFile, out _) == RenamedFiles.Outcome.None && !File.Exists(newConfigFile), "with no old file there's nothing to do");
		File.WriteAllText(oldConfigFile, "{ \"helpLevel\": 1 }");
		Check(RenamedFiles.CarryOver(oldConfigFile, newConfigFile, out _) == RenamedFiles.Outcome.Moved && File.ReadAllText(newConfigFile) == "{ \"helpLevel\": 1 }" && !File.Exists(oldConfigFile),
			"an old file takes the new name, just as it was");
		Check(RenamedFiles.CarryOver(oldConfigFile, newConfigFile, out _) == RenamedFiles.Outcome.None && File.Exists(newConfigFile), "and only once");
		File.WriteAllText(oldConfigFile, "{ \"helpLevel\": 3 }");
		Check(RenamedFiles.CarryOver(oldConfigFile, newConfigFile, out _) == RenamedFiles.Outcome.Both && File.ReadAllText(newConfigFile) == "{ \"helpLevel\": 1 }" && File.Exists(oldConfigFile),
			"a file with the new name wins, and the old one is left alone");
		string nowhere = Path.Combine(renamedDir, "missing", "CabbySteps_locations.json");
		Check(RenamedFiles.CarryOver(oldConfigFile, nowhere, out string renameError) == RenamedFiles.Outcome.Failed && !string.IsNullOrEmpty(renameError) && File.Exists(oldConfigFile),
			"one that can't be renamed stays where it was, and says why");

		// A save slot's marks: carried over as its save loads, and not brought back once a new game
		// in that slot has retired them (a copy left under the old name would be).
		string oldMarksFile = Path.Combine(renamedDir, "BabyStepsMinimap_completed0.json");
		string newMarksFile = Path.Combine(renamedDir, "CabbySteps_completed0.json");
		Poi renamedPear = new Poi { Id = 488902, Title = "Pear" };
		PoiProgress marksBefore = new PoiProgress(new MelonLogger.Instance());
		marksBefore.Attach(oldMarksFile, "save0.sav", 10f, newGame: false);
		marksBefore.Toggle(renamedPear);
		marksBefore.Commit(20f);
		RenamedFiles.CarryOver(oldMarksFile, newMarksFile, out _);
		PoiProgress marksAfter = new PoiProgress(new MelonLogger.Instance());
		marksAfter.Attach(newMarksFile, "save0.sav", 20f, newGame: false);
		Check(marksAfter.IsDone(renamedPear) && marksAfter.Count == 1, "a slot's completed POIs come along with it");
		marksAfter.Attach(newMarksFile, "save0.sav", 0f, newGame: true);
		marksAfter.Commit(5f);
		Check(RenamedFiles.CarryOver(oldMarksFile, newMarksFile, out _) == RenamedFiles.Outcome.None && !File.Exists(newMarksFile) && File.Exists(PoiProgress.RetiredPath(newMarksFile)),
			"a new game in that slot retires them, and there's no old file left to bring them back");
		marksAfter.Attach(newMarksFile, "save0.sav", 5f, newGame: false);
		Check(marksAfter.Count == 0, "so continuing the new game keeps it fresh");
	}
	finally
	{
		Directory.Delete(renamedDir, recursive: true);
	}
}

Console.WriteLine("summoned items: where they're put down");
{
	Vector3 nate = new Vector3(0f, 1f, 0f);
	Vector3 ahead = new Vector3(0f, 0f, 1f);
	List<Vector3> none = new List<Vector3>();
	const float radius = 0.3f;
	const float height = 0.4f;
	float reach = SummonSpot.Gap + radius;
	Vector3 Toward(float degrees) => SummonSpot.Turn(ahead, degrees);
	bool At(Vector3 spot, float degrees, float ground = 0f) => Near(spot.x, Toward(degrees).x * reach, 0.01f) && Near(spot.z, Toward(degrees).z * reach, 0.01f) && Near(spot.y, ground, 0.01f);

	Vector3 right = SummonSpot.Turn(ahead, 90f);
	Vector3 back = SummonSpot.Turn(new Vector3(1f, 0f, 0f), 90f);
	Check(Near(right.x, 1f) && Near(right.z, 0f) && Near(back.x, 0f) && Near(back.z, -1f), "Turn turns clockwise seen from above, as Unity's yaw does");

	TestWorld open = new TestWorld();
	Check(SummonSpot.Find(open, nate, ahead, radius, height, none, out Vector3 spot) && At(spot, 0f), "on open ground the item goes just in front of Nate, on the ground");
	Check(SummonSpot.Find(open, nate, right, radius, height, none, out spot) && Near(spot.x, reach, 0.01f) && Near(spot.z, 0f, 0.01f), "in front of the way he faces");

	Check(SummonSpot.Find(open, nate, ahead, 0.1f, height, none, out spot) && Near(spot.z, SummonSpot.Gap + 0.1f, 0.01f)
		&& SummonSpot.Find(open, nate, ahead, 0.5f, height, none, out Vector3 big) && Near(big.z, SummonSpot.Gap + 0.5f, 0.01f), "a bigger item is put down a little further out, clear of him");

	TestWorld hill = new TestWorld { Height = (x, z) => 0.25f };
	Check(SummonSpot.Find(hill, nate, ahead, radius, height, none, out spot) && Near(spot.y, 0.25f, 0.01f), "at the height of the ground under it");

	TestWorld walled = new TestWorld();
	walled.Solids.Add((new Vector3(-0.5f, -1f, 0.6f), new Vector3(0.5f, 3f, 0.8f)));
	Check(SummonSpot.Find(walled, nate, ahead, radius, height, none, out spot) && At(spot, 70f) && !walled.Blocked(nate, new Vector3(spot.x, 1f, spot.z)),
		"a wall in front: round the side, to his right first, still in sight of him");

	TestWorld cliff = new TestWorld { Height = (x, z) => (z > 0.5f) ? null : 0f };
	Check(SummonSpot.Find(cliff, nate, ahead, radius, height, none, out spot) && spot.z + radius < 0.5f && spot.x > 0f, "a drop in front: nowhere near the edge");

	TestWorld deep = new TestWorld { Height = (x, z) => -2f };
	Check(!SummonSpot.Find(deep, nate, ahead, radius, height, none, out spot), "ground further below his hips than the drop limit doesn't count");

	TestWorld rock = new TestWorld { Height = (x, z) => (Math.Abs(x) < 0.2f && z > 1.3f && z < 1.6f) ? 0.5f : 0f };
	Check(SummonSpot.Find(rock, nate, ahead, radius, height, none, out spot) && !At(spot, 0f) && Near(spot.y, 0f, 0.01f), "a rock under part of the item: somewhere flat instead");

	TestWorld littered = new TestWorld();
	littered.Items.Add((new Vector3(-0.15f, 0f, 1.05f), new Vector3(0.15f, 0.3f, 1.35f)));
	Check(SummonSpot.Find(littered, nate, ahead, radius, height, none, out spot) && !At(spot, 0f) && !littered.Occupied(new Vector3(spot.x, spot.y + SummonSpot.Clearance + height * 0.5f, spot.z), new Vector3(radius, height * 0.5f, radius)),
		"something already lying in front: the item goes where there's room");

	List<Vector3> taken = new List<Vector3> { new Vector3(0f, 0f, reach) };
	Check(SummonSpot.Find(open, nate, ahead, radius, height, taken, out spot) && At(spot, 70f), "a spot just taken by another item is kept clear");
	taken.Add(spot);
	Check(SummonSpot.Find(open, nate, ahead, radius, height, taken, out Vector3 third) && At(third, -70f), "and the next item goes on his other side");

	TestWorld slope = new TestWorld { Level = (x, z) => 0.5f };
	Check(SummonSpot.Find(slope, nate, ahead, radius, height, none, out spot) && At(spot, 0f), "only steep ground about: the nearest spot in sight of him will do");

	TestWorld ledge = new TestWorld { Height = (x, z) => null };
	Check(!SummonSpot.Find(ledge, nate, ahead, radius, height, none, out spot), "no ground at all: no spot (the item goes in front of him instead)");
}

Console.WriteLine("saved locations: when a trip may put Nate down");
{
	// Frames at 60 a second, physics steps at 50, as in the game. The gate's answer to "may the
	// teleport go on" is read after each frame; returns when it let go (seconds), or -1.
	float Run(ArrivalGate gate, float limit, Func<float, bool> ready, Func<float, bool> loading, Func<float, bool> ground, Func<float, float> fastest, bool physics = true)
	{
		gate.Begin(0f, true);
		float nextStep = 0f;
		for (int frame = 1; frame <= limit * 60f; frame++)
		{
			float now = frame / 60f;
			while (physics && nextStep <= now)
			{
				gate.Step(nextStep, fastest(nextStep));
				nextStep += 0.02f;
			}
			bool footing = gate.WantsFooting && ground(now);
			gate.Update(now, ready(now), loading(now), footing);
			if (!gate.Holding)
			{
				return now;
			}
		}
		return -1f;
	}
	Func<float, bool> always = _ => true;
	Func<float, bool> never = _ => false;
	Func<float, float> standing = _ => 0.1f;

	ArrivalGate gate = new ArrivalGate();
	float went = Run(gate, 30f, always, never, always, standing);
	Check(went > 0.15f && went < 0.3f && !gate.FootingMissing && !gate.StillLoading && !gate.TimedOut,
		$"a short trip, everything in and Nate standing: held only a moment ({went:0.00} s) for him to be held still");

	went = Run(gate, 30f, always, never, always, t => (t < 0.06f) ? 9f : 0.2f);
	Check(went > 0.2f && went < 0.4f && Near(gate.FastestSeen, 9f) && gate.Summary().Contains("9.0 m/s"),
		$"tumbling when sent off: held until he's been still for {ArrivalGate.StillSteps} steps ({went:0.00} s), and the log says how fast he was going");

	went = Run(gate, 30f, always, never, always, _ => 0.8f);
	Check(went >= ArrivalGate.StillPatience && went < ArrivalGate.StillPatience + 0.05f && gate.Summary().Contains("never quite still"),
		$"a limb his muscles keep moving a little: held still for {ArrivalGate.StillPatience} s, then he goes ({went:0.00} s)");

	went = Run(gate, 30f, t => t >= 2f, t => t < 2f, t => t >= 2.6f, standing);
	Check(went >= 2.6f && went < 2.75f && !gate.FootingMissing && gate.Summary().Contains("footing there after 2.6"),
		$"a long trip: held while the world loads, then until there's ground under his saved feet ({went:0.00} s)");

	went = Run(gate, 30f, t => t >= 1f, never, never, standing);
	Check(went >= 1f + ArrivalGate.FootingPatience && went < 1.1f + ArrivalGate.FootingPatience && gate.FootingMissing && gate.Summary().Contains("not found"),
		$"no ground ever turns up under his feet (what he stood on has moved): he goes after {ArrivalGate.FootingPatience} s without it ({went:0.00} s)");

	went = Run(gate, 30f, t => t < 0.02f || t >= 0.5f, t => t < 0.5f, always, standing);
	Check(went >= 0.5f + ArrivalGate.ReadyTime, $"one stale \"loaded\" from the loader at the start doesn't let him go ({went:0.00} s)");

	went = Run(gate, 30f, always, t => ((int)(t * 60f)) % 3 != 0, always, standing);
	Check(went >= ArrivalGate.LoadingPatience && went < ArrivalGate.LoadingPatience + 0.1f && gate.StillLoading,
		$"far-off scenery that keeps loading holds him up to {ArrivalGate.LoadingPatience} s at most ({went:0.00} s)");

	went = Run(gate, 30f, always, t => t < 0.5f, always, standing);
	Check(went > 0.5f && went < 0.65f && !gate.StillLoading && gate.Summary().Contains("quiet after"), $"props still loading round the spot: he waits for them ({went:0.00} s)");

	went = Run(gate, 30f, always, never, always, standing, physics: false);
	Check(went >= ArrivalGate.StillTimeout && went < ArrivalGate.StillTimeout + 0.05f && gate.StillnessUnseen, $"physics not stepping (the game frozen): he isn't waited on for ever ({went:0.00} s)");

	went = Run(gate, 30f, never, always, never, standing);
	Check(Near(went, ArrivalGate.MaxHold, 0.02f) && gate.TimedOut && gate.Summary().Contains("as long as a trip may be held"), $"the world never loads: let go after {ArrivalGate.MaxHold} s ({went:0.00} s)");

	gate.Begin(0f, needFooting: false);
	Check(gate.Holding && !gate.WantsFooting, "a trip without a saved footing doesn't look for ground");
	gate.Abandon(1f);
	Check(!gate.Holding && !gate.Step(1.1f, 0f) && Near(gate.Held, 1f), "abandoned: no longer held, nor holding him still");
}

Console.WriteLine("saved locations: what Nate set off with is given back to him there");
{
	// Things by id (0: nothing): a key (7) and a lantern (9); hands 0 right, 1 left.
	const long key = 7, lantern = 9;
	Check(CarryPlan.Refill(0, key, 0, 0, 0, true) && !CarryPlan.Refill(1, key, 0, 0, 0, true),
		"a key dropped from his right hand on the way goes back in his right hand, and nothing in his left");
	Check(!CarryPlan.Refill(0, key, 0, key, 0, true), "a key the teleport gave back isn't given again");
	Check(!CarryPlan.Refill(0, key, 0, 0, 0, false), "a key gone from the world (melted, broken) isn't given back");
	Check(!CarryPlan.Refill(0, key, 0, lantern, 0, true), "a hand holding something else now is left be");
	Check(!CarryPlan.Refill(0, key, 0, 0, key, true), "a key now in his other hand stays there");
	Check(CarryPlan.Refill(0, key, key, 0, 0, true) && CarryPlan.Refill(1, key, key, key, 0, true),
		"a thing held in both hands goes back in both, as the teleport gives it back");
	Check(CarryPlan.Refill(0, key, lantern, 0, lantern, true) && !CarryPlan.Refill(1, key, lantern, 0, lantern, true),
		"one hand's thing dropped, the other's kept: only the dropped one is given back");
	Check(CarryPlan.Rewear(lantern, 0, true) && !CarryPlan.Rewear(lantern, lantern, true) && !CarryPlan.Rewear(lantern, key, true)
		&& !CarryPlan.Rewear(lantern, 0, false) && !CarryPlan.Rewear(0, 0, true),
		"his hat goes back on if it came off and is still about; not over another hat, nor if he set off without one");
}

Console.WriteLine("Grippy feet: his weight counted over his feet");
{
	// One foot's outline as the game builds it (PlayerMovement.polySupport): counter-clockwise in
	// x/z, 10 cm by 25 cm, its first point repeated at the end.
	float[] fx = { 0f, 0.1f, 0.1f, 0f, 0f };
	float[] fz = { 0f, 0f, 0.25f, 0.25f, 0f };
	const float margin = 0.02f;
	float Gap(float ax, float az, float bx, float bz) => (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
	Check(SupportPolygon.Inside(fx, fz, 5, 0.05f, 0.1f) && !SupportPolygon.Inside(fx, fz, 5, 0.3f, 0.1f) && !SupportPolygon.Inside(fx, fz, 5, 0.05f, -0.2f)
		&& !SupportPolygon.Inside(fx, fz, 5, -0.01f, 0.1f) && !SupportPolygon.Inside(fx, fz, 5, 0.05f, 0.26f),
		"over his foot and beyond it on every side, as the game's InsidePolySup has it");
	Check(SupportPolygon.Inside(fx, fz, 5, 0.1f, 0.1f) && SupportPolygon.Inside(fx, fz, 5, 0f, 0f), "on the edge counts as over it, as in the game");
	Check(SupportPolygon.Closest(fx, fz, 5, 0.4f, 0.1f, out float cx, out float cz) && Near(cx, 0.1f) && Near(cz, 0.1f)
		&& SupportPolygon.Closest(fx, fz, 5, 0.05f, 0.2f, out float ix, out float iz) && Near(ix, 0.05f) && Near(iz, 0.25f)
		&& SupportPolygon.Closest(fx, fz, 5, -0.3f, -0.3f, out float kx, out float kz) && Near(kx, 0f) && Near(kz, 0f),
		"the nearest point of the outline, as ClosestPtOnPolySupport has it: on an edge, a corner, and an edge even from inside");
	Check(!SupportPolygon.Steady(fx, fz, 5, 0.05f, 0.1f, margin, out float sx, out float sz) && sx == 0.05f && sz == 0.1f,
		"weight already over his foot stays where it is");
	bool moved = SupportPolygon.Steady(fx, fz, 5, 0.4f, 0.1f, margin, out float mx, out float mz);
	Check(moved && SupportPolygon.Inside(fx, fz, 5, mx, mz) && Near(Gap(mx, mz, 0.1f, 0.1f), margin, 1e-4f) && mx < 0.1f - 0.01f,
		$"weight 30 cm beyond his foot is counted 2 cm inside the edge it went over ({mx:0.000}, {mz:0.000})");
	moved = SupportPolygon.Steady(fx, fz, 5, -0.3f, -0.3f, margin, out mx, out mz);
	Check(moved && SupportPolygon.Inside(fx, fz, 5, mx, mz) && Gap(mx, mz, 0f, 0f) <= margin + 1e-4f,
		"beyond a corner: just inside that corner");
	// Both feet down: the outline round both, 30 cm apart, him leaning half a metre forward.
	float[] bx = { 0f, 0.4f, 0.4f, 0f, 0f };
	float[] bz = { 0f, 0f, 0.25f, 0.25f, 0f };
	moved = SupportPolygon.Steady(bx, bz, 5, 0.2f, 0.75f, margin, out mx, out mz);
	Check(moved && SupportPolygon.Inside(bx, bz, 5, mx, mz) && Near(mx, 0.2f, 1e-4f) && Near(mz, 0.25f - margin, 1e-4f),
		$"both feet down, leaning forward: counted just behind the toes, between his feet ({mx:0.000}, {mz:0.000})");
	// What the game would never call balanced is left to the game.
	Check(!SupportPolygon.Steady(fx, fz, 1, 0.4f, 0.1f, margin, out _, out _) && !SupportPolygon.Steady(fx, fz, 0, 0.4f, 0.1f, margin, out _, out _),
		"fewer than two points: the game counts any weight as over them, and so does this");
	float[] cwx = { 0f, 0f, 0.1f, 0.1f, 0f };
	float[] cwz = { 0f, 0.25f, 0.25f, 0f, 0f };
	Check(!SupportPolygon.Inside(cwx, cwz, 5, 0.05f, 0.1f) && !SupportPolygon.Steady(cwx, cwz, 5, 0.4f, 0.1f, margin, out sx, out sz) && sx == 0.4f && sz == 0.1f,
		"an outline wound the other way, which the game would never call him balanced on, is left alone");
	float[] ox = { 0f, 0.1f, 0.1f, 0f };
	float[] oz = { 0f, 0f, 0.25f, 0.25f };
	Check(SupportPolygon.Inside(ox, oz, 4, -0.5f, 0.1f) && !SupportPolygon.Steady(ox, oz, 4, -0.5f, 0.1f, margin, out _, out _)
		&& SupportPolygon.Steady(ox, oz, 4, 0.4f, 0.1f, margin, out mx, out mz) && SupportPolygon.Inside(ox, oz, 4, mx, mz),
		"an outline that isn't closed is read as the game reads it: no edge from the last point back to the first");
	float[] lx = { 0f, 0f };
	float[] lz = { 0f, 0.25f };
	moved = SupportPolygon.Steady(lx, lz, 2, 0.3f, 0.1f, margin, out mx, out mz);
	Check(moved && SupportPolygon.Inside(lx, lz, 2, mx, mz) && Near(mx, 0f, 1e-4f),
		"two points (an outline seen edge-on): weight off to the wrong side is put on the line");
}

Console.WriteLine("Grippy feet: its levels, and the config file");
{
	Check(GripLevels.Next(GripLevel.Off) == GripLevel.Grippy && GripLevels.Next(GripLevel.Grippy) == GripLevel.Extreme
		&& GripLevels.Next(GripLevel.Extreme) == GripLevel.Off,
		"F7 steps off, Grippy feet, Extreme grip, and round to off");
	Check(GripLevels.Read(true) == GripLevel.Grippy && GripLevels.Read(false) == GripLevel.Off,
		"an older config's true/false (before Extreme grip) is Grippy feet / off");
	Check(GripLevels.Read("extreme") == GripLevel.Extreme && GripLevels.Read(" Grippy ") == GripLevel.Grippy && GripLevels.Read("off") == GripLevel.Off
		&& GripLevels.Read(2.0) == GripLevel.Extreme && GripLevels.Read(1.0) == GripLevel.Grippy && GripLevels.Read(0.0) == GripLevel.Off,
		"the names (any case, spaces round them) and the numbers 0 to 2");
	Check(GripLevels.Read(null) == GripLevel.Off && GripLevels.Read("wall-walk") == GripLevel.Off && GripLevels.Read(new List<object>()) == GripLevel.Off,
		"anything else is off");
	Check(new[] { GripLevel.Off, GripLevel.Grippy, GripLevel.Extreme }.All(level => GripLevels.Read(GripLevels.Name(level)) == level)
		&& GripLevels.Name(GripLevel.Extreme) == "extreme",
		"each level is written as a name that reads back as itself");
	Dictionary<string, object> written = MiniJson.ParseObject("{ \"grippyFeet\": \"" + GripLevels.Name(GripLevel.Extreme) + "\" }");
	Check(GripLevels.Read(written["grippyFeet"]) == GripLevel.Extreme, "and survives the mod's JSON reader");
}

Console.WriteLine("Extreme grip: when a foot takes hold, keeps hold and lets go");
{
	// FootGrip.Next(stepping, down, ground, holding, planted, lifting): the game's GroundedCheck
	// asking (stepping), Nate down or in a cutscene, the game's own answer, the foot's hold, and its
	// FootData.planted and isLifting.
	FootGrip.Move Rule(bool stepping, bool down, bool ground, bool holding, bool planted, bool lifting, out FootGrip.Why why)
		=> FootGrip.Next(stepping, down, ground, holding, planted, lifting, out why);
	FootGrip.Why why;
	Check(Rule(true, false, true, false, true, false, out why) == FootGrip.Move.None && why == FootGrip.Why.None
		&& Rule(true, false, true, false, false, false, out _) == FootGrip.Move.None,
		"ground the game finds is the game's: an ordinary step, nothing looked for");
	Check(Rule(true, false, true, true, true, false, out why) == FootGrip.Move.LetGo && why == FootGrip.Why.Ground,
		"a foot that holds and finds real ground under it lets go: an ordinary step from then on");
	Check(Rule(true, false, false, false, false, false, out why) == FootGrip.Move.Consider && why == FootGrip.Why.None
		&& Rule(true, false, false, false, true, false, out _) == FootGrip.Move.Consider,
		"no ground, no hold, not being lifted: it may take hold of what it touches (in the air, or planted on ground gone from under it)");
	Check(Rule(true, false, false, false, true, true, out why) == FootGrip.Move.None && why == FootGrip.Why.None,
		"a planted foot the game is lifting doesn't take hold: it's on its way up");
	Check(FootGrip.PutDown(0f) && FootGrip.PutDown(FootGrip.PutDownTrigger) && !FootGrip.PutDown(0.06f) && !FootGrip.PutDown(1f),
		"only a foot being put down takes hold: trigger at most 0.05, as the game's TriggerMove drops it; above, it's held up");
	Check(Rule(true, false, false, true, true, false, out why) == FootGrip.Move.Hold && why == FootGrip.Why.None,
		"a foot that holds stays planted where it is, whatever is under it");
	Check(Rule(true, false, false, true, true, true, out why) == FootGrip.Move.LetGo && why == FootGrip.Why.Lifted,
		"and lets go when the game lifts it (its trigger, once his weight is off it)");
	Check(Rule(true, false, false, true, false, false, out why) == FootGrip.Move.LetGo && why == FootGrip.Why.Unplanted,
		"or when the game has already taken it up");
	Check(Rule(true, true, false, true, true, false, out why) == FootGrip.Move.LetGo && why == FootGrip.Why.Down
		&& Rule(true, true, false, false, false, false, out _) == FootGrip.Move.None,
		"Nate down or in a cutscene: no holds, none taken");
	Check(Rule(false, false, false, true, true, false, out why) == FootGrip.Move.LetGo && why == FootGrip.Why.StoodUp
		&& Rule(false, false, false, false, false, false, out _) == FootGrip.Move.None
		&& Rule(false, false, true, true, true, false, out why) == FootGrip.Move.LetGo && why == FootGrip.Why.StoodUp,
		"the game standing him up (OnStandUp, after a fall or a trip) lets every hold go and takes none");
	// One foot up a wall, step by step, as the game would ask about it.
	bool holding = false, planted = false;
	List<string> steps = new List<string>();
	void Step(string what, bool ground, bool lifting, float trigger, bool touching)
	{
		FootGrip.Move move = FootGrip.Next(true, false, ground, holding, planted, lifting, out FootGrip.Why w);
		bool now = ground;
		if (move == FootGrip.Move.Hold || (move == FootGrip.Move.Consider && FootGrip.PutDown(trigger) && touching))
		{
			holding = true;
			now = true;
		}
		else if (move == FootGrip.Move.LetGo)
		{
			holding = false;
		}
		planted = now;
		steps.Add(what + ":" + (holding ? "holds" : (planted ? "planted" : "up")));
	}
	Step("on the ground", true, false, 0f, true);
	Step("being lifted", true, true, 1f, true);
	Step("up, pushed into the wall", false, false, 1f, true);
	Step("put down against it", false, false, 0f, true);
	Step("held", false, false, 0f, true);
	Step("the other foot lifted", false, false, 0f, true);
	Step("its own trigger pulled", false, true, 0.8f, true);
	Step("up again, along the wall", false, false, 0.8f, true);
	Step("put down in the air", false, false, 0f, false);
	Step("landing", true, false, 0f, false);
	string walk = string.Join(", ", steps);
	Check(walk == "on the ground:planted, being lifted:planted, up, pushed into the wall:up, put down against it:holds, held:holds, "
		+ "the other foot lifted:holds, its own trigger pulled:up, up again, along the wall:up, put down in the air:up, landing:planted",
		"a foot up a wall: held up it slides along it, put down it holds, lifted it lets go, put down in the air it falls to the ground ("
		+ walk + ")");
}

Console.WriteLine("Hold tight: what the game may still take from Nate");
{
	LetGo[] causes = Enum.GetValues<LetGo>();
	Check(new[] { LetGo.Jolted, LetGo.Hit, LetGo.Wriggled }.All(c => HoldRules.KeepInHand(true, c, false, true)),
		"jolted or hit while he's down, or the Baby Trophy wriggling free as he goes down: what he holds stays in his hand");
	Check(new[] { LetGo.Jolted, LetGo.Camp, LetGo.Blown }.All(c => HoldRules.KeepOnHead(true, c, false, true)),
		"jolted while he's down, a camp's night, the wind: his hat stays on");
	Check(!HoldRules.KeepInHand(true, LetGo.Other, false, true) && !HoldRules.KeepOnHead(true, LetGo.Other, false, true),
		"whatever else the game takes (handed over, used up, a trip, the ending) it still takes");
	Check(!HoldRules.KeepInHand(true, LetGo.Camp, false, true) && !HoldRules.KeepInHand(true, LetGo.Blown, false, true)
		&& !HoldRules.KeepOnHead(true, LetGo.Hit, false, true) && !HoldRules.KeepOnHead(true, LetGo.Wriggled, false, true),
		"each cause keeps only what it takes (a camp and the wind take hats, a hit and the trophy what's in his hands)");
	Check(causes.All(c => !HoldRules.KeepInHand(true, c, true, true) && !HoldRules.KeepOnHead(true, c, true, true)),
		"a swap always goes ahead, even while he's down (a hat put on from his hand takes off the one he wore)");
	Check(causes.All(c => !HoldRules.KeepInHand(true, c, false, false) && !HoldRules.KeepOnHead(true, c, false, false)),
		"a thing gone from the world (melted, broken, switched off) is let go of, so his hand never holds what isn't there");
	Check(causes.All(c => !HoldRules.KeepInHand(false, c, false, true) && !HoldRules.KeepOnHead(false, c, false, true)) && !HoldRules.KeepWhole(false, true),
		"with the cheat off, it's all the game's");
	Check(HoldRules.KeepWhole(true, true) && !HoldRules.KeepWhole(true, false), "a vase in his hand doesn't break; one he has put down does");
	Check(HoldRules.Read(true) && !HoldRules.Read(false) && HoldRules.Read(" On ") && HoldRules.Read("true") && HoldRules.Read(1.0) && !HoldRules.Read(0.0)
		&& !HoldRules.Read(null) && !HoldRules.Read("maybe") && !HoldRules.Read(new List<object>()),
		"the config's holdTight: true or false (also on, 1 or 0); anything else is off");
	Dictionary<string, object> written = MiniJson.ParseObject("{ \"holdTight\": true, \"other\": false }");
	Check(HoldRules.Read(written["holdTight"]) && !HoldRules.Read(written["other"]), "and survives the mod's JSON reader");
}

Console.WriteLine("Hero's Journey: the game's record, drawn as its ending draws it");
{
	// Two numbers per point: x across the loop at 63.999 a metre (top bit: unseen from above), and
	// how far up at 13.107 a metre; both over 512. Zeros are unwritten, a repeat is skipped.
	JourneyPath path = new JourneyPath();
	ushort[] raw = { 0x8000 | 32000, 6553, 0, 0, 32000, 6553, 100, 13107, 100, 13107 + 66, 0, 0 };
	path.Add(raw, raw.Length);
	Check(path.Count == 3, "unwritten pairs and points that haven't moved are skipped (the unseen bit isn't a move)");
	Check(Near(path.X(0), 32000f / 63.999f / 512f, 1e-5f) && Near(path.Y(0), 6553f / 13.107f / 512f, 1e-5f)
		&& Near(path.X(1), 100f / 63.999f / 512f, 1e-5f) && Near(path.Y(1), 1f / 512f * 1000f, 1e-4f),
		"decoded to the ending's units: x 0..1 across the loop, y up the mountain, metres / 512");
	path.Add(new ushort[] { 100, 13107 + 66 }, 2);
	Check(path.Count == 3, "a later file's first point is checked against the last one kept too");
	path.BuildDots();
	Check(path.IsSeam(1) && path.DotBig(1) && Near(path.DotX(1), path.X(0)) && !path.DotBig(2) && Near(path.DotX(2), path.X(1))
		&& path.DotSegment(1) == 1 && path.DotSegment(2) == 1,
		"a step of more than 0.75 across is the seam: a big dot ends the stroke, a new one starts on the other side");
	Check(!path.DotBig(0) && path.DotSegment(0) == 0 && path.FirstDotOf(1) == 1 && path.FirstDotOf(2) == 3,
		"the first point's dot comes first, before any segment");

	// Dots every 0.01, stepping from the last dot towards the next point; the rest carries on.
	JourneyPath line = new JourneyPath();
	ushort[] Pair(float x, float y) => new ushort[] { (ushort)Math.Round(x * 512f * 63.999f), (ushort)Math.Round(y * 512f * 13.107f) };
	foreach (var (x, y) in new[] { (0.1f, 0.1f), (0.1f, 0.155f), (0.1f, 0.2f) })
	{
		ushort[] p = Pair(x, y);
		line.Add(p, 2);
	}
	line.BuildDots();
	List<float> ys = new List<float>();
	for (int i = 0; i < line.DotCount; i++)
	{
		ys.Add(line.DotY(i));
	}
	bool evenly = true;
	for (int i = 1; i < ys.Count; i++)
	{
		evenly &= Near(ys[i] - ys[i - 1], JourneyPath.DotSpacing, 1e-4f);
	}
	// 0.0550 then 0.0450 up (as stored): 5 dots, then 4 more from the last one, not from the point.
	Check(line.DotCount == 10 && evenly && line.DotSegment(5) == 1 && line.DotSegment(6) == 2 && line.Y(2) - ys[9] < JourneyPath.DotSpacing,
		"a dot every 0.01 along the path, the remainder carried round to the next segment");
	line.Tip(2, 0.5f, out float tipX, out float tipY);
	int partial = line.PartialDots(2, tipX, tipY, (x, y) => { });
	Check(Near(tipY, (line.Y(1) + line.Y(2)) * 0.5f, 1e-5f) && partial == 2, "part way along, the tip is there and the segment's dots step towards it");
	Check(Math.Abs(line.Speed - 13f) < 1e-4f, "13 points a second");
	JourneyPath longer = new JourneyPath();
	ushort[] many = new ushort[2 * 17280];
	for (int i = 0; i < 17280; i++)
	{
		many[2 * i] = (ushort)(i % 30000 + 1);
		many[2 * i + 1] = (ushort)(1000 + i);
	}
	longer.Add(many, many.Length);
	Check(longer.Count == 17280 && Math.Abs(longer.Speed - 26f) < 1e-3f, "and for more than 8640 points, faster in proportion: never longer than 8640 would take");
	longer.YRange(out float low, out float high);
	Check(Near(low, 1000f / 13.107f / 512f, 1e-5f) && Near(high, 18279f / 13.107f / 512f, 1e-5f), "the y range covers the whole record");
}

Console.WriteLine(failures == 0 ? "all passed" : $"{failures} FAILED");
return failures == 0 ? 0 : 1;

static bool Near(float a, float b, float tol = 0.001f) => Math.Abs(a - b) <= tol;

static string FindRoot()
{
	for (DirectoryInfo d = new DirectoryInfo(Directory.GetCurrentDirectory()); d != null; d = d.Parent)
	{
		if (Directory.Exists(Path.Combine(d.FullName, "package", "Mods")))
		{
			return d.FullName;
		}
	}
	throw new DirectoryNotFoundException("run from inside the repository, or pass its path");
}

/// <summary>A made-up world round Nate for the summon spot checks: ground of any shape, walls and rocks, items lying about.</summary>
sealed class TestWorld : ISpotProbe
{
	/// <summary>The ground's height at (x, z); null where there's none (a drop).</summary>
	public Func<float, float, float?> Height = (x, z) => 0f;

	/// <summary>How level the ground is (its normal's y).</summary>
	public Func<float, float, float> Level = (x, z) => 1f;

	/// <summary>Walls and rocks: they block the view and take up room.</summary>
	public readonly List<(Vector3 min, Vector3 max)> Solids = new List<(Vector3, Vector3)>();

	/// <summary>Things lying about: they take up room but don't block the view (as items don't).</summary>
	public readonly List<(Vector3 min, Vector3 max)> Items = new List<(Vector3, Vector3)>();

	public bool Blocked(Vector3 a, Vector3 b)
	{
		for (int i = 0; i <= 200; i++)
		{
			float t = i / 200f;
			Vector3 p = new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
			float? ground = Height(p.x, p.z);
			if ((ground != null && p.y < ground.Value) || Solids.Any(s => Inside(p, s.min, s.max)))
			{
				return true;
			}
		}
		return false;
	}

	public bool Ground(Vector3 from, float depth, out Vector3 point, out float level)
	{
		float? ground = Height(from.x, from.z);
		point = default;
		level = 0f;
		if (ground == null || ground.Value > from.y || from.y - ground.Value > depth)
		{
			return false;
		}
		point = new Vector3(from.x, ground.Value, from.z);
		level = Level(from.x, from.z);
		return true;
	}

	public bool Occupied(Vector3 c, Vector3 half)
	{
		Vector3 min = new Vector3(c.x - half.x, c.y - half.y, c.z - half.z);
		Vector3 max = new Vector3(c.x + half.x, c.y + half.y, c.z + half.z);
		if (Solids.Concat(Items).Any(s => min.x < s.max.x && max.x > s.min.x && min.y < s.max.y && max.y > s.min.y && min.z < s.max.z && max.z > s.min.z))
		{
			return true;
		}
		for (int i = 0; i < 4; i++)
		{
			float? ground = Height((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.z : max.z);
			if (ground != null && min.y < ground.Value)
			{
				return true;
			}
		}
		return false;
	}

	private static bool Inside(Vector3 p, Vector3 min, Vector3 max)
	{
		return p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z;
	}
}
