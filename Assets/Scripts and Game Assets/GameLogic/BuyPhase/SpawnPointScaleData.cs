using UnityEngine;

/// <summary>
/// Stores scale information for a spawn point.
/// Added by BattlefieldGridTool to spawn point objects.
/// Used by FieldSpawner to apply scale to spawned unit instances.
/// </summary>
public class SpawnPointScaleData : MonoBehaviour
{
    [Tooltip("Scale multiplier to apply to units spawned at this point")]
    public float scaleMultiplier = 1.0f;
}
