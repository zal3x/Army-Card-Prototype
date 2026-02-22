public enum CombatEventType
{
    // Debug-only notes (safe to ignore in UI)
    DebugNote,

    // Combat lifecycle
    CombatStart,
    PreemptiveStrikeDeclared,

    // Ability / synergy events (UI-safe)
    SynergyActivated,
    AbilityTriggered,
    TargetMarked,
    CounterattackTriggered,

    // Damage results (UI-safe)
    DamageAppliedToUnit,
    UnitDestroyed,
    CommanderDamaged,

    CleanupMovedToDiscard,
    CombatEnd
}



/// <summary>
/// One entry in the combat log. Playback/UI reads these in order.
/// NOTE: This is intentionally simple for a prototype.
/// Later, we can make this immutable (read-only) once the event schema stabilizes.
/// </summary>
[System.Serializable]
public class CombatEvent
{
    public CombatEventType Type;

    // Who caused it / who received it
    public Player SourcePlayer;   // can be null for system events
    public Player TargetPlayer;   // can be null for system events

    // Which units were involved (optional depending on event)
    public Unit SourceUnit;       // attacker / striker
    public Unit TargetUnit;       // unit taking damage / dying

    // Extra info
    public RowType? Row;          // where it happened (Front/Middle/Back)
    public UnitType? DamageBucket; // which UnitType bucket the damage came from (Infantry/Vehicle/Air/Ranged)
    public int Amount;            // damage amount or moved count, etc.

    // Flags for special damage types
    public bool IsCounterDamage;  // true if this damage is from a counterattack

    // Useful for UI text later; for now you can ignore it
    public string Note;

    /// <summary>
    /// Helpful for debugging when Note isn't provided.
    /// CombatPlayback can use this automatically if Note is empty.
    /// </summary>
    public override string ToString()
    {
        string srcP = SourcePlayer != null ? SourcePlayer.Name : "(no source player)";
        string tgtP = TargetPlayer != null ? TargetPlayer.Name : "(no target player)";
        string srcU = SourceUnit != null ? SourceUnit.Name : "(no source unit)";
        string tgtU = TargetUnit != null ? TargetUnit.Name : "(no target unit)";
        string row  = Row.HasValue ? Row.Value.ToString() : "(no row)";

        return $"{Type} srcP={srcP} tgtP={tgtP} srcU={srcU} tgtU={tgtU} amt={Amount} row={row}";
    }

    // -----------------------------
    // OPTIONAL helper creators (best practice)
    // These are NOT required, but they reduce mistakes later.
    // -----------------------------

    public static CombatEvent DamageToUnit(Player sourcePlayer, Player targetPlayer, Unit sourceUnit, Unit targetUnit, RowType? row, int amount, string note = null)
    {
        return new CombatEvent
        {
            Type = CombatEventType.DamageAppliedToUnit,
            SourcePlayer = sourcePlayer,
            TargetPlayer = targetPlayer,
            SourceUnit = sourceUnit,
            TargetUnit = targetUnit,
            Row = row,
            Amount = amount,
            Note = note
        };
    }

    public static CombatEvent UnitDied(Player targetPlayer, Unit targetUnit, RowType? row, string note = null)
    {
        return new CombatEvent
        {
            Type = CombatEventType.UnitDestroyed,
            TargetPlayer = targetPlayer,
            TargetUnit = targetUnit,
            Row = row,
            Note = note
        };
    }
}

