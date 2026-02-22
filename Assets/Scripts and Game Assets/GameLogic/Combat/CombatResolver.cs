using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// CombatResolver: The core combat simulation engine.
/// 
/// DESIGN PHILOSOPHY:
/// - Pure simulation (no rendering, no UI, no coroutines)
/// - Deterministic and event-driven
/// - Generates a CombatReport that can be replayed by CombatPlayback
/// 
/// COMBAT FLOW:
/// 1. Pre-emptive Strike Phase (simultaneous)
///    - Units with pre-emptive abilities strike first
///    - All strikes resolve simultaneously
///    - Dead units don't contribute to main combat
/// 
/// 2. Main Combat Phase
///    a) Snapshot total ATK for both sides (abilities apply here)
///    b) Calculate commander bypass damage (Navy Seal ability)
///    c) Apply main damage streams (Front → Middle → Back)
///    d) Collect counterattack damage
///    e) Apply counterattack streams (no further counters)
/// 
/// 3. Cleanup Phase
///    - Remove dead units from rows
///    - Move dead units to discard piles
/// 
/// KEY SYSTEMS:
/// - Damage Streaming: Damage flows through units like water through pipes
/// - Armor Piercing: Disables damage reduction abilities
/// - Target Marking: Marked units take 2x HP damage per stream damage
/// - Counterattack: Damaged units deal 50% damage back
/// - Taunt: Forces damage to a specific row first
/// </summary>
public static class CombatResolver
{
    // ========================================
    // CORE API
    // ========================================

    /// <summary>
    /// Main entry point: resolves combat between two players.
    /// 
    /// PARAMETERS:
    /// - a, b: The two players fighting
    /// - alsoDebugLog: If true, prints DebugNote events to console (normally false)
    /// 
    /// RETURNS:
    /// A CombatReport containing all events in order (for playback/UI)
    /// 
    /// SIDE EFFECTS:
    /// - Modifies unit HP
    /// - Modifies commander HP
    /// - Removes dead units from rows (moved to discard pile)
    /// </summary>
    public static CombatReport ResolveCombat(Player a, Player b, bool alsoDebugLog = false)
    {
        CombatReport report = new CombatReport();

        // Log combat start
        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            Note = "Combat started"
        }, alsoDebugLog);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            Note = $"{a.Name} Commander HP: {a.CommanderHP}"
        }, alsoDebugLog);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            Note = $"{b.Name} Commander HP: {b.CommanderHP}"
        }, alsoDebugLog);

        // Announce existing target marks (set during buy phase)
        EmitTargetMarkedEvents(a, report, alsoDebugLog);
        EmitTargetMarkedEvents(b, report, alsoDebugLog);

        // Store starting unit counts BEFORE any combat damage
        // Used by playback to know when a row is completely wiped
        var countsA = CountUnitsByType(a);
        var countsB = CountUnitsByType(b);
        report.SetStartingCounts(countsA, countsB);

        // PHASE 1: Pre-emptive strikes (simultaneous)
        ResolvePreemptivePhase(a, b, report, alsoDebugLog);

        // PHASE 2: Main combat
        // Snapshot attack totals AFTER pre-emptive kills
       var atkBucketsA = CalculateAttackBuckets(owner: a, enemy: b, report: report, alsoDebugLog: alsoDebugLog);
var atkBucketsB = CalculateAttackBuckets(owner: b, enemy: a, report: report, alsoDebugLog: alsoDebugLog);

int totalAtkA =
    atkBucketsA[UnitType.Infantry] +
    atkBucketsA[UnitType.Vehicle] +
    atkBucketsA[UnitType.Air] +
    atkBucketsA[UnitType.Ranged];

