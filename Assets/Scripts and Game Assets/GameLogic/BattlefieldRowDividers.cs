using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Renders visual COLUMN dividers on the battlefield to help players understand combat flow.
/// Shows clear separation between Infantry, Vehicle, and Air COLUMNS.
///
/// LAYOUT: Combat flows HORIZONTALLY toward the center:
/// [AIR] → [VEHICLE] → [INFANTRY] ←→ [INFANTRY] ← [VEHICLE] ← [AIR]
///   ^                      ^           ^                       ^
/// Player A (left)      Center Line              Player B (right)
///
/// Setup:
/// 1. Attach this to a GameObject in your scene
/// 2. Configure column X positions (distance from center)
/// 3. Configure the visual style
/// </summary>
public class BattlefieldRowDividers : MonoBehaviour
{
    // ========================================
    // CONFIGURATION
    // ========================================

    [Header("Column Positions (X offset from center)")]
    [Tooltip("X offset for Infantry column (closest to center)")]
    public float infantryColumnX = 2f;

    [Tooltip("X offset for Vehicle column (middle)")]
    public float vehicleColumnX = 5f;

    [Tooltip("X offset for Air column (furthest from center)")]
    public float airColumnX = 8f;

    [Header("Divider Appearance")]
    [Tooltip("Height of the battlefield (auto-calculated from camera if 0)")]
    public float battlefieldHeight = 0f;

    [Tooltip("Width of each divider line")]
    public float dividerWidth = 0.1f;

    [Tooltip("Color of the divider lines")]
    public Color dividerColor = new Color(1f, 1f, 1f, 0.15f);

    [Tooltip("Use gradient fade at edges")]
    public bool useGradientFade = true;

    [Tooltip("Gradient fade height at edges")]
    public float fadeHeight = 2f;

    [Header("Column Labels")]
    [Tooltip("Show column type labels")]
    public bool showColumnLabels = true;

    [Tooltip("Label offset from top")]
    public float labelOffsetY = 0.5f;

    [Header("Combat Flow Arrows")]
    [Tooltip("Show arrows indicating damage flow direction (horizontal toward center)")]
    public bool showCombatFlowArrows = true;

    [Tooltip("Color of combat flow arrows")]
    public Color arrowColor = new Color(1f, 0.8f, 0.2f, 0.4f);

    [Header("Column Highlight (during combat)")]
    [Tooltip("Highlight color when a column is active in combat")]
    public Color activeColumnHighlight = new Color(1f, 1f, 0f, 0.1f);

    [Header("Rendering")]
    [Tooltip("Sorting layer for dividers")]
    public string sortingLayer = "Default";

    [Tooltip("Sorting order for dividers (should be behind units)")]
    public int sortingOrder = -10;

    [Header("References")]
    [Tooltip("Optional: Auto-detect column positions from FieldSpawner")]
    public FieldSpawner fieldSpawner;

    // Internal rendering
    private List<GameObject> dividerObjects = new List<GameObject>();
    private List<GameObject> labelObjects = new List<GameObject>();
    private List<GameObject> arrowObjects = new List<GameObject>();
    private Material dividerMaterial;
    private bool initialized = false;

    // ========================================
    // LIFECYCLE
    // ========================================

    private void Start()
    {
        Initialize();
    }

    private void OnDestroy()
    {
        CleanupDividers();
    }

    // ========================================
    // INITIALIZATION
    // ========================================

    public void Initialize()
    {
        if (initialized)
        {
            CleanupDividers();
        }

        // Auto-calculate battlefield height from camera
        if (battlefieldHeight <= 0f)
        {
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic)
            {
                battlefieldHeight = cam.orthographicSize * 2f;
            }
            else
            {
                battlefieldHeight = 12f; // Default fallback
            }
        }

        // Create divider material
        dividerMaterial = new Material(Shader.Find("Sprites/Default"));
        dividerMaterial.color = dividerColor;

        // Create center battle line (main divider between players)
        CreateVerticalDividerLine(0f, "CenterLine", Color.white * 0.5f);

        // Create dividers between columns for Player A (left side, negative X)
        float dividerInfVehA = -(infantryColumnX + vehicleColumnX) / 2f;
        float dividerVehAirA = -(vehicleColumnX + airColumnX) / 2f;
        CreateVerticalDividerLine(dividerInfVehA, "Divider_A_Inf_Veh");
        CreateVerticalDividerLine(dividerVehAirA, "Divider_A_Veh_Air");

