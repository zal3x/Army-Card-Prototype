using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility for calculating pixel-perfect unit layouts.
/// Helps determine optimal scales and positions for your battlefield.
///
/// Usage:
/// 1. Attach to a GameObject or call static methods
/// 2. Use CalculateOptimalLayout() to get recommended settings
/// 3. Apply to your FieldSpawner spawn point positions
/// </summary>
public class PixelPerfectLayoutHelper : MonoBehaviour
{
    // ========================================
    // CONFIGURATION
    // ========================================

    [Header("Screen Settings")]
    public int screenWidth = 1920;
    public int screenHeight = 1080;
    public int pixelsPerUnit = 100;

    [Header("Unit Pixel Sizes (original art)")]
    public int infantryPixels = 256;
    public int vehiclePixels = 512;
    public int airPixels = 512;

    [Header("Layout Requirements")]
    public int unitsPerRow = 5;
    public int numberOfRows = 3;
    public float horizontalPaddingPercent = 0.05f; // 5% padding on each edge
    public float unitSpacingPercent = 0.02f; // 2% spacing between units

    [Header("Results (Read-Only)")]
    [SerializeField] private float recommendedInfantryScale;
    [SerializeField] private float recommendedVehicleScale;
    [SerializeField] private float recommendedAirScale;
    [SerializeField] private float unitDisplaySize;
    [SerializeField] private float unitSpacing;

    // ========================================
    // STATIC UTILITY METHODS
    // ========================================

    /// <summary>
    /// Valid pixel-perfect scales (powers of 2 fractions and integers)
    /// </summary>
    public static readonly float[] ValidScales = { 0.125f, 0.25f, 0.5f, 1f, 2f, 3f, 4f };

    /// <summary>
    /// Checks if a scale value is pixel-perfect.
    /// </summary>
    public static bool IsPixelPerfectScale(float scale)
    {
        // Check against known good values
        foreach (float valid in ValidScales)
        {
            if (Mathf.Approximately(scale, valid))
                return true;
        }

        // Also allow any integer >= 1
        if (scale >= 1f && Mathf.Approximately(scale, Mathf.Round(scale)))
            return true;

        return false;
    }

