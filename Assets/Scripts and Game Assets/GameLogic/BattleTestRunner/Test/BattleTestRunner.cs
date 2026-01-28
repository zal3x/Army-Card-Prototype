using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;




/// <summary>
/// BattleTestRunner: Main controller for the auto-battler prototype.
/// 
/// RESPONSIBILITIES:
/// - Manages the multi-round battle loop (default: 3 rounds)
/// - Controls gold economy and card drawing
/// - Orchestrates buy phase (human UI + AI opponent)
/// - Triggers rearrangement phase (rounds 2+)
/// - Calls combat resolution and playback
/// - Syncs world visuals with simulation state
/// 
/// BATTLE FLOW (per round):
/// 1. Gold Phase: Award gold to both players
/// 2. Draw Phase: Draw cards (rounds 2+)
/// 3. Buy Phase: Alternating purchases (A buys, B buys, repeat until both pass)
/// 4. Rearrangement Phase: Sort/swap rows (rounds 2+)
/// 5. Combat Phase: Resolve combat â†’ playback events â†’ sync visuals
/// </summary>
[RequireComponent(typeof(CombatPlayback))]
public class BattleTestRunner : MonoBehaviour, AbilitySystem.IOnPlayApi
{
//Kamikazee section
    public static BattleTestRunner Instance { get; private set; }


private void Awake()
{
    Instance = this;
}


private enum TargetMode
{
    None,
    Kamikaze_SelectFriendlyAir,
    Kamikaze_SelectEnemy
}


private TargetMode targetMode = TargetMode.None;


private Player kamikazeOwner;
private Player kamikazeEnemy;


private Unit selectedFriendlyAir;
private RowType selectedFriendlyAirRow;


private Unit selectedEnemyUnit;
private RowType selectedEnemyRow;


private bool kamikazeDone;


    // ========================================
    // INSPECTOR CONFIGURATION
    // ========================================


    #region Battle Rules
    [Header("Battle Rules")]
    [Tooltip("How many rounds before battle ends (unless commander dies).")]
    public int maxRounds = 3;


    [Tooltip("Starting HP for both commanders.")]
    public int commanderStartHP = 200;
    #endregion


    #region Draw Rules
    [Header("Draw Rules")]
    [Tooltip("How many cards each player draws at battle start.")]
    public int startingHandSize = 10;


    [Tooltip("How many cards to draw at the start of rounds 2 and 3.")]
    public int drawAtRoundStart = 1;
    #endregion


    #region Gold Rules
    [Header("Gold Rules")]
    [Tooltip("Gold awarded to each player in round 1 only.")]
    public int startingGoldRound1 = 10;


    [Tooltip("Gold awarded to each player in rounds 2+.")]
    public int goldPerRound = 10;
    #endregion


    #region Board Rules
    [Header("Board Rules")]
    [Tooltip("Maximum units allowed per row (Front/Middle/Back).")]
    public int maxUnitsPerRow = 5;
    #endregion


    #region Deck Assets
    [Header("ScriptableObject Decks (drag these in)")]
    [Tooltip("Player A's deck definition (ScriptableObject).")]
    public DeckListDefinition playerADeck;


    [Tooltip("Player B's deck definition (ScriptableObject).")]
    public DeckListDefinition playerBDeck;
    #endregion


    #region Test Tweaks - Global Multipliers
    [Header("Test Tweaks (Global Multipliers)")]
    [Tooltip("Multiplies ALL unit HP and MaxHP when created/placed. (1.0 = normal)")]
    public float healthMultiplier = 1f;


    [Tooltip("Multiplies ALL unit ATK when created/placed. (1.0 = normal)")]
    public float damageMultiplier = 1f;
    #endregion


    #region Test Tweaks - Shuffling
    [Header("Test Tweaks (Shuffling)")]
    [Tooltip("If ON: decks shuffle at battle start. If OFF: decks keep exact asset order.")]
    public bool shuffleDeckOnPlay = false;


    [Tooltip("If ON: uses random shuffle seed each Play (different each run). If OFF: uses fixedShuffleSeed.")]
    public bool randomizeShuffleSeedOnPlay = true;


    [Tooltip("Fixed shuffle seed (used when randomizeShuffleSeedOnPlay is OFF). Player B uses seed+1.")]
    public int fixedShuffleSeed = 111;
    #endregion


    #region Test Tweaks - AI Buying
    [Header("Test Tweaks (AI Buying)")]
    [Tooltip("If ON: AI buys random affordable units. If OFF: AI always buys cheapest.")]
    public bool randomizeAIBuys = false;


    [Tooltip("If ON: uses random seed for buy randomness. If OFF: uses fixedBuySeed (repeatable).")]
    public bool randomizeBuySeedOnPlay = true;


    [Tooltip("Fixed buy seed (used when randomizeBuySeedOnPlay is OFF).")]
    public int fixedBuySeed = 999;
    #endregion


    #region TargetMark Randomness
    [Header("TargetMark Randomness")]
    [Tooltip("If ON: TargetMark uses UnityEngine.Random (true random). If OFF: uses buyRng (seeded/repeatable).")]
    public bool targetMarkUseTrueRandom = false;
    #endregion


    #region Human Player Settings
    [Header("Human Test Mode")]
    [Tooltip("If ON: Player A uses UI. If OFF: Player A uses AI (legacy testing).")]
    public bool playerAIsHuman = true;
    #endregion


    #region UI References
    [Header("Phase 2 UI")]
    [Tooltip("Reference to the buy phase UI controller.")]
    public BuyPhaseUI_FixedHand buyUI;
    #endregion


    #region World Visual Spawning
    [Header("World Visual Spawning")]
    [Tooltip("Spawns/removes world-space unit prefabs when units are bought/destroyed.")]
    public FieldSpawner fieldSpawner;
    [Tooltip("Spawns VFX for unit deaths and other combat effects.")]
public CombatVFXSpawner combatVFXSpawner; // ADD THIS LINE




    [Tooltip("List of UnitCardDefinition assets (Infantry, Tank, Rockets, Plane, etc.). Used for spawning visuals.")]
    public List<UnitCardDefinition> cardDefinitions = new List<UnitCardDefinition>();
    #endregion


    [Header("Battle HUD")]
[Tooltip("Central HUD coordinator for commanders and army stats")]
public BattleHUD battleHUD;


[Header("Rearrangement UI")]
public RearrangementUI_SwapRows rearrangeUI;
[SerializeField] public SpecialCards specialCards;


[Header("Commanders (Optional)")]
[SerializeField] private CommanderDefinition playerACommander;
[SerializeField] private CommanderDefinition playerBCommander;


public Player PlayerA => playerA;
public Player PlayerB => playerB;
// =====================================================
// SPECIAL CARD WRAPPERS (called by SpecialCards.cs)
// =====================================================








public void ShowRevealOverlay(string title, List<UnitCard> cards)
{
    buyUI?.ShowRevealOverlay(title, cards);
}


public RowType GetPlacementRowPublic(Player p, UnitType type)
{
    return GetPlacementRow(p, type);
}


public bool Special_HasAnyUnit(Player owner)
{
    if (owner == null || owner.Rows == null) return false;


    foreach (var kvp in owner.Rows)
    {
        var row = kvp.Value;
        if (row?.Units == null) continue;


        for (int i = 0; i < row.Units.Count; i++)
        {
            if (row.Units[i] != null && row.Units[i].IsAlive)
                return true;
        }
    }


    return false;
}


public bool Special_HasAnyAirUnit(Player owner)
{
    if (owner == null || owner.Rows == null) return false;


    // IMPORTANT: Use the *current semantic row* for Air (accounts for swaps)
    RowType airRow = GetPlacementRow(owner, UnitType.Air);
    var units = owner.Rows[airRow].Units;


    for (int i = 0; i < units.Count; i++)
    {
        if (units[i] != null && units[i].IsAlive)
            return true;
    }


    return false;
}


/// <summary>
/// Default/temporary behavior: sacrifices highest HP friendly air unit,
/// destroys highest HP enemy unit.
/// Later you can replace the target selection with your manual selection UI.
/// </summary>




/// <summary>
/// Default/temporary behavior: recalls highest HP friendly unit anywhere.
/// Returns a new UnitCard to hand that costs 0, allowing on-play triggers again.
/// </summary>
/// 


public void Special_DoPaperTiger(Player owner)
{
    if (owner == null) return;


    Unit pick = FindHighestHpUnitAnywhere(owner, out RowType row);
    if (pick == null)
    {
        Debug.LogWarning("[PAPER TIGER] No friendly unit found to recall.");
        return;
    }


    // Remove unit from board + world (no death effects)
    RemoveUnitQuiet(owner, pick, row);


    // Pull the ACTUAL played card from discard first
    UnitCard returnedCard =
        owner.Deck.TryRemoveFirstMatchingFromDiscard(c => c != null && c.Name == pick.Name)
        ?? owner.Deck.TryRemoveFirstMatchingFromDeck(c => c != null && c.Name == pick.Name);


    if (returnedCard == null)
    {
        Debug.LogWarning($"[PAPER TIGER] Could not find card '{pick.Name}' in deck piles.");
        return;
    }


    returnedCard.Cost = 0;
    owner.Hand.Add(returnedCard); // ✅ immediate HAND


    // ✅ refresh the buy UI immediately so it appears now (not next round)
RefreshBuyHandUI(owner);


    Debug.Log($"[PAPER TIGER] {owner.Name} recalled '{pick.Name}' → returned to HAND (Cost=0).");
}










/// <summary>
/// Heals the current semantic row by percentage of max HP.
/// pct = 0.5f means heal 50% of max HP.
/// </summary>
public void Special_HealRowBySemanticType(Player owner, UnitType semantic, float pct)
{
    if (owner == null) return;


    RowType rowType = GetPlacementRow(owner, semantic);
    var units = owner.Rows[rowType].Units;


    for (int i = 0; i < units.Count; i++)
    {
        Unit u = units[i];
        if (u == null || !u.IsAlive) continue;


        int maxHp = u.MaxHP; // assumes you have MaxHP; if not, see note below
        int heal = Mathf.RoundToInt(maxHp * pct);
        if (heal <= 0) continue;


        u.HP = Mathf.Min(maxHp, u.HP + heal);
    }
}




public bool IsManualRearrangeActive { get; private set; }
public int MaxUnitsPerRow => maxUnitsPerRow;


public void RequestReorderUnitWithinLane(int unitId, int newSlotIndex)
{
    if (!IsManualRearrangeActive) return;
    if (fieldSpawner == null) return;


    if (!fieldSpawner.TryGetSlotInfo(unitId, out var laneType, out var side, out var oldSlotIndex))
        return;


    if (side != PlayerSide.A) return;


   // Replace your hardcoded lane->row mapping
RowType row = GetPlacementRow(playerA, laneType);




    var list = playerA.Rows[row].Units;


    // find unit in that row by InstanceId
    int fromIndex = list.FindIndex(u => u.InstanceId == unitId);
    if (fromIndex < 0) return;


    newSlotIndex = Mathf.Clamp(newSlotIndex, 0, list.Count - 1);
    if (newSlotIndex == fromIndex) 
    {
        // snap back visually
        fieldSpawner.ReorderLaneVisuals(PlayerSide.A, laneType, list.ConvertAll(u => u.InstanceId).ToArray(), animate:true, duration:0.15f);
        return;
    }


    // reorder simulation list
    var unit = list[fromIndex];
    list.RemoveAt(fromIndex);
    list.Insert(newSlotIndex, unit);


    // reorder visuals to match new list order
    int[] ids = list.ConvertAll(u => u.InstanceId).ToArray();
    fieldSpawner.ReorderLaneVisuals(PlayerSide.A, laneType, ids, animate:true, duration:0.20f);


    // (optional) refresh HUD stats — not required but harmless
    if (battleHUD != null) battleHUD.RefreshArmyStats(playerA, playerB);
}




// Tracks where each semantic group currently lives for each player.
// This must NOT be inferred from living units (rows can be empty).
private struct RowLayout
{
    public RowType infantryRow;
    public RowType vehicleRow;  // Vehicle + Ranged
    public RowType airRow;
}


private readonly Dictionary<Player, RowLayout> rowLayoutByPlayer = new();


