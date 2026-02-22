# Battle Tactics — Project Status Update (Feb 3, 2026)

## Current playable loop (what’s working right now)

**Core round loop is in place and playable end-to-end:**
- Multi-round battle flow: **Gold → Draw (round 2+) → Buy (UI for Player A, AI for Player B) → Rearrange (round 2+) → Combat resolve → Combat playback → World sync**.
- Deterministic-ish testing knobs exist (shuffle seeds, AI buy randomness, etc.) to reproduce scenarios.
- World visuals spawn/despawn and are slot-managed (no more classic overlap issues in normal flows).
- Card UI is carousel-based and supports special cards (separate prefab/template).
- Special cards system exists with per-round and per-battle state, including Radar/Recon overlays, KGB tick, etc.
- Card flight system is integrated:
  - Unit cards fly to unit spawn slot, slam, then unit reveals.
  - Special cards fly to a “special impact point” and only apply their effect **after** slam callback.

**Combat feedback is already strong:**
- Floating damage numbers are implemented (type-colored, pop scale, drift + fade).
- Commander HUD exists and can spawn commander damage numbers.
- Army stats HUD updates after phases.

## Major systems & where they live (high-level map)

- **Battle orchestration:** `BattleTestRunner`
  - Owns the round loop + seeds + player state + phase progression.
  - Calls combat resolution, then plays it back via `CombatPlayback`.
- **Simulation:** `CombatResolver` + model classes (`Player`, `Row`, `Unit`)
  - Runs the “truth” of combat.
- **World visuals + slots:** `FieldSpawner`
  - Spawns unit prefabs, tracks unitId → visual, manages occupied slots + reservations.
  - Supports manual lane reorder and row swap visual syncing.
- **UI (Buy + Cards):** `BuyPhaseUI_FixedHand`, `CardCarousel`, `CardVisual`
  - Carousel selection, card removal returning a live CardVisual for flight.
- **Card flight:** `CardFlightManager`
  - Flies bought card visuals to unit or special impact point.
  - Reuses original CardVisual on unit death for discard flight (preferred path).
- **HUD:** `BattleHUD` (+ commander/army stat display components)
  - Commanders, army stats, gold display, commander damage number spawn positions.

## The current “unfinished” feature: Active unit health bars

**Last known target:** health bar integration for active units on the battlefield, and bars responding to damage during combat playback.

### What’s missing (in practical terms)
Right now:
- Unit visuals exist as world prefabs (spawned & moved by `FieldSpawner`).
- Combat playback shows damage numbers (and likely applies visual death/VFX timing).
- **But there is no per-unit HP UI element** bound to each world unit visual that:
  1) Initializes to MaxHP/HP,
  2) Updates on each damage event during playback,
  3) Handles heals (if/when introduced),
  4) Hides/destroys correctly when the unit dies and its visual is removed.

### Why this is non-trivial in this project (important context)
You have **two separate timelines**:
1) **Simulation timeline** (CombatResolver) — computes final HPs and produces a CombatReport.
2) **Playback timeline** (CombatPlayback) — replays events over time, spawning damage numbers and VFX.

To make health bars “respond to active damage,” the bars must update in **playback time**, not just jump to final HP at the end.

## Recommended implementation plan (minimal risk path)

### A) Add a health bar component to world unit prefabs
Create a script like `UnitHealthBarWorld` and attach it to each unit world prefab (or a shared child prefab).

**Responsibilities:**
- Own references to:
  - `Slider` or `Image fill` (prefer Image fill for world space),
  - optional `CanvasGroup` for fades,
  - an optional `Text` for “HP / MaxHP”.
- Provide API:
  - `Initialize(int maxHp, int currentHp)`
  - `SetHP(int newHp, bool animate)`
  - `SetMaxHP(int maxHp)` (optional)
  - `OnDeath()` (fade out, disable)

**Important:** Don’t make it query Player/Row/Unit on its own; feed it from systems that already know unitId.

### B) Ensure you can locate the bar by unitId (binding step)
You already track `unitId → visual GameObject` in `FieldSpawner`.
Add a helper method to `FieldSpawner`:

- `bool TryGetUnitHealthBar(int unitId, out UnitHealthBarWorld bar)`
  - Find the visual by unitId.
  - GetComponentInChildren<UnitHealthBarWorld>().
  - Cache it if you want (optional).

Then when a unit is spawned (TrySpawn / reserved spawn), initialize:
- `bar.Initialize(unit.MaxHP, unit.HP)`.

### C) Update bars during playback events
Where to hook:
- `CombatPlayback` should be the driver, because it already iterates through events with timing.

Add playback-time HP tracking:
- Maintain dictionaries in `CombatPlayback`:
  - `unitId → currentHp`
  - `unitId → maxHp`
- Initialize these dictionaries at `Play(report)` start, using the *starting snapshot* if available, or by reading current simulation state before playing.

When a damage event fires:
- decrement tracked hp
- update the world bar via `FieldSpawner.TryGetUnitHealthBar(unitId, out bar)`

**Do NOT** wait until the end of playback to set final HP values; update per event.

### D) Handle unit destruction timing
When a death event happens:
- set bar to 0
- optionally fade bar
- then when `FieldSpawner.SyncToBoard()` removes visuals after playback, bars will go away naturally.

If you do “death VFX then remove visual,” make sure the bar is on that visual so it disappears in sync.

## Known related touchpoints & gotchas

- `FieldSpawner` already:
  - Moves visuals on rearrangement
  - Frees slots on death and sync
  - Stores `transformsByUnitId` for positioning damage numbers
- `CardFlightManager` may reuse original CardVisual on death to fly to discard; **health bars should be tied to the world unit prefab**, not the card visual.
- If you introduce **healing** (e.g., Medic special), health bars should handle upward changes too.

## Next concrete tasks (ordered)

1. **Create `UnitHealthBarWorld`** (script + prefab/child setup).
2. Add `TryGetUnitHealthBar()` (or similar) in `FieldSpawner`.
3. **Initialize bars on spawn**:
   - In `SpawnWorldVisualForBoughtUnit()` (BattleTestRunner) or inside `FieldSpawner.TrySpawn()` after Instantiate.
4. **Update bars during `CombatPlayback`**:
   - Add per-event HP updates and call into FieldSpawner to set bar value.
5. Ensure bar cleanup:
   - On unit death (playback), set to 0 + fade.
   - On world sync removal, nothing extra needed if bar is part of unit prefab.

## Quick “definition of done” for the feature
- Buying a unit shows a full HP bar above it.
- During combat playback, each time damage is dealt, the bar decreases immediately (with optional smooth tween).
- If a unit dies, bar hits 0 and disappears/fades as the unit death plays.
- No null refs when visuals are destroyed or swapped/moved.

---

## References (code locations)
- Battle flow + buy → combat: `BattleTestRunner`
- Unit visual spawn/track/move: `FieldSpawner`
- HUD + commander damage numbers: `BattleHUD`
- Damage number visuals: `FloatingDamageNumber`
- Card flight system: `CardFlightManager`
