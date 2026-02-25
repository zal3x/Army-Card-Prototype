using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Helper tool to set up World Map UI elements.
/// Window → Army Prototype → World Map Setup
/// </summary>
public class WorldMapSetupHelper : EditorWindow
{
    private Vector2 scrollPos;

    [MenuItem("Window/Army Prototype/World Map Setup")]
    public static void ShowWindow()
    {
        GetWindow<WorldMapSetupHelper>("World Map Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("World Map Setup Helper", EditorStyles.boldLabel);
        GUILayout.Space(10);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Find WorldMapScreen
        var worldMap = FindAnyObjectByType<WorldMapScreen>(FindObjectsInactive.Include);
        if (worldMap == null)
        {
            EditorGUILayout.HelpBox("WorldMapScreen not found in scene!\n\nClick 'Create WorldMapScreen Structure' below to create one.", MessageType.Warning);
        }
        else
        {
            GUILayout.Label($"Found: {worldMap.gameObject.name}", EditorStyles.boldLabel);
            GUILayout.Space(10);

            var so = new SerializedObject(worldMap);

            // Check each reference
            GUILayout.Label("Map Settings", EditorStyles.boldLabel);
            CheckAndShowRef(so, "mapScrollRect", "Map Scroll Rect");
            CheckAndShowRef(so, "mapContent", "Map Content (RectTransform)");

            GUILayout.Space(10);
            GUILayout.Label("Commander Spawning", EditorStyles.boldLabel);
            CheckAndShowRef(so, "markerPrefab", "Commander Marker Prefab");
            CheckAndShowRef(so, "markerContainer", "Marker Container");

            // Check commander pool
            var commanderPool = so.FindProperty("availableCommanders");
            if (commanderPool != null)
            {
                int count = commanderPool.arraySize;
                if (count == 0)
                {
                    GUI.color = new Color(1f, 0.5f, 0.5f);
                    EditorGUILayout.HelpBox("No commanders in pool! Add CommanderDefinition assets.", MessageType.Warning);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = new Color(0.5f, 1f, 0.5f);
                    EditorGUILayout.LabelField($"✓ Commander Pool", $"{count} commanders");
                    GUI.color = Color.white;
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("HUD Elements", EditorStyles.boldLabel);
            CheckAndShowRef(so, "warFundsText", "War Funds Text");
            CheckAndShowRef(so, "barracksButton", "Barracks Button");
            CheckAndShowRef(so, "warCabinetButton", "War Cabinet Button (optional)");

            GUILayout.Space(10);
            GUILayout.Label("Commander Info Panel", EditorStyles.boldLabel);
            CheckAndShowRef(so, "commanderInfoPanel", "Info Panel Root");
            CheckAndShowRef(so, "commanderNameText", "Commander Name Text");
            CheckAndShowRef(so, "commanderDescriptionText", "Commander Description Text");
            CheckAndShowRef(so, "commanderPortraitImage", "Commander Portrait Image");
            CheckAndShowRef(so, "fightButton", "Fight Button");
            CheckAndShowRef(so, "closeInfoButton", "Close Info Button");
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.Label("Create Structures", EditorStyles.boldLabel);

        if (GUILayout.Button("Create WorldMapScreen Structure"))
        {
            CreateWorldMapStructure();
        }

        if (GUILayout.Button("Create CommanderMapMarker Prefab"))
        {
            CreateCommanderMarkerPrefab();
        }

        GUILayout.Space(10);
        GUILayout.Label("Quick Actions", EditorStyles.boldLabel);

        if (worldMap != null)
        {
            if (GUILayout.Button("Select WorldMapScreen"))
            {
                Selection.activeGameObject = worldMap.gameObject;
            }

            if (GUILayout.Button("Find & Add All Commanders to Pool"))
            {
                AddAllCommandersToPool(worldMap);
            }
        }

        // Wire to GameStateManager
        var gsm = FindAnyObjectByType<GameStateManager>();
        if (gsm != null && worldMap != null)
        {
            var gsmSo = new SerializedObject(gsm);
            var worldMapProp = gsmSo.FindProperty("worldMapScreen");
            if (worldMapProp != null && worldMapProp.objectReferenceValue == null)
            {
                if (GUILayout.Button("Wire WorldMapScreen to GameStateManager"))
                {
                    worldMapProp.objectReferenceValue = worldMap;
                    gsmSo.ApplyModifiedProperties();
                    EditorUtility.DisplayDialog("Wired", "WorldMapScreen connected to GameStateManager!", "OK");
                }
            }
        }
    }

    private void CheckAndShowRef(SerializedObject so, string propName, string displayName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null)
        {
            EditorGUILayout.HelpBox($"{displayName}: Property not found!", MessageType.Error);
            return;
        }

        if (prop.objectReferenceValue == null)
        {
            GUI.color = new Color(1f, 0.5f, 0.5f);
            EditorGUILayout.HelpBox($"{displayName}: NOT ASSIGNED", MessageType.Warning);
            GUI.color = Color.white;
        }
        else
        {
            GUI.color = new Color(0.5f, 1f, 0.5f);
            EditorGUILayout.LabelField($"✓ {displayName}", prop.objectReferenceValue.name);
            GUI.color = Color.white;
        }
    }

    private GameObject CreateUIGameObject(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        return go;
    }

    private T CreateUIElement<T>(Transform parent, string name) where T : Component
    {
        var go = CreateUIGameObject(name, parent);
        return go.AddComponent<T>();
    }

    private void CreateWorldMapStructure()
    {
        var canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in scene!", "OK");
            return;
        }

        // Check if WorldMapScreen already exists
        var existing = FindAnyObjectByType<WorldMapScreen>(FindObjectsInactive.Include);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("Already Exists",
                "WorldMapScreen already exists. Create another one anyway?", "Yes", "No"))
            {
                Selection.activeGameObject = existing.gameObject;
                return;
            }
        }

        // Create WorldMapScreen root
        var rootGo = CreateUIGameObject("WorldMapScreen", canvas.transform);
        var rootRect = rootGo.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        var worldMapScreen = rootGo.AddComponent<WorldMapScreen>();

        // Background
        var bgImage = rootGo.AddComponent<Image>();
        bgImage.color = new Color(0.1f, 0.15f, 0.2f, 1f);

        // ===== Map ScrollRect =====
        var scrollGo = CreateUIGameObject("MapScrollView", rootGo.transform);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        var scrollRectTrans = scrollGo.GetComponent<RectTransform>();
        scrollRectTrans.anchorMin = Vector2.zero;
        scrollRectTrans.anchorMax = Vector2.one;
        scrollRectTrans.offsetMin = new Vector2(0, 80);
        scrollRectTrans.offsetMax = new Vector2(0, -60);

        // Viewport
        var viewportGo = CreateUIGameObject("Viewport", scrollGo.transform);
        var viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        viewportGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
        viewportGo.AddComponent<Mask>().showMaskGraphic = false;

        // Map Content (the actual map image container)
        var contentGo = CreateUIGameObject("MapContent", viewportGo.transform);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0.5f, 0.5f);
        contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.sizeDelta = new Vector2(2000, 1000); // Large map size

