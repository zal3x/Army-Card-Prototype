using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

/// <summary>
/// Persistent player data for the overworld meta-game.
/// Manages war funds, card collection, deck, doctrines, and perks.
/// Can be serialized to/from JSON for save/load functionality.
/// </summary>
[CreateAssetMenu(menuName = "Army Prototype/Player Progress Data", fileName = "PlayerProgress")]
public class PlayerProgressData : ScriptableObject
{
    public static PlayerProgressData Instance { get; private set; }

    [Header("Currency")]
    [Tooltip("War Funds earned from battles, spent in War Cabinet")]
    public int warFunds = 0;

    [Header("Card Collection")]
    [Tooltip("All cards the player owns (not in deck)")]
    public List<OwnedCard> ownedCards = new List<OwnedCard>();

    [Header("Current Deck")]
    [Tooltip("Cards currently in the player's deck")]
    public List<OwnedCard> currentDeck = new List<OwnedCard>();

    [Header("Doctrines")]
    [Tooltip("All doctrines the player owns (not equipped)")]
    public List<DoctrineDefinition> ownedDoctrines = new List<DoctrineDefinition>();

    [Tooltip("Currently equipped doctrines (max 5)")]
    public List<DoctrineDefinition> equippedDoctrines = new List<DoctrineDefinition>();
    public const int MaxDoctrines = 5;

    [Header("Perks")]
    [Tooltip("Current tier for each purchased perk")]
    public List<PerkProgress> perkLevels = new List<PerkProgress>();

    [Header("Commander Bonuses (from perks)")]
    [Tooltip("Bonus HP from Extra Lives perk")]
    public int bonusCommanderHP = 0;
    [Tooltip("Wall defense from Walls perk (absorbs damage before commander)")]
    public int wallDefense = 0;
    [Tooltip("Wall defense remaining for current run (resets after battle)")]
    public int currentWallDefense = 0;

    [Header("Deck Rules")]
    public int minDeckSize = 25;
    public int maxDeckSize = 45;
    public int maxUnitCards = 35;
    public int maxSpecialCards = 10;

    private static int nextInstanceId = 1;

    private void OnEnable()
    {
        if (Instance == null)
            Instance = this;
    }

    /// <summary>
    /// Add a new card to the player's collection (owned, not in deck).
    /// </summary>
    public OwnedCard AddCard(UnitCardDefinition definition)
    {
        if (definition == null) return null;

        var owned = new OwnedCard
        {
            instanceId = GenerateInstanceId(),
            definition = definition,
            bonusAtk = 0,
            bonusHp = 0
        };

        ownedCards.Add(owned);
        return owned;
    }

