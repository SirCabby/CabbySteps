namespace CabbySteps;

/// <summary>
/// Extreme grip's rule for one foot, each time the game asks whether that foot is on the ground
/// (<c>PlayerMovement.GetGrounded</c>, from <c>GroundedCheck</c> every physics step): what
/// <see cref="ExtremeGrip"/> makes of the game's answer. Game-free, checked offline.
/// <para>
/// A foot put down (its trigger let go) that the game finds no ground under, but that touches
/// something, takes hold of it and counts as planted right there, as if on a ledge. It keeps that
/// hold until the game lifts it (its trigger, once his weight is off it), finds real ground under
/// it, or stands him up somewhere (a fall, a trip, a cutscene). A foot that's being lifted never
/// takes hold.
/// </para>
/// </summary>
public static class FootGrip
{
	/// <summary>
	/// A trigger at or below this is a foot being put down: the game's <c>TriggerMove</c> drops the
	/// foot then, and lifts it to a height above it.
	/// </summary>
	public const float PutDownTrigger = 0.05f;

	/// <summary>What happens to a foot's hold this time the game asks.</summary>
	public enum Move
	{
		/// <summary>Nothing: no hold, and none to look for.</summary>
		None,
		/// <summary>It keeps hold: the foot counts as planted where it is.</summary>
		Hold,
		/// <summary>It lets go (see <see cref="Why"/>): the game's answer stands.</summary>
		LetGo,
		/// <summary>No hold yet, but it may take one: see whether it's put down (<see cref="PutDown"/>) and touching something.</summary>
		Consider
	}

	/// <summary>Why a hold ended, for the log.</summary>
	public enum Why
	{
		None,
		/// <summary>The game is lifting the foot (its trigger, his weight off it).</summary>
		Lifted,
		/// <summary>The game found real ground under it: an ordinary step from here on.</summary>
		Ground,
		/// <summary>Nate is down, or in a cutscene.</summary>
		Down,
		/// <summary>The game stood him up (a fall, a trip, loading): his feet were put somewhere new.</summary>
		StoodUp,
		/// <summary>The game had already taken the foot off whatever it stood on.</summary>
		Unplanted
	}

	/// <summary>
	/// The rule. <paramref name="stepping"/>: the game is asking from its step (<c>GroundedCheck</c>),
	/// not while standing him up. <paramref name="down"/>: Nate is down or in a cutscene.
	/// <paramref name="ground"/>: the game's own answer (it found ground under the foot).
	/// <paramref name="holding"/>: the foot has a hold. <paramref name="planted"/>,
	/// <paramref name="lifting"/>: the foot's <c>FootData.planted</c> and <c>isLifting</c>.
	/// </summary>
	public static Move Next(bool stepping, bool down, bool ground, bool holding, bool planted, bool lifting, out Why why)
	{
		why = Why.None;
		if (!stepping || down || ground)
		{
			if (!holding)
			{
				return Move.None;
			}
			why = !stepping ? Why.StoodUp : (down ? Why.Down : Why.Ground);
			return Move.LetGo;
		}
		if (holding)
		{
			if (lifting || !planted)
			{
				why = lifting ? Why.Lifted : Why.Unplanted;
				return Move.LetGo;
			}
			return Move.Hold;
		}
		// The game sets isLifting only on a planted foot it has started lifting; a foot in the air
		// has it false, and the trigger says whether it's held up or being put down.
		return lifting ? Move.None : Move.Consider;
	}

	/// <summary>Whether a foot with this trigger is being put down (so may take hold of what it touches) rather than held up.</summary>
	public static bool PutDown(float trigger)
	{
		return trigger <= PutDownTrigger;
	}

	/// <summary>"his right foot" / "his left foot": the game's foot 0 is the right one.</summary>
	public static string FootName(int i)
	{
		return (i == 0) ? "his right foot" : "his left foot";
	}

	/// <summary>A hold's ending, for the log: "it was lifted", "it found the ground", ...</summary>
	public static string Describe(Why why)
	{
		switch (why)
		{
			case Why.Lifted:
				return "lifted";
			case Why.Ground:
				return "on solid ground again";
			case Why.Down:
				return "Nate went down";
			case Why.StoodUp:
				return "the game stood him up somewhere";
			case Why.Unplanted:
				return "the game took the foot up";
			default:
				return "let go";
		}
	}
}
