using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI element for a single shop item (card or doctrine).
/// </summary>
public class ShopItemUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text descriptionText;

    [SerializeField]
    private TMP_Text priceText;

    [SerializeField]
    private TMP_Text statsText;

    [SerializeField]
    private Image rarityBorder;

    [SerializeField]
    private Button purchaseButton;

    [Header("Image Mode (Doctrines)")]
    [Tooltip("Background art image for doctrine template display")]
    [SerializeField]
    private Image backgroundArtImage;

    [Tooltip("Default doctrine template sprite (DoctineDesign.png, 128x192)")]
    [SerializeField]
    private Sprite defaultDoctrineTemplate;

    [Header("Rarity Colors")]
    [SerializeField]
    private Color commonColor = Color.gray;
    [SerializeField]
    private Color uncommonColor = Color.green;
    [SerializeField]
    private Color rareColor = Color.blue;
    [SerializeField]
    private Color epicColor = new Color(0.6f, 0.2f, 0.8f);
    [SerializeField]
    private Color legendaryColor = new Color(1f, 0.8f, 0f);

    private ShopItem cardItem;
    private DoctrineShopItem doctrineItem;
    private Action<ShopItem> onCardClicked;
    private Action<DoctrineShopItem> onDoctrineClicked;

    private void Awake()
    {
        if (purchaseButton != null)
            purchaseButton.onClick.AddListener(OnPurchaseClicked);
    }

    public void SetupCard(ShopItem item, Action<ShopItem> onClick)
    {
        cardItem = item;
        doctrineItem = null;
        onCardClicked = onClick;
        onDoctrineClicked = null;

        if (item == null || item.card == null) return;

        var card = item.card;

        if (nameText != null)
            nameText.text = card.cardName;

        if (iconImage != null)
        {
            Sprite portrait = GetCardPortrait(card);
            if (portrait != null)
                iconImage.sprite = portrait;
        }

        if (descriptionText != null)
            descriptionText.text = card.abilityDescription;

        if (statsText != null)
            statsText.text = card.isSpecialCard ? "Special" : $"ATK: {card.atk}  HP: {card.hp}";

        if (priceText != null)
            priceText.text = $"{item.price} WF";

        SetRarityColor(card.rarity);
        UpdateAffordability(item.price);
    }

    public void SetupDoctrine(DoctrineShopItem item, Action<DoctrineShopItem> onClick)
    {
        cardItem = null;
        doctrineItem = item;
        onCardClicked = null;
        onDoctrineClicked = onClick;

        if (item == null || item.doctrine == null) return;

        var doctrine = item.doctrine;

        // Apply doctrine template background
        if (backgroundArtImage != null && defaultDoctrineTemplate != null)
        {
            backgroundArtImage.sprite = defaultDoctrineTemplate;
            backgroundArtImage.enabled = true;
        }

        if (nameText != null)
            nameText.text = doctrine.displayName;

        if (iconImage != null && doctrine.iconSprite != null)
            iconImage.sprite = doctrine.iconSprite;

        if (descriptionText != null)
            descriptionText.text = doctrine.description;

        if (statsText != null)
            statsText.text = "Doctrine";

        if (priceText != null)
            priceText.text = $"{item.price} WF";

        SetRarityColor(doctrine.rarity);
        UpdateAffordability(item.price);
    }

    private void SetRarityColor(CardRarity rarity)
    {
        if (rarityBorder == null) return;

        rarityBorder.color = rarity switch
        {
            CardRarity.Common => commonColor,
            CardRarity.Uncommon => uncommonColor,
            CardRarity.Rare => rareColor,
            CardRarity.Epic => epicColor,
            CardRarity.Legendary => legendaryColor,
            _ => commonColor
        };
    }

    private void UpdateAffordability(int price)
    {
        if (purchaseButton == null) return;

        var progress = PlayerProgressData.Instance;
        bool canAfford = progress != null && progress.warFunds >= price;

        purchaseButton.interactable = canAfford;

        if (priceText != null)
            priceText.color = canAfford ? Color.white : Color.red;
    }

    private void OnPurchaseClicked()
    {
        if (cardItem != null)
            onCardClicked?.Invoke(cardItem);
        else if (doctrineItem != null)
            onDoctrineClicked?.Invoke(doctrineItem);
    }

    /// <summary>
    /// Gets the portrait sprite for a card, falling back to extracting from the unit prefab if needed.
    /// For special cards, uses the backgroundSprite instead.
    /// </summary>
    private Sprite GetCardPortrait(UnitCardDefinition card)
    {
        // Prefer explicitly authored portrait
        if (card.portraitSprite != null)
            return card.portraitSprite;

        // Special cards use backgroundSprite as their icon
        if (card.isSpecialCard && card.backgroundSprite != null)
            return card.backgroundSprite;

        // Fallback: try to extract from world prefab
        GameObject prefab = card.worldPrefabA != null ? card.worldPrefabA : card.worldPrefabB;
        if (prefab == null) return null;

        // Try SpriteRenderer first (common for 2D units)
        var sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.sprite;

        // Try Unity UI Image (if the prefab uses UI art)
        var img = prefab.GetComponentInChildren<Image>();
        if (img != null && img.sprite != null)
            return img.sprite;

        return null;
    }
}
