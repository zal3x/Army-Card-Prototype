using System.Collections;
using UnityEngine;

/// <summary>
/// Handles camera shake effects for impactful hits and deaths.
/// Attach this to the main camera or a camera manager object.
/// </summary>
public class CameraShake : MonoBehaviour
{
    private static CameraShake _instance;
    public static CameraShake Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CameraShake>();
            }
            return _instance;
        }
    }

    [Header("Settings")]
    [SerializeField] private bool useGlobalSettings = true;
    [SerializeField] private float localIntensity = 1.0f;

    private Transform cameraTransform;
    private Vector3 originalPosition;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[CameraShake] Multiple instances detected. Using first one.");
        }

        // Try to get camera transform
        cameraTransform = GetComponent<Camera>()?.transform;
        if (cameraTransform == null)
            cameraTransform = Camera.main?.transform;

        if (cameraTransform != null)
            originalPosition = cameraTransform.localPosition;
    }

    /// <summary>
    /// Triggers a camera shake effect.
    /// </summary>
    /// <param name="intensity">Shake intensity (0-1). If not provided, uses threshold-based intensity.</param>
    /// <param name="duration">Shake duration in seconds. Defaults to 0.2s.</param>
    public void Shake(float intensity = 0.5f, float duration = 0.2f)
    {
        var settings = CombatVisualSettings.Instance;

        // Check if screen shake is enabled
        if (useGlobalSettings && settings != null && !settings.enableScreenShake)
            return;

        if (cameraTransform == null)
        {
            Debug.LogWarning("[CameraShake] No camera transform found!");
            return;
        }

        // Apply global intensity multiplier
        float effectiveIntensity = intensity;
        if (useGlobalSettings && settings != null)
            effectiveIntensity *= settings.screenShakeIntensity;
        else
            effectiveIntensity *= localIntensity;

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(effectiveIntensity, duration));
    }

    /// <summary>
    /// Triggers a camera shake based on damage amount.
    /// Larger hits = stronger shake.
    /// </summary>
    public void ShakeOnDamage(int damageAmount)
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null) return;

        // Scale intensity based on damage threshold
        float normalizedIntensity = Mathf.Clamp01((float)damageAmount / settings.largeHitThreshold);

        // Only shake if damage is significant (> 20% of threshold)
        if (normalizedIntensity > 0.2f)
        {
            Shake(normalizedIntensity * 0.8f, 0.15f);
        }
    }

    /// <summary>
    /// Triggers a strong shake for unit death.
    /// </summary>
    public void ShakeOnDeath()
    {
        Shake(0.6f, 0.25f);
    }

    private IEnumerator ShakeRoutine(float intensity, float duration)
    {
        if (cameraTransform == null) yield break;

        Vector3 startPos = cameraTransform.localPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - (elapsed / duration); // Decay over time

            // Random offset with decay
            float offsetX = Random.Range(-1f, 1f) * intensity * t;
            float offsetY = Random.Range(-1f, 1f) * intensity * t;

            cameraTransform.localPosition = startPos + new Vector3(offsetX, offsetY, 0f);
            yield return null;
        }

        // Restore original position
        cameraTransform.localPosition = startPos;
        shakeRoutine = null;
    }

    private void OnDestroy()
    {
        if (shakeRoutine != null) StopCoroutine(shakeRoutine);

        // Restore position on destroy
        if (cameraTransform != null)
            cameraTransform.localPosition = originalPosition;
    }
}
