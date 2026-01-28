using UnityEngine;

/// <summary>
/// CombatVFXSpawner: Spawns visual effects for combat events.
/// 
/// RESPONSIBILITIES:
/// - Death explosions (sprite-based)
/// - Spawn smoke/effects (when card lands)
/// - Destroying unit visuals on death
/// 
/// DESIGN:
/// - Separate from CardFlightManager (single responsibility)
/// - Uses SpriteAnimationPlayer for pixel art animations
/// - Called by CombatPlayback when units die
/// - Can also trigger CardFlightManager for card-to-discard flight
/// 
/// SETUP:
/// 1. Create GameObject in scene with this component
/// 2. Assign explosion sprite frames in Inspector
/// 3. Optionally assign spawn smoke frames
/// 4. Assign FieldSpawner reference
/// 5. Call from CombatPlayback.HandleEvent() on UnitDestroyed
/// </summary>
public class CombatVFXSpawner : MonoBehaviour
{
    // ========================================
    // SINGLETON (optional)
    // ========================================

    private static CombatVFXSpawner _instance;
    public static CombatVFXSpawner Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CombatVFXSpawner>();
            }
            return _instance;
        }
    }

    // ========================================
    // INSPECTOR CONFIGURATION
    // ========================================

    [Header("Death Explosion (Sprite Animation)")]
    [Tooltip("Array of sprites for the death explosion animation. Drag your PNG frames here.")]
    public Sprite[] deathExplosionFrames;

    [Tooltip("Frames per second for the explosion animation.")]
    [Range(1, 60)]
    public int explosionFPS = 12;

    [Tooltip("Scale multiplier for the explosion.")]
    public float explosionScale = 1f;

    [Header("Explosion Position Offset")]
    [Tooltip("Offset from unit position (useful if unit pivot is at feet).")]
    public Vector3 explosionOffset = Vector3.zero;

    [Tooltip("Z position for explosion (negative = in front of units).")]
    public float explosionZ = -0.5f;

    [Header("Explosion Sorting")]
    public string explosionSortingLayer = "VFX";
    public int explosionSortingOrder = 100;

    [Header("Explosion Effects")]
    [Tooltip("If true, explosion scales up then down during animation.")]
    public bool useScalePop = true;

    [Tooltip("If true, explosion fades out near the end.")]
    public bool useFadeOut = true;

    [Header("Alternative: Prefab-Based Explosions")]
    [Tooltip("If assigned, spawns this prefab instead of building from frames. Prefab should have SpriteAnimationPlayer.")]
    public GameObject explosionPrefab;

    [Header("=== SPAWN SMOKE/ANIMATION ===")]
    [Tooltip("Array of sprites for spawn smoke animation. Plays when card lands on unit.")]
    public Sprite[] spawnSmokeFrames;

    [Tooltip("Frames per second for spawn smoke.")]
    [Range(1, 60)]
    public int spawnSmokeFPS = 15;

    [Tooltip("Scale multiplier for spawn smoke.")]
    public float spawnSmokeScale = 1f;

    [Tooltip("Z position for spawn smoke.")]
    public float spawnSmokeZ = -0.3f;

    [Header("Spawn Smoke Sorting")]
    public string spawnSmokeSortingLayer = "VFX";
    public int spawnSmokeSortingOrder = 90;

    [Header("Alternative: Prefab-Based Spawn Smoke")]
    [Tooltip("If assigned, spawns this prefab instead of building from frames.")]
    public GameObject spawnSmokePrefab;

    [Header("Integration")]
    [Tooltip("If assigned, triggers card-to-discard flight after explosion.")]
    public CardFlightManager cardFlightManager;

    [Tooltip("Reference to FieldSpawner for getting unit positions and destroying visuals.")]
    public FieldSpawner fieldSpawner;

    [Header("Timing")]
    [Tooltip("Delay before notifying CardFlightManager (lets explosion play a bit first).")]
    public float cardFlightDelay = 0.2f;

    [Tooltip("If true, unit visual is destroyed immediately when explosion spawns.")]
    public bool destroyUnitVisualOnExplosion = true;

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else if (_instance != this)
        {
            Debug.LogWarning("[CombatVFXSpawner] Multiple instances detected. Using first one.");
        }

        // Auto-find references if not assigned
        if (cardFlightManager == null)
            cardFlightManager = FindObjectOfType<CardFlightManager>();

        if (fieldSpawner == null)
            fieldSpawner = FindObjectOfType<FieldSpawner>();
    }

    // ========================================
    // PUBLIC API - DEATH EXPLOSIONS
    // ========================================

    /// <summary>
    /// Spawns a death explosion at the specified world position.
    /// Destroys the unit visual immediately and triggers card flight to discard.
    /// 
    /// CALL THIS FROM: CombatPlayback when handling UnitDestroyed event.
    /// </summary>
    /// <param name="worldPosition">Where to spawn the explosion</param>
    /// <param name="unitId">Unit ID (for card flight integration and visual destruction)</param>
    /// <param name="triggerCardFlight">If true, also tells CardFlightManager about the death</param>
    public void SpawnDeathExplosion(Vector3 worldPosition, int unitId = -1, bool triggerCardFlight = true)
    {
        // IMMEDIATELY destroy the unit visual (both Player A and B)
        if (destroyUnitVisualOnExplosion && unitId >= 0)
        {
            DestroyUnitVisual(unitId);
        }

        // Calculate explosion position with offset
        Vector3 explosionPos = worldPosition + explosionOffset;
        explosionPos.z = explosionZ;

        // Spawn the explosion
        SpriteAnimationPlayer explosion = null;

        if (explosionPrefab != null)
        {
            // Use prefab
            explosion = SpriteAnimationPlayer.SpawnFromPrefab(explosionPrefab, explosionPos);
        }
        else if (deathExplosionFrames != null && deathExplosionFrames.Length > 0)
        {
            // Build from frames
            explosion = SpriteAnimationPlayer.SpawnExplosion(
                deathExplosionFrames,
                explosionPos,
                explosionFPS,
                destroyOnComplete: true,
                sortingLayer: explosionSortingLayer,
                sortingOrder: explosionSortingOrder
            );

            // Apply additional settings
            if (explosion != null)
            {
                explosion.SetBaseScale(explosionScale);
                explosion.useScalePop = useScalePop;
                explosion.fadeOutAtEnd = useFadeOut;
            }
        }
        else
        {
            Debug.LogWarning("[CombatVFXSpawner] No explosion frames or prefab assigned!");
        }

        // Log for debugging
        Debug.Log($"[CombatVFXSpawner] Death explosion spawned at {explosionPos} for unit {unitId}");

        // Trigger card flight after a short delay (so explosion is visible first)
        if (triggerCardFlight && cardFlightManager != null && unitId >= 0)
        {
            if (cardFlightDelay > 0f)
            {
                LeanTween.delayedCall(gameObject, cardFlightDelay, () =>
                {
                    cardFlightManager.OnUnitDestroyed(unitId);
                });
            }
            else
            {
                cardFlightManager.OnUnitDestroyed(unitId);
            }
        }
    }

    /// <summary>
    /// Spawns a death explosion for a unit, automatically getting position from FieldSpawner.
    /// </summary>
    public void SpawnDeathExplosionForUnit(int unitId, bool triggerCardFlight = true)
    {
        Vector3 position = Vector3.zero;

        if (fieldSpawner != null)
        {
            position = fieldSpawner.GetUnitPosition(unitId);
        }

        if (position == Vector3.zero)
        {
            Debug.LogWarning($"[CombatVFXSpawner] Could not find position for unit {unitId}");
            // Still trigger card flight even if we can't show explosion
            if (triggerCardFlight && cardFlightManager != null && unitId >= 0)
            {
                cardFlightManager.OnUnitDestroyed(unitId);
            }
            return;
        }

        SpawnDeathExplosion(position, unitId, triggerCardFlight);
    }

    /// <summary>
    /// Spawns explosions for multiple dead units.
    /// Useful when processing a batch of deaths at round end.
    /// </summary>
    public void SpawnDeathExplosionsForUnits(int[] unitIds, float staggerDelay = 0.1f)
    {
        for (int i = 0; i < unitIds.Length; i++)
        {
            int unitId = unitIds[i];
            float delay = i * staggerDelay;

            if (delay > 0f)
            {
                LeanTween.delayedCall(gameObject, delay, () =>
                {
                    SpawnDeathExplosionForUnit(unitId, triggerCardFlight: true);
                });
            }
            else
            {
                SpawnDeathExplosionForUnit(unitId, triggerCardFlight: true);
            }
        }
    }

    // ========================================
    // PUBLIC API - SPAWN SMOKE
    // ========================================

    /// <summary>
    /// Spawns spawn smoke/dust effect when a card lands and unit appears.
    /// Call this from CardFlightManager when card slam completes.
    /// </summary>
    /// <param name="worldPosition">Where to spawn the smoke (unit position)</param>
    public void SpawnSpawnSmoke(Vector3 worldPosition)
    {
        Vector3 smokePos = worldPosition;
        smokePos.z = spawnSmokeZ;

        SpriteAnimationPlayer smoke = null;

        if (spawnSmokePrefab != null)
        {
            smoke = SpriteAnimationPlayer.SpawnFromPrefab(spawnSmokePrefab, smokePos);
        }
        else if (spawnSmokeFrames != null && spawnSmokeFrames.Length > 0)
        {
            smoke = SpriteAnimationPlayer.SpawnExplosion(
                spawnSmokeFrames,
                smokePos,
                spawnSmokeFPS,
                destroyOnComplete: true,
                sortingLayer: spawnSmokeSortingLayer,
                sortingOrder: spawnSmokeSortingOrder
            );

            if (smoke != null)
            {
                smoke.SetBaseScale(spawnSmokeScale);
                smoke.useScalePop = false; // Smoke usually doesn't pop
                smoke.fadeOutAtEnd = true; // Smoke fades out naturally
            }
        }

        if (smoke != null)
        {
            Debug.Log($"[CombatVFXSpawner] Spawn smoke at {smokePos}");
        }
    }

    /// <summary>
    /// Returns the duration of the spawn smoke animation.
    /// Used by CardFlightManager to time unit reveal.
    /// </summary>
    public float GetSpawnSmokeDuration()
    {
        if (spawnSmokeFrames != null && spawnSmokeFrames.Length > 0 && spawnSmokeFPS > 0)
        {
            return (float)spawnSmokeFrames.Length / spawnSmokeFPS;
        }
        return 0.3f; // Default fallback
    }

    // ========================================
    // HELPER METHODS
    // ========================================

    /// <summary>
    /// Destroys a unit's world visual immediately.
    /// Works for both Player A and Player B units.
    /// </summary>
    private void DestroyUnitVisual(int unitId)
    {
        if (fieldSpawner == null)
        {
            Debug.LogWarning("[CombatVFXSpawner] No FieldSpawner assigned - cannot destroy unit visual");
            return;
        }

        fieldSpawner.DestroyUnitVisual(unitId);
    }
}

