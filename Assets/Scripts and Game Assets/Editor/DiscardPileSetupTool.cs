using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Editor tool to create and configure the DiscardPileUI prefab hierarchy.
///
/// CREATES:
/// - DiscardPileRoot (small bottom-left stack indicator, holds DiscardPileUI component)
/// - DiscardPileExpandedPanel (full-screen overlay with CardCarousel, sibling of Root)
///
/// HOW TO USE:
/// 1. Open: Window → Army Prototype → Discard Pile Setup Tool
/// 2. Select your BattleHUD Canvas (or any Canvas) as parent
/// 3. Assign card prefabs (Infantry/Vehicle/Air/Ranged)
/// 4. Click "Create Discard Pile UI"
/// 5. The ExpandedPanel is a sibling — both live directly under your Canvas
///
/// CALLING AT RUNTIME:
/// - DiscardPileUI.Instance.AddCard(unitCard)   — add destroyed card
/// - DiscardPileUI.Instance.ShowPopup()         — abilities/doctrines call this
/// - DiscardPileUI.Instance.Clear()             — call at round start
/// </summary>
public class DiscardPileSetupTool : EditorWindow
{
    [MenuItem("Window/Army Prototype/Discard Pile Setup Tool")]
    public static void ShowWindow()
    {
        GetWindow<DiscardPileSetupTool>("Discard Pile Setup");
    }

    // ========================================
    // TOOL STATE
    // ========================================

    private Transform parentTransform;
    private BuyPhaseUI_FixedHand buyPhaseUI; // used to auto-fill card prefabs

    // Card prefabs for the carousel
    private GameObject infantryCardPrefab;
    private GameObject vehicleCardPrefab;
    private GameObject airCardPrefab;
    private GameObject rangedCardPrefab;
    private GameObject specialCardPrefab;

    // Stack prefab (shown on top of the pile indicator)
    private GameObject stackCardPrefab;

    // Layout tweaks
    private bool showLayoutOptions = false;
    private Vector2 stackPanelSize   = new Vector2(130f, 175f);
    private Vector2 expandedWinSize  = new Vector2(920f, 460f);
    private float   carouselSpacing  = 165f;
    private float   baseCardScale    = 0.55f;
    private Vector2 stackPanelOffset = new Vector2(10f, 10f); // from bottom-left

    private Vector2 scrollPos;

