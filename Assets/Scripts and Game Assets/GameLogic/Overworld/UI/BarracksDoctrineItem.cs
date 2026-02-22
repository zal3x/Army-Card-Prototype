using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI element for a single doctrine in the barracks doctrine manager.
/// Used for both owned (unequipped) and equipped doctrine displays.
/// </summary>
public class BarracksDoctrineItem : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text descriptionText;

    [SerializeField]
    private Image rarityBorder;

    [SerializeField]
    private GameObject equippedIndicator;

    [Header("Hover Effect")]
    [SerializeField]
    private float hoverScale = 1.1f;

    [SerializeField]
    private float hoverDuration = 0.15f;

    [Header("Tooltip")]
    [SerializeField]
    private GameObject tooltipPanel;

    [SerializeField]
    private TMP_Text tooltipText;

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

    private DoctrineDefinition doctrine;
    private bool isEquipped;
    private Action<DoctrineDefinition, bool> onClick;
    private int hoverTweenId = -1;

    /// <summary>
    /// Setup the doctrine item display.
    /// </summary>
    /// <param name="doctrineDef">The doctrine to display</param>
    /// <param name="equipped">Whether this doctrine is currently equipped</param>
    /// <param name="onClicked">Callback when clicked (doctrine, wasEquipped)</param>
    public void Setup(DoctrineDefinition doctrineDef, bool equipped, Action<DoctrineDefinition, bool> onClicked)
    {
        doctrine = doctrineDef;
        isEquipped = equipped;
        onClick = onClicked;

        if (doctrine == null) return;

        // Icon
        if (iconImage != null && doctrine.iconSprite != null)
        {
            iconImage.sprite = doctrine.iconSprite;
            iconImage.color = Color.white;
        }

        // Name
        if (nameText != null)
            nameText.text = doctrine.displayName;

        // Description (short form)
        if (descriptionText != null)
            descriptionText.text = GetShortDescription();

        // Rarity border
        if (rarityBorder != null)
        {
            rarityBorder.color = doctrine.rarity switch
            {
                CardRarity.Common => commonColor,
                CardRarity.Uncommon => uncommonColor,
                CardRarity.Rare => rareColor,
                CardRarity.Epic => epicColor,
                CardRarity.Legendary => legendaryColor,
                _ => commonColor
            };
        }

        // Equipped indicator
        if (equippedIndicator != null)
            equippedIndicator.SetActive(isEquipped);

        // Tooltip
        if (tooltipText != null)
            tooltipText.text = $"{doctrine.displayName}\n\n{doctrine.description}";

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private string GetShortDescription()
    {
        // Return first 50 chars or up to first newline
        if (string.IsNullOrEmpty(doctrine.description))
            return "";

        string desc = doctrine.description;
        int newlineIndex = desc.IndexOf('\n');
        if (newlineIndex > 0 && newlineIndex < 50)
            return desc.Substring(0, newlineIndex) + "...";
        if (desc.Length > 50)
            return desc.Substring(0, 47) + "...";
        return desc;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke(doctrine, isEquipped);

        // Click feedback animation
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

        if (tooltipPanel != null)
            tooltipPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverTweenId != -1)
            LeanTween.cancel(hoverTweenId);

        hoverTweenId = LeanTween.scale(gameObject, Vector3.one, hoverDuration)
            .setEaseOutQuad()
            .id;

        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        LeanTween.cancel(gameObject);
    }
}
