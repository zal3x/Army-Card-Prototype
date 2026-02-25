using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// ATK overlays that can either:
/// 1) Spawn pooled overlay objects under a world-space Canvas, positioned at an anchor (FieldSpawner pos or UnitVisualTop)
/// 2) OR update an existing TMP text attached to the unit prefab under OverlayAnchor (recommended for different-sized units)
/// 
/// Uses pooling (no Destroy during battle) to avoid Unity inspector null serialization spam.
/// </summary>
public class AttackPowerFieldSpawnerOverlays : MonoBehaviour
{
    public enum DisplayMode
    {
        /// <summary>
        /// Create overlay objects (TMPUGUI) under a world-space Canvas and position them in world space.
        /// </summary>
        PooledCanvasOverlay,

        /// <summary>
        /// Do NOT create overlay objects. Instead, find a TMP text under the unit's OverlayAnchor and update it.
        /// This is best when units have different sizes and you want per-unit placement/styling in prefabs.
        /// </summary>
        UsePrefabAnchorText
    }

    public enum AnchorMode
    {
        FieldSpawnerPosition,
        UnitVisualTop
    }

    [Header("Scene References (optional auto-find)")]
    public FieldSpawner fieldSpawner;
    public BattleTestRunner battleRunner;

    [Header("Mode")]
    public DisplayMode displayMode = DisplayMode.UsePrefabAnchorText;

    [Header("Canvas (used only for PooledCanvasOverlay)")]
    public Canvas worldSpaceCanvas;

    [Header("Overlay Prefab (used only for PooledCanvasOverlay)")]
    [Tooltip("Prefab that contains a TextMeshProUGUI in children. If null, a simple default overlay is created.")]
    public GameObject overlayPrefab;

    [Header("Anchor")]
    public AnchorMode anchorMode = AnchorMode.UnitVisualTop;

    [Tooltip("Child name on the unit visual for explicit anchor (optional).")]
    public string overlayAnchorName = "OverlayAnchor";

    [Tooltip("If UsePrefabAnchorText, we will try to find this path under OverlayAnchor (optional, but recommended): OverlayAnchor/ATKRoot/ATKText")]
    public string atkTextChildName = "ATKText";

    [Tooltip("Extra vertical lift when anchoring to UnitVisualTop bounds.")]
    public float unitVisualTopExtraHeight = 0.15f;

    [Header("Per-Player Fine-Tune Offsets (applied only for PooledCanvasOverlay positioning)")]
    public Vector3 playerAOffset = new Vector3(0f, 0f, 0f);
    public Vector3 playerBOffset = new Vector3(0f, 0f, 0f);

    [Header("Overlay Behavior")]
    public bool billboardToCamera = true;
    public float overlayWorldScale = 0.01f;
    public bool hideIfMissingRefs = true;

    private Camera mainCam;

    private class OverlayEntry
    {
        public int unitId;

        // If displayMode == UsePrefabAnchorText:
        public TMP_Text prefabTmp;

        // If displayMode == PooledCanvasOverlay:
        public GameObject overlayGo;
        public TextMeshProUGUI overlayTmpUgui;

        public int lastAtk;
    }

    private readonly Dictionary<int, OverlayEntry> overlaysByUnitId = new Dictionary<int, OverlayEntry>();
    private readonly Stack<OverlayEntry> overlayPool = new Stack<OverlayEntry>();

    private struct UnitContext
    {
        public Unit unit;
        public Player owner;
        public Player enemy;
        public RowType row;
    }

    private void Awake()
    {
        mainCam = Camera.main;
    }

    private void Start()
    {
        AutoWireReferences();
        if (displayMode == DisplayMode.PooledCanvasOverlay)
            EnsureCanvasExists();
    }

    private void AutoWireReferences()
    {
        if (fieldSpawner == null)
            fieldSpawner = FindObjectOfType<FieldSpawner>();

        if (battleRunner == null)
        {
            if (GameStateManager.Instance != null && GameStateManager.Instance.battleRunner != null)
                battleRunner = GameStateManager.Instance.battleRunner;
            else
                battleRunner = FindObjectOfType<BattleTestRunner>();
        }
    }

