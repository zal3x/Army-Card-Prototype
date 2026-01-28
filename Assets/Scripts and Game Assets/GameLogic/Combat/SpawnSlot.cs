using UnityEngine;

public class SpawnSlot : MonoBehaviour
{
    public int OccupantUnitId = -1; // -1 means empty
    public bool IsEmpty => OccupantUnitId < 0;

    public void Claim(int unitId) => OccupantUnitId = unitId;
    public void Clear() => OccupantUnitId = -1;
}
