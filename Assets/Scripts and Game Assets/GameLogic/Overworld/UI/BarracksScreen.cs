using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Barracks screen with tabbed interface for Deck, Doctrines, and Perks.
/// Deck tab: Left panel shows owned cards, Right panel shows current deck
/// Doctrines tab: Left panel shows owned doctrines, Right panel shows 5 equipped slots
/// Perks tab: Shows purchased perks with their tier levels
/// </summary>
public class BarracksScreen : OverworldScreen
{
    #region Tab System

    private enum BarracksTab
    {
        Deck,
        Doctrines,
        Perks
    }

    [Header("Tab Buttons")]
    [Tooltip("Button to show Deck tab (default)")]
    [SerializeField]
    private Button deckTabButton;

    [Tooltip("Button to show Doctrines tab")]
    [SerializeField]
    private Button doctrinesTabButton;

    [Tooltip("Button to show Perks tab")]
    [SerializeField]
    private Button perksTabButton;

    [Header("Tab Visual States")]
    [SerializeField]
    private Color activeTabColor = Color.white;
    [SerializeField]
    private Color inactiveTabColor = new Color(0.6f, 0.6f, 0.6f);

    [Header("Tab Panels")]
    [Tooltip("Root panel for Deck tab content")]
    [SerializeField]
    private GameObject deckPanel;

    [Tooltip("Root panel for Doctrines tab content")]
    [SerializeField]
    private GameObject doctrinesPanel;

    [Tooltip("Root panel for Perks tab content")]
    [SerializeField]
    private GameObject perksPanel;

    private BarracksTab currentTab = BarracksTab.Deck;

    #endregion

    #region Deck Tab (Existing Functionality)

    [Header("Deck - Collection Panel (Left)")]
    [Tooltip("Scroll view for owned cards")]
    [SerializeField]
    private ScrollRect ownedCardsScroll;

    [Tooltip("Container for owned card items")]
    [SerializeField]
    private Transform ownedCardsContainer;

    [Header("Deck - Deck Panel (Right)")]
    [Tooltip("Scroll view for deck cards")]
    [SerializeField]
    private ScrollRect deckCardsScroll;

    [Tooltip("Container for deck card items")]
    [SerializeField]
    private Transform deckCardsContainer;

    [Header("Deck - Card Item Prefab")]
    [Tooltip("Prefab for individual card display")]
    [SerializeField]
    private BarracksCardItem cardItemPrefab;

    [Header("Deck - Stats Display")]
    [Tooltip("Text showing total deck size")]
    [SerializeField]
    private TMP_Text deckCountText;

    [Tooltip("Text showing unit card count")]
    [SerializeField]
    private TMP_Text unitCountText;

    [Tooltip("Text showing special card count")]
    [SerializeField]
    private TMP_Text specialCountText;

    [Tooltip("Text showing deck validity status")]
    [SerializeField]
    private TMP_Text validityText;

    private List<BarracksCardItem> ownedCardItems = new List<BarracksCardItem>();
    private List<BarracksCardItem> deckCardItems = new List<BarracksCardItem>();

    #endregion

    #region Doctrines Tab

    [Header("Doctrines - Owned Panel (Left)")]
    [Tooltip("Scroll view for owned doctrines")]
    [SerializeField]
    private ScrollRect ownedDoctrinesScroll;

    [Tooltip("Container for owned doctrine items")]
    [SerializeField]
    private Transform ownedDoctrinesContainer;

    [Header("Doctrines - Equipped Panel (Right)")]
    [Tooltip("Container for the 5 equipped doctrine slots")]
    [SerializeField]
    private Transform equippedDoctrinesContainer;

    [Header("Doctrines - Prefabs")]
    [Tooltip("Prefab for doctrine slot (equipped side)")]
    [SerializeField]
    private DoctrineSlot doctrineSlotPrefab;

    [Tooltip("Prefab for doctrine item display")]
    [SerializeField]
    private BarracksDoctrineItem doctrineItemPrefab;

    [Header("Doctrines - Stats")]
    [Tooltip("Text showing equipped doctrine count")]
    [SerializeField]
    private TMP_Text equippedDoctrinesCountText;

    private List<BarracksDoctrineItem> ownedDoctrineItems = new List<BarracksDoctrineItem>();
    private DoctrineSlot[] equippedDoctrineSlots = new DoctrineSlot[PlayerProgressData.MaxDoctrines];

    #endregion

    #region Perks Tab

    [Header("Perks - Panel")]
    [Tooltip("Scroll view for purchased perks")]
    [SerializeField]
    private ScrollRect perksScroll;

    [Tooltip("Container for perk items")]
    [SerializeField]
    private Transform perksContainer;

