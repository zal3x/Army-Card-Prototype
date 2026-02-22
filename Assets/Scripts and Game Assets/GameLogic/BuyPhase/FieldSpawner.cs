
using UnityEngine;
using System;
using System.Collections.Generic;


/// <summary>
/// FieldSpawner: Manages world-space unit visuals on the battlefield.
///
/// UPDATED VERSION: Now supports card flight animations
/// - New method to get spawn position BEFORE spawning (for card flight target)
/// - New method to spawn unit with delayed visibility (hidden until card lands)
/// - Tracks slot reservations separately from actual spawns
///
/// IMPROVEMENTS IN THIS VERSION:
/// - Tracks which spawn slots are occupied (fixes overlapping bug)
/// - Reuses freed slots when units die
/// - Better sync between simulation and visuals
/// - Supports card flight system integration
/// </summary>
public enum PlayerSide { A, B }


public class FieldSpawner : MonoBehaviour
{
   // ========================================
   // SPAWN POINT CONFIGURATION
   // ========================================
  
   [Header("Player A spawn rows (left-to-right order)")]
   [SerializeField] private Transform[] infantryRowA;
   [SerializeField] private Transform[] vehicleRowA;
   [SerializeField] private Transform[] airRowA;
   [Tooltip("Used on GroundOnly battlefields where Ranged has its own row")]
   [SerializeField] private Transform[] rangedRowA;


   [Header("Player B spawn rows (left-to-right order)")]
   [SerializeField] private Transform[] infantryRowB;
   [SerializeField] private Transform[] vehicleRowB;
   [SerializeField] private Transform[] airRowB;
   [Tooltip("Used on GroundOnly battlefields where Ranged has its own row")]
   [SerializeField] private Transform[] rangedRowB;


  // Track world health bars by UnitId (optional component on prefab)
private Dictionary<int, WorldHealthBar> healthBarsByUnitId = new Dictionary<int, WorldHealthBar>();

// Track spawn order for visual layering (later spawns appear in front)
private int nextSortingOrder = 10; // Start at base sorting order (matches prefab default)


   // ========================================
   // PRIVATE STATE - IMPROVED TRACKING
   // ========================================


   // Maps UnitId â†’ spawned GameObject
   private readonly Dictionary<int, GameObject> visualsByUnitId = new Dictionary<int, GameObject>();
  
   // Maps UnitId â†’ Transform (for damage numbers, etc.)
   private readonly Dictionary<int, Transform> transformsByUnitId = new Dictionary<int, Transform>();
  
   // Maps UnitId â†’ Slot Index (so we can free slots when units die)
   private readonly Dictionary<int, SlotInfo> slotByUnitId = new Dictionary<int, SlotInfo>();
  
   // Tracks which slots are occupied
   private readonly HashSet<int> occupiedSlotsInfA = new HashSet<int>();
   private readonly HashSet<int> occupiedSlotsVehA = new HashSet<int>();
   private readonly HashSet<int> occupiedSlotsAirA = new HashSet<int>();
   private readonly HashSet<int> occupiedSlotsRangedA = new HashSet<int>();
   private readonly HashSet<int> occupiedSlotsInfB = new HashSet<int>();
   private readonly HashSet<int> occupiedSlotsVehB = new HashSet<int>();
   private readonly HashSet<int> occupiedSlotsAirB = new HashSet<int>();
   private readonly HashSet<int> occupiedSlotsRangedB = new HashSet<int>();


   // NEW: Track reserved slots (claimed but not yet spawned - for card flight)
   private readonly Dictionary<int, SlotReservation> reservedSlots = new Dictionary<int, SlotReservation>();


   // RowType (Front/Middle/Back) → which UnitType slot array to use, per side.
// Default: Front=Infantry, Middle=Vehicle, Back=Air.
private readonly Dictionary<PlayerSide, Dictionary<RowType, UnitType>> rowLayoutMap
   = new Dictionary<PlayerSide, Dictionary<RowType, UnitType>>()
{
   { PlayerSide.A, new Dictionary<RowType, UnitType>
       {
           { RowType.Front, UnitType.Infantry },
           { RowType.Middle, UnitType.Vehicle },
           { RowType.Back, UnitType.Air }
       }
   },
   { PlayerSide.B, new Dictionary<RowType, UnitType>
       {
           { RowType.Front, UnitType.Infantry },
           { RowType.Middle, UnitType.Vehicle },
           { RowType.Back, UnitType.Air }
       }
   }
};
public void SetRowLayout(PlayerSide side, UnitType front, UnitType middle, UnitType back)
{
   rowLayoutMap[side][RowType.Front] = front;
   rowLayoutMap[side][RowType.Middle] = middle;
   rowLayoutMap[side][RowType.Back] = back;


   Debug.Log($"[FieldSpawner] RowLayout set for {side}: Front={front}, Middle={middle}, Back={back}");
}




   // Helper struct to remember where a unit was spawned
   private struct SlotInfo
   {
       public UnitType unitType;
       public PlayerSide side;
       public int slotIndex;
   }


   // NEW: Reservation info for card flight system
   private struct SlotReservation
   {
       public UnitType unitType;
       public PlayerSide side;
       public int slotIndex;
       public Vector3 position;
       public Quaternion rotation;
   }


   // ========================================
   // PUBLIC API - ORIGINAL METHODS
   // ========================================


 // ========================================
// PUBLIC API - ORIGINAL METHODS (UPDATED)
// ========================================


/// <summary>
/// Spawns a unit visual. Now accounts for row swaps during rearrangement.
/// </summary>
/// <param name="def">Card definition</param>
/// <param name="side">Which player</param>
/// <param name="unitId">Unit instance ID</param>
/// <param name="ownerPlayer">Optional: Player reference to check for row swaps</param>
public bool TrySpawn(UnitCardDefinition def, PlayerSide side, int unitId, Player ownerPlayer = null)
{
   if (def == null)
   {
       Debug.LogError("[FieldSpawner] def is null.");
       return false;
   }


   GameObject prefabToSpawn = (side == PlayerSide.A) ? def.worldPrefabA : def.worldPrefabB;


   if (prefabToSpawn == null)
   {
       Debug.LogError($"[FieldSpawner] Missing prefab for {side} on card '{def.cardName}'.");
       return false;
   }


UnitType spawnRowType = GetDefaultRowForCardType(def.type);




   // Check if we have a reservation for this unit
   if (reservedSlots.TryGetValue(unitId, out SlotReservation reservation))
   {
       // Use reserved slot
       GameObject go = Instantiate(prefabToSpawn, reservation.position, reservation.rotation);

       // Apply scale from spawn point if available
       Transform reservedSlot = FindSlotTransform(reservation.unitType, reservation.side, reservation.slotIndex);
       if (reservedSlot != null)
       {
           ApplySpawnPointScale(go, reservedSlot);
       }

       if (unitId >= 0)
       {
           RegisterSpawn(unitId, go, reservation.unitType, reservation.side, reservation.slotIndex);
       }


       // Clear reservation
       reservedSlots.Remove(unitId);


       Debug.Log($"[FieldSpawner] Spawned {def.cardName} (UnitId={unitId}) at RESERVED {side} slot {reservation.slotIndex}");
       return true;
   }


   // No reservation - find next available slot using the correct spawn row type
   int slotIndex;
   Transform slot = GetNextAvailableSlot(spawnRowType, side, out slotIndex);
  
   if (slot == null)
   {
       Debug.LogWarning($"[FieldSpawner] No available slot for {side} / {spawnRowType} (row full or rows swapped).");
       return false;
   }


   // Instantiate the visual at the spawn point
   GameObject go2 = Instantiate(prefabToSpawn, slot.position, slot.rotation);

   // Apply scale from spawn point if available (non-destructive to prefab)
   ApplySpawnPointScale(go2, slot);

   var handle = go2.GetComponent<UnitWorldHandle>();
   if (handle == null) handle = go2.AddComponent<UnitWorldHandle>();
   handle.Init(unitId, side, def.type);






   // Track this visual
   if (unitId >= 0)
   {
       RegisterSpawn(unitId, go2, spawnRowType, side, slotIndex);
   }


   Debug.Log($"[FieldSpawner] Spawned {def.cardName} (UnitId={unitId}) at {side} {spawnRowType} slot {slotIndex}");


   return true;
}


/// <summary>
/// Legacy overload - spawns without unit ID or player context.
/// </summary>
public bool TrySpawn(UnitCardDefinition def, PlayerSide side)
{
   Debug.LogError(
       "[FieldSpawner] ERROR: TrySpawn(def, side) is deprecated. " +
       "Use TrySpawn(def, side, unitId, ownerPlayer) so slot tracking works."
   );
   return false;
}






