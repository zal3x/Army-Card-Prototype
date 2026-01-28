using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// SpriteAnimationPlayer: Plays a sequence of sprites as an animation.
/// 
/// PERFECT FOR:
/// - Pixel art explosions
/// - Death effects
/// - Hit impacts
/// - Any frame-by-frame animation
/// 
/// USAGE:
/// 1. Create a GameObject with SpriteRenderer
/// 2. Add this component
/// 3. Assign your sprite frames in the Inspector (or via code)
/// 4. Call Play() or set playOnAwake = true
/// 
/// FEATURES:
/// - Configurable framerate
/// - Loop or play-once modes
/// - Auto-destroy when complete
/// - Callbacks for animation events
/// - Can be pooled (just call Play() to restart)
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimationPlayer : MonoBehaviour
{
    // ========================================
    // INSPECTOR CONFIGURATION
    // ========================================

    [Header("Sprite Frames")]
    [Tooltip("Array of sprites to play in sequence. Drag your explosion PNGs here.")]
    public Sprite[] frames;

    [Header("Playback Settings")]
    [Tooltip("Frames per second (12-15 is good for pixel art).")]
    [Range(1, 60)]
    public int framesPerSecond = 12;

    [Tooltip("If true, animation loops forever. If false, plays once.")]
    public bool loop = false;

    [Tooltip("If true, animation starts playing immediately on Awake.")]
    public bool playOnAwake = true;

    [Tooltip("If true, GameObject is destroyed when animation completes (only if loop=false).")]
    public bool destroyOnComplete = true;

    [Tooltip("If true, GameObject is deactivated instead of destroyed (for pooling).")]
    public bool deactivateInsteadOfDestroy = false;

    [Header("Sorting (2D)")]
    [Tooltip("Sorting layer for the explosion sprite.")]
    public string sortingLayerName = "VFX";

    [Tooltip("Sorting order within the layer.")]
    public int sortingOrder = 100;

    [Header("Scale Animation (Optional)")]
    [Tooltip("If true, scales up slightly during explosion then back down.")]
    public bool useScalePop = false;

    [Tooltip("Starting scale multiplier.")]
    public float scaleStart = 0.8f;

    [Tooltip("Peak scale multiplier (at middle of animation).")]
    public float scalePeak = 1.2f;

    [Tooltip("Ending scale multiplier.")]
    public float scaleEnd = 1.0f;

    [Header("Fade Out (Optional)")]
    [Tooltip("If true, fades out during the last portion of the animation.")]
    public bool fadeOutAtEnd = false;

    [Tooltip("When to start fading (0-1, where 1 = end of animation).")]
    [Range(0f, 1f)]
    public float fadeStartProgress = 0.7f;

    // ========================================
    // EVENTS / CALLBACKS
    // ========================================

    /// <summary>
    /// Fired when the animation completes (only if loop=false).
    /// </summary>
    public event Action OnAnimationComplete;

    /// <summary>
    /// Fired on each frame change. Passes the frame index.
    /// </summary>
    public event Action<int> OnFrameChanged;

    // ========================================
    // PRIVATE STATE
    // ========================================

    private SpriteRenderer spriteRenderer;
    private int currentFrame = 0;
    private float frameTimer = 0f;
    private bool isPlaying = false;
    private Vector3 originalScale;
    private Color originalColor;

    // ========================================
    // PROPERTIES
    // ========================================

    /// <summary>
    /// Is the animation currently playing?
    /// </summary>
    public bool IsPlaying => isPlaying;

    /// <summary>
    /// Current frame index (0 to frames.Length-1).
    /// </summary>
    public int CurrentFrame => currentFrame;

    /// <summary>
    /// Total number of frames in the animation.
    /// </summary>
    public int FrameCount => frames != null ? frames.Length : 0;

    /// <summary>
    /// Duration of the full animation in seconds.
    /// </summary>
    public float Duration => frames != null && framesPerSecond > 0 
        ? (float)frames.Length / framesPerSecond 
        : 0f;

    /// <summary>
    /// Current progress through the animation (0 to 1).
    /// </summary>
    public float Progress => frames != null && frames.Length > 0 
        ? (float)currentFrame / (frames.Length - 1) 
        : 0f;

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originalColor = spriteRenderer.color;

        // Apply sorting settings
        spriteRenderer.sortingLayerName = sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;

        // Set initial frame (invisible until Play is called, unless playOnAwake)
        if (frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
        }

        if (!playOnAwake)
        {
            spriteRenderer.enabled = false;
        }
    }

    private void Start()
    {
        // Capture original scale HERE (after external code may have changed it)
        // This allows CombatVFXSpawner to set scale before Play() uses it
        if (originalScale == Vector3.zero)
        {
            originalScale = transform.localScale;
        }

        if (playOnAwake)
        {
            Play();
        }
    }

    /// <summary>
    /// Call this if you set the scale externally and want scale pop to use that as the base.
    /// </summary>
    public void SetBaseScale(float scale)
    {
        originalScale = Vector3.one * scale;
        transform.localScale = originalScale;
    }

    private void Update()
    {
        if (!isPlaying || frames == null || frames.Length == 0)
            return;

        // Advance timer
        frameTimer += Time.deltaTime;

        float frameDuration = 1f / framesPerSecond;

        // Check if we should advance to next frame
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            AdvanceFrame();
        }

        // Apply optional effects
        ApplyScaleEffect();
        ApplyFadeEffect();
    }

    // ========================================
    // PUBLIC API
    // ========================================

    /// <summary>
    /// Starts playing the animation from the beginning.
    /// </summary>
    public void Play()
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning($"[SpriteAnimationPlayer] No frames assigned on {gameObject.name}");
            return;
        }

        // Reset state
        currentFrame = 0;
        frameTimer = 0f;
        isPlaying = true;

        // Reset visuals
        spriteRenderer.enabled = true;
        spriteRenderer.sprite = frames[0];
        spriteRenderer.color = originalColor;
        transform.localScale = useScalePop ? originalScale * scaleStart : originalScale;

        OnFrameChanged?.Invoke(0);
    }

    /// <summary>
    /// Stops the animation.
    /// </summary>
    public void Stop()
    {
        isPlaying = false;
    }

    /// <summary>
    /// Pauses the animation (can be resumed with Resume()).
    /// </summary>
    public void Pause()
    {
        isPlaying = false;
    }

    /// <summary>
    /// Resumes a paused animation.
    /// </summary>
    public void Resume()
    {
        if (frames != null && frames.Length > 0)
        {
            isPlaying = true;
        }
    }

    /// <summary>
    /// Sets the frames at runtime (useful for reusing the same player with different animations).
    /// </summary>
    public void SetFrames(Sprite[] newFrames)
    {
        frames = newFrames;
        currentFrame = 0;
        
        if (frames != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
        }
    }

    /// <summary>
    /// Jumps to a specific frame.
    /// </summary>
    public void GoToFrame(int frameIndex)
    {
        if (frames == null || frames.Length == 0) return;

        currentFrame = Mathf.Clamp(frameIndex, 0, frames.Length - 1);
        spriteRenderer.sprite = frames[currentFrame];
        OnFrameChanged?.Invoke(currentFrame);
    }

    // ========================================
    // PRIVATE HELPERS
    // ========================================

    private void AdvanceFrame()
    {
        currentFrame++;

        // Check if animation is complete
        if (currentFrame >= frames.Length)
        {
            if (loop)
            {
                // Loop back to start
                currentFrame = 0;
            }
            else
            {
                // Animation complete
                currentFrame = frames.Length - 1; // Stay on last frame
                isPlaying = false;

                OnAnimationComplete?.Invoke();

                if (destroyOnComplete)
                {
                    if (deactivateInsteadOfDestroy)
                    {
                        gameObject.SetActive(false);
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                }

                return;
            }
        }

        // Update sprite
        spriteRenderer.sprite = frames[currentFrame];
        OnFrameChanged?.Invoke(currentFrame);
    }

    private void ApplyScaleEffect()
    {
        if (!useScalePop) return;

        float progress = Progress;

        // Scale up to peak at 50%, then back down
        float scale;
        if (progress < 0.5f)
        {
            // First half: scale up
            float t = progress / 0.5f;
            scale = Mathf.Lerp(scaleStart, scalePeak, t);
        }
        else
        {
            // Second half: scale down
            float t = (progress - 0.5f) / 0.5f;
            scale = Mathf.Lerp(scalePeak, scaleEnd, t);
        }

        transform.localScale = originalScale * scale;
    }

    private void ApplyFadeEffect()
    {
        if (!fadeOutAtEnd) return;

        float progress = Progress;

        if (progress >= fadeStartProgress)
        {
            // Calculate fade amount
            float fadeProgress = (progress - fadeStartProgress) / (1f - fadeStartProgress);
            float alpha = Mathf.Lerp(1f, 0f, fadeProgress);

            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }
    }

    // ========================================
    // STATIC FACTORY METHODS
    // ========================================

    /// <summary>
    /// Spawns an explosion at a world position using the provided frames.
    /// Returns the SpriteAnimationPlayer instance (or null if failed).
    /// 
    /// USAGE:
    /// SpriteAnimationPlayer.SpawnExplosion(explosionFrames, transform.position);
    /// </summary>
    public static SpriteAnimationPlayer SpawnExplosion(
        Sprite[] frames,
        Vector3 worldPosition,
        int fps = 12,
        bool destroyOnComplete = true,
        string sortingLayer = "VFX",
        int sortingOrder = 100)
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("[SpriteAnimationPlayer] Cannot spawn explosion with no frames");
            return null;
        }

        // Create GameObject
        GameObject go = new GameObject("Explosion");
        go.transform.position = worldPosition;

        // Add SpriteRenderer
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = sortingOrder;

        // Add and configure SpriteAnimationPlayer
        SpriteAnimationPlayer player = go.AddComponent<SpriteAnimationPlayer>();
        player.frames = frames;
        player.framesPerSecond = fps;
        player.loop = false;
        player.playOnAwake = true;
        player.destroyOnComplete = destroyOnComplete;
        player.sortingLayerName = sortingLayer;
        player.sortingOrder = sortingOrder;

        return player;
    }

    /// <summary>
    /// Spawns an explosion using an existing prefab that has SpriteAnimationPlayer.
    /// </summary>
    public static SpriteAnimationPlayer SpawnFromPrefab(
        GameObject prefab,
        Vector3 worldPosition)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[SpriteAnimationPlayer] Cannot spawn from null prefab");
            return null;
        }

        GameObject go = Instantiate(prefab, worldPosition, Quaternion.identity);
        SpriteAnimationPlayer player = go.GetComponent<SpriteAnimationPlayer>();

        if (player == null)
        {
            Debug.LogWarning($"[SpriteAnimationPlayer] Prefab '{prefab.name}' has no SpriteAnimationPlayer component");
            Destroy(go);
            return null;
        }

        // Play() is called automatically if playOnAwake is true
        // If not, caller can call player.Play()

        return player;
    }
}

