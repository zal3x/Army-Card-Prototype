using UnityEngine;
using System.Collections.Generic;
using System;


/// <summary>
/// CardFlightManager:
/// - Coordinates card flight (buy -> unit) and (unit death -> discard)
/// - Can reuse the actual CardVisual instance the player bought (best path)
/// - Falls back to spawning a prefab by card type if no live visual is available
/// </summary>
public class CardFlightManager : MonoBehaviour
{
    // ========================================
    // SINGLETON
    // ========================================
    private static CardFlightManager _instance;
    public static CardFlightManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<CardFlightManager>();
                if (_instance == null)
                    Debug.LogWarning("[CardFlightManager] No instance found in scene!");
            }
            return _instance;
        }
    }

    // ========================================
    // INSPECTOR REFERENCES
    // ========================================

    [Header("Discard Pile Position")]
    [Tooltip("World position where cards fly to when unit dies.")]
    public Transform discardPilePosition;

    [Tooltip("Fallback position if no transform assigned.")]
    public Vector3 fallbackDiscardPosition = new Vector3(-10f, -5f, 0f);

    [Header("Card Prefabs (fallback on death if no live visual)")]
    public GameObject infantryCardPrefab;
    public GameObject vehicleCardPrefab;
    public GameObject airCardPrefab;
    public GameObject rangedCardPrefab;

    [Header("Flight Context (IMPORTANT)")]
    [Tooltip("RectTransform under the root canvas where flying cards are parented.")]
    public RectTransform flightLayer;

    [Tooltip("Camera that renders the battlefield (for world-to-screen conversion).")]
    public Camera worldCamera;

    [Tooltip("Special card prefab (with CardVisual component)")]
public GameObject specialCardPrefab;

[Header("Special Card Flight Settings")]
[SerializeField] private Transform specialImpactPoint;     // assign in scene (center of battlefield/screen)
[SerializeField] private float flightToSpecialDuration = 0.45f;
[SerializeField] private float specialArcHeight = 0.10f;