int totalAtkB =
    atkBucketsB[UnitType.Infantry] +
    atkBucketsB[UnitType.Vehicle] +
    atkBucketsB[UnitType.Air] +
    atkBucketsB[UnitType.Ranged];

        // Store firepower totals in report for visual playback ordering
        // (Lower firepower player's units explode first per row)
        report.SetFirepower(totalAtkA, totalAtkB);

        // Navy Seal bypass damage (goes directly to commander)
        ApplyBypassDamage(a, b, report, alsoDebugLog);
        ApplyBypassDamage(b, a, report, alsoDebugLog);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            Note = $"{a.Name} total ATK this combat: {totalAtkA}",
            SourcePlayer = a,
            Amount = totalAtkA
        }, alsoDebugLog);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            Note = $"{b.Name} total ATK this combat: {totalAtkB}",
            SourcePlayer = b,
            Amount = totalAtkB
        }, alsoDebugLog);

        // Check for armor piercing (disables damage reduction)
        bool aHasArmorPiercing = HasAnyAliveAbility(a, UnitAbilityType.ArmorPiercing);
        bool bHasArmorPiercing = HasAnyAliveAbility(b, UnitAbilityType.ArmorPiercing);

        // Apply main damage streams (returns counter damage and leftover damage)
        // leftoverToB = damage that overflowed past all of B's units (from A's attack)
        // leftoverToA = damage that overflowed past all of A's units (from B's attack)
        var (counterBackToA, leftoverToB) = ApplyDamageBucketsToEntireArmy(
            attacker: a,
            defender: b,
            buckets: atkBucketsA,
            report: report,
            alsoDebugLog: alsoDebugLog,
            attackerHasArmorPiercing: aHasArmorPiercing,
            allowCounterattack: true
        );

        var (counterBackToB, leftoverToA) = ApplyDamageBucketsToEntireArmy(
            attacker: b,
            defender: a,
            buckets: atkBucketsB,
            report: report,
            alsoDebugLog: alsoDebugLog,
            attackerHasArmorPiercing: bHasArmorPiercing,
            allowCounterattack: true
        );

        // Apply counterattack streams (no further counters to prevent infinite loops)
        // Counter damage can also overflow to commander
        if (counterBackToA > 0)
        {
            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.CounterattackTriggered,
                SourcePlayer = b,
                TargetPlayer = a,
                Amount = counterBackToA,
                Note = $"[COUNTER] Total counter damage back to {a.Name}: {counterBackToA}"
            }, alsoDebugLog);

            var (_, counterLeftoverToA) = ApplyDamageToEntireArmy(
                attacker: b,
                defender: a,
                damage: counterBackToA,
                report: report,
                alsoDebugLog: alsoDebugLog,
                attackerHasArmorPiercing: false,
                allowCounterattack: false, // Prevent counter-counter loops
                isCounterDamage: true  // Flag for counterattack visual effects
            );
            leftoverToA += counterLeftoverToA;
        }

        if (counterBackToB > 0)
        {
            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.CounterattackTriggered,
                SourcePlayer = a,
                TargetPlayer = b,
                Amount = counterBackToB,
                Note = $"[COUNTER] Total counter damage back to {b.Name}: {counterBackToB}"
            }, alsoDebugLog);

            var (_, counterLeftoverToB) = ApplyDamageToEntireArmy(
                attacker: a,
                defender: b,
                damage: counterBackToB,
                report: report,
                alsoDebugLog: alsoDebugLog,
                attackerHasArmorPiercing: false,
                allowCounterattack: false, // Prevent counter-counter loops
                isCounterDamage: true  // Flag for counterattack visual effects
            );
            leftoverToB += counterLeftoverToB;
        }

        // PHASE 3: Cleanup dead units
        CleanupDeadUnits(a, report, alsoDebugLog);
        CleanupDeadUnits(b, report, alsoDebugLog);

        // UNIFIED HEALTH SYSTEM: Recalculate max HP based on surviving units
        // This shrinks the health bar to reflect only living units
        a.RecalculateCommanderMaxHP();
        b.RecalculateCommanderMaxHP();

        // PHASE 4: Firepower Resolution
        // Commander damage is based on leftover damage that overflowed past all enemy units:
        // - leftoverToB = damage A dealt that overflowed past all of B's units
        // - leftoverToA = damage B dealt that overflowed past all of A's units
        // - Only the player with HIGHER leftover deals commander damage
        // - Commander damage = Winner's leftover - Loser's leftover
        ResolveFirepowerDamage(a, b, leftoverToB, leftoverToA, report, alsoDebugLog);

        // Log combat end
        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.CombatEnd,
            Note = "Combat ended"
        }, alsoDebugLog);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.CombatEnd,
            Note = $"{a.Name} Commander HP: {a.CommanderHP}"
        }, alsoDebugLog);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.CombatEnd,
            Note = $"{b.Name} Commander HP: {b.CommanderHP}"
        }, alsoDebugLog);

        return report;
    }

    // ========================================
    // PHASE 1: PRE-EMPTIVE STRIKES
    // ========================================

    /// <summary>
    /// Container for pre-emptive strike data before resolution.
    /// </summary>
    private class PendingStrike
    {
        public Player AttackerOwner;
        public Unit Attacker;
        public Player DefenderOwner;
        public Unit Target;
        public int Damage;
        public RowType? TargetRow;
    }

    /// <summary>
    /// Resolves all pre-emptive strikes simultaneously.
    /// 
    /// RULES:
    /// - All strikes are declared first
    /// - Then all damage is applied simultaneously
    /// - Units can be hit by multiple strikes at once
    /// - Dead units are cleaned up immediately (won't contribute to main combat)
    /// </summary>
    private static void ResolvePreemptivePhase(Player a, Player b, CombatReport report, bool alsoDebugLog)
    {
        List<PendingStrike> strikes = new List<PendingStrike>();

        CollectStrikes(a, b, strikes);
        CollectStrikes(b, a, strikes);

        if (strikes.Count == 0)
        {
            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.PreemptiveStrikeDeclared,
                Note = "No pre-emptive strikes"
            }, alsoDebugLog);
            return;
        }

        // Declare all strikes (for UI/logging)
        foreach (var s in strikes)
        {
            if (s.Target == null || !s.Target.IsAlive) continue;

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.PreemptiveStrikeDeclared,
                SourcePlayer = s.AttackerOwner,
                TargetPlayer = s.DefenderOwner,
                SourceUnit = s.Attacker,
                TargetUnit = s.Target,
                Amount = s.Damage,
                Row = s.TargetRow,
                Note = $"{s.AttackerOwner.Name}'s {s.Attacker.Name} pre-strikes {s.DefenderOwner.Name}'s {s.Target.Name} for {s.Damage}"
            }, alsoDebugLog);
        }

        // Accumulate damage per target (simultaneous resolution)
        Dictionary<Unit, int> dmgByTarget = new Dictionary<Unit, int>();
        Dictionary<Unit, Player> ownerByTarget = new Dictionary<Unit, Player>();
        Dictionary<Unit, RowType?> rowByTarget = new Dictionary<Unit, RowType?>();
        Dictionary<Unit, Unit> sourceByTarget = new Dictionary<Unit, Unit>();  // Track first attacker per target

        foreach (var s in strikes)
        {
            if (s.Target == null || !s.Target.IsAlive) continue;

            if (!dmgByTarget.ContainsKey(s.Target))
            {
                dmgByTarget[s.Target] = 0;
                ownerByTarget[s.Target] = s.DefenderOwner;
                rowByTarget[s.Target] = s.TargetRow;
                sourceByTarget[s.Target] = s.Attacker;  // Remember first attacker for visual effects
            }

            dmgByTarget[s.Target] += s.Damage;
        }

        // Apply accumulated damage to each target
        foreach (var kvp in dmgByTarget)
        {
            Unit target = kvp.Key;
            int incoming = kvp.Value;

            if (!target.IsAlive) continue;

            Player targetOwner = ownerByTarget[target];
            RowType? targetRow = rowByTarget[target];

            int beforeHP = target.HP;

            // Pre-emptive strikes use the same damage resolution system
            // (respects armor, but no armor piercing in pre-emptive phase currently)
            DamageResult dr = ResolveIncomingDamageForUnit(
                targetUnit: target,
                streamIncoming: incoming,
                report: report,
                alsoDebugLog: alsoDebugLog,
                targetOwner: targetOwner,
                row: targetRow,
                attackerHasArmorPiercing: false
            );

            target.HP = Mathf.Max(0, target.HP - dr.AppliedToHP);
            int afterHP = target.HP;

            // UNIFIED HEALTH SYSTEM: Sync damage to commander HP
            if (dr.AppliedToHP > 0)
            {
                targetOwner.OnUnitDamaged(dr.AppliedToHP);
            }

            // Get the source unit for attack effects (first attacker against this target)
            Unit sourceUnit = sourceByTarget.ContainsKey(target) ? sourceByTarget[target] : null;

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DamageAppliedToUnit,
                TargetPlayer = targetOwner,
                SourceUnit = sourceUnit,  // Add source unit for attack effects
                TargetUnit = target,
                Row = targetRow,
                Amount = dr.AppliedToHP,
                Note = $"{targetOwner.Name}'s {target.Name} takes total pre-emptive {dr.AppliedToHP} ({targetRow}), HP {beforeHP}->{afterHP} [PRE-EMPTIVE]"
            }, alsoDebugLog);

            if (target.HP <= 0)
            {
                AddEvent(report, new CombatEvent
                {
                    Type = CombatEventType.UnitDestroyed,
                    TargetPlayer = targetOwner,
                    TargetUnit = target,
                    Row = targetRow,
                    Note = $"{targetOwner.Name}'s {target.Name} destroyed in pre-emptive phase ({targetRow})"
                }, alsoDebugLog);

                // SALVAGE WRECKAGE: Award gold when destroying enemy vehicles in preemptive phase
                // Find the attacker owner from our pending strikes
                Player attackerOwner = null;
                foreach (var s in strikes)
                {
                    if (s.Target == target)
                    {
                        attackerOwner = s.AttackerOwner;
                        break;
                    }
                }
                if (target.Type == UnitType.Vehicle && attackerOwner != null && DoctrineSystem.Instance != null)
                {
                    DoctrineSystem.Instance.OnVehicleDestroyed(attackerOwner, target);
                }
            }
        }

        // Cleanup immediately so destroyed units don't contribute to main ATK
        CleanupDeadUnits(a, report, alsoDebugLog);
        CleanupDeadUnits(b, report, alsoDebugLog);
    }

    /// <summary>
    /// Collects all pre-emptive strikes from one player against another.
    /// </summary>
    private static void CollectStrikes(Player attackerOwner, Player defenderOwner, List<PendingStrike> strikes)
    {
        foreach (var rowKvp in attackerOwner.Rows)
        {
            Row row = rowKvp.Value;

            foreach (var attacker in row.Units)
            {
                if (!attacker.IsAlive) continue;
                if (attacker.PreemptiveStrike == null) continue;

                (Unit target, RowType? targetRow) = SelectPreemptiveTarget(defenderOwner, attacker.PreemptiveStrike);

                if (target == null) continue;

                strikes.Add(new PendingStrike
                {
                    AttackerOwner = attackerOwner,
                    Attacker = attacker,
                    DefenderOwner = defenderOwner,
                    Target = target,
                    Damage = attacker.PreemptiveStrike.Damage,
                    TargetRow = targetRow
                });
            }
        }
    }

    /// <summary>
    /// Finds a valid target for a pre-emptive strike.
    /// 
    /// TARGETING RULES:
    /// - If TargetBackRowOnly is true, only search back row
    /// - Otherwise, search Front → Middle → Back (first match wins)
    /// - Only targets units of matching UnitType
    /// </summary>
    private static (Unit, RowType?) SelectPreemptiveTarget(Player defender, PreemptiveStrikeData data)
    {
        if (data.TargetBackRowOnly)
        {
            Unit t = FindFirstUnitOfType(defender.Rows[RowType.Back], data.TargetType);
            return (t, t != null ? RowType.Back : (RowType?)null);
        }

        Unit x;

        x = FindFirstUnitOfType(defender.Rows[RowType.Front], data.TargetType);
        if (x != null) return (x, RowType.Front);

        x = FindFirstUnitOfType(defender.Rows[RowType.Middle], data.TargetType);
        if (x != null) return (x, RowType.Middle);

        x = FindFirstUnitOfType(defender.Rows[RowType.Back], data.TargetType);
        if (x != null) return (x, RowType.Back);

        return (null, null);
    }

    /// <summary>
    /// Finds the first alive unit of a specific type in a row.
    /// </summary>
    private static Unit FindFirstUnitOfType(Row row, UnitType type)
    {
        foreach (var u in row.Units)
        {
            if (u.IsAlive && u.Type == type)
                return u;
        }
        return null;
    }
