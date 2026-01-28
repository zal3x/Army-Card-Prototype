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
        Albert,
        Hank,
        Roy,
        Geralt,
        Aelin,
        Selena,
        Roxanne,
        Terra,
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
    /// (Roy: all units cost -1 forever)
    /// </summary>
    public static Func<Player, UnitCard, int> GetCostDelta;

    /// <summary>
    /// Optional: damage-taken multiplier vs an attacker unit type (Geralt).
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
    if (p == null) return CommanderId.None;
    return commanderByPlayer.TryGetValue(p, out var id) ? id : CommanderId.None;
}
public void ApplyPermanentBonusesToCards(Player owner, List<UnitCard> cards)
{
    if (owner == null || cards == null) return;

    for (int i = 0; i < cards.Count; i++)
    {
        ApplyPermanentBonusesToCard(owner, cards[i]);
    }
}
public void ApplyPermanentBonusesToCard(Player owner, UnitCard card)
{
    if (owner == null || card == null) return;

    // stamp owner so UI can read it
    card.Owner = owner;

    var cmd = GetCommanderId(owner);

    // Roy: all units cost 1 less gold forever (not specials)
    if (cmd == CommanderId.Roy && !card.IsSpecialCard)
    {
        card.Cost = Mathf.Max(0, card.Cost - 1);
    }

    // Albert: all units 130% ATK, 80% HP (not specials)
    if (cmd == CommanderId.Albert && !card.IsSpecialCard)
    {
        card.BaseATK = Mathf.RoundToInt(card.BaseATK * 1.30f);
        card.BaseHP  = Mathf.RoundToInt(card.BaseHP * 0.80f);
    }

    // (We can add Aelin/Selena/Roxanne/Hank next once this test works)
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
        if (player == null) return;
        commanderByPlayer[player] = commander;
        Debug.Log($"[COMMANDER] {player.Name} set commander = {commander}");
    }

    /// <summary>
    /// Applies only ALWAYS-ON + TYPE-based bonuses permanently onto a newly created unit.
    /// (Does NOT apply terrain/weather gated ATK bonuses, because those can flip on/off.)
    ///
    /// Call this right after you create a Unit instance (and after ApplyGlobalMultipliers if you want).
    /// </summary>
    public void ApplyPermanentBonusesToUnit(Player owner, Unit unit)
    {
        if (owner == null || unit == null) return;

        // Always-on and type-based HP/ATK changes are baked into stats here.
        float atkMultAlwaysOn = ComputeAttackMultiplier_AlwaysOn(owner, unit);
        float hpMultAlwaysOn = ComputeHpMultiplier(owner, unit); // in your list, HP bonuses are always-on/type-based only

        if (!Mathf.Approximately(atkMultAlwaysOn, 1f))
            unit.ATK = Mathf.Max(0, Mathf.RoundToInt(unit.ATK * atkMultAlwaysOn));

        if (!Mathf.Approximately(hpMultAlwaysOn, 1f))
        {
            int newMax = Mathf.Max(1, Mathf.RoundToInt(unit.MaxHP * hpMultAlwaysOn));
            unit.MaxHP = newMax;
            unit.HP = newMax;
        }
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

    float dmgTakenVsRanged = 1f;

    var cmd = GetCommanderId(card.Owner);

    // Conditional (battlefield-dependent) bonuses: Terra/Aqua
    // Only active during battle context
    if (IsInBattleContext && !card.IsSpecialCard)
    {
        if (cmd == CommanderId.Terra && cachedTerrain == Battlefield.TerrainType.Roads)
            atk = Mathf.RoundToInt(atk * 1.50f);

        if (cmd == CommanderId.Aqua && cachedWeather == Battlefield.WeatherType.Rain)
            atk = Mathf.RoundToInt(atk * 1.50f);
    }

    // Geralt is not stat-changing, but useful for tooltip later
    if (cmd == CommanderId.Geralt)
        dmgTakenVsRanged = 0.80f;

    return new EffectiveCardStats
    {
        Attack = atk,
        HP = hp,
        Cost = Mathf.Max(0, cost),
        DamageTakenMultiplierVsRanged = dmgTakenVsRanged
    };
}

