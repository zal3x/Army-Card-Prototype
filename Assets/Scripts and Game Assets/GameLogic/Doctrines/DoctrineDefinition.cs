using UnityEngine;

/// <summary>
/// Types of effects that doctrines can provide.
/// Each doctrine has exactly one effect type.
/// </summary>
public enum DoctrineEffectType
{
    /// <summary>Military Industrial Complex: +X gold at start of each battle</summary>
    StartingGoldBonus,

    /// <summary>Motorhead/Birdwatcher: Reduce cost of specific unit type by X</summary>
    ReduceTypeCost,

    /// <summary>Merchant of Death: Spend leftover gold to attack commander</summary>
    LeftoverGoldDamage,

    /// <summary>Salvage Wreckage: Gain X gold per destroyed vehicle (max per battle)</summary>
    GoldOnVehicleDestroyed,

    /// <summary>Combined Arms: Type gets +X ATK when other type is present</summary>
    TypeBonusWhenOtherTypePresent,

    /// <summary>Infantry Surge: Start battle with X units on field</summary>
    StartWithUnitsOnField,

    /// <summary>Strategic Reserves: Draw X extra cards at game start</summary>
    ExtraDrawAtStart,

    /// <summary>Reinforcements: When army defeated, spawn unit before commander damage</summary>
    SpawnOnArmyDefeated,

    /// <summary>SOS: Revive X units from discard at specified round</summary>
    ReviveFromDiscard,

    /// <summary>Ranged Warrior/Close Combat/Iron Skin/Death from Above: Type gets +X ATK</summary>
    TypeAtkBonus,

    /// <summary>Thick Skin: Commander receives X% damage (e.g., 50 = half damage)</summary>
    CommanderDamageReduction,

    /// <summary>Priority Deployment: Select card, add copy of same type to hand</summary>
    DuplicateCardType
}

/// <summary>
/// ScriptableObject definition for a Doctrine.
/// Doctrines are purchased in the War Cabinet and equipped before battles (max 5).
/// </summary>
[CreateAssetMenu(menuName = "Army Prototype/Doctrine Definition", fileName = "Doctrine_")]
public class DoctrineDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Unique identifier for save/load")]
    public string doctrineId;

    [Tooltip("Display name shown in UI")]
    public string displayName;

    [TextArea(2, 6)]
    [Tooltip("Description of what this doctrine does")]
    public string description;

    [Header("Rarity & Cost")]
    [Tooltip("Determines drop rate in Chief of Staff shop")]
    public CardRarity rarity = CardRarity.Common;

    [Tooltip("Cost in War Funds to purchase")]
    public int baseCost = 100;

    [Header("Effect Configuration")]
    [Tooltip("The type of effect this doctrine provides")]
    public DoctrineEffectType effectType;

    [Tooltip("Primary effect amount (meaning varies by effect type)")]
    public int effectAmount;

    [Tooltip("For type-specific effects: which unit type is affected")]
    public UnitType targetUnitType;

    [Tooltip("For Combined Arms: which unit type must be present")]
    public UnitType requiredUnitType;

    [Tooltip("For effects with limits: max triggers per battle (0 = unlimited)")]
    public int maxTriggersPerBattle = 0;

    [Tooltip("For SOS: which round to trigger (0 = immediate)")]
    public int triggerRound = 0;

    [Header("Visual")]
    [Tooltip("Icon shown in doctrine slots and shop")]
    public Sprite iconSprite;

    /// <summary>
    /// Calculate the actual cost based on rarity multiplier.
    /// </summary>
    public int GetFinalCost(float rarityMultiplier = 1.5f)
    {
        float multiplier = rarity switch
        {
            CardRarity.Common => 1f,
            CardRarity.Uncommon => rarityMultiplier,
            CardRarity.Rare => rarityMultiplier * rarityMultiplier,
            CardRarity.Epic => rarityMultiplier * rarityMultiplier * rarityMultiplier,
            CardRarity.Legendary => rarityMultiplier * rarityMultiplier * rarityMultiplier * rarityMultiplier,
            _ => 1f
        };

        return Mathf.RoundToInt(baseCost * multiplier);
    }

    private void OnValidate()
    {
        // Auto-generate ID from name if empty
        if (string.IsNullOrEmpty(doctrineId) && !string.IsNullOrEmpty(displayName))
        {
            doctrineId = displayName.Replace(" ", "_").ToLower();
        }
    }
}
