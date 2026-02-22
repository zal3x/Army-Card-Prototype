using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Configuration for the War Cabinet shop system.
/// Defines drop rates, shop sizes, and item pools.
/// </summary>
[CreateAssetMenu(menuName = "Army Prototype/Shop Inventory Config", fileName = "ShopConfig")]
public class ShopInventoryConfig : ScriptableObject
{
    [Header("Drop Rates (must sum to 100)")]
    [Tooltip("Chance to roll Common rarity (default: 60%)")]
    [Range(0, 100)]
    public float commonDropRate = 60f;

    [Tooltip("Chance to roll Uncommon rarity (default: 20%)")]
    [Range(0, 100)]
    public float uncommonDropRate = 20f;

    [Tooltip("Chance to roll Rare rarity (default: 10%)")]
    [Range(0, 100)]
    public float rareDropRate = 10f;

    [Tooltip("Chance to roll Epic rarity (default: 7%)")]
    [Range(0, 100)]
    public float epicDropRate = 7f;

    [Tooltip("Chance to roll Legendary rarity (default: 3%)")]
    [Range(0, 100)]
    public float legendaryDropRate = 3f;

    [Header("Shop Sizes")]
    [Tooltip("Number of unit cards shown by Field Recruiter")]
    public int fieldRecruiterSlots = 5;

    [Tooltip("Number of special cards shown by Intelligence Officer")]
    public int intelligenceOfficerSlots = 5;

    [Tooltip("Number of doctrines shown by Chief of Staff")]
    public int chiefOfStaffSlots = 3;

    [Header("Card Pools")]
    [Tooltip("All unit cards available for purchase")]
    public List<UnitCardDefinition> unitCardPool = new List<UnitCardDefinition>();

    [Tooltip("All special cards available for purchase")]
    public List<UnitCardDefinition> specialCardPool = new List<UnitCardDefinition>();

    [Tooltip("All doctrines available for purchase")]
    public List<DoctrineDefinition> doctrinePool = new List<DoctrineDefinition>();

    [Tooltip("All perks available for purchase")]
    public List<PerkDefinition> perkPool = new List<PerkDefinition>();

    [Header("Pricing")]
    [Tooltip("Base cost for unit cards (before rarity multiplier)")]
    public int baseUnitCardCost = 50;

    [Tooltip("Base cost for special cards (before rarity multiplier)")]
    public int baseSpecialCardCost = 75;

    [Tooltip("Base cost for doctrines (before rarity multiplier)")]
    public int baseDoctrineCost = 100;

    [Tooltip("Price multiplier per rarity tier (1.5 = 50% more per tier)")]
    public float rarityPriceMultiplier = 1.5f;

    /// <summary>
    /// Roll a random rarity based on configured drop rates.
    /// </summary>
    public CardRarity RollRarity()
    {
        float roll = Random.Range(0f, 100f);

        if (roll < commonDropRate)
            return CardRarity.Common;

        roll -= commonDropRate;
        if (roll < uncommonDropRate)
            return CardRarity.Uncommon;

        roll -= uncommonDropRate;
        if (roll < rareDropRate)
            return CardRarity.Rare;

        roll -= rareDropRate;
        if (roll < epicDropRate)
            return CardRarity.Epic;

        return CardRarity.Legendary;
    }

    /// <summary>
    /// Get all cards from pool matching a specific rarity.
    /// </summary>
    public List<UnitCardDefinition> GetCardsByRarity(List<UnitCardDefinition> pool, CardRarity rarity)
    {
        return pool.FindAll(c => c.rarity == rarity);
    }

    /// <summary>
    /// Get all doctrines from pool matching a specific rarity.
    /// </summary>
    public List<DoctrineDefinition> GetDoctrinesByRarity(CardRarity rarity)
    {
        return doctrinePool.FindAll(d => d.rarity == rarity);
    }

