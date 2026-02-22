using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Clickable marker on the world map representing an enemy commander.
/// </summary>
public class CommanderMapMarker : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Elements")]
    [Tooltip("Portrait image showing commander")]
    [SerializeField]
    private Image portraitImage;

    [Tooltip("Name text (shown on hover or always)")]
    [SerializeField]
    private TMP_Text nameText;

    [Tooltip("Difficulty indicator (stars, number, etc.)")]
    [SerializeField]
    private TMP_Text difficultyText;

    [Tooltip("Background or frame image")]
    [SerializeField]
    private Image frameImage;

    [Header("Animation")]
    [Tooltip("Scale when hovered")]
    [SerializeField]
    private float hoverScale = 1.15f;

    [Tooltip("Animation duration for hover")]
    [SerializeField]
    private float hoverDuration = 0.2f;

    [Tooltip("Idle bob animation amplitude")]
    [SerializeField]
    private float bobAmplitude = 5f;

    [Tooltip("Idle bob animation speed")]
    [SerializeField]
    private float bobSpeed = 2f;

    /// <summary>
    /// Event fired when this marker is clicked.
    /// </summary>
    public event Action<CommanderDefinition> OnClicked;

    private CommanderDefinition commander;
    private RectTransform rectTransform;
    private Vector2 basePosition;
    private int hoverTweenId = -1;
    private bool isHovered = false;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    /// <summary>
    /// Initialize the marker with a commander and position.
    /// </summary>
    public void Initialize(CommanderDefinition commanderDef, Vector2 position)
    {
        commander = commanderDef;

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = position;
            basePosition = position;
        }

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (commander == null) return;

        // Portrait
        if (portraitImage != null && commander.mapPortrait != null)
        {
            portraitImage.sprite = commander.mapPortrait;
        }

        // Name
        if (nameText != null)
        {
            nameText.text = commander.displayName;
        }

        // Difficulty
        if (difficultyText != null)
        {
            difficultyText.text = GetDifficultyString(commander.difficulty);
        }
    }

    private string GetDifficultyString(int difficulty)
    {
        // Show difficulty as stars
        return new string('\u2605', Mathf.Clamp(difficulty, 1, 5)); // Unicode star
    }

    private void Update()
    {
        // Idle bob animation when not hovered
        if (!isHovered && rectTransform != null)
        {
            float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
            rectTransform.anchoredPosition = basePosition + new Vector2(0, bobOffset);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        OnClicked?.Invoke(commander);

        // Click feedback animation
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, Vector3.one * 0.9f, 0.1f)
            .setEaseOutQuad()
            .setOnComplete(() =>
            {
                LeanTween.scale(gameObject, Vector3.one * hoverScale, 0.15f)
                    .setEaseOutBack();
            });
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;

        // Cancel any existing hover tween
        if (hoverTweenId != -1)
            LeanTween.cancel(hoverTweenId);

        // Scale up
        hoverTweenId = LeanTween.scale(gameObject, Vector3.one * hoverScale, hoverDuration)
            .setEaseOutQuad()
            .id;

        // Show name if hidden
        if (nameText != null)
            nameText.gameObject.SetActive(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;

        // Cancel any existing hover tween
        if (hoverTweenId != -1)
            LeanTween.cancel(hoverTweenId);

        // Scale back to normal
        hoverTweenId = LeanTween.scale(gameObject, Vector3.one, hoverDuration)
            .setEaseOutQuad()
            .id;

        // Hide name if it should be hidden by default
        // (you could add a toggle for always-visible names)
    }

    private void OnDestroy()
    {
        // Clean up tweens
        LeanTween.cancel(gameObject);
    }
}