   // ========================================
   // PUBLIC API - NEW METHODS FOR CARD FLIGHT
   // ========================================


   /// <summary>
   /// Reserves a slot and returns its world position WITHOUT spawning yet.
   /// Used by card flight system to know where the card should fly TO.
   ///
   /// Call TrySpawnAtReservedSlot() later to actually spawn the unit.
   /// </summary>
   /// <param name="unitType">Type of unit (determines which row)</param>
   /// <param name="side">Which player</param>
   /// <param name="unitId">Unit instance ID (used to track reservation)</param>
   /// <param name="position">OUT: World position of the slot</param>
   /// <returns>True if a slot was reserved, false if row is full</returns>
  public bool TryReserveSlot(UnitType cardType, PlayerSide side, int unitId, Player player, out Vector3 position)
{
   position = Vector3.zero;


   // Check if already reserved
   if (reservedSlots.ContainsKey(unitId))
   {
       position = reservedSlots[unitId].position;
       return true;
   }


UnitType spawnRowType = GetDefaultRowForCardType(cardType);


   // Find next available slot
   int slotIndex;
   Transform slot = GetNextAvailableSlot(spawnRowType, side, out slotIndex);
  
   if (slot == null)
   {
       Debug.LogWarning($"[FieldSpawner] No available slot to reserve for {side} / {spawnRowType}");
       return false;
   }


   // Reserve the slot
   reservedSlots[unitId] = new SlotReservation
   {
       unitType = spawnRowType,
       side = side,
       slotIndex = slotIndex,
       position = slot.position,
       rotation = slot.rotation
   };


   // Mark slot as occupied so nothing else takes it
   MarkSlotOccupied(spawnRowType, side, slotIndex);


   position = slot.position;
   Debug.Log($"[FieldSpawner] Reserved slot for UnitId={unitId}: {side} {spawnRowType} slot {slotIndex} at {position}");


   return true;
}


   /// <summary>
   /// Spawns a unit at its previously reserved slot.
   /// Call this after card flight animation completes.
   /// </summary>
   /// <param name="def">Card definition (for prefab)</param>
   /// <param name="side">Which player</param>
   /// <param name="unitId">Unit instance ID (must match reservation)</param>
   /// <param name="startHidden">If true, unit starts invisible (for smoke reveal)</param>
   /// <returns>The spawned GameObject, or null if failed</returns>
   public GameObject TrySpawnAtReservedSlot(UnitCardDefinition def, PlayerSide side, int unitId, bool startHidden = false)
   {
       if (def == null)
       {
           Debug.LogError("[FieldSpawner] def is null.");
           return null;
       }


       if (!reservedSlots.TryGetValue(unitId, out SlotReservation reservation))
       {
           Debug.LogWarning($"[FieldSpawner] No reservation found for UnitId={unitId}. Using TrySpawn instead.");
           TrySpawn(def, side, unitId);
           return visualsByUnitId.TryGetValue(unitId, out var fallbackGo) ? fallbackGo : null;
       }


       GameObject prefabToSpawn = (side == PlayerSide.A) ? def.worldPrefabA : def.worldPrefabB;


       if (prefabToSpawn == null)
       {
           Debug.LogError($"[FieldSpawner] Missing prefab for {side} on card '{def.cardName}'.");
           CancelReservation(unitId);
           return null;
       }


       // Instantiate at reserved position
       GameObject go = Instantiate(prefabToSpawn, reservation.position, reservation.rotation);

       // Apply scale from spawn point if available
       Transform reservedSlot = FindSlotTransform(reservation.unitType, reservation.side, reservation.slotIndex);
       if (reservedSlot != null)
       {
           ApplySpawnPointScale(go, reservedSlot);
       }

       // Optionally start hidden
       if (startHidden)
       {
           SetVisibility(go, false);
       }


       // Register spawn
       if (unitId >= 0)
       {
           RegisterSpawn(unitId, go, reservation.unitType, reservation.side, reservation.slotIndex);
       }


       // Clear reservation
       reservedSlots.Remove(unitId);


       Debug.Log($"[FieldSpawner] Spawned {def.cardName} (UnitId={unitId}) at reserved slot. Hidden={startHidden}");


       return go;
   }


   /// <summary>
   /// Cancels a slot reservation (e.g., if purchase fails).
   /// </summary>
   public void CancelReservation(int unitId)
   {
       if (reservedSlots.TryGetValue(unitId, out SlotReservation reservation))
       {
           // Free the slot
           FreeSlot(reservation.unitType, reservation.side, reservation.slotIndex);
           reservedSlots.Remove(unitId);
           Debug.Log($"[FieldSpawner] Cancelled reservation for UnitId={unitId}");
       }
   }


   /// <summary>
   /// Shows or hides a unit visual.
   /// Used for smoke reveal effect.
   /// </summary>
   public void SetUnitVisibility(int unitId, bool visible)
   {
       if (visualsByUnitId.TryGetValue(unitId, out GameObject go) && go != null)
       {
           SetVisibility(go, visible);
       }
   }


