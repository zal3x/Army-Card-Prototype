using UnityEngine;

/// <summary>
/// ScriptableObject that defines a battlefield's configuration.
/// Controls which unit types are allowed and where they go.
///
/// BATTLEFIELD MODES:
/// - Standard: Infantry / Vehicle+Ranged / Air (default)
/// - GroundOnly: Infantry / Vehicle / Ranged (Air disabled)
///
/// Create assets via: Create > Army Prototype > Battlefield Definition
/// </summary>
[CreateAssetMenu(menuName = "Army Prototype/Battlefield Definition", fileName = "Battlefield_")]
public class BattlefieldDefinition : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Display name shown in UI")]
    public string displayName = "Unnamed Battlefield";

    [Header("Environment")]
    [Tooltip("Terrain type affects certain commander bonuses")]
    public Battlefield.TerrainType terrain = Battlefield.TerrainType.Plains;

    [Tooltip("Weather type affects certain abilities")]
    public Battlefield.WeatherType weather = Battlefield.WeatherType.Clear;

    [Header("Row Configuration")]
    [Tooltip("Controls which unit types can be played and where they go")]
    public BattlefieldRowMode rowMode = BattlefieldRowMode.Standard;

    // ========================================
    // DERIVED PROPERTIES
    // ========================================

    /// <summary>
    /// Returns true if Air units can be played on this battlefield.
    /// </summary>
    public bool IsAirAllowed => rowMode != BattlefieldRowMode.GroundOnly;

    /// <summary>
    /// Returns true if Ranged units have their own row (not shared with Vehicle).
    /// </summary>
    public bool IsRangedSeparate => rowMode == BattlefieldRowMode.GroundOnly;

    /// <summary>
    /// Returns true if Ranged units should be treated as "vehicle-ish" for abilities.
    /// On Standard: true (Ranged is in the vehicle group)
    /// On GroundOnly: false (Ranged is separate)
    /// </summary>
    public bool IsRangedVehicleish => rowMode == BattlefieldRowMode.Standard;

    // ========================================
    // ROW MAPPING
    // ========================================

    /// <summary>
    /// Returns which physical row a UnitType should go to on this battlefield.
    ///
    /// STANDARD MODE:
    /// - Infantry → Front
    /// - Vehicle → Middle (shared with Ranged)
    /// - Ranged → Middle (shared with Vehicle)
    /// - Air → Back
    ///
    /// GROUND-ONLY MODE:
    /// - Infantry → Front
    /// - Vehicle → Middle (alone)
    /// - Ranged → Back (alone, replaces Air)
    /// - Air → (blocked, but returns Back as fallback)
    /// </summary>
    public RowType GetRowForType(UnitType type)
    {
        return rowMode switch
        {
            BattlefieldRowMode.GroundOnly => type switch
            {
                UnitType.Infantry => RowType.Front,
                UnitType.Vehicle => RowType.Middle,
                UnitType.Ranged => RowType.Back,  // Ranged gets its own row!
                UnitType.Air => RowType.Back,     // (Air is blocked, but fallback)
                _ => RowType.Front
            },
            _ => type switch  // Standard mode (default)
            {
                UnitType.Infantry => RowType.Front,
                UnitType.Vehicle => RowType.Middle,
                UnitType.Ranged => RowType.Middle,  // Shares with Vehicle
                UnitType.Air => RowType.Back,
                _ => RowType.Front
            }
        };
    }

    /// <summary>
    /// Returns which UnitType occupies a physical row on this battlefield.
    /// Used for display/UI purposes.
    ///
    /// Note: On Standard mode, Middle row returns Vehicle (even though Ranged shares it).
    /// </summary>
    public UnitType GetPrimaryTypeForRow(RowType row)
    {
        return rowMode switch
        {
            BattlefieldRowMode.GroundOnly => row switch
            {
                RowType.Front => UnitType.Infantry,
                RowType.Middle => UnitType.Vehicle,
                RowType.Back => UnitType.Ranged,
                _ => UnitType.Infantry
            },
            _ => row switch  // Standard mode
            {
                RowType.Front => UnitType.Infantry,
                RowType.Middle => UnitType.Vehicle,  // Primary, Ranged shares
                RowType.Back => UnitType.Air,
                _ => UnitType.Infantry
            }
        };
    }

    /// <summary>
    /// Checks if a specific UnitType can be played on this battlefield.
    /// </summary>
    public bool IsTypeAllowed(UnitType type)
    {
        // Air is blocked on GroundOnly battlefields
        if (type == UnitType.Air && rowMode == BattlefieldRowMode.GroundOnly)
            return false;

        return true;
    }

    // ========================================
    // DEBUG
    // ========================================

    public override string ToString()
    {
        return $"Battlefield '{displayName}' ({rowMode})";
    }
}