    // ========================================
    // GUI
    // ========================================

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Discard Pile UI Setup", EditorStyles.boldLabel);
        GUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "Creates a DiscardPileUI with:\n" +
            "  • Stack panel (compact, bottom-left — shows top card + count)\n" +
            "  • Full-screen expanded overlay with a CardCarousel\n" +
            "  • All inspector references auto-wired\n\n" +
            "Parent should be a Canvas (or Canvas child).",
            MessageType.Info
        );
        GUILayout.Space(8);

        // ---- Parent ----
        GUILayout.Label("1. Parent Object", EditorStyles.boldLabel);
        parentTransform = (Transform)EditorGUILayout.ObjectField(
            "Canvas / Parent", parentTransform, typeof(Transform), true);

        if (parentTransform == null && Selection.activeGameObject != null)
        {
            if (GUILayout.Button("Use Selected Object as Parent", GUILayout.Height(22)))
                parentTransform = Selection.activeGameObject.transform;
        }

        if (parentTransform == null)
            EditorGUILayout.HelpBox("Select a Canvas in the scene and click the button above.", MessageType.Warning);

        GUILayout.Space(8);

        // ---- Card Prefabs ----
        GUILayout.Label("2. Card Prefabs (for CardCarousel)", EditorStyles.boldLabel);

        EditorGUILayout.HelpBox(
            "IMPORTANT: Assign the CardVisual child objects (e.g. CardVisualVeh, CardVisualInf), " +
            "NOT the full CardCanvas root prefabs. These are the same prefabs used by your Buy Phase carousel. " +
            "Use the button below to copy them automatically.",
            MessageType.Warning
        );
        GUILayout.Space(4);

        // Auto-fill from buy phase carousel
        if (GUILayout.Button("Copy Prefabs from Buy Phase Carousel", GUILayout.Height(24)))
            CopyPrefabsFromBuyPhase();

        GUILayout.Space(4);
        infantryCardPrefab = PrefabField("Infantry", infantryCardPrefab);
        vehicleCardPrefab  = PrefabField("Vehicle",  vehicleCardPrefab);
        airCardPrefab      = PrefabField("Air",      airCardPrefab);
        rangedCardPrefab   = PrefabField("Ranged",   rangedCardPrefab);
        specialCardPrefab  = PrefabField("Special",  specialCardPrefab);

        GUILayout.Space(4);
        stackCardPrefab = PrefabField("Stack Top Card (optional)", stackCardPrefab);

        GUILayout.Space(8);

        // ---- Layout options (collapsed by default) ----
        showLayoutOptions = EditorGUILayout.Foldout(showLayoutOptions, "3. Layout Options (Advanced)");
        if (showLayoutOptions)
        {
            EditorGUI.indentLevel++;
            stackPanelSize   = EditorGUILayout.Vector2Field("Stack Panel Size",      stackPanelSize);
            stackPanelOffset = EditorGUILayout.Vector2Field("Stack Panel Offset",    stackPanelOffset);
            expandedWinSize  = EditorGUILayout.Vector2Field("Expanded Window Size",  expandedWinSize);
            carouselSpacing  = EditorGUILayout.FloatField  ("Carousel Card Spacing", carouselSpacing);
            baseCardScale    = EditorGUILayout.FloatField  ("Carousel Base Scale",   baseCardScale);
            EditorGUI.indentLevel--;
        }

        GUILayout.Space(12);

        // ---- Create button ----
        bool canCreate = parentTransform != null;
        bool missingPrefabs = infantryCardPrefab == null || vehicleCardPrefab == null
                           || airCardPrefab == null      || rangedCardPrefab == null;
        if (missingPrefabs)
            EditorGUILayout.HelpBox("Some card prefabs are unassigned. The carousel will have empty prefab slots — assign them in Inspector after creation.", MessageType.Warning);

        GUI.enabled = canCreate;
        if (GUILayout.Button("Create Discard Pile UI", GUILayout.Height(36)))
            CreateDiscardPileUI();
        GUI.enabled = true;

        GUILayout.Space(8);
        DrawDivider();
        GUILayout.Space(8);

        // ---- Wire-up existing ----
        GUILayout.Label("Wire Up Existing", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("If you already have a DiscardPileUI in the scene, select its GameObject and use the button below to assign card prefabs to its carousel.", MessageType.None);

        if (Selection.activeGameObject != null)
        {
            DiscardPileUI existing = Selection.activeGameObject.GetComponent<DiscardPileUI>();
            if (existing != null)
            {
                EditorGUILayout.LabelField($"Selected: {Selection.activeGameObject.name}", EditorStyles.helpBox);
                if (GUILayout.Button("Wire Card Prefabs to Selected Carousel", GUILayout.Height(26)))
                    WireUpCarouselPrefabs(existing);

                GUILayout.Space(4);
                if (GUILayout.Button("Validate References", GUILayout.Height(26)))
                    ValidateReferences(existing);
            }
            else
            {
                EditorGUILayout.LabelField($"Selected object has no DiscardPileUI component.", EditorStyles.helpBox);
            }
        }

        EditorGUILayout.EndScrollView();
    }

    // ========================================
    // CREATE
    // ========================================

    private void CreateDiscardPileUI()
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Create Discard Pile UI");

        // ---- Root (stack indicator, anchored bottom-left) ----
        GameObject root = CreateUIGO("DiscardPileRoot", parentTransform);
        Undo.RegisterCreatedObjectUndo(root, "Create DiscardPileRoot");

        DiscardPileUI discardUI = root.AddComponent<DiscardPileUI>();
        root.AddComponent<CanvasGroup>();

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.zero;
        rootRect.pivot     = Vector2.zero;
        rootRect.anchoredPosition = stackPanelOffset;
        rootRect.sizeDelta = stackPanelSize;

        // ---- Stack Panel (child of root) ----
        GameObject stackPanel = CreateUIGO("StackPanel", root.transform);
        Image stackBg = stackPanel.AddComponent<Image>();
        stackBg.color = new Color(0.08f, 0.08f, 0.1f, 0.9f);
        FillRect(stackPanel.GetComponent<RectTransform>());

        // TopCardSlot
        GameObject topCardSlot = CreateUIGO("TopCardSlot", stackPanel.transform);
        RectTransform topRect = topCardSlot.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0f, 0.28f);
        topRect.anchorMax = new Vector2(1f, 1f);
        topRect.offsetMin = new Vector2(6f, 4f);
        topRect.offsetMax = new Vector2(-6f, -4f);

        // CardCountText
        GameObject countObj = CreateUIGO("CardCountText", stackPanel.transform);
        TextMeshProUGUI countTMP = countObj.AddComponent<TextMeshProUGUI>();
        countTMP.text      = "0 cards";
        countTMP.fontSize  = 11f;
        countTMP.alignment = TextAlignmentOptions.Center;
        countTMP.color     = new Color(0.85f, 0.85f, 0.85f, 1f);
        RectTransform countRect = countObj.GetComponent<RectTransform>();
        countRect.anchorMin = new Vector2(0f, 0.14f);
        countRect.anchorMax = new Vector2(1f, 0.28f);
        countRect.offsetMin = new Vector2(2f, 0f);
        countRect.offsetMax = new Vector2(-2f, 0f);

        // ExpandButton
        GameObject expandBtnGO = CreateUIGO("ExpandButton", stackPanel.transform);
        Image expandBtnImg = expandBtnGO.AddComponent<Image>();
        expandBtnImg.color = new Color(0.18f, 0.35f, 0.65f, 1f);
        Button expandBtn = expandBtnGO.AddComponent<Button>();
        var expandColors = expandBtn.colors;
        expandColors.highlightedColor = new Color(0.25f, 0.45f, 0.8f, 1f);
        expandColors.pressedColor     = new Color(0.12f, 0.25f, 0.5f, 1f);
        expandBtn.colors = expandColors;
        RectTransform expandBtnRect = expandBtnGO.GetComponent<RectTransform>();
        expandBtnRect.anchorMin = new Vector2(0f, 0f);
        expandBtnRect.anchorMax = new Vector2(1f, 0.14f);
        expandBtnRect.offsetMin = new Vector2(5f, 3f);
        expandBtnRect.offsetMax = new Vector2(-5f, -2f);

        GameObject expandBtnText = CreateUIGO("Text", expandBtnGO.transform);
        TextMeshProUGUI expandTMP = expandBtnText.AddComponent<TextMeshProUGUI>();
        expandTMP.text      = "View Discard";
        expandTMP.fontSize  = 9.5f;
        expandTMP.alignment = TextAlignmentOptions.Center;
        expandTMP.color     = Color.white;
        FillRect(expandBtnText.GetComponent<RectTransform>());

        // ---- Expanded Panel (sibling of root, full-screen overlay) ----
        GameObject expandedPanel = CreateUIGO("DiscardPileExpandedPanel", parentTransform);
        Undo.RegisterCreatedObjectUndo(expandedPanel, "Create DiscardPileExpandedPanel");

        CanvasGroup expandedCG = expandedPanel.AddComponent<CanvasGroup>();
        FillRect(expandedPanel.GetComponent<RectTransform>());

        // Dark background blocker
        GameObject bg = CreateUIGO("Background", expandedPanel.transform);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.72f);
        Button bgBtn = bg.AddComponent<Button>(); // clicking BG closes panel
        var bgColors = bgBtn.colors;
        bgColors.highlightedColor = bgColors.normalColor;
        bgColors.pressedColor     = bgColors.normalColor;
        bgBtn.colors = bgColors;
        FillRect(bg.GetComponent<RectTransform>());

        // Centered window
        GameObject window = CreateUIGO("Window", expandedPanel.transform);
        Image windowImg = window.AddComponent<Image>();
        windowImg.color = new Color(0.1f, 0.1f, 0.16f, 0.97f);
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin        = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax        = new Vector2(0.5f, 0.5f);
        windowRect.pivot            = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta        = expandedWinSize;

        float headerHeight = 52f;

        // Header
        GameObject header = CreateUIGO("Header", window.transform);
        Image headerImg = header.AddComponent<Image>();
        headerImg.color = new Color(0.07f, 0.07f, 0.12f, 1f);
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin        = new Vector2(0f, 1f);
        headerRect.anchorMax        = new Vector2(1f, 1f);
        headerRect.pivot            = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = Vector2.zero;
        headerRect.sizeDelta        = new Vector2(0f, headerHeight);

        // Title
        GameObject titleGO = CreateUIGO("TitleText", header.transform);
        TextMeshProUGUI titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
        titleTMP.text      = "Destroyed This Round";
        titleTMP.fontSize  = 19f;
        titleTMP.fontStyle = FontStyles.Bold;
        titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
        titleTMP.color     = Color.white;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(16f, 0f);
        titleRect.offsetMax = new Vector2(-62f, 0f);

        // Close button
        GameObject closeBtnGO = CreateUIGO("CloseButton", header.transform);
        Image closeBtnImg = closeBtnGO.AddComponent<Image>();
        closeBtnImg.color = new Color(0.65f, 0.18f, 0.18f, 1f);
        Button closeBtn = closeBtnGO.AddComponent<Button>();
        var closeColors = closeBtn.colors;
        closeColors.highlightedColor = new Color(0.8f, 0.25f, 0.25f, 1f);
        closeColors.pressedColor     = new Color(0.5f, 0.12f, 0.12f, 1f);
        closeBtn.colors = closeColors;
        RectTransform closeBtnRect = closeBtnGO.GetComponent<RectTransform>();
        closeBtnRect.anchorMin        = new Vector2(1f, 0f);
        closeBtnRect.anchorMax        = new Vector2(1f, 1f);
        closeBtnRect.pivot            = new Vector2(1f, 0.5f);
        closeBtnRect.anchoredPosition = new Vector2(-6f, 0f);
        closeBtnRect.sizeDelta        = new Vector2(48f, -10f);

        GameObject closeBtnTextGO = CreateUIGO("Text", closeBtnGO.transform);
        TextMeshProUGUI closeTMP = closeBtnTextGO.AddComponent<TextMeshProUGUI>();
        closeTMP.text      = "×";
        closeTMP.fontSize  = 26f;
        closeTMP.alignment = TextAlignmentOptions.Center;
        closeTMP.color     = Color.white;
        FillRect(closeBtnTextGO.GetComponent<RectTransform>());

        // Carousel container (fills window below header)
        GameObject carouselGO = CreateUIGO("CarouselContainer", window.transform);
        RectTransform carouselRect = carouselGO.GetComponent<RectTransform>();
        carouselRect.anchorMin = new Vector2(0f, 0f);
        carouselRect.anchorMax = new Vector2(1f, 1f);
        carouselRect.offsetMin = new Vector2(10f, 10f);
        carouselRect.offsetMax = new Vector2(-10f, -headerHeight);

        CardCarousel carousel = carouselGO.AddComponent<CardCarousel>();
        carousel.cardContainer             = carouselRect;
        carousel.cardSpacing               = carouselSpacing;
        carousel.baseCardScale             = baseCardScale;
        carousel.centerScale               = 1.1f;
        carousel.sideScale                 = 0.72f;
        carousel.centerYOffset             = 15f;
        carousel.sideCardSpacingMultiplier = 0.85f;
        carousel.scrollSpeed               = 12f;
        carousel.snapToCenter              = true;
        carousel.allowKeyboardInput        = false; // Avoid clashing with buy phase
        carousel.centerAlpha               = 1f;
        carousel.sideAlpha                 = 0.55f;

        // Assign card prefabs
        carousel.infantryCardPrefab = infantryCardPrefab;
        carousel.vehicleCardPrefab  = vehicleCardPrefab;
        carousel.airCardPrefab      = airCardPrefab;
        carousel.rangedCardPrefab   = rangedCardPrefab;
        carousel.specialCardPrefab  = specialCardPrefab;

        // ---- Wire up DiscardPileUI references ----
        discardUI.stackPanel          = stackPanel;
        discardUI.expandedPanel       = expandedPanel;
        discardUI.topCardSlot         = topRect;
        discardUI.stackCardPrefab     = stackCardPrefab;
        discardUI.cardCountText       = countTMP;
        discardUI.expandButton        = expandBtn;
        discardUI.expandedContent     = carouselRect;
        discardUI.expandedCardPrefab  = null; // carousel path used instead
        discardUI.closeExpandedButton = closeBtn;
        discardUI.carousel            = carousel;

        // Start hidden
        expandedPanel.SetActive(false);
        stackPanel.SetActive(false);

        // Mark dirty and select root
        EditorUtility.SetDirty(root);
        EditorUtility.SetDirty(expandedPanel);
        Selection.activeGameObject = root;

        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());

        Debug.Log(
            "[DiscardPileSetupTool] Created DiscardPileRoot and DiscardPileExpandedPanel.\n" +
            "• Call DiscardPileUI.Instance.AddCard(card) when a unit dies.\n" +
            "• Call DiscardPileUI.Instance.ShowPopup() from abilities/doctrines.\n" +
            "• Call DiscardPileUI.Instance.Clear() at round start."
        );
    }

    // ========================================
    // WIRE-UP HELPERS
    // ========================================

    private void CopyPrefabsFromBuyPhase()
    {
        // Try to find a BuyPhaseUI_FixedHand in the scene if not assigned
        if (buyPhaseUI == null)
            buyPhaseUI = Object.FindObjectOfType<BuyPhaseUI_FixedHand>();

        if (buyPhaseUI == null)
        {
            Debug.LogWarning("[DiscardPileSetupTool] No BuyPhaseUI_FixedHand found in scene. Open the battle scene first.");
            return;
        }

        CardCarousel source = buyPhaseUI.cardCarousel;
        if (source == null)
        {
            Debug.LogWarning("[DiscardPileSetupTool] BuyPhaseUI_FixedHand has no cardCarousel assigned.");
            return;
        }

        infantryCardPrefab = source.infantryCardPrefab;
        vehicleCardPrefab  = source.vehicleCardPrefab;
        airCardPrefab      = source.airCardPrefab;
        rangedCardPrefab   = source.rangedCardPrefab;
        specialCardPrefab  = source.specialCardPrefab;

        Debug.Log("[DiscardPileSetupTool] Copied card prefabs from Buy Phase carousel.");
    }

    private void WireUpCarouselPrefabs(DiscardPileUI discardUI)
    {
        if (discardUI.carousel == null)
        {
            Debug.LogWarning("[DiscardPileSetupTool] No carousel reference on DiscardPileUI.");
            return;
        }

        Undo.RecordObject(discardUI.carousel, "Wire Carousel Prefabs");
        discardUI.carousel.infantryCardPrefab = infantryCardPrefab;
        discardUI.carousel.vehicleCardPrefab  = vehicleCardPrefab;
        discardUI.carousel.airCardPrefab      = airCardPrefab;
        discardUI.carousel.rangedCardPrefab   = rangedCardPrefab;
        discardUI.carousel.specialCardPrefab  = specialCardPrefab;
        EditorUtility.SetDirty(discardUI.carousel);
        Debug.Log("[DiscardPileSetupTool] Card prefabs wired to carousel.");
    }

    private void ValidateReferences(DiscardPileUI discardUI)
    {
        var problems = new List<string>();

        if (discardUI.stackPanel         == null) problems.Add("stackPanel");
        if (discardUI.expandedPanel      == null) problems.Add("expandedPanel");
        if (discardUI.topCardSlot        == null) problems.Add("topCardSlot");
        if (discardUI.cardCountText      == null) problems.Add("cardCountText");
        if (discardUI.expandButton       == null) problems.Add("expandButton");
        if (discardUI.closeExpandedButton== null) problems.Add("closeExpandedButton");
        if (discardUI.expandedContent    == null) problems.Add("expandedContent");
        if (discardUI.carousel           == null) problems.Add("carousel (optional but recommended)");

        if (discardUI.carousel != null)
        {
            if (discardUI.carousel.infantryCardPrefab == null) problems.Add("carousel.infantryCardPrefab");
            if (discardUI.carousel.vehicleCardPrefab  == null) problems.Add("carousel.vehicleCardPrefab");
            if (discardUI.carousel.airCardPrefab      == null) problems.Add("carousel.airCardPrefab");
            if (discardUI.carousel.rangedCardPrefab   == null) problems.Add("carousel.rangedCardPrefab");
        }

        if (problems.Count == 0)
            Debug.Log("[DiscardPileSetupTool] VALIDATE: All references OK!");
        else
            Debug.LogWarning("[DiscardPileSetupTool] VALIDATE: Missing refs:\n  • " + string.Join("\n  • ", problems));
    }

    // ========================================
    // UTILITY
    // ========================================

    private static GameObject CreateUIGO(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.AddComponent<RectTransform>();
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void FillRect(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject PrefabField(string label, GameObject current)
    {
        return (GameObject)EditorGUILayout.ObjectField(label, current, typeof(GameObject), false);
    }

    private static void DrawDivider()
    {
        Rect r = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(r, new Color(0.35f, 0.35f, 0.35f, 1f));
    }
}
