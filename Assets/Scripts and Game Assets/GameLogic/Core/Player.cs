using System.Collections.Generic;

public class Player
{
    public string Name;

    // ========================================
    // UNIFIED COMMANDER HEALTH SYSTEM
    // ========================================
    // Total Army Health = CommanderBaseHP + Sum of all unit HP
    // When units are deployed, CommanderMaxHP expands
    // When units take damage, CommanderHP decreases in real-time
    // When units die, the lost HP is "locked in" as permanent damage
    // At end of combat, CommanderMaxHP shrinks to surviving units + base HP

    /// <summary>
    /// Current commander HP. Decreases when units take damage.
    /// </summary>
    public int CommanderHP = 20;

    /// <summary>
    /// Base commander HP without any units (starting health).
    /// </summary>
    public int CommanderBaseHP = 20;

    /// <summary>
    /// Maximum HP including all deployed units.
    /// Expands when units are played, shrinks when units die.
    /// </summary>
    public int CommanderMaxHP = 20;

public Dictionary<RowType, Row> Rows = new();

// Tracks where each semantic group currently lives.
// This must NOT be inferred from living units (rows can be empty).
// - Infantry cards always place into InfantryRow
// - Vehicle + Ranged cards always place into VehicleRow
// - Air cards always place into AirRow
public RowType InfantryRow = RowType.Front;
public RowType VehicleRow = RowType.Middle;
public RowType AirRow = RowType.Back;

    // Units removed from play (combat deaths OR destroyed by abilities) go here.
    public List<Unit> DiscardPile = new();

    // Economy + cards
    public int Gold = 0;
    public List<UnitCard> Hand = new();
    public SimpleDeck Deck;

    public Player(string name, int deckSeed = 123)
    {
        Name = name;

        Rows[RowType.Front] = new Row(RowType.Front);
        Rows[RowType.Middle] = new Row(RowType.Middle);
        Rows[RowType.Back] = new Row(RowType.Back);

        Deck = new SimpleDeck(deckSeed);
    }

    // ========================================
    // NEW COMBAT SYSTEM: Simple Commander HP
    // ========================================

    /// <summary>
    /// Apply damage directly to the commander.
    /// Used in new combat system where damage = ATK difference.
    /// </summary>
    public void TakeDamage(int amount)
    {
        CommanderHP = UnityEngine.Mathf.Max(0, CommanderHP - amount);
    }

    /// <summary>
    /// Check if commander is dead (HP <= 0).
    /// </summary>
    public bool IsCommanderDead => CommanderHP <= 0;

    /// <summary>
    /// Initialize commander HP at battle start.
    /// In new system, MaxHP equals BaseHP (no unit HP expansion).
    /// </summary>
    public void InitializeCommanderHP(int baseHP)
    {
        CommanderBaseHP = baseHP;
        CommanderHP = baseHP;
        CommanderMaxHP = baseHP;
    }

    // ========================================
    // DEPRECATED: Unified Health System
    // These methods are no longer used but kept for compatibility.
    // ========================================

    [System.Obsolete("Not used in new combat system - units no longer have HP")]
    public int TotalUnitHP
    {
        get
        {
            int total = 0;
            foreach (var row in Rows.Values)
            {
                foreach (var unit in row.Units)
                {
                    if (unit != null && unit.IsAlive)
                        total += unit.MaxHP;
                }
            }
            return total;
        }
    }

    [System.Obsolete("Not used in new combat system")]
    public void RecalculateCommanderMaxHP()
    {
        CommanderMaxHP = CommanderBaseHP + TotalUnitHP;
    }

    [System.Obsolete("Use InitializeCommanderHP instead")]
    public void InitializeUnifiedHealth(int baseHP)
    {
        CommanderBaseHP = baseHP;
        CommanderHP = baseHP;
        RecalculateCommanderMaxHP();
    }

    [System.Obsolete("Not used in new combat system - units no longer expand health pool")]
    public void OnUnitDeployed(Unit unit)
    {
        if (unit == null) return;
        CommanderHP += unit.MaxHP;
        CommanderMaxHP += unit.MaxHP;
    }

    [System.Obsolete("Not used in new combat system")]
    public void OnUnitDamaged(int damageAmount)
    {
        CommanderHP -= damageAmount;
    }

    [System.Obsolete("Not used in new combat system")]
    public void OnUnitMaxHPReduced(int amountReduced)
    {
        CommanderHP -= amountReduced;
        CommanderMaxHP -= amountReduced;
    }
}
