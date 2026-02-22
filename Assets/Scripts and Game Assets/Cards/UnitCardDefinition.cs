using UnityEngine;

// NOTE: A ScriptableObject is an "editable data file" stored in your project.
// You can make one asset per card, tweak values in the Inspector, and no recompile needed.
[CreateAssetMenu(menuName = "Army Prototype/Card Definition", fileName = "Card_")]
public class UnitCardDefinition : ScriptableObject
{
    [Header("Identity")]
    public string cardName;

    [Header("Stats")]
    public UnitType type;
    public int cost;
    public int hp;
    public int atk;

    [Header("Rarity")]
    [Tooltip("Determines drop rate in shops and card value")]
    public CardRarity rarity = CardRarity.Common;

    [Header("Ability (simple keyword + parameters)")]
    public UnitAbilityType ability = UnitAbilityType.None;

    [Header("Classification Flags")]
    public bool countsAsVehicle = false; // Considers ranged units

    // NOTE: AbilityParams is defined in UnitCard.cs
    public AbilityParams abilityParams = new AbilityParams { Multiplier = 1f, Amount = 0 };

    // ===== Special Card Support =====
    [Header("Special Card (does NOT place a unit)")]
    public bool isSpecialCard = false;
    public SpecialCards.SpecialCardType specialType;

    [Header("Preemptive Strike (optional)")]
    public bool hasPreemptive = false;
    public UnitType preemptiveTargetType;
    public int preemptiveDamage;
    public bool targetBackRowOnly;

    [Header("World Visual")]
    public GameObject worldPrefabA;
    public GameObject worldPrefabB;

    // ===== Card Visual =====
    [Header("Card Visual")]
    [Tooltip("Portrait sprite for card (96x96 recommended). You can extract this from your world prefabs or create custom card art.")]
    public Sprite portraitSprite;

    [Tooltip("Optional: background/frame sprite for this card. If null, CardVisual will fall back to default/type tinting.")]
    public Sprite backgroundSprite;

    // ===== Card Text =====
    [Header("Card Text")]
    [TextArea(3, 8)]
    [Tooltip("Shown in the Ability text box. For special cards, this should describe what the card does.")]
    public string abilityDescription;

    public Player Owner;
public UnitCardDefinition Definition;


    // NOTE: This converts the editable ScriptableObject into your runtime UnitCard class.
    // Runtime objects are what the simulator actually uses.
    public UnitCard ToRuntimeCard()
    {
        PreemptiveStrikeData preemptive = null;

        if (hasPreemptive)
        {
            preemptive = new PreemptiveStrikeData(
                preemptiveTargetType,
                preemptiveDamage,
                targetBackRowOnly
            );
        }

        // IMPORTANT:
        // Use authored portrait if present, otherwise fall back to prefab extraction
        Sprite portraitToUse = GetFallbackPortrait();

        return new UnitCard(
            cardName,
            type,
            cost,
            hp,
            atk,
            preemptive,
            ability,
            abilityParams,
            portraitToUse,        // Portrait (with fallback)
            backgroundSprite,     // Background/frame
            abilityDescription,   // Ability text (special cards use this)
            isSpecialCard,
            specialType,
            owner: null,
            definition: this

        );
    }

    /// <summary>
    /// Tries to pull a portrait sprite from the world prefab if one wasn't authored.
    /// This keeps old cards working while you migrate to per-card portraits.
    /// </summary>
    private Sprite GetFallbackPortrait()
    {
        // Prefer explicitly authored portrait
        if (portraitSprite != null)
            return portraitSprite;

        // Fallback: try to extract from world prefab
        GameObject prefab = worldPrefabA != null ? worldPrefabA : worldPrefabB;
        if (prefab == null) return null;

        // Try SpriteRenderer first (common for 2D units)
        var sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.sprite;

        // Try Unity UI Image (if your prefab uses UI art)
        var img = prefab.GetComponentInChildren<UnityEngine.UI.Image>();
        if (img != null && img.sprite != null)
            return img.sprite;

        return null;
    }
}
