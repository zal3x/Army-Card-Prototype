using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks ability activation info for UI feedback (floating damage numbers).
/// </summary>
public struct AbilityActivationInfo
{
    public bool AbilityActivated;
    public UnitAbilityType AbilityType;
    public int BonusATK;
    public string Reason;
}

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
        /// Used by: Duplicate ability
        /// </summary>
        bool TryPlayCopyFromDeck(Player owner, Unit sourceUnit);

        /// <summary>
        /// Destroys the strongest enemy vehicle if their vehicle row is full.
        /// Used by: DestroyStrongestVehicle ability
        /// </summary>
        void TryDestroyStrongestEnemyVehicleIfRowFull(Player owner, Player enemy);

        /// <summary>
        /// Destroys the weakest enemy unit (lowest ATK) on the battlefield.
        /// Used by: TargetMark ability
        /// </summary>
        void DestroyWeakestEnemyUnit(Player owner, Player enemy, Unit sourceUnit);

        /// <summary>
        /// Reduces a random enemy unit's ATK by 1.
        /// Used by: Taunt ability
        /// </summary>
        void ReduceRandomEnemyATK(Player owner, Player enemy, Unit sourceUnit, int amount);
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

            case UnitAbilityType.MrMoneybags:
            {
                int amount = unit.AbilityData.Amount;
                owner.Gold += amount;

                Debug.Log($"[ON-PLAY] {owner.Name} gains {amount} gold from {unit.Name}. GoldNow={owner.Gold}");
                break;
            }

            // ========================================
            // SUMMONING ABILITIES
            // ========================================

            case UnitAbilityType.Duplicate:
            {
                // Searches deck for another copy of this unit and plays it immediately
                // (if there's room in the row)
                api.TryPlayCopyFromDeck(owner, unit);
                break;
            }

            // ========================================
            // DESTRUCTION ABILITIES
            // ========================================

            case UnitAbilityType.DestroyStrongestVehicle:
            {
                // Destroys the strongest enemy vehicle (highest ATK, then HP)
                // but ONLY if their vehicle row is completely full
                api.TryDestroyStrongestEnemyVehicleIfRowFull(owner, enemy);
                break;
            }

            // ========================================
            // TARGETING ABILITIES (Feb 2026 rework)
            // ========================================

            case UnitAbilityType.TargetMark:
            {
                // Destroys the weakest enemy unit (lowest ATK) when played
                api.DestroyWeakestEnemyUnit(owner, enemy, unit);
                break;
            }

            // ========================================
            // DEBUFF ABILITIES (Feb 2026 rework)
            // ========================================

            case UnitAbilityType.Taunt:
            {
                // Taunt lowers the ATK of a random enemy unit by 1
                api.ReduceRandomEnemyATK(owner, enemy, unit, 1);
                break;
            }

            // No default case - abilities that don't have on-play effects simply do nothing here
        }
    }

    // ========================================
    // BUY PHASE: EFFECTIVE ATTACK CALCULATION
    // ========================================

    /// <summary>
    /// Calculates the effective attack value for a unit during the buy phase.
    /// This allows the UI to show accurate firepower including ability bonuses
    /// BEFORE combat starts.
    ///
    /// WHEN THIS IS CALLED:
    /// - During buy phase when units are placed
    /// - When FirepowerDisplay refreshes
    /// - Anytime UI needs to show ability-adjusted ATK
    ///
    /// RETURNS:
    /// Final ATK value including ability modifiers (for UI display)
    ///
    /// Also returns ability activation info for floating damage numbers.
    /// </summary>
    public static int GetEffectiveAttackForBuyPhase(
        Unit unit,
        Player owner,
        Player enemy,
        RowType row,
        Dictionary<string, int> aliveCountByNameOnRow,
        out AbilityActivationInfo activationInfo)
    {
        activationInfo = new AbilityActivationInfo();

        if (unit == null || !unit.IsAlive)
            return 0;

        int baseAtk = unit.ATK;
        int finalAtk = baseAtk;

        // ========================================
        // 3-OF-A-KIND: Double ATK if 3+ copies in this row
        // ========================================
        if (unit.Ability == UnitAbilityType.ThreeOfAKind)
        {
            if (aliveCountByNameOnRow != null &&
                aliveCountByNameOnRow.TryGetValue(unit.Name, out int count) &&
                count >= 3)
            {
                finalAtk *= 2;
                activationInfo.AbilityActivated = true;
                activationInfo.AbilityType = UnitAbilityType.ThreeOfAKind;
                activationInfo.BonusATK = baseAtk;
                activationInfo.Reason = $"3+ {unit.Name} in row";
            }
        }

        // ========================================
        // UNMATCHED: Double ATK if only one of this unit on field
        // ========================================
        if (unit.Ability == UnitAbilityType.Unmatched)
        {
            int totalCopiesOnField = CountTotalCopiesOnField(owner, unit.Name);
            if (totalCopiesOnField == 1)
            {
                finalAtk *= 2;
                activationInfo.AbilityActivated = true;
                activationInfo.AbilityType = UnitAbilityType.Unmatched;
                activationInfo.BonusATK = baseAtk;
                activationInfo.Reason = "Unmatched";
            }
        }

        // ========================================
        // ARMOR PIERCING: +1 ATK per enemy ARMORED unit
        // ========================================
        if (unit.Ability == UnitAbilityType.ArmorPiercing)
        {
            int armoredCount = CountEnemyArmoredUnits(enemy);
            if (armoredCount > 0)
            {
                finalAtk += armoredCount;
                activationInfo.AbilityActivated = true;
                activationInfo.AbilityType = UnitAbilityType.ArmorPiercing;
                activationInfo.BonusATK = armoredCount;
                activationInfo.Reason = $"+{armoredCount} vs Armored";
            }
        }

        // ========================================
        // SOS: Double ATK when commander HP below 20%
        // ========================================
        if (unit.Ability == UnitAbilityType.SOS && owner != null)
        {
            float hpPercent = (float)owner.CommanderHP / owner.CommanderMaxHP;
            if (hpPercent < 0.2f)
            {
                finalAtk *= 2;
                activationInfo.AbilityActivated = true;
                activationInfo.AbilityType = UnitAbilityType.SOS;
                activationInfo.BonusATK = baseAtk;
                activationInfo.Reason = "SOS! Low HP";
            }
        }

        // ========================================
        // BROTHER IN ARMS: Double ATK if another copy exists
        // ========================================
        if (unit.Ability == UnitAbilityType.BrotherInArms)
        {
            int totalCopiesOnField = CountTotalCopiesOnField(owner, unit.Name);
            if (totalCopiesOnField >= 2)
            {
                finalAtk *= 2;
                activationInfo.AbilityActivated = true;
                activationInfo.AbilityType = UnitAbilityType.BrotherInArms;
                activationInfo.BonusATK = baseAtk;
                activationInfo.Reason = "Brother in Arms!";
            }
        }

        // Commander bonuses are NOT tracked as ability activations.
        // They are permanent stat changes applied at battle start via:
        // - CommanderBonus.ApplyPermanentBonusesToCard() (for always-on bonuses)
        // - CommanderBonus.ApplyPermanentBonusesToUnit() (for unit-level bonuses)
        // The stats are already baked into unit.ATK at this point.
        //
        // Conditional bonuses (terrain/weather like Seth/Aqua) are also NOT activations -
        // they're just part of the battlefield state that affects combat calculations.
        //
        // DO NOT add ability activation tracking here for commander bonuses.

        // ========================================
        // SPECIAL CARD MODIFIERS (Barbed Wire, Trenches, etc.)
        // ========================================
        if (SpecialCards.GetATKModifierForUnit != null)
        {
            int specialMod = SpecialCards.GetATKModifierForUnit(owner, unit);
            finalAtk += specialMod;
            // Note: Special card modifiers are tracked separately, not as ability activations
        }

        // ATK cannot go below 0
        finalAtk = Mathf.Max(0, finalAtk);

        return finalAtk;
    }

    /// <summary>
    /// Simplified version for UI calculations that doesn't track activation.
    /// </summary>
    public static int GetEffectiveAttackForBuyPhase(
        Unit unit,
        Player owner,
        Player enemy,
        RowType row,
        Dictionary<string, int> aliveCountByNameOnRow)
    {
        return GetEffectiveAttackForBuyPhase(unit, owner, enemy, row, aliveCountByNameOnRow, out _);
    }

    /// <summary>
    /// Counts total copies of a unit by name on the player's entire field.
    /// Used by: Unmatched (exactly 1 = bonus), BrotherInArms (2+ = bonus)
    /// </summary>
    public static int CountTotalCopiesOnField(Player player, string unitName)
    {
        if (player == null || player.Rows == null || string.IsNullOrEmpty(unitName))
            return 0;

        int count = 0;
        foreach (var row in player.Rows.Values)
        {
            foreach (var u in row.Units)
            {
                if (u != null && u.IsAlive && u.Name == unitName)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Counts enemy units with the ARMORED ability.
    /// Used by: Armor Piercing (+1 ATK per armored enemy)
    /// </summary>
    public static int CountEnemyArmoredUnits(Player enemy)
    {
        if (enemy == null || enemy.Rows == null) return 0;

        int count = 0;
        foreach (var row in enemy.Rows.Values)
        {
            foreach (var u in row.Units)
            {
                if (u != null && u.IsAlive && u.Ability == UnitAbilityType.Armored)
                    count++;
            }
        }
        return count;
    }

    /// <summary>
    /// DEPRECATED: Old helper for tank checking.
    /// Now use CountEnemyArmoredUnits for Armor Piercing ability.
    /// </summary>
    [System.Obsolete("Use CountEnemyArmoredUnits instead")]
    public static bool EnemyHasAliveTank(Player enemy)
    {
        return CountEnemyArmoredUnits(enemy) > 0;
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
    /// - Conditional ATK multipliers (ArmorPiercing, ThreeOfAKind, Unmatched)
    /// - Row-wide buffs (future: Commander ability)
    /// - Enemy-dependent modifiers (SOS below 20% HP)
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
        bool enemyHasTank,  // DEPRECATED parameter, kept for compatibility
        CombatReport report,
        bool alsoDebugLog)
    {
        int atk = unit.ATK;

        // ========================================
        // 3-OF-A-KIND: Double ATK if 3+ copies in this row
        // ========================================
        if (unit.Ability == UnitAbilityType.ThreeOfAKind)
        {
            if (aliveCountByNameOnRow.TryGetValue(unit.Name, out int count) && count >= 3)
            {
                atk *= 2;
                AddAbilityEvent(report, alsoDebugLog,
                    $"[3-OF-A-KIND] {owner.Name} {row}: {count}x '{unit.Name}' => double ATK",
                    sourcePlayer: owner, sourceUnit: unit, row: row, amount: 2);
            }
        }

        // ========================================
        // UNMATCHED: Double ATK if only one of this unit on field
        // ========================================
        if (unit.Ability == UnitAbilityType.Unmatched)
        {
            int totalCopiesOnField = CountTotalCopiesOnField(owner, unit.Name);
            if (totalCopiesOnField == 1)
            {
                atk *= 2;
                AddAbilityEvent(report, alsoDebugLog,
                    $"[UNMATCHED] {owner.Name} {row}: '{unit.Name}' is unique => double ATK",
                    sourcePlayer: owner, sourceUnit: unit, row: row, amount: 2);
            }
        }

        // ========================================
        // ARMOR PIERCING: +1 ATK per enemy ARMORED unit
        // ========================================
        if (unit.Ability == UnitAbilityType.ArmorPiercing)
        {
            int armoredCount = CountEnemyArmoredUnits(enemy);
            if (armoredCount > 0)
            {
                atk += armoredCount;
                AddAbilityEvent(report, alsoDebugLog,
                    $"[ARMOR PIERCING] {owner.Name} {row}: '{unit.Name}' +{armoredCount} ATK vs {armoredCount} armored",
                    sourcePlayer: owner, sourceUnit: unit, row: row, amount: armoredCount);
            }
        }

        // ========================================
        // SOS: Double ATK when commander HP below 20%
        // ========================================
        if (unit.Ability == UnitAbilityType.SOS && owner != null)
        {
            float hpPercent = (float)owner.CommanderHP / Mathf.Max(1, owner.CommanderMaxHP);
            if (hpPercent < 0.2f)
            {
                atk *= 2;
                AddAbilityEvent(report, alsoDebugLog,
                    $"[SOS] {owner.Name} {row}: '{unit.Name}' => double ATK (commander at {hpPercent:P0})",
                    sourcePlayer: owner, sourceUnit: unit, row: row, amount: 2);
            }
        }

        // ========================================
        // BROTHER IN ARMS: Double ATK if another copy exists
        // ========================================
        if (unit.Ability == UnitAbilityType.BrotherInArms)
        {
            int totalCopiesOnField = CountTotalCopiesOnField(owner, unit.Name);
            if (totalCopiesOnField >= 2)
            {
                atk *= 2;
                AddAbilityEvent(report, alsoDebugLog,
                    $"[BROTHER IN ARMS] {owner.Name} {row}: '{unit.Name}' has {totalCopiesOnField} copies => double ATK",
                    sourcePlayer: owner, sourceUnit: unit, row: row, amount: 2);
            }
        }

        // ========================================
        // SPECIAL CARD MODIFIERS (Barbed Wire, Trenches, etc.)
        // ========================================
        if (SpecialCards.GetATKModifierForUnit != null)
        {
            int specialMod = SpecialCards.GetATKModifierForUnit(owner, unit);
            if (specialMod != 0)
            {
                atk += specialMod;
                AddAbilityEvent(report, alsoDebugLog,
                    $"[SPECIAL CARD] {owner.Name} {row}: '{unit.Name}' ATK {(specialMod > 0 ? "+" : "")}{specialMod}",
                    sourcePlayer: owner, sourceUnit: unit, row: row, amount: specialMod);
            }
        }

        // ATK cannot go below 0
        atk = Mathf.Max(0, atk);

        return atk;
    }

    // ========================================
    // COMBAT ABILITIES: COMMANDER BYPASS
    // ========================================

    /// <summary>
    /// Calculates commander bypass damage for a unit (Navy Seal "Direct Attack" ability).
    ///
    /// DIRECT ATTACK (Feb 2026 rework):
    /// - Flat 5 damage directly to enemy commander (ignores units entirely)
    /// - Applied during combat resolution
    /// - Changed from percentage-based (25% of ATK) to flat damage
    ///
    /// RETURNS:
    /// Bypass damage amount (0 if unit doesn't have this ability, 5 if it does)
    /// </summary>
    public static int GetCommanderBypassDamage(Unit unit)
    {
        if (unit.Ability != UnitAbilityType.DirectAttack)
            return 0;

        // Flat 5 damage
        return 5;
    }

    // ========================================
    // COMBAT ABILITIES: INCOMING DAMAGE MODIFICATION
    // ========================================
    // NOTE: This section is currently handled directly in CombatResolver
    // because damage resolution is tightly coupled with the streaming algorithm.
    //
    // The following abilities are implemented in CombatResolver.ResolveIncomingDamageForUnit:
    // - Armored (ATK protection)
    // - TargetMark (destroy weakest)
    // - Counterattack (1 damage to commander on death)
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
// ABILITY CATALOG (Feb 2026 Rework)
// ========================================
// This section documents all abilities currently in the game.
// Update this list when adding new abilities!
//
// === ON-PLAY ABILITIES (Buy Phase) ===
// - MrMoneybags: Gain gold when placed
// - Duplicate: Play another copy from deck if available
// - DestroyStrongestVehicle: Destroys strongest enemy vehicle if row is full
// - TargetMark: Destroys weakest enemy unit (lowest ATK)
// - Taunt: Lowers random enemy unit ATK by 1
//
// === COMBAT ATK MODIFIERS ===
// - ThreeOfAKind: 2x ATK if 3+ copies in row
// - Unmatched: 2x ATK if only one copy on field
// - ArmorPiercing: +1 ATK per enemy ARMORED unit
// - SOS: 2x ATK when commander HP below 20%
// - BrotherInArms: 2x ATK if another copy on field
//
// === COMMANDER DAMAGE ABILITIES ===
// - DirectAttack: Flat 5 damage to commander
// - Counterattack: Deals 1 commander damage when destroyed
//
// === DEFENSIVE ABILITIES ===
// - Armored: ATK cannot be reduced by abilities
// - Persist: Survives combat if owner won or tied
//
// === FUTURE ABILITIES (Planned) ===
// - Commander: Doubles ATK of all units in row
// - Mechanic: Adds HP to commander
// - Spy: Infiltrate enemy field, gain gold while alive
//