    /// <summary>
/// Gets the most recently placed unit of a given type for a player.
/// Used to find the unit ID after a card is bought.
/// </summary>
private Unit GetLastPlacedUnit(Player player, UnitType cardType)
{
RowType row = GetPlacementRow(player, cardType);
    var units = player.Rows[row].Units;
    
    if (units.Count == 0) return null;
    
    // Return the last unit in the row (most recently added)
    return units[units.Count - 1];
}


    // ========================================
    // PRIVATE STATE
    // ========================================


    private Player playerA;
    private Player playerB;
    private CombatPlayback playback;
    private Coroutine battleRoutine;


    // RNG for buy-phase randomness (seeded for repeatability if desired)
    private System.Random buyRng;


    // Quick lookup: card name â†’ definition (for spawning visuals)
    private Dictionary<string, UnitCardDefinition> defByName;


   // ========================================
// UNITY LIFECYCLE
// ========================================
// ========================================
// UNITY LIFECYCLE
// ========================================
private void Start()
{
    Debug.Log($"[BTR] START on GameObject='{gameObject.name}' instanceId={GetInstanceID()} maxRounds={maxRounds}");


    playback = GetComponent<CombatPlayback>();


    InitializeSeeds();
    InitializePlayers();
    BuildCardDefinitionLookup();

// Apply commanders from ScriptableObjects (if provided)
if (CommanderBonus.Instance != null)
{
    if (playerACommander != null)
        CommanderBonus.Instance.SetCommanderForPlayer(playerA, playerACommander.commanderId);

    if (playerBCommander != null)
        CommanderBonus.Instance.SetCommanderForPlayer(playerB, playerBCommander.commanderId);
}

// Optional starting stat overrides (HP/Gold)
ApplyCommanderStartingOverrides(playerA, playerACommander);
ApplyCommanderStartingOverrides(playerB, playerBCommander);


    // Load decks AFTER commander assignment (so LoadDecks can apply permanent bonuses)
    LoadDecks();


    DrawInitialHands();


    if (CommanderBonus.Instance != null)
    CommanderBonus.Instance.EnterBattleContext();




    // Start battle ONCE
    battleRoutine = StartCoroutine(RunBattle());


    // Auto-find references if not assigned in Inspector
    if (combatVFXSpawner == null)
    {
        combatVFXSpawner = FindObjectOfType<CombatVFXSpawner>();
        if (combatVFXSpawner == null)
        {
            Debug.LogWarning("[BattleTestRunner] CombatVFXSpawner not found in scene! On-play destruction effects will not work.");
        }
    }


    if (fieldSpawner == null)
    {
        fieldSpawner = FindObjectOfType<FieldSpawner>();
    }
}






    // ========================================
    // INITIALIZATION
    // ========================================


    /// <summary>
    /// Sets up RNG seeds for shuffling and buying.
    /// If randomize flags are ON, seeds are random each run.
    /// If OFF, seeds are fixed (repeatable tests).
    /// </summary>
    private void InitializeSeeds()
    {
        int seedA = randomizeShuffleSeedOnPlay ? UnityEngine.Random.Range(1, 1_000_000) : fixedShuffleSeed;
        int seedB = randomizeShuffleSeedOnPlay ? UnityEngine.Random.Range(1, 1_000_000) : fixedShuffleSeed + 1;
        int buySeed = randomizeBuySeedOnPlay ? UnityEngine.Random.Range(1, 1_000_000) : fixedBuySeed;


        buyRng = new System.Random(buySeed);


        Debug.Log($"[SEEDS] ShuffleSeedA={seedA} ShuffleSeedB={seedB} | BuySeed={buySeed} | ShuffleOnPlay={shuffleDeckOnPlay} | RandomBuys={randomizeAIBuys}");
    }


    /// <summary>
    /// Creates Player A and Player B with initial state.
    /// </summary>
   private void InitializePlayers()
{
    int seedA = randomizeShuffleSeedOnPlay ? UnityEngine.Random.Range(1, 1_000_000) : fixedShuffleSeed;
    int seedB = randomizeShuffleSeedOnPlay ? UnityEngine.Random.Range(1, 1_000_000) : fixedShuffleSeed + 1;


    playerA = new Player("Player A", deckSeed: seedA);
    playerB = new Player("Player B", deckSeed: seedB);


    // ✅ ADD THIS (layout must exist even if rows are empty)
    rowLayoutByPlayer[playerA] = new RowLayout { infantryRow = RowType.Front, vehicleRow = RowType.Middle, airRow = RowType.Back };
    rowLayoutByPlayer[playerB] = new RowLayout { infantryRow = RowType.Front, vehicleRow = RowType.Middle, airRow = RowType.Back };


    playerA.CommanderHP = commanderStartHP;
    playerB.CommanderHP = commanderStartHP;


    if (battleHUD != null)
    {
        battleHUD.Initialize(commanderStartHP);
        battleHUD.FullRefresh(playerA, playerB, immediate: true);
    }
    else
    {
        Debug.LogWarning("[BATTLE] BattleHUD not assigned!");
    }
}










    /// <summary>
    /// Loads decks from ScriptableObject assets and optionally shuffles them.
    /// </summary>
    private void LoadDecks()
{
    // Determine which deck definitions to use (commander deck if present, otherwise fallback)
    DeckListDefinition deckA = (playerACommander != null && playerACommander.deck != null) ? playerACommander.deck : playerADeck;
    DeckListDefinition deckB = (playerBCommander != null && playerBCommander.deck != null) ? playerBCommander.deck : playerBDeck;

    if (deckA == null || deckB == null)
    {
        Debug.LogError("[BATTLE] Missing DeckListDefinition! Assign commander decks or drag PlayerADeck/PlayerBDeck into the inspector.");
        return;
    }

    // Build runtime decks WITH OWNER CONTEXT (needed for commander-aware UI/effects)
    var aCards = deckA.BuildRuntimeDeck(playerA);
    var bCards = deckB.BuildRuntimeDeck(playerB);

    // Apply permanent commander changes BEFORE loading into SimpleDeck
    if (CommanderBonus.Instance != null)
    {
        CommanderBonus.Instance.ApplyPermanentBonusesToCards(playerA, aCards);
        CommanderBonus.Instance.ApplyPermanentBonusesToCards(playerB, bCards);
    }

    playerA.Deck.SetStartingDeck(aCards);
    playerB.Deck.SetStartingDeck(bCards);

    if (shuffleDeckOnPlay)
    {
        playerA.Deck.ShuffleDrawPile();
        playerB.Deck.ShuffleDrawPile();
    }
}