    /// <summary>
    /// Remove a card from the collection by instance ID.
    /// </summary>
    public bool RemoveCard(string instanceId)
    {
        var card = ownedCards.Find(c => c.instanceId == instanceId);
        if (card != null)
        {
            ownedCards.Remove(card);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Move a card from collection to deck.
    /// </summary>
    public bool AddCardToDeck(string instanceId)
    {
        var card = ownedCards.Find(c => c.instanceId == instanceId);
        if (card == null) return false;

        if (!CanAddToDeck(card.definition)) return false;

        ownedCards.Remove(card);
        currentDeck.Add(card);
        return true;
    }

    /// <summary>
    /// Move a card from deck back to collection.
    /// </summary>
    public bool RemoveCardFromDeck(string instanceId)
    {
        var card = currentDeck.Find(c => c.instanceId == instanceId);
        if (card == null) return false;

        currentDeck.Remove(card);
        ownedCards.Add(card);
        return true;
    }

    /// <summary>
    /// Check if a card can be added to the deck based on rules.
    /// </summary>
    public bool CanAddToDeck(UnitCardDefinition definition)
    {
        if (currentDeck.Count >= maxDeckSize) return false;

        int unitCount = currentDeck.Count(c => !c.definition.isSpecialCard);
        int specialCount = currentDeck.Count(c => c.definition.isSpecialCard);

        if (!definition.isSpecialCard && unitCount >= maxUnitCards) return false;
        if (definition.isSpecialCard && specialCount >= maxSpecialCards) return false;

        return true;
    }

    /// <summary>
    /// Check if current deck is valid for battle.
    /// </summary>
    public bool IsDeckValid()
    {
        return currentDeck.Count >= minDeckSize && currentDeck.Count <= maxDeckSize;
    }

    /// <summary>
    /// Get deck stats for UI display.
    /// </summary>
    public (int total, int units, int specials) GetDeckStats()
    {
        int units = currentDeck.Count(c => !c.definition.isSpecialCard);
        int specials = currentDeck.Count(c => c.definition.isSpecialCard);
        return (currentDeck.Count, units, specials);
    }

    /// <summary>
    /// Equip a doctrine (max 5).
    /// </summary>
    public bool EquipDoctrine(DoctrineDefinition doctrine)
    {
        if (doctrine == null) return false;
        if (equippedDoctrines.Count >= MaxDoctrines) return false;
        if (equippedDoctrines.Contains(doctrine)) return false;

        equippedDoctrines.Add(doctrine);
        return true;
    }

    /// <summary>
    /// Unequip a doctrine.
    /// </summary>
    public bool UnequipDoctrine(DoctrineDefinition doctrine)
    {
        return equippedDoctrines.Remove(doctrine);
    }

    /// <summary>
    /// Add a doctrine to the owned collection (purchased but not equipped).
    /// </summary>
    public bool AddDoctrineToCollection(DoctrineDefinition doctrine)
    {
        if (doctrine == null) return false;

        // Check if already owned (either equipped or in collection)
        if (equippedDoctrines.Contains(doctrine) || ownedDoctrines.Contains(doctrine))
            return false;

        ownedDoctrines.Add(doctrine);
        return true;
    }

    /// <summary>
    /// Move doctrine from owned collection to equipped slots.
    /// </summary>
    public bool EquipDoctrineFromCollection(DoctrineDefinition doctrine)
    {
        if (doctrine == null) return false;
        if (!ownedDoctrines.Contains(doctrine)) return false;
        if (equippedDoctrines.Count >= MaxDoctrines) return false;
        if (equippedDoctrines.Contains(doctrine)) return false;

        ownedDoctrines.Remove(doctrine);
        equippedDoctrines.Add(doctrine);
        return true;
    }

    /// <summary>
    /// Move doctrine from equipped back to owned collection.
    /// </summary>
    public bool UnequipDoctrineToCollection(DoctrineDefinition doctrine)
    {
        if (doctrine == null) return false;
        if (!equippedDoctrines.Contains(doctrine)) return false;

        equippedDoctrines.Remove(doctrine);
        ownedDoctrines.Add(doctrine);
        return true;
    }

    /// <summary>
    /// Check if player owns a doctrine (either equipped or in collection).
    /// </summary>
    public bool OwnsDoctrine(DoctrineDefinition doctrine)
    {
        if (doctrine == null) return false;
        return equippedDoctrines.Contains(doctrine) || ownedDoctrines.Contains(doctrine);
    }

    /// <summary>
    /// Get current tier for a perk (0 = not purchased).
    /// </summary>
    public int GetPerkTier(PerkDefinition perk)
    {
        var progress = perkLevels.Find(p => p.perk == perk);
        return progress?.currentTier ?? 0;
    }

    /// <summary>
    /// Upgrade a perk to next tier.
    /// </summary>
    public bool UpgradePerk(PerkDefinition perk)
    {
        if (perk == null) return false;

        var progress = perkLevels.Find(p => p.perk == perk);
        if (progress == null)
        {
            progress = new PerkProgress { perk = perk, currentTier = 0 };
            perkLevels.Add(progress);
        }

        if (progress.currentTier >= perk.maxTier) return false;

        int cost = perk.GetCostForTier(progress.currentTier + 1);
        if (warFunds < cost) return false;

        warFunds -= cost;
        progress.currentTier++;

        // Apply immediate perk effects
        ApplyPerkEffects(perk, progress.currentTier);

        return true;
    }

    private void ApplyPerkEffects(PerkDefinition perk, int tier)
    {
        // Perks that modify persistent stats
        switch (perk.effectType)
        {
            case PerkEffectType.CommanderHP:
                bonusCommanderHP = perk.effectAmountPerTier * tier;
                break;
            case PerkEffectType.WallDefense:
                wallDefense = perk.effectAmountPerTier * tier;
                break;
        }
    }

    /// <summary>
    /// Reset wall defense to max at start of battle.
    /// </summary>
    public void ResetWallDefenseForBattle()
    {
        currentWallDefense = wallDefense;
    }

    /// <summary>
    /// Apply wall damage, returns remaining damage that hits commander.
    /// </summary>
    public int ApplyWallDamage(int damage)
    {
        if (currentWallDefense <= 0) return damage;

        int absorbed = Mathf.Min(damage, currentWallDefense);
        currentWallDefense -= absorbed;
        return damage - absorbed;
    }

    /// <summary>
    /// Build a runtime DeckListDefinition from current deck for battle.
    /// </summary>
    public DeckListDefinition BuildRuntimeDeckList()
    {
        var deckList = ScriptableObject.CreateInstance<DeckListDefinition>();

        Debug.Log($"[PlayerProgressData] BuildRuntimeDeckList called. currentDeck has {currentDeck.Count} cards:");

        // Group cards by definition and count
        var grouped = currentDeck.GroupBy(c => c.definition);
        foreach (var group in grouped)
        {
            var entry = new DeckListDefinition.Entry
            {
                card = group.Key,
                count = group.Count()
            };
            deckList.entries.Add(entry);

            string cardType = group.Key.isSpecialCard ? "SPECIAL" : "Unit";
            Debug.Log($"  - {group.Key.cardName} x{group.Count()} ({cardType})");
        }

        int specialCount = currentDeck.Count(c => c.definition.isSpecialCard);
        Debug.Log($"[PlayerProgressData] Built deck with {deckList.entries.Count} unique cards, {currentDeck.Count} total, {specialCount} specials");

        return deckList;
    }

    private string GenerateInstanceId()
    {
        return $"card_{nextInstanceId++}_{DateTime.Now.Ticks}";
    }

    #region Save/Load

    private const string SaveFileName = "player_progress.json";

    /// <summary>
    /// Save progress to JSON file.
    /// </summary>
    public void SaveToJson()
    {
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        var saveData = new PlayerSaveData(this);
        string json = JsonUtility.ToJson(saveData, prettyPrint: true);
        File.WriteAllText(path, json);
        Debug.Log($"[PlayerProgressData] Saved to: {path}");
    }

    /// <summary>
    /// Load progress from JSON file.
    /// </summary>
    public bool LoadFromJson(List<UnitCardDefinition> cardPool, List<DoctrineDefinition> doctrinePool, List<PerkDefinition> perkPool)
    {
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);

        if (!File.Exists(path))
        {
            Debug.Log("[PlayerProgressData] No save file found, starting fresh.");
            return false;
        }

        try
        {
            string json = File.ReadAllText(path);
            var saveData = JsonUtility.FromJson<PlayerSaveData>(json);
            saveData.ApplyTo(this, cardPool, doctrinePool, perkPool);
            Debug.Log($"[PlayerProgressData] Loaded from: {path}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[PlayerProgressData] Failed to load: {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Delete save file.
    /// </summary>
    public void DeleteSave()
    {
        string path = Path.Combine(Application.persistentDataPath, SaveFileName);
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("[PlayerProgressData] Save file deleted.");
        }
    }

    /// <summary>
    /// Reset all progress to default state.
    /// </summary>
    public void ResetProgress()
    {
        warFunds = 0;
        ownedCards.Clear();
        currentDeck.Clear();
        ownedDoctrines.Clear();
        equippedDoctrines.Clear();
        perkLevels.Clear();
        bonusCommanderHP = 0;
        wallDefense = 0;
        currentWallDefense = 0;
    }

    /// <summary>
    /// Initialize the player's deck from a starting DeckListDefinition.
    /// All cards go into the currentDeck (ready to play).
    /// Call this when starting a new game or if deck is empty.
    /// </summary>
    public void InitializeFromDeck(DeckListDefinition startingDeck)
    {
        if (startingDeck == null || startingDeck.entries == null)
        {
            Debug.LogWarning("[PlayerProgressData] Cannot initialize from null deck!");
            return;
        }

        Debug.Log($"[PlayerProgressData] Initializing deck from {startingDeck.name} with {startingDeck.entries.Count} unique cards...");

        // Clear existing deck (but keep owned cards that weren't in deck)
        currentDeck.Clear();

        int totalCards = 0;
        foreach (var entry in startingDeck.entries)
        {
            if (entry.card == null) continue;

            // Create owned instances for each copy
            for (int i = 0; i < entry.count; i++)
            {
                var owned = new OwnedCard
                {
                    instanceId = GenerateInstanceId(),
                    definition = entry.card,
                    bonusAtk = 0,
                    bonusHp = 0
                };

                currentDeck.Add(owned);
                totalCards++;
            }
        }

        Debug.Log($"[PlayerProgressData] Initialized deck with {totalCards} total cards ({currentDeck.Count} in deck, {ownedCards.Count} in collection)");
    }

    /// <summary>
    /// Check if the player has any cards (either in deck or collection).
    /// Use this to determine if we need to initialize from starting deck.
    /// </summary>
    public bool HasAnyCards()
    {
        return currentDeck.Count > 0 || ownedCards.Count > 0;
    }

    /// <summary>
    /// Initialize from a deck only if the player doesn't have any cards yet.
    /// Safe to call multiple times - will only initialize once.
    /// </summary>
    public void InitializeFromDeckIfEmpty(DeckListDefinition startingDeck)
    {
        if (!HasAnyCards())
        {
            InitializeFromDeck(startingDeck);
        }
        else
        {
            Debug.Log($"[PlayerProgressData] Player already has {currentDeck.Count} deck cards and {ownedCards.Count} collection cards, skipping initialization.");
        }
    }

    #endregion
}

/// <summary>
/// Represents a single owned card instance with potential stat bonuses.
/// </summary>
[Serializable]
public class OwnedCard
{
    [Tooltip("Unique instance ID for this specific card copy")]
    public string instanceId;

    [Tooltip("Reference to the card definition")]
    public UnitCardDefinition definition;

    [Tooltip("Bonus ATK from Medals perk")]
    public int bonusAtk;

    [Tooltip("Bonus HP from Medals perk")]
    public int bonusHp;
}

/// <summary>
/// Tracks upgrade progress for a single perk.
/// </summary>
[Serializable]
public class PerkProgress
{
    public PerkDefinition perk;
    public int currentTier;
}

/// <summary>
/// Serializable save data structure for JSON persistence.
/// </summary>
[Serializable]
public class PlayerSaveData
{
    public int warFunds;
    public List<SavedCard> ownedCards = new List<SavedCard>();
    public List<SavedCard> deckCards = new List<SavedCard>();
    public List<string> ownedDoctrineIds = new List<string>();
    public List<string> equippedDoctrineIds = new List<string>();
    public List<SavedPerkProgress> perkProgress = new List<SavedPerkProgress>();
    public int bonusCommanderHP;
    public int wallDefense;

    public PlayerSaveData() { }

    public PlayerSaveData(PlayerProgressData data)
    {
        warFunds = data.warFunds;
        bonusCommanderHP = data.bonusCommanderHP;
        wallDefense = data.wallDefense;

        foreach (var card in data.ownedCards)
        {
            ownedCards.Add(new SavedCard
            {
                instanceId = card.instanceId,
                cardName = card.definition?.cardName ?? "",
                bonusAtk = card.bonusAtk,
                bonusHp = card.bonusHp
            });
        }

        foreach (var card in data.currentDeck)
        {
            deckCards.Add(new SavedCard
            {
                instanceId = card.instanceId,
                cardName = card.definition?.cardName ?? "",
                bonusAtk = card.bonusAtk,
                bonusHp = card.bonusHp
            });
        }

        foreach (var doctrine in data.ownedDoctrines)
        {
            if (doctrine != null)
                ownedDoctrineIds.Add(doctrine.doctrineId);
        }

        foreach (var doctrine in data.equippedDoctrines)
        {
            if (doctrine != null)
                equippedDoctrineIds.Add(doctrine.doctrineId);
        }

        foreach (var perk in data.perkLevels)
        {
            if (perk.perk != null)
            {
                perkProgress.Add(new SavedPerkProgress
                {
                    perkId = perk.perk.perkId,
                    tier = perk.currentTier
                });
            }
        }
    }

    public void ApplyTo(PlayerProgressData data, List<UnitCardDefinition> cardPool, List<DoctrineDefinition> doctrinePool, List<PerkDefinition> perkPool)
    {
        data.warFunds = warFunds;
        data.bonusCommanderHP = bonusCommanderHP;
        data.wallDefense = wallDefense;

        data.ownedCards.Clear();
        foreach (var saved in ownedCards)
        {
            var def = cardPool.Find(c => c.cardName == saved.cardName);
            if (def != null)
            {
                data.ownedCards.Add(new OwnedCard
                {
                    instanceId = saved.instanceId,
                    definition = def,
                    bonusAtk = saved.bonusAtk,
                    bonusHp = saved.bonusHp
                });
            }
        }

        data.currentDeck.Clear();
        foreach (var saved in deckCards)
        {
            var def = cardPool.Find(c => c.cardName == saved.cardName);
            if (def != null)
            {
                data.currentDeck.Add(new OwnedCard
                {
                    instanceId = saved.instanceId,
                    definition = def,
                    bonusAtk = saved.bonusAtk,
                    bonusHp = saved.bonusHp
                });
            }
        }

        data.ownedDoctrines.Clear();
        foreach (var id in ownedDoctrineIds)
        {
            var doctrine = doctrinePool.Find(d => d.doctrineId == id);
            if (doctrine != null)
                data.ownedDoctrines.Add(doctrine);
        }

        data.equippedDoctrines.Clear();
        foreach (var id in equippedDoctrineIds)
        {
            var doctrine = doctrinePool.Find(d => d.doctrineId == id);
            if (doctrine != null)
                data.equippedDoctrines.Add(doctrine);
        }

        data.perkLevels.Clear();
        foreach (var saved in perkProgress)
        {
            var perk = perkPool.Find(p => p.perkId == saved.perkId);
            if (perk != null)
            {
                data.perkLevels.Add(new PerkProgress
                {
                    perk = perk,
                    currentTier = saved.tier
                });
            }
        }
    }
}

[Serializable]
public class SavedCard
{
    public string instanceId;
    public string cardName;
    public int bonusAtk;
    public int bonusHp;
}

[Serializable]
public class SavedPerkProgress
{
    public string perkId;
    public int tier;
}
