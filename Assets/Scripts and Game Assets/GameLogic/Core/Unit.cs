public class Unit
{
    // -------------------------
    // DEBUG / TRACKING
    // -------------------------
    // Gives every Unit instance a unique ID so logs can prove whether something
    // is truly a new unit (new ID) or the same unit being moved around.
    private static int _nextId = 1;
    
    public AbilityParams AbilityData; // copied from UnitCard

    // Read-only outside of this class, so other code can't accidentally change it.
    public int InstanceId { get; private set; }

    public string Name;
    public UnitType Type;

    // DEPRECATED: Unit HP is no longer used in the new combat system.
    // Combat is now based on total ATK comparison, not unit HP streaming.
    // Keeping these fields temporarily for compatibility.
    [System.Obsolete("Unit HP not used in new combat system")]
    public int HP;
    [System.Obsolete("Unit MaxHP not used in new combat system")]
    public int MaxHP;

    public UnitAbilityType Ability = UnitAbilityType.None; // copied from UnitCard
    public int ATK;

    // Changed from computed property (HP > 0) to settable flag.
    // In new system, units are marked dead after combat (IsAlive = false).
    private bool _isAlive = true;
    public bool IsAlive
    {
        get => _isAlive;
        set => _isAlive = value;
    }

    // Optional: unit can have a pre-emptive strike ability
    public PreemptiveStrikeData PreemptiveStrike;
    // Marks whether this unit is currently the "Target" mark victim.
    public bool IsMarkedTarget = false;

    // Some units may be "Ranged" for row placement,
// but still count as a Vehicle for certain abilities.
public bool CountsAsVehicle = false;

public UnitCard SourceCard { get; private set; }

public void SetSourceCard(UnitCard card)
{
    SourceCard = card;
}

/// <summary>
/// Defines an opening (pre-emptive) strike.
/// Example: AA unit hits Air units before main combat.
/// </summary>

    public Unit(
        string name,
        UnitType type,
        int hp,
        int atk,
        PreemptiveStrikeData preemptiveStrike = null,
        UnitAbilityType ability = UnitAbilityType.None,
        AbilityParams abilityData = default)
    {
        // Assign a unique ID for debugging.
        InstanceId = _nextId++;

        Name = name;
        Type = type;

        HP = hp;
        MaxHP = hp; // full health reference

        ATK = atk;

        PreemptiveStrike = preemptiveStrike;
        Ability = ability;
        AbilityData = abilityData;

    }
}


public class PreemptiveStrikeData
{
    public UnitType TargetType;
    public int Damage;
    public bool TargetBackRowOnly;

    public PreemptiveStrikeData(
        UnitType targetType,
        int damage,
        bool targetBackRowOnly = false)
    {
        TargetType = targetType;
        Damage = damage;
        TargetBackRowOnly = targetBackRowOnly;
    }
}


