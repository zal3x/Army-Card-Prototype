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
    // UNIFIED HEALTH SYSTEM HELPERS
    // ========================================

    /// <summary>
    /// Calculates total HP contribution from all alive units.
    /// </summary>
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

    /// <summary>
    /// Recalculates CommanderMaxHP based on surviving units.
    /// Call this after units are added or removed.
    /// </summary>
    public void RecalculateCommanderMaxHP()
    {
        CommanderMaxHP = CommanderBaseHP + TotalUnitHP;
    }

    /// <summary>
    /// Initializes the unified health system at battle start.
    /// Sets base HP and syncs max HP with current units.
    /// </summary>
    public void InitializeUnifiedHealth(int baseHP)
    {
        CommanderBaseHP = baseHP;
        CommanderHP = baseHP;
        RecalculateCommanderMaxHP();
    }

    /// <summary>
    /// Called when a unit is deployed. Expands the health pool.
    /// </summary>
    public void OnUnitDeployed(Unit unit)
    {
        if (unit == null) return;

        // Expand both current and max HP by unit's HP
        CommanderHP += unit.MaxHP;
        CommanderMaxHP += unit.MaxHP;
    }

    /// <summary>
    /// Called when a unit takes damage. Syncs damage to commander.
    /// </summary>
    public void OnUnitDamaged(int damageAmount)
    {
        CommanderHP -= damageAmount;
    }

    /// <summary>
    /// Called when a unit's MaxHP is reduced (e.g., marked).
    /// Shrinks the health pool by reducing both HP and MaxHP.
    /// This is different from damage - it reduces the total capacity.
    /// </summary>
    public void OnUnitMaxHPReduced(int amountReduced)
    {
        CommanderHP -= amountReduced;
        CommanderMaxHP -= amountReduced;
    }
}
