using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Editor tool to help set up the Barracks tabbed interface.
/// Window > Army Prototype > Barracks Setup
/// </summary>
public class BarracksSetupHelper : EditorWindow
{
    private BarracksScreen barracksScreen;
    private Vector2 scrollPosition;
    private bool showPrefabSection = true;
    private bool showPanelSection = true;
    private bool showWiringSection = true;

    [MenuItem("Window/Army Prototype/Barracks Setup")]
    public static void ShowWindow()
    {
        var window = GetWindow<BarracksSetupHelper>("Barracks Setup");
        window.minSize = new Vector2(500, 700);
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("Barracks Tabbed Interface Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        // Find or assign BarracksScreen
        barracksScreen = (BarracksScreen)EditorGUILayout.ObjectField(
            "Barracks Screen",
            barracksScreen,
            typeof(BarracksScreen),
            true
        );

        if (barracksScreen == null)
        {
            EditorGUILayout.HelpBox(
                "Drag your BarracksScreen here or click 'Find in Scene'",
                MessageType.Info
            );

            if (GUILayout.Button("Find BarracksScreen in Scene"))
            {
                barracksScreen = FindObjectOfType<BarracksScreen>();
                if (barracksScreen == null)
                    EditorUtility.DisplayDialog("Not Found", "No BarracksScreen in scene.", "OK");
            }

            EditorGUILayout.EndScrollView();
            return;
        }

        GUILayout.Space(10);

        // Quick actions
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Select in Inspector", GUILayout.Height(30)))
        {
            Selection.activeGameObject = barracksScreen.gameObject;
        }
        if (GUILayout.Button("VALIDATE ALL", GUILayout.Height(30)))
        {
            ValidateSetup();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(15);

        // ==================== PREFAB SECTION ====================
        showPrefabSection = EditorGUILayout.Foldout(showPrefabSection, "1. CREATE PREFABS", true, EditorStyles.foldoutHeader);
        if (showPrefabSection)
        {
            EditorGUI.indentLevel++;
            DrawPrefabSection();
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(10);

        // ==================== PANEL SECTION ====================
        showPanelSection = EditorGUILayout.Foldout(showPanelSection, "2. CREATE PANELS", true, EditorStyles.foldoutHeader);
        if (showPanelSection)
        {
            EditorGUI.indentLevel++;
            DrawPanelSection();
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(10);

        // ==================== WIRING SECTION ====================
        showWiringSection = EditorGUILayout.Foldout(showWiringSection, "3. CHECK REFERENCES", true, EditorStyles.foldoutHeader);
        if (showWiringSection)
        {
            EditorGUI.indentLevel++;
            DrawWiringSection();
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawPrefabSection()
    {
        EditorGUILayout.HelpBox(
            "You need 2 NEW prefabs for the tabbed interface:\n" +
            "- BarracksDoctrineItem (for doctrine collection)\n" +
            "- BarracksPerkItem (for perk display)\n\n" +
            "You also need your existing DoctrineSlot prefab.",
            MessageType.Info
        );

        GUILayout.Space(5);

        if (GUILayout.Button("Create BarracksDoctrineItem Prefab"))
        {
            CreateDoctrineItemPrefab();
        }

        if (GUILayout.Button("Create BarracksPerkItem Prefab"))
        {
            CreatePerkItemPrefab();
        }

        if (GUILayout.Button("Create DoctrineSlot Prefab (if needed)"))
        {
            CreateDoctrineSlotPrefab();
        }
    }

    private void DrawPanelSection()
    {
        EditorGUILayout.HelpBox(
            "You need 3 panels and 3 tab buttons.\n\n" +
            "PANELS:\n" +
            "- DeckPanel (your existing deck builder content)\n" +
            "- DoctrinesPanel (owned doctrines left, equipped right)\n" +
            "- PerksPanel (list of purchased perks)\n\n" +
            "TAB BUTTONS:\n" +
            "You said you made these already - great!\n" +
            "Just wire them up in section 3.",
            MessageType.Info
        );

        GUILayout.Space(5);

        EditorGUILayout.LabelField("Panel Hierarchy:", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(
@"BarracksScreen
├── TabButtons (your 3 buttons)
│
├── DeckPanel (wrap existing content)
│   ├── CollectionScroll (left - ownedCardsContainer)
│   ├── DeckScroll (right - deckCardsContainer)
│   └── StatsPanel
│
├── DoctrinesPanel [inactive by default]
│   ├── OwnedDoctrinesScroll (left)
│   │   └── Content → ownedDoctrinesContainer
│   ├── EquippedDoctrinesContainer (right, no scroll)
│   └── EquippedDoctrinesCountText
│
└── PerksPanel [inactive by default]
    ├── NoPerksText
    └── PerksScroll
        └── Content → perksContainer",
            GUILayout.Height(220)
        );

        GUILayout.Space(10);

        EditorGUILayout.LabelField("Quick Create:", EditorStyles.boldLabel);

        if (GUILayout.Button("Create DoctrinesPanel (duplicate and modify your deck panel)"))
        {
            CreateDoctrinesPanel();
        }

        if (GUILayout.Button("Create PerksPanel"))
        {
            CreatePerksPanel();
        }
    }

    private void DrawWiringSection()
    {
        var so = new SerializedObject(barracksScreen);

        EditorGUILayout.LabelField("Tab Buttons:", EditorStyles.boldLabel);
        DrawFieldStatus(so, "deckTabButton", "Deck Tab Button");
        DrawFieldStatus(so, "doctrinesTabButton", "Doctrines Tab Button");
        DrawFieldStatus(so, "perksTabButton", "Perks Tab Button");

        GUILayout.Space(5);
        EditorGUILayout.LabelField("Tab Panels:", EditorStyles.boldLabel);
        DrawFieldStatus(so, "deckPanel", "Deck Panel");
        DrawFieldStatus(so, "doctrinesPanel", "Doctrines Panel");
        DrawFieldStatus(so, "perksPanel", "Perks Panel");

        GUILayout.Space(5);
        EditorGUILayout.LabelField("Deck Tab:", EditorStyles.boldLabel);
        DrawFieldStatus(so, "ownedCardsScroll", "Owned Cards Scroll");
        DrawFieldStatus(so, "ownedCardsContainer", "Owned Cards Container");
        DrawFieldStatus(so, "deckCardsScroll", "Deck Cards Scroll");
        DrawFieldStatus(so, "deckCardsContainer", "Deck Cards Container");
        DrawFieldStatus(so, "cardItemPrefab", "Card Item Prefab");
        DrawFieldStatus(so, "deckCountText", "Deck Count Text");
        DrawFieldStatus(so, "unitCountText", "Unit Count Text");
        DrawFieldStatus(so, "specialCountText", "Special Count Text");
        DrawFieldStatus(so, "validityText", "Validity Text");

        GUILayout.Space(5);
        EditorGUILayout.LabelField("Doctrines Tab:", EditorStyles.boldLabel);
        DrawFieldStatus(so, "ownedDoctrinesScroll", "Owned Doctrines Scroll");
        DrawFieldStatus(so, "ownedDoctrinesContainer", "Owned Doctrines Container");
        DrawFieldStatus(so, "equippedDoctrinesContainer", "Equipped Doctrines Container");
        DrawFieldStatus(so, "doctrineSlotPrefab", "Doctrine Slot Prefab");
        DrawFieldStatus(so, "doctrineItemPrefab", "Doctrine Item Prefab");
        DrawFieldStatus(so, "equippedDoctrinesCountText", "Equipped Count Text");

        GUILayout.Space(5);
        EditorGUILayout.LabelField("Perks Tab:", EditorStyles.boldLabel);
        DrawFieldStatus(so, "perksScroll", "Perks Scroll");
        DrawFieldStatus(so, "perksContainer", "Perks Container");
        DrawFieldStatus(so, "perkItemPrefab", "Perk Item Prefab");
        DrawFieldStatus(so, "noPerksText", "No Perks Text");

        GUILayout.Space(5);
        EditorGUILayout.LabelField("Navigation:", EditorStyles.boldLabel);
        DrawFieldStatus(so, "backButton", "Back Button");
        DrawFieldStatus(so, "saveButton", "Save Button");
    }

    private void DrawFieldStatus(SerializedObject so, string fieldName, string displayName)
    {
        var prop = so.FindProperty(fieldName);
        bool isSet = prop != null && prop.objectReferenceValue != null;

        EditorGUILayout.BeginHorizontal();

        var icon = isSet ? EditorGUIUtility.IconContent("d_greenLight") : EditorGUIUtility.IconContent("d_redLight");
        GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(18));

        var style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = isSet ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.3f);
        EditorGUILayout.LabelField(displayName, style);

        EditorGUILayout.EndHorizontal();
    }

    private void ValidateSetup()
    {
        var so = new SerializedObject(barracksScreen);
        var missing = new List<string>();
        var optional = new List<string>();

        // Required fields
        CheckField(so, "deckTabButton", "Deck Tab Button", missing);
        CheckField(so, "doctrinesTabButton", "Doctrines Tab Button", missing);
        CheckField(so, "perksTabButton", "Perks Tab Button", missing);
        CheckField(so, "deckPanel", "Deck Panel", missing);
        CheckField(so, "doctrinesPanel", "Doctrines Panel", missing);
        CheckField(so, "perksPanel", "Perks Panel", missing);
        CheckField(so, "ownedCardsContainer", "Owned Cards Container", missing);
        CheckField(so, "deckCardsContainer", "Deck Cards Container", missing);
        CheckField(so, "cardItemPrefab", "Card Item Prefab", missing);
        CheckField(so, "ownedDoctrinesContainer", "Owned Doctrines Container", missing);
        CheckField(so, "equippedDoctrinesContainer", "Equipped Doctrines Container", missing);
        CheckField(so, "doctrineSlotPrefab", "Doctrine Slot Prefab", missing);
        CheckField(so, "doctrineItemPrefab", "Doctrine Item Prefab", missing);
        CheckField(so, "perksContainer", "Perks Container", missing);
        CheckField(so, "perkItemPrefab", "Perk Item Prefab", missing);
        CheckField(so, "backButton", "Back Button", missing);
        CheckField(so, "saveButton", "Save Button", missing);

        // Optional but recommended
        CheckField(so, "deckCountText", "Deck Count Text", optional);
        CheckField(so, "noPerksText", "No Perks Text", optional);
        CheckField(so, "equippedDoctrinesCountText", "Equipped Count Text", optional);

        if (missing.Count == 0)
        {
            string optionalMsg = optional.Count > 0
                ? $"\n\nOptional fields missing (non-critical):\n- {string.Join("\n- ", optional)}"
                : "";

            EditorUtility.DisplayDialog(
                "Setup Valid!",
                "All required fields are assigned!\n\n" +
                "Remember to:\n" +
                "1. Set DoctrinesPanel and PerksPanel to INACTIVE\n" +
                "2. Test tab switching in Play mode" + optionalMsg,
                "OK"
            );
        }
        else
        {
            EditorUtility.DisplayDialog(
                $"Missing {missing.Count} Required Fields",
                $"Please assign:\n\n- {string.Join("\n- ", missing)}",
                "OK"
            );
        }
    }

    private void CheckField(SerializedObject so, string fieldName, string displayName, List<string> list)
    {
        var prop = so.FindProperty(fieldName);
        if (prop == null || prop.objectReferenceValue == null)
            list.Add(displayName);
    }

    private void CreateDoctrineItemPrefab()
    {
        var root = new GameObject("BarracksDoctrineItem");
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(280, 80);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.2f, 0.2f, 0.25f, 1f);

        // Icon
        var iconGO = CreateChild("Icon", root.transform);
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0);
        iconRect.anchorMax = new Vector2(0, 1);
        iconRect.offsetMin = new Vector2(5, 5);
        iconRect.offsetMax = new Vector2(70, -5);
        var iconImage = iconGO.AddComponent<Image>();
        iconImage.color = Color.gray;

        // Name
        var nameGO = CreateChild("NameText", root.transform);
        var nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.6f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(80, 0);
        nameRect.offsetMax = new Vector2(-10, -5);
        var nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = "Doctrine Name";
        nameText.fontSize = 16;
        nameText.fontStyle = FontStyles.Bold;

        // Description
        var descGO = CreateChild("DescriptionText", root.transform);
        var descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 0);
        descRect.anchorMax = new Vector2(1, 0.6f);
        descRect.offsetMin = new Vector2(80, 5);
        descRect.offsetMax = new Vector2(-10, 0);
        var descText = descGO.AddComponent<TextMeshProUGUI>();
        descText.text = "Description here...";
        descText.fontSize = 11;
        descText.color = new Color(0.7f, 0.7f, 0.7f);

        // Rarity border
        var borderGO = CreateChild("RarityBorder", root.transform);
        var borderRect = borderGO.GetComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero;
        borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2);
        borderRect.offsetMax = new Vector2(2, 2);
        var borderImage = borderGO.AddComponent<Image>();
        borderImage.color = Color.gray;
        borderImage.raycastTarget = false;
        borderGO.transform.SetAsFirstSibling();

        // Add component
        var item = root.AddComponent<BarracksDoctrineItem>();

        // Wire up
        var so = new SerializedObject(item);
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = descText;
        so.FindProperty("rarityBorder").objectReferenceValue = borderImage;
        so.ApplyModifiedProperties();

        SavePrefab(root, "BarracksDoctrineItem");
    }

    private void CreatePerkItemPrefab()
    {
        var root = new GameObject("BarracksPerkItem");
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(350, 90);

        var bg = root.AddComponent<Image>();
        bg.color = new Color(0.15f, 0.2f, 0.25f, 1f);

        // Icon
        var iconGO = CreateChild("Icon", root.transform);
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0);
        iconRect.anchorMax = new Vector2(0, 1);
        iconRect.offsetMin = new Vector2(5, 5);
        iconRect.offsetMax = new Vector2(75, -5);
        var iconImage = iconGO.AddComponent<Image>();
        iconImage.color = Color.gray;

        // Name
        var nameGO = CreateChild("NameText", root.transform);
        var nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0.7f);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.offsetMin = new Vector2(85, 0);
        nameRect.offsetMax = new Vector2(-10, -5);
        var nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = "Perk Name";
        nameText.fontSize = 16;
        nameText.fontStyle = FontStyles.Bold;