   /// <summary>
   /// Gets the spawn position for a unit type WITHOUT reserving.
   /// Use this for preview/targeting purposes.
   /// </summary>
   public Vector3 PeekNextSpawnPosition(UnitType unitType, PlayerSide side)
   {
       int slotIndex;
       Transform slot = GetNextAvailableSlot(unitType, side, out slotIndex);
       return slot != null ? slot.position : Vector3.zero;
   }
// ========================================
// PUBLIC API - REARRANGEMENT SUPPORT
// ========================================


/// <summary>
/// Moves a unit visual to a new slot position.
/// Used during rearrangement phase to sync visuals with simulation.
/// </summary>
/// <param name="unitId">Unit instance ID</param>
/// <param name="newSlotIndex">New slot index in its current row</param>
/// <param name="animate">If true, animates the movement. If false, instant teleport.</param>
/// <param name="duration">Animation duration (only used if animate=true and not using global settings)</param>
public void MoveUnitToSlot(int unitId, int newSlotIndex, bool animate = true, float duration = 0.3f)
{
   // Find the unit's visual and current slot info
   if (!visualsByUnitId.TryGetValue(unitId, out GameObject visual) || visual == null)
   {
       Debug.LogWarning($"[FieldSpawner] Cannot move unit {unitId} - visual not found");
       return;
   }


   if (!slotByUnitId.TryGetValue(unitId, out SlotInfo currentSlot))
   {
       Debug.LogWarning($"[FieldSpawner] Cannot move unit {unitId} - slot info not found");
       return;
   }


   // Get the target slot transform
   Transform[] row = GetRowArray(currentSlot.unitType, currentSlot.side);

   if (row == null || newSlotIndex < 0 || newSlotIndex >= row.Length)
   {
       Debug.LogWarning($"[FieldSpawner] Invalid slot index {newSlotIndex} for unit {unitId}");
       return;
   }


   Transform targetSlot = row[newSlotIndex];
   if (targetSlot == null)
   {
       Debug.LogWarning($"[FieldSpawner] Target slot {newSlotIndex} is null");
       return;
   }


   // Update slot tracking (free old slot, occupy new slot)
   FreeSlot(currentSlot.unitType, currentSlot.side, currentSlot.slotIndex);
   MarkSlotOccupied(currentSlot.unitType, currentSlot.side, newSlotIndex);


   // Update stored slot info
   slotByUnitId[unitId] = new SlotInfo
   {
       unitType = currentSlot.unitType,
       side = currentSlot.side,
       slotIndex = newSlotIndex
   };


   // Move the visual
   if (animate && duration > 0f)
   {
       // Use global settings if available
       float effectiveDuration = duration;
       if (CombatVisualSettings.Instance != null)
           effectiveDuration = CombatVisualSettings.Instance.rearrangementMoveDuration;

       // Animate movement with LeanTween
       LeanTween.move(visual, targetSlot.position, effectiveDuration)
           .setEase(LeanTweenType.easeInOutQuad);
   }
   else
   {
       // Instant teleport
       visual.transform.position = targetSlot.position;
   }


   Debug.Log($"[FieldSpawner] Moved unit {unitId} from slot {currentSlot.slotIndex} to {newSlotIndex} (animate={animate})");
}


/// <summary>
/// Swaps two rows' unit visuals.
/// Used when rearrangement swaps entire rows (e.g., Front <-> Middle).
/// </summary>
/// <param name="side">Which player</param>
/// <param name="rowA">First row type</param>
/// <param name="rowB">Second row type</param>
/// <param name="unitIdsInRowA">Unit IDs currently in rowA (after swap in simulation)</param>
/// <param name="unitIdsInRowB">Unit IDs currently in rowB (after swap in simulation)</param>
/// <param name="animate">If true, animates the swap</param>
/// <param name="duration">Animation duration (only used if not using global settings)</param>
public void SwapRowVisuals(
   PlayerSide side,
   RowType rowA,
   RowType rowB,
   int[] unitIdsInRowA,
   int[] unitIdsInRowB,
   bool animate = true,
   float duration = 0.4f)
{
   // Convert RowType to UnitType for slot lookup
UnitType typeA = RowTypeToUnitType(side, rowA);
UnitType typeB = RowTypeToUnitType(side, rowB);

   // Use global settings if available
   float effectiveDuration = duration;
   if (CombatVisualSettings.Instance != null)
       effectiveDuration = CombatVisualSettings.Instance.rowSwapDuration;

   Transform[] slotsA = GetRowArray(typeA, side);
   Transform[] slotsB = GetRowArray(typeB, side);


   // Move units that are now in rowA to rowA slots
   for (int i = 0; i < unitIdsInRowA.Length; i++)
   {
       int unitId = unitIdsInRowA[i];
      
       if (!visualsByUnitId.TryGetValue(unitId, out GameObject visual) || visual == null)
           continue;


       if (i >= slotsA.Length)
       {
           Debug.LogWarning($"[FieldSpawner] SwapRows: Not enough slots in rowA for unit {unitId}");
           continue;
       }


       Vector3 targetPos = slotsA[i].position;


       // Update tracking
       if (slotByUnitId.ContainsKey(unitId))
       {
           SlotInfo oldInfo = slotByUnitId[unitId];
           FreeSlot(oldInfo.unitType, oldInfo.side, oldInfo.slotIndex);
       }


       slotByUnitId[unitId] = new SlotInfo
       {
           unitType = typeA,
           side = side,
           slotIndex = i
       };


       MarkSlotOccupied(typeA, side, i);


       // Animate or teleport
       if (animate && effectiveDuration > 0f)
       {
           LeanTween.move(visual, targetPos, effectiveDuration).setEase(LeanTweenType.easeInOutQuad);
       }
       else
       {
           visual.transform.position = targetPos;
       }
   }


   // Move units that are now in rowB to rowB slots
   for (int i = 0; i < unitIdsInRowB.Length; i++)
   {
       int unitId = unitIdsInRowB[i];

       if (!visualsByUnitId.TryGetValue(unitId, out GameObject visual) || visual == null)
           continue;


       if (i >= slotsB.Length)
       {
           Debug.LogWarning($"[FieldSpawner] SwapRows: Not enough slots in rowB for unit {unitId}");
           continue;
       }


       Vector3 targetPos = slotsB[i].position;


       // Update tracking
       if (slotByUnitId.ContainsKey(unitId))
       {
           SlotInfo oldInfo = slotByUnitId[unitId];
           FreeSlot(oldInfo.unitType, oldInfo.side, oldInfo.slotIndex);
       }


       slotByUnitId[unitId] = new SlotInfo
       {
           unitType = typeB,
           side = side,
           slotIndex = i
       };


       MarkSlotOccupied(typeB, side, i);


       // Animate or teleport
       if (animate && effectiveDuration > 0f)
       {
           LeanTween.move(visual, targetPos, effectiveDuration).setEase(LeanTweenType.easeInOutQuad);
       }
       else
       {
           visual.transform.position = targetPos;
       }
   }


   Debug.Log($"[FieldSpawner] Swapped row visuals: {side} {rowA} <-> {rowB} (animate={animate})");
}


/// <summary>
/// Helper to convert RowType to UnitType for slot array lookup.
/// </summary>
private UnitType RowTypeToUnitType(PlayerSide side, RowType row)
{
   if (rowLayoutMap.TryGetValue(side, out var map) && map.TryGetValue(row, out var t))
       return t;


   // fallback
   return row switch
   {
       RowType.Front => UnitType.Infantry,
       RowType.Middle => UnitType.Vehicle,
       RowType.Back => UnitType.Air,
       _ => UnitType.Infantry
   };
}


   // ========================================
   // PUBLIC API - SYNC AND QUERY
   // ========================================


   public void SyncToBoard(Player playerA, Player playerB)
   {
       // Build set of all alive unit IDs
       HashSet<int> alive = new HashSet<int>();
       AddAlive(playerA, alive);
       AddAlive(playerB, alive);


       // Find visuals whose unit is no longer alive
       List<int> toRemove = new List<int>();


       foreach (var kvp in visualsByUnitId)
       {
           int unitId = kvp.Key;
           GameObject go = kvp.Value;


           if (!alive.Contains(unitId))
           {
               // Unit is dead â†’ destroy visual
               if (go != null) Destroy(go);
               toRemove.Add(unitId);
           }
       }


       // Remove from tracking dictionaries AND free up slots
       foreach (int unitId in toRemove)
       {
           // Free the slot this unit was using
           if (slotByUnitId.TryGetValue(unitId, out SlotInfo slot))
           {
               FreeSlot(slot.unitType, slot.side, slot.slotIndex);
               slotByUnitId.Remove(unitId);
           }
          
           visualsByUnitId.Remove(unitId);
           transformsByUnitId.Remove(unitId);
       }


       if (toRemove.Count > 0)
       {
           Debug.Log($"[FieldSpawner] SyncToBoard removed {toRemove.Count} dead unit visuals and freed their slots");
       }
   }


