using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor tool for visualizing and placing battlefield spawn points.
///
/// LAYOUT: 3 COLUMNS + FLYING ROW per side
/// - Column 1 (closest to center): Infantry (front line)
/// - Column 2 (middle): Vehicles
/// - Column 3 (furthest from center): Ranged units
/// - Flying Row (horizontal, above): Air units
///
/// Combat flows HORIZONTALLY from edges toward center:
/// Ranged → Vehicle → Infantry → [CENTER] → Infantry → Vehicle → Ranged
/// Air units fly above in the Flying Row
/// </summary>
public class BattlefieldGridTool : EditorWindow
{
    // ========================================
    // LAYOUT SETTINGS
    // ========================================

    [Header("Screen Layout")]
    private float screenWidth = 1920f;
    private float screenHeight = 1080f;
    private float orthoSize = 5.4f; // Camera orthographic size (half-height in world units)
    private int pixelsPerUnit = 100; // Unity PPU setting
 
 
    [Header("Grid Configuration")]
    private int unitsPerColumn = 5;  // Units stacked vertically in each column
    private int columnCount = 3;     // Infantry, Vehicle, Air
    private float horizontalPadding = 0.5f; // Padding from screen edge
    private float verticalPadding = 0.5f;   // Padding from top/bottom
    private float columnSpacing = 2.0f;     // Horizontal space between columns
    private float unitSpacing = 1.2f;       // Vertical space between units in a column

    [Header("Unit Sizes (in pixels)")]
    private int infantryPixelSize = 256;
    private int vehiclePixelSize = 512;
    private int airPixelSize = 512;

    [Header("Unit Scales")]
    private float infantryScale = 1.0f;
    private float vehicleScale = 0.5f;
    private float rangedScale = 0.5f;

    [Header("Column Positions (world X offset from center line)")]
    [Tooltip("Distance from center for Infantry column (closest to middle)")]
    private float col1X = 2.0f;   // Infantry (front) - closest to center
    [Tooltip("Distance from center for Vehicle column")]
    private float col2X = 5.0f;   // Vehicle (middle)
    [Tooltip("Distance from center for Ranged column (furthest from middle)")]
    private float col3X = 8.0f;   // Ranged (back) - furthest from center

    [Header("Vertical Position")]
    [Tooltip("Y offset for the entire grid (positive = up, negative = down)")]
    private float gridCenterY = 0f;  // Center Y position of all columns

    [Header("Slot X/Y Offsets (Individual Positioning)")]
    [Tooltip("X offset for each slot in Infantry column (slot 0-4)")]
    private float[] infantrySlotOffsets = new float[5] { 0f, 0f, 0f, 0f, 0f };
    [Tooltip("Y offset for each slot in Infantry column (slot 0-4)")]
    private float[] infantrySlotOffsetsY = new float[5] { 0f, 0f, 0f, 0f, 0f };
    [Tooltip("X offset for each slot in Vehicle column (slot 0-4)")]
    private float[] vehicleSlotOffsets = new float[5] { 0f, 0f, 0f, 0f, 0f };
    [Tooltip("Y offset for each slot in Vehicle column (slot 0-4)")]
    private float[] vehicleSlotOffsetsY = new float[5] { 0f, 0f, 0f, 0f, 0f };
    [Tooltip("X offset for each slot in Ranged column (slot 0-4)")]
    private float[] rangedSlotOffsets = new float[5] { 0f, 0f, 0f, 0f, 0f };
    [Tooltip("Y offset for each slot in Ranged column (slot 0-4)")]
    private float[] rangedSlotOffsetsY = new float[5] { 0f, 0f, 0f, 0f, 0f };

    [Header("Flying Row Slot Offsets (Individual X/Y)")]
    [Tooltip("X offset for each slot in Flying row (slot 0-6)")]
    private float[] flyingSlotOffsetsX = new float[7] { 0f, 0f, 0f, 0f, 0f, 0f, 0f };
    [Tooltip("Y offset for each slot in Flying row (slot 0-6)")]
    private float[] flyingSlotOffsetsY = new float[7] { 0f, 0f, 0f, 0f, 0f, 0f, 0f };

    [Header("Flying Row (Air Units - Horizontal)")]
    [Tooltip("Enable flying row generation for Air units")]
    private bool enableFlyingRow = true;
    [Tooltip("Y position for flying row (above columns)")]
    private float flyingRowY = 3.5f;
    [Tooltip("Number of Air units in flying row")]
    private int flyingRowUnits = 5;
    [Tooltip("Horizontal spacing between Air units")]
    private float flyingUnitSpacing = 2.0f;
    [Tooltip("Scale for Air units in flying row")]
    private float flyingScale = 0.5f;

    [Header("Preview")]
    private bool showPreviewGizmos = true;
    private bool showColumnLabels = true;
    private bool showUnitSizeBoxes = true;
    private Color playerAColor = new Color(0.2f, 0.5f, 1f, 0.5f);
    private Color playerBColor = new Color(1f, 0.3f, 0.2f, 0.5f);
    private Color columnDividerColor = new Color(1f, 1f, 1f, 0.3f);
    private Color flyingRowColor = new Color(0.5f, 1f, 0.5f, 0.5f);

    // Internal state
    private Vector2 scrollPosition;
    // Positions organized by column: [column][unit in column]
    private List<Vector3> infantryPositionsA = new List<Vector3>();
    private List<Vector3> vehiclePositionsA = new List<Vector3>();
    private List<Vector3> rangedPositionsA = new List<Vector3>();
    private List<Vector3> infantryPositionsB = new List<Vector3>();
    private List<Vector3> vehiclePositionsB = new List<Vector3>();
    private List<Vector3> rangedPositionsB = new List<Vector3>();
    private List<Vector3> flyingPositionsA = new List<Vector3>();  // Air units
    private List<Vector3> flyingPositionsB = new List<Vector3>();  // Air units
    private FieldSpawner targetSpawner;
    private bool autoRefresh = true;
    private bool showSlotOffsets = false; // Foldout for slot offset sliders

    // ========================================
    // MENU & WINDOW
    // ========================================

    [MenuItem("Tools/Battlefield Grid Tool")]
    public static void ShowWindow()
    {
        var window = GetWindow<BattlefieldGridTool>("Battlefield Grid");
        window.minSize = new Vector2(400, 600);
    }

