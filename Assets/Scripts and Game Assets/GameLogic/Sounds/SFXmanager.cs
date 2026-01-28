using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [Header("Explosion Sounds")]
    public AudioClip[] explosionClips;

    [Range(0f, 1f)]
    public float explosionVolume = 0.8f;

    private AudioSource audioSource;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.spatialBlend = 0f; // 2D sound (recommended for now)
    }

    public void PlayExplosion()
    {
        if (explosionClips == null || explosionClips.Length == 0)
            return;

        var clip = explosionClips[UnityEngine.Random.Range(0, explosionClips.Length)];
        audioSource.PlayOneShot(clip, explosionVolume);
    }
}
