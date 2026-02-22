using System;
using UnityEngine;

/// <summary>
/// Represents the current state of the overworld meta-game.
/// </summary>
public enum GameState
{
    MainMenu,
    Battle,
    VictoryDefeat,
    WarCabinet,
    WorldMap,
    Barracks
}

/// <summary>
/// Result of a completed battle.
/// </summary>
public enum BattleResult
{
    Victory,
    Defeat
}

/// <summary>
/// Central orchestrator for the overworld meta-game system.
/// Manages state transitions and coordinates all screens.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [Header("Battle System")]
    [Tooltip("Reference to the existing battle runner")]
    public BattleTestRunner battleRunner;

    [Header("Player Data")]
    [Tooltip("Persistent player progress data")]
    public PlayerProgressData playerProgress;

    [Header("Starting Deck")]
    [Tooltip("The starting deck for new players (usually Player A's deck from BattleTestRunner)")]
    public DeckListDefinition startingDeck;

    [Header("Screen References")]
    [Tooltip("Victory/Defeat screen shown after battles")]
    public VictoryDefeatScreen victoryDefeatScreen;

    [Tooltip("War Cabinet screen for shopping")]
    public WarCabinetScreen warCabinetScreen;

    [Tooltip("World Map screen for commander selection")]
    public WorldMapScreen worldMapScreen;

    [Tooltip("Barracks screen for deck building")]
    public BarracksScreen barracksScreen;

    [Header("Reward Configuration")]
    [Tooltip("Base war funds for winning a battle")]
    public int baseVictoryReward = 100;

    [Tooltip("Base war funds for losing a battle")]
    public int baseDefeatReward = 25;

    [Tooltip("Bonus war funds per remaining commander HP on victory")]
    public float hpBonusMultiplier = 0.5f;

    [Header("Battle UI (to hide during overworld)")]
    [Tooltip("The BuyPhaseCardCarouselCanvas containing the Fight button")]
    [SerializeField]
    private GameObject buyPhaseCanvas;

    [Tooltip("Other battle-related UI to hide during overworld screens")]
    [SerializeField]
    private GameObject[] additionalBattleUI;

    [Header("State")]
    [SerializeField]
    private GameState currentState = GameState.Battle;

    public GameState CurrentState => currentState;

    /// <summary>
    /// Event fired when game state changes.
    /// </summary>
    public event Action<GameState, GameState> OnStateChanged;

    /// <summary>
    /// The commander selected for the current/next battle.
    /// </summary>
    public CommanderDefinition SelectedEnemyCommander { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        // Initialize player deck from starting deck if empty
        InitializePlayerDeckIfNeeded();

        // Initialize to starting state
        HideAllScreens();

        // Show the initial state (Battle by default for now, WorldMap when overworld is ready)
        ShowScreenForState(currentState);
    }

    /// <summary>
    /// Initialize the player's deck from the starting deck if they don't have cards yet.
    /// </summary>
    private void InitializePlayerDeckIfNeeded()
    {
        if (playerProgress == null)
        {
            Debug.LogWarning("[GameStateManager] playerProgress is not assigned!");
            return;
        }

        // Use starting deck if provided, otherwise try to get from BattleTestRunner
        DeckListDefinition deckToUse = startingDeck;

        if (deckToUse == null && battleRunner != null)
        {
            deckToUse = battleRunner.playerADeck;
            Debug.Log("[GameStateManager] No startingDeck assigned, using BattleTestRunner's playerADeck");
        }

        if (deckToUse != null)
        {
            playerProgress.InitializeFromDeckIfEmpty(deckToUse);
        }
        else
        {
            Debug.LogWarning("[GameStateManager] No starting deck available to initialize player deck!");
        }
    }

    /// <summary>
    /// Transition to a new game state.
    /// </summary>
    public void TransitionTo(GameState newState)
    {
        if (currentState == newState) return;

        var previousState = currentState;
        Debug.Log($"[GameStateManager] Transitioning from {previousState} to {newState}");

        // Hide current state's screen
        HideScreenForState(currentState);

        currentState = newState;

        // Show new state's screen
        ShowScreenForState(newState);

        OnStateChanged?.Invoke(previousState, newState);
    }

    private void HideAllScreens()
    {
        victoryDefeatScreen?.Hide();
        warCabinetScreen?.Hide();
        worldMapScreen?.Hide();
        barracksScreen?.Hide();
        SetBattleUIVisible(false);
    }

    /// <summary>
    /// Show or hide battle-related UI (Fight button, card carousel, etc.)
    /// </summary>
    private void SetBattleUIVisible(bool visible)
    {
        Debug.Log($"[GameStateManager] SetBattleUIVisible({visible}) - buyPhaseCanvas is {(buyPhaseCanvas != null ? "assigned" : "NULL")}");

        if (buyPhaseCanvas != null)
        {
            buyPhaseCanvas.SetActive(visible);
            Debug.Log($"[GameStateManager] Set buyPhaseCanvas.activeSelf = {buyPhaseCanvas.activeSelf}");
        }
        else
        {
            Debug.LogWarning("[GameStateManager] buyPhaseCanvas is NOT assigned! Drag BuyPhaseCardCarouselCanvas into the Inspector.");
        }

        if (additionalBattleUI != null)
        {
            foreach (var ui in additionalBattleUI)
            {
                if (ui != null)
                    ui.SetActive(visible);
            }
        }
    }

    private void HideScreenForState(GameState state)
    {
        switch (state)
        {
            case GameState.VictoryDefeat:
                victoryDefeatScreen?.Hide();
                break;
            case GameState.WarCabinet:
                warCabinetScreen?.Hide();
                break;
            case GameState.WorldMap:
                worldMapScreen?.Hide();
                break;
            case GameState.Barracks:
                barracksScreen?.Hide();
                break;
        }
    }

    private void ShowScreenForState(GameState state)
    {
        // Hide battle UI for all non-battle states
        bool isBattle = (state == GameState.Battle);
        SetBattleUIVisible(isBattle);

        switch (state)
        {
            case GameState.VictoryDefeat:
                victoryDefeatScreen?.Show();
                break;
            case GameState.WarCabinet:
                warCabinetScreen?.Show();
                break;
            case GameState.WorldMap:
                worldMapScreen?.Show();
                break;
            case GameState.Barracks:
                barracksScreen?.Show();
                break;
            case GameState.Battle:
                // Battle is handled by BattleTestRunner
                // Battle UI is already shown above
                break;
        }
    }

    /// <summary>
    /// Called when a battle is complete.
    /// Calculates rewards and shows victory/defeat screen.
    /// </summary>
    public void OnBattleComplete(BattleResult result, int remainingCommanderHP, int maxCommanderHP)
    {
        int warFundsEarned = CalculateBattleReward(result, remainingCommanderHP, maxCommanderHP);

        if (playerProgress != null)
        {
            playerProgress.warFunds += warFundsEarned;
            playerProgress.SaveToJson();
        }

        // Show victory/defeat screen
        if (victoryDefeatScreen != null)
        {
            victoryDefeatScreen.SetResult(result, warFundsEarned, remainingCommanderHP, maxCommanderHP);
        }

        TransitionTo(GameState.VictoryDefeat);
    }

    /// <summary>
    /// Calculate war funds earned from a battle.
    /// </summary>
    public int CalculateBattleReward(BattleResult result, int remainingHP, int maxHP)
    {
        if (result == BattleResult.Defeat)
        {
            return baseDefeatReward;
        }

        // Victory: base + HP bonus
        int hpBonus = Mathf.RoundToInt(remainingHP * hpBonusMultiplier);
        return baseVictoryReward + hpBonus;
    }

    /// <summary>
    /// Start a battle against the selected enemy commander.
    /// </summary>
    public void StartBattle(CommanderDefinition enemyCommander)
    {
        if (enemyCommander == null)
        {
            Debug.LogError("[GameStateManager] Cannot start battle: no enemy commander selected!");
            return;
        }

        SelectedEnemyCommander = enemyCommander;

        // === DIAGNOSTIC: Log full campaign state before battle ===
        LogCampaignState("STARTING BATTLE");

        // Reset wall defense for battle
        playerProgress?.ResetWallDefenseForBattle();

        // Configure battle runner with enemy commander
        if (battleRunner != null)
        {
            // Set enemy commander
            battleRunner.SetEnemyCommander(enemyCommander);

            // Set enemy deck from commander definition
            if (enemyCommander.deck != null)
            {
                battleRunner.playerBDeck = enemyCommander.deck;
            }

            // Set player deck from current deck (only if playerProgress has a deck)
            if (playerProgress != null)
            {
                var progressDeck = playerProgress.BuildRuntimeDeckList();
                if (progressDeck != null && progressDeck.entries != null && progressDeck.entries.Count > 0)
                {
                    battleRunner.playerADeck = progressDeck;
                    Debug.Log($"[GameStateManager] Using player progress deck with {progressDeck.entries.Count} entries");
                }
                else
                {
                    Debug.Log("[GameStateManager] PlayerProgress deck is empty, keeping existing playerADeck");
                }
            }

            // Transition to battle state first (shows battle UI)
            TransitionTo(GameState.Battle);

            // Then restart the battle with the new configuration
            battleRunner.RestartBattle();
        }
        else
        {
            Debug.LogError("[GameStateManager] battleRunner is null! Cannot start battle.");
            TransitionTo(GameState.Battle);
        }

        Debug.Log($"[GameStateManager] Starting battle against: {enemyCommander.displayName}");
    }

    /// <summary>
    /// Logs the full campaign state for debugging purposes.
    /// </summary>
    private void LogCampaignState(string context)
    {
        Debug.Log($"========== CAMPAIGN STATE: {context} ==========");

        // Check PlayerProgressData
        if (playerProgress == null)
        {
            Debug.LogError("[CAMPAIGN] GameStateManager.playerProgress is NULL!");
        }
        else
        {
            Debug.Log($"[CAMPAIGN] War Funds: {playerProgress.warFunds}");
            Debug.Log($"[CAMPAIGN] Deck Cards: {playerProgress.currentDeck.Count}");
            Debug.Log($"[CAMPAIGN] Collection Cards: {playerProgress.ownedCards.Count}");
            Debug.Log($"[CAMPAIGN] Equipped Doctrines: {playerProgress.equippedDoctrines.Count}");
            foreach (var d in playerProgress.equippedDoctrines)
            {
                Debug.Log($"  - Doctrine: {d?.displayName ?? "NULL"}");
            }
            Debug.Log($"[CAMPAIGN] Perk Levels: {playerProgress.perkLevels.Count}");
            foreach (var p in playerProgress.perkLevels)
            {
                Debug.Log($"  - Perk: {p.perk?.displayName ?? "NULL"} Tier {p.currentTier}");
            }
            Debug.Log($"[CAMPAIGN] Bonus HP: {playerProgress.bonusCommanderHP}, Wall: {playerProgress.wallDefense}");
        }

        // Check if PlayerProgressData.Instance matches our reference
        if (PlayerProgressData.Instance != playerProgress)
        {
            Debug.LogWarning($"[CAMPAIGN] WARNING: PlayerProgressData.Instance ({PlayerProgressData.Instance?.name ?? "NULL"}) != GameStateManager.playerProgress ({playerProgress?.name ?? "NULL"})!");
        }

        // Check DoctrineSystem
        if (DoctrineSystem.Instance == null)
        {
            Debug.LogWarning("[CAMPAIGN] DoctrineSystem.Instance is NULL!");
        }
        else
        {
            Debug.Log("[CAMPAIGN] DoctrineSystem is active");
        }

        // Check PerkSystem
        if (PerkSystem.Instance == null)
        {
            Debug.LogWarning("[CAMPAIGN] PerkSystem.Instance is NULL!");
        }
        else
        {
            Debug.Log("[CAMPAIGN] PerkSystem is active");
        }

        Debug.Log("================================================");
    }

    /// <summary>
    /// Continue from victory/defeat screen to war cabinet.
    /// </summary>
    public void ContinueToWarCabinet()
    {
        TransitionTo(GameState.WarCabinet);
    }

    /// <summary>
    /// Exit war cabinet to world map.
    /// </summary>
    public void ExitWarCabinetToWorldMap()
    {
        TransitionTo(GameState.WorldMap);
    }

    /// <summary>
    /// Open barracks from world map.
    /// </summary>
    public void OpenBarracks()
    {
        TransitionTo(GameState.Barracks);
    }

    /// <summary>
    /// Close barracks and return to world map.
    /// </summary>
    public void CloseBarracks()
    {
        TransitionTo(GameState.WorldMap);
    }

    /// <summary>
    /// Quick access to player's current war funds.
    /// </summary>
    public int GetWarFunds()
    {
        return playerProgress?.warFunds ?? 0;
    }

    /// <summary>
    /// Spend war funds if player has enough.
    /// </summary>
    public bool TrySpendWarFunds(int amount)
    {
        if (playerProgress == null) return false;
        if (playerProgress.warFunds < amount) return false;

        playerProgress.warFunds -= amount;
        return true;
    }
}