        // Map background image
        var mapBg = contentGo.AddComponent<Image>();
        mapBg.color = new Color(0.2f, 0.3f, 0.4f); // Placeholder ocean color

        // Marker Container (for commander markers)
        var markerContainer = CreateUIGameObject("MarkerContainer", contentGo.transform);
        var markerContainerRect = markerContainer.GetComponent<RectTransform>();
        markerContainerRect.anchorMin = Vector2.zero;
        markerContainerRect.anchorMax = Vector2.one;
        markerContainerRect.offsetMin = Vector2.zero;
        markerContainerRect.offsetMax = Vector2.zero;

        // Wire ScrollRect
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = true;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;

        // ===== Top HUD =====
        var topHudGo = CreateUIGameObject("TopHUD", rootGo.transform);
        var topHudRect = topHudGo.GetComponent<RectTransform>();
        topHudRect.anchorMin = new Vector2(0, 1);
        topHudRect.anchorMax = new Vector2(1, 1);
        topHudRect.pivot = new Vector2(0.5f, 1);
        topHudRect.anchoredPosition = Vector2.zero;
        topHudRect.sizeDelta = new Vector2(0, 60);
        var topHudBg = topHudGo.AddComponent<Image>();
        topHudBg.color = new Color(0, 0, 0, 0.7f);