// If you want special cards to use a different slam behavior than units:
[SerializeField] private bool specialDeactivateAfterSlam = true;



    // ========================================
    // TUNING
    // ========================================

    [Header("Flight To Unit Settings")]
    public float flightToUnitDuration = 0.4f;
    [Tooltip("Arc height as fraction of distance (0..1).")]
    public float flightArcHeight = 0.4f;

    [Header("Slam Settings")]
    public float slamPopDuration = 0.15f;
    public float slamPopScale = 1.3f;

    [Header("Hide After Slam")]
    public bool deactivateAfterSlam = true; // if false, just alpha=0

    [Header("Reveal Timing")]
    public float spawnRevealDelay = 0.5f;

    [Header("Discard Flight Settings")]
    public float flightToDiscardDuration = 0.6f;
    public float discardArcHeight = 0.25f;
    public float discardSpinRotations = 1.0f;

    [Header("Flight Scale (late shrink)")]
    public bool scaleDuringFlight = true;
    [Range(0.1f, 1.5f)] public float flightStartScaleMult = 1.0f;
    [Range(0.1f, 1.5f)] public float flightEndScaleMult = 0.65f;
    [Range(0f, 1f)] public float shrinkStartT = 0.75f;
    public LeanTweenType shrinkEase = LeanTweenType.easeInQuad;

    // ========================================
    // TRACKING
    // ========================================

    private class TrackedCard
    {
        public int unitId;
        public UnitCard cardData;
        public Vector3 lastKnownUnitPosition;
        public PlayerSide ownerSide;

        // If we have the actual bought card visual, we can reuse it on death.
        public CardVisual liveVisual;

        // Optional: if you want to store definition later for hidden spawn flows
        public UnitCardDefinition cardDef;
    }

    private readonly Dictionary<int, TrackedCard> trackedCards = new Dictionary<int, TrackedCard>();
    private FieldSpawner fieldSpawner;

    // ========================================
    // UNITY
    // ========================================

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else if (_instance != this)
        {
            Destroy(gameObject);
            return;
        }

        fieldSpawner = FindObjectOfType<FieldSpawner>();

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    // ========================================
    // REGISTRATION API
    // ========================================

    /// <summary>
    /// Old API: just track data (death will spawn a NEW card prefab).
    /// </summary>
    public void RegisterCardForUnit(int unitId, UnitCard cardData, Vector3 unitWorldPosition, PlayerSide side)
    {
        RegisterCardForUnit(unitId, cardData, null, unitWorldPosition, side, null);
    }

    /// <summary>
    /// Internal: store live visual + optional card definition too.
    /// </summary>
    private void RegisterCardForUnit(int unitId, UnitCard cardData, UnitCardDefinition cardDef, Vector3 unitWorldPosition, PlayerSide side, CardVisual liveVisual)
    {
        trackedCards[unitId] = new TrackedCard
        {
            unitId = unitId,
            cardData = cardData,
            cardDef = cardDef,
            lastKnownUnitPosition = unitWorldPosition,
            ownerSide = side,
            liveVisual = liveVisual
        };

        Debug.Log($"[CardFlightManager] Registered '{cardData?.Name}' for UnitId={unitId} at {unitWorldPosition} (liveVisual={(liveVisual != null)})");
    }

    /// <summary>
    /// Best entry point: call this when player buys a card and you still have the CardVisual instance.
    /// This flies the actual card to the unit, slams, then hides it until death.
    /// </summary>
    public void RegisterAndFlyToUnit(int unitId, CardVisual cardVisual, UnitCardDefinition cardDef, Vector3 unitWorldPosition, PlayerSide side)
    {
        if (cardVisual == null || cardVisual.cardData == null)
        {
            Debug.LogWarning("[CardFlightManager] RegisterAndFlyToUnit called with null cardVisual or cardData");
            return;
        }

        // Ensure the CardVisual uses the correct manager context (fixes "flies down behind carousel")
        cardVisual.SetFlightContext(worldCamera, flightLayer);
        cardVisual.SetUseInternalFlightScaling(false);


        // Track (store live visual so we can reuse it on death)
        RegisterCardForUnit(unitId, cardVisual.cardData, cardDef, unitWorldPosition, side, cardVisual);

        // Apply manager tuning
        cardVisual.SetFlightTuning(flightToUnitDuration, flightArcHeight);

        // Late shrink (only near end)
        StartLateShrink(cardVisual);

        // Fly -> slam -> hide -> spawn smoke -> reveal unit
        cardVisual.FlyToWorldPosition(unitWorldPosition, () =>
        {
            SlamThenHide(cardVisual);

            // Spawn smoke VFX at unit position
            if (CombatVFXSpawner.Instance != null)
            {
                CombatVFXSpawner.Instance.SpawnSpawnSmoke(unitWorldPosition);
            }

            // Reveal unit after a short delay (so slam animation plays first)
            LeanTween.delayedCall(gameObject, spawnRevealDelay, () =>
            {
                if (fieldSpawner != null)
                    fieldSpawner.SetUnitVisibility(unitId, true);
            });
        });
    }
    /// <summary>
/// Special card path:
/// - flies to specialImpactPoint (center)
/// - plays slam (you can swap animation later)
/// - DOES NOT spawn unit smoke
/// - DOES NOT reveal any unit
/// - DOES NOT register trackedCards (no unitId)
/// </summary>
public void FlySpecialToCenter(CardVisual cardVisual, Action onComplete = null)
{
    if (cardVisual == null || cardVisual.cardData == null)
    {
        Debug.LogWarning("[CardFlightManager] FlySpecialToCenter called with null cardVisual or cardData");
        onComplete?.Invoke();
        return;
    }

    if (specialImpactPoint == null)
    {
        Debug.LogWarning("[CardFlightManager] specialImpactPoint not assigned. Destroying card visual.");
        Destroy(cardVisual.gameObject);
        onComplete?.Invoke();
        return;
    }

    // Ensure correct flight context (same fix you do for unit cards)
    cardVisual.SetFlightContext(worldCamera, flightLayer);
    cardVisual.SetUseInternalFlightScaling(false);

    // Apply special tuning (separate from unit tuning)
    cardVisual.SetFlightTuning(flightToSpecialDuration, specialArcHeight);

    // Optional: reuse your “late shrink” behavior (safe + consistent feel)
    StartLateShrink(cardVisual);

    Vector3 target = specialImpactPoint.position;

    cardVisual.FlyToWorldPosition(target, () =>
    {
        // Replace this later with your new “special slam” animation.
        SlamSpecialThenFinish(cardVisual, onComplete);
    });
}