   public void ReorderLaneVisuals(PlayerSide side, UnitType laneType, int[] unitIdsInOrder, bool animate = true, float duration = 0.25f)
{
   var slots = GetRowArray(laneType, side);
   if (slots == null) return;

   // Use global settings if available
   float effectiveDuration = duration;
   if (CombatVisualSettings.Instance != null)
       effectiveDuration = CombatVisualSettings.Instance.rearrangementMoveDuration;

   // 1) Clear occupied indices for this lane+side
   var occ = GetOccupiedSet(laneType, side);
   occ.Clear();


   // 2) Reassign each unit to its new slot index
   for (int i = 0; i < unitIdsInOrder.Length; i++)
   {
       int unitId = unitIdsInOrder[i];
       if (!visualsByUnitId.TryGetValue(unitId, out var visual) || visual == null)
           continue;


       // Update tracking
       slotByUnitId[unitId] = new SlotInfo { unitType = laneType, side = side, slotIndex = i };
       occ.Add(i);


       // Move visual
       Vector3 targetPos = slots[i].position;
       if (animate && effectiveDuration > 0f)
           LeanTween.move(visual, targetPos, effectiveDuration).setEase(LeanTweenType.easeInOutQuad);
       else
           visual.transform.position = targetPos;
   }
}


// helper to route to the right occupied hashset
private HashSet<int> GetOccupiedSet(UnitType laneType, PlayerSide side)
{
   // Handle Ranged based on battlefield mode
   if (laneType == UnitType.Ranged)
   {
       if (Battlefield.IsRangedSeparate)
           return side == PlayerSide.A ? occupiedSlotsRangedA : occupiedSlotsRangedB;
       else
           return side == PlayerSide.A ? occupiedSlotsVehA : occupiedSlotsVehB;
   }

   return (laneType, side) switch
   {
       (UnitType.Infantry, PlayerSide.A) => occupiedSlotsInfA,
       (UnitType.Vehicle, PlayerSide.A) => occupiedSlotsVehA,
       (UnitType.Air, PlayerSide.A) => occupiedSlotsAirA,
       (UnitType.Infantry, PlayerSide.B) => occupiedSlotsInfB,
       (UnitType.Vehicle, PlayerSide.B) => occupiedSlotsVehB,
       (UnitType.Air, PlayerSide.B) => occupiedSlotsAirB,
       _ => occupiedSlotsInfA
   };
}




   /// <summary>
   /// NEW: Syncs to board but returns list of dead unit IDs before removing.
   /// Used by combat playback to trigger death animations.
   /// </summary>
   public List<int> GetDeadUnitIds(Player playerA, Player playerB)
   {
       HashSet<int> alive = new HashSet<int>();
       AddAlive(playerA, alive);
       AddAlive(playerB, alive);


       List<int> deadIds = new List<int>();


       foreach (var kvp in visualsByUnitId)
       {
           if (!alive.Contains(kvp.Key))
           {
               deadIds.Add(kvp.Key);
           }
       }


       return deadIds;
   }


   /// <summary>
   /// Gets which side a unit belongs to.
   /// </summary>
   public bool TryGetUnitSide(int unitId, out PlayerSide side)
   {
       if (slotByUnitId.TryGetValue(unitId, out SlotInfo info))
       {
           side = info.side;
           return true;
       }
       side = PlayerSide.A;
       return false;
   }


   /// <summary>
   /// Gets all tracked unit IDs for a specific player side.
   /// Used by CombatPlayback to get units for opening volley before combat resolution modifies player data.
   /// </summary>
   public List<int> GetTrackedUnitIdsByPlayer(PlayerSide side)
   {
       var result = new List<int>();
       foreach (var kvp in slotByUnitId)
       {
           if (kvp.Value.side == side)
           {
               result.Add(kvp.Key);
           }
       }
       return result;
   }


   public Vector3 GetUnitPosition(int unitId)
   {
       if (transformsByUnitId.TryGetValue(unitId, out var transform) && transform != null)
           return transform.position;

       return Vector3.zero;
   }

   /// <summary>
   /// Gets a unit's visual GameObject without removing it.
   /// Used for attaching effects (like MarkTargetEffect) to units.
   /// </summary>
   public GameObject GetUnitVisual(int unitId)
   {
       if (visualsByUnitId.TryGetValue(unitId, out GameObject go))
           return go;
       return null;
   }


   /// <summary>
   /// Gets and removes a unit's visual GameObject.
   /// Used by CombatVFXSpawner to destroy unit visuals on death.
   /// Returns the GameObject (caller should Destroy it) and frees the slot.
   /// </summary>
   public GameObject GetAndRemoveUnitVisual(int unitId)
   {
       if (!visualsByUnitId.TryGetValue(unitId, out GameObject go))
       {
           return null;
       }


       // Free the slot
       if (slotByUnitId.TryGetValue(unitId, out SlotInfo slot))
       {
           FreeSlot(slot.unitType, slot.side, slot.slotIndex);
           slotByUnitId.Remove(unitId);
       }


       // Remove from tracking
       visualsByUnitId.Remove(unitId);
       transformsByUnitId.Remove(unitId);


       Debug.Log($"[FieldSpawner] GetAndRemoveUnitVisual: removed unit {unitId}");


       return go;
   }


   /// <summary>
   /// Destroys a unit visual immediately and frees its slot.
   /// Convenience method that handles the Destroy call.
   /// </summary>
   public void DestroyUnitVisual(int unitId)
   {
       GameObject go = GetAndRemoveUnitVisual(unitId);
       if (go != null)
       {
           Destroy(go);
       }
       healthBarsByUnitId.Remove(unitId);

   }