        // Title
        var titleText = CreateUIElement<TextMeshProUGUI>(topHudGo.transform, "TitleText");
        titleText.text = "WORLD MAP";
        titleText.fontSize = 28;
        titleText.fontStyle = FontStyles.Bold;
        titleText.alignment = TextAlignmentOptions.Center;
        var titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.3f, 0);
        titleRect.anchorMax = new Vector2(0.7f, 1);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        // War Funds
        var fundsText = CreateUIElement<TextMeshProUGUI>(topHudGo.transform, "WarFundsText");
        fundsText.text = "War Funds: 0";
        fundsText.fontSize = 20;
        fundsText.alignment = TextAlignmentOptions.Left;
        var fundsRect = fundsText.GetComponent<RectTransform>();
        fundsRect.anchorMin = new Vector2(0, 0);
        fundsRect.anchorMax = new Vector2(0.3f, 1);
        fundsRect.offsetMin = new Vector2(20, 0);
        fundsRect.offsetMax = Vector2.zero;

        // ===== Bottom HUD =====
        var bottomHudGo = CreateUIGameObject("BottomHUD", rootGo.transform);
        var bottomHudRect = bottomHudGo.GetComponent<RectTransform>();
        bottomHudRect.anchorMin = new Vector2(0, 0);
        bottomHudRect.anchorMax = new Vector2(1, 0);
        bottomHudRect.pivot = new Vector2(0.5f, 0);
        bottomHudRect.anchoredPosition = Vector2.zero;
        bottomHudRect.sizeDelta = new Vector2(0, 80);
        var bottomHudBg = bottomHudGo.AddComponent<Image>();
        bottomHudBg.color = new Color(0, 0, 0, 0.7f);

        // Barracks Button
        var barracksGo = CreateUIGameObject("BarracksButton", bottomHudGo.transform);
        var barracksBtn = barracksGo.AddComponent<Button>();
        var barracksBtnImg = barracksGo.AddComponent<Image>();
        barracksBtnImg.color = new Color(0.3f, 0.5f, 0.3f);
        var barracksRect = barracksGo.GetComponent<RectTransform>();
        barracksRect.anchorMin = new Vector2(0, 0.5f);
        barracksRect.anchorMax = new Vector2(0, 0.5f);
        barracksRect.pivot = new Vector2(0, 0.5f);
        barracksRect.anchoredPosition = new Vector2(20, 0);
        barracksRect.sizeDelta = new Vector2(150, 50);

        var barracksBtnText = CreateUIElement<TextMeshProUGUI>(barracksGo.transform, "Text");
        barracksBtnText.text = "BARRACKS";
        barracksBtnText.fontSize = 18;
        barracksBtnText.fontStyle = FontStyles.Bold;
        barracksBtnText.alignment = TextAlignmentOptions.Center;
        var barracksBtnTextRect = barracksBtnText.GetComponent<RectTransform>();
        barracksBtnTextRect.anchorMin = Vector2.zero;
        barracksBtnTextRect.anchorMax = Vector2.one;
        barracksBtnTextRect.offsetMin = Vector2.zero;
        barracksBtnTextRect.offsetMax = Vector2.zero;

        // War Cabinet Button (optional/debug)
        var warCabGo = CreateUIGameObject("WarCabinetButton", bottomHudGo.transform);
        var warCabBtn = warCabGo.AddComponent<Button>();
        var warCabBtnImg = warCabGo.AddComponent<Image>();
        warCabBtnImg.color = new Color(0.5f, 0.4f, 0.2f);
        var warCabRect = warCabGo.GetComponent<RectTransform>();
        warCabRect.anchorMin = new Vector2(0, 0.5f);
        warCabRect.anchorMax = new Vector2(0, 0.5f);
        warCabRect.pivot = new Vector2(0, 0.5f);
        warCabRect.anchoredPosition = new Vector2(190, 0);
        warCabRect.sizeDelta = new Vector2(150, 50);

        var warCabBtnText = CreateUIElement<TextMeshProUGUI>(warCabGo.transform, "Text");
        warCabBtnText.text = "WAR CABINET";
        warCabBtnText.fontSize = 16;
        warCabBtnText.fontStyle = FontStyles.Bold;
        warCabBtnText.alignment = TextAlignmentOptions.Center;
        var warCabBtnTextRect = warCabBtnText.GetComponent<RectTransform>();
        warCabBtnTextRect.anchorMin = Vector2.zero;
        warCabBtnTextRect.anchorMax = Vector2.one;
        warCabBtnTextRect.offsetMin = Vector2.zero;
        warCabBtnTextRect.offsetMax = Vector2.zero;

        // ===== Commander Info Panel =====
        var infoPanelGo = CreateUIGameObject("CommanderInfoPanel", rootGo.transform);
        var infoPanelRect = infoPanelGo.GetComponent<RectTransform>();
        infoPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        infoPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        infoPanelRect.pivot = new Vector2(0.5f, 0.5f);
        infoPanelRect.anchoredPosition = Vector2.zero;
        infoPanelRect.sizeDelta = new Vector2(400, 350);
        var infoPanelBg = infoPanelGo.AddComponent<Image>();
        infoPanelBg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // Close button
        var closeGo = CreateUIGameObject("CloseButton", infoPanelGo.transform);
        var closeBtn = closeGo.AddComponent<Button>();
        var closeBtnImg = closeGo.AddComponent<Image>();
        closeBtnImg.color = new Color(0.7f, 0.2f, 0.2f);
        var closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.pivot = new Vector2(1, 1);
        closeRect.anchoredPosition = new Vector2(-10, -10);
        closeRect.sizeDelta = new Vector2(35, 35);

        var closeBtnText = CreateUIElement<TextMeshProUGUI>(closeGo.transform, "X");
        closeBtnText.text = "X";
        closeBtnText.fontSize = 20;
        closeBtnText.alignment = TextAlignmentOptions.Center;
        var closeBtnTextRect = closeBtnText.GetComponent<RectTransform>();
        closeBtnTextRect.anchorMin = Vector2.zero;
        closeBtnTextRect.anchorMax = Vector2.one;
        closeBtnTextRect.offsetMin = Vector2.zero;
        closeBtnTextRect.offsetMax = Vector2.zero;

        // Portrait
        var portraitGo = CreateUIGameObject("Portrait", infoPanelGo.transform);
        var portraitImg = portraitGo.AddComponent<Image>();
        portraitImg.color = Color.gray;
        var portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.5f, 1);
        portraitRect.anchorMax = new Vector2(0.5f, 1);
        portraitRect.pivot = new Vector2(0.5f, 1);
        portraitRect.anchoredPosition = new Vector2(0, -20);
        portraitRect.sizeDelta = new Vector2(120, 120);

        // Commander Name
        var cmdNameText = CreateUIElement<TextMeshProUGUI>(infoPanelGo.transform, "CommanderNameText");
        cmdNameText.text = "Commander Name";
        cmdNameText.fontSize = 22;
        cmdNameText.fontStyle = FontStyles.Bold;
        cmdNameText.alignment = TextAlignmentOptions.Center;
        var cmdNameRect = cmdNameText.GetComponent<RectTransform>();
        cmdNameRect.anchorMin = new Vector2(0, 1);
        cmdNameRect.anchorMax = new Vector2(1, 1);
        cmdNameRect.pivot = new Vector2(0.5f, 1);
        cmdNameRect.anchoredPosition = new Vector2(0, -150);
        cmdNameRect.sizeDelta = new Vector2(-20, 30);

        // Commander Description
        var cmdDescText = CreateUIElement<TextMeshProUGUI>(infoPanelGo.transform, "CommanderDescText");
        cmdDescText.text = "Commander description and abilities...";
        cmdDescText.fontSize = 14;
        cmdDescText.alignment = TextAlignmentOptions.Center;
        cmdDescText.enableWordWrapping = true;
        var cmdDescRect = cmdDescText.GetComponent<RectTransform>();
        cmdDescRect.anchorMin = new Vector2(0, 1);
        cmdDescRect.anchorMax = new Vector2(1, 1);
        cmdDescRect.pivot = new Vector2(0.5f, 1);
        cmdDescRect.anchoredPosition = new Vector2(0, -185);
        cmdDescRect.sizeDelta = new Vector2(-30, 80);

        // Fight Button
        var fightGo = CreateUIGameObject("FightButton", infoPanelGo.transform);
        var fightBtn = fightGo.AddComponent<Button>();
        var fightBtnImg = fightGo.AddComponent<Image>();
        fightBtnImg.color = new Color(0.7f, 0.2f, 0.2f);
        var fightRect = fightGo.GetComponent<RectTransform>();
        fightRect.anchorMin = new Vector2(0.5f, 0);
        fightRect.anchorMax = new Vector2(0.5f, 0);
        fightRect.pivot = new Vector2(0.5f, 0);
        fightRect.anchoredPosition = new Vector2(0, 20);
        fightRect.sizeDelta = new Vector2(200, 50);

        var fightBtnText = CreateUIElement<TextMeshProUGUI>(fightGo.transform, "Text");
        fightBtnText.text = "FIGHT!";
        fightBtnText.fontSize = 24;
        fightBtnText.fontStyle = FontStyles.Bold;
        fightBtnText.alignment = TextAlignmentOptions.Center;
        var fightBtnTextRect = fightBtnText.GetComponent<RectTransform>();
        fightBtnTextRect.anchorMin = Vector2.zero;
        fightBtnTextRect.anchorMax = Vector2.one;
        fightBtnTextRect.offsetMin = Vector2.zero;
        fightBtnTextRect.offsetMax = Vector2.zero;

        // Hide info panel by default
        infoPanelGo.SetActive(false);

        // ===== Wire up WorldMapScreen references =====
        var so = new SerializedObject(worldMapScreen);
        so.FindProperty("root").objectReferenceValue = rootGo;
        so.FindProperty("mapScrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("mapContent").objectReferenceValue = contentRect;
        so.FindProperty("markerContainer").objectReferenceValue = markerContainer.transform;
        so.FindProperty("warFundsText").objectReferenceValue = fundsText;
        so.FindProperty("barracksButton").objectReferenceValue = barracksBtn;
        so.FindProperty("warCabinetButton").objectReferenceValue = warCabBtn;
        so.FindProperty("commanderInfoPanel").objectReferenceValue = infoPanelGo;
        so.FindProperty("commanderNameText").objectReferenceValue = cmdNameText;
        so.FindProperty("commanderDescriptionText").objectReferenceValue = cmdDescText;
        so.FindProperty("commanderPortraitImage").objectReferenceValue = portraitImg;
        so.FindProperty("fightButton").objectReferenceValue = fightBtn;
        so.FindProperty("closeInfoButton").objectReferenceValue = closeBtn;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = rootGo;

        EditorUtility.DisplayDialog("Created",
            "WorldMapScreen structure created!\n\n" +
            "Next steps:\n" +
            "1. Add a map background image to MapContent\n" +
            "2. Create CommanderMapMarker prefab\n" +
            "3. Add CommanderDefinition assets to the commander pool\n" +
            "4. Wire WorldMapScreen to GameStateManager", "OK");
    }

    private void CreateCommanderMarkerPrefab()
    {
        // Create marker root
        var go = new GameObject("CommanderMapMarker", typeof(RectTransform));
        var rootRect = go.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(80, 110);

        // Background/frame image
        var frameImage = go.AddComponent<Image>();
        frameImage.color = new Color(0.2f, 0.2f, 0.3f, 0.9f);

        // Add marker component (uses IPointerClickHandler, no Button needed)
        var marker = go.AddComponent<CommanderMapMarker>();

        // Portrait
        var portraitGo = CreateUIGameObject("Portrait", go.transform);
        var portrait = portraitGo.AddComponent<Image>();
        portrait.color = Color.gray;
        var portraitRect = portraitGo.GetComponent<RectTransform>();
        portraitRect.anchorMin = new Vector2(0.5f, 1);
        portraitRect.anchorMax = new Vector2(0.5f, 1);
        portraitRect.pivot = new Vector2(0.5f, 1);
        portraitRect.anchoredPosition = new Vector2(0, -5);
        portraitRect.sizeDelta = new Vector2(60, 60);

        // Name
        var nameText = CreateUIElement<TextMeshProUGUI>(go.transform, "NameText");
        nameText.text = "Commander";
        nameText.fontSize = 10;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Center;
        var nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0);
        nameRect.pivot = new Vector2(0.5f, 0);
        nameRect.anchoredPosition = new Vector2(0, 25);
        nameRect.sizeDelta = new Vector2(0, 20);

        // Difficulty text (stars)
        var diffText = CreateUIElement<TextMeshProUGUI>(go.transform, "DifficultyText");
        diffText.text = "★★★";
        diffText.fontSize = 12;
        diffText.alignment = TextAlignmentOptions.Center;
        diffText.color = Color.yellow;
        var diffRect = diffText.GetComponent<RectTransform>();
        diffRect.anchorMin = new Vector2(0, 0);
        diffRect.anchorMax = new Vector2(1, 0);
        diffRect.pivot = new Vector2(0.5f, 0);
        diffRect.anchoredPosition = new Vector2(0, 5);
        diffRect.sizeDelta = new Vector2(0, 18);

        // Wire up references
        var so = new SerializedObject(marker);
        so.FindProperty("portraitImage").objectReferenceValue = portrait;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("difficultyText").objectReferenceValue = diffText;
        so.FindProperty("frameImage").objectReferenceValue = frameImage;
        so.ApplyModifiedProperties();

        // Save prefab
        string path = "Assets/Scripts and Game Assets/GameLogic/Overworld/UI/Prefabs";
        if (!AssetDatabase.IsValidFolder(path))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts and Game Assets/GameLogic/Overworld/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Scripts and Game Assets/GameLogic/Overworld", "UI");
            }
            AssetDatabase.CreateFolder("Assets/Scripts and Game Assets/GameLogic/Overworld/UI", "Prefabs");
        }

        string prefabPath = $"{path}/CommanderMapMarker.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);

        EditorUtility.DisplayDialog("Created",
            $"CommanderMapMarker prefab created at:\n{prefabPath}\n\nDrag this into WorldMapScreen's 'Marker Prefab' field!", "OK");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorGUIUtility.PingObject(prefab);
    }

    private void AddAllCommandersToPool(WorldMapScreen worldMap)
    {
        var guids = AssetDatabase.FindAssets("t:CommanderDefinition");
        if (guids.Length == 0)
        {
            EditorUtility.DisplayDialog("None Found", "No CommanderDefinition assets found in project.", "OK");
            return;
        }

        var so = new SerializedObject(worldMap);
        var pool = so.FindProperty("availableCommanders");

        int added = 0;
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var commander = AssetDatabase.LoadAssetAtPath<CommanderDefinition>(path);
            if (commander != null)
            {
                // Check if already in pool
                bool exists = false;
                for (int i = 0; i < pool.arraySize; i++)
                {
                    if (pool.GetArrayElementAtIndex(i).objectReferenceValue == commander)
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                {
                    pool.InsertArrayElementAtIndex(pool.arraySize);
                    pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = commander;
                    added++;
                }
            }
        }

        so.ApplyModifiedProperties();

        EditorUtility.DisplayDialog("Complete",
            $"Added {added} commanders to pool.\nTotal: {pool.arraySize}", "OK");
    }
}
