using UnityEngine;

/// <summary>
/// MarkTargetEffect: Plays a sprite sequence animation overlay when a unit is marked.
///
/// This component is self-contained and handles its own lifecycle:
/// - Spawns as a child of the target unit
/// - Plays the sprite sequence animation
/// - Either persists as a visual indicator or destroys itself after animation
///
/// USAGE:
/// Call MarkTargetEffect.SpawnOnUnit() to create and play the effect on a unit.
/// </summary>
public class MarkTargetEffect : MonoBehaviour
{
    // ========================================
    // CONFIGURATION
    // ========================================

    [Header("Animation Settings")]
    [Tooltip("Sprite frames for the mark animation")]
    public Sprite[] animationFrames;

    [Tooltip("Frames per second for animation playback")]
    [Range(1, 60)]
    public int fps = 12;

    [Tooltip("If true, animation loops continuously after first play")]
    public bool loopAnimation = false;

    [Tooltip("Number of times to play the animation before stopping (0 = infinite if looping)")]
    public int playCount = 1;

    [Tooltip("If true, destroy the effect after animation completes (ignored if looping infinitely)")]
    public bool destroyOnComplete = false;

    [Tooltip("If true, effect persists as static indicator after animation (shows last frame)")]
    public bool persistAfterAnimation = true;

    [Header("Visual Settings")]
    [Tooltip("Color tint for the effect")]
    public Color tintColor = Color.white;

    [Tooltip("Scale multiplier for the effect")]
    public float scale = 1.0f;

    [Tooltip("Offset from unit center (in local space)")]
    public Vector3 localOffset = Vector3.zero;

    [Tooltip("Sorting order offset (relative to unit's sorting order)")]
    public int sortingOrderOffset = 10;

    // ========================================
    // PRIVATE STATE
    // ========================================

    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float frameTimer = 0f;
    private int currentPlayCount = 0;
    private bool isPlaying = false;
    private bool animationComplete = false;

    // ========================================
    // STATIC FACTORY METHOD
    // ========================================

    /// <summary>
    /// Spawns a MarkTargetEffect on the specified unit.
    /// Reads settings from CombatVisualSettings.
    /// </summary>
    /// <param name="unitVisual">The unit's visual GameObject to attach to</param>
    /// <returns>The spawned effect, or null if settings not available</returns>
    public static MarkTargetEffect SpawnOnUnit(GameObject unitVisual)
    {
        if (unitVisual == null) return null;

        var settings = CombatVisualSettings.Instance;
        if (settings == null) return null;

        // Check if mark effect is enabled and has frames
        if (settings.markTargetEffectFrames == null || settings.markTargetEffectFrames.Length == 0)
        {
            Debug.LogWarning("[MarkTargetEffect] No animation frames configured in CombatVisualSettings");
            return null;
        }

        // Create effect GameObject
        GameObject effectGO = new GameObject("MarkTargetEffect");
        effectGO.transform.SetParent(unitVisual.transform, false);
        effectGO.transform.localPosition = settings.markTargetEffectOffset;
        effectGO.transform.localScale = Vector3.one * settings.markTargetEffectScale;

        // Add SpriteRenderer
        SpriteRenderer sr = effectGO.AddComponent<SpriteRenderer>();
        sr.color = settings.markTargetEffectColor;

        // Get unit's sorting order and place effect above it
        SpriteRenderer unitSR = unitVisual.GetComponent<SpriteRenderer>();
        if (unitSR == null) unitSR = unitVisual.GetComponentInChildren<SpriteRenderer>();
        int baseSortingOrder = unitSR != null ? unitSR.sortingOrder : 0;
        sr.sortingOrder = baseSortingOrder + settings.markTargetEffectSortingOffset;
        sr.sortingLayerName = unitSR != null ? unitSR.sortingLayerName : "Default";

        // Add and configure the effect component
        MarkTargetEffect effect = effectGO.AddComponent<MarkTargetEffect>();
        effect.animationFrames = settings.markTargetEffectFrames;
        effect.fps = settings.markTargetEffectFPS;
        effect.loopAnimation = settings.markTargetEffectLoops;
        effect.playCount = settings.markTargetEffectPlayCount;
        effect.destroyOnComplete = !settings.markTargetEffectPersists;
        effect.persistAfterAnimation = settings.markTargetEffectPersists;
        effect.tintColor = settings.markTargetEffectColor;
        effect.scale = settings.markTargetEffectScale;
        effect.localOffset = settings.markTargetEffectOffset;
        effect.sortingOrderOffset = settings.markTargetEffectSortingOffset;

        // Initialize and start playing
        effect.Initialize();

        Debug.Log($"[MarkTargetEffect] Spawned on {unitVisual.name} with {effect.animationFrames.Length} frames at {effect.fps} FPS");

        return effect;
    }

