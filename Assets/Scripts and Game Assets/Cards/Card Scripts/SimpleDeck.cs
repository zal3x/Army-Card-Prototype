using System;
using System.Collections.Generic;

/// <summary>
/// SimpleDeck: Manages a player's deck of cards with draw and discard piles.
/// 
/// DESIGN PHILOSOPHY:
/// - Two-pile system: draw pile (face down) and discard pile (face up)
/// - When draw pile empties, discard pile is shuffled back into draw pile
/// - Seeded RNG for deterministic shuffling (repeatable tests)
/// 
/// LIFECYCLE:
/// 1. Created at battle start with a seed
/// 2. Starting deck loaded via SetStartingDeck()
/// 3. Optionally shuffled at battle start
/// 4. Cards drawn during draw phase
/// 5. Cards discarded after being played/used
/// 6. Auto-reshuffle when draw pile empties
/// 
/// CARD FLOW:
/// Draw Pile → Hand → Played → Discard Pile → (reshuffle) → Draw Pile
/// 
/// SEEDING:
/// - Each player gets a unique seed (e.g., 111 for Player A, 112 for Player B)
/// - Same seed = same shuffle order (useful for debugging/testing)
/// - Different seed = different shuffle (provides variety)
/// 
/// PERFORMANCE NOTES:
/// - DrawOne() from end of list is O(1) (fast)
/// - Fisher-Yates shuffle is O(n) (efficient)
/// - Search operations are O(n) (acceptable for deck sizes ~30-60 cards)
/// 
/// FUTURE ENHANCEMENTS:
/// - Deck size limits (min/max cards)
/// - Card rarity/weighting system
/// - "Exile" pile (permanently removed cards)
/// - Deck statistics (cards remaining, reshuffle count, etc.)
/// </summary>
public class SimpleDeck 
{
    // ========================================
    // PRIVATE STATE
    // ========================================

    /// <summary>
    /// Seeded RNG for deterministic shuffling.
    /// Same seed = same shuffle order (useful for testing/replay).
    /// </summary>
    private readonly System.Random rng;

    /// <summary>
    /// Draw pile: Cards that can be drawn into hand.
    /// Drawing happens from the END of the list (O(1) operation).
    /// </summary>
    private readonly List<UnitCard> drawPile = new();

    /// <summary>
    /// Discard pile: Cards that have been played/used.
    /// When draw pile empties, discard is shuffled back into draw.
    /// </summary>
    private readonly List<UnitCard> discardPile = new();

    // ========================================
    // CONSTRUCTOR
    // ========================================

    /// <summary>
    /// Creates a new SimpleDeck with a specific RNG seed.
    /// 
    /// PARAMETERS:
    /// - seed: RNG seed for shuffling (same seed = same shuffle order)
    /// 
    /// USAGE:
    /// Player A: new SimpleDeck(111)
    /// Player B: new SimpleDeck(112)
    /// </summary>
    public SimpleDeck(int seed)
    {
        rng = new System.Random(seed);
    }

    // ========================================
    // DECK SETUP
    // ========================================

    /// <summary>
    /// Loads the starting deck from a list of cards.
    /// 
    /// WHEN TO CALL:
    /// - Once at battle start, after deck is built from DeckListDefinition
    /// 
    /// WHAT IT DOES:
    /// 1. Clears both draw and discard piles (fresh start)
    /// 2. Adds all cards to draw pile
    /// 3. Cards are in the order provided (not shuffled yet)
    /// 
    /// NOTE: Call ShuffleDrawPile() afterwards if you want randomization.
    /// </summary>
    public void SetStartingDeck(List<UnitCard> cards)
    {
        drawPile.Clear();
        discardPile.Clear();
        drawPile.AddRange(cards);
    }

    /// <summary>
    /// Shuffles the draw pile using Fisher-Yates algorithm.
    /// 
    /// WHEN TO CALL:
    /// - After SetStartingDeck() if you want random order
    /// - Automatically called when draw pile empties and discard is moved back
    /// 
    /// ALGORITHM:
    /// Fisher-Yates shuffle: O(n) time, unbiased random permutation
    /// - Starts from end of list, swaps each element with random earlier position
    /// - Uses seeded RNG for deterministic results
    /// 
    /// EXAMPLE:
    /// Before: [Infantry, Tank, Plane, Infantry, Tank]
    /// After:  [Plane, Infantry, Tank, Infantry, Tank] (depends on seed)
    /// </summary>
    public void ShuffleDrawPile()
    {
        // Fisher-Yates shuffle (modern algorithm)
        for (int i = drawPile.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (drawPile[i], drawPile[j]) = (drawPile[j], drawPile[i]);
        }
    }

