using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor tool to diagnose and fix Overworld system setup issues.
/// Window → Army Prototype → Overworld Setup Validator
/// </summary>
public class OverworldSetupValidator : EditorWindow
{
    private Vector2 scrollPos;
    private List<string> errors = new List<string>();
    private List<string> warnings = new List<string>();
    private List<string> info = new List<string>();

    [MenuItem("Window/Army Prototype/Overworld Setup Validator")]
    public static void ShowWindow()
    {
        GetWindow<OverworldSetupValidator>("Overworld Validator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Overworld System Validator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        if (GUILayout.Button("Run Diagnostics", GUILayout.Height(30)))
        {
            RunDiagnostics();
        }

        GUILayout.Space(10);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Errors (red)
        if (errors.Count > 0)
        {
            GUI.color = new Color(1f, 0.3f, 0.3f);
            GUILayout.Label($"ERRORS ({errors.Count})", EditorStyles.boldLabel);
            GUI.color = Color.white;
            foreach (var e in errors)
            {
                EditorGUILayout.HelpBox(e, MessageType.Error);
            }
            GUILayout.Space(10);
        }

        // Warnings (yellow)
        if (warnings.Count > 0)
        {
            GUI.color = new Color(1f, 0.9f, 0.3f);
            GUILayout.Label($"WARNINGS ({warnings.Count})", EditorStyles.boldLabel);
            GUI.color = Color.white;
            foreach (var w in warnings)
            {
                EditorGUILayout.HelpBox(w, MessageType.Warning);
            }
            GUILayout.Space(10);
        }

        // Info (green)
        if (info.Count > 0)
        {
            GUI.color = new Color(0.3f, 1f, 0.3f);
            GUILayout.Label($"OK ({info.Count})", EditorStyles.boldLabel);
            GUI.color = Color.white;
            foreach (var i in info)
            {
                EditorGUILayout.HelpBox(i, MessageType.Info);
            }
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.Label("Quick Fixes", EditorStyles.boldLabel);

        if (GUILayout.Button("Create GameStateManager (if missing)"))
        {
            CreateGameStateManager();
        }

        if (GUILayout.Button("Find & Wire All Screen References"))
        {
            WireScreenReferences();
        }

        if (GUILayout.Button("Select GameStateManager in Scene"))
        {
            SelectGameStateManager();
        }

        if (GUILayout.Button("Log Runtime State (Play Mode Only)"))
        {
            LogRuntimeState();
        }
    }

    private void RunDiagnostics()
    {
        errors.Clear();
        warnings.Clear();
        info.Clear();

        // 1. Check for GameStateManager
        var gsm = FindAnyObjectByType<GameStateManager>();
        if (gsm == null)
        {
            errors.Add("GameStateManager NOT FOUND in scene!\n\nThis is why nothing happens when you win. Click 'Create GameStateManager' below to fix.");
        }
        else
        {
            info.Add($"GameStateManager found on '{gsm.gameObject.name}'");

            // Check screen references via reflection/serialization
            var so = new SerializedObject(gsm);

            CheckScreenReference(so, "victoryDefeatScreen", "VictoryDefeatScreen");
            CheckScreenReference(so, "warCabinetScreen", "WarCabinetScreen");
            CheckScreenReference(so, "barracksScreen", "BarracksScreen");
            CheckScreenReference(so, "worldMapScreen", "WorldMapScreen");
            CheckReference(so, "playerProgress", "PlayerProgressData");
        }

        // 2. Check for BattleTestRunner
        var btr = FindAnyObjectByType<BattleTestRunner>();
        if (btr == null)
        {
            warnings.Add("BattleTestRunner not found - are you in the battle scene?");
        }
        else
        {
            info.Add($"BattleTestRunner found on '{btr.gameObject.name}'");
        }

        // 3. Check for individual screens in scene
        var victoryScreen = FindAnyObjectByType<VictoryDefeatScreen>(FindObjectsInactive.Include);
        if (victoryScreen == null)
        {
            errors.Add("VictoryDefeatScreen component NOT FOUND in scene!\n\nCreate a Panel with VictoryDefeatScreen.cs attached.");
        }
        else
        {
            info.Add($"VictoryDefeatScreen found on '{victoryScreen.gameObject.name}' (active: {victoryScreen.gameObject.activeInHierarchy})");

            // Check its references
            var vso = new SerializedObject(victoryScreen);
            CheckReference(vso, "root", "Root Panel");
            CheckReference(vso, "resultText", "Result Text");
            CheckReference(vso, "fundsText", "Funds Text");
            CheckReference(vso, "continueButton", "Continue Button");
        }

        var warCabinet = FindAnyObjectByType<WarCabinetScreen>(FindObjectsInactive.Include);
        if (warCabinet == null)
        {
            warnings.Add("WarCabinetScreen not found in scene");
        }
        else
        {
            info.Add($"WarCabinetScreen found on '{warCabinet.gameObject.name}'");
        }

        // 4. Check for PlayerProgress asset
        var progressAssets = AssetDatabase.FindAssets("t:PlayerProgressData");
        if (progressAssets.Length == 0)
        {
            errors.Add("No PlayerProgressData asset found!\n\nCreate one via: Right-click → Create → Army Prototype → Player Progress Data");
        }
        else
        {
            var path = AssetDatabase.GUIDToAssetPath(progressAssets[0]);
            info.Add($"PlayerProgressData asset found at: {path}");
        }

        // 5. Check PerkSystem and DoctrineSystem
        var perkSys = FindAnyObjectByType<PerkSystem>();
        if (perkSys == null)
        {
            warnings.Add("PerkSystem not found in scene - perks won't work");
        }
        else
        {
            info.Add($"PerkSystem found on '{perkSys.gameObject.name}'");
        }

        var doctrineSys = FindAnyObjectByType<DoctrineSystem>();
        if (doctrineSys == null)
        {
            warnings.Add("DoctrineSystem not found in scene - doctrines won't work");
        }
        else
        {
            info.Add($"DoctrineSystem found on '{doctrineSys.gameObject.name}'");
        }

        if (errors.Count == 0 && warnings.Count == 0)
        {
            info.Insert(0, "All systems appear correctly configured!");
        }

        Repaint();
    }

    private void CheckScreenReference(SerializedObject so, string propertyName, string displayName)
    {
        var prop = so.FindProperty(propertyName);
        if (prop == null)
        {
            warnings.Add($"Property '{propertyName}' not found on GameStateManager - check field names");
        }
        else if (prop.objectReferenceValue == null)
        {
            errors.Add($"GameStateManager.{propertyName} is NULL!\n\nDrag the {displayName} object here in the Inspector.");
        }
        else
        {
            info.Add($"GameStateManager.{propertyName} → {prop.objectReferenceValue.name}");
        }
    }

    private void CheckReference(SerializedObject so, string propertyName, string displayName)
    {
        var prop = so.FindProperty(propertyName);
        if (prop != null && prop.objectReferenceValue == null)
        {
            warnings.Add($"{so.targetObject.GetType().Name}.{propertyName} ({displayName}) is not assigned");
        }
    }

    private void CreateGameStateManager()
    {
        var existing = FindAnyObjectByType<GameStateManager>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Already Exists",
                $"GameStateManager already exists on '{existing.gameObject.name}'", "OK");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var go = new GameObject("GameStateManager");
        go.AddComponent<GameStateManager>();

        // Also add PerkSystem and DoctrineSystem if missing
        if (FindAnyObjectByType<PerkSystem>() == null)
        {
            go.AddComponent<PerkSystem>();
        }
        if (FindAnyObjectByType<DoctrineSystem>() == null)
        {
            go.AddComponent<DoctrineSystem>();
        }

        Selection.activeGameObject = go;
        EditorUtility.DisplayDialog("Created",
            "Created GameStateManager.\n\nNow drag your screen references into the Inspector!", "OK");

        RunDiagnostics();
    }

    private void WireScreenReferences()
    {
        var gsm = FindAnyObjectByType<GameStateManager>();
        if (gsm == null)
        {
            EditorUtility.DisplayDialog("Error", "Create GameStateManager first!", "OK");
            return;
        }

        var so = new SerializedObject(gsm);
        int wired = 0;

        // Find and wire VictoryDefeatScreen
        var victory = FindAnyObjectByType<VictoryDefeatScreen>(FindObjectsInactive.Include);
        if (victory != null)
        {
            var prop = so.FindProperty("victoryDefeatScreen");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = victory;
                wired++;
            }
        }

        // Find and wire WarCabinetScreen
        var warCabinet = FindAnyObjectByType<WarCabinetScreen>(FindObjectsInactive.Include);
        if (warCabinet != null)
        {
            var prop = so.FindProperty("warCabinetScreen");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = warCabinet;
                wired++;
            }
        }

