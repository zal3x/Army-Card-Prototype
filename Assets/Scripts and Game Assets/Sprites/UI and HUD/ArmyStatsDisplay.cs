using UnityEngine;
using TMPro;

/// <summary>
/// ArmyStatsDisplay: Shows total ATK and HP for all units on a player's field.
/// 
/// FEATURES:
/// - Real-time summing of all unit stats
/// - Animated number transitions
/// - Color-coded stats (green for advantage, red for disadvantage)
/// - Optional icons for ATK/HP
/// 
/// UPDATES:
/// - After buy phase (new units placed)
/// - After combat (units take damage/die)
/// - During rearrangement (no stat change but visual refresh)
/// 
/// INTEGRATION:
/// - Called by BattleTestRunner after each phase
/// - Reads directly from Player.Rows data structure
/// 
/// SETUP:
/// 1. Create UI panel with TextMeshProUGUI for ATK and HP
/// 2. Add optional icons (sword for ATK, shield for HP)
/// 3. Assign to BattleTestRunner for both players
/// </summary>
public class ArmyStatsDisplay : MonoBehaviour
{
    // ========================================
    // INSPECTOR REFERENCES
    // ========================================

    [Header("Text Fields")]
    [Tooltip("Displays total attack (e.g., 'ATK: 45')")]
    public TextMeshProUGUI attackText;

    [Tooltip("Displays total health (e.g., 'HP: 120')")]
    public TextMeshProUGUI healthText;

    [Header("Display Format")]
    [Tooltip("Prefix for attack display")]
    public string attackPrefix = "⚔️ ";

    [Tooltip("Prefix for health display")]
    public string healthPrefix = "❤️ ";

    [Header("Animation")]
    [Tooltip("Enable number count-up animation")]
    public bool animateNumbers = true;

    [Tooltip("Animation duration (seconds)")]
    public float animationDuration = 0.3f;

    [Header("Comparison Colors (Optional)")]
[Tooltip("Enable color coding based on comparison with opponent")]
public bool enableComparisonColors = false;

[Header("ATK Comparison Colors")]
[Tooltip("Color when ATK is higher than opponent")]
public Color atkAdvantageColor = new Color(1f, 0.4f, 0.2f); // Orange/Red (aggressive)

[Tooltip("Color when ATK is equal to opponent")]
public Color atkNeutralColor = new Color(1f, 1f, 0.6f); // Light Yellow

[Tooltip("Color when ATK is lower than opponent")]
public Color atkDisadvantageColor = new Color(0.7f, 0.7f, 0.7f); // Gray

[Header("HP Comparison Colors")]
[Tooltip("Color when HP is higher than opponent")]
public Color hpAdvantageColor = new Color(0.3f, 1f, 0.5f); // Bright Green (healthy)

[Tooltip("Color when HP is equal to opponent")]
public Color hpNeutralColor = new Color(0.6f, 0.8f, 1f); // Light Blue

[Tooltip("Color when HP is lower than opponent")]
public Color hpDisadvantageColor = new Color(1f, 0.5f, 0.5f); // Light Red (danger)

    // ========================================
    // PRIVATE STATE
    // ========================================

    private int currentATK = 0;
    private int currentHP = 0;
    private int comparisonATK = 0; // Opponent's ATK (for color coding)
    private int comparisonHP = 0;  // Opponent's HP (for color coding)

    // ========================================
    // PUBLIC API
    // ========================================

    /// <summary>
    /// Updates display with new army stats.
    /// Animates numbers if enabled.
    /// </summary>
    public void UpdateStats(int totalATK, int totalHP, bool immediate = false)
    {
        int oldATK = currentATK;
        int oldHP = currentHP;

        currentATK = totalATK;
        currentHP = totalHP;

        if (immediate || !animateNumbers)
        {
            SetATKText(currentATK);
            SetHPText(currentHP);
        }
        else
        {
            AnimateNumber(attackText, oldATK, currentATK, isATK: true);
            AnimateNumber(healthText, oldHP, currentHP, isATK: false);
        }

        UpdateColors();
    }

    /// <summary>
    /// Sets comparison values for color coding.
    /// Call this with opponent's stats to enable advantage/disadvantage colors.
    /// </summary>
    public void SetComparisonStats(int enemyATK, int enemyHP)
    {
        comparisonATK = enemyATK;
        comparisonHP = enemyHP;
        UpdateColors();
    }

