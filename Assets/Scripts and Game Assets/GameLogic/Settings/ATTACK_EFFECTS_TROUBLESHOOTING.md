# Attack Effects Troubleshooting

## Quick Diagnostic Checklist

### 1. Check Component Setup
- [ ] `UnitAttackEffects` component is on unit prefab
- [ ] Attack Type is NOT set to "None"
- [ ] Muzzle Flash Size is visible (try 0.5 for testing)
- [ ] Muzzle Flash Color has visible alpha (try pure yellow 1,1,0)

### 2. Check Sorting Layers
1. Go to Edit > Project Settings > Tags and Layers
2. Expand "Sorting Layers"
3. Verify "VFX" layer exists
4. Make sure VFX layer is ABOVE "Unit" layer in the list (higher order = renders on top)

**Correct order example:**
```
0: Default
1: Background
2: Unit
3: VFX  <-- Must be above Unit layer
4: UI
```

### 3. Check Combat Events
Add this debug line to see if attacks are being triggered:

In `FieldSpawner.PlayUnitAttackEffects()` at line 1470:
```csharp
public void PlayUnitAttackEffects(int attackerUnitId, int targetUnitId)
{
    Debug.Log($"[ATTACK] Attacker {attackerUnitId} attacking target {targetUnitId}"); // ADD THIS

    if (attackerUnitId < 0) return;
    // ... rest of method
}
```

### 4. Check for SourceUnit in Events
The attack effects require `e.SourceUnit` to exist. Check your combat log:

```csharp
// In CombatPlayback, add before the attack call:
Debug.Log($"[DEBUG] SourceUnit: {e.SourceUnit?.InstanceId}, TargetUnit: {e.TargetUnit?.InstanceId}");
```

### 5. Test with Maximum Visibility
Temporarily max out the settings to see if anything appears:

```csharp
// In UnitAttackEffects Inspector:
Attack Type: MuzzleFlash
Muzzle Flash Color: (1, 1, 0, 1) - Pure yellow
Muzzle Flash Size: 2.0 (huge!)
Enable Recoil: true
Recoil Distance: 0.5
```

Run combat and watch for yellow circles.

---

## Common Issues

### Issue: "VFX layer doesn't exist"
**Solution:**
1. Edit > Project Settings > Tags and Layers
2. Click "+" under Sorting Layers
3. Name it exactly "VFX" (case-sensitive)
4. Drag it above "Unit" layer

### Issue: "Component is on prefab but not working"
**Solution:**
- Check that you saved the prefab after adding component
- Check that the prefab instance is being used in battle (not a different copy)
- Try adding component to prefab instance in scene during play mode to test

### Issue: "SourceUnit is null in combat events"
**Solution:**
This means the combat events aren't tracking who dealt the damage.

Check `CombatResolver.cs` - when creating damage events:
```csharp
// Should look like this:
report.AddEvent(new CombatEvent {
    Type = CombatEventType.DamageAppliedToUnit,
    SourceUnit = attackingUnit,  // <-- Must be set!
    TargetUnit = targetUnit,
    Amount = damage
});
```

### Issue: "Effects spawn but immediately disappear"
**Solution:**
- VFX layer might be behind Unit layer
- Reorder in Tags and Layers so VFX is on top

### Issue: "Projectiles go in wrong direction"
**Solution:**
- Ensure target position is valid
- Add debug to see positions:
```csharp
Debug.Log($"Projectile: {spawnPos} -> {targetPos}");
```

---

## Test Scene Setup

Create a minimal test:

1. **Create test unit prefab:**
   - GameObject with SpriteRenderer (any sprite)
   - Add `UnitAttackEffects`
   - Set Attack Type = MuzzleFlash
   - Set Color = Yellow (1, 1, 0)
   - Set Size = 1.0

2. **Create test script:**
```csharp
public class AttackEffectTest : MonoBehaviour
{
    public UnitAttackEffects attackFx;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("Testing attack effect!");
            Vector3 target = transform.position + Vector3.right * 2f;
            attackFx.PlayAttackEffect(target);
        }
    }
}
```

3. **Test:**
   - Run scene
   - Press Spacebar
   - Should see yellow flash at unit position

---

## Debug Mode

Add comprehensive logging to `UnitAttackEffects.PlayAttackEffect()`:

```csharp
public void PlayAttackEffect(Vector3 targetPosition)
{
    Debug.Log($"[ATTACK FX] PlayAttackEffect called. Type: {attackType}");

    switch (attackType)
    {
        case AttackEffectType.MuzzleFlash:
            Debug.Log("[ATTACK FX] Spawning muzzle flash");
            SpawnMuzzleFlash();
            if (enableRecoil)
            {
                Debug.Log("[ATTACK FX] Playing recoil");
                PlayRecoil();
            }
            break;
        // ... etc
    }
}
```

And in `SpawnMuzzleFlash()`:
```csharp
private void SpawnMuzzleFlash()
{
    Vector3 spawnPos = attackSpawnPoint.position;
    Debug.Log($"[MUZZLE FLASH] Spawning at {spawnPos}, color: {muzzleFlashColor}, size: {muzzleFlashSize}");

    GameObject flash = new GameObject("MuzzleFlash");
    flash.transform.position = spawnPos;

    SpriteRenderer sr = flash.AddComponent<SpriteRenderer>();
    // ... rest of method

    Debug.Log($"[MUZZLE FLASH] Created! Sorting layer: {sr.sortingLayerName}, order: {sr.sortingOrder}");
}
```

---

## Expected Console Output (Working)

When combat happens, you should see:
```
[ATTACK] Attacker 12345 attacking target 67890
[ATTACK FX] PlayAttackEffect called. Type: MuzzleFlash
[ATTACK FX] Spawning muzzle flash
[MUZZLE FLASH] Spawning at (2.5, 1.0, 0), color: RGBA(1.000, 0.900, 0.300, 1.000), size: 0.5
[MUZZLE FLASH] Created! Sorting layer: VFX, order: 100
```

## Expected Console Output (Not Working)

If you see NONE of the above, the component isn't being called at all. Check:
1. Is component on prefab?
2. Is CombatPlayback calling it?
3. Does SourceUnit exist in events?

---

## Quick Fix: Fallback to Debug Circles

If VFX layer is causing issues, temporarily change sorting layer in code:

In `UnitAttackEffects.cs`, find these lines:
```csharp
sr.sortingLayerName = "VFX";
sr.sortingOrder = 100;
```

Change to:
```csharp
sr.sortingLayerName = "Default";  // Use Default layer temporarily
sr.sortingOrder = 1000;           // Very high order to render on top
```

This should make effects visible even if VFX layer isn't set up correctly.

---

## Still Not Working?

1. **Check Unity Console for errors**
   - Look for NullReferenceException
   - Look for "sorting layer 'VFX' not found"

2. **Verify battle is actually happening**
   - Do damage numbers appear?
   - Do health bars update?
   - If not, combat system isn't working

3. **Test in Editor vs Build**
   - Try in Play Mode in editor first
   - Some effects might not show in Scene view

4. **Check Camera**
   - Is camera positioned to see the battlefield?
   - Is camera's culling mask including VFX layer?

5. **Scene Lighting**
   - Some sprites need lit environment
   - Try setting sprite material to "Sprites/Default" (unlit)
