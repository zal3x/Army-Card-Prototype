using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AbilitySystem: Centralized ability execution for the entire game.
/// 
/// DESIGN PHILOSOPHY:
/// - Single source of truth for ALL ability behavior
/// - Prevents combat/buy phase logic from drifting apart over time
/// - Makes it easy to add new abilities (one place to edit)
/// - Clear separation between ability phases (OnPlay vs Combat)
/// 
/// ABILITY PHASES:
/// 
/// 1. ON-PLAY PHASE (Buy Phase)
///    - Triggers immediately when a unit is bought and placed
///    - Examples: gain gold, duplicate self, destroy enemy unit, mark target
///    - Requires IOnPlayApi to interact with game state
/// 
/// 2. COMBAT PHASE (Multiple sub-phases)
///    - Attack Snapshot: Modifies ATK totals before damage is applied
///    - Commander Bypass: Deals damage directly to commander (Navy Seal)
///    - Incoming Damage: Modifies how units take damage (Armor, Target Mark)
///    - Counterattack: Damaged units deal damage back
/// 
/// ADDING NEW ABILITIES:
/// 1. Add enum value to UnitAbilityType (Enums.cs)
/// 2. Add execution logic to the appropriate phase method here
/// 3. Document the ability behavior in comments
/// 4. Create UnitCardDefinition assets using the new ability
/// </summary>
public static class AbilitySystem
{
    // ========================================
    // BUY PHASE API (INTERFACE)
    // ========================================

    /// <summary>
    /// Interface for on-play abilities that need to interact with game state.
    /// 
    /// WHY AN INTERFACE?
    /// Some on-play abilities need help from the "buy phase controller"
    /// (because it knows maxUnitsPerRow, placement rules, deck access, etc.)
    /// 
    /// This interface lets abilities request actions without coupling
    /// AbilitySystem to BattleTestRunner or other controllers.
    /// </summary>
    public interface IOnPlayApi
    {
        /// <summary>
        /// Attempts to play a duplicate copy of a unit from the deck.
        /// Used by: DuplicateSelfFromDeckOnPlay
        /// </summary>
        bool TryPlayCopyFromDeck(Player owner, Unit sourceUnit);

        /// <summary>
        /// Destroys the strongest enemy vehicle if their vehicle row is full.
        /// Used by: DestroyStrongestEnemyVehicleIfEnemyVehicleRowIsFullOnPlay
        /// </summary>
        void TryDestroyStrongestEnemyVehicleIfRowFull(Player owner, Player enemy);

        /// <summary>
        /// Marks a random enemy unit for 2x vulnerability during combat.
        /// Used by: TargetMark
        /// </summary>
        void ApplyTargetMarkOnPlay(Player owner, Player enemy, Unit markerUnit);
    }

    // ========================================
    // ON-PLAY ABILITIES (BUY PHASE)
    // ========================================

    /// <summary>
    /// Executes on-play abilities immediately after a unit is bought and placed.
    /// 
    /// WHEN THIS IS CALLED:
    /// - After gold is spent
    /// - After card is removed from hand
    /// - After unit is placed on the board
    /// - Before rearrangement phase
    /// - Before combat phase
    /// 
    /// PARAMETERS:
    /// - owner: Player who bought the unit
    /// - enemy: Opponent player
    /// - unit: The unit that was just placed
    /// - api: Interface to request game state changes
    /// </summary>
    public static void ExecuteOnPlay(Player owner, Player enemy, Unit unit, IOnPlayApi api)
    {
        switch (unit.Ability)
        {
            // ========================================
            // ECONOMY ABILITIES
            // ========================================

            case UnitAbilityType.GainGoldOnPlay:
            {
                int amount = unit.AbilityData.Amount;
                owner.Gold += amount;

                Debug.Log($"[ON-PLAY] {owner.Name} gains {amount} gold from {unit.Name}. GoldNow={owner.Gold}");
                break;
            }

            // ========================================
            // SUMMONING ABILITIES
            // ========================================

            case UnitAbilityType.DuplicateSelfFromDeckOnPlay:
            {
                // Searches deck for another copy of this unit and plays it immediately
                // (if there's room in the row)
                api.TryPlayCopyFromDeck(owner, unit);
                break;
            }

            // ========================================
            // DESTRUCTION ABILITIES
            // ========================================

            case UnitAbilityType.DestroyStrongestEnemyVehicleIfEnemyVehicleRowIsFullOnPlay:
            {
                // Destroys the strongest enemy vehicle (highest ATK, then HP)
                // but ONLY if their vehicle row is completely full
                api.TryDestroyStrongestEnemyVehicleIfRowFull(owner, enemy);
                break;
            }

            // ========================================
            // TARGETING ABILITIES
            // ========================================

            case UnitAbilityType.TargetMark:
            {
                // Marks a random enemy unit for 2x vulnerability
                // Mark persists until the unit dies or is replaced
                api.ApplyTargetMarkOnPlay(owner, enemy, unit);
                break;
            }

            // ========================================
            // PASSIVE COMBAT ABILITIES (NO ACTION NEEDED)
            // ========================================

            case UnitAbilityType.TauntRowFirst:
            {
                // Taunt is a passive combat routing rule (handled in CombatResolver)
                // Nothing to do here, but we log it for clarity
                Debug.Log($"[ON-PLAY] {owner.Name} placed TAUNT unit: {unit.Name} (UnitId={unit.InstanceId})");
                break;
            }

            // No default case - abilities that don't have on-play effects simply do nothing here
        }
    }

