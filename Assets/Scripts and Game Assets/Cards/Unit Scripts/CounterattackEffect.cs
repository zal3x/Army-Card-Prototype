using UnityEngine;

/// <summary>
/// CounterattackEffect: Plays a sprite sequence animation when counterattack damage is dealt.
///
/// This component is self-contained and handles its own lifecycle:
/// - Spawns at the position where counterattack damage is dealt
/// - Plays the sprite sequence animation
/// - Destroys itself after animation completes
///
/// USAGE:
/// Call CounterattackEffect.SpawnAtPosition() to create and play the effect.
/// </summary>
public class CounterattackEffect : MonoBehaviour
{
    // ========================================
    // CONFIGURATION
    // ========================================

    [Header("Animation Settings")]
    [Tooltip("Sprite frames for the counterattack animation")]
    public Sprite[] animationFrames;

    [Tooltip("Frames per second for animation playback")]
    [Range(1, 60)]
    public int fps = 15;

    [Tooltip("If true, animation loops")]
    public bool loopAnimation = false;

    [Tooltip("Number of times to play the animation (0 = infinite if looping)")]
    public int playCount = 1;

    [Header("Visual Settings")]
    [Tooltip("Color tint for the effect")]
    public Color tintColor = Color.white;

    [Tooltip("Scale multiplier for the effect")]
    public float scale = 1.0f;

    [Tooltip("Sorting order for the effect")]
    public int sortingOrder = 100;

    [Tooltip("Sorting layer name")]
    public string sortingLayerName = "Default";

    // ========================================
    // PRIVATE STATE
    // ========================================

    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float frameTimer = 0f;
    private int currentPlayCount = 0;
    private bool isPlaying = false;

    // ========================================
    // STATIC FACTORY METHODS
    // ========================================

    /// <summary>
    /// Spawns a CounterattackEffect at the specified world position.
    /// Reads settings from CombatVisualSettings.
    /// </summary>
    /// <param name="worldPosition">World position to spawn the effect</param>
    /// <returns>The spawned effect, or null if settings not available</returns>
    public static CounterattackEffect SpawnAtPosition(Vector3 worldPosition)
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null) { Debug.LogWarning("[CounterattackEffect] CombatVisualSettings instance is null"); return null; }
        if (!settings.enableCounterattackEffect) { Debug.LogWarning("[CounterattackEffect] enableCounterattackEffect is FALSE in CombatVisualSettings"); return null; }

        // Check if effect has frames
        if (settings.counterattackEffectFrames == null || settings.counterattackEffectFrames.Length == 0)
        {
            Debug.LogWarning("[CounterattackEffect] No animation frames assigned in CombatVisualSettings — assign sprites to counterattackEffectFrames");
            return null;
        }
        Debug.Log($"[CounterattackEffect] Spawning at {worldPosition} with {settings.counterattackEffectFrames.Length} frames");

        // Create effect GameObject
        GameObject effectGO = new GameObject("CounterattackEffect");
        effectGO.transform.position = worldPosition + settings.counterattackEffectOffset;
        effectGO.transform.localScale = Vector3.one * settings.counterattackEffectScale;

        // Add SpriteRenderer
        SpriteRenderer sr = effectGO.AddComponent<SpriteRenderer>();
        sr.color = settings.counterattackEffectColor;
        sr.sortingLayerName = settings.counterattackEffectSortingLayer;
        sr.sortingOrder = settings.counterattackEffectSortingOrder;

        // Add and configure the effect component
        CounterattackEffect effect = effectGO.AddComponent<CounterattackEffect>();
        effect.animationFrames = settings.counterattackEffectFrames;
        effect.fps = settings.counterattackEffectFPS;
        effect.loopAnimation = false;
        effect.playCount = 1;
        effect.tintColor = settings.counterattackEffectColor;
        effect.scale = settings.counterattackEffectScale;
        effect.sortingOrder = settings.counterattackEffectSortingOrder;

        // Initialize and start playing
        effect.Initialize();

        return effect;
    }

    /// <summary>
    /// Spawns a CounterattackEffect on a unit's visual.
    /// </summary>
    /// <param name="unitVisual">The unit's visual GameObject</param>
    /// <returns>The spawned effect, or null if settings not available</returns>
    public static CounterattackEffect SpawnOnUnit(GameObject unitVisual)
    {
        if (unitVisual == null) return null;

        var settings = CombatVisualSettings.Instance;
        if (settings == null || !settings.enableCounterattackEffect) return null;

        // Check if effect has frames
        if (settings.counterattackEffectFrames == null || settings.counterattackEffectFrames.Length == 0)
        {
            Debug.LogWarning("[CounterattackEffect] No animation frames configured in CombatVisualSettings");
            return null;
        }

        // Create effect GameObject as child of unit
        GameObject effectGO = new GameObject("CounterattackEffect");
        effectGO.transform.SetParent(unitVisual.transform, false);
        effectGO.transform.localPosition = settings.counterattackEffectOffset;
        effectGO.transform.localScale = Vector3.one * settings.counterattackEffectScale;

        // Add SpriteRenderer
        SpriteRenderer sr = effectGO.AddComponent<SpriteRenderer>();
        sr.color = settings.counterattackEffectColor;

        // Get unit's sorting order and place effect above it
        SpriteRenderer unitSR = unitVisual.GetComponent<SpriteRenderer>();
        if (unitSR == null) unitSR = unitVisual.GetComponentInChildren<SpriteRenderer>();
        int baseSortingOrder = unitSR != null ? unitSR.sortingOrder : 0;
        sr.sortingOrder = baseSortingOrder + settings.counterattackEffectSortingOrder;
        sr.sortingLayerName = unitSR != null ? unitSR.sortingLayerName : "Default";

        // Add and configure the effect component
        CounterattackEffect effect = effectGO.AddComponent<CounterattackEffect>();
        effect.animationFrames = settings.counterattackEffectFrames;
        effect.fps = settings.counterattackEffectFPS;
        effect.loopAnimation = false;
        effect.playCount = 1;
        effect.tintColor = settings.counterattackEffectColor;
        effect.scale = settings.counterattackEffectScale;

        // Initialize and start playing
        effect.Initialize();

        return effect;
    }

    /// <summary>
    /// Spawns a CounterattackEffect with custom frames.
    /// </summary>
    public static CounterattackEffect SpawnAtPosition(Vector3 worldPosition, Sprite[] frames, int animFPS = 15)
    {
        if (frames == null || frames.Length == 0) return null;

        // Create effect GameObject
        GameObject effectGO = new GameObject("CounterattackEffect");
        effectGO.transform.position = worldPosition;

        // Add SpriteRenderer
        SpriteRenderer sr = effectGO.AddComponent<SpriteRenderer>();
        sr.color = Color.white;
        sr.sortingOrder = 100;

        // Add and configure the effect component
        CounterattackEffect effect = effectGO.AddComponent<CounterattackEffect>();
        effect.animationFrames = frames;
        effect.fps = animFPS;
        effect.loopAnimation = false;
        effect.playCount = 1;

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
                // Animation complete - destroy self
                Destroy(gameObject);
                return;
            }
        }

        // Update sprite
        if (spriteRenderer != null && currentFrame < animationFrames.Length)
        {
            spriteRenderer.sprite = animationFrames[currentFrame];
        }
    }

    // ========================================
    // PUBLIC API
    // ========================================

    /// <summary>
    /// Stops the animation and destroys the effect.
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
        Destroy(gameObject);
    }

    /// <summary>
    /// Returns true if the animation is currently playing.
    /// </summary>
    public bool IsPlaying => isPlaying;
}
