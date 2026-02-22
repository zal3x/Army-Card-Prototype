using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// FirepowerDisplay: Shows the "effective firepower" for each player.
///
/// CALCULATION:
/// Firepower = Player's Total ATK - Enemy's Total HP
///
/// MEANING:
/// - Positive value = Potential commander damage if you win
/// - Only the player with HIGHER Firepower deals commander damage
/// - Commander damage = Winner's Firepower - Loser's Firepower
///
/// FINAL DAMAGE DISPLAYS:
/// - Final Damage: How much Player A will deal to Commander B (only if A wins)
/// - Final Received: How much damage Player A will take (only if B wins)
/// </summary>
public class FirepowerDisplay : MonoBehaviour
{
    // ========================================
    // INSPECTOR REFERENCES
    // ========================================

    [Header("Player A (Left Side)")]
    [Tooltip("Firepower value text for Player A")]
    public TextMeshProUGUI firepowerTextA;

    [Tooltip("Optional: Background image for Player A (for color changes)")]
    public Image backgroundA;

    [Header("Player B (Right Side)")]
    [Tooltip("Firepower value text for Player B")]
    public TextMeshProUGUI firepowerTextB;

    [Tooltip("Optional: Background image for Player B (for color changes)")]
    public Image backgroundB;

    [Header("Final Damage (Player A Deals) - DEPRECATED")]
    [Tooltip("DEPRECATED: No longer used with unified health system. Shows how much damage Player A will deal.")]
    public TextMeshProUGUI finalDamageText;

    [Header("Final Received Damage (Player A Takes) - DEPRECATED")]
    [Tooltip("DEPRECATED: No longer used with unified health system. Shows how much damage Player A will receive.")]
    public TextMeshProUGUI finalReceivedText;

    [Tooltip("If true, hides the final damage texts (deprecated with unified health system)")]
    public bool hideFinalDamageTexts = true;

    // ========================================
    // ROW ATTACK SUM DISPLAYS (DEBUG/TESTING)
    // ========================================

    [Header("Row Attack Sums - Player A")]
    [Tooltip("Shows sum of ATK for Player A's Infantry row")]
    public TextMeshProUGUI rowSumInfantryA;

    [Tooltip("Shows sum of ATK for Player A's Vehicle row")]
    public TextMeshProUGUI rowSumVehicleA;

    [Tooltip("Shows sum of ATK for Player A's 3rd row (Air/Ranged)")]
    public TextMeshProUGUI rowSum3rdRowA;

    [Header("Row Attack Sums - Player B")]
    [Tooltip("Shows sum of ATK for Player B's Infantry row")]
    public TextMeshProUGUI rowSumInfantryB;

    [Tooltip("Shows sum of ATK for Player B's Vehicle row")]
    public TextMeshProUGUI rowSumVehicleB;

    [Tooltip("Shows sum of ATK for Player B's 3rd row (Air/Ranged)")]
    public TextMeshProUGUI rowSum3rdRowB;

    [Header("Row Sum Text Formatting")]
    [Tooltip("Prefix for Infantry row sum (leave empty for just the number)")]
    public string rowSumInfantryPrefix = "";

    [Tooltip("Prefix for Vehicle row sum (leave empty for just the number)")]
    public string rowSumVehiclePrefix = "";

    [Tooltip("Prefix for 3rd row sum (leave empty for just the number)")]
    public string rowSum3rdRowPrefix = "";

    [Tooltip("Color for row sum text")]
    public Color rowSumTextColor = Color.white;

    // ========================================
    // TUG-OF-WAR GAUGE
    // ========================================

    [Header("Tug-of-War Gauge")]
    [Tooltip("The sliding indicator that moves left/right based on advantage")]
    public RectTransform gaugeIndicator;

    [Tooltip("The background track for the gauge")]
    public Image gaugeTrack;

    [Tooltip("Optional: Fill image for Player A's advantage (right side)")]
    public Image gaugeFillA;

    [Tooltip("Optional: Fill image for Player B's advantage (left side)")]
    public Image gaugeFillB;

    [Tooltip("Optional: Text showing the difference value on the indicator")]
    public TextMeshProUGUI gaugeDifferenceText;

    [Header("Gauge Settings")]
    [Tooltip("Starting/minimum range for the gauge (e.g., 50 means +/-50 is initial max)")]
    public float gaugeBaseRange = 50f;

    [Tooltip("How much the range expands when indicator gets close to edge (multiplier)")]
    public float gaugeRangeGrowthFactor = 1.5f;

    [Tooltip("When indicator reaches this % of the range, expand it (0.7 = 70%)")]
    [Range(0.5f, 0.95f)]
    public float gaugeExpandThreshold = 0.75f;

    [Tooltip("Width of the gauge track in pixels (indicator moves within this range)")]
    public float gaugeTrackWidth = 400f;

    [Tooltip("Animation duration for gauge movement")]
    public float gaugeAnimationDuration = 0.3f;

    [Tooltip("Color for Player A advantage (indicator/fill when moving right)")]
    public Color gaugeColorA = new Color(0.2f, 0.8f, 0.2f); // Green

    [Tooltip("Color for Player B advantage (indicator/fill when moving left)")]
    public Color gaugeColorB = new Color(0.8f, 0.2f, 0.2f); // Red

    [Tooltip("Color when neutral (no advantage)")]
    public Color gaugeColorNeutral = new Color(0.5f, 0.5f, 0.5f); // Gray

    [Header("Colors")]
    [Tooltip("Color when firepower is 0 or negative (no commander damage)")]
    public Color normalColor = Color.white;

    [Tooltip("Color when firepower is positive (potential commander damage)")]
    public Color commanderDamageColor = new Color(1f, 0.84f, 0f); // Gold

    [Tooltip("Color for damage Player A takes")]
    public Color receivedDamageColor = new Color(1f, 0.3f, 0.3f); // Red