        // Find and wire BarracksScreen
        var barracks = FindAnyObjectByType<BarracksScreen>(FindObjectsInactive.Include);
        if (barracks != null)
        {
            var prop = so.FindProperty("barracksScreen");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = barracks;
                wired++;
            }
        }

        // Find and wire WorldMapScreen
        var worldMap = FindAnyObjectByType<WorldMapScreen>(FindObjectsInactive.Include);
        if (worldMap != null)
        {
            var prop = so.FindProperty("worldMapScreen");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = worldMap;
                wired++;
            }
        }

        // Find and wire PlayerProgressData
        var progressAssets = AssetDatabase.FindAssets("t:PlayerProgressData");
        if (progressAssets.Length > 0)
        {
            var path = AssetDatabase.GUIDToAssetPath(progressAssets[0]);
            var progress = AssetDatabase.LoadAssetAtPath<PlayerProgressData>(path);
            if (progress != null)
            {
                var prop = so.FindProperty("playerProgress");
                if (prop != null && prop.objectReferenceValue == null)
                {
                    prop.objectReferenceValue = progress;
                    wired++;
                }

                // Also wire to PerkSystem and DoctrineSystem
                var perkSys = FindAnyObjectByType<PerkSystem>();
                if (perkSys != null)
                {
                    var pso = new SerializedObject(perkSys);
                    var pprop = pso.FindProperty("playerProgress");
                    if (pprop != null && pprop.objectReferenceValue == null)
                    {
                        pprop.objectReferenceValue = progress;
                        pso.ApplyModifiedProperties();
                        wired++;
                    }
                }

                var doctrineSys = FindAnyObjectByType<DoctrineSystem>();
                if (doctrineSys != null)
                {
                    var dso = new SerializedObject(doctrineSys);
                    var dprop = dso.FindProperty("playerProgress");
                    if (dprop != null && dprop.objectReferenceValue == null)
                    {
                        dprop.objectReferenceValue = progress;
                        dso.ApplyModifiedProperties();
                        wired++;
                    }
                }
            }
        }

        so.ApplyModifiedProperties();

        EditorUtility.DisplayDialog("Wiring Complete",
            $"Auto-wired {wired} references.\n\nRun diagnostics again to check status.", "OK");

        RunDiagnostics();
    }

    private void SelectGameStateManager()
    {
        var gsm = FindAnyObjectByType<GameStateManager>();
        if (gsm != null)
        {
            Selection.activeGameObject = gsm.gameObject;
            EditorGUIUtility.PingObject(gsm.gameObject);
        }
        else
        {
            EditorUtility.DisplayDialog("Not Found", "GameStateManager not in scene", "OK");
        }
    }

    private void LogRuntimeState()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Not Playing", "Enter Play Mode first to check runtime state", "OK");
            return;
        }

        Debug.Log("=== OVERWORLD RUNTIME STATE ===");

        if (GameStateManager.Instance != null)
        {
            Debug.Log($"GameStateManager.Instance: EXISTS");
            Debug.Log($"  Current State: {GameStateManager.Instance.CurrentState}");
        }
        else
        {
            Debug.LogError("GameStateManager.Instance is NULL at runtime!");
        }

        if (BattleTestRunner.Instance != null)
        {
            Debug.Log($"BattleTestRunner.Instance: EXISTS");
        }

        if (PerkSystem.Instance != null)
        {
            Debug.Log($"PerkSystem.Instance: EXISTS");
        }
        else
        {
            Debug.LogWarning("PerkSystem.Instance is NULL");
        }

        if (DoctrineSystem.Instance != null)
        {
            Debug.Log($"DoctrineSystem.Instance: EXISTS");
        }
        else
        {
            Debug.LogWarning("DoctrineSystem.Instance is NULL");
        }

        Debug.Log("=== END RUNTIME STATE ===");
    }
}
