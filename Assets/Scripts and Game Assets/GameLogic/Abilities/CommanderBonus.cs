using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CommanderBonus = permanent "commander identity" per Player + bonus rules.
///
/// DESIGN:
/// - Similar to SpecialCards: static hooks so static systems (CombatResolver, AbilitySystem) can query.
/// - Always-on bonuses can be applied permanently to units on spawn (mutating HP/ATK).
/// - Conditional bonuses (terrain/weather) are queried dynamically (so they can turn on/off mid-battle).
/// </summary>
public class CommanderBonus : MonoBehaviour
{
    public enum CommanderId
    {
        None,

        // Examples from your list
        Gruff,
        Hank,
        Leo,
        Nicholas,
        Alex,
        Ashley,
        Roxanne,
        Seth,
        Aqua
    }

    // =========================
    // Static hooks (query-style)
    // =========================

    /// <summary>Returns the current commander for a player.</summary>
    public static Func<Player, CommanderId> GetCommander;

    /// <summary>
    /// Dynamic attack multiplier (includes terrain/weather gated bonuses).
    /// Use this in combat snapshot calculations.
    /// </summary>
    public static Func<Player, Unit, float> GetAttackMultiplier;

    /// <summary>
    /// Dynamic HP multiplier (currently only always-on/type-based; no terrain/weather HP bonuses in your examples).
    /// </summary>
    public static Func<Player, Unit, float> GetHpMultiplier;

    /// <summary>
    /// Returns cost delta to apply at time of purchase / affordability checks.
    /// (Leo: all units cost -1 forever)
    /// </summary>
    public static Func<Player, UnitCard, int> GetCostDelta;

    /// <summary>
    /// Optional: damage-taken multiplier vs an attacker unit type (Nicholas).
    /// NOTE: Your current "stream damage" model doesn't carry per-source-type info,
    /// so you'll need an integration point if you later track type-specific damage sources.
    /// </summary>
    public static Func<Player, UnitType, float> GetDamageTakenMultiplierAgainstAttackerType;

    // =========================
    // Instance storage
    // =========================

    private readonly Dictionary<Player, CommanderId> commanderByPlayer = new();

    // Cached battlefield state (so bonuses can flip on/off)
    private Battlefield.TerrainType cachedTerrain = Battlefield.TerrainType.Plains;
    private Battlefield.WeatherType cachedWeather = Battlefield.WeatherType.Clear;

public static CommanderBonus Instance { get; private set; }

    private void Awake()
    {
            Instance = this; // Now UI can call: CommanderBonus.Instance.GetEffectiveStatsForCard(...)

        // Wire static hooks (like SpecialCards does)
        GetCommander = (p) => p != null && commanderByPlayer.TryGetValue(p, out var id) ? id : CommanderId.None;

        GetAttackMultiplier = (p, u) =>
        {
            if (p == null || u == null) return 1f;
            return ComputeAttackMultiplier(p, u);
        };

        GetHpMultiplier = (p, u) =>
        {
            if (p == null || u == null) return 1f;
            return ComputeHpMultiplier(p, u);
        };

        GetCostDelta = (p, card) =>
        {
            if (p == null || card == null) return 0;
            return ComputeCostDelta(p, card);
        };

        GetDamageTakenMultiplierAgainstAttackerType = (defender, attackerType) =>
        {
            if (defender == null) return 1f;
            return ComputeDamageTakenMultiplier(defender, attackerType);
        };

        // Initialize cache from current battlefield (if present)
        if (Battlefield.Instance != null)
        {
            cachedTerrain = Battlefield.Instance.Terrain;
            cachedWeather = Battlefield.Instance.Weather;
        }

        // Listen for changes
        Battlefield.OnTerrainChanged += HandleTerrainChanged;
        Battlefield.OnWeatherChanged += HandleWeatherChanged;
    }

