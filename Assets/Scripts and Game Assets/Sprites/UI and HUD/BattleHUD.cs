using UnityEngine;
using TMPro;


/// <summary>
/// BattleHUD: Central coordinator for all battle UI elements.
/// 
/// RESPONSIBILITIES:
/// - Manages commander displays for both players
/// - Manages army stats displays for both players
/// - Provides unified API for BattleTestRunner
/// - Handles damage number spawning for commanders
/// 
/// DESIGN PHILOSOPHY:
/// - Single entry point for all HUD updates
/// - Decouples BattleTestRunner from individual UI components
/// - Makes it easy to add/remove HUD elements
/// 
/// INTEGRATION:
/// - BattleTestRunner holds a reference to BattleHUD
/// - BattleHUD holds references to all display components
/// - CombatPlayback can query positions for damage numbers
/// 
/// SETUP:
/// 1. Create empty GameObject named "BattleHUD"
/// 2. Add this component
/// 3. Create/assign all child UI elements in inspector
/// 4. Assign BattleHUD to BattleTestRunner
/// </summary>
public class BattleHUD : MonoBehaviour
{
    // ========================================
    // INSPECTOR REFERENCES
    // ========================================


    [Header("Commander Displays")]
    [Tooltip("Player A's commander display")]
    public CommanderDisplay commanderDisplayA;

    [Tooltip("Player B's commander display")]
    public CommanderDisplay commanderDisplayB;

    [Header("Army Stats Displays")]
    [Tooltip("Player A's army stats (total ATK/HP)")]
    public ArmyStatsDisplay armyStatsA;

    [Tooltip("Player B's army stats (total ATK/HP)")]
    public ArmyStatsDisplay armyStatsB;

    [Header("Optional: Commander Sprites")]
    [Tooltip("Portrait sprite for Player A's commander")]
    public Sprite commanderSpriteA;

    [Tooltip("Portrait sprite for Player B's commander")]
    public Sprite commanderSpriteB;

    [Header("Settings")]
    [Tooltip("Enable damage numbers spawning at commander portraits")]
    public bool spawnCommanderDamageNumbers = true;

    [Tooltip("Enable army stat comparison colors")]
    public bool enableStatComparison = true;

    [Header("Damage Number Projection (UI Commanders)")]
    [Tooltip("If your commanders are UI (RectTransform), this Z is used when projecting Screen->World.")]
    public float uiCommanderProjectionZ = 10f;

    [Tooltip("Vertical offset for commander damage numbers (negative = below, positive = above)")]
    public float commanderDamageYOffset = -1.5f;

    [Tooltip("Horizontal offset for commander damage numbers (adjust if commanders are at screen edge)")]
    public float commanderDamageXOffset = 0f;

[Header("Gold Displays (Optional)")]
[Tooltip("Player A's gold display")]
public TextMeshProUGUI goldDisplayA;

[Tooltip("Player B's gold display")]
public TextMeshProUGUI goldDisplayB;

[Header("Gold Display Format")]
[Tooltip("Prefix for gold display")]
public string goldPrefix = "💰 ";

    // ========================================
    // PUBLIC API - INITIALIZATION
    // ========================================

    /// <summary>
    /// Initializes all HUD elements at battle start.
    /// Call this once in BattleTestRunner.Start().
    /// </summary>
    public void Initialize(int commanderStartHP)
    {
        // Initialize commander displays
        if (commanderDisplayA != null)
        {
            commanderDisplayA.Initialize(commanderStartHP, commanderSpriteA, "Player A");
        }

        if (commanderDisplayB != null)
        {
            commanderDisplayB.Initialize(commanderStartHP, commanderSpriteB, "Player B");
        }

        // Initialize army stats to zero
        if (armyStatsA != null)
        {
            armyStatsA.UpdateStats(0, 0, immediate: true);
        }

        if (armyStatsB != null)
        {
            armyStatsB.UpdateStats(0, 0, immediate: true);
        }

        Debug.Log("[BattleHUD] Initialized with commander HP: " + commanderStartHP);

        // Initialize gold displays to starting gold
if (goldDisplayA != null)
{
    UpdateGoldDisplayA(0); // Will be updated in first gold phase
}

if (goldDisplayB != null)
{
    UpdateGoldDisplayB(0);
}
    }

    // ========================================
    // PUBLIC API - COMMANDER UPDATES
    // ========================================

    /// <summary>
    /// Updates Player A's commander HP.
    /// Plays damage effects and spawns damage numbers if HP decreased.
    /// </summary>
    public void UpdateCommanderA(int newHP, int damageAmount = 0)
    {
        if (commanderDisplayA == null) return;

        int oldHP = commanderDisplayA.GetCurrentHP();
        commanderDisplayA.SetHealth(newHP, playDamageEffects: newHP < oldHP);

        // Spawn damage number if damage occurred
        if (spawnCommanderDamageNumbers && damageAmount > 0)
        {
            Vector3 spawnPos = commanderDisplayA.GetDamageNumberSpawnPosition();
            DamageNumberSpawner.Spawn(damageAmount, spawnPos, DamageNumberType.CommanderDamage);
        }
    }