    private void EnsureCanvasExists()
    {
        if (worldSpaceCanvas != null)
            return;

        // Prefer a canvas named ATKOverlayCanvas if present
        Canvas[] canvases = FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == "ATKOverlayCanvas")
            {
                worldSpaceCanvas = canvases[i];
                break;
            }
        }

        // Otherwise use any world-space canvas
        if (worldSpaceCanvas == null)
        {
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].renderMode == RenderMode.WorldSpace)
                {
                    worldSpaceCanvas = canvases[i];
                    break;
                }
            }
        }

        // Final fallback: minimal world-space canvas (Canvas only)
        if (worldSpaceCanvas == null)
        {
            GameObject go = new GameObject("ATKOverlayCanvas (Runtime)");
            worldSpaceCanvas = go.AddComponent<Canvas>();
            worldSpaceCanvas.renderMode = RenderMode.WorldSpace;
            go.transform.position = Vector3.zero;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }
    }

    private void Update()
    {
        if (mainCam == null) mainCam = Camera.main;

        bool missingRefs = (fieldSpawner == null || battleRunner == null);
        if (!missingRefs && displayMode == DisplayMode.PooledCanvasOverlay && worldSpaceCanvas == null)
            missingRefs = true;

        if (missingRefs)
        {
            if (hideIfMissingRefs)
                SetAllActiveEntriesActive(false);
            return;
        }
        if (hideIfMissingRefs)
            SetAllActiveEntriesActive(true);

        // Gather alive units
        List<UnitContext> liveUnits = GatherAliveUnitsOnField(battleRunner.PlayerA, battleRunner.PlayerB);

        // Ensure entries exist for live units
        for (int i = 0; i < liveUnits.Count; i++)
            EnsureEntryForUnit(liveUnits[i].unit.InstanceId);

        // Pool/deactivate entries for dead/missing units
        PoolMissingEntries(liveUnits);

        // Update entries
        for (int i = 0; i < liveUnits.Count; i++)
        {
            UnitContext info = liveUnits[i];

            if (!overlaysByUnitId.TryGetValue(info.unit.InstanceId, out OverlayEntry entry))
                continue;

            // Dynamic ATK
            int atk = ComputeCurrentCombatAtk(info.unit, info.owner, info.enemy, info.row);

            if (atk != entry.lastAtk)
            {
                entry.lastAtk = atk;

                // Write into prefab TMP (3D or UGUI, TMP_Text covers both)
                if (displayMode == DisplayMode.UsePrefabAnchorText)
                {
                    if (entry.prefabTmp != null)
                        entry.prefabTmp.text = atk.ToString();
                }
                else
                {
                    if (entry.overlayTmpUgui != null)
                        entry.overlayTmpUgui.text = atk.ToString();
                }
            }

            // Only position/billboard when using pooled overlays
            if (displayMode == DisplayMode.PooledCanvasOverlay && entry.overlayGo != null)
            {
                Vector3 anchorPos = GetAnchorPosition(info.unit.InstanceId);

                Vector3 playerOffset = (info.owner == battleRunner.PlayerA) ? playerAOffset : playerBOffset;
                entry.overlayGo.transform.position = anchorPos + playerOffset;

                if (billboardToCamera && mainCam != null)
                {
                    Vector3 toCam = entry.overlayGo.transform.position - mainCam.transform.position;
                    if (toCam.sqrMagnitude > 0.0001f)
                        entry.overlayGo.transform.rotation = Quaternion.LookRotation(toCam, Vector3.up);
                }
            }
            // If UsePrefabAnchorText: we do not reposition at all (it moves with the unit prefab)
        }
    }

    private void SetAllActiveEntriesActive(bool active)
    {
        foreach (var kvp in overlaysByUnitId)
        {
            OverlayEntry e = kvp.Value;
            if (e == null) continue;

            if (displayMode == DisplayMode.PooledCanvasOverlay)
            {
                if (e.overlayGo != null)
                    e.overlayGo.SetActive(active);
            }
            else
            {
                if (e.prefabTmp != null)
                    e.prefabTmp.gameObject.SetActive(active);
            }
        }
    }

    // -----------------------------
    // Entries (prefab TMP vs pooled overlay)
    // -----------------------------

    private void EnsureEntryForUnit(int unitId)
    {
        if (overlaysByUnitId.ContainsKey(unitId))
            return;

        if (displayMode == DisplayMode.UsePrefabAnchorText)
        {
            OverlayEntry entry = BuildPrefabTextEntry(unitId);
            if (entry != null)
                overlaysByUnitId[unitId] = entry;
            return;
        }

        // PooledCanvasOverlay mode
        OverlayEntry pooled = GetOverlayFromPoolOrCreate();
        pooled.unitId = unitId;
        pooled.lastAtk = int.MinValue;

        if (pooled.overlayGo != null)
        {
            pooled.overlayGo.name = $"ATKOverlay_Unit_{unitId}";
            pooled.overlayGo.SetActive(true);
        }

        overlaysByUnitId[unitId] = pooled;
    }

    private OverlayEntry BuildPrefabTextEntry(int unitId)
    {
        GameObject visual = fieldSpawner.GetUnitVisual(unitId);
        if (visual == null)
        {
            // Visual might not be spawned yet this frame; we'll try again next Update.
            return null;
        }

        Transform overlayAnchor = visual.transform.Find(overlayAnchorName);
        if (overlayAnchor == null)
        {
            Debug.LogWarning($"[ATKOverlay] Unit visual '{visual.name}' is missing child '{overlayAnchorName}'. UnitId={unitId}");
            return null;
        }

        // Find TMP text anywhere under OverlayAnchor (or specifically ATKText if you want strictness)
        TMP_Text tmp = null;

        // Prefer strict child name if present
        Transform atkTextTr = overlayAnchor.Find($"ATKRoot/{atkTextChildName}");
        if (atkTextTr == null)
            atkTextTr = overlayAnchor.Find(atkTextChildName);

        if (atkTextTr != null)
            tmp = atkTextTr.GetComponent<TMP_Text>();

        // Fallback: any TMP under anchor
        if (tmp == null)
            tmp = overlayAnchor.GetComponentInChildren<TMP_Text>(true);

        if (tmp == null)
        {
            Debug.LogWarning($"[ATKOverlay] OverlayAnchor exists but no TMP_Text found under it. UnitId={unitId}");
            return null;
        }

        // Ensure it's enabled
        tmp.gameObject.SetActive(true);

        return new OverlayEntry
        {
            unitId = unitId,
            prefabTmp = tmp,
            overlayGo = null,
            overlayTmpUgui = null,
            lastAtk = int.MinValue
        };
    }

    private OverlayEntry GetOverlayFromPoolOrCreate()
    {
        if (overlayPool.Count > 0)
            return overlayPool.Pop();

        // Create new overlay object under canvas
        GameObject overlayGo =
            (overlayPrefab != null)
                ? Instantiate(overlayPrefab, worldSpaceCanvas.transform)
                : CreateDefaultOverlay(worldSpaceCanvas.transform);

        overlayGo.transform.localScale = Vector3.one * overlayWorldScale;

        TextMeshProUGUI tmpUgui = overlayGo.GetComponentInChildren<TextMeshProUGUI>(true);
        if (tmpUgui == null)
            Debug.LogError("[ATKOverlay] Overlay prefab has no TextMeshProUGUI in children.");

        return new OverlayEntry
        {
            unitId = -1,
            prefabTmp = null,
            overlayGo = overlayGo,
            overlayTmpUgui = tmpUgui,
            lastAtk = int.MinValue
        };
    }

    private GameObject CreateDefaultOverlay(Transform parent)
    {
        GameObject root = new GameObject("ATKOverlay_Default");
        root.transform.SetParent(parent, false);

        RectTransform rt = root.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 80);

        GameObject textGo = new GameObject("ATKText");
        textGo.transform.SetParent(root.transform, false);

        RectTransform trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = new Vector2(0.5f, 0.5f);
        trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = new Vector2(200, 80);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "?";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 48;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 18;
        tmp.fontSizeMax = 60;

        return root;
    }

    private void PoolMissingEntries(List<UnitContext> liveUnits)
    {
        var liveIds = new HashSet<int>();
        for (int i = 0; i < liveUnits.Count; i++)
            liveIds.Add(liveUnits[i].unit.InstanceId);

        var toPool = new List<int>();
        foreach (var kvp in overlaysByUnitId)
        {
            if (!liveIds.Contains(kvp.Key))
                toPool.Add(kvp.Key);
        }

        for (int i = 0; i < toPool.Count; i++)
        {
            int unitId = toPool[i];

            if (!overlaysByUnitId.TryGetValue(unitId, out OverlayEntry entry))
                continue;

            overlaysByUnitId.Remove(unitId);

            if (entry == null) continue;

            if (displayMode == DisplayMode.PooledCanvasOverlay)
            {
                if (entry.overlayGo != null)
                {
                    entry.overlayGo.SetActive(false);
                    entry.unitId = -1;
                    entry.lastAtk = int.MinValue;
                    overlayPool.Push(entry);
                }
            }
            else
            {
                // For prefab-attached text, we don't pool/destroy; we simply stop tracking it.
                // (You can choose to hide it here if you want, but leaving it visible is often fine.)
            }
        }
    }

    // -----------------------------
    // Anchoring (used only for pooled overlays)
    // -----------------------------

    private Vector3 GetAnchorPosition(int unitId)
    {
        Vector3 fallback = fieldSpawner.GetUnitPosition(unitId);
        if (anchorMode == AnchorMode.FieldSpawnerPosition)
            return fallback;

        GameObject visual = fieldSpawner.GetUnitVisual(unitId);
        if (visual == null)
            return fallback;

        // If OverlayAnchor exists, use it
        Transform overlayAnchor = visual.transform.Find(overlayAnchorName);
        if (overlayAnchor != null)
            return overlayAnchor.position;

        // Otherwise compute top of combined sprite bounds
        SpriteRenderer[] renderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0)
            return fallback;

        bool hasBounds = false;
        Bounds combined = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || sr.sprite == null) continue;

            if (!hasBounds)
            {
                combined = sr.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(sr.bounds);
            }
        }

        if (!hasBounds)
            return fallback;

        Vector3 topCenter = new Vector3(combined.center.x, combined.max.y + unitVisualTopExtraHeight, combined.center.z);
        return topCenter;
    }

    // -----------------------------
    // Live unit gathering
    // -----------------------------

    private List<UnitContext> GatherAliveUnitsOnField(Player a, Player b)
    {
        var list = new List<UnitContext>(64);
        if (a != null) AddPlayerUnits(a, b, list);
        if (b != null) AddPlayerUnits(b, a, list);
        return list;
    }

    private void AddPlayerUnits(Player owner, Player enemy, List<UnitContext> outList)
    {
        if (owner.Rows == null) return;

        foreach (var kvp in owner.Rows)
        {
            RowType rowType = kvp.Key;
            Row rowObj = kvp.Value;
            if (rowObj == null || rowObj.Units == null) continue;

            for (int i = 0; i < rowObj.Units.Count; i++)
            {
                Unit u = rowObj.Units[i];
                if (u == null || !u.IsAlive) continue;

                outList.Add(new UnitContext
                {
                    unit = u,
                    owner = owner,
                    enemy = enemy,
                    row = rowType
                });
            }
        }
    }

    private void OnDisable()
    {
        // Deactivate pooled overlays; prefab-attached text stays as-is.
        foreach (var kvp in overlaysByUnitId)
        {
            OverlayEntry e = kvp.Value;
            if (e == null) continue;

            if (e.overlayGo != null)
                e.overlayGo.SetActive(false);
        }
    }

    // -----------------------------
    // ATK calculation (dynamic)
    // -----------------------------

    private int ComputeCurrentCombatAtk(Unit unit, Player owner, Player enemy, RowType row)
    {
        if (unit == null || !unit.IsAlive)
            return 0;

        Dictionary<string, int> aliveCountByName = BuildAliveCountByNameOnRow(owner, row);

        int atk = AbilitySystem.GetEffectiveAttackForBuyPhase(unit, owner, enemy, row, aliveCountByName);

        float cmdMult = (CommanderBonus.GetAttackMultiplier != null)
            ? CommanderBonus.GetAttackMultiplier(owner, unit)
            : 1f;

        if (!Mathf.Approximately(cmdMult, 1f))
            atk = Mathf.RoundToInt(atk * cmdMult);

        int perkBonus = (PerkSystem.GetTypeAtkBonus != null)
            ? PerkSystem.GetTypeAtkBonus(owner, unit.Type)
            : 0;
        atk += perkBonus;

        int doctrineBonus = (DoctrineSystem.GetTypeAtkBonus != null)
            ? DoctrineSystem.GetTypeAtkBonus(owner, unit.Type)
            : 0;
        atk += doctrineBonus;

        if (DoctrineSystem.Instance != null)
            atk += DoctrineSystem.Instance.GetCombinedArmsBonus(owner, unit.Type);

        if (atk < 0) atk = 0;
        return atk;
    }

    private Dictionary<string, int> BuildAliveCountByNameOnRow(Player owner, RowType row)
    {
        var dict = new Dictionary<string, int>();

        if (owner == null || owner.Rows == null)
            return dict;

        if (!owner.Rows.TryGetValue(row, out Row rowObj) || rowObj == null || rowObj.Units == null)
            return dict;

        for (int i = 0; i < rowObj.Units.Count; i++)
        {
            Unit u = rowObj.Units[i];
            if (u == null || !u.IsAlive) continue;

            if (!dict.ContainsKey(u.Name))
                dict[u.Name] = 0;
            dict[u.Name]++;
        }

        return dict;
    }
}