    private void OnDestroy()
    {
        Battlefield.OnTerrainChanged -= HandleTerrainChanged;
        Battlefield.OnWeatherChanged -= HandleWeatherChanged;
    }
private CommanderId GetCommanderId(Player p)
{
    if (p == null)
    {
        Debug.LogWarning("[COMMANDER] GetCommanderId called with NULL player!");
        return CommanderId.None;
    }

    bool found = commanderByPlayer.TryGetValue(p, out var id);
    if (!found)
    {
        Debug.LogWarning($"[COMMANDER] GetCommanderId: Player '{p.Name}' (hash={p.GetHashCode()}) NOT found in dictionary! Dictionary has {commanderByPlayer.Count} entries.");
        // Log all keys in dictionary for debugging
        foreach (var kvp in commanderByPlayer)
        {
            Debug.Log($"[COMMANDER]   - Dictionary contains: '{kvp.Key?.Name}' (hash={kvp.Key?.GetHashCode()}) -> {kvp.Value}");
        }
        return CommanderId.None;
    }

    return id;
}
public void ApplyPermanentBonusesToCards(Player owner, List<UnitCard> cards)
{
    if (owner == null || cards == null) return;

    var cmd = GetCommanderId(owner);
    Debug.Log($"[COMMANDER BONUS] {owner.Name} | Commander={cmd} | Applying to {cards.Count} cards");

    for (int i = 0; i < cards.Count; i++)
    {
        ApplyPermanentBonusesToCard(owner, cards[i]);
    }
}
/// <summary>
/// FEBRUARY 2026 COMMANDER BONUS SYSTEM
///
/// Bonuses are now flat +1 ATK instead of percentage multipliers.
/// This makes the system simpler and more predictable.
///
/// PERMANENT BONUSES (applied at card creation):
/// - Gruff: All vehicle units have +1 ATK
/// - Hank: All infantry units have +1 ATK
/// - Leo: All units cost 1 less gold, forever
/// - Alex: Ranged units have +1 ATK
/// - Ashley: Air units have +1 ATK
///
/// CONDITIONAL BONUSES (applied during combat):
/// - Seth: All units gain +1 ATK on "roads" maps
/// - Aqua: All units gain +1 ATK in rainy or snowy weather
///
/// ROUND-BASED BONUSES (special handling):
/// - Roxanne: Ranged units cost 2 less gold in round 1 of every battle
///
/// META BONUSES (not affecting cards directly):
/// - Nicholas: Can equip 1 extra Perk (handled in perk system)
/// </summary>
public void ApplyPermanentBonusesToCard(Player owner, UnitCard card)
{
    if (owner == null || card == null) return;

    // stamp owner so UI can read it
    card.Owner = owner;

    var cmd = GetCommanderId(owner);

    if (card.IsSpecialCard) return; // No bonuses for special cards

    // Leo: all units cost 1 less gold forever
    if (cmd == CommanderId.Leo)
    {
        int oldCost = card.Cost;
        card.Cost = Mathf.Max(0, card.Cost - 1);
        Debug.Log($"[COMMANDER BONUS] Leo cost reduction: {oldCost} -> {card.Cost}");
    }

    // Gruff: all vehicle units have +1 ATK
    if (cmd == CommanderId.Gruff && card.Type == UnitType.Vehicle)
    {
        int oldATK = card.BaseATK;
        card.BaseATK += 1;
        Debug.Log($"[COMMANDER BONUS] Gruff (Vehicle) applied: ATK {oldATK} -> {card.BaseATK}");
    }

    // Hank: all infantry units have +1 ATK
    if (cmd == CommanderId.Hank && card.Type == UnitType.Infantry)
    {
        int oldATK = card.BaseATK;
        card.BaseATK += 1;
        Debug.Log($"[COMMANDER BONUS] Hank (Infantry) applied: ATK {oldATK} -> {card.BaseATK}");
    }

    // Alex: ranged units have +1 ATK
    if (cmd == CommanderId.Alex && card.Type == UnitType.Ranged)
    {
        int oldATK = card.BaseATK;
        card.BaseATK += 1;
        Debug.Log($"[COMMANDER BONUS] Alex (Ranged) applied: ATK {oldATK} -> {card.BaseATK}");
    }

    // Ashley: air units have +1 ATK
    if (cmd == CommanderId.Ashley && card.Type == UnitType.Air)
    {
        int oldATK = card.BaseATK;
        card.BaseATK += 1;
        Debug.Log($"[COMMANDER BONUS] Ashley (Air) applied: ATK {oldATK} -> {card.BaseATK}");
    }

    // Roxanne: ranged units cost 2 less gold in round 1 (handled in ApplyRound1Bonuses)
    // Nicholas: extra perk slot (handled in perk system)
}

private void HandleTerrainChanged(Battlefield.TerrainType t)
{
    cachedTerrain = t;
    if (IsInBattleContext)
        OnEffectiveStatsChanged?.Invoke();
}

private void HandleWeatherChanged(Battlefield.WeatherType w)
{
    cachedWeather = w;
    if (IsInBattleContext)
        OnEffectiveStatsChanged?.Invoke();
}


