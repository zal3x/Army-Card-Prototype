using System.Collections.Generic;

/// <summary>
/// A single round's "story" in ordered events.
/// This is for testing/debugging now, and UI/replay later.
/// </summary>
public class RoundReport
{
    private readonly List<RoundEvent> _events = new();
    public IReadOnlyList<RoundEvent> Events => _events;

    public void Add(RoundEvent e)
    {
        if (e == null) return;
        _events.Add(e);
    }
}

/// <summary>
/// One event that happened during a round.
/// Some are phase events (buy/draw), and some wrap CombatEvent.
/// </summary>
public class RoundEvent
{
    public RoundEventType Type;

    public int RoundNumber;

    public Player Player;     // who it happened to (optional)
    public RowType? Row;

    public UnitCard Card;     // for draw/buy (optional)
    public Unit Unit;         // for placed unit (optional)

    public CombatEvent Combat; // for combat events (optional)

    public int Amount;        // gold gained, etc. (optional)
    public string Note;       // human-readable (optional)
}
