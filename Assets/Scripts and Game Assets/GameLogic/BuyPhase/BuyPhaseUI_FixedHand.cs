using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


/// <summary>
/// BuyPhaseUI_FixedHand: UI controller for the card-buying phase with carousel support.
/// 
/// DESIGN APPROACH:
/// This UI now uses a CardCarousel for scrollable card display instead of fixed buttons.
/// The carousel shows cards horizontally with zoom/focus on the center card.
/// 
/// RESPONSIBILITIES:
/// - Displays player's hand via CardCarousel
/// - Shows current round number and gold amount
/// - Handles card selection and "pass" button clicks
/// - Displays enemy's purchase feedback
/// 
/// HOW IT WORKS:
/// 1. BattleTestRunner calls Begin() to start the buy phase
/// 2. UI displays cards in carousel and waits for player input
/// 3. Player clicks center card → callback fires with UnitCard
/// 4. BattleTestRunner handles purchase logic
/// 5. BattleTestRunner updates UI as needed
/// 6. BattleTestRunner calls End() to hide UI
/// 
/// CALLBACK PATTERN:
/// This UI uses callbacks instead of directly modifying game state.
/// This keeps the UI decoupled from game logic.
/// </summary>
public class BuyPhaseUI_FixedHand : MonoBehaviour
{
    // ========================================
    // UI REFERENCES
    // ========================================

    [Header("Root Panel")]
    [Tooltip("The main UI panel. Set inactive when not in buy phase.")]
    public GameObject rootPanel;

    [Header("Text Fields")]
    [Tooltip("Displays current round number and gold amount. Example: 'Round 2 | Gold: 15'")]
    public TMP_Text roundGoldText;

    [Tooltip("Displays hand status or feedback messages. Example: 'Click a card to buy it.'")]
    public TMP_Text handSummaryText;

    [Tooltip("Displays what the enemy bought this turn. Example: 'Enemy bought: Tank (Cost=5)'")]
    public TMP_Text enemyBoughtText;

    [Header("Buttons")]
    [Tooltip("Pass button (ends buy phase for this player).")]
    public Button passButton;

    [Header("Card Carousel")]
    [Tooltip("CardCarousel component for displaying hand")]
    public CardCarousel cardCarousel;

[SerializeField] private CardRevealOverlay revealOverlay;

public void ShowRevealOverlay(string title, List<UnitCard> cards)
{
    // Disable main hand carousel input while overlay is open
    SetHandCarouselInputEnabled(false);

    if (revealOverlay == null)
    {
        Debug.LogError("[BuyUI] revealOverlay is NULL! Assign CardRevealOverlay in Inspector on BuyPhaseUI_FixedHand.");
        SetHandCarouselInputEnabled(true); // Re-enable since we can't show overlay
        return;
    }

    Debug.Log($"[BuyUI] ShowRevealOverlay called: title='{title}', cards={cards?.Count ?? 0}");

    revealOverlay.Show(title, cards, cloneCards: true, onDismiss: () =>
    {
        // Re-enable hand carousel when overlay is dismissed
        SetHandCarouselInputEnabled(true);
    });

    // Fallback: if overlay's root didn't activate, try forcing it from here (we're on an active object)
    StartCoroutine(EnsureOverlayActiveCoroutine());
}

/// <summary>
/// Shows a reveal overlay with selection capability (for POW card selection).
/// </summary>
public void ShowRevealOverlay(string title, List<UnitCard> cards, System.Action<UnitCard> onSelect)
{
    SetHandCarouselInputEnabled(false);

    if (revealOverlay == null)
    {
        Debug.LogError("[BuyUI] revealOverlay is NULL! Assign CardRevealOverlay in Inspector.");
        SetHandCarouselInputEnabled(true);
        return;
    }

    Debug.Log($"[BuyUI] ShowRevealOverlay with selection: title='{title}', cards={cards?.Count ?? 0}");

    revealOverlay.ShowWithSelection(title, cards, cloneCards: true,
        onSelect: (selected) =>
        {
            SetHandCarouselInputEnabled(true);
            onSelect?.Invoke(selected);
        },
        onDismiss: () =>
        {
            SetHandCarouselInputEnabled(true);
        }
    );

    StartCoroutine(EnsureOverlayActiveCoroutine());
}

private System.Collections.IEnumerator EnsureOverlayActiveCoroutine()
{
    yield return null; // Wait one frame

    if (revealOverlay != null && !revealOverlay.gameObject.activeInHierarchy)
    {
        Debug.LogWarning("[BuyUI] Overlay still inactive after Show()! Forcing activation from BuyPhaseUI.");
        revealOverlay.gameObject.SetActive(true);

        // Also try to activate the overlay's transform hierarchy
        Transform t = revealOverlay.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
            {
                Debug.Log($"[BuyUI] Activating: {t.name}");
                t.gameObject.SetActive(true);
            }
            t = t.parent;
        }
    }
}