    // =========================
    // Public API (set commander)
    // =========================

    /// <summary>
    /// Call this when a campaign starts / player picks a commander.
    /// </summary>
    public void SetCommanderForPlayer(Player player, CommanderId commander)
    {
        if (player == null)
        {
            Debug.LogError("[COMMANDER] SetCommanderForPlayer called with NULL player!");
            return;
        }
        commanderByPlayer[player] = commander;
        Debug.Log($"[COMMANDER] SetCommanderForPlayer: {player.Name} (hash={player.GetHashCode()}) set to {commander}. Dictionary now has {commanderByPlayer.Count} entries.");
    }

    /// <summary>
    /// Applies only ALWAYS-ON + TYPE-based bonuses permanently onto a newly created unit.
    /// (Does NOT apply terrain/weather gated ATK bonuses, because those can flip on/off.)
    ///
    /// Call this right after you create a Unit instance (and after ApplyGlobalMultipliers if you want).
    /// </summary>
    public void ApplyPermanentBonusesToUnit(Player owner, Unit unit)
    {
        // NOTE (Feb 2026): Permanent ATK bonuses are now flat +1 ATK applied at card creation
        // via ApplyPermanentBonusesToCard(). Unit HP is no longer used in the new combat system.
        // This method is kept for call-site compatibility but no longer modifies any stats.
    }

    // =========================
    // Bonus computations
    // =========================

  public struct EffectiveCardStats
{
    public int Attack;
    public int HP;
    public int Cost;