    /// <summary>
    /// Updates Player B's commander HP.
    /// Plays damage effects and spawns damage numbers if HP decreased.
    /// </summary>
    public void UpdateCommanderB(int newHP, int damageAmount = 0)
    {
        if (commanderDisplayB == null) return;

        int oldHP = commanderDisplayB.GetCurrentHP();
        commanderDisplayB.SetHealth(newHP, playDamageEffects: newHP < oldHP);

        // Spawn damage number if damage occurred
        if (spawnCommanderDamageNumbers && damageAmount > 0)
        {
            Vector3 spawnPos = commanderDisplayB.GetDamageNumberSpawnPosition();
            DamageNumberSpawner.Spawn(damageAmount, spawnPos, DamageNumberType.CommanderDamage);
        }
    }

    /// <summary>
    /// Updates both commanders at once (convenience method).
    /// </summary>
    public void UpdateBothCommanders(Player playerA, Player playerB)
    {
        if (commanderDisplayA != null && playerA != null)
        {
            int oldHPA = commanderDisplayA.GetCurrentHP();
            int damageA = Mathf.Max(0, oldHPA - playerA.CommanderHP);
            UpdateCommanderA(playerA.CommanderHP, damageA);
        }

        if (commanderDisplayB != null && playerB != null)
        {
            int oldHPB = commanderDisplayB.GetCurrentHP();
            int damageB = Mathf.Max(0, oldHPB - playerB.CommanderHP);
            UpdateCommanderB(playerB.CommanderHP, damageB);
        }
    }

    // ========================================
    // PUBLIC API - ARMY STATS UPDATES
    // ========================================

    /// <summary>
    /// Refreshes army stats for both players.
    /// Call this after buy phase, combat, or rearrangement.
    /// </summary>
    public void RefreshArmyStats(Player playerA, Player playerB, bool immediate = false)
    {
        if (armyStatsA != null && playerA != null)
        {
            armyStatsA.RefreshFromPlayer(playerA, immediate);
        }

        if (armyStatsB != null && playerB != null)
        {
            armyStatsB.RefreshFromPlayer(playerB, immediate);
        }

        // Update comparison colors if enabled
        if (enableStatComparison)
        {
            UpdateStatComparisons(playerA, playerB);
        }
    }

    /// <summary>
    /// Updates stat comparison colors (advantage/disadvantage indicators).
    /// </summary>
    private void UpdateStatComparisons(Player playerA, Player playerB)
    {
        if (armyStatsA == null || armyStatsB == null) return;
        if (playerA == null || playerB == null) return;

        // Calculate totals for comparison
        (int atkA, int hpA) = CalculateTotalStats(playerA);
        (int atkB, int hpB) = CalculateTotalStats(playerB);

        // Set comparison values
        if (armyStatsA != null)
            armyStatsA.SetComparisonStats(atkB, hpB);

        if (armyStatsB != null)
            armyStatsB.SetComparisonStats(atkA, hpA);
    }

    /// <summary>
    /// Helper method to calculate total army stats.
    /// </summary>
    private (int totalATK, int totalHP) CalculateTotalStats(Player player)
    {
        int totalATK = 0;
        int totalHP = 0;

        if (player != null && player.Rows != null)
        {
            foreach (var rowKvp in player.Rows)
            {
                foreach (var unit in rowKvp.Value.Units)
                {
                    if (unit != null && unit.IsAlive)
                    {
                        totalATK += unit.ATK;
                        totalHP += unit.HP;
                    }
                }
            }
        }

        return (totalATK, totalHP);
    }

// ========================================
// PUBLIC API - GOLD UPDATES
// ========================================

/// <summary>
/// Updates Player A's gold display.
/// </summary>
public void UpdateGoldA(int gold)
{
    UpdateGoldDisplayA(gold);
}

/// <summary>
/// Updates Player B's gold display.
/// </summary>
public void UpdateGoldB(int gold)
{
    UpdateGoldDisplayB(gold);
}

/// <summary>
/// Updates both players' gold at once.
/// </summary>
public void UpdateBothGold(Player playerA, Player playerB)
{
    if (playerA != null)
        UpdateGoldDisplayA(playerA.Gold);
    
    if (playerB != null)
        UpdateGoldDisplayB(playerB.Gold);
}

/// <summary>
/// Internal method to update gold text for Player A.
/// </summary>
private void UpdateGoldDisplayA(int gold)
{
    if (goldDisplayA != null)
        goldDisplayA.text = $"{goldPrefix}{gold}";
}

/// <summary>
/// Internal method to update gold text for Player B.
/// </summary>
private void UpdateGoldDisplayB(int gold)
{
    if (goldDisplayB != null)
        goldDisplayB.text = $"{goldPrefix}{gold}";
}
    // ========================================
    // PUBLIC API - QUERY METHODS
    // ========================================

