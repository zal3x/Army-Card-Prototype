using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SPECIAL / SPELL cards that trigger effects but do NOT place a unit.
/// This manager tracks per-round and per-battle status effects and exposes hooks
/// for BattleTestRunner / CombatResolver to query.
/// </summary>
public class SpecialCards : MonoBehaviour
{
    public enum SpecialCardType
    {
        BarbedWire,
        LandMines,
        MetalDetector,
        Radar,
        Logistics,
        KGB,
        POW,
        Kamikaze,
        EMPStrike,
        Trenches,
        EmergencyFunds,
        SupplyConvoy,
        Recon,
        PaperTiger,
        Medic,
        Mechanic,
        HangarRat
    }

    [Serializable]
    public class Status
    {
        // ===== Round-only incoming effects applied AGAINST this player =====
        public bool barbedWireIncomingThisRound;
        public bool landMinesIncomingThisRound;
        public bool empIncomingThisRound;
        public HashSet<int> barbedWireAppliedUnitIdsThisRound = new();


        // ===== Round-only negations for this player =====
        public bool metalDetectorActiveThisRound; // negates barbedWire/landMines if they were incoming

        // ===== Battle-long effects on this player =====
        public bool trenchesThisBattle;
        public bool kgbThisBattle;
    }

    // This is how CombatResolver / other systems can query status without needing scene refs.
    public static Func<Player, bool> HasTrenchesThisBattle;
    public static Func<Player, bool> IsEmpActiveOnPlayerThisRound;
    public static Func<Player, bool> IsBarbedWireActiveOnPlayerThisRound;

    // Rearrangement gate (Land Mines)
    public static Func<Player, bool> CannotRearrangeThisRound;

    // Optional UI hooks
    public event Action<Player, List<UnitCard>> OnRadarReveal;
    public event Action<Player, List<UnitCard>> OnReconPeek;

    private readonly Dictionary<Player, Status> statusByPlayer = new();

    private Status S(Player p)
    {
        if (p == null) return null;
        if (!statusByPlayer.TryGetValue(p, out var s) || s == null)
        {
            s = new Status();
            statusByPlayer[p] = s;
        }
        return s;
    }

    private void Awake()
    {
        // Wire static hooks so CombatResolver (static) can query.
        HasTrenchesThisBattle = (p) => p != null && S(p).trenchesThisBattle;
        IsEmpActiveOnPlayerThisRound = (p) => p != null && S(p).empIncomingThisRound;
        IsBarbedWireActiveOnPlayerThisRound = (p) =>
        {
            if (p == null) return false;
            var st = S(p);
            // Metal detector negates incoming traps for THIS ROUND
            if (st.metalDetectorActiveThisRound) return false;
            return st.barbedWireIncomingThisRound;
        };

        CannotRearrangeThisRound = (p) =>
        {
            if (p == null) return false;
            var st = S(p);
            if (st.metalDetectorActiveThisRound) return false;
            return st.landMinesIncomingThisRound;
        };
    }

    /// <summary>
    /// Call at the START of each round (before buy/rearrange/combat).
    /// Clears round-only effects, then applies any "every round" battle effects (KGB).
    /// </summary>
    public void StartRound(Player playerA, Player playerB)
    {
        ClearRoundOnly(playerA);
        ClearRoundOnly(playerB);

        // KGB: steal 1 gold every round this battle
        ApplyKgbTick(playerA, playerB);
        ApplyKgbTick(playerB, playerA);
    }

    /// <summary>
    /// Call at END of battle if you want to reset persistent statuses.
    /// </summary>
    public void ResetAll(Player playerA, Player playerB)
    {
        statusByPlayer.Clear();
    }

    private void ClearRoundOnly(Player p)
    {
        if (p == null) return;
        var st = S(p);
        st.barbedWireAppliedUnitIdsThisRound.Clear();
        st.barbedWireIncomingThisRound = false;
        st.landMinesIncomingThisRound = false;
        st.empIncomingThisRound = false;
        st.metalDetectorActiveThisRound = false;
    }
private void TryApplyBarbedWireToUnit(Player defender, Unit u)
{
    if (defender == null || u == null || !u.IsAlive) return;

    var st = S(defender);

    // Only applies while Barbed Wire is active this round, and not negated
    if (st.metalDetectorActiveThisRound) return;
    if (!st.barbedWireIncomingThisRound) return;

    // Only infantry
    if (u.Type != UnitType.Infantry) return;

    // Only once per round per unit instance
    if (st.barbedWireAppliedUnitIdsThisRound.Contains(u.InstanceId)) return;

    // 1 HP units unaffected; cannot kill
    if (u.HP > 1)
    {
        int dmg = Mathf.FloorToInt(u.HP * 0.5f);
        dmg = Mathf.Clamp(dmg, 0, u.HP - 1);
        u.HP -= dmg;
    }

    st.barbedWireAppliedUnitIdsThisRound.Add(u.InstanceId);
}
private void ApplyBarbedWireToCurrentInfantry(Player defender, BattleTestRunner runner)
{
    if (defender == null || runner == null) return;

    RowType infRow = runner.GetPlacementRowPublic(defender, UnitType.Infantry);
    var units = defender.Rows[infRow].Units;

    foreach (var u in units)
        TryApplyBarbedWireToUnit(defender, u);
}

