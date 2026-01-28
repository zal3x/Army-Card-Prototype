using System.Collections;
using UnityEngine;

/// <summary>
/// Plays back a CombatReport's events in order using a coroutine.
/// Right now it just logs and waits; later youâ€™ll swap the log calls
/// for animations, VFX, UI updates, etc.
/// </summary>
public class CombatPlayback : MonoBehaviour
{   [Header("References")]
[Tooltip("Reference to FieldSpawner (for getting unit positions).")]
public FieldSpawner fieldSpawner;
    [Header("Playback")]
    [Tooltip("1 = normal speed. 2 = twice as fast. 0.5 = half speed.")]
    public float speedMultiplier = 1f;

    [Tooltip("Delay between most events (seconds).")]
    public float defaultDelay = 0.15f;

    [Tooltip("Extra delay after unit destruction (seconds).")]
    public float destroyDelay = 0.25f;

    [Tooltip("Extra delay after commander damage (seconds).")]
    public float commanderHitDelay = 0.35f;

    [Tooltip("If true, CombatPlayback prints each event as it plays.")]
    public bool logEvents = false;

    [Header("Debug")]
    [Tooltip("If true, logs a stack trace whenever Play() is called.")]
    public bool logPlayCallStack = false;
// Add these fields near the top of CombatPlayback class
private Player playerA;
private Player playerB;

// Add this public method to set the references
public void SetPlayers(Player a, Player b)
{
    playerA = a;
    playerB = b;
}
public BattleHUD battleHUD;
    private Coroutine playbackRoutine;

    public bool IsPlaying => playbackRoutine != null;

    private void OnDisable()
    {
        // Stop cleanly so we never leave a routine reference dangling.
        Stop();

        // Keep this light; disable/destroy happens normally when exiting play mode.
        if (logEvents)
    Debug.Log("[PLAYBACK] CombatPlayback disabled");

    }

    private void OnDestroy()
    {
        Stop();
        if (logEvents)
    Debug.Log("[PLAYBACK] CombatPlayback destroyed");

    }

    /// <summary>
    /// Starts playing a report. If something is already playing, it stops and restarts.
    /// </summary>
    public Coroutine Play(CombatReport report)
    {
        if (logPlayCallStack)
            Debug.LogWarning("[PLAYBACK] Play() CALLED FROM:\n" + System.Environment.StackTrace);

        if (report == null || report.Events == null)
{
    Debug.LogWarning("CombatPlayback.Play called with null report/events.");
    return null;
}
if (logEvents)
    Debug.Log($"[PLAYBACK] Starting playback. Events={report.Events.Count}");



        Stop();
        playbackRoutine = StartCoroutine(PlayRoutine(report));
        return playbackRoutine;
    }

    /// <summary>
    /// Stops current playback (if any).
    /// </summary>
    public void Stop()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }
    }

    private IEnumerator PlayRoutine(CombatReport report)
    {
        for (int i = 0; i < report.Events.Count; i++)
        {
            CombatEvent e = report.Events[i];

            // If object gets disabled, Unity will stop coroutines, but this keeps us safe if you ever change behavior.
            if (!isActiveAndEnabled)
            {
                playbackRoutine = null;
                yield break;
            }

            try
            {
                HandleEvent(e);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PLAYBACK] Exception during HandleEvent at index {i}/{report.Events.Count}: {ex}");
                playbackRoutine = null;
                yield break;
            }

            float delay = GetDelayForEvent(e);
            float denom = Mathf.Max(0.0001f, speedMultiplier);

            if (delay > 0f)
                yield return new WaitForSeconds(delay / denom);
            else
                yield return null;
        }

