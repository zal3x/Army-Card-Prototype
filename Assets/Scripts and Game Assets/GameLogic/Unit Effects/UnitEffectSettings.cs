using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ScriptableObject that maps effect IDs to their visual parameters.
/// Create via Assets > Create > Army Card > Unit Effect Settings.
/// Place the asset in a Resources/ folder so it can be loaded at runtime.
/// </summary>
[CreateAssetMenu(menuName = "Army Card/Unit Effect Settings", fileName = "UnitEffectSettings")]
public class UnitEffectSettings : ScriptableObject
{
    // -------------------------------------------------------
    // Effect type flags (can combine: Outline | Glow)
    // -------------------------------------------------------
    [Flags]
    public enum UnitEffectType
    {
        None        = 0,
        Outline     = 1 << 0,   // Hard pixel outline via SpriteOutline shader (child renderer)
        Glow        = 1 << 1,   // Rim glow/pulse via RimPulse shader (MaterialPropertyBlock on main)
        Holographic = 1 << 2,   // Rainbow holographic shimmer via HoloFoilSoft shader (child renderer)
    }

    // -------------------------------------------------------
    // Per-effect definition (set up in Inspector)
    // -------------------------------------------------------
    [Serializable]
    public class EffectDefinition
    {
        [Tooltip("Unique string key used in code to identify this effect. E.g. 'BarbedWire', 'ThreeOfAKind'.")]
        public string effectId;

        [Tooltip("The color used for outline/glow. Blended with other active effects.")]
        public Color effectColor = Color.white;

        [Tooltip("Which visual layers to enable for this effect.")]
        public UnitEffectType effectTypes = UnitEffectType.Outline;

        [Tooltip("Intensity multiplier for glow and holographic amount. 0-1.")]
        [Range(0f, 1f)]
        public float intensity = 0.8f;

        [Tooltip("Outline pixel width (only used if Outline flag is set).")]
        [Range(1f, 8f)]
        public float outlineWidthPixels = 2f;

        [Tooltip("If true, the effect pulses over time.")]
        public bool pulsing;

        [Tooltip("Speed of pulse animation (used with RimPulse's PulseSpeed property).")]
        [Range(0f, 10f)]
        public float pulseSpeed = 3f;
    }

    [Tooltip("All defined effects. Each must have a unique effectId.")]
    public EffectDefinition[] effectDefinitions = Array.Empty<EffectDefinition>();

    // -------------------------------------------------------
    // Runtime lookup (built on first access)
    // -------------------------------------------------------
    private Dictionary<string, EffectDefinition> _lookup;

    public EffectDefinition Get(string id)
    {
        if (_lookup == null)
            BuildLookup();
        _lookup.TryGetValue(id, out var def);
        return def;
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, EffectDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in effectDefinitions)
        {
            if (!string.IsNullOrEmpty(def.effectId))
                _lookup[def.effectId] = def;
        }
    }

    private void OnEnable()
    {
        // Rebuild lookup when asset is loaded/reloaded
        _lookup = null;
    }
}