// ========================================
// USAGE EXAMPLES
// ========================================
//
// === INSPECTOR SETUP ===
// 1. Create empty GameObject at explosion position
// 2. Add SpriteRenderer component (automatic with RequireComponent)
// 3. Add SpriteAnimationPlayer component
// 4. Drag your explosion PNG frames into the "Frames" array
// 5. Set framesPerSecond (12-15 for pixel art)
// 6. Enable "Destroy On Complete" for one-shot effects
// 7. Enable "Play On Awake" to auto-play
//
// === CODE USAGE (SPAWN AT RUNTIME) ===
// 
// // Option 1: Using static factory method
// SpriteAnimationPlayer.SpawnExplosion(myExplosionFrames, unit.transform.position);
//
// // Option 2: Using prefab
// SpriteAnimationPlayer.SpawnFromPrefab(explosionPrefab, unit.transform.position);
//
// // Option 3: Manual setup
// GameObject go = new GameObject("Explosion");
// go.transform.position = deathPosition;
// SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
// SpriteAnimationPlayer player = go.AddComponent<SpriteAnimationPlayer>();
// player.frames = explosionSprites;
// player.framesPerSecond = 12;
// player.destroyOnComplete = true;
// player.Play();
//
// === CALLBACKS ===
// player.OnAnimationComplete += () => Debug.Log("Explosion finished!");
// player.OnFrameChanged += (frame) => Debug.Log($"Now showing frame {frame}");
//
// === POOLING (ADVANCED) ===
// Instead of destroying, deactivate and reuse:
// player.destroyOnComplete = false;
// player.deactivateInsteadOfDestroy = true;
// player.OnAnimationComplete += () => ReturnToPool(player.gameObject);