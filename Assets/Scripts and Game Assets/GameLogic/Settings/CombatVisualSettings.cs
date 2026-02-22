using UnityEngine;

/// <summary>
/// Global settings for combat visual effects (damage numbers, health bars, VFX, etc.)
/// Create via: Assets > Create > Combat Visual Settings
/// Place in Resources folder for auto-loading.
/// </summary>
[CreateAssetMenu(fileName = "CombatVisualSettings", menuName = "Settings/Combat Visual Settings")]
public class CombatVisualSettings : ScriptableObject
{
    // ========================================
    // HEALTH BARS
    // ========================================
    [Header("━━━━━━━━━━ HEALTH BARS ━━━━━━━━━━")]
    [Tooltip("How fast health bars animate when taking damage (seconds)")]
    [Range(0.05f, 1.0f)]
    public float healthBarTweenDuration = 0.15f;

    [Tooltip("Show health bars all the time (vs only when damaged)")]
    public bool alwaysShowHealthBars = true;

    [Tooltip("Keep health bars visible for units that have taken damage (currentHp < maxHp)")]
    public bool alwaysShowDamagedHealth = true;

    [Tooltip("How long health bars stay visible after damage (if not always shown)")]
    [Range(0.5f, 5.0f)]
    public float healthBarVisibilityDuration = 2.0f;

    [Tooltip("Flash health bar when taking damage")]
    public bool flashHealthBarOnDamage = true;

    [Tooltip("Color when health is full")]
    public Color healthBarColorFull = new Color(0.2f, 1f, 0.2f);

    [Tooltip("Color when health is low (< 30%)")]
    public Color healthBarColorLow = new Color(1f, 0.2f, 0.2f);

    // ========================================
    // UNIFIED COMMANDER HEALTH
    // ========================================
    [Header("━━━━━━━━━━ UNIFIED COMMANDER HEALTH ━━━━━━━━━━")]
    [Tooltip("Enable unified commander health system (unit HP adds to commander HP)")]
    public bool useUnifiedCommanderHealth = true;

    [Tooltip("Duration of health bar expansion animation when units are deployed")]
    [Range(0.1f, 1.0f)]
    public float commanderExpandAnimDuration = 0.4f;

    [Tooltip("Duration of health bar shrink animation at end of combat")]
    [Range(0.1f, 1.0f)]
    public float commanderShrinkAnimDuration = 0.3f;

    [Tooltip("Minimum width of commander health bar (base HP only)")]
    [Range(100f, 400f)]
    public float commanderBarMinWidth = 200f;

    [Tooltip("Maximum width of commander health bar (fully expanded)")]
    [Range(200f, 800f)]
    public float commanderBarMaxWidth = 400f;

    [Tooltip("Pulse effect when health bar expands")]
    public bool pulseOnExpand = true;

    [Tooltip("Scale of pulse effect on expansion")]
    [Range(1.0f, 1.3f)]
    public float expandPulseScale = 1.1f;

    // ========================================
    // DAMAGE NUMBERS
    // ========================================
    [Header("━━━━━━━━━━ DAMAGE NUMBERS ━━━━━━━━━━")]
    [Tooltip("How long damage numbers stay visible (seconds)")]
    [Range(0.5f, 3.0f)]
    public float damageNumberLifetime = 1.5f;

    [Tooltip("How fast damage numbers rise")]
    [Range(0.1f, 5.0f)]
    public float damageNumberRiseSpeed = 1.0f;

    [Tooltip("Scale multiplier for damage numbers (1.0 = normal size)")]
    [Range(0.5f, 2.0f)]
    public float damageNumberScale = 1.0f;

    [Tooltip("Pop animation strength (how much they scale up initially)")]
    [Range(1.0f, 2.0f)]
    public float damageNumberPopScale = 1.3f;

    [Tooltip("Enable critical hit effect (screen shake, larger numbers)")]
    public bool enableCriticalHitEffect = true;

    [Tooltip("Damage threshold for 'large hit' effect")]
    [Range(10, 100)]
    public int largeHitThreshold = 30;

    // ========================================
    // COMBAT PLAYBACK
    // ========================================
    [Header("━━━━━━━━━━ COMBAT PLAYBACK ━━━━━━━━━━")]
    [Tooltip("Overall speed multiplier (1 = normal, 2 = 2x speed, 0.5 = slow-mo)")]
    [Range(0.25f, 5.0f)]
    public float combatSpeedMultiplier = 1.0f;

