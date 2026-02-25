using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SPECIAL / SPELL cards that trigger effects but do NOT place a unit.
/// This manager tracks per-round and per-battle status effects and exposes hooks
/// for BattleTestRunner / CombatResolver to query.
///
/// FEBRUARY 2026 REWORK:
/// - Effects are now ATK-based, not HP-based
/// - Barbed Wire/Land Mines/Radar Jammer: -1 ATK to specific enemy rows
/// - Wire Cutter/Mechanic/Hanger Rat: Nullify specific debuffs
/// - Trenches: +1 ATK to friendly infantry
/// </summary>
public class SpecialCards : MonoBehaviour
{
    public enum SpecialCardType
    {
        BarbedWire,     // -1 ATK to enemy infantry row
        LandMines,      // -1 ATK to enemy vehicle row
        RadarJammer,    // -1 ATK to enemy ranged/air row
        MetalDetector,  // DEPRECATED: use specific counters instead
        Radar,          // View opponent's hand
        Logistics,      // Draw 2 cards
        KGB,            // Steal unit from enemy field to your hand
        POW,            // Take unit from enemy discard pile
        Kamikaze,       // Sacrifice air unit to destroy any enemy
        EMPStrike,      // -1 ATK to enemy vehicles AND ranged/air
        Trenches,       // +1 ATK to friendly infantry
        EmergencyFunds, // Gain 10 gold
        SupplyConvoy,   // -1 cost to all cards in hand
        Recon,          // View top 3 deck cards
        PaperTiger,     // Return own unit to hand at 0 cost
        Medic,          // Restore 10 commander HP
        WireCutter,     // Nullify barbed wire
        Mechanic,       // Nullify land mines
        HangarRat       // Nullify radar jammer
    }

    [Serializable]
    public class Status
    {
        // ===== Round-only ATK debuffs applied AGAINST this player =====
        public bool barbedWireActiveThisRound;    // -1 ATK to infantry
        public bool landMinesActiveThisRound;     // -1 ATK to vehicles
        public bool radarJammerActiveThisRound;   // -1 ATK to ranged/air
        public bool empActiveThisRound;           // -1 ATK to vehicles AND ranged/air

        // ===== Round-only ATK buffs for this player =====
        public bool trenchesActiveThisRound;      // +1 ATK to infantry

        // ===== Round-only nullifications (counter cards) =====
        public bool wireCutterActiveThisRound;    // Nullifies barbed wire
        public bool mechanicActiveThisRound;      // Nullifies land mines
        public bool hangarRatActiveThisRound;     // Nullifies radar jammer

        // ===== Legacy / DEPRECATED =====
        [System.Obsolete("Use specific counter cards instead")]
        public bool metalDetectorActiveThisRound;

        // ===== Battle-long effects =====
        public bool kgbThisBattle;  // DEPRECATED: KGB is now instant effect
    }

    // ========================================
    // STATIC HOOKS FOR ABILITY SYSTEM
    // ========================================
    // These allow AbilitySystem to query ATK modifiers without scene references.

    /// <summary>
    /// Gets the ATK modifier for a unit based on active special cards.
    /// Returns the total bonus/penalty to add to the unit's base ATK.
    /// </summary>
    public static Func<Player, Unit, int> GetATKModifierForUnit;

    /// <summary>
    /// Legacy hook - kept for compatibility but may be removed.
    /// </summary>
    public static Func<Player, bool> HasTrenchesThisBattle;
    public static Func<Player, bool> IsEmpActiveOnPlayerThisRound;
    public static Func<Player, bool> IsBarbedWireActiveOnPlayerThisRound;
    public static Func<Player, bool> CannotRearrangeThisRound;

    // UI hooks for card reveals
    public event Action<Player, List<UnitCard>> OnRadarReveal;
    public event Action<Player, List<UnitCard>> OnReconPeek;
    public event Action<Player, List<UnitCard>> OnPOWSelection;

    // KGB targeting callback
    public event Action<Player, Player, Action<Unit>> OnKGBSelectUnit;

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
        // Wire static hooks for AbilitySystem and CombatResolver queries.

