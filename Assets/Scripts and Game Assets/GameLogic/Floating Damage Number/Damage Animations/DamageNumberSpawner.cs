using UnityEngine;


// REMOVED: DamageType enum (use DamageNumberType from FloatingDamageNumber.cs instead)


public class DamageNumberSpawner : MonoBehaviour
{
   [Header("Prefab Reference")]
   [Tooltip("Drag your FloatingDamageNumber prefab here.")]
   public GameObject damageNumberPrefab;

   [Header("Ability Display Settings")]
   [Tooltip("ScriptableObject containing display text for each ability type. Create via: Right-click > Create > Army Card > Ability Display Settings")]
   public AbilityDisplaySettings abilityDisplaySettings;

   [Header("Spawn Settings")]
   [Tooltip("Vertical offset above unit position (so text doesn't spawn inside unit).")]
   public float spawnHeightOffset = 1f;

   [Tooltip("If true, damage numbers spawn. Turn off for performance testing.")]
   public bool enableDamageNumbers = true;

   private static DamageNumberSpawner instance;


   private void Awake()
   {
       if (instance == null)
       {
           instance = this;
       }
       else
       {
           Debug.LogWarning("[DamageNumberSpawner] Multiple spawners detected! Using first one.");
       }
   }
public static void SpawnOnUnit(
   int amount,
   Transform unitVisual,
   DamageNumberType type = DamageNumberType.Normal,
   string prefix = "",
   string suffix = "")
{
   if (instance == null || !instance.enableDamageNumbers || instance.damageNumberPrefab == null || unitVisual == null)
       return;


   // Find a sprite/renderer to get a good top point
   var sr = unitVisual.GetComponentInChildren<SpriteRenderer>();
   Vector3 worldPos = unitVisual.position;


   if (sr != null)
       worldPos = sr.bounds.center + Vector3.up * (sr.bounds.extents.y + instance.spawnHeightOffset);
   else
       worldPos = unitVisual.position + Vector3.up * instance.spawnHeightOffset;


   worldPos += Vector3.forward * -0.5f; // keep on top
   GameObject go = Instantiate(instance.damageNumberPrefab, worldPos, Quaternion.identity);


   var damageNum = go.GetComponent<FloatingDamageNumber>();
   if (damageNum != null) damageNum.Initialize(amount, type, prefix, suffix);
   else Destroy(go);
}


   public static void Spawn(
       int amount,
       Vector3 worldPosition,
       DamageNumberType type = DamageNumberType.Normal,
       string prefix = "",
       string suffix = "")
   {
       if (instance == null || !instance.enableDamageNumbers || instance.damageNumberPrefab == null)
           return;


       Vector3 spawnPos = worldPosition + Vector3.up * instance.spawnHeightOffset;
       spawnPos += Vector3.forward * -0.5f; // Render on top


       GameObject go = Instantiate(instance.damageNumberPrefab, spawnPos, Quaternion.identity);


       FloatingDamageNumber damageNum = go.GetComponent<FloatingDamageNumber>();
       if (damageNum != null)
       {
           damageNum.Initialize(amount, type, prefix, suffix);
       }
       else
       {
           Destroy(go);
       }
   }

   /// <summary>
   /// Spawns ability activation text using AbilityDisplaySettings.
   /// Text, color, and size are configured in the ScriptableObject.
   /// </summary>
   public static void SpawnAbilityText(UnitAbilityType abilityType, Vector3 worldPosition)
   {
       if (instance == null || !instance.enableDamageNumbers || instance.damageNumberPrefab == null)
           return;

       // Get display settings
       string text;
       Color color;
       float fontMultiplier;

       if (instance.abilityDisplaySettings != null)
       {
           text = instance.abilityDisplaySettings.GetDisplayText(abilityType);
           color = instance.abilityDisplaySettings.GetColor(abilityType);
           fontMultiplier = instance.abilityDisplaySettings.GetFontSizeMultiplier(abilityType);
       }
       else
       {
           // Fallback defaults if no settings assigned
           text = "x2 ATK";
           color = new Color(0.3f, 1f, 0.3f);
           fontMultiplier = 1.1f;
           Debug.LogWarning("[DamageNumberSpawner] AbilityDisplaySettings not assigned! Using defaults.");
       }

       Vector3 spawnPos = worldPosition + Vector3.up * instance.spawnHeightOffset;
       spawnPos += Vector3.forward * -0.5f;

       GameObject go = Instantiate(instance.damageNumberPrefab, spawnPos, Quaternion.identity);

       FloatingDamageNumber damageNum = go.GetComponent<FloatingDamageNumber>();
       if (damageNum != null)
       {
           damageNum.InitializeText(text, color, fontMultiplier);
       }
       else
       {
           Destroy(go);
       }
   }

   /// <summary>
   /// Spawns ability activation text on a unit using AbilityDisplaySettings.
   /// </summary>
   public static void SpawnAbilityTextOnUnit(UnitAbilityType abilityType, Transform unitVisual)
   {
       if (instance == null || !instance.enableDamageNumbers || instance.damageNumberPrefab == null || unitVisual == null)
           return;

       // Find a sprite/renderer to get a good top point
       var sr = unitVisual.GetComponentInChildren<SpriteRenderer>();
       Vector3 worldPos = unitVisual.position;

       if (sr != null)
           worldPos = sr.bounds.center + Vector3.up * (sr.bounds.extents.y + instance.spawnHeightOffset);
       else
           worldPos = unitVisual.position + Vector3.up * instance.spawnHeightOffset;

       worldPos += Vector3.forward * -0.5f;

       // Get display settings
       string text;
       Color color;
       float fontMultiplier;

       if (instance.abilityDisplaySettings != null)
       {
           text = instance.abilityDisplaySettings.GetDisplayText(abilityType);
           color = instance.abilityDisplaySettings.GetColor(abilityType);
           fontMultiplier = instance.abilityDisplaySettings.GetFontSizeMultiplier(abilityType);
       }
       else
       {
           text = "x2 ATK";
           color = new Color(0.3f, 1f, 0.3f);
           fontMultiplier = 1.1f;
           Debug.LogWarning("[DamageNumberSpawner] AbilityDisplaySettings not assigned! Using defaults.");
       }

       GameObject go = Instantiate(instance.damageNumberPrefab, worldPos, Quaternion.identity);

       FloatingDamageNumber damageNum = go.GetComponent<FloatingDamageNumber>();
       if (damageNum != null)
       {
           damageNum.InitializeText(text, color, fontMultiplier);
       }
       else
       {
           Destroy(go);
       }
   }
}