   /// <summary>
   /// Clears all spawned unit visuals and resets all tracking data.
   /// Call this when restarting a battle.
   /// </summary>
   public void ClearAllVisuals()
   {
       Debug.Log("[FieldSpawner] Clearing all unit visuals...");

       // Destroy all spawned GameObjects
       foreach (var kvp in visualsByUnitId)
       {
           if (kvp.Value != null)
           {
               Destroy(kvp.Value);
           }
       }

       // Clear all tracking dictionaries
       visualsByUnitId.Clear();
       transformsByUnitId.Clear();
       slotByUnitId.Clear();
       healthBarsByUnitId.Clear();
       reservedSlots.Clear();

       // Clear all occupied slot sets
       occupiedSlotsInfA.Clear();
       occupiedSlotsVehA.Clear();
       occupiedSlotsAirA.Clear();
       occupiedSlotsRangedA.Clear();
       occupiedSlotsInfB.Clear();
       occupiedSlotsVehB.Clear();
       occupiedSlotsAirB.Clear();
       occupiedSlotsRangedB.Clear();

       // Reset sorting order
       nextSortingOrder = 10;

       Debug.Log("[FieldSpawner] All visuals cleared.");
   }


 




/// <summary>
/// Gets the world position where the NEXT unit of this type would spawn,
/// WITHOUT actually spawning anything. Used by CardFlightManager to know
/// where to fly the card before the unit is spawned.
/// </summary>
public Vector3 GetNextSpawnPositionPreview(UnitType cardType, PlayerSide side, Player player = null)
{
UnitType spawnRowType = GetDefaultRowForCardType(cardType);




   Transform[] row = GetRowArray(spawnRowType, side);
   HashSet<int> occupied = GetOccupiedSlotsSet(spawnRowType, side);


   if (row == null || row.Length == 0)
   {
       Debug.LogWarning($"[FieldSpawner] No row configured for {spawnRowType} / {side}");
       return Vector3.zero;
   }


   // Find first unoccupied slot
   for (int i = 0; i < row.Length; i++)
   {
       if (!occupied.Contains(i) && row[i] != null)
       {
           return row[i].position;
       }
   }


   // No free slots - return center of row as fallback
   Debug.LogWarning($"[FieldSpawner] No available slot preview for {side} / {spawnRowType} (row full)");
   return row[row.Length / 2].position;
}
   /// <summary>
   /// DEPRECATED: Use SyncToBoard instead, which automatically frees slots.
   /// This is kept for backwards compatibility but does nothing.
   /// </summary>
   public void ResetIndices()
   {
       Debug.LogWarning("[FieldSpawner] ResetIndices() is deprecated - slots are now freed automatically in SyncToBoard()");
   }
/// <summary>
/// Gets which spawn row a unit type should use based on current player state.
/// This accounts for row swaps during rearrangement.
/// </summary>
/// <param name="cardType">The card's unit type</param>
/// <param name="side">Which player</param>
/// <param name="player">Player object (to check what's actually in each row)</param>
/// <returns>The UnitType that corresponds to the correct spawn row</returns>
public UnitType GetSpawnRowForCardType(UnitType cardType, PlayerSide side, Player player)
{
   if (player == null)
   {
       // Fallback to default mapping
       return GetDefaultRowForCardType(cardType);
   }


   // IMPORTANT:
   // Do NOT infer swap-state from units currently in rows (rows can be empty).
   // Instead, rely on the player's tracked layout:
   // - player.InfantryRow / player.VehicleRow / player.AirRow are physical RowType positions.
   //
   // This method returns which *spawn row array* to use:
   // - UnitType.Infantry  -> use infantryRowA/B (physical FRONT row)
   // - UnitType.Vehicle   -> use vehicleRowA/B  (physical MIDDLE row)
   // - UnitType.Air       -> use airRowA/B      (physical BACK row)


   // Resolve which physical row this card's group should spawn into.
   RowType physicalRow;
   if (cardType == UnitType.Ranged && Battlefield.IsRangedSeparate)
   {
       // On GroundOnly, Ranged has its own row (uses Back/Air position)
       physicalRow = player.AirRow;
   }
   else
   {
       physicalRow = cardType switch
       {
           UnitType.Infantry => player.InfantryRow,
           UnitType.Vehicle => player.VehicleRow,
           UnitType.Ranged => player.VehicleRow,   // Standard mode: shares Vehicle row
           UnitType.Air => player.AirRow,
           _ => player.InfantryRow
       };
   }


   // Convert the physical RowType to the appropriate spawn row array key.
   // Special case: Ranged on GroundOnly uses dedicated ranged spawn points
   if (cardType == UnitType.Ranged && Battlefield.IsRangedSeparate)
   {
       return UnitType.Ranged;
   }

   return physicalRow switch
   {
       RowType.Front => UnitType.Infantry,
       RowType.Middle => UnitType.Vehicle,
       RowType.Back => UnitType.Air,
       _ => UnitType.Infantry
   };
}


/// <summary>
/// Gets the default spawn row for a card type (before any swaps).
/// </summary>
/// <summary>
/// Gets which spawn row a card type should use.
/// Respects battlefield configuration for row layout.
///
/// STANDARD MODE:
/// - Infantry → Infantry row (Front)
/// - Vehicle → Vehicle row (Middle)
/// - Ranged → Vehicle row (Middle, shared)
/// - Air → Air row (Back)
///
/// GROUND-ONLY MODE:
/// - Infantry → Infantry row (Front)
/// - Vehicle → Vehicle row (Middle)
/// - Ranged → Ranged row (Back, dedicated spawn points)
/// - Air → (blocked, but returns Air row as fallback)
/// </summary>
private UnitType GetDefaultRowForCardType(UnitType cardType)
{
    // Check battlefield config for row overrides
    if (Battlefield.IsRangedSeparate && cardType == UnitType.Ranged)
    {
        // On GroundOnly, Ranged uses dedicated ranged spawn points
        return UnitType.Ranged;
    }

    return cardType switch
    {
        UnitType.Infantry => UnitType.Infantry,
        UnitType.Vehicle => UnitType.Vehicle,
        UnitType.Ranged => UnitType.Vehicle,  // Standard mode: shares with Vehicle
        UnitType.Air => UnitType.Air,
        _ => UnitType.Infantry
    };
}


/// <summary>
/// Gets which logical row a card type should go to.
/// Respects battlefield configuration.
/// </summary>
private RowType GetLogicalRowForCardType(UnitType cardType)
{
   // On GroundOnly, Ranged gets its own row (Back)
   if (cardType == UnitType.Ranged && Battlefield.IsRangedSeparate)
   {
       return RowType.Back;
   }

   return cardType switch
   {
       UnitType.Infantry => RowType.Front,
       UnitType.Vehicle => RowType.Middle,
       UnitType.Ranged => RowType.Middle,  // Standard mode: shares with Vehicle
       UnitType.Air => RowType.Back,
       _ => RowType.Front
   };
}
   // ========================================
   // PRIVATE HELPERS - IMPROVED SLOT MANAGEMENT
   // ========================================
private Transform[] GetPhysicalRowArray(PlayerSide side, RowType row)
{
   // IMPORTANT: these arrays are PHYSICAL rows in the scene:
   // infantryRowX = physical FRONT, vehicleRowX = physical MIDDLE, airRowX = physical BACK
   if (side == PlayerSide.A)
   {
       return row switch
       {
           RowType.Front => infantryRowA,
           RowType.Middle => vehicleRowA,
           RowType.Back => airRowA,
           _ => infantryRowA
       };
   }
   else
   {
       return row switch
       {
           RowType.Front => infantryRowB,
           RowType.Middle => vehicleRowB,
           RowType.Back => airRowB,
           _ => infantryRowB
       };
   }
}
   private void RegisterSpawn(int unitId, GameObject go, UnitType unitType, PlayerSide side, int slotIndex)
   {
       // Safety check: if somehow we reuse an ID, clean up old visual
       if (visualsByUnitId.TryGetValue(unitId, out var existing) && existing != null)
           Destroy(existing);


       visualsByUnitId[unitId] = go;
       if (go != null)
           transformsByUnitId[unitId] = go.transform;

       // Set sorting order for visual layering (later units appear in front)
       if (go != null)
       {
           SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
           if (sr != null)
           {
               sr.sortingOrder = nextSortingOrder;
               nextSortingOrder++;
           }
       }

       // Remember which slot this unit is using
       slotByUnitId[unitId] = new SlotInfo
       {
           unitType = unitType,
           side = side,
           slotIndex = slotIndex
       };


       // Slot should already be marked occupied from reservation or direct spawn
       // But mark again just in case
       MarkSlotOccupied(unitType, side, slotIndex);


       // Make unit clickable
var handle = go.GetComponent<UnitWorldHandle>();
if (handle == null) handle = go.AddComponent<UnitWorldHandle>();
handle.Init(unitId, side, unitType);

// Cache optional WorldHealthBar (if prefab has one)
var hb = go.GetComponentInChildren<WorldHealthBar>(true);
if (hb != null)
    healthBarsByUnitId[unitId] = hb;
else
    healthBarsByUnitId.Remove(unitId); // keep clean if prefab lacks bar

   }


   private void MarkSlotOccupied(UnitType type, PlayerSide side, int index)
   {
       HashSet<int> slots = GetOccupiedSlotsSet(type, side);
       slots.Add(index);
   }


   private void FreeSlot(UnitType type, PlayerSide side, int index)
   {
       HashSet<int> slots = GetOccupiedSlotsSet(type, side);
       slots.Remove(index);
       Debug.Log($"[FieldSpawner] Freed slot: {side} {type} slot {index}");
   }


