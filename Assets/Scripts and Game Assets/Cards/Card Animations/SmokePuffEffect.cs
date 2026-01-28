using UnityEngine;

/// <summary>
/// SmokePuffEffect: Simple placeholder smoke puff effect for unit spawning.
/// 
/// This creates a quick expanding and fading circle to simulate smoke.
/// Replace with a proper particle system later for better visuals.
/// 
/// SETUP:
/// 1. Create an empty GameObject
/// 2. Add a SpriteRenderer with a soft circle/cloud sprite
/// 3. Add this component
/// 4. Make it a prefab
/// 5. Assign to CardFlightManager.smokePuffPrefab
/// 
/// ALTERNATIVE:
/// For better results, use Unity's Particle System instead:
/// - Create a Particle System with short burst emission
/// - Use a soft particle texture
/// - Set size over lifetime to expand
/// - Set color over lifetime to fade out
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SmokePuffEffect : MonoBehaviour
{
    [Header("Animation Settings")]
    [Tooltip("How long the smoke effect lasts")]
    public float duration = 0.5f;

    [Tooltip("Starting scale of the smoke")]
    public float startScale = 0.5f;

    [Tooltip("Final scale of the smoke (expands over time)")]
    public float endScale = 2f;

    [Tooltip("Starting opacity (1 = fully opaque)")]
    public float startAlpha = 0.8f;

    [Header("Color")]
    [Tooltip("Smoke color")]
    public Color smokeColor = new Color(0.9f, 0.9f, 0.9f, 1f);

    [Header("Movement")]
    [Tooltip("How much the smoke rises during animation")]
    public float riseAmount = 0.5f;

    [Tooltip("Random spread for multiple puffs")]
    public float spreadRange = 0.3f;

    [Header("Multi-Puff")]
    [Tooltip("Number of smoke puffs to spawn")]
    public int puffCount = 3;

    [Tooltip("Delay between puff spawns")]
    public float puffDelay = 0.05f;

    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Start()
    {
        // If this is the "master" puff (position hasn't been randomized), spawn children
        if (puffCount > 1)
        {
            SpawnChildPuffs();
        }

        // Animate this puff
        AnimatePuff();
    }

    private void SpawnChildPuffs()
    {
        for (int i = 1; i < puffCount; i++)
        {
            // Create a child puff with slight position offset and delay
            Vector3 offset = new Vector3(
                Random.Range(-spreadRange, spreadRange),
                Random.Range(-spreadRange * 0.5f, spreadRange * 0.5f),
                0f
            );

            GameObject childPuff = Instantiate(gameObject, transform.position + offset, Quaternion.identity);
            
            // Prevent infinite recursion
            SmokePuffEffect childEffect = childPuff.GetComponent<SmokePuffEffect>();
            if (childEffect != null)
            {
                childEffect.puffCount = 1; // No more children
                
                // Slight variation in timing and size
                childEffect.duration = duration * Random.Range(0.8f, 1.2f);
                childEffect.startScale = startScale * Random.Range(0.7f, 1.3f);
                childEffect.endScale = endScale * Random.Range(0.8f, 1.2f);
            }

            // Stagger the start slightly
            childPuff.SetActive(false);
            StartCoroutine(ActivateDelayed(childPuff, puffDelay * i));
        }
    }

    private System.Collections.IEnumerator ActivateDelayed(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null)
            obj.SetActive(true);
    }

    private void AnimatePuff()
    {
        // Set initial state
        transform.localScale = Vector3.one * startScale;
        
        if (spriteRenderer != null)
        {
            Color c = smokeColor;
            c.a = startAlpha;
            spriteRenderer.color = c;
        }

        // Random rotation for variety
        transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        // Scale up
        LeanTween.scale(gameObject, Vector3.one * endScale, duration)
            .setEaseOutQuad();

        // Rise up
        Vector3 targetPos = transform.position + Vector3.up * riseAmount;
        LeanTween.move(gameObject, targetPos, duration)
            .setEaseOutQuad();

        // Fade out
        if (spriteRenderer != null)
        {
            LeanTween.value(gameObject, startAlpha, 0f, duration)
                .setEaseInQuad()
                .setOnUpdate((float val) =>
                {
                    if (spriteRenderer != null)
                    {
                        Color c = spriteRenderer.color;
                        c.a = val;
                        spriteRenderer.color = c;
                    }
                });
        }

        // Destroy when done
        Destroy(gameObject, duration + 0.1f);
    }
}