    /// <summary>
    /// Builds a quick lookup dictionary: card name â†’ UnitCardDefinition.
    /// Used by FieldSpawner to find the correct prefab when spawning visuals.
    /// </summary>
    private void BuildCardDefinitionLookup()
    {
        defByName = new Dictionary<string, UnitCardDefinition>();


        foreach (var def in cardDefinitions)
        {
            if (def == null) continue;
            defByName[def.cardName] = def;
        }
    }
    /// <summary>
/// Tries to buy a specific card from the player's hand.
/// This replaces TryBuyHandCardByIndex for carousel-based UI.
/// </summary>
/// 
private bool TryBuyCard(Player player, UnitCard card, out int placedUnitId, out UnitCard boughtCard)
{
    boughtCard = null;
    placedUnitId = -1;
    if (card == null) return false;


    int handIndex = player.Hand.IndexOf(card);
    if (handIndex == -1)
        return false;


    return TryBuyHandCardByIndex(player, handIndex, out placedUnitId, out boughtCard);
}


private bool TryBuyCard(Player player, UnitCard card, out int placedUnitId)
{
    placedUnitId = -1;
    if (card == null) return false;


    int handIndex = player.Hand.IndexOf(card);
    if (handIndex == -1)
    {
        Debug.LogWarning($"[BUY] Card {card.Name} not found in {player.Name}'s hand!");
        return false;
    }


    return TryBuyHandCardByIndex(player, handIndex, out placedUnitId);
}
    /// <summary>
    /// Draws the starting hand for both players.
    /// </summary>
    private void DrawInitialHands()
    {
        DrawCards(playerA, startingHandSize);
        DrawCards(playerB, startingHandSize);
    }


    // ========================================
    // MAIN BATTLE LOOP
    // ========================================


    /// <summary>
    /// Main battle coroutine. Runs up to maxRounds, ending early if a commander dies.
    /// </summary>
    private IEnumerator RunBattle()
    {
        for (int round = 1; round <= maxRounds; round++)
        {
            if (IsBattleOver())
            {
                Debug.Log($"[BATTLE] Battle already over before Round {round} starts.");
                break;
            }


            RoundReport roundReport = new RoundReport();
            Debug.Log($"================ ROUND {round} ================");


            roundReport.Add(new RoundEvent
{
    Type = RoundEventType.RoundStart,
    RoundNumber = round,
    Note = $"Round {round} starts"
});


// START OF ROUND: clear round-only effects + apply per-round battle effects (ex: KGB tick)
if (specialCards != null)
{
    specialCards.StartRound(playerA, playerB);
}
else
{
    Debug.LogWarning("[SPECIAL] specialCards is null (StartRound skipped).");
}


// 1) GOLD PHASE
yield return GoldPhase(round, roundReport);


            // 2) DRAW PHASE (rounds 2+)
            if (round >= 2)
            {
                yield return DrawPhase(round, roundReport);
            }


            // 3) BUY PHASE
if (fieldSpawner != null)
    fieldSpawner.RebuildSlotOccupancyFromReality(clearReservations: true, snapUnitsToSlots: false);


            if (playerAIsHuman)
            {
                Debug.Log("=== Buy Phase (Player A = HUMAN UI, Player B = AI) ===");
                yield return BuyPhaseAlternating_UI(playerA, playerB, round);
            }
            else
            {
                Debug.LogWarning("[BATTLE] playerAIsHuman is OFF. No buy phase implemented for AI vs AI.");
            }


            LogBoardState(playerA, $"Round {round} AFTER BUY");
            LogBoardState(playerB, $"Round {round} AFTER BUY");


            // 4) REARRANGEMENT PHASE (rounds 2+)
if (round >= 2)
{
    Debug.Log("=== Rearrangement Phase ===");
    LogBoardState(playerA, $"Round {round} BEFORE REARRANGE");
    LogBoardState(playerB, $"Round {round} BEFORE REARRANGE");


    // Player A: manual (drag + UI swap)
    yield return RearrangementPhase_Manual(playerA, round);


    // Player B: keep your current AI heuristic for now
    RearrangementPhase(playerB);


    LogBoardState(playerA, $"Round {round} AFTER REARRANGE");
    LogBoardState(playerB, $"Round {round} AFTER REARRANGE");


    roundReport.Add(new RoundEvent
    {
        Type = RoundEventType.Rearranged,
        RoundNumber = round,
        Note = "Rearrangement phase complete"
    });


    // NEW: Wait for rearrangement animations to complete
    yield return new WaitForSeconds(0.5f);
}


          // 5) COMBAT PHASE
if (fieldSpawner != null)
    fieldSpawner.RebuildSlotOccupancyFromReality(clearReservations: true, snapUnitsToSlots: false);


Debug.Log("=== Combat Phase ===");


// PRE-COMBAT SPECIAL EFFECTS (ex: Barbed Wire)
if (specialCards != null)
{
    specialCards.ApplyPreCombat(playerA, this);
    specialCards.ApplyPreCombat(playerB, this);
}


CombatReport report = CombatResolver.ResolveCombat(playerA, playerB, alsoDebugLog: false);


// Set player references for CombatPlayback
if (playback != null)
{
    playback.SetPlayers(playerA, playerB);
    playback.battleHUD = battleHUD; // Pass BattleHUD reference too
}


playback.Play(report);
yield return new WaitUntil(() => playback != null && !playback.IsPlaying);


            
// Update HUD (commander HP + army stats)
if (battleHUD != null)
{
    battleHUD.UpdateBothCommanders(playerA, playerB);
    battleHUD.RefreshArmyStats(playerA, playerB);
}




            // Sync world visuals with simulation (remove dead units)
            fieldSpawner.SyncToBoard(playerA, playerB);
            fieldSpawner.ResetIndices();
            ResetLayoutIfBoardEmpty(playerA, PlayerSide.A);
ResetLayoutIfBoardEmpty(playerB, PlayerSide.B);


            Debug.Log($"[BATTLE] Round {round} complete. AHP={playerA.CommanderHP} BHP={playerB.CommanderHP}");


            roundReport.Add(new RoundEvent
            {
                Type = RoundEventType.RoundEnd,
                RoundNumber = round,
                Note = $"Round {round} ends. AHP={playerA.CommanderHP}, BHP={playerB.CommanderHP}"
            });


            DumpRoundReport(roundReport);


            if (IsBattleOver())
            {
                Debug.Log($"Battle ended after Round {round}.");
                break;
            }


            yield return new WaitForSeconds(0.25f);
        }


        Debug.Log($"FINAL RESULT: {playerA.Name} HP={playerA.CommanderHP} vs {playerB.Name} HP={playerB.CommanderHP}");
    }


    // ========================================
    // PHASE IMPLEMENTATIONS
    // ========================================


    #region Gold Phase
    /// <summary>
    /// Awards gold to both players.
    /// Round 1: startingGoldRound1
    /// Rounds 2+: goldPerRound
    /// </summary>
    private IEnumerator GoldPhase(int round, RoundReport roundReport)
    {
        int goldGain = (round == 1) ? startingGoldRound1 : goldPerRound;


        playerA.Gold += goldGain;
        playerB.Gold += goldGain;


        roundReport.Add(new RoundEvent
        {
            Type = RoundEventType.GoldGained,
            RoundNumber = round,
            Player = playerA,
            Amount = goldGain,
            Note = $"{playerA.Name} gains {goldGain} gold (now {playerA.Gold})"
        });


        roundReport.Add(new RoundEvent
        {
            Type = RoundEventType.GoldGained,
            RoundNumber = round,
            Player = playerB,
            Amount = goldGain,
            Note = $"{playerB.Name} gains {goldGain} gold (now {playerB.Gold})"
        });


        Debug.Log($"{playerA.Name} gains {goldGain} gold (now {playerA.Gold})");
        Debug.Log($"{playerB.Name} gains {goldGain} gold (now {playerB.Gold})");


       // At the end of GoldPhase, after the Debug.Log lines, add:


// Update gold displays
if (battleHUD != null)
{
    battleHUD.UpdateBothGold(playerA, playerB);
}


yield break;
    }
    #endregion


    #region Draw Phase
    /// <summary>
    /// Draws cards for both players (rounds 2+).
    /// </summary>
    private IEnumerator DrawPhase(int round, RoundReport roundReport)
    {
        int beforeA = playerA.Hand.Count;
        int beforeB = playerB.Hand.Count;


        DrawCards(playerA, drawAtRoundStart);
        DrawCards(playerB, drawAtRoundStart);


        roundReport.Add(new RoundEvent
        {
            Type = RoundEventType.CardDrawn,
            RoundNumber = round,
            Player = playerA,
            Amount = playerA.Hand.Count - beforeA,
            Note = $"{playerA.Name} draws {playerA.Hand.Count - beforeA} (hand now {playerA.Hand.Count})"
        });


        roundReport.Add(new RoundEvent
        {
            Type = RoundEventType.CardDrawn,
            RoundNumber = round,
            Player = playerB,
            Amount = playerB.Hand.Count - beforeB,
            Note = $"{playerB.Name} draws {playerB.Hand.Count - beforeB} (hand now {playerB.Hand.Count})"
        });


        yield break;
    }


    /// <summary>
    /// Draws cards from deck into hand. Stops when deck is empty.
    /// </summary>
    private void DrawCards(Player p, int count)
    {
        int drawn = 0;


        for (int i = 0; i < count; i++)
        {
            UnitCard card = p.Deck.DrawOne();
            if (card == null) break;


            p.Hand.Add(card);
            drawn++;
        }


        Debug.Log($"{p.Name} draws {drawn} (hand now {p.Hand.Count})");
    }
    #endregion


