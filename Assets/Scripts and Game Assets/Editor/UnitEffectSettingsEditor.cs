using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Custom inspector for UnitEffectSettings.
/// Displays each effect as a row in a table with:
///   Col 1 : Effect ID  (text field + ▼ preset dropdown)
///   Col 2 : Color      (color picker with alpha as opacity + ▼ preset swatches)
///   Col 3 : Effect Type(s) (Outline / Glow / Holographic flags)
///   Col 4 : Pulse toggle
///   Col 5 : Intensity slider  (0–1)
///   Col 6 : Width px slider   (outline only, greyed when not applicable)
///   Col 7 : Speed slider      (pulse only, greyed when not pulsing)
///   [X]   : Delete row
/// </summary>
[CustomEditor(typeof(UnitEffectSettings))]
public class UnitEffectSettingsEditor : Editor
{
    // ------------------------------------------------------------------
    // Known effect IDs — shown in the ▼ dropdown so you can pick quickly
    // ------------------------------------------------------------------
    private static readonly string[] KnownEffectIds =
    {
        // Special card debuffs
        "BarbedWire", "LandMines", "RadarJammer", "EmpStrike",
        // Special card buffs
        "Trenches",
        // Conditional ATK abilities
        "ThreeOfAKind", "Unmatched", "ArmorPiercing", "SOS", "BrotherInArms",
        // Always-on abilities
        "Persist", "Counterattack", "Armored", "DirectAttack",
    };

    // ------------------------------------------------------------------
    // Preset color swatches — shown in the ▼ next to the color field
    // ------------------------------------------------------------------
    private static readonly (string label, Color color)[] PresetColors =
    {
        ("Red",        new Color(0.90f, 0.10f, 0.10f)),
        ("Orange",     new Color(1.00f, 0.45f, 0.00f)),
        ("Gold",       new Color(1.00f, 0.85f, 0.00f)),
        ("Yellow",     Color.yellow),
        ("Lime",       new Color(0.30f, 0.90f, 0.10f)),
        ("Green",      new Color(0.10f, 0.80f, 0.20f)),
        ("Cyan",       Color.cyan),
        ("Sky Blue",   new Color(0.30f, 0.65f, 1.00f)),
        ("Blue",       new Color(0.10f, 0.30f, 1.00f)),
        ("Purple",     new Color(0.60f, 0.00f, 1.00f)),
        ("Magenta",    Color.magenta),
        ("Pink",       new Color(1.00f, 0.40f, 0.80f)),
        ("White",      Color.white),
    };

    // ------------------------------------------------------------------
    // Column widths (px)
    // ------------------------------------------------------------------
    const float W_ID     = 155f;   // effect ID text field
    const float W_IDPICK =  22f;   // ▼ dropdown button next to ID
    const float W_COLOR  =  60f;   // color field
    const float W_CPICK  =  22f;   // ▼ dropdown button next to color
    const float W_TYPE   = 185f;   // effect type flags
    const float W_PULSE  =  40f;   // pulse toggle
    const float W_INT    =  85f;   // intensity slider
    const float W_WIDTH  =  85f;   // outline width slider
    const float W_SPEED  =  85f;   // pulse speed slider
    const float W_DEL    =  22f;   // [X] delete button

    private Vector2 _scrollPos;
    private bool    _showHelp;
    private bool    _showDefaults;

    // ------------------------------------------------------------------
    // Convenience type-alias combos for recommended defaults
    // ------------------------------------------------------------------
    private static readonly UnitEffectSettings.UnitEffectType T_Outline     = UnitEffectSettings.UnitEffectType.Outline;
    private static readonly UnitEffectSettings.UnitEffectType T_Glow        = UnitEffectSettings.UnitEffectType.Glow;
    private static readonly UnitEffectSettings.UnitEffectType T_Holo        = UnitEffectSettings.UnitEffectType.Holographic;
    private static readonly UnitEffectSettings.UnitEffectType T_OutGlow     = UnitEffectSettings.UnitEffectType.Outline | UnitEffectSettings.UnitEffectType.Glow;
    private static readonly UnitEffectSettings.UnitEffectType T_GlowHolo    = UnitEffectSettings.UnitEffectType.Glow    | UnitEffectSettings.UnitEffectType.Holographic;
    private static readonly UnitEffectSettings.UnitEffectType T_OutGlowHolo = UnitEffectSettings.UnitEffectType.Outline | UnitEffectSettings.UnitEffectType.Glow | UnitEffectSettings.UnitEffectType.Holographic;

