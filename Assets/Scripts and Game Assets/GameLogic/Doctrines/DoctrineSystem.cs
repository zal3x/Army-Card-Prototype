using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime system that applies doctrine effects during battle.
/// Follows the same static hooks pattern as CommanderBonus and SpecialCards.
/// </summary>
public class DoctrineSystem : MonoBehaviour
{
    public static DoctrineSystem Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Reference to player progress data")]
    [SerializeField]
    private PlayerProgressData playerProgress;

    // Static hooks for other systems to query doctrine effects
    public static Func<Player, int> GetStartingGoldBonus;
    public static Func<Player, UnitType, int> GetTypeCostReduction;
    public static Func<Player, UnitType, int> GetTypeAtkBonus;
    public static Func<Player, float> GetCommanderDamageMultiplier;
    public static Func<Player, int> GetExtraDrawCount;
    public static Func<Player, int> GetBonusGoldPerRound;

    // Per-battle tracking
    private Dictionary<Player, int> salvageGoldEarned = new Dictionary<Player, int>();
    private Dictionary<Player, bool> reinforcementsUsed = new Dictionary<Player, bool>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Register static hooks
        GetStartingGoldBonus = CalculateStartingGoldBonus;
        GetTypeCostReduction = CalculateTypeCostReduction;
        GetTypeAtkBonus = CalculateTypeAtkBonus;
        GetCommanderDamageMultiplier = CalculateCommanderDamageMultiplier;
        GetExtraDrawCount = CalculateExtraDrawCount;
        GetBonusGoldPerRound = CalculateBonusGoldPerRound;

