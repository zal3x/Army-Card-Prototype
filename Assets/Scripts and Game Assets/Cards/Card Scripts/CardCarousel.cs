using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// CardCarousel: Horizontal card scroller with center-focused zoom effect.
/// 
/// UPDATED: Now supports different card prefabs per unit type (Infantry/Vehicle/Air/Ranged)
/// UPDATED: RemoveCard now supports flying to a world position
/// 
/// FEATURES:
/// - Smooth horizontal scrolling
/// - Center card is zoomed/highlighted
/// - Snap-to-center behavior
/// - Touch and mouse input support
/// - Keyboard navigation (left/right arrows)
/// - Type-specific card templates
/// - Cards fly to world positions when purchased
/// 
/// SETUP:
/// 1. Create a UI Canvas
/// 2. Add a Panel for the carousel container
/// 3. Add this script to the panel
/// 4. Create 4 different card prefabs (Infantry, Vehicle, Air, Ranged) - each with CardVisual component
/// 5. Assign all 4 prefabs in Inspector
/// 6. Set spacing and scale values
/// 
/// USAGE:
/// carousel.SetCards(listOfUnitCards);
/// carousel.OnCardSelected += (card) => { BuyCard(card); };
/// </summary>
public class CardCarousel : MonoBehaviour
{
    [Header("Card Setup - Type-Specific Prefabs")]
    [Tooltip("Infantry card prefab (with CardVisual component)")]
    public GameObject infantryCardPrefab;
    
    [Tooltip("Vehicle card prefab (with CardVisual component)")]
    public GameObject vehicleCardPrefab;
    
    [Tooltip("Air card prefab (with CardVisual component)")]
    public GameObject airCardPrefab;
    
    [Tooltip("Ranged card prefab (with CardVisual component)")]
    public GameObject rangedCardPrefab;

    [Tooltip("Parent transform where cards are instantiated")]
    public RectTransform cardContainer;

    [Header("Layout Settings")]
    [Tooltip("Horizontal spacing between cards")]
    public float cardSpacing = 150f;

    [Tooltip("Spacing multiplier for cards far from center (0.5 = bunched up, 1.0 = normal)")]
    [Range(0.1f, 1f)]
    public float sideCardSpacingMultiplier = 1.0f;

    [Tooltip("Base scale multiplier for ALL cards (1.0 = original size, 0.5 = half size)")]
    [Range(0.1f, 2f)]
    public float baseCardScale = 1.0f;

    [Tooltip("Scale of center (focused) card (relative to baseCardScale)")]
    public float centerScale = 1.2f;

    [Tooltip("Scale of non-focused cards (relative to baseCardScale)")]
    public float sideScale = 0.8f;

    [Tooltip("Vertical offset for center card (adds depth)")]
    public float centerYOffset = 20f;

    [Header("Opacity Settings")]
    [Tooltip("Opacity of center card (1.0 = fully opaque)")]
    [Range(0f, 1f)]
    public float centerAlpha = 1.0f;

    [Tooltip("Opacity of side cards at maximum distance (0.5 = half transparent)")]
    [Range(0f, 1f)]
    public float sideAlpha = 0.5f;

    [Header("Scroll Settings")]
    [Tooltip("How fast cards move to center when scrolling")]
    public float scrollSpeed = 10f;

    [Tooltip("Minimum drag distance to trigger scroll")]
    public float dragThreshold = 10f;

    [Tooltip("If true, automatically snap to nearest card when released")]
    public bool snapToCenter = true;

    [Header("Input")]
    [Tooltip("If true, allows keyboard arrow keys to navigate")]
    public bool allowKeyboardInput = true;

    [Header("Click Protection")]
    [Tooltip("Prevents spam clicking (seconds between clicks). Lower = more responsive.")]
    public float clickCooldown = 0.05f; // Reduced from 0.1f for snappier response

    private float lastClickTime = 0f;