// ========================================
// PHASE 2: MAIN COMBAT - ATTACK BUCKETS (BY UNIT TYPE)
// ========================================

/// <summary>
/// Returns total attack buckets by UnitType (after abilities + commander multipliers).
/// </summary>
private static Dictionary<UnitType, int> CalculateAttackBuckets(Player owner, Player enemy, CombatReport report, bool alsoDebugLog)
{
    var buckets = new Dictionary<UnitType, int>
    {
        { UnitType.Infantry, 0 },
        { UnitType.Vehicle, 0 },
        { UnitType.Air, 0 },
        { UnitType.Ranged, 0 }
    };

    AddRowBuckets(owner, enemy, owner.Rows[RowType.Front], buckets, report, alsoDebugLog);
    AddRowBuckets(owner, enemy, owner.Rows[RowType.Middle], buckets, report, alsoDebugLog);
    AddRowBuckets(owner, enemy, owner.Rows[RowType.Back], buckets, report, alsoDebugLog);

    return buckets;
}

private static void AddRowBuckets(
    Player owner,
    Player enemy,
    Row row,
    Dictionary<UnitType, int> buckets,
    CombatReport report,
    bool alsoDebugLog)
{
    // Count alive units by name (for 3-of-a-kind synergy)
    Dictionary<string, int> aliveCountByName = new Dictionary<string, int>();
    foreach (var u in row.Units)
    {
        if (!u.IsAlive) continue;

        if (!aliveCountByName.ContainsKey(u.Name))
            aliveCountByName[u.Name] = 0;

        aliveCountByName[u.Name]++;
    }

    bool enemyHasTank = EnemyHasAliveTank(enemy);

    foreach (var u in row.Units)
    {
        if (!u.IsAlive) continue;

        int atk = AbilitySystem.GetAttackForSnapshot(
            unit: u,
            owner: owner,
            enemy: enemy,
            row: row.RowType,
            aliveCountByNameOnRow: aliveCountByName,
            enemyHasTank: enemyHasTank,
            report: report,
            alsoDebugLog: alsoDebugLog
        );

        // Commander snapshot multiplier (Terra/Aqua etc.)
        float cmdMult = (CommanderBonus.GetAttackMultiplier != null)
            ? CommanderBonus.GetAttackMultiplier(owner, u)
            : 1f;

        if (!Mathf.Approximately(cmdMult, 1f))
            atk = Mathf.RoundToInt(atk * cmdMult);

        // Perk ATK bonuses (Stopping Power, Piercing, Explosives, Heads Up)
        int perkBonus = (PerkSystem.GetTypeAtkBonus != null)
            ? PerkSystem.GetTypeAtkBonus(owner, u.Type)
            : 0;
        if (perkBonus > 0)
        {
            atk += perkBonus;
            if (alsoDebugLog)
                Debug.Log($"[PERK] {u.Name} +{perkBonus} ATK from perk (now {atk})");
        }

        // Doctrine ATK bonuses (Close Combat, Ranged Warrior, Iron Skin, Death from Above)
        int doctrineBonus = (DoctrineSystem.GetTypeAtkBonus != null)
            ? DoctrineSystem.GetTypeAtkBonus(owner, u.Type)
            : 0;
        if (doctrineBonus > 0)
        {
            atk += doctrineBonus;
            if (alsoDebugLog)
                Debug.Log($"[DOCTRINE] {u.Name} +{doctrineBonus} ATK from doctrine (now {atk})");
        }

        // Combined Arms doctrine bonus (vehicles +ATK when infantry present)
        if (DoctrineSystem.Instance != null)
        {
            int combinedArmsBonus = DoctrineSystem.Instance.GetCombinedArmsBonus(owner, u.Type);
            if (combinedArmsBonus > 0)
            {
                atk += combinedArmsBonus;
                if (alsoDebugLog)
                    Debug.Log($"[DOCTRINE] {u.Name} +{combinedArmsBonus} ATK from Combined Arms (now {atk})");
            }
        }

        buckets[u.Type] += atk;
    }
}

