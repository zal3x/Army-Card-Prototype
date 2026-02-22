using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Screen shown after a battle ends.
/// Displays victory/defeat message and war funds earned.
/// </summary>
public class VictoryDefeatScreen : OverworldScreen
{
    [Header("UI Elements")]
    [Tooltip("Text displaying VICTORY or DEFEAT")]
    [SerializeField]
    private TMP_Text resultText;

    [Tooltip("Text showing war funds earned")]
    [SerializeField]
    private TMP_Text warFundsEarnedText;

    [Tooltip("Text showing HP remaining (victory only)")]
    [SerializeField]
    private TMP_Text hpRemainingText;

    [Tooltip("Text showing total war funds after this battle")]
    [SerializeField]
    private TMP_Text totalWarFundsText;

    [Tooltip("Button to continue to War Cabinet")]
    [SerializeField]
    private Button continueButton;

    [Header("Visual Feedback")]
    [Tooltip("Color for victory text")]
    [SerializeField]
    private Color victoryColor = new Color(0.2f, 0.8f, 0.2f);

    [Tooltip("Color for defeat text")]
    [SerializeField]
    private Color defeatColor = new Color(0.8f, 0.2f, 0.2f);

    [Tooltip("Optional particle effect or animation for victory")]
    [SerializeField]
    private GameObject victoryEffectPrefab;

    [Tooltip("Optional sound effect name for victory")]
    [SerializeField]
    private string victorySoundName = "Victory";

    [Tooltip("Optional sound effect name for defeat")]
    [SerializeField]
    private string defeatSoundName = "Defeat";

    // Cached result data
    private BattleResult cachedResult;
    private int cachedWarFundsEarned;
    private int cachedHPRemaining;
    private int cachedMaxHP;

    protected override void Awake()
    {
        base.Awake();

        // Wire up continue button
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    /// <summary>
    /// Set the result data to display.
    /// Call this before Show().
    /// </summary>
    public void SetResult(BattleResult result, int warFundsEarned, int hpRemaining, int maxHP)
    {
        cachedResult = result;
        cachedWarFundsEarned = warFundsEarned;
        cachedHPRemaining = hpRemaining;
        cachedMaxHP = maxHP;
    }

    protected override void OnShow()
    {
        UpdateUI();
        PlayResultEffects();
    }

    protected override void OnHide()
    {
        // Clean up any spawned effects
    }

    private void UpdateUI()
    {
        // Result text
        if (resultText != null)
        {
            bool isVictory = cachedResult == BattleResult.Victory;
            resultText.text = isVictory ? "VICTORY!" : "DEFEAT";
            resultText.color = isVictory ? victoryColor : defeatColor;
        }

        // War funds earned
        if (warFundsEarnedText != null)
        {
            warFundsEarnedText.text = $"+{cachedWarFundsEarned} War Funds";
        }

        // HP remaining (show only on victory or always)
        if (hpRemainingText != null)
        {
            if (cachedResult == BattleResult.Victory)
            {
                hpRemainingText.text = $"HP Remaining: {cachedHPRemaining}/{cachedMaxHP}";
                hpRemainingText.gameObject.SetActive(true);
            }
            else
            {
                hpRemainingText.gameObject.SetActive(false);
            }
        }

        // Total war funds
        if (totalWarFundsText != null && PlayerProgressData.Instance != null)
        {
            totalWarFundsText.text = $"Total War Funds: {PlayerProgressData.Instance.warFunds}";
        }
    }

    private void PlayResultEffects()
    {
        bool isVictory = cachedResult == BattleResult.Victory;

        // Spawn victory effect
        if (isVictory && victoryEffectPrefab != null)
        {
            Instantiate(victoryEffectPrefab, transform);
        }

        // Play sound (if audio system exists)
        // AudioManager.Instance?.PlaySound(isVictory ? victorySoundName : defeatSoundName);

        // Animate text scale
        if (resultText != null)
        {
            AnimateResultText();
        }
    }

    private void AnimateResultText()
    {
        // Use LeanTween for animation (matching project's existing pattern)
        if (resultText == null) return;

        var rectTransform = resultText.rectTransform;

        // Start small and pop in
        rectTransform.localScale = Vector3.zero;
        LeanTween.scale(rectTransform, Vector3.one * 1.2f, 0.3f)
            .setEaseOutBack()
            .setOnComplete(() =>
            {
                LeanTween.scale(rectTransform, Vector3.one, 0.15f)
                    .setEaseInOutQuad();
            });
    }

    private void OnContinueClicked()
    {
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ContinueToWarCabinet();
        }
        else
        {
            Debug.LogWarning("[VictoryDefeatScreen] GameStateManager.Instance is null!");
            Hide();
        }
    }

    private void Update()
    {
        // Allow space/enter to continue
        if (IsVisible)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            {
                OnContinueClicked();
            }
        }
    }
}