    /// <summary>
    /// Gets world position for spawning commander A damage numbers.
    /// Used by CombatPlayback for precise damage number placement.
    /// </summary>
    public Vector3 GetCommanderAPosition()
    {
        if (commanderDisplayA == null) return Vector3.zero;
        
        // Get the base RectTransform position
        RectTransform commanderRect = commanderDisplayA.GetComponent<RectTransform>();
        Vector3 basePos = GetCommanderUIWorldPosition(commanderRect);
        
        if (basePos == Vector3.zero) return Vector3.zero;
        
        // Apply customizable offsets (adjust in Inspector)
        basePos.y += commanderDamageYOffset; // Default: -1.5f (below)
        basePos.x += commanderDamageXOffset; // Default: 0f (centered)
        basePos.z = -2f;                     // In front of UI
        
        return basePos;
    }

    /// <summary>
    /// Gets world position for spawning commander B damage numbers.
    /// Used by CombatPlayback for precise damage number placement.
    /// </summary>
    public Vector3 GetCommanderBPosition()
    {
        if (commanderDisplayB == null) return Vector3.zero;
        
        // Get the base RectTransform position
        RectTransform commanderRect = commanderDisplayB.GetComponent<RectTransform>();
        Vector3 basePos = GetCommanderUIWorldPosition(commanderRect);
        
        if (basePos == Vector3.zero) return Vector3.zero;
        
        // Apply customizable offsets (adjust in Inspector)
        basePos.y += commanderDamageYOffset; // Default: -1.5f (below)
        basePos.x += commanderDamageXOffset; // Default: 0f (centered)
        basePos.z = -2f;                     // In front of UI
        
        return basePos;
    }

    /// <summary>
    /// Gets commander position by Player reference.
    /// This is what CombatPlayback should call.
    /// </summary>
    public Vector3 GetCommanderPosition(Player player, Player playerA, Player playerB)
    {
        return GetCommanderPositionFromUI(player, playerA, playerB);
    }

    /// <summary>
    /// Commander displays are UI elements (RectTransform).
    /// Converts UI element position to a world position for damage number spawning.
    /// </summary>
    public Vector3 GetCommanderPositionFromUI(Player target, Player playerA, Player playerB)
    {
        if (target == null) return Vector3.zero;

        if (target == playerA)
            return GetCommanderAPosition();

        if (target == playerB)
            return GetCommanderBPosition();

        return Vector3.zero;
    }

    /// <summary>
    /// Core helper that projects a UI RectTransform into world space.
    /// Works for Screen Space - Overlay and Screen Space - Camera canvases.
    /// </summary>
    private Vector3 GetCommanderUIWorldPosition(RectTransform commanderRect)
    {
        if (commanderRect == null) return Vector3.zero;

        Canvas canvas = commanderRect.GetComponentInParent<Canvas>();
        if (canvas == null) return Vector3.zero;

        // For Overlay canvases, the UI camera should be null for WorldToScreenPoint.
        Camera uiCamera = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;

        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, commanderRect.position);

        Camera worldCamera = Camera.main;
        if (worldCamera == null) return Vector3.zero;

        return worldCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, uiCommanderProjectionZ));
    }

    // ========================================
    // CONVENIENCE METHODS
    // ========================================

    /// <summary>
    /// Full HUD update - refreshes all elements.
    /// Call this at the end of each phase for a complete refresh.
    /// </summary>
    public void FullRefresh(Player playerA, Player playerB, bool immediate = false)
    {
        UpdateBothCommanders(playerA, playerB);
        RefreshArmyStats(playerA, playerB, immediate);
    }

    /// <summary>
    /// Hides/shows the entire HUD (useful for cutscenes or menus).
    /// </summary>
    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }
}

// ========================================
// INTEGRATION GUIDE
// ========================================
//
// === BATTLETESTRUNNER SETUP ===
// Add this field to BattleTestRunner:
//
// [Header("Battle HUD")]
// public BattleHUD battleHUD;
//
// === INITIALIZATION (BattleTestRunner.Start) ===
// if (battleHUD != null)
// {
//     battleHUD.Initialize(commanderStartHP);
// }
//
// === AFTER BUY PHASE ===
// if (battleHUD != null)
// {
//     battleHUD.RefreshArmyStats(playerA, playerB);
// }
//
// === AFTER COMBAT ===
// if (battleHUD != null)
// {
//     battleHUD.UpdateBothCommanders(playerA, playerB);
//     battleHUD.RefreshArmyStats(playerA, playerB);
// }
//
// === FULL REFRESH (anytime) ===
// if (battleHUD != null)
// {
//     battleHUD.FullRefresh(playerA, playerB);
// }
//
// === COMBATPLAYBACK INTEGRATION ===
// In CombatPlayback.HandleEvent(), when processing CommanderDamaged events:
//
// case CombatEventType.CommanderDamaged:
//     if (battleHUD != null)
//     {
//         Vector3 pos = battleHUD.GetCommanderPosition(e.TargetPlayer, playerA, playerB);
//         DamageNumberSpawner.Spawn(e.Amount, pos, DamageNumberType.CommanderDamage);
//     }
//     break;
//