        GetATKModifierForUnit = (player, unit) =>
        {
            if (player == null || unit == null || !unit.IsAlive)
                return 0;

            int modifier = 0;
            var st = S(player);

            // ========================================
            // BUFFS (applied to player's OWN units)
            // ========================================

            // Trenches: +1 ATK to infantry
            if (st.trenchesActiveThisRound && unit.Type == UnitType.Infantry)
            {
                modifier += 1;
            }

            // ========================================
            // DEBUFFS (applied by ENEMY cards to this player's units)
            // ========================================

            // Barbed Wire: -1 ATK to infantry (nullified by Wire Cutter)
            if (st.barbedWireActiveThisRound && !st.wireCutterActiveThisRound)
            {
                if (unit.Type == UnitType.Infantry)
                    modifier -= 1;
            }

            // Land Mines: -1 ATK to vehicles (nullified by Mechanic)
            if (st.landMinesActiveThisRound && !st.mechanicActiveThisRound)
            {
                if (unit.Type == UnitType.Vehicle || unit.CountsAsVehicle)
                    modifier -= 1;
            }

            // Radar Jammer: -1 ATK to ranged/air (nullified by Hangar Rat)
            if (st.radarJammerActiveThisRound && !st.hangarRatActiveThisRound)
            {
                if (unit.Type == UnitType.Ranged || unit.Type == UnitType.Air)
                    modifier -= 1;
            }

            // EMP Strike: -1 ATK to vehicles AND ranged/air
            // EMP is NOT nullified by counter cards - it's a one-time powerful effect
            if (st.empActiveThisRound)
            {
                if (unit.Type == UnitType.Vehicle || unit.CountsAsVehicle ||
                    unit.Type == UnitType.Ranged || unit.Type == UnitType.Air)
                {
                    modifier -= 1;
                }
            }

            return modifier;
        };

        // Legacy hooks - kept for backwards compatibility
        HasTrenchesThisBattle = (p) => p != null && S(p).trenchesActiveThisRound;
        IsEmpActiveOnPlayerThisRound = (p) => p != null && S(p).empActiveThisRound;
        IsBarbedWireActiveOnPlayerThisRound = (p) =>
        {
            if (p == null) return false;
            var st = S(p);
            if (st.wireCutterActiveThisRound) return false;
            return st.barbedWireActiveThisRound;
        };

