using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// CommanderDisplay: Shows commander portrait, health bar, and damage feedback.
/// 
/// FEATURES:
/// - Commander portrait image
/// - Health bar with smooth fill animation
/// - Health text (current/max)
/// - Damage flash effect
/// - Health bar color changes based on HP percentage
/// 
/// INTEGRATION:
/// - Listens for commander damage events
/// - Works with DamageNumberSpawner for floating text
/// - Updates from BattleTestRunner or CombatPlayback
/// 
/// SETUP:
/// 1. Create UI panel with Image for portrait
/// 2. Add child Image for health bar (with fill type = Filled)
/// 3. Add TextMeshProUGUI for health numbers
/// 4. Assign to BattleTestRunner for both players
/// </summary>
public class CommanderDisplay : MonoBehaviour
{
    // ========================================
    // INSPECTOR REFERENCES
    // ========================================

    [Header("Visual Elements")]
    [Tooltip("Commander portrait image")]
    public Image portraitImage;

    [Tooltip("Health bar fill image (should be Image with Fill type)")]
    public Image healthBarFill;

    [Tooltip("Optional: health bar background")]
    public Image healthBarBackground;

    [Tooltip("Health text (shows current/max HP)")]
    public TextMeshProUGUI healthText;

    [Header("Commander Data")]
    [Tooltip("Sprite to display for this commander")]
    public Sprite commanderSprite;

    [Tooltip("Commander name (optional, for tooltips/labels)")]
    public string commanderName = "Commander";

    [Header("Health Bar Colors")]
    [Tooltip("Health bar color when HP > 70%")]
    public Color healthyColor = new Color(0.2f, 0.8f, 0.3f); // Green

    [Tooltip("Health bar color when HP is 40-70%")]
    public Color damagedColor = new Color(0.9f, 0.7f, 0.2f); // Yellow

    [Tooltip("Health bar color when HP < 40%")]
    public Color criticalColor = new Color(0.9f, 0.2f, 0.2f); // Red

    [Header("Animation Settings")]
    [Tooltip("How fast health bar animates (seconds)")]
    public float healthBarAnimSpeed = 0.3f;

    [Header("Unified Health System")]
    [Tooltip("Reference to health bar container RectTransform for width animation")]
    public RectTransform healthBarContainer;

    [Tooltip("Flash color when taking damage")]
    public Color damageFlashColor = new Color(1f, 0.3f, 0.3f, 0.5f);

    [Tooltip("Duration of damage flash (seconds)")]
    public float damageFlashDuration = 0.2f;

    [Header("Shake on Damage")]
    [Tooltip("Enable shake animation when commander takes damage")]
    public bool shakeOnDamage = true;

    [Tooltip("Shake intensity (pixels)")]
    public float shakeAmount = 10f;

    [Tooltip("Shake duration (seconds)")]
    public float shakeDuration = 0.25f;

    // ========================================
    // PRIVATE STATE
    // ========================================