public void HideRevealOverlay()
{
    revealOverlay?.Hide();
    SetHandCarouselInputEnabled(true);
}

[SerializeField] private GameObject tooltipPanel;
[SerializeField] private TMPro.TMP_Text tooltipText;

private Coroutine tooltipRoutine;



public void ShowTooltip(string msg, float seconds = 2.5f)
{
    if (tooltipPanel == null || tooltipText == null)
    {
        Debug.LogWarning("[BuyUI] Tooltip references not set.");
        return;
    }

    tooltipText.text = msg;
    tooltipPanel.SetActive(true);

    if (tooltipRoutine != null)
        StopCoroutine(tooltipRoutine);

    tooltipRoutine = StartCoroutine(HideTooltipAfter(seconds));
}
private System.Collections.IEnumerator HideTooltipAfter(float seconds)
{
    yield return new WaitForSeconds(seconds);

    if (tooltipPanel != null)
        tooltipPanel.SetActive(false);

    tooltipRoutine = null;
}



    // ========================================
    // PRIVATE STATE
    // ========================================

    private Action<UnitCard> onCardSelected;
    private Action onPassClicked;
    private Player currentPlayer;
    private int currentGold;
    private CardVisual lastClickedVisual;

    // ========================================
    // LIFECYCLE
    // ========================================

    private void Awake()
    {
        // Wire pass button once at startup
        if (passButton != null)
            passButton.onClick.AddListener(() => onPassClicked?.Invoke());

        // Wire up carousel events
        if (cardCarousel != null)
        {
            cardCarousel.OnCardSelected += OnCarouselCardSelected;
        }
    }

    private void OnDestroy()
    {
        // Clean up carousel events
        if (cardCarousel != null)
        {
            cardCarousel.OnCardSelected -= OnCarouselCardSelected;
        }
    }

    // ========================================
    // PUBLIC API
    // ========================================

    /// <summary>
    /// Starts the buy phase UI.
    /// </summary>
    public void Begin(int round, Player player, Action<UnitCard> cardSelected, Action passClicked)
    {
        if (rootPanel != null)
            rootPanel.SetActive(true);

        currentPlayer = player;
        currentGold = player.Gold;
        onCardSelected = cardSelected;
        onPassClicked = passClicked;

        SetRoundGold(round, player.Gold);
        SetHandSummary("Scroll through cards. Click center card to buy.");
        SetEnemyBought("(none yet)");

        RefreshCarousel();
        CommanderBonus.OnEffectiveStatsChanged -= RefreshHandVisualsOnly;
CommanderBonus.OnEffectiveStatsChanged += RefreshHandVisualsOnly;

    }

public void RefreshHandVisualsOnly()
{
    if (cardCarousel != null)
        cardCarousel.RefreshVisuals();
}

public void SetHandCarouselInputEnabled(bool enabled)
{
    if (cardCarousel != null)
        cardCarousel.SetInputEnabled(enabled);
}

/// <summary>
/// Enables or disables the pass button interactivity.
/// Call this to visually indicate when the player can pass.
/// </summary>
public void SetPassButtonInteractable(bool interactable)
{
    if (passButton != null)
        passButton.interactable = interactable;
}

