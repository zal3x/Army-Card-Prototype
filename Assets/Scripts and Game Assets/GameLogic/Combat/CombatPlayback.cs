using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays back a CombatReport's events visually using coroutines.
///
/// NEW COMBAT VISUAL FLOW (Feb 2026):
/// 1. Winner fires opening volley FIRST (higher total ATK)
/// 2. Loser fires back (brief delay)
/// 3. Brief pause
/// 4. Random explosions begin - loser's units start slightly earlier
/// 5. Synchronized finale - last 2 units explode together
/// 6. Commander damage display
///
/// All units explode/die (except those with PERSIST ability who won/tied).
/// </summary>
public class CombatPlayback : MonoBehaviour
{   [Header("References")]
[Tooltip("Reference to FieldSpawner (for getting unit positions).")]
public FieldSpawner fieldSpawner;
    [Header("Playback")]
    [Tooltip("Use global settings from CombatVisualSettings (recommended)")]
    public bool useGlobalSettings = true;

    [Tooltip("1 = normal speed. 2 = twice as fast. 0.5 = half speed. (Only used if not using global settings)")]
    public float speedMultiplier = 1f;

    [Tooltip("Delay between most events (seconds). (Only used if not using global settings)")]
    public float defaultDelay = 0.15f;

    [Tooltip("Extra delay after unit destruction (seconds). (Only used if not using global settings)")]
    public float destroyDelay = 0.25f;

    [Tooltip("Extra delay after commander damage (seconds). (Only used if not using global settings)")]
    public float commanderHitDelay = 0.35f;

    [Tooltip("If true, CombatPlayback prints each event as it plays.")]
    public bool logEvents = false;

    [Header("Debug")]
    [Tooltip("If true, logs a stack trace whenever Play() is called.")]
    public bool logPlayCallStack = false;
// Add these fields near the top of CombatPlayback class
private Player playerA;
private Player playerB;
private bool openingVolleyPlayed = false;  // Track if we've played the opening volley
private Coroutine continuousFiringRoutine;  // Background firing coroutine

// Interleaved explosion system (firepower-based ordering)
// Explosions trigger AS SOON as a row is wiped (all units of that type dead)
// Order: Weaker Infantry → Stronger Infantry → Weaker Vehicle → etc.
private List<CombatEvent> allDeathEvents = new List<CombatEvent>();
private List<CombatEvent> commanderDamageEvents = new List<CombatEvent>();
private List<CombatEvent> abilityTriggeredEvents = new List<CombatEvent>();
private bool explosionsPhaseStarted = false;
private int cachedFirepowerA = 0;
private int cachedFirepowerB = 0;

// Track death counts and row explosions for immediate triggering
private Dictionary<UnitType, int> startingCountsA = new Dictionary<UnitType, int>();
private Dictionary<UnitType, int> startingCountsB = new Dictionary<UnitType, int>();
private Dictionary<UnitType, int> deathCountsA = new Dictionary<UnitType, int>();
private Dictionary<UnitType, int> deathCountsB = new Dictionary<UnitType, int>();
private HashSet<UnitType> rowsExplodedWeaker = new HashSet<UnitType>();
private HashSet<UnitType> rowsExplodedStronger = new HashSet<UnitType>();
private HashSet<UnitType> rowsExplodingWeaker = new HashSet<UnitType>(); // Currently exploding
private HashSet<UnitType> rowsExplodingStronger = new HashSet<UnitType>();
private HashSet<int> explodedUnitIds = new HashSet<int>(); // Track which units have already exploded
private Player weakerPlayer = null;
private Player strongerPlayer = null;

// Staged commander damage - HP drops as rows explode
private int initialCommanderHPA = 0;
private int initialCommanderHPB = 0;
private int totalCommanderDamageToA = 0;
private int totalCommanderDamageToB = 0;
private int totalDeathsA = 0;
private int totalDeathsB = 0;

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

        // Always stop continuous firing
        StopContinuousFiring();

        // Clean up any lingering VFX objects
        UnitAttackEffects.CleanupAllVfx();

        // Reset state
        openingVolleyPlayed = false;

        // Reset interleaved explosion system state
        allDeathEvents.Clear();
        commanderDamageEvents.Clear();
        abilityTriggeredEvents.Clear();
        explosionsPhaseStarted = false;
        cachedFirepowerA = 0;
        cachedFirepowerB = 0;
 
        // Reset immediate row explosion tracking
        startingCountsA.Clear();
        startingCountsB.Clear();
        deathCountsA.Clear();
        deathCountsB.Clear();
        rowsExplodedWeaker.Clear();
        rowsExplodedStronger.Clear();
        rowsExplodingWeaker.Clear();
        rowsExplodingStronger.Clear();
        explodedUnitIds.Clear();
        weakerPlayer = null;
        strongerPlayer = null;