        Debug.Log($"[DoctrineSystem] Initialized. PlayerProgress assigned: {playerProgress != null}");
    }

    private void Start()
    {
        if (playerProgress == null)
        {
            Debug.LogError("[DoctrineSystem] PlayerProgress is NOT assigned! Drag PlayerProgress.asset into the DoctrineSystem component.");
        }
        else
        {
            Debug.Log($"[DoctrineSystem] Ready. Equipped doctrines: {playerProgress.equippedDoctrines.Count}");
            foreach (var d in playerProgress.equippedDoctrines)
            {
                Debug.Log($"  - {d.displayName}: {d.description}");
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            GetStartingGoldBonus = null;
            GetTypeCostReduction = null;
            GetTypeAtkBonus = null;
            GetCommanderDamageMultiplier = null;
            GetExtraDrawCount = null;
            GetBonusGoldPerRound = null;
        }
    }

    /// <summary>
    /// Call at the start of each battle to reset per-battle tracking.
    /// </summary>
    public void OnBattleStart()
    {
        salvageGoldEarned.Clear();
        reinforcementsUsed.Clear();

        var progress = GetPlayerProgress();

        // Diagnostic: Log what doctrines we see at battle start
        Debug.Log($"[DoctrineSystem] OnBattleStart - playerProgress assigned: {progress != null}");
        if (progress != null)
        {
            Debug.Log($"[DoctrineSystem] OnBattleStart - Equipped doctrines: {progress.equippedDoctrines.Count}");
            foreach (var d in progress.equippedDoctrines)
            {
                Debug.Log($"  - {d?.displayName ?? "NULL"}: {d?.description ?? ""}");
            }
        }
        else
        {
            Debug.LogError("[DoctrineSystem] playerProgress is NULL at battle start! Doctrines will not work.");
        }
    }

    /// <summary>
    /// Get the effective PlayerProgressData reference.
    /// Uses Inspector-assigned reference first, falls back to singleton.
    /// </summary>
    private PlayerProgressData GetPlayerProgress()
    {
        if (playerProgress != null) return playerProgress;
        if (PlayerProgressData.Instance != null)
        {
            Debug.Log("[DoctrineSystem] Using PlayerProgressData.Instance as fallback (Inspector reference is null)");
            return PlayerProgressData.Instance;
        }
        return null;
    }

    /// <summary>
    /// Get the list of active doctrines for a player.
    /// For now, only the human player (Player A) has doctrines from PlayerProgressData.
    /// </summary>
    private List<DoctrineDefinition> GetActiveDoctrines(Player player)
    {
        var progress = GetPlayerProgress();

        // Only human player has persistent doctrines
        if (progress != null && player?.Name == "Player A")
        {
            return progress.equippedDoctrines;
        }
        return new List<DoctrineDefinition>();
    }

    private int CalculateStartingGoldBonus(Player player)
    {
        int bonus = 0;
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.StartingGoldBonus)
            {
                bonus += doctrine.effectAmount;
            }
        }
        return bonus;
    }

    private int CalculateTypeCostReduction(Player player, UnitType type)
    {
        int reduction = 0;
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.ReduceTypeCost &&
                doctrine.targetUnitType == type)
            {
                reduction += doctrine.effectAmount;
            }
        }
        return reduction;
    }

    private int CalculateTypeAtkBonus(Player player, UnitType type)
    {
        int bonus = 0;
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.TypeAtkBonus &&
                doctrine.targetUnitType == type)
            {
                bonus += doctrine.effectAmount;
            }
        }
        return bonus;
    }

    private float CalculateCommanderDamageMultiplier(Player player)
    {
        float multiplier = 1f;
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.CommanderDamageReduction)
            {
                // effectAmount is percentage (50 = half damage)
                multiplier *= doctrine.effectAmount / 100f;
            }
        }
        return multiplier;
    }

    private int CalculateExtraDrawCount(Player player)
    {
        int extra = 0;
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.ExtraDrawAtStart)
            {
                extra += doctrine.effectAmount;
            }
        }
        return extra;
    }

    private int CalculateBonusGoldPerRound(Player player)
    {
        int bonus = 0;
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.StartingGoldBonus)
            {
                // This doctrine gives gold every round, not just at start
                // Actually, per the doc it's "at start of each battle" so this is correct
            }
        }
        return bonus;
    }

    /// <summary>
    /// Check if player has Combined Arms doctrine and conditions are met.
    /// </summary>
    public int GetCombinedArmsBonus(Player player, UnitType unitType)
    {
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.TypeBonusWhenOtherTypePresent)
            {
                // Check if target type matches and required type is present
                if (doctrine.targetUnitType == unitType)
                {
                    // Check if player has required type on field
                    bool hasRequiredType = HasUnitTypeOnField(player, doctrine.requiredUnitType);
                    if (hasRequiredType)
                    {
                        return doctrine.effectAmount;
                    }
                }
            }
        }
        return 0;
    }

    private bool HasUnitTypeOnField(Player player, UnitType type)
    {
        if (player?.Rows == null) return false;

        foreach (var row in player.Rows.Values)
        {
            foreach (var unit in row.Units)
            {
                if (unit.Type == type && unit.IsAlive)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Called when a vehicle is destroyed. Awards gold if Salvage Wreckage is active.
    /// </summary>
    public void OnVehicleDestroyed(Player owner, Unit vehicle)
    {
        foreach (var doctrine in GetActiveDoctrines(owner))
        {
            if (doctrine.effectType == DoctrineEffectType.GoldOnVehicleDestroyed)
            {
                if (!salvageGoldEarned.ContainsKey(owner))
                    salvageGoldEarned[owner] = 0;

                if (salvageGoldEarned[owner] < doctrine.maxTriggersPerBattle)
                {
                    owner.Gold += doctrine.effectAmount;
                    salvageGoldEarned[owner]++;
                    Debug.Log($"[DoctrineSystem] Salvage Wreckage: +{doctrine.effectAmount} gold ({salvageGoldEarned[owner]}/{doctrine.maxTriggersPerBattle})");
                }
            }
        }
    }

    /// <summary>
    /// Check if Infantry Surge should spawn units at battle start.
    /// Returns the number of infantry to spawn.
    /// </summary>
    public int GetInfantrySurgeCount(Player player)
    {
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.StartWithUnitsOnField &&
                doctrine.targetUnitType == UnitType.Infantry)
            {
                return doctrine.effectAmount;
            }
        }
        return 0;
    }

    /// <summary>
    /// Check if Reinforcements doctrine should trigger.
    /// Returns true if should spawn an infantry unit.
    /// </summary>
    public bool ShouldSpawnReinforcement(Player player)
    {
        if (reinforcementsUsed.ContainsKey(player) && reinforcementsUsed[player])
            return false;

        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.SpawnOnArmyDefeated)
            {
                reinforcementsUsed[player] = true;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Check if SOS doctrine should revive a unit at the specified round.
    /// </summary>
    public bool ShouldReviveUnit(Player player, int currentRound)
    {
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.ReviveFromDiscard &&
                doctrine.triggerRound == currentRound)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Calculate leftover gold damage for Merchant of Death doctrine.
    /// </summary>
    public int GetLeftoverGoldDamage(Player player)
    {
        foreach (var doctrine in GetActiveDoctrines(player))
        {
            if (doctrine.effectType == DoctrineEffectType.LeftoverGoldDamage)
            {
                return player.Gold * doctrine.effectAmount;
            }
        }
        return 0;
    }
}
