using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// War Cabinet screen with 4 NPCs selling different items.
/// - Field Recruiter: Unit cards
/// - Intelligence Officer: Special cards
/// - Chief of Staff: Doctrines
/// - Defense Minister: Perks
/// </summary>
public class WarCabinetScreen : OverworldScreen
{
    [Header("NPC Buttons")]
    [Tooltip("Button to open Field Recruiter shop (unit cards)")]
    [SerializeField]
    private Button fieldRecruiterButton;

    [Tooltip("Button to open Intelligence Officer shop (special cards)")]
    [SerializeField]
    private Button intelligenceOfficerButton;

    [Tooltip("Button to open Chief of Staff shop (doctrines)")]
    [SerializeField]
    private Button chiefOfStaffButton;

    [Tooltip("Button to open Defense Minister shop (perks)")]
    [SerializeField]
    private Button defenseMinisterButton;

    [Header("Shop Panels")]
    [Tooltip("Panel for card purchases")]
    [SerializeField]
    private ShopPanel cardShopPanel;

    [Tooltip("Panel for doctrine purchases")]
    [SerializeField]
    private ShopPanel doctrineShopPanel;

    [Tooltip("Panel for perk upgrades")]
    [SerializeField]
    private PerkPanel perkPanel;

    [Header("Configuration")]
    [Tooltip("Shop configuration (drop rates, pools)")]
    [SerializeField]
    private ShopInventoryConfig shopConfig;

    [Header("HUD")]
    [Tooltip("Display current war funds")]
    [SerializeField]
    private TMP_Text warFundsText;

    [Tooltip("Button to exit to world map")]
    [SerializeField]
    private Button exitButton;

    private List<ShopItem> currentUnitCards;
    private List<ShopItem> currentSpecialCards;
    private List<DoctrineShopItem> currentDoctrines;

