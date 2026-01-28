using System;
using UnityEngine;

/// <summary>
/// Battlefield = authoritative source for the current battle's terrain + weather.
/// CommanderBonus (and anything else) can subscribe to changes.
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

    [Header("Battlefield State")]
    [SerializeField] private TerrainType terrain = TerrainType.Plains;
    [SerializeField] private WeatherType weather = WeatherType.Clear;

    public TerrainType Terrain => terrain;
    public WeatherType Weather => weather;

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

}
