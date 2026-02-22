using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Panel displaying purchasable items (cards or doctrines) in the War Cabinet.
/// </summary>
public class ShopPanel : MonoBehaviour
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
    private ShopItemUI itemPrefab;

    [Header("Scroll")]
    [SerializeField]
    private ScrollRect scrollRect;

    public bool IsVisible => root != null && root.activeSelf;

    private List<ShopItemUI> activeItems = new List<ShopItemUI>();
    private Action<ShopItem> onCardPurchased;
    private Action<DoctrineShopItem> onDoctrinePurchased;

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
    /// Populate panel with card shop items.
    /// </summary>
    public void PopulateWithCards(List<ShopItem> items, Action<ShopItem> onPurchase)
    {
        ClearItems();
        onCardPurchased = onPurchase;
        onDoctrinePurchased = null;

        if (items == null || itemPrefab == null || itemContainer == null) return;

        foreach (var item in items)
        {
            var ui = Instantiate(itemPrefab, itemContainer);
            ui.SetupCard(item, OnCardItemClicked);
            activeItems.Add(ui);
        }
    }

    /// <summary>
    /// Populate panel with doctrine shop items.
    /// </summary>
    public void PopulateWithDoctrines(List<DoctrineShopItem> items, Action<DoctrineShopItem> onPurchase)
    {
        ClearItems();
        onCardPurchased = null;
        onDoctrinePurchased = onPurchase;

        if (items == null || itemPrefab == null || itemContainer == null) return;

        foreach (var item in items)
        {
            var ui = Instantiate(itemPrefab, itemContainer);
            ui.SetupDoctrine(item, OnDoctrineItemClicked);
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

    private void OnCardItemClicked(ShopItem item)
    {
        onCardPurchased?.Invoke(item);
    }

    private void OnDoctrineItemClicked(DoctrineShopItem item)
    {
        onDoctrinePurchased?.Invoke(item);
    }

    private void OnDestroy()
    {
        ClearItems();
    }
}