    //dont allow moving if elsewhere on screen
[Header("Input Gating")]
[SerializeField] private bool inputEnabled = true;
[SerializeField] private bool requirePointerOverThisCarousel = true;

private RectTransform selfRect;

    [Tooltip("Special card prefab (with CardVisual component)")]
public GameObject specialCardPrefab;


    // Internal state
    private List<CardVisual> cardVisuals = new List<CardVisual>();
    private int currentCenterIndex = 0;
    private float targetScrollPosition = 0f;
    private float currentScrollPosition = 0f;
    private bool isDragging = false;
    private Vector2 dragStartPos;
    private float[] cachedCardPositions = new float[0]; // Stores compressed positions for each card

    // Events
    public System.Action<UnitCard> OnCardSelected;
    public System.Action<int> OnFocusChanged; // Passes new center index
    public CardVisual LastClickedVisual { get; private set; }

    // ========================================
    // INITIALIZATION
    // ========================================

    private void Awake()
    {
        if (cardContainer == null)
            cardContainer = GetComponent<RectTransform>();
                selfRect = GetComponent<RectTransform>();

    }

    /// <summary>
    /// Populates the carousel with cards from a list of UnitCard data.
    /// Now automatically selects the correct prefab based on card type!
    /// </summary>
    public void SetCards(List<UnitCard> cards)
    {
        // Clear existing cards
        ClearCards();

        // Create new card visuals
        for (int i = 0; i < cards.Count; i++)
        {
            // SELECT THE RIGHT PREFAB BASED ON CARD TYPE
GameObject prefabToUse = GetPrefabForCard(cards[i]);
            
            if (prefabToUse == null)
            {
                Debug.LogError($"[CardCarousel] No prefab assigned for {cards[i].Type}! Skipping card.");
                continue;
            }

            GameObject cardObj = Instantiate(prefabToUse, cardContainer);
            CardVisual cardVisual = cardObj.GetComponent<CardVisual>();

            if (cardVisual == null)
            {
                Debug.LogError($"[CardCarousel] Prefab for {cards[i].Type} is missing CardVisual component!");
                Destroy(cardObj);
                continue;
            }

            cardVisual.SetCard(cards[i]);
            cardVisual.OnCardClicked += OnCardClicked;

            // Check if card is blocked by battlefield configuration
            cardVisual.UpdateBattlefieldRestrictionVisual();

            cardVisuals.Add(cardVisual);
        }

        // Position cards
        currentCenterIndex = 0;
        targetScrollPosition = 0f;
        currentScrollPosition = 0f;
        UpdateCardPositions(immediate: true);
    }

// so UI can enable/disable
public void SetInputEnabled(bool enabled)
{
    // Only apply skip frames when transitioning from disabled -> enabled
    // This prevents repeated calls from compounding the delay
    bool wasDisabled = !inputEnabled;

    inputEnabled = enabled;
    // Always reset drag state when changing input enabled
    isDragging = false;

    // Only skip frames when re-enabling from a disabled state
    // Skip just 1 frame instead of 2 for snappier response
    if (enabled && wasDisabled)
        inputSkipUntilFrame = Time.frameCount + 1;
}

private int inputSkipUntilFrame = 0;

    /// <summary>
    /// NEW METHOD: Selects the correct card prefab based on unit type.
    /// </summary>
    /// <summary>
/// Selects the correct card prefab.
/// Special cards override type-based selection.
/// </summary>
private GameObject GetPrefabForCard(UnitCard card)
{
    if (card == null) return infantryCardPrefab;

    // IMPORTANT: Special cards get their own template
    if (card.IsSpecialCard && specialCardPrefab != null)
        return specialCardPrefab;

    // Normal unit cards use type templates
    GameObject prefab = card.Type switch
    {
        UnitType.Infantry => infantryCardPrefab,
        UnitType.Vehicle  => vehicleCardPrefab,
        UnitType.Air      => airCardPrefab,
        UnitType.Ranged   => rangedCardPrefab,
        _ => infantryCardPrefab
    };

    if (prefab == null)
    {
        Debug.LogWarning($"[CardCarousel] No prefab assigned for {card.Type}, using infantry as fallback");
        return infantryCardPrefab;
    }

    return prefab;
}