    // ========================================
    // COMBAT ABILITIES: ATTACK SNAPSHOT
    // ========================================

    /// <summary>
    /// Calculates the attack value for a unit after applying ability modifiers.
    /// 
    /// WHEN THIS IS CALLED:
    /// - Once per unit, before damage is applied
    /// - After pre-emptive strikes are resolved
    /// - Before damage streams are calculated
    /// 
    /// ABILITY TYPES HANDLED HERE:
    /// - Conditional ATK multipliers (RPG, 3-of-a-kind, full health)
    /// - Row-wide buffs (future: Commander ability)
    /// - Enemy-dependent modifiers (future: SOS below 50% HP)
    /// 
    /// RETURNS:
    /// Final ATK value for this unit (used in damage stream calculation)
    /// </summary>
    public static int GetAttackForSnapshot(
        Unit unit,
        Player owner,
        Player enemy,
        RowType row,
        Dictionary<string, int> aliveCountByNameOnRow,
        bool enemyHasTank,
        CombatReport report,
        bool alsoDebugLog)
    {
        int atk = unit.ATK;

        // ========================================
        // RPG: Double ATK if enemy has any alive tank
        // ========================================
        if (unit.Ability == UnitAbilityType.DoubleAttackIfEnemyHasTank && enemyHasTank)
        {
            atk *= 2;
            AddAbilityEvent(report, alsoDebugLog,
                $"[ABILITY] {owner.Name} {row}: '{unit.Name}' => enemy has tank, ATK doubled",
                sourcePlayer: owner, sourceUnit: unit, row: row, amount: 2);
        }

        // ========================================
        // 3-OF-A-KIND: Double ATK if 3+ copies in this row
        // ========================================
        if (unit.Ability == UnitAbilityType.DoubleAttackIfThreeOfKindOnRow)
        {
            if (aliveCountByNameOnRow.TryGetValue(unit.Name, out int count) && count >= 3)
            {
                atk *= 2;
                AddAbilityEvent(report, alsoDebugLog,
                    $"[SYNERGY] {owner.Name} {row}: {count}x '{unit.Name}' => double ATK (3-of-a-kind)",
                    sourcePlayer: owner, sourceUnit: unit, row: row, amount: 2);
            }
        }

        // ========================================
        // FULL HEALTH: Double ATK if at max HP
        // ========================================
        if (unit.Ability == UnitAbilityType.DoubleAttackIfFullHealth)
        {
            if (unit.HP == unit.MaxHP)
            {
                atk *= 2;
                AddAbilityEvent(report, alsoDebugLog,
                    $"[ABILITY] {owner.Name} {row}: '{unit.Name}' full HP ({unit.HP}/{unit.MaxHP}) => double ATK",
                    sourcePlayer: owner, sourceUnit: unit, row: row, amount: 2);
            }
        }

        return atk;
    }

    // ========================================
    // COMBAT ABILITIES: COMMANDER BYPASS
    // ========================================

    /// <summary>
    /// Calculates commander bypass damage for a unit (Navy Seal ability).
    /// 
    /// COMMANDER BYPASS:
    /// - Damage goes directly to enemy commander (ignores units entirely)
    /// - Applied BEFORE main damage streams
    /// - Based on unit's ATK × multiplier (typically 25%)
    /// 
    /// EXAMPLE:
    /// Navy Seal with 6 ATK and 25% multiplier = 1.5 → 2 bypass damage
    /// 
    /// RETURNS:
    /// Bypass damage amount (0 if unit doesn't have this ability)
    /// </summary>
    public static int GetCommanderBypassDamage(Unit unit)
    {
        if (unit.Ability != UnitAbilityType.CommanderBypassDamage25Percent)
            return 0;

        float m = unit.AbilityData.Multiplier;
        if (m <= 0f) m = 0.25f; // Default 25% for Navy Seal

        return Mathf.RoundToInt(unit.ATK * m);
    }