    /// <summary>
    /// Calculate final price for a card based on its rarity.
    /// </summary>
    public int CalculateCardPrice(UnitCardDefinition card, bool isSpecial)
    {
        int basePrice = isSpecial ? baseSpecialCardCost : baseUnitCardCost;
        return CalculatePriceWithRarity(basePrice, card.rarity);
    }

    /// <summary>
    /// Calculate final price for a doctrine based on its rarity.
    /// </summary>
    public int CalculateDoctrinePrice(DoctrineDefinition doctrine)
    {
        return CalculatePriceWithRarity(baseDoctrineCost, doctrine.rarity);
    }

    private int CalculatePriceWithRarity(int basePrice, CardRarity rarity)
    {
        float multiplier = rarity switch
        {
            CardRarity.Common => 1f,
            CardRarity.Uncommon => rarityPriceMultiplier,
            CardRarity.Rare => rarityPriceMultiplier * rarityPriceMultiplier,
            CardRarity.Epic => Mathf.Pow(rarityPriceMultiplier, 3),
            CardRarity.Legendary => Mathf.Pow(rarityPriceMultiplier, 4),
            _ => 1f
        };

        return Mathf.RoundToInt(basePrice * multiplier);
    }

    /// <summary>
    /// Generate random shop items for a specific slot count.
    /// </summary>
    public List<ShopItem> GenerateUnitCardShop()
    {
        return GenerateShopItems(unitCardPool, fieldRecruiterSlots, isSpecial: false);
    }

    /// <summary>
    /// Generate random special card shop items.
    /// </summary>
    public List<ShopItem> GenerateSpecialCardShop()
    {
        return GenerateShopItems(specialCardPool, intelligenceOfficerSlots, isSpecial: true);
    }

    /// <summary>
    /// Generate random doctrine shop items.
    /// </summary>
    public List<DoctrineShopItem> GenerateDoctrineShop()
    {
        var items = new List<DoctrineShopItem>();

        for (int i = 0; i < chiefOfStaffSlots; i++)
        {
            CardRarity rarity = RollRarity();
            var candidates = GetDoctrinesByRarity(rarity);

            // Fallback to any rarity if none found
            if (candidates.Count == 0)
            {
                candidates = doctrinePool;
            }

            if (candidates.Count > 0)
            {
                var chosen = candidates[Random.Range(0, candidates.Count)];
                items.Add(new DoctrineShopItem
                {
                    doctrine = chosen,
                    price = CalculateDoctrinePrice(chosen)
                });
            }
        }

        return items;
    }

    private List<ShopItem> GenerateShopItems(List<UnitCardDefinition> pool, int count, bool isSpecial)
    {
        var items = new List<ShopItem>();

        for (int i = 0; i < count; i++)
        {
            CardRarity rarity = RollRarity();
            var candidates = GetCardsByRarity(pool, rarity);

            // Fallback to any rarity if none found
            if (candidates.Count == 0)
            {
                candidates = pool;
            }

            if (candidates.Count > 0)
            {
                var chosen = candidates[Random.Range(0, candidates.Count)];
                items.Add(new ShopItem
                {
                    card = chosen,
                    price = CalculateCardPrice(chosen, isSpecial)
                });
            }
        }

        return items;
    }

    private void OnValidate()
    {
        // Warn if rates don't sum to 100
        float sum = commonDropRate + uncommonDropRate + rareDropRate + epicDropRate + legendaryDropRate;
        if (Mathf.Abs(sum - 100f) > 0.1f)
        {
            Debug.LogWarning($"[ShopInventoryConfig] Drop rates sum to {sum}%, should be 100%");
        }
    }
}

/// <summary>
/// Represents a single item available for purchase in the shop.
/// </summary>
[System.Serializable]
public class ShopItem
{
    public UnitCardDefinition card;
    public int price;
}

/// <summary>
/// Represents a doctrine available for purchase.
/// </summary>
[System.Serializable]
public class DoctrineShopItem
{
    public DoctrineDefinition doctrine;
    public int price;
}
