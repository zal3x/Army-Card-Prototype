using UnityEngine;
using UnityEditor;

/// <summary>
/// Editor tool for diagnosing and fixing mismatched Commander Display panel sizes.
///
/// Open via: Tools > Commander Panel Fixer
///
/// Shows both panels' RectTransform values side-by-side and lets you
/// copy layout settings from one to the other.
/// </summary>
public class CommanderPanelFixer : EditorWindow
{
    private CommanderDisplay panelA;
    private CommanderDisplay panelB;
    private Vector2 scroll;

    [MenuItem("Tools/Commander Panel Fixer")]
    public static void OpenWindow()
    {
        var win = GetWindow<CommanderPanelFixer>("Commander Panel Fixer");
        win.minSize = new Vector2(420, 520);
        win.AutoFind();
    }

    // ---------------------------------------------------------------

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawHeader();
        EditorGUILayout.Space(6);

        DrawPanelPickers();
        EditorGUILayout.Space(8);

        if (panelA != null && panelB != null)
        {
            DrawComparison();
            EditorGUILayout.Space(8);
            DrawActions();
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Assign both panels above, or click Auto-Find to search the scene.",
                MessageType.Info);
        }

        EditorGUILayout.Space(8);
        DrawCanvasScalerInfo();

        EditorGUILayout.EndScrollView();
    }

    // ---------------------------------------------------------------
    // HEADER
    // ---------------------------------------------------------------

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("Commander Panel Fixer", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "Diagnose and equalize Commander Display panel sizes.",
            EditorStyles.wordWrappedMiniLabel);
    }

    // ---------------------------------------------------------------
    // PANEL PICKERS
    // ---------------------------------------------------------------

    private void DrawPanelPickers()
    {
        EditorGUILayout.LabelField("Panels", EditorStyles.boldLabel);

        EditorGUI.BeginChangeCheck();
        panelA = (CommanderDisplay)EditorGUILayout.ObjectField(
            "Player A Panel", panelA, typeof(CommanderDisplay), allowSceneObjects: true);
        panelB = (CommanderDisplay)EditorGUILayout.ObjectField(
            "Player B Panel", panelB, typeof(CommanderDisplay), allowSceneObjects: true);
        EditorGUI.EndChangeCheck();

        if (GUILayout.Button("Auto-Find in Scene"))
            AutoFind();
    }

    private void AutoFind()
    {
        var all = FindObjectsOfType<CommanderDisplay>();
        if (all.Length >= 2)
        {
            panelA = all[0];
            panelB = all[1];
            Debug.Log($"[CommanderPanelFixer] Found: {panelA.name}, {panelB.name}");
        }
        else if (all.Length == 1)
        {
            panelA = all[0];
            Debug.LogWarning("[CommanderPanelFixer] Only one CommanderDisplay found in scene.");
        }
        else
        {
            Debug.LogWarning("[CommanderPanelFixer] No CommanderDisplay found in scene. Is the battle scene loaded?");
        }
    }

    // ---------------------------------------------------------------
    // SIDE-BY-SIDE COMPARISON
    // ---------------------------------------------------------------

    private void DrawComparison()
    {
        RectTransform rtA = panelA.GetComponent<RectTransform>();
        RectTransform rtB = panelB.GetComponent<RectTransform>();

        if (rtA == null || rtB == null)
        {
            EditorGUILayout.HelpBox("One or both panels are missing a RectTransform.", MessageType.Error);
            return;
        }

        EditorGUILayout.LabelField("RectTransform Comparison", EditorStyles.boldLabel);

        // Column headers
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Property", EditorStyles.miniLabel, GUILayout.Width(130));
        EditorGUILayout.LabelField(panelA.name, EditorStyles.miniLabel, GUILayout.Width(130));
        EditorGUILayout.LabelField(panelB.name, EditorStyles.miniLabel, GUILayout.Width(130));
        EditorGUILayout.LabelField("Match?", EditorStyles.miniLabel, GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        DrawSeparator();

        DrawRow("Width",          rtA.rect.width,         rtB.rect.width,         tolerance: 0.5f);
        DrawRow("Height",         rtA.rect.height,        rtB.rect.height,        tolerance: 0.5f);
        DrawRow2("AnchorMin",     rtA.anchorMin,          rtB.anchorMin,          tolerance: 0.001f);
        DrawRow2("AnchorMax",     rtA.anchorMax,          rtB.anchorMax,          tolerance: 0.001f);
        DrawRow2("Pivot",         rtA.pivot,              rtB.pivot,              tolerance: 0.001f);
        DrawRow2("AnchoredPos",   rtA.anchoredPosition,   rtB.anchoredPosition,   tolerance: 1f);
        DrawRow2("SizeDelta",     rtA.sizeDelta,          rtB.sizeDelta,          tolerance: 0.5f);
        DrawRow("Scale X",        rtA.localScale.x,       rtB.localScale.x,       tolerance: 0.001f);
        DrawRow("Scale Y",        rtA.localScale.y,       rtB.localScale.y,       tolerance: 0.001f);

        DrawSeparator();

        // Parent info
        string parentA = rtA.parent != null ? rtA.parent.name : "(none)";
        string parentB = rtB.parent != null ? rtB.parent.name : "(none)";
        bool parentsMatch = parentA == parentB;
        DrawRowString("Parent", parentA, parentB, parentsMatch);
    }

    private void DrawRow(string label, float a, float b, float tolerance)
    {
        bool match = Mathf.Abs(a - b) <= tolerance;
        Color prev = GUI.color;
        if (!match) GUI.color = new Color(1f, 0.6f, 0.6f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(130));
        EditorGUILayout.LabelField(a.ToString("F2"), GUILayout.Width(130));
        EditorGUILayout.LabelField(b.ToString("F2"), GUILayout.Width(130));
        EditorGUILayout.LabelField(match ? "✓" : "✗", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        GUI.color = prev;
    }

    private void DrawRow2(string label, Vector2 a, Vector2 b, float tolerance)
    {
        bool match = (a - b).magnitude <= tolerance;
        Color prev = GUI.color;
        if (!match) GUI.color = new Color(1f, 0.6f, 0.6f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(130));
        EditorGUILayout.LabelField($"({a.x:F2}, {a.y:F2})", GUILayout.Width(130));
        EditorGUILayout.LabelField($"({b.x:F2}, {b.y:F2})", GUILayout.Width(130));
        EditorGUILayout.LabelField(match ? "✓" : "✗", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        GUI.color = prev;
    }

    private void DrawRowString(string label, string a, string b, bool match)
    {
        Color prev = GUI.color;
        if (!match) GUI.color = new Color(1f, 0.6f, 0.6f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(130));
        EditorGUILayout.LabelField(a, GUILayout.Width(130));
        EditorGUILayout.LabelField(b, GUILayout.Width(130));
        EditorGUILayout.LabelField(match ? "✓" : "✗", GUILayout.Width(50));
        EditorGUILayout.EndHorizontal();

        GUI.color = prev;
    }

    private static void DrawSeparator()
    {
        var rect = EditorGUILayout.GetControlRect(false, 1);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
    }

    // ---------------------------------------------------------------
    // ACTIONS
    // ---------------------------------------------------------------

    private void DrawActions()
    {
        RectTransform rtA = panelA.GetComponent<RectTransform>();
        RectTransform rtB = panelB.GetComponent<RectTransform>();
        if (rtA == null || rtB == null) return;

        EditorGUILayout.LabelField("Fix Actions", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button($"Copy A → B\n({panelA.name})"))
            CopyLayout(rtA, rtB, $"{panelB.name}");

        if (GUILayout.Button($"Copy B → A\n({panelB.name})"))
            CopyLayout(rtB, rtA, $"{panelA.name}");

        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Average sizes (keep each panel's position)"))
            AverageSizes(rtA, rtB);

        EditorGUILayout.HelpBox(
            "Copy transfers: SizeDelta (W/H), AnchorMin, AnchorMax, and Pivot.\n" +
            "AnchoredPosition is NOT copied so each panel stays in place.",
            MessageType.None);
    }

    private void CopyLayout(RectTransform src, RectTransform dst, string dstName)
    {
        Undo.RecordObject(dst, $"Commander Panel Fixer: Copy to {dstName}");
        dst.sizeDelta    = src.sizeDelta;
        dst.anchorMin    = src.anchorMin;
        dst.anchorMax    = src.anchorMax;
        dst.pivot        = src.pivot;
        dst.localScale   = src.localScale;
        EditorUtility.SetDirty(dst);
        Debug.Log($"[CommanderPanelFixer] Copied layout from {src.name} to {dst.name}. SizeDelta={src.sizeDelta}");
    }

    private void AverageSizes(RectTransform rtA, RectTransform rtB)
    {
        Undo.RecordObjects(new Object[] { rtA, rtB }, "Commander Panel Fixer: Average Sizes");
        Vector2 avg = (rtA.sizeDelta + rtB.sizeDelta) * 0.5f;
        rtA.sizeDelta = avg;
        rtB.sizeDelta = avg;
        EditorUtility.SetDirty(rtA);
        EditorUtility.SetDirty(rtB);
        Debug.Log($"[CommanderPanelFixer] Averaged sizes to {avg}");
    }

    // ---------------------------------------------------------------
    // CANVAS SCALER INFO
    // ---------------------------------------------------------------

    private void DrawCanvasScalerInfo()
    {
        EditorGUILayout.LabelField("Canvas Scaler Info", EditorStyles.boldLabel);

        Canvas canvas = null;
        if (panelA != null)
            canvas = panelA.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            EditorGUILayout.LabelField("No canvas found (assign a panel above).",
                EditorStyles.wordWrappedMiniLabel);
            return;
        }

        var scaler = canvas.GetComponent<UnityEngine.UI.CanvasScaler>();
        if (scaler == null)
        {
            EditorGUILayout.LabelField($"Canvas '{canvas.name}': No CanvasScaler attached.",
                EditorStyles.wordWrappedMiniLabel);
            return;
        }

        EditorGUILayout.LabelField($"Canvas: {canvas.name}  |  Render Mode: {canvas.renderMode}");
        EditorGUILayout.LabelField($"Scale Mode: {scaler.uiScaleMode}");

        if (scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize)
        {
            EditorGUILayout.LabelField($"Reference Resolution: {scaler.referenceResolution}");
            EditorGUILayout.LabelField($"Screen Match Mode: {scaler.screenMatchMode}");
            EditorGUILayout.LabelField($"Match (0=W, 1=H): {scaler.matchWidthOrHeight}");
            EditorGUILayout.LabelField($"Current Scale Factor: {canvas.scaleFactor:F3}");

            EditorGUILayout.HelpBox(
                "Scale With Screen Size scales ALL children uniformly — it won't cause two sibling panels " +
                "to be different sizes. The mismatch is in the RectTransform values above (red rows).",
                MessageType.Info);
        }
        else if (scaler.uiScaleMode == UnityEngine.UI.CanvasScaler.ScaleMode.ConstantPixelSize)
        {
            EditorGUILayout.LabelField($"Scale Factor: {scaler.scaleFactor:F3}");
        }
    }
}
