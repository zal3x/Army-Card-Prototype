using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardRevealOverlay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;           // the overlay panel root
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private CardCarousel carousel;

    [Header("Dismiss")]
    [Tooltip("Optional dismiss button. If not assigned, clicking the background will dismiss.")]
    [SerializeField] private Button dismissButton;
    [Tooltip("Optional background button for click-anywhere-to-dismiss.")]
    [SerializeField] private Button backgroundButton;

    private System.Action onDismissCallback;
    private System.Action<UnitCard> onSelectCallback;
    private bool isSelectionMode;

    private void Awake()
    {
        if (root != null) root.SetActive(false);

        // Wire up dismiss button if assigned
        if (dismissButton != null)
            dismissButton.onClick.AddListener(OnDismissClicked);

        // Wire up background click if assigned
        if (backgroundButton != null)
            backgroundButton.onClick.AddListener(OnDismissClicked);
    }

    public void Show(string title, List<UnitCard> cards, bool cloneCards = true, System.Action onDismiss = null)
    {
        Debug.Log($"[CardRevealOverlay] Show called: title='{title}', cards={cards?.Count ?? 0}, root={root}, carousel={carousel}");

        if (root == null)
        {
            Debug.LogError("[CardRevealOverlay] root is NULL! Assign the overlay panel root in Inspector on the CardRevealOverlay component.");
            onDismiss?.Invoke(); // Still invoke callback so game doesn't freeze
            return;
        }

        if (carousel == null)
        {
            Debug.LogError("[CardRevealOverlay] carousel is NULL! Assign a CardCarousel in Inspector on the CardRevealOverlay component.");
            onDismiss?.Invoke(); // Still invoke callback so game doesn't freeze
            return;
        }

        onDismissCallback = onDismiss;
        onSelectCallback = null;
        isSelectionMode = false;
        showFrame = Time.frameCount;

        if (titleText != null) titleText.text = title;
        // Make reveal carousel read-only (no selection)
        carousel.OnCardSelected = null;
        carousel.allowKeyboardInput = false;
        carousel.SetInputEnabled(true);

        // For safety: show copies so nothing can modify real cards.
        List<UnitCard> toShow = cloneCards ? CloneCards(cards) : cards;

        // Use preserve-center so it feels stable
        carousel.SetCardsPreserveCenter(toShow);

        // Debug: show hierarchy state before activation
        Debug.Log($"[CardRevealOverlay] BEFORE activation: root.name={root.name}, activeSelf={root.activeSelf}, activeInHierarchy={root.activeInHierarchy}, scene={root.scene.name}");
        if (root.transform.parent != null)
            Debug.Log($"[CardRevealOverlay] Parent: {root.transform.parent.name}, parent.activeInHierarchy={root.transform.parent.gameObject.activeInHierarchy}");

        // Check if this is a valid scene object (not a prefab)
        if (string.IsNullOrEmpty(root.scene.name))
        {
            Debug.LogError("[CardRevealOverlay] root appears to be a prefab reference, not a scene instance! Drag the SCENE object, not the prefab.");
        }

        // Ensure all parents in the hierarchy are active
        Transform t = root.transform;
        while (t.parent != null)
        {
            if (!t.parent.gameObject.activeSelf)
            {
                Debug.Log($"[CardRevealOverlay] Activating inactive parent: {t.parent.name}");
                t.parent.gameObject.SetActive(true);
            }
            t = t.parent;
        }

        // Check if root is same as this.gameObject
        bool rootIsSelf = (root == gameObject);
        Debug.Log($"[CardRevealOverlay] rootIsSelf={rootIsSelf}, this.gameObject.activeSelf={gameObject.activeSelf}");

        // Activate root
        root.SetActive(true);
        Debug.Log($"[CardRevealOverlay] After SetActive(true): activeSelf={root.activeSelf}");

        // Double-check: if root is self and still inactive, force it via gameObject
        if (rootIsSelf && !root.activeSelf)
        {
            Debug.LogWarning("[CardRevealOverlay] root==gameObject but still inactive, trying gameObject.SetActive(true)");
            gameObject.SetActive(true);
        }

        // If STILL not active, log error with full diagnosis
        if (!root.activeSelf)
        {
            Debug.LogError($"[CardRevealOverlay] FAILED to activate! root.activeSelf={root.activeSelf}, activeInHierarchy={root.activeInHierarchy}. Check if a parent CanvasGroup or Layout is controlling visibility.");
        }
        else
        {
            Debug.Log($"[CardRevealOverlay] SUCCESS: Overlay is now active. activeSelf={root.activeSelf}, activeInHierarchy={root.activeInHierarchy}");
        }
    }

    /// <summary>
    /// Shows the overlay in selection mode. Player can click a card to select it.
    /// Used by POW to select from enemy discard pile.
    /// </summary>
    public void ShowWithSelection(string title, List<UnitCard> cards, bool cloneCards = true,
        System.Action<UnitCard> onSelect = null, System.Action onDismiss = null)
    {
        Debug.Log($"[CardRevealOverlay] ShowWithSelection: title='{title}', cards={cards?.Count ?? 0}");

        if (root == null || carousel == null)
        {
            Debug.LogError("[CardRevealOverlay] root or carousel is NULL!");
            onDismiss?.Invoke();
            return;
        }

        isSelectionMode = true;
        onSelectCallback = onSelect;
        onDismissCallback = onDismiss;
        showFrame = Time.frameCount;

        if (titleText != null) titleText.text = title + " (Click to Select)";

        // Enable selection on the carousel
        carousel.OnCardSelected = OnCardSelectedInOverlay;
        carousel.allowKeyboardInput = true;
        carousel.SetInputEnabled(true);

        List<UnitCard> toShow = cloneCards ? CloneCards(cards) : cards;
        carousel.SetCardsPreserveCenter(toShow);

        // Activate overlay (same logic as Show)
        Transform t = root.transform;
        while (t.parent != null)
        {
            if (!t.parent.gameObject.activeSelf)
                t.parent.gameObject.SetActive(true);
            t = t.parent;
        }

        root.SetActive(true);
    }

    private void OnCardSelectedInOverlay(UnitCard card)
    {
        if (!isSelectionMode) return;

        Debug.Log($"[CardRevealOverlay] Card selected: {card?.Name}");

        isSelectionMode = false;
        Hide();
        onSelectCallback?.Invoke(card);
    }

    private void Update()
    {
        // Allow Escape or Space to dismiss (NOT mouse click - that would prevent carousel interaction)
        if (root != null && root.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Space))
            {
                // Small delay to prevent immediate dismiss on the same frame as show
                if (Time.frameCount > showFrame + 1)
                    OnDismissClicked();
            }
        }
    }

    private int showFrame;

    private void OnEnable()
    {
        showFrame = Time.frameCount;
    }

    private void OnDismissClicked()
    {
        Hide();
        onDismissCallback?.Invoke();
    }

    public void Hide()
    {
        isSelectionMode = false;

        if (carousel != null)
        {
            carousel.OnCardSelected = null;
            carousel.SetInputEnabled(false);
        }

        if (root != null) root.SetActive(false);
    }

    private List<UnitCard> CloneCards(List<UnitCard> cards)
    {
        var list = new List<UnitCard>();
        if (cards == null) return list;

        foreach (var c in cards)
        {
            if (c == null) continue;

            // Shallow copy of runtime card data.
            // If your UnitCard is a class with a copy constructor, use that instead.
            UnitCard copy = c.Clone(); // <-- if you don't have Clone(), see note below
            list.Add(copy);
        }
        return list;
    }
}
