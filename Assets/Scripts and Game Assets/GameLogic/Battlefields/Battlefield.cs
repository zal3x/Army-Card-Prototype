using System;
using UnityEngine;

/// <summary>
/// Battlefield = authoritative source for the current battle's terrain + weather + row configuration.
/// CommanderBonus (and anything else) can subscribe to changes.
///
/// ROW CONFIGURATION:
/// The battlefield can be configured with different row modes via BattlefieldDefinition:
/// - Standard: Infantry / Vehicle+Ranged / Air
/// - GroundOnly: Infantry / Vehicle / Ranged (Air disabled)
/// </summary>
public class Battlefield : MonoBehaviour
{
    public enum TerrainType
    {
        Roads,
        Plains,
        Cities,
        Towns,
        Village,
        Mountains,
        Sea,
        Coastal
        // (more may be added later)
    }

    public enum WeatherType
    {
        Clear,
        Rain,
        Snow,
        Fog
    }

    // Simple singleton for "current battle" context.
    public static Battlefield Instance { get; private set; }

    public static event Action<TerrainType> OnTerrainChanged;
    public static event Action<WeatherType> OnWeatherChanged;
    public static event Action<BattlefieldRowMode> OnRowModeChanged;

    [Header("Battlefield Configuration")]
    [Tooltip("Optional: Assign a BattlefieldDefinition asset to configure row layout")]
    [SerializeField] private BattlefieldDefinition config;

    [Header("Battlefield State")]
    [SerializeField] private TerrainType terrain = TerrainType.Plains;
    [SerializeField] private WeatherType weather = WeatherType.Clear;

    public TerrainType Terrain => terrain;
    public WeatherType Weather => weather;

    /// <summary>
    /// The active battlefield configuration. May be null if no config assigned.
    /// </summary>
    public BattlefieldDefinition Config => config;

    /// <summary>
    /// Current row mode. Returns Standard if no config is assigned.
    /// </summary>
    public BattlefieldRowMode RowMode => config?.rowMode ?? BattlefieldRowMode.Standard;

    // ========================================
    // STATIC HELPER PROPERTIES
    // ========================================

    /// <summary>
    /// Returns true if Air units can be played on the current battlefield.
    /// Safe to call even if no Battlefield instance exists (returns true).
    /// </summary>
    public static bool IsAirAllowed => Instance?.config?.IsAirAllowed ?? true;

    /// <summary>
    /// Returns true if Ranged units have their own row (not shared with Vehicle).
    /// Safe to call even if no Battlefield instance exists (returns false).
    /// </summary>
    public static bool IsRangedSeparate => Instance?.config?.IsRangedSeparate ?? false;

    /// <summary>
    /// Returns true if Ranged should be treated as "vehicle-ish" for abilities.
    /// On Standard: true (Ranged shares with Vehicle)
    /// On GroundOnly: false (Ranged is distinct)
    /// </summary>
    public static bool IsRangedVehicleish => Instance?.config?.IsRangedVehicleish ?? true;

    /// <summary>
    /// Current row mode from static context. Returns Standard if no instance.
    /// </summary>
    public static BattlefieldRowMode CurrentRowMode => Instance?.RowMode ?? BattlefieldRowMode.Standard;

    // ========================================
    // STATIC HELPER METHODS
    // ========================================

    /// <summary>
    /// Returns which physical row a UnitType should go to on the current battlefield.
    /// Safe to call even if no Battlefield instance exists (uses Standard mapping).
    /// </summary>
    public static RowType GetRowForType(UnitType type)
    {
        if (Instance?.config != null)
            return Instance.config.GetRowForType(type);

        // Default to Standard mode mapping
        return type switch
        {
            UnitType.Infantry => RowType.Front,
            UnitType.Vehicle => RowType.Middle,
            UnitType.Ranged => RowType.Middle,
            UnitType.Air => RowType.Back,
            _ => RowType.Front
        };
    }

    /// <summary>
    /// Checks if a specific UnitType can be played on the current battlefield.
    /// Safe to call even if no Battlefield instance exists (returns true).
    /// </summary>
    public static bool IsTypeAllowed(UnitType type)
    {
        if (Instance?.config != null)
            return Instance.config.IsTypeAllowed(type);

        return true;  // Default: all types allowed
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        // Optional: keep across scenes if you want
        // DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Call this when you load a map / start a battle.
    /// </summary>
    public void SetTerrain(TerrainType newTerrain)
    {
        if (terrain == newTerrain) return;
        terrain = newTerrain;
        OnTerrainChanged?.Invoke(terrain);
        Debug.Log($"[BATTLEFIELD] Terrain set to {terrain}");
    }

    /// <summary>
    /// Call this when weather changes (or is assigned at battle start).
    /// </summary>
    public void SetWeather(WeatherType newWeather)
    {
        if (weather == newWeather) return;
        weather = newWeather;
        OnWeatherChanged?.Invoke(weather);
        Debug.Log($"[BATTLEFIELD] Weather set to {weather}");
    }
    public void Configure(TerrainType t, WeatherType w)
    {
        SetTerrain(t);
        SetWeather(w);
    }

    /// <summary>
    /// Loads a battlefield configuration from a BattlefieldDefinition asset.
    /// This applies terrain, weather, and row mode from the definition.
    /// </summary>
    public void LoadConfig(BattlefieldDefinition definition)
    {
        if (definition == null)
        {
            Debug.LogWarning("[BATTLEFIELD] LoadConfig called with null definition");
            return;
        }

        config = definition;
        SetTerrain(definition.terrain);
        SetWeather(definition.weather);

        OnRowModeChanged?.Invoke(definition.rowMode);
        Debug.Log($"[BATTLEFIELD] Loaded config: {definition.displayName} (Mode: {definition.rowMode})");
    }

    /// <summary>
    /// Clears the current configuration, resetting to Standard mode.
    /// </summary>
    public void ClearConfig()
    {
        if (config != null)
        {
            var oldMode = config.rowMode;
            config = null;

            if (oldMode != BattlefieldRowMode.Standard)
                OnRowModeChanged?.Invoke(BattlefieldRowMode.Standard);

            Debug.Log("[BATTLEFIELD] Config cleared, reset to Standard mode");
        }
    }
}