    #region Buy Phase - UI Version
    /// <summary>
    /// PHASE 2: Alternating buy loop using UI for Player A and AI for Player B.
    /// 
    /// FLOW:
    /// - Player A clicks a hand card or passes
    /// - Player B auto-buys one unit (or passes)
    /// - Repeat until both pass
    /// 
    /// NOTES:
    /// - Buy happens immediately (gold spent, unit placed, card discarded)
    /// - On-play abilities trigger after placement
    /// - World visual is spawned via FieldSpawner
    /// </summary>
    private IEnumerator BuyPhaseAlternating_UI(Player a, Player b, int round)
{
    bool aPassed = false;
    bool bPassed = false;
    bool waitingForInput = false;
    bool processingPurchase = false;
    UnitCard selectedCard = null;


    


    // Get reference to your BuyPhaseUI component
    if (buyUI == null)
    {
        Debug.LogError("[BUY] BuyPhaseUI_FixedHand not found!");
        yield break;
    }


    buyUI.Begin(
        round,
        a,
        cardSelected: (card) => 
        { 
            if (!waitingForInput || processingPurchase) return;
            selectedCard = card;
            waitingForInput = false;
            processingPurchase = true;
        },
        passClicked: () => 
        { 
            if (!waitingForInput || processingPurchase) return;
            aPassed = true;
            waitingForInput = false;
            processingPurchase = true;
        }
    );


while (!aPassed || !bPassed)  // CORRECT - exits when both pass
    {
        // === PLAYER A (HUMAN) ===
        waitingForInput = true;
        processingPurchase = false;
        selectedCard = null;
        
        float timeout = 0f;
        while (waitingForInput && timeout < 300f)
        {
            timeout += Time.deltaTime;
            yield return null;
        }


        if (timeout >= 300f)
        {
            Debug.LogError("[BUY] Timeout!");
            aPassed = true;
            break;
        }


      if (!aPassed && selectedCard != null)
{
    bool isSpecial = selectedCard.IsSpecialCard;


    // Only unit cards need a spawn position preview
    Vector3 spawnPos = Vector3.zero;
    if (!isSpecial && fieldSpawner != null)
        spawnPos = fieldSpawner.GetNextSpawnPositionPreview(selectedCard.Type, PlayerSide.A, a);


    int placedUnitId;
bool success = TryBuyCard(a, selectedCard, out placedUnitId, out UnitCard boughtCard);


    if (!success)
    {
        buyUI.SetHandSummary("Can't buy that!");
        continue;
    }


// Remove the card from UI and get its visual for flight animation
CardVisual purchasedVisual = buyUI.RemovePurchasedCardAndReturnVisual(selectedCard);
buyUI.SetHandSummary(isSpecial ? $"Played {selectedCard.Name}!" : $"Bought {selectedCard.Name}!");


// IMPORTANT: Special effects should not apply until the slam finishes.
// This function runs AFTER the slam (or immediately if we have no visual).
void ApplySpecialCardNow()
{
    if (specialCards == null || boughtCard == null) return;


    // Kamikaze is interactive: start targeting AFTER slam
    if (boughtCard.IsSpecialCard &&
        boughtCard.SpecialType == SpecialCards.SpecialCardType.Kamikaze)
    {
        if (buyUI != null)
            buyUI.ShowTooltip("Kamikaze: Click your AIR unit, then click an ENEMY unit to destroy.", 4f);


        StartCoroutine(KamikazeTargetingCoroutine(a, b));
        return;
    }


    // All other specials apply here (AFTER slam)
    specialCards.Play(boughtCard.SpecialType, a, GetOpponent(a), this);
}




// UNIT CARD: animate to unit + reveal when arrived
if (!isSpecial)
{
    if (placedUnitId >= 0 && fieldSpawner != null)
        fieldSpawner.SetUnitVisibility(placedUnitId, false);


    if (placedUnitId >= 0 && CardFlightManager.Instance != null && purchasedVisual != null)
    {
        CardFlightManager.Instance.RegisterAndFlyToUnit(
            placedUnitId,
            purchasedVisual,
            null,
            spawnPos,
            PlayerSide.A
        );
    }
}
else
{




    // SPECIAL CARD: fly first, then apply effect after slam
    bool specialResolved = false;


void ResolveSpecial()
{
    ApplySpecialCardNow();
    specialResolved = true;
}


if (purchasedVisual != null && CardFlightManager.Instance != null)
{
    CardFlightManager.Instance.FlySpecialToCenter(purchasedVisual, ResolveSpecial);
}
else
{
    Debug.LogWarning($"[BUY] Special card visual missing or CardFlightManager missing. purchasedVisual={(purchasedVisual ? purchasedVisual.name : "NULL")}");
    ResolveSpecial();
    if (purchasedVisual != null) Destroy(purchasedVisual.gameObject);
}


// Wait until the slam callback applied the special before continuing
float waitTimeout = 2.0f;
float t = 0f;
while (!specialResolved && t < waitTimeout)
{
    t += Time.deltaTime;
    yield return null;
}


if (!specialResolved)
{
    Debug.LogWarning("[BUY] Timed out waiting for special slam to resolve. Forcing apply now.");
    ApplySpecialCardNow();
}


}










    // Refresh UI
    if (battleHUD != null)
        battleHUD.RefreshArmyStats(playerA, playerB);


    buyUI.SetRoundGold(round, a.Gold);
    yield return new WaitForSeconds(0.5f);
}




        if (aPassed) break;


      // === PLAYER B (AI) ===


// "Human-like thinking" delay
yield return new WaitForSeconds(UnityEngine.Random.Range(.35f, .85f));


UnitCard bCard = null;
bool bSuccess = TryAutoBuyOne(b, out bCard);
        
        if (bSuccess)
        {
            buyUI.SetEnemyBought($"{bCard.Name} ({bCard.Type}) Cost={bCard.Cost}");
        }
        else
        {
            bPassed = true;
            buyUI.SetEnemyBought("PASS");
        }


        yield return new WaitForSeconds(0.3f);
    }


    buyUI.End();
}


    /// <summary>
    /// Buys the exact card at a specific hand index (used by UI clicks).
    /// Returns true if purchase succeeded.
    /// </summary>
    /// 
    private bool TryBuyHandCardByIndex(Player p, int handIndex, out int placedUnitId)
{
    return TryBuyHandCardByIndex(p, handIndex, out placedUnitId, out _);
}
private bool TryBuyHandCardByIndex(Player p, int handIndex, out int placedUnitId, out UnitCard boughtCard)
{
    boughtCard = null;
    placedUnitId = -1;


    if (p.Hand == null) return false;
    if (handIndex < 0 || handIndex >= p.Hand.Count) return false;


    UnitCard chosen = p.Hand[handIndex];


    // Can we afford it?
    if (chosen.Cost > p.Gold)
        return false;


    // =========================================================
    // SPECIAL CARD PATH (does NOT place a unit)
    // =========================================================
    if (chosen.IsSpecialCard)
    {
        if (specialCards != null && !specialCards.CanPlay(chosen.SpecialType, p, GetOpponent(p), this))
            return false;


        // BUY
        p.Gold -= chosen.Cost;
        p.Hand.RemoveAt(handIndex);


        // DISCARD
        p.Deck.Discard(chosen);


        Debug.Log($"[BUY] {p.Name} plays SPECIAL CARD: {chosen.Name} (SpecialType={chosen.SpecialType}, Cost={chosen.Cost}) | GoldAfterPlay={p.Gold}");


        // NOTE: Special-card effect is triggered AFTER the slam animation now.
// (Kamikaze is also handled later due to targeting.)




        if (battleHUD != null)
        {
            battleHUD.RefreshArmyStats(playerA, playerB);
            battleHUD.UpdateBothGold(playerA, playerB);
        }


        boughtCard = chosen;
        return true;
    }


    // =========================================================
    // NORMAL UNIT CARD PATH
    // =========================================================
    RowType targetRow = GetPlacementRow(p, chosen.Type);


    if (p.Rows[targetRow].Units.Count >= maxUnitsPerRow)
    {
        Debug.LogWarning($"[BUY] Cannot buy {chosen.Name} - row {targetRow} is full ({p.Rows[targetRow].Units.Count}/{maxUnitsPerRow})");
        return false;
    }


    // BUY
    p.Gold -= chosen.Cost;
    p.Hand.RemoveAt(handIndex);


    // PLACE
    Unit unit = chosen.CreateUnitInstance();
    ApplyGlobalMultipliers(unit);
    p.Rows[targetRow].Units.Add(unit);
    specialCards?.OnUnitAddedToBoard(p, unit);
    SpawnWorldVisualForBoughtUnit(p, unit, chosen);
    placedUnitId = unit.InstanceId;


    // DISCARD
    p.Deck.Discard(chosen);


    Debug.Log($"[BUY] {p.Name} buys CARD: {chosen.Name} (Type={chosen.Type}, Cost={chosen.Cost}) -> {targetRow} | UnitId=[{unit.InstanceId}] | GoldAfterBuy={p.Gold}");


    AbilitySystem.ExecuteOnPlay(p, GetOpponent(p), unit, this);


    if (battleHUD != null)
    {
        battleHUD.RefreshArmyStats(playerA, playerB);
        battleHUD.UpdateBothGold(playerA, playerB);
    }


    boughtCard = chosen;
    return true;
}