    private void ApplyKgbTick(Player kgbOwner, Player enemy)
    {
        if (kgbOwner == null || enemy == null) return;
        if (!S(kgbOwner).kgbThisBattle) return;

        if (enemy.Gold <= 0) return;
        enemy.Gold -= 1;
        kgbOwner.Gold += 1;
    }

    /// <summary>
    /// Whether this card is currently playable (preconditions like Kamikaze needing an air unit).
    /// BattleTestRunner can use this to filter AI buys too.
    /// </summary>
    public bool CanPlay(SpecialCardType type, Player owner, Player enemy, BattleTestRunner runner)
    {
        if (owner == null) return false;
        switch (type)
        {
            case SpecialCardType.Kamikaze:
                // Must have an air unit deployed
                return runner != null && runner.Special_HasAnyAirUnit(owner);

            case SpecialCardType.POW:
                return enemy != null && enemy.Hand != null && enemy.Hand.Count > 0;

            case SpecialCardType.Radar:
                return enemy != null && enemy.Hand != null;

            case SpecialCardType.Recon:
                return owner.Deck != null;

            case SpecialCardType.PaperTiger:
                return runner != null && runner.Special_HasAnyUnit(owner);

            default:
                return true;
        }
    }

    /// <summary>
    /// Executes the special card effect. This is called AFTER paying gold and removing the card from hand.
    /// </summary>
    public void Play(SpecialCardType type, Player owner, Player enemy, BattleTestRunner runner)
    {
        if (owner == null) return;

        switch (type)
        {
            // 1) Barbed wire: Reduce enemy infantry row by 50% health this round. Cannot kill. 1 HP unaffected.
            case SpecialCardType.BarbedWire:
{
    if (enemy != null)
    {
        // Keep it active for the rest of the round (for late-added infantry)
        S(enemy).barbedWireIncomingThisRound = true;

        // Apply instantly to whatever infantry exists right now (slam moment)
        if (runner != null)
            ApplyBarbedWireToCurrentInfantry(enemy, runner);
    }
    break;
}



            // 2) Land Mines: Enemy cannot rearrange units this round.
            case SpecialCardType.LandMines:
                if (enemy != null) S(enemy).landMinesIncomingThisRound = true;
                break;

            // 3) Metal Detector: If enemy applied barbed wire or land mines against you, remove for this round.
            case SpecialCardType.MetalDetector:
                S(owner).metalDetectorActiveThisRound = true;
                break;

            // 4) Radar: view opponents current hand
case SpecialCardType.Radar:
    if (enemy != null && enemy.Hand != null)
    {
        // Make a safe snapshot for UI (clones prevent accidental mutation)
        var snapshot = new List<UnitCard>(enemy.Hand.Count);
        for (int i = 0; i < enemy.Hand.Count; i++)
        {
            if (enemy.Hand[i] != null)
                snapshot.Add(enemy.Hand[i].Clone());
        }

        // Keep your event hook if you still want it
        OnRadarReveal?.Invoke(owner, snapshot);

        // Only show overlay for human player (Player A)
        bool isHuman = runner != null && owner == runner.PlayerA;
        Debug.Log($"[RADAR] owner={owner?.Name}, PlayerA={runner?.PlayerA?.Name}, isHuman={isHuman}");
        if (isHuman)
        {
            runner.ShowRevealOverlay("Radar: Enemy Hand", snapshot);
        }

        // Optional debug log
        Debug.Log($"[RADAR] {owner.Name} sees {enemy.Name} hand: " +
                  string.Join(", ", snapshot.ConvertAll(c => c.Name)));
    }
    break;


            // 5) Logistics: Gain 2 random cards from deck to your hand
case SpecialCardType.Logistics:
    DoLogistics(owner, runner);
    runner?.RefreshBuyHandUI(owner);
    break;


            // 6) KGB: steal 1 gold from opponent every round this battle.
            case SpecialCardType.KGB:
                S(owner).kgbThisBattle = true;
                
                break;

// 7) POW: Steal a random card from opponent hand
case SpecialCardType.POW:
    if (enemy != null && enemy.Hand != null && enemy.Hand.Count > 0 && owner.Hand != null)
    {
        int idx = UnityEngine.Random.Range(0, enemy.Hand.Count);
        UnitCard stolen = enemy.Hand[idx];
        enemy.Hand.RemoveAt(idx);

        owner.Hand.Add(stolen);

        // ✅ IMPORTANT: update the visible hand immediately
runner?.RefreshBuyHandUI(owner);    }
    break;


            // 8) Kamikaze: Destroy any enemy unit at cost of one of your active air units.
            case SpecialCardType.Kamikaze:
    // handled by BattleTestRunner targeting coroutine (click to select plane then enemy)
    break;


            // 9) EMP strike: Enemy vehicles lose their unit abilities this round.
            case SpecialCardType.EMPStrike:
                if (enemy != null) S(enemy).empIncomingThisRound = true;
                break;

            // 10) Trenches: All friendly infantry absorb 50% more damage this BATTLE (like tank armor behavior).
            case SpecialCardType.Trenches:
                S(owner).trenchesThisBattle = true;
                break;

            // 11) Emergency Funds: Immediately gain 5 gold
            case SpecialCardType.EmergencyFunds:
                owner.Gold += 5;
                break;

            // 12) Supply Convoy: reduce price of all units in hand by 1 gold
case SpecialCardType.SupplyConvoy:
    if (owner.Hand != null)
    {
        foreach (var c in owner.Hand)
            c.Cost = Mathf.Max(0, c.Cost - 1);

        // IMPORTANT: update cost text without rebuilding carousel
        runner?.RefreshBuyHandUI(owner, visualsOnly: true);
    }
    break;


            // 13) Recon: Look at the top 3 cards of your deck that will be drawn next round
case SpecialCardType.Recon:
    if (owner != null && owner.Deck != null)
    {
        var peek = owner.Deck.PeekTop(3);

        // Clone for safety (PeekTop may already return references from drawPile)
        var snapshot = new List<UnitCard>(peek.Count);
        for (int i = 0; i < peek.Count; i++)
        {
            if (peek[i] != null)
                snapshot.Add(peek[i].Clone());
        }

        // Keep event hook
        OnReconPeek?.Invoke(owner, snapshot);

        // Only show overlay for human player (Player A)
        bool isHumanRecon = runner != null && owner == runner.PlayerA;
        Debug.Log($"[RECON] owner={owner?.Name}, PlayerA={runner?.PlayerA?.Name}, isHuman={isHumanRecon}");
        if (isHumanRecon)
        {
            runner.ShowRevealOverlay("Recon: Next 3 Draws", snapshot);
        }

        // Optional debug log
        Debug.Log($"[RECON] {owner.Name} peeks next 3: " +
                  string.Join(", ", snapshot.ConvertAll(c => c.Name)));
    }
    break;


            // 14) Paper Tiger: Recall/replace any unit on your army (returns to hand at cost 0).
            case SpecialCardType.PaperTiger:
                if (runner != null)
                {
                    runner.Special_DoPaperTiger(owner);
                }
                break;

            // 15) Medic: infantry row recovers 50% HP
            case SpecialCardType.Medic:
                if (runner != null) runner.Special_HealRowBySemanticType(owner, UnitType.Infantry, 0.5f);
                break;

            // 16) Mechanic: vehicle row recovers 50% HP
            case SpecialCardType.Mechanic:
                if (runner != null) runner.Special_HealRowBySemanticType(owner, UnitType.Vehicle, 0.5f);
                break;

            // 17) Hangar Rat: air row recovers 50% HP
            case SpecialCardType.HangarRat:
                if (runner != null) runner.Special_HealRowBySemanticType(owner, UnitType.Air, 0.5f);
                break;
        }
    }
private void DoLogistics(Player owner, BattleTestRunner runner)
{
    if (owner == null || owner.Deck == null || owner.Hand == null)
        return;

    int drawnCount = 0;

    // Logistics: draw up to 2 cards immediately into HAND
    for (int i = 0; i < 2; i++)
    {
        UnitCard drawn = owner.Deck.DrawOne();
        if (drawn == null)
            break;

        owner.Hand.Add(drawn);
        drawnCount++;
    }

    Debug.Log($"[LOGISTICS] {owner.Name} drew {drawnCount} card(s) into hand.");

    // IMPORTANT: refresh the buy UI immediately for human player
    if (runner != null)
        runner.RefreshBuyHandUI(owner);
}



public void OnUnitAddedToBoard(Player owner, Unit unit)
{
    TryApplyBarbedWireToUnit(owner, unit);
}
private void ApplyBarbedWireNow(Player defender, BattleTestRunner runner)
{
    if (defender == null || runner == null) return;

    // Target the defender's CURRENT infantry semantic row
    RowType infRow = runner.GetPlacementRowPublic(defender, UnitType.Infantry);
    var units = defender.Rows[infRow].Units;

    foreach (var u in units)
    {
        if (u == null || !u.IsAlive) continue;

        // 1 HP units unaffected
        if (u.HP <= 1) continue;

        // Reduce by 50% of CURRENT HP, rounding down damage.
        int dmg = Mathf.FloorToInt(u.HP * 0.5f);

        // Cannot kill: clamp so at least 1 HP remains
        dmg = Mathf.Clamp(dmg, 0, u.HP - 1);

        u.HP -= dmg;
    }
}

    /// <summary>
    /// Applies Barbed Wire damage (called by BattleTestRunner right before combat begins).
    /// </summary>
    public void ApplyPreCombat(Player defender, BattleTestRunner runner)
{
    if (defender == null || runner == null) return;

    // Only apply if Barbed Wire is active this round (and not negated)
    if (!IsBarbedWireActiveOnPlayerThisRound(defender)) return;

    // Fallback: apply to any infantry that exist now but haven't been hit yet.
    ApplyBarbedWireToCurrentInfantry(defender, runner);
}

}
