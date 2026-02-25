using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;

/// <summary>
/// CardVisual: Visual representation of a UnitCard in the UI.
///
/// FLIGHT ANIMATION SYSTEM:
/// - Keep card in Screen Space UI the whole time.
/// - Convert WORLD target to SCREEN using worldCamera.WorldToScreenPoint.
/// - Convert screen points to local points in a FlightLayer under the ROOT canvas.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class CardVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Card Data")]
    public UnitCard cardData;

    [Header("Visual References")]
    public Image cardBackground;
    public Image unitPortrait;

    [Header("Text Fields")]
    public TextMeshProUGUI costText;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI typeText;
    public TextMeshProUGUI abilityText;
    public TextMeshProUGUI attackText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI rankText;

    [Header("Hover Effects")]
    public float hoverScale = 1.05f;
    public float hoverAnimDuration = 0.15f;
    [Tooltip("Delay before hover effect starts (prevents rapid flickering)")]
    public float hoverDelay = 0.2f;
    public Image hoverGlow;

    [Header("Selection State")]
    public bool isSelected = false;
    public Image selectionOutline;

    [Header("Flight Animation Settings")]
    [Tooltip("Duration of the flight animation in seconds")]
    public float flightDuration = 0.5f;

public float FlightDuration => flightDuration;
public Vector3 OriginalScale => originalScale;

    [Header("Flight Scaling")]
[SerializeField] private bool scaleDuringFlight = true;

// 1.0 = originalScale, 0.65 = shrunk at landing
[SerializeField, Range(0.1f, 1.5f)] private float flightStartScaleMult = 1.0f;
[SerializeField, Range(0.1f, 1.5f)] private float flightEndScaleMult = 0.65f;

// When shrink begins (0..1). Example: 0.75 means “only last 25%”
[SerializeField, Range(0f, 1f)] private float shrinkStartT = 0.75f;

// Curve remaps the shrink portion (0..1) -> (0..1)
// Default makes it ease in/out; you can edit in Inspector.
[SerializeField] private AnimationCurve shrinkCurve =
    new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f)
    );


    [Tooltip("Arc height as a fraction of the flight distance")]
    public float arcHeight = 0.3f;

    [Header("Flight Cameras/Layers")]
    [Tooltip("If set, this is the world camera used for WorldToScreenPoint. Strongly recommended.")]
    [SerializeField] private Camera worldCameraOverride;

    [Tooltip("Optional: assign a RectTransform under the ROOT canvas to use as a flight layer. If null, will try to find child named 'FlightLayer', else uses root canvas.")]
    [SerializeField] private RectTransform flightLayerOverride;

    [Header("Flight Scaling")]
public float scaleAtTakeoff = 1f;
public float scaleAtLanding = 0.6f;

[Header("Flight Scaling Control")]
[SerializeField]
private bool useInternalFlightScaling = true;
float startScaleMult = 1.00f;

[Header("Special Card Layout")]
[SerializeField] private Vector2 specialPortraitSize = new Vector2(111f, 111f);

// cached original portrait size so unit cards stay unchanged
private Vector2 originalPortraitSize;
private RectTransform portraitRect;


    private CanvasGroup canvasGroup;
    private Vector3 originalScale;
    private bool isHovered = false;
    private bool isFlying = false;
    private RectTransform rectTransform;

    // Track tweens so hover/click can't cancel flight
    private int flightTweenId = -1;
    private int hoverTweenId = -1;
    private int clickTweenId = -1;

    // Hover delay tracking
    private Coroutine hoverDelayCoroutine;
    private Vector3 scaleBeforeHover;
    private bool hoverEffectActive = false; // True when hover scale is applied

    public System.Action<CardVisual> OnCardClicked;

    // Public property for carousel to check if hover is active
    public bool IsHoverEffectActive => hoverEffectActive;
    public System.Action<CardVisual> OnCardHoverEnter;
    public System.Action<CardVisual> OnCardHoverExit;
private void Awake()
{
    canvasGroup = GetComponent<CanvasGroup>();
    if (canvasGroup == null)
        canvasGroup = gameObject.AddComponent<CanvasGroup>();

    rectTransform = GetComponent<RectTransform>();
    if (rectTransform == null)
    {
        Debug.LogError($"[CardVisual] Missing RectTransform on '{name}'. scenePath='{GetScenePath(transform)}'", this);
        return;
    }

    // Unit portrait is not guaranteed for special cards.
    if (unitPortrait != null)
    {
        portraitRect = unitPortrait.rectTransform;
        originalPortraitSize = portraitRect.sizeDelta;
    }
    else
    {
        portraitRect = null;
        originalPortraitSize = Vector2.zero;
        // Optional: uncomment if you want a warning during setup
        // Debug.LogWarning($"[CardVisual] unitPortrait not set on {name} (ok for special cards).", this);
    }

    originalScale = transform.localScale;

    if (hoverGlow != null) hoverGlow.enabled = false;
    if (selectionOutline != null) selectionOutline.enabled = false;
}

