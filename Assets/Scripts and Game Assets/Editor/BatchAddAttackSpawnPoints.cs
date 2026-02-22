using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor utility to batch-add AttackSpawnPoint child objects to unit prefabs.
/// Also ensures UnitAttackEffects component is present.
/// Menu: Tools > Batch Add Attack Spawn Points
/// </summary>
public class BatchAddAttackSpawnPoints : EditorWindow
{
    // ========================================
    // SETTINGS
    // ========================================

    [System.Serializable]
    private class UnitTypeSettings
    {
        public string name;
        public Vector3 offset;
        public AttackEffectType attackType;
        public Color muzzleFlashColor;
        public float muzzleFlashSize;
        public bool enableRecoil;
        public float recoilDistance;
        public bool expanded;

        public UnitTypeSettings(string name, Vector3 offset, AttackEffectType attackType = AttackEffectType.MuzzleFlash)
        {
            this.name = name;
            this.offset = offset;
            this.attackType = attackType;
            this.muzzleFlashColor = new Color(1f, 0.9f, 0.3f); // Yellow-orange
            this.muzzleFlashSize = 0.3f;
            this.enableRecoil = attackType != AttackEffectType.None;
            this.recoilDistance = 0.1f;
            this.expanded = false;
        }
    }

    private UnitTypeSettings infantrySettings;
    private UnitTypeSettings tankSettings;
    private UnitTypeSettings airSettings;
    private UnitTypeSettings rangedSettings;

    private string searchFolder = "Assets/Scripts and Game Assets/Cards";
    private bool addComponentIfMissing = true;
    private bool overwriteExisting = false;
    private bool showPreview = true;
    private bool searchFromCardDefinitions = true;

    private Vector2 scrollPosition;
    private List<PrefabPreviewInfo> previewList = new List<PrefabPreviewInfo>();
    private HashSet<string> prefabsFromCards = new HashSet<string>();

    private class PrefabPreviewInfo
    {
        public string path;
        public string name;
        public bool hasSpawnPoint;
        public bool hasAttackEffects;
        public string detectedType;
        public bool willUpdate;
        public bool isFromCardDef;
    }

    // ========================================
    // WINDOW SETUP
    // ========================================

    [MenuItem("Tools/Batch Add Attack Spawn Points")]
    static void ShowWindow()
    {
        var window = GetWindow<BatchAddAttackSpawnPoints>("Attack Spawn Points");
        window.minSize = new Vector2(450, 600);
        window.InitializeSettings();
    }

    private void InitializeSettings()
    {
        // Infantry: gun barrel is typically forward and slightly up
        // Assuming unit faces RIGHT (positive X)
        infantrySettings = new UnitTypeSettings(
            "Infantry",
            new Vector3(0.3f, 0.1f, 0f),  // Forward and slightly up
            AttackEffectType.MuzzleFlash
        );
        infantrySettings.muzzleFlashSize = 0.2f;
        infantrySettings.enableRecoil = true;
        infantrySettings.recoilDistance = 0.05f;

        // Tanks: cannon barrel is forward and higher
        tankSettings = new UnitTypeSettings(
            "Tank/Vehicle",
            new Vector3(0.5f, 0.15f, 0f),  // Further forward, higher
            AttackEffectType.MuzzleFlash
        );
        tankSettings.muzzleFlashColor = new Color(1f, 0.7f, 0.2f); // More orange
        tankSettings.muzzleFlashSize = 0.4f;
        tankSettings.enableRecoil = true;
        tankSettings.recoilDistance = 0.15f;

        // Air units: nose or wing guns
        airSettings = new UnitTypeSettings(
            "Air/Plane",
            new Vector3(0.4f, 0f, 0f),  // Forward from nose
            AttackEffectType.Projectile
        );
        airSettings.muzzleFlashSize = 0.25f;
        airSettings.enableRecoil = false;

        // Ranged: rockets/artillery - larger effects
        rangedSettings = new UnitTypeSettings(
            "Ranged/Artillery",
            new Vector3(0.4f, 0.2f, 0f),
            AttackEffectType.Projectile
        );
        rangedSettings.muzzleFlashColor = new Color(1f, 0.5f, 0.1f); // Orange-red
        rangedSettings.muzzleFlashSize = 0.5f;
        rangedSettings.enableRecoil = true;
        rangedSettings.recoilDistance = 0.2f;
    }

