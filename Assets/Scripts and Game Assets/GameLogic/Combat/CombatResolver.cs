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

        // Apply main damage streams (returns counter damage)
       int counterBackToA = ApplyDamageBucketsToEntireArmy(
    attacker: a,
    defender: b,
    buckets: atkBucketsA,
    report: report,
    alsoDebugLog: alsoDebugLog,
    attackerHasArmorPiercing: aHasArmorPiercing,
    allowCounterattack: true
);

int counterBackToB = ApplyDamageBucketsToEntireArmy(
    attacker: b,
    defender: a,
    buckets: atkBucketsB,
    report: report,
    alsoDebugLog: alsoDebugLog,
    attackerHasArmorPiercing: bHasArmorPiercing,
    allowCounterattack: true
);


        // Apply counterattack streams (no further counters to prevent infinite loops)
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

            ApplyDamageToEntireArmy(
                attacker: b,
                defender: a,
                damage: counterBackToA,
                report: report,
                alsoDebugLog: alsoDebugLog,
                attackerHasArmorPiercing: false,
                allowCounterattack: false // Prevent counter-counter loops
            );
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

            ApplyDamageToEntireArmy(
                attacker: a,
                defender: b,
                damage: counterBackToB,
                report: report,
                alsoDebugLog: alsoDebugLog,
                attackerHasArmorPiercing: false,
                allowCounterattack: false // Prevent counter-counter loops
            );
        }

        // PHASE 3: Cleanup dead units
        CleanupDeadUnits(a, report, alsoDebugLog);
        CleanupDeadUnits(b, report, alsoDebugLog);

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

        foreach (var s in strikes)
        {
            if (s.Target == null || !s.Target.IsAlive) continue;

            if (!dmgByTarget.ContainsKey(s.Target))
            {
                dmgByTarget[s.Target] = 0;
                ownerByTarget[s.Target] = s.DefenderOwner;
                rowByTarget[s.Target] = s.TargetRow;
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

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DamageAppliedToUnit,
                TargetPlayer = targetOwner,
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

        buckets[u.Type] += atk;
    }
}