    [Tooltip("Color when value is 0")]
    public Color neutralColor = new Color(0.7f, 0.7f, 0.7f); // Gray

    [Header("Display Format")]
    [Tooltip("Show + sign for positive values")]
    public bool showPlusSign = true;

    [Header("Animation")]
    [Tooltip("Enable number count animation")]
    public bool animateNumbers = true;

    [Tooltip("Duration for number tick animation (seconds) - slot machine style")]
    public float animationDuration = 0.8f;

    [Tooltip("Delay between each number tick (seconds) - creates slot machine feel")]
    public float tickDelay = 0.05f;

    [Tooltip("Pulse animation when value becomes positive")]
    public bool pulseOnPositive = true;

    [Tooltip("Pulse scale amount")]
    public float pulseScale = 1.2f;

    [Header("Decrease Animation (Round End)")]
    [Tooltip("Speed multiplier when value is DECREASING (e.g., round end reset). Higher = faster countdown.")]
    [Range(1f, 20f)]
    public float decreaseSpeedMultiplier = 10f;

    [Tooltip("When decrease is larger than this, skip multiple numbers per tick instead of showing every number.")]
    public int skipThreshold = 50;

    [Tooltip("How many numbers to skip per tick when decrease exceeds skipThreshold. Higher = faster.")]
    [Range(1, 50)]
    public int skipStepSize = 5;

    [Header("Gauge Animation")]
    [Tooltip("Use stepped/tick animation instead of smooth slide")]
    public bool useTickAnimation = true;

    [Tooltip("Time between each step of the gauge (seconds)")]
    public float gaugeTickInterval = 0.08f;

    [Tooltip("How many units the gauge moves per tick")]
    public int gaugeStepSize = 1;

    [Header("Debug")]
    [Tooltip("Log calculation details to console")]
    public bool debugLog = false;

    // ========================================
    // PRIVATE STATE
    // ========================================

    private int currentFirepowerA = 0;
    private int currentFirepowerB = 0;
    private int currentFinalDamage = 0;
    private int currentReceivedDamage = 0;
    private float currentGaugeDifference = 0f;

    // Dynamic gauge range - only grows, never shrinks (within a round)
    private float currentGaugeRange = 0f;

    // Track running coroutines so we can stop them when starting new animations
    private Coroutine runningGaugeAnimation = null;

    // Track number animation coroutines per text field
    private System.Collections.Generic.Dictionary<TextMeshProUGUI, Coroutine> runningNumberAnimations =
        new System.Collections.Generic.Dictionary<TextMeshProUGUI, Coroutine>();

    // ========================================
    // PUBLIC API
    // ========================================

    /// <summary>
    /// Updates the firepower display for both players.
    /// Call this after any board state change.
    /// </summary>
    public void Refresh(Player playerA, Player playerB, bool immediate = false)
    {
        if (playerA == null || playerB == null)
        {
            if (debugLog)
                Debug.LogWarning($"[FirepowerDisplay] Refresh called with null player! A={playerA != null}, B={playerB != null}");
            return;
        }

        // Warn if same text field assigned to multiple slots (common mistake)
        if (firepowerTextA != null && firepowerTextA == firepowerTextB)
            Debug.LogError("[FirepowerDisplay] ERROR: firepowerTextA and firepowerTextB are the SAME text field!");
        if (finalDamageText != null && finalDamageText == finalReceivedText)
            Debug.LogError("[FirepowerDisplay] ERROR: finalDamageText and finalReceivedText are the SAME text field!");

        // Calculate total stats for each player (with enemy context for ability calculations)
        // Player A's enemy is Player B, and vice versa
        (int atkA, int hpA) = CalculateTotalStats(playerA, playerB);
        (int atkB, int hpB) = CalculateTotalStats(playerB, playerA);

        // NEW UNIFIED HEALTH SYSTEM:
        // Firepower is now just raw total ATK (no longer ATK - Enemy HP)
        // Unit HP is part of commander health, not separate
        int firepowerA = atkA;
        int firepowerB = atkB;

        // Calculate row-specific attack sums (with enemy context for ability calculations)
        (int infA, int vehA, int row3A) = CalculateRowAttackSums(playerA, playerB);
        (int infB, int vehB, int row3B) = CalculateRowAttackSums(playerB, playerA);

        // Always log stats calculation (helps debug display issues)
        if (debugLog)
        {
            Debug.Log($"[FirepowerDisplay] Player A ({playerA.Name}): Total ATK={atkA} | Player B ({playerB.Name}): Total ATK={atkB}");
            Debug.Log($"[FirepowerDisplay] Firepower A: {firepowerA} | Firepower B: {firepowerB}");
            Debug.Log($"[FirepowerDisplay] Row Sums A: Inf={infA}, Veh={vehA}, 3rd={row3A}");
            Debug.Log($"[FirepowerDisplay] Row Sums B: Inf={infB}, Veh={vehB}, 3rd={row3B}");
        }

        // Update Player A display
        UpdateFirepowerText(
            firepowerTextA,
            backgroundA,
            firepowerA,
            ref currentFirepowerA,
            immediate
        );

        // Update Player B display
        UpdateFirepowerText(
            firepowerTextB,
            backgroundB,
            firepowerB,
            ref currentFirepowerB,
            immediate
        );

        // Debug: Log text AFTER update to verify it was set
        if (debugLog && firepowerTextB != null)
            Debug.Log($"[FirepowerDisplay] TextB AFTER update: '{firepowerTextB.text}'");

        // ========================================
        // FINAL DAMAGE DISPLAYS (UNIFIED HEALTH SYSTEM)
        // ========================================
        // In the unified health system, ALL enemy ATK reduces your CommanderHP:
        // - Unit absorption: OnUnitDamaged() reduces CommanderHP
        // - Overflow: Goes to commander after units die
        // So the TOTAL incoming damage = enemy's full ATK (firepowerB)
        //
        // OLD FORMULA (WRONG): damageReceived = firepowerB - firepowerA
        //   This only showed overflow difference, missing unit absorption damage
        //
        // NEW FORMULA: Show full enemy ATK as incoming damage
        //   This accurately represents total HP you'll lose

        if (!hideFinalDamageTexts)
        {
            // In unified health: ALL enemy ATK = damage to your CommanderHP
            int damageDealt = firepowerA;    // Your total ATK = damage to enemy
            int damageReceived = firepowerB; // Enemy total ATK = damage to you

            UpdateFinalDamageText(finalDamageText, damageDealt, ref currentFinalDamage, commanderDamageColor, immediate);
            UpdateFinalReceivedText(finalReceivedText, damageReceived, ref currentReceivedDamage, receivedDamageColor, immediate);
        }

        // ========================================
        // ROW ATTACK SUM DISPLAYS
        // ========================================

        UpdateRowSumText(rowSumInfantryA, infA, rowSumInfantryPrefix);
        UpdateRowSumText(rowSumVehicleA, vehA, rowSumVehiclePrefix);
        UpdateRowSumText(rowSum3rdRowA, row3A, rowSum3rdRowPrefix);
        UpdateRowSumText(rowSumInfantryB, infB, rowSumInfantryPrefix);
        UpdateRowSumText(rowSumVehicleB, vehB, rowSumVehiclePrefix);
        UpdateRowSumText(rowSum3rdRowB, row3B, rowSum3rdRowPrefix);

        // Update Tug-of-War Gauge
        // Difference: positive = Player A advantage, negative = Player B advantage
        // In unified health: both players deal their full ATK to enemy CommanderHP
        // The difference shows who has the firepower advantage (likely to win)
        int gaugeDifference = firepowerA - firepowerB;
        UpdateTugOfWarGauge(gaugeDifference, immediate);

        // Debug: Log all text fields AFTER updates
        if (debugLog)
        {
            Debug.Log($"[FirepowerDisplay] FINAL STATE: FinalDmg='{finalDamageText?.text}', FinalRcvd='{finalReceivedText?.text}'");
            Debug.Log($"[FirepowerDisplay] Gauge Difference: {firepowerA} - {firepowerB} = {gaugeDifference}");
        }
    }

