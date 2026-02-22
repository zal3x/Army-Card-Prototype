using UnityEngine;

[ExecuteAlways]
public class BattlefieldSlotVisualizer : MonoBehaviour
{
    [Header("Battlefield Dimensions (Pixels)")]
    public int battlefieldWidth = 1920;
    public int battlefieldHeight = 1080;
    public int landHeight = 700;
    public int armyDivideX = 960;

    [Header("Infantry")]
    public Vector2 infantrySize = new Vector2(256, 256);
    public Vector2 infantrySpacing = new Vector2(180, 180);
    public int infantryStackCount = 4;
    public Vector2 stackOffset = new Vector2(24, -24);

    [Header("Tanks")]
    public Vector2 tankSize = new Vector2(512, 512);
    public Vector2 tankSpacing = new Vector2(520, 520);

    [Header("Debug")]
    public bool showInfantry = true;
    public bool showTanks = true;
    public bool mirrorRightSide = true;

    void OnDrawGizmos()
    {
        if (showInfantry)
            DrawInfantrySlots(0, armyDivideX);

        if (mirrorRightSide && showInfantry)
            DrawInfantrySlots(armyDivideX, battlefieldWidth);

        if (showTanks)
            DrawTankSlots();
    }

    void DrawInfantrySlots(int startX, int endX)
    {
        Gizmos.color = new Color(0f, 1f, 0f, 0.4f);

        for (float x = startX + infantrySpacing.x / 2; x < endX; x += infantrySpacing.x)
        {
            for (float y = infantrySpacing.y / 2; y < landHeight; y += infantrySpacing.y)
            {
                Vector3 basePos = new Vector3(x, y, 0);

                // Base slot
                Gizmos.DrawWireCube(basePos, infantrySize);

                // Stack visualization
                for (int i = 1; i < infantryStackCount; i++)
                {
                    Vector3 offsetPos = basePos + new Vector3(
                        stackOffset.x * i,
                        stackOffset.y * i,
                        -i * 0.1f
                    );

                    Gizmos.color = new Color(0f, 0.7f, 0f, 0.25f);
                    Gizmos.DrawWireCube(offsetPos, infantrySize);
                }

                Gizmos.color = new Color(0f, 1f, 0f, 0.4f);
            }
        }
    }

    void DrawTankSlots()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.5f);

        for (float x = tankSpacing.x / 2; x < battlefieldWidth; x += tankSpacing.x)
        {
            for (float y = tankSpacing.y / 2; y < landHeight; y += tankSpacing.y)
            {
                // Prevent tanks from straddling the divide
                if (Mathf.Abs(x - armyDivideX) < tankSize.x / 2)
                    continue;

                Gizmos.DrawWireCube(
                    new Vector3(x, y, 0),
                    tankSize
                );
            }
        }
    }
}