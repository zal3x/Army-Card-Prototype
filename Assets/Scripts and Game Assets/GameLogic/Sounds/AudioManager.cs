using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central audio manager for all game sounds.
/// Handles music playback with crossfading and categorized SFX.
///
/// SETUP:
/// 1. Create empty GameObject named "AudioManager"
/// 2. Add this component
/// 3. Place AudioSettings.asset in Resources folder
/// 4. AudioManager auto-loads settings on Awake
/// </summary>
public class AudioManager : MonoBehaviour
{
    // ========================================
    // SINGLETON
    // ========================================
    public static AudioManager Instance { get; private set; }

    // ========================================
    // AUDIO SOURCES
    // ========================================
    [Header("Audio Sources (auto-created if not assigned)")]
    [SerializeField] private AudioSource musicSourceA;
    [SerializeField] private AudioSource musicSourceB;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource ambienceSource;

    // ========================================
    // STATE
    // ========================================
    private AudioSource activeMusicSource;
    private AudioSource inactiveMusicSource;
    private Coroutine crossfadeRoutine;
    private GameState currentMusicState;
    private Dictionary<string, float> lastPlayedTimes = new Dictionary<string, float>();

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Ensure there is always an AudioListener in the scene
        if (FindAnyObjectByType<AudioListener>() == null)
            gameObject.AddComponent<AudioListener>();

        // Create audio sources if not assigned
        SetupAudioSources();

