namespace CabbySteps;

/// <summary>
/// What has the game taking something from Nate's hands or head, as the Hold tight cheat tells it
/// apart (read from the game's code: every caller of <c>PlayerMovement.DropHandItem</c> and
/// <c>KnockOffHat</c>).
/// </summary>
public enum LetGo
{
	/// <summary>
	/// Anything the cheat leaves to the game: a swap (picking up something else with that hand,
	/// putting on another hat, being given something), handing a thing over in a conversation, using
	/// it up (a potion drunk, the camera's photo taken), it breaking or melting away, the lantern
	/// going out, a trip (the teleport lets go and gives it back), the ending, the developers' keys.
	/// </summary>
	Other,

	/// <summary>Jolted while he's down (<c>DeadUpdate</c>): his head's or a hand's speed changed too fast for his grip, or for his hat.</summary>
	Jolted,

	/// <summary>A hand or an arm hitting something hard while he's down (<c>BodyPartHit</c>).</summary>
	Hit,

	/// <summary>The Baby Trophy wriggling free as he goes down, to run off (<c>RunawayTrophy.FixedUpdate</c>).</summary>
	Wriggled,

	/// <summary>The hat taken off for the night at a camp and set down by the fire (the camp's morning, <c>Menu.MorningCampFireCo</c>).</summary>
	Camp,

	/// <summary>The wind lifting the propeller hat off his head (<c>PropHat.FixedUpdate</c>).</summary>
	Blown,
}

/// <summary>
/// The Hold tight cheat's rule: whether what the game is taking from Nate stays with him
/// (game-free, checked offline; <see cref="HoldTight"/> applies it). A swap always goes ahead, and
/// so does anything the game takes for its own reasons; a thing gone from the world (switched off,
/// destroyed) is always let go of, so nothing is left in his hand that isn't there.
/// </summary>
public static class HoldRules
{
	/// <summary>Whether the cheat keeps an item in his hand: it's on, he's being made to drop it by a fall (jolted or hit while down, or the Baby Trophy wriggling free), it's no swap, and the item is still out in the world.</summary>
	public static bool KeepInHand(bool on, LetGo cause, bool swapping, bool outInTheWorld)
	{
		if (!on || swapping || !outInTheWorld)
		{
			return false;
		}
		return cause == LetGo.Jolted || cause == LetGo.Hit || cause == LetGo.Wriggled;
	}

	/// <summary>Whether the cheat keeps his hat on: it's on, the hat is coming off from a fall (jolted while down), for the night at a camp or in the wind, it's no swap, and the hat is still out in the world.</summary>
	public static bool KeepOnHead(bool on, LetGo cause, bool swapping, bool outInTheWorld)
	{
		if (!on || swapping || !outInTheWorld)
		{
			return false;
		}
		return cause == LetGo.Jolted || cause == LetGo.Camp || cause == LetGo.Blown;
	}

	/// <summary>Whether a vase knocked hard stays whole: the cheat is on and it's in his hand. One he has put down (swapped for something else) breaks as usual.</summary>
	public static bool KeepWhole(bool on, bool held)
	{
		return on && held;
	}

	/// <summary>How the config file's "holdTight" reads: true, or a number above zero; anything else is off.</summary>
	public static bool Read(object value)
	{
		switch (value)
		{
			case bool on:
				return on;
			case double number:
				return number > 0.5;
			case string text:
				switch (text.Trim().ToLowerInvariant())
				{
					case "true":
					case "on":
						return true;
				}
				break;
		}
		return false;
	}
}
