# Unit Attack Effects Setup Guide

This guide explains how to add firing/attack animations to your units using simple placeholder effects.

---

## 🎯 Quick Setup (Per Unit Prefab)

### 1. Add Component to Unit Prefab

1. Open your unit prefab (e.g., Infantry, Tank, Plane)
2. Add `UnitAttackEffects` component
3. Configure the attack type and colors in Inspector
4. Save prefab

**That's it!** No individual animations needed - the system uses simple visual effects.

---

## 🎨 Attack Effect Types

### **None**
- No visual effect when attacking
- Use for: Support units, abilities that don't "fire"

### **MuzzleFlashOnly**
- Just a quick flash at the unit's position
- Use for: Melee units, explosives, area effects
- Best for: Infantry with grenades, artillery

### **MuzzleFlash** (Recommended for most units)
- Flash at unit position + optional recoil animation
- Use for: Rifles, cannons, machine guns
- Best for: Infantry, tanks, mounted guns

### **Projectile**
- Flash + visible projectile that travels to target
- Use for: Rockets, missiles, tracers
- Best for: Rocket launchers, planes, helicopters

---

## 📋 Recommended Settings by Unit Type

### Infantry (Rifleman)
```
Attack Type: MuzzleFlash
Muzzle Flash Color: Yellow-white (1, 0.9, 0.3)
Muzzle Flash Size: 0.2
Enable Recoil: false
```

### Infantry (RPG / Rocket Launcher)
```
Attack Type: Projectile
Muzzle Flash Color: Orange (1, 0.6, 0.2)
Muzzle Flash Size: 0.4
Projectile Color: Orange-yellow (1, 0.7, 0.3)
Projectile Size: 0.15
Projectile Speed: 15
Enable Recoil: true
Recoil Distance: 0.15
```

### Tank / Armored Vehicle
```
Attack Type: MuzzleFlash
Muzzle Flash Color: Bright orange (1, 0.7, 0.2)
Muzzle Flash Size: 0.5
Enable Recoil: true
Recoil Distance: 0.2
```

### Plane / Helicopter
```
Attack Type: Projectile
Muzzle Flash Color: Yellow-white (1, 0.95, 0.5)
Muzzle Flash Size: 0.15
Projectile Color: Yellow (1, 1, 0.5)
Projectile Size: 0.08
Projectile Speed: 30
Enable Recoil: false
```

### Artillery / Long Range
```
Attack Type: Projectile
Muzzle Flash Color: Bright red-orange (1, 0.5, 0)
Muzzle Flash Size: 0.6
Projectile Color: Red-orange (1, 0.6, 0.2)
Projectile Size: 0.2
Projectile Speed: 25
Enable Recoil: true
Recoil Distance: 0.25
```

### Melee / Special Units
```
Attack Type: MuzzleFlashOnly
Muzzle Flash Color: White or unit-specific color
Muzzle Flash Size: 0.3
```

---

## 🔧 Advanced: Custom Sprites

If you want to replace the placeholder circles with actual sprites:

1. Create your muzzle flash sprite (PNG, white foreground, transparent background)
2. Create your projectile sprite (PNG, arrow/bullet shape)
3. In unit prefab, assign sprites to:
   - `Muzzle Flash Sprite` (optional)
   - `Projectile Sprite` (optional)

**Sprite Tips:**
- Muzzle flash: 64x64px, star/burst shape
- Projectile: 32x16px, bullet/missile shape pointing right
- Use white color with alpha - script will tint it

---

## 🎬 Attack Spawn Points

By default, effects spawn at the unit's center. To customize:

1. In unit prefab, create child GameObject named "AttackSpawnPoint"
2. Position it where you want muzzle flash/projectiles to originate (e.g., barrel of tank gun)
3. Component will auto-find and use it

**Example Positions:**
- Infantry: Slightly in front and at chest level
- Tank: At the end of the cannon barrel
- Plane: Under the wings or nose

---

## ⚙️ How It Works

```csharp
// When unit A attacks unit B during combat:
1. CombatPlayback detects DamageAppliedToUnit event
2. Calls fieldSpawner.PlayUnitAttackEffects(attackerID, targetID)
3. UnitAttackEffects component spawns visual effect:
   - Muzzle flash appears at attacker
   - Optional projectile travels from attacker to target
   - Optional recoil animation plays
4. Target unit takes damage (existing system)
```

---

## 🎨 Color Palette Reference

