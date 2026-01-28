#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// UnitCardBulkEditor: Custom Editor Window for editing all UnitCardDefinition assets
/// in a spreadsheet-like view.
/// 
/// HOW TO USE:
/// 1. Place this script in an "Editor" folder in your project (e.g., Assets/Editor/)
/// 2. Open via menu: Tools > Unit Card Bulk Editor
/// 3. Click "Refresh" to load all UnitCardDefinition assets
/// 4. Edit values directly in the table
/// 5. Changes are saved automatically (Ctrl+S to save project)
/// 
/// FEATURES:
/// - Spreadsheet-style editing of all cards at once
/// - Sortable columns (click column header)
/// - Search/filter by name
/// - Color-coded by unit type
/// - Resizable columns
/// - Undo support
/// </summary>
public class UnitCardBulkEditor : EditorWindow
{
    // ========================================
    // WINDOW STATE
    // ========================================
    
    private List<UnitCardDefinition> allCards = new List<UnitCardDefinition>();
    private Vector2 scrollPosition;
    private string searchFilter = "";
    
    // Column widths (adjustable)
    private float colName = 100f;
    private float colType = 80f;
    private float colCost = 45f;
    private float colHP = 45f;
    private float colATK = 45f;
    private float colAbility = 140f;
    private float colCountsAsVehicle = 60f;
    private float colMultiplier = 65f;
    private float colAmount = 55f;
    private float colHasPreemptive = 50f;
    private float colPreemptiveTarget = 80f;
    private float colPreemptiveDmg = 50f;
    private float colBackRowOnly = 50f;
    private float colPrefabA = 120f;
    private float colPrefabB = 120f;
    private float colPortrait = 120f;
    
    // Sorting
    private enum SortColumn { Name, Type, Cost, HP, ATK, Ability }
    private SortColumn currentSort = SortColumn.Name;
    private bool sortAscending = true;
    
    // Styles (initialized in OnGUI)
    private GUIStyle headerStyle;
    private GUIStyle rowStyleEven;
    private GUIStyle rowStyleOdd;
    private bool stylesInitialized = false;
    
    // ========================================
    // MENU ITEM - OPENS THE WINDOW
    // ========================================
    
    [MenuItem("Tools/Unit Card Bulk Editor")]
    public static void ShowWindow()
    {
        UnitCardBulkEditor window = GetWindow<UnitCardBulkEditor>("Unit Card Editor");
        window.minSize = new Vector2(800, 400);
        window.RefreshCardList();
    }
    
    // ========================================
    // REFRESH / LOAD CARDS
    // ========================================
    
    private void RefreshCardList()
    {
        allCards.Clear();
        
        // Find all UnitCardDefinition assets in the project
        string[] guids = AssetDatabase.FindAssets("t:UnitCardDefinition");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            UnitCardDefinition card = AssetDatabase.LoadAssetAtPath<UnitCardDefinition>(path);
            
            if (card != null)
            {
                allCards.Add(card);
            }
        }
        
        SortCards();
        