    protected override void Awake()
    {
        base.Awake();

        // Wire up NPC buttons
        if (fieldRecruiterButton != null)
            fieldRecruiterButton.onClick.AddListener(OnFieldRecruiterClicked);

        if (intelligenceOfficerButton != null)
            intelligenceOfficerButton.onClick.AddListener(OnIntelligenceOfficerClicked);

        if (chiefOfStaffButton != null)
            chiefOfStaffButton.onClick.AddListener(OnChiefOfStaffClicked);

        if (defenseMinisterButton != null)
            defenseMinisterButton.onClick.AddListener(OnDefenseMinisterClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        // Hide all shop panels initially
        HideAllShopPanels();
    }

    protected override void OnShow()
    {
        RefreshWarFundsDisplay();
        GenerateShopInventory();
        HideAllShopPanels();
    }

    protected override void OnHide()
    {
        HideAllShopPanels();
    }

    private void RefreshWarFundsDisplay()
    {
        if (warFundsText != null && PlayerProgressData.Instance != null)
        {
            warFundsText.text = $"War Funds: {PlayerProgressData.Instance.warFunds}";
        }
    }

    private void GenerateShopInventory()
    {
        if (shopConfig == null)
        {
            Debug.LogWarning("[WarCabinetScreen] ShopInventoryConfig not assigned!");
            return;
        }

        // Generate fresh inventory for each visit
        currentUnitCards = shopConfig.GenerateUnitCardShop();
        currentSpecialCards = shopConfig.GenerateSpecialCardShop();
        currentDoctrines = shopConfig.GenerateDoctrineShop();

        Debug.Log($"[WarCabinetScreen] Generated shop inventory: {currentUnitCards.Count} units, {currentSpecialCards.Count} specials, {currentDoctrines.Count} doctrines");
    }

    private void HideAllShopPanels()
    {
        if (cardShopPanel != null)
            cardShopPanel.Hide();

        if (doctrineShopPanel != null)
            doctrineShopPanel.Hide();

        if (perkPanel != null)
            perkPanel.Hide();
    }

    private void OnFieldRecruiterClicked()
    {
        HideAllShopPanels();

        if (cardShopPanel != null)
        {
            cardShopPanel.SetTitle("Field Recruiter - Unit Cards");
            cardShopPanel.PopulateWithCards(currentUnitCards, OnCardPurchased);
            cardShopPanel.Show();
        }
    }

    private void OnIntelligenceOfficerClicked()
    {
        HideAllShopPanels();

        if (cardShopPanel != null)
        {
            cardShopPanel.SetTitle("Intelligence Officer - Special Cards");
            cardShopPanel.PopulateWithCards(currentSpecialCards, OnCardPurchased);
            cardShopPanel.Show();
        }
    }

    private void OnChiefOfStaffClicked()
    {
        HideAllShopPanels();

        if (doctrineShopPanel != null)
        {
            doctrineShopPanel.SetTitle("Chief of Staff - Doctrines");
            doctrineShopPanel.PopulateWithDoctrines(currentDoctrines, OnDoctrinePurchased);
            doctrineShopPanel.Show();
        }
    }

    private void OnDefenseMinisterClicked()
    {
        HideAllShopPanels();

        if (perkPanel != null && shopConfig != null)
        {
            perkPanel.SetTitle("Defense Minister - Perks");
            perkPanel.PopulateWithPerks(shopConfig.perkPool, OnPerkUpgraded);
            perkPanel.Show();
        }
    }

    private void OnCardPurchased(ShopItem item)
    {
        if (item == null || item.card == null) return;

        var progress = PlayerProgressData.Instance;
        if (progress == null) return;

        // Check funds
        if (progress.warFunds < item.price)
        {
            Debug.Log("[WarCabinetScreen] Not enough war funds!");
            return;
        }

        // Deduct funds and add card
        progress.warFunds -= item.price;
        progress.AddCard(item.card);
        progress.SaveToJson();

        // Remove from shop
        currentUnitCards.Remove(item);
        currentSpecialCards.Remove(item);

        RefreshWarFundsDisplay();

        // Refresh the current panel
        if (cardShopPanel != null && cardShopPanel.IsVisible)
        {
            // Determine which list to show
            bool isSpecial = item.card.isSpecialCard;
            cardShopPanel.PopulateWithCards(
                isSpecial ? currentSpecialCards : currentUnitCards,
                OnCardPurchased
            );
        }

        Debug.Log($"[WarCabinetScreen] Purchased card: {item.card.cardName} for {item.price} war funds");
    }

    private void OnDoctrinePurchased(DoctrineShopItem item)
    {
        if (item == null || item.doctrine == null) return;

        var progress = PlayerProgressData.Instance;
        if (progress == null) return;

        // Check funds
        if (progress.warFunds < item.price)
        {
            Debug.Log("[WarCabinetScreen] Not enough war funds!");
            return;
        }

        // Check if already owned (either equipped or in collection)
        if (progress.OwnsDoctrine(item.doctrine))
        {
            Debug.Log("[WarCabinetScreen] Already own this doctrine!");
            return;
        }

        // Deduct funds and add to owned collection (not equipped)
        progress.warFunds -= item.price;
        progress.AddDoctrineToCollection(item.doctrine);
        progress.SaveToJson();

        // Remove from shop
        currentDoctrines.Remove(item);

        RefreshWarFundsDisplay();

        // Refresh panel
        if (doctrineShopPanel != null && doctrineShopPanel.IsVisible)
        {
            doctrineShopPanel.PopulateWithDoctrines(currentDoctrines, OnDoctrinePurchased);
        }

        Debug.Log($"[WarCabinetScreen] Purchased doctrine: {item.doctrine.displayName} for {item.price} war funds (added to collection)");
    }

    private void OnPerkUpgraded(PerkDefinition perk)
    {
        if (perk == null) return;

        var progress = PlayerProgressData.Instance;
        if (progress == null) return;

        // Attempt upgrade (handles cost checking internally)
        if (progress.UpgradePerk(perk))
        {
            progress.SaveToJson();
            RefreshWarFundsDisplay();

            // Refresh perk panel
            if (perkPanel != null && perkPanel.IsVisible && shopConfig != null)
            {
                perkPanel.PopulateWithPerks(shopConfig.perkPool, OnPerkUpgraded);
            }

            Debug.Log($"[WarCabinetScreen] Upgraded perk: {perk.displayName}");
        }
        else
        {
            Debug.Log("[WarCabinetScreen] Cannot upgrade perk (not enough funds or max tier)");
        }
    }

    private void OnExitClicked()
    {
        GameStateManager.Instance?.ExitWarCabinetToWorldMap();
    }
}
