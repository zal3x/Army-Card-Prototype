// AbilityPhase.cs
// One canonical list of "when abilities can run".
// Anything not in this list should NOT trigger abilities.

public enum AbilityPhase
{
    // Buy / placement timeline
    OnPlay,

    // Combat timeline
    OnCombatStart,
    OnAttackSnapshot,   // when we total ATK (row buffs, conditional doubles, etc.)
    OnIncomingDamage,   // when a unit is about to process stream damage
    OnCombatEnd,
}
