using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// World Map screen where player selects the next enemy commander to fight.
/// Shows a scrollable aerial view of earth with commander markers.
/// </summary>
public class WorldMapScreen : OverworldScreen
{
    [Header("Map")]
    [Tooltip("ScrollRect containing the map image")]
    [SerializeField]
    private ScrollRect mapScrollRect;

    [Tooltip("Content transform for the map (child of ScrollRect)")]
    [SerializeField]
    private RectTransform mapContent;

    [Header("Commander Spawning")]
    [Tooltip("Prefab for commander map markers")]
    [SerializeField]
    private CommanderMapMarker markerPrefab;

    [Tooltip("Container for spawned markers")]
    [SerializeField]
    private Transform markerContainer;

    [Tooltip("Number of enemy commanders to spawn")]
    [SerializeField]
    private int commanderCount = 3;

    [Tooltip("Spawn positions on the map (normalized 0-1 coordinates)")]
    [SerializeField]
    private List<Vector2> spawnPositions = new List<Vector2>
    {
        new Vector2(0.2f, 0.5f),
        new Vector2(0.5f, 0.3f),
        new Vector2(0.8f, 0.6f)
    };

    [Header("Commander Pool")]
    [Tooltip("All available enemy commanders")]
    [SerializeField]
    private List<CommanderDefinition> availableCommanders = new List<CommanderDefinition>();

    [Header("HUD Elements")]
    [Tooltip("Display current war funds")]
    [SerializeField]
    private TMP_Text warFundsText;

    [Tooltip("Button to open barracks")]
    [SerializeField]
    private Button barracksButton;

    [Tooltip("Button to open war cabinet (for testing/debug)")]
    [SerializeField]
    private Button warCabinetButton;

    [Header("Commander Info Panel")]
    [Tooltip("Panel showing selected commander info")]
    [SerializeField]
    private GameObject commanderInfoPanel;

    [SerializeField]
    private TMP_Text commanderNameText;

    [SerializeField]
    private TMP_Text commanderDescriptionText;

    [SerializeField]
    private Image commanderPortraitImage;

    [SerializeField]
    private Button fightButton;

    [SerializeField]
    private Button closeInfoButton;

    private List<CommanderMapMarker> activeMarkers = new List<CommanderMapMarker>();
    private CommanderDefinition selectedCommander;

    protected override void Awake()
    {
        base.Awake();

        // Wire up buttons
        if (barracksButton != null)
            barracksButton.onClick.AddListener(OnBarracksClicked);

        if (warCabinetButton != null)
            warCabinetButton.onClick.AddListener(OnWarCabinetClicked);

        if (fightButton != null)
            fightButton.onClick.AddListener(OnFightClicked);

        if (closeInfoButton != null)
            closeInfoButton.onClick.AddListener(OnCloseInfoClicked);

        // Hide info panel initially
        if (commanderInfoPanel != null)
            commanderInfoPanel.SetActive(false);
    }

    protected override void OnShow()
    {
        RefreshWarFundsDisplay();
        SpawnRandomCommanders();
        HideCommanderInfo();
    }

    protected override void OnHide()
    {
        ClearMarkers();
        HideCommanderInfo();
    }

    private void RefreshWarFundsDisplay()
    {
        if (warFundsText != null && PlayerProgressData.Instance != null)
        {
            warFundsText.text = $"War Funds: {PlayerProgressData.Instance.warFunds}";
        }
    }

    private void SpawnRandomCommanders()
    {
        ClearMarkers();

        if (markerPrefab == null)
        {
            Debug.LogError("[WorldMapScreen] markerPrefab is not assigned! Use Window > Army Prototype > World Map Setup to create one.");
            return;
        }

        if (markerContainer == null)
        {
            Debug.LogError("[WorldMapScreen] markerContainer is not assigned! Drag the CommanderMarkerContainer from hierarchy.");
            return;
        }

        if (availableCommanders == null || availableCommanders.Count == 0)
        {
            Debug.LogError("[WorldMapScreen] availableCommanders list is empty! Add CommanderDefinition assets (Albert, Roy, etc.) to the list.");
            return;
        }

        // Shuffle commanders
        var shuffled = new List<CommanderDefinition>(availableCommanders);
        ShuffleList(shuffled);

        // Spawn up to commanderCount markers
        int spawnCount = Mathf.Min(commanderCount, shuffled.Count, spawnPositions.Count);

        if (spawnCount == 0)
        {
            Debug.LogWarning("[WorldMapScreen] spawnCount is 0. Check commanderCount, availableCommanders.Count, and spawnPositions.Count");
            return;
        }

        for (int i = 0; i < spawnCount; i++)
        {
            var marker = Instantiate(markerPrefab, markerContainer);
            marker.Initialize(shuffled[i], GetWorldPositionFromNormalized(spawnPositions[i]));
            marker.OnClicked += OnCommanderMarkerClicked;
            activeMarkers.Add(marker);
        }

        Debug.Log($"[WorldMapScreen] Spawned {spawnCount} commander markers.");
    }

    private void ClearMarkers()
    {
        foreach (var marker in activeMarkers)
        {
            if (marker != null)
            {
                marker.OnClicked -= OnCommanderMarkerClicked;
                Destroy(marker.gameObject);
            }
        }
        activeMarkers.Clear();
    }

    private Vector2 GetWorldPositionFromNormalized(Vector2 normalized)
    {
        if (mapContent == null) return Vector2.zero;

        Rect rect = mapContent.rect;
        return new Vector2(
            rect.xMin + normalized.x * rect.width,
            rect.yMin + normalized.y * rect.height
        );
    }

    private void OnCommanderMarkerClicked(CommanderDefinition commander)
    {
        selectedCommander = commander;
        ShowCommanderInfo(commander);
    }

    private void ShowCommanderInfo(CommanderDefinition commander)
    {
        if (commanderInfoPanel == null) return;

        commanderInfoPanel.SetActive(true);

        if (commanderNameText != null)
            commanderNameText.text = commander.displayName;

        if (commanderDescriptionText != null)
            commanderDescriptionText.text = commander.description;

        if (commanderPortraitImage != null && commander.mapPortrait != null)
            commanderPortraitImage.sprite = commander.mapPortrait;
    }

    private void HideCommanderInfo()
    {
        if (commanderInfoPanel != null)
            commanderInfoPanel.SetActive(false);

        selectedCommander = null;
    }

    private void OnBarracksClicked()
    {
        GameStateManager.Instance?.OpenBarracks();
    }

    private void OnWarCabinetClicked()
    {
        GameStateManager.Instance?.TransitionTo(GameState.WarCabinet);
    }

    private void OnFightClicked()
    {
        if (selectedCommander != null)
        {
            GameStateManager.Instance?.StartBattle(selectedCommander);
        }
    }

    private void OnCloseInfoClicked()
    {
        HideCommanderInfo();
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
