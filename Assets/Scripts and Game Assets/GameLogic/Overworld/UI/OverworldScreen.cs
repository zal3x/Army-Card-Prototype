using UnityEngine;

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
    public bool IsVisible => root != null && root.activeSelf;

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

        // Also activate all direct children (in case they were manually disabled)
        foreach (Transform child in root.transform)
        {
            if (!child.gameObject.activeSelf)
            {
                Debug.Log($"[{GetType().Name}] Activating child: {child.name}");
                child.gameObject.SetActive(true);
            }
        }

        // Check for Canvas and log its sort order
        var canvas = root.GetComponent<Canvas>();
        if (canvas == null)
            canvas = root.GetComponentInParent<Canvas>();

        if (canvas != null)
        {
            Debug.Log($"[{GetType().Name}] Canvas found: {canvas.name}, SortOrder: {canvas.sortingOrder}, RenderMode: {canvas.renderMode}");
        }
        else
        {
            Debug.LogWarning($"[{GetType().Name}] No Canvas found! Screen may not render properly.");
        }

        OnShow();

        Debug.Log($"[{GetType().Name}] Screen shown. Root active: {root.activeSelf}, Root position: {root.transform.position}");
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
        // Hide by default on awake
        if (root != null)
            root.SetActive(false);
    }
}
