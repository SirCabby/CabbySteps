using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Il2CppInterop.Runtime;
using MelonLoader;
using UnityEngine;
using UnityEngine.Profiling;

namespace CabbySteps;

/// <summary>
/// Leak hunting, on while "debugMemory" is set in the config. Once a minute (and on F10) it logs
/// two lines saying where memory is going:
/// <list type="bullet">
/// <item>the process as Windows sees it (working set, private bytes);</item>
/// <item>the game's GC heap (managed game objects, and every string or array the mod hands to
/// the game), Unity's native allocator (textures, meshes...), FMOD (the game's audio) and the
/// mod's own .NET heap;</item>
/// <item>counts of Unity objects by type;</item>
/// <item>how often the mod's own suspects ran since the last report;</item>
/// <item>what the mod itself allocated on the game's heap (read around its update and its
/// drawing, counted only when no collection ran in between), and collections the
/// <see cref="GcWatchdog"/> had to ask for.</item>
/// </list>
/// Every figure comes with its change per minute, so whatever climbs with the process is the leak.
/// F11 first forces a full collection (the game's GC, .NET's, and unused Unity assets) and reports
/// two seconds later: what is still there then is really being held on to, not just garbage.
/// </summary>
public class MemoryDiagnostics
{
	// What the mod did since the last report, counted at the call sites.
	public static long DialogKeyChecks;

	public static long LoaderSearches;

	public static long StyleBuilds;

	public static long GuiContents;

	public static long OnGuiCalls;

	private const float FirstReportAfter = 30f;

	/// <summary>Where the mod was when it allocated: its per-frame update, the minimap and HUD, or the full map.</summary>
	public enum Section
	{
		Update,
		Gui,
		MapGui
	}

	private readonly long[] _sectionBytes = new long[3];

	private int _sectionSkips;

	private int _probeCollections;

	/// <summary>
	/// Unity objects are counted only every few reports: each count makes the game build arrays of
	/// every object of a type (130,000 for all of them), a megabyte or two on its heap.
	/// </summary>
	private const int CountEvery = 5;

	private int _reports;

	private int[] _counted;

	private float _countedAt;

	private sealed class Sample
	{
		public float Time;

		public long WorkingSet = -1;

		public long Private = -1;

		public long GameHeap = -1;

		public int GameCollections = -1;

		public long UnityAllocated = -1;

		public long UnityReserved = -1;

		public long Fmod = -1;

		public long DotNet;

		public int[] Objects;
	}

	private static readonly string[] TypeNames = { "all", "GameObject", "Texture", "Mesh", "Material", "AudioClip", "Sprite" };

	private readonly MelonLogger.Instance _log;

	private readonly ModContext _ctx;

	private readonly GcWatchdog _watchdog;

	private readonly float _interval;

	private Il2CppSystem.Type[] _types;

	private Sample _first;

	private Sample _last;

	private float _next;

	private float _reportAfterCollect = -1f;

	private long _frames;

	private float _mapOpenSeconds;

	private int _marksWrites;

	private int _errors;

	public MemoryDiagnostics(MelonLogger.Instance log, ModContext ctx, float interval, GcWatchdog watchdog)
	{
		_log = log;
		_ctx = ctx;
		_watchdog = watchdog;
		_interval = Fast.Max(10f, interval);
		_next = Frame.Now + FirstReportAfter;
	}

	/// <summary>The game's heap in use, at the start of a stretch of the mod's own work (-1 if unknown).</summary>
	public long ProbeStart()
	{
		try
		{
			_probeCollections = Il2CppSystem.GC.CollectionCount(0);
			return Profiler.GetMonoUsedSizeLong();
		}
		catch
		{
			return -1;
		}
	}

	/// <summary>Count what the heap grew by since <see cref="ProbeStart"/>, unless a collection ran meanwhile.</summary>
	public void ProbeEnd(long start, Section section)
	{
		if (start < 0)
		{
			return;
		}
		try
		{
			long end = Profiler.GetMonoUsedSizeLong();
			if (Il2CppSystem.GC.CollectionCount(0) != _probeCollections || end < start)
			{
				_sectionSkips++;
				return;
			}
			_sectionBytes[(int)section] += end - start;
		}
		catch
		{
		}
	}

