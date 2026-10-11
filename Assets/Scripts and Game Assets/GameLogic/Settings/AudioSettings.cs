using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Global audio settings for music and sound effects.
/// Create via: Assets > Create > Settings/Audio Settings
/// Place in Resources folder for auto-loading.
/// </summary>
[CreateAssetMenu(fileName = "AudioSettings", menuName = "Settings/Audio Settings")]
public class AudioSettings : ScriptableObject
{
    // ========================================
    // VOLUME CONTROLS
    // ========================================
    [Header("━━━━━━━━━━ VOLUME CONTROLS ━━━━━━━━━━")]
    [Tooltip("Master volume (affects both music and SFX)")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Tooltip("Music volume multiplier")]
    [Range(0f, 1f)]
    public float musicVolume = 0.7f;

    [Tooltip("Sound effects volume multiplier")]
    [Range(0f, 1f)]
    public float sfxVolume = 0.8f;

    [Tooltip("UI sounds volume (button clicks, card interactions)")]
    [Range(0f, 1f)]
    public float uiVolume = 0.6f;

    // ========================================
    // MUSIC TRACKS
    // ========================================
    [Header("━━━━━━━━━━ MUSIC TRACKS ━━━━━━━━━━")]
    [Tooltip("Main menu / title screen music")]
    public AudioClip mainMenuMusic;

    [Tooltip("Overworld map music (WorldMap, WarCabinet, Barracks states)")]
    public AudioClip overworldMusic;

    [Tooltip("Battle/combat music (during buy phase and combat)")]
    public AudioClip battleMusic;

    [Tooltip("Victory fanfare (plays on battle win)")]
    public AudioClip victoryMusic;

    [Tooltip("Defeat music (plays on battle loss)")]
    public AudioClip defeatMusic;

    [Header("Music Behavior")]
    [Tooltip("Duration for music crossfade between states (seconds)")]
    [Range(0.5f, 3f)]
    public float musicCrossfadeDuration = 1.5f;

    [Tooltip("Should music loop? (Usually true for background music)")]
    public bool loopMusic = true;

    // ========================================
    // WEAPON SOUNDS
    // ========================================
    [Header("━━━━━━━━━━ WEAPON SOUNDS ━━━━━━━━━━")]
    [Tooltip("Generic gunfire sounds (rifles, pistols)")]
    public AudioClip[] gunshotSounds;
    [Range(0f, 1f)]
    public float gunshotVolume = 0.7f;

    [Tooltip("Heavy weapon sounds (cannons, tank shots)")]
    public AudioClip[] heavyWeaponSounds;
    [Range(0f, 1f)]
    public float heavyWeaponVolume = 0.8f;

    [Tooltip("Missile/rocket launch sounds")]
    public AudioClip[] missileLaunchSounds;
    [Range(0f, 1f)]
    public float missileLaunchVolume = 0.75f;

    [Tooltip("Aircraft attack sounds (jets, bombers)")]
    public AudioClip[] aircraftAttackSounds;
    [Range(0f, 1f)]
    public float aircraftAttackVolume = 0.7f;

    // ========================================
    // IMPACT & EXPLOSION SOUNDS
    // ========================================
    [Header("━━━━━━━━━━ IMPACT & EXPLOSIONS ━━━━━━━━━━")]
    [Tooltip("Small explosion sounds (unit deaths, impacts)")]
    public AudioClip[] smallExplosionSounds;
    [Range(0f, 1f)]
    public float smallExplosionVolume = 0.8f;

    [Tooltip("Large explosion sounds (vehicle/air unit deaths)")]
    public AudioClip[] largeExplosionSounds;
    [Range(0f, 1f)]
    public float largeExplosionVolume = 0.9f;

    [Tooltip("Impact/hit sounds (bullet impacts, damage taken)")]
    public AudioClip[] impactSounds;
    [Range(0f, 1f)]
    public float impactVolume = 0.6f;

    // ========================================
    // COMBAT FEEDBACK SOUNDS
    // ========================================
    [Header("━━━━━━━━━━ COMBAT FEEDBACK ━━━━━━━━━━")]
    [Tooltip("Commander taking damage sound")]
    public AudioClip commanderDamageSound;
    [Range(0f, 1f)]
    public float commanderDamageVolume = 0.9f;

    [Tooltip("Critical hit / big damage sound")]
    public AudioClip criticalHitSound;
    [Range(0f, 1f)]
    public float criticalHitVolume = 0.85f;

    [Tooltip("Ability activation sound (generic)")]
    public AudioClip abilityActivateSound;
    [Range(0f, 1f)]
    public float abilityActivateVolume = 0.7f;

    [Tooltip("Pre-emptive strike declaration sound")]
    public AudioClip preemptiveStrikeSound;
    [Range(0f, 1f)]
    public float preemptiveStrikeVolume = 0.8f;

    [Tooltip("Counterattack sound")]
    public AudioClip counterattackSound;
    [Range(0f, 1f)]
    public float counterattackVolume = 0.75f;

    // ========================================
    // CARD & UI SOUNDS
    // ========================================
    [Header("━━━━━━━━━━ CARD & UI SOUNDS ━━━━━━━━━━")]
    [Tooltip("Card played/placed sound")]
    public AudioClip cardPlaySound;
    [Range(0f, 1f)]
    public float cardPlayVolume = 0.6f;