    private int currentHP;
    private int maxHP;
    private float targetFillAmount;
    private Vector3 originalPosition;
    private Image flashOverlay;

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    private void Awake()
    {
        // Create flash overlay if not assigned
        if (portraitImage != null && flashOverlay == null)
        {
            GameObject flashGO = new GameObject("DamageFlash");
            flashGO.transform.SetParent(portraitImage.transform, false);
            
            flashOverlay = flashGO.AddComponent<Image>();
            flashOverlay.color = new Color(1f, 1f, 1f, 0f);
            flashOverlay.raycastTarget = false;
            
            RectTransform rt = flashOverlay.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        originalPosition = transform.localPosition;
    }

    private void Start()
    {
        // Apply commander sprite if assigned
        if (portraitImage != null && commanderSprite != null)
        {
            portraitImage.sprite = commanderSprite;
        }
    }

    // ========================================
    // PUBLIC API
    // ========================================

    /// <summary>
    /// Initializes commander display with starting HP.
    /// Call this once at battle start.
    /// </summary>
    public void Initialize(int startingHP, Sprite portrait = null, string name = null)
    {
        maxHP = startingHP;
        currentHP = startingHP;
        targetFillAmount = 1f;

        if (portrait != null)
        {
            commanderSprite = portrait;
            if (portraitImage != null)
                portraitImage.sprite = portrait;
        }

        if (name != null)
            commanderName = name;

        UpdateHealthBar(immediate: true);
        UpdateHealthText();
    }

    /// <summary>
    /// Updates commander HP and animates health bar.
    /// Call this whenever commander takes damage or heals.
    /// </summary>
    public void SetHealth(int newHP, bool playDamageEffects = true)
    {
        int oldHP = currentHP;
        currentHP = Mathf.Clamp(newHP, 0, maxHP);

        // Calculate new fill amount
        targetFillAmount = maxHP > 0 ? (float)currentHP / maxHP : 0f;

        // Play damage effects if HP decreased
        if (playDamageEffects && currentHP < oldHP)
        {
            PlayDamageEffects();
        }

        UpdateHealthBar(immediate: false);
        UpdateHealthText();
    }

    /// <summary>
    /// Gets current HP (for external queries).
    /// </summary>
    public int GetCurrentHP() => currentHP;

    /// <summary>
    /// Gets max HP (for external queries).
    /// </summary>
    public int GetMaxHP() => maxHP;

    // ========================================
    // UNIFIED HEALTH SYSTEM METHODS
    // ========================================

    /// <summary>
    /// Expands max HP when a unit is deployed.
    /// Both currentHP and maxHP increase by the unit's HP.
    /// Health bar physically grows wider.
    /// </summary>
    public void ExpandMaxHP(int additionalHP, bool animate = true)
    {
        int oldMax = maxHP;
        int oldCurrent = currentHP;

        maxHP += additionalHP;
        currentHP += additionalHP;

        // Calculate new fill amount (should stay at same percentage or increase)
        targetFillAmount = maxHP > 0 ? (float)currentHP / maxHP : 1f;

        if (animate)
        {
            AnimateExpansion(oldMax, maxHP, oldCurrent, currentHP);
        }
        else
        {
            UpdateHealthBar(immediate: true);
            UpdateHealthText();
            UpdateBarWidth(maxHP, immediate: true);
        }
    }

    /// <summary>
    /// Sets max HP directly (for end-of-combat shrink).
    /// CurrentHP is clamped to not exceed new max.
    /// </summary>
    public void SetMaxHP(int newMaxHP, bool animate = true)
    {
        int oldMax = maxHP;
        maxHP = Mathf.Max(1, newMaxHP);
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        targetFillAmount = maxHP > 0 ? (float)currentHP / maxHP : 0f;

        if (animate)
        {
            AnimateShrink(oldMax, maxHP);
        }
        else
        {
            UpdateHealthBar(immediate: true);
            UpdateHealthText();
            UpdateBarWidth(maxHP, immediate: true);
        }
    }

    /// <summary>
    /// Full state sync - sets both current and max HP.
    /// Used after combat to sync display with actual player state.
    /// </summary>
    public void SetHealthState(int newCurrent, int newMax, bool animate = true)
    {
        int oldMax = maxHP;
        int oldCurrent = currentHP;

        maxHP = Mathf.Max(1, newMax);
        currentHP = Mathf.Clamp(newCurrent, 0, maxHP);

        targetFillAmount = maxHP > 0 ? (float)currentHP / maxHP : 0f;

        if (animate)
        {
            // Determine if we're shrinking or expanding
            if (newMax < oldMax)
            {
                AnimateShrink(oldMax, maxHP);
            }
            else if (newMax > oldMax)
            {
                AnimateExpansion(oldMax, maxHP, oldCurrent, currentHP);
            }
            else
            {
                // Same max, just update fill
                UpdateHealthBar(immediate: false);
                UpdateHealthText();
            }

            // Also animate HP change if it's damage
            if (newCurrent < oldCurrent)
            {
                PlayDamageEffects();
            }
        }
        else
        {
            UpdateHealthBar(immediate: true);
            UpdateHealthText();
            UpdateBarWidth(maxHP, immediate: true);
        }
    }

    /// <summary>
    /// Animates health bar expansion when units are deployed.
    /// </summary>
    private void AnimateExpansion(int oldMax, int newMax, int oldCurrent, int newCurrent)
    {
        var settings = CombatVisualSettings.Instance;
        float duration = settings != null ? settings.commanderExpandAnimDuration : 0.4f;

        // Animate bar width
        UpdateBarWidth(newMax, immediate: false, customDuration: duration);

        // Animate fill amount
        LeanTween.cancel(healthBarFill.gameObject);
        LeanTween.value(healthBarFill.gameObject, healthBarFill.fillAmount, targetFillAmount, duration)
            .setEaseOutQuad()
            .setOnUpdate((float val) => {
                if (healthBarFill != null)
                    healthBarFill.fillAmount = val;
            });

        // Update text
        UpdateHealthText();

        // Optional pulse effect
        if (settings != null && settings.pulseOnExpand && healthBarContainer != null)
        {
            Vector3 originalScale = healthBarContainer.localScale;
            float pulseScale = settings.expandPulseScale;

            LeanTween.scale(healthBarContainer, originalScale * pulseScale, duration * 0.3f)
                .setEaseOutQuad()
                .setOnComplete(() => {
                    LeanTween.scale(healthBarContainer, originalScale, duration * 0.3f)
                        .setEaseInQuad();
                });
        }
    }

    /// <summary>
    /// Animates health bar shrinking at end of combat.
    /// </summary>
    private void AnimateShrink(int oldMax, int newMax)
    {
        var settings = CombatVisualSettings.Instance;
        float duration = settings != null ? settings.commanderShrinkAnimDuration : 0.3f;

        // Animate bar width
        UpdateBarWidth(newMax, immediate: false, customDuration: duration);

        // Animate fill amount
        LeanTween.cancel(healthBarFill.gameObject);
        LeanTween.value(healthBarFill.gameObject, healthBarFill.fillAmount, targetFillAmount, duration)
            .setEaseOutQuad()
            .setOnUpdate((float val) => {
                if (healthBarFill != null)
                    healthBarFill.fillAmount = val;
            });

        // Update text and color
        UpdateHealthText();

        // Update color based on new HP percentage
        float hpPercent = targetFillAmount;
        Color newColor;
        if (hpPercent > 0.7f)
            newColor = healthyColor;
        else if (hpPercent > 0.4f)
            newColor = damagedColor;
        else
            newColor = criticalColor;
        healthBarFill.color = newColor;
    }

    /// <summary>
    /// Updates health bar container width based on current max HP.
    /// </summary>
    private void UpdateBarWidth(int forMaxHP, bool immediate, float customDuration = -1f)
    {
        if (healthBarContainer == null) return;

        var settings = CombatVisualSettings.Instance;
        if (settings == null || !settings.useUnifiedCommanderHealth) return;

        // Calculate target width (we use 20 as base HP reference, adjust as needed)
        float targetWidth = settings.GetCommanderBarWidth(forMaxHP, 20);

        if (immediate)
        {
            Vector2 size = healthBarContainer.sizeDelta;
            size.x = targetWidth;
            healthBarContainer.sizeDelta = size;
        }
        else
        {
            float duration = customDuration > 0 ? customDuration : settings.commanderExpandAnimDuration;
            float startWidth = healthBarContainer.sizeDelta.x;

            LeanTween.cancel(healthBarContainer.gameObject);
            LeanTween.value(healthBarContainer.gameObject, startWidth, targetWidth, duration)
                .setEaseOutQuad()
                .setOnUpdate((float val) => {
                    if (healthBarContainer != null)
                    {
                        Vector2 size = healthBarContainer.sizeDelta;
                        size.x = val;
                        healthBarContainer.sizeDelta = size;
                    }
                });
        }
    }

    /// <summary>
    /// Returns world position for spawning damage numbers.
    /// Spawns BELOW the portrait with proper Z-depth to appear in front.
    /// </summary>
    public Vector3 GetDamageNumberSpawnPosition()
    {
        if (portraitImage != null)
        {
            // Get the portrait's world position
            Vector3 basePos = portraitImage.transform.position;
            
            // Offset DOWNWARD so number appears below commander portrait
            // (commanders are at top of screen, so down keeps it visible)
            basePos.y -= 1.5f;
            
            // CRITICAL: Move forward in Z so it renders in front of UI
            // Negative Z = closer to camera in world space
            basePos.z = -2f;
            
            return basePos;
        }
        
        return transform.position;
    }

    // ========================================
    // PRIVATE HELPERS
    // ========================================

    /// <summary>
    /// Updates health bar fill amount and color.
    /// </summary>
    private void UpdateHealthBar(bool immediate)
    {
        if (healthBarFill == null) return;

        // Update color based on HP percentage
        float hpPercent = targetFillAmount;
        Color newColor;

        if (hpPercent > 0.7f)
            newColor = healthyColor;
        else if (hpPercent > 0.4f)
            newColor = damagedColor;
        else
            newColor = criticalColor;

        healthBarFill.color = newColor;

        // Animate fill amount
        if (immediate)
        {
            healthBarFill.fillAmount = targetFillAmount;
        }
        else
        {
            LeanTween.cancel(healthBarFill.gameObject);
            LeanTween.value(healthBarFill.gameObject, healthBarFill.fillAmount, targetFillAmount, healthBarAnimSpeed)
                .setEaseOutQuad()
                .setOnUpdate((float val) => {
                    if (healthBarFill != null)
                        healthBarFill.fillAmount = val;
                });
        }
    }

    /// <summary>
    /// Updates health text display.
    /// </summary>
    private void UpdateHealthText()
    {
        if (healthText == null) return;

        healthText.text = $"{currentHP} / {maxHP}";

        // Optional: color text based on HP
        float hpPercent = maxHP > 0 ? (float)currentHP / maxHP : 0f;
        if (hpPercent > 0.7f)
            healthText.color = Color.white;
        else if (hpPercent > 0.4f)
            healthText.color = new Color(1f, 0.9f, 0.5f); // Light yellow
        else
            healthText.color = new Color(1f, 0.5f, 0.5f); // Light red
    }

    /// <summary>
    /// Plays damage visual effects (flash + shake).
    /// </summary>
    private void PlayDamageEffects()
    {
        PlayDamageFlash();

        if (shakeOnDamage)
            PlayShakeAnimation();
    }

    /// <summary>
    /// Flash overlay animation when taking damage.
    /// </summary>
    private void PlayDamageFlash()
    {
        if (flashOverlay == null) return;

        LeanTween.cancel(flashOverlay.gameObject);

        // Flash from damage color to transparent
        flashOverlay.color = damageFlashColor;
        
        LeanTween.alpha(flashOverlay.rectTransform, 0f, damageFlashDuration)
            .setEaseInQuad();
    }

    /// <summary>
    /// Shake animation when taking damage.
    /// </summary>
    private void PlayShakeAnimation()
    {
        LeanTween.cancel(gameObject);

        // Random shake direction
        Vector3 shakeOffset = new Vector3(
            Random.Range(-shakeAmount, shakeAmount),
            Random.Range(-shakeAmount, shakeAmount),
            0f
        );

        transform.localPosition = originalPosition + shakeOffset;

        // Return to original position
        LeanTween.moveLocal(gameObject, originalPosition, shakeDuration)
            .setEaseOutElastic();
    }
}

// ========================================
// USAGE EXAMPLES
// ========================================
//
// === SETUP IN INSPECTOR ===
// 1. Create UI panel for commander (portrait + health bar)
// 2. Add CommanderDisplay component
// 3. Assign portrait image, health bar fill, health text
// 4. Set commander sprite and colors
// 5. Drag into BattleTestRunner's inspector fields
//
// === INITIALIZATION (BattleTestRunner.Start) ===
// commanderDisplayA.Initialize(commanderStartHP, commanderSpriteA, "Player A");
// commanderDisplayB.Initialize(commanderStartHP, commanderSpriteB, "Player B");
//
// === UPDATE HP (after combat) ===
// commanderDisplayA.SetHealth(playerA.CommanderHP, playDamageEffects: true);
//
// === SPAWN DAMAGE NUMBERS ===
// Vector3 spawnPos = commanderDisplayA.GetDamageNumberSpawnPosition();
// DamageNumberSpawner.Spawn(damageAmount, spawnPos, DamageNumberType.CommanderDamage);
//