    // ==================================================================
    // OnInspectorGUI
    // ==================================================================
    public override void OnInspectorGUI()
    {
        var settings = (UnitEffectSettings)target;
        serializedObject.Update();

        // ---- Header ----
        EditorGUILayout.Space(4);
        DrawHelp();
        EditorGUILayout.Space(6);

        // ---- Toolbar buttons ----
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ Add Row", EditorStyles.miniButton, GUILayout.Height(22)))
            AddRow(settings);

        GUILayout.FlexibleSpace();

        _showDefaults = GUILayout.Toggle(_showDefaults, "Load Defaults ▼", EditorStyles.miniButton, GUILayout.Width(120), GUILayout.Height(22));
        EditorGUILayout.EndHorizontal();

        if (_showDefaults)
            DrawDefaultsPanel(settings);

        EditorGUILayout.Space(4);

        // ---- Column header ----
        DrawColumnHeader();

        // ---- Scrollable rows ----
        _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

        var defs = new List<UnitEffectSettings.EffectDefinition>(
            settings.effectDefinitions ?? Array.Empty<UnitEffectSettings.EffectDefinition>());

        int deleteIndex = -1;
        bool anyDirty  = false;

        for (int i = 0; i < defs.Count; i++)
        {
            bool deleted = DrawRow(defs[i], i, ref anyDirty);
            if (deleted) deleteIndex = i;
        }

        EditorGUILayout.EndScrollView();

        // Apply deletions
        if (deleteIndex >= 0)
        {
            defs.RemoveAt(deleteIndex);
            settings.effectDefinitions = defs.ToArray();
            anyDirty = true;
        }

        if (anyDirty)
        {
            EditorUtility.SetDirty(target);
            serializedObject.ApplyModifiedProperties();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(
            $"{settings.effectDefinitions?.Length ?? 0} effect(s) defined",
            EditorStyles.centeredGreyMiniLabel);
    }

    // ==================================================================
    // Column header row
    // ==================================================================
    private void DrawColumnHeader()
    {
        var s = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize  = 9,
            alignment = TextAnchor.MiddleCenter,
        };

        var bgRect = EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(18));
        EditorGUI.DrawRect(bgRect, new Color(0.18f, 0.18f, 0.18f, 0.6f));

        GUILayout.Label("Effect ID",    s, GUILayout.Width(W_ID + W_IDPICK));
        GUILayout.Label("Color / α",    s, GUILayout.Width(W_COLOR + W_CPICK));
        GUILayout.Label("Type(s)",      s, GUILayout.Width(W_TYPE));
        GUILayout.Label("Pulse",        s, GUILayout.Width(W_PULSE));
        GUILayout.Label("Intensity",    s, GUILayout.Width(W_INT));
        GUILayout.Label("Width px",     s, GUILayout.Width(W_WIDTH));
        GUILayout.Label("Speed",        s, GUILayout.Width(W_SPEED));
        GUILayout.Label("",             GUILayout.Width(W_DEL));

