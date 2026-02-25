public enum UnitType {
    Infantry,
    Vehicle,
    Air,
    Ranged
}

/// <summary>
/// Card rarity for the shop/drop system.
/// Default drop rates: Common 60%, Uncommon 20%, Rare 10%, Epic 7%, Legendary 3%
/// </summary>
public enum CardRarity
{
    Common,
    Uncommon,
    Rare,
    Epic,
    Legendary
}

public enum RowType {
    Front,
    Middle,
    Back
}

/// <summary>
/// Defines the row configuration mode for a battlefield.
/// Controls which unit types can be played and where they go.
/// </summary>
public enum BattlefieldRowMode
{
    /// <summary>
    /// Standard layout: Infantry / Vehicle+Ranged / Air
    /// - Front: Infantry
    /// - Middle: Vehicle AND Ranged (shared)
    /// - Back: Air
    /// </summary>
    Standard,

    /// <summary>
    /// Ground-only layout: Infantry / Vehicle / Ranged (Air disabled)
    /// - Front: Infantry
    /// - Middle: Vehicle only
    /// - Back: Ranged only
    /// - Air units CANNOT be played
    /// </summary>
    GroundOnly,

    // Future modes can be added here:
    // NavalOnly, AirSupremacy, etc.
}
// -------------------------
// UNIT ABILITIES (Feb 2026 Rework)
// -------------------------
// Abilities are processed in AbilitySystem.cs (ATK calculation) and CombatResolver.cs (combat effects)
public enum UnitAbilityType
{
    None,

    // ========================================
    // COMBAT ATK MODIFIERS
    // ========================================

    /// <summary>
    /// "3 of a Kind" - Double ATK if 3+ copies of this unit in the same row.
    /// </summary>
    ThreeOfAKind,

    /// <summary>
    /// "Unmatched" - Double ATK if only one of this exact unit on the entire field.
    /// </summary>
    Unmatched,

    /// <summary>
    /// "Armor Piercing" - Gains +1 ATK for each enemy ARMORED unit on the field.
    /// </summary>
    ArmorPiercing,

    /// <summary>
    /// "SOS" - Double ATK when owner's commander HP is below 20%.
    /// </summary>
    SOS,

    /// <summary>
    /// "Brother in Arms" - Double ATK if another copy of this unit is on the field.
    /// Both copies get the bonus.
    /// </summary>
    BrotherInArms,

    // ========================================
    // COMMANDER DAMAGE ABILITIES
    // ========================================

    /// <summary>
    /// "Direct Attack" - Deals flat 5 damage directly to enemy commander.
    /// </summary>
    DirectAttack,

    /// <summary>
    /// "Counterattack" - Deals 1 commander damage when this unit is destroyed.
    /// </summary>
    Counterattack,

    // ========================================
    // DEFENSIVE ABILITIES
    // ========================================

    /// <summary>
    /// "Armored" - Unit's ATK cannot be reduced by enemy abilities, doctrines, perks, or commander effects.
    /// </summary>
    Armored,

    /// <summary>
    /// DEPRECATED: Legacy enum value, do not use.
    /// </summary>
    [System.Obsolete("Use ArmorPiercing instead")]
    ArmorPiercing_DEPRECATED,

    // ========================================
    // ON-PLAY ABILITIES
    // ========================================

    /// <summary>
    /// "Mr. Moneybags" - Gain gold when played.
    /// </summary>
    MrMoneybags,

    /// <summary>
    /// "Duplicate" - Play another copy of this unit from deck if available.
    /// </summary>
    Duplicate,

    /// <summary>
    /// "Destroy Strongest Vehicle" - Destroys strongest enemy vehicle if their vehicle row is full.
    /// </summary>
    DestroyStrongestVehicle,

    /// <summary>
    /// "Taunt" - Lowers the ATK of a random enemy unit by 1 when played.
    /// </summary>
    Taunt,

    /// <summary>
    /// "Target Mark" - Destroys the weakest enemy unit (lowest ATK) when played.
    /// </summary>
    TargetMark,

    // ========================================
    // PERSISTENCE
    // ========================================

    /// <summary>
    /// "Persist" - Unit survives combat if owner won or tied the round.
    /// Without this, all units are destroyed after each combat.
    /// </summary>
    Persist,

    // ========================================
    // SPECIAL DISPLAY-ONLY TYPES
    // ========================================

    /// <summary>
    /// Used for floating text display when commander bonus activates.
    /// Not a real unit ability.
    /// </summary>
    CommanderBonusActivation,
}