    /// <summary>
    /// AI auto-buy: buys exactly ONE affordable unit.
    /// Returns true if a purchase was made, false if player passes.
    /// </summary>
   private bool TryAutoBuyOne(Player p, out UnitCard boughtCard)
{
    boughtCard = null;


    if (p.Hand == null || p.Hand.Count == 0)
        return false;


    // Build list of affordable cards
    List<UnitCard> affordable = new List<UnitCard>();


    for (int i = 0; i < p.Hand.Count; i++)
    {
        UnitCard card = p.Hand[i];


        if (card.Cost > p.Gold)
            continue;


        // =========================================================
        // SPECIAL CARDS: no row capacity checks (but optional CanPlay)
        // =========================================================
        if (card.IsSpecialCard)
        {
            // Optional: enforce preconditions (Kamikaze needs air, POW needs enemy hand, etc.)
            if (specialCards != null && !specialCards.CanPlay(card.SpecialType, p, GetOpponent(p), this))
                continue;


            affordable.Add(card);
            continue;
        }


        // =========================================================
        // UNIT CARDS: must have space in correct row (accounting for swaps)
        // =========================================================
        RowType targetRow = GetPlacementRow(p, card.Type);
        if (p.Rows[targetRow].Units.Count >= maxUnitsPerRow)
            continue;


        affordable.Add(card);
    }


    // Player cannot buy anything → pass
    if (affordable.Count == 0)
        return false;


    UnitCard chosen;


    if (!randomizeAIBuys)
    {
        // Deterministic: buy cheapest
        affordable.Sort((a, b) => a.Cost.CompareTo(b.Cost));
        chosen = affordable[0];
    }
    else
    {
        // Random: buy any affordable card
        int index = buyRng.Next(0, affordable.Count);
        chosen = affordable[index];
    }


    // =========================================================
    // EXECUTE BUY
    // =========================================================


    // BUY
    p.Gold -= chosen.Cost;
    p.Hand.Remove(chosen);


    // DISCARD
    p.Deck.Discard(chosen);


    // =========================================================
    // SPECIAL CARD PATH
    // =========================================================
    if (chosen.IsSpecialCard)
    {
        Debug.Log($"[BUY-AI] {p.Name} plays SPECIAL CARD: {chosen.Name} (SpecialType={chosen.SpecialType}, Cost={chosen.Cost}) | GoldAfterPlay={p.Gold}");


        if (specialCards != null)
        {
            specialCards.Play(chosen.SpecialType, p, GetOpponent(p), this);
        }
        else
        {
            Debug.LogError("[SPECIAL] specialCards reference is NULL on BattleTestRunner. Add SpecialCards component and assign it in Inspector.");
        }


        // Update HUD after AI special card use
        if (battleHUD != null)
        {
            battleHUD.RefreshArmyStats(playerA, playerB);
            battleHUD.UpdateBothGold(playerA, playerB);
        }


        boughtCard = chosen;
        return true;
    }


    // =========================================================
    // UNIT CARD PATH
    // =========================================================


    // Use actual row (accounting for swaps)
    RowType chosenRow = GetPlacementRow(p, chosen.Type);


    // PLACE
    Unit unit = chosen.CreateUnitInstance();
    ApplyGlobalMultipliers(unit);
    p.Rows[chosenRow].Units.Add(unit);
    SpawnWorldVisualForBoughtUnit(p, unit, chosen);


    Debug.Log($"[BUY-AI] {p.Name} buys {chosen.Name} (Cost={chosen.Cost}) -> {chosenRow} | UnitId=[{unit.InstanceId}] | GoldAfterBuy={p.Gold}");


    // ON-PLAY TRIGGERS
    AbilitySystem.ExecuteOnPlay(p, GetOpponent(p), unit, this);


    // Update HUD after AI purchase
    if (battleHUD != null)
    {
        battleHUD.RefreshArmyStats(playerA, playerB);
        battleHUD.UpdateBothGold(playerA, playerB);
    }


    boughtCard = chosen;
    return true;
}


    #endregion


    #region Rearrangement Phase
    /// <summary>
    /// 
    /// Rearrangement phase: sorts rows by HP and optionally swaps Front/Middle.
    /// Currently uses simple heuristics (future: let players manually rearrange).
    /// </summary>
    /// <summary>
/// Rearrangement phase: sorts rows by HP and optionally swaps Front/Middle.
/// NOW WITH VISUAL UPDATES!
/// </summary>
private void RearrangementPhase(Player p)
{


    // LAND MINES: block AI rearrangement too
if (SpecialCards.CannotRearrangeThisRound != null &&
    SpecialCards.CannotRearrangeThisRound(p))
{
    Debug.Log($"[LAND MINES] {p.Name} cannot rearrange this round (AI).");
    return;
}


    if (p == null) return;


    PlayerSide side = (p == playerA) ? PlayerSide.A : PlayerSide.B;


    // 0) Always enforce row purity before doing anything else.
    // This guarantees Infantry-only, Vehicle/Ranged together, Air-only.
    EnforceRowPurity(p);


    // 1) Sort the PHYSICAL rows by HP (highest first).
    // After EnforceRowPurity, the physical rows contain the correct semantic groups.
    SortRowByHPDescending(p, RowType.Front);
    SortRowByHPDescending(p, RowType.Middle);
    SortRowByHPDescending(p, RowType.Back);


    // 2) Decide whether to swap Front/Middle (your heuristic).
    int frontHP = SumRowHP(p, RowType.Front);
    int middleHP = SumRowHP(p, RowType.Middle);


    bool didSwap = false;


   if (middleHP > frontHP + 10)
{
    didSwap = true;


    // Swap in simulation (SwapRows should also swap the semantic layout via SwapInfantryVehicleLayout)
    SwapRows(p, RowType.Front, RowType.Middle);


    Debug.Log($"{p.Name} swaps Front and Middle rows (heuristic).");


    // After swapping rows, enforce purity again as a safety net.
    EnforceRowPurity(p);


    // Tell FieldSpawner the new layout (Front/Middle meaning changed)
    if (fieldSpawner != null)
    {
        // ✅ layout must be fetched AFTER SwapRows (because SwapRows updates rowLayoutByPlayer)
        if (rowLayoutByPlayer.TryGetValue(p, out RowLayout layout))
        {
            // If infantry group currently occupies the FRONT physical row, then FRONT is infantry, otherwise FRONT is vehicle/ranged.
            UnitType frontType  = (layout.infantryRow == RowType.Front)  ? UnitType.Infantry : UnitType.Vehicle;
            UnitType middleType = (layout.infantryRow == RowType.Middle) ? UnitType.Infantry : UnitType.Vehicle;


fieldSpawner.SetRowLayout(side, frontType, middleType, UnitType.Air);        }
        else
        {
            Debug.LogWarning($"[{p.Name}] rowLayoutByPlayer missing entry; cannot update FieldSpawner row layout.");
        }
    }
}




    // 3) FINAL VISUAL SYNC (ONE PASS)
    // MoveUnitToSlot uses the unit's semantic UnitType and rowLayoutMap to pick the correct PHYSICAL row.
    if (fieldSpawner != null)
    {
        void SyncRow(RowType rowType)
        {
            var units = p.Rows[rowType].Units;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == null) continue;
                fieldSpawner.MoveUnitToSlot(u.InstanceId, i, animate: true, duration: 0.3f);
            }
        }


        SyncRow(RowType.Front);
        SyncRow(RowType.Middle);
        SyncRow(RowType.Back);
    }


    Debug.Log($"{p.Name} post-rearrange (swap={didSwap}): " +
              $"FrontHP={SumRowHP(p, RowType.Front)} " +
              $"MiddleHP={SumRowHP(p, RowType.Middle)} " +
              $"BackHP={SumRowHP(p, RowType.Back)}");


}








    private void SortRowByHPDescending(Player p, RowType row)
    {
        p.Rows[row].Units.Sort((u1, u2) => u2.HP.CompareTo(u1.HP));
    }


    private int SumRowHP(Player p, RowType row)
    {
        int total = 0;
        foreach (var u in p.Rows[row].Units)
            if (u.IsAlive) total += u.HP;
        return total;
    }
