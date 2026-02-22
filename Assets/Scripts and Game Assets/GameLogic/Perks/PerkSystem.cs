using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime system that applies perk effects during battle.
/// Perks are permanent upgrades that persist across battles.
/// </summary>
public class PerkSystem : MonoBehaviour
{
    public static PerkSystem Instance { get; private set; }

    [Header("Configuration")]
    [Tooltip("Reference to player progress data")]
    [SerializeField]
    private PlayerProgressData playerProgress;

    // Static hooks for other systems to query perk effects
    public static Func<Player, int> GetBonusCommanderHP;
    public static Func<Player, int> GetWallDefense;
    public static Func<Player, int> GetBonusGoldPerRound;
    public static Func<Player, UnitType, int> GetTypeAtkBonus;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Register static hooks
        GetBonusCommanderHP = CalculateBonusCommanderHP;
        GetWallDefense = CalculateWallDefense;
        GetBonusGoldPerRound = CalculateBonusGoldPerRound;
        GetTypeAtkBonus = CalculateTypeAtkBonus;

        Debug.Log($"[PerkSystem] Initialized. PlayerProgress assigned: {playerProgress != null}");
    }

    private void Start()
    {
        if (playerProgress == null)
        {
            Debug.LogError("[PerkSystem] PlayerProgress is NOT assigned! Drag PlayerProgress.asset into the PerkSystem component.");
        }
        else
        {
            Debug.Log($"[PerkSystem] Ready. Perk levels: {playerProgress.perkLevels.Count}, Bonus HP: {playerProgress.bonusCommanderHP}, Wall: {playerProgress.wallDefense}");
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            GetBonusCommanderHP = null;
            GetWallDefense = null;
            GetBonusGoldPerRound = null;
            GetTypeAtkBonus = null;
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
            Debug.Log("[PerkSystem] Using PlayerProgressData.Instance as fallback (Inspector reference is null)");
            return PlayerProgressData.Instance;
        }
        return null;
    }

    /// <summary>
    /// Check if this is the human player who has perks.
    /// </summary>
    private bool IsHumanPlayer(Player player)
    {
        return player?.Name == "Player A";
    }

    /// <summary>
    /// Get the tier of a specific perk for the human player.
    /// </summary>
    private int GetPerkTier(PerkEffectType effectType, UnitType? targetType = null)
    {
        var progress = GetPlayerProgress();
        if (progress == null) return 0;

        foreach (var perkProgress in progress.perkLevels)
        {
            if (perkProgress.perk == null) continue;

            if (perkProgress.perk.effectType == effectType)
            {
                // For type-specific perks, also check target type
                if (targetType.HasValue && perkProgress.perk.targetUnitType != targetType.Value)
                    continue;

                return perkProgress.currentTier;
            }
        }
        return 0;
    }

    /// <summary>
    /// Get the effect amount for a perk at the current tier.
    /// </summary>
    private int GetPerkEffect(PerkEffectType effectType, UnitType? targetType = null)
    {
        var progress = GetPlayerProgress();
        if (progress == null) return 0;

        foreach (var perkProgress in progress.perkLevels)
        {
            if (perkProgress.perk == null) continue;

            if (perkProgress.perk.effectType == effectType)
            {
                if (targetType.HasValue && perkProgress.perk.targetUnitType != targetType.Value)
                    continue;

                return perkProgress.perk.effectAmountPerTier * perkProgress.currentTier;
            }
        }
        return 0;
    }

    private int CalculateBonusCommanderHP(Player player)
    {
        if (!IsHumanPlayer(player)) return 0;
        return GetPerkEffect(PerkEffectType.CommanderHP);
    }

    private int CalculateWallDefense(Player player)
    {
        if (!IsHumanPlayer(player)) return 0;
        return GetPerkEffect(PerkEffectType.WallDefense);
    }

    private int CalculateBonusGoldPerRound(Player player)
    {
        if (!IsHumanPlayer(player)) return 0;
        return GetPerkEffect(PerkEffectType.GoldPerRound);
    }

    private int CalculateTypeAtkBonus(Player player, UnitType type)
    {
        if (!IsHumanPlayer(player)) return 0;
        return GetPerkEffect(PerkEffectType.TypeAtkBonus, type);
    }

    /// <summary>
    /// Apply perk bonuses to a player at the start of battle.
    /// NOTE: HP bonus is now calculated separately in BattleTestRunner.InitializePlayers()
    /// to avoid double-application. Use ApplyNonHPBattleStartBonuses() instead.
    /// </summary>
    [System.Obsolete("Use ApplyNonHPBattleStartBonuses instead - HP is now calculated in InitializePlayers")]
    public void ApplyBattleStartBonuses(Player player)
    {
        ApplyNonHPBattleStartBonuses(player);
    }

    /// <summary>
    /// Apply non-HP perk bonuses to a player at the start of battle.
    /// HP bonus is calculated separately in BattleTestRunner.InitializePlayers() to avoid double-application.
    /// </summary>
    public void ApplyNonHPBattleStartBonuses(Player player)
    {
        var progress = GetPlayerProgress();

        // Diagnostic: Log perk state at battle start
        Debug.Log($"[PerkSystem] ApplyNonHPBattleStartBonuses - playerProgress assigned: {progress != null}");
        if (progress != null)
        {
            Debug.Log($"[PerkSystem] Perk levels: {progress.perkLevels.Count}");
            foreach (var p in progress.perkLevels)
            {
                Debug.Log($"  - {p.perk?.displayName ?? "NULL"} Tier {p.currentTier}");
            }
            Debug.Log($"[PerkSystem] bonusCommanderHP: {progress.bonusCommanderHP}, wallDefense: {progress.wallDefense}");
        }
        else
        {
            Debug.LogError("[PerkSystem] playerProgress is NULL at battle start! Perks will not work.");
        }

        if (!IsHumanPlayer(player) || progress == null) return;

        // NOTE: HP bonus is NOT applied here - it's calculated in InitializePlayers()
        // to ensure the HUD is initialized with the correct value

        // Reset wall defense for battle
        progress.ResetWallDefenseForBattle();
        if (progress.currentWallDefense > 0)
        {
            Debug.Log($"[PerkSystem] Wall defense active: {progress.currentWallDefense}");
        }
    }

    /// <summary>
    /// Apply wall damage absorption. Returns remaining damage after wall absorbs.
    /// </summary>
    public int ApplyWallDamage(Player player, int incomingDamage)
    {
        var progress = GetPlayerProgress();
        if (!IsHumanPlayer(player) || progress == null) return incomingDamage;

        if (progress.currentWallDefense <= 0) return incomingDamage;

        int absorbed = Mathf.Min(incomingDamage, progress.currentWallDefense);
        progress.currentWallDefense -= absorbed;
        int remaining = incomingDamage - absorbed;

        if (absorbed > 0)
        {
            Debug.Log($"[PerkSystem] Wall absorbed {absorbed} damage. Wall remaining: {progress.currentWallDefense}");
        }

        return remaining;
    }

    /// <summary>
    /// Apply Medals perk bonus to a random unit in the deck.
    /// Called when player purchases the Medals perk.
    /// </summary>
    public void ApplyMedalBonus()
    {
        var progress = GetPlayerProgress();
        if (progress == null) return;

        // Find all cards that haven't received a medal yet
        var eligibleCards = new List<OwnedCard>();

        foreach (var card in progress.ownedCards)
        {
            if (card.bonusAtk == 0 && card.bonusHp == 0)
                eligibleCards.Add(card);
        }

        foreach (var card in progress.currentDeck)
        {
            if (card.bonusAtk == 0 && card.bonusHp == 0)
                eligibleCards.Add(card);
        }

        if (eligibleCards.Count == 0)
        {
            Debug.Log("[PerkSystem] No eligible cards for Medals bonus (all cards already have medals)");
            return;
        }

        // Pick a random card and apply +1/+1
        var chosen = eligibleCards[UnityEngine.Random.Range(0, eligibleCards.Count)];
        chosen.bonusAtk += 1;
        chosen.bonusHp += 1;

        Debug.Log($"[PerkSystem] Medals: Applied +1/+1 to {chosen.definition?.cardName ?? "unknown"}");
    }

    /// <summary>
    /// Get total ATK bonus for a unit from all sources (perks + doctrines).
    /// </summary>
    public int GetTotalAtkBonus(Player player, Unit unit)
    {
        int bonus = 0;

        // Perk bonus
        bonus += CalculateTypeAtkBonus(player, unit.Type);

        // Doctrine bonus (from DoctrineSystem)
        if (DoctrineSystem.Instance != null)
        {
            bonus += DoctrineSystem.GetTypeAtkBonus?.Invoke(player, unit.Type) ?? 0;
            bonus += DoctrineSystem.Instance.GetCombinedArmsBonus(player, unit.Type);
        }

        return bonus;
    }
}
