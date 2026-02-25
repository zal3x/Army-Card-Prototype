using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Base class for all overworld UI screens.
/// Provides consistent show/hide pattern using root.SetActive().
/// </summary>
public abstract class OverworldScreen : MonoBehaviour
{
    [Header("Screen Root")]
    [Tooltip("The root GameObject that contains all UI for this screen. Will be toggled on/off.")]
    [SerializeField]
    protected GameObject root;

    /// <summary>
    /// Whether this screen is currently visible.
    /// </summary>
    public bool IsVisible => root != null && root.activeInHierarchy;

    /// <summary>
    /// Show this screen.
    /// </summary>
    public virtual void Show()
    {
        if (root == null)
        {
            Debug.LogWarning($"[{GetType().Name}] root is null, cannot show screen.");
            return;
        }

        // Ensure all parents in hierarchy are active
        EnsureParentsActive();

        root.SetActive(true);

        // WORKAROUND: If this screen component lives on the root GameObject,
        // the first SetActive(true) triggers Awake() which may re-disable root.
        // Awake only fires once, so a second call will stick.
        if (!root.activeSelf)
        {
            root.SetActive(true);
        }

        // Also activate all direct children (in case they were manually disabled)
        foreach (Transform child in root.transform)
        {
            if (!child.gameObject.activeSelf)
            {
                Debug.Log($"[{GetType().Name}] Activating child: {child.name}");
                child.gameObject.SetActive(true);
            }
        }

        // Verify the root is actually active in the full hierarchy
        if (!root.activeInHierarchy)
        {
            Debug.LogError($"[{GetType().Name}] root.activeInHierarchy is false! A parent is still inactive.");
            Transform t = root.transform;
            while (t.parent != null)
            {
                if (!t.parent.gameObject.activeSelf)
                {
                    Debug.LogWarning($"[{GetType().Name}] Force-activating parent: {t.parent.name}");
                    t.parent.gameObject.SetActive(true);
                }
                t = t.parent;
            }
        }

        // Force Unity to recalculate Canvas layout after activating previously-inactive UI
        Canvas.ForceUpdateCanvases();

        // Log the Canvas and RectTransform chain for diagnostics
        var canvas = root.GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            Debug.Log($"[{GetType().Name}] Canvas: {canvas.name}, renderMode={canvas.renderMode}, sortOrder={canvas.sortingOrder}");
        }
        LogRectTransformChain();

        OnShow();

        Debug.Log($"[{GetType().Name}] Screen shown. Root active: {root.activeSelf}, activeInHierarchy: {root.activeInHierarchy}");
    }

    /// <summary>
    /// Hide this screen.
    /// </summary>
    public virtual void Hide()
    {
        if (root == null)
        {
            Debug.LogWarning($"[{GetType().Name}] root is null, cannot hide screen.");
            return;
        }

        OnHide();
        root.SetActive(false);

        Debug.Log($"[{GetType().Name}] Screen hidden.");
    }

    /// <summary>
    /// Called after the screen is shown.
    /// Override to initialize/refresh UI content.
    /// </summary>
    protected abstract void OnShow();

    /// <summary>
    /// Called before the screen is hidden.
    /// Override to clean up or save state.
    /// </summary>
    protected abstract void OnHide();

    /// <summary>
    /// Logs the RectTransform details of root and every parent up to the Canvas.
    /// Helps identify which transform in the chain is introducing a position offset.
    /// </summary>
    private void LogRectTransformChain()
    {
        var rt = root.GetComponent<RectTransform>();
        if (rt == null) return;

        Transform t = rt.transform;
        int depth = 0;
        while (t != null)
        {
            var rect = t.GetComponent<RectTransform>();
            if (rect != null)
            {
                Debug.Log($"[{GetType().Name}] RT[{depth}] '{t.name}': anchoredPos={rect.anchoredPosition}, anchors=({rect.anchorMin})-({rect.anchorMax}), offsets=({rect.offsetMin})-({rect.offsetMax}), localPos={rect.localPosition}");
            }

            var canvas = t.GetComponent<Canvas>();
            if (canvas != null && canvas.isRootCanvas)
                break;

            t = t.parent;
            depth++;
        }
    }

    /// <summary>
    /// Ensures all parent GameObjects in the hierarchy are active.
    /// This prevents issues where a parent is inactive, blocking child activation.
    /// </summary>
    protected void EnsureParentsActive()
    {
        if (root == null) return;

        Transform t = root.transform;
        while (t.parent != null)
        {
            if (!t.parent.gameObject.activeSelf)
            {
                Debug.Log($"[{GetType().Name}] Activating inactive parent: {t.parent.name}");
                t.parent.gameObject.SetActive(true);
            }
            t = t.parent;
        }
    }

    /// <summary>
    /// Toggle screen visibility.
    /// </summary>
    public void Toggle()
    {
        if (IsVisible)
            Hide();
        else
            Show();
    }

    protected virtual void Awake()
    {
        // Note: We no longer deactivate root in Awake.
        // GameStateManager.HideAllScreens() handles initial hiding in Start().
        // The old root.SetActive(false) here caused a bug: if the screen component
        // lives on the root GameObject, calling root.SetActive(true) in Show()
        // would trigger Awake, which immediately undid the activation.
    }
}