public EffectiveCardStats GetEffectiveStatsForCard(Player owner, UnitCardDefinition def)
{
    if (def == null)
        return default;

    int atk = def.atk;
    int hp  = def.hp;
    int cost = def.cost;

    float dmgTakenVsRanged = 1f;

    var cmd = GetCommanderId(owner);

    // Permanent (always-on) commander adjustments
    switch (cmd)
    {
        case CommanderId.Roy:
            // Units cost -1 forever (leave specials unchanged)
            if (!def.isSpecialCard) cost = Mathf.Max(0, cost - 1);
            break;

        case CommanderId.Albert:
            // All units: 130% ATK, 80% HP
            if (!def.isSpecialCard)
            {
                atk = Mathf.RoundToInt(atk * 1.30f);
                hp  = Mathf.RoundToInt(hp  * 0.80f);
            }
            break;

        case CommanderId.Aelin:
            // Infantry: +20% ATK/HP
            if (!def.isSpecialCard && def.type == UnitType.Infantry)
            {
                atk = Mathf.RoundToInt(atk * 1.20f);
                hp  = Mathf.RoundToInt(hp  * 1.20f);
            }
            break;

        case CommanderId.Selena:
            // Ranged: +20% ATK
            if (!def.isSpecialCard && def.type == UnitType.Ranged)
                atk = Mathf.RoundToInt(atk * 1.20f);
            break;

        case CommanderId.Roxanne:
            // Air: +10% ATK/HP
            if (!def.isSpecialCard && def.type == UnitType.Air)
            {
                atk = Mathf.RoundToInt(atk * 1.10f);
                hp  = Mathf.RoundToInt(hp  * 1.10f);
            }
            break;

        case CommanderId.Hank:
            // Vehicles: +10% ATK (you said "non-ranged vehicles"; your UnitType enum
            // doesn't separate ranged vehicles, so this applies to Vehicle type)
            if (!def.isSpecialCard && def.type == UnitType.Vehicle)
                atk = Mathf.RoundToInt(atk * 1.10f);
            break;

        case CommanderId.Geralt:
            // Doesn't change card numbers; changes damage taken vs ranged
            dmgTakenVsRanged = 0.80f;
            break;
    }

    // Conditional display (terrain/weather) — only if there is a battlefield active
    // (You can later gate this with an IsInBattleContext bool if you want)
    if (Battlefield.Instance != null && !def.isSpecialCard)
    {
        if (cmd == CommanderId.Terra && cachedTerrain == Battlefield.TerrainType.Roads)
            atk = Mathf.RoundToInt(atk * 1.50f);

        if (cmd == CommanderId.Aqua && cachedWeather == Battlefield.WeatherType.Rain)
            atk = Mathf.RoundToInt(atk * 1.50f);
    }

    return new EffectiveCardStats
    {
        Attack = atk,
        HP = hp,
        Cost = Mathf.Max(0, cost),
        DamageTakenMultiplierVsRanged = dmgTakenVsRanged
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



    private float ComputeAttackMultiplier(Player owner, Unit unit)
    {
        // Always-on + type-based
        float m = ComputeAttackMultiplier_AlwaysOn(owner, unit);

        // Conditional (terrain/weather)
        m *= ComputeAttackMultiplier_Conditional(owner, unit);

        return Mathf.Max(0f, m);
    }

    private float ComputeAttackMultiplier_AlwaysOn(Player owner, Unit unit)
    {
        var cmd = GetCommander(owner);
        float m = 1f;

        switch (cmd)
        {
            case CommanderId.Albert:
                // All units 130% ATK
                m *= 1.30f;
                break;

            case CommanderId.Hank:
                // All NON-RANGED vehicles do 110% attack
                // In your codebase, Ranged is UnitType.Ranged (and Vehicle is UnitType.Vehicle).
                if (unit.Type == UnitType.Vehicle)
                    m *= 1.10f;
                break;

            case CommanderId.Aelin:
                // Infantry gain 20% hp and attack
                if (unit.Type == UnitType.Infantry)
                    m *= 1.20f;
                break;

            case CommanderId.Selena:
                // Ranged units gain 20% atk
                if (unit.Type == UnitType.Ranged)
                    m *= 1.20f;
                break;

            case CommanderId.Roxanne:
                // Air units have 10% more attack
                if (unit.Type == UnitType.Air)
                    m *= 1.10f;
                break;
        }

        return m;
    }

    private float ComputeAttackMultiplier_Conditional(Player owner, Unit unit)
    {
        var cmd = GetCommander(owner);
        float m = 1f;

        switch (cmd)
        {
            case CommanderId.Terra:
                // All units get 50% attack on Battlefield type "Roads"
                if (cachedTerrain == Battlefield.TerrainType.Roads)
                    m *= 1.50f;
                break;

            case CommanderId.Aqua:
                // All units get 50% more attack when weather is "Rain"
                if (cachedWeather == Battlefield.WeatherType.Rain)
                    m *= 1.50f;
                break;
        }

        return m;
    }

    private float ComputeHpMultiplier(Player owner, Unit unit)
    {
        var cmd = GetCommander(owner);
        float m = 1f;

        switch (cmd)
        {
            case CommanderId.Albert:
                // All units only 80% HP
                m *= 0.80f;
                break;

            case CommanderId.Aelin:
                // Infantry +20% HP
                if (unit.Type == UnitType.Infantry)
                    m *= 1.20f;
                break;

            case CommanderId.Roxanne:
                // Air +10% HP
                if (unit.Type == UnitType.Air)
                    m *= 1.10f;
                break;
        }

        return Mathf.Max(0.01f, m);
    }

    private int ComputeCostDelta(Player owner, UnitCard card)
    {
        var cmd = GetCommander(owner);

        // Roy: All units cost 1 less gold, forever
        if (cmd == CommanderId.Roy)
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

    // Geralt: take 20% less damage from Ranged attackers
    if (cmd == CommanderId.Geralt && attackerType == UnitType.Ranged)
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