    /// <summary>
    /// Finds the nearest pixel-perfect scale to the target.
    /// </summary>
    public static float GetNearestPixelPerfectScale(float targetScale)
    {
        float nearest = 1f;
        float minDiff = float.MaxValue;

        foreach (float valid in ValidScales)
        {
            float diff = Mathf.Abs(targetScale - valid);
            if (diff < minDiff)
            {
                minDiff = diff;
                nearest = valid;
            }
        }

        // Check integers up to 4
        for (int i = 1; i <= 4; i++)
        {
            float diff = Mathf.Abs(targetScale - i);
            if (diff < minDiff)
            {
                minDiff = diff;
                nearest = i;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Calculates the world size of a sprite given its pixel size and scale.
    /// </summary>
    public static float CalculateWorldSize(int pixelSize, int ppu, float scale)
    {
        return ((float)pixelSize / ppu) * scale;
    }

    /// <summary>
    /// Calculates the ideal scale to achieve a target world size.
    /// </summary>
    public static float CalculateScaleForWorldSize(int pixelSize, int ppu, float targetWorldSize)
    {
        float baseWorldSize = (float)pixelSize / ppu;
        return targetWorldSize / baseWorldSize;
    }

    // ========================================
    // LAYOUT CALCULATION
    // ========================================

    [ContextMenu("Calculate Optimal Layout")]
    public void CalculateOptimalLayout()
    {
        LayoutResult result = CalculateLayout(
            screenWidth, screenHeight, pixelsPerUnit,
            infantryPixels, vehiclePixels, airPixels,
            unitsPerRow, horizontalPaddingPercent, unitSpacingPercent
        );

        // Store results
        recommendedInfantryScale = result.infantryScale;
        recommendedVehicleScale = result.vehicleScale;
        recommendedAirScale = result.airScale;
        unitDisplaySize = result.unitWorldSize;
        unitSpacing = result.unitSpacing;

        Debug.Log($"[PixelPerfectLayoutHelper] Optimal Layout Calculated:\n" +
                  $"  Infantry Scale: {result.infantryScale}x ({infantryPixels * result.infantryScale}px display)\n" +
                  $"  Vehicle Scale: {result.vehicleScale}x ({vehiclePixels * result.vehicleScale}px display)\n" +
                  $"  Air Scale: {result.airScale}x ({airPixels * result.airScale}px display)\n" +
                  $"  Unit World Size: {result.unitWorldSize} units\n" +
                  $"  Unit Spacing: {result.unitSpacing} units\n" +
                  $"  Pixel Perfect: {result.isPixelPerfect}");
    }

    public struct LayoutResult
    {
        public float infantryScale;
        public float vehicleScale;
        public float airScale;
        public float unitWorldSize;
        public float unitSpacing;
        public bool isPixelPerfect;

        public Vector3[] playerAPositions;
        public Vector3[] playerBPositions;
    }

    /// <summary>
    /// Calculates the optimal layout for the given parameters.
    /// </summary>
    public static LayoutResult CalculateLayout(
        int screenWidth, int screenHeight, int ppu,
        int infantryPixels, int vehiclePixels, int airPixels,
        int unitsPerRow, float paddingPercent, float spacingPercent)
    {
        LayoutResult result = new LayoutResult();

        // Calculate world dimensions
        float aspectRatio = (float)screenWidth / screenHeight;
        // Assume ortho size = 5.4 (giving 1080px at 100 PPU)
        float orthoSize = (float)screenHeight / ppu / 2f;
        float worldWidth = orthoSize * 2f * aspectRatio;
        float halfWidth = worldWidth / 2f; // Each player side

        // Calculate available space per side
        float padding = halfWidth * paddingPercent;
        float availableWidth = halfWidth - (padding * 2f);

        // Calculate spacing between units
        float totalSpacing = availableWidth * spacingPercent * (unitsPerRow - 1);
        float spaceForUnits = availableWidth - totalSpacing;
        float maxUnitWorldSize = spaceForUnits / unitsPerRow;

        result.unitSpacing = totalSpacing / (unitsPerRow - 1);

        // Find the largest pixel-perfect scale that fits
        // We want all units to be similar size, so use the largest sprite
        int maxPixels = Mathf.Max(infantryPixels, vehiclePixels, airPixels);
        float baseWorldSize = (float)maxPixels / ppu;

        // Find the best pixel-perfect scale
        float idealScale = maxUnitWorldSize / baseWorldSize;
        float bestScale = GetNearestPixelPerfectScale(idealScale);

        // If best scale is too large, go smaller
        while (baseWorldSize * bestScale > maxUnitWorldSize && bestScale > 0.125f)
        {
            // Find next smaller scale
            float current = bestScale;
            bestScale = 0.125f;
            foreach (float s in ValidScales)
            {
                if (s < current && s > bestScale)
                    bestScale = s;
            }
        }

        // Calculate scales for each unit type to achieve similar visual size
        float targetDisplaySize = maxPixels * bestScale;

        result.infantryScale = GetNearestPixelPerfectScale(targetDisplaySize / infantryPixels);
        result.vehicleScale = GetNearestPixelPerfectScale(targetDisplaySize / vehiclePixels);
        result.airScale = GetNearestPixelPerfectScale(targetDisplaySize / airPixels);

        // For air, use a smaller scale to show distance/sky perspective
        if (result.airScale == result.vehicleScale)
        {
            // Find next smaller pixel-perfect scale
            float currentAirScale = result.airScale;
            foreach (float s in ValidScales)
            {
                if (s < currentAirScale)
                {
                    result.airScale = s;
                    break;
                }
            }
        }

        result.unitWorldSize = ((float)vehiclePixels / ppu) * result.vehicleScale;

        result.isPixelPerfect =
            IsPixelPerfectScale(result.infantryScale) &&
            IsPixelPerfectScale(result.vehicleScale) &&
            IsPixelPerfectScale(result.airScale);

        // Calculate spawn positions (3 rows x unitsPerRow)
        result.playerAPositions = new Vector3[3 * unitsPerRow];
        result.playerBPositions = new Vector3[3 * unitsPerRow];

        float[] rowYs = CalculateRowYPositions(orthoSize, 3);
        float rowWidth = (unitsPerRow - 1) * (result.unitWorldSize + result.unitSpacing);

        for (int row = 0; row < 3; row++)
        {
            float y = rowYs[row];
            float startXA = -(halfWidth / 2f) - (rowWidth / 2f);
            float startXB = (halfWidth / 2f) - (rowWidth / 2f);

            for (int slot = 0; slot < unitsPerRow; slot++)
            {
                int idx = row * unitsPerRow + slot;
                float xOffset = slot * (result.unitWorldSize + result.unitSpacing);

                result.playerAPositions[idx] = new Vector3(startXA + xOffset, y, 0);
                result.playerBPositions[idx] = new Vector3(startXB + xOffset, y, 0);
            }
        }

        return result;
    }

    /// <summary>
    /// Calculates evenly-spaced row Y positions.
    /// </summary>
    public static float[] CalculateRowYPositions(float orthoSize, int numRows)
    {
        float[] positions = new float[numRows];
        float worldHeight = orthoSize * 2f;
        float padding = worldHeight * 0.1f; // 10% padding top/bottom
        float usableHeight = worldHeight - (padding * 2f);
        float rowSpacing = usableHeight / (numRows - 1);

        for (int i = 0; i < numRows; i++)
        {
            // Row 0 = bottom (front), Row 2 = top (back)
            positions[i] = -orthoSize + padding + (i * rowSpacing);
        }

        return positions;
    }

    // ========================================
    // RECOMMENDED CONFIGURATIONS
    // ========================================

    /// <summary>
    /// Returns a preset configuration for a compact layout (small units).
    /// </summary>
    public static (float inf, float veh, float air) GetCompactPreset()
    {
        // All units at 0.25x for maximum fit
        return (0.5f, 0.25f, 0.25f);
    }

    /// <summary>
    /// Returns a preset for a balanced layout (medium units).
    /// </summary>
    public static (float inf, float veh, float air) GetBalancedPreset()
    {
        // Infantry at 1x (256px), Vehicle at 0.5x (256px), Air at 0.25x (128px)
        return (1f, 0.5f, 0.25f);
    }

    /// <summary>
    /// Returns a preset for maximum visual impact (larger units, may overlap).
    /// </summary>
    public static (float inf, float veh, float air) GetImpactPreset()
    {
        // Larger units with intentional overlapping
        return (1f, 0.5f, 0.5f);
    }

    // ========================================
    // EDITOR HELPERS
    // ========================================

#if UNITY_EDITOR
    [ContextMenu("Log Current Camera Settings")]
    private void LogCameraSettings()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("No main camera found");
            return;
        }

        Debug.Log($"[Camera Settings]\n" +
                  $"  Orthographic: {cam.orthographic}\n" +
                  $"  Ortho Size: {cam.orthographicSize}\n" +
                  $"  Aspect: {cam.aspect}\n" +
                  $"  World Width: {cam.orthographicSize * 2f * cam.aspect}\n" +
                  $"  World Height: {cam.orthographicSize * 2f}");
    }

    [ContextMenu("Apply Compact Preset")]
    private void ApplyCompactPreset()
    {
        var preset = GetCompactPreset();
        recommendedInfantryScale = preset.inf;
        recommendedVehicleScale = preset.veh;
        recommendedAirScale = preset.air;
        Debug.Log($"Applied Compact Preset: Infantry={preset.inf}x, Vehicle={preset.veh}x, Air={preset.air}x");
    }

    [ContextMenu("Apply Balanced Preset")]
    private void ApplyBalancedPreset()
    {
        var preset = GetBalancedPreset();
        recommendedInfantryScale = preset.inf;
        recommendedVehicleScale = preset.veh;
        recommendedAirScale = preset.air;
        Debug.Log($"Applied Balanced Preset: Infantry={preset.inf}x, Vehicle={preset.veh}x, Air={preset.air}x");
    }

    private void OnValidate()
    {
        // Recalculate when values change in inspector
        if (Application.isPlaying) return;

        CalculateOptimalLayout();
    }
#endif
}
