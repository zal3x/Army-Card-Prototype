using UnityEngine;

/// <summary>
/// DamageNumberDebugger: Add this to your scene temporarily to diagnose damage number issues.
/// 
/// USAGE:
/// 1. Create empty GameObject in scene
/// 2. Add this component
/// 3. Run your battle
/// 4. Check console for detailed spawn logs
/// 5. Remove when fixed
/// </summary>
public class DamageNumberDebugger : MonoBehaviour
{
    [Header("Test Settings")]
    [Tooltip("If true, spawns test damage numbers on Start")]
    public bool spawnTestNumbers = true;
    
    [Tooltip("Positions to test spawn at")]
    public Vector3[] testPositions = new Vector3[]
    {
        new Vector3(-3f, 0f, 0f), // Left
        new Vector3(0f, 0f, 0f),  // Center
        new Vector3(3f, 0f, 0f)   // Right
    };

    private void Start()
    {
        Debug.Log("=== DAMAGE NUMBER DEBUGGER STARTED ===");
        
        // Check if spawner exists
        DamageNumberSpawner spawner = FindObjectOfType<DamageNumberSpawner>();
        if (spawner == null)
        {
            Debug.LogError("[DEBUG] No DamageNumberSpawner found in scene!");
            return;
        }
        else
        {
            Debug.Log($"[DEBUG] Found DamageNumberSpawner on '{spawner.gameObject.name}'");
            Debug.Log($"[DEBUG] - Enabled: {spawner.enableDamageNumbers}");
            Debug.Log($"[DEBUG] - Prefab: {(spawner.damageNumberPrefab != null ? spawner.damageNumberPrefab.name : "NULL")}");
            Debug.Log($"[DEBUG] - Spawn Offset: {spawner.spawnHeightOffset}");
        }
        
        // Check if prefab has required components
        if (spawner.damageNumberPrefab != null)
        {
            FloatingDamageNumber fdn = spawner.damageNumberPrefab.GetComponent<FloatingDamageNumber>();
            if (fdn == null)
            {
                Debug.LogError("[DEBUG] Prefab missing FloatingDamageNumber component!");
            }
            else
            {
                Debug.Log("[DEBUG] Prefab has FloatingDamageNumber component ✓");
            }
        }
        
        if (spawnTestNumbers)
        {
            Invoke(nameof(SpawnTestNumbers), 1f);
        }
    }

    private void SpawnTestNumbers()
    {
        Debug.Log("=== SPAWNING TEST DAMAGE NUMBERS ===");
        
        // Test each damage type at different positions
        foreach (var pos in testPositions)
        {
            Debug.Log($"[DEBUG] Spawning at {pos}");
            
            // Normal damage
            DamageNumberSpawner.Spawn(25, pos + Vector3.up * 2f, DamageNumberType.Normal);
            
            // Commander damage
            DamageNumberSpawner.Spawn(10, pos + Vector3.up * 1.5f, DamageNumberType.CommanderDamage);
            
            // Ability bonus
            DamageNumberSpawner.Spawn(0, pos + Vector3.up * 1f, DamageNumberType.AbilityBonus, "x2", "ATK");
        }
        
        Debug.Log("[DEBUG] Test numbers spawned. If you don't see them:");
        Debug.Log("  1. Check Camera position (are you looking at the right area?)");
        Debug.Log("  2. Check Canvas render mode (should be World Space)");
        Debug.Log("  3. Check sorting layers (VFX layer should be visible)");
        Debug.Log("  4. Check prefab scale (might be too small/large)");
        Debug.Log("  5. Enable 'logEvents' on CombatPlayback to see event details");
    }

    [ContextMenu("Force Spawn Test Numbers Now")]
    public void ForceSpawnTestNumbers()
    {
        SpawnTestNumbers();
    }

    [ContextMenu("Check Scene Setup")]
    public void CheckSceneSetup()
    {
        Debug.Log("=== CHECKING DAMAGE NUMBER SETUP ===");
        
        // Check spawner
        var spawner = FindObjectOfType<DamageNumberSpawner>();
        if (spawner == null)
        {
            Debug.LogError("✗ No DamageNumberSpawner in scene");
            return;
        }
        Debug.Log("✓ DamageNumberSpawner exists");
        
        // Check spawner settings
        if (!spawner.enableDamageNumbers)
            Debug.LogWarning("⚠ DamageNumberSpawner.enableDamageNumbers is FALSE");
        else
            Debug.Log("✓ DamageNumberSpawner enabled");
        
        if (spawner.damageNumberPrefab == null)
        {
            Debug.LogError("✗ No prefab assigned to DamageNumberSpawner");
            return;
        }
        Debug.Log($"✓ Prefab assigned: {spawner.damageNumberPrefab.name}");
        
        // Check prefab components
        var fdn = spawner.damageNumberPrefab.GetComponent<FloatingDamageNumber>();
        if (fdn == null)
        {
            Debug.LogError("✗ Prefab missing FloatingDamageNumber component");
            return;
        }
        Debug.Log("✓ Prefab has FloatingDamageNumber");
        
        var tmp = spawner.damageNumberPrefab.GetComponentInChildren<TMPro.TextMeshProUGUI>();
        if (tmp == null)
        {
            Debug.LogError("✗ Prefab missing TextMeshProUGUI component");
            return;
        }
        Debug.Log("✓ Prefab has TextMeshProUGUI");
        
        // Check canvas
        var canvas = tmp.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("✗ TextMeshProUGUI not under a Canvas");
            return;
        }
        Debug.Log($"✓ Canvas exists (RenderMode: {canvas.renderMode})");
        
        if (canvas.renderMode != RenderMode.WorldSpace)
        {
            Debug.LogWarning($"⚠ Canvas render mode is {canvas.renderMode} (should be WorldSpace for damage numbers)");
        }
        
        Debug.Log("=== SETUP CHECK COMPLETE ===");
        Debug.Log("If numbers still don't appear, try enabling 'spawnTestNumbers' on this component");
    }
}