using System.Collections.Generic;

public class Player
{
    public string Name;
    public int CommanderHP = 20;

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
}