   private HashSet<int> GetOccupiedSlotsSet(UnitType type, PlayerSide side)
   {
       if (side == PlayerSide.A)
       {
           switch (type)
           {
               case UnitType.Infantry: return occupiedSlotsInfA;
               case UnitType.Vehicle: return occupiedSlotsVehA;
               case UnitType.Ranged:
                   // On GroundOnly, Ranged has its own slots; otherwise shares with Vehicle
                   return Battlefield.IsRangedSeparate ? occupiedSlotsRangedA : occupiedSlotsVehA;
               case UnitType.Air: return occupiedSlotsAirA;
           }
       }
       else
       {
           switch (type)
           {
               case UnitType.Infantry: return occupiedSlotsInfB;
               case UnitType.Vehicle: return occupiedSlotsVehB;
               case UnitType.Ranged:
                   // On GroundOnly, Ranged has its own slots; otherwise shares with Vehicle
                   return Battlefield.IsRangedSeparate ? occupiedSlotsRangedB : occupiedSlotsVehB;
               case UnitType.Air: return occupiedSlotsAirB;
           }
       }

       Debug.LogError($"[FieldSpawner] Unknown type/side: {type} / {side}");
       return new HashSet<int>();
   }
// ========================================
// PUBLIC API - SLOT REBUILD (DESYNC FIX)
// ========================================


/// <summary>
/// Rebuilds occupied-slot tracking by looking at CURRENT unit visual positions on the field.
/// This is a safety net for cases where rearrangement / swaps / deaths caused internal slot tracking to drift.
///
/// Call this at phase boundaries (start of Buy, start of Combat, etc.) before reserving/spawning.
/// </summary>
/// <param name="clearReservations">If true, clears any outstanding slot reservations first.</param>
/// <param name="snapUnitsToSlots">If true, teleports visuals to their detected nearest slot (hard resync).</param>
public void RebuildSlotOccupancyFromReality(bool clearReservations = true, bool snapUnitsToSlots = false)
{
   if (clearReservations)
       reservedSlots.Clear();


   // 1) Clear occupied slot sets
   occupiedSlotsInfA.Clear();
   occupiedSlotsVehA.Clear();
   occupiedSlotsAirA.Clear();
   occupiedSlotsRangedA.Clear();
   occupiedSlotsInfB.Clear();
   occupiedSlotsVehB.Clear();
   occupiedSlotsAirB.Clear();
   occupiedSlotsRangedB.Clear();


   // 2) Remove stale entries where visuals were destroyed
   var toRemove = new List<int>();
   foreach (var kvp in visualsByUnitId)
   {
       if (kvp.Value == null)
           toRemove.Add(kvp.Key);
   }


   foreach (int unitId in toRemove)
   {
       visualsByUnitId.Remove(unitId);
       transformsByUnitId.Remove(unitId);
       slotByUnitId.Remove(unitId);
   }


   // 3) Build candidate slot lists per side (LAYOUT-AWARE)
var slotsA = new List<(UnitType type, int index, Transform t)>();
var slotsB = new List<(UnitType type, int index, Transform t)>();


// Track which slots have already been claimed during rebuild
var claimed = new HashSet<(PlayerSide side, UnitType type, int index)>();


// Picks the nearest unclaimed slot on the given side
bool TryPickNearest(
   PlayerSide side,
   List<(UnitType type, int index, Transform t)> slots,
   Vector3 unitPosition,
   out UnitType typeChosen,
   out int indexChosen,
   out Transform slotChosen)
{
   float bestDist = float.MaxValue;
   typeChosen = UnitType.Infantry;
   indexChosen = -1;
   slotChosen = null;


   foreach (var s in slots)
   {
       if (claimed.Contains((side, s.type, s.index)))
           continue;


       if (s.t == null)
           continue;


       float d = Vector3.SqrMagnitude(s.t.position - unitPosition);
       if (d < bestDist)
       {
           bestDist = d;
           typeChosen = s.type;
           indexChosen = s.index;
           slotChosen = s.t;
       }
   }


   return slotChosen != null;
}



void AddSlots(Transform[] row, UnitType semanticType, PlayerSide side)
{
   if (row == null) return;
   for (int i = 0; i < row.Length; i++)
   {
       if (row[i] == null) continue;


       // IMPORTANT: When rebuilding, "semanticType" is what this PHYSICAL row currently hosts
       if (side == PlayerSide.A) slotsA.Add((semanticType, i, row[i]));
       else slotsB.Add((semanticType, i, row[i]));
   }
}


// Determine current semantic type hosted by each PHYSICAL row, using rowLayoutMap.
UnitType A_FrontType  = RowTypeToUnitType(PlayerSide.A, RowType.Front);
UnitType A_MiddleType = RowTypeToUnitType(PlayerSide.A, RowType.Middle);
UnitType A_BackType   = RowTypeToUnitType(PlayerSide.A, RowType.Back);


UnitType B_FrontType  = RowTypeToUnitType(PlayerSide.B, RowType.Front);
UnitType B_MiddleType = RowTypeToUnitType(PlayerSide.B, RowType.Middle);
UnitType B_BackType   = RowTypeToUnitType(PlayerSide.B, RowType.Back);


// PHYSICAL rows in the scene:
// infantryRowX = physical FRONT
// vehicleRowX  = physical MIDDLE
// airRowX      = physical BACK (standard mode)
// rangedRowX   = physical BACK (ground-only mode, replaces air)
AddSlots(infantryRowA, A_FrontType,  PlayerSide.A);
AddSlots(vehicleRowA,  A_MiddleType, PlayerSide.A);
AddSlots(airRowA,      A_BackType,   PlayerSide.A);
// Include ranged slots if they exist (for GroundOnly battlefields)
if (Battlefield.IsRangedSeparate && rangedRowA != null)
    AddSlots(rangedRowA, UnitType.Ranged, PlayerSide.A);


AddSlots(infantryRowB, B_FrontType,  PlayerSide.B);
AddSlots(vehicleRowB,  B_MiddleType, PlayerSide.B);
AddSlots(airRowB,      B_BackType,   PlayerSide.B);
// Include ranged slots if they exist (for GroundOnly battlefields)
if (Battlefield.IsRangedSeparate && rangedRowB != null)
    AddSlots(rangedRowB, UnitType.Ranged, PlayerSide.B);




  // 4) Reassign each visual to nearest slot
foreach (var kvp in transformsByUnitId)
{
   int unitId = kvp.Key;
   Transform unitTf = kvp.Value;
   if (unitTf == null) continue;


   // Prefer last-known side if available
   PlayerSide preferredSide = PlayerSide.A;
   if (slotByUnitId.TryGetValue(unitId, out var oldSlot))
       preferredSide = oldSlot.side;


   PlayerSide chosenSide = preferredSide;


   // Initialize defaults to satisfy C# definite assignment rules
   UnitType typeChosen = UnitType.Infantry;
   int indexChosen = -1;
   Transform slotChosen = null;


   bool picked =
       (preferredSide == PlayerSide.A && TryPickNearest(PlayerSide.A, slotsA, unitTf.position, out typeChosen, out indexChosen, out slotChosen)) ||
       (preferredSide == PlayerSide.B && TryPickNearest(PlayerSide.B, slotsB, unitTf.position, out typeChosen, out indexChosen, out slotChosen));


   if (!picked)
   {
       // Fall back to the other side
       if (preferredSide == PlayerSide.A)
       {
           chosenSide = PlayerSide.B;
           picked = TryPickNearest(PlayerSide.B, slotsB, unitTf.position, out typeChosen, out indexChosen, out slotChosen);
       }
       else
       {
           chosenSide = PlayerSide.A;
           picked = TryPickNearest(PlayerSide.A, slotsA, unitTf.position, out typeChosen, out indexChosen, out slotChosen);
       }
   }


   if (!picked)
   {
       Debug.LogWarning($"[FieldSpawner] RebuildSlotOccupancyFromReality: No available slots found for unit {unitId}. Leaving untracked.");
       continue;
   }


   claimed.Add((chosenSide, typeChosen, indexChosen));
   MarkSlotOccupied(typeChosen, chosenSide, indexChosen);


   slotByUnitId[unitId] = new SlotInfo
   {
       unitType = typeChosen,
       side = chosenSide,
       slotIndex = indexChosen
   };


   if (snapUnitsToSlots && slotChosen != null)
   {
       unitTf.position = slotChosen.position;
       unitTf.rotation = slotChosen.rotation;
   }
}




   // 5) Re-apply reservations (only matters if clearReservations == false)
   foreach (var kvp in reservedSlots)
   {
       var r = kvp.Value;
       MarkSlotOccupied(r.unitType, r.side, r.slotIndex);
   }


   Debug.Log("[FieldSpawner] Rebuilt slot occupancy from reality.");
}


