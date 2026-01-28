using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RearrangementUI_SwapRows : MonoBehaviour
{
    [Header("Root")]
    public GameObject rootPanel;

    [Header("Buttons")]
    public Button swapFrontMiddleButton;
    public Button swapMiddleBackButton;
    public Button swapFrontBackButton;
    public Button doneButton;

    [Header("Optional Text")]
    public TMP_Text statusText;

    private Action<RowType, RowType> onSwapRows;
    private Action onDone;

    public void Begin(Action<RowType, RowType> swapRows, Action done)
    {
        gameObject.SetActive(true);   // show UI
        onSwapRows = swapRows;
        onDone = done;

        if (rootPanel != null) rootPanel.SetActive(true);
        SetStatus("Drag units to reorder within a row. Use buttons to swap rows.");

        // Wire buttons
        if (swapFrontMiddleButton != null)
        {
            swapFrontMiddleButton.onClick.RemoveAllListeners();
            swapFrontMiddleButton.onClick.AddListener(() => RequestSwap(RowType.Front, RowType.Middle));
        }

        if (swapMiddleBackButton != null)
        {
            swapMiddleBackButton.onClick.RemoveAllListeners();
            swapMiddleBackButton.onClick.AddListener(() => RequestSwap(RowType.Middle, RowType.Back));
        }

        if (swapFrontBackButton != null)
        {
            swapFrontBackButton.onClick.RemoveAllListeners();
            swapFrontBackButton.onClick.AddListener(() => RequestSwap(RowType.Front, RowType.Back));
        }

        if (doneButton != null)
        {
            doneButton.onClick.RemoveAllListeners();
            doneButton.onClick.AddListener(() => onDone?.Invoke());
        }
    }

    public void End()
    {
        onSwapRows = null;
        onDone = null;

        if (rootPanel != null) rootPanel.SetActive(false);
            gameObject.SetActive(false);  // hide UI

    }

    private void RequestSwap(RowType a, RowType b)
    {
        onSwapRows?.Invoke(a, b);
        SetStatus($"Swapped {a} ↔ {b}");
    }

    public void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
    }
}