        // Reset staged commander damage tracking
        initialCommanderHPA = 0;
        initialCommanderHPB = 0;
        totalCommanderDamageToA = 0;
        totalCommanderDamageToB = 0;
        totalDeathsA = 0;
        totalDeathsB = 0;
    }

    private IEnumerator PlayRoutine(CombatReport report)
    {
        var settings = CombatVisualSettings.Instance;

        // Pause idle animations during combat for cleaner visuals
        PauseAllIdleAnimations();

        // Determine winner/loser for visual ordering (NEW COMBAT SYSTEM)
        Player winner = report.Winner;
        Player loser = (winner == playerA) ? playerB : (winner == playerB) ? playerA : null;

        // Collect all death events and commander damage events
        allDeathEvents.Clear();
        commanderDamageEvents.Clear();
        abilityTriggeredEvents.Clear();
        foreach (var evt in report.Events)
        {
            if (evt.Type == CombatEventType.UnitDestroyed)
                allDeathEvents.Add(evt);
            else if (evt.Type == CombatEventType.CommanderDamaged)
                commanderDamageEvents.Add(evt);
            else if (evt.Type == CombatEventType.AbilityTriggered)
                abilityTriggeredEvents.Add(evt);
        }

        if (logEvents)
        {
            Debug.Log($"[NEW COMBAT VISUAL] Winner: {winner?.Name ?? "TIE"}, Deaths: {allDeathEvents.Count}, CmdDmg: {commanderDamageEvents.Count}");
        }

        // Apply visual effects for unit abilities at combat start (conditions checked per ability)
        ApplyAbilityEffectsForPlayer(playerA, playerB);
        ApplyAbilityEffectsForPlayer(playerB, playerA);

        // ============================================
        // START CONTINUOUS FIRING IMMEDIATELY
        // Units fire throughout combat until they explode
        // ============================================
        if (settings != null && settings.continueFiringDuringDamage)
        {
            Debug.Log("[CONTINUOUS FIRE] Starting combat firing...");
            continuousFiringRoutine = StartCoroutine(ContinuousFiringRoutine());
        }

        // Fighting phase: units fire before explosions begin (configurable duration)
        float fightingDuration = settings != null ? settings.ApplySpeedMultiplier(settings.fightingPhaseDuration) : 0.5f;
        yield return new WaitForSeconds(fightingDuration);

        // ============================================
        // RANDOM EXPLOSIONS
        // Loser's units start exploding slightly earlier
        // ============================================
        Debug.Log($"[EXPLOSIONS] Starting random explosions - {allDeathEvents.Count} deaths");
        yield return StartCoroutine(PlayRandomExplosions(loser, winner));

        // ============================================
        // ALWAYS STOP CONTINUOUS FIRING AT END OF COMBAT
        // This ensures PERSIST units don't keep firing forever
        // ============================================
        StopContinuousFiring();

        // ============================================
        // PHASE 3b: ABILITY TRIGGERED EVENTS
        // (e.g. Counterattack dealing commander damage on unit death)
        // ============================================
        foreach (var abilityEvt in abilityTriggeredEvents)
        {
            HandleEvent(abilityEvt);
            yield return null; // one frame gap between ability events
        }

        // ============================================
        // PHASE 4: COMMANDER DAMAGE DISPLAY
        // ============================================
        if (commanderDamageEvents.Count > 0)
        {
            Debug.Log("[COMMANDER DAMAGE] Showing damage numbers");

            foreach (var dmgEvent in commanderDamageEvents)
            {
                if (dmgEvent.TargetPlayer != null && dmgEvent.Amount > 0)
                {
                    Vector3 cmdPos = (dmgEvent.TargetPlayer == playerA)
                        ? (battleHUD?.GetCommanderAPosition() ?? Vector3.zero)
                        : (battleHUD?.GetCommanderBPosition() ?? Vector3.zero);

                    if (cmdPos != Vector3.zero)
                    {
                        DamageNumberSpawner.Spawn(dmgEvent.Amount, cmdPos, DamageNumberType.CommanderDamage);
                    }

                    // Camera shake
                    if (CameraShake.Instance != null)
                    {
                        CameraShake.Instance.Shake(0.8f, 0.3f);
                    }

                    // Sound
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayCommanderDamage();
                    }
                }
            }

            // Update commander HP display
            if (battleHUD != null)
            {
                battleHUD.UpdateBothCommanders(playerA, playerB);
            }
        }

        // Wait for any in-flight projectiles to reach their targets before cleaning up
        float flightBuffer = settings != null ? settings.ApplySpeedMultiplier(settings.projectileFlightBuffer) : 1.0f;
        if (flightBuffer > 0f)
            yield return new WaitForSeconds(flightBuffer);

        // Clean up any lingering VFX objects
        UnitAttackEffects.CleanupAllVfx();

        // Resume idle animations on surviving units
        ResumeAllIdleAnimations();

        // Final safety: ensure firing is stopped (in case of early exit or PERSIST units)
        StopContinuousFiring();

        // Clear all unit visual effects now that combat is over
        UnitEffectManager.ClearAllEffects();

        if (logEvents)
            Debug.Log("[PLAYBACK] Finished playback.");
        playbackRoutine = null;
    }

    private void ApplyAbilityEffectsForPlayer(Player player, Player enemy)
    {
        if (player == null) return;

        foreach (var rowKvp in player.Rows)
        {
            RowType rowType = rowKvp.Key;
            var row = rowKvp.Value;

            // Build alive-count-by-name for this row (needed by ThreeOfAKind etc.)
            var aliveCountByName = new Dictionary<string, int>();
            foreach (var u in row.Units)
            {
                if (u == null || !u.IsAlive) continue;
                if (!aliveCountByName.ContainsKey(u.Name))
                    aliveCountByName[u.Name] = 0;
                aliveCountByName[u.Name]++;
            }

            foreach (var unit in row.Units)
            {
                if (unit == null || !unit.IsAlive || unit.Ability == UnitAbilityType.None)
                    continue;

                if (IsConditionalAbility(unit.Ability))
                {
                    // Only show effect when the ability condition is actually met
                    AbilitySystem.GetEffectiveAttackForBuyPhase(
                        unit, player, enemy, rowType, aliveCountByName,
                        out AbilityActivationInfo activation);

                    if (activation.AbilityActivated)
                    {
                        Debug.Log($"[UnitEffect] CONDITIONAL '{unit.Ability}' ACTIVATED on {player.Name}'s {unit.Name} (id={unit.InstanceId}). Reason: {activation.Reason}");
                        UnitEffectManager.ApplyEffect(unit.Ability.ToString(), unit.InstanceId);
                    }
                    else
                    {
                        Debug.Log($"[UnitEffect] CONDITIONAL '{unit.Ability}' NOT met for {player.Name}'s {unit.Name} (id={unit.InstanceId}) - no effect shown.");
                    }
                }
                else if (IsAlwaysOnAbility(unit.Ability))
                {
                    // Persist, Counterattack, Armored, DirectAttack - always visible
                    Debug.Log($"[UnitEffect] ALWAYS-ON '{unit.Ability}' applied to {player.Name}'s {unit.Name} (id={unit.InstanceId}).");
                    UnitEffectManager.ApplyEffect(unit.Ability.ToString(), unit.InstanceId);
                }
                else
                {
                    // On-play abilities (MrMoneybags, Duplicate, TargetMark, Taunt, etc.) skip - already fired at buy time
                    Debug.Log($"[UnitEffect] SKIPPING on-play ability '{unit.Ability}' on {player.Name}'s {unit.Name} - not a combat-phase effect.");
                }
            }
        }
    }

    private static bool IsConditionalAbility(UnitAbilityType ability)
    {
        switch (ability)
        {
            case UnitAbilityType.ThreeOfAKind:
            case UnitAbilityType.Unmatched:
            case UnitAbilityType.ArmorPiercing:
            case UnitAbilityType.SOS:
            case UnitAbilityType.BrotherInArms:
                return true;
            default:
                return false;
        }
    }

    private static bool IsAlwaysOnAbility(UnitAbilityType ability)
    {
        switch (ability)
        {
            case UnitAbilityType.Persist:
            case UnitAbilityType.Counterattack:
            case UnitAbilityType.Armored:
            case UnitAbilityType.DirectAttack:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Plays random explosions with loser starting slightly earlier.
    /// Last 2 units explode together for synchronized finale.
    /// </summary>
    private IEnumerator PlayRandomExplosions(Player loser, Player winner)
    {
        if (allDeathEvents.Count == 0) yield break;

        var settings = CombatVisualSettings.Instance;
        float explosionDuration = settings != null ? settings.deathExplosionDuration : 0.5f;

        // Separate deaths by player
        var loserDeaths = new List<CombatEvent>();
        var winnerDeaths = new List<CombatEvent>();

        foreach (var e in allDeathEvents)
        {
            if (e.TargetPlayer == loser)
                loserDeaths.Add(e);
            else if (e.TargetPlayer == winner)
                winnerDeaths.Add(e);
            else if (loser == null)
            {
                // Tie - split evenly
                if (e.TargetPlayer == playerA)
                    loserDeaths.Add(e);
                else
                    winnerDeaths.Add(e);
            }
        }

        // Shuffle both lists for randomness
        ShuffleList(loserDeaths);
        ShuffleList(winnerDeaths);

        if (logEvents)
            Debug.Log($"[EXPLOSIONS] Loser deaths: {loserDeaths.Count}, Winner deaths: {winnerDeaths.Count}");

        // Reserve last unit from each side for synchronized finale (if available)
        CombatEvent lastLoserDeath = loserDeaths.Count > 0 ? loserDeaths[loserDeaths.Count - 1] : null;
        CombatEvent lastWinnerDeath = winnerDeaths.Count > 0 ? winnerDeaths[winnerDeaths.Count - 1] : null;

        if (lastLoserDeath != null) loserDeaths.RemoveAt(loserDeaths.Count - 1);
        if (lastWinnerDeath != null) winnerDeaths.RemoveAt(winnerDeaths.Count - 1);

        // Loser gets a head start (0.3s)
        float loserTime = 0f;
        float winnerTime = 0.3f;

        int loserIndex = 0;
        int winnerIndex = 0;

        // Interleave explosions randomly
        while (loserIndex < loserDeaths.Count || winnerIndex < winnerDeaths.Count)
        {
            // Decide which side explodes next based on accumulated time
            bool pickLoser = (loserTime <= winnerTime && loserIndex < loserDeaths.Count) ||
                             (winnerIndex >= winnerDeaths.Count);

            if (pickLoser && loserIndex < loserDeaths.Count)
            {
                var e = loserDeaths[loserIndex++];
                yield return StartCoroutine(ExplodeAfterMarkAnimation(e));
                loserTime += Random.Range(0.1f, 0.25f);
            }
            else if (winnerIndex < winnerDeaths.Count)
            {
                var e = winnerDeaths[winnerIndex++];
                yield return StartCoroutine(ExplodeAfterMarkAnimation(e));
                winnerTime += Random.Range(0.1f, 0.25f);
            }

            yield return new WaitForSeconds(settings != null ? settings.ApplySpeedMultiplier(settings.explosionIntervalDelay) : 0.15f);
        }

        // Brief pause before finale
        yield return new WaitForSeconds(0.2f);

        // FINALE: Commander assault (if there's commander damage and a clear winner),
        // otherwise synchronized explosion of last 2 units
        bool hasCommanderDamage = commanderDamageEvents.Count > 0;
        bool hasClearWinner = winner != null && loser != null;

        if (hasCommanderDamage && hasClearWinner && (lastWinnerDeath != null || lastLoserDeath != null))
        {
            Debug.Log("[COMMANDER ASSAULT] Starting assault finale!");
            yield return StartCoroutine(PlayCommanderAssaultFinale(lastWinnerDeath, lastLoserDeath, winner, loser));
        }
        else
        {
            // No commander damage or tie - synchronized explosion
            Debug.Log("[EXPLOSIONS] Synchronized finale!");
            if (lastLoserDeath != null) yield return StartCoroutine(ExplodeAfterMarkAnimation(lastLoserDeath));
            if (lastWinnerDeath != null) yield return StartCoroutine(ExplodeAfterMarkAnimation(lastWinnerDeath));
            yield return new WaitForSeconds(explosionDuration);
        }
    }

    /// <summary>
    /// Commander assault finale: last winning unit fires at enemy commander while last losing unit
    /// fires back at it (last stand). Then all remaining units explode simultaneously.
    /// </summary>
    private IEnumerator PlayCommanderAssaultFinale(CombatEvent winnerUnitDeath, CombatEvent loserUnitDeath, Player winner, Player loser)
    {
        var settings = CombatVisualSettings.Instance;
        float duration = settings != null ? settings.ApplySpeedMultiplier(settings.commanderAssaultDuration) : 1.5f;
        float fireInterval = settings != null ? settings.ApplySpeedMultiplier(settings.continuousFiringInterval) : 0.4f;
        float explosionDuration = settings != null ? settings.deathExplosionDuration : 0.5f;

        // NOTE: We intentionally keep continuous firing running here.
        // PERSIST units still have visuals and will keep firing via ContinuousFiringRoutine.
        // The assault loop adds targeted shots for the reserved winner/loser units on top.
        // Firing is stopped at the end of PlayRoutine after commander damage is shown.

        // Get commander position of the loser (who takes the damage)
        Vector3 loserCommanderPos = (loser == playerA)
            ? (battleHUD?.GetCommanderAPosition() ?? Vector3.zero)
            : (battleHUD?.GetCommanderBPosition() ?? Vector3.zero);

        int winnerUnitId = winnerUnitDeath?.TargetUnit?.InstanceId ?? -1;
        int loserUnitId = loserUnitDeath?.TargetUnit?.InstanceId ?? -1;

        Debug.Log($"[COMMANDER ASSAULT] {winner.Name}'s last unit ({winnerUnitDeath?.TargetUnit?.Name}) fires at {loser.Name}'s commander for {duration}s");
        if (loserUnitDeath != null)
            Debug.Log($"[COMMANDER ASSAULT] {loser.Name}'s last unit ({loserUnitDeath.TargetUnit?.Name}) fires back!");

        // Assault loop: winner fires at commander, loser fires at winner
        float elapsed = 0f;
        while (elapsed < duration)
        {
            // Winner's last unit fires at enemy commander
            if (winnerUnitId >= 0 && loserCommanderPos != Vector3.zero)
                fieldSpawner.PlayUnitAttackEffectsAtPosition(winnerUnitId, loserCommanderPos);

            // Loser's last unit fires at winner's last unit (last stand)
            if (loserUnitId >= 0 && winnerUnitId >= 0)
                fieldSpawner.PlayUnitAttackEffects(loserUnitId, winnerUnitId);

            yield return new WaitForSeconds(fireInterval);
            elapsed += fireInterval;
        }

        // Camera shake before the simultaneous final explosion
        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.8f, 0.4f);

        // All remaining reserved units explode simultaneously
        Debug.Log("[COMMANDER ASSAULT] Simultaneous final explosion!");
        if (loserUnitDeath != null) yield return StartCoroutine(ExplodeAfterMarkAnimation(loserUnitDeath));
        if (winnerUnitDeath != null) yield return StartCoroutine(ExplodeAfterMarkAnimation(winnerUnitDeath));

        yield return new WaitForSeconds(explosionDuration);
    }

    /// <summary>
    /// For units marked by MarkTarget: plays the mark animation first, then explodes.
    /// For all other units: explodes immediately (same as ExplodeUnitImmediate).
    /// </summary>
    private IEnumerator ExplodeAfterMarkAnimation(CombatEvent e)
    {
        // Check the event note — IsMarkedTarget gets cleared during cleanup before playback runs
        bool isMarked = e.Note != null && e.Note.Contains("[MARKED]");

        if (e.TargetUnit != null && isMarked)
        {
            var settings = CombatVisualSettings.Instance;
            if (settings != null && settings.enableMarkTargetEffect &&
                settings.markTargetEffectFrames != null && settings.markTargetEffectFrames.Length > 0)
            {
                GameObject unitVisual = fieldSpawner?.GetUnitVisual(e.TargetUnit.InstanceId);
                if (unitVisual != null)
                {
                    // Destroy any existing persistent mark indicator before playing the death animation
                    var existing = unitVisual.GetComponentInChildren<MarkTargetEffect>();
                    if (existing != null) Destroy(existing.gameObject);

                    // Spawn fresh mark animation (plays once, destroys itself)
                    var effect = MarkTargetEffect.SpawnOnUnit(unitVisual);

                    // Wait for the animation to complete before exploding
                    float animDuration = (float)settings.markTargetEffectFrames.Length / settings.markTargetEffectFPS;
                    yield return new WaitForSeconds(settings.ApplySpeedMultiplier(animDuration));
                }
            }
        }

        ExplodeUnitImmediate(e);
    }

    /// <summary>
    /// Immediately explodes a unit (triggers VFX and destroys visual).
    /// </summary>
    private void ExplodeUnitImmediate(CombatEvent e)
    {
        if (e.TargetUnit == null) return;

        int unitId = e.TargetUnit.InstanceId;

        // Check if already exploded
        if (explodedUnitIds.Contains(unitId)) return;
        explodedUnitIds.Add(unitId);

        // Spawn explosion VFX
        if (CombatVFXSpawner.Instance != null)
        {
            CombatVFXSpawner.Instance.SpawnDeathExplosionForUnit(unitId);
        }

        // Destroy the unit visual
        if (fieldSpawner != null)
        {
            fieldSpawner.DestroyUnitVisual(unitId);
        }

        if (logEvents)
            Debug.Log($"[EXPLOSION] {e.TargetPlayer?.Name}'s {e.TargetUnit.Name} exploded");
    }

    /// <summary>
    /// Shuffles a list in place using Fisher-Yates algorithm.
    /// </summary>
    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = list[i];
            list[i] = list[j];
            list[j] = temp;
        }
    }

    // LEGACY: Old interleaved explosion code (kept for reference)
    private IEnumerator PlayRoutine_Legacy(CombatReport report)
    {
        openingVolleyPlayed = false;  // Reset for each combat

        // Initialize interleaved explosion tracking from report data
        var settings = CombatVisualSettings.Instance;
        if (settings != null && settings.useInterleavedExplosions)
        {
            // Cache firepower and determine weaker/stronger player
            cachedFirepowerA = report.TotalFirepowerA;
            cachedFirepowerB = report.TotalFirepowerB;
            var (weaker, stronger) = DetermineExplosionOrder(cachedFirepowerA, cachedFirepowerB);
            weakerPlayer = weaker;
            strongerPlayer = stronger;

            // Copy starting counts from report
            startingCountsA = new Dictionary<UnitType, int>(report.StartingCountsA);
            startingCountsB = new Dictionary<UnitType, int>(report.StartingCountsB);

            // Initialize death count tracking
            deathCountsA = new Dictionary<UnitType, int>
            {
                { UnitType.Infantry, 0 }, { UnitType.Vehicle, 0 },
                { UnitType.Air, 0 }, { UnitType.Ranged, 0 }
            };
            deathCountsB = new Dictionary<UnitType, int>
            {
                { UnitType.Infantry, 0 }, { UnitType.Vehicle, 0 },
                { UnitType.Air, 0 }, { UnitType.Ranged, 0 }
            };

            // Mark empty rows as already "exploded" (nothing to wait for)
            foreach (UnitType ut in new[] { UnitType.Infantry, UnitType.Vehicle, UnitType.Air, UnitType.Ranged })
            {
                int weakerCount = (weakerPlayer == playerA) ? GetStartingCount(startingCountsA, ut) : GetStartingCount(startingCountsB, ut);
                int strongerCount = (strongerPlayer == playerA) ? GetStartingCount(startingCountsA, ut) : GetStartingCount(startingCountsB, ut);

                if (weakerCount == 0) rowsExplodedWeaker.Add(ut);
                if (strongerCount == 0) rowsExplodedStronger.Add(ut);
            }

            // Initialize staged commander damage tracking
            // Scan events to count total deaths and commander damage
            totalDeathsA = 0;
            totalDeathsB = 0;
            totalCommanderDamageToA = 0;
            totalCommanderDamageToB = 0;

            foreach (var evt in report.Events)
            {
                if (evt.Type == CombatEventType.UnitDestroyed && evt.TargetPlayer != null)
                {
                    if (evt.TargetPlayer == playerA) totalDeathsA++;
                    else if (evt.TargetPlayer == playerB) totalDeathsB++;
                }
                else if (evt.Type == CombatEventType.CommanderDamaged && evt.TargetPlayer != null)
                {
                    if (evt.TargetPlayer == playerA) totalCommanderDamageToA += evt.Amount;
                    else if (evt.TargetPlayer == playerB) totalCommanderDamageToB += evt.Amount;
                }
            }

            // Calculate initial HP (before combat damage was applied)
            // CombatResolver has already modified CommanderHP, so we reverse-calculate
            initialCommanderHPA = (playerA != null ? playerA.CommanderHP : 0) + totalCommanderDamageToA;
            initialCommanderHPB = (playerB != null ? playerB.CommanderHP : 0) + totalCommanderDamageToB;

            if (logEvents)
            {
                Debug.Log($"[INTERLEAVED] Initialized - Weaker: {weakerPlayer?.Name} ({(weakerPlayer == playerA ? cachedFirepowerA : cachedFirepowerB)} FP), Stronger: {strongerPlayer?.Name}");
                Debug.Log($"[STAGED DAMAGE] Deaths: A={totalDeathsA}, B={totalDeathsB}, CmdDmg: A={totalCommanderDamageToA}, B={totalCommanderDamageToB}");
            }
        }

        // Pause idle animations during combat for cleaner visuals
        PauseAllIdleAnimations();

        for (int i = 0; i < report.Events.Count; i++)
        {
            CombatEvent e = report.Events[i];

            // If object gets disabled, Unity will stop coroutines, but this keeps us safe if you ever change behavior.
            if (!isActiveAndEnabled)
            {
                playbackRoutine = null;
                yield break;
            }

            // OPENING VOLLEY: Before first damage event (unit OR commander), trigger all units to fire
            // This ensures units fire even when the enemy has no units (firing at commander)
            if (!openingVolleyPlayed && IsDamageEvent(e))
            {
                if (settings != null && settings.enableOpeningVolley)
                {
                    Debug.Log("[OPENING VOLLEY] All units firing!");
                    yield return StartCoroutine(PlayOpeningVolley());
                }
                openingVolleyPlayed = true;
            }

            // Before CombatEnd, wait for explosions and play commander finale
            if (e.Type == CombatEventType.CombatEnd)
            {
                var combatEndSettings = CombatVisualSettings.Instance;

                if (logEvents)
                    Debug.Log($"[INTERLEAVED] CombatEnd - forcing all remaining explosions...");

                // Wait for any in-progress row explosions to complete first
                while (rowsExplodingWeaker.Count > 0 || rowsExplodingStronger.Count > 0)
                {
                    yield return null;
                }

                // Force-trigger ALL remaining deaths in order (weaker first, by row priority)
                // This handles partial row clears and any deaths that couldn't trigger due to prerequisites
                yield return StartCoroutine(ForceRemainingExplosions());

                // Small delay before commander finale
                if (combatEndSettings != null && combatEndSettings.preExplosionDelay > 0)
                {
                    yield return new WaitForSeconds(combatEndSettings.ApplySpeedMultiplier(combatEndSettings.preExplosionDelay));
                }

                // Stop continuous firing before finale
                if (continuousFiringRoutine != null)
                {
                    StopCoroutine(continuousFiringRoutine);
                    continuousFiringRoutine = null;
                }

                // Play commander finale (surviving units fire at enemy commander)
                yield return StartCoroutine(PlayCommanderFinale());

                // Cleanup interleaved system state
                allDeathEvents.Clear();
                commanderDamageEvents.Clear();
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

        // Stop continuous firing when combat ends
        if (continuousFiringRoutine != null)
        {
            StopCoroutine(continuousFiringRoutine);
            continuousFiringRoutine = null;
            Debug.Log("[CONTINUOUS FIRE] Stopped background firing (combat ended)");
        }

        // Clean up any lingering VFX objects (projectiles, muzzle flashes, impacts)
        // This prevents orphaned VFX when units are destroyed before their coroutines finish
        UnitAttackEffects.CleanupAllVfx();

        // Resume idle animations on surviving units
        ResumeAllIdleAnimations();

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
            {
                // DEBUG: Check if SourceUnit exists
                Debug.Log($"[ATTACK DEBUG] Damage event - SourceUnit: {(e.SourceUnit != null ? e.SourceUnit.Name + " (ID:" + e.SourceUnit.InstanceId + ")" : "NULL")}, TargetUnit: {e.TargetUnit.Name} (ID:{e.TargetUnit.InstanceId}), Amount: {e.Amount}");

                // Determine if we should show individual attack effects
                bool isPreemptive = !string.IsNullOrEmpty(e.Note) && e.Note.Contains("[PRE-EMPTIVE]");
                bool shouldShowAttack = isPreemptive; // Always show pre-emptive attacks

                // For main combat, DISABLE individual attacks if continuous firing is enabled
                // (the background coroutine handles all unit firing instead)
                if (!isPreemptive && openingVolleyPlayed)
                {
                    var settings = CombatVisualSettings.Instance;
                    // Only show individual attacks if continuous firing is DISABLED
                    shouldShowAttack = settings != null && !settings.continueFiringDuringDamage;
                }

                // Trigger attack effects from source unit (muzzle flash, projectile)
                if (shouldShowAttack && e.SourceUnit != null)
                {
                    Debug.Log($"[ATTACK DEBUG] Calling PlayUnitAttackEffects for attacker {e.SourceUnit.InstanceId}");
                    fieldSpawner.PlayUnitAttackEffects(e.SourceUnit.InstanceId, e.TargetUnit.InstanceId);
                }
                else if (!shouldShowAttack && !isPreemptive)
                {
                    Debug.Log($"[ATTACK DEBUG] Skipping individual attack (continuous firing handling all attacks)");
                }
                else if (e.SourceUnit == null)
                {
                    Debug.LogWarning($"[ATTACK DEBUG] SourceUnit is NULL - cannot trigger attack effects for damage to {e.TargetUnit.Name}");
                }

                // Update health bar with animation
                fieldSpawner.UpdateUnitHealthBar(
                    e.TargetUnit.InstanceId,
                    e.TargetUnit.MaxHP,
                    e.TargetUnit.HP,
                    animate: true
                );

                // Trigger damage visual effects (flash, shake, camera shake)
                fieldSpawner.PlayUnitDamageEffects(e.TargetUnit.InstanceId, e.Amount);

                // UNIFIED HEALTH SYSTEM: Update commander HP display
                // With interleaved explosions, commander HP only updates during finale
                var damageSettings = CombatVisualSettings.Instance;
                bool deferCommanderUpdate = damageSettings != null && damageSettings.useInterleavedExplosions;

                if (!deferCommanderUpdate && battleHUD != null && e.TargetPlayer != null)
                {
                    // Legacy: refresh display immediately
                    battleHUD.UpdateBothCommanders(playerA, playerB);
                }
            }

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
                else if (e.TargetUnit.Ability == UnitAbilityType.Armored)
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
    // Track commander damage for finale (if using interleaved explosions)
    var cmdSettings = CombatVisualSettings.Instance;
    bool useCommanderFinale = cmdSettings != null &&
                               cmdSettings.useInterleavedExplosions &&
                               cmdSettings.enableCommanderFinale;

    if (useCommanderFinale)
    {
        // Queue for finale - damage number will show after firing animation
        commanderDamageEvents.Add(e);
        if (logEvents)
            Debug.Log($"[COMMANDER FINALE] Queued commander damage: {e.Amount} to {e.TargetPlayer?.Name}");
    }
    else if (battleHUD != null && e.TargetPlayer != null && e.Amount > 0)
    {
        // Immediate display (legacy behavior)
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

        // Exaggerated camera shake for commander damage - this is the big moment!
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.8f, 0.3f);
        }

        // Play commander damage sound
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayCommanderDamage();
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
            // Interleaved explosion system - trigger explosions when row is cleared
            if (e.TargetUnit != null && e.TargetPlayer != null)
            {
                UnitType unitType = e.TargetUnit.Type;

                // Queue death for this row
                allDeathEvents.Add(e);

                // Increment death count for this player/type
                bool isPlayerA = (e.TargetPlayer == playerA);
                var deathCounts = isPlayerA ? deathCountsA : deathCountsB;
                if (!deathCounts.ContainsKey(unitType)) deathCounts[unitType] = 0;
                deathCounts[unitType]++;

                int deathCount = deathCounts[unitType];
                var startingCounts = isPlayerA ? startingCountsA : startingCountsB;
                int startingCount = GetStartingCount(startingCounts, unitType);

                if (logEvents)
                    Debug.Log($"[INTERLEAVED] Death: {e.TargetPlayer.Name}'s {e.TargetUnit.Name} ({unitType}) - {deathCount}/{startingCount}");

                // Check if this row is now cleared (all units dead)
                if (deathCount >= startingCount && startingCount > 0)
                {
                    if (logEvents)
                        Debug.Log($"[INTERLEAVED] Row cleared: {e.TargetPlayer.Name}'s {unitType}!");

                    // Try to trigger explosion for this row (checks prerequisites)
                    TryTriggerRowExplosion(e.TargetPlayer, unitType);
                }
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

                // COUNTERATTACK: Purple "COUNTER X!" + sprite animation over enemy commander
                if (e.Note != null && e.Note.Contains("[COUNTERATTACK]"))
                {
                    Debug.Log($"[DEBUG] COUNTER detected! Amount={e.Amount}");
                    DamageNumberSpawner.Spawn(e.Amount, pos, DamageNumberType.Counterattack, "COUNTER", "!");

                    // Spawn counterattack sprite animation over the enemy commander portrait.
                    if (e.TargetPlayer != null && battleHUD != null)
                    {
                        Vector3 portraitPos = battleHUD.GetCommanderPortraitPosition(e.TargetPlayer, playerA, playerB);

                        if (portraitPos == Vector3.zero)
                        {
                            // Fallback to panel center if portrait lookup failed
                            portraitPos = (e.TargetPlayer == playerA)
                                ? battleHUD.GetCommanderAPosition()
                                : battleHUD.GetCommanderBPosition();
                        }

                        if (portraitPos != Vector3.zero)
                            CounterattackEffect.SpawnAtPosition(portraitPos);
                    }

                    // Play counterattack sound
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayCounterattack();
                    }
                }
                // x2 ATK ABILITIES: Green "x2 ATK" - check Amount OR look for multiplier in Note
                else if (e.Amount >= 2 || (e.Note != null && e.Note.Contains("double")))
                {
                    string multiplier = e.Amount >= 2 ? $"x{e.Amount}" : "x2";
                    Debug.Log($"[DEBUG] Ability bonus detected! Multiplier={multiplier}");
                    DamageNumberSpawner.Spawn(0, pos, DamageNumberType.AbilityBonus, multiplier, "ATK");

                    // Play ability activation sound
                    if (AudioManager.Instance != null)
                    {
                        AudioManager.Instance.PlayAbilityActivate();
                    }
                }
            }
            break;

        case CombatEventType.PreemptiveStrikeDeclared:
            // Trigger attack effects for pre-emptive strikes (these HAVE SourceUnit)
            if (e.SourceUnit != null && e.TargetUnit != null)
            {
                Debug.Log($"[ATTACK DEBUG] Pre-emptive strike - SourceUnit: {e.SourceUnit.Name} (ID:{e.SourceUnit.InstanceId}), TargetUnit: {e.TargetUnit.Name}");
                fieldSpawner.PlayUnitAttackEffects(e.SourceUnit.InstanceId, e.TargetUnit.InstanceId);

                // Play pre-emptive strike sound
                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayPreemptiveStrike();
                }
            }
            break;

        case CombatEventType.CombatStart:
            // Play combat start sound
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCombatStart();
            }
            break;

        case CombatEventType.CombatEnd:
            // UNIFIED HEALTH SYSTEM: Sync commander health state at end of combat
            // This shrinks the health bar to reflect only surviving units
            if (battleHUD != null && playerA != null && playerB != null)
            {
                battleHUD.SyncCommanderHealthState(playerA, playerB);
                Debug.Log($"[PLAYBACK] Combat ended - synced commander health states (A: {playerA.CommanderHP}/{playerA.CommanderMaxHP}, B: {playerB.CommanderHP}/{playerB.CommanderMaxHP})");
            }
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
        // Use global settings if enabled
        if (useGlobalSettings && CombatVisualSettings.Instance != null)
        {
            var settings = CombatVisualSettings.Instance;

            switch (e.Type)
            {
                case CombatEventType.UnitDestroyed:
                    return settings.ApplySpeedMultiplier(settings.unitDestroyDelay);
                case CombatEventType.CommanderDamaged:
                    return settings.ApplySpeedMultiplier(settings.commanderDamageDelay);
                case CombatEventType.CombatStart:
                case CombatEventType.CombatEnd:
                    return 0.05f;
                default:
                    return settings.ApplySpeedMultiplier(settings.defaultEventDelay);
            }
        }

        // Fall back to local settings
        switch (e.Type)
        {
            case CombatEventType.UnitDestroyed:     return destroyDelay;
            case CombatEventType.CommanderDamaged:  return commanderHitDelay;
            case CombatEventType.CombatStart:
            case CombatEventType.CombatEnd:         return 0.05f;
            default:                                return defaultDelay;
        }
    }

    /// <summary>
    /// Checks if this is a main combat damage event (not pre-emptive).
    /// Pre-emptive damage has "[PRE-EMPTIVE]" in the note.
    /// </summary>
    private bool IsMainCombatDamageEvent(CombatEvent e)
    {
        if (e.Type != CombatEventType.DamageAppliedToUnit) return false;
        if (string.IsNullOrEmpty(e.Note)) return true;
        return !e.Note.Contains("[PRE-EMPTIVE]");
    }

    /// <summary>
    /// Checks if this is any damage event (unit damage OR commander damage).
    /// Used to trigger opening volley before first damage, including when enemy has no units.
    /// Excludes pre-emptive strikes which have their own visual handling.
    /// </summary>
    private bool IsDamageEvent(CombatEvent e)
    {
        // Commander damage always triggers volley (happens when enemy has no units)
        if (e.Type == CombatEventType.CommanderDamaged)
            return true;

        // Unit damage triggers volley (but not pre-emptive which has its own handling)
        if (e.Type == CombatEventType.DamageAppliedToUnit)
        {
            if (string.IsNullOrEmpty(e.Note)) return true;
            return !e.Note.Contains("[PRE-EMPTIVE]");
        }

        return false;
    }

    /// <summary>
    /// Pauses idle animations on all units during combat.
    /// Call at start of combat for cleaner visuals.
    /// </summary>
    private void PauseAllIdleAnimations()
    {
        foreach (var anim in FindObjectsByType<UnitAnimator>(FindObjectsSortMode.None))
        {
            anim.PauseIdle();
        }
        if (logEvents)
            Debug.Log("[PLAYBACK] Paused all unit idle animations");
    }

    /// <summary>
    /// Resumes idle animations on all units after combat.
    /// Call at end of combat.
    /// </summary>
    private void ResumeAllIdleAnimations()
    {
        foreach (var anim in FindObjectsByType<UnitAnimator>(FindObjectsSortMode.None))
        {
            anim.ResumeIdle();
        }
        if (logEvents)
            Debug.Log("[PLAYBACK] Resumed all unit idle animations");
    }

    /// <summary>
    /// Opening volley: All alive units from both armies trigger attack effects simultaneously.
    /// Creates a dramatic "all units firing" effect before damage resolution.
    /// When one side has no units, the other side fires at the enemy commander display.
    /// </summary>
    private IEnumerator PlayOpeningVolley()
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null || fieldSpawner == null) yield break;

        Debug.Log("[OPENING VOLLEY] Triggering all unit attack effects...");

        // Get all unit IDs from FieldSpawner's tracked visuals (not from player.Rows which may have been modified by combat resolution)
        // This ensures we fire ALL units that are visually present on the battlefield
        var playerAUnits = fieldSpawner.GetTrackedUnitIdsByPlayer(PlayerSide.A);
        var playerBUnits = fieldSpawner.GetTrackedUnitIdsByPlayer(PlayerSide.B);

        int totalUnits = playerAUnits.Count + playerBUnits.Count;
        Debug.Log($"[OPENING VOLLEY] Found {totalUnits} units with visuals (A:{playerAUnits.Count}, B:{playerBUnits.Count})");

        // Get commander positions for when enemy has no units
        Vector3 commanderBPos = (battleHUD != null) ? battleHUD.GetCommanderBPosition() : Vector3.zero;
        Vector3 commanderAPos = (battleHUD != null) ? battleHUD.GetCommanderAPosition() : Vector3.zero;

        // Trigger attack effects for all Player A units - targeting random Player B units (or commander B if no enemies)
        Debug.Log($"[OPENING VOLLEY] Firing Player A units: [{string.Join(", ", playerAUnits)}]");
        foreach (var unitId in playerAUnits)
        {
            if (playerBUnits.Count > 0)
            {
                int targetId = GetRandomEnemyTarget(playerBUnits);
                Debug.Log($"[OPENING VOLLEY] P1 unit {unitId} firing at target {targetId}");
                fieldSpawner.PlayUnitAttackEffects(unitId, targetId);
            }
            else if (commanderBPos != Vector3.zero)
            {
                // No enemy units - fire at enemy commander
                Debug.Log($"[OPENING VOLLEY] P1 unit {unitId} firing at Commander B at {commanderBPos}");
                fieldSpawner.PlayUnitAttackEffectsAtPosition(unitId, commanderBPos);
            }
        }

        // Trigger attack effects for all Player B units - targeting random Player A units (or commander A if no enemies)
        Debug.Log($"[OPENING VOLLEY] Firing Player B units: [{string.Join(", ", playerBUnits)}]");
        foreach (var unitId in playerBUnits)
        {
            if (playerAUnits.Count > 0)
            {
                int targetId = GetRandomEnemyTarget(playerAUnits);
                Debug.Log($"[OPENING VOLLEY] P2 unit {unitId} firing at target {targetId}");
                fieldSpawner.PlayUnitAttackEffects(unitId, targetId);
            }
            else if (commanderAPos != Vector3.zero)
            {
                // No enemy units - fire at enemy commander
                Debug.Log($"[OPENING VOLLEY] P2 unit {unitId} firing at Commander A at {commanderAPos}");
                fieldSpawner.PlayUnitAttackEffectsAtPosition(unitId, commanderAPos);
            }
        }

        // Wait for opening volley duration
        float duration = settings.ApplySpeedMultiplier(settings.openingVolleyDuration);
        Debug.Log($"[OPENING VOLLEY] Waiting {duration}s for volley to complete...");
        yield return new WaitForSeconds(duration);

        Debug.Log("[OPENING VOLLEY] Complete! Beginning damage resolution...");

        // Start continuous firing if enabled
        if (settings.continueFiringDuringDamage)
        {
            Debug.Log("[CONTINUOUS FIRE] Starting background firing for all units...");
            continuousFiringRoutine = StartCoroutine(ContinuousFiringRoutine());
        }
    }

    /// <summary>
    /// Stops continuous firing if running. Safe to call multiple times.
    /// </summary>
    private void StopContinuousFiring()
    {
        if (continuousFiringRoutine != null)
        {
            StopCoroutine(continuousFiringRoutine);
            continuousFiringRoutine = null;
            Debug.Log("[CONTINUOUS FIRE] Stopped");
        }
    }

    /// <summary>
    /// Gets a random enemy unit ID from the provided list.
    /// Returns -1 if no enemies are available (fallback to direction-based targeting).
    /// </summary>
    private int GetRandomEnemyTarget(List<int> enemyUnitIds)
    {
        if (enemyUnitIds == null || enemyUnitIds.Count == 0)
            return -1;

        return enemyUnitIds[UnityEngine.Random.Range(0, enemyUnitIds.Count)];
    }

    /// <summary>
    /// Continuously triggers attack effects for all units with visuals during damage resolution.
    /// Runs in the background while damage events are being processed.
    /// Uses FieldSpawner's tracked visuals - units automatically stop firing when their visual is destroyed.
    /// When one side has no units, the other side fires at the enemy commander display.
    /// </summary>
    private IEnumerator ContinuousFiringRoutine()
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null || fieldSpawner == null) yield break;

        // Fire interval from settings (default 0.4s for intense combat)
        float fireInterval = settings.continuousFiringInterval;

        Debug.Log($"[CONTINUOUS FIRE] Background firing started (interval: {fireInterval}s)");

        // Get commander positions for when enemy has no units
        Vector3 commanderBPos = (battleHUD != null) ? battleHUD.GetCommanderBPosition() : Vector3.zero;
        Vector3 commanderAPos = (battleHUD != null) ? battleHUD.GetCommanderAPosition() : Vector3.zero;

        while (true)
        {
            // Get all unit IDs that still have visuals on the battlefield
            // Units are automatically excluded when their visual is destroyed during playback
            var playerAUnits = fieldSpawner.GetTrackedUnitIdsByPlayer(PlayerSide.A);
            var playerBUnits = fieldSpawner.GetTrackedUnitIdsByPlayer(PlayerSide.B);

            int totalUnits = playerAUnits.Count + playerBUnits.Count;

            // If no units have visuals, stop firing
            if (totalUnits == 0)
            {
                Debug.Log("[CONTINUOUS FIRE] No units with visuals - stopping background firing");
                yield break;
            }

            // Trigger attack effects for all Player A units - each targeting a random enemy (or commander B if none)
            foreach (var unitId in playerAUnits)
            {
                if (playerBUnits.Count > 0)
                {
                    int targetId = GetRandomEnemyTarget(playerBUnits);
                    fieldSpawner.PlayUnitAttackEffects(unitId, targetId);
                }
                else if (commanderBPos != Vector3.zero)
                {
                    // No enemy units - fire at enemy commander
                    fieldSpawner.PlayUnitAttackEffectsAtPosition(unitId, commanderBPos);
                }
            }

            // Trigger attack effects for all Player B units - each targeting a random enemy (or commander A if none)
            foreach (var unitId in playerBUnits)
            {
                if (playerAUnits.Count > 0)
                {
                    int targetId = GetRandomEnemyTarget(playerAUnits);
                    fieldSpawner.PlayUnitAttackEffects(unitId, targetId);
                }
                else if (commanderAPos != Vector3.zero)
                {
                    // No enemy units - fire at enemy commander
                    fieldSpawner.PlayUnitAttackEffectsAtPosition(unitId, commanderAPos);
                }
            }

            // Wait before next volley
            float delay = settings.ApplySpeedMultiplier(fireInterval);
            yield return new WaitForSeconds(delay);
        }
    }

    // ========================================
    // INTERLEAVED EXPLOSION SYSTEM
    // Firepower-based ordering: lower firepower player explodes first per row
    // Row priority: Infantry → Vehicle → Ranged/Air
    // ========================================

    /// <summary>
    /// Determines explosion order based on firepower.
    /// Returns (weakerPlayer, strongerPlayer) - weaker explodes first.
    /// </summary>
    private (Player weaker, Player stronger) DetermineExplosionOrder(int firepowerA, int firepowerB)
    {
        if (firepowerA < firepowerB)
            return (playerA, playerB);  // A is weaker, explodes first
        else if (firepowerB < firepowerA)
            return (playerB, playerA);  // B is weaker, explodes first
        else
            return (playerA, playerB);  // Tie - A goes first (arbitrary but consistent)
    }

    /// <summary>
    /// Gets starting count for a unit type, defaulting to 0 if not found.
    /// </summary>
    private int GetStartingCount(Dictionary<UnitType, int> counts, UnitType unitType)
    {
        return counts != null && counts.TryGetValue(unitType, out int count) ? count : 0;
    }

    /// <summary>
    /// Gets row priority (lower = explodes first).
    /// Infantry=0, Vehicle=1, Air/Ranged=2
    /// </summary>
    private int GetRowPriority(UnitType unitType)
    {
        switch (unitType)
        {
            case UnitType.Infantry: return 0;
            case UnitType.Vehicle: return 1;
            case UnitType.Air:
            case UnitType.Ranged: return 2;
            default: return 3;
        }
    }

    /// <summary>
    /// Gets the previous priority row type (for prerequisite checking).
    /// </summary>
    private UnitType? GetPreviousPriorityRow(UnitType unitType)
    {
        switch (unitType)
        {
            case UnitType.Vehicle: return UnitType.Infantry;
            case UnitType.Air:
            case UnitType.Ranged: return UnitType.Vehicle;
            default: return null;
        }
    }

    /// <summary>
    /// Checks if a row can explode based on prerequisites:
    /// - Weaker player's row N: Stronger player's row N-1 must have exploded (or been empty)
    /// - Stronger player's row N: Weaker player's row N must have exploded (or been empty)
    /// </summary>
    private bool CanRowExplode(Player player, UnitType rowType)
    {
        bool isWeaker = (player == weakerPlayer);

        if (isWeaker)
        {
            // Weaker player: needs stronger player's PREVIOUS priority row to be done
            UnitType? prevRow = GetPreviousPriorityRow(rowType);
            if (prevRow.HasValue)
            {
                // Check if stronger player's previous row has exploded (or was empty)
                if (!rowsExplodedStronger.Contains(prevRow.Value))
                {
                    // Check if it was empty (already marked as exploded at start)
                    return false;
                }
            }
            // Infantry (priority 0) has no prerequisites
            return true;
        }
        else
        {
            // Stronger player: needs weaker player's SAME row to be done
            if (!rowsExplodedWeaker.Contains(rowType))
            {
                return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Attempts to trigger explosion for a cleared row.
    /// If prerequisites are met, starts explosion immediately.
    /// Also checks if this unlocks any other pending rows.
    /// </summary>
    private void TryTriggerRowExplosion(Player player, UnitType rowType)
    {
        bool isWeaker = (player == weakerPlayer);
        var explodingSet = isWeaker ? rowsExplodingWeaker : rowsExplodingStronger;
        var explodedSet = isWeaker ? rowsExplodedWeaker : rowsExplodedStronger;

        // Already exploding or exploded?
        if (explodingSet.Contains(rowType) || explodedSet.Contains(rowType))
            return;

        // Check prerequisites
        if (!CanRowExplode(player, rowType))
        {
            if (logEvents)
                Debug.Log($"[INTERLEAVED] {player?.Name}'s {rowType} cleared but waiting for prerequisites");
            return;
        }

        // Start explosion!
        explodingSet.Add(rowType);
        StartCoroutine(ExplodeRowAndContinue(player, rowType, isWeaker));
    }

    /// <summary>
    /// Explodes a row and then checks if other rows can now explode.
    /// </summary>
    private IEnumerator ExplodeRowAndContinue(Player player, UnitType rowType, bool isWeaker)
    {
        if (logEvents)
            Debug.Log($"[INTERLEAVED] Exploding {player?.Name}'s {rowType}!");

        // Get deaths for this row
        var deaths = GetDeathsForPlayerRow(player, rowType);

        // Explode the batch
        yield return StartCoroutine(ExplodeDeathBatch(deaths));

        // Update staged commander damage after this batch
        UpdateStagedCommanderDamage();

        // Mark as completed
        var explodingSet = isWeaker ? rowsExplodingWeaker : rowsExplodingStronger;
        var explodedSet = isWeaker ? rowsExplodedWeaker : rowsExplodedStronger;
        explodingSet.Remove(rowType);
        explodedSet.Add(rowType);

        if (logEvents)
            Debug.Log($"[INTERLEAVED] {player?.Name}'s {rowType} explosion complete");

        // Delay between rows
        var settings = CombatVisualSettings.Instance;
        if (settings != null && settings.rowInterleavingDelay > 0)
        {
            yield return new WaitForSeconds(settings.ApplySpeedMultiplier(settings.rowInterleavingDelay));
        }

        // Check if this unlocks any pending rows
        CheckPendingRowExplosions();
    }

    /// <summary>
    /// Checks all rows to see if any pending (cleared but not exploded) rows can now explode.
    /// </summary>
    private void CheckPendingRowExplosions()
    {
        UnitType[] rowOrder = { UnitType.Infantry, UnitType.Vehicle, UnitType.Air, UnitType.Ranged };

        foreach (var rowType in rowOrder)
        {
            // Check weaker player
            if (IsRowCleared(weakerPlayer, rowType) && !rowsExplodedWeaker.Contains(rowType) && !rowsExplodingWeaker.Contains(rowType))
            {
                TryTriggerRowExplosion(weakerPlayer, rowType);
            }

            // Check stronger player
            if (IsRowCleared(strongerPlayer, rowType) && !rowsExplodedStronger.Contains(rowType) && !rowsExplodingStronger.Contains(rowType))
            {
                TryTriggerRowExplosion(strongerPlayer, rowType);
            }
        }
    }

    /// <summary>
    /// Checks if a row is cleared (all units dead).
    /// </summary>
    private bool IsRowCleared(Player player, UnitType rowType)
    {
        if (player == null) return false;

        bool isPlayerA = (player == playerA);
        var deathCounts = isPlayerA ? deathCountsA : deathCountsB;
        var startingCounts = isPlayerA ? startingCountsA : startingCountsB;

        int deaths = deathCounts.TryGetValue(rowType, out int d) ? d : 0;
        int starting = GetStartingCount(startingCounts, rowType);

        return starting > 0 && deaths >= starting;
    }

    /// <summary>
    /// Gets all death events for a specific player and row type.
    /// Filters out units that have already exploded.
    /// </summary>
    private List<CombatEvent> GetDeathsForPlayerRow(Player player, UnitType rowType)
    {
        var result = new List<CombatEvent>();
        foreach (var e in allDeathEvents)
        {
            if (e.TargetPlayer == player && e.TargetUnit != null && e.TargetUnit.Type == rowType)
            {
                // Skip units that have already exploded
                if (!explodedUnitIds.Contains(e.TargetUnit.InstanceId))
                {
                    result.Add(e);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Forces all remaining deaths to explode in order at CombatEnd.
    /// This handles partial row clears and any deaths that didn't trigger during combat.
    /// Order: Weaker Infantry -> Stronger Infantry -> Weaker Vehicle -> etc.
    /// </summary>
    private IEnumerator ForceRemainingExplosions()
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null) yield break;

        UnitType[] rowOrder = { UnitType.Infantry, UnitType.Vehicle, UnitType.Air, UnitType.Ranged };

        if (logEvents)
            Debug.Log($"[INTERLEAVED] ForceRemainingExplosions - processing any remaining deaths");

        foreach (var rowType in rowOrder)
        {
            // Get remaining (not yet exploded) deaths for this row
            var weakerDeaths = GetDeathsForPlayerRow(weakerPlayer, rowType);
            var strongerDeaths = GetDeathsForPlayerRow(strongerPlayer, rowType);

            // Skip if nothing left to explode in this row
            if (weakerDeaths.Count == 0 && strongerDeaths.Count == 0)
                continue;

            if (logEvents)
                Debug.Log($"[INTERLEAVED] Force-exploding {rowType}: Weaker={weakerDeaths.Count}, Stronger={strongerDeaths.Count}");

            // Weaker player's remaining deaths explode first
            if (weakerDeaths.Count > 0)
            {
                yield return StartCoroutine(ExplodeDeathBatch(weakerDeaths));
                UpdateStagedCommanderDamage(); // Update commander HP after this batch
            }

            // Brief delay between same-row batches
            if (weakerDeaths.Count > 0 && strongerDeaths.Count > 0 && settings.rowInterleavingDelay > 0)
            {
                yield return new WaitForSeconds(settings.ApplySpeedMultiplier(settings.rowInterleavingDelay));
            }

            // Stronger player's remaining deaths explode second
            if (strongerDeaths.Count > 0)
            {
                yield return StartCoroutine(ExplodeDeathBatch(strongerDeaths));
                UpdateStagedCommanderDamage(); // Update commander HP after this batch
            }

            // Delay before next row type
            if (settings.betweenRowDelay > 0)
            {
                yield return new WaitForSeconds(settings.ApplySpeedMultiplier(settings.betweenRowDelay));
            }
        }

        if (logEvents)
            Debug.Log("[INTERLEAVED] ForceRemainingExplosions complete");
    }

    /// <summary>
    /// Updates commander HP display based on explosion progress.
    /// Commander damage is shown proportionally as units explode.
    /// This creates a visual tie between units dying and commander HP dropping.
    /// </summary>
    private void UpdateStagedCommanderDamage()
    {
        if (battleHUD == null) return;

        int totalDeaths = totalDeathsA + totalDeathsB;
        if (totalDeaths == 0) return; // No deaths to track

        int currentExplosions = explodedUnitIds.Count;
        float progressRatio = Mathf.Clamp01((float)currentExplosions / totalDeaths);

        // Calculate how much commander damage to show based on progress
        int stagedDamageToA = Mathf.RoundToInt(totalCommanderDamageToA * progressRatio);
        int stagedDamageToB = Mathf.RoundToInt(totalCommanderDamageToB * progressRatio);

        // Calculate visual HP (initial HP minus staged damage)
        int visualHPA = initialCommanderHPA - stagedDamageToA;
        int visualHPB = initialCommanderHPB - stagedDamageToB;

        // Clamp to valid range
        visualHPA = Mathf.Max(0, visualHPA);
        visualHPB = Mathf.Max(0, visualHPB);

        if (logEvents)
            Debug.Log($"[STAGED DAMAGE] Progress: {currentExplosions}/{totalDeaths} ({progressRatio:P0}), Visual HP: A={visualHPA}, B={visualHPB}");

        // Update display with staged HP values
        battleHUD.UpdateCommanderHPStaged(playerA, visualHPA, playerB, visualHPB);
    }

    /// <summary>
    /// Main interleaved explosion coroutine.
    /// Order: Weaker Infantry -> Stronger Infantry -> Weaker Vehicle -> Stronger Vehicle -> etc.
    /// </summary>
    private IEnumerator PlayInterleavedExplosions(CombatReport report)
    {
        var settings = CombatVisualSettings.Instance;
        if (settings == null) yield break;

        var (weaker, stronger) = DetermineExplosionOrder(cachedFirepowerA, cachedFirepowerB);

        if (logEvents)
            Debug.Log($"[INTERLEAVED] Explosion order: {weaker?.Name} (weaker, {(weaker == playerA ? cachedFirepowerA : cachedFirepowerB)} FP) explodes before {stronger?.Name} ({(stronger == playerA ? cachedFirepowerA : cachedFirepowerB)} FP)");

        // Row priority order: Infantry, Vehicle, then Ranged OR Air (only one exists per battlefield)
        UnitType[] rowOrder = { UnitType.Infantry, UnitType.Vehicle, UnitType.Air, UnitType.Ranged };

        // Pre-explosion delay
        if (settings.preExplosionDelay > 0)
        {
            yield return new WaitForSeconds(settings.ApplySpeedMultiplier(settings.preExplosionDelay));
        }

        foreach (var rowType in rowOrder)
        {
            // Get deaths for this row from both players
            var weakerDeaths = GetDeathsForPlayerRow(weaker, rowType);
            var strongerDeaths = GetDeathsForPlayerRow(stronger, rowType);

            // Skip if neither player has deaths in this row
            if (weakerDeaths.Count == 0 && strongerDeaths.Count == 0)
                continue;

            if (logEvents)
                Debug.Log($"[INTERLEAVED] Processing {rowType} row - Weaker: {weakerDeaths.Count}, Stronger: {strongerDeaths.Count}");

            // WEAKER player's row explodes first
            if (weakerDeaths.Count > 0)
            {
                if (logEvents)
                    Debug.Log($"[INTERLEAVED] {weaker?.Name}'s {rowType} exploding ({weakerDeaths.Count} deaths)");

                yield return StartCoroutine(ExplodeDeathBatch(weakerDeaths));
            }

            // Brief delay between same-row batches
            if (weakerDeaths.Count > 0 && strongerDeaths.Count > 0 && settings.rowInterleavingDelay > 0)
            {
                yield return new WaitForSeconds(settings.ApplySpeedMultiplier(settings.rowInterleavingDelay));
            }

            // STRONGER player's row explodes second
            if (strongerDeaths.Count > 0)
            {
                if (logEvents)
                    Debug.Log($"[INTERLEAVED] {stronger?.Name}'s {rowType} exploding ({strongerDeaths.Count} deaths)");

                yield return StartCoroutine(ExplodeDeathBatch(strongerDeaths));
            }

            // Delay before next row type
            if (settings.betweenRowDelay > 0)
            {
                yield return new WaitForSeconds(settings.ApplySpeedMultiplier(settings.betweenRowDelay));
            }
        }

        if (logEvents)
            Debug.Log("[INTERLEAVED] All explosions complete");
    }

    /// <summary>
    /// Explodes a batch of deaths with staggered timing.
    /// </summary>
    private IEnumerator ExplodeDeathBatch(List<CombatEvent> deaths)
    {
        if (deaths == null || deaths.Count == 0) yield break;

        var settings = CombatVisualSettings.Instance;
        float maxRandomOffset = settings != null ? settings.deathBurstRandomOffset : 0.25f;
        float explosionDuration = settings != null ? settings.deathExplosionDuration : 0.5f;

        int pendingCount = deaths.Count;

        // Start all explosions with random stagger
        foreach (var deathEvent in deaths)
        {
            float randomDelay = Random.Range(0f, maxRandomOffset);
            StartCoroutine(ExplodeSingleUnit(deathEvent, randomDelay, () => pendingCount--));
        }

        // Wait for all explosions to finish
        while (pendingCount > 0)
        {
            yield return null;
        }

        // Additional wait for explosion visuals to complete
        yield return new WaitForSeconds(explosionDuration);
    }

    /// <summary>
    /// Explodes a single unit with delay.
    /// Marks the unit as exploded to prevent double-explosions.
    /// </summary>
    private IEnumerator ExplodeSingleUnit(CombatEvent e, float delay, System.Action onComplete)
    {
        if (delay > 0)
            yield return new WaitForSeconds(delay);

        if (e.TargetUnit != null)
        {
            // Mark as exploded to prevent double-explosion
            explodedUnitIds.Add(e.TargetUnit.InstanceId);

            if (CombatVFXSpawner.Instance != null)
            {
                CombatVFXSpawner.Instance.SpawnDeathExplosionForUnit(e.TargetUnit.InstanceId);
            }
        }

        onComplete?.Invoke();
    }

    // ========================================
    // COMMANDER DAMAGE FINALE
    // Surviving units fire at enemy commander, then show damage
    // ========================================

    /// <summary>
    /// Commander damage finale - surviving units fire at enemy commander.
    /// Called after all explosions complete, before combat ends.
    /// </summary>
    private IEnumerator PlayCommanderFinale()
    {
        var settings = CombatVisualSettings.Instance;

        // Always update commander display at end (damage was deferred during combat)
        // Do this even if we skip the firing animation
        if (settings == null || !settings.enableCommanderFinale || commanderDamageEvents.Count == 0)
        {
            if (logEvents)
                Debug.Log("[COMMANDER FINALE] No commander damage or disabled - updating display and skipping finale");

            // Still need to sync the deferred commander HP display
            if (battleHUD != null)
            {
                battleHUD.UpdateBothCommanders(playerA, playerB);
            }
            yield break;
        }

        if (logEvents)
            Debug.Log($"[COMMANDER FINALE] Starting - {commanderDamageEvents.Count} damage events queued");

        // Determine which commander took damage
        bool aWasDamaged = false;
        bool bWasDamaged = false;
        int totalDamageToA = 0;
        int totalDamageToB = 0;

        foreach (var e in commanderDamageEvents)
        {
            if (e.TargetPlayer == playerA)
            {
                aWasDamaged = true;
                totalDamageToA += e.Amount;
            }
            else if (e.TargetPlayer == playerB)
            {
                bWasDamaged = true;
                totalDamageToB += e.Amount;
            }
        }

        // Get surviving units for each side
        var survivingA = fieldSpawner != null ? fieldSpawner.GetTrackedUnitIdsByPlayer(PlayerSide.A) : new List<int>();
        var survivingB = fieldSpawner != null ? fieldSpawner.GetTrackedUnitIdsByPlayer(PlayerSide.B) : new List<int>();

        // Get commander positions
        Vector3 commanderAPos = battleHUD != null ? battleHUD.GetCommanderAPosition() : Vector3.zero;
        Vector3 commanderBPos = battleHUD != null ? battleHUD.GetCommanderBPosition() : Vector3.zero;

        // If B was damaged, A's surviving units fire at B's commander
        if (bWasDamaged && survivingA.Count > 0 && commanderBPos != Vector3.zero)
        {
            if (logEvents)
                Debug.Log($"[COMMANDER FINALE] {survivingA.Count} units firing at Commander B");

            foreach (var unitId in survivingA)
            {
                fieldSpawner.PlayUnitAttackEffectsAtPosition(unitId, commanderBPos);
            }
        }

        // If A was damaged, B's surviving units fire at A's commander
        if (aWasDamaged && survivingB.Count > 0 && commanderAPos != Vector3.zero)
        {
            if (logEvents)
                Debug.Log($"[COMMANDER FINALE] {survivingB.Count} units firing at Commander A");

            foreach (var unitId in survivingB)
            {
                fieldSpawner.PlayUnitAttackEffectsAtPosition(unitId, commanderAPos);
            }
        }

        // Wait for firing duration
        yield return new WaitForSeconds(settings.ApplySpeedMultiplier(settings.commanderFiringDuration));

        // Camera shake for drama
        if (CameraShake.Instance != null && settings.commanderFinaleShakeIntensity > 0)
        {
            CameraShake.Instance.Shake(settings.commanderFinaleShakeIntensity, 0.4f);
        }

        // Post-firing delay
        if (settings.postFiringDelay > 0)
        {
            yield return new WaitForSeconds(settings.ApplySpeedMultiplier(settings.postFiringDelay));
        }

        // Now show the commander damage numbers
        if (bWasDamaged && totalDamageToB > 0 && commanderBPos != Vector3.zero)
        {
            DamageNumberSpawner.Spawn(totalDamageToB, commanderBPos, DamageNumberType.CommanderDamage);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCommanderDamage();
            }
        }

        if (aWasDamaged && totalDamageToA > 0 && commanderAPos != Vector3.zero)
        {
            DamageNumberSpawner.Spawn(totalDamageToA, commanderAPos, DamageNumberType.CommanderDamage);

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCommanderDamage();
            }
        }

        // NOW update the commander HP display (damage was deferred during combat)
        if (battleHUD != null)
        {
            battleHUD.UpdateBothCommanders(playerA, playerB);
            if (logEvents)
                Debug.Log("[COMMANDER FINALE] Updated commander HP display");
        }

        if (logEvents)
            Debug.Log("[COMMANDER FINALE] Complete");
    }
}