   private Transform GetNextAvailableSlot(UnitType type, PlayerSide side, out int slotIndex)
   {
       Transform[] row = GetRowArray(type, side);
       HashSet<int> occupied = GetOccupiedSlotsSet(type, side);


       // Find first unoccupied slot
       for (int i = 0; i < row.Length; i++)
       {
           if (!occupied.Contains(i))
           {
               slotIndex = i;
               return row[i];
           }
       }


       // No free slots
       slotIndex = -1;
       return null;
   }


  private Transform[] GetRowArray(UnitType type, PlayerSide side)
{
   // Handle Ranged specially based on battlefield mode
   if (type == UnitType.Ranged)
   {
       if (Battlefield.IsRangedSeparate)
       {
           // GroundOnly mode: Ranged uses dedicated ranged spawn points
           return side == PlayerSide.A ? rangedRowA : rangedRowB;
       }
       else
       {
           // Standard mode: Ranged shares Vehicle row
           type = UnitType.Vehicle;
       }
   }

   // Find which PHYSICAL row currently hosts this type
   // using rowLayoutMap (RowType -> UnitType)
   if (rowLayoutMap.TryGetValue(side, out var map))
   {
       foreach (var kvp in map) // kvp.Key = RowType, kvp.Value = UnitType hosted there
       {
           if (kvp.Value == type)
           {
               return GetPhysicalRowArray(side, kvp.Key);
           }
       }
   }


   // fallback to default physical mapping
   return type switch
   {
       UnitType.Infantry => GetPhysicalRowArray(side, RowType.Front),
       UnitType.Vehicle  => GetPhysicalRowArray(side, RowType.Middle),
       UnitType.Air      => GetPhysicalRowArray(side, RowType.Back),
       _ => GetPhysicalRowArray(side, RowType.Front)
   };
}


/// <summary>
/// Initialize the world HP bar for a unit (full at spawn / battle start).
/// Safe to call even if the prefab has no bar.
/// </summary>
public void InitUnitHealthBar(int unitId, int maxHp, int currentHp)
{
    if (unitId < 0) return;

    if (!healthBarsByUnitId.TryGetValue(unitId, out var hb) || hb == null)
    {
        if (visualsByUnitId.TryGetValue(unitId, out var go) && go != null)
        {
            hb = go.GetComponentInChildren<WorldHealthBar>(true);
            if (hb != null) healthBarsByUnitId[unitId] = hb;
        }
    }

    if (hb != null)
        hb.Init(maxHp, currentHp);
}

/// <summary>
/// Update the HP bar to a new HP value. Animates by default.
/// Called during combat playback to show HP changes in real-time.
/// </summary>
public void UpdateUnitHealthBar(int unitId, int maxHp, int newCurrentHp, bool animate = true)
{
    if (unitId < 0) return;

    if (!healthBarsByUnitId.TryGetValue(unitId, out var hb) || hb == null)
    {
        // lazy-resolve once
        if (visualsByUnitId.TryGetValue(unitId, out var go) && go != null)
        {
            hb = go.GetComponentInChildren<WorldHealthBar>(true);
            if (hb != null) healthBarsByUnitId[unitId] = hb;
        }
    }

    if (hb == null) return;

    // If maxHp changed (shouldn't happen for units, but good safety check),
    // reinitialize the bar. Otherwise just update the current HP.
    if (hb.MaxHp != maxHp)
    {
        hb.Init(maxHp, newCurrentHp);
    }
    else
    {
        // Update the bar's HP value with or without animation
        if (animate)
            hb.AnimateTo(newCurrentHp); // smooth tween to new HP
        else
            hb.SetFillInstant(newCurrentHp); // instant update to new HP
    }
}

/// <summary>
/// Triggers damage visual effects on a unit (flash, shake).
/// Call this when a unit takes damage during combat.
/// </summary>
/// <param name="unitId">Unit instance ID</param>
/// <param name="damageAmount">Amount of damage taken (for screen shake intensity)</param>
public void PlayUnitDamageEffects(int unitId, int damageAmount = 0)
{
    if (unitId < 0) return;

    // Get the unit visual
    if (!visualsByUnitId.TryGetValue(unitId, out GameObject visual) || visual == null)
        return;

    // Trigger unit damage effects (flash + shake)
    UnitVisualEffects vfx = visual.GetComponentInChildren<UnitVisualEffects>();
    if (vfx != null)
    {
        vfx.PlayDamageEffects();
    }

    // Camera shake for unit damage removed - only triggers on commander damage now
}

/// <summary>
/// Triggers attack visual effects (muzzle flash, projectile) when a unit attacks.
/// Call this from CombatPlayback when a unit deals damage.
/// </summary>
/// <param name="attackerUnitId">ID of the attacking unit</param>
/// <param name="targetUnitId">ID of the target unit (for projectile direction)</param>
public void PlayUnitAttackEffects(int attackerUnitId, int targetUnitId)
{
    // Debug.Log($"[ATTACK] PlayUnitAttackEffects called - Attacker ID: {attackerUnitId}, Target ID: {targetUnitId}. " +
    //           $"Tracked visuals: {visualsByUnitId.Count}, IDs: [{string.Join(", ", visualsByUnitId.Keys)}]");

    if (attackerUnitId < 0)
    {
        Debug.LogWarning($"[ATTACK] Invalid attacker ID: {attackerUnitId}");
        return;
    }

    // Get attacker visual
    if (!visualsByUnitId.TryGetValue(attackerUnitId, out GameObject attackerVisual) || attackerVisual == null)
    {
        Debug.LogWarning($"[ATTACK] Could not find visual for attacker ID {attackerUnitId}. " +
                        $"Available IDs in dictionary: [{string.Join(", ", visualsByUnitId.Keys)}]");
        return;
    }

    // Debug.Log($"[ATTACK] Found attacker visual: {attackerVisual.name}");

    // Get target position
    Vector3 targetPos = Vector3.zero;
    if (targetUnitId >= 0)
    {
        targetPos = GetUnitPosition(targetUnitId);
        // Debug.Log($"[ATTACK] Target position: {targetPos}");
    }
    else
    {
        // No specific target - fire towards enemy side based on attacker position
        Vector3 attackerPos = attackerVisual.transform.position;
        float direction = attackerPos.x < 0 ? 1f : -1f; // Left side fires right, right side fires left
        targetPos = attackerPos + new Vector3(direction * 10f, 0f, 0f);
        // Debug.Log($"[ATTACK] No specific target - firing towards enemy side at {targetPos}");
    }

    // Trigger attack effects
    UnitAttackEffects attackFx = attackerVisual.GetComponentInChildren<UnitAttackEffects>();
    if (attackFx != null)
    {
        // Debug.Log($"[ATTACK] Found UnitAttackEffects component on {attackerVisual.name}, calling PlayAttackEffect()");
        attackFx.PlayAttackEffect(targetPos);
    }
    else
    {
        Debug.LogWarning($"[ATTACK] NO UnitAttackEffects component found on {attackerVisual.name}! Add the component to your unit prefab.");
    }
}

/// <summary>
/// Triggers attack visual effects (muzzle flash, projectile) when a unit attacks a specific position.
/// Use this overload when targeting a position directly (e.g., commander display when no enemy units).
/// </summary>
/// <param name="attackerUnitId">ID of the attacking unit</param>
/// <param name="targetPosition">World position to fire at</param>
public void PlayUnitAttackEffectsAtPosition(int attackerUnitId, Vector3 targetPosition)
{
    // Debug.Log($"[ATTACK] PlayUnitAttackEffectsAtPosition called - Attacker ID: {attackerUnitId}, Target Position: {targetPosition}");

    if (attackerUnitId < 0)
    {
        Debug.LogWarning($"[ATTACK] Invalid attacker ID: {attackerUnitId}");
        return;
    }

    // Get attacker visual
    if (!visualsByUnitId.TryGetValue(attackerUnitId, out GameObject attackerVisual) || attackerVisual == null)
    {
        Debug.LogWarning($"[ATTACK] Could not find visual for attacker ID {attackerUnitId}. " +
                        $"Available IDs in dictionary: [{string.Join(", ", visualsByUnitId.Keys)}]");
        return;
    }

    // Debug.Log($"[ATTACK] Found attacker visual: {attackerVisual.name}, firing at position {targetPosition}");

    // Trigger attack effects
    UnitAttackEffects attackFx = attackerVisual.GetComponentInChildren<UnitAttackEffects>();
    if (attackFx != null)
    {
        attackFx.PlayAttackEffect(targetPosition);
    }
    else
    {
        Debug.LogWarning($"[ATTACK] NO UnitAttackEffects component found on {attackerVisual.name}! Add the component to your unit prefab.");
    }
}