        EditorGUILayout.EndHorizontal();
    }

    // ==================================================================
    // Single data row — returns true if row should be deleted
    // ==================================================================
    private bool DrawRow(UnitEffectSettings.EffectDefinition def, int index, ref bool anyDirty)
    {
        bool shouldDelete = false;

        // Alternating background
        var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight + 2));
        if (index % 2 == 0)
            EditorGUI.DrawRect(rowRect, new Color(0f, 0f, 0f, 0.07f));

        // ── Col 1a: Effect ID text field ──────────────────────────────
        var newId = EditorGUILayout.TextField(def.effectId, GUILayout.Width(W_ID));
        if (newId != def.effectId) { def.effectId = newId; anyDirty = true; }

        // ── Col 1b: ▼ preset ID dropdown ─────────────────────────────
        if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(W_IDPICK)))
        {
            var menu = new GenericMenu();
            foreach (var id in KnownEffectIds)
            {
                string captured = id;
                menu.AddItem(
                    new GUIContent(id),
                    def.effectId == id,
                    () => { def.effectId = captured; EditorUtility.SetDirty(target); });
            }
            menu.ShowAsContext();
        }

        // ── Col 2a: Color field (alpha = opacity) ─────────────────────
        var newColor = EditorGUILayout.ColorField(
            GUIContent.none, def.effectColor,
            true, true, false,
            GUILayout.Width(W_COLOR));
        if (newColor != def.effectColor) { def.effectColor = newColor; anyDirty = true; }

        // ── Col 2b: ▼ preset color swatch dropdown ────────────────────
        if (GUILayout.Button("▼", EditorStyles.miniButton, GUILayout.Width(W_CPICK)))
        {
            var menu = new GenericMenu();
            foreach (var (label, col) in PresetColors)
            {
                Color captured = col;
                menu.AddItem(
                    new GUIContent(label),
                    false,
                    () =>
                    {
                        // Preserve current alpha when applying preset
                        def.effectColor = new Color(captured.r, captured.g, captured.b, def.effectColor.a);
                        EditorUtility.SetDirty(target);
                    });
            }
            menu.ShowAsContext();
        }

        // ── Col 3: Effect type flags ──────────────────────────────────
        var newType = (UnitEffectSettings.UnitEffectType)EditorGUILayout.EnumFlagsField(
            def.effectTypes, GUILayout.Width(W_TYPE));
        if (newType != def.effectTypes) { def.effectTypes = newType; anyDirty = true; }

        // ── Col 4: Pulsing toggle ─────────────────────────────────────
        var newPulse = EditorGUILayout.Toggle(def.pulsing, GUILayout.Width(W_PULSE));
        if (newPulse != def.pulsing) { def.pulsing = newPulse; anyDirty = true; }

        // ── Col 5: Intensity slider ───────────────────────────────────
        var newInt = EditorGUILayout.Slider(def.intensity, 0f, 1f, GUILayout.Width(W_INT));
        if (!Mathf.Approximately(newInt, def.intensity)) { def.intensity = newInt; anyDirty = true; }

        // ── Col 6: Outline Width slider (greyed if Outline not checked) ─
        bool hasOutline = (def.effectTypes & UnitEffectSettings.UnitEffectType.Outline) != 0;
        EditorGUI.BeginDisabledGroup(!hasOutline);
        var newWidth = EditorGUILayout.Slider(def.outlineWidthPixels, 1f, 8f, GUILayout.Width(W_WIDTH));
        if (!Mathf.Approximately(newWidth, def.outlineWidthPixels) && hasOutline)
        { def.outlineWidthPixels = newWidth; anyDirty = true; }
        EditorGUI.EndDisabledGroup();

        // ── Col 7: Pulse Speed slider (greyed if not pulsing) ─────────
        EditorGUI.BeginDisabledGroup(!def.pulsing);
        var newSpeed = EditorGUILayout.Slider(def.pulseSpeed, 0f, 10f, GUILayout.Width(W_SPEED));
        if (!Mathf.Approximately(newSpeed, def.pulseSpeed) && def.pulsing)
        { def.pulseSpeed = newSpeed; anyDirty = true; }
        EditorGUI.EndDisabledGroup();

        // ── Delete button ─────────────────────────────────────────────
        var prevColor = GUI.color;
        GUI.color = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(W_DEL)))
            shouldDelete = true;
        GUI.color = prevColor;

        EditorGUILayout.EndHorizontal();
        return shouldDelete;
    }

    // ==================================================================
    // Help foldout
    // ==================================================================
    private void DrawHelp()
    {
        _showHelp = EditorGUILayout.Foldout(_showHelp, "How to use", true, EditorStyles.foldoutHeader);
        if (!_showHelp) return;

        EditorGUILayout.HelpBox(
            "EFFECT ID — must exactly match the string passed to UnitEffectManager.ApplyEffect(\"ID\", unitId).\n" +
            "   Use ▼ to pick from known built-in IDs.\n\n" +
            "COLOR — tint for outline and glow. Alpha channel = opacity of the overlay.\n" +
            "   Use ▼ to pick a preset swatch (keeps current alpha).\n\n" +
            "TYPE(S) — which visual layers activate:\n" +
            "   • Outline     : pixel-perfect silhouette edge (SpriteOutline shader)\n" +
            "   • Glow        : soft rim glow that pulses (RimPulse shader)\n" +
            "   • Holographic : rainbow foil shimmer (HoloFoilSoft shader)\n" +
            "   You can combine all three.\n\n" +
            "PULSE — makes the glow throb. Controls SPEED slider (greyed when off).\n\n" +
            "INTENSITY — 0=off, 1=max. Scales glow brightness and foil strength.\n\n" +
            "WIDTH px — outline thickness in pixels (1–8). Only matters when Outline is on.\n\n" +
            "SPEED — pulse rate (0–10). Only matters when Pulse is checked.\n\n" +
            "⚠  The asset must be saved inside a  Resources/  folder so it loads at runtime.",
            MessageType.Info);
    }

    // ==================================================================
    // Load Defaults panel
    // ==================================================================
    private void DrawDefaultsPanel(UnitEffectSettings settings)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Load Recommended Defaults", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Replaces ALL existing rows with a full set of themed defaults.",
            EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Replace All With Defaults", GUILayout.Height(24)))
        {
            if (EditorUtility.DisplayDialog(
                "Replace All Effects?",
                "This will REPLACE every existing row with the recommended defaults. Continue?",
                "Yes, Replace", "Cancel"))
            {
                ApplyRecommendedDefaults(settings);
                _showDefaults = false;
            }
        }
        if (GUILayout.Button("Add Defaults (keep existing)", GUILayout.Height(24)))
        {
            ApplyRecommendedDefaults(settings, append: true);
            _showDefaults = false;
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    // ==================================================================
    // Recommended defaults factory
    // ==================================================================
    private void ApplyRecommendedDefaults(UnitEffectSettings settings, bool append = false)
    {
        var newDefs = new UnitEffectSettings.EffectDefinition[]
        {
            // ── Special card debuffs ──────────────────────────────────
            MakeDef("BarbedWire",    new Color(0.90f, 0.10f, 0.10f), T_OutGlow,     0.70f, width: 3f),
            MakeDef("LandMines",     new Color(0.80f, 0.40f, 0.00f), T_OutGlow,     0.70f, width: 3f),
            MakeDef("RadarJammer",   new Color(0.25f, 0.25f, 0.95f), T_OutGlow,     0.75f, width: 3f, pulse: true, speed: 4f),
            MakeDef("EmpStrike",     new Color(0.70f, 0.00f, 1.00f), T_OutGlowHolo, 0.90f, width: 4f, pulse: true, speed: 5f),

            // ── Special card buffs ────────────────────────────────────
            MakeDef("Trenches",      new Color(0.10f, 0.80f, 0.20f), T_OutGlow,     0.75f, width: 2f),

            // ── Conditional ATK abilities ─────────────────────────────
            MakeDef("ThreeOfAKind",  new Color(1.00f, 0.85f, 0.00f), T_GlowHolo,   0.85f, pulse: true, speed: 2f),
            MakeDef("Unmatched",     new Color(1.00f, 0.75f, 0.05f), T_GlowHolo,   0.90f, pulse: true, speed: 3f),
            MakeDef("ArmorPiercing", new Color(0.30f, 0.70f, 1.00f), T_OutGlow,     0.80f, width: 3f),
            MakeDef("SOS",           new Color(1.00f, 0.20f, 0.20f), T_OutGlow,     1.00f, width: 4f, pulse: true, speed: 6f),
            MakeDef("BrotherInArms", new Color(1.00f, 0.55f, 0.00f), T_OutGlow,     0.80f, width: 2f, pulse: true, speed: 3f),

            // ── Always-on abilities ───────────────────────────────────
            MakeDef("Persist",       new Color(0.40f, 0.80f, 1.00f), T_Outline,     0.70f, width: 2f),
            MakeDef("Counterattack", new Color(0.85f, 0.25f, 1.00f), T_OutGlow,     0.75f, width: 2f),
            MakeDef("Armored",       new Color(0.40f, 0.70f, 1.00f), T_OutGlow,     0.80f, width: 3f),
            MakeDef("DirectAttack",  new Color(1.00f, 0.30f, 0.10f), T_GlowHolo,   0.85f, pulse: true, speed: 4f),
        };

        if (append && settings.effectDefinitions != null && settings.effectDefinitions.Length > 0)
        {
            var combined = new List<UnitEffectSettings.EffectDefinition>(settings.effectDefinitions);
            combined.AddRange(newDefs);
            settings.effectDefinitions = combined.ToArray();
        }
        else
        {
            settings.effectDefinitions = newDefs;
        }

        EditorUtility.SetDirty(settings);
    }

    private static UnitEffectSettings.EffectDefinition MakeDef(
        string id, Color color, UnitEffectSettings.UnitEffectType types,
        float intensity = 0.8f, float width = 2f, bool pulse = false, float speed = 3f)
    {
        return new UnitEffectSettings.EffectDefinition
        {
            effectId           = id,
            effectColor        = color,
            effectTypes        = types,
            intensity          = intensity,
            outlineWidthPixels = width,
            pulsing            = pulse,
            pulseSpeed         = speed,
        };
    }

    // ==================================================================
    // Add an empty row to the list
    // ==================================================================
    private void AddRow(UnitEffectSettings settings)
    {
        var list = new List<UnitEffectSettings.EffectDefinition>(
            settings.effectDefinitions ?? Array.Empty<UnitEffectSettings.EffectDefinition>())
        {
            new UnitEffectSettings.EffectDefinition
            {
                effectId           = "NewEffect",
                effectColor        = Color.white,
                effectTypes        = UnitEffectSettings.UnitEffectType.Outline,
                intensity          = 0.8f,
                outlineWidthPixels = 2f,
                pulsing            = false,
                pulseSpeed         = 3f,
            }
        };
        settings.effectDefinitions = list.ToArray();
        EditorUtility.SetDirty(settings);
    }
}
