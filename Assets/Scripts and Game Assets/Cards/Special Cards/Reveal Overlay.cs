using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class CardRevealOverlay : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject root;           // the overlay panel root
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private CardCarousel carousel;

    private void Awake()
    {
        if (root != null) root.SetActive(false);
    }

    public void Show(string title, List<UnitCard> cards, bool cloneCards = true)
    {
        if (root == null || carousel == null) return;

        if (titleText != null) titleText.text = title;
        // Make reveal carousel read-only (no selection)
carousel.OnCardSelected = null;
carousel.allowKeyboardInput = false;
carousel.SetInputEnabled(true);

        // For safety: show copies so nothing can modify real cards.
        List<UnitCard> toShow = cloneCards ? CloneCards(cards) : cards;

        // Use preserve-center so it feels stable
        carousel.SetCardsPreserveCenter(toShow);

        root.SetActive(true);
    }

    public void Hide()
    {
        carousel.SetInputEnabled(false);

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