   private void AddAlive(Player p, HashSet<int> alive)
   {
       if (p == null) return;


       foreach (var rowKvp in p.Rows)
       {
           foreach (var u in rowKvp.Value.Units)
           {
               if (u != null && u.IsAlive)
                   alive.Add(u.InstanceId);
           }
       }
   }
public bool TryGetSlotInfo(int unitId, out UnitType laneType, out PlayerSide side, out int slotIndex)
{
   laneType = UnitType.Infantry;
   side = PlayerSide.A;
   slotIndex = -1;


   if (!slotByUnitId.TryGetValue(unitId, out var info))
       return false;


   laneType = info.unitType;
   side = info.side;
   slotIndex = info.slotIndex;
   return true;
}


public Transform[] GetLaneSlots(UnitType laneType, PlayerSide side)
{
   return GetRowArray(laneType, side); // you already have this private helper in FieldSpawner
}






public int GetRowCapacity(UnitType spawnRowType, PlayerSide side)
{
   Transform[] row;

   if (spawnRowType == UnitType.Ranged)
   {
       // Ranged uses dedicated row on GroundOnly, shares Vehicle on Standard
       if (Battlefield.IsRangedSeparate)
           row = (side == PlayerSide.A) ? rangedRowA : rangedRowB;
       else
           row = (side == PlayerSide.A) ? vehicleRowA : vehicleRowB;
   }
   else
   {
       row = spawnRowType switch
       {
           UnitType.Infantry => (side == PlayerSide.A) ? infantryRowA : infantryRowB,
           UnitType.Vehicle  => (side == PlayerSide.A) ? vehicleRowA  : vehicleRowB,
           UnitType.Air      => (side == PlayerSide.A) ? airRowA      : airRowB,
           _ => null
       };
   }

   return row?.Length ?? 0;
}


   /// <summary>
   /// Applies scale from spawn point to spawned unit instance (non-destructive to prefab).
   /// Checks if the spawn point has SpawnPointScaleData component and applies the scale.
   /// </summary>
   private void ApplySpawnPointScale(GameObject spawnedUnit, Transform spawnPoint)
   {
       if (spawnedUnit == null || spawnPoint == null) return;

       SpawnPointScaleData scaleData = spawnPoint.GetComponent<SpawnPointScaleData>();
       if (scaleData != null && scaleData.scaleMultiplier != 1.0f)
       {
           spawnedUnit.transform.localScale = Vector3.one * scaleData.scaleMultiplier;
           Debug.Log($"[FieldSpawner] Applied scale {scaleData.scaleMultiplier}x to spawned unit at {spawnPoint.name}");
       }
   }

   /// <summary>
   /// Finds the Transform of a slot by its type, side, and index.
   /// Used to retrieve spawn point data for reserved slots.
   /// </summary>
   private Transform FindSlotTransform(UnitType type, PlayerSide side, int slotIndex)
   {
       Transform[] row = GetRowArray(type, side);
       if (row != null && slotIndex >= 0 && slotIndex < row.Length)
       {
           return row[slotIndex];
       }
       return null;
   }

   private void SetVisibility(GameObject go, bool visible)
   {
       if (go == null) return;


       // Try multiple approaches to hide/show


       // 1. SpriteRenderer
       SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
       if (sr != null)
       {
           sr.enabled = visible;
       }


       // 2. MeshRenderer
       MeshRenderer mr = go.GetComponent<MeshRenderer>();
       if (mr != null)
       {
           mr.enabled = visible;
       }


       // 3. All child renderers
       Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
       foreach (var r in renderers)
       {
           r.enabled = visible;
       }


       // 4. CanvasGroup (if it's a UI element somehow)
       CanvasGroup cg = go.GetComponent<CanvasGroup>();
       if (cg != null)
       {
           cg.alpha = visible ? 1f : 0f;
       }
   }


   // ========================================
   // DEBUG HELPERS
   // ========================================


   public void DebugPrintSlotStatus()
   {
       Debug.Log($"[FieldSpawner] Slot Status (RangedSeparate={Battlefield.IsRangedSeparate}):");
       Debug.Log($"  A Infantry: {string.Join(",", occupiedSlotsInfA)} / {infantryRowA?.Length ?? 0}");
       Debug.Log($"  A Vehicle:  {string.Join(",", occupiedSlotsVehA)} / {vehicleRowA?.Length ?? 0}");
       Debug.Log($"  A Air:      {string.Join(",", occupiedSlotsAirA)} / {airRowA?.Length ?? 0}");
       Debug.Log($"  A Ranged:   {string.Join(",", occupiedSlotsRangedA)} / {rangedRowA?.Length ?? 0}");
       Debug.Log($"  B Infantry: {string.Join(",", occupiedSlotsInfB)} / {infantryRowB?.Length ?? 0}");
       Debug.Log($"  B Vehicle:  {string.Join(",", occupiedSlotsVehB)} / {vehicleRowB?.Length ?? 0}");
       Debug.Log($"  B Air:      {string.Join(",", occupiedSlotsAirB)} / {airRowB?.Length ?? 0}");
       Debug.Log($"  B Ranged:   {string.Join(",", occupiedSlotsRangedB)} / {rangedRowB?.Length ?? 0}");
       Debug.Log($"  Reservations: {reservedSlots.Count}");
   }
}