    /// <summary>
    /// Removes all cards from the carousel.
    /// </summary>
    public void ClearCards()
    {
        foreach (var card in cardVisuals)
        {
            if (card != null)
                Destroy(card.gameObject);
        }
        cardVisuals.Clear();
    }

    /// <summary>
    /// Refreshes battlefield restriction visuals for all cards.
    /// Call this if the battlefield configuration changes.
    /// </summary>
    public void RefreshBattlefieldRestrictions()
    {
        foreach (var cardVisual in cardVisuals)
        {
            if (cardVisual != null)
            {
                cardVisual.UpdateBattlefieldRestrictionVisual();
            }
        }
    }

    // ========================================
    // UPDATE & INPUT
    // ========================================

    private void Update()
    {
        if (!inputEnabled)
    return;

        HandleInput();
        UpdateScrollPosition();
        UpdateCardPositions(immediate: false);
    }

    private void HandleInput()
    {
        // Skip input for a couple frames after re-enabling (prevents click bleed-through)
        if (Time.frameCount < inputSkipUntilFrame)
            return;

        // Keyboard navigation
        if (allowKeyboardInput)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow))
                ScrollToCard(currentCenterIndex - 1);

            if (Input.GetKeyDown(KeyCode.RightArrow))
                ScrollToCard(currentCenterIndex + 1);
        }

        // IMPORTANT: Always process mouse up first to prevent stuck drag state
        if (Input.GetMouseButtonUp(0))
        {
            if (isDragging && snapToCenter)
            {
                // Snap to nearest card
                SnapToNearestCard();
            }
            isDragging = false;
        }

        // Mouse/touch drag start
        if (Input.GetMouseButtonDown(0))
        {
            if (requirePointerOverThisCarousel)
            {
                // Only start dragging if the click started on THIS carousel rect
                bool over = RectTransformUtility.RectangleContainsScreenPoint(
                    selfRect != null ? selfRect : cardContainer,
                    Input.mousePosition,
                    null
                );

                if (!over)
                {
                    // Not over this carousel, don't start dragging but don't return early
                    // (we still need to process other input)
                }
                else
                {
                    isDragging = true;
                    dragStartPos = Input.mousePosition;
                }
            }
            else
            {
                isDragging = true;
                dragStartPos = Input.mousePosition;
            }
        }

        // Process drag movement
        if (isDragging && Input.GetMouseButton(0))
        {
            Vector2 dragDelta = (Vector2)Input.mousePosition - dragStartPos;

            if (Mathf.Abs(dragDelta.x) > dragThreshold)
            {
                // Drag to scroll (inverted for natural feel)
                targetScrollPosition -= dragDelta.x * 0.5f;
                dragStartPos = Input.mousePosition;
            }
        }
    }
    /// <summary>
/// Re-reads cardData and updates text/images on the existing card visuals.
/// Use this when costs/stats change without rebuilding the carousel.
/// </summary>
// Keeps the same objects; just updates text/images from cardData
public void RefreshVisuals()
{
    for (int i = 0; i < cardVisuals.Count; i++)
    {
        if (cardVisuals[i] != null)
            cardVisuals[i].UpdateVisuals();
    }
}

