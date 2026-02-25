using System.Collections.Generic;

/// <summary>
/// A CombatReport is the "replay log" of a combat.
/// Best practice: keep the internal list private so other code can't accidentally
/// clear it, reorder it, or add invalid events.
/// </summary>
public class CombatReport
{
    // Private list: only CombatReport controls mutation.
    private readonly List<CombatEvent> _events = new();

    // Expose read-only view: playback/UI can read, but cannot modify.
    public IReadOnlyList<CombatEvent> Events => _events;

    // Firepower totals for visual explosion ordering
    // Lower firepower player's units explode first per row
    public int TotalFirepowerA { get; private set; }
    public int TotalFirepowerB { get; private set; }

    // New combat system: Winner tracking
    // Winner is the player with higher total ATK (null if tied)
    public Player Winner { get; private set; }

    // Damage dealt to the losing commander (difference between total ATKs)
    public int TotalDamageDealt { get; private set; }

    // Starting unit counts per type (for tracking when rows are wiped)
    // Key: UnitType, Value: count
    public Dictionary<UnitType, int> StartingCountsA { get; private set; } = new();
    public Dictionary<UnitType, int> StartingCountsB { get; private set; } = new();

    /// <summary>
    /// Add an event to the report.
    /// We keep this method small so it stays safe and predictable.
    /// </summary>
    public void AddEvent(CombatEvent e)
    {
        if (e == null) return;
        _events.Add(e);
    }

    /// <summary>
    /// Store firepower totals for visual playback ordering.
    /// Called by CombatResolver after calculating attack totals.
    /// </summary>
    public void SetFirepower(int firepowerA, int firepowerB)
    {
        TotalFirepowerA = firepowerA;
        TotalFirepowerB = firepowerB;
    }

    /// <summary>
    /// Set the winner of this combat round (null if tied).
    /// Used by new combat system for visual playback (winner fires first).
    /// </summary>
    public void SetWinner(Player winner)
    {
        Winner = winner;
    }

    /// <summary>
    /// Set the total damage dealt to the losing commander.
    /// This is the difference between total ATK values.
    /// </summary>
    public void SetDamageDealt(int damage)
    {
        TotalDamageDealt = damage;
    }

    /// <summary>
    /// Store starting unit counts per type for each player.
    /// Called before combat damage begins so playback knows when rows are cleared.
    /// </summary>
    public void SetStartingCounts(Dictionary<UnitType, int> countsA, Dictionary<UnitType, int> countsB)
    {
        StartingCountsA = countsA ?? new Dictionary<UnitType, int>();
        StartingCountsB = countsB ?? new Dictionary<UnitType, int>();
    }
}