**Weapon Types:**
- Small arms (rifles): Yellow-white `(1, 0.9, 0.3)`
- Cannons/tanks: Orange `(1, 0.7, 0.2)`
- Explosives/rockets: Red-orange `(1, 0.5, 0)`
- Energy weapons: Cyan/blue `(0.3, 0.7, 1)`
- Lasers: Bright red `(1, 0.2, 0.2)`
- Plasma: Purple `(0.8, 0.3, 1)`

**Projectile Colors:**
- Bullets/tracers: Yellow-white `(1, 1, 0.5)`
- Rockets: Orange `(1, 0.6, 0.2)`
- Missiles: Red `(1, 0.3, 0.3)`
- Energy bolts: Cyan `(0.3, 0.8, 1)`

---

## 🚀 Testing

1. Add `UnitAttackEffects` to at least one unit prefab
2. Run a battle in Unity
3. Watch for muzzle flashes when units attack
4. Adjust colors/sizes in Inspector during play mode to preview
5. Stop play mode and apply changes to prefab

---

## 💡 Tips & Best Practices

### Performance
- Simple effects are intentionally lightweight
- No particle systems needed for basic combat
- Minimal memory/CPU usage

### Visual Polish
- Use different colors for different factions (e.g., red vs blue tracers)
- Larger units = larger muzzle flash size
- Slower projectile speed = more visible (15-20 for dramatic)
- Faster projectile speed = more realistic (25-35 for bullets)

### Recoil
- Only enable for heavy weapons (tanks, artillery, RPGs)
- Small units (infantry) usually don't need visible recoil
- Increase recoil distance for heavier weapons

### Timing
- Muzzle flash plays instantly when attack starts
- Projectiles travel in real-time (affected by Time.timeScale)
- Impact flash spawns when projectile reaches target

---

## 🔄 Upgrading to Full Animations Later

This placeholder system is designed to be easily replaced:

1. Keep `UnitAttackEffects` component
2. Add your animated sprites to unit prefab
3. Set `Attack Type = None` to disable placeholder effects
4. Add Unity Animator with attack animation
5. Trigger animation from `PlayAttackEffect()` method

**OR**: Extend `UnitAttackEffects` to play Animator instead of spawning sprites.

---

## 📝 Example Configuration

### Setup for "Army Demo Deck"

```
Infantry Rifleman:
- Attack Type: MuzzleFlash
- Muzzle Color: (1, 0.9, 0.3) Yellow
- Size: 0.2
- Recoil: Off

RPG Soldier:
- Attack Type: Projectile
- Muzzle Color: (1, 0.6, 0.2) Orange
- Projectile Color: (1, 0.7, 0.3)
- Size: 0.15
- Speed: 15
- Recoil: On (0.15)

M1A2 Tank:
- Attack Type: MuzzleFlash
- Muzzle Color: (1, 0.7, 0.2) Bright orange
- Size: 0.5
- Recoil: On (0.2)

F-16 Fighter:
- Attack Type: Projectile
- Muzzle Color: (1, 0.95, 0.5) Yellow-white
- Projectile Color: (1, 1, 0.5) Yellow
- Size: 0.08
- Speed: 30
- Recoil: Off

Artillery:
- Attack Type: Projectile
- Muzzle Color: (1, 0.5, 0) Red-orange
- Projectile Color: (1, 0.6, 0.2)
- Size: 0.2
- Speed: 20
- Recoil: On (0.25)
```

---

## 🐛 Troubleshooting

**Effects not appearing:**
- Check that `UnitAttackEffects` component is on prefab
- Verify unit has `SpriteRenderer` (for recoil to work)
- Make sure "VFX" sorting layer exists (Edit > Project Settings > Tags and Layers)

**Projectiles going wrong direction:**
- Ensure FieldSpawner can find target unit position
- Check that target unit ID is valid

**Effects too small/large:**
- Adjust `Muzzle Flash Size` and `Projectile Size` in Inspector
- Test in play mode and adjust in real-time

**Performance issues:**
- Use `MuzzleFlashOnly` for most units
- Reserve `Projectile` type for special units (10-20% of army)
- Reduce projectile speed to spawn fewer frames

---

## 🎯 Quick Reference: What Goes Where

```
Unit Prefab
├── SpriteRenderer (existing)
├── WorldHealthBar (existing)
├── UnitVisualEffects (damage feedback)
└── UnitAttackEffects (NEW - firing effects)
    └── (Optional) AttackSpawnPoint child object
```

No code changes needed in CombatPlayback - already hooked up!