    private void OnEnable()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        LoadSettings();
        CalculatePreviewPositions();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        SaveSettings();
    }

    // ========================================
    // EDITOR WINDOW GUI
    // ========================================

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        EditorGUILayout.LabelField("Battlefield Grid Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "COLUMN-BASED LAYOUT: 3 columns + Flying Row per side\n\n" +
            "• Column 1 (center): Infantry - frontline\n" +
            "• Column 2 (middle): Vehicles\n" +
            "• Column 3 (edge): Ranged units\n" +
            "• Flying Row (above): Air units\n\n" +
            "Combat flows HORIZONTALLY toward the center line.",
            MessageType.Info);

        EditorGUILayout.Space(10);

        // ========================================
        // SCREEN & CAMERA SETTINGS
        // ========================================
        EditorGUILayout.LabelField("Screen & Camera", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        screenWidth = EditorGUILayout.FloatField("Screen Width (px)", screenWidth);
        screenHeight = EditorGUILayout.FloatField("Screen Height (px)", screenHeight);
        orthoSize = EditorGUILayout.FloatField("Camera Ortho Size", orthoSize);
        pixelsPerUnit = EditorGUILayout.IntField("Pixels Per Unit", pixelsPerUnit);

        // Calculate and show derived values
        float worldWidth = orthoSize * 2f * (screenWidth / screenHeight);
        float worldHeight = orthoSize * 2f;
        float halfWorldWidth = worldWidth / 2f;

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Derived Values:", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"  World Size: {worldWidth:F2} x {worldHeight:F2} units");
        EditorGUILayout.LabelField($"  Each half (player area): {halfWorldWidth:F2} units wide");

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(10);

        // ========================================
        // UNIT SIZE REFERENCE
        // ========================================
        EditorGUILayout.LabelField("Unit Sizes (Reference)", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        infantryPixelSize = EditorGUILayout.IntField("Infantry (pixels)", infantryPixelSize);
        vehiclePixelSize = EditorGUILayout.IntField("Vehicle (pixels)", vehiclePixelSize);
        airPixelSize = EditorGUILayout.IntField("Air (pixels)", airPixelSize);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("World Sizes at PPU " + pixelsPerUnit + ":", EditorStyles.miniLabel);
        EditorGUILayout.LabelField($"  Infantry: {(float)infantryPixelSize / pixelsPerUnit:F2} units");
        EditorGUILayout.LabelField($"  Vehicle: {(float)vehiclePixelSize / pixelsPerUnit:F2} units");
        EditorGUILayout.LabelField($"  Air: {(float)airPixelSize / pixelsPerUnit:F2} units");

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(10);

        // ========================================
        // SCALE SETTINGS
        // ========================================
        EditorGUILayout.LabelField("Unit Scales", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        infantryScale = EditorGUILayout.Slider("Infantry Scale", infantryScale, 0.25f, 2f);
        vehicleScale = EditorGUILayout.Slider("Vehicle Scale", vehicleScale, 0.25f, 2f);
        rangedScale = EditorGUILayout.Slider("Ranged Scale", rangedScale, 0.25f, 2f);

        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("Scaled World Sizes:", EditorStyles.miniLabel);
        float infScaledSize = ((float)infantryPixelSize / pixelsPerUnit) * infantryScale;
        float vehScaledSize = ((float)vehiclePixelSize / pixelsPerUnit) * vehicleScale;
        float rangedScaledSize = ((float)airPixelSize / pixelsPerUnit) * rangedScale;
        EditorGUILayout.LabelField($"  Infantry: {infScaledSize:F2} units");
        EditorGUILayout.LabelField($"  Vehicle: {vehScaledSize:F2} units");
        EditorGUILayout.LabelField($"  Ranged: {rangedScaledSize:F2} units");
        EditorGUILayout.HelpBox("Air (Flying) scale is set in the Flying Row section below.", MessageType.None);

        // Pixel-perfect scale warning
        EditorGUILayout.Space(5);
        bool hasNonPixelPerfectScale = !IsPixelPerfectScale(infantryScale) ||
                                        !IsPixelPerfectScale(vehicleScale) ||
                                        !IsPixelPerfectScale(rangedScale) ||
                                        !IsPixelPerfectScale(flyingScale);
        if (hasNonPixelPerfectScale)
        {
            EditorGUILayout.HelpBox(
                "⚠ Non-pixel-perfect scales detected!\n\n" +
                "Pixel-perfect scales: 0.25, 0.5, 1, 2, 3, 4...\n\n" +
                "Using 0.75 or 1.2 will cause pixel shimmering. " +
                "Consider using clean fractions or design assets at target size.",
                MessageType.Warning);
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(10);

        // ========================================
        // COLUMN POSITIONS
        // ========================================
        EditorGUILayout.LabelField("Column Positions (X offset from center)", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        EditorGUILayout.HelpBox("These set the BASE position for each column. Individual slot offsets are added on top.", MessageType.Info);

        col1X = EditorGUILayout.FloatField("Col 1 - Infantry (closest to center)", col1X);
        col2X = EditorGUILayout.FloatField("Col 2 - Vehicle (middle)", col2X);
        col3X = EditorGUILayout.FloatField("Col 3 - Ranged (furthest from center)", col3X);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Vertical Position", EditorStyles.boldLabel);
        gridCenterY = EditorGUILayout.Slider("Grid Center Y", gridCenterY, -5f, 5f);
        EditorGUILayout.HelpBox("Shift the entire grid up (+) or down (-) on the Y axis.", MessageType.None);

        EditorGUILayout.Space(5);
        if (GUILayout.Button("Auto-Space Columns (Even Distribution)"))
        {
            AutoSpaceColumns();
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(10);

        // ========================================
        // SLOT X/Y OFFSETS (INDIVIDUAL POSITIONING)
        // ========================================
        showSlotOffsets = EditorGUILayout.Foldout(showSlotOffsets, "Individual Slot X/Y Offsets (Positioning)", true, EditorStyles.foldoutHeader);

        if (showSlotOffsets)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.HelpBox("Adjust X and Y position for each individual slot.\nX: Positive = toward enemy | Y: Positive = up", MessageType.Info);

            // Infantry Slot Offsets
            EditorGUILayout.LabelField("Infantry Slots (Ground)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < 5; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  Slot {i}", GUILayout.Width(60));
                EditorGUILayout.LabelField("X:", GUILayout.Width(15));
                infantrySlotOffsets[i] = EditorGUILayout.Slider(infantrySlotOffsets[i], -2f, 2f);
                EditorGUILayout.LabelField("Y:", GUILayout.Width(15));
                infantrySlotOffsetsY[i] = EditorGUILayout.Slider(infantrySlotOffsetsY[i], -2f, 2f);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            // Vehicle Slot Offsets
            EditorGUILayout.LabelField("Vehicle Slots (Ground)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < 5; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  Slot {i}", GUILayout.Width(60));
                EditorGUILayout.LabelField("X:", GUILayout.Width(15));
                vehicleSlotOffsets[i] = EditorGUILayout.Slider(vehicleSlotOffsets[i], -2f, 2f);
                EditorGUILayout.LabelField("Y:", GUILayout.Width(15));
                vehicleSlotOffsetsY[i] = EditorGUILayout.Slider(vehicleSlotOffsetsY[i], -2f, 2f);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);

            // Ranged Slot Offsets
            EditorGUILayout.LabelField("Ranged Slots (Ground)", EditorStyles.miniBoldLabel);
            for (int i = 0; i < 5; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  Slot {i}", GUILayout.Width(60));
                EditorGUILayout.LabelField("X:", GUILayout.Width(15));
                rangedSlotOffsets[i] = EditorGUILayout.Slider(rangedSlotOffsets[i], -2f, 2f);
                EditorGUILayout.LabelField("Y:", GUILayout.Width(15));
                rangedSlotOffsetsY[i] = EditorGUILayout.Slider(rangedSlotOffsetsY[i], -2f, 2f);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(10);

            // Flying Row Slot Offsets (X and Y)
            EditorGUILayout.LabelField("Flying Row Slots (Air)", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox("Slot 0 is center-most.", MessageType.None);

            for (int i = 0; i < flyingRowUnits; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"  Slot {i}", GUILayout.Width(60));
                EditorGUILayout.LabelField("X:", GUILayout.Width(15));
                flyingSlotOffsetsX[i] = EditorGUILayout.Slider(flyingSlotOffsetsX[i], -3f, 3f);
                EditorGUILayout.LabelField("Y:", GUILayout.Width(15));
                flyingSlotOffsetsY[i] = EditorGUILayout.Slider(flyingSlotOffsetsY[i], -3f, 5f);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space(5);
            if (GUILayout.Button("Reset All Offsets"))
            {
                for (int i = 0; i < 5; i++)
                {
                    infantrySlotOffsets[i] = 0f;
                    infantrySlotOffsetsY[i] = 0f;
                    vehicleSlotOffsets[i] = 0f;
                    vehicleSlotOffsetsY[i] = 0f;
                    rangedSlotOffsets[i] = 0f;
                    rangedSlotOffsetsY[i] = 0f;
                }
                for (int i = 0; i < 7; i++)
                {
                    flyingSlotOffsetsX[i] = 0f;
                    flyingSlotOffsetsY[i] = 0f;
                }
                CalculatePreviewPositions();
                SceneView.RepaintAll();
            }

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);

        // ========================================
        // FLYING ROW SETTINGS
        // ========================================
        EditorGUILayout.LabelField("Flying Row (Horizontal)", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        enableFlyingRow = EditorGUILayout.Toggle("Enable Flying Row (Air)", enableFlyingRow);

        GUI.enabled = enableFlyingRow;
        EditorGUILayout.HelpBox("Horizontal row for Air units above the columns.\nAuto-assigned to FieldSpawner's airRowA/B.", MessageType.Info);

        flyingRowY = EditorGUILayout.Slider("Y Position (Height)", flyingRowY, -5f, 10f);
        flyingRowUnits = EditorGUILayout.IntSlider("Number of Units", flyingRowUnits, 3, 7);
        flyingUnitSpacing = EditorGUILayout.FloatField("Horizontal Spacing", flyingUnitSpacing);
        flyingScale = EditorGUILayout.Slider("Air Unit Scale", flyingScale, 0.25f, 2f);

        float flyingScaledSize = ((float)airPixelSize / pixelsPerUnit) * flyingScale;
        EditorGUILayout.LabelField($"  Scaled Size: {flyingScaledSize:F2} units", EditorStyles.miniLabel);

        GUI.enabled = true;
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(10);

        // ========================================
        // SPACING SETTINGS
        // ========================================
        EditorGUILayout.LabelField("Spacing", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        unitsPerColumn = EditorGUILayout.IntSlider("Units Per Column", unitsPerColumn, 3, 7);
        unitSpacing = EditorGUILayout.FloatField("Unit Spacing (vertical)", unitSpacing);
        columnSpacing = EditorGUILayout.FloatField("Column Spacing (horizontal)", columnSpacing);
        verticalPadding = EditorGUILayout.FloatField("Top/Bottom Padding", verticalPadding);

        EditorGUILayout.Space(5);
        if (GUILayout.Button("Auto-Calculate Spacing (Fit 5 units vertically)"))
        {
            AutoCalculateSpacing();
        }

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(10);

        // ========================================
        // PREVIEW SETTINGS
        // ========================================
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;

        showPreviewGizmos = EditorGUILayout.Toggle("Show Gizmos in Scene", showPreviewGizmos);
        showColumnLabels = EditorGUILayout.Toggle("Show Column Labels", showColumnLabels);
        showUnitSizeBoxes = EditorGUILayout.Toggle("Show Unit Size Boxes", showUnitSizeBoxes);
        autoRefresh = EditorGUILayout.Toggle("Auto-Refresh Preview", autoRefresh);

        playerAColor = EditorGUILayout.ColorField("Player A Color (Left)", playerAColor);
        playerBColor = EditorGUILayout.ColorField("Player B Color (Right)", playerBColor);
        columnDividerColor = EditorGUILayout.ColorField("Column Divider Color", columnDividerColor);

        EditorGUI.indentLevel--;
        EditorGUILayout.Space(10);

        // ========================================
        // ACTIONS
        // ========================================
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh Preview", GUILayout.Height(30)))
        {
            CalculatePreviewPositions();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(5);

        // Find or assign FieldSpawner
        targetSpawner = EditorGUILayout.ObjectField("Target FieldSpawner", targetSpawner, typeof(FieldSpawner), true) as FieldSpawner;

        if (targetSpawner == null)
        {
            if (GUILayout.Button("Find FieldSpawner in Scene"))
            {
                targetSpawner = FindObjectOfType<FieldSpawner>();
                if (targetSpawner == null)
                    EditorUtility.DisplayDialog("Not Found", "No FieldSpawner found in the scene.", "OK");
            }
        }

        EditorGUILayout.Space(5);

        GUI.enabled = targetSpawner != null;
        if (GUILayout.Button("Generate Spawn Point Objects", GUILayout.Height(35)))
        {
            GenerateSpawnPoints();
        }
        GUI.enabled = true;

        EditorGUILayout.Space(10);

        // ========================================
        // POSITION OUTPUT
        // ========================================
        EditorGUILayout.LabelField("Generated Positions", EditorStyles.boldLabel);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Player A (Left Side):", EditorStyles.miniLabel);

        // Infantry Column
        EditorGUILayout.LabelField("  Infantry Column (Col 1 - Front):");
        for (int i = 0; i < infantryPositionsA.Count; i++)
        {
            Vector3 pos = infantryPositionsA[i];
            EditorGUILayout.LabelField($"    Slot {i}: ({pos.x:F2}, {pos.y:F2})");
        }

        // Vehicle Column
        EditorGUILayout.LabelField("  Vehicle Column (Col 2 - Middle):");
        for (int i = 0; i < vehiclePositionsA.Count; i++)
        {
            Vector3 pos = vehiclePositionsA[i];
            EditorGUILayout.LabelField($"    Slot {i}: ({pos.x:F2}, {pos.y:F2})");
        }

        // Ranged Column
        EditorGUILayout.LabelField("  Ranged Column (Col 3 - Back):");
        for (int i = 0; i < rangedPositionsA.Count; i++)
        {
            Vector3 pos = rangedPositionsA[i];
            EditorGUILayout.LabelField($"    Slot {i}: ({pos.x:F2}, {pos.y:F2})");
        }

        // Flying Rows / Air Units (if enabled)
        if (enableFlyingRow)
        {
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Air Row A (Flying - Horizontal):", EditorStyles.miniLabel);
            for (int i = 0; i < flyingPositionsA.Count; i++)
            {
                Vector3 pos = flyingPositionsA[i];
                EditorGUILayout.LabelField($"    Slot {i}: ({pos.x:F2}, {pos.y:F2})");
            }

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Air Row B (Flying - Horizontal):", EditorStyles.miniLabel);
            for (int i = 0; i < flyingPositionsB.Count; i++)
            {
                Vector3 pos = flyingPositionsB[i];
                EditorGUILayout.LabelField($"    Slot {i}: ({pos.x:F2}, {pos.y:F2})");
            }
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();

        // Auto-refresh
        if (autoRefresh && GUI.changed)
        {
            CalculatePreviewPositions();
            SceneView.RepaintAll();
        }
    }

    // ========================================
    // CALCULATIONS
    // ========================================

    private bool IsPixelPerfectScale(float scale)
    {
        // Pixel-perfect scales are 1/4, 1/2, 1, 2, 3, 4, etc.
        float[] validScales = { 0.25f, 0.5f, 1f, 2f, 3f, 4f };
        foreach (float valid in validScales)
        {
            if (Mathf.Approximately(scale, valid))
                return true;
        }
        // Also allow integer scales
        return Mathf.Approximately(scale, Mathf.Round(scale)) && scale >= 1f;
    }

    private void AutoSpaceColumns()
    {
        float worldWidth = orthoSize * 2f * (screenWidth / screenHeight);
        float halfWidth = worldWidth / 2f;
        float usableWidth = halfWidth - horizontalPadding;

        // Distribute 3 columns evenly across each player's half
        // Column 1 (Infantry) closest to center, Column 3 (Air) at edge
        col1X = usableWidth * 0.2f;   // 20% from center
        col2X = usableWidth * 0.5f;   // 50% from center
        col3X = usableWidth * 0.8f;   // 80% from center

        CalculatePreviewPositions();
        SceneView.RepaintAll();
    }

    private void AutoCalculateSpacing()
    {
        float worldHeight = orthoSize * 2f;
        float usableHeight = worldHeight - (verticalPadding * 2f);

        // Calculate vertical spacing to fit all units in a column
        float largestUnit = Mathf.Max(
            ((float)infantryPixelSize / pixelsPerUnit) * infantryScale,
            ((float)vehiclePixelSize / pixelsPerUnit) * vehicleScale,
            ((float)airPixelSize / pixelsPerUnit) * rangedScale
        );

        // Space needed = (units - 1) * spacing + units * size
        // usableHeight = (n-1) * spacing + n * size
        // spacing = (usableHeight - n * size) / (n - 1)
        unitSpacing = (usableHeight - unitsPerColumn * largestUnit) / (unitsPerColumn - 1);
        unitSpacing = Mathf.Max(unitSpacing, 0.1f); // Minimum spacing

        CalculatePreviewPositions();
        SceneView.RepaintAll();
    }

    private void CalculatePreviewPositions()
    {
        infantryPositionsA.Clear();
        vehiclePositionsA.Clear();
        rangedPositionsA.Clear();
        infantryPositionsB.Clear();
        vehiclePositionsB.Clear();
        rangedPositionsB.Clear();
        flyingPositionsA.Clear();
        flyingPositionsB.Clear();

        float worldHeight = orthoSize * 2f;

        // Calculate vertical positions (same for all columns)
        float totalColumnHeight = (unitsPerColumn - 1) * unitSpacing;
        float startY = totalColumnHeight / 2f + gridCenterY; // Start at TOP for slot 0

        // Column X positions (offset from center)
        // Player A: columns go LEFT from center (negative X)
        // Player B: columns go RIGHT from center (positive X)

        for (int slot = 0; slot < unitsPerColumn; slot++)
        {
            float y = startY - (slot * unitSpacing); // Subtract to go DOWN (slot 0 = top, slot 4 = bottom)

            // Apply individual slot X offsets
            float infantryOffsetA = -infantrySlotOffsets[slot]; // Negative because Player A faces right
            float vehicleOffsetA = -vehicleSlotOffsets[slot];
            float rangedOffsetA = -rangedSlotOffsets[slot];

            float infantryOffsetB = infantrySlotOffsets[slot]; // Positive because Player B faces left
            float vehicleOffsetB = vehicleSlotOffsets[slot];
            float rangedOffsetB = rangedSlotOffsets[slot];

            // Apply individual slot Y offsets (same for both players)
            float infantryYOffset = infantrySlotOffsetsY[slot];
            float vehicleYOffset = vehicleSlotOffsetsY[slot];
            float rangedYOffset = rangedSlotOffsetsY[slot];

            // Player A (left side) - Infantry closest to center (smallest X magnitude)
            infantryPositionsA.Add(new Vector3(-col1X + infantryOffsetA, y + infantryYOffset, 0));
            vehiclePositionsA.Add(new Vector3(-col2X + vehicleOffsetA, y + vehicleYOffset, 0));
            rangedPositionsA.Add(new Vector3(-col3X + rangedOffsetA, y + rangedYOffset, 0));

            // Player B (right side) - Infantry closest to center (smallest X magnitude)
            infantryPositionsB.Add(new Vector3(col1X + infantryOffsetB, y + infantryYOffset, 0));
            vehiclePositionsB.Add(new Vector3(col2X + vehicleOffsetB, y + vehicleYOffset, 0));
            rangedPositionsB.Add(new Vector3(col3X + rangedOffsetB, y + rangedYOffset, 0));
        }

        // Calculate flying row positions (horizontal) for Air units
        // REVERSED: Slot 0 is center-most, slot 4 is at the edge
        if (enableFlyingRow)
        {
            float totalFlyingWidth = (flyingRowUnits - 1) * flyingUnitSpacing;
            float halfWidth = totalFlyingWidth / 2f;

            for (int i = 0; i < flyingRowUnits; i++)
            {
                // Reverse the order: slot 0 is center-most (smallest offset from col2X)
                int reversedIndex = flyingRowUnits - 1 - i;
                float localX = -halfWidth + (reversedIndex * flyingUnitSpacing); // Position relative to center

                // Apply individual slot offsets (X and Y)
                float slotOffsetX = (i < flyingSlotOffsetsX.Length) ? flyingSlotOffsetsX[i] : 0f;
                float slotOffsetY = (i < flyingSlotOffsetsY.Length) ? flyingSlotOffsetsY[i] : 0f;

                // Player A (left side) - positions go from center outward (negative X)
                // Positive X offset moves toward center (enemy side)
                float xA = -col2X + localX - slotOffsetX;
                float yA = flyingRowY + slotOffsetY;
                flyingPositionsA.Add(new Vector3(xA, yA, 0));

                // Player B (right side) - MIRROR by negating localX
                // Positive X offset moves toward center (enemy side)
                float xB = col2X - localX + slotOffsetX;
                float yB = flyingRowY + slotOffsetY;
                flyingPositionsB.Add(new Vector3(xB, yB, 0));
            }
        }
    }

    // ========================================
    // SCENE VIEW DRAWING
    // ========================================

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!showPreviewGizmos) return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.Always;

        float worldWidth = orthoSize * 2f * (screenWidth / screenHeight);
        float worldHeight = orthoSize * 2f;

        // Draw screen bounds
        Handles.color = Color.gray;
        Handles.DrawWireCube(Vector3.zero, new Vector3(worldWidth, worldHeight, 0.1f));

        // Draw center divider line (battle line between players)
        Handles.color = Color.white;
        Handles.DrawLine(new Vector3(0, -worldHeight/2, 0), new Vector3(0, worldHeight/2, 0));

        // Unit sizes for each column
        float infSize = ((float)infantryPixelSize / pixelsPerUnit) * infantryScale;
        float vehSize = ((float)vehiclePixelSize / pixelsPerUnit) * vehicleScale;
        float rangedSize = ((float)airPixelSize / pixelsPerUnit) * rangedScale; // Ranged has its own scale

        // Draw column divider lines (between unit types)
        Handles.color = columnDividerColor;
        float[] colXs = { col1X, col2X, col3X };
        string[] colNames = { "INFANTRY\n(Front)", "VEHICLE\n(Middle)", "RANGED\n(Back)" };

        // Draw dividers between columns for Player A (left side)
        for (int i = 0; i < 2; i++)
        {
            float dividerX = -(colXs[i] + colXs[i + 1]) / 2f;
            Handles.DrawDottedLine(
                new Vector3(dividerX, -worldHeight/2, 0),
                new Vector3(dividerX, worldHeight/2, 0),
                5f
            );
        }

        // Draw dividers between columns for Player B (right side)
        for (int i = 0; i < 2; i++)
        {
            float dividerX = (colXs[i] + colXs[i + 1]) / 2f;
            Handles.DrawDottedLine(
                new Vector3(dividerX, -worldHeight/2, 0),
                new Vector3(dividerX, worldHeight/2, 0),
                5f
            );
        }

        // Column labels
        if (showColumnLabels)
        {
            GUIStyle labelStyle = new GUIStyle();
            labelStyle.normal.textColor = Color.white;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.alignment = TextAnchor.MiddleCenter;
            labelStyle.fontSize = 10;

            for (int i = 0; i < 3; i++)
            {
                // Player A labels (above columns)
                Handles.Label(new Vector3(-colXs[i], worldHeight/2 - 0.5f, 0), colNames[i], labelStyle);
                // Player B labels
                Handles.Label(new Vector3(colXs[i], worldHeight/2 - 0.5f, 0), colNames[i], labelStyle);
            }
        }

        // Draw spawn positions - Infantry Column
        for (int i = 0; i < infantryPositionsA.Count; i++)
        {
            DrawUnitSlot(infantryPositionsA[i], infSize, playerAColor);
            DrawUnitSlot(infantryPositionsB[i], infSize, playerBColor);
        }

        // Draw spawn positions - Vehicle Column
        for (int i = 0; i < vehiclePositionsA.Count; i++)
        {
            DrawUnitSlot(vehiclePositionsA[i], vehSize, playerAColor);
            DrawUnitSlot(vehiclePositionsB[i], vehSize, playerBColor);
        }

        // Draw spawn positions - Ranged Column
        for (int i = 0; i < rangedPositionsA.Count; i++)
        {
            DrawUnitSlot(rangedPositionsA[i], rangedSize, playerAColor);
            DrawUnitSlot(rangedPositionsB[i], rangedSize, playerBColor);
        }

        // Draw flying rows
        if (enableFlyingRow)
        {
            float flyingSize = ((float)airPixelSize / pixelsPerUnit) * flyingScale; // Use air size as base

            // Draw horizontal divider line
            Handles.color = flyingRowColor;
            Handles.DrawDottedLine(
                new Vector3(-worldWidth/2, flyingRowY, 0),
                new Vector3(worldWidth/2, flyingRowY, 0),
                5f
            );

            // Draw labels
            if (showColumnLabels)
            {
                GUIStyle labelStyle = new GUIStyle();
                labelStyle.normal.textColor = Color.green;
                labelStyle.fontStyle = FontStyle.Bold;
                labelStyle.alignment = TextAnchor.MiddleCenter;
                labelStyle.fontSize = 10;

                // Label for Player A air row (flying)
                Handles.Label(new Vector3(-col2X, flyingRowY + 0.5f, 0), "AIR A", labelStyle);

                // Label for Player B air row (flying)
                Handles.Label(new Vector3(col2X, flyingRowY + 0.5f, 0), "AIR B", labelStyle);
            }

            // Draw spawn positions for Player A flying row
            for (int i = 0; i < flyingPositionsA.Count; i++)
            {
                DrawUnitSlot(flyingPositionsA[i], flyingSize, playerAColor);
            }

            // Draw spawn positions for Player B flying row
            for (int i = 0; i < flyingPositionsB.Count; i++)
            {
                DrawUnitSlot(flyingPositionsB[i], flyingSize, playerBColor);
            }
        }

        // Draw combat flow arrows (horizontal, toward center)
        Handles.color = new Color(1f, 1f, 0f, 0.5f);

        // Player A: arrows point RIGHT (toward center)
        DrawArrow(new Vector3(-col3X, 0, 0), new Vector3(-col2X, 0, 0));  // Ranged → Vehicle
        DrawArrow(new Vector3(-col2X, 0, 0), new Vector3(-col1X, 0, 0));  // Vehicle → Infantry
        DrawArrow(new Vector3(-col1X, 0, 0), new Vector3(-0.3f, 0, 0));   // Infantry → Center

        // Player B: arrows point LEFT (toward center)
        DrawArrow(new Vector3(col3X, 0, 0), new Vector3(col2X, 0, 0));    // Ranged → Vehicle
        DrawArrow(new Vector3(col2X, 0, 0), new Vector3(col1X, 0, 0));    // Vehicle → Infantry
        DrawArrow(new Vector3(col1X, 0, 0), new Vector3(0.3f, 0, 0));     // Infantry → Center
    }

    private void DrawUnitSlot(Vector3 position, float size, Color color)
    {
        Handles.color = color;
        if (showUnitSizeBoxes)
        {
            Handles.DrawWireCube(position, new Vector3(size, size, 0.1f));
        }
        Handles.DrawSolidDisc(position, Vector3.forward, 0.15f);
    }

    private void DrawArrow(Vector3 from, Vector3 to)
    {
        Handles.DrawLine(from, to);

        Vector3 dir = (to - from).normalized;
        Vector3 right = Vector3.Cross(dir, Vector3.forward).normalized;

        float arrowSize = 0.3f;
        Handles.DrawLine(to, to - dir * arrowSize + right * arrowSize * 0.5f);
        Handles.DrawLine(to, to - dir * arrowSize - right * arrowSize * 0.5f);
    }

    // ========================================
    // SPAWN POINT GENERATION
    // ========================================

    private void GenerateSpawnPoints()
    {
        if (targetSpawner == null)
        {
            EditorUtility.DisplayDialog("Error", "No FieldSpawner assigned!", "OK");
            return;
        }

        // Create parent objects
        Transform spawnerTransform = targetSpawner.transform;

        GameObject spawnPointsA = new GameObject("SpawnPoints_PlayerA");
        spawnPointsA.transform.SetParent(spawnerTransform);
        spawnPointsA.transform.localPosition = Vector3.zero;

        GameObject spawnPointsB = new GameObject("SpawnPoints_PlayerB");
        spawnPointsB.transform.SetParent(spawnerTransform);
        spawnPointsB.transform.localPosition = Vector3.zero;

        // Arrays to collect transforms for auto-assignment
        Transform[] infantryRowATransforms = new Transform[infantryPositionsA.Count];
        Transform[] infantryRowBTransforms = new Transform[infantryPositionsB.Count];
        Transform[] vehicleRowATransforms = new Transform[vehiclePositionsA.Count];
        Transform[] vehicleRowBTransforms = new Transform[vehiclePositionsB.Count];
        Transform[] rangedRowATransforms = new Transform[rangedPositionsA.Count];
        Transform[] rangedRowBTransforms = new Transform[rangedPositionsB.Count];
        Transform[] airRowATransforms = new Transform[flyingPositionsA.Count];  // Flying row = Air
        Transform[] airRowBTransforms = new Transform[flyingPositionsB.Count];  // Flying row = Air

        // Create Infantry Column (Col 1 - Front, closest to center)
        GameObject infantryColA = new GameObject("InfantryColumn_A");
        infantryColA.transform.SetParent(spawnPointsA.transform);
        infantryColA.transform.localPosition = Vector3.zero;

        GameObject infantryColB = new GameObject("InfantryColumn_B");
        infantryColB.transform.SetParent(spawnPointsB.transform);
        infantryColB.transform.localPosition = Vector3.zero;

        for (int slot = 0; slot < infantryPositionsA.Count; slot++)
        {
            GameObject slotA = new GameObject($"Slot_{slot}");
            slotA.transform.SetParent(infantryColA.transform);
            slotA.transform.position = infantryPositionsA[slot];
            AddScaleData(slotA, infantryScale);
            infantryRowATransforms[slot] = slotA.transform;

            GameObject slotB = new GameObject($"Slot_{slot}");
            slotB.transform.SetParent(infantryColB.transform);
            slotB.transform.position = infantryPositionsB[slot];
            AddScaleData(slotB, infantryScale);
            infantryRowBTransforms[slot] = slotB.transform;
        }

        // Create Vehicle Column (Col 2 - Middle)
        GameObject vehicleColA = new GameObject("VehicleColumn_A");
        vehicleColA.transform.SetParent(spawnPointsA.transform);
        vehicleColA.transform.localPosition = Vector3.zero;

        GameObject vehicleColB = new GameObject("VehicleColumn_B");
        vehicleColB.transform.SetParent(spawnPointsB.transform);
        vehicleColB.transform.localPosition = Vector3.zero;

        for (int slot = 0; slot < vehiclePositionsA.Count; slot++)
        {
            GameObject slotA = new GameObject($"Slot_{slot}");
            slotA.transform.SetParent(vehicleColA.transform);
            slotA.transform.position = vehiclePositionsA[slot];
            AddScaleData(slotA, vehicleScale);
            vehicleRowATransforms[slot] = slotA.transform;

            GameObject slotB = new GameObject($"Slot_{slot}");
            slotB.transform.SetParent(vehicleColB.transform);
            slotB.transform.position = vehiclePositionsB[slot];
            AddScaleData(slotB, vehicleScale);
            vehicleRowBTransforms[slot] = slotB.transform;
        }

        // Create Ranged Column (Col 3 - Back, furthest from center)
        GameObject rangedColA = new GameObject("RangedColumn_A");
        rangedColA.transform.SetParent(spawnPointsA.transform);
        rangedColA.transform.localPosition = Vector3.zero;

        GameObject rangedColB = new GameObject("RangedColumn_B");
        rangedColB.transform.SetParent(spawnPointsB.transform);
        rangedColB.transform.localPosition = Vector3.zero;

        for (int slot = 0; slot < rangedPositionsA.Count; slot++)
        {
            GameObject slotA = new GameObject($"Slot_{slot}");
            slotA.transform.SetParent(rangedColA.transform);
            slotA.transform.position = rangedPositionsA[slot];
            AddScaleData(slotA, rangedScale); // Ranged has its own scale
            rangedRowATransforms[slot] = slotA.transform;

            GameObject slotB = new GameObject($"Slot_{slot}");
            slotB.transform.SetParent(rangedColB.transform);
            slotB.transform.position = rangedPositionsB[slot];
            AddScaleData(slotB, rangedScale);
            rangedRowBTransforms[slot] = slotB.transform;
        }

        // Create Flying Row for Air units (horizontal, above columns)
        string flyingRowInfo = "";
        if (enableFlyingRow)
        {
            GameObject flyingRowA = new GameObject("AirRow_A");
            flyingRowA.transform.SetParent(spawnPointsA.transform);
            flyingRowA.transform.localPosition = Vector3.zero;

            GameObject flyingRowB = new GameObject("AirRow_B");
            flyingRowB.transform.SetParent(spawnPointsB.transform);
            flyingRowB.transform.localPosition = Vector3.zero;

            for (int slot = 0; slot < flyingPositionsA.Count; slot++)
            {
                GameObject slotA = new GameObject($"Slot_{slot}");
                slotA.transform.SetParent(flyingRowA.transform);
                slotA.transform.position = flyingPositionsA[slot];
                AddScaleData(slotA, flyingScale);
                airRowATransforms[slot] = slotA.transform;

                GameObject slotB = new GameObject($"Slot_{slot}");
                slotB.transform.SetParent(flyingRowB.transform);
                slotB.transform.position = flyingPositionsB[slot];
                AddScaleData(slotB, flyingScale);
                airRowBTransforms[slot] = slotB.transform;
            }

            flyingRowInfo = $"\n\n✓ Air Row (Flying) created\n  Scale: {flyingScale}x\n  Auto-assigned to FieldSpawner airRowA/B";
        }

        // Auto-assign transforms to FieldSpawner
        AssignTransformsToFieldSpawner(
            infantryRowATransforms, vehicleRowATransforms, rangedRowATransforms, airRowATransforms,
            infantryRowBTransforms, vehicleRowBTransforms, rangedRowBTransforms, airRowBTransforms);

        // Mark as dirty for save
        EditorUtility.SetDirty(spawnerTransform.gameObject);
        EditorUtility.SetDirty(targetSpawner);

        EditorUtility.DisplayDialog("Success",
            $"Generated spawn points under {targetSpawner.name}!\n\n" +
            "✓ Spawn points created with scale data\n" +
            "✓ Slot offsets applied (including flying X/Y)\n" +
            "✓ Automatically assigned to FieldSpawner slots\n\n" +
            "Column layout (per side):\n" +
            "• InfantryColumn - closest to center (frontline)\n" +
            "• VehicleColumn - middle\n" +
            "• RangedColumn - furthest from center (backline)\n" +
            "• AirRow (Flying) - horizontal row above\n\n" +
            "Scale values:\n" +
            $"• Infantry: {infantryScale}x\n" +
            $"• Vehicle: {vehicleScale}x\n" +
            $"• Ranged: {rangedScale}x\n" +
            $"• Air (Flying): {flyingScale}x" +
            flyingRowInfo,
            "OK");

        Selection.activeGameObject = spawnPointsA;
    }

    /// <summary>
    /// Adds scale data component to a spawn point GameObject
    /// </summary>
    private void AddScaleData(GameObject spawnPoint, float scale)
    {
        SpawnPointScaleData scaleData = spawnPoint.AddComponent<SpawnPointScaleData>();
        scaleData.scaleMultiplier = scale;
    }

    /// <summary>
    /// Automatically assigns generated spawn point transforms to the FieldSpawner's serialized arrays
    /// </summary>
    private void AssignTransformsToFieldSpawner(
        Transform[] infantryA, Transform[] vehicleA, Transform[] rangedA, Transform[] airA,
        Transform[] infantryB, Transform[] vehicleB, Transform[] rangedB, Transform[] airB)
    {
        SerializedObject serializedSpawner = new SerializedObject(targetSpawner);

        // Assign Player A rows
        AssignTransformArray(serializedSpawner, "infantryRowA", infantryA);
        AssignTransformArray(serializedSpawner, "vehicleRowA", vehicleA);
        AssignTransformArray(serializedSpawner, "rangedRowA", rangedA);   // Ranged column
        AssignTransformArray(serializedSpawner, "airRowA", airA);         // Flying row for Air

        // Assign Player B rows
        AssignTransformArray(serializedSpawner, "infantryRowB", infantryB);
        AssignTransformArray(serializedSpawner, "vehicleRowB", vehicleB);
        AssignTransformArray(serializedSpawner, "rangedRowB", rangedB);   // Ranged column
        AssignTransformArray(serializedSpawner, "airRowB", airB);         // Flying row for Air

        serializedSpawner.ApplyModifiedProperties();

        int totalSlots = infantryA.Length + vehicleA.Length + rangedA.Length + airA.Length;
        Debug.Log($"[BattlefieldGridTool] Auto-assigned {totalSlots * 2} spawn points to FieldSpawner (Infantry, Vehicle, Ranged, Air for both players)!");
    }

    /// <summary>
    /// Helper to assign an array of transforms to a serialized property
    /// </summary>
    private void AssignTransformArray(SerializedObject serializedObject, string propertyName, Transform[] transforms)
    {
        SerializedProperty arrayProperty = serializedObject.FindProperty(propertyName);

        if (arrayProperty == null)
        {
            Debug.LogError($"[BattlefieldGridTool] Could not find property '{propertyName}' on FieldSpawner");
            return;
        }

        arrayProperty.arraySize = transforms.Length;

        for (int i = 0; i < transforms.Length; i++)
        {
            SerializedProperty elementProperty = arrayProperty.GetArrayElementAtIndex(i);
            elementProperty.objectReferenceValue = transforms[i];
        }
    }

    // ========================================
    // SETTINGS PERSISTENCE
    // ========================================

    private const string PREFS_PREFIX = "BattlefieldGridTool_";

    private void SaveSettings()
    {
        // Screen & Camera
        EditorPrefs.SetFloat(PREFS_PREFIX + "screenWidth", screenWidth);
        EditorPrefs.SetFloat(PREFS_PREFIX + "screenHeight", screenHeight);
        EditorPrefs.SetFloat(PREFS_PREFIX + "orthoSize", orthoSize);
        EditorPrefs.SetInt(PREFS_PREFIX + "pixelsPerUnit", pixelsPerUnit);

        // Grid Configuration
        EditorPrefs.SetInt(PREFS_PREFIX + "unitsPerColumn", unitsPerColumn);
        EditorPrefs.SetInt(PREFS_PREFIX + "columnCount", columnCount);
        EditorPrefs.SetFloat(PREFS_PREFIX + "horizontalPadding", horizontalPadding);
        EditorPrefs.SetFloat(PREFS_PREFIX + "verticalPadding", verticalPadding);
        EditorPrefs.SetFloat(PREFS_PREFIX + "columnSpacing", columnSpacing);
        EditorPrefs.SetFloat(PREFS_PREFIX + "unitSpacing", unitSpacing);

        // Unit Sizes
        EditorPrefs.SetInt(PREFS_PREFIX + "infantryPixelSize", infantryPixelSize);
        EditorPrefs.SetInt(PREFS_PREFIX + "vehiclePixelSize", vehiclePixelSize);
        EditorPrefs.SetInt(PREFS_PREFIX + "airPixelSize", airPixelSize);

        // Unit Scales
        EditorPrefs.SetFloat(PREFS_PREFIX + "infantryScale", infantryScale);
        EditorPrefs.SetFloat(PREFS_PREFIX + "vehicleScale", vehicleScale);
        EditorPrefs.SetFloat(PREFS_PREFIX + "rangedScale", rangedScale);

        // Column Positions
        EditorPrefs.SetFloat(PREFS_PREFIX + "col1X", col1X);
        EditorPrefs.SetFloat(PREFS_PREFIX + "col2X", col2X);
        EditorPrefs.SetFloat(PREFS_PREFIX + "col3X", col3X);
        EditorPrefs.SetFloat(PREFS_PREFIX + "gridCenterY", gridCenterY);

        // Infantry Slot Offsets
        for (int i = 0; i < 5; i++)
        {
            EditorPrefs.SetFloat(PREFS_PREFIX + "infantrySlotOffsets_" + i, infantrySlotOffsets[i]);
            EditorPrefs.SetFloat(PREFS_PREFIX + "infantrySlotOffsetsY_" + i, infantrySlotOffsetsY[i]);
        }

        // Vehicle Slot Offsets
        for (int i = 0; i < 5; i++)
        {
            EditorPrefs.SetFloat(PREFS_PREFIX + "vehicleSlotOffsets_" + i, vehicleSlotOffsets[i]);
            EditorPrefs.SetFloat(PREFS_PREFIX + "vehicleSlotOffsetsY_" + i, vehicleSlotOffsetsY[i]);
        }

        // Ranged Slot Offsets
        for (int i = 0; i < 5; i++)
        {
            EditorPrefs.SetFloat(PREFS_PREFIX + "rangedSlotOffsets_" + i, rangedSlotOffsets[i]);
            EditorPrefs.SetFloat(PREFS_PREFIX + "rangedSlotOffsetsY_" + i, rangedSlotOffsetsY[i]);
        }

        // Flying Row Slot Offsets
        for (int i = 0; i < 7; i++)
        {
            EditorPrefs.SetFloat(PREFS_PREFIX + "flyingSlotOffsetsX_" + i, flyingSlotOffsetsX[i]);
            EditorPrefs.SetFloat(PREFS_PREFIX + "flyingSlotOffsetsY_" + i, flyingSlotOffsetsY[i]);
        }

        // Flying Row Settings
        EditorPrefs.SetBool(PREFS_PREFIX + "enableFlyingRow", enableFlyingRow);
        EditorPrefs.SetFloat(PREFS_PREFIX + "flyingRowY", flyingRowY);
        EditorPrefs.SetInt(PREFS_PREFIX + "flyingRowUnits", flyingRowUnits);
        EditorPrefs.SetFloat(PREFS_PREFIX + "flyingUnitSpacing", flyingUnitSpacing);
        EditorPrefs.SetFloat(PREFS_PREFIX + "flyingScale", flyingScale);

        // Preview Settings
        EditorPrefs.SetBool(PREFS_PREFIX + "showPreviewGizmos", showPreviewGizmos);
        EditorPrefs.SetBool(PREFS_PREFIX + "showColumnLabels", showColumnLabels);
        EditorPrefs.SetBool(PREFS_PREFIX + "showUnitSizeBoxes", showUnitSizeBoxes);
        EditorPrefs.SetBool(PREFS_PREFIX + "autoRefresh", autoRefresh);
        EditorPrefs.SetBool(PREFS_PREFIX + "showSlotOffsets", showSlotOffsets);

        // Colors (saved as RGBA string)
        EditorPrefs.SetString(PREFS_PREFIX + "playerAColor", ColorToString(playerAColor));
        EditorPrefs.SetString(PREFS_PREFIX + "playerBColor", ColorToString(playerBColor));
        EditorPrefs.SetString(PREFS_PREFIX + "columnDividerColor", ColorToString(columnDividerColor));
        EditorPrefs.SetString(PREFS_PREFIX + "flyingRowColor", ColorToString(flyingRowColor));
    }

    private void LoadSettings()
    {
        // Check if settings exist
        if (!EditorPrefs.HasKey(PREFS_PREFIX + "screenWidth"))
        {
            // First time opening the tool, use defaults
            return;
        }

        // Screen & Camera
        screenWidth = EditorPrefs.GetFloat(PREFS_PREFIX + "screenWidth", screenWidth);
        screenHeight = EditorPrefs.GetFloat(PREFS_PREFIX + "screenHeight", screenHeight);
        orthoSize = EditorPrefs.GetFloat(PREFS_PREFIX + "orthoSize", orthoSize);
        pixelsPerUnit = EditorPrefs.GetInt(PREFS_PREFIX + "pixelsPerUnit", pixelsPerUnit);

        // Grid Configuration
        unitsPerColumn = EditorPrefs.GetInt(PREFS_PREFIX + "unitsPerColumn", unitsPerColumn);
        columnCount = EditorPrefs.GetInt(PREFS_PREFIX + "columnCount", columnCount);
        horizontalPadding = EditorPrefs.GetFloat(PREFS_PREFIX + "horizontalPadding", horizontalPadding);
        verticalPadding = EditorPrefs.GetFloat(PREFS_PREFIX + "verticalPadding", verticalPadding);
        columnSpacing = EditorPrefs.GetFloat(PREFS_PREFIX + "columnSpacing", columnSpacing);
        unitSpacing = EditorPrefs.GetFloat(PREFS_PREFIX + "unitSpacing", unitSpacing);

        // Unit Sizes
        infantryPixelSize = EditorPrefs.GetInt(PREFS_PREFIX + "infantryPixelSize", infantryPixelSize);
        vehiclePixelSize = EditorPrefs.GetInt(PREFS_PREFIX + "vehiclePixelSize", vehiclePixelSize);
        airPixelSize = EditorPrefs.GetInt(PREFS_PREFIX + "airPixelSize", airPixelSize);

        // Unit Scales
        infantryScale = EditorPrefs.GetFloat(PREFS_PREFIX + "infantryScale", infantryScale);
        vehicleScale = EditorPrefs.GetFloat(PREFS_PREFIX + "vehicleScale", vehicleScale);
        rangedScale = EditorPrefs.GetFloat(PREFS_PREFIX + "rangedScale", rangedScale);

        // Column Positions
        col1X = EditorPrefs.GetFloat(PREFS_PREFIX + "col1X", col1X);
        col2X = EditorPrefs.GetFloat(PREFS_PREFIX + "col2X", col2X);
        col3X = EditorPrefs.GetFloat(PREFS_PREFIX + "col3X", col3X);
        gridCenterY = EditorPrefs.GetFloat(PREFS_PREFIX + "gridCenterY", gridCenterY);

        // Infantry Slot Offsets
        for (int i = 0; i < 5; i++)
        {
            infantrySlotOffsets[i] = EditorPrefs.GetFloat(PREFS_PREFIX + "infantrySlotOffsets_" + i, 0f);
            infantrySlotOffsetsY[i] = EditorPrefs.GetFloat(PREFS_PREFIX + "infantrySlotOffsetsY_" + i, 0f);
        }

        // Vehicle Slot Offsets
        for (int i = 0; i < 5; i++)
        {
            vehicleSlotOffsets[i] = EditorPrefs.GetFloat(PREFS_PREFIX + "vehicleSlotOffsets_" + i, 0f);
            vehicleSlotOffsetsY[i] = EditorPrefs.GetFloat(PREFS_PREFIX + "vehicleSlotOffsetsY_" + i, 0f);
        }

        // Ranged Slot Offsets
        for (int i = 0; i < 5; i++)
        {
            rangedSlotOffsets[i] = EditorPrefs.GetFloat(PREFS_PREFIX + "rangedSlotOffsets_" + i, 0f);
            rangedSlotOffsetsY[i] = EditorPrefs.GetFloat(PREFS_PREFIX + "rangedSlotOffsetsY_" + i, 0f);
        }

        // Flying Row Slot Offsets
        for (int i = 0; i < 7; i++)
        {
            flyingSlotOffsetsX[i] = EditorPrefs.GetFloat(PREFS_PREFIX + "flyingSlotOffsetsX_" + i, 0f);
            flyingSlotOffsetsY[i] = EditorPrefs.GetFloat(PREFS_PREFIX + "flyingSlotOffsetsY_" + i, 0f);
        }

        // Flying Row Settings
        enableFlyingRow = EditorPrefs.GetBool(PREFS_PREFIX + "enableFlyingRow", enableFlyingRow);
        flyingRowY = EditorPrefs.GetFloat(PREFS_PREFIX + "flyingRowY", flyingRowY);
        flyingRowUnits = EditorPrefs.GetInt(PREFS_PREFIX + "flyingRowUnits", flyingRowUnits);
        flyingUnitSpacing = EditorPrefs.GetFloat(PREFS_PREFIX + "flyingUnitSpacing", flyingUnitSpacing);
        flyingScale = EditorPrefs.GetFloat(PREFS_PREFIX + "flyingScale", flyingScale);

        // Preview Settings
        showPreviewGizmos = EditorPrefs.GetBool(PREFS_PREFIX + "showPreviewGizmos", showPreviewGizmos);
        showColumnLabels = EditorPrefs.GetBool(PREFS_PREFIX + "showColumnLabels", showColumnLabels);
        showUnitSizeBoxes = EditorPrefs.GetBool(PREFS_PREFIX + "showUnitSizeBoxes", showUnitSizeBoxes);
        autoRefresh = EditorPrefs.GetBool(PREFS_PREFIX + "autoRefresh", autoRefresh);
        showSlotOffsets = EditorPrefs.GetBool(PREFS_PREFIX + "showSlotOffsets", showSlotOffsets);

        // Colors
        if (EditorPrefs.HasKey(PREFS_PREFIX + "playerAColor"))
            playerAColor = StringToColor(EditorPrefs.GetString(PREFS_PREFIX + "playerAColor"), playerAColor);
        if (EditorPrefs.HasKey(PREFS_PREFIX + "playerBColor"))
            playerBColor = StringToColor(EditorPrefs.GetString(PREFS_PREFIX + "playerBColor"), playerBColor);
        if (EditorPrefs.HasKey(PREFS_PREFIX + "columnDividerColor"))
            columnDividerColor = StringToColor(EditorPrefs.GetString(PREFS_PREFIX + "columnDividerColor"), columnDividerColor);
        if (EditorPrefs.HasKey(PREFS_PREFIX + "flyingRowColor"))
            flyingRowColor = StringToColor(EditorPrefs.GetString(PREFS_PREFIX + "flyingRowColor"), flyingRowColor);
    }

    /// <summary>
    /// Converts a Color to a string format "r,g,b,a"
    /// </summary>
    private static string ColorToString(Color color)
    {
        return $"{color.r},{color.g},{color.b},{color.a}";
    }

    /// <summary>
    /// Converts a string "r,g,b,a" back to a Color
    /// </summary>
    private static Color StringToColor(string colorString, Color defaultColor)
    {
        try
        {
            string[] components = colorString.Split(',');
            if (components.Length == 4)
            {
                float r = float.Parse(components[0]);
                float g = float.Parse(components[1]);
                float b = float.Parse(components[2]);
                float a = float.Parse(components[3]);
                return new Color(r, g, b, a);
            }
        }
        catch
        {
            // If parsing fails, return default
        }
        return defaultColor;
    }
}
