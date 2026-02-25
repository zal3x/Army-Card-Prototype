#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BulkRemoveAtkOverlayAnchors
{
    private static readonly string[] SearchFolders = new[]
    {
        "Assets"
    };

    private const string OverlayAnchorName = "OverlayAnchor";

    [MenuItem("Tools/Battle Tactics/REMOVE ATK Overlay Anchors From Prefabs")]
    public static void Run()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", SearchFolders);

        int removedCount = 0;

        AssetDatabase.StartAssetEditing();

        try
        {
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                if (root == null) continue;

                Transform anchor = root.transform.Find(OverlayAnchorName);
                if (anchor != null)
                {
                    Object.DestroyImmediate(anchor.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    removedCount++;
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

        Debug.Log($"Removed OverlayAnchor from {removedCount} prefabs.");
    }
}
#endif