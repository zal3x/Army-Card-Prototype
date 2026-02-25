using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// DiscardPileUI: UI controller for displaying the player's discard pile.
/// 
/// FEATURES:
/// - Shows a stack of cards with only the top card visible
/// - Click to expand and see all discarded cards
/// - Supports future "play from discard" ability selection
/// - Cards animate when added to the pile
/// 
/// DESIGN:
/// - Lives in Screen Space Canvas (bottom-left corner recommended)
/// - Uses a simplified card visual for the stack
/// - Expandable grid view for browsing all cards
/// 
/// SETUP:
/// 1. Create a UI Panel in your Canvas
/// 2. Add this component
/// 3. Create child objects for the stack display and expanded view
/// 4. Assign references in Inspector
/// </summary>
public class DiscardPileUI : MonoBehaviour
{
    // ========================================
    // INSPECTOR CONFIGURATION
    // ========================================

    [Header("Root Panels")]
    [Tooltip("The main discard pile panel (always visible when cards exist)")]
    public GameObject stackPanel;

    [Tooltip("The expanded view panel (shown when player clicks to expand)")]
    public GameObject expandedPanel;

    [Header("Stack Display")]
    [Tooltip("Where the top card visual is displayed")]
    public RectTransform topCardSlot;

    [Tooltip("Card prefab for the stack display (simplified card visual)")]
    public GameObject stackCardPrefab;

    [Tooltip("Text showing card count (e.g., '5 cards')")]
    public TextMeshProUGUI cardCountText;

    [Tooltip("Button to expand/collapse the pile")]
    public Button expandButton;

    [Header("Expanded View")]
    [Tooltip("Scroll content parent where expanded cards are placed")]
    public RectTransform expandedContent;

    [Tooltip("Card prefab for expanded view (can be same as stack or different)")]
    public GameObject expandedCardPrefab;

    [Tooltip("Button to close expanded view")]
    public Button closeExpandedButton;

    [Tooltip("Optional: callback when a card is selected in expanded view (for 'play from discard' ability)")]
    public bool allowCardSelection = false;

    [Header("Carousel (Optional - overrides grid view)")]
    [Tooltip("If assigned, expanded view uses this CardCarousel instead of the grid layout")]
    public CardCarousel carousel;

    [Header("Animation")]
    [Tooltip("Duration of card landing animation")]
    public float cardLandDuration = 0.2f;

    [Tooltip("How much the stack 'jumps' when a new card lands")]
    public float stackBounceAmount = 10f;

    // ========================================
    // EVENTS
    // ========================================

    /// <summary>
    /// Fired when a card is selected from the expanded view.
    /// Used for future "play from discard" abilities.
    /// </summary>
    public event Action<UnitCard> OnCardSelected;

    // ========================================
    // SINGLETON (for ability/doctrine access)
    // ========================================

    public static DiscardPileUI Instance { get; private set; }

    // ========================================
    // PRIVATE STATE
    // ========================================

    private List<UnitCard> discardedCards = new List<UnitCard>();
    private GameObject currentTopCardVisual;
    private List<GameObject> expandedCardVisuals = new List<GameObject>();
    private bool isExpanded = false;

    // ========================================
    // UNITY LIFECYCLE
    // ========================================

    private void Awake()
    {
        Instance = this;

        // Wire up buttons
        if (expandButton != null)
            expandButton.onClick.AddListener(ToggleExpanded);

        if (closeExpandedButton != null)
            closeExpandedButton.onClick.AddListener(CloseExpanded);

        // Start collapsed and hidden
        if (expandedPanel != null)
            expandedPanel.SetActive(false);

        UpdateVisuals();
    }

    private void OnDestroy()
    {
        if (expandButton != null)
            expandButton.onClick.RemoveListener(ToggleExpanded);

        if (closeExpandedButton != null)
            closeExpandedButton.onClick.RemoveListener(CloseExpanded);
    }

    // ========================================
    // PUBLIC API
    // ========================================

    /// <summary>
    /// Adds a card to the discard pile with animation.
    /// Called by CardFlightManager when a death flight completes.
    /// </summary>
    public void AddCard(UnitCard card)
    {
        if (card == null) return;

        discardedCards.Add(card);
        UpdateVisuals();
        PlayCardLandAnimation();

        Debug.Log($"[DiscardPileUI] Added card: {card.Name}. Total: {discardedCards.Count}");
    }