    /// <summary>
    /// Spawns a MarkTargetEffect with custom frames (for testing or special cases).
    /// </summary>
    public static MarkTargetEffect SpawnOnUnit(GameObject unitVisual, Sprite[] frames, int animFPS = 12, bool loop = false)
    {
        if (unitVisual == null || frames == null || frames.Length == 0) return null;

        // Create effect GameObject
        GameObject effectGO = new GameObject("MarkTargetEffect");
        effectGO.transform.SetParent(unitVisual.transform, false);
        effectGO.transform.localPosition = Vector3.zero;

        // Add SpriteRenderer
        SpriteRenderer sr = effectGO.AddComponent<SpriteRenderer>();
        sr.color = Color.white;

        // Get unit's sorting order and place effect above it
        SpriteRenderer unitSR = unitVisual.GetComponent<SpriteRenderer>();
        if (unitSR == null) unitSR = unitVisual.GetComponentInChildren<SpriteRenderer>();
        int baseSortingOrder = unitSR != null ? unitSR.sortingOrder : 0;
        sr.sortingOrder = baseSortingOrder + 10;
        sr.sortingLayerName = unitSR != null ? unitSR.sortingLayerName : "Default";

        // Add and configure the effect component
        MarkTargetEffect effect = effectGO.AddComponent<MarkTargetEffect>();
        effect.animationFrames = frames;
        effect.fps = animFPS;
        effect.loopAnimation = loop;
        effect.playCount = loop ? 0 : 1;
        effect.destroyOnComplete = !loop;
        effect.persistAfterAnimation = !loop;

        effect.Initialize();

        return effect;
    }

    // ========================================
    // INITIALIZATION
    // ========================================

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
    }

    private void Initialize()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (animationFrames != null && animationFrames.Length > 0)
        {
            spriteRenderer.sprite = animationFrames[0];
            spriteRenderer.color = tintColor;
            currentFrame = 0;
            frameTimer = 0f;
            currentPlayCount = 0;
            isPlaying = true;
            animationComplete = false;
        }
    }

    // ========================================
    // ANIMATION UPDATE
    // ========================================

    private void Update()
    {
        if (!isPlaying || animationFrames == null || animationFrames.Length == 0)
            return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / fps;

        while (frameTimer >= frameDuration && isPlaying)
        {
            frameTimer -= frameDuration;
            AdvanceFrame();
        }
    }

    private void AdvanceFrame()
    {
        currentFrame++;

        // Check if we completed a loop
        if (currentFrame >= animationFrames.Length)
        {
            currentPlayCount++;

            // Check if we should continue playing
            bool shouldContinue = loopAnimation && (playCount == 0 || currentPlayCount < playCount);

            if (shouldContinue)
            {
                // Loop back to start
                currentFrame = 0;
            }
            else
            {
                // Animation complete
                OnAnimationComplete();
                return;
            }
        }

        // Update sprite
        if (spriteRenderer != null && currentFrame < animationFrames.Length)
        {
            spriteRenderer.sprite = animationFrames[currentFrame];
        }
    }

    private void OnAnimationComplete()
    {
        isPlaying = false;
        animationComplete = true;

        if (destroyOnComplete)
        {
            Destroy(gameObject);
        }
        else if (persistAfterAnimation)
        {
            // Show last frame as static indicator
            if (spriteRenderer != null && animationFrames.Length > 0)
            {
                spriteRenderer.sprite = animationFrames[animationFrames.Length - 1];
            }
        }
    }

    // ========================================
    // PUBLIC API
    // ========================================

    /// <summary>
    /// Restarts the animation from the beginning.
    /// </summary>
    public void Replay()
    {
        currentFrame = 0;
        frameTimer = 0f;
        currentPlayCount = 0;
        isPlaying = true;
        animationComplete = false;

        if (spriteRenderer != null && animationFrames != null && animationFrames.Length > 0)
        {
            spriteRenderer.sprite = animationFrames[0];
        }
    }

    /// <summary>
    /// Stops the animation and optionally destroys the effect.
    /// </summary>
    public void Stop(bool destroy = false)
    {
        isPlaying = false;

        if (destroy)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Returns true if the animation is currently playing.
    /// </summary>
    public bool IsPlaying => isPlaying;

    /// <summary>
    /// Returns true if the animation has completed at least once.
    /// </summary>
    public bool IsComplete => animationComplete;
}