    /// <summary>
    /// Resets display to zero (call at battle start).
    /// </summary>
    public void Reset()
    {
        // Stop all running number animation coroutines
        foreach (var kvp in runningNumberAnimations)
        {
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);
        }
        runningNumberAnimations.Clear();

        currentFirepowerA = 0;
        currentFirepowerB = 0;
        currentFinalDamage = 0;
        currentReceivedDamage = 0;
        currentGaugeDifference = 0f;

        // Reset dynamic gauge range to base value
        currentGaugeRange = gaugeBaseRange;

        SetTextDirect(firepowerTextA, 0, normalColor);
        SetTextDirect(firepowerTextB, 0, normalColor);
        SetTextDirect(finalDamageText, 0, neutralColor);
        SetTextDirect(finalReceivedText, 0, neutralColor);

        // Reset row sum displays
        UpdateRowSumText(rowSumInfantryA, 0, rowSumInfantryPrefix);
        UpdateRowSumText(rowSumVehicleA, 0, rowSumVehiclePrefix);
        UpdateRowSumText(rowSum3rdRowA, 0, rowSum3rdRowPrefix);
        UpdateRowSumText(rowSumInfantryB, 0, rowSumInfantryPrefix);
        UpdateRowSumText(rowSumVehicleB, 0, rowSumVehiclePrefix);
        UpdateRowSumText(rowSum3rdRowB, 0, rowSum3rdRowPrefix);

        if (backgroundA != null) backgroundA.color = new Color(1f, 1f, 1f, 0.1f);
        if (backgroundB != null) backgroundB.color = new Color(1f, 1f, 1f, 0.1f);