    // ========================================
    // DRAWING CARDS
    // ========================================

    /// <summary>
    /// Draws one card from the deck.
    /// 
    /// BEHAVIOR:
    /// 1. If draw pile has cards: return top card (end of list)
    /// 2. If draw pile empty but discard has cards: reshuffle discard into draw, then draw
    /// 3. If both piles empty: return null (deck exhausted)
    /// 
    /// PERFORMANCE:
    /// - Draws from END of list (O(1) operation)
    /// - RemoveAt(end) is fast, RemoveAt(0) would be slow (O(n))
    /// 
    /// RETURNS:
    /// The drawn card, or null if deck is completely empty
    /// 
    /// EXAMPLE USAGE:
    /// UnitCard card = deck.DrawOne();
    /// if (card != null) hand.Add(card);
    /// </summary>
    public UnitCard DrawOne()
    {
        // If draw pile is empty, try to reshuffle discard
        if (drawPile.Count == 0)
        {
            // Both piles empty = deck exhausted
            if (discardPile.Count == 0) return null;

            // Move discard to draw and shuffle
            drawPile.AddRange(discardPile);
            discardPile.Clear();
            ShuffleDrawPile();
        }

        // Draw from end of list (O(1) operation)
        int lastIndex = drawPile.Count - 1;
        UnitCard card = drawPile[lastIndex];
        drawPile.RemoveAt(lastIndex);

        return card;
    }

    // ========================================
    // DISCARDING CARDS
    // ========================================

    /// <summary>
    /// Adds a card to the discard pile.
    /// 
    /// WHEN TO CALL:
    /// - After a card is played/bought and the unit is placed
    /// - When a card is destroyed or removed from play
    /// 
    /// NOTE: Null cards are safely ignored (defensive programming).
    /// 
    /// EXAMPLE:
    /// UnitCard card = hand[0];
    /// hand.RemoveAt(0);
    /// Unit unit = card.CreateUnitInstance();
    /// board.Add(unit);
    /// deck.Discard(card); // Card goes to discard
    /// </summary>
    public void Discard(UnitCard card)
    {
        if (card != null) discardPile.Add(card);
    }

    // ========================================
    // ADVANCED OPERATIONS
    // ========================================

    /// <summary>
    /// Searches the entire deck for a matching card and removes it.
    /// 
    /// SEARCH ORDER:
    /// 1. Draw pile (backwards, for removal efficiency)
    /// 2. Discard pile (backwards, for removal efficiency)
    /// 
    /// USE CASES:
    /// - Duplicate ability (find another copy of a unit)
    /// - Card tutoring effects (search for specific card type)
    /// - Exile effects (permanently remove cards from deck)
    /// 
    /// PARAMETERS:
    /// - match: Predicate function that returns true for the desired card
    /// 
    /// RETURNS:
    /// The first matching card found (removed from deck), or null if not found
    /// 
    /// PERFORMANCE:
    /// - O(n) search through both piles
    /// - Acceptable for typical deck sizes (30-60 cards)
    /// 
    /// EXAMPLE USAGE:
    /// // Find another Infantry card
    /// UnitCard copy = deck.TryRemoveFirstMatchingFromDeck(c => c.Name == "Infantry");
    /// if (copy != null) {
    ///     // Found and removed from deck, can now play it
    /// }
    /// </summary>
    public UnitCard TryRemoveFirstMatchingFromDeck(System.Predicate<UnitCard> match)
    {
        // Search draw pile (backwards for efficient removal)
        for (int i = drawPile.Count - 1; i >= 0; i--)
        {
            if (match(drawPile[i]))
            {
                UnitCard found = drawPile[i];
                drawPile.RemoveAt(i);
                return found;
            }
        }
       

        // Search discard pile (backwards for efficient removal)
        for (int i = discardPile.Count - 1; i >= 0; i--)
        {
            if (match(discardPile[i]))
            {
                UnitCard found = discardPile[i];
                discardPile.RemoveAt(i);
                return found;
            }
        }

        // Not found in either pile
        return null;
    }
/// <summary>
/// Searches ONLY the discard pile for a matching card and removes it.
/// Use this when you specifically want "the card that was just played"
/// (since played cards go to discard).
/// </summary>
public UnitCard TryRemoveFirstMatchingFromDiscard(System.Predicate<UnitCard> match)
{
    // Search discard pile (backwards for efficient removal)
    for (int i = discardPile.Count - 1; i >= 0; i--)
    {
        if (match(discardPile[i]))
        {
            UnitCard found = discardPile[i];
            discardPile.RemoveAt(i);
            return found;
        }
    }

    return null;
}

