using UnityEngine;
using System;

/// <summary>
/// AbilityDisplaySettings: ScriptableObject for configuring floating text displays for abilities.
///
/// SETUP:
/// 1. Right-click in Project window > Create > Army Card > Ability Display Settings
/// 2. Configure the display text for each ability type
/// 3. Assign to DamageNumberSpawner in scene
///
/// USAGE:
/// Each ability can have custom floating text (e.g., "x2 ATK" for double attack abilities)
/// </summary>
[CreateAssetMenu(fileName = "AbilityDisplaySettings", menuName = "Army Card/Ability Display Settings")]
public class AbilityDisplaySettings : ScriptableObject
{
    [Serializable]
    public class AbilityDisplayEntry
    {
        [Tooltip("The ability type this display is for")]
        public UnitAbilityType abilityType;

        [Tooltip("The text to display (e.g., 'x2 ATK', '+5 ATK')")]
        public string displayText = "x2 ATK";

        [Tooltip("Color for the floating text")]
        public Color textColor = new Color(0.3f, 1f, 0.3f); // Default green

        [Tooltip("Font size multiplier (1.0 = normal)")]
        public float fontSizeMultiplier = 1.1f;
    }

    [Header("Ability Display Entries")]
    [Tooltip("Configure display text for each ability type")]
    public AbilityDisplayEntry[] abilityDisplays = new AbilityDisplayEntry[]
    {
        new AbilityDisplayEntry { abilityType = UnitAbilityType.ThreeOfAKind, displayText = "x2 ATK" },
        new AbilityDisplayEntry { abilityType = UnitAbilityType.Unmatched, displayText = "x2 ATK" },
        new AbilityDisplayEntry { abilityType = UnitAbilityType.ArmorPiercing, displayText = "x2 ATK" },
        new AbilityDisplayEntry { abilityType = UnitAbilityType.Armored, displayText = "ARMORED", textColor = new Color(0.5f, 0.5f, 1f) },
        new AbilityDisplayEntry { abilityType = UnitAbilityType.Duplicate, displayText = "DUPLICATE", textColor = new Color(0.3f, 1f, 0.8f) },
        new AbilityDisplayEntry { abilityType = UnitAbilityType.TargetMark, displayText = "MARKED", textColor = new Color(1f, 0.3f, 0.3f) },
        new AbilityDisplayEntry { abilityType = UnitAbilityType.MrMoneybags, displayText = "+GOLD", textColor = new Color(1f, 0.85f, 0.1f) },
        new AbilityDisplayEntry { abilityType = UnitAbilityType.DirectAttack, displayText = "BYPASS", textColor = new Color(1f, 0.5f, 0f) }, // Orange
        new AbilityDisplayEntry { abilityType = UnitAbilityType.Counterattack, displayText = "COUNTER", textColor = new Color(0.8f, 0.2f, 0.8f) }, // Purple
        // NOTE: Commander bonuses are NOT abilities - they are permanent stat changes applied at battle start.
        // Do not add CommanderBonusActivation here.
    };

    [Header("Default Settings")]
    [Tooltip("Default text if ability not found in list")]
    public string defaultDisplayText = "ABILITY";

    [Tooltip("Default color if ability not found")]
    public Color defaultColor = new Color(0.3f, 1f, 0.3f);

    [Tooltip("Default font size multiplier")]
    public float defaultFontSizeMultiplier = 1.1f;

    /// <summary>
    /// Gets the display settings for a specific ability type.
    /// Returns null if not found (use defaults).
    /// </summary>
    public AbilityDisplayEntry GetDisplayForAbility(UnitAbilityType abilityType)
    {
        if (abilityDisplays == null) return null;

        foreach (var entry in abilityDisplays)
        {
            if (entry.abilityType == abilityType)
                return entry;
        }

        return null;
    }

    /// <summary>
    /// Gets the display text for a specific ability type.
    /// Returns default text if not configured.
    /// </summary>
    public string GetDisplayText(UnitAbilityType abilityType)
    {
        var entry = GetDisplayForAbility(abilityType);
        return entry != null ? entry.displayText : defaultDisplayText;
    }

    /// <summary>
    /// Gets the color for a specific ability type.
    /// Returns default color if not configured.
    /// </summary>
    public Color GetColor(UnitAbilityType abilityType)
    {
        var entry = GetDisplayForAbility(abilityType);
        return entry != null ? entry.textColor : defaultColor;
    }

    /// <summary>
    /// Gets the font size multiplier for a specific ability type.
    /// Returns default multiplier if not configured.
    /// </summary>
    public float GetFontSizeMultiplier(UnitAbilityType abilityType)
    {
        var entry = GetDisplayForAbility(abilityType);
        return entry != null ? entry.fontSizeMultiplier : defaultFontSizeMultiplier;
    }
}