        // Create dividers between columns for Player B (right side, positive X)
        float dividerInfVehB = (infantryColumnX + vehicleColumnX) / 2f;
        float dividerVehAirB = (vehicleColumnX + airColumnX) / 2f;
        CreateVerticalDividerLine(dividerInfVehB, "Divider_B_Inf_Veh");
        CreateVerticalDividerLine(dividerVehAirB, "Divider_B_Veh_Air");

        // Create column labels if enabled
        if (showColumnLabels)
        {
            float labelY = battlefieldHeight / 2f - labelOffsetY;

            // Player A labels (left side)
            CreateColumnLabel("INFANTRY", -infantryColumnX, labelY, "Front line");
            CreateColumnLabel("VEHICLE", -vehicleColumnX, labelY, "Support");
            CreateColumnLabel("AIR", -airColumnX, labelY, "Back line");

            // Player B labels (right side)
            CreateColumnLabel("INFANTRY", infantryColumnX, labelY, "Front line");
            CreateColumnLabel("VEHICLE", vehicleColumnX, labelY, "Support");
            CreateColumnLabel("AIR", airColumnX, labelY, "Back line");
        }

        // Create horizontal combat flow arrows if enabled
        if (showCombatFlowArrows)
        {
            // Player A: arrows point RIGHT toward center
            CreateHorizontalFlowArrow(-airColumnX, -vehicleColumnX, 0f, "Arrow_A_Air_to_Veh");
            CreateHorizontalFlowArrow(-vehicleColumnX, -infantryColumnX, 0f, "Arrow_A_Veh_to_Inf");
            CreateHorizontalFlowArrow(-infantryColumnX, -0.5f, 0f, "Arrow_A_Inf_to_Center");

            // Player B: arrows point LEFT toward center
            CreateHorizontalFlowArrow(airColumnX, vehicleColumnX, 0f, "Arrow_B_Air_to_Veh");
            CreateHorizontalFlowArrow(vehicleColumnX, infantryColumnX, 0f, "Arrow_B_Veh_to_Inf");
            CreateHorizontalFlowArrow(infantryColumnX, 0.5f, 0f, "Arrow_B_Inf_to_Center");
        }