        // Rearrangement is now always allowed (Land Mines just reduces ATK)
        CannotRearrangeThisRound = (p) => false;
    }

    /// <summary>
    /// Call at the START of each round (before buy/rearrange/combat).
    /// Clears all round-only effects (game state and visuals).
    /// </summary>
    public void StartRound(Player playerA, Player playerB)
    {
        ClearRoundOnly(playerA);
        ClearRoundOnly(playerB);
        UnitEffectManager.ClearAllEffects();
    }

    /// <summary>
    /// Call at END of battle to reset all statuses.
    /// </summary>
    public void ResetAll(Player playerA, Player playerB)
    {
        statusByPlayer.Clear();
    }

    private void ClearRoundOnly(Player p)
    {
        if (p == null) return;
        var st = S(p);

        // Clear debuffs
        st.barbedWireActiveThisRound = false;
        st.landMinesActiveThisRound = false;
        st.radarJammerActiveThisRound = false;
        st.empActiveThisRound = false;

        // Clear buffs
        st.trenchesActiveThisRound = false;

        // Clear counters
        st.wireCutterActiveThisRound = false;
        st.mechanicActiveThisRound = false;
        st.hangarRatActiveThisRound = false;

#pragma warning disable CS0618
        st.metalDetectorActiveThisRound = false;
#pragma warning restore CS0618
    }

    /// <summary>
    /// Whether this card is currently playable (preconditions).
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

            case SpecialCardType.KGB:
                // Enemy must have at least one unit on field
                return enemy != null && runner != null && runner.Special_HasAnyUnit(enemy);

            case SpecialCardType.POW:
                // Enemy must have cards in discard pile
                return enemy != null && enemy.Deck != null && enemy.Deck.DiscardCount > 0;

            case SpecialCardType.Radar:
                return enemy != null && enemy.Hand != null;

            case SpecialCardType.Recon:
                return owner.Deck != null;

            case SpecialCardType.PaperTiger:
                return runner != null && runner.Special_HasAnyUnit(owner);

            case SpecialCardType.Medic:
                // Only playable if commander is damaged
                return owner.CommanderHP < owner.CommanderMaxHP;

            // Counter cards require the debuff to be active
            case SpecialCardType.WireCutter:
                return S(owner).barbedWireActiveThisRound;

            case SpecialCardType.Mechanic:
                return S(owner).landMinesActiveThisRound;

            case SpecialCardType.HangarRat:
                return S(owner).radarJammerActiveThisRound;

            default:
                return true;
        }
    }

    /// <summary>
    /// Executes the special card effect.
    /// Called AFTER paying gold and removing the card from hand.
    /// </summary>
    public void Play(SpecialCardType type, Player owner, Player enemy, BattleTestRunner runner)
    {
        if (owner == null) return;

        switch (type)
        {
            // ========================================
            // ATK DEBUFF CARDS (Applied to enemy)
            // ========================================

            case SpecialCardType.BarbedWire:
                // -1 ATK to enemy infantry row for this round
                if (enemy != null)
                {
                    S(enemy).barbedWireActiveThisRound = true;
                    ApplyEffectToUnitsWhere("BarbedWire", enemy, u => u.Type == UnitType.Infantry);
                    Debug.Log($"[BARBED WIRE] {owner.Name} applies -1 ATK to {enemy.Name}'s infantry.");
                }
                break;

            case SpecialCardType.LandMines:
                // -1 ATK to enemy vehicle row for this round
                if (enemy != null)
                {
                    S(enemy).landMinesActiveThisRound = true;
                    ApplyEffectToUnitsWhere("LandMines", enemy, u => u.Type == UnitType.Vehicle || u.CountsAsVehicle);
                    Debug.Log($"[LAND MINES] {owner.Name} applies -1 ATK to {enemy.Name}'s vehicles.");
                }
                break;

            case SpecialCardType.RadarJammer:
                // -1 ATK to enemy ranged/air row for this round
                if (enemy != null)
                {
                    S(enemy).radarJammerActiveThisRound = true;
                    ApplyEffectToUnitsWhere("RadarJammer", enemy, u => u.Type == UnitType.Ranged || u.Type == UnitType.Air);
                    Debug.Log($"[RADAR JAMMER] {owner.Name} applies -1 ATK to {enemy.Name}'s ranged/air.");
                }
                break;

            case SpecialCardType.EMPStrike:
                // -1 ATK to enemy vehicles AND ranged/air for this round
                if (enemy != null)
                {
                    S(enemy).empActiveThisRound = true;
                    ApplyEffectToUnitsWhere("EmpStrike", enemy, u =>
                        u.Type == UnitType.Vehicle || u.CountsAsVehicle ||
                        u.Type == UnitType.Ranged  || u.Type == UnitType.Air);
                    Debug.Log($"[EMP STRIKE] {owner.Name} applies -1 ATK to {enemy.Name}'s vehicles and ranged/air.");
                }
                break;

            // ========================================
            // ATK BUFF CARDS (Applied to self)
            // ========================================

            case SpecialCardType.Trenches:
                // +1 ATK to all friendly infantry for this round
                S(owner).trenchesActiveThisRound = true;
                ApplyEffectToUnitsWhere("Trenches", owner, u => u.Type == UnitType.Infantry);
                Debug.Log($"[TRENCHES] {owner.Name}'s infantry gains +1 ATK.");
                break;

            // ========================================
            // COUNTER / NULLIFICATION CARDS
            // ========================================

            case SpecialCardType.WireCutter:
                // Nullifies barbed wire effect
                S(owner).wireCutterActiveThisRound = true;
                RemoveEffectFromUnitsWhere("BarbedWire", owner, u => u.Type == UnitType.Infantry);
                Debug.Log($"[WIRE CUTTER] {owner.Name} nullifies barbed wire.");
                break;

            case SpecialCardType.Mechanic:
                // Nullifies land mines effect
                S(owner).mechanicActiveThisRound = true;
                RemoveEffectFromUnitsWhere("LandMines", owner, u => u.Type == UnitType.Vehicle || u.CountsAsVehicle);
                Debug.Log($"[MECHANIC] {owner.Name} nullifies land mines.");
                break;

            case SpecialCardType.HangarRat:
                // Nullifies radar jammer effect
                S(owner).hangarRatActiveThisRound = true;
                RemoveEffectFromUnitsWhere("RadarJammer", owner, u => u.Type == UnitType.Ranged || u.Type == UnitType.Air);
                Debug.Log($"[HANGAR RAT] {owner.Name} nullifies radar jammer.");
                break;

            case SpecialCardType.MetalDetector:
                // DEPRECATED: Now uses specific counter cards
#pragma warning disable CS0618
                S(owner).metalDetectorActiveThisRound = true;
#pragma warning restore CS0618
                Debug.Log($"[METAL DETECTOR] {owner.Name} activates metal detector (legacy).");
                break;

            // ========================================
            // INTEL / REVEAL CARDS
            // ========================================

            case SpecialCardType.Radar:
                // View opponent's current hand
                if (enemy != null && enemy.Hand != null)
                {
                    var snapshot = new List<UnitCard>(enemy.Hand.Count);
                    for (int i = 0; i < enemy.Hand.Count; i++)
                    {
                        if (enemy.Hand[i] != null)
                            snapshot.Add(enemy.Hand[i].Clone());
                    }

                    OnRadarReveal?.Invoke(owner, snapshot);

                    bool isHuman = runner != null && owner == runner.PlayerA;
                    if (isHuman)
                    {
                        runner.ShowRevealOverlay("Radar: Enemy Hand", snapshot);
                    }

                    Debug.Log($"[RADAR] {owner.Name} sees {enemy.Name}'s hand: " +
                              string.Join(", ", snapshot.ConvertAll(c => c.Name)));
                }
                break;

            case SpecialCardType.Recon:
                // View top 3 cards of your deck
                if (owner != null && owner.Deck != null)
                {
                    var peek = owner.Deck.PeekTop(3);
                    var snapshot = new List<UnitCard>(peek.Count);
                    for (int i = 0; i < peek.Count; i++)
                    {
                        if (peek[i] != null)
                            snapshot.Add(peek[i].Clone());
                    }

                    OnReconPeek?.Invoke(owner, snapshot);

                    bool isHuman = runner != null && owner == runner.PlayerA;
                    if (isHuman)
                    {
                        runner.ShowRevealOverlay("Recon: Next 3 Draws", snapshot);
                    }

                    Debug.Log($"[RECON] {owner.Name} peeks next 3: " +
                              string.Join(", ", snapshot.ConvertAll(c => c.Name)));
                }
                break;

            // ========================================
            // ECONOMY CARDS
            // ========================================

            case SpecialCardType.EmergencyFunds:
                // Gain 10 gold immediately
                owner.Gold += 10;
                Debug.Log($"[EMERGENCY FUNDS] {owner.Name} gains 10 gold. Total: {owner.Gold}");
                break;

            case SpecialCardType.SupplyConvoy:
                // Reduce cost of all cards in hand by 1
                if (owner.Hand != null)
                {
                    foreach (var c in owner.Hand)
                        c.Cost = Mathf.Max(0, c.Cost - 1);

                    runner?.RefreshBuyHandUI(owner, visualsOnly: true);
                    Debug.Log($"[SUPPLY CONVOY] {owner.Name}'s hand cards cost reduced by 1.");
                }
                break;

            case SpecialCardType.Logistics:
                // Draw 2 cards from deck to hand
                DoLogistics(owner, runner);
                runner?.RefreshBuyHandUI(owner);
                break;

            // ========================================
            // UNIT MANIPULATION CARDS
            // ========================================

            case SpecialCardType.KGB:
                // Steal a unit from enemy battlefield to your hand
                if (runner != null && enemy != null)
                {
                    runner.Special_DoKGBSteal(owner, enemy);
                }
                break;

            case SpecialCardType.POW:
                // Take a unit from enemy discard pile
                if (runner != null && enemy != null && enemy.Deck != null)
                {
                    runner.Special_DoPOWSelect(owner, enemy);
                }
                break;

            case SpecialCardType.PaperTiger:
                // Return any of your units to hand at 0 cost
                if (runner != null)
                {
                    runner.Special_DoPaperTiger(owner);
                }
                break;

            case SpecialCardType.Kamikaze:
                // Handled by BattleTestRunner targeting coroutine
                // Player clicks own air unit, then enemy unit to destroy both
                break;

            // ========================================
            // HEALING / RESTORATION CARDS
            // ========================================

            case SpecialCardType.Medic:
                // Restore 10 commander HP
                int healAmount = 10;
                int oldHP = owner.CommanderHP;
                owner.CommanderHP = Mathf.Min(owner.CommanderMaxHP, owner.CommanderHP + healAmount);
                int actualHeal = owner.CommanderHP - oldHP;
                Debug.Log($"[MEDIC] {owner.Name} restores {actualHeal} commander HP. Now: {owner.CommanderHP}/{owner.CommanderMaxHP}");
                break;
        }
    }

    private void ApplyEffectToUnitsWhere(string effectId, Player player, System.Func<Unit, bool> filter)
    {
        if (player == null) return;
        foreach (var row in player.Rows.Values)
        {
            foreach (var unit in row.Units)
            {
                if (unit != null && unit.IsAlive && filter(unit))
                {
                    Debug.Log($"[UnitEffect] Special card applying '{effectId}' to {player.Name}'s {unit.Name} ({unit.Type}, id={unit.InstanceId}).");
                    UnitEffectManager.ApplyEffect(effectId, unit.InstanceId);
                }
            }
        }
    }

    private void RemoveEffectFromUnitsWhere(string effectId, Player player, System.Func<Unit, bool> filter)
    {
        if (player == null) return;
        foreach (var row in player.Rows.Values)
        {
            foreach (var unit in row.Units)
            {
                if (unit != null && filter(unit))
                    UnitEffectManager.RemoveEffect(effectId, unit.InstanceId);
            }
        }
    }

    private void DoLogistics(Player owner, BattleTestRunner runner)
    {
        if (owner == null || owner.Deck == null || owner.Hand == null)
            return;

        int drawnCount = 0;

        // Draw up to 2 cards into hand
        for (int i = 0; i < 2; i++)
        {
            UnitCard drawn = owner.Deck.DrawOne();
            if (drawn == null)
                break;

            owner.Hand.Add(drawn);
            drawnCount++;
        }

        Debug.Log($"[LOGISTICS] {owner.Name} drew {drawnCount} card(s) into hand.");

        if (runner != null)
            runner.RefreshBuyHandUI(owner);
    }

    /// <summary>
    /// DEPRECATED: Units no longer have HP in new combat system.
    /// This method is kept for backwards compatibility but does nothing.
    /// </summary>
    [System.Obsolete("HP-based effects removed in Feb 2026 rework")]
    public void OnUnitAddedToBoard(Player owner, Unit unit)
    {
        // No longer applies HP damage - use GetATKModifierForUnit instead
    }

    /// <summary>
    /// DEPRECATED: Use GetATKModifierForUnit hook instead.
    /// </summary>
    [System.Obsolete("HP-based effects removed in Feb 2026 rework")]
    public void ApplyPreCombat(Player defender, BattleTestRunner runner)
    {
        // No longer applies HP damage - ATK modifiers are applied via GetATKModifierForUnit
    }

    // ========================================
    // HELPER METHODS FOR UI
    // ========================================

    /// <summary>
    /// Gets a display string for active status effects on a player.
    /// Useful for UI tooltips.
    /// </summary>
    public string GetActiveEffectsDisplay(Player player)
    {
        if (player == null) return "";

        var st = S(player);
        var effects = new List<string>();

        // Debuffs
        if (st.barbedWireActiveThisRound && !st.wireCutterActiveThisRound)
            effects.Add("Barbed Wire (-1 ATK Infantry)");
        if (st.landMinesActiveThisRound && !st.mechanicActiveThisRound)
            effects.Add("Land Mines (-1 ATK Vehicles)");
        if (st.radarJammerActiveThisRound && !st.hangarRatActiveThisRound)
            effects.Add("Radar Jammer (-1 ATK Ranged/Air)");
        if (st.empActiveThisRound)
            effects.Add("EMP Strike (-1 ATK Vehicles & Ranged/Air)");

        // Buffs
        if (st.trenchesActiveThisRound)
            effects.Add("Trenches (+1 ATK Infantry)");

        // Counters
        if (st.wireCutterActiveThisRound)
            effects.Add("Wire Cutter (Barbed Wire nullified)");
        if (st.mechanicActiveThisRound)
            effects.Add("Mechanic (Land Mines nullified)");
        if (st.hangarRatActiveThisRound)
            effects.Add("Hangar Rat (Radar Jammer nullified)");

        return string.Join("\n", effects);
    }

    /// <summary>
    /// Checks if a specific debuff is active and not nullified.
    /// </summary>
    public bool IsDebuffActive(Player player, SpecialCardType debuffType)
    {
        if (player == null) return false;
        var st = S(player);

        switch (debuffType)
        {
            case SpecialCardType.BarbedWire:
                return st.barbedWireActiveThisRound && !st.wireCutterActiveThisRound;
            case SpecialCardType.LandMines:
                return st.landMinesActiveThisRound && !st.mechanicActiveThisRound;
            case SpecialCardType.RadarJammer:
                return st.radarJammerActiveThisRound && !st.hangarRatActiveThisRound;
            case SpecialCardType.EMPStrike:
                return st.empActiveThisRound;
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks if a specific buff is active.
    /// </summary>
    public bool IsBuffActive(Player player, SpecialCardType buffType)
    {
        if (player == null) return false;
        var st = S(player);

        switch (buffType)
        {
            case SpecialCardType.Trenches:
                return st.trenchesActiveThisRound;
            default:
                return false;
        }
    }
}
