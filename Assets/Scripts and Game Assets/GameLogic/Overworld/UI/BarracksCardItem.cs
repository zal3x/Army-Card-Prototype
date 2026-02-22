using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI element for a single card in the barracks deck builder.
/// </summary>
public class BarracksCardItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    [SerializeField]
    private Image portraitImage;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text statsText;

    [SerializeField]
    private TMP_Text costText;

    [SerializeField]
    private Image rarityBorder;

    [SerializeField]
    private Image typeIcon;

    [SerializeField]
    private GameObject bonusIndicator;

    [SerializeField]
    private TMP_Text bonusText;

    [Header("Hover Effect")]
    [SerializeField]
    private float hoverScale = 1.1f;

    [SerializeField]
    private float hoverDuration = 0.15f;

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

    private OwnedCard card;
    private bool isInDeck;
    private Action<OwnedCard> onClick;
    private int hoverTweenId = -1;

    public void Setup(OwnedCard ownedCard, bool isInDeck, Action<OwnedCard> onClicked)
    {
        card = ownedCard;
        this.isInDeck = isInDeck;
        onClick = onClicked;

        if (card == null || card.definition == null) return;

        var def = card.definition;

        // Portrait - fetch from prefab or use background for special cards
        if (portraitImage != null)
        {
            Sprite portrait = GetCardPortrait(def);
            if (portrait != null)
                portraitImage.sprite = portrait;
        }

        // Name
        if (nameText != null)
            nameText.text = def.cardName;

        // Stats
        if (statsText != null)
        {
            if (def.isSpecialCard)
                statsText.text = "Special";
            else
            {
                int totalAtk = def.atk + card.bonusAtk;
                int totalHp = def.hp + card.bonusHp;
                statsText.text = $"{totalAtk}/{totalHp}";
            }
        }

        // Cost
        if (costText != null)
            costText.text = def.cost.ToString();

        // Rarity
        if (rarityBorder != null)
        {
            rarityBorder.color = def.rarity switch
            {
                CardRarity.Common => commonColor,
                CardRarity.Uncommon => uncommonColor,
                CardRarity.Rare => rareColor,
                CardRarity.Epic => epicColor,
                CardRarity.Legendary => legendaryColor,
                _ => commonColor
            };
        }

        // Bonus indicator (for Medals perk)
        if (bonusIndicator != null)
        {
            bool hasBonus = card.bonusAtk > 0 || card.bonusHp > 0;
            bonusIndicator.SetActive(hasBonus);

            if (hasBonus && bonusText != null)
                bonusText.text = $"+{card.bonusAtk}/+{card.bonusHp}";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke(card);

        // Click feedback
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, Vector3.one * 0.95f, 0.08f)
            .setEaseOutQuad()
            .setOnComplete(() =>
            {
                LeanTween.scale(gameObject, Vector3.one, 0.1f).setEaseOutBack();
            });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverTweenId != -1)
            LeanTween.cancel(hoverTweenId);

        hoverTweenId = LeanTween.scale(gameObject, Vector3.one * hoverScale, hoverDuration)
            .setEaseOutQuad()
            .id;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverTweenId != -1)
            LeanTween.cancel(hoverTweenId);

        hoverTweenId = LeanTween.scale(gameObject, Vector3.one, hoverDuration)
            .setEaseOutQuad()
            .id;
    }

    private void OnDestroy()
    {
        LeanTween.cancel(gameObject);
    }

    /// <summary>
    /// Gets the portrait sprite for a card.
    /// Unit cards: extracts from worldPrefabA's SpriteRenderer
    /// Special cards: uses the backgroundSprite
    /// Falls back to portraitSprite if explicitly set
    /// </summary>
    private Sprite GetCardPortrait(UnitCardDefinition def)
    {
        // Prefer explicitly authored portrait
        if (def.portraitSprite != null)
            return def.portraitSprite;

        // Special cards use backgroundSprite as their portrait
        if (def.isSpecialCard)
        {
            return def.backgroundSprite;
        }

        // Unit cards: extract from worldPrefabA
        GameObject prefab = def.worldPrefabA;
        if (prefab == null)
            prefab = def.worldPrefabB;

        if (prefab == null)
            return null;

        // Try SpriteRenderer first (common for 2D units)
        var sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
            return sr.sprite;

        // Try Unity UI Image (if prefab uses UI art)
        var img = prefab.GetComponentInChildren<Image>();
        if (img != null && img.sprite != null)
            return img.sprite;

        return null;
    }
}
