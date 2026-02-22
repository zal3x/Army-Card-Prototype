using UnityEngine;

/// <summary>
/// Types of effects that perks can provide.
/// Perks are permanent upgrades purchased from the Defense Minister.
/// </summary>
public enum PerkEffectType
{
    /// <summary>Walls: +X wall defense per tier (absorbs damage before commander)</summary>
    WallDefense,

    /// <summary>Extra Lives: +X commander HP per tier</summary>
    CommanderHP,

    /// <summary>Medals: +X ATK/HP to random unit (infinite purchases, no stacking)</summary>
    RandomUnitStatBoost,

    /// <summary>Stopping Power/Piercing/Explosives/Heads Up: +X ATK per tier for type</summary>
    TypeAtkBonus,

    /// <summary>Purse: +X gold per round per tier</summary>
    GoldPerRound
}

/// <summary>
/// ScriptableObject definition for a Perk.
/// Perks are permanent upgrades with multiple tiers, purchased from the Defense Minister.
/// </summary>
[CreateAssetMenu(menuName = "Army Prototype/Perk Definition", fileName = "Perk_")]
public class PerkDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique identifier for save/load")]
    public string perkId;

    [Tooltip("Display name shown in UI")]
    public string displayName;

    [TextArea(2, 6)]
    [Tooltip("Description of what this perk does")]
    public string description;

    [Header("Tiers")]
    [Tooltip("Maximum tier this perk can reach (typically 5)")]
    public int maxTier = 5;

    [Tooltip("War Funds cost for each tier upgrade. Array length should equal maxTier.")]
    public int[] costPerTier = new int[] { 100, 200, 400, 800, 1600 };

    [Header("Effect Configuration")]
    [Tooltip("The type of effect this perk provides")]
    public PerkEffectType effectType;

    [Tooltip("Effect amount gained per tier")]
    public int effectAmountPerTier = 10;

    [Tooltip("For type-specific effects: which unit type is affected")]
    public UnitType targetUnitType;

    [Header("Special Flags")]
    [Tooltip("If true, can be purchased infinitely (like Medals)")]
    public bool infinitePurchases = false;

    [Header("Visual")]
    [Tooltip("Icon shown in perk panel")]
    public Sprite iconSprite;

    /// <summary>
    /// Get the cost to upgrade to a specific tier.
    /// </summary>
    /// <param name="tier">The tier to upgrade TO (1-indexed)</param>
    public int GetCostForTier(int tier)
    {
        if (tier < 1) return 0;
        if (tier > costPerTier.Length) return costPerTier[costPerTier.Length - 1];
        return costPerTier[tier - 1];
    }

    /// <summary>
    /// Get the total effect amount at a specific tier.
    /// </summary>
    public int GetEffectAtTier(int tier)
    {
        return effectAmountPerTier * tier;
    }

    /// <summary>
    /// Get description text for a specific tier.
    /// </summary>
    public string GetTierDescription(int tier)
    {
        int effect = GetEffectAtTier(tier);

        return effectType switch
        {
            PerkEffectType.WallDefense => $"+{effect} Wall Defense",
            PerkEffectType.CommanderHP => $"+{effect} Commander HP",
            PerkEffectType.RandomUnitStatBoost => $"+{effectAmountPerTier} ATK/HP to random unit",
            PerkEffectType.TypeAtkBonus => $"+{effect} {targetUnitType} ATK",
            PerkEffectType.GoldPerRound => $"+{effect} Gold per round",
            _ => $"+{effect}"
        };
    }

    private void OnValidate()
    {
        // Auto-generate ID from name if empty
        if (string.IsNullOrEmpty(perkId) && !string.IsNullOrEmpty(displayName))
        {
            perkId = displayName.Replace(" ", "_").ToLower();
        }

        // Ensure cost array matches max tier
        if (costPerTier == null || costPerTier.Length != maxTier)
        {
            int[] newCosts = new int[maxTier];
            for (int i = 0; i < maxTier; i++)
            {
                if (costPerTier != null && i < costPerTier.Length)
                    newCosts[i] = costPerTier[i];
                else
                    newCosts[i] = 100 * (int)Mathf.Pow(2, i); // Default: 100, 200, 400, 800, 1600
            }
            costPerTier = newCosts;
        }
    }
}