    [Tooltip("Delay between most combat events (seconds)")]
    [Range(0.0f, 1.0f)]
    public float defaultEventDelay = 0.15f;

    [Tooltip("Extra delay after a unit is destroyed (seconds)")]
    [Range(0.0f, 2.0f)]
    public float unitDestroyDelay = 0.25f;

    [Tooltip("Extra delay after commander takes damage (seconds)")]
    [Range(0.0f, 2.0f)]
    public float commanderDamageDelay = 0.35f;

    [Tooltip("Skip animations and play combat instantly (for testing)")]
    public bool skipCombatAnimations = false;

    [Tooltip("Pause briefly between combat phases (pre-emptive, main, cleanup)")]
    public bool pauseBetweenPhases = false;

    [Tooltip("Duration of phase pause (seconds)")]
    [Range(0.1f, 2.0f)]
    public float phasePauseDuration = 0.5f;

    [Tooltip("Play opening volley: all units fire before damage is resolved")]
    public bool enableOpeningVolley = true;

    [Tooltip("Duration of opening volley (all units firing together before damage)")]
    [Range(0.5f, 5.0f)]
    public float openingVolleyDuration = 2.0f;

    [Tooltip("Continue firing animations during damage resolution")]
    public bool continueFiringDuringDamage = true;

    // ========================================
    // DEATH & VFX
    // ========================================
    [Header("━━━━━━━━━━ DEATH & VFX ━━━━━━━━━━")]
    [Tooltip("Scale of death explosion VFX (1.0 = normal)")]
    [Range(0.5f, 3.0f)]
    public float deathExplosionScale = 1.0f;

    [Tooltip("How long death explosion lasts (seconds)")]
    [Range(0.2f, 2.0f)]
    public float deathExplosionDuration = 0.5f;

    [Tooltip("Flash unit white when taking damage")]
    public bool enableDamageFlash = true;

    [Tooltip("How long damage flash lasts (seconds)")]
    [Range(0.05f, 0.5f)]
    public float damageFlashDuration = 0.1f;

    [Tooltip("Shake unit slightly when taking damage")]
    public bool enableDamageShake = true;

    [Tooltip("Intensity of damage shake")]
    [Range(0.0f, 1.0f)]
    public float damageShakeIntensity = 0.3f;

    // ========================================
    // CARD FLIGHT & BUY PHASE
    // ========================================
    [Header("━━━━━━━━━━ CARD FLIGHT ━━━━━━━━━━")]
    [Tooltip("Duration of card flight to battlefield (seconds)")]
    [Range(0.1f, 2.0f)]
    public float cardFlightDuration = 0.6f;

    [Tooltip("Height of card flight arc")]
    [Range(0.0f, 5.0f)]
    public float cardFlightArcHeight = 1.5f;

    [Tooltip("How much card rotates during flight (degrees)")]
    [Range(0, 360)]
    public float cardFlightRotation = 180f;

    [Tooltip("Duration of unit reveal animation after card lands")]
    [Range(0.1f, 1.0f)]
    public float unitRevealDuration = 0.3f;

    [Tooltip("Duration of card discard flight to graveyard")]
    [Range(0.2f, 1.5f)]
    public float discardFlightDuration = 0.4f;

    // ========================================
    // REARRANGEMENT
    // ========================================
    [Header("━━━━━━━━━━ REARRANGEMENT ━━━━━━━━━━")]
    [Tooltip("Duration of unit movement during rearrangement (seconds)")]
    [Range(0.1f, 1.0f)]
    public float rearrangementMoveDuration = 0.3f;

    [Tooltip("Duration of row swap animation (seconds)")]
    [Range(0.1f, 1.5f)]
    public float rowSwapDuration = 0.4f;

    [Tooltip("Highlight units that can be rearranged")]
    public bool highlightDraggableUnits = true;

    [Tooltip("Color of draggable unit highlight")]
    public Color draggableHighlightColor = new Color(1f, 1f, 0.5f, 0.5f);

    // ========================================
    // SPECIAL EFFECTS
    // ========================================
    [Header("━━━━━━━━━━ SPECIAL EFFECTS ━━━━━━━━━━")]
    [Tooltip("Enable screen shake on big hits")]
    public bool enableScreenShake = true;

    [Tooltip("Screen shake intensity multiplier")]
    [Range(0.1f, 2.0f)]
    public float screenShakeIntensity = 1.0f;