// ========================================
// INTEGRATION WITH COMBATPLAYBACK
// ========================================
//
// In your CombatPlayback.cs, in the HandleEvent() method:
//
// case CombatEventType.UnitDestroyed:
//     if (e.TargetUnit != null)
//     {
//         int unitId = e.TargetUnit.InstanceId;
//         
//         // Option 1: Use CombatVFXSpawner (recommended)
//         if (CombatVFXSpawner.Instance != null)
//         {
//             CombatVFXSpawner.Instance.SpawnDeathExplosionForUnit(unitId);
//         }
//         
//         // Option 2: Direct call (if you don't want singleton pattern)
//         // combatVFXSpawner.SpawnDeathExplosionForUnit(unitId);
//     }
//     break;
//
// ========================================
// SETUP INSTRUCTIONS
// ========================================
//
// 1. Create empty GameObject named "CombatVFXSpawner"
// 2. Add CombatVFXSpawner component
// 3. In Inspector, drag your explosion PNG frames into "Death Explosion Frames"
//    - Order matters: first frame to last frame of animation
//    - 8-16 frames is typical for a good pixel art explosion
// 4. Set "Explosion FPS" (12-15 is good for pixel art)
// 5. Adjust "Explosion Scale" to match your unit sizes
// 6. Optionally assign CardFlightManager and FieldSpawner references
//    (auto-finds if not assigned)
// 7. Make sure you have a "VFX" sorting layer in your project
//    (Edit > Project Settings > Tags and Layers)
//
// ========================================
// CREATING EXPLOSION PREFAB (ALTERNATIVE)
// ========================================
//
// If you prefer prefabs over frame arrays:
// 1. Create empty GameObject
// 2. Add SpriteRenderer
// 3. Add SpriteAnimationPlayer
// 4. Configure frames, FPS, etc. in the SpriteAnimationPlayer
// 5. Save as prefab
// 6. Assign prefab to CombatVFXSpawner.explosionPrefab
// 7. Leave deathExplosionFrames empty (prefab takes priority)