        // Set initial active music source
        activeMusicSource = musicSourceA;
        inactiveMusicSource = musicSourceB;
    }

    private void Start()
    {
        // Subscribe to game state changes
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged += OnGameStateChanged;
            Debug.Log("[AudioManager] Subscribed to GameStateManager.OnStateChanged");
        }
        else
        {
            Debug.LogWarning("[AudioManager] GameStateManager.Instance is null - music won't auto-switch on state changes");
        }

        // Subscribe to battle events
        if (BattleTestRunner.Instance != null)
        {
            BattleTestRunner.Instance.OnBattleEnded += OnBattleEnded;
            Debug.Log("[AudioManager] Subscribed to BattleTestRunner.OnBattleEnded");
        }
        else
        {
            Debug.LogWarning("[AudioManager] BattleTestRunner.Instance is null - victory/defeat music won't play");
        }

        // Start with appropriate music based on current state
        if (GameStateManager.Instance != null)
        {
            PlayMusicForState(GameStateManager.Instance.CurrentState);
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe from events
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnStateChanged -= OnGameStateChanged;
        }

        if (BattleTestRunner.Instance != null)
        {
            BattleTestRunner.Instance.OnBattleEnded -= OnBattleEnded;
        }
    }

    private void SetupAudioSources()
    {
        if (musicSourceA == null)
        {
            musicSourceA = CreateAudioSource("MusicSourceA", true);
        }
        if (musicSourceB == null)
        {
            musicSourceB = CreateAudioSource("MusicSourceB", true);
        }
        if (sfxSource == null)
        {
            sfxSource = CreateAudioSource("SFXSource", false);
        }
        if (uiSource == null)
        {
            uiSource = CreateAudioSource("UISource", false);
        }
        if (ambienceSource == null)
        {
            ambienceSource = CreateAudioSource("AmbienceSource", true);
        }

        // Music sources need loop capability
        musicSourceA.loop = true;
        musicSourceB.loop = true;
        ambienceSource.loop = true;
    }

    private AudioSource CreateAudioSource(string sourceName, bool loop)
    {
        GameObject child = new GameObject(sourceName);
        child.transform.SetParent(transform);
        AudioSource source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f; // 2D sound
        return source;
    }

    // ========================================
    // MUSIC SYSTEM
    // ========================================

    /// <summary>
    /// Plays music for the specified game state with crossfade.
    /// </summary>
    public void PlayMusicForState(GameState state)
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        AudioClip newClip = settings.GetMusicForState(state);

        // Don't restart same music
        if (activeMusicSource.clip == newClip && activeMusicSource.isPlaying)
            return;

        currentMusicState = state;
        CrossfadeToMusic(newClip);
    }

    /// <summary>
    /// Plays victory or defeat music (one-shot, doesn't loop).
    /// </summary>
    public void PlayVictoryDefeatMusic(bool isVictory)
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        AudioClip clip = isVictory ? settings.victoryMusic : settings.defeatMusic;
        if (clip == null)
        {
            Debug.Log($"[AudioManager] No {(isVictory ? "victory" : "defeat")} music assigned");
            return;
        }

        // Stop current music and play fanfare
        StopMusic();

        activeMusicSource.clip = clip;
        activeMusicSource.loop = false; // Don't loop victory/defeat music
        activeMusicSource.volume = settings.GetEffectiveVolume(SoundCategory.Music);
        activeMusicSource.Play();

        Debug.Log($"[AudioManager] Playing {(isVictory ? "victory" : "defeat")} music");
    }

    /// <summary>
    /// Crossfades from current music to new music.
    /// </summary>
    private void CrossfadeToMusic(AudioClip newClip)
    {
        if (crossfadeRoutine != null)
            StopCoroutine(crossfadeRoutine);

        crossfadeRoutine = StartCoroutine(CrossfadeRoutine(newClip));
    }

    private IEnumerator CrossfadeRoutine(AudioClip newClip)
    {
        var settings = AudioSettings.Instance;
        float duration = settings.musicCrossfadeDuration;
        float targetVolume = settings.GetEffectiveVolume(SoundCategory.Music);

        // Setup new music on inactive source
        inactiveMusicSource.clip = newClip;
        inactiveMusicSource.loop = settings.loopMusic;
        inactiveMusicSource.volume = 0f;

        if (newClip != null)
            inactiveMusicSource.Play();

        // Crossfade
        float elapsed = 0f;
        float startVolumeActive = activeMusicSource.volume;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            activeMusicSource.volume = Mathf.Lerp(startVolumeActive, 0f, easedT);
            inactiveMusicSource.volume = Mathf.Lerp(0f, targetVolume, easedT);

            yield return null;
        }

        // Swap sources
        activeMusicSource.Stop();
        var temp = activeMusicSource;
        activeMusicSource = inactiveMusicSource;
        inactiveMusicSource = temp;

        crossfadeRoutine = null;
    }

    /// <summary>
    /// Stops all music immediately.
    /// </summary>
    public void StopMusic()
    {
        if (crossfadeRoutine != null)
        {
            StopCoroutine(crossfadeRoutine);
            crossfadeRoutine = null;
        }

        musicSourceA.Stop();
        musicSourceB.Stop();
    }

    /// <summary>
    /// Sets music volume (updates currently playing music).
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        var settings = AudioSettings.Instance;
        if (settings != null)
        {
            settings.musicVolume = Mathf.Clamp01(volume);
            activeMusicSource.volume = settings.GetEffectiveVolume(SoundCategory.Music);
        }
    }

    // ========================================
    // SFX SYSTEM
    // ========================================

    /// <summary>
    /// Plays a sound effect (with optional pitch variation for variety).
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitchVariation = 0f)
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll || clip == null) return;

        // Check sound frequency limiting
        if (settings.reduceSoundFrequency)
        {
            string clipName = clip.name;
            if (lastPlayedTimes.TryGetValue(clipName, out float lastTime))
            {
                if (Time.time - lastTime < settings.minSoundInterval)
                    return;
            }
            lastPlayedTimes[clipName] = Time.time;
        }

        float effectiveVolume = volume * settings.GetEffectiveVolume(SoundCategory.SFX);

        if (pitchVariation > 0f)
        {
            sfxSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        }
        else
        {
            sfxSource.pitch = 1f;
        }

        sfxSource.PlayOneShot(clip, effectiveVolume);
    }

    /// <summary>
    /// Plays a random sound from an array.
    /// </summary>
    public void PlayRandomSFX(AudioClip[] clips, float volume = 1f, float pitchVariation = 0.05f)
    {
        var settings = AudioSettings.Instance;
        if (settings == null || clips == null || clips.Length == 0) return;

        AudioClip clip = settings.GetRandomClip(clips);
        PlaySFX(clip, volume, pitchVariation);
    }

    // ========================================
    // COMBAT SOUNDS
    // ========================================

    /// <summary>
    /// Plays weapon fire sound based on attack type.
    /// Called from UnitAttackEffects.PlayAttackEffect()
    /// </summary>
    public void PlayWeaponSound(AttackEffectType attackType)
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        AudioClip clip = settings.GetWeaponSound(attackType, out float volume);

        if (clip != null)
        {
            PlaySFX(clip, volume, 0.1f); // 10% pitch variation for variety
        }
    }

    /// <summary>
    /// Plays explosion sound (for unit death).
    /// Called from CombatVFXSpawner.SpawnDeathExplosion()
    /// </summary>
    public void PlayExplosion(bool isLarge = false)
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        if (isLarge)
        {
            PlayRandomSFX(settings.largeExplosionSounds, settings.largeExplosionVolume, 0.1f);
        }
        else
        {
            PlayRandomSFX(settings.smallExplosionSounds, settings.smallExplosionVolume, 0.1f);
        }
    }

    /// <summary>
    /// Legacy method for compatibility with existing SFXManager calls.
    /// </summary>
    public void PlayExplosion()
    {
        PlayExplosion(false);
    }

    /// <summary>
    /// Plays impact sound (bullet/projectile hit).
    /// </summary>
    public void PlayImpact()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlayRandomSFX(settings.impactSounds, settings.impactVolume, 0.15f);
    }

    /// <summary>
    /// Plays commander damage sound.
    /// </summary>
    public void PlayCommanderDamage()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlaySFX(settings.commanderDamageSound, settings.commanderDamageVolume);
    }

    /// <summary>
    /// Plays ability activation sound.
    /// </summary>
    public void PlayAbilityActivate()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlaySFX(settings.abilityActivateSound, settings.abilityActivateVolume);
    }

    /// <summary>
    /// Plays pre-emptive strike sound.
    /// </summary>
    public void PlayPreemptiveStrike()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlaySFX(settings.preemptiveStrikeSound, settings.preemptiveStrikeVolume);
    }

    /// <summary>
    /// Plays counterattack sound.
    /// </summary>
    public void PlayCounterattack()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlaySFX(settings.counterattackSound, settings.counterattackVolume);
    }

    // ========================================
    // UI SOUNDS
    // ========================================

    /// <summary>
    /// Plays card play/placed sound.
    /// </summary>
    public void PlayCardPlay()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlayUISound(settings.cardPlaySound, settings.cardPlayVolume);
    }

    /// <summary>
    /// Plays card draw sound.
    /// </summary>
    public void PlayCardDraw()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlayUISound(settings.cardDrawSound, settings.cardDrawVolume);
    }

    /// <summary>
    /// Plays card hover sound.
    /// </summary>
    public void PlayCardHover()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlayUISound(settings.cardHoverSound, settings.cardHoverVolume);
    }

    /// <summary>
    /// Plays button click sound.
    /// </summary>
    public void PlayButtonClick()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlayUISound(settings.buttonClickSound, settings.buttonClickVolume);
    }

    /// <summary>
    /// Plays gold received sound.
    /// </summary>
    public void PlayGoldReceived()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlayUISound(settings.goldReceivedSound, settings.goldReceivedVolume);
    }

    /// <summary>
    /// Plays round start fanfare.
    /// </summary>
    public void PlayRoundStart()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlaySFX(settings.roundStartSound, settings.roundStartVolume);
    }

    /// <summary>
    /// Plays combat start sting.
    /// </summary>
    public void PlayCombatStart()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll) return;

        PlaySFX(settings.combatStartSound, settings.combatStartVolume);
    }

    private void PlayUISound(AudioClip clip, float volume)
    {
        var settings = AudioSettings.Instance;
        if (settings == null || settings.muteAll || clip == null) return;

        float effectiveVolume = volume * settings.GetEffectiveVolume(SoundCategory.UI);
        uiSource.PlayOneShot(clip, effectiveVolume);
    }

    // ========================================
    // AMBIENCE
    // ========================================

    /// <summary>
    /// Starts battle ambience (looping background sounds).
    /// </summary>
    public void StartBattleAmbience()
    {
        var settings = AudioSettings.Instance;
        if (settings == null || !settings.enableBattleAmbience || settings.muteAll) return;

        if (settings.battleAmbience != null)
        {
            ambienceSource.clip = settings.battleAmbience;
            ambienceSource.volume = settings.battleAmbienceVolume * settings.GetEffectiveVolume(SoundCategory.SFX);
            ambienceSource.Play();
        }
    }

    /// <summary>
    /// Stops battle ambience.
    /// </summary>
    public void StopBattleAmbience()
    {
        ambienceSource.Stop();
    }

    // ========================================
    // EVENT HANDLERS
    // ========================================

    private void OnGameStateChanged(GameState previousState, GameState newState)
    {
        Debug.Log($"[AudioManager] State changed: {previousState} -> {newState}");

        // Handle ambience
        if (newState == GameState.Battle)
        {
            StartBattleAmbience();
        }
        else if (previousState == GameState.Battle)
        {
            StopBattleAmbience();
        }

        // Handle music (skip VictoryDefeat - handled by OnBattleEnded)
        if (newState != GameState.VictoryDefeat)
        {
            PlayMusicForState(newState);
        }
    }

    private void OnBattleEnded(bool isPlayerAWinner, int playerAHP, int playerAMaxHP)
    {
        Debug.Log($"[AudioManager] Battle ended - Victory: {isPlayerAWinner}");
        PlayVictoryDefeatMusic(isPlayerAWinner);
    }

    // ========================================
    // VOLUME CONTROL API
    // ========================================

    public void SetMasterVolume(float volume)
    {
        var settings = AudioSettings.Instance;
        if (settings != null)
        {
            settings.masterVolume = Mathf.Clamp01(volume);
            UpdateAllVolumes();
        }
    }

    public void SetSFXVolume(float volume)
    {
        var settings = AudioSettings.Instance;
        if (settings != null)
        {
            settings.sfxVolume = Mathf.Clamp01(volume);
        }
    }

    public void SetUIVolume(float volume)
    {
        var settings = AudioSettings.Instance;
        if (settings != null)
        {
            settings.uiVolume = Mathf.Clamp01(volume);
        }
    }

    public void ToggleMute()
    {
        var settings = AudioSettings.Instance;
        if (settings != null)
        {
            settings.muteAll = !settings.muteAll;
            UpdateAllVolumes();
        }
    }

    private void UpdateAllVolumes()
    {
        var settings = AudioSettings.Instance;
        if (settings == null) return;

        if (settings.muteAll)
        {
            activeMusicSource.volume = 0f;
            ambienceSource.volume = 0f;
        }
        else
        {
            activeMusicSource.volume = settings.GetEffectiveVolume(SoundCategory.Music);
            ambienceSource.volume = settings.battleAmbienceVolume * settings.GetEffectiveVolume(SoundCategory.SFX);
        }
    }
}
