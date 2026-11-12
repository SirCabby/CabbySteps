namespace CabbySteps;

/// <summary>
/// What of the things Nate set off on a trip with is to be given back to him once he's there
/// (game-free, checked offline; <see cref="CarryGuard"/> does the giving). Things are told apart
/// by an id, the game object's address; 0 is nothing.
/// </summary>
public static class CarryPlan
{
	/// <summary>
	/// Whether hand <paramref name="hand"/> (0 right, 1 left) is to be given back what it held as he
	/// set off (<paramref name="had0"/>, <paramref name="had1"/>): it held something then, holds
	/// nothing now (<paramref name="has0"/>, <paramref name="has1"/>), that thing is still out in the
	/// world (<paramref name="usable"/>), and it isn't in his other hand now, unless it was held in
	/// both. A hand holding something else now is left be.
	/// </summary>
	public static bool Refill(int hand, long had0, long had1, long has0, long has1, bool usable)
	{
		long had = (hand == 0) ? had0 : had1;
		long has = (hand == 0) ? has0 : has1;
		long hadOther = (hand == 0) ? had1 : had0;
		long hasOther = (hand == 0) ? has1 : has0;
		if (had == 0 || has != 0 || !usable)
		{
			return false;
		}
		return hasOther != had || hadOther == had;
	}

	/// <summary>Whether to put his hat back on him: he wore one as he set off, wears none now, and it's still out in the world.</summary>
	public static bool Rewear(long had, long has, bool usable)
	{
		return had != 0 && has == 0 && usable;
	}
}
