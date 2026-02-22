using UnityEngine;
using System;

/// <summary>
/// UnitAnimator: Handles all sprite animations for a unit (idle, attack, etc.)
///
/// FEATURES:
/// - Unique idle animation per unit (loops continuously)
/// - Unique attack/combat animation per unit (plays once, returns to idle)
/// - State machine for clean transitions
/// - Configurable per-prefab (each unit type can have different animations)
///
/// SETUP:
/// 1. Add this component to your unit prefab
/// 2. Assign idle frames (will loop after spawn)
/// 3. Assign attack frames (plays when unit attacks, then returns to idle)
/// 4. Tweak FPS settings as needed
///
/// USAGE:
/// - Idle plays automatically on Start
/// - Call PlayAttack() when unit attacks (from CombatPlayback or UnitAttackEffects)
/// - Call PauseIdle() / ResumeIdle() for combat phases if desired
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class UnitAnimator : MonoBehaviour
{
    // ========================================
    // ANIMATION STATE
    // ========================================

    public enum AnimState
    {
        Idle,
        Attacking,
        Custom,     // For future expansion (death, hit reaction, etc.)
        Paused
    }

    // ========================================
    // INSPECTOR CONFIGURATION
    // ========================================

    [Header("Idle Animation")]
    [Tooltip("Sprite frames for idle animation (loops continuously)")]
    public Sprite[] idleFrames;

    [Tooltip("Frames per second for idle animation (8-12 is good for pixel art)")]
    [Range(1, 30)]
    public int idleFPS = 8;

    [Tooltip("If true, idle animation starts playing on Start()")]
    public bool playIdleOnStart = true;

    [Header("Attack Animation")]
    [Tooltip("Sprite frames for attack animation (plays once per attack)")]
    public Sprite[] attackFrames;

    [Tooltip("Frames per second for attack animation (faster = snappier attacks)")]
    [Range(1, 60)]
    public int attackFPS = 12;

    [Tooltip("If true, attack animation loops until manually stopped")]
    public bool loopAttack = false;

    [Header("Optional: Custom Animations")]
    [Tooltip("Additional animation that can be triggered via PlayCustom()")]
    public Sprite[] customFrames;
    public int customFPS = 10;
    public bool loopCustom = false;

    [Header("Fallback")]
    [Tooltip("Static sprite to show if no animation frames are assigned")]
    public Sprite fallbackSprite;

    // ========================================
    // EVENTS
    // ========================================

    /// <summary>Fired when attack animation completes (if not looping)</summary>
    public event Action OnAttackComplete;

    /// <summary>Fired when custom animation completes (if not looping)</summary>
    public event Action OnCustomComplete;

    /// <summary>Fired on each frame change (passes frame index)</summary>
    public event Action<int> OnFrameChanged;

    // ========================================
    // PRIVATE STATE
    // ========================================

    private SpriteRenderer spriteRenderer;
    private AnimState currentState = AnimState.Idle;
    private AnimState stateBeforePause = AnimState.Idle;

    // Current animation data
    private Sprite[] currentFrames;
    private int currentFPS;
    private bool currentLoop;
    private Action currentCompleteCallback;

    // Playback state
    private int currentFrame = 0;
    private float frameTimer = 0f;
    private bool isPlaying = false;

    // ========================================
    // PROPERTIES
    // ========================================

    public AnimState CurrentState => currentState;
    public bool IsPlaying => isPlaying;
    public int CurrentFrame => currentFrame;

    /// <summary>
    /// Returns true if this unit has idle animation frames assigned.
    /// </summary>
    public bool HasIdleAnimation => idleFrames != null && idleFrames.Length > 0;

    /// <summary>
    /// Returns true if this unit has attack animation frames assigned.
    /// </summary>
    public bool HasAttackAnimation => attackFrames != null && attackFrames.Length > 0;

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // If no sprite renderer found on this object, check children
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        if (playIdleOnStart)
        {
            PlayIdle();
        }
        else if (fallbackSprite != null && spriteRenderer != null)
        {
            spriteRenderer.sprite = fallbackSprite;
        }
    }

    private void Update()
    {
        if (!isPlaying || currentFrames == null || currentFrames.Length == 0)
            return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / currentFPS;

        // Advance frames
        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            AdvanceFrame();
        }
    }

    // ========================================
    // PUBLIC API - IDLE
    // ========================================

    /// <summary>
    /// Starts playing the idle animation (loops forever).
    /// Safe to call even if no idle frames are assigned.
    /// </summary>
    public void PlayIdle()
    {
        if (!HasIdleAnimation)
        {
            // No idle animation - just show fallback or first frame
            if (fallbackSprite != null && spriteRenderer != null)
                spriteRenderer.sprite = fallbackSprite;
            return;
        }

        SetAnimation(idleFrames, idleFPS, true, null);
        currentState = AnimState.Idle;
    }

    /// <summary>
    /// Pauses the current animation (remembers state for resume).
    /// </summary>
    public void PauseIdle()
    {
        if (currentState == AnimState.Paused) return;

        stateBeforePause = currentState;
        currentState = AnimState.Paused;
        isPlaying = false;
    }

    /// <summary>
    /// Resumes animation from paused state.
    /// </summary>
    public void ResumeIdle()
    {
        if (currentState != AnimState.Paused) return;

        currentState = stateBeforePause;
        isPlaying = true;
    }

    /// <summary>
    /// Stops all animation completely.
    /// </summary>
    public void StopAll()
    {
        isPlaying = false;
        currentState = AnimState.Idle;
    }

    // ========================================
    // PUBLIC API - ATTACK
    // ========================================

    /// <summary>
    /// Plays the attack animation once, then returns to idle.
    /// If no attack frames are assigned, immediately fires OnAttackComplete.
    /// </summary>
    public void PlayAttack()
    {
        PlayAttack(null);
    }

    /// <summary>
    /// Plays the attack animation with a callback when complete.
    /// </summary>
    /// <param name="onComplete">Called when attack animation finishes</param>
    public void PlayAttack(Action onComplete)
    {
        if (!HasAttackAnimation)
        {
            // No attack animation - immediately complete
            onComplete?.Invoke();
            OnAttackComplete?.Invoke();
            return;
        }

        currentState = AnimState.Attacking;

        // Chain callbacks: user callback + return to idle
        Action chainedCallback = () =>
        {
            onComplete?.Invoke();
            OnAttackComplete?.Invoke();

            // Return to idle after attack (unless we're paused or destroyed)
            if (this != null && gameObject != null && gameObject.activeInHierarchy)
            {
                PlayIdle();
            }
        };

        SetAnimation(attackFrames, attackFPS, loopAttack, loopAttack ? null : chainedCallback);
    }

    /// <summary>
    /// Interrupts attack animation and returns to idle immediately.
    /// </summary>
    public void CancelAttack()
    {
        if (currentState == AnimState.Attacking)
        {
            PlayIdle();
        }
    }

    // ========================================
    // PUBLIC API - CUSTOM ANIMATION
    // ========================================

    /// <summary>
    /// Plays a custom animation (for future expansion: hit reactions, special abilities, etc.)
    /// </summary>
    public void PlayCustom(Action onComplete = null)
    {
        if (customFrames == null || customFrames.Length == 0)
        {
            onComplete?.Invoke();
            OnCustomComplete?.Invoke();
            return;
        }

        currentState = AnimState.Custom;

        Action chainedCallback = () =>
        {
            onComplete?.Invoke();
            OnCustomComplete?.Invoke();

            if (this != null && gameObject != null && gameObject.activeInHierarchy)
            {
                PlayIdle();
            }
        };

        SetAnimation(customFrames, customFPS, loopCustom, loopCustom ? null : chainedCallback);
    }

    /// <summary>
    /// Plays an arbitrary set of frames (for dynamic/runtime animations).
    /// </summary>
    /// <param name="frames">Sprite frames to play</param>
    /// <param name="fps">Frames per second</param>
    /// <param name="loop">If true, loops forever</param>
    /// <param name="onComplete">Called when animation completes (if not looping)</param>
    public void PlayFrames(Sprite[] frames, int fps, bool loop, Action onComplete = null)
    {
        if (frames == null || frames.Length == 0)
        {
            onComplete?.Invoke();
            return;
        }

        currentState = AnimState.Custom;
        SetAnimation(frames, fps, loop, onComplete);
    }

    // ========================================
    // PRIVATE HELPERS
    // ========================================

    private void SetAnimation(Sprite[] frames, int fps, bool loop, Action onComplete)
    {
        currentFrames = frames;
        currentFPS = fps;
        currentLoop = loop;
        currentCompleteCallback = onComplete;

        currentFrame = 0;
        frameTimer = 0f;
        isPlaying = true;

        // Show first frame immediately
        if (spriteRenderer != null && frames.Length > 0)
        {
            spriteRenderer.sprite = frames[0];
        }

        OnFrameChanged?.Invoke(0);
    }

    private void AdvanceFrame()
    {
        currentFrame++;

        // Check if animation is complete
        if (currentFrame >= currentFrames.Length)
        {
            if (currentLoop)
            {
                // Loop back to start
                currentFrame = 0;
            }
            else
            {
                // Animation complete
                currentFrame = currentFrames.Length - 1; // Stay on last frame
                isPlaying = false;

                // Fire callback
                currentCompleteCallback?.Invoke();
                return;
            }
        }

        // Update sprite
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = currentFrames[currentFrame];
        }

        OnFrameChanged?.Invoke(currentFrame);
    }

    // ========================================
    // EDITOR HELPERS
    // ========================================

#if UNITY_EDITOR
    [ContextMenu("Preview Idle Animation")]
    private void PreviewIdle()
    {
        if (idleFrames != null && idleFrames.Length > 0)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = idleFrames[0];
        }
    }

    [ContextMenu("Preview Attack Animation")]
    private void PreviewAttack()
    {
        if (attackFrames != null && attackFrames.Length > 0)
        {
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = attackFrames[0];
        }
    }
#endif
}