private void SlamSpecialThenFinish(CardVisual cardVisual, Action onComplete)
{
    if (cardVisual == null) { onComplete?.Invoke(); return; }

    // For now you can reuse the same slam as unit cards,
    // but keep this method separate so you can swap animation later safely.
    // (Unit path uses SlamThenHide + smoke + reveal):contentReference[oaicite:4]{index=4}.
    SlamThenHide(cardVisual);

     // Invoke after slam finishes (slamPopDuration exists in CardFlightManager)
    LeanTween.delayedCall(gameObject, slamPopDuration + 0.05f, () =>
    {
        if (cardVisual != null)
            Destroy(cardVisual.gameObject);

        onComplete?.Invoke();
    });
}

    // ========================================
    // UNIT DEATH
    // ========================================

    public void OnUnitDestroyed(int unitId)
    {
        if (!trackedCards.TryGetValue(unitId, out TrackedCard tracked))
        {
            Debug.Log($"[CardFlightManager] No tracked card for unit {unitId} (might be enemy unit)");
            return;
        }

        // Determine start position
        Vector3 startWorldPos = tracked.lastKnownUnitPosition;
        if (fieldSpawner != null)
        {
            Vector3 currentPos = fieldSpawner.GetUnitPosition(unitId);
            if (currentPos != Vector3.zero)
                startWorldPos = currentPos;
        }

       
        Vector3 discardWorldPos = GetDiscardWorldPos();

        // Preferred: reuse the original purchased visual
        if (tracked.liveVisual != null)
        {
            CardVisual cv = tracked.liveVisual;

            // Re-enable / make visible
            cv.gameObject.SetActive(true);

            CanvasGroup cg = cv.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.alpha = 1f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
            }

            // Ensure correct flight context for discard too
            cv.SetFlightContext(worldCamera, flightLayer);

            // Apply discard tuning
            cv.SetFlightTuning(flightToDiscardDuration, discardArcHeight);

            // Optional spin during discard
            if (discardSpinRotations != 0f)
            {
                LeanTween.rotateZ(cv.gameObject, 360f * discardSpinRotations, flightToDiscardDuration)
                    .setEase(LeanTweenType.linear);
            }

            // Fly to discard then register with DiscardPileUI and destroy
            UnitCard cardDataCapture = tracked.cardData;
            cv.FlyToWorldPosition(discardWorldPos, () =>
            {
                DiscardPileUI.Instance?.AddCard(cardDataCapture);
                Destroy(cv.gameObject);
            });
        }
        else
        {
            // Fallback: spawn a type-specific card prefab at unit position and fly to discard
            SpawnAndFlyToDiscard(tracked.cardData, startWorldPos, discardWorldPos);
        }

        trackedCards.Remove(unitId);
        Debug.Log($"[CardFlightManager] Unit {unitId} destroyed - card '{tracked.cardData?.Name}' flying to discard");
    }

    // ========================================
    // HELPERS
    // ========================================

    public void UpdateUnitPosition(int unitId, Vector3 worldPos)
    {
        if (trackedCards.TryGetValue(unitId, out TrackedCard tracked))
            tracked.lastKnownUnitPosition = worldPos;
    }

    public void ClearAll()
    {
        trackedCards.Clear();
        Debug.Log("[CardFlightManager] Cleared all tracked cards");
    }

    private Vector3 GetDiscardWorldPos()
    {
        if (discardPilePosition != null)
            return discardPilePosition.position;

        return fallbackDiscardPosition;
    }

    private void SpawnAndFlyToDiscard(UnitCard cardData, Vector3 worldStartPosition, Vector3 discardWorldPos)
    {
        GameObject prefab = GetCardPrefabForType(cardData);
        if (prefab == null)
        {
            Debug.LogWarning("[CardFlightManager] No card prefab assigned for this type. Cannot animate discard.");
            return;
        }

        // Instantiate in UI flight layer space (as a UI card)
        // If your card prefabs are UI prefabs with CardVisual, instantiate them under flightLayer.
        Transform parent = (flightLayer != null) ? flightLayer : null;
        GameObject go = Instantiate(prefab, parent);

        CardVisual cv = go.GetComponent<CardVisual>();
        if (cv == null)
        {
            Debug.LogWarning("[CardFlightManager] Spawned card prefab has no CardVisual.");
            Destroy(go);
            return;
        }

        // Initialize visual
        cv.SetCard(cardData);

        // Ensure correct flight context
        cv.SetFlightContext(worldCamera, flightLayer);

        // Place it at the start world position (converted internally by FlyToWorldPosition)
        cv.SetFlightTuning(flightToDiscardDuration, discardArcHeight);

        // Optional spin
        if (discardSpinRotations != 0f)
        {
            LeanTween.rotateZ(go, 360f * discardSpinRotations, flightToDiscardDuration)
                .setEase(LeanTweenType.linear);
        }

        // Fly from world position to discard
        // NOTE: CardVisual converts from its current rectTransform.position, so we need to move it to start first.
        // Simplest: just snap its transform in world to match start, then let CardVisual convert.
        // If your CardVisual requires UI position, this still works because FlyToWorldPosition reads rectTransform.position.
        cv.transform.position = worldStartPosition;

        UnitCard fallbackCardData = cardData;
        cv.FlyToWorldPosition(discardWorldPos, () =>
        {
            DiscardPileUI.Instance?.AddCard(fallbackCardData);
            Destroy(go);
        });
    }

    private GameObject GetCardPrefabForType(UnitCard cardData)
{
    if (cardData == null) return infantryCardPrefab;

    // Special override
    if (cardData.IsSpecialCard && specialCardPrefab != null)
        return specialCardPrefab;

    switch (cardData.Type)
    {
        case UnitType.Infantry: return infantryCardPrefab;
        case UnitType.Vehicle:  return vehicleCardPrefab;
        case UnitType.Air:      return airCardPrefab;
        case UnitType.Ranged:   return rangedCardPrefab;
        default:                return infantryCardPrefab;
    }
}


    private void SlamThenHide(CardVisual cardVisual)
    {
        if (cardVisual == null) return;

        // Slam = shrink down quickly to simulate impact, then hide
        Vector3 currentScale = cardVisual.transform.localScale;
        Vector3 slamScale = currentScale * 0.5f; // Shrink to half size on impact

        // Quick shrink + fade out simultaneously (feels like impact absorption)
        LeanTween.scale(cardVisual.gameObject, slamScale, slamPopDuration)
            .setEaseInQuad(); // Fast at start, slows at end (impact feel)

        // Fade out at the same time
        CanvasGroup cg = cardVisual.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            LeanTween.alphaCanvas(cg, 0f, slamPopDuration)
                .setEaseInQuad()
                .setOnComplete(() =>
                {
                    cg.interactable = false;
                    cg.blocksRaycasts = false;

                    if (deactivateAfterSlam)
                        cardVisual.gameObject.SetActive(false);
                });
        }
        else
        {
            // Fallback if no CanvasGroup - just hide after scale completes
            LeanTween.delayedCall(gameObject, slamPopDuration, () =>
            {
                if (deactivateAfterSlam)
                    cardVisual.gameObject.SetActive(false);
            });
        }
    }

    private void StartLateShrink(CardVisual cv)
    {
        if (!scaleDuringFlight || cv == null) return;

        float dur = Mathf.Max(0.05f, cv.FlightDuration);
        float delay = dur * Mathf.Clamp01(shrinkStartT);
        float shrinkDur = Mathf.Max(0.01f, dur - delay);

        Vector3 startScale = cv.OriginalScale * flightStartScaleMult;
        Vector3 endScale = cv.OriginalScale * flightEndScaleMult;

        cv.transform.localScale = startScale;

        LeanTween.scale(cv.gameObject, endScale, shrinkDur)
            .setDelay(delay)
            .setEase(shrinkEase);
    }

}