    [Header("Perks - Item Prefab")]
    [Tooltip("Prefab for perk display (read-only, shows tier)")]
    [SerializeField]
    private BarracksPerkItem perkItemPrefab;

    [Header("Perks - Empty State")]
    [Tooltip("Text shown when no perks purchased")]
    [SerializeField]
    private TMP_Text noPerksText;

    private List<BarracksPerkItem> perkItems = new List<BarracksPerkItem>();

    #endregion

    #region Navigation

    [Header("Navigation")]
    [Tooltip("Button to go back to world map")]
    [SerializeField]
    private Button backButton;

    [Tooltip("Button to save changes")]
    [SerializeField]
    private Button saveButton;

    #endregion

    #region Colors

    [Header("Colors")]
    [SerializeField]
    private Color validColor = Color.green;
    [SerializeField]
    private Color invalidColor = Color.red;
    [SerializeField]
    private Color warningColor = Color.yellow;

    #endregion

    #region Unity Lifecycle

    protected override void Awake()
    {
        base.Awake();

        // Navigation buttons
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);

        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveClicked);

        // Tab buttons
        if (deckTabButton != null)
            deckTabButton.onClick.AddListener(() => SwitchToTab(BarracksTab.Deck));

        if (doctrinesTabButton != null)
            doctrinesTabButton.onClick.AddListener(() => SwitchToTab(BarracksTab.Doctrines));

        if (perksTabButton != null)
            perksTabButton.onClick.AddListener(() => SwitchToTab(BarracksTab.Perks));
    }

    protected override void OnShow()
    {
        var progress = PlayerProgressData.Instance;
        if (progress != null)
        {
            int specialCount = progress.currentDeck.Count(c => c.definition != null && c.definition.isSpecialCard);
            Debug.Log($"[BarracksScreen] Opening. Deck: {progress.currentDeck.Count} cards ({specialCount} specials), Collection: {progress.ownedCards.Count} cards");
        }

        // Always start on Deck tab
        SwitchToTab(BarracksTab.Deck);
    }

    protected override void OnHide()
    {
        // Auto-save on hide if deck is valid
        var progress = PlayerProgressData.Instance;
        if (progress != null)
        {
            int specialCount = progress.currentDeck.Count(c => c.definition != null && c.definition.isSpecialCard);
            Debug.Log($"[BarracksScreen] Closing. Deck: {progress.currentDeck.Count} cards ({specialCount} specials)");

            if (progress.IsDeckValid())
            {
                progress.SaveToJson();
                Debug.Log("[BarracksScreen] Progress saved to JSON.");
            }
            else
            {
                Debug.LogWarning("[BarracksScreen] Deck is invalid, NOT saving.");
            }
        }

        // Clear all UI elements
        ClearAllUIElements();
    }

    #endregion

    #region Tab Management

    private void SwitchToTab(BarracksTab tab)
    {
        currentTab = tab;

        // Hide all panels first (hide-all-then-show-selected pattern)
        HideAllPanels();

        // Update tab button visuals
        UpdateTabButtonVisuals();

        // Show and populate the selected tab
        switch (tab)
        {
            case BarracksTab.Deck:
                ShowDeckTab();
                break;
            case BarracksTab.Doctrines:
                ShowDoctrinesTab();
                break;
            case BarracksTab.Perks:
                ShowPerksTab();
                break;
        }

        Debug.Log($"[BarracksScreen] Switched to {tab} tab");
    }

    private void HideAllPanels()
    {
        if (deckPanel != null)
            deckPanel.SetActive(false);

        if (doctrinesPanel != null)
            doctrinesPanel.SetActive(false);

        if (perksPanel != null)
            perksPanel.SetActive(false);
    }

    private void UpdateTabButtonVisuals()
    {
        UpdateTabButtonColor(deckTabButton, currentTab == BarracksTab.Deck);
        UpdateTabButtonColor(doctrinesTabButton, currentTab == BarracksTab.Doctrines);
        UpdateTabButtonColor(perksTabButton, currentTab == BarracksTab.Perks);
    }

    private void UpdateTabButtonColor(Button button, bool isActive)
    {
        if (button == null) return;

        var colors = button.colors;
        colors.normalColor = isActive ? activeTabColor : inactiveTabColor;
        colors.highlightedColor = isActive ? activeTabColor : inactiveTabColor;
        button.colors = colors;

        // Also update text color if present
        var text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.color = isActive ? Color.black : Color.white;
    }

    private void ClearAllUIElements()
    {
        ClearCardItems(ownedCardItems);
        ClearCardItems(deckCardItems);
        ClearDoctrineItems();
        ClearPerkItems();
    }

    #endregion

    #region Deck Tab Logic

    private void ShowDeckTab()
    {
        if (deckPanel != null)
            deckPanel.SetActive(true);

        LoadCollection();
        LoadDeck();
        RefreshDeckStats();
    }

    private void LoadCollection()
    {
        ClearCardItems(ownedCardItems);

        var progress = PlayerProgressData.Instance;
        if (progress == null || cardItemPrefab == null || ownedCardsContainer == null) return;

        foreach (var card in progress.ownedCards)
        {
            var item = Instantiate(cardItemPrefab, ownedCardsContainer);
            item.Setup(card, isInDeck: false, OnCollectionCardClicked);
            ownedCardItems.Add(item);
        }
    }

    private void LoadDeck()
    {
        ClearCardItems(deckCardItems);

        var progress = PlayerProgressData.Instance;
        if (progress == null || cardItemPrefab == null || deckCardsContainer == null) return;

        foreach (var card in progress.currentDeck)
        {
            var item = Instantiate(cardItemPrefab, deckCardsContainer);
            item.Setup(card, isInDeck: true, OnDeckCardClicked);
            deckCardItems.Add(item);
        }
    }

    private void ClearCardItems(List<BarracksCardItem> items)
    {
        foreach (var item in items)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        items.Clear();
    }

    private void OnCollectionCardClicked(OwnedCard card)
    {
        var progress = PlayerProgressData.Instance;
        if (progress == null) return;

        if (!progress.CanAddToDeck(card.definition))
        {
            Debug.Log("[BarracksScreen] Cannot add card to deck (rules limit reached)");
            return;
        }

        if (progress.AddCardToDeck(card.instanceId))
        {
            string cardType = card.definition.isSpecialCard ? "SPECIAL" : "Unit";
            Debug.Log($"[BarracksScreen] Added {card.definition.cardName} ({cardType}) to deck.");

            LoadCollection();
            LoadDeck();
            RefreshDeckStats();
        }
    }

    private void OnDeckCardClicked(OwnedCard card)
    {
        var progress = PlayerProgressData.Instance;
        if (progress == null) return;

        if (progress.RemoveCardFromDeck(card.instanceId))
        {
            string cardType = card.definition.isSpecialCard ? "SPECIAL" : "Unit";
            Debug.Log($"[BarracksScreen] Removed {card.definition.cardName} ({cardType}) from deck.");

            LoadCollection();
            LoadDeck();
            RefreshDeckStats();
        }
    }

    private void RefreshDeckStats()
    {
        var progress = PlayerProgressData.Instance;
        if (progress == null) return;

        var (total, units, specials) = progress.GetDeckStats();
        bool isValid = progress.IsDeckValid();

        if (deckCountText != null)
        {
            deckCountText.text = $"Deck: {total}/{progress.minDeckSize}-{progress.maxDeckSize}";
            deckCountText.color = total >= progress.minDeckSize && total <= progress.maxDeckSize
                ? validColor : invalidColor;
        }

        if (unitCountText != null)
        {
            unitCountText.text = $"Units: {units}/{progress.maxUnitCards}";
            unitCountText.color = units <= progress.maxUnitCards ? validColor : invalidColor;
        }

        if (specialCountText != null)
        {
            specialCountText.text = $"Special: {specials}/{progress.maxSpecialCards}";
            specialCountText.color = specials <= progress.maxSpecialCards ? validColor : invalidColor;
        }

        if (validityText != null)
        {
            if (isValid)
            {
                validityText.text = "DECK VALID";
                validityText.color = validColor;
            }
            else if (total < progress.minDeckSize)
            {
                validityText.text = $"NEED {progress.minDeckSize - total} MORE CARDS";
                validityText.color = warningColor;
            }
            else
            {
                validityText.text = "DECK INVALID";
                validityText.color = invalidColor;
            }
        }

        if (saveButton != null)
            saveButton.interactable = isValid;
    }

    #endregion

    #region Doctrines Tab Logic

    private void ShowDoctrinesTab()
    {
        if (doctrinesPanel != null)
            doctrinesPanel.SetActive(true);

        LoadOwnedDoctrines();
        LoadEquippedDoctrines();
        RefreshDoctrineStats();
    }

    private void LoadOwnedDoctrines()
    {
        ClearDoctrineItems();

        var progress = PlayerProgressData.Instance;
        if (progress == null || doctrineItemPrefab == null || ownedDoctrinesContainer == null) return;

        foreach (var doctrine in progress.ownedDoctrines)
        {
            var item = Instantiate(doctrineItemPrefab, ownedDoctrinesContainer);
            item.Setup(doctrine, equipped: false, OnDoctrineItemClicked);
            ownedDoctrineItems.Add(item);
        }
    }

    private void LoadEquippedDoctrines()
    {
        // Clear existing slots
        ClearEquippedDoctrineSlots();

        var progress = PlayerProgressData.Instance;
        if (progress == null || doctrineSlotPrefab == null || equippedDoctrinesContainer == null) return;

        // Create 5 slots
        for (int i = 0; i < PlayerProgressData.MaxDoctrines; i++)
        {
            var slot = Instantiate(doctrineSlotPrefab, equippedDoctrinesContainer);
            DoctrineDefinition doctrine = i < progress.equippedDoctrines.Count
                ? progress.equippedDoctrines[i]
                : null;

            slot.Setup(doctrine, i, OnEquippedSlotClicked);
            equippedDoctrineSlots[i] = slot;
        }
    }

    private void ClearDoctrineItems()
    {
        foreach (var item in ownedDoctrineItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        ownedDoctrineItems.Clear();
    }

    private void ClearEquippedDoctrineSlots()
    {
        for (int i = 0; i < equippedDoctrineSlots.Length; i++)
        {
            if (equippedDoctrineSlots[i] != null)
            {
                Destroy(equippedDoctrineSlots[i].gameObject);
                equippedDoctrineSlots[i] = null;
            }
        }
    }

    private void OnDoctrineItemClicked(DoctrineDefinition doctrine, bool wasEquipped)
    {
        // Clicking owned doctrine = equip it
        if (!wasEquipped)
        {
            var progress = PlayerProgressData.Instance;
            if (progress == null) return;

            if (progress.EquipDoctrineFromCollection(doctrine))
            {
                Debug.Log($"[BarracksScreen] Equipped doctrine: {doctrine.displayName}");
                LoadOwnedDoctrines();
                LoadEquippedDoctrines();
                RefreshDoctrineStats();
            }
            else
            {
                Debug.Log("[BarracksScreen] Cannot equip doctrine (slots full or error)");
            }
        }
    }

    private void OnEquippedSlotClicked(int slotIndex, DoctrineDefinition currentDoctrine)
    {
        // Clicking equipped slot with doctrine = unequip it
        if (currentDoctrine != null)
        {
            var progress = PlayerProgressData.Instance;
            if (progress == null) return;

            if (progress.UnequipDoctrineToCollection(currentDoctrine))
            {
                Debug.Log($"[BarracksScreen] Unequipped doctrine: {currentDoctrine.displayName}");
                LoadOwnedDoctrines();
                LoadEquippedDoctrines();
                RefreshDoctrineStats();
            }
        }
        else
        {
            Debug.Log($"[BarracksScreen] Empty slot {slotIndex + 1} clicked - select a doctrine from the left to equip");
        }
    }

    private void RefreshDoctrineStats()
    {
        var progress = PlayerProgressData.Instance;
        if (progress == null) return;

        if (equippedDoctrinesCountText != null)
        {
            int equipped = progress.equippedDoctrines.Count;
            equippedDoctrinesCountText.text = $"Equipped: {equipped}/{PlayerProgressData.MaxDoctrines}";
            equippedDoctrinesCountText.color = equipped > 0 ? validColor : warningColor;
        }
    }

    #endregion

    #region Perks Tab Logic

    private void ShowPerksTab()
    {
        if (perksPanel != null)
            perksPanel.SetActive(true);

        LoadPerks();
    }

    private void LoadPerks()
    {
        ClearPerkItems();

        var progress = PlayerProgressData.Instance;
        if (progress == null || perksContainer == null) return;

        // Only show perks that have been purchased (tier > 0)
        var purchasedPerks = progress.perkLevels.Where(p => p.perk != null && p.currentTier > 0).ToList();

        if (purchasedPerks.Count == 0)
        {
            if (noPerksText != null)
            {
                noPerksText.gameObject.SetActive(true);
                noPerksText.text = "No perks purchased yet.\nVisit the War Cabinet to buy perks!";
            }
            return;
        }

        if (noPerksText != null)
            noPerksText.gameObject.SetActive(false);

        if (perkItemPrefab == null) return;

        foreach (var perkProgress in purchasedPerks)
        {
            var item = Instantiate(perkItemPrefab, perksContainer);
            item.Setup(perkProgress.perk, perkProgress.currentTier);
            perkItems.Add(item);
        }
    }

    private void ClearPerkItems()
    {
        foreach (var item in perkItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        perkItems.Clear();
    }

    #endregion

    #region Navigation

    private void OnBackClicked()
    {
        GameStateManager.Instance?.CloseBarracks();
    }

    private void OnSaveClicked()
    {
        var progress = PlayerProgressData.Instance;
        if (progress != null)
        {
            progress.SaveToJson();
            Debug.Log("[BarracksScreen] Progress saved!");
        }
    }

    #endregion
}