    // 1.0 means normal damage taken, 0.8 means 20% less damage taken
    public float DamageTakenMultiplierVsRanged;
}

public EffectiveCardStats GetEffectiveStatsForCard(UnitCard card)
{
    if (card == null)
        return default;

    // Start from the card's CURRENT runtime values (already includes permanent commander changes)
    int atk = card.BaseATK;
    int hp = card.BaseHP;
    int cost = card.Cost;

    var cmd = GetCommanderId(card.Owner);

    // Conditional (battlefield-dependent) bonuses: Seth/Aqua
    // Only active during battle context
    if (IsInBattleContext && !card.IsSpecialCard)
    {
        // Seth: +1 ATK on roads maps
        if (cmd == CommanderId.Seth && cachedTerrain == Battlefield.TerrainType.Roads)
            atk += 1;

        // Aqua: +1 ATK in rain or snow
        if (cmd == CommanderId.Aqua &&
            (cachedWeather == Battlefield.WeatherType.Rain || cachedWeather == Battlefield.WeatherType.Snow))
            atk += 1;
    }

    return new EffectiveCardStats
    {
        Attack = atk,
        HP = hp,
        Cost = Mathf.Max(0, cost),
        DamageTakenMultiplierVsRanged = 1f // Nicholas perk bonus handled elsewhere
    };
}

/// <summary>
/// FEBRUARY 2026 COMMANDER BONUSES:
/// - Gruff: Vehicle units +1 ATK
/// - Hank: Infantry units +1 ATK
/// - Leo: All units cost 1 less gold
/// - Alex: Ranged units +1 ATK
/// - Ashley: Air units +1 ATK
/// - Roxanne: Ranged units cost 2 less in round 1 (handled separately)
/// - Seth: +1 ATK on roads maps (conditional)
/// - Aqua: +1 ATK in rain/snow (conditional)
/// - Nicholas: Extra perk slot (meta, not stat-changing)
/// </summary>
public EffectiveCardStats GetEffectiveStatsForCard(Player owner, UnitCardDefinition def)
{
    if (def == null)
        return default;

    int atk = def.atk;
    int hp  = def.hp;
    int cost = def.cost;

    var cmd = GetCommanderId(owner);

    if (!def.isSpecialCard)
    {
        // Permanent commander adjustments (flat +1 ATK bonuses)
        switch (cmd)
        {
            case CommanderId.Leo:
                // All units cost 1 less gold forever
                cost = Mathf.Max(0, cost - 1);
                break;

            case CommanderId.Gruff:
                // Vehicle units +1 ATK
                if (def.type == UnitType.Vehicle)
                    atk += 1;
                break;

            case CommanderId.Hank:
                // Infantry units +1 ATK
                if (def.type == UnitType.Infantry)
                    atk += 1;
                break;

            case CommanderId.Alex:
                // Ranged units +1 ATK
                if (def.type == UnitType.Ranged)
                    atk += 1;
                break;

            case CommanderId.Ashley:
                // Air units +1 ATK
                if (def.type == UnitType.Air)
                    atk += 1;
                break;

            // Roxanne: Round 1 cost reduction handled in ApplyRound1Bonuses
            // Nicholas: Extra perk slot (meta bonus, not stat-changing)
        }

        // Conditional bonuses (terrain/weather)
        if (Battlefield.Instance != null)
        {
            // Seth: +1 ATK on roads maps
            if (cmd == CommanderId.Seth && cachedTerrain == Battlefield.TerrainType.Roads)
                atk += 1;

            // Aqua: +1 ATK in rain or snow
            if (cmd == CommanderId.Aqua &&
                (cachedWeather == Battlefield.WeatherType.Rain || cachedWeather == Battlefield.WeatherType.Snow))
                atk += 1;
        }
    }

    return new EffectiveCardStats
    {
        Attack = atk,
        HP = hp,
        Cost = Mathf.Max(0, cost),
        DamageTakenMultiplierVsRanged = 1f
    };
}

public static event Action OnEffectiveStatsChanged;

public bool IsInBattleContext { get; private set; }

public void EnterBattleContext()
{
    IsInBattleContext = true;

    // Sync to current battlefield state (if present)
    if (Battlefield.Instance != null)
    {
        cachedTerrain = Battlefield.Instance.Terrain;
        cachedWeather = Battlefield.Instance.Weather;
    }

    OnEffectiveStatsChanged?.Invoke();
}

public void ExitBattleContext()
{
    IsInBattleContext = false;
    OnEffectiveStatsChanged?.Invoke();
}



    // ========================================
    // FEBRUARY 2026: FLAT ATK BONUS SYSTEM
    // ========================================
    // Bonuses are now flat +1 ATK instead of percentage multipliers.
    // The multiplier functions are kept for backwards compatibility but now return 1.0.
    // Actual bonuses are applied in ApplyPermanentBonusesToCard (permanent)
    // and GetConditionalATKBonus (terrain/weather).

    private float ComputeAttackMultiplier(Player owner, Unit unit)
    {
        // Permanent bonuses are already baked into unit.ATK via ApplyPermanentBonusesToCard
        // Conditional bonuses are applied via GetConditionalATKBonus
        return 1f;
    }

    private float ComputeAttackMultiplier_AlwaysOn(Player owner, Unit unit)
    {
        // DEPRECATED: Permanent bonuses are now flat +1 ATK applied at card creation
        return 1f;
    }

    private float ComputeAttackMultiplier_Conditional(Player owner, Unit unit)
    {
        // DEPRECATED: Conditional bonuses are now flat +1 ATK via GetConditionalATKBonus
        return 1f;
    }