public void SetCardsPreserveCenter(List<UnitCard> cards)
{
    // Remember current scroll state BEFORE we rebuild visuals
    UnitCard centeredCard = GetCenterCard();
    float oldCurrentScroll = currentScrollPosition;
    float oldTargetScroll = targetScrollPosition;
    int oldCenterIndex = currentCenterIndex;

    // Rebuild visuals (this destroys/creates cardVisuals)
    SetCards(cards);

    // Restore scroll state immediately so we don't snap to index 0 for a frame
    currentScrollPosition = oldCurrentScroll;
    targetScrollPosition = oldTargetScroll;

    // Restore center index (clamp in case list shrank)
    currentCenterIndex = Mathf.Clamp(oldCenterIndex, 0, cardVisuals.Count - 1);

    // If the same card still exists, re-center it *immediately*
    if (centeredCard != null)
    {
        int newIndex = cards.IndexOf(centeredCard);
        if (newIndex >= 0)
        {
            currentCenterIndex = newIndex;
            currentScrollPosition = -newIndex * cardSpacing;
            targetScrollPosition = currentScrollPosition;
        }
    }

    // Apply positions immediately using the restored scroll
    UpdateCardPositions(immediate: true);

    // Notify focus
    OnFocusChanged?.Invoke(currentCenterIndex);
}



    private void UpdateScrollPosition()
    {
        // Smooth interpolation toward target
        currentScrollPosition = Mathf.Lerp(
            currentScrollPosition,
            targetScrollPosition,
            Time.deltaTime * scrollSpeed
        );

        // Update center index based on scroll position
        int newCenterIndex = Mathf.RoundToInt(-currentScrollPosition / cardSpacing);
        newCenterIndex = Mathf.Clamp(newCenterIndex, 0, cardVisuals.Count - 1);

        if (newCenterIndex != currentCenterIndex)
        {
            currentCenterIndex = newCenterIndex;
            OnFocusChanged?.Invoke(currentCenterIndex);
        }
    }

    private void UpdateCardPositions(bool immediate)
    {
        // Calculate compressed positions for all cards
        cachedCardPositions = new float[cardVisuals.Count];
        for (int i = 0; i < cardVisuals.Count; i++)
        {
            // Distance from center in card indices
            int distFromCenter = Mathf.Abs(i - currentCenterIndex);

            // Cards further from center use compressed spacing
            float spacingForThisCard = cardSpacing;
            if (distFromCenter > 0)
            {
                // Progressive compression: cards 1+ away from center bunch up
                float compressionFactor = Mathf.Lerp(1f, sideCardSpacingMultiplier, (float)distFromCenter / Mathf.Max(1f, cardVisuals.Count));
                spacingForThisCard *= compressionFactor;
            }

            // Build cumulative position
            if (i == 0)
                cachedCardPositions[i] = 0;
            else
                cachedCardPositions[i] = cachedCardPositions[i - 1] + spacingForThisCard;
        }

        for (int i = 0; i < cardVisuals.Count; i++)
        {
            CardVisual card = cardVisuals[i];
            if (card == null) continue;

            // Use pre-calculated compressed position
            float targetX = cachedCardPositions[i] + currentScrollPosition;
            float distanceFromCenter = Mathf.Abs(targetX);

            // Scale based on distance from center (now includes baseCardScale)
            float relativeScale = Mathf.Lerp(centerScale, sideScale, distanceFromCenter / cardSpacing);
            float scale = relativeScale * baseCardScale;

            // Y offset for center card (creates depth effect)
            float yOffset = distanceFromCenter < cardSpacing / 2f ? centerYOffset : 0f;

            // Apply position and scale
            RectTransform rect = card.GetComponent<RectTransform>();
            Vector2 targetPos = new Vector2(targetX, yOffset);

            // Skip scale updates if card has active hover effect
            bool skipScaleUpdate = card.IsHoverEffectActive;

            if (immediate)
            {
                rect.anchoredPosition = targetPos;
                if (!skipScaleUpdate)
                    rect.localScale = Vector3.one * scale;
            }
            else
            {
                rect.anchoredPosition = Vector2.Lerp(
                    rect.anchoredPosition,
                    targetPos,
                    Time.deltaTime * scrollSpeed
                );

                if (!skipScaleUpdate)
                {
                    rect.localScale = Vector3.Lerp(
                        rect.localScale,
                        Vector3.one * scale,
                        Time.deltaTime * scrollSpeed
                    );
                }
            }

            // Update selection state
            bool isCenter = (i == currentCenterIndex);
            card.SetSelected(isCenter);

            // Alpha fade for cards far from center (skip if hover is active)
            if (!skipScaleUpdate)
            {
                CanvasGroup canvasGroup = card.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    float alpha = Mathf.Lerp(centerAlpha, sideAlpha, distanceFromCenter / (cardSpacing * 2f));
                    canvasGroup.alpha = alpha;
                }
            }
        }

        // Update sibling order so center card renders on top of side cards
        // Cards closer to center get higher sibling index (render later = on top)
        UpdateCardSiblingOrder();
    }

    /// <summary>
    /// Updates card sibling order so center card renders on top.
    /// Cards are ordered by distance from center - closest to center renders last (on top).
    /// </summary>
    private void UpdateCardSiblingOrder()
    {
        if (cardVisuals.Count == 0) return;

        // Build list of (card, distance from center)
        var cardsWithDistance = new List<(CardVisual card, float distance)>();
        for (int i = 0; i < cardVisuals.Count; i++)
        {
            if (cardVisuals[i] == null) continue;
            int distFromCenter = Mathf.Abs(i - currentCenterIndex);
            cardsWithDistance.Add((cardVisuals[i], distFromCenter));
        }

        // Sort by distance descending (furthest first = lowest sibling index)
        cardsWithDistance.Sort((a, b) => b.distance.CompareTo(a.distance));

        // Apply sibling order
        for (int i = 0; i < cardsWithDistance.Count; i++)
        {
            cardsWithDistance[i].card.transform.SetSiblingIndex(i);
        }
    }

    // ========================================
    // NAVIGATION
    // ========================================

    /// <summary>
    /// Scrolls to show a specific card at the center.
    /// </summary>
    public void ScrollToCard(int index)
    {
        index = Mathf.Clamp(index, 0, cardVisuals.Count - 1);

        // Use cached compressed position for accurate centering
        if (cachedCardPositions != null && index < cachedCardPositions.Length)
            targetScrollPosition = -cachedCardPositions[index];
        else
            targetScrollPosition = -index * cardSpacing; // Fallback

        currentCenterIndex = index;
        OnFocusChanged?.Invoke(currentCenterIndex);
    }

    /// <summary>
    /// Snaps to the nearest card (called when drag is released).
    /// </summary>
    private void SnapToNearestCard()
    {
        // Find the card whose compressed position is closest to current scroll position
        int nearestIndex = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < cachedCardPositions.Length; i++)
        {
            float distance = Mathf.Abs(-cachedCardPositions[i] - currentScrollPosition);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }

        ScrollToCard(nearestIndex);
    }

    /// <summary>
    /// Gets the currently focused card.
    /// </summary>
    public UnitCard GetCenterCard()
    {
        if (currentCenterIndex >= 0 && currentCenterIndex < cardVisuals.Count)
            return cardVisuals[currentCenterIndex].cardData;
        return null;
    }

    /// <summary>
    /// Gets the currently focused card visual.
    /// </summary>
    public CardVisual GetCenterCardVisual()
    {
        if (currentCenterIndex >= 0 && currentCenterIndex < cardVisuals.Count)
            return cardVisuals[currentCenterIndex];
        return null;
    }

    // ========================================
    // EVENTS
    // ========================================

    private void OnCardClicked(CardVisual card)
{
    // Debug: Log all carousel click handling
    Debug.Log($"[CAROUSEL] OnCardClicked received - inputEnabled={inputEnabled}, frameSkip={inputSkipUntilFrame}, currentFrame={Time.frameCount}");

    // Track the most recent click (used by BuyPhase to remove/fly the correct visual)
    LastClickedVisual = card;

    // DEBOUNCE: Prevent spam clicking
    float timeSinceLastClick = Time.time - lastClickTime;
    if (timeSinceLastClick < clickCooldown)
    {
        Debug.Log($"[CAROUSEL] Click ignored (cooldown: {timeSinceLastClick:F3}s < {clickCooldown}s)");
        return;
    }

    lastClickTime = Time.time;

    // Check card index
    int index = cardVisuals.IndexOf(card);
    Debug.Log($"[CAROUSEL] Card '{card?.cardData?.Name}' index={index}, currentCenterIndex={currentCenterIndex}, cardVisuals.Count={cardVisuals.Count}");

    if (index == -1)
    {
        Debug.LogWarning($"[CAROUSEL] Clicked card NOT in cardVisuals list! Card may have been removed.");
        return;
    }

    if (index != currentCenterIndex)
    {
        Debug.Log($"[CAROUSEL] Scrolling to card at index {index} (center is {currentCenterIndex})");
        ScrollToCard(index);
    }
    else
    {
        // Center card was clicked - select it
        Debug.Log($"[CAROUSEL] SELECTED: {card.cardData.Name} - invoking OnCardSelected");
        OnCardSelected?.Invoke(card.cardData);
    }
}


    /// <summary>
    /// Removes a card from the carousel with optional flight animation to a world position.
    /// 
    /// If worldTargetPosition is provided, the card flies toward that position.
    /// If not provided, uses the default "float up and fade" animation.
    /// </summary>
    /// <param name="card">The card to remove</param>
    /// <param name="worldTargetPosition">Optional world position to fly toward (e.g., unit spawn location)</param>
    public void RemoveCard(CardVisual card, Vector3? worldTargetPosition = null)
    {
        int index = cardVisuals.IndexOf(card);
        if (index == -1) return;

        cardVisuals.RemoveAt(index);

        // Choose animation based on whether we have a target
        if (worldTargetPosition.HasValue)
        {
            // Fly toward the world position
            card.FlyToWorldPosition(worldTargetPosition.Value, () =>
            {
                // After animation, reposition remaining cards
                if (currentCenterIndex >= cardVisuals.Count)
                    currentCenterIndex = Mathf.Max(0, cardVisuals.Count - 1);

                ScrollToCard(currentCenterIndex);
            });
        }
        else
        {
            // Default animation (float up and fade)
            card.PlayCardAnimation(() =>
            {
                // After animation, reposition remaining cards
                if (currentCenterIndex >= cardVisuals.Count)
                    currentCenterIndex = Mathf.Max(0, cardVisuals.Count - 1);

                ScrollToCard(currentCenterIndex);
            });
        }
    }

    /// <summary>