    [Tooltip("Card drawn sound")]
    public AudioClip cardDrawSound;
    [Range(0f, 1f)]
    public float cardDrawVolume = 0.5f;

    [Tooltip("Card hover/select sound")]
    public AudioClip cardHoverSound;
    [Range(0f, 1f)]
    public float cardHoverVolume = 0.3f;

    [Tooltip("Button click sound")]
    public AudioClip buttonClickSound;
    [Range(0f, 1f)]
    public float buttonClickVolume = 0.5f;

    [Tooltip("Gold/currency received sound")]
    public AudioClip goldReceivedSound;
    [Range(0f, 1f)]
    public float goldReceivedVolume = 0.6f;

    [Tooltip("Round start fanfare/sting")]
    public AudioClip roundStartSound;
    [Range(0f, 1f)]
    public float roundStartVolume = 0.7f;

    [Tooltip("Combat start sting")]
    public AudioClip combatStartSound;
    [Range(0f, 1f)]
    public float combatStartVolume = 0.75f;

    // ========================================
    // SOUL SOUNDS
    // ========================================
    [Header("━━━━━━━━━━ SOUL SOUNDS ━━━━━━━━━━")]
    [Tooltip("Soul flying toward the progress bar (ghostly whoosh) - one picked at random")]
    public AudioClip[] soulFlightSounds;
    [Range(0f, 1f)]
    public float soulFlightVolume = 0.5f;

    [Tooltip("Soul landing on the progress bar (soft thump + ping) - one picked at random")]
    public AudioClip[] soulLandSounds;
    [Range(0f, 1f)]
    public float soulLandVolume = 0.6f;

    [Tooltip("Progress bar filling with souls (liquid bubbling) - one picked at random")]
    public AudioClip[] soulGurgleSounds;
    [Range(0f, 1f)]
    public float soulGurgleVolume = 0.55f;

    [Tooltip("Progress bar full / soul magic released (shimmering arpeggio) - one picked at random")]
    public AudioClip[] soulMagicSounds;
    [Range(0f, 1f)]
    public float soulMagicVolume = 0.75f;

    // ========================================
    // AMBIENT & ATMOSPHERE
    // ========================================
    [Header("━━━━━━━━━━ AMBIENT & ATMOSPHERE ━━━━━━━━━━")]
    [Tooltip("Battle ambient sounds (distant gunfire, radio chatter)")]
    public AudioClip battleAmbience;
    [Range(0f, 1f)]
    public float battleAmbienceVolume = 0.3f;

    [Tooltip("Enable ambient sounds during combat")]
    public bool enableBattleAmbience = true;

    // ========================================
    // ACCESSIBILITY
    // ========================================
    [Header("━━━━━━━━━━ ACCESSIBILITY ━━━━━━━━━━")]
    [Tooltip("Mute all sounds")]
    public bool muteAll = false;

    [Tooltip("Reduce sound frequency (for audio sensitivity)")]
    public bool reduceSoundFrequency = false;

    [Tooltip("Minimum delay between similar sounds (seconds) when reduce mode is on")]
    [Range(0.1f, 1f)]
    public float minSoundInterval = 0.2f;

    // ========================================
    // SINGLETON ACCESS
    // ========================================
    private static AudioSettings _instance;
    public static AudioSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<AudioSettings>("AudioSettings");
                if (_instance == null)
                {
                    Debug.LogWarning("[AudioSettings] No AudioSettings found in Resources folder. Using defaults.");
                    _instance = CreateInstance<AudioSettings>();
                }
            }
            return _instance;
        }
    }

    // ========================================
    // HELPER METHODS
    // ========================================

    /// <summary>
    /// Gets effective volume for a category (applies master volume).
    /// </summary>
    public float GetEffectiveVolume(SoundCategory category)
    {
        if (muteAll) return 0f;

        float categoryVolume = category switch
        {
            SoundCategory.Music => musicVolume,
            SoundCategory.SFX => sfxVolume,
            SoundCategory.UI => uiVolume,
            _ => 1f
        };

        return masterVolume * categoryVolume;
    }

    /// <summary>
    /// Gets a random clip from an array (for variety).
    /// </summary>
    public AudioClip GetRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }

    /// <summary>
    /// Gets attack sound based on AttackEffectType.
    /// </summary>
    public AudioClip GetWeaponSound(AttackEffectType attackType, out float volume)
    {
        volume = gunshotVolume;

        switch (attackType)
        {
            case AttackEffectType.Projectile:
                volume = missileLaunchVolume;
                return GetRandomClip(missileLaunchSounds) ?? GetRandomClip(gunshotSounds);

            case AttackEffectType.MuzzleFlash:
            case AttackEffectType.MuzzleFlashOnly:
            default:
                volume = gunshotVolume;
                return GetRandomClip(gunshotSounds);
        }
    }

    /// <summary>
    /// Gets appropriate music clip for a game state.
    /// </summary>
    public AudioClip GetMusicForState(GameState state)
    {
        return state switch
        {
            GameState.MainMenu => mainMenuMusic,
            GameState.Battle => battleMusic,
            GameState.VictoryDefeat => null, // Handled separately by victory/defeat
            GameState.WarCabinet => overworldMusic,
            GameState.WorldMap => overworldMusic,
            GameState.Barracks => overworldMusic,
            _ => null
        };
    }
}

/// <summary>
/// Sound categories for volume control.
/// </summary>
public enum SoundCategory
{
    Music,
    SFX,
    UI
}
