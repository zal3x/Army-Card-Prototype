using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// FloatingDamageNumber: Displays damage numbers that float upward and fade out.
/// 
/// FEATURES:
/// - Animates upward with smooth easing
/// - Fades out over time
/// - Color-coded by damage type (normal, critical, heal)
/// - Random horizontal spread for visual variety
/// - Auto-destroys when animation completes
/// 
/// SETUP:
/// 1. Create a UI Canvas set to "World Space" (not Screen Space)
/// 2. Create a TextMeshPro text object as a child
/// 3. Make it a prefab
/// 4. Assign to DamageNumberSpawner
/// 
/// USAGE:
/// DamageNumberSpawner.Spawn(25, unitPosition, DamageType.Normal);
/// </summary>
/// 
/// 

public enum DamageNumberType
{
    Normal, //white
    MarkedTarget, //orange is working but bigger isn't working
    ArmorReduced, //blue and working
    Counterattack, //purple counter text
    CommanderDamage, // gold and working
    AbilityBonus, //green x2 attack or some shit
    PreemptiveStrike
}

public class FloatingDamageNumber : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("How fast the number floats upward (units per second).")]
    public float riseSpeed = 2f;

    [Tooltip("How long before the number fades out completely (seconds).")]
    public float lifetime = 1.5f;

    [Tooltip("Random horizontal drift amount (left/right).")]
    public float horizontalSpread = 0.5f;

    [Header("Scale Animation")]
    [Tooltip("If true, number pops in with a scale animation.")]
    public bool useScalePop = true;

    [Tooltip("Duration of the scale pop (seconds).")]
    public float scalePopDuration = 0.2f;

    [Tooltip("Overshoot scale (1.2 = 120% before settling to 100%).")]
    public float scalePopAmount = 1.2f;

    // Private state
    private TextMeshProUGUI textMesh;
    private CanvasGroup canvasGroup;
    private Vector3 velocity;
    private float timer;

    private void Awake()
    {
        textMesh = GetComponentInChildren<TextMeshProUGUI>();
        canvasGroup = GetComponentInChildren<CanvasGroup>();

        // Add CanvasGroup if missing (for fade effect)
        if (canvasGroup == null && textMesh != null)
        {
            canvasGroup = textMesh.gameObject.AddComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Initializes and starts the damage number animation.
    /// Called by DamageNumberSpawner.
    /// </summary>
    public void Initialize(int amount, DamageNumberType type, string prefix = "", string suffix = "")
    {
        transform.localScale = Vector3.one;

        if (textMesh == null)
        {
            Debug.LogError("[FloatingDamageNumber] TextMeshProUGUI not found!");
            Destroy(gameObject);
            return;
        }

        // Build text with prefix/suffix
        string displayText = amount.ToString();
        if (!string.IsNullOrEmpty(prefix))
            displayText = prefix + " " + displayText;
        if (!string.IsNullOrEmpty(suffix))
            displayText = displayText + suffix;

        textMesh.text = displayText;
        textMesh.color = GetColorForType(type);
        textMesh.fontSize = GetFontSizeForType(type, amount);

        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        velocity = new Vector3(randomX, riseSpeed, 0f);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (useScalePop)
            StartCoroutine(ScalePopAnimation());

        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Initializes with custom text, color, and font size (no amount number).
    /// Used for ability activations like "x2 ATK".
    /// </summary>
    public void InitializeText(string text, Color color, float fontSizeMultiplier = 1f)
    {
        transform.localScale = Vector3.one;

        if (textMesh == null)
        {
            Debug.LogError("[FloatingDamageNumber] TextMeshProUGUI not found!");
            Destroy(gameObject);
            return;
        }

        textMesh.text = text;
        textMesh.color = color;
        textMesh.fontSize = 28f * fontSizeMultiplier; // Base size * multiplier

        float randomX = Random.Range(-horizontalSpread, horizontalSpread);
        velocity = new Vector3(randomX, riseSpeed, 0f);

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (useScalePop)
            StartCoroutine(ScalePopAnimation());

        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        timer += Time.deltaTime;

        // Move upward
        transform.position += velocity * Time.deltaTime;

        // Fade out over time
        if (canvasGroup != null)
        {
            float fadeProgress = timer / lifetime;
            canvasGroup.alpha = 1f - fadeProgress;
        }

        // Slow down horizontal drift over time
        velocity.x *= 0.95f;
    }

    /// <summary>
    /// Scale pop animation: grows to 120%, then shrinks to 100%.
    /// </summary>
    private IEnumerator ScalePopAnimation()
    {
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 0.5f; // Start small
        Vector3 popScale = Vector3.one * scalePopAmount;
        Vector3 finalScale = Vector3.one;

        // Pop to overshoot scale
        while (elapsed < scalePopDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (scalePopDuration * 0.5f);
            transform.localScale = Vector3.Lerp(startScale, popScale, EaseOutBack(t));
            yield return null;
        }

        // Settle to normal scale
        elapsed = 0f;
        while (elapsed < scalePopDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (scalePopDuration * 0.5f);
            transform.localScale = Vector3.Lerp(popScale, finalScale, t);
            yield return null;
        }

        transform.localScale = finalScale;
    }

    /// <summary>
    /// Easing function for bounce effect.
    /// </summary>
    private float EaseOutBack(float t)
    {
        float c1 = 1.70158f;
        float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    /// <summary>
    /// Gets color based on damage type.
    /// </summary>
    private Color GetColorForType(DamageNumberType type)
{
    return type switch
    {
        DamageNumberType.Normal => Color.white,
        DamageNumberType.MarkedTarget => new Color(1f, 0.6f, 0f),
        DamageNumberType.ArmorReduced => new Color(0.3f, 0.6f, 1f),
        DamageNumberType.Counterattack => new Color(0.8f, 0.3f, 1f),
        DamageNumberType.CommanderDamage => new Color(1f, 0.8f, 0f),
        DamageNumberType.AbilityBonus => new Color(0.3f, 1f, 0.3f),
        DamageNumberType.PreemptiveStrike => new Color(1f, 0.3f, 0.3f),
        _ => Color.white
    };
}

    /// <summary>
    /// Scales font size based on damage amount (bigger damage = bigger text).
    /// </summary>
   private float GetFontSizeForType(DamageNumberType type, int amount)
{
    float baseSize = 28f;
    if (amount >= 20) baseSize = 48f;
    else if (amount >= 10) baseSize = 36f;

    switch (type)
    {
        case DamageNumberType.MarkedTarget:
            return baseSize * 1.3f;
        case DamageNumberType.CommanderDamage:
            return baseSize * 1.8f; // MUCH bigger for commander damage (was 1.2f)
        case DamageNumberType.AbilityBonus:
            return baseSize * 1.1f;
        default:
            return baseSize;
    }
}
}



// ========================================
// SPAWNER COMPONENT
// ========================================

/// <summary>
/// DamageNumberSpawner: Static utility for spawning damage numbers.
/// 
/// SETUP:
/// 1. Create this as a component on a GameObject in your scene
/// 2. Assign the damage number prefab in Inspector
/// 3. Make sure prefab has FloatingDamageNumber component
/// 
/// USAGE FROM COMBAT:
/// DamageNumberSpawner.Spawn(25, unitTransform.position, DamageType.Normal);
/// </summary>


// ========================================
// USAGE EXAMPLES
// ========================================
//
// === IN COMBATPLAYBACK.CS ===
// In HandleEvent(), add:
//
// case CombatEventType.DamageAppliedToUnit:
//     if (e.TargetUnit != null && e.Amount > 0)
//     {
//         // Get unit's world position (you'll need to track this)
//         Vector3 unitPos = GetUnitPosition(e.TargetUnit);
//         DamageNumberSpawner.Spawn(e.Amount, unitPos, DamageType.Normal);
//     }
//     break;
//
// case CombatEventType.CommanderDamaged:
//     if (e.Amount > 0)
//     {
//         Vector3 commanderPos = GetCommanderPosition(e.TargetPlayer);
//         DamageNumberSpawner.Spawn(e.Amount, commanderPos, DamageType.CommanderDamage);
//     }
//     break;
//
// === GETTING UNIT POSITIONS ===
// Option 1: Track in FieldSpawner
// When spawning visuals, store: Dictionary<int, Transform> transformsByUnitId
//
// Option 2: Search by unitId (slower but simpler)
// GameObject unitVisual = GameObject.Find($"Unit_{unitId}");
// if (unitVisual != null) return unitVisual.transform.position;
//