    // ========================================
    // GUI
    // ========================================

    void OnGUI()
    {
        if (infantrySettings == null) InitializeSettings();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Batch Add Attack Spawn Points", EditorStyles.boldLabel);
        GUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "This tool adds 'AttackSpawnPoint' child objects to unit prefabs, " +
            "positioned at the barrel/muzzle. It also adds the UnitAttackEffects component if missing.",
            MessageType.Info);

        GUILayout.Space(10);

        // Search folder
        GUILayout.Label("Search Settings", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        searchFolder = EditorGUILayout.TextField("Prefab Folder:", searchFolder);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string folder = EditorUtility.OpenFolderPanel("Select Prefab Folder", "Assets", "");
            if (!string.IsNullOrEmpty(folder))
            {
                // Convert to relative path
                if (folder.StartsWith(Application.dataPath))
                {
                    searchFolder = "Assets" + folder.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        addComponentIfMissing = EditorGUILayout.Toggle("Add UnitAttackEffects if missing", addComponentIfMissing);
        overwriteExisting = EditorGUILayout.Toggle("Overwrite existing spawn points", overwriteExisting);

        GUILayout.Space(10);
        GUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
        searchFromCardDefinitions = EditorGUILayout.Toggle("Also scan Card Definitions", searchFromCardDefinitions);
        GUI.backgroundColor = Color.white;
        if (searchFromCardDefinitions)
        {
            EditorGUILayout.HelpBox(
                "This will find ALL prefabs referenced by UnitCardDefinition assets (both worldPrefabA and worldPrefabB). " +
                "This ensures both Player 1 and Player 2 prefabs are updated!",
                MessageType.Info);
        }

        GUILayout.Space(15);

        // Unit type settings
        GUILayout.Label("Spawn Point Offsets by Unit Type", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Offsets are relative to unit center. Positive X = forward (assuming unit faces right). " +
            "The tool auto-detects unit type from prefab name.",
            MessageType.None);

        GUILayout.Space(5);

        DrawUnitTypeSettings(infantrySettings, "Infantry (SMG, Rifle, RPG, Grenadier, etc.)");
        DrawUnitTypeSettings(tankSettings, "Tanks & Vehicles (Tank, IFV, etc.)");
        DrawUnitTypeSettings(airSettings, "Air Units (F16, Bomber, Helicopter, etc.)");
        DrawUnitTypeSettings(rangedSettings, "Ranged/Artillery (Rockets, Artillery, etc.)");

        GUILayout.Space(15);

        // Preview section
        showPreview = EditorGUILayout.Foldout(showPreview, "Preview Changes", true);
        if (showPreview)
        {
            if (GUILayout.Button("Scan Prefabs", GUILayout.Height(30)))
            {
                ScanPrefabs();
            }

            if (previewList.Count > 0)
            {
                GUILayout.Space(5);
                DrawPreviewList();
            }
        }

        GUILayout.Space(15);

        // Action buttons
        EditorGUILayout.BeginHorizontal();

        GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
        if (GUILayout.Button("Apply to All Prefabs", GUILayout.Height(40)))
        {
            if (EditorUtility.DisplayDialog("Confirm",
                "This will modify prefabs in:\n" + searchFolder + "\n\nContinue?",
                "Apply", "Cancel"))
            {
                ApplyToAllPrefabs();
            }
        }

        GUI.backgroundColor = new Color(1f, 1f, 0.5f);
        if (GUILayout.Button("Apply to Selected Only", GUILayout.Height(40)))
        {
            ApplyToSelectedPrefabs();
        }

        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
    }

    private void DrawUnitTypeSettings(UnitTypeSettings settings, string description)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        settings.expanded = EditorGUILayout.Foldout(settings.expanded, settings.name, true);

        if (settings.expanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(description, EditorStyles.miniLabel);
            settings.offset = EditorGUILayout.Vector3Field("Offset", settings.offset);
            settings.attackType = (AttackEffectType)EditorGUILayout.EnumPopup("Attack Type", settings.attackType);
            settings.muzzleFlashColor = EditorGUILayout.ColorField("Muzzle Flash Color", settings.muzzleFlashColor);
            settings.muzzleFlashSize = EditorGUILayout.Slider("Muzzle Flash Size", settings.muzzleFlashSize, 0.1f, 1f);
            settings.enableRecoil = EditorGUILayout.Toggle("Enable Recoil", settings.enableRecoil);
            if (settings.enableRecoil)
            {
                settings.recoilDistance = EditorGUILayout.Slider("Recoil Distance", settings.recoilDistance, 0.01f, 0.3f);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewList()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        int willUpdate = 0;
        int missingFX = 0;
        int fromCardDefs = 0;
        foreach (var info in previewList)
        {
            if (info.willUpdate || !info.hasAttackEffects) willUpdate++;
            if (!info.hasAttackEffects) missingFX++;
            if (info.isFromCardDef) fromCardDefs++;
        }

        GUILayout.Label($"Found {previewList.Count} prefabs ({fromCardDefs} from Card Definitions)", EditorStyles.boldLabel);

        if (missingFX > 0)
        {
            GUI.color = Color.red;
            GUILayout.Label($"⚠ {missingFX} prefabs MISSING UnitAttackEffects (won't fire!)", EditorStyles.boldLabel);
            GUI.color = Color.white;
        }

        GUILayout.Label($"{willUpdate} will be updated", EditorStyles.miniLabel);
        GUILayout.Space(5);
        GUILayout.Label("Legend: CD = From Card Definition, FX = Has UnitAttackEffects", EditorStyles.miniLabel);

        foreach (var info in previewList)
        {
            EditorGUILayout.BeginHorizontal();

            // Status icon
            if (info.hasSpawnPoint && info.hasAttackEffects && !overwriteExisting)
            {
                GUI.color = Color.gray;
                GUILayout.Label("✓", GUILayout.Width(20));
            }
            else if (info.willUpdate || !info.hasAttackEffects)
            {
                GUI.color = Color.green;
                GUILayout.Label("→", GUILayout.Width(20));
            }
            else
            {
                GUI.color = Color.yellow;
                GUILayout.Label("?", GUILayout.Width(20));
            }
            GUI.color = Color.white;

            // Source indicator (from card def?)
            if (info.isFromCardDef)
            {
                GUI.color = new Color(0.5f, 0.8f, 1f);
                GUILayout.Label("CD", GUILayout.Width(22)); // Card Definition
                GUI.color = Color.white;
            }
            else
            {
                GUILayout.Label("  ", GUILayout.Width(22));
            }

            // Name and type
            GUILayout.Label(info.name, GUILayout.Width(180));
            GUILayout.Label($"[{info.detectedType}]", EditorStyles.miniLabel, GUILayout.Width(60));

            // Status
            string status = "";
            if (!info.hasAttackEffects)
            {
                GUI.color = Color.red;
                status = "NO FX! ";
                GUI.color = Color.white;
            }
            if (info.hasSpawnPoint) status += "Spawn ";
            if (info.hasAttackEffects) status += "FX";
            if (string.IsNullOrEmpty(status)) status = "New";
            GUILayout.Label(status, EditorStyles.miniLabel);

            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.EndVertical();
    }

    // ========================================
    // LOGIC
    // ========================================

    private void ScanPrefabs()
    {
        previewList.Clear();
        prefabsFromCards.Clear();

        // First, scan card definitions to find ALL referenced prefabs
        if (searchFromCardDefinitions)
        {
            ScanCardDefinitionsForPrefabs();
        }

        // Then scan the folder for prefabs
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { searchFolder });

        HashSet<string> processedPaths = new HashSet<string>();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (processedPaths.Contains(path)) continue;
            processedPaths.Add(path);

            AddPrefabToPreview(path);
        }

        // Also add any prefabs found via card definitions that weren't in the search folder
        foreach (string path in prefabsFromCards)
        {
            if (processedPaths.Contains(path)) continue;
            processedPaths.Add(path);

            AddPrefabToPreview(path);
        }

        Debug.Log($"[BatchAddAttackSpawnPoints] Scanned {previewList.Count} unit prefabs " +
                  $"({prefabsFromCards.Count} from card definitions)");
    }

    private void AddPrefabToPreview(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) return;

        // Check if it looks like a unit prefab (has SpriteRenderer)
        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr == null) return;

        var info = new PrefabPreviewInfo
        {
            path = path,
            name = prefab.name,
            hasSpawnPoint = prefab.transform.Find("AttackSpawnPoint") != null,
            hasAttackEffects = prefab.GetComponent<UnitAttackEffects>() != null,
            detectedType = DetectUnitType(prefab.name),
            isFromCardDef = prefabsFromCards.Contains(path)
        };

        info.willUpdate = !info.hasSpawnPoint || overwriteExisting;

        previewList.Add(info);
    }