    /// <summary>
    /// Gets the conditional ATK bonus for terrain/weather effects.
    /// Called during combat ATK calculation.
    ///
    /// - Seth: +1 ATK on "roads" maps
    /// - Aqua: +1 ATK in rainy or snowy weather
    /// </summary>
    public int GetConditionalATKBonus(Player owner, Unit unit)
    {
        if (owner == null || unit == null) return 0;

        var cmd = GetCommanderId(owner);
        int bonus = 0;

        switch (cmd)
        {
            case CommanderId.Seth:
                // All units gain +1 ATK on roads maps
                if (cachedTerrain == Battlefield.TerrainType.Roads)
                    bonus += 1;
                break;

            case CommanderId.Aqua:
                // All units gain +1 ATK in rain or snow
                if (cachedWeather == Battlefield.WeatherType.Rain ||
                    cachedWeather == Battlefield.WeatherType.Snow)
                    bonus += 1;
                break;
        }

        return bonus;
    }

    /// <summary>
    /// Applies Roxanne's round 1 cost reduction to ranged units.
    /// Call this at the start of round 1 of each battle.
    /// Ranged units cost 2 less gold in round 1.
    /// </summary>
    public void ApplyRound1Bonuses(Player owner, List<UnitCard> hand)
    {
        if (owner == null || hand == null) return;

        var cmd = GetCommanderId(owner);

        if (cmd == CommanderId.Roxanne)
        {
            foreach (var card in hand)
            {
                if (card != null && !card.IsSpecialCard && card.Type == UnitType.Ranged)
                {
                    int oldCost = card.Cost;
                    card.Cost = Mathf.Max(0, card.Cost - 2);
                    Debug.Log($"[COMMANDER BONUS] Roxanne round 1: {card.Name} cost {oldCost} -> {card.Cost}");
                }
            }
        }
    }

    /// <summary>
    /// Checks if the player has Nicholas commander (for extra perk slot).
    /// </summary>
    public bool HasExtraPerkSlot(Player owner)
    {
        if (owner == null) return false;
        return GetCommanderId(owner) == CommanderId.Nicholas;
    }

    private float ComputeHpMultiplier(Player owner, Unit unit)
    {
        // DEPRECATED: Units no longer have HP in the new combat system.
        // HP bonuses have been removed. All commander bonuses are now ATK-based.
        return 1f;
    }

    private int ComputeCostDelta(Player owner, UnitCard card)
    {
        var cmd = GetCommander(owner);

        // Leo: All units cost 1 less gold, forever
        if (cmd == CommanderId.Leo)
        {
            // Usually you don't want special/spell cards discounted unless you explicitly do.
            // If you DO want special cards discounted too, remove this guard.
            if (!card.IsSpecialCard)
                return -1;
        }

        return 0;
    }
private float ComputeDamageTakenMultiplier(Player defender, UnitType attackerType)
{
    if (defender == null)
        return 1f;

    var cmd = GetCommanderId(defender);

    // Nicholas: take 20% less damage from Ranged attackers
    if (cmd == CommanderId.Nicholas && attackerType == UnitType.Ranged)
        return 0.80f;

    return 1f;
}

    // =========================
    // Convenience helpers
    // =========================

    /// <summary>
    /// Returns the effective cost (base + delta), clamped to 0.
    /// Handy for Buy UI / affordability checks.
    /// </summary>
    public static int GetEffectiveCost(Player owner, UnitCard card)
    {
        int baseCost = card != null ? card.Cost : 0;
        int delta = (GetCostDelta != null) ? GetCostDelta(owner, card) : 0;
        return Mathf.Max(0, baseCost + delta);
    }

    /// <summary>
    /// Returns ATK after applying dynamic (terrain/weather) multipliers.
    /// Handy during combat snapshot.
    /// </summary>
    public static int GetEffectiveAttack(Player owner, Unit unit)
    {
        if (unit == null) return 0;
        float m = (GetAttackMultiplier != null) ? GetAttackMultiplier(owner, unit) : 1f;
        return Mathf.Max(0, Mathf.RoundToInt(unit.ATK * m));
    }
}