/// Removes a card from the carousel WITHOUT playing any animation.
/// Use this when another system (CardFlightManager) will animate the card.
/// </summary>
public void RemoveCardNoAnimation(CardVisual card)
{
    int index = cardVisuals.IndexOf(card);
    if (index == -1) return;

    cardVisuals.RemoveAt(index);

    // IMPORTANT: Immediately disable raycasts so removed card can't block clicks
    CanvasGroup cg = card.GetComponent<CanvasGroup>();
    if (cg != null)
    {
        cg.blocksRaycasts = false;
        cg.interactable = false;
    }

    // Detach from carousel so UpdateCardPositions no longer drives it
    // (Keep world position so it doesn't jump)
    RectTransform rt = card.GetComponent<RectTransform>();
    Vector3 worldBefore = rt.position;

    // Prefer flight layer if CardFlightManager exists, otherwise keep under current canvas root
    RectTransform newParent = cardContainer;
    if (CardFlightManager.Instance != null && CardFlightManager.Instance.flightLayer != null)
        newParent = CardFlightManager.Instance.flightLayer;

    rt.SetParent(newParent, true);
    rt.position = worldBefore;
    rt.SetAsLastSibling();

    // Reposition remaining cards
    if (currentCenterIndex >= cardVisuals.Count)
        currentCenterIndex = Mathf.Max(0, cardVisuals.Count - 1);

    ScrollToCard(currentCenterIndex);
}


    // ========================================
    // UTILITY
    // ========================================

    /// <summary>
    /// Refreshes card visuals (e.g., after gold changes and cards become affordable).
    /// </summary>
    public void RefreshCards()
    {
        foreach (var card in cardVisuals)
        {
            if (card != null)
                card.UpdateVisuals();
        }
    }
}