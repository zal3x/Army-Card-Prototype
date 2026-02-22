using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Helper tool to set up War Cabinet UI elements.
/// Window → Army Prototype → War Cabinet Setup
/// </summary>
public class WarCabinetSetupHelper : EditorWindow
{
    private Vector2 scrollPos;

    [MenuItem("Window/Army Prototype/War Cabinet Setup")]
    public static void ShowWindow()
    {
        GetWindow<WarCabinetSetupHelper>("War Cabinet Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("War Cabinet Setup Helper", EditorStyles.boldLabel);
        GUILayout.Space(10);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        // Find WarCabinetScreen
        var warCabinet = FindObjectOfType<WarCabinetScreen>(true);
        if (warCabinet == null)
        {
            EditorGUILayout.HelpBox("WarCabinetScreen not found in scene!", MessageType.Error);
            EditorGUILayout.EndScrollView();
            return;
        }

        GUILayout.Label($"Found: {warCabinet.gameObject.name}", EditorStyles.boldLabel);
        GUILayout.Space(10);

        var so = new SerializedObject(warCabinet);

        // Check each reference
        GUILayout.Label("NPC Buttons", EditorStyles.boldLabel);
        CheckAndShowRef(so, "fieldRecruiterButton", "Field Recruiter Button");
        CheckAndShowRef(so, "intelligenceOfficerButton", "Intelligence Officer Button");
        CheckAndShowRef(so, "chiefOfStaffButton", "Chief of Staff Button");
        CheckAndShowRef(so, "defenseMinisterButton", "Defense Minister Button");

        GUILayout.Space(10);
        GUILayout.Label("Shop Panels", EditorStyles.boldLabel);
        CheckAndShowRef(so, "cardShopPanel", "Card Shop Panel (ShopPanel)");
        CheckAndShowRef(so, "doctrineShopPanel", "Doctrine Shop Panel (ShopPanel)");
        CheckAndShowRef(so, "perkPanel", "Perk Panel (PerkPanel)");

        GUILayout.Space(10);
        GUILayout.Label("Configuration", EditorStyles.boldLabel);
        CheckAndShowRef(so, "shopConfig", "Shop Config (ShopInventoryConfig)");

        GUILayout.Space(10);
        GUILayout.Label("HUD", EditorStyles.boldLabel);
        CheckAndShowRef(so, "warFundsText", "War Funds Text");
        CheckAndShowRef(so, "exitButton", "Exit Button");

        // Check ShopPanel internals if they exist
        var cardShopProp = so.FindProperty("cardShopPanel");
        if (cardShopProp?.objectReferenceValue != null)
        {
            GUILayout.Space(10);
            GUILayout.Label("Card Shop Panel Internals", EditorStyles.boldLabel);
            var shopPanel = cardShopProp.objectReferenceValue as ShopPanel;
            if (shopPanel != null)
            {
                CheckShopPanelInternals(shopPanel);
            }
        }

        EditorGUILayout.EndScrollView();

        GUILayout.Space(10);
        GUILayout.Label("Create Prefabs", EditorStyles.boldLabel);

        if (GUILayout.Button("Create ShopItemUI Prefab (for Cards)"))
        {
            CreateShopItemPrefab();
        }

        if (GUILayout.Button("Create DoctrineItemUI Prefab"))
        {
            CreateDoctrineItemPrefab();
        }

        if (GUILayout.Button("Create PerkItemUI Prefab"))
        {
            CreatePerkItemPrefab();
        }

        GUILayout.Space(10);
        GUILayout.Label("Create Panel Structures", EditorStyles.boldLabel);

        if (GUILayout.Button("Create ShopPanel Structure"))
        {
            CreateShopPanelStructure();
        }

        if (GUILayout.Button("Create PerkPanel Structure"))
        {
            CreatePerkPanelStructure();
        }

        GUILayout.Space(10);
        if (GUILayout.Button("Select WarCabinetScreen"))
        {
            Selection.activeGameObject = warCabinet.gameObject;
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

    private void CheckShopPanelInternals(ShopPanel panel)
    {
        var so = new SerializedObject(panel);

        CheckAndShowRef(so, "root", "  → Root");
        CheckAndShowRef(so, "titleText", "  → Title Text");
        CheckAndShowRef(so, "closeButton", "  → Close Button");
        CheckAndShowRef(so, "itemContainer", "  → Item Container");
        CheckAndShowRef(so, "itemPrefab", "  → Item Prefab (ShopItemUI)");
    }

    /// <summary>
    /// Creates a proper UI GameObject with RectTransform
    /// </summary>
    private GameObject CreateUIGameObject(string name, Transform parent = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null)
        {
            go.transform.SetParent(parent, false);
        }
        return go;
    }

    /// <summary>
    /// Creates a UI element with RectTransform and the specified component
    /// </summary>
    private T CreateUIElement<T>(Transform parent, string name) where T : Component
    {
        var go = CreateUIGameObject(name, parent);
        return go.AddComponent<T>();
    }

    private void CreateShopItemPrefab()
    {
        // Create root with RectTransform
        var go = CreateUIGameObject("ShopItemUI");
        var rootRect = go.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(200, 280);

        // Add background image first (this also ensures RectTransform)
        var bgImage = go.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

        // Add ShopItemUI component
        var itemUI = go.AddComponent<ShopItemUI>();

        // Rarity border (behind content)
        var rarityBorder = CreateUIElement<Image>(go.transform, "RarityBorder");
        rarityBorder.color = Color.gray;
        var rarityRect = rarityBorder.GetComponent<RectTransform>();
        rarityRect.anchorMin = Vector2.zero;
        rarityRect.anchorMax = Vector2.one;
        rarityRect.offsetMin = new Vector2(-3, -3);
        rarityRect.offsetMax = new Vector2(3, 3);
        rarityBorder.transform.SetAsFirstSibling();

        // Icon
        var icon = CreateUIElement<Image>(go.transform, "Icon");
        icon.color = Color.white;
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0, -15);
        iconRect.sizeDelta = new Vector2(120, 120);

        // Name text
        var nameText = CreateUIElement<TextMeshProUGUI>(go.transform, "NameText");
        nameText.text = "Card Name";
        nameText.fontSize = 16;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Center;
        var nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1);
        nameRect.anchoredPosition = new Vector2(0, -140);
        nameRect.sizeDelta = new Vector2(-10, 25);

        // Stats text
        var statsText = CreateUIElement<TextMeshProUGUI>(go.transform, "StatsText");
        statsText.text = "ATK: 5  HP: 10";
        statsText.fontSize = 14;
        statsText.alignment = TextAlignmentOptions.Center;
        var statsRect = statsText.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0, 1);
        statsRect.anchorMax = new Vector2(1, 1);
        statsRect.pivot = new Vector2(0.5f, 1);
        statsRect.anchoredPosition = new Vector2(0, -165);
        statsRect.sizeDelta = new Vector2(-10, 20);