        // Description
        var descGO = CreateChild("DescriptionText", root.transform);
        var descRect = descGO.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 0.35f);
        descRect.anchorMax = new Vector2(1, 0.7f);
        descRect.offsetMin = new Vector2(85, 0);
        descRect.offsetMax = new Vector2(-10, 0);
        var descText = descGO.AddComponent<TextMeshProUGUI>();
        descText.text = "Perk description";
        descText.fontSize = 11;
        descText.color = new Color(0.7f, 0.7f, 0.7f);

        // Tier text
        var tierGO = CreateChild("TierText", root.transform);
        var tierRect = tierGO.GetComponent<RectTransform>();
        tierRect.anchorMin = new Vector2(0, 0);
        tierRect.anchorMax = new Vector2(0.4f, 0.35f);
        tierRect.offsetMin = new Vector2(85, 5);
        tierRect.offsetMax = new Vector2(0, 0);
        var tierText = tierGO.AddComponent<TextMeshProUGUI>();
        tierText.text = "Tier: 1/5";
        tierText.fontSize = 12;

        // Effect text
        var effectGO = CreateChild("EffectText", root.transform);
        var effectRect = effectGO.GetComponent<RectTransform>();
        effectRect.anchorMin = new Vector2(0.4f, 0);
        effectRect.anchorMax = new Vector2(0.7f, 0.35f);
        effectRect.offsetMin = new Vector2(0, 5);
        effectRect.offsetMax = new Vector2(0, 0);
        var effectText = effectGO.AddComponent<TextMeshProUGUI>();
        effectText.text = "+10 ATK";
        effectText.fontSize = 12;
        effectText.color = Color.yellow;

        // Tier indicators container
        var indicatorsGO = CreateChild("TierIndicators", root.transform);
        var indicatorsRect = indicatorsGO.GetComponent<RectTransform>();
        indicatorsRect.anchorMin = new Vector2(0.7f, 0);
        indicatorsRect.anchorMax = new Vector2(1, 0.35f);
        indicatorsRect.offsetMin = new Vector2(0, 5);
        indicatorsRect.offsetMax = new Vector2(-10, 0);
        var hlg = indicatorsGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 4;
        hlg.childForceExpandWidth = false;
        hlg.childAlignment = TextAnchor.MiddleLeft;

        var indicators = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            var ind = CreateChild($"Tier{i + 1}", indicatorsGO.transform);
            var indImg = ind.AddComponent<Image>();
            indImg.color = i < 1 ? Color.yellow : Color.gray;
            var le = ind.AddComponent<LayoutElement>();
            le.preferredWidth = 12;
            le.preferredHeight = 12;
            indicators[i] = indImg;
        }

        // Add component
        var item = root.AddComponent<BarracksPerkItem>();

        // Wire up
        var so = new SerializedObject(item);
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("descriptionText").objectReferenceValue = descText;
        so.FindProperty("tierText").objectReferenceValue = tierText;
        so.FindProperty("effectText").objectReferenceValue = effectText;

        var tierIndicatorsProp = so.FindProperty("tierIndicators");
        tierIndicatorsProp.arraySize = 5;
        for (int i = 0; i < 5; i++)
        {
            tierIndicatorsProp.GetArrayElementAtIndex(i).objectReferenceValue = indicators[i];
        }

        so.ApplyModifiedProperties();

        SavePrefab(root, "BarracksPerkItem");
    }

    private void CreateDoctrineSlotPrefab()
    {
        var root = new GameObject("DoctrineSlot");
        var rootRect = root.AddComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(100, 120);

        // Empty frame
        var emptyFrame = CreateChild("EmptyFrame", root.transform);
        StretchToFill(emptyFrame.GetComponent<RectTransform>());
        var emptyImage = emptyFrame.AddComponent<Image>();
        emptyImage.color = new Color(0.25f, 0.25f, 0.3f, 1f);

        // Filled frame
        var filledFrame = CreateChild("FilledFrame", root.transform);
        StretchToFill(filledFrame.GetComponent<RectTransform>());
        var filledImage = filledFrame.AddComponent<Image>();
        filledImage.color = new Color(0.3f, 0.4f, 0.5f, 1f);
        filledFrame.SetActive(false);

        // Icon
        var iconGO = CreateChild("Icon", root.transform);
        var iconRect = iconGO.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.1f, 0.3f);
        iconRect.anchorMax = new Vector2(0.9f, 0.95f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;
        var iconImage = iconGO.AddComponent<Image>();
        iconImage.color = new Color(1, 1, 1, 0.3f);

        // Slot number
        var slotNumGO = CreateChild("SlotNumberText", root.transform);
        var slotNumRect = slotNumGO.GetComponent<RectTransform>();
        slotNumRect.anchorMin = new Vector2(0, 0);
        slotNumRect.anchorMax = new Vector2(0.3f, 0.25f);
        slotNumRect.offsetMin = Vector2.zero;
        slotNumRect.offsetMax = Vector2.zero;
        var slotNumText = slotNumGO.AddComponent<TextMeshProUGUI>();
        slotNumText.text = "1";
        slotNumText.fontSize = 14;
        slotNumText.alignment = TextAlignmentOptions.Center;

        // Name text
        var nameGO = CreateChild("NameText", root.transform);
        var nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 0);
        nameRect.anchorMax = new Vector2(1, 0.25f);
        nameRect.offsetMin = new Vector2(5, 0);
        nameRect.offsetMax = new Vector2(-5, 0);
        var nameText = nameGO.AddComponent<TextMeshProUGUI>();
        nameText.text = "Empty";
        nameText.fontSize = 11;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.enableAutoSizing = true;
        nameText.fontSizeMin = 8;
        nameText.fontSizeMax = 11;

        // Add component
        var slot = root.AddComponent<DoctrineSlot>();

        // Wire up
        var so = new SerializedObject(slot);
        so.FindProperty("iconImage").objectReferenceValue = iconImage;
        so.FindProperty("nameText").objectReferenceValue = nameText;
        so.FindProperty("emptyFrame").objectReferenceValue = emptyImage;
        so.FindProperty("filledFrame").objectReferenceValue = filledImage;
        so.FindProperty("slotNumberText").objectReferenceValue = slotNumText;
        so.ApplyModifiedProperties();

        SavePrefab(root, "DoctrineSlot");
    }

    private void CreateDoctrinesPanel()
    {
        if (barracksScreen == null) return;

        var panel = new GameObject("DoctrinesPanel");
        panel.transform.SetParent(barracksScreen.transform, false);

        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Left side - owned doctrines scroll
        var ownedScroll = CreateScrollView("OwnedDoctrinesScroll", panel.transform);
        var ownedScrollRect = ownedScroll.GetComponent<RectTransform>();
        ownedScrollRect.anchorMin = new Vector2(0, 0.1f);
        ownedScrollRect.anchorMax = new Vector2(0.48f, 0.9f);
        ownedScrollRect.offsetMin = new Vector2(20, 0);
        ownedScrollRect.offsetMax = new Vector2(-10, -20);

        // Add header for left side
        var ownedHeader = CreateChild("OwnedHeader", panel.transform);
        var ownedHeaderRect = ownedHeader.GetComponent<RectTransform>();
        ownedHeaderRect.anchorMin = new Vector2(0, 0.9f);
        ownedHeaderRect.anchorMax = new Vector2(0.48f, 0.95f);
        ownedHeaderRect.offsetMin = new Vector2(20, 0);
        ownedHeaderRect.offsetMax = new Vector2(-10, 0);
        var ownedHeaderText = ownedHeader.AddComponent<TextMeshProUGUI>();
        ownedHeaderText.text = "OWNED DOCTRINES";
        ownedHeaderText.fontSize = 20;
        ownedHeaderText.alignment = TextAlignmentOptions.Center;

        // Right side - equipped container (no scroll, vertical layout)
        var equippedContainer = new GameObject("EquippedDoctrinesContainer");
        equippedContainer.transform.SetParent(panel.transform, false);
        var equippedRect = equippedContainer.AddComponent<RectTransform>();
        equippedRect.anchorMin = new Vector2(0.52f, 0.1f);
        equippedRect.anchorMax = new Vector2(1, 0.9f);
        equippedRect.offsetMin = new Vector2(10, 0);
        equippedRect.offsetMax = new Vector2(-20, -20);

        var equippedBg = equippedContainer.AddComponent<Image>();
        equippedBg.color = new Color(0.12f, 0.12f, 0.15f, 0.8f);

        var vlg = equippedContainer.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 10;
        vlg.padding = new RectOffset(15, 15, 15, 15);
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        // Add header for right side
        var equippedHeader = CreateChild("EquippedHeader", panel.transform);
        var equippedHeaderRect = equippedHeader.GetComponent<RectTransform>();
        equippedHeaderRect.anchorMin = new Vector2(0.52f, 0.9f);
        equippedHeaderRect.anchorMax = new Vector2(1, 0.95f);
        equippedHeaderRect.offsetMin = new Vector2(10, 0);
        equippedHeaderRect.offsetMax = new Vector2(-20, 0);
        var equippedHeaderText = equippedHeader.AddComponent<TextMeshProUGUI>();
        equippedHeaderText.text = "EQUIPPED (5 SLOTS)";
        equippedHeaderText.fontSize = 20;
        equippedHeaderText.alignment = TextAlignmentOptions.Center;

        // Stats text
        var statsGO = CreateChild("EquippedDoctrinesCountText", panel.transform);
        var statsRect = statsGO.GetComponent<RectTransform>();
        statsRect.anchorMin = new Vector2(0.3f, 0.02f);
        statsRect.anchorMax = new Vector2(0.7f, 0.08f);
        statsRect.offsetMin = Vector2.zero;
        statsRect.offsetMax = Vector2.zero;
        var statsText = statsGO.AddComponent<TextMeshProUGUI>();
        statsText.text = "Equipped: 0/5";
        statsText.fontSize = 18;
        statsText.alignment = TextAlignmentOptions.Center;

        panel.SetActive(false);

        // Wire up to BarracksScreen
        var so = new SerializedObject(barracksScreen);
        so.FindProperty("doctrinesPanel").objectReferenceValue = panel;
        so.FindProperty("ownedDoctrinesScroll").objectReferenceValue = ownedScroll.GetComponent<ScrollRect>();
        so.FindProperty("ownedDoctrinesContainer").objectReferenceValue = ownedScroll.GetComponentInChildren<ScrollRect>().content;
        so.FindProperty("equippedDoctrinesContainer").objectReferenceValue = equippedContainer.transform;
        so.FindProperty("equippedDoctrinesCountText").objectReferenceValue = statsText;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create Doctrines Panel");

        EditorUtility.DisplayDialog("DoctrinesPanel Created",
            "Created DoctrinesPanel with:\n" +
            "- Owned doctrines scroll (left)\n" +
            "- Equipped doctrines container (right)\n" +
            "- Stats text\n\n" +
            "Panel is set to INACTIVE.\n" +
            "References have been auto-wired!",
            "OK");

        Selection.activeGameObject = panel;
    }

    private void CreatePerksPanel()
    {
        if (barracksScreen == null) return;

        var panel = new GameObject("PerksPanel");
        panel.transform.SetParent(barracksScreen.transform, false);

        var panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // Header
        var header = CreateChild("Header", panel.transform);
        var headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0.2f, 0.9f);
        headerRect.anchorMax = new Vector2(0.8f, 0.95f);
        headerRect.offsetMin = Vector2.zero;
        headerRect.offsetMax = Vector2.zero;
        var headerText = header.AddComponent<TextMeshProUGUI>();
        headerText.text = "YOUR PERKS";
        headerText.fontSize = 24;
        headerText.alignment = TextAlignmentOptions.Center;

        // No perks text
        var noPerksGO = CreateChild("NoPerksText", panel.transform);
        var noPerksRect = noPerksGO.GetComponent<RectTransform>();
        noPerksRect.anchorMin = new Vector2(0.2f, 0.4f);
        noPerksRect.anchorMax = new Vector2(0.8f, 0.6f);
        noPerksRect.offsetMin = Vector2.zero;
        noPerksRect.offsetMax = Vector2.zero;
        var noPerksText = noPerksGO.AddComponent<TextMeshProUGUI>();
        noPerksText.text = "No perks purchased yet.\nVisit the War Cabinet to buy perks!";
        noPerksText.fontSize = 20;
        noPerksText.alignment = TextAlignmentOptions.Center;
        noPerksText.color = new Color(0.5f, 0.5f, 0.5f);

        // Perks scroll
        var perksScroll = CreateScrollView("PerksScroll", panel.transform);
        var perksScrollRect = perksScroll.GetComponent<RectTransform>();
        perksScrollRect.anchorMin = new Vector2(0.1f, 0.1f);
        perksScrollRect.anchorMax = new Vector2(0.9f, 0.88f);
        perksScrollRect.offsetMin = Vector2.zero;
        perksScrollRect.offsetMax = Vector2.zero;

        panel.SetActive(false);

        // Wire up
        var so = new SerializedObject(barracksScreen);
        so.FindProperty("perksPanel").objectReferenceValue = panel;
        so.FindProperty("perksScroll").objectReferenceValue = perksScroll.GetComponent<ScrollRect>();
        so.FindProperty("perksContainer").objectReferenceValue = perksScroll.GetComponentInChildren<ScrollRect>().content;
        so.FindProperty("noPerksText").objectReferenceValue = noPerksText;
        so.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(panel, "Create Perks Panel");

        EditorUtility.DisplayDialog("PerksPanel Created",
            "Created PerksPanel with:\n" +
            "- Header\n" +
            "- No perks text\n" +
            "- Perks scroll view\n\n" +
            "Panel is set to INACTIVE.\n" +
            "References have been auto-wired!",
            "OK");

        Selection.activeGameObject = panel;
    }

    private GameObject CreateScrollView(string name, Transform parent)
    {
        var scrollGO = new GameObject(name);
        scrollGO.transform.SetParent(parent, false);

        var scrollRect = scrollGO.AddComponent<RectTransform>();
        var scroll = scrollGO.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;

        var scrollBg = scrollGO.AddComponent<Image>();
        scrollBg.color = new Color(0.1f, 0.1f, 0.12f, 0.9f);
        scrollGO.AddComponent<Mask>().showMaskGraphic = true;

        // Viewport
        var viewport = CreateChild("Viewport", scrollGO.transform);
        StretchToFill(viewport.GetComponent<RectTransform>());

        // Content
        var content = CreateChild("Content", viewport.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0, 1);
        contentRect.anchorMax = new Vector2(1, 1);
        contentRect.pivot = new Vector2(0.5f, 1);
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 8;
        vlg.padding = new RectOffset(10, 10, 10, 10);
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;

        var csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.GetComponent<RectTransform>();
        scroll.content = contentRect;

        return scrollGO;
    }

    private GameObject CreateChild(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private void StretchToFill(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void SavePrefab(GameObject go, string name)
    {
        string folder = "Assets/Scripts and Game Assets/GameLogic/Overworld/UI/Prefabs";

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/Scripts and Game Assets/GameLogic/Overworld/UI", "Prefabs");
        }

        string path = $"{folder}/{name}.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
        {
            if (!EditorUtility.DisplayDialog("Prefab Exists", $"{name}.prefab exists. Overwrite?", "Yes", "No"))
            {
                DestroyImmediate(go);
                return;
            }
        }

        PrefabUtility.SaveAsPrefabAsset(go, path);
        DestroyImmediate(go);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        EditorGUIUtility.PingObject(prefab);

        EditorUtility.DisplayDialog("Prefab Created",
            $"Created: {path}\n\nDrag this into the BarracksScreen inspector.",
            "OK");
    }
}
