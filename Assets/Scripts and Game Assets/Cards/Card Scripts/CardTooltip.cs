using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// CardTooltip: Displays detailed information about a card when hovered.
/// 
/// REQUIRES: LeanTween (free animation library)
/// </summary>
public class CardTooltip : MonoBehaviour
{
    [Header("References")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI statsText;

    [Header("Layout")]
    public Vector2 cursorOffset = new Vector2(20f, -20f);
    public float screenEdgePadding = 20f;

    [Header("Animation")]
    public float fadeInDuration = 0.15f;
    public float fadeOutDuration = 0.1f;

    private static CardTooltip instance;
    private CanvasGroup canvasGroup;
    private RectTransform rectTransform;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        canvasGroup.alpha = 0f;
        gameObject.SetActive(false);
    }

    public static void Show(UnitCard card, Vector2 screenPosition)
    {
        if (instance == null || card == null) return;

        instance.gameObject.SetActive(true);
        instance.UpdateContent(card);
        instance.UpdatePosition(screenPosition);
        instance.FadeIn();
    }

    public static void Hide()
    {
        if (instance == null) return;
        instance.FadeOut();
    }

    public static void UpdateTooltipPosition(Vector2 screenPosition)
    {
        if (instance == null || !instance.gameObject.activeSelf) return;
        instance.UpdatePosition(screenPosition);
    }

    private void UpdateContent(UnitCard card)
    {
        if (titleText != null)
            titleText.text = $"{card.Name} [{card.Type}]";

        if (descriptionText != null)
            descriptionText.text = GetAbilityDescription(card.Ability);

        if (statsText != null)
            statsText.text = $"Cost: {card.Cost} | ATK: {card.BaseATK} | HP: {card.BaseHP}";
    }

    private void UpdatePosition(Vector2 screenPosition)
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform,
            screenPosition,
            canvas.worldCamera,
            out Vector2 localPoint
        );

        localPoint += cursorOffset;

        Vector2 canvasSize = (canvas.transform as RectTransform).sizeDelta;
        Vector2 tooltipSize = rectTransform.sizeDelta;

        localPoint.x = Mathf.Clamp(localPoint.x, -canvasSize.x / 2 + screenEdgePadding, canvasSize.x / 2 - tooltipSize.x - screenEdgePadding);
        localPoint.y = Mathf.Clamp(localPoint.y, -canvasSize.y / 2 + screenEdgePadding, canvasSize.y / 2 - tooltipSize.y - screenEdgePadding);

        rectTransform.anchoredPosition = localPoint;
    }

    private void FadeIn()
    {
        LeanTween.cancel(gameObject);
        LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration).setEaseOutQuad();
    }

    private void FadeOut()
    {
        LeanTween.cancel(gameObject);
        LeanTween.alphaCanvas(canvasGroup, 0f, fadeOutDuration)
            .setEaseInQuad()
            .setOnComplete(() => gameObject.SetActive(false));
    }

    private string GetAbilityDescription(UnitAbilityType ability)
    {
        return ability switch
        {
            UnitAbilityType.None => "No special ability.",
            UnitAbilityType.Taunt => "<b>Taunt:</b> Forces enemy damage to hit this row first.",
            UnitAbilityType.ArmorPiercing => "<b>Armor Piercing:</b> Deals 2x damage if enemy has vehicles.",
            UnitAbilityType.ThreeOfAKind => "<b>3-of-a-Kind:</b> Deals 2x damage with 3+ copies in row.",
            UnitAbilityType.Unmatched => "<b>Unmatched:</b> Deals 2x damage at max health.",
            UnitAbilityType.Armored => "<b>Armored:</b> Spawns with 2x HP (visible on health bar).",
            UnitAbilityType.Counterattack => "<b>Counterattack:</b> Deals 50% damage back.",
            UnitAbilityType.DirectAttack => "<b>Direct Attack:</b> 25% damage goes directly to commander.",
            UnitAbilityType.TargetMark => "<b>Mark Target:</b> Marks random enemy, halving their HP immediately.",
            UnitAbilityType.MrMoneybags => "<b>Mr. Moneybags:</b> Grants bonus gold when played.",
            UnitAbilityType.Duplicate => "<b>Duplicate:</b> Plays another copy from deck.",
            UnitAbilityType.DestroyStrongestVehicle => "<b>Anti-Vehicle:</b> Destroys strongest enemy vehicle if row is full.",
            _ => $"<i>{ability}</i>"
        };
    }
}