        // Description text
        var descText = CreateUIElement<TextMeshProUGUI>(go.transform, "DescriptionText");
        descText.text = "Card description";
        descText.fontSize = 11;
        descText.alignment = TextAlignmentOptions.Center;
        descText.enableWordWrapping = true;
        var descRect = descText.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 1);
        descRect.anchorMax = new Vector2(1, 1);
        descRect.pivot = new Vector2(0.5f, 1);
        descRect.anchoredPosition = new Vector2(0, -185);
        descRect.sizeDelta = new Vector2(-10, 35);

        // Price text
        var priceText = CreateUIElement<TextMeshProUGUI>(go.transform, "PriceText");
        priceText.text = "100 WF";
        priceText.fontSize = 16;
        priceText.fontStyle = FontStyles.Bold;
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.color = Color.yellow;
        var priceRect = priceText.GetComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(0, 1);
        priceRect.anchorMax = new Vector2(1, 1);
        priceRect.pivot = new Vector2(0.5f, 1);
        priceRect.anchoredPosition = new Vector2(0, -225);
        priceRect.sizeDelta = new Vector2(-10, 25);

        // Purchase button
        var buttonGo = CreateUIGameObject("PurchaseButton", go.transform);
        var button = buttonGo.AddComponent<Button>();
        var buttonImg = buttonGo.AddComponent<Image>();
        buttonImg.color = new Color(0.2f, 0.6f, 0.2f);
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0);
        buttonRect.anchorMax = new Vector2(0.5f, 0);
        buttonRect.pivot = new Vector2(0.5f, 0);
        buttonRect.anchoredPosition = new Vector2(0, 10);
        buttonRect.sizeDelta = new Vector2(150, 35);

        var buttonText = CreateUIElement<TextMeshProUGUI>(buttonGo.transform, "Text");
        buttonText.text = "BUY";
        buttonText.fontSize = 16;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.alignment = TextAlignmentOptions.Center;
        var btnTextRect = buttonText.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        // Wire up references
        var so = new SerializedObject(itemUI);
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = descText;
        so.FindProperty("priceText").objectReferenceValue = priceText;
        so.FindProperty("statsText").objectReferenceValue = statsText;
        so.FindProperty("rarityBorder").objectReferenceValue = rarityBorder;
        so.FindProperty("purchaseButton").objectReferenceValue = button;
        so.ApplyModifiedProperties();

        // Save as prefab
        SavePrefab(go, "ShopItemUI");
    }

    private void CreateDoctrineItemPrefab()
    {
        // Create root with RectTransform
        var go = CreateUIGameObject("DoctrineItemUI");
        var rootRect = go.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(220, 300);

        // Add background
        var bgImage = go.AddComponent<Image>();
        bgImage.color = new Color(0.15f, 0.15f, 0.25f, 0.95f);

        // Add ShopItemUI component (doctrines use same component)
        var itemUI = go.AddComponent<ShopItemUI>();

        // Rarity border
        var rarityBorder = CreateUIElement<Image>(go.transform, "RarityBorder");
        rarityBorder.color = Color.blue;
        var rarityRect = rarityBorder.GetComponent<RectTransform>();
        rarityRect.anchorMin = Vector2.zero;
        rarityRect.anchorMax = Vector2.one;
        rarityRect.offsetMin = new Vector2(-4, -4);
        rarityRect.offsetMax = new Vector2(4, 4);
        rarityBorder.transform.SetAsFirstSibling();

        // Icon
        var icon = CreateUIElement<Image>(go.transform, "Icon");
        icon.color = Color.white;
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.5f, 1f);
        iconRect.anchorMax = new Vector2(0.5f, 1f);
        iconRect.pivot = new Vector2(0.5f, 1f);
        iconRect.anchoredPosition = new Vector2(0, -15);
        iconRect.sizeDelta = new Vector2(80, 80);

        // Name
        var nameText = CreateUIElement<TextMeshProUGUI>(go.transform, "NameText");
        nameText.text = "Doctrine Name";
        nameText.fontSize = 16;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Center;
        var nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0.5f, 1);
        nameRect.anchoredPosition = new Vector2(0, -100);
        nameRect.sizeDelta = new Vector2(-10, 25);

        // Stats (shows "Doctrine")
        var statsText = CreateUIElement<TextMeshProUGUI>(go.transform, "StatsText");
        statsText.text = "Doctrine";
        statsText.fontSize = 12;
        statsText.color = new Color(0.7f, 0.7f, 1f);
        statsText.alignment = TextAlignmentOptions.Center;
        var statsRect = statsText.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0, 1);
        statsRect.anchorMax = new Vector2(1, 1);
        statsRect.pivot = new Vector2(0.5f, 1);
        statsRect.anchoredPosition = new Vector2(0, -125);
        statsRect.sizeDelta = new Vector2(-10, 20);

        // Description
        var descText = CreateUIElement<TextMeshProUGUI>(go.transform, "DescriptionText");
        descText.text = "Doctrine effect description";
        descText.fontSize = 11;
        descText.alignment = TextAlignmentOptions.Center;
        descText.enableWordWrapping = true;
        var descRect = descText.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 1);
        descRect.anchorMax = new Vector2(1, 1);
        descRect.pivot = new Vector2(0.5f, 1);
        descRect.anchoredPosition = new Vector2(0, -145);
        descRect.sizeDelta = new Vector2(-10, 60);

        // Price
        var priceText = CreateUIElement<TextMeshProUGUI>(go.transform, "PriceText");
        priceText.text = "150 WF";
        priceText.fontSize = 16;
        priceText.fontStyle = FontStyles.Bold;
        priceText.alignment = TextAlignmentOptions.Center;
        priceText.color = Color.yellow;
        var priceRect = priceText.GetComponent<RectTransform>();
        priceRect.anchorMin = new Vector2(0, 1);
        priceRect.anchorMax = new Vector2(1, 1);
        priceRect.pivot = new Vector2(0.5f, 1);
        priceRect.anchoredPosition = new Vector2(0, -215);
        priceRect.sizeDelta = new Vector2(-10, 25);

        // Button
        var buttonGo = CreateUIGameObject("PurchaseButton", go.transform);
        var button = buttonGo.AddComponent<Button>();
        var buttonImg = buttonGo.AddComponent<Image>();
        buttonImg.color = new Color(0.3f, 0.3f, 0.7f);
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0);
        buttonRect.anchorMax = new Vector2(0.5f, 0);
        buttonRect.pivot = new Vector2(0.5f, 0);
        buttonRect.anchoredPosition = new Vector2(0, 10);
        buttonRect.sizeDelta = new Vector2(160, 40);

        var buttonText = CreateUIElement<TextMeshProUGUI>(buttonGo.transform, "Text");
        buttonText.text = "EQUIP";
        buttonText.fontSize = 16;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.alignment = TextAlignmentOptions.Center;
        var btnTextRect = buttonText.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        // Wire up
        var so = new SerializedObject(itemUI);
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = descText;
        so.FindProperty("priceText").objectReferenceValue = priceText;
        so.FindProperty("statsText").objectReferenceValue = statsText;
        so.FindProperty("rarityBorder").objectReferenceValue = rarityBorder;
        so.FindProperty("purchaseButton").objectReferenceValue = button;
        so.ApplyModifiedProperties();

        SavePrefab(go, "DoctrineItemUI");
    }

    private void CreatePerkItemPrefab()
    {
        // Create root with RectTransform
        var go = CreateUIGameObject("PerkItemUI");
        var rootRect = go.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(280, 150);

        // Background
        var bgImage = go.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.15f, 0.1f, 0.95f);

        // Add PerkItemUI component
        var itemUI = go.AddComponent<PerkItemUI>();

        // Icon
        var icon = CreateUIElement<Image>(go.transform, "Icon");
        icon.color = Color.white;
        var iconRect = icon.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0.5f);
        iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.pivot = new Vector2(0, 0.5f);
        iconRect.anchoredPosition = new Vector2(10, 0);
        iconRect.sizeDelta = new Vector2(60, 60);

        // Name
        var nameText = CreateUIElement<TextMeshProUGUI>(go.transform, "NameText");
        nameText.text = "Perk Name";
        nameText.fontSize = 16;
        nameText.fontStyle = FontStyles.Bold;
        nameText.alignment = TextAlignmentOptions.Left;
        var nameRect = nameText.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.pivot = new Vector2(0, 1);
        nameRect.anchoredPosition = new Vector2(80, -10);
        nameRect.sizeDelta = new Vector2(-90, 25);

        // Tier text
        var tierText = CreateUIElement<TextMeshProUGUI>(go.transform, "TierText");
        tierText.text = "Tier: 0/5";
        tierText.fontSize = 12;
        tierText.alignment = TextAlignmentOptions.Left;
        tierText.color = new Color(1f, 0.8f, 0.4f);
        var tierRect = tierText.GetComponent<RectTransform>();
        tierRect.anchorMin = new Vector2(0, 1);
        tierRect.anchorMax = new Vector2(1, 1);
        tierRect.pivot = new Vector2(0, 1);
        tierRect.anchoredPosition = new Vector2(80, -35);
        tierRect.sizeDelta = new Vector2(-90, 20);

        // Description
        var descText = CreateUIElement<TextMeshProUGUI>(go.transform, "DescriptionText");
        descText.text = "Perk description";
        descText.fontSize = 11;
        descText.alignment = TextAlignmentOptions.Left;
        descText.enableWordWrapping = true;
        var descRect = descText.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 1);
        descRect.anchorMax = new Vector2(1, 1);
        descRect.pivot = new Vector2(0, 1);
        descRect.anchoredPosition = new Vector2(80, -55);
        descRect.sizeDelta = new Vector2(-170, 35);

        // Effect text
        var effectText = CreateUIElement<TextMeshProUGUI>(go.transform, "EffectText");
        effectText.text = "+10 HP per tier";
        effectText.fontSize = 12;
        effectText.alignment = TextAlignmentOptions.Left;
        effectText.color = Color.green;
        var effectRect = effectText.GetComponent<RectTransform>();
        effectRect.anchorMin = new Vector2(0, 1);
        effectRect.anchorMax = new Vector2(1, 1);
        effectRect.pivot = new Vector2(0, 1);
        effectRect.anchoredPosition = new Vector2(80, -90);
        effectRect.sizeDelta = new Vector2(-170, 20);

        // Cost text
        var costText = CreateUIElement<TextMeshProUGUI>(go.transform, "CostText");
        costText.text = "Next: 100 WF";
        costText.fontSize = 14;
        costText.fontStyle = FontStyles.Bold;
        costText.alignment = TextAlignmentOptions.Center;
        costText.color = Color.yellow;
        var costRect = costText.GetComponent<RectTransform>();
        costRect.anchorMin = new Vector2(1, 0.5f);
        costRect.anchorMax = new Vector2(1, 0.5f);
        costRect.pivot = new Vector2(1, 0.5f);
        costRect.anchoredPosition = new Vector2(-10, 20);
        costRect.sizeDelta = new Vector2(80, 25);

        // Upgrade button
        var buttonGo = CreateUIGameObject("UpgradeButton", go.transform);
        var button = buttonGo.AddComponent<Button>();
        var buttonImg = buttonGo.AddComponent<Image>();
        buttonImg.color = new Color(0.6f, 0.4f, 0.1f);
        var buttonRect = buttonGo.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(1, 0);
        buttonRect.anchorMax = new Vector2(1, 0);
        buttonRect.pivot = new Vector2(1, 0);
        buttonRect.anchoredPosition = new Vector2(-10, 10);
        buttonRect.sizeDelta = new Vector2(80, 35);

        var buttonText = CreateUIElement<TextMeshProUGUI>(buttonGo.transform, "Text");
        buttonText.text = "UPGRADE";
        buttonText.fontSize = 12;
        buttonText.fontStyle = FontStyles.Bold;
        buttonText.alignment = TextAlignmentOptions.Center;
        var btnTextRect = buttonText.GetComponent<RectTransform>();
        btnTextRect.anchorMin = Vector2.zero;
        btnTextRect.anchorMax = Vector2.one;
        btnTextRect.offsetMin = Vector2.zero;
        btnTextRect.offsetMax = Vector2.zero;

        // Tier indicators (5 small boxes)
        var indicatorContainer = CreateUIGameObject("TierIndicators", go.transform);
        var indicatorRect = indicatorContainer.GetComponent<RectTransform>();
        indicatorRect.anchorMin = new Vector2(0, 0);
        indicatorRect.anchorMax = new Vector2(0, 0);
        indicatorRect.pivot = new Vector2(0, 0);
        indicatorRect.anchoredPosition = new Vector2(80, 10);
        indicatorRect.sizeDelta = new Vector2(100, 20);

        var hlg = indicatorContainer.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 5;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;

        Image[] tierIndicators = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var indicator = CreateUIElement<Image>(indicatorContainer.transform, $"Tier{i + 1}");
            indicator.color = Color.gray;
            var indRect = indicator.GetComponent<RectTransform>();
            indRect.sizeDelta = new Vector2(15, 15);
            tierIndicators[i] = indicator;
        }

        // Wire up
        var so = new SerializedObject(itemUI);
        so.FindProperty("iconImage").objectReferenceValue = icon;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = descText;
        so.FindProperty("tierText").objectReferenceValue = tierText;
        so.FindProperty("costText").objectReferenceValue = costText;
        so.FindProperty("effectText").objectReferenceValue = effectText;
        so.FindProperty("upgradeButton").objectReferenceValue = button;
        so.FindProperty("upgradeButtonText").objectReferenceValue = buttonText;

        // Wire tier indicators array
        var tierProp = so.FindProperty("tierIndicators");
        tierProp.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            tierProp.GetArrayElementAtIndex(i).objectReferenceValue = tierIndicators[i];
        }
        so.ApplyModifiedProperties();

        SavePrefab(go, "PerkItemUI");
    }

    private void SavePrefab(GameObject go, string prefabName)
    {
        string path = "Assets/Scripts and Game Assets/GameLogic/Overworld/UI/Prefabs";
        if (!AssetDatabase.IsValidFolder(path))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts and Game Assets/GameLogic/Overworld/UI"))
            {
                AssetDatabase.CreateFolder("Assets/Scripts and Game Assets/GameLogic/Overworld", "UI");
            }
            AssetDatabase.CreateFolder("Assets/Scripts and Game Assets/GameLogic/Overworld/UI", "Prefabs");
        }

        string prefabPath = $"{path}/{prefabName}.prefab";

        // Delete old prefab if exists
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
        {
            AssetDatabase.DeleteAsset(prefabPath);
        }

        PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
        DestroyImmediate(go);

        EditorUtility.DisplayDialog("Created",
            $"{prefabName} prefab created at:\n{prefabPath}\n\nDrag this into the appropriate panel's prefab field!", "OK");

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        EditorGUIUtility.PingObject(prefab);
    }

    private void CreateShopPanelStructure()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in scene!", "OK");
            return;
        }

        // Create ShopPanel root
        var panelGo = CreateUIGameObject("ShopPanel", canvas.transform);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.1f, 0.1f);
        panelRect.anchorMax = new Vector2(0.9f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var shopPanel = panelGo.AddComponent<ShopPanel>();
        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

        // Title
        var titleText = CreateUIElement<TextMeshProUGUI>(panelGo.transform, "TitleText");
        titleText.text = "Shop Title";
        titleText.fontSize = 28;
        titleText.alignment = TextAlignmentOptions.Center;
        var titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -10);
        titleRect.sizeDelta = new Vector2(0, 50);

        // Close Button
        var closeGo = CreateUIGameObject("CloseButton", panelGo.transform);
        var closeBtn = closeGo.AddComponent<Button>();
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.8f, 0.2f, 0.2f);
        var closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.pivot = new Vector2(1, 1);
        closeRect.anchoredPosition = new Vector2(-10, -10);
        closeRect.sizeDelta = new Vector2(40, 40);

        var closeText = CreateUIElement<TextMeshProUGUI>(closeGo.transform, "X");
        closeText.text = "X";
        closeText.fontSize = 24;
        closeText.alignment = TextAlignmentOptions.Center;
        var closeTextRect = closeText.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;

        // Scroll View
        var scrollGo = CreateUIGameObject("ScrollView", panelGo.transform);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        var scrollRectTrans = scrollGo.GetComponent<RectTransform>();
        scrollRectTrans.anchorMin = new Vector2(0, 0);
        scrollRectTrans.anchorMax = new Vector2(1, 1);
        scrollRectTrans.offsetMin = new Vector2(20, 20);
        scrollRectTrans.offsetMax = new Vector2(-20, -70);

        // Viewport (IMPORTANT - must be between ScrollView and Content)
        var viewportGo = CreateUIGameObject("Viewport", scrollGo.transform);
        var viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0.3f);
        viewportGo.AddComponent<Mask>().showMaskGraphic = true;

        // Content (item container)
        var contentGo = CreateUIGameObject("Content", viewportGo.transform);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        // GridLayoutGroup for items
        var grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(200, 280);
        grid.spacing = new Vector2(15, 15);
        grid.padding = new RectOffset(15, 15, 15, 15);
        grid.childAlignment = TextAnchor.UpperCenter;

        // ContentSizeFitter
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wire ScrollRect
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Wire ShopPanel
        var so = new SerializedObject(shopPanel);
        so.FindProperty("root").objectReferenceValue = panelGo;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("itemContainer").objectReferenceValue = contentGo.transform;
        so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = panelGo;

        EditorUtility.DisplayDialog("Created",
            "ShopPanel structure created!\n\nNow:\n1. Create ShopItemUI prefab\n2. Drag it into ShopPanel's 'Item Prefab' field", "OK");
    }

    private void CreatePerkPanelStructure()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("Error", "No Canvas found in scene!", "OK");
            return;
        }

        // Create PerkPanel root
        var panelGo = CreateUIGameObject("PerkPanel", canvas.transform);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.1f, 0.1f);
        panelRect.anchorMax = new Vector2(0.9f, 0.9f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var perkPanel = panelGo.AddComponent<PerkPanel>();
        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.1f, 0.1f, 0.95f);

        // Title
        var titleText = CreateUIElement<TextMeshProUGUI>(panelGo.transform, "TitleText");
        titleText.text = "Perks";
        titleText.fontSize = 28;
        titleText.alignment = TextAlignmentOptions.Center;
        var titleRect = titleText.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0, 1);
        titleRect.anchorMax = new Vector2(1, 1);
        titleRect.pivot = new Vector2(0.5f, 1);
        titleRect.anchoredPosition = new Vector2(0, -10);
        titleRect.sizeDelta = new Vector2(0, 50);

        // Close Button
        var closeGo = CreateUIGameObject("CloseButton", panelGo.transform);
        var closeBtn = closeGo.AddComponent<Button>();
        var closeImg = closeGo.AddComponent<Image>();
        closeImg.color = new Color(0.8f, 0.2f, 0.2f);
        var closeRect = closeGo.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1, 1);
        closeRect.anchorMax = new Vector2(1, 1);
        closeRect.pivot = new Vector2(1, 1);
        closeRect.anchoredPosition = new Vector2(-10, -10);
        closeRect.sizeDelta = new Vector2(40, 40);

        var closeText = CreateUIElement<TextMeshProUGUI>(closeGo.transform, "X");
        closeText.text = "X";
        closeText.fontSize = 24;
        closeText.alignment = TextAlignmentOptions.Center;
        var closeTextRect = closeText.GetComponent<RectTransform>();
        closeTextRect.anchorMin = Vector2.zero;
        closeTextRect.anchorMax = Vector2.one;
        closeTextRect.offsetMin = Vector2.zero;
        closeTextRect.offsetMax = Vector2.zero;

        // Scroll View
        var scrollGo = CreateUIGameObject("ScrollView", panelGo.transform);
        var scrollRect = scrollGo.AddComponent<ScrollRect>();
        var scrollRectTrans = scrollGo.GetComponent<RectTransform>();
        scrollRectTrans.anchorMin = new Vector2(0, 0);
        scrollRectTrans.anchorMax = new Vector2(1, 1);
        scrollRectTrans.offsetMin = new Vector2(20, 20);
        scrollRectTrans.offsetMax = new Vector2(-20, -70);

        // Viewport
        var viewportGo = CreateUIGameObject("Viewport", scrollGo.transform);
        var viewportRect = viewportGo.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        var viewportImg = viewportGo.AddComponent<Image>();
        viewportImg.color = new Color(0, 0, 0, 0.3f);
        viewportGo.AddComponent<Mask>().showMaskGraphic = true;

        // Content
        var contentGo = CreateUIGameObject("Content", viewportGo.transform);
        var contentRect = contentGo.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0, 0);

        // VerticalLayoutGroup for perk items (they're wider, stacked vertically)
        var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.padding = new RectOffset(15, 15, 15, 15);
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;

        // ContentSizeFitter
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Wire ScrollRect
        scrollRect.content = contentRect;
        scrollRect.viewport = viewportRect;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;

        // Wire PerkPanel
        var so = new SerializedObject(perkPanel);
        so.FindProperty("root").objectReferenceValue = panelGo;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("itemContainer").objectReferenceValue = contentGo.transform;
        so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        so.ApplyModifiedProperties();

        Selection.activeGameObject = panelGo;

        EditorUtility.DisplayDialog("Created",
            "PerkPanel structure created!\n\nNow:\n1. Create PerkItemUI prefab\n2. Drag it into PerkPanel's 'Item Prefab' field", "OK");
    }
}
