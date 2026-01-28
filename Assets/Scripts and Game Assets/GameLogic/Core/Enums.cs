public enum UnitType {
    Infantry,
    Vehicle,
    Air,
    Ranged
}

public enum RowType {
    Front,
    Middle,
    Back
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

}