/// <summary>
/// Ends the buy phase UI.
/// </summary>
public void End()
{
    // Always unsubscribe first
    CommanderBonus.OnEffectiveStatsChanged -= RefreshHandVisualsOnly;

    currentPlayer = null;
    onCardSelected = null;
    onPassClicked = null;

    if (cardCarousel != null)
        cardCarousel.ClearCards();

    if (rootPanel != null)
        rootPanel.SetActive(false);
}


    /// <summary>
    /// Updates the round number and gold display.
    /// </summary>
    public void SetRoundGold(int round, int gold)
    {
        currentGold = gold; // Update cached gold
        if (roundGoldText != null)
            roundGoldText.text = $"Round {round} | Gold: {gold}";
    }

    /// <summary>
    /// Updates the hand summary message.
    /// </summary>
    public void SetHandSummary(string msg)
    {
        if (handSummaryText != null)
            handSummaryText.text = msg;
    }

    /// <summary>
    /// Updates the enemy purchase feedback.
    /// </summary>
    public void SetEnemyBought(string msg)
    {
        if (enemyBoughtText != null)
            enemyBoughtText.text = $"Enemy bought: {msg}";
    }

    /// <summary>
    /// Refreshes the carousel with current player's hand.
    /// Call this after any purchase or when the hand changes.
    /// </summary>
    public void RefreshCarousel()
    {
        if (cardCarousel == null || currentPlayer == null) 
        {
            Debug.LogWarning("[BuyUI] Cannot refresh carousel - missing references!");
            return;
        }

cardCarousel.SetCardsPreserveCenter(currentPlayer.Hand);
        
        if (handSummaryText != null)
            handSummaryText.text = $"Hand: {currentPlayer.Hand.Count} cards | Gold: {currentGold}";
    }

    /// <summary>
    /// Removes a purchased card from the carousel with flight animation to the unit's world position.
    /// </summary>
    /// <param name="card">The card that was purchased</param>
    /// <param name="worldTargetPosition">World position where the unit spawned (card flies toward this)</param>
    /// <summary>
/// Removes a purchased card from the carousel with flight animation to unit position.
/// </summary>
/// <summary>
/// Removes a purchased card from the carousel with flight animation to unit position.
/// </summary>
/// <summary>
/// Removes a purchased card from the carousel with flight animation to unit position.
/// </summary>
/// <summary>
/// Removes the purchased card from the carousel WITHOUT animation and returns the CardVisual.
/// CardFlightManager should animate it (fly -> slam -> hide -> later discard).
/// </summary>
public CardVisual RemovePurchasedCardAndReturnVisual(UnitCard card)
{
    if (cardCarousel == null) return null;

    // Prefer the actual clicked visual (works even if carousel center changes)
    CardVisual cv = cardCarousel.LastClickedVisual;
    if (cv != null && cv.cardData == card)
    {
        cardCarousel.RemoveCardNoAnimation(cv);
        return cv;
    }

    // Fallback to old behavior
    cv = cardCarousel.GetCenterCardVisual();
    if (cv != null && cv.cardData == card)
    {
        cardCarousel.RemoveCardNoAnimation(cv);
        return cv;
    }

    return null;
}


public void RemovePurchasedCard(UnitCard card, Vector3 worldTargetPosition)
{
    if (cardCarousel == null) return;
    
    CardVisual cardVisual = cardCarousel.GetCenterCardVisual();
    if (cardVisual != null && cardVisual.cardData == card)
    {
        cardCarousel.RemoveCard(cardVisual, worldTargetPosition);
    }
}


public void RemovePurchasedCard(UnitCard card)
{
    if (cardCarousel == null) return;
    
    CardVisual cardVisual = cardCarousel.GetCenterCardVisual();
    if (cardVisual != null && cardVisual.cardData == card)
    {
        cardCarousel.RemoveCard(cardVisual);
    }
}
    // ========================================
    // PRIVATE HELPERS
    // ========================================

    /// <summary>
    /// Called when a card is selected from the carousel.
    /// </summary>
    private void OnCarouselCardSelected(UnitCard card)
    {
        Debug.Log($"[BuyUI] OnCarouselCardSelected called - card={(card?.Name ?? "NULL")}, currentGold={currentGold}, onCardSelected={(onCardSelected != null ? "SET" : "NULL")}");

        if (card == null)
        {
            Debug.Log("[BuyUI] Card is null, returning");
            return;
        }

        // Check affordability before invoking callback
        if (card.Cost > currentGold)
        {
            Debug.Log($"[BuyUI] Can't afford! Cost={card.Cost} > Gold={currentGold}");
            SetHandSummary($"Not enough gold! Need {card.Cost}, have {currentGold}");
            return;
        }

        // Invoke callback - let BattleTestRunner handle the purchase logic
        Debug.Log($"[BuyUI] Invoking onCardSelected for {card.Name}");
        onCardSelected?.Invoke(card);
    }
}