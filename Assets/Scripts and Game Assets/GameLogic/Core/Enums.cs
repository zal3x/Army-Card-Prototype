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
// UNIT ABILITIES (prototype)
// -------------------------
// These are simple "keywords" that modify how ATK is counted.
// Later you can expand this into a full ability system.
public enum UnitAbilityType
{
    None,

    // Combat-time abilities
    DoubleAttackIfThreeOfKindOnRow,
    DoubleAttackIfFullHealth,
    DoubleAttackIfEnemyHasTank,
    CommanderBypassDamage25Percent,
    ReduceIncomingDamage50Percent,

     // -------------------------
    // NEW ABILITIES (combat)
    // -------------------------

    
  
    // ARMOR PIERCING: If attacker has ANY unit with this alive,
    // it disables ReduceIncomingDamage50Percent on the defender.
    ArmorPiercing,

    // COUNTERATTACK: When damaged but not destroyed, deals 50% of HP damage dealt
    // back to the attacker as a second "counter damage stream".
    Counterattack,


    // On-Play abilities (trigger immediately when unit is bought + placed)
    // TAUNT (ROW): If defender has this unit alive in a row,
    // that ROW is attacked first when damage is applied to the army.


    // TARGET: If attacker has this unit alive, it marks ONE enemy unit.
    // Marked unit takes 2x HP damage from the stream (vulnerability).
    GainGoldOnPlay,
    DuplicateSelfFromDeckOnPlay,
    DestroyStrongestEnemyVehicleIfEnemyVehicleRowIsFullOnPlay,
    TauntRowFirst,
    TargetMark,

    // -------------------------
    // SPECIAL DISPLAY-ONLY TYPES
    // -------------------------
    // These aren't real unit abilities but are used for floating text display

    /// <summary>
    /// Used when a commander bonus (not a unit ability) triggers an ATK/HP bonus.
    /// This allows separate display settings in AbilityDisplaySettings.
    /// </summary>
    CommanderBonusActivation,
}

