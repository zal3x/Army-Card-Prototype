using UnityEngine;
using UnityEditor;
using System.Linq;

/// <summary>
/// Editor utility to batch-update WorldHealthBar tween settings on all unit prefabs.
/// Menu: Tools > Update All Health Bar Tween Speed
/// </summary>
public class BatchUpdateHealthBars : EditorWindow
{
    private float newTweenSeconds = 0.15f;

    [MenuItem("Tools/Update All Health Bar Tween Speed")]
    static void ShowWindow()
    {
        GetWindow<BatchUpdateHealthBars>("Health Bar Batch Update");
    }

    void OnGUI()
    {
        GUILayout.Label("Batch Update WorldHealthBar Tween Speed", EditorStyles.boldLabel);
        GUILayout.Space(10);

        newTweenSeconds = EditorGUILayout.FloatField("New Tween Seconds:", newTweenSeconds);

        GUILayout.Space(10);
        GUILayout.Label("This will update ALL prefabs that have WorldHealthBar components.", EditorStyles.helpBox);

        GUILayout.Space(10);
        if (GUILayout.Button("Update All Prefabs", GUILayout.Height(40)))
        {
            UpdateAllHealthBars();
        }
    }

    void UpdateAllHealthBars()
    {
        // Find all prefabs in the project
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int updateCount = 0;

        EditorUtility.DisplayProgressBar("Updating Health Bars", "Searching prefabs...", 0);

        try
        {
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                EditorUtility.DisplayProgressBar("Updating Health Bars", $"Processing {path}", (float)i / guids.Length);

                // Load the prefab
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;

                // Check if it has WorldHealthBar (including in children)
                WorldHealthBar[] healthBars = prefab.GetComponentsInChildren<WorldHealthBar>(true);

                if (healthBars.Length > 0)
                {
                    // Load prefab for editing
                    string prefabPath = AssetDatabase.GetAssetPath(prefab);
                    GameObject prefabInstance = PrefabUtility.LoadPrefabContents(prefabPath);

                    // Update all health bars in this prefab
                    WorldHealthBar[] bars = prefabInstance.GetComponentsInChildren<WorldHealthBar>(true);
                    foreach (var bar in bars)
                    {
                        SerializedObject so = new SerializedObject(bar);
                        SerializedProperty prop = so.FindProperty("tweenSeconds");
                        if (prop != null)
                        {
                            prop.floatValue = newTweenSeconds;
                            so.ApplyModifiedProperties();
                            updateCount++;
                        }
                    }

                    // Save the prefab
                    PrefabUtility.SaveAsPrefabAsset(prefabInstance, prefabPath);
                    PrefabUtility.UnloadPrefabContents(prefabInstance);
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        EditorUtility.DisplayDialog("Update Complete",
            $"Updated {updateCount} WorldHealthBar component(s) across {guids.Length} prefabs.\nNew tween seconds: {newTweenSeconds}",
            "OK");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