    [Tooltip("Enable slow-motion on unit death")]
    public bool enableDeathSlowMotion = false;

    [Tooltip("Slow-motion time scale (0.5 = half speed)")]
    [Range(0.1f, 1.0f)]
    public float deathSlowMotionScale = 0.5f;

    [Tooltip("How long slow-motion lasts (seconds)")]
    [Range(0.1f, 1.0f)]
    public float deathSlowMotionDuration = 0.3f;

    [Header("Death Burst")]
    [Tooltip("Maximum random delay offset for death explosions (creates staggered effect within a row)")]
    [Range(0.0f, 1.0f)]
    public float deathBurstRandomOffset = 0.25f;

    [Tooltip("Delay AFTER enemy (Player B) row is destroyed before it explodes. Enemy explosions are delayed while you continue taking damage. Set to 0 for immediate explosions.")]
    [Range(0.0f, 3.0f)]
    public float enemyDeathDelay = 1.0f;

    [Tooltip("(Legacy) Kept for backwards compatibility - use enemyDeathDelay instead.")]
    [HideInInspector]
    public float enemyExplosionHeadStart = 1.0f;

    [Tooltip("Visual indicator when unit is marked for targeting")]
    public bool showTargetMarkIndicator = true;

    [Tooltip("Color of target mark indicator")]
    public Color targetMarkColor = new Color(1f, 0.5f, 0f);

    // ========================================
    // MARK TARGET EFFECT (Sprite Animation)
    // ========================================
    [Header("━━━━━━━━━━ MARK TARGET EFFECT ━━━━━━━━━━")]
    [Tooltip("Enable sprite sequence animation when unit is marked")]
    public bool enableMarkTargetEffect = true;

    [Tooltip("Sprite frames for mark target animation (assign in Inspector)")]
    public Sprite[] markTargetEffectFrames;

    [Tooltip("Animation frames per second")]
    [Range(1, 60)]
    public int markTargetEffectFPS = 12;

    [Tooltip("Color tint for the mark effect")]
    public Color markTargetEffectColor = new Color(1f, 0.3f, 0.3f, 0.9f);

    [Tooltip("Scale multiplier for the effect")]
    [Range(0.1f, 5.0f)]
    public float markTargetEffectScale = 1.0f;

    [Tooltip("Offset from unit center (in local space)")]
    public Vector3 markTargetEffectOffset = Vector3.zero;

    [Tooltip("Sorting order offset (places effect above unit sprite)")]
    public int markTargetEffectSortingOffset = 10;

    [Tooltip("If true, animation loops while unit is marked")]
    public bool markTargetEffectLoops = false;

    [Tooltip("Number of times to play animation (0 = infinite if looping)")]
    [Range(0, 10)]
    public int markTargetEffectPlayCount = 1;

    [Tooltip("If true, effect persists as static indicator after animation completes")]
    public bool markTargetEffectPersists = true;

    // ========================================
    // COUNTERATTACK EFFECT (Sprite Animation)
    // ========================================
    [Header("━━━━━━━━━━ COUNTERATTACK EFFECT ━━━━━━━━━━")]
    [Tooltip("Enable sprite sequence animation when counterattack damage is dealt")]
    public bool enableCounterattackEffect = true;

    [Tooltip("Sprite frames for counterattack animation (assign in Inspector)")]
    public Sprite[] counterattackEffectFrames;

    [Tooltip("Animation frames per second")]
    [Range(1, 60)]
    public int counterattackEffectFPS = 15;

    [Tooltip("Color tint for the counterattack effect")]
    public Color counterattackEffectColor = new Color(1f, 0.8f, 0.2f, 1f);

    [Tooltip("Scale multiplier for the effect")]
    [Range(0.1f, 5.0f)]
    public float counterattackEffectScale = 1.0f;

    [Tooltip("Offset from damage position (in world space)")]
    public Vector3 counterattackEffectOffset = Vector3.zero;

    [Tooltip("Sorting order for the effect (high value = renders on top)")]
    public int counterattackEffectSortingOrder = 100;

    // ========================================
    // INTERLEAVED EXPLOSION SYSTEM
    // ========================================
    [Header("━━━━━━━━━━ INTERLEAVED EXPLOSIONS ━━━━━━━━━━")]
    [Tooltip("Enable firepower-based interleaved explosions (weaker player's row explodes first per row type)")]
    public bool useInterleavedExplosions = true;

