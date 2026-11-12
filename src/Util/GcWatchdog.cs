using System;
using System.Runtime.CompilerServices;
using MelonLoader;
using UnityEngine.Profiling;

namespace CabbySteps;

/// <summary>
/// Works round a bug in the game's garbage collector (Unity's Boehm GC under IL2CPP, seen under
/// Proton). Now and then a collection measures the threads' stacks as negative in total (-2.9 GiB
/// on 2026-11-09). The collector decides when to collect next from that total; the sum wraps round
/// to about 3 exabytes, so it never collects by itself again, and everything allocated from then on
/// stays: that session reached a 58 GB heap and was killed for running out of memory an hour in.
/// <para>
/// The collector still collects when asked, and every collection measures the stacks afresh. So
/// when the game's collection count stands still while its heap keeps growing, this asks for one.
/// While collection works (several times a minute in play), it never does anything.
/// </para>
/// </summary>
public class GcWatchdog
{
	private const float CheckInterval = 5f;

	/// <summary>No collection for this long...</summary>
	private const float StallSeconds = 120f;

	/// <summary>...while the heap grew this much: the collector has stopped by itself.</summary>
	private const long StallGrowth = 512L << 20;

	private readonly MelonLogger.Instance _log;

	private int _count = -1;

	private long _usedAtCount;

	private float _countSince;

	private float _nextCheck;

	private int _errors;

	public GcWatchdog(MelonLogger.Instance log)
	{
		_log = log;
	}

	/// <summary>Collections asked for because the game's own had stopped.</summary>
	public int Forced { get; private set; }

	public void Update()
	{
		float now = Frame.Now;
		if (now < _nextCheck)
		{
			return;
		}
		_nextCheck = now + CheckInterval;
		try
		{
			Check(now);
		}
		catch (Exception ex)
		{
			if (_errors++ < 3)
			{
				_log.Warning("[gc] can't watch the game's garbage collector: " + ex.Message);
			}
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private void Check(float now)
	{
		int count = Il2CppSystem.GC.CollectionCount(0);
		long used = Profiler.GetMonoUsedSizeLong();
		if (count != _count)
		{
			_count = count;
			_usedAtCount = used;
			_countSince = now;
			return;
		}
		float stalled = now - _countSince;
		long growth = used - _usedAtCount;
		if (stalled < StallSeconds || growth < StallGrowth)
		{
			return;
		}
		Forced++;
		_log.Warning($"[gc] The game's garbage collector hasn't run for {stalled / 60f:0.0} min while its heap grew {growth >> 20} MB "
			+ $"(collection {count}). It stops by itself now and then (a bug in the game's GC, see the README); collecting now.");
		float started = Frame.Now;
		Il2CppSystem.GC.Collect();
		float took = Frame.Now - started;
		int after = Il2CppSystem.GC.CollectionCount(0);
		long left = Profiler.GetMonoUsedSizeLong();
		_log.Msg($"[gc] Collected in {took * 1000f:0} ms: heap {used >> 20} MB -> {left >> 20} MB, collection {after}.");
		_count = after;
		_usedAtCount = left;
		_countSince = Frame.Now;
	}
}