        initialized = true;
    }

    private float GetDividerX(float col1X, float col2X)
    {
        return (col1X + col2X) / 2f;
    }

    // ========================================
    // DIVIDER CREATION
    // ========================================

    private void CreateVerticalDividerLine(float xPosition, string name, Color? customColor = null)
    {
        GameObject divider = new GameObject(name);
        divider.transform.SetParent(transform);
        divider.transform.position = new Vector3(xPosition, 0, 0);

        // Create a sprite renderer for the line
        SpriteRenderer sr = divider.AddComponent<SpriteRenderer>();
        sr.sprite = CreateLineSprite();
        sr.color = customColor ?? dividerColor;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;

        // Scale to battlefield height (vertical line)
        divider.transform.localScale = new Vector3(dividerWidth, battlefieldHeight, 1f);

        dividerObjects.Add(divider);
    }

    private Sprite CreateLineSprite()
    {
        // Create a simple 1x1 white texture for the line
        Texture2D tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1);
    }

    private void CreateColumnLabel(string text, float columnX, float yPosition, string tooltip)
    {
        // Create a TextMesh for the column label
        GameObject label = new GameObject($"Label_{text}_{(columnX < 0 ? "A" : "B")}");
        label.transform.SetParent(transform);
        label.transform.position = new Vector3(columnX, yPosition, 0);

        TextMesh tm = label.AddComponent<TextMesh>();
        tm.text = text;
        tm.fontSize = 24;
        tm.characterSize = 0.12f;
        tm.anchor = TextAnchor.UpperCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(1f, 1f, 1f, 0.5f);

        // Set font to something readable
        tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        MeshRenderer mr = label.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sortingLayerName = sortingLayer;
            mr.sortingOrder = sortingOrder + 1;
        }

        labelObjects.Add(label);
    }

    private void CreateHorizontalFlowArrow(float fromX, float toX, float yPosition, string name)
    {
        // Create horizontal arrow indicating damage flow direction toward center
        GameObject arrow = new GameObject(name);
        arrow.transform.SetParent(transform);
        arrow.transform.position = new Vector3((fromX + toX) / 2f, yPosition, 0);

        float arrowLength = Mathf.Abs(toX - fromX) * 0.6f;
        float direction = Mathf.Sign(toX - fromX);

        // Create arrow using line renderer
        LineRenderer lr = arrow.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(-arrowLength / 2f * direction, 0, 0));
        lr.SetPosition(1, new Vector3(arrowLength / 2f * direction, 0, 0));

        lr.startWidth = 0.05f;
        lr.endWidth = 0.15f; // Arrow head effect (wider at end = arrow pointing in direction of movement)
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = arrowColor;
        lr.endColor = arrowColor;
        lr.useWorldSpace = false;
        lr.sortingLayerName = sortingLayer;
        lr.sortingOrder = sortingOrder;

        arrowObjects.Add(arrow);
    }

    // ========================================
    // RUNTIME UPDATES
    // ========================================

    /// <summary>
    /// Highlights a specific column during combat.
    /// Call this when combat is active in a column.
    /// </summary>
    /// <param name="unitType">The unit type (Infantry, Vehicle, Air) to highlight</param>
    /// <param name="side">Which player's column to highlight</param>
    /// <param name="highlight">True to show highlight, false to hide</param>
    public void HighlightColumn(UnitType unitType, PlayerSide side, bool highlight)
    {
        float columnX = unitType switch
        {
            UnitType.Infantry => infantryColumnX,
            UnitType.Vehicle => vehicleColumnX,
            UnitType.Ranged => vehicleColumnX,
            UnitType.Air => airColumnX,
            _ => infantryColumnX
        };

        // Flip X for Player A (left side)
        if (side == PlayerSide.A)
            columnX = -columnX;

        // Find or create highlight object for this column
        string highlightName = $"ColumnHighlight_{unitType}_{side}";
        Transform existing = transform.Find(highlightName);

        if (highlight)
        {
            GameObject highlightObj;
            if (existing != null)
            {
                highlightObj = existing.gameObject;
            }
            else
            {
                highlightObj = new GameObject(highlightName);
                highlightObj.transform.SetParent(transform);

                SpriteRenderer sr = highlightObj.AddComponent<SpriteRenderer>();
                sr.sprite = CreateLineSprite();
                sr.color = activeColumnHighlight;
                sr.sortingLayerName = sortingLayer;
                sr.sortingOrder = sortingOrder - 1;
            }

            // Position and size the highlight (vertical column)
            float columnWidth = Mathf.Abs(vehicleColumnX - infantryColumnX) * 0.8f;
            highlightObj.transform.position = new Vector3(columnX, 0, 0);
            highlightObj.transform.localScale = new Vector3(columnWidth, battlefieldHeight, 1f);
            highlightObj.SetActive(true);

            // Optional: Animate the highlight
            StartCoroutine(PulseHighlight(highlightObj));
        }
        else if (existing != null)
        {
            existing.gameObject.SetActive(false);
            StopAllCoroutines();
        }
    }

    /// <summary>
    /// Legacy method for row-based highlight (maps to column highlight).
    /// </summary>
    public void HighlightRow(RowType row, bool highlight)
    {
        UnitType unitType = row switch
        {
            RowType.Front => UnitType.Infantry,
            RowType.Middle => UnitType.Vehicle,
            RowType.Back => UnitType.Air,
            _ => UnitType.Infantry
        };

        // Highlight both players' columns of this type
        HighlightColumn(unitType, PlayerSide.A, highlight);
        HighlightColumn(unitType, PlayerSide.B, highlight);
    }

    private System.Collections.IEnumerator PulseHighlight(GameObject highlight)
    {
        SpriteRenderer sr = highlight.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float time = 0f;
        Color baseColor = activeColumnHighlight;

        while (highlight.activeSelf)
        {
            time += Time.deltaTime * 2f;
            float alpha = Mathf.Lerp(0.05f, 0.2f, (Mathf.Sin(time) + 1f) / 2f);
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            yield return null;
        }
    }

    /// <summary>
    /// Updates divider positions (call if column positions change at runtime).
    /// </summary>
    public void RefreshDividers()
    {
        CleanupDividers();
        Initialize();
    }

    /// <summary>
    /// Sets column positions from FieldSpawner spawn point transforms.
    /// </summary>
    public void AutoDetectColumnPositions()
    {
        if (fieldSpawner == null)
        {
            fieldSpawner = FindAnyObjectByType<FieldSpawner>();
        }

        if (fieldSpawner == null)
        {
            Debug.LogWarning("[BattlefieldRowDividers] No FieldSpawner found for auto-detection");
            return;
        }

        // This would need access to FieldSpawner's private arrays
        // For now, use the configured values
        Debug.Log("[BattlefieldRowDividers] Using configured column positions");
    }

    // ========================================
    // CLEANUP
    // ========================================

    private void CleanupDividers()
    {
        foreach (var obj in dividerObjects)
        {
            if (obj != null)
            {
                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
        }
        dividerObjects.Clear();

        foreach (var obj in labelObjects)
        {
            if (obj != null)
            {
                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
        }
        labelObjects.Clear();

        foreach (var obj in arrowObjects)
        {
            if (obj != null)
            {
                if (Application.isPlaying)
                    Destroy(obj);
                else
                    DestroyImmediate(obj);
            }
        }
        arrowObjects.Clear();

        if (dividerMaterial != null)
        {
            if (Application.isPlaying)
                Destroy(dividerMaterial);
            else
                DestroyImmediate(dividerMaterial);
        }

        initialized = false;
    }

    // ========================================
    // EDITOR PREVIEW
    // ========================================

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            // Preview column dividers in editor
            float height = battlefieldHeight > 0 ? battlefieldHeight : 12f;

            // Center line (battle line between players)
            Gizmos.color = Color.white * 0.5f;
            Gizmos.DrawLine(new Vector3(0, -height / 2f, 0), new Vector3(0, height / 2f, 0));

            Gizmos.color = dividerColor;

            // Player A dividers (left side, negative X)
            float dividerA_InfVeh = -(infantryColumnX + vehicleColumnX) / 2f;
            float dividerA_VehAir = -(vehicleColumnX + airColumnX) / 2f;
            Gizmos.DrawLine(new Vector3(dividerA_InfVeh, -height / 2f, 0), new Vector3(dividerA_InfVeh, height / 2f, 0));
            Gizmos.DrawLine(new Vector3(dividerA_VehAir, -height / 2f, 0), new Vector3(dividerA_VehAir, height / 2f, 0));

            // Player B dividers (right side, positive X)
            float dividerB_InfVeh = (infantryColumnX + vehicleColumnX) / 2f;
            float dividerB_VehAir = (vehicleColumnX + airColumnX) / 2f;
            Gizmos.DrawLine(new Vector3(dividerB_InfVeh, -height / 2f, 0), new Vector3(dividerB_InfVeh, height / 2f, 0));
            Gizmos.DrawLine(new Vector3(dividerB_VehAir, -height / 2f, 0), new Vector3(dividerB_VehAir, height / 2f, 0));

            // Column labels
            if (showColumnLabels)
            {
                float labelY = height / 2f - 0.3f;

                // Player A labels
                UnityEditor.Handles.Label(new Vector3(-infantryColumnX, labelY, 0), "INF");
                UnityEditor.Handles.Label(new Vector3(-vehicleColumnX, labelY, 0), "VEH");
                UnityEditor.Handles.Label(new Vector3(-airColumnX, labelY, 0), "AIR");

                // Player B labels
                UnityEditor.Handles.Label(new Vector3(infantryColumnX, labelY, 0), "INF");
                UnityEditor.Handles.Label(new Vector3(vehicleColumnX, labelY, 0), "VEH");
                UnityEditor.Handles.Label(new Vector3(airColumnX, labelY, 0), "AIR");
            }

            // Horizontal combat flow arrows (toward center)
            if (showCombatFlowArrows)
            {
                Gizmos.color = arrowColor;

                // Player A: arrows point RIGHT toward center
                DrawGizmoArrow(new Vector3(-airColumnX, 0, 0), new Vector3(-vehicleColumnX, 0, 0));
                DrawGizmoArrow(new Vector3(-vehicleColumnX, 0, 0), new Vector3(-infantryColumnX, 0, 0));
                DrawGizmoArrow(new Vector3(-infantryColumnX, 0, 0), new Vector3(-0.5f, 0, 0));

                // Player B: arrows point LEFT toward center
                DrawGizmoArrow(new Vector3(airColumnX, 0, 0), new Vector3(vehicleColumnX, 0, 0));
                DrawGizmoArrow(new Vector3(vehicleColumnX, 0, 0), new Vector3(infantryColumnX, 0, 0));
                DrawGizmoArrow(new Vector3(infantryColumnX, 0, 0), new Vector3(0.5f, 0, 0));
            }
        }
    }

    private void DrawGizmoArrow(Vector3 from, Vector3 to)
    {
        Gizmos.DrawLine(from, to);

        Vector3 direction = (to - from).normalized;
        float arrowSize = 0.3f;

        Vector3 right = Vector3.Cross(direction, Vector3.forward).normalized;
        Gizmos.DrawLine(to, to - direction * arrowSize + right * arrowSize * 0.5f);
        Gizmos.DrawLine(to, to - direction * arrowSize - right * arrowSize * 0.5f);
    }
#endif
}