// Swaps two PHYSICAL rows in the semantic mapping (infantryRow/vehicleRow/airRow).
private void SwapLayoutRows(Player p, RowType a, RowType b)
{
    if (p == null) return;
    if (!rowLayoutByPlayer.TryGetValue(p, out var layout)) return;


    layout.infantryRow = SwapIfMatches(layout.infantryRow, a, b);
    layout.vehicleRow  = SwapIfMatches(layout.vehicleRow,  a, b);
    layout.airRow      = SwapIfMatches(layout.airRow,      a, b);


    rowLayoutByPlayer[p] = layout;


    static RowType SwapIfMatches(RowType current, RowType aRow, RowType bRow)
    {
        if (current == aRow) return bRow;
        if (current == bRow) return aRow;
        return current;
    }
}
// Swaps the semantic layout mapping for ANY physical rows (Front/Middle/Back).
private void SwapRowLayout(Player p, RowType a, RowType b)
{
    if (p == null) return;
    if (!rowLayoutByPlayer.TryGetValue(p, out var layout)) return;
    if (a == b) return;


    // For each semantic group, if it currently lives in row a, move it to b, and vice versa.
    layout.infantryRow = SwapIfMatches(layout.infantryRow, a, b);
    layout.vehicleRow  = SwapIfMatches(layout.vehicleRow,  a, b);
    layout.airRow      = SwapIfMatches(layout.airRow,      a, b);


    rowLayoutByPlayer[p] = layout;


    static RowType SwapIfMatches(RowType current, RowType aRow, RowType bRow)
    {
        if (current == aRow) return bRow;
        if (current == bRow) return aRow;
        return current;
    }
}
private void SwapRows(Player p, RowType a, RowType b)
{
    if (a == b) return;


    // Swap semantic mapping for ANY physical row swap (Front/Middle/Back)
    SwapRowLayout(p, a, b); // or SwapLayoutRows(p,a,b) — pick ONE helper, not both


    var listA = p.Rows[a].Units;
    var listB = p.Rows[b].Units;


    var temp = new List<Unit>(listA);


    listA.Clear();
    listA.AddRange(listB);


    listB.Clear();
    listB.AddRange(temp);
}






    private void EnforceRowPurity(Player p)
{
    RowLayout layout = rowLayoutByPlayer[p];


    // Collect all units from all rows
    List<Unit> all = new();
    foreach (var kvp in p.Rows)
        all.AddRange(kvp.Value.Units);


    // Clear rows
    p.Rows[RowType.Front].Units.Clear();
    p.Rows[RowType.Middle].Units.Clear();
    p.Rows[RowType.Back].Units.Clear();


    // Re-distribute to correct row based on TYPE rules
    foreach (Unit u in all)
    {
        if (u == null) continue;


        RowType target;
        if (u.Type == UnitType.Infantry) target = layout.infantryRow;
        else if (u.Type == UnitType.Air) target = layout.airRow;
        else if (IsVehicleish(u)) target = layout.vehicleRow;
        else target = layout.infantryRow;


        p.Rows[target].Units.Add(u);
    }
}


private IEnumerator RearrangementPhase_Manual(Player p, int round)
{
    // LAND MINES: block rearrangement for this player this round
if (SpecialCards.CannotRearrangeThisRound != null &&
    SpecialCards.CannotRearrangeThisRound(p))
{
    Debug.Log($"[LAND MINES] {p.Name} cannot rearrange this round.");
    yield break; // skip manual rearrangement entirely
}


  
    if (rearrangeUI == null)
    {
        Debug.LogError("[REARRANGE] RearrangementUI_SwapRows not assigned!");
        // Fallback to auto so your game doesn't hard break
        RearrangementPhase(p);
        yield break;
    }


    bool done = false;


    // Enable your drag controller gate (you said you already did steps 1-5)
    IsManualRearrangeActive = true;


   rearrangeUI.Begin(
    swapRows: (rowA, rowB) =>
    {
        ApplyRowSwap_PlayerA(rowA, rowB);


        if (battleHUD != null)
            battleHUD.RefreshArmyStats(playerA, playerB);
    },
    done: () => done = true
);






    // Optional: Small delay so phase change feels intentional
    yield return new WaitForSeconds(0.1f);


    // Wait for Done
    yield return new WaitUntil(() => done);


    // Cleanup
    IsManualRearrangeActive = false;
    rearrangeUI.End();


    // Short delay to let last animations settle
    yield return new WaitForSeconds(0.15f);
}
private void ApplyRowSwap_PlayerA(RowType rowA, RowType rowB)
{
    // 1) Swap in simulation (also updates rowLayoutByPlayer via SwapLayoutRows)
    SwapRows(playerA, rowA, rowB);


    // 2) Sync FieldSpawner mapping BEFORE moving visuals
ApplySpawnerRowLayout(playerA, PlayerSide.A);


    // 3) Build unitId arrays AFTER swap
    int[] idsA = playerA.Rows[rowA].Units.ConvertAll(u => u.InstanceId).ToArray();
    int[] idsB = playerA.Rows[rowB].Units.ConvertAll(u => u.InstanceId).ToArray();


    // 4) Swap visuals
    if (fieldSpawner != null)
    {
        fieldSpawner.SwapRowVisuals(
            PlayerSide.A,
            rowA,
            rowB,
            idsA,
            idsB,
            animate: true,
            duration: 0.35f
        );
    }
    ApplySpawnerRowLayout(playerA, PlayerSide.A);




    Debug.Log($"[REARRANGE] Player A swapped {rowA} <-> {rowB}");
}






    #endregion


    // ========================================
    // HELPER METHODS
    // ========================================


    #region Utility
    /// <summary>
    /// Applies global test multipliers to a unit (HP and ATK).
    /// Used for balancing tests without editing card assets.
    /// </summary>
    /// 
    /// 
    /// 
   private void RemoveUnitQuiet(Player owner, Unit unit, RowType rowType)
{
    // 1) Remove from simulation row
    owner.Rows[rowType].Units.Remove(unit);


    // 2) Remove from world visuals + free slot
    if (fieldSpawner != null)
        fieldSpawner.DestroyUnitVisual(unit.InstanceId);
}
private bool TryFindUnitById(Player owner, int unitId, out Unit found, out RowType foundRow)
{
    found = null;
    foundRow = default;


    foreach (var kvp in owner.Rows)
    {
        RowType rowType = kvp.Key;
        var units = kvp.Value.Units;


        for (int i = 0; i < units.Count; i++)
        {
            Unit u = units[i];
            if (u == null) continue;


            if (u.InstanceId == unitId)
            {
                found = u;
                foundRow = rowType;
                return true;
            }
        }
    }


    return false;
}


private void StampOwnerAndDefinition(Player p)
{
    if (p == null) return;


    // Hand
    if (p.Hand != null)
    {
        for (int i = 0; i < p.Hand.Count; i++)
        {
            var c = p.Hand[i];
            if (c == null) continue;


            c.Owner = p;


            // If Definition wasn't stamped during ToRuntimeCard(), derive it from name lookup.
            if (c.Definition == null && defByName != null && !string.IsNullOrEmpty(c.Name) && defByName.TryGetValue(c.Name, out var def))
                c.Definition = def;
        }
    }
}

private void ApplyCommanderStartingOverrides(Player p, CommanderDefinition def)
{
    if (p == null || def == null) return;

    if (def.overrideStartingHP)
        p.CommanderHP = def.startingHP;

    if (def.overrideStartingGold)
        p.Gold = def.startingGold;
}

/// <summary>
/// Implement this using your existing visual tracking (InstanceId -> GameObject).
/// If you already have a method that removes the visual, call that instead.
/// </summary>
private void DespawnWorldVisual(Unit unit)
{
    // TODO: hook into your existing world visual management
    // Example if you have something like:
    // fieldSpawner.RemoveVisual(unit.InstanceId);
    // OR a dictionary: worldByUnitId.Remove(...)
    Debug.Log($"[VISUAL] DespawnWorldVisual for UnitId={unit.InstanceId} (implement hookup).");
}


 private Unit FindHighestHpUnitInRow(Player p, RowType rowType)
{
    Unit best = null;
    int bestHp = -1;


    var units = p.Rows[rowType].Units;
    for (int i = 0; i < units.Count; i++)
    {
        Unit u = units[i];
        if (u == null || !u.IsAlive) continue;


        if (u.HP > bestHp)
        {
            bestHp = u.HP;
            best = u;
        }
    }


    return best;
}
//updated carousel refresh that isnt so snappy and crappy looking
public void RefreshBuyHandUI(Player p, bool visualsOnly = false)
{
    if (buyUI == null) return;
    if (p != playerA) return;


    if (visualsOnly)
        buyUI.RefreshHandVisualsOnly();
    else
        buyUI.RefreshCarousel();
}


private Unit FindHighestHpUnitAnywhere(Player p, out RowType foundRow)
{
    foundRow = default;
    Unit best = null;
    int bestHp = -1;


    foreach (var kvp in p.Rows)
    {
        RowType rowType = kvp.Key;
        var row = kvp.Value;
        if (row?.Units == null) continue;


        for (int i = 0; i < row.Units.Count; i++)
        {
            Unit u = row.Units[i];
            if (u == null || !u.IsAlive) continue;


            if (u.HP > bestHp)
            {
                bestHp = u.HP;
                best = u;
                foundRow = rowType;
            }
        }
    }


    return best;
}


    private void ApplyGlobalMultipliers(Unit u)
    {
        float hpMult = Mathf.Max(0.01f, healthMultiplier);
        float dmgMult = Mathf.Max(0f, damageMultiplier);


        int newAtk = Mathf.RoundToInt(u.ATK * dmgMult);
        if (newAtk < 0) newAtk = 0;
        u.ATK = newAtk;


        int newMax = Mathf.RoundToInt(u.MaxHP * hpMult);
        if (newMax < 1) newMax = 1;


        u.MaxHP = newMax;
        u.HP = newMax;
    }
private static bool IsVehicleish(Unit u) =>
    u != null && (u.Type == UnitType.Vehicle || u.Type == UnitType.Ranged || u.CountsAsVehicle);


private RowType GetPlacementRow(Player p, UnitType type)
{
    RowLayout layout = rowLayoutByPlayer[p];


    if (type == UnitType.Infantry) return layout.infantryRow;
    if (type == UnitType.Air) return layout.airRow;


    // Vehicles + Ranged always go to the vehicle group row
    if (type == UnitType.Vehicle || type == UnitType.Ranged) return layout.vehicleRow;


    return layout.infantryRow;
}
private void SwapInfantryVehicleLayout(Player p)
{
    if (p == null) return;
    if (!rowLayoutByPlayer.TryGetValue(p, out var layout)) return;


    (layout.infantryRow, layout.vehicleRow) = (layout.vehicleRow, layout.infantryRow);
    rowLayoutByPlayer[p] = layout;
}