        // Reset gauge to center
        ResetGauge();
    }

    // ========================================
    // PRIVATE HELPERS
    // ========================================

    /// <summary>
    /// Updates a firepower text field.
    /// </summary>
    private void UpdateFirepowerText(
        TextMeshProUGUI textField,
        Image background,
        int newValue,
        ref int currentValue,
        bool immediate)
    {
        if (textField == null) return;

        int oldValue = currentValue;
        bool isPositive = newValue > 0;
        bool wasPositive = oldValue > 0;

        // CRITICAL: If value hasn't changed AND animation is already running, let it continue
        // This prevents double-refresh from killing an in-progress animation
        if (!immediate && oldValue == newValue && runningNumberAnimations.ContainsKey(textField))
        {
            return;
        }

        currentValue = newValue;

        // Determine color
        Color textColor = isPositive ? commanderDamageColor : normalColor;

        // Cancel any running LeanTween scale animations
        LeanTween.cancel(textField.gameObject);

        // Only set text directly if we're NOT going to animate
        // (animating will set it to oldValue first, then tick up)
        if (immediate || !animateNumbers || oldValue == newValue)
        {
            SetTextDirect(textField, newValue, textColor);
        }

        // Animate only if there's a visual difference AND animations are enabled
        if (!immediate && animateNumbers && oldValue != newValue)
        {
            AnimateNumber(textField, oldValue, newValue, textColor);
        }

        // Update background
        if (background != null)
        {
            background.color = isPositive
                ? new Color(commanderDamageColor.r, commanderDamageColor.g, commanderDamageColor.b, 0.3f)
                : new Color(1f, 1f, 1f, 0.1f);
        }

        // Pulse when transitioning to positive
        if (pulseOnPositive && isPositive && !wasPositive)
        {
            PulseText(textField);
        }
    }

    /// <summary>
    /// Updates the Final Damage text (what Player A deals).
    /// </summary>
    private void UpdateFinalDamageText(
        TextMeshProUGUI textField,
        int newValue,
        ref int currentValue,
        Color positiveColor,
        bool immediate)
    {
        if (textField == null) return;

        int oldValue = currentValue;
        bool wasPositive = oldValue > 0;

        // CRITICAL: If value hasn't changed AND animation is already running, let it continue
        if (!immediate && oldValue == newValue && runningNumberAnimations.ContainsKey(textField))
        {
            return;
        }

        currentValue = newValue;

        Color textColor = newValue > 0 ? positiveColor : neutralColor;

        // Cancel any running LeanTween scale animations
        LeanTween.cancel(textField.gameObject);

        // Only set text directly if we're NOT going to animate
        if (immediate || !animateNumbers || oldValue == newValue)
        {
            SetTextDirect(textField, newValue, textColor);
        }

        // Animate only if there's a visual difference AND animations are enabled
        if (!immediate && animateNumbers && oldValue != newValue)
        {
            AnimateNumber(textField, oldValue, newValue, textColor);
        }

        // Pulse when transitioning to positive
        if (pulseOnPositive && newValue > 0 && !wasPositive)
        {
            PulseText(textField);
        }
    }

    /// <summary>
    /// Updates the Final Received text (what Player A takes).
    /// </summary>
    private void UpdateFinalReceivedText(
        TextMeshProUGUI textField,
        int newValue,
        ref int currentValue,
        Color positiveColor,
        bool immediate)
    {
        if (textField == null) return;

        int oldValue = currentValue;
        bool wasPositive = oldValue > 0;

        // CRITICAL: If value hasn't changed AND animation is already running, let it continue
        if (!immediate && oldValue == newValue && runningNumberAnimations.ContainsKey(textField))
        {
            return;
        }

        currentValue = newValue;

        Color textColor = newValue > 0 ? positiveColor : neutralColor;

        // Cancel any running LeanTween scale animations
        LeanTween.cancel(textField.gameObject);

        // Only set text directly if we're NOT going to animate
        if (immediate || !animateNumbers || oldValue == newValue)
        {
            SetTextDirect(textField, newValue, textColor);
        }

        // Animate only if there's a visual difference AND animations are enabled
        if (!immediate && animateNumbers && oldValue != newValue)
        {
            AnimateNumber(textField, oldValue, newValue, textColor);
        }

        // Pulse when transitioning to positive (warning pulse for taking damage)
        if (pulseOnPositive && newValue > 0 && !wasPositive)
        {
            PulseText(textField);
        }
    }

    /// <summary>
    /// Sets text directly without animation.
    /// </summary>
    private void SetTextDirect(TextMeshProUGUI textField, int value, Color color)
    {
        if (textField == null) return;

        string sign = (value > 0 && showPlusSign) ? "+" : "";
        textField.text = $"{sign}{value}";
        textField.color = color;
    }

    /// <summary>
    /// Animates number from old to new value with a slot-machine tick effect.
    /// Note: Caller must set the correct final value BEFORE calling this method
    /// and must cancel any running animations.
    /// </summary>
    private void AnimateNumber(TextMeshProUGUI textField, int oldValue, int newValue, Color targetColor)
    {
        if (textField == null) return;
        if (animationDuration <= 0) return;

        // CRITICAL: Stop any previous animation coroutine for this text field
        if (runningNumberAnimations.TryGetValue(textField, out Coroutine existingCoroutine))
        {
            if (existingCoroutine != null)
            {
                StopCoroutine(existingCoroutine);
            }
            runningNumberAnimations.Remove(textField);
        }

        // Start from old value visually
        string startSign = (oldValue > 0 && showPlusSign) ? "+" : "";
        textField.text = $"{startSign}{oldValue}";

        // Use coroutine for tick-by-tick animation (slot machine style)
        Coroutine newCoroutine = StartCoroutine(TickNumberAnimation(textField, oldValue, newValue));
        runningNumberAnimations[textField] = newCoroutine;
    }

    /// <summary>
    /// Coroutine that ticks through numbers one at a time for slot machine effect.
    /// Uses faster animation when decreasing (e.g., round end reset to 0).
    /// </summary>
    private System.Collections.IEnumerator TickNumberAnimation(TextMeshProUGUI textField, int fromValue, int toValue)
    {
        if (textField == null) yield break;

        int current = fromValue;
        int direction = (toValue > fromValue) ? 1 : -1;
        int steps = Mathf.Abs(toValue - fromValue);
        bool isDecreasing = direction < 0;

        // Calculate delay per tick to fit within animationDuration
        float actualTickDelay = (steps > 0) ? Mathf.Max(tickDelay, animationDuration / steps) : tickDelay;

        // When DECREASING (especially large decreases like round end), go much faster
        int stepSize = 1;
        if (isDecreasing)
        {
            // Apply speed multiplier to tick delay
            actualTickDelay /= decreaseSpeedMultiplier;

            // For large decreases, skip multiple numbers per tick
            if (steps > skipThreshold)
            {
                stepSize = skipStepSize;
            }
        }

        while (current != toValue)
        {
            // Move by stepSize, but don't overshoot
            int stepsToMove = Mathf.Min(stepSize, Mathf.Abs(toValue - current));
            current += direction * stepsToMove;

            string sign = (current > 0 && showPlusSign) ? "+" : "";
            textField.text = $"{sign}{current}";

            // Small scale bump on each tick for extra satisfaction (skip when going fast)
            if (stepSize == 1)
            {
                RectTransform rect = textField.GetComponent<RectTransform>();
                if (rect != null)
                {
                    Vector3 originalScale = rect.localScale;
                    LeanTween.scale(rect, originalScale * 1.05f, actualTickDelay * 0.3f)
                        .setEaseOutQuad()
                        .setOnComplete(() => {
                            LeanTween.scale(rect, originalScale, actualTickDelay * 0.3f);
                        });
                }
            }

            yield return new WaitForSeconds(actualTickDelay);
        }

        // Ensure final value is set
        string finalSign = (toValue > 0 && showPlusSign) ? "+" : "";
        textField.text = $"{finalSign}{toValue}";
    }

    /// <summary>
    /// Pulse animation for emphasis.
    /// NOTE: Do NOT call LeanTween.cancel here - it would cancel value animations!
    /// </summary>
    private void PulseText(TextMeshProUGUI textField)
    {
        if (textField == null) return;

        RectTransform rect = textField.GetComponent<RectTransform>();
        if (rect == null) return;

        // IMPORTANT: Capture the actual current scale, not assume Vector3.one
        Vector3 originalScale = rect.localScale;

        // If scale is zero or near-zero, default to one
        if (originalScale.magnitude < 0.01f)
            originalScale = Vector3.one;

        // DO NOT cancel here - LeanTween.cancel cancels ALL tweens including value animations!
        // Just start the scale animation - LeanTween handles overlapping scale tweens gracefully

        LeanTween.scale(rect, originalScale * pulseScale, 0.1f)
            .setEaseOutQuad()
            .setOnComplete(() => {
                LeanTween.scale(rect, originalScale, 0.1f)
                    .setEaseInQuad();
            });
    }

    // ========================================
    // TUG-OF-WAR GAUGE METHODS
    // ========================================

    /// <summary>
    /// Updates the tug-of-war gauge based on the difference between firepowers.
    /// Positive difference = Player A advantage (moves right, deals commander damage)
    /// Negative difference = Player B advantage (moves left, deals commander damage to A)
    /// Zero = Tied, no commander damage to either
    ///
    /// DYNAMIC RANGE: The gauge range expands when the indicator gets close to the edge,
    /// ensuring it never gets stuck at the ends. Range only grows, never shrinks.
    /// </summary>
    private void UpdateTugOfWarGauge(int difference, bool immediate)
    {
        if (gaugeIndicator == null) return;

        float oldDifference = currentGaugeDifference;

        // CRITICAL: If value hasn't changed AND animation is already running, don't restart
        // This prevents double-refresh from killing the first animation
        if (!immediate && Mathf.RoundToInt(oldDifference) == difference && runningGaugeAnimation != null)
        {
            // Animation already running to this target, let it continue
            return;
        }

        currentGaugeDifference = difference;

        // Initialize range if not set (first call or after scene load)
        if (currentGaugeRange <= 0)
            currentGaugeRange = gaugeBaseRange;

        // DYNAMIC RANGE: Check if we need to expand the range
        // If the absolute difference exceeds the threshold, grow the range
        float absDifference = Mathf.Abs(difference);
        float thresholdValue = currentGaugeRange * gaugeExpandThreshold;

        if (absDifference > thresholdValue)
        {
            // Expand range so current difference is at threshold position
            // This ensures the indicator moves forward, not backwards
            float newRange = absDifference / gaugeExpandThreshold;

            // Round up to a nice number (nearest 50 or 100)
            newRange = Mathf.Ceil(newRange / 50f) * 50f;

            // Only expand, never shrink
            if (newRange > currentGaugeRange)
            {
                if (debugLog)
                    Debug.Log($"[FirepowerDisplay] Expanding gauge range: {currentGaugeRange} -> {newRange} (difference={difference})");
                currentGaugeRange = newRange;
            }
        }

        // Calculate the normalized position (-1 to 1) using dynamic range
        float normalizedPosition = Mathf.Clamp(difference / currentGaugeRange, -1f, 1f);

        // Calculate target X position (0 = center, positive = right, negative = left)
        float targetX = normalizedPosition * (gaugeTrackWidth / 2f);

        // Determine color based on advantage
        Color indicatorColor;
        if (difference > 0)
            indicatorColor = gaugeColorA; // Player A winning
        else if (difference < 0)
            indicatorColor = gaugeColorB; // Player B winning
        else
            indicatorColor = gaugeColorNeutral; // Tied

        // Cancel any running gauge animations (both LeanTween AND coroutines)
        LeanTween.cancel(gaugeIndicator.gameObject);
        if (runningGaugeAnimation != null)
        {
            StopCoroutine(runningGaugeAnimation);
            runningGaugeAnimation = null;
        }

        if (immediate || gaugeAnimationDuration <= 0)
        {
            // Set position immediately
            SetGaugePosition(targetX, indicatorColor, difference);
        }
        else
        {
            // Animate to new position - fills are updated during animation
            AnimateGauge(oldDifference, difference, targetX, indicatorColor);
            // DON'T update fills here - the tick animation handles it step by step
            return;
        }

        // Update fill images only when setting immediately (not animating)
        UpdateGaugeFills(normalizedPosition, indicatorColor);
    }

    /// <summary>
    /// Sets the gauge position immediately without animation.
    /// </summary>
    private void SetGaugePosition(float targetX, Color color, int difference)
    {
        if (gaugeIndicator == null) return;

        // Set position
        Vector2 anchoredPos = gaugeIndicator.anchoredPosition;
        anchoredPos.x = targetX;
        gaugeIndicator.anchoredPosition = anchoredPos;

        // Set indicator color (if it has an Image component)
        Image indicatorImage = gaugeIndicator.GetComponent<Image>();
        if (indicatorImage != null)
            indicatorImage.color = color;

        // Update difference text if present
        if (gaugeDifferenceText != null)
        {
            string sign = difference > 0 ? "+" : "";
            gaugeDifferenceText.text = $"{sign}{difference}";
            gaugeDifferenceText.color = color;
        }
    }

    /// <summary>
    /// Animates the gauge from old position to new position.
    /// Uses tick-style stepped animation when useTickAnimation is true.
    /// </summary>
    private void AnimateGauge(float oldDifference, int newDifference, float targetX, Color targetColor)
    {
        if (gaugeIndicator == null) return;

        // CRITICAL: Stop any previous gauge animation coroutine before starting new one
        // LeanTween.cancel only cancels LeanTween animations, NOT Unity coroutines!
        if (runningGaugeAnimation != null)
        {
            StopCoroutine(runningGaugeAnimation);
            runningGaugeAnimation = null;
        }

        if (useTickAnimation)
        {
            // Use coroutine-based tick animation for slot machine feel
            runningGaugeAnimation = StartCoroutine(TickGaugeAnimation(oldDifference, newDifference, targetX, targetColor));
        }
        else
        {
            // Use smooth LeanTween animation
            SmoothAnimateGauge(oldDifference, newDifference, targetX, targetColor);
        }
    }

    /// <summary>
    /// Coroutine that ticks the gauge step-by-step for slot machine effect.
    /// Uses faster animation when moving toward zero (e.g., round end reset).
    /// </summary>
    private System.Collections.IEnumerator TickGaugeAnimation(float oldDifference, int newDifference, float targetX, Color targetColor)
    {
        if (gaugeIndicator == null) yield break;

        int currentDiff = Mathf.RoundToInt(oldDifference);
        int targetDiff = newDifference;
        int direction = (targetDiff > currentDiff) ? 1 : -1;
        int totalSteps = Mathf.Abs(targetDiff - currentDiff);

        // Check if we're moving toward zero (round end reset)
        bool isMovingTowardZero = Mathf.Abs(targetDiff) < Mathf.Abs(currentDiff);

        // Get start color for interpolation
        Image indicatorImage = gaugeIndicator.GetComponent<Image>();
        Color startColor = indicatorImage != null ? indicatorImage.color : gaugeColorNeutral;

        // Calculate how many gauge units to move per tick
        int stepSize = Mathf.Max(1, gaugeStepSize);

        // Calculate tick interval - faster when moving toward zero
        float actualTickInterval = gaugeTickInterval;
        if (isMovingTowardZero)
        {
            actualTickInterval /= decreaseSpeedMultiplier;
            // Also increase step size for large resets
            if (totalSteps > skipThreshold)
            {
                stepSize = Mathf.Max(stepSize, skipStepSize);
            }
        }

        while (currentDiff != targetDiff)
        {
            // Move by step size, but don't overshoot
            int stepsToMove = Mathf.Min(stepSize, Mathf.Abs(targetDiff - currentDiff));
            currentDiff += direction * stepsToMove;

            // Calculate position for current difference
            float normalizedPos = Mathf.Clamp(currentDiff / currentGaugeRange, -1f, 1f);
            float currentX = normalizedPos * (gaugeTrackWidth / 2f);

            // Update position
            Vector2 anchoredPos = gaugeIndicator.anchoredPosition;
            anchoredPos.x = currentX;
            gaugeIndicator.anchoredPosition = anchoredPos;

            // Update difference text
            if (gaugeDifferenceText != null)
            {
                string sign = currentDiff > 0 ? "+" : "";
                gaugeDifferenceText.text = $"{sign}{currentDiff}";
            }

            // Interpolate color based on progress
            float progress = (totalSteps > 0) ? 1f - ((float)Mathf.Abs(targetDiff - currentDiff) / totalSteps) : 1f;
            Color currentColor = Color.Lerp(startColor, targetColor, progress);

            if (indicatorImage != null)
                indicatorImage.color = currentColor;
            if (gaugeDifferenceText != null)
                gaugeDifferenceText.color = currentColor;

            // Update gauge fills
            UpdateGaugeFills(normalizedPos, currentColor);

            // Small bounce on each tick for extra satisfaction (skip when going fast)
            if (stepSize <= gaugeStepSize)
            {
                Vector3 originalScale = gaugeIndicator.localScale;
                LeanTween.scale(gaugeIndicator, originalScale * 1.03f, actualTickInterval * 0.3f)
                    .setEaseOutQuad()
                    .setOnComplete(() => {
                        LeanTween.scale(gaugeIndicator, originalScale, actualTickInterval * 0.3f);
                    });
            }

            yield return new WaitForSeconds(actualTickInterval);
        }

        // Ensure final position and values are set
        Vector2 finalPos = gaugeIndicator.anchoredPosition;
        finalPos.x = targetX;
        gaugeIndicator.anchoredPosition = finalPos;

        if (gaugeDifferenceText != null)
        {
            string sign = newDifference > 0 ? "+" : "";
            gaugeDifferenceText.text = $"{sign}{newDifference}";
            gaugeDifferenceText.color = targetColor;
        }

        if (indicatorImage != null)
            indicatorImage.color = targetColor;

        // Final fill update
        float finalNormalized = Mathf.Clamp(newDifference / currentGaugeRange, -1f, 1f);
        UpdateGaugeFills(finalNormalized, targetColor);
    }

    /// <summary>
    /// Smooth animation for gauge (original behavior).
    /// </summary>
    private void SmoothAnimateGauge(float oldDifference, int newDifference, float targetX, Color targetColor)
    {
        if (gaugeIndicator == null) return;

        // Calculate starting X position
        float normalizedOld = Mathf.Clamp(oldDifference / currentGaugeRange, -1f, 1f);
        float startX = normalizedOld * (gaugeTrackWidth / 2f);

        // Animate position
        LeanTween.value(gaugeIndicator.gameObject, startX, targetX, gaugeAnimationDuration)
            .setEaseOutQuad()
            .setOnUpdate((float x) => {
                Vector2 anchoredPos = gaugeIndicator.anchoredPosition;
                anchoredPos.x = x;
                gaugeIndicator.anchoredPosition = anchoredPos;

                // Calculate current difference for text display
                float progress = Mathf.InverseLerp(startX, targetX, x);
                int displayDiff = Mathf.RoundToInt(Mathf.Lerp(oldDifference, newDifference, progress));

                // Update difference text during animation
                if (gaugeDifferenceText != null)
                {
                    string sign = displayDiff > 0 ? "+" : "";
                    gaugeDifferenceText.text = $"{sign}{displayDiff}";
                }

                // Update gauge fills during animation
                float normalizedPos = x / (gaugeTrackWidth / 2f);
                UpdateGaugeFills(normalizedPos, targetColor);
            })
            .setOnComplete(() => {
                // Ensure final position is set
                Vector2 anchoredPos = gaugeIndicator.anchoredPosition;
                anchoredPos.x = targetX;
                gaugeIndicator.anchoredPosition = anchoredPos;

                // Set final difference text
                if (gaugeDifferenceText != null)
                {
                    string sign = newDifference > 0 ? "+" : "";
                    gaugeDifferenceText.text = $"{sign}{newDifference}";
                }

                // Final fill update
                float finalNormalized = targetX / (gaugeTrackWidth / 2f);
                UpdateGaugeFills(finalNormalized, targetColor);
            });

        // Animate indicator color
        Image indicatorImage = gaugeIndicator.GetComponent<Image>();
        if (indicatorImage != null)
        {
            Color startColor = indicatorImage.color;
            LeanTween.value(gaugeIndicator.gameObject, 0f, 1f, gaugeAnimationDuration)
                .setOnUpdate((float t) => {
                    indicatorImage.color = Color.Lerp(startColor, targetColor, t);
                    if (gaugeDifferenceText != null)
                        gaugeDifferenceText.color = Color.Lerp(startColor, targetColor, t);
                });
        }
    }

    /// <summary>
    /// Updates the gauge fill images based on normalized position.
    /// </summary>
    private void UpdateGaugeFills(float normalizedPosition, Color advantageColor)
    {
        // Fill A shows Player A's advantage (fills from center to right)
        if (gaugeFillA != null)
        {
            if (normalizedPosition > 0)
            {
                gaugeFillA.fillAmount = normalizedPosition;
                gaugeFillA.color = gaugeColorA;
                gaugeFillA.enabled = true;
            }
            else
            {
                gaugeFillA.fillAmount = 0;
                gaugeFillA.enabled = false;
            }
        }

        // Fill B shows Player B's advantage (fills from center to left)
        if (gaugeFillB != null)
        {
            if (normalizedPosition < 0)
            {
                gaugeFillB.fillAmount = -normalizedPosition; // Make positive
                gaugeFillB.color = gaugeColorB;
                gaugeFillB.enabled = true;
            }
            else
            {
                gaugeFillB.fillAmount = 0;
                gaugeFillB.enabled = false;
            }
        }
    }

    /// <summary>
    /// Resets the gauge to center position.
    /// </summary>
    private void ResetGauge()
    {
        // Stop any running gauge animation coroutine
        if (runningGaugeAnimation != null)
        {
            StopCoroutine(runningGaugeAnimation);
            runningGaugeAnimation = null;
        }

        if (gaugeIndicator != null)
        {
            LeanTween.cancel(gaugeIndicator.gameObject);
            SetGaugePosition(0f, gaugeColorNeutral, 0);
        }

        if (gaugeFillA != null)
        {
            gaugeFillA.fillAmount = 0;
            gaugeFillA.enabled = false;
        }

        if (gaugeFillB != null)
        {
            gaugeFillB.fillAmount = 0;
            gaugeFillB.enabled = false;
        }
    }

    /// <summary>
    /// Calculates total ATK and HP for a player's army.
    /// Uses ability-aware ATK calculation (includes double ATK abilities).
    /// Also includes perk and doctrine ATK bonuses to match CombatResolver.
    /// </summary>
    private (int totalATK, int totalHP) CalculateTotalStats(Player player, Player enemy = null)
    {
        int totalATK = 0;
        int totalHP = 0;
        int unitCount = 0;

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

                        // Add Perk ATK bonuses (Stopping Power, Piercing, Explosives, Heads Up)
                        if (PerkSystem.GetTypeAtkBonus != null)
                        {
                            effectiveATK += PerkSystem.GetTypeAtkBonus(player, unit.Type);
                        }

                        // Add Doctrine ATK bonuses (Close Combat, Ranged Warrior, Iron Skin, Death from Above)
                        if (DoctrineSystem.GetTypeAtkBonus != null)
                        {
                            effectiveATK += DoctrineSystem.GetTypeAtkBonus(player, unit.Type);
                        }

                        // Combined Arms doctrine bonus (vehicles +ATK when infantry present)
                        if (DoctrineSystem.Instance != null)
                        {
                            effectiveATK += DoctrineSystem.Instance.GetCombinedArmsBonus(player, unit.Type);
                        }

                        totalATK += effectiveATK;
                        totalHP += unit.HP;
                        unitCount++;

                        if (debugLog)
                            Debug.Log($"[FirepowerDisplay] {player.Name} unit: {unit.Name} BaseATK={unit.ATK} EffectiveATK={effectiveATK} HP={unit.HP} (Row={rowType})");
                    }
                }
            }
        }

        if (debugLog)
            Debug.Log($"[FirepowerDisplay] {player?.Name ?? "NULL"} totals: {unitCount} units, ATK={totalATK}, HP={totalHP}");

        return (totalATK, totalHP);
    }

    /// <summary>
    /// Calculates attack sums for each row type.
    /// Returns (Infantry/Front ATK, Vehicle/Middle ATK, 3rdRow/Back ATK)
    /// Uses ability-aware ATK calculation (includes double ATK abilities).
    /// </summary>
    private (int infantry, int vehicle, int thirdRow) CalculateRowAttackSums(Player player, Player enemy = null)
    {
        int infantryATK = 0;  // Front row
        int vehicleATK = 0;   // Middle row
        int thirdRowATK = 0;  // Back row (Air or Ranged depending on map)

        if (player != null && player.Rows != null)
        {
            // Front row = Infantry
            if (player.Rows.TryGetValue(RowType.Front, out Row frontRow))
            {
                infantryATK = CalculateRowATKWithAbilities(player, enemy, frontRow, RowType.Front);
            }

            // Middle row = Vehicle
            if (player.Rows.TryGetValue(RowType.Middle, out Row middleRow))
            {
                vehicleATK = CalculateRowATKWithAbilities(player, enemy, middleRow, RowType.Middle);
            }

            // Back row = Air/Ranged (depends on map)
            if (player.Rows.TryGetValue(RowType.Back, out Row backRow))
            {
                thirdRowATK = CalculateRowATKWithAbilities(player, enemy, backRow, RowType.Back);
            }
        }

        return (infantryATK, vehicleATK, thirdRowATK);
    }

    /// <summary>
    /// Helper to calculate total ATK for a single row with ability modifiers.
    /// Includes perk and doctrine ATK bonuses to match CombatResolver.
    /// </summary>
    private int CalculateRowATKWithAbilities(Player player, Player enemy, Row row, RowType rowType)
    {
        if (row == null || row.Units == null) return 0;

        // Build count by name for this row (for 3-of-a-kind)
        var aliveCountByName = new Dictionary<string, int>();
        foreach (var u in row.Units)
        {
            if (u != null && u.IsAlive)
            {
                if (!aliveCountByName.ContainsKey(u.Name))
                    aliveCountByName[u.Name] = 0;
                aliveCountByName[u.Name]++;
            }
        }

        int totalATK = 0;
        foreach (var unit in row.Units)
        {
            if (unit != null && unit.IsAlive)
            {
                int atk = AbilitySystem.GetEffectiveAttackForBuyPhase(
                    unit, player, enemy, rowType, aliveCountByName);

                // Add Perk ATK bonuses (Stopping Power, Piercing, Explosives, Heads Up)
                if (PerkSystem.GetTypeAtkBonus != null)
                {
                    atk += PerkSystem.GetTypeAtkBonus(player, unit.Type);
                }

                // Add Doctrine ATK bonuses (Close Combat, Ranged Warrior, Iron Skin, Death from Above)
                if (DoctrineSystem.GetTypeAtkBonus != null)
                {
                    atk += DoctrineSystem.GetTypeAtkBonus(player, unit.Type);
                }

                // Combined Arms doctrine bonus (vehicles +ATK when infantry present)
                if (DoctrineSystem.Instance != null)
                {
                    atk += DoctrineSystem.Instance.GetCombinedArmsBonus(player, unit.Type);
                }

                totalATK += atk;
            }
        }

        return totalATK;
    }

    /// <summary>
    /// Updates a row sum text display with customizable prefix and color.
    /// </summary>
    private void UpdateRowSumText(TextMeshProUGUI textField, int value, string prefix)
    {
        if (textField == null) return;

        // If prefix is empty, just show the number; otherwise show "prefix: value"
        if (string.IsNullOrEmpty(prefix))
        {
            textField.text = value.ToString();
        }
        else
        {
            textField.text = $"{prefix}: {value}";
        }

        // Use the configurable row sum text color
        textField.color = rowSumTextColor;
    }
}

