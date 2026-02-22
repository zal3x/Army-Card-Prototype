using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles attack visual effects for units (muzzle flash, projectiles, recoil).
/// Simple placeholder system that can be replaced with full animations later.
/// </summary>
public class UnitAttackEffects : MonoBehaviour
{
    // Static list to track all spawned VFX objects for cleanup
    private static List<GameObject> activeVfxObjects = new List<GameObject>();

    /// <summary>
    /// Cleans up all lingering VFX objects (projectiles, muzzle flashes, impacts).
    /// Call this at the end of each combat round to prevent VFX buildup.
    /// </summary>
    public static void CleanupAllVfx()
    {
        int count = 0;
        for (int i = activeVfxObjects.Count - 1; i >= 0; i--)
        {
            if (activeVfxObjects[i] != null)
            {
                Object.Destroy(activeVfxObjects[i]);
                count++;
            }
        }
        activeVfxObjects.Clear();

        if (count > 0)
            Debug.Log($"[UnitAttackEffects] Cleaned up {count} lingering VFX objects");
    }

    /// <summary>
    /// Registers a VFX object for cleanup tracking.
    /// </summary>
    private static void TrackVfxObject(GameObject vfxObject)
    {
        if (vfxObject != null)
            activeVfxObjects.Add(vfxObject);
    }

    /// <summary>
    /// Unregisters a VFX object (call before destroying it normally).
    /// </summary>
    private static void UntrackVfxObject(GameObject vfxObject)
    {
        activeVfxObjects.Remove(vfxObject);
    }

    /// <summary>
    /// Public static version of UntrackVfxObject for use by ProjectileBehavior.
    /// </summary>
    public static void UntrackVfxObjectStatic(GameObject vfxObject)
    {
        activeVfxObjects.Remove(vfxObject);
    }

    [Header("Attack Type")]
    [Tooltip("Type of attack effect this unit uses")]
    public AttackEffectType attackType = AttackEffectType.MuzzleFlash;

    [Header("Muzzle Flash")]
    [Tooltip("Color of muzzle flash (ignored if using sprites)")]
    public Color muzzleFlashColor = new Color(1f, 0.9f, 0.3f);
    [Tooltip("Size of muzzle flash")]
    [Range(0.1f, 2f)]
    public float muzzleFlashSize = 0.3f;

    [Header("Projectile")]
    [Tooltip("Color of projectile tracer")]
    public Color projectileColor = Color.yellow;
    [Tooltip("Size of projectile (go big for comical effects!)")]
    [Range(0.05f, 5f)]
    public float projectileSize = 0.1f;
    [Tooltip("Speed of projectile (units per second)")]
    [Range(1f, 50f)]
    public float projectileSpeed = 20f;

    [Header("Recoil")]
    [Tooltip("Enable recoil animation when firing")]
    public bool enableRecoil = false;
    [Tooltip("Recoil distance")]
    [Range(0f, 0.5f)]
    public float recoilDistance = 0.1f;

    [Header("Squash & Stretch")]
    [Tooltip("Enable squash/stretch animation when firing (gives weight to attacks)")]
    public bool enableSquashStretch = true;
    [Tooltip("How much to squash/stretch (1.0 = no effect, 1.2 = 20% squash/stretch)")]
    [Range(1.0f, 1.5f)]
    public float squashStretchAmount = 1.15f;

    [Header("Optional Sprites")]
    [Tooltip("Optional: Custom muzzle flash sprite (leave empty for default)")]
    public Sprite muzzleFlashSprite;
    [Tooltip("Optional: Custom projectile sprite (leave empty for default)")]
    public Sprite projectileSprite;

    [Header("Spawn Points")]
    [Tooltip("Where muzzle flash/projectiles spawn (relative to unit). Leave empty to auto-detect.")]
    public Transform attackSpawnPoint;

    [Header("Debug")]
    [Tooltip("Enable debug logging for troubleshooting")]
    public bool debugMode = false;

    private SpriteRenderer spriteRenderer;
    private Vector3 originalPosition;
    private Vector3 originalScale;
    private Coroutine recoilRoutine;
    private Coroutine squashStretchRoutine;
    private UnitAnimator unitAnimator;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        originalPosition = transform.localPosition;
        originalScale = transform.localScale;

        // Cache reference to UnitAnimator if present
        unitAnimator = GetComponent<UnitAnimator>();

