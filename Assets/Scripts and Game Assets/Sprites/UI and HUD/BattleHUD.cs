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

    [Header("Firepower Display (Center Panel)")]
    [Tooltip("Shows firepower for both players (ATK - Enemy HP). Only the player with higher firepower deals commander damage.")]
    public FirepowerDisplay firepowerDisplay;

    [Header("Optional: Commander Sprites")]
    [Tooltip("Portrait sprite for Player A's commander (default if not overridden)")]
    public Sprite commanderSpriteA;

    [Tooltip("Portrait sprite for Player B's commander (default if not overridden by enemy commander)")]
    public Sprite commanderSpriteB;

    // Runtime override for enemy commander portrait (set by BattleTestRunner)
    private Sprite runtimeEnemyPortrait;

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
    /// Sets the enemy commander's portrait for this battle.
    /// Call this before Initialize() when starting a battle from the world map.
    /// </summary>
    public void SetEnemyCommander(CommanderDefinition enemyCommander)
    {
        if (enemyCommander != null && enemyCommander.mapPortrait != null)
        {
            runtimeEnemyPortrait = enemyCommander.mapPortrait;
            Debug.Log($"[BattleHUD] Set enemy commander portrait: {enemyCommander.displayName}");
        }
        else
        {
            runtimeEnemyPortrait = null;
            Debug.Log("[BattleHUD] Enemy commander has no portrait, using default");
        }
    }

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

        // Use runtime enemy portrait if set, otherwise fallback to Inspector default
        Sprite enemyPortrait = runtimeEnemyPortrait != null ? runtimeEnemyPortrait : commanderSpriteB;
        if (commanderDisplayB != null)
        {
            commanderDisplayB.Initialize(commanderStartHP, enemyPortrait, "Player B");
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

        // Initialize firepower display
        if (firepowerDisplay != null)
        {
            firepowerDisplay.Reset();
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
    // UNIFIED COMMANDER HEALTH SYSTEM
    // ========================================

    /// <summary>
    /// Called when a unit is deployed to the battlefield.
    /// Expands the commander's health pool by the unit's HP.
    /// </summary>
    public void OnUnitDeployed(Player player, Unit unit, Player playerA, Player playerB)
    {
        if (player == null || unit == null) return;

        var settings = CombatVisualSettings.Instance;
        if (settings == null || !settings.useUnifiedCommanderHealth) return;

        // Expand the appropriate commander's health bar
        if (player == playerA && commanderDisplayA != null)
        {
            commanderDisplayA.ExpandMaxHP(unit.MaxHP, animate: true);
            Debug.Log($"[BattleHUD] Player A deployed {unit.Name}: +{unit.MaxHP} HP (Total: {player.CommanderMaxHP})");
        }
        else if (player == playerB && commanderDisplayB != null)
        {
            commanderDisplayB.ExpandMaxHP(unit.MaxHP, animate: true);
            Debug.Log($"[BattleHUD] Player B deployed {unit.Name}: +{unit.MaxHP} HP (Total: {player.CommanderMaxHP})");
        }

        // Refresh army stats to show updated totals
        RefreshArmyStats(playerA, playerB);
    }

    /// <summary>
    /// Updates commander HP displays with specific staged values.
    /// Used for visual feedback during explosion sequences - HP drops as units explode.
    /// Does NOT spawn damage numbers (those are handled separately in the finale).
    /// </summary>
    public void UpdateCommanderHPStaged(Player playerA, int visualHPA, Player playerB, int visualHPB)
    {
        if (commanderDisplayA != null && playerA != null)
        {
            commanderDisplayA.SetHealth(visualHPA, playDamageEffects: false);
        }

        if (commanderDisplayB != null && playerB != null)
        {
            commanderDisplayB.SetHealth(visualHPB, playDamageEffects: false);
        }
    }

    /// <summary>
    /// Syncs commander displays to current player state after combat.
    /// Shrinks health bars to reflect surviving units.
    /// </summary>
    public void SyncCommanderHealthState(Player playerA, Player playerB)
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null || !settings.useUnifiedCommanderHealth) return;

        if (commanderDisplayA != null && playerA != null)
        {
            commanderDisplayA.SetHealthState(playerA.CommanderHP, playerA.CommanderMaxHP, animate: true);
            Debug.Log($"[BattleHUD] Synced Player A health: {playerA.CommanderHP}/{playerA.CommanderMaxHP}");
        }

        if (commanderDisplayB != null && playerB != null)
        {
            commanderDisplayB.SetHealthState(playerB.CommanderHP, playerB.CommanderMaxHP, animate: true);
            Debug.Log($"[BattleHUD] Synced Player B health: {playerB.CommanderHP}/{playerB.CommanderMaxHP}");
        }
    }

    /// <summary>
    /// Initializes unified health display for a player.
    /// Call this at battle start after setting up the Player's CommanderBaseHP.
    /// </summary>
    public void InitializeUnifiedHealth(Player playerA, Player playerB)
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null || !settings.useUnifiedCommanderHealth) return;

        if (commanderDisplayA != null && playerA != null)
        {
            commanderDisplayA.SetHealthState(playerA.CommanderHP, playerA.CommanderMaxHP, animate: false);
        }

        if (commanderDisplayB != null && playerB != null)
        {
            commanderDisplayB.SetHealthState(playerB.CommanderHP, playerB.CommanderMaxHP, animate: false);
        }
    }

    // ========================================
    // PUBLIC API - ARMY STATS UPDATES
    // ========================================

    /// <summary>
    /// Refreshes army stats for both players.
    /// Call this after buy phase, combat, or rearrangement.
    /// Uses ability-aware ATK calculations (includes double ATK abilities).
    /// </summary>
    public void RefreshArmyStats(Player playerA, Player playerB, bool immediate = false)
    {
        // Pass enemy context for ability-aware ATK calculations
        if (armyStatsA != null && playerA != null)
        {
            armyStatsA.RefreshFromPlayer(playerA, playerB, immediate);
        }

        if (armyStatsB != null && playerB != null)
        {
            armyStatsB.RefreshFromPlayer(playerB, playerA, immediate);
        }

        // Update comparison colors if enabled
        if (enableStatComparison)
        {
            UpdateStatComparisons(playerA, playerB);
        }

        // Update firepower display (center panel)
        if (firepowerDisplay != null)
        {
            firepowerDisplay.Refresh(playerA, playerB, immediate);
        }
    }

    /// <summary>
    /// Updates stat comparison colors (advantage/disadvantage indicators).
    /// </summary>
    private void UpdateStatComparisons(Player playerA, Player playerB)
    {
        if (armyStatsA == null || armyStatsB == null) return;
        if (playerA == null || playerB == null) return;

        // Calculate totals for comparison (with enemy context for ability calculations)
        (int atkA, int hpA) = CalculateTotalStats(playerA, playerB);
        (int atkB, int hpB) = CalculateTotalStats(playerB, playerA);

        // Set comparison values
        if (armyStatsA != null)
            armyStatsA.SetComparisonStats(atkB, hpB);

        if (armyStatsB != null)
            armyStatsB.SetComparisonStats(atkA, hpA);
    }

    /// <summary>
    /// Helper method to calculate total army stats.
    /// Uses ability-aware ATK calculation (includes double ATK abilities).
    /// </summary>
    private (int totalATK, int totalHP) CalculateTotalStats(Player player, Player enemy = null)
    {
        int totalATK = 0;
        int totalHP = 0;

        if (player != null && player.Rows != null)
        {
            foreach (var rowKvp in player.Rows)
            {
                RowType rowType = rowKvp.Key;
                var row = rowKvp.Value;

                // Build count by name for this row (for 3-of-a-kind)
                var aliveCountByName = new System.Collections.Generic.Dictionary<string, int>();
                foreach (var u in row.Units)
                {
                    if (u != null && u.IsAlive)
                    {
                        if (!aliveCountByName.ContainsKey(u.Name))
                            aliveCountByName[u.Name] = 0;
                        aliveCountByName[u.Name]++;
                    }
                }

                foreach (var unit in row.Units)
                {
                    if (unit != null && unit.IsAlive)
                    {
                        // Use ability-aware ATK calculation
                        int effectiveATK = AbilitySystem.GetEffectiveAttackForBuyPhase(
                            unit, player, enemy, rowType, aliveCountByName);

                        totalATK += effectiveATK;
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

        // Use the portrait RectTransform so we target the actual face, not the panel root
        RectTransform commanderRect = commanderDisplayA.GetPortraitRectTransform()
                                   ?? commanderDisplayA.GetComponent<RectTransform>();
        Vector3 basePos = ScreenUIToGameWorldPosition(commanderRect);

        if (basePos == Vector3.zero) return Vector3.zero;

        basePos.y += commanderDamageYOffset;
        basePos.x += commanderDamageXOffset;
        return basePos;
    }

    /// <summary>
    /// Gets world position for spawning commander B damage numbers and firing targets.
    /// </summary>
    public Vector3 GetCommanderBPosition()
    {
        if (commanderDisplayB == null) return Vector3.zero;

        // Use the portrait RectTransform so we target the actual face, not the panel root
        RectTransform commanderRect = commanderDisplayB.GetPortraitRectTransform()
                                   ?? commanderDisplayB.GetComponent<RectTransform>();
        Vector3 basePos = ScreenUIToGameWorldPosition(commanderRect);

        if (basePos == Vector3.zero) return Vector3.zero;

        basePos.y += commanderDamageYOffset;
        basePos.x += commanderDamageXOffset;
        return basePos;
    }

    [Header("Portrait Sprite Effect Positioning")]
    [Tooltip("World Z plane where sprite overlay effects are placed (match your battlefield sprites, e.g. -0.5)")]
    public float portraitEffectWorldZ = -0.5f;

    /// <summary>
    /// Gets the world position of the commander portrait image center,
    /// projected onto the game world z-plane for SpriteRenderer-based effects.
    /// Uses ray casting so it works for both orthographic and perspective cameras.
    /// </summary>
    public Vector3 GetCommanderPortraitPositionA()
    {
        if (commanderDisplayA == null) return Vector3.zero;
        RectTransform portraitRT = commanderDisplayA.GetPortraitRectTransform();
        if (portraitRT != null)
            return ScreenUIToGameWorldPosition(portraitRT);
        return GetCommanderAPosition();
    }

    public Vector3 GetCommanderPortraitPositionB()
    {
        if (commanderDisplayB == null) return Vector3.zero;
        RectTransform portraitRT = commanderDisplayB.GetPortraitRectTransform();
        if (portraitRT != null)
            return ScreenUIToGameWorldPosition(portraitRT);
        return GetCommanderBPosition();
    }

    /// <summary>
    /// Projects a Screen Space - Overlay UI element onto the game world z-plane.
    /// Casts a ray from the camera through the element's screen position and finds
    /// the intersection with the target z-plane. Works for perspective and orthographic.
    /// </summary>
    private Vector3 ScreenUIToGameWorldPosition(RectTransform rt)
    {
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        if (canvas == null) return Vector3.zero;

        Camera uiCamera = (canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : canvas.worldCamera;
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, rt.position);

        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Ray ray = cam.ScreenPointToRay(screenPos);

        // Intersect ray with the target world z-plane
        if (Mathf.Abs(ray.direction.z) < 0.0001f) return Vector3.zero; // ray parallel to z-plane
        float t = (portraitEffectWorldZ - ray.origin.z) / ray.direction.z;
        if (t < 0) return Vector3.zero; // intersection behind camera

        Vector3 worldPos = ray.origin + ray.direction * t;
        worldPos.z = portraitEffectWorldZ;
        return worldPos;
    }

    public Vector3 GetCommanderPortraitPosition(Player player, Player playerA, Player playerB)
    {
        if (player == playerA) return GetCommanderPortraitPositionA();
        if (player == playerB) return GetCommanderPortraitPositionB();
        return Vector3.zero;
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
        UpdateBothGold(playerA, playerB);
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