if (logEvents)
    Debug.Log("[PLAYBACK] Finished playback.");
        playbackRoutine = null;
    }

    private void HandleEvent(CombatEvent e)
{
    // ENHANCED DEBUG
    if (e.Type == CombatEventType.DamageAppliedToUnit && e.TargetUnit != null)
    {
        Debug.Log($"[DEBUG DAMAGE] Target={e.TargetUnit.Name} (ID={e.TargetUnit.InstanceId}), Amount={e.Amount}, IsMarked={e.TargetUnit.IsMarkedTarget}, Ability={e.TargetUnit.Ability}");
        
        Vector3 pos = fieldSpawner.GetUnitPosition(e.TargetUnit.InstanceId);
        Debug.Log($"[DEBUG POSITION] Position for ID {e.TargetUnit.InstanceId}: {pos}");
    }
    
    if (e.Type == CombatEventType.PreemptiveStrikeDeclared)
    {
        Debug.Log($"[DEBUG PRE] Note={e.Note}, SourceUnit={(e.SourceUnit != null ? e.SourceUnit.Name : "null")}, Amount={e.Amount}");
    }

    // Existing logging
    if (logEvents)
    {
        if (!string.IsNullOrEmpty(e.Note))
            Debug.Log($"[PLAY] {e.Note}");
        else
            Debug.Log($"[PLAY] {e}");
    }

    if (fieldSpawner == null)
    {
        Debug.LogError("[DEBUG] FieldSpawner is NULL!");
        return;
    }

    switch (e.Type)
    {
        case CombatEventType.DamageAppliedToUnit:
            if (e.TargetUnit != null && e.Amount > 0)
            fieldSpawner.UpdateUnitHealthBar(
    e.TargetUnit.InstanceId,
    e.TargetUnit.MaxHP,
    e.TargetUnit.HP,
    animate: true
);

            {
                Vector3 pos = fieldSpawner.GetUnitPosition(e.TargetUnit.InstanceId);
                if (pos == Vector3.zero)
                {
                    Debug.LogWarning($"[DEBUG] Position is ZERO for unit {e.TargetUnit.InstanceId}");
                    break;
                }

                DamageNumberType damageType = DamageNumberType.Normal;
                string suffix = "";
                string prefix = "";

                // IMPORTANT FIX: Check the event Note instead of current unit state
                // The IsMarkedTarget flag gets cleared during combat resolution,
                // so we need to detect marked hits from the event description
                bool wasMarkedHit = e.Note != null && e.Note.Contains("MARKED");
                bool isPreemptive = e.Note != null && e.Note.Contains("PRE-EMPTIVE");

                // PRE-EMPTIVE STRIKE: Red + "PRE" prefix (highest priority)
                if (isPreemptive)
                {
                    Debug.Log($"[DEBUG] PRE-EMPTIVE DETECTED (from event note)! Spawning red number");
                    damageType = DamageNumberType.PreemptiveStrike;
                    prefix = "PRE";
                }
                // MARKED TARGET: Orange + bigger + "!"
                else if (wasMarkedHit)
                {
                    Debug.Log($"[DEBUG] MARKED DETECTED (from event note)! Spawning orange number");
                    damageType = DamageNumberType.MarkedTarget;
                    suffix = "!";
                }
                // ARMORED TANK: Blue
                else if (e.TargetUnit.Ability == UnitAbilityType.ReduceIncomingDamage50Percent)
                {
                    Debug.Log($"[DEBUG] ARMOR DETECTED! Spawning blue number");
                    damageType = DamageNumberType.ArmorReduced;
                }
                else
                {
                    Debug.Log($"[DEBUG] NORMAL damage number");
                }

                Debug.Log($"[DEBUG SPAWN] Spawning {damageType} at {pos}");
                DamageNumberSpawner.Spawn(e.Amount, pos, damageType, prefix, suffix);
            }
            break;
        
       case CombatEventType.CommanderDamaged:
    if (battleHUD != null && e.TargetPlayer != null && e.Amount > 0)
    {
        Vector3 spawnPos = battleHUD.GetCommanderPosition(
            e.TargetPlayer, 
            playerA, 
            playerB
        );
        
        if (spawnPos != Vector3.zero)
        {
            DamageNumberSpawner.Spawn(
                e.Amount, 
                spawnPos, 
                DamageNumberType.CommanderDamage
            );
        }
    }
    break;

        case CombatEventType.SynergyActivated:
            // This handles double ATK abilities (RPG, 3-of-a-kind, Full Health bonus)
            if (e.SourceUnit != null && e.Amount >= 2)
            {
                Vector3 pos = Vector3.zero;
                
                // Try to get position from FieldSpawner
                if (fieldSpawner != null)
                {
                    pos = fieldSpawner.GetUnitPosition(e.SourceUnit.InstanceId);
                }
                
                // If we didn't get a valid position, try fallback
                if (pos == Vector3.zero)
                {
                    Debug.LogWarning($"[PLAYBACK] Could not find position for synergy source unit {e.SourceUnit.InstanceId} ({e.SourceUnit.Name})");
                    // Try to spawn at a default location based on player
                    if (e.SourcePlayer != null)
                    {
                        pos = e.SourcePlayer.Name == "Player A" 
                            ? new Vector3(-3f, 0f, 0f) 
                            : new Vector3(3f, 0f, 0f);
                    }
                    else
                    {
                        break; // No valid position, skip
                    }
                }

                // Spawn green "x2 ATK" text
                string multiplier = $"x{e.Amount}";
                Debug.Log($"[DEBUG] Synergy/ability ATK bonus detected! Unit={e.SourceUnit.Name}, Multiplier={multiplier}, Pos={pos}");
                DamageNumberSpawner.Spawn(0, pos, DamageNumberType.AbilityBonus, multiplier, "ATK");
            }
            break;
        
        case CombatEventType.UnitDestroyed:
    if (CombatVFXSpawner.Instance != null && e.TargetUnit != null)
    {
        CombatVFXSpawner.Instance.SpawnDeathExplosionForUnit(e.TargetUnit.InstanceId);
    }
    break;
        case CombatEventType.AbilityTriggered:
            if (e.SourceUnit != null)
            {
                Vector3 pos = Vector3.zero;
                
                // Try to get position from FieldSpawner
                if (fieldSpawner != null)
                {
                    pos = fieldSpawner.GetUnitPosition(e.SourceUnit.InstanceId);
                }
                
                // If we didn't get a valid position, log warning but don't break
                if (pos == Vector3.zero)
                {
                    Debug.LogWarning($"[PLAYBACK] Could not find position for ability source unit {e.SourceUnit.InstanceId} ({e.SourceUnit.Name})");
                    // Try to spawn at a default location based on player
                    if (e.SourcePlayer != null)
                    {
                        pos = e.SourcePlayer.Name == "Player A" 
                            ? new Vector3(-3f, 0f, 0f) 
                            : new Vector3(3f, 0f, 0f);
                    }
                    else
                    {
                        break; // No valid position, skip this one
                    }
                }

                // COUNTERATTACK: Purple "COUNTER X!"
                if (e.Note != null && e.Note.Contains("[COUNTER]"))
                {
                    Debug.Log($"[DEBUG] COUNTER detected! Amount={e.Amount}");
                    DamageNumberSpawner.Spawn(e.Amount, pos, DamageNumberType.Counterattack, "COUNTER", "!");
                }
                // x2 ATK ABILITIES: Green "x2 ATK" - check Amount OR look for multiplier in Note
                else if (e.Amount >= 2 || (e.Note != null && e.Note.Contains("double")))
                {
                    string multiplier = e.Amount >= 2 ? $"x{e.Amount}" : "x2";
                    Debug.Log($"[DEBUG] Ability bonus detected! Multiplier={multiplier}");
                    DamageNumberSpawner.Spawn(0, pos, DamageNumberType.AbilityBonus, multiplier, "ATK");
                }
            }
            break;

        case CombatEventType.PreemptiveStrikeDeclared:
            // Pre-emptive damage numbers are now spawned in DamageAppliedToUnit event
            // This event is just for logging the strike declaration
            break;
    }
}

// Helper method for commander position - uses BattleHUD if available
private Vector3 GetCommanderPosition(Player player)
{
    // Use BattleHUD to get commander position if available
    if (battleHUD != null)
    {
        Vector3 pos = battleHUD.GetCommanderPosition(player, playerA, playerB);
        if (pos != Vector3.zero) return pos;
    }
    
    // Fallback to fixed positions
    if (player.Name == "Player A")
        return new Vector3(-5f, 2f, 0f); // Left side, higher up
    else
        return new Vector3(5f, 2f, 0f);  // Right side, higher up
}
    private float GetDelayForEvent(CombatEvent e)
    {
        switch (e.Type)
        {
            case CombatEventType.UnitDestroyed:     return destroyDelay;
            case CombatEventType.CommanderDamaged:  return commanderHitDelay;
            case CombatEventType.CombatStart:
            case CombatEventType.CombatEnd:         return 0.05f;
            default:                                return defaultDelay;
        }
    }
}