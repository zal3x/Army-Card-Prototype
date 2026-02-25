using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages visual status effects (outline, glow, holographic shimmer) on a single
/// battlefield unit sprite. Attach automatically via UnitEffectController.InitializeOnUnit().
///
/// Effect layering:
///   - Outline  : child SpriteRenderer (sorting order -1) with SpriteOutline shader.
///   - Glow     : MaterialPropertyBlock on the main SpriteRenderer using RimPulse shader
///                properties (_RimColor, _RimIntensity, _EffectAmount).
///   - Holographic : child SpriteRenderer (sorting order +1) with HoloFoilSoft shader.
///
/// Multiple effects blend their colors via successive Color.Lerp calls.
/// The sprite of both child renderers is synced to the main renderer every LateUpdate
/// so animated units always display correctly.
/// </summary>
public class UnitEffectController : MonoBehaviour
{
    // -------------------------------------------------------
    // Private state
    // -------------------------------------------------------
    private SpriteRenderer _mainRenderer;
    private SpriteRenderer _outlineRenderer;    // child, sort order -1
    private SpriteRenderer _holoRenderer;       // child, sort order +1

    private Material _outlineMat;
    private Material _holoMat;
    private MaterialPropertyBlock _mainPropBlock;
    private MaterialPropertyBlock _outlinePropBlock;
    private MaterialPropertyBlock _holoPropBlock;

    private UnitEffectSettings _settings;

    // active effects: effectId → definition snapshot
    private readonly Dictionary<string, UnitEffectSettings.EffectDefinition> _activeEffects
        = new Dictionary<string, UnitEffectSettings.EffectDefinition>();

    // Shader property IDs (cached for performance)
    private static readonly int PropEffectAmount     = Shader.PropertyToID("_EffectAmount");
    private static readonly int PropOutlineColor     = Shader.PropertyToID("_OutlineColor");
    private static readonly int PropOutlineWidth     = Shader.PropertyToID("_OutlineWidthPixels");
    private static readonly int PropRimColor         = Shader.PropertyToID("_RimColor");
    private static readonly int PropRimIntensity     = Shader.PropertyToID("_RimIntensity");
    private static readonly int PropPulseSpeed       = Shader.PropertyToID("_PulseSpeed");
    private static readonly int PropPulseAmount      = Shader.PropertyToID("_PulseAmount");
    private static readonly int PropFoilAmount       = Shader.PropertyToID("_FoilAmount");

    // -------------------------------------------------------
    // Static factory: attach and configure on a spawned unit GO
    // -------------------------------------------------------

    /// <summary>
    /// Attaches UnitEffectController to a unit's world GameObject and configures it.
    /// Call this immediately after the unit is spawned.
    /// </summary>
    public static UnitEffectController InitializeOnUnit(GameObject unitGO)
    {
        if (unitGO == null) return null;

        var ctrl = unitGO.GetComponent<UnitEffectController>();
        if (ctrl != null) return ctrl; // already initialized

        ctrl = unitGO.AddComponent<UnitEffectController>();
        ctrl.Setup();
        return ctrl;
    }

    // -------------------------------------------------------
    // Setup
    // -------------------------------------------------------

