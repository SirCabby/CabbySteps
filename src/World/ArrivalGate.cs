namespace CabbySteps;

/// <summary>
/// When the game's teleport may put Nate down at a saved location. The game's own teleport
/// (<c>Menu.TeleportCo</c>) goes as soon as its loader says the world round the spot is in
/// (<c>BestRegionLoader.fullyLoaded</c>), and Nate keeps moving where he is all the while it waits.
/// A trip is held (<see cref="Recall"/> keeps that flag down) until, all at once:
/// <list type="bullet">
/// <item>the loader has said the world is in for <see cref="ReadyFrames"/> frames and
/// <see cref="ReadyTime"/> seconds in a row (one frame's word can be stale: what it loads round
/// the spot is worked out a frame late), and nothing more is loading for <see cref="QuietFrames"/>
/// frames in a row, or <see cref="LoadingPatience"/> seconds have gone by (far-off scenery can keep
/// loading for a while, and doesn't matter);</item>
/// <item>there's ground under each foot he was saved standing on (given up on after
/// <see cref="FootingPatience"/> seconds: what he stood on may have moved);</item>
/// <item>he has been still for <see cref="StillSteps"/> physics steps in a row: he's held still
/// every step while the trip waits (<see cref="Step"/>), so whatever he was doing (tumbling,
/// falling, falling through ground that unloaded under him as the world loaded round the spot) is
/// over before the teleport switches him off.</item>
/// </list>
/// It never holds a trip longer than <see cref="MaxHold"/> seconds. Pure logic, so it can be
/// checked without the game; times are real seconds.
/// </summary>
public sealed class ArrivalGate
{
	/// <summary>Frames in a row the loader has to say the world is in.</summary>
	public const int ReadyFrames = 6;

	/// <summary>Seconds in a row the loader has to say the world is in.</summary>
	public const float ReadyTime = 0.15f;

	/// <summary>Frames in a row with nothing loading (props arrive a little after their chunks).</summary>
	public const int QuietFrames = 4;

	/// <summary>Once the world is in, how long to wait for nothing to be loading before going anyway.</summary>
	public const float LoadingPatience = 1f;

	/// <summary>Once the world is in, how long to wait for ground under his saved feet.</summary>
	public const float FootingPatience = 2.5f;

	/// <summary>Physics steps in a row with every part of him slower than <see cref="StillSpeed"/>.</summary>
	public const int StillSteps = 8;

	/// <summary>Metres a second: a part of Nate slower than this is still.</summary>
	public const float StillSpeed = 0.6f;

	/// <summary>
	/// Seconds of holding him still after which he counts as still anyway: what he could carry
	/// into the trip is gone after a few steps, and his own muscles can keep a limb moving a little
	/// faster than <see cref="StillSpeed"/> each step.
	/// </summary>
	public const float StillPatience = 0.5f;

	/// <summary>If physics doesn't step at all (the game frozen), stop waiting for stillness after this many seconds.</summary>
	public const float StillTimeout = 1.5f;

	/// <summary>The longest a trip is ever held, in seconds.</summary>
	public const float MaxHold = 20f;

	private float _start;

	private float _now;

	private bool _needFooting;

	private int _readyFrames;

	private float _readySince;

	private int _quietFrames;

	private bool _footingDone;

	private int _stillSteps;

	// Seconds into the trip: when the world was (last) found in, quiet, the footing there, him still (negative: not yet).
	private float _loadedAt;

	private float _quietAt;

	private float _footingAt;

	private float _stillAt;

	/// <summary>The trip is held: the teleport mustn't put Nate down yet.</summary>
	public bool Holding { get; private set; }

	/// <summary>How long the trip was held (seconds), once it's let go.</summary>
	public float Held { get; private set; }

	/// <summary>Physics steps he was held still for.</summary>
	public int Steps { get; private set; }

	/// <summary>The fastest any part of him was going while held (m/s): at the start, what he was doing when sent off.</summary>
	public float FastestSeen { get; private set; }

	/// <summary>Things were still loading when he went (only far-off scenery, by then).</summary>
	public bool StillLoading { get; private set; }

	/// <summary>No ground turned up under his saved feet; he was let go without it.</summary>
	public bool FootingMissing { get; private set; }

	/// <summary>Let go because it was held as long as it may be.</summary>
	public bool TimedOut { get; private set; }

	/// <summary>Let go because physics didn't step (the game frozen), not because he was seen still.</summary>
	public bool StillnessUnseen { get; private set; }

	/// <summary>
	/// While held, with the world in but no ground found yet under his saved feet: the caller
	/// should look, and say what it found in the next <see cref="Update"/>.
	/// </summary>
	public bool WantsFooting => Holding && _needFooting && !_footingDone && Loaded;

	private bool Loaded => _readyFrames >= ReadyFrames && _now - _readySince >= ReadyTime;

