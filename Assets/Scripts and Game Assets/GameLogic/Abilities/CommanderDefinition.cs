using UnityEngine;

[CreateAssetMenu(menuName = "Army Prototype/Commander Definition", fileName = "Commander_")]
public class CommanderDefinition : ScriptableObject
{
    [Header("Identity")]
    public CommanderBonus.CommanderId commanderId = CommanderBonus.CommanderId.None;

    public string displayName = "Commander";
    [TextArea(2, 6)]
    public string description;

    [Header("World Map Display")]
    [Tooltip("Portrait sprite shown on world map marker")]
    public Sprite mapPortrait;

    [Tooltip("Difficulty rating (1-5 stars)")]
    [Range(1, 5)]
    public int difficulty = 1;

    [Header("Deck")]
    public DeckListDefinition deck;

    [Header("Optional Starting Overrides")]
    [Tooltip("If true, overrides the player's starting Commander HP for this battle/campaign run.")]
    public bool overrideStartingHP = false;
    public int startingHP = 2000;

    [Tooltip("If true, overrides the player's starting gold for this battle/campaign run.")]
    public bool overrideStartingGold = false;
    public int startingGold = 0;
}
