using UnityEngine;

[CreateAssetMenu(menuName = "Army Prototype/Commander Definition", fileName = "Commander_")]
public class CommanderDefinition : ScriptableObject
{
    [Header("Identity")]
    public CommanderBonus.CommanderId commanderId = CommanderBonus.CommanderId.None;

    public string displayName = "Commander";
    [TextArea(2, 6)]
    public string description;

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
