using UnityEngine;

public class UnitClickRelay : MonoBehaviour
{
    private Vector3 mouseDownPos;
    private const float clickThreshold = 8f; // pixels

    private UnitWorldHandle handle;

    private void Awake()
    {
        handle = GetComponent<UnitWorldHandle>();
    }

    private void OnMouseDown()
    {
        mouseDownPos = Input.mousePosition;
    }

    private void OnMouseUp()
    {
        // If the player dragged, don't count it as a click
        float dist = Vector3.Distance(mouseDownPos, Input.mousePosition);
        if (dist > clickThreshold) return;

        if (handle == null) return;

        // Notify BattleTestRunner (we'll add this next)
        BattleTestRunner.Instance?.NotifyUnitClicked(handle);
    }
}
