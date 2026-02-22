# CombatVisualSettings Integration Status

## ✅ Currently Hooked Up

### **Health Bars** (WorldHealthBar.cs)
- ✅ `healthBarTweenDuration` - Controls animation speed of HP changes
- ✅ `healthBarColorFull/Low` - Gradient color based on HP percentage
- ✅ `flashHealthBarOnDamage` - White flash when taking damage
- ✅ `damageFlashDuration` - How long the flash lasts
- ✅ `alwaysShowHealthBars` - Show always vs only when damaged
- ✅ `alwaysShowDamagedHealth` - Keep bars visible for damaged units (currentHp < maxHp)
- ✅ `healthBarVisibilityDuration` - How long bars stay visible after damage

### **Combat Playback** (CombatPlayback.cs)
- ✅ `combatSpeedMultiplier` - Overall speed control
- ✅ `defaultEventDelay` - Delay between events
- ✅ `unitDestroyDelay` - Extra pause after unit deaths
- ✅ `commanderDamageDelay` - Extra pause after commander damage
- ✅ `skipCombatAnimations` - Instant combat (via ApplySpeedMultiplier)

### **Rearrangement** (FieldSpawner.cs)
- ✅ `rearrangementMoveDuration` - Unit movement speed
- ✅ `rowSwapDuration` - Row swap animation speed

### **Death & VFX** (CombatVFXSpawner.cs, UnitVisualEffects.cs)
- ✅ `deathExplosionScale` - Size of death explosions
- ✅ `enableDamageFlash` - Flash unit white when taking damage
- ✅ `damageFlashDuration` - How long unit damage flash lasts
- ✅ `enableDamageShake` - Shake unit when taking damage
- ✅ `damageShakeIntensity` - Intensity of unit shake
- ⚠️ Not yet: explosion duration control (uses sprite FPS)

### **Special Effects** (CameraShake.cs, CombatVFXSpawner.cs)
- ✅ `enableScreenShake` - Enable camera shake on big hits
- ✅ `screenShakeIntensity` - Camera shake intensity multiplier
- ✅ `enableDeathSlowMotion` - Slow-motion on unit death
- ✅ `deathSlowMotionScale` - Slow-motion time scale
- ✅ `deathSlowMotionDuration` - How long slow-motion lasts
- ⚠️ Not yet: `showTargetMarkIndicator` - Visual indicator for marked units

---

### **Unit Damage Effects** (CombatPlayback.cs)
- ✅ Unit flash/shake effects integrated with `DamageAppliedToUnit` event
- ✅ Camera shake triggered on damage

## ⚠️ Ready But Not Connected Yet

### **Camera Shake Setup**
1. Create empty GameObject named "CameraShake" in your scene
2. Add CameraShake component
3. That's it! It will auto-find the main camera

### **Damage Numbers** (DamageNumberSpawner.cs)
```csharp
// Add to DamageNumberSpawner.Spawn():
var settings = CombatVisualSettings.Instance;
float scale = settings.GetDamageNumberScale();
float lifetime = settings.damageNumberLifetime;
float riseSpeed = settings.damageNumberRiseSpeed;
```

### **Card Flight** (CardFlightManager.cs)
```csharp
// Add to flight methods:
var settings = CombatVisualSettings.Instance;
float duration = settings.cardFlightDuration;
float arc = settings.cardFlightArcHeight;
float rotation = settings.cardFlightRotation;
```

---

## 🎯 Quick Setup Guide

### 1. Create the Settings Asset (One Time)

1. In Unity Project window, navigate to `Assets/Resources/`
2. Right-click → **Create > Settings > Combat Visual Settings**
3. Name it exactly: `CombatVisualSettings` (case-sensitive!)
4. The file must be in `Resources/` for auto-loading to work

### 2. Adjust Settings in Inspector

Open the asset and adjust sliders:
- **Health Bars** → Tween Duration (0.05-1.0s)
- **Combat Playback** → Speed Multiplier (0.25-5.0x)
- **Damage Numbers** → Lifetime, Rise Speed, Scale
- **Rearrangement** → Movement Duration, Row Swap Duration

### 3. Test in Play Mode

- Health bars animate smoothly during combat ✓
- Combat playback respects speed multiplier ✓
- Rearrangement uses new durations ✓
- Change settings while game is running to see live updates!

---

## 🎨 Preset Examples

### Fast-Paced Action
```
healthBarTweenDuration = 0.1
combatSpeedMultiplier = 1.5
defaultEventDelay = 0.1
rearrangementMoveDuration = 0.15
```

### Cinematic/Dramatic
```
healthBarTweenDuration = 0.3
combatSpeedMultiplier = 0.75
defaultEventDelay = 0.25
pauseBetweenPhases = true
```

### Debug/Testing
```
skipCombatAnimations = true
(Plays entire combat instantly)
```

---

## 📋 Integration Checklist

- [x] Create CombatVisualSettings.cs
- [x] Create settings asset in Resources/
- [x] Hook up WorldHealthBar tween duration
- [x] Hook up WorldHealthBar colors (gradient green→red)
- [x] Hook up WorldHealthBar visibility (always show vs on-damage)
- [x] Hook up WorldHealthBar damaged-unit visibility (alwaysShowDamagedHealth)
- [x] Hook up WorldHealthBar flash effect
- [x] Hook up CombatPlayback delays
- [x] Hook up FieldSpawner rearrangement
- [x] Hook up CombatVFXSpawner explosion scale
- [x] Add unit damage flash effect (UnitVisualEffects)
- [x] Add unit damage shake effect (UnitVisualEffects)
- [x] Add screen shake on big hits (CameraShake)
- [x] Add death slow-motion effect (CombatVFXSpawner)
- [ ] Hook up DamageNumberSpawner (optional)
- [ ] Hook up CardFlightManager (optional)
- [ ] Add target mark indicator (optional)

---

## 🚀 Next Steps (Optional Enhancements)

1. **Damage Number Integration**: Add scale/lifetime to DamageNumberSpawner
2. **Health Bar Colors**: Use `GetHealthBarColor()` to change color based on HP%
3. **Card Flight**: Hook up durations for smoother card animations
4. **Screen Shake**: Implement screen shake on big hits
5. **Accessibility**: Add colorblind mode color overrides

---

## 🔍 How to Check if Settings Are Working

1. Create the settings asset in `Resources/`
2. Play the game
3. During combat, health bars should animate at your chosen speed
4. Check Console - should NOT see warning about missing settings
5. Change `combatSpeedMultiplier` to 2.0 → combat should play 2x faster
6. Change `healthBarTweenDuration` to 0.5 → HP bars animate slower

If you see `[CombatVisualSettings] No settings found in Resources folder`:
- Settings asset is missing or in wrong folder
- Must be named exactly `CombatVisualSettings`
- Must be in `Assets/Resources/` folder
