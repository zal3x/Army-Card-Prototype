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

    /// <summary>
    /// Add an event to the report.
    /// We keep this method small so it stays safe and predictable.
    /// </summary>
    public void AddEvent(CombatEvent e)
    {
        if (e == null) return;
        _events.Add(e);
    }
}