        // Auto-find spawn point if not assigned
        if (attackSpawnPoint == null)
        {
            // Try to find a child named "AttackSpawnPoint"
            Transform found = transform.Find("AttackSpawnPoint");
            if (found != null)
                attackSpawnPoint = found;
            else
                attackSpawnPoint = transform; // Use unit position as fallback
        }
    }

    /// <summary>
    /// Plays attack animation/effects when this unit fires.
    /// Call this from CombatPlayback when this unit deals damage.
    /// </summary>
    /// <param name="targetPosition">Where the attack is aimed (for projectiles)</param>
    public void PlayAttackEffect(Vector3 targetPosition)
    {
        // Always log when this is called (to debug firing issues)
        Debug.Log($"[UnitAttackEffects] PlayAttackEffect called on {gameObject.name}! Type: {attackType}, Target: {targetPosition}, SpawnPoint: {(attackSpawnPoint != null ? attackSpawnPoint.name : "NULL")}");

        // Play weapon sound based on attack type
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWeaponSound(attackType);
        }

        // Squash & stretch for attack weight (runs alongside other effects)
        if (enableSquashStretch)
        {
            PlaySquashStretch();
        }

        // Trigger attack animation if UnitAnimator is present
        if (unitAnimator != null && unitAnimator.HasAttackAnimation)
        {
            unitAnimator.PlayAttack();
            if (debugMode)
                Debug.Log($"[UnitAttackEffects] Triggered UnitAnimator.PlayAttack() on {gameObject.name}");
        }

        if (attackSpawnPoint == null)
        {
            Debug.LogWarning($"[UnitAttackEffects] {gameObject.name} has NULL attackSpawnPoint! Trying to find one...");
            // Try to auto-find
            attackSpawnPoint = transform.Find("AttackSpawnPoint");
            if (attackSpawnPoint == null)
            {
                attackSpawnPoint = transform; // Fallback to unit center
                Debug.LogWarning($"[UnitAttackEffects] {gameObject.name} using unit center as fallback spawn point");
            }
        }

        switch (attackType)
        {
            case AttackEffectType.MuzzleFlash:
                Debug.Log($"[UnitAttackEffects] {gameObject.name} spawning MuzzleFlash");
                SpawnMuzzleFlash();
                if (enableRecoil) PlayRecoil();
                break;

            case AttackEffectType.Projectile:
                Debug.Log($"[UnitAttackEffects] {gameObject.name} spawning Projectile + MuzzleFlash");
                SpawnProjectile(targetPosition);
                SpawnMuzzleFlash(); // Also spawn flash
                if (enableRecoil) PlayRecoil();
                break;

            case AttackEffectType.MuzzleFlashOnly:
                Debug.Log($"[UnitAttackEffects] {gameObject.name} spawning MuzzleFlashOnly");
                SpawnMuzzleFlash();
                break;

            case AttackEffectType.None:
                Debug.LogWarning($"[UnitAttackEffects] {gameObject.name} attack type is NONE - no effect will be shown!");
                break;
        }
    }

    private void SpawnMuzzleFlash()
    {
        Vector3 spawnPos = attackSpawnPoint.position;

        if (debugMode)
            Debug.Log($"[UnitAttackEffects] Spawning muzzle flash at {spawnPos}, color: {muzzleFlashColor}, size: {muzzleFlashSize}");

        // Create muzzle flash GameObject
        GameObject flash = new GameObject("MuzzleFlash");
        flash.transform.position = spawnPos;
        TrackVfxObject(flash);

        SpriteRenderer sr = flash.AddComponent<SpriteRenderer>();

        if (muzzleFlashSprite != null)
        {
            // Use custom sprite
            sr.sprite = muzzleFlashSprite;
            if (debugMode)
                Debug.Log($"[UnitAttackEffects] Using custom muzzle flash sprite");
        }
        else
        {
            // Create simple circle sprite as placeholder
            sr.sprite = CreateCircleSprite();
            if (debugMode)
                Debug.Log($"[UnitAttackEffects] Using generated circle sprite");
        }

        sr.color = muzzleFlashColor;
        sr.sortingLayerName = "VFX";
        sr.sortingOrder = 100;

        if (debugMode)
            Debug.Log($"[UnitAttackEffects] Muzzle flash created! Sorting: {sr.sortingLayerName} (order {sr.sortingOrder})");

        // Set size
        flash.transform.localScale = Vector3.one * muzzleFlashSize;

        // Animate: pop out then fade
        StartCoroutine(MuzzleFlashRoutine(flash));
    }

    private IEnumerator MuzzleFlashRoutine(GameObject flash)
    {
        // Safety check - flash may be destroyed by cleanup
        if (flash == null) yield break;

        SpriteRenderer sr = flash.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float duration = 0.1f;
        Vector3 startScale = flash.transform.localScale * 0.5f;
        Vector3 endScale = flash.transform.localScale * 1.5f;

        // Pop out
        float elapsed = 0f;
        while (elapsed < duration * 0.3f)
        {
            if (flash == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.3f);
            flash.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            yield return null;
        }

        // Fade out
        if (flash == null) yield break;
        Color color = sr.color;
        elapsed = 0f;
        while (elapsed < duration * 0.7f)
        {
            if (flash == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.7f);
            color.a = Mathf.Lerp(1f, 0f, t);
            sr.color = color;
            yield return null;
        }

        if (flash == null) yield break;
        UntrackVfxObject(flash);
        Destroy(flash);
    }

    private void SpawnProjectile(Vector3 targetPosition)
    {
        Vector3 spawnPos = attackSpawnPoint.position;

        // Create projectile GameObject
        GameObject projectile = new GameObject("Projectile");
        projectile.transform.position = spawnPos;
        TrackVfxObject(projectile);

        SpriteRenderer sr = projectile.AddComponent<SpriteRenderer>();

        if (projectileSprite != null)
        {
            // Use custom sprite
            sr.sprite = projectileSprite;
        }
        else
        {
            // Create simple circle sprite as placeholder
            sr.sprite = CreateCircleSprite();
        }

        sr.color = projectileColor;
        sr.sortingLayerName = "VFX";
        sr.sortingOrder = 95;

        // Set size
        projectile.transform.localScale = Vector3.one * projectileSize;

        // Use ProjectileBehavior component for self-contained movement and destruction
        // This ensures the projectile completes its journey even if the firing unit is destroyed
        ProjectileBehavior behavior = projectile.AddComponent<ProjectileBehavior>();
        behavior.Initialize(spawnPos, targetPosition, projectileSpeed, true);
    }

    private IEnumerator ProjectileRoutine(GameObject projectile, Vector3 start, Vector3 target)
    {
        // Safety check - projectile may be destroyed by cleanup
        if (projectile == null) yield break;

        float distance = Vector3.Distance(start, target);
        float duration = distance / projectileSpeed;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Check if projectile was destroyed (e.g., by cleanup or unit death)
            if (projectile == null)
            {
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            projectile.transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        // Final null check before cleanup
        if (projectile == null) yield break;

        // Small impact flash at target
        SpawnImpactEffect(target);

        UntrackVfxObject(projectile);
        Destroy(projectile);
    }

    private void SpawnImpactEffect(Vector3 position)
    {
        // Call static version
        SpawnImpactEffectStatic(position);
    }

    /// <summary>
    /// Static version of SpawnImpactEffect that can be called from ProjectileBehavior.
    /// Uses ImpactEffect component for self-contained animation.
    /// </summary>
    public static void SpawnImpactEffectStatic(Vector3 position)
    {
        // Play impact sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayImpact();
        }

        GameObject impact = new GameObject("Impact");
        impact.transform.position = position;
        TrackVfxObject(impact);

        SpriteRenderer sr = impact.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSpriteStatic();
        sr.color = new Color(1f, 0.5f, 0f, 1f); // Orange
        sr.sortingLayerName = "VFX";
        sr.sortingOrder = 100;

        impact.transform.localScale = Vector3.one * 0.2f;

        // Use ImpactEffect component for self-contained animation
        ImpactEffect impactFx = impact.AddComponent<ImpactEffect>();
        impactFx.Initialize();
    }

    private IEnumerator ImpactFlashRoutine(GameObject impact)
    {
        // Safety check - impact may be destroyed by cleanup
        if (impact == null) yield break;

        SpriteRenderer sr = impact.GetComponent<SpriteRenderer>();
        if (sr == null) yield break;

        float duration = 0.15f;
        float elapsed = 0f;

        Vector3 startScale = impact.transform.localScale;
        Vector3 endScale = startScale * 2f;

        while (elapsed < duration)
        {
            if (impact == null) yield break;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // Scale up and fade out
            impact.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            Color color = sr.color;
            color.a = Mathf.Lerp(1f, 0f, t);
            sr.color = color;

            yield return null;
        }

        if (impact == null) yield break;
        UntrackVfxObject(impact);
        Destroy(impact);
    }

    private void PlayRecoil()
    {
        if (recoilRoutine != null) StopCoroutine(recoilRoutine);
        recoilRoutine = StartCoroutine(RecoilRoutine());
    }

    private IEnumerator RecoilRoutine()
    {
        Vector3 startPos = transform.localPosition;
        Vector3 recoilPos = startPos;

        // Determine recoil direction based on unit facing
        // For now, assume units face right if on left side, left if on right side
        float recoilDirection = (transform.position.x < 0) ? -1f : 1f;
        recoilPos.x = startPos.x + (recoilDirection * recoilDistance);

        float duration = 0.1f;

        // Recoil back
        float elapsed = 0f;
        while (elapsed < duration * 0.4f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.4f);
            transform.localPosition = Vector3.Lerp(startPos, recoilPos, t);
            yield return null;
        }

        // Return to original position
        elapsed = 0f;
        while (elapsed < duration * 0.6f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.6f);
            transform.localPosition = Vector3.Lerp(recoilPos, startPos, t);
            yield return null;
        }

        transform.localPosition = startPos;
        recoilRoutine = null;
    }

    private void PlaySquashStretch()
    {
        if (squashStretchRoutine != null) StopCoroutine(squashStretchRoutine);
        squashStretchRoutine = StartCoroutine(SquashStretchRoutine());
    }

    private IEnumerator SquashStretchRoutine()
    {
        // Squash & Stretch gives weight to attacks:
        // 1. Squash (anticipation) - compress before firing
        // 2. Stretch (release) - extend as attack fires
        // 3. Return to normal

        float amount = squashStretchAmount;
        float duration = 0.15f;

        // Squash: compress vertically, expand horizontally
        Vector3 squashScale = new Vector3(
            originalScale.x * amount,
            originalScale.y / amount,
            originalScale.z
        );

        // Stretch: expand vertically, compress horizontally
        Vector3 stretchScale = new Vector3(
            originalScale.x / amount,
            originalScale.y * amount,
            originalScale.z
        );

        float elapsed = 0f;

        // Phase 1: Squash (anticipation) - quick
        while (elapsed < duration * 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.3f);
            t = t * t; // Ease in
            transform.localScale = Vector3.Lerp(originalScale, squashScale, t);
            yield return null;
        }

        // Phase 2: Stretch (release) - quick snap
        elapsed = 0f;
        while (elapsed < duration * 0.3f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.3f);
            transform.localScale = Vector3.Lerp(squashScale, stretchScale, t);
            yield return null;
        }

        // Phase 3: Return to normal - ease out
        elapsed = 0f;
        while (elapsed < duration * 0.4f)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / (duration * 0.4f);
            t = 1f - (1f - t) * (1f - t); // Ease out
            transform.localScale = Vector3.Lerp(stretchScale, originalScale, t);
            yield return null;
        }

        transform.localScale = originalScale;
        squashStretchRoutine = null;
    }

    /// <summary>
    /// Creates a simple circle sprite as a placeholder.
    /// Replace with actual sprites later.
    /// </summary>
    private Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);

                if (distance < radius)
                {
                    // Soft edge
                    float alpha = Mathf.Clamp01(1f - (distance / radius));
                    alpha = Mathf.Pow(alpha, 0.5f); // Soften edge
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    /// <summary>
    /// Static version of CreateCircleSprite for use in static methods.
    /// </summary>
    private static Sprite CreateCircleSpriteStatic()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];

        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);

                if (distance < radius)
                {
                    float alpha = Mathf.Clamp01(1f - (distance / radius));
                    alpha = Mathf.Pow(alpha, 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private void OnDestroy()
    {
        if (recoilRoutine != null) StopCoroutine(recoilRoutine);
        if (squashStretchRoutine != null) StopCoroutine(squashStretchRoutine);

        // Restore original scale in case we're destroyed mid-animation
        transform.localScale = originalScale;
    }
}

/// <summary>
/// Types of attack effects units can use.
/// </summary>
public enum AttackEffectType
{
    None,              // No visual effect
    MuzzleFlashOnly,   // Just a flash (melee, explosions)
    MuzzleFlash,       // Flash + optional recoil (rifles, cannons)
    Projectile         // Flash + visible projectile (rockets, missiles)
}