    /// <summary>
    /// Removes a card from the discard pile.
    /// Used when "play from discard" ability retrieves a card.
    /// </summary>
    public bool RemoveCard(UnitCard card)
    {
        bool removed = discardedCards.Remove(card);
        if (removed)
        {
            UpdateVisuals();
            RefreshExpandedView();
            Debug.Log($"[DiscardPileUI] Removed card: {card.Name}. Remaining: {discardedCards.Count}");
        }
        return removed;
    }

    /// <summary>
    /// Opens the expanded carousel view immediately.
    /// Called by abilities, doctrines, or special cards that need to show the discard pile.
    /// </summary>
    public void ShowPopup()
    {
        OpenExpanded(forceOpen: true);
    }

    /// <summary>
    /// Clears all cards from the discard pile.
    /// Called at the start of a new match, or by abilities that reset the discard pile.
    /// NOT called between rounds — cards accumulate for the entire match.
    /// </summary>
    public void Clear()
    {
        discardedCards.Clear();
        UpdateVisuals();
        CloseExpanded();
        Debug.Log("[DiscardPileUI] Cleared all cards.");
    }

    /// <summary>
    /// Gets the world position where flying cards should land.
    /// Used by CardFlightManager for death flight destination.
    /// </summary>
    public Vector3 GetDropTargetWorldPosition(Camera camera)
    {
        if (topCardSlot == null || camera == null)
            return Vector3.zero;

        // Convert UI position to world position
        Vector3 screenPos = RectTransformUtility.WorldToScreenPoint(null, topCardSlot.position);
        
        // If using a screen space overlay canvas, we need to handle differently
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // For overlay canvas, the position is already in screen space
            screenPos = topCardSlot.position;
        }

        Vector3 worldPos = camera.ScreenToWorldPoint(new Vector3(
            screenPos.x,
            screenPos.y,
            camera.nearClipPlane + 10f
        ));

