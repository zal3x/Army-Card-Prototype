using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Image fillImage; // assign in prefab
    [SerializeField] private CanvasGroup canvasGroup; // optional - for fade in/out

    [Header("Animation")]
    [SerializeField] private float tweenSeconds = 0.20f;
    [SerializeField] private bool useGlobalSettings = true;

    private int maxHp = 1;
    private int currentHp = 1;
    private Coroutine anim;
    private Coroutine fadeRoutine;
    private Coroutine flashRoutine;
    private Color originalFillColor;
    private float visibilityTimer = 0f;

    // Public getter for maxHp (used by FieldSpawner to check if reinit needed)
    public int MaxHp => maxHp;

    // Get effective tween duration (global settings or local override)
    private float EffectiveTweenDuration
    {
        get
        {
            if (useGlobalSettings && CombatVisualSettings.Instance != null)
                return CombatVisualSettings.Instance.healthBarTweenDuration;
            return tweenSeconds;
        }
    }

    private void Awake()
    {
        if (fillImage != null)
            originalFillColor = fillImage.color;

        // Auto-find CanvasGroup if not assigned
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        // If still no CanvasGroup, try to add one
        if (canvasGroup == null)
        {
            // Try adding to parent Canvas
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                Debug.Log($"[WorldHealthBar] Auto-added CanvasGroup to {canvas.gameObject.name} for fade effects");
            }
            else
            {
                // Add to this GameObject as fallback
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                Debug.Log($"[WorldHealthBar] Auto-added CanvasGroup to {gameObject.name} for fade effects");
            }
        }
    }

    private void Update()
    {
        // Handle auto-hide after damage
        var settings = CombatVisualSettings.Instance;
        if (settings != null && !settings.alwaysShowHealthBars && visibilityTimer > 0f)
        {
            visibilityTimer -= Time.deltaTime;
            if (visibilityTimer <= 0f)
            {
                // Don't hide if alwaysShowDamagedHealth is enabled and unit is damaged
                if (settings.alwaysShowDamagedHealth && currentHp < maxHp)
                {
                    // Keep the bar visible for damaged units
                    return;
                }

                HideBar();
            }
        }
    }

    public void Init(int maxHp, int currentHp)
    {
        this.maxHp = Mathf.Max(1, maxHp);
        this.currentHp = Mathf.Clamp(currentHp, 0, this.maxHp);

        if (anim != null) StopCoroutine(anim);
        anim = null;

        SetFillInstant(this.currentHp);
        UpdateColor();

        // Only show health bar on spawn if unit is damaged or always-show mode is enabled
        var settings = CombatVisualSettings.Instance;
        if (settings != null && (settings.alwaysShowHealthBars || this.currentHp < this.maxHp))
        {
            // Show for 1 second, then fade (unless always-show mode)
            ShowBar(1.0f);
        }
        else
        {
            // Unit is at full health and not in always-show mode - hide the bar
            if (canvasGroup != null)
                canvasGroup.alpha = 0f;
        }
    }

    public void AnimateTo(int newCurrentHp)
    {
        newCurrentHp = Mathf.Clamp(newCurrentHp, 0, maxHp);
        if (newCurrentHp == currentHp) return;

        // Show bar when damage is taken
        ShowBar();

        // Flash effect on damage
        var settings = CombatVisualSettings.Instance;
        if (settings != null && settings.flashHealthBarOnDamage && newCurrentHp < currentHp)
        {
            FlashBar();
        }

        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(AnimateRoutine(currentHp, newCurrentHp));
        currentHp = newCurrentHp;
    }

    public void SetFillInstant(int hp)
    {
        currentHp = Mathf.Clamp(hp, 0, maxHp);
        float t = (float)currentHp / maxHp;
        if (fillImage != null) fillImage.fillAmount = t;
        UpdateColor();
    }

    private IEnumerator AnimateRoutine(int fromHp, int toHp)
    {
        float from = (float)fromHp / maxHp;
        float to = (float)toHp / maxHp;

        float duration = EffectiveTweenDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Clamp01(elapsed / duration);
            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Lerp(from, to, a);
                // Update color gradually as HP changes
                float currentPercent = fillImage.fillAmount;
                UpdateColor(currentPercent);
            }
            yield return null;
        }

        if (fillImage != null)
        {
            fillImage.fillAmount = to;
            UpdateColor(to);
        }
        anim = null;
    }

    /// <summary>
    /// Updates the health bar color based on current HP percentage.
    /// </summary>
    private void UpdateColor(float? hpPercentOverride = null)
    {
        if (fillImage == null) return;

        var settings = CombatVisualSettings.Instance;
        if (settings == null) return;

        float hpPercent = hpPercentOverride ?? ((float)currentHp / maxHp);
        Color targetColor = settings.GetHealthBarColor(hpPercent);

        // Only update if not currently flashing
        if (flashRoutine == null)
            fillImage.color = targetColor;
    }

    /// <summary>
    /// Shows the health bar (used when damage is taken or always-on mode).
    /// </summary>
    /// <param name="customDuration">Optional custom visibility duration (e.g., 1 second for spawn)</param>
    private void ShowBar(float? customDuration = null)
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null) return;

        if (canvasGroup != null)
        {
            if (fadeRoutine != null) StopCoroutine(fadeRoutine);
            canvasGroup.alpha = 1f;
        }

        // Start visibility timer if not always showing
        if (!settings.alwaysShowHealthBars)
        {
            visibilityTimer = customDuration ?? settings.healthBarVisibilityDuration;
        }
    }

    /// <summary>
    /// Hides the health bar with fade out (used when not in always-show mode).
    /// </summary>
    private void HideBar()
    {
        if (canvasGroup == null) return;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);
        fadeRoutine = StartCoroutine(FadeRoutine(canvasGroup.alpha, 0f, 0.3f));
    }

    /// <summary>
    /// Flashes the health bar white briefly when taking damage.
    /// </summary>
    private void FlashBar()
    {
        if (fillImage == null) return;

        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(from, to, t);
            yield return null;
        }
        canvasGroup.alpha = to;
        fadeRoutine = null;
    }

    private IEnumerator FlashRoutine()
    {
        if (fillImage == null) yield break;

        Color originalColor = fillImage.color;
        Color flashColor = Color.white;

        var settings = CombatVisualSettings.Instance;
        float flashDuration = settings != null ? settings.damageFlashDuration : 0.1f;

        // Flash to white
        float elapsed = 0f;
        while (elapsed < flashDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flashDuration * 0.5f);
            fillImage.color = Color.Lerp(originalColor, flashColor, t);
            yield return null;
        }

        // Flash back to original color
        elapsed = 0f;
        while (elapsed < flashDuration * 0.5f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (flashDuration * 0.5f);
            fillImage.color = Color.Lerp(flashColor, originalColor, t);
            yield return null;
        }

        // Restore proper color based on HP
        UpdateColor();
        flashRoutine = null;
    }
}