private void ResetRowLayoutToDefault(Player p)
{
    if (p == null) return;
    if (!rowLayoutByPlayer.TryGetValue(p, out var layout)) return;


    layout.infantryRow = RowType.Front;
    layout.vehicleRow  = RowType.Middle;
    layout.airRow      = RowType.Back;


    rowLayoutByPlayer[p] = layout;
}
/// <summary>
/// If the player's board is empty, reset semantic row layout to defaults.
/// Useful to avoid confusion when all units died and next buys "seem" to go to the wrong row.
/// </summary>
private void ResetLayoutIfBoardEmpty(Player p, PlayerSide side)
{
    if (p == null) return;


    int totalUnits =
        p.Rows[RowType.Front].Units.Count +
        p.Rows[RowType.Middle].Units.Count +
        p.Rows[RowType.Back].Units.Count;


    if (totalUnits > 0) return;


    ResetRowLayoutToDefault(p);
    ApplySpawnerRowLayout(p, side);


    Debug.Log($"[{p.Name}] Board empty → row layout reset to default (Infantry=Front, Vehicle=Middle, Air=Back).");
}


private UnitType GetSemanticTypeLivingInRow(RowLayout layout, RowType physicalRow)
{
    if (layout.infantryRow == physicalRow) return UnitType.Infantry;
    if (layout.vehicleRow  == physicalRow) return UnitType.Vehicle;
    if (layout.airRow      == physicalRow) return UnitType.Air;


    // Fallback (should never happen if layout is valid)
    return UnitType.Infantry;
}
private void ApplySpawnerRowLayout(Player p, PlayerSide side)
{
    if (fieldSpawner == null) return;
    if (!rowLayoutByPlayer.TryGetValue(p, out var layout)) return;


    UnitType front  = GetSemanticTypeLivingInRow(layout, RowType.Front);
    UnitType middle = GetSemanticTypeLivingInRow(layout, RowType.Middle);
    UnitType back   = GetSemanticTypeLivingInRow(layout, RowType.Back);


    fieldSpawner.SetRowLayout(side, front, middle, back);
}


    /// <summary>
    /// Finds the most recently placed unit for a player (highest InstanceId).
    /// Used to get spawn position for card flight animation.
    /// </summary>
    private Unit GetMostRecentlyPlacedUnit(Player player)
    {
        Unit mostRecent = null;
        int highestId = -1;


        foreach (var rowKvp in player.Rows)
        {
            foreach (var unit in rowKvp.Value.Units)
            {
                if (unit.InstanceId > highestId)
                {
                    highestId = unit.InstanceId;
                    mostRecent = unit;
                }
            }
        }


        return mostRecent;
    }


    
// Removes a unit from whichever row currently contains it.
// Returns true if removed, and outputs the row it was found in.
private bool RemoveUnitFromAnyRow(Player owner, Unit unit, out RowType foundRow)
{
    foreach (var kvp in owner.Rows)
    {
        if (kvp.Value.Units.Remove(unit))
        {
            foundRow = kvp.Key;
            return true;
        }
    }


    foundRow = RowType.Front;
    return false;
}


   
   
    /// <summary>
    /// Returns the opponent of a given player.
    /// </summary>
    private Player GetOpponent(Player p)
    {
        return (p == playerA) ? playerB : playerA;
    }


    /// <summary>
    /// Checks if the battle is over (either commander dead).
    /// </summary>
    private bool IsBattleOver()
    {
        return playerA.CommanderHP <= 0 || playerB.CommanderHP <= 0;
    }


    /// <summary>
    /// Destroys a unit visually AND in simulation.
    /// Use this for any ability that destroys units (on-play or combat).
    /// Ensures VFX, card flight, and field spawner cleanup all happen correctly.
    /// </summary>
private void DestroyUnitWithEffects(Player owner, Unit unit, RowType rowHint)
{
    if (unit == null) return;


    // Get position BEFORE removing from field
    Vector3 position = Vector3.zero;
    if (fieldSpawner != null)
        position = fieldSpawner.GetUnitPosition(unit.InstanceId);


    // Remove from simulation (ROW SAFE)
    if (!RemoveUnitFromAnyRow(owner, unit, out var actualRow))
    {
        Debug.LogWarning($"[DESTROY] Unit [{unit.InstanceId}] {unit.Name} was not found in any row (ghost?)");
    }


    unit.HP = 0;
    owner.DiscardPile.Add(unit);


    // Trigger visual effects
    if (combatVFXSpawner != null && position != Vector3.zero)
    {
        combatVFXSpawner.SpawnDeathExplosion(position, unit.InstanceId, triggerCardFlight: true);
    }
    else if (fieldSpawner != null)
    {
        fieldSpawner.DestroyUnitVisual(unit.InstanceId);
    }


    Debug.Log($"[DESTROY] Unit [{unit.InstanceId}] {unit.Name} destroyed with effects (Row={actualRow})");
}


    #endregion


    private void SanitizeRows(Player p, string label)
{
    foreach (var kvp in p.Rows)
    {
        var rowType = kvp.Key;
        var list = kvp.Value.Units;


        // Remove nulls + dead
        list.RemoveAll(u => u == null || !u.IsAlive);


        // Remove duplicate instance IDs (ghost duplicates)
        var seen = new HashSet<int>();
        for (int i = list.Count - 1; i >= 0; i--)
        {
            int id = list[i].InstanceId;
            if (!seen.Add(id))
            {
                Debug.LogWarning($"[SANITIZE] Removed duplicate UnitId={id} from {p.Name} {rowType} ({label})");
                list.RemoveAt(i);
            }
        }
    }
}




    #region World Visual Spawning
    /// <summary>
    /// Spawns a world-space visual prefab for a bought unit.
    /// Uses UnitCardDefinition lookup to find the correct prefab.
    /// </summary>
    private void SpawnWorldVisualForBoughtUnit(Player buyer, Unit unit, UnitCard chosen)
    {
        if (fieldSpawner == null) return;
        if (buyer == null || unit == null || chosen == null) return;


        if (defByName == null || !defByName.TryGetValue(chosen.Name, out var def) || def == null)
        {
            Debug.LogWarning($"[WORLD] No UnitCardDefinition found for '{chosen.Name}'. Did you add it to cardDefinitions?");
            return;
        }


        var side = (buyer == playerA) ? PlayerSide.A : PlayerSide.B;
        
        // IMPORTANT: Pass player context so FieldSpawner knows about row swaps
        fieldSpawner.TrySpawn(def, side, unit.InstanceId, buyer);
        fieldSpawner.InitUnitHealthBar(unit.InstanceId, unit.MaxHP, unit.HP);

    }
    #endregion


    #region Debug Logging
    /// <summary>
    /// Logs the current state of a player's board (all three rows).
    /// </summary>
    private void LogBoardState(Player p, string label)
    {
        string RowDump(RowType rowType)
        {
            var units = p.Rows[rowType].Units;
            if (units.Count == 0) return "(empty)";


            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                sb.Append($"[{u.InstanceId}] {u.Name} HP={u.HP} ATK={u.ATK}");
                if (i < units.Count - 1) sb.Append(" | ");
            }
            return sb.ToString();
        }


        Debug.Log(
            $"[BOARD] {label} :: {p.Name}\n" +
            $"  Front : {RowDump(RowType.Front)}\n" +
            $"  Middle: {RowDump(RowType.Middle)}\n" +
            $"  Back  : {RowDump(RowType.Back)}"
        );
    }


    /// <summary>
    /// Dumps all events from a RoundReport to the console (for debugging).
    /// </summary>
    private void DumpRoundReport(RoundReport rr)
    {
        int roundNum = -1;
        if (rr != null && rr.Events != null && rr.Events.Count > 0)
            roundNum = rr.Events[0].RoundNumber;


        Debug.Log($"===== ROUND REPORT (Round {roundNum}) =====");


        foreach (var e in rr.Events)
        {
            if (!string.IsNullOrEmpty(e.Note))
                Debug.Log(e.Note);
            else
                Debug.Log($"Event: {e.Type}");
        }


        Debug.Log($"===== END ROUND REPORT (Round {roundNum}) =====");
    }


    /// <summary>
    /// Helper to check if unit order changed after sorting.
    /// </summary>