    private void Setup()
    {
        _settings = Resources.Load<UnitEffectSettings>("UnitEffectSettings");
        if (_settings == null)
            Debug.LogWarning("[UnitEffectController] UnitEffectSettings asset not found in Resources/. Create it via Assets > Create > Army Card > Unit Effect Settings.");

        // Find the main SpriteRenderer on this GameObject or an immediate child
        _mainRenderer = GetComponent<SpriteRenderer>();
        if (_mainRenderer == null)
            _mainRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (_mainRenderer == null)
        {
            Debug.LogWarning($"[UnitEffectController] No SpriteRenderer found on {gameObject.name}. Effects disabled.");
            return;
        }

        _mainPropBlock = new MaterialPropertyBlock();

        // Load shaders
        var outlineShader = Shader.Find("Cards/SpriteOutline");
        var holoShader    = Shader.Find("Cards/HoloFoil_Soft");

        if (outlineShader == null)
            Debug.LogWarning("[UnitEffectController] Shader 'Cards/SpriteOutline' not found.");
        if (holoShader == null)
            Debug.LogWarning("[UnitEffectController] Shader 'Cards/HoloFoil_Soft' not found.");

        int baseSortOrder = _mainRenderer.sortingOrder;

        // ----- Outline child -----
        if (outlineShader != null)
        {
            var outlineGO = new GameObject("_EffectOutline");
            outlineGO.transform.SetParent(transform, false);
            outlineGO.transform.localPosition = Vector3.zero;
            outlineGO.transform.localScale    = Vector3.one;

            _outlineRenderer = outlineGO.AddComponent<SpriteRenderer>();
            _outlineRenderer.sprite           = _mainRenderer.sprite;
            _outlineRenderer.sortingLayerID   = _mainRenderer.sortingLayerID;
            _outlineRenderer.sortingOrder     = baseSortOrder - 1;

            _outlineMat = new Material(outlineShader);
            _outlineRenderer.material = _outlineMat;

            _outlinePropBlock = new MaterialPropertyBlock();
            _outlineRenderer.SetPropertyBlock(_outlinePropBlock);
            _outlineRenderer.enabled = false;
        }

        // ----- Holographic overlay child -----
        if (holoShader != null)
        {
            var holoGO = new GameObject("_EffectHolo");
            holoGO.transform.SetParent(transform, false);
            holoGO.transform.localPosition = Vector3.zero;
            holoGO.transform.localScale    = Vector3.one;

            _holoRenderer = holoGO.AddComponent<SpriteRenderer>();
            _holoRenderer.sprite         = _mainRenderer.sprite;
            _holoRenderer.sortingLayerID = _mainRenderer.sortingLayerID;
            _holoRenderer.sortingOrder   = baseSortOrder + 1;

            // Start with _FoilAmount = 0 on the material itself so the shader default
            // (0.55) never shows even if the renderer is accidentally enabled.
            _holoMat = new Material(holoShader);
            _holoMat.SetFloat("_FoilAmount", 0f);
            _holoRenderer.material = _holoMat;

            _holoPropBlock = new MaterialPropertyBlock();
            _holoRenderer.SetPropertyBlock(_holoPropBlock);
            _holoRenderer.enabled = false;
        }

        // Zero out all effect-controlling shader properties on EVERY SpriteRenderer
        // now that child renderers have been created and are included in the search.
        // This covers both the original prefab renderers AND the children we just made.
        var tempBlock = new MaterialPropertyBlock();
        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.GetPropertyBlock(tempBlock);
            tempBlock.SetFloat(PropEffectAmount, 0f);  // RimPulse glow gate
            tempBlock.SetFloat(PropFoilAmount,   0f);  // HoloFoil shimmer gate
            tempBlock.SetFloat(PropRimIntensity, 0f);  // rim glow strength
            tempBlock.SetFloat(PropPulseAmount,  0f);  // pulse strength
            sr.SetPropertyBlock(tempBlock);
        }
    }

    // -------------------------------------------------------
    // Public API
    // -------------------------------------------------------

    /// <summary>Activates an effect by ID (looked up in UnitEffectSettings).</summary>
    public void Apply(string effectId)
    {
        if (string.IsNullOrEmpty(effectId)) return;

        if (_settings == null)
        {
            Debug.LogWarning($"[UnitEffectController] Cannot apply effect '{effectId}' on {gameObject.name}: " +
                             "UnitEffectSettings asset not found. Create it via Assets > Create > Army Card > Unit Effect Settings " +
                             "and save it in a Resources/ folder.");
            return;
        }

        var def = _settings.Get(effectId);
        if (def == null)
        {
            Debug.LogWarning($"[UnitEffectController] Effect ID '{effectId}' not found in UnitEffectSettings on {gameObject.name}. " +
                             "Add an entry with this ID to the UnitEffectSettings asset.");
            return;
        }

        _activeEffects[effectId] = def;
        Recompute();
    }

    /// <summary>Removes a specific active effect by ID.</summary>
    public void Remove(string effectId)
    {
        if (_activeEffects.Remove(effectId))
            Recompute();
    }

    /// <summary>Removes all active effects.</summary>
    public void ClearAll()
    {
        if (_activeEffects.Count == 0) return;
        _activeEffects.Clear();
        Recompute();
    }

    // -------------------------------------------------------
    // Internal: recompute blended visual state
    // -------------------------------------------------------

    private void Recompute()
    {
        if (_activeEffects.Count == 0)
        {
            DisableAll();
            return;
        }

        // --- Blend color across all active effects ---
        Color blendedColor = Color.black;
        bool first = true;
        bool wantOutline     = false;
        bool wantGlow        = false;
        bool wantHolographic = false;
        float maxIntensity   = 0f;
        float maxOutlineWidth = 2f;
        bool wantPulse       = false;
        float maxPulseSpeed  = 3f;

        foreach (var def in _activeEffects.Values)
        {
            blendedColor = first ? def.effectColor : Color.Lerp(blendedColor, def.effectColor, 0.5f);
            first = false;

            if ((def.effectTypes & UnitEffectSettings.UnitEffectType.Outline) != 0)     wantOutline = true;
            if ((def.effectTypes & UnitEffectSettings.UnitEffectType.Glow) != 0)        wantGlow = true;
            if ((def.effectTypes & UnitEffectSettings.UnitEffectType.Holographic) != 0) wantHolographic = true;

            if (def.intensity > maxIntensity)           maxIntensity = def.intensity;
            if (def.outlineWidthPixels > maxOutlineWidth) maxOutlineWidth = def.outlineWidthPixels;
            if (def.pulsing)
            {
                wantPulse = true;
                if (def.pulseSpeed > maxPulseSpeed) maxPulseSpeed = def.pulseSpeed;
            }
        }

        // --- Apply outline ---
        if (_outlineRenderer != null)
        {
            _outlineRenderer.enabled = wantOutline;
            if (wantOutline)
            {
                _outlineRenderer.GetPropertyBlock(_outlinePropBlock);
                _outlinePropBlock.SetColor(PropOutlineColor, blendedColor);
                _outlinePropBlock.SetFloat(PropOutlineWidth, maxOutlineWidth);
                _outlinePropBlock.SetFloat(PropEffectAmount, maxIntensity);
                _outlineRenderer.SetPropertyBlock(_outlinePropBlock);
            }
        }

        // --- Apply glow (RimPulse) via MaterialPropertyBlock on main renderer ---
        if (_mainRenderer != null)
        {
            _mainRenderer.GetPropertyBlock(_mainPropBlock);
            if (wantGlow)
            {
                _mainPropBlock.SetColor(PropRimColor, blendedColor);
                _mainPropBlock.SetFloat(PropRimIntensity, Mathf.Lerp(1.5f, 4f, maxIntensity));
                _mainPropBlock.SetFloat(PropEffectAmount, maxIntensity);
                _mainPropBlock.SetFloat(PropPulseSpeed, wantPulse ? maxPulseSpeed : 0f);
                _mainPropBlock.SetFloat(PropPulseAmount, wantPulse ? 0.7f : 0f);
            }
            else
            {
                _mainPropBlock.SetFloat(PropEffectAmount, 0f);
            }
            _mainRenderer.SetPropertyBlock(_mainPropBlock);
        }

        // --- Apply holographic ---
        if (_holoRenderer != null)
        {
            _holoRenderer.enabled = wantHolographic;
            if (wantHolographic)
            {
                _holoRenderer.GetPropertyBlock(_holoPropBlock);
                _holoPropBlock.SetFloat(PropFoilAmount, Mathf.Lerp(0.3f, 0.8f, maxIntensity));
                _holoRenderer.SetPropertyBlock(_holoPropBlock);
            }
        }
    }

    private void DisableAll()
    {
        if (_outlineRenderer != null)
            _outlineRenderer.enabled = false;

        if (_holoRenderer != null)
            _holoRenderer.enabled = false;

        // Zero out glow on main renderer
        if (_mainRenderer != null)
        {
            _mainRenderer.GetPropertyBlock(_mainPropBlock);
            _mainPropBlock.SetFloat(PropEffectAmount, 0f);
            _mainRenderer.SetPropertyBlock(_mainPropBlock);
        }
    }

    // -------------------------------------------------------
    // Sprite sync for animated units
    // -------------------------------------------------------

    private void LateUpdate()
    {
        if (_mainRenderer == null) return;
        var currentSprite = _mainRenderer.sprite;

        if (_outlineRenderer != null && _outlineRenderer.sprite != currentSprite)
            _outlineRenderer.sprite = currentSprite;

        if (_holoRenderer != null && _holoRenderer.sprite != currentSprite)
            _holoRenderer.sprite = currentSprite;

        // Keep child sorting orders in sync with main renderer
        // (FieldSpawner may update sortingOrder after UnitEffectController is initialized)
        int baseSortOrder = _mainRenderer.sortingOrder;
        if (_outlineRenderer != null && _outlineRenderer.sortingOrder != baseSortOrder - 1)
            _outlineRenderer.sortingOrder = baseSortOrder - 1;
        if (_holoRenderer != null && _holoRenderer.sortingOrder != baseSortOrder + 1)
            _holoRenderer.sortingOrder = baseSortOrder + 1;
    }
}
