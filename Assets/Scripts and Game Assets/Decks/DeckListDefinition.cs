using System;
using System.Collections.Generic;
using UnityEngine;

// NOTE: This is a deck "recipe": which cards exist in the deck and how many.
[CreateAssetMenu(menuName = "Army Prototype/Deck List", fileName = "Deck_")]
public class DeckListDefinition : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public UnitCardDefinition card; // reference to a card asset
        public int count = 1;           // how many copies
    }

    public List<Entry> entries = new();
// NEW: Builds a runtime deck list for a specific owner (commander context)
public List<UnitCard> BuildRuntimeDeck(Player owner)
{
    var result = new List<UnitCard>();

    foreach (var e in entries)
    {
        if (e.card == null) continue;
        if (e.count <= 0) continue;

        for (int i = 0; i < e.count; i++)
        {
            var c = e.card.ToRuntimeCard();
            c.Owner = owner;          // stamp owner for commander bonuses
            c.Definition = e.card;    // keep definition reference (redundant but safe)
            result.Add(c);
        }
    }

    return result;
}

    // NOTE: This builds a runtime deck list of UnitCards for the simulator.
    public List<UnitCard> BuildRuntimeDeck()
    {
        var result = new List<UnitCard>();

        foreach (var e in entries)
        {
            if (e.card == null) continue;
            if (e.count <= 0) continue;

            for (int i = 0; i < e.count; i++)
            {
                result.Add(e.card.ToRuntimeCard());
            }
        }

        return result;
    }
}