    // ========================================
    // COMBAT ABILITIES: INCOMING DAMAGE MODIFICATION
    // ========================================
    // NOTE: This section is currently handled directly in CombatResolver
    // because damage resolution is tightly coupled with the streaming algorithm.
    // 
    // The following abilities are implemented in CombatResolver.ResolveIncomingDamageForUnit:
    // - ReduceIncomingDamage50Percent (Armored Tank)
    // - ArmorPiercing (disables damage reduction)
    // - TargetMark (2x HP damage vulnerability)
    // - Counterattack (50% damage back to attacker)
    // 
    // If we wanted to centralize them here, we'd need to create a more complex
    // damage resolution API. For now, keeping them in CombatResolver is cleaner.

    // ========================================
    // HELPER METHODS
    // ========================================

    /// <summary>
    /// Adds an ability-triggered event to the combat report.
    /// 
    /// WHY NOT USE Debug.Log DIRECTLY?
    /// - CombatResolver controls console noise (simulation should be quiet by default)
    /// - All events go into the report for playback/UI display
    /// - Only CombatResolver decides what prints to console (via alsoDebugLog flag)
    /// </summary>
    private static void AddAbilityEvent(
        CombatReport report,
        bool alsoDebugLog,
        string note,
        Player sourcePlayer = null,
        Unit sourceUnit = null,
        Player targetPlayer = null,
        Unit targetUnit = null,
        RowType? row = null,
        int amount = 0)
    {
        // Use SynergyActivated as the generic "ability fired" event
        // (You can split this into AbilityTriggered vs SynergyActivated later if needed)
        report.AddEvent(new CombatEvent
        {
            Type = CombatEventType.SynergyActivated,
            Note = note,
            SourcePlayer = sourcePlayer,
            SourceUnit = sourceUnit,
            TargetPlayer = targetPlayer,
            TargetUnit = targetUnit,
            Row = row,
            Amount = amount
        });

        // IMPORTANT: Do NOT print Debug.Log here
        // CombatResolver controls console output via alsoDebugLog
        // The report is the authoritative truth; playback decides what to show
    }
}

// ========================================
// ABILITY CATALOG (FOR REFERENCE)
// ========================================
// This section documents all abilities currently in the game.
// Update this list when adding new abilities!
//
// === ON-PLAY ABILITIES (Buy Phase) ===
// - GainGoldOnPlay: Immediately gain gold when placed
// - DuplicateSelfFromDeckOnPlay: Play another copy from deck if available
// - DestroyStrongestEnemyVehicleIfEnemyVehicleRowIsFullOnPlay: Conditional destruction
// - TargetMark: Mark a random enemy unit for 2x vulnerability
// - TauntRowFirst: Passive - forces damage to this row first
//
// === COMBAT ABILITIES (Attack Snapshot) ===
// - DoubleAttackIfEnemyHasTank: RPG - 2x ATK if enemy has vehicles
// - DoubleAttackIfThreeOfKindOnRow: 3-of-a-kind synergy - 2x ATK if 3+ copies
// - DoubleAttackIfFullHealth: 2x ATK if at max HP
//
// === COMBAT ABILITIES (Commander Bypass) ===
// - CommanderBypassDamage25Percent: Navy Seal - 25% of ATK goes directly to commander
//
// === COMBAT ABILITIES (Damage Modification) ===
// - ReduceIncomingDamage50Percent: Armored Tank - takes 50% damage (or dies if overwhelmed)
// - ArmorPiercing: Disables ReduceIncomingDamage50Percent on enemies
// - Counterattack: Damaged units deal 50% damage back to attacker
// - TargetMark (passive): Marked units take 2x HP damage from streams
//
// === FUTURE ABILITIES (Planned) ===
// - Snipe: Deals damage directly to back row
// - SOS: Double damage if commander HP below 50%
// - Buddy System: Double damage if placed next to same unit type
// - Commander: Doubles ATK of all units in row
// - Mechanic: Doubles HP of all units in row
// - Spy: Infiltrate enemy field, gain gold while alive
// - Sabotage: Destroy strongest infantry if enemy infantry row full
// - Bombard: Destroy strongest ranged if 3+ ranged units fielded
// - Death from Below: Destroy strongest air if enemy air row full
// - Last Stand: Survive with 1 HP, absorb total damage
// - From the Grave: Deal 5 damage upon death
//