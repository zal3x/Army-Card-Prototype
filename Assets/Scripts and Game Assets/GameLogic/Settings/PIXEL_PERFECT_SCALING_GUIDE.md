# Pixel-Perfect Scaling Guide for Your Auto Battler

## The Core Problem

You have:
- Infantry: 256x256 pixels
- Vehicles/Ranged/Air: 512x512 pixels
- Screen: 1920x1080, split vertically (960px per player side)
- 3 rows × 5 units = 15 units per side

And you need to fit everything while keeping pixel art crisp.

---

## Why Scaling Matters for Pixel Art

### Pixel-Perfect Scales (Safe to Use)
These scales render cleanly because they map to exact pixel boundaries:

| Scale | Effect | When to Use |
|-------|--------|-------------|
| 0.25x | 1/4 size | Very small units |
| 0.5x | 1/2 size | Medium reduction |
| 1x | Original | Default |
| 2x | Double | Larger display |
| 3x | Triple | Very large |

### Non-Pixel-Perfect Scales (Cause Issues)
These cause pixel shimmering/blurring:

| Scale | Problem |
|-------|---------|
| 0.75x | 256 × 0.75 = 192 pixels (works) but 512 × 0.75 = 384 (odd scaling) |
| 1.2x | Creates sub-pixel artifacts |
| 0.8x | Doesn't map to clean pixel grid |

---

## Your Specific Layout Challenge

### Current Measurements

At **100 PPU** (Pixels Per Unit):

| Unit Type | Pixels | World Units | At 0.5x Scale |
|-----------|--------|-------------|---------------|
| Infantry | 256 | 2.56 units | 1.28 units |
| Vehicle | 512 | 5.12 units | 2.56 units |
| Air | 512 | 5.12 units | 2.56 units |

### Screen Space Available

With ortho size ~5.4 (which gives ~10.8 units vertical):
- Horizontal: ~19.2 units total (1920 ÷ 100)
- Per player side: ~9.6 units

### Fitting 5 Units Per Row

**For 512px vehicles at 1x scale:**
- Each unit: 5.12 units wide
- 5 units: 25.6 units needed
- Available: ~9.6 units
- **Won't fit!**

**For 512px vehicles at 0.5x scale:**
- Each unit: 2.56 units wide
- 5 units: 12.8 units needed
- Available: ~9.6 units
- **Still tight, but possible with overlap**

---

## Solutions

### Solution 1: Design Assets at Target Size (BEST)

Instead of scaling 512px art down, create new art at the size you want:

| Unit Type | Current | If You Need ~192px Final |
|-----------|---------|--------------------------|
| Infantry | 256px | Keep at 256px, scale 0.75 → **OR** redraw at 192px, use 1x |
| Vehicle | 512px | Redraw at 384px, use 0.5x = 192px |
| Air | 512px | Redraw at 384px, use 0.5x = 192px |

**Benefits:**
- Perfectly crisp pixels
- Full control over detail at actual display size
- No runtime scaling artifacts

### Solution 2: Embrace Non-Pixel-Perfect for Distant Units

For air units "in the sky," slight blur might actually enhance the look:
- Air at 0.4x scale → appears more distant
- The slight blur reads as atmospheric haze
- Only matters for planes, not ground units

### Solution 3: Adjust PPU (Pixels Per Unit)

If your art is 256px and you want more flexibility:

| PPU Setting | 256px Sprite = | 512px Sprite = |
|-------------|----------------|----------------|
| 100 | 2.56 units | 5.12 units |
| 128 | 2.0 units | 4.0 units |
| 256 | 1.0 units | 2.0 units |

With **PPU = 256**, a 256px infantry sprite = 1 world unit exactly.

### Solution 4: Use PixelPerfectCamera's "Upscale RT" Mode

Unity's Pixel Perfect Camera has a setting called **"Upscale Render Texture"** that:
1. Renders at a lower resolution
2. Upscales to screen resolution using point filtering

This allows any scale while maintaining crispness, at the cost of a more "chunky" look.

---

## Recommended Layout for Your Game

Based on your requirements (3 rows, 5 units, 960px per side):

### Option A: Uniform Scaling (Simplest)

```
Infantry:  256px at 0.5x  = 128px display = 1.28 world units
Vehicle:   512px at 0.25x = 128px display = 1.28 world units
Air:       512px at 0.25x = 128px display = 1.28 world units
```

All units same size, clean pixel-perfect scaling.

### Option B: Hierarchical Scaling (More Visual Interest)

```
Infantry:  256px at 1x    = 256px display = 2.56 world units (FRONT)
Vehicle:   512px at 0.5x  = 256px display = 2.56 world units (MIDDLE)
Air:       512px at 0.25x = 128px display = 1.28 world units (BACK/SKY)
```

Infantry and vehicles are prominent, air units are smaller (distant).

### Option C: Custom Art Sizes (Best Quality)

Create your art at the exact display sizes you want:
- Infantry: Draw at 160px, display at 1x
- Vehicle: Draw at 200px, display at 1x
- Air: Draw at 120px, display at 1x

---

## Calculating Your Grid

For 5 units per row in 960px (assuming some padding):

```
Available width: 960px - 100px padding = 860px
Per unit: 860 / 5 = 172px max

With 20px spacing between units:
(860 - 4*20) / 5 = 156px per unit
```

So your units should display at ~156px or smaller.

| Original Size | Scale Needed | Pixel-Perfect? |
|--------------|--------------|----------------|
| 256px → 156px | 0.609x | ❌ No |
| 256px → 128px | 0.5x | ✅ Yes |
| 512px → 256px | 0.5x | ✅ Yes |
| 512px → 128px | 0.25x | ✅ Yes |

---

## Quick Reference: Scale Math

To check if a scale is pixel-perfect:

```csharp
bool IsPixelPerfect(float scale)
{
    // Must be 1/2^n or n (integer)
    float[] valid = { 0.125f, 0.25f, 0.5f, 1f, 2f, 3f, 4f };
    return valid.Contains(scale) ||
           (scale >= 1f && Mathf.Approximately(scale, Mathf.Round(scale)));
}
```

---

## Summary

1. **Stick to 0.25x, 0.5x, 1x, 2x scales** for crisp pixels
2. **Don't use 0.75x or 1.2x** - they cause shimmering
3. **Consider drawing new art at target size** rather than scaling
4. **Air units can be blurry** - it looks like atmospheric perspective
5. **Use the BattlefieldGridTool** to preview layouts before committing
