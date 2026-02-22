using System.Collections;
using UnityEngine;

/// <summary>
/// Handles visual effects for units (damage flash, shake, etc.)
/// Attach this to unit prefabs alongside WorldHealthBar.
/// </summary>
public class UnitVisualEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Settings")]
    [SerializeField] private bool useGlobalSettings = true;
    [SerializeField] private float localFlashDuration = 0.1f;
    [SerializeField] private float localShakeIntensity = 0.3f;

    private Color originalColor;
    private Vector3 originalPosition;
    private Coroutine flashRoutine;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        // Auto-find SpriteRenderer if not assigned
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            originalColor = spriteRenderer.color;

        originalPosition = transform.localPosition;
    }

    /// <summary>
    /// Triggers damage flash effect (unit flashes white briefly).
    /// Call this when unit takes damage.
    /// </summary>
    public void FlashDamage()
    {
        var settings = CombatVisualSettings.Instance;

        // Check if flash is enabled
        if (useGlobalSettings && settings != null && !settings.enableDamageFlash)
            return;

        if (spriteRenderer == null) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    /// <summary>
    /// Triggers damage shake effect (unit shakes briefly).
    /// Call this when unit takes damage.
    /// </summary>
    public void ShakeDamage()
    {
        var settings = CombatVisualSettings.Instance;

        // Check if shake is enabled
        if (useGlobalSettings && settings != null && !settings.enableDamageShake)
            return;

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    /// <summary>
    /// Triggers both flash and shake effects.
    /// Convenience method for damage feedback.
    /// </summary>
    public void PlayDamageEffects()
    {
        FlashDamage();
        ShakeDamage();
    }

    private IEnumerator FlashRoutine()
    {
        if (spriteRenderer == null) yield break;

        Color originalColor = spriteRenderer.color;
        Color flashColor = Color.white;

        var settings = CombatVisualSettings.Instance;
        float duration = (useGlobalSettings && settings != null)
            ? settings.damageFlashDuration
            : localFlashDuration;

        // Flash to white
        float elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            spriteRenderer.color = Color.Lerp(originalColor, flashColor, t);
            yield return null;
        }

        // Flash back to original
        elapsed = 0f;
        while (elapsed < duration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.5f);
            spriteRenderer.color = Color.Lerp(flashColor, originalColor, t);
            yield return null;
        }

        spriteRenderer.color = originalColor;
        flashRoutine = null;
    }

    private IEnumerator ShakeRoutine()
    {
        var settings = CombatVisualSettings.Instance;
        float intensity = (useGlobalSettings && settings != null)
            ? settings.damageShakeIntensity
            : localShakeIntensity;

        float duration = 0.2f; // Fixed shake duration
        float elapsed = 0f;

        Vector3 originalPos = transform.localPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / duration); // Decay over time

            // Random offset with decay
            float offsetX = Random.Range(-1f, 1f) * intensity * t * 0.1f;
            float offsetY = Random.Range(-1f, 1f) * intensity * t * 0.1f;

            transform.localPosition = originalPos + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }

        // Restore original position
        transform.localPosition = originalPos;
        shakeRoutine = null;
    }

    private void OnDestroy()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
    }
}