	/// <summary>A trip starts now. <paramref name="needFooting"/>: he's to stand on saved spots, so wait for ground under them.</summary>
	public void Begin(float now, bool needFooting)
	{
		_start = now;
		_now = now;
		_needFooting = needFooting;
		_readyFrames = 0;
		_readySince = now;
		_quietFrames = 0;
		_footingDone = !needFooting;
		_stillSteps = 0;
		_loadedAt = -1f;
		_quietAt = -1f;
		_footingAt = -1f;
		_stillAt = -1f;
		Holding = true;
		Held = 0f;
		Steps = 0;
		FastestSeen = 0f;
		StillLoading = false;
		FootingMissing = false;
		TimedOut = false;
		StillnessUnseen = false;
	}

	/// <summary>Stop holding, whatever the state (the trip ended, or the game moved him on regardless).</summary>
	public void Abandon(float now)
	{
		if (Holding)
		{
			Holding = false;
			Held = now - _start;
		}
	}

	/// <summary>
	/// Each physics step while the trip waits, with the speed of the fastest part of Nate (m/s,
	/// before he's held still this step). True while the trip is held: hold him still now.
	/// </summary>
	public bool Step(float now, float fastest)
	{
		if (!Holding)
		{
			return false;
		}
		Steps++;
		if (fastest > FastestSeen)
		{
			FastestSeen = fastest;
		}
		if (fastest < StillSpeed)
		{
			if (++_stillSteps >= StillSteps && _stillAt < 0f)
			{
				_stillAt = now - _start;
			}
		}
		else
		{
			_stillSteps = 0;
			_stillAt = -1f;
		}
		return true;
	}

	/// <summary>
	/// Each frame while the trip waits: <paramref name="loaderReady"/>, the game's loader last said
	/// the world round the spot is in; <paramref name="loading"/>, something is loading right now;
	/// <paramref name="footing"/>, there's ground under his saved feet (only looked at while
	/// <see cref="WantsFooting"/>, before this call). Lets the trip go once everything's ready.
	/// </summary>
	public void Update(float now, bool loaderReady, bool loading, bool footing)
	{
		if (!Holding)
		{
			return;
		}
		bool askedFooting = WantsFooting;
		_now = now;
		if (loaderReady)
		{
			if (_readyFrames == 0)
			{
				_readySince = now;
			}
			_readyFrames++;
		}
		else
		{
			_readyFrames = 0;
			_readySince = now;
			_quietFrames = 0;
		}
		bool loaded = Loaded;
		if (!loaded)
		{
			_loadedAt = -1f;
			_quietAt = -1f;
		}
		else if (_loadedAt < 0f)
		{
			_loadedAt = now - _start;
		}
		_quietFrames = (loaded && !loading) ? _quietFrames + 1 : 0;
		if (_quietFrames >= QuietFrames && _quietAt < 0f)
		{
			_quietAt = now - _start;
		}
		bool settled = loaded && (_quietFrames >= QuietFrames || now - _readySince >= LoadingPatience);
		if (loaded && !_footingDone)
		{
			if (askedFooting && footing)
			{
				_footingDone = true;
				_footingAt = now - _start;
			}
			else if (now - _readySince >= FootingPatience)
			{
				_footingDone = true;
				FootingMissing = true;
			}
		}
		bool still = _stillAt >= 0f || (Steps >= StillSteps && now - _start >= StillPatience);
		if (!still && Steps == 0 && now - _start >= StillTimeout)
		{
			// Physics isn't stepping (the game is frozen): there's nothing to wait for.
			still = true;
			StillnessUnseen = true;
		}
		if (settled && _footingDone && still)
		{
			Holding = false;
			Held = now - _start;
			StillLoading = _quietFrames < QuietFrames;
		}
		else if (now - _start >= MaxHold)
		{
			Holding = false;
			Held = now - _start;
			TimedOut = true;
			StillLoading = loading || !loaded;
		}
	}

	/// <summary>
	/// What the trip waited for, for the log: "held 1.40 s: world in after 1.10 s, quiet after 1.20 s,
	/// footing there after 1.15 s, held still for 70 physics steps (fastest part 7.3 m/s)".
	/// </summary>
	public string Summary()
	{
		System.Text.StringBuilder text = new System.Text.StringBuilder(200);
		text.Append("held ").Append(Held.ToString("0.00")).Append(" s");
		if (TimedOut)
		{
			text.Append(" (as long as a trip may be held)");
		}
		text.Append(": world in ").Append((_loadedAt >= 0f) ? ("after " + _loadedAt.ToString("0.00") + " s") : "not yet");
		if (_quietAt >= 0f)
		{
			text.Append(", quiet after ").Append(_quietAt.ToString("0.00")).Append(" s");
		}
		else if (StillLoading)
		{
			text.Append(", still loading things");
		}
		if (_needFooting)
		{
			text.Append(", footing ").Append(FootingMissing ? "not found (went without it)" : ((_footingAt >= 0f) ? ("there after " + _footingAt.ToString("0.00") + " s") : "not looked for yet"));
		}
		if (StillnessUnseen)
		{
			text.Append(", physics not running");
		}
		else
		{
			text.Append(", held still for ").Append(Steps).Append(" physics steps");
			if (_stillAt < 0f)
			{
				text.Append(" (never quite still)");
			}
		}
		text.Append(" (fastest part ").Append(FastestSeen.ToString("0.0")).Append(" m/s)");
		return text.ToString();
	}
}