        Debug.Log($"[UnitCardBulkEditor] Loaded {allCards.Count} card definitions.");
    }
    
    private void SortCards()
    {
        switch (currentSort)
        {
            case SortColumn.Name:
                allCards = sortAscending 
                    ? allCards.OrderBy(c => c.cardName).ToList()
                    : allCards.OrderByDescending(c => c.cardName).ToList();
                break;
            case SortColumn.Type:
                allCards = sortAscending 
                    ? allCards.OrderBy(c => c.type).ToList()
                    : allCards.OrderByDescending(c => c.type).ToList();
                break;
            case SortColumn.Cost:
                allCards = sortAscending 
                    ? allCards.OrderBy(c => c.cost).ToList()
                    : allCards.OrderByDescending(c => c.cost).ToList();
                break;
            case SortColumn.HP:
                allCards = sortAscending 
                    ? allCards.OrderBy(c => c.hp).ToList()
                    : allCards.OrderByDescending(c => c.hp).ToList();
                break;
            case SortColumn.ATK:
                allCards = sortAscending 
                    ? allCards.OrderBy(c => c.atk).ToList()
                    : allCards.OrderByDescending(c => c.atk).ToList();
                break;
            case SortColumn.Ability:
                allCards = sortAscending 
                    ? allCards.OrderBy(c => c.ability).ToList()
                    : allCards.OrderByDescending(c => c.ability).ToList();
                break;
        }
    }
    
    private void ToggleSort(SortColumn column)
    {
        if (currentSort == column)
        {
            sortAscending = !sortAscending;
        }
        else
        {
            currentSort = column;
            sortAscending = true;
        }
        SortCards();
    }
    
    // ========================================
    // GUI DRAWING
    // ========================================
    
    private void OnGUI()
    {
        InitStyles();
        
        // Toolbar
        DrawToolbar();
        
        // Column Headers
        DrawColumnHeaders();
        
        // Scrollable card list
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        DrawCardRows();
        EditorGUILayout.EndScrollView();
        
        // Footer with stats
        DrawFooter();
    }
    
    private void InitStyles()
    {
        if (stylesInitialized) return;
        
        headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        
        rowStyleEven = new GUIStyle(GUI.skin.box)
        {
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(2, 2, 2, 2)
        };
        
        rowStyleOdd = new GUIStyle(GUI.skin.box)
        {
            margin = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(2, 2, 2, 2)
        };
        
        stylesInitialized = true;
    }
    
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        // Refresh button
        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            RefreshCardList();
        }
        
        GUILayout.Space(10);
        
        // Search field
        GUILayout.Label("Search:", GUILayout.Width(50));
        string newSearch = EditorGUILayout.TextField(searchFilter, EditorStyles.toolbarSearchField, GUILayout.Width(150));
        if (newSearch != searchFilter)
        {
            searchFilter = newSearch;
        }
        
        if (GUILayout.Button("X", EditorStyles.toolbarButton, GUILayout.Width(20)))
        {
            searchFilter = "";
            GUI.FocusControl(null);
        }
        
        GUILayout.FlexibleSpace();
        
        // Create new card button
        if (GUILayout.Button("+ New Card", EditorStyles.toolbarButton, GUILayout.Width(80)))
        {
            CreateNewCard();
        }
        
        // Save all button
        if (GUILayout.Button("Save All", EditorStyles.toolbarButton, GUILayout.Width(60)))
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[UnitCardBulkEditor] All changes saved.");
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawColumnHeaders()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        // Sortable headers
        if (GUILayout.Button(GetSortLabel("Name", SortColumn.Name), EditorStyles.toolbarButton, GUILayout.Width(colName)))
            ToggleSort(SortColumn.Name);
        
        if (GUILayout.Button(GetSortLabel("Type", SortColumn.Type), EditorStyles.toolbarButton, GUILayout.Width(colType)))
            ToggleSort(SortColumn.Type);
        
        if (GUILayout.Button(GetSortLabel("Cost", SortColumn.Cost), EditorStyles.toolbarButton, GUILayout.Width(colCost)))
            ToggleSort(SortColumn.Cost);
        
        if (GUILayout.Button(GetSortLabel("HP", SortColumn.HP), EditorStyles.toolbarButton, GUILayout.Width(colHP)))
            ToggleSort(SortColumn.HP);
        
        if (GUILayout.Button(GetSortLabel("ATK", SortColumn.ATK), EditorStyles.toolbarButton, GUILayout.Width(colATK)))
            ToggleSort(SortColumn.ATK);
        
        if (GUILayout.Button(GetSortLabel("Ability", SortColumn.Ability), EditorStyles.toolbarButton, GUILayout.Width(colAbility)))
            ToggleSort(SortColumn.Ability);
        
        // Non-sortable headers
        GUILayout.Label("Vehicle", EditorStyles.toolbarButton, GUILayout.Width(colCountsAsVehicle));
        GUILayout.Label("Mult", EditorStyles.toolbarButton, GUILayout.Width(colMultiplier));
        GUILayout.Label("Amt", EditorStyles.toolbarButton, GUILayout.Width(colAmount));
        GUILayout.Label("Pre", EditorStyles.toolbarButton, GUILayout.Width(colHasPreemptive));
        GUILayout.Label("Target", EditorStyles.toolbarButton, GUILayout.Width(colPreemptiveTarget));
        GUILayout.Label("Dmg", EditorStyles.toolbarButton, GUILayout.Width(colPreemptiveDmg));
        GUILayout.Label("Back", EditorStyles.toolbarButton, GUILayout.Width(colBackRowOnly));
        GUILayout.Label("Prefab A", EditorStyles.toolbarButton, GUILayout.Width(colPrefabA));
        GUILayout.Label("Prefab B", EditorStyles.toolbarButton, GUILayout.Width(colPrefabB));
        GUILayout.Label("Portrait", EditorStyles.toolbarButton, GUILayout.Width(colPortrait));
        
        GUILayout.FlexibleSpace();
        
        EditorGUILayout.EndHorizontal();
    }
    
    private string GetSortLabel(string label, SortColumn column)
    {
        if (currentSort == column)
        {
            return label + (sortAscending ? " ▲" : " ▼");
        }
        return label;
    }
    
    private void DrawCardRows()
    {
        if (allCards.Count == 0)
        {
            EditorGUILayout.HelpBox("No UnitCardDefinition assets found. Click 'Refresh' or create a new card.", MessageType.Info);
            return;
        }
        
        int rowIndex = 0;
        
        foreach (var card in allCards)
        {
            if (card == null) continue;
            
            // Apply search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                if (!card.cardName.ToLower().Contains(searchFilter.ToLower()) &&
                    !card.type.ToString().ToLower().Contains(searchFilter.ToLower()) &&
                    !card.ability.ToString().ToLower().Contains(searchFilter.ToLower()))
                {
                    continue;
                }
            }
            
            // Alternate row colors
            Color bgColor = GetRowColor(card.type, rowIndex % 2 == 0);
            GUI.backgroundColor = bgColor;
            
            EditorGUILayout.BeginHorizontal("box");
            GUI.backgroundColor = Color.white;
            
            // Register undo before any changes
            Undo.RecordObject(card, "Edit Card");
            
            // Card Name (clickable to select asset)
            if (GUILayout.Button(card.cardName, EditorStyles.label, GUILayout.Width(colName - 4)))
            {
                Selection.activeObject = card;
                EditorGUIUtility.PingObject(card);
            }
            
            // Type (dropdown)
            card.type = (UnitType)EditorGUILayout.EnumPopup(card.type, GUILayout.Width(colType - 4));
            
            // Cost
            card.cost = EditorGUILayout.IntField(card.cost, GUILayout.Width(colCost - 4));
            
            // HP
            card.hp = EditorGUILayout.IntField(card.hp, GUILayout.Width(colHP - 4));
            
            // ATK
            card.atk = EditorGUILayout.IntField(card.atk, GUILayout.Width(colATK - 4));
            
            // Ability (dropdown)
            card.ability = (UnitAbilityType)EditorGUILayout.EnumPopup(card.ability, GUILayout.Width(colAbility - 4));
            
            // Counts As Vehicle (checkbox)
            card.countsAsVehicle = EditorGUILayout.Toggle(card.countsAsVehicle, GUILayout.Width(colCountsAsVehicle - 4));
            
            // Ability Params - Multiplier
            card.abilityParams.Multiplier = EditorGUILayout.FloatField(card.abilityParams.Multiplier, GUILayout.Width(colMultiplier - 4));
            
            // Ability Params - Amount
            card.abilityParams.Amount = EditorGUILayout.IntField(card.abilityParams.Amount, GUILayout.Width(colAmount - 4));
            
            // Has Preemptive (checkbox)
            card.hasPreemptive = EditorGUILayout.Toggle(card.hasPreemptive, GUILayout.Width(colHasPreemptive - 4));
            
            // Preemptive Target Type (dropdown, only if hasPreemptive)
            EditorGUI.BeginDisabledGroup(!card.hasPreemptive);
            card.preemptiveTargetType = (UnitType)EditorGUILayout.EnumPopup(card.preemptiveTargetType, GUILayout.Width(colPreemptiveTarget - 4));
            
            // Preemptive Damage
            card.preemptiveDamage = EditorGUILayout.IntField(card.preemptiveDamage, GUILayout.Width(colPreemptiveDmg - 4));
            
            // Target Back Row Only (checkbox)
            card.targetBackRowOnly = EditorGUILayout.Toggle(card.targetBackRowOnly, GUILayout.Width(colBackRowOnly - 4));
            EditorGUI.EndDisabledGroup();
            
            // World Prefab A
            card.worldPrefabA = (GameObject)EditorGUILayout.ObjectField(card.worldPrefabA, typeof(GameObject), false, GUILayout.Width(colPrefabA - 4));
            
            // World Prefab B
            card.worldPrefabB = (GameObject)EditorGUILayout.ObjectField(card.worldPrefabB, typeof(GameObject), false, GUILayout.Width(colPrefabB - 4));
            
            // Portrait Sprite
            card.portraitSprite = (Sprite)EditorGUILayout.ObjectField(card.portraitSprite, typeof(Sprite), false, GUILayout.Width(colPortrait - 4));
            
            // Mark dirty if changed
            if (GUI.changed)
            {
                EditorUtility.SetDirty(card);
            }
            
            EditorGUILayout.EndHorizontal();
            
            rowIndex++;
        }
    }
    
    private Color GetRowColor(UnitType type, bool isEven)
    {
        float alpha = isEven ? 0.15f : 0.25f;
        
        switch (type)
        {
            case UnitType.Infantry:
                return new Color(0.9f, 0.7f, 0.5f, alpha); // Tan/brown
            case UnitType.Vehicle:
                return new Color(0.5f, 0.7f, 0.9f, alpha); // Blue
            case UnitType.Air:
                return new Color(0.7f, 0.9f, 1f, alpha);   // Light blue
            case UnitType.Ranged:
                return new Color(0.5f, 0.9f, 0.6f, alpha); // Green
            default:
                return new Color(1f, 1f, 1f, alpha);
        }
    }
    
    private void DrawFooter()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        
        int visibleCount = allCards.Count(c => 
            string.IsNullOrEmpty(searchFilter) || 
            c.cardName.ToLower().Contains(searchFilter.ToLower()) ||
            c.type.ToString().ToLower().Contains(searchFilter.ToLower()) ||
            c.ability.ToString().ToLower().Contains(searchFilter.ToLower()));
        
        GUILayout.Label($"Showing {visibleCount} of {allCards.Count} cards");
        
        GUILayout.FlexibleSpace();
        
        // Quick stats
        if (allCards.Count > 0)
        {
            int totalCost = allCards.Sum(c => c.cost);
            int avgHP = Mathf.RoundToInt((float)allCards.Average(c => c.hp));
            int avgATK = Mathf.RoundToInt((float)allCards.Average(c => c.atk));
            
            GUILayout.Label($"Avg HP: {avgHP} | Avg ATK: {avgATK} | Total Cost: {totalCost}");
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    // ========================================
    // CARD CREATION
    // ========================================
    
    private void CreateNewCard()
    {
        // Create new ScriptableObject
        UnitCardDefinition newCard = ScriptableObject.CreateInstance<UnitCardDefinition>();
        newCard.cardName = "New Card";
        newCard.type = UnitType.Infantry;
        newCard.cost = 1;
        newCard.hp = 5;
        newCard.atk = 3;
        newCard.ability = UnitAbilityType.None;
        newCard.abilityParams = new AbilityParams { Multiplier = 1f, Amount = 0 };
        
        // Find a good save path
        string path = "Assets/ScriptableObjects/Cards";
        if (!AssetDatabase.IsValidFolder(path))
        {
            // Try to create the folder structure
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
            {
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            }
            AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Cards");
        }
        
        // Generate unique filename
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{path}/Card_NewCard.asset");
        
        AssetDatabase.CreateAsset(newCard, assetPath);
        AssetDatabase.SaveAssets();
        
        // Refresh and select
        RefreshCardList();
        Selection.activeObject = newCard;
        EditorGUIUtility.PingObject(newCard);
        
        Debug.Log($"[UnitCardBulkEditor] Created new card at {assetPath}");
    }
    
    // ========================================
    // WINDOW LIFECYCLE
    // ========================================
    
    private void OnEnable()
    {
        RefreshCardList();
    }
    
    private void OnFocus()
    {
        // Optional: auto-refresh when window gains focus
        // RefreshCardList();
    }
}
#endif