	public void Update(bool mapOpen)
	{
		_frames++;
		if (mapOpen)
		{
			_mapOpenSeconds += Frame.Dt;
		}
		float now = Frame.Now;
		if (Controls.KeyDown(KeyCode.F10))
		{
			Report("F10");
		}
		if (Controls.KeyDown(KeyCode.F11))
		{
			ForceCollect();
			_reportAfterCollect = now + 2f;
			_ctx.Toasts.Show("Memory: forced a full collection, report in the log in 2 s");
		}
		if (_reportAfterCollect > 0f && now >= _reportAfterCollect)
		{
			_reportAfterCollect = -1f;
			Report("after a forced full collection");
		}
		if (now >= _next)
		{
			_next = now + _interval;
			Report(null);
		}
	}

	private void ForceCollect()
	{
		try
		{
			GameCollect();
			GC.Collect();
			GC.WaitForPendingFinalizers();
			GC.Collect();
		}
		catch (Exception ex)
		{
			_log.Warning("[mem] forced collection failed: " + ex.Message);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void GameCollect()
	{
		Il2CppSystem.GC.Collect();
		Resources.UnloadUnusedAssets();
	}

	private void Report(string why)
	{
		try
		{
			Sample s = Measure();
			_first ??= s;
			Sample prev = _last ?? s;
			float minutes = Fast.Max(1f / 60f, (s.Time - prev.Time) / 60f);
			StringBuilder a = new StringBuilder(400);
			a.Append("[mem] ").Append(((s.Time - _first.Time) / 60f).ToString("0.0", CultureInfo.InvariantCulture)).Append(" min");
			if (why != null)
			{
				a.Append(" (").Append(why).Append(')');
			}
			a.Append(" | process: working set ").Append(Size(s.WorkingSet, prev.WorkingSet, minutes))
				.Append(", private ").Append(Size(s.Private, prev.Private, minutes))
				.Append(" | game GC heap ").Append(Size(s.GameHeap, prev.GameHeap, minutes))
				.Append((s.GameCollections >= 0) ? $" ({s.GameCollections} collections)" : "")
				.Append(" | Unity allocated ").Append(Size(s.UnityAllocated, prev.UnityAllocated, minutes))
				.Append(", reserved ").Append(Size(s.UnityReserved, prev.UnityReserved, minutes))
				.Append(" | FMOD ").Append(Size(s.Fmod, prev.Fmod, minutes))
				.Append(" | mod .NET heap ").Append(Size(s.DotNet, prev.DotNet, minutes));
			_log.Msg(a.ToString());

			StringBuilder b = new StringBuilder(400);
			if (s.Objects != null)
			{
				float sinceCount = Fast.Max(1f / 60f, (s.Time - _countedAt) / 60f);
				b.Append("[mem] objects:");
				for (int i = 0; i < TypeNames.Length; i++)
				{
					int now = s.Objects[i];
					int was = (_counted != null) ? _counted[i] : now;
					b.Append(' ').Append(TypeNames[i]).Append(' ').Append((now >= 0) ? now.ToString(CultureInfo.InvariantCulture) : "?");
					if (now >= 0 && was >= 0)
					{
						b.Append(" (").Append(((now - was) / sinceCount).ToString("+0;-0;+0", CultureInfo.InvariantCulture)).Append("/min)");
					}
				}
				_counted = s.Objects;
				_countedAt = s.Time;
			}
			else
			{
				b.Append("[mem] objects: counted every ").Append(CountEvery).Append(" reports");
			}
			int writes = _ctx.Progress.Writes;
			b.Append(" | mod, per min: dialog-key checks ").Append(PerMin(ref DialogKeyChecks, minutes))
				.Append(", loader searches ").Append(PerMin(ref LoaderSearches, minutes))
				.Append(", style builds ").Append(PerMin(ref StyleBuilds, minutes))
				.Append(", GUIContents ").Append(PerMin(ref GuiContents, minutes))
				.Append(", OnGUI calls ").Append(PerMin(ref OnGuiCalls, minutes))
				.Append(", frames ").Append(PerMin(ref _frames, minutes))
				.Append(", marks writes ").Append(((writes - _marksWrites) / minutes).ToString("0", CultureInfo.InvariantCulture))
				.Append(" | map open ").Append(Fast.Clamp01(_mapOpenSeconds / (minutes * 60f)).ToString("0%", CultureInfo.InvariantCulture))
				.Append(" of the time")
				.Append(" | mod's own allocations on the game heap, per min: update ").Append(PerMinMb(Section.Update, minutes))
				.Append(", minimap/HUD ").Append(PerMinMb(Section.Gui, minutes))
				.Append(", map ").Append(PerMinMb(Section.MapGui, minutes))
				.Append(" (").Append(_sectionSkips).Append(" readings skipped for a collection)")
				.Append(" | GC watchdog: ").Append((_watchdog != null) ? _watchdog.Forced.ToString(CultureInfo.InvariantCulture) : "off").Append(" forced");
			_sectionSkips = 0;
			_marksWrites = writes;
			_mapOpenSeconds = 0f;
			_log.Msg(b.ToString());
			_last = s;
		}
		catch (Exception ex)
		{
			if (_errors++ < 3)
			{
				_log.Warning("[mem] report failed: " + ex.Message);
			}
		}
	}

	private Sample Measure()
	{
		Sample s = new Sample
		{
			Time = Frame.Now,
			DotNet = GC.GetTotalMemory(false)
		};
		try
		{
			using Process p = Process.GetCurrentProcess();
			s.WorkingSet = p.WorkingSet64;
			s.Private = p.PrivateMemorySize64;
		}
		catch
		{
		}
		Try(() => s.GameHeap = Profiler.GetMonoUsedSizeLong());
		Try(() => s.GameCollections = Il2CppSystem.GC.CollectionCount(0));
		Try(() => s.UnityAllocated = Profiler.GetTotalAllocatedMemoryLong());
		Try(() => s.UnityReserved = Profiler.GetTotalReservedMemoryLong());
		Try(() => s.Fmod = FmodBytes());
		if (_reports++ % CountEvery == 0)
		{
			Try(() => s.Objects = CountObjects());
		}
		return s;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static long FmodBytes()
	{
		Il2CppFMOD.Memory.GetStats(out int current, out int _, false);
		return current;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private int[] CountObjects()
	{
		_types ??= new[]
		{
			Il2CppType.Of<UnityEngine.Object>(),
			Il2CppType.Of<GameObject>(),
			Il2CppType.Of<Texture>(),
			Il2CppType.Of<Mesh>(),
			Il2CppType.Of<Material>(),
			Il2CppType.Of<AudioClip>(),
			Il2CppType.Of<Sprite>()
		};
		int[] counts = new int[_types.Length];
		for (int i = 0; i < _types.Length; i++)
		{
			counts[i] = Resources.FindObjectsOfTypeAll(_types[i]).Length;
		}
		return counts;
	}

	private static void Try(Action measure)
	{
		try
		{
			measure();
		}
		catch
		{
			// Not available in this build of the game; reported as "?".
		}
	}

	private static string Size(long now, long was, float minutes)
	{
		if (now < 0)
		{
			return "?";
		}
		string text = Mb(now);
		if (was >= 0)
		{
			double perMin = (now - was) / (double)minutes / 1048576.0;
			text += " (" + perMin.ToString("+0;-0;+0", CultureInfo.InvariantCulture) + " MB/min)";
		}
		return text;
	}

	private static string Mb(long bytes)
	{
		return (bytes >= 1L << 30)
			? (bytes / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB"
			: (bytes / 1048576.0).ToString("0", CultureInfo.InvariantCulture) + " MB";
	}

	private string PerMinMb(Section section, float minutes)
	{
		long bytes = _sectionBytes[(int)section];
		_sectionBytes[(int)section] = 0;
		return (bytes / (double)minutes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
	}

	private static string PerMin(ref long counter, float minutes)
	{
		long n = counter;
		counter = 0;
		return (n / minutes).ToString("0", CultureInfo.InvariantCulture);
	}
}
