using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel displaying purchasable perks in the War Cabinet.
/// Each perk has multiple upgrade tiers.
/// </summary>
public class PerkPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField]
    private GameObject root;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private Button closeButton;

    [Header("Item Container")]
    [SerializeField]
    private Transform itemContainer;

    [SerializeField]
    private PerkItemUI itemPrefab;

    [Header("Scroll")]
    [SerializeField]
    private ScrollRect scrollRect;

    public bool IsVisible => root != null && root.activeSelf;

    private List<PerkItemUI> activeItems = new List<PerkItemUI>();
    private Action<PerkDefinition> onPerkUpgraded;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (root != null)
            root.SetActive(false);
    }

    public void SetTitle(string title)
    {
        if (titleText != null)
            titleText.text = title;
    }

    public void Show()
    {
        if (root != null)
            root.SetActive(true);
    }

    public void Hide()
    {
        if (root != null)
            root.SetActive(false);
    }

    /// <summary>
    /// Populate panel with perks.
    /// </summary>
    public void PopulateWithPerks(List<PerkDefinition> perks, Action<PerkDefinition> onUpgrade)
    {
        ClearItems();
        onPerkUpgraded = onUpgrade;

        if (perks == null || itemPrefab == null || itemContainer == null) return;

        foreach (var perk in perks)
        {
            var ui = Instantiate(itemPrefab, itemContainer);
            ui.Setup(perk, OnPerkClicked);
            activeItems.Add(ui);
        }
    }

    private void ClearItems()
    {
        foreach (var item in activeItems)
        {
            if (item != null)
                Destroy(item.gameObject);
        }
        activeItems.Clear();
    }

    private void OnPerkClicked(PerkDefinition perk)
    {
        onPerkUpgraded?.Invoke(perk);
    }

    private void OnDestroy()
    {
        ClearItems();
    }
}