private static string GetScenePath(Transform t)
{
    if (t == null) return "null";
    string path = t.name;
    while (t.parent != null)
    {
        t = t.parent;
        path = t.name + "/" + path;
    }
    return path;
}
public void SetCard(UnitCard card)
{
    cardData = card;
    UpdateVisuals();
}


public void SetUseInternalFlightScaling(bool enabled)
{
    useInternalFlightScaling = enabled;
}

    public void UpdateVisuals()
{
    if (cardData == null) return;

    bool isSpecial = cardData.IsSpecialCard;

    // Commander-effective display stats (projection only)
    int displayCost = cardData.Cost;
    int displayAtk = cardData.BaseATK;
    int displayHp = cardData.BaseHP;

    if (CommanderBonus.Instance != null && cardData.Owner != null)
    {
        var eff = CommanderBonus.Instance.GetEffectiveStatsForCard(cardData);
        displayCost = eff.Cost;
        displayAtk = eff.Attack;
        displayHp = eff.HP;
    }

    // Apply Doctrine cost reductions (Motorhead for vehicles, Birdwatcher for aircraft)
    if (DoctrineSystem.GetTypeCostReduction != null && cardData.Owner != null)
    {
        int reduction = DoctrineSystem.GetTypeCostReduction(cardData.Owner, cardData.Type);
        displayCost = Mathf.Max(0, displayCost - reduction);
    }

    // Add Perk ATK bonuses (Stopping Power, Piercing, Explosives, Heads Up)
    if (PerkSystem.GetTypeAtkBonus != null && cardData.Owner != null)
    {
        displayAtk += PerkSystem.GetTypeAtkBonus(cardData.Owner, cardData.Type);
    }

    // Add Doctrine ATK bonuses (Close Combat, Ranged Warrior, Iron Skin, Death from Above)
    if (DoctrineSystem.GetTypeAtkBonus != null && cardData.Owner != null)
    {
        displayAtk += DoctrineSystem.GetTypeAtkBonus(cardData.Owner, cardData.Type);
    }

    // --- Always shown (both unit + special) ---
    if (costText != null) costText.text = displayCost.ToString();
    if (nameText != null) nameText.text = cardData.Name;
    //if (rankText != null) rankText.text = GetRankSymbol(displayCost);

    // --- Ability text behavior ---
    if (abilityText != null)
    {
        if (!string.IsNullOrWhiteSpace(cardData.AbilityDescription))
            abilityText.text = cardData.AbilityDescription;
        else
            abilityText.text = GetAbilityShortName(cardData.Ability);
    }

    // --- Hide/Show fields based on layout ---
    if (attackText != null) attackText.gameObject.SetActive(!isSpecial);
    if (healthText != null) healthText.gameObject.SetActive(!isSpecial);
    if (typeText != null) typeText.gameObject.SetActive(!isSpecial);

    if (!isSpecial)
    {
        // Unit card stats/type
        if (typeText != null) typeText.text = $"[{cardData.Type}]";
        if (attackText != null) attackText.text = displayAtk.ToString();
        if (healthText != null) healthText.text = displayHp.ToString();
    }

    // --- Portrait ---
    if (unitPortrait != null)
    {
        if (cardData.PortraitSprite != null)
        {
            unitPortrait.enabled = true;
            unitPortrait.sprite = cardData.PortraitSprite;
        }
        else
        {
            unitPortrait.sprite = null;
            unitPortrait.enabled = false;
        }
    }

    // Resize portrait for special cards (bigger art)
    if (portraitRect != null)
        portraitRect.sizeDelta = isSpecial ? specialPortraitSize : originalPortraitSize;

    // --- Background ---
    if (cardBackground != null)
    {
        if (cardData.BackgroundSprite != null)
        {
            cardBackground.sprite = cardData.BackgroundSprite;
            cardBackground.color = Color.white;
        }
        else
        {
            cardBackground.color = GetTypeColor(cardData.Type);
        }
    }
}



    private string GetAbilityShortName(UnitAbilityType ability)
    {
        return ability switch
        {
            UnitAbilityType.None => "None",
            UnitAbilityType.Taunt => "Taunt",
            UnitAbilityType.ArmorPiercing => "Armor Piercing",
            UnitAbilityType.ThreeOfAKind => "3-of-a-Kind",
            UnitAbilityType.Unmatched => "Unmatched",
            UnitAbilityType.Armored => "Armored",
            UnitAbilityType.Counterattack => "Counter",
            UnitAbilityType.DirectAttack => "Direct Attack",
            UnitAbilityType.TargetMark => "Mark Target",
            UnitAbilityType.MrMoneybags => "Mr. Moneybags",
            UnitAbilityType.Duplicate => "Duplicate",
            UnitAbilityType.DestroyStrongestVehicle => "Destroy Strongest Vehicle",
            _ => ability.ToString()
        };
    }

   /// private string GetRankSymbol(int cost)
   /// {
   ///     if (cost <= 1) return "★";
   ///     if (cost <= 3) return "★★";
   //////     if (cost <= 6) return "★★★";
   ///     return "★★★★";
  ///  }

    private Color GetTypeColor(UnitType type)
    {
        return type switch
        {
            UnitType.Infantry => new Color(0.9f, 0.85f, 0.8f),
            UnitType.Ranged => new Color(0.8f, 0.9f, 0.85f),
            UnitType.Vehicle => new Color(0.85f, 0.85f, 0.9f),
            UnitType.Air => new Color(0.9f, 0.9f, 1f),
            _ => Color.white
        };
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!canvasGroup.interactable) return;
        if (isFlying) return;

        isHovered = true;

        // Cancel any existing hover delay
        if (hoverDelayCoroutine != null)
        {
            StopCoroutine(hoverDelayCoroutine);
        }

        // Start delayed hover effect
        hoverDelayCoroutine = StartCoroutine(HoverDelayCoroutine());

        OnCardHoverEnter?.Invoke(this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isFlying) return;

        isHovered = false;

        // Cancel hover delay if still waiting (hover never activated)
        if (hoverDelayCoroutine != null)
        {
            StopCoroutine(hoverDelayCoroutine);
            hoverDelayCoroutine = null;
            // No need to scale back - hover effect never started
            return;
        }

        // Only scale back if hover effect was actually active
        if (hoverEffectActive)
        {
            if (hoverGlow != null) hoverGlow.enabled = false;

            CancelHoverTweens();

            // Scale back to the scale we had before hover
            // Keep hoverEffectActive=true during shrink to prevent carousel interference
            hoverTweenId = LeanTween.scale(gameObject, scaleBeforeHover, hoverAnimDuration)
                .setEaseInOutQuad()
                .setOnComplete(() => {
                    hoverEffectActive = false; // Now carousel can resume updates
                })
                .id;
        }

        OnCardHoverExit?.Invoke(this);
    }

    private System.Collections.IEnumerator HoverDelayCoroutine()
    {
        // Wait for delay
        yield return new WaitForSeconds(hoverDelay);

        // Store current scale and alpha before applying hover
        scaleBeforeHover = transform.localScale;

        // Boost opacity during hover
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1.0f; // Full opacity when hovered
        }

        if (hoverGlow != null) hoverGlow.enabled = true;

        // Mark hover effect as active (prevents carousel from overriding scale/alpha)
        hoverEffectActive = true;

        CancelHoverTweens();

        // Apply hover scale (relative to current scale, not original)
        Vector3 targetScale = scaleBeforeHover * hoverScale;
        hoverTweenId = LeanTween.scale(gameObject, targetScale, hoverAnimDuration)
            .setEaseOutBack()
            .id;

        hoverDelayCoroutine = null;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Debug: Log all click attempts to diagnose delay issues
        Debug.Log($"[CardVisual] OnPointerClick on '{cardData?.Name ?? "null"}' - interactable={canvasGroup?.interactable}, isFlying={isFlying}");

        if (!canvasGroup.interactable)
        {
            Debug.Log($"[CardVisual] Click BLOCKED - card not interactable");
            return;
        }
        if (isFlying)
        {
            Debug.Log($"[CardVisual] Click BLOCKED - card is flying");
            return;
        }

        OnCardClicked?.Invoke(this);

        // Cancel only click/hover tweens, NOT flight.
        CancelClickTweens();
        CancelHoverTweens();

        clickTweenId = LeanTween.scale(gameObject, originalScale * 0.95f, 0.05f)
            .setEaseOutQuad()
            .setOnComplete(() =>
            {
                float target = isHovered ? hoverScale : 1f;
                hoverTweenId = LeanTween.scale(gameObject, originalScale * target, 0.1f)
                    .setEaseOutBack()
                    .id;
            })
            .id;
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionOutline != null) selectionOutline.enabled = selected;
    }

    public void SetInteractable(bool interactable)
    {
        canvasGroup.interactable = interactable;
        canvasGroup.alpha = interactable ? 1f : 0.5f;
    }

    /// <summary>
    /// Checks if this card can be played on the current battlefield.
    /// Returns false if the card's UnitType is blocked (e.g., Air on GroundOnly).
    /// </summary>
    public bool IsPlayableOnBattlefield()
    {
        if (cardData == null) return true;
        return Battlefield.IsTypeAllowed(cardData.Type);
    }

    /// <summary>
    /// Updates the card visual to show it's blocked by the battlefield.
    /// Call this when displaying cards in hand on battlefields with restrictions.
    /// </summary>
    public void UpdateBattlefieldRestrictionVisual()
    {
        if (cardData == null) return;
        if (canvasGroup == null) return; // Guard against missing CanvasGroup

        bool isBlocked = !Battlefield.IsTypeAllowed(cardData.Type);

        if (isBlocked)
        {
            // Gray out the card and make it non-interactable
            canvasGroup.alpha = 0.4f;
            canvasGroup.interactable = false;

            // Optional: Add a visual indicator (could be a lock icon overlay)
            // For now, the grayed-out appearance indicates it's blocked
        }
        else
        {
            // Restore normal appearance
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
        }
    }

    /// <summary>
    /// Gets the tooltip text explaining why a card is blocked (if blocked).
    /// Returns null if card is playable.
    /// </summary>
    public string GetBlockedReason()
    {
        if (cardData == null) return null;

        if (!Battlefield.IsTypeAllowed(cardData.Type))
        {
            return $"{cardData.Type} units not available on this battlefield";
        }

        return null;
    }

    private void CancelHoverTweens()
    {
        if (hoverTweenId != -1)
        {
            LeanTween.cancel(hoverTweenId);
            hoverTweenId = -1;
        }
    }

    private void CancelClickTweens()
    {
        if (clickTweenId != -1)
        {
            LeanTween.cancel(clickTweenId);
            clickTweenId = -1;
        }
    }

    private void CancelFlightTween()
    {
        if (flightTweenId != -1)
        {
            LeanTween.cancel(flightTweenId);
            flightTweenId = -1;
        }
    }

    /// <summary>
    /// Original fallback animation (float up). (No fade/destroy for now.)
    /// </summary>
    public void PlayCardAnimation(System.Action onComplete = null)
    {
        // NOTE: keep simple and visible while debugging.
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        if (rootCanvas != null)
        {
            Vector3 worldPosBefore = transform.position;
            transform.SetParent(rootCanvas.transform, true);
            transform.position = worldPosBefore;
        }

        CancelHoverTweens();
        CancelClickTweens();
        CancelFlightTween();

        LeanTween.moveY(gameObject, transform.position.y + 200f, 0.4f).setEaseInBack();
        LeanTween.scale(gameObject, originalScale * 1.2f, 0.2f).setEaseOutQuad();

        onComplete?.Invoke();
    }

    private void DetachFromLayout()
    {
        // Ignore layout while flying
        var le = GetComponent<LayoutElement>();
        if (le == null) le = gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;

        // Disable fitters (layout jitter)
        var fitter = GetComponent<ContentSizeFitter>();
        if (fitter != null) fitter.enabled = false;
    }

    /// <summary>
    /// Fly card to a WORLD position by converting world -> screen (WorldToScreenPoint),
    /// then screen -> local anchored position in the root canvas flight layer.
    /// </summary>