    private void ScanCardDefinitionsForPrefabs()
    {
        // Find all UnitCardDefinition assets
        string[] cardGuids = AssetDatabase.FindAssets("t:UnitCardDefinition");

        foreach (string guid in cardGuids)
        {
            string cardPath = AssetDatabase.GUIDToAssetPath(guid);
            UnitCardDefinition cardDef = AssetDatabase.LoadAssetAtPath<UnitCardDefinition>(cardPath);

            if (cardDef == null) continue;

            // Get Player A prefab
            if (cardDef.worldPrefabA != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(cardDef.worldPrefabA);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    prefabsFromCards.Add(prefabPath);
                    Debug.Log($"[BatchAddAttackSpawnPoints] Found P1 prefab from {cardDef.cardName}: {prefabPath}");
                }
            }

            // Get Player B prefab
            if (cardDef.worldPrefabB != null)
            {
                string prefabPath = AssetDatabase.GetAssetPath(cardDef.worldPrefabB);
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    prefabsFromCards.Add(prefabPath);
                    Debug.Log($"[BatchAddAttackSpawnPoints] Found P2 prefab from {cardDef.cardName}: {prefabPath}");
                }
            }
        }

        Debug.Log($"[BatchAddAttackSpawnPoints] Found {prefabsFromCards.Count} prefabs from card definitions");
    }

    private string DetectUnitType(string prefabName)
    {
        string lower = prefabName.ToLower();

        // Air units
        if (lower.Contains("f16") || lower.Contains("f22") || lower.Contains("plane") ||
            lower.Contains("bomber") || lower.Contains("stealth") || lower.Contains("helicopter") ||
            lower.Contains("jaguar") || lower.Contains("air"))
        {
            return "Air";
        }

        // Tanks/Vehicles
        if (lower.Contains("tank") || lower.Contains("ifv") || lower.Contains("vehicle") ||
            lower.Contains("m1a2") || lower.Contains("armor"))
        {
            return "Tank";
        }

        // Ranged/Artillery
        if (lower.Contains("rocket") || lower.Contains("artillery") || lower.Contains("rpg") ||
            lower.Contains("missile") || lower.Contains("sam"))
        {
            return "Ranged";
        }

        // Default to infantry
        return "Infantry";
    }

    private UnitTypeSettings GetSettingsForType(string typeName)
    {
        switch (typeName)
        {
            case "Air": return airSettings;
            case "Tank": return tankSettings;
            case "Ranged": return rangedSettings;
            default: return infantrySettings;
        }
    }

    private void ApplyToAllPrefabs()
    {
        if (previewList.Count == 0)
        {
            ScanPrefabs();
        }

        int updateCount = 0;
        EditorUtility.DisplayProgressBar("Adding Attack Spawn Points", "Processing...", 0);

        try
        {
            for (int i = 0; i < previewList.Count; i++)
            {
                var info = previewList[i];
                EditorUtility.DisplayProgressBar("Adding Attack Spawn Points",
                    $"Processing {info.name}", (float)i / previewList.Count);

                if (info.willUpdate || addComponentIfMissing)
                {
                    if (ProcessPrefab(info.path, info.detectedType))
                    {
                        updateCount++;
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Complete",
            $"Updated {updateCount} prefabs.\n\nAttack spawn points added at barrel positions.",
            "OK");

        // Refresh preview
        ScanPrefabs();
    }

    private void ApplyToSelectedPrefabs()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection",
                "Please select prefabs in the Project window first.", "OK");
            return;
        }

        int updateCount = 0;

        foreach (GameObject obj in selected)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) continue;

            string typeName = DetectUnitType(obj.name);
            if (ProcessPrefab(path, typeName))
            {
                updateCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Complete",
            $"Updated {updateCount} selected prefabs.", "OK");
    }

    private bool ProcessPrefab(string prefabPath, string unitType)
    {
        GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);
        if (prefabInstance == null) return false;

        bool modified = false;
        UnitTypeSettings settings = GetSettingsForType(unitType);

        // Check/add AttackSpawnPoint
        Transform spawnPoint = prefabInstance.transform.Find("AttackSpawnPoint");

        if (spawnPoint == null)
        {
            // Create new spawn point
            GameObject spawnPointObj = new GameObject("AttackSpawnPoint");
            spawnPointObj.transform.SetParent(prefabInstance.transform);
            spawnPointObj.transform.localPosition = settings.offset;
            spawnPointObj.transform.localRotation = Quaternion.identity;
            spawnPointObj.transform.localScale = Vector3.one;

            // Add a small gizmo icon for visibility in editor
            // (This is optional - adds an icon to make it visible in Scene view)

            spawnPoint = spawnPointObj.transform;
            modified = true;
            Debug.Log($"[BatchAddAttackSpawnPoints] Added AttackSpawnPoint to {prefabInstance.name} at {settings.offset}");
        }
        else if (overwriteExisting)
        {
            // Update existing position
            spawnPoint.localPosition = settings.offset;
            modified = true;
            Debug.Log($"[BatchAddAttackSpawnPoints] Updated AttackSpawnPoint on {prefabInstance.name} to {settings.offset}");
        }

        // Check/add UnitAttackEffects component
        if (addComponentIfMissing)
        {
            UnitAttackEffects attackEffects = prefabInstance.GetComponent<UnitAttackEffects>();

            if (attackEffects == null)
            {
                attackEffects = prefabInstance.AddComponent<UnitAttackEffects>();
                modified = true;
                Debug.Log($"[BatchAddAttackSpawnPoints] Added UnitAttackEffects to {prefabInstance.name}");
            }

            // Configure the component
            attackEffects.attackType = settings.attackType;
            attackEffects.muzzleFlashColor = settings.muzzleFlashColor;
            attackEffects.muzzleFlashSize = settings.muzzleFlashSize;
            attackEffects.enableRecoil = settings.enableRecoil;
            attackEffects.recoilDistance = settings.recoilDistance;
            attackEffects.attackSpawnPoint = spawnPoint;

            // Mark as modified since we updated settings
            EditorUtility.SetDirty(attackEffects);
        }

        if (modified)
        {
            PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
        }

        PrefabUtility.UnloadPrefabContents(prefabInstance);
        return modified;
    }
}
