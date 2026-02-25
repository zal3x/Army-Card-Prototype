using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Read-only display of a purchased perk with its current tier.
/// Used in the Barracks Perks tab to show what perks the player has.
/// </summary>
public class BarracksPerkItem : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField]
    private Image iconImage;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text descriptionText;

    [SerializeField]
    private TMP_Text tierText;

    [SerializeField]
    private TMP_Text effectText;

    [Header("Image Mode")]
    [Tooltip("Background art image for perk template display")]
    [SerializeField]
    private Image backgroundArtImage;

    [Tooltip("Default perk template sprite (PerkOutline.png, 384x96)")]
    [SerializeField]
    private Sprite defaultPerkTemplate;

    [Header("Tier Indicators")]
    [SerializeField]
    private Image[] tierIndicators;

    [SerializeField]
    private Color activeColor = Color.yellow;

    [SerializeField]
    private Color inactiveColor = Color.gray;

    /// <summary>
    /// Setup the perk display (read-only, no upgrade button).
    /// </summary>
    public void Setup(PerkDefinition perk, int currentTier)
    {
        if (perk == null) return;

        // Apply perk template background
        if (backgroundArtImage != null && defaultPerkTemplate != null)
        {
            backgroundArtImage.sprite = defaultPerkTemplate;
            backgroundArtImage.enabled = true;
        }

        // Icon
        if (iconImage != null && perk.iconSprite != null)
            iconImage.sprite = perk.iconSprite;

        // Name
        if (nameText != null)
            nameText.text = perk.displayName;

        // Description
        if (descriptionText != null)
            descriptionText.text = perk.description;

        // Tier display
        if (tierText != null)
        {
            bool isMaxed = currentTier >= perk.maxTier;
            tierText.text = isMaxed
                ? $"Tier: {currentTier}/{perk.maxTier} (MAX)"
                : $"Tier: {currentTier}/{perk.maxTier}";
        }

        // Effect at current tier
        if (effectText != null)
            effectText.text = perk.GetTierDescription(currentTier);

        // Update tier indicators
        if (tierIndicators != null)
        {
            for (int i = 0; i < tierIndicators.Length; i++)
            {
                if (tierIndicators[i] != null)
                {
                    tierIndicators[i].color = i < currentTier ? activeColor : inactiveColor;
                }
            }
        }
    }
}
