using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// UI slot for a single doctrine at the bottom of the barracks.
/// </summary>
public class DoctrineSlot : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    [Tooltip("Icon showing the equipped doctrine")]
    [SerializeField]
    private Image iconImage;

    [Tooltip("Text showing doctrine name")]
    [SerializeField]
    private TMP_Text nameText;

    [Tooltip("Background frame when empty")]
    [SerializeField]
    private Image emptyFrame;

    [Tooltip("Background frame when filled")]
    [SerializeField]
    private Image filledFrame;

    [Tooltip("Slot number indicator")]
    [SerializeField]
    private TMP_Text slotNumberText;

    [Header("Empty State")]
    [SerializeField]
    private Sprite emptySlotSprite;

    [SerializeField]
    private string emptyText = "Empty";

    [Header("Hover")]
    [SerializeField]
    private float hoverScale = 1.1f;

    [SerializeField]
    private float hoverDuration = 0.15f;

    [SerializeField]
    private GameObject tooltipPanel;

    [SerializeField]
    private TMP_Text tooltipText;

    private DoctrineDefinition doctrine;
    private int slotIndex;
    private Action<int, DoctrineDefinition> onClick;
    private int hoverTweenId = -1;

    public void Setup(DoctrineDefinition doctrineDef, int index, Action<int, DoctrineDefinition> onClicked)
    {
        doctrine = doctrineDef;
        slotIndex = index;
        onClick = onClicked;

        // Slot number
        if (slotNumberText != null)
            slotNumberText.text = (index + 1).ToString();

        if (doctrine != null)
        {
            // Filled state
            if (iconImage != null && doctrine.iconSprite != null)
            {
                iconImage.sprite = doctrine.iconSprite;
                iconImage.color = Color.white;
            }

            if (nameText != null)
                nameText.text = doctrine.displayName;

            if (emptyFrame != null)
                emptyFrame.gameObject.SetActive(false);

            if (filledFrame != null)
                filledFrame.gameObject.SetActive(true);

            // Tooltip
            if (tooltipText != null)
                tooltipText.text = $"{doctrine.displayName}\n{doctrine.description}";
        }
        else
        {
            // Empty state
            if (iconImage != null)
            {
                iconImage.sprite = emptySlotSprite;
                iconImage.color = new Color(1, 1, 1, 0.3f);
            }

            if (nameText != null)
                nameText.text = emptyText;

            if (emptyFrame != null)
                emptyFrame.gameObject.SetActive(true);

            if (filledFrame != null)
                filledFrame.gameObject.SetActive(false);

            if (tooltipText != null)
                tooltipText.text = "Click to select a doctrine";
        }

        // Hide tooltip initially
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        onClick?.Invoke(slotIndex, doctrine);

        // Click feedback
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, Vector3.one * 0.9f, 0.08f)
            .setEaseOutQuad()
            .setOnComplete(() =>
            {
                LeanTween.scale(gameObject, Vector3.one, 0.15f).setEaseOutBack();
            });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // Scale up
        if (hoverTweenId != -1)
            LeanTween.cancel(hoverTweenId);

        hoverTweenId = LeanTween.scale(gameObject, Vector3.one * hoverScale, hoverDuration)
            .setEaseOutQuad()
            .id;

        // Show tooltip
        if (tooltipPanel != null)
            tooltipPanel.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Scale down
        if (hoverTweenId != -1)
            LeanTween.cancel(hoverTweenId);

        hoverTweenId = LeanTween.scale(gameObject, Vector3.one, hoverDuration)
            .setEaseOutQuad()
            .id;

        // Hide tooltip
        if (tooltipPanel != null)
            tooltipPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        LeanTween.cancel(gameObject);
    }
}
