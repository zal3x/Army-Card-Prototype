# Visual Effects Setup Guide

This guide explains how to set up and use the new combat visual effects system.

---

## 🎯 Quick Setup (5 Minutes)

### 1. Add Components to Unit Prefabs

For **each unit prefab** (Infantry, Tank, Plane, etc.):

1. Open the prefab in Unity
2. Add `UnitVisualEffects` component
3. Assign the unit's `SpriteRenderer` in the Inspector (or leave blank for auto-detect)
4. Check "Use Global Settings" (enabled by default)
5. Save prefab

**What this does:**
- Units will flash white when taking damage
- Units will shake briefly when hit
- Effects controlled by `CombatVisualSettings`

---

### 2. Add Camera Shake to Scene

1. Create empty GameObject named "CameraShake"
2. Add `CameraShake` component
3. Check "Use Global Settings" (enabled by default)

**What this does:**
- Camera shakes on big hits (damage > largeHitThreshold)
- Camera shakes on unit deaths
- Intensity controlled by `CombatVisualSettings`

**Note:** If you already have a camera manager, you can add `CameraShake` to that instead.

---

### 3. Connect to Combat Playback

To trigger effects during combat, add this code to `CombatPlayback.cs`:

```csharp
// In HandleEvent() method, find the UnitDamaged case:
case CombatEventType.UnitDamaged:
    if (e.TargetUnit != null)
    {
        int unitId = e.TargetUnit.InstanceId;
        int damage = e.Damage;

        // Update health bar (already exists)
        fieldSpawner?.UpdateUnitHealthBar(unitId, e.TargetUnit.MaxHp, e.TargetUnit.CurrentHp);

        // NEW: Add this line to trigger visual effects
        fieldSpawner?.PlayUnitDamageEffects(unitId, damage);
    }
    break;
```

---

## 🎨 Available Effects

### Unit Damage Effects (`UnitVisualEffects.cs`)

**Flash Effect:**
- Unit sprite flashes white briefly when taking damage
- Duration: `damageFlashDuration` (default 0.1s)
- Controlled by: `enableDamageFlash`

**Shake Effect:**
- Unit shakes/vibrates when hit
- Intensity: `damageShakeIntensity` (default 0.3)
- Controlled by: `enableDamageShake`

**How to trigger:**
```csharp
UnitVisualEffects vfx = unit.GetComponentInChildren<UnitVisualEffects>();
vfx.PlayDamageEffects(); // Flash + shake
// Or separately:
vfx.FlashDamage();
vfx.ShakeDamage();
```

---

### Camera Shake (`CameraShake.cs`)

**On Damage:**
- Scales with damage amount
- Larger hits = stronger shake
- Only shakes if damage > 20% of `largeHitThreshold`

**On Death:**
- Strong shake when units die
- Fixed intensity (0.6)

**How to trigger:**
```csharp
CameraShake.Instance.ShakeOnDamage(damageAmount);
CameraShake.Instance.ShakeOnDeath();
CameraShake.Instance.Shake(intensity, duration); // Custom shake
```

---

### Death Slow-Motion (`CombatVFXSpawner.cs`)

**Effect:**
- Briefly slows down time when units die
- Creates dramatic "bullet-time" effect

**Settings:**
- `enableDeathSlowMotion` - Enable/disable effect
- `deathSlowMotionScale` - Time scale during effect (0.5 = half speed)
- `deathSlowMotionDuration` - How long effect lasts (seconds)

**How it works:**
- Automatically triggered by `SpawnDeathExplosion()`
- Uses `Time.timeScale` to slow everything down
- Restores normal time after duration expires

---

## ⚙️ Settings Reference

All effects are controlled via `CombatVisualSettings` asset.

### Death & VFX Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `enableDamageFlash` | true | Flash unit white when taking damage |
| `damageFlashDuration` | 0.1s | How long unit flash lasts |
| `enableDamageShake` | true | Shake unit when taking damage |
| `damageShakeIntensity` | 0.3 | How much unit shakes (0-1) |
| `deathExplosionScale` | 1.0 | Size of death explosions |
| `deathExplosionDuration` | 0.5s | How long explosions last |

