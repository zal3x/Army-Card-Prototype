using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor tool for testing the Overworld systems without full UI.
/// Use: Window → Army Prototype → Overworld Test Tool
/// </summary>
public class OverworldTestTool : EditorWindow
{
    private PlayerProgressData playerProgress;
    private List<DoctrineDefinition> allDoctrines;
    private List<PerkDefinition> allPerks;
    private Vector2 scrollPos;

    [MenuItem("Window/Army Prototype/Overworld Test Tool")]
    public static void ShowWindow()
    {
        GetWindow<OverworldTestTool>("Overworld Test");
    }

    private void OnEnable()
    {
        RefreshAssets();
    }

    private void RefreshAssets()
    {
        // Find PlayerProgress asset
        string[] progressGuids = AssetDatabase.FindAssets("t:PlayerProgressData");
        if (progressGuids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(progressGuids[0]);
            playerProgress = AssetDatabase.LoadAssetAtPath<PlayerProgressData>(path);
        }

        // Find all doctrines
        allDoctrines = new List<DoctrineDefinition>();
        string[] doctrineGuids = AssetDatabase.FindAssets("t:DoctrineDefinition");
        foreach (var guid in doctrineGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var doctrine = AssetDatabase.LoadAssetAtPath<DoctrineDefinition>(path);
            if (doctrine != null) allDoctrines.Add(doctrine);
        }

        // Find all perks
        allPerks = new List<PerkDefinition>();
        string[] perkGuids = AssetDatabase.FindAssets("t:PerkDefinition");
        foreach (var guid in perkGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var perk = AssetDatabase.LoadAssetAtPath<PerkDefinition>(path);
            if (perk != null) allPerks.Add(perk);
        }
    }

    private void OnGUI()
    {
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

        GUILayout.Label("Overworld Test Tool", EditorStyles.boldLabel);
        GUILayout.Space(5);

        if (playerProgress == null)
        {
            EditorGUILayout.HelpBox("No PlayerProgressData found! Create one first.", MessageType.Error);
            if (GUILayout.Button("Refresh"))
                RefreshAssets();
            EditorGUILayout.EndScrollView();
            return;
        }

        // ========== WAR FUNDS ==========
        GUILayout.Label("War Funds", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"Current: {playerProgress.warFunds}", GUILayout.Width(150));
        if (GUILayout.Button("+100")) { playerProgress.warFunds += 100; MarkDirty(); }
        if (GUILayout.Button("+500")) { playerProgress.warFunds += 500; MarkDirty(); }
        if (GUILayout.Button("+1000")) { playerProgress.warFunds += 1000; MarkDirty(); }
        if (GUILayout.Button("Reset")) { playerProgress.warFunds = 0; MarkDirty(); }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);

        // ========== DOCTRINES ==========
        GUILayout.Label($"Equipped Doctrines ({playerProgress.equippedDoctrines.Count}/5)", EditorStyles.boldLabel);

        // Show equipped
        for (int i = playerProgress.equippedDoctrines.Count - 1; i >= 0; i--)
        {
            var d = playerProgress.equippedDoctrines[i];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"• {d.displayName} ({d.rarity})", GUILayout.Width(250));
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                playerProgress.equippedDoctrines.RemoveAt(i);
                MarkDirty();
            }
            EditorGUILayout.EndHorizontal();
        }

        // Add new
        if (playerProgress.equippedDoctrines.Count < 5)
        {
            GUILayout.Space(5);
            GUILayout.Label("Add Doctrine:");
            foreach (var doctrine in allDoctrines)
            {
                if (playerProgress.equippedDoctrines.Contains(doctrine)) continue;

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"+ {doctrine.displayName}", GUILayout.Width(200)))
                {
                    playerProgress.equippedDoctrines.Add(doctrine);
                    MarkDirty();
                }
                EditorGUILayout.LabelField($"({doctrine.rarity}) - {doctrine.description}", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
            }
        }

        GUILayout.Space(15);

        // ========== PERKS ==========
        GUILayout.Label("Perks", EditorStyles.boldLabel);

        foreach (var perk in allPerks)
        {
            var progress = playerProgress.perkLevels.Find(p => p.perk == perk);
            int currentTier = progress?.currentTier ?? 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"{perk.displayName}", GUILayout.Width(120));
            EditorGUILayout.LabelField($"Tier {currentTier}/{perk.maxTier}", GUILayout.Width(70));

            if (currentTier < perk.maxTier)
            {
                if (GUILayout.Button("+1 Tier", GUILayout.Width(60)))
                {
                    if (progress == null)
                    {
                        progress = new PerkProgress { perk = perk, currentTier = 0 };
                        playerProgress.perkLevels.Add(progress);
                    }
                    progress.currentTier++;
                    ApplyPerkEffect(perk, progress.currentTier);
                    MarkDirty();
                }
            }

            if (currentTier > 0)
            {
                if (GUILayout.Button("Reset", GUILayout.Width(50)))
                {
                    if (progress != null)
                    {
                        playerProgress.perkLevels.Remove(progress);
                        // Reset perk effects
                        playerProgress.bonusCommanderHP = 0;
                        playerProgress.wallDefense = 0;
                        MarkDirty();
                    }
                }
            }

            EditorGUILayout.LabelField(perk.GetTierDescription(currentTier), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(15);

        // ========== CURRENT BONUSES ==========
        GUILayout.Label("Current Bonuses (from perks)", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Bonus Commander HP: +{playerProgress.bonusCommanderHP}");
        EditorGUILayout.LabelField($"Wall Defense: {playerProgress.wallDefense}");

        GUILayout.Space(15);

        // ========== ACTIONS ==========
        GUILayout.Label("Actions", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Save Progress"))
        {
            playerProgress.SaveToJson();
            Debug.Log("Progress saved!");
        }
        if (GUILayout.Button("Reset All Progress"))
        {
            if (EditorUtility.DisplayDialog("Reset Progress", "Are you sure? This will clear all war funds, doctrines, and perks.", "Yes", "Cancel"))
            {
                playerProgress.ResetProgress();
                MarkDirty();
            }
        }
        if (GUILayout.Button("Refresh"))
        {
            RefreshAssets();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "How to test:\n" +
            "1. Add some doctrines above\n" +
            "2. Upgrade some perks\n" +
            "3. Enter Play Mode and start a battle\n" +
            "4. Watch the Console for doctrine/perk messages\n\n" +
            "Examples:\n" +
            "• Military Industrial Complex: +10 gold in round 1\n" +
            "• Extra Lives perk: +HP per tier\n" +
            "• Purse perk: +gold each round",
            MessageType.Info
        );

        EditorGUILayout.EndScrollView();
    }

    private void ApplyPerkEffect(PerkDefinition perk, int tier)
    {
        switch (perk.effectType)
        {
            case PerkEffectType.CommanderHP:
                playerProgress.bonusCommanderHP = perk.effectAmountPerTier * tier;
                break;
            case PerkEffectType.WallDefense:
                playerProgress.wallDefense = perk.effectAmountPerTier * tier;
                break;
        }
    }

    private void MarkDirty()
    {
        EditorUtility.SetDirty(playerProgress);
    }
}