    /// <summary>
    /// Refreshes stats by reading directly from a Player's board.
    /// This is a convenience method - you can also calculate externally and call UpdateStats().
    /// </summary>
    public void RefreshFromPlayer(Player player, bool immediate = false)
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

        UpdateStats(totalATK, totalHP, immediate);
    }

    // ========================================
    // PRIVATE HELPERS
    // ========================================

    /// <summary>
    /// Sets ATK text directly (no animation).
    /// </summary>
    private void SetATKText(int value)
    {
        if (attackText != null)
            attackText.text = $"{attackPrefix}{value}";
    }

    /// <summary>
    /// Sets HP text directly (no animation).
    /// </summary>
    private void SetHPText(int value)
    {
        if (healthText != null)
            healthText.text = $"{healthPrefix}{value}";
    }

    /// <summary>
    /// Animates number from old value to new value.
    /// </summary>
    private void AnimateNumber(TextMeshProUGUI textField, int oldValue, int newValue, bool isATK)
    {
        if (textField == null) return;

        LeanTween.cancel(textField.gameObject);

        LeanTween.value(textField.gameObject, (float)oldValue, (float)newValue, animationDuration)
            .setEaseOutQuad()
            .setOnUpdate((float val) => {
                int displayValue = Mathf.RoundToInt(val);
                if (isATK)
                    SetATKText(displayValue);
                else
                    SetHPText(displayValue);
            });
    }

 /// <summary>
/// Updates text colors based on comparison with opponent stats.
/// </summary>
private void UpdateColors()
{
    if (!enableComparisonColors) return;

    // ATK comparison (uses ATK color scheme)
    if (attackText != null)
    {
        if (currentATK > comparisonATK)
            attackText.color = atkAdvantageColor;
        else if (currentATK < comparisonATK)
            attackText.color = atkDisadvantageColor;
        else
            attackText.color = atkNeutralColor;
    }

    // HP comparison (uses HP color scheme)
    if (healthText != null)
    {
        if (currentHP > comparisonHP)
            healthText.color = hpAdvantageColor;
        else if (currentHP < comparisonHP)
            healthText.color = hpDisadvantageColor;
        else
            healthText.color = hpNeutralColor;
    }
}

    // ========================================
    // OPTIONAL: AUTO-REFRESH MODE
    // ========================================
    // If you want stats to update automatically every frame (not recommended for performance),
    // you can enable this. Better to call RefreshFromPlayer() explicitly after each phase.

    private Player trackedPlayer;
    private bool autoRefresh = false;

    /// <summary>
    /// Enables automatic stat refreshing every frame (NOT RECOMMENDED for production).
    /// Better to call RefreshFromPlayer() explicitly after buy/combat phases.
    /// </summary>
    public void EnableAutoRefresh(Player player)
    {
        trackedPlayer = player;
        autoRefresh = true;
    }

    /// <summary>
    /// Disables automatic refreshing.
    /// </summary>
    public void DisableAutoRefresh()
    {
        autoRefresh = false;
        trackedPlayer = null;
    }

    private void Update()
    {
        if (autoRefresh && trackedPlayer != null)
        {
            RefreshFromPlayer(trackedPlayer, immediate: true);
        }
    }
}

// ========================================
// USAGE EXAMPLES
// ========================================
//
// === SETUP IN BATTLETESTRUNNER ===
// [Header("Army Stats Displays")]
// public ArmyStatsDisplay armyStatsA;
// public ArmyStatsDisplay armyStatsB;
//
// === INITIALIZATION (optional) ===
// armyStatsA.UpdateStats(0, 0, immediate: true);
// armyStatsB.UpdateStats(0, 0, immediate: true);
//
// === AFTER BUY PHASE ===
// armyStatsA.RefreshFromPlayer(playerA);
// armyStatsB.RefreshFromPlayer(playerB);
//
// // Optional: enable comparison colors
// int atkB, hpB = GetTotalStats(playerB);
// armyStatsA.SetComparisonStats(atkB, hpB);
//
// === AFTER COMBAT ===
// armyStatsA.RefreshFromPlayer(playerA);
// armyStatsB.RefreshFromPlayer(playerB);
//
// === MANUAL CALCULATION (alternative) ===
// int totalATK = 0, totalHP = 0;
// foreach (var row in playerA.Rows.Values) {
//     foreach (var unit in row.Units) {
//         if (unit.IsAlive) {
//             totalATK += unit.ATK;
//             totalHP += unit.HP;
//         }
//     }
// }
// armyStatsA.UpdateStats(totalATK, totalHP);
//