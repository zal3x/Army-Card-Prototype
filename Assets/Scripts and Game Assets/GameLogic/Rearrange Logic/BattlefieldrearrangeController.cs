using UnityEngine;

public class BattlefieldRearrangeController : MonoBehaviour
{
    public FieldSpawner fieldSpawner;
    public BattleTestRunner battle;   // simplest for now
    public Camera mainCamera;
    public LayerMask unitLayer;

    private UnitWorldHandle dragging;
    private Vector3 dragOffset;
    private int startSlotIndex;
    private int hoverSlotIndex;

    private void Awake()
    {
        if (mainCamera == null) mainCamera = Camera.main;
    }

    private void Update()
    {
        if (battle == null || !battle.IsManualRearrangeActive) return;

        if (Input.GetMouseButtonDown(0)) TryStartDrag();
        if (dragging != null) DragUpdate();
        if (Input.GetMouseButtonUp(0) && dragging != null) EndDrag();
    }

    private void TryStartDrag()
    {
        var world = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        var hit = Physics2D.Raycast(world, Vector2.zero, 0f, unitLayer);
        if (!hit.collider) return;

        var handle = hit.collider.GetComponentInParent<UnitWorldHandle>();
        if (handle == null) return;

        // Only allow dragging Player A units
        if (handle.Side != PlayerSide.A) return;

        dragging = handle;

        // slot info
        if (!fieldSpawner.TryGetSlotInfo(dragging.UnitId, out var laneType, out var side, out startSlotIndex))
        {
            dragging = null;
            return;
        }

        hoverSlotIndex = startSlotIndex;

        Vector3 unitPos = dragging.transform.position;
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = unitPos.z;
        dragOffset = unitPos - mousePos;
    }

    private void DragUpdate()
    {
        Vector3 mousePos = mainCamera.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = dragging.transform.position.z;

        dragging.transform.position = mousePos + dragOffset;

        // find closest slot in this lane
        fieldSpawner.TryGetSlotInfo(dragging.UnitId, out var laneType, out var side, out _);
        var slots = fieldSpawner.GetLaneSlots(laneType, side);

        hoverSlotIndex = FindClosestSlotIndex(slots, dragging.transform.position, maxIndex: battle.MaxUnitsPerRow - 1);
        // OPTIONAL: show highlight on hoverSlotIndex
    }

    private int FindClosestSlotIndex(Transform[] slots, Vector3 pos, int maxIndex)
    {
        int best = 0;
        float bestDist = float.MaxValue;

        int limit = Mathf.Min(slots.Length, maxIndex + 1);
        for (int i = 0; i < limit; i++)
        {
            float d = Vector3.SqrMagnitude(slots[i].position - pos);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private void EndDrag()
    {
        int unitId = dragging.UnitId;

        // ask battle runner to update simulation + visuals
        battle.RequestReorderUnitWithinLane(unitId, hoverSlotIndex);

        dragging = null;
    }
}
