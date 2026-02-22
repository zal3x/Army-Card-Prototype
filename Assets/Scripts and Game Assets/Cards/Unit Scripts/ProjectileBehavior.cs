using UnityEngine;

/// <summary>
/// Self-contained projectile behavior. Handles its own movement and destruction
/// independently of the firing unit. This ensures projectiles complete their
/// journey even if the firing unit is destroyed mid-combat.
/// </summary>
public class ProjectileBehavior : MonoBehaviour
{
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float speed;
    private float elapsed;
    private float duration;
    private bool initialized = false;
    private bool spawnImpact = true;

    /// <summary>
    /// Initialize the projectile with its trajectory.
    /// </summary>
    /// <param name="start">Starting position</param>
    /// <param name="target">Target position</param>
    /// <param name="projectileSpeed">Speed in units per second</param>
    /// <param name="spawnImpactEffect">Whether to spawn impact effect on hit</param>
    public void Initialize(Vector3 start, Vector3 target, float projectileSpeed, bool spawnImpactEffect = true)
    {
        startPosition = start;
        targetPosition = target;
        speed = projectileSpeed;
        spawnImpact = spawnImpactEffect;
        elapsed = 0f;

        float distance = Vector3.Distance(start, target);
        duration = distance / speed;

        // Point toward target
        Vector3 direction = (target - start).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0, 0, angle);

        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        transform.position = Vector3.Lerp(startPosition, targetPosition, t);

        // Check if we've reached the target
        if (t >= 1f)
        {
            OnHitTarget();
        }
    }

    private void OnHitTarget()
    {
        // Spawn impact effect using static method (doesn't depend on any unit)
        if (spawnImpact)
        {
            UnitAttackEffects.SpawnImpactEffectStatic(targetPosition);
        }

        // Untrack and destroy immediately
        UnitAttackEffects.UntrackVfxObjectStatic(gameObject);
        Destroy(gameObject);
    }
}