### Special Effects Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `enableScreenShake` | true | Enable camera shake on big hits |
| `screenShakeIntensity` | 1.0 | Camera shake strength multiplier |
| `enableDeathSlowMotion` | false | Slow-motion on unit death |
| `deathSlowMotionScale` | 0.5 | Time scale during slow-mo (0.5 = half speed) |
| `deathSlowMotionDuration` | 0.3s | How long slow-motion lasts |

---

## 🎬 Preset Configurations

### Action-Packed
```
enableDamageFlash = true
damageFlashDuration = 0.1
enableDamageShake = true
damageShakeIntensity = 0.5
enableScreenShake = true
screenShakeIntensity = 1.2
enableDeathSlowMotion = false
```

### Cinematic/Dramatic
```
enableDamageFlash = true
damageFlashDuration = 0.15
enableDamageShake = true
damageShakeIntensity = 0.4
enableScreenShake = true
screenShakeIntensity = 0.8
enableDeathSlowMotion = true
deathSlowMotionScale = 0.4
deathSlowMotionDuration = 0.4
```

### Minimal/Clean
```
enableDamageFlash = false
enableDamageShake = false
enableScreenShake = false
enableDeathSlowMotion = false
```

### Debug/Testing
```
enableDamageFlash = true
damageFlashDuration = 0.5  (longer to see clearly)
enableDamageShake = true
damageShakeIntensity = 1.0 (max shake)
enableScreenShake = true
screenShakeIntensity = 2.0 (exaggerated)
```

---

## 🔧 Advanced Usage

### Custom Unit Flash Colors

By default, units flash white. To customize:

1. Edit `UnitVisualEffects.cs`
2. Find `FlashRoutine()` method
3. Change `Color flashColor = Color.white;` to your preferred color

### Per-Unit Effect Overrides

If you want specific units to have different effects:

1. Uncheck "Use Global Settings" on that unit's `UnitVisualEffects`
2. Set `localFlashDuration` and `localShakeIntensity` in Inspector

### Disable Effects for Performance

To completely disable effects:

1. Don't add `UnitVisualEffects` to unit prefabs
2. Don't create `CameraShake` GameObject
3. Set `enableDeathSlowMotion = false` in settings

---

## 🐛 Troubleshooting

**Effects not working:**
- Check that components are added to prefabs
- Verify `useGlobalSettings` is enabled
- Check that settings asset exists in `Resources/`
- Make sure `CombatPlayback` calls `PlayUnitDamageEffects()`

**Camera shake too intense:**
- Lower `screenShakeIntensity` in settings (try 0.5)
- Lower `damageShakeIntensity` (try 0.2)

**Slow-motion feels weird:**
- Try shorter duration (0.2s instead of 0.3s)
- Try less extreme scale (0.7 instead of 0.5)
- Disable it entirely if it disrupts gameplay

**Unit flash not visible:**
- Make sure unit has a `SpriteRenderer`
- Check that `enableDamageFlash` is true
- Try increasing `damageFlashDuration` to 0.2s to see it more clearly

---

## 📋 Integration Checklist

- [ ] Add `UnitVisualEffects` to all unit prefabs
- [ ] Create `CameraShake` GameObject in scene
- [ ] Add `PlayUnitDamageEffects()` call to `CombatPlayback.cs`
- [ ] Test damage flash by attacking a unit
- [ ] Test damage shake by dealing small damage
- [ ] Test camera shake by dealing large damage
- [ ] Test death slow-motion by killing a unit
- [ ] Adjust settings to your preference

---

## 🚀 Next Steps

1. **Damage Numbers**: Hook up `DamageNumberSpawner` to show floating damage text
2. **Card Flight**: Integrate card animation durations with settings
3. **Target Marks**: Add visual indicator for units affected by "Mark Target" ability
4. **Screen Flash**: Add full-screen flash on commander damage
5. **Unit Highlights**: Highlight units that can be targeted during abilities

---

## 💡 Tips

- Test effects in slow-motion first (`combatSpeedMultiplier = 0.5`) to see them clearly
- Start with default settings, then adjust to taste
- Use "Debug/Testing" preset to exaggerate effects for visibility
- Remember: `alwaysShowHealthBars = false` works great with the flash/shake effects to keep the battlefield clean