    // ========================================
    // DISCARD PILE ACCESS
    // ========================================

    /// <summary>
    /// Number of cards currently in the discard pile.
    /// Useful for checking if POW (take from discard) can be played.
    /// </summary>
    public int DiscardCount => discardPile.Count;

    /// <summary>
    /// Number of cards currently in the draw pile.
    /// </summary>
    public int DrawCount => drawPile.Count;

    /// <summary>
    /// Gets a copy of the discard pile for UI display (e.g., POW card selection).
    /// Returns cloned cards to prevent mutation.
    /// </summary>
    public List<UnitCard> GetDiscardPileSnapshot()
    {
        var snapshot = new List<UnitCard>(discardPile.Count);
        foreach (var card in discardPile)
        {
            if (card != null)
                snapshot.Add(card.Clone());
        }
        return snapshot;
    }

    /// <summary>
    /// Removes a specific card from the discard pile by index.
    /// Used by POW to take a card from enemy discard.
    /// </summary>
    public UnitCard RemoveFromDiscardAt(int index)
    {
        if (index < 0 || index >= discardPile.Count)
            return null;

        UnitCard card = discardPile[index];
        discardPile.RemoveAt(index);
        return card;
    }

    /// <summary>
    /// Removes a specific card from the discard pile by matching predicate.
    /// Returns the removed card, or null if not found.
    /// </summary>
    public UnitCard RemoveFromDiscardByName(string cardName)
    {
        for (int i = discardPile.Count - 1; i >= 0; i--)
        {
            if (discardPile[i] != null && discardPile[i].Name == cardName)
            {
                UnitCard found = discardPile[i];
                discardPile.RemoveAt(i);
                return found;
            }
        }
        return null;
    }

    //ability to see opponent hand

public List<UnitCard> PeekTop(int count)
{
    // Draw pile draws from END (top = end)
int n = Math.Max(0, Math.Min(count, drawPile.Count));
    List<UnitCard> result = new List<UnitCard>(n);

    for (int i = 0; i < n; i++)
    {
        int index = drawPile.Count - 1 - i;
        if (index >= 0 && index < drawPile.Count)
            result.Add(drawPile[index]);
    }

    return result;
}
} 

// ========================================
// USAGE EXAMPLES
// ========================================
//
// === BASIC SETUP ===
// SimpleDeck deck = new SimpleDeck(seed: 123);
// List<UnitCard> startingCards = deckDefinition.BuildRuntimeDeck();
// deck.SetStartingDeck(startingCards);
// deck.ShuffleDrawPile(); // Optional: randomize order
//
// === DRAW PHASE ===
// for (int i = 0; i < 5; i++) {
//     UnitCard card = deck.DrawOne();
//     if (card != null) hand.Add(card);
// }
//
// === BUY PHASE ===
// UnitCard chosen = hand[0];
// hand.RemoveAt(0);
// Unit unit = chosen.CreateUnitInstance();
// board.Add(unit);
// deck.Discard(chosen); // Card goes to discard
//
// === DUPLICATE ABILITY ===
// Unit sourceUnit = // ... unit that was just played
// UnitCard copy = deck.TryRemoveFirstMatchingFromDeck(c => c.Name == sourceUnit.Name);
// if (copy != null) {
//     Unit duplicate = copy.CreateUnitInstance();
//     board.Add(duplicate);
//     deck.Discard(copy);
// }
//
// ========================================
// DESIGN NOTES
// ========================================
//
// === WHY TWO PILES? ===
// - Draw pile: Unknown order (face down in real card games)
// - Discard pile: Known order (face up in real card games)
// - Auto-reshuffle when draw empties keeps gameplay flowing
// - Prevents "deck out" unless both piles are empty
//
// === WHY DRAW FROM END? ===
// - List.RemoveAt(lastIndex) is O(1) - instant
// - List.RemoveAt(0) is O(n) - must shift entire list
// - For 50-card deck: end removal = 1 operation, start removal = 50 operations
//
// === ALTERNATIVE DESIGNS ===
// - Single pile: Simpler but no reshuffle mechanic
// - Queue<T>: Could use for FIFO, but loses random access
// - LinkedList<T>: Good for removal anywhere, overkill for this use case
// - Current approach: Best balance of simplicity and efficiency
//