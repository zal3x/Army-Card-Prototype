using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// UI element for a single perk with tier upgrades.
/// </summary>
public class PerkItemUI : MonoBehaviour
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
    private TMP_Text costText;

    [SerializeField]
    private TMP_Text effectText;

    [SerializeField]
    private Button upgradeButton;

    [SerializeField]
    private TMP_Text upgradeButtonText;

    [Header("Tier Indicators")]
    [SerializeField]
    private Image[] tierIndicators;

    [SerializeField]
    private Color activeColor = Color.yellow;

    [SerializeField]
    private Color inactiveColor = Color.gray;

    private PerkDefinition perk;
    private Action<PerkDefinition> onClick;

    private void Awake()
    {
        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
    }

    public void Setup(PerkDefinition perkDef, Action<PerkDefinition> onUpgrade)
    {
        perk = perkDef;
        onClick = onUpgrade;

        if (perk == null) return;

        // Get current tier from player progress
        int currentTier = PlayerProgressData.Instance?.GetPerkTier(perk) ?? 0;
        bool isMaxed = currentTier >= perk.maxTier;

        // Name and icon
        if (nameText != null)
            nameText.text = perk.displayName;

        if (iconImage != null && perk.iconSprite != null)
            iconImage.sprite = perk.iconSprite;

        // Description
        if (descriptionText != null)
            descriptionText.text = perk.description;

        // Tier display
        if (tierText != null)
            tierText.text = $"Tier: {currentTier}/{perk.maxTier}";

        // Effect text
        if (effectText != null)
            effectText.text = perk.GetTierDescription(currentTier);

        // Cost for next tier
        if (costText != null)
        {
            if (isMaxed)
                costText.text = "MAX";
            else
                costText.text = $"Next: {perk.GetCostForTier(currentTier + 1)} WF";
        }

        // Update tier indicators
        UpdateTierIndicators(currentTier);

        // Button state
        UpdateButtonState(currentTier, isMaxed);
    }

    private void UpdateTierIndicators(int currentTier)
    {
        if (tierIndicators == null) return;

        for (int i = 0; i < tierIndicators.Length; i++)
        {
            if (tierIndicators[i] != null)
            {
                tierIndicators[i].color = i < currentTier ? activeColor : inactiveColor;
            }
        }
    }

    private void UpdateButtonState(int currentTier, bool isMaxed)
    {
        if (upgradeButton == null) return;

        if (isMaxed)
        {
            upgradeButton.interactable = false;
            if (upgradeButtonText != null)
                upgradeButtonText.text = "MAXED";
        }
        else
        {
            int nextCost = perk.GetCostForTier(currentTier + 1);
            bool canAfford = PlayerProgressData.Instance != null &&
                            PlayerProgressData.Instance.warFunds >= nextCost;

            upgradeButton.interactable = canAfford;

            if (upgradeButtonText != null)
                upgradeButtonText.text = canAfford ? "UPGRADE" : "NOT ENOUGH";

            if (costText != null)
                costText.color = canAfford ? Color.white : Color.red;
        }
    }

    private void OnUpgradeClicked()
    {
        onClick?.Invoke(perk);
    }
}
