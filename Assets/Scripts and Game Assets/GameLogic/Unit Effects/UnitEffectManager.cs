using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Static manager that applies / removes visual status effects on battlefield units.
///
/// Usage:
///   UnitEffectManager.ApplyEffect("BarbedWire", unit.InstanceId);
///   UnitEffectManager.RemoveEffect("BarbedWire", unit.InstanceId);
///   UnitEffectManager.ClearAllEffects();
///
/// Initialize must be called once (FieldSpawner does this in Awake).
/// Effect IDs must match entries in the UnitEffectSettings ScriptableObject.
/// </summary>
public static class UnitEffectManager
{
    private static FieldSpawner _fieldSpawner;

    // Tracks which unit IDs currently have a controller (so ClearAll is efficient)
    private static readonly HashSet<int> _trackedUnitIds = new HashSet<int>();

    // -------------------------------------------------------
    // Initialization (called by FieldSpawner.Awake)
    // -------------------------------------------------------

    public static void Initialize(FieldSpawner fieldSpawner)
    {
        _fieldSpawner = fieldSpawner;
        _trackedUnitIds.Clear();
    }

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    /// <summary>
    /// Activates a named effect on a single unit.
    /// If the unit visual doesn't exist yet, this is a silent no-op.
    /// </summary>
    public static void ApplyEffect(string effectId, int unitId)
    {
        var ctrl = GetOrCreateController(unitId);
        ctrl?.Apply(effectId);
    }

    /// <summary>
    /// Removes a specific named effect from a unit.
    /// </summary>
    public static void RemoveEffect(string effectId, int unitId)
    {
        var ctrl = GetExistingController(unitId);
        ctrl?.Remove(effectId);
    }

    /// <summary>
    /// Removes all active effects from a single unit.
    /// </summary>
    public static void ClearAllEffectsOnUnit(int unitId)
    {
        var ctrl = GetExistingController(unitId);
        ctrl?.ClearAll();
    }

    /// <summary>
    /// Removes all active effects from every tracked unit.
    /// Call at round start (SpecialCards.StartRound) and after combat playback finishes.
    /// </summary>
    public static void ClearAllEffects()
    {
        if (_fieldSpawner == null) return;
        foreach (int unitId in _trackedUnitIds)
        {
            var go = _fieldSpawner.GetUnitVisual(unitId);
            if (go == null) continue;
            var ctrl = go.GetComponent<UnitEffectController>();
            ctrl?.ClearAll();
        }
        // Keep the set populated — units may still be on the field
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    private static UnitEffectController GetOrCreateController(int unitId)
    {
        if (_fieldSpawner == null) return null;

        var go = _fieldSpawner.GetUnitVisual(unitId);
        if (go == null) return null;

        var ctrl = go.GetComponent<UnitEffectController>();
        if (ctrl == null)
            ctrl = UnitEffectController.InitializeOnUnit(go);

        if (ctrl != null)
            _trackedUnitIds.Add(unitId);

        return ctrl;
    }

    private static UnitEffectController GetExistingController(int unitId)
    {
        if (_fieldSpawner == null) return null;
        var go = _fieldSpawner.GetUnitVisual(unitId);
        return go != null ? go.GetComponent<UnitEffectController>() : null;
    }
}
