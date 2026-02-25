using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// Editor utility to create all the Doctrine, Perk, and Commander assets.
/// Use: Window → Army Prototype → Create Overworld Assets
/// </summary>
public class OverworldAssetCreator : EditorWindow
{
    [MenuItem("Window/Army Prototype/Create Overworld Assets")]
    public static void ShowWindow()
    {
        GetWindow<OverworldAssetCreator>("Create Overworld Assets");
    }

    private void OnGUI()
    {
        GUILayout.Label("Overworld Asset Creator", EditorStyles.boldLabel);
        GUILayout.Space(10);

        GUILayout.Label("This will create all Doctrine, Perk, and sample Commander assets.");
        GUILayout.Label("Assets will be created in ScriptableObjects/ folder.");
        GUILayout.Space(20);

        if (GUILayout.Button("Create All Doctrine Assets (15)", GUILayout.Height(30)))
        {
            CreateAllDoctrines();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Create All Perk Assets (8)", GUILayout.Height(30)))
        {
            CreateAllPerks();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Create Sample Commander Assets (8)", GUILayout.Height(30)))
        {
            CreateSampleCommanders();
        }

        GUILayout.Space(20);

        if (GUILayout.Button("Create Shop Config Asset", GUILayout.Height(30)))
        {
            CreateShopConfig();
        }

        GUILayout.Space(10);

        if (GUILayout.Button("Create Player Progress Asset", GUILayout.Height(30)))
        {
            CreatePlayerProgress();
        }
    }

    private static void CreateAllDoctrines()
    {
        string folder = "Assets/Scripts and Game Assets/ScriptableObjects/Doctrines";
        EnsureFolder(folder);

        // Define all 15 doctrines from the user's document
        var doctrines = new[]
        {
            ("Military Industrial Complex", "Adds +10 gold at start of each battle", CardRarity.Common, DoctrineEffectType.StartingGoldBonus, 10),
            ("Motorhead", "Reduce cost of vehicles by 1 gold", CardRarity.Common, DoctrineEffectType.ReduceTypeCost, 1),
            ("Birdwatcher", "Reduce cost of aircraft by 1 gold", CardRarity.Common, DoctrineEffectType.ReduceTypeCost, 1),
            ("Merchant of Death", "Spend leftover gold to attack commander", CardRarity.Legendary, DoctrineEffectType.LeftoverGoldDamage, 1),
            ("Salvage Wreckage", "Gain 1 gold for destroyed vehicles (max 10 per battle)", CardRarity.Epic, DoctrineEffectType.GoldOnVehicleDestroyed, 1),
            ("Combined Arms Doctrine", "Vehicles gain +2 ATK when you also have infantry on field", CardRarity.Common, DoctrineEffectType.TypeBonusWhenOtherTypePresent, 2),
            ("Infantry Surge", "Start each battle with 2 infantry on field", CardRarity.Epic, DoctrineEffectType.StartWithUnitsOnField, 2),
            ("Strategic Reserves", "Draw an extra card at beginning of game", CardRarity.Uncommon, DoctrineEffectType.ExtraDrawAtStart, 1),
            ("Reinforcements", "When your commander takes damage, an infantry unit appears (once per battle)", CardRarity.Legendary, DoctrineEffectType.SpawnOnCommanderDamaged, 1),
            ("SOS", "Revive any one unit at beginning of round 3 from discard pile", CardRarity.Rare, DoctrineEffectType.ReviveFromDiscard, 1),
            ("Ranged Warrior", "All ranged units do +3 ATK", CardRarity.Uncommon, DoctrineEffectType.TypeAtkBonus, 3),
            ("Close Combat", "All infantry units do +3 ATK", CardRarity.Uncommon, DoctrineEffectType.TypeAtkBonus, 3),
            ("Iron Skin", "All vehicles do +3 ATK", CardRarity.Uncommon, DoctrineEffectType.TypeAtkBonus, 3),
            ("Death from Above", "All aircraft do +3 ATK", CardRarity.Uncommon, DoctrineEffectType.TypeAtkBonus, 3),
            ("Thick Skin", "Commander receives half damage", CardRarity.Legendary, DoctrineEffectType.CommanderDamageReduction, 50),
        };

        // Target unit types for type-specific doctrines
        var targetTypes = new[]
        {
            UnitType.Infantry, // Military Industrial Complex (not used)
            UnitType.Vehicle,  // Motorhead
            UnitType.Air,      // Birdwatcher
            UnitType.Infantry, // Merchant of Death (not used)
            UnitType.Vehicle,  // Salvage Wreckage
            UnitType.Vehicle,  // Combined Arms (target = Vehicle, required = Infantry)
            UnitType.Infantry, // Infantry Surge
            UnitType.Infantry, // Strategic Reserves (not used)
            UnitType.Infantry, // Reinforcements
            UnitType.Infantry, // SOS (not used - picks any)
            UnitType.Ranged,   // Ranged Warrior
            UnitType.Infantry, // Close Combat
            UnitType.Vehicle,  // Iron Skin
            UnitType.Air,      // Death from Above
            UnitType.Infantry, // Thick Skin (not used)
        };

        int created = 0;
        for (int i = 0; i < doctrines.Length; i++)
        {
            var (name, desc, rarity, effectType, amount) = doctrines[i];

            string safeName = name.Replace(" ", "_");
            string path = $"{folder}/Doctrine_{safeName}.asset";

            if (AssetDatabase.LoadAssetAtPath<DoctrineDefinition>(path) != null)
            {
                Debug.Log($"Skipping existing: {path}");
                continue;
            }

            var asset = ScriptableObject.CreateInstance<DoctrineDefinition>();
            asset.doctrineId = safeName.ToLower();
            asset.displayName = name;
            asset.description = desc;
            asset.rarity = rarity;
            asset.baseCost = GetBaseCostForRarity(rarity);
            asset.effectType = effectType;
            asset.effectAmount = amount;
            asset.targetUnitType = targetTypes[i];

            // Special case for Combined Arms - needs required type
            if (effectType == DoctrineEffectType.TypeBonusWhenOtherTypePresent)
            {
                asset.requiredUnitType = UnitType.Infantry;
            }

            // Special case for SOS - trigger at round 3
            if (name == "SOS")
            {
                asset.triggerRound = 3;
            }

            // Special case for Salvage Wreckage - max 10 per battle
            if (name == "Salvage Wreckage")
            {
                asset.maxTriggersPerBattle = 10;
            }

            AssetDatabase.CreateAsset(asset, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created {created} Doctrine assets in {folder}");
    }

    private static void CreateAllPerks()
    {
        string folder = "Assets/Scripts and Game Assets/ScriptableObjects/Perks";
        EnsureFolder(folder);

        // Define all 8 perks from the user's document
        var perks = new[]
        {
            ("Walls", "Adds wall defense each tier (absorbs damage before commander)", PerkEffectType.WallDefense, 10, UnitType.Infantry),
            ("Extra Lives", "Permanently adds commander health each tier", PerkEffectType.CommanderHP, 10, UnitType.Infantry),
            ("Medals", "Increases ATK and HP of a random unit in your deck by 1", PerkEffectType.RandomUnitStatBoost, 1, UnitType.Infantry),
            ("Stopping Power", "Increase ATK of infantry by 1 each tier", PerkEffectType.TypeAtkBonus, 1, UnitType.Infantry),
            ("Piercing", "Increase ATK of vehicles by 1 each tier", PerkEffectType.TypeAtkBonus, 1, UnitType.Vehicle),
            ("Explosives", "Increase ATK of planes by 1 each tier", PerkEffectType.TypeAtkBonus, 1, UnitType.Air),
            ("Heads Up", "Increase ATK of ranged units by 1 each tier", PerkEffectType.TypeAtkBonus, 1, UnitType.Ranged),
            ("Purse", "Get +1 additional gold each round. Stacks.", PerkEffectType.GoldPerRound, 1, UnitType.Infantry),
        };

        int created = 0;
        foreach (var (name, desc, effectType, amountPerTier, targetType) in perks)
        {
            string safeName = name.Replace(" ", "_");
            string path = $"{folder}/Perk_{safeName}.asset";

            if (AssetDatabase.LoadAssetAtPath<PerkDefinition>(path) != null)
            {
                Debug.Log($"Skipping existing: {path}");
                continue;
            }

            var asset = ScriptableObject.CreateInstance<PerkDefinition>();
            asset.perkId = safeName.ToLower();
            asset.displayName = name;
            asset.description = desc;
            asset.maxTier = 5;
            asset.costPerTier = new int[] { 100, 200, 400, 800, 1600 };
            asset.effectType = effectType;
            asset.effectAmountPerTier = amountPerTier;
            asset.targetUnitType = targetType;

            // Medals can be purchased infinitely
            if (name == "Medals")
            {
                asset.infinitePurchases = true;
                asset.maxTier = 999; // Effectively infinite
            }

            AssetDatabase.CreateAsset(asset, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created {created} Perk assets in {folder}");
    }

    private static void CreateSampleCommanders()
    {
        string folder = "Assets/Scripts and Game Assets/ScriptableObjects/Commanders";
        EnsureFolder(folder);

        // Sample enemy commanders (you can customize these later)
        var commanders = new[]
        {
            ("General Steel", "A ruthless tank commander who favors heavy armor.", 2),
            ("Sky Marshal", "Master of aerial warfare and precision strikes.", 3),
            ("The Swarm", "Overwhelms enemies with sheer numbers of infantry.", 1),
            ("Iron Curtain", "Defensive specialist with impenetrable formations.", 3),
            ("Blitzkrieg", "Lightning-fast attacks leave enemies no time to react.", 4),
            ("The Phantom", "Special ops expert using deception and sabotage.", 4),
            ("Dreadnought", "Slow but unstoppable, crushing all opposition.", 5),
            ("Chaos Lord", "Unpredictable strategies keep enemies guessing.", 5),
        };

        int created = 0;
        foreach (var (name, desc, difficulty) in commanders)
        {
            string safeName = name.Replace(" ", "_");
            string path = $"{folder}/Commander_{safeName}.asset";

            if (AssetDatabase.LoadAssetAtPath<CommanderDefinition>(path) != null)
            {
                Debug.Log($"Skipping existing: {path}");
                continue;
            }

            var asset = ScriptableObject.CreateInstance<CommanderDefinition>();
            asset.displayName = name;
            asset.description = desc;
            asset.difficulty = difficulty;
            // Note: You'll need to assign deck and mapPortrait manually in Inspector

            AssetDatabase.CreateAsset(asset, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created {created} Commander assets in {folder}. Remember to assign decks and portraits!");
    }

    private static void CreateShopConfig()
    {
        string folder = "Assets/Scripts and Game Assets/ScriptableObjects";
        EnsureFolder(folder);

        string path = $"{folder}/ShopConfig.asset";

        if (AssetDatabase.LoadAssetAtPath<ShopInventoryConfig>(path) != null)
        {
            Debug.Log($"ShopConfig already exists at {path}");
            return;
        }

        var asset = ScriptableObject.CreateInstance<ShopInventoryConfig>();
        // Default values are already set in the class

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created ShopConfig at {path}. Remember to populate card/doctrine/perk pools!");
    }

    private static void CreatePlayerProgress()
    {
        string folder = "Assets/Scripts and Game Assets/ScriptableObjects";
        EnsureFolder(folder);

        string path = $"{folder}/PlayerProgress.asset";

        if (AssetDatabase.LoadAssetAtPath<PlayerProgressData>(path) != null)
        {
            Debug.Log($"PlayerProgress already exists at {path}");
            return;
        }

        var asset = ScriptableObject.CreateInstance<PlayerProgressData>();

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Created PlayerProgress at {path}");
    }

    private static int GetBaseCostForRarity(CardRarity rarity)
    {
        return rarity switch
        {
            CardRarity.Common => 75,
            CardRarity.Uncommon => 100,
            CardRarity.Rare => 150,
            CardRarity.Epic => 200,
            CardRarity.Legendary => 300,
            _ => 100
        };
    }

    private static void EnsureFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string parent = Path.GetDirectoryName(path).Replace("\\", "/");
            string folderName = Path.GetFileName(path);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
