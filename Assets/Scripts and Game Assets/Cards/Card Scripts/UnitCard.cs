using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UnitCard is a template used in decks and hands.
/// When played, it creates a runtime Unit instance.
///
/// NOTE (important for Inspector):
/// - AbilityParams must be [System.Serializable] for Unity to show its fields in the Inspector
///   when used inside ScriptableObjects (like UnitCardDefinition).
/// - Fields must be public (or [SerializeField]) to show up.
/// </summary>
[System.Serializable]
public struct AbilityParams
{
    // Used for percentage / scaling effects (0.25f, 2.0f, 0.5f, etc.)
    public float Multiplier;

    // Used for flat amounts (ex: +1000 gold, +3 cards, etc.)
    public int Amount;
}

/// <summary>
/// Runtime card object used in decks/hands/combat.
/// Visual fields (Portrait/Background sprites) are copied from the definition at runtime.
/// </summary>
public class UnitCard
{
    // ===== Core Data =====
    public string Name;
    public UnitType Type;
    public int Cost;

    public int BaseHP;
    public int BaseATK;

    public PreemptiveStrikeData PreemptiveStrike;

    // Optional: ability keyword + parameters copied into Unit on creation
    public UnitAbilityType Ability = UnitAbilityType.None;
    public AbilityParams AbilityData;
    // Who this card belongs to (used for commander bonuses/UI display/cost checks)
public Player Owner;

// Which ScriptableObject this card came from (shared immutable base definition)
public UnitCardDefinition Definition;


    // ===== Visuals =====
    // Portrait sprite for card display (96x96 recommended)
    public Sprite PortraitSprite;

    // Optional: author-written description for the Ability text box.
// Special cards should use this heavily.
public string AbilityDescription;


    // Optional: background/frame sprite for this specific card.
    // If null, CardVisual should fall back to its type-based tinting/default prefab art.
    public Sprite BackgroundSprite;

    // ===== Special Card Support =====
    // If true: this card triggers an effect and does NOT place a unit.
    public bool IsSpecialCard;

    // Which special effect this card triggers (only meaningful if IsSpecialCard == true)
    public SpecialCards.SpecialCardType SpecialType;

    public UnitCard(
        string name,
        UnitType type,
        int cost,
        int hp,
        int atk,
        PreemptiveStrikeData preemptive = null,
        UnitAbilityType ability = UnitAbilityType.None,
        AbilityParams? abilityData = null,
        Sprite portraitSprite = null,
        Sprite backgroundSprite = null,
         // NEW:
    string abilityDescription = "",
        bool isSpecialCard = false,
        SpecialCards.SpecialCardType specialType = default,
        Player owner = null,
UnitCardDefinition definition = null

    )
    {
        Name = name;
        Type = type;
        Cost = cost;

        BaseHP = hp;
        BaseATK = atk;

        PreemptiveStrike = preemptive;

        Ability = ability;

        // NOTE: Default values if the card didn't supply any params.
        AbilityData = abilityData ?? new AbilityParams { Multiplier = 1f, Amount = 0 };

        PortraitSprite = portraitSprite;
        BackgroundSprite = backgroundSprite;
        // NEW
AbilityDescription = abilityDescription;

        IsSpecialCard = isSpecialCard;
        SpecialType = specialType;
        Owner = owner;
Definition = definition;

    }

    /// <summary>
    /// Creates a runtime Unit instance from this card.
    /// IMPORTANT: Special cards should NOT call this.
    ///
    /// ABILITY EFFECTS APPLIED HERE:
    /// - Armored (Armored): HP is doubled.
    ///   This replaces the complex damage reduction logic - now the unit
    ///   simply has more HP and takes damage normally.
    /// </summary>
    public Unit CreateUnitInstance()
    {
        if (IsSpecialCard)
        {
            Debug.LogError($"Tried to create a Unit from special card '{Name}'. Special cards should not place units.");
            return null;
        }

        int finalHP = BaseHP;

        // ARMORED: Double HP instead of complex damage reduction
        // This makes the effect visible on the health bar and simplifies combat
        if (Ability == UnitAbilityType.Armored)
        {
            finalHP = BaseHP * 2;
            Debug.Log($"[ARMORED] '{Name}' HP doubled: {BaseHP} -> {finalHP}");
        }

        Unit unit = new Unit(
            Name,
            Type,
            finalHP,
            BaseATK,
            PreemptiveStrike,
            Ability,
            AbilityData
        );
        unit.SetSourceCard(this);
        return unit;
    }

    /// <summary>
    /// Creates a safe copy of this UnitCard for UI-only displays (Radar/Recon).
    /// This prevents reveal UI from accidentally mutating real hand/deck data.
    /// </summary>
    public UnitCard Clone()
    {
        // Struct (AbilityParams) copies by value automatically.
        // Reference types (PreemptiveStrike, Sprite) are intentionally shared read-only.
        return new UnitCard(
            name: Name,
            type: Type,
            cost: Cost,
            hp: BaseHP,
            atk: BaseATK,
            preemptive: PreemptiveStrike,
            ability: Ability,
            abilityData: AbilityData,
            portraitSprite: PortraitSprite,
            backgroundSprite: BackgroundSprite,
                abilityDescription: AbilityDescription, // NEW

            isSpecialCard: IsSpecialCard,
            specialType: SpecialType,
            owner: Owner,
    definition: Definition
        );
    }
}
