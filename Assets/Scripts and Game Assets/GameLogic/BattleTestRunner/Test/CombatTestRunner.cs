using UnityEngine;

public class CombatTestRunner : MonoBehaviour
{
    void Start()
    {
        Player playerA = new Player("Player A");
        Player playerB = new Player("Player B");

        // =========================
        // PLAYER A SETUP (uses indices)
        // =========================

        // Front row (4 units -> indices 0..3 are meaningful)
        PlaceAt(playerA, RowType.Front, 0, new Unit("A Infantry 1", UnitType.Infantry, 5, 10));
        PlaceAt(playerA, RowType.Front, 1, new Unit("A Mech",       UnitType.Infantry, 6, 4));
        PlaceAt(playerA, RowType.Front, 2, new Unit("A Marines",    UnitType.Infantry, 6, 6));
        PlaceAt(playerA, RowType.Front, 3, new Unit("A SEAL",       UnitType.Infantry, 7, 6));

        // Middle row (3 units)
        PlaceAt(playerA, RowType.Middle, 0, new Unit("A Tank",      UnitType.Vehicle, 20, 10));
        PlaceAt(playerA, RowType.Middle, 1, new Unit("A Rockets",   UnitType.Ranged,  5, 10));
        
        // AA unit with PRE-EMPTIVE STRIKE: hits first enemy Air unit in back row for 8
        PlaceAt(playerA, RowType.Middle, 2, new Unit(
            "A AA Missiles",
            UnitType.Ranged,
            12,
            6,
            new PreemptiveStrikeData(UnitType.Air, damage: 12, targetBackRowOnly: true)
        ));

        // =========================
        // PLAYER B SETUP (uses indices)
        // =========================

        // Front row (3 units)
        PlaceAt(playerB, RowType.Front, 0, new Unit("B Marines 1",  UnitType.Infantry, 6, 6));
        PlaceAt(playerB, RowType.Front, 1, new Unit("B Infantry 2", UnitType.Infantry, 5, 10));
        PlaceAt(playerB, RowType.Front, 2, new Unit("B RPG",        UnitType.Infantry, 5, 4));

        // Back row (2 air units)
        PlaceAt(playerB, RowType.Back, 0, new Unit("B Helicopter",  UnitType.Air, 10, 10));
        PlaceAt(playerB, RowType.Back, 1, new Unit("B Bomber",      UnitType.Air, 5, 15));

        // =========================
        // RUN COMBAT
        // =========================

        CombatReport report = CombatResolver.ResolveCombat(playerA, playerB);
        GetComponent<CombatPlayback>().Play(report);


    }

    /// <summary>
    /// Places a unit at an explicit index in the row.
    /// Index 0 = first to take damage.
    /// Index Count = last (protected behind others).
    ///
    /// Note: If index is larger than Count, it will clamp to Count (insert at end).
    /// </summary>
    private void PlaceAt(Player player, RowType row, int index, Unit unit)
    {
        var list = player.Rows[row].Units;

        // Clamp index so we never crash
        if (index < 0) index = 0;
        if (index > list.Count) index = list.Count;

        list.Insert(index, unit);
    }
}