public void SetFlightTuning(float duration, float arc)
{
    flightDuration = duration;
    arcHeight = arc;
}
public void SetFlightContext(Camera worldCam, RectTransform flightLayer)
{
    worldCameraOverride = worldCam;
    flightLayerOverride = flightLayer;
}
    public void FlyToWorldPosition(Vector3 worldTargetPosition, System.Action onComplete = null)
{
    Debug.Log("FlyToWorldPosition called");

    isFlying = true;
    canvasGroup.interactable = false;
    canvasGroup.blocksRaycasts = false;

    // Make sure it's visible
    canvasGroup.alpha = 1f;

    Canvas rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
    if (rootCanvas == null) { isFlying = false; onComplete?.Invoke(); return; }

    Camera uiCam = (rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : rootCanvas.worldCamera;
    Camera worldCam = worldCameraOverride != null ? worldCameraOverride : Camera.main;
    if (worldCam == null) { isFlying = false; onComplete?.Invoke(); return; }

    RectTransform flightLayer = flightLayerOverride;
    if (flightLayer == null)
    {
        Transform found = rootCanvas.transform.Find("FlightLayer");
        flightLayer = (found as RectTransform) != null ? (RectTransform)found : rootCanvas.GetComponent<RectTransform>();
    }

    // Stop layout from snapping us back into the carousel slot
    DetachFromLayout();

    // Cancel only our own tweens (DON'T nuke everything on this GO)
    CancelHoverTweens();
    CancelClickTweens();
    CancelFlightTween();

    Vector2 startScreen = RectTransformUtility.WorldToScreenPoint(uiCam, rectTransform.position);
    Vector3 targetScreen3 = worldCam.WorldToScreenPoint(worldTargetPosition);
    if (targetScreen3.z < 0f) { isFlying = false; onComplete?.Invoke(); return; }

    Vector2 targetScreen = new Vector2(targetScreen3.x, targetScreen3.y);

    RectTransformUtility.ScreenPointToLocalPointInRectangle(flightLayer, startScreen, uiCam, out Vector2 startLocal);
    RectTransformUtility.ScreenPointToLocalPointInRectangle(flightLayer, targetScreen, uiCam, out Vector2 targetLocal);

    // Reparent to non-layout layer but preserve current world position
    Vector3 worldBefore = rectTransform.position;
    rectTransform.SetParent(flightLayer, true);
    rectTransform.position = worldBefore;

    rectTransform.SetAsLastSibling();
    rectTransform.anchorMin = rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
    rectTransform.pivot = new Vector2(0.5f, 0.5f);

    // IMPORTANT: set only START. Do not teleport to target.
    rectTransform.anchoredPosition = startLocal;

    float dur = Mathf.Max(0.05f, flightDuration);

    // --- NEW: flight scaling ---
    // Start at your originalScale, shrink toward the unit as it flies.
    // Tweak these values to taste.

    Vector3 startScale = originalScale;
Vector3 endScale = originalScale;

if (useInternalFlightScaling)
{
    float startScaleMult = flightStartScaleMult;
    float endScaleMult   = flightEndScaleMult;
    startScale = originalScale * startScaleMult;
    endScale   = originalScale * endScaleMult;

    transform.localScale = startScale;
}


    // Quadratic bezier arc
    Vector2 mid = (startLocal + targetLocal) * 0.5f;
    float dist = Vector2.Distance(startLocal, targetLocal);
    mid.y += dist * Mathf.Clamp01(arcHeight);

    flightTweenId = LeanTween.value(gameObject, 0f, 1f, dur)
        .setEase(LeanTweenType.easeInOutQuad)
        .setOnUpdate((float t) =>
        {
            // position along arc
            float u = 1f - t;
            Vector2 p = (u * u * startLocal) + (2f * u * t * mid) + (t * t * targetLocal);
            rectTransform.anchoredPosition = p;

            // scale down during flight
if (scaleDuringFlight)
{
    // Hold scale until shrinkStartT, then shrink over remaining time
    float shrinkT = 0f;
    if (t <= shrinkStartT)
    {
        shrinkT = 0f;
    }
    else
    {
        // normalize t into 0..1 over the “shrink window”
        float window = Mathf.Max(0.0001f, 1f - shrinkStartT);
        shrinkT = Mathf.Clamp01((t - shrinkStartT) / window);
    }

    // apply curve for nicer feel
    float curved = shrinkCurve != null ? shrinkCurve.Evaluate(shrinkT) : shrinkT;

if (useInternalFlightScaling)
{
    transform.localScale = Vector3.LerpUnclamped(startScale, endScale, curved);
}
} 

        })
        .setOnComplete(() =>
        {
            // Slam: pop relative to landing scale, then settle back to landing scale
            Vector3 baseScale = endScale;

            LeanTween.scale(gameObject, baseScale * 1.15f, 0.08f)
                .setEaseOutQuad()
                .setOnComplete(() =>
                {
                    LeanTween.scale(gameObject, baseScale, 0.08f)
                        .setEaseInQuad()
                        .setOnComplete(() =>
                        {
                            flightTweenId = -1;
                            isFlying = false;
                            onComplete?.Invoke();
                        });
                });
        })
        .id;
}

}