/// <summary>
/// Applies multiple damage streams (one per UnitType), allowing defender commander to modify damage taken
/// by attacker type (Geralt vs Ranged).
/// Returns tuple of (total counter-stream damage, total leftover damage that overflowed).
/// </summary>
private static (int counterTotal, int leftoverTotal) ApplyDamageBucketsToEntireArmy(
    Player attacker,
    Player defender,
    Dictionary<UnitType, int> buckets,
    CombatReport report,
    bool alsoDebugLog,
    bool attackerHasArmorPiercing,
    bool allowCounterattack,
    bool isCounterDamage = false)
{
    int counterTotal = 0;
    int leftoverTotal = 0;

    // Deterministic order
    UnitType[] order = { UnitType.Infantry, UnitType.Vehicle, UnitType.Air, UnitType.Ranged };

    foreach (var t in order)
    {
        if (!buckets.TryGetValue(t, out int dmg) || dmg <= 0)
            continue;

        // Defender commander damage-taken modifier vs this attacker type (Geralt etc.)
        float takenMult = (CommanderBonus.GetDamageTakenMultiplierAgainstAttackerType != null)
            ? CommanderBonus.GetDamageTakenMultiplierAgainstAttackerType(defender, t)
            : 1f;

        int adjusted = Mathf.RoundToInt(dmg * takenMult);
        if (report != null && !Mathf.Approximately(takenMult, 1f))
        {
            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DebugNote,
                SourcePlayer = attacker,
                TargetPlayer = defender,
                Amount = adjusted,
                Note = $"{defender.Name} commander reduced {t} damage: {dmg} -> {adjusted} (x{takenMult:0.##})"
            }, alsoDebugLog);
        }

        if (alsoDebugLog)
            Debug.Log($"[TYPE STREAM] {attacker.Name}->{defender.Name} type={t} base={dmg} mult={takenMult:0.###} adjusted={adjusted}");

        var (counter, leftover) = ApplyDamageToEntireArmy(
            attacker: attacker,
            defender: defender,
            damage: adjusted,
            report: report,
            alsoDebugLog: alsoDebugLog,
            attackerHasArmorPiercing: attackerHasArmorPiercing,
            allowCounterattack: allowCounterattack,
            damageBucketType: t,
            isCounterDamage: isCounterDamage
        );

        counterTotal += counter;
        leftoverTotal += leftover;
    }

    return (counterTotal, leftoverTotal);
}

    // ========================================
    // PHASE 2: MAIN COMBAT - ATTACK SNAPSHOT
    // ========================================

    /// <summary>
    /// Calculates the total attack for one player.
    /// Sums all three rows, applying ability modifiers.
    /// </summary>
    /// 
    private static int CalculateTotalAttack(Player owner, Player enemy, CombatReport report, bool alsoDebugLog)
    {
        int total = 0;
        total += CalculateRowAttackWithAbilities(owner, enemy, owner.Rows[RowType.Front], report, alsoDebugLog);
        total += CalculateRowAttackWithAbilities(owner, enemy, owner.Rows[RowType.Middle], report, alsoDebugLog);
        total += CalculateRowAttackWithAbilities(owner, enemy, owner.Rows[RowType.Back], report, alsoDebugLog);
        return total;
    }

    /// <summary>
    /// Calculates attack for one row with ability modifiers.
    /// 
    /// ABILITY CHECKS:
    /// - 3-of-a-kind synergy (per row)
    /// - Full health double ATK
    /// - RPG double ATK vs tanks
    /// - (others handled by AbilitySystem)
    /// </summary>
    private static int CalculateRowAttackWithAbilities(Player owner, Player enemy, Row row, CombatReport report, bool alsoDebugLog)
    {
        // Count alive units by name (for 3-of-a-kind synergy)
        Dictionary<string, int> aliveCountByName = new Dictionary<string, int>();
        foreach (var u in row.Units)
        {
            if (!u.IsAlive) continue;

            if (!aliveCountByName.ContainsKey(u.Name))
                aliveCountByName[u.Name] = 0;

            aliveCountByName[u.Name]++;
        }

        bool enemyHasTank = EnemyHasAliveTank(enemy);

        int total = 0;

        foreach (var u in row.Units)
        {
            if (!u.IsAlive) continue;

            // Delegate to AbilitySystem for attack calculation
            int atk = AbilitySystem.GetAttackForSnapshot(
    unit: u,
    owner: owner,
    enemy: enemy,
    row: row.RowType,
    aliveCountByNameOnRow: aliveCountByName,
    enemyHasTank: enemyHasTank,
    report: report,
    alsoDebugLog: alsoDebugLog
);

// Commander snapshot multiplier (Terra/Aqua etc.)
float cmdMult = (CommanderBonus.GetAttackMultiplier != null)
    ? CommanderBonus.GetAttackMultiplier(owner, u)
    : 1f;

if (!Mathf.Approximately(cmdMult, 1f))
{
    int before = atk;
    atk = Mathf.RoundToInt(atk * cmdMult);

    if (alsoDebugLog)
        Debug.Log($"[CMD SNAPSHOT] {owner.Name} mult {cmdMult:0.###} on {u.Name}: {before}->{atk}");
}

total += atk;

        }

        return total;
    }

    /// <summary>
    /// Checks if the enemy has any alive tanks (vehicles).
    /// Used by RPG ability (DoubleAttackIfEnemyHasTank).
    /// </summary>
    private static bool EnemyHasAliveTank(Player enemy)
    {
        foreach (var row in enemy.Rows.Values)
        {
            foreach (var u in row.Units)
            {
                if (!u.IsAlive) continue;

                // NOTE: Treating all vehicles as "tanks" for now
                // If you add non-tank vehicles later, add a Tag/Category field
                if (u.Type == UnitType.Vehicle)
                    return true;
            }
        }
        return false;
    }

    // ========================================
    // PHASE 2: COMMANDER BYPASS DAMAGE
    // ========================================

    /// <summary>
    /// Applies commander bypass damage (Navy Seal ability).
    /// This damage goes directly to the enemy commander, ignoring units.
    /// </summary>
    private static void ApplyBypassDamage(Player attacker, Player defender, CombatReport report, bool alsoDebugLog)
    {
        int bypassDamage = CalculateCommanderBypassDamage(attacker, report, alsoDebugLog);

        if (bypassDamage > 0)
        {
            int before = defender.CommanderHP;
            defender.CommanderHP = Mathf.Max(0, defender.CommanderHP - bypassDamage);

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.CommanderDamaged,
                SourcePlayer = attacker,
                TargetPlayer = defender,
                Amount = bypassDamage,
                Note = $"[ABILITY] {attacker.Name} bypass deals {bypassDamage} to {defender.Name} commander (HP {before}->{defender.CommanderHP})"
            }, alsoDebugLog);
        }
    }

    /// <summary>
    /// Calculates total commander bypass damage from all Navy Seal units.
    /// </summary>
    private static int CalculateCommanderBypassDamage(Player player, CombatReport report, bool alsoDebugLog)
    {
        int total = 0;

        foreach (var row in player.Rows.Values)
        {
            foreach (var u in row.Units)
            {
                if (!u.IsAlive) continue;

                int bonus = AbilitySystem.GetCommanderBypassDamage(u);

                if (bonus > 0)
                {
                    total += bonus;

                    AddEvent(report, new CombatEvent
                    {
                        Type = CombatEventType.SynergyActivated,
                        SourcePlayer = player,
                        SourceUnit = u,
                        Row = row.RowType,
                        Amount = bonus,
                        Note = $"[ABILITY] {player.Name} {row.RowType}: '{u.Name}' adds {bonus} bypass commander damage"
                    }, alsoDebugLog);
                }
            }
        }

        return total;
    }

    // ========================================
    // PHASE 2: DAMAGE APPLICATION (STREAMING)
    // ========================================

    /// <summary>
    /// Applies damage to an entire army using the "streaming damage" model.
    ///
    /// DAMAGE STREAMING:
    /// - Damage flows through rows in order: Front → Middle → Back
    /// - Each unit "absorbs" some damage from the stream
    /// - Leftover damage spills to the next unit
    /// - Any remaining damage is tracked for Firepower resolution
    ///
    /// TAUNT:
    /// - If defender has a TauntRowFirst unit, that row is attacked first
    ///
    /// RETURNS:
    /// Tuple of (counterattack damage, leftover damage that overflowed past all units)
    /// </summary>
    private static (int counterDamage, int leftoverDamage) ApplyDamageToEntireArmy(
        Player attacker,
        Player defender,
        int damage,
        CombatReport report,
        bool alsoDebugLog,
        bool attackerHasArmorPiercing,
        bool allowCounterattack,
        UnitType? damageBucketType = null,
        bool isCounterDamage = false)
    {
        if (damage <= 0)
            return (0, 0);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            TargetPlayer = defender,
            Amount = damage,
            Note = $"Applying {damage} damage to {defender.Name}'s army"
        }, alsoDebugLog);

        // Default row order: Front → Middle → Back
        List<RowType> rowOrder = new List<RowType> { RowType.Front, RowType.Middle, RowType.Back };

        // TAUNT: If defender has TauntRowFirst, that row is attacked first
        if (TryGetTauntRowFirst(defender, out RowType tauntRow))
        {
            rowOrder.Remove(tauntRow);
            rowOrder.Insert(0, tauntRow);

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.AbilityTriggered,
                TargetPlayer = defender,
                Row = tauntRow,
                Note = $"[TAUNT] {defender.Name}'s {tauntRow} row is attacked first"
            }, alsoDebugLog);
        }

        int counterStreamBackToAttacker = 0;

        // Apply damage to each row in order
        foreach (RowType r in rowOrder)
        {
            if (damage <= 0) break;

            Row row = defender.Rows[r];

            damage = ApplyDamageToRow(
                attacker: attacker,
                defender: defender,
                row: row,
                incomingDamage: damage,
                report: report,
                alsoDebugLog: alsoDebugLog,
                attackerHasArmorPiercing: attackerHasArmorPiercing,
                allowCounterattack: allowCounterattack,
                counterStreamBackToAttacker: ref counterStreamBackToAttacker,
                damageBucketType: damageBucketType,
                isCounterDamage: isCounterDamage
            );
        }

        // Track leftover damage for Firepower resolution
        // This damage overflowed past all units and will be used to calculate commander damage
        if (damage > 0)
        {
            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DebugNote,
                TargetPlayer = defender,
                Amount = damage,
                Note = $"[FIREPOWER] {damage} leftover stream damage tracked for Firepower resolution"
            }, alsoDebugLog);
        }

        return (counterStreamBackToAttacker, damage);
    }

    /// <summary>
    /// Applies streaming damage to a single row.
    /// 
    /// FLOW:
    /// - Damage flows through units left-to-right
    /// - Each unit absorbs damage and takes HP loss
    /// - Counterattack damage is accumulated (if enabled)
    /// 
    /// RETURNS:
    /// Remaining damage (spills to next row)
    /// </summary>
    private static int ApplyDamageToRow(
        Player attacker,
        Player defender,
        Row row,
        int incomingDamage,
        CombatReport report,
        bool alsoDebugLog,
        bool attackerHasArmorPiercing,
        bool allowCounterattack,
        ref int counterStreamBackToAttacker,
        UnitType? damageBucketType = null,
        bool isCounterDamage = false)
    {
        int damage = incomingDamage;

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            TargetPlayer = defender,
            Amount = damage,
            Row = row.RowType,
            Note = $"[ROW START] {defender.Name} {row.RowType}: incoming damage={damage}, units={row.Units.Count}"
        }, alsoDebugLog);

        for (int i = 0; i < row.Units.Count; i++)
        {
            if (damage <= 0) break;

            Unit unit = row.Units[i];
            if (!unit.IsAlive) continue;

            // Resolve how this unit processes the damage stream
            DamageResult dr = ResolveIncomingDamageForUnit(
                targetUnit: unit,
                streamIncoming: damage,
                report: report,
                alsoDebugLog: alsoDebugLog,
                targetOwner: defender,
                row: row.RowType,
                attackerHasArmorPiercing: attackerHasArmorPiercing
            );

            int beforeHP = unit.HP;
            unit.HP = Mathf.Max(0, unit.HP - dr.AppliedToHP);
            int afterHP = unit.HP;

            // UNIFIED HEALTH SYSTEM: Sync damage to commander HP
            if (dr.AppliedToHP > 0)
            {
                defender.OnUnitDamaged(dr.AppliedToHP);
            }

            // Reduce stream by absorbed amount
            damage -= dr.AbsorbedFromStream;

            // Find a representative attacking unit for visual effects
            // Pick the first alive unit from any attacking row
            Unit sourceUnit = null;
            foreach (var rowKvp in attacker.Rows)
            {
                var attackingRow = rowKvp.Value;
                if (attackingRow != null && attackingRow.Units != null)
                {
                    foreach (var u in attackingRow.Units)
                    {
                        if (u != null && u.IsAlive)
                        {
                            sourceUnit = u;
                            break;
                        }
                    }
                    if (sourceUnit != null) break;
                }
            }

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DamageAppliedToUnit,
                SourcePlayer = attacker,
                TargetPlayer = defender,
                SourceUnit = sourceUnit,  // Add representative source unit for attack effects
                TargetUnit = unit,
                Amount = dr.AppliedToHP,
                Row = row.RowType,
                IsCounterDamage = isCounterDamage,  // Flag for counterattack visual effects
                Note = $"{defender.Name}'s {unit.Name} takes {dr.AppliedToHP} damage ({row.RowType}), HP {beforeHP}->{afterHP} (absorbed {dr.AbsorbedFromStream}, remaining stream={damage})"
                       + (unit.IsMarkedTarget ? " [MARKED]" : "")  // Add MARKED indicator for playback
                       + (isCounterDamage ? " [COUNTER]" : "")  // Add COUNTER indicator for playback
            }, alsoDebugLog);

            // COUNTERATTACK: Damaged but not destroyed units deal 50% back
            if (allowCounterattack &&
                unit.IsAlive &&
                unit.Ability == UnitAbilityType.Counterattack &&
                dr.AppliedToHP > 0)
            {
                int counter = Mathf.FloorToInt(dr.AppliedToHP * 0.5f);
                if (counter > 0)
                {
                    counterStreamBackToAttacker += counter;

                    AddEvent(report, new CombatEvent
                    {
                        Type = CombatEventType.AbilityTriggered,
                        SourcePlayer = defender,
                        TargetPlayer = attacker,
                        SourceUnit = unit,
                        Amount = counter,
                        Row = row.RowType,
                        Note = $"[COUNTER] {defender.Name}'s {unit.Name} counterattacks for {counter} (50% of {dr.AppliedToHP})"
                    }, alsoDebugLog);
                }
            }

            // Check for unit death
            if (unit.HP <= 0)
            {
                // Clear target mark if this unit was marked
                if (unit.IsMarkedTarget)
                    unit.IsMarkedTarget = false;

                AddEvent(report, new CombatEvent
                {
                    Type = CombatEventType.UnitDestroyed,
                    TargetPlayer = defender,
                    TargetUnit = unit,
                    Row = row.RowType,
                    Note = $"{defender.Name}'s {unit.Name} destroyed ({row.RowType})"
                }, alsoDebugLog);

                // SALVAGE WRECKAGE: Award gold when destroying enemy vehicles
                if (unit.Type == UnitType.Vehicle && DoctrineSystem.Instance != null)
                {
                    DoctrineSystem.Instance.OnVehicleDestroyed(attacker, unit);
                }
            }
        }

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            TargetPlayer = defender,
            Amount = damage,
            Row = row.RowType,
            DamageBucket = damageBucketType,
            Note = $"[ROW END] {defender.Name} {row.RowType} (bucket={damageBucketType}): remaining stream damage={damage}"
        }, alsoDebugLog);

        return damage;
    }

    // ========================================
    // DAMAGE RESOLUTION (CORE ALGORITHM)
    // ========================================

    /// <summary>
    /// Represents how damage is processed by a unit.
    /// </summary>
    private struct DamageResult
    {
        public int AbsorbedFromStream; // How much to subtract from the damage stream
        public int AppliedToHP;        // How much HP the unit actually loses
    }

    /// <summary>
    /// CORE DAMAGE ALGORITHM: Resolves how a unit processes incoming stream damage.
    /// 
    /// This is the heart of the combat system. It handles:
    /// - Normal damage (1:1 stream to HP)
    /// - Target Mark (2x HP damage per stream damage)
    /// - Armored Tank (reduces damage OR gets overwhelmed)
    /// - Armor Piercing (disables armor reduction)
    /// 
    /// RETURNS:
    /// DamageResult with two values:
    /// - AbsorbedFromStream: How much the attack stream is reduced
    /// - AppliedToHP: How much HP the unit actually loses
    /// 
    /// KEY DESIGN:
    /// - Target Mark DOUBLES HP damage but does NOT spend extra stream
    /// - Armor Piercing completely disables armor reduction (no partial effect)
    /// - Tank overwhelm is binary: either reduced damage OR instant death
    /// </summary>
    /// <summary>
    /// SIMPLIFIED DAMAGE RESOLUTION
    ///
    /// Previous complex behaviors have been replaced with visible HP changes:
    /// - ARMORED (ReduceIncomingDamage50Percent): HP is doubled at unit creation
    /// - MARKED (IsMarkedTarget): HP is halved when marked during buy phase
    ///
    /// Now all units take damage using simple 1:1 stream-to-HP ratio.
    /// The only special handling remaining is:
    /// - EMP: Disables vehicle abilities
    /// - TRENCHES: Special card that protects infantry
    /// </summary>
    private static DamageResult ResolveIncomingDamageForUnit(
        Unit targetUnit,
        int streamIncoming,
        CombatReport report,
        bool alsoDebugLog,
        Player targetOwner,
        RowType? row,
        bool attackerHasArmorPiercing)
    {
        int hp = targetUnit.HP;

        // ========================================
        // SPECIAL EFFECT: EMP disables VEHICLE abilities this round
        // ========================================
        bool empActive =
            (SpecialCards.IsEmpActiveOnPlayerThisRound != null) &&
            SpecialCards.IsEmpActiveOnPlayerThisRound(targetOwner);

        if (empActive && targetUnit.Type == UnitType.Vehicle)
        {
            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.AbilityTriggered,
                TargetPlayer = targetOwner,
                TargetUnit = targetUnit,
                Row = row,
                Note = $"[EMP] '{targetUnit.Name}' vehicle ability disabled this round"
            }, alsoDebugLog);
        }

        // DEFAULT BEHAVIOR (all units including Armored and Marked):
        // - Unit absorbs up to its remaining HP from the stream
        // - Unit takes that same amount to HP (1:1 ratio)
        //
        // NOTE: Armored units already have 2x HP, Marked units already have 0.5x HP
        // so no special damage math is needed here anymore.
        int absorbed = Mathf.Min(streamIncoming, hp);
        int applied = absorbed;

        // ========================================
        // SPECIAL EFFECT: TRENCHES (Infantry take half HP damage)
        // This is a special card effect, not a unit ability
        // ========================================
        bool trenchesActive =
            (SpecialCards.HasTrenchesThisBattle != null) &&
            SpecialCards.HasTrenchesThisBattle(targetOwner);

        if (trenchesActive && targetUnit.Type == UnitType.Infantry)
        {
            // Trenches: Infantry absorbs all stream but takes half HP damage
            int threshold = hp * 2;

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.AbilityTriggered,
                TargetPlayer = targetOwner,
                TargetUnit = targetUnit,
                Row = row,
                Note = $"[TRENCHES] '{targetUnit.Name}' infantry protected"
            }, alsoDebugLog);

            if (streamIncoming >= threshold)
            {
                // OVERWHELMED: unit dies but absorbs 2x HP from the stream
                return new DamageResult
                {
                    AbsorbedFromStream = Mathf.Min(streamIncoming, threshold),
                    AppliedToHP = hp
                };
            }
            else
            {
                // PROTECTED: absorbs all stream but takes half HP damage
                int trenchAbsorbed = streamIncoming;
                int trenchApplied = Mathf.RoundToInt(streamIncoming * 0.5f);
                trenchApplied = Mathf.Min(trenchApplied, hp);

                return new DamageResult
                {
                    AbsorbedFromStream = trenchAbsorbed,
                    AppliedToHP = trenchApplied
                };
            }
        }

        // Return default damage result (simple 1:1 ratio for all units)
        return new DamageResult
        {
            AbsorbedFromStream = absorbed,
            AppliedToHP = applied
        };
    }

    // ========================================
    // PHASE 3: CLEANUP
    // ========================================

    /// <summary>
    /// Removes dead units from rows and moves them to discard pile.
    /// </summary>
    private static void CleanupDeadUnits(Player player, CombatReport report, bool alsoDebugLog)
    {
        int moved = 0;

        foreach (var rowKvp in player.Rows)
        {
            Row row = rowKvp.Value;

            // Iterate backwards to safely remove during iteration
            for (int i = row.Units.Count - 1; i >= 0; i--)
            {
                if (!row.Units[i].IsAlive)
                {
                    player.DiscardPile.Add(row.Units[i]);
                    row.Units.RemoveAt(i);
                    moved++;
                }
            }
        }

        if (moved > 0)
        {
            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.CleanupMovedToDiscard,
                TargetPlayer = player,
                Amount = moved,
                Note = $"{player.Name} moved {moved} dead units to discard"
            }, alsoDebugLog);
        }
    }

    // ========================================
    // FIREPOWER RESOLUTION
    // ========================================

    /// <summary>
    /// Resolves commander damage using the FIREPOWER system.
    ///
    /// FIREPOWER RULES:
    /// - Firepower = Leftover damage that overflowed past all enemy units
    /// - Only the player with HIGHER Firepower deals commander damage
    /// - Commander damage = Winner's Firepower - Loser's Firepower
    /// - If tied, no commander damage to either side
    ///
    /// The leftover damage is tracked during combat as damage streams flow
    /// through units. Any damage that isn't absorbed by units becomes "firepower".
    /// </summary>
    private static void ResolveFirepowerDamage(Player a, Player b, int leftoverToB, int leftoverToA, CombatReport report, bool alsoDebugLog)
    {
        // Firepower is the leftover damage that overflowed past all enemy units
        // leftoverToB = damage from A that overflowed past B's army (A's firepower)
        // leftoverToA = damage from B that overflowed past A's army (B's firepower)
        int firepowerA = leftoverToB;
        int firepowerB = leftoverToA;

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            Note = $"[FIREPOWER] {a.Name}: Leftover damage to {b.Name} = {firepowerA}"
        }, alsoDebugLog);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            Note = $"[FIREPOWER] {b.Name}: Leftover damage to {a.Name} = {firepowerB}"
        }, alsoDebugLog);

        // Determine winner and apply commander damage
        if (firepowerA > firepowerB)
        {
            // Player A wins - deals damage to Player B's commander
            int damage = firepowerA - firepowerB;

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DebugNote,
                SourcePlayer = a,
                Note = $"[FIREPOWER] {a.Name} WINS! Firepower {firepowerA} > {firepowerB}. Deals {damage} commander damage."
            }, alsoDebugLog);

            // THICK SKIN: Apply commander damage multiplier (50% damage reduction)
            float thickSkinMult = DoctrineSystem.GetCommanderDamageMultiplier?.Invoke(b) ?? 1f;
            if (!Mathf.Approximately(thickSkinMult, 1f))
            {
                int beforeThickSkin = damage;
                damage = Mathf.RoundToInt(damage * thickSkinMult);
                AddEvent(report, new CombatEvent
                {
                    Type = CombatEventType.AbilityTriggered,
                    TargetPlayer = b,
                    Amount = damage,
                    Note = $"[THICK SKIN] {b.Name} commander damage reduced: {beforeThickSkin} -> {damage}"
                }, alsoDebugLog);
            }

            // WALLS: Apply wall defense absorption (damage absorbed before commander HP)
            if (PerkSystem.Instance != null && damage > 0)
            {
                int beforeWall = damage;
                damage = PerkSystem.Instance.ApplyWallDamage(b, damage);
                if (damage != beforeWall)
                {
                    AddEvent(report, new CombatEvent
                    {
                        Type = CombatEventType.AbilityTriggered,
                        TargetPlayer = b,
                        Amount = beforeWall - damage,
                        Note = $"[WALLS] {b.Name} wall absorbed {beforeWall - damage} damage, {damage} remaining"
                    }, alsoDebugLog);
                }
            }

            int beforeHP = b.CommanderHP;
            b.CommanderHP = Mathf.Max(0, b.CommanderHP - damage);

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.CommanderDamaged,
                SourcePlayer = a,
                TargetPlayer = b,
                Amount = damage,
                Note = $"{b.Name} commander takes {damage} damage (HP {beforeHP}->{b.CommanderHP})"
            }, alsoDebugLog);
        }
        else if (firepowerB > firepowerA)
        {
            // Player B wins - deals damage to Player A's commander
            int damage = firepowerB - firepowerA;

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DebugNote,
                SourcePlayer = b,
                Note = $"[FIREPOWER] {b.Name} WINS! Firepower {firepowerB} > {firepowerA}. Deals {damage} commander damage."
            }, alsoDebugLog);

            // THICK SKIN: Apply commander damage multiplier (50% damage reduction)
            float thickSkinMult = DoctrineSystem.GetCommanderDamageMultiplier?.Invoke(a) ?? 1f;
            if (!Mathf.Approximately(thickSkinMult, 1f))
            {
                int beforeThickSkin = damage;
                damage = Mathf.RoundToInt(damage * thickSkinMult);
                AddEvent(report, new CombatEvent
                {
                    Type = CombatEventType.AbilityTriggered,
                    TargetPlayer = a,
                    Amount = damage,
                    Note = $"[THICK SKIN] {a.Name} commander damage reduced: {beforeThickSkin} -> {damage}"
                }, alsoDebugLog);
            }

            // WALLS: Apply wall defense absorption (damage absorbed before commander HP)
            if (PerkSystem.Instance != null && damage > 0)
            {
                int beforeWall = damage;
                damage = PerkSystem.Instance.ApplyWallDamage(a, damage);
                if (damage != beforeWall)
                {
                    AddEvent(report, new CombatEvent
                    {
                        Type = CombatEventType.AbilityTriggered,
                        TargetPlayer = a,
                        Amount = beforeWall - damage,
                        Note = $"[WALLS] {a.Name} wall absorbed {beforeWall - damage} damage, {damage} remaining"
                    }, alsoDebugLog);
                }
            }

            int beforeHP = a.CommanderHP;
            a.CommanderHP = Mathf.Max(0, a.CommanderHP - damage);

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.CommanderDamaged,
                SourcePlayer = b,
                TargetPlayer = a,
                Amount = damage,
                Note = $"{a.Name} commander takes {damage} damage (HP {beforeHP}->{a.CommanderHP})"
            }, alsoDebugLog);
        }
        else
        {
            // Tied - no commander damage to either side
            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DebugNote,
                Note = $"[FIREPOWER] TIED! Both players have Firepower={firepowerA}. No commander damage."
            }, alsoDebugLog);
        }

        // MERCHANT OF DEATH: Spend leftover gold to deal extra commander damage
        ApplyMerchantOfDeath(a, b, report, alsoDebugLog);
        ApplyMerchantOfDeath(b, a, report, alsoDebugLog);
    }

    /// <summary>
    /// MERCHANT OF DEATH doctrine: Spend all remaining gold to deal commander damage.
    /// Called after normal firepower damage resolution.
    /// </summary>
    private static void ApplyMerchantOfDeath(Player attacker, Player defender, CombatReport report, bool alsoDebugLog)
    {
        if (DoctrineSystem.Instance == null) return;
        if (attacker.Gold <= 0) return;

        int bonusDamage = DoctrineSystem.Instance.GetLeftoverGoldDamage(attacker);
        if (bonusDamage <= 0) return;

        int goldSpent = attacker.Gold;
        attacker.Gold = 0;

        // Apply Thick Skin reduction to Merchant of Death damage too
        float thickSkinMult = DoctrineSystem.GetCommanderDamageMultiplier?.Invoke(defender) ?? 1f;
        if (!Mathf.Approximately(thickSkinMult, 1f))
        {
            bonusDamage = Mathf.RoundToInt(bonusDamage * thickSkinMult);
        }

        // Apply wall absorption
        if (PerkSystem.Instance != null && bonusDamage > 0)
        {
            bonusDamage = PerkSystem.Instance.ApplyWallDamage(defender, bonusDamage);
        }

        if (bonusDamage <= 0) return;

        int beforeHP = defender.CommanderHP;
        defender.CommanderHP = Mathf.Max(0, defender.CommanderHP - bonusDamage);

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.AbilityTriggered,
            SourcePlayer = attacker,
            TargetPlayer = defender,
            Amount = bonusDamage,
            Note = $"[MERCHANT OF DEATH] {attacker.Name} spends {goldSpent} gold to deal {bonusDamage} damage to {defender.Name} commander (HP {beforeHP}->{defender.CommanderHP})"
        }, alsoDebugLog);
    }

    /// <summary>
    /// Calculates total ATK and HP for a player's surviving (alive) units.
    /// Used for Firepower calculation after combat.
    /// Includes perk and doctrine ATK bonuses to match combat damage calculation.
    /// </summary>
    private static (int totalATK, int totalHP) CalculateSurvivingStats(Player player)
    {
        int totalATK = 0;
        int totalHP = 0;

        if (player != null && player.Rows != null)
        {
            foreach (var rowKvp in player.Rows)
            {
                foreach (var unit in rowKvp.Value.Units)
                {
                    if (unit != null && unit.IsAlive)
                    {
                        int atk = unit.ATK;

                        // Add Perk ATK bonuses (Stopping Power, Piercing, Explosives, Heads Up)
                        if (PerkSystem.GetTypeAtkBonus != null)
                        {
                            atk += PerkSystem.GetTypeAtkBonus(player, unit.Type);
                        }

                        // Add Doctrine ATK bonuses (Close Combat, Ranged Warrior, Iron Skin, Death from Above)
                        if (DoctrineSystem.GetTypeAtkBonus != null)
                        {
                            atk += DoctrineSystem.GetTypeAtkBonus(player, unit.Type);
                        }

                        // Combined Arms doctrine bonus (vehicles +ATK when infantry present)
                        if (DoctrineSystem.Instance != null)
                        {
                            atk += DoctrineSystem.Instance.GetCombinedArmsBonus(player, unit.Type);
                        }

                        totalATK += atk;
                        totalHP += unit.HP;
                    }
                }
            }
        }

        return (totalATK, totalHP);
    }

    // ========================================
    // HELPER METHODS
    // ========================================

    /// <summary>
    /// Checks if a player has any alive unit with a specific ability.
    /// </summary>
    private static bool HasAnyAliveAbility(Player p, UnitAbilityType ability)
    {
        foreach (var row in p.Rows.Values)
        {
            foreach (var u in row.Units)
            {
                if (u.IsAlive && u.Ability == ability)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// TAUNT (ROW FIRST): Finds which row should be attacked first.
    /// 
    /// RULE:
    /// - If defender has any alive TauntRowFirst unit, that row is attacked first
    /// - If multiple taunters in different rows, lowest InstanceId wins (deterministic)
    /// </summary>
    private static bool TryGetTauntRowFirst(Player defender, out RowType tauntRow)
    {
        tauntRow = RowType.Front;

        Unit best = null;
        RowType bestRow = RowType.Front;

        foreach (var kvp in defender.Rows)
        {
            RowType rowType = kvp.Key;
            Row row = kvp.Value;

            foreach (var u in row.Units)
            {
                if (!u.IsAlive) continue;
                if (u.Ability != UnitAbilityType.TauntRowFirst) continue;

                // Deterministic tiebreaker: lowest InstanceId wins
                if (best == null || u.InstanceId < best.InstanceId)
                {
                    best = u;
                    bestRow = rowType;
                }
            }
        }

        if (best == null)
            return false;

        tauntRow = bestRow;
        return true;
    }

    /// <summary>
    /// Emits UI-safe TargetMarked events for any units currently marked.
    /// This bridges buy-phase state (IsMarkedTarget) into the CombatReport
    /// so the UI/playback can display it at combat start.
    /// </summary>
    private static void EmitTargetMarkedEvents(Player ownerOfMarkedUnits, CombatReport report, bool alsoDebugLog)
    {
        foreach (var kvp in ownerOfMarkedUnits.Rows)
        {
            RowType rowType = kvp.Key;
            Row row = kvp.Value;

            foreach (var u in row.Units)
            {
                if (!u.IsAlive) continue;

                if (u.IsMarkedTarget)
                {
                    AddEvent(report, new CombatEvent
                    {
                        Type = CombatEventType.TargetMarked,
                        TargetPlayer = ownerOfMarkedUnits,
                        TargetUnit = u,
                        Row = rowType,
                        Note = $"[TARGET] {ownerOfMarkedUnits.Name}: '{u.Name}' is MARKED (Row={rowType}, UnitId={u.InstanceId})"
                    }, alsoDebugLog);
                }
            }
        }
    }

    /// <summary>
    /// Adds an event to the combat report.
    ///
    /// LOGGING BEHAVIOR:
    /// - Combat simulation is QUIET by default (no console spam)
    /// - If alsoDebugLog is true, only DebugNote events are printed
    /// - All events are added to the report regardless (for playback)
    /// </summary>
    private static void AddEvent(CombatReport report, CombatEvent e, bool alsoDebugLog)
    {
        report.AddEvent(e);

        // Only log DebugNote events to console (if enabled)
        // This keeps combat simulation clean while allowing optional verbose output
        if (alsoDebugLog && e.Type == CombatEventType.DebugNote && !string.IsNullOrEmpty(e.Note))
            Debug.Log(e.Note);
    }

    /// <summary>
    /// Counts alive units per UnitType for a player.
    /// Used to store starting counts so playback knows when a row is wiped.
    /// </summary>
    private static Dictionary<UnitType, int> CountUnitsByType(Player player)
    {
        var counts = new Dictionary<UnitType, int>
        {
            { UnitType.Infantry, 0 },
            { UnitType.Vehicle, 0 },
            { UnitType.Air, 0 },
            { UnitType.Ranged, 0 }
        };

        if (player?.Rows == null) return counts;

        foreach (var rowKvp in player.Rows)
        {
            foreach (var unit in rowKvp.Value.Units)
            {
                if (unit != null && unit.IsAlive)
                {
                    counts[unit.Type]++;
                }
            }
        }

        return counts;
    }
}