/// <summary>
/// Applies multiple damage streams (one per UnitType), allowing defender commander to modify damage taken
/// by attacker type (Geralt vs Ranged).
/// Returns total counter-stream damage back to attacker.
/// </summary>
private static int ApplyDamageBucketsToEntireArmy(
    Player attacker,
    Player defender,
    Dictionary<UnitType, int> buckets,
    CombatReport report,
    bool alsoDebugLog,
    bool attackerHasArmorPiercing,
    bool allowCounterattack)
{
    int counterTotal = 0;

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

        int counter = ApplyDamageToEntireArmy(
            attacker: attacker,
            defender: defender,
            damage: adjusted,
            report: report,
            alsoDebugLog: alsoDebugLog,
            attackerHasArmorPiercing: attackerHasArmorPiercing,
            allowCounterattack: allowCounterattack
        );

        counterTotal += counter;
    }

    return counterTotal;
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
    /// - Any remaining damage hits the commander
    /// 
    /// TAUNT:
    /// - If defender has a TauntRowFirst unit, that row is attacked first
    /// 
    /// RETURNS:
    /// Total counterattack damage to send back to the attacker
    /// </summary>
    private static int ApplyDamageToEntireArmy(
        Player attacker,
        Player defender,
        int damage,
        CombatReport report,
        bool alsoDebugLog,
        bool attackerHasArmorPiercing,
        bool allowCounterattack)
    {
        if (damage <= 0)
            return 0;

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
                counterStreamBackToAttacker: ref counterStreamBackToAttacker
            );
        }

        // Leftover damage hits commander
        if (damage > 0)
        {
            int before = defender.CommanderHP;
            defender.CommanderHP = Mathf.Max(0, defender.CommanderHP - damage);

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.CommanderDamaged,
                SourcePlayer = attacker,
                TargetPlayer = defender,
                Amount = damage,
                Note = $"{defender.Name} commander takes {damage} damage (HP {before}->{defender.CommanderHP})"
            }, alsoDebugLog);

            damage = 0;
        }

        return counterStreamBackToAttacker;
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
        ref int counterStreamBackToAttacker)
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

            // Reduce stream by absorbed amount
            damage -= dr.AbsorbedFromStream;

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.DamageAppliedToUnit,
                SourcePlayer = attacker,
                TargetPlayer = defender,
                TargetUnit = unit,
                Amount = dr.AppliedToHP,
                Row = row.RowType,
                Note = $"{defender.Name}'s {unit.Name} takes {dr.AppliedToHP} damage ({row.RowType}), HP {beforeHP}->{afterHP} (absorbed {dr.AbsorbedFromStream}, remaining stream={damage})" 
                       + (unit.IsMarkedTarget ? " [MARKED]" : "")  // Add MARKED indicator for playback
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
            }
        }

        AddEvent(report, new CombatEvent
        {
            Type = CombatEventType.DebugNote,
            TargetPlayer = defender,
            Amount = damage,
            Row = row.RowType,
            Note = $"[ROW END] {defender.Name} {row.RowType}: remaining stream damage={damage}"
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
UnitAbilityType effectiveAbility = targetUnit.Ability;

bool empActive =
    (SpecialCards.IsEmpActiveOnPlayerThisRound != null) &&
    SpecialCards.IsEmpActiveOnPlayerThisRound(targetOwner);

if (empActive && targetUnit.Type == UnitType.Vehicle)
{
    effectiveAbility = UnitAbilityType.None;

    AddEvent(report, new CombatEvent
    {
        Type = CombatEventType.AbilityTriggered,
        TargetPlayer = targetOwner,
        TargetUnit = targetUnit,
        Row = row,
        Note = $"[EMP] '{targetUnit.Name}' vehicle ability disabled this round"
    }, alsoDebugLog);
}


        // DEFAULT BEHAVIOR (normal units):
        // - Unit absorbs up to its remaining HP from the stream
        // - Unit takes that same amount to HP (1:1 ratio)
        int absorbed = Mathf.Min(streamIncoming, hp);
        int applied = absorbed;

        // ========================================
        // TARGET MARK: 2x HP damage per stream damage
        // ========================================
        if (targetUnit.IsMarkedTarget && streamIncoming > 0)
        {
            // MARKED UNIT RULE:
            // - Takes 2x HP damage per 1 stream damage (vulnerability)
            // - Stream is only reduced by the amount "absorbed", NOT by damage dealt
            // 
            // EXAMPLE: Unit has 10 HP, stream is 20
            // - We only need 5 stream to deal 10 HP damage (5 stream × 2 = 10 HP)
            // - Stream is reduced by 5 (not 10)

            // Calculate how much stream is needed to kill this unit
            int needStreamToKill = (hp + 1) / 2; // Ceiling division

            absorbed = Mathf.Min(streamIncoming, needStreamToKill);
            applied = Mathf.Min(hp, absorbed * 2);

            AddEvent(report, new CombatEvent
            {
                Type = CombatEventType.AbilityTriggered,
                TargetPlayer = targetOwner,
                TargetUnit = targetUnit,
                Row = row,
                Amount = applied,
                Note = $"[TARGET] {targetOwner.Name} {row}: '{targetUnit.Name}' is MARKED -> takes 2x HP damage per stream (absorbs {absorbed}, takes {applied})"
            }, alsoDebugLog);
        }
        // ========================================
// SPECIAL EFFECT: TRENCHES (Infantry take half HP damage but absorb like tank)
// Whole battle effect controlled by SpecialCards.HasTrenchesThisBattle(owner)
// ========================================
bool trenchesActive =
    (SpecialCards.HasTrenchesThisBattle != null) &&
    SpecialCards.HasTrenchesThisBattle(targetOwner);

if (trenchesActive && targetUnit.Type == UnitType.Infantry)
{
    // Same “tank armor” behavior:
    // Threshold = 2x current HP
    int threshold = hp * 2;

    AddEvent(report, new CombatEvent
    {
        Type = CombatEventType.AbilityTriggered,
        TargetPlayer = targetOwner,
        TargetUnit = targetUnit,
        Row = row,
        Note = $"[TRENCHES] '{targetUnit.Name}' infantry protected (threshold={threshold})"
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
        // ARMORED: absorbs normally (up to HP) but takes half HP damage
        int trenchAbsorbed = Mathf.Min(streamIncoming, hp);
        int trenchApplied = Mathf.RoundToInt(trenchAbsorbed * 0.5f);

        // Match your tank edge-case behavior: marked multiplies final HP damage
        if (targetUnit.IsMarkedTarget && trenchApplied > 0)
            trenchApplied = Mathf.Min(hp, trenchApplied * 2);

        return new DamageResult
        {
            AbsorbedFromStream = trenchAbsorbed,
            AppliedToHP = trenchApplied
        };
    }
}


        // ========================================
        // ARMORED TANK: Damage reduction or overwhelm
        // ========================================
if (effectiveAbility == UnitAbilityType.ReduceIncomingDamage50Percent)
        {
            // ARMOR PIERCING: Completely disables tank armor
            if (attackerHasArmorPiercing)
            {
                AddEvent(report, new CombatEvent
                {
                    Type = CombatEventType.AbilityTriggered,
                    TargetPlayer = targetOwner,
                    TargetUnit = targetUnit,
                    Row = row,
                    Note = $"[ARMOR PIERCE] Armor reduction disabled on '{targetUnit.Name}'"
                }, alsoDebugLog);

                // Return early with default damage (no armor bonus)
                return new DamageResult
                {
                    AbsorbedFromStream = absorbed,
                    AppliedToHP = applied
                };
            }

            // TANK ARMOR LOGIC:
            // Tank has two states: "armored" or "overwhelmed"
            // Threshold = 2x current HP

            int threshold = hp * 2;

            if (streamIncoming >= threshold)
            {
                // OVERWHELMED STATE:
                // - Tank dies instantly (takes full HP damage)
                // - BUT it absorbs a big chunk from stream (2x HP)
                // 
                // EXAMPLE: Tank has 12 HP, stream is 30
                // - Threshold = 24
                // - Stream (30) >= threshold (24) → overwhelmed
                // - Tank dies (takes 12 HP damage)
                // - Stream is reduced by 24 (not 12)

                return new DamageResult
                {
                    AbsorbedFromStream = Mathf.Min(streamIncoming, threshold),
                    AppliedToHP = hp
                };
            }
            else
            {
                // ARMORED STATE:
                // - Tank takes HALF damage to HP
                // - BUT still absorbs full amount from stream
                // 
                // EXAMPLE: Tank has 12 HP, stream is 10
                // - Threshold = 24
                // - Stream (10) < threshold (24) → armored
                // - Tank absorbs 10 from stream
                // - Tank takes 5 HP damage (10 × 50%)

                int tankAbsorbed = Mathf.Min(streamIncoming, hp);
                int tankApplied = Mathf.RoundToInt(tankAbsorbed * 0.5f);

                // EDGE CASE: Marked tank still gets 2x HP damage multiplier
                // (even though it's armored, the mark overrides the reduction)
                if (targetUnit.IsMarkedTarget && tankApplied > 0)
                    tankApplied = Mathf.Min(hp, tankApplied * 2);

                return new DamageResult
                {
                    AbsorbedFromStream = tankAbsorbed,
                    AppliedToHP = tankApplied
                };
            }
        }

        // Return default damage result (normal units, or marked units handled above)
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
}