// ========================================
// UNITY SETUP GUIDE (UNIFIED HEALTH SYSTEM)
// ========================================
//
// === FIREPOWER TEXTS ===
// Create two TextMeshProUGUI elements:
// - "FirepowerValueA" for Player A (left side)
// - "FirepowerValueB" for Player B (right side)
// These now show: Total ATK (raw sum of all unit attack values)
// Gold when positive
//
// === FINAL DAMAGE TEXTS (DEPRECATED) ===
// With unified health system, these are no longer needed.
// Set hideFinalDamageTexts = true to disable them.
//
// === ROW ATTACK SUM DISPLAYS (NEW) ===
// For testing/debugging, shows ATK sum per row:
// - rowSumInfantryA/B: Sum of ATK in Infantry/Front row
// - rowSumVehicleA/B: Sum of ATK in Vehicle/Middle row
// - rowSum3rdRowA/B: Sum of ATK in Air/Ranged/Back row
//
// === TUG-OF-WAR GAUGE ===
// A horizontal gauge that shows the attack power difference at a glance.
// Shows who has more total attack power.
//
// Setup:
// 1. Create a parent GameObject "TugOfWarGauge"
// 2. Add an Image child as the "Track" (background bar)
//    - Set width to match gaugeTrackWidth (default 400)
//    - Set a neutral background color
// 3. Add an Image child as the "Indicator" (sliding marker)
//    - This will move left/right along the track
//    - Center anchor (pivot at 0.5, 0.5)
//    - Assign its RectTransform to gaugeIndicator
// 4. (Optional) Add two filled Images for visual fill:
//    - FillA: Anchored to center, fills RIGHT (set Fill Origin = Left)
//    - FillB: Anchored to center, fills LEFT (set Fill Origin = Right)
// 5. (Optional) Add TextMeshProUGUI on the indicator for difference number
//
// Behavior:
// - Center = TIED (no commander damage to either side!)
// - Moves RIGHT when Player A has advantage (green) - A deals damage!
// - Moves LEFT when Player B has advantage (red) - B deals damage!
// - DYNAMIC RANGE: Starts at gaugeBaseRange (default 50), expands when
//   indicator gets close to edge (at gaugeExpandThreshold, default 75%)
//
// === EXAMPLE ===
// Player A: Tank (22 ATK, 20 HP)
// Player B: Infantry (30 ATK, 20 HP)
//
// Firepower A: 22 - 20 = +2
// Firepower B: 30 - 20 = +10
//
// Player B has higher Firepower (10 > 2)
// Player B WINS and deals: 10 - 2 = 8 commander damage
// Player A deals: 0 (doesn't exceed enemy firepower)
//
// Gauge Difference: 2 - 10 = -8
// Indicator moves LEFT by 8 units (toward Player B's side)
// Shows Player B will deal 8 commander damage
//