        return worldPos;
    }

    /// <summary>
    /// Gets a copy of all discarded cards (for display or ability selection).
    /// </summary>
    public IReadOnlyList<UnitCard> GetDiscardedCards()
    {
        return discardedCards.AsReadOnly();
    }

    /// <summary>
    /// Returns the number of cards in the discard pile.
    /// </summary>
    public int CardCount => discardedCards.Count;

    // ========================================
    // PRIVATE - VISUAL UPDATES
    // ========================================

    private void UpdateVisuals()
    {
        bool hasCards = discardedCards.Count > 0;

        // Show/hide stack panel based on whether we have cards
        if (stackPanel != null)
            stackPanel.SetActive(hasCards);

        // Update card count text
        if (cardCountText != null)
        {
            if (discardedCards.Count == 1)
                cardCountText.text = "1 card";
            else
                cardCountText.text = $"{discardedCards.Count} cards";
        }

        // Update top card visual
        UpdateTopCard();
    }

    private void UpdateTopCard()
    {
        // Destroy old visual
        if (currentTopCardVisual != null)
        {
            Destroy(currentTopCardVisual);
            currentTopCardVisual = null;
        }

        // Create new visual if we have cards
        if (discardedCards.Count > 0 && stackCardPrefab != null && topCardSlot != null)
        {
            UnitCard topCard = discardedCards[discardedCards.Count - 1];

            currentTopCardVisual = Instantiate(stackCardPrefab, topCardSlot);
            currentTopCardVisual.transform.localPosition = Vector3.zero;
            currentTopCardVisual.transform.localRotation = Quaternion.identity;
            currentTopCardVisual.transform.localScale = Vector3.one;

            // Set card data
            CardVisual cardVisual = currentTopCardVisual.GetComponent<CardVisual>();
            if (cardVisual != null)
            {
                cardVisual.SetCard(topCard);
                cardVisual.SetInteractable(false);
            }

            // Add slight random rotation for "messy stack" look
            float randomRotation = UnityEngine.Random.Range(-3f, 3f);
            currentTopCardVisual.transform.localRotation = Quaternion.Euler(0f, 0f, randomRotation);
        }
    }

    private void PlayCardLandAnimation()
    {
        if (currentTopCardVisual == null) return;

        // Start above and animate down with bounce
        Vector3 startPos = currentTopCardVisual.transform.localPosition + Vector3.up * 50f;
        Vector3 endPos = Vector3.zero;

        currentTopCardVisual.transform.localPosition = startPos;

        LeanTween.moveLocal(currentTopCardVisual, endPos, cardLandDuration)
            .setEaseOutBounce();

        // Scale bounce
        currentTopCardVisual.transform.localScale = Vector3.one * 1.2f;
        LeanTween.scale(currentTopCardVisual, Vector3.one, cardLandDuration)
            .setEaseOutBack();

        // Bounce the stack indicator
        if (topCardSlot != null)
        {
            Vector3 originalPos = topCardSlot.localPosition;
            LeanTween.moveLocalY(topCardSlot.gameObject, originalPos.y + stackBounceAmount, cardLandDuration * 0.3f)
                .setEaseOutQuad()
                .setOnComplete(() =>
                {
                    LeanTween.moveLocalY(topCardSlot.gameObject, originalPos.y, cardLandDuration * 0.5f)
                        .setEaseInQuad();
                });
        }
    }

    // ========================================
    // PRIVATE - EXPANDED VIEW
    // ========================================

    private void ToggleExpanded()
    {
        if (isExpanded)
            CloseExpanded();
        else
            OpenExpanded();
    }

    private void OpenExpanded(bool forceOpen = false)
    {
        if (expandedPanel == null) return;
        if (discardedCards.Count == 0 && !forceOpen) return;

        isExpanded = true;
        expandedPanel.SetActive(true);

        RefreshExpandedView();

        // Animate panel appearing
        CanvasGroup cg = expandedPanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            LeanTween.alphaCanvas(cg, 1f, 0.2f).setEaseOutQuad();
        }

        // Scale animation
        expandedPanel.transform.localScale = Vector3.one * 0.8f;
        LeanTween.scale(expandedPanel, Vector3.one, 0.2f).setEaseOutBack();
    }

    private void CloseExpanded()
    {
        if (expandedPanel == null) return;

        isExpanded = false;

        // Animate panel disappearing
        CanvasGroup cg = expandedPanel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            LeanTween.alphaCanvas(cg, 0f, 0.15f)
                .setEaseInQuad()
                .setOnComplete(() => expandedPanel.SetActive(false));
        }
        else
        {
            expandedPanel.SetActive(false);
        }

        ClearExpandedVisuals();
    }

    private void RefreshExpandedView()
    {
        // If a carousel is assigned, use it (preferred path)
        if (carousel != null)
        {
            // Build list newest-first for carousel
            var cardsNewestFirst = new System.Collections.Generic.List<UnitCard>(discardedCards);
            cardsNewestFirst.Reverse();
            carousel.SetCards(cardsNewestFirst);

            if (allowCardSelection)
                carousel.OnCardSelected += HandleCarouselCardSelected;

            return;
        }

        // Fallback: manual grid layout using expandedCardPrefab
        ClearExpandedVisuals();

        if (expandedContent == null || expandedCardPrefab == null) return;

        // Create card visuals for each discarded card (newest first)
        for (int i = discardedCards.Count - 1; i >= 0; i--)
        {
            UnitCard card = discardedCards[i];
            GameObject cardObj = Instantiate(expandedCardPrefab, expandedContent);

            CardVisual cardVisual = cardObj.GetComponent<CardVisual>();
            if (cardVisual != null)
            {
                cardVisual.SetCard(card);

                if (allowCardSelection)
                {
                    cardVisual.SetInteractable(true);
                    int cardIndex = i;
                    cardVisual.OnCardClicked += (cv) => OnExpandedCardClicked(cardIndex);
                }
                else
                {
                    cardVisual.SetInteractable(false);
                }
            }

            expandedCardVisuals.Add(cardObj);
        }
    }

    private void HandleCarouselCardSelected(UnitCard card)
    {
        if (card == null) return;
        Debug.Log($"[DiscardPileUI] Carousel card selected: {card.Name}");
        OnCardSelected?.Invoke(card);
    }

    private void ClearExpandedVisuals()
    {
        // Clear carousel if in use
        if (carousel != null)
        {
            carousel.OnCardSelected -= HandleCarouselCardSelected;
            carousel.ClearCards();
            return;
        }

        foreach (var visual in expandedCardVisuals)
        {
            if (visual != null)
                Destroy(visual);
        }
        expandedCardVisuals.Clear();
    }

    private void OnExpandedCardClicked(int cardIndex)
    {
        if (cardIndex < 0 || cardIndex >= discardedCards.Count) return;

        UnitCard selectedCard = discardedCards[cardIndex];
        Debug.Log($"[DiscardPileUI] Card selected: {selectedCard.Name}");

        OnCardSelected?.Invoke(selectedCard);
    }
}