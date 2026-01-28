using UnityEngine;

public class UnitWorldHandle : MonoBehaviour
{
    public int UnitId { get; private set; }
    public PlayerSide Side { get; private set; }

    // Semantic unit type (Infantry / Vehicle / Air)
    public UnitType UnitType { get; private set; }

    public void Init(int unitId, PlayerSide side, UnitType unitType)
    {
        UnitId = unitId;
        Side = side;
        UnitType = unitType;
    }
}