/// <summary>
/// Helper to check if unit order changed after sorting.
/// </summary>
private bool HasOrderChanged(List<Unit> currentUnits, List<int> originalIds)
{
    if (currentUnits.Count != originalIds.Count) return true;


    for (int i = 0; i < currentUnits.Count; i++)
    {
        if (currentUnits[i].InstanceId != originalIds[i])
            return true;
    }


    return false;
}
    #endregion


    // ========================================
    // IOnPlayApi IMPLEMENTATION
    // ========================================
    // These methods are called by AbilitySystem when on-play abilities trigger.


    #region On-Play Ability Handlers
    /// <summary>
    /// ON-PLAY: Duplicates a unit by finding another copy in the deck and playing it.
    /// Used by: DuplicateSelfFromDeckOnPlay ability.
    /// </summary>
    public bool TryPlayCopyFromDeck(Player owner, Unit sourceUnit)
    {
        // 1) Find another copy in deck
        UnitCard copyCard = owner.Deck.TryRemoveFirstMatchingFromDeck(
            c => c.Name == sourceUnit.Name
        );


        if (copyCard == null)
            return false;


        // 2) Check row space - use FindRowForUnitType to account for swaps
RowType row = GetPlacementRow(owner, copyCard.Type);


        if (owner.Rows[row].Units.Count >= maxUnitsPerRow)
        {
            // No space → discard card safely
            owner.Deck.Discard(copyCard);
            return false;
        }


        // 3) Create and place
        Unit copyUnit = copyCard.CreateUnitInstance();
        ApplyGlobalMultipliers(copyUnit);
        owner.Rows[row].Units.Add(copyUnit);


        owner.Deck.Discard(copyCard);


        Debug.Log(
            $"[ON-PLAY] {owner.Name} plays extra copy of {copyUnit.Name} " +
            $"-> {row} | UnitId=[{copyUnit.InstanceId}]"
        );


        // 4) IMPORTANT: Prevent infinite loops
        if (copyUnit.Ability != UnitAbilityType.DuplicateSelfFromDeckOnPlay)
        {
            AbilitySystem.ExecuteOnPlay(
                owner,
                GetOpponent(owner),
                copyUnit,
                this
            );
        }


        return true;
    }


    private IEnumerator KamikazeTargetingCoroutine(Player owner, Player enemy)
{
    kamikazeOwner = owner;
    kamikazeEnemy = enemy;


    selectedFriendlyAir = null;
    selectedEnemyUnit = null;
    kamikazeDone = false;


    targetMode = TargetMode.Kamikaze_SelectFriendlyAir;


    buyUI.SetHandSummary("Kamikaze: Click a friendly AIR unit, then click an enemy unit.");


    // Wait until the player completes both clicks
    yield return new WaitUntil(() => kamikazeDone);


    // Safety
    if (selectedFriendlyAir == null || selectedEnemyUnit == null)
    {
        Debug.LogWarning("[KAMIKAZE] Targeting ended without valid selections.");
        yield break;
    }


    // Execute effect using your safe helper
    DestroyUnitWithEffects(enemy, selectedEnemyUnit, selectedEnemyRow);
    DestroyUnitWithEffects(owner, selectedFriendlyAir, selectedFriendlyAirRow);


    buyUI.SetHandSummary("Kamikaze executed!");
}


public void NotifyUnitClicked(UnitWorldHandle handle)
{
    if (handle == null) return;


    // Only respond when targeting is active
    if (targetMode == TargetMode.None) return;


    // Resolve which player this clicked unit belongs to
    Player clickedOwner = (handle.Side == PlayerSide.A) ? playerA : playerB;


    // Find the Unit + Row from the UnitId
    if (!TryFindUnitById(clickedOwner, handle.UnitId, out Unit unit, out RowType row))
        return;


    // --- Kamikaze step 1: select friendly air unit ---
    if (targetMode == TargetMode.Kamikaze_SelectFriendlyAir)
    {
        if (clickedOwner != kamikazeOwner)
        {
            Debug.Log("[KAMIKAZE] Click a FRIENDLY plane first.");
            return;
        }


        if (unit.Type != UnitType.Air)
        {
            Debug.Log("[KAMIKAZE] First selection must be an AIR unit.");
            return;
        }


        selectedFriendlyAir = unit;
        selectedFriendlyAirRow = row;


        Debug.Log($"[KAMIKAZE] Selected friendly air: {unit.Name} (UnitId={unit.InstanceId})");


        targetMode = TargetMode.Kamikaze_SelectEnemy;
        return;
    }


    // --- Kamikaze step 2: select enemy unit ---
    if (targetMode == TargetMode.Kamikaze_SelectEnemy)
    {
        if (clickedOwner != kamikazeEnemy)
        {
            Debug.Log("[KAMIKAZE] Now click an ENEMY unit.");
            return;
        }


        selectedEnemyUnit = unit;
        selectedEnemyRow = row;


        Debug.Log($"[KAMIKAZE] Selected enemy target: {unit.Name} (UnitId={unit.InstanceId})");


        kamikazeDone = true;
        targetMode = TargetMode.None;
    }
}


    /// <summary>
    /// ON-PLAY: Destroys the strongest enemy vehicle if the enemy vehicle row is full.
    /// Used by: DestroyStrongestEnemyVehicleIfEnemyVehicleRowIsFullOnPlay ability.
    /// </summary>
   public void TryDestroyStrongestEnemyVehicleIfRowFull(Player owner, Player enemy)
{
        SanitizeRows(enemy, "pre-destroy-check");


    if (owner == null || enemy == null)
    {
        Debug.LogWarning("[ON-PLAY] destroy-check: owner or enemy is null.");
        return;
    }


    if (owner == enemy)
    {
        Debug.LogError("[ON-PLAY] destroy-check: owner == enemy (logic error).");
        return;
    }


    int capacity = maxUnitsPerRow;


    // "Vehicle-ish" includes Vehicles, Ranged, and anything flagged CountsAsVehicle
    bool IsVehicleish(Unit u) =>
        u != null && (u.Type == UnitType.Vehicle || u.Type == UnitType.Ranged || u.CountsAsVehicle);


    // --------------------------------------------------
    // 1) Enemy must have at least ONE vehicle-ish anywhere
    // --------------------------------------------------
    bool enemyHasAnyVehicleish = false;


    foreach (var kvp in enemy.Rows)
    {
        foreach (var u in kvp.Value.Units)
        {
            if (IsVehicleish(u))
            {
                enemyHasAnyVehicleish = true;
                break;
            }
        }
        if (enemyHasAnyVehicleish)
            break;
    }


    if (!enemyHasAnyVehicleish)
    {
        Debug.Log($"[ON-PLAY] {owner.Name}: enemy has no vehicle/ranged units anywhere. Ability does nothing.");
        return;
    }


    // --------------------------------------------------
    // 2) Find which row enemy vehicles live in
    // --------------------------------------------------
RowType vehicleRow = GetPlacementRow(enemy, UnitType.Vehicle);


    // --------------------------------------------------
    // 3) Count ONLY vehicle-ish in that row
    // --------------------------------------------------
    int vehicleishCountInRow = 0;


    foreach (var u in enemy.Rows[vehicleRow].Units)
    {
        if (IsVehicleish(u))
            vehicleishCountInRow++;
    }


    Debug.Log(
        $"[ON-PLAY] destroy-check: owner={owner.Name} enemy={enemy.Name} " +
        $"vehicleRow={vehicleRow} vehicleishInRow={vehicleishCountInRow}/{capacity}"
    );


    if (vehicleishCountInRow < capacity)
    {
        Debug.Log($"[ON-PLAY] {owner.Name}: enemy vehicle row not full. Ability does nothing.");
        
        return;
    }
    Debug.Log($"[ON-PLAY] {enemy.Name} {vehicleRow} units: " +
          string.Join(", ", enemy.Rows[vehicleRow].Units.Select(u => u == null ? "null" : $"{u.InstanceId}:{u.Type}:{u.HP}")));




    // --------------------------------------------------
    // 4) Pick strongest enemy vehicle-ish in that row
    // Strength = ATK primary, HP tiebreaker
    // --------------------------------------------------
    Unit strongest = null;
    int bestScore = int.MinValue;


    foreach (var u in enemy.Rows[vehicleRow].Units)
    {
        if (!IsVehicleish(u)) continue;


        int score = (u.ATK * 1000) + u.HP;


        if (score > bestScore)
        {
            bestScore = score;
            strongest = u;
        }
    }


    if (strongest == null)
    {
        Debug.Log($"[ON-PLAY] {owner.Name}: no valid vehicle/ranged unit found to destroy.");
        return;
    }


    Debug.Log(
        $"[ON-PLAY] {owner.Name} DESTROYS enemy VEHICLE/RANGED: " +
        $"[{strongest.InstanceId}] {strongest.Name}"
    );


DestroyUnitWithEffects(enemy, strongest, vehicleRow);
    Debug.Log($"[ON-PLAY] Vehicle destroy check PASSED: row={vehicleRow} count={vehicleishCountInRow}/{capacity}");
    


}




    /// <summary>
    /// ON-PLAY: Marks a random enemy unit for 2x vulnerability.
    /// Used by: TargetMark ability.
    /// </summary>
    public void ApplyTargetMarkOnPlay(Player owner, Player enemy, Unit markerUnit)
    {
        // Build list of all alive enemy units
        List<Unit> aliveEnemies = new List<Unit>();


        foreach (var row in enemy.Rows.Values)
        {
            foreach (var u in row.Units)
            {
                if (u.IsAlive)
                    aliveEnemies.Add(u);
            }
        }


        if (aliveEnemies.Count == 0)
        {
            Debug.Log($"[ON-PLAY][TARGET] {owner.Name}'s {markerUnit.Name} tried to mark, but enemy has no alive units.");
            return;
        }


        // Pick a random target
        int index;
        if (targetMarkUseTrueRandom)
        {
            index = UnityEngine.Random.Range(0, aliveEnemies.Count);
        }
        else
        {
            index = buyRng.Next(0, aliveEnemies.Count);
        }


        Unit target = aliveEnemies[index];
        target.IsMarkedTarget = true;


        // Find the row for logging clarity
        RowType targetRow = RowType.Front;
        foreach (var kvp in enemy.Rows)
        {
            if (kvp.Value.Units.Contains(target))
            {
                targetRow = kvp.Key;
                break;
            }
        }


        Debug.Log($"[ON-PLAY][TARGET] {owner.Name}'s {markerUnit.Name} marks {enemy.Name}'s {target.Name} (Row={targetRow}, UnitId={target.InstanceId})");
    }
    #endregion


    
}



