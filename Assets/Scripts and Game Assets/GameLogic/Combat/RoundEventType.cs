public enum RoundEventType
{
    RoundStart,
    GoldGained,
    CardDrawn,
    UnitBought,
    UnitPlaced,
    Rearranged,
    CombatEvent,      // wraps an existing CombatEvent
    RoundEnd,
    DebugNote
}
