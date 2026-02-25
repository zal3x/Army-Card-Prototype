#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Bulk-adds an OverlayAnchor -> ATKRoot -> (Icon SpriteRenderer + ATKText TextMeshPro) hierarchy
/// to ONLY prefabs inside the specified folder(s).
///
/// Safety features:
/// - Only scans SearchFolders.
/// - Only modifies prefabs that contain a component named "Unit" somewhere in the prefab hierarchy
///   (no direct reference to the Unit type, avoiding assembly/namespace collisions).
/// </summary>
public static class BulkAddAtkOverlayAnchors
{
    // ====== CONFIG ======
    private static readonly string[] SearchFolders = new[]
    {
        "Assets/Scripts and Game Assets/Cards/UnitSprites/Faction 1 Prefabs"
    };

    private const string OverlayAnchorName = "OverlayAnchor";
    private const string AtkRootName = "ATKRoot";
    private const string IconName = "Icon";
    private const string AtkTextName = "ATKText";
// Prefab must contain at least ONE of these component type names to be considered a unit visual prefab.
private static readonly string[] UnitMarkerComponentNames = new[]
{
    "UnitAnimator",
    "UnitVisualEffects",
    "UnitAttackEffects",
    "UnitCardDefinition"
};
// Path to your BoldPixels SDF TMP Font Asset
// Right-click the font in Project window → Copy Path → paste here
private const string BoldPixelsFontPath = 
    "Assets/BoldPixels/Assets/font/BoldPixels SDF.asset";
private static bool PrefabContainsAnyComponentNamed(GameObject root, string[] typeNames)
{
    var comps = root.GetComponentsInChildren<Component>(true);
    for (int i = 0; i < comps.Length; i++)
    {
        var c = comps[i];
        if (c == null) continue;

        string n = c.GetType().Name;
        for (int t = 0; t < typeNames.Length; t++)
        {
            if (n == typeNames[t])
                return true;
        }
    }
    return false;
}
    private static readonly Vector3 DefaultAnchorLocalPos = new Vector3(0f, 1.8f, 0f);
    private static readonly Vector3 DefaultRootLocalPos = Vector3.zero;

    private static readonly Vector3 DefaultIconLocalPos = new Vector3(-0.25f, 0f, 0f);
    private static readonly Vector3 DefaultTextLocalPos = new Vector3(0.15f, 0f, 0f);

    private const string DefaultSortingLayer = "UI"; // change if you have a dedicated overlay sorting layer
    private const int IconOrderInLayer = 50;
    private const int TextOrderInLayer = 51;

    private const float DefaultFontSize = 4.0f;
    private const TextAlignmentOptions DefaultAlignment = TextAlignmentOptions.Center;

    [MenuItem("Tools/Battle Tactics/Bulk Add ATK Overlay Anchors (Faction 1 Prefabs Only)")]
  public static void Run()
{
    TMP_FontAsset boldPixelsFont =
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldPixelsFontPath);

    if (boldPixelsFont == null)
    {
        Debug.LogError("[BulkAddAtkOverlayAnchors] Could not load BoldPixels SDF font at path: " + BoldPixelsFontPath);
        return;
    }
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);
        if (prefabGuids == null || prefabGuids.Length == 0)
        {
            Debug.LogWarning("[BulkAddAtkOverlayAnchors] No prefabs found in SearchFolders.");
            return;
        }

        int scanned = 0;
        int skippedNotUnit = 0;
        int changedCount = 0;

        try
        {
            AssetDatabase.StartAssetEditing();

            for (int i = 0; i < prefabGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                if (string.IsNullOrEmpty(path)) continue;

                scanned++;

                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                // SAFETY: only touch prefabs that have a component named "Unit" somewhere.
              if (!PrefabContainsAnyComponentNamed(root, UnitMarkerComponentNames))
{
    skippedNotUnit++;
    PrefabUtility.UnloadPrefabContents(root);
    continue;
}

bool changed = EnsureHierarchy(root, boldPixelsFont);
                if (changed)
                {
                    changedCount++;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }

                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[BulkAddAtkOverlayAnchors] Done. Scanned={scanned}, Updated={changedCount}, Skipped(No 'Unit' component)={skippedNotUnit}");
    }

    private static bool PrefabContainsComponentNamed(GameObject root, string typeName)
    {
        // Pull all Components and compare actual runtime type names (no hard reference needed).
        var comps = root.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < comps.Length; i++)
        {
            var c = comps[i];
            if (c == null) continue;

            if (c.GetType().Name == typeName)
                return true;
        }
        return false;
    }

private static bool EnsureHierarchy(GameObject prefabRoot, TMP_FontAsset boldPixelsFont)    {
        bool changed = false;

        // 1) OverlayAnchor
        Transform anchor = prefabRoot.transform.Find(OverlayAnchorName);
        if (anchor == null)
        {
            GameObject go = new GameObject(OverlayAnchorName);
            go.transform.SetParent(prefabRoot.transform, false);
            go.transform.localPosition = DefaultAnchorLocalPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            anchor = go.transform;
            changed = true;
        }

        // 2) ATKRoot under OverlayAnchor
        Transform atkRoot = anchor.Find(AtkRootName);
        if (atkRoot == null)
        {
            GameObject go = new GameObject(AtkRootName);
            go.transform.SetParent(anchor, false);
            go.transform.localPosition = DefaultRootLocalPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            atkRoot = go.transform;
            changed = true;
        }

        // 3) Icon under ATKRoot
        Transform icon = atkRoot.Find(IconName);
        if (icon == null)
        {
            GameObject go = new GameObject(IconName);
            go.transform.SetParent(atkRoot, false);
            go.transform.localPosition = DefaultIconLocalPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = DefaultSortingLayer;
            sr.sortingOrder = IconOrderInLayer;

            changed = true;
        }
        else
        {
            var sr = icon.GetComponent<SpriteRenderer>();
            if (sr == null)
            {
                sr = icon.gameObject.AddComponent<SpriteRenderer>();
                changed = true;
            }
            sr.sortingLayerName = DefaultSortingLayer;
            sr.sortingOrder = IconOrderInLayer;
        }

        // 4) ATKText under ATKRoot (TextMeshPro 3D)
        Transform atkText = atkRoot.Find(AtkTextName);
        if (atkText == null)
        {
            GameObject go = new GameObject(AtkTextName);
            go.transform.SetParent(atkRoot, false);
            go.transform.localPosition = DefaultTextLocalPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

          var tmp = go.AddComponent<TextMeshPro>();

tmp.font = boldPixelsFont;     // <-- FORCE BoldPixels SDF
tmp.text = "0";
tmp.fontSize = 8f;             // <-- EXACT SIZE 8
tmp.alignment = TextAlignmentOptions.Center;
tmp.enableAutoSizing = false;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = DefaultSortingLayer;
                mr.sortingOrder = TextOrderInLayer;
            }

            changed = true;
        }
        else
        {
           var tmp = atkText.GetComponent<TextMeshPro>();
if (tmp == null)
{
    tmp = atkText.gameObject.AddComponent<TextMeshPro>();
    changed = true;
}

tmp.font = boldPixelsFont;   // Force BoldPixels SDF
tmp.fontSize = 8f;
tmp.alignment = TextAlignmentOptions.Center;
tmp.enableAutoSizing = false;

            var mr = atkText.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingLayerName = DefaultSortingLayer;
                mr.sortingOrder = TextOrderInLayer;
            }
        }

        return changed;
    }
}
#endif