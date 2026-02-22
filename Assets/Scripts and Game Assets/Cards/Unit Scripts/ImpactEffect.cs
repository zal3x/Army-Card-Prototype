using UnityEngine;

/// <summary>
/// Self-contained impact effect behavior. Handles its own animation and destruction
/// independently of any parent object. This ensures impact effects complete properly
/// even if units are destroyed mid-combat.
/// </summary>
public class ImpactEffect : MonoBehaviour
{
    private float duration = 0.15f;
    private float elapsed = 0f;
    private Vector3 startScale;
    private Vector3 endScale;
    private SpriteRenderer spriteRenderer;
    private Color startColor;
    private bool initialized = false;

    /// <summary>
    /// Initialize the impact effect.
    /// </summary>
    public void Initialize()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogWarning("[ImpactEffect] No SpriteRenderer found!");
            Destroy(gameObject);
            return;
        }

        startScale = transform.localScale;
        endScale = startScale * 2f;
        startColor = spriteRenderer.color;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        elapsed += Time.deltaTime;
        float t = elapsed / duration;

        if (t >= 1f)
        {
            // Animation complete - destroy self
            UnitAttackEffects.UntrackVfxObjectStatic(gameObject);
            Destroy(gameObject);
            return;
        }

        // Scale up and fade out
        transform.localScale = Vector3.Lerp(startScale, endScale, t);
        Color color = startColor;
        color.a = Mathf.Lerp(1f, 0f, t);
        spriteRenderer.color = color;
    }
}