    [Tooltip("Delay between same-row explosion batches (e.g., P2 Infantry finishes -> delay -> P1 Infantry starts)")]
    [Range(0.0f, 2.0f)]
    public float rowInterleavingDelay = 0.3f;

    [Tooltip("Delay between different row types (e.g., after all Infantry complete, before Vehicle starts)")]
    [Range(0.0f, 2.0f)]
    public float betweenRowDelay = 0.5f;

    [Tooltip("Delay before starting explosion phase after all damage is resolved")]
    [Range(0.0f, 2.0f)]
    public float preExplosionDelay = 0.5f;

    // ========================================
    // COMMANDER DAMAGE FINALE
    // ========================================
    [Header("━━━━━━━━━━ COMMANDER FINALE ━━━━━━━━━━")]
    [Tooltip("Enable dramatic finale where surviving units fire at enemy commander before showing damage")]
    public bool enableCommanderFinale = true;

    [Tooltip("Duration of surviving units firing at enemy commander (seconds)")]
    [Range(0.5f, 3.0f)]
    public float commanderFiringDuration = 1.5f;

    [Tooltip("Delay after firing before showing commander damage number")]
    [Range(0.0f, 1.0f)]
    public float postFiringDelay = 0.3f;

    [Tooltip("Intensity of camera shake during commander finale")]
    [Range(0.0f, 2.0f)]
    public float commanderFinaleShakeIntensity = 1.2f;

    // ========================================
    // ACCESSIBILITY
    // ========================================
    [Header("━━━━━━━━━━ ACCESSIBILITY ━━━━━━━━━━")]
    [Tooltip("Reduce particle effects and flashing")]
    public bool reduceVisualEffects = false;

    [Tooltip("Increase contrast for colorblind players")]
    public bool colorblindMode = false;

    [Tooltip("Larger UI elements and damage numbers")]
    public bool largeTextMode = false;

    [Tooltip("Text size multiplier when Large Text Mode is enabled")]
    [Range(1.0f, 2.0f)]
    public float largeTextMultiplier = 1.5f;

    // ========================================
    // SINGLETON ACCESS
    // ========================================
    private static CombatVisualSettings instance;
    public static CombatVisualSettings Instance
    {
        get
        {
            if (instance == null)
            {
                instance = Resources.Load<CombatVisualSettings>("CombatVisualSettings");
                if (instance == null)
                {
                    Debug.LogWarning("[CombatVisualSettings] No settings found in Resources folder. Using defaults.");
                    instance = CreateInstance<CombatVisualSettings>();
                }
            }
            return instance;
        }
    }

    // ========================================
    // HELPER METHODS
    // ========================================

    /// <summary>
    /// Gets the effective damage number scale (accounts for large text mode).
    /// </summary>
    public float GetDamageNumberScale()
    {
        float scale = damageNumberScale;
        if (largeTextMode)
            scale *= largeTextMultiplier;
        return scale;
    }
 
    /// <summary>
    /// Gets health bar color based on HP percentage.
    /// </summary>
    public Color GetHealthBarColor(float hpPercent)
    {
        if (hpPercent < 0.3f)
            return healthBarColorLow;
        return Color.Lerp(healthBarColorLow, healthBarColorFull, (hpPercent - 0.3f) / 0.7f);
    }
 
 
    /// <summary>
    /// Applies combat speed multiplier to a duration.
    /// </summary>
    public float ApplySpeedMultiplier(float duration)
    {
        if (skipCombatAnimations)
            return 0.001f; // Near-instant
        return duration / Mathf.Max(0.1f, combatSpeedMultiplier);
    }

    /// <summary>
    /// Calculates commander health bar width based on HP.
    /// </summary>
    /// <param name="currentMaxHP">Current max HP (base + units)</param>
    /// <param name="baseHP">Base commander HP (without units)</param>
    /// <param name="maxExpectedHP">Maximum expected HP (for scaling, e.g., 500)</param>
    public float GetCommanderBarWidth(int currentMaxHP, int baseHP, int maxExpectedHP = 500)
    {
        if (currentMaxHP <= baseHP)
            return commanderBarMinWidth;

        // Scale width proportionally between min and max based on HP
        float hpRatio = Mathf.Clamp01((float)(currentMaxHP - baseHP) / (maxExpectedHP - baseHP));
        return Mathf.Lerp(commanderBarMinWidth, commanderBarMaxWidth